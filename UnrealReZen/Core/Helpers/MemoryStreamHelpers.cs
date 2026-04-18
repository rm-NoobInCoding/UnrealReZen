using System.Text;

namespace UnrealReZen.Core.Helpers
{
    public static class MemoryStreamExtensions
    {
        public static void Write(this MemoryStream ms, byte[] input) => ms.Write(input, 0, input.Length);

        public static void Write(this MemoryStream ms, string input, Encoding encoding)
        {
            var bytes = encoding.GetBytes(input);
            ms.Write(bytes, 0, bytes.Length);
        }

        public static void Write(this MemoryStream ms, byte input) => ms.WriteByte(input);
        public static void Write(this MemoryStream ms, ushort input) => ms.Write(BitConverter.GetBytes(input), 0, 2);
        public static void Write(this MemoryStream ms, short input) => ms.Write(BitConverter.GetBytes(input), 0, 2);
        public static void Write(this MemoryStream ms, uint input) => ms.Write(BitConverter.GetBytes(input), 0, 4);
        public static void Write(this MemoryStream ms, int input) => ms.Write(BitConverter.GetBytes(input), 0, 4);
        public static void Write(this MemoryStream ms, ulong input) => ms.Write(BitConverter.GetBytes(input), 0, 8);
        public static void Write(this MemoryStream ms, long input) => ms.Write(BitConverter.GetBytes(input), 0, 8);
    }
}
