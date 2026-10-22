using System.Globalization;
using System.Text;

namespace Pulse.Core;

public enum SearchKind { Application, File, Folder, Command, Calculation, Web }
public sealed record SearchEntry(string Id, string Title, string Detail, SearchKind Kind, string Target);
public enum SearchProvider { Local, Google, YouTube }
public readonly record struct SearchQuery(SearchProvider Provider, string Text, bool ExplicitProvider = false);

public static class QuickSearch
{
    public static SearchQuery ParseQuery(string? query, SearchProvider provider = SearchProvider.Local)
    {
        var text = (query ?? "").Trim();
        var colon = text.IndexOf(':');
        if (colon >= 0)
        {
            var prefix = text[..colon].Trim().ToLowerInvariant();
            if (prefix is "google" or "browser" or "web" or "youtube" or "yt")
                return new(prefix is "youtube" or "yt" ? SearchProvider.YouTube : SearchProvider.Google, text[(colon + 1)..].Trim(), true);
            if (prefix is "app" or "file" or "folder") return new(SearchProvider.Local, text, true);
        }
        if (text.StartsWith("web ", StringComparison.OrdinalIgnoreCase)) return new(SearchProvider.Google, text[4..].Trim(), true);
        if (text.StartsWith('>')) return new(SearchProvider.Local, text, true);
        return new(Enum.IsDefined(provider) ? provider : SearchProvider.Local, text);
    }

    public static IReadOnlyList<SearchEntry> Find(IEnumerable<SearchEntry> entries, string? query, int limit = 40, SearchProvider provider = SearchProvider.Local)
    {
        var text = (query ?? "").Trim();
        if (text.Length > 256) return [];
        var request = ParseQuery(text, provider);
        text = request.Text;
        if (request.Provider != SearchProvider.Local)
        {
            var youtube = request.Provider == SearchProvider.YouTube;
            var name = youtube ? "YouTube" : "Google";
            var home = youtube ? "https://www.youtube.com/" : "https://www.google.com/";
            var target = text.Length == 0 ? home : (youtube ? "https://www.youtube.com/results?search_query=" : "https://www.google.com/search?q=") + Uri.EscapeDataString(text);
            return [new("web:" + name.ToLowerInvariant(), text.Length == 0 ? "Open " + name : "Search " + name + " for “" + text + "”", "Opens your browser on Enter", SearchKind.Web, target)];
        }
        SearchKind? filter = null;
        foreach (var (prefix, kind) in new[] { ("app:", SearchKind.Application), ("file:", SearchKind.File), ("folder:", SearchKind.Folder), (">", SearchKind.Command) })
            if (text.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) { filter = kind; text = text[prefix.Length..].Trim(); break; }
        var results = entries.Where(e => filter is null || e.Kind == filter)
            .Select(e => (Entry: e, Score: text.Length == 0 && filter.HasValue ? 0 : Score(e, text)))
            .Where(e => e.Score >= 0).OrderByDescending(e => e.Score)
            .ThenBy(e => e.Entry.Title, StringComparer.CurrentCultureIgnoreCase)
            .DistinctBy(e => e.Entry.Id, StringComparer.OrdinalIgnoreCase)
            .Take(Math.Clamp(limit, 1, 100)).Select(e => e.Entry).ToList();
        if (filter is null && Calculator.TryCalculate(text, out var number))
            results.Insert(0, new("calculation", number, "Calculator · Enter to copy result", SearchKind.Calculation, number));
        return results.Take(Math.Clamp(limit, 1, 100)).ToArray();
    }

    private static string Fold(string value)
    {
        var builder = new StringBuilder();
        foreach (var c in value.Normalize(NormalizationForm.FormD))
            if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark) builder.Append(char.ToLowerInvariant(c));
        return builder.ToString();
    }
    private static int Score(SearchEntry entry, string query)
    {
        if (query.Length == 0) return entry.Kind switch { SearchKind.Application => 40, SearchKind.Command => 20, SearchKind.Folder when entry.Id.StartsWith("root:") => 30, _ => -1 };
        var title = Fold(entry.Title); var q = Fold(query);
        var boost = entry.Kind == SearchKind.Application ? 15 : 0;
        if (title == q) return 1000 + boost;
        if (title.StartsWith(q, StringComparison.Ordinal)) return 800 + boost;
        if (title.Contains(q, StringComparison.Ordinal)) return 600 + boost;
        var words = title.Split([' ', '-', '_', '.'], StringSplitOptions.RemoveEmptyEntries);
        if (string.Concat(words.Select(w => w[0])).StartsWith(q, StringComparison.Ordinal)) return 450 + boost;
        var tokens = q.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (tokens.Length > 1 && tokens.All(t => title.Contains(t, StringComparison.Ordinal))) return 400 + boost;
        if (q.Length >= 2 && tokens.All(t => Fold(entry.Detail).Contains(t, StringComparison.Ordinal))) return 100;
        return -1;
    }
}

// A bounded arithmetic parser: no script engine, variables, function calls or command execution.
public static class Calculator
{
    public static bool TryCalculate(string text, out string result)
    {
        result = ""; text = text.Trim().TrimStart('=').Trim();
        if (text.Length is 0 or > 128 || !text.Any(c => "+-*/%()".Contains(c))) return false;
        try
        {
            var parser = new Parser(text); var value = parser.Expression(); parser.Space();
            if (parser.Position != text.Length || !double.IsFinite(value)) return false;
            result = value.ToString("G12", CultureInfo.InvariantCulture); return true;
        }
        catch (FormatException) { return false; }
    }
    private sealed class Parser(string text)
    {
        public int Position; private int _depth;
        public void Space() { while (Position < text.Length && char.IsWhiteSpace(text[Position])) Position++; }
        private bool Eat(char c) { Space(); if (Position < text.Length && text[Position] == c) { Position++; return true; } return false; }
        public double Expression()
        {
            var n = Term(); while (true) { if (Eat('+')) n += Term(); else if (Eat('-')) n -= Term(); else return n; }
        }
        private double Term()
        {
            var n = Factor(); while (true) { if (Eat('*')) n *= Factor(); else if (Eat('/')) n /= Factor(); else if (Eat('%')) n %= Factor(); else return n; }
        }
        private double Factor()
        {
            if (++_depth > 32) throw new FormatException();
            try
            {
                if (Eat('+')) return Factor(); if (Eat('-')) return -Factor();
                if (Eat('(')) { var n = Expression(); if (!Eat(')')) throw new FormatException(); return n; }
                Space(); var start = Position;
                while (Position < text.Length && (char.IsAsciiDigit(text[Position]) || text[Position] == '.')) Position++;
                if (start == Position || !double.TryParse(text[start..Position], NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var value)) throw new FormatException();
                return value;
            }
            finally { _depth--; }
        }
    }
}
