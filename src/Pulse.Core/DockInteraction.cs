namespace Pulse.Core;

public enum DockStage { Resting, Compact, Expanded }

/// <summary>Compact controls follow pointer visibility or an explicit persistent preference.</summary>
public sealed class DockInteraction
{
    public DockStage Stage { get; private set; }
    public bool AlwaysShowMiniBar { get; private set; }
    public void SetAlwaysShowMiniBar(bool enabled)
    {
        AlwaysShowMiniBar = enabled;
        if (Stage != DockStage.Expanded) Stage = enabled ? DockStage.Compact : DockStage.Resting;
    }
    public void Hover() { if (Stage == DockStage.Resting) Stage = DockStage.Compact; }
    public void Click()
    {
        Hover();
    }
    public void Leave() { if (Stage == DockStage.Compact) Collapse(); }
    public void Expand() => Stage = DockStage.Expanded;
    public void Collapse() => Stage = AlwaysShowMiniBar ? DockStage.Compact : DockStage.Resting;
}
