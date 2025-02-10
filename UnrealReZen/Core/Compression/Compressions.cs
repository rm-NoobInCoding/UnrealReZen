using K4os.Compression.LZ4.Streams;
using OodleDotNet;
using ZlibngDotNet;

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
            using var _zlib = new Zlibng(Path.Combine(Constants.ToolDirectory, CUE4Parse.Compression.ZlibHelper.DLL_NAME));
            var compressedBufferSize = (int)_zlib.CompressBound(inData.Length);
            var compressedBuffer = new byte[compressedBufferSize];
            var compressionResult = _zlib.Compress(compressedBuffer, inData, out int compressedSize);
            if (compressionResult.CompareTo(ZlibngCompressionResult.Ok) != 0)
            {
                throw new Exception($"Zlib compression failed with error code {compressionResult}");
            }
            return compressedBuffer;

        }

        private static byte[] CompressOodle(byte[] inData)
        {
            const OodleCompressor compressor = OodleCompressor.Kraken;
            using var _oodle = new Oodle(Path.Combine(Constants.ToolDirectory, CUE4Parse.Compression.OodleHelper.OODLE_DLL_NAME));
            var compressedBufferSize = (int)_oodle.GetCompressedBufferSizeNeeded(compressor, inData.Length);
            var compressedBuffer = new byte[compressedBufferSize];
            var compressedSize = (int)_oodle.Compress(compressor, OodleCompressionLevel.Max, inData, compressedBuffer);
            return compressedBuffer;
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
    }
}