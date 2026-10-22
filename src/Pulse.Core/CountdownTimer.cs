namespace Pulse.Core;

// Deadline-based: delayed UI ticks and sleep never lengthen the countdown.
public sealed class CountdownTimer(TimeProvider? time = null)
{
    private readonly TimeProvider _time = time ?? TimeProvider.System;
    private DateTimeOffset? _deadline;
    private TimeSpan _paused;
    public bool Running => _deadline.HasValue;
    public bool Active => Running || _paused > TimeSpan.Zero;
    public bool Paused => !Running && Active;
    public TimeSpan Remaining => _deadline is { } deadline ? TimeSpan.FromTicks(Math.Max(0, (deadline - _time.GetUtcNow()).Ticks)) : _paused;
    public string Text => Format(Remaining);
    public void Start(TimeSpan duration)
    {
        if (duration <= TimeSpan.Zero || duration > TimeSpan.FromDays(1)) throw new ArgumentOutOfRangeException(nameof(duration));
        _paused = TimeSpan.Zero; _deadline = _time.GetUtcNow() + duration;
    }
    public void Pause() { if (!Running) return; _paused = Remaining; _deadline = null; }
    public void Resume() { if (_paused <= TimeSpan.Zero) return; _deadline = _time.GetUtcNow() + _paused; _paused = TimeSpan.Zero; }
    public void Cancel() { _deadline = null; _paused = TimeSpan.Zero; }
    public bool Complete()
    { if (!Running || Remaining > TimeSpan.Zero) return false; Cancel(); return true; }
    public static string Format(TimeSpan remaining)
    {
        var seconds = Math.Max(0, (long)Math.Ceiling(remaining.TotalSeconds));
        return seconds >= 3600 ? $"{seconds / 3600}:{seconds / 60 % 60:00}:{seconds % 60:00}" : $"{seconds / 60:00}:{seconds % 60:00}";
    }
}
