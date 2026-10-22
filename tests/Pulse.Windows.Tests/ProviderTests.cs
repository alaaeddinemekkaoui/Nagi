using Pulse.Windows;
using Xunit;

namespace Pulse.Windows.Tests;

public sealed class ProviderTests
{
    [Fact] public void FpsHelperRequestsPermissionOnlyForAFileAndPassesLiteralArguments()
    {
        var directory = Path.Combine(Path.GetTempPath(), "Nagi helper test", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory); var script = Path.Combine(directory, "Start-NagiFpsCapture.ps1"); File.WriteAllText(script, "# fixture");
        try
        {
            var settings = Path.Combine(directory, "settings.json");
            var start = FpsCaptureHelper.CreateStartInfo(script, settings, 123, 456);
            Assert.True(start.UseShellExecute); Assert.Equal("runas", start.Verb); Assert.Equal(System.Diagnostics.ProcessWindowStyle.Hidden, start.WindowStyle);
            Assert.EndsWith(@"WindowsPowerShell\v1.0\powershell.exe", start.FileName);
            Assert.Contains(script, start.ArgumentList); Assert.Contains(settings, start.ArgumentList);
            Assert.Contains(Path.Combine(directory, "Telemetry"), start.ArgumentList); Assert.DoesNotContain("-Command", start.ArgumentList);
            Assert.Throws<ArgumentException>(() => FpsCaptureHelper.CreateStartInfo(script, settings, 123, 123));
            Assert.Throws<ArgumentException>(() => FpsCaptureHelper.CreateStartInfo("missing.ps1", settings, 123, 456));
        }
        finally { Directory.Delete(directory, true); }
    }
    [Fact] public void GameCatalogIsCachedAndRescanRefreshesInstalledLibraries()
    {
        var reads = 0;
        var games = new GameDetectionService(() => { reads++; return reads == 1 ? [new(@"D:\Games\First", "Steam")] : [new(@"D:\Games\Second", "Epic Games")]; });
        var first = new Pulse.Core.ForegroundApplication(123, "game", @"D:\Games\First\game.exe");
        Assert.True(games.Classify(first).IsGame);
        Assert.Equal("Steam", games.Classify(first).GameSource); Assert.Equal(1, reads);
        var second = first with { ExecutablePath = @"D:\Games\Second\game.exe" };
        Assert.False(games.Classify(second).IsGame);
        games.Invalidate();
        Assert.Equal("Epic Games", games.Classify(second).GameSource); Assert.Equal(2, reads);
        Assert.False(games.Classify(first).IsGame);
    }
    [Fact] public async Task ExternalFpsHelperReportsOnlyFreshFramesForTheSelectedProcess()
    {
        var directory = Path.Combine(Path.GetTempPath(), "PulseFpsTest", Guid.NewGuid().ToString("N")); Directory.CreateDirectory(directory);
        var file = Path.Combine(directory, "frames.csv");
        try
        {
            File.WriteAllText(file, "ProcessID,MsBetweenPresents\n12,20\n12,20\n");
            var provider = new FpsService("missing.exe", file);
            Assert.Equal(50, (await provider.SampleAsync(true, 12, default)).Fps);
            Assert.Null((await provider.SampleAsync(true, 99, default)).Fps);
            File.SetLastWriteTimeUtc(file, DateTime.UtcNow.AddMinutes(-1)); Assert.Null((await provider.SampleAsync(true, 12, default)).Fps);
        }
        finally { Directory.Delete(directory, true); }
    }
    [Fact] public void StartupRegistrationQuotesPathsAndRestoresThePreviousUserValue()
    {
        using var key = Microsoft.Win32.Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run", true);
        var existed = key.GetValueNames().Contains("Nagi", StringComparer.OrdinalIgnoreCase); var previous = key.GetValue("Nagi", null, Microsoft.Win32.RegistryValueOptions.DoNotExpandEnvironmentNames);
        var kind = existed ? key.GetValueKind("Nagi") : Microsoft.Win32.RegistryValueKind.String;
        try
        {
            var executable = Environment.ProcessPath!;
            Assert.True(ActionLauncher.ConfigureStartup(true, executable).Success); Assert.Equal("\"" + executable + "\"", key.GetValue("Nagi"));
            Assert.True(ActionLauncher.ConfigureStartup(false, executable).Success); Assert.Null(key.GetValue("Nagi"));
        }
        finally { if (existed) key.SetValue("Nagi", previous!, kind); else key.DeleteValue("Nagi", false); }
    }
    [Fact] public async Task GpuAndTemperatureReadingsAreRealBoundedOrExplicitlyMissing()
    {
        using var gpu = new GpuTelemetryService(); gpu.Sample(); await Task.Delay(250); var sample = gpu.Sample();
        Assert.False(string.IsNullOrWhiteSpace(sample.Name)); Assert.False(string.IsNullOrWhiteSpace(sample.Status));
        if (sample.Usage is { } usage) Assert.InRange(usage, 0, 100);
        var sensors = new TemperatureService().Sample();
        Assert.All(sensors.Readings, sensor => Assert.InRange(sensor.Celsius, -40, 150));
        if (sensors.Readings.Length == 0) Assert.Contains("provider", sensors.Status);
    }
    [Fact] public void DefaultMicrophoneCanReadAndWriteItsExistingMuteWithoutRecording()
    {
        using var mic = new AudioControlService(capture: true); var before = mic.Sample();
        if (!before.Volume.HasValue) { Assert.NotNull(before.Error); return; }
        Assert.InRange(before.Volume.Value, 0, 100); Assert.True(mic.SetMute(before.Muted).Success);
        Assert.Equal(before.Muted, mic.Sample().Muted);
    }
    [Fact] public async Task DisabledAndMissingFpsProvidersDoNotStartProcessesOrInventRates()
    {
        var fps = new FpsService("missing-presentmon.exe");
        Assert.Null((await fps.SampleAsync(false, 12, default)).Fps);
        Assert.Null((await fps.SampleAsync(true, 12, default)).Fps);
        Assert.False((await new ObsService().SampleAsync(false, 4455, "", default)).Connected);
    }
    [Fact] public void ObsCredentialIsProtectedForTheWindowsUserAndCorruptionIsRecoverable()
    {
        var directory = Path.Combine(Path.GetTempPath(), "PulseSecrets", Guid.NewGuid().ToString("N")); var path = Path.Combine(directory, "obs.credential");
        try
        {
            var store = new ProtectedSecretStore(path); store.Save("test-password-57838");
            Assert.Equal("test-password-57838", store.Read());
            Assert.DoesNotContain("test-password", System.Text.Encoding.UTF8.GetString(File.ReadAllBytes(path)));
            File.WriteAllText(path, "corrupt"); Assert.Equal("", store.Read()); store.Clear(); Assert.False(store.Exists);
        }
        finally { if (Directory.Exists(directory)) Directory.Delete(directory, true); }
    }
}
