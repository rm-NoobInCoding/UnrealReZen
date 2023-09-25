using Newtonsoft.Json;
using System.Collections.Generic;

namespace UEcastocLib
{

    public class FileDependency
    {
        [JsonProperty("uncompressedSize")]
        public ulong FileSize { get; set; }

        [JsonProperty("exportObjects")]
        public uint ExportObjects { get; set; }

        [JsonProperty("requiredValueSomehow")]
        public uint MostlyOne { get; set; }

        [JsonProperty("uniqueIndex")]
        public ulong SomeIndex { get; set; }

        [JsonProperty("dependencies")]
        public List<ulong> Dependencies { get; set; }
    }

    public class Dependencies
    {
        [JsonProperty("packageID")]
        public ulong ThisPackageID { get; set; }

        [JsonProperty("dependencies")]
        public Dictionary<ulong, FileDependency> ChunkIDToDependencies { get; set; }
    }

    public class DepsHeader
    {
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
    }


    public class DepLinks
    {
        public ulong FileSize { get; set; }
        public uint ExportObjects { get; set; }
        public uint MostlyOne { get; set; }
        public ulong SomeIndex { get; set; }
        public uint DependencyPackages { get; set; }
        public uint Offset { get; set; }
        public static int SizeOf()
        {
            return sizeof(ulong) * 2 + sizeof(uint) * 4;
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
                    FileSize = Conns[i].FileSize,
                    ExportObjects = Conns[i].ExportObjects,
                    MostlyOne = Conns[i].MostlyOne,
                    SomeIndex = Conns[i].SomeIndex,
                    Dependencies = new List<ulong>()
                };
                var idx = Conns[i].Offset / 8;
                for (int j = 0; j < Conns[i].DependencyPackages; j++)
                {
                    fd.Dependencies.Add(Deps[(int)(idx + j)]);
                }
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
