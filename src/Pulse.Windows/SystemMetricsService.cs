using System.Diagnostics;
using System.Net.NetworkInformation;
using System.Runtime.InteropServices;
using Pulse.Core;

namespace Pulse.Windows;

public sealed class SystemMetricsService : ISystemMetricsService
{
    private long _idle, _total;
    private bool _primed;
    private long _lastTimestamp;
    private Dictionary<string, (long Received, long Sent)> _network = new();

    public SystemSnapshot Sample()
    {
        double? cpu = null, receive = null, send = null;
        string? error = null;
        if (Native.GetSystemTimes(out var idle, out var kernel, out var user))
        {
            var total = kernel + user;
            if (_primed && total > _total) cpu = Math.Clamp(100d * (1 - (double)(idle - _idle) / (total - _total)), 0, 100);
            _idle = idle; _total = total; _primed = true;
        }
        else error = "CPU counter unavailable";
        var memory = new Native.MemoryStatus { Length = (uint)Marshal.SizeOf<Native.MemoryStatus>() };
        if (!Native.GlobalMemoryStatusEx(ref memory)) error = "Memory information unavailable";
        try
        {
            var now = Stopwatch.GetTimestamp();
            var current = new Dictionary<string, (long Received, long Sent)>();
            long received = 0, sent = 0; var matched = false;
            foreach (var nic in NetworkInterface.GetAllNetworkInterfaces().Where(n => n.OperationalStatus == OperationalStatus.Up
                         && n.NetworkInterfaceType is not NetworkInterfaceType.Loopback and not NetworkInterfaceType.Tunnel))
            {
                var stats = nic.GetIPStatistics();
                current[nic.Id] = (stats.BytesReceived, stats.BytesSent);
                if (_network.TryGetValue(nic.Id, out var previous))
                {
                    received += Math.Max(0, stats.BytesReceived - previous.Received);
                    sent += Math.Max(0, stats.BytesSent - previous.Sent);
                    matched = true;
                }
            }
            if (matched && _lastTimestamp != 0)
            {
                var seconds = Stopwatch.GetElapsedTime(_lastTimestamp, now).TotalSeconds;
                if (seconds > 0) { receive = received / seconds; send = sent / seconds; }
            }
            _network = current; _lastTimestamp = now;
        }
        catch (NetworkInformationException) { }
        var power = Native.GetSystemPowerStatus(out var status)
            ? PowerSnapshot.FromWindows(status.ACLineStatus, status.BatteryFlag, status.BatteryLifePercent) : PowerSnapshot.Unknown;
        return new(cpu, memory.TotalPhysical, memory.AvailablePhysical, receive, send, error, power);
    }
}
