using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;

namespace UEcastocLib
{
    public class UTocData
    {
        public UTocHeader Header;
        public string MountPoint;
        public List<GameFileMetaData> Files;
        public List<string> CompressionMethods;
        public byte[] aesKey;

        public UTocData()
        {
            CompressionMethods = new List<string>();
            Files = new List<GameFileMetaData>();
        }

        public bool IsEncrypted()
        {
            return Header.IsEncrypted();
        }

        public byte[] UnpackDependencies(string ucasPath)
        {
            GameFileMetaData depFile = Files.FirstOrDefault(f => f.FilePath == Constants.DepFileName);

            if (depFile == null)
            {
                throw new Exception("Could not derive dependencies");
            }

            using (FileStream openUcas = new FileStream(ucasPath, FileMode.Open, FileAccess.Read))
            {
                List<byte[]> outputData = new List<byte[]>();

                foreach (var block in depFile.CompressionBlocks)
                {
                    openUcas.Seek((long)block.GetOffset(), SeekOrigin.Begin);
                    byte[] buf = new byte[this.IsEncrypted() ? Helpers.Align(block.GetCompressedSize(), 16) : block.GetCompressedSize()];
                    openUcas.Read(buf, 0, (int)buf.Length);

                    if (this.IsEncrypted())
                    {
                        var temp = Helpers.DecryptAES(buf, this.aesKey);
                        buf = new byte[block.GetCompressedSize()];
                        Array.Copy(temp, buf, buf.Length);
                    }

                    if (buf.Length != block.GetCompressedSize())
                    {
                        throw new Exception("Could not read the correct size");
                    }

                    outputData.Add(buf);
                }


                return UCasDataParser.UnpackFileBuffer(this, depFile, outputData); ;
            }

        }
    }
}
