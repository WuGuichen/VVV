function bindPhysicalAnalysisPanel(root) {
  root.querySelectorAll("[data-physical-analysis-config]").forEach(input => input.addEventListener("change", () => updatePhysicalAnalysisConfig(input.dataset.physicalAnalysisConfig,input.value)));
  root.querySelectorAll("[data-physical-analysis-action]").forEach(button => button.addEventListener("click", () => physicalAnalysisAction(button.dataset.physicalAnalysisAction,button.dataset)));
}

function updatePhysicalAnalysisConfig(path, value) {
  const cue=selectedCue(); if(!cue)return;
  const analysis=physicalAnalysisForCue(cue), [scope,key]=String(path).split(".");
  const config=scope==="sweep"?analysis.sweepConfig:analysis.heatmapConfig;
  if(!config||!key)return;
  config[key]=["samples","size"].includes(key)?Math.trunc(numberValue(value)):key.endsWith("Min")||key.endsWith("Max")||["min","max"].includes(key)?numberValue(value):value;
  if(key==="axis"){const axis=PHYSICAL_ANALYSIS_AXES[value];if(axis?.type!=="material"){config.min=axis.min;config.max=axis.max;}}
  if(key==="xAxis"){const axis=PHYSICAL_ANALYSIS_AXES[value];if(axis?.type!=="material"){config.xMin=axis.min;config.xMax=axis.max;}if(config.yAxis===value){config.yAxis=value==="intensity"?"relativeSpeed":"intensity";const next=PHYSICAL_ANALYSIS_AXES[config.yAxis];config.yMin=next.min;config.yMax=next.max;}}
  if(key==="yAxis"){const axis=PHYSICAL_ANALYSIS_AXES[value];if(axis?.type!=="material"){config.yMin=axis.min;config.yMax=axis.max;}if(config.xAxis===value){config.xAxis=value==="relativeSpeed"?"intensity":"relativeSpeed";const next=PHYSICAL_ANALYSIS_AXES[config.xAxis];config.xMin=next.min;config.xMax=next.max;}}
  renderPhysicalAnalysisTarget(cue.audioCueId);
}

async function physicalAnalysisAction(action,dataset) {
  if(action==="run-sweep"){await runPhysicalSweep();return;}
  if(action==="run-heatmap"){await runPhysicalHeatmap();return;}
  if(action==="apply-sweep"){applyPhysicalSweepPoint(Number(dataset.analysisIndex));return;}
  if(action==="apply-heatmap")applyPhysicalHeatmapCell(Number(dataset.analysisX),Number(dataset.analysisY));
}

function renderPhysicalAnalysisTarget(cueId) {
  const cue=findCue(cueId); if(!cue||selectedCue()?.audioCueId!==cueId)return;
  const target=el.cueForm.querySelector("[data-physical-analysis-panel]");
  if(!target)return;
  target.innerHTML=renderPhysicalAnalysisPanel(cue);
  bindPhysicalAnalysisPanel(target);
}

function physicalAnalysisSignature(cue, ignoredAxes=[]) {
  const fact=structuredClone(physicalScenarioForCue(cue));
  for(const path of ignoredAxes)writeAnalysisAxisValue(fact,path,PHYSICAL_ANALYSIS_AXES[path]?.type==="material"?"__swept__":0);
  return stableText({ fact, physicalManifest:state.physicalManifest });
}

function readAnalysisAxisValue(fact,path) {
  const parts=String(path).split("."); let value=fact;
  for(const part of parts)value=value?.[part];
  return value;
}

function writeAnalysisAxisValue(fact,path,value) {
  const parts=String(path).split("."); let target=fact;
  for(let index=0;index<parts.length-1;index++)target=target[parts[index]];
  target[parts[parts.length-1]]=PHYSICAL_ANALYSIS_AXES[path]?.type==="material"?String(value):numberValue(value);
}

function analysisValuesForAxis(cue,scenario,path,min,max,count) {
  const axis=PHYSICAL_ANALYSIS_AXES[path];
  if(axis?.type==="material")return relevantAnalysisMaterials(cue,scenario,path.startsWith("impactor")?"impactor":"receiver");
  const low=Math.min(numberValue(min,axis?.min),numberValue(max,axis?.max)),high=Math.max(numberValue(min,axis?.min),numberValue(max,axis?.max)),size=Math.max(2,Math.min(11,Math.trunc(numberValue(count,7))));
  return Array.from({length:size},(_,index)=>low+(high-low)*(index/(size-1)));
}

function relevantAnalysisMaterials(cue,scenario,side) {
  const basis=currentBasisForCue(cue), family=basis&&(state.physicalManifest?.families||[]).find(item=>sameId(item.id,basis.familyId));
  const ids=[]; const add=id=>{if(id&&!ids.some(item=>sameId(item,id)))ids.push(id);};
  add(scenario?.[side]?.materialId);
  for(const item of state.physicalManifest?.basisSounds||[])if(!family||sameId(item.familyId,family.id))add(item?.[side]?.materialId);
  for(const item of state.physicalManifest?.materialProfiles||[])if(ids.length<8)add(item.id);
  return ids.slice(0,8);
}

function formatAnalysisValue(axis,value) {
  if(axis?.type==="material")return (state.physicalManifest?.materialProfiles||[]).find(item=>sameId(item.id,value))?.displayName||String(value);
  return `${numberValue(value).toFixed(axis?.decimals??2)}${axis?.unit||""}`;
}

function shortAnalysisValue(axis,value) {
  const text=formatAnalysisValue(axis,value);
  return text.length>8?`${text.slice(0,7)}…`:text;
}

async function resolvePhysicalFacts(facts,physicalManifest,onProgress) {
  const results=new Array(facts.length); let next=0,done=0;
  const worker=async()=>{while(true){const index=next++;if(index>=facts.length)return;try{const response=await api("/api/authoring/audio/physical/resolve",{method:"POST",body:JSON.stringify({physicalManifest,fact:facts[index]})});results[index]=response.resolved||response;}catch(error){results[index]={success:false,code:error.code||"RESOLVE_REQUEST_FAILED",message:error.message,trace:null};}done++;onProgress?.(done,facts.length);}};
  await Promise.all(Array.from({length:Math.min(4,facts.length)},worker));
  return results;
}

async function runPhysicalSweep() {
  const cue=selectedCue();if(!cue)return;
  const analysis=physicalAnalysisForCue(cue);if(analysis.pendingKind)return;
  const config=analysis.sweepConfig;
  const signature=physicalAnalysisSignature(cue,[config.axis]),physicalManifest=structuredClone(state.physicalManifest),base=structuredClone(physicalScenarioForCue(cue)),values=analysisValuesForAxis(cue,base,config.axis,config.min,config.max,config.samples);
  const facts=values.map(value=>{const fact=structuredClone(base);writeAnalysisAxisValue(fact,config.axis,value);return fact;});
  const runId=++state.physicalAnalysisSequence;analysis.activeRunId=runId;analysis.pendingKind="sweep";analysis.progress=0;renderPhysicalAnalysisTarget(cue.audioCueId);
  const results=await resolvePhysicalFacts(facts,physicalManifest,(done,total)=>{if(analysis.activeRunId!==runId)return;const progress=Math.round(done*100/total);if(progress===100||progress-analysis.progress>=10){analysis.progress=progress;renderPhysicalAnalysisTarget(cue.audioCueId);}});
  if(analysis.activeRunId!==runId)return;
  analysis.sweep={signature,config:structuredClone(config),axis:config.axis,points:values.map((value,index)=>({value,result:results[index]}))};analysis.pendingKind="";analysis.progress=100;renderPhysicalAnalysisTarget(cue.audioCueId);
}

async function runPhysicalHeatmap() {
  const cue=selectedCue();if(!cue)return;
  const analysis=physicalAnalysisForCue(cue);if(analysis.pendingKind)return;
  const config=analysis.heatmapConfig,signature=physicalAnalysisSignature(cue,[config.xAxis,config.yAxis]),physicalManifest=structuredClone(state.physicalManifest),base=structuredClone(physicalScenarioForCue(cue));
  const xValues=analysisValuesForAxis(cue,base,config.xAxis,config.xMin,config.xMax,config.size),yValues=analysisValuesForAxis(cue,base,config.yAxis,config.yMin,config.yMax,config.size);
  const cells=[];const facts=[];
  for(const yValue of yValues)for(const xValue of xValues){const fact=structuredClone(base);writeAnalysisAxisValue(fact,config.xAxis,xValue);writeAnalysisAxisValue(fact,config.yAxis,yValue);facts.push(fact);cells.push({xValue,yValue,result:null});}
  if(facts.length>64){setServer("热图最多允许 64 个解析点，请缩小材质范围或连续轴精度。","error");return;}
  const runId=++state.physicalAnalysisSequence;analysis.activeRunId=runId;analysis.pendingKind="heatmap";analysis.progress=0;renderPhysicalAnalysisTarget(cue.audioCueId);
  const results=await resolvePhysicalFacts(facts,physicalManifest,(done,total)=>{if(analysis.activeRunId!==runId)return;const progress=Math.round(done*100/total);if(progress===100||progress-analysis.progress>=8){analysis.progress=progress;renderPhysicalAnalysisTarget(cue.audioCueId);}});
  if(analysis.activeRunId!==runId)return;
  cells.forEach((cell,index)=>cell.result=results[index]);
  analysis.heatmap={signature,config:structuredClone(config),xAxis:config.xAxis,yAxis:config.yAxis,xValues,yValues,cells};analysis.pendingKind="";analysis.progress=100;renderPhysicalAnalysisTarget(cue.audioCueId);
}

function applyPhysicalSweepPoint(index) {
  const cue=selectedCue(),analysis=cue&&physicalAnalysisForCue(cue),run=analysis?.sweep,point=run?.points?.[index];if(!cue||!point)return;
  writeAnalysisAxisValue(physicalScenarioForCue(cue),run.axis,point.value);state.physicalResolve=point.result;state.physicalPreview=null;renderEditor();
}

function applyPhysicalHeatmapCell(xIndex,yIndex) {
  const cue=selectedCue(),analysis=cue&&physicalAnalysisForCue(cue),run=analysis?.heatmap;if(!cue||!run)return;
  const cell=run.cells?.[yIndex*run.xValues.length+xIndex];if(!cell)return;
  const scenario=physicalScenarioForCue(cue);writeAnalysisAxisValue(scenario,run.xAxis,cell.xValue);writeAnalysisAxisValue(scenario,run.yAxis,cell.yValue);state.physicalResolve=cell.result;state.physicalPreview=null;renderEditor();
}

async function physicalAction(action, resolvedCueId = "") {
  if (action === "promote") { await promoteCurrentBasis(); return; }
  if (action === "managed-create") { await createManagedPhysicalEvent(); return; }
  if (action === "locate-pair") { locateBasisPair(); return; }
  if (action === "coverage") { await refreshPhysicalCoverage(); return; }
  if (action === "capture") { await requestPhysicalCapture(); return; }
  if (action === "resolve") { await resolvePhysicalScenario(); return; }
  if (action === "open-resolved" && resolvedCueId) { await selectCue(resolvedCueId); return; }
  if (action === "preview-resolved" && resolvedCueId) await previewPhysicalResolvedCue(resolvedCueId);
}

function updateManagedPhysicalDraft(field,value) {
  const cue=selectedCue(),eventDef=cue&&findEvent(cue.eventDefinitionId);if(!cue)return;
  const draft=managedPhysicalDraftForCue(cue,eventDef);draft[field]=value;
  const target=el.cueForm.querySelector(".managed-event-path code");
  if(target)target.textContent=managedPhysicalEventPath(draft);
}

async function createManagedPhysicalEvent() {
  const cue=selectedCue(),eventDef=cue&&findEvent(cue.eventDefinitionId);if(!cue||isDirty()||(state.managedPhysicalEvent&&!state.managedPhysicalEvent.terminal))return;
  const draft=managedPhysicalDraftForCue(cue,eventDef);
  if(!draft.familyId||!draft.basisId||!/^[A-Za-z0-9._-]+$/.test(draft.basisId)){setServer("Basis ID 只能包含字母、数字、点、下划线和连字符。","error");return;}
  try{
    setServer("正在启动受控 Basis / Cue / Event 创建…","");
    const status=await api("/api/authoring/audio/physical/managed-event",{method:"POST",body:JSON.stringify({
      package:state.package,expectedSemanticHash:state.contentHash,expectedPhysicalHash:state.physicalManifestHash,
      familyId:draft.familyId,basisId:draft.basisId,createBasis:draft.createBasis,
      templateEventDefinitionId:draft.templateEventDefinitionId||"",displayName:draft.displayName||draft.basisId
    })});
    state.managedPhysicalEvent=status;
    rememberManagedPhysicalOperation(status.operationId);
    renderEditor();
    if(status.terminal)await finishManagedPhysicalEvent(status);else scheduleManagedPhysicalEventPoll(status.operationId);
  }catch(error){
    state.managedPhysicalEvent={stage:"Failed",progress:0,terminal:true,code:error.code||"AUTH_AUDIO_MANAGED_EVENT_FAILED",message:error.message,detail:error.detail||""};
    forgetManagedPhysicalOperation();
    setServer(error.message,"error");
    renderEditor();
  }
}

function managedPhysicalOperationStorageKey() { return `mx.audioPhysicalManagedEvent:${state.package}`; }
function rememberManagedPhysicalOperation(operationId) { try{if(operationId)localStorage.setItem(managedPhysicalOperationStorageKey(),operationId);}catch(_){} }
function forgetManagedPhysicalOperation() { try{localStorage.removeItem(managedPhysicalOperationStorageKey());}catch(_){} }

function restoreManagedPhysicalEventOperation() {
  let operationId="";try{operationId=localStorage.getItem(managedPhysicalOperationStorageKey())||"";}catch(_){}
  if(operationId)scheduleManagedPhysicalEventPoll(operationId,0);
}

function scheduleManagedPhysicalEventPoll(operationId,delay=700) {
  clearTimeout(state.managedPhysicalEventPoll);
  state.managedPhysicalEventPoll=setTimeout(()=>pollManagedPhysicalEvent(operationId),delay);
}

async function pollManagedPhysicalEvent(operationId) {
  try{
    const status=await api(`/api/authoring/audio/physical/managed-event/${encodeURIComponent(operationId)}`);
    state.managedPhysicalEvent=status;updateManagedPhysicalEventUi();
    if(status.terminal)await finishManagedPhysicalEvent(status);else scheduleManagedPhysicalEventPoll(operationId);
  }catch(error){
    const status={operationId,stage:"Failed",progress:0,terminal:true,code:error.code||"AUTH_AUDIO_MANAGED_EVENT_STATUS_FAILED",message:error.message,detail:error.detail||""};
    state.managedPhysicalEvent=status;forgetManagedPhysicalOperation();updateManagedPhysicalEventUi();setServer(error.message,"error");
  }
}

async function finishManagedPhysicalEvent(status) {
  clearTimeout(state.managedPhysicalEventPoll);forgetManagedPhysicalOperation();updateManagedPhysicalEventUi();
  if(status.stage!=="Ready") { setServer(status.message||"受控 Basis / Cue / Event 创建失败。","error");return; }
  state.selectedCueId=status.audioCueId||state.selectedCueId;
  if(status.audioCueId){state.appScreen="basis";state.workspaceMode="cues";state.workspaceTabs.set(status.audioCueId,"basis");const url=new URL(location.href);url.searchParams.set("screen","basis");url.searchParams.delete("mode");url.searchParams.delete("pair");url.searchParams.set("cue",status.audioCueId);url.searchParams.set("package",state.package);history.replaceState(null,"",url);}
  setServer("受控 Basis / Cue / Event 已创建，正在刷新工作台…","ok");
  await loadState();
}

function updateManagedPhysicalEventUi() {
  if(state.workspaceMode==="pairs"){renderGlobalPairInspector();return;}
  const target=el.cueForm.querySelector("[data-managed-physical-status]");if(target)target.innerHTML=renderManagedPhysicalEventStatus(state.managedPhysicalEvent);
  const button=el.cueForm.querySelector('[data-physical-action="managed-create"]');
  if(button){const cue=selectedCue(),eventDef=cue&&findEvent(cue.eventDefinitionId),draft=cue&&managedPhysicalDraftForCue(cue,eventDef),running=Boolean(state.managedPhysicalEvent&&!state.managedPhysicalEvent.terminal);button.disabled=isDirty()||running||!draft?.templateEventDefinitionId;button.textContent=running?"正在创建…":draft&&!draft.createBasis?"创建 / 同步当前受控 Event":"创建受控 Basis + Cue / Event";}
}

async function requestPhysicalCapture() {
  const cue=selectedCue();if(!cue||state.physicalCaptureActiveRequestId)return;
  clearTimeout(state.physicalCapturePoll);
  try{
    const response=await api("/api/authoring/audio/physical/capture",{method:"POST",body:"{}"});
    const capture={...response,status:response.status||"pending",targetCueId:cue.audioCueId};
    state.physicalCaptures.set(cue.audioCueId,capture);
    state.physicalCaptureActiveRequestId=response.requestId||"";
    updatePhysicalCaptureUi(cue.audioCueId);
    if(response.requestId)pollPhysicalCapture(response.requestId,cue.audioCueId,0);
  }catch(error){
    state.physicalCaptures.set(cue.audioCueId,{status:"failed",code:error.code,message:error.message,targetCueId:cue.audioCueId});
    updatePhysicalCaptureUi(cue.audioCueId);
  }
}

async function pollPhysicalCapture(requestId,cueId,attempt) {
  clearTimeout(state.physicalCapturePoll);
  try{
    const capture=await api(`/api/authoring/audio/physical/capture-status?id=${encodeURIComponent(requestId)}`);
    capture.targetCueId=cueId;
    state.physicalCaptures.set(cueId,capture);
    if(capture.status==="pending"&&attempt<40){updatePhysicalCaptureUi(cueId);state.physicalCapturePoll=setTimeout(()=>pollPhysicalCapture(requestId,cueId,attempt+1),500);return;}
    if(capture.status==="pending"){capture.status="failed";capture.code="AUDIO_PHYSICAL_CAPTURE_TIMEOUT";capture.message="等待 Unity 回写真实交互事实超时；未修改当前情境。";}
    if(capture.status==="succeeded")applyCapturedPhysicalFact(cueId,capture);
    if(state.physicalCaptureActiveRequestId===requestId)state.physicalCaptureActiveRequestId="";
    updatePhysicalCaptureUi(cueId);
  }catch(error){
    state.physicalCaptures.set(cueId,{status:"failed",code:error.code,message:error.message,requestId,targetCueId:cueId});
    if(state.physicalCaptureActiveRequestId===requestId)state.physicalCaptureActiveRequestId="";
    updatePhysicalCaptureUi(cueId);
  }
}

function applyCapturedPhysicalFact(cueId,capture) {
  const cue=findCue(cueId),fact=capture?.fact;if(!cue||!fact)return;
  const impactorMaterial=stableMaterialIdFromRuntime(fact.impactor?.runtimeMaterialId),receiverMaterial=stableMaterialIdFromRuntime(fact.receiver?.runtimeMaterialId);
  if(!impactorMaterial.success||!receiverMaterial.success){
    capture.mappingCode="AUDIO_PHYSICAL_CAPTURE_MATERIAL_MAPPING_INVALID";
    capture.mappingError=[impactorMaterial.message,receiverMaterial.message].filter(Boolean).join(" ");
    return;
  }
  const body=(source,materialId)=>({materialId,mass:numberValue(source?.mass),contactArea:numberValue(source?.contactArea),surface:source?.surface||"Any",construction:source?.construction||"Any"});
  const mapped={
    interactionKind:fact.interactionKind||"ObjectImpact",interactionMode:fact.interactionMode||"Impact",phase:fact.phase||"Started",
    impactor:body(fact.impactor,impactorMaterial.materialId),receiver:body(fact.receiver,receiverMaterial.materialId),
    relativeSpeed:numberValue(fact.relativeSpeed),normalSpeed:numberValue(fact.normalSpeed),tangentialSpeed:numberValue(fact.tangentialSpeed),
    pressure:numberValue(fact.pressure),impulse:numberValue(fact.impulse),intensity:numberValue(fact.intensity),angleDegrees:numberValue(fact.angleDegrees),
    contactNormal:structuredClone(fact.contactNormal||{x:0,y:0,z:0}),
    emitterId:Math.trunc(numberValue(fact.emitterId)),frame:Math.trunc(numberValue(fact.frame)),sequence:Math.trunc(numberValue(fact.sequence)),position:structuredClone(fact.position||{x:0,y:0,z:0})
  };
  const scenario=physicalScenarioForCue(cue);Object.assign(scenario,mapped);
  state.physicalPreview=null;state.physicalResolve=null;
  syncPhysicalScenarioControls(cueId,scenario);
  resolvePhysicalScenarioForCue(cueId);
}

function stableMaterialIdFromRuntime(runtimeMaterialId) {
  const matches=(state.physicalManifest?.materialProfiles||[]).filter(item=>Number(item.runtimeMaterialId)===Number(runtimeMaterialId));
  if(matches.length===1)return{success:true,materialId:matches[0].id};
  const reason=matches.length?"映射不唯一":"不存在";
  return{success:false,message:`runtime material #${runtimeMaterialId} 在当前 Physical Manifest 中${reason}，已保留原情境。`};
}

function syncPhysicalScenarioControls(cueId,scenario) {
  if(selectedCue()?.audioCueId!==cueId)return;
  for(const input of el.cueForm.querySelectorAll("[data-physical-fact]")){
    const value=scenario[input.dataset.physicalFact];if(value===undefined)continue;input.value=value;updateScenarioOutput(input);
  }
  for(const input of el.cueForm.querySelectorAll("[data-physical-body-field]")){
    const value=scenario[input.dataset.physicalBody]?.[input.dataset.physicalBodyField];if(value===undefined)continue;input.value=value;updateScenarioOutput(input);
  }
  for(const input of el.cueForm.querySelectorAll("[data-physical-normal]")){
    const value=scenario.contactNormal?.[input.dataset.physicalNormal];if(value===undefined)continue;input.value=numberValue(value);
  }
}

function updatePhysicalCaptureUi(cueId) {
  if(selectedCue()?.audioCueId===cueId){const target=el.cueForm.querySelector("[data-physical-capture-status]");if(target)target.innerHTML=renderPhysicalCaptureStatus(findCue(cueId));}
  const button=el.cueForm.querySelector("[data-physical-capture-button]");if(button){button.disabled=Boolean(state.physicalCaptureActiveRequestId);button.textContent=state.physicalCaptureActiveRequestId?"Unity 捕获中…":"从 Unity 捕获真实交互";}
}

async function resolvePhysicalScenarioForCue(cueId) {
  if(selectedCue()?.audioCueId===cueId){await resolvePhysicalScenario();return;}
  const cue=findCue(cueId);if(!cue)return;
  try{const result=await api("/api/authoring/audio/physical/resolve",{method:"POST",body:JSON.stringify({physicalManifest:state.physicalManifest,fact:physicalScenarioForCue(cue)})});const capture=state.physicalCaptures.get(cueId);if(capture)capture.authoringResolve=result.resolved||result;}catch(_){}
}

async function promoteCurrentBasis() {
  const cue = selectedCue();
  const activeSet = activeAdoptionSet(cue);
  const recipe = cue && state.generationBriefs.get(cue.audioCueId);
  if (!cue || !activeSet || activeSet.status !== "Applied" || isDirty()) return;
  const wantedKind = physicalKindFromCue(cue);
  const family = (state.physicalManifest?.families || []).find(item => item.interactionKind === wantedKind)
    || (state.physicalManifest?.families || [])[0];
  try {
    setServer("正在提升为稳定 Basis Sound…", "");
    const result = await api("/api/authoring/audio/physical/promote", { method:"POST", body:JSON.stringify({
      package:state.package,
      expectedSemanticHash:state.contentHash,
      expectedGenerationHash:state.generationManifestHash,
      expectedPhysicalHash:state.physicalManifestHash,
      audioCueId:cue.audioCueId,
      adoptionSetId:activeSet.setId,
      familyId:family?.id || "",
      collisionProfile:recipe?.collisionProfile || null
    }) });
    state.physicalManifest = structuredClone(result.manifest || state.physicalManifest);
    normalizePhysicalManifest();
    state.physicalManifestHash = result.currentHash || state.physicalManifestHash;
    state.savedPhysicalManifestText = stableText(state.physicalManifest);
    state.physicalCoverage = result.coverage || state.physicalCoverage;
    state.physicalCoverageStale = false;
    if (state.payload?.audioPhysical) {
      state.payload.audioPhysical.manifest = structuredClone(state.physicalManifest);
      state.payload.audioPhysical.manifestHash = state.physicalManifestHash;
      state.payload.audioPhysical.validation = result.validation || state.payload.audioPhysical.validation;
      state.payload.audioPhysical.coverage = state.physicalCoverage;
    }
    setServer(result.message || "已提升为基础音效", "ok");
    render();
  } catch (error) { setServer(error.message,"error"); }
}

async function refreshPhysicalCoverage() {
  try {
    setServer("正在校验 Physical 覆盖…", "");
    const result = await api("/api/authoring/audio/physical/coverage", { method:"POST", body:JSON.stringify({ physicalManifest:state.physicalManifest }) });
    state.physicalCoverage = result.coverage || result;
    state.physicalCoverageStale = false;
    if (state.payload?.audioPhysical) state.payload.audioPhysical.coverage = state.physicalCoverage;
    setServer(`覆盖校验完成 · 空洞 ${numberValue(state.physicalCoverage.missingCellCount)} · 歧义 ${numberValue(state.physicalCoverage.ambiguousCellCount)}`, state.physicalCoverage.missingCellCount || state.physicalCoverage.ambiguousCellCount ? "error" : "ok");
    renderEditor();
  } catch (error) { setServer(error.message,"error"); }
}

function updatePhysicalBasis(path, value) {
  const basis = currentBasisForCue();
  if (!basis) return;
  basis[path] = ["priority","runtimeBasisId"].includes(path) ? Number(value || 0) : value;
  invalidatePhysicalCoverage();
  updateSaveIndicator();
  renderInspector();
}

function updatePhysicalBasisNested(path,value,numeric=false) {
  const basis=currentBasisForCue(); if(!basis)return;
  setNestedValue(basis,path,numeric?Number(value||0):value);
  invalidatePhysicalCoverage();
  updatePhysicalBasisSummary(basis);
  updateSaveIndicator();
  renderInspector();
}

function updatePhysicalBasisRangeInput(input,commit=false) {
  const basis=currentBasisForCue(),path=input.dataset.physicalBasisRange;
  if(!basis||!path)return;
  const raw=String(input.value||"").trim(),value=Number(raw);
  if(!raw||!Number.isFinite(value)) {
    if(commit)syncPhysicalBasisRangeInputs(input,basis);
    return;
  }
  const lower=Number(input.min),upper=Number(input.max);
  const bounded=commit?Math.max(Number.isFinite(lower)?lower:-Infinity,Math.min(Number.isFinite(upper)?upper:Infinity,value)):value;
  setNestedValue(basis,path,bounded);
  if(commit)normalizePhysicalBasisRange(path,basis);
  invalidatePhysicalCoverage();
  updatePhysicalBasisSummary(basis);
  updateSaveIndicator();
  renderInspector();
  if(commit)syncPhysicalBasisRangeInputs(input,basis);
}

function normalizePhysicalBasisRange(path,basis) {
  const separator=path.lastIndexOf("."),rangePath=path.slice(0,separator),editedKey=path.slice(separator+1),range=getNestedValue(basis,rangePath);
  if(!range)return;
  let min=numberValue(range.min),anchor=numberValue(range.anchor),max=numberValue(range.max);
  if(editedKey==="min"&&min>max)max=min;
  else if(editedKey==="max"&&max<min)min=max;
  else if(min>max)[min,max]=[max,min];
  anchor=Math.max(min,Math.min(max,anchor));
  range.min=min;range.anchor=anchor;range.max=max;
}

function syncPhysicalBasisRangeInputs(input,basis) {
  const group=input.closest(".physical-basis-range");
  if(!group)return;
  group.querySelectorAll("[data-physical-basis-range]").forEach(control=>{
    const current=getNestedValue(basis,control.dataset.physicalBasisRange);
    if(Number.isFinite(Number(current)))control.value=String(current);
  });
}

function updatePhysicalBasisSummary(basis) {
  const summary=el.cueForm.querySelector(".physical-basis-edit-summary");
  if(!summary)return;
  const values=[renderAcousticAnchorText(basis.impactor),renderAcousticAnchorText(basis.receiver),renderBasisRangeText(basis)];
  summary.querySelectorAll("strong").forEach((node,index)=>node.innerHTML=values[index]||"—");
}

function selectCoverageFamily(familyId) {
  if(!familyId||sameId(familyId,state.physicalCoverageFamilyId))return;
  state.physicalCoverageFamilyId=familyId;
  state.physicalCoverageCell=null;
  renderEditor({force:true});
}

function selectCoverageMatrixCell(dataset) {
  state.physicalCoverageFamilyId=dataset.coverageCellFamily||state.physicalCoverageFamilyId;
  state.physicalCoverageCell={familyId:dataset.coverageCellFamily||"",impactorMaterialId:dataset.coverageCellImpactor||"",receiverMaterialId:dataset.coverageCellReceiver||""};
  renderEditor({force:true});
}

async function coverageInspectorAction(action,basisCueId="") {
  if(action==="open-basis"&&basisCueId){await selectCue(basisCueId);state.workspaceTabs.set(basisCueId,"basis");renderEditor({force:true});return;}
  if(action!=="open-scenario")return;
  const cue=selectedCue();if(!cue)return;
  const family=(state.physicalManifest?.families||[]).find(item=>sameId(item.id,state.physicalCoverageFamilyId));
  const cell=state.physicalCoverageCell;
  const scenario=physicalScenarioForCue(cue);
  if(family){scenario.interactionKind=family.interactionKind||scenario.interactionKind;scenario.interactionMode=family.interactionMode||scenario.interactionMode;}
  if(cell){scenario.impactor.materialId=cell.impactorMaterialId;scenario.receiver.materialId=cell.receiverMaterialId;}
  const coverageCell=coverageCellsForFamily(family?.id||"").find(item=>sameId(item.impactorMaterialId,cell?.impactorMaterialId)&&sameId(item.receiverMaterialId,cell?.receiverMaterialId));
  const basis=(coverageCell?.selectedBasisIds||[]).map(id=>(state.physicalManifest?.basisSounds||[]).find(item=>sameId(item.id,id))).find(Boolean);
  if(basis){scenario.impactor.mass=numberValue(basis.impactor?.mass?.anchor,scenario.impactor.mass);scenario.impactor.contactArea=numberValue(basis.impactor?.contactArea?.anchor,scenario.impactor.contactArea);scenario.receiver.mass=numberValue(basis.receiver?.mass?.anchor,scenario.receiver.mass);scenario.receiver.contactArea=numberValue(basis.receiver?.contactArea?.anchor,scenario.receiver.contactArea);scenario.relativeSpeed=numberValue(basis.relativeSpeed?.anchor,scenario.relativeSpeed);scenario.intensity=numberValue(basis.intensity?.anchor,scenario.intensity);scenario.angleDegrees=numberValue(basis.angleDegrees?.anchor,scenario.angleDegrees);}
  state.workspaceTabs.set(cue.audioCueId,"scenario");
  state.physicalResolve=null;state.physicalPreview=null;
  renderEditor({force:true});
}

function updateCoverageBasisRange(input) {
  const basis=(state.physicalManifest?.basisSounds||[]).find(item=>sameId(item.id,input.dataset.coverageBasisId)),path=input.dataset.coverageBasisRange,value=Number(input.value);
  if(!basis||!path||!Number.isFinite(value))return;
  setNestedValue(basis,path,value);
  normalizePhysicalBasisRange(path,basis);
  const output=input.closest(".coverage-anchor-slider")?.querySelector("b");
  if(output){const unit=path.startsWith("relativeSpeed")?"m/s":"";output.textContent=`${value.toFixed(Number(input.step)<.1?2:1)}${unit}`;}
  invalidatePhysicalCoverage();
  updateSaveIndicator();
  renderInspector();
}

function updateCoverageRequirementField(input) {
  const index=Number(input.closest("[data-coverage-requirement-index]")?.dataset.coverageRequirementIndex),requirement=state.physicalManifest?.coverageRequirements?.[index];
  if(!requirement)return;
  requirement[input.dataset.coverageRequirementField]=input.type==="checkbox"?input.checked:input.value;
  invalidatePhysicalCoverage(); updateSaveIndicator(); renderInspector();
}

function updateCoverageRequirementMaterial(input) {
  const index=Number(input.closest("[data-coverage-requirement-index]")?.dataset.coverageRequirementIndex),requirement=state.physicalManifest?.coverageRequirements?.[index];
  if(!requirement)return;
  const key=`${input.dataset.coverageRequirementMaterial}MaterialIds`,values=requirement[key]||=[];
  const found=values.findIndex(value=>sameId(value,input.value));
  if(input.checked&&found<0)values.push(input.value);
  if(!input.checked&&found>=0)values.splice(found,1);
  requirement[key]=values;
  const footer=input.closest(".coverage-requirement")?.querySelector("footer");
  if(footer)footer.textContent=`${(requirement.impactorMaterialIds||[]).length} × ${(requirement.receiverMaterialIds||[]).length} = ${(requirement.impactorMaterialIds||[]).length*(requirement.receiverMaterialIds||[]).length} 个材质对${requirement.facts?.length?` · ${requirement.facts.length} 个显式事实`:""}`;
  invalidatePhysicalCoverage(); updateSaveIndicator(); renderInspector();
}

function physicalCoverageAction(action,indexValue,familyId="") {
  state.physicalManifest.coverageRequirements||=[];
  if(action==="delete") {
    const index=Number(indexValue); if(!Number.isInteger(index)||!state.physicalManifest.coverageRequirements[index])return;
    state.physicalManifest.coverageRequirements.splice(index,1);
  } else if(action==="add") {
    const cue=selectedCue(),basis=currentBasisForCue(cue),targetFamilyId=familyId||state.physicalCoverageFamilyId||basis?.familyId||state.physicalManifest.families?.[0]?.id||"";
    const existing=new Set(state.physicalManifest.coverageRequirements.map(item=>item.id)); let suffix=1,id=`coverage.${String(targetFamilyId).split(".").pop()||"family"}.custom`;
    while(existing.has(id))id=`coverage.${String(targetFamilyId).split(".").pop()||"family"}.custom.${++suffix}`;
    const familyBasis=(state.physicalManifest.basisSounds||[]).find(item=>sameId(item.familyId,targetFamilyId))||basis;
    state.physicalManifest.coverageRequirements.push({id,familyId:targetFamilyId,impactorMaterialIds:familyBasis?.impactor?.materialId?[familyBasis.impactor.materialId]:[],receiverMaterialIds:familyBasis?.receiver?.materialId?[familyBasis.receiver.materialId]:[],facts:[],scenarioPresetIds:[],required:true});
  } else return;
  invalidatePhysicalCoverage(); updateSaveIndicator(); renderEditor({force:true}); renderInspector();
}

function setNestedValue(target,path,value) {
  const parts=String(path).split("."); let current=target;
  for(let index=0;index<parts.length-1;index++)current=current[parts[index]]||(current[parts[index]]={});
  current[parts[parts.length-1]]=value;
}

function getNestedValue(target,path) {
  return String(path).split(".").reduce((current,key)=>current?.[key],target);
}

function invalidatePhysicalCoverage() {
  state.physicalCoverageStale=true;
  if(state.payload?.audioPhysical)state.payload.audioPhysical.coverage=null;
  const summary=el.cueForm.querySelector("[data-coverage-summary]");
  if(summary)summary.innerHTML=renderCoverageCompactSummary(state.physicalCoverage);
}

function updatePhysicalFamily(familyId, path, value) {
  const family = (state.physicalManifest?.families || []).find(item => sameId(item.id,familyId));
  if (!family) return;
  family[path] = ["priority","cooldownFrames"].includes(path) ? Number(value || 0) : value;
  invalidatePhysicalCoverage();
  updateSaveIndicator();
}

function updatePhysicalWeight(familyId, key, value, input) {
  const family = (state.physicalManifest?.families || []).find(item => sameId(item.id,familyId));
  if (!family) return;
  family.weights ||= {};
  family.weights[key] = Number(value || 0);
  const output = input?.parentElement?.querySelector("output");
  if (output) output.textContent = family.weights[key].toFixed(2);
  invalidatePhysicalCoverage();
  updateSaveIndicator();
}

function updateResponsePoint(input) {
  const profile = state.physicalManifest?.responseProfiles?.[Number(input.dataset.responseProfileIndex)];
  const curve = profile?.curves?.[Number(input.dataset.responseCurveIndex)];
  const point = curve?.points?.[Number(input.dataset.responsePointIndex)];
  if (!point) return;
  point[input.dataset.responsePoint] = Number(input.value || 0);
  invalidatePhysicalCoverage();
  updateSaveIndicator();
}

function updatePhysicalFact(fieldName, value, input) {
  const cue = selectedCue(); if (!cue) return;
  const scenario = physicalScenarioForCue(cue);
  scenario[fieldName] = input?.tagName === "SELECT" ? value : Number(value || 0);
  state.physicalPreview = null;
  updateScenarioOutput(input);
  schedulePhysicalResolve();
}

function updatePhysicalNormal(axis, value) {
  const cue = selectedCue(); if (!cue) return;
  const scenario = physicalScenarioForCue(cue);
  scenario.contactNormal ||= {x:0,y:0,z:0};
  scenario.contactNormal[axis] = Number(value || 0);
  state.physicalPreview = null;
  schedulePhysicalResolve();
}

function updatePhysicalBodyFact(side, fieldName, value, input) {
  const cue = selectedCue(); if (!cue) return;
  const body = physicalScenarioForCue(cue)[side]; if (!body) return;
  body[fieldName] = input?.tagName === "SELECT" ? value : Number(value || 0);
  state.physicalPreview = null;
  updateScenarioOutput(input);
  schedulePhysicalResolve();
}

function updateScenarioOutput(input) {
  if (!input || input.type !== "range") return;
  const output = input.parentElement?.querySelector("output");
  if (!output) return;
  const suffix = output.textContent.match(/[^0-9.\-]+$/)?.[0] || "";
  output.textContent = `${Number(input.value).toFixed(Number(input.step) < .1 ? 2 : Number(input.step) < 1 ? 1 : 0)}${suffix}`;
}

function schedulePhysicalResolve() {
  clearTimeout(state.physicalResolveTimer);
  state.physicalResolveTimer = setTimeout(resolvePhysicalScenario,220);
}

async function resolvePhysicalScenario() {
  const cue = selectedCue(); if (!cue || state.physicalResolvePending) return;
  state.physicalResolvePending = true;
  state.physicalPreview = null;
  const target = el.cueForm.querySelector("[data-physical-resolve-result]");
  if (target) target.innerHTML = renderPhysicalResolveResult(null);
  try {
    const result = await api("/api/authoring/audio/physical/resolve", { method:"POST", body:JSON.stringify({ physicalManifest:state.physicalManifest, fact:physicalScenarioForCue(cue) }) });
    state.physicalResolve = result.resolved || result;
  } catch (error) {
    state.physicalResolve = { success:false, code:error.code, message:error.message, trace:null };
  } finally {
    state.physicalResolvePending = false;
    const resultTarget = el.cueForm.querySelector("[data-physical-resolve-result]");
    if (resultTarget) {
      resultTarget.innerHTML = renderPhysicalResolveResult(state.physicalResolve);
      resultTarget.querySelectorAll("[data-physical-action]").forEach(button => button.addEventListener("click", () => physicalAction(button.dataset.physicalAction,button.dataset.resolvedCue || "")));
    }
  }
}

async function previewPhysicalResolvedCue(audioCueId) {
  const cue = findCue(audioCueId);
  const eventDef = cue && findEvent(cue.eventDefinitionId);
  if (!cue || !eventDef) return;
  try {
    const selected = selectedCue();
    const result = await api("/api/authoring/audio/physical/preview", { method:"POST", body:JSON.stringify({
      package:state.package,
      semanticManifest:state.manifest,
      physicalManifest:state.physicalManifest,
      fact:physicalScenarioForCue(selected || cue),
      position:{x:0,y:1,z:2}
    }) });
    state.physicalPreview = { ...result, audioCueId:result.audioCueId || cue.audioCueId };
    state.preview = { ...result, audioCueId:result.audioCueId || cue.audioCueId };
    setServer(`Unity 正在用 Runtime Physical Catalog 解析并试听：${cue.displayName || cue.audioCueId}`,"ok");
    renderEditor();
    pollPhysicalPreview(result.requestId,result.audioCueId || cue.audioCueId);
  } catch (error) { state.physicalPreview={status:"failed",message:error.message};setServer(error.message,"error");renderEditor(); }
}

async function pollPhysicalPreview(requestId, audioCueId) {
  clearTimeout(state.previewPoll);
  try {
    const result = await api(`/api/authoring/audio/preview-status?id=${encodeURIComponent(requestId)}`);
    state.physicalPreview = { ...result, audioCueId };
    state.preview = { ...result, audioCueId };
    renderInspector();
    const target = el.cueForm.querySelector("[data-physical-resolve-result]");
    if (target) {
      target.innerHTML = renderPhysicalResolveResult(state.physicalResolve);
      target.querySelectorAll("[data-physical-action]").forEach(button => button.addEventListener("click", () => physicalAction(button.dataset.physicalAction,button.dataset.resolvedCue || "")));
    }
    if (result.status === "pending") state.previewPoll = setTimeout(() => pollPhysicalPreview(requestId,audioCueId),500);
  } catch (error) {
    state.physicalPreview = { status:"failed",message:error.message,audioCueId };
    state.preview = { ...state.physicalPreview };
    renderInspector();
  }
}
