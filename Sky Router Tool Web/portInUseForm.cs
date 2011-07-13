using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;

namespace Sky_Router_Tool_Web
{
    public partial class PortInUseForm : Form
    {
        public int Port { get; set; }
        public PortInUseForm(int port)
        {
            Port = port;
            InitializeComponent();
        }

        private void portInUseForm_Load(object sender, EventArgs e)
        {
            txtPort.Text = Port.ToString();
            lblCaption.Text = string.Format("Unable to start web interface server. The port {0:G0} appears to be in use.\r\nPlease select a different port:",
                Port);
        }

        private void buttonOk_Click(object sender, EventArgs e)
        {
            int httpPort;
            if (int.TryParse(txtPort.Text, out httpPort) && httpPort > 0 && httpPort < 65535)
            {
                this.Port = httpPort;
                this.DialogResult = DialogResult.OK;
                this.Close();
            }
            else
            {
                MessageBox.Show("HTTP Port number must be a number from 1 to 65535.", "Input Error");
            }
        }

        private void buttonCancel_Click(object sender, EventArgs e)
        {
            this.DialogResult = DialogResult.Cancel;   
            this.Close();
        }
    }
}
