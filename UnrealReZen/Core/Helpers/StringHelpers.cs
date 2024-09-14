using System.Text;

namespace UnrealReZen.Core.Helpers
{
    public static class StringHelpers
    {

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

        public static string ReadFString(this BinaryReader reader)
        {
            var length = reader.ReadInt32();
            if (length > 0)
            {
                byte[] strBytes = reader.ReadBytes(length);
                return Encoding.UTF8.GetString(strBytes, 0, strBytes.Length - 1);
            }
            else if (length < 0)
            {
                length *= -2;
                byte[] strBytes = reader.ReadBytes(length);
                return Encoding.Unicode.GetString(strBytes, 0, strBytes.Length - 2);
            }
            else
                return "";
        }

        public static byte[] HexStringToByteArray(string hexString)
        {
            if (hexString.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
            {
                hexString = hexString.Substring(2);
            }

            if (hexString.Length % 2 != 0)
            {
                throw new ArgumentException("Hexadecimal string length must be even.");
            }

            byte[] byteArray = new byte[hexString.Length / 2];

            for (int i = 0; i < byteArray.Length; i++)
            {
                byteArray[i] = Convert.ToByte(hexString.Substring(i * 2, 2), 16);
            }

            return byteArray;
        }


    }
}
