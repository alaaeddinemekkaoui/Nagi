using System.Runtime.InteropServices;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Pulse.App.Controls;
using Pulse.App.Themes;
using Pulse.Core;
using Pulse.Infrastructure;
using Pulse.Windows;
using Windows.Graphics;
using Windows.UI;
using WinRT.Interop;

namespace Pulse.App;

public sealed class WelcomeWindow : Window
{
    private readonly SettingsService _settings;
    private readonly HaloAcrylicBackdrop _glass = new();
    private PulseSettings _draft;
    private readonly CancellationTokenSource _lifetime = new();
    private readonly WeatherService _weather = new();
    private readonly ProtectedSecretStore _secret;
    private readonly Grid _root = new() { Padding = new(32), RowSpacing = 18 };
    private readonly StackPanel _body = new() { Spacing = 16 };
    private readonly TextBlock _stepLabel = new() { FontSize = 12, Opacity = .7 };
    private readonly TextBlock _title = new() { FontSize = 29, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold, TextWrapping = TextWrapping.Wrap };
    private readonly TextBlock _description = new() { FontSize = 14, Opacity = .8, TextWrapping = TextWrapping.Wrap };
    private readonly TextBlock _error = new() { TextWrapping = TextWrapping.Wrap, FontSize = 12 };
    private readonly Button _back = new() { Content = L.T("Back"), MinWidth = 80 };
    private readonly Button _skip = new() { Content = L.T("Skip for now"), MinWidth = 100 };
    private readonly Button _next = new() { Content = L.T("Continue"), MinWidth = 120 };
    private readonly TextBox _name = new() { Header = L.T("Your name"), PlaceholderText = L.T("How should Nagi greet you?"), MaxLength = 32 };
    private readonly ComboBox _theme = new() { Header = L.T("Theme"), ItemsSource = new[] { "Akeno", "Dark", "Light", "System" }, HorizontalAlignment = HorizontalAlignment.Stretch };
    private readonly ComboBox _orientation = new() { Header = L.T("Dock layout"), ItemsSource = new[] { "Vertical", "Horizontal" }, HorizontalAlignment = HorizontalAlignment.Stretch };
    private readonly ComboBox _edge = new() { Header = L.T("Screen edge"), HorizontalAlignment = HorizontalAlignment.Stretch };
    private readonly NumberBox _port = new() { Header = L.T("OBS WebSocket port"), Minimum = 1024, Maximum = 65535, SpinButtonPlacementMode = NumberBoxSpinButtonPlacementMode.Compact };
    private readonly PasswordBox _password = new() { Header = L.T("OBS password"), MaxLength = 512 };
    private readonly Button _connect = new() { Content = L.T("Connect to OBS") };
    private readonly TextBlock _obsStatus = new() { TextWrapping = TextWrapping.Wrap, FontSize = 12 };
    private readonly WeatherLocationPicker _city;
    private int _step, _obsGeneration;
    private readonly ComboBox _language = new() { HorizontalAlignment = HorizontalAlignment.Stretch, MinWidth = 260 };
    private readonly TextBlock _languageStatus = new() { TextWrapping = TextWrapping.Wrap, FontSize = 13, Opacity = .8 };
    private readonly WelcomeGreeting _greeting = new();
    private bool _changingLanguage;
    private bool _connected, _closed, _saving, _finished;
    private string? _passwordToSave;
    public event Action<PulseSettings>? Finished;
    public bool Completed => _finished;

    public WelcomeWindow(SettingsService settings, PulseSettings configuration)
    {
        _settings = settings; _draft = configuration;
        _language.ItemsSource = LanguageChoices.All();
        _language.SelectedItem = ((LanguageChoice[])_language.ItemsSource).FirstOrDefault(l => l.Code == configuration.Language)
            ?? ((LanguageChoice[])_language.ItemsSource)[0];
        _language.SelectionChanged += (_, _) =>
        {
            if (_changingLanguage || _language.SelectedItem is not LanguageChoice choice) return;
            _draft = _draft with { Language = choice.Code }; L.Select(choice.Code);
            RefreshLanguage(); RenderStep();
        };
        _secret = new(Path.Combine(Path.GetDirectoryName(settings.FilePath)!, "obs.credential"));
        _city = new(_weather, configuration.WeatherLocation, configuration.WeatherUnit, _lifetime.Token);
        Title = L.T("Nagi · Welcome");
        AppWindow.SetIcon(Path.Combine(AppContext.BaseDirectory, "Assets", "Nagi.ico"));
        _name.Text = configuration.UserName; _theme.SelectedItem = configuration.Theme;
        _orientation.SelectedItem = configuration.Position.Orientation; UpdateEdges(configuration.Position.Anchor);
        _port.Value = configuration.ObsPort;
        _password.PlaceholderText = _secret.Exists ? L.T("Saved password · enter to replace") : L.T("Server password");
        foreach (var row in new[] { GridLength.Auto, GridLength.Auto, GridLength.Auto, new GridLength(1, GridUnitType.Star), GridLength.Auto, GridLength.Auto })
            _root.RowDefinitions.Add(new() { Height = row });
        var brand = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 14 };
        brand.Children.Add(new PulseMark { Width = 42, Height = 42 });
        brand.Children.Add(new TextBlock { Text = L.T("Nagi"), FontSize = 26, VerticalAlignment = VerticalAlignment.Center, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold });
        brand.Children.Add(_stepLabel); _stepLabel.VerticalAlignment = VerticalAlignment.Center;
        _root.Children.Add(brand);
        AddRow(_title, 1); AddRow(_description, 2);
        AddRow(new ScrollViewer { Content = _body, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled }, 3);
        AddRow(_error, 4);
        var actions = new Grid { ColumnSpacing = 10 };
        actions.ColumnDefinitions.Add(new() { Width = GridLength.Auto }); actions.ColumnDefinitions.Add(new() { Width = new(1, GridUnitType.Star) });
        actions.ColumnDefinitions.Add(new() { Width = GridLength.Auto }); actions.ColumnDefinitions.Add(new() { Width = GridLength.Auto });
        actions.Children.Add(_back); Grid.SetColumn(_skip, 2); actions.Children.Add(_skip); Grid.SetColumn(_next, 3); actions.Children.Add(_next); AddRow(actions, 5);
        Content = _root;
        AutomationProperties.SetAutomationId(_language, "WelcomeLanguage");
        AutomationProperties.SetAutomationId(_languageStatus, "WelcomeCurrentLanguage");
        AutomationProperties.SetAutomationId(_name, "WelcomeName"); AutomationProperties.SetAutomationId(_next, "WelcomeNext");
        AutomationProperties.SetAutomationId(_skip, "WelcomeSkip"); AutomationProperties.SetAutomationId(_back, "WelcomeBack");
        AutomationProperties.SetAutomationId(_orientation, "WelcomeOrientation"); AutomationProperties.SetAutomationId(_edge, "WelcomeEdge");
        AutomationProperties.SetAutomationId(_theme, "WelcomeTheme"); AutomationProperties.SetAutomationId(_connect, "WelcomeObsConnect");
        AutomationProperties.SetAutomationId(_port, "WelcomeObsPort"); AutomationProperties.SetAutomationId(_password, "WelcomeObsPassword");
        AutomationProperties.SetAutomationId(_obsStatus, "WelcomeObsStatus");
        AutomationProperties.SetLiveSetting(_error, Microsoft.UI.Xaml.Automation.Peers.AutomationLiveSetting.Polite);
        _back.Click += (_, _) => { if (_step > 0) { _step--; RenderStep(); } };
        _next.Click += async (_, _) => await NextAsync();
        _skip.Click += async (_, _) =>
        {
            if (_step == 4) { _obsGeneration++; _connected = false; _passwordToSave = null; _draft = _draft with { ObsEnabled = false }; _step = 5; RenderStep(); }
            else if (_step == 5) { _draft = _draft with { WeatherEnabled = false }; _step = 6; RenderStep(); }
        };
        _theme.SelectionChanged += (_, _) => ApplyTheme();
        _orientation.SelectionChanged += (_, _) => UpdateEdges(null);
        _password.PasswordChanged += (_, _) => InvalidateObs(); _port.ValueChanged += (_, _) => InvalidateObs();
        _connect.Click += async (_, _) =>
        {
            if (!double.IsFinite(_port.Value) || _port.Value is < 1024 or > 65535) { _obsStatus.Text = L.T("Enter a port between 1024 and 65535."); return; }
            var generation = ++_obsGeneration; var entered = _password.Password;
            _connect.IsEnabled = false; _obsStatus.Text = L.T("Connecting to OBS…");
            try
            {
                var result = await new ObsService().SampleAsync(true, (int)_port.Value, entered.Length > 0 ? entered : _secret.Read(), _lifetime.Token);
                if (_closed || generation != _obsGeneration) return;
                _connected = result.Connected; _obsStatus.Text = result.Status;
                if (_connected) { _draft = _draft with { ObsEnabled = true, ObsPort = (int)_port.Value }; _passwordToSave = entered.Length > 0 ? entered : null; }
                _next.IsEnabled = _connected;
            }
            catch (OperationCanceledException) { }
            finally { if (!_closed) _connect.IsEnabled = true; }
        };
        _city.ChoiceChanged += () => { if (_step == 5) _next.IsEnabled = _city.SelectedLocation is { IsValid: true }; };
        Closed += (_, _) => { _closed = true; _greeting.Stop(); _lifetime.Cancel(); _weather.Dispose(); _password.Password = ""; _passwordToSave = null; };
        var presenter = (OverlappedPresenter)AppWindow.Presenter;
        presenter.IsResizable = false; presenter.IsMaximizable = false; presenter.IsMinimizable = false;
        AppWindow.Closing += (_, args) => { if (_saving && !_finished) args.Cancel = true; };
        var work = DisplayArea.GetFromWindowId(AppWindow.Id, DisplayAreaFallback.Primary).WorkArea;
        var scale = Math.Max(96, GetDpiForWindow(WindowNative.GetWindowHandle(this))) / 96d;
        var width = Math.Min(work.Width - 32, (int)(720 * scale)); var height = Math.Min(work.Height - 32, (int)(670 * scale));
        AppWindow.MoveAndResize(new RectInt32(work.X + (work.Width - width) / 2, work.Y + (work.Height - height) / 2, width, height));
        RefreshLanguage(); RenderStep();
    }
    private void RefreshLanguage()
    {
        Title = L.T("Nagi · Welcome");
        _root.FlowDirection = L.Direction;
        _back.Content = L.T("Back"); _language.Header = L.T("Language");
        _name.Header = L.T("Your name"); _name.PlaceholderText = L.T("How should Nagi greet you?");
        _theme.Header = L.T("Theme"); _orientation.Header = L.T("Dock layout"); _edge.Header = L.T("Screen edge");
        _port.Header = L.T("OBS WebSocket port"); _password.Header = L.T("OBS password");
        _password.PlaceholderText = L.T(_secret.Exists ? "Saved password · enter to replace" : "Server password");
        _connect.Content = L.T("Connect to OBS");
        L.Options(_theme); L.Options(_orientation); L.Options(_edge);
        _city.RefreshLanguage();
        var selected = _draft.Language;
        _changingLanguage = true;
        _language.ItemsSource = LanguageChoices.All();
        _language.SelectedItem = ((LanguageChoice[])_language.ItemsSource).FirstOrDefault(l => l.Code == selected) ?? ((LanguageChoice[])_language.ItemsSource)[0];
        _changingLanguage = false;
        _languageStatus.Text = L.F($"Current language: {L.Catalog.Selected.NativeName}");
    }
    private void AddRow(FrameworkElement element, int row) { Grid.SetRow(element, row); _root.Children.Add(element); }
    private static TextBlock Note(string text) => new() { Text = text, TextWrapping = TextWrapping.Wrap, FontSize = 13, Opacity = .8 };
    private void InvalidateObs() { _obsGeneration++; _connected = false; if (_step == 4) _next.IsEnabled = false; }
    private void UpdateEdges(string? selected)
    {
        var horizontal = _orientation.SelectedItem as string == "Horizontal";
        var edges = horizontal ? new[] { "Top edge", "Bottom edge" } : new[] { "Right edge", "Left edge" };
        _edge.ItemsSource = edges; _edge.SelectedItem = selected is not null && edges.Contains(selected) ? selected : horizontal ? "Bottom edge" : "Right edge";
    }
    private void RenderStep()
    {
        _greeting.Stop(); _body.Children.Clear(); _error.Text = ""; _stepLabel.Text = _step < 2 ? "" : L.F($"{_step - 1} / 5");
        _back.Visibility = _step > 0 ? Visibility.Visible : Visibility.Collapsed;
        _skip.Visibility = _step is 4 or 5 ? Visibility.Visible : Visibility.Collapsed;
        _skip.Content = _step == 4 ? L.T("Skip OBS") : L.T("Skip weather");
        _next.Content = _step == 6 ? L.T("Start Nagi") : L.T("Continue");
        _next.IsEnabled = _step == 4 ? _connected : _step != 5 || _city.SelectedLocation is { IsValid: true };
        switch (_step)
        {
            case 0:
                _title.Text = L.T("Hello."); _description.Text = L.T("Your calm desktop companion.");
                _body.Children.Add(_greeting); _greeting.Start(_draft.ReducedMotion);
                break;
            case 1:
                _title.Text = L.T("Choose your language.");
                _description.Text = L.T("Nagi follows your system language. You can choose another before continuing.");
                _body.Children.Add(_language); _body.Children.Add(_languageStatus);
                _body.Children.Add(Note(L.T("You can change this later in Settings → General.")));
                break;
            case 2:
                _title.Text = L.T("A little calm, made yours."); _description.Text = L.T("Welcome to Nagi. Start with the name you'd like to see in your greeting.");
                _body.Children.Add(_name); _body.Children.Add(Note(L.T("This is a local display name. No account is created."))); break;
            case 3:
                _title.Text = L.T("Choose your space."); _description.Text = L.T("Pick a theme, a layout, and the edge where Nagi will live.");
                _body.Children.Add(_theme); _body.Children.Add(_orientation); _body.Children.Add(_edge);
                _body.Children.Add(Note(L.T("Vertical: a slim rail on the left or right. Horizontal: a compact bar along the top or bottom. Details open inward. You can change these in Settings."))); break;
            case 4:
                _title.Text = L.T("Bring OBS along. Or skip it."); _description.Text = L.T("Optional recording controls and studio status, connected only to OBS on this computer.");
                _body.Children.Add(Note(L.T("In OBS, enable Tools → WebSocket Server Settings, then enter the matching connection below.")));
                _body.Children.Add(_port); _body.Children.Add(_password); _body.Children.Add(_connect); _body.Children.Add(_obsStatus);
                _body.Children.Add(Note(L.T("Connection only checks status. Recording always requires a later button click. Your password is protected for your Windows account when you finish."))); break;
            case 5:
                _title.Text = L.T("A small window on the weather."); _description.Text = L.T("Choose a city for current conditions, or skip and keep Nagi offline.");
                _body.Children.Add(_city); break;
            case 6:
                _title.Text = L.T("Make your profiles.");
                _description.Text = L.T("Customize Default and the presets, or create profiles with your own names. Choose widgets and apps for each, then start Nagi when you are ready.");
                _body.Children.Add(new ProfileSetupEditor(_draft, next => _draft = next, _lifetime.Token)); break;
        }
        ApplyTheme();
        if (!_draft.ReducedMotion && new global::Windows.UI.ViewManagement.UISettings().AnimationsEnabled)
        {
            var visual = Microsoft.UI.Xaml.Hosting.ElementCompositionPreview.GetElementVisual(_body);
            var fade = visual.Compositor.CreateScalarKeyFrameAnimation(); fade.InsertKeyFrame(0, 0); fade.InsertKeyFrame(1, 1); fade.Duration = TimeSpan.FromMilliseconds(200);
            visual.StartAnimation("Opacity", fade);
        }
    }
    private void ApplyTheme()
    {
        var theme = _theme.SelectedItem as string ?? "Dark";
        _root.RequestedTheme = theme == "Light" ? ElementTheme.Light : theme == "System" ? ElementTheme.Default : ElementTheme.Dark;
        var contrast = new global::Windows.UI.ViewManagement.AccessibilitySettings().HighContrast;
        if (contrast)
        {
            SystemBackdrop = null;
            _root.Background = new SolidColorBrush(new global::Windows.UI.ViewManagement.UISettings().GetColorValue(global::Windows.UI.ViewManagement.UIColorType.Background));
            _next.ClearValue(Control.BackgroundProperty); _next.ClearValue(Control.ForegroundProperty); return;
        }
        var light = theme == "Light" || theme == "System" && _root.ActualTheme == ElementTheme.Light;
        _glass.SetLight(light); _glass.SetAkeno(theme == "Akeno");
        var appearance = _draft with { Theme = theme };
        _glass.SetTint(AppAppearance.Tint(appearance));
        SystemBackdrop = AppAppearance.UsesGlass(_draft) && HaloAcrylicBackdrop.IsSupported ? _glass : null;
        _root.Background = new SolidColorBrush(AppAppearance.Surface(appearance, light, SystemBackdrop is not null));
        _next.Background = new SolidColorBrush(Color.FromArgb(255, 207, 58, 62)); _next.Foreground = new SolidColorBrush(Microsoft.UI.Colors.White);
        var dark = light ? 0 : 1; DwmSetWindowAttribute(WindowNative.GetWindowHandle(this), 20, ref dark, 4);
    }
    private async Task NextAsync()
    {
        if (_saving) return;
        if (_step == 6) { await FinishAsync(); return; }
        if (_step == 2)
        {
            var name = _name.Text.Trim();
            if (name.Length is < 1 or > 32 || name.Any(char.IsControl)) { _error.Text = L.T("Enter a name of 1–32 characters."); return; }
            _draft = _draft with { UserName = name };
        }
        if (_step == 3) _draft = _draft with { Theme = _theme.SelectedItem as string ?? "Dark", Position = _draft.Position with
            { Orientation = _orientation.SelectedItem as string ?? "Vertical", Anchor = _edge.SelectedItem as string ?? "Right edge", OffsetX = 0, OffsetY = 0 } };
        if (_step == 4 && !_connected) return;
        if (_step == 5)
        {
            if (_city.SelectedLocation is not { IsValid: true } city) { _city.Validation(L.T("Choose a city, or select Skip weather.")); return; }
            _draft = _draft with { WeatherEnabled = true, WeatherLocation = city, WeatherUnit = _city.SelectedUnit };
            _step = 6; RenderStep(); return;
        }
        _step++; RenderStep();
    }
    private async Task FinishAsync()
    {
        if (_saving) return;
        _saving = true; _next.IsEnabled = _back.IsEnabled = _skip.IsEnabled = false;
        try
        {
            var settings = (_draft with { WelcomeCompleted = true, StartExpanded = false }).Normalize();
            if (settings.ObsEnabled && _passwordToSave is { } password) _secret.Save(password);
            await _settings.SaveAsync(settings, _lifetime.Token);
            if (_closed) return;
            _finished = true; Finished?.Invoke(settings);
        }
        catch (OperationCanceledException) { }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException or System.ComponentModel.Win32Exception)
        { _error.Text = L.T("Setup could not be saved. Check access to the Nagi data folder, then try again."); }
        finally { if (!_closed) { _saving = false; _back.IsEnabled = _skip.IsEnabled = true; _next.IsEnabled = true; } }
    }
    [DllImport("user32.dll")] private static extern uint GetDpiForWindow(nint hwnd);
    [DllImport("dwmapi.dll")] private static extern int DwmSetWindowAttribute(nint hwnd, int attribute, ref int value, int size);
}
