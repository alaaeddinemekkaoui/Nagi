using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using Pulse.Core;

namespace Pulse.Infrastructure;

public sealed class SettingsService : ISettingsService, IDisposable
{
    public static JsonSerializerOptions JsonOptions { get; } = new()
    {
        WriteIndented = true, PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() }
    };
    private readonly SemaphoreSlim _gate = new(1, 1);
    private JsonObject _original = new();
    private bool _futureSchema;
    private bool _protectedOriginal;
    public string FilePath { get; }
    public string? RecoveryMessage { get; private set; }
    public SettingsService(string? directory = null)
    {
        directory ??= Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Pulse");
        FilePath = Path.Combine(directory, "settings.json");
    }

    public async Task<PulseSettings> LoadAsync(CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            if (!File.Exists(FilePath)) return new();
            try
            {
                var text = await File.ReadAllTextAsync(FilePath, cancellationToken);
                _original = JsonNode.Parse(text) as JsonObject ?? throw new JsonException("Expected a JSON object.");
                var safe = JsonSerializer.SerializeToNode(new PulseSettings(), JsonOptions)!.AsObject();
                var invalid = new List<string>();
                var nullability = new System.Reflection.NullabilityInfoContext();
                // Deserialize each known field separately so a broken field cannot erase valid siblings.
                foreach (var property in typeof(PulseSettings).GetProperties().Where(p => p.Name != nameof(PulseSettings.Extra)))
                {
                    var key = _original.Select(p => p.Key).FirstOrDefault(k => k.Equals(property.Name, StringComparison.OrdinalIgnoreCase));
                    if (key is null) continue;
                    try
                    {
                        var node = _original[key];
                        if (node is null && Nullable.GetUnderlyingType(property.PropertyType) is null
                            && nullability.Create(property).ReadState != System.Reflection.NullabilityState.Nullable) throw new JsonException();
                        var value = node?.Deserialize(property.PropertyType, JsonOptions);
                        safe[property.Name] = JsonSerializer.SerializeToNode(value, property.PropertyType, JsonOptions);
                    }
                    catch (Exception ex) when (ex is JsonException or ArgumentException or NotSupportedException) { invalid.Add(property.Name); }
                }
                foreach (var pair in _original)
                    if (!safe.Any(p => p.Key.Equals(pair.Key, StringComparison.OrdinalIgnoreCase))) safe[pair.Key] = pair.Value?.DeepClone();
                var settings = safe.Deserialize<PulseSettings>(JsonOptions)!.Normalize();
                _futureSchema = settings.SchemaVersion > 1;
                if (_futureSchema) RecoveryMessage = "Settings were created by a newer Pulse. Changes are read-only to protect them.";
                else if (invalid.Count > 0)
                {
                    _protectedOriginal = !Backup("recovered");
                    RecoveryMessage = "Recovered invalid settings: " + string.Join(", ", invalid)
                        + (_protectedOriginal ? ". Changes are read-only because a backup could not be created." : ". Original backed up.");
                }
                return settings with { SchemaVersion = Math.Max(1, settings.SchemaVersion) };
            }
            catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException)
            {
                _protectedOriginal = !Backup("corrupt");
                RecoveryMessage = "Settings could not be read. Safe defaults loaded; the original was preserved."
                    + (_protectedOriginal ? " Changes are read-only until access is restored and Pulse restarts." : "");
                _original = new();
                return new();
            }
        }
        finally { _gate.Release(); }
    }

    private bool Backup(string suffix)
    {
        try
        {
            if (File.Exists(FilePath)) File.Copy(FilePath, FilePath + "." + suffix + "." + DateTime.UtcNow.ToString("yyyyMMddHHmmssfff"), false);
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { return false; }
    }

    public async Task SaveAsync(PulseSettings settings, CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken);
        var temporary = FilePath + ".tmp";
        try
        {
            if (_futureSchema) throw new InvalidOperationException("A newer settings schema is read-only.");
            if (_protectedOriginal) throw new InvalidOperationException("Original settings could not be backed up. Restore file access and restart Pulse before making changes.");
            Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
            var merged = _original.DeepClone().AsObject();
            foreach (var pair in JsonSerializer.SerializeToNode(settings.Normalize(), JsonOptions)!.AsObject())
            {
                foreach (var oldKey in merged.Select(p => p.Key).Where(k => k.Equals(pair.Key, StringComparison.OrdinalIgnoreCase)).ToArray()) merged.Remove(oldKey);
                merged[pair.Key] = pair.Value?.DeepClone();
            }
            await File.WriteAllTextAsync(temporary, merged.ToJsonString(JsonOptions), cancellationToken);
            if (File.Exists(FilePath)) File.Replace(temporary, FilePath, FilePath + ".bak", true);
            else File.Move(temporary, FilePath);
            _original = merged;
        }
        finally
        {
            if (File.Exists(temporary)) File.Delete(temporary);
            _gate.Release();
        }
    }
    public void Dispose() => _gate.Dispose();
}
