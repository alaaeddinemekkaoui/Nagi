using Microsoft.UI.Dispatching;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Pulse.App.Themes;
using Pulse.Core;
using Pulse.Windows;
using Windows.ApplicationModel.DataTransfer;
using Windows.Graphics;
using VirtualKey = Windows.System.VirtualKey;
using WinRT.Interop;

namespace Pulse.App;

public sealed class QuickSearchWindow : Window
{
    private readonly Grid _root = new() { Padding = new(22), RowSpacing = 12 };
    private readonly TextBox _query = new() { PlaceholderText = L.T("Search apps, files, folders… or calculate"), MaxLength = 256, FontSize = 20, MinHeight = 52, CornerRadius = new(12) };
    private readonly Dictionary<SearchProvider, Microsoft.UI.Xaml.Controls.Primitives.ToggleButton> _providerButtons = [];
    private SearchProvider _provider;
    private bool _updatingProviders;
    private readonly ListView _results = new() { SelectionMode = ListViewSelectionMode.Single, IsItemClickEnabled = true, Background = new SolidColorBrush(Microsoft.UI.Colors.Transparent) };
    private readonly TextBlock _status = new() { FontSize = 11, TextWrapping = TextWrapping.Wrap };
    private readonly TextBlock _detail = new() { FontSize = 11, TextTrimming = TextTrimming.CharacterEllipsis };
    private readonly Button _open = new() { Content = L.T("Open ↵"), MinWidth = 78 };
    private readonly Button _reveal = new() { Content = L.T("Show in folder") };
    private readonly Button _copy = new() { Content = L.T("Copy path") };
    private readonly DispatcherQueueTimer _delay;
    private readonly Func<SearchEntry[]> _entries;
    private readonly Func<string> _indexStatus;
    private readonly Func<SearchEntry, string, Task<OperationResult>> _execute;
    private readonly Func<string, Brush> _brush;
    private readonly HaloAcrylicBackdrop _glass = new();
    private readonly nint _handle;
    private int _generation, _openGeneration;
    private bool _visible, _closing, _executing;
    public bool IsOpen => _visible;
    public event Action? Dismissed;
    private SearchEntry? Selected => (_results.SelectedItem as ListViewItem)?.Tag as SearchEntry;

    public QuickSearchWindow(Func<SearchEntry[]> entries, Func<string> status,
        Func<SearchEntry, string, Task<OperationResult>> execute, Func<string, Brush> brush)
    {
        _entries = entries; _indexStatus = status; _execute = execute; _brush = brush;
        Title = L.T("Nagi · Quick search");
        _root.RowDefinitions.Add(new() { Height = GridLength.Auto });
        _root.RowDefinitions.Add(new() { Height = GridLength.Auto });
        _root.RowDefinitions.Add(new() { Height = new(1, GridUnitType.Star) });
        _root.RowDefinitions.Add(new() { Height = GridLength.Auto });
        _root.RowDefinitions.Add(new() { Height = GridLength.Auto });
        var header = new Grid(); header.ColumnDefinitions.Add(new() { Width = new(1, GridUnitType.Star) }); header.ColumnDefinitions.Add(new() { Width = GridLength.Auto });
        header.Children.Add(new TextBlock { Text = L.T("Nagi  /  Quick search"), FontSize = 14, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold });
        var close = new Button { Content = L.T("Esc"), Padding = new(10, 4, 10, 4) }; close.Click += (_, _) => Hide(); Grid.SetColumn(close, 1); header.Children.Add(close);
        _root.Children.Add(header);
        var input = new StackPanel { Spacing = 8 }; input.Children.Add(_query);
        var providers = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 };
        foreach (var provider in Enum.GetValues<SearchProvider>())
        {
            var label = provider == SearchProvider.Local ? "Nagi" : provider.ToString();
            var button = new Microsoft.UI.Xaml.Controls.Primitives.ToggleButton { Content = label, MinHeight = 30, Padding = new(12, 4, 12, 4), CornerRadius = new(15) };
            AutomationProperties.SetAutomationId(button, "QuickSearchProvider" + provider);
            AutomationProperties.SetName(button, provider == SearchProvider.Local ? L.T("Search apps and files") : label);
            button.Checked += (_, _) => { if (!_updatingProviders) SelectProvider(provider); };
            button.Unchecked += (_, _) => { if (!_updatingProviders) UpdateProviders(); };
            providers.Children.Add(button); _providerButtons.Add(provider, button);
        }
        input.Children.Add(providers); Grid.SetRow(input, 1); _root.Children.Add(input);
        Grid.SetRow(_results, 2); _root.Children.Add(_results);
        var footer = new StackPanel { Spacing = 7 }; footer.Children.Add(_detail);
        var actions = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        actions.Children.Add(_open); actions.Children.Add(_reveal); actions.Children.Add(_copy); footer.Children.Add(actions);
        Grid.SetRow(footer, 3); _root.Children.Add(footer);
        var help = new StackPanel { Spacing = 4 }; help.Children.Add(_status);
        help.Children.Add(new TextBlock { Text = L.T("↑↓ Select   Enter Open   Ctrl+Enter Folder   Ctrl+C Path   Esc Close\napp: Apps   file: Files   folder: Folders   > Actions   web Search"), FontSize = 10, Opacity = .72, TextWrapping = TextWrapping.Wrap });
        help.Children.Add(new TextBlock { Text = L.T("Tab switches Nagi → Google → YouTube. Use browser:, google: or youtube: to choose directly. Shift+Tab moves keyboard focus."), FontSize = 10, Opacity = .72, TextWrapping = TextWrapping.Wrap });
        Grid.SetRow(help, 4); _root.Children.Add(help);
        _root.FlowDirection = L.Direction;
        Content = new Border { CornerRadius = new(20), Child = _root };
        _handle = WindowNative.GetWindowHandle(this);
        var presenter = (OverlappedPresenter)AppWindow.Presenter;
        presenter.SetBorderAndTitleBar(false, false); presenter.IsResizable = false; presenter.IsMaximizable = false; presenter.IsMinimizable = false; presenter.IsAlwaysOnTop = true;
        AppWindow.IsShownInSwitchers = false;
        SearchWindowPlacement.RemoveFrame(_handle);
        _delay = DispatcherQueue.CreateTimer(); _delay.Interval = TimeSpan.FromMilliseconds(100); _delay.IsRepeating = false;
        _delay.Tick += async (_, _) => await RefreshAsync();
        _query.TextChanged += (_, _) => { _generation++; UpdateProviders(); _delay.Stop(); _delay.Start(); };
        _results.SelectionChanged += (_, _) => UpdateSelection();
        _results.ItemClick += async (_, e) => { _results.SelectedItem = e.ClickedItem; await ExecuteAsync("open"); };
        _open.Click += async (_, _) => await ExecuteAsync("open");
        _reveal.Click += async (_, _) => await ExecuteAsync("reveal");
        _copy.Click += async (_, _) => await ExecuteAsync("copy");
        _root.AddHandler(UIElement.PreviewKeyDownEvent, new KeyEventHandler(OnKeyDown), true);
        _root.Loaded += (_, _) => _query.Focus(FocusState.Programmatic);
        Activated += async (_, args) =>
        {
            if (args.WindowActivationState != WindowActivationState.Deactivated) return;
            var generation = _openGeneration;
            // WinUI can deliver an earlier deactivation after a new window is activated.
            // Confirm native focus after the activation queue settles before dismissing.
            await Task.Delay(150);
            if (_visible && !_executing && generation == _openGeneration && !SearchWindowPlacement.IsForeground(_handle)) Hide();
        };
        AppWindow.Closing += (_, args) => { if (!_closing) { args.Cancel = true; Hide(); } };
        AutomationProperties.SetAutomationId(_query, "QuickSearchQuery"); AutomationProperties.SetName(_query, L.T("Search apps and files"));
        AutomationProperties.SetAutomationId(_results, "QuickSearchResults"); AutomationProperties.SetName(_results, L.T("Search results"));
        AutomationProperties.SetAutomationId(_status, "QuickSearchStatus"); AutomationProperties.SetName(close, L.T("Close quick search"));
        AutomationProperties.SetAutomationId(_detail, "QuickSearchDetail");
    }
    public void Open(PulseSettings settings)
    {
        var foreground = SearchWindowPlacement.Foreground;
        // Acrylic reads XamlRoot.Content, so the theme belongs on the outer surface.
        // Setting only the child Grid can leave light controls over a dark backdrop.
        if (Content is FrameworkElement themedSurface)
            themedSurface.RequestedTheme = settings.Theme switch { "Light" => ElementTheme.Light, "System" => ElementTheme.Default, _ => ElementTheme.Dark };
        _root.Background = new SolidColorBrush(Microsoft.UI.Colors.Transparent);
        var highContrast = new global::Windows.UI.ViewManagement.AccessibilitySettings().HighContrast;
        _glass.SetLight(_root.ActualTheme == ElementTheme.Light); _glass.SetAkeno(settings.Theme == "Akeno");
        _glass.SetTint(AppAppearance.Tint(settings));
        SystemBackdrop = AppAppearance.UsesGlass(settings) && !highContrast && HaloAcrylicBackdrop.IsSupported ? _glass : null;
        if (Content is Border surface) surface.Background = highContrast ? _brush("CanvasBrush")
            : new SolidColorBrush(AppAppearance.Surface(settings, _root.ActualTheme == ElementTheme.Light, SystemBackdrop is not null));
        _query.Foreground = _brush("InkBrush"); _query.Background = _brush("InsetBrush"); _status.Foreground = _detail.Foreground = _brush("MutedBrush");
        _open.Background = _brush("AccentWashBrush");
        _query.Resources["TextControlBorderBrushFocused"] = _brush("PulseAccentBrush");
        _results.Resources["ListViewItemSelectionIndicatorBrush"] = _brush("PulseAccentBrush");
        var frame = SearchWindowPlacement.Prepare(foreground);
        AppWindow.MoveAndResize(new RectInt32(frame.X, frame.Y, frame.Width, frame.Height));
        _visible = true; _openGeneration++; _provider = SearchProvider.Local; _query.Text = ""; UpdateProviders(); SearchWindowPlacement.Show(_handle); Activate();
        _query.Focus(FocusState.Programmatic); _query.SelectAll(); _ = RefreshAsync();
        if (!settings.ReducedMotion && new global::Windows.UI.ViewManagement.UISettings().AnimationsEnabled)
        {
            var visual = Microsoft.UI.Xaml.Hosting.ElementCompositionPreview.GetElementVisual(_root);
            var fade = visual.Compositor.CreateScalarKeyFrameAnimation(); fade.InsertKeyFrame(0, .2f); fade.InsertKeyFrame(1, 1); fade.Duration = TimeSpan.FromMilliseconds(140); visual.StartAnimation("Opacity", fade);
        }
    }
    public void Hide()
    {
        if (!_visible) return;
        _visible = false; _generation++; _delay.Stop(); SearchWindowPlacement.Hide(_handle);
        Dismissed?.Invoke();
    }
    public void DisposeWindow() { _closing = true; _visible = false; _generation++; _delay.Stop(); Close(); }
    public void IndexChanged() { if (_visible) { _delay.Stop(); _delay.Start(); } }
    private async Task RefreshAsync()
    {
        if (!_visible) return;
        var generation = ++_generation; var query = _query.Text; var entries = _entries(); var provider = _provider;
        var found = await Task.Run(() => QuickSearch.Find(entries, query, provider: provider));
        if (!_visible || generation != _generation) return;
        var selectedId = Selected?.Id; _results.Items.Clear();
        foreach (var entry in found)
        {
            var row = new Grid { ColumnSpacing = 14, Padding = new(4, 7, 8, 7) };
            row.ColumnDefinitions.Add(new() { Width = new(32) }); row.ColumnDefinitions.Add(new() { Width = new(1, GridUnitType.Star) });
            row.Children.Add(entry.Kind == SearchKind.Application
                ? new Controls.AppIcon(entry.Title, entry.Target, _brush("InkBrush"))
                : new FontIcon { Glyph = entry.Kind switch { SearchKind.File => "\uE8A5", SearchKind.Folder => "\uE8B7", SearchKind.Calculation => "\uE8EF", SearchKind.Web => "\uE774", _ => "\uE713" }, FontSize = 18, Foreground = _brush("PulseAccentBrush") });
            var text = new StackPanel { Spacing = 3 }; text.Children.Add(new TextBlock { Text = entry.Title, FontSize = 14, TextTrimming = TextTrimming.CharacterEllipsis, Foreground = _brush("InkBrush") });
            text.Children.Add(new TextBlock { Text = entry.Detail, FontSize = 11, TextTrimming = TextTrimming.CharacterEllipsis, Foreground = _brush("MutedBrush") }); Grid.SetColumn(text, 1); row.Children.Add(text);
            var item = new ListViewItem { Content = row, Tag = entry, HorizontalContentAlignment = HorizontalAlignment.Stretch, CornerRadius = new(10), Margin = new(0, 2, 0, 2) };
            AutomationProperties.SetName(item, entry.Title + " · " + entry.Kind); _results.Items.Add(item);
        }
        _results.SelectedIndex = found.Count == 0 ? -1 : Math.Max(0, found.ToList().FindIndex(e => e.Id == selectedId));
        _status.Text = QuickSearch.ParseQuery(query, provider).Provider == SearchProvider.Local
            ? (found.Count == 0 ? L.T("No matches. Try another name or filter.  ") : "") + _indexStatus()
            : L.T("Your query is sent only when you open the result.");
        UpdateSelection();
    }
    private void UpdateProviders()
    {
        var current = QuickSearch.ParseQuery(_query.Text, _provider).Provider;
        _updatingProviders = true;
        try
        {
            foreach (var (provider, button) in _providerButtons)
            {
                foreach (var state in new[] { "Checked", "CheckedPointerOver", "CheckedPressed" })
                {
                    button.Resources["ToggleButtonBackground" + state] = _brush("AccentWashBrush");
                    button.Resources["ToggleButtonForeground" + state] = _brush("InkBrush");
                    button.Resources["ToggleButtonBorderBrush" + state] = _brush("EdgeBrush");
                }
                button.IsChecked = current == provider;
                button.Background = _brush(current == provider ? "AccentWashBrush" : "InsetBrush");
            }
        }
        finally { _updatingProviders = false; }
    }
    private void SelectProvider(SearchProvider provider)
    {
        var request = QuickSearch.ParseQuery(_query.Text, _provider);
        _provider = provider;
        if (request.ExplicitProvider)
        {
            // Keep the search words when switching away from an explicit provider/filter.
            var text = request.Text;
            if (request.Provider == SearchProvider.Local)
                text = text.StartsWith('>') ? text[1..].TrimStart() : text.Contains(':') ? text[(text.IndexOf(':') + 1)..].TrimStart() : text;
            _query.Text = text;
        }
        UpdateProviders(); _query.Focus(FocusState.Programmatic); _query.SelectionStart = _query.Text.Length;
        _delay.Stop(); _ = RefreshAsync();
    }
    private void UpdateSelection()
    {
        var selected = Selected; _detail.Text = selected?.Target ?? L.T("Type an app name, filename, action, or 24 * 7.");
        if (selected?.Kind is SearchKind.Command or SearchKind.Calculation) _detail.Text = selected.Detail;
        _open.IsEnabled = selected is not null; _open.Content = selected?.Kind == SearchKind.Calculation ? L.T("Copy result ↵") : L.T("Open ↵");
        _reveal.IsEnabled = selected?.Kind is SearchKind.File or SearchKind.Folder;
        _copy.IsEnabled = selected?.Kind is SearchKind.File or SearchKind.Folder or SearchKind.Calculation;
        _copy.Content = selected?.Kind == SearchKind.Calculation ? L.T("Copy result") : L.T("Copy path");
    }
    private async Task ExecuteAsync(string action)
    {
        if (!_visible || _executing || Selected is not { } selected) return;
        _executing = true;
        try
        {
            var result = await _execute(selected, action);
            if (result.Success) Hide(); else _status.Text = result.Message;
        }
        catch (OperationCanceledException) { }
        catch (Exception) { _status.Text = L.T("This item could not be opened. Rebuild the index or try again."); }
        finally { _executing = false; }
    }
    private async void OnKeyDown(object sender, KeyRoutedEventArgs args)
    {
        var ctrl = (Microsoft.UI.Input.InputKeyboardSource.GetKeyStateForCurrentThread(VirtualKey.Control) & global::Windows.UI.Core.CoreVirtualKeyStates.Down) != 0;
        var shift = (Microsoft.UI.Input.InputKeyboardSource.GetKeyStateForCurrentThread(VirtualKey.Shift) & global::Windows.UI.Core.CoreVirtualKeyStates.Down) != 0;
        if (args.Key == VirtualKey.Escape) { args.Handled = true; Hide(); }
        else if (args.Key == VirtualKey.Tab && !ctrl && !shift && _query.FocusState != FocusState.Unfocused)
        {
            args.Handled = true;
            SelectProvider((SearchProvider)(((int)QuickSearch.ParseQuery(_query.Text, _provider).Provider + 1) % 3));
        }
        else if (args.Key is VirtualKey.Down or VirtualKey.Up)
        {
            args.Handled = true; var count = _results.Items.Count; if (count == 0) return;
            _results.SelectedIndex = Math.Clamp(_results.SelectedIndex + (args.Key == VirtualKey.Down ? 1 : -1), 0, count - 1); _results.ScrollIntoView(_results.SelectedItem);
        }
        else if (args.Key == VirtualKey.Enter)
        {
            if (_providerButtons.Values.Any(button => button.FocusState != FocusState.Unfocused)) return;
            args.Handled = true; _delay.Stop();
            var query = _query.Text; var provider = _provider;
            // Enter can arrive before the debounce refresh; never open an older result.
            await RefreshAsync();
            if (_visible && query == _query.Text && provider == _provider) await ExecuteAsync(ctrl ? "reveal" : "open");
        }
        else if (args.Key == VirtualKey.C && ctrl && _query.SelectionLength == 0 && Selected?.Kind is SearchKind.File or SearchKind.Folder or SearchKind.Calculation)
        { args.Handled = true; await ExecuteAsync("copy"); }
    }
}
