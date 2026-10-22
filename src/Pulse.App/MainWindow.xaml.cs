using System.Diagnostics;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;
using Microsoft.UI.Xaml.Media.Imaging;
using Pulse.App.ViewModels;
using Pulse.App.Motion;
using Pulse.App.Themes;
using Pulse.Core;
using Pulse.Infrastructure;
using Pulse.Windows;
using Windows.Storage.Streams;
using Windows.System;
using Windows.UI.ViewManagement;
using WinRT.Interop;

namespace Pulse.App;

public sealed partial class MainWindow : Window
{
    public PulseViewModel ViewModel { get; }
    private readonly SettingsService _settings;
    private readonly DiagnosticLog _log;
    private readonly HaloWindowManager _windowManager;
    private readonly HaloMotion _motion;
    private readonly HaloAcrylicBackdrop _glass = new();
    private readonly Dictionary<string, SolidColorBrush> _themeBrushes = new();
    private bool _surfaceIsDrop = true;
    private readonly AccessibilitySettings _themeAccessibility = new();
    private bool _lastHighContrast;
    private readonly SystemTrayService? _tray;
    private readonly ISystemMetricsService _metrics = new SystemMetricsService();
    private readonly IForegroundApplicationService _foreground = new ForegroundApplicationService();
    private readonly IMediaSessionService _media = new MediaSessionService();
    private readonly AudioControlService _audio = new();
    private readonly CancellationTokenSource _shutdown = new();
    private readonly SemaphoreSlim _settingsGate = new(1, 1);
    private readonly DispatcherTimer _volumeTimer = new() { Interval = TimeSpan.FromMilliseconds(50) };
    private Task? _polling;
    private readonly DockInteraction _dock = new();
    private bool _expanded { get => _dock.Stage == DockStage.Expanded; set { if (value) _dock.Expand(); else _dock.Collapse(); } }
    private bool _dashboard, _updating, _dialogOpen, _closed, _userHidden, _revealFullscreen, _controlCenter;
    private bool _hotkeyAvailable;
    private bool _presenterControlCenter;
    private bool _hideHotkeyAvailable, _hiding, _active, _quickMenuOpen;
    private int _visibilityGeneration;
    private int _lastForeground;
    private string _page = "Overview";
    private byte[]? _artwork;
    private Profile? _renderedProfile;
    private double _pendingVolume;
    private PulseSettings _pollSettings;
    private AudioSnapshot _lastAudio = new(null, false, "Audio output unavailable");
    private readonly Stopwatch _uptime = Stopwatch.StartNew();

    public MainWindow(SettingsService settings, PulseSettings configuration, DiagnosticLog log)
    {
        _settings = settings; _log = log; _pollSettings = configuration;
        ViewModel = new(configuration);
        InitializeComponent();
        Root.DataContext = ViewModel;
        L.Options(ProfilePicker);
        ProfilePicker.ItemsSource = new[] { "Automatic" }.Concat(configuration.Profiles.Select(configuration.ProfileName)).ToArray();
        foreach (var railButton in Compact.Children.OfType<Button>()) railButton.Padding = new(4);
        var presenter = (OverlappedPresenter)AppWindow.Presenter;
        presenter.SetBorderAndTitleBar(false, false);
        presenter.IsResizable = false; presenter.IsMaximizable = false; presenter.IsMinimizable = false;
        presenter.IsAlwaysOnTop = true;
        AppWindow.IsShownInSwitchers = false;
        var handle = WindowNative.GetWindowHandle(this);
        _windowManager = new HaloWindowManager(handle);
        _glass.ContourMaskChanged += () => DispatcherQueue.TryEnqueue(() =>
        {
            if (_closed) return;
            _windowManager.AntialiasNotch = _glass.ContourMasked;
            _log.Write("appearance", "Notch antialiasing=" + _glass.ContourMasked);
            _windowManager.ApplyFrame(_windowManager.CurrentFrame);
        });
        _windowManager.FrameApplied += frame =>
        {
            _hasPresentedFrame = true;
            _glass.SetContour((float)Math.Max(16, Math.Round(frame.Width)), (float)Math.Max(16, Math.Round(frame.Height)), frame.DropAmount, ViewModel.Settings.Position.Anchor, frame.NotchAmount, (float)frame.Scale);
            WindowSurface.SetShape(frame.DropAmount, ViewModel.Settings.Position.Anchor, frame.NotchAmount);
            var drop = frame.DropAmount > .001;
            if (_surfaceIsDrop == drop && _materialResting == SettledNotch) return;
            _surfaceIsDrop = drop;
            ApplyGlass();
        };
        _windowManager.ControlCenterResized += frame =>
        {
            if (!_controlCenter || _closed) return;
            _motion?.Stop(); PlaceDockContent(false, frame.Width / frame.Scale, frame.Height / frame.Scale);
        };
        _notchSystemColors.ColorValuesChanged += NotchColorsChanged;
        _motion = new HaloMotion(_windowManager, () => !ViewModel.Settings.ReducedMotion && _audioMotion.AnimationsEnabled,
            handle, () => ViewModel.Settings.AnimationMode == "Efficiency");
        BuildMusicLinks();
        _windowManager.ToggleRequested += () => { if (_expanded && !_userHidden) Collapse(); else Reveal(); };
        _windowManager.HideRequested += () => { if (_userHidden) Reveal(); else HideHalo(); };
        _windowManager.EscapeRequested += () => { if (!_dialogOpen) Collapse(); };
        _windowManager.DisplayChanged += () => DispatcherQueue.TryEnqueue(() => { DismissDockIntroduction(); Layout(); });
        _windowManager.PositionChanged += async position => await CommitAsync(s => s with { Position = position });
        Activated += async (_, args) =>
        {
            _active = args.WindowActivationState != WindowActivationState.Deactivated;
            if (_active) return;
            await Task.Delay(90);
            if (!_closed && !_controlCenter && !_active && !_windowManager.OwnsForeground && _tray?.IsMenuOpen != true && _expanded && !_dialogOpen && !_userHidden && ViewModel.Settings.CloseOnDeactivate) Collapse();
        };
        Closed += async (_, _) => await ShutdownAsync();
        ViewModel.FocusActivated += () => { if (!_controlCenter) Collapse(); };
        ViewModel.PropertyChanged += (_, args) => { if (args.PropertyName == nameof(PulseViewModel.Notice)) Animate(NoticeText); };
        _volumeTimer.Tick += VolumeTimer_Tick;
        InitializePreviews();
        _hotkeyAvailable = Report(_windowManager.RegisterHotkey(configuration.Hotkey), successNotice: false);
        _hideHotkeyAvailable = Report(_windowManager.RegisterHideHotkey(configuration.HideHotkey), successNotice: false);
        InitializeQuickSearch();
        InitializeTimer();
        InitializeIntegrations();
        try
        {
            _tray = new SystemTrayService(handle, Path.Combine(AppContext.BaseDirectory, "Assets", "Nagi.Tray.ico"));
            _tray.Translate = L.T;
            _tray.Translate = L.T;
            _tray.ShowRequested += Reveal;
            _tray.SearchRequested += ToggleQuickSearch;
            _tray.HideRequested += HideHalo;
            _tray.SettingsRequested += () => OpenControlCenter("Settings");
            _tray.QuitRequested += Close;
            _tray.MenuOpening += () =>
            {
                ResetPreview();
                if (_dock.Stage == DockStage.Compact) _dock.Collapse();
                Layout(); // Settle the owner before its native menu enters the modal loop.
            };
            _tray.ProfileRequested += async profile => await ActivateProfileAsync(profile);
            _tray.RegistrationChanged += result => { Report(result, false); UpdateTray(); };
            Report(_tray.Register(), false);
        }
        catch (Exception ex) { _log.Write("tray", "Tray initialization failed", ex); ViewModel.Publish(L.T("Tray unavailable. Relaunch Nagi to recover Nagi."), true); }
        Shell.ActualThemeChanged += (_, _) =>
        {
            _log.Write("theme", $"Theme changed: preference={ViewModel.Settings.Theme}, actual={Shell.ActualTheme}, glass={ViewModel.Settings.GlassEnabled}");
            ApplyGlass(); UpdateTabs(); ApplyLuaVisuals(); _ = ReloadLuaUiAsync();
        };
        // HighContrastChanged registration can fail for unpackaged desktop hosts.
        // Observe the system preference on the existing UI refresh instead.
        _lastHighContrast = _themeAccessibility.HighContrast;
        Root.Loaded += (_, _) => { WireButtons(Root); ApplyLuaVisuals(); };
        _dock.SetAlwaysShowMiniBar(configuration.AlwaysShowMiniBar);
        _expanded = configuration.StartExpanded && ViewModel.Profile != Profile.Focus;
        ApplyPreferences();
        ContentScroll.FlowDirection = L.Direction;
        PeekHost.FlowDirection = L.Direction;
        BuildRulesPage(); BuildSettingsPage(); BuildQuickActions(); BuildProfileDock();
        if (settings.RecoveryMessage is { } recovery) ViewModel.Publish(recovery, true);
    }

    public void ShowInitial()
    {
        Layout();
        _windowManager.SetVisible(true);
        _polling = Task.Run(PollAsync);
        _audioPolling = Task.Run(PollAudioAsync);
        _searchStarted = true; RebuildSearchIndex(); _indexTimer.Start();
        _integrationPolling = Task.Run(PollIntegrationsAsync);
        _weatherPolling = Task.Run(PollWeatherAsync);
        InitializeUpdates();
    }
    public void Reveal()
    {
        if (_closed) return;
        ResetPreview();
        _visibilityGeneration++; _hiding = false; _motion.Reset(Root);
        _userHidden = false; _revealFullscreen = true;
        _dashboard = _page != "Overview";
        _expanded = true; _windowManager.SetVisible(true); Layout(true); Activate(); UpdateTray();
    }
    private void Collapse()
    {
        if (_closed || _dialogOpen) return;
        ResetPreview();
        _dockSuppressed = true;
        _windowManager.TryGetPointerScreenPosition(out _dockDismissX, out _dockDismissY);
        _controlCenter = false; _expanded = false; _dashboard = false; _revealFullscreen = false; Layout(true);
    }
    private void Layout(bool animate = false)
    {
        if (_presenterControlCenter != _controlCenter)
        {
            _presenterControlCenter = _controlCenter;
            var presenter = (OverlappedPresenter)AppWindow.Presenter;
            if (!_controlCenter && presenter.State != OverlappedPresenterState.Restored) presenter.Restore();
            presenter.SetBorderAndTitleBar(_controlCenter, _controlCenter);
            ExtendsContentIntoTitleBar = _controlCenter;
            WindowCaption.Visibility = _controlCenter ? Visibility.Visible : Visibility.Collapsed;
            Expanded.Margin = _controlCenter ? new(0, 32, 0, 0) : new(0);
            SetTitleBar(_controlCenter ? WindowCaption : null);
            presenter.IsResizable = presenter.IsMaximizable = presenter.IsMinimizable = _controlCenter;
            presenter.IsAlwaysOnTop = !_controlCenter; AppWindow.IsShownInSwitchers = _controlCenter;
        }
        ApplyThemeBackground();
        if (!_closed && !_expanded && !_userHidden) _previewWatch.Start(); else _previewWatch.Stop();
        var wasExpanded = Expanded.Visibility == Visibility.Visible;
        var wasPreview = PeekHost.Visibility == Visibility.Visible;
        var wasDrop = EdgeDrop.Visibility == Visibility.Visible;
        RefreshLiveActivity(false);
        var resting = _dock.Stage == DockStage.Resting;
        ApplyGlass(); // Connect acrylic before the first expanding frame, not at the end of the morph.
        var frame = _controlCenter ? _windowManager.PrepareControlCenter(ViewModel.Settings.Position)
            : _windowManager.PrepareLayout(_expanded, _dashboard, ViewModel.Settings.Position, PreviewVisible ? _preview == PreviewKind.Media ? 380 : 336 : 84, resting, RequestedRailExtent, ViewModel.Settings.Position.Orientation == "Horizontal" ? DockPresentation.HorizontalHeight : DockPresentation.VerticalWidth, HasRestingActivity, _restingWidth, _restingHeight);
        if (PreviewVisible && !_controlCenter && !_expanded)
        {
            var horizontal = ViewModel.Settings.Position.Orientation == "Horizontal";
            var previewWidth = frame.Width / frame.Scale - (horizontal ? 0 : DockPresentation.VerticalWidth);
            var required = MeasurePreviewHeight(previewWidth) + (horizontal ? DockPresentation.HorizontalHeight : 0);
            frame = _windowManager.PrepareLayout(false, false, ViewModel.Settings.Position, required, false, RequestedRailExtent,
                horizontal ? DockPresentation.HorizontalHeight : DockPresentation.VerticalWidth);
        }
        var logicalWidth = frame.Width / frame.Scale; var logicalHeight = frame.Height / frame.Scale;
        PlaceDockContent(resting, logicalWidth, logicalHeight);
        if (resting)
        {
            EdgeDrop.Visibility = Visibility.Visible;
            Expanded.IsHitTestVisible = PeekHost.IsHitTestVisible = false;
            if (animate)
            {
                _motion.Fade(Compact, false, 90); _motion.Fade(Expanded, false, 90); _motion.Fade(PeekHost, false, 90);
                _motion.Reveal(EdgeDrop, 0, 230, .96f);
            }
            else { Compact.Visibility = Expanded.Visibility = PeekHost.Visibility = Visibility.Collapsed; _motion.Reset(EdgeDrop); }
        }
        else if (_expanded)
        {
            EdgeDrop.Visibility = Visibility.Collapsed;
            PeekHost.Visibility = Visibility.Collapsed;
            Expanded.Visibility = Visibility.Visible; Expanded.IsHitTestVisible = true;
            Compact.Visibility = Visibility.Visible;
            RailViewport.Visibility = _controlCenter ? Visibility.Collapsed : Visibility.Visible;
            if (animate && !wasExpanded) { _motion.Reveal(Expanded, 0, 200, 1); RevealCards(); }
            else _motion.Reset(Expanded);
        }
        else
        {
            Compact.Visibility = Visibility.Visible; Expanded.IsHitTestVisible = false;
            if (animate && (wasExpanded || wasDrop)) { _motion.Fade(Expanded, false, 95); _motion.Fade(EdgeDrop, false, 90); _motion.Reveal(Compact, 0, 180, 1); }
            else { Expanded.Visibility = Visibility.Collapsed; _motion.Reset(Compact); }
            PeekHost.IsHitTestVisible = PreviewVisible;
            if (PreviewVisible)
            {
                PeekHost.Visibility = Visibility.Visible;
                if (animate && !wasPreview) _motion.Reveal(PeekHost, 0, 180, 1); else _motion.Reset(PeekHost);
            }
            else if (animate && wasPreview) _motion.Fade(PeekHost, false, 90);
            else PeekHost.Visibility = Visibility.Collapsed;
        }
        if (!resting) RenderAudio(_lastAudio);
        Shell.CornerRadius = new(0); // DWM clips the outer corners; avoid an opaque seam behind a second XAML clip.
        _motion.Move(frame, animate && !_userHidden, () =>
        {
            ApplyGlass();
            if (!_expanded) Expanded.Visibility = Visibility.Collapsed;
            if (!PreviewVisible) PeekHost.Visibility = Visibility.Collapsed;
            if (_dock.Stage != DockStage.Resting) EdgeDrop.Visibility = Visibility.Collapsed;
            if (_dock.Stage == DockStage.Resting) Compact.Visibility = Visibility.Collapsed;
        });
        if (_userHidden && !_hiding) _windowManager.SetVisible(false);
        UpdateRuntimeDemand();
    }
    private void Animate(FrameworkElement? target = null)
    {
        if (_closed) return;
        _motion.Reveal(target ?? (_expanded ? Expanded : _dock.Stage == DockStage.Resting ? EdgeDrop : Compact), duration: 160, scale: 1);
    }
    private void RevealCards()
    {
        if (_page != "Overview") return;
        _motion.Reveal(MetricsGrid, 30, 180, 1);
        _motion.Reveal(MediaCard, 45, 180, 1);
        _motion.Reveal(AudioCard, 60, 180, 1);
        _motion.Reveal(QuickActionsPanel, 75, 180, 1);
    }
    private async void HideHalo()
    {
        DismissDockIntroduction();
        if (_closed || _dialogOpen || _userHidden) return;
        if (!_hotkeyAvailable && !_hideHotkeyAvailable && _tray?.IsRegistered != true)
        { ViewModel.Publish(L.T("Set a working shortcut or restore the system tray before hiding Nagi."), true); return; }
        var generation = ++_visibilityGeneration;
        ResetPreview();
        _userHidden = true; _hiding = true; _motion.Stop(); UpdateTray();
        _previewWatch.Stop();
        _motion.Fade(Root, false, 110);
        try { await Task.Delay(ViewModel.Settings.ReducedMotion || !new UISettings().AnimationsEnabled ? 0 : 110, _shutdown.Token); }
        catch (OperationCanceledException) { return; }
        if (_closed || generation != _visibilityGeneration) return;
        _windowManager.SetVisible(false); _motion.Reset(Root); _hiding = false;
    }
    private void UpdateTray()
    {
        if (_tray is not null) _tray.ProfileChoices = ViewModel.Settings.Profiles.Select(p => (p, ProfileLabel(p))).ToArray();
        _tray?.Update(ViewModel.Profile, ViewModel.Settings.LockedProfile ?? (ViewModel.Settings.AutomaticSwitching ? null : ViewModel.Profile), _userHidden, PulseViewModel.FormatHotkey(ViewModel.Settings.HideHotkey));
        ToolTipService.SetToolTip(HideHeaderButton, L.T("Hide Nagi · ") + (_hideHotkeyAvailable ? PulseViewModel.FormatHotkey(ViewModel.Settings.HideHotkey) : L.T("Shortcut unavailable")));
        TrayStatusText.Text = _tray?.IsRegistered == true ? L.T("In system tray") : L.T("Local & private");
    }
    private void WireButtons(DependencyObject parent)
    {
        if (parent is Button button) _motion.Wire(button);
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++) WireButtons(VisualTreeHelper.GetChild(parent, i));
    }
    private Brush ThemeBrush(string key)
    {
        var dictionary = Application.Current.Resources.MergedDictionaries[1];
        var theme = _themeAccessibility.HighContrast ? "HighContrast" : Shell.ActualTheme == ElementTheme.Light ? "Light" : "Dark";
        var resource = (Brush)((ResourceDictionary)dictionary.ThemeDictionaries[theme])[key];
        if (resource is not SolidColorBrush solid) return resource;
        if (!_themeBrushes.TryGetValue(key, out var shared)) _themeBrushes[key] = shared = new(solid.Color);
        else if (!shared.Color.Equals(solid.Color)) shared.Color = solid.Color;
        return shared;
    }
    private void RefreshThemeBrushes()
    {
        foreach (var key in _themeBrushes.Keys.ToArray()) _ = ThemeBrush(key);
    }
    private void ApplyGlass()
    {
        _windowManager.SetTitleBarTheme(Shell.ActualTheme != ElementTheme.Light);
        if (_controlCenter)
        {
            AppWindow.TitleBar.ButtonBackgroundColor = AppWindow.TitleBar.ButtonInactiveBackgroundColor = Microsoft.UI.Colors.Transparent;
            AppWindow.TitleBar.ButtonForegroundColor = ((SolidColorBrush)ThemeBrush("InkBrush")).Color;
            AppWindow.TitleBar.ButtonInactiveForegroundColor = ((SolidColorBrush)ThemeBrush("MutedBrush")).Color;
        }
        var previousGlass = SystemBackdrop is not null;
        var previousResting = _materialResting;
        _glass.SetAkeno(ViewModel.Settings.Theme == "Akeno");
        _glass.SetLight(Shell.ActualTheme == ElementTheme.Light);
        _glass.SetTint(AppAppearance.Tint(ViewModel.Settings));
        _materialResting = SettledNotch;
        var useGlass = AppAppearance.UsesGlass(ViewModel.Settings);
        _glass.SetSolidSurface(useGlass ? null : NotchColor);
        if ((useGlass || _surfaceIsDrop) && HaloAcrylicBackdrop.IsSupported && !_themeAccessibility.HighContrast)
        { if (!ReferenceEquals(SystemBackdrop, _glass)) SystemBackdrop = _glass; Shell.Background = new SolidColorBrush(Microsoft.UI.Colors.Transparent); }
        else
        {
            SystemBackdrop = null;
            Shell.Background = _themeAccessibility.HighContrast ? ThemeBrush("CanvasBrush")
                : new SolidColorBrush(ViewModel.Settings.Theme == "Akeno" ? global::Windows.UI.Color.FromArgb(255, 16, 11, 13)
                    : Shell.ActualTheme == ElementTheme.Light ? global::Windows.UI.Color.FromArgb(255, 241, 244, 249) : global::Windows.UI.Color.FromArgb(255, 27, 30, 40));
        }
        if (_surfaceIsDrop) Shell.Background = new SolidColorBrush(Microsoft.UI.Colors.Transparent);
        Root.Background = new SolidColorBrush(Microsoft.UI.Colors.Transparent);
        UpdateSurfaceMaterial();
        _localMedia?.ApplyAppearance(ViewModel.Settings);
        if (previousGlass != (SystemBackdrop is not null) || previousResting != _materialResting)
        {
            _windowManager.SuppressSystemCorners();
            _log.Write("appearance", $"Glass={SystemBackdrop is not null}; resting={_materialResting}; stage={_dock.Stage}");
        }
    }
    private void UpdateTabs()
    {
        OverviewTab.CornerRadius = RulesTab.CornerRadius = new(19);
        OverviewTab.Background = ThemeBrush(_page == "Overview" ? "SelectedTabBrush" : "InsetBrush");
        RulesTab.Background = ThemeBrush(_page == "Profiles" ? "SelectedTabBrush" : "InsetBrush");
        UtilitiesTab.Background = ThemeBrush(_page == "Utilities" ? "SelectedTabBrush" : "InsetBrush");
        PreferencesTab.Background = ThemeBrush(_page == "Settings" ? "SelectedTabBrush" : "InsetBrush");
    }
    private void RefreshContrast()
    {
        var contrast = _themeAccessibility.HighContrast;
        if (_closed || contrast == _lastHighContrast) return;
        _lastHighContrast = contrast;
        ApplyLuaVisuals(); ApplyGlass(); UpdateTabs();
    }
    private async Task PollAsync()
    {
        var token = _shutdown.Token;
        while (!token.IsCancellationRequested)
        {
            var config = Volatile.Read(ref _pollSettings);
            try
            {
                var metrics = _metrics.Sample();
                var foreground = _foreground.Sample();
                MediaSnapshot media;
                using (var deadline = CancellationTokenSource.CreateLinkedTokenSource(token))
                {
                    deadline.CancelAfter(TimeSpan.FromSeconds(3));
                    try { media = config.ShowMedia ? await _media.SampleAsync(deadline.Token) : MediaSnapshot.Empty; }
                    catch (OperationCanceledException) when (!token.IsCancellationRequested)
                    { media = MediaSnapshot.Empty with { Artist = "Media session timed out" }; }
                }
                var audio = Volatile.Read(ref _latestAudio);
                if (token.IsCancellationRequested) break;
                DispatcherQueue.TryEnqueue(() =>
                {
                    if (_closed) return;
                    if (foreground.ProcessId != _lastForeground && foreground.ProcessId != Environment.ProcessId)
                    { _lastForeground = foreground.ProcessId; _revealFullscreen = false; }
                    var local = _localMedia?.Snapshot;
                    _usingLocalMedia = config.ShowMedia && local?.Available == true;
                    ViewModel.Apply(metrics, foreground, _usingLocalMedia ? local! : media, audio);
                    Render();
                    var hide = _userHidden || (config.HideInFullscreen && foreground.ProcessId != Environment.ProcessId && foreground.IsFullscreen && !_revealFullscreen);
                    if (hide && PreviewVisible) { ResetPreview(); Layout(); }
                    if (!_hiding) _windowManager.SetVisible(!hide);
                    UpdateRuntimeDemand();
                });
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested) { break; }
            catch (Exception ex)
            {
                _log.Write("poll", "Integration sampling failed", ex);
                DispatcherQueue.TryEnqueue(() => { if (!_closed) ViewModel.Publish(L.T("An integration failed. See diagnostics."), true); });
            }
            try { await Task.Delay(config.PollingIntervalMs, token); }
            catch (OperationCanceledException) { break; }
        }
    }

    private async void Render()
    {
        ObserveTimerProfile(); UpdateTimerLabels();
        RefreshLiveActivity();
        UpdateRuntimeDemand();
        RefreshContrast();
        RefreshThemeBrushes();
        RefreshProfileBadges();
        if (_renderedProfile != ViewModel.Profile)
        {
            _renderedProfile = ViewModel.Profile; BuildQuickActions(); BuildProfileDock(); ApplyWorkspaceVisibility();
            if (_page == "Utilities") BuildUtilitiesPage();
            Animate(); _ = ReloadLuaUiAsync();
        }
        var metrics = ViewModel.Metrics;
        UpdateTray();
        var power = metrics.Power ?? PowerSnapshot.Unknown;
        BatteryFill.Width = (power.Percent ?? 0) * .12;
        BatteryFill.Fill = ThemeBrush(power.Charging == true ? "PulseAccentBrush" : "MutedBrush");
        BatteryGlyph.Visibility = power.HasBattery == false ? Visibility.Collapsed : Visibility.Visible;
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(CompactBatteryButton, ViewModel.BatteryDetail);
        CpuMetric.Update(_luaStyle.CpuLabel, ViewModel.CpuText, L.T("System utilization"), metrics.Cpu);
        RamMetric.Update(_luaStyle.MemoryLabel, ViewModel.RamText, metrics.TotalMemory > 0
            ? $"{GiB(metrics.TotalMemory - metrics.AvailableMemory):0.0} / {GiB(metrics.TotalMemory):0.0} GB" : "Unavailable", metrics.MemoryPercent);
        PeekCpuMetric.Update(_luaStyle.CpuLabel, ViewModel.CpuText, L.T("System utilization"), metrics.Cpu);
        PeekRamMetric.Update(_luaStyle.MemoryLabel, ViewModel.RamText, metrics.TotalMemory > 0
            ? $"{GiB(metrics.TotalMemory - metrics.AvailableMemory):0.0} / {GiB(metrics.TotalMemory):0.0} GB" : "Unavailable", metrics.MemoryPercent);
        NetworkValue.Text = metrics.ReceiveBytesPerSecond.HasValue
            ? L.F($"↓ {Rate(metrics.ReceiveBytesPerSecond)}    ↑ {Rate(metrics.SendBytesPerSecond)}") : L.T("No reliable sample");
        PeekNetwork.Text = L.T("Network  ") + NetworkValue.Text;
        RenderAudio(ViewModel.Audio);
        var media = ViewModel.Media;
        PreviousButton.IsEnabled = media.Available && media.CanPrevious;
        PlayButton.IsEnabled = media.Available && media.CanPlayPause;
        NextButton.IsEnabled = media.Available && media.CanNext;
        PlayIcon.Glyph = media.Playing ? "\uE769" : "\uE768";
        PeekPreviousButton.IsEnabled = PreviousButton.IsEnabled;
        PeekPlayButton.IsEnabled = PlayButton.IsEnabled;
        PeekNextButton.IsEnabled = NextButton.IsEnabled;
        PeekPlayIcon.Glyph = PlayIcon.Glyph;
        RenderMusicLinks();
        PeekPlaybackState.Text = media.Available ? media.Playing ? L.T("Playing") : L.T("Paused") : L.T("No active session");
        CompactSecondaryLabel.Text = L.T("♫");
        CompactSecondaryValue.Text = media.Available && ViewModel.Settings.ShowMedia ? media.Playing ? L.T("Playing") : L.T("Paused") : L.T("Music");
        if (!ReferenceEquals(_artwork, media.Artwork))
        {
            _artwork = media.Artwork;
            AlbumArt.Source = null;
            PeekAlbumArt.Source = null;
            CompactAlbumArt.Source = null;
            if (_artwork is { Length: > 0 } bytes)
            {
                try
                {
                    using var stream = new InMemoryRandomAccessStream();
                    using (var writer = new DataWriter(stream.GetOutputStreamAt(0))) { writer.WriteBytes(bytes); await writer.StoreAsync(); }
                    stream.Seek(0); var bitmap = new BitmapImage { DecodePixelWidth = 256 }; await bitmap.SetSourceAsync(stream);
                    if (!_closed && ReferenceEquals(_artwork, bytes)) { AlbumArt.Source = bitmap; PeekAlbumArt.Source = bitmap; CompactAlbumArt.Source = bitmap; }
                }
                catch (Exception ex) { _log.Write("media", "Album artwork could not be decoded", ex); }
            }
        }
        var hasArt = ViewModel.Settings.ShowMedia && media.Available && CompactAlbumArt.Source is not null;
        CompactArtworkHost.Visibility = Visibility.Visible;
        CompactSecondaryLabel.Visibility = Visibility.Collapsed;
        CompactTrackTitle.Text = DockPresentation.ShortTitle(ViewModel.MediaTitle);
        UpdateCompactMediaPresentation();
        // The rich hover preview owns this information. Replacing a native tooltip every
        // media refresh dismisses it under a stationary pointer and causes a brief flash.
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetHelpText(CompactMediaButton, ViewModel.MediaTitle + "\n" + ViewModel.MediaArtist);
        if (!_controlCenter && Math.Abs(RequestedRailExtent - _railExtent) > .5) Layout(_motion.IsMoving);
        ApplyWorkspaceVisibility();
        RefreshMediaPreviewLayout();
        RestingArtwork.Source = CompactAlbumArt.Source;
    }

    private void ApplyPreferences()
    {
        var settings = ViewModel.Settings;
        if (_dock.AlwaysShowMiniBar != settings.AlwaysShowMiniBar) _dock.SetAlwaysShowMiniBar(settings.AlwaysShowMiniBar);
        EnsureSearchScope(); RenderWeather();
        _updating = true;
        Shell.RequestedTheme = settings.Theme switch { "Light" => ElementTheme.Light, "System" => ElementTheme.Default, _ => ElementTheme.Dark };
        ApplyGlass(); UpdateTabs();
        ProfilePicker.ItemsSource = new[] { "Automatic" }.Concat(settings.Profiles.Select(settings.ProfileName)).ToArray();
        ProfilePicker.SelectedItem = settings.LockedProfile is { } locked ? settings.ProfileName(locked) : settings.AutomaticSwitching ? "Automatic" : settings.ProfileName(settings.DefaultProfile);
        LockIcon.Glyph = settings.LockedProfile.HasValue ? "\uE72E" : "\uE785";
        ApplyLuaVisuals();
        _ = ReloadLuaUiAsync();
        _updating = false;
        ViewModel.Evaluate(); ViewModel.Refresh();
        ObserveTimerProfile(); UpdateTimerLabels();
        Layout(_motion.IsMoving); UpdateTray();
    }
    private async Task<bool> CommitAsync(Func<PulseSettings, PulseSettings> change)
    {
        if (_closed) return false;
        await _settingsGate.WaitAsync();
        try
        {
            var next = change(ViewModel.Settings).Normalize();
            await _settings.SaveAsync(next, _shutdown.Token);
            ViewModel.Settings = next;
            Volatile.Write(ref _pollSettings, next);
            ApplyPreferences(); BuildQuickActions(); BuildProfileDock(); BuildMusicLinks(); ApplyWorkspaceVisibility();
            return true;
        }
        catch (OperationCanceledException) { return false; }
        catch (Exception ex)
        {
            _log.Write("settings", "Could not save settings", ex);
            ViewModel.Publish(L.T("Settings were not saved: ") + ex.Message, true);
            ApplyPreferences(); return false;
        }
        finally { _settingsGate.Release(); }
    }
    private bool Report(OperationResult result, bool successNotice = true)
    {
        if (_closed) return result.Success;
        if (!result.Success) { _log.Write("operation", result.Message); ViewModel.Publish(result.Message, true); }
        else if (successNotice) { _log.Write("activity", result.Message); ViewModel.Publish(result.Message); }
        return result.Success;
    }
    private void Navigate(string page)
    {
        ResetPreview();
        if (page == "Settings") _settingsCategory = null;
        SettingsNavigationHeader.Visibility = Visibility.Collapsed;
        _page = page; _expanded = true; _dashboard = page != "Overview";
        ContextCard.Visibility = page == "Overview" ? Visibility.Visible : Visibility.Collapsed;
        HomePage.Visibility = page == "Overview" ? Visibility.Visible : Visibility.Collapsed;
        RulesPage.Visibility = page == "Profiles" ? Visibility.Visible : Visibility.Collapsed;
        UtilitiesPage.Visibility = page == "Utilities" ? Visibility.Visible : Visibility.Collapsed;
        SettingsPage.Visibility = page == "Settings" ? Visibility.Visible : Visibility.Collapsed;
        OverviewTab.Opacity = page == "Overview" ? 1 : .5;
        RulesTab.Opacity = page == "Profiles" ? 1 : .5;
        UtilitiesTab.Opacity = page == "Utilities" ? 1 : .65;
        PreferencesTab.Opacity = page == "Settings" ? 1 : .5;
        UpdateTabs();
        if (page == "Profiles") BuildRulesPage();
        if (page == "Utilities") BuildUtilitiesPage();
        if (page == "Settings") BuildSettingsPage();
        ContentScroll.ChangeView(null, 0, null, true);
        Layout(!_controlCenter);
        _motion.Reveal(page == "Overview" ? HomePage : page == "Profiles" ? RulesPage : page == "Utilities" ? UtilitiesPage : SettingsPage, 0, 160, 1);
    }

    private void Expand_Click(object sender, RoutedEventArgs e) => Dock_Click(sender, e);
    private void Collapse_Click(object sender, RoutedEventArgs e) => Collapse();
    private void Hide_Click(object sender, RoutedEventArgs e) => HideHalo();
    private void Overview_Click(object sender, RoutedEventArgs e) => Navigate("Overview");
    private void Rules_Click(object sender, RoutedEventArgs e) => Navigate("Profiles");
    private void Settings_Click(object sender, RoutedEventArgs e) => OpenControlCenter("Settings");
    private void Actions_Click(object sender, RoutedEventArgs e) { Navigate("Profiles"); }
    private void Root_RightTapped(object sender, RightTappedRoutedEventArgs e)
    {
        if (_dialogOpen) return;
        for (var source = e.OriginalSource as DependencyObject; source is not null; source = VisualTreeHelper.GetParent(source))
            if (ReferenceEquals(source, RailObsButton)) return; // Preserve the OBS-specific context menu.
        ResetPreview();
        if (_dock.Stage == DockStage.Compact) _dock.Collapse();
        _quickMenuOpen = true; Layout();
        var menu = new MenuFlyout();
        menu.Closed += (_, _) => { _quickMenuOpen = false; if (!_closed) Layout(); };
        var open = new MenuFlyoutItem { Text = L.T("Open Nagi"), Icon = new FontIcon { Glyph = "\uE8A7" } }; open.Click += (_, _) => Reveal();
        var hide = new MenuFlyoutItem { Text = L.T("Hide Nagi"), Icon = new FontIcon { Glyph = "\uED1A" } }; hide.Click += (_, _) => HideHalo();
        var settings = new MenuFlyoutItem { Text = L.T("Settings"), Icon = new FontIcon { Glyph = "\uE713" } }; settings.Click += (_, _) => OpenControlCenter("Settings");
        var quit = new MenuFlyoutItem { Text = L.T("Quit Nagi"), Icon = new FontIcon { Glyph = "\uE8BB" } }; quit.Click += (_, _) => Close();
        menu.Items.Add(open); menu.Items.Add(hide); menu.Items.Add(settings); menu.Items.Add(new MenuFlyoutSeparator()); menu.Items.Add(quit);
#if NAGI_DEVELOPMENT
        var developer = new MenuFlyoutItem { Text = "Developer tools", Icon = new FontIcon { Glyph = "\uEBE8" } };
        developer.Click += (_, _) => OpenDevelopmentPage("Developer tools"); menu.Items.Insert(3, developer);
#endif
        var point = e.GetPosition(Root);
        if (_windowManager.TryGetPointerPosition(out var x, out var y)) point = new(x, y);
        menu.ShowAt(Root, point); e.Handled = true;
    }
    private void Root_KeyDown(object sender, KeyRoutedEventArgs e)
    {
#if NAGI_DEVELOPMENT
        if (e.Key == VirtualKey.F12) { OpenDevelopmentPage("Developer tools"); e.Handled = true; return; }
#endif
        if (e.Key == VirtualKey.Escape && !_dialogOpen) { Collapse(); e.Handled = true; }
    }
    private async void Profile_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (_updating || ProfilePicker.SelectedIndex < 0) return;
        var selected = ProfilePicker.SelectedItem as string;
        await ActivateProfileAsync(selected is null or "Automatic" ? null : ParseProfile(selected));
    }
    private async void Lock_Click(object sender, RoutedEventArgs e) => await CommitAsync(s => s with { LockedProfile = s.LockedProfile.HasValue ? null : ViewModel.Profile });
    private async void Previous_Click(object sender, RoutedEventArgs e) => Report(await SendMediaAsync("previous"), false);
    private async void Play_Click(object sender, RoutedEventArgs e) => Report(await SendMediaAsync("toggle"), false);
    private async void Next_Click(object sender, RoutedEventArgs e) => Report(await SendMediaAsync("next"), false);
    private async void Mute_Click(object sender, RoutedEventArgs e)
    {
        var result = await Task.Run(() => _audio.SetMute(!_lastAudio.Muted));
        Report(result, false);
    }
    private void Volume_Changed(object sender, RangeBaseValueChangedEventArgs e)
    {
        if (_updating || _closed || sender is not Slider { IsEnabled: true }) return;
        _pendingVolume = e.NewValue;
        if (!_volumeTimer.IsEnabled && !_volumeWriteInFlight) _volumeTimer.Start();
    }
    private async void VolumeTimer_Tick(object? sender, object e)
    {
        _volumeTimer.Stop();
        var volume = _pendingVolume;
        _volumeWriteInFlight = true;
        var result = await Task.Run(() => _audio.SetVolume(volume));
        _volumeWriteInFlight = false;
        if (!_closed && Math.Abs(_pendingVolume - volume) > .1) _volumeTimer.Start();
        Report(result, false);
        if (!result.Success) { _updating = true; VolumeSlider.Value = _lastAudio.Volume ?? 0; _updating = false; }
    }

    private void BuildQuickActions()
    {
        QuickActionsPanel.Children.Clear();
        foreach (var action in ViewModel.Settings.QuickActions.Where(a => !a.Profile.HasValue || a.Profile == ViewModel.Profile))
        {
            var button = new Button { Content = action.Label, CornerRadius = new(10), Margin = new(0, 0, 8, 4), HorizontalAlignment = HorizontalAlignment.Stretch, FontSize = 11 };
            button.Background = ThemeBrush("SurfaceBrush"); button.BorderBrush = ThemeBrush("EdgeBrush");
            _motion.Wire(button);
            ToolTipService.SetToolTip(button, action.Kind + L.T(" · ") + action.Target);
            button.Click += async (_, _) =>
            {
                if (action.Kind == ActionKind.Profile)
                {
                    if (Report(ActionValidator.Validate(action, ViewModel.Settings), false)) await ActivateProfileAsync(Enum.Parse<Profile>(action.Target));
                }
                else if (action.Kind == ActionKind.ToggleHalo)
                {
                    HideHalo();
                }
                else Report(ActionLauncher.Launch(action));
            };
            QuickActionsPanel.Children.Add(button);
        }
        if (QuickActionsPanel.Children.Count < 24)
        {
            var add = new Button { Content = L.T("+ Add a shortcut"), FontSize = 11, CornerRadius = new(10), Margin = new(0, 0, 8, 4), HorizontalAlignment = HorizontalAlignment.Stretch };
            add.Click += async (_, _) => await EditActionAsync(null);
            add.Background = ThemeBrush("InsetBrush"); add.BorderBrush = ThemeBrush("EdgeBrush");
            _motion.Wire(add);
            QuickActionsPanel.Children.Add(add);
        }
    }

    private async Task ShutdownAsync()
    {
        _timerTick.Stop();
        if (_closed) return;
        _closed = true;
        _notchSystemColors.ColorValuesChanged -= NotchColorsChanged;
        _localMedia?.ClosePlayer();
        DismissDockIntroduction(); _visibilityGeneration++; _volumeTimer.Stop(); _updateTimer.Stop(); _shutdown.Cancel();
        _indexTimer.Stop(); _fileWatcher?.Dispose(); _searchCancellation?.Cancel(); _searchWindow?.DisposeWindow();
        ResetPreview();
        _motion.Dispose(); _tray?.Dispose(); _windowManager.Dispose();
        _previewWatch.Stop();
        if (_polling is not null)
        {
            try { await _polling; } catch (OperationCanceledException) { }
        }
        if (_audioPolling is not null) { try { await _audioPolling; } catch (OperationCanceledException) { } }
        _audio.Dispose();
        if (_integrationPolling is not null) { try { await _integrationPolling; } catch (OperationCanceledException) { } }
        _microphone.Dispose();
        _gpu.Dispose();
        if (_weatherPolling is not null) { try { await _weatherPolling; } catch (OperationCanceledException) { } }
        _weatherRefresh.Dispose(); _weather.Dispose();
        if (_searchWork is not null) { try { await _searchWork; } catch (OperationCanceledException) { } }
        _log.Write("lifecycle", "Nagi stopped");
        _settings.Dispose(); _shutdown.Dispose();
    }
    private static double GiB(ulong bytes) => bytes / 1073741824d;
    private static string Rate(double? bytes) => bytes is null ? "—" : bytes >= 1048576 ? $"{bytes / 1048576:0.0} MB/s" : $"{bytes / 1024:0.0} KB/s";
}
