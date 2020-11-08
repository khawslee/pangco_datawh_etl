using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace pangco_datawh_etl
{
    public partial class Form2 : Form
    {
        public Form2()
        {
            InitializeComponent();
        }

        private void Form2_Load(object sender, EventArgs e)
        {
            if (!File.Exists(@"config.json"))
            {
                MessageBox.Show("Error! File not found.");
            }
            else
            {
                string readText = File.ReadAllText(@"config.json");
                Debug.WriteLine(readText);
                string dep = Encrypt.DecryptString(readText);
                Debug.WriteLine(dep);
                richTextBox1.Text = dep;
            }
        }

        private void button1_Click(object sender, EventArgs e)
        {
            string enp = Encrypt.EncryptString(richTextBox1.Text);
            File.WriteAllText(@"config.json", enp);
            Close();
        }
    }
}
