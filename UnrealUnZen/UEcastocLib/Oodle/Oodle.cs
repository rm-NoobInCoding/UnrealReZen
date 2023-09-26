using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;

namespace OodleExtensions
{
    public static class Oodle
    {
        [DllImport("oo2core_9_win64.dll")]
        private static extern int OodleLZ_Compress(
          OodleFormat format,
          byte[] buffer,
          long bufferSize,
          byte[] outputBuffer,
          OodleCompressionLevel level,
          uint unused1,
          uint unused2,
          uint unused3);

        [DllImport("oo2core_9_win64.dll")]
        private static extern int OodleLZ_Decompress(
          byte[] buffer,
          long bufferSize,
          byte[] outputBuffer,
          long outputBufferSize,
          uint a,
          uint b,
          ulong c,
          uint d,
          uint e,
          uint f,
          uint g,
          uint h,
          uint i,
          uint threadModule);

        public static byte[] Compress(
          byte[] buffer,
          int size,
          OodleFormat format,
          OodleCompressionLevel level)
        {
            byte[] numArray = new byte[(int)Oodle.GetCompressionBound((uint)size)];
            int count = Oodle.OodleLZ_Compress(format, buffer, (long)size, numArray, level, 0U, 0U, 0U);
            byte[] dst = new byte[count];
            Buffer.BlockCopy((Array)numArray, 0, (Array)dst, 0, count);
            return dst;
        }

        public static byte[] Decompress(byte[] buffer, int uncompressedSize)
        {
            byte[] numArray = new byte[uncompressedSize];
            int count = Oodle.OodleLZ_Decompress(buffer, (long)buffer.Length, numArray, (long)uncompressedSize, 0U, 0U, 0UL, 0U, 0U, 0U, 0U, 0U, 0U, 3U);
            if (count == uncompressedSize)
                return numArray;
            return count < uncompressedSize ? ((IEnumerable<byte>)numArray).Take<byte>(count).ToArray<byte>() : throw new Exception("There was an error while decompressing");
        }

        private static uint GetCompressionBound(uint bufferSize) => bufferSize + 274U * ((bufferSize + 262143U) / 262144U);
    }
}
