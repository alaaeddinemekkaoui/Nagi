using System.Runtime.InteropServices;
using Pulse.Core;

namespace Pulse.Windows;

public sealed record ApplicationCatalog(SearchEntry[] Entries, string? Error = null, string? Diagnostic = null);

public static class SearchApplications
{
    // The Windows Applications folder includes desktop and Microsoft Store applications.
    private const string AppsFolder = "shell:AppsFolder";
    public static Task<ApplicationCatalog> ReadAsync(CancellationToken token) => Sta(() =>
    {
        object? shell = null, folder = null, items = null;
        try
        {
            shell = Activator.CreateInstance(Type.GetTypeFromProgID("Shell.Application", true)!);
            folder = ((dynamic)shell!).NameSpace(AppsFolder);
            if (folder is null) return new ApplicationCatalog([], "Windows application catalog is unavailable.");
            items = ((dynamic)folder).Items();
            var entries = new List<SearchEntry>();
            var count = Math.Min((int)((dynamic)items).Count, 5000);
            for (var i = 0; i < count; i++)
            {
                token.ThrowIfCancellationRequested(); object? item = null;
                try
                {
                    item = ((dynamic)items).Item(i);
                    string name = ((dynamic)item!).Name; string target = ((dynamic)item).Path;
                    if (!string.IsNullOrWhiteSpace(name) && !string.IsNullOrWhiteSpace(target))
                        entries.Add(new("app:" + target, name, "Application · Windows app catalog", SearchKind.Application, target));
                }
                catch (COMException) { }
                finally { Release(item); }
            }
            return new ApplicationCatalog(entries.DistinctBy(e => e.Id, StringComparer.OrdinalIgnoreCase).ToArray());
        }
        catch (Exception ex) when (ex is COMException or Microsoft.CSharp.RuntimeBinder.RuntimeBinderException)
        { return new ApplicationCatalog([], "Windows application catalog could not be read.", ex.GetType().Name + ": " + ex.Message); }
        finally { Release(items); Release(folder); Release(shell); }
    }, token);

    public static Task<OperationResult> OpenAsync(SearchEntry entry, CancellationToken token) => Sta(() =>
    {
        if (entry.Kind != SearchKind.Application) return OperationResult.Fail("Choose an application.");
        object? shell = null, folder = null, items = null;
        try
        {
            shell = Activator.CreateInstance(Type.GetTypeFromProgID("Shell.Application", true)!);
            folder = ((dynamic)shell!).NameSpace(AppsFolder);
            if (folder is null) return OperationResult.Fail("Windows application catalog is unavailable.");
            items = ((dynamic)folder).Items();
            var count = Math.Min((int)((dynamic)items).Count, 5000);
            for (var i = 0; i < count; i++)
            {
                token.ThrowIfCancellationRequested(); object? item = null;
                try
                {
                    item = ((dynamic)items).Item(i);
                    string path = ((dynamic)item!).Path;
                    if (!string.Equals(path, entry.Target, StringComparison.OrdinalIgnoreCase)) continue;
                    // Resolve again through the live catalog; never execute a cached command line.
                    ((dynamic)item).InvokeVerb("open");
                    return OperationResult.Ok("Opened " + entry.Title);
                }
                finally { Release(item); }
            }
            return OperationResult.Fail("This application is no longer installed. Rebuild the search index.");
        }
        catch (Exception ex) when (ex is COMException or Microsoft.CSharp.RuntimeBinder.RuntimeBinderException)
        { return OperationResult.Fail("Windows could not open this application."); }
        finally { Release(items); Release(folder); Release(shell); }
    }, token);

    private static void Release(object? value) { if (value is not null && Marshal.IsComObject(value)) Marshal.ReleaseComObject(value); }
    private static Task<T> Sta<T>(Func<T> action, CancellationToken token)
    {
        var completion = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);
        var thread = new Thread(() =>
        {
            try { token.ThrowIfCancellationRequested(); completion.TrySetResult(action()); }
            catch (OperationCanceledException) { completion.TrySetCanceled(token); }
            catch (Exception ex) { completion.TrySetException(ex); }
        }) { IsBackground = true, Name = "Nagi application catalog" };
        thread.SetApartmentState(ApartmentState.STA); thread.Start(); return completion.Task;
    }

    public static string[] StandardFolders()
    {
        var folders = new List<string> { Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments) };
        var id = new Guid("374DE290-123F-4565-9164-39C4925E467B");
        if (SHGetKnownFolderPath(ref id, 0, 0, out var pointer) >= 0)
        { try { folders.Add(Marshal.PtrToStringUni(pointer) ?? ""); } finally { Marshal.FreeCoTaskMem(pointer); } }
        return folders.Where(p => !string.IsNullOrWhiteSpace(p) && !p.StartsWith("\\\\", StringComparison.Ordinal)).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
    }
    [DllImport("shell32.dll")] private static extern int SHGetKnownFolderPath(ref Guid id, uint flags, nint token, out nint path);
}
