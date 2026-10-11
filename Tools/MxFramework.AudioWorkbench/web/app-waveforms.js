async function renderWaveformPreview(cue) {
  let host = el.editorWaveformPanel.querySelector("[data-waveform-preview]");
  if (!host) {
    el.editorWaveformPanel.innerHTML = `<div class="waveform-preview" data-waveform-preview></div>`;
    host = el.editorWaveformPanel.querySelector("[data-waveform-preview]");
  }
  const sources = findWaveformSources(cue);
  const token = ++state.waveformRenderToken;
  if (!sources.length) {
    host.innerHTML = `<div class="waveform-empty"><strong>当前没有可分析的源文件</strong><span>Cue 未配置预览源，当前 FMOD Event 也没有已应用的 adoption set；请以 Unity / FMOD 真实试听为准。</span></div>`;
    return;
  }

  host.innerHTML = `<div class="waveform-loading">正在构建 ${sources.length} 层音频分析…</div>`;
  const results = await Promise.all(sources.map(loadAnalysisResult));
  const comparisonSource = findComparisonSource(cue, sources[0]?.setId);
  const comparison = comparisonSource ? await loadAnalysisResult(comparisonSource) : null;
  if (token !== state.waveformRenderToken || selectedCue()?.audioCueId !== cue.audioCueId) return;
  renderAudioAnalysisMode(host, cue, results, comparison);
}

async function loadAnalysisResult(source) {
  try {
    let promise = state.waveformCache.get(source.path);
    if (!promise) {
      promise = loadWaveformAnalysis(source.path, true);
      state.waveformCache.set(source.path, promise);
    }
    return { source, analysis: await promise, error: null };
  } catch (error) {
    state.waveformCache.delete(source.path);
    return { source, analysis: null, error };
  }
}

function renderAudioAnalysisMode(host, cue, results, comparison) {
  const mode = state.audioAnalysisMode;
  const active = results.find(result => result.analysis) || results[0];
  const setId = results.find(result => result.source.kind === "adoption")?.source.setId || "";
  host.innerHTML = `<section class="audio-analysis-console">
    <header class="analysis-header"><div><strong>当前音频分析</strong>${badge(mode === "fmod" ? "FMOD 最终输出" : mode === "compare" ? "A/B 版本对比" : "FMOD 采用素材", mode === "fmod" ? "warn" : "ok")}<code>${esc(String(setId || cue.audioCueId).slice(0,24))}</code></div><div class="analysis-header-actions"><nav aria-label="音频分析模式">${analysisModeButton("source","源素材",mode)}${analysisModeButton("fmod","FMOD 最终输出",mode)}${analysisModeButton("compare","A/B 对比",mode)}</nav><details class="analysis-cue-menu"><summary aria-label="Cue 管理">•••</summary><div><button type="button" data-analysis-cue-action="duplicate">复制 Cue</button><button type="button" data-analysis-cue-action="archive">${cue.lifecycle === "Archived" ? "恢复为 Draft" : "归档 Cue"}</button><button type="button" class="danger" data-analysis-cue-action="delete"${cue.lifecycle === "Archived" && referenceCount(cue.audioCueId) === 0 ? "" : " disabled"}>永久删除</button></div></details></div></header>
    ${mode === "fmod" ? renderFmodOutputUnavailable(cue) : mode === "compare" ? renderVersionComparisonMode(cue, active, comparison) : renderSourceAnalysis(cue, results, comparison)}
  </section>`;
  bindAudioAnalysisPanel(host, cue, results, comparison);
}

function analysisModeButton(value, label, current) { return `<button type="button" class="${value === current ? "active" : ""}" data-analysis-mode="${value}">${label}</button>`; }

function renderSourceAnalysis(cue, results, comparison) {
  const active = results.find(result => result.analysis);
  if (!active) return `<div class="waveform-empty error"><strong>素材分析失败</strong><span>${esc(results[0]?.error?.message || "没有可解码的源文件。")}</span></div>`;
  const maxDuration = Math.max(...results.filter(result => result.analysis).map(result => result.analysis.duration));
  return `${renderAnalysisTransport(active.source, maxDuration)}
    <div class="analysis-time-ruler">${[0,.2,.4,.6,.8,1].map(ratio => `<span style="left:${ratio*100}%">${formatTimelineTime(maxDuration*ratio)}</span>`).join("")}</div>
    <div class="analysis-layers">${results.map((result,index) => renderAnalysisLayer(result,index,maxDuration)).join("")}</div>
    <section class="spectrogram-panel"><div class="spectrogram-scale"><span>20 kHz</span><span>10 kHz</span><span>1 kHz</span><span>100 Hz</span><span>20 Hz</span></div><canvas data-analysis-spectrogram width="960" height="180" aria-label="${esc(cue.displayName || cue.audioCueId)} 频谱图"></canvas></section>
    <div class="frequency-bands" aria-label="frequency-bands">${renderFrequencyBand("低频",active.analysis.bands.low,"low")}${renderFrequencyBand("中频",active.analysis.bands.mid,"mid")}${renderFrequencyBand("高频",active.analysis.bands.high,"high")}</div>
    ${renderAnalysisMetrics(active.analysis)}${renderAnalysisIssues(active.analysis)}${renderVersionSummary(active, comparison)}
    <p class="waveform-disclaimer">本页指标来自当前采用源文件，LUFS-I 为未经过 FMOD Bus、参数和空间化处理的近似值；最终输出必须通过 Unity / FMOD 捕获后复核。</p>`;
}

function renderAnalysisTransport(source, duration) {
  const current=Math.max(0,Math.min(duration,state.audioAnalysisSeek));
  return `<div class="analysis-transport"><div class="analysis-transport-controls"><div><button type="button" data-analysis-play title="播放/暂停源素材（Space）" aria-label="播放或暂停源素材">▶</button><button type="button" data-analysis-stop title="停止" aria-label="停止源素材">■</button><button type="button" class="${state.audioAnalysisLoop ? "active" : ""}" data-analysis-loop title="循环" aria-label="循环播放">↻</button><kbd>Space</kbd><output data-analysis-time>${formatTransportTime(current)} / ${formatTransportTime(duration)}</output></div><code title="${esc(source.path)}">${esc(source.layer || source.ownerCueId || "source")} · ${esc(source.kind === "adoption" ? `take ${String(source.take).padStart(2,"0")}` : "预览源")}</code></div></div>`;
}

function renderAnalysisLayer(result, index, duration) {
  const { source, analysis, error } = result;
  const label = source.layer || (source.kind === "shared" ? "shared" : "source");
  if (error) return `<article class="analysis-layer error"><div><strong>${esc(label)}</strong><small>解析失败</small></div><p>${esc(error.message || "无法解码")}</p></article>`;
  const current=Math.max(0,Math.min(duration,state.audioAnalysisSeek)),progress=duration>0?current/duration*100:0;
  return `<article class="analysis-layer"><div><strong>${esc(label)}</strong><small>${Number.isFinite(source.take) ? `take ${String(source.take).padStart(2,"0")}` : `${analysis.channels}ch`}</small></div><div class="analysis-waveform-track" data-analysis-waveform-track role="slider" tabindex="0" aria-label="${esc(label)} 播放位置" aria-valuemin="0" aria-valuemax="${duration}" aria-valuenow="${current}"><canvas class="waveform-canvas" data-waveform-source="${index}" width="960" height="72"></canvas><i class="analysis-playhead" data-analysis-playhead style="left:${progress}%"><span data-analysis-playhead-time>${formatTimelineTime(current)}</span></i></div><dl><div><dt>增益</dt><dd>${numberValue(source.gainDb).toFixed(1)} dB</dd></div><div><dt>延迟</dt><dd>${numberValue(source.delayMs).toFixed(0)} ms</dd></div></dl></article>`;
}

function renderAnalysisMetrics(analysis) {
  const metrics = [["LUFS-I≈",analysis.lufsEstimate.toFixed(1),"source estimate"],["Sample Peak",analysis.peakDb.toFixed(1),"dBFS"],["RMS",analysis.rmsDb.toFixed(1),"dBFS"],["Crest",analysis.crestDb.toFixed(1),"dB"],["动态范围",analysis.dynamicRangeDb.toFixed(1),"dB"],["Decay",analysis.decaySeconds.toFixed(2),"s"]];
  return `<section class="analysis-metrics">${metrics.map(([label,value,unit]) => `<div><span>${label}</span><strong>${value}</strong><small>${unit}</small></div>`).join("")}</section>`;
}

function renderAnalysisIssues(analysis) {
  const issues = analysis.issues.length ? analysis.issues : [{ tone:"ok", code:"CLEAN", label:"未发现削波或明显静音问题", time:0 }];
  return `<section class="analysis-results"><strong>分析结果</strong><div>${issues.map((issue,index) => `<button type="button" class="${issue.tone}" data-analysis-issue="${index}" data-analysis-time-value="${issue.time}">${issue.tone === "error" ? "×" : issue.tone === "warn" ? "△" : "✓"} ${esc(issue.label)}</button>`).join("")}</div><span>DC Offset ${(analysis.dcOffset*100).toFixed(2)}%</span></section>`;
}

function renderVersionSummary(active, comparison) {
  if (!comparison?.analysis) return `<section class="analysis-version-empty"><strong>版本对比</strong><span>当前 Event 没有可分析的上一版本。</span></section>`;
  const loudnessDelta = active.analysis.lufsEstimate - comparison.analysis.lufsEstimate;
  const decayDelta = active.analysis.decaySeconds - comparison.analysis.decaySeconds;
  return `<section class="analysis-version-summary"><div><span>当前版本</span><canvas data-version-waveform="current" width="360" height="54"></canvas><small>${esc(String(active.source.setId || "current").slice(0,18))}</small></div><div class="version-deltas"><span>响度 <strong>${signed(loudnessDelta)} LU</strong></span><span>尾音 <strong>${signed(decayDelta)} s</strong></span></div><div><span>上一版本</span><canvas data-version-waveform="previous" width="360" height="54"></canvas><small>${esc(String(comparison.source.setId || "previous").slice(0,18))}</small></div></section>`;
}

function renderFmodOutputUnavailable() {
  return `<div class="fmod-output-empty"><div class="output-icon">⌁</div><strong>尚未捕获 FMOD 最终输出</strong><p>当前 Unity 预览桥只回写播放结果，不提供可分析的混音文件。这里不会用源素材冒充 Bus、空间化和参数处理后的最终输出。</p><code>AUDIO_FMOD_OUTPUT_CAPTURE_UNAVAILABLE</code><button type="button" class="primary" data-analysis-fmod-preview>开始 Unity / FMOD 真实试听</button></div>`;
}

function renderVersionComparisonMode(cue, active, comparison) {
  if (!active?.analysis || !comparison?.analysis) return `<div class="waveform-empty"><strong>没有足够的版本进行 A/B 对比</strong><span>至少需要当前版本和一个历史 adoption set。</span></div>`;
  return `<div class="comparison-workspace"><header><div><strong>当前版本</strong><code>${esc(active.source.setId || "current")}</code></div><span>对比</span><div><strong>上一版本</strong><code>${esc(comparison.source.setId || "previous")}</code></div></header>${renderVersionSummary(active,comparison)}<div class="comparison-metrics"><span>响度差 <strong>${signed(active.analysis.lufsEstimate-comparison.analysis.lufsEstimate)} LU</strong></span><span>峰值差 <strong>${signed(active.analysis.peakDb-comparison.analysis.peakDb)} dB</strong></span><span>动态差 <strong>${signed(active.analysis.dynamicRangeDb-comparison.analysis.dynamicRangeDb)} dB</strong></span><span>尾音差 <strong>${signed(active.analysis.decaySeconds-comparison.analysis.decaySeconds)} s</strong></span></div><p class="waveform-disclaimer">A/B 比较源文件分析结果；点击下方波形可分别播放对应版本。</p></div>`;
}

function bindAudioAnalysisPanel(host, cue, results, comparison) {
  host.querySelectorAll("[data-analysis-mode]").forEach(button => button.addEventListener("click", () => { stopAudioAnalysisPlayback(); state.audioAnalysisMode=button.dataset.analysisMode; renderAudioAnalysisMode(host,cue,results,comparison); }));
  results.forEach((result,index) => { if (result.analysis) drawWaveform(host.querySelector(`[data-waveform-source="${index}"]`),result.analysis.peaks); });
  const active = results.find(result => result.analysis);
  if (active?.analysis?.spectrogram) drawSpectrogram(host.querySelector("[data-analysis-spectrogram]"),active.analysis.spectrogram);
  if (active?.analysis) drawWaveform(host.querySelector('[data-version-waveform="current"]'),active.analysis.peaks);
  if (comparison?.analysis) drawWaveform(host.querySelector('[data-version-waveform="previous"]'),comparison.analysis.peaks);
  host.querySelector('[data-version-waveform="current"]')?.addEventListener("click", () => playAnalysisSource(active?.source,host));
  host.querySelector('[data-version-waveform="previous"]')?.addEventListener("click", () => playAnalysisSource(comparison?.source,host));
  host.querySelector("[data-analysis-play]")?.addEventListener("click", () => playAnalysisSource(active?.source, host));
  host.querySelector("[data-analysis-stop]")?.addEventListener("click", () => stopAudioAnalysisPlayback(true,host,active?.analysis?.duration));
  host.querySelector("[data-analysis-loop]")?.addEventListener("click", event => { state.audioAnalysisLoop=!state.audioAnalysisLoop;event.currentTarget.classList.toggle("active",state.audioAnalysisLoop);if(state.audioAnalysisPlayer)state.audioAnalysisPlayer.loop=state.audioAnalysisLoop; });
  host.querySelectorAll("[data-analysis-waveform-track]").forEach(track => {
    track.addEventListener("click", event => seekAnalysisFromWaveform(event,host,active?.analysis?.duration));
    track.addEventListener("keydown", event => seekAnalysisFromKeyboard(event,host,active?.analysis?.duration));
  });
  host.querySelectorAll("[data-analysis-issue]").forEach(button => button.addEventListener("click", () => playAnalysisSource(active?.source,host,numberValue(button.dataset.analysisTimeValue))));
  host.querySelector("[data-analysis-fmod-preview]")?.addEventListener("click", () => auditionAction("preview"));
  host.querySelectorAll("[data-analysis-cue-action]").forEach(button => button.addEventListener("click", () => { if(button.dataset.analysisCueAction === "duplicate")duplicateCue();else if(button.dataset.analysisCueAction === "archive")toggleArchive();else deleteCue(); }));
}

function playAnalysisSource(source, host, startTime=null) {
  if (!source?.path) return;
  stopAudioAnalysisPlayback(false);
  const player = new Audio(toStaticProjectUrl(source.path));
  player.loop = state.audioAnalysisLoop;
  const requestedTime=startTime===null?state.audioAnalysisSeek:numberValue(startTime);
  state.audioAnalysisPlayer = player;
  const update = () => { updateAnalysisProgress(host,player.currentTime,player.duration);if(!player.paused)state.audioAnalysisTimer=requestAnimationFrame(update); };
  player.addEventListener("ended",()=>{if(!player.loop)stopAudioAnalysisPlayback(true,host,player.duration);});
  const beginPlayback=()=>{if(state.audioAnalysisPlayer!==player)return;player.currentTime=Math.max(0,Math.min(Number.isFinite(player.duration)?player.duration:requestedTime,requestedTime));player.play().then(()=>{updateAnalysisPlayButton(host,true);update();}).catch(error => setServer(`源素材播放失败：${error.message}`,"error"));};
  if(player.readyState>=1)beginPlayback();else player.addEventListener("loadedmetadata",beginPlayback,{once:true});
}

function toggleAudioAnalysisPlayback(cue) { const host=el.editorWaveformPanel.querySelector("[data-waveform-preview]");if(!host)return;if(state.audioAnalysisPlayer&&!state.audioAnalysisPlayer.paused){state.audioAnalysisPlayer.pause();cancelAnimationFrame(state.audioAnalysisTimer);state.audioAnalysisTimer=0;updateAnalysisPlayButton(host,false);return;}const source=findWaveformSources(cue)[0];if(source)playAnalysisSource(source,host); }
function seekAnalysisFromWaveform(event,host,duration) { const bounds=event.currentTarget.getBoundingClientRect();if(bounds.width<=0)return;const ratio=Math.max(0,Math.min(1,(event.clientX-bounds.left)/bounds.width));seekAnalysisPlayback(numberValue(duration)*ratio,host,duration); }
function seekAnalysisFromKeyboard(event,host,duration) { const step=Math.max(.01,numberValue(duration)/100);let next=state.audioAnalysisSeek;if(event.key==="ArrowLeft")next-=step;else if(event.key==="ArrowRight")next+=step;else if(event.key==="Home")next=0;else if(event.key==="End")next=numberValue(duration);else return;event.preventDefault();seekAnalysisPlayback(next,host,duration); }
function seekAnalysisPlayback(time,host,duration) { state.audioAnalysisSeek=Math.max(0,Math.min(numberValue(duration),numberValue(time)));if(state.audioAnalysisPlayer)state.audioAnalysisPlayer.currentTime=state.audioAnalysisSeek;updateAnalysisProgress(host,state.audioAnalysisSeek,duration); }
function updateAnalysisProgress(host,current,duration) { const safeDuration=Number.isFinite(duration)?duration:0,safeCurrent=Math.max(0,Math.min(safeDuration,numberValue(current))),progress=safeDuration>0?safeCurrent/safeDuration*100:0;state.audioAnalysisSeek=safeCurrent;const time=host?.querySelector("[data-analysis-time]");if(time)time.textContent=`${formatTransportTime(safeCurrent)} / ${formatTransportTime(safeDuration)}`;host?.querySelectorAll("[data-analysis-playhead]").forEach(playhead=>{playhead.style.left=`${progress}%`;const track=playhead.closest("[data-analysis-waveform-track]"),label=playhead.querySelector("[data-analysis-playhead-time]");if(track){track.setAttribute("aria-valuemax",safeDuration);track.setAttribute("aria-valuenow",safeCurrent);}if(label)label.textContent=formatTimelineTime(safeCurrent);}); }
function updateAnalysisPlayButton(host,playing) { const button=host?.querySelector("[data-analysis-play]");if(!button)return;button.textContent=playing?"❚❚":"▶";button.classList.toggle("active",playing);button.setAttribute("aria-label",playing?"暂停源素材":"播放或暂停源素材"); }
function stopAudioAnalysisPlayback(reset=true,host=null,duration=0) { if(state.audioAnalysisPlayer){state.audioAnalysisPlayer.pause();state.audioAnalysisPlayer=null;}cancelAnimationFrame(state.audioAnalysisTimer);state.audioAnalysisTimer=0;updateAnalysisPlayButton(host,false);if(reset){state.audioAnalysisSeek=0;updateAnalysisProgress(host,0,duration);} }

function findWaveformSources(cue) {
  const eventState = (state.generationManifest?.eventStates || []).find(item => sameId(item.eventDefinitionId, cue?.eventDefinitionId));
  const activeSet = (state.generationManifest?.adoptionSets || []).find(item => sameId(item.setId, eventState?.activeSetId) && item.status === "Applied");
  const adoptedSources = (activeSet?.sources || [])
    .map(item => ({ item, path: String(item?.projectRelativePath || item?.path || "").trim() }))
    .filter(value => value.path)
    .map(({ item, path }) => { const mix=(activeSet.layerMix||[]).find(value=>sameId(value.layer,item.layer))||{};return { path, ownerCueId: cue.audioCueId, kind: "adoption", setId: activeSet.setId, layer: item.layer, take: Number(item.take), gainDb:numberValue(mix.gainDb), delayMs:numberValue(mix.delayMs) }; });
  if (adoptedSources.length) return adoptedSources;

  const ownPath = String(cue?.sourcePreviewPath || "").trim();
  if (ownPath) return [{ path: ownPath, ownerCueId: cue.audioCueId, kind: "cue" }];
  const sibling = state.manifest?.cues?.find(item => sameId(item.eventDefinitionId, cue?.eventDefinitionId) && String(item.sourcePreviewPath || "").trim());
  return sibling ? [{ path: sibling.sourcePreviewPath.trim(), ownerCueId: sibling.audioCueId, kind: "shared" }] : [];
}

function findComparisonSource(cue, activeSetId) {
  const sets=(state.generationManifest?.adoptionSets||[]).filter(item=>sameId(item.eventDefinitionId,cue?.eventDefinitionId)&&!sameId(item.setId,activeSetId)&&item.sources?.length).slice().sort((a,b)=>String(b.createdAtUtc||"").localeCompare(String(a.createdAtUtc||"")));
  const set=sets[0],source=set?.sources?.find(item=>String(item.projectRelativePath||item.path||"").trim());
  if(!source)return null;
  const mix=(set.layerMix||[]).find(item=>sameId(item.layer,source.layer))||{};
  return { path:String(source.projectRelativePath||source.path).trim(),ownerCueId:cue.audioCueId,kind:"history",setId:set.setId,layer:source.layer,take:Number(source.take),gainDb:numberValue(mix.gainDb),delayMs:numberValue(mix.delayMs) };
}

async function loadWaveformAnalysis(sourcePath, detailed = false) {
  const response = await fetch(toStaticProjectUrl(sourcePath));
  if (!response.ok) throw new Error(`源素材读取失败：${response.status} ${response.statusText}`);
  const bytes = await response.arrayBuffer();
  state.audioContext ||= new (window.AudioContext || window.webkitAudioContext)();
  const audioBuffer = await state.audioContext.decodeAudioData(bytes.slice(0));
  return analyzeAudioBuffer(audioBuffer, detailed);
}

function drawWaveform(canvas, peaks) {
  if (!canvas) return;
  const context = canvas.getContext("2d");
  const width = canvas.width;
  const height = canvas.height;
  const center = height / 2;
  context.clearRect(0, 0, width, height);
  context.fillStyle = "#0a1018";
  context.fillRect(0, 0, width, height);
  context.strokeStyle = "#253143";
  context.beginPath();
  context.moveTo(0, center + 0.5);
  context.lineTo(width, center + 0.5);
  context.stroke();
  context.strokeStyle = "#65d6c7";
  context.lineWidth = Math.max(1, width / peaks.length * 0.7);
  context.beginPath();
  for (let index = 0; index < peaks.length; index++) {
    const x = (index + 0.5) * width / peaks.length;
    const amplitude = Math.max(1, peaks[index] * (height * 0.44));
    context.moveTo(x, center - amplitude);
    context.lineTo(x, center + amplitude);
  }
  context.stroke();
}

function renderFrequencyBand(label, value, toneName) {
  return `<div class="frequency-band ${toneName}"><span>${label}</span><div><i style="width:${Math.max(2, value)}%"></i></div><strong>${value}%</strong></div>`;
}

function toStaticProjectUrl(path) {
  return `/${String(path || "").split("/").map(segment => encodeURIComponent(segment)).join("/")}`;
}

function formatDuration(seconds) {
  if (!Number.isFinite(seconds)) return "—";
  return seconds < 1 ? `${Math.round(seconds * 1000)} ms` : `${seconds.toFixed(seconds < 10 ? 2 : 1)} s`;
}

function formatTransportTime(seconds) { if(!Number.isFinite(seconds))return "00:00.000";const minutes=Math.floor(seconds/60),rest=seconds-minutes*60;return `${String(minutes).padStart(2,"0")}:${rest.toFixed(3).padStart(6,"0")}`; }
function formatTimelineTime(seconds) { return `${Math.max(0,seconds).toFixed(3)}s`; }
function signed(value) { const number=numberValue(value);return `${number>=0?"+":""}${number.toFixed(2)}`; }
