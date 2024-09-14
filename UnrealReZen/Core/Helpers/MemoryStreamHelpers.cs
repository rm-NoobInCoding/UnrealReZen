using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace UnrealReZen.Core.Helpers
{

    public static class MemoryStreamOverrides
    {

        #region Write Method
        //Write Method
        public static void Write(this MemoryStream ms, byte[] input)
        {
            ms.Write(input, 0, input.Length);
        }

        public static void Write(this MemoryStream ms,string input, Encoding encoding)
        {
            ms.Write(encoding.GetBytes(input), 0, encoding.GetBytes(input).Length);
        }

        public static void Write(this MemoryStream ms,double input)
        {
            ms.Write(BitConverter.GetBytes(input), 0, 8);
        }

        public static void Write(this MemoryStream ms,float input)
        {
            ms.Write(BitConverter.GetBytes(input), 0, 4);
        }

        public static void Write(this MemoryStream ms,long input)
        {
            ms.Write(BitConverter.GetBytes(input), 0, 8);
        }

        public static void Write(this MemoryStream ms,ulong input)
        {
            ms.Write(BitConverter.GetBytes(input), 0, 8);
        }

        public static void Write(this MemoryStream ms,int input)
        {
            ms.Write(BitConverter.GetBytes(input), 0, 4);
        }

        public static void Write(this MemoryStream ms,uint input)
        {
            ms.Write(BitConverter.GetBytes(input), 0, 4);

        }

        public static void Write(this MemoryStream ms,short input)
        {
            ms.Write(BitConverter.GetBytes(input), 0, 2);
        }

        public static void Write(this MemoryStream ms,ushort input)
        {
            ms.Write(BitConverter.GetBytes(input), 0, 2);

        }

        public static void Write(this MemoryStream ms,byte input)
        {
            ms.WriteByte(input);
        }

        #endregion

        #region Read Method
        //Read Method
        public static byte[] ReadBytes(this MemoryStream ms, int size)
        {
            var data = new byte[size];
            ms.Read(data, 0, size);
            return data;
        }

        public static byte[] ReadToEnd(this MemoryStream ms)
        {
            var data = new byte[ms.Length - ms.Position];
            ms.Read(data, 0, (int)(ms.Length - ms.Position));
            return data;
        }

        public static byte[] ReadBytes(this MemoryStream ms, long size)
        {
            var data = new byte[size];
            ms.Read(data, 0, (int)size);
            return data;
        }

        public static short ReadInt16(this MemoryStream ms)
        {
            var data = new byte[2];
            ms.Read(data, 0, 2);
            return BitConverter.ToInt16(data, 0);
        }

        public static ushort ReadUInt16(this MemoryStream ms)
        {
            var data = new byte[2];
            ms.Read(data, 0, 2);
            return BitConverter.ToUInt16(data, 0);
        }

        public static int ReadInt32(this MemoryStream ms)
        {
            var data = new byte[4];
            ms.Read(data, 0, 4);
            return BitConverter.ToInt32(data, 0);
        }

        public static uint ReadUInt32(this MemoryStream ms)
        {
            var data = new byte[4];
            ms.Read(data, 0, 4);
            return BitConverter.ToUInt32(data, 0);
        }

        public static long ReadInt64(this MemoryStream ms)
        {
            var data = new byte[8];
            ms.Read(data, 0, 8);
            return BitConverter.ToInt64(data, 0);
        }

        public static ulong ReadUInt64(this MemoryStream ms)
        {
            var data = new byte[8];
            ms.Read(data, 0, 8);
            return BitConverter.ToUInt64(data, 0);
        }

        public static float ReadSingle(this MemoryStream ms)
        {
            var data = new byte[4];
            ms.Read(data, 0, 4);
            return BitConverter.ToSingle(data, 0);
        }

        public static string ReadString(this MemoryStream ms, int size, Encoding encoding)
        {
            var data = new byte[size];
            ms.Read(data, 0, size);
            return encoding.GetString(data);
        }

        public static string ReadString(this MemoryStream ms)
        {
            List<byte> output = new List<byte>();
            while (true)
            {
                byte reader = (byte)ms.ReadByte();
                if (reader == 0)
                {
                    output.Add(reader);
                    break;
                }
                output.Add(reader);
            }
            return Encoding.ASCII.GetString(output.ToArray());
        }

        public static string ReadStringUTF8(this MemoryStream ms)
        {
            List<byte> output = new List<byte>();
            while (true)
            {
                byte reader = (byte)ms.ReadByte();
                if (reader == 0)
                {
                    output.Add(reader);
                    break;
                }
                output.Add(reader);
            }
            return Encoding.UTF8.GetString(output.ToArray());
        }
        #endregion

        #region Location Method
        //Location Method
        public static void Skip(this MemoryStream ms,int to)
        {
            ms.Seek(to, SeekOrigin.Current);
        }

        public static void Skip(this MemoryStream ms, long to)
        {
            ms.Seek(to, SeekOrigin.Current);
        }


        public static long Tell(this MemoryStream ms)
        {
            return ms.Position;
        }

        public static void Seek(this MemoryStream ms, long to)
        {
            ms.Seek(to, SeekOrigin.Begin);
        }

        public static void Pos(this MemoryStream ms, int Base)
        {
            ms.Position = Base;
        }
        #endregion
    }
}
