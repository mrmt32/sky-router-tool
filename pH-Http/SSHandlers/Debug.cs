using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace pHMb.pHHttp.SSHandlers
{
    public class Debug : ISSHandler
    {
        #region ISSHandler Members

        public string Name
        {
            get { return "Debug"; }
        }

        public string Author
        {
            get { return "mrmt32"; }
        }

        public string Description
        {
            get { return "Prints out various debug variables"; }
        }

        public void HandleRequest(HttpClient httpClient, string localPath)
        {
            httpClient.SendResponseStatus(HttpStatusCode.OK);
            httpClient.SendResponseHeader(httpClient.GetStandardHeaders());

            string body =     "<b>Path: </b>" + localPath
                            + "<br/><b>Query String: </b>" + httpClient.Request.Uri.Query
                            + "<br/><b>Virtual Path: </b>" + httpClient.Request.Uri.LocalPath
                            + "<br/><b>Url: </b>" + httpClient.Request.Uri.AbsoluteUri
                            + "<br/><b>Post Data: </b>" + (httpClient.Request.Method == "POST" ? Encoding.ASCII.GetString(httpClient.Request.PostData) : "");

            httpClient.SendResponseBody(body);
        }

        #endregion
    }
}
