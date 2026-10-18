using System.Diagnostics;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;

namespace Pulse.App;

internal sealed record LanguageChoice(string Code, string Label)
{
    public override string ToString() => Label;
}

internal static class LanguageChoices
{
    public static LanguageChoice[] All() =>
    [
        new("System", L.T("System default") + " · " + L.SystemLanguage),
        .. L.Catalog.Languages.Select(l => new LanguageChoice(l.Code, l.NativeName + " · " + l.Name))
    ];
}

public sealed partial class MainWindow
{
    private void AddLanguageSettings(StackPanel panel)
    {
        var choices = LanguageChoices.All();
        var languages = new ComboBox { Header = L.T("Language"), ItemsSource = choices,
            SelectedItem = choices.FirstOrDefault(l => l.Code == ViewModel.Settings.Language) ?? choices[0],
            HorizontalAlignment = HorizontalAlignment.Stretch };
        AutomationProperties.SetAutomationId(languages, "AppLanguage");
        AutomationProperties.SetName(languages, L.T("Language"));
        panel.Children.Add(languages);
        var current = Note(L.F($"Current language: {L.Catalog.Selected.NativeName}"));
        AutomationProperties.SetAutomationId(current, "SettingsCurrentLanguage"); panel.Children.Add(current);
        var status = Note(L.T("Restart Nagi to apply a different language.")); panel.Children.Add(status);
        languages.SelectionChanged += async (_, _) =>
        {
            if (languages.SelectedItem is LanguageChoice choice && await CommitAsync(s => s with { Language = choice.Code }))
                status.Text = L.T("Language saved. Restart Nagi to apply it.");
        };
        var restart = Button(L.T("Restart Nagi"), (_, _) => RestartForLanguage());
        AutomationProperties.SetAutomationId(restart, "RestartForLanguage"); panel.Children.Add(restart);
    }

    private void RestartForLanguage()
    {
        if (_closed || _setupRestarting || _dialogOpen) return;
        try
        {
            var start = new ProcessStartInfo(Environment.ProcessPath!) { UseShellExecute = false, CreateNoWindow = true };
            start.ArgumentList.Add("--wait-for-exit"); start.ArgumentList.Add(Environment.ProcessId.ToString());
            if (((App)Application.Current).LaunchOptions.DataDirectory is { } directory)
            { start.ArgumentList.Add("--data-dir"); start.ArgumentList.Add(directory); }
            using var child = Process.Start(start) ?? throw new InvalidOperationException("Restart could not start.");
            _setupRestarting = true; Close();
        }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException)
        { _log.Write("error", "Language restart failed", ex); ViewModel.Publish(L.T("Could not restart Nagi. Close it and open it again."), true); }
    }
}
