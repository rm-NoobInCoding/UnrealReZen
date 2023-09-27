using Microsoft.WindowsAPICodePack.Dialogs;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
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

        string tocadd = "";
        UTocData uToc = new UTocData();

        private void Form1_Load(object sender, EventArgs e)
        {
            comboBox1.SelectedIndex = 0;
            comboBox2.SelectedIndex = 0;
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
                tocadd = ofd.FileName;
                uToc = UTocDataParser.ParseUtocFile(tocadd, Helpers.HexStringToByteArray(AESKey.Text));
                List<string> pathes = new List<string>();

                for (int i = 0; i < uToc.Files.Count; i++)
                {
                    pathes.Add(uToc.Files[i].FilePath);
                }

                treeView1.Nodes.Clear();
                treeView1.Nodes.Add(MakeTreeFromPaths(pathes, Path.GetFileNameWithoutExtension(tocadd), '\\'));
                button1.Text = "Load TOC (Loaded " + Path.GetFileNameWithoutExtension(tocadd) + ")";
                button2.Enabled = true;
                button4.Enabled = true;

            }
        }
        void FixManifest(string jsonFilePath, string extpath)
        {

            // Read the JSON file
            string jsonText = File.ReadAllText(jsonFilePath);
            JObject jsonObject = JObject.Parse(jsonText);

            // Get the Files array
            JArray filesArray = jsonObject["Files"] as JArray;

            if (filesArray != null)
            {
                // Create a list to store the indices of items to remove
                List<int> indicesToRemove = new List<int>();

                for (int i = 0; i < filesArray.Count; i++)
                {
                    JObject fileObject = filesArray[i] as JObject;

                    if (fileObject != null)
                    {
                        string filePath = fileObject["Path"]?.ToString().Replace("/", "\\");
                        //MessageBox.Show(extpath+ fileObject["Path"]?.ToString().Replace("/", "\\"));
                        string chunkId = fileObject["ChunkId"]?.ToString();

                        if (!File.Exists(Path.Combine(extpath, filePath)) && filePath != "dependencies")
                        {
                            // File does not exist, mark it for removal
                            indicesToRemove.Add(i);

                            // Find the corresponding ChunkId in Dependencies and remove it
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

                // Remove the marked items from Files array in reverse order to avoid index issues
                indicesToRemove.Reverse();
                foreach (int indexToRemove in indicesToRemove)
                {
                    filesArray.RemoveAt(indexToRemove);
                }

                // Serialize the updated JSON back to a string
                string updatedJsonText = jsonObject.ToString();

                // Write the updated JSON back to the file
                File.WriteAllText(jsonFilePath + "_Fix.json", updatedJsonText);

                Console.WriteLine("JSON file updated successfully.");
            }
            else
            {
                Console.WriteLine("JSON file structure is not as expected.");
            }
        }
        private void button2_Click(object sender, EventArgs e)
        {
            Directory.CreateDirectory(tocadd + "_Export");
            int exportcount = uToc.UnpackUcasFiles(Path.ChangeExtension(tocadd, ".ucas"), tocadd + "_Export", "");
            MessageBox.Show(exportcount + " file(s) extracted!");
            //int res = unpackAllGameFiles(tocadd, Path.ChangeExtension(tocadd, ".ucas"), tocadd + "_Export\\", AESKey.Text);
            //if (res != -1)
            //{
            //    MessageBox.Show(res + " files extracted!");
            //}
            //else
            //{
            //    IntPtr errorPtr = getError();
            //    string errorMessage = Marshal.PtrToStringAnsi(errorPtr);
            //    MessageBox.Show(res + " - " + errorMessage);
            //}
        }

        private void button4_Click(object sender, EventArgs e)
        {
            SaveFileDialog saveFile = new SaveFileDialog();
            saveFile.Filter = "Manifest Json File|*.json";
            saveFile.FileName = Path.GetFileNameWithoutExtension(tocadd);
            if (saveFile.ShowDialog() == DialogResult.OK)
            {
                Manifest manifest = uToc.ConstructManifest(Path.ChangeExtension(tocadd, ".ucas"));
                File.WriteAllText(saveFile.FileName, JsonConvert.SerializeObject(manifest, Formatting.Indented));
                MessageBox.Show("Done!");
            }
        }

        private void button5_Click(object sender, EventArgs e)
        {
            OpenFileDialog openFileDialog = new OpenFileDialog();
            openFileDialog.Filter = "jso|*.json";
            CommonOpenFileDialog dialog = new CommonOpenFileDialog();
            dialog.IsFolderPicker = true;
            if (dialog.ShowDialog() == CommonFileDialogResult.Ok && openFileDialog.ShowDialog() == DialogResult.OK)
            {
                FixManifest(openFileDialog.FileName, dialog.FileName);
                MessageBox.Show("Done!");
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
                int res = Packer.PackGameFiles(dialog.FileName, openFileDialog.FileName, saveFileDialog.FileName, comboBox1.GetItemText(comboBox1.SelectedItem), AESKey.Text);
                //    int res = packGameFiles(dialog.FileName + "\\", openFileDialog.FileName, Path.GetDirectoryName(saveFileDialog.FileName) + "\\" + Path.GetFileNameWithoutExtension(saveFileDialog.FileName) + "_P", "Zlib", "");
                if (res != -1)
                {
                    MessageBox.Show(res + " file(s) packed!");
                }
                //    else
                //    {
                //        IntPtr errorPtr = getError();
                //        string errorMessage = Marshal.PtrToStringAnsi(errorPtr);
                //        MessageBox.Show(res + " : " + errorMessage);
                //    }
            }
        }

        private void textBox1_TextChanged(object sender, EventArgs e)
        {
            Constants.MountPoint = textBox1.Text;
        }
    }
}
