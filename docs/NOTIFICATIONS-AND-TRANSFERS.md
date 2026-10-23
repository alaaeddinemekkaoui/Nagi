# Notifications and transfers

## Show app notifications in the notch

1. Open **Settings → Integrations → Notifications**.
2. Click **Connect Windows notifications**. In the portable edition this enables **Mirror Windows notification banners**; you can also use that toggle directly.
3. In Windows, open **Settings → System → Notifications**. Enable notifications and **Show notification banners** for the app you want to mirror. Windows' **Do not disturb** can prevent banners from appearing; Nagi does not change that setting.
4. Receive a new notification from that app. A supported visible Windows banner briefly appears in Nagi's resting notch, with a circular indicator. Nagi does not need to have its main window open.
5. The most recent mirrored banners appear under **Utilities → Notifications** while Nagi is running. **Disconnect Windows notifications** stops observation and clears the in-memory external items.

Nagi's own notification preference must be enabled to show the preview. Focus mode suppresses external previews. Portable mirroring uses read-only Windows accessibility APIs, not a notification database, process injection or a permission bypass. It observes supported visible toast containers on the current desktop; silent notifications, banners hidden by Windows, secure desktops and unsupported shell layouts cannot be mirrored. It cannot recover notifications received while Nagi was closed. Text stays in memory, is limited to 30 recent banners and is never logged, saved to disk, uploaded or dismissed from Windows. Repeated visible banners are deduplicated.

For full Windows notification history, use the packaged listener described below. That option requires a trusted signed package and explicit Windows permission.

## Windows copies and moves, without copying through Nagi

**Show Windows file transfers in the notch** is enabled by default. Change it under **Settings → Integrations → Notifications** or in the **Utilities → Windows file transfers** card.

1. Leave Nagi running, with its main window closed or collapsed.
2. Copy or move files normally in **File Explorer**. Windows Shell operations started by another app can also expose the standard progress dialog.
3. Keep the Windows progress dialog available. Nagi reads its actual accessible percentage or standard copy-dialog caption and shows a circular percentage in the notch. With multiple transfers, the status lists the active count and the notch follows one dialog; it does not invent an aggregate byte percentage.
4. When the dialog closes, progress leaves the notch. A closed dialog is not reported as a successful copy unless Windows was observed at 100%; closing may also mean cancellation.

Very short transfers can finish between samples. Command-line copies, background browser downloads, cloud synchronization and apps without a supported Windows progress dialog are not observable through this bridge. Nagi does not install a disk driver, inspect arbitrary disk I/O, change the Windows operation or offer cancellation of another app's copy. Disable the switch to stop observation.

## Optional Nagi file shelf

Utilities also contains the existing Transfer Center shelf. Drop files onto Nagi or use Add files. The shelf remembers up to 24 local paths; it does not upload or duplicate files automatically. Remove/Clear only remove shelf references. Copy to folder uses Windows' folder picker, preserves originals and creates a numbered name when the destination already exists. Cancel removes the partially written current file and keeps any completed files. This shelf is independent of automatic Windows transfer observation.

Download progress for app updates appears in the same center and in the resting notch as a rounded circular fill. Nagi alerts briefly replace the logo, then normal timer/media activity resumes. Focus suppresses nonessential Nagi notices and external notification previews.

## Packaged Windows notification listener

Settings → Integrations → Notifications offers Connect Windows notifications. Notification titles and bodies stay in memory; Nagi does not write them to its activity log, upload them, or dismiss them in Windows. Disconnect removes the in-memory items and stops the listener. Windows can revoke access at any time.

Microsoft requires the `userNotificationListener` package capability and user permission for the history/listener API. The ordinary portable EXE has no package identity, so Windows cannot grant it that capability; portable mirroring instead observes visible banners as described above. See [Microsoft's listener guide](https://learn.microsoft.com/en-us/windows/apps/develop/notifications/app-notifications/notification-listener) and [UI Automation client properties](https://learn.microsoft.com/en-us/windows/win32/winauto/uiauto-propertiesforclients).

## Optional packaged edition

Build the matching production app first, then run:

```powershell
pwsh -NoProfile -File scripts/Build-NagiMsix.ps1
```

This produces `artifacts/Nagi-Unsigned.msix` for review. It cannot be installed as a normal production package until signed with a certificate trusted by the target PC. The build never creates or installs a certificate, changes trust, or enables Developer Mode.

To sign with your existing code-signing certificate in the current user's certificate store:

```powershell
pwsh -NoProfile -File scripts/Build-NagiMsix.ps1 -CertificateThumbprint YOUR_CERTIFICATE_THUMBPRINT
```

The signing script uses the certificate's subject as Publisher, timestamps the signature and verifies it. It does not export the private key. Install the signed MSIX, close any portable copy, launch the packaged Nagi and connect through Integrations → Notifications to grant Windows permission. Packaged updates use a newer signed MSIX with the same identity and publisher; portable ZIP self-updates are disabled in that edition because installed package files are managed by Windows.

The native accessibility reader and banner filters are tested against owned Windows controls and fixtures. A real 1 GB Windows Shell copy was observed in the resting notch. The test toast did not produce an accessible banner on this desktop, so receiving real app banners is not certified here. The full history/listener path additionally requires an installed trusted package and Windows permission and remains environment-dependent.

## FPS permission and troubleshooting

See [the FPS guide](INTEGRATIONS.md#fps). Enable the Gaming profile's Performance/FPS widgets and **Integrations → PresentMon FPS → Capture foreground app FPS**. Start and focus the game. If Windows reports denied tracing permission, open **FPS help → Start FPS helper…**, review the prompt and approve Windows' request for the helper only. Return to the game and allow a few seconds for a fresh sample. Nagi itself stays unprivileged; the helper does not permanently change Windows user groups. Disable FPS or close Nagi to stop an app-started helper. Rates from old or unrelated processes are rejected, and unavailable FPS stays hidden.

If you use the manual PowerShell helper, supply the actual game PID from Task Manager → Details and follow the command in the FPS guide. It measures application presentations, not monitor refresh rate; a high video frame rate is not automatic game recognition.

## Replacing the earlier v0.3 build

This release is named **v0.3.0**, with internal app version **0.3.0**. The earlier v0.3 build had the same internal version, so it cannot detect this replacement as a newer semantic version. Close the old copy and download/run the new portable or installer manually. Preferences remain in the existing data directory. Users on older versions such as v0.2.2 can use **Settings → Updates** normally.
