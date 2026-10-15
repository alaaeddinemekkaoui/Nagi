using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace Pulse.Core;

public sealed record GpuSnapshot(double? Usage, string Name, string Status);
public sealed record TemperatureReading(string Name, double Celsius);
public sealed record TemperatureSnapshot(TemperatureReading[] Readings, string Status);
public sealed record FpsSnapshot(double? Fps, int ProcessId, string Status);
public sealed record ObsSnapshot(bool Connected, bool Recording, bool Streaming, double? RenderFps, string Status);

public static class IntegrationMath
{
    public static double? BusiestGpuEngine(IEnumerable<(string Engine, double Usage)> samples)
    {
        var groups = samples.Where(x => double.IsFinite(x.Usage) && x.Usage >= 0).GroupBy(x => x.Engine)
            .Select(g => Math.Clamp(g.Sum(x => x.Usage), 0, 100)).ToArray();
        return groups.Length == 0 ? null : groups.Max();
    }
    public static string ObsAuthentication(string password, string salt, string challenge)
    {
        var secret = Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(password + salt)));
        return Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(secret + challenge)));
    }
    public static double? FpsFromCsv(IEnumerable<string> source, int processId)
    {
        using var lines = source.GetEnumerator(); if (!lines.MoveNext()) return null;
        var header = Csv(lines.Current); var pid = Array.IndexOf(header, "ProcessID");
        var interval = Array.IndexOf(header, "MsBetweenPresents"); if (interval < 0) interval = Array.IndexOf(header, "MsBetweenAppStart");
        var swap = Array.IndexOf(header, "SwapChainAddress");
        if (pid < 0 || interval < 0) return null;
        var groups = new Dictionary<string, List<double>>(); var count = 0;
        while (count++ < 100000 && lines.MoveNext())
        {
            var cells = Csv(lines.Current); if (cells.Length <= Math.Max(pid, interval) || cells[pid] != processId.ToString(CultureInfo.InvariantCulture)) continue;
            if (!double.TryParse(cells[interval], NumberStyles.Float, CultureInfo.InvariantCulture, out var ms) || !double.IsFinite(ms) || ms <= 0 || ms > 10000) continue;
            var key = swap >= 0 && swap < cells.Length ? cells[swap] : "default";
            if (!groups.TryGetValue(key, out var values)) groups[key] = values = [];
            values.Add(ms);
        }
        var dominant = groups.Values.OrderByDescending(g => g.Count).FirstOrDefault();
        return dominant is { Count: >= 2 } ? 1000 / dominant.Average() : null;
    }
    private static string[] Csv(string text)
    {
        var result = new List<string>(); var cell = new StringBuilder(); var quoted = false;
        for (var i = 0; i < text.Length; i++)
        {
            if (text[i] == '"') { if (quoted && i + 1 < text.Length && text[i + 1] == '"') { cell.Append('"'); i++; } else quoted = !quoted; }
            else if (text[i] == ',' && !quoted) { result.Add(cell.ToString().Trim()); cell.Clear(); } else cell.Append(text[i]);
        }
        result.Add(cell.ToString().Trim()); return result.ToArray();
    }
}
