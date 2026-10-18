# Languages and translation contributions

Nagi includes English, French, Spanish, German, Russian and Arabic. All interface language files are UTF-8 JSON in [`languages/`](../languages). The app reads this folder when it starts; adding a language does not require changing or recompiling application code.

Welcome starts with an animated, hand-drawn **Nagi / ナギ** greeting. Continue to the language page before entering your name. **System default** selects your Windows display language, with a regional fallback: French (France) and French (Canada) both use `fr.json`. The page shows the current language by its native name. Selecting English immediately changes the welcome controls to English. Unsupported system languages use English.

After setup, use **Settings → Language**, then **Restart Nagi**. Your profiles, theme, shortcuts and other preferences are preserved. Arabic content uses right-to-left layout; the dock stays attached to your chosen physical screen edge. Reduced motion and the Windows animation preference disable the handwriting animation.

## Add your language

1. Copy `languages/en.json` to a new file, for example `languages/it.json`.
2. Set `code` to the language's BCP-47 code (`it`, `pt-BR`, `zh-Hant`, etc.), `name` to its English name and `nativeName` to its name in that language.
3. Translate the **values** in `strings`. Keep every key unchanged, including keys starting with `ui.`. English phrases are readable keys for code-built controls; `ui.` keys identify XAML labels, and their English text is in `en.json`.
4. Keep placeholders such as `{0}`, `{1}`, `{0:0}%` and `{0:HH:mm}`. You may reorder them, but do not remove or add arguments. Keep technical tokens such as `app:`, `file:`, `folder:`, `Ctrl`, `.exe` and Lua option names unchanged. Do not translate Nagi or other product names.
5. Save as UTF-8, restart Nagi, select the new language and check it. For a portable installation, put the file in the `languages` folder **next to the extracted `Nagi.exe`**, rather than next to the self-extracting launcher. The standalone EXE stores its extracted app under `%LOCALAPPDATA%\Nagi\Portable\`.
6. Submit the JSON file in a pull request. The build includes all `languages/*.json` files automatically. No language registry or code changes are needed.

A small, working Italian contribution can start like this:

```json
{
  "schemaVersion": 1,
  "code": "it",
  "name": "Italian",
  "nativeName": "Italiano",
  "strings": {
    "Settings": "Impostazioni",
    "Continue": "Continua",
    "Current language: {0}": "Lingua attuale: {0}"
  }
}
```

The filename and `code` must match. Partial translations work: missing or empty values fall back to English. Nagi also falls back through regional language codes before using English. Invalid files, duplicate keys and mismatched placeholders are ignored and recorded in the error log; they do not stop startup. Files must be under 1 MB and use schema version 1. English is also embedded so it remains available if the external folder is missing. You may edit `en.json` too; embedded English fills any gaps.

## Check a contribution

From a source checkout with the .NET SDK installed:

```powershell
dotnet test tests/Pulse.Tests/Pulse.Tests.csproj --filter FullyQualifiedName~LanguageTests
```

The tests load the actual shipped files, validate placeholders, exercise regional and English fallback, check right-to-left selection and verify saved settings. The six supplied files cover the same source entries. The non-English packs began with machine translations; native-speaker corrections and wording improvements are welcome. Translations ship with the app and work offline. Nagi never sends user text to a translation service.

When adding a new interface phrase in code, wrap it in `L.T("English phrase")`, or use `L.F($"Current language: {name}")` for formatted text, and add its English entry to `en.json`. Translators only need to edit JSON files. Do not translate saved identifiers such as `Light`, `Work`, app paths or user-entered names; only their displayed labels are translated.
