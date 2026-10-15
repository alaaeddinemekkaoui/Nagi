namespace Pulse.Core;

public sealed record PowerSnapshot(int? Percent, bool? HasBattery, bool? Charging, bool? PluggedIn)
{
    public static PowerSnapshot Unknown { get; } = new(null, null, null, null);
    public static PowerSnapshot FromWindows(byte acLine, byte flags, byte percent)
    {
        bool? present = flags == 255 ? null : (flags & 128) == 0;
        return new(present != false && percent <= 100 ? percent : null, present,
            flags == 255 || present == false ? null : (flags & 8) != 0,
            acLine == 1 ? true : acLine == 0 ? false : null);
    }
    public string CompactText => Percent is { } percent ? $"{percent}%" : HasBattery == false ? "AC" : "—";
    public string Detail => HasBattery == false ? "No system battery detected" : Percent is { } percent
        ? $"Battery {percent}% · " + (Charging == true ? "Charging" : PluggedIn == true ? "Plugged in" : PluggedIn == false ? "On battery" : "Power source unavailable")
        : "Battery percentage unavailable";
}
