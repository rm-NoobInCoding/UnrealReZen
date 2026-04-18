using K4os.Compression.LZ4.Streams;
using OodleDotNet;
using ZlibngDotNet;

namespace UnrealReZen.Core.Compression
{
    public static class CompressionUtils
    {
        public static readonly Dictionary<string, Func<byte[], byte[]>> CompressionMethods = new(StringComparer.OrdinalIgnoreCase)
        {
            { "None", CompressNone },
            { "Zlib", CompressZlib },
            { "Oodle", CompressOodle },
            { "Lz4", CompressLZ4 }
        };

        public static Func<byte[], byte[]>? GetCompressionFunction(string method)
            => CompressionMethods.TryGetValue(method, out var fn) ? fn : null;

        private static readonly Lazy<Zlibng> _zlib = new(() =>
            new Zlibng(Path.Combine(Constants.ToolDirectory, CUE4Parse.Compression.ZlibHelper.DllName)),
            isThreadSafe: true);

        private static readonly Lazy<Oodle> _oodle = new(() =>
            new Oodle(Path.Combine(Constants.ToolDirectory, CUE4Parse.Compression.OodleHelper.OodleFileName)),
            isThreadSafe: true);

        private static byte[] CompressNone(byte[] inData) => inData;

        private static byte[] CompressZlib(byte[] inData)
        {
            var zlib = _zlib.Value;
            var bufferSize = (int)zlib.CompressBound(inData.Length);
            var buffer = new byte[bufferSize];
            var result = zlib.Compress(buffer, inData, out int compressedSize);
            if (result != ZlibngCompressionResult.Ok)
            {
                throw new InvalidOperationException($"Zlib compression failed with error code {result}");
            }
            Array.Resize(ref buffer, compressedSize);
            return buffer;
        }

        private static byte[] CompressOodle(byte[] inData)
        {
            const OodleCompressor compressor = OodleCompressor.Kraken;
            var oodle = _oodle.Value;
            var bufferSize = (int)oodle.GetCompressedBufferSizeNeeded(compressor, inData.Length);
            var buffer = new byte[bufferSize];
            var compressedSize = (int)oodle.Compress(compressor, OodleCompressionLevel.Max, inData, buffer);
            Array.Resize(ref buffer, compressedSize);
            return buffer;
        }

        private static byte[] CompressLZ4(byte[] inData)
        {
            using var inputStream = new MemoryStream(inData);
            using var outputStream = new MemoryStream(inData.Length);
            using (var lz4Stream = LZ4Stream.Encode(outputStream))
            {
                inputStream.CopyTo(lz4Stream);
            }
            return outputStream.ToArray();
        }
    }
}
