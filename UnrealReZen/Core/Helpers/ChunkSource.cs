using System.IO.MemoryMappedFiles;
using System.Security.Cryptography;

namespace UnrealReZen.Core.Helpers
{
    public sealed class ChunkSource : IDisposable
    {
        private readonly MemoryMappedFile? _mmf;
        private readonly MemoryMappedViewAccessor? _accessor;
        private readonly byte[]? _bytes;

        public long Length { get; }

        private ChunkSource(MemoryMappedFile mmf, MemoryMappedViewAccessor accessor, long length)
        {
            _mmf = mmf;
            _accessor = accessor;
            Length = length;
        }

        private ChunkSource(byte[] bytes)
        {
            _bytes = bytes;
            Length = bytes.LongLength;
        }

        public static ChunkSource FromFile(string path)
        {
            var length = new FileInfo(path).Length;
            if (length == 0)
            {
                return new ChunkSource(Array.Empty<byte>());
            }
            var mmf = MemoryMappedFile.CreateFromFile(path, FileMode.Open, null, 0, MemoryMappedFileAccess.Read);
            var accessor = mmf.CreateViewAccessor(0, length, MemoryMappedFileAccess.Read);
            return new ChunkSource(mmf, accessor, length);
        }

        public static ChunkSource FromBytes(byte[] bytes) => new(bytes);

        public void ReadInto(long offset, byte[] dest, int destOffset, int count)
        {
            if (_accessor != null)
            {
                _accessor.ReadArray(offset, dest, destOffset, count);
            }
            else
            {
                Array.Copy(_bytes!, offset, dest, destOffset, count);
            }
        }

        public byte[] ComputeSha1()
        {
            using var sha1 = SHA1.Create();
            const int bufferSize = 64 * 1024;
            var buffer = new byte[bufferSize];
            long position = 0;
            while (position < Length)
            {
                int bytesToRead = (int)Math.Min(bufferSize, Length - position);
                ReadInto(position, buffer, 0, bytesToRead);
                sha1.TransformBlock(buffer, 0, bytesToRead, buffer, 0);
                position += bytesToRead;
            }
            sha1.TransformFinalBlock(buffer, 0, 0);
            return sha1.Hash!;
        }

        public void Dispose()
        {
            _accessor?.Dispose();
            _mmf?.Dispose();
        }
    }
}
