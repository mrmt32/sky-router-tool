using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace pHMb.Router
{
    public enum PowerState
    {
        L0,
        L1,
        L2,
        L3
    }

    /// <summary>
    /// Describes various connection details
    /// </summary>
    public class ConnectionDetails
    {
        /// <summary>
        /// The current connection status (e.g. showtime or negotiating)
        /// </summary>
        public string Status;

        /// <summary>
        /// The current power state
        /// </summary>
        public PowerState PowerState;

        /// <summary>
        /// The connection mode (ADSL2+/ADSL2/G.DMT etc.)
        /// </summary>
        public string Mode;

        /// <summary>
        /// The channel (Interleaved/FAST)
        /// </summary>
        public string Channel;

        /// <summary>
        /// Details about the downstream connection
        /// </summary>
        public SyncDetails DownstreamSync;

        /// <summary>
        /// Details about the upstream connection
        /// </summary>
        public SyncDetails UpstreamSync;

        public ErrorDetails Errors;
    }
}
