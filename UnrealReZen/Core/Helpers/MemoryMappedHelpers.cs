using System.IO.MemoryMappedFiles;
using System.Security.Cryptography;

namespace UnrealReZen.Core.Helpers
{
    public static class MemoryMappedHelpers
    {
        public static byte[] ReadBytesOfFile(this MemoryMappedFile mmf, long offset, long length)
        {
            using (MemoryMappedViewAccessor accessor = mmf.CreateViewAccessor(offset, length))
            {
                byte[] data = new byte[length];
                accessor.ReadArray(0, data, 0, data.Length);
                return data;
            }
        }
        public static MemoryMappedFile CreateMemoryMappedFileFromByteArray(byte[] data, string filePath)
        {
            using (MemoryMappedFile mmf = MemoryMappedFile.CreateNew(Path.GetFileNameWithoutExtension(filePath), data.Length))
            {
                using (MemoryMappedViewAccessor accessor = mmf.CreateViewAccessor())
                {
                    accessor.WriteArray(0, data, 0, data.Length);
                }
                return MemoryMappedFile.OpenExisting(Path.GetFileNameWithoutExtension(filePath));
            }
        }
        public static byte[] SHA1Hash(this MemoryMappedFile mmf)
        {
            using MemoryMappedViewAccessor accessor = mmf.CreateViewAccessor();
            long length = accessor.Capacity;
            int bufferSize = 4096;
            byte[] buffer = new byte[bufferSize];

            using SHA1 sha1 = SHA1.Create();
            long position = 0;
            while (position < length)
            {
                int bytesToRead = (int)Math.Min(bufferSize, length - position);

                accessor.ReadArray(position, buffer, 0, bytesToRead);
                sha1.TransformBlock(buffer, 0, bytesToRead, buffer, 0);

                position += bytesToRead;
            }

            sha1.TransformFinalBlock(buffer, 0, 0);
            return sha1.Hash;
        }
    }
}
