using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;
using System.Reflection;
using System.Diagnostics;
using System.Threading;

namespace Sky_Router_Tool_Web
{
    static class Program
    {
        private static Mutex _mutex;

        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main()
        {
            try
            {
                // Check for other running instances
                _mutex = new Mutex(true, "SkyRouterTool");

#if !DEBUG
                if (!_mutex.WaitOne(0, false))
                {
                    // There is another instance, just show the web interface
                    System.Diagnostics.Process.Start(string.Format("http://localhost:{0:G}/", int.Parse(Properties.Settings.Default.HttpPort)));
                }
                else
#endif
                {
                    // Change data direcory to folder in programdata
                    AppDomain.CurrentDomain.SetData("DataDirectory",
                      System.Environment.GetFolderPath(System.Environment.SpecialFolder.CommonApplicationData) + @"\sky router tool");

                    // Start main thread
                    Application.EnableVisualStyles();
                    Application.SetCompatibleTextRenderingDefault(false);
                    Application.Run(new SkyRouterTool());
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("An unhandled exception occured: " + ex.ToString(), "Sky Router Tool", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            
        }
    }

}
