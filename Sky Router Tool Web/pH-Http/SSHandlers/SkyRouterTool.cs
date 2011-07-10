using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using pHMb.Router;

namespace pHMb.pHHttp.SSHandlers
{
    public class SkyRouterTool : ISSHandler
    {
        #region ISSHandler Members
        public string Name
        {
            get { return "Sky Router Tool Interface"; }
        }

        public string Author
        {
            get { return "mrmt32"; }
        }

        public string Description
        {
            get { return "Provides a JSON interface to access the DG834GT router"; }
        }

        public void HandleRequest(HttpClient httpClient, string localPath)
        {
            Dictionary<string, string> getRequestValues = httpClient.DecodeQueryString(httpClient.Request, false);
            Dictionary<string, string> postData = httpClient.DecodeQueryString(httpClient.Request, true);
           
            string address;
            string sizeString;
            double size;
            string action;
            string pidString;
            int pid;

            if (getRequestValues.TryGetValue("action", out action))
            {
                switch (action)
                {
                    case "get_value":
                        string type;
                        string name;

                        if (getRequestValues.TryGetValue("type", out type) && getRequestValues.TryGetValue("name", out name))
                        {
                            GetValue(type, name, getRequestValues, httpClient);
                        }
                        else
                        {
                            SendError("Invalid request: Both `type` and `name` values must be set.", httpClient);
                        }
                        break;

                    case "set_settings":
                        try
                        {
                            if (_settingsChanged(postData))
                            {
                                SendJsonOutput(_getSettings(postData.Keys.ToList<string>()), httpClient);
                            }
                            else
                            {
                                SendError("Error changing settings", httpClient);
                            }
                        }
                        catch (Exception ex)
                        {
                            SendError("Error changing settings: " + ex.ToString(), httpClient);
                        }
                        break;

                    case "get_settings":
                        SendJsonOutput(_getSettings(new List<string>(getRequestValues["settings"].Split(';'))), httpClient);
                        break;

                    case "get_consolidation_rules":
                        SendJsonOutput(_routerPoll.ConsolidationPeriods, httpClient);
                        break;

                    case "set_consolidation_rules":
                        try
                        {
                            if (postData.ContainsKey("update") && postData["update"] != "")
                            {
                                foreach (string consolidationUpdateString in postData["update"].Split(';'))
                                {
                                    string[] consolidationUpdateArray = consolidationUpdateString.Split(':');

                                    _routerPoll.UpdateConsolidationRule(new ConsolidationPeriod()
                                    {
                                        Period = new TimeSpan(0, 0, 0, int.Parse(consolidationUpdateArray[0])),
                                        Resolution = new TimeSpan(0, 0, 0, int.Parse(consolidationUpdateArray[1])),
                                        Id = (consolidationUpdateArray.Length == 3 ? int.Parse(consolidationUpdateArray[2]) : 0)
                                    });
                                }
                            }

                            if (postData.ContainsKey("delete") && postData["delete"] != "")
                            {
                                foreach (string consolidationDeleteId in postData["delete"].Split(';'))
                                {
                                    _routerPoll.RemoveConsolidationRule(int.Parse(consolidationDeleteId));
                                }
                            }
                            // Restart polling (to enact changes)
                            _routerPoll.Stop();
                            _routerPoll.Start();

                            SendJsonOutput(_routerPoll.ConsolidationPeriods, httpClient);
                        }
                        catch (Exception ex)
                        {
                            _routerPoll.Stop();
                            _routerPoll.Start();
                            SendError("Error updating consolidation rules: " + ex.ToString(), httpClient);
                        }
                        break;

                    case "reboot":
                        _routerCommand.Reboot();
                        SendJsonOutput("OK", httpClient);
                        break;

                    case "resync":
                        try
                        {
                            _routerCommand.Resync();
                            SendJsonOutput("OK", httpClient);
                        }
                        catch (Exception ex)
                        {
                            SendError("Unhandled Exception: " + ex.ToString(), httpClient);
                        }
                        break;

                    case "set_target_snrm":
                        try
                        {
                            string targetSnrmString;
                            double targetSnrm;
                            if (postData.TryGetValue("target_snrm", out targetSnrmString) && double.TryParse(targetSnrmString, out targetSnrm))
                            {
                                if (targetSnrm > 0 && targetSnrm < 500)
                                {
                                    _routerCommand.SetTargetSnrm(targetSnrm);
                                    SendJsonOutput("OK", httpClient);
                                }
                                else
                                {
                                    SendError("Invalid target SNRM: Value out of range.", httpClient);
                                }
                            }
                            else
                            {
                                SendError("Invalid target SNRM: Value not a number.", httpClient);
                            }
                        }
                        catch (Exception ex)
                        {
                            SendError("Unhandled Exception: " + ex.ToString(), httpClient);
                        }
                        break;

                    case "ping":
                        if (postData.TryGetValue("address", out address))
                        {
                            SendJsonOutput(_routerCommand.GetPing(address),httpClient);
                        }
                        else
                        {
                            SendError("IP Address not specified.", httpClient);
                        }
                        break;
                    
                    case "kill_process":
                        if (postData.TryGetValue("pid", out pidString) && int.TryParse(pidString, out pid))
                        {
                            if (_routerCommand.KillProcess(pid))
                            {
                                SendJsonOutput("OK", httpClient);
                            }
                            else
                            {
                                SendError("Killing process failed, maybe it no longer exists?", httpClient);
                            }
                        }
                        else
                        {
                            SendError("Invalid process id.", httpClient);
                        }
                        break;

                    case "speed_test":
                        if (postData.TryGetValue("address", out address) &&
                            postData.TryGetValue("size", out sizeString) && double.TryParse(sizeString, out size))
                        {
                            SendJsonOutput(new SpeedTestResult() { Speed = _routerCommand.PerformSpeedTest(address, size), Address = address }, httpClient);
                        }
                        else
                        {
                            SendError("Address or size not specified.", httpClient);
                        }
                        break;

                    case "calculate_defaults":
                        string lanMacAddress;
                        if(postData.TryGetValue("lanMacAddress", out lanMacAddress) && lanMacAddress.Replace(":", "").Length == 12)
                        {
                            string serial;
                            if (postData.TryGetValue("serial", out serial))
                            {
                                SendJsonOutput(SkyPasswordGen.GetInformation(lanMacAddress.Replace(":", ""), serial), httpClient);
                            }
                            else
                            {
                                SendJsonOutput(SkyPasswordGen.GetInformation(lanMacAddress.Replace(":", "")), httpClient);
                            }
                        }
                        else
                        {
                            SendError("LAN MAC address was not specified or is invalid.", httpClient);
                        }
                        break;

                    case "command":
                        try
                        {
                            string commandString;
                            if (postData.TryGetValue("command", out commandString))
                            {
                                SendJsonOutput(_routerConnection.SendCommand(commandString), httpClient);
                            }
                            else
                            {
                                SendError("Invalid post data, command not specified.", httpClient);
                            }
                        }
                        catch (Exception ex)
                        {
                            SendError("Unhandled Exception: " + ex.ToString(), httpClient);
                        }
                        break;

                    case "get_firmware":
                        try
                        {
                            byte[] firmware = _routerCommand.GetFirmware();
                            httpClient.SendResponseStatus(HttpStatusCode.OK);

                            Dictionary<string, string> headers = httpClient.GetStandardHeaders();
                            headers.Add("Content-Type", "application/octet-stream");
                            headers.Add("Content-disposition", "attachment; filename=firmware.img");

                            httpClient.SendResponseHeader(headers);
                            httpClient.SendResponseBody(firmware);
                        }
                        catch (Exception ex)
                        {
                            httpClient.SendResponseStatus(HttpStatusCode.Internal_Server_Error);
                            httpClient.SendResponseHeader(httpClient.GetStandardHeaders());
                            httpClient.SendResponseBody("Internal Server Error: " + ex.ToString());
                        }
                        break;

                    default:
                        SendError("Invalid request: Unknown action command.", httpClient);
                        break;
                }
            }
            else
            {
                SendError("Invalid request: An action must be supplied.", httpClient);
            }
        }
        #endregion

        #region Private Variables
        private RouterPoll _routerPoll;
        private RouterHttp _routerConnection;
        private pHMb.Router.Interfaces.IRouterInterface _routerCommand;

        private GetSettingsDelegate _getSettings;
        private SettingChangeDelegate _settingsChanged;
        #endregion

        #region Private Data Types
        private class JsonOutput
        {
            public bool isError;
            public string ErrorString;
            public object ReturnData;
        }

        private class SpeedTestResult
        {
            public string Address;
            public double Speed;
        }
        #endregion

        #region Private Methods
        private void SendError(string errorString, HttpClient httpClient)
        {
            SendJson(new JsonOutput() { isError = true, ErrorString = errorString, ReturnData = null }, httpClient);
        }

        private void SendJsonOutput(object objectToSend, HttpClient httpClient)
        {
            SendJson(new JsonOutput() { isError = false, ReturnData = objectToSend }, httpClient);
        }

        private void SendJson(object objectToSend, HttpClient httpClient)
        {
            httpClient.SendResponseStatus(HttpStatusCode.OK);

            Dictionary<string, string> headers = httpClient.GetStandardHeaders();
            headers.Add("Content-Type", "application/json");

            httpClient.SendResponseHeader(headers);
            httpClient.SendResponseBody(JsonConvert.SerializeObject(objectToSend, new JavaScriptDateTimeConverter(), new StringEnumConverter(), new JavascriptTimespanConverter()));
        }

        private void GetValue(string type, string name, Dictionary<string, string> getRequestValues, HttpClient httpClient)
        {
            string startTime;
            string endTime;

            switch (type)
            {
                case "current":
                    // These are current values taken directly from the router
                    GetCurrentValue(name, httpClient);
                    break;

                case "log":
                    // These are logged values logged by RouterPoll
                    if (getRequestValues.TryGetValue("startTime", out startTime) && getRequestValues.TryGetValue("endTime", out endTime))
                    {
                        SendJsonOutput(_routerPoll.RetrieveAsObject(name, UnixToDate(double.Parse(startTime)), UnixToDate(double.Parse(endTime))), httpClient);
                    }
                    else
                    {
                        SendError("Invalid request: Start time or end time not provided", httpClient);
                    }
                    break;

                case "totals":
                    // These are aggregate values from data logged by RouterPoll
                    if (getRequestValues.TryGetValue("startTime", out startTime) && getRequestValues.TryGetValue("endTime", out endTime))
                    {
                        SendJsonOutput(_routerPoll.GetAggregateAsObject(name, UnixToDate(double.Parse(startTime)), UnixToDate(double.Parse(endTime))), httpClient);
                    }
                    else
                    {
                        SendError("Invalid request: Start time or end time not provided", httpClient);
                    }
                    break;

                default:
                    SendError(string.Format("Invalid request: Unkown value type '{0}'.", type), httpClient);
                    break;
            }
        }

        private void GetCurrentValue(string name, HttpClient httpClient)
        {
            try
            {
                string[] names = name.Split(';');
                Dictionary<string, object> output = new Dictionary<string, object>();

                _routerCommand.Update();

                foreach (string oneName in names)
                {
                    string[] oneNameArray = oneName.Split(':');
                    switch (oneNameArray[0])
                    {
                        case "connectionDetails":
                            output.Add(oneName, _routerCommand.RouterInfo.ConnectionDetails);
                            break;

                        case "uptime":
                            output.Add(oneName, _routerCommand.RouterInfo.Uptime);
                            break;

                        case "processList":
                            output.Add(oneName, _routerCommand.RouterInfo.ProcessList);
                            break;

                        case "detailedProcessInfo":
                            output.Add(oneName, _routerCommand.GetDetailedProcessInfo(int.Parse(oneNameArray[1])));
                            break;

                        case "bitLoading":
                            output.Add(oneName, _routerCommand.RouterInfo.BitLoading);
                            break;

                        case "qln":
                            output.Add(oneName, _routerCommand.RouterInfo.Qln);
                            break;

                        case "snr":
                            output.Add(oneName, _routerCommand.RouterInfo.Snr);
                            break;

                        case "hlog":
                            output.Add(oneName, _routerCommand.RouterInfo.Hlog);
                            break;

                        case "lanMacAddress":
                            output.Add(oneName, _routerCommand.RouterInfo.LanMacAddress);
                            break;

                        default:

                            break;
                    }
                }
                SendJsonOutput(output, httpClient);
            }
            catch (Exception ex)
            {
                SendError("Exception occured: " + ex.ToString(), httpClient);
            }
        }

 
        private DateTime UnixToDate(double timestamp)
        {
            DateTime converted = new DateTime(1970, 1, 1, 0, 0, 0, 0);

            converted = converted.AddSeconds(timestamp);

            return converted.ToLocalTime();
        } 
        #endregion

        #region Public Delegates
        public delegate bool SettingChangeDelegate(Dictionary<string, string> changedSettings);
        public delegate Dictionary<string, string> GetSettingsDelegate(List<string> settings);
        #endregion

        #region Public Methods
        public SkyRouterTool(RouterPoll routerPoll, RouterHttp routerConnection, GetSettingsDelegate getSettings, SettingChangeDelegate settingsChanged, pHMb.Router.Interfaces.IRouterInterface routerCommand)
        {
            _settingsChanged = settingsChanged;
            _getSettings = getSettings;
            _routerConnection = routerConnection;
            _routerPoll = routerPoll;
            _routerCommand = routerCommand;
        } 
        #endregion
    }

    public class JavascriptTimespanConverter : JsonConverter
    {
        public override bool CanConvert(Type objectType)
        {
            return objectType.IsAssignableFrom(typeof(TimeSpan));
        }

        public override object ReadJson(JsonReader reader, Type objectType, JsonSerializer serializer)
        {
            throw new NotImplementedException();
        }

        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            serializer.Serialize(writer, ((TimeSpan)value).TotalMilliseconds);
        }
    }

}
