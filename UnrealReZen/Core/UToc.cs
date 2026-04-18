using CUE4Parse.UE4.Objects.Core.Misc;
using System.Text;
using UnrealReZen.Core.Helpers;

namespace UnrealReZen.Core
{
    public class UTocHeader
    {
        public required string Magic { get; init; }
        public required byte Version { get; init; }
        public byte[] Reserved0 { get; init; } = new byte[3];
        public required uint HeaderSize { get; init; }
        public required uint EntryCount { get; init; }
        public required uint CompressedBlockEntryCount { get; init; }
        public required uint CompressedBlockEntrySize { get; init; }
        public required uint CompressionMethodNameCount { get; init; }
        public required uint CompressionMethodNameLength { get; init; }
        public required uint CompressionBlockSize { get; init; }
        public required uint DirectoryIndexSize { get; init; }
        public required uint PartitionCount { get; init; }
        public required FIoContainerID ContainerID { get; init; }
        public FGuid EncryptionKeyGuid { get; init; }
        public required EIoContainerFlags ContainerFlags { get; init; }
        public byte[] Reserved1 { get; init; } = new byte[3];
        public uint TocChunkPerfectHashSeedsCount { get; init; }
        public required ulong PartitionSize { get; init; }
        public uint TocChunksWithoutPerfectHashCount { get; init; }
        public byte[] Reserved2 { get; init; } = new byte[44];

        public const int SizeOf = 144;

        public void Write(MemoryStream ms)
        {
            ms.Write(Magic, Encoding.UTF8);
            ms.Write(Version);
            ms.Write(Reserved0);
            ms.Write(HeaderSize);
            ms.Write(EntryCount);
            ms.Write(CompressedBlockEntryCount);
            ms.Write(CompressedBlockEntrySize);
            ms.Write(CompressionMethodNameCount);
            ms.Write(CompressionMethodNameLength);
            ms.Write(CompressionBlockSize);
            ms.Write(DirectoryIndexSize);
            ms.Write(PartitionCount);
            ms.Write(ContainerID.Value);
            ms.Write(0);
            ms.Write(0);
            ms.Write(0);
            ms.Write(0);
            ms.Write((byte)ContainerFlags);
            ms.Write(Reserved1);
            ms.Write(TocChunkPerfectHashSeedsCount);
            ms.Write(PartitionSize);
            ms.Write(TocChunksWithoutPerfectHashCount);
            ms.Write(Reserved2);
        }
    }

    public class AssetMetadata
    {
        public required string FilePath { get; set; }
        public required FIoChunkID ChunkID { get; init; }
        public required FIoOffsetAndLength OffLen { get; init; }
        public required List<FIoStoreTocCompressedBlockEntry> CompressionBlocks { get; init; }
        public required FIoStoreTocEntryMeta Metadata { get; init; }
    }

    public class FIoContainerID(ulong value)
    {
        public ulong Value { get; } = value;
    }
}
