using System.Security.Cryptography;
using System.Text;

namespace UnrealReZen.Core;

public static class PakWriter
{
    // Pak v8 = FNameBasedCompressionMethod, legacy index format, no encryption
    private const uint PakMagic = 0x5A6F12E1;
    private const int PakVersion = 8;
    private const int EntryStructSize = 8 + 8 + 8 + 4 + 20 + 4 + 1; // 53 bytes

    public static void WritePak(string outputPath, string mountPoint, IEnumerable<(string virtualPath, string diskPath)> files)
    {
        var fileList = files.ToList();
        using var f = new FileStream(outputPath, FileMode.Create, FileAccess.Write);
        var indexEntries = new List<(string virtualPath, long entryOffset, long size, byte[] hash)>();

        foreach (var (virtualPath, diskPath) in fileList)
        {
            var data = File.ReadAllBytes(diskPath);
            long entryOffset = f.Position;
            using var sha1 = SHA1.Create();
            var hash = sha1.ComputeHash(data);

            WriteEntry(f, entryOffset, data.LongLength, hash);
            f.Write(data);
            indexEntries.Add((virtualPath, entryOffset, data.LongLength, hash));
        }

        long indexOffset = f.Position;
        using var idx = new MemoryStream();
        WriteFString(idx, mountPoint);
        Write32(idx, indexEntries.Count);
        foreach (var (virtualPath, entryOffset, size, hash) in indexEntries)
        {
            WriteFString(idx, virtualPath);
            WriteEntry(idx, entryOffset, size, hash);
        }

        byte[] indexBytes = idx.ToArray();
        f.Write(indexBytes);

        using var sha1Idx = SHA1.Create();
        var indexHash = sha1Idx.ComputeHash(indexBytes);

        // FPakInfo footer
        f.Write(new byte[16]);                      // EncryptionKeyGuid = zeros
        f.WriteByte(0);                             // EncryptedIndex = false
        Write32(f, unchecked((int)PakMagic));
        Write32(f, PakVersion);
        Write64(f, indexOffset);
        Write64(f, indexBytes.LongLength);
        f.Write(indexHash);                         // 20-byte SHA1 of index
        f.Write(new byte[4 * 32]);                  // 4 compression method name slots (all empty = None)
    }

    // Writes an FPakEntry record (used both inline before data and in the index)
    private static void WriteEntry(Stream s, long selfOffset, long size, byte[] hash)
    {
        Write64(s, selfOffset);     // Offset (absolute, points to this inline entry)
        Write64(s, size);           // CompressedSize
        Write64(s, size);           // UncompressedSize
        Write32(s, 0);              // CompressionMethodIndex = 0 (None)
        s.Write(hash, 0, 20);       // SHA1 hash of uncompressed data
        Write32(s, unchecked((int)0u)); // CompressionBlockSize = 0
        s.WriteByte(0);             // Flags = 0
    }

    // FString: int32 char-count (includes null) + UTF-8 bytes + null terminator
    private static void WriteFString(Stream s, string str)
    {
        var bytes = Encoding.UTF8.GetBytes(str);
        Write32(s, bytes.Length + 1);
        s.Write(bytes);
        s.WriteByte(0);
    }

    private static void Write32(Stream s, int v) => s.Write(BitConverter.GetBytes(v));
    private static void Write64(Stream s, long v) => s.Write(BitConverter.GetBytes(v));
}
