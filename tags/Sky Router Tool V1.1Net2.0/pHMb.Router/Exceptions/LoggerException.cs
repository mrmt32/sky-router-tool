using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace pHMb.Router.Exceptions
{
    [global::System.Serializable]
    public class LoggerException : Exception
    {
        public string LoggerName { get; private set; }

        public LoggerException() { }
        public LoggerException(string message) : base(message) { }
        public LoggerException(string message, Exception inner) : base(message, inner) { }

        public LoggerException(string message, Exception inner, string loggerName) : base(message, inner) 
        {
            LoggerName = loggerName;
        }

        protected LoggerException(
          System.Runtime.Serialization.SerializationInfo info,
          System.Runtime.Serialization.StreamingContext context)
            : base(info, context) { }
    }
}
