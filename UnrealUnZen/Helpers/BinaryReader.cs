using System;
using System.Collections.Generic;
using System.Text;

namespace StarfieldArchiveTool
{
    internal class BinaryReader : System.IO.BinaryReader
    {
        public BinaryReader(System.IO.Stream stream) : base(stream) { }

        public string Magic(int size)
        {
            return Encoding.Default.GetString(ReadBytes(size));
        }

        public void Skip(int To)
        {
            base.BaseStream.Seek(To, System.IO.SeekOrigin.Current);
        }

        public void Seek(int To)
        {
            base.BaseStream.Seek(To, System.IO.SeekOrigin.Begin);
        }

        public void Seek(long To)
        {
            base.BaseStream.Seek(To, System.IO.SeekOrigin.Begin);
        }

        public void Seek(uint To)
        {
            base.BaseStream.Seek(To, System.IO.SeekOrigin.Begin);
        }

        public byte[] ReadToEnd()
        {
            return base.ReadBytes((int)base.BaseStream.Length - (int)base.BaseStream.Position);
        }

        public void Pos(int Base)
        {
            base.BaseStream.Position = Base;
        }

        public UInt64[] ReadULonges(int Size)
        {
            List<UInt64> ulongs = new List<UInt64>();
            for (int i = 0; i < Size; i++)
            {
                ulongs.Add(ReadUInt64());
            }
            return ulongs.ToArray();
        }

        public UInt32[] ReadUInts(int Size)
        {
            List<UInt32> uints = new List<UInt32>();
            for (int i = 0; i < Size; i++)
            {
                uints.Add(ReadUInt32());
            }
            return uints.ToArray();
        }

        public long Tell()
        {
            return base.BaseStream.Position;
        }

        public string ReadNullTerminated()
        {
            var bldr = new System.Text.StringBuilder();
            int nc;
            while ((nc = base.Read()) > 0)
                bldr.Append((char)nc);
            return bldr.ToString();
        }

        public string ReadNullTerminatedAtOffset(int pos)
        {
            long curpos = base.BaseStream.Position;
            base.BaseStream.Seek(pos, System.IO.SeekOrigin.Begin);
            var bldr = new System.Text.StringBuilder();
            int nc;
            while ((nc = base.Read()) > 0)
                bldr.Append((char)nc);
            base.BaseStream.Seek(curpos, System.IO.SeekOrigin.Begin);
            return bldr.ToString();
        }
        public byte[] ReadStringBytesAtOffset(int pos)
        {
            return Encoding.UTF8.GetBytes(ReadNullTerminatedAtOffset(pos) + "\0");
        }

        public byte[] ReadAtOffset(long pos, int length)
        {
            long curpos = base.BaseStream.Position;
            base.BaseStream.Seek(pos, System.IO.SeekOrigin.Begin);
            byte[] ret = base.ReadBytes(length);
            base.BaseStream.Seek(curpos, System.IO.SeekOrigin.Begin);
            return ret;
        }
    }
}
