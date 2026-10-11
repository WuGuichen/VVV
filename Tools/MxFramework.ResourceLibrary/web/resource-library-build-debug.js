// Resource Library split module.
// Build readiness, build diagnostics, inspector, and Debug workspace.
// Loaded by index.html as a classic script; keep shared bindings global.
function renderPlan() {
  const plan = getPlanDocument();
  const status = getPlanStatus();
  if (!state.resourcePlanPayload) {
    el.planSummary.textContent = "没有读取到 resource plan API 数据。";
    el.planGrid.innerHTML = emptyBlock("未生成运行时资源计划");
    return;
  }

  const groups = getPlanGroups();
  const totalResources = groups.reduce((sum, group) => sum + group.resources.length, 0);
  el.planSummary.textContent = `状态 ${status}，${groups.length} 个分组，${totalResources} 个资源引用`;

  el.planGrid.innerHTML = groups.map(group => `
    <article class="plan-group">
      <div class="plan-group-head">
        <strong>${escapeHtml(group.label)}</strong>
        <span>${group.resources.length}</span>
      </div>
      <p>${escapeHtml(group.required)} / ${escapeHtml(group.failurePolicy || "-")}</p>
      <ul>
        ${group.resources.slice(0, 6).map(resource => `<li>${escapeHtml(resource.resourceKey || resource.id || resource.cue || resource.bank || JSON.stringify(resource))}</li>`).join("")}
      </ul>
      ${group.resources.length > 6 ? `<div class="more">另有 ${group.resources.length - 6} 项</div>` : ""}
    </article>`).join("") || emptyBlock(plan ? "resource plan 没有资源分组" : "未生成 resource plan") ;
}

function renderBuildWorkspace() {
  renderWorkflowAssistant(el.buildWorkflowAssistant, getBuildGuideState());
  const plan = getBundlePlan();
  const bundles = asArray(pick(plan, "bundles"));
  const status = getBuildStatus();
  const diagnostics = getBuildDiagnostics();
  const readiness = getAuthorBuildReadiness();
  const counts = pick(plan, "counts") || {};

  el.bundlePlanSummary.textContent = `${readiness.label} · ${readiness.nextActionLabel}`;
  el.buildChecklist.innerHTML = "";
  el.bundlePlanContent.innerHTML = `
    ${renderResourceMappingStatus()}
    ${renderBuildReadinessPanel(readiness, status)}
    <section class="build-subsection build-checklist-inline">
      <h3>构建检查</h3>
      ${renderBuildChecklist()}
    </section>
    <div class="summary-grid">
      ${metric("内部 Bundle", bundles.length)}
      ${metric("External 引用", numericValue(pick(counts, "external", "External")))}
      ${metric("Excluded", numericValue(pick(counts, "excluded", "Excluded")))}
      ${metric("诊断", diagnostics.length)}
    </div>
    ${renderBuildArtifactReport(status)}
    ${renderBuildStatusBundleReport(status, bundles)}
    <section class="build-subsection">
      <h3>Bundle Plan 预览</h3>
      ${renderBundlePlanSummary(bundles)}
    </section>
    ${renderBundlePlanExternalSummary(plan)}`;
  el.buildDiagnostics.innerHTML = `
    <div class="detail-card">
      <h3>构建诊断</h3>
      ${hasReferenceBundleRecommendationIssues() ? `
        <div class="build-reference-callout">
          <div>
            <strong>引用关系可能影响 Bundle 分配</strong>
            <p>有 required 引用缺 Profile、缺 Bundle，或同源资源分散在多个 Bundle。</p>
          </div>
          <button type="button" data-action="open-references-issues" class="primary-action">去 Refs 查看建议</button>
        </div>` : ""}
      ${renderBuildDiagnosticsForAuthors(diagnostics)}
    </div>
    <p class="profile-hint">${escapeHtml(UNITY_WORKBENCH_GUIDANCE)}</p>`;
}

function renderResourceMappingStatus() {
  const payload = state.resourceMappingPayload || {};
  const plan = pick(payload, "plan") || {};
  const counts = pick(plan, "counts") || {};
  const diagnostics = asArray(pick(payload, "diagnostics"));
  const success = Boolean(pick(payload, "success"));
  return `
    <section class="build-subsection resource-mapping-panel ${success ? "ok" : "warn"}">
      <div class="bundle-status-head">
        <div>
          <span class="status-pill ${success ? "ok" : "warn"}">生产资源映射</span>
          <h3>资源映射编译状态</h3>
        </div>
        <strong>${success ? "编译通过" : "等待生产者数据"}</strong>
      </div>
      <p>Resource Manager 与 Unity Builder 共享同一个编译计划；Player 产物由 Unity 构建入口生成。</p>
      <div class="summary-grid">
        ${metric("Internal", numericValue(pick(counts, "internal")))}
        ${metric("External", numericValue(pick(counts, "external")))}
        ${metric("诊断", diagnostics.length)}
        ${metric("Compiler", pick(payload, "compilerContractVersion") || "-")}
      </div>
      <dl class="detail-list compact-list">
        <div><dt>Profile</dt><dd>${escapeHtml(pick(payload, "profilePath") || "-")}</dd></div>
        <div><dt>mappingInputFingerprint</dt><dd><code>${escapeHtml(pick(payload, "mappingInputFingerprint") || "-")}</code></dd></div>
        <div><dt>buildPlanFingerprint</dt><dd><code>${escapeHtml(pick(payload, "buildPlanFingerprint") || "-")}</code></dd></div>
      </dl>
      ${diagnostics.length > 0 ? `<details><summary>${diagnostics.length} 条资源映射诊断</summary>${renderDiagnosticsList(diagnostics)}</details>` : ""}
    </section>`;
}

function hasReferenceBundleRecommendationIssues() {
  return getBundleRecommendationRows(getBrowserItems())
    .some(row => row.severity === "blocking" || row.severity === "suggested");
}

function renderBuildChecklist() {
  const status = getBuildStatus();
  const readiness = getAuthorBuildReadiness();
  const summary = pick(status, "summary") || {};
  const artifactStatuses = asArray(pick(status, "artifacts"));
  const requiredArtifactsOk = getRequiredBuildArtifactsOk(status);
  const items = [
    { label: "Profile 已保存", ok: !state.buildProfileDirty && Boolean(getBuildProfile()) },
    { label: "Bundle Plan 可构建", ok: Boolean(getBundlePlan()) && !Boolean(pick(status, "hasErrors")) && readiness.state !== "fixErrors" },
    { label: "生成产物存在", ok: requiredArtifactsOk, hint: artifactStatuses.length > 0 ? "来自 StreamingAssets / build report 检查" : "等待 Authoring 状态 API" },
    { label: "产物未过期", ok: !Boolean(pick(status, "stale")) && readiness.state !== "possiblyStale" },
    { label: "External 已降噪", ok: numericValue(pick(summary, "externalConfigurationWarnings")) >= 0, hint: `${numericValue(pick(summary, "externalConfigurationWarnings"))} 个 external 配置引用默认折叠` }
  ];
  return `<ul class="build-checklist-list">${items.map(item => `
    <li class="${item.ok ? "ok" : "pending"}">
      <span>${item.ok ? "✓" : "○"}</span>
      <div>
        <strong>${escapeHtml(item.label)}</strong>
        ${item.hint ? `<small>${escapeHtml(item.hint)}</small>` : ""}
      </div>
    </li>`).join("")}</ul>`;
}

function getRequiredBuildArtifactsOk(status = getBuildStatus()) {
  const artifactStatuses = asArray(pick(status, "artifacts"));
  return artifactStatuses.length > 0 &&
    artifactStatuses.filter(artifact => pick(artifact, "required")).every(artifact => Boolean(pick(artifact, "exists")));
}

function countAssignedInternalProfileEntries() {
  const rules = new Set(getBuildProfileBundleRules().map(rule => rule.id).filter(Boolean));
  return getDraftBuildProfileEntries().filter(entry => {
    const mode = pick(entry, "deliveryMode") || "internal";
    const bundleRule = pick(entry, "bundleRule") || "";
    return mode === "internal" && bundleRule && rules.has(bundleRule);
  }).length;
}

function getBuildStatus() {
  return state.buildStatusPayload || {};
}

function getBuildDiagnostics() {
  const status = getBuildStatus();
  return mergeDiagnostics([
    ...asArray(pick(pick(state.buildProfilePayload, "validation"), "issues")),
    ...asArray(pick(getBundlePlan(), "diagnostics")),
    ...asArray(pick(pick(status, "validation"), "issues")),
    ...asArray(pick(pick(status, "plan"), "diagnostics")),
    ...asArray(pick(pick(status, "buildReport"), "issues"))
  ], []);
}

function getAuthorBuildReadiness() {
  if (state.buildProfileDirty) {
    return {
      state: "needsSave",
      label: "需要保存 Profile",
      tone: "warn",
      nextAction: "saveProfile",
      nextActionLabel: "先点击右上角保存 Profile，再刷新构建状态。"
    };
  }

  const status = getBuildStatus();
  const stateName = stringValue(pick(status, "state")) || "unknown";
  const label = stringValue(pick(status, "label")) || "构建状态未知";
  const nextAction = stringValue(pick(status, "nextAction")) || "refresh";
  const nextActionLabel = stringValue(pick(status, "nextActionLabel")) || "刷新 Resource Manager 或检查 Authoring Server。";
  return {
    state: stateName,
    label,
    tone: toneForBuildReadiness(stateName),
    nextAction,
    nextActionLabel
  };
}

function toneForBuildReadiness(stateName) {
  if (stateName === "readyForOfflineVerify") return "ok";
  if (stateName === "fixErrors") return "error";
  if (stateName === "needsSave" || stateName === "needsUnityBuild" || stateName === "possiblyStale") return "warn";
  return "muted";
}

function renderBuildReadinessPanel(readiness, status) {
  const summary = pick(status, "summary") || {};
  const profileTime = formatBuildTime(pick(status, "profileLastWriteUtc"));
  const reportTime = formatBuildTime(pick(status, "buildReportLastWriteUtc"));
  const effectiveTarget = pick(status, "effectiveBuildTarget") || "-";
  return `
    <section class="build-readiness-panel ${escapeHtml(readiness.tone)}">
      <div>
        <span class="status-pill ${escapeHtml(readiness.tone)}">构建状态</span>
        <h3>${escapeHtml(readiness.label)}</h3>
        <p>${escapeHtml(readiness.nextActionLabel)}</p>
        ${renderBuildReadinessAction(readiness)}
        <p class="build-boundary-note">Web UI 只读构建状态，不构建 AssetBundle，也不写 StreamingAssets。</p>
      </div>
      <div class="build-readiness-meta">
        ${metric("BuildTarget", effectiveTarget)}
        ${metric("Profile", profileTime || "-")}
        ${metric("Build Report", reportTime || "-")}
        ${metric("Errors", numericValue(pick(summary, "validationErrors")) + numericValue(pick(summary, "planErrors")) + numericValue(pick(summary, "buildReportErrors")))}
      </div>
    </section>`;
}

function renderBuildReadinessAction(readiness) {
  const action = getBuildReadinessAction(readiness);
  return `
    <div class="build-readiness-actions">
      <button type="button" class="primary-action" data-guide-action="${escapeHtml(action.action)}" title="${escapeHtml(action.hint)}">
        ${escapeHtml(action.label)}
      </button>
    </div>`;
}

function getBuildReadinessAction(readiness) {
  if (readiness.state === "needsSave") {
    return { action: "saveProfile", label: "保存 Profile", hint: "保存当前 Global Resource Build Profile 草稿。" };
  }
  if (readiness.state === "fixErrors") {
    return { action: "openDiagnostics", label: "查看必须处理", hint: "打开 Diagnostics 查看阻塞问题。" };
  }
  if (readiness.state === "needsUnityBuild" || readiness.state === "possiblyStale") {
    return { action: "openUnity", label: "Unity 构建指引", hint: "查看 Unity 菜单路径；Web 不执行 AssetBundle 构建。" };
  }
  if (readiness.state === "readyForOfflineVerify") {
    return { action: "copyBuildReport", label: "复制验证清单", hint: "复制构建总览，进入 Offline 验证前留存。" };
  }
  return { action: "refresh", label: "刷新状态", hint: "重新读取 Authoring 服务数据。" };
}

function renderBuildStatusBundleReport(status, fallbackBundles) {
  const statusBundles = asArray(pick(status, "bundles"));
  const bundles = statusBundles.length > 0
    ? statusBundles
    : asArray(fallbackBundles).map(bundle => ({
      bundleName: pick(bundle, "bundleName"),
      bundleRuleId: pick(bundle, "bundleId"),
      compression: pick(bundle, "compression"),
      entryCount: asArray(pick(bundle, "resourceKeys")).length,
      exists: false,
      sizeBytes: 0,
      dependencies: [],
      resourceKeys: asArray(pick(bundle, "resourceKeys"))
    }));
  if (bundles.length === 0) return emptyBlock("没有规划内部 Bundle。");
  return `
    <section class="build-subsection">
      <h3>Bundle 构建状态</h3>
      <div class="bundle-status-list">
        ${bundles.map(bundle => {
          const exists = Boolean(pick(bundle, "exists"));
          const dependencies = asArray(pick(bundle, "dependencies"));
          const resourceKeys = asArray(pick(bundle, "resourceKeys"));
          return `
            <article class="bundle-status-row ${exists ? "ok" : "warn"}">
              <div class="bundle-status-head">
                <strong>${escapeHtml(pick(bundle, "bundleName") || "-")}</strong>
                <span class="status-pill ${exists ? "ok" : "warn"}">${exists ? "已生成" : "缺少文件"}</span>
              </div>
              <div class="bundle-status-metrics">
                ${metric("资源", pick(bundle, "entryCount") ?? resourceKeys.length)}
                ${metric("大小", formatBytes(pick(bundle, "sizeBytes")))}
                ${metric("规则", pick(bundle, "bundleRuleId") || "-")}
                ${metric("类型", pick(bundle, "sceneOnly") ? "Scene-only" : "Asset")}
              </div>
              <small>${escapeHtml(pick(bundle, "path") || "等待 Unity 构建生成 bundle 文件")}</small>
              <small>dependencies: ${escapeHtml(dependencies.join(", ") || "none")}</small>
              <details>
                <summary>${resourceKeys.length} 个 ResourceKey</summary>
                <ul class="bundle-plan-resources">${resourceKeys.map(key => `<li>${escapeHtml(formatProfileResourceKey(key) || key)}</li>`).join("")}</ul>
              </details>
            </article>`;
        }).join("")}
      </div>
    </section>`;
}

function renderBuildArtifactReport(status) {
  const artifacts = asArray(pick(status, "artifacts"));
  if (artifacts.length === 0) return emptyBlock("没有读取到生成产物状态。");
  return `
    <section class="build-subsection">
      <h3>生成产物</h3>
      <div class="artifact-status-grid">
        ${artifacts.map(artifact => {
          const exists = Boolean(pick(artifact, "exists"));
          return `
            <article class="artifact-status-card ${exists ? "ok" : "warn"}">
              <div>
                <strong>${escapeHtml(formatArtifactKind(artifact))}</strong>
                <span class="status-pill ${exists ? "ok" : "warn"}">${exists ? "存在" : "缺失"}</span>
              </div>
              <small>${escapeHtml(pick(artifact, "path") || "-")}</small>
              <span>${escapeHtml(formatBytes(pick(artifact, "sizeBytes")))} · ${escapeHtml(formatBuildTime(pick(artifact, "lastWriteUtc")) || "-")}</span>
            </article>`;
        }).join("")}
      </div>
    </section>`;
}

function renderBuildDiagnosticsForAuthors(diagnostics) {
  const groups = groupBuildDiagnosticsForAuthors(diagnostics);
  const blocking = groups.blocking;
  const external = groups.externalConfigurationOnly;
  const otherWarnings = groups.otherWarnings;
  const info = groups.info;
  if (blocking.length + external.length + otherWarnings.length + info.length === 0) {
    return emptyBlock("没有构建诊断。");
  }

  return `
    ${blocking.length > 0 ? `
      <section class="diagnostic-author-group">
        <h4>必须处理</h4>
        ${renderDiagnosticsList(blocking)}
      </section>` : ""}
    ${otherWarnings.length > 0 ? `
      <section class="diagnostic-author-group">
        <h4>可稍后处理</h4>
        ${renderDiagnosticsList(otherWarnings)}
      </section>` : ""}
    ${external.length > 0 ? `
      <details class="diagnostic-author-group external-collapsed">
        <summary>${external.length} 个 external 配置引用（不进入本地 AssetBundle，已折叠）</summary>
        ${renderDiagnosticsList(external)}
      </details>` : ""}
    ${info.length > 0 ? renderDiagnosticsList(info) : ""}`;
}

function groupBuildDiagnosticsForAuthors(diagnostics) {
  const rows = asArray(diagnostics);
  const result = {
    blocking: [],
    externalConfigurationOnly: [],
    otherWarnings: [],
    info: []
  };
  for (const diagnostic of rows) {
    const severity = getSeverity(diagnostic);
    if (severity === "Error") {
      result.blocking.push(diagnostic);
    } else if (isExternalConfigurationOnlyDiagnostic(diagnostic)) {
      result.externalConfigurationOnly.push(diagnostic);
    } else if (severity === "Warning") {
      result.otherWarnings.push(diagnostic);
    } else {
      result.info.push(diagnostic);
    }
  }
  return result;
}

function isExternalConfigurationOnlyDiagnostic(diagnostic) {
  const code = stringValue(pick(diagnostic, "code"));
  return code.includes("ExternalEntryConfigurationOnly") ||
    code.includes("externalEntryConfigurationOnly") ||
    stringValue(pick(diagnostic, "message")).includes("External resource remains a configuration reference");
}

function formatArtifactKind(artifact) {
  const kind = pick(artifact, "kind") || "-";
  const bundleName = pick(artifact, "bundleName");
  return bundleName ? `${kind}: ${bundleName}` : kind;
}

function formatBytes(value) {
  const bytes = numericValue(value);
  if (bytes <= 0) return "0 B";
  const units = ["B", "KB", "MB", "GB"];
  let size = bytes;
  let index = 0;
  while (size >= 1024 && index < units.length - 1) {
    size /= 1024;
    index++;
  }
  return `${size.toFixed(index === 0 ? 0 : 1)} ${units[index]}`;
}

function formatBuildTime(value) {
  if (!value) return "";
  const date = new Date(value);
  if (Number.isNaN(date.getTime())) return "";
  return date.toLocaleString("zh-CN", { hour12: false });
}


function renderInspector() {
  for (const tab of el.inspectorTabs) {
    tab.classList.toggle("active", tab.dataset.tab === state.activeTab);
  }

  const item = getSelectedItem();
  if (!item) {
    el.inspectorStatus.textContent = "选择一个资源项";
    el.inspectorContent.innerHTML = state.activeTab === "diagnostics"
      ? renderDiagnosticsTab(null, null)
      : emptyBlock("选择左侧资源后查看总览、Unity、运行时、引用和诊断。");
    return;
  }

  const detail = getCurrentDetail(item);
  const fallbackText = state.inspectState.status === "fallback" ? "inspect fallback" : state.inspectState.status;
  el.inspectorStatus.textContent = `${item.libraryItemId || item.stableId || item.resourceKey} · ${fallbackText}`;

  if (state.inspectState.status === "loading") {
    el.inspectorContent.innerHTML = emptyBlock("正在读取 inspect endpoint；列表和 plan 推导详情已可用。");
    return;
  }

  const notice = state.inspectState.error
    ? `<div class="notice warn">${escapeHtml(state.inspectState.error)}</div>`
    : "";
  el.inspectorContent.innerHTML = notice + renderInspectorTab(state.activeTab, item, detail);
  renderRawJson();
}

function renderDebugPicker() {
  const items = getFilteredItems(getBrowserItems()).slice(0, 40);
  const selectedKey = state.selectedResourceKey;
  el.debugPickerSummary.textContent = items.length === 0
    ? "资源浏览工作区暂无可见资源"
    : `显示 ${items.length} 个可见资源（与资源浏览筛选同步）`;
  if (items.length === 0) {
    el.debugResourcePicker.innerHTML = emptyBlock("请先在资源浏览中加载资源");
    return;
  }
  el.debugResourcePicker.innerHTML = items.map(item => `
    <button type="button" class="debug-picker-item${item.key === selectedKey ? " active" : ""}" data-resource-key="${escapeHtml(item.key)}">
      <strong>${escapeHtml(formatResourceDisplayName(item))}</strong>
      <span>${escapeHtml(item.kind || "-")} · ${escapeHtml(getBuildProfileStatus(item))}${item.foldedCount > 1 ? ` · ${item.foldedCount} 身份` : ""}${item.diagnosticCount > 0 ? ` · 诊断 ${item.diagnosticCount}` : ""}</span>
    </button>`).join("");
}

function renderRawJson() {
  const item = getSelectedItem();
  const detail = item ? getCurrentDetail(item) : null;
  const payload = item
    ? { item: item.raw, inspect: detail, buildProfile: findBuildProfileEntryForItem(item) }
    : {
      package: state.packageRelative,
      resources: getNormalizedItems().map(resource => resource.raw),
      resourcePlan: state.resourcePlanPayload,
      buildProfile: getBuildProfile()
    };
  el.rawJsonContent.innerHTML = renderJsonBlock(payload);
}

function renderInspectorTab(tab, item, detail) {
  if (tab === "unity") return renderUnityTab(item, detail);
  if (tab === "runtime") return renderRuntimeTab(item, detail);
  if (tab === "build") return renderBuildTab(item);
  if (tab === "references") return renderReferencesTab(detail);
  if (tab === "diagnostics") return renderDiagnosticsTab(detail, item);
  return renderOverviewTab(item, detail);
}

function renderOverviewTab(item) {
  return `
    <section class="inspector-section">
      <h3>总览</h3>
      ${renderKeyValueList([
        ["canonicalName", formatResourceDisplayName(item)],
        ["technicalIdentity", formatResourceIdentity(item)],
        ["provider 层级", formatResourceProviderStack(item)],
        ["折叠身份", item.foldedCount > 1 ? `${item.foldedCount} 同源资源身份` : "1"],
        ["libraryItemId", item.libraryItemId || "-"],
        ["stableId", item.stableId || "-"],
        ["displayName", item.displayName || "-"],
        ["类型 / 用途", `${item.kind || "-"} / ${item.usage || "-"}`],
        ["标签", item.tags.length > 0 ? item.tags.join(", ") : "-"],
        ["源类型", item.sourceKind || "-"],
        ["源路径", item.sourcePath || "-"],
        ["导入状态", item.importStatus || "-"],
        ["运行时可用性", item.runtimeAvailability || "-"]
      ])}
      <p class="profile-hint">完整 JSON 见下方原始 JSON（默认折叠）。</p>
    </section>
    ${renderFoldedIdentityTable(item)}`;
}

function renderFoldedIdentityTable(item) {
  const peers = asArray(item.foldedItems);
  if (peers.length <= 1) return "";
  const rows = peers.map(peer => ({
    canonicalName: peer.canonicalDisplayName || peer.displayName || "-",
    providerId: peer.providerId || "-",
    sourceKind: peer.sourceKind || "-",
    runtime: peer.runtimeAvailability || "-",
    identity: chooseCanonicalIdentity([peer]) || peer.stableId || peer.libraryItemId || "-"
  }));
  return `
    <section class="inspector-section">
      <h3>同源身份折叠</h3>
      <p class="profile-hint">这些 provider/profile/raw 记录指向同一个底层资源，资源浏览默认折叠为上面的 canonicalName。</p>
      ${renderArrayTable(rows, ["canonicalName", "providerId", "sourceKind", "runtime", "identity"])}
    </section>`;
}

function renderUnityTab(item, detail) {
  const unity = detail.unity || {};
  return `
    <section class="inspector-section">
      <h3>Unity 同步状态</h3>
      ${renderKeyValueList([
        ["unityAssetGuid", pick(unity, "unityAssetGuid", "guid") || "-"],
        ["unityAssetPath", pick(unity, "unityAssetPath", "assetPath") || item.unityAssetPath || "-"],
        ["Importer 类型", pick(unity, "importerKind", "importer") || "-"],
        ["主对象类型", pick(unity, "mainObjectType", "objectType") || "-"],
        ["最近导入操作", pick(unity, "lastImportOperation", "lastOperation") || "-"]
      ])}
    </section>
    <section class="inspector-section">
      <h3>子资产</h3>
      ${renderArrayTable(asArray(pick(unity, "subAssets", "subassets")), ["name", "type", "localId", "path"])}
    </section>
    <section class="inspector-section">
      <h3>Unity 诊断</h3>
      ${renderDiagnosticsList(asArray(pick(unity, "diagnostics", "unityDiagnostics")))}
    </section>`;
}

function renderRuntimeTab(item, detail) {
  const runtime = detail.runtime || {};
  const plans = normalizeInspectPlans(asArray(detail.plans));
  return `
    <section class="inspector-section">
      <h3>运行时绑定</h3>
      ${renderKeyValueList([
        ["运行时绑定类型", pick(runtime, "runtimeBindingKind", "bindingKind") || item.runtimeBindingKind || "-"],
        ["resourceKey", pick(runtime, "resourceKey", "id") || item.resourceKey || "-"],
        ["provider id", pick(runtime, "providerId", "provider") || item.providerId || "-"],
        ["address", pick(runtime, "address", "assetPath") || "-"],
        ["资产类型", pick(runtime, "assetType", "type", "typeId") || item.kind || "-"],
        ["hash", pick(runtime, "hash") || item.hash || "-"],
        ["预加载策略", plans.map(plan => plan.groupName || plan.group || plan.preloadPolicy).filter(Boolean).join(", ") || "-"]
      ])}
    </section>
    <section class="inspector-section">
      <h3>包含的运行时计划分组</h3>
      ${renderArrayTable(plans, ["groupName", "group", "required", "failurePolicy", "resourceKey", "preloadPolicy"])}
    </section>
    <section class="inspector-section">
      <h3>运行时 JSON</h3>
      ${renderJsonBlock(runtime)}
    </section>`;
}

function renderBuildTab(item) {
  const entry = findDraftBuildProfileEntryForItem(item);
  const savedEntry = findSavedBuildProfileEntryForItem(item);
  const planEntry = findBundlePlanEntryForItem(item);
  const profileStatus = getBuildProfileStatus(item);
  return `
    <section class="inspector-section">
      <h3>全局构建 Profile</h3>
      ${entry ? renderKeyValueList([
        ["状态", profileStatus],
        ["owner", pick(pick(entry, "producerReceipt"), "producerId") || MANUAL_RESOURCE_MAPPING_PRODUCER],
        ["deliveryMode", pick(entry, "deliveryMode") || "internal"],
        ["overrideIntent", pick(entry, "overrideIntent") || "-"],
        ["bundleRule", pick(entry, "bundleRule") || "-"],
        ["preloadGroups", asArray(pick(entry, "preloadGroups")).join(", ") || "-"],
        ["labels", asArray(pick(entry, "labels")).join(", ") || "-"]
      ]) : savedEntry ? renderKeyValueList([
        ["状态", profileStatus],
        ["已保存交付模式", pick(savedEntry, "deliveryMode") || "internal"],
        ["draft", "已从草稿移除，保存后生效"]
      ]) : emptyBlock("当前资源尚未加入 Global Resource Build Profile。")}
    </section>
    <section class="inspector-section">
      <h3>Bundle 规划器</h3>
      ${planEntry ? renderKeyValueList([
        ["bundleId", pick(planEntry, "bundleId") || "-"],
        ["catalogMountId", pick(planEntry, "catalogMountId") || "-"],
        ["deliveryIntent", pick(planEntry, "deliveryIntent") || "-"],
        ["overrideIntent", pick(planEntry, "overrideIntent") || "-"]
      ]) : emptyBlock("当前资源没有 planner 输出。")}
    </section>
    <section class="inspector-section">
      <h3>Profile 条目 JSON</h3>
      ${renderJsonBlock(entry || {})}
    </section>`;
}

function normalizeInspectPlans(plans) {
  return plans.map(plan => {
    const resource = pick(plan, "resource") || {};
    return {
      ...resource,
      ...plan,
      groupName: stringValue(pick(plan, "groupName", "group")) || stringValue(pick(resource, "groupName", "group")),
      resourceKey: stringValue(pick(plan, "resourceKey", "id", "cue", "bank"))
        || stringValue(pick(resource, "resourceKey", "id", "cue", "bank"))
    };
  });
}

function renderReferencesTab(detail) {
  const references = asArray(detail.references);
  return `
    <section class="inspector-section">
      <h3>引用</h3>
      ${references.length === 0 ? emptyBlock("没有引用关系。") : renderArrayTable(references, [
        "sourceConfigKind",
        "sourceStableId",
        "sourceField",
        "targetLibraryItemStableId",
        "targetResourceKey",
        "bindingKind",
        "isRequiredAtRuntime",
        "preloadPolicy"
      ])}
    </section>`;
}

function renderDiagnosticsTab(detail, item) {
  const globalDiagnostics = getAllDiagnostics();
  const itemDiagnostics = detail ? asArray(detail.diagnostics) : [];
  return `
    <section class="inspector-section diagnostics-overview">
      <h3>全局诊断</h3>
      ${renderDiagnosticsSummary(globalDiagnostics)}
      ${renderDiagnosticsList(globalDiagnostics)}
    </section>
    <section class="inspector-section">
      <h3>${item ? "当前资源诊断" : "当前资源诊断"}</h3>
      ${item ? `<p class="profile-hint">${escapeHtml(formatResourceDisplayName(item))}</p>` : ""}
      ${renderDiagnosticsList(itemDiagnostics)}
    </section>`;
}
