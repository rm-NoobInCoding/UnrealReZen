using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;

namespace UEcastocLib
{
    public static class UCasDataParser
    {
        private static void UnpackFile(UTocData utoc, GameFileMetaData fdata, List<byte[]> blockData, string outDir)
        {
            Directory.CreateDirectory(outDir);
            List<byte> outputData = new List<byte>();

            for (int i = 0; i < blockData.Count; i++)
            {
                string method = utoc.CompressionMethods[fdata.CompressionBlocks[i].CompressionMethod];
                Func<byte[], uint, byte[]> decomp = CompressionUtils.DecompressionMethods[method];

                if (decomp == null)
                {
                    throw new Exception($"Decompression method {method} not known");
                }

                byte[] newData = decomp(blockData[i], fdata.CompressionBlocks[i].GetUncompressedSize());
                outputData.AddRange(newData);
            }

            string fpath = Path.Combine(outDir, fdata.FilePath);
            string directory = Path.GetDirectoryName(fpath);
            Directory.CreateDirectory(directory);
            if (fpath.Length >= 255) fpath = @"\\?\" + fpath;
            File.WriteAllBytes(fpath, outputData.ToArray());
           
            
        }
        public static byte[] UnpackFileBuffer(UTocData utoc, GameFileMetaData fdata, List<byte[]> blockData)
        {
            List<byte> outputData = new List<byte>();

            for (int i = 0; i < blockData.Count; i++)
            {
                string method = utoc.CompressionMethods[fdata.CompressionBlocks[i].CompressionMethod];
                Func<byte[], uint, byte[]> decomp = CompressionUtils.DecompressionMethods[method];

                if (decomp == null)
                {
                    throw new Exception($"Decompression method {method} not known");
                }

                byte[] newData = decomp(blockData[i], fdata.CompressionBlocks[i].GetUncompressedSize());
                outputData.AddRange(newData);

            }
            return outputData.ToArray();

        }
        private static List<GameFileMetaData> MatchRegex(UTocData utoc, string regex)
        {
            List<GameFileMetaData> filesToUnpack = new List<GameFileMetaData>();

            foreach (var v in utoc.Files)
            {
                bool match = Regex.IsMatch(v.FilePath, regex);

                if (match && v.FilePath != Constants.DepFileName)
                {
                    filesToUnpack.Add(v);
                }
            }

            return filesToUnpack;
        }
        public static int UnpackUcasFiles(this UTocData utoc, string ucasPath, string outDir, string regex)
        {
            outDir += utoc.MountPoint;
            int filesUnpacked = 0;

            using (FileStream openUcas = File.OpenRead(ucasPath))
            {
                List<GameFileMetaData> filesToUnpack = MatchRegex(utoc, regex);

                foreach (var v in filesToUnpack)
                {
                    List<byte[]> compressionBlockData = new List<byte[]>();

                    foreach (var b in v.CompressionBlocks)
                    {
                        openUcas.Seek((long)b.GetOffset(), SeekOrigin.Begin);
                        byte[] buf = new byte[utoc.IsEncrypted() ? Helpers.Align(b.GetCompressedSize(), 16) : b.GetCompressedSize()];

                        openUcas.Read(buf, 0, buf.Length);

                        if (utoc.IsEncrypted())
                        {
                            var temp = Helpers.DecryptAES(buf, utoc.aesKey);
                            buf = new byte[b.GetCompressedSize()];
                            Array.Copy(temp, buf, buf.Length);
                        }

                        /*if (readBytes != b.GetCompressedSize())
                        {
                            throw new Exception("Could not read the correct size");
                        }*/

                        compressionBlockData.Add(buf);
                    }

                    UnpackFile(utoc, v, compressionBlockData, outDir);
                    filesUnpacked++;
                }
            }

            return filesUnpacked;
        }
    }
}
