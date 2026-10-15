using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Pulse.Infrastructure;
using Windows.UI.ViewManagement;

namespace Pulse.App;

public sealed partial class MainWindow
{
    private LuaUiService? _lua;
    private LuaUiStyle _luaStyle = new();
    private string? _luaContext;
    private string _luaStatus = "Built-in appearance";
    private int _luaGeneration;
    private readonly Dictionary<(string Theme, string Key), global::Windows.UI.Color> _baseColors = new();
    private TextBlock? _luaStatusText;
    private LuaUiService Lua => _lua ??= new(Path.GetDirectoryName(_settings.FilePath)!);

    private async Task ReloadLuaUiAsync(bool force = false)
    {
        if (_closed) return;
        var enabled = ViewModel.Settings.LuaUiEnabled;
        var theme = ViewModel.Settings.Theme == "Akeno" ? "Akeno" : Shell.ActualTheme == ElementTheme.Light ? "Light" : "Dark";
        var profile = ViewModel.Profile.ToString();
        var context = $"{enabled}/{theme}/{profile}";
        if (!force && context == _luaContext) return;
        _luaContext = context;
        var generation = ++_luaGeneration;
        if (!enabled)
        {
            _luaStyle = new(); SetLuaStatus("Built-in appearance"); ApplyLuaVisuals(); return;
        }
        try
        {
            var source = await Lua.ReadAsync(_shutdown.Token);
            var style = await Task.Run(() => LuaUiService.Evaluate(source, theme, profile), _shutdown.Token);
            if (_closed || generation != _luaGeneration) return;
            _luaStyle = style;
            SetLuaStatus("Lua active · " + style.Name);
            ApplyLuaVisuals(); Render();
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            if (_closed || generation != _luaGeneration) return;
            SetLuaStatus("Lua error · " + ex.Message);
            _log.Write("lua-ui", "UI script could not be applied", ex);
            ViewModel.Publish("Lua: " + ex.Message + " Previous appearance kept.", true);
        }
    }

    private void SetLuaStatus(string status)
    {
        _luaStatus = status;
        if (_luaStatusText is not null) _luaStatusText.Text = status;
    }

    private static global::Windows.UI.Color ParseLuaColor(string value)
    {
        var bits = Convert.ToUInt32(value[1..], 16);
        return global::Windows.UI.Color.FromArgb(value.Length == 9 ? (byte)(bits >> 24) : (byte)255,
            (byte)(bits >> 16), (byte)(bits >> 8), (byte)bits);
    }

    private void ApplyLuaVisuals()
    {
        if (_closed) return;
        var tokens = Application.Current.Resources.MergedDictionaries[1];
        foreach (var theme in new[] { "Dark", "Light" })
        {
            var palette = (ResourceDictionary)tokens.ThemeDictionaries[theme];
            foreach (var key in new[] { "CanvasBrush", "SurfaceBrush", "InsetBrush", "EdgeBrush", "InkBrush", "MutedBrush", "PulseAccentBrush", "AccentWashBrush", "SelectedTabBrush" })
            {
                var brush = (SolidColorBrush)palette[key];
                if (!_baseColors.ContainsKey((theme, key))) _baseColors[(theme, key)] = brush.Color;
                var color = _baseColors[(theme, key)];
                if (theme == "Dark" && ViewModel.Settings.Theme == "Akeno")
                    color = ((SolidColorBrush)((ResourceDictionary)tokens.ThemeDictionaries["Akeno"])[key]).Color;
                if (key == "SurfaceBrush" && _luaStyle.Surface is { } surface) color = ParseLuaColor(surface);
                if (key is "PulseAccentBrush" or "AccentWashBrush" && _luaStyle.Accent is { } accent)
                {
                    color = ParseLuaColor(accent);
                    if (key == "AccentWashBrush") color.A = theme == "Dark" ? (byte)56 : (byte)24;
                }
                brush.Color = color;
            }
        }
        // Leave the system's HighContrast palette intact.
        var ak = ViewModel.Settings.Theme == "Akeno" && !_themeAccessibility.HighContrast;
        AkenoDecor.Visibility = ak ? Visibility.Visible : Visibility.Collapsed;
        AkenoSeal.Visibility = Visibility.Collapsed;
        HeaderMark.Visibility = Visibility.Visible;
        BrandSubtitle.Text = ak ? "凪 · Calm, within reach." : "Calm, within reach.";
        Root.Background = ak ? new SolidColorBrush(Microsoft.UI.Colors.Transparent) : ThemeBrush("HeaderGlowBrush");
        WindowSurface.SetColor(((SolidColorBrush)ThemeBrush("SurfaceBrush")).Color);
        HomePage.Spacing = _luaStyle.Spacing;
        MetricsGrid.ColumnSpacing = _luaStyle.Spacing;
        MetricsGrid.Visibility = _luaStyle.ShowMetrics ? Visibility.Visible : Visibility.Collapsed;
        NetworkRow.Visibility = _luaStyle.ShowNetwork ? Visibility.Visible : Visibility.Collapsed;
        MediaCard.Visibility = ViewModel.Settings.ShowMedia && _luaStyle.ShowMedia ? Visibility.Visible : Visibility.Collapsed;
        PeekMediaInfo.Visibility = PeekTransport.Visibility = ViewModel.Settings.ShowMedia ? Visibility.Visible : Visibility.Collapsed;
        AudioCard.Visibility = _luaStyle.ShowAudio ? Visibility.Visible : Visibility.Collapsed;
        var accentBrush = (SolidColorBrush)ThemeBrush("PulseAccentBrush");
        foreach (var slider in new[] { VolumeSlider, PeekVolumeSlider })
        {
            slider.Foreground = accentBrush;
            foreach (var key in new[] { "SliderTrackValueFill", "SliderTrackValueFillPointerOver", "SliderTrackValueFillPressed", "SliderThumbBackground", "SliderThumbBackgroundPointerOver", "SliderThumbBackgroundPressed" })
                slider.Resources[key] = accentBrush;
        }
        foreach (var metric in new[] { CpuMetric, RamMetric, PeekCpuMetric, PeekRamMetric })
            metric.Customize(_luaStyle.CardRadius, accentBrush);
        CpuMetric.Rename(_luaStyle.CpuLabel); PeekCpuMetric.Rename(_luaStyle.CpuLabel);
        RamMetric.Rename(_luaStyle.MemoryLabel); PeekRamMetric.Rename(_luaStyle.MemoryLabel);
        ApplyCardRadius(Root);
    }

    private void ApplyCardRadius(DependencyObject parent)
    {
        if (parent is Border border && ReferenceEquals(border.Style, Application.Current.Resources["Card"]))
            border.CornerRadius = new(_luaStyle.CardRadius);
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++) ApplyCardRadius(VisualTreeHelper.GetChild(parent, i));
    }

    private void BuildLuaSettings()
    {
        var content = Stack();
        content.Children.Add(Heading("Lua UI studio"));
        content.Children.Add(Note("Style Nagi with Lua: accents, glass cards, spacing, labels and overview sections. Theme and profile changes reapply your script."));
        content.Children.Add(Toggle("Enable Lua UI", "Use your saved script. Turn off to restore the built-in design.", ViewModel.Settings.LuaUiEnabled, (s, v) => s with { LuaUiEnabled = v }));
        _luaStatusText = Note(_luaStatus);
        AutomationProperties.SetName(_luaStatusText, "Lua UI status");
        AutomationProperties.SetLiveSetting(_luaStatusText, Microsoft.UI.Xaml.Automation.Peers.AutomationLiveSetting.Polite);
        content.Children.Add(_luaStatusText);
        var actions = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        actions.Children.Add(Button("Edit Lua UI", async (_, _) => await EditLuaUiAsync()));
        actions.Children.Add(Button("Reload Lua UI", async (_, _) => await ReloadLuaUiAsync(true)));
        content.Children.Add(actions);
        content.Children.Add(Note("Script: " + Lua.FilePath));
        SettingsPage.Children.Add(Card(content));
    }

    private async Task EditLuaUiAsync()
    {
        string source;
        try { source = await Lua.ReadAsync(_shutdown.Token); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Text.DecoderFallbackException)
        { source = LuaUiService.Presets["Rose glass"]; ViewModel.Publish("Could not read UI script: " + ex.Message, true); }
        var content = Stack();
        var presets = Select(new[] { "Saved script" }.Concat(LuaUiService.Presets.Keys), "Saved script", "Lua UI preset");
        var editor = new TextBox
        {
            Text = source, AcceptsReturn = true, TextWrapping = TextWrapping.NoWrap, Height = 230,
            FontFamily = new("Cascadia Code, Consolas"), FontSize = 12, MaxLength = LuaUiService.MaximumBytes,
            IsSpellCheckEnabled = false, IsTextPredictionEnabled = false
        };
        AutomationProperties.SetName(editor, "Lua UI source");
        ScrollViewer.SetHorizontalScrollBarVisibility(editor, ScrollBarVisibility.Auto);
        ScrollViewer.SetVerticalScrollBarVisibility(editor, ScrollBarVisibility.Auto);
        presets.SelectionChanged += (_, _) => { if (presets.SelectedItem is string name && LuaUiService.Presets.TryGetValue(name, out var preset)) editor.Text = preset; };
        content.Children.Add(presets); content.Children.Add(editor);
        content.Children.Add(Note("Return a table. Options: name, accent (#RRGGBB), surface (#AARRGGBB), card_radius (8–28), spacing (6–20), labels = { cpu, memory }, sections = { metrics, network, media, audio }. nagi.theme and nagi.profile are available. Lua arithmetic, functions, loops, tables and math are supported."));
        var error = Note(""); AutomationProperties.SetName(error, "Lua UI error");
        AutomationProperties.SetLiveSetting(error, Microsoft.UI.Xaml.Automation.Peers.AutomationLiveSetting.Assertive);
        content.Children.Add(error);
        var dialog = Dialog("Lua UI studio", content); dialog.PrimaryButtonText = "Apply script";
        dialog.PrimaryButtonClick += async (_, args) =>
        {
            var deferral = args.GetDeferral();
            try
            {
                var text = editor.Text;
                var theme = ViewModel.Settings.Theme == "Akeno" ? "Akeno" : Shell.ActualTheme == ElementTheme.Light ? "Light" : "Dark";
                var profile = ViewModel.Profile.ToString();
                await Task.Run(() => LuaUiService.Evaluate(text, theme, profile), _shutdown.Token);
                await Lua.SaveAsync(text, _shutdown.Token);
                if (!await CommitAsync(s => s with { LuaUiEnabled = true })) { args.Cancel = true; error.Text = "Could not enable Lua. Your script was saved; try again."; return; }
                await ReloadLuaUiAsync(true);
                ViewModel.Publish("Lua UI applied.", true);
            }
            catch (Exception ex) { args.Cancel = true; error.Text = ex.Message; }
            finally { deferral.Complete(); }
        };
        await ShowDialogAsync(dialog);
        BuildSettingsPage();
    }
}
