using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace pHMb.Router
{
    /// <summary>
    /// Describes connection details which are common between upstream/downstream
    /// </summary>
    public class SyncDetails
    {
        /// <summary>
        /// The Signal to Noise Ratio Margin
        /// </summary>
        public decimal SnrMargin;

        /// <summary>
        /// The overall attenuation (signal loss)
        /// </summary>
        public decimal Attenuation;

        /// <summary>
        /// The power of the signal
        /// </summary>
        public decimal Power;

        /// <summary>
        /// The estimated maximum attainable rate (calculated by the router from the snr and attenuation values)
        /// </summary>
        public int MaxAttainableRate;

        /// <summary>
        /// The rate the router is currently synced at
        /// </summary>
        public int SyncRate;

        /// <summary>
        /// The number of errored seconds
        /// </summary>
        public int ErroredSeconds;
    }
}
