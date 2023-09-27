using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace UEcastocLib
{

    public class MemoryStream : System.IO.MemoryStream
    {
        public MemoryStream(byte[] stream) : base(stream) { }

        public MemoryStream() : base() { }

        #region Write Method
        //Write Method
        public void Write(byte[] input)
        {
            base.Write(input, 0, input.Length);
        }

        public void Write(string input, Encoding encoding)
        {
            base.Write(encoding.GetBytes(input), 0, encoding.GetBytes(input).Length);
        }

        public void Write(double input)
        {
            base.Write(BitConverter.GetBytes(input), 0, 8);
        }

        public void Write(float input)
        {
            base.Write(BitConverter.GetBytes(input), 0, 4);
        }

        public void Write(Int64 input)
        {
            base.Write(BitConverter.GetBytes(input), 0, 8);
        }
        public void Write(ulong input)
        {
            base.Write(BitConverter.GetBytes(input), 0, 8);
        }
        public void Write(int input)
        {
            base.Write(BitConverter.GetBytes(input), 0, 4);
        }
        public void Write(uint input)
        {
            base.Write(BitConverter.GetBytes(input), 0, 4);

        }

        public void Write(short input)
        {
            base.Write(BitConverter.GetBytes(input), 0, 2);
        }
        public void Write(ushort input)
        {
            base.Write(BitConverter.GetBytes(input), 0, 2);

        }

        public void Write(byte input)
        {
            base.WriteByte(input);
        }

        #endregion

        #region Read Method
        //Read Method
        public byte[] ReadBytes(int size)
        {
            var data = new byte[size];
            base.Read(data, 0, size);
            return data;
        }

        public byte[] ReadToEnd()
        {
            var data = new byte[base.Length - base.Position];
            base.Read(data, 0, (int)(base.Length - base.Position));
            return data;
        }

        public byte[] ReadBytes(long size)
        {
            var data = new byte[size];
            base.Read(data, 0, (int)size);
            return data;
        }

        public Int16 ReadInt16()
        {
            var data = new byte[2];
            base.Read(data, 0, 2);
            return BitConverter.ToInt16(data, 0);
        }

        public UInt16 ReadUInt16()
        {
            var data = new byte[2];
            base.Read(data, 0, 2);
            return BitConverter.ToUInt16(data, 0);
        }

        public Int32 ReadInt32()
        {
            var data = new byte[4];
            base.Read(data, 0, 4);
            return BitConverter.ToInt32(data, 0);
        }

        public UInt32 ReadUInt32()
        {
            var data = new byte[4];
            base.Read(data, 0, 4);
            return BitConverter.ToUInt32(data, 0);
        }

        public Int64 ReadInt64()
        {
            var data = new byte[8];
            base.Read(data, 0, 8);
            return BitConverter.ToInt64(data, 0);
        }

        public UInt64 ReadUInt64()
        {
            var data = new byte[8];
            base.Read(data, 0, 8);
            return BitConverter.ToUInt64(data, 0);
        }

        public float ReadSingle()
        {
            var data = new byte[4];
            base.Read(data, 0, 4);
            return BitConverter.ToSingle(data, 0);
        }

        public string ReadString(int size, Encoding encoding)
        {
            var data = new byte[size];
            base.Read(data, 0, size);
            return encoding.GetString(data);
        }

        public string ReadString()
        {
            List<byte> output = new List<byte>();
            while (true)
            {
                byte reader = (byte)base.ReadByte();
                if (reader == 0)
                {
                    output.Add(reader);
                    break;
                }
                output.Add(reader);
            }
            return Encoding.ASCII.GetString(output.ToArray());
        }

        public string ReadStringUTF8()
        {
            List<byte> output = new List<byte>();
            while (true)
            {
                byte reader = (byte)base.ReadByte();
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
        public void Skip(int to)
        {
            base.Seek(to, SeekOrigin.Current);
        }

        public void Skip(long to)
        {
            base.Seek(to, SeekOrigin.Current);
        }


        public long Tell()
        {
            return base.Position;
        }

        public void Seek(long to)
        {
            base.Seek(to, SeekOrigin.Begin);
        }

        public void Pos(int Base)
        {
            base.Position = Base;
        }
        #endregion
    }
    static class ByteSearch
    {
        static readonly int[] Empty = new int[0];

        public static int[] Locate(this byte[] self, byte[] candidate)
        {
            if (IsEmptyLocate(self, candidate))
                return Empty;

            var list = new List<int>();

            for (int i = 0; i < self.Length; i++)
            {
                if (!IsMatch(self, i, candidate))
                    continue;

                list.Add(i);
            }

            return list.Count == 0 ? Empty : list.ToArray();
        }

        static bool IsMatch(byte[] array, int position, byte[] candidate)
        {
            if (candidate.Length > (array.Length - position))
                return false;

            for (int i = 0; i < candidate.Length; i++)
                if (array[position + i] != candidate[i])
                    return false;

            return true;
        }

        static bool IsEmptyLocate(byte[] array, byte[] candidate)
        {
            return array == null
                || candidate == null
                || array.Length == 0
                || candidate.Length == 0
                || candidate.Length > array.Length;
        }

    }
}
