using Microsoft.WindowsAPICodePack.Dialogs;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using UEcastocLib;

namespace UnrealUnZen
{
    public partial class MainTool : Form
    {
        public MainTool()
        {
            InitializeComponent();
        }

        string UTocFileAddress = "";
        UTocData UTocFile = new UTocData();

        private void Form1_Load(object sender, EventArgs e)
        {
            RepackMethodCMB.SelectedIndex = 0;
            UTocVerCMB.SelectedIndex = 0;
        }
        public static TreeNode MakeTreeFromPaths(List<string> paths, string rootNodeName = "", char separator = '/')
        {
            var rootNode = new TreeNode(rootNodeName);
            foreach (var path in paths.Where(x => !string.IsNullOrEmpty(x.Trim())))
            {
                var currentNode = rootNode;
                var pathItems = path.Split(separator);
                foreach (var item in pathItems)
                {
                    var tmp = currentNode.Nodes.Cast<TreeNode>().Where(x => x.Text.Equals(item));
                    currentNode = tmp.Count() > 0 ? tmp.Single() : currentNode.Nodes.Add(item);
                }
            }
            return rootNode;
        }
        private void OpenTocBTN_Click(object sender, EventArgs e)
        {
            OpenFileDialog ofd = new OpenFileDialog();
            ofd.Filter = "UToc files(*.utoc)|*.utoc";
            if (ofd.ShowDialog() == DialogResult.OK)
            {
                UTocFileAddress = ofd.FileName;
                UTocFile = UTocDataParser.ParseUtocFile(UTocFileAddress, Helpers.HexStringToByteArray(AESKey.Text));
                List<string> pathes = new List<string>();

                for (int i = 0; i < UTocFile.Files.Count; i++)
                {
                    pathes.Add(UTocFile.Files[i].FilePath);
                }

                ArchiveViewTV.Nodes.Clear();
                ArchiveViewTV.Nodes.Add(MakeTreeFromPaths(pathes, Path.GetFileNameWithoutExtension(UTocFileAddress), '\\'));
                OpenTocBTN.Text = "Load TOC (Loaded " + Path.GetFileNameWithoutExtension(UTocFileAddress) + ")";
                UnpackBTN.Enabled = true;
                RepackBTN.Enabled = true;
                saveManifestToolStripMenuItem.Enabled = true;
                fixManifestToolStripMenuItem.Enabled = true;
            }
        }
        private void UnpackBTN_Click(object sender, EventArgs e)
        {
            Directory.CreateDirectory(UTocFileAddress + "_Export");
            int exportcount = UTocFile.UnpackUcasFiles(Path.ChangeExtension(UTocFileAddress, ".ucas"), UTocFileAddress + "_Export", RegexUnpack.Text);
            MessageBox.Show(exportcount + " file(s) extracted!");
        }


        private void RepackBTN_Click(object sender, EventArgs e)
        {
            CommonOpenFileDialog dialog = new CommonOpenFileDialog();
            dialog.IsFolderPicker = true;
            SaveFileDialog saveFileDialog = new SaveFileDialog();
            saveFileDialog.Filter = "utoc file|*.utoc";
            if (dialog.ShowDialog() == CommonFileDialogResult.Ok && saveFileDialog.ShowDialog() == DialogResult.OK)
            {
                Manifest manifest = UTocFile.ConstructManifest(Path.ChangeExtension(UTocFileAddress, ".ucas"));
                foreach (var f in manifest.Files.ToList())
                {
                    if (!File.Exists(Path.Combine(dialog.FileName, f.Filepath.Replace("/", "\\"))) && f.Filepath != "dependencies")
                    {
                        manifest.Files.Remove(f);
                        manifest.Deps.ChunkIDToDependencies.Remove(ulong.Parse(f.ChunkID.Substring(0, 16), System.Globalization.NumberStyles.HexNumber));
                    }
                }
                //File.WriteAllText("debg.json", JsonConvert.SerializeObject(manifest, Formatting.Indented));
                int res = Packer.PackGameFiles(dialog.FileName, manifest, saveFileDialog.FileName, RepackMethodCMB.GetItemText(RepackMethodCMB.SelectedItem), AESKey.Text);
                if (res != 0)
                {
                    MessageBox.Show(res + " file(s) packed!");
                }
            }
        }
        private void MountPointTXB_TextChanged(object sender, EventArgs e)
        {
            Constants.MountPoint = MountPointTXB.Text;
        }

        private void saveManifestToolStripMenuItem_Click(object sender, EventArgs e)
        {
            SaveFileDialog saveFile = new SaveFileDialog();
            saveFile.Filter = "Manifest Json File|*.json";
            saveFile.FileName = Path.GetFileNameWithoutExtension(UTocFileAddress);
            if (saveFile.ShowDialog() == DialogResult.OK)
            {
                Manifest manifest = UTocFile.ConstructManifest(Path.ChangeExtension(UTocFileAddress, ".ucas"));
                File.WriteAllText(saveFile.FileName, JsonConvert.SerializeObject(manifest, Formatting.Indented));
                MessageBox.Show("Done!");
            }
        }

        private void fixManifestToolStripMenuItem_Click(object sender, EventArgs e)
        {
            OpenFileDialog openFileDialog = new OpenFileDialog();
            openFileDialog.Filter = "Json Manifest File|*.json";
            CommonOpenFileDialog dialog = new CommonOpenFileDialog();
            dialog.IsFolderPicker = true;
            if (dialog.ShowDialog() == CommonFileDialogResult.Ok && openFileDialog.ShowDialog() == DialogResult.OK)
            {
                Manifest manifest = Packer.JsonToManifest(openFileDialog.FileName);
                foreach (var f in manifest.Files.ToList())
                {
                    if (!File.Exists(Path.Combine(dialog.FileName, f.Filepath.Replace("/", "\\"))) && f.Filepath != "dependencies")
                    {
                        manifest.Files.Remove(f);
                        manifest.Deps.ChunkIDToDependencies.Remove(ulong.Parse(f.ChunkID.Substring(0, 16), System.Globalization.NumberStyles.HexNumber));
                    }
                }
                File.WriteAllText(openFileDialog.FileName + ".Fixed_json", JsonConvert.SerializeObject(manifest, Formatting.Indented));
                MessageBox.Show("Done!");
            }
        }

        private void repackUsingCustomManifestToolStripMenuItem_Click(object sender, EventArgs e)
        {
            CommonOpenFileDialog dialog = new CommonOpenFileDialog();
            dialog.IsFolderPicker = true;
            OpenFileDialog jsonManifest = new OpenFileDialog();
            jsonManifest.Filter = "Manifest Json File|*.json";
            SaveFileDialog saveFileDialog = new SaveFileDialog();
            saveFileDialog.Filter = "utoc file|*.utoc";
            if (dialog.ShowDialog() == CommonFileDialogResult.Ok && jsonManifest.ShowDialog() == DialogResult.OK && saveFileDialog.ShowDialog() == DialogResult.OK)
            {
                Manifest manifest = Packer.JsonToManifest(dialog.FileName);

                int res = Packer.PackGameFiles(dialog.FileName, manifest, saveFileDialog.FileName, RepackMethodCMB.GetItemText(RepackMethodCMB.SelectedItem), AESKey.Text);
                if (res != 0)
                {
                    MessageBox.Show(res + " file(s) packed!");
                }
            }
        }

        private void HelpFilter_Click(object sender, EventArgs e)
        {
            MessageBox.Show("this will filter the files to extract using the W wildcards separated by comma or semicolon, example {}.mp3,{}.txt;{}myname{}\r\nuse {} instead of * to avoid issues on Windows");
        }
    }
}
