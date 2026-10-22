namespace Pulse.Core;

public sealed record RuntimeDemand(bool Gpu = false, bool Microphone = false, bool Sensors = false, bool Fps = false,
    int Audio = 0, int ProcessId = 0)
{
    public bool MicrophonePeak { get; init; }
    public bool NetworkLatency { get; init; }
    public int AudioIntervalMs => Audio == 2 ? 50 : MicrophonePeak ? 100 : Audio == 1 ? 500 : 2000;
    public bool AudioPeak => Audio == 2;
}

public static class AnimationCadence
{
    public static double Hertz(double displayHertz, bool efficiency) =>
        Math.Clamp(double.IsFinite(displayHertz) && displayHertz >= 24 ? displayHertz : 60, 24, efficiency ? 60 : 240);
}
