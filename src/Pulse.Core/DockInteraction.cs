namespace Pulse.Core;

public enum DockStage { Resting, Compact, Expanded }

/// <summary>Pointer reveal is temporary; the first click pins it and the next opens the panel.</summary>
public sealed class DockInteraction
{
    public DockStage Stage { get; private set; }
    public bool Pinned { get; private set; }
    public void Hover() { if (Stage == DockStage.Resting) Stage = DockStage.Compact; }
    public void Click()
    {
        if (Stage == DockStage.Resting) { Stage = DockStage.Compact; Pinned = true; }
        else if (Stage == DockStage.Compact && !Pinned) Pinned = true;
        else Expand();
    }
    public void Leave() { if (Stage == DockStage.Compact && !Pinned) Collapse(); }
    public void Expand() { Stage = DockStage.Expanded; Pinned = false; }
    public void Collapse() { Stage = DockStage.Resting; Pinned = false; }
}
