using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Text;

namespace pHMb.Router.RouterCommandSets
{
    /// <summary>
    /// All commands common to busybox routers
    /// </summary>
    public abstract class Busybox : Interfaces.IRouterInterface
    {
        public IRouterConnection RouterConnection 
        {
            get
            {
                return _routerConnection;
            }
        }
        protected IRouterConnection _routerConnection;

        #region Private Methods

        /// <summary>
        /// Gets the time in seconds since the router was last reset
        /// </summary>
        /// <returns>Time in seconds since last reset</returns>
        protected virtual double GetUptime()
        {
            string result = _routerConnection.SendCommand("cat /proc/uptime");
            Match timesMatch = Regex.Match(result, "([0-9]*\\.[0-9]*) ([0-9]*\\.[0-9]*)");

            return double.Parse(timesMatch.Groups[1].Value);
        }

        /// <summary>
        /// Gets the number of bytes transfered since the count last reached 2^32 bytes (it's an int32!)
        /// </summary>
        /// <returns>The number of bytes transfered, [0] is upload, [1] is download</returns>
        protected virtual uint[] GetBytesTransferred()
        {
            string result = _routerConnection.SendCommand("ifconfig ppp0");
            Match transferedMatch = Regex.Match(result, "RX bytes:([0-9]*).*TX bytes:([0-9]*)");

            return new uint[] { uint.Parse(transferedMatch.Groups[2].Value), uint.Parse(transferedMatch.Groups[1].Value) };
        }

        protected List<T> RegexAndParse<T>(string input, string pattern, int[] groups)
        {
            Match regexMatch = Regex.Match(input, pattern);

            List<T> result = new List<T>();

            if (regexMatch.Success)
            {
                foreach (int group in groups)
                {
                    if (regexMatch.Groups[group].Value != "")
                    {
                        result.Add((T)typeof(T).GetMethod("parse").Invoke(null, new object[] { regexMatch.Groups[group].Value }));
                    }
                }
            }
            return result;
        }

        protected T RegexAndParse<T>(string input, string pattern, int group)
        {
            Match regexMatch = Regex.Match(input, pattern);

            T result = default(T);

            if (regexMatch.Success)
            {
                if (regexMatch.Groups[group].Value != "")
                {
                    result = (T)typeof(T).GetMethod("Parse", new Type[] { typeof(string) }).Invoke(null, new object[] { regexMatch.Groups[group].Value });
                }
            }
            return result;
        }


        protected virtual string GetLanMacAddress()
        {
            string result = _routerConnection.SendCommand("ifconfig ath0");

            return Regex.Match(result, "HWaddr ([A-F0-9:]*)").Groups[1].Value.Replace(":", "");
        }

        protected ProcessRunState StringToRunState(string runStateString)
        {
            ProcessRunState runState;
            switch (runStateString)
            {
                case "S":
                    runState = ProcessRunState.InterruptibleSleep;
                    break;

                case "R":
                    runState = ProcessRunState.Running;
                    break;

                case "D":
                    runState = ProcessRunState.UninterruptibleSleep;
                    break;

                case "T":
                    runState = ProcessRunState.Stopped;
                    break;

                default:
                    runState = ProcessRunState.Other;
                    break;
            }
            return runState;
        }

        protected ProcessPriority StringToPriority(string priorityString)
        {
            ProcessPriority priority;
            switch (priorityString)
            {
                case "<":
                    priority = ProcessPriority.HighPriority;
                    break;

                case "N":
                    priority = ProcessPriority.LowPriority;
                    break;

                case " ":
                    priority = ProcessPriority.Normal;
                    break;

                default:
                    priority = ProcessPriority.Other;
                    break;
            }
            return priority;
        }

        protected virtual List<RouterProcess> GetProcessList()
        {
            string psText = _routerConnection.SendCommand("ps -w");
            List<RouterProcess> processList = new List<RouterProcess>();

            Match processMatch = Regex.Match(psText, " *([0-9]+) *([A-z]+) *([0-9]*) (.)(.)(.) (.*)\n");

            while (processMatch.Success)
            {
                int memUsage = 0;
                int.TryParse(processMatch.Groups[3].Value, out memUsage);

                // Find run-state
                ProcessRunState runState = StringToRunState(processMatch.Groups[4].Value);

                // Find priority
                ProcessPriority priority = StringToPriority(processMatch.Groups[6].Value);

                processList.Add(new RouterProcess()
                {
                    Pid = int.Parse(processMatch.Groups[1].Value),
                    UserId = processMatch.Groups[2].Value,
                    VmSize = memUsage,
                    RunState = runState,
                    isPaging = processMatch.Groups[5].Value == "W",
                    Priority = priority,
                    CommandLine = processMatch.Groups[7].Value
                });

                processMatch = processMatch.NextMatch();
            }
            return processList;
        }
        #endregion

        #region Public Methods
        /// <summary>
        /// Constructor
        /// </summary>
        public Busybox(string username, string password, string host, int httpServerPort, string httpServerUsername, string httpServerPassword, bool skyCompatibilityMode)
        {
            
        } 
        #endregion

        #region IRouterInterface Members
        public abstract Interfaces.IRouterData RouterInfo { get; protected set; }

        /// <summary>
        /// Clears all cached data
        /// </summary>
        public abstract void Update();

        public abstract void SetTargetSnrm(double snrm);

        public abstract void Resync();

        public byte[] GetFirmware()
        {
            return Encoding.ASCII.GetBytes(_routerConnection.SendCommand("cat /dev/mtdblock/2; cat /dev/mtdblock/1"));
        }

        public string FlashFirmware(byte[] firmware)
        {
            //return _routerHttp.SendCommand("cd /tmp/; wget http://[srtip]/fw.bin; cat fw.bin > /dev/mtdblock/1");
            return "";
        }

        /// <summary>
        /// Gets detailed process info for a process
        /// </summary>
        /// <param name="pid">Process ID</param>
        /// <returns></returns>
        public RouterProcessDetailed GetDetailedProcessInfo(int pid)
        {
            RouterProcessDetailed process = new RouterProcessDetailed();

            string psText = _routerConnection.SendCommand(string.Format("cat /proc/{0:G}/cmdline; echo @\\\\$@; cat /proc/{0:G}/environ; echo @\\\\$@; cat /proc/{0:G}/status", pid)).Replace("\0", " ");


            process.CommandLine = Regex.Match(psText, "(.*?)@\\$@").Groups[1].Value;

            if (Regex.Match(process.CommandLine, "cat:(.*)").Success)
            {
                // The process does not exist!
                process.CommandLine = null;
            }
            else
            {
                // Parse environment variables
                process.Environment = new Dictionary<string, string>();

                string[] envList = Regex.Match(psText, Regex.Escape(process.CommandLine) + "@\\$@\n(.*)@\\$@").Groups[1].Value.Split(' ');
                foreach (string env in envList)
                {
                    string[] envKvp = env.Split('=');
                    if (envKvp.Length == 2)
                    {
                        process.Environment.Add(envKvp[0], envKvp[1]);
                    }
                }

                process.Name = Regex.Match(psText, "Name:[\t ]+(.*)\n").Groups[1].Value;

                process.RunState = StringToRunState(Regex.Match(psText, "State:[\t ]+(.*) \\(").Groups[1].Value);

                process.SleepAverage = RegexAndParse<short>(psText, "SleepAVG:[\t ]+(.*)%", 1);

                process.Pid = RegexAndParse<int>(psText, "Pid:[\t ]+([0-9]*)", 1);

                process.VmSize = RegexAndParse<int>(psText, "VmSize:[\t ]+([0-9]*)", 1);

                process.VmRSS = RegexAndParse<int>(psText, "VmRSS:[\t ]+([0-9]*)", 1);

                process.VmData = RegexAndParse<int>(psText, "VmData:[\t ]+([0-9]*)", 1);

                process.VmStk = RegexAndParse<int>(psText, "VmStk:[\t ]+([0-9]*)", 1);

                process.VmExe = RegexAndParse<int>(psText, "VmExe:[\t ]+([0-9]*)", 1);

                process.Threads = RegexAndParse<int>(psText, "Threads:[\t ]+([0-9]*)", 1);
            }

            return process;
        }

        /// <summary>
        /// Pings a host
        /// </summary>
        /// <param name="hostname">The host to ping</param>
        /// <returns>Result from ping operation</returns>
        public string GetPing(string hostname)
        {
            return _routerConnection.SendCommand("ping -c 5 '" + hostname + "'");
        }

        /// <summary>
        /// Performs a speed test using the specified url (it downloads the file and times how long it takes)
        /// </summary>
        /// <param name="url">The url of the file to download</param>
        /// <param name="size">The size of the file in KB</param>
        /// <returns>The speed in KB/sec</returns>
        public double PerformSpeedTest(string url, double size)
        {
            string result = _routerConnection.SendCommand(string.Format("cat /proc/uptime; wget -O /dev/null {0}; cat /proc/uptime", url));

            // Parse times
            Match timesMatch = Regex.Match(result, "([0-9]*\\.[0-9]*) ([0-9]*\\.[0-9]*)\n");

            double startTime = double.Parse(timesMatch.Groups[1].Value);
            double endTime = double.Parse(timesMatch.NextMatch().Groups[1].Value);

            double timeTaken = endTime - startTime;

            return (size / timeTaken);
        }

        public bool KillProcess(int pid)
        {
            string result = _routerConnection.SendCommand(string.Format("kill {0:G0}", pid));
            if (result.StartsWith("kill:"))
            {
                return false;
            }
            else
            {
                return true;
            }
        }

        public void Reboot()
        {
            _routerConnection.SendCommand("reboot");
        }
        #endregion
    }
}
