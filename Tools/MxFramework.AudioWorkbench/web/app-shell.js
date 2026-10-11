const APP_SCREENS = {
  dashboard: { breadcrumb:"项目总览", kicker:"Project Overview", title:"运行时激活中心", description:"只显示当前项目真正需要处理的决定，不用在完整目录中寻找下一步。" },
  cues: { breadcrumb:"Cue 制作", kicker:"Cue Authoring", title:"Cue 制作台", description:"在一个界面完成语义、Event 绑定、素材版本与真实试听。", tab:"semantic" },
  candidates: { breadcrumb:"候选实验室", kicker:"Candidate Lab", title:"候选实验室", description:"生成、A/B、微剪辑与采用集中在同一条决策链中。", tab:"candidates" },
  basis: { breadcrumb:"Basis 与响应", kicker:"Physical Basis", title:"Basis 与响应设计", description:"把声音素材、稳定 Basis 身份与物理响应规则明确分开。", tab:"basis" },
  matrix: { breadcrumb:"材质覆盖", kicker:"Coverage Matrix", title:"公共材质覆盖", description:"矩阵只回答哪些 Pair × Variant 可以运行；编辑与创建放在右侧上下文。" },
  preview: { breadcrumb:"游戏预览与发布", kicker:"Runtime Preview", title:"游戏预览与发布", description:"使用同一份 Physical Resolver 和真实 FMOD Event 验证最终玩家体验。", tab:"scenario" }
};

function initialAppScreen() {
  const params = new URLSearchParams(location.search);
  const requested = params.get("screen");
  if (["dashboard","cues","candidates","basis","matrix","preview"].includes(requested)) return requested;
  return params.get("mode") === "pairs" ? "matrix" : "dashboard";
}

function renderAppChrome() {
  const config = APP_SCREENS[state.appScreen] || APP_SCREENS.dashboard;
  el.breadcrumbTitle.textContent = config.breadcrumb;
  el.pageKicker.textContent = config.kicker;
  el.pageTitle.textContent = config.title;
  el.pageDescription.textContent = config.description;
  const activation = state.payload?.runtimeActivation || {};
  const diagnostics = [...(state.payload?.validation?.diagnostics || []), ...(state.payload?.audioPhysical?.validation?.diagnostics || [])];
  const activeCues = (state.manifest?.cues || []).filter(item => item.lifecycle === "Active");
  const candidateCues = activeCues.filter(item => {
    const event = findEvent(item.eventDefinitionId);
    const eventState = (state.generationManifest?.eventStates || []).find(value => sameId(value.eventDefinitionId,event?.id));
    return !eventState?.activeSetId;
  });
  const runtimeSync = state.payload?.runtimeSync || {};
  el.dashboardCount.textContent = activation.blockers?.length || 0;
  el.cueCount.textContent = activeCues.length;
  el.candidateCount.textContent = candidateCues.length;
  el.basisCount.textContent = activation.activeBasisCount || 0;
  el.coverageCount.textContent = numberValue(activation.coverageMissing) + numberValue(activation.coverageAmbiguous);
  el.blockerCount.textContent = activation.blockers?.length || 0;
  el.snapshotState.textContent = activation.snapshotReady ? "current" : "stale";
  el.diagnosticCount.textContent = diagnostics.length;
  const basisTotal = numberValue(activation.activeBasisCount);
  const adopted = numberValue(activation.adoptedBasisCount);
  const percent = activation.ready ? 100 : basisTotal ? Math.round((adopted / basisTotal) * 70 + (activation.coverageMissing || activation.coverageAmbiguous ? 0 : 12) + (activation.snapshotReady ? 8 : 0)) : 0;
  el.activationPercent.textContent = `${Math.min(100,percent)}%`;
  el.activationProgress.value = Math.min(100,percent);
  el.activationBasisSummary.textContent = `${adopted} / ${basisTotal} Basis`;
  el.activationMode.textContent = activation.ready ? "GeneratedLinked" : activation.publishReady ? "PublishReady" : "Blocked";
  el.pageActions.innerHTML = state.appScreen === "dashboard" ? `<span class="badge ${runtimeSync.pendingCount ? "warn" : "ok"}">${runtimeSync.pendingCount ? `${runtimeSync.pendingCount} 个待同步` : "游戏目录已同步"}</span><button type="button" data-page-sync>一键同步到游戏</button><button type="button" data-page-copy>复制交接摘要</button>`
    : state.appScreen === "matrix" ? `<button type="button" data-page-coverage>重新校验 Required</button>`
    : state.appScreen === "preview" ? `<span class="badge ${activation.ready ? "ok" : "warn"}">${activation.ready ? "GeneratedLinked" : "LegacyFallback"}</span>`
    : state.appScreen === "cues" ? `<button type="button" class="primary" data-page-new-cue>＋ 新建 Cue</button>` : "";
  el.pageActions.querySelector("[data-page-copy]")?.addEventListener("click", copyRuntimeActivationDiagnostics);
  el.pageActions.querySelector("[data-page-sync]")?.addEventListener("click", publishRuntime);
  el.pageActions.querySelector("[data-page-coverage]")?.addEventListener("click", refreshPhysicalCoverage);
  el.pageActions.querySelector("[data-page-new-cue]")?.addEventListener("click", createCue);
  updateRuntimeSyncControls();
}

function renderDashboard() {
  if (state.appScreen !== "dashboard") return;
  const activation = state.payload?.runtimeActivation;
  if (!activation) { el.dashboardContent.innerHTML = empty("Authoring 服务尚未返回运行时激活状态。"); return; }
  const blockers = activation.blockers || [];
  const work = [
    { tone:"danger", title:`直接采用 ${Math.max(0,activation.activeBasisCount-activation.adoptedBasisCount)} 个 Basis 当前 Event`, detail:"保留当前 Event；空 Event 作为可替换的静音占位", action:"direct-adopt" },
    { tone:"warn", title:`完成 ${Math.max(0,activation.activeBasisCount-activation.approvedBasisCount)} 个 Basis 权威审听`, detail:"试听证据必须匹配当前 Basis、adoption set 与 Bank hash", screen:"basis" },
    { tone:"ok", title:activation.coverageMissing || activation.coverageAmbiguous ? "公共材质覆盖仍需处理" : "公共材质覆盖已完整", detail:`Required ${activation.coverageRequired} · missing ${activation.coverageMissing} · ambiguous ${activation.coverageAmbiguous}`, screen:"matrix" }
  ];
  const recent = [
    { title:"FMOD Snapshot", value:activation.snapshotReady ? "已刷新" : "需要同步", tone:activation.snapshotReady?"ok":"warn" },
    { title:"Catalog mirror", value:activation.mirrorsMatch ? "一致" : "尚未联接", tone:activation.mirrorsMatch?"ok":"warn" },
    { title:"CharacterTest", value:activation.ready ? "GeneratedLinked" : "LegacyFallback", tone:activation.ready?"ok":"error" }
  ];
  el.dashboardContent.innerHTML = `<section class="dashboard-card work-queue"><header><div><h2>下一批工作</h2><p>按照发布阻断顺序排列</p></div><span class="badge warn">${blockers.length} 个阻断</span></header><div>${work.map(item=>`<button type="button" class="work-item ${item.tone}" ${item.action ? `data-dashboard-action="${item.action}"` : `data-dashboard-screen="${item.screen}"`}><span></span><div><strong>${esc(item.title)}</strong><small>${esc(item.detail)}</small></div><b>开始处理 →</b></button>`).join("")}</div></section>
    <section class="dashboard-card health-card"><header><div><h2>内容健康度</h2><p>Active scope only</p></div></header><div class="health-list">${[["Cue 审听",activation.readyCueCount,activation.activeCueCount],["Basis 采用",activation.adoptedBasisCount,activation.activeBasisCount],["Basis 批准",activation.approvedBasisCount,activation.activeBasisCount]].map(([label,value,total])=>`<article><div><span>${label}</span><strong>${value}/${total}</strong></div><progress max="${Math.max(1,total)}" value="${value}"></progress></article>`).join("")}</div></section>
    <section class="dashboard-card recent-card"><header><div><h2>最近状态</h2><p>本次 Authoring 快照</p></div></header><div>${recent.map(item=>`<article><span class="status-dot ${item.tone}">✓</span><div><strong>${item.title}</strong><small>${item.value}</small></div></article>`).join("")}</div><button type="button" data-dashboard-screen="preview">打开发布检查 →</button></section>`;
  el.dashboardContent.querySelectorAll("[data-dashboard-screen]").forEach(button=>button.addEventListener("click",()=>setAppScreen(button.dataset.dashboardScreen)));
  el.dashboardContent.querySelector("[data-dashboard-action='direct-adopt']")?.addEventListener("click",directAdoptCurrentAudio);
}
