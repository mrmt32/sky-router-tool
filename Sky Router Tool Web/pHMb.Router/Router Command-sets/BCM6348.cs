using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace pHMb.Router.RouterCommandSets
{
    /// <summary>
    /// Provides support for the Broadcom BCM6348 chipset
    /// </summary>
    public class BCM6348 : Busybox
    {
        private object _updateLocker = new object();

        #region Private Methods
        private Dictionary<int, double> ParseAdslTableDouble(string tableText)
        {
            Dictionary<int, double> outputTable = new Dictionary<int, double>();

            Match regexMatch = Regex.Match(tableText, "\n   ([0-9]+)[\t ]*(-?[0-9]+\\.[0-9]+).*\n");

            while (regexMatch.Success)
            {
                outputTable.Add(int.Parse(regexMatch.Groups[1].Value), double.Parse(regexMatch.Groups[2].Value));

                regexMatch = regexMatch.NextMatch();
            }

            return outputTable;
        }

        private Dictionary<int, int> ParseAdslTableInt(string tableText)
        {
            Dictionary<int, int> outputTable = new Dictionary<int, int>();

            Match regexMatch = Regex.Match(tableText, "\n   ([0-9]+)[\t ]*?([0-9]+).*\n");

            while (regexMatch.Success)
            {
                outputTable.Add(int.Parse(regexMatch.Groups[1].Value), int.Parse(regexMatch.Groups[2].Value));

                regexMatch = regexMatch.NextMatch();
            }

            return outputTable;
        }

        /// <summary>
        /// Gets bit loading info (how may bits are loaded onto each tone)
        /// </summary>
        /// <returns>Bit loading info</returns>
        private Dictionary<int, int> GetBitLoading()
        {
            string bitLoadingText = _routerConnection.SendCommand("adslctl info --Bits");

            return ParseAdslTableInt(bitLoadingText);
        }

        /// <summary>
        /// Gets a table of SNR values for each tone
        /// </summary>
        /// <returns>Table of SNR values</returns>
        private Dictionary<int, double> GetSnrTable()
        {
            string snrText = _routerConnection.SendCommand("adslctl info --SNR");

            return ParseAdslTableDouble(snrText);
        }

        /// <summary>
        /// Gets the Quiet Line Noise (this is determined during training and is a measure of the amount of noise on the line)
        /// </summary>
        /// <returns>Table of QLN values</returns>
        private Dictionary<int, double> GetQlnTable()
        {
            string qlnText = _routerConnection.SendCommand("adslctl info --QLN");

            return ParseAdslTableDouble(qlnText);
        }

        /// <summary>
        /// Gets a table of Hlog(f) values (like attenuation)
        /// </summary>
        /// <returns>Table of Hlog(f) values</returns>
        private Dictionary<int, double> GetHlogTable()
        {
            string hlogText = _routerConnection.SendCommand("adslctl info --Hlog");

            return ParseAdslTableDouble(hlogText);
        }

        /// <summary>
        /// Gets various connection details
        /// </summary>
        /// <returns>ConnectionDetails object holding the details</returns>
        private ConnectionDetails GetConnectionDetails()
        {
            ConnectionDetails details = new ConnectionDetails();
            details.DownstreamSync = new SyncDetails();
            details.UpstreamSync = new SyncDetails();

            // Get connection details from telnet
            string conDetailsText = _routerConnection.SendCommand("adslctl info --stats");
            
            details.PowerState = (PowerState)Enum.Parse(typeof(PowerState), Regex.Match(conDetailsText, "Power State:[\t ]*(.*?)\n").Groups[1].Value);

            details.Status = Regex.Match(conDetailsText, "Status:[\t ]*(.*?)(Retrain.*?)?\n").Groups[1].Value;

            // Check the modem is currently synced
            if (details.Status == "Showtime")
            {
                try
                {
                    // Parse ADSL mode
                    details.Mode = Regex.Match(conDetailsText, "Mode:\t*(.*)\n").Groups[1].Value;

                    // Parse Channel (interleaved/fast)
                    details.Channel = Regex.Match(conDetailsText, "Channel:\t*([A-Za-z]*)\n").Groups[1].Value;

                    // Parse SNR margins
                    Match snrMatch = Regex.Match(conDetailsText, "SNR \\(dB\\):\t(-?[0-9]*\\.[0-9]*)\t\t(-?[0-9]*\\.[0-9]*)\n");
                    details.DownstreamSync.SnrMargin = decimal.Parse(snrMatch.Groups[1].Value);
                    details.UpstreamSync.SnrMargin = decimal.Parse(snrMatch.Groups[2].Value);

                    // Parse Attenuations
                    Match attnMatch = Regex.Match(conDetailsText, "Attn\\(dB\\):\t(-?[0-9]*\\.[0-9]*)\t\t(-?[0-9]*\\.[0-9]*)\n");
                    details.DownstreamSync.Attenuation = decimal.Parse(attnMatch.Groups[1].Value);
                    details.UpstreamSync.Attenuation = decimal.Parse(attnMatch.Groups[2].Value);

                    // Parse Power
                    Match powerMatch = Regex.Match(conDetailsText, "Pwr\\(dBm\\):\t(-?[0-9]*\\.[0-9]*)\t\t(-?[0-9]*\\.[0-9]*)\n");
                    details.DownstreamSync.Power = decimal.Parse(powerMatch.Groups[1].Value);
                    details.UpstreamSync.Power = decimal.Parse(powerMatch.Groups[2].Value);

                    // Parse max attainable rate (this is predicted by the router)
                    Match maxRateMatch = Regex.Match(conDetailsText, "Max\\(Kbps\\):\t([0-9]*)\t\t([0-9]*)\n");
                    details.DownstreamSync.MaxAttainableRate = int.Parse(maxRateMatch.Groups[1].Value);
                    details.UpstreamSync.MaxAttainableRate = int.Parse(maxRateMatch.Groups[2].Value);

                    // Parse sync rate
                    Match syncRateMatch = Regex.Match(conDetailsText, "Rate \\(Kbps\\):\t([0-9]*)\t\t([0-9]*)\n");
                    details.DownstreamSync.SyncRate = int.Parse(syncRateMatch.Groups[1].Value);
                    details.UpstreamSync.SyncRate = int.Parse(syncRateMatch.Groups[2].Value);

                    // Parse Errors
                    Match errorsMatch = Regex.Match(conDetailsText, "CRC = ([0-9]*)\nLOS = ([0-9]*)\nLOF = ([0-9]*)\nES  = ([0-9]*)\n");

                    // Total errors:
                    details.Errors.Total.Crc = int.Parse(errorsMatch.Groups[1].Value);
                    details.Errors.Total.Los = int.Parse(errorsMatch.Groups[2].Value);
                    details.Errors.Total.Lof = int.Parse(errorsMatch.Groups[3].Value);
                    details.Errors.Total.Es = int.Parse(errorsMatch.Groups[4].Value);

                    // Errors in last day:
                    errorsMatch = errorsMatch.NextMatch();
                    details.Errors.LatestDay.Crc = int.Parse(errorsMatch.Groups[1].Value);
                    details.Errors.LatestDay.Los = int.Parse(errorsMatch.Groups[2].Value);
                    details.Errors.LatestDay.Lof = int.Parse(errorsMatch.Groups[3].Value);
                    details.Errors.LatestDay.Es = int.Parse(errorsMatch.Groups[4].Value);

                    // Current errors:
                    errorsMatch = errorsMatch.NextMatch();
                    details.Errors.Current.Crc = int.Parse(errorsMatch.Groups[1].Value);
                    details.Errors.Current.Los = int.Parse(errorsMatch.Groups[2].Value);
                    details.Errors.Current.Lof = int.Parse(errorsMatch.Groups[3].Value);
                    details.Errors.Current.Es = int.Parse(errorsMatch.Groups[4].Value);

                    // -30min to -15min
                    errorsMatch = errorsMatch.NextMatch();
                    errorsMatch = errorsMatch.NextMatch();
                    errorsMatch = errorsMatch.NextMatch();
                    details.Errors.M30Min.Crc = int.Parse(errorsMatch.Groups[1].Value);
                    details.Errors.M30Min.Los = int.Parse(errorsMatch.Groups[2].Value);
                    details.Errors.M30Min.Lof = int.Parse(errorsMatch.Groups[3].Value);
                    details.Errors.M30Min.Es = int.Parse(errorsMatch.Groups[4].Value);

                    // -45min to -30min
                    errorsMatch = errorsMatch.NextMatch();
                    details.Errors.M45Min.Crc = int.Parse(errorsMatch.Groups[1].Value);
                    details.Errors.M45Min.Los = int.Parse(errorsMatch.Groups[2].Value);
                    details.Errors.M45Min.Lof = int.Parse(errorsMatch.Groups[3].Value);
                    details.Errors.M45Min.Es = int.Parse(errorsMatch.Groups[4].Value);

                    // -60min to -45min
                    errorsMatch = errorsMatch.NextMatch();
                    details.Errors.M60Min.Crc = int.Parse(errorsMatch.Groups[1].Value);
                    details.Errors.M60Min.Los = int.Parse(errorsMatch.Groups[2].Value);
                    details.Errors.M60Min.Lof = int.Parse(errorsMatch.Groups[3].Value);
                    details.Errors.M60Min.Es = int.Parse(errorsMatch.Groups[4].Value);
                }
                catch (FormatException)
                {

                }
            }
            return details;
        }
        #endregion

        #region Public Data Types
        public class RouterData : Interfaces.IRouterData
        {
            private BCM6348 _routerCommand;

            private string _lanMacAddress;
            private ConnectionDetails _connectionDetails;
            private List<RouterProcess> _processList;
            private Dictionary<int, int> _bitLoading;
            private Dictionary<int, double> _snr;
            private Dictionary<int, double> _hlog;
            private Dictionary<int, double> _qln;
            private double? _uptime;
            private uint[] _bytesTransferred;

            public RouterData(BCM6348 routerCommand)
            {
                LastUpdated = DateTime.Now;
                _routerCommand = routerCommand;
            }

            public DateTime LastUpdated { get; private set; }

            public string LanMacAddress
            {
                get
                {
                    if (_lanMacAddress == null)
                    {
                        _lanMacAddress = _routerCommand.GetLanMacAddress();
                    }
                    return _lanMacAddress;
                }
            }

            public ConnectionDetails ConnectionDetails
            {
                get
                {
                    if (_connectionDetails == null)
                    {
                        _connectionDetails = _routerCommand.GetConnectionDetails();
                    }
                    return _connectionDetails;
                }
            }

            public List<RouterProcess> ProcessList
            {
                get
                {
                    if (_processList == null)
                    {
                        _processList = _routerCommand.GetProcessList();
                    }
                    return _processList;
                }
            }
            
            public Dictionary<int, int> BitLoading
            {
                get
                {
                    if (_bitLoading == null)
                    {
                        _bitLoading = _routerCommand.GetBitLoading();
                    }
                    return _bitLoading;
                }
            }
            
            public Dictionary<int, double> Snr
            {
                get
                {
                    if (_snr == null)
                    {
                        _snr = _routerCommand.GetSnrTable();
                    }
                    return _snr;
                }
            }

            public Dictionary<int, double> Hlog
            {
                get
                {
                    if (_hlog == null)
                    {
                        _hlog = _routerCommand.GetHlogTable();
                    }
                    return _hlog;
                }
            }
            
            public Dictionary<int, double> Qln
            {
                get
                {
                    if (_qln == null)
                    {
                        _qln = _routerCommand.GetQlnTable();
                    }
                    return _qln;
                }
            }

            public double Uptime
            {
                get
                {
                    if (!_uptime.HasValue)
                    {
                        _uptime = _routerCommand.GetUptime();
                    }
                    return _uptime.GetValueOrDefault();
                }
            }

            public uint[] BytesTransferred
            {
                get
                {
                    if (_bytesTransferred == null)
                    {
                        _bytesTransferred = _routerCommand.GetBytesTransferred();
                    }
                    return _bytesTransferred;
                }
            }
        }
        #endregion

        #region Public Properties
        private Interfaces.IRouterData _routerInfo;
        public override Interfaces.IRouterData RouterInfo
        {
            get
            {
                lock (_updateLocker)
                {
                    return _routerInfo;
                }
            }

            protected set
            {
                lock (_updateLocker)
                {
                    _routerInfo = value;
                }
            }
        }
        #endregion

        #region Public Methods
        /// <summary>
        /// Constructor
        /// </summary>
        public BCM6348(string username, string password, string host, int httpServerPort, string httpServerUsername, string httpServerPassword, bool skyCompatibilityMode) : base(username, password, host, httpServerPort, httpServerUsername, httpServerPassword, skyCompatibilityMode) 
        {
        }

        /// <summary>
        /// Clears all cached data
        /// </summary>
        public override void Update()
        {
            RouterInfo = new RouterData(this);
        }

        /// <summary>
        /// Set the target SNR margin
        /// </summary>
        /// <param name="targetSnrm">The target SNRM</param>
        public override void SetTargetSnrm(double targetSnrm)
        {
            _routerConnection.SendCommand(string.Format("adslctl configure --snr {0:N1}", targetSnrm));
        }

        /// <summary>
        /// Resyncs the router
        /// </summary>
        public override void Resync()
        {
            _routerConnection.SendCommand("adslctl connection --up");
        }
        #endregion
    }
}
