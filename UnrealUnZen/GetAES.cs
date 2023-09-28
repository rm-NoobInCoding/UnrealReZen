using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace UnrealUnZen
{
    public partial class GetAES : Form
    {
        public GetAES()
        {
            InitializeComponent();
        }
        public string AESKey { get; set; }

        private void button1_Click(object sender, EventArgs e)
        {
            this.AESKey = textBox1.Text;
            this.DialogResult = DialogResult.OK;
            this.Close();
        }

        private void button2_Click(object sender, EventArgs e)
        {
            this.DialogResult = DialogResult.Cancel;
        }
    }
}
