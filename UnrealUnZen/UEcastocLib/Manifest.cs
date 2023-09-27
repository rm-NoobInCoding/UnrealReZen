using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace UEcastocLib
{
    public static class ManifestData
    {
        public const string DepFileName = "dependencies";

        public static byte[] DeparseDependencies(this Dependencies d)
        {
            using (var ms = new MemoryStream())
            {

                var hdr = new DepsHeader
                {
                    ThisPackageID = d.ThisPackageID,
                    NumberOfIDs = (ulong)d.ChunkIDToDependencies.Count,
                    IDSize = 8,
                    Padding = new byte[] { 0x00, 0x00, 0x64, 0xC1 },
                    ZeroBytes = new byte[4],
                    NumberOfIDsAgain = (uint)d.ChunkIDToDependencies.Count
                };
                hdr.Write(ms);

                // write list of IDs
                var ids = d.ChunkIDToDependencies.Keys.ToList();
                var totalNumberOfDependencies = 0;
                foreach (var k in d.ChunkIDToDependencies)
                {
                    totalNumberOfDependencies += k.Value.Dependencies.Count;
                }

                // these IDs are stored in order in this file, so sort here and use this ordering
                ids.Sort();
                ids.Reverse();

                foreach (var id in ids)
                {
                    ms.Write(id);
                }

                // write file length from this point onwards
                var flength = (uint)(d.ChunkIDToDependencies.Count * DepLinks.SizeOf()) + totalNumberOfDependencies * 8;
                ms.Write((uint)flength);

                // write list of DepLinks entries
                var endOfDeps = flength - (uint)(totalNumberOfDependencies * 8);
                var depsToWrite = new List<ulong>();
                for (int i = 0; i < ids.Count; i++)
                {
                    var id = ids[i];
                    var entry = d.ChunkIDToDependencies[id];
                    var link = new DepLinks
                    {
                        FileSize = entry.FileSize,
                        ExportObjects = entry.ExportObjects,
                        MostlyOne = entry.MostlyOne,
                        SomeIndex = entry.SomeIndex,
                        DependencyPackages = (uint)entry.Dependencies.Count,
                        Offset = 0
                    };
                    if (link.DependencyPackages != 0)
                    {
                        var offsetFieldOffset = i * DepLinks.SizeOf() + 16 + 8;
                        var target = (int)endOfDeps + depsToWrite.Count * 8;
                        depsToWrite.AddRange(entry.Dependencies);
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
        }
        public static Manifest ConstructManifest(this UTocData u, string ucasPath)
        {
            Manifest m = new Manifest { Files = new List<ManifestFile>(), Deps = new Dependencies() };

            foreach (var v in u.Files)
            {
                ManifestFile mf = new ManifestFile { Filepath = v.FilePath.Replace("\\", "/"), ChunkID = v.ChunkID.ToHexString().ToLower() };
                m.Files.Add(mf);
            }

            byte[] data = u.UnpackDependencies(ucasPath);
            File.WriteAllBytes("t.bin", data);
            m.Deps = ParseDependencies(data);

            return m;
        }
        public static Dependencies ParseDependencies(byte[] b)
        {
            var s = new ParseDependencies();
            s.IDToConn = new Dictionary<ulong, DepLinks>();
            using (var reader = new BinaryReader(new MemoryStream(b)))
            {
                s.Hdr = new DepsHeader
                {
                    ThisPackageID = reader.ReadUInt64(),
                    NumberOfIDs = reader.ReadUInt64(),
                    IDSize = reader.ReadUInt32(),
                    Padding = reader.ReadBytes(4),
                    ZeroBytes = reader.ReadBytes(4),
                    NumberOfIDsAgain = reader.ReadUInt32()
                };
                s.IDs = new List<ulong>();
                for (int i = 0; i < (int)s.Hdr.NumberOfIDs; i++)
                {
                    s.IDs.Add(reader.ReadUInt64());
                }
                s.FileLength = reader.ReadUInt32();
                var curr = reader.BaseStream.Position;
                s.OffsetAfterConss = curr + (long)s.Hdr.NumberOfIDs * DepLinks.SizeOf();
                s.Conns = new List<DepLinks>();
                for (int i = 0; i < (int)s.Hdr.NumberOfIDs; i++)
                {
                    var conn = new DepLinks
                    {
                        FileSize = reader.ReadUInt64(),
                        ExportObjects = reader.ReadUInt32(),
                        MostlyOne = reader.ReadUInt32(),
                        SomeIndex = reader.ReadUInt64(),
                        DependencyPackages = reader.ReadUInt32(),
                        Offset = reader.ReadUInt32()
                    };
                    if (conn.Offset != 0)
                    {
                        curr = reader.BaseStream.Position;
                        conn.Offset = (uint)(curr + conn.Offset - s.OffsetAfterConss - 8);
                    }
                    s.IDToConn[s.IDs[i]] = conn;
                    s.Conns.Add(conn);
                }
                s.OffsetAfterConss = reader.BaseStream.Position;
                var toParse = s.FileLength - (uint)s.Hdr.NumberOfIDs * (uint)DepLinks.SizeOf();
                s.Deps = new List<ulong>();
                for (int i = 0; i < (int)toParse; i += 8)
                {
                    s.Deps.Add(reader.ReadUInt64());
                }
            }
            return s.ExtractDependencies();
        }
    }
}
