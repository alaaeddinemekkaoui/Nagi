# Quick search

Press **Alt + Space**, click the search icon in Nagi, or select **Quick search** in the tray menu. Type an application name, filename, folder or Nagi action. Arrow keys select a result, Enter opens it, and Escape hides the launcher. Clicking outside also hides it. The launcher is separate from the edge dock and reuses one window.

| Input / shortcut | Result |
| --- | --- |
| `app: chrome` | Installed applications only |
| `file: report` | Indexed filenames only |
| `folder: projects` | Indexed folders only |
| `> volume` | Nagi actions only |
| `24 * 7` or `(8 + 4) / 3` | Calculator; Enter copies the result |
| `web your search` | Browser search, sent only when opened |
| Ctrl + Enter | Open the selected file's containing folder |
| Ctrl + C | Copy a selected path/result when no query text is selected |

Useful actions include Work/Gaming/Focus and Automatic profiles, output mute, volume up/down, Nagi visibility, settings, index rebuilding, and copying the local date/time. Files and folders also have visible Open, Show in folder and Copy path buttons. Query text remains local unless you deliberately open a web result. Search does not execute typed shell commands.

## Folders and index

Under **Settings → Quick search**, enable/disable file indexing, add a folder with the Windows folder picker, edit the folder list, restore standard folders, or rebuild the index. Defaults are the current user's Desktop, Documents and Downloads, including their Windows-known redirected locations. Turning off file indexing retains app search and commands. An empty custom list selects standard folders; saving an empty list in the editor disables file indexing.

Only filenames, paths and folder names are indexed; file contents are never read. The cache is `%LOCALAPPDATA%\Pulse\Search\file-index.json`. It scans at startup, after scope changes and on explicit rebuild. Debounced filesystem events incrementally update created, renamed and deleted entries. Every ten minutes the app catalog and watcher health are checked; unchanged file trees are not rescanned. Event overflow or watcher errors trigger a recovery scan. The previous snapshot remains searchable during ordinary rebuilds; removing a folder immediately removes it from results. A cached snapshot is accepted only for the same roots. Missing/stale items are revalidated at open time. The UI shows index counts, refresh status, the 40,000-item cap, and inaccessible-item counts.

Selected folders are visited before deeper subfolders, so a large tree in the first root does not prevent visiting the other selected roots. Indexing is bounded to 16 selected roots and 40,000 files/folders. System/reparse entries and common build folders (`.git`, `node_modules`, `bin`, `obj`, `AppData`) are skipped. This is filename search within selected folders, not a full-drive or file-content index. Files added between refreshes appear after the next rebuild.

## Applications and shortcuts

Apps come from Windows' Applications folder, including desktop and Store apps. Opening an app resolves it against the current catalog again and uses its native open verb. File/folder actions retain Nagi's local-path and action validation. Unsupported script/indirect shortcut files can be located or copied but cannot be opened as file actions.

Alt+Space is configurable with **Search shortcut**. If another app owns it, Nagi reports the conflict and keeps the prior registration when a changed shortcut fails. The Nagi search button, tray command and Settings entry remain available. Ctrl+Alt+Space and Ctrl+Alt+H retain their existing behavior.

The launcher uses the current foreground display's work area and DPI, the selected theme and Lua colors. Windows contrast/transparency and reduced-motion preferences remain authoritative. Calculator arithmetic is bounded and uses no script interpreter; it supports parentheses, unary signs, +, -, *, / and remainder `%` with a decimal point.

Windows catalog integration follows [Microsoft Shell.NameSpace](https://learn.microsoft.com/en-us/windows/win32/shell/shell-namespace), [Applications known folder](https://learn.microsoft.com/en-us/windows/win32/shell/knownfolderid), and [RegisterHotKey](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-registerhotkey).
