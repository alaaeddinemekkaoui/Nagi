using Pulse.Core;
using Pulse.Infrastructure;
using Xunit;

namespace Pulse.Tests;

public sealed class QuickSearchTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "PulseSearchTests", Guid.NewGuid().ToString("N"));
    private static SearchEntry App(string name) => new("app:" + name, name, "Application", SearchKind.Application, name);
    [Fact] public void NamesPrefixesAccentsAndInitialsRankAheadOfPathOnlyMatches()
    {
        SearchEntry[] entries = [App("Visual Studio Code"), App("Café"), new("file", "Readme.txt", "Visual Studio Code projects", SearchKind.File, "readme"), App("Code")];
        Assert.Equal("Code", QuickSearch.Find(entries, "code")[0].Title);
        Assert.Equal("Visual Studio Code", QuickSearch.Find(entries, "vsc")[0].Title);
        Assert.Equal("Café", QuickSearch.Find(entries, "cafe")[0].Title);
        Assert.Equal("Visual Studio Code", QuickSearch.Find(entries, "visual code")[0].Title);
    }
    [Theory]
    [InlineData("app:", SearchKind.Application)] [InlineData("file:", SearchKind.File)]
    [InlineData("folder:", SearchKind.Folder)] [InlineData(">", SearchKind.Command)]
    public void FiltersRestrictResultKinds(string query, SearchKind kind)
    {
        SearchEntry[] entries = [App("Notes"), new("file", "Notes.txt", "", SearchKind.File, "x"), new("root:folder", "Notes", "", SearchKind.Folder, "x"), new("cmd", "Notes", "", SearchKind.Command, "x")];
        var found = QuickSearch.Find(entries, query); Assert.Single(found); Assert.Equal(kind, found[0].Kind);
    }
    [Fact] public void EmptySearchDoesNotDumpPersonalFileNamesAndCapsResults()
    {
        SearchEntry[] entries = [new("private", "Private document", "", SearchKind.File, "x"), .. Enumerable.Range(0, 100).Select(i => App("App " + i))];
        Assert.Equal(40, QuickSearch.Find(entries, "").Count);
        Assert.DoesNotContain(QuickSearch.Find(entries, ""), e => e.Kind == SearchKind.File);
        Assert.Empty(QuickSearch.Find(entries, new string('a', 257)));
    }
    [Fact] public void WebSearchIsExplicitAndEscapesQueryAsOneParameter()
    {
        Assert.Empty(QuickSearch.Find([], "hello world"));
        var result = Assert.Single(QuickSearch.Find([], "web a&b #c"));
        Assert.Equal(SearchKind.Web, result.Kind); Assert.EndsWith("a%26b%20%23c", result.Target);
    }
    [Theory]
    [InlineData("2 + 3 * 4", "14")] [InlineData("(2+3)*4", "20")] [InlineData("24 * 7", "168")]
    [InlineData("-2 * -4", "8")] [InlineData("1.5 / .5", "3")] [InlineData("10 % 3", "1")]
    public void CalculatorUsesPrecedenceAndProducesCopyableValues(string input, string expected)
    { Assert.True(Calculator.TryCalculate(input, out var value)); Assert.Equal(expected, value); }
    [Theory]
    [InlineData("1/0")] [InlineData("0/0")] [InlineData("2 + cmd.exe")] [InlineData("2**3")]
    [InlineData("(1+2")] [InlineData("1,5+1")] [InlineData("1..2+3")] [InlineData("File.txt")]
    public void CalculatorRejectsInvalidOrNonFiniteExpressions(string input) => Assert.False(Calculator.TryCalculate(input, out _));
    [Fact] public void CalculatorBoundsRecursionAndSourceLength()
    { Assert.False(Calculator.TryCalculate(new string('(', 40) + "1+1" + new string(')', 40), out _)); Assert.False(Calculator.TryCalculate(new string('1', 129) + "+1", out _)); }
    [Fact] public async Task FileIndexFindsLockedFilesWithoutReadingContentsAndSkipsBuildFolders()
    {
        var root = Path.Combine(_directory, "files"); Directory.CreateDirectory(root); Directory.CreateDirectory(Path.Combine(root, "node_modules"));
        var file = Path.Combine(root, "Important notes.txt"); await File.WriteAllTextAsync(file, "SECRET-CONTENTS-DO-NOT-INDEX");
        await File.WriteAllTextAsync(Path.Combine(root, "node_modules", "noise.txt"), "x");
        using var locked = File.Open(file, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
        var index = new FileSearchIndex(Path.Combine(_directory, "index")); await index.RefreshAsync([root], default);
        var item = Assert.Single(index.Snapshot.Entries); Assert.Equal(file, item.Target);
        Assert.Equal("Important notes.txt", Assert.Single(QuickSearch.Find(index.Snapshot.Entries, "important")).Title);
        Assert.DoesNotContain("SECRET-CONTENTS", await File.ReadAllTextAsync(Path.Combine(_directory, "index", "file-index.json")));
        var loaded = new FileSearchIndex(Path.Combine(_directory, "index")); await loaded.LoadAsync([root], default);
        Assert.Equal(item, Assert.Single(loaded.Snapshot.Entries));
        await loaded.LoadAsync([Path.Combine(_directory, "different")], default); Assert.Empty(loaded.Snapshot.Entries);
    }
    [Fact] public async Task IndexCancellationPreservesSnapshotAndMalformedCacheCanRecover()
    {
        Directory.CreateDirectory(_directory); var file = Path.Combine(_directory, "file-index.json"); await File.WriteAllTextAsync(file, "not json");
        var index = new FileSearchIndex(_directory); await index.LoadAsync([_directory], default); Assert.Empty(index.Snapshot.Entries);
        using var cancellation = new CancellationTokenSource(); cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => index.RefreshAsync([_directory], cancellation.Token));
        Assert.Null(index.Snapshot.Updated);
        await index.RefreshAsync([], default); Assert.NotNull(index.Snapshot.Updated); Assert.Empty(index.Snapshot.Entries);
    }
    [Fact] public async Task BoundedIndexVisitsSelectedRootsBeforeDescendingIntoLargeTrees()
    {
        var first = Path.Combine(_directory, "first"); var deep = Path.Combine(first, "large-project"); var second = Path.Combine(_directory, "second");
        Directory.CreateDirectory(deep); Directory.CreateDirectory(second);
        for (var i = 0; i < 12; i++) await File.WriteAllTextAsync(Path.Combine(deep, i + ".txt"), "x");
        var important = Path.Combine(second, "Report.txt"); await File.WriteAllTextAsync(important, "x");
        var index = new FileSearchIndex(Path.Combine(_directory, "cache"), 4); await index.RefreshAsync([first, second], default);
        Assert.Equal(4, index.Snapshot.Entries.Length); Assert.True(index.Snapshot.Limited);
        Assert.Contains(index.Snapshot.Entries, e => e.Target == important);
    }
    [Fact] public void RemovedScopesAndSiblingPrefixesAreNotWithinRoot()
    {
        var root = Path.Combine(_directory, "a"); Assert.True(FileSearchIndex.WithinRoot(Path.Combine(root, "file.txt"), root));
        Assert.False(FileSearchIndex.WithinRoot(Path.Combine(_directory, "ab", "file.txt"), root));
        Assert.False(FileSearchIndex.WithinRoot(Path.Combine(root, "..", "other.txt"), root));
    }
    [Fact] public async Task SearchPreferencesRoundTripAndNormalizeWithoutErasingOtherPreferences()
    {
        using var settings = new SettingsService(_directory);
        await settings.SaveAsync(new() { Theme = "Akeno", SearchHotkey = new(6, 0x51), IndexFiles = false, SearchFolders = ["  test  ", "TEST", ""] });
        var read = await settings.LoadAsync(); Assert.Equal(new(6, 0x51), read.SearchHotkey); Assert.False(read.IndexFiles); Assert.Equal(["test"], read.SearchFolders); Assert.Equal("Akeno", read.Theme);
        Assert.Equal(new(1, 0x20), new PulseSettings { SearchHotkey = null! }.Normalize().SearchHotkey);
    }
    public void Dispose() { if (Directory.Exists(_directory)) Directory.Delete(_directory, true); }
}
