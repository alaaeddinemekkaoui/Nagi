using System.ComponentModel;
using System.Runtime.InteropServices;

namespace Pulse.Windows;

public static class LocalMediaFilePicker
{
    // A native desktop dialog avoids dependence on the packaged Windows picker broker.
    public static Task<string[]> PickAsync(nint owner, string title)
    {
        var completion = new TaskCompletionSource<string[]>(TaskCreationOptions.RunContinuationsAsynchronously);
        var thread = new Thread(() =>
        {
            const int capacity = 65_536;
            var buffer = Marshal.AllocHGlobal(capacity * sizeof(char));
            try
            {
                Marshal.Copy(new byte[capacity * sizeof(char)], 0, buffer, capacity * sizeof(char));
                var dialog = new OpenFileName
                {
                    Size = Marshal.SizeOf<OpenFileName>(), Owner = owner,
                    Filter = "Media files\0*.mp3;*.wav;*.flac;*.m4a;*.aac;*.ogg;*.wma;*.mp4;*.mkv;*.avi;*.mov;*.webm;*.wmv\0All files\0*.*\0\0",
                    FilterIndex = 1, File = buffer, MaxFile = capacity, Title = title,
                    Flags = 0x00080000 | 0x00000200 | 0x00001000 | 0x00000800 | 0x00000008 | 0x00000004 | 0x02000000
                };
                if (!GetOpenFileNameW(ref dialog))
                {
                    var error = CommDlgExtendedError();
                    if (error != 0) throw new Win32Exception((int)error, "The local media file dialog failed.");
                    completion.TrySetResult([]); return;
                }
                var selected = new List<string>(); var offset = 0;
                while (offset < capacity)
                {
                    var value = Marshal.PtrToStringUni(buffer + offset * sizeof(char)) ?? "";
                    if (value.Length == 0) break;
                    selected.Add(value); offset += value.Length + 1;
                }
                completion.TrySetResult(selected.Count <= 1 ? selected.ToArray()
                    : selected.Skip(1).Select(file => Path.Combine(selected[0], file)).ToArray());
            }
            catch (Exception ex) { completion.TrySetException(ex); }
            finally { Marshal.FreeHGlobal(buffer); }
        }) { IsBackground = true, Name = "Nagi local media picker" };
        thread.SetApartmentState(ApartmentState.STA); thread.Start();
        return completion.Task;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct OpenFileName
    {
        public int Size; public nint Owner, Instance;
        [MarshalAs(UnmanagedType.LPWStr)] public string? Filter;
        public nint CustomFilter; public int MaxCustomFilter, FilterIndex;
        public nint File; public int MaxFile; public nint FileTitle; public int MaxFileTitle;
        [MarshalAs(UnmanagedType.LPWStr)] public string? InitialDirectory;
        [MarshalAs(UnmanagedType.LPWStr)] public string? Title;
        public uint Flags; public ushort FileOffset, FileExtension;
        public nint DefaultExtension, CustomData, Hook, TemplateName, Reserved;
        public int ReservedWord; public uint FlagsEx;
    }
    [DllImport("comdlg32.dll", CharSet = CharSet.Unicode, ExactSpelling = true)]
    [return: MarshalAs(UnmanagedType.Bool)] private static extern bool GetOpenFileNameW(ref OpenFileName dialog);
    [DllImport("comdlg32.dll")] private static extern uint CommDlgExtendedError();
}
