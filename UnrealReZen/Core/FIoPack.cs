using CUE4Parse.Encryption.Aes;
using CUE4Parse.UE4.IO.Objects;
using CUE4Parse.UE4.Objects.Core.Misc;
using CUE4Parse.UE4.Versions;
using System.Globalization;
using System.Security.Cryptography;
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

            if (!string.Equals(aes.KeyString, Constants.DefaultAES, StringComparison.OrdinalIgnoreCase))
            {
                EncryptUcasInPlace(Path.ChangeExtension(outFilename, ".ucas"), aes.Key);
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

            var compMethodNumber = !compression.Equals("none", StringComparison.OrdinalIgnoreCase) ? (byte)1 : (byte)0;
            var compFun = CompressionUtils.GetCompressionFunction(compression) ?? throw new Exception("Could not find " + compression + " method. Please use None, Oodle or Zlib");
            using var f = File.Open(Path.ChangeExtension(outFilename, ".ucas"), FileMode.Create);
            var readBuffer = new byte[Constants.CompSize];
            for (int i = 0; i < files.Count; i++)
            {
                WriteProgressBar(i + 1, files.Count);

                var pathToRead = Path.Combine(dir, files[i].FilePath);
                using ChunkSource source = File.Exists(pathToRead)
                    ? ChunkSource.FromFile(pathToRead)
                    : CreateDependencyChunkSource(files[i], m, depver);

                files[i].OffLen.SetLength((ulong)source.Length);

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

                files[i].Metadata.ChunkHash = new FIoChunkHash(source.ComputeSha1());
                files[i].Metadata.Flags = FIoStoreTocEntryMetaFlags.CompressedMetaFlag;

                long readPos = 0;
                while (readPos < source.Length)
                {
                    int chunkLen = (int)Math.Min(Constants.CompSize, source.Length - readPos);
                    source.ReadInto(readPos, readBuffer, 0, chunkLen);
                    readPos += chunkLen;

                    byte[] compressedChunk = compFun(chunkLen == readBuffer.Length ? readBuffer : readBuffer[..chunkLen]);

                    var block = new FIoStoreTocCompressedBlockEntry
                    {
                        CompressionMethod = compMethodNumber
                    };
                    block.SetOffset((ulong)f.Position);
                    block.SetUncompressedSize((uint)chunkLen);
                    block.SetCompressedSize((uint)compressedChunk.Length);
                    files[i].CompressionBlocks.Add(block);

                    f.Write(compressedChunk, 0, compressedChunk.Length);
                    int padLen = (0x10 - compressedChunk.Length % 0x10) & 0x0F;
                    if (padLen > 0)
                    {
                        Span<byte> pad = stackalloc byte[16];
                        RandomNumberGenerator.Fill(pad[..padLen]);
                        f.Write(pad[..padLen]);
                    }
                }
            }

            Console.WriteLine("");
        }

        private static ChunkSource CreateDependencyChunkSource(AssetMetadata entry, Dependency m, FIoDependencyFormat depver)
        {
            if (entry.FilePath != Constants.DepFileName)
            {
                throw new FileNotFoundException($"Content file not found: {entry.FilePath}");
            }
            byte[] depBytes = depver == FIoDependencyFormat.UE4 ? m.WriteDependenciesAsUE4() : m.WriteDependenciesAsUE5();
            entry.FilePath = "";
            return ChunkSource.FromBytes(depBytes);
        }

        private static void EncryptUcasInPlace(string path, byte[] aesKey)
        {
            string tempPath = path + ".enc.tmp";
            using (var aes = System.Security.Cryptography.Aes.Create())
            {
                aes.Key = aesKey;
                aes.Mode = CipherMode.ECB;
                aes.Padding = PaddingMode.None;
                using var input = File.Open(path, FileMode.Open, FileAccess.Read);
                using var output = File.Open(tempPath, FileMode.Create, FileAccess.Write);
                using var encryptor = aes.CreateEncryptor();
                using var crypto = new CryptoStream(output, encryptor, CryptoStreamMode.Write);
                input.CopyTo(crypto);
            }
            File.Move(tempPath, path, overwrite: true);
        }

        public static void WriteProgressBar(int count, int total)
        {
            const int MaxProgress = 20;
            int denom = Math.Max(total, 1);
            int progress = Math.Min(count * MaxProgress / denom, MaxProgress);
            string str = new string('#', progress) + new string('.', MaxProgress - progress);
            Console.Write($"\r[{str}] {count}/{total}");
        }

        public static byte[] DeparseDirectoryIndex(List<AssetMetadata> files)
        {
            var strIdx = new Dictionary<string, int>();
            var strSlice = new List<string>();
            foreach (var file in files)
            {
                foreach (var segment in SplitPath(file.FilePath))
                {
                    if (strIdx.TryAdd(segment, strSlice.Count))
                    {
                        strSlice.Add(segment);
                    }
                }
            }

            var root = new FIoDirectoryIndexEntry
            {
                Name = Constants.NoneEntry,
                FirstChildEntry = Constants.NoneEntry,
                NextSiblingEntry = Constants.NoneEntry,
                FirstFileEntry = Constants.NoneEntry,
            };

            var wrapper = new DirIndexWrapper(
                new List<FIoDirectoryIndexEntry> { root },
                new List<FIoFileIndexEntry>(files.Count),
                strIdx,
                strSlice.ToArray());

            for (var i = 0; i < files.Count; i++)
            {
                root.AddFile(SplitPath(files[i].FilePath).ToArray(), (uint)i, wrapper);
            }

            return wrapper.ToBytes();
        }

        private static IEnumerable<string> SplitPath(string path)
        {
            int start = path.StartsWith('/') ? 1 : 0;
            for (int i = start; i < path.Length; i++)
            {
                if (path[i] == '/')
                {
                    if (i > start) yield return path[start..i];
                    start = i + 1;
                }
            }
            if (start < path.Length) yield return path[start..];
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
