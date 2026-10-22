import { getLatestRelease, fallbackRelease } from '../lib/releases.js';

export default async function handler(request, response) {
  if (request.method !== 'GET' && request.method !== 'HEAD') {
    response.setHeader('Allow', 'GET, HEAD');
    return response.status(405).json({ error: 'Method not allowed' });
  }
  try {
    const release = await getLatestRelease();
    response.setHeader('Cache-Control', 'public, max-age=60, s-maxage=300, stale-while-revalidate=3600');
    return response.status(200).json(release);
  } catch {
    // Never pin a stale version or fabricate a download when GitHub is unavailable.
    response.setHeader('Cache-Control', 'public, max-age=15, s-maxage=30');
    return response.status(200).json(fallbackRelease);
  }
}
