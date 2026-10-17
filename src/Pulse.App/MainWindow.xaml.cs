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
        ProfilePicker.ItemsSource = new[] { "Automatic" }.Concat(ProfileWorkspaces.Profiles.Select(p => p.ToString())).ToArray();
        BuildMusicLinks();
        foreach (var railButton in Compact.Children.OfType<Button>()) railButton.Padding = new(4);
        var presenter = (OverlappedPresenter)AppWindow.Presenter;
        presenter.SetBorderAndTitleBar(false, false);
        presenter.IsResizable = false; presenter.IsMaximizable = false; presenter.IsMinimizable = false;
        presenter.IsAlwaysOnTop = true;
        AppWindow.IsShownInSwitchers = false;
        var handle = WindowNative.GetWindowHandle(this);
        _windowManager = new HaloWindowManager(handle);
        _windowManager.FrameApplied += frame =>
        {
            WindowSurface.SetShape(frame.DropAmount, ViewModel.Settings.Position.Anchor);
            var drop = frame.DropAmount > .001;
            if (_surfaceIsDrop == drop) return;
            _surfaceIsDrop = drop;
            ApplyGlass();
        };
        _motion = new HaloMotion(_windowManager, () => !ViewModel.Settings.ReducedMotion && new UISettings().AnimationsEnabled);
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
            if (!_closed && !_active && !_windowManager.OwnsForeground && _tray?.IsMenuOpen != true && _expanded && !_dialogOpen && !_userHidden && ViewModel.Settings.CloseOnDeactivate) Collapse();
        };
        Closed += async (_, _) => await ShutdownAsync();
        ViewModel.FocusActivated += () => { if (!_controlCenter) Collapse(); };
        ViewModel.PropertyChanged += (_, args) => { if (args.PropertyName == nameof(PulseViewModel.Notice)) Animate(NoticeText); };
        _volumeTimer.Tick += VolumeTimer_Tick;
        InitializePreviews();
        _hotkeyAvailable = Report(_windowManager.RegisterHotkey(configuration.Hotkey), successNotice: false);
        _hideHotkeyAvailable = Report(_windowManager.RegisterHideHotkey(configuration.HideHotkey), successNotice: false);
        InitializeQuickSearch();
        InitializeIntegrations();
        try
        {
            _tray = new SystemTrayService(handle, Path.Combine(AppContext.BaseDirectory, "Assets", "Nagi.Tray.ico"));
            _tray.ShowRequested += Reveal;
            _tray.SearchRequested += ToggleQuickSearch;
            _tray.HideRequested += HideHalo;
            _tray.SettingsRequested += () => OpenControlCenter("Settings");
            _tray.QuitRequested += Close;
            _tray.MenuOpening += () =>
            {
                ResetPreview();
                if (_dock.Stage == DockStage.Compact && !_dock.Pinned) _dock.Collapse();
                Layout(); // Settle the owner before its native menu enters the modal loop.
            };
            _tray.ProfileRequested += async profile => await ActivateProfileAsync(profile);
            _tray.RegistrationChanged += result => { Report(result, false); UpdateTray(); };
            Report(_tray.Register(), false);
        }
        catch (Exception ex) { _log.Write("tray", "Tray initialization failed", ex); ViewModel.Publish("Tray unavailable. Relaunch Nagi to recover Nagi.", true); }
        Shell.ActualThemeChanged += (_, _) => { ApplyGlass(); UpdateTabs(); ApplyLuaVisuals(); _ = ReloadLuaUiAsync(); };
        // HighContrastChanged registration can fail for unpackaged desktop hosts.
        // Observe the system preference on the existing UI refresh instead.
        _lastHighContrast = _themeAccessibility.HighContrast;
        Root.Loaded += (_, _) => { WireButtons(Root); ApplyLuaVisuals(); };
        _expanded = configuration.StartExpanded && ViewModel.Profile != Profile.Focus;
        ApplyPreferences();
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
        ApplyThemeBackground();
        if (!_closed && !_expanded && !_userHidden) _previewWatch.Start(); else _previewWatch.Stop();
        var wasExpanded = Expanded.Visibility == Visibility.Visible;
        var wasPreview = PeekHost.Visibility == Visibility.Visible;
        var wasDrop = EdgeDrop.Visibility == Visibility.Visible;
        var resting = _dock.Stage == DockStage.Resting;
        var frame = _controlCenter ? _windowManager.PrepareControlCenter(ViewModel.Settings.Position)
            : _windowManager.PrepareLayout(_expanded, _dashboard, ViewModel.Settings.Position, PreviewVisible ? _preview == PreviewKind.Media ? 380 : 336 : 84, resting);
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
            if (animate) { _motion.Reveal(Expanded, 0, 260, .985f); RevealCards(); }
            else _motion.Reset(Expanded);
        }
        else
        {
            Compact.Visibility = Visibility.Visible; Expanded.IsHitTestVisible = false;
            if (animate && (wasExpanded || wasDrop)) { _motion.Fade(Expanded, false, 95); _motion.Fade(EdgeDrop, false, 90); _motion.Reveal(Compact, 0, 220); }
            else { Expanded.Visibility = Visibility.Collapsed; _motion.Reset(Compact); }
            PeekHost.IsHitTestVisible = PreviewVisible;
            if (PreviewVisible)
            {
                PeekHost.Visibility = Visibility.Visible;
                if (animate) _motion.Reveal(PeekHost, 0, 220, .99f); else _motion.Reset(PeekHost);
            }
            else if (animate && wasPreview) _motion.Fade(PeekHost, false, 90);
            else PeekHost.Visibility = Visibility.Collapsed;
        }
        Shell.CornerRadius = new(0); // DWM clips the outer corners; avoid an opaque seam behind a second XAML clip.
        _motion.Move(frame, animate && !_userHidden, () =>
        {
            if (!_expanded) Expanded.Visibility = Visibility.Collapsed;
            if (!PreviewVisible) PeekHost.Visibility = Visibility.Collapsed;
            if (_dock.Stage != DockStage.Resting) EdgeDrop.Visibility = Visibility.Collapsed;
            if (_dock.Stage == DockStage.Resting) Compact.Visibility = Visibility.Collapsed;
        });
        if (_userHidden && !_hiding) _windowManager.SetVisible(false);
    }
    private void Animate(FrameworkElement? target = null)
    {
        if (_closed) return;
        _motion.Reveal(target ?? (_expanded ? Expanded : _dock.Stage == DockStage.Resting ? EdgeDrop : Compact), duration: 200, scale: .99f);
    }
    private void RevealCards()
    {
        if (_page != "Overview") return;
        _motion.Reveal(MetricsGrid, 95, 260);
        _motion.Reveal(MediaCard, 125, 280);
        _motion.Reveal(AudioCard, 150, 280);
        _motion.Reveal(QuickActionsPanel, 170, 280);
    }
    private async void HideHalo()
    {
        DismissDockIntroduction();
        if (_closed || _dialogOpen || _userHidden) return;
        if (!_hotkeyAvailable && !_hideHotkeyAvailable && _tray?.IsRegistered != true)
        { ViewModel.Publish("Set a working shortcut or restore the system tray before hiding Nagi.", true); return; }
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
        _tray?.Update(ViewModel.Profile, ViewModel.Settings.LockedProfile ?? (ViewModel.Settings.AutomaticSwitching ? null : ViewModel.Profile), _userHidden, PulseViewModel.FormatHotkey(ViewModel.Settings.HideHotkey));
        ToolTipService.SetToolTip(HideHeaderButton, "Hide Nagi · " + (_hideHotkeyAvailable ? PulseViewModel.FormatHotkey(ViewModel.Settings.HideHotkey) : "Shortcut unavailable"));
        TrayStatusText.Text = _tray?.IsRegistered == true ? "In system tray" : "Local & private";
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
        return (Brush)((ResourceDictionary)dictionary.ThemeDictionaries[theme])[key];
    }
    private void ApplyGlass()
    {
        _glass.SetAkeno(ViewModel.Settings.Theme == "Akeno");
        _glass.SetLight(Shell.ActualTheme == ElementTheme.Light);
        if (!_surfaceIsDrop && ViewModel.Settings.GlassEnabled && HaloAcrylicBackdrop.IsSupported && !_themeAccessibility.HighContrast)
        { if (!ReferenceEquals(SystemBackdrop, _glass)) SystemBackdrop = _glass; Shell.Background = ThemeBrush("CanvasBrush"); }
        else
        {
            SystemBackdrop = null;
            Shell.Background = _themeAccessibility.HighContrast ? ThemeBrush("CanvasBrush")
                : new SolidColorBrush(ViewModel.Settings.Theme == "Akeno" ? global::Windows.UI.Color.FromArgb(255, 16, 11, 13)
                    : Shell.ActualTheme == ElementTheme.Light ? global::Windows.UI.Color.FromArgb(255, 241, 244, 249) : global::Windows.UI.Color.FromArgb(255, 27, 30, 40));
        }
        if (_surfaceIsDrop) Shell.Background = new SolidColorBrush(Microsoft.UI.Colors.Transparent);
        Root.Background = _surfaceIsDrop || ViewModel.Settings.Theme == "Akeno"
            ? new SolidColorBrush(Microsoft.UI.Colors.Transparent) : ThemeBrush("HeaderGlowBrush");
        UpdateSurfaceMaterial();
    }
    private void UpdateTabs()
    {
        var clear = new SolidColorBrush(Microsoft.UI.Colors.Transparent);
        OverviewTab.Background = _page == "Overview" ? ThemeBrush("SelectedTabBrush") : clear;
        RulesTab.Background = _page == "Profiles" ? ThemeBrush("SelectedTabBrush") : clear;
        PreferencesTab.Background = _page == "Settings" ? ThemeBrush("SelectedTabBrush") : clear;
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
                    ViewModel.Apply(metrics, foreground, media, audio);
                    Render();
                    var hide = _userHidden || (config.HideInFullscreen && foreground.ProcessId != Environment.ProcessId && foreground.IsFullscreen && !_revealFullscreen);
                    if (hide && PreviewVisible) { ResetPreview(); Layout(); }
                    if (!_hiding) _windowManager.SetVisible(!hide);
                });
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested) { break; }
            catch (Exception ex)
            {
                _log.Write("poll", "Integration sampling failed", ex);
                DispatcherQueue.TryEnqueue(() => { if (!_closed) ViewModel.Publish("An integration failed. See diagnostics.", true); });
            }
            try { await Task.Delay(config.PollingIntervalMs, token); }
            catch (OperationCanceledException) { break; }
        }
    }

    private async void Render()
    {
        RefreshContrast();
        if (_renderedProfile != ViewModel.Profile) { _renderedProfile = ViewModel.Profile; BuildQuickActions(); BuildProfileDock(); ApplyWorkspaceVisibility(); Animate(); _ = ReloadLuaUiAsync(); }
        var metrics = ViewModel.Metrics;
        UpdateTray();
        var power = metrics.Power ?? PowerSnapshot.Unknown;
        BatteryFill.Width = (power.Percent ?? 0) * .12;
        BatteryFill.Fill = ThemeBrush(power.Charging == true ? "PulseAccentBrush" : "MutedBrush");
        BatteryGlyph.Visibility = power.HasBattery == false ? Visibility.Collapsed : Visibility.Visible;
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(CompactBatteryButton, power.Detail);
        CpuMetric.Update(_luaStyle.CpuLabel, ViewModel.CpuText, "System utilization", metrics.Cpu);
        RamMetric.Update(_luaStyle.MemoryLabel, ViewModel.RamText, metrics.TotalMemory > 0
            ? $"{GiB(metrics.TotalMemory - metrics.AvailableMemory):0.0} / {GiB(metrics.TotalMemory):0.0} GB" : "Unavailable", metrics.MemoryPercent);
        PeekCpuMetric.Update(_luaStyle.CpuLabel, ViewModel.CpuText, "System utilization", metrics.Cpu);
        PeekRamMetric.Update(_luaStyle.MemoryLabel, ViewModel.RamText, metrics.TotalMemory > 0
            ? $"{GiB(metrics.TotalMemory - metrics.AvailableMemory):0.0} / {GiB(metrics.TotalMemory):0.0} GB" : "Unavailable", metrics.MemoryPercent);
        NetworkValue.Text = metrics.ReceiveBytesPerSecond.HasValue
            ? $"↓ {Rate(metrics.ReceiveBytesPerSecond)}    ↑ {Rate(metrics.SendBytesPerSecond)}" : "No reliable sample";
        PeekNetwork.Text = "Network  " + NetworkValue.Text;
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
        PeekPlaybackState.Text = media.Available ? media.Playing ? "Playing" : "Paused" : "No active session";
        CompactSecondaryLabel.Text = "♫";
        CompactSecondaryValue.Text = media.Available && ViewModel.Settings.ShowMedia ? media.Playing ? "Playing" : "Paused" : "Music";
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
        CompactArtworkHost.Visibility = hasArt ? Visibility.Visible : Visibility.Collapsed;
        CompactSecondaryLabel.Visibility = hasArt ? Visibility.Collapsed : Visibility.Visible;
        ApplyWorkspaceVisibility();
    }

    private void ApplyPreferences()
    {
        var settings = ViewModel.Settings;
        EnsureSearchScope(); RenderWeather();
        _updating = true;
        Shell.RequestedTheme = settings.Theme switch { "Light" => ElementTheme.Light, "System" => ElementTheme.Default, _ => ElementTheme.Dark };
        ApplyGlass(); UpdateTabs();
        ProfilePicker.SelectedItem = settings.LockedProfile is { } locked ? locked.ToString() : settings.AutomaticSwitching ? "Automatic" : settings.DefaultProfile.ToString();
        LockIcon.Glyph = settings.LockedProfile.HasValue ? "\uE72E" : "\uE785";
        ApplyLuaVisuals();
        _ = ReloadLuaUiAsync();
        _updating = false;
        ViewModel.Evaluate(); ViewModel.Refresh();
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
            ViewModel.Publish("Settings were not saved: " + ex.Message, true);
            ApplyPreferences(); return false;
        }
        finally { _settingsGate.Release(); }
    }
    private bool Report(OperationResult result, bool successNotice = true)
    {
        if (_closed) return result.Success;
        if (!result.Success) { _log.Write("operation", result.Message); ViewModel.Publish(result.Message, true); }
        else if (successNotice) ViewModel.Publish(result.Message);
        return result.Success;
    }
    private void Navigate(string page)
    {
        ResetPreview();
        if (page == "Settings") _settingsCategory = null;
        SettingsNavigationHeader.Visibility = Visibility.Collapsed;
        _page = page; _expanded = true; _dashboard = page != "Overview";
        HomePage.Visibility = page == "Overview" ? Visibility.Visible : Visibility.Collapsed;
        RulesPage.Visibility = page == "Profiles" ? Visibility.Visible : Visibility.Collapsed;
        SettingsPage.Visibility = page == "Settings" ? Visibility.Visible : Visibility.Collapsed;
        OverviewTab.Opacity = page == "Overview" ? 1 : .5;
        RulesTab.Opacity = page == "Profiles" ? 1 : .5;
        PreferencesTab.Opacity = page == "Settings" ? 1 : .5;
        UpdateTabs();
        if (page == "Profiles") BuildRulesPage();
        if (page == "Settings") BuildSettingsPage();
        ContentScroll.ChangeView(null, 0, null, true);
        Layout(true);
        _motion.Reveal(page == "Overview" ? HomePage : page == "Profiles" ? RulesPage : SettingsPage, 50, 220);
    }

    private void Expand_Click(object sender, RoutedEventArgs e) => Dock_Click(sender, e);
    private void Collapse_Click(object sender, RoutedEventArgs e) => Collapse();
    private void Hide_Click(object sender, RoutedEventArgs e) => HideHalo();
    private void Overview_Click(object sender, RoutedEventArgs e) => Navigate("Overview");
    private void Rules_Click(object sender, RoutedEventArgs e) => Navigate("Profiles");
    private void Settings_Click(object sender, RoutedEventArgs e) => OpenControlCenter("Settings");
    private void Actions_Click(object sender, RoutedEventArgs e) { Navigate("Profiles"); }
    private void Drag_PointerPressed(object sender, PointerRoutedEventArgs e)
    { if (e.GetCurrentPoint(null).Properties.IsLeftButtonPressed) { e.Handled = true; ResetPreview(); Layout(); _motion.Stop(); _windowManager.Drag(); } }
    private void Root_RightTapped(object sender, RightTappedRoutedEventArgs e)
    {
        if (_dialogOpen) return;
        ResetPreview();
        if (_dock.Stage == DockStage.Compact && !_dock.Pinned) _dock.Collapse();
        _quickMenuOpen = true; Layout();
        var menu = new MenuFlyout();
        menu.Closed += (_, _) => { _quickMenuOpen = false; if (!_closed) Layout(); };
        var open = new MenuFlyoutItem { Text = "Open Nagi", Icon = new FontIcon { Glyph = "\uE8A7" } }; open.Click += (_, _) => Reveal();
        var hide = new MenuFlyoutItem { Text = "Hide Nagi", Icon = new FontIcon { Glyph = "\uED1A" } }; hide.Click += (_, _) => HideHalo();
        var settings = new MenuFlyoutItem { Text = "Settings", Icon = new FontIcon { Glyph = "\uE713" } }; settings.Click += (_, _) => OpenControlCenter("Settings");
        var quit = new MenuFlyoutItem { Text = "Quit Nagi", Icon = new FontIcon { Glyph = "\uE8BB" } }; quit.Click += (_, _) => Close();
        menu.Items.Add(open); menu.Items.Add(hide); menu.Items.Add(settings); menu.Items.Add(new MenuFlyoutSeparator()); menu.Items.Add(quit);
        var point = e.GetPosition(Root);
        if (_windowManager.TryGetPointerPosition(out var x, out var y)) point = new(x, y);
        menu.ShowAt(Root, point); e.Handled = true;
    }
    private void Root_KeyDown(object sender, KeyRoutedEventArgs e) { if (e.Key == VirtualKey.Escape && !_dialogOpen) { Collapse(); e.Handled = true; } }
    private async void Profile_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (_updating || ProfilePicker.SelectedIndex < 0) return;
        var selected = ProfilePicker.SelectedItem as string;
        await ActivateProfileAsync(Enum.TryParse<Profile>(selected, out var profile) ? profile : null);
    }
    private async void Lock_Click(object sender, RoutedEventArgs e) => await CommitAsync(s => s with { LockedProfile = s.LockedProfile.HasValue ? null : ViewModel.Profile });
    private async void Previous_Click(object sender, RoutedEventArgs e) => Report(await _media.SendAsync("previous"), false);
    private async void Play_Click(object sender, RoutedEventArgs e) => Report(await _media.SendAsync("toggle"), false);
    private async void Next_Click(object sender, RoutedEventArgs e) => Report(await _media.SendAsync("next"), false);
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
            ToolTipService.SetToolTip(button, action.Kind + " · " + action.Target);
            button.Click += async (_, _) =>
            {
                if (action.Kind == ActionKind.Profile)
                {
                    if (Report(ActionValidator.Validate(action), false)) await ActivateProfileAsync(Enum.Parse<Profile>(action.Target));
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
            var add = new Button { Content = "+ Add a shortcut", FontSize = 11, CornerRadius = new(10), Margin = new(0, 0, 8, 4), HorizontalAlignment = HorizontalAlignment.Stretch };
            add.Click += async (_, _) => await EditActionAsync(null);
            add.Background = ThemeBrush("InsetBrush"); add.BorderBrush = ThemeBrush("EdgeBrush");
            _motion.Wire(add);
            QuickActionsPanel.Children.Add(add);
        }
    }

    private async Task ShutdownAsync()
    {
        if (_closed) return;
        _closed = true; DismissDockIntroduction(); _visibilityGeneration++; _volumeTimer.Stop(); _updateTimer.Stop(); _logoDragTimer?.Stop(); _shutdown.Cancel();
        _indexTimer.Stop(); _searchCancellation?.Cancel(); _searchWindow?.DisposeWindow();
        ResetPreview();
        _motion.Dispose(); _tray?.Dispose(); _windowManager.Dispose();
        _previewWatch.Stop();
        if (_polling is not null)
        {
            try { await _polling; } catch (OperationCanceledException) { }
        }
        if (_audioPolling is not null) { try { await _audioPolling; } catch (OperationCanceledException) { } }
        _audio.Dispose(); _microphone.Dispose();
        if (_integrationPolling is not null) { try { await _integrationPolling; } catch (OperationCanceledException) { } }
        _gpu.Dispose();
        if (_weatherPolling is not null) { try { await _weatherPolling; } catch (OperationCanceledException) { } }
        _weather.Dispose();
        if (_searchWork is not null) { try { await _searchWork; } catch (OperationCanceledException) { } }
        _log.Write("lifecycle", "Nagi stopped");
        _settings.Dispose(); _shutdown.Dispose();
    }
    private static double GiB(ulong bytes) => bytes / 1073741824d;
    private static string Rate(double? bytes) => bytes is null ? "—" : bytes >= 1048576 ? $"{bytes / 1048576:0.0} MB/s" : $"{bytes / 1024:0.0} KB/s";
}
