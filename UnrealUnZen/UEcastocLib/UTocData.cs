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
                new Exception("Could not derive dependencies");
            }

            using (FileStream openUcas = new FileStream(ucasPath, FileMode.Open, FileAccess.Read))
            {
                List<byte[]> outputData = new List<byte[]>();

                foreach (var block in depFile.CompressionBlocks)
                {
                    openUcas.Seek((long)block.GetOffset(), SeekOrigin.Begin);
                    MessageBox.Show((long)block.GetOffset() + " " + block.GetCompressedSize() + " " + block.GetUncompressedSize());
                    byte[] buf = new byte[block.GetCompressedSize()];
                    int readBytes = openUcas.Read(buf, 0, (int)block.GetCompressedSize());

                    if (readBytes != block.GetCompressedSize())
                    {
                        new Exception("Could not read the correct size");
                    }

                    outputData.Add(buf);
                }


                return UCasDataParser.UnpackFileBuffer(this, depFile, outputData); ;
            }

        }
    }
}
