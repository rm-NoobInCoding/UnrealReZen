using CUE4Parse.UE4.IO.Objects;
using UnrealReZen.Core.Helpers;

namespace UnrealReZen.Core
{

    public class DependenciesData
    {
        public ulong ThisPackageID { get; set; }
        public required Dictionary<ulong, FFilePackageStoreEntry> ChunkIDToDependencies { get; set; }
    }

    public class ManifestFile
    {
        public required string Filepath { get; set; }
        public required FIoChunkID ChunkID { get; set; }
    }

    public class Dependency
    {
        
        public required List<ManifestFile> Files { get; set; }

        public required DependenciesData Deps { get; set; }
        public byte[] WriteDependenciesAsUE4()
        {
            using var ms = new MemoryStream();

            var hdr = new DepsHeader_UE4
            {
                ThisPackageID = Deps.ThisPackageID,
                NumberOfIDs = (ulong)Deps.ChunkIDToDependencies.Count,
                IDSize = 8,
                Padding = [0x00, 0x00, 0x64, 0xC1],
                ZeroBytes = new byte[4],
                NumberOfIDsAgain = (uint)Deps.ChunkIDToDependencies.Count
            };
            hdr.Write(ms);

            var ids = Deps.ChunkIDToDependencies.Keys.ToList();
            var totalNumberOfDependencies = 0;
            foreach (var k in Deps.ChunkIDToDependencies)
            {
                totalNumberOfDependencies += k.Value.ImportedPackages.Length;
            }

            ids.Sort();
            ids.Reverse();

            foreach (var id in ids)
            {
                ms.Write(id);
            }

            var flength = (uint)(Deps.ChunkIDToDependencies.Count * DepLinks_UE4.SizeOf()) + totalNumberOfDependencies * 8;
            ms.Write((uint)flength);

            var endOfDeps = flength - (uint)(totalNumberOfDependencies * 8);
            var depsToWrite = new List<ulong>();
            for (int i = 0; i < ids.Count; i++)
            {
                var id = ids[i];
                var entry = Deps.ChunkIDToDependencies[id];
                var link = new DepLinks_UE4
                {
                    FileSize = 0,
                    ExportCount = entry.ExportCount,
                    ExportBundleCount = entry.ExportBundleCount,
                    ImportedPackages = entry.ImportedPackages.Select(a => a.id).ToList(),
                    ShaderMapHashes = entry.ShaderMapHashes.Select(a => a.Hash).ToList(),
                    SomeIndex = 0,
                    DependencyPackages = entry.ImportedPackages.Length,
                    Offset = 0
                };
                if (link.DependencyPackages != 0)
                {
                    var offsetFieldOffset = i * DepLinks_UE4.SizeOf() + 16 + 8;
                    var target = (int)endOfDeps + depsToWrite.Count * 8;
                    depsToWrite.AddRange(entry.ImportedPackages.Select(a => a.id));
                    link.Offset = (uint)(target - offsetFieldOffset);
                }
                link.Write(ms);

            }
            foreach (var depLink in depsToWrite)
            {
                ms.Write(depLink);
            }
            var nulls = (ulong)0;
            ms.Write(nulls);
            return ms.ToArray();
        }
        public byte[] WriteDependenciesAsUE5()
        {
            using var ms = new MemoryStream();

            var hdr = new DepsHeader_UE5
            {
                Signature = Constants.UE5_DepFile_Sig,
                Version = EIoContainerHeaderVersion.OptionalSegmentPackages,
                ContainerId = Deps.ThisPackageID,
                PackageCount = Deps.ChunkIDToDependencies.Count
            };
            hdr.Write(ms);

            var ids = Deps.ChunkIDToDependencies.Keys.ToList();
            var totalNumberOfDependencies = 0;
            foreach (var k in Deps.ChunkIDToDependencies)
            {
                totalNumberOfDependencies += k.Value.ImportedPackages.Length;
            }

            ids.Sort();
            ids.Reverse();

            foreach (var id in ids)
            {
                ms.Write(id);
            }

            MemoryStream storeEntries = new MemoryStream();
            MemoryStream storeEntriesbuffer = new MemoryStream();

            var depsToWrite = new List<ulong>();
            for (int i = 0; i < ids.Count; i++)
            {
                var id = ids[i];
                var entry = Deps.ChunkIDToDependencies[id];

                var link = new DepLinks_UE5
                {
                    ExportCount = entry.ExportCount,
                    ExportBundleCount = entry.ExportBundleCount,
                    ImportedPackages = entry.ImportedPackages.Select(a => a.id).ToList(),
                    BaseOffset = storeEntriesbuffer.Length + (ids.Count * 24 - storeEntries.Position),
                    ShaderMapHashes = entry.ShaderMapHashes.Select(a => a.Hash).ToList(),
                };
                foreach (var ImpPak in entry.ImportedPackages)
                {
                    storeEntriesbuffer.Write(ImpPak.id);
                }
                foreach (var ShMapHash in entry.ShaderMapHashes)
                {
                    storeEntriesbuffer.Write(ShMapHash.Hash);
                }
                link.Write(storeEntries);

            }
            ms.Write((int)storeEntries.Length + (int)storeEntriesbuffer.Length);
            ms.Write(storeEntries.ToArray());
            ms.Write(storeEntriesbuffer.ToArray());
            ms.Write(new byte[20]);
            return ms.ToArray();
        }
    }

    public class DepsHeader_UE4
    {
        public EIoContainerHeaderVersion Version { get; set; }
        public ulong ThisPackageID { get; set; }
        public ulong NumberOfIDs { get; set; }
        public uint IDSize { get; set; }
        public required byte[] Padding { get; set; }
        public required byte[] ZeroBytes { get; set; }
        public uint NumberOfIDsAgain { get; set; }

        public void Write(MemoryStream ms)
        {
            ms.Write(ThisPackageID);
            ms.Write(NumberOfIDs);
            ms.Write(IDSize);
            ms.Write(Padding);
            ms.Write(ZeroBytes);
            ms.Write(NumberOfIDsAgain);

        }
    }

    public class DepLinks_UE4
    {
        public ulong FileSize { get; set; }
        public int ExportCount { get; set; }
        public int ExportBundleCount { get; set; }
        public ulong SomeIndex { get; set; }
        public int DependencyPackages { get; set; }
        public uint Offset { get; set; }
        public required List<ulong> ImportedPackages { get; set; }
        public required List<byte[]> ShaderMapHashes { get; set; }
        public static int SizeOf()
        {
            return sizeof(ulong) * 2 + sizeof(uint) * 4;
        }
        public void Write(MemoryStream br)
        {
            br.Write(FileSize);
            br.Write(ExportCount);
            br.Write(ExportBundleCount);
            br.Write(SomeIndex);
            br.Write(DependencyPackages);
            br.Write(Offset);
        }
    }

    public class DepsHeader_UE5
    {
        public int Signature { get; set; }
        public EIoContainerHeaderVersion Version { get; set; }
        public ulong ContainerId { get; set; }
        public int PackageCount { get; set; }

        public void Write(MemoryStream ms)
        {
            ms.Write(Signature);
            if ((int)Version > -1) ms.Write((int)Version);
            ms.Write(ContainerId);
            if ((int)Version >= 2) ms.Write(PackageCount);
        }
    }

    public class DepLinks_UE5
    {
        public int ExportCount { get; set; }
        public int ExportBundleCount { get; set; }
        public required List<ulong> ImportedPackages { get; set; }
        public required List<byte[]> ShaderMapHashes { get; set; }
        public long BaseOffset { get; set; }
        public void Write(MemoryStream br)
        {
            br.Write(ExportCount);
            br.Write(ExportBundleCount);
            br.Write(ImportedPackages.Count);
            if (ImportedPackages.Count > 0) br.Write(BaseOffset - 4); else br.Write(0);
            br.Write(ShaderMapHashes.Count);
            if (ShaderMapHashes.Count > 0) br.Write(BaseOffset + ImportedPackages.Count * 8); else br.Write(0);
        }
    }

    public static class ManifestData
    {
        public const int UE5_DepFile_Sig = 1232028526;
        
    }

}
