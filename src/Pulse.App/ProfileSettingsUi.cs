using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Pulse.Core;

namespace Pulse.App;

public sealed partial class MainWindow
{
    private readonly Dictionary<Profile, TextBlock> _profileBadges = [];

    private static string ProfileGlyph(Profile profile) => profile switch
    { Profile.Work => "\uE821", Profile.Gaming => "\uE7FC", Profile.Stream => "\uE714", Profile.Focus => "\uE708", _ => "\uE80F" };

    private void RefreshProfileBadges()
    {
        foreach (var (profile, badge) in _profileBadges)
            badge.Visibility = ViewModel.Profile == profile ? Visibility.Visible : Visibility.Collapsed;
    }

    private void OpenWorkspaceEditor(Profile profile)
    {
        _editingWorkspace = profile; BuildRulesPage();
        ContentScroll.ChangeView(null, 0, null, true);
        Animate(WorkspacePage);
    }

    private void BuildProfileList()
    {
        _profileBadges.Clear();
        WorkspacePage.Children.Add(Heading(L.T("Profiles")));
        WorkspacePage.Children.Add(Note(L.T("Choose a profile to edit its widgets, apps and activation rules.")));
        foreach (var profile in ViewModel.Settings.Profiles)
        {
            var workspace = ViewModel.Settings.Workspace(profile);
            var row = new Grid { ColumnSpacing = 14 };
            row.ColumnDefinitions.Add(new() { Width = GridLength.Auto }); row.ColumnDefinitions.Add(new());
            row.ColumnDefinitions.Add(new() { Width = GridLength.Auto }); row.ColumnDefinitions.Add(new() { Width = GridLength.Auto });
            row.Children.Add(new Border { Width = 42, Height = 42, CornerRadius = new(12), Background = ThemeBrush("AccentWashBrush"),
                Child = new FontIcon { Glyph = ProfileGlyph(profile), FontSize = 19, Foreground = ThemeBrush("PulseAccentBrush") } });
            var copy = Stack(); copy.Spacing = 4; copy.VerticalAlignment = VerticalAlignment.Center;
            copy.Children.Add(Note(L.T(ModePresets.For(workspace) + " mode")));
            copy.Children.Add(new TextBlock { Text = ProfileLabel(profile), FontSize = 16, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold });
            var rules = ViewModel.Settings.Rules.Count(r => r.Profile == profile && r.Enabled);
            copy.Children.Add(Note(L.F($"{workspace.Apps.Length} apps · {workspace.Widgets.Length} widgets · {rules} activation rules")));
            Grid.SetColumn(copy, 1); row.Children.Add(copy);
            var badge = new TextBlock { Text = L.T("Active"), Foreground = ThemeBrush("PulseAccentBrush"), FontSize = 11,
                VerticalAlignment = VerticalAlignment.Center }; _profileBadges[profile] = badge;
            Grid.SetColumn(badge, 2); row.Children.Add(badge);
            var arrow = new FontIcon { Glyph = "\uE76C", FontSize = 12, VerticalAlignment = VerticalAlignment.Center, Foreground = ThemeBrush("MutedBrush") };
            Grid.SetColumn(arrow, 3); row.Children.Add(arrow);
            var card = new Button { Content = row, Background = ThemeBrush("SurfaceBrush"), BorderThickness = new(0), CornerRadius = new(16),
                Padding = new(14), MinHeight = 72, HorizontalAlignment = HorizontalAlignment.Stretch, HorizontalContentAlignment = HorizontalAlignment.Stretch };
            AutomationProperties.SetName(card, L.F($"Customize {ProfileLabel(profile)}"));
            AutomationProperties.SetAutomationId(card, "ProfileCard" + profile);
            card.Click += (_, _) => OpenWorkspaceEditor(profile); _motion.Wire(card);
            var item = new Grid { ColumnSpacing = 8 }; item.ColumnDefinitions.Add(new()); item.ColumnDefinitions.Add(new() { Width = GridLength.Auto });
            item.Children.Add(card);
            var activate = Button(L.T("Activate ") + ProfileLabel(profile), async (_, _) => { await ActivateProfileAsync(profile); RefreshProfileBadges(); });
            AutomationProperties.SetAutomationId(activate, "ActivateProfile" + (int)profile);
            Grid.SetColumn(activate, 1); item.Children.Add(activate); WorkspacePage.Children.Add(item);
        }
        WorkspacePage.Children.Add(Button(L.T("+ Create profile"), async (_, _) => await CreateProfileAsync()));
        RefreshProfileBadges();
        var switching = Stack(); switching.Children.Add(Heading(L.T("Profile switching")));
        switching.Children.Add(Toggle(L.T("Automatic context switching"), L.T("Match the foreground application against your rules."), ViewModel.Settings.AutomaticSwitching,
            (s, enabled) => s with { AutomaticSwitching = enabled, LockedProfile = enabled ? null : s.LockedProfile }));
        switching.Children.Add(Button(L.T("Use automatic profiles"), async (_, _) => { await ActivateProfileAsync(null); BuildRulesPage(); }));
        var fallback = Select(ViewModel.Settings.Profiles.Select(ViewModel.Settings.ProfileName), ViewModel.Settings.ProfileName(ViewModel.Settings.DefaultProfile), "Default profile");
        fallback.SelectionChanged += async (_, _) => { if (fallback.SelectedItem is string value) await CommitAsync(s => s with { DefaultProfile = ParseProfile(value) }); };
        switching.Children.Add(Labeled(L.T("Fallback when no application rule matches"), fallback));
        WorkspacePage.Children.Add(Card(switching));
        var shortcuts = Stack();
        foreach (var action in ViewModel.Settings.QuickActions)
        {
            var row = Stack(); row.Children.Add(Heading(action.Label)); row.Children.Add(Note(action.Target));
            var buttons = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
            buttons.Children.Add(Button(L.T("Edit"), async (_, _) => await EditActionAsync(action)));
            buttons.Children.Add(Button(L.T("Remove"), async (_, _) => { if (await CommitAsync(s => s with { QuickActions = s.QuickActions.Where(a => a != action).ToArray() })) BuildRulesPage(); }));
            row.Children.Add(buttons); shortcuts.Children.Add(Card(row));
        }
        shortcuts.Children.Add(Button(L.T("+ Add quick action"), async (_, _) => await EditActionAsync(null)));
        WorkspacePage.Children.Add(new Expander { Header = L.T("Quick actions"), Content = shortcuts, HorizontalAlignment = HorizontalAlignment.Stretch, HorizontalContentAlignment = HorizontalAlignment.Stretch });
    }

    private void BuildProfileTimer(Profile profile)
    {
        var timer = ViewModel.Settings.TimerFor(profile);
        var content = Stack(); content.Children.Add(Heading(L.T("Timer")));
        var automatic = Toggle(L.T("Start timer when this profile activates"), L.T("An active timer keeps running when you switch profiles."), timer.AutoStart,
            (s, enabled) => s with { Workspaces = s.Workspaces.Select(w => w.Profile == profile
                ? w with { Timer = s.TimerFor(profile) with { AutoStart = enabled } } : w).ToArray() });
        AutomationProperties.SetAutomationId(automatic, "ProfileTimerAutoStart"); content.Children.Add(automatic);
        var duration = new NumberBox { Header = L.T("Duration (minutes)"), Minimum = 1, Maximum = 1440, Value = timer.Minutes,
            SmallChange = 1, LargeChange = 5, SpinButtonPlacementMode = NumberBoxSpinButtonPlacementMode.Compact };
        AutomationProperties.SetAutomationId(duration, "ProfileTimerMinutes"); content.Children.Add(duration);
        var save = Button(L.T("Save duration"), async (_, _) =>
        {
            if (!double.IsFinite(duration.Value) || duration.Value is < 1 or > 1440 || duration.Value != Math.Truncate(duration.Value))
            { ViewModel.Publish(L.T("Enter a whole number of minutes from 1 to 1440."), true); return; }
            var current = ViewModel.Settings.Workspace(profile);
            await SaveWorkspaceAsync(current with { Timer = ViewModel.Settings.TimerFor(profile) with { Minutes = (int)duration.Value } });
        });
        AutomationProperties.SetAutomationId(save, "ProfileTimerSave"); content.Children.Add(save);
        content.Children.Add(Note(L.T("An active timer keeps running when you switch profiles.")));
        WorkspacePage.Children.Insert(4, Card(content));
    }

    private void BuildProfileActivation(Profile profile)
    {
        var activation = Stack(); activation.Children.Add(Heading(L.T("When to activate")));
        activation.Children.Add(Note(L.T("Switch to this profile when a matching executable becomes the active window.")));
        if (ViewModel.Settings.LockedProfile is not null || !ViewModel.Settings.AutomaticSwitching)
            activation.Children.Add(Button(L.T("Use automatic profiles"), async (_, _) => { await ActivateProfileAsync(null); BuildRulesPage(); }));
        var rules = ViewModel.Settings.Rules.Where(r => r.Profile == profile).ToArray();
        if (rules.Length == 0) activation.Children.Add(Note(L.T("No application rules. Select this profile manually.")));
        foreach (var rule in rules)
        {
            var content = Stack(); content.Spacing = 6;
            content.Children.Add(new TextBlock { Text = rule.Identity, FontSize = 12, TextWrapping = TextWrapping.Wrap });
            content.Children.Add(Note(L.F($"Priority {rule.Priority}") + " · " + L.T(rule.Enabled ? "Enabled" : "Disabled")));
            var controls = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 };
            controls.Children.Add(Button(L.T("Edit"), async (_, _) => await EditRuleAsync(rule, profile)));
            controls.Children.Add(Button(L.T(rule.Enabled ? "Disable" : "Enable"), async (_, _) =>
            { if (await CommitAsync(s => s with { Rules = s.Rules.Select(r => r == rule ? r with { Enabled = !r.Enabled } : r).ToArray() })) BuildRulesPage(); }));
            controls.Children.Add(Button(L.T("Remove"), async (_, _) =>
            { if (await CommitAsync(s => s with { Rules = s.Rules.Where(r => r != rule).ToArray() })) BuildRulesPage(); }));
            content.Children.Add(controls);
            activation.Children.Add(new Border { Child = content, Background = ThemeBrush("InsetBrush"), CornerRadius = new(10), Padding = new(12) });
        }
        activation.Children.Add(Button(L.T("+ Add application rule"), async (_, _) => await EditRuleAsync(null, profile)));
        activation.Children.Add(Note(L.T("Highest priority wins. Manual activation stays locked until you choose automatic profiles.")));
        WorkspacePage.Children.Add(Card(activation));
    }
}
