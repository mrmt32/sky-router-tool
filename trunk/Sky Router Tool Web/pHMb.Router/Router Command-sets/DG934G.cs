using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace pHMb.Router.RouterCommandSets
{
    public class DG934G : Busybox
    {
        #region Private Methods
        private Dictionary<int, int> GetBitLoading()
        {
            Dictionary<int, int> output = new Dictionary<int, int>();
            int toneId = 0;

            string bitLoadingText = _routerHttp.SendCommand("cat /proc/avalanche/avsar_bit_allocation_table");

            Match bitLoadingMatch = Regex.Match(bitLoadingText.Replace("AR7 DSL Modem US Bit Allocation:", "").Replace("AR7 DSL Modem DS Bit Allocation:", ""), "[A-f0-9]{2}");
            while (bitLoadingMatch.Success)
            {
                output.Add(toneId, Convert.ToInt32(bitLoadingMatch.Value, 16));
                bitLoadingMatch = bitLoadingMatch.NextMatch();
                toneId++;
            }
            return output;
        }

        private ConnectionDetails GetConnectionDetails()
        {
            ConnectionDetails details = new ConnectionDetails();
            details.DownstreamSync = new SyncDetails();
            details.UpstreamSync = new SyncDetails();
            
            // Get connection status
            string connectionStatusText = _routerHttp.SendCommand("cat /proc/avalanche/avsar_modem_training");

            // Get status text
            details.Status = Regex.Match(connectionStatusText, "(.*?)\r?\n").Groups[1].Value;

            // Check the modem is currently synced
            if (details.Status == "SHOWTIME")
            {
                try
                {
                    // Get mode table
                    string modeTableText = _routerHttp.SendCommand("cat /proc/avalanche/avsar_dsl_modulation_schemes");

                    // Get connection details from telnet
                    string conDetailsText = _routerHttp.SendCommand("cat /proc/avalanche/avsar_modem_stats");

                    details.PowerState = (PowerState)Enum.Parse(typeof(PowerState), Regex.Match(conDetailsText, "Power Management Status:[\t ]*(L.)").Groups[1].Value);

                    // Parse mode ids
                    Dictionary<int, string> modes = new Dictionary<int, string>();
                    Match modeTableMatch = Regex.Match(modeTableText, "([^\t^\r^\n^ ]*)[\t ]*?0x([A-f0-9]*)");
                    while (modeTableMatch.Success)
                    {
                        int currentModeId = Convert.ToInt32(modeTableMatch.Groups[2].Value, 16);

                        if (!modes.ContainsKey(currentModeId))
                            modes.Add(currentModeId, modeTableMatch.Groups[1].Value);

                        modeTableMatch = modeTableMatch.NextMatch();
                    }

                    // Parse ADSL mode
                    Match modeMatch = Regex.Match(conDetailsText, "Trained Mode:[\t ]*([0-9]*)");
                    int modeId = int.Parse(modeMatch.Groups[1].Value);
                    details.Mode = modes[modeId];

                    // Parse Channel (interleaved/fast)
                    Match channelMatch = Regex.Match(conDetailsText, "Trained Path:[\t ]*([0-9]*)");
                    switch (channelMatch.Groups[1].Value)
                    {
                        case "0":
                            details.Channel = "FAST";
                            break;
                        case "1":
                            details.Channel = "INTERLEAVED";
                            break;
                        default:
                            details.Channel = "Unknown";
                            break;
                    }
                    

                    // Parse SNR margins + Attenuations
                    Match snrMatch = Regex.Match(conDetailsText, "DS Line Attenuation:[\t ]*?(-?[0-9]*\\.?[0-9]*)[\t ]*DS Margin:[\t ]*(-?[0-9]*\\.?[0-9]*)");
                    details.DownstreamSync.SnrMargin = decimal.Parse(snrMatch.Groups[2].Value);
                    details.DownstreamSync.Attenuation = decimal.Parse(snrMatch.Groups[1].Value);

                    snrMatch = Regex.Match(conDetailsText, "US Line Attenuation:[\t ]*?(-?[0-9]*\\.?[0-9]*)[\t ]*US Margin:[\t ]*(-?[0-9]*\\.?[0-9]*)");
                    details.UpstreamSync.SnrMargin = decimal.Parse(snrMatch.Groups[2].Value);
                    details.UpstreamSync.Attenuation = decimal.Parse(snrMatch.Groups[1].Value);

                    // Parse Power
                    Match powerMatch = Regex.Match(conDetailsText, "US Transmit Power ?:[\t ]*([0-9]*)[\t ]*DS Transmit Power:[\t ]*([0-9]*)");
                    details.DownstreamSync.Power = decimal.Parse(powerMatch.Groups[1].Value);
                    details.UpstreamSync.Power = decimal.Parse(powerMatch.Groups[2].Value);

                    // Parse max attainable rate (this is predicted by the router)
                    Match maxRateMatch = Regex.Match(conDetailsText, "DS Max Attainable Bit Rate:[\t ]*([0-9]*) kbps");
                    details.DownstreamSync.MaxAttainableRate = int.Parse(maxRateMatch.Groups[1].Value);

                    maxRateMatch = Regex.Match(conDetailsText, "US Max Attainable Bit Rate:[\t ]*([0-9]*) bps");
                    details.UpstreamSync.MaxAttainableRate = int.Parse(maxRateMatch.Groups[1].Value) / 1024;

                    // Parse sync rate
                    Match syncRateMatch = Regex.Match(conDetailsText, "US Connection Rate:[\t ]*?([0-9]*)[\t ]*DS Connection Rate:[\t ]*([0-9]*)");
                    details.DownstreamSync.SyncRate = int.Parse(syncRateMatch.Groups[2].Value);
                    details.UpstreamSync.SyncRate = int.Parse(syncRateMatch.Groups[1].Value);

                    // Parse Errors
                    Match losErrorsMatch = Regex.Match(conDetailsText, "LOS errors:[\t ]*([0-9]*)");
                    details.Errors.Total.Los = int.Parse(losErrorsMatch.Groups[1].Value);

                    Match esErrorsMatch = Regex.Match(conDetailsText, "Errored Seconds:[\t ]*([0-9]*)");
                    details.Errors.Total.Es = int.Parse(esErrorsMatch.Groups[1].Value);

                    Match crcErrorsMatch = Regex.Match(conDetailsText, "CRC:[\t ]*([0-9]*)");
                    details.Errors.Total.Crc = 0;
                    while (crcErrorsMatch.Success)
                    {
                        details.Errors.Total.Crc += int.Parse(crcErrorsMatch.Groups[1].Value);
                        crcErrorsMatch = crcErrorsMatch.NextMatch();
                    }

                    /*
                    details.Errors.Total.Lof = int.Parse(errorsMatch.Groups[3].Value);
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
                    */
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
            private DG934G _routerCommand;

            private string _lanMacAddress;
            private ConnectionDetails _connectionDetails;
            private List<RouterProcess> _processList;
            private Dictionary<int, int> _bitLoading;
            private Dictionary<int, double> _snr;
            private Dictionary<int, double> _hlog;
            private Dictionary<int, double> _qln;
            private double? _uptime;
            private uint[] _bytesTransferred;

            public RouterData(DG934G routerCommand)
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
                        //_snr = _routerCommand.GetSnrTable();
                        _snr = new Dictionary<int, double>();
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
                        //_hlog = _routerCommand.GetHlogTable();
                        _hlog = new Dictionary<int, double>();
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
                        //_qln = _routerCommand.GetQlnTable();
                        _qln = new Dictionary<int, double>();
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
        private object _updateLocker = new object();
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
        public DG934G(RouterHttp routerHttp) : base(routerHttp) { }

        /// <summary>
        /// Clears all cached data
        /// </summary>
        public override void Update()
        {
            RouterInfo = new RouterData(this);
        }

        public override void Resync()
        {
            throw new NotSupportedException("Resyncing is not supported for the DG934G router.");
        }

        public override void SetTargetSnrm(double snrm)
        {
            throw new NotSupportedException("Changing the target SNR margin is not supported for the DG934G router.");
        } 
        #endregion
    }
}
