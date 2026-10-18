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
            panel.Children.Add(OpenLocalMediaButton(ReferenceEquals(panel, MusicLinksPanel) ? "Overview" : "Preview"));
            foreach (var player in MusicPlayers.For(ViewModel.Settings.PreferredMusicPlayer))
            {
                var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
                var data = player.Name switch
                {
                    "Spotify" => "M12,2 A10,10 0 1 1 11.999,2 M6,9 C10,7 14,7 18,9 M7,12 C10,10.5 14,10.5 17,12 M8,15 C11,14 13,14 16,15",
                    "YouTube Music" => "M12,2 A10,10 0 1 1 11.999,2 M9,7 L17,12 L9,17 Z",
                    _ => "M9,17 L9,5 L19,3 L19,15 M9,8 L19,6 M9,17 C9,20 3,22 3,18 C3,15 9,14 9,17 M19,15 C19,18 13,20 13,16 C13,13 19,12 19,15"
                };
                var icon = (Microsoft.UI.Xaml.Shapes.Path)Microsoft.UI.Xaml.Markup.XamlReader.Load("<Path xmlns=\"http://schemas.microsoft.com/winfx/2006/xaml/presentation\" Data=\"" + data + "\" Width=\"18\" Height=\"18\" Stretch=\"Uniform\" StrokeThickness=\"1.5\" StrokeStartLineCap=\"Round\" StrokeEndLineCap=\"Round\"/>");
                icon.Stroke = ThemeBrush("InkBrush"); row.Children.Add(icon);
                row.Children.Add(new TextBlock { Text = L.T("Open ") + player.Name, FontSize = 12, VerticalAlignment = VerticalAlignment.Center });
                var link = new HyperlinkButton { Content = row, NavigateUri = new(player.Url), FontSize = 12, Padding = new(0, 3, 0, 3) };
                AutomationProperties.SetName(link, L.T("Open ") + player.Name); panel.Children.Add(link);
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
        var content = Stack(); content.Children.Add(Heading(L.T("Now playing")));
        content.Children.Add(Note(L.T("When nothing is playing, open your music service here. Choose All to show all three links, or select one preferred service. Playback controls follow the active Windows media session.")));
        var player = Select(MusicPlayers.Names, ViewModel.Settings.PreferredMusicPlayer, "Preferred music player");
        player.SelectionChanged += async (_, _) =>
        {
            if (player.SelectedItem is string name) await CommitAsync(s => s with { PreferredMusicPlayer = name });
        };
        content.Children.Add(Labeled(L.T("Music service"), player));
        content.Children.Add(OpenLocalMediaButton("Settings"));
        content.Children.Add(Note(L.T("Play local audio or video in Nagi. Queue multiple files; dock controls follow playback. Supported formats depend on the media codecs installed in Windows.")));
        SettingsPage.Children.Add(Card(content));
    }
}
