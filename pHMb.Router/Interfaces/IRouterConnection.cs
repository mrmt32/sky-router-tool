using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace pHMb.Router
{
    public class ConnectionErrorEventArgs : EventArgs
    {
        public Exception Exception;
        public bool isIncorrectCredentials;
    }
    /// <summary>
    /// Classes which provide connection to the router via HTTP/Telnet etc
    /// </summary>
    public interface IRouterConnection
    {
        string SendCommand(string command);
        event EventHandler<ConnectionErrorEventArgs> ConnectionError;
        event EventHandler ConnectionSuccess;

        string Username { get; set; }
        string Password { get; set; }
        string Host { get; set; }
        int HttpServerPort { get; set; }
        string HttpServerPassword { get; set; }
        string HttpServerUsername { get; set; }
        bool Connected { get; }
        bool SkyCompatibilityMode { get; set; }
    }
}
