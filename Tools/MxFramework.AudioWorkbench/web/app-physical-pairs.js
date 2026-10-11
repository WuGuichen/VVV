const PHYSICAL_VARIANTS = ["Impact","Step","Landing","Hit","Block","Sweep","Scrape","Roll","Compression"];

function renderGlobalPairWorkspace() {
  const materials = activePhysicalMaterials();
  if (!state.selectedMaterialPairKey && materials.length) state.selectedMaterialPairKey = pairKey(materials[0].id,materials[0].id);
  el.libraryTitle.textContent = "公共材质";
  el.newCueButton.hidden = true;
  el.searchInput.placeholder = "筛选材质";
  el.categoryFilter.hidden = true;
  el.syncFilter.hidden = true;
  el.statusFilter.innerHTML = `<option value="all">全部 Pair 状态</option><option value="required-missing">Required 缺失</option><option value="partial">部分变体</option><option value="ready">Ready</option><option value="fallback">仅 fallback</option>`;
  if (!["all","required-missing","partial","ready","fallback"].includes(state.statusFilter)) state.statusFilter = "all";
  state.pairStatusFilter = state.statusFilter;
  el.statusFilter.value = state.pairStatusFilter;
  const filteredMaterials = materials.filter(item => !state.search || `${item.id} ${item.displayName}`.toLowerCase().includes(state.search.toLowerCase()));
  el.cueSummary.textContent = `${materials.length} 种 Active 材质 · ${(materials.length*(materials.length+1))/2} 个无序组合`;
  el.cueList.innerHTML = filteredMaterials.map(material => {
    const count = (state.physicalManifest?.materialPairs||[]).filter(pair => pair.materialAId===material.id || pair.materialBId===material.id).length;
    return `<button type="button" class="cue-row material-row" data-pair-material-filter="${esc(material.id)}"><strong>${esc(material.displayName||material.id)}</strong><small>${esc(material.id)}</small><span class="meta">${badge(`${count} Pair`,count?"info":"")}</span></button>`;
  }).join("") || empty("没有匹配材质");
  el.cueList.querySelectorAll("[data-pair-material-filter]").forEach(button => button.addEventListener("click",()=>{
    state.search = button.dataset.pairMaterialFilter;
    el.searchInput.value = state.search;
    renderGlobalPairWorkspace();
  }));

  el.duplicateButton.hidden = el.archiveButton.hidden = el.deleteButton.hidden = true;
  el.editorTitle.textContent = "公共材质矩阵";
  el.editorSubtitle.textContent = `${materials.length} 种材质 · ${(materials.length*(materials.length+1))/2} 个全项目共享 Pair · A×B 与 B×A 共用身份`;
  el.cueForm.innerHTML = renderGlobalPairMatrix(materials);
  bindGlobalPairMatrix();
  renderGlobalPairInspector();
}

function activePhysicalMaterials() {
  return (state.physicalManifest?.materialProfiles||[])
    .filter(item=>item.lifecycle==="Active")
    .slice()
    .sort((a,b)=>numberValue(a.runtimeMaterialId)-numberValue(b.runtimeMaterialId));
}

function pairKey(first,second) { return [String(first||""),String(second||"")].sort().join("|"); }

function materialPairFor(first,second) {
  const key=pairKey(first,second);
  return (state.physicalManifest?.materialPairs||[]).find(item=>pairKey(item.materialAId,item.materialBId)===key);
}

function pairBasisSounds(pair,variant="all") {
  if(!pair)return [];
  return (state.physicalManifest?.basisSounds||[]).filter(item=>sameId(item.materialPairId,pair.id)&&(variant==="all"||item.variantKind===variant));
}

function requiredVariantsForPair(pair) {
  const result=new Set();
  if(!pair)return result;
  for(const requirement of state.physicalManifest?.coverageRequirements||[]) {
    if(requirement.required!==false&&(requirement.materialPairIds||[]).some(id=>sameId(id,pair.id)))
      for(const variant of requirement.variantKinds||[])result.add(variant);
  }
  return result;
}

function basisMatrixReady(basis) {
  const cue=findCue(basis?.audioCueId); if(!basis||!cue||basis.lifecycle!=="Active")return false;
  const set=(state.generationManifest?.adoptionSets||[]).find(item=>sameId(item.setId,basis.adoptionSetId));
  const evidence=cue.auditionEvidence||{};
  return set?.status==="Applied"&&evidence.status==="Approved"&&sameId(evidence.audioBasisId,basis.id)&&sameId(evidence.appliedSetId,basis.adoptionSetId);
}

function appliedSetForBasis(basis) {
  if(!basis)return null;
  const cue=findCue(basis.audioCueId),eventState=(state.generationManifest?.eventStates||[]).find(item=>sameId(item.eventDefinitionId,cue?.eventDefinitionId));
  const setId=basis.adoptionSetId||eventState?.activeSetId;
  return (state.generationManifest?.adoptionSets||[]).find(item=>sameId(item.setId,setId)&&item.status==="Applied")||null;
}

function fallbackPairFor(first,second,variant) {
  const materials=state.physicalManifest?.materialProfiles||[],chain=id=>{const out=[],seen=new Set();let current=id;while(current&&!seen.has(current)&&out.length<=materials.length){seen.add(current);out.push(current);current=materials.find(item=>sameId(item.id,current))?.fallbackMaterialId||"";}return out;};
  const a=chain(first),b=chain(second); let best=null;
  for(let i=0;i<a.length;i++)for(let j=0;j<b.length;j++){
    if(i===0&&j===0)continue;
    const pair=materialPairFor(a[i],b[j]); if(!pair||!pairBasisSounds(pair,variant).length)continue;
    const rank=[i+j,Math.max(i,j),numberValue(pair.runtimeMaterialPairId)];
    if(!best||rank[0]<best.rank[0]||rank[0]===best.rank[0]&&rank[1]<best.rank[1]||rank[0]===best.rank[0]&&rank[1]===best.rank[1]&&rank[2]<best.rank[2])best={pair,rank};
  }
  return best?.pair||null;
}

function pairMatrixState(materialA,materialB) {
  const pair=materialPairFor(materialA.id,materialB.id),variant=state.pairVariantFilter;
  const required=requiredVariantsForPair(pair),variants=variant==="all"?PHYSICAL_VARIANTS:[variant];
  const relevantRequired=[...required].filter(item=>variant==="all"||item===variant);
  const exact=variants.flatMap(item=>pairBasisSounds(pair,item));
  const readyVariants=variants.filter(item=>pairBasisSounds(pair,item).some(basisMatrixReady));
  const missingRequired=relevantRequired.filter(item=>!pairBasisSounds(pair,item).some(basisMatrixReady));
  const fallback=variant!=="all"&&!exact.length?fallbackPairFor(materialA.id,materialB.id,variant):null;
  let status="neutral";
  if(missingRequired.length)status="required-missing";
  else if(relevantRequired.length&&readyVariants.length===relevantRequired.length)status="ready";
  else if(fallback)status="fallback";
  else if(exact.length)status="partial";
  return {pair,required,relevantRequired,exact,readyVariants,missingRequired,fallback,status};
}

function renderGlobalPairMatrix(materials) {
  const visible=(left,right)=>{
    if(state.search&&!`${left.id} ${left.displayName} ${right.id} ${right.displayName}`.toLowerCase().includes(state.search.toLowerCase()))return false;
    const status=pairMatrixState(left,right).status;
    return state.pairStatusFilter==="all"||status===state.pairStatusFilter;
  };
  const cells=[];
  cells.push(`<span class="global-pair-corner">A × B</span>`,...materials.map(item=>`<span class="global-pair-axis column" title="${esc(item.displayName)}">${esc(item.displayName)}</span>`));
  for(let row=0;row<materials.length;row++){
    cells.push(`<span class="global-pair-axis row">${esc(materials[row].displayName)}</span>`);
    for(let column=0;column<materials.length;column++){
      if(column<row){cells.push(`<span class="global-pair-cell mirrored" aria-hidden="true"></span>`);continue;}
      const left=materials[row],right=materials[column],info=pairMatrixState(left,right),key=pairKey(left.id,right.id),selected=key===state.selectedMaterialPairKey;
      const hidden=!visible(left,right),variantCount=new Set(pairBasisSounds(info.pair).map(item=>item.variantKind)).size;
      const title=`${left.displayName} × ${right.displayName} · ${info.status}${info.fallback?` → ${info.fallback.id}`:""}`;
      cells.push(`<button type="button" class="global-pair-cell ${info.status}${selected?" selected":""}${hidden?" filtered":""}" data-global-pair="${esc(key)}" data-material-a="${esc(left.id)}" data-material-b="${esc(right.id)}" aria-pressed="${selected}" title="${esc(title)}"><strong>${info.pair?variantCount:"+"}</strong><small>${info.required.size?`${info.readyVariants.length}/${info.required.size}`:info.fallback?"fallback":info.pair?"已建":"未纳入"}</small></button>`);
    }
  }
  return `<section class="section global-pair-workspace"><div class="workspace-section-head"><div><p class="eyebrow">项目级公共权威</p><h3>无序材质对 · 105</h3><p>只显示上三角；运行时仍保留碰撞体/受体方向用于质量、速度、面积与响应调制。</p></div><button type="button" data-physical-action="coverage">重新校验 Required</button></div>
    <div class="global-pair-usage"><strong>怎么使用</strong><span><b>1</b> 选择材质格</span><span><b>2</b> 选择 Impact / Step 等动作</span><span><b>3</b> 试听当前 FMOD 或进入素材工作区编辑</span><span><b>4</b> 应用后再做 Unity 权威试听</span></div>
    <div class="global-pair-controls"><label>矩阵显示<select data-pair-filter="variant"><option value="all">全部动作</option>${PHYSICAL_VARIANTS.map(value=>`<option${state.pairVariantFilter===value?" selected":""}>${value}</option>`).join("")}</select></label><div class="global-pair-legend"><span class="ready">Ready</span><span class="partial">部分变体</span><span class="required-missing">Required 缺失</span><span class="fallback">仅 fallback</span><span>未纳入</span></div></div>
    <div class="global-pair-selection-bar" data-pair-selection-bar>${renderGlobalPairSelectionBar()}</div>
    <div class="global-pair-matrix-scroll"><div class="global-pair-matrix" style="--pair-material-count:${materials.length}">${cells.join("")}</div></div></section>`;
}

function renderGlobalPairSelectionBar() {
  const context=selectedMaterialPairContext();
  if(!context)return `<span><strong>尚未选择材质对</strong><small>点击矩阵上三角中的格子开始。</small></span>`;
  const bases=pairBasisSounds(context.pair,state.selectedPairVariant),basis=bases[0],applied=appliedSetForBasis(basis);
  return `<span><strong>已选择：${esc(context.materialA.displayName)} × ${esc(context.materialB.displayName)} · ${esc(state.selectedPairVariant)}</strong><small>点击格子只改变选中项，不会重新载入或跳离矩阵。</small></span><div class="row-actions"><button type="button" data-pair-quick="preview"${basis?"":" disabled"}>${applied?"试听当前 FMOD":"试听可用候选"} <kbd>Space</kbd></button><button type="button" class="primary" data-pair-quick="edit">${basis?"编辑 / 选择候选":"创建动作变体"}</button><button type="button" data-pair-quick="simulate"${basis?"":" disabled"}>模拟游戏情境</button></div>`;
}

function updateGlobalPairSelectionBar() {
  const target=el.cueForm.querySelector("[data-pair-selection-bar]");
  if(!target)return;
  target.innerHTML=renderGlobalPairSelectionBar();
  target.querySelectorAll("[data-pair-quick]").forEach(button=>button.addEventListener("click",()=>pairInspectorAction(button.dataset.pairQuick)));
}

function bindGlobalPairMatrix() {
  el.cueForm.querySelectorAll("[data-global-pair]").forEach(button=>button.addEventListener("click",()=>{
    if(state.selectedMaterialPairKey===button.dataset.globalPair)return;
    el.cueForm.querySelector("[data-global-pair].selected")?.classList.remove("selected");
    el.cueForm.querySelectorAll("[data-global-pair][aria-pressed='true']").forEach(item=>item.setAttribute("aria-pressed","false"));
    state.selectedMaterialPairKey=button.dataset.globalPair;
    button.classList.add("selected");button.setAttribute("aria-pressed","true");
    const url=new URL(location.href);url.searchParams.set("pair",state.selectedMaterialPairKey);history.replaceState(null,"",url);
    updateGlobalPairSelectionBar();
    renderGlobalPairInspector();
  }));
  el.cueForm.querySelectorAll("[data-pair-filter]").forEach(select=>select.addEventListener("change",()=>{state.pairVariantFilter=select.value;renderGlobalPairWorkspace();}));
  el.cueForm.querySelectorAll('[data-physical-action="coverage"]').forEach(button=>button.addEventListener("click",refreshPhysicalCoverage));
  updateGlobalPairSelectionBar();
}

function selectedMaterialPairContext() {
  const [first,second]=String(state.selectedMaterialPairKey||"").split("|");
  const materials=activePhysicalMaterials();
  const materialA=materials.find(item=>sameId(item.id,first)),materialB=materials.find(item=>sameId(item.id,second));
  return materialA&&materialB?{materialA,materialB,pair:materialPairFor(materialA.id,materialB.id)}:null;
}

function renderGlobalPairInspector() {
  const context=selectedMaterialPairContext();
  const inspectorHead=document.querySelector(".next-step-card .panel-head");
  if(inspectorHead){inspectorHead.querySelector(".eyebrow").textContent="项目级公共权威";inspectorHead.querySelector("h2").textContent="Pair 与动作变体";}
  el.referenceSummary.textContent="全局公共配置";
  el.diffPanel.innerHTML=el.auditionPanel.innerHTML=el.diagnosticsPanel.innerHTML=el.runtimePanel.innerHTML="";
  if(!context){el.nextStepPanel.innerHTML=empty("从矩阵选择一个材质对");return;}
  const {materialA,materialB,pair}=context,required=requiredVariantsForPair(pair);
  const selectedVariantBasis=pairBasisSounds(pair,state.selectedPairVariant);
  const variants=PHYSICAL_VARIANTS.map(variant=>{
    const bases=pairBasisSounds(pair,variant),isRequired=required.has(variant),selected=state.selectedPairVariant===variant;
    return `<article class="global-pair-variant${selected?" selected":""}"><button type="button" data-pair-select-variant="${variant}"><strong>${variant}</strong><small>${bases.length?`${bases.length} 个 Basis`:isRequired?"Required · 缺失":"未制作"} · ${isRequired?"Required":"Optional"}</small></button><div class="global-pair-bases">${bases.map(basis=>`<button type="button" data-pair-open-cue="${esc(basis.audioCueId)}"><strong>${esc(basis.displayName||basis.id)}</strong><small>${esc(basis.audioCueId)} · #${esc(basis.runtimeBasisId)} · ${basisMatrixReady(basis)?"Ready":"待应用/审听"}</small></button>`).join("")||`<small>${isRequired?"需要创建可播放实现":"可按玩法需要逐步补充"}</small>`}</div></article>`;
  }).join("");
  const basis=selectedVariantBasis[0],applied=appliedSetForBasis(basis),preview=state.pairPreview?.audioCueId===basis?.audioCueId?state.pairPreview:null;
  const previewStatus=preview?`<div class="global-pair-preview-status ${preview.status||"pending"}"><strong>${preview.status==="succeeded"?preview.sourceKind==="candidate"?"候选试听已开始":"当前 FMOD 试听成功":preview.status==="failed"?"试听失败":preview.sourceKind==="candidate"?"正在载入候选":"Unity / FMOD 正在试听"}</strong><small>${esc(preview.message||"正在准备声音来源。")}</small>${preview.candidateUrl?`<audio data-pair-preview-audio controls preload="auto" src="${esc(preview.candidateUrl)}"></audio>`:""}</div>`:"";
  el.nextStepPanel.innerHTML=`<section class="global-pair-inspector"><header><p class="eyebrow">全局公共 Pair</p><h3>${esc(materialA.displayName)} <span>×</span> ${esc(materialB.displayName)}</h3><small>${pair?`${esc(pair.id)} · runtime #${esc(pair.runtimeMaterialPairId)}`:"尚未创建稳定 Pair 身份"}</small></header><div class="global-pair-materials">${[materialA,materialB].map((material,index)=>`<article class="global-pair-material"><span>Side ${index?"B":"A"}</span><strong>${esc(material.displayName)}</strong><div class="global-pair-features"><span>硬度<b>${numberValue(material.hardness).toFixed(2)}</b></span><span>密度<b>${numberValue(material.density).toFixed(2)}</b></span><span>粗糙<b>${numberValue(material.roughness).toFixed(2)}</b></span></div><small>fallback → ${esc(material.fallbackMaterialId||"无")}</small></article>${index===0?"<i>×</i>":""}`).join("")}</div>${pair?`<div class="global-pair-variants">${variants}</div>`:`<button type="button" class="primary" data-pair-action="create">创建 Pair</button>`}<div data-managed-physical-status>${renderManagedPhysicalEventStatus(state.managedPhysicalEvent)}</div>${pair&&basis?`<div class="global-pair-source ${applied?"applied":"candidate"}"><span>当前试听来源</span><strong>${applied?"已应用 FMOD Event":"生成候选（尚未应用到 FMOD）"}</strong><small>${applied?esc(applied.setId):"试听候选用于选材；保存并应用替换后，才会由 FMOD Event 播放。"}</small></div><div class="global-pair-primary-actions"><button type="button" class="primary" data-pair-action="preview">${applied?"试听当前 FMOD Event":"试听可用候选"} <kbd>Space</kbd></button><button type="button" data-pair-action="stop-preview">停止</button><button type="button" data-pair-action="edit">编辑配方 / 生成素材</button><button type="button" data-pair-action="candidates">查看和选择候选</button></div>${previewStatus}`:""}<div class="global-pair-actions">${pair?`<button type="button" data-pair-action="toggle-required">${required.has(state.selectedPairVariant)?"取消":"设为"} ${state.selectedPairVariant} Required</button><button type="button" data-pair-action="use-cue">${selectedVariantBasis.length?"查看 Basis 配置":"创建动作变体"}</button><button type="button" class="wide" data-pair-action="simulate">模拟游戏属性与选择结果</button>`:""}</div><p class="notice">矩阵负责选择公共 Pair 与动作；未应用时试听生成候选，应用后试听当前 Bank 中的 FMOD Event。Space 与主试听按钮行为一致。</p></section>`;
  el.nextStepPanel.querySelectorAll("[data-pair-select-variant]").forEach(button=>button.addEventListener("click",()=>{state.selectedPairVariant=button.dataset.pairSelectVariant;updateGlobalPairSelectionBar();renderGlobalPairInspector();}));
  el.nextStepPanel.querySelectorAll("[data-pair-open-cue]").forEach(button=>button.addEventListener("click",()=>openPairCue(button.dataset.pairOpenCue,"basis")));
  el.nextStepPanel.querySelectorAll("[data-pair-action]").forEach(button=>button.addEventListener("click",()=>pairInspectorAction(button.dataset.pairAction)));
  if(basis&&!applied)primePairCandidateChoices(basis.audioCueId).catch(()=>{});
}

function createSelectedMaterialPair() {
  const context=selectedMaterialPairContext();if(!context||context.pair)return context?.pair;
  const ordered=[context.materialA,context.materialB].sort((a,b)=>numberValue(a.runtimeMaterialId)-numberValue(b.runtimeMaterialId));
  const allocation=state.physicalManifest.idAllocation||=( {} );allocation.nextMaterialPairId=Math.max(550001,numberValue(allocation.nextMaterialPairId,550001));
  const token=id=>String(id).replace(/^audio\.material\./,"").replace(/[^a-z0-9._-]+/gi,"-");
  const pair={id:`audio.pair.${token(ordered[0].id)}--${token(ordered[1].id)}`,runtimeMaterialPairId:allocation.nextMaterialPairId++,materialAId:ordered[0].id,materialBId:ordered[1].id,lifecycle:"Active",metadata:{}};
  (state.physicalManifest.materialPairs||=[]).push(pair);state.physicalCoverageStale=true;updateSaveIndicator();return pair;
}

async function pairInspectorAction(action) {
  const context=selectedMaterialPairContext();if(!context)return;
  const pair=context.pair||createSelectedMaterialPair();
  if(action==="create"){renderGlobalPairWorkspace();return;}
  if(action==="toggle-required"){
    const requirements=state.physicalManifest.coverageRequirements||=([]);
    let requirement=requirements.find(item=>item.required!==false&&(item.materialPairIds||[]).length===1&&sameId(item.materialPairIds[0],pair.id));
    if(!requirement){requirement={id:`coverage.${pair.id.replace(/^audio\.pair\./,"")}`,familyId:"",materialPairIds:[pair.id],variantKinds:[],impactorMaterialIds:[],receiverMaterialIds:[],facts:[],scenarioPresetIds:[],required:true};requirements.push(requirement);}
    requirement.variantKinds||=[];const index=requirement.variantKinds.indexOf(state.selectedPairVariant);if(index>=0)requirement.variantKinds.splice(index,1);else requirement.variantKinds.push(state.selectedPairVariant);
    if(!requirement.variantKinds.length)requirements.splice(requirements.indexOf(requirement),1);
    state.physicalCoverageStale=true;updateSaveIndicator();renderGlobalPairWorkspace();return;
  }
  if(action==="use-cue"){
    const family=(state.physicalManifest.families||[]).find(item=>item.variantKind===state.selectedPairVariant);
    const basis=(state.physicalManifest.basisSounds||[]).find(item=>sameId(item.materialPairId,pair.id)&&item.variantKind===state.selectedPairVariant);
    if(basis){openPairCue(basis.audioCueId,"basis");return;}
    if(isDirty()){setServer("请先保存公共 Pair，再增加动作变体。","warn");return;}
    if(!family){setServer(`找不到 ${state.selectedPairVariant} 对应的 Physical Audio Family。`,"error");return;}
    const leaf=pair.id.replace(/^audio\.pair\./,"").replace(/[^a-z0-9._-]+/gi,".");
    let basisId=`audio.basis.generated.${leaf}.${state.selectedPairVariant.toLowerCase()}`,suffix=2;
    while((state.physicalManifest.basisSounds||[]).some(item=>sameId(item.id,basisId)))basisId=`audio.basis.generated.${leaf}.${state.selectedPairVariant.toLowerCase()}.${suffix++}`;
    const templateDraft={familyId:family.id,basisId,createBasis:true};
    const templateEventDefinitionId=managedPhysicalDefaultTemplateId(state.manifest?.eventDefinitions||[],templateDraft,null);
    if(!templateEventDefinitionId){setServer("当前 Snapshot 中没有可复制技术事实的 OneShot Event 模板。","error");return;}
    try{
      setServer(`正在为 ${pair.id} 增加 ${state.selectedPairVariant} 动作变体…`,"");
      const status=await api("/api/authoring/audio/physical/managed-event",{method:"POST",body:JSON.stringify({
        package:state.package,expectedSemanticHash:state.contentHash,expectedPhysicalHash:state.physicalManifestHash,
        familyId:family.id,materialPairId:pair.id,variantKind:state.selectedPairVariant,basisId,createBasis:true,
        templateEventDefinitionId,displayName:`${context.materialA.displayName} × ${context.materialB.displayName} · ${state.selectedPairVariant}`
      })});
      state.managedPhysicalEvent=status;rememberManagedPhysicalOperation(status.operationId);renderGlobalPairInspector();
      if(status.terminal)await finishManagedPhysicalEvent(status);else scheduleManagedPhysicalEventPoll(status.operationId);
    }catch(error){state.managedPhysicalEvent={stage:"Failed",progress:0,terminal:true,code:error.code||"AUTH_AUDIO_MANAGED_EVENT_FAILED",message:error.message,detail:error.detail||""};setServer(error.message,"error");renderGlobalPairInspector();}
    return;
  }
  if(action==="edit"||action==="candidates"){
    const basis=(state.physicalManifest.basisSounds||[]).find(item=>sameId(item.materialPairId,pair.id)&&item.variantKind===state.selectedPairVariant);
    if(basis)openPairCue(basis.audioCueId,action==="edit"?"recipe":"candidates");
    else await pairInspectorAction("use-cue");
    return;
  }
  if(action==="preview"||action==="stop-preview"){
    const basis=(state.physicalManifest.basisSounds||[]).find(item=>sameId(item.materialPairId,pair.id)&&item.variantKind===state.selectedPairVariant);
    if(!basis){setServer(`请先为 ${pair.id} · ${state.selectedPairVariant} 创建 Basis。`,"warn");return;}
    if(action==="stop-preview")await stopPairPreview(basis.audioCueId);else await previewPairSelection();
    return;
  }
  if(action==="simulate"){
    const basis=(state.physicalManifest.basisSounds||[]).find(item=>sameId(item.materialPairId,pair.id)&&item.variantKind===state.selectedPairVariant);
    if(basis)openPairCue(basis.audioCueId,"scenario");
    else setServer(`请先为 ${pair.id} · ${state.selectedPairVariant} 创建 Basis。`,"warn");
  }
}

async function previewPairSelection() {
  const context=selectedMaterialPairContext(),basis=context&&pairBasisSounds(context.pair,state.selectedPairVariant)[0];
  if(!basis)return;
  if(appliedSetForBasis(basis)){await previewPairBasis(basis.audioCueId);return;}
  await previewPairCandidate(basis.audioCueId);
}

async function previewPairCandidate(audioCueId) {
  stopPairCandidate();
  state.pairPreview={status:"pending",sourceKind:"candidate",audioCueId,message:"正在读取该 Basis 最近生成的候选。"};renderGlobalPairInspector();
  try {
    // The inspector primes this metadata in the background. With a warm cache,
    // Audio.play() runs in the original click/Space task and Safari accepts the
    // user gesture instead of treating it as autoplay.
    const choices=state.pairCandidateChoices.get(audioCueId)||await primePairCandidateChoices(audioCueId);
    const selected=choices.find(item=>item.candidate.status==="Ready")||choices[0];
    if(!selected)throw new Error("该动作还没有生成候选，请先进入素材工作区生成。");
    const sourceUrl=candidateUrl(selected.job.jobId,selected.candidate.candidateId);
    const diagnostic=selected.candidate.status!=="Ready";
    state.pairPreview={status:"succeeded",sourceKind:"candidate",audioCueId,jobId:selected.job.jobId,candidateId:selected.candidate.candidateId,candidateUrl:sourceUrl,message:diagnostic?`正在试听技术异常候选 ${selected.candidate.candidateId}（${selected.candidate.status}），仅供排查，不能采用。`:`正在试听可用候选 ${selected.candidate.candidateId}；尚未应用到 FMOD。`};
    renderGlobalPairInspector();
    const source=el.nextStepPanel.querySelector("[data-pair-preview-audio]");
    if(!source)throw new Error("候选播放器创建失败。");
    state.pairCandidateSource=source;
    const playback=source.play();
    source.onended=()=>{if(state.pairCandidateSource===source)state.pairCandidateSource=null;};
    playback.catch(error=>{if(state.pairCandidateSource!==source)return;state.pairPreview={...state.pairPreview,status:"failed",message:`浏览器阻止了自动播放，请点击下方播放器的播放按钮。${error.message?`（${error.message}）`:""}`};renderGlobalPairInspector();});
  } catch(error) { state.pairPreview={status:"failed",sourceKind:"candidate",audioCueId,message:error.message};setServer(error.message,"error");renderGlobalPairInspector(); }
}

function primePairCandidateChoices(audioCueId) {
  if(state.pairCandidateChoices.has(audioCueId))return Promise.resolve(state.pairCandidateChoices.get(audioCueId));
  if(state.pairCandidateLoads.has(audioCueId))return state.pairCandidateLoads.get(audioCueId);
  const load=api(`/api/authoring/audio/generation/jobs?audioCueId=${encodeURIComponent(audioCueId)}`).then(jobs=>{
    const choices=jobs.flatMap(job=>(job.candidates||[]).map(candidate=>({job,candidate})));
    state.pairCandidateChoices.set(audioCueId,choices);state.pairCandidateLoads.delete(audioCueId);return choices;
  }).catch(error=>{state.pairCandidateLoads.delete(audioCueId);throw error;});
  state.pairCandidateLoads.set(audioCueId,load);return load;
}

function stopPairCandidate() {
  if(!state.pairCandidateSource)return;
  try{if(typeof state.pairCandidateSource.stop==="function")state.pairCandidateSource.stop();else state.pairCandidateSource.pause();}catch(_){ }
  try{if(typeof state.pairCandidateSource.disconnect==="function")state.pairCandidateSource.disconnect();}catch(_){ }
  state.pairCandidateSource=null;
}

async function stopPairPreview(audioCueId) {
  stopPairCandidate();
  if(state.pairPreview?.sourceKind==="fmod")await previewPairBasis(audioCueId,true);
  else { state.pairPreview={status:"stopped",sourceKind:"candidate",audioCueId,message:"候选试听已停止。"};renderGlobalPairInspector(); }
}

async function previewPairBasis(audioCueId,stop=false) {
  const cue=findCue(audioCueId),eventDef=cue&&findEvent(cue.eventDefinitionId);
  if(!cue||!eventDef){setServer("当前动作没有可试听的 Cue / Event 绑定。","warn");return;}
  const endpoint=stop?"/api/authoring/audio/stop-preview":"/api/authoring/audio/preview";
  try {
    const eventBindingHash=await computeBindingHash(cue,eventDef);
    const result=await api(endpoint,{method:"POST",body:JSON.stringify({package:state.package,audioCueId:cue.audioCueId,fmodEventPath:eventDef.fmodEventPath,fmodEventGuid:eventDef.fmodEventGuid,parameters:Object.fromEntries((cue.defaultParameters||[]).map(item=>[item.name,item.value])),position:{x:0,y:1,z:2},eventBindingHash})});
    state.preview={...result,audioCueId:cue.audioCueId};state.pairPreview={...result,sourceKind:"fmod",audioCueId:cue.audioCueId};renderGlobalPairInspector();
    if(!stop)pollPairPreview(result.requestId,cue.audioCueId);
  } catch(error) { state.preview={status:"failed",message:error.message,audioCueId:cue.audioCueId};state.pairPreview={...state.preview,sourceKind:"fmod"};setServer(error.message,"error");renderGlobalPairInspector(); }
}

async function pollPairPreview(requestId,audioCueId) {
  clearTimeout(state.previewPoll);
  try {
    const result=await api(`/api/authoring/audio/preview-status?id=${encodeURIComponent(requestId)}`);
    state.preview={...result,audioCueId};state.pairPreview={...result,sourceKind:"fmod",audioCueId};
    if(state.workspaceMode==="pairs")renderGlobalPairInspector();
    if(result.status==="pending")state.previewPoll=setTimeout(()=>pollPairPreview(requestId,audioCueId),500);
  } catch(error) { state.preview={status:"failed",message:error.message,audioCueId};state.pairPreview={...state.preview,sourceKind:"fmod"};if(state.workspaceMode==="pairs")renderGlobalPairInspector(); }
}

function openPairCue(cueId,tab) { state.appScreen=["recipe","generate","candidates"].includes(tab)?"candidates":tab==="basis"?"basis":tab==="scenario"?"preview":"cues";state.workspaceMode="cues";state.selectedCueId=cueId;state.workspaceTabs.set(cueId,tab);const url=new URL(location.href);url.searchParams.delete("mode");url.searchParams.set("screen",state.appScreen);url.searchParams.set("cue",cueId);history.replaceState(null,"",url);render(); }

function locateBasisPair(basis=currentBasisForCue()) {
  if(!basis?.materialPairId)return;
  const pair=(state.physicalManifest?.materialPairs||[]).find(item=>sameId(item.id,basis.materialPairId));
  if(!pair)return;
  state.appScreen="matrix";
  state.workspaceMode="pairs";
  state.selectedMaterialPairKey=pairKey(pair.materialAId,pair.materialBId);
  state.selectedPairVariant=basis.variantKind||"Impact";
  state.search="";state.statusFilter="all";
  const url=new URL(location.href);url.searchParams.set("screen","matrix");url.searchParams.set("mode","pairs");url.searchParams.set("pair",state.selectedMaterialPairKey);history.replaceState(null,"",url);
  render();
}
