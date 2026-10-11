"use strict";
(() => {
  const view = document.getElementById("authoring-view"), esc = value => Hub.escapeHtml(String(value ?? ""));
  const storage = "combat-design-authoring-v1", pendingKey = `${storage}:pending`, state = { library:null, detail:null, plan:null, busy:false, ready:false, result:null, baseline:"", dirty:false };
  const query = new URL(location.href).searchParams;
  let scope = query.get("authorScope") || "main", loaded = false;
  if (!/^(main|experimentA|experimentB|fixture:[a-f0-9]{64})$/.test(scope)) scope = "main";
  view.innerHTML = `<p class="eyebrow">TACTICAL CARD AUTHORING</p><h1>卡制作与发布</h1><p class="authoring-note">选择模板，保存作者源，再预检并发布。发布生成一个新的正式版本；行为组和玩家持有保持原状。</p>
    <div class="authoring-toolbar"><label>作者工作区<select id="authoring-scope"><option value="main">Main · 正式作者</option><option value="experimentA">Experiment A</option><option value="experimentB">Experiment B</option>${scope.startsWith("fixture:") ? `<option value="${scope}">隔离验收 · 不写正式库</option>` : ""}</select></label><button id="authoring-refresh" class="quiet-button">刷新作者库</button><button id="authoring-resume" class="quiet-button" hidden>查询上次操作</button></div>
    <p id="authoring-connection" class="authoring-banner" role="status">正在检查 Unity 作者连接…</p><p id="authoring-message" class="authoring-status" role="status" aria-live="polite"></p><div id="authoring-result"></div>
    <div class="authoring-layout"><aside class="authoring-library"><label>查找卡<input id="authoring-search" type="search" placeholder="名称或来源版本"></label><div id="authoring-library">连接后读取已有作者库。</div></aside><section class="authoring-editor" id="authoring-editor"><p>选择一张卡开始编辑。</p></section></div>
    <dialog id="authoring-confirm"><h2>确认发布这个版本</h2><div id="authoring-confirm-body"></div><div class="authoring-toolbar"><button id="authoring-confirm-publish" class="quiet-button">确认发布</button><button id="authoring-confirm-cancel" class="quiet-button">取消，返回编辑</button></div></dialog>`;
  const $ = id => document.getElementById(id), json = value => JSON.stringify(value, null, 2);
  $("authoring-scope").value = scope;
  const note = (message, error=false) => { $("authoring-message").textContent = message || ""; $("authoring-message").classList.toggle("authoring-error", error); };
  const localKey = detail => `${storage}:${scope}:${detail.workspaceId}:${detail.cardId}`;
  const form = () => ({name:$("authoring-name").value,cost:Number($("authoring-cost").value),cooldown:Number($("authoring-cooldown").value),source:JSON.parse($("authoring-source").value)});
  const rawForm = () => ({name:$("authoring-name").value,cost:$("authoring-cost").value,cooldown:$("authoring-cooldown").value,sourceText:$("authoring-source").value});
  const config = () => ({ characterId:Number($("authoring-character").value), actions:Object.fromEntries([...view.querySelectorAll("[data-slot]")].filter(e=>e.value).map(e=>[e.dataset.slot,Number(e.value)])) });
  function persistLocal() {
    if (!state.detail) return;
    if(state.plan) $("authoring-result").innerHTML='<p class="authoring-status authoring-error">作者源或预检配置已变化，请重新预检。</p>';
    state.dirty = JSON.stringify(rawForm()) !== state.baseline; state.plan = null;
    localStorage.setItem(localKey(state.detail), json({form:rawForm(),configuration:config(),snapshotToken:state.detail.snapshotToken,dirty:state.dirty}));
    controls();
  }
  function controls() {
    view.querySelectorAll("button:not(#authoring-confirm-cancel), input, select, textarea").forEach(e => { e.disabled = state.busy; });
    if ($("authoring-save")) $("authoring-save").disabled = state.busy || !state.ready || !state.dirty;
    if ($("authoring-preflight")) $("authoring-preflight").disabled = state.busy || !state.ready || state.dirty;
    if ($("authoring-publish")) $("authoring-publish").disabled = state.busy || !state.ready || state.dirty || !state.plan?.canPublish;
    if ($("authoring-dirty")) $("authoring-dirty").textContent = state.dirty ? "有未保存编辑 · 已保留在本浏览器" : "作者源已保存";
    $("authoring-resume").hidden = !localStorage.getItem(pendingKey);
  }
  async function api(path, body) {
    const response = await fetch(`/api/combat-design/authoring${path}`, body ? {method:"POST",headers:{"Content-Type":"application/json"},body:JSON.stringify(body)} : {});
    const result = await response.json(); if (!response.ok) throw new Error(result.message || result.code || response.statusText); return result;
  }
  async function connection() {
    try { const info = await api(""); state.ready = info.ready; $("authoring-connection").textContent = info.message + (scope.startsWith("fixture:") ? " · 当前为隔离验收目录" : ""); }
    catch (error) { state.ready = false; $("authoring-connection").textContent = `作者服务不可用：${error.message}`; }
    controls();
  }
  async function finish(command, response) {
    for (let tries=0; response.status === "PendingEditor" && tries<25; tries++) {
      note("等待 Unity 作者处理…可以保留此页，刷新后继续查询同一请求。");
      await new Promise(resolve=>setTimeout(resolve,800)); response = await api(`/status?operationId=${encodeURIComponent(response.operationId)}`);
    }
    if (response.status === "PendingEditor") { note("Unity 尚未完成。点击“查询上次操作”继续，不会重复提交。",true); return null; }
    localStorage.removeItem(pendingKey);
    state.result = response; renderResult(response);
    if (response.status !== "Succeeded") { note(response.message || "操作未完成",true); return null; }
    note(response.message || "操作完成");
    return response;
  }
  async function command(operation, request, resume=false) {
    if (state.busy) return null;
    if (!resume && localStorage.getItem(pendingKey)) { note("存在尚未确认结果的请求，请先查询上次操作。",true); return null; }
    let payload = resume ? JSON.parse(localStorage.getItem(pendingKey)) : {operation,idempotencyKey:crypto.randomUUID(),request:{...request,scope}};
    if (!payload) return null;
    state.busy = true; controls();
    try {
      localStorage.setItem(pendingKey,JSON.stringify(payload));
      const response = await api("/submit",payload); return await finish(payload,response);
    } catch (error) {
      // A transport failure can happen after enqueue: retain the same key until its result is known.
      if (/OWNER_UNAVAILABLE|IDEMPOTENCY_CONFLICT|UNKNOWN_|INVALID_|JSON_/.test(error.message)) localStorage.removeItem(pendingKey);
      note(error.message,true); return null;
    } finally { state.busy = false; controls(); }
  }
  function renderResult(result) {
    const target = $("authoring-result");
    if (result.status === "RecoveryRequired") {
      target.innerHTML = `<div class="authoring-result authoring-error"><strong>发布恢复待处理</strong><p>${esc(result.message)}</p><p>阶段：${esc(result.publicationStage || "中断")}。请恢复原操作后再发布。</p><button id="authoring-recover" class="quiet-button">恢复发布结果与草稿检查点</button></div>`;
      $("authoring-recover").onclick = async()=> { const r = await command("reconcile",{cardId:result.cardId || state.detail?.cardId,recoveryOperationId:result.recoveryOperationId || result.operationId}); if(r?.detail) showDetail(r.detail); };
    } else if (result.published) {
      target.innerHTML = `<div class="authoring-result"><strong>${scope.startsWith("fixture:") ? "隔离发布完成" : "正式版本已发布"} · r${esc(result.revision)}</strong><p>${esc(result.message)}</p><p>内容 hash <code>${esc(result.contentHash)}</code><br>语义 hash <code>${esc(result.semanticHash)}</code></p></div>`;
    } else if (result.planToken) {
      target.innerHTML = `<div class="authoring-result"><strong>预检通过 · ${result.canPublish ? `将发布 r${esc(result.nextRevision)}` : "暂不可发布"}</strong><p>${esc(result.message)}</p><p>${esc(result.description)}</p><details><summary>预检依赖与发布目标</summary><pre>${esc(json({sourceHash:result.sourceHash,inputFingerprint:result.inputFingerprint,expiresUtc:result.expiresUtc,catalogPath:result.catalogPath}))}</pre></details></div>`;
    } else if (result.status === "Rejected") target.innerHTML = `<div class="authoring-result authoring-error"><strong>操作被拒绝，编辑内容已保留</strong><pre>${esc((result.diagnostics || [result.message]).join("\n"))}</pre></div>`;
    else target.innerHTML = "";
  }
  function renderLibrary() {
    const search = $("authoring-search").value.toLocaleLowerCase();
    const cards = state.library?.cards.filter(c=>`${c.name} ${c.revision}`.toLocaleLowerCase().includes(search)) || [];
    $("authoring-library").innerHTML = cards.map(c=>`<button class="quiet-button" data-card-key="${esc(c.key)}" aria-pressed="${c.key === `draft:${state.detail?.cardId}`}"><strong>${esc(c.name)}</strong><small>${esc(c.revision)}</small>${!c.draft?"<small>建立编辑草稿后修改</small>":""}</button>`).join("") || "没有匹配的卡。";
  }
  async function refresh() {
    state.plan=null;
    await connection(); if(!state.ready) return;
    const library = await command("refresh",{}); if(!library) return;
    state.library=library; renderLibrary(); loaded=true;
    const selected = new URL(location.href).searchParams.get("authorCard");
    if (!state.detail && selected && library.cards.some(c=>c.key===`draft:${selected}`)) await openCard(`draft:${selected}`);
  }
  async function openCard(key) {
    if (state.dirty && !confirm("当前编辑已保留在本浏览器。仍要切换卡片吗？选择取消可继续编辑。")) return;
    const createDraft = key.startsWith("published:");
    if(createDraft && !confirm("为这个正式模板建立 Main/当前工作区编辑草稿？正式版本不会改变。")) return;
    const detail = await command("open",{key,createDraft,snapshotToken:state.library.snapshotToken});
    if(detail) { state.library.snapshotToken=detail.snapshotToken; showDetail(detail); renderLibrary(); }
  }
  function showDetail(detail) {
    state.detail=detail; state.plan=null; state.dirty=false;
    if(state.library) {
      state.library.snapshotToken=detail.snapshotToken;
      const row=state.library.cards.find(c=>c.key===`draft:${detail.cardId}`||c.key===`published:${detail.stableId}`);
      if(row) Object.assign(row,{key:`draft:${detail.cardId}`,name:detail.name,draft:true,revision:`草稿 r${detail.revision} · ${detail.publishedRevision?`正式 r${detail.publishedRevision}`:"未发布"}`});
      renderLibrary();
    }
    const url=new URL(location.href); url.searchParams.set("authorScope",scope); url.searchParams.set("authorCard",detail.cardId); history.replaceState(null,"",url);
    const options=(items,value)=>items.map(i=>`<option value="${esc(i.id)}" ${i.id===value?"selected":""}>${esc(i.name)} · ${esc(i.id)}</option>`).join("");
    $("authoring-editor").innerHTML=`<p class="authoring-note">${esc(detail.role)} · 草稿 r${esc(detail.revision)} · 正式来源 ${detail.publishedRevision?`r${detail.publishedRevision}`:"未发布"}</p><h2>${esc(detail.name)}</h2>
      <div class="authoring-form"><label>卡名称<input id="authoring-name" maxlength="512" value="${esc(detail.name)}"></label><label>费用<input id="authoring-cost" type="number" min="0" max="10" value="${detail.cost}"></label><label>冷却（秒）<input id="authoring-cooldown" type="number" min="0" step="0.1" value="${detail.cooldown}"></label></div>
      <p id="authoring-dirty" class="authoring-note"></p><div id="authoring-structure"></div><details><summary>作者源 JSON · 完整编辑</summary><label>作者源<textarea id="authoring-source" spellcheck="false">${esc(json(detail.source))}</textarea></label><p class="authoring-note">按现有 schema 保存，由 Unity 严格校验。共享源在下方查看，修改共享源请使用原作者入口。</p></details>
      <div class="authoring-toolbar"><button id="authoring-save" class="quiet-button">保存作者源</button><button id="authoring-reset" class="quiet-button">撤销本地编辑</button><button id="authoring-reload" class="quiet-button">重新加载这张卡</button><button id="authoring-copy" class="quiet-button">导出本地编辑</button></div>
      <h3>预检配置</h3><p class="authoring-note">仅用于验证模板的真实装配；不会改变角色装备或玩家持有。动作摘要缺失时，请先在原摘要工具中准备。</p>
      <label>预检角色<select id="authoring-character"><option value="">请选择角色</option>${options(state.library?.characters || [],0)}${scope.startsWith("fixture:") ? '<option value="1">隔离无动作模板 · 角色 1</option>' : ""}</select></label>
      ${detail.slots.map(slot=>`<label>${esc(slot)}<select data-slot="${esc(slot)}"><option value="">请选择动作</option>${options(state.library?.actions||[],0)}</select></label>`).join("")}
      <div class="authoring-toolbar"><button id="authoring-preflight" class="quiet-button">预检当前保存版本</button><button id="authoring-publish" class="quiet-button" disabled>查看发布确认</button></div>
      <details><summary>共享依赖 · ${detail.shared.length} 项（只读）</summary>${detail.shared.map(d=>`<details><summary>${esc(d.name||d.id)} · r${d.revision}</summary><code>${esc(d.hash)}</code><pre>${esc(json(d.source))}</pre></details>`).join("")}</details>`;
    state.baseline=JSON.stringify(rawForm());
    if(detail.pendingPublication) renderResult({status:"RecoveryRequired",message:"此前发布操作尚未完成草稿检查点。",publicationStage:detail.pendingPublication.stage,cardId:detail.cardId,recoveryOperationId:detail.pendingPublication.operationId});
    try {
      const saved=JSON.parse(localStorage.getItem(localKey(detail)) || "null");
      if(saved) {
        $("authoring-character").value=saved.configuration.characterId || "";
        view.querySelectorAll("[data-slot]").forEach(e=>{e.value=saved.configuration.actions[e.dataset.slot] || "";});
        if(saved.dirty) {
          $("authoring-name").value=saved.form.name; $("authoring-cost").value=saved.form.cost; $("authoring-cooldown").value=saved.form.cooldown; $("authoring-source").value=saved.form.sourceText;
          state.dirty=true;
          if(saved.snapshotToken!==detail.snapshotToken) { state.detail.snapshotToken=saved.snapshotToken; note("本地编辑的磁盘基线已变化。编辑已保留；请先导出，重新加载后再合并，旧编辑不能覆盖新源。",true); }
          else note("已恢复上次未保存的本地编辑。");
        }
      }
    }catch(error){note(`本地恢复失败：${error.message}`,true);}
    renderStructure();
    $("authoring-editor").oninput=persistLocal;
    $("authoring-editor").onchange=()=>{persistLocal();renderStructure();};
    $("authoring-save").onclick=async()=>{try{const r=await command("save",{...form(),cardId:detail.cardId,snapshotToken:state.detail.snapshotToken});if(r){const savedConfig=config();localStorage.removeItem(localKey(detail));showDetail(r);$("authoring-character").value=savedConfig.characterId;view.querySelectorAll("[data-slot]").forEach(e=>{e.value=savedConfig.actions[e.dataset.slot]||"";});persistLocal();}}catch(error){note(`作者源 JSON 无效：${error.message}`,true);}};
    $("authoring-reset").onclick=()=>{if(!state.dirty||confirm("撤销本浏览器尚未保存的编辑？磁盘作者源保持原样。")){localStorage.removeItem(localKey(detail));showDetail(detail);}};
    $("authoring-reload").onclick=async()=>{if(state.dirty&&!confirm("丢弃本地编辑并读取最新作者源？可先取消并导出。"))return;localStorage.removeItem(localKey(detail));state.dirty=false;state.detail=null;await refresh();};
    $("authoring-copy").onclick=()=>{const blob=new Blob([json({scope,cardId:detail.cardId,...rawForm(),configuration:config()})],{type:"application/json"});const a=document.createElement("a");a.href=URL.createObjectURL(blob);a.download="tactical-card-local-edit.json";a.click();URL.revokeObjectURL(a.href);};
    $("authoring-preflight").onclick=async()=>{state.plan=null;const r=await command("preflight",{cardId:detail.cardId,snapshotToken:state.detail.snapshotToken,configuration:config()});if(r){state.plan=r;controls();}};
    $("authoring-publish").onclick=()=>{if(!state.plan)return;$("authoring-confirm-body").innerHTML=`<p><strong>${esc(detail.name)}</strong> → r${state.plan.nextRevision}</p><p>${esc(state.plan.description)}</p><p>${scope.startsWith("fixture:")?"目标为隔离验收目录。":"目标为正式战术卡目录。"}这次只发布此卡版本。</p><p>确认后再次校验源、共享依赖、动作与目录版本。取消不会发布。</p>`;$("authoring-confirm").showModal();};
    controls();
  }
  function renderStructure() {
    try {
      const source=JSON.parse($("authoring-source").value), root=source.root, target=$("authoring-structure");
      if(!root?.children){target.innerHTML="<p class='authoring-note'>该卡使用事件源，可展开作者源 JSON 编辑其时间轴与参数。</p>";return;}
      target.innerHTML=`<h3>模板结构 · ${esc(root.kind)}</h3>${root.children.map((node,index)=>`<div class="authoring-node"><span>${esc(state.detail.shared.find(d=>d.id===node.definitionId)?.name || node.kind)}</span><button class="quiet-button" data-move="${index}:up" aria-label="上移第 ${index+1} 项">↑</button><button class="quiet-button" data-move="${index}:down" aria-label="下移第 ${index+1} 项">↓</button><button class="quiet-button" data-remove="${index}">移除</button></div>`).join("")}<label>添加现有共享源<select id="authoring-shared-choice">${state.detail.shared.map(d=>`<option value="${esc(d.id)}">${esc(d.name||d.id)}</option>`).join("")}</select></label><button id="authoring-add-node" class="quiet-button">添加到结构末尾</button>`;
      const changed=()=>{$("authoring-source").value=json(source);persistLocal();renderStructure();};
      target.querySelectorAll("[data-move]").forEach(button=>{button.onclick=()=>{const [index,direction]=button.dataset.move.split(":"),i=Number(index),j=i+(direction==="up"?-1:1);if(j<0||j>=root.children.length)return;[root.children[i],root.children[j]]=[root.children[j],root.children[i]];changed();};});
      target.querySelectorAll("[data-remove]").forEach(button=>{button.onclick=()=>{root.children.splice(Number(button.dataset.remove),1);changed();};});
      $("authoring-add-node").onclick=()=>{root.children.push({nodeId:`web-${crypto.randomUUID()}`,kind:"Reference",definitionId:$("authoring-shared-choice").value,children:[]});changed();};
    }catch{ $("authoring-structure").textContent="JSON 尚未有效，结构视图将在修正后恢复。"; }
  }
  $("authoring-confirm-cancel").onclick=()=>$("authoring-confirm").close();
  $("authoring-confirm-publish").onclick=async()=>{const plan=state.plan;$("authoring-confirm").close();if(!plan)return;state.plan=null;const r=await command("publish",{planToken:plan.planToken,confirmed:true});if(r?.detail){localStorage.removeItem(localKey(state.detail));showDetail(r.detail);}};
  $("authoring-search").oninput=renderLibrary;
  $("authoring-library").onclick=event=>{const button=event.target.closest("[data-card-key]");if(button)openCard(button.dataset.cardKey);};
  $("authoring-refresh").onclick=refresh;
  $("authoring-resume").onclick=async()=>{const r=await command(null,null,true);if(r?.detail)showDetail(r.detail);else if(r?.cardId&&r.source)showDetail(r);else if(r?.cards){state.library=r;renderLibrary();}};
  $("authoring-scope").onchange=async event=>{if(state.dirty&&!confirm("本地编辑已保留，仍要切换工作区吗？")){event.target.value=scope;return;}scope=event.target.value;state.detail=null;state.dirty=false;state.plan=null;$("authoring-editor").textContent="选择一张卡开始编辑。";const url=new URL(location.href);url.searchParams.set("authorScope",scope);url.searchParams.delete("authorCard");history.replaceState(null,"",url);await refresh();};
  window.addEventListener("beforeunload",event=>{if(state.dirty){event.preventDefault();event.returnValue="";}});
  window.addEventListener("combat-design-route",()=>{if(location.hash==="#authoring"&&!loaded&&!state.busy)refresh();});
  connection(); if(location.hash==="#authoring")refresh();
})();
