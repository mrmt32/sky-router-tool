using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;

namespace pHMb.pHHttp.SSHandlers
{
    public class DefaultHandler : ISSHandler
    {
        #region ISSHandler Members

        public string Name
        {
            get { return "Default Handler"; }
        }

        public string Author
        {
            get { return "mrmt32"; }
        }

        public string Description
        {
            get { return "Just sends the file requested"; }
        }

        public void HandleRequest(HttpClient httpClient, string localPath)
        {
            if (File.Exists(localPath))
            {
                Dictionary<string, string> headers = httpClient.GetStandardHeaders();

                headers.Add("Content-Type", httpClient.GetMimeType(localPath));

                httpClient.SendResponseStatus(HttpStatusCode.OK);
                httpClient.SendResponseHeader(headers);
                byte[] fileCont = File.ReadAllBytes(localPath);
                httpClient.SendResponseBody(fileCont);
            }
            else
            {
                httpClient.SendError(HttpStatusCode.Not_Found);
            }
        }
        #endregion
    }
}
