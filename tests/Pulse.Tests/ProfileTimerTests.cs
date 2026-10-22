using Pulse.Core;
using Pulse.Infrastructure;
using Xunit;

namespace Pulse.Tests;

public sealed class ProfileTimerTests
{
    [Fact]
    public void LegacyTimerMigratesToItsProfileOnceWithoutEnablingOthers()
    {
        var settings = (new PulseSettings { TimerProfile = Profile.Gaming, TimerMinutes = 17 }).Normalize();
        Assert.Equal(new ProfileTimer(true, 17), settings.TimerFor(Profile.Gaming));
        Assert.False(settings.TimerFor(Profile.Focus).AutoStart);
        var normalizedAgain = (settings with { TimerProfile = Profile.Work, TimerMinutes = 45 }).Normalize();
        Assert.Equal(new ProfileTimer(true, 17), normalizedAgain.TimerFor(Profile.Gaming));
        Assert.Equal(new ProfileTimer(false, 17), normalizedAgain.TimerFor(Profile.Work));
    }

    [Fact]
    public void FocusDefaultsToThirtyMinutesAndOtherProfilesStartManually()
    {
        var settings = new PulseSettings().Normalize();
        Assert.Equal(new ProfileTimer(true, 30), settings.TimerFor(Profile.Focus));
        Assert.All(settings.Profiles.Where(p => p != Profile.Focus), p => Assert.Equal(new ProfileTimer(false, 30), settings.TimerFor(p)));
        var disabled = (new PulseSettings { TimerAutoStart = false, Workspaces = null! }).Normalize();
        Assert.False(disabled.TimerFor(Profile.Focus).AutoStart);
    }

    [Theory]
    [InlineData(-10, 1)] [InlineData(2000, 1440)]
    public void ExplicitProfileTimerWinsAndItsDurationIsBounded(int requested, int expected)
    {
        var settings = new PulseSettings { Workspaces = [ProfileWorkspaces.Default(Profile.Focus) with { Timer = new(false, requested) }] }.Normalize();
        Assert.Equal(new ProfileTimer(false, expected), settings.TimerFor(Profile.Focus));
    }

    [Fact]
    public async Task ProfilesKeepIndependentTimersAcrossSaveCloneAndDelete()
    {
        var path = Path.Combine(Path.GetTempPath(), "Nagi-profile-timers-" + Guid.NewGuid());
        try
        {
            var settings = new PulseSettings { Workspaces = [
                ProfileWorkspaces.Default(Profile.Work) with { Timer = new(true, 25) },
                ProfileWorkspaces.Default(Profile.Gaming) with { Timer = new(false, 90) }] }.Normalize();
            settings = ProfileCustomization.Create(settings, "Study", Profile.Work);
            var custom = settings.CustomProfiles.Single().Id;
            Assert.Equal(new ProfileTimer(true, 25), settings.TimerFor(custom));
            using var store = new SettingsService(path); await store.SaveAsync(settings);
            var loaded = await store.LoadAsync();
            Assert.Equal(new ProfileTimer(true, 25), loaded.TimerFor(Profile.Work));
            Assert.Equal(new ProfileTimer(false, 90), loaded.TimerFor(Profile.Gaming));
            Assert.Equal(new ProfileTimer(true, 25), loaded.TimerFor(custom));
            var removed = ProfileCustomization.Remove(loaded, custom);
            Assert.DoesNotContain(removed.Workspaces, w => w.Profile == custom);
            Assert.Equal(new ProfileTimer(true, 25), removed.TimerFor(Profile.Work));
        }
        finally { if (Directory.Exists(path)) Directory.Delete(path, true); }
    }
}
