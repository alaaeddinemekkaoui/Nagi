namespace Pulse.Core.Motion;

/// <summary>A critically damped spring that preserves velocity when its destination changes.</summary>
public sealed class SpringValue(double initial, double tolerance = .2)
{
    private readonly double _tolerance = double.IsFinite(tolerance) && tolerance > 0 ? tolerance : .2;
    public double Value { get; private set; } = initial;
    public double Velocity { get; private set; }
    public double Target { get; set; } = initial;
    public bool IsSettled => Math.Abs(Value - Target) < _tolerance && Math.Abs(Velocity) < _tolerance * 10;
    public void Snap(double value) { Value = Target = value; Velocity = 0; }
    public void Advance(double seconds, double frequency = 22)
    {
        if (!double.IsFinite(seconds) || seconds <= 0) return;
        var displacement = Value - Target;
        var impulse = Velocity + frequency * displacement;
        var decay = Math.Exp(-frequency * seconds);
        Value = Target + (displacement + impulse * seconds) * decay;
        Velocity = (Velocity - frequency * impulse * seconds) * decay;
        if (IsSettled) Snap(Target);
    }
}
