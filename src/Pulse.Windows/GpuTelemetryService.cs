using System.Management;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using Pulse.Core;

namespace Pulse.Windows;

public sealed class GpuTelemetryService : IDisposable
{
    private nint _query, _counter;
    private bool _primed, _disposed;
    private long _retry;
    private string _name = "GPU";
    private bool _nameRead;
    public GpuSnapshot Sample()
    {
        if (_disposed) return new(null, _name, "Stopped");
        try
        {
            if (!_nameRead)
            {
                _nameRead = true;
                try
                {
                    using var search = new ManagementObjectSearcher("SELECT Name FROM Win32_VideoController");
                    search.Options.Timeout = TimeSpan.FromSeconds(2);
                    using var names = search.Get();
                    _name = string.Join(" / ", names.Cast<ManagementBaseObject>().Select(x => x["Name"]?.ToString()).Where(x => !string.IsNullOrWhiteSpace(x)));
                    if (_name.Length == 0) _name = "GPU";
                }
                catch (ManagementException) { }
            }
            if (_query == 0)
            {
                if (Environment.TickCount64 < _retry) return new(null, _name, "Windows GPU counters unavailable");
                _retry = Environment.TickCount64 + 30000;
                if (PdhOpenQuery(null, 0, out _query) != 0 || PdhAddEnglishCounter(_query, @"\GPU Engine(*)\Utilization Percentage", 0, out _counter) != 0)
                { Reset(); return new(null, _name, "Windows GPU counters unavailable"); }
            }
            if (PdhCollectQueryData(_query) != 0) { Reset(); return new(null, _name, "GPU counter collection failed"); }
            if (!_primed) { _primed = true; return new(null, _name, "Sampling Windows GPU engines…"); }
            uint size = 0, count = 0;
            var code = PdhGetFormattedCounterArray(_counter, 0x200 | 0x8000, ref size, ref count, 0);
            if (code != 0x800007D2 || size == 0 || size > 8 * 1024 * 1024) return new(null, _name, "No GPU engine samples");
            var memory = Marshal.AllocHGlobal((int)size);
            try
            {
                if (PdhGetFormattedCounterArray(_counter, 0x200 | 0x8000, ref size, ref count, memory) != 0) return new(null, _name, "GPU engines changed; retrying");
                var stride = Marshal.SizeOf<CounterItem>(); var values = new List<(string, double)>();
                for (var i = 0; i < count && (i + 1L) * stride <= size; i++)
                {
                    var item = Marshal.PtrToStructure<CounterItem>(memory + i * stride);
                    if (item.Value.Status > 1) continue;
                    var name = Marshal.PtrToStringUni(item.Name) ?? "";
                    var match = Regex.Match(name, @"^pid_\d+_(luid_.+?_phys_\d+_eng_\d+)_", RegexOptions.CultureInvariant);
                    if (match.Success) values.Add((match.Groups[1].Value, item.Value.Value));
                }
                var usage = IntegrationMath.BusiestGpuEngine(values);
                return new(usage, _name, usage.HasValue ? "Windows GPU engine counters · busiest engine" : "No GPU engine samples");
            }
            finally { Marshal.FreeHGlobal(memory); }
        }
        catch (Exception ex) when (ex is ManagementException or COMException or UnauthorizedAccessException)
        { Reset(); return new(null, _name, "GPU counters unavailable for this driver"); }
    }
    private void Reset() { if (_query != 0) PdhCloseQuery(_query); _query = _counter = 0; _primed = false; }
    public void Dispose() { _disposed = true; Reset(); }
    [StructLayout(LayoutKind.Sequential)] private struct CounterValue { public uint Status; public double Value; }
    [StructLayout(LayoutKind.Sequential)] private struct CounterItem { public nint Name; public CounterValue Value; }
    [DllImport("pdh.dll", CharSet = CharSet.Unicode, EntryPoint = "PdhOpenQueryW")] private static extern uint PdhOpenQuery(string? source, nint user, out nint query);
    [DllImport("pdh.dll", CharSet = CharSet.Unicode, EntryPoint = "PdhAddEnglishCounterW")] private static extern uint PdhAddEnglishCounter(nint query, string path, nint user, out nint counter);
    [DllImport("pdh.dll")] private static extern uint PdhCollectQueryData(nint query);
    [DllImport("pdh.dll", CharSet = CharSet.Unicode, EntryPoint = "PdhGetFormattedCounterArrayW")] private static extern uint PdhGetFormattedCounterArray(nint counter, uint format, ref uint bytes, ref uint count, nint data);
    [DllImport("pdh.dll")] private static extern uint PdhCloseQuery(nint query);
}
