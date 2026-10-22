using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Pulse.Core;

namespace Pulse.Infrastructure;

public sealed record RepositoryBuild(string Repository, string Status, string? Url);
public sealed record DeveloperSnapshot(int? Unread, RepositoryBuild[] Repositories, string Status);

public sealed class GitHubModeService(HttpClient? client = null)
{
    private readonly HttpClient _http = client ?? new(new HttpClientHandler { AllowAutoRedirect = false }) { Timeout = TimeSpan.FromSeconds(10) };
    public async Task<DeveloperSnapshot> SampleAsync(IEnumerable<string> repositories, string token, CancellationToken cancellationToken)
    {
        var builds = new List<RepositoryBuild>(); int? unread = null; var status = token.Length == 0 ? "Public repositories · connect GitHub for notifications" : "GitHub connected";
        if (token.Length > 0)
        {
            try { using var notifications = await GetAsync("notifications?per_page=50", token, cancellationToken); unread = notifications.RootElement.GetArrayLength(); status = unread >= 50 ? "GitHub connected · at least 50 unread" : "GitHub connected"; }
            catch (HttpRequestException) { status = "Notifications unavailable · check token permissions or rate limit"; }
        }
        foreach (var repository in repositories.Select(RepositoryName.Normalize).Where(r => r is not null).Distinct(StringComparer.OrdinalIgnoreCase).Take(6))
        {
            try
            {
                using var runs = await GetAsync($"repos/{repository}/actions/runs?per_page=1", token, cancellationToken);
                var values = runs.RootElement.GetProperty("workflow_runs");
                if (values.GetArrayLength() == 0) { builds.Add(new(repository!, "No workflow runs", null)); continue; }
                var run = values[0]; var state = run.GetProperty("status").GetString() ?? "Unknown";
                if (state == "completed" && run.TryGetProperty("conclusion", out var conclusion)) state = conclusion.GetString() ?? state;
                var url = run.TryGetProperty("html_url", out var link) ? link.GetString() : null;
                if (!Uri.TryCreate(url, UriKind.Absolute, out var safe) || safe.Scheme != "https" || safe.Host != "github.com") url = null;
                builds.Add(new(repository!, state, url));
            }
            catch (HttpRequestException) { builds.Add(new(repository!, "Unavailable · permission or rate limit", null)); }
        }
        return new(unread, builds.ToArray(), status);
    }
    private async Task<JsonDocument> GetAsync(string path, string token, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "https://api.github.com/" + path);
        request.Headers.UserAgent.ParseAdd("Nagi/0.2.2"); request.Headers.Accept.ParseAdd("application/vnd.github+json"); request.Headers.Add("X-GitHub-Api-Version", "2022-11-28");
        if (token.Length > 0) request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        using var response = await _http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken); response.EnsureSuccessStatusCode();
        var text = await BoundedText.ReadAsync(response.Content, 1024 * 1024, cancellationToken);
        return JsonDocument.Parse(text);
    }
}

public sealed class CalendarModeService(HttpClient? client = null)
{
    private readonly HttpClient _http = client ?? new(new HttpClientHandler { AllowAutoRedirect = false }) { Timeout = TimeSpan.FromSeconds(10) };
    public async Task<CalendarSnapshot> SampleAsync(string source, DateTimeOffset now, CancellationToken token)
    {
        if (string.IsNullOrWhiteSpace(source)) return new(null, "Connect a calendar link or .ics file");
        string text;
        if (Uri.TryCreate(source, UriKind.Absolute, out var uri) && uri.Scheme == "https" && string.IsNullOrEmpty(uri.UserInfo))
        {
            using var response = await _http.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead, token); response.EnsureSuccessStatusCode();
            text = await BoundedText.ReadAsync(response.Content, 2 * 1024 * 1024, token);
        }
        else
        {
            if (!Path.IsPathFullyQualified(source) || !source.EndsWith(".ics", StringComparison.OrdinalIgnoreCase)) return new(null, "Choose an HTTPS calendar link or an absolute .ics file path");
            var file = new FileInfo(source); if (!file.Exists || file.Length > 2 * 1024 * 1024) return new(null, "Calendar file missing or too large");
            text = await File.ReadAllTextAsync(source, token);
        }
        return CalendarMeetings.Parse(text, now);
    }
}

internal static class BoundedText
{
    internal static async Task<string> ReadAsync(HttpContent content, int limit, CancellationToken token)
    {
        if (content.Headers.ContentLength > limit) throw new HttpRequestException("Response too large");
        await using var stream = await content.ReadAsStreamAsync(token); using var memory = new MemoryStream(); var buffer = new byte[8192]; int read;
        while ((read = await stream.ReadAsync(buffer, token)) > 0) { if (memory.Length + read > limit) throw new HttpRequestException("Response too large"); memory.Write(buffer, 0, read); }
        return Encoding.UTF8.GetString(memory.ToArray());
    }
}
