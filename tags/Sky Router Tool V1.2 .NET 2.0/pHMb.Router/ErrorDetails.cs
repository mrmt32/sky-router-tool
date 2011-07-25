using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace pHMb.Router
{
    public struct Error
    {
        public int Crc;
        public int Los;
        public int Lof;
        public int Es;
    }

    public struct ErrorDetails
    {
        public Error Total;
        public Error LatestDay;
        public Error Current;
        public Error M30Min;
        public Error M45Min;
        public Error M60Min;
    }
}
