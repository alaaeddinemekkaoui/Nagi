using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Pulse.Core;

namespace Pulse.App.ViewModels;

public sealed class PulseViewModel : INotifyPropertyChanged, INotificationService
{
    private readonly ContextEngine _engine = new();
    public PulseSettings Settings { get; set; }
    public SystemSnapshot Metrics { get; private set; } = new(null, 0, 0, null, null);
    public ForegroundApplication Foreground { get; private set; } = ForegroundApplication.Empty;
    public MediaSnapshot Media { get; private set; } = MediaSnapshot.Empty;
    public AudioSnapshot Audio { get; private set; } = new(null, false, "Checking output…");
    public Profile Profile => _engine.Current;
    public ObservableCollection<ActivityEntry> Activity { get; } = [];
    private string _notice = L.T("Your desktop, in rhythm.");
    public string Notice { get => _notice; private set { _notice = value; OnPropertyChanged(); } }
    public string CpuText => Metrics.Cpu is { } cpu ? $"{cpu:0}%" : "—";
    public string RamText => Metrics.MemoryPercent is { } ram ? $"{ram:0}%" : "—";
    public string Clock => DateTime.Now.ToString("HH:mm");
    public string BatteryText => (Metrics.Power ?? PowerSnapshot.Unknown).CompactText;
    public string BatteryDetail
    {
        get
        {
            var power = Metrics.Power ?? PowerSnapshot.Unknown;
            return power.HasBattery == false ? L.T("No system battery detected") : power.Percent is { } percent
                ? L.F($"Battery {percent}% · {L.T(power.Charging == true ? "Charging" : power.PluggedIn == true ? "Plugged in" : power.PluggedIn == false ? "On battery" : "Power source unavailable")}")
                : L.T("Battery percentage unavailable");
        }
    }
    public string ProfileText => L.T(Profile.ToString());
    public string ContextText => Settings.LockedProfile.HasValue ? L.F($"{ProfileText} · Profile locked") : Settings.AutomaticSwitching ? L.F($"{ProfileText} · Automatic") : L.F($"{ProfileText} · Manual");
    public string ForegroundText => Foreground.Name;
    public string MediaTitle => Media.Available ? Media.Title : L.T("Nothing playing");
    public string MediaArtist => Media.Available ? Media.Artist : L.T("Start playback in a media app");
    public string AudioDevice => Audio.Device;
    public string HotkeyText => FormatHotkey(Settings.Hotkey);
    public event PropertyChangedEventHandler? PropertyChanged;
    public event Action? FocusActivated;

    public PulseViewModel(PulseSettings settings)
    {
        Settings = settings;
        _engine.Evaluate(Foreground, settings, DateTimeOffset.UtcNow);
    }
    public void Apply(SystemSnapshot metrics, ForegroundApplication foreground, MediaSnapshot media, AudioSnapshot audio)
    {
        Metrics = metrics;
        if (foreground.ProcessId != Environment.ProcessId) Foreground = foreground;
        Media = media; Audio = audio;
        Evaluate();
        Refresh();
    }
    public void Evaluate()
    {
        var previous = Profile;
        _engine.Evaluate(Foreground, Settings, DateTimeOffset.UtcNow);
        if (previous != Profile)
        {
            Publish(Profile + L.T(" profile activated"), true);
            if (Profile == Profile.Focus) FocusActivated?.Invoke();
        }
    }
    public void ApplyAudio(AudioSnapshot audio)
    {
        var changed = Audio.Device != audio.Device;
        Audio = audio;
        if (changed) OnPropertyChanged(nameof(AudioDevice));
    }
    public void Refresh() => OnPropertyChanged("");
    public void Publish(string message, bool essential = false)
    {
        if (!essential && (!Settings.Notifications || Profile == Profile.Focus)) return;
        Notice = message;
        Activity.Insert(0, new(DateTimeOffset.Now, message));
        while (Activity.Count > 12) Activity.RemoveAt(Activity.Count - 1);
    }
    public static string FormatHotkey(Hotkey key)
    {
        var parts = new List<string>();
        if ((key.Modifiers & 2) != 0) parts.Add("Ctrl"); if ((key.Modifiers & 1) != 0) parts.Add("Alt");
        if ((key.Modifiers & 4) != 0) parts.Add("Shift"); if ((key.Modifiers & 8) != 0) parts.Add("Win");
        parts.Add(key.Key == 0x20 ? "Space" : key.Key is >= 0x70 and <= 0x87 ? "F" + (key.Key - 0x6F) : ((char)key.Key).ToString());
        return string.Join(" + ", parts);
    }
    private void OnPropertyChanged([CallerMemberName] string? propertyName = null) => PropertyChanged?.Invoke(this, new(propertyName));
}
