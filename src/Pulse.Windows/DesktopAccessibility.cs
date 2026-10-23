using System.Runtime.InteropServices;

[assembly: System.Runtime.CompilerServices.InternalsVisibleTo("Pulse.Windows.Tests")]

namespace Pulse.Windows;

// Minimal native UI Automation interfaces from the Windows SDK; avoids a WPF runtime dependency.
internal static class DesktopAccessibility
{
    internal static AccessibleDesktopNode[] Read(nint window)
    {
        var automation = (IAutomation)Activator.CreateInstance(Type.GetTypeFromCLSID(new("ff48dba4-60ef-4201-aa87-54103eef594e"), true)!)!;
        IElement? root = null; IWalker? walker = null;
        try {
            root = automation.ElementFromHandle(window); walker = automation.ControlViewWalker();
            var result = new List<AccessibleDesktopNode>();
            void Visit(IElement node, int parent, int depth) {
                if (depth > 12 || result.Count >= 192) return;
                var index = result.Count;
                string Text(int id) { var text = node.Property(id) as string ?? ""; return text.Length > 2048 ? text[..2048] : text; }
                double? Number(int id) => node.Property(id) is double value && double.IsFinite(value) ? value : null;
                var password = node.Property(30019) is true;
                result.Add(new(password ? "" : Text(30005), Text(30011), Text(30012), node.Property(30003) is int type ? type : 0,
                    node.Property(30022) is true, Number(30047), Number(30049), Number(30050), parent));
                if (password) return;
                var child = walker.First(node);
                while (child is not null && result.Count < 192) {
                    IElement? next = null;
                    try { Visit(child, index, depth + 1); next = walker.Next(child); }
                    finally { Marshal.ReleaseComObject(child); }
                    child = next;
                }
                if (child is not null) Marshal.ReleaseComObject(child);
            }
            Visit(root, -1, 0); return result.ToArray();
        }
        finally { if (walker is not null) Marshal.ReleaseComObject(walker); if (root is not null) Marshal.ReleaseComObject(root); Marshal.ReleaseComObject(automation); }
    }
    [ComImport, Guid("30cbe57d-d9d0-452a-ab13-7ac5ac4825ee"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IAutomation
    {
        void CompareElements(); void CompareRuntimeIds(); void GetRootElement();
        IElement ElementFromHandle(nint window);
        void ElementFromPoint(); void GetFocusedElement(); void GetRootElementBuildCache(); void ElementFromHandleBuildCache();
        void ElementFromPointBuildCache(); void GetFocusedElementBuildCache(); void CreateTreeWalker();
        IWalker ControlViewWalker();
    }
    [ComImport, Guid("d22108aa-8ac5-49a5-837b-37bbb3d7591e"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IElement
    {
        void SetFocus(); void GetRuntimeId(); void FindFirst(); void FindAll(); void FindFirstBuildCache(); void FindAllBuildCache(); void BuildUpdatedCache();
        [return: MarshalAs(UnmanagedType.Struct)] object? Property(int id);
    }
    [ComImport, Guid("4042c624-389c-4afc-a630-9df854a541fc"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IWalker
    {
        void Parent(); IElement? First(IElement node); void Last(); IElement? Next(IElement node);
    }
}
