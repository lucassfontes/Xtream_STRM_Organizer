// Exercise the real status renderer with controlled API responses; no browser or dependencies.
const fs=require('node:fs');
const vm=require('node:vm');
const assert=require('node:assert/strict');
const html=fs.readFileSync(require('node:path').join(__dirname,'../wwwroot/index.html'),'utf8');
const source=html.slice(html.indexOf('let statusInFlight=false;'),html.indexOf("document.addEventListener('visibilitychange'"));
assert(source.includes('finally'));
const elements=new Map();
function $(id){if(!elements.has(id)){const attrs={};elements.set(id,{style:{},attrs,classList:{toggle(n,v){this[n]=v;}},setAttribute(n,v){attrs[n]=v;},removeAttribute(n){delete attrs[n];}});}return elements.get(id);}
let state={phase:'Aguardando',running:false,progressPercent:0,messages:[]};let calls=0;let blocked;
const ctx=vm.createContext({$,AbortController,setTimeout,clearTimeout,duration:()=>'',api:async()=>{calls++;if(blocked)return blocked;return state;}});
vm.runInContext(source,ctx);
(async()=>{
 await ctx.status();assert.equal($('pct').textContent,'0%');
 state={...state,startedAt:'now',running:true,progressIndeterminate:true};
 await ctx.status();assert.equal($('pct').textContent,'Calculando…');assert(!('aria-valuenow' in $('meter').attrs));
 state={...state,progressIndeterminate:false,catalogReady:true,itemsTotal:10,itemsProcessed:3,progressPercent:30};
 await ctx.status();assert.equal($('bar').style.width,'30%');assert($('progress').textContent.includes('3/10'));
 state={...state,itemsProcessed:10,progressPercent:99,filesPending:1};
 await ctx.status();assert.equal($('pct').textContent,'99%');
 state={...state,running:false,phase:'Cancelado',progressPercent:30};
 await ctx.status();assert.equal($('pct').textContent,'30%');
 let release;blocked=new Promise(r=>release=r);const before=calls;
 const request=ctx.status();await ctx.status();assert.equal(calls,before+1,'overlapping status requests prevented');
 release({...state,phase:'Concluído',completed:true,progressPercent:100,filesPending:0});await request;blocked=null;
 assert.equal($('pct').textContent,'100%');
 console.log('PASS: real status renderer: unknown total, counts, pending queue, cancellation, completion and overlapping requests.');
})().catch(e=>{console.error(e);process.exitCode=1;});
