using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Drawing;  
using System.Windows.Forms;
using System.Text.RegularExpressions;
using System.IO;
using pHMb.pHHttp;
using pHMb.Router;

namespace Sky_Router_Tool_Web 
{
    public class SkyRouterTool : Form
    {
        #region Private Variables
        private Form _configurationForm;
        private NotifyIcon _systemTrayIcon;
        private HttpServer _httpServer;
        private RouterPoll _routerPoll;
        private RouterHttp _routerConnection;

        private StreamWriter _routerPollLog;
        private StreamWriter _httpServerLog;

        private bool _isErrorState = false;
        public bool _isFirstRun = true;
        private int _errCount = 0;
        #endregion

        #region Server Side Handler Callbacks
        private bool OnSettingChange(Dictionary<string, string> settings)
        {
            foreach (KeyValuePair<string, string> kvp in settings)
            {
                if ((string)Properties.Settings.Default[kvp.Key] != kvp.Value)
                {
                    // Setting is different, update it
                    Properties.Settings.Default[kvp.Key] = kvp.Value;

                    // Update live objects 
                    switch (kvp.Key)
                    {
                        case "RouterPassword":
                            _routerConnection.Password = kvp.Value;
                            break;

                        case "RouterUsername":
                            _routerConnection.Username = kvp.Value;
                            break;

                        case "RouterHostname":
                            _routerConnection.Host = kvp.Value;
                            break;

                        case "HttpPassword":
                            _httpServer.Password = kvp.Value;
                            break;

                        case "HttpUsername":
                            _httpServer.Username = kvp.Value;
                            break;

                        case "RouterPollInterval":
                            _routerPoll.PollingInterval = int.Parse(kvp.Value);
                            break;

                        case "RouterModel":
                            RestartServer();
                            break;
                    }
                }
            }

            Properties.Settings.Default.Save();
            return true;
        }

        private Dictionary<string, string> GetSettings(List<string> settings)
        {
            Dictionary<string, string> output = new Dictionary<string, string>();

            foreach (string setting in settings)
            {
                output.Add(setting, (string)Properties.Settings.Default[setting]);
            }

            return output;
        } 
        #endregion

        #region Event Handlers
        void _routerConnection_ConnectionSuccess(object sender, EventArgs e)
        {
            if (_isErrorState || _isFirstRun)
            {
                _isErrorState = false;
                _systemTrayIcon.Icon = Properties.Resources.icon;
                _isFirstRun = false;
            }
        }

        void _routerConnection_ConnectionError(object sender, RouterHttp.ConnectionErrorEventArgs e)
        {
            if (e.isIncorrectCredentials && (!_isErrorState || _errCount > 2))
            {
                _systemTrayIcon.Icon = Properties.Resources.icon_error;
                _systemTrayIcon.ShowBalloonTip(0, "Router Connection Error",
                    "An error has occured while connecting to your router. The username and password provided where incorrect.",
                    ToolTipIcon.Error);
                _errCount = 0;
            }
            else if (!_isErrorState)
            {
                _systemTrayIcon.Icon = Properties.Resources.icon_error;
                _systemTrayIcon.ShowBalloonTip(0, "Router Connection Error",
                    string.Format("An error has occured while connecting to your router. The error was: {0}", e.Exception),
                    ToolTipIcon.Error);
                _errCount = 0;
            }
            _isErrorState = true;
            _isFirstRun = false;

            _errCount++;
        } 

        void _httpServer_LogEvent(object sender, HttpServer.LogEventArgs e)
        {
            _httpServerLog.WriteLine(e.LogText);
            _httpServerLog.Flush();
        }

        void _routerPoll_LogEvent(object sender, RouterPoll.LogEventArgs e)
        {
            _routerPollLog.WriteLine(e.LogText);
            _routerPollLog.Flush();
        }

        protected override void OnLoad(EventArgs e)
        {
            this.Visible = false;
            this.ShowInTaskbar = false;

            // Set up system tray
            ContextMenu systemTrayMenu = new ContextMenu();
            systemTrayMenu.MenuItems.Add("Open Web Admin", OnMenuWebAdminClick).DefaultItem = true;
            systemTrayMenu.MenuItems.Add("Configuration", OnMenuConfigClick);
            systemTrayMenu.MenuItems.Add("-");
            systemTrayMenu.MenuItems.Add("Exit", OnMenuExitClick);

            _systemTrayIcon = new NotifyIcon();
            _systemTrayIcon.ContextMenu = systemTrayMenu;
            _systemTrayIcon.Text = "Sky Router Tool";
            _systemTrayIcon.Icon = Properties.Resources.icon_error;
            _systemTrayIcon.BalloonTipClicked += new EventHandler(OnMenuConfigClick);
            _systemTrayIcon.DoubleClick += new EventHandler(OnMenuWebAdminClick);

            _systemTrayIcon.Visible = true;

            RestartServer();

            base.OnLoad(e);
        }

        private void OnMenuWebAdminClick(object sender, EventArgs e)
        {
            // Open web admin
            System.Diagnostics.Process.Start(string.Format("http://localhost:{0:G}/", int.Parse(Properties.Settings.Default.HttpPort)));
        }

        private void OnMenuConfigClick(object sender, EventArgs e)
        {
            if (_configurationForm == null)
            {
                _configurationForm = new Configuration(this);
                _configurationForm.FormClosed += new FormClosedEventHandler(_configurationForm_FormClosed);
                _configurationForm.Show();
            }
            else
            {
                _configurationForm.BringToFront();
            }
        }

        void _configurationForm_FormClosed(object sender, FormClosedEventArgs e)
        {
            _configurationForm.Dispose();
            _configurationForm = null;
        }

        private void OnMenuExitClick(object sender, EventArgs e)
        {
            Exit();
        }       
        #endregion

        #region Public Properties
        public bool isErrorState
        {
            get
            {
                return _isErrorState;
            }
        }

        public RouterHttp RouterConnection
        {
            get
            {
                return _routerConnection;
            }
        }

        public HttpServer HttpServer
        {
            get
            {
                return _httpServer;
            }
        }
        #endregion

        #region Public Methods
        public SkyRouterTool()
        {

        }

        public void Exit()
        {
            _systemTrayIcon.Visible = false;

            if (_httpServer != null)
            {
                _httpServer.StopServer();
                _httpServerLog.Close();

                _httpServer = null;
                _httpServerLog = null;
            }

            if (_routerPoll != null)
            {
                _routerPoll.Stop();
                _routerPollLog.Close();

                _routerPoll = null;
                _routerPollLog = null;
            }

            Application.Exit();
        }

        public void RestartServer()
        {
            try
            {
                if (_httpServer != null)
                {
                    _httpServer.StopServer();
                    _httpServerLog.Close();

                    _httpServer = null;
                    _httpServerLog = null;
                }

                if (_routerPoll != null)
                {
                    _routerPoll.Stop();
                    _routerPollLog.Close();

                    _routerPoll = null;
                    _routerPollLog = null;
                }

                // Check program data directory exists
                string programDataPath = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData) + @"\sky router tool\";
                if (!Directory.Exists(programDataPath))
                {
                    Directory.CreateDirectory(programDataPath);
                }

                // Check databases exist
                if (!File.Exists(programDataPath + @"\routerlogs.sdf"))
                {
                    File.Copy("routerlogs.sdf", programDataPath + @"\routerlogs.sdf");
                }
                if (!File.Exists(programDataPath + @"\settings.sdf"))
                {
                    File.Copy("settings.sdf", programDataPath + @"\settings.sdf");
                }

                // Start the http server
                _httpServerLog = new StreamWriter(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData) + @"\sky router tool\http_server.log", true);
                _httpServer = new HttpServer();
                _httpServer.LogEvent += new EventHandler<HttpServer.LogEventArgs>(_httpServer_LogEvent);
                _httpServer.Port = int.Parse(Properties.Settings.Default.HttpPort);
#if DEBUG
                _httpServer.DocumentRoot = @"..\..\..\..\pH-Http\htdocs";
#else
                _httpServer.DocumentRoot = @"htdocs";
#endif
                _httpServer.Realm = "Sky Router Tool Web Interface";
                _httpServer.Username = Properties.Settings.Default.HttpUsername;
                _httpServer.Password = Properties.Settings.Default.HttpPassword;
                _httpServer.StartServer();

                // Create a router connection
                _routerConnection = new RouterHttp(Properties.Settings.Default.RouterUsername,
                    Properties.Settings.Default.RouterPassword,
                    Properties.Settings.Default.RouterHostname,
                    int.Parse(Properties.Settings.Default.HttpPort),
                    Properties.Settings.Default.HttpUsername,
                    Properties.Settings.Default.HttpPassword,
                    bool.Parse(Properties.Settings.Default.SkyCompatibilityMode));
                _routerConnection.ConnectionError += new EventHandler<RouterHttp.ConnectionErrorEventArgs>(_routerConnection_ConnectionError);
                _routerConnection.ConnectionSuccess += new EventHandler(_routerConnection_ConnectionSuccess);

                // Start the router polling loop
                _routerPollLog = new StreamWriter(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData) + @"\sky router tool\router_poll.log", true);
                _routerPoll = new RouterPoll(_routerConnection, Properties.Settings.Default.RouterModel);
                _routerPoll.PollingInterval = int.Parse(Properties.Settings.Default.RouterPollInterval) * 1000;
                _routerPoll.LogEvent += new EventHandler<RouterPoll.LogEventArgs>(_routerPoll_LogEvent);
                _routerPoll.Loggers.Add(new pHMb.Router.Loggers.BandwidthUsage());
                _routerPoll.Loggers.Add(new pHMb.Router.Loggers.Snrm());
                _routerPoll.Loggers.Add(new pHMb.Router.Loggers.Errors());
                _routerPoll.Start();

                // Add HTTP handler
                _httpServer.SSHandlers.Add(Regex.Escape(Path.GetFullPath(_httpServer.DocumentRoot + "\\interface.json")),
                                new pHMb.pHHttp.SSHandlers.SkyRouterTool(_routerPoll, _routerConnection, GetSettings, OnSettingChange,
                                    (pHMb.Router.Interfaces.IRouterInterface)Activator.CreateInstance(typeof(pHMb.Router.Interfaces.IRouterInterface).Assembly.GetType("pHMb.Router.RouterCommandSets." + Properties.Settings.Default.RouterModel), _routerConnection)));
            }
            catch (System.Net.Sockets.SocketException ex)
            {
                if (ex.SocketErrorCode == System.Net.Sockets.SocketError.AddressAlreadyInUse)
                {
                    // Port is in use
                    PortInUseForm portInUseDialog = new PortInUseForm(int.Parse(Properties.Settings.Default.HttpPort));
                    if (portInUseDialog.ShowDialog() == DialogResult.OK)
                    {
                        Properties.Settings.Default.HttpPort = portInUseDialog.Port.ToString();
                        Properties.Settings.Default.Save();
                        RestartServer();
                    }
                    else
                    {
                        Exit();
                    }
                }
                else
                {
                    switch (MessageBox.Show(string.Format("An error occured while creating the HTTP server: {0}", ex.ToString()),
                        "Sky Router Tool",
                        MessageBoxButtons.RetryCancel,
                        MessageBoxIcon.Error))
                    {
                        case DialogResult.Cancel:
                            Exit();
                            break;

                        case DialogResult.Retry:
                            RestartServer();
                            break;
                    }
                }
            }
            catch (Exception ex)
            {
                switch (MessageBox.Show(string.Format("An error occured while creating the HTTP server: {0}", ex.ToString()),
                        "Sky Router Tool",
                        MessageBoxButtons.RetryCancel,
                        MessageBoxIcon.Error))
                {
                    case DialogResult.Cancel:
                        Exit();
                        break;

                    case DialogResult.Retry:
                        RestartServer();
                        break;
                }
            }
        }
        #endregion
    }
}
