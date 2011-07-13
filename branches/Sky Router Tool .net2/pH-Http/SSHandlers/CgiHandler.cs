using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Diagnostics;
using System.Net;
using System.IO;

namespace pHMb.pHHttp.SSHandlers
{
    public class CgiHandler : ISSHandler
    {
        #region Public Properties
        public string Name
        {
            get
            {
                return "CGI Handler";
            }
        }

        public string Author
        {
            get
            {
                return "mrmt32";
            }
        }

        public string Description
        {
            get
            {
                return "Implements the common gateway interface";
            }
        }

        public string CgiPath { get; private set; }
        #endregion


        #region Public Methods
        public CgiHandler(string cgiPath)
        {
            CgiPath = cgiPath;
        }

        public void HandleRequest(HttpClient httpClient, string path)
        {
            HttpServer httpServer = httpClient.HttpServer;
            HttpRequest request = httpClient.Request;

            Uri fileUri = httpClient.Request.Uri;
            string filePath = httpClient.GetLocalPath(fileUri);

            ProcessStartInfo cgiProcessInfo;
            if (CgiPath != null)
            {
                cgiProcessInfo = new ProcessStartInfo(CgiPath);
            }
            else
            {
                cgiProcessInfo = new ProcessStartInfo(filePath);
            }

            IPHostEntry remoteHostEntry = Dns.GetHostEntry(((IPEndPoint)request.Socket.RemoteEndPoint).Address);

            // Add CGI environment
            cgiProcessInfo.EnvironmentVariables.Add("REDIRECT_STATUS", "200");
            cgiProcessInfo.EnvironmentVariables.Add("DOCUMENT_ROOT", Path.GetFullPath(httpServer.DocumentRoot));
            cgiProcessInfo.EnvironmentVariables.Add("SERVER_SOFTWARE", "pH-Http/0.1");
            cgiProcessInfo.EnvironmentVariables.Add("SERVER_NAME", httpServer.HostName);
            cgiProcessInfo.EnvironmentVariables.Add("GATEWAY_INTERFACE", "CGI/1.1");
            cgiProcessInfo.EnvironmentVariables.Add("SERVER_PROTOCOL", "HTTP/1.1");
            cgiProcessInfo.EnvironmentVariables.Add("SERVER_PORT", httpServer.Port.ToString());
            cgiProcessInfo.EnvironmentVariables.Add("REQUEST_METHOD", request.Method);
            cgiProcessInfo.EnvironmentVariables.Add("PATH_INFO", "test.php");
            //cgiProcessInfo.EnvironmentVariables.Add("PATH_TRANSLATED", filePath);
            cgiProcessInfo.EnvironmentVariables.Add("SCRIPT_NAME", fileUri.LocalPath);
            cgiProcessInfo.EnvironmentVariables.Add("SCRIPT_FILENAME", filePath);
            cgiProcessInfo.EnvironmentVariables.Add("QUERY_STRING", fileUri.Query.TrimStart('?'));
            cgiProcessInfo.EnvironmentVariables.Add("REMOTE_HOST", remoteHostEntry.HostName);
            cgiProcessInfo.EnvironmentVariables.Add("REMOTE_ADDR", ((IPEndPoint)request.Socket.RemoteEndPoint).Address.ToString());
            //cgiProcessInfo.EnvironmentVariables.Add("AUTH_TYPE", "");
            //cgiProcessInfo.EnvironmentVariables.Add("REMOTE_USER", "");
            //cgiProcessInfo.EnvironmentVariables.Add("REMOTE_IDENT", "");
            //cgiProcessInfo.EnvironmentVariables.Add("CONTENT_TYPE", "");
            //cgiProcessInfo.EnvironmentVariables.Add("CONTENT_LENGTH", "");
            
            foreach (KeyValuePair<string, string> kvp in request.Headers)
            {
                cgiProcessInfo.EnvironmentVariables.Add("HTTP_" + kvp.Key.Replace("-", "_"), kvp.Value);
            }

            cgiProcessInfo.CreateNoWindow = true;
            cgiProcessInfo.RedirectStandardInput = true;
            cgiProcessInfo.RedirectStandardOutput = true;
            cgiProcessInfo.RedirectStandardError = true;
            cgiProcessInfo.UseShellExecute = false;
            cgiProcessInfo.WorkingDirectory = httpServer.DocumentRoot;
            cgiProcessInfo.Arguments = "test.php";

            Process cgiProcess = Process.Start(cgiProcessInfo);
            StreamReader cgiOutput = cgiProcess.StandardOutput;

            if (request.PostData != null)
            {
                cgiProcess.StandardInput.Write(Encoding.ASCII.GetString(request.PostData));
            }
            //StreamReader cgiOutput = cgiProcess.StandardError;

            // Send response header
            StringBuilder responseString = new StringBuilder();

            responseString.Append("HTTP/1.1 " + HttpStatusCode.OK.GetStringValue() + "\r\n");

            Dictionary<string, string> headers = httpClient.GetStandardHeaders();
            headers.Add("Content-type", "text/html");
            foreach (KeyValuePair<string, string> kvp in headers)
            {
                responseString.Append(kvp.Key + ": " + kvp.Value + "\r\n");
            }

            request.Socket.Send(Encoding.ASCII.GetBytes(responseString.ToString()));

            // Send stdout to client
            int bytesRead = 0;
            byte[] readBuffer = new byte[1024];
            while (!cgiProcess.HasExited)
            {
                bytesRead = cgiOutput.BaseStream.Read(readBuffer, 0, 1024);

                if (bytesRead > 0)
                {
                    request.Socket.Send(readBuffer, bytesRead, System.Net.Sockets.SocketFlags.None);
                }
            }
        } 
        #endregion
        
    }
}
