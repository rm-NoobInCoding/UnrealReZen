using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Windows.Forms;

namespace UEcastocLib
{
    public static class Constants
    {
        public const string MagicUtoc = "-==--==--==--==-";
        public const string UnrealSignature = "\xC1\x83\x2A\x9E";
        public const string MountPoint = "../../../";
        public const uint NoneEntry = 0xFFFFFFFF;
        public const string DepFileName = "dependencies";
    }
    public static class UTocDataExtensions
    {
        public static void RecursiveDirExplorer(string parentPath, uint pDir, List<GameFilePathData> outputList,
            List<string> strTable, List<FIoDirectoryIndexEntry> dirs, List<FIoFileIndexEntry> files)
        {
            uint dirIdx = dirs[(int)pDir].FirstChildEntry;
            uint fileIdx = dirs[(int)pDir].FirstFileEntry;

            if (dirIdx == Constants.NoneEntry && fileIdx == Constants.NoneEntry)
            {
                return;
            }

            while (dirIdx != Constants.NoneEntry)
            {
                var dirEntry = dirs[(int)dirIdx];
                var newDirName = Path.Combine(parentPath, strTable[(int)dirEntry.Name]);
                RecursiveDirExplorer(newDirName, dirIdx, outputList, strTable, dirs, files);
                dirIdx = dirEntry.NextSiblingEntry;
            }

            while (fileIdx != Constants.NoneEntry)
            {
                var fileEntry = files[(int)fileIdx];
                var filePath = Path.Combine(parentPath, strTable[(int)fileEntry.Name]);
                outputList.Add(new GameFilePathData { FilePath = filePath, UserData = fileEntry.UserData });
                fileIdx = fileEntry.NextFileEntry;
            }
        }

        public static Tuple<string, string[]> ParseDirectoryIndex(byte[] directoryIndexData, int numberOfChunks)
        {
            using (MemoryStream stream = new MemoryStream(directoryIndexData))
            using (BinaryReader reader = new BinaryReader(stream))
            {
                uint size = reader.ReadUInt32();
                byte[] mntPtBytes = reader.ReadBytes((int)size);
                string mountPointName = Encoding.ASCII.GetString(mntPtBytes, 0, mntPtBytes.Length - 1);

                if (!mountPointName.StartsWith(Constants.MountPoint))
                {
                    return new Tuple<string, string[]>(string.Empty, null);
                }

                string mountpoint = mountPointName.Substring(Constants.MountPoint.Length);

                uint dirCount = reader.ReadUInt32();
                var dirs = new List<FIoDirectoryIndexEntry>();
                for (int i = 0; i < dirCount; i++)
                {
                    var dirEntry = new FIoDirectoryIndexEntry();
                    dirEntry.Read(reader);
                    dirs.Add(dirEntry);
                }

                uint fileCount = reader.ReadUInt32();
                var files = new List<FIoFileIndexEntry>();
                for (int i = 0; i < fileCount; i++)
                {
                    var fileEntry = new FIoFileIndexEntry();
                    fileEntry.Read(reader);
                    files.Add(fileEntry);
                }

                uint stringCount = reader.ReadUInt32();
                var strTable = new List<string>();
                for (int i = 0; i < stringCount; i++)
                {
                    size = reader.ReadUInt32();
                    byte[] strBytes = reader.ReadBytes((int)size);
                    string str = Encoding.ASCII.GetString(strBytes, 0, strBytes.Length - 1);
                    strTable.Add(str);
                }

                if (dirs[0].Name != Constants.NoneEntry)
                {
                    return new Tuple<string, string[]>(string.Empty, null);
                }

                var gamefilePaths = new List<GameFilePathData>();

                RecursiveDirExplorer("", 0, gamefilePaths, strTable, dirs, files);

                string[] orderedPaths = new string[numberOfChunks];
                foreach (var v in gamefilePaths)
                {
                    orderedPaths[v.UserData] = v.FilePath;
                }

                return new Tuple<string, string[]>(mountpoint, orderedPaths);
            }
        }

        public static UTocHeader ParseUtocHeader(byte[] utocData)
        {
            using (MemoryStream stream = new MemoryStream(utocData))
            using (BinaryReader reader = new BinaryReader(stream))
            {
                var header = new UTocHeader();
                header.Magic = Encoding.ASCII.GetString(reader.ReadBytes(16));
                header.Version = reader.ReadByte();
                reader.ReadBytes(3); // Reserved0
                header.HeaderSize = reader.ReadUInt32();
                header.EntryCount = reader.ReadUInt32();
                header.CompressedBlockEntryCount = reader.ReadUInt32();
                header.CompressedBlockEntrySize = reader.ReadUInt32();
                header.CompressionMethodNameCount = reader.ReadUInt32();
                header.CompressionMethodNameLength = reader.ReadUInt32();
                header.CompressionBlockSize = reader.ReadUInt32();
                header.DirectoryIndexSize = reader.ReadUInt32();
                header.PartitionCount = reader.ReadUInt32();
                header.ContainerID = new FIoContainerID(reader.ReadUInt64());
                header.EncryptionKeyGuid = new FGuid { A = reader.ReadUInt32(), B = reader.ReadUInt32(), C = reader.ReadUInt32(), D = reader.ReadUInt32() };
                header.ContainerFlags = (EIoContainerFlags)reader.ReadByte();
                reader.ReadBytes(3); // Reserved1
                header.TocChunkPerfectHashSeedsCount = reader.ReadUInt32();
                header.PartitionSize = reader.ReadUInt64();
                header.TocChunksWithoutPerfectHashCount = reader.ReadUInt32();
                reader.ReadBytes(36); // Reserved2

                //MessageBox.Show(header.ContainerFlags + "");

                if (header.Magic != Constants.MagicUtoc)
                {
                    throw new Exception("magic word of .utoc file was not found");
                }
                if(header.Version < VersionConstants.VersionDirectoryIndex)
                {
                    throw new Exception("utoc version is outdated");
                }
                if(header.Version > VersionConstants.VersionLatest)
                {
                    throw new Exception("too new utoc version");
                }
                if(header.CompressedBlockEntrySize != 12)
                {
                    throw new Exception("compressed block entry size was incorrect");
                }

                return header;
            }
        }

        public static List<FIoStoreTocCompressedBlockEntry> ParseCompressionBlocks(byte[] compressedBlocksData)
        {
            List<FIoStoreTocCompressedBlockEntry> compressionBlocks = new List<FIoStoreTocCompressedBlockEntry>();

            using (MemoryStream stream = new MemoryStream(compressedBlocksData))
            using (BinaryReader reader = new BinaryReader(stream))
            {
                while (reader.BaseStream.Position < reader.BaseStream.Length)
                {
                    var block = new FIoStoreTocCompressedBlockEntry();
                    block.Read(reader);
                    compressionBlocks.Add(block);
                }
            }

            return compressionBlocks;
        }

        public static List<string> ParseCompressionMethods(byte[] compressionMethodsData, int count)
        {
            List<string> compressionMethods = new List<string> { "None" };

            using (MemoryStream stream = new MemoryStream(compressionMethodsData))
            using (BinaryReader reader = new BinaryReader(stream))
            {
                for (uint i = 0; i < count; i++)
                {
                    byte[] methodBytes = reader.ReadBytes(32);
                    string method = Encoding.ASCII.GetString(methodBytes).Replace("\0", "");
                    compressionMethods.Add(method);
                }
            }

            return compressionMethods;
        }

        public static List<FIoChunkID> ParseChunkIds(byte[] chunkIdsData)
        {
            List<FIoChunkID> chunkIds = new List<FIoChunkID>();
            using (MemoryStream stream = new MemoryStream(chunkIdsData))
            using (BinaryReader reader = new BinaryReader(stream))
            {
                while (reader.BaseStream.Position < reader.BaseStream.Length)
                {
                    var ChunkID = new FIoChunkID(reader.ReadUInt64(), reader.ReadUInt16(), reader.ReadByte(), reader.ReadByte());
                    chunkIds.Add(ChunkID);
                }
            }
            return chunkIds;
        }

        public static List<FIoOffsetAndLength> ParseOffsetAndLength(byte[] offsetAndLengthData)
        {
            List<FIoOffsetAndLength> offsetAndLength = new List<FIoOffsetAndLength>();
            using (MemoryStream stream = new MemoryStream(offsetAndLengthData))
            using (BinaryReader reader = new BinaryReader(stream))
            {
                while (reader.BaseStream.Position < reader.BaseStream.Length)
                {
                    var block = new FIoOffsetAndLength(reader.ReadBytes(5), reader.ReadBytes(5));
                    offsetAndLength.Add(block);
                }
            }
            return offsetAndLength;
        }

        public static List<FIoStoreTocEntryMeta> ParseChunkMeta(byte[] metaData)
        {
            List<FIoStoreTocEntryMeta> ChunkMeta = new List<FIoStoreTocEntryMeta>();
            using (MemoryStream stream = new MemoryStream(metaData))
            using (BinaryReader reader = new BinaryReader(stream))
            {
                while (reader.BaseStream.Position < reader.BaseStream.Length)
                {
                    var Hash = new FIoChunkHash(reader.ReadBytes(20), reader.ReadBytes(12));
                    var Flag = (FIoStoreTocEntryMetaFlags)reader.ReadByte();
                    var Chunk = new FIoStoreTocEntryMeta();
                    Chunk.ChunkHash = Hash;
                    Chunk.Flags = Flag;
                    ChunkMeta.Add(Chunk);
                }
                return ChunkMeta;
            }
        }

    }
}
