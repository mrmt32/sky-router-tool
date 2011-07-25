using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Reflection;

namespace pHMb.pHHttp
{
    public class HttpServer
    {
        #region Private Variables
        private TcpListener _tcpListener;
        #endregion

        #region Private Methods
        private void ClientConnect(IAsyncResult result)
        {
            try
            {
                Socket workerSocket = _tcpListener.EndAcceptSocket(result);

                // Create client handler for the connection
                HttpClient httpClient = new HttpClient(this, workerSocket);

                httpClient.RequestComplete += new EventHandler<HttpClient.RequestCompleteEventArgs>(httpClient_RequestComplete);

                HttpClients.Add(httpClient);
                OnClientConnectionChange();
            }
            catch (ObjectDisposedException ex)
            {
                // HTTP server has been stopped
                return;
            }
            catch (Exception ex)
            {
                OnLogEvent(string.Format("Unhandled exception while accepting connection: {0}\r\n", ex.ToString()));
            }


            _tcpListener.BeginAcceptSocket(new AsyncCallback(ClientConnect), new object());
        }

        private void httpClient_RequestComplete(object sender, HttpClient.RequestCompleteEventArgs e)
        {
            HttpClient httpClient = (HttpClient)sender;
            HttpClients.Remove(httpClient);

            if (!e.isSuccess)
            {
                if (e.StatusCode == HttpStatusCode.None)
                {
                    // An unhandled exception occured
                    OnLogEvent(string.Format("Unhandled exception while handleing request: {0}\r\n", e.Exception.ToString()));
                }
                else
                {
                    // And http error occured
                    if (httpClient.Request != null)
                    {
                        OnLogEvent(string.Format("'{0}' Error occured while accessing '{1}'\r\n", e.StatusCode.GetStringValue(), httpClient.Request.Path));
                    }
                    else
                    {
                        OnLogEvent(string.Format("'{0}' Error occured\r\n", e.StatusCode.GetStringValue()));
                    }
                }
            }
        }
        #endregion

        #region Public Properties
        public List<HttpClient> HttpClients { get; private set; }

        public int MaxConnections { get; set; }
        public int Port { get; set; }
        public int BufferSize { get; set; }
        public int RequestTimeout { get; set; }

        public string DocumentRoot { get; set; }
        public string HostName { get; set; }

        public string Username { get; set; }
        public string Password { get; set; }
        public string Realm { get; set; }

        public Dictionary<String, ISSHandler> SSHandlers { get; set; }
        public Dictionary<string, string> MimeTypes { get; set; }
        public List<string> IndexPages { get; set; }

        public ISSHandler DefaultSSHandler
        {
            get
            {
                return SSHandlers[".*"];
            }

            set
            {
                SSHandlers[".*"] = value;
            }
        }
        #endregion

        #region Public Events & Event Methods
        public class LogEventArgs : EventArgs
        {
            public string LogText;
        }
        public event EventHandler<LogEventArgs> LogEvent;
        public void OnLogEvent(string text)
        {
            if (LogEvent != null)
            {
                LogEvent.Invoke(this, new LogEventArgs() { LogText = text });
            }
        }

        public event EventHandler ClientConnectionChange;
        private void OnClientConnectionChange()
        {
            if (ClientConnectionChange != null)
            {
                ClientConnectionChange.Invoke(this, EventArgs.Empty);
            }
        }
        #endregion

        #region Public Methods
        public HttpServer()
        {
            MaxConnections = 10;
            Port = 80;
            BufferSize = 1024;
            RequestTimeout = 60;

            DocumentRoot = "htdocs\\";
            HostName = "localhost";

            MimeTypes = new Dictionary<string, string>();
            SSHandlers = new Dictionary<string, ISSHandler>();

            HttpClients = new List<HttpClient>();

            DefaultSSHandler = new SSHandlers.DefaultHandler();

            // Get mime types
            string mimeTypes = File.ReadAllText("mime_types.conf");
            Match regexMatch = Regex.Match(mimeTypes, "(^[^# \t]*?)[ \t]+(.*)[\r\n]+", RegexOptions.Multiline);

            while (regexMatch.Success)
            {
                MimeTypes.Add(regexMatch.Groups[1].Value, regexMatch.Groups[2].Value);
                regexMatch = regexMatch.NextMatch();
            }

            // Get index page names
            IndexPages = new List<string>(File.ReadAllLines("index_pages.conf"));
        }

        public void StartServer()
        {
            _tcpListener = new TcpListener(IPAddress.Any, Port);
            _tcpListener.Start();
            _tcpListener.BeginAcceptSocket(new AsyncCallback(ClientConnect), new object());
        }

        public void StopServer()
        {
            _tcpListener.Stop();
        }
        #endregion
    }

   
    public class HttpRequest
    {
        public Socket Socket;
        public string Method;
        public string Path;
        public Uri Uri;
        public string Version;
        public byte[] PostData;
        public Dictionary<string, string> Headers;
    }

    public class HttpResponse
    {
        public byte[] Body;
        public HttpStatusCode StatusCode;
        public Dictionary<string, string> Headers;
    }

    public enum HttpStatusCode
    {
        [StringValue("100 Continue")]
        Continue = 100,

        [StringValue("101 Switching Protocols")]
        Switching_Protocols = 101,

        [StringValue("200 OK")]
        OK = 200,

        [StringValue("201 Created")]
        Created = 201,

        [StringValue("202 Accepted")]
        Accepted = 202,

        [StringValue("204 No Content")]
        No_Content = 204,

        [StringValue("205 Reset Content")]
        Reset_Content = 205,

        [StringValue("206 Partial Content")]
        Partial_Content = 206,

        [StringValue("207 Multi Status")]
        Multi_Status = 207,

        [StringValue("300 Multiple Choices")]
        Multiple_Choices = 300,

        [StringValue("301 Moved Permanently")]
        Moved_Permanently = 301,

        [StringValue("302 Found")]
        Found = 302,

        [StringValue("303 See Other")]
        See_Other = 303,

        [StringValue("304 Not Modified")]
        Not_Modified = 304,

        [StringValue("305 Use Proxy")]
        Use_Proxy = 305,

        [StringValue("306 Switch Proxy")]
        Switch_Proxy = 306,

        [StringValue("307 Temporary Redirect")]
        Temporary_Redirect = 307,

        [StringValue("400 Bad Request")]
        Bad_Request = 400,

        [StringValue("401 Unauthorized")]
        Unauthorized = 401,

        [StringValue("402 Payment Required")]
        Payment_Required = 402,

        [StringValue("403 Forbidden")]
        Forbidden = 403,

        [StringValue("404 Not Found")]
        Not_Found = 404,

        [StringValue("405 Method Not Allowed")]
        Method_Not_Allowed = 405,

        [StringValue("406 Not Acceptable")]
        Not_Acceptable = 406,

        [StringValue("408 Request Timeout")]
        Request_Timeout = 408,

        [StringValue("409 Conflict")]
        Conflict = 409,

        [StringValue("410 Gone")]
        Gone = 410,

        [StringValue("411 Length Required")]
        Length_Required = 411,

        [StringValue("412 Precondition Failed")]
        Precondition_Failed = 412,

        [StringValue("413 Request Entity Too Large")]
        Request_Entity_Too_Large = 413,

        [StringValue("414 Request-URI Too Long")]
        Request_URI_Tool_Long = 414,

        [StringValue("415 Unsupported_Media_Type")]
        Unsupported_Media_Type = 415,

        [StringValue("416 Request Range Not Satisfiable")]
        Requested_Range_Not_Satisfiable = 416,

        [StringValue("417 Expectation Failed")]
        Expectation_Failed = 417,

        [StringValue("418 I'm a teapot")]
        Im_a_teapot = 418,

        [StringValue("500 Internal Server Error")]
        Internal_Server_Error = 500,

        [StringValue("501 Not Implemented")]
        Not_Implemented = 501,

        [StringValue("502 Bad Gateway")]
        Bad_Gateway = 502,

        [StringValue("503 Service Unavailable")]
        Service_Unavailable = 503,

        [StringValue("504 Gateway Timeout")]
        Gateway_Timeout = 504,

        [StringValue("505 HTTP Version Not Supported")]
        HTTP_Version_Not_Supported = 505,

        [StringValue("0 None")]
        None = 0
    } 
}
