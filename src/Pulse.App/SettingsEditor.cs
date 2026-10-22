using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Pulse.Core;
using Pulse.Windows;
using Windows.System;

namespace Pulse.App;

public sealed partial class MainWindow
{
    private static TextBlock Heading(string title, string? detail = null) => new()
    {
        Text = detail is null ? title : title + "\n" + detail,
        FontSize = detail is null ? 17 : 13, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
        TextWrapping = TextWrapping.Wrap, Margin = new(0, 4, 0, 4)
    };
    private static TextBlock Note(string text) => new() { Text = text, FontSize = 11, Opacity = .82, TextWrapping = TextWrapping.Wrap };
    private static Border Card(UIElement content) => new() { Style = (Style)Application.Current.Resources["Card"], Child = content };
    private static StackPanel Stack() => new() { Spacing = 10 };
    private Button Button(string label, RoutedEventHandler action)
    {
        var button = new Button { Content = label, CornerRadius = new(8), FontSize = 12 };
        button.Click += action; _motion.Wire(button); return button;
    }
    private static ComboBox Select(IEnumerable<string> items, string selected, string name)
    {
        var box = new ComboBox { ItemsSource = items.ToArray(), SelectedItem = selected, HorizontalAlignment = HorizontalAlignment.Stretch, MinWidth = 120, FontSize = 12 };
        L.Options(box); AutomationProperties.SetName(box, L.T(name)); return box;
    }
    private static UIElement Labeled(string label, UIElement input)
    {
        var stack = Stack(); stack.Spacing = 6; stack.Children.Add(Note(label)); stack.Children.Add(input); return stack;
    }
    private ToggleSwitch Toggle(string title, string detail, bool value, Func<PulseSettings, bool, PulseSettings> change)
    {
        var toggle = new ToggleSwitch { Header = title, IsOn = value, FontSize = 12, OnContent = L.T("On"), OffContent = L.T("Off") };
        AutomationProperties.SetName(toggle, title);
        ToolTipService.SetToolTip(toggle, detail);
        toggle.Toggled += async (_, _) =>
        {
            if (_updating) return;
            if (!await CommitAsync(s => change(s, toggle.IsOn))) BuildSettingsPage();
        };
        return toggle;
    }

    private void BuildRulesPage()
    {
        WorkspacePage.Children.Clear();
        if (_editingWorkspace is { } editing) BuildWorkspaceEditor(editing);
        else BuildProfileList();
    }

    private async Task EditRuleAsync(AppRule? existing, Profile? owner = null)
    {
        var identity = new TextBox { Header = L.T("Process name or full executable path"), Text = existing?.Identity ?? (owner.HasValue ? "" : ViewModel.Foreground.ExecutablePath ?? (ViewModel.Foreground.Name + L.T(".exe"))), PlaceholderText = L.T("Code.exe or C:\\Apps\\Editor.exe") };
        var profile = Select(ViewModel.Settings.Profiles.Select(ViewModel.Settings.ProfileName), ViewModel.Settings.ProfileName(owner ?? existing?.Profile ?? Profile.Work), "Rule profile");
        AutomationProperties.SetAutomationId(identity, "ProfileRuleExecutable");
        var priority = new NumberBox { Header = L.T("Priority (higher wins)"), Minimum = 0, Maximum = 10000, Value = existing?.Priority ?? 100, SpinButtonPlacementMode = NumberBoxSpinButtonPlacementMode.Compact };
        var enabled = new ToggleSwitch { Header = L.T("Rule enabled"), IsOn = existing?.Enabled ?? true };
        var error = Note(""); var content = Stack();
        content.Children.Add(identity);
        content.Children.Add(Button(L.T("Browse executable"), async (_, _) =>
        {
            try
            {
                var files = await LocalMediaFilePicker.PickExecutableAsync(WinRT.Interop.WindowNative.GetWindowHandle(this), L.T("Choose executable"));
                if (files.Length > 0) identity.Text = files[0];
            }
            catch (Exception ex) { error.Text = L.T("Could not open file picker."); _log.Write("profiles", "Executable picker failed", ex); }
        }));
        if (owner is null) content.Children.Add(Labeled(L.T("Profile"), profile));
        content.Children.Add(priority); content.Children.Add(enabled); content.Children.Add(error);
        var dialog = Dialog(existing is null ? L.T("Add application rule") : L.T("Edit application rule"), content);
        dialog.PrimaryButtonClick += async (_, args) =>
        {
            var value = identity.Text.Trim();
            if (value.Length == 0 || value.Length > 32768 || value.IndexOfAny(['*', '?', '"', '\0']) >= 0 || !double.IsFinite(priority.Value))
            { error.Text = L.T("Enter an exact executable name or path and a valid priority."); args.Cancel = true; return; }
            var rule = new AppRule(value, ParseProfile((string)profile.SelectedItem), (int)priority.Value, enabled.IsOn);
            var deferral = args.GetDeferral();
            try
            {
                if (!await CommitAsync(s => s with { Rules = existing is null ? [.. s.Rules, rule] : s.Rules.Select(r => r == existing ? rule : r).ToArray() }))
                { error.Text = L.T("The rule could not be saved."); args.Cancel = true; }
            }
            finally { deferral.Complete(); }
        };
        await ShowDialogAsync(dialog); BuildRulesPage();
    }

    private async Task EditActionAsync(QuickAction? existing)
    {
        var label = new TextBox { Header = L.T("Name"), Text = existing?.Label ?? "", MaxLength = 40, PlaceholderText = L.T("My project") };
        var kinds = Enum.GetValues<ActionKind>();
        static string KindLabel(ActionKind value) => value == ActionKind.ToggleHalo ? "Toggle Nagi" : value.ToString();
        var kind = Select(kinds.Select(KindLabel), KindLabel(existing?.Kind ?? ActionKind.Folder), "Action type");
        var target = new TextBox { Header = L.T("Target"), Text = existing?.Target ?? "", PlaceholderText = L.T("Absolute path, URL, or profile name") };
        var filter = Select(new[] { "All profiles" }.Concat(ViewModel.Settings.Profiles.Select(ViewModel.Settings.ProfileName)), existing?.Profile is { } filterProfile ? ViewModel.Settings.ProfileName(filterProfile) : "All profiles", "Visible in profile");
        var error = Note(""); var content = Stack();
        content.Children.Add(label); content.Children.Add(Labeled(L.T("Action type"), kind)); content.Children.Add(target);
        content.Children.Add(Note(L.T("Application: local .exe, no arguments. Profile: Default, Work, Gaming, Stream or Focus. Toggle Nagi: leave target empty. Scripts and command hosts are blocked.")));
        content.Children.Add(Labeled(L.T("Show this action in"), filter)); content.Children.Add(error);
        var dialog = Dialog(existing is null ? L.T("Add quick action") : L.T("Edit quick action"), content);
        dialog.PrimaryButtonClick += async (_, args) =>
        {
            var action = new QuickAction(label.Text.Trim(), kinds[kind.SelectedIndex], target.Text.Trim(),
                filter.SelectedIndex == 0 ? null : ParseProfile((string)filter.SelectedItem));
            if (action.Kind == ActionKind.Profile && ViewModel.Settings.Profiles.Any(p => ViewModel.Settings.ProfileName(p).Equals(action.Target, StringComparison.OrdinalIgnoreCase)))
                action = action with { Target = ((int)ViewModel.Settings.Profiles.First(p => ViewModel.Settings.ProfileName(p).Equals(action.Target, StringComparison.OrdinalIgnoreCase))).ToString() };
            var validation = ActionValidator.Validate(action, ViewModel.Settings);
            if (!validation.Success) { error.Text = validation.Message; args.Cancel = true; return; }
            var deferral = args.GetDeferral();
            try
            {
                if (!await CommitAsync(s => s with { QuickActions = existing is null ? [.. s.QuickActions, action] : s.QuickActions.Select(a => a == existing ? action : a).ToArray() }))
                { error.Text = L.T("The action could not be saved."); args.Cancel = true; }
            }
            finally { deferral.Complete(); }
        };
        await ShowDialogAsync(dialog); BuildRulesPage();
    }

    private void BuildAppearanceSettings()
    {
        var appearance = Stack();
        appearance.Children.Add(Heading(L.T("Appearance & placement")));
        var theme = Select(new[] { "Glass (system)", "Akeno", "Dark", "Light" }, ViewModel.Settings.Theme == "System" ? "Glass (system)" : ViewModel.Settings.Theme, "Theme");
        theme.SelectionChanged += async (_, _) =>
        {
            if (theme.SelectedItem is string selected && await CommitAsync(s => s with
                { Theme = selected == "Glass (system)" ? "System" : selected, GlassEnabled = selected == "Glass (system)" || s.GlassEnabled })) BuildSettingsPage();
        };
        appearance.Children.Add(Labeled(L.T("Theme"), theme));
        var glass = Toggle(L.T("Translucent glass"), L.T("Native acrylic follows Windows transparency and contrast preferences."), Themes.AppAppearance.UsesGlass(ViewModel.Settings), (s, v) => s with { GlassEnabled = v });
        if (ViewModel.Settings.Theme != "System") appearance.Children.Add(glass);
        appearance.Children.Add(Note(L.T("Glass follows Windows light and dark mode. Native transparency respects Windows accessibility preferences.")));
        SettingsPage.Children.Add(Card(appearance));
        BuildVisualizerSettings();
        var color = Stack(); BuildBackgroundAppearance(color);
        if (color.Children.Count > 0) SettingsPage.Children.Add(Card(color));
        appearance = Stack();
        if (ViewModel.Settings.Theme == "Akeno")
        {
        var background = Select(new[] { "Samurai", "Gradient", "None" }, ViewModel.Settings.AkenoBackground, "Akeno background");
        background.SelectionChanged += async (_, _) => { if (background.SelectedItem is string selected) await CommitAsync(s => s with { AkenoBackground = selected }); };
        appearance.Children.Add(Labeled(L.T("Akeno background · choose None to hide"), background));
        appearance.Children.Add(Note(L.T("The generated samurai artwork appears behind expanded Akeno panels. Choose Gradient for a simple red wash.")));
        SettingsPage.Children.Add(Card(appearance)); appearance = Stack();
        }
        appearance.Children.Add(Heading(L.T("Dock & display")));
        var orientation = Select(new[] { "Vertical", "Horizontal" }, ViewModel.Settings.Position.Orientation, "Dock layout");
        orientation.SelectionChanged += async (_, _) =>
        {
            if (orientation.SelectedItem is not string selected) return;
            await CommitAsync(s => s with { Position = s.Position with { Orientation = selected, Anchor = selected == "Horizontal" ? "Bottom edge" : "Right edge", OffsetX = 0, OffsetY = 0 } });
            BuildSettingsPage();
        };
        appearance.Children.Add(Labeled(L.T("Dock layout"), orientation));
        var edges = ViewModel.Settings.Position.Orientation == "Horizontal" ? new[] { "Top edge", "Bottom edge" } : new[] { "Right edge", "Left edge" };
        var anchors = Select(edges, ViewModel.Settings.Position.Anchor, "Nagi position");
        anchors.SelectionChanged += async (_, _) =>
        { if (anchors.SelectedItem is string selected) await CommitAsync(s => s with { Position = s.Position with { Anchor = selected, OffsetX = 0, OffsetY = 0 } }); };
        appearance.Children.Add(Labeled(L.T("Screen edge · drag the logo to reposition"), anchors));
        var monitors = HaloWindowManager.GetMonitors();
        var displayNames = new[] { "Primary display" }.Concat(monitors.Select(m => m.Label)).ToArray();
        var selectedDisplay = monitors.FirstOrDefault(m => m.Device == ViewModel.Settings.Position.Monitor)?.Label ?? "Primary display";
        var display = Select(displayNames, selectedDisplay, "Preferred display");
        display.SelectionChanged += async (_, _) =>
        {
            var device = display.SelectedIndex <= 0 ? "" : monitors[display.SelectedIndex - 1].Device;
            await CommitAsync(s => s with { Position = s.Position with { Monitor = device, OffsetX = 0, OffsetY = 0 } });
        };
        appearance.Children.Add(Labeled(L.T("Display"), display));
        appearance.Children.Add(Toggle(L.T("Reduce motion"), L.T("Windows animation preferences are also respected."), ViewModel.Settings.ReducedMotion, (s, v) => s with { ReducedMotion = v }));
        SettingsPage.Children.Add(Card(appearance));
    }

    private void BuildBehaviorSettings()
    {
        var behavior = Stack(); behavior.Children.Add(Heading(L.T("Behavior")));
        var cadence = Select(["Display", "Efficiency"], ViewModel.Settings.AnimationMode, "Animation mode");
        cadence.SelectionChanged += async (_, _) => { if (cadence.SelectedItem is string mode) await CommitAsync(s => s with { AnimationMode = mode }); };
        behavior.Children.Add(Labeled(L.T("Animation mode"), cadence));
        behavior.Children.Add(Note(L.T("Display follows your monitor refresh rate, up to 240 Hz. Efficiency caps transitions at 60 Hz. Animations stop when settled.")));
        behavior.Children.Add(Toggle(L.T("Dismiss when clicking outside"), L.T("Keep the panel open by turning this off."), ViewModel.Settings.CloseOnDeactivate, (s, v) => s with { CloseOnDeactivate = v }));
        behavior.Children.Add(Toggle(L.T("Hide over fullscreen apps"), L.T("Applies to any fullscreen app; does not change profile rules."), ViewModel.Settings.HideInFullscreen, (s, v) => s with { HideInFullscreen = v }));
        behavior.Children.Add(Toggle(L.T("Open expanded on startup"), L.T("Default is a small water drop attached to the screen edge."), ViewModel.Settings.StartExpanded, (s, v) => s with { StartExpanded = v }));
        behavior.Children.Add(Toggle(L.T("Show media controls"), L.T("Read supported Windows media sessions."), ViewModel.Settings.ShowMedia, (s, v) => s with { ShowMedia = v }));
        behavior.Children.Add(Toggle(L.T("Contextual notifications"), L.T("Focus suppresses nonessential notices."), ViewModel.Settings.Notifications, (s, v) => s with { Notifications = v }));
        var interval = new NumberBox { Header = L.T("Polling interval (milliseconds)"), Minimum = 500, Maximum = 10000, SmallChange = 500, Value = ViewModel.Settings.PollingIntervalMs, SpinButtonPlacementMode = NumberBoxSpinButtonPlacementMode.Compact };
        behavior.Children.Add(interval);
        behavior.Children.Add(Button(L.T("Apply polling interval"), async (_, _) =>
        {
            if (double.IsFinite(interval.Value)) await CommitAsync(s => s with { PollingIntervalMs = (int)interval.Value });
            else ViewModel.Publish(L.T("Enter a valid polling interval."), true);
        }));
        behavior.Children.Add(Button(L.T("Change global shortcut · ") + ViewModel.HotkeyText, async (_, _) => await EditHotkeyAsync()));
        behavior.Children.Add(Button(L.T("Hide / show shortcut · ") + Pulse.App.ViewModels.PulseViewModel.FormatHotkey(ViewModel.Settings.HideHotkey), async (_, _) => await EditHotkeyAsync(true)));
        behavior.Children.Add(Note(L.T("The first shortcut expands or collapses Nagi. The hide/show shortcut removes Nagi from the desktop and restores it on the next press. The tray icon stays available for Settings and Quit.")));
        var startup = new ToggleSwitch { Header = L.T("Start Nagi when I sign in"), IsOn = ViewModel.Settings.StartWithWindows };
        startup.Toggled += async (_, _) =>
        {
            if (_updating) return;
            var previous = ViewModel.Settings.StartWithWindows;
            var success = Report(ActionLauncher.ConfigureStartup(startup.IsOn, Environment.ProcessPath!));
            if (success && !await CommitAsync(s => s with { StartWithWindows = startup.IsOn })) ActionLauncher.ConfigureStartup(previous, Environment.ProcessPath!);
            BuildSettingsPage();
        };
        behavior.Children.Add(startup); behavior.Children.Add(Note(L.T("Startup is per-user and opt-in. Re-enable it if you move the executable.")));
        SettingsPage.Children.Add(Card(behavior));

    }

    private void BuildDiagnosticsSettings()
    {
        var thanks = Stack(); thanks.Children.Add(Heading(L.T("Thanks")));
        var reddit = new HyperlinkButton { Content = "u/alkidnn · Reddit", NavigateUri = new("https://www.reddit.com/user/alkidnn/"),
            Padding = new(0), HorizontalAlignment = HorizontalAlignment.Left, Foreground = ThemeBrush("PulseAccentBrush") };
        AutomationProperties.SetAutomationId(reddit, "AboutRedditCredit");
        thanks.Children.Add(reddit);
        var acknowledgment = Note(L.T("Thank you for the suggestions to improve Nagi."));
        AutomationProperties.SetAutomationId(acknowledgment, "AboutAcknowledgment"); thanks.Children.Add(acknowledgment);
        SettingsPage.Children.Add(Card(thanks));
        var diagnostics = Stack(); diagnostics.Children.Add(Heading(L.T("Integrations & diagnostics")));
        diagnostics.Children.Add(Note(L.F($"Nagi {CurrentVersion.ToString(3)} · WinUI 3 · .NET {Environment.Version}\nSession uptime: {_uptime.Elapsed:hh\\:mm\\:ss}\nForeground: {ViewModel.Foreground.Name} · PID {ViewModel.Foreground.ProcessId}")));
        diagnostics.Children.Add(Note(L.T("CPU: ") + (ViewModel.Metrics.Cpu.HasValue ? L.T("available · GetSystemTimes") : L.T("waiting / unavailable"))
            + L.T("\nMemory: ") + (ViewModel.Metrics.TotalMemory > 0 ? L.F($"{GiB(ViewModel.Metrics.AvailableMemory):0.0} GB available of {GiB(ViewModel.Metrics.TotalMemory):0.0} GB") : L.T("unavailable"))
            + L.T("\nNetwork: ") + (ViewModel.Metrics.ReceiveBytesPerSecond.HasValue ? L.T("available · aggregate active adapters") : L.T("waiting / unavailable"))
            + L.T("\nMedia: ") + (ViewModel.Media.Available ? L.T("Windows media session connected") : ViewModel.Media.Artist)
            + L.T("\nAudio: ") + ViewModel.Audio.Device
            + L.T("\nBattery: ") + ViewModel.BatteryDetail
            + L.T("\nGlobal hotkey: ") + (_hotkeyAvailable ? L.T("registered") : L.T("unavailable · choose a different shortcut"))
            + L.T("\nHide / show hotkey: ") + (_hideHotkeyAvailable ? L.T("registered") : L.T("unavailable · choose a different shortcut"))
            + L.T("\nSystem tray: ") + (_tray?.IsRegistered == true ? L.T("registered · right-click for controls") : L.T("unavailable"))
            + L.T("\nGlass material: ") + _glass.MaterialStatus
            + L.T("\nGPU: ") + _gpuSnapshot.Status + L.T("\nTemperatures: ") + _temperatureSnapshot.Status + L.T("\nFPS: ") + _fpsSnapshot.Status + L.T("\nMicrophone: ") + _microphoneSnapshot.Device + L.T("\nOBS: ") + _obsSnapshot.Status));
        diagnostics.Children.Add(Note(L.T("Settings: ") + _settings.FilePath + L.T("\nActivity log: ") + _log.FilePath + L.T("\nError log: ") + _log.ErrorFilePath));
        diagnostics.Children.Add(Button(L.T("Open activity log"), (_, _) => Report(ActionLauncher.Launch(new("Activity log", ActionKind.File, _log.FilePath)))));
        diagnostics.Children.Add(Button(L.T("Open error log"), (_, _) => Report(ActionLauncher.Launch(new("Error log", ActionKind.File, _log.ErrorFilePath)))));
        diagnostics.Children.Add(Note(L.F($"Audio refresh: {Volatile.Read(ref _runtimeDemand).AudioIntervalMs} ms · Signal meter: {(ViewModel.Audio.Peak.HasValue ? "available" : "unavailable")} · Frames: {_audioFrameCount}")));
        diagnostics.Children.Add(Note(L.T("Media artwork: ") + (_artwork is { Length: > 0 } ? L.T("provided by media app") : L.T("not provided by the current session"))));
        diagnostics.Children.Add(Button(L.T("Open Nagi data folder"), (_, _) => Report(ActionLauncher.Launch(new("Nagi data", ActionKind.Folder, Path.GetDirectoryName(_settings.FilePath)!)))));
        diagnostics.Children.Add(Button(L.T("Refresh diagnostics"), (_, _) => BuildSettingsPage()));
        SettingsPage.Children.Add(Card(diagnostics));
        SettingsPage.Children.Add(Heading(L.T("Recent activity · this session only")));
        if (ViewModel.Activity.Count == 0) SettingsPage.Children.Add(Note(L.T("No profile changes yet.")));
        foreach (var entry in ViewModel.Activity) SettingsPage.Children.Add(Note(entry.Time.ToString("HH:mm:ss") + "   " + entry.Message));
        var footer = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10 };
        footer.Children.Add(Button(L.T("Restore defaults"), async (_, _) => await RestoreDefaultsAsync()));
        footer.Children.Add(Button(L.T("Quit Nagi"), (_, _) => Close()));
        SettingsPage.Children.Add(footer);
    }

    private async Task EditHotkeyAsync(bool hideShortcut = false, bool searchShortcut = false)
    {
        var current = searchShortcut ? ViewModel.Settings.SearchHotkey : hideShortcut ? ViewModel.Settings.HideHotkey : ViewModel.Settings.Hotkey;
        var ctrl = new CheckBox { Content = L.T("Ctrl"), IsChecked = (current.Modifiers & 2) != 0 };
        var alt = new CheckBox { Content = L.T("Alt"), IsChecked = (current.Modifiers & 1) != 0 };
        var shift = new CheckBox { Content = L.T("Shift"), IsChecked = (current.Modifiers & 4) != 0 };
        var win = new CheckBox { Content = L.T("Win"), IsChecked = (current.Modifiers & 8) != 0 };
        var keys = new[] { "Space" }.Concat(Enumerable.Range('A', 26).Select(c => ((char)c).ToString())).Concat(Enumerable.Range(1, 12).Select(i => "F" + i)).ToArray();
        var keyName = current.Key == 0x20 ? "Space" : current.Key is >= 0x70 and <= 0x7B ? "F" + (current.Key - 0x6F) : ((char)current.Key).ToString();
        var key = Select(keys, keyName, "Shortcut key");
        var content = Stack(); content.Children.Add(Note(L.T("Choose at least one modifier. If another application owns the shortcut, Nagi keeps your previous shortcut.")));
        var modifiers = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 12 };
        modifiers.Children.Add(ctrl); modifiers.Children.Add(alt); modifiers.Children.Add(shift); modifiers.Children.Add(win);
        content.Children.Add(modifiers); content.Children.Add(key); var error = Note(""); content.Children.Add(error);
        var dialog = Dialog(searchShortcut ? L.T("Search shortcut") : hideShortcut ? L.T("Hide / show shortcut") : L.T("Global shortcut"), content);
        dialog.PrimaryButtonClick += async (_, args) =>
        {
            uint flags = (ctrl.IsChecked == true ? 2u : 0) | (alt.IsChecked == true ? 1u : 0) | (shift.IsChecked == true ? 4u : 0) | (win.IsChecked == true ? 8u : 0);
            if (flags == 0 || key.SelectedItem is not string selected) { error.Text = L.T("Choose a key and at least one modifier."); args.Cancel = true; return; }
            var virtualKey = selected == "Space" ? 0x20u : selected.Length > 1 ? 0x6Fu + uint.Parse(selected[1..]) : selected[0];
            var next = new Hotkey(flags, virtualKey);
            if (next == current && (searchShortcut ? _searchHotkeyAvailable : hideShortcut ? _hideHotkeyAvailable : _hotkeyAvailable)) return;
            var result = searchShortcut ? _windowManager.RegisterSearchHotkey(next) : hideShortcut ? _windowManager.RegisterHideHotkey(next) : _windowManager.RegisterHotkey(next);
            if (!result.Success) { error.Text = result.Message; args.Cancel = true; return; }
            var deferral = args.GetDeferral();
            try
            {
                if (await CommitAsync(s => searchShortcut ? s with { SearchHotkey = next } : hideShortcut ? s with { HideHotkey = next } : s with { Hotkey = next }))
                {
                    if (searchShortcut) _searchHotkeyAvailable = true; else if (hideShortcut) _hideHotkeyAvailable = true; else _hotkeyAvailable = true;
                    ViewModel.Publish(searchShortcut ? L.T("Search shortcut updated.") : hideShortcut ? L.T("Hide / show shortcut updated.") : L.T("Global shortcut updated."), true);
                }
                else
                {
                    if (searchShortcut) _searchHotkeyAvailable = _windowManager.RegisterSearchHotkey(current).Success;
                    else if (hideShortcut) _hideHotkeyAvailable = _windowManager.RegisterHideHotkey(current).Success;
                    else _hotkeyAvailable = _windowManager.RegisterHotkey(current).Success;
                    error.Text = L.T("Shortcut could not be saved."); args.Cancel = true;
                }
            }
            finally { deferral.Complete(); }
        };
        await ShowDialogAsync(dialog); BuildSettingsPage();
    }
    private async Task RestoreDefaultsAsync()
    {
        var content = Note(L.T("Reset Nagi preferences, shortcuts and application rules? This also disables startup registration. A backup of your previous settings remains in the Nagi data folder."));
        var dialog = Dialog(L.T("Restore defaults?"), content); dialog.PrimaryButtonText = L.T("Restore");
        if (await ShowDialogAsync(dialog) != ContentDialogResult.Primary) return;
        var previous = ViewModel.Settings;
        var defaults = new PulseSettings();
        if (previous.Hotkey != defaults.Hotkey && !Report(_windowManager.RegisterHotkey(defaults.Hotkey), false)) defaults = defaults with { Hotkey = previous.Hotkey };
        if (previous.HideHotkey != defaults.HideHotkey && !Report(_windowManager.RegisterHideHotkey(defaults.HideHotkey), false)) defaults = defaults with { HideHotkey = previous.HideHotkey };
        if (previous.SearchHotkey != defaults.SearchHotkey && !Report(_windowManager.RegisterSearchHotkey(defaults.SearchHotkey), false)) defaults = defaults with { SearchHotkey = previous.SearchHotkey };
        if (!Report(ActionLauncher.ConfigureStartup(false, Environment.ProcessPath!), false)) defaults = defaults with { StartWithWindows = previous.StartWithWindows };
        if (!await CommitAsync(_ => defaults))
        {
            _windowManager.RegisterHotkey(previous.Hotkey);
            _windowManager.RegisterHideHotkey(previous.HideHotkey);
            _windowManager.RegisterSearchHotkey(previous.SearchHotkey);
            ActionLauncher.ConfigureStartup(previous.StartWithWindows, Environment.ProcessPath!);
        }
        else ViewModel.Publish(L.T("Nagi defaults restored."), true);
        BuildSettingsPage(); BuildRulesPage();
    }
    private ContentDialog Dialog(string title, UIElement content)
    {
        var dialog = new ContentDialog
        {
            Title = title, Content = new ScrollViewer { Content = content, MaxHeight = 420, VerticalScrollBarVisibility = ScrollBarVisibility.Auto },
            PrimaryButtonText = L.T("Save"), CloseButtonText = L.T("Cancel"), DefaultButton = ContentDialogButton.Primary,
            XamlRoot = Root.XamlRoot, RequestedTheme = Shell.ActualTheme, FlowDirection = L.Direction
        };
        if (ViewModel.Settings.Theme == "Akeno" && !_themeAccessibility.HighContrast)
        {
            dialog.Resources["AccentFillColorDefaultBrush"] = new SolidColorBrush(global::Windows.UI.Color.FromArgb(255, 180, 42, 47));
            dialog.Resources["AccentFillColorSecondaryBrush"] = new SolidColorBrush(global::Windows.UI.Color.FromArgb(255, 196, 47, 52));
            dialog.Resources["AccentFillColorTertiaryBrush"] = new SolidColorBrush(global::Windows.UI.Color.FromArgb(255, 165, 36, 41));
            dialog.Resources["TextOnAccentFillColorPrimaryBrush"] = new SolidColorBrush(Colors.White);
            foreach (var key in new[] { "ListBoxItemBackgroundSelected", "ListBoxItemBackgroundSelectedPointerOver", "ListBoxItemBackgroundSelectedPressed" })
                dialog.Resources[key] = ThemeBrush("AccentWashBrush");
        }
        return dialog;
    }
    private async Task<ContentDialogResult> ShowDialogAsync(ContentDialog dialog)
    {
        if (_dialogOpen) return ContentDialogResult.None;
        _dialogOpen = true;
        try { return await dialog.ShowAsync(); }
        finally { _dialogOpen = false; }
    }
}
