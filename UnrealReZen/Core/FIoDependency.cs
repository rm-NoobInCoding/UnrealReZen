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
            const EIoContainerHeaderVersion version = EIoContainerHeaderVersion.OptionalSegmentPackages;

            ms.Write(Constants.UE5_DepFile_Sig);
            ms.Write((int)version);
            ms.Write(Deps.ThisPackageID);

            var ids = Deps.ChunkIDToDependencies.Keys.ToList();
            ids.Sort();

            ms.Write(ids.Count);
            foreach (var id in ids) ms.Write(id);

            var storeEntriesBytes = BuildStoreEntriesSection(ids);
            ms.Write(storeEntriesBytes.Length);
            ms.Write(storeEntriesBytes);

            ms.Write(0);
            ms.Write(0);

            ms.Write(0);

            ms.Write(0);

            ms.Write(0);

            return ms.ToArray();
        }

        private byte[] BuildStoreEntriesSection(List<ulong> ids)
        {
            const int HeaderSize = 24;
            const int PackageIdSize = 8;
            const int ShaderMapHashSize = 20;

            var headersSize = ids.Count * HeaderSize;
            using var headers = new MemoryStream();
            using var blob = new MemoryStream();

            for (int i = 0; i < ids.Count; i++)
            {
                var entry = Deps.ChunkIDToDependencies[ids[i]];
                var entryStart = i * HeaderSize;

                headers.Write(entry.ExportCount);
                headers.Write(entry.ExportBundleCount);

                WriteCArrayView(headers, blob, entryStart + 8, headersSize, entry.ImportedPackages.Length,
                    () => { foreach (var p in entry.ImportedPackages) blob.Write(p.id); },
                    PackageIdSize);

                WriteCArrayView(headers, blob, entryStart + 16, headersSize, entry.ShaderMapHashes.Length,
                    () => { foreach (var h in entry.ShaderMapHashes) blob.Write(h.Hash); },
                    ShaderMapHashSize);
            }

            var result = new byte[headers.Length + blob.Length];
            Buffer.BlockCopy(headers.ToArray(), 0, result, 0, (int)headers.Length);
            Buffer.BlockCopy(blob.ToArray(), 0, result, (int)headers.Length, (int)blob.Length);
            return result;
        }

        private static void WriteCArrayView(MemoryStream headers, MemoryStream blob, int initialPos, int headersSize, int count, Action writeItems, int _)
        {
            if (count == 0)
            {
                headers.Write(0);
                headers.Write(0);
                return;
            }
            var dataPos = headersSize + (int)blob.Length;
            var offsetFromThis = dataPos - initialPos;
            headers.Write(count);
            headers.Write(offsetFromThis);
            writeItems();
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

}
