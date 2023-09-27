using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace UEcastocLib
{
    public static class VersionConstants
    {
        public const byte VersionInvalid = 0;
        public const byte VersionInitial = 1;
        public const byte VersionDirectoryIndex = 2;
        public const byte VersionPartitionSize = 3;
        public const byte VersionPerfectHash = 4;
        public const byte VersionPerfectHashWithOverflow = 5;
        public const byte VersionLatest = 6;
    }
    public class FGuid
    {
        public uint A, B, C, D;
    }

    public class UTocHeader
    {
        public string Magic;                            // [16]byte
        public byte Version;                            // uint8
        public byte[] Reserved0 = new byte[3];          // [3]uint8
        public uint HeaderSize;                         // uint32
        public uint EntryCount;                         // uint32
        public uint CompressedBlockEntryCount;          // uint32
        public uint CompressedBlockEntrySize;           // uint32
        public uint CompressionMethodNameCount;         // uint32
        public uint CompressionMethodNameLength;        // uint32
        public uint CompressionBlockSize;               // uint32
        public uint DirectoryIndexSize;                 // uint32
        public uint PartitionCount;                     // uint32
        public FIoContainerID ContainerID;              // FIoContainerID
        public FGuid EncryptionKeyGuid;                 // FGuid
        public EIoContainerFlags ContainerFlags;        // EIoContainerFlags
        public byte[] Reserved1 = new byte[3];          // [3]byte
        public uint TocChunkPerfectHashSeedsCount;      // uint32
        public ulong PartitionSize;                     // uint64
        public uint TocChunksWithoutPerfectHashCount;   // uint32
        public byte[] Reserved2 = new byte[44];         // [44]byte

       
        public void Write(MemoryStream br)
        {
            br.Write(Magic, Encoding.UTF8);
            br.Write(Version);
            br.Write(Reserved0);
            br.Write(HeaderSize);
            br.Write(EntryCount);
            br.Write(CompressedBlockEntryCount);
            br.Write(CompressedBlockEntrySize);
            br.Write(CompressionMethodNameCount);
            br.Write(CompressionMethodNameLength);
            br.Write(CompressionBlockSize);
            br.Write(DirectoryIndexSize);
            br.Write(PartitionCount);
            br.Write(ContainerID.Value);
            br.Write(0);
            br.Write(0);
            br.Write(0);
            br.Write(0);
            br.Write((byte)ContainerFlags);
            br.Write(Reserved1);
            br.Write(TocChunkPerfectHashSeedsCount);
            br.Write(PartitionSize);
            br.Write(TocChunksWithoutPerfectHashCount);
            br.Write(Reserved2);


        }
        public bool IsEncrypted()
        {
            return (ContainerFlags & EIoContainerFlags.EncryptedContainerFlag) != 0;
        }
        public int SizeOf()
        {
            return 144;
        }
    }

    public class GameFileMetaData
    {
        public string FilePath;
        public FIoChunkID ChunkID;
        public FIoOffsetAndLength OffLen;
        public List<FIoStoreTocCompressedBlockEntry> CompressionBlocks;
        public FIoStoreTocEntryMeta Metadata;
    }

    public class GameFilePathData
    {
        public string FilePath;
        public uint UserData;
    }



    public static class UTocDataParser
    {
        public static UTocData ParseUtocFile(string utocFile, byte[] aesKey)
        {
            var udata = new UTocData();

            byte[] utocData = File.ReadAllBytes(utocFile);
            udata.Header = UTocDataExtensions.ParseUtocHeader(utocData);

            if (udata.IsEncrypted())
            {
                if (aesKey == null || aesKey.Length == 0)
                {
                    throw new Exception("Encrypted file, but no AES key was provided! Please pass the AES key as a byte array.");
                }


            }

            long ReadBefore = (long)udata.Header.HeaderSize;

            byte[] chunkIdsData = utocData.Skip((int)ReadBefore).Take((int)udata.Header.EntryCount * 12).ToArray();
            var chunkIds = UTocDataExtensions.ParseChunkIds(chunkIdsData);
            ReadBefore += chunkIdsData.Length;

            byte[] OffsetAndLengthsData = utocData.Skip((int)ReadBefore).Take((int)udata.Header.EntryCount * 10).ToArray();
            var OffsetAndLength = UTocDataExtensions.ParseOffsetAndLength(OffsetAndLengthsData);
            ReadBefore += OffsetAndLengthsData.Length;

            byte[] compressionBlocksData = utocData.Skip((int)ReadBefore).Take((int)udata.Header.CompressedBlockEntrySize * (int)udata.Header.CompressedBlockEntryCount).ToArray();
            var customCompressionBlocks = UTocDataExtensions.ParseCompressionBlocks(compressionBlocksData);
            ReadBefore += compressionBlocksData.Length;

            byte[] compressionMethodsData = utocData.Skip((int)ReadBefore).Take((int)udata.Header.CompressionMethodNameLength * (int)udata.Header.CompressionMethodNameCount).ToArray();
            udata.CompressionMethods = UTocDataExtensions.ParseCompressionMethods(compressionMethodsData, (int)udata.Header.CompressionMethodNameCount);
            ReadBefore += compressionMethodsData.Length;

            if (udata.Header.ContainerFlags.HasFlag(EIoContainerFlags.SignedContainerFlag))
            {
                int hSize = BitConverter.ToInt32(utocData.Skip((int)ReadBefore).Take(4).ToArray(), 0);
                ReadBefore += 4 + hSize * 2 + 20 * udata.Header.CompressedBlockEntryCount;
            }

            byte[] directoryIndexData = utocData.Skip((int)ReadBefore).Take((int)udata.Header.DirectoryIndexSize).ToArray();
            ReadBefore += directoryIndexData.Length;
            if (udata.IsEncrypted())
            {
                directoryIndexData = Helpers.DecryptAES(directoryIndexData, aesKey);
            }
           
            var directoryIndex = UTocDataExtensions.ParseDirectoryIndex(directoryIndexData, (int)udata.Header.EntryCount);
            string mountPoint = directoryIndex.Item1;
            string[] orderedPaths = directoryIndex.Item2;

            if (orderedPaths == null)
            {
                throw new Exception("Something went wrong parsing the directory index!");
            }

            byte[] chunksMetaData = utocData.Skip((int)ReadBefore).Take((int)udata.Header.EntryCount * 33).ToArray();
            var chunksMeta = UTocDataExtensions.ParseChunkMeta(chunksMetaData);
            ReadBefore += chunksMetaData.Length;
            for (int i = 0; i < orderedPaths.Length; i++)
            {
                string path = orderedPaths[i];
                if (path == null)
                {
                    if (chunkIds[i].Type != 10 && udata.Header.ContainerID.Value != chunkIds[i].ID)
                    {
                        // if the name is empty, the type is 10, and the chunkID doesnt match, then it's not the dependencies
                        continue;
                    }
                    path = Constants.DepFileName; // Ensure the dependencies file has the correct name

                }
                uint startBlock = (uint)(OffsetAndLength[i].GetOffset() / udata.Header.CompressionBlockSize);
                uint endBlock = (uint)(startBlock + (OffsetAndLength[i].GetLength() + (udata.Header.CompressionBlockSize - 1)) / udata.Header.CompressionBlockSize);
                var blocks = customCompressionBlocks.GetRange((int)startBlock, (int)(endBlock - startBlock));

                udata.Files.Add(new GameFileMetaData
                {
                    FilePath = path,
                    ChunkID = chunkIds[i],
                    OffLen = OffsetAndLength[i],
                    CompressionBlocks = blocks,
                    Metadata = new FIoStoreTocEntryMeta(),
                });
            }

            if (!udata.Files.Exists(f => f.FilePath == Constants.DepFileName))
            {
                throw new Exception("Couldn't find dependencies");
            }

            return udata;
        }
    }
}


