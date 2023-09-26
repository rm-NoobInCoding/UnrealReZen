using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace UEcastocLib
{
    internal class Helpers
    {

        public static byte[] DecryptAES(byte[] ciphertext, byte[] AESKey)
        {
            using (Aes aesAlg = Aes.Create())
            {
                aesAlg.Key = AESKey;
                aesAlg.Mode = CipherMode.ECB;
                aesAlg.Padding = PaddingMode.Zeros;
                //aesAlg.IV = new byte[16];

                ICryptoTransform decryptor = aesAlg.CreateDecryptor(aesAlg.Key, aesAlg.IV);

                byte[] decryptedBytes = new byte[ciphertext.Length];

                using (MemoryStream msDecrypt = new MemoryStream(ciphertext))
                {
                    using (CryptoStream csDecrypt = new CryptoStream(msDecrypt, decryptor, CryptoStreamMode.Read))
                    {
                        csDecrypt.Read(decryptedBytes, 0, decryptedBytes.Length);
                    }
                }

                return decryptedBytes;
            }
        }

        public static byte[] EncryptAES(byte[] plaintext, byte[] AES)
        {
            using (AesManaged aes = new AesManaged())
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

        public static byte[] HexStringToByteArray(string hexString)
        {
            // Remove the "0x" prefix if present
            if (hexString.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
            {
                hexString = hexString.Substring(2);
            }

            // Check if the input string has an even number of characters
            if (hexString.Length % 2 != 0)
            {
                throw new ArgumentException("Hexadecimal string length must be even.");
            }

            // Create a byte array to store the result
            byte[] byteArray = new byte[hexString.Length / 2];

            // Convert the hexadecimal string to a byte array
            for (int i = 0; i < byteArray.Length; i++)
            {
                byteArray[i] = Convert.ToByte(hexString.Substring(i * 2, 2), 16);
            }

            return byteArray;
        }

        public static byte[] SHA1Hash(byte[] data)
        {
            using (SHA1Managed sha1 = new SHA1Managed())
            {
                return sha1.ComputeHash(data);
            }
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

        public static byte[] StringToFString(string str)
        {
            byte[] strBytes = Encoding.ASCII.GetBytes(str);
            byte[] strlenBytes = BitConverter.GetBytes((uint)(str.Length + 1));
            byte[] fstring = new byte[strlenBytes.Length + strBytes.Length + 1];
            Array.Copy(strlenBytes, fstring, strlenBytes.Length);
            Array.Copy(strBytes, 0, fstring, strlenBytes.Length, strBytes.Length);
            fstring[fstring.Length - 1] = 0;
            return fstring;
        }

        public static byte[] UInt32ToBytes(uint a)
        {
            return BitConverter.GetBytes(a);
        }

        public static long Align(uint ptr, int alignment)
        {
            return ptr + alignment - 1 & ~(alignment - 1);
        }
    }
}
