function renderEditor({ force = false } = {}) {
  // Async Cue/generation updates may finish after the user has switched to the
  // project-wide Pair workspace. They must not replace the matrix with the
  // previously selected Cue editor.
  if (state.workspaceMode === "pairs") return;
  if (!force && isCueFormEditing()) {
    state.editorRenderPending = true;
    return;
  }
  state.editorRenderPending = false;
  const cue = selectedCue();
  const eventDef = cue ? findEvent(cue.eventDefinitionId) : null;
  el.duplicateButton.disabled = !cue;
  el.archiveButton.disabled = !cue;
  el.deleteButton.disabled = !cue || cue.lifecycle !== "Archived" || referenceCount(cue.audioCueId) > 0;
  if (!cue) {
    el.editorTitle.textContent = "未选择 Cue";
    el.editorSubtitle.textContent = "从左侧选择或创建 Cue";
    el.cueForm.innerHTML = empty("Cue 编辑表单将在这里显示");
    return;
  }
  el.editorTitle.textContent = cue.displayName || cue.audioCueId;
  el.editorSubtitle.textContent = `${cue.audioCueId} · runtime ${cue.runtimeCueId} · ${eventDef?.fmodEventPath || "未绑定 Event"}`;
  el.archiveButton.textContent = cue.lifecycle === "Archived" ? "恢复为 Draft" : "归档";
  let activeView = currentWorkspaceTab(cue);
  if(activeView === "coverage") { activeView="basis"; state.workspaceTabs.set(cue.audioCueId,"basis"); }
  const candidates = state.generationCueId === cue.audioCueId ? state.generationJob?.candidates || [] : [];
  const historyCount = (state.generationManifest?.adoptionSets || []).filter(item => item.eventDefinitionId === cue.eventDefinitionId).length;
  const basis = currentBasisForCue(cue);
  const workspaceTabs = `<nav class="workspace-tabs" role="tablist" aria-label="Cue 工作区">${[["semantic","语义与 Event"],["recipe","配方"],["generate","生成"],["candidates",`候选与混合${candidates.length ? ` · ${candidates.length}` : ""}`],["history",`版本历史${historyCount ? ` · ${historyCount}` : ""}`],["basis",`基础音效${basis ? " · 已提升" : ""}`],["scenario","游戏情境预览"]].map(([id,label]) => `<button type="button" role="tab" aria-selected="${activeView === id}" class="${activeView === id ? "active" : ""}" data-workspace-tab="${id}">${label}</button>`).join("")}</nav>`;
  const options = (state.manifest.eventDefinitions || []).map(item => `<option value="${esc(item.id)}"${item.id === cue.eventDefinitionId ? " selected" : ""}>${esc(item.displayName || item.id)} · ${esc(item.fmodEventPath)}</option>`).join("");
  const semanticView = `
    <section class="section"><h3>语义 · 为什么播放</h3><div class="field-grid">
      ${field("显示名", "displayName", cue.displayName)}${field("Lifecycle", "lifecycle", cue.lifecycle, "select", ["Draft","Active","Archived"])}
      ${field("用途", "purpose", cue.purpose, "textarea", [], true)}${field("使用语境", "context", cue.context, "textarea", [], true)}
      ${field("标签（逗号分隔）", "tags", (cue.tags || []).join(", "), "text", [], true)}
    </div></section>
    <section class="section"><h3>Event Definition · 播放哪个 FMOD event</h3>
      <div class="event-binding"><label class="field"><span>绑定 Event</span><select name="eventDefinitionId">${options}</select></label><button type="button" data-action="refresh-snapshot">刷新 FMOD Snapshot</button></div>
      <div class="kv" style="margin-top:10px"><div><span>FMOD path</span><strong><code>${esc(eventDef?.fmodEventPath || "未绑定")}</code></strong></div><div><span>技术事实</span><strong>${eventDef ? `${eventDef.is3D ? "3D" : "2D"} · ${eventDef.isLoop ? "Loop" : "OneShot"} · ${eventDef.maxDistance || 0}m` : "—"}</strong></div></div>
      <details class="engineering"><summary>工程详情 · runtime ID / guid / 参数映射</summary><div class="kv" style="margin-top:9px">
        <div><span>runtime Cue ID</span><strong>${cue.runtimeCueId}</strong></div><div><span>runtime Event ID</span><strong>${eventDef?.runtimeEventId || "—"}</strong></div><div><span>FMOD guid</span><strong><code>${esc(eventDef?.fmodEventGuid || "—")}</code></strong></div><div><span>参数</span><strong>${esc((eventDef?.parameters || []).map(item => `${item.name}#${item.runtimeParameterId}`).join(", ") || "—")}</strong></div>
      </div></details>
    </section>
    <details class="section supporting-settings"><summary>播放策略与替换约束</summary><div class="field-grid supporting-grid">
      ${field("播放模式", "playMode", cue.playMode, "select", ["OneShot","StartEvent"])}${field("缺失策略", "missingPolicy", cue.missingPolicy, "select", ["Mute","FailRequest"])}${field("源试听文件", "sourcePreviewPath", cue.sourcePreviewPath || "", "text", [], true)}
      ${checkbox("保持 2D / 3D", "replacement.requireSameSpatial", cue.replacementContract?.requireSameSpatial !== false)}${checkbox("保持 Loop", "replacement.requireSameLoop", cue.replacementContract?.requireSameLoop !== false)}
      ${field("最短长度 ms", "replacement.minLengthMs", cue.replacementContract?.minLengthMs || 0, "number")}${field("最长长度 ms", "replacement.maxLengthMs", cue.replacementContract?.maxLengthMs || 0, "number")}
      ${field("必需参数（逗号分隔）", "replacement.requiredParameters", (cue.replacementContract?.requiredParameters || []).join(", "), "text", [], true)}
    </div></details>`;
  const generationView = ["recipe","generate","candidates"].includes(activeView) ? `<section class="section generation-workbench">${renderGenerationPanel(cue, eventDef, activeView)}</section>` : "";
  const historyView = activeView === "history" ? `<section class="section history-workspace"><div class="workspace-section-head"><div><p class="eyebrow">不可变音频版本</p><h3>音效版本历史</h3><p>试听并重新应用原始版本或任一历史版本。</p></div></div>${renderAdoptionHistory(cue, eventDef, true)}</section>` : "";
  const physicalView = activeView === "basis"
    ? renderPhysicalBasisWorkspace(cue, eventDef)
    : activeView === "scenario"
        ? renderPhysicalScenarioWorkspace(cue)
        : "";
  el.cueForm.innerHTML = `${workspaceTabs}<div class="workspace-view">${activeView === "semantic" ? semanticView : generationView || historyView || physicalView}</div>`;
  bindGenerationPanel();
  bindPhysicalPanel();
}

function renderPhysicalBasisWorkspace(cue, eventDef) {
  const basis = currentBasisForCue(cue);
  const activeSet = activeAdoptionSet(cue);
  const recipe = state.generationBriefs.get(cue.audioCueId);
  const families = state.physicalManifest?.families || [];
  const pair = basis && (state.physicalManifest?.materialPairs||[]).find(item=>sameId(item.id,basis.materialPairId));
  const materialA = pair && (state.physicalManifest?.materialProfiles||[]).find(item=>sameId(item.id,pair.materialAId));
  const materialB = pair && (state.physicalManifest?.materialProfiles||[]).find(item=>sameId(item.id,pair.materialBId));
  const canPromote = Boolean(activeSet && activeSet.status === "Applied" && !isDirty());
  const basisUsesCurrentSet = Boolean(basis && activeSet && sameId(basis.adoptionSetId,activeSet.setId));
  const identity = basis ? `<section class="section physical-identity-card">
      <div class="workspace-section-head"><div><p class="eyebrow">稳定运行时身份</p><h3>${esc(basis.displayName || basis.id)}</h3><p>${esc(basis.id)} · runtime #${esc(basis.runtimeBasisId)} · Cue ${esc(basis.audioCueId)}</p></div><div class="row-actions">${badge(basis.lifecycle || "Draft", basis.lifecycle === "Active" ? "ok" : "warn")}${basisUsesCurrentSet ? badge("素材版本当前","ok") : `<button type="button" data-physical-action="promote"${canPromote ? "" : " disabled"}>关联当前应用版本</button>`}</div></div>
      <div class="physical-identity-grid">
        <div class="physical-readonly"><span>公共材质 Pair</span><strong>${esc(materialA?.displayName||pair?.materialAId||"—")} × ${esc(materialB?.displayName||pair?.materialBId||"—")}</strong></div>
        <div class="physical-readonly"><span>动作 Variant</span><strong>${esc(basis.variantKind||"—")}</strong></div>
        <div class="physical-readonly"><span>Pair runtime</span><strong>${pair?`#${esc(pair.runtimeMaterialPairId)}`:"—"}</strong></div>
        <div class="physical-readonly"><span>当前素材版本</span><strong><code>${esc(basis.adoptionSetId || "—")}</code></strong></div>
      </div>
      <div class="physical-anchor-summary physical-basis-edit-summary"><article><span>Pair Side A</span><strong>${renderAcousticAnchorText(basis.sideA||basis.impactor)}</strong></article><article><span>Pair Side B</span><strong>${renderAcousticAnchorText(basis.sideB||basis.receiver)}</strong></article><article><span>适用范围</span><strong>${renderBasisRangeText(basis)}</strong></article></div>
      <div class="row-actions"><button type="button" class="primary" data-physical-action="locate-pair"${pair?"":" disabled"}>在公共矩阵中定位</button><button type="button" data-workspace-tab="scenario">用游戏事实模拟</button></div>
      <p class="notice ${basisUsesCurrentSet ? "" : "warn"}">${basisUsesCurrentSet ? "Basis ID 是稳定 gameplay 身份；重新应用历史音频只切换素材版本，不创建新的 Basis。" : "当前 FMOD 素材版本与 Basis 记录不一致；关联后旧试听记录会变为过期，但不影响同步。"}</p>
    </section>` : `<section class="section physical-promote-card">
      <div><p class="eyebrow">从素材到运行时</p><h3>这个 Cue 还不是基础音效</h3><p>先应用满意的素材版本，再把 Cue/Event 与双方物理描述提升为稳定 Basis Sound。</p></div>
      <div class="physical-promote-facts"><span>Event <strong>${esc(eventDef?.fmodEventPath || "未绑定")}</strong></span><span>素材版本 <strong>${activeSet ? esc(activeSet.setId) : "尚未应用"}</strong></span><span>物理配方 <strong>${recipe?.collisionProfile ? esc(collisionProfileSummary(recipe.collisionProfile)) : "尚未配置"}</strong></span></div>
      <button type="button" class="primary" data-physical-action="promote"${canPromote ? "" : " disabled"}>提升为基础音效</button>
      <p class="notice ${activeSet ? "" : "warn"}">${canPromote ? "提升后可配置覆盖范围，并通过游戏情境预览验证选择结果。" : isDirty() ? "请先保存当前 Cue / Recipe / Physical 改动。" : "需要先在候选页完成“保存并应用替换”。"}</p>
    </section>`;
  const library = (state.physicalManifest?.basisSounds || []).map(item => {
    const itemCue = findCue(item.audioCueId);
    const itemPair=(state.physicalManifest?.materialPairs||[]).find(candidate=>sameId(candidate.id,item.materialPairId));
    return `<button type="button" class="basis-library-row ${item.audioCueId===cue.audioCueId?"active":""}" data-basis-cue="${esc(item.audioCueId)}"><span><strong>${esc(item.displayName || itemCue?.displayName || item.id)}</strong><small>${esc(itemPair?.id||"未绑定 Pair")} · ${esc(item.variantKind||"Unknown")} · #${esc(item.runtimeBasisId)}</small></span><span>${badge(item.variantKind || "Unknown", "info")}${badge(item.lifecycle || "Draft", item.lifecycle === "Active" ? "ok" : "")}</span></button>`;
  }).join("");
  return `${identity}${renderManagedPhysicalEventCreator(cue,eventDef)}<details class="section basis-library"><summary>其他 Basis 引用 · ${state.physicalManifest?.basisSounds?.length || 0}</summary><div>${library || empty("尚无 Basis。可先新建一个受控 Basis / Cue / Event，再生成并应用素材。")}</div></details>`;
}

function renderPhysicalBasisAnchorEditor(side,label,anchor={}) {
  const materials=state.physicalManifest?.materialProfiles||[];
  const materialOptions=materials.map(item=>`<option value="${esc(item.id)}"${sameId(item.id,anchor.materialId)?" selected":""}>${esc(item.displayName||item.id)} · #${esc(item.runtimeMaterialId)}</option>`).join("");
  return `<article class="physical-basis-anchor-card"><header><span>${side==="impactor"?"A":"B"}</span><div><strong>${esc(label)}</strong><small>${side==="impactor"?"提供运动能量":"决定接触共振"}</small></div></header>
    <label class="field"><span>声学材质</span><select data-physical-basis-anchor="${side}" data-physical-basis-anchor-field="materialId">${materialOptions}</select></label>
    <div class="physical-basis-enum-grid"><label class="field"><span>表面</span><select data-physical-basis-anchor="${side}" data-physical-basis-anchor-field="surface">${["Any","Polished","Smooth","Normal","Rough","Granular"].map(value=>`<option${anchor.surface===value?" selected":""}>${value}</option>`).join("")}</select></label><label class="field"><span>构造</span><select data-physical-basis-anchor="${side}" data-physical-basis-anchor-field="construction">${["Any","Solid","Hollow","ThinShell","SoftFilled","Gas"].map(value=>`<option${anchor.construction===value?" selected":""}>${value}</option>`).join("")}</select></label></div>
    ${renderPhysicalBasisRangeEditor(`${side}.mass`,"质量",anchor.mass,0,1,.01,"")}
    ${renderPhysicalBasisRangeEditor(`${side}.contactArea`,"接触面积",anchor.contactArea,0,1,.01,"")}
  </article>`;
}

function renderPhysicalBasisRangeEditor(path,label,range={},min,max,step,unit) {
  const value=(name,fallback)=>numberValue(range?.[name],fallback);
  return `<fieldset class="physical-basis-range"><legend>${esc(label)}${unit?`（${esc(unit)}）`:""}</legend>${[["min","最小",min],["anchor","锚点",(min+max)/2],["max","最大",max]].map(([key,text,fallback])=>`<label><span>${text}</span><input type="number" min="${min}" max="${max}" step="${step}" value="${value(key,fallback)}" data-physical-basis-range="${esc(path)}.${key}"></label>`).join("")}</fieldset>`;
}

function renderManagedPhysicalEventCreator(cue,eventDef) {
  const draft=managedPhysicalDraftForCue(cue,eventDef),families=state.physicalManifest?.families||[],events=state.manifest?.eventDefinitions||[];
  const operation=state.managedPhysicalEvent,running=Boolean(operation&&!operation.terminal),blocked=isDirty()||running;
  const templates=managedPhysicalTemplateEvents(events,draft),templateOptions=[`<option value=""${draft.templateEventDefinitionId?"":" selected"} disabled>请选择 Snapshot 中的 OneShot 模板</option>`,...templates.map(item=>`<option value="${esc(item.id)}"${sameId(item.id,draft.templateEventDefinitionId)?" selected":""}>${esc(item.displayName||item.id)} · ${esc(item.fmodEventPath||"未绑定")}</option>`)].join("");
  const materialize=!draft.createBasis,targetPath=managedPhysicalEventPath(draft),actionBlocked=blocked||!draft.templateEventDefinitionId;
  return `<section class="section managed-physical-event"><div class="workspace-section-head"><div><p class="eyebrow">受控命名空间</p><h3>${materialize?"当前 Basis 的 FMOD Event":"新建基础音效"}</h3><p>${materialize?"创建或同步当前稳定 Basis 对应的受控 Event；不分配新的 Basis/Cue/Event 身份。":"一次创建稳定 Basis、Draft Cue 和 FMOD Event；只允许写入 MxFramework/Generated/Physical。"}</p></div>${badge("OneShot · 受控创建","info")}</div>
    <div data-managed-physical-status>${renderManagedPhysicalEventStatus(operation)}</div>
    <details${materialize||operation?.stage==="Failed"?" open":""}><summary>${materialize?"创建 / 同步当前受控 Event":"创建新的 Basis / Cue / Event"}</summary><div class="managed-physical-form">
      <label class="field"><span>显示名</span><input value="${esc(draft.displayName)}" data-managed-physical-field="displayName" maxlength="120"></label>
      ${materialize?`<div class="physical-readonly"><span>稳定 Basis</span><strong><code>${esc(draft.basisId)}</code></strong></div><div class="physical-readonly"><span>Physical Family</span><strong><code>${esc(draft.familyId)}</code></strong></div>`:`<label class="field"><span>稳定 Basis ID</span><input value="${esc(draft.basisId)}" data-managed-physical-field="basisId" spellcheck="false"></label><label class="field"><span>Physical Family</span><select data-managed-physical-field="familyId">${families.map(item=>`<option value="${esc(item.id)}"${sameId(item.id,draft.familyId)?" selected":""}>${esc(item.displayName||item.id)} · ${esc(item.interactionKind)}</option>`).join("")}</select></label>`}
      <label class="field"><span>FMOD 技术模板</span><select data-managed-physical-field="templateEventDefinitionId">${templateOptions}</select><small>仅复制 Bank、Bus、2D/3D 与距离；不会复制或修改模板素材。</small></label>
      <div class="managed-event-path"><span>目标 Event</span><code>${esc(targetPath)}</code></div>
      <div class="managed-event-actions"><button type="button" class="primary" data-physical-action="managed-create"${actionBlocked?" disabled":""}>${running?"正在创建…":materialize?"创建 / 同步当前受控 Event":"创建受控 Basis + Cue / Event"}</button><span>${isDirty()?"请先保存当前 Cue / Recipe / Physical 改动。":!draft.templateEventDefinitionId?"必须选择一个当前 Snapshot 中存在的 OneShot 模板。":materialize?"不会创建新 Basis；完成后素材仍为 Draft / 未采用，必须生成、应用并人工审听。":"创建会自动备份 FMOD，Build Banks，刷新 Unity Snapshot；失败完整回滚。"}</span></div>
    </div></details>
  </section>`;
}

function managedPhysicalDraftForCue(cue,eventDef) {
  let draft=state.managedPhysicalDrafts.get(cue.audioCueId);
  if(draft)return draft;
  const currentBasis=currentBasisForCue(cue),kind=physicalKindFromCue(cue),family=currentBasis&&(state.physicalManifest?.families||[]).find(item=>sameId(item.id,currentBasis.familyId))||(state.physicalManifest?.families||[]).find(item=>item.interactionKind===kind)||(state.physicalManifest?.families||[])[0];
  const currentTarget=currentBasis?`event:/MxFramework/Generated/Physical/${currentBasis.familyId}/${currentBasis.id}`:"";
  if(currentBasis&&sameId(eventDef?.fmodEventPath,currentTarget)){
    const seed={displayName:currentBasis.displayName||cue.displayName||currentBasis.id,basisId:currentBasis.id,familyId:currentBasis.familyId,templateEventDefinitionId:"",createBasis:false};
    seed.templateEventDefinitionId=managedPhysicalDefaultTemplateId(state.manifest?.eventDefinitions||[],seed,eventDef);
    state.managedPhysicalDrafts.set(cue.audioCueId,seed);
    return seed;
  }
  const leaf=String(cue.audioCueId||"basis").toLowerCase().replace(/[^a-z0-9._-]+/g,".").replace(/^\.+|\.+$/g,"")||"basis";
  let basisId=`audio.basis.generated.${leaf}`,suffix=2;
  while((state.physicalManifest?.basisSounds||[]).some(item=>sameId(item.id,basisId)))basisId=`audio.basis.generated.${leaf}.${suffix++}`;
  draft={displayName:`${cue.displayName||"New"} Basis`,basisId,familyId:family?.id||"",templateEventDefinitionId:"",createBasis:true};
  draft.templateEventDefinitionId=managedPhysicalDefaultTemplateId(state.manifest?.eventDefinitions||[],draft,eventDef);
  state.managedPhysicalDrafts.set(cue.audioCueId,draft);
  return draft;
}

function managedPhysicalEventPath(draft) { return `event:/MxFramework/Generated/Physical/${draft.familyId}/${draft.basisId}`; }

function managedPhysicalTemplateEvents(events,draft) {
  const target=managedPhysicalEventPath(draft),snapshotEvents=state.payload?.snapshot?.events||[];
  return (events||[]).filter(item=>{
    if(item.isLoop||sameId(item.fmodEventPath,target))return false;
    const observed=snapshotEvents.find(candidate=>sameId(candidate.path,item.fmodEventPath));
    return Boolean(observed&&!observed.isLoop&&observed.kind==="Event"&&(!item.fmodEventGuid||sameId(observed.guid,item.fmodEventGuid)));
  });
}

function managedPhysicalDefaultTemplateId(events,draft,eventDef) {
  const templates=managedPhysicalTemplateEvents(events,draft);
  if(eventDef&&templates.some(item=>sameId(item.id,eventDef.id)))return eventDef.id;
  return templates.find(item=>sameId(item.id,"audio.event.mxframework.charactertest.action.lightattackhit"))?.id
    ||templates.find(item=>item.is3D)?.id||templates[0]?.id||"";
}

function renderManagedPhysicalEventStatus(operation) {
  if(!operation)return `<div class="managed-event-empty"><strong>模板化创建，不修改未知外部 Event</strong><span>新 Event 继承模板的 Bank、Bus、2D/3D 与距离；素材仍在候选页生成和应用。</span></div>`;
  const stages=["Queued","Backup","FmodCreate","BuildBanks","Mirror","UnityRefresh","Snapshot","Ready"],failed=operation.stage==="Failed",done=operation.stage==="Ready";
  const current=Math.max(0,stages.indexOf(operation.stage));
  return `<div class="managed-event-progress ${failed?"failed":done?"ready":"running"}"><div><strong>${failed?"受控创建失败":done?"受控 Basis / Cue / Event 已创建":"正在创建受控 Basis / Cue / Event"}</strong><span>${esc(operation.message||operation.stage||"")}</span><b>${numberValue(operation.progress)}%</b></div><progress max="100" value="${numberValue(operation.progress)}"></progress><div class="managed-event-stages">${stages.slice(1).map((stage,index)=>{const position=index+1;return `<span class="${done||position<current?"done":position===current?"active":""}">${esc(managedPhysicalStageLabel(stage))}</span>`;}).join("<i>→</i>")}</div>${operation.eventPath?`<code>${esc(operation.eventPath)}</code>`:""}${failed?`<details><summary>技术详情</summary><p>${esc(operation.code||"AUTH_AUDIO_MANAGED_EVENT_FAILED")} · ${esc(operation.detail||operation.message||"")}</p></details>`:""}</div>`;
}

function managedPhysicalStageLabel(stage) { return ({Backup:"备份",FmodCreate:"创建 Event",BuildBanks:"Build Banks",Mirror:"同步 Banks",UnityRefresh:"Unity 刷新",Snapshot:"Snapshot",Ready:"完成"})[stage]||stage; }

function renderPhysicalCoverageWorkspace(cue) {
  const families = state.physicalManifest?.families || [];
  const basisSounds = state.physicalManifest?.basisSounds || [];
  const coverage = state.physicalCoverage;
  if (!families.length) return `<section class="section physical-coverage-head">${empty("Physical Manifest 尚无 Family。")}</section>`;
  const family = selectedCoverageFamily(cue,families);
  const cells = coverageCellsForFamily(family.id);
  const selection = selectedCoverageCell(family,cells,basisSounds);
  return `<section class="section physical-coverage-head coverage-command-bar"><div class="workspace-section-head"><div><p class="eyebrow">选择器权威配置</p><h3>覆盖与调制</h3><p>先选择交互 Family，再检查材质对命中的 Basis；只有当前组合的参数会进入编辑区。</p></div><div class="row-actions"><button type="button" data-physical-action="coverage">重新校验</button><button type="button" class="primary" data-coverage-inspector-action="open-scenario">进入情境预览</button></div></div>
    <div data-coverage-summary>${renderCoverageCompactSummary(coverage)}</div>
  </section>
  <section class="coverage-workbench">
    ${renderCoverageFamilyNavigation(families,coverage,family.id)}
    ${renderCoverageMatrixPanel(family,cells,selection,basisSounds)}
    ${renderCoverageInspector(family,selection,basisSounds)}
  </section>
  <details class="section coverage-advanced-settings"><summary>高级选择器设置 · ${esc(family.displayName || family.id)}</summary>
    <p class="notice">仅在需要修改 Requirement ID、笛卡尔材质集合、fallback、权重或响应曲线时展开。</p>
    ${renderCoverageRequirementEditor(cue,family.id)}
    <div class="physical-family-grid">${renderPhysicalFamilyCard(family,basisSounds)}</div>
    <details class="physical-response-profiles"><summary>响应曲线 · ${state.physicalManifest?.responseProfiles?.length || 0}</summary>${renderResponseProfiles()}</details>
  </details>`;
}

function selectedCoverageFamily(cue,families=state.physicalManifest?.families||[]) {
  const current=state.physicalCoverageFamilyId&&families.find(item=>sameId(item.id,state.physicalCoverageFamilyId));
  const basis=currentBasisForCue(cue);
  const family=current||families.find(item=>sameId(item.id,basis?.familyId))||families.find(item=>item.lifecycle!=="Archived")||families[0];
  state.physicalCoverageFamilyId=family?.id||"";
  return family;
}

function coverageCellsForFamily(familyId) { return (state.physicalCoverage?.cells||[]).filter(item=>sameId(item.familyId,familyId)); }

function selectedCoverageCell(family,cells,basisSounds) {
  let selected=state.physicalCoverageCell;
  if(!selected||!sameId(selected.familyId,family.id)||!cells.some(item=>sameId(item.impactorMaterialId,selected.impactorMaterialId)&&sameId(item.receiverMaterialId,selected.receiverMaterialId))){
    const cueBasis=currentBasisForCue();
    const anchored=sameId(cueBasis?.familyId,family.id)&&cells.find(item=>sameId(item.impactorMaterialId,cueBasis?.impactor?.materialId)&&sameId(item.receiverMaterialId,cueBasis?.receiver?.materialId));
    const cell=anchored||cells.find(item=>String(item.status).toLowerCase()!=="covered")||cells[0]||null;
    selected=cell?{familyId:family.id,impactorMaterialId:cell.impactorMaterialId,receiverMaterialId:cell.receiverMaterialId}:null;
    state.physicalCoverageCell=selected;
  }
  return selected?cells.find(item=>sameId(item.impactorMaterialId,selected.impactorMaterialId)&&sameId(item.receiverMaterialId,selected.receiverMaterialId))||null:null;
}

function renderCoverageCompactSummary(coverage) {
  if(!coverage)return `<div class="coverage-compact-summary stale"><span>覆盖结果已失效</span><strong>保存后重新校验</strong></div>`;
  const required=numberValue(coverage.requiredCellCount),covered=numberValue(coverage.coveredCellCount),missing=numberValue(coverage.missingCellCount),ambiguous=numberValue(coverage.ambiguousCellCount);
  return `<div class="coverage-compact-summary${state.physicalCoverageStale?" stale":""}">${state.physicalCoverageStale?`<span class="warn"><b>待校验</b> 以下为修改前结果</span>`:""}<span><b>${covered}/${required}</b> 必需组合已覆盖</span><span class="${missing?"bad":"good"}"><b>${missing}</b> 空洞</span><span class="${ambiguous?"warn":"good"}"><b>${ambiguous}</b> 歧义</span><span><b>${state.physicalManifest?.basisSounds?.length||0}</b> Basis</span></div>`;
}

function renderCoverageFamilyNavigation(families,coverage,selectedId) {
  const allCells=coverage?.cells||[];
  return `<nav class="coverage-family-nav" aria-label="交互 Family"><header><span>交互 Family</span><small>一次只配置一类声音</small></header>${families.map(family=>{const cells=allCells.filter(item=>sameId(item.familyId,family.id)),bad=cells.filter(item=>String(item.status).toLowerCase()!=="covered").length,basisCount=(state.physicalManifest?.basisSounds||[]).filter(item=>sameId(item.familyId,family.id)).length;return `<button type="button" class="${sameId(family.id,selectedId)?"active":""}" data-coverage-family-id="${esc(family.id)}"><span><strong>${esc(family.displayName||family.id)}</strong><small>${esc(family.interactionKind)} · ${basisCount} Basis</small></span><b class="${bad?"bad":"good"}">${cells.length?bad?`${bad} 待处理`:`${cells.length}/${cells.length}`:"未校验"}</b></button>`;}).join("")}</nav>`;
}

function renderCoverageMatrixPanel(family,cells,selection,basisSounds) {
  const materials=state.physicalManifest?.materialProfiles||[],label=id=>materials.find(item=>sameId(item.id,id))?.displayName||id;
  const impactors=[...new Set(cells.map(item=>item.impactorMaterialId))],receivers=[...new Set(cells.map(item=>item.receiverMaterialId))];
  if(!cells.length)return `<section class="coverage-matrix-panel"><header><div><p class="eyebrow">材质对覆盖矩阵</p><h3>${esc(family.displayName||family.id)}</h3></div></header><div class="coverage-empty"><strong>尚无可显示的覆盖单元</strong><span>在高级设置中添加必需覆盖契约，然后重新校验。</span></div></section>`;
  const cellAt=(left,right)=>cells.find(item=>sameId(item.impactorMaterialId,left)&&sameId(item.receiverMaterialId,right));
  const matrix=`<div class="coverage-matrix" style="--coverage-columns:${receivers.length}"><span class="coverage-matrix-corner">碰撞体 ↓ / 受体 →</span>${receivers.map(id=>`<span class="coverage-matrix-label">${esc(label(id))}</span>`).join("")}${impactors.map(left=>`<span class="coverage-matrix-label row">${esc(label(left))}</span>${receivers.map(right=>{const cell=cellAt(left,right);if(!cell)return `<span class="coverage-matrix-cell unavailable">非必需</span>`;const selected=sameId(selection?.impactorMaterialId,left)&&sameId(selection?.receiverMaterialId,right),basis=(cell.selectedBasisIds||[]).map(id=>basisSounds.find(item=>sameId(item.id,id))).find(Boolean),status=String(cell.status||"Unknown").toLowerCase();return `<button type="button" class="coverage-matrix-cell ${status}${selected?" selected":""}" data-coverage-cell data-coverage-cell-family="${esc(family.id)}" data-coverage-cell-impactor="${esc(left)}" data-coverage-cell-receiver="${esc(right)}"><strong>${esc(basis?.displayName||basis?.id||cell.status||"未命中")}</strong><small>${numberValue(cell.resolvedScenarioCount)}/${numberValue(cell.scenarioCount)} 情境 · ${numberValue(cell.matchCount)} match</small></button>`;}).join("")}`).join("")}</div>`;
  const selectedBasis=(selection?.selectedBasisIds||[]).map(id=>basisSounds.find(item=>sameId(item.id,id))).find(Boolean);
  return `<section class="coverage-matrix-panel"><header><div><p class="eyebrow">材质两两组合矩阵 · ${impactors.length} × ${receivers.length}</p><h3>${esc(family.displayName||family.id)}</h3><p>${esc(family.interactionKind)} · ${esc(family.interactionMode)}；行是碰撞体材质，列是受体材质，交叉单元格显示实际命中的 Basis。</p></div><span class="coverage-matrix-legend"><i></i>已覆盖 <i class="warn"></i>歧义 <i class="bad"></i>空洞</span></header><div class="coverage-matrix-scroll">${matrix}</div><article class="coverage-selection-result"><div><span>当前选择结果</span><strong>${esc(selectedBasis?.displayName||selectedBasis?.id||selection?.status||"请选择材质对")}</strong><small>${selection?`${esc(label(selection.impactorMaterialId))} × ${esc(label(selection.receiverMaterialId))} · ${numberValue(selection.resolvedScenarioCount)}/${numberValue(selection.scenarioCount)} 情境解析成功`:"—"}</small></div>${selectedBasis?`<button type="button" data-coverage-inspector-action="open-basis" data-coverage-basis-cue="${esc(selectedBasis.audioCueId)}">打开 Basis</button>`:""}</article></section>`;
}

function renderCoverageInspector(family,cell,basisSounds) {
  const materials=state.physicalManifest?.materialProfiles||[],material=id=>materials.find(item=>sameId(item.id,id)),basis=(cell?.selectedBasisIds||[]).map(id=>basisSounds.find(item=>sameId(item.id,id))).find(Boolean);
  if(!cell)return `<aside class="coverage-cell-inspector"><header><p class="eyebrow">当前组合</p><h3>尚未选择</h3></header><p class="notice">从矩阵选择一个材质对后，在这里调整命中 Basis 的锚点。</p></aside>`;
  const anchor=(path,label,min,max,step,unit)=>{const range=getNestedValue(basis,path),value=numberValue(range?.anchor,min);return `<label class="coverage-anchor-slider"><span>${label}<b>${value.toFixed(step<.1?2:1)}${unit}</b></span><input type="range" min="${min}" max="${max}" step="${step}" value="${value}" data-coverage-basis-id="${esc(basis?.id||"")}" data-coverage-basis-range="${esc(path)}.anchor"${basis?"":" disabled"}><small>适用范围 ${numberValue(range?.min).toFixed(2)}–${numberValue(range?.max).toFixed(2)}${unit}</small></label>`;};
  return `<aside class="coverage-cell-inspector"><header><p class="eyebrow">当前组合</p><h3>${esc(material(cell.impactorMaterialId)?.displayName||cell.impactorMaterialId)} <span>×</span> ${esc(material(cell.receiverMaterialId)?.displayName||cell.receiverMaterialId)}</h3><small>${esc(family.displayName||family.id)}</small></header><div class="coverage-pair-roles"><span><b>A</b><small>碰撞体</small><strong>${esc(material(cell.impactorMaterialId)?.displayName||cell.impactorMaterialId)}</strong></span><i>×</i><span><b>B</b><small>受体</small><strong>${esc(material(cell.receiverMaterialId)?.displayName||cell.receiverMaterialId)}</strong></span></div><div class="coverage-inspector-basis"><span>命中 Basis</span><strong>${esc(basis?.displayName||basis?.id||"未命中")}</strong><small>${basis?`#${esc(basis.runtimeBasisId)} · ${esc(basis.audioCueId)}`:`${esc(cell.status||"Missing")} · 请补充 Basis 或 fallback`}</small></div>${basis?`<div class="coverage-anchor-controls">${anchor("relativeSpeed","速度锚点",0,30,.1,"m/s")}${anchor("intensity","强度锚点",0,1,.01,"")}${anchor("impactor.mass","质量锚点",0,1,.01,"")}</div>`:""}<div class="coverage-inspector-actions"><button type="button" data-coverage-inspector-action="open-scenario">用此组合模拟</button>${basis?`<button type="button" class="primary" data-coverage-inspector-action="open-basis" data-coverage-basis-cue="${esc(basis.audioCueId)}">编辑 Basis</button>`:""}</div><p class="notice">矩阵结果由 Authoring Core 计算。材质契约、fallback 和完整范围位于下方高级设置。</p></aside>`;
}

function renderCoverageRequirementEditor(cue,familyId="") {
  const requirements=state.physicalManifest?.coverageRequirements||[],families=state.physicalManifest?.families||[],materials=state.physicalManifest?.materialProfiles||[];
  const familyOptions=value=>families.map(item=>`<option value="${esc(item.id)}"${sameId(value,item.id)?" selected":""}>${esc(item.displayName||item.id)}</option>`).join("");
  const materialChecks=(requirement,side)=>materials.map(item=>`<label class="coverage-material"><input type="checkbox" data-coverage-requirement-material="${side}" value="${esc(item.id)}"${(requirement[`${side}MaterialIds`]||[]).some(value=>sameId(value,item.id))?" checked":""}><span>${esc(item.displayName||item.id)}</span></label>`).join("");
  const visible=requirements.map((requirement,index)=>({requirement,index})).filter(item=>!familyId||sameId(item.requirement.familyId,familyId));
  return `<section class="physical-coverage-contracts"><div class="workspace-section-head"><div><p class="eyebrow">发布所需情境</p><h3>必需覆盖契约</h3><p>明确当前 Family 必须解析的材质对；空契约不能视为覆盖完成。</p></div><button type="button" data-physical-coverage-action="add" data-coverage-family="${esc(familyId)}">新增必需覆盖</button></div>
    <div class="coverage-requirement-list">${visible.map(({requirement,index})=>`<article class="coverage-requirement" data-coverage-requirement-index="${index}"><header><label class="field"><span>稳定 Requirement ID</span><input value="${esc(requirement.id||"")}" data-coverage-requirement-field="id" spellcheck="false"></label><button type="button" class="danger" data-physical-coverage-action="delete" data-coverage-index="${index}">删除</button></header><div class="coverage-requirement-meta"><label class="field"><span>Family</span><select data-coverage-requirement-field="familyId">${familyOptions(requirement.familyId)}</select></label><label class="coverage-required"><input type="checkbox" data-coverage-requirement-field="required"${requirement.required!==false?" checked":""}><span>阻止发布的必需覆盖</span></label></div><div class="coverage-material-pair"><fieldset><legend>碰撞体材质</legend>${materialChecks(requirement,"impactor")}</fieldset><strong>×</strong><fieldset><legend>受体材质</legend>${materialChecks(requirement,"receiver")}</fieldset></div><footer>${(requirement.impactorMaterialIds||[]).length} × ${(requirement.receiverMaterialIds||[]).length} = ${(requirement.impactorMaterialIds||[]).length*(requirement.receiverMaterialIds||[]).length} 个材质对${requirement.facts?.length?` · ${requirement.facts.length} 个显式事实`:""}</footer></article>`).join("")||empty("当前 Family 尚无覆盖契约。")}</div>
  </section>`;
}

function renderPhysicalFamilyCard(family, basisSounds) {
  const familyBasis = basisSounds.filter(item => sameId(item.familyId,family.id));
  const responseOptions = (state.physicalManifest?.responseProfiles || []).map(item => `<option value="${esc(item.id)}"${sameId(item.id,family.responseProfileId)?" selected":""}>${esc(item.displayName || item.id)}</option>`).join("");
  const basisOptions = [`<option value="">无</option>`, ...basisSounds.map(item => `<option value="${esc(item.id)}"${sameId(item.id,family.fallbackBasisId)?" selected":""}>${esc(item.displayName || item.id)}</option>`)].join("");
  const familyOptions = [`<option value="">无</option>`, ...(state.physicalManifest?.families || []).filter(item => item!==family).map(item => `<option value="${esc(item.id)}"${sameId(item.id,family.fallbackFamilyId)?" selected":""}>${esc(item.displayName || item.id)}</option>`)].join("");
  return `<article class="physical-family-card" data-family-id="${esc(family.id)}"><header><div><strong>${esc(family.displayName || family.id)}</strong><small>${esc(family.interactionKind)} · ${esc(family.interactionMode)}</small></div>${badge(`${familyBasis.length} Basis`, familyBasis.length ? "ok" : "warn")}</header>
    <div class="physical-family-settings">
      <label class="field"><span>Family fallback</span><select data-physical-family-path="fallbackFamilyId">${familyOptions}</select></label>
      <label class="field"><span>Basis fallback</span><select data-physical-family-path="fallbackBasisId">${basisOptions}</select></label>
      <label class="field"><span>响应曲线</span><select data-physical-family-path="responseProfileId">${responseOptions}</select></label>
      <label class="field"><span>冷却帧</span><input type="number" min="0" max="600" value="${numberValue(family.cooldownFrames,0)}" data-physical-family-path="cooldownFrames"></label>
      <label class="field"><span>优先级</span><input type="number" min="-100" max="100" value="${numberValue(family.priority,0)}" data-physical-family-path="priority"></label>
    </div>
    <details><summary>选择权重与必需 FMOD 参数</summary><div class="physical-weight-grid">${Object.entries(family.weights || {}).map(([key,value]) => `<label><span>${esc(key)}</span><input type="range" min="0" max="4" step="0.05" value="${numberValue(value,0)}" data-physical-weight="${esc(key)}"><output>${numberValue(value,0).toFixed(2)}</output></label>`).join("") || `<p class="notice">当前 Family 使用编译器默认权重。</p>`}</div><p class="notice">必需参数：${esc((family.requiredParameterIds || []).join(", ") || "无")}</p></details>
    <footer>${familyBasis.map(item => `<button type="button" data-basis-cue="${esc(item.audioCueId)}">${esc(item.displayName || item.id)}</button>`).join("") || `<span class="notice warn">覆盖空洞：没有 Basis</span>`}</footer>
  </article>`;
}

function renderPhysicalScenarioWorkspace(cue) {
  const scenario = physicalScenarioForCue(cue);
  const materials = state.physicalManifest?.materialProfiles || [];
  const materialOptions = value => materials.map(item => `<option value="${esc(item.id)}"${sameId(value,item.id)?" selected":""}>${esc(item.displayName || item.id)} · #${esc(item.runtimeMaterialId)}</option>`).join("");
  const capture=state.physicalCaptures.get(cue.audioCueId);
  const capturePending=Boolean(state.physicalCaptureActiveRequestId);
  return `<section class="section physical-scenario"><div class="workspace-section-head"><div><p class="eyebrow">Authoring 初筛 + Unity Runtime 权威试听</p><h3>游戏情境预览</h3><p>输入双方属性与交互事实；先由 Authoring Core 显示选择轨迹，试听时由 Unity 加载临时 Runtime Catalog 并再次运行真实 PhysicalAudioResolver。</p></div><div class="row-actions"><button type="button" data-physical-action="capture" data-physical-capture-button${capturePending?" disabled":""}>${capturePending?"Unity 捕获中…":"从 Unity 捕获真实交互"}</button><button type="button" class="primary" data-physical-action="resolve">解析当前情境</button></div></div>
    <div class="scenario-kind-row"><label class="field"><span>交互类型</span><select data-physical-fact="interactionKind">${["Footstep","Landing","ObjectImpact","WeaponHit","WeaponBlock","AirDisplacement","Scrape","Roll","Compression"].map(value => `<option${scenario.interactionKind===value?" selected":""}>${value}</option>`).join("")}</select></label><label class="field"><span>交互模式</span><select data-physical-fact="interactionMode">${["Any","Step","Landing","Impact","Hit","Block","Sweep","Scrape","Roll","Compression"].map(value => `<option${scenario.interactionMode===value?" selected":""}>${value}</option>`).join("")}</select></label></div>
    <div class="scenario-normal"><div><strong>${scenario.interactionKind==="AirDisplacement"?"扫掠方向":"接触法线"}</strong><small>独立于播放朝向；发送到 Unity 前会安全归一化。</small></div>${["x","y","z"].map(axis=>`<label><span>${axis.toUpperCase()}</span><input type="number" min="-1" max="1" step="0.01" value="${numberValue(scenario.contactNormal?.[axis]).toFixed(2)}" data-physical-normal="${axis}"></label>`).join("")}</div>
    <div class="scenario-body-grid">
      ${renderScenarioBody("impactor","碰撞体 / 武器 / 鞋底",scenario.impactor,materialOptions(scenario.impactor.materialId))}
      ${renderScenarioBody("receiver","受体 / 目标 / 地面",scenario.receiver,materialOptions(scenario.receiver.materialId))}
    </div>
    <div class="scenario-sliders">
      ${scenarioSlider("relativeSpeed","相对速度",scenario.relativeSpeed,0,30,.1,"m/s")}
      ${scenarioSlider("normalSpeed","法向速度",scenario.normalSpeed,0,30,.1,"m/s")}
      ${scenarioSlider("tangentialSpeed","切向速度",scenario.tangentialSpeed,0,30,.1,"m/s")}
      ${scenarioSlider("pressure","压力",scenario.pressure,0,1,.01,"")}
      ${scenarioSlider("impulse","冲量",scenario.impulse,0,1,.01,"")}
      ${scenarioSlider("intensity","冲击强度",scenario.intensity,0,1,.01,"")}
      ${scenarioSlider("angleDegrees","接触角度",scenario.angleDegrees,0,180,1,"°")}
    </div>
    <div data-physical-capture-status>${renderPhysicalCaptureStatus(cue,capture)}</div>
  </section>
  <section class="section physical-resolve-result" data-physical-resolve-result>${renderPhysicalResolveResult(state.physicalResolve)}</section>
  <section class="section physical-analysis" data-physical-analysis-panel>${renderPhysicalAnalysisPanel(cue)}</section>`;
}

function physicalAnalysisForCue(cue) {
  let analysis = state.physicalAnalyses.get(cue.audioCueId);
  if (analysis) return analysis;
  analysis = {
    sweepConfig:{ axis:"relativeSpeed", min:0, max:20, samples:9 },
    heatmapConfig:{ xAxis:"impactor.materialId", yAxis:"receiver.materialId", xMin:0, xMax:1, yMin:0, yMax:1, size:7 },
    sweep:null, heatmap:null, pendingKind:"", progress:0, activeRunId:0
  };
  state.physicalAnalyses.set(cue.audioCueId,analysis);
  return analysis;
}

function renderPhysicalAnalysisPanel(cue) {
  const analysis = physicalAnalysisForCue(cue);
  const disabled = analysis.pendingKind ? " disabled" : "";
  const sweepAxis = PHYSICAL_ANALYSIS_AXES[analysis.sweepConfig.axis] || PHYSICAL_ANALYSIS_AXES.relativeSpeed;
  const heatX = PHYSICAL_ANALYSIS_AXES[analysis.heatmapConfig.xAxis] || PHYSICAL_ANALYSIS_AXES["impactor.materialId"];
  const heatY = PHYSICAL_ANALYSIS_AXES[analysis.heatmapConfig.yAxis] || PHYSICAL_ANALYSIS_AXES["receiver.materialId"];
  return `<div class="workspace-section-head"><div><p class="eyebrow">服务器权威批量解析</p><h3>Sweep 与覆盖热图</h3><p>每个采样点都调用 Authoring Core 的 PhysicalAudioResolver；浏览器只组织事实和显示结果。</p></div>${analysis.pendingKind ? badge(`解析中 ${analysis.progress}%`,"info") : badge("最多 64 点 · 并发 4")}</div>
    <div class="physical-analysis-grid">
      <article class="physical-sweep-card">
        <header><div><strong>单参数 Sweep</strong><small>观察 Basis、fallback 和播放参数的切换边界</small></div><button type="button" class="primary" data-physical-analysis-action="run-sweep"${disabled}>运行 Sweep</button></header>
        <div class="analysis-controls">
          <label class="field"><span>扫描参数</span><select data-physical-analysis-config="sweep.axis"${disabled}>${renderPhysicalAxisOptions(analysis.sweepConfig.axis)}</select></label>
          ${sweepAxis.type === "material" ? `<div class="analysis-readonly"><span>取值</span><strong>当前 Family 关联材质</strong></div>` : `${analysisNumberField("sweep.min","最小值",analysis.sweepConfig.min,sweepAxis,disabled)}${analysisNumberField("sweep.max","最大值",analysis.sweepConfig.max,sweepAxis,disabled)}`}
          <label class="field"><span>采样数</span><select data-physical-analysis-config="sweep.samples"${disabled}>${[5,7,9,11].map(value=>`<option value="${value}"${Number(analysis.sweepConfig.samples)===value?" selected":""}>${value} 点</option>`).join("")}</select></label>
        </div>
        ${renderPhysicalSweepResult(cue,analysis)}
      </article>
      <article class="physical-heatmap-card">
        <header><div><strong>二维覆盖热图</strong><small>检查材质对或连续属性组合是否落入预期 Basis</small></div><button type="button" class="primary" data-physical-analysis-action="run-heatmap"${disabled}>生成热图</button></header>
        <div class="analysis-controls heatmap-controls">
          <label class="field"><span>横轴</span><select data-physical-analysis-config="heatmap.xAxis"${disabled}>${renderPhysicalAxisOptions(analysis.heatmapConfig.xAxis)}</select></label>
          <label class="field"><span>纵轴</span><select data-physical-analysis-config="heatmap.yAxis"${disabled}>${renderPhysicalAxisOptions(analysis.heatmapConfig.yAxis)}</select></label>
          ${heatX.type === "material" ? "" : `${analysisNumberField("heatmap.xMin","横轴最小",analysis.heatmapConfig.xMin,heatX,disabled)}${analysisNumberField("heatmap.xMax","横轴最大",analysis.heatmapConfig.xMax,heatX,disabled)}`}
          ${heatY.type === "material" ? "" : `${analysisNumberField("heatmap.yMin","纵轴最小",analysis.heatmapConfig.yMin,heatY,disabled)}${analysisNumberField("heatmap.yMax","纵轴最大",analysis.heatmapConfig.yMax,heatY,disabled)}`}
          <label class="field"><span>连续轴精度</span><select data-physical-analysis-config="heatmap.size"${disabled}>${[5,7].map(value=>`<option value="${value}"${Number(analysis.heatmapConfig.size)===value?" selected":""}>${value} 档</option>`).join("")}</select></label>
        </div>
        ${renderPhysicalHeatmapResult(cue,analysis)}
      </article>
    </div>`;
}

function renderPhysicalCaptureStatus(cue,capture=state.physicalCaptures.get(cue.audioCueId)) {
  if(!capture)return `<div class="physical-capture-status idle"><strong>可从 Unity 回放真实事实</strong><span>仅读取最近一次已经进入 Physical Audio 播放队列的交互；没有真实来源时不会生成模拟数据。</span></div>`;
  if(capture.status==="pending")return `<div class="physical-capture-status pending"><strong>正在等待 Unity 捕获…</strong><span>${esc(capture.message||"Unity 正在查询最近的 Physical Audio 交互。")}</span></div>`;
  if(capture.status==="unavailable")return `<div class="physical-capture-status unavailable"><strong>Unity 暂无可捕获的真实交互</strong><span>${esc(capture.message||"请先在 CharacterTest 中触发一次物理音效。")}</span><small>${esc(capture.code||"AUDIO_PHYSICAL_CAPTURE_SOURCE_UNAVAILABLE")}</small></div>`;
  if(capture.status==="failed"||capture.mappingError)return `<div class="physical-capture-status error"><strong>真实事实未应用到情境</strong><span>${esc(capture.mappingError||capture.message||"Unity 事实捕获失败。")}</span><small>${esc(capture.mappingCode||capture.code||"AUDIO_PHYSICAL_CAPTURE_FAILED")}</small></div>`;
  const basis=(state.physicalManifest?.basisSounds||[]).find(item=>Number(item.runtimeBasisId)===Number(capture.runtimeBasisId));
  const runtimeCue=(state.manifest?.cues||[]).find(item=>Number(item.runtimeCueId)===Number(capture.runtimeCueId));
  const parameters=(capture.parameters||[]).map(item=>`<span><b>${esc(item.name||`#${item.parameterId}`)}</b><strong>${numberValue(item.value).toFixed(3)}</strong></span>`).join("");
  const fact=capture.fact||{};
  const normal=fact.contactNormal||{};
  const dynamics=fact.dynamicsSource&&fact.dynamicsSource!=="Unknown"
    ? `<div class="resolved-parameters"><span><b>动力学来源</b><strong>${esc(fact.dynamicsSource)}</strong></span><span><b>法向速度</b><strong>${numberValue(fact.normalSpeed).toFixed(3)} m/s</strong></span><span><b>切向速度</b><strong>${numberValue(fact.tangentialSpeed).toFixed(3)} m/s</strong></span><span><b>原始法向冲量</b><strong>${numberValue(fact.rawNormalImpulseNewtonSeconds).toFixed(3)} N·s</strong></span><span><b>原始平均压力</b><strong>${numberValue(fact.rawAveragePressurePascals).toFixed(0)} Pa</strong></span></div>`
    : "";
  return `<div class="physical-capture-status succeeded"><div><strong>已回填 Unity 真实交互</strong><span>revision ${esc(capture.captureRevision||0)} · frame ${esc(fact.frame??"—")} · sequence ${esc(fact.sequence??"—")}</span></div><div class="physical-capture-runtime"><span><small>Runtime Basis</small><strong>${esc(basis?.displayName||basis?.id||`#${capture.runtimeBasisId}`)}</strong></span><span><small>Runtime Cue</small><strong>${esc(runtimeCue?.displayName||runtimeCue?.audioCueId||`#${capture.runtimeCueId}`)}</strong></span><span><small>匹配</small><strong>${numberValue(capture.matchDistance).toFixed(3)}${capture.usedFallback?" · fallback":""}</strong></span></div>${dynamics}${parameters?`<div class="resolved-parameters">${parameters}</div>`:""}<small>方向 (${numberValue(normal.x).toFixed(2)}, ${numberValue(normal.y).toFixed(2)}, ${numberValue(normal.z).toFixed(2)}) · ${esc(capture.message||"")} · ${esc(capture.updatedUtc||"")}</small></div>`;
}

function renderPhysicalAxisOptions(selected) {
  return Object.entries(PHYSICAL_ANALYSIS_AXES).map(([id,axis]) => `<option value="${esc(id)}"${id===selected?" selected":""}>${esc(axis.label)}</option>`).join("");
}

function analysisNumberField(path,label,value,axis,disabled) {
  const step = axis.decimals === 0 ? 1 : axis.decimals === 1 ? .1 : .01;
  return `<label class="field"><span>${label}${axis.unit ? `（${esc(axis.unit)}）` : ""}</span><input type="number" step="${step}" value="${numberValue(value).toFixed(axis.decimals ?? 2)}" data-physical-analysis-config="${path}"${disabled}></label>`;
}

function renderPhysicalSweepResult(cue, analysis) {
  if (analysis.pendingKind === "sweep") return `<div class="analysis-pending"><span style="--progress:${analysis.progress}%"></span><strong>正在解析 ${analysis.progress}%</strong></div>`;
  const run = analysis.sweep;
  if (!run) return `<div class="analysis-empty">选择一个参数后运行；结果可点击回填当前情境。</div>`;
  const axis=PHYSICAL_ANALYSIS_AXES[run.axis]||PHYSICAL_ANALYSIS_AXES.relativeSpeed;
  const stale = run.signature !== physicalAnalysisSignature(cue,[run.axis]) || stableText(run.config) !== stableText(analysis.sweepConfig);
  const unique = new Set(run.points.filter(item=>item.result?.success).map(item=>item.result.basisId));
  return `${stale ? `<div class="notice warn analysis-stale">当前事实或 Physical Manifest 已变化，以下是上一轮结果。</div>` : ""}<div class="sweep-summary"><span>${run.points.length} 个采样</span><span>${unique.size} 个 Basis</span><span>${run.points.filter(item=>item.result?.trace?.usedFallback).length} 个 fallback</span><span>${run.points.filter(item=>!item.result?.success).length} 个未命中</span></div><div class="physical-sweep-results">${run.points.map((point,index) => {
    const result = point.result || {};
    const tone = result.success ? basisColor(result.basisId) : "#8d3d4a";
    return `<button type="button" class="sweep-point ${result.success ? "" : "missing"}" style="--basis-color:${tone}" data-physical-analysis-action="apply-sweep" data-analysis-index="${index}" title="${esc(analysisResultTitle(result))}"><span>${esc(formatAnalysisValue(axis,point.value))}</span><i></i><strong>${esc(shortBasisName(result))}</strong><small>${result.success ? `score ${numberValue(result.score).toFixed(3)}${result.trace?.usedFallback ? " · fallback" : ""}` : esc(result.code || "未命中")}</small></button>`;
  }).join("")}</div>${renderPhysicalAnalysisLegend(run.points)}`;
}

function renderPhysicalHeatmapResult(cue, analysis) {
  if (analysis.pendingKind === "heatmap") return `<div class="analysis-pending"><span style="--progress:${analysis.progress}%"></span><strong>正在解析 ${analysis.progress}%</strong></div>`;
  const run = analysis.heatmap;
  if (!run) return `<div class="analysis-empty">默认检查双方材质组合；也可改为速度 × 强度等连续属性。</div>`;
  const xAxis=PHYSICAL_ANALYSIS_AXES[run.xAxis]||PHYSICAL_ANALYSIS_AXES["impactor.materialId"],yAxis=PHYSICAL_ANALYSIS_AXES[run.yAxis]||PHYSICAL_ANALYSIS_AXES["receiver.materialId"];
  const stale = run.signature !== physicalAnalysisSignature(cue,[run.xAxis,run.yAxis]) || stableText(run.config) !== stableText(analysis.heatmapConfig);
  const columns = run.xValues.length;
  return `${stale ? `<div class="notice warn analysis-stale">当前事实或 Physical Manifest 已变化，以下是上一轮结果。</div>` : ""}<div class="heatmap-axis-title"><span>纵轴：${esc(yAxis.label)}</span><strong>横轴：${esc(xAxis.label)}</strong></div><div class="physical-heatmap-scroll"><div class="physical-heatmap" style="--heatmap-columns:${columns}"><span class="heatmap-corner"></span>${run.xValues.map(value=>`<span class="heatmap-x-label" title="${esc(formatAnalysisValue(xAxis,value))}">${esc(shortAnalysisValue(xAxis,value))}</span>`).join("")}${run.yValues.map((yValue,yIndex)=>`<span class="heatmap-y-label" title="${esc(formatAnalysisValue(yAxis,yValue))}">${esc(shortAnalysisValue(yAxis,yValue))}</span>${run.xValues.map((_,xIndex)=>{
      const cell=run.cells[yIndex*columns+xIndex],result=cell?.result||{},color=result.success?basisColor(result.basisId):"#8d3d4a";
      return `<button type="button" class="heatmap-cell ${result.success?"":"missing"} ${result.trace?.usedFallback?"fallback":""}" style="--basis-color:${color}" data-physical-analysis-action="apply-heatmap" data-analysis-x="${xIndex}" data-analysis-y="${yIndex}" title="${esc(`${formatAnalysisValue(xAxis,cell.xValue)} × ${formatAnalysisValue(yAxis,cell.yValue)}\n${analysisResultTitle(result)}`)}"><span>${result.success ? esc(shortBasisName(result)) : "×"}</span></button>`;
    }).join("")}`).join("")}</div></div>${renderPhysicalAnalysisLegend(run.cells)}`;
}

function renderPhysicalAnalysisLegend(items) {
  const entries = new Map();
  for (const item of items || []) if (item.result?.success && !entries.has(item.result.basisId)) entries.set(item.result.basisId,item.result.audioCueId);
  return `<div class="analysis-legend">${[...entries].map(([basisId,cueId])=>`<span><i style="--basis-color:${basisColor(basisId)}"></i>${esc(findCue(cueId)?.displayName || basisId)}</span>`).join("")}${(items||[]).some(item=>!item.result?.success)?`<span><i class="missing"></i>未命中</span>`:""}<span><i class="fallback"></i>斜纹 = fallback</span></div>`;
}

function shortBasisName(result) {
  if (!result?.success) return "未命中";
  return findCue(result.audioCueId)?.displayName || String(result.basisId || "Basis").split(".").pop();
}

function analysisResultTitle(result) {
  if (!result?.success) return `${result?.code || "NO_MATCH"} · ${result?.message || "未命中 Basis"}`;
  const parameters = (result.parameters || []).map(item=>`#${item.parameterId}=${numberValue(item.value).toFixed(3)}`).join(", ");
  return `${result.basisId} · score ${numberValue(result.score).toFixed(3)}${result.trace?.usedFallback ? " · fallback" : ""}${parameters ? ` · ${parameters}` : ""}`;
}

function basisColor(id) {
  let hash=2166136261; const text=String(id||"missing");
  for(let index=0;index<text.length;index++){hash^=text.charCodeAt(index);hash=Math.imul(hash,16777619);}
  return `hsl(${Math.abs(hash)%360} 52% 48%)`;
}

function renderScenarioBody(side, label, body, materialOptions) {
  return `<article class="scenario-body-card"><header><span>${side === "impactor" ? "A" : "B"}</span><div><strong>${label}</strong><small>${side === "impactor" ? "提供运动能量" : "决定接触共振"}</small></div></header>
    <label class="field"><span>声学材质</span><select data-physical-body="${side}" data-physical-body-field="materialId">${materialOptions}</select></label>
    ${scenarioBodySlider(side,"mass","归一化质量",body.mass,0,1,.01,"")}
    ${scenarioBodySlider(side,"contactArea","接触面积",body.contactArea,0,1,.01,"")}
    <div class="scenario-body-selects"><label class="field"><span>表面</span><select data-physical-body="${side}" data-physical-body-field="surface">${["Any","Polished","Smooth","Normal","Rough","Granular"].map(value => `<option${body.surface===value?" selected":""}>${value}</option>`).join("")}</select></label><label class="field"><span>构造</span><select data-physical-body="${side}" data-physical-body-field="construction">${["Any","Solid","Hollow","ThinShell","SoftFilled","Gas"].map(value => `<option${body.construction===value?" selected":""}>${value}</option>`).join("")}</select></label></div>
  </article>`;
}

function scenarioBodySlider(side, fieldName, label, value, min, max, step, unit) {
  const number = numberValue(value,min);
  return `<label class="scenario-slider"><span>${label}</span><input type="range" min="${min}" max="${max}" step="${step}" value="${number}" data-physical-body="${side}" data-physical-body-field="${fieldName}"><output>${number.toFixed(step < .1 ? 2 : 1)}${unit}</output></label>`;
}

function scenarioSlider(fieldName, label, value, min, max, step, unit) {
  const number = numberValue(value,min);
  return `<label class="scenario-slider"><span>${label}</span><input type="range" min="${min}" max="${max}" step="${step}" value="${number}" data-physical-fact="${fieldName}"><output>${number.toFixed(step < .1 ? 2 : step < 1 ? 1 : 0)}${unit}</output></label>`;
}

function currentBasisForCue(cue = selectedCue()) {
  return cue ? (state.physicalManifest?.basisSounds || []).find(item => item.audioCueId === cue.audioCueId) : null;
}

function activeAdoptionSet(cue = selectedCue()) {
  if (!cue) return null;
  const eventState = (state.generationManifest?.eventStates || []).find(item => item.eventDefinitionId === cue.eventDefinitionId);
  return (state.generationManifest?.adoptionSets || []).find(item => item.setId === eventState?.activeSetId) || null;
}

function sameId(left, right) { return String(left ?? "") === String(right ?? ""); }
function numberValue(value, fallback = 0) { const number = Number(value); return Number.isFinite(number) ? number : fallback; }

function collisionProfileSummary(profile) { return profile ? collisionSummary(profile) : "尚未配置"; }

function renderAcousticAnchorText(anchor) {
  if (!anchor) return "—";
  const material = (state.physicalManifest?.materialProfiles || []).find(item => sameId(item.id,anchor.materialId));
  const mass = anchor.mass?.anchor ?? anchor.mass ?? 0;
  const area = anchor.contactArea?.anchor ?? anchor.contactArea ?? 0;
  return esc(`${material?.displayName || anchor.materialId || "自动"} · 质量 ${numberValue(mass).toFixed(2)} · 面积 ${numberValue(area).toFixed(2)} · ${anchor.surface || "Any"} / ${anchor.construction || "Any"}`);
}

function renderBasisRangeText(basis) {
  const format = (value, suffix = "") => value ? `${numberValue(value.min).toFixed(2)}–${numberValue(value.max).toFixed(2)}${suffix}（锚 ${numberValue(value.anchor).toFixed(2)}）` : "—";
  return esc(`速度 ${format(basis.relativeSpeed,"m/s")} · 强度 ${format(basis.intensity)} · 角度 ${format(basis.angleDegrees,"°")}`);
}

function renderCoverageSummary(coverage) {
  if (!coverage) return `<div class="coverage-empty"><strong>尚未运行覆盖校验</strong><span>保存或点击“重新校验覆盖”后，由 Authoring Core 检测空洞与歧义。</span></div>`;
  const missing = numberValue(coverage.missingCellCount);
  const ambiguous = numberValue(coverage.ambiguousCellCount);
  const cells = coverage.cells || [];
  return `<div class="coverage-summary"><article><span>必需情境</span><strong>${numberValue(coverage.requiredCellCount)}</strong></article><article><span>已覆盖</span><strong>${numberValue(coverage.coveredCellCount)}</strong></article><article class="${missing ? "bad" : "good"}"><span>覆盖空洞</span><strong>${missing}</strong></article><article class="${ambiguous ? "warn" : "good"}"><span>同分歧义</span><strong>${ambiguous}</strong></article></div>${cells.length ? `<details><summary>覆盖单元 · ${cells.length}</summary><div class="coverage-cells">${cells.map(item => `<span class="${String(item.status||"").toLowerCase()}"><strong>${esc(item.status)}</strong>${esc(item.impactorMaterialId)} × ${esc(item.receiverMaterialId)} · ${esc(item.familyId)} · ${numberValue(item.matchCount)} match</span>`).join("")}</div></details>` : ""}`;
}

function renderResponseProfiles() {
  const profiles = state.physicalManifest?.responseProfiles || [];
  return `<div class="response-profile-list">${profiles.map((profile,profileIndex) => `<article><header><div><strong>${esc(profile.displayName || profile.id)}</strong><small>${esc(profile.id)} · runtime #${esc(profile.runtimeResponseProfileId)}</small></div>${badge(profile.lifecycle || "Active", profile.lifecycle === "Active" ? "ok" : "")}</header><div>${(profile.curves || []).map((curve,curveIndex) => {
    const points = curve.points || [];
    const inputMax = Math.max(1,...points.map(point => numberValue(point.input))) * 1.25;
    const outputMin = numberValue(curve.outputMin);
    const outputMax = numberValue(curve.outputMax,1);
    return `<section><div class="response-curve-head"><strong>参数 #${esc(curve.parameterId)}</strong><span>${esc(curve.input)} → ${outputMin.toFixed(2)}..${outputMax.toFixed(2)}</span></div><div class="response-points">${points.map((point,pointIndex) => `<label><span>${pointIndex + 1}</span><input type="number" min="0" max="${inputMax}" step="0.01" value="${numberValue(point.input).toFixed(2)}" data-response-point="input" data-response-profile-index="${profileIndex}" data-response-curve-index="${curveIndex}" data-response-point-index="${pointIndex}"><i>→</i><input type="number" min="${outputMin}" max="${outputMax}" step="0.01" value="${numberValue(point.output).toFixed(2)}" data-response-point="output" data-response-profile-index="${profileIndex}" data-response-curve-index="${curveIndex}" data-response-point-index="${pointIndex}"></label>`).join("")}</div></section>`;
  }).join("") || `<p class="notice">此 profile 暂无参数曲线。</p>`}</div></article>`).join("") || empty("尚无响应曲线。")}</div>`;
}

function physicalScenarioForCue(cue) {
  let scenario = state.physicalScenarios.get(cue.audioCueId);
  if (scenario) return scenario;
  const basis = currentBasisForCue(cue);
  const family = basis && (state.physicalManifest?.families || []).find(item => sameId(item.id,basis.familyId));
  const materials = state.physicalManifest?.materialProfiles || [];
  const defaultMaterial = materials[0]?.id || "audio.material.generic_solid";
  const body = anchor => ({
    materialId: anchor?.materialId || defaultMaterial,
    mass: numberValue(anchor?.mass?.anchor ?? anchor?.mass,.5),
    contactArea: numberValue(anchor?.contactArea?.anchor ?? anchor?.contactArea,.5),
    surface: anchor?.surface || "Normal",
    construction: anchor?.construction || "Solid"
  });
  scenario = {
    interactionKind: family?.interactionKind || physicalKindFromCue(cue),
    interactionMode: family?.interactionMode || physicalModeFromCue(cue),
    impactor: body(basis?.impactor), receiver: body(basis?.receiver),
    relativeSpeed: numberValue(basis?.relativeSpeed?.anchor,4), normalSpeed: numberValue(basis?.relativeSpeed?.anchor,4),
    tangentialSpeed: numberValue(basis?.relativeSpeed?.anchor,.4) * .1, pressure: .5, impulse: .5, intensity: numberValue(basis?.intensity?.anchor,.5),
    angleDegrees: numberValue(basis?.angleDegrees?.anchor,90),
    contactNormal:(family?.interactionKind||physicalKindFromCue(cue))==="AirDisplacement"?{x:0,y:0,z:1}:{x:0,y:1,z:0},
    frame: 0, sequence: 0
  };
  state.physicalScenarios.set(cue.audioCueId,scenario);
  return scenario;
}

function physicalKindFromCue(cue) {
  const id = `${cue?.audioCueId || ""} ${(cue?.tags || []).join(" ")}`.toLowerCase();
  if (id.includes("compression") || id.includes("compress")) return "Compression";
  if (id.includes("scrape") || id.includes("scraping")) return "Scrape";
  if (id.includes("roll") || id.includes("rolling")) return "Roll";
  if (id.includes("air_displacement") || id.includes("air displacement") || id.includes("sweep")) return "AirDisplacement";
  if (id.includes("landing")) return "Landing";
  if (id.includes("weapon") || id.includes("attack")) return id.includes("blocked") ? "WeaponBlock" : "WeaponHit";
  if (id.includes("sole") || id.includes("foot")) return "Footstep";
  return "ObjectImpact";
}

function physicalModeFromCue(cue) {
  return ({Footstep:"Step",Landing:"Landing",WeaponHit:"Hit",WeaponBlock:"Block",AirDisplacement:"Sweep",Scrape:"Scrape",Roll:"Roll",Compression:"Compression"})[physicalKindFromCue(cue)] || "Impact";
}

function renderPhysicalResolveResult(result) {
  if (state.physicalResolvePending) return `<div class="physical-resolve-empty"><strong>正在解析…</strong><span>使用当前未保存的 Physical Manifest 和情境事实。</span></div>`;
  if (!result) return `<div class="physical-resolve-empty"><strong>等待情境输入</strong><span>调节任一参数或点击解析；这里不会在浏览器重写选择算法。</span></div>`;
  if (!result.success) return `<div class="physical-resolve-empty error"><strong>未命中 Basis · ${esc(result.code || "NO_MATCH")}</strong><span>${esc(result.message || "请检查覆盖空洞和 family fallback。")}</span></div>${renderResolveTrace(result.trace)}`;
  const cue = findCue(result.audioCueId);
  return `<div class="physical-resolve-winner"><div><span>Authoring 选择结果</span><strong>${esc(result.basisId)}</strong><small>${esc(cue?.displayName || result.audioCueId)} · score ${numberValue(result.score).toFixed(3)}${result.trace?.usedFallback ? " · 使用 fallback" : ""}</small></div><div class="resolve-actions"><button type="button" data-physical-action="open-resolved" data-resolved-cue="${esc(result.audioCueId)}">打开 Cue</button><button type="button" class="primary" data-physical-action="preview-resolved" data-resolved-cue="${esc(result.audioCueId)}">Unity 权威解析并试听</button></div></div><div class="resolved-parameters">${(result.parameters || []).map(item => `<span><b>#${esc(item.parameterId)}</b><strong>${numberValue(item.value).toFixed(3)}</strong></span>`).join("") || `<span>无运行时参数</span>`}</div>${renderPhysicalPreviewStatus()}${renderResolveTrace(result.trace)}`;
}

function renderPhysicalPreviewStatus() {
  const preview = state.physicalPreview;
  if (!preview) return `<div class="physical-preview-authority"><strong>Unity 权威结果尚未运行</strong><span>点击试听后，Unity 会验证 Catalog Hash、Basis/Cue 身份并播放真实 FMOD Event。</span></div>`;
  const pending = preview.status === "pending";
  const succeeded = preview.status === "succeeded";
  const parameters = preview.resolvedParameters || [];
  return `<div class="physical-preview-authority ${succeeded ? "succeeded" : pending ? "pending" : "failed"}"><div><strong>${succeeded ? "Unity Runtime 解析一致" : pending ? "等待 Unity Runtime…" : "Unity Runtime 解析失败"}</strong><span>${esc(preview.message || "")}</span></div>${succeeded ? `<small>Basis ${esc(preview.resolvedBasisId)} #${esc(preview.resolvedRuntimeBasisId)} · Cue ${esc(preview.resolvedAudioCueId)} #${esc(preview.resolvedRuntimeCueId)} · distance ${numberValue(preview.matchDistance).toFixed(3)}${preview.usedFallback ? " · fallback" : ""}</small><div class="resolved-parameters">${parameters.map(item => `<span><b>${esc(item.name || `#${item.parameterId}`)}</b><strong>${numberValue(item.value).toFixed(3)}</strong></span>`).join("") || `<span>无运行时参数</span>`}</div>` : ""}</div>`;
}

function renderResolveTrace(trace) {
  if (!trace) return "";
  return `<details class="resolve-trace" open><summary>选择轨迹 · ${esc(trace.familyId || "未找到 Family")}</summary><div>${(trace.candidates || []).map(item => `<article class="${item.accepted ? "accepted" : "rejected"}"><strong>${esc(item.basisId)}</strong><span>${item.accepted ? `score ${numberValue(item.score).toFixed(3)}` : esc(item.reason || "不匹配")}</span></article>`).join("") || `<p class="notice">没有候选。</p>`}</div></details>`;
}
