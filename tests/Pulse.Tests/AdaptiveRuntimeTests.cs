using Pulse.Core;
using Pulse.Infrastructure;
using Xunit;

namespace Pulse.Tests;

public sealed class AdaptiveRuntimeTests
{
    private sealed class Clock : TimeProvider
    {
        public DateTimeOffset Now = DateTimeOffset.Parse("2026-10-20T12:00:00Z");
        public override DateTimeOffset GetUtcNow() => Now;
    }
    [Fact]
    public void CountdownUsesDeadlinePausesResumesAndCompletesExactlyOnce()
    {
        var clock = new Clock(); var timer = new CountdownTimer(clock);
        timer.Start(TimeSpan.FromMinutes(30)); Assert.Equal("30:00", timer.Text);
        clock.Now += TimeSpan.FromMinutes(10); timer.Pause(); Assert.True(timer.Paused);
        clock.Now += TimeSpan.FromHours(1); Assert.Equal("20:00", timer.Text);
        timer.Resume(); clock.Now += TimeSpan.FromMinutes(21);
        Assert.Equal("00:00", timer.Text); Assert.True(timer.Complete()); Assert.False(timer.Complete()); Assert.False(timer.Active);
        timer.Start(TimeSpan.FromHours(2)); Assert.Equal("2:00:00", timer.Text); timer.Cancel(); Assert.False(timer.Active);
    }
    [Theory]
    [InlineData(0, 2000, false)] [InlineData(1, 500, false)] [InlineData(2, 50, true)]
    public void OnlyVisibleMetersNeedFastPeakSampling(int audio, int interval, bool peak)
    { var demand = new RuntimeDemand(Audio: audio); Assert.Equal(interval, demand.AudioIntervalMs); Assert.Equal(peak, demand.AudioPeak); }
    [Theory]
    [InlineData(144, false, 144)] [InlineData(144, true, 60)] [InlineData(59.94, false, 59.94)]
    [InlineData(0, false, 60)] [InlineData(360, false, 240)] [InlineData(30, true, 30)]
    public void CadenceFollowsDisplayWithAnEfficiencyCap(double display, bool efficiency, double expected)
        => Assert.Equal(expected, AnimationCadence.Hertz(display, efficiency));
    [Fact]
    public async Task CustomProfilesSurviveSaveAndActivationAndDeleteTheirReferences()
    {
        var path = Path.Combine(Path.GetTempPath(), "Nagi-profiles-" + Guid.NewGuid());
        try
        {
            var settings = ProfileCustomization.Create(new(), "Design", Profile.Work);
            var id = settings.CustomProfiles.Single().Id;
            settings = settings with { LockedProfile = id, DefaultProfile = id, TimerProfile = id,
                Rules = [new("design.exe", id)], QuickActions = [new("Design", ActionKind.Profile, ((int)id).ToString(), id)] };
            using var store = new SettingsService(path); await store.SaveAsync(settings);
            var loaded = await store.LoadAsync(); Assert.Equal("Design", loaded.ProfileName(id));
            Assert.Equal(id, new ContextEngine().Evaluate(ForegroundApplication.Empty, loaded, DateTimeOffset.UtcNow));
            Assert.True(ActionValidator.Validate(loaded.QuickActions.Single(), loaded).Success);
            Assert.Equal(settings.Workspace(Profile.Work).Widgets, loaded.Workspace(id).Widgets);
            var renamed = ProfileCustomization.Rename(loaded, id, "Creative"); Assert.Equal("Creative", renamed.ProfileName(id));
            var removed = ProfileCustomization.Remove(renamed, id);
            Assert.Empty(removed.CustomProfiles); Assert.Null(removed.LockedProfile); Assert.Empty(removed.Rules); Assert.Empty(removed.QuickActions);
            Assert.Equal(Profile.Default, removed.DefaultProfile); Assert.Equal(Profile.Focus, removed.TimerProfile);
        }
        finally { if (Directory.Exists(path)) Directory.Delete(path, true); }
    }
    [Fact]
    public void CustomNamesCannotOverwritePresetsOrExistingProfiles()
    {
        Assert.Throws<ArgumentException>(() => ProfileCustomization.Create(new(), "Work", Profile.Default));
        Assert.Throws<ArgumentException>(() => ProfileCustomization.Create(new(), "Automatic", Profile.Default));
        var settings = ProfileCustomization.Create(new(), "Design", Profile.Default);
        Assert.Throws<ArgumentException>(() => ProfileCustomization.Create(settings, "design", Profile.Default));
        Assert.Throws<ArgumentException>(() => ProfileCustomization.Create(settings, "\n", Profile.Default));
    }
    [Fact]
    public async Task IncrementalIndexTracksFolderRenameDeletionAndHonorsScopeAndSkippedFolders()
    {
        var path = Path.Combine(Path.GetTempPath(), "Nagi-index-" + Guid.NewGuid());
        var root = Path.Combine(path, "root"); Directory.CreateDirectory(root);
        try
        {
            var index = new FileSearchIndex(Path.Combine(path, "cache"));
            await index.RefreshAsync([root], default);
            var folder = Path.Combine(root, "new"); Directory.CreateDirectory(folder);
            await File.WriteAllTextAsync(Path.Combine(folder, "test.txt"), "test");
            await index.ApplyChangesAsync([root], [folder], default); Assert.Equal(2, index.Snapshot.Entries.Length);
            var renamed = Path.Combine(root, "renamed"); Directory.Move(folder, renamed);
            await index.ApplyChangesAsync([root], [folder, renamed], default);
            Assert.DoesNotContain(index.Snapshot.Entries, e => FileSearchIndex.WithinRoot(e.Target, folder));
            Assert.Contains(index.Snapshot.Entries, e => e.Target == Path.Combine(renamed, "test.txt"));
            var skipped = Path.Combine(root, "node_modules"); Directory.CreateDirectory(skipped);
            var ignored = Path.Combine(skipped, "ignored.txt"); await File.WriteAllTextAsync(ignored, "test");
            await index.ApplyChangesAsync([root], [skipped, ignored], default); Assert.Equal(2, index.Snapshot.Entries.Length);
            Directory.Delete(renamed, true); await index.ApplyChangesAsync([root], [renamed], default); Assert.Empty(index.Snapshot.Entries);
            index.Clear([]); await index.ApplyChangesAsync([root], [root], default); Assert.Empty(index.Snapshot.Entries);
        }
        finally { Directory.Delete(path, true); }
    }
    [Fact]
    public async Task FileWatcherUpdatesWithoutARescanTimer()
    {
        var path = Path.Combine(Path.GetTempPath(), "Nagi-watch-" + Guid.NewGuid());
        var root = Path.Combine(path, "root"); Directory.CreateDirectory(root);
        try
        {
            var index = new FileSearchIndex(Path.Combine(path, "cache")); await index.RefreshAsync([root], default);
            using var watcher = new FileIndexWatcher(index, [root], default);
            var changed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            watcher.Changed += () => changed.TrySetResult();
            var file = Path.Combine(root, "added.txt"); await File.WriteAllTextAsync(file, "hello");
            await changed.Task.WaitAsync(TimeSpan.FromSeconds(8));
            Assert.Contains(index.Snapshot.Entries, e => e.Target == file);
        }
        finally { Directory.Delete(path, true); }
    }
    [Fact]
    public async Task ReconciliationAttachesToARootThatAppearsLater()
    {
        var path = Path.Combine(Path.GetTempPath(), "Nagi-watch-retry-" + Guid.NewGuid());
        var root = Path.Combine(path, "later"); Directory.CreateDirectory(path);
        try
        {
            var index = new FileSearchIndex(Path.Combine(path, "cache")); await index.RefreshAsync([root], default);
            using var watcher = new FileIndexWatcher(index, [root], default);
            var changed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            watcher.Changed += () => changed.TrySetResult();
            Directory.CreateDirectory(root); var file = Path.Combine(root, "first.txt"); await File.WriteAllTextAsync(file, "hello");
            watcher.Reconcile(); await changed.Task.WaitAsync(TimeSpan.FromSeconds(8));
            Assert.Contains(index.Snapshot.Entries, e => e.Target == file);
            var second = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            watcher.Changed += () => second.TrySetResult();
            var next = Path.Combine(root, "next.txt"); await File.WriteAllTextAsync(next, "hello");
            await second.Task.WaitAsync(TimeSpan.FromSeconds(8)); Assert.Contains(index.Snapshot.Entries, e => e.Target == next);
        }
        finally { Directory.Delete(path, true); }
    }
}
