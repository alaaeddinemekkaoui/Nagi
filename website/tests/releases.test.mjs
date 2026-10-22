import { test } from 'node:test';
import assert from 'node:assert/strict';
import { normalizeRelease, getLatestRelease, RELEASES, githubUrl } from '../lib/releases.js';
import handler from '../api/release.js';

const release = { tag_name:'v0.1.3', html_url:'https://github.com/alaaeddinemekkaoui/Nagi/releases/tag/v0.1.3', body:'Theme and dock fixes.', assets:[] };
const asset = name => ({ name, browser_download_url:`https://github.com/alaaeddinemekkaoui/Nagi/releases/download/v0.1.3/${name}` });
test('prefers the production portable executable regardless of asset ordering', () => {
  const result = normalizeRelease({ ...release, assets: [asset('Nagi-Dev-Portable.exe'), asset('Nagi-win-x64.zip'), asset('Nagi-Portable.exe')] });
  assert.equal(result.asset, 'Nagi-Portable.exe'); assert.equal(result.version, 'v0.1.3'); assert.equal(result.changelog, release.body);
});
test('uses production ZIP then installer, never a developer executable', () => {
  assert.equal(normalizeRelease({...release,assets:[asset('Nagi-Setup.exe'),asset('Nagi-win-x64.zip')]}).asset,'Nagi-win-x64.zip');
  assert.equal(normalizeRelease({...release,assets:[asset('Nagi-Setup.exe')]}).asset,'Nagi-Setup.exe');
  const result=normalizeRelease({...release,assets:[asset('Nagi-Dev-Portable.exe')]});
  assert.equal(result.asset,null); assert.equal(result.downloadUrl,RELEASES);
});
test('rejects drafts, prereleases and malformed releases',()=>{
  for(const value of [null,{}, {...release,draft:true},{...release,prerelease:true},{...release,tag_name:''}]) assert.throws(()=>normalizeRelease(value));
});
test('rejects foreign and unsafe release and download URLs',()=>{
  for(const url of ['javascript:alert(1)','http://github.com/alaaeddinemekkaoui/Nagi/releases/latest','https://example.com/file.exe','https://github.com/other/Nagi/releases/latest']) assert.equal(githubUrl(url),RELEASES);
  const result=normalizeRelease({...release,assets:[{name:'Nagi-Portable.exe',browser_download_url:'https://example.com/bad.exe'}]});
  assert.equal(result.downloadUrl,RELEASES); assert.equal(result.asset,null);
});
test('fetches latest release on every uncached request and preserves new version/changelog',async()=>{
  const result=await getLatestRelease(async(url,options)=>{assert.match(url,/releases\/latest$/);assert.ok(options.signal);return {ok:true,json:async()=>({...release,tag_name:'v0.1.4',body:'A new changelog.'})};});
  assert.equal(result.version,'v0.1.4'); assert.equal(result.changelog,'A new changelog.');
});
test('rate limits and failed GitHub responses do not become fabricated releases',async()=>{
  await assert.rejects(()=>getLatestRelease(async()=>({ok:false,status:403})),/403/);
});
test('endpoint restricts request methods',async()=>{
  const headers={}; let body; const response={setHeader:(key,value)=>{headers[key]=value;},status(code){this.statusCode=code;return this;},json(value){body=value;}};
  await handler({method:'POST'},response); assert.equal(response.statusCode,405);assert.equal(headers.Allow,'GET, HEAD');assert.equal(body.error,'Method not allowed');
});
test('endpoint failure provides a safe GitHub link and a short retry cache',async()=>{
  const original=globalThis.fetch; globalThis.fetch=async()=>{throw new Error('offline');};
  try {const headers={};let body;const response={setHeader:(key,value)=>{headers[key]=value;},status(code){this.statusCode=code;return this;},json(value){body=value;}};await handler({method:'GET'},response);assert.equal(body.status,'unavailable');assert.equal(body.version,null);assert.equal(body.downloadUrl,RELEASES);assert.match(headers['Cache-Control'],/s-maxage=30/);} finally {globalThis.fetch=original;}
});
