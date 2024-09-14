namespace UnrealReZen.Core
{
    public enum EIoContainerFlags : byte
    {
        NoneContainerFlag = 0,
        CompressedContainerFlag = 1 << 0,
        EncryptedContainerFlag = 1 << 1,
        SignedContainerFlag = 1 << 2,
        IndexedContainerFlag = 1 << 3
    }

    public enum FIoDependencyFormat
    {
        UE4,
        UE5
    }

    public enum FIoStoreTocEntryMetaFlags : byte
    {
        NoneMetaFlag,
        CompressedMetaFlag,
        MemoryMappedMetaFlag
    }

    public enum EIoContainerHeaderVersion
    {
        BeforeVersionWasAdded = -1,
        Initial = 0,
        LocalizedPackages = 1,
        OptionalSegmentPackages = 2,
        NoExportInfo = 3,
    }
}
