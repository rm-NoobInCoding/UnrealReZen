using UnrealReZen.Core.Helpers;

namespace UnrealReZen.Core
{
    public class FString(string value)
    {
        public string Value { get; set; } = value;
    }

    public class FName(string value)
    {
        public string Value { get; set; } = value;
    }

    public class FIoDirectoryIndexEntry
    {
        public uint Name { get; set; }
        public uint FirstChildEntry { get; set; }
        public uint NextSiblingEntry { get; set; }
        public uint FirstFileEntry { get; set; }

        public void Write(MemoryStream br)
        {
            br.Write(Name);
            br.Write(FirstChildEntry);
            br.Write(NextSiblingEntry);
            br.Write(FirstFileEntry);
        }
        public void AddFile(string[] fpathSections, uint fIndex, DirIndexWrapper structure)
        {
            if (fpathSections.Length == 0)
            {
                return;
            }

            if (fpathSections.Length == 1)
            {
                // Only one item, add file and return; base case
                string fname = fpathSections[0];
                uint nameIndex = (uint)structure.StrTable[fname];

                FIoFileIndexEntry newFile = new()
                {
                    Name = nameIndex,
                    NextFileEntry = Constants.NoneEntry,
                    UserData = fIndex
                };

                uint newEntryIndex = (uint)structure.Files.Count;
                structure.Files.Add(newFile);

                if (FirstFileEntry == Constants.NoneEntry)
                {
                    FirstFileEntry = newEntryIndex;
                }
                else
                {
                    FIoFileIndexEntry fentry = structure.Files[(int)FirstFileEntry];

                    // Filenames (with their path) are unique and will always be added
                    while (fentry.NextFileEntry != Constants.NoneEntry)
                    {
                        fentry = structure.Files[(int)fentry.NextFileEntry];
                    }

                    fentry.NextFileEntry = newEntryIndex;
                }

                return;
            }

            // Recursive case; find directory if present, otherwise add.
            string currDirName = fpathSections[0];
            uint currDirNameIndex = (uint)structure.StrTable[currDirName];

            uint possibleNewEntryIndex = (uint)structure.Dirs.Count;
            if (FirstChildEntry == Constants.NoneEntry)
            {
                FIoDirectoryIndexEntry newDirEntry = new()
                {
                    Name = currDirNameIndex,
                    FirstChildEntry = Constants.NoneEntry,
                    NextSiblingEntry = Constants.NoneEntry,
                    FirstFileEntry = Constants.NoneEntry
                };

                FirstChildEntry = possibleNewEntryIndex;
                structure.Dirs.Add(newDirEntry);
                FIoDirectoryIndexEntry currDir = newDirEntry;

                currDir.AddFile(fpathSections.Skip(1).ToArray(), fIndex, structure);
            }
            else
            {
                FIoDirectoryIndexEntry dentry = structure.Dirs[(int)FirstChildEntry];
                FIoDirectoryIndexEntry lastDentry = null;

                while (dentry != null && dentry.Name != currDirNameIndex)
                {
                    lastDentry = dentry;
                    dentry = dentry.NextSiblingEntry != Constants.NoneEntry ? structure.Dirs[(int)dentry.NextSiblingEntry] : null;
                }

                if (dentry != null && dentry.Name == currDirNameIndex)
                {
                    // Directory found
                    dentry.AddFile(fpathSections.Skip(1).ToArray(), fIndex, structure);
                }
                else
                {
                    // Add new directory
                    FIoDirectoryIndexEntry newDirEntry = new()
                    {
                        Name = currDirNameIndex,
                        FirstChildEntry = Constants.NoneEntry,
                        NextSiblingEntry = Constants.NoneEntry,
                        FirstFileEntry = Constants.NoneEntry
                    };

                    if (lastDentry != null)
                    {
                        lastDentry.NextSiblingEntry = possibleNewEntryIndex;
                    }
                    else
                    {
                        FirstChildEntry = possibleNewEntryIndex;
                    }

                    structure.Dirs.Add(newDirEntry);
                    FIoDirectoryIndexEntry currDir = newDirEntry;

                    currDir.AddFile(fpathSections.Skip(1).ToArray(), fIndex, structure);
                }
            }
        }
        public static int SizeOf()
        {
            return sizeof(uint) * 4;
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
        public void Write(MemoryStream br)
        {
            br.Write(Name);
            br.Write(NextFileEntry);
            br.Write(UserData);
        }
    }

    public class FIoPerfectHashSeeds(uint hashSeed)
    {
        public uint HashSeed { get; set; } = hashSeed;
    }

    public class FIoChunkID(ulong id, ushort index, byte padding, byte type)
    {
        public ulong ID { get; set; } = id;
        public ushort Index { get; set; } = index;
        public byte Padding { get; set; } = padding;
        public byte Type { get; set; } = type;

        public void Write(MemoryStream br)
        {
            br.Write(ID);
            br.Write(Index);
            br.Write(Padding);
            br.Write(Type);
        }
        public string ToHexString()
        {
            return $"{ID:X16}{Index:X4}{Padding:X2}{Type:X2}";
        }

        public static FIoChunkID FromHexString(string hexString)
        {
            if (hexString.Length != 24)
                throw new ArgumentException("Invalid hex string length", nameof(hexString));

            ulong id = ulong.Parse(hexString[..16], System.Globalization.NumberStyles.HexNumber);
            ushort index = ushort.Parse(hexString.Substring(16, 4), System.Globalization.NumberStyles.HexNumber);
            byte padding = byte.Parse(hexString.Substring(20, 2), System.Globalization.NumberStyles.HexNumber);
            byte type = byte.Parse(hexString.Substring(22, 2), System.Globalization.NumberStyles.HexNumber);

            return new FIoChunkID(id, index, padding, type);
        }
    }

    public class DirIndexWrapper
    {
        public List<FIoDirectoryIndexEntry> Dirs { get; set; }
        public List<FIoFileIndexEntry> Files { get; set; }
        public Dictionary<string, int> StrTable { get; set; }
        public string[] StrSlice { get; set; }

        public DirIndexWrapper(List<FIoDirectoryIndexEntry> dirs, List<FIoFileIndexEntry> files, Dictionary<string, int> strTable, string[] strSlice)
        {
            Dirs = dirs;
            Files = files;
            StrTable = strTable;
            StrSlice = strSlice;
        }
        public byte[] ToBytes()
        {
            MemoryStream output = new();

            uint dirCount = (uint)Dirs.Count;
            uint fileCount = (uint)Files.Count;
            uint strCount = (uint)StrSlice.Length;

            // Mount point string
            byte[] mountPointStr = StringHelpers.StringToFString(Constants.MountPoint);
            output.Write(mountPointStr);

            // Directory index entries
            output.Write(dirCount);
            foreach (FIoDirectoryIndexEntry directoryEntry in Dirs)
            {
                directoryEntry.Write(output);
            }

            // File index entries
            output.Write(fileCount);
            foreach (FIoFileIndexEntry fileEntry in Files)
            {
                fileEntry.Write(output);
            }

            // String table
            output.Write(strCount);
            foreach (string str in StrSlice)
            {
                byte[] strBytes = StringHelpers.StringToFString(str);
                output.Write(strBytes);
            }

            return output.ToArray();
        }
        public DirIndexWrapper() { }
    }

    public class FIoStoreTocCompressedBlockEntry
    {
        public byte[] Offset { get; set; } = new byte[5];
        public byte[] CompressedSize { get; set; } = new byte[3];
        public byte[] UncompressedSize { get; set; } = new byte[3];
        public byte CompressionMethod { get; set; }

        public void Write(MemoryStream br)
        {
            br.Write(Offset);
            br.Write(CompressedSize);
            br.Write(UncompressedSize);
            br.Write(CompressionMethod);
        }

        public void SetOffset(ulong offset)
        {
            byte[] bytes = new byte[5];
            for (ulong i = 0; i < 5; i++)
            {
                bytes[i] = (byte)(offset >> (int)(i * 8) & 0xFF);
            }
            Array.Copy(bytes, Offset, bytes.Length);
        }

        public void SetUncompressedSize(uint size)
        {
            UncompressedSize[0] = (byte)(size >> 0);
            UncompressedSize[1] = (byte)(size >> 8);
            UncompressedSize[2] = (byte)(size >> 16);
        }

        public void SetCompressedSize(uint size)
        {
            CompressedSize[0] = (byte)(size >> 0);
            CompressedSize[1] = (byte)(size >> 8);
            CompressedSize[2] = (byte)(size >> 16);
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
            return Offset[4] | (ulong)Offset[3] << 8 | (ulong)Offset[2] << 16 | (ulong)Offset[1] << 24 | (ulong)Offset[0] << 32;
        }

        public ulong GetLength()
        {
            return Length[4] | (ulong)Length[3] << 8 | (ulong)Length[2] << 16 | (ulong)Length[1] << 24 | (ulong)Length[0] << 32;
        }

        public void SetOffset(ulong offset)
        {
            Offset[0] = (byte)(offset >> 32);
            Offset[1] = (byte)(offset >> 24);
            Offset[2] = (byte)(offset >> 16);
            Offset[3] = (byte)(offset >> 8);
            Offset[4] = (byte)(offset >> 0);
        }

        public void SetLength(ulong length)
        {
            Length[0] = (byte)(length >> 32);
            Length[1] = (byte)(length >> 24);
            Length[2] = (byte)(length >> 16);
            Length[3] = (byte)(length >> 8);
            Length[4] = (byte)(length >> 0);
        }
        public void Write(MemoryStream br)
        {
            br.Write(Offset);
            br.Write(Length);
        }
    }

    public class FIoStoreTocEntryMeta
    {
        public FIoChunkHash ChunkHash { get; set; }
        public FIoStoreTocEntryMetaFlags Flags { get; set; }
        public void Write(MemoryStream br)
        {
            br.Write(ChunkHash.Hash);
            br.Write(ChunkHash.Padding);
            br.Write((byte)Flags);
        }
    }

    public class FIoChunkHash
    {
        public byte[] Hash { get; set; } = new byte[20];
        public byte[] Padding { get; set; } = new byte[12];
        public FIoChunkHash(byte[] hash)
        {
            Hash = hash;
        }
        public FIoChunkHash(byte[] hash, byte[] padding)
        {
            Hash = hash;
            Padding = padding;
        }
    }
}
