const assert=require('node:assert/strict'),fs=require('node:fs'),path=require('node:path');
const {promisify}=require('node:util'),execFile=promisify(require('node:child_process').execFile);
const {chromium}=require(process.env.PLAYWRIGHT_MODULE||'playwright');
const base=process.env.EDITOR_HUB_URL||'http://127.0.0.1:4873';
const root=process.env.TACTICAL_PROJECT_ROOT;
const candidate=process.env.TACTICAL_CANDIDATE_ROOT;
const assembly=process.env.TACTICAL_CANDIDATE_ASSEMBLY;
const output=path.resolve(process.env.TACTICAL_CARD_EVIDENCE_DIR||'tactical-card-type-evidence');
fs.mkdirSync(output,{recursive:true});
const checks=[],errors=[],writes=[],requests=[];
let reads=0,catalog,fixture=null;
const check=(pass,name)=>{assert.ok(pass,name);checks.push(name);};
(async()=>{
 const browser=await chromium.launch({headless:true,...(process.env.CHROMIUM_EXECUTABLE_PATH?{executablePath:process.env.CHROMIUM_EXECUTABLE_PATH}:{})});
 const page=await browser.newPage({viewport:{width:1500,height:1000}});
 page.on('pageerror',e=>errors.push(e.message));
 page.on('request',r=>{requests.push(r.method()+' '+r.url());if(!['GET','HEAD'].includes(r.method()))writes.push(r.method()+' '+r.url());});
 try{
  await page.route('**/*',async route=>{
   const request=route.request(),url=new URL(request.url());
   if(!['GET','HEAD'].includes(request.method())){await route.abort();return;}
   if(url.pathname==='/api/tactical-card/catalog'){
    reads++;
    if(assembly&&!fixture){
     const jsonFile=path.join(output,'catalog-read-'+reads+'.json');
     await execFile('dotnet',[assembly,'catalog',root,jsonFile],{windowsHide:true,maxBuffer:4*1024*1024});
     catalog=JSON.parse(fs.readFileSync(jsonFile,'utf8'));
    }else if(!fixture){const response=await route.fetch();catalog=await response.json();}
    await route.fulfill({status:200,contentType:'application/json',body:JSON.stringify(fixture||catalog)});return;
   }
   if(url.pathname==='/api/tactical-card/preview'&&assembly){
    const result=await execFile('dotnet',[assembly,'preview',root,url.searchParams.get('key')],{windowsHide:true,maxBuffer:4*1024*1024});
    await route.fulfill({status:200,contentType:'application/json',body:result.stdout});return;
   }
   if(candidate&&['combat-design-cards.js','combat-design-cards.css'].includes(path.basename(url.pathname))){
    const file=path.join(candidate,'Tools/MxFramework.EditorHub/web',path.basename(url.pathname));
    await route.fulfill({status:200,contentType:file.endsWith('.js')?'text/javascript':'text/css',body:fs.readFileSync(file,'utf8')});return;
   }
   await route.continue();
  });
  const url=base+'/Tools/MxFramework.EditorHub/web/combat-design.html#cards';
  const ready=async()=>{await page.waitForFunction(()=>document.querySelector('#cards-status')?.textContent.startsWith('显示 '));};
  const keys=async()=>await page.locator('.card-entry').evaluateAll(els=>els.map(e=>e.dataset.cardKey));
  const selects=async values=>{for(const [id,value] of Object.entries(values))await page.locator('#cards-'+id).selectOption(value);};
  const reset=async()=>page.locator('#cards-reset').click();
  await page.goto(url);await ready();
  const defaultKeys=await keys();
  check(defaultKeys.length===catalog.cards.filter(c=>c.record.sourceLayer!=='disk-configured').length,'default preserves formal and author-source records');
  check(catalog.cards.every(c=>c.cardType),'current directory response supplies explicit type outcomes');
  const formalPrep=catalog.cards.filter(c=>c.state==='published'&&c.cardType.kind==='Utility'&&c.cardType.utilityPayloadKind==='InteractionPreparation');
  check(formalPrep.length===5,'five real current formal cards have Utility / InteractionPreparation');
  await selects({layer:'disk-published',state:'published',kind:'Utility',payload:'InteractionPreparation'});
  check((await keys()).length===formalPrep.length,'source + state + kind + preparation subkind combine');
  check((await page.locator('#cards-grid .face-type').allTextContents()).every(t=>t==='功能卡 · 交互准备'),'every preparation face visibly labels its correct type');
  for(const card of formalPrep){
   assert.ok(card.generated?.description.length>15,'fixed preparation has useful generated text');
   assert.equal(await page.locator('[data-card-key="'+card.key+'"] .face-description').textContent(),card.generated.description);
  }
  check(true,'all five preparation faces display their authoritative effect description');
  check(await page.locator('#cards-grid .face-type img').count()===0,'Utility faces do not reuse the Action icon');
  check(await page.locator('#cards-grid .face-shell').count()===formalPrep.length,'painted card theme remains mounted');
  const themedFaces=page.locator('#cards-grid .tactical-face');
  for(let i=0;i<await themedFaces.count();i++){
   await themedFaces.nth(i).scrollIntoViewIfNeeded();
   await page.waitForFunction(index=>document.querySelector('#cards-view')?.dataset.themeError==='true'||document.querySelectorAll('#cards-grid .tactical-face')[index]?.classList.contains('theme-rendered'),i);
  }
  check(await page.locator('#cards-grid .theme-rendered').count()===formalPrep.length,'all five existing painted card themes complete their actual render');
  await page.waitForFunction(()=>[...document.querySelectorAll('#cards-grid .face-art')].every(i=>i.complete&&i.naturalWidth>0));
  await page.evaluate(()=>document.fonts.ready);
  await page.screenshot({path:path.join(output,'formal-preparation-desktop.png'),fullPage:true});
  const name=formalPrep[0].record.title,key=formalPrep[0].key;
  await page.locator('#cards-search').fill(name);
  check((await keys()).includes(key)&& (await keys()).length===formalPrep.filter(c=>c.record.title.includes(name)).length,'search combines with formal preparation filters');
  await page.locator('[data-open-card]').first().click();
  check(await page.locator('#card-detail').isVisible()&&await page.locator('#card-preview-face .face-type').textContent()==='功能卡 · 交互准备','detail face shares the same type');
  check((await page.locator('.card-facts').innerText()).includes('正式来源 · 游戏运行时解析'),'detail identifies the formal runtime basis');
  check(await page.locator('#card-full-description').textContent()===formalPrep[0].generated.description
   &&(await page.locator('#card-generated-details').textContent()).includes('F+600'),'detail shows the same useful body and authoritative fixed rules');
  await page.locator('#card-preview-apply').click();
  await page.waitForFunction(()=>document.querySelector('#card-preview-status')?.textContent.startsWith('临时预览已更新'));
  check(await page.locator('#card-preview-face .face-type').textContent()==='功能卡 · 交互准备','temporary binding preview keeps the source type');
  check(await page.locator('#card-full-description').textContent()===formalPrep[0].generated.description,'temporary preview keeps the fixed authoritative preparation text');
  await page.locator('#card-detail-close').click();
  await selects({kind:'Action'});
  check((await keys()).length===0&&await page.locator('.cards-empty').isVisible(),'incompatible Action + preparation filters produce an explicit empty result');
  await page.screenshot({path:path.join(output,'empty-combination.png')});
  await page.locator('[data-clear-cards]').click();
  check(JSON.stringify(await keys())===JSON.stringify(defaultKeys),'empty-result clear restores all default filters');
  await selects({layer:'disk-published',state:'published',kind:'Utility',payload:'InteractionPreparation'});
  const before=await keys();
  await page.locator('#cards-refresh').click();await ready();
  check(JSON.stringify(await keys())===JSON.stringify(before)&&await page.locator('#cards-kind').inputValue()==='Utility','fresh catalog read preserves all type filters');
  await page.reload();await ready();
  check(JSON.stringify(await keys())===JSON.stringify(before)&&await page.locator('#cards-payload').inputValue()==='InteractionPreparation','URL reload restores type and subkind alongside source/state');
  const bookmarked=page.url();
  await reset();await selects({layer:'disk-published',state:'published',kind:'Action'});
  const actions=catalog.cards.filter(c=>c.record.sourceLayer==='disk-published'&&c.state==='published'&&c.cardType.kind==='Action');
  check((await keys()).length===actions.length&&actions.length>0,'Action filter keeps current formal action records');
  check((await page.locator('#cards-grid .face-type').allTextContents()).every(t=>t==='行动卡'),'action face labels agree with source types');
  const componentCard=actions.find(c=>c.mechanisms.length&&c.intents.length);
  assert.ok(componentCard,'an action with current mechanism/component classification exists');
  await selects({mechanism:componentCard.mechanisms[0],intent:componentCard.intents[0].key});
  const combined=actions.filter(c=>c.mechanisms.includes(componentCard.mechanisms[0])&&c.intents.some(i=>i.key===componentCard.intents[0].key));
  check(JSON.stringify((await keys()).sort())===JSON.stringify(combined.map(c=>c.key).sort()),'mechanism and intent-component filters retain their intersection with type');
  await page.goto(bookmarked);await ready();
  await page.setViewportSize({width:390,height:844});
  check(await page.evaluate(()=>document.documentElement.scrollWidth<=innerWidth),'narrow viewport has no horizontal overflow');
  await page.locator('#cards-grid .face-type').first().scrollIntoViewIfNeeded();
  await page.waitForFunction(()=>document.querySelector('#cards-grid .theme-rendered'));
  check(await page.locator('.face-type').first().isVisible(),'type label stays visible in narrow view');
  await page.screenshot({path:path.join(output,'formal-preparation-mobile.png')});
  // The following rows are labelled fixtures, distinct from the real current-source receipt above.
  const template=structuredClone(formalPrep[0]);
  const unknown={...structuredClone(template),key:'fixture:unknown',state:'unpublished',cardType:{kind:'Unknown',status:'unavailable',message:'Fixture unsupported source'},generated:null};
  unknown.record={...unknown.record,title:'交互准备',recordId:'fixture-unknown',stableId:'',sourceLayer:'disk-author'};
  const draft={...structuredClone(template),key:'fixture:unpublished',state:'unpublished',cardType:{...template.cardType,status:'source-preview'},generated:null};
  draft.record={...draft.record,title:'普通行动名称',recordId:'fixture-draft',stableId:'',sourceLayer:'disk-author'};
  fixture={...catalog,cards:[unknown,draft],counts:{published:0,publishedVersions:0,authorDrafts:2,configured:0,unavailable:1,total:2}};
  await page.goto(url);await ready();await reset();
  check((await keys()).length===2,'fixture unsupported and unpublished records remain visible');
  await selects({kind:'Unknown'});
  check(JSON.stringify(await keys())===JSON.stringify(['fixture:unknown']),'unknown filter does not guess Utility from a preparation title');
  await reset();await selects({state:'unpublished',kind:'Utility',payload:'InteractionPreparation'});
  check(JSON.stringify(await keys())===JSON.stringify(['fixture:unpublished'])&&(await page.locator('.card-caption').innerText()).includes('来源类型预览'),'unpublished type preview remains independently searchable and explicitly labelled');
  await page.setViewportSize({width:1500,height:1000});await reset();
  await page.screenshot({path:path.join(output,'unknown-and-unpublished-fixtures.png')});
  const punctuation={...structuredClone(draft),key:'fixture:punctuation',generated:{description:'。',status:'generated-unmeasured'}};
  const emptyText={...structuredClone(draft),key:'fixture:empty-text',storedDescription:' \n\t',generated:{description:'',status:'generated-unmeasured'}};
  fixture={...fixture,cards:[punctuation,emptyText],counts:{...fixture.counts,total:2,authorDrafts:2}};
  await page.reload();await ready();
  check(await page.locator('[data-card-key="fixture:punctuation"] .face-description').textContent()===draft.storedDescription
    &&(await page.locator('[data-card-key="fixture:punctuation"] .face-basis').textContent()).includes('静态说明'),'punctuation-only generated fixture falls back to explicitly static saved text');
  check(await page.locator('[data-card-key="fixture:empty-text"] .face-description').textContent()==='尚未配置可显示的说明。','empty generated and saved content shows a useful unavailable-description placeholder');
  await page.locator('[data-card-key="fixture:empty-text"] [data-open-card]').click();
  check(await page.locator('#card-full-description').textContent()==='尚未配置可显示的说明。','empty-content detail uses the same unavailable-description fallback');
  await page.locator('#card-detail-close').click();
  fixture={...fixture,cards:[{...unknown,cardType:undefined}],counts:{...fixture.counts,total:1,authorDrafts:1}};
  await page.reload();await ready();
  check((await page.locator('#cards-notices').innerText()).includes('当前服务缺少类型投影')&&await page.locator('.face-type').textContent()==='?类型未确认','legacy service without the additive field shows unknown and an upgrade notice');
  fixture=null;await page.goto(bookmarked);await ready();
  check((await keys()).length===5,'verification restores real-source preparation view');
  check(errors.length===0&&writes.length===0,'browser has no page errors or mutating requests');
  const evidence={checkedUtc:new Date().toISOString(),mode:candidate?'isolated candidate JS/CSS and candidate C# API response over GET-only existing static server':'running EditorHub',base,root,candidate,reads,realRecordCount:catalog.cards.length,formalPreparationKeys:formalPrep.map(c=>c.key),checks,errors,writes,bookmarked};
  fs.writeFileSync(path.join(output,'browser-verification.json'),JSON.stringify(evidence,null,2));console.log(JSON.stringify(evidence,null,2));
 }catch(error){await page.screenshot({path:path.join(output,'failure.png')});fs.writeFileSync(path.join(output,'failure.json'),JSON.stringify({message:error.message,checks,errors,writes,url:page.url(),body:await page.locator('body').innerText()},null,2));throw error;}
 finally{await browser.close();}
})().catch(error=>{console.error(error);process.exitCode=1;});
