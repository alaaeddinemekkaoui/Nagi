using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Pulse.Core;
using Pulse.Windows;
using Windows.Storage;
using WinRT.Interop;

namespace Pulse.App;

public sealed partial class MainWindow
{
    private LocalMediaWindow? _localMedia;
    private bool _usingLocalMedia;
    private async Task OpenLocalMediaAsync()
    {
        if (_closed || _dialogOpen) return;
        _log.Write("media", "Opening local media picker");
        _dialogOpen = true;
        try
        {
            Activate();
            var selected = await LocalMediaFilePicker.PickAsync(WindowNative.GetWindowHandle(this), L.T("Open local music or video"));
            if (_closed || selected.Length == 0) return;
            var files = new List<StorageFile>();
            foreach (var path in selected) files.Add(await StorageFile.GetFileFromPathAsync(path));
            if (_closed) return;
            if (_localMedia is null)
            {
                var window = new LocalMediaWindow(_log); _localMedia = window;
                window.Closed += (_, _) => { if (ReferenceEquals(_localMedia, window)) _localMedia = null; };
            }
            _localMedia.Open(files);
        }
        catch (Exception ex)
        { _log.Write("error", "Could not open local media", ex); ViewModel.Publish(L.T("Local media could not be opened."), true); }
        finally { _dialogOpen = false; }
    }

    private Task<OperationResult> SendMediaAsync(string command) => _usingLocalMedia && _localMedia is { } local
        ? Task.FromResult(local.Send(command)) : _media.SendAsync(command);

    private Button OpenLocalMediaButton(string location)
    {
        var button = Button(L.T("Open local music or video"), async (_, _) => await OpenLocalMediaAsync());
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetAutomationId(button, "OpenLocalMedia" + location);
        return button;
    }
}
