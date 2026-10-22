# Notifications and transfers

Utilities contains a Transfer Center and notification cards. Drop files onto Nagi or use Add files. The shelf remembers up to 24 local paths; it does not upload or duplicate files automatically. Remove/Clear only remove shelf references. Copy to folder uses Windows' folder picker, preserves originals and creates a numbered name when the destination already exists. Cancel removes the partially written current file and keeps any completed files.

Download progress for app updates appears in the same center and in the resting notch as a rounded circular fill. Nagi alerts briefly replace the logo, then normal timer/media activity resumes. Focus suppresses nonessential Nagi notices and external notification previews.

## Other Windows apps

Settings → Integrations → Notifications offers Connect Windows notifications. Notification titles and bodies stay in memory; Nagi does not write them to its activity log, upload them, or dismiss them in Windows. Disconnect removes the in-memory items and stops the listener. Windows can revoke access at any time.

Microsoft requires the `userNotificationListener` package capability and user permission. The ordinary portable EXE has no package identity, so Windows cannot grant it this capability. It shows an explanation instead of claiming to be connected. See [Microsoft's listener guide](https://learn.microsoft.com/en-us/windows/apps/develop/notifications/app-notifications/notification-listener).

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

The listener's portable/unavailable behavior and transfer service are tested here. Receiving other apps' real notifications requires an installed, trusted package and the user's Windows permission; that environment-dependent flow is not certified by the portable test.
