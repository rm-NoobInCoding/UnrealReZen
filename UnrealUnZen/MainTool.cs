using Microsoft.WindowsAPICodePack.Dialogs;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Threading;
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

        string TocFilePath = "";
        string AESKey = "";
        UTocData uToc = new UTocData();

        private void Form1_Load(object sender, EventArgs e)
        {
            comboBox1.SelectedIndex = 1;
            UCasDataParser.FileUnpacked += UCas_FileUnpacked;

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

        private void button1_Click(object sender, EventArgs e)
        {
            OpenFileDialog ofd = new OpenFileDialog();
            ofd.Filter = "UToc files(*.utoc)|*.utoc";
            if (ofd.ShowDialog() == DialogResult.OK)
            {
                button1.Text = "Loading toc file...";
                TocFilePath = ofd.FileName;
                LoadToc();


            }
        }

        public void LoadToc()
        {
            try
            {
                uToc = UTocDataParser.ParseUtocFile(TocFilePath, Helpers.HexStringToByteArray(AESKey));

            }
            catch (Exception ex)
            {
                if (ex.Message == "Encrypted file")
                {
                    GetAES a = new GetAES();
                    if (a.ShowDialog() == DialogResult.OK)
                    {
                        AESKey = a.AESKey;
                        LoadToc();
                    }
                    else
                    {
                        MessageBox.Show("UnrealUnZen can't read this toc without aes key.");

                    }

                }
                else
                {
                    MessageBox.Show("An error happened when loading toc file:\n" + ex.ToString());
                }
            }
            if (uToc.IsFullyRead)
            {
                List<string> pathes = new List<string>();

                for (int i = 0; i < uToc.Files.Count; i++)
                {
                    pathes.Add(uToc.Files[i].FilePath);
                }

                this.Invoke(new MethodInvoker(delegate
                {
                    treeView1.Nodes.Clear();
                    treeView1.Nodes.Add(MakeTreeFromPaths(pathes, Path.GetFileNameWithoutExtension(TocFilePath), '\\'));
                    button1.Text = "Load TOC (Loaded " + Path.GetFileNameWithoutExtension(TocFilePath) + ")";
                    button2.Enabled = true;
                    button4.Enabled = true;
                }));
            }


        }

        void FixManifest(string jsonFilePath, string extpath)
        {
            string jsonText = File.ReadAllText(jsonFilePath);
            JObject jsonObject = JObject.Parse(jsonText);
            JArray filesArray = jsonObject["Files"] as JArray;

            if (filesArray != null)
            {
                List<int> indicesToRemove = new List<int>();

                for (int i = 0; i < filesArray.Count; i++)
                {
                    JObject fileObject = filesArray[i] as JObject;

                    if (fileObject != null)
                    {
                        string filePath = fileObject["Path"]?.ToString().Replace("/", "\\");
                        string chunkId = fileObject["ChunkId"]?.ToString();

                        if (!File.Exists(extpath + filePath) && filePath != "dependencies")
                        {
                            indicesToRemove.Add(i);
                            if (chunkId != null && chunkId.Length >= 16)
                            {
                                string chunkIdPrefix = ulong.Parse(chunkId.Substring(0, 16), System.Globalization.NumberStyles.HexNumber).ToString();
                                JObject dependenciesObject = jsonObject["Dependencies"] as JObject;
                                JObject chunkIdToDependenciesObject = dependenciesObject?["ChunkIDToDependencies"] as JObject;

                                if (chunkIdToDependenciesObject != null && chunkIdToDependenciesObject.ContainsKey(chunkIdPrefix))
                                {
                                    chunkIdToDependenciesObject.Remove(chunkIdPrefix);
                                }
                            }
                        }
                    }
                }
                indicesToRemove.Reverse();
                foreach (int indexToRemove in indicesToRemove)
                {
                    filesArray.RemoveAt(indexToRemove);
                }
                string updatedJsonText = jsonObject.ToString();
                File.WriteAllText(jsonFilePath + "_Fix.json", updatedJsonText);

                MessageBox.Show("Manifest file updated successfully.");
            }
            else
            {
                Console.WriteLine("Manifest file structure is not as expected.");
            }
        }
        private void button2_Click(object sender, EventArgs e)
        {
            Directory.CreateDirectory(TocFilePath + "_Export");
            Thread t = new Thread(() =>
            {
                int exportcount = uToc.UnpackUcasFiles(Path.ChangeExtension(TocFilePath, ".ucas"), TocFilePath + "_Export", AESKey);
                this.Invoke(new MethodInvoker(delegate
                {
                    toolStripProgressBar1.Value = 0;
                    toolStripStatusLabel1.Text = "";
                }));
                MessageBox.Show(exportcount + " file(s) extracted!");
            });
            t.Start();

        }

        private void UCas_FileUnpacked(object sender, UCasDataParserEventArgs e)
        {
            this.Invoke(new MethodInvoker(delegate
            {
                toolStripProgressBar1.Maximum = e.Len;
                toolStripProgressBar1.Value += 1;
                toolStripStatusLabel1.Text = Path.GetFileName(e.UnpackedFile);
            }));
        }

        private void button4_Click(object sender, EventArgs e)
        {
            SaveFileDialog saveFile = new SaveFileDialog();
            saveFile.Filter = "Manifest Json File|*.json";
            saveFile.FileName = Path.GetFileNameWithoutExtension(TocFilePath);
            if (saveFile.ShowDialog() == DialogResult.OK)
            {
                Manifest manifest = uToc.ConstructManifest(Path.ChangeExtension(TocFilePath, ".ucas"));
                File.WriteAllText(saveFile.FileName, JsonConvert.SerializeObject(manifest, Formatting.Indented));
                MessageBox.Show("Done!");
            }
        }

        private void button5_Click(object sender, EventArgs e)
        {
            OpenFileDialog openFileDialog = new OpenFileDialog();
            openFileDialog.Filter = "Manifest Json File|*.json";
            CommonOpenFileDialog dialog = new CommonOpenFileDialog();
            dialog.IsFolderPicker = true;
            if (dialog.ShowDialog() == CommonFileDialogResult.Ok && openFileDialog.ShowDialog() == DialogResult.OK)
            {
                FixManifest(openFileDialog.FileName, dialog.FileName);

            }
        }

        private void button6_Click(object sender, EventArgs e)
        {
            CommonOpenFileDialog dialog = new CommonOpenFileDialog();
            dialog.IsFolderPicker = true;
            OpenFileDialog openFileDialog = new OpenFileDialog();
            openFileDialog.Filter = "Manifest file|*.json";
            SaveFileDialog saveFileDialog = new SaveFileDialog();
            saveFileDialog.Filter = "utoc file|*.utoc";
            if (dialog.ShowDialog() == CommonFileDialogResult.Ok && openFileDialog.ShowDialog() == DialogResult.OK && saveFileDialog.ShowDialog() == DialogResult.OK)
            {
                int res = Packer.PackGameFiles(dialog.FileName, openFileDialog.FileName, saveFileDialog.FileName, comboBox1.GetItemText(comboBox1.SelectedItem), AESKey);
                if (res != 0)
                {
                    MessageBox.Show(res + " file(s) packed!");
                }
            }
        }
    }
}
