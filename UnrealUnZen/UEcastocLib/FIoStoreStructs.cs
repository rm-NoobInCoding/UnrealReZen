using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;


namespace UEcastocLib
{
    public class FIoContainerID
    {
        public ulong Value { get; set; }

        public FIoContainerID(ulong value)
        {
            Value = value;
        }
    }

    public enum EIoContainerFlags : byte
    {
        NoneContainerFlag = 0,
        CompressedContainerFlag = 1 << 0,
        EncryptedContainerFlag = 1 << 1,
        SignedContainerFlag = 1 << 2,
        IndexedContainerFlag = 1 << 3
    }

    public enum FIoStoreTocEntryMetaFlags : byte
    {
        NoneMetaFlag,
        CompressedMetaFlag,
        MemoryMappedMetaFlag
    }

    public class FString
    {
        public string Value { get; set; }

        public FString(string value)
        {
            Value = value;
        }
    }

    public class FName
    {
        public string Value { get; set; }

        public FName(string value)
        {
            Value = value;
        }
    }

    public class FIoDirectoryIndexEntry
    {
        public uint Name { get; set; }
        public uint FirstChildEntry { get; set; }
        public uint NextSiblingEntry { get; set; }
        public uint FirstFileEntry { get; set; }

        public void Read(BinaryReader br)
        {
            Name = br.ReadUInt32();
            FirstChildEntry = br.ReadUInt32();
            NextSiblingEntry = br.ReadUInt32();
            FirstFileEntry = br.ReadUInt32();
        }
    }

    public class FIoFileIndexEntry
    {
        public uint Name { get; set; }
        public uint NextFileEntry { get; set; }
        public uint UserData { get; set; }

        public void Read(BinaryReader br)
        {
            Name = br.ReadUInt32();
            NextFileEntry = br.ReadUInt32();
            UserData = br.ReadUInt32();

        }
    }

    public class FIoChunkID
    {
        public ulong ID { get; set; }
        public ushort Index { get; set; }
        public byte Padding { get; set; }
        public byte Type { get; set; }

        public FIoChunkID(ulong id, ushort index, byte padding, byte type)
        {
            ID = id;
            Index = index;
            Padding = padding;
            Type = type;
        }

        public string ToHexString()
        {
            return $"{ID:X16}{Index:X4}{Padding:X2}{Type:X2}";
        }

        public static FIoChunkID FromHexString(string hexString)
        {
            if (hexString.Length != 24)
                throw new ArgumentException("Invalid hex string length", nameof(hexString));

            ulong id = ulong.Parse(hexString.Substring(0, 16), System.Globalization.NumberStyles.HexNumber);
            ushort index = ushort.Parse(hexString.Substring(16, 4), System.Globalization.NumberStyles.HexNumber);
            byte padding = byte.Parse(hexString.Substring(20, 2), System.Globalization.NumberStyles.HexNumber);
            byte type = byte.Parse(hexString.Substring(22, 2), System.Globalization.NumberStyles.HexNumber);

            return new FIoChunkID(id, index, padding, type);
        }
    }

    public class DirIndexWrapper
    {
        public FIoDirectoryIndexEntry[] Dirs { get; set; }
        public FIoFileIndexEntry[] Files { get; set; }
        public Dictionary<string, int> StrTable { get; set; }
        public string[] StrSlice { get; set; }

        public DirIndexWrapper(FIoDirectoryIndexEntry[] dirs, FIoFileIndexEntry[] files, Dictionary<string, int> strTable, string[] strSlice)
        {
            Dirs = dirs;
            Files = files;
            StrTable = strTable;
            StrSlice = strSlice;
        }
    }

    public class FIoStoreTocCompressedBlockEntry
    {
        public byte[] Offset { get; set; } = new byte[5];
        public byte[] CompressedSize { get; set; } = new byte[3];
        public byte[] UncompressedSize { get; set; } = new byte[3];
        public byte CompressionMethod { get; set; }

        public void Read(BinaryReader br)
        {
            Offset = br.ReadBytes(5);
            CompressedSize = br.ReadBytes(3);
            UncompressedSize = br.ReadBytes(3);
            CompressionMethod = br.ReadByte();
        }
        public ulong GetLength()
        {
            return GetUncompressedSize();
        }

        public ulong GetOffset()
        {
            byte[] realdata = Offset.Concat(new byte[] { 0, 0, 0 }).ToArray();
            return BitConverter.ToUInt64(realdata, 0);
        }

        public uint GetCompressedSize()
        {
            return BitConverter.ToUInt32(CompressedSize.Concat(new byte[] { 0 }).ToArray(), 0);
        }

        public uint GetUncompressedSize()
        {
            return BitConverter.ToUInt32(UncompressedSize.Concat(new byte[] { 0 }).ToArray(), 0);
        }

        public void SetOffset(ulong offset)
        {
            byte[] offsetBytes = BitConverter.GetBytes(offset);
            Array.Copy(offsetBytes, Offset, Math.Min(offsetBytes.Length, Offset.Length));
        }

        public void SetCompressedSize(uint size)
        {
            byte[] sizeBytes = BitConverter.GetBytes(size);
            Array.Copy(sizeBytes, CompressedSize, Math.Min(sizeBytes.Length, CompressedSize.Length));
        }

        public void SetUncompressedSize(uint size)
        {
            byte[] sizeBytes = BitConverter.GetBytes(size);
            Array.Copy(sizeBytes, UncompressedSize, Math.Min(sizeBytes.Length, UncompressedSize.Length));
        }
    }

    public class FIoOffsetAndLength
    {
        public byte[] Offset { get; set; }
        public byte[] Length { get; set; }

        public FIoOffsetAndLength()
        {
            Offset = new byte[5];
            Length = new byte[5];
        }

        public FIoOffsetAndLength(byte[] offset, byte[] length)
        {
            Offset = offset;
            Length = length;
        }

        public ulong GetOffset()
        {
            return (ulong)Offset[4] | ((ulong)Offset[3] << 8) | ((ulong)Offset[2] << 16) | ((ulong)Offset[1] << 24) | ((ulong)Offset[0] << 32);
        }

        public ulong GetLength()
        {
            return (ulong)Length[4] | ((ulong)Length[3] << 8) | ((ulong)Length[2] << 16) | ((ulong)Length[1] << 24) | ((ulong)Length[0] << 32);
        }

        public void SetOffset(ulong offset)
        {
            byte[] offsetBytes = BitConverter.GetBytes(offset);
            Array.Copy(offsetBytes, Offset, Math.Min(offsetBytes.Length, Offset.Length));
        }

        public void SetLength(ulong length)
        {
            byte[] lengthBytes = BitConverter.GetBytes(length);
            Array.Copy(lengthBytes, Length, Math.Min(lengthBytes.Length, Length.Length));
        }
    }

    public class FIoStoreTocEntryMeta
    {
        public FIoChunkHash ChunkHash { get; set; }
        public FIoStoreTocEntryMetaFlags Flags { get; set; }
    }

    public class FIoChunkHash
    {
        public byte[] Hash { get; set; } = new byte[20];
        public byte[] Padding { get; set; } = new byte[12];
    }

    public static class ByteArrayExtensions
    {
        public static byte[] Concat(this byte[] first, byte[] second)
        {
            byte[] result = new byte[first.Length + second.Length];
            first.CopyTo(result, 0);
            second.CopyTo(result, first.Length);
            return result;
        }
    }

    public static class FIoOffsetAndLengthExtensions
    {
        public static void SetOffset(this FIoOffsetAndLength offsetAndLength, ulong offset)
        {
            offsetAndLength.Offset = BitConverter.GetBytes(offset);
        }

        public static void SetLength(this FIoOffsetAndLength offsetAndLength, ulong length)
        {
            offsetAndLength.Length = BitConverter.GetBytes(length);
        }
    }

}

