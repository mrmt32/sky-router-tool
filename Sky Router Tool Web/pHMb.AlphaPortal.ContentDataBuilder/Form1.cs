using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using System.Diagnostics;

namespace pHMb.AlphaPortal.ContentDataBuilder
{
    public partial class Form1 : Form
    {
        private string _folderPath;

        public Form1()
        {
            InitializeComponent();
        }

        private void Form1_Load(object sender, EventArgs e)
        {

            FolderBrowserDialog openDialog = new FolderBrowserDialog();
            openDialog.SelectedPath = @"C:\Users\mrmt32\Documents\visual studio 2010\Projects\Sky Router Tool Web\Sources\pH-Http\htdocs\pages";
            if (openDialog.ShowDialog() == DialogResult.OK)
            {
                _folderPath = openDialog.SelectedPath;
                update();
            }
            else
            {
                this.Close();
            }
        }

        public void update()
        {
            Process cDataUpdateProcess = new Process();
            cDataUpdateProcess.StartInfo.RedirectStandardOutput = true;
            cDataUpdateProcess.StartInfo.CreateNoWindow = true;
            cDataUpdateProcess.StartInfo.FileName = "updatePages.exe";
            cDataUpdateProcess.StartInfo.Arguments = "\"" + _folderPath + "\" \"" + _folderPath + @"\.." + "\"";
            cDataUpdateProcess.StartInfo.UseShellExecute = false;
            cDataUpdateProcess.OutputDataReceived += new DataReceivedEventHandler(cDataUpdateProcess_OutputDataReceived);

            cDataUpdateProcess.Start();
            cDataUpdateProcess.BeginOutputReadLine();
        }

        void cDataUpdateProcess_OutputDataReceived(object sender, DataReceivedEventArgs e)
        {
            txtResult.Invoke(new textUpdateDelegate(textUpdate), new object[] { e.Data });
        }

        private delegate void textUpdateDelegate(string text);

        private void textUpdate(string text)
        {
            txtResult.Text += text + "\r\n";
            this.BringToFront();
        }

        private void buttonOk_Click(object sender, EventArgs e)
        {
            txtResult.Text = "";
            update();
        }
    }
}
