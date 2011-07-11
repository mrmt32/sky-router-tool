using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace pHMb.Router.RouterCommandSets
{
    /// <summary>
    /// Adds support for the F@ST2504 router
    /// </summary>
    public class F_ST2504 : BCM6348
    {
        public F_ST2504(string username, string password, string host, int httpServerPort, string httpServerUsername, string httpServerPassword, bool skyCompatibilityMode)
            : base(username, password, host, httpServerPort, httpServerUsername, httpServerPassword, skyCompatibilityMode) 
        {
            _routerConnection = new TelnetConnector(username, password, host, httpServerPort, httpServerUsername, httpServerPassword, skyCompatibilityMode);
            ((TelnetConnector)_routerConnection).isSagem = true;
            Update();
        }
    }
}
