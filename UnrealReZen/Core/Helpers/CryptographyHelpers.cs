using System.Security.Cryptography;

namespace UnrealReZen.Core.Helpers
{
    internal class CryptographyHelpers
    {
        public static byte[] EncryptAES(byte[] plaintext, byte[] AES)
        {
            using (Aes aes = Aes.Create())
            {
                aes.Key = AES;
                aes.Mode = CipherMode.ECB;
                aes.Padding = PaddingMode.None;

                using (ICryptoTransform encryptor = aes.CreateEncryptor(aes.Key, aes.IV))
                {
                    return encryptor.TransformFinalBlock(plaintext, 0, plaintext.Length);
                }
            }
        }
        public static byte[] SHA1Hash(byte[] data)
        {
            return SHA1.HashData(data);
        }
        public static ulong RandomUlong()
        {
            Random rnd = new();
            int rawsize = System.Runtime.InteropServices.Marshal.SizeOf(ulong.MaxValue);
            var buffer = new byte[rawsize];
            rnd.NextBytes(buffer);
            return BitConverter.ToUInt64(buffer, 0);
        }
        public static byte[] GetRandomBytes(int n)
        {
            byte[] ret = new byte[n];
            using (RandomNumberGenerator rng = RandomNumberGenerator.Create())
            {
                rng.GetBytes(ret);
            }
            return ret;
        }
        public static long Align(uint ptr, int alignment)
        {
            return ptr + alignment - 1 & ~(alignment - 1);
        }
    }
}
