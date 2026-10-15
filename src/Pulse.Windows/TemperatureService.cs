using System.Management;
using Pulse.Core;

namespace Pulse.Windows;

// Read an existing sensor provider. Pulse never installs or loads a kernel driver.
public sealed class TemperatureService
{
    private long _nextRead;
    private TemperatureSnapshot _last = new([], "Connect Libre Hardware Monitor or Open Hardware Monitor (WMI)");
    public TemperatureSnapshot Sample()
    {
        if (Environment.TickCount64 < _nextRead) return _last;
        _nextRead = Environment.TickCount64 + 5000;
        foreach (var provider in new[] { "LibreHardwareMonitor", "OpenHardwareMonitor" })
        {
            try
            {
                var scope = new ManagementScope(@"\\.\root\" + provider);
                using var search = new ManagementObjectSearcher(scope, new ObjectQuery("SELECT Name,Value,Identifier,Parent FROM Sensor WHERE SensorType='Temperature'"), new System.Management.EnumerationOptions { Timeout = TimeSpan.FromSeconds(2) });
                using var sensors = search.Get(); var readings = new List<TemperatureReading>();
                foreach (var sensor in sensors.Cast<ManagementBaseObject>())
                {
                    using (sensor)
                    {
                        var name = sensor["Name"]?.ToString() ?? "Temperature";
                        var parent = sensor["Parent"]?.ToString() ?? "";
                        if (!double.TryParse(Convert.ToString(sensor["Value"], System.Globalization.CultureInfo.InvariantCulture), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var value)) continue;
                        if (!double.IsFinite(value) || value < -40 || value > 150) continue;
                        var kind = parent.Contains("cpu", StringComparison.OrdinalIgnoreCase) ? "CPU · " : parent.Contains("gpu", StringComparison.OrdinalIgnoreCase) ? "GPU · " : "Sensor · ";
                        readings.Add(new(kind + name, value)); if (readings.Count >= 24) break;
                    }
                }
                if (readings.Count > 0) return _last = new(readings.ToArray(), provider + " WMI sensors");
            }
            catch (Exception ex) when (ex is ManagementException or UnauthorizedAccessException or System.Runtime.InteropServices.COMException) { }
        }
        _nextRead = Environment.TickCount64 + 30000;
        return _last = new([], "No temperature sensor provider · connect Libre Hardware Monitor WMI");
    }
}
