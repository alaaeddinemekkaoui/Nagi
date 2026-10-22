import { createServer } from 'node:http';
import { readFile } from 'node:fs/promises';
import { resolve, extname, sep } from 'node:path';
import handler from '../api/release.js';

const root = resolve('public');
const types = { '.html': 'text/html; charset=utf-8', '.css': 'text/css; charset=utf-8', '.js': 'text/javascript; charset=utf-8', '.svg': 'image/svg+xml', '.png': 'image/png', '.webp': 'image/webp' };
const server = createServer(async (request, response) => {
  const url = new URL(request.url, 'http://localhost');
  if (url.pathname === '/api/release') {
    response.status = code => { response.statusCode = code; return response; };
    response.json = value => { response.setHeader('Content-Type', 'application/json'); response.end(request.method === 'HEAD' ? undefined : JSON.stringify(value)); };
    return handler(request, response);
  }
  let pathname;
  try { pathname = decodeURIComponent(url.pathname); } catch { response.writeHead(400).end(); return; }
  if (pathname === '/') pathname = '/index.html';
  else if (!extname(pathname)) pathname += '.html';
  const file = resolve(root, `.${pathname}`);
  if (!file.startsWith(root + sep)) { response.writeHead(403).end(); return; }
  try { const bytes = await readFile(file); response.writeHead(200, { 'Content-Type': types[extname(file)] || 'application/octet-stream' }); response.end(request.method === 'HEAD' ? undefined : bytes); }
  catch { response.writeHead(404, { 'Content-Type': types['.html'] }); response.end(await readFile(resolve(root, '404.html'))); }
});
server.listen(Number(process.env.PORT || 4173), '0.0.0.0', () => console.log(`Nagi site: http://localhost:${server.address().port}`));
