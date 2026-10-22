# Nagi website

The Nagi showcase is a lightweight, dependency-free static site with one Node.js Vercel Function. It lives separately from the Windows application.

```powershell
cd website
npm run dev
npm run check
```

Open http://localhost:4173. The home page includes interactive notch, timer, music, profile and theme previews; `/roadmap` is a separate page. Previews do not run the native app or play audio. App screenshots and the logo come from this repository.

## Release updates

`GET /api/release` reads the public GitHub latest stable release for `alaaeddinemekkaoui/Nagi`. Vercel caches successful responses for five minutes and can serve stale data while refreshing. The browser checks on load and every five minutes while visible, including after returning to an inactive tab.

The navbar version, changelog, release date and Windows download all come from that response. Production assets are chosen in this order: `Nagi-Portable.exe`, `Nagi-win-x64.zip`, `Nagi-Setup.exe`. Development builds are excluded. If GitHub is unavailable or no production asset exists, links open GitHub Releases rather than inventing a version or a file. No token or secret is required. Changelog text is rendered as plain text.

## Deploy

Set the Vercel project root to `website`, framework to **Other**, output directory to **public**, and leave build and install commands empty. Deploy this directory; do not upload the native app, local settings, logs or executable artifacts. `vercel.json` configures clean page URLs and security headers. With the CLI already authenticated to the intended account:

```powershell
vercel deploy --prod
```

Linux and macOS are marked Coming soon, with no download offered. The roadmap distinguishes current source features, ongoing validation and future directions; future dates are not promised.
