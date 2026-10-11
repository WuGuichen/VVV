// Audio workspace model, filtering, and primary rendering.
function renderAudioWorkspace() {
  if (!el.audioWorkspace) return;
  const model = createAudioWorkspaceModel();
  syncAudioFilterControls(model);
  renderAudioSummary(model);
  el.audioProviderSummary.innerHTML = renderAudioProviderSummary(model);
  el.audioBankList.innerHTML = renderAudioBankList(model);
  el.audioCueList.innerHTML = renderAudioCueList(model);
  el.audioImpactPanel.innerHTML = renderAudioImpactPanel(model);
}

function createAudioWorkspaceModel() {
  const rows = getNormalizedItems()
    .filter(isAudioResourceItem)
    .map(createAudioRow)
    .sort(compareAudioRows);
  const dedupedRows = dedupeAudioRows(rows);
  const eventRows = dedupedRows.filter(isAudioEventLibraryRow);
  const sourceSampleRows = dedupedRows.filter(row => !isAudioEventLibraryRow(row));
  const provider = getAudioProviderState(dedupedRows);
  const bankOptions = getAudioBankOptions(eventRows);
  const filteredRows = filterAudioRows(dedupedRows, provider);
  const filteredEventRows = filteredRows.filter(isAudioEventLibraryRow);
  const filteredSourceRows = filteredRows.filter(row => !isAudioEventLibraryRow(row));
  const bankGroups = groupAudioRowsByBank(filteredEventRows);
  const cueRows = dedupedRows
    .filter(row => row.audioCueId || row.runtimeAvailability === "AudioCueOnly" || row.inCueManifest)
    .sort(compareAudioRows);
  let selectedRow = dedupedRows.find(row => selectionMatchesItem(row.item, state.selectedResourceKey)) || null;
  const selectedWasImplicit = !selectedRow;
  if (!selectedRow) {
    selectedRow = getRecommendedAudioSelection(dedupedRows, provider, cueRows);
    if (selectedRow) state.selectedResourceKey = selectedRow.item.key;
  }
  const diagnosticSummary = summarizeAudioDiagnosticHealth(
    mergeAudioDiagnostics(dedupedRows.flatMap(row => row.diagnostics), provider.diagnostics));
  const health = getAudioHealth(provider, eventRows, cueRows, diagnosticSummary);

  return {
    provider,
    health,
    rows: dedupedRows,
    eventRows,
    sourceSampleRows,
    filteredRows,
    filteredEventRows,
    filteredSourceRows,
    bankGroups,
    cueRows,
    selectedRow,
    selectedWasImplicit,
    bankOptions,
    diagnosticsCount: diagnosticSummary.actionableCount,
    diagnosticSummary,
    semanticApiState: getAudioSemanticApiState()
  };
}

function isAudioResourceItem(item) {
  if (!item) return false;
  const text = [
    item.kind,
    item.usage,
    item.sourceKind,
    item.runtimeAvailability,
    item.runtimeBindingKind,
    item.providerId,
    asArray(item.providerIds).join(" "),
    item.fmodEventPath,
    item.audioCueId,
    item.audioEventDefinitionId,
    stringValue(pick(item.metadata, "fmodEventPath")),
    stringValue(pick(item.metadata, "audioCueId")),
    stringValue(pick(item.metadata, "audioEventDefinitionId"))
  ].join(" ").toLowerCase();
  return text.includes("audio")
    || text.includes("fmod")
    || text.includes("audiocueonly")
    || Boolean(item.fmodEventPath || item.audioCueId || item.audioEventDefinitionId);
}

function createAudioRow(item) {
  const banks = getAudioStringList(item, "banks", "bank", "bankName", "requiredBanks");
  const parameters = getAudioStringList(item, "parameters", "parameterNames", "globalParameters");
  const fmodEventPath = stringValue(getAudioFirstValue(item, "fmodEventPath", "eventPath", "path")) || item.fmodEventPath || "";
  const fmodEventGuid = stringValue(getAudioFirstValue(item, "fmodEventGuid", "eventGuid", "guid")) || "";
  const audioCueId = stringValue(getAudioFirstValue(item, "audioCueId", "cueId")) || item.audioCueId || "";
  const audioEventDefinitionId = stringValue(getAudioFirstValue(item, "audioEventDefinitionId", "eventDefinitionId")) || item.audioEventDefinitionId || "";
  const semanticApiState = getAudioSemanticApiState();
  const semanticEntry = findAudioSemanticEntry(audioCueId, fmodEventPath, fmodEventGuid);
  const semanticTags = parseAudioStringList(firstString(
    getAudioFirstValue(item, "audioSemanticTags", "semanticTags"),
    pick(semanticEntry, "tags")
  ));
  const diagnostics = getAudioDiagnosticsForRow(item, fmodEventPath, fmodEventGuid, audioCueId, audioEventDefinitionId);
  const references = getReferencesForItem(item);
  const audioPlanEntries = getAudioPlanEntriesForRow(item, banks, audioCueId, audioEventDefinitionId, fmodEventPath);
  const manifestEntry = findAudioCueManifestEntry(item, audioCueId, audioEventDefinitionId, fmodEventPath, fmodEventGuid);
  const packageState = getAudioPackageState(references, audioPlanEntries, manifestEntry);

  return {
    item,
    resourceKey: item.resourceKey || item.runtimeResourceKey || item.packageResourceKey || item.key,
    displayName: formatResourceDisplayName(item),
    runtimeAvailability: item.runtimeAvailability || "",
    fmodEventPath,
    fmodEventGuid,
    audioCueId,
    audioEventDefinitionId,
    semanticEntry,
    semanticStatus: semanticApiState === "ready"
      ? firstString(getAudioFirstValue(item, "audioSemanticStatus"), semanticEntry ? "Draft" : "Missing")
      : semanticApiState === "error" ? "Error" : "Unavailable",
    semanticDisplayName: firstString(getAudioFirstValue(item, "audioSemanticDisplayName"), pick(semanticEntry, "displayName")),
    semanticPurpose: firstString(getAudioFirstValue(item, "audioSemanticPurpose", "purpose"), pick(semanticEntry, "purpose")),
    semanticContext: firstString(getAudioFirstValue(item, "audioSemanticContext", "context"), pick(semanticEntry, "context")),
    semanticTags,
    sourcePreviewPath: firstString(getAudioFirstValue(item, "sourcePreviewPath"), pick(semanticEntry, "sourcePreviewPath")),
    auditionStatus: firstString(getAudioFirstValue(item, "auditionStatus"), pick(semanticEntry, "auditionStatus"), "Needed"),
    replacementContract: buildAudioReplacementContract(item, semanticEntry),
    banks: banks.length > 0 ? banks : ["未绑定 Bank"],
    parameters,
    is3D: getAudioBoolean(item, "is3D", "is3d", "spatial", "spatialized", "isSpatial", "isSpatialized"),
    isLoop: getAudioBoolean(item, "isLoop", "loop", "looping", "isLooping"),
    isStream: getAudioBoolean(item, "isStream", "stream", "streaming", "isStreaming"),
    lengthMs: getAudioNumber(item, "lengthMs", "durationMs"),
    minDistance: getAudioNumber(item, "minDistance"),
    maxDistance: getAudioNumber(item, "maxDistance", "maximumDistance", "maxAudibleDistance"),
    cacheStale: getAudioBoolean(item, "cacheStale") === true,
    cacheValid: getAudioBoolean(item, "cacheValid"),
    diagnostics,
    references,
    audioPlanEntries,
    manifestEntry,
    inAudioPlan: audioPlanEntries.length > 0,
    inCueManifest: Boolean(manifestEntry),
    packageState,
    profileStatus: getBuildProfileStatus(item)
  };
}

function dedupeAudioRows(rows) {
  const seen = new Set();
  const result = [];
  for (const row of rows) {
    const key = [
      row.fmodEventGuid,
      row.fmodEventPath,
      row.audioCueId,
      row.audioEventDefinitionId,
      row.resourceKey,
      row.displayName
    ].map(value => stringValue(value).toLowerCase()).find(Boolean) || row.item.key;
    if (seen.has(key)) continue;
    seen.add(key);
    result.push(row);
  }
  return result;
}

function isAudioEventLibraryRow(row) {
  if (!row) return false;
  return Boolean(
    row.fmodEventPath
    || row.fmodEventGuid
    || row.audioCueId
    || row.audioEventDefinitionId
    || row.inCueManifest
    || row.runtimeAvailability === "AudioCueOnly"
  );
}

function getAudioProviderState(rows) {
  const provider = getProviders().find(provider => {
    const text = [provider.providerId, provider.displayName, provider.sourceKind].join(" ").toLowerCase();
    return text.includes("fmod") || text.includes("audio library");
  }) || null;
  const metadata = getAudioGlobalMetadata();
  const providerDiagnostics = mergeAudioDiagnostics(getAllDiagnostics().filter(isAudioProviderDiagnostic));
  const hasSnapshot = Boolean(stringValue(pick(metadata, "fmodAudioLibrarySnapshotPath")));
  const hasAudioSnapshotData = hasSnapshot || rows.length > 0;
  const cacheStale = getAudioMetadataBoolean(metadata, "fmodCacheStale", "cacheStale") || rows.some(row => row.cacheStale);
  const cacheValid = getAudioMetadataBoolean(metadata, "fmodCacheValid", "cacheValid");
  const unavailable = Boolean(provider && (provider.available === false || /unavailable|missing/i.test(provider.status || "")))
    || (!provider && !hasAudioSnapshotData)
    || providerDiagnostics.some(isFmodProviderUnavailableDiagnostic);
  const stale = !unavailable && (
    cacheStale
    || /stale/i.test(provider?.status || "")
    || providerDiagnostics.some(diagnostic => /FMOD_CACHE_STALE|SNAPSHOT_STALE/i.test(stringValue(pick(diagnostic, "code"))))
  );
  const hasDiagnostics = providerDiagnostics.some(isAudioActionableDiagnostic) || rows.some(row => row.diagnostics.some(isAudioInfrastructureDiagnostic));
  const stateKey = unavailable ? "unavailable" : stale ? "stale" : hasDiagnostics ? "diagnostics" : "ready";

  return {
    provider,
    stateKey,
    label: AUDIO_PROVIDER_STATE_LABELS[stateKey],
    available: Boolean(provider && !unavailable),
    stale,
    cacheValid,
    diagnostics: providerDiagnostics,
    snapshotPath: stringValue(pick(metadata, "fmodAudioLibrarySnapshotPath")),
    generatedAtUtc: stringValue(pick(metadata, "fmodGeneratedAtUtc")),
    cacheTimeUtc: stringValue(pick(metadata, "fmodCacheTimeUtc")),
    source: stringValue(pick(metadata, "fmodSource")),
    statusMessage: provider?.message || summarizeAudioDiagnostics(providerDiagnostics) || ""
  };
}

function getAudioHealth(provider, rows, cueRows, diagnosticSummary) {
  const cuePlanGaps = cueRows.filter(row => row.packageState === "referenced-missing-plan" && !row.inAudioPlan).length;
  const cueManifestGaps = cueRows.filter(row => row.packageState === "referenced-missing-plan" && !row.inCueManifest).length;
  const manifestStaleCount = cueRows.filter(row => row.packageState === "manifest-stale").length;
  const rowsWithDiagnostics = rows.filter(row => row.diagnostics.some(isAudioActionableDiagnostic)).length;
  const unreferencedRows = rows.filter(row => row.references.length === 0).length;
  const unboundBankRows = rows.filter(row => row.banks.includes("未绑定 Bank")).length;
  const staleRows = rows.filter(row => row.cacheStale).length;
  const packageTargetRows = cueRows.filter(row => row.packageState !== "not-targeted");
  const semanticReadyCount = cueRows.filter(row => row.semanticStatus === "Ready").length;
  const semanticDraftCount = cueRows.filter(row => row.semanticStatus === "Missing" || row.semanticStatus === "Draft").length;
  const semanticReviewCount = cueRows.filter(row => row.semanticStatus === "ReviewNeeded").length;
  const semanticRejectedCount = cueRows.filter(row => row.semanticStatus === "Rejected").length;
  return {
    cuePlanGaps,
    cueManifestGaps,
    manifestStaleCount,
    rowsWithDiagnostics,
    unreferencedRows,
    unboundBankRows,
    staleRows,
    diagnosticsCount: diagnosticSummary.actionableCount,
    diagnosticSummary,
    packageTargetCount: packageTargetRows.length,
    packageNotTargetedCount: cueRows.filter(row => row.packageState === "not-targeted").length,
    audioPlanReadyCount: packageTargetRows.filter(row => row.packageState === "referenced-ready" || row.packageState === "packaged-unreferenced").length,
    semanticReadyCount,
    semanticDraftCount,
    semanticReviewCount,
    semanticRejectedCount,
    providerTone: provider.stateKey === "ready" ? "ok" : provider.stateKey === "stale" ? "warn" : provider.stateKey === "unavailable" ? "error" : "info"
  };
}

function getRecommendedAudioSelection(rows, provider, cueRows) {
  if (provider.stateKey === "unavailable") return rows[0] || null;
  return rows.find(row => row.diagnostics.some(isAudioActionableDiagnostic))
    || cueRows.find(isAudioPackagingGap)
    || rows.find(row => row.banks.includes("未绑定 Bank"))
    || cueRows.find(row => row.semanticStatus === "Missing" || row.semanticStatus === "Draft" || row.semanticStatus === "Rejected")
    || cueRows.find(row => row.semanticStatus === "ReviewNeeded")
    || rows[0]
    || null;
}

function syncAudioFilterControls(model) {
  if (el.audioSearchInput) el.audioSearchInput.value = state.audioFilters.search || "";
  syncAudioBankFilter(model.bankOptions);
  if (el.audioSpatialFilter) el.audioSpatialFilter.value = state.audioFilters.spatial || "all";
  if (el.audioLoopFilter) el.audioLoopFilter.value = state.audioFilters.loop || "all";
  if (el.audioDiagnosticsFilter) el.audioDiagnosticsFilter.value = state.audioFilters.diagnostics || "all";
  if (el.audioReferencedFilter) el.audioReferencedFilter.value = state.audioFilters.referenced || "all";
}

function syncAudioBankFilter(bankOptions) {
  if (!el.audioBankFilter) return;
  const selected = bankOptions.some(([value]) => value === state.audioFilters.bank)
    ? state.audioFilters.bank
    : "all";
  state.audioFilters.bank = selected;
  el.audioBankFilter.innerHTML = bankOptions
    .map(([value, label]) => `<option value="${escapeHtml(value)}"${value === selected ? " selected" : ""}>${escapeHtml(label)}</option>`)
    .join("");
}

function renderAudioSummary(model) {
  const bankCount = model.bankOptions.length > 0 ? model.bankOptions.length - 1 : 0;
  const semanticLabel = getAudioSemanticApiLabel(model.semanticApiState);
  const sourceText = model.sourceSampleRows.length > 0 ? `，${model.sourceSampleRows.length} 源样本` : "";
  el.audioSummary.textContent = `${model.provider.label}，${model.eventRows.length} events${sourceText}，${bankCount} banks，${model.cueRows.length} cues，${semanticLabel}，${model.diagnosticSummary.errorCount} error / ${model.diagnosticSummary.warningCount} warning / ${model.diagnosticSummary.infoCount} info`;
}

function renderAudioProviderSummary(model) {
  const provider = model.provider;
  const focus = getAudioPrimaryFocus(model);
  const tone = focus.tone;
  const providerName = provider.provider?.displayName || "FMOD Audio Library";
  return `
    <section class="audio-workflow-panel ${tone}">
      <div class="audio-focus-card">
        <div>
          <span class="audio-focus-kicker">${escapeHtml(focus.kicker)}</span>
          <h2>${escapeHtml(focus.title)}</h2>
          <p>${escapeHtml(focus.body)}</p>
        </div>
        <div class="audio-focus-actions">
          ${focus.actions.map(action => `
            <button type="button" class="${escapeHtml(action.primary ? "audio-primary-action" : "secondary")}" data-audio-action="${escapeHtml(action.action)}">${escapeHtml(action.label)}</button>
          `).join("")}
        </div>
      </div>
      <div class="audio-health-grid">
        ${renderAudioHealthCard("FMOD", provider.label, provider.snapshotPath ? "snapshot 已定位" : "snapshot 缺失", model.health.providerTone, provider.stale ? "focus-stale" : "open-debug")}
        ${renderAudioHealthCard("Events", model.eventRows.length, `${model.health.unboundBankRows} 未绑定 Bank`, model.health.unboundBankRows > 0 ? "warn" : "ok", model.health.unboundBankRows > 0 ? "focus-unbound-bank" : "")}
        ${renderAudioHealthCard("当前包", `${model.health.audioPlanReadyCount}/${model.health.packageTargetCount}`, `${model.health.cuePlanGaps + model.health.cueManifestGaps + model.health.manifestStaleCount} 个进包缺口，${model.health.packageNotTargetedCount} 个未使用`, model.health.cuePlanGaps || model.health.cueManifestGaps || model.health.manifestStaleCount ? "warn" : "ok", model.health.cuePlanGaps || model.health.cueManifestGaps || model.health.manifestStaleCount ? "focus-packaging-gaps" : "")}
        ${renderAudioHealthCard("语义", `${model.health.semanticReadyCount}/${model.cueRows.length}`, `${model.health.semanticDraftCount} 草稿，${model.health.semanticReviewCount} 待试听，${model.health.semanticRejectedCount} 已拒绝`, model.health.semanticDraftCount || model.health.semanticRejectedCount ? "warn" : model.health.semanticReviewCount ? "info" : "ok", "")}
        ${renderAudioHealthCard("校验", `${model.diagnosticSummary.errorCount} / ${model.diagnosticSummary.warningCount}`, `${model.diagnosticSummary.infoCount} 条信息不阻塞`, model.diagnosticSummary.errorCount ? "error" : model.diagnosticSummary.warningCount ? "warn" : "ok", model.diagnosticsCount > 0 ? "focus-diagnostics" : "")}
      </div>
      <details class="audio-provider-details">
        <summary>FMOD snapshot / cache 详情</summary>
        <div class="audio-provider-detail-body">
          <div class="audio-status-header">
            <div>
              <h2>Provider 状态</h2>
              <p>${escapeHtml(providerName)} · ${escapeHtml(provider.label)}</p>
            </div>
            ${statusChip("FMOD", provider.label, model.health.providerTone)}
          </div>
          <div class="summary-grid audio-summary-grid">
            ${metric("Snapshot", provider.snapshotPath ? "已定位" : "缺失")}
            ${metric("生成时间", provider.generatedAtUtc || "-")}
            ${metric("Cache", provider.cacheValid === false ? "invalid" : provider.cacheValid === true ? "valid" : "-")}
            ${metric("Provider 问题", provider.diagnostics.filter(isAudioActionableDiagnostic).length)}
          </div>
          ${renderKeyValueList([
            ["snapshot 路径", provider.snapshotPath || "-"],
            ["source", provider.source || "-"],
            ["cache 时间", provider.cacheTimeUtc || "-"],
            ["provider message", provider.statusMessage || "-"]
          ])}
          <div class="audio-readonly-note">只读聚合状态；不替代 FMOD Studio，不构建 bank，不写 StreamingAssets，也不替代 CharacterStudio 字段绑定。</div>
          ${provider.diagnostics.length > 0 ? `
            <div class="audio-diagnostics-block">
              <h3>Provider 诊断</h3>
              ${renderDiagnosticsList(provider.diagnostics)}
            </div>` : ""}
        </div>
      </details>
    </section>`;
}

function getAudioPrimaryFocus(model) {
  const { provider, health } = model;
  if (provider.stateKey === "unavailable") {
    return {
      tone: "error",
      kicker: "当前阻塞",
      title: "FMOD snapshot 不能作为可用来源",
      body: "先处理 Provider 或 snapshot 问题；列表里的派生状态不应该作为最终运行时结论。",
      actions: [
        { action: "open-debug", label: "打开 Debug 详情", primary: true },
        { action: "open-browse-audio", label: "回 Browse 看音频资源" }
      ]
    };
  }
  if (provider.stateKey === "stale" || health.staleRows > 0) {
    return {
      tone: "warn",
      kicker: "先确认来源",
      title: "FMOD snapshot 可能不是最新",
      body: "可以继续看 event 和 cue，但最终确认前应该重新导出或刷新 FMOD / Unity 目录快照。",
      actions: [
        { action: "focus-stale", label: "筛选 stale 项", primary: true },
        { action: "open-debug", label: "打开 Debug 详情" }
      ]
    };
  }
  if (health.diagnosticSummary.errorCount > 0) {
    return {
      tone: "error",
      kicker: "当前阻塞",
      title: `${health.diagnosticSummary.errorCount} 条音频错误需要处理`,
      body: "错误会阻塞保存、替换或构建；信息级试听待办不会计入这里。",
      actions: [
        { action: "focus-diagnostics", label: "筛选错误与警告", primary: true },
        { action: "open-build", label: "打开 Build 诊断" }
      ]
    };
  }
  if (health.diagnosticSummary.warningCount > 0) {
    return {
      tone: "warn",
      kicker: "需要检查",
      title: `${health.diagnosticSummary.warningCount} 条音频警告需要确认`,
      body: "警告不会与人工试听 Info 混在一起；可先处理语义草稿、缺失绑定或技术证据问题。",
      actions: [
        { action: "focus-diagnostics", label: "筛选错误与警告", primary: true },
        { action: "open-debug", label: "打开 Debug 详情" }
      ]
    };
  }
  if (health.cuePlanGaps > 0 || health.cueManifestGaps > 0 || health.manifestStaleCount > 0) {
    return {
      tone: "warn",
      kicker: "当前主要问题",
      title: `${health.cuePlanGaps + health.cueManifestGaps + health.manifestStaleCount} 个当前包音频计划缺口`,
      body: "只统计已被当前 package 引用或 Plan / Manifest 不一致的 Cue；全局目录中未被该包使用的 Cue 不算问题。",
      actions: [
        { action: "focus-packaging-gaps", label: "查看未进包 Cue", primary: true },
        { action: "open-build", label: "打开 Build 诊断" },
        { action: "open-profile", label: "打开 Profile" }
      ]
    };
  }
  if (health.unboundBankRows > 0) {
    return {
      tone: "warn",
      kicker: "需要整理",
      title: `${health.unboundBankRows} 个 event 没有 Bank 归属`,
      body: "这些条目通常来自旧字段或未补齐的 providerData；先确认它们是否还应该保留。",
      actions: [
        { action: "focus-unbound-bank", label: "查看未绑定 Bank", primary: true },
        { action: "open-browse-audio", label: "回 Browse 对照资源" }
      ]
    };
  }
  if (health.semanticDraftCount > 0 || health.semanticRejectedCount > 0) {
    return {
      tone: "warn",
      kicker: "完善语义",
      title: `${health.semanticDraftCount + health.semanticRejectedCount} 个 Cue 尚未语义就绪`,
      body: "补齐名称、用途、语境和有效事件绑定；试听拒绝的 Cue 需要重新选择实现。",
      actions: [
        { action: "copy-summary", label: "复制语义状态摘要", primary: true }
      ]
    };
  }
  if (health.semanticReviewCount > 0) {
    return {
      tone: "info",
      kicker: "人工审听",
      title: `${health.semanticReviewCount} 个 Cue 等待人工试听`,
      body: "这是工作流状态，不是错误；可逐个试听源样本并在 FMOD / Unity 中确认真实事件行为。",
      actions: [
        { action: "copy-summary", label: "复制待试听摘要", primary: true }
      ]
    };
  }
  return {
    tone: "ok",
    kicker: "当前可用",
    title: "FMOD 来源与 Authoring Cue 没有明显阻塞",
    body: "当前页面只读汇总 Audio 状态；语义、Event 绑定、试听批准与发布请进入 Audio Workbench。",
    actions: [
      { action: "open-browse-audio", label: "打开 Browse 音频筛选", primary: true },
      { action: "copy-summary", label: "复制 Audio 状态摘要" }
    ]
  };
}

function renderAudioHealthCard(label, value, detail, tone, action) {
  const tag = action ? "button" : "div";
  const typeAttribute = action ? ` type="button"` : "";
  const actionAttribute = action ? ` data-audio-action="${escapeHtml(action)}"` : "";
  return `
    <${tag}${typeAttribute} class="audio-health-card ${escapeHtml(tone)}"${actionAttribute}>
      <span>${escapeHtml(label)}</span>
      <strong>${escapeHtml(String(value))}</strong>
      <small>${escapeHtml(detail || "-")}</small>
    </${tag}>`;
}

function renderAudioBankList(model) {
  if (model.rows.length === 0) {
    return emptyBlock("当前资源聚合里没有 AudioCueOnly / FMOD event 资源。若 FMOD provider unavailable，请先导出 FMOD audio library snapshot。");
  }
  if (model.filteredRows.length === 0) {
    return emptyBlock("没有符合当前筛选的 FMOD event。");
  }

  const bankSections = model.bankGroups.map(group => `
    <article class="audio-bank-group">
      <div class="audio-bank-head">
        <div>
          <strong>${escapeHtml(group.bank)}</strong>
          <span>${escapeHtml(group.rows.length)} events</span>
        </div>
        <div class="audio-bank-metrics">
          ${smallBadge(`${group.rows.filter(row => row.is3D === true).length} 3D`, "info")}
          ${smallBadge(`${group.rows.filter(row => row.isLoop === true).length} Loop`, "warn")}
          ${renderAudioGroupDiagnosticBadge(group.rows)}
        </div>
      </div>
      <div class="audio-event-list">
        ${group.rows.map(row => renderAudioEventRow(row, model.selectedRow)).join("")}
      </div>
    </article>`).join("");
  const sourceSection = model.filteredSourceRows.length > 0 ? renderAudioSourceSampleGroup(model.filteredSourceRows, model.bankGroups.length === 0) : "";
  return bankSections + sourceSection;
}

function renderAudioEventRow(row, selectedRow) {
  const active = selectedRow && selectionMatchesItem(row.item, selectedRow.item.key);
  const spatialLabel = row.is3D === true ? "3D" : row.is3D === false ? "2D" : "空间未知";
  const loopLabel = row.isLoop === true ? "Loop" : row.isLoop === false ? "One-shot" : "Loop 未知";
  const packageState = getAudioPackageStatePresentation(row.packageState);
  const diagnosticSummary = summarizeAudioDiagnosticHealth(row.diagnostics);
  return `
    <button type="button" class="audio-event-row${active ? " active" : ""}" data-audio-resource-key="${escapeHtml(row.item.key)}">
      <span class="audio-event-main">
        <strong>${escapeHtml(row.semanticDisplayName || row.displayName || row.audioCueId || "未命名 Cue")}</strong>
        <small>${escapeHtml(row.audioCueId || row.audioEventDefinitionId || row.resourceKey || "-")}</small>
        <small>${escapeHtml(row.fmodEventPath || "未绑定 FMOD event")}</small>
      </span>
      <span class="audio-event-meta">
        ${smallBadge(row.banks.join(", ") || "-", "neutral")}
        ${smallBadge(spatialLabel, row.is3D === true ? "info" : "neutral")}
        ${smallBadge(loopLabel, row.isLoop === true ? "warn" : "ok")}
        ${row.isStream === true ? smallBadge("Stream", "info") : ""}
        ${smallBadge(packageState.label, packageState.tone)}
        ${renderAudioSemanticStatusBadge(row)}
        ${diagnosticSummary.errorCount > 0 ? smallBadge(`错误 ${diagnosticSummary.errorCount}`, "error") : diagnosticSummary.warningCount > 0 ? smallBadge(`警告 ${diagnosticSummary.warningCount}`, "warn") : ""}
      </span>
    </button>`;
}

function renderAudioSourceSampleGroup(rows, open) {
  return `
    <details class="audio-source-samples"${open ? " open" : ""}>
      <summary>
        <span>
          <strong>源文件样本</strong>
          <small>${escapeHtml(rows.length)} 个非 FMOD event 条目，供试听和替换来源对照。</small>
        </span>
      </summary>
      <div class="audio-source-list">
        ${rows.map(row => `
          <button type="button" class="audio-source-row" data-audio-resource-key="${escapeHtml(row.item.key)}">
            <span>
              <strong>${escapeHtml(row.displayName || row.resourceKey || "-")}</strong>
              <small>${escapeHtml(row.sourcePreviewPath || row.resourceKey || "-")}</small>
            </span>
            ${row.diagnostics.some(isAudioActionableDiagnostic) ? smallBadge(`问题 ${row.diagnostics.filter(isAudioActionableDiagnostic).length}`, "warn") : smallBadge("样本", "neutral")}
          </button>`).join("")}
      </div>
    </details>`;
}

function renderAudioCueList(model) {
  if (model.cueRows.length === 0) {
    return emptyBlock("当前没有 AudioCueOnly 或 audioCueId 资源。");
  }
  const expanded = state.audioCueDetailExpanded === true;
  const gaps = model.cueRows.filter(isAudioPackagingGap).length;
  if (!expanded) {
    return `
      <div class="audio-cue-foldout">
        <button type="button" class="audio-cue-toggle" data-audio-toggle="cue-detail" aria-expanded="false">
          <span>
            <strong>Cue 进包明细</strong>
            <small>${escapeHtml(model.cueRows.length)} cues，${escapeHtml(gaps)} 个当前 package 计划缺口；未使用 Cue 不计入。</small>
          </span>
          <span class="audio-cue-toggle-icon">展开</span>
        </button>
      </div>`;
  }
  return `
    <div class="audio-cue-foldout">
      <button type="button" class="audio-cue-toggle" data-audio-toggle="cue-detail" aria-expanded="true">
        <span>
          <strong>Cue 进包明细</strong>
          <small>${escapeHtml(model.cueRows.length)} cues，${escapeHtml(gaps)} 个需要处理。</small>
        </span>
        <span class="audio-cue-toggle-icon">收起</span>
      </button>
      <div class="audio-cue-table" role="table" aria-label="AudioCue manifest 状态">
        <div class="audio-cue-row head" role="row">
          <span>AudioCueOnly / audioCueId</span>
          <span>audioEventDefinitionId</span>
          <span>Audio plan</span>
          <span>audio_cue_manifest</span>
        </div>
        ${model.cueRows.map(row => {
          const active = model.selectedRow && selectionMatchesItem(row.item, model.selectedRow.item.key);
          const gap = isAudioPackagingGap(row);
          return `
            <button type="button" class="audio-cue-row${active ? " active" : ""}${gap ? " gap" : ""}" data-audio-resource-key="${escapeHtml(row.item.key)}" role="row">
              <span>
                <strong>${escapeHtml(row.audioCueId || row.displayName)}</strong>
                <small>${escapeHtml(row.runtimeAvailability || "AudioCueOnly")}${gap ? " · 需处理" : ""}</small>
              </span>
              <span>${escapeHtml(row.audioEventDefinitionId || "-")}</span>
              <span>${row.inAudioPlan ? smallBadge("已进入", "ok") : smallBadge(row.packageState === "not-targeted" ? "未使用" : "未进入", row.packageState === "not-targeted" ? "neutral" : "warn")}</span>
              <span>${row.inCueManifest ? smallBadge("已生成", "ok") : smallBadge(row.packageState === "not-targeted" ? "未使用" : "未生成", row.packageState === "not-targeted" ? "neutral" : "warn")}</span>
            </button>`;
        }).join("")}
      </div>
    </div>`;
}

function renderAudioImpactPanel(model) {
  const row = model.selectedRow;
  if (!row) {
    return emptyBlock("选择一个 FMOD event 或 AudioCue 后查看引用者、Refs 跳转、Profile / Build 状态、相关诊断和可复制 JSON。");
  }

  const detail = getCurrentDetail(row.item);
  const references = asArray(pick(detail, "references")).length > 0 ? asArray(pick(detail, "references")) : row.references;
  const diagnostics = mergeDiagnostics(row.diagnostics, asArray(pick(detail, "diagnostics")));
  const profileStatus = getBuildProfileStatus(row.item);
  const profileLabel = formatProfileStatusLabel(profileStatus);
  const profileTone = toneForBuildProfileStatus(profileStatus);
  const preview = getAudioPreviewInfo(row);
  const semanticDraft = getAudioSemanticDraft(row);
  const activeTab = getAudioInspectorTab();
  const eventOptions = model.eventRows.filter(candidate => candidate.fmodEventPath);
  const previewOptions = getAudioPreviewPathOptions(model.rows);
  return `
    <section class="audio-impact-card">
      <div class="audio-impact-head">
        <div>
          <h2>音频 Inspector ${model.selectedWasImplicit ? smallBadge("建议先看", "warn") : ""}</h2>
          ${renderAudioCodeLine(row.fmodEventPath || row.displayName)}
        </div>
      </div>
      ${renderAudioInspectorTabs(activeTab)}
      <div class="audio-authority-note">本页是只读音频总览。Gameplay Cue、Event Definition、试听证据和 Runtime Catalog 发布统一由 Audio Workbench 管理。</div>
      ${renderAudioInspectorContent(activeTab, { row, references, diagnostics, profileLabel, profileTone, preview, semanticDraft, eventOptions, previewOptions })}
    </section>`;
}
