# Nagi UI design

Nagi v0.1 is a native Windows desktop companion with a small resting teardrop, a revealed dock and inward-opening controls.

## Dock and app folder

The teardrop touches the selected screen edge. Hover reveals the dock without taking keyboard focus. The logo opens the control center and supports drag-to-position. Horizontal top/bottom and vertical left/right layouts follow monitor bounds and DPI.

Profiles show up to three direct app shortcuts. A folder tile groups extra apps and expands on hover or click. Moving into the folder keeps its controls available. The expanded app grid uses up to four columns horizontally and two vertically. Overview lists every app in rows of up to four.

## Controls and surfaces

The dock combines search, CPU/GPU, now playing and enabled optional widgets. Details for system readings, media/audio and OBS open inward. App launch and recording/streaming/scene changes require deliberate clicks.

Native acrylic, Win2D surfaces, readable Windows typography, rounded cards and the red arc/ripple logo keep the interface consistent. Glass follows system appearance; Akeno adds a black/red palette with optional artwork. The native window region matches the visible silhouette.

## Motion and accessibility

Native window springs preserve velocity when retargeted. Content fades and button feedback use the Windows compositor. Pointer previews preserve the dock's location, and delayed dismissal gives time to move into them. Reduced motion snaps to the same final layout. High contrast and Windows transparency policy remain authoritative.

Unsupported media controls are disabled. Missing device/provider values stay unavailable. Audio visuals use measured system peaks, without recording raw audio.

## References

- [Microsoft system backdrops](https://learn.microsoft.com/en-us/windows/apps/develop/ui/system-backdrops)
- [Microsoft accessibility guidance](https://learn.microsoft.com/en-us/windows/apps/design/accessibility/accessibility)
- [Core Audio signal metering](https://learn.microsoft.com/en-us/windows/win32/api/endpointvolume/nn-endpointvolume-iaudiometerinformation)
