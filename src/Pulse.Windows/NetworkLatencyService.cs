using System.Net.NetworkInformation;

namespace Pulse.Windows;

/// <summary>Bounded ICMP latency, cached for fifteen seconds. Unreachable hosts have no value.</summary>
public sealed class NetworkLatencyService(string host = "1.1.1.1")
{
    private long _nextSample;
    private double? _milliseconds;
    public async Task<double?> SampleAsync(CancellationToken token)
    {
        if (Environment.TickCount64 < _nextSample) return _milliseconds;
        _nextSample = Environment.TickCount64 + 15000;
        try
        {
            using var ping = new Ping();
            var reply = await ping.SendPingAsync(host, TimeSpan.FromMilliseconds(800), cancellationToken: token);
            _milliseconds = reply.Status == IPStatus.Success ? reply.RoundtripTime : null;
        }
        catch (Exception ex) when (ex is PingException or System.Net.Sockets.SocketException or ArgumentException or InvalidOperationException)
        { _milliseconds = null; }
        return _milliseconds;
    }
}
