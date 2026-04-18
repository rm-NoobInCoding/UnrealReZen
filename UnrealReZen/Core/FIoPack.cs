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
                    Metadata = new FIoStoreTocEntryMeta { ChunkHash = new FIoChunkHash(new byte[20]) },
                    CompressionBlocks = [],
                };

                fdata.Add(newEntry);
            }

            PackFilesToUcas(fdata, m, dir, outFilename, compression, depver);

            if (!string.Equals(aes.KeyString, Constants.DefaultAES, StringComparison.OrdinalIgnoreCase))
            {
                EncryptUcasInPlace(Path.ChangeExtension(outFilename, ".ucas"), aes.Key);
            }

            var utocBytes = ConstructUtocFile(fdata, compression, aes, gameVer);
            File.WriteAllBytes(outFilename, utocBytes);
            File.WriteAllBytes(Path.ChangeExtension(outFilename, ".pak"), PakHolder.Packed_P);
            return fdata.Count;
        }

        public static void PackFilesToUcas(List<AssetMetadata> files, Dependency m, string dir, string outFilename, string compression, FIoDependencyFormat depver)
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
            Span<byte> padBuffer = stackalloc byte[16];
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

                files[i].Metadata.Flags = FIoStoreTocEntryMetaFlags.CompressedMetaFlag;

                using var sha1 = SHA1.Create();
                long readPos = 0;
                while (readPos < source.Length)
                {
                    int chunkLen = (int)Math.Min(Constants.CompSize, source.Length - readPos);
                    source.ReadInto(readPos, readBuffer, 0, chunkLen);
                    readPos += chunkLen;
                    sha1.TransformBlock(readBuffer, 0, chunkLen, readBuffer, 0);

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
                        RandomNumberGenerator.Fill(padBuffer[..padLen]);
                        f.Write(padBuffer[..padLen]);
                    }
                }
                sha1.TransformFinalBlock(readBuffer, 0, 0);
                files[i].Metadata.ChunkHash = new FIoChunkHash(sha1.Hash!);
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

        public static byte[] ConstructUtocFile(List<AssetMetadata> files, string compression, FAesKey aesKey, EGame gameVer)
        {
            bool isCompressed = !compression.Equals("none", StringComparison.OrdinalIgnoreCase);
            bool isEncrypted = !string.Equals(aesKey.KeyString, Constants.DefaultAES, StringComparison.OrdinalIgnoreCase);

            var containerFlags = EIoContainerFlags.IndexedContainerFlag;
            if (isCompressed) containerFlags |= EIoContainerFlags.CompressedContainerFlag;
            if (isEncrypted) containerFlags |= EIoContainerFlags.EncryptedContainerFlag;

            byte containerChunkType = gameVer >= EGame.GAME_UE5_0
                ? (byte)EIoChunkType5.ContainerHeader
                : (byte)EIoChunkType.ContainerHeader;

            int compressedBlocksCount = 0;
            int containerIndex = 0;
            for (int i = 0; i < files.Count; i++)
            {
                compressedBlocksCount += files[i].CompressionBlocks.Count;
                if (files[i].ChunkID.Type == containerChunkType) containerIndex = i;
            }

            var dirIndexBytes = DeparseDirectoryIndex(files);
            uint compressionMethodCount = isCompressed ? 1u : 0u;

            var header = new UTocHeader
            {
                Magic = Constants.MagicUtoc,
                Version = (byte)Constants.PackUtocVersion,
                HeaderSize = (uint)UTocHeader.SizeOf,
                EntryCount = (uint)files.Count,
                CompressedBlockEntryCount = (uint)compressedBlocksCount,
                CompressedBlockEntrySize = 12,
                CompressionMethodNameCount = compressionMethodCount,
                CompressionMethodNameLength = Constants.CompressionNameLength,
                CompressionBlockSize = Constants.CompSize,
                DirectoryIndexSize = (uint)dirIndexBytes.Length,
                ContainerID = new FIoContainerID(files[containerIndex].ChunkID.ID),
                ContainerFlags = containerFlags,
                PartitionSize = ulong.MaxValue,
                PartitionCount = 1
            };

            using var buf = new MemoryStream();
            header.Write(buf);
            foreach (var file in files) file.ChunkID.Write(buf);
            foreach (var file in files) file.OffLen.Write(buf);
            foreach (var file in files)
                foreach (var block in file.CompressionBlocks)
                    block.Write(buf);

            if (isCompressed)
            {
                var capitalized = CultureInfo.InvariantCulture.TextInfo.ToTitleCase(compression);
                var nameBytes = Encoding.ASCII.GetBytes(capitalized);
                var padded = new byte[Constants.CompressionNameLength];
                Array.Copy(nameBytes, padded, nameBytes.Length);
                buf.Write(padded, 0, padded.Length);
            }

            buf.Write(dirIndexBytes, 0, dirIndexBytes.Length);
            foreach (var file in files) file.Metadata.Write(buf);

            return buf.ToArray();
        }
    }
}
