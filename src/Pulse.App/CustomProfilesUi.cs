using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Pulse.Core;

namespace Pulse.App;

public sealed partial class MainWindow
{
    private string ProfileLabel(Profile profile) => ViewModel.Settings.CustomProfiles.Any(p => p.Id == profile)
        ? ViewModel.Settings.ProfileName(profile) : L.T(profile.ToString());
    private Profile ParseProfile(string name) => ViewModel.Settings.Profiles.First(p => ViewModel.Settings.ProfileName(p) == name);
    private async Task CreateProfileAsync()
    {
        var name = new TextBox { Header = L.T("Profile name"), MaxLength = 40 };
        AutomationProperties.SetAutomationId(name, "NewProfileName");
        var template = Select(ViewModel.Settings.Profiles.Select(ViewModel.Settings.ProfileName), "Default", "Copy widgets and apps from");
        var content = Stack(); content.Children.Add(name); content.Children.Add(Labeled(L.T("Copy widgets and apps from"), template));
        var error = Note(""); content.Children.Add(error);
        var dialog = Dialog(L.T("Create profile"), content);
        dialog.PrimaryButtonClick += async (_, args) =>
        {
            try
            {
                var next = ProfileCustomization.Create(ViewModel.Settings, name.Text, ParseProfile((string)template.SelectedItem));
                var id = next.CustomProfiles.Last().Id;
                var deferral = args.GetDeferral();
                try { if (await CommitAsync(_ => next)) { _editingWorkspace = id; BuildRulesPage(); } else args.Cancel = true; }
                finally { deferral.Complete(); }
            }
            catch (ArgumentException ex) { error.Text = L.T(ex.Message); args.Cancel = true; }
        };
        await ShowDialogAsync(dialog);
    }
    private void BuildCustomProfileOptions(Profile profile)
    {
        if (ViewModel.Settings.CustomProfiles.All(p => p.Id != profile)) return;
        var content = Stack(); var name = new TextBox { Header = L.T("Profile name"), Text = ViewModel.Settings.ProfileName(profile), MaxLength = 40 };
        content.Children.Add(name);
        content.Children.Add(Button(L.T("Rename profile"), async (_, _) =>
        {
            try { if (await CommitAsync(s => ProfileCustomization.Rename(s, profile, name.Text))) BuildRulesPage(); }
            catch (ArgumentException ex) { ViewModel.Publish(L.T(ex.Message), true); }
        }));
        content.Children.Add(Button(L.T("Delete profile"), async (_, _) =>
        { if (await CommitAsync(s => ProfileCustomization.Remove(s, profile))) { _editingWorkspace = null; BuildRulesPage(); } }));
        WorkspacePage.Children.Add(Card(content));
    }
}
