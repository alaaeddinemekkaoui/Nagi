const REPO = 'https://github.com/alaaeddinemekkaoui/Nagi';
const icons = {
  bell:'M18 8a6 6 0 0 0-12 0c0 7-3 7-3 9h18c0-2-3-2-3-9M10 21h4',
  'arrow-down':'M12 4v16m-6-6 6 6 6-6', 'arrow-up-right':'M6 18 18 6M6 6h12v12', plus:'M12 5v14M5 12h14',
  search:'M21 21l-5-5M18 10a8 8 0 1 1-16 0 8 8 0 0 1 16 0',folder:'M3 7h6l2-3h8a2 2 0 0 1 2 2v13a2 2 0 0 1-2 2H4a2 2 0 0 1-2-2V6a2 2 0 0 1 2-2h5',
  timer:'M10 2h4M12 8v5l3 2M18 5l1-1M20 14a8 8 0 1 1-16 0 8 8 0 0 1 16 0',play:'m9 5 11 7-11 7Z',pause:'M8 5v14M16 5v14',reset:'M3 10a9 9 0 1 1 0 5M3 4v6h6',check:'m5 12 4 4 10-10',
  previous:'M5 5v14m13-14-9 7 9 7Z',next:'M19 5v14M6 5l9 7-9 7Z',mic:'M9 5a3 3 0 0 1 6 0v7a3 3 0 0 1-6 0Zm-3 6v1a6 6 0 0 0 12 0v-1M12 18v4m-4 0h8',cloud:'M7 18h11a4 4 0 0 0 0-8 6 6 0 0 0-12-1 4.5 4.5 0 0 0 1 9Z',
  sparkles:'m12 3 2.5 6.5L21 12l-6.5 2.5L12 21l-2.5-6.5L3 12l6.5-2.5ZM20 2v4m-2-2h4',move:'M12 2v20M2 12h20M9 5l3-3 3 3M9 19l3 3 3-3M5 9l-3 3 3 3M19 9l3 3-3 3',
  windows:'m3 5 8-1v7H3Zm10-1 8-1v8h-8ZM3 13h8v7l-8-1Zm10 0h8v8l-8-1Z',briefcase:'M8 6V3h8v3M3 6h18v14H3ZM3 11l9 4 9-4m-9-2v7',gamepad:'M8 8h8c4 0 6 9 4 11-1 1-4-2-5-2H9c-1 0-4 3-5 2-2-2 0-11 4-11Zm-1 3v5m-2-2h4m7-2h.01m2 3h.01',
  video:'M3 5h12v14H3Zm12 5 7-4v12l-7-4',terminal:'M3 4h18v16H3m3-12 4 4-4 4m7 0h5',code:'m8 6-6 6 6 6m8-12 6 6-6 6m-3-16-2 20',globe:'M21 12a9 9 0 1 1-18 0 9 9 0 0 1 18 0ZM3 12h18M12 3c-5 4-5 14 0 18 5-4 5-14 0-18',
  settings:'m9 3 6 0 1 3 3 1 2 5-2 2-1 4-4 2-3-1-4-2-1-4-2-2 2-5 3-1Zm6 9a3 3 0 1 1-6 0 3 3 0 0 1 6 0',shield:'m12 2 9 4v6c0 5-9 10-9 10S3 17 3 12V6Zm-5 10 3 3 7-7',feather:'M20 4c-3-3-11 0-13 3-2 3-2 7-1 11 4 1 8 1 11-2 3-3 5-9 3-12ZM3 21 16 8M7 14h6',
  headphones:'M3 14v-3a9 9 0 0 1 18 0v3M3 12h4v9H3Zm14 0h4v9h-4',apple:'M16 3c0-2 2-3 3-3 0 2-1 3-3 3ZM12 6c-6-4-11 3-8 10s5 6 8 5c3 1 5 2 8-5-4-2-4-6 0-8-2-3-5-4-8-2Z',
  github:'M9 21v-4c-4 1-5-2-5-2m11 6v-4c0-1-1-2-1-2 4 0 7-2 7-6 0-2-1-3-2-4 0-1 0-2-1-3-2 0-4 2-4 2h-4s-2-2-4-2C5 3 5 4 5 5 4 6 3 7 3 9c0 4 3 6 7 6 0 0-1 1-1 2',monitor:'M2 3h20v14H2Zm10 14v5m-5 0h10'
};
function icon(name) { return `<svg class="icon" viewBox="0 0 24 24" aria-hidden="true"><path d="${icons[name] || icons.sparkles}"/></svg>`; }
function renderIcons(root = document) { root.querySelectorAll('[data-icon]').forEach(el => { el.replaceWith(document.createRange().createContextualFragment(icon(el.dataset.icon))); }); }
const isRoadmap = location.pathname.replace(/\/$/, '').replace(/\.html$/, '') === '/roadmap';
document.querySelector('[data-header]').innerHTML = `<div class="header-inner wrap"><a class="wordmark" href="/" aria-label="Nagi home"><img src="/logo.svg" alt="" width="27" height="27">nagi</a><nav class="header-nav" aria-label="Main navigation"><a href="/#experience">Explore</a><a class="nav-themes" href="/#modes">Modes</a><a href="/#about">About</a><a href="/roadmap" ${isRoadmap ? 'aria-current="page"' : ''}>Roadmap</a></nav><div class="header-right"><a class="version-pill" data-release-link href="${REPO}/releases/latest" aria-label="Latest Nagi release"><span class="status-dot"></span><span data-release-version>Latest release</span></a><a class="github-link" href="${REPO}" target="_blank" rel="noopener noreferrer" aria-label="Nagi on GitHub">GitHub ${icon('github')}</a></div></div>`;
document.querySelector('[data-footer]').innerHTML = `<div class="footer-top"><a class="wordmark" href="/"><img src="/logo.svg" alt="" width="23" height="23">nagi</a><nav class="footer-links" aria-label="Footer"><a href="/#experience">Explore</a><a href="/#about">About me</a><a href="/roadmap">Roadmap</a><a href="${REPO}" target="_blank" rel="noopener noreferrer">GitHub ↗</a><a href="${REPO}/blob/main/docs/USER-GUIDE.md" target="_blank" rel="noopener noreferrer">Documentation ↗</a></nav></div><div class="footer-bottom"><span>Made with care. Improved together. Thanks to <a href="https://www.reddit.com/user/alkidnn/" target="_blank" rel="noopener noreferrer">u/alkidnn</a> and <a href="https://www.reddit.com/user/will25u1/" target="_blank" rel="noopener noreferrer">u/will25u1</a> for the ideas, testing and feedback.</span><span>A LITTLE SPACE. MORE CONTROL.</span></div>`;
renderIcons();

// Render release text as text, never as HTML supplied by an external API.
async function refreshRelease() {
  try {
    const response = await fetch('/api/release', { signal: AbortSignal.timeout(10000) });
    if (!response.ok) throw new Error('Release unavailable');
    const release = await response.json();
    if (release.status !== 'live') throw new Error('GitHub temporarily unavailable');
    document.querySelectorAll('[data-release-version]').forEach(el => { el.textContent = release.version; });
    document.querySelectorAll('[data-release-link]').forEach(el => { el.href = release.url; });
    document.querySelectorAll('[data-download]').forEach(el => { el.href = release.downloadUrl; });
    if (!release.asset) document.querySelectorAll('[data-download-label]').forEach(el => { el.textContent = 'See Windows downloads'; });
    document.querySelectorAll('[data-changelog]').forEach(el => { el.textContent = release.changelog || 'This release has no changelog. Open the release on GitHub for details.'; });
    document.querySelectorAll('[data-release-date]').forEach(el => {
      const date = new Date(release.publishedAt);
      el.textContent = Number.isNaN(date.getTime()) ? 'Latest published GitHub release.' : `Published ${new Intl.DateTimeFormat('en', { dateStyle: 'long' }).format(date)} · Latest stable release`;
    });
  } catch {
    document.querySelectorAll('[data-changelog]').forEach(el => { el.textContent = 'GitHub’s release feed is temporarily unavailable. Use the release link to read the changelog and download the latest build.'; });
  }
}
refreshRelease();
// A visitor keeping the page open sees subsequent releases without a redeploy.
let lastRefresh = Date.now();
setInterval(() => { if (!document.hidden) { refreshRelease(); lastRefresh = Date.now(); } }, 300000);
document.addEventListener('visibilitychange', () => { if (!document.hidden && Date.now() - lastRefresh > 300000) { refreshRelease(); lastRefresh = Date.now(); } });

const changelogButton = document.querySelector('[data-changelog-toggle]');
changelogButton?.addEventListener('click', () => {
  const panel = document.querySelector('#release-notes');
  panel.hidden = !panel.hidden;
  changelogButton.setAttribute('aria-expanded', String(!panel.hidden));
});

const reduceMotion = matchMedia('(prefers-reduced-motion: reduce)').matches;
if ('IntersectionObserver' in window && !reduceMotion) {
  document.documentElement.classList.add('js-animate');
  const observer = new IntersectionObserver(entries => { entries.forEach(entry => { if (entry.isIntersecting) { entry.target.classList.add('visible'); observer.unobserve(entry.target); } }); }, { threshold: 0.07 });
  document.querySelectorAll('.reveal').forEach(el => observer.observe(el));
}

const desktop = document.querySelector('[data-desktop]');
const notch = document.querySelector('[data-notch]');
let selectedDemo = 'idle';
function selectDemo(value) {
  selectedDemo = value;
  if (!desktop) return;
  desktop.classList.toggle('dock-visible', value === 'dock');
  notch.dataset.notch = value === 'dock' ? 'idle' : value;
  notch.setAttribute('aria-expanded', String(value === 'dock'));
  document.querySelectorAll('[data-demo]').forEach(el => { const active = el.dataset.demo === value; el.classList.toggle('active', active); el.setAttribute('aria-pressed', String(active)); });
}
document.querySelectorAll('[data-demo]').forEach(el => el.addEventListener('click', () => selectDemo(el.dataset.demo)));
notch?.addEventListener('click', () => selectDemo(selectedDemo === 'dock' ? 'idle' : 'dock'));
notch?.addEventListener('keydown', event => { if (event.key === 'Enter' || event.key === ' ') { event.preventDefault(); selectDemo(selectedDemo === 'dock' ? 'idle' : 'dock'); } });
notch?.addEventListener('pointerenter', event => { if (event.pointerType !== 'touch') desktop.classList.add('dock-visible'); });
desktop?.addEventListener('pointerleave', () => { if (selectedDemo !== 'dock') desktop.classList.remove('dock-visible'); });

let remaining = 25 * 60;
let timerDeadline = null;
let timerInterval;
const toggleTimer = document.querySelector('[data-timer-toggle]');
function updateTimer() {
  if (timerDeadline) remaining = Math.max(0, Math.ceil((timerDeadline - Date.now()) / 1000));
  const text = `${String(Math.floor(remaining / 60)).padStart(2, '0')}:${String(remaining % 60).padStart(2, '0')}`;
  document.querySelectorAll('[data-focus-time], [data-demo-time]').forEach(el => { el.textContent = text; });
  if (!remaining && timerDeadline) { timerDeadline = null; clearInterval(timerInterval); toggleTimer.innerHTML = icon('check'); toggleTimer.setAttribute('aria-label', 'Restart demo timer'); }
}
toggleTimer?.addEventListener('click', () => {
  if (timerDeadline) { updateTimer(); timerDeadline = null; clearInterval(timerInterval); toggleTimer.innerHTML = icon('play'); toggleTimer.setAttribute('aria-label', 'Resume demo timer'); }
  else { if (!remaining) remaining = 25 * 60; timerDeadline = Date.now() + remaining * 1000; timerInterval = setInterval(updateTimer, 1000); toggleTimer.innerHTML = icon('pause'); toggleTimer.setAttribute('aria-label', 'Pause demo timer'); selectDemo('timer'); }
  updateTimer();
});
document.querySelector('[data-timer-reset]')?.addEventListener('click', () => { clearInterval(timerInterval); timerDeadline = null; remaining = 25 * 60; toggleTimer.innerHTML = icon('play'); toggleTimer.setAttribute('aria-label', 'Start demo timer'); updateTimer(); });

let mediaPlaying = true;
document.querySelector('[data-media-toggle]')?.addEventListener('click', event => {
  mediaPlaying = !mediaPlaying;
  const button = event.currentTarget;
  button.innerHTML = icon(mediaPlaying ? 'pause' : 'play');
  button.setAttribute('aria-label', mediaPlaying ? 'Pause demo music' : 'Play demo music');
  button.setAttribute('aria-pressed', String(mediaPlaying));
  document.querySelector('.media-stage').classList.toggle('paused', !mediaPlaying);
  selectDemo(mediaPlaying ? 'media' : 'idle');
});
const tracks = ['Somewhere, slowly', 'A quieter morning', 'The space between'];
let trackIndex = 0;
function changeTrack(direction) { trackIndex = (trackIndex + direction + tracks.length) % tracks.length; document.querySelector('.media-track h3').textContent = tracks[trackIndex]; }
document.querySelector('[data-media-prev]')?.addEventListener('click', () => changeTrack(-1));
document.querySelector('[data-media-next]')?.addEventListener('click', () => changeTrack(1));

const profiles = {
  Work: [['code','GitHub notifications'], ['terminal','Actions build status'], ['folder','Pinned repositories'], ['monitor','CPU & RAM']],
  Gaming: [['gamepad','Active game time'], ['monitor','PresentMon FPS'], ['cloud','Temperatures'], ['headphones','Discord desktop']],
  Stream: [['video','OBS Studio'], ['globe','Browser'], ['mic','Microphone'], ['monitor','Three scenes']],
  Focus: [['timer','Focus timer'], ['code','One current task'], ['cloud','Next calendar meeting'], ['feather','Important Nagi notices']]
};
function selectProfile(name) {
  const root = document.querySelector('[data-profile-apps]');
  if (!root) return;
  document.querySelector('[data-profile-title]').textContent = name;
  root.innerHTML = profiles[name].map(([symbol,label]) => `<div class="app-tile"><span class="app-icon">${icon(symbol)}</span><span>${label}</span></div>`).join('');
  document.querySelectorAll('[data-profile]').forEach(el => { const active = el.dataset.profile === name; el.classList.toggle('active', active); el.setAttribute('aria-pressed', String(active)); });
}
selectProfile('Work');
document.querySelectorAll('[data-profile]').forEach(el => el.addEventListener('click', () => selectProfile(el.dataset.profile)));
const captions = { glass: 'Glass · a little of your desktop, everywhere.', dark: 'Dark · settle into something quieter.', light: 'Light · a little room for a brighter day.', akeno: 'Akeno · Japanese black and red, with a little drama.' };
document.querySelectorAll('[data-theme-choice]').forEach(el => el.addEventListener('click', () => {
  document.querySelector('[data-theme-preview]').dataset.themePreview = el.dataset.themeChoice;
  document.querySelector('[data-theme-caption]').textContent = captions[el.dataset.themeChoice];
  document.querySelectorAll('[data-theme-choice]').forEach(button => { const active = button === el; button.classList.toggle('active', active); button.setAttribute('aria-pressed', String(active)); });
}));
