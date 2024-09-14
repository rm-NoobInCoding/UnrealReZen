using Ionic.Zlib;
using K4os.Compression.LZ4.Streams;

namespace UnrealReZen.Core.Compression
{
    public class CompressionUtils
    {

        public static readonly Dictionary<string, Func<byte[], byte[]>> CompressionMethods = new Dictionary<string, Func<byte[], byte[]>>
        {
            { "None", CompressNone },
            { "Zlib", CompressZlib },
            { "Oodle", CompressOodle },
            { "Lz4", CompressLZ4 }
        };

        public static byte[]? Compress(string method, byte[] inputData)
        {
            if (CompressionMethods.TryGetValue(method.ToLower(), out var compressionFunction))
            {
                return compressionFunction(inputData);
            }
            return null;
        }

        public static Func<byte[], byte[]>? GetCompressionFunction(string method)
        {
            if (CompressionMethods.TryGetValue(method, out var compressionFunction))
            {
                return compressionFunction;
            }
            return null;
        }

        private static byte[] CompressNone(byte[] inData)
        {
            return inData;
        }

        private static byte[] CompressZlib(byte[] inData)
        {
            using (var outputStream = new MemoryStream())
            using (var compressionStream = new ZlibStream(outputStream, CompressionMode.Compress))
            {
                compressionStream.Write(inData, 0, inData.Length);
                compressionStream.Close();
                return outputStream.ToArray();
            }
        }

        private static byte[] CompressOodle(byte[] inData)
        {
            if (!IsOodleDllExist())
            {
                throw new Exception("oo2core_9_win64.dll was not found (oodle compression)");
            }
            var compressedData = Oodle.Compress(inData, inData.Length, Oodle.OodleLZ_OodleFormat.Kraken, Oodle.OodleLZ_OodleCompressionLevel.Optimal3);
            return compressedData;
        }

        private static byte[] CompressLZ4(byte[] inData)
        {
            using (var inputStream = new MemoryStream(inData))
            using (var outputStream = new MemoryStream())
            using (var lz4Stream = LZ4Stream.Encode(outputStream))
            {
                inputStream.CopyTo(lz4Stream);
                lz4Stream.Flush();
                return outputStream.ToArray();
            }

        }

        private static bool IsOodleDllExist()
        {
            var dllPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "oo2core_9_win64.dll");
            return File.Exists(dllPath);
        }
    }
}