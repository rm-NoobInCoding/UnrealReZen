using CUE4Parse.UE4.Objects.Core.Misc;
using System.Text;
using UnrealReZen.Core.Helpers;

namespace UnrealReZen.Core
{
    public class UToc(UTocHeader uTocHeader, List<AssetMetadata> gameFileMetaDatas, string mountPoint, List<string> supportedCompressionMethods)
    {
        public Stream TocStream;
        public byte[] AESKey;
        public string UCasPath;
        public UTocHeader HeaderTable = uTocHeader;
        public List<FIoChunkID> ChunkIdsTable;
        public List<FIoOffsetAndLength> OffsetAndLengthsTable;
        public List<FIoPerfectHashSeeds> PerfectHashSeedsTable;
        public List<FIoStoreTocCompressedBlockEntry> CompressionBlocksDataTable;
        public List<string> SupportedCompressionMethods = supportedCompressionMethods;
        public byte[] SigBlock;
        public string MountPoint = mountPoint;
        public List<string> OrderedPaths;
        public List<FIoStoreTocEntryMeta> ChunksMeta;
        public List<AssetMetadata> Files = gameFileMetaDatas;
        public byte[] aesKey;
    }

    public class UTocHeader
    {
        public string Magic;
        public byte Version;
        public byte[] Reserved0 = new byte[3];
        public uint HeaderSize;
        public uint EntryCount;
        public uint CompressedBlockEntryCount;
        public uint CompressedBlockEntrySize;
        public uint CompressionMethodNameCount;
        public uint CompressionMethodNameLength;
        public uint CompressionBlockSize;
        public uint DirectoryIndexSize;
        public uint PartitionCount;
        public FIoContainerID ContainerID;
        public FGuid EncryptionKeyGuid;
        public EIoContainerFlags ContainerFlags;
        public byte[] Reserved1 = new byte[3];
        public uint TocChunkPerfectHashSeedsCount;
        public ulong PartitionSize;
        public uint TocChunksWithoutPerfectHashCount;
        public byte[] Reserved2 = new byte[44];

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

        public static int SizeOf => 144;
    }

    public class AssetMetadata
    {
        public required string FilePath;
        public required FIoChunkID ChunkID;
        public required FIoOffsetAndLength OffLen;
        public required List<FIoStoreTocCompressedBlockEntry> CompressionBlocks;
        public required FIoStoreTocEntryMeta Metadata;
    }

    public class FIoContainerID(ulong value)
    {
        public ulong Value { get; set; } = value;
    }
}

