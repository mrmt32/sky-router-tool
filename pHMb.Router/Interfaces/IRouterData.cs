using System;
using System.Collections.Generic;

namespace pHMb.Router.Interfaces
{
    public interface IRouterData
    {
        Dictionary<int, int> BitLoading { get; }
        uint[] BytesTransferred { get; }
        ConnectionDetails ConnectionDetails { get; }
        Dictionary<int, double> Hlog { get; }
        string LanMacAddress { get; }
        DateTime LastUpdated { get; }
        List<pHMb.Router.RouterProcess> ProcessList { get; }
        Dictionary<int, double> Qln { get; }
        Dictionary<int, double> Snr { get; }
        double Uptime { get; }
    }
}
