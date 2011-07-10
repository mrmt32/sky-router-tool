using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using System.Text.RegularExpressions;
using System.IO;
using System.Net;
using pHMb.Router;
using pHMb.pHHttp;

namespace Sky_Router_Tool_Web
{
    public partial class Configuration : Form
    {
        public SkyRouterTool _mainClass;

        public Configuration(SkyRouterTool mainClass)
        {
            _mainClass = mainClass;
            InitializeComponent();
        }

        private void Configuration_Load(object sender, EventArgs e)
        {
            _mainClass.RouterConnection.ConnectionError += new EventHandler<RouterHttp.ConnectionErrorEventArgs>(RouterConnection_ConnectionError);
            _mainClass.RouterConnection.ConnectionSuccess += new EventHandler(RouterConnection_ConnectionSuccess);

            if (_mainClass.RouterConnection.Connected)
            {
                txtRouterConStatus.Text = "Connected";
            }
            else
            {
                txtRouterConStatus.Text = "Not Connected!";
            }

            // Fill settings text boxes
            txtHttpPort.Text = Properties.Settings.Default.HttpPort;
            txtHttpUsername.Text = Properties.Settings.Default.HttpUsername;
            txtHttpPassword.Text = Properties.Settings.Default.HttpPassword;

            txtRouterHostname.Text = Properties.Settings.Default.RouterHostname;
            txtRouterUsername.Text = Properties.Settings.Default.RouterUsername;
            txtRouterPassword.Text = Properties.Settings.Default.RouterPassword;
        }

        void RouterConnection_ConnectionSuccess(object sender, EventArgs e)
        {
            if (this.InvokeRequired)
            {
                this.Invoke(new EventHandler(RouterConnection_ConnectionSuccess));
            }
            else
            {
                txtRouterConStatus.Text = "Connected";
            }
        }

        void RouterConnection_ConnectionError(object sender, RouterHttp.ConnectionErrorEventArgs e)
        {
            if (this.InvokeRequired)
            {
                this.Invoke(new EventHandler<RouterHttp.ConnectionErrorEventArgs>(RouterConnection_ConnectionError), sender, e);
            }
            else
            {
                if (e.isIncorrectCredentials)
                {
                    txtRouterConStatus.Text = "Unable to connect, username or password incorrect.";
                }
                else
                {
                    txtRouterConStatus.Text = e.Exception.ToString();
                }
            }
        }

        private void buttonApply_Click(object sender, EventArgs e)
        {
            // Check input
            int httpPort;
            if (int.TryParse(txtHttpPort.Text, out httpPort) && httpPort > 0 && httpPort < 65535)
            {
                if (txtRouterHostname.Text != "" && txtRouterPassword.Text != "" && txtRouterUsername.Text != "")
                {
                    Properties.Settings.Default.HttpPort = txtHttpPort.Text;
                    Properties.Settings.Default.HttpUsername = txtHttpUsername.Text;
                    Properties.Settings.Default.HttpPassword = txtHttpPassword.Text;
                    Properties.Settings.Default.RouterHostname = txtRouterHostname.Text;
                    Properties.Settings.Default.RouterUsername = txtRouterUsername.Text;
                    Properties.Settings.Default.RouterPassword = txtRouterPassword.Text;

                    Properties.Settings.Default.Save();
                    _mainClass.RestartServer();
                    MessageBox.Show("Settings saved succesfully", "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                else
                {
                    MessageBox.Show("Router hostname, username and password must all be entered.", "Input Error");
                }
            }
            else
            {
                MessageBox.Show("HTTP Port number must be a number from 1 to 65535.", "Input Error");
            }
        }

        private void buttonClose_Click(object sender, EventArgs e)
        {
            this.Close();
        }

        private void button1_Click(object sender, EventArgs e)
        {
            SkyPasswordGen.GetInformation("00184D635952");
        }
    }
}
