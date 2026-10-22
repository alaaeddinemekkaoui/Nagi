using System.Globalization;
using System.Text;
using System.Text.Json;

namespace Pulse.Infrastructure;

public sealed record AppLanguage(string Code, string Name, string NativeName, bool RightToLeft,
    IReadOnlyDictionary<string, string> Strings)
{
    public override string ToString() => NativeName + " · " + Name;
}

/// <summary>Data-only language packs. English phrases are stable, readable translation keys.</summary>
public sealed class LanguageCatalog
{
    private readonly Dictionary<string, AppLanguage> _languages = new(StringComparer.OrdinalIgnoreCase);
    private readonly AppLanguage _embeddedEnglish;
    public IReadOnlyCollection<AppLanguage> Languages => _languages.Values.OrderBy(l => l.NativeName, StringComparer.Ordinal).ToArray();
    public AppLanguage Selected { get; private set; }
    public CultureInfo Culture => CultureInfo.GetCultureInfo(Selected.Code);

    public LanguageCatalog(string directory, Action<string, Exception>? invalidPack = null)
    {
        using var english = typeof(LanguageCatalog).Assembly.GetManifestResourceStream("Pulse.Languages.en.json")!;
        var fallback = _embeddedEnglish = Read(english);
        _languages.Add("en", fallback); Selected = fallback;
        if (!Directory.Exists(directory)) return;
        foreach (var path in Directory.EnumerateFiles(directory, "*.json").Order(StringComparer.Ordinal))
        {
            try
            {
                if (new FileInfo(path).Length > 1_048_576) throw new InvalidDataException("Language pack exceeds 1 MB.");
                using var input = File.OpenRead(path);
                var pack = Read(input);
                if (!Path.GetFileNameWithoutExtension(path).Equals(pack.Code, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidDataException("The filename must match the language code.");
                if (pack.Code.Equals("en", StringComparison.OrdinalIgnoreCase)) { _languages["en"] = pack; continue; }
                if (!_languages.TryAdd(pack.Code, pack)) throw new InvalidDataException("Duplicate language code.");
            }
            catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException or JsonException or ArgumentException or FormatException or KeyNotFoundException or InvalidOperationException or OverflowException)
            { invalidPack?.Invoke(path, ex); }
        }
        Selected = _languages["en"];
    }

    public void Select(string? requested, string? systemLanguage = null)
    {
        var code = string.IsNullOrWhiteSpace(requested) || requested.Equals("System", StringComparison.OrdinalIgnoreCase)
            ? systemLanguage ?? CultureInfo.InstalledUICulture.Name : requested;
        while (!string.IsNullOrEmpty(code))
        {
            if (_languages.TryGetValue(code, out var language)) { Selected = language; return; }
            // Culture parents include script variants: zh-CN → zh-Hans, zh-TW → zh-Hant.
            string parent;
            try { parent = CultureInfo.GetCultureInfo(code).Parent.Name; }
            catch (CultureNotFoundException) { parent = ""; }
            var separator = code.LastIndexOf('-');
            code = parent.Length > 0 && !parent.Equals(code, StringComparison.OrdinalIgnoreCase)
                ? parent : separator < 0 ? "" : code[..separator];
        }
        Selected = _languages["en"];
    }

    public string Text(string key)
    {
        if (Selected.Strings.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value)) return value;
        if (_languages["en"].Strings.TryGetValue(key, out value) && !string.IsNullOrWhiteSpace(value)) return value;
        return _embeddedEnglish.Strings.GetValueOrDefault(key, key);
    }

    public string Format(string key, params object?[] arguments)
    {
        try { return string.Format(Culture, Text(key), arguments); }
        catch (FormatException) { return string.Format(CultureInfo.InvariantCulture, key, arguments); }
    }

    public static AppLanguage Read(Stream input)
    {
        using var json = JsonDocument.Parse(input);
        var root = json.RootElement;
        if (root.GetProperty("schemaVersion").GetInt32() != 1) throw new InvalidDataException("Unsupported language schema.");
        var code = root.GetProperty("code").GetString() ?? "";
        if (code.Length is < 2 or > 35 || code.Any(c => !char.IsAsciiLetterOrDigit(c) && c != '-'))
            throw new InvalidDataException("Use a BCP-47 language code, such as fr or pt-BR.");
        var culture = CultureInfo.GetCultureInfo(code);
        var name = root.GetProperty("name").GetString(); var native = root.GetProperty("nativeName").GetString();
        if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(native) || name.Length > 80 || native.Length > 80)
            throw new InvalidDataException("Language names must contain 1–80 characters.");
        var strings = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var item in root.GetProperty("strings").EnumerateObject())
        {
            if (item.Value.ValueKind != JsonValueKind.String) throw new InvalidDataException("Translations must be strings.");
            var value = item.Value.GetString()!;
            if (!string.IsNullOrWhiteSpace(value) && !SamePlaceholders(item.Name, value)) throw new InvalidDataException("Translation placeholders differ: " + item.Name);
            if (!strings.TryAdd(item.Name, value)) throw new InvalidDataException("Duplicate translation key: " + item.Name);
        }
        return new(code, name, native, culture.TextInfo.IsRightToLeft, strings);
    }

    public static bool SamePlaceholders(string source, string translation)
    {
        if (source == translation) return true;
        // Literal braces in Lua examples are text, not composite-format arguments.
        static bool HasArgument(string text) => System.Text.RegularExpressions.Regex.IsMatch(text, @"(?<!\{)\{\d+");
        if (!HasArgument(source) && !HasArgument(translation)) return true;
        try { return Arguments(source).SetEquals(Arguments(translation)); }
        catch (FormatException) { return false; }
    }

    private static HashSet<int> Arguments(string format)
    {
        _ = CompositeFormat.Parse(format);
        var result = new HashSet<int>();
        for (var i = 0; i < format.Length; i++)
        {
            if (format[i] != '{') continue;
            if (i + 1 < format.Length && format[i + 1] == '{') { i++; continue; }
            var start = ++i;
            while (i < format.Length && char.IsAsciiDigit(format[i])) i++;
            result.Add(int.Parse(format.AsSpan(start, i - start), CultureInfo.InvariantCulture));
            while (i < format.Length && format[i] != '}') i++;
        }
        return result;
    }
}
