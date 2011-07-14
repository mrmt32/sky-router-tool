using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace pHMb.pHHttp
{
    public interface ISSHandler
    {
        /// <summary>
        /// The name of the handler
        /// </summary>
        string Name { get; }

        /// <summary>
        /// The handeler author
        /// </summary>
        string Author { get; }

        /// <summary>
        /// The handler description
        /// </summary>
        string Description { get; }

        /// <summary>
        /// Handles http requests
        /// </summary>
        /// <param name="httpClient">The HttpClient object for the request</param>
        /// <param name="localPath">The local path to the file requested</param>
        void HandleRequest(HttpClient httpClient, string localPath);
    }
}
