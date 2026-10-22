export const REPOSITORY = 'https://github.com/alaaeddinemekkaoui/Nagi';
export const RELEASES = `${REPOSITORY}/releases/latest`;
export const RELEASE_API = 'https://api.github.com/repos/alaaeddinemekkaoui/Nagi/releases/latest';

export function githubUrl(value, fallback = RELEASES) {
  try {
    const url = new URL(value);
    return url.protocol === 'https:' && url.hostname === 'github.com' &&
      url.pathname.startsWith('/alaaeddinemekkaoui/Nagi/releases/') ? url.href : fallback;
  } catch { return fallback; }
}

export function normalizeRelease(release) {
  if (!release || release.draft || release.prerelease || typeof release.tag_name !== 'string' || !release.tag_name.trim()) {
    throw new Error('A published stable release is required.');
  }
  // Exact production asset names prevent accidentally offering a developer build.
  const assets = Array.isArray(release.assets) ? release.assets : [];
  const asset = ['Nagi-Portable.exe', 'Nagi-win-x64.zip', 'Nagi-Setup.exe']
    .map(name => assets.find(item => item.name === name && githubUrl(item.browser_download_url, null)))
    .find(Boolean);
  return {
    version: release.tag_name.slice(0, 64),
    name: typeof release.name === 'string' ? release.name.slice(0, 160) : release.tag_name,
    url: githubUrl(release.html_url),
    downloadUrl: asset ? githubUrl(asset.browser_download_url) : RELEASES,
    asset: asset?.name ?? null,
    publishedAt: typeof release.published_at === 'string' ? release.published_at : null,
    changelog: typeof release.body === 'string' ? release.body.slice(0, 30000) : '',
    status: 'live'
  };
}

export async function getLatestRelease(fetcher = fetch) {
  const response = await fetcher(RELEASE_API, {
    headers: { Accept: 'application/vnd.github+json', 'User-Agent': 'Nagi-website' },
    signal: AbortSignal.timeout(8000)
  });
  if (!response.ok) throw new Error(`GitHub responded ${response.status}`);
  return normalizeRelease(await response.json());
}

export const fallbackRelease = {
  version: null, name: 'Latest release on GitHub', url: RELEASES,
  downloadUrl: RELEASES, asset: null, publishedAt: null, changelog: '', status: 'unavailable'
};
