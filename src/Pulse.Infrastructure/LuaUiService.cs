using System.Text;
using MoonSharp.Interpreter;

namespace Pulse.Infrastructure;

public sealed record LuaUiStyle(string Name = "Custom", string? Accent = null, string? Surface = null,
    double CardRadius = 18, double Spacing = 10, bool ShowMetrics = true, bool ShowNetwork = true,
    bool ShowMedia = true, bool ShowAudio = true, string CpuLabel = "Processor", string MemoryLabel = "Memory");

/// <summary>A small declarative Lua API. No CLR objects, IO, modules or Lua callbacks enter the host.</summary>
public sealed class LuaUiService(string directory)
{
    public const int MaximumBytes = 32 * 1024;
    public string FilePath { get; } = Path.Combine(directory, "scripts", "ui.lua");
    public static IReadOnlyDictionary<string, string> Presets { get; } = new Dictionary<string, string>
    {
        ["Rose glass"] = """
            -- Return appearance settings. nagi.theme is "Dark" or "Light";
            -- nagi.profile is "Work", "Gaming" or "Focus".
            local light = nagi.theme == "Light"
            return {
              name = "Rose glass",
              accent = light and "#AC244C" or "#FF8BA3",
              card_radius = 18,
              spacing = 10,
              labels = { cpu = "Processor", memory = "Memory" },
              sections = { metrics = true, network = true, media = true, audio = true }
            }
            """,
        ["Glacier"] = """
            local light = nagi.theme == "Light"
            return {
              name = "Glacier", accent = light and "#006C8C" or "#78DFFF",
              surface = light and "#A8F0FAFF" or "#50253848",
              card_radius = 22, spacing = 14,
              labels = { cpu = "CPU activity", memory = "Memory" }
            }
            """,
        ["Quiet focus"] = """
            return {
              name = "Quiet focus",
              accent = nagi.theme == "Light" and "#496B2A" or "#BDE89C",
              card_radius = 14, spacing = 16,
              sections = { network = false, media = nagi.profile ~= "Focus" }
            }
            """
    };

    public async Task<string> ReadAsync(CancellationToken cancellationToken = default)
    {
        if (!File.Exists(FilePath)) return Presets["Rose glass"];
        // Bounded even if an external editor grows the file while it is being read.
        await using var stream = new FileStream(FilePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        using var buffer = new MemoryStream();
        var bytes = new byte[4096];
        int count;
        while ((count = await stream.ReadAsync(bytes, cancellationToken)) > 0)
        {
            if (buffer.Length + count > MaximumBytes) throw new InvalidDataException("UI scripts must be 32 KB or smaller.");
            buffer.Write(bytes, 0, count);
        }
        return new UTF8Encoding(false, true).GetString(buffer.ToArray()).TrimStart('\uFEFF');
    }

    public async Task SaveAsync(string source, CancellationToken cancellationToken = default)
    {
        CheckSize(source);
        Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
        var temporary = FilePath + ".tmp";
        try
        {
            await File.WriteAllTextAsync(temporary, source, new UTF8Encoding(false), cancellationToken);
            if (File.Exists(FilePath)) File.Replace(temporary, FilePath, FilePath + ".bak", true);
            else File.Move(temporary, FilePath);
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }

    public static LuaUiStyle Evaluate(string source, string theme, string profile)
    {
        CheckSize(source);
        // Deliberately omit string/table/metatable/coroutine libraries: their callbacks can bypass
        // preemptive yielding. Arithmetic, functions, loops, tables and math remain available.
        var script = new Script(CoreModules.Math);
        var context = new Table(script);
        context.Set("theme", DynValue.NewString(theme));
        context.Set("profile", DynValue.NewString(profile));
        script.Globals.Set("nagi", DynValue.NewTable(context));
        // Preserve appearance scripts written before the Nagi rename.
        script.Globals.Set("pulse", DynValue.NewTable(context));
        try
        {
            var coroutine = script.CreateCoroutine(script.LoadString(source, codeFriendlyName: "ui.lua"));
            coroutine.Coroutine.AutoYieldCounter = 20_000;
            var result = coroutine.Coroutine.Resume();
            if (coroutine.Coroutine.State != CoroutineState.Dead)
                throw new InvalidDataException("UI script exceeded its 20,000 instruction budget. Simplify loops or calculations.");
            var root = RequireTable(result, "Script must return one appearance table.");
            CheckKeys(root, "name", "accent", "surface", "card_radius", "spacing", "labels", "sections");
            var labels = OptionalTable(root, "labels");
            var sections = OptionalTable(root, "sections");
            if (labels is not null) CheckKeys(labels, "cpu", "memory");
            if (sections is not null) CheckKeys(sections, "metrics", "network", "media", "audio");
            return new(Text(root, "name", "Custom", 40), Color(root, "accent", false), Color(root, "surface", true),
                Number(root, "card_radius", 18, 8, 28), Number(root, "spacing", 10, 6, 20),
                Flag(sections, "metrics"), Flag(sections, "network"), Flag(sections, "media"), Flag(sections, "audio"),
                Text(labels, "cpu", "Processor", 24), Text(labels, "memory", "Memory", 24));
        }
        catch (InterpreterException ex) { throw new InvalidDataException(ex.DecoratedMessage ?? ex.Message, ex); }
    }

    private static void CheckSize(string source)
    {
        if (source.Length > MaximumBytes || Encoding.UTF8.GetByteCount(source) > MaximumBytes)
            throw new InvalidDataException("UI scripts must be 32 KB or smaller.");
    }
    private static Table RequireTable(DynValue value, string error) => value.Type == DataType.Table ? value.Table : throw new InvalidDataException(error);
    private static Table? OptionalTable(Table table, string key) => table.Get(key).IsNil() ? null : RequireTable(table.Get(key), key + " must be a table.");
    private static void CheckKeys(Table table, params string[] keys)
    {
        foreach (var pair in table.Pairs)
            if (pair.Key.Type != DataType.String || !keys.Contains(pair.Key.String, StringComparer.Ordinal))
                throw new InvalidDataException("Unknown UI option: " + pair.Key.ToPrintString());
    }
    private static string Text(Table? table, string key, string fallback, int maximum)
    {
        var value = table?.Get(key);
        if (value is null || value.IsNil()) return fallback;
        if (value.Type != DataType.String || string.IsNullOrWhiteSpace(value.String) || value.String.Length > maximum || value.String.Any(char.IsControl))
            throw new InvalidDataException($"{key} must be text of 1–{maximum} characters without control characters.");
        return value.String;
    }
    private static string? Color(Table table, string key, bool alpha)
    {
        if (table.Get(key).IsNil()) return null;
        var value = Text(table, key, "", 9);
        if (value[0] != '#' || !(value.Length == 7 || alpha && value.Length == 9) || !value.AsSpan(1).ToString().All(Uri.IsHexDigit))
            throw new InvalidDataException(key + (alpha ? " must be #RRGGBB or #AARRGGBB." : " must be #RRGGBB."));
        return value;
    }
    private static double Number(Table table, string key, double fallback, double minimum, double maximum)
    {
        var value = table.Get(key);
        if (value.IsNil()) return fallback;
        if (value.Type != DataType.Number || !double.IsFinite(value.Number) || value.Number < minimum || value.Number > maximum)
            throw new InvalidDataException($"{key} must be a number from {minimum} to {maximum}.");
        return value.Number;
    }
    private static bool Flag(Table? table, string key)
    {
        var value = table?.Get(key);
        if (value is null || value.IsNil()) return true;
        if (value.Type != DataType.Boolean) throw new InvalidDataException(key + " must be true or false.");
        return value.Boolean;
    }
}
