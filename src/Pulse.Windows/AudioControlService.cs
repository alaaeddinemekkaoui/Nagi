using System.Runtime.InteropServices;
using Pulse.Core;

namespace Pulse.Windows;

// Cached Core Audio interfaces are serialized across sampling and control calls.
public sealed class AudioControlService(bool capture = false) : IAudioControlService, IDisposable
{
    private static readonly Guid EndpointVolumeId = new("5CDF2C82-841E-4546-9722-0CF74078229A");
    private static readonly Guid MeterId = new("C02216F6-8C67-4B5B-9D00-D008E73E0064");
    private readonly object _gate = new();
    private IMMDeviceEnumerator? _enumerator;
    private IMMDevice? _device;
    private IAudioEndpointVolume? _volume;
    private IAudioMeterInformation? _meter;
    private string? _deviceId;
    private string _name = "Default output";
    private long _nextProbe;
    private bool _disposed;

    private void EnsureEndpoint()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        var now = Environment.TickCount64;
        if (now < _nextProbe)
        {
            if (_volume is not null) return;
            throw new InvalidOperationException("Audio endpoint unavailable");
        }
        _nextProbe = now + 500;
        _enumerator ??= (IMMDeviceEnumerator)new MMDeviceEnumerator();
        IMMDevice? probe = null;
        try
        {
            Marshal.ThrowExceptionForHR(_enumerator.GetDefaultAudioEndpoint(capture ? 1 : 0, capture ? 2 : 1, out probe));
            Marshal.ThrowExceptionForHR(probe.GetId(out var id));
            if (_volume is not null && id == _deviceId) return;
            ReleaseEndpoint();
            _device = probe; probe = null; _deviceId = id;
            var iid = EndpointVolumeId;
            Marshal.ThrowExceptionForHR(_device.Activate(ref iid, 23, 0, out var volume));
            _volume = (IAudioEndpointVolume)volume;
            iid = MeterId;
            if (_device.Activate(ref iid, 23, 0, out var meter) >= 0) _meter = (IAudioMeterInformation)meter;
            _name = ReadName(_device);
        }
        finally { if (probe is not null) Marshal.ReleaseComObject(probe); }
    }
    private static string ReadName(IMMDevice device)
    {
        IPropertyStore? store = null;
        try
        {
            Marshal.ThrowExceptionForHR(device.OpenPropertyStore(0, out store));
            var key = new PropertyKey { Format = new("a45c254e-df1c-4efd-8020-67d146a850e0"), Id = 14 };
            Marshal.ThrowExceptionForHR(store.GetValue(ref key, out var value));
            try { return value.Type == 31 ? Marshal.PtrToStringUni(value.Pointer) ?? "Default output" : "Default output"; }
            finally { PropVariantClear(ref value); }
        }
        catch (COMException) { return "Default output"; }
        finally { if (store is not null) Marshal.ReleaseComObject(store); }
    }
    private T WithEndpoint<T>(Func<IAudioEndpointVolume, T> operation)
    {
        lock (_gate)
        {
            try { EnsureEndpoint(); return operation(_volume!); }
            catch { ReleaseEndpoint(); throw; }
        }
    }
    public AudioSnapshot Sample()
    {
        try
        {
            return WithEndpoint<AudioSnapshot>(volume =>
            {
                Marshal.ThrowExceptionForHR(volume.GetMasterVolumeLevelScalar(out var level));
                Marshal.ThrowExceptionForHR(volume.GetMute(out var mute));
                double? peak = null;
                if (_meter is not null && _meter.GetPeakValue(out var measured) >= 0 && float.IsFinite(measured)) peak = Math.Clamp(measured, 0, 1);
                return new(level * 100, mute, _name, Peak: peak);
            });
        }
        catch (Exception ex) { return new(null, false, capture ? "Microphone unavailable" : "Audio output unavailable", ex.GetType().Name); }
    }
    public OperationResult SetVolume(double percent)
    {
        if (!double.IsFinite(percent)) return OperationResult.Fail("Enter a finite volume.");
        return Execute(v => { var context = Guid.Empty; return v.SetMasterVolumeLevelScalar((float)(Math.Clamp(percent, 0, 100) / 100), ref context); });
    }
    public OperationResult SetMute(bool muted) => Execute(v =>
    {
        var context = Guid.Empty; return v.SetMute(muted, ref context);
    });
    private OperationResult Execute(Func<IAudioEndpointVolume, int> action)
    {
        try { return WithEndpoint(volume => { Marshal.ThrowExceptionForHR(action(volume)); return OperationResult.Ok("Audio updated"); }); }
        catch (Exception ex) { return OperationResult.Fail("Audio operation failed: " + ex.GetType().Name); }
    }
    private void ReleaseEndpoint()
    {
        if (_meter is not null) Marshal.ReleaseComObject(_meter);
        if (_volume is not null) Marshal.ReleaseComObject(_volume);
        if (_device is not null) Marshal.ReleaseComObject(_device);
        _meter = null; _volume = null; _device = null; _deviceId = null;
    }
    public void Dispose()
    {
        lock (_gate)
        {
            if (_disposed) return;
            _disposed = true; ReleaseEndpoint();
            if (_enumerator is not null) Marshal.ReleaseComObject(_enumerator);
            _enumerator = null;
        }
    }
    [ComImport, Guid("C02216F6-8C67-4B5B-9D00-D008E73E0064"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IAudioMeterInformation
    {
        [PreserveSig] int GetPeakValue(out float peak);
        [PreserveSig] int GetMeteringChannelCount(out uint count);
        [PreserveSig] int GetChannelsPeakValues(uint count, [Out, MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 0)] float[] peaks);
        [PreserveSig] int QueryHardwareSupport(out uint mask);
    }
    [DllImport("ole32.dll")] private static extern int PropVariantClear(ref PropVariant value);
    [StructLayout(LayoutKind.Sequential)] private struct PropertyKey { public Guid Format; public uint Id; }
    [StructLayout(LayoutKind.Explicit, Size = 24)] private struct PropVariant { [FieldOffset(0)] public ushort Type; [FieldOffset(8)] public nint Pointer; }
    [ComImport, Guid("BCDE0395-E52F-467C-8E3D-C4579291692E")] private class MMDeviceEnumerator { }
    [ComImport, Guid("A95664D2-9614-4F35-A746-DE8DB63617E6"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IMMDeviceEnumerator
    {
        [PreserveSig] int EnumAudioEndpoints(int flow, uint mask, out nint devices);
        [PreserveSig] int GetDefaultAudioEndpoint(int flow, int role, out IMMDevice device);
    }
    [ComImport, Guid("D666063F-1587-4E43-81F1-B948E807363F"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IMMDevice
    {
        [PreserveSig] int Activate(ref Guid iid, uint context, nint parameters, [MarshalAs(UnmanagedType.IUnknown)] out object result);
        [PreserveSig] int OpenPropertyStore(uint mode, out IPropertyStore store);
        [PreserveSig] int GetId([MarshalAs(UnmanagedType.LPWStr)] out string id);
        [PreserveSig] int GetState(out uint state);
    }
    [ComImport, Guid("886d8eeb-8cf2-4446-8d02-cdba1dbdcf99"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IPropertyStore
    {
        [PreserveSig] int GetCount(out uint count);
        [PreserveSig] int GetAt(uint index, out PropertyKey key);
        [PreserveSig] int GetValue(ref PropertyKey key, out PropVariant value);
    }
    [ComImport, Guid("5CDF2C82-841E-4546-9722-0CF74078229A"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IAudioEndpointVolume
    {
        [PreserveSig] int RegisterControlChangeNotify(nint callback);
        [PreserveSig] int UnregisterControlChangeNotify(nint callback);
        [PreserveSig] int GetChannelCount(out uint count);
        [PreserveSig] int SetMasterVolumeLevel(float level, [In] ref Guid context);
        [PreserveSig] int SetMasterVolumeLevelScalar(float level, [In] ref Guid context);
        [PreserveSig] int GetMasterVolumeLevel(out float level);
        [PreserveSig] int GetMasterVolumeLevelScalar(out float level);
        [PreserveSig] int SetChannelVolumeLevel(uint channel, float level, [In] ref Guid context);
        [PreserveSig] int SetChannelVolumeLevelScalar(uint channel, float level, [In] ref Guid context);
        [PreserveSig] int GetChannelVolumeLevel(uint channel, out float level);
        [PreserveSig] int GetChannelVolumeLevelScalar(uint channel, out float level);
        [PreserveSig] int SetMute([MarshalAs(UnmanagedType.Bool)] bool mute, [In] ref Guid context);
        [PreserveSig] int GetMute([MarshalAs(UnmanagedType.Bool)] out bool mute);
    }
}
