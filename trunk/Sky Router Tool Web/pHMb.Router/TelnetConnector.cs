using System;
using System.Collections.Generic;

using System.Text;
using System.Net.Sockets;
using System.IO;
using System.Threading;


namespace pHMb.Router
{
    public class TelnetReader : TextReader
    {
        public NetworkStream BaseStream { get; private set; }

        public TelnetReader(NetworkStream stream)
        {
            BaseStream = stream;
        }

        public override int Read()
        {
            return BaseStream.ReadByte();
        }
    }

    public class TelnetConnector : IRouterConnection
    {
        private TcpClient _tcpClient;
        private NetworkStream _tcpStream;
        private TelnetReader _telnetReader;
        private StreamWriter _telnetWriter;
        private object _locker = new object();

        public string Username { get; set; }
        public string Password { get; set; }
        public string Host { get; set; }
        public int HttpServerPort { get; set; }
        public string HttpServerPassword { get; set; }
        public string HttpServerUsername { get; set; }
        public bool Connected { get; private set; }
        public bool SkyCompatibilityMode { get; set; }
        public bool isSagem { get; set; }
       
        public TelnetConnector(string username, string password, string host, int httpServerPort, string httpServerUsername, string httpServerPassword, bool skyCompatibilityMode)
        {
            Username = username;
            Password = password;
            Host = host;
            HttpServerPort = httpServerPort;
            HttpServerUsername = httpServerUsername;
            HttpServerPassword = httpServerPassword;
            SkyCompatibilityMode = skyCompatibilityMode;
            Connected = false;

            new Thread(new ThreadStart(Connect)).Start();
        }

        private string TelnetReadAll(int timeToWait = 800)
        {
            char[] buffer = new char[0];
            int charCount;
            StringBuilder output = new StringBuilder();

            Thread.Sleep(timeToWait);
            while ((charCount = _tcpClient.Available) > 0)
            {
                buffer = new char[charCount];
                _telnetReader.Read(buffer, 0, charCount);

                output.Append(buffer);
                Thread.Sleep(timeToWait);
            }

            return output.ToString();
        }

        private void TelnetSlowSend(string text, int characterTime = 100)
        {
            char[] textChar = text.ToCharArray();

            foreach (char character in textChar)
            {
                _telnetWriter.Write(character);
                _telnetWriter.Flush();
                Thread.Sleep(characterTime);
            }
        }

        public void Connect()
        {
            lock (_locker)
            {
                try
                {
                    int port = 23;
                    // Connect to telnet server
                    _tcpClient = new TcpClient(Host, port);
                    _tcpClient.ReceiveTimeout = 5000;
                    _tcpStream = _tcpClient.GetStream();
                    _tcpClient.Client.Blocking = false;

                    _telnetReader = new TelnetReader(_tcpStream);
                    _telnetWriter = new StreamWriter(_tcpStream);

                    // Allow the telnet server some time to send data:
                    Thread.Sleep(200);

                    if (TelnetReadAll().Contains("Login:"))
                    {
                        // Try logging in with login details:
                        _telnetWriter.Write(Username + "\r\n");
                        _telnetWriter.Flush();

                        if (TelnetReadAll().Contains("Password:"))
                        {
                            // Username accepted, enter password
                            TelnetSlowSend(Password + "\r\n");
                        }
                        else
                        {
                            throw new ArgumentException("Error connecting to telnet, are login details correct?");
                        }
                    }

                    // Seek to the prompt
                    string prompt;
                    if (isSagem)
                    {
                        prompt = TelnetReadAll();
                        if (prompt.Trim().EndsWith("->"))
                        {
                            // Lets get a real shell
                            _telnetWriter.Write("sh\r\n");
                            _telnetWriter.Flush();
                        }
                        else if (prompt.Contains("Login incorrect"))
                        {
                            throw new ArgumentException("Telnet login details incorrect.");
                        }
                        else
                        {
                            throw new ArgumentException("Error connecting to telnet, could not find sagem prompt.");
                        }
                    }

                    prompt = TelnetReadAll();
                    if (prompt.Trim().EndsWith("#"))
                    {
                        // Success!
                        Connected = true;
                    }
                    else if (prompt.Contains("Login incorrect"))
                    {
                        throw new ArgumentException("Telnet login details incorrect.");
                    }
                    else
                    {
                        throw new ArgumentException("Error connecting to telnet, could not find prompt.");
                    }
                }
                catch
                {
                    Connected = false;
                    _tcpClient.Close();
                    Thread.Sleep(10000);
                    Connect();
                }
            }
        }

        /// <summary>
        /// Closes the telnet connection
        /// </summary>
        public void Close()
        {
            _telnetWriter.Close();
            _telnetReader.Close();
        }

        /// <summary>
        /// Sends a command to the server and retreives the response
        /// </summary>
        /// <param name="command">The command</param>
        /// <returns>The response from the command</returns>
        public string SendCommand(string command)
        {
            while (!Connected)
            {
                Thread.Sleep(500);
            }

            lock (_locker)
            {
                // Flush out any data in buffer
                Console.WriteLine("Flushed: " + TelnetReadAll());

                // Send the command
                Console.WriteLine("Telnet > " + command + "; echo \"&&TERMINATE&&\"");
                _telnetWriter.WriteLine(command + "; echo \"&&TERMINATE&&\"");
                _telnetWriter.Flush();

                while (_tcpClient.Available < command.Length + "; echo \"&&TERMINATE&&\"\r\n".Length) { }

                char[] discarded = new char[command.Length + "; echo \"&&TERMINATE&&\"\r\n".Length];
                _telnetReader.Read(discarded, 0, command.Length + "; echo \"&&TERMINATE&&\"\r\n".Length);

                StringBuilder output = new StringBuilder();
                string buffer;
                int i = 0;
                while (!output.ToString().Contains("&&TERMINATE&&") && i < 30)
                {
                    // Retreive any response
                    buffer = TelnetReadAll(1);
                    if (buffer.Length > 0)
                    {
                        output.Append(buffer);
                        i = 0;
                    }
                    else
                    {
                        i++;
                    }
                    Thread.Sleep(100);
                }

                string outString = output.ToString();
                return outString.Substring(0, outString.Length - 17);
            }
        }


        public event EventHandler<ConnectionErrorEventArgs> ConnectionError;

        public event EventHandler ConnectionSuccess;
    }
}
