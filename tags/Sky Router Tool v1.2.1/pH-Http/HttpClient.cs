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
using System.Web;

namespace pHMb.pHHttp
{
    public class HttpClient
    {
        #region Private Variables
        private HttpServer _httpServer;
        private Socket _clientSocket;
        private HttpRequest _httpRequest;
        private Dictionary<string, string> _responseHeaders;

        private byte[] _dataBuffer = new byte[50];
        private StringBuilder _requestHeaderBuilder = new StringBuilder();
        private StringBuilder _requestContentBuilder = new StringBuilder();
        private AsyncCallback _dataCallback;

        private bool _responseStringSent = false;
        private bool _bodyStarted = false;
        #endregion

        #region Private Methods
        /// <summary>
        /// Calls OnDataRecieved() on new data being added to the buffer
        /// </summary>
        private void WaitForData()
        {
            _clientSocket.BeginReceive(_dataBuffer, 0, _dataBuffer.Length, SocketFlags.None, _dataCallback, null);
        }

        /// <summary>
        /// Processes incoming data
        /// </summary>
        /// <param name="result">IAsyncResult object</param>
        private void OnDataReceived(IAsyncResult result)
        {
            try
            {
                int receivedBytes = _clientSocket.EndReceive(result);

                if (_clientSocket.Connected && receivedBytes > 0)
                {
                    if (_httpRequest == null)
                    {
                        _requestHeaderBuilder.Append(Encoding.ASCII.GetString(_dataBuffer, 0, receivedBytes));

                        // We havn't parsed the header yet, we must still be waiting for it
                        if (_requestHeaderBuilder.ToString().Contains("\r\n\r\n"))
                        {
                            // We now have all of the header (and it hasn't been parse yet), it can now be parsed
                            ParseRequestHeader();

                            // Find out which method is being used
                            switch (_httpRequest.Method)
                            {
                                case "POST":
                                    // We need to get the extra post content
                                    GetPostData();
                                    break;

                                case "GET":
                                case "HEAD":
                                    // We can just go straight to handleing the request, there should be no extra content
                                    HandleRequest();
                                    break;

                                default:
                                    // We don't support any other methods
                                    SendError(HttpStatusCode.Not_Implemented);
                                    break;
                            }
                        }
                        else
                        {
                            // We still need some more data; lets get it:
                            WaitForData();
                        }
                    }
                    else
                    {
                        _requestContentBuilder.Append(Encoding.ASCII.GetString(_dataBuffer, 0, receivedBytes));

                        // We have the header so we must be waiting for post data
                        GetPostData();
                    }
                }
                else
                {
                    // The client disconnected!
                    _clientSocket.Close();
                    OnRequestComplete(false, HttpStatusCode.None, new ApplicationException("Client closed connection before request could be completed."));
                    return;
                }
            }
            catch (HttpException ex)
            {
                if (_clientSocket != null)
                {
                    _clientSocket.Close();
                }
                OnRequestComplete(false, ex.StatusCode, ex);
            }
            catch (Exception ex)
            {
                // We don't want to be here!
                if (_clientSocket != null)
                {
                    _clientSocket.Close();
                }
                OnRequestComplete(false, HttpStatusCode.None, ex);
            }
            OnRequestComplete(true, HttpStatusCode.OK, null);
        }

        /// <summary>
        /// Parses the request header sent by the client
        /// </summary>
        private void ParseRequestHeader()
        {
            string requestHeader = _requestHeaderBuilder.ToString();

            // Check we only have a header in requestHeader, if not remove the content and put it in _requestContent
            if (!requestHeader.EndsWith("\r\n\r\n"))
            {
                string[] requestSplit = requestHeader.Split(new string[] { "\r\n\r\n" }, 2, StringSplitOptions.None);
                _requestContentBuilder.Append(requestSplit[1]);
                requestHeader = requestSplit[0] + "\r\n\r\n";
            }

            // Get request headers
            _httpRequest = new HttpRequest();
            _httpRequest.Socket = _clientSocket;
            _httpRequest.Headers = new Dictionary<string, string>();

            // Find the request line
            Match regexMatch = Regex.Match(requestHeader, "\\A([A-z]+) ?([^ \r]*) ?([^ \r]*)\r\n");

            if (!regexMatch.Success)
            {
                SendError(HttpStatusCode.Bad_Request);
            }
            else
            {
                _httpRequest.Method = regexMatch.Groups[1].Value;
                _httpRequest.Path = regexMatch.Groups[2].Value;
                _httpRequest.Version = regexMatch.Groups[3].Value;
            }


            // Find request headers
            regexMatch = Regex.Match(requestHeader, "(.*?): (.*)\r\n");

            while (regexMatch.Success)
            {
                _httpRequest.Headers.Add(regexMatch.Groups[1].Value, regexMatch.Groups[2].Value);
                regexMatch = regexMatch.NextMatch();
            }

            _httpRequest.Uri = GetUri(_httpRequest.Path);


            // Log the access
            _httpServer.OnLogEvent(string.Format("{0} request - {1} from {2} \r\n",
                _httpRequest.Method, _httpRequest.Uri.AbsoluteUri, ((IPEndPoint)_clientSocket.RemoteEndPoint).Address));
        }

        /// <summary>
        /// Gets the post data sent by the client
        /// </summary>
        private void GetPostData()
        {
            string contentLengthStr = "0";
            int contentLength = 0;

            if (_httpRequest.Headers.TryGetValue("Content-Length", out contentLengthStr))
            {
                if (int.TryParse(contentLengthStr, out contentLength))
                {
                    // Content length is valid, see if we have all of the content:
                    if (_requestContentBuilder.Length >= contentLength)
                    {
                        // We have all the content!
                        _httpRequest.PostData = Encoding.ASCII.GetBytes(_requestContentBuilder.ToString());
                        HandleRequest();
                    }
                    else
                    {
                        // We still need to get some more content

                        // Resize the buffer to a more optimum size (the amount of data left)
                        _dataBuffer = new byte[contentLength - _requestContentBuilder.Length];

                        WaitForData();
                    }
                }
                else
                {
                    // Invalid header!
                    SendError(HttpStatusCode.Bad_Request);
                }
            }
            else
            {
                // Invalid header!
                SendError(HttpStatusCode.Bad_Request);
            }
        }

        /// <summary>
        /// Handles an incoming request and responds with an appropriate response
        /// </summary>
        private void HandleRequest()
        {
            // Check basic authentication
            bool authorized = false;
            if (_httpServer.Username != null && _httpServer.Username != "")
            {
                string usernamePasswordEncoded;
                if (_httpRequest.Headers.TryGetValue("Authorization", out usernamePasswordEncoded))
                {
                    string[] usernamePassword = Encoding.ASCII.GetString(Convert.FromBase64String(usernamePasswordEncoded.Replace("Basic ", ""))).Split(':');
                    if (usernamePassword.Length == 2 && usernamePassword[0] == _httpServer.Username && usernamePassword[1] == _httpServer.Password)
                    {
                        authorized = true;
                    }
                    else
                    {
                        SendAuthorizationRequired();
                    }
                }
                else
                {
                    SendAuthorizationRequired();
                }
            }
            else
            {
                authorized = true;
            }

            if (authorized)
            {

                // Get path of file
                string path = GetLocalPath(_httpRequest);

                // Find out if a directory is being referenced instead of a file and sort out index pages
                if (!File.Exists(path) && Directory.Exists(path))
                {
                    // Directory requested, see if an index page exists
                    foreach (string indexPage in _httpServer.IndexPages)
                    {
                        String documentPath = Path.Combine(path, indexPage);
                        if (File.Exists(documentPath))
                        {
                            path = documentPath;
                            break;
                        }
                    }
                }

                // Find a handler to use for this request
                KeyValuePair<string, ISSHandler> ssHandler = _httpServer.SSHandlers.Last<KeyValuePair<string, ISSHandler>>(x => Regex.Match(path, x.Key).Success);

                try
                {
                    // Call handler
                    ssHandler.Value.HandleRequest(this, path);
                }
                catch (HttpException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    SendError(HttpStatusCode.Internal_Server_Error, "ERROR 500. Error running handler: " + ex.ToString());
                }
            }

            // All done, close the socket
            _clientSocket.Close(10);
        }
        #endregion
        
        #region Public Properties
        /// <summary>
        /// The HttpRequest object describing the request
        /// </summary>
        public HttpRequest Request
        {
            get
            {
                return _httpRequest;
            }
        }

        /// <summary>
        /// The parent HttpServer object
        /// </summary>
        public HttpServer HttpServer
        {
            get
            {
                return _httpServer;
            }
        }

        public Socket ClientSocket
        {
            get
            {
                return _clientSocket;
            }
        }
        #endregion

        #region Public Events
        public class RequestCompleteEventArgs : EventArgs
        {
            public bool isSuccess;
            public HttpStatusCode StatusCode;
            public Exception Exception;
        }
        public event EventHandler<RequestCompleteEventArgs> RequestComplete;
        private void OnRequestComplete(bool isSucess, HttpStatusCode statusCode, Exception exception)
        {
            if (RequestComplete != null)
            {
                RequestComplete.Invoke(this, new RequestCompleteEventArgs() { Exception = exception, isSuccess = isSucess, StatusCode = statusCode });
            }
        } 
        #endregion

        #region Public Methods
        /// <summary>
        /// Creates a new instance of the HttpClient object
        /// </summary>
        /// <param name="httpServer">A reference to the parent HttpServer object</param>
        /// <param name="clientSocket">A reference to the client socket to use</param>
        public HttpClient(HttpServer httpServer, Socket clientSocket)
        {
            _httpServer = httpServer;
            _clientSocket = clientSocket;
            _responseHeaders = GetStandardHeaders();

            _dataCallback = new AsyncCallback(OnDataReceived);

            _clientSocket.ReceiveTimeout = _httpServer.RequestTimeout * 1000;

            WaitForData();
        }

        /// <summary>
        /// Gets a Uri object from a path supplied in a GET request
        /// </summary>
        /// <param name="httpPath">The path supplied in a GET request</param>
        /// <returns>A Uri object containing the URL of the page requested (e.g. http://example.com/index.html)</returns>
        public Uri GetUri(string httpPath)
        {
            return new Uri(new UriBuilder("http", ((IPEndPoint)_clientSocket.LocalEndPoint).Address.ToString()).Uri, new Uri(httpPath.TrimStart('/', '\\'), UriKind.Relative));
        }

        /// <summary>
        /// Get a a local path from an HttpRequest object
        /// </summary>
        /// <param name="httpRequest">An HttpRequest object describing the request</param>
        /// <returns>The local path to the file requested</returns>
        /// <remarks>This is safe, url attacks such as using "../" are protected against</remarks>
        public String GetLocalPath(HttpRequest httpRequest)
        {
            return GetLocalPath(httpRequest.Uri);
        }

        /// <summary>
        /// Get a local path from a Uri object
        /// </summary>
        /// <param name="requestUri">The uri object describing a url</param>
        /// <returns>The local path to the file requested</returns>
        /// <remarks>This is safe, url attacks such as using "../" are protected against</remarks>
        public String GetLocalPath(Uri requestUri)
        {
            string documentRoot = Path.GetFullPath(_httpServer.DocumentRoot);
            return Path.Combine(documentRoot, requestUri.LocalPath.TrimStart('/'));
        }

        /// <summary>
        /// Gets the standard headers, these should always be sent for all responses
        /// </summary>
        /// <returns>Standard headers</returns>
        public Dictionary<string, string> GetStandardHeaders()
        {
            Dictionary<string, string> responseHeaders = new Dictionary<string, string>();

            responseHeaders.Add("Date", DateTime.Now.ToString("r"));
            responseHeaders.Add("Server", "pH-Mb Sky Router Tools - mrmt32");
            responseHeaders.Add("Connection", "close");

            return responseHeaders;
        }

        /// <summary>
        /// Sends the status string to the client (first line of response)
        /// </summary>
        /// <param name="statusCode">HttpStatusCode describing the status</param>
        /// <remarks>This can only be used once per response</remarks>
        public void SendResponseStatus(HttpStatusCode statusCode)
        {
            if (!_responseStringSent)
            {
                _clientSocket.Send(Encoding.ASCII.GetBytes("HTTP/1.1 " + statusCode.GetStringValue() + "\r\n"));
                _responseStringSent = true;
            }
            else
            {
                throw new InvalidOperationException("Status string can only be sent once.");
            }
        }

        /// <summary>
        /// Sends a collection of response headers to the client
        /// </summary>
        /// <param name="headers">The headers to send</param>
        /// <remarks>This can only be used before the first body text is sent</remarks>
        public void SendResponseHeader(Dictionary<string, string> headers)
        {
            foreach (KeyValuePair<string, string> kvp in headers)
            {
                SendResponseHeader(kvp.Key, kvp.Value);
            }
        }

        /// <summary>
        /// Sends a response header to the client
        /// </summary>
        /// <param name="headerName">The header name</param>
        /// <param name="headerContents">The value of the header</param>
        /// <remarks>This can only be used before the first body text is sent</remarks>
        public void SendResponseHeader(string headerName, string headerContents)
        {
            if (!_responseStringSent)
            {
                throw new InvalidOperationException("Status string must be sent before any headers.");
            }
            else if (_bodyStarted)
            {
                throw new InvalidOperationException("Body already started, headers can only be sent before the body.");
            }
            else
            {
                _clientSocket.Send(Encoding.ASCII.GetBytes(string.Format("{0}: {1}\r\n", headerName, headerContents)));
            }
        }

        /// <summary>
        /// Sends body-text to the client
        /// </summary>
        /// <param name="bodyContents">Body-text to send</param>
        public void SendResponseBody(string bodyContents)
        {
            SendResponseBody(Encoding.ASCII.GetBytes(bodyContents));
        }

        /// <summary>
        /// Sends body-text to the client
        /// </summary>
        /// <param name="bodyContents">Body-text to send</param>
        public void SendResponseBody(byte[] bodyContents)
        {
            if (!_responseStringSent)
            {
                throw new InvalidOperationException("Status string must be sent before body.");
            }
            else if (!_bodyStarted)
            {
                _clientSocket.Send(Encoding.ASCII.GetBytes("\r\n"));
            }

            if (_httpRequest.Method != "HEAD")
                _clientSocket.Send(bodyContents);
        }

        public void SendAuthorizationRequired()
        {
            SendResponseStatus(HttpStatusCode.Unauthorized);

            Dictionary<string, string> headers = GetStandardHeaders();
            headers.Add("WWW-Authenticate", string.Format("Basic realm=\"{0}\"", _httpServer.Realm));
            SendResponseHeader(headers);

            SendResponseBody("Error 401: Unauthorized");
        }

        /// <summary>
        /// Sends an HTTP error
        /// </summary>
        /// <param name="statusCode">The HTTP status code to send</param>
        public void SendError(HttpStatusCode statusCode)
        {
            SendError(statusCode, null);
        }

        /// <summary>
        /// Sends an HTTP error
        /// </summary>
        /// <param name="statusCode">The HTTP status code to send</param>
        /// <param name="text">The text to send as the body</param>
        public void SendError(HttpStatusCode statusCode, string text)
        {
            SendResponseStatus(statusCode);
            SendResponseHeader(GetStandardHeaders());

            if (text == null)
            {
                SendResponseBody("Error: " + statusCode.GetStringValue());
            }
            else
            {
                SendResponseBody(text);
            }

            throw new HttpException(statusCode, _httpRequest);
        }

        /// <summary>
        /// Gets the MIME type of a files
        /// </summary>
        /// <param name="path">Path to the file</param>
        /// <returns>The MIME type</returns>
        public string GetMimeType(string path)
        {
            string mimeType;

            // Find mime type
            if (!_httpServer.MimeTypes.TryGetValue(Path.GetExtension(path).TrimStart('.'), out mimeType))
            {
                mimeType = "text/plain";
            }

            return mimeType;
        }

        /// <summary>
        /// Decodes a query string or post request (only application/x-www-form-urlencoded supported currently)
        /// </summary>
        /// <param name="request">The request object</param>
        /// <param name="isPost">Weather to get post values (true) or get values (false)</param>
        /// <returns></returns>
        public Dictionary<string, string> DecodeQueryString(HttpRequest request, bool isPost)
        {
            if (isPost)
            {
                string contentType;
                if (request.Headers.TryGetValue("Content-Type", out contentType))
                {
                    switch ((string)contentType.Split(';').GetValue(0))
                    {
                        case "application/x-www-form-urlencoded":
                            return DecodeQueryString(Encoding.ASCII.GetString(request.PostData));
                            break;
                        default:
                            return null;
                    }
                }
                else
                {
                    return null;
                }
            }
            else
            {
                return DecodeQueryString(request.Uri.Query);
            }
        }

        public Dictionary<string, string> DecodeQueryString(string queryString)
        {
            Dictionary<string, string> decodedQuery = new Dictionary<string, string>();

            queryString = HttpUtility.UrlDecode(queryString.TrimStart('?'));
            string[] keyValuePairStrings = queryString.Split('&');

            foreach (string keyValuePairString in keyValuePairStrings)
            {
                string[] keyValuePairArray = keyValuePairString.Split(new char[] { '=' }, StringSplitOptions.None);

                if (!decodedQuery.ContainsKey(keyValuePairArray[0]))
                {
                    decodedQuery.Add(keyValuePairArray[0], (keyValuePairArray[1] == null ? "" : keyValuePairArray[1]));
                }
            }

            return decodedQuery;
        }
        #endregion
    }
}
