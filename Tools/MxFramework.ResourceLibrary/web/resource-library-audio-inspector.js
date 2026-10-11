// Audio inspector, semantic drafts, and replacement validation.
function getAudioInspectorTab() {
  const value = state.audioInspectorTab || "overview";
  return AUDIO_INSPECTOR_TABS.some(([tab]) => tab === value) ? value : "overview";
}

function renderAudioInspectorTabs(activeTab) {
  return `
    <div class="audio-inspector-tabs" role="tablist" aria-label="音频 Inspector">
      ${AUDIO_INSPECTOR_TABS.map(([tab, label]) => `
        <button type="button" class="${tab === activeTab ? "active" : ""}" data-audio-inspector-tab="${escapeHtml(tab)}" role="tab" aria-selected="${tab === activeTab ? "true" : "false"}">${escapeHtml(label)}</button>
      `).join("")}
    </div>`;
}

function renderAudioInspectorContent(activeTab, context) {
  if (activeTab === "semantic") return renderAudioSemanticEditor(context.row, context.semanticDraft, context.previewOptions);
  if (activeTab === "replacement") return renderAudioReplacementEditor(context.row, context.semanticDraft, context.references, context.eventOptions);
  if (activeTab === "technical") return renderAudioTechnicalPanel(context.row, context.profileLabel, context.profileTone);
  return renderAudioOverviewPanel(context);
}

function renderAudioOverviewPanel(context) {
  const { row, references, diagnostics, profileLabel, preview } = context;
  const packageState = getAudioPackageStatePresentation(row.packageState);
  const diagnosticSummary = summarizeAudioDiagnosticHealth(diagnostics);
  return `
    <div class="audio-inspector-page">
      ${renderAudioPreviewPanel(row, preview)}
      <div class="summary-grid audio-summary-grid">
        ${metric("引用者", references.length)}
        ${metric("Build Profile", profileLabel)}
        ${metric("当前 package", packageState.label)}
        ${metric("校验问题", diagnosticSummary.actionableCount)}
      </div>
      <section class="audio-next-panel">
        <h3>下一步动作</h3>
        <div class="context-actions inline-actions">
          <button type="button" class="secondary" data-audio-action="open-references-current">检查引用</button>
          <button type="button" class="secondary" data-audio-action="open-profile">分配 Profile</button>
          <button type="button" class="secondary" data-audio-action="open-build">Build 诊断</button>
          <button type="button" class="secondary" data-audio-action="open-audio-workbench">在 Audio Workbench 编辑</button>
          <button type="button" class="secondary" data-audio-action="copy-selected-json">复制 JSON</button>
        </div>
      </section>
      <section class="audio-reference-block">
        <h3>引用者</h3>
        ${references.length === 0 ? emptyBlock("当前 referenceGraph 没有引用者。") : renderAudioReferenceList(references)}
      </section>
      <section class="audio-diagnostics-block">
        <h3>相关诊断</h3>
        ${renderDiagnosticsList(diagnostics)}
      </section>
    </div>`;
}

function renderAudioTechnicalPanel(row, profileLabel, profileTone) {
  return `
    <div class="audio-inspector-page">
      <section class="audio-technical-panel">
        <h3>技术字段</h3>
        <div class="audio-code-stack">
          ${renderAudioCodeField("fmodEventPath", row.fmodEventPath || "-")}
          ${renderAudioCodeField("fmodEventGuid", row.fmodEventGuid || "-")}
          ${renderAudioCodeField("audioCueId", row.audioCueId || "-")}
          ${renderAudioCodeField("audioEventDefinitionId", row.audioEventDefinitionId || "-")}
        </div>
        <div class="audio-tech-kv">
          ${renderKeyValueList([
            ["semantic status", row.semanticStatus || "-"],
            ["auditionStatus", row.auditionStatus || "-"],
            ["banks", row.banks.join(", ")],
            ["parameters", row.parameters.join(", ") || "-"],
            ["is3D", row.is3D === null ? "-" : row.is3D],
            ["isLoop", row.isLoop === null ? "-" : row.isLoop],
            ["lengthMs", Number.isFinite(row.lengthMs) ? row.lengthMs : "-"],
            ["maxDistance", Number.isFinite(row.maxDistance) ? row.maxDistance : "-"],
            ["Profile 状态", profileLabel],
            ["Profile tone", profileTone]
          ])}
        </div>
      </section>
      <section class="audio-technical-panel">
        <h3>Resource Plan / Manifest</h3>
        ${renderKeyValueList([
          ["Audio plan", row.inAudioPlan ? `${row.audioPlanEntries.length} 项` : "未进入"],
          ["audio_cue_manifest", row.inCueManifest ? "已生成" : "未生成"],
          ["manifest cueId", stringValue(pick(row.manifestEntry, "cueId")) || "-"],
          ["manifest bank", stringValue(pick(row.manifestEntry, "bank")) || "-"]
        ])}
      </section>
      <details class="audio-raw-json">
        <summary>Raw Audio JSON</summary>
        ${renderJsonBlock(buildSelectedAudioJson(row))}
      </details>
    </div>`;
}

function renderAudioSemanticEditor(row, draft, previewOptions) {
  return `
    <section class="audio-semantic-editor">
      <div class="audio-semantic-head">
        <div>
          <h3>语义清单 · 只读</h3>
          <p>${escapeHtml(getAudioSemanticManifestPath())}</p>
        </div>
        <div class="audio-semantic-state">
          ${renderAudioSemanticStatusBadge(row)}
          ${row.auditionStatus === "Approved" ? smallBadge("已试听批准", "ok") : smallBadge("需人工试听", "warn")}
        </div>
      </div>
      <div class="audio-semantic-grid">
        ${renderAudioSemanticReadonly("显示名", draft.displayName || row.displayName)}
        ${renderAudioSemanticReadonly("audioCueId", draft.audioCueId || row.audioCueId)}
        ${renderAudioSemanticReadonly("用途", draft.purpose || "-")}
        ${renderAudioSemanticReadonly("标签", asArray(draft.tags).join(", ") || "-")}
        ${renderAudioSemanticReadonly("源文件试听", draft.sourcePreviewPath || "-")}
        ${renderAudioSemanticReadonly("人工试听状态", draft.auditionStatus || row.auditionStatus || "Needed")}
        ${renderAudioSemanticReadonly("语境", draft.context || "-")}
      </div>
      <div class="audio-semantic-actions">
        <button type="button" class="audio-primary-action" data-audio-action="open-audio-workbench">在 Audio Workbench 编辑此 Cue</button>
      </div>
    </section>`;
}

function renderAudioReplacementEditor(row, draft, references, eventOptions) {
  const contract = draft.replacementContract || {};
  const packageState = getAudioPackageStatePresentation(row.packageState);
  return `
    <section class="audio-semantic-editor">
      <div class="audio-semantic-head">
        <div>
          <h3>替换契约 · 只读</h3>
          <p>稳定 audioCueId 不变；请在 Audio Workbench 绑定 Event Definition 并完成真实试听。</p>
        </div>
        <div class="audio-semantic-state">
          ${renderAudioSemanticStatusBadge(row)}
          ${smallBadge(packageState.label, packageState.tone)}
        </div>
      </div>
      <div class="audio-semantic-grid">
        ${renderAudioSemanticReadonly("audioCueId", draft.audioCueId || row.audioCueId)}
        ${renderAudioSemanticReadonly("目标 FMOD event path", draft.fmodEventPath || row.fmodEventPath)}
        ${renderAudioSemanticReadonly("目标 FMOD guid", draft.fmodEventGuid || row.fmodEventGuid)}
      </div>
      <div class="audio-replacement-grid">
        ${renderAudioSemanticReadonly("保持 2D/3D", contract.requireSameSpatial !== false ? "是" : "否")}
        ${renderAudioSemanticReadonly("保持 Loop", contract.requireSameLoop !== false ? "是" : "否")}
        ${renderAudioSemanticReadonly("长度范围", `${contract.minLengthMs || 0} - ${contract.maxLengthMs || 0} ms`)}
        ${renderAudioSemanticReadonly("必需参数", asArray(contract.requiredParameters).join(", ") || "-")}
        ${renderAudioSemanticReadonly("必需 Bank", asArray(contract.requiredBanks).join(", ") || "-")}
      </div>
      <div class="audio-semantic-actions">
        <button type="button" class="audio-primary-action" data-audio-action="open-audio-workbench">在 Audio Workbench 替换 Event</button>
      </div>
    </section>`;
}

function renderAudioSemanticInput(label, field, value, type, disabled = false) {
  return `
    <label class="audio-semantic-field${field === "context" ? " wide" : ""}">
      <span>${escapeHtml(label)}</span>
      <input type="${escapeHtml(type || "text")}" value="${escapeHtml(value ?? "")}" data-audio-semantic-field="${escapeHtml(field)}"${disabled ? " disabled" : ""}>
    </label>`;
}

function renderAudioSemanticSelect(label, field, value, options, disabled = false) {
  const values = uniqueStrings([value, ...asArray(options).map(option => typeof option === "string" ? option : option.value)].filter(Boolean));
  return `
    <label class="audio-semantic-field">
      <span>${escapeHtml(label)}</span>
      <select data-audio-semantic-field="${escapeHtml(field)}"${disabled ? " disabled" : ""}>
        <option value="">未设置</option>
        ${values.map(optionValue => {
          const option = asArray(options).find(candidate => (typeof candidate === "string" ? candidate : candidate.value) === optionValue);
          const labelText = option && typeof option === "object" ? option.label : optionValue;
          return `<option value="${escapeHtml(optionValue)}"${optionValue === value ? " selected" : ""}>${escapeHtml(labelText)}</option>`;
        }).join("")}
      </select>
    </label>`;
}

function renderAudioReplacementEventPicker(draft, eventOptions, disabled) {
  const selectedPath = stringValue(draft.fmodEventPath);
  const options = asArray(eventOptions).filter(row => row.fmodEventPath);
  return `
    <label class="audio-semantic-field wide">
      <span>目标 FMOD event</span>
      <select data-audio-replacement-target${disabled ? " disabled" : ""}>
        <option value="">选择当前 FMOD snapshot 中的事件</option>
        ${options.map(row => {
          const spatial = row.is3D === true ? "3D" : row.is3D === false ? "2D" : "空间未知";
          const loop = row.isLoop === true ? "Loop" : "One-shot";
          const label = `${row.semanticDisplayName || row.displayName || row.fmodEventPath} · ${spatial} · ${loop}`;
          return `<option value="${escapeHtml(row.fmodEventPath)}"${row.fmodEventPath === selectedPath ? " selected" : ""}>${escapeHtml(label)}</option>`;
        }).join("")}
      </select>
    </label>`;
}

function renderAudioSemanticReadonly(label, value) {
  return `
    <label class="audio-semantic-field readonly">
      <span>${escapeHtml(label)}</span>
      ${renderAudioCodeLine(value || "-")}
    </label>`;
}

function renderAudioSemanticCheckbox(label, field, checked, disabled = false) {
  return `
    <label class="audio-semantic-check">
      <input type="checkbox" data-audio-semantic-field="${escapeHtml(field)}"${checked ? " checked" : ""}${disabled ? " disabled" : ""}>
      <span>${escapeHtml(label)}</span>
    </label>`;
}

function renderAudioSemanticStatusBadge(row) {
  const presentation = AUDIO_SEMANTIC_STATUS_LABELS[row.semanticStatus];
  if (presentation) return smallBadge(presentation[0], presentation[1]);
  if (row.semanticStatus === "Error") return smallBadge("语义 API 错误", "error");
  if (row.semanticStatus === "Unavailable") return smallBadge("语义 API 不可用", "warn");
  return smallBadge(row.semanticStatus || "语义未知", "neutral");
}

function renderAudioSemanticUnavailable(apiState) {
  const detail = apiState === "error"
    ? state.audioSemanticApiError || "Audio semantic manifest API 请求失败。"
    : "Authoring server 版本过旧或语义 API 不可用；请换到包含 audio-semantics endpoint 的当前 Authoring server。";
  return `
    <div class="audio-api-warning">
      <strong>${apiState === "error" ? "语义 API 请求失败" : "语义 API 不可用"}</strong>
      <span>${escapeHtml(detail)}</span>
    </div>`;
}

function renderAudioCodeLine(value) {
  return `<code class="audio-code-line" title="${escapeHtml(value || "-")}">${escapeHtml(value || "-")}</code>`;
}

function renderAudioCodeField(label, value) {
  const text = stringValue(value || "-");
  return `
    <div class="audio-code-field">
      <span>${escapeHtml(label)}</span>
      <div class="audio-code-row">
        ${renderAudioCodeLine(text)}
        <button type="button" class="secondary mini" data-audio-copy-text="${escapeHtml(text)}">复制</button>
      </div>
    </div>`;
}

function renderAudioSemanticSaveState(row) {
  const saveState = state.audioSemanticSaveState || {};
  if (saveState.audioCueId !== row.audioCueId || (!saveState.message && !saveState.error)) return "";
  const tone = saveState.status === "error" ? "error" : saveState.status === "saved" ? "ok" : "info";
  return `<p class="audio-save-state ${tone}">${escapeHtml(saveState.error || saveState.message)}</p>`;
}

function renderAudioPreviewPanel(row, preview) {
  const isCurrent = audioPreviewState.resourceKey === row.item.key;
  const playing = isCurrent && audioPreviewState.status === "playing";
  const status = isCurrent && audioPreviewState.message ? audioPreviewState.message : preview.message;
  return `
    <section class="audio-preview-panel ${preview.url ? "ready" : "blocked"}">
      <div class="audio-preview-main">
        <div>
          <h3>试听</h3>
          <p>${escapeHtml(status)}</p>
          ${preview.url ? `<small>${escapeHtml(preview.path)}</small>` : ""}
        </div>
        <div class="audio-preview-actions">
          <button type="button" class="audio-primary-action" data-audio-action="play-preview"${preview.url ? "" : " disabled"}>${playing ? "重新播放" : "试听源文件"}</button>
          <button type="button" class="secondary" data-audio-action="stop-preview"${playing ? "" : " disabled"}>停止</button>
        </div>
      </div>
      <div class="audio-preview-note">Web 试听播放的是可定位的源音频文件；FMOD event 的参数、随机、多轨、3D 衰减和 bank runtime 行为仍以 Unity / FMOD 预览为准。</div>
    </section>`;
}

function renderAudioReferenceList(references) {
  return `<div class="audio-reference-list">${references.slice(0, 12).map(reference => `
    <article class="audio-reference-row">
      <strong>${escapeHtml(pick(reference, "sourceConfigKind", "sourceConsumerKind") || "reference")}</strong>
      <span>${escapeHtml(pick(reference, "sourceField") || pick(reference, "sourceStableId") || "-")}</span>
      <small>${escapeHtml(pick(reference, "preloadPolicy") || (pick(reference, "isRequiredAtRuntime") ? "required" : "optional"))}</small>
    </article>`).join("")}${references.length > 12 ? `<p class="audio-list-more">另有 ${references.length - 12} 个引用，打开 Refs 查看完整列表。</p>` : ""}</div>`;
}

function filterAudioRows(rows, provider) {
  const filters = state.audioFilters || {};
  const search = stringValue(filters.search).trim().toLowerCase();
  return rows.filter(row => {
    if (filters.bank && filters.bank !== "all" && !row.banks.includes(filters.bank)) return false;
    if (filters.spatial === "3d" && row.is3D !== true) return false;
    if (filters.spatial === "2d" && row.is3D !== false) return false;
    if (filters.loop === "loop" && row.isLoop !== true) return false;
    if (filters.loop === "oneShot" && row.isLoop !== false) return false;
    if (filters.loop === "stream" && row.isStream !== true) return false;
    if (filters.diagnostics === "issues" && !row.diagnostics.some(isAudioActionableDiagnostic)) return false;
    if (filters.diagnostics === "clean" && row.diagnostics.some(isAudioActionableDiagnostic)) return false;
    if (filters.diagnostics === "stale" && !(provider.stale || provider.stateKey === "unavailable" || row.cacheStale)) return false;
    if (filters.referenced === "referenced" && row.references.length === 0) return false;
    if (filters.referenced === "unreferenced" && row.references.length > 0) return false;
    if (filters.referenced === "audioPlan" && !(row.inAudioPlan || row.inCueManifest)) return false;
    if (filters.referenced === "notAudioPlan" && !isAudioPackagingGap(row)) return false;
    if (!search) return true;
    return [
      row.displayName,
      row.fmodEventPath,
      row.fmodEventGuid,
      row.audioCueId,
      row.audioEventDefinitionId,
      row.semanticPurpose,
      row.semanticContext,
      row.semanticTags.join(" "),
      row.auditionStatus,
      row.resourceKey,
      row.runtimeAvailability,
      row.banks.join(" "),
      row.parameters.join(" ")
    ].join(" ").toLowerCase().includes(search);
  });
}

function isAudioPackagingGap(row) {
  return row && (row.packageState === "referenced-missing-plan" || row.packageState === "manifest-stale");
}

function getAudioPackageState(references, audioPlanEntries, manifestEntry) {
  const referenced = asArray(references).length > 0;
  const inAudioPlan = asArray(audioPlanEntries).length > 0;
  const inCueManifest = Boolean(manifestEntry);
  if (inAudioPlan !== inCueManifest) return "manifest-stale";
  if (inAudioPlan && inCueManifest) return referenced ? "referenced-ready" : "packaged-unreferenced";
  if (referenced) return "referenced-missing-plan";
  return "not-targeted";
}

function getAudioPackageStatePresentation(value) {
  if (value === "referenced-ready") return { label: "当前包已就绪", tone: "ok" };
  if (value === "packaged-unreferenced") return { label: "已进包未引用", tone: "info" };
  if (value === "referenced-missing-plan") return { label: "引用缺计划", tone: "warn" };
  if (value === "manifest-stale") return { label: "Plan / Manifest 不一致", tone: "warn" };
  return { label: "当前包未使用", tone: "neutral" };
}

function getAudioPreviewInfo(row) {
  const candidates = getAudioPreviewCandidatePaths(row);
  const path = candidates[0] || "";
  if (path) {
    return {
      path,
      url: toAudioPreviewUrl(path),
      message: row.fmodEventPath ? "可试听一个源文件样本；不是完整 FMOD event runtime 播放。" : "可直接试听源音频文件。"
    };
  }
  if (row.fmodEventPath || row.fmodEventGuid) {
    return {
      path: "",
      url: "",
      message: "这个条目只有 FMOD event/bank 信息；需要 Unity / FMOD Preview Bridge 才能真实播放。"
    };
  }
  return {
    path: "",
    url: "",
    message: "没有定位到可由浏览器直接播放的 .wav / .ogg / .mp3 源文件。"
  };
}

function getAudioPreviewCandidatePaths(row) {
  const candidates = [];
  if (isAudioPreviewPath(row.sourcePreviewPath)) candidates.push(row.sourcePreviewPath);
  for (const object of getAudioDataObjects(row.item)) {
    for (const key of ["previewUrl", "audioPreviewUrl", "sourceUrl", "unityAssetPath", "externalSourcePath", "relativePath", "path"]) {
      const value = stringValue(pick(object, key));
      if (isAudioPreviewPath(value)) candidates.push(value);
    }
  }

  const names = [
    row.displayName,
    row.resourceKey,
    row.audioCueId,
    row.audioEventDefinitionId,
    row.fmodEventPath
  ].filter(Boolean);
  const magicName = names.map(getMagicEffectAudioName).find(Boolean);
  if (magicName) {
    for (const extension of AUDIO_PREVIEW_EXTENSIONS) {
      candidates.push(`Assets/Audio/MxFramework/Samples/MagicEffects/${magicName}.${extension}`);
    }
  }

  const physicalCandidate = getPhysicalAudioPreviewCandidate(row);
  if (physicalCandidate) candidates.push(physicalCandidate);
  if (/event:\/MxFramework\/Demo\//i.test(row.fmodEventPath)) {
    candidates.push("FMOD/MxFrameworkAudioDemo/Audio/mxframework_demo_beep.wav");
    candidates.push("FMOD/MxFrameworkAudioDemo/MxFrameworkAudioDemo/Assets/mxframework_demo_beep.wav");
  }

  return uniqueStrings(candidates)
    .map(path => path.replace(/^\/+/, ""))
    .filter(isAudioPreviewPath);
}

function getAudioPreviewPathOptions(rows) {
  return uniqueStrings(asArray(rows).flatMap(row => getAudioPreviewCandidatePaths(row)))
    .sort(compareText)
    .map(path => ({ value: path, label: path }));
}

function getMagicEffectAudioName(value) {
  const text = stringValue(value).toLowerCase();
  const match = text.match(/audio\.magic_effect\.([a-z0-9_]+)/);
  return match ? match[1] : "";
}

function getPhysicalAudioPreviewCandidate(row) {
  const eventName = stringValue(row.fmodEventPath).split("/").pop() || "";
  const snake = camelToSnake(eventName);
  if (!snake) return "";
  if (snake.startsWith("sole_contact_")) {
    return `Assets/Audio/MxFramework/CharacterTest/PhysicalAudio/SoleContacts/${snake}_01.wav`;
  }
  if (snake.startsWith("object_collision_")) {
    return `Assets/Audio/MxFramework/CharacterTest/PhysicalAudio/ObjectCollisions/${snake}_01.wav`;
  }
  return "";
}

function camelToSnake(value) {
  return stringValue(value)
    .replace(/([a-z0-9])([A-Z])/g, "$1_$2")
    .replace(/[\s.-]+/g, "_")
    .toLowerCase();
}

function isAudioPreviewPath(value) {
  const text = stringValue(value).trim();
  return /\.(wav|ogg|mp3)(\?|#|$)/i.test(text);
}

function toAudioPreviewUrl(path) {
  if (/^https?:\/\//i.test(path) || path.startsWith("/")) return path;
  return "/" + path.split("/").map(part => encodeURIComponent(part)).join("/");
}

function getAudioSemanticApiState() {
  const value = state.audioSemanticApiState || (state.audioSemanticPayload ? "ready" : "unavailable");
  return value === "ready" || value === "unavailable" || value === "error" ? value : "unavailable";
}

function getAudioSemanticApiLabel(value = getAudioSemanticApiState()) {
  if (value === "ready") return "语义已连接";
  if (value === "error") return "语义 API 错误";
  return "语义 API 不可用";
}

function getAudioSemanticApiDetail() {
  const stateKey = getAudioSemanticApiState();
  if (stateKey === "ready") return "manifest 已读取";
  if (stateKey === "error") return state.audioSemanticApiError || "请求失败";
  return "旧 Authoring server 或 endpoint 缺失";
}

function getAudioSemanticManifestPath() {
  return stringValue(pick(state.audioSemanticPayload, "manifestPath")) || "Assets/Config/MxFramework/Audio/audio_semantic_manifest.json";
}

function getAudioSemanticManifest() {
  return pick(state.audioSemanticPayload, "manifest") || { schemaVersion: 1, manifestId: "mxframework.audio.semantic", entries: [] };
}

function getAudioSemanticEntries() {
  const manifest = getAudioSemanticManifest();
  const legacyEntries = asArray(pick(manifest, "entries"));
  if (legacyEntries.length > 0) return legacyEntries;
  const events = new Map(asArray(pick(manifest, "eventDefinitions")).map(item => [stringValue(pick(item, "id")), item]));
  return asArray(pick(manifest, "cues")).map(cue => {
    const eventDefinitionId = stringValue(pick(cue, "eventDefinitionId"));
    const eventDefinition = events.get(eventDefinitionId) || {};
    return {
      audioCueId: stringValue(pick(cue, "audioCueId")),
      audioEventDefinitionId: eventDefinitionId,
      fmodEventPath: stringValue(pick(eventDefinition, "fmodEventPath")),
      fmodEventGuid: stringValue(pick(eventDefinition, "fmodEventGuid")),
      displayName: stringValue(pick(cue, "displayName")),
      purpose: stringValue(pick(cue, "purpose")),
      context: stringValue(pick(cue, "context")),
      tags: asArray(pick(cue, "tags")),
      sourcePreviewPath: stringValue(pick(cue, "sourcePreviewPath")),
      auditionStatus: stringValue(pick(pick(cue, "auditionEvidence"), "status")) || "Needed",
      replacementContract: pick(cue, "replacementContract") || {},
      metadata: pick(cue, "metadata") || {}
    };
  });
}

function findAudioSemanticEntry(audioCueId, fmodEventPath, fmodEventGuid) {
  return getAudioSemanticEntries().find(entry => {
    if (!entry) return false;
    if (audioCueId && stringValue(pick(entry, "audioCueId")) === audioCueId) return true;
    if (fmodEventPath && stringValue(pick(entry, "fmodEventPath")) === fmodEventPath) return true;
    if (fmodEventGuid && stringValue(pick(entry, "fmodEventGuid")).toLowerCase() === fmodEventGuid.toLowerCase()) return true;
    return false;
  }) || null;
}

function buildAudioReplacementContract(item, semanticEntry) {
  const contract = pick(semanticEntry, "replacementContract") || {};
  return {
    requireSameSpatial: parseAudioBoolean(firstString(pick(contract, "requireSameSpatial"), getAudioFirstValue(item, "replacementRequireSameSpatial"))) !== false,
    requireSameLoop: parseAudioBoolean(firstString(pick(contract, "requireSameLoop"), getAudioFirstValue(item, "replacementRequireSameLoop"))) !== false,
    minLengthMs: numericValue(firstString(pick(contract, "minLengthMs"), getAudioFirstValue(item, "replacementMinLengthMs")), 0),
    maxLengthMs: numericValue(firstString(pick(contract, "maxLengthMs"), getAudioFirstValue(item, "replacementMaxLengthMs")), 0),
    requiredParameters: parseAudioStringList(firstString(pick(contract, "requiredParameters"), getAudioFirstValue(item, "replacementRequiredParameters"))),
    requiredBanks: parseAudioStringList(firstString(pick(contract, "requiredBanks"), getAudioFirstValue(item, "replacementRequiredBanks")))
  };
}

function getAudioSemanticDraft(row) {
  const key = row.audioCueId || row.item.key;
  if (!state.audioSemanticDrafts) state.audioSemanticDrafts = {};
  if (!state.audioSemanticDrafts[key]) {
    state.audioSemanticDrafts[key] = createAudioSemanticDraft(row);
  }
  return state.audioSemanticDrafts[key];
}

function createAudioSemanticDraft(row) {
  const entry = row.semanticEntry || findAudioSemanticEntry(row.audioCueId, row.fmodEventPath, row.fmodEventGuid) || {};
  const contract = pick(entry, "replacementContract") || row.replacementContract || {};
  return {
    audioCueId: firstString(pick(entry, "audioCueId"), row.audioCueId),
    audioEventDefinitionId: firstString(pick(entry, "audioEventDefinitionId"), row.audioEventDefinitionId),
    fmodEventPath: firstString(pick(entry, "fmodEventPath"), row.fmodEventPath),
    fmodEventGuid: firstString(pick(entry, "fmodEventGuid"), row.fmodEventGuid),
    displayName: firstString(pick(entry, "displayName"), row.semanticDisplayName, row.displayName),
    purpose: firstString(pick(entry, "purpose"), row.semanticPurpose),
    context: firstString(pick(entry, "context"), row.semanticContext),
    tags: parseAudioStringList(pick(entry, "tags")).length > 0 ? parseAudioStringList(pick(entry, "tags")) : row.semanticTags,
    sourcePreviewPath: firstString(pick(entry, "sourcePreviewPath"), row.sourcePreviewPath),
    auditionStatus: firstString(pick(entry, "auditionStatus"), row.auditionStatus, "Needed"),
    replacementContract: {
      requireSameSpatial: parseAudioBoolean(pick(contract, "requireSameSpatial")) !== false,
      requireSameLoop: parseAudioBoolean(pick(contract, "requireSameLoop")) !== false,
      minLengthMs: numericValue(pick(contract, "minLengthMs"), 0),
      maxLengthMs: numericValue(pick(contract, "maxLengthMs"), 0),
      requiredParameters: parseAudioStringList(pick(contract, "requiredParameters")),
      requiredBanks: parseAudioStringList(pick(contract, "requiredBanks"))
    },
    metadata: pick(entry, "metadata") || {}
  };
}

function updateAudioSemanticDraftFromControl(control) {
  const model = createAudioWorkspaceModel();
  const row = model.selectedRow;
  if (!row) return;

  const draft = getAudioSemanticDraft(row);
  const field = control.dataset.audioSemanticField || "";
  let value = control.type === "checkbox" ? control.checked : control.value;
  if (control.type === "number") value = numericValue(value, 0);
  setAudioSemanticDraftValue(draft, field, value);
  state.audioSemanticSaveState = { status: "dirty", audioCueId: row.audioCueId, error: "", message: "语义草稿有未保存修改。" };
  if (state.audioReplacementValidation?.audioCueId === row.audioCueId) {
    state.audioReplacementValidation = { status: "idle", audioCueId: row.audioCueId, payload: null, error: "" };
  }
}

function setAudioSemanticDraftValue(draft, field, value) {
  if (!draft || !field) return;
  if (field === "tags") {
    draft.tags = parseAudioStringList(value);
    return;
  }
  if (field.startsWith("replacementContract.")) {
    const key = field.slice("replacementContract.".length);
    if (!draft.replacementContract) draft.replacementContract = {};
    if (key === "requiredParameters" || key === "requiredBanks") {
      draft.replacementContract[key] = parseAudioStringList(value);
    } else if (key === "minLengthMs" || key === "maxLengthMs") {
      draft.replacementContract[key] = numericValue(value, 0);
    } else {
      draft.replacementContract[key] = value;
    }
    return;
  }
  draft[field] = value;
}

function updateAudioReplacementTarget(control) {
  const model = createAudioWorkspaceModel();
  const row = model.selectedRow;
  if (!row) return;
  const target = model.eventRows.find(candidate => candidate.fmodEventPath === control.value);
  if (!target) return;

  const draft = getAudioSemanticDraft(row);
  draft.fmodEventPath = target.fmodEventPath;
  draft.fmodEventGuid = target.fmodEventGuid;
  state.audioSemanticSaveState = { status: "dirty", audioCueId: row.audioCueId, error: "", message: "替换目标已更新；请先验证替换，再保存语义。" };
  state.audioReplacementValidation = { status: "idle", audioCueId: row.audioCueId, payload: null, error: "" };
  renderAudioWorkspace();
}
function buildAudioSemanticManifestWithDraft(row) {
  const manifest = structuredCloneCompat(getAudioSemanticManifest());
  if (!manifest.entries) manifest.entries = [];
  const draft = normalizeAudioSemanticDraft(getAudioSemanticDraft(row), row);
  const index = manifest.entries.findIndex(entry => entry && stringValue(pick(entry, "audioCueId")) === draft.audioCueId);
  if (index >= 0) manifest.entries[index] = draft;
  else manifest.entries.push(draft);
  return manifest;
}

function normalizeAudioSemanticDraft(draft, row) {
  const normalized = structuredCloneCompat(draft || {});
  normalized.audioCueId = firstString(normalized.audioCueId, row.audioCueId);
  normalized.audioEventDefinitionId = firstString(normalized.audioEventDefinitionId, row.audioEventDefinitionId);
  normalized.fmodEventPath = firstString(normalized.fmodEventPath, row.fmodEventPath);
  normalized.fmodEventGuid = firstString(normalized.fmodEventGuid, row.fmodEventGuid);
  normalized.displayName = stringValue(normalized.displayName).trim();
  normalized.purpose = stringValue(normalized.purpose).trim();
  normalized.context = stringValue(normalized.context).trim();
  normalized.tags = parseAudioStringList(normalized.tags);
  normalized.sourcePreviewPath = stringValue(normalized.sourcePreviewPath).trim();
  normalized.auditionStatus = firstString(normalized.auditionStatus, "Needed");
  normalized.replacementContract = normalized.replacementContract || {};
  normalized.replacementContract.requireSameSpatial = normalized.replacementContract.requireSameSpatial !== false;
  normalized.replacementContract.requireSameLoop = normalized.replacementContract.requireSameLoop !== false;
  normalized.replacementContract.minLengthMs = numericValue(normalized.replacementContract.minLengthMs, 0);
  normalized.replacementContract.maxLengthMs = numericValue(normalized.replacementContract.maxLengthMs, 0);
  normalized.replacementContract.requiredParameters = parseAudioStringList(normalized.replacementContract.requiredParameters);
  normalized.replacementContract.requiredBanks = parseAudioStringList(normalized.replacementContract.requiredBanks);
  normalized.metadata = normalized.metadata || {};
  return normalized;
}

async function saveSelectedAudioSemanticDraft() {
  const model = createAudioWorkspaceModel();
  const row = model.selectedRow;
  if (!row) return;
  if (getAudioSemanticApiState() !== "ready") {
    state.audioSemanticSaveState = {
      status: "error",
      audioCueId: row.audioCueId,
      error: "语义 API 不可用，无法保存 audio_semantic_manifest.json。",
      message: ""
    };
    renderAudioWorkspace();
    return;
  }

  const draft = getAudioSemanticDraft(row);
  if (hasAudioReplacementDraft(row, draft) && !isAudioReplacementValidated(row, draft)) {
    state.audioSemanticSaveState = {
      status: "error",
      audioCueId: row.audioCueId,
      error: "FMOD event 绑定已变化；请先在“替换”页验证通过后再保存。",
      message: ""
    };
    state.audioInspectorTab = "replacement";
    renderAudioWorkspace();
    return;
  }

  state.audioSemanticSaveState = { status: "saving", audioCueId: row.audioCueId, error: "", message: "正在保存音频语义清单..." };
  renderAudioWorkspace();
  try {
    const manifest = buildAudioSemanticManifestWithDraft(row);
    const expectedHash = stringValue(pick(state.audioSemanticPayload, "contentHash"));
    const payload = await postJson(API.saveAudioSemantics, {
      package: state.packageRelative,
      expectedHash,
      manifest
    });
    await loadPackageData();
    state.audioSemanticSaveState = {
      status: "saved",
      audioCueId: row.audioCueId,
      error: "",
      message: stringValue(pick(payload, "message")) || "音频语义清单已保存。"
    };
    render();
  } catch (error) {
    state.audioSemanticSaveState = {
      status: "error",
      audioCueId: row.audioCueId,
      error: error.message || "音频语义保存失败。",
      message: ""
    };
    if (error.data) {
      state.audioSemanticPayload = {
        ...(state.audioSemanticPayload || {}),
        validation: pick(error.data, "validation") || pick(state.audioSemanticPayload, "validation"),
        contentHash: pick(error.data, "currentHash") || pick(state.audioSemanticPayload, "contentHash")
      };
    }
    renderAudioWorkspace();
  }
}

function hasAudioReplacementDraft(row, draft) {
  const original = row.semanticEntry || {};
  return stringValue(pick(original, "fmodEventPath")) !== stringValue(pick(draft, "fmodEventPath")) ||
    stringValue(pick(original, "fmodEventGuid")).toLowerCase() !== stringValue(pick(draft, "fmodEventGuid")).toLowerCase();
}

function isAudioReplacementValidated(row, draft) {
  const validation = state.audioReplacementValidation || {};
  const payload = validation.audioCueId === row.audioCueId ? validation.payload : null;
  return validation.status === "done" &&
    pick(payload, "accepted") === true &&
    stringValue(pick(payload, "targetFmodEventPath")) === stringValue(pick(draft, "fmodEventPath"));
}

async function validateSelectedAudioReplacement() {
  const model = createAudioWorkspaceModel();
  const row = model.selectedRow;
  if (!row) return;
  if (getAudioSemanticApiState() !== "ready") {
    state.audioReplacementValidation = {
      status: "error",
      audioCueId: row.audioCueId,
      payload: null,
      error: "语义 API 不可用，无法验证替换草稿。"
    };
    renderAudioWorkspace();
    return;
  }

  const draftEntry = normalizeAudioSemanticDraft(getAudioSemanticDraft(row), row);
  state.audioReplacementValidation = { status: "validating", audioCueId: row.audioCueId, payload: null, error: "" };
  renderAudioWorkspace();
  try {
    const payload = await postJson(API.validateAudioReplacement, {
      package: state.packageRelative,
      audioCueId: row.audioCueId,
      targetFmodEventPath: draftEntry.fmodEventPath,
      targetFmodEventGuid: draftEntry.fmodEventGuid,
      draftEntry
    });
    state.audioReplacementValidation = { status: "done", audioCueId: row.audioCueId, payload, error: "" };
  } catch (error) {
    state.audioReplacementValidation = {
      status: "error",
      audioCueId: row.audioCueId,
      payload: error.data || null,
      error: error.message || "替换验证失败。"
    };
  }
  renderAudioWorkspace();
}

function resetSelectedAudioSemanticDraft() {
  const model = createAudioWorkspaceModel();
  const row = model.selectedRow;
  if (!row) return;

  const key = row.audioCueId || row.item.key;
  if (state.audioSemanticDrafts) delete state.audioSemanticDrafts[key];
  state.audioSemanticSaveState = { status: "idle", audioCueId: row.audioCueId, error: "", message: "" };
  state.audioReplacementValidation = { status: "idle", audioCueId: row.audioCueId, payload: null, error: "" };
  renderAudioWorkspace();
}
