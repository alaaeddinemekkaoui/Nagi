// Maintainer-only initial draft generation. Nagi itself never calls translation services.
// Sends only public English UI strings, preserves existing translations and format tokens.
import fs from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
const root = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..');
const directory = path.join(root, 'languages');
const english = JSON.parse(fs.readFileSync(path.join(directory, 'en.json'), 'utf8'));
const languages = [
  ['fr','French','Français'],['es','Spanish','Español'],['de','German','Deutsch'],['ru','Russian','Русский'],['ar','Arabic','العربية'],
  ['ja','Japanese','日本語'],['zh-Hans','Chinese (Simplified)','简体中文','zh-CN'],['zh-Hant','Chinese (Traditional)','繁體中文','zh-TW'],['ko','Korean','한국어'],
  ['it','Italian','Italiano'],['pt','Portuguese','Português'],['nl','Dutch','Nederlands'],['pl','Polish','Polski'],['uk','Ukrainian','Українська'],
  ['cs','Czech','Čeština'],['sk','Slovak','Slovenčina'],['hu','Hungarian','Magyar'],['ro','Romanian','Română'],['bg','Bulgarian','Български'],
  ['el','Greek','Ελληνικά'],['tr','Turkish','Türkçe'],['sv','Swedish','Svenska'],['da','Danish','Dansk'],['nb','Norwegian Bokmål','Norsk bokmål','no'],
  ['fi','Finnish','Suomi'],['id','Indonesian','Bahasa Indonesia'],['ms','Malay','Bahasa Melayu'],['vi','Vietnamese','Tiếng Việt'],['th','Thai','ไทย'],
  ['hi','Hindi','हिन्दी'],['bn','Bengali','বাংলা'],['ur','Urdu','اردو'],['fa','Persian','فارسی'],['he','Hebrew','עברית'],
  ['ta','Tamil','தமிழ்'],['te','Telugu','తెలుగు'],['fil','Filipino','Filipino','tl'],['sw','Swahili','Kiswahili'],['ca','Catalan','Català']
];
const selected = process.argv.slice(2);
const queue = languages.filter(([code]) => !selected.length || selected.includes(code));
const pause = ms => new Promise(resolve => setTimeout(resolve, ms));
const tokens = text => [...text.matchAll(/(?<!\{)\{\d+[^{}]*\}/g)].map(item => item[0]).sort().join('|');
function protect(text) {
  const saved = [];
  return { text:text.replace(/\{[^{}]*\}|\b(?:Nagi|OBS|WinUI|Win2D|Open-Meteo|GeoNames|YouTube Music|Spotify|Apple Music|Lua|PresentMon|CPU|GPU|FPS|Alt|Ctrl|Shift)\b|\b(?:app:|file:|folder:)\b|\n/g, value => {
    const token = `ZXP${String(saved.length).padStart(3,'0')}PXZ`; saved.push(value); return token;
  }), restore(value) {
    return value.trim().normalize('NFKC').replace(/ZXP\s*(\d{3})\s*PXZ/gi, (_,index) => saved[Number(index)] ?? '');
  } };
}
async function translate(batch, target) {
  const protectedItems = batch.map(protect);
  const text = protectedItems.map((item,index) => `[[NAGI${String(index).padStart(4,'0')}]] ${item.text}`).join('\n');
  const url = new URL('https://translate.googleapis.com/translate_a/single');
  Object.entries({client:'gtx',sl:'en',tl:target,dt:'t',q:text}).forEach(([key,value])=>url.searchParams.set(key,value));
  for(let attempt=0;attempt<4;attempt++) {
    try {
      const response = await fetch(url,{signal:AbortSignal.timeout(20000)});
      if(!response.ok) { if(response.status===429) await pause(Math.min(60000,Number(response.headers.get('retry-after')||5)*1000)); throw new Error(`Translation service ${response.status}`); }
      const data=await response.json();
      const output=data[0].map(segment=>segment[0]??'').join('').normalize('NFKC').replace(/[\u200e\u200f\u202a-\u202e\u2066-\u2069]/g,'');
      const markers=[...output.matchAll(/\[\[\s*NAGI\s*(\d{4})\s*\]\]/gi)];
      if(markers.length!==batch.length) {
        if(batch.length>1){const middle=Math.ceil(batch.length/2);return [...await translate(batch.slice(0,middle),target),...await translate(batch.slice(middle),target)];}
        const single=new URL(url);single.searchParams.set('q',protectedItems[0].text);
        const retry=await fetch(single,{signal:AbortSignal.timeout(20000)});if(!retry.ok)throw new Error(`Translation service ${retry.status}`);
        const raw=await retry.json();const value=protectedItems[0].restore(raw[0].map(item=>item[0]??'').join(''));
        if(!value||tokens(value)!==tokens(batch[0]))throw new Error('Translation changed single phrase');
        return [value];
      }
      const result=markers.map((marker,index)=>protectedItems[Number(marker[1])].restore(output.slice(marker.index+marker[0].length,markers[index+1]?.index??output.length)));
      for(let index=0;index<result.length;index++) if(!result[index]||tokens(result[index])!==tokens(batch[index])||/ZXP\d{3}PXZ/i.test(result[index])) {
        // Very short fragments can merge into adjacent lines; retry that phrase alone.
        const single=new URL(url);single.searchParams.set('q',protectedItems[index].text);
        const retry=await fetch(single,{signal:AbortSignal.timeout(20000)});
        if(!retry.ok)throw new Error(`Translation service ${retry.status}`);
        const raw=await retry.json();result[index]=protectedItems[index].restore(raw[0].map(item=>item[0]??'').join(''));
        if(!result[index]||tokens(result[index])!==tokens(batch[index]))throw new Error(`Translation changed format tokens: ${JSON.stringify(batch[index])}`);
      }
      return result;
    } catch(error) { if(attempt===3) throw error; await pause(1000*(attempt+1)); }
  }
}
async function generate([code,name,nativeName,target=code]) {
  const filename=path.join(directory,`${code}.json`);
  const pack=fs.existsSync(filename)?JSON.parse(fs.readFileSync(filename,'utf8')):{schemaVersion:1,code,name,nativeName,strings:{}};
  const cacheDirectory=path.join(root,'artifacts','translation-cache');fs.mkdirSync(cacheDirectory,{recursive:true});
  const cacheFile=path.join(cacheDirectory,`${code}.json`);
  const translated=new Map(fs.existsSync(cacheFile)?JSON.parse(fs.readFileSync(cacheFile,'utf8')):[]);
  // Keep the composite diagnostic format literal; its escaped TimeSpan format is code.
  for(const value of Object.values(english.strings)) if(value.includes('Session uptime: {2:hh'))translated.set(value,value);
  const missing=[...new Set(Object.entries(english.strings).filter(([key,value])=>!pack.strings[key]&&!translated.has(value)).map(([,value])=>value))];
  const batches=[];let batch=[],length=0;
  for(const text of missing) { if(length+text.length>2600&&batch.length){batches.push(batch);batch=[];length=0;}batch.push(text);length+=text.length+24; }
  if(batch.length)batches.push(batch);
  for(let index=0;index<batches.length;index++) {
    const items=batches[index];const values=await translate(items,target);items.forEach((text,i)=>translated.set(text,values[i]));
    fs.writeFileSync(cacheFile,JSON.stringify([...translated]));
    await pause(1500);
  }
  const strings={};for(const [key,value] of Object.entries(english.strings)){
    const text=pack.strings[key]||translated.get(value)||value;
    strings[key]=(value.match(/^\s+/)?.[0]??'')+text.trim()+(value.match(/\s+$/)?.[0]??'');
  }
  fs.writeFileSync(filename,JSON.stringify({...pack,strings},null,2)+'\n');
  console.log(`${code}: ${Object.keys(strings).length} entries; ${missing.length} newly translated phrases`);
}
let failed=false;
async function worker() { while(queue.length){const language=queue.shift();try{await generate(language);}catch(error){failed=true;console.error(`${language[0]} failed: ${error.message}`);}} }
await worker();
if(failed)process.exitCode=1;
