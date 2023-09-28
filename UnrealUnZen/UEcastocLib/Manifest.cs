using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;

namespace UEcastocLib
{
    public static class ManifestData
    {
        public const string DepFileName = "dependencies";
        public const int UE5_DepFile_Sig = 1232028526;
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
                        FileSize = 0,
                        ExportObjects = entry.ExportObjects,
                        MostlyOne = entry.MostlyOne,
                        SomeIndex = 0,
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
                ManifestFile mf = new ManifestFile { Filepath = "/" + v.FilePath.Replace("\\", "/"), ChunkID = v.ChunkID.ToHexString().ToLower() };
                if (v.FilePath == "/dependencies") v.FilePath = "dependencies";
                m.Files.Add(mf);
            }

            byte[] data = u.UnpackDependencies(ucasPath);
            File.WriteAllBytes("t.bin", data);
            m.Deps = ParseDependencies(data);

            return m;
        }

        public static void GetDep(this List<ulong> Deps, BinaryReader br)
        {
            var initialPos = br.BaseStream.Position;
            int arrayNum = br.ReadInt32();
            int offset = br.ReadInt32();
            var continuePos = br.BaseStream.Position;
            br.BaseStream.Seek(initialPos + offset, SeekOrigin.Begin);
            for (int i = 0; i < arrayNum; i++)
            {
                Deps.Add(br.ReadUInt64());
            }
            br.BaseStream.Seek(continuePos, SeekOrigin.Begin);
        }
        public static void GetHash(this List<string> Deps, BinaryReader br)
        {
            var initialPos = br.BaseStream.Position;
            int arrayNum = br.ReadInt32();
            int offset = br.ReadInt32();
            var continuePos = br.BaseStream.Position;
            br.BaseStream.Seek(initialPos + offset, SeekOrigin.Begin);
            for (int i = 0; i < arrayNum; i++)
            {
                Deps.Add(BitConverter.ToString(br.ReadBytes(20)).Replace("-", ""));
            }
            br.BaseStream.Seek(continuePos, SeekOrigin.Begin);
        }

        public static Dependencies ParseDependencies(byte[] b)
        {
            var s = new ParseDependencies();
            s.IDToConn = new Dictionary<ulong, DepLinks>();
            using (var reader = new BinaryReader(new MemoryStream(b)))
            {
                s.Hdr = new DepsHeader();
                if (reader.ReadInt32() != UE5_DepFile_Sig)
                {
                    s.Hdr.Version = EIoContainerHeaderVersion.BeforeVersionWasAdded;
                    reader.BaseStream.Seek(0, SeekOrigin.Begin); //back to start of file for ue4 dep file
                }
                else
                {
                    s.Hdr.Version = (EIoContainerHeaderVersion)reader.ReadInt32();
                }

                s.Hdr.ThisPackageID = reader.ReadUInt64();
                s.Hdr.NumberOfIDs = s.Hdr.Version <= EIoContainerHeaderVersion.OptionalSegmentPackages ? reader.ReadUInt32() : 0;
                if (s.Hdr.Version == EIoContainerHeaderVersion.BeforeVersionWasAdded)
                {
                    reader.ReadBytes(4); //number of ids is uint64 in ue4 but we skip 4 empty byte
                    s.Hdr.IDSize = reader.ReadUInt32();
                    reader.ReadBytes(8);
                    s.Hdr.NumberOfIDsAgain = reader.ReadUInt32();

                }
                s.IDs = new List<ulong>();
                for (int i = 0; i < (int)s.Hdr.NumberOfIDs; i++)
                {
                    s.IDs.Add(reader.ReadUInt64());
                }

                s.FileLength = reader.ReadUInt32();
                s.Conns = new List<DepLinks>();
                for (int i = 0; i < (int)s.Hdr.NumberOfIDs; i++)
                {
                    var conn = new DepLinks();
                    conn.Deps = new List<ulong>();
                    conn.Hashs = new List<string>();
                    if (s.Hdr.Version >= EIoContainerHeaderVersion.Initial)
                    {
                        if (s.Hdr.Version < EIoContainerHeaderVersion.NoExportInfo)
                        {
                            conn.ExportObjects = reader.ReadUInt32();
                            conn.MostlyOne = reader.ReadUInt32();
                        }
                        conn.Deps.GetDep(reader);
                        conn.Hashs.GetHash(reader);

                    }
                    else
                    {
                        reader.ReadBytes(8); //File Size
                        conn.ExportObjects = reader.ReadUInt32();
                        conn.MostlyOne = reader.ReadUInt32();
                        reader.ReadBytes(8); //Index
                        conn.Deps.GetDep(reader);
                    }
                    s.IDToConn[s.IDs[i]] = conn;
                    s.Conns.Add(conn);
                }
                //s.OffsetAfterConss = reader.BaseStream.Position;
                //var toParse = s.FileLength - (uint)s.Hdr.NumberOfIDs * (uint)DepLinks.SizeOf();
                //s.Deps = new List<ulong>();
                //for (int i = 0; i < (int)toParse; i += 8)
                //{
                //    s.Deps.Add(reader.ReadUInt64());
                //}
            }
            return s.ExtractDependencies();
        }
    }
}
