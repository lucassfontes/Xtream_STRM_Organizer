/* Optional browser test: npm install --no-save playwright; npx playwright install chromium
   PUBLISH_DIR=/tmp/xtream-publish node tests/ui-smoke.cjs
   DOTNET and CHROME_EXECUTABLE may point to custom installations. */
const assert=require('node:assert/strict');
const fs=require('node:fs');
const os=require('node:os');
const path=require('node:path');
const {spawn}=require('node:child_process');
const {chromium}=require(require.resolve('playwright',{paths:[process.cwd(),process.env.CODEX_PRIMARY_RUNTIME_NODE_MODULES||process.cwd()]}));
(async()=>{
 const temp=fs.mkdtempSync(path.join(os.tmpdir(),'xtream-ui-'));
 const publish=path.resolve(process.env.PUBLISH_DIR||'/tmp/xtream-publish');
 const server=spawn(process.env.DOTNET||'dotnet',[path.join(publish,'XtreamStrmOrganizer.dll')],{
   cwd:publish,env:{...process.env,CONFIG_FILE:path.join(temp,'config/config.json'),OUTPUT_ROOT:path.join(temp,'media'),ASPNETCORE_URLS:'http://127.0.0.1:0'},
   stdio:['ignore','pipe','pipe']
 });
 let browser;
 try {
   const base=await new Promise((resolve,reject)=>{
     const deadline=setTimeout(()=>reject(Error('Server startup timeout')),15000);
     server.stdout.on('data',data=>{const m=data.toString().match(/Now listening on: (http:\/\/127\.0\.0\.1:\d+)/);if(m){clearTimeout(deadline);resolve(m[1]);}});
     server.on('exit',code=>{clearTimeout(deadline);reject(Error('Server exit '+code));});
   });
   browser=await chromium.launch({headless:true,executablePath:process.env.CHROME_EXECUTABLE||undefined,args:['--no-sandbox']});
   let context=await browser.newContext({viewport:{width:390,height:844}});
   let page=await context.newPage();
   const errors=[];
   const watch=p=>p.on('pageerror',e=>errors.push(e.message));watch(page);
   const shot=async name=>{if(process.env.UI_SCREENSHOTS){fs.mkdirSync(process.env.UI_SCREENSHOTS,{recursive:true});await page.screenshot({path:path.join(process.env.UI_SCREENSHOTS,name+'.png'),fullPage:true});}};
   const fits=async()=>{
     assert(await page.evaluate(()=>document.documentElement.scrollWidth<=innerWidth+1),'Horizontal overflow at '+JSON.stringify(page.viewportSize()));
   };
   await page.goto(base+'/setup');
   await page.locator('#username').fill('testadmin');
   await page.locator('#password').fill('test-admin-123');
   await page.locator('#confirm').fill('test-admin-123');
   await page.locator('#create').click();
   await page.waitForURL('**/login.html');
   assert((await page.request.get(base+'/responsive.css')).headers()['content-type'].includes('text/css'));
   assert((await page.request.get(base+'/icon.png')).headers()['content-type'].includes('image/png'));
   for(const width of [320,390,768,1440]) {await page.setViewportSize({width,height:844});await fits();}
   await page.setViewportSize({width:390,height:844});
   await shot('login-mobile');
   await page.locator('#username').fill('testadmin');
   await page.locator('#password').fill('test-admin-123');
   await page.locator('#submit').click();
   await page.waitForURL(base+'/');
   await page.waitForFunction(()=>document.querySelector('#profileName').value==='Minha IPTV');
   const manual=['liveImages','liveNfo','movieImages','movieNfo','seriesImages','seriesNfo'];
   const scheduled=manual.map(key=>'auto'+key[0].toUpperCase()+key.slice(1));
   await page.locator('#profileName').fill('Minha IPTV');
   await page.locator('#url').fill('http://fixture.invalid');
   await page.locator('#username').fill('fixture');
   await page.locator('#password').fill('fake-fixture-password');
   for(const key of manual)await page.locator('#'+key).uncheck();
   for(const key of scheduled)await page.locator('#'+key).check();
   await page.locator('#autoUpdate').check();
   await page.locator('#saveSchedule').click();
   await page.waitForFunction(()=>document.querySelector('#scheduleNotice').textContent.includes('salvos'));
   await page.reload();
   await page.waitForFunction(()=>document.querySelector('#autoSeriesNfo').checked);
   for(const key of manual) assert(!await page.locator('#'+key).isChecked(),key);
   for(const key of scheduled) assert(await page.locator('#'+key).isChecked(),key);
   await page.locator('#autoMovieNfo').uncheck();
   await page.locator('#saveSchedule').click();
   await page.waitForFunction(()=>document.querySelector('#scheduleNotice').textContent.includes('salvos'));
   await page.reload();
   await page.waitForFunction(()=>document.querySelector('#autoSeriesNfo').checked);
   assert(!await page.locator('#autoMovieNfo').isChecked());
   for(const width of [320,390,768,1440]){
     await page.setViewportSize({width,height:900});
     await fits();
     await page.evaluate(()=>window.scrollTo({top:0,behavior:'instant'}));
     if(width===390||width===1440)await shot('dashboard-'+width);
   }
   // Browser storage contains the last user/preference, never either password.
   const state=await context.storageState();
   const storage=state.origins.flatMap(o=>o.localStorage);
   assert.equal(storage.find(x=>x.name==='xtream.lastUsername').value,'testadmin');
   assert(!JSON.stringify(storage).includes('test-admin-123'));
   assert(!JSON.stringify(storage).includes('fake-fixture-password'));
   assert(state.cookies.find(c=>c.name==='xtream_strm_session').expires>Date.now()/1000+29*86400);
   await context.close();
   context=await browser.newContext({storageState:state,viewport:{width:390,height:844}});
   page=await context.newPage();watch(page);
   await page.goto(base+'/login');
   await page.waitForURL(base+'/');
   await page.locator('#logout').click();
   await page.waitForURL('**/login.html');
   assert.equal(await page.locator('#username').inputValue(),'testadmin');
   assert.equal(await page.locator('#password').inputValue(),'');
   await page.locator('#rememberMe').uncheck();
   await page.locator('#password').fill('test-admin-123');
   await page.locator('#submit').click();
   await page.waitForURL(base+'/');
   assert.equal((await context.cookies()).find(c=>c.name==='xtream_strm_session').expires,-1);
   assert.deepEqual(errors,[]);
   console.log('PASS: Chromium browser, 320/390/768/1440px without horizontal overflow; real forms, separate schedule persistence, username memory, persistent session reopen, logout and opt-out. No JS errors.');
 } finally {
   if(browser)await browser.close();
   server.kill();
   await new Promise(resolve=>{if(server.exitCode!==null)return resolve();server.on('exit',resolve);setTimeout(()=>{server.kill('SIGKILL');resolve();},3000).unref();});
   fs.rmSync(temp,{recursive:true,force:true});
 }
})().catch(e=>{console.error(e);process.exitCode=1;});
