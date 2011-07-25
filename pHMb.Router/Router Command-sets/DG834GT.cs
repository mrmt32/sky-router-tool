using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace pHMb.Router.RouterCommandSets
{
    /// <summary>
    /// Class for communicating with the DG834GT (Sky BB Netgear V1) router
    /// </summary>
    public class DG834GT : BCM6348
    {
        public DG834GT(string username, string password, string host, int httpServerPort, string httpServerUsername, string httpServerPassword, bool skyCompatibilityMode)
            : base(username, password, host, httpServerPort, httpServerUsername, httpServerPassword, skyCompatibilityMode) 
        {
            _routerConnection = new RouterHttp(username, password, host, httpServerPort, httpServerUsername, httpServerPassword, skyCompatibilityMode);
            Update();
        }
    }
}
