using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Pulse.Core;

namespace Pulse.App;

public sealed partial class MainWindow
{
    private void BuildMusicLinks()
    {
        foreach (var panel in new[] { MusicLinksPanel, PeekMusicLinksPanel })
        {
            panel.Children.Clear();
            foreach (var player in MusicPlayers.For(ViewModel.Settings.PreferredMusicPlayer))
            {
                var link = new HyperlinkButton { Content = "Open " + player.Name, NavigateUri = new(player.Url), FontSize = 12, Padding = new(0, 3, 0, 3) };
                AutomationProperties.SetName(link, "Open " + player.Name); panel.Children.Add(link);
            }
        }
        RenderMusicLinks();
    }
    private void RenderMusicLinks()
    {
        var available = ViewModel.Media.Available;
        MusicLinksPanel.Visibility = PeekMusicLinksPanel.Visibility = available ? Visibility.Collapsed : Visibility.Visible;
        MediaTransport.Visibility = PeekTransport.Visibility = available ? Visibility.Visible : Visibility.Collapsed;
    }
    private void BuildMusicSettings()
    {
        var content = Stack(); content.Children.Add(Heading("Now playing"));
        content.Children.Add(Note("When nothing is playing, open your music service here. Choose All to show all three links, or select one preferred service. Playback controls follow the active Windows media session."));
        var player = Select(MusicPlayers.Names, ViewModel.Settings.PreferredMusicPlayer, "Preferred music player");
        player.SelectionChanged += async (_, _) =>
        {
            if (player.SelectedItem is string name) await CommitAsync(s => s with { PreferredMusicPlayer = name });
        };
        content.Children.Add(Labeled("Music service", player));
        SettingsPage.Children.Add(Card(content));
    }
}
