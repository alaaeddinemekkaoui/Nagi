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
        AutomationProperties.SetName(box, name); return box;
    }
    private static UIElement Labeled(string label, UIElement input)
    {
        var stack = Stack(); stack.Spacing = 6; stack.Children.Add(Note(label)); stack.Children.Add(input); return stack;
    }
    private ToggleSwitch Toggle(string title, string detail, bool value, Func<PulseSettings, bool, PulseSettings> change)
    {
        var toggle = new ToggleSwitch { Header = title, IsOn = value, FontSize = 12, OnContent = "On", OffContent = "Off" };
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
        if (_editingWorkspace is { } editing) { BuildWorkspaceEditor(editing); return; }
        WorkspacePage.Children.Add(Heading("A desktop that follows your rhythm."));
        WorkspacePage.Children.Add(Note("Choose a profile anytime. Manual selections stay locked until you choose Automatic or unlock the profile."));
        var profileCards = Stack();
        foreach (var (profile, description) in new[]
        {
            (Profile.Default, "Search, CPU, GPU and music. No apps by default."),
            (Profile.Stream, "OBS recording, streaming and three scene shortcuts."),
            (Profile.Work, "Your apps, projects and essential system information."),
            (Profile.Gaming, "Performance at a glance, with media controls close by."),
            (Profile.Focus, "A quieter Nagi. Nonessential notices stay out of your way.")
        })
        {
            var row = new Grid { ColumnSpacing = 12 };
            row.ColumnDefinitions.Add(new() { Width = new(1, GridUnitType.Star) }); row.ColumnDefinitions.Add(new() { Width = GridLength.Auto });
            var copy = Stack(); copy.Spacing = 3; copy.Children.Add(Heading(profile.ToString())); copy.Children.Add(Note(description));
            row.Children.Add(copy);
            var controls = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, VerticalAlignment = VerticalAlignment.Center };
            controls.Children.Add(Button("Activate " + profile, async (_, _) => await ActivateProfileAsync(profile)));
            controls.Children.Add(Button("Customize " + profile, (_, _) => { _editingWorkspace = profile; BuildRulesPage(); ContentScroll.ChangeView(null, 0, null); }));
            Grid.SetColumn(controls, 1); row.Children.Add(controls);
            profileCards.Children.Add(row);
        }
        WorkspacePage.Children.Add(Card(profileCards));
        var automatic = Toggle("Automatic context switching", "Match the foreground application against your rules.", ViewModel.Settings.AutomaticSwitching,
            (s, enabled) => s with { AutomaticSwitching = enabled, LockedProfile = enabled ? null : s.LockedProfile });
        WorkspacePage.Children.Add(automatic);
        var defaultProfile = Select(Enum.GetNames<Profile>(), ViewModel.Settings.DefaultProfile.ToString(), "Default profile");
        defaultProfile.SelectionChanged += async (_, _) => { if (defaultProfile.SelectedItem is string selected) await CommitAsync(s => s with { DefaultProfile = Enum.Parse<Profile>(selected) }); };
        WorkspacePage.Children.Add(Labeled("Fallback when no application rule matches", defaultProfile));
        WorkspacePage.Children.Add(Heading("Application rules"));
        WorkspacePage.Children.Add(Note("Highest priority wins. Equal priorities keep list order. A match must stay stable for 1.8 seconds. Fullscreen windows are never classified as games automatically."));
        foreach (var rule in ViewModel.Settings.Rules)
        {
            var row = Stack();
            row.Children.Add(Heading(rule.Profile + " · priority " + rule.Priority + (rule.Enabled ? "" : " · disabled")));
            row.Children.Add(Note(rule.Identity));
            var actions = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
            actions.Children.Add(Button("Edit", async (_, _) => await EditRuleAsync(rule)));
            actions.Children.Add(Button(rule.Enabled ? "Disable" : "Enable", async (_, _) =>
            { if (await CommitAsync(s => s with { Rules = s.Rules.Select(r => r == rule ? r with { Enabled = !r.Enabled } : r).ToArray() })) BuildRulesPage(); }));
            actions.Children.Add(Button("Remove", async (_, _) =>
            { if (await CommitAsync(s => s with { Rules = s.Rules.Where(r => r != rule).ToArray() })) BuildRulesPage(); }));
            row.Children.Add(actions); WorkspacePage.Children.Add(Card(row));
        }
        WorkspacePage.Children.Add(Button("+ Add application rule", async (_, _) => await EditRuleAsync(null)));
        WorkspacePage.Children.Add(Heading("Quick actions"));
        WorkspacePage.Children.Add(Note("Shortcuts run only when you click them. Use local files and .exe paths without arguments, or http/https URLs."));
        foreach (var action in ViewModel.Settings.QuickActions)
        {
            var row = Stack(); row.Children.Add(Heading(action.Label));
            row.Children.Add(Note((action.Kind == ActionKind.ToggleHalo ? "Toggle Nagi" : action.Kind.ToString()) + " · " + action.Target + (action.Profile.HasValue ? " · " + action.Profile + " only" : " · All profiles")));
            var buttons = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
            buttons.Children.Add(Button("Edit", async (_, _) => await EditActionAsync(action)));
            buttons.Children.Add(Button("Remove", async (_, _) =>
            { if (await CommitAsync(s => s with { QuickActions = s.QuickActions.Where(a => a != action).ToArray() })) BuildRulesPage(); }));
            row.Children.Add(buttons); WorkspacePage.Children.Add(Card(row));
        }
        WorkspacePage.Children.Add(Button("+ Add quick action", async (_, _) => await EditActionAsync(null)));
    }

    private async Task EditRuleAsync(AppRule? existing)
    {
        var identity = new TextBox { Header = "Process name or full executable path", Text = existing?.Identity ?? ViewModel.Foreground.ExecutablePath ?? (ViewModel.Foreground.Name + ".exe"), PlaceholderText = "Code.exe or C:\\Apps\\Editor.exe" };
        var profile = Select(Enum.GetNames<Profile>(), (existing?.Profile ?? Profile.Work).ToString(), "Rule profile");
        var priority = new NumberBox { Header = "Priority (higher wins)", Minimum = 0, Maximum = 10000, Value = existing?.Priority ?? 100, SpinButtonPlacementMode = NumberBoxSpinButtonPlacementMode.Compact };
        var enabled = new ToggleSwitch { Header = "Rule enabled", IsOn = existing?.Enabled ?? true };
        var error = Note(""); var content = Stack();
        content.Children.Add(identity); content.Children.Add(Labeled("Profile", profile)); content.Children.Add(priority); content.Children.Add(enabled); content.Children.Add(error);
        var dialog = Dialog(existing is null ? "Add application rule" : "Edit application rule", content);
        dialog.PrimaryButtonClick += async (_, args) =>
        {
            var value = identity.Text.Trim();
            if (value.Length == 0 || value.Length > 32768 || value.IndexOfAny(['*', '?', '"', '\0']) >= 0 || !double.IsFinite(priority.Value))
            { error.Text = "Enter an exact executable name or path and a valid priority."; args.Cancel = true; return; }
            var rule = new AppRule(value, Enum.Parse<Profile>((string)profile.SelectedItem), (int)priority.Value, enabled.IsOn);
            var deferral = args.GetDeferral();
            try
            {
                if (!await CommitAsync(s => s with { Rules = existing is null ? [.. s.Rules, rule] : s.Rules.Select(r => r == existing ? rule : r).ToArray() }))
                { error.Text = "The rule could not be saved."; args.Cancel = true; }
            }
            finally { deferral.Complete(); }
        };
        await ShowDialogAsync(dialog); BuildRulesPage();
    }

    private async Task EditActionAsync(QuickAction? existing)
    {
        var label = new TextBox { Header = "Name", Text = existing?.Label ?? "", MaxLength = 40, PlaceholderText = "My project" };
        var kinds = Enum.GetValues<ActionKind>();
        static string KindLabel(ActionKind value) => value == ActionKind.ToggleHalo ? "Toggle Nagi" : value.ToString();
        var kind = Select(kinds.Select(KindLabel), KindLabel(existing?.Kind ?? ActionKind.Folder), "Action type");
        var target = new TextBox { Header = "Target", Text = existing?.Target ?? "", PlaceholderText = "Absolute path, URL, or profile name" };
        var filter = Select(new[] { "All profiles" }.Concat(ProfileWorkspaces.Profiles.Select(p => p.ToString())), existing?.Profile?.ToString() ?? "All profiles", "Visible in profile");
        var error = Note(""); var content = Stack();
        content.Children.Add(label); content.Children.Add(Labeled("Action type", kind)); content.Children.Add(target);
        content.Children.Add(Note("Application: local .exe, no arguments. Profile: Default, Work, Gaming, Stream or Focus. Toggle Nagi: leave target empty. Scripts and command hosts are blocked."));
        content.Children.Add(Labeled("Show this action in", filter)); content.Children.Add(error);
        var dialog = Dialog(existing is null ? "Add quick action" : "Edit quick action", content);
        dialog.PrimaryButtonClick += async (_, args) =>
        {
            var action = new QuickAction(label.Text.Trim(), kinds[kind.SelectedIndex], target.Text.Trim(),
                filter.SelectedIndex == 0 ? null : Enum.Parse<Profile>((string)filter.SelectedItem));
            var validation = ActionValidator.Validate(action);
            if (!validation.Success) { error.Text = validation.Message; args.Cancel = true; return; }
            var deferral = args.GetDeferral();
            try
            {
                if (!await CommitAsync(s => s with { QuickActions = existing is null ? [.. s.QuickActions, action] : s.QuickActions.Select(a => a == existing ? action : a).ToArray() }))
                { error.Text = "The action could not be saved."; args.Cancel = true; }
            }
            finally { deferral.Complete(); }
        };
        await ShowDialogAsync(dialog); BuildRulesPage();
    }

    private void BuildAppearanceSettings()
    {
        var appearance = Stack();
        appearance.Children.Add(Heading("Appearance & placement"));
        var theme = Select(new[] { "Glass (system)", "Akeno", "Dark", "Light" }, ViewModel.Settings.Theme == "System" ? "Glass (system)" : ViewModel.Settings.Theme, "Theme");
        theme.SelectionChanged += async (_, _) => { if (theme.SelectedItem is string selected) await CommitAsync(s => s with { Theme = selected == "Glass (system)" ? "System" : selected }); };
        appearance.Children.Add(Labeled("Theme", theme));
        appearance.Children.Add(Toggle("Translucent glass", "Native acrylic follows Windows transparency and contrast preferences.", ViewModel.Settings.GlassEnabled, (s, v) => s with { GlassEnabled = v }));
        appearance.Children.Add(Note("Glass follows Windows light and dark mode. Native transparency respects Windows accessibility preferences."));
        var background = Select(new[] { "Samurai", "Gradient", "None" }, ViewModel.Settings.AkenoBackground, "Akeno background");
        background.SelectionChanged += async (_, _) => { if (background.SelectedItem is string selected) await CommitAsync(s => s with { AkenoBackground = selected }); };
        appearance.Children.Add(Labeled("Akeno background · choose None to hide", background));
        appearance.Children.Add(Note("The generated samurai artwork appears behind expanded Akeno panels. Choose Gradient for a simple red wash."));
        var orientation = Select(new[] { "Vertical", "Horizontal" }, ViewModel.Settings.Position.Orientation, "Dock layout");
        orientation.SelectionChanged += async (_, _) =>
        {
            if (orientation.SelectedItem is not string selected) return;
            await CommitAsync(s => s with { Position = s.Position with { Orientation = selected, Anchor = selected == "Horizontal" ? "Bottom edge" : "Right edge", OffsetX = 0, OffsetY = 0 } });
            BuildSettingsPage();
        };
        appearance.Children.Add(Labeled("Dock layout", orientation));
        var edges = ViewModel.Settings.Position.Orientation == "Horizontal" ? new[] { "Top edge", "Bottom edge" } : new[] { "Right edge", "Left edge" };
        var anchors = Select(edges, ViewModel.Settings.Position.Anchor, "Nagi position");
        anchors.SelectionChanged += async (_, _) =>
        { if (anchors.SelectedItem is string selected) await CommitAsync(s => s with { Position = s.Position with { Anchor = selected, OffsetX = 0, OffsetY = 0 } }); };
        appearance.Children.Add(Labeled("Screen edge · drag the logo to reposition", anchors));
        var monitors = HaloWindowManager.GetMonitors();
        var displayNames = new[] { "Primary display" }.Concat(monitors.Select(m => m.Label)).ToArray();
        var selectedDisplay = monitors.FirstOrDefault(m => m.Device == ViewModel.Settings.Position.Monitor)?.Label ?? "Primary display";
        var display = Select(displayNames, selectedDisplay, "Preferred display");
        display.SelectionChanged += async (_, _) =>
        {
            var device = display.SelectedIndex <= 0 ? "" : monitors[display.SelectedIndex - 1].Device;
            await CommitAsync(s => s with { Position = s.Position with { Monitor = device, OffsetX = 0, OffsetY = 0 } });
        };
        appearance.Children.Add(Labeled("Display", display));
        appearance.Children.Add(Toggle("Reduce motion", "Windows animation preferences are also respected.", ViewModel.Settings.ReducedMotion, (s, v) => s with { ReducedMotion = v }));
        SettingsPage.Children.Add(Card(appearance));
    }

    private void BuildBehaviorSettings()
    {
        var behavior = Stack(); behavior.Children.Add(Heading("Behavior"));
        behavior.Children.Add(Toggle("Dismiss when clicking outside", "Keep the panel open by turning this off.", ViewModel.Settings.CloseOnDeactivate, (s, v) => s with { CloseOnDeactivate = v }));
        behavior.Children.Add(Toggle("Hide over fullscreen apps", "Applies to any fullscreen app; does not change profile rules.", ViewModel.Settings.HideInFullscreen, (s, v) => s with { HideInFullscreen = v }));
        behavior.Children.Add(Toggle("Open expanded on startup", "Default is a small water drop attached to the screen edge.", ViewModel.Settings.StartExpanded, (s, v) => s with { StartExpanded = v }));
        behavior.Children.Add(Toggle("Show media controls", "Read supported Windows media sessions.", ViewModel.Settings.ShowMedia, (s, v) => s with { ShowMedia = v }));
        behavior.Children.Add(Toggle("Contextual notifications", "Focus suppresses nonessential notices.", ViewModel.Settings.Notifications, (s, v) => s with { Notifications = v }));
        var interval = new NumberBox { Header = "Polling interval (milliseconds)", Minimum = 500, Maximum = 10000, SmallChange = 500, Value = ViewModel.Settings.PollingIntervalMs, SpinButtonPlacementMode = NumberBoxSpinButtonPlacementMode.Compact };
        behavior.Children.Add(interval);
        behavior.Children.Add(Button("Apply polling interval", async (_, _) =>
        {
            if (double.IsFinite(interval.Value)) await CommitAsync(s => s with { PollingIntervalMs = (int)interval.Value });
            else ViewModel.Publish("Enter a valid polling interval.", true);
        }));
        behavior.Children.Add(Button("Change global shortcut · " + ViewModel.HotkeyText, async (_, _) => await EditHotkeyAsync()));
        behavior.Children.Add(Button("Hide / show shortcut · " + Pulse.App.ViewModels.PulseViewModel.FormatHotkey(ViewModel.Settings.HideHotkey), async (_, _) => await EditHotkeyAsync(true)));
        behavior.Children.Add(Note("The first shortcut expands or collapses Nagi. The hide/show shortcut removes Nagi from the desktop and restores it on the next press. The tray icon stays available for Settings and Quit."));
        var startup = new ToggleSwitch { Header = "Start Nagi when I sign in", IsOn = ViewModel.Settings.StartWithWindows };
        startup.Toggled += async (_, _) =>
        {
            if (_updating) return;
            var previous = ViewModel.Settings.StartWithWindows;
            var success = Report(ActionLauncher.ConfigureStartup(startup.IsOn, Environment.ProcessPath!));
            if (success && !await CommitAsync(s => s with { StartWithWindows = startup.IsOn })) ActionLauncher.ConfigureStartup(previous, Environment.ProcessPath!);
            BuildSettingsPage();
        };
        behavior.Children.Add(startup); behavior.Children.Add(Note("Startup is per-user and opt-in. Re-enable it if you move the executable."));
        SettingsPage.Children.Add(Card(behavior));

    }

    private void BuildDiagnosticsSettings()
    {
        var diagnostics = Stack(); diagnostics.Children.Add(Heading("Integrations & diagnostics"));
        diagnostics.Children.Add(Note($"Nagi version 1 · WinUI 3 · .NET {Environment.Version}\nSession uptime: {_uptime.Elapsed:hh\\:mm\\:ss}\nForeground: {ViewModel.Foreground.Name} · PID {ViewModel.Foreground.ProcessId}"));
        diagnostics.Children.Add(Note("CPU: " + (ViewModel.Metrics.Cpu.HasValue ? "available · GetSystemTimes" : "waiting / unavailable")
            + "\nMemory: " + (ViewModel.Metrics.TotalMemory > 0 ? $"{GiB(ViewModel.Metrics.AvailableMemory):0.0} GB available of {GiB(ViewModel.Metrics.TotalMemory):0.0} GB" : "unavailable")
            + "\nNetwork: " + (ViewModel.Metrics.ReceiveBytesPerSecond.HasValue ? "available · aggregate active adapters" : "waiting / unavailable")
            + "\nMedia: " + (ViewModel.Media.Available ? "Windows media session connected" : ViewModel.Media.Artist)
            + "\nAudio: " + ViewModel.Audio.Device
            + "\nBattery: " + ViewModel.BatteryDetail
            + "\nGlobal hotkey: " + (_hotkeyAvailable ? "registered" : "unavailable · choose a different shortcut")
            + "\nHide / show hotkey: " + (_hideHotkeyAvailable ? "registered" : "unavailable · choose a different shortcut")
            + "\nSystem tray: " + (_tray?.IsRegistered == true ? "registered · right-click for controls" : "unavailable")
            + "\nGlass material: " + _glass.MaterialStatus
            + "\nGPU: " + _gpuSnapshot.Status + "\nTemperatures: " + _temperatureSnapshot.Status + "\nFPS: " + _fpsSnapshot.Status + "\nMicrophone: " + _microphoneSnapshot.Device + "\nOBS: " + _obsSnapshot.Status));
        diagnostics.Children.Add(Note("Settings: " + _settings.FilePath + "\nLogs: " + _log.FilePath));
        diagnostics.Children.Add(Note($"Audio refresh: 50 ms · Signal meter: {(ViewModel.Audio.Peak.HasValue ? "available" : "unavailable")} · Frames: {_audioFrameCount}"));
        diagnostics.Children.Add(Note("Media artwork: " + (_artwork is { Length: > 0 } ? "provided by media app" : "not provided by the current session")));
        diagnostics.Children.Add(Button("Open Nagi data folder", (_, _) => Report(ActionLauncher.Launch(new("Nagi data", ActionKind.Folder, Path.GetDirectoryName(_settings.FilePath)!)))));
        diagnostics.Children.Add(Button("Refresh diagnostics", (_, _) => BuildSettingsPage()));
        SettingsPage.Children.Add(Card(diagnostics));
        SettingsPage.Children.Add(Heading("Recent activity · this session only"));
        if (ViewModel.Activity.Count == 0) SettingsPage.Children.Add(Note("No profile changes yet."));
        foreach (var entry in ViewModel.Activity) SettingsPage.Children.Add(Note(entry.Time.ToString("HH:mm:ss") + "   " + entry.Message));
        var footer = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10 };
        footer.Children.Add(Button("Restore defaults", async (_, _) => await RestoreDefaultsAsync()));
        footer.Children.Add(Button("Quit Nagi", (_, _) => Close()));
        SettingsPage.Children.Add(footer);
    }

    private async Task EditHotkeyAsync(bool hideShortcut = false, bool searchShortcut = false)
    {
        var current = searchShortcut ? ViewModel.Settings.SearchHotkey : hideShortcut ? ViewModel.Settings.HideHotkey : ViewModel.Settings.Hotkey;
        var ctrl = new CheckBox { Content = "Ctrl", IsChecked = (current.Modifiers & 2) != 0 };
        var alt = new CheckBox { Content = "Alt", IsChecked = (current.Modifiers & 1) != 0 };
        var shift = new CheckBox { Content = "Shift", IsChecked = (current.Modifiers & 4) != 0 };
        var win = new CheckBox { Content = "Win", IsChecked = (current.Modifiers & 8) != 0 };
        var keys = new[] { "Space" }.Concat(Enumerable.Range('A', 26).Select(c => ((char)c).ToString())).Concat(Enumerable.Range(1, 12).Select(i => "F" + i)).ToArray();
        var keyName = current.Key == 0x20 ? "Space" : current.Key is >= 0x70 and <= 0x7B ? "F" + (current.Key - 0x6F) : ((char)current.Key).ToString();
        var key = Select(keys, keyName, "Shortcut key");
        var content = Stack(); content.Children.Add(Note("Choose at least one modifier. If another application owns the shortcut, Nagi keeps your previous shortcut."));
        var modifiers = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 12 };
        modifiers.Children.Add(ctrl); modifiers.Children.Add(alt); modifiers.Children.Add(shift); modifiers.Children.Add(win);
        content.Children.Add(modifiers); content.Children.Add(key); var error = Note(""); content.Children.Add(error);
        var dialog = Dialog(searchShortcut ? "Search shortcut" : hideShortcut ? "Hide / show shortcut" : "Global shortcut", content);
        dialog.PrimaryButtonClick += async (_, args) =>
        {
            uint flags = (ctrl.IsChecked == true ? 2u : 0) | (alt.IsChecked == true ? 1u : 0) | (shift.IsChecked == true ? 4u : 0) | (win.IsChecked == true ? 8u : 0);
            if (flags == 0 || key.SelectedItem is not string selected) { error.Text = "Choose a key and at least one modifier."; args.Cancel = true; return; }
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
                    ViewModel.Publish(searchShortcut ? "Search shortcut updated." : hideShortcut ? "Hide / show shortcut updated." : "Global shortcut updated.", true);
                }
                else
                {
                    if (searchShortcut) _searchHotkeyAvailable = _windowManager.RegisterSearchHotkey(current).Success;
                    else if (hideShortcut) _hideHotkeyAvailable = _windowManager.RegisterHideHotkey(current).Success;
                    else _hotkeyAvailable = _windowManager.RegisterHotkey(current).Success;
                    error.Text = "Shortcut could not be saved."; args.Cancel = true;
                }
            }
            finally { deferral.Complete(); }
        };
        await ShowDialogAsync(dialog); BuildSettingsPage();
    }
    private async Task RestoreDefaultsAsync()
    {
        var content = Note("Reset Nagi preferences, shortcuts and application rules? This also disables startup registration. A backup of your previous settings remains in the Nagi data folder.");
        var dialog = Dialog("Restore defaults?", content); dialog.PrimaryButtonText = "Restore";
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
        else ViewModel.Publish("Nagi defaults restored.", true);
        BuildSettingsPage(); BuildRulesPage();
    }
    private ContentDialog Dialog(string title, UIElement content)
    {
        var dialog = new ContentDialog
        {
            Title = title, Content = new ScrollViewer { Content = content, MaxHeight = 420, VerticalScrollBarVisibility = ScrollBarVisibility.Auto },
            PrimaryButtonText = "Save", CloseButtonText = "Cancel", DefaultButton = ContentDialogButton.Primary,
            XamlRoot = Root.XamlRoot, RequestedTheme = Shell.ActualTheme
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
