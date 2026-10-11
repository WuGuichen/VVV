// Resource Library split module.
// Build Profile and Bundle authoring workspace.
// Loaded by index.html as a classic script; keep shared bindings global.
const MANUAL_RESOURCE_MAPPING_PRODUCER = "resource-manager.manual";

function isManualProfileEntry(entry) {
  const owner = stringValue(pick(pick(entry, "producerReceipt"), "producerId"));
  return !owner || owner === MANUAL_RESOURCE_MAPPING_PRODUCER;
}

function isManualProfileItem(item) {
  const entry = item ? findDraftBuildProfileEntryForItem(item) : null;
  return !entry || isManualProfileEntry(entry);
}

function normalizeProfileDeliveryMode(value) {
  const mode = String(value || "internal").toLowerCase();
  return mode === "editoronly" ? "editorOnly" : mode;
}
async function addSelectedToBuildProfile() {
  const item = getSelectedItem();
  const profile = ensureBuildProfileDraft();
  if (!item || !profile) return;
  if (findDraftBuildProfileEntryForItem(item)) {
    setWorkspace("profile");
    return;
  }
  if (!Array.isArray(profile.entries)) profile.entries = [];
  profile.entries.push(buildDraftBuildProfileEntryForItem(item));
  markBuildProfileDirty();
  state.lastActionMessage = "已加入构建 Profile 草稿，保存后生效。";
  setWorkspace("profile");
  render();
}

async function removeSelectedFromBuildProfile() {
  const item = getSelectedItem();
  const profile = getBuildProfile();
  if (!item || !profile || !Array.isArray(profile.entries)) return;
  const existing = findDraftBuildProfileEntryForItem(item);
  if (existing && !isManualProfileEntry(existing)) {
    state.lastActionMessage = `只读：该条目由 ${pick(existing.producerReceipt, "producerId")} 生产，需在所属创作工具中修改。`;
    render();
    return;
  }
  const before = profile.entries.length;
  profile.entries = profile.entries.filter(entry => !isManualProfileEntry(entry) || !profileEntryMatchesItem(entry, item));
  if (before !== profile.entries.length) markBuildProfileDirty();
  state.lastActionMessage = before === profile.entries.length ? "当前资源不在构建 Profile 中。" : "已从构建 Profile 草稿移除，保存后生效。";
  render();
}

async function addCheckedToBuildProfile() {
  const profile = ensureBuildProfileDraft();
  const checkedItems = getCheckedItems();
  if (!profile || checkedItems.length === 0) return;
  if (!Array.isArray(profile.entries)) profile.entries = [];
  let added = 0;
  for (const item of checkedItems) {
    if (findDraftBuildProfileEntryForItem(item)) continue;
    profile.entries.push(buildDraftBuildProfileEntryForItem(item));
    added++;
  }
  if (added > 0) markBuildProfileDirty();
  state.lastActionMessage = added > 0
    ? `已将 ${added} 个勾选资源加入 Build Profile 草稿，保存后生效。`
    : "已勾选资源都已在 Build Profile 草稿中。";
  if (added > 0) setWorkspace("profile");
  render();
}

async function removeCheckedFromBuildProfile() {
  const profile = getBuildProfile();
  const checkedItems = getCheckedItems();
  if (!profile || !Array.isArray(profile.entries) || checkedItems.length === 0) return;
  const before = profile.entries.length;
  profile.entries = profile.entries.filter(entry => !isManualProfileEntry(entry) || !checkedItems.some(item => profileEntryMatchesItem(entry, item)));
  const removed = before - profile.entries.length;
  if (removed > 0) markBuildProfileDirty();
  state.lastActionMessage = removed > 0
    ? `已将 ${removed} 个勾选资源从 Build Profile 草稿移除，保存后生效。`
    : "已勾选资源当前不在 Build Profile 草稿中。";
  render();
}

function getActionBundleRuleId(button) {
  const container = button.closest(".context-actions, .batch-bar, .bundle-actions, .bundle-members-toolbar") || document;
  const select = container.querySelector("[data-bundle-action-select]");
  return select?.value || state.selectedBundleRuleId || "";
}

function assignSelectedToBundle(bundleRuleId) {
  const item = getSelectedItem();
  if (!item) return;
  const rule = ensureBundleRuleForAssignment(bundleRuleId);
  if (!rule) return;
  assignItemsToBundle([item], rule.id);
}

function assignCheckedToBundle(bundleRuleId) {
  const items = getCheckedItems();
  if (items.length === 0) {
    state.lastActionMessage = "No checked resources to assign.";
    render();
    return;
  }
  const rule = ensureBundleRuleForAssignment(bundleRuleId);
  if (!rule) return;
  assignItemsToBundle(items, rule.id);
}

function clearCheckedBundleAssignments() {
  const checkedItems = getCheckedItems();
  if (checkedItems.length === 0) return;
  let changed = 0;
  for (const item of checkedItems) {
    const entry = findDraftBuildProfileEntryForItem(item);
    if (!entry || !isManualProfileEntry(entry) || !entry.bundleRule) continue;
    entry.bundleRule = "";
    changed++;
  }
  if (changed > 0) markBuildProfileDirty();
  state.lastActionMessage = changed > 0
    ? `Cleared bundle assignment from ${changed} checked resource(s).`
    : "Checked resources did not have bundle assignments.";
  render();
}

function assignItemsToBundle(items, bundleRuleId) {
  const profile = ensureBuildProfileDraft();
  if (!profile || !bundleRuleId) return;
  const assignableItems = items.filter(canAssignItemToInternalBundle);
  let skipped = items.length - assignableItems.length;
  if (assignableItems.length === 0) {
    state.lastActionMessage = "未加入 Bundle：所选资源不是可构建的 internal runtime 资源。请先导入/同步为 Unity 可构建资源。";
    render();
    return;
  }
  if (!Array.isArray(profile.entries)) profile.entries = [];
  let changed = 0;
  let added = 0;
  for (const item of assignableItems) {
    let entry = findDraftBuildProfileEntryForItem(item);
    if (entry && !isManualProfileEntry(entry)) {
      skipped++;
      continue;
    }
    if (!entry) {
      entry = buildDraftBuildProfileEntryForItem(item);
      profile.entries.push(entry);
      added++;
    }
    if (entry.bundleRule !== bundleRuleId || entry.deliveryMode !== "internal") {
      entry.bundleRule = bundleRuleId;
      entry.deliveryMode = "internal";
      changed++;
    }
  }
  if (changed > 0 || added > 0) markBuildProfileDirty();
  state.selectedBundleRuleId = bundleRuleId;
  const skippedSuffix = skipped > 0 ? `，跳过 ${skipped} 个 external/editor-only/source-only 资源` : "";
  state.lastActionMessage = `Assigned ${assignableItems.length} resource(s) to bundle ${bundleRuleId}; added ${added} new profile entr${added === 1 ? "y" : "ies"}${skippedSuffix}.`;
  setWorkspace("profile");
  render();
}

function ensureBundleRuleForAssignment(bundleRuleId) {
  const profile = ensureBuildProfileDraft();
  if (!profile) return null;
  if (!Array.isArray(profile.bundleRules)) profile.bundleRules = [];
  let rule = findBundleRuleById(bundleRuleId);
  if (rule) return rule;
  if (bundleRuleId) {
    state.lastActionMessage = `Bundle policy ${bundleRuleId} does not exist. Update it through its policy producer.`;
    render();
    return null;
  }
  state.lastActionMessage = "Bundle policy is read-only in Resource Manager; select an existing policy rule.";
  render();
  return null;
}

function applyBuildProfileBatchFields() {
  const checkedItems = getCheckedItems();
  const batch = readBuildProfileBatchFields();
  if (checkedItems.length === 0) {
    state.lastActionMessage = "未勾选资源，批量字段编辑未执行。";
    render();
    return;
  }
  if (batch.length === 0) {
    state.lastActionMessage = "没有启用可应用的批量字段；空输入不会覆盖现有值。";
    render();
    return;
  }

  let applied = 0;
  let skipped = 0;
  let changed = 0;
  const assignsBundle = batch.some(field => field.key === "bundleRule" && field.value);
  for (const item of checkedItems) {
    const entry = findDraftBuildProfileEntryForItem(item);
    if (!entry || !isManualProfileEntry(entry)) {
      skipped++;
      continue;
    }
    if (assignsBundle && !canAssignItemToInternalBundle(item)) {
      skipped++;
      continue;
    }
    let entryChanged = false;
    for (const field of batch) {
      if (buildProfileBatchValuesEqual(entry[field.key], field.value)) continue;
      entry[field.key] = cloneBuildProfileBatchValue(field.value);
      entryChanged = true;
    }
    if (batch.some(field => field.key === "deliveryMode" && field.value !== "internal") && entry.bundleRule) {
      entry.bundleRule = "";
      entryChanged = true;
    }
    if (batch.some(field => field.key === "bundleRule" && field.value)) {
      entry.deliveryMode = "internal";
    }
    applied++;
    if (entryChanged) changed++;
  }

  if (changed > 0) markBuildProfileDirty();
  state.lastActionMessage = `批量字段编辑已应用 ${applied} 个 draft profile entry，跳过 ${skipped} 个未加入草稿的勾选资源。`;
  render();
}

function getProfileBatchRoot() {
  return !el.profileBatchBar.classList.contains("hidden") ? el.profileBatchBar : el.buildProfileContent;
}

function readBuildProfileBatchFields() {
  const fields = [];
  const root = getProfileBatchRoot();
  const controls = Array.from(root.querySelectorAll("[data-profile-batch-field]"));
  for (const control of controls) {
    const key = control.dataset.profileBatchField;
    const enabled = root.querySelector(`[data-profile-batch-enabled="${cssEscapeCompat(key)}"]`);
    if (!enabled?.checked) continue;
    if (control.disabled) continue;
    const rawValue = String(control.value || "");
    if (rawValue.trim() === "") continue;
    const value = key === "labels" || key === "preloadGroups"
      ? splitCsv(rawValue)
      : rawValue.trim();
    if (Array.isArray(value) && value.length === 0) continue;
    if (key === "overrideIntent" && !["forbid", "allow"].includes(value)) continue;
    fields.push({ key, value });
  }
  return fields;
}

function buildProfileBatchValuesEqual(left, right) {
  if (Array.isArray(left) || Array.isArray(right)) {
    if (!Array.isArray(left) || !Array.isArray(right)) return false;
    return left.length === right.length && left.every((value, index) => value === right[index]);
  }
  return left === right;
}

function cloneBuildProfileBatchValue(value) {
  return Array.isArray(value) ? [...value] : value;
}

function syncBuildProfileBatchField(field, enabled) {
  const control = getProfileBatchRoot().querySelector(`[data-profile-batch-field="${cssEscapeCompat(field)}"]`);
  if (control) control.disabled = !enabled;
}

function updateSelectedBuildProfileField(field, value, renderFeedback) {
  const item = getSelectedItem();
  const entry = item ? findDraftBuildProfileEntryForItem(item) : null;
  if (!entry || !isManualProfileEntry(entry)) {
    if (entry) state.lastActionMessage = `只读：该条目由 ${pick(entry.producerReceipt, "producerId")} 生产。`;
    return;
  }
  if (field === "bundleRule" && value && !canAssignItemToInternalBundle(item)) {
    entry.bundleRule = "";
    state.lastActionMessage = `不能加入 Bundle：${describeBundleAssignBlocker(item)}`;
    markBuildProfileDirty();
    render();
    return;
  }
  if (field === "labels" || field === "preloadGroups") {
    entry[field] = splitCsv(value);
  } else {
    entry[field] = value;
  }
  if (field === "deliveryMode" && value !== "internal") {
    entry.bundleRule = "";
    entry.bundleGroupHint = entry.bundleGroupHint || "";
  }
  if (field === "bundleRule") {
    entry.deliveryMode = "internal";
    state.selectedBundleRuleId = value || state.selectedBundleRuleId;
  }
  if (field === "resourceKeyId") {
    const parsed = parseProfileResourceKey(value);
    entry.resourceKey = {
      ...(entry.resourceKey || {}),
      packageId: parsed.packageId || pick(entry.resourceKey, "packageId") || "",
      type: parsed.type || pick(entry.resourceKey, "type") || "",
      id: parsed.id || "",
      variant: parsed.variant || pick(entry.resourceKey, "variant") || ""
    };
  }
  markBuildProfileDirty();
  if (renderFeedback) {
    render();
  } else {
    renderContextActions();
  }
}

async function saveBuildProfileDraft() {
  const profile = getBuildProfile();
  if (!profile) return;
  setWorkspace("profile");
  state.writeState = { status: "running", action: "save-build-profile", error: "" };
  state.lastActionMessage = "正在提交 Resource Manager manual producer 快照...";
  state.profileFieldHighlight = "";
  render();
  try {
    const snapshot = await buildManualProducerSnapshot(profile);
    await postJson(API.saveBuildProfile, {
      expectedRevision: stringValue(pick(state.buildProfilePayload, "revision")),
      buildTarget: requireEffectiveResourceBuildTarget(),
      snapshot
    });
    await loadBuildProfile();
    state.buildProfileDirty = false;
    state.referencesLastPatch = null;
    await loadBundlePlan();
    await loadBuildStatus();
    state.writeState = { status: "idle", action: "", error: "" };
    state.lastActionMessage = "Resource Manager manual producer 快照已保存。";
  } catch (error) {
    state.writeState = { status: "error", action: "save-build-profile", error: error.message };
    state.lastActionMessage = `Profile 保存失败：${formatValidationSummary(error.data) || error.message}`;
    state.profileFieldHighlight = inferProfileValidationField(error.data);
    if (error.data) {
      state.buildProfileDirty = true;
      state.referencesLastPatch = null;
    }
    state.errors.push(apiError("保存资源映射 manual producer", error));
  }
  render();
}

function normalizeResourceMappingProfileForEditor(profile) {
  if (!profile || !Array.isArray(profile.entries)) return profile;
  for (const entry of profile.entries) {
    entry.deliveryMode = normalizeProfileDeliveryMode(entry.deliveryIntent);
    entry.bundleRule = entry.bundleIntent?.ruleId || "";
    entry.overrideIntent = entry.overrideIntent == null ? null : String(entry.overrideIntent).toLowerCase();
    entry.profileOwnerReadOnly = !isManualProfileEntry(entry);
    entry.runtimeLoadable = ["internal", "external"].includes(entry.deliveryMode);
    entry.editorOnly = entry.deliveryMode === "editorOnly";
    const locator = entry.sourceLocator || {};
    entry.source = {
      unityAssetPath: locator.cachedPath || "",
      unityGuid: locator.unityGuid || "",
      externalSourcePath: locator.packageRelativePath || "",
      providerData: locator.providerData || {}
    };
  }
  return profile;
}

async function buildManualProducerSnapshot(profile) {
  const owned = asArray(profile?.entries).filter(entry =>
    !entry?.producerReceipt?.producerId || entry.producerReceipt.producerId === "resource-manager.manual");
  const entries = [];
  for (let index = 0; index < owned.length; index++) {
    const source = owned[index];
    const mode = normalizeProfileDeliveryMode(source.deliveryMode || source.deliveryIntent);
    const persistedMode = mode.toLowerCase();
    const runtime = persistedMode === "internal" || persistedMode === "external";
    if (persistedMode === "external" && !source.externalCatalogBinding?.catalogMountId) {
      throw new Error("External manual resource requires an explicit CatalogMount.");
    }
    const sourceView = source.source || {};
    const hasViewLocator = Boolean(sourceView.unityGuid || sourceView.unityAssetPath);
    const locatorSource = source.sourceLocator || (hasViewLocator ? {
      kind: "unityAsset",
      unityGuid: sourceView.unityGuid || "",
      cachedPath: sourceView.unityAssetPath || "",
      generatedAssetId: "",
      domainSourceId: "resource-manager.manual",
      packageRelativePath: sourceView.externalSourcePath || "",
      contentHash: "",
      providerData: sourceView.providerData || {}
    } : null);
    const locator = locatorSource ? {
      kind: locatorSource.kind || "unityAsset",
      unityGuid: locatorSource.unityGuid || "",
      cachedPath: locatorSource.cachedPath || "",
      generatedAssetId: locatorSource.generatedAssetId || "",
      domainSourceId: locatorSource.domainSourceId || "resource-manager.manual",
      packageRelativePath: locatorSource.packageRelativePath || "",
      contentHash: locatorSource.contentHash || "",
      providerData: cloneManualProviderData(locatorSource.providerData)
    } : null;
    const hasLocator = persistedMode !== "excluded" && locator !== null;
    const entry = {
      authoringRecordId: source.authoringRecordId || `resource-manager.manual:${index}:${source.resourceKey?.id || "excluded"}`,
      resourceKey: runtime ? normalizeManualResourceKey(source.resourceKey) : null,
      sourceLocator: hasLocator ? locator : null,
      deliveryIntent: mode,
      overrideIntent: runtime ? String(source.overrideIntent || "forbid").toLowerCase() : null,
      externalCatalogBinding: persistedMode === "external" ? source.externalCatalogBinding : null,
      bundleIntent: persistedMode === "internal" ? {
        ruleId: Object.prototype.hasOwnProperty.call(source, "bundleRule") ? source.bundleRule || "" : source.bundleIntent?.ruleId || ""
      } : null,
      labels: asArray(source.labels),
      preloadGroups: runtime ? asArray(source.preloadGroups) : [],
      dependencies: runtime ? asArray(source.dependencies) : [],
      providerData: cloneManualProviderData(source.providerData),
      producerReceipt: null
    };
    const fingerprint = await sha256Text(JSON.stringify(entry));
    entry.producerReceipt = {
      producerId: "resource-manager.manual",
      producerContractVersion: "2",
      sourceRecordFingerprint: fingerprint
    };
    entries.push(entry);
  }
  return { producerId: "resource-manager.manual", entries, bundleRules: [], catalogMounts: [], preloadGroups: [] };
}

function normalizeManualResourceKey(key) {
  return {
    id: stringValue(pick(key, "id")),
    typeId: stringValue(pick(key, "typeId", "type")),
    variant: stringValue(pick(key, "variant")),
    packageId: stringValue(pick(key, "packageId"))
  };
}

function cloneManualProviderData(data) {
  const result = {};
  const forbidden = new Set([
    "address", "providerid", "resourcekey", "resourceid", "typeid", "variant", "packageid",
    "deliveryintent", "deliverymode", "runtimeloadable", "editoronly", "forceexternal", "exclude",
    "bundleintent", "bundleruleid", "bundleoverridemode", "bundleoverridevalue", "forcebundle", "forcestandalone",
    "runtimeresourcekey", "providerresourcekey", "packageresourcekey", "projectresourcekey"
  ]);
  for (const [key, value] of Object.entries(data || {})) {
    if (forbidden.has(String(key).toLowerCase())) continue;
    if (value != null && String(value).trim()) result[key] = String(value);
  }
  return result;
}

async function sha256Text(value) {
  const bytes = new TextEncoder().encode(value);
  const digest = await crypto.subtle.digest("SHA-256", bytes);
  return `sha256:${Array.from(new Uint8Array(digest)).map(byte => byte.toString(16).padStart(2, "0")).join("")}`;
}


function revertSelectedBuildProfileDraft() {
  const item = getSelectedItem();
  const saved = item ? findSavedBuildProfileEntryForItem(item) : null;
  const profile = getBuildProfile();
  if (!item || !profile || !Array.isArray(profile.entries)) return;
  const draftEntry = findDraftBuildProfileEntryForItem(item);
  if (!draftEntry) return;
  if (!isManualProfileEntry(draftEntry)) {
    state.lastActionMessage = `只读：该条目由 ${pick(draftEntry.producerReceipt, "producerId")} 生产。`;
    render();
    return;
  }
  if (saved) {
    const index = profile.entries.findIndex(entry => profileEntryMatchesItem(entry, item));
    if (index >= 0) profile.entries[index] = structuredCloneCompat(saved);
  } else {
    profile.entries = profile.entries.filter(entry => !profileEntryMatchesItem(entry, item));
  }
  markBuildProfileDirty();
  state.lastActionMessage = saved ? "已还原当前资源为已保存 Profile 字段。" : "已从草稿移除当前资源。";
  render();
}


function renderBuildProfile() {
  renderWorkflowAssistant(el.profileWorkflowAssistant, getProfileGuideState());
  const profile = getBuildProfile();
  const entries = getBuildProfileEntries();
  const selected = getSelectedItem();
  const selectedEntry = selected ? findBuildProfileEntryForItem(selected) : null;
  const checkedCount = state.checkedResourceKeys.size;
  const bundles = getBuildProfileBundleRules();

  if (!profile) {
    el.buildProfileSummary.textContent = "没有读取到 Global Resource Build Profile API。";
    el.buildProfileContent.innerHTML = emptyBlock("Authoring Server 未暴露构建 Profile 状态。");
    return;
  }

  const dirtySuffix = state.buildProfileDirty ? "，草稿未保存" : "";
  const profileStateCounts = countBuildProfileStates(getNormalizedItems());
  const deliveryCounts = countBuildProfileDeliveryModes(entries);
  ensureSelectedBundleRuleId();
  const selectedBundle = getSelectedBundleRule();
  el.buildProfileSummary.textContent = `${bundles.length} 个 Bundle，${entries.length} 个 profile entry${dirtySuffix} · internal ${deliveryCounts.internal} / external ${deliveryCounts.external} / excluded ${deliveryCounts.excluded}`;

  if (checkedCount > 0) {
    el.profileBatchBar.classList.remove("hidden");
    el.profileBatchBar.innerHTML = `
      <span class="batch-bar-label">已选择 ${checkedCount} 个资源 · 批量分包 / 字段编辑</span>
      ${renderBundleActionSelect("profileCheckedBundleTarget")}
      <button type="button" data-action="batch-assign-bundle" class="primary-action">批量加入 Bundle</button>
      <button type="button" data-action="batch-clear-bundle" class="secondary">批量移出 Bundle</button>
      <details class="profile-batch-advanced">
        <summary>高级批量字段编辑</summary>
        ${renderBuildProfileBatchEditor()}
      </details>`;
  } else {
    el.profileBatchBar.classList.add("hidden");
    el.profileBatchBar.innerHTML = "";
  }

  el.buildProfileContent.innerHTML = `
    ${state.buildProfileDirty ? `
      <div class="profile-save-callout">
        <div>
          <strong>Profile 草稿未保存</strong>
          <p>保存后 Build 预览和 Unity 构建才会使用这些分包修改。</p>
        </div>
        <button type="button" data-action="save-profile" class="primary-action">保存 Profile</button>
      </div>` : ""}
    ${bundles.length === 0 ? `<div class="profile-empty-guidance"><strong>缺少 Bundle policy</strong><p>请通过 resource-manager.profile-policy 生产者补齐规则。</p></div>` : ""}
    <div class="profile-state-strip">
      ${["notInProfile", "saved", "draftOnly", "removedInDraft", "modifiedInDraft"].map(status => smallBadge(`${formatProfileStatusLabel(status)} ${profileStateCounts[status] || 0}`, toneForBuildProfileStatus(status))).join("")}
      ${smallBadge(`内部构建 ${deliveryCounts.internal}`, deliveryCounts.internal > 0 ? "ok" : "muted")}
      ${smallBadge(`外部配置 ${deliveryCounts.external}`, deliveryCounts.external > 0 ? "warn" : "muted")}
      ${smallBadge(`不构建 ${deliveryCounts.excluded}`, deliveryCounts.excluded > 0 ? "error" : "muted")}
    </div>
    <div class="bundle-profile-layout">
      <aside class="bundle-list-panel">
        <div class="bundle-panel-heading">
          <div>
            <h3>Bundle Policy（只读）</h3>
            <p>${bundles.length} 条规则 · ${countUnassignedInternalEntries()} 个 manual 条目未分配</p>
          </div>
        </div>
        <div class="bundle-rule-list">
          ${bundles.length === 0 ? emptyBlock("没有读取到 Bundle policy。") : bundles.map(rule => renderBundleRuleCard(rule)).join("")}
        </div>
      </aside>
      <section class="bundle-members-panel">
        ${selectedBundle ? renderBundleMembers(selectedBundle) : emptyBlock("选择一个 Bundle policy 后管理 manual 成员资源。")}
      </section>
      <aside class="bundle-settings-panel">
        ${selectedBundle ? renderBundleSettings(selectedBundle) : emptyBlock("Bundle 设置会在这里显示。")}
      </aside>
    </div>
    <details class="profile-resource-editor">
      <summary>当前资源 Profile 字段</summary>
      <div class="profile-editor-grid">
        <div class="detail-card profile-steps-card">
          ${selected ? renderBuildProfileEditor(selected, selectedEntry) : emptyBlock("请先在 Browse 工作区选择一个资源，或从 Browse 点击「加入到 Bundle」跳转至此。")}
        </div>
      </div>
    </details>`;
}

function renderBundleRuleCard(rule) {
  const stats = getBundleRuleStats(rule);
  const active = rule.id === state.selectedBundleRuleId;
  return `
    <button type="button" class="bundle-rule-card${active ? " active" : ""}" data-bundle-action="select-bundle" data-bundle-rule-id="${escapeHtml(rule.id)}">
      <strong>${escapeHtml(rule.id || "(missing id)")}</strong>
      <span>${escapeHtml(rule.bundleName || "-")}</span>
      <span class="bundle-rule-meta">${escapeHtml(rule.compression || "lz4")} · ${escapeHtml(rule.buildTarget || "ActiveBuildTarget")}</span>
      <div class="bundle-card-metrics">
        ${smallBadge(`构建 ${stats.internalCount}`, stats.internalCount > 0 ? "ok" : "warn")}
        ${smallBadge(`跳过 ${stats.externalCount}`, stats.externalCount > 0 ? "warn" : "muted")}
        ${smallBadge(`缺 GUID ${stats.missingUnityGuidCount}`, stats.missingUnityGuidCount > 0 ? "error" : "muted")}
        ${smallBadge(`诊断 ${stats.diagnosticsCount}`, stats.diagnosticsCount > 0 ? "warn" : "muted")}
      </div>
    </button>`;
}

function renderBundleMembers(rule) {
  const members = getBundleRuleMemberRows(rule);
  const filtered = filterBundleMemberRows(members);
  const stats = getBundleRuleStats(rule);
  return `
    <div class="bundle-members-heading">
      <div>
        <h3>${escapeHtml(rule.id)} 成员</h3>
        <p>${filtered.length} / ${members.length} resources · ${stats.dependencyBundleNames.length > 0 ? `depends on ${stats.dependencyBundleNames.join(", ")}` : "no bundle dependencies"}</p>
      </div>
      <div class="bundle-members-toolbar">
        <input data-bundle-member-search value="${escapeHtml(state.bundleMemberSearch)}" placeholder="搜索成员 resourceKey / path">
        <button type="button" data-bundle-action="assign-checked-selected-bundle" class="secondary">添加已勾选资源</button>
        <button type="button" data-action="open-references-issues" class="secondary">检查引用建议</button>
      </div>
    </div>
    <div class="bundle-member-table">
      ${filtered.length === 0 ? emptyBlock("当前 Bundle 没有匹配成员。") : filtered.map(row => renderBundleMemberRow(row)).join("")}
    </div>`;
}

function renderBundleMemberRow(row) {
  const item = row.item;
  const entry = row.entry;
  return `
    <article class="bundle-member-row">
      <div class="bundle-member-main">
        <strong>${escapeHtml(item ? formatResourceDisplayName(item) : pick(pick(entry, "resourceKey"), "id") || row.key)}</strong>
        <span>${escapeHtml(formatProfileResourceKey(pick(entry, "resourceKey")) || row.key)}</span>
      </div>
      <span>${escapeHtml(pick(pick(entry, "resourceKey"), "type", "typeId") || item?.kind || "-")}</span>
      <span>${escapeHtml(pick(pick(entry, "source"), "providerId") || item?.providerId || "-")}</span>
      <span>${escapeHtml(item?.runtimeAvailability || pick(entry, "deliveryMode") || "-")}</span>
      <span>${escapeHtml(asArray(pick(entry, "preloadGroups")).join(", ") || "-")}</span>
      <span>${item ? item.referenceCount : 0} refs · ${item ? item.diagnosticCount : 0} diag</span>
      <button type="button" class="secondary" data-bundle-action="remove-entry-bundle" data-entry-index="${row.index}" ${isManualProfileEntry(entry) ? "" : "disabled"}>${isManualProfileEntry(entry) ? "移出" : "生产者只读"}</button>
    </article>`;
}

function renderBundleSettings(rule) {
  const stats = getBundleRuleStats(rule);
  return `
    <div class="detail-card bundle-settings-card">
      <h3>Bundle 设置</h3>
      <div class="profile-form single-column">
        <label>
          <span>id</span>
          <input data-bundle-field="id" value="${escapeHtml(rule.id || "")}" disabled>
        </label>
        <label>
          <span>bundleName</span>
          <input data-bundle-field="bundleName" value="${escapeHtml(rule.bundleName || "")}" disabled>
        </label>
        <label>
          <span>compression</span>
          <select data-bundle-field="compression" disabled>${selectOptions(["lz4", "uncompressed", "lzma"], rule.compression || "lz4")}</select>
        </label>
        <label>
          <span>buildTarget</span>
          <input data-bundle-field="buildTarget" value="${escapeHtml(rule.buildTarget || "ActiveBuildTarget")}" disabled>
        </label>
        <label class="inline-check">
          <input type="checkbox" data-bundle-field="includeDependencies" ${rule.includeDependencies !== false ? "checked" : ""} disabled>
          <span>include dependencies</span>
        </label>
        <label class="inline-check">
          <input type="checkbox" data-bundle-field="allowEmpty" ${rule.allowEmpty ? "checked" : ""} disabled>
          <span>allow empty</span>
        </label>
      </div>
      <div class="summary-grid compact-summary">
        ${metric("build", stats.internalCount)}
        ${metric("skipped", stats.externalCount)}
        ${metric("no guid", stats.missingUnityGuidCount)}
        ${metric("diagnostics", stats.diagnosticsCount)}
      </div>
      <details class="advanced-filters">
        <summary class="advanced-filters-summary">Advanced match rules</summary>
        <div class="profile-form single-column">
          <label>
            <span>matchLabels</span>
            <input data-bundle-field="matchLabels" value="${escapeHtml(asArray(rule.matchLabels).join(", "))}" disabled>
          </label>
          <label>
            <span>matchDomains</span>
            <input data-bundle-field="matchDomains" value="${escapeHtml(asArray(rule.matchDomains).join(", "))}" disabled>
          </label>
          <label>
            <span>matchPackageIds</span>
            <input data-bundle-field="matchPackageIds" value="${escapeHtml(asArray(rule.matchPackageIds).join(", "))}" disabled>
          </label>
        </div>
      </details>
      <p class="profile-hint">规则由 ${escapeHtml(pick(pick(rule, "producerReceipt"), "producerId") || "resource-manager.profile-policy")} 拥有；这里只显示并供 manual 条目选择。</p>
    </div>`;
}

function renderBuildProfileEditor(item, entry) {
  const inProfile = Boolean(entry);
  const editable = inProfile && isManualProfileEntry(entry);
  const draft = entry || buildProfileEntryFromItem(item);
  const deliveryMode = pick(draft, "deliveryMode") || "internal";
  const isInternal = deliveryMode === "internal";
  const resourceKeyId = stringValue(pick(pick(draft, "resourceKey"), "id"));
  const keyValid = isValidProfileResourceKeyId(resourceKeyId);
  const deliveryState = getDeliveryEntryState(item, entry);
  const highlight = state.profileFieldHighlight;
  const saveStatus = getBuildProfileStatus(item);

  return `
    <div class="profile-status-line">
      ${smallBadge(deliveryState.label, deliveryState.tone)}
      ${smallBadge(saveStatus, toneForBuildProfileStatus(saveStatus))}
      ${smallBadge(inProfile ? "inProfile" : "notInProfile", inProfile ? "ok" : "warn")}
      ${inProfile ? smallBadge(editable ? "manual 可编辑" : `${pick(entry.producerReceipt, "producerId")} 只读`, editable ? "info" : "neutral") : ""}
    </div>
    <section class="profile-step${highlight === "resourceKey" ? " field-highlight" : ""}">
      <h3>步骤 1 · 资源身份</h3>
      <label class="profile-field-inline">
        <span>ResourceKey.id</span>
        <input data-profile-field="resourceKeyId" value="${escapeHtml(resourceKeyId)}" ${editable ? "" : "disabled"}>
        <small class="${keyValid ? "field-ok" : "field-error"}">${keyValid ? "格式有效" : "无效字符：仅允许 a-z、0-9、.、_、-"}</small>
      </label>
      ${renderKeyValueList([
        ["类型", stringValue(pick(pick(draft, "resourceKey"), "type")) || item.kind || "-"],
        ["packageId", stringValue(pick(pick(draft, "resourceKey"), "packageId")) || "-"],
        ["源 provider", stringValue(pick(pick(draft, "source"), "providerId")) || item.providerId || "-"],
        ["unityGuid / assetPath", `${pick(pick(draft, "source"), "unityGuid") || item.unityGuid || "-"} / ${item.unityAssetPath || "-"}`]
      ])}
    </section>
    <section class="profile-step${highlight === "deliveryMode" ? " field-highlight" : ""}">
      <h3>步骤 2 · 交付模式</h3>
      <div class="profile-form">
        <label>
          <span>交付模式</span>
          <select data-profile-field="deliveryMode" ${editable ? "" : "disabled"}>
            ${selectOptions(["internal", "external", "editorOnly", "excluded"], deliveryMode)}
          </select>
        </label>
        <label>
          <span>同键覆盖意图</span>
          <select data-profile-field="overrideIntent" ${editable && ["internal", "external"].includes(deliveryMode) ? "" : "disabled"}>
            ${selectOptions(["forbid", "allow"], pick(draft, "overrideIntent") || "forbid")}
          </select>
        </label>
      </div>
    </section>
    <section class="profile-step${highlight === "bundleRule" ? " field-highlight" : ""}">
      <h3>步骤 3 · Bundle 归属</h3>
      ${isInternal ? `
        <div class="profile-form">
          <label>
            <span>Bundle</span>
            <select data-profile-field="bundleRule" ${editable ? "" : "disabled"}>
              <option value="">未分配 Bundle</option>
              ${getBuildProfileBundleRules().map(rule => `<option value="${escapeHtml(rule.id)}"${rule.id === pick(draft, "bundleRule") ? " selected" : ""}>${escapeHtml(rule.id)} · ${escapeHtml(rule.bundleName || "-")}</option>`).join("")}
            </select>
          </label>
          <label>
            <span>标签</span>
            <input data-profile-field="labels" value="${escapeHtml(asArray(pick(draft, "labels")).join(", "))}" ${editable ? "" : "disabled"}>
          </label>
        </div>` : `<p class="profile-hint">deliveryMode 为 ${escapeHtml(deliveryMode)} 时不进入本地 AssetBundle catalog。external 条目只会通过已编译 CatalogMount、精确 allowlist 和可用 Provider 挂载。</p>`}
    </section>
    <section class="profile-step">
      <h3>步骤 4 · 预加载</h3>
      <div class="profile-form">
        <label>
          <span>预加载组</span>
          <input data-profile-field="preloadGroups" value="${escapeHtml(asArray(pick(draft, "preloadGroups")).join(", "))}" ${editable ? "" : "disabled"}>
        </label>
      </div>
      <p class="profile-hint">失败策略由资源计划分组决定；此处只声明预加载意图。</p>
    </section>
    <section class="profile-step">
      <h3>步骤 5 · 保存状态</h3>
      ${renderKeyValueList([
        ["Profile 状态", saveStatus],
        ["交付结果", deliveryState.label],
        ["完整 resourceKey", formatProfileResourceKey(pick(draft, "resourceKey")) || item.resourceKey || "-"]
      ])}
    </section>`;
}

function renderBuildProfileBatchEditor() {
  const checkedItems = getCheckedItems();
  const editableCount = checkedItems.filter(item => {
    const entry = findDraftBuildProfileEntryForItem(item);
    return entry && isManualProfileEntry(entry);
  }).length;
  const skippedCount = checkedItems.length - editableCount;
  return `
    <p class="profile-hint">只作用于已勾选且已有 Profile 草稿条目的资源；未启用字段和空输入不会覆盖现有值。</p>
    <div class="profile-batch-summary">
      ${smallBadge(`已勾选 ${checkedItems.length}`, checkedItems.length > 0 ? "info" : "warn")}
      ${smallBadge(`可编辑 ${editableCount}`, editableCount > 0 ? "ok" : "warn")}
      ${smallBadge(`跳过 ${skippedCount}`, skippedCount > 0 ? "warn" : "muted")}
    </div>
    <div class="profile-batch-form">
      ${renderBuildProfileBatchField("deliveryMode", "交付模式", "select", ["internal", "external", "editorOnly", "excluded"])}
      ${renderBuildProfileBatchField("overrideIntent", "同键覆盖意图", "select", ["forbid", "allow"])}
      ${renderBuildProfileBatchField("bundleRule", "Bundle", "select", getBuildProfileBundleRules().map(rule => rule.id))}
      ${renderBuildProfileBatchField("preloadGroups", "预加载组")}
      ${renderBuildProfileBatchField("labels", "标签")}
    </div>
    <button class="profile-batch-apply" type="button" data-profile-batch-apply ${editableCount > 0 ? "" : "disabled"}>应用到已勾选草稿条目</button>`;
}

function renderBuildProfileBatchField(field, label, kind = "text", options = []) {
  const control = kind === "select"
    ? `<select data-profile-batch-field="${escapeHtml(field)}" disabled>${selectOptions(options, options[0] || "")}</select>`
    : `<input data-profile-batch-field="${escapeHtml(field)}" value="" placeholder="留空不覆盖" disabled>`;
  return `
    <label class="profile-batch-field">
      <span class="profile-batch-toggle">
        <input type="checkbox" data-profile-batch-enabled="${escapeHtml(field)}">
        <span>${escapeHtml(label)}</span>
      </span>
      ${control}
    </label>`;
}

function renderBundleActionSelect(name) {
  const bundles = getBuildProfileBundleRules();
  const selected = state.selectedBundleRuleId || bundles[0]?.id || "";
  return `
    <select data-bundle-action-select="${escapeHtml(name)}" class="bundle-action-select">
      <option value="">选择现有 Bundle policy...</option>
      ${bundles.map(rule => `<option value="${escapeHtml(rule.id)}"${rule.id === selected ? " selected" : ""}>${escapeHtml(rule.id)} · ${escapeHtml(rule.bundleName || "-")}</option>`).join("")}
    </select>`;
}

function renderBundlePlanSummary(bundles) {
  if (bundles.length === 0) return emptyBlock("Planner 尚未生成内部 bundle。");
  return `<div class="bundle-list">${bundles.slice(0, 8).map(bundle => `
    <article class="bundle-row">
      <div class="bundle-row-head">
        <strong>${escapeHtml(pick(bundle, "bundleName") || "-")}</strong>
        <span>${escapeHtml(pick(bundle, "compression") || "-")} · ${asArray(pick(bundle, "resourceKeys")).length} resources · rule ${escapeHtml(pick(bundle, "bundleId") || "-")}</span>
      </div>
      <small>${pick(bundle, "includeDependencies") === false ? "dependency loading disabled by policy" : "dependency loading enabled"}</small>
      <ul class="bundle-plan-resources">
        ${asArray(pick(bundle, "resourceKeys")).slice(0, 12).map(key => `<li>${escapeHtml(formatProfileResourceKey(key) || pick(key, "id") || "-")}</li>`).join("")}
      </ul>
      ${asArray(pick(bundle, "resourceKeys")).length > 12 ? `<small>另有 ${asArray(pick(bundle, "resourceKeys")).length - 12} 项</small>` : ""}
    </article>`).join("")}</div>`;
}

function renderBundlePlanExternalSummary(plan) {
  const entries = asArray(pick(plan, "entries")).filter(entry => String(pick(entry, "deliveryIntent") || "").toLowerCase() === "external");
  if (entries.length === 0) return "";
  const providerCounts = countPlanEntriesByProvider(entries);
  return `
    <section class="external-plan-panel">
      <div class="bundle-row-head">
        <strong>External configuration references</strong>
        <span>${entries.length} resources · 不进入本地 AssetBundle catalog · 仅通过已编译 CatalogMount 严格挂载</span>
      </div>
      <div class="profile-state-strip">
        ${Object.keys(providerCounts).sort().map(provider => smallBadge(`${provider} ${providerCounts[provider]}`, "warn")).join("")}
      </div>
      <ul class="bundle-plan-resources">
        ${entries.slice(0, 12).map(entry => `<li>${escapeHtml(formatExternalPlanEntry(entry))}</li>`).join("")}
      </ul>
      ${entries.length > 12 ? `<small>另有 ${entries.length - 12} 项 external config references</small>` : ""}
    </section>`;
}


function getBuildProfile() {
  return state.buildProfileDraft || pick(state.buildProfilePayload, "profile") || null;
}

function ensureBuildProfileDraft() {
  if (!state.buildProfileDraft) {
    state.buildProfileDraft = structuredCloneCompat(pick(state.buildProfilePayload, "profile") || {
      schema: "mx.resource-mapping-profile.v2",
      profileId: "global.default",
      catalogId: "global.runtime",
      entries: [],
      bundleRules: [],
      preloadGroups: []
    });
  }
  if (!Array.isArray(state.buildProfileDraft.entries)) state.buildProfileDraft.entries = [];
  if (!Array.isArray(state.buildProfileDraft.bundleRules)) state.buildProfileDraft.bundleRules = [];
  return state.buildProfileDraft;
}

function markBuildProfileDirty() {
  state.buildProfileDirty = true;
}

function getBuildProfileEntries() {
  return asArray(pick(getBuildProfile(), "entries"));
}

function getBuildProfileBundleRules() {
  return asArray(pick(getBuildProfile(), "bundleRules")).filter(Boolean);
}

function findBundleRuleById(id) {
  if (!id) return null;
  return getBuildProfileBundleRules().find(rule => rule.id === id) || null;
}

function getSelectedBundleRule() {
  return findBundleRuleById(state.selectedBundleRuleId) || getBuildProfileBundleRules()[0] || null;
}

function ensureSelectedBundleRuleId() {
  const rules = getBuildProfileBundleRules();
  if (rules.length === 0) {
    state.selectedBundleRuleId = "";
    return;
  }
  if (!state.selectedBundleRuleId || !rules.some(rule => rule.id === state.selectedBundleRuleId)) {
    state.selectedBundleRuleId = rules[0].id || "";
  }
}

function getSavedBuildProfileEntries() {
  return asArray(pick(pick(state.buildProfilePayload, "profile"), "entries"));
}

function getDraftBuildProfileEntries() {
  return asArray(pick(state.buildProfileDraft, "entries"));
}

function getBundlePlan() {
  return pick(state.bundlePlanPayload, "plan") || null;
}

function findBuildProfileEntryForItem(item) {
  return findDraftBuildProfileEntryForItem(item);
}

function findDraftBuildProfileEntryForItem(item) {
  return getDraftBuildProfileEntries().find(entry => profileEntryMatchesItem(entry, item)) || null;
}

function findSavedBuildProfileEntryForItem(item) {
  return getSavedBuildProfileEntries().find(entry => profileEntryMatchesItem(entry, item)) || null;
}

function profileEntryMatchesItem(entry, item) {
  if (asArray(item?.foldedItems).some(peer => peer !== item && profileEntryMatchesItem(entry, peer))) return true;
  const key = pick(entry, "resourceKey") || {};
  const source = pick(entry, "source") || {};
  const entryCanonical = normalizeProfileResourceKey(key);
  const itemCanonical = normalizeProfileResourceKey(buildProfileResourceKeyFromItem(item));
  if (entryCanonical && itemCanonical) return entryCanonical === itemCanonical;

  const sourceRuntimeKey = stringValue(pick(source, "runtimeResourceKey", "providerResourceKey", "packageResourceKey"));
  return Boolean(
    (sourceRuntimeKey && [item.resourceKey, item.runtimeResourceKey, item.providerResourceKey, item.packageResourceKey].includes(sourceRuntimeKey))
    || (source.unityGuid && item.unityGuid && source.unityGuid === item.unityGuid)
    || (source.unityAssetPath && item.unityAssetPath && source.unityAssetPath === item.unityAssetPath)
  );
}

function findBundlePlanEntryForItem(item) {
  const plan = getBundlePlan();
  const candidates = asArray(pick(plan, "entries"));
  return candidates.find(entry => {
    const resourceKey = pick(entry, "resourceKey") || {};
    const key = formatProfileResourceKey(resourceKey);
    const id = stringValue(pick(resourceKey, "id"));
    return matchesItem(item, "", key, id) || id === item.resourceKey || id === item.stableId;
  }) || null;
}

function buildProfileEntryFromItem(item) {
  const resourceKey = buildProfileResourceKeyFromItem(item);
  const detail = getCurrentDetail(item);
  const authoring = pick(detail, "authoring") || {};
  const runtime = pick(detail, "runtime") || {};
  const authoringBindings = asArray(pick(authoring, "providerBindings"));
  const primaryBinding = getPrimaryProviderBinding(authoringBindings.length > 0 ? authoringBindings : item.providerBindings);
  const providerData = pick(runtime, "providerData") || pick(primaryBinding, "providerData") || item.metadata || {};
  const preloadGroups = getRuntimePreloadGroupsForItem(item);
  const domainLabel = item.kind ? `domain.${normalizeLabelSegment(item.kind)}` : "domain.resource";
  const bundleHint = preloadGroups.length > 0
    ? normalizeLabelSegment(preloadGroups[0])
    : item.usage ? normalizeLabelSegment(item.usage) : item.kind ? normalizeLabelSegment(item.kind) : "misc";
  const deliveryMode = inferDeliveryModeForItem(item);
  const isInternalDelivery = deliveryMode === "internal";
  return {
    resourceKey,
    source: {
      providerId: item.providerId || "unknown",
      unityAssetPath: item.unityAssetPath || "",
      unityGuid: item.unityGuid || "",
      runtimeResourceKey: stringValue(pick(runtime, "runtimeResourceKey", "resourceKey")) || item.runtimeResourceKey || item.resourceKey || "",
      externalSourcePath: stringValue(pick(authoring, "sourcePath")) || item.sourcePath || "",
      providerData
    },
    labels: [domainLabel],
    bundleRule: "",
    deliveryMode,
    overrideIntent: "forbid",
    bundleGroupHint: isInternalDelivery ? bundleHint : "",
    preloadGroups,
    dependencies: [],
    providerData: {},
    runtimeLoadable: isRuntimeLoadable(item) || isRuntimeRequired(item),
    editorOnly: String(item.runtimeAvailability || "").toLowerCase().includes("editor")
  };
}

function buildDraftBuildProfileEntryForItem(item) {
  const savedEntry = findSavedBuildProfileEntryForItem(item);
  return savedEntry ? structuredCloneCompat(savedEntry) : buildProfileEntryFromItem(item);
}

function buildProfileResourceKeyFromItem(item) {
  const detail = getCurrentDetail(item);
  const runtime = pick(detail, "runtime") || {};
  const planResource = getPrimaryRuntimePlanResource(item);
  const providerData = pick(runtime, "providerData") || item.metadata || {};
  const parsed = parseProfileResourceKey(item.resourceKey || item.runtimeResourceKey || item.providerResourceKey || item.packageResourceKey || "");
  const type = stringValue(pick(planResource, "typeId", "type")) || parsed.type || stringValue(pick(runtime, "assetType")) || item.kind || "Object";
  const id = firstValidProfileResourceKeyId(
    stringValue(pick(planResource, "resourceKey", "id")),
    stringValue(pick(runtime, "resourceKey", "runtimeResourceKey", "providerResourceKey", "packageResourceKey")),
    parsed.id,
    stringValue(pick(providerData, "runtimeResourceKey", "providerResourceKey", "packageResourceKey")),
    item.runtimeResourceKey,
    item.providerResourceKey,
    item.packageResourceKey,
    item.stableId,
    item.libraryItemId,
    item.displayName,
    item.resourceId
  );
  return {
    id,
    type,
    typeId: type,
    variant: stringValue(pick(planResource, "variant")) || parsed.variant || stringValue(pick(providerData, "variant")),
    packageId: stringValue(pick(planResource, "packageId")) || parsed.packageId || stringValue(pick(providerData, "packageId")) || getSelectedPackageLabel()
  };
}

function inferDeliveryModeForItem(item) {
  if (!isRuntimeLoadable(item)) return "excluded";
  if (["characterPackage", "runtimeCatalog", "fmod", "externalImportStaging"].includes(item.providerId)) return "external";
  if (isRuntimeRequired(item)) return "internal";
  return "internal";
}

function canAssignItemToInternalBundle(item) {
  if (!item) return false;
  if (!isRuntimeLoadable(item)) return false;
  if (String(item.runtimeAvailability || "").toLowerCase().includes("editor")) return false;
  if (["characterPackage", "runtimeCatalog", "fmod", "externalImportStaging"].includes(item.providerId)) return false;
  return ["", "unityAssetDatabase", "unityProjectAssets", "generatedAssets", "resourceMapping"].includes(item.providerId || "");
}

function describeBundleAssignBlocker(item) {
  if (!item) return "未选择资源。";
  if (!isRuntimeLoadable(item)) return "资源不是 runtime-loadable。";
  if (String(item.runtimeAvailability || "").toLowerCase().includes("editor")) return "资源是 editor-only。";
  if (["characterPackage", "runtimeCatalog", "fmod", "externalImportStaging"].includes(item.providerId)) {
    return `${item.providerId} 是 external/source provider，不能直接进入 internal Player Bundle。`;
  }
  return "资源 provider 不是 internal bundle 可构建来源。";
}

function getRuntimePreloadGroupsForItem(item) {
  return Array.from(new Set(asArray(pick(getCurrentDetail(item), "plans"))
    .map(plan => stringValue(pick(plan, "groupName", "group", "preloadPolicy")))
    .filter(Boolean)));
}

function getPrimaryRuntimePlanResource(item) {
  for (const plan of asArray(pick(getCurrentDetail(item), "plans"))) {
    const resource = pick(plan, "resource") || plan;
    if (pick(resource, "resourceKey", "id")) return resource;
  }
  return {};
}

function isRuntimeRequired(item) {
  const detail = getCurrentDetail(item);
  if (asArray(pick(detail, "plans")).some(plan => pick(plan, "required") === true)) return true;
  return asArray(pick(detail, "references")).some(reference => pick(reference, "isRequiredAtRuntime") === true);
}

function getBuildProfileStatus(item) {
  const savedEntry = findSavedBuildProfileEntryForItem(item);
  const draftEntry = findDraftBuildProfileEntryForItem(item);
  if (!savedEntry && !draftEntry) return "notInProfile";
  if (!savedEntry && draftEntry) return "draftOnly";
  if (savedEntry && !draftEntry) return "removedInDraft";
  if (!profileEntriesEqual(savedEntry, draftEntry)) return "modifiedInDraft";
  return "saved";
}

function countBuildProfileStates(items) {
  const counts = {
    notInProfile: 0,
    saved: 0,
    draftOnly: 0,
    removedInDraft: 0,
    modifiedInDraft: 0
  };
  for (const item of items) {
    const status = getBuildProfileStatus(item);
    counts[status] = (counts[status] || 0) + 1;
  }
  return counts;
}

function countBuildProfileDeliveryModes(entries) {
  const counts = { internal: 0, external: 0, excluded: 0 };
  for (const entry of asArray(entries)) {
    const mode = pick(entry, "deliveryMode") || "internal";
    if (mode === "excluded" || mode === "editorOnly" || pick(entry, "editorOnly") === true || pick(entry, "runtimeLoadable") === false) {
      counts.excluded++;
    } else if (mode === "external") {
      counts.external++;
    } else {
      counts.internal++;
    }
  }
  return counts;
}

function countPlanEntriesByProvider(entries) {
  const counts = {};
  for (const entry of asArray(entries)) {
    const mount = stringValue(pick(entry, "catalogMountId")) || "(missing mount)";
    counts[mount] = (counts[mount] || 0) + 1;
  }
  return counts;
}

function formatExternalPlanEntry(entry) {
  const key = formatProfileResourceKey(pick(entry, "resourceKey")) || "-";
  const mount = stringValue(pick(entry, "catalogMountId")) || "(missing mount)";
  return `${key} · CatalogMount ${mount} · order ${pick(entry, "effectiveMountOrder") ?? "-"}`;
}

function getBundleRuleMemberRows(rule) {
  if (!rule) return [];
  const items = getBrowserItems();
  return getDraftBuildProfileEntries()
    .map((entry, index) => ({ entry, index, item: items.find(item => profileEntryMatchesItem(entry, item)) || null }))
    .filter(row => pick(row.entry, "bundleRule") === rule.id)
    .map(row => ({
      ...row,
      key: formatProfileResourceKey(pick(row.entry, "resourceKey")) || pick(pick(row.entry, "resourceKey"), "id") || String(row.index)
    }));
}

function filterBundleMemberRows(rows) {
  const needle = state.bundleMemberSearch.trim().toLowerCase();
  if (!needle) return rows;
  return rows.filter(row => {
    const item = row.item;
    const text = [
      row.key,
      item?.displayName,
      item?.resourceKey,
      item?.sourcePath,
      item?.unityAssetPath,
      pick(pick(row.entry, "source"), "unityAssetPath")
    ].filter(Boolean).join(" ").toLowerCase();
    return text.includes(needle);
  });
}

function getBundleRuleStats(rule) {
  const rows = getBundleRuleMemberRows(rule);
  const planBundle = getBundlePlanBundleForRule(rule);
  const externalCount = rows.filter(row => pick(row.entry, "deliveryMode") !== "internal").length;
  const plannedInternalCount = asArray(pick(planBundle, "resourceKeys")).length;
  const missingUnityGuidCount = rows.filter(row => {
    const provider = pick(pick(row.entry, "source"), "providerId");
    return provider === "unityAssetDatabase" && !pick(pick(row.entry, "source"), "unityGuid");
  }).length;
  const diagnosticsCount = rows.reduce((sum, row) => sum + (row.item?.diagnosticCount || 0), 0)
    + asArray(pick(planBundle, "diagnostics")).length;
  return {
    memberCount: rows.length,
    internalCount: Math.max(rows.length - externalCount, plannedInternalCount),
    externalCount,
    missingUnityGuidCount,
    diagnosticsCount,
    dependencyBundleNames: []
  };
}

function getBundlePlanBundleForRule(rule) {
  if (!rule) return null;
  const bundles = asArray(pick(getBundlePlan(), "bundles"));
  return bundles.find(bundle => pick(bundle, "bundleId") === rule.id || pick(bundle, "bundleName") === rule.bundleName) || null;
}

function countUnassignedInternalEntries() {
  const rules = new Set(getBuildProfileBundleRules().map(rule => rule.id));
  return getDraftBuildProfileEntries().filter(entry => {
    if (!isManualProfileEntry(entry)) return false;
    const mode = pick(entry, "deliveryMode") || "internal";
    if (mode !== "internal") return false;
    const bundleRule = pick(entry, "bundleRule") || "";
    return !bundleRule || !rules.has(bundleRule);
  }).length;
}

function handleBundleProfileAction(button) {
  const action = button.dataset.bundleAction;
  if (action === "select-bundle") {
    state.selectedBundleRuleId = button.dataset.bundleRuleId || "";
    state.bundleMemberSearch = "";
    render();
    return;
  }
  if (action === "assign-checked-selected-bundle") {
    assignCheckedToBundle(state.selectedBundleRuleId);
    return;
  }
  if (action === "remove-entry-bundle") {
    clearEntryBundleAssignment(Number(button.dataset.entryIndex));
    return;
  }
}

function clearEntryBundleAssignment(index) {
  const profile = getBuildProfile();
  const entry = profile?.entries?.[index];
  if (!entry || !isManualProfileEntry(entry)) {
    state.lastActionMessage = "生产者拥有的条目不能在 Resource Manager 中修改。";
    render();
    return;
  }
  entry.bundleRule = "";
  markBuildProfileDirty();
  state.lastActionMessage = "Removed resource from current Bundle.";
  render();
}

function toneForBuildProfileStatus(status) {
  if (status === "saved") return "ok";
  if (status === "draftOnly" || status === "modifiedInDraft") return "info";
  if (status === "removedInDraft") return "warn";
  if (status === "conflict" || status === "missing") return "error";
  return "neutral";
}

function profileEntriesEqual(left, right) {
  return stableJson(left || {}) === stableJson(right || {});
}
