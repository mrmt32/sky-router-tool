using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace pHMb.Router.RouterCommandSets
{
    class FAST2504N : FAST2504_http
    {
        public FAST2504N(string username, string password, string host, int httpServerPort, string httpServerUsername, string httpServerPassword, bool skyCompatibilityMode)
            : base(username, password, host, httpServerPort, httpServerUsername, httpServerPassword, skyCompatibilityMode) 
        {
        }

        protected override ConnectionDetails GetConnectionDetails()
        {
            ConnectionDetails details = new ConnectionDetails();
            details.DownstreamSync = new SyncDetails();
            details.UpstreamSync = new SyncDetails();

            // Get connection details from telnet
            string conDetailsText = _routerConnection.SendCommand("adslctl info --stats");

            string telnetTest = Regex.Match(conDetailsText, "([^\r]*)(\r?)\n").Groups[2].Value;

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
                    Match snrMatch = Regex.Match(conDetailsText, "SNR \\(dB\\):\t ?(-?[0-9]*\\.[0-9]*)\t\t ?(-?[0-9]*\\.[0-9]*)\n");
                    details.DownstreamSync.SnrMargin = decimal.Parse(snrMatch.Groups[1].Value);
                    details.UpstreamSync.SnrMargin = decimal.Parse(snrMatch.Groups[2].Value);

                    // Parse Attenuations
                    Match attnMatch = Regex.Match(conDetailsText, "Attn\\(dB\\):\t ?(-?[0-9]*\\.[0-9]*)\t\t ?(-?[0-9]*\\.[0-9]*)\n");
                    details.DownstreamSync.Attenuation = decimal.Parse(attnMatch.Groups[1].Value);
                    details.UpstreamSync.Attenuation = decimal.Parse(attnMatch.Groups[2].Value);

                    // Parse Power
                    Match powerMatch = Regex.Match(conDetailsText, "Pwr\\(dBm\\):\t ?(-?[0-9]*\\.[0-9]*)\t\t ?(-?[0-9]*\\.[0-9]*)\n");
                    details.DownstreamSync.Power = decimal.Parse(powerMatch.Groups[1].Value);
                    details.UpstreamSync.Power = decimal.Parse(powerMatch.Groups[2].Value);

                    // Parse max attainable rate (this is predicted by the router)
                    Match maxRateMatch = Regex.Match(conDetailsText, "Max:\tUpstream rate = ([0-9]*) Kbps, Downstream rate = ([0-9]*) Kbps\n");
                    details.DownstreamSync.MaxAttainableRate = int.Parse(maxRateMatch.Groups[2].Value);
                    details.UpstreamSync.MaxAttainableRate = int.Parse(maxRateMatch.Groups[1].Value);

                    // Parse sync rate
                    Match syncRateMatch = Regex.Match(conDetailsText, "Bearer:	[0-9], Upstream rate = ([0-9]*) Kbps, Downstream rate = ([0-9]*) Kbps\n");
                    details.DownstreamSync.SyncRate = int.Parse(syncRateMatch.Groups[2].Value);
                    details.UpstreamSync.SyncRate = int.Parse(syncRateMatch.Groups[1].Value);

                    // Parse Errors
                    Match errorsMatch = Regex.Match(conDetailsText, "FEC:\t\t([0-9]*)\t\t([0-9]*)\n" +
                                                                    "CRC:\t\t([0-9]*)\t\t([0-9]*)\n" + 
                                                                    "ES:\t\t([0-9]*)\t\t([0-9]*)\n" +
                                                                    "SES:\t\t([0-9]*)\t\t([0-9]*)\n" +
                                                                    "UAS:\t\t([0-9]*)\t\t([0-9]*)\n" +
                                                                    "LOS:\t\t([0-9]*)\t\t([0-9]*)\n" +
                                                                    "LOF:\t\t([0-9]*)\t\t([0-9]*)\n" +
                                                                    "LOM:\t\t([0-9]*)\t\t([0-9]*)\n");

                    // Total errors:
                    details.Errors.Total.Crc = int.Parse(errorsMatch.Groups[3].Value);
                    details.Errors.Total.Los = int.Parse(errorsMatch.Groups[11].Value);
                    details.Errors.Total.Lof = int.Parse(errorsMatch.Groups[13].Value);
                    details.Errors.Total.Es = int.Parse(errorsMatch.Groups[5].Value);

                    // Current errors:
                    errorsMatch = errorsMatch.NextMatch();
                    details.Errors.Current.Crc = int.Parse(errorsMatch.Groups[3].Value);
                    details.Errors.Current.Los = int.Parse(errorsMatch.Groups[11].Value);
                    details.Errors.Current.Lof = int.Parse(errorsMatch.Groups[13].Value);
                    details.Errors.Current.Es = int.Parse(errorsMatch.Groups[5].Value);

                    // -30min to -15min
                    errorsMatch = errorsMatch.NextMatch();
                    details.Errors.M30Min.Crc = int.Parse(errorsMatch.Groups[3].Value);
                    details.Errors.M30Min.Los = int.Parse(errorsMatch.Groups[11].Value);
                    details.Errors.M30Min.Lof = int.Parse(errorsMatch.Groups[13].Value);
                    details.Errors.M30Min.Es = int.Parse(errorsMatch.Groups[5].Value);

                    // -45min to -30min
                    errorsMatch = errorsMatch.NextMatch();
                    details.Errors.M45Min.Crc = int.Parse(errorsMatch.Groups[3].Value);
                    details.Errors.M45Min.Los = int.Parse(errorsMatch.Groups[11].Value);
                    details.Errors.M45Min.Lof = int.Parse(errorsMatch.Groups[13].Value);
                    details.Errors.M45Min.Es = int.Parse(errorsMatch.Groups[5].Value);

                    // -60min to -45min
                    errorsMatch = errorsMatch.NextMatch();
                    details.Errors.M60Min.Crc = int.Parse(errorsMatch.Groups[3].Value);
                    details.Errors.M60Min.Los = int.Parse(errorsMatch.Groups[11].Value);
                    details.Errors.M60Min.Lof = int.Parse(errorsMatch.Groups[13].Value);
                    details.Errors.M60Min.Es = int.Parse(errorsMatch.Groups[5].Value);

                    // Errors in last day:
                    errorsMatch = errorsMatch.NextMatch();
                    details.Errors.LatestDay.Crc = int.Parse(errorsMatch.Groups[3].Value);
                    details.Errors.LatestDay.Los = int.Parse(errorsMatch.Groups[11].Value);
                    details.Errors.LatestDay.Lof = int.Parse(errorsMatch.Groups[13].Value);
                    details.Errors.LatestDay.Es = int.Parse(errorsMatch.Groups[5].Value);
                }
                catch (FormatException)
                {

                }
            }
            return details;

        }
    }
}
