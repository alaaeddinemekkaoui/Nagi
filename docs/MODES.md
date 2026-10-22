# Integrated profile modes

Nagi v0.2.2 gives profiles an integrated mode, a compact dock button, hover details and an Overview card. Gaming maps to Gaming, Work to Developer, Stream to Creator and Focus to Focus. Default remains quiet. Custom profiles can choose any mode and inherit their template's mode.

Open a profile, choose **Integrated mode**, then **Apply mode layout** if you want its suggested widget visibility. Applying a layout preserves apps and activation rules. Existing customized widgets are not silently replaced on upgrade. You can hide mode cards and their dock button independently of ordinary widgets.

| Mode | Live features | Setup |
| --- | --- | --- |
| Gaming | PresentMon FPS, supported temperatures, active foreground game time, Discord desktop running/closed status | Integrations → Gaming; enable detection/FPS. Add exact executable rules for unsupported launchers. |
| Developer | CPU/RAM, development app shortcuts, unread GitHub notifications, up to six pinned repositories and their latest Actions workflow result | Work profile → Developer. Integrations → GitHub. |
| Creator | OBS recording/streaming state, media, a real microphone peak meter, microphone mute and existing OBS record/stream/scene controls | Stream profile → Creator. Integrations → OBS Studio. |
| Focus | Saved current task, timer, next calendar meeting, important Nagi notices | Focus profile → save task. Integrations → Calendar. |

## Gaming

Steam, Epic Games and GOG recognition uses cached local launcher metadata. Rules and locked profiles take priority. Game time measures recognized or explicitly configured games in the foreground; Alt-Tab pauses it and another game starts a fresh session. Discord status only detects the desktop process. It does not claim to read private presence, voice channels or messages. Temperature sensors require an existing supported WMI provider. FPS requires Windows tracing permission; use the explicitly approved helper when needed. See [integration setup](INTEGRATIONS.md).

## Developer

Public repository Actions status can be read without a token. Notifications need a GitHub personal token with notifications access; private repositories also require repository access. Use `owner/repository` or a GitHub repository URL, one per line, up to six. GitHub's notifications endpoint may require a classic token where fine-grained tokens are unsupported. Tokens are encrypted for the current Windows user in `github.credential`, separate from settings JSON. Disconnect removes the saved token and clears the displayed data.

Requests are read-only. Nagi never marks a notification read, changes a repository or runs a build. Notification counts are capped as `50+`. Pinned build cards open the repository's real Actions run. Unavailable permissions, rate limits and empty workflows are shown explicitly. Data refreshes at most every five minutes while the mode or its integration settings are visible; hidden modes make no GitHub requests.

## Creator

Enable the OBS WebSocket v5 server and save its local connection in Integrations. Recording, streaming and scene changes continue to require a click. Microphone levels come from the default communications endpoint, without capturing or storing PCM audio. Peak sampling runs only while Creator's meter is visible, at up to 10 samples per second, and pauses on unrelated pages or at rest.

## Focus and calendar

Save a task in the profile's mode card. Calendar accepts an absolute `.ics` file path or HTTPS feed; a native file picker is provided. Sources can contain private feed keys, so they are encrypted in `calendar.credential`. Calendar contents stay in memory and are not uploaded to Nagi's website. Remote feeds are fetched only when Focus or Calendar's integration settings need data, at most every five minutes. No calendar account is required.

The bounded reader supports timed events, UTC/floating/IANA or Windows time zones, daily and weekly recurrence, interval/count/until/BYDAY, EXDATE and edited/cancelled occurrences. It ignores all-day entries as meetings. Unsupported recurrence, custom time zones or invalid entries are reported rather than displayed as certain meetings. The next ongoing or upcoming meeting within 90 days is shown, with a Join link only when the event supplies an HTTPS URL. Other apps' Windows notifications are unchanged; Focus filters only nonessential Nagi notices.

Connections are optional. Empty or unavailable states are shown clearly. No sample FPS, temperature, build, microphone or calendar values are substituted for unavailable providers.
