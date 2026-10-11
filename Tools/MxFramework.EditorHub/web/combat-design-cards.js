"use strict";
(() => {
  const mount=document.getElementById("cards-view"), esc=value=>Hub.escapeHtml(String(value??""));
  const stateNames={published:"正式 · 当前版",historical:"正式 · 历史版","draft-synced":"草稿 · 已发布检查点","draft-changed":"草稿 · 待核对改动",unpublished:"草稿 · 未发布","missing-source":"草稿 · 来源正式版缺失",configured:"配置测试副本",invalid:"来源失效"};
  const kindNames={Action:"行动卡",Utility:"功能卡",Unknown:"类型未确认"};
  const payloadNames={AddMomentum:"增加动势",ApplyStatus:"施加状态",TestPreparation:"测试准备",InteractionPreparation:"交互准备",Unknown:"子类型未确认"};
  function cardType(card){const t=card.cardType;if(!t||!['Action','Utility'].includes(t.kind))return{kind:'Unknown',utilityPayloadKind:null,status:'unavailable',message:t?.message||'服务未提供受支持的类型投影，请升级 EditorHub 后刷新。'};return{...t,utilityPayloadKind:t.kind==='Utility'?(t.utilityPayloadKind||'Unknown'):null};}
  function typeLabel(card){const t=cardType(card);return kindNames[t.kind]+(t.kind==='Utility'?' · '+(payloadNames[t.utilityPayloadKind]||t.utilityPayloadKind):'');}
  function typeIcon(card){const t=cardType(card);if(t.kind==='Action')return '<img src="'+art+'standard_common/card_type_action_32x32.png" alt="">';if(t.kind==='Utility')return '<svg viewBox="0 0 24 24" aria-hidden="true"><path d="M12 2 22 12 12 22 2 12Z M8 12h8 M12 8v8"/></svg>';return '<span aria-hidden="true">?</span>';}
  const layerNames={"disk-published":"正式模板","disk-author":"作者草稿","disk-configured":"配置测试副本"};
  const art="/FGUIProject/assets/MxCharacterTestRuntimeAiLabUi/Images/MxUiFoundation/";
  let catalog=null,loading=false,loadSerial=0,previewSerial=0,selected=null,preview=null,returnFocus=null;
  const $=id=>document.getElementById(id), urlParams=()=>new URL(location.href).searchParams;
  mount.innerHTML=`<div class="cards-heading"><div><p class="eyebrow">TACTICAL CARDS</p><h1>战术卡目录</h1><p>查看当前正式模板与作者草稿。卡面沿用游戏资源，说明与参数由游戏的同一投影生成。</p></div><button id="cards-refresh" class="quiet-button">刷新目录</button></div>
    <div id="cards-totals" class="cards-totals"></div><p id="cards-status" role="status" aria-live="polite">正在读取当前卡目录…</p><div id="cards-notices"></div>
    <div class="cards-filters"><label>名称、描述或身份<input id="cards-search" type="search" placeholder="例如：后撤、等待、卡 ID"></label><label>来源<select id="cards-layer"><option value="templates">正式模板与作者草稿</option><option value="all">全部来源</option><option value="disk-published">仅正式模板</option><option value="disk-author">仅作者草稿</option><option value="disk-configured">仅配置测试副本</option></select></label><label>卡牌类型<select id="cards-kind"><option value="">全部类型</option>${Object.entries(kindNames).map(([v,t])=>`<option value="${v}">${t}</option>`).join("")}</select></label><label>功能子类型<select id="cards-payload"><option value="">全部功能子类型</option>${Object.entries(payloadNames).map(([v,t])=>`<option value="${v}">${t}</option>`).join("")}</select></label><label>发布状态<select id="cards-state"><option value="">全部状态</option>${Object.entries(stateNames).map(([v,t])=>`<option value="${v}">${t}</option>`).join("")}</select></label><label>机制<select id="cards-mechanism"><option value="">全部机制</option></select></label><label>意图组件<select id="cards-intent"><option value="">全部组件</option></select></label><button id="cards-reset" class="quiet-button">清除筛选</button></div>
    <div id="cards-grid" class="cards-grid" aria-label="战术卡"><p class="cards-loading">读取正式索引、工作区索引和共享说明…</p></div>
    <dialog id="card-detail" aria-labelledby="card-detail-title"><div class="card-detail-top"><h2 id="card-detail-title"></h2><button id="card-detail-close" class="quiet-button">关闭</button></div><div id="card-detail-body" class="card-detail-body"></div></dialog>`;
  const controls={q:$("cards-search"),layer:$("cards-layer"),kind:$("cards-kind"),payload:$("cards-payload"),state:$("cards-state"),mechanism:$("cards-mechanism"),intent:$("cards-intent")};
  function navigate(changes){const url=new URL(location.href);for(const[k,v]of Object.entries(changes)){if(v)url.searchParams.set(k,v);else url.searchParams.delete(k);}history.replaceState(null,"",url);}
  function filtersFromUrl(){const p=urlParams();for(const[k,el]of Object.entries(controls)){const v=p.get(`cards-${k}`)??(k==="layer"?"templates":"");el.value=v;if(el.tagName==="SELECT"&&el.selectedIndex<0)el.value=k==="layer"?"templates":"";}}
  function storeFilters(){for(const[k,el]of Object.entries(controls))navigate({[`cards-${k}`]:el.value===(k==="layer"?"templates":"")?"":el.value});render();}
  function usableText(value){return typeof value==='string'&&/[\p{L}\p{N}]/u.test(value);}
  function generated(card){return usableText(card.generated?.description)?card.generated:null;}
  function description(card){return generated(card)?.description||(usableText(card.storedDescription)?card.storedDescription:"尚未配置可显示的说明。");}
  function face(card,projection){
    const current=usableText(projection?.description)?projection:generated(card),text=current?.description||description(card);
    const bound=card.slots.length>0,cost=bound||card.templateCost==null?"—":card.templateCost;
    const utility=current?.utilityFace;
    if(cardType(card).kind==='Utility'&&utility?.schema==='mx.tactical-card.utility-face.v1')return `<span class="tactical-face utility-face" data-style-id="${esc(card.record.stableId)}" data-utility-illustration="${esc(utility.illustration)}"><canvas class="face-shell" aria-hidden="true"></canvas><span class="face-cost">${esc(cost)}</span><span class="face-title" title="${esc(card.record.title)}">${esc(card.record.title)}</span><img class="face-art" alt=""><span class="face-description">${esc(utility.cardText||text)}</span></span>`;
    return `<span class="tactical-face" data-style-id="${esc(card.record.stableId||(card.record.baseKey?.startsWith('published:')?card.record.baseKey.slice(10):''))}"><canvas class="face-shell" aria-hidden="true"></canvas><span class="face-cost" title="${bound?"模板费用待真实动作装配；基础费用见详情":"模板基础费用"}">${esc(cost)}</span><span class="face-title" title="${esc(card.record.title)}">${esc(card.record.title||"未命名卡")}</span><img class="face-art" src="./assets/tactical-card-fallback.svg" alt=""><span class="face-type" data-card-kind="${esc(cardType(card).kind)}" title="${esc(typeLabel(card))}">${typeIcon(card)}<span>${esc(typeLabel(card))}</span></span><span class="face-description ${current?"":"face-empty"}">${esc(text)}</span><span class="face-basis">${bound?"动作与费用待真实装配":"模板展示"}${current?"":" · 静态说明"}</span></span>`;
  }
  function badges(card){return `<span class="card-badge card-type-badge" data-card-kind="${esc(cardType(card).kind)}">${esc(typeLabel(card))}</span>${cardType(card).status==="source-preview"?'<span class="card-badge">来源类型预览</span>':""}<span class="card-badge" data-state="${esc(card.state)}">${esc(stateNames[card.state]||card.state)}</span><span class="card-badge">r${esc(card.record.revision)}</span>`;}
  function fitNames(){mount.querySelectorAll(".face-title").forEach(el=>{el.style.fontSize="";let size=parseFloat(getComputedStyle(el).fontSize);for(let i=0;i<10&&el.scrollWidth>el.clientWidth&&size>12;i++){size-=1;el.style.fontSize=`${size}px`;}});}
  function render(){
    if(!catalog)return;
    const f=Object.fromEntries(Object.entries(controls).map(([k,e])=>[k,e.value])),q=f.q.trim().toLocaleLowerCase();
    const cards=catalog.cards.filter(c=>(f.layer==="all"||f.layer==="templates"?f.layer==="all"||c.record.sourceLayer!=="disk-configured":c.record.sourceLayer===f.layer)
      &&(!f.kind||cardType(c).kind===f.kind)&&(!f.payload||cardType(c).kind==='Utility'&&cardType(c).utilityPayloadKind===f.payload)
      &&(!f.state||c.state===f.state)&&(!f.mechanism||c.mechanisms.includes(f.mechanism))&&(!f.intent||c.intents.some(i=>i.key===f.intent))
      &&(!q||[c.record.title,description(c),typeLabel(c),c.record.stableId,c.record.recordId,...c.intents.map(i=>i.name),...c.mechanisms].join(" ").toLocaleLowerCase().includes(q)));
    $("cards-status").textContent=`显示 ${cards.length} / ${catalog.counts.total} 条来源记录 · 正式 ${catalog.counts.published} 张 · 作者草稿 ${catalog.counts.authorDrafts} 份 · 测试副本 ${catalog.counts.configured} 份。`;
    $("cards-grid").innerHTML=cards.map(c=>`<article class="card-entry" data-card-key="${esc(c.key)}"><button class="card-open" data-open-card="${esc(c.key)}" aria-label="查看 ${esc(c.record.title)} · ${esc(typeLabel(c))} · ${esc(layerNames[c.record.sourceLayer])} · r${c.record.revision}">${face(c)}</button><div class="card-caption"><div class="card-badges">${badges(c)}</div><span>${esc(c.record.sourceLayer==="disk-author"?`${c.workspaceRole==="main"?"Main":"工作区"} · ${c.record.workspaceName||c.record.workspaceId}`:layerNames[c.record.sourceLayer])}</span><span class="card-identity">${esc(c.record.stableId||c.record.recordId)}</span>${c.state==="invalid"||c.state==="missing-source"?'<span class="cards-load-error">保留该条目，查看来源诊断</span>':""}</div></article>`).join("")||'<div class="cards-empty"><h2>没有符合筛选的卡</h2><p>可清除筛选，或刷新重新读取当前目录。</p><button class="quiet-button" data-clear-cards>清除筛选</button></div>';
    requestAnimationFrame(fitNames);
  }
  function options(){
    const payloads=[...new Set(catalog.cards.map(c=>cardType(c)).filter(t=>t.kind==='Utility').map(t=>t.utilityPayloadKind))];
    const requested=urlParams().get('cards-payload');if(requested&&!payloads.includes(requested))payloads.push(requested);
    for(const payload of payloads)if(![...controls.payload.options].some(o=>o.value===payload))controls.payload.add(new Option(payload,payload));
    const mechanisms=[...new Set(catalog.cards.flatMap(c=>c.mechanisms))].sort(),intents=[...new Map(catalog.cards.flatMap(c=>c.intents).map(i=>[i.key,i])).values()];
    $("cards-mechanism").innerHTML='<option value="">全部机制</option>'+mechanisms.map(m=>`<option value="${esc(m)}">${esc(m)}</option>`).join("");
    $("cards-intent").innerHTML='<option value="">全部组件</option>'+intents.map(i=>`<option value="${esc(i.key)}">${esc(i.name)}</option>`).join("");
  }
  async function load(){
    const id=++loadSerial;loading=true;$("cards-refresh").disabled=true;$("cards-status").textContent="正在重新读取索引，并由游戏投影生成卡面说明…";
    try{
      const response=await fetch('/api/tactical-card/catalog?locale=zh-CN',{cache:'no-store'});if(!response.ok)throw Error(`目录接口 ${response.status}，请检查 EditorHub 服务版本。`);
      const data=await response.json();if(data.schema!=="mx.tactical-card.directory.v1"||!Array.isArray(data.cards))throw Error("卡目录协议不匹配。");if(id!==loadSerial)return;
      catalog=data;options();filtersFromUrl();render();
      $("cards-totals").innerHTML=`<span><strong>${data.counts.published}</strong>正式卡</span><span><strong>${data.counts.authorDrafts}</strong>作者草稿</span><span><strong>${data.counts.configured}</strong>测试副本</span>`;
      const unknownTypes=data.cards.filter(c=>cardType(c).kind==='Unknown').length,missingTypeContract=data.cards.some(c=>!c.cardType);
      const stale=data.cards.some(c=>c.generated?.status==="generated-assembly-may-be-stale"),failed=data.cards.filter(c=>!generated(c)).length;
      $("cards-notices").innerHTML=`<p class="cards-notice">正式模板、草稿与配置测试副本分别展示。同名不合并，版本与身份保留；这里不代表玩家当前持有实例。</p>${unknownTypes?`<p class="cards-notice warn">${unknownTypes} 条记录的卡牌类型未确认；仍保留在目录中。${missingTypeContract?"当前服务缺少类型投影，请由服务 owner 升级 EditorHub 后刷新。":"来源缺失或运行时解析不支持时，不推断为行动卡。"}</p>`:""}${stale?'<p class="cards-notice warn">部分描述源码比当前编译程序集新。已使用当前程序集与最新本地化表生成说明；请待 Unity 正常编译后刷新核对。</p>':""}${failed?`<p class="cards-notice warn">${failed} 条记录暂不能生成同源卡面说明，保留静态说明及来源诊断。</p>`:""}${data.diagnostics.length?`<details class="card-source-details"><summary>目录读取诊断 · ${data.diagnostics.length}</summary><pre>${esc(data.diagnostics.join('\n'))}</pre></details>`:""}`;
      const key=urlParams().get('card');if(key&&location.hash==="#cards")open(key,false);
    }catch(error){if(id!==loadSerial)return;$("cards-status").textContent=`读取失败：${error.message}`;$("cards-notices").innerHTML='<p class="cards-notice warn">目录读取未完成。已有结果仅作此前读取的参考，请点击刷新重试。</p>';if(!catalog)$("cards-grid").innerHTML='<div class="cards-empty">卡目录暂时不可用。可点击上方“刷新目录”重试。</div>';}
    finally{if(id===loadSerial){loading=false;$("cards-refresh").disabled=false;}}
  }
  const icons={move:'<path d="M7 3c-3 0-3 8 0 8s3-8 0-8zm0 11v3m10-8c-3 0-3 8 0 8s3-8 0-8zm0 11v2"/>',turn:'<path d="M5 8a8 8 0 1 1-1 8M5 3v5h5"/>',time:'<circle cx="12" cy="12" r="9"/><path d="M12 5v7l5 2"/>',range:'<path d="M3 4v16M21 4v16M3 12h18m-4-4 4 4-4 4M7 8l-4 4 4 4"/>',facing:'<path d="M12 20 4 4h16zM12 20V9"/>'};
  function field(label,value,icon){return `<div class="parameter-value" title="${esc(label)}"><svg viewBox="0 0 24 24" aria-hidden="true">${icons[icon]}</svg><div><span>${esc(value??"—")}</span><small>${esc(label)}</small></div></div>`;}
  function parameters(data){
    const panels=[];
    for(const segment of data?.parameters||[]){
      for(const kind of ['Action','Arrival','Retreat','Orbit','KeepDistance','FacingStep','WaitOpportunity','FacingTarget']){
        const hint=segment[kind];if(!hint)continue;
        const movement=hint.Movement,fields=[];
        if(movement){fields.push(field('移动倍率',movement.MovementText,'move'));fields.push(field('转向倍率',movement.RotationText,'turn'));}
        if(hint.RangeText!=null)fields.push(field(kind==='Action'?'攻击距离':'期望距离',hint.RangeText,'range'));
        if(hint.FacingText!=null)fields.push(field('朝向容差',hint.FacingText,'facing'));
        if(hint.DurationText!=null)fields.push(field('持续时间',hint.DurationText,'time'));
        const title={Action:'出招参数',Arrival:hint.IsRetreat?'后撤至期望距离':'接近参数',Retreat:'后撤参数',Orbit:'环绕参数',KeepDistance:'守距参数',FacingStep:'垫步参数',WaitOpportunity:'等待参数',FacingTarget:'朝向参数'}[kind];
        panels.push(`<section class="parameter-panel" data-parameter-kind="${kind}"><h4>${title}</h4><div class="parameter-values">${fields.join('')||'<span>当前源无数值参数</span>'}</div><p>${esc(segment.text)}</p></section>`);
      }
    }
    return panels.length?`<div class="parameter-panels">${panels.join('')}</div>`:'<p class="cards-notice">此来源没有可用的结构化参数组件。完整说明和诊断保留在下方。</p>';
  }
  function currentBindings(){return Object.fromEntries([...document.querySelectorAll('#card-detail [data-preview-slot]')].map(s=>[s.dataset.previewSlot,s.value]));}
  function showProjection(data){
    if(!selected)return;preview=data;
    $("card-preview-face").innerHTML=face(selected,data);
    $("card-full-description").textContent=usableText(data?.description)?data.description:description(selected);
    $("card-parameters").innerHTML=parameters(data);
    $("card-generated-details").textContent=data?.descriptionDetails||selected.sourceIssue||'没有生成说明。';
    $("card-projection-evidence").textContent=JSON.stringify({status:data?.status,measured:data?.measured,executable:data?.executable,locale:data?.locale,moduleId:data?.moduleId,fingerprint:data?.fingerprint,sourceFingerprint:selected.record.sourceFingerprint,diagnostics:data?.diagnostics||[data?.message].filter(Boolean)},null,2);
    requestAnimationFrame(fitNames);
  }
  function open(key,update=true){
    const card=catalog?.cards.find(c=>c.key===key);if(!card){if(update)navigate({card:''});return;}
    selected=card;previewSerial++;returnFocus=document.querySelector(`[data-open-card="${CSS.escape(key)}"]`);if(update)navigate({card:key});
    $("card-detail-title").textContent=card.record.title||'未命名卡';
    const actions=[...new Map(catalog.actions.map(a=>[`${a.id}:${a.name}`,a])).values()];
    const choices=(slot)=>'<option value="">未装配</option>'+actions.filter(a=>slot.resolver!=='ordinaryGuard'||a.category==='Guard').map(a=>`<option value="${esc(a.id)}">${esc(a.name)} · ${a.actionId}</option>`).join('');
    $("card-detail-body").innerHTML=`<div class="card-detail-visual"><div id="card-preview-face"></div><div class="card-badges" style="margin-top:12px">${badges(card)}</div><p class="cards-notice">${esc(layerNames[card.record.sourceLayer])}。卡面保留四行摘要，右侧完整说明可阅读长文本。</p></div><div class="card-detail-data">
      <p class="eyebrow">${esc(layerNames[card.record.sourceLayer])} · ${esc(card.record.stableId||card.record.recordId)}</p><p class="card-full-description" id="card-full-description"></p>
      <div class="card-facts"><p>卡牌类型<b>${esc(typeLabel(card))}</b></p><p>类型依据<b>${esc(cardType(card).status==='published-source'?'正式来源 · 游戏运行时解析':cardType(card).status==='source-preview'?'来源类型预览 · 非正式发布结论':'类型未确认')}</b></p><p>模板基础费用<b>${esc(card.templateCost??'未配置')}</b></p><p>实际费用<b>${card.slots.length?'待真实角色与动作装配':'模板基础值；使用时规则另检'}</b></p><p>来源版本<b>${card.record.sourceLayer==='disk-published'?`正式 r${card.record.revision}`:card.sourcePublishedRevision?`正式 r${card.sourcePublishedRevision}`:'无正式来源版本'}</b></p><p>来源身份<b>${esc(card.record.workspaceId||card.record.recordId||card.record.stableId)}</b></p></div>
      <h3>角色与动作预览</h3><p class="cards-notice">只改变本页说明，刷新后恢复来源配置。未读取玩家持有，也不会准备测量；未知参数显示“—”。</p>
      <div class="card-binding-form"><label>角色上下文<select id="card-preview-role"><option value="">未指定角色 · 模板预览</option>${catalog.characters.map(c=>`<option value="${esc(c.id)}">${esc(c.name)} · ${esc(c.id)}</option>`).join('')}</select></label>${card.slots.map(s=>`<label>${esc(s.label||s.path)}<small>${esc(s.path)}</small><select data-preview-slot="${esc(s.path)}">${choices(s)}</select></label>`).join('')}</div>
      <div class="card-detail-controls"><button class="quiet-button" id="card-preview-apply">更新预览</button><button class="quiet-button" id="card-preview-clear">全部未装配</button><button class="quiet-button" id="card-description-copy">复制完整说明</button></div><p id="card-preview-status" role="status" aria-live="polite">${card.slots.length?'来源动作配置；实际可用性未验证。':'此模板没有可替换的动作槽。'}</p>
      <h3>游戏参数组件</h3><div id="card-parameters"></div><h3>意图组件</h3><div class="card-badges">${card.intents.map(i=>`<a class="card-badge" href="?entry=${encodeURIComponent(i.key)}#intent">${esc(i.name)}</a>`).join('')||'<span>现有组件解析器未分类；来源仍完整保留。</span>'}</div>
      <details class="card-source-details"><summary>完整生成说明、来源与版本</summary><p id="card-generated-details"></p>${card.sourceUrl?`<a target="_blank" rel="noopener" href="${esc(card.sourceUrl)}">打开真实源文件</a>`:''}<pre>${esc(JSON.stringify({key:card.key,source:card.record.sourcePath,version:card.record.revision,contentHash:card.record.contentHash,sourcePublishedContentHash:card.sourcePublishedContentHash,sourceIssue:card.sourceIssue,cardType:card.cardType,typeDiagnostic:cardType(card).message,storedDescription:card.storedDescription,bindings:card.record.bindings},null,2))}</pre>${card.record.sourceLayer==='disk-author'&&card.workspaceRole==='main'?`<a href="?authorScope=main&authorCard=${encodeURIComponent(card.record.recordId)}#authoring">进入已有卡制作入口</a>`:''}</details><details class="card-source-details"><summary>生成依据与未准备参数诊断</summary><pre id="card-projection-evidence"></pre></details></div>`;
    for(const el of document.querySelectorAll('#card-detail [data-preview-slot]')){
      const value=card.record.bindings[el.dataset.previewSlot];if(!value)continue;
      const match=actions.find(a=>a.id===value)||actions.find(a=>String(a.actionId)===String(value));
      if(match&&[...el.options].some(o=>o.value===match.id))el.value=match.id;
      else{el.add(new Option(`来源动作失效 / ${value}`,value));el.value=value;}
    }
    if(catalog.characters.some(c=>c.id===card.record.owner))$("card-preview-role").value=card.record.owner;
    showProjection(card.generated);$("card-preview-apply").onclick=applyPreview;
    $("card-preview-clear").onclick=()=>{document.querySelectorAll('#card-detail [data-preview-slot]').forEach(s=>s.value='');applyPreview();};
    $("card-description-copy").onclick=async()=>{try{await navigator.clipboard.writeText($("card-full-description").textContent);$("card-preview-status").textContent='已复制完整说明。';}catch{$("card-preview-status").textContent='浏览器未允许复制，可直接选择上方说明。';}};
    if(!$("card-detail").open)$("card-detail").showModal();
  }
  async function applyPreview(){
    if(!selected)return;const serial=++previewSerial,key=selected.key,button=$("card-preview-apply");button.disabled=true;$("card-preview-status").textContent='正在用游戏共享投影更新临时预览…';
    const q=new URLSearchParams({key,locale:'zh-CN',role:$("card-preview-role").value,bindings:JSON.stringify(currentBindings()),sourceFingerprint:selected.record.sourceFingerprint||''});
    try{const r=await fetch(`/api/tactical-card/preview?${q}`,{cache:'no-store'}),result=await r.json();if(!r.ok)throw Error(result.message||'预览失败');if(serial!==previewSerial||selected?.key!==key)return;
      if(!usableText(result.generated?.description))throw Error(result.generated?.message||'此来源暂不支持生成有效说明预览');showProjection(result.generated);$("card-preview-status").textContent='临时预览已更新；角色动作的测量参数与实际费用未准备。';
    }catch(error){if(serial===previewSerial)$("card-preview-status").textContent=`预览未更新：${error.message}。保留上一次说明。`;}
    finally{if(serial===previewSerial&&button.isConnected)button.disabled=false;}
  }
  function close(){previewSerial++;selected=null;preview=null;navigate({card:''});$("card-detail").close();returnFocus?.focus();}
  $("card-detail-close").onclick=close;$("card-detail").addEventListener('cancel',event=>{event.preventDefault();close();});
  $("cards-grid").onclick=event=>{const card=event.target.closest('[data-open-card]');if(card)open(card.dataset.openCard);if(event.target.closest('[data-clear-cards]'))reset();};
  function reset(){for(const[k,e]of Object.entries(controls))e.value=k==='layer'?'templates':'';storeFilters();}
  for(const[k,e]of Object.entries(controls))e.addEventListener(k==='q'?'input':'change',storeFilters);
  $("cards-reset").onclick=reset;$("cards-refresh").onclick=load;
  window.addEventListener('combat-design-route',()=>{if(location.hash==='#cards'){if(!catalog&&!loading)load();else{filtersFromUrl();render();const key=urlParams().get('card');if(key&&selected?.key!==key)open(key,false);}}else if($("card-detail").open)close();});
  window.addEventListener('resize',fitNames);filtersFromUrl();if(location.hash==='#cards'||!location.hash)load();
})();
