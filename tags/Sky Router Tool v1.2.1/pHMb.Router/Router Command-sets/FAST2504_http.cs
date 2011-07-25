using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace pHMb.Router.RouterCommandSets
{
    /// <summary>
    /// Class for communicating with the sagem F@ST 2504 router by http
    /// (with my custom firmware)
    /// </summary>
    public class FAST2504_http : BCM6348
    {
        public FAST2504_http(string username, string password, string host, int httpServerPort, string httpServerUsername, string httpServerPassword, bool skyCompatibilityMode)
            : base(username, password, host, httpServerPort, httpServerUsername, httpServerPassword, skyCompatibilityMode) 
        {
            _routerConnection = new RouterHttp(username, password, host, httpServerPort, httpServerUsername, httpServerPassword, skyCompatibilityMode, false);
            Update();
        }
        protected override uint[] GetBytesTransferred()
        {
            string result = _routerConnection.SendCommand("ifconfig atm0");
            Match transferedMatch = Regex.Match(result, "RX bytes:([0-9]*).*TX bytes:([0-9]*)");

            return new uint[] { uint.Parse(transferedMatch.Groups[2].Value), uint.Parse(transferedMatch.Groups[1].Value) };
        }

        protected override string GetLanMacAddress()
        {
            string result = _routerConnection.SendCommand("ifconfig br0");

            return Regex.Match(result, "HWaddr ([A-F0-9:]*)").Groups[1].Value.Replace(":", "");
        }
    }
}
