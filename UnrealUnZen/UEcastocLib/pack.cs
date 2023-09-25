using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace UEcastocLib
{
    //public class Pack
    //{
    //    const int CompSize = 0x10000;
    //    const int PackUtocVersion = 3;
    //    const int CompressionNameLength = 32;

    //    public static List<GameFileMetaData> ListFilesInDir(string dir, Dictionary<string, FIoChunkID> pathToChunkID)
    //    {
    //        var files = new List<GameFileMetaData>();
    //        Directory.GetFiles(dir, "*", SearchOption.AllDirectories).ToList().ForEach(path =>
    //        {
    //            var info = new FileInfo(path);
    //            var mountedPath = Path.GetFullPath(path).Replace("\\", "/").Substring(dir.Length).TrimStart('/');
    //            var offlen = new FIoOffsetAndLength();
    //            offlen.SetLength((ulong)info.Length);

    //            if (!pathToChunkID.TryGetValue(mountedPath, out var chidData))
    //            {
    //                throw new Exception("A problem occurred while constructing the file. Did you use the correct manifest file?");
    //            }

    //            var newEntry = new GameFileMetaData
    //            {
    //                FilePath = mountedPath,
    //                ChunkID = chidData,
    //                OffLen = offlen
    //            };

    //            files.Add(newEntry);
    //        });

    //        return files;
    //    }

    //    public static void PackFilesToUcas(List<GameFileMetaData> files, Manifest m, string dir, string outFilename, string compression)
    //    {
    //        /* manually add the "dependencies" section here */
    //        // only include the dependencies that are present
    //        var subsetDependencies = files.Select(v => m.Deps.ChunkIDToDependencies[v.ChunkID.ID]).ToDictionary(k => k.ID, v => v);
    //        m.Deps.ChunkIDToDependencies = subsetDependencies;

    //        var depHexString = files.FirstOrDefault(v => v.FilePath == Constants.DepFileName)?.ChunkID;
    //        var compMethodNumber = compression.ToLower() != "none" ? (byte)1 : (byte)0;
    //        var compFun = CompressionUtils.GetCompressionFunction(compression);

    //        if (compFun == null)
    //        {
    //            throw new Exception("Could not find compression method. Please use none, oodle or zlib");
    //        }

    //        Directory.CreateDirectory(Path.GetDirectoryName(outFilename));
    //        using (var f = File.Create(outFilename + ".ucas"))
    //        {
    //            foreach (var file in files)
    //            {
    //                var b = File.ReadAllBytes(Path.Combine(dir, file.FilePath));

    //                if (!File.Exists(file.FilePath))
    //                {
    //                    if(file.FilePath != Constants.DepFileName) throw new Exception("File doesn't exist, but the filepath indicates it's the dependency file.");
    //                    b = m.Deps.DeparseDependencies();
    //                    file.FilePath = "";
    //                    file.ChunkID = FIoChunkID.FromHexString(depHexString);
    //                }

                    

    //                file.offlen.Length = (ulong)b.Length;
    //                file.offlen.Offset = files.IndexOf(file) == 0
    //                    ? 0
    //                    : (((long)files[files.IndexOf(file) - 1].offlen.Offset + (long)files[files.IndexOf(file) - 1].offlen.Length + CompSize - 1) / CompSize) * CompSize;

    //                file.metadata.ChunkHash = Sha1Hash(b);
    //                file.metadata.Flags = 1;

    //                while (b.Length != 0)
    //                {
    //                    var chunkLen = b.Length > CompSize ? CompSize : b.Length;
    //                    var chunk = new byte[chunkLen];
    //                    Array.Copy(b, chunk, chunkLen);

    //                    var cChunkPtr = compFun(chunk);
    //                    var compressedChunk = cChunkPtr.ToArray();

    //                    var block = new FIoStoreTocCompressedBlockEntry
    //                    {
    //                        CompressionMethod = compMethodNumber,
    //                        Offset = (ulong)f.Position,
    //                        UncompressedSize = (uint)chunkLen,
    //                        CompressedSize = (uint)compressedChunk.Length
    //                    };

    //                    compressedChunk = compressedChunk.Concat(GetRandomBytes((0x10 - (compressedChunk.Length % 0x10)) & (0x10 - 1))).ToArray();
    //                    b = b.Skip(chunkLen).ToArray();
    //                    file.compressionBlocks.Add(block);

    //                    f.Write(compressedChunk, 0, compressedChunk.Length);
    //                }
    //            }
    //        }
    //    }

    //    public static byte[] ToBytes(DirIndexWrapper w)
    //    {
    //        using (var buf = new MemoryStream())
    //        {
    //            var dirCount = (uint)w.dirs.Count;
    //            var fileCount = (uint)w.files.Count;
    //            var strCount = (uint)w.strSlice.Count;

    //            var mountPointStr = StringToFString(MountPoint);
    //            buf.Write(mountPointStr, 0, mountPointStr.Length);

    //            buf.Write(BitConverter.GetBytes(dirCount), 0, 4);
    //            foreach (var directoryEntry in w.dirs)
    //            {
    //                var buffer = new byte[Marshal.SizeOf(directoryEntry)];
    //                var handle = GCHandle.Alloc(buffer, GCHandleType.Pinned);
    //                Marshal.StructureToPtr(directoryEntry, handle.AddrOfPinnedObject(), false);
    //                handle.Free();
    //                buf.Write(buffer, 0, buffer.Length);
    //            }

    //            buf.Write(BitConverter.GetBytes(fileCount), 0, 4);
    //            foreach (var fileEntry in w.files)
    //            {
    //                var buffer = new byte[Marshal.SizeOf(fileEntry)];
    //                var handle = GCHandle.Alloc(buffer, GCHandleType.Pinned);
    //                Marshal.StructureToPtr(fileEntry, handle.AddrOfPinnedObject(), false);
    //                handle.Free();
    //                buf.Write(buffer, 0, buffer.Length);
    //            }

    //            buf.Write(BitConverter.GetBytes(strCount), 0, 4);
    //            foreach (var str in w.strSlice)
    //            {
    //                var strBytes = StringToFString(str);
    //                buf.Write(strBytes, 0, strBytes.Length);
    //            }

    //            return buf.ToArray();
    //        }
    //    }

    //    public static byte[] DeparseDirectoryIndex(List<GameFileMetaData> files)
    //    {
    //        var wrapper = new DirIndexWrapper();
    //        var dirIndexEntries = new List<FIoDirectoryIndexEntry>();
    //        var fileIndexEntries = new List<FIoFileIndexEntry>();

    //        var strmap = new Dictionary<string, bool>();
    //        foreach (var v in files)
    //        {
    //            var dirfiles = v.filepath.Split('/');
    //            if (dirfiles[0] == "")
    //            {
    //                dirfiles = dirfiles.Skip(1).ToArray();
    //            }

    //            foreach (var str in dirfiles)
    //            {
    //                strmap[str] = true;
    //            }
    //        }

    //        var strSlice = strmap.Keys.ToList();
    //        var strIdx = strSlice.Select((str, i) => new { str, i }).ToDictionary(x => x.str, x => x.i);
    //        var root = new FIoDirectoryIndexEntry
    //        {
    //            Name = NoneEntry,
    //            FirstChildEntry = NoneEntry,
    //            NextSiblingEntry = NoneEntry,
    //            FirstFileEntry = NoneEntry,
    //        };

    //        dirIndexEntries.Add(root);
    //        wrapper.dirs = dirIndexEntries;
    //        wrapper.files = fileIndexEntries;
    //        wrapper.strTable = strIdx;
    //        wrapper.strSlice = strSlice;

    //        for (var i = 0; i < files.Count; i++)
    //        {
    //            var fpathSections = files[i].filepath.Split('/');
    //            if (fpathSections[0] == "")
    //            {
    //                fpathSections = fpathSections.Skip(1).ToArray();
    //            }

    //            root.AddFile(fpathSections, (uint)i, wrapper);
    //        }

    //        using (var buf = new MemoryStream())
    //        {
    //            foreach (var directoryEntry in wrapper.dirs)
    //            {
    //                var buffer = new byte[Marshal.SizeOf(directoryEntry)];
    //                var handle = GCHandle.Alloc(buffer, GCHandleType.Pinned);
    //                Marshal.StructureToPtr(directoryEntry, handle.AddrOfPinnedObject(), false);
    //                handle.Free();
    //                buf.Write(buffer, 0, buffer.Length);
    //            }

    //            return buf.ToArray();
    //        }
    //    }

    //    public static byte[] ConstructUtocFile(List<GameFileMetaData> files, string compression, byte[] AESKey)
    //    {
    //        var udata = new UTocData();
    //        var newContainerFlags = (byte)IndexedContainerFlag;
    //        var compressionMethods = new List<string> { "None" };

    //        if (compression.ToLower() != "none")
    //        {
    //            compressionMethods.Add(compression);
    //            newContainerFlags |= (byte)CompressedContainerFlag;
    //        }

    //        if (AESKey.Length != 0)
    //        {
    //            newContainerFlags |= (byte)EncryptedContainerFlag;
    //        }

    //        var compressedBlocksCount = files.Select(file => file.compressionBlocks.Count).Sum();
    //        var containerIndex = 0;

    //        for (var i = 0; i < files.Count; i++)
    //        {
    //            compressedBlocksCount += files[i].compressionBlocks.Count;
    //            if (files[i].chunkID.Type == 10)
    //            {
    //                containerIndex = i;
    //            }
    //        }

    //        var dirIndexBytes = DeparseDirectoryIndex(files);
    //        var magic = new byte[16];
    //        for (var i = 0; i < MagicUtoc.Length; i++)
    //        {
    //            magic[i] = MagicUtoc[i];
    //        }

    //        udata.hdr = new UTocHeader
    //        {
    //            Magic = magic,
    //            Version = 3,
    //            HeaderSize = (uint)Marshal.SizeOf(udata.hdr),
    //            EntryCount = (uint)files.Count,
    //            CompressedBlockEntryCount = compressedBlocksCount,
    //            CompressedBlockEntrySize = 12,
    //            CompressionMethodNameCount = (uint)(compressionMethods.Count - 1),
    //            CompressionMethodNameLength = CompressionNameLength,
    //            CompressionBlockSize = CompSize,
    //            DirectoryIndexSize = (uint)dirIndexBytes.Length,
    //            ContainerID = (FIoContainerID)files[containerIndex].chunkID.ID,
    //            ContainerFlags = (EIoContainerFlags)newContainerFlags,
    //            PartitionSize = ulong.MaxValue,
    //            PartitionCount = 1
    //        };

    //        using (var buf = new MemoryStream())
    //        {
    //            var headerBytes = new byte[Marshal.SizeOf(udata.hdr)];
    //            var headerHandle = GCHandle.Alloc(headerBytes, GCHandleType.Pinned);
    //            Marshal.StructureToPtr(udata.hdr, headerHandle.AddrOfPinnedObject(), false);
    //            headerHandle.Free();
    //            buf.Write(headerBytes, 0, headerBytes.Length);

    //            foreach (var file in files)
    //            {
    //                var chunkIDBytes = BitConverter.GetBytes(file.chunkID.ID);
    //                buf.Write(chunkIDBytes, 0, chunkIDBytes.Length);
    //            }

    //            foreach (var file in files)
    //            {
    //                var offlenBytes = new byte[Marshal.SizeOf(file.offlen)];
    //                var offlenHandle = GCHandle.Alloc(offlenBytes, GCHandleType.Pinned);
    //                Marshal.StructureToPtr(file.offlen, offlenHandle.AddrOfPinnedObject(), false);
    //                offlenHandle.Free();
    //                buf.Write(offlenBytes, 0, offlenBytes.Length);
    //            }

    //            foreach (var file in files)
    //            {
    //                foreach (var block in file.compressionBlocks)
    //                {
    //                    var blockBytes = new byte[Marshal.SizeOf(block)];
    //                    var blockHandle = GCHandle.Alloc(blockBytes, GCHandleType.Pinned);
    //                    Marshal.StructureToPtr(block, blockHandle.AddrOfPinnedObject(), false);
    //                    blockHandle.Free();
    //                    buf.Write(blockBytes, 0, blockBytes.Length);
    //                }
    //            }

    //            foreach (var compMethod in compressionMethods)
    //            {
    //                if (compMethod.ToLower() == "none")
    //                {
    //                    continue;
    //                }

    //                var capitalized = CultureInfo.CurrentCulture.TextInfo.ToTitleCase(compMethod);
    //                var bname = Encoding.ASCII.GetBytes(capitalized);
    //                var paddedName = new byte[CompressionNameLength];
    //                Array.Copy(bname, paddedName, bname.Length);
    //                buf.Write(paddedName, 0, paddedName.Length);
    //            }

    //            buf.Write(dirIndexBytes, 0, dirIndexBytes.Length);

    //            foreach (var file in files)
    //            {
    //                var metadataBytes = new byte[Marshal.SizeOf(file.metadata)];
    //                var metadataHandle = GCHandle.Alloc(metadataBytes, GCHandleType.Pinned);
    //                Marshal.StructureToPtr(file.metadata, metadataHandle.AddrOfPinnedObject(), false);
    //                metadataHandle.Free();
    //                buf.Write(metadataBytes, 0, metadataBytes.Length);
    //            }

    //            return buf.ToArray();
    //        }
    //    }

    //    public static int PackToCasToc(string dir, Manifest m, string outFilename, string compression, byte[] aes)
    //    {
    //        var offlen = new FIoOffsetAndLength();
    //        var fdata = new List<GameFileMetaData>();
    //        GameFileMetaData newEntry;

    //        foreach (var v in m.Files)
    //        {
    //            var p = Path.Combine(dir, v.Filepath);
    //            if (File.Exists(p))
    //            {
    //                offlen.Length = (ulong)new FileInfo(p).Length;
    //            }
    //            else if (v.Filepath == DepFileName)
    //            {
    //                offlen.Length = 0; // Will be fixed in a later function
    //            }

    //            newEntry = new GameFileMetaData
    //            {
    //                filepath = v.Filepath,
    //                chunkID = FromHexString(v.ChunkID),
    //                offlen = offlen
    //            };

    //            fdata.Add(newEntry);
    //        }

    //        var files = ListFilesInDir(dir, m.Files.ToDictionary(k => k.Filepath, v => FromHexString(v.ChunkID)));

    //        PackFilesToUcas(fdata, m, dir, outFilename, compression);

    //        if (aes.Length != 0)
    //        {
    //            var b = File.ReadAllBytes(outFilename + ".ucas");
    //            var encrypted = EncryptAES(b, aes);
    //            File.WriteAllBytes(outFilename + ".ucas", encrypted);
    //        }

    //        var utocBytes = ConstructUtocFile(fdata, compression, aes);
    //        File.WriteAllBytes(outFilename + ".utoc", utocBytes);
    //        return fdata.Count;
    //    }
}
