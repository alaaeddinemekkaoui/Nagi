using System.Globalization;

namespace Pulse.Core;

public sealed record CalendarMeeting(string Title, DateTimeOffset Start, DateTimeOffset End, string? JoinUrl);
public sealed record CalendarSnapshot(CalendarMeeting? Next, string Status);

public static class CalendarMeetings
{
    private sealed record Field(string Parameters, string Value);
    public static CalendarSnapshot Parse(string text, DateTimeOffset now, TimeZoneInfo? localZone = null)
    {
        if (text.Length > 2 * 1024 * 1024 || !text.Contains("BEGIN:VCALENDAR", StringComparison.Ordinal)) return new(null, "Invalid or oversized calendar");
        var lines = new List<string>();
        foreach (var line in text.Replace("\r\n", "\n").Split('\n'))
        {
            if ((line.StartsWith(' ') || line.StartsWith('\t')) && lines.Count > 0) { if (lines[^1].Length + line.Length > 65536) return new(null, "Calendar line too long"); lines[^1] += line[1..]; }
            else lines.Add(line.TrimEnd('\r'));
        }
        var events = new List<Dictionary<string, Field>>(); Dictionary<string, Field>? current = null; var depth = 0;
        foreach (var line in lines)
        {
            if (line == "BEGIN:VEVENT") { current = new(StringComparer.OrdinalIgnoreCase); depth = 1; continue; }
            if (current is null) continue;
            if (line == "END:VEVENT") { if (events.Count < 5000) events.Add(current); current = null; continue; }
            if (line.StartsWith("BEGIN:")) { depth++; continue; } if (line.StartsWith("END:")) { depth--; continue; } if (depth != 1) continue;
            var colon = line.IndexOf(':'); if (colon < 1) continue;
            var name = line[..colon].Split(';')[0]; var field = new Field(line[..colon], line[(colon + 1)..]);
            if (name.Equals("EXDATE", StringComparison.OrdinalIgnoreCase) && current.TryGetValue(name, out var existing)) field = field with { Value = existing.Value + "," + field.Value };
            current.TryAdd(name, field); if (name.Equals("EXDATE", StringComparison.OrdinalIgnoreCase)) current[name] = field;
        }
        var meetings = new List<(string Uid, CalendarMeeting Meeting)>(); var overrides = new HashSet<(string, DateTimeOffset)>(); var skipped = 0;
        foreach (var item in events)
        {
            var uid = item.GetValueOrDefault("UID")?.Value ?? "";
            if (item.TryGetValue("RECURRENCE-ID", out var recurrence) && Date(recurrence, localZone) is { } replacement) overrides.Add((uid, replacement));
        }
        foreach (var item in events)
        {
            if (item.GetValueOrDefault("STATUS")?.Value.Equals("CANCELLED", StringComparison.OrdinalIgnoreCase) == true) continue;
            if (!item.TryGetValue("DTSTART", out var startField) || Date(startField, localZone) is not { } start) { skipped++; continue; }
            var end = item.TryGetValue("DTEND", out var endField) ? Date(endField, localZone) : start;
            if (!item.ContainsKey("DTEND") && item.TryGetValue("DURATION", out var durationField))
            { try { end = start + System.Xml.XmlConvert.ToTimeSpan(durationField.Value); } catch (FormatException) { skipped++; continue; } }
            if (end is null) { skipped++; continue; }
            var duration = end.Value - start; if (duration < TimeSpan.Zero || duration > TimeSpan.FromDays(7)) { skipped++; continue; }
            var uid = item.GetValueOrDefault("UID")?.Value ?? "";
            var title = Unescape(item.GetValueOrDefault("SUMMARY")?.Value ?? "Meeting");
            var url = item.GetValueOrDefault("URL")?.Value;
            if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || uri.Scheme != "https") url = null;
            var excluded = new HashSet<DateTimeOffset>();
            if (item.TryGetValue("EXDATE", out var exceptions)) foreach (var value in exceptions.Value.Split(',')) if (Date(exceptions with { Value = value }, localZone) is { } date) excluded.Add(date);
            var occurrences = new List<DateTimeOffset> { start };
            if (item.TryGetValue("RRULE", out var rule))
            {
                var options = rule.Value.Split(';').Select(x => x.Split('=', 2)).Where(x => x.Length == 2).GroupBy(x => x[0], StringComparer.OrdinalIgnoreCase).ToDictionary(x => x.Key, x => x.Last()[1], StringComparer.OrdinalIgnoreCase);
                if (!options.TryGetValue("FREQ", out var frequency) || frequency is not ("DAILY" or "WEEKLY") || options.Keys.Any(x => x is not ("FREQ" or "INTERVAL" or "COUNT" or "UNTIL" or "BYDAY" or "WKST"))) { skipped++; continue; }
                var interval = options.TryGetValue("INTERVAL", out var intervalText) && int.TryParse(intervalText, out var parsedInterval) ? Math.Clamp(parsedInterval, 1, 366) : 1;
                var count = options.TryGetValue("COUNT", out var countText) && int.TryParse(countText, out var parsedCount) ? Math.Clamp(parsedCount, 0, 1000000) : int.MaxValue;
                var until = options.TryGetValue("UNTIL", out var untilText) ? Date(startField with { Value = untilText }, localZone) ?? DateTimeOffset.MinValue : now.AddDays(90);
                var zone = Zone(startField, localZone); if (zone is null) { skipped++; continue; }
                var wallStart = TimeZoneInfo.ConvertTime(start, zone).DateTime;
                var weekdays = options.TryGetValue("BYDAY", out var days) ? days.Split(',') : [DayCode(wallStart.DayOfWeek)];
                if (weekdays.Any(d => !new[] { "MO", "TU", "WE", "TH", "FR", "SA", "SU" }.Contains(d)) || options.TryGetValue("WKST", out var weekStart) && weekStart != "MO") { skipped++; continue; }
                occurrences.Clear(); var seen = 0; var mondayOffset = ((int)wallStart.DayOfWeek + 6) % 7;
                for (var day = 0; day <= 20000 && seen < count; day++)
                {
                    var wall = wallStart.AddDays(day);
                    if (wall > TimeZoneInfo.ConvertTime(now.AddDays(90), zone).DateTime || zone.IsInvalidTime(wall)) { if (zone.IsInvalidTime(wall)) continue; break; }
                    var matches = frequency == "DAILY" ? day % interval == 0 && (!options.ContainsKey("BYDAY") || weekdays.Contains(DayCode(wall.DayOfWeek)))
                        : (day + mondayOffset) / 7 % interval == 0 && weekdays.Contains(DayCode(wall.DayOfWeek));
                    if (!matches) continue;
                    var occurrence = new DateTimeOffset(wall, zone.GetUtcOffset(wall)); if (occurrence > until) break; seen++;
                    if (occurrence + duration > now) occurrences.Add(occurrence);
                }
            }
            foreach (var occurrence in occurrences)
                if (occurrence + duration > now && occurrence < now.AddDays(90) && !excluded.Contains(occurrence)
                    && (item.ContainsKey("RECURRENCE-ID") || !overrides.Contains((uid, occurrence))))
                    meetings.Add((uid, new(title, occurrence, occurrence + duration, url)));
        }
        var next = meetings.OrderBy(x => x.Meeting.Start).Select(x => x.Meeting).FirstOrDefault();
        return new(next, skipped > 0 ? "Calendar loaded · some all-day, unsupported recurrence or timezone entries were skipped" : "Calendar connected");
    }
    private static string DayCode(DayOfWeek day) => new[] { "SU", "MO", "TU", "WE", "TH", "FR", "SA" }[(int)day];
    private static string Unescape(string text)
    { var value = text.Replace("\\n", " ", StringComparison.OrdinalIgnoreCase).Replace("\\,", ",").Replace("\\;", ";").Replace("\\\\", "\\"); return value[..Math.Min(160, value.Length)]; }
    private static TimeZoneInfo? Zone(Field field, TimeZoneInfo? local)
    {
        if (field.Value.EndsWith('Z')) return TimeZoneInfo.Utc;
        var zone = field.Parameters.Split(';').FirstOrDefault(x => x.StartsWith("TZID=", StringComparison.OrdinalIgnoreCase))?[5..].Trim('"');
        if (zone is null) return local ?? TimeZoneInfo.Local;
        try { return TimeZoneInfo.FindSystemTimeZoneById(zone); } catch (TimeZoneNotFoundException) { return null; } catch (InvalidTimeZoneException) { return null; }
    }
    private static DateTimeOffset? Date(Field field, TimeZoneInfo? local)
    {
        if (!DateTime.TryParseExact(field.Value.TrimEnd('Z'), ["yyyyMMdd'T'HHmmss", "yyyyMMdd'T'HHmm"], CultureInfo.InvariantCulture, DateTimeStyles.None, out var wall)) return null;
        var zone = Zone(field, local); return zone is null || zone.IsInvalidTime(wall) ? null : new DateTimeOffset(wall, zone.GetUtcOffset(wall));
    }
}
