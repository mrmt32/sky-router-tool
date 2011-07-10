using System;
namespace pHMb.Router.Interfaces
{
    public interface IRouterInterface
    {
        RouterProcessDetailed GetDetailedProcessInfo(int pid);
        string GetPing(string hostname);
        bool KillProcess(int pid);
        double PerformSpeedTest(string url, double size);
        void Reboot();
        void Resync();
        IRouterData RouterInfo { get; }
        void SetTargetSnrm(double targetSnrm);
        byte[] GetFirmware();
        string FlashFirmware(byte[] firmware);
        void Update();
    }
}
