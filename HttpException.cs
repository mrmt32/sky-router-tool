using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace pHMb.pHHttp
{
    public class HttpException : Exception
    {
        public HttpStatusCode StatusCode { get; private set; }

        public HttpRequest HttpRequest { get; private set; }

        public override string Message
        {
            get
            {
                return StatusCode.GetStringValue();
            }
        }

        public HttpException(HttpStatusCode stausCode)
        {
            StatusCode = stausCode;
        }

        public HttpException(HttpStatusCode stausCode, HttpRequest httpRequest)
        {
            StatusCode = stausCode;
            HttpRequest = httpRequest;
        }
    }
}
