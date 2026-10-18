using System.IO.Compression;
using System.Net;
using System.Security.Cryptography;
using System.Text.Json;
using Pulse.Infrastructure;
using Xunit;

namespace Pulse.Tests;

public sealed class AppUpdateTests
{
    [Theory]
    [InlineData(null, "")]
    [InlineData("- Fixed themes\n- Aligned widgets", "- Fixed themes\n- Aligned widgets")]
    [InlineData("## Nagi v0.1.3\r\n\r\n- Fixed widgets\r\n\r\n---\r\n\r\n<div>Full README, downloads and installation</div>", "## Nagi v0.1.3\n\n- Fixed widgets")]
    [InlineData("## Nagi v0.1.3\n- Fixed widgets\n\n## Nagi v0.1.2\n- Older changes", "## Nagi v0.1.3\n- Fixed widgets")]
    public void UpdateNotesContainOnlyTheCurrentChangelog(string? body, string expected)
        => Assert.Equal(expected, AppUpdateService.ExtractChangelog(body));

    [Theory]
    [InlineData("v2", "2.0.0.0")]
    [InlineData("v0.1", "0.1.0.0")]
    [InlineData("v0.1.1", "0.1.1.0")]
    [InlineData("1.2.3", "1.2.3.0")]
    [InlineData("v1.2.3.4", "1.2.3.4")]
    [InlineData("v1.2.3-beta", null)]
    [InlineData("not-a-version", null)]
    [InlineData("v99999999999999999.1", null)]
    public void StableReleaseVersionsAreComparedNumerically(string input, string? expected) => Assert.Equal(expected, AppUpdateService.ParseVersion(input)?.ToString());

    [Theory]
    [InlineData("https://github.com/alaaeddinemekkaoui/Nagi/releases/download/v2/Nagi-win-x64.zip", true)]
    [InlineData("https://example.com/Nagi.zip", false)]
    [InlineData("http://github.com/alaaeddinemekkaoui/Nagi/releases/download/v2/Nagi-win-x64.zip", false)]
    [InlineData("https://github.com/another/project/releases/download/v2/Nagi-win-x64.zip", false)]
    public void DownloadsBelongToTheNagiReleaseRepository(string address, bool expected) => Assert.Equal(expected, AppUpdateService.IsReleaseDownload(new(address)));

    [Theory]
    [InlineData("v1.0.0", false, false)]
    [InlineData("v1.10.0", false, true)]
    [InlineData("v2.0.0", true, false)]
    public async Task ChecksSkipCurrentVersionsAndPrereleases(string tag, bool prerelease, bool available)
    {
        using var client = new HttpClient(new Handler(_ => new(HttpStatusCode.OK) { Content = new StringContent(JsonSerializer.Serialize(new
        {
            tag_name = tag, draft = false, prerelease,
            assets = new[] { new { name = "Nagi-win-x64.zip", browser_download_url = Download, size = 12, digest = "sha256:" + new string('a', 64) } }
        })) }));
        var release = await new AppUpdateService(client).CheckAsync(new(1, 0, 0), default);
        Assert.Equal(available, release is not null);
    }

    [Theory]
    [InlineData("0.1.0", "v0.1.1", true)]
    [InlineData("0.1.1", "v0.1.1", false)]
    [InlineData("0.1.1", "v0.1.2", true)]
    [InlineData("0.1.2", "v0.1.1", false)]
    public async Task EarlyVersionsDetectOnlyNewerReleasesAndCarryTheirChangelog(string current, string tag, bool available)
    {
        const string notes = "- Fixed light theme\n- Added separate error logs";
        using var client = new HttpClient(new Handler(_ => new(HttpStatusCode.OK) { Content = new StringContent(JsonSerializer.Serialize(new
        {
            tag_name = tag, draft = false, prerelease = false, body = notes,
            html_url = "https://github.com/alaaeddinemekkaoui/Nagi/releases/tag/" + tag,
            assets = new[] { new { name = "Nagi-win-x64.zip", browser_download_url = Download, size = 12, digest = "sha256:" + new string('a', 64) } }
        })) }));
        var release = await new AppUpdateService(client).CheckAsync(Version.Parse(current), default);
        Assert.Equal(available, release is not null);
        if (available) { Assert.Equal(notes, release!.Notes); Assert.NotNull(release.ReleasePage); }
    }

    [Theory] [InlineData(false)] [InlineData(true)]
    public async Task DownloadsVerifyHashAndExtractCompletePayload(bool checksumFile)
    {
        var bytes = Package(); var hash = Convert.ToHexString(SHA256.HashData(bytes));
        using var client = new HttpClient(new Handler(request => new(HttpStatusCode.OK)
        { Content = request.RequestUri!.AbsolutePath.EndsWith(".sha256") ? new StringContent(hash + "  Nagi-win-x64.zip") : new ByteArrayContent(bytes) }));
        var root = Scratch();
        try
        {
            var release = new AppRelease(new(2, 0, 0), new(Download), bytes.Length, checksumFile ? null : hash, new(Download + ".sha256"));
            var prepared = await new AppUpdateService(client).PrepareAsync(release, root, null, default);
            Assert.Equal("new-app", await File.ReadAllTextAsync(Path.Combine(prepared.Payload, "Nagi.exe")));
            Assert.Equal(release, prepared.Release);
        }
        finally { Directory.Delete(root, true); }
    }

    [Theory] [InlineData(false)] [InlineData(true)]
    public async Task IncompleteOrCorruptDownloadsLeaveNoInstallablePayload(bool incomplete)
    {
        var bytes = Package(); using var client = new HttpClient(new Handler(_ => new(HttpStatusCode.OK) { Content = new ByteArrayContent(bytes) }));
        var root = Scratch();
        try
        {
            var release = new AppRelease(new(2, 0, 0), new(Download), bytes.Length + (incomplete ? 1 : 0), new string('0', 64), null);
            await Assert.ThrowsAsync<InvalidDataException>(() => new AppUpdateService(client).PrepareAsync(release, root, null, default));
            Assert.Empty(Directory.GetFileSystemEntries(root));
        }
        finally { Directory.Delete(root, true); }
    }

    [Theory]
    [InlineData("../outside.txt")]
    [InlineData("/absolute.txt")]
    [InlineData("C:/absolute.txt")]
    [InlineData("Nagi.exe:stream")]
    public void UnsafeArchivePathsAreRejected(string path)
    {
        var root = Scratch(); var archive = Path.Combine(root, "archive.zip");
        try
        {
            using (var zip = ZipFile.Open(archive, ZipArchiveMode.Create)) using (var writer = new StreamWriter(zip.CreateEntry(path).Open())) writer.Write("bad");
            Assert.Throws<InvalidDataException>(() => AppUpdateService.ExtractPackage(archive, Path.Combine(root, "payload")));
            Assert.False(File.Exists(Path.Combine(root, "outside.txt")));
        }
        finally { Directory.Delete(root, true); }
    }
    [Fact] public void IncompleteArchivesCannotBecomeAnUpdate()
    {
        var root = Scratch(); var archive = Path.Combine(root, "archive.zip");
        try
        {
            using (var zip = ZipFile.Open(archive, ZipArchiveMode.Create)) zip.CreateEntry("Nagi.exe");
            Assert.Throws<InvalidDataException>(() => AppUpdateService.ExtractPackage(archive, Path.Combine(root, "payload")));
        }
        finally { Directory.Delete(root, true); }
    }
    private const string Download = "https://github.com/alaaeddinemekkaoui/Nagi/releases/download/v2/Nagi-win-x64.zip";
    private static string Scratch() { var root = Path.Combine(Path.GetTempPath(), "NagiUpdateTests", Guid.NewGuid().ToString("N")); Directory.CreateDirectory(root); return root; }
    private static byte[] Package()
    {
        using var memory = new MemoryStream();
        using (var zip = new ZipArchive(memory, ZipArchiveMode.Create, true))
            foreach (var name in new[] { "Nagi.exe", "Nagi.dll", "Nagi.pri", "App.xbf", "MainWindow.xbf" })
            { using var writer = new StreamWriter(zip.CreateEntry(name).Open()); writer.Write("new-app"); }
        return memory.ToArray();
    }
    private sealed class Handler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) => Task.FromResult(respond(request));
    }
}
