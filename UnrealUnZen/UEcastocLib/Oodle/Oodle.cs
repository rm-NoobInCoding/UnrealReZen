using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;

namespace OodleExtension
{
    public static class Oodle
    {
        public enum OodleLZ_Verbosity
        {
            None,
            Max = 3
        }
        public enum OodleLZ_FuzzSafe
        {
            No,
            Yes
        }
        public enum OodleLZ_Decode_ThreadPhase
        {
            ThreadPhase1 = 0x1,
            ThreadPhase2 = 0x2,

            Unthreaded = ThreadPhase1 | ThreadPhase2
        }
        public enum OodleLZ_CheckCRC
        {
            No,
            Yes
        }
        public enum OodleLZ_OodleCompressionLevel
        {
            HyperFast4 = -4,
            HyperFast3,
            HyperFast2,
            HyperFast1,
            None,
            SuperFast,
            VeryFast,
            Fast,
            Normal,
            Optimal1,
            Optimal2,
            Optimal3,
            Optimal4,
            Optimal5,
            // TooHigh,

            Min = HyperFast4,
            Max = Optimal5
        }
        public enum OodleLZ_OodleFormat
        {
            Invalid = -1,
            LZH,
            LZHLW,
            LZNIB,
            None,
            LZB16,
            LZBLW,
            LZA,
            LZNA,
            Kraken,
            Mermaid,
            BitKnit,
            Selkie,
            Hydra,
            Leviathan
        }

        [DllImport("oo2core_9_win64.dll")]
        private static extern long OodleLZ_Compress(OodleLZ_OodleFormat format,byte[] buffer,long bufferSize,byte[] outputBuffer,OodleLZ_OodleCompressionLevel level,long opts, long context, long unused, long scratch, long scratch_size);
        [DllImport("oo2core_9_win64.dll")]
        private static extern long OodleLZ_Decompress(byte[] buffer, long bufferSize, byte[] outputBuffer, long outputBufferSize, OodleLZ_FuzzSafe fuzz, OodleLZ_CheckCRC crc, OodleLZ_Verbosity verbosity, long context, long e, long callback, long callback_ctx, long scratch, long scratch_size, OodleLZ_Decode_ThreadPhase thread_phase);
        [DllImport("oo2core_9_win64.dll")]
        private static extern long OodleLZDecoder_MemorySizeNeeded(OodleLZ_OodleFormat format, long size);

        public static byte[] Compress(byte[] buffer, long size, OodleLZ_OodleFormat format, OodleLZ_OodleCompressionLevel level)
        {
            byte[] numArray = new byte[OodleLZDecoder_MemorySizeNeeded(format, size)];
            long count = Oodle.OodleLZ_Compress(format, buffer, size, numArray, level, 0, 0, 0, 0, 0);
            byte[] dst = new byte[count];
            Buffer.BlockCopy((Array)numArray, 0, (Array)dst, 0, (int)count);
            return dst;
        }

        public static byte[] Decompress(byte[] buffer, int uncompressedSize, OodleLZ_FuzzSafe fuzz, OodleLZ_CheckCRC crc, OodleLZ_Verbosity verbos, OodleLZ_Decode_ThreadPhase threadPhase)
        {
            byte[] numArray = new byte[uncompressedSize];
            long count = Oodle.OodleLZ_Decompress(buffer, buffer.Length, numArray, (long)uncompressedSize, fuzz, crc, verbos, 0U, 0U, 0U, 0U, 0U, 0U, threadPhase);
            if (count == uncompressedSize)
                return numArray;
            return count < uncompressedSize ? numArray.Take((int)count).ToArray<byte>() : throw new Exception("There was an error while decompressing");
        }

        private static uint GetCompressionBound(uint bufferSize) => bufferSize + 274U * ((bufferSize + 262143U) / 262144U);
    }
}