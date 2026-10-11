const fs=require('fs'),path=require('path'),assert=require('assert');
const {chromium}=require(process.env.PLAYWRIGHT_MODULE||'playwright-core');
const origin=process.argv[2]||'http://127.0.0.1:4873',out=path.resolve(process.argv[3]||'artifacts/tactical-cards');
fs.mkdirSync(out,{recursive:true});
(async()=>{
  const browser=await chromium.launch({executablePath:process.env.BROWSER_PATH,headless:true});
  const page=await browser.newPage({viewport:{width:1550,height:1080}}),errors=[],checks=[],writes=[];
  page.on('pageerror',e=>errors.push(e.message));
  page.context().on('request',r=>{if(!['GET','HEAD'].includes(r.method()))writes.push(r.method()+' '+r.url());});
  const check=(name,condition)=>{assert(condition,name);checks.push(name);console.log('PASS '+name);};
  try{
    const responsePromise=page.waitForResponse(r=>r.url().includes('/api/tactical-card/catalog')&&r.status()===200);
    await page.goto(origin+'/Tools/MxFramework.EditorHub/web/combat-design.html');
    const catalog=await(await responsePromise).json();await page.locator('.card-entry').first().waitFor();
    check('Default card tab selected',await page.locator('#cards-tab').getAttribute('aria-selected')==='true');
    check('All current formal and author records shown initially',await page.locator('.card-entry').count()===catalog.counts.publishedVersions+catalog.counts.authorDrafts);
    check('Game card art loads',await page.locator('.face-art').first().evaluate(e=>e.complete&&e.naturalWidth>0));
    check('Game card background resolves', (await page.request.get(origin+'/FGUIProject/assets/MxCharacterTestRuntimeAiLabUi/Images/MxUiFoundation/card_foundation_ai_v1/behavior_card_base.png')).ok());
    await page.screenshot({path:path.join(out,'all-templates.png'),fullPage:true});
    await page.screenshot({path:path.join(out,'desktop.png')});
    await page.locator('#cards-layer').selectOption('all');check('All three source layers accounted for',await page.locator('.card-entry').count()===catalog.counts.total);
    await page.locator('#cards-layer').selectOption('disk-published');check('Formal count agrees with current index',await page.locator('.card-entry').count()===catalog.counts.publishedVersions);
    await page.locator('#cards-search').fill('常规移动接敌出招');check('Same-name formal cards stay distinct',await page.locator('.card-entry').count()===2);
    await page.locator('#cards-reset').click();await page.locator('#cards-state').selectOption('invalid');check('Invalid indexed draft remains visible',await page.locator('.card-entry').count()===catalog.counts.unavailable);
    await page.locator('#cards-reset').click();await page.locator('#cards-search').fill('不存在的卡目录回归搜索');check('Empty search is explicit',await page.locator('.cards-empty').isVisible());
    await page.getByRole('button',{name:'清除筛选',exact:true}).last().click();
    for(let i=0;i<3;i++){
      await page.locator('#cards-layer').selectOption('disk-published');await page.locator('#cards-mechanism').selectOption('移动');
      check('Repeated mechanism filter '+i,(await page.locator('.card-entry').count())===catalog.cards.filter(c=>c.record.sourceLayer==='disk-published'&&c.mechanisms.includes('移动')).length);
      await page.locator('#cards-reset').click();await page.locator('#cards-intent').selectOption('component/ordinary-guard');
      check('Repeated guard component filter '+i,(await page.locator('.card-entry').count())===catalog.cards.filter(c=>c.record.sourceLayer!=='disk-configured'&&c.intents.some(x=>x.key==='component/ordinary-guard')).length);
      await page.locator('#cards-reset').click();
    }
    for(const [intent,id] of [['wait-two-seconds','bc_n1xa3efw08'],['wait-target-distance','bc_vyj9212dvz'],['selected-point','bc_4vbe5xqnaa'],['behind-target-arrive','bc_n5xfe76ha9'],['behind-target-maintain','bc_ej3sd7tx4x']]){
      await page.locator('#cards-reset').click();await page.locator('#cards-layer').selectOption('disk-published');await page.locator('#cards-intent').selectOption('component/'+intent);
      check('Merged component filter reaches its real card: '+intent,await page.locator('.card-identity').allTextContents().then(ids=>ids.includes(id)));
    }
    await page.locator('#cards-reset').click();await page.locator('#cards-layer').selectOption('disk-published');
    for(const card of catalog.cards.filter(c=>c.record.sourceLayer==='disk-published')){
      await page.locator('[data-open-card]').filter({hasText:card.record.title}).first().waitFor();
      await page.locator('[data-open-card]').evaluateAll((els,key)=>els.find(e=>e.dataset.openCard===key).click(),card.key);
      check('Exact formal detail: '+card.record.stableId,await page.locator('#card-full-description').textContent()===(card.generated.description||card.storedDescription||'尚未配置可显示的说明。'));
      await page.locator('#card-detail-close').click();
    }
    const delayed=catalog.cards.find(c=>c.record.stableId==='bc_n1xa3efw08'&&c.state==='published');
    await page.locator('[data-open-card]').evaluateAll((els,key)=>els.find(e=>e.dataset.openCard===key).click(),delayed.key);
    check('Unbound cost is not fabricated',await page.locator('#card-preview-face .face-cost').textContent()==='—');
    await page.locator('#card-preview-role').selectOption('char.000008');
    const action=catalog.actions.find(a=>a.actionId===301);await page.locator('[data-preview-slot]').first().selectOption(action.id);
    const previewResponse=page.waitForResponse(r=>r.url().includes('/api/tactical-card/preview?')&&r.status()===200);
    await page.locator('#card-preview-apply').click();const previewData=await(await previewResponse).json();await page.waitForFunction(()=>document.querySelector('#card-preview-status').textContent.startsWith('临时预览已更新'));
    check('Shared current localized description matches verbatim',await page.locator('#card-full-description').textContent()===previewData.generated.description);
    check('Full shared description preserves line breaks and indentation',await page.locator('#card-full-description').evaluate(e=>getComputedStyle(e).whiteSpace)==='pre-wrap');
    check('Card summary preserves shared line breaks and indentation',await page.locator('#card-preview-face .face-description').evaluate(e=>getComputedStyle(e).whiteSpace)==='pre-wrap');
    check('Parameters show actual movement DTO',await page.locator('.parameter-panel').allTextContents().then(rows=>rows.some(r=>r.includes('×1'))));
    await page.screenshot({path:path.join(out,'bound-preview.png')});
    await page.locator('#card-detail summary').filter({hasText:'完整生成说明、来源与版本'}).click();
    const [popup]=await Promise.all([page.waitForEvent('popup'),page.locator('#card-detail a').filter({hasText:'打开真实源文件'}).click()]);
    await popup.locator('#source-code span').first().waitFor();check('Formal source viewer displays current file',await popup.locator('#source-title').textContent().then(t=>t.includes(delayed.record.stableId)));
    await popup.close();
    await page.locator('#card-preview-clear').click();await page.waitForFunction(()=>document.querySelector('#card-preview-status').textContent.startsWith('临时预览已更新'));
    check('Clearing bindings removes selected action name',!(await page.locator('#card-full-description').textContent()).includes('Light Attack'));
    await page.keyboard.press('Escape');check('Dialog closes with Escape',!(await page.locator('#card-detail').getAttribute('open')));
    await page.locator('#cards-reset').click();await page.locator('#cards-search').fill('后撤');
    const count=await page.locator('.card-entry').count();await page.reload();await page.locator('.card-entry').first().waitFor();
    check('Refresh restores filter and count',await page.locator('#cards-search').inputValue()==='后撤'&&await page.locator('.card-entry').count()===count);
    await page.locator('#cards-reset').click();await page.locator('#intent-tab').click();check('Existing intent tab selection retained',await page.locator('#intent-tab').getAttribute('aria-selected')==='true');
    await page.locator('#cards-tab').click();check('Return to complete card directory',await page.locator('.card-entry').count()===catalog.counts.publishedVersions+catalog.counts.authorDrafts);
    await page.setViewportSize({width:390,height:844});await page.screenshot({path:path.join(out,'mobile.png'),fullPage:true});
    check('Narrow layout has no horizontal document overflow',await page.evaluate(()=>document.documentElement.scrollWidth<=innerWidth+1));
    await page.locator('.card-open').first().click();check('Narrow detail has no horizontal overflow',await page.locator('#card-detail').evaluate(e=>e.scrollWidth<=e.clientWidth+1));
    await page.screenshot({path:path.join(out,'mobile-detail.png'),fullPage:true});
    check('No JavaScript exceptions',errors.length===0);
    check('No mutating browser requests',writes.length===0);
    fs.writeFileSync(path.join(out,'verification.json'),JSON.stringify({browser:process.env.BROWSER_PATH||'local Playwright Chromium',origin,counts:catalog.counts,passed:checks.length,checks,errors,writes,playerSavesRead:false,publicationsSubmitted:0},null,2));
  }catch(error){await page.screenshot({path:path.join(out,'failure.png'),fullPage:true});fs.writeFileSync(path.join(out,'failure.json'),JSON.stringify({checks,errors,error:error.stack,url:page.url()},null,2));throw error;}
  finally{await browser.close();}
})().catch(e=>{console.error(e);process.exit(1)});
