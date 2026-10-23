using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Pulse.Core;

namespace Pulse.App;

public sealed partial class MainWindow
{
    private readonly CountdownTimer _timer = new();
    private readonly Controls.MusicVisualizer _restingVisualizer = new(5) { Width = 26, Height = 16 };
    private readonly DispatcherTimer _timerTick = new() { Interval = TimeSpan.FromSeconds(1) };
    private DateTimeOffset _completedUntil;
    private string _restingActivity = "";
    private double _restingWidth = DockPresentation.LiveNotchWidth;
    private double _restingHeight = DockPresentation.LiveNotchHeight;
    private bool? _restingHorizontal;
    private int _restingTextLength;
    private Profile? _timerObservedProfile;
    private bool HasRestingActivity => _restingActivity.Length > 0;
    private bool HasDockTimer => DockPresentation.ShowTimer(ViewModel.Settings.Workspace(ViewModel.Profile), _timer.Active);
    private string TimerDisplayText => _timer.Active || DateTimeOffset.UtcNow < _completedUntil
        ? _timer.Text : CountdownTimer.Format(TimeSpan.FromMinutes(ViewModel.Settings.TimerFor(ViewModel.Profile).Minutes));
    private bool RestingMediaVisible => _restingActivity == "media" && _dock.Stage == DockStage.Resting;
    private TextBlock? _timerSettingsValue;
    private Button? _timerSettingsPause, _timerSettingsCancel;
    private void InitializeTimer()
    {
        RestingVisualizerHost.Children.Add(_restingVisualizer);
        AutomationProperties.SetName(RestingComplete, L.T("Timer complete"));
        _timerTick.Tick += (_, _) =>
        {
            if (_timer.Complete())
            {
                _completedUntil = DateTimeOffset.UtcNow.AddSeconds(5);
                ViewModel.Publish(L.T("Timer complete · 00:00"), true);
                _log.Write("timer", "Countdown complete");
                if (!_userHidden) _motion.Reveal(RestingActivity, duration: 220, scale: .85f);
            }
            UpdateTimerLabels(); RefreshLiveActivity(); UpdateRuntimeDemand();
            if (!_timer.Running && DateTimeOffset.UtcNow >= _completedUntil) _timerTick.Stop();
        };
        UpdateTimerLabels();
    }
    private void ObserveTimerProfile()
    {
        if (_timerObservedProfile == ViewModel.Profile) return;
        _timerObservedProfile = ViewModel.Profile;
        if (ViewModel.Settings.TimerFor(ViewModel.Profile).AutoStart && !_timer.Active)
            StartTimer();
    }
    private void StartTimer(int? duration = null)
    {
        var minutes = duration ?? ViewModel.Settings.TimerFor(ViewModel.Profile).Minutes;
        _completedUntil = default;
        _timerDuration = TimeSpan.FromMinutes(minutes);
        _timer.Start(TimeSpan.FromMinutes(minutes)); _timerTick.Start();
        _log.Write("timer", "Started " + minutes + " minutes");
        UpdateTimerLabels(); RefreshLiveActivity(); Layout(true);
    }
    private void TimerStart_Click(object sender, RoutedEventArgs e) => StartTimer();
    private void TimerDock_Click(object sender, RoutedEventArgs e) => OpenControlCenter("Utilities");
    private void TimerPause_Click(object sender, RoutedEventArgs e)
    {
        if (_timer.Paused) { _timer.Resume(); _timerTick.Start(); } else _timer.Pause();
        UpdateTimerLabels(); RefreshLiveActivity();
    }
    private void TimerCancel_Click(object sender, RoutedEventArgs e)
    {
        _timer.Cancel(); _completedUntil = default; _timerTick.Stop();
        UpdateTimerLabels(); RefreshLiveActivity(); Layout(true);
    }
    private void UpdateTimerLabels()
    {
        var text = TimerDisplayText;
        RestingCountdown.Text = _timer.Text;
        CompactTimerValue.Text = text;
        CompactTimerButton.Visibility = HasDockTimer ? Visibility.Visible : Visibility.Collapsed;
        PeekTimerRemaining.Text = text; PeekTimerProfile.Text = ProfileLabel(ViewModel.Profile);
        PeekTimerRemaining.FontSize = text.Length > 5 ? 30 : 42;
        var startLabel = L.T(_timer.Active ? "Restart timer" : "Start timer");
        var pauseLabel = L.T(_timer.Paused ? "Resume" : "Pause");
        AutomationProperties.SetName(PeekTimerStart, startLabel); ToolTipService.SetToolTip(PeekTimerStart, startLabel);
        AutomationProperties.SetName(PeekTimerPause, pauseLabel); ToolTipService.SetToolTip(PeekTimerPause, pauseLabel);
        PeekTimerPauseIcon.Glyph = _timer.Paused ? "\uE768" : "\uE769"; PeekTimerPause.IsEnabled = _timer.Active;
        PeekTimerCancel.IsEnabled = _timer.Active;
        TimerHeading.Text = L.T("Timer"); TimerOverviewValue.Text = text;
        TimerOverviewStart.Content = L.T(_timer.Active ? "Restart timer" : "Start timer");
        TimerOverviewPause.Content = L.T(_timer.Paused ? "Resume" : "Pause"); TimerOverviewPause.IsEnabled = _timer.Active;
        TimerOverviewCancel.Content = L.T("Cancel timer"); TimerOverviewCancel.IsEnabled = _timer.Active;
        TimerCard.Visibility = ViewModel.Settings.Workspace(ViewModel.Profile).Shows("Timer") || _timer.Active ? Visibility.Visible : Visibility.Collapsed;
        if (_timerSettingsValue is not null) _timerSettingsValue.Text = text;
        if (_timerSettingsPause is not null) { _timerSettingsPause.Content = TimerOverviewPause.Content; _timerSettingsPause.IsEnabled = _timer.Active; }
        if (_timerSettingsCancel is not null) _timerSettingsCancel.IsEnabled = _timer.Active;
        AutomationProperties.SetName(CompactTimerValue, L.T("Timer") + " " + text);
        AutomationProperties.SetName(CompactTimerButton, L.T("Timer") + " " + text);
        UpdateUtilityTimer();
        if (!HasDockTimer && _preview == PreviewKind.Timer) ClosePreview();
    }
    private void RefreshLiveActivity(bool layout = true)
    {
        var next = DateTimeOffset.UtcNow < _completedUntil ? "complete"
            : NotificationActivity.Length > 0 ? NotificationActivity
            : _timer.Active && ViewModel.Settings.LiveTimer ? "timer"
            : ViewModel.Settings.LiveMedia && ViewModel.Settings.ShowMedia && ViewModel.Media.Playing ? "media" : "";
#if NAGI_DEVELOPMENT
        if (DateTimeOffset.UtcNow < _developmentPreviewUntil) next = _developmentPreview == "idle" ? "" : _developmentPreview;
        _developmentNotification.Visibility = next == "notification" ? Visibility.Visible : Visibility.Collapsed;
#endif
        _notchStatusRing.Visibility = _notchStatusText.Visibility = next is "notice" or "transfer" ? Visibility.Visible : Visibility.Collapsed;
        var horizontal = ViewModel.Settings.Position.Orientation == "Horizontal";
        var changed = next != _restingActivity || _restingHorizontal != horizontal;
        // Tabular digits need a new measurement only when the formatted length changes.
        var contentLength = next == "timer" ? _timer.Text.Length : next is "notice" or "transfer" ? _notchStatusText.Text.Length : 0;
        if (!changed && _restingTextLength == contentLength) return;
        _restingTextLength = contentLength;
        _restingHorizontal = horizontal;
        _restingActivity = next;
        ConfigureRestingActivity(horizontal);
        NotchMark.Visibility = next.Length == 0 ? Visibility.Visible : Visibility.Collapsed;
        RestingActivity.Visibility = next.Length > 0 ? Visibility.Visible : Visibility.Collapsed;
        RestingArtworkHost.Visibility = RestingVisualizerHost.Visibility = next == "media" ? Visibility.Visible : Visibility.Collapsed;
        RestingTimerIcon.Visibility = next == "timer" ? Visibility.Visible : Visibility.Collapsed;
        RestingCountdown.Visibility = next is "timer" or "complete" ? Visibility.Visible : Visibility.Collapsed;
        if (next == "complete") RestingCountdown.Foreground = RestingComplete.Foreground;
        else RestingCountdown.ClearValue(TextBlock.ForegroundProperty);
        RestingComplete.Visibility = next == "complete" ? Visibility.Visible : Visibility.Collapsed;
        RestingActivity.Measure(new global::Windows.Foundation.Size(double.PositiveInfinity, double.PositiveInfinity));
        var size = DockPresentation.LiveNotchSizeFor(RestingActivity.DesiredSize.Width, RestingActivity.DesiredSize.Height, horizontal);
        if (!changed && Math.Abs(size.Width - _restingWidth) < .5 && Math.Abs(size.Height - _restingHeight) < .5) return;
        _restingWidth = size.Width; _restingHeight = size.Height;
        AutomationProperties.SetHelpText(EdgeDropButton, next == "timer" ? L.T("Timer") + " " + _timer.Text : next == "media" ? ViewModel.MediaTitle : next == "complete" ? L.T("Timer complete · 00:00") : L.T("Hover to reveal compact controls"));
        if (layout && !_closed && _dock.Stage == DockStage.Resting) Layout(true);
    }
    private void ConfigureRestingActivity(bool horizontal)
    {
        _notchStatusText.MaxWidth = horizontal ? 140 : 64;
#if NAGI_DEVELOPMENT
        _developmentNotification.MaxWidth = horizontal ? 140 : 60;
#endif
        RestingActivity.Orientation = horizontal ? Orientation.Horizontal : Orientation.Vertical;
        RestingActivity.Spacing = horizontal ? 8 : 6;
        RestingCountdown.FontSize = horizontal ? 14 : 11;
    }
    private void RenderRestingAudio(AudioSnapshot audio)
    {
        if (!RestingMediaVisible || _userHidden || !_windowManager.IsVisible) return;
        _restingVisualizer.Update(audio.Muted ? 0 : audio.Peak ?? 0, audio.Peak.HasValue,
            VisualizerBrush, !ViewModel.Settings.ReducedMotion && _audioMotion.AnimationsEnabled);
    }
    private void BuildTimerSettings()
    {
        var content = Stack(); content.Children.Add(Heading(L.T("Timer & live activity")));
        _timerSettingsValue = new TextBlock { FontSize = 30, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold };
        AutomationProperties.SetAutomationId(_timerSettingsValue, "TimerRemaining"); content.Children.Add(_timerSettingsValue);
        content.Children.Add(Note(L.T("Set the duration and automatic start in Profiles.")));
        content.Children.Add(Button(L.T("Profiles"), CustomizeActiveWorkspace_Click));
        var actions = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        var start = Button(L.T("Start timer"), TimerStart_Click);
        AutomationProperties.SetAutomationId(start, "TimerStart"); actions.Children.Add(start);
        _timerSettingsPause = Button(L.T("Pause"), TimerPause_Click); actions.Children.Add(_timerSettingsPause);
        _timerSettingsCancel = Button(L.T("Cancel timer"), TimerCancel_Click); actions.Children.Add(_timerSettingsCancel); content.Children.Add(actions);
        content.Children.Add(Toggle(L.T("Show timer before hovering"), L.T("A green check still appears when a hidden timer finishes."), ViewModel.Settings.LiveTimer, (s, v) => s with { LiveTimer = v }));
        content.Children.Add(Toggle(L.T("Show playing music before hovering"), L.T("Show only cover artwork and a small live audio visualizer."), ViewModel.Settings.LiveMedia, (s, v) => s with { LiveMedia = v }));
        content.Children.Add(Note(L.T("The timer appears beside volume in the dock. Timer activity takes priority over music. Closing Nagi cancels the timer.")));
        SettingsPage.Children.Add(Card(content)); UpdateTimerLabels();
    }
}
