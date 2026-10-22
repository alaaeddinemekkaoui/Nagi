# Languages and translation contributions

Nagi v0.2 includes **40 language packs**, including Japanese, Simplified Chinese, Traditional Chinese, Korean, English, French, Spanish, German, Russian and Arabic. All interface language files are UTF-8 JSON in [`languages/`](../languages). The app reads this folder when it starts; adding a language does not require changing or recompiling application code.

New settings-card and integration phrases are registered in every pack. The new category names are translated across all 40 packs; primary new controls also have French, Spanish, German, Russian, Arabic, Japanese and Chinese translations. Some new explanatory text still uses English values pending contributor review. Translate these values in the JSON file to improve coverage.

Welcome starts with an animated, hand-drawn **Nagi / ナギ** greeting. Continue to the language page before entering your name. **System default** selects your Windows display language, with a regional fallback: French (France) and French (Canada) both use `fr.json`. The page shows the current language by its native name. Selecting English immediately changes the welcome controls to English. Unsupported system languages use English.

After setup, use **Settings → Language**, then **Restart Nagi**. Your profiles, theme, shortcuts and other preferences are preserved. Arabic, Hebrew, Persian and Urdu use right-to-left layout; the dock stays attached to your chosen physical screen edge. Chinese system locales use culture/script parents: `zh-CN` resolves to `zh-Hans`, while `zh-TW` and `zh-HK` resolve to `zh-Hant`. Reduced motion and the Windows animation preference disable the handwriting animation.

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

The tests load all shipped files, validate placeholders, exercise regional/script and English fallback, check right-to-left selection and verify saved settings. All 40 files cover the current English source keys. The non-English packs began with machine translations; native-speaker corrections and wording improvements are welcome. Technical identifiers and the escaped diagnostic version/uptime format remain literal. Future or missing phrases fall back to English. Translations ship with the app and work offline. Nagi never sends user text to a translation service.

Maintainers can generate initial drafts with `node scripts/Update-NagiTranslations.mjs ja zh-Hans`, using Node.js 22 or newer with network access. This explicit maintenance command sends only the public English UI phrases to Google Translate, preserves existing entries, protects format tokens and checkpoints in ignored `artifacts/translation-cache`. It is never invoked by the app or release build. Review output and run the language tests before committing it.

When adding a new interface phrase in code, wrap it in `L.T("English phrase")`, or use `L.F($"Current language: {name}")` for formatted text, and add its English entry to `en.json`. Translators only need to edit JSON files. Do not translate saved identifiers such as `Light`, `Work`, app paths or user-entered names; only their displayed labels are translated.

## Included languages

| Language | Native name | File |
| --- | --- | --- |
| Arabic | العربية | [ar.json](../languages/ar.json) |
| Bengali | বাংলা | [bn.json](../languages/bn.json) |
| Bulgarian | Български | [bg.json](../languages/bg.json) |
| Catalan | Català | [ca.json](../languages/ca.json) |
| Chinese (Simplified) | 简体中文 | [zh-Hans.json](../languages/zh-Hans.json) |
| Chinese (Traditional) | 繁體中文 | [zh-Hant.json](../languages/zh-Hant.json) |
| Czech | Čeština | [cs.json](../languages/cs.json) |
| Danish | Dansk | [da.json](../languages/da.json) |
| Dutch | Nederlands | [nl.json](../languages/nl.json) |
| English | English | [en.json](../languages/en.json) |
| Filipino | Filipino | [fil.json](../languages/fil.json) |
| Finnish | Suomi | [fi.json](../languages/fi.json) |
| French | Français | [fr.json](../languages/fr.json) |
| German | Deutsch | [de.json](../languages/de.json) |
| Greek | Ελληνικά | [el.json](../languages/el.json) |
| Hebrew | עברית | [he.json](../languages/he.json) |
| Hindi | हिन्दी | [hi.json](../languages/hi.json) |
| Hungarian | Magyar | [hu.json](../languages/hu.json) |
| Indonesian | Bahasa Indonesia | [id.json](../languages/id.json) |
| Italian | Italiano | [it.json](../languages/it.json) |
| Japanese | 日本語 | [ja.json](../languages/ja.json) |
| Korean | 한국어 | [ko.json](../languages/ko.json) |
| Malay | Bahasa Melayu | [ms.json](../languages/ms.json) |
| Norwegian Bokmål | Norsk bokmål | [nb.json](../languages/nb.json) |
| Persian | فارسی | [fa.json](../languages/fa.json) |
| Polish | Polski | [pl.json](../languages/pl.json) |
| Portuguese | Português | [pt.json](../languages/pt.json) |
| Romanian | Română | [ro.json](../languages/ro.json) |
| Russian | Русский | [ru.json](../languages/ru.json) |
| Slovak | Slovenčina | [sk.json](../languages/sk.json) |
| Spanish | Español | [es.json](../languages/es.json) |
| Swahili | Kiswahili | [sw.json](../languages/sw.json) |
| Swedish | Svenska | [sv.json](../languages/sv.json) |
| Tamil | தமிழ் | [ta.json](../languages/ta.json) |
| Telugu | తెలుగు | [te.json](../languages/te.json) |
| Thai | ไทย | [th.json](../languages/th.json) |
| Turkish | Türkçe | [tr.json](../languages/tr.json) |
| Ukrainian | Українська | [uk.json](../languages/uk.json) |
| Urdu | اردو | [ur.json](../languages/ur.json) |
| Vietnamese | Tiếng Việt | [vi.json](../languages/vi.json) |
