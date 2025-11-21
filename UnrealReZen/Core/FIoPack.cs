using CUE4Parse.Encryption.Aes;
using CUE4Parse.UE4.IO.Objects;
using CUE4Parse.UE4.Objects.Core.Misc;
using CUE4Parse.UE4.Versions;
using System.Globalization;
using System.IO.MemoryMappedFiles;
using System.Text;
using UnrealReZen.Core.Compression;
using UnrealReZen.Core.Helpers;

namespace UnrealReZen.Core
{
    public static class Packer
    {
        public static int PackToCasToc(string dir, Dependency m, string outFilename, string compression, FAesKey aes, string mountPoint, EGame gameVer)
        {
            FIoDependencyFormat depver = gameVer >= EGame.GAME_UE5_0 ? FIoDependencyFormat.UE5 : FIoDependencyFormat.UE4;

            var fdata = new List<AssetMetadata>();
            AssetMetadata newEntry;
            Constants.MountPoint = mountPoint;

            foreach (var v in m.Files)
            {
                var offlen = new FIoOffsetAndLength();
                var p = Path.Combine(dir, v.Filepath);
                if (File.Exists(p))
                {
                    offlen.SetLength((ulong)new FileInfo(p).Length);
                }
                else if (v.Filepath == Constants.DepFileName)
                {
                    offlen.SetLength(0);
                }

                newEntry = new AssetMetadata
                {
                    FilePath = v.Filepath,
                    ChunkID = v.ChunkID,
                    OffLen = offlen,
                    Metadata = new FIoStoreTocEntryMeta(),
                    CompressionBlocks = [],
                };

                fdata.Add(newEntry);
            }

            fdata.PackFilesToUcas(m, dir, outFilename, compression, depver);

            if (aes.KeyString != Constants.DefaultAES)
            {
                var b = File.ReadAllBytes(Path.ChangeExtension(outFilename, ".ucas"));
                var encrypted = CryptographyHelpers.EncryptAES(b, aes.Key);
                File.WriteAllBytes(Path.ChangeExtension(outFilename, ".ucas"), encrypted);
            }

            var utocBytes = fdata.ConstructUtocFile(compression, aes, gameVer);
            File.WriteAllBytes(outFilename, utocBytes);
            File.WriteAllBytes(Path.ChangeExtension(outFilename, ".pak"), PakHolder.Packed_P);
            return fdata.Count;
        }

        public static void PackFilesToUcas(this List<AssetMetadata> files, Dependency m, string dir, string outFilename, string compression, FIoDependencyFormat depver)
        {

            var subsetDependencies = new Dictionary<ulong, FFilePackageStoreEntry>();
            if (m.Deps.ChunkIDToDependencies.Count > 0)
            {
                foreach (var file in files)
                {
                    if (m.Deps.ChunkIDToDependencies.TryGetValue(file.ChunkID.ID, out FFilePackageStoreEntry? value) && !subsetDependencies.ContainsKey(file.ChunkID.ID))
                    {
                        subsetDependencies.Add(file.ChunkID.ID, value);

                    }

                }
            }

            m.Deps.ChunkIDToDependencies = subsetDependencies;

            var compMethodNumber = !compression.Equals("none", StringComparison.CurrentCultureIgnoreCase) ? (byte)1 : (byte)0;
            var compFun = CompressionUtils.GetCompressionFunction(compression) ?? throw new Exception("Could not find " + compression + " method. Please use None, Oodle or Zlib");
            using var f = File.Open(Path.ChangeExtension(outFilename, ".ucas"), FileMode.Create);
            for (int i = 0; i < files.Count; i++)
            {
                WriteProgressBar(i, files.Count - 1);

                MemoryMappedFile mmf;
                long SizeOfmmf;
                string pathToread = Path.Combine(dir.Replace("/", "\\"), files[i].FilePath.Replace("/", "\\"));
                if (!File.Exists(pathToread))
                {
                    if (files[i].FilePath != Constants.DepFileName) throw new Exception("File doesn't exist, and also its not the dependency file.");
                    byte[] ManifestCreatedFile = depver == FIoDependencyFormat.UE4 ? m.WriteDependenciesAsUE4() : m.WriteDependenciesAsUE5();
                    mmf = MemoryMappedHelpers.CreateMemoryMappedFileFromByteArray(ManifestCreatedFile, files[i].FilePath);
                    SizeOfmmf = ManifestCreatedFile.LongLength;
                    files[i].FilePath = "";
                }
                else
                {
                    mmf = MemoryMappedFile.CreateFromFile(pathToread, FileMode.Open, Path.GetFileNameWithoutExtension(pathToread));
                    SizeOfmmf = new FileInfo(pathToread).Length;
                }


                files[i].OffLen.SetLength((ulong)SizeOfmmf);

                if (i == 0)
                {
                    files[i].OffLen.SetOffset(0);
                }
                else
                {
                    var off = files[i - 1].OffLen.GetOffset() + files[i - 1].OffLen.GetLength();
                    off = (off + Constants.CompSize - 1) / Constants.CompSize * Constants.CompSize;
                    files[i].OffLen.SetOffset(off);
                }

                files[i].Metadata.ChunkHash = new FIoChunkHash(mmf.SHA1Hash());
                files[i].Metadata.Flags = FIoStoreTocEntryMetaFlags.CompressedMetaFlag;

                long PosOfReaded = 0;
                long RemainSize = SizeOfmmf;
                while (PosOfReaded != SizeOfmmf)
                {
                    var block = new FIoStoreTocCompressedBlockEntry();
                    var chunkLen = RemainSize;
                    if (chunkLen > Constants.CompSize)
                    {
                        chunkLen = Constants.CompSize;
                    }
                    RemainSize -= chunkLen;
                    var chunk = mmf.ReadBytesOfFile(PosOfReaded, chunkLen);
                    PosOfReaded += chunkLen;
                    var cChunkPtr = compFun(chunk);
                    var compressedChunk = cChunkPtr.ToArray();

                    block.CompressionMethod = compMethodNumber;
                    block.SetOffset((ulong)f.Position);
                    block.SetUncompressedSize((uint)chunkLen);
                    block.SetCompressedSize((uint)compressedChunk.Length);

                    compressedChunk = [.. compressedChunk, .. CryptographyHelpers.GetRandomBytes(0x10 - compressedChunk.Length % 0x10 & 0x10 - 1)];
                    files[i].CompressionBlocks.Add(block);

                    f.Write(compressedChunk, 0, compressedChunk.Length);
                }
                mmf.Dispose();
            }

            // Add a line feed for the progress bar
            Console.WriteLine("");
        }

        public static void WriteProgressBar(int count, int maxCount)
        {
            // Display a progress bar on the console.
            // e.g. WriteProgressBar(54, 100)
            //      [##########..........] 54/100
            const int MaxProgress = 20;
            var progress = count * MaxProgress / maxCount;
            string str = new string('#', progress) + new string('.', MaxProgress - progress);
            Console.Write($"\r[{str}] {count}/{maxCount}");
        }

        public static byte[] DeparseDirectoryIndex(List<AssetMetadata> files)
        {
            var wrapper = new DirIndexWrapper();
            var dirIndexEntries = new List<FIoDirectoryIndexEntry>();
            var fileIndexEntries = new List<FIoFileIndexEntry>();

            var strmap = new Dictionary<string, bool>();
            foreach (var v in files)
            {
                var dirfiles = v.FilePath.Split('/');
                if (dirfiles[0] == "")
                {
                    dirfiles = dirfiles.Skip(1).ToArray();
                }

                foreach (var str in dirfiles)
                {
                    strmap[str] = true;
                }
            }

            var strSlice = strmap.Keys.ToList();
            var strIdx = new Dictionary<string, int>();
            for (int iv = 0; iv < strSlice.Count; iv++)
            {
                strIdx.Add(strSlice[iv], iv);
            }


            var root = new FIoDirectoryIndexEntry
            {
                Name = Constants.NoneEntry,
                FirstChildEntry = Constants.NoneEntry,
                NextSiblingEntry = Constants.NoneEntry,
                FirstFileEntry = Constants.NoneEntry,
            };

            dirIndexEntries.Add(root);
            wrapper.Dirs = dirIndexEntries;
            wrapper.Files = fileIndexEntries;
            wrapper.StrTable = strIdx;
            wrapper.StrSlice = [.. strSlice];

            for (var i = 0; i < files.Count; i++)
            {
                var fpathSections = files[i].FilePath.Split('/');
                if (fpathSections[0] == "")
                {
                    fpathSections = fpathSections.Skip(1).ToArray();
                }

                root.AddFile(fpathSections, (uint)i, wrapper);
            }

            return wrapper.ToBytes();
        }

        public static byte[] ConstructUtocFile(this List<AssetMetadata> files, string compression, FAesKey AESKey, EGame gameVer)
        {
            var udata = new UToc(new UTocHeader(), [], "", []);

            var newContainerFlags = (byte)EIoContainerFlags.IndexedContainerFlag;
            var compressionMethods = new List<string> { "None" };

            if (!compression.Equals("none", StringComparison.CurrentCultureIgnoreCase))
            {
                compressionMethods.Add(compression);
                newContainerFlags |= (byte)EIoContainerFlags.CompressedContainerFlag;
            }

            if (AESKey.KeyString != Constants.DefaultAES)
            {
                newContainerFlags |= (byte)EIoContainerFlags.EncryptedContainerFlag;
            }

            var compressedBlocksCount = 0;
            var containerIndex = 0;

            for (var i = 0; i < files.Count; i++)
            {
                compressedBlocksCount += files[i].CompressionBlocks.Count;
                if ((gameVer < EGame.GAME_UE5_0 && files[i].ChunkID.Type == (byte)EIoChunkType.ContainerHeader) ||
                    gameVer >= EGame.GAME_UE5_0 && files[i].ChunkID.Type == (byte)EIoChunkType5.ContainerHeader)
                {
                    containerIndex = i;
                }
            }

            var dirIndexBytes = DeparseDirectoryIndex(files);

            udata.HeaderTable = new UTocHeader
            {
                Magic = Constants.MagicUtoc,
                Version = (byte)Constants.PackUtocVersion,
                HeaderSize = (uint)UTocHeader.SizeOf,
                EntryCount = (uint)files.Count,
                CompressedBlockEntryCount = (uint)compressedBlocksCount,
                CompressedBlockEntrySize = 12,
                CompressionMethodNameCount = (uint)(compressionMethods.Count - 1),
                CompressionMethodNameLength = Constants.CompressionNameLength,
                CompressionBlockSize = Constants.CompSize,
                DirectoryIndexSize = (uint)dirIndexBytes.Length,
                ContainerID = new FIoContainerID(files[containerIndex].ChunkID.ID),
                ContainerFlags = (EIoContainerFlags)newContainerFlags,
                PartitionSize = ulong.MaxValue,
                PartitionCount = 1
            };

            using var buf = new MemoryStream();
            udata.HeaderTable.Write(buf);
            foreach (var file in files)
            {
                file.ChunkID.Write(buf);
            }
            foreach (var file in files)
            {
                file.OffLen.Write(buf);
            }
            foreach (var file in files)
            {
                foreach (var block in file.CompressionBlocks)
                {
                    block.Write(buf);
                }
            }

            foreach (var compMethod in compressionMethods)
            {
                if (compMethod.Equals("none", StringComparison.CurrentCultureIgnoreCase))
                {
                    continue;
                }

                var capitalized = CultureInfo.CurrentCulture.TextInfo.ToTitleCase(compMethod);
                var bname = Encoding.ASCII.GetBytes(capitalized);
                var paddedName = new byte[Constants.CompressionNameLength];
                Array.Copy(bname, paddedName, bname.Length);
                buf.Write(paddedName, 0, paddedName.Length);
            }

            buf.Write(dirIndexBytes, 0, dirIndexBytes.Length);

            foreach (var file in files)
            {
                file.Metadata.Write(buf);
            }

            return buf.ToArray();
        }

    }
}
