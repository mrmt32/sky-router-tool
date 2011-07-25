using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace pHMb.Router
{
    public enum ProcessRunState
    {
        UninterruptibleSleep,
        Running,
        InterruptibleSleep,
        Stopped,
        Other = 0
    }

    public enum ProcessPriority
    {
        HighPriority,
        Normal,
        LowPriority,
        Other = 0
    }

    public class RouterProcess
    {
        public int Pid;
        public string UserId;
        public int VmSize;
        public ProcessRunState RunState;
        public bool isPaging;
        public ProcessPriority Priority;
        public string CommandLine;
    }

    public class RouterProcessDetailed : RouterProcess
    {
        public string Name;
        public short SleepAverage;
        public int VmRSS;
        public int VmData;
        public int VmStk;
        public int VmExe;
        public int VmLib;
        public int Threads;
        public Dictionary<string, string> Environment;
    }
}
