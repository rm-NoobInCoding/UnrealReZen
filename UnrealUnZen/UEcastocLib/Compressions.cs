using Ionic.Zlib;
using LZ4;
using OodleExtension;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace UEcastocLib
{
    public class CompressionUtils
    {
        public static readonly Dictionary<string, Func<byte[], uint, byte[]>> DecompressionMethods = new Dictionary<string, Func<byte[], uint, byte[]>>
    {
        { "None", DecompressNone },
        { "Zlib", DecompressZlib },
        { "Oodle", DecompressOodle },
        { "Lz4", DecompressLZ4 }
    };

        public static readonly Dictionary<string, Func<byte[], byte[]>> CompressionMethods = new Dictionary<string, Func<byte[], byte[]>>
    {
        { "None", CompressNone },
        { "Zlib", CompressZlib },
        { "Oodle", CompressOodle },
        { "Lz4", CompressLZ4 }
    };

        public static byte[] Decompress(string method, byte[] inputData, uint expectedOutputSize)
        {
            if (DecompressionMethods.TryGetValue(method.ToLower(), out var decompressionFunction))
            {
                return decompressionFunction(inputData, expectedOutputSize);
            }
            return null;
        }

        public static byte[] Compress(string method, byte[] inputData)
        {
            if (CompressionMethods.TryGetValue(method.ToLower(), out var compressionFunction))
            {
                return compressionFunction(inputData);
            }
            return null;
        }

        public static Func<byte[], byte[]> GetCompressionFunction(string method)
        {
            if (CompressionMethods.TryGetValue(method, out var compressionFunction))
            {
                return compressionFunction;
            }
            return null;
        }

        private static byte[] DecompressNone(byte[] inData, uint expectedOutputSize)
        {
            return inData;
        }

        private static byte[] DecompressZlib(byte[] inData, uint expectedOutputSize)
        {
            using (var inputStream = new MemoryStream(inData.ToArray()))
            using (var outputStream = new MemoryStream())
            using (var decompressionStream = new ZlibStream(inputStream, Ionic.Zlib.CompressionMode.Decompress))
            {
                decompressionStream.CopyTo(outputStream);
                var uncompressed = outputStream.ToArray();
                if (uncompressed.Length != expectedOutputSize)
                {
                    throw new Exception("Zlib did not decompress correctly");
                }
                return uncompressed;
            }
        }

        private static byte[] DecompressOodle(byte[] inData, uint expectedOutputSize)
        {
            if (!IsOodleDllExist())
            {
                throw new Exception("oo2core_9_win64.dll was not found (oodle decompression)");
            }

            var output = Oodle.Decompress(inData, (int)expectedOutputSize, Oodle.OodleLZ_FuzzSafe.No, Oodle.OodleLZ_CheckCRC.No, Oodle.OodleLZ_Verbosity.Max, Oodle.OodleLZ_Decode_ThreadPhase.Unthreaded);
            return output;
        }

        private static byte[] DecompressLZ4(byte[] inData, uint expectedOutputSize)
        {
            using (var inputStream = new MemoryStream(inData))
            using (var outputStream = new MemoryStream())
            using (var lz4Stream = new LZ4Stream(inputStream, LZ4StreamMode.Decompress))
            {
                lz4Stream.CopyTo(outputStream);
                var decompressed = outputStream.ToArray();
                return decompressed;
            }
        }

        private static byte[] CompressNone(byte[] inData)
        {
            return inData;
        }

        private static byte[] CompressZlib(byte[] inData)
        {
            using (var outputStream = new MemoryStream())
            using (var compressionStream = new ZlibStream(outputStream, Ionic.Zlib.CompressionMode.Compress))
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
            using (var lz4Stream = new LZ4Stream(outputStream, LZ4StreamMode.Compress))
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