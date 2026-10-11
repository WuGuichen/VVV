// Global reaction-configuration workbench embedded in the combined-weapon workspace.

const REACTION_MATRIX = [
  { key: "death", label: "死亡" },
  { key: "cohesionBreak", label: "破防" },
  { key: "normal.high", label: "普通 · 上段" },
  { key: "normal.mid", label: "普通 · 中段" },
  { key: "normal.low", label: "普通 · 下段" },
  { key: "strong.high", label: "强冲击 · 上段" },
  { key: "strong.mid", label: "强冲击 · 中段" },
  { key: "strong.low", label: "强冲击 · 下段" }
];

function canonicalReactionValue(value) {
  if (Array.isArray(value)) return value.map(canonicalReactionValue);
  if (!value || typeof value !== "object") return value;
  return Object.keys(value).sort().reduce((result, key) => {
    result[key] = canonicalReactionValue(value[key]);
    return result;
  }, {});
}

function classifyReactionSlot(slot) {
  const id = String(slot?.slotId || "").toLowerCase();
  const trigger = String(slot?.trigger || "").toLowerCase();
  const band = String(slot?.pressureBand || "").toLowerCase();
  const group = String(slot?.reactionGroupId || "").toLowerCase();
  const all = `${id} ${trigger} ${band} ${group}`;
  if (all.includes("death")) return "death";
  if (all.includes("cohesion") || all.includes("break")) return "cohesionBreak";
  const strength = all.includes("strong") || all.includes("heavy") || all.includes("highimpact") ? "strong"
    : all.includes("normal") || all.includes("light") ? "normal" : "";
  const height = all.includes("upper") || all.includes("high") ? "high"
    : all.includes("lower") || all.includes("low") ? "low"
      : all.includes("middle") || all.includes("mid") ? "mid" : "";
  return strength && height ? `${strength}.${height}` : "custom";
}

function groupReactionSlots(slots = []) {
  const known = new Map(REACTION_MATRIX.map(item => [item.key, []]));
  const custom = [];
  for (const slot of slots) {
    const key = classifyReactionSlot(slot);
    if (known.has(key)) known.get(key).push(slot);
    else custom.push(slot);
  }
  return { known, custom };
}

function resolveReactionActionRole(configuration, actionRole) {
  if (!actionRole) return { state: "missing", label: "未填写 ActionRole" };
  const actions = configuration?.actions || [];
  const bindings = configuration?.reactionActionBindings || [];
  const matches = bindings.filter(binding => binding?.actionRole === actionRole);
  if (matches.length !== 1) return { state: "missing", label: matches.length ? "ActionRole 重复绑定" : "ActionRole 未绑定" };
  const actionId = matches[0]?.actionId;
  if (!actionId) return { state: "missing", label: "ActionRole 未绑定" };
  if (!actions.some(action => String(action?.actionId) === String(actionId))) return { state: "missing", label: "绑定的 Action 不存在" };
  return { state: "resolved", label: String(actionId) };
}

function getReactionRecordStatus(item, baseline = []) {
  const id = item?.reactionConfigurationId || "";
  const original = baseline.find(value => value?.reactionConfigurationId === id);
  if (!original) return "new";
  return JSON.stringify(canonicalReactionValue(item)) === JSON.stringify(canonicalReactionValue(original)) ? "clean" : "modified";
}

function getReactionConfigurations() {
  state.weaponLibrary ||= { schemaVersion: "2.0" };
  state.weaponLibrary.reactionConfigurations = Array.isArray(state.weaponLibrary.reactionConfigurations)
    ? state.weaponLibrary.reactionConfigurations : [];
  return state.weaponLibrary.reactionConfigurations;
}

function getActiveReactionWorkbenchConfiguration() {
  const items = getReactionConfigurations();
  return items.find(item => item?.reactionConfigurationId === state.activeReactionConfigurationWorkbenchId)
    || items.find(item => item?.reactionConfigurationId === getActiveCombinedWorkbenchConfiguration()?.reactionConfigurationId)
    || items[0] || null;
}

function markReactionLibraryDirty(message = "") {
  state.weaponLibraryDirty = true;
  state.globalConfigurationReferences = {};
  if (message) state.message = message;
}

function updateReactionConfigurationField(field, value) {
  const item = getActiveReactionWorkbenchConfiguration();
  if (!item || !["displayName", "description", "defaultSlotId"].includes(field)) return;
  item[field] = value;
  markReactionLibraryDirty();
  render();
}

function updateReactionSlotField(field, value, inputType = "text") {
  const item = getActiveReactionWorkbenchConfiguration();
  const slot = (item?.slots || []).find(value => value?.slotId === state.activeReactionSlotId);
  if (!slot || field === "slotId") return;
  if (["durationFrames", "priority"].includes(field)) slot[field] = Number(value || 0);
  else if (["minImpactForce", "maxImpactForce"].includes(field)) slot[field] = value === "" ? null : Number(value);
  else if (field === "required") slot[field] = inputType === "checkbox" ? Boolean(value) : value === "true";
  else slot[field] = value;
  markReactionLibraryDirty();
  render();
}

function uniqueReactionId(items, field, base) {
  const used = new Set(items.map(item => item?.[field]));
  let candidate = base;
  let index = 1;
  while (used.has(candidate)) candidate = `${base}.${index++}`;
  return candidate;
}

function createReactionConfiguration(copyCurrent = false) {
  const current = getActiveReactionWorkbenchConfiguration();
  const defaultId = uniqueReactionId(getReactionConfigurations(), "reactionConfigurationId", copyCurrent && current ? `${current.reactionConfigurationId}.copy` : "reaction.new");
  const id = window.prompt("稳定 ID（创建后不可修改）", defaultId)?.trim();
  if (!id) return;
  if (getReactionConfigurations().some(item => item?.reactionConfigurationId === id)) {
    state.message = `受击配置 ID 已存在：${id}`;
    renderShellStatus();
    return;
  }
  const item = copyCurrent && current ? clone(current) : {
    reactionConfigurationId: id,
    displayName: "新受击配置",
    description: "",
    defaultSlotId: "",
    slots: []
  };
  item.reactionConfigurationId = id;
  item.displayName = window.prompt("显示名称", copyCurrent && current ? `${current.displayName || current.reactionConfigurationId} 副本` : "新受击配置")?.trim() || item.displayName;
  getReactionConfigurations().push(item);
  state.activeReactionConfigurationWorkbenchId = id;
  state.activeReactionSlotId = item.slots?.[0]?.slotId || "";
  markReactionLibraryDirty(`已创建受击配置 ${id}，保存全局库后生效。`);
  render();
}

async function deleteReactionConfiguration() {
  const item = getActiveReactionWorkbenchConfiguration();
  if (!item) return;
  const key = `reaction:${item.reactionConfigurationId}`;
  let referenceState = state.globalConfigurationReferences?.[key];
  if (!referenceState) {
    referenceState = await readJson(`/api/weapon-library/references?configurationType=reaction&configurationId=${encodeURIComponent(item.reactionConfigurationId)}`, null);
    state.globalConfigurationReferences[key] = referenceState;
  }
  if (!referenceState || (referenceState.references || []).length) {
    state.message = !referenceState ? "引用状态未知，禁止删除。" : "此受击配置仍被角色组合引用，禁止删除。";
    renderShellStatus();
    return;
  }
  if (!window.confirm(`删除受击配置 ${item.displayName || item.reactionConfigurationId}？`)) return;
  state.weaponLibrary.reactionConfigurations = getReactionConfigurations().filter(value => value !== item);
  state.activeReactionConfigurationWorkbenchId = state.weaponLibrary.reactionConfigurations[0]?.reactionConfigurationId || "";
  state.activeReactionSlotId = "";
  markReactionLibraryDirty();
  render();
}

function getReactionSemanticTemplate(key) {
  if (key === "death") return { id: "death", displayName: "死亡", trigger: "Death", pressureBand: "", actionRole: "reaction.death" };
  if (key === "cohesionBreak") return { id: "cohesionBreak", displayName: "破防", trigger: "CohesionBreak", pressureBand: "", actionRole: "reaction.cohesionBreak" };
  const [strength, height] = String(key || "").split(".");
  if (!["normal", "strong"].includes(strength) || !["high", "mid", "low"].includes(height)) return null;
  return {
    id: `hit.${strength}.${height}`,
    displayName: `${strength === "strong" ? "强冲击" : "普通"} · ${{ high: "上段", mid: "中段", low: "下段" }[height]}`,
    trigger: "Damage",
    pressureBand: `${strength}.${height}`,
    actionRole: `reaction.hit.${strength}.${height}`
  };
}

function getReactionSlotPresetState(slot) {
  if (!slot) return { kind: "custom", semanticKey: "", template: null, conflicts: [] };
  const semanticKey = REACTION_MATRIX.find(item => getReactionSemanticTemplate(item.key)?.id === slot.slotId)?.key || "";
  if (!semanticKey) return { kind: "custom", semanticKey: "", template: null, conflicts: [] };
  const template = getReactionSemanticTemplate(semanticKey);
  const conflicts = ["trigger", "pressureBand", "actionRole"].filter(field => String(slot[field] || "") !== String(template[field] || ""));
  return { kind: conflicts.length ? "conflict" : "preset", semanticKey, template, conflicts };
}

function createReactionPresetSlot(slots, semanticKey) {
  const template = getReactionSemanticTemplate(semanticKey);
  if (!template) return { state: "invalid", slot: null, template: null };
  const existing = (slots || []).find(slot => slot?.slotId === template.id);
  if (existing) {
    const presetState = getReactionSlotPresetState(existing);
    return { state: presetState.kind === "conflict" ? "conflict" : "existing", slot: existing, template, conflicts: presetState.conflicts };
  }
  return {
    state: "created",
    template,
    slot: {
      slotId: template.id,
      displayName: template.displayName,
      trigger: template.trigger,
      pressureBand: template.pressureBand,
      reactionGroupId: "",
      minImpactForce: null,
      maxImpactForce: null,
      actionRole: template.actionRole,
      durationFrames: 0,
      priority: 0,
      resourceKey: "",
      preloadPolicy: "AnimationWarmup",
      required: false,
      resourceSelection: null
    }
  };
}

function createReactionSlot(copyCurrent = false, semanticKey = "") {
  const item = getActiveReactionWorkbenchConfiguration();
  if (!item) return;
  item.slots = Array.isArray(item.slots) ? item.slots : [];
  const current = item.slots.find(slot => slot?.slotId === state.activeReactionSlotId);
  const semantic = !copyCurrent ? getReactionSemanticTemplate(semanticKey) : null;
  if (semantic) {
    const result = createReactionPresetSlot(item.slots, semanticKey);
    if (!result.slot) return;
    state.activeReactionSlotId = result.slot.slotId;
    if (result.state === "created") {
      item.slots.push(result.slot);
      if (!item.defaultSlotId || !item.slots.some(value => value?.slotId === item.defaultSlotId)) item.defaultSlotId = result.slot.slotId;
      markReactionLibraryDirty(`已创建预设 ${result.slot.displayName}（${result.slot.slotId}）；补齐资源和有效组合动作映射后即可使用。`);
    } else if (result.state === "conflict") {
      state.message = `预设 ID ${result.slot.slotId} 已存在，但 ${result.conflicts.join("、")} 与标准语义冲突；请在当前 Slot 的冲突提示中恢复预设值。`;
    } else {
      state.message = `预设 ${result.slot.displayName || result.slot.slotId} 已存在，已定位到该 Slot。`;
    }
    render();
    return;
  }
  const baseId = copyCurrent && current ? `${current.slotId}.copy` : "reaction.custom";
  const id = window.prompt("Slot 稳定 ID（创建后不可修改）", uniqueReactionId(item.slots, "slotId", baseId))?.trim();
  if (!id || item.slots.some(slot => slot?.slotId === id)) return;
  const slot = copyCurrent && current ? clone(current) : {
    slotId: id,
    displayName: "新受击槽位",
    trigger: "Damage",
    pressureBand: "",
    reactionGroupId: "",
    minImpactForce: null,
    maxImpactForce: null,
    actionRole: "",
    durationFrames: 0,
    priority: 0,
    resourceKey: "",
    preloadPolicy: "AnimationWarmup",
    required: false,
    resourceSelection: null
  };
  slot.slotId = id;
  if (copyCurrent) slot.displayName = `${slot.displayName || current.slotId} 副本`;
  item.slots.push(slot);
  if (!item.defaultSlotId || !(item.slots || []).some(value => value?.slotId === item.defaultSlotId)) item.defaultSlotId = id;
  state.activeReactionSlotId = id;
  markReactionLibraryDirty(`已创建 ${slot.displayName || id}；补齐资源和有效组合动作映射后即可使用。`);
  render();
}

function restoreReactionSlotPreset() {
  const item = getActiveReactionWorkbenchConfiguration();
  const slot = (item?.slots || []).find(value => value?.slotId === state.activeReactionSlotId);
  const preset = getReactionSlotPresetState(slot);
  if (!slot || !preset.template || preset.kind !== "conflict") return;
  slot.trigger = preset.template.trigger;
  slot.pressureBand = preset.template.pressureBand;
  slot.actionRole = preset.template.actionRole;
  markReactionLibraryDirty(`已恢复 ${slot.slotId} 的标准 Trigger、PressureBand 和 ActionRole。`);
  render();
}

function deleteReactionSlot() {
  const item = getActiveReactionWorkbenchConfiguration();
  const slot = (item?.slots || []).find(value => value?.slotId === state.activeReactionSlotId);
  if (!item || !slot || !window.confirm(`删除 Slot ${slot.displayName || slot.slotId}？`)) return;
  item.slots = item.slots.filter(value => value !== slot);
  const deletedDefault = item.defaultSlotId === slot.slotId;
  if (deletedDefault) item.defaultSlotId = "";
  state.activeReactionSlotId = item.slots[0]?.slotId || "";
  markReactionLibraryDirty(deletedDefault ? "已删除默认 Slot；请显式选择新的默认 Slot。" : "");
  render();
}

function renderReactionConfigurationWorkbench() {
  const items = getReactionConfigurations();
  const query = String(state.reactionConfigurationSearchQuery || "").toLowerCase();
  const visible = items.filter(item => `${item?.reactionConfigurationId || ""} ${item?.displayName || ""}`.toLowerCase().includes(query));
  const active = getActiveReactionWorkbenchConfiguration();
  if (active && !state.activeReactionConfigurationWorkbenchId) state.activeReactionConfigurationWorkbenchId = active.reactionConfigurationId;
  if (active && !(active.slots || []).some(slot => slot?.slotId === state.activeReactionSlotId)) {
    const defaultExists = (active.slots || []).some(slot => slot?.slotId === active.defaultSlotId);
    state.activeReactionSlotId = defaultExists ? active.defaultSlotId : active.slots?.[0]?.slotId || "";
  }
  const currentCombination = getActiveCombinedWorkbenchConfiguration();
  const defaultSlotMissing = Boolean(active?.defaultSlotId) && !(active?.slots || []).some(slot => slot?.slotId === active.defaultSlotId);
  return `<div class="reaction-workbench">
    <header class="reaction-workbench-head"><div><button type="button" class="reaction-back-button" data-reaction-action="back">← 返回组合配置</button><div><strong>受击配置工作台</strong><span>编辑项目级共享受击规则 · Global Library 6.0</span></div></div><div><span class="sync-badge ${state.weaponLibraryDirty ? "warn" : "ok"}">${state.weaponLibraryDirty ? "有未保存修改" : "全局库已保存"}</span><button type="button" data-reaction-action="new">新建配置</button><button type="button" class="primary" data-reaction-action="save"${state.weaponLibraryDirty ? "" : " disabled"}>保存全局库</button></div></header>
    <section class="reaction-workbench-intro"><div><span class="combined-eyebrow">当前组合</span><strong>${escapeHtml(currentCombination?.displayName || currentCombination?.combinedWeaponConfigurationId || "未选择")}</strong><code>${escapeHtml(currentCombination?.reactionConfigurationId || "未绑定受击配置")}</code></div><p>${defaultSlotMissing ? `<strong class="warn-text">默认 Slot ${escapeHtml(active.defaultSlotId)} 已失效。</strong> 请在右侧选择有效默认 Slot；当前已先打开第一条可用 Slot。` : "先从左侧选择共享配置，再在语义矩阵中选择 Slot。修改会影响所有引用该配置的公共组合；具体 Action 使用公共默认，角色可整组覆盖。"}</p><span>revision ${escapeHtml((state.weaponLibraryRevision || "missing").slice(0, 12))}</span></section>
    <div class="reaction-workbench-layout"><aside class="reaction-master-list"><div class="reaction-master-list-head"><div><strong>受击配置</strong><span>${items.length} 条全局配置</span></div><button type="button" data-reaction-action="new">＋</button></div><label class="reaction-search"><span>搜索</span><input data-reaction-search value="${escapeHtml(state.reactionConfigurationSearchQuery || "")}" placeholder="按名称或稳定 ID"></label><div class="single-weapon-list">${visible.map(renderReactionConfigurationListItem).join("") || `<div class="empty compact">没有匹配的受击配置。</div>`}</div></aside><section class="reaction-detail">${active ? renderReactionConfigurationDetail(active) : `<div class="empty">新建一条受击配置后即可开始编辑。</div>`}</section></div>
  </div>`;
}

function renderReactionConfigurationListItem(item) {
  const id = item?.reactionConfigurationId || "";
  const refs = state.globalConfigurationReferences?.[`reaction:${id}`];
  globalThis.requestGlobalConfigurationReferences?.("reaction", id);
  const status = getReactionRecordStatus(item, state.reactionConfigurationLibraryBaseline || []);
  return `<button type="button" class="single-weapon-list-item${id === state.activeReactionConfigurationWorkbenchId ? " active" : ""}" data-reaction-action="selectConfiguration" data-reaction-id="${escapeHtml(id)}"><span class="single-weapon-list-title"><strong>${escapeHtml(item.displayName || id)}</strong><span class="sync-badge ${status === "clean" ? "ok" : "warn"}">${status === "new" ? "新增" : status === "modified" ? "已修改" : "已保存"}</span></span><code>${escapeHtml(id)}</code><span class="single-weapon-list-summary"><span>${(item.slots || []).length} Slot</span><span>${refs ? `${(refs.references || []).length} 个引用` : "引用加载中"}</span></span></button>`;
}

function renderReactionConfigurationDetail(item) {
  const slots = item.slots || [];
  const grouped = groupReactionSlots(slots);
  const activeSlot = slots.find(slot => slot?.slotId === state.activeReactionSlotId) || null;
  requestReactionReferenceSnapshots(item.reactionConfigurationId);
  return `<div class="single-weapon-detail-head"><div><strong>${escapeHtml(item.displayName || item.reactionConfigurationId)}</strong><code>${escapeHtml(item.reactionConfigurationId)}</code></div><div class="single-weapon-detail-actions"><button type="button" data-reaction-action="copyConfiguration">复制</button><button type="button" class="danger" data-reaction-action="deleteConfiguration">删除</button></div></div>
    <article class="single-weapon-form-section"><h4>身份与默认槽位</h4><div class="single-weapon-field-grid"><label><span>稳定 ID</span><input readonly value="${escapeHtml(item.reactionConfigurationId)}"></label><label><span>显示名称</span><input data-reaction-config-field="displayName" value="${escapeHtml(item.displayName || "")}"></label><label class="wide"><span>说明</span><input data-reaction-config-field="description" value="${escapeHtml(item.description || "")}"></label><label><span>默认 Slot</span><select data-reaction-config-field="defaultSlotId"><option value="">未设置</option>${slots.map(slot => `<option value="${escapeHtml(slot.slotId)}"${slot.slotId === item.defaultSlotId ? " selected" : ""}>${escapeHtml(slot.displayName || slot.slotId)}</option>`).join("")}</select></label></div></article>
    <article class="single-weapon-form-section"><div class="reaction-section-head"><h4>语义矩阵</h4><div><button type="button" data-reaction-action="newSlot">新建 Slot</button><button type="button" data-reaction-action="copySlot"${activeSlot ? "" : " disabled"}>复制当前 Slot</button></div></div><div class="reaction-semantic-matrix">${REACTION_MATRIX.map(definition => renderReactionMatrixCell(definition, grouped.known.get(definition.key) || [])).join("")}</div>${grouped.custom.length ? `<h5>自定义槽位</h5><div class="reaction-custom-slots">${grouped.custom.map(renderReactionSlotChip).join("")}</div>` : `<p class="field-help">没有自定义槽位；未识别的语义会原样保留在这里。</p>`}</article>
    ${activeSlot ? renderReactionSlotDetail(item, activeSlot) : `<article class="single-weapon-form-section"><div class="empty compact">选择矩阵或自定义列表中的 Slot 进行编辑。</div></article>`}
    ${renderReactionReferenceMappings(item)}
    <details class="global-quick-editor reaction-audit"><summary><strong>只读 JSON 审计</strong><span>用于复制和核对，不可写</span></summary><pre>${escapeHtml(JSON.stringify(item, null, 2))}</pre></details>`;
}

function requestReactionReferenceSnapshots(configurationId) {
  const references = state.globalConfigurationReferences?.[`reaction:${configurationId}`]?.references || [];
  state.reactionReferenceSnapshots ||= {};
  for (const packageRelative of new Set(references.map(item => item?.packageRelative).filter(Boolean))) {
    if (Object.prototype.hasOwnProperty.call(state.reactionReferenceSnapshots, packageRelative)) continue;
    state.reactionReferenceSnapshots[packageRelative] = null;
    readJson(`/api/character/weapon-configurations?package=${encodeURIComponent(packageRelative)}`, null).then(payload => {
      state.reactionReferenceSnapshots[packageRelative] = payload || false;
      if (state.combinedWeaponSubWorkspace === "reaction") render();
    });
  }
}

function renderReactionReferenceMappings(configuration) {
  const references = state.globalConfigurationReferences?.[`reaction:${configuration.reactionConfigurationId}`]?.references || [];
  if (!references.length) return `<article class="single-weapon-form-section"><h4>引用组合 ActionRole 状态</h4><p class="field-help">当前没有已保存的角色组合引用。</p></article>`;
  const unique = new Map();
  for (const reference of references) unique.set(`${reference.packageRelative}\n${reference.combinedWeaponConfigurationId}`, reference);
  const currentPackage = state.packageRelative || "";
  const rows = [...unique.values()].map(reference => {
    let actionConfiguration = null;
    let loading = false;
    if (reference.packageRelative === currentPackage) {
      const draft = (state.package?.applicationConfig?.combinedWeaponConfigurations || []).find(item =>
        item?.combinedWeaponConfigurationId === reference.combinedWeaponConfigurationId);
      actionConfiguration = draft?.actionConfiguration || null;
    } else {
      const snapshot = state.reactionReferenceSnapshots?.[reference.packageRelative];
      loading = snapshot === null || snapshot === undefined;
      actionConfiguration = snapshot?.combinedWeaponConfigurations?.find(item =>
        item?.combinedWeaponConfigurationId === reference.combinedWeaponConfigurationId)?.actionConfiguration || null;
    }
    const roles = (configuration.slots || []).map(slot => resolveReactionActionRole(actionConfiguration, slot.actionRole));
    const resolved = roles.filter(item => item.state === "resolved").length;
    const label = loading ? "加载中" : `${resolved}/${roles.length} 已解析`;
    return `<tr><td>${escapeHtml(reference.packageRelative || "角色")}</td><td>${escapeHtml(reference.combinedWeaponConfigurationId || "组合")}</td><td><span class="sync-badge ${!loading && resolved === roles.length ? "ok" : "warn"}">${label}</span></td></tr>`;
  }).join("");
  return `<article class="single-weapon-form-section"><h4>引用组合 ActionRole 状态</h4><div class="single-weapon-overview-table-wrap"><table class="single-weapon-overview-table"><thead><tr><th>角色</th><th>组合</th><th>映射</th></tr></thead><tbody>${rows}</tbody></table></div><p class="field-help">这里核对最终有效映射；actionId 来自公共动作默认或角色动作覆盖。</p></article>`;
}

function renderReactionMatrixCell(definition, slots) {
  return `<section class="${slots.length ? "configured" : "empty"}"><header><span>${escapeHtml(definition.label)}</span><small>${slots.length}</small></header>${slots.length ? slots.map(renderReactionSlotChip).join("") : `<button type="button" class="reaction-matrix-empty" data-reaction-action="newSemanticSlot" data-reaction-semantic-key="${escapeHtml(definition.key)}">＋ 添加 Slot</button>`}</section>`;
}

function renderReactionSlotChip(slot) {
  const selected = slot?.slotId === state.activeReactionSlotId;
  const preset = getReactionSlotPresetState(slot);
  const badge = preset.kind === "conflict" ? " · 语义冲突" : "";
  return `<button type="button" class="reaction-slot-chip${selected ? " active" : ""}${preset.kind === "conflict" ? " warn" : ""}" data-reaction-action="selectSlot" data-reaction-slot-id="${escapeHtml(slot?.slotId || "")}">${escapeHtml(slot?.displayName || slot?.slotId || "未命名")}${escapeHtml(badge)}</button>`;
}

function renderReactionSlotDetail(configuration, slot) {
  const combined = getActiveCombinedWorkbenchConfiguration();
  const mapping = resolveReactionActionRole(combined?.actionConfiguration, slot.actionRole);
  const resource = slot.resourceKey || slot.resourceSelection?.runtimeResourceKey || "未选择动画资源";
  const preset = getReactionSlotPresetState(slot);
  const fixedSemantic = preset.kind === "preset" || preset.kind === "conflict";
  const presetNotice = preset.kind === "conflict"
      ? `<div class="reaction-preset-notice warn"><div><strong>预设语义冲突</strong><span>${escapeHtml(preset.conflicts.join("、"))} 与固定预设不一致。</span></div><button type="button" data-reaction-action="restorePreset">恢复预设值</button></div>`
      : fixedSemantic ? `<div class="reaction-preset-notice"><div><strong>标准语义预设</strong><span>ID、Trigger、PressureBand 与 ActionRole 由系统固定；请编辑资源和表现参数。</span></div></div>` : "";
  return `<article class="reaction-slot-editor"><header class="reaction-slot-editor-head"><div><span class="combined-eyebrow">当前 Slot</span><strong>${escapeHtml(slot.displayName || slot.slotId)}</strong><code>${escapeHtml(slot.slotId)}</code></div><div><span class="sync-badge ${configuration.defaultSlotId === slot.slotId ? "ok" : ""}">${configuration.defaultSlotId === slot.slotId ? "默认 Slot" : "普通 Slot"}</span><button type="button" data-reaction-action="copySlot">复制</button><button type="button" data-reaction-action="setDefault"${configuration.defaultSlotId === slot.slotId ? " disabled" : ""}>设为默认</button><button type="button" class="danger subtle" data-reaction-action="deleteSlot">删除</button></div></header>
    ${presetNotice}<div class="reaction-slot-groups"><fieldset><legend>身份与语义</legend><div class="single-weapon-field-grid"><label><span>稳定 ID</span><input readonly value="${escapeHtml(slot.slotId)}"></label><label><span>显示名称</span><input data-reaction-slot-field="displayName" value="${escapeHtml(slot.displayName || "")}"></label><label><span>Trigger</span><input data-reaction-slot-field="trigger" value="${escapeHtml(slot.trigger || "")}"${fixedSemantic ? " readonly" : ""}></label><label><span>PressureBand</span><input data-reaction-slot-field="pressureBand" value="${escapeHtml(slot.pressureBand || "")}"${fixedSemantic ? " readonly" : ""}></label></div></fieldset>
    <fieldset><legend>Action 与时序</legend><div class="single-weapon-field-grid"><label><span>ReactionGroup</span><input data-reaction-slot-field="reactionGroupId" value="${escapeHtml(slot.reactionGroupId || "")}"></label><label><span>ActionRole</span><input data-reaction-slot-field="actionRole" value="${escapeHtml(slot.actionRole || "")}"${fixedSemantic ? " readonly" : ""}></label><label><span>持续帧</span><input type="number" min="0" data-reaction-slot-field="durationFrames" value="${Number(slot.durationFrames || 0)}"></label><label><span>优先级</span><input type="number" data-reaction-slot-field="priority" value="${Number(slot.priority || 0)}"></label></div><div class="reaction-action-role-status ${mapping.state}"><div><strong>${mapping.state === "resolved" ? "ActionRole 已解析" : "ActionRole 待处理"}</strong><span>${escapeHtml(mapping.label)}</span></div><button type="button" data-reaction-action="locateAction">定位组合动作</button></div></fieldset>
    <fieldset><legend>冲击与资源</legend><div class="single-weapon-field-grid"><label><span>最小冲击（可空）</span><input type="number" step="any" data-reaction-slot-field="minImpactForce" value="${slot.minImpactForce ?? ""}"></label><label><span>最大冲击（可空）</span><input type="number" step="any" data-reaction-slot-field="maxImpactForce" value="${slot.maxImpactForce ?? ""}"></label><label><span>PreloadPolicy</span><input data-reaction-slot-field="preloadPolicy" value="${escapeHtml(slot.preloadPolicy || "")}"></label><label class="single-weapon-checkbox"><input type="checkbox" data-reaction-slot-field="required"${slot.required ? " checked" : ""}><span>Required</span></label></div><div class="reaction-resource-row"><div><span>动画资源</span><code>${escapeHtml(resource)}</code></div><div><button type="button" class="primary" data-reaction-action="chooseResource">选择 / 替换</button><button type="button" data-reaction-action="clearResource"${resource === "未选择动画资源" ? " disabled" : ""}>清除</button></div></div></fieldset></div></article>`;
}

async function handleReactionWorkbenchClick(event) {
  const button = event.target.closest("button[data-reaction-action]");
  if (!button || button.disabled) return;
  const action = button.dataset.reactionAction;
  if (action === "back") { state.combinedWeaponSubWorkspace = ""; render(); }
  else if (action === "new") createReactionConfiguration(false);
  else if (action === "copyConfiguration") createReactionConfiguration(true);
  else if (action === "deleteConfiguration") await deleteReactionConfiguration();
  else if (action === "save") { if (await saveGlobalWeaponLibrary()) { state.message = "全局受击配置已保存。"; showToast(state.message, "success"); render(); } }
  else if (action === "selectConfiguration") { state.activeReactionConfigurationWorkbenchId = button.dataset.reactionId || ""; state.activeReactionSlotId = ""; render(); }
  else if (action === "selectSlot") { state.activeReactionSlotId = button.dataset.reactionSlotId || ""; render(); }
  else if (action === "newSlot") createReactionSlot(false);
  else if (action === "newSemanticSlot") createReactionSlot(false, button.dataset.reactionSemanticKey || "");
  else if (action === "copySlot") createReactionSlot(true);
  else if (action === "restorePreset") restoreReactionSlotPreset();
  else if (action === "deleteSlot") deleteReactionSlot();
  else if (action === "setDefault") updateReactionConfigurationField("defaultSlotId", state.activeReactionSlotId);
  else if (action === "chooseResource") await openGlobalReactionSlotPicker(getActiveReactionWorkbenchConfiguration()?.reactionConfigurationId, state.activeReactionSlotId);
  else if (action === "clearResource") {
    const slot = (getActiveReactionWorkbenchConfiguration()?.slots || []).find(value => value?.slotId === state.activeReactionSlotId);
    if (slot) { slot.resourceKey = ""; slot.resourceSelection = null; markReactionLibraryDirty(); render(); }
  } else if (action === "locateAction") {
    state.combinedWeaponSubWorkspace = "";
    render();
    requestAnimationFrame(() => document.querySelector("[data-combined-private-json]")?.scrollIntoView({ behavior: "smooth", block: "center" }));
  }
}

function handleReactionWorkbenchChange(event) {
  const configField = event.target.closest("[data-reaction-config-field]");
  if (configField) updateReactionConfigurationField(configField.dataset.reactionConfigField, configField.value);
  const slotField = event.target.closest("[data-reaction-slot-field]");
  if (slotField) updateReactionSlotField(slotField.dataset.reactionSlotField, slotField.type === "checkbox" ? slotField.checked : slotField.value, slotField.type);
}

function handleReactionWorkbenchInput(event) {
  const search = event.target.closest("[data-reaction-search]");
  if (!search) return;
  const selectionStart = search.selectionStart;
  state.reactionConfigurationSearchQuery = search.value || "";
  renderCombinedWeaponWorkbench();
  const next = el.combinedWeaponConfigurationPanel?.querySelector("[data-reaction-search]");
  next?.focus();
  if (next && Number.isInteger(selectionStart)) next.setSelectionRange(selectionStart, selectionStart);
}

Object.assign(globalThis, {
  renderReactionConfigurationWorkbench,
  handleReactionWorkbenchClick,
  handleReactionWorkbenchChange,
  handleReactionWorkbenchInput,
  classifyReactionSlot,
  groupReactionSlots,
  resolveReactionActionRole,
  getReactionSemanticTemplate
});

export { classifyReactionSlot, groupReactionSlots, resolveReactionActionRole, getReactionRecordStatus, getReactionSemanticTemplate, getReactionSlotPresetState, createReactionPresetSlot };
