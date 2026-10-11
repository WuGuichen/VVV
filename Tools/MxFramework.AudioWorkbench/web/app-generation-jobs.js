async function generationAction(action) {
  const cue = selectedCue(); const eventDef = cue && findEvent(cue.eventDefinitionId); if (!cue || !eventDef) return;
  if (action === "derive") {
    const current = state.generationBriefs.get(cue.audioCueId);
    if (current?.promptModified && !confirm("规则重新派生会替换人工修改的提示词，继续吗？")) return;
    state.generationBriefs.delete(cue.audioCueId); state.generationManifest.recipes = state.generationManifest.recipes.filter(item => item.audioCueId !== cue.audioCueId); queueGenerationBrief(cue, eventDef); renderEditor(); return;
  }
  if (action === "reset-physical") {
    const brief=state.generationBriefs.get(cue.audioCueId);if(!brief)return;
    brief.collisionProfile=semanticCollisionProfile(cue,brief);syncCollisionPrompts(brief);renderEditor();return;
  }
  if (action === "rebuild-physical") {
    const brief=state.generationBriefs.get(cue.audioCueId);if(!brief?.collisionProfile)return;
    rebuildCollisionPrompts(brief);brief.updatedAtUtc=new Date().toISOString();updateSaveIndicator();renderEditor();return;
  }
  if (action === "optimize") {
    const current = state.generationBriefs.get(cue.audioCueId); if (!current) return;
    setServer("Codex CLI 正在优化英文提示词…", "");
    const result = await api("/api/authoring/audio/generation/prompt/optimize", { method: "POST", body: JSON.stringify({ cue, audioEvent: eventDef, recipe: current }) });
    const next = result.recipe;
    if (confirm(`应用提示词优化？\n\n当前：${String(current.positivePrompt).slice(0,240)}\n\n优化后：${String(next.positivePrompt).slice(0,240)}`)) upsertRecipe(next);
    setServer(result.fallback ? result.message : "英文提示词已优化，保存草稿后持久化。", result.fallback ? "warn" : "ok"); renderEditor(); return;
  }
  if (action === "restore-saved") { const saved = savedRecipe(cue.audioCueId); if (saved) upsertRecipe(structuredClone(saved)); renderEditor(); return; }
  if (action === "restore-initial") { const initial = state.initialRecipes.get(cue.audioCueId); if (initial) upsertRecipe(structuredClone(initial)); else { state.generationBriefs.delete(cue.audioCueId); queueGenerationBrief(cue,eventDef); } renderEditor(); return; }
  if (action === "undo") { const history = state.generationBriefs.get(cue.audioCueId)?.history || []; if (history.length) applyHistory(cue, history[history.length-1]); renderEditor(); return; }
  if (action === "copy-recipe") { await navigator.clipboard.writeText(JSON.stringify(state.generationBriefs.get(cue.audioCueId), null, 2)); setServer("Recipe JSON 已复制", "ok"); return; }
  if (action === "copy-prompt") { await navigator.clipboard.writeText(state.generationBriefs.get(cue.audioCueId)?.positivePrompt || ""); setServer("英文 Prompt 已复制", "ok"); return; }
  if (action === "paste-recipe") { try { const value=JSON.parse(await navigator.clipboard.readText()); adoptRecipeIdentity(value,cue,eventDef); upsertRecipe(value); renderEditor(); } catch(error) { setServer("粘贴的 Recipe JSON 无效", "error"); } return; }
  if (action === "reconnect") {
    state.generationBootstrap = await api("/api/authoring/audio/generation/host/ensure", { method: "POST", body: "{}" });
    renderEditor(); pollGenerationBootstrap(state.generationBootstrap.bootstrapId); return;
  }
  if (action === "copy-start") { await navigator.clipboard.writeText("python Tools/MxFramework.AudioGenerationHost/tools/manage_wg200.py ensure"); setServer("已复制 SFX Host 启动命令", "ok"); return; }
  if (action === "show-diagnostics") { const details = el.cueForm.querySelector("[data-generation-diagnostics]"); if (details) details.open = true; return; }
  if (action === "play-mix") { await playCandidateMix(); return; }
  if (action === "stop-mix") { stopCandidateMix(); return; }
  if (action === "random-mix") { randomizeMix(); return; }
  try {
    if (action === "generate" || action === "explore") {
      const brief = state.generationBriefs.get(cue.audioCueId); if (!brief) return;
      normalizeRecipeCounts(brief);
      const requestBrief = structuredClone(brief);
      if (action === "explore") requestBrief.layers.forEach(layer => { if (layer.enabled) { layer.takes = 3; layer.adoptionTakes = Math.min(layer.adoptionTakes || 1, 3); } });
      state.selectedCandidates.clear();
      state.generationTabs.set(cue.audioCueId, "generate");
      state.generationCueId = cue.audioCueId;
      const recipeHash = recipeIdentityHash(brief);
      state.generationJob = await api("/api/authoring/audio/generation/jobs", { method: "POST", body: JSON.stringify({ brief: requestBrief, seed: Math.floor(Date.now() % 2147483647), is3D: eventDef.is3D, recipeRevision: brief.revision || 1, recipeHash }) });
      const session = currentSession(); session.activeJobId = state.generationJob.jobId; session.recipeHash = recipeHash; await saveGenerationSession();
      pollGenerationJob(state.generationJob.jobId, cue.audioCueId);
    } else if (action === "cancel" && state.generationJob) {
      await api(`/api/authoring/audio/generation/jobs/${encodeURIComponent(state.generationJob.jobId)}/cancel`, { method: "POST", body: "{}" });
    } else if (action === "adopt" && state.generationJob) {
      const brief = state.generationBriefs.get(cue.audioCueId);
      const result = await api("/api/authoring/audio/generation/apply", { method: "POST", body: JSON.stringify({
        jobId: state.generationJob.jobId, audioCueId: cue.audioCueId, eventDefinitionId: eventDef.id,
        expectedBindingHash: bindingHash(cue.audioCueId), expectedGenerationManifestHash: state.payload?.audioGeneration?.manifestHash || "",
        selectedCandidateIds: [...state.selectedCandidates], layout: (brief.layers || []).length > 1 ? "Layered" : "MultiInstrument",
        prompt: brief.positivePrompt, negativePrompt: brief.negativePrompt, intentZh: brief.intentZh || "", layerPrompts: brief.layers || [], collisionProfile: brief.collisionProfile || null, recipeRevision: brief.revision || 1, durationMode:brief.durationMode||"LegacyFixed", durationCharacter:brief.durationCharacter||"Auto", decayProfile:brief.decayProfile||"Natural", maxDurationSeconds:Number(brief.maxDurationSeconds||brief.targetDurationSeconds||0), derivationProvider: brief.derivationProvider || "Manual", adoptedBy: "Audio Workbench user",
        candidateEdits: Object.fromEntries([...state.selectedCandidates].map(id=>{const candidate=state.generationJob.candidates.find(item=>item.candidateId===id);return [id,candidate?candidateEditFor(state.generationJob,candidate):null];}).filter(([,edit])=>edit))
      }) });
      state.audioApplication=result.application||null;setServer(result.message,"ok");renderEditor();if(state.audioApplication?.applicationId)pollAudioApplication(state.audioApplication.applicationId);return;
    }
    renderEditor();
  } catch (error) { setServer(error.message, "error"); }
}

async function applyHistorySet(setId,setHash){try{const result=await api(`/api/authoring/audio/generation/history/${encodeURIComponent(setId)}/apply`,{method:"POST",body:JSON.stringify({expectedSetHash:setHash})});state.audioApplication=result;renderEditor();pollAudioApplication(result.applicationId);}catch(error){setServer(error.message,"error");}}
async function deleteHistorySet(setId, setHash) {
  const set = (state.generationManifest?.adoptionSets || []).find(item => item.setId === setId);
  if (!set || !confirm(`永久删除这个音效历史版本？\n\n${set.createdAtUtc || set.setId}\n${set.sources?.length || 0} 个素材文件\n\n当前版本与原始恢复版本不会受影响。`)) return;
  try {
    const result = await api(`/api/authoring/audio/generation/history/${encodeURIComponent(setId)}/delete`, { method: "POST", body: JSON.stringify({ expectedSetHash: setHash, expectedGenerationManifestHash: state.generationManifestHash }) });
    setServer(result.message || "历史版本已删除。", result.cleanupWarnings?.length ? "warn" : "ok");
    await loadState();
  } catch (error) { setServer(error.message, "error"); }
}
async function pollAudioApplication(id){clearTimeout(state.audioApplicationPoll);try{state.audioApplication=await api(`/api/authoring/audio/generation/apply/${encodeURIComponent(id)}`);renderEditor();if(!state.audioApplication.terminal)state.audioApplicationPoll=setTimeout(()=>pollAudioApplication(id),700);else{setServer(state.audioApplication.message,state.audioApplication.stage==="Ready"?"ok":"error");await loadState();state.audioApplication=await api(`/api/authoring/audio/generation/apply/${encodeURIComponent(id)}`);renderEditor();}}catch(error){setServer(error.message,"error");}}

async function pollGenerationBootstrap(bootstrapId) {
  clearTimeout(state.generationBootstrapPoll);
  if (!bootstrapId) return;
  try {
    state.generationBootstrap = await api(`/api/authoring/audio/generation/host/bootstrap/${encodeURIComponent(bootstrapId)}`);
    renderEditor();
    if (state.generationBootstrap.status === "Ready") { await refreshGenerationHealth(); return; }
    if (state.generationBootstrap.status !== "Failed") state.generationBootstrapPoll = setTimeout(() => pollGenerationBootstrap(bootstrapId), 1000);
  } catch (error) {
    state.generationBootstrap = { bootstrapId, status: "Failed", progress: 100, code: error.code, message: "SFX Host 启动状态读取失败", detail: error.detail || error.message, retryable: true };
    renderEditor();
  }
}

async function pollGenerationJob(jobId, cueId) {
  clearTimeout(state.generationPoll);
  try {
    const job = await api(`/api/authoring/audio/generation/jobs/${encodeURIComponent(jobId)}`);
    if (state.generationCueId !== cueId) return;
    state.generationJob = job;
    const shelfIndex=state.generationJobs.findIndex(item=>item.jobId===job.jobId);if(shelfIndex>=0)state.generationJobs[shelfIndex]=job;else state.generationJobs.unshift(job);
    if (job.status === "Succeeded") state.generationTabs.set(cueId, "candidates");
    if (selectedCue()?.audioCueId === cueId) renderEditor();
    if (!["Succeeded","Failed","Cancelled"].includes(job.status)) state.generationPoll = setTimeout(() => pollGenerationJob(jobId, cueId), 800);
    else { saveGenerationSession(); refreshGenerationHealth(); }
  } catch (error) { state.generationJob = { ...state.generationJob, status: "Failed", failureReason: error.message }; renderEditor(); }
}

function toggleCandidate(candidateId, checked) {
  if (checked) state.selectedCandidates.add(candidateId); else state.selectedCandidates.delete(candidateId);
  saveGenerationSession();
  renderEditor();
}

function isCandidateSelectionReady(brief, candidates) {
  normalizeRecipeCounts(brief);
  const selected = (candidates || []).filter(item => state.selectedCandidates.has(item.candidateId) && candidateIsReady(item));
  return (brief?.layers || []).filter(layer=>layer.enabled).every(layer => selected.filter(item => item.layer === layer.name).length === layer.adoptionTakes)
    && selected.length === adoptionTotal(brief);
}

function candidateUrl(jobId, candidateId) { return `/api/authoring/audio/generation/jobs/${encodeURIComponent(jobId)}/candidates/${encodeURIComponent(candidateId)}`; }

async function decodeCandidate(jobId,id,context) {
  const key=`${jobId}:${id}`;
  if(state.mixDecodeCache.has(key))return state.mixDecodeCache.get(key);
  const bytes=await (await fetch(candidateUrl(jobId,id))).arrayBuffer();
  const buffer=await context.decodeAudioData(bytes.slice(0)); state.mixDecodeCache.set(key,buffer); return buffer;
}

function activeMixEntries() {
  const brief=state.generationBriefs.get(state.selectedCueId), job=state.generationJob, session=currentSession(); if(!brief||!job)return[];
  const mix=session.activeMix==="B"?session.mixB:session.mixA;
  const solos=new Set([...el.cueForm.querySelectorAll("[data-mix-solo]:checked")].map(item=>item.dataset.mixSolo));
  const muted=new Set([...el.cueForm.querySelectorAll("[data-mix-mute]:checked")].map(item=>item.dataset.mixMute));
  return brief.layers.filter(layer=>layer.enabled&&mix[layer.name]&&!muted.has(layer.name)&&(!solos.size||solos.has(layer.name))).map(layer=>({layer,id:mix[layer.name]}));
}

async function playCandidateMix(){
  stopCandidateMix(); const job=state.generationJob,entries=activeMixEntries();if(!job||!entries.length)return;
  const context=state.audioContext||(state.audioContext=new (window.AudioContext||window.webkitAudioContext)());await context.resume();
  const limiter=context.createDynamicsCompressor();limiter.threshold.value=-3;limiter.knee.value=1;limiter.ratio.value=20;limiter.attack.value=0.002;limiter.release.value=0.08;limiter.connect(context.destination);
  const when=context.currentTime+0.06;state.mixSources=[];
  for(const entry of entries){const candidate=job.candidates.find(item=>item.candidateId===entry.id),edit=candidateEditFor(job,candidate),buffer=await decodeCandidate(job.jobId,entry.id,context),source=context.createBufferSource(),gain=context.createGain(),start=when+entry.layer.delayMs/1000,duration=(edit.outMs-edit.inMs)/1000,level=Math.pow(10,entry.layer.gainDb/20);source.buffer=buffer;gain.gain.setValueAtTime(0,start);gain.gain.linearRampToValueAtTime(level,start+edit.fadeInMs/1000);gain.gain.setValueAtTime(level,Math.max(start+edit.fadeInMs/1000,start+duration-edit.fadeOutMs/1000));gain.gain.linearRampToValueAtTime(0,start+duration);source.connect(gain).connect(limiter);source.start(start,edit.inMs/1000,duration);state.mixSources.push(source);}
}
function stopCandidateMix(){for(const source of state.mixSources){try{source.stop();}catch(_){}}state.mixSources=[];}
function randomizeMix(){const job=state.generationJob,brief=state.generationBriefs.get(state.selectedCueId),session=currentSession();if(!job||!brief)return;const mix=session.activeMix==="B"?session.mixB:session.mixA;for(const layer of brief.layers.filter(item=>item.enabled)){const ready=job.candidates.filter(item=>item.layer===layer.name&&candidateIsReviewable(item)&&state.candidateReviews.get(item.candidateId)!=="Reject");if(ready.length)mix[layer.name]=ready[Math.floor(Math.random()*ready.length)].candidateId;}saveGenerationSession();renderEditor();}

async function renderOfflineMixWaveform(){
  const canvas=el.cueForm.querySelector("[data-mix-waveform]"),peakLabel=el.cueForm.querySelector("[data-mix-peak]");if(!canvas||!state.generationJob)return;
  try{const entries=activeMixEntries();if(!entries.length)return;const job=state.generationJob,decode=state.audioContext||(state.audioContext=new (window.AudioContext||window.webkitAudioContext)());const buffers=await Promise.all(entries.map(item=>decodeCandidate(job.jobId,item.id,decode))),edits=entries.map(item=>candidateEditFor(job,job.candidates.find(candidate=>candidate.candidateId===item.id)));const duration=Math.max(...edits.map((edit,index)=>(edit.outMs-edit.inMs+entries[index].layer.delayMs)/1000));const offline=new OfflineAudioContext(2,Math.ceil(duration*48000),48000);const limiter=offline.createDynamicsCompressor();limiter.threshold.value=-3;limiter.knee.value=1;limiter.ratio.value=20;limiter.connect(offline.destination);buffers.forEach((buffer,index)=>{const source=offline.createBufferSource(),gain=offline.createGain(),edit=edits[index],start=entries[index].layer.delayMs/1000,clipDuration=(edit.outMs-edit.inMs)/1000,level=Math.pow(10,entries[index].layer.gainDb/20);source.buffer=buffer;gain.gain.setValueAtTime(0,start);gain.gain.linearRampToValueAtTime(level,start+edit.fadeInMs/1000);gain.gain.setValueAtTime(level,Math.max(start+edit.fadeInMs/1000,start+clipDuration-edit.fadeOutMs/1000));gain.gain.linearRampToValueAtTime(0,start+clipDuration);source.connect(gain).connect(limiter);source.start(start,edit.inMs/1000,clipDuration);});const rendered=await offline.startRendering();const data=rendered.getChannelData(0),peaks=[],blocks=120;let peak=0;for(let i=0;i<blocks;i++){let value=0;for(let j=Math.floor(i*data.length/blocks);j<Math.floor((i+1)*data.length/blocks);j++){const sample=Math.abs(data[j]);value=Math.max(value,sample);peak=Math.max(peak,sample);}peaks.push(value);}drawWaveform(canvas,peaks);if(peakLabel){peakLabel.textContent=`监听峰值 ${(20*Math.log10(Math.max(peak,1e-6))).toFixed(1)} dBFS${peak>.98?" · 注意削波风险":""}`;peakLabel.className=peak>.98?"warn":"";}}
  catch(error){if(peakLabel)peakLabel.textContent=`混合波形不可用：${error.message}`;}
}

async function renderCandidateWaveforms() {
  const job = state.generationJob; if (!job) return;
  const canvases = [...el.cueForm.querySelectorAll("[data-candidate-waveform]")];
  const token = ++state.candidateWaveformRenderToken;
  for (const canvas of canvases) {
    if (token !== state.candidateWaveformRenderToken) return;
    try {
      const candidate = (job.candidates || []).find(item => item.candidateId === canvas.dataset.candidateWaveform);
      if (candidate?.waveformPeaks?.length) {
        drawWaveform(canvas, candidate.waveformPeaks);
        const editorCanvas = [...el.cueForm.querySelectorAll("[data-edit-waveform]")].find(item => item.dataset.editWaveform === candidate.candidateId);
        if (editorCanvas) drawCandidateEditWaveform(editorCanvas, candidate, candidateEditFor(job,candidate));
        continue;
      }
      const analysis = await loadWaveformAnalysis(candidateUrl(job.jobId, canvas.dataset.candidateWaveform));
      if (token !== state.candidateWaveformRenderToken || !canvas.isConnected) return;
      drawWaveform(canvas, analysis.peaks);
      const editorCanvas = [...el.cueForm.querySelectorAll("[data-edit-waveform]")].find(item => item.dataset.editWaveform === candidate?.candidateId);
      if (editorCanvas) drawCandidateEditWaveform(editorCanvas, {...candidate,waveformPeaks:analysis.peaks},candidateEditFor(job,candidate));
    } catch (error) {
      if (token === state.candidateWaveformRenderToken && canvas.isConnected) {
        canvas.classList.add("failed"); canvas.title = error?.message || "候选波形解析失败";
      }
    }
  }
}
