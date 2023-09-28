using Newtonsoft.Json;
using System.Collections.Generic;

namespace UEcastocLib
{

    public class FileDependency
    {

        [JsonProperty("exportObjects")]
        public uint ExportObjects { get; set; }

        [JsonProperty("exportBundleCount")]
        public uint MostlyOne { get; set; }

        [JsonProperty("dependencies")]
        public List<ulong> Dependencies { get; set; }

        [JsonProperty("shaderMapHashes")]
        public List<string> ShaderMapHashes { get; set; }

    }

    public class Dependencies
    {
        [JsonProperty("packageID")]
        public ulong ThisPackageID { get; set; }

        [JsonProperty("ChunkIDToDependencies")]
        public Dictionary<ulong, FileDependency> ChunkIDToDependencies { get; set; }
    }

    public class DepsHeader
    {
        public EIoContainerHeaderVersion Version { get; set; }
        public ulong ThisPackageID { get; set; }
        public ulong NumberOfIDs { get; set; }
        public uint IDSize { get; set; }
        public byte[] Padding { get; set; }
        public byte[] ZeroBytes { get; set; }
        public uint NumberOfIDsAgain { get; set; }

        //public DepsHeader(ulong thisPackageID, ulong numberOfIDs, uint iDSize, uint unknown, byte[] padding, uint numberOfIDsAgain)
        //{
        //    ThisPackageID = thisPackageID;
        //    NumberOfIDs = numberOfIDs;
        //    IDSize = iDSize;
        //    Unknown = unknown;
        //    Padding = padding;
        //    NumberOfIDsAgain = numberOfIDsAgain;
        //}
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

    public enum EIoContainerHeaderVersion
    {
        BeforeVersionWasAdded = -1, // Custom constant to indicate pre-UE5 data
        Initial = 0,
        LocalizedPackages = 1,
        OptionalSegmentPackages = 2,
        NoExportInfo = 3,
    }

    public class DepLinks
    {
        public ulong FileSize { get; set; }
        public uint ExportObjects { get; set; }
        public uint MostlyOne { get; set; }
        public ulong SomeIndex { get; set; }
        public uint DependencyPackages { get; set; }
        public uint Offset { get; set; }
        public List<ulong> Deps { get; set; }
        public List<string> Hashs { get; set; }
        public static int SizeOf()
        {
            return sizeof(ulong) * 2 + sizeof(uint) * 4;
        }
        public void Write(MemoryStream br)
        {
            br.Write(FileSize);
            br.Write(ExportObjects);
            br.Write(MostlyOne);
            br.Write(SomeIndex);
            br.Write(DependencyPackages);
            br.Write(Offset);
        }
    }

    public class ParseDependencies
    {
        public DepsHeader Hdr { get; set; }
        public List<ulong> IDs { get; set; }
        public uint FileLength { get; set; }
        public List<DepLinks> Conns { get; set; }
        public long OffsetAfterConss { get; set; }
        public List<ulong> Deps { get; set; }
        public Dictionary<ulong, DepLinks> IDToConn { get; set; }

        public Dependencies ExtractDependencies()
        {
            var d = new Dependencies();
            d.ThisPackageID = Hdr.ThisPackageID;
            d.ChunkIDToDependencies = new Dictionary<ulong, FileDependency>();
            for (int i = 0; i < IDs.Count; i++)
            {
                var id = IDs[i];
                var fd = new FileDependency
                {
                    ExportObjects = Conns[i].ExportObjects,
                    MostlyOne = Conns[i].MostlyOne,
                    Dependencies = Conns[i].Deps,
                    ShaderMapHashes = Conns[i].Hashs
                };
                d.ChunkIDToDependencies[id] = fd;
            }
            return d;
        }
    }

    public class ManifestFile
    {
        [JsonProperty("Path")]
        public string Filepath { get; set; }

        [JsonProperty("ChunkId")]
        public string ChunkID { get; set; }
    }


    public class Manifest
    {
        [JsonProperty("Files")]
        public List<ManifestFile> Files { get; set; }

        [JsonProperty("Dependencies")]
        public Dependencies Deps { get; set; }
    }
    public class UcasPackages
    {
        [JsonProperty("Name")]
        public string PathName { get; set; }

        [JsonProperty("ExportBundleChunkIds")]
        public List<string> ExportBundleChunkIds { get; set; }

        [JsonProperty("BulkDataChunkIds")]
        public List<string> BulkDataChunkIds { get; set; }
    }


}
