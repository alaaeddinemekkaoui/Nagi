namespace Pulse.Core;

/// <summary>Time-based attack/release smoothing of a measured peak; never generates activity.</summary>
public sealed class AudioEnvelope
{
    public double Value { get; private set; }
    public double Update(double? peak, double seconds)
    {
        var target = peak is { } p && double.IsFinite(p) ? Math.Clamp(p, 0, 1) : 0;
        var elapsed = double.IsFinite(seconds) ? Math.Clamp(seconds, 0, 1) : 0;
        var tau = target > Value ? .035 : .16;
        Value += (target - Value) * (1 - Math.Exp(-elapsed / tau));
        if (Value < .001) Value = 0;
        return Value;
    }
}
