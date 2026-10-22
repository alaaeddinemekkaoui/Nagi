using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Pulse.Core;
using Pulse.Windows;

namespace Pulse.App.Controls;

// Welcome edits a draft only. Nothing is saved or launched until setup is completed.
public sealed class ProfileSetupEditor : UserControl
{
    private PulseSettings _draft;
    private readonly Action<PulseSettings> _changed;
    private readonly CancellationToken _token;
    private readonly StackPanel _content = new() { Spacing = 12 };
    private ApplicationCatalog _catalog = new([]);
    private Profile _selected = Profile.Default;
    private bool _loaded;
    private string _newProfileName = "";
    public ProfileSetupEditor(PulseSettings draft, Action<PulseSettings> changed, CancellationToken token)
    {
        _draft = draft; _changed = changed; _token = token; Content = _content;
        Loaded += async (_, _) =>
        {
            if (_loaded) return; _loaded = true;
            try { _catalog = await SearchApplications.ReadAsync(_token); if (!_token.IsCancellationRequested) Render(); }
            catch (OperationCanceledException) { }
        };
        Render();
    }
    private string ProfileName(Profile p) => _draft.CustomProfiles.Any(c => c.Id == p) ? _draft.ProfileName(p) : L.T(p.ToString());
    private void Set(PulseSettings next) { _draft = next.Normalize(); _changed(_draft); }
    private void Workspace(Func<ProfileWorkspace, ProfileWorkspace> update) => Set(_draft with
    { Workspaces = _draft.Workspaces.Select(w => w.Profile == _selected ? update(w) : w).ToArray() });
    private static Button Action(string text, RoutedEventHandler click)
    { var button = new Button { Content = text }; button.Click += click; return button; }
    private void Render()
    {
        _content.Children.Clear();
        var profiles = new ComboBox { Header = L.T("Choose a profile to customize"), ItemsSource = _draft.Profiles,
            HorizontalAlignment = HorizontalAlignment.Stretch };
        // Labels are objects so custom names never need enum parsing or translation keys.
        var choices = _draft.Profiles.Select(p => new Choice(p, ProfileName(p))).ToArray();
        profiles.ItemsSource = choices; profiles.SelectedItem = choices.First(p => p.Id == _selected);
        AutomationProperties.SetAutomationId(profiles, "WelcomeProfilePicker");
        profiles.SelectionChanged += (_, _) => { if (profiles.SelectedItem is Choice choice && choice.Id != _selected) { _selected = choice.Id; Render(); } };
        _content.Children.Add(profiles);
        var create = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        var name = new TextBox { PlaceholderText = L.T("New profile name"), Text = _newProfileName, MaxLength = 40, Width = 240 };
        name.TextChanged += (_, _) => _newProfileName = name.Text;
        AutomationProperties.SetAutomationId(name, "WelcomeNewProfileName"); create.Children.Add(name);
        var error = new TextBlock { TextWrapping = TextWrapping.Wrap };
        var add = Action(L.T("Create profile"), (_, _) =>
        {
            try { Set(ProfileCustomization.Create(_draft, name.Text, _selected)); _selected = _draft.CustomProfiles.Last().Id; _newProfileName = ""; Render(); }
            catch (ArgumentException ex) { error.Text = L.T(ex.Message); }
        });
        AutomationProperties.SetAutomationId(add, "WelcomeCreateProfile"); create.Children.Add(add); _content.Children.Add(create); _content.Children.Add(error);
        _content.Children.Add(new TextBlock { Text = ProfileName(_selected), FontSize = 19, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold });
        var activate = Action(L.T("Activate ") + ProfileName(_selected), (_, _) => { Set(_draft with { LockedProfile = _selected }); Render(); });
        AutomationProperties.SetAutomationId(activate, "WelcomeActivateProfile"); _content.Children.Add(activate);
        _content.Children.Add(new TextBlock { Text = L.T("Starting profile") + ": " + ProfileName(_draft.LockedProfile ?? _draft.DefaultProfile), TextWrapping = TextWrapping.Wrap });
        var grid = new Grid { ColumnSpacing = 12, RowSpacing = 4 }; grid.ColumnDefinitions.Add(new()); grid.ColumnDefinitions.Add(new());
        var workspace = _draft.Workspace(_selected);
        for (var i = 0; i < ProfileWorkspaces.WidgetNames.Length; i++)
        {
            if (i % 2 == 0) grid.RowDefinitions.Add(new() { Height = GridLength.Auto });
            var widget = ProfileWorkspaces.WidgetNames[i];
            var check = new CheckBox { Content = L.T(widget), IsChecked = workspace.Shows(widget) };
            AutomationProperties.SetAutomationId(check, "WelcomeWidget" + widget);
            check.Checked += (_, _) => Workspace(w => w with { Widgets = [.. w.Widgets, widget] });
            check.Unchecked += (_, _) => Workspace(w => w with { Widgets = w.Widgets.Where(v => v != widget).ToArray() });
            Grid.SetRow(check, i / 2); Grid.SetColumn(check, i % 2); grid.Children.Add(check);
        }
        _content.Children.Add(grid);
        var timer = _draft.TimerFor(_selected);
        var automatic = new ToggleSwitch { Header = L.T("Start timer when this profile activates"), IsOn = timer.AutoStart,
            OnContent = L.T("On"), OffContent = L.T("Off") };
        AutomationProperties.SetAutomationId(automatic, "WelcomeProfileTimerAutoStart");
        automatic.Toggled += (_, _) => Workspace(w => w with { Timer = _draft.TimerFor(_selected) with { AutoStart = automatic.IsOn } });
        _content.Children.Add(automatic);
        var duration = new NumberBox { Header = L.T("Duration (minutes)"), Minimum = 1, Maximum = 1440, Value = timer.Minutes,
            SmallChange = 1, LargeChange = 5, SpinButtonPlacementMode = NumberBoxSpinButtonPlacementMode.Compact };
        AutomationProperties.SetAutomationId(duration, "WelcomeProfileTimerMinutes");
        duration.ValueChanged += (_, _) =>
        {
            if (double.IsFinite(duration.Value) && duration.Value is >= 1 and <= 1440 && duration.Value == Math.Truncate(duration.Value))
                Workspace(w => w with { Timer = _draft.TimerFor(_selected) with { Minutes = (int)duration.Value } });
        };
        _content.Children.Add(duration);
        var picker = new ComboBox { Header = L.T("App dock"), ItemsSource = _catalog.Entries.OrderBy(e => e.Title).ToArray(), DisplayMemberPath = "Title",
            IsEditable = true, IsTextSearchEnabled = true, PlaceholderText = L.T("Choose an installed app"), HorizontalAlignment = HorizontalAlignment.Stretch };
        AutomationProperties.SetAutomationId(picker, "WelcomeAppPicker"); _content.Children.Add(picker);
        _content.Children.Add(Action(L.T("Add selected app"), (_, _) =>
        {
            if (picker.SelectedItem is SearchEntry entry && _draft.Workspace(_selected).Apps.Length < 16)
            { Workspace(w => w with { Apps = [.. w.Apps, new(entry.Title, entry.Target)] }); Render(); }
        }));
        foreach (var app in workspace.Apps)
        {
            var row = new Grid(); row.ColumnDefinitions.Add(new()); row.ColumnDefinitions.Add(new() { Width = GridLength.Auto });
            row.Children.Add(new TextBlock { Text = app.Name, VerticalAlignment = VerticalAlignment.Center });
            var remove = Action(L.T("Remove ") + app.Name, (_, _) => { Workspace(w => w with { Apps = w.Apps.Where(a => a != app).ToArray() }); Render(); });
            Grid.SetColumn(remove, 1); row.Children.Add(remove); _content.Children.Add(row);
        }
    }
    private sealed record Choice(Profile Id, string Label) { public override string ToString() => Label; }
}
