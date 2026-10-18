using System.IO.Compression;
using System.Net;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Pulse.Infrastructure;

public sealed record AppRelease(Version Version, Uri Download, long Size, string? Sha256, Uri? Checksum,
    string Notes = "", Uri? ReleasePage = null);
public sealed record PreparedUpdate(AppRelease Release, string Directory, string Payload);

public sealed class AppUpdateService(HttpClient client)
{
    public const string Repository = "alaaeddinemekkaoui/Nagi";
    public static string ExtractChangelog(string? body)
    {
        if (string.IsNullOrWhiteSpace(body)) return "";
        var notes = body.Replace("\r\n", "\n").Trim();
        // Older releases appended the full README after a horizontal rule.
        var readme = Regex.Match(notes, @"(?m)^\s*---+\s*$");
        if (readme.Success) notes = notes[..readme.Index].Trim();
        // If a whole changelog was pasted, show only the newest release section.
        var releases = Regex.Matches(notes, @"(?m)^#{1,3}\s+(?:Nagi\s+)?v?\d+\.\d+(?:\.\d+)?\b[^\n]*", RegexOptions.IgnoreCase);
        if (releases.Count > 1) notes = notes[..releases[1].Index].Trim();
        return notes.Length > 24000 ? notes[..24000] : notes;
    }
    public static Version? ParseVersion(string tag)
    {
        var match = Regex.Match(tag, @"^v?(\d+)(?:\.(\d+))?(?:\.(\d+))?(?:\.(\d+))?$", RegexOptions.IgnoreCase);
        if (!match.Success) return null;
        var numbers = new int[4];
        for (var i = 0; i < numbers.Length; i++)
            if (match.Groups[i + 1].Success && !int.TryParse(match.Groups[i + 1].Value, out numbers[i])) return null;
        return new(numbers[0], numbers[1], numbers[2], numbers[3]);
    }
    public static bool IsReleaseDownload(Uri uri) => uri.Scheme == "https" && uri.Host == "github.com"
        && uri.AbsolutePath.StartsWith("/" + Repository + "/releases/download/", StringComparison.Ordinal) && uri.UserInfo.Length == 0;

    public async Task<AppRelease?> CheckAsync(Version current, CancellationToken token)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "https://api.github.com/repos/" + Repository + "/releases/latest");
        request.Headers.UserAgent.ParseAdd("Nagi/" + current);
        request.Headers.Accept.ParseAdd("application/vnd.github+json");
        using var response = await client.SendAsync(request, token);
        if (response.StatusCode == HttpStatusCode.NotFound) return null;
        response.EnsureSuccessStatusCode();
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(token));
        var root = document.RootElement;
        if (root.GetProperty("draft").GetBoolean() || root.GetProperty("prerelease").GetBoolean()) return null;
        var version = ParseVersion(root.GetProperty("tag_name").GetString() ?? "");
        if (version is null || version <= new Version(current.Major, current.Minor, Math.Max(0, current.Build), Math.Max(0, current.Revision))) return null;
        var assets = root.GetProperty("assets").EnumerateArray().ToArray();
        var asset = assets.FirstOrDefault(a => a.GetProperty("name").GetString() == "Nagi-win-x64.zip");
        if (asset.ValueKind == JsonValueKind.Undefined) throw new InvalidDataException("The new release has no Windows x64 package yet.");
        var url = new Uri(asset.GetProperty("browser_download_url").GetString()!);
        if (!IsReleaseDownload(url)) throw new InvalidDataException("The release download address is invalid.");
        string? hash = asset.TryGetProperty("digest", out var digest) && digest.GetString() is { } value && value.StartsWith("sha256:") ? value[7..] : null;
        var checksumAsset = assets.FirstOrDefault(a => a.GetProperty("name").GetString() == "Nagi-win-x64.zip.sha256");
        var checksum = checksumAsset.ValueKind == JsonValueKind.Undefined ? null : new Uri(checksumAsset.GetProperty("browser_download_url").GetString()!);
        if (hash is null && (checksum is null || !IsReleaseDownload(checksum))) throw new InvalidDataException("The release has no SHA256 checksum yet.");
        var size = asset.GetProperty("size").GetInt64();
        if (size is <= 0 or > 1024L * 1024 * 1024) throw new InvalidDataException("The release package size is invalid.");
        var notes = root.TryGetProperty("body", out var body) ? body.GetString() ?? "" : "";
        Uri? page = null;
        if (root.TryGetProperty("html_url", out var html) && Uri.TryCreate(html.GetString(), UriKind.Absolute, out var candidate)
            && candidate.Scheme == "https" && candidate.Host == "github.com"
            && candidate.AbsolutePath.StartsWith("/" + Repository + "/releases/tag/", StringComparison.Ordinal)) page = candidate;
        return new(version, url, size, hash, checksum, ExtractChangelog(notes), page);
    }

    public async Task<PreparedUpdate> PrepareAsync(AppRelease release, string updateRoot, IProgress<double>? progress, CancellationToken token)
    {
        if (!IsReleaseDownload(release.Download)) throw new InvalidDataException("Invalid release address.");
        var hash = release.Sha256;
        if (hash is null && release.Checksum is { } checksum && IsReleaseDownload(checksum))
        {
            var text = await client.GetStringAsync(checksum, token);
            var match = Regex.Match(text.Trim(), @"\A([a-fA-F0-9]{64})(?:\s+\*?Nagi-win-x64\.zip)?\s*\z");
            if (match.Success) hash = match.Groups[1].Value;
        }
        if (hash is null || !Regex.IsMatch(hash, @"\A[a-fA-F0-9]{64}\z")) throw new InvalidDataException("The package checksum is missing or invalid.");
        var directory = Path.Combine(Path.GetFullPath(updateRoot), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            var archive = Path.Combine(directory, "download.zip");
            using (var response = await client.GetAsync(release.Download, HttpCompletionOption.ResponseHeadersRead, token))
            {
                response.EnsureSuccessStatusCode();
                await using var input = await response.Content.ReadAsStreamAsync(token);
                await using var output = File.Create(archive);
                var buffer = new byte[81920]; long received = 0; double reported = -1;
                while (true)
                {
                    var count = await input.ReadAsync(buffer, token); if (count == 0) break;
                    received += count;
                    if (received > release.Size) throw new InvalidDataException("The package exceeds its published size.");
                    await output.WriteAsync(buffer.AsMemory(0, count), token);
                    var percent = 100d * received / release.Size;
                    if (percent >= reported + 1) { reported = percent; progress?.Report(percent); }
                }
                if (received != release.Size) throw new InvalidDataException("The package download was incomplete.");
            }
            await using (var downloaded = File.OpenRead(archive))
            {
                var actual = Convert.ToHexString(await SHA256.HashDataAsync(downloaded, token));
                if (!actual.Equals(hash, StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("The package checksum did not match. Nothing was installed.");
            }
            var payload = Path.Combine(directory, "payload");
            await Task.Run(() => ExtractPackage(archive, payload, token), token);
            return new(release, directory, payload);
        }
        catch
        {
            // Only remove the newly created staging directory, never the app or user data.
            if (Directory.Exists(directory) && Path.GetFullPath(directory).StartsWith(Path.GetFullPath(updateRoot).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)) Directory.Delete(directory, true);
            throw;
        }
    }

    public static void ExtractPackage(string archivePath, string destination, CancellationToken token = default)
    {
        var root = Path.GetFullPath(destination) + Path.DirectorySeparatorChar;
        if (Directory.Exists(root) && Directory.EnumerateFileSystemEntries(root).Any()) throw new InvalidDataException("Update extraction requires an empty staging directory.");
        Directory.CreateDirectory(root);
        using var archive = ZipFile.OpenRead(archivePath);
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase); long total = 0;
        foreach (var entry in archive.Entries)
        {
            token.ThrowIfCancellationRequested();
            var name = entry.FullName.Replace('\\', '/');
            if (name.Contains(':') || name.Split('/').Any(p => p is ".." or "." || p.EndsWith(' ') || p.EndsWith('.')) || name.StartsWith('/') || ((entry.ExternalAttributes >> 16) & 0xF000) == 0xA000)
                throw new InvalidDataException("The package contains an invalid path.");
            var target = Path.GetFullPath(Path.Combine(root, name));
            if (!target.StartsWith(root, StringComparison.OrdinalIgnoreCase) || !names.Add(target)) throw new InvalidDataException("The package contains an invalid or duplicate path.");
            total += entry.Length;
            if (total > 3L * 1024 * 1024 * 1024 || names.Count > 30000) throw new InvalidDataException("The expanded package is too large.");
            if (name.EndsWith('/')) { Directory.CreateDirectory(target); continue; }
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            entry.ExtractToFile(target, false);
        }
        foreach (var required in new[] { "Nagi.exe", "Nagi.dll", "Nagi.pri", "App.xbf", "MainWindow.xbf" })
            if (!File.Exists(Path.Combine(root, required))) throw new InvalidDataException("The package is missing " + required + ".");
    }
}
