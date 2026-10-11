// CharacterStudio combined-weapon configuration workbench.
// Owns public combined-weapon defaults plus current-character bindings and overrides.

function canonicalizeCombinedWeaponValue(value) {
  if (Array.isArray(value)) return value.map(canonicalizeCombinedWeaponValue);
  if (!value || typeof value !== "object") return value;
  const result = {};
  for (const key of Object.keys(value).sort()) result[key] = canonicalizeCombinedWeaponValue(value[key]);
  return result;
}

function canonicalizeCombinedWeaponConfiguration(value) {
  return JSON.stringify(canonicalizeCombinedWeaponValue(value || {}));
}

function getCombinedWeaponRecordStatus(item, baselineItems = []) {
  const id = item?.combinedWeaponConfigurationId || "";
  const baseline = baselineItems.find(candidate => candidate?.combinedWeaponConfigurationId === id);
  if (!baseline) return "new";
  return canonicalizeCombinedWeaponConfiguration(item) === canonicalizeCombinedWeaponConfiguration(baseline)
    ? "clean"
    : "modified";
}

function getCombinedWeaponDraftSummary(items = [], baselineItems = []) {
  const currentIds = new Set(items.map(item => item?.combinedWeaponConfigurationId).filter(Boolean));
  let added = 0;
  let modified = 0;
  for (const item of items) {
    const status = getCombinedWeaponRecordStatus(item, baselineItems);
    if (status === "new") added++;
    else if (status === "modified") modified++;
  }
  const deleted = baselineItems.filter(item =>
    item?.combinedWeaponConfigurationId && !currentIds.has(item.combinedWeaponConfigurationId)).length;
  return { added, modified, deleted, changed: added + modified + deleted };
}

function getCombinedWeaponSlot(item, slotId) {
  return (item?.slots || []).find(slot => slot?.slotId === slotId) || null;
}

function getCombinedWeaponCompleteness(item) {
  const missing = [];
  for (const slotId of ["mainHand", "offHand"]) {
    if (!getCombinedWeaponSlot(item, slotId)?.singleWeaponConfigurationId) missing.push(slotId);
  }
  if (!item?.animationConfigurationId) missing.push("animation");
  if (!item?.reactionConfigurationId) missing.push("reaction");
  if (!(item?.actionConfiguration?.actions || []).some(action => action?.actionRole && action?.actionId))
    missing.push("actions");
  return { complete: missing.length === 0, missing };
}

function getCombinedWeaponReadiness(item, snapshot, baselineItems = [], recordItem = item) {
  const recordStatus = getCombinedWeaponRecordStatus(recordItem, baselineItems);
  const completeness = getCombinedWeaponCompleteness(item);
  if (!completeness.complete) return { state: "draft", label: `草稿 · 缺少 ${completeness.missing.length} 项`, missing: completeness.missing };
  if (recordStatus !== "clean") return { state: "draft", label: "待保存并重新生成", missing: [] };
  if ((snapshot?.diagnostics || []).length) return { state: "blocked", label: `阻断 · ${snapshot.diagnostics.length} 项`, missing: [] };
  if (String(snapshot?.generationStatus || "").toLowerCase() === "generated") return { state: "ready", label: "可生成", missing: [] };
  return { state: "draft", label: "等待生成快照", missing: [] };
}

function filterAndSortCombinedWeaponConfigurations(items = [], options = {}, snapshots = [], baselineItems = [], defaultId = "") {
  const query = String(options.query || "").trim().toLocaleLowerCase();
  const readinessFilter = options.readiness || "all";
  const defaultFilter = options.defaultState || "all";
  const snapshotsById = new Map(snapshots.map(item => [item?.combinedWeaponConfigurationId || "", item]));
  const readinessFor = item => {
    if (typeof options.isBound === "function" && !options.isBound(item))
      return { state: "draft", label: "当前角色未引用", missing: [] };
    const effective = typeof options.resolveItem === "function" ? options.resolveItem(item) : item;
    return getCombinedWeaponReadiness(
      effective,
      snapshotsById.get(item?.combinedWeaponConfigurationId),
      baselineItems,
      item);
  };
  const filtered = items.filter(item => {
    const slots = item?.slots || [];
    const searchable = [
      item?.combinedWeaponConfigurationId,
      item?.displayName,
      item?.animationConfigurationId,
      item?.abilityConfigurationId,
      item?.reactionConfigurationId,
      ...slots.flatMap(slot => [slot?.slotId, slot?.singleWeaponConfigurationId])
    ].join(" ").toLocaleLowerCase();
    if (query && !searchable.includes(query)) return false;
    const isDefault = item?.combinedWeaponConfigurationId === defaultId;
    if (defaultFilter === "default" && !isDefault) return false;
    if (defaultFilter === "nonDefault" && isDefault) return false;
    const readiness = readinessFor(item);
    return readinessFilter === "all" || readiness.state === readinessFilter;
  });

  const statusRank = { new: 0, modified: 1, clean: 2 };
  const readinessRank = { blocked: 0, draft: 1, ready: 2 };
  const sort = options.sort || "id";
  filtered.sort((left, right) => {
    let comparison = 0;
    if (sort === "name") comparison = String(left?.displayName || "").localeCompare(String(right?.displayName || ""));
    else if (sort === "status") comparison = (statusRank[getCombinedWeaponRecordStatus(left, baselineItems)] ?? 3) -
      (statusRank[getCombinedWeaponRecordStatus(right, baselineItems)] ?? 3);
    else if (sort === "readiness") {
      comparison = (readinessRank[readinessFor(left).state] ?? 3) -
        (readinessRank[readinessFor(right).state] ?? 3);
    } else if (sort === "default") {
      comparison = Number(right?.combinedWeaponConfigurationId === defaultId) - Number(left?.combinedWeaponConfigurationId === defaultId);
    }
    if (comparison !== 0) return comparison;
    return String(left?.combinedWeaponConfigurationId || "").localeCompare(String(right?.combinedWeaponConfigurationId || ""));
  });
  return filtered;
}

function getCombinedWeaponItems() {
  const library = state.weaponLibrary ||= {};
  library.schemaVersion ||= "6.0";
  library.combinedWeaponConfigurations = Array.isArray(library.combinedWeaponConfigurations)
    ? library.combinedWeaponConfigurations : [];
  return library.combinedWeaponConfigurations;
}

function getCombinedWeaponBaselineItems() {
  return state.combinedWeaponConfigurationLibraryBaseline || [];
}

function getCombinedWeaponBindings() {
  if (!state.package) return [];
  const app = state.package.applicationConfig ||= {};
  app.schemaVersion ||= "8.0";
  app.combinedWeaponConfigurationBindings = Array.isArray(app.combinedWeaponConfigurationBindings)
    ? app.combinedWeaponConfigurationBindings : [];
  return app.combinedWeaponConfigurationBindings;
}

function getCombinedWeaponBinding(id, create = false) {
  const bindings = getCombinedWeaponBindings();
  let binding = bindings.find(item => item?.combinedWeaponConfigurationId === id) || null;
  if (!binding && create && id) {
    binding = { combinedWeaponConfigurationId: id, overrides: {} };
    bindings.push(binding);
  }
  if (binding) binding.overrides = binding.overrides && typeof binding.overrides === "object"
    ? binding.overrides : {};
  return binding;
}

function isCombinedWeaponBound(id) {
  return Boolean(getCombinedWeaponBinding(id));
}

function resolveCombinedWeaponConfiguration(item) {
  if (!item) return null;
  const effective = cloneCombinedWeaponValue(item);
  const overrides = getCombinedWeaponBinding(item.combinedWeaponConfigurationId)?.overrides || {};
  for (const field of ["animationConfigurationId", "abilityConfigurationId", "reactionConfigurationId"]) {
    const value = String(overrides[field] || "").trim();
    if (value) effective[field] = value;
  }
  if (overrides.actionConfiguration != null)
    effective.actionConfiguration = cloneCombinedWeaponValue(overrides.actionConfiguration);
  return effective;
}

function markCombinedWeaponLibraryDirty() {
  state.weaponLibraryDirty = true;
}

function getCombinedWeaponSnapshots() {
  return state.combinedWeaponConfigurationsSnapshot?.combinedWeaponConfigurations || [];
}

function getCombinedWeaponSnapshot(id) {
  return getCombinedWeaponSnapshots().find(item => item?.combinedWeaponConfigurationId === id) || null;
}

function getActiveCombinedWorkbenchConfiguration() {
  return resolveCombinedWeaponConfiguration(getActiveCombinedWeaponBaseConfiguration());
}

function getActiveCombinedWeaponBaseConfiguration() {
  const items = getCombinedWeaponItems();
  return items.find(item => item?.combinedWeaponConfigurationId === state.activeCombinedWeaponConfigurationId)
    || items.find(item => item?.combinedWeaponConfigurationId === state.package?.applicationConfig?.defaultCombinedWeaponConfigurationId)
    || items[0]
    || null;
}

function cloneCombinedWeaponValue(value) {
  return typeof clone === "function" ? clone(value) : JSON.parse(JSON.stringify(value));
}

function normalizeCombinedIdSegment(value) {
  return String(value || "item").trim().toLocaleLowerCase().replace(/[^a-z0-9._-]+/g, "_").replace(/^_+|_+$/g, "") || "item";
}

function uniqueCombinedWeaponConfigurationId(base = "weapon_configuration") {
  const used = new Set(getCombinedWeaponItems().map(item => item?.combinedWeaponConfigurationId));
  let candidate = base;
  let index = 1;
  while (used.has(candidate)) candidate = `${base}.${index++}`;
  return candidate;
}

function createBlankCombinedWeaponConfiguration() {
  return {
    combinedWeaponConfigurationId: "",
    displayName: "",
    slots: [
      { slotId: "mainHand", singleWeaponConfigurationId: "weapon.unarmed" },
      { slotId: "offHand", singleWeaponConfigurationId: "weapon.unarmed" }
    ],
    animationConfigurationId: "",
    abilityConfigurationId: "",
    reactionConfigurationId: "",
    actionConfiguration: {
      actions: [],
      commandSequences: [],
      abilityActionBindings: [],
      reactionActionBindings: []
    }
  };
}

function rewriteCombinedPrivateActionIds(configuration, configurationId) {
  const actionConfiguration = configuration.actionConfiguration ||= {
    actions: [], commandSequences: [], abilityActionBindings: [], reactionActionBindings: []
  };
  // Action IDs identify reusable library entries. The copied combination gets a new
  // deterministic ActionSet ID at compile time while its action references stay stable.
}

function openCombinedWeaponCreationDialog(mode, sourceId = "") {
  const source = getCombinedWeaponItems().find(item => item?.combinedWeaponConfigurationId === sourceId)
    || getActiveCombinedWeaponBaseConfiguration();
  if (mode === "copy" && !source) return;
  const packageId = normalizeCombinedIdSegment(state.package?.manifest?.packageId || "character");
  const base = mode === "copy" ? `${source.combinedWeaponConfigurationId}.copy` : `${packageId}.weapon_configuration`;
  state.combinedWeaponCreationDialog = {
    mode,
    sourceId: mode === "copy" ? source.combinedWeaponConfigurationId : "",
    id: uniqueCombinedWeaponConfigurationId(base),
    displayName: mode === "copy" ? `${source.displayName || source.combinedWeaponConfigurationId} 副本` : "新组合武器配置",
    error: ""
  };
  render();
}

function submitCombinedWeaponCreationDialog() {
  const dialog = state.combinedWeaponCreationDialog;
  if (!dialog) return;
  const id = String(dialog.id || "").trim();
  const displayName = String(dialog.displayName || "").trim();
  if (!id) dialog.error = "稳定 ID 不能为空。";
  else if (getCombinedWeaponItems().some(item => item?.combinedWeaponConfigurationId === id)) dialog.error = "稳定 ID 已存在；ID 区分大小写且必须唯一。";
  else if (!displayName) dialog.error = "显示名称不能为空。";
  else dialog.error = "";
  if (dialog.error) {
    render();
    return;
  }
  const source = dialog.mode === "copy"
    ? getCombinedWeaponItems().find(item => item?.combinedWeaponConfigurationId === dialog.sourceId)
    : null;
  const item = source ? cloneCombinedWeaponValue(source) : createBlankCombinedWeaponConfiguration();
  item.combinedWeaponConfigurationId = id;
  item.displayName = displayName;
  if (source) {
    stripFrozenAbilityDataFromCombinedCopy(item);
    rewriteCombinedPrivateActionIds(item, id);
  }
  getCombinedWeaponItems().push(item);
  getCombinedWeaponBinding(id, true);
  if (!state.package.applicationConfig.defaultCombinedWeaponConfigurationId)
    state.package.applicationConfig.defaultCombinedWeaponConfigurationId = id;
  state.activeCombinedWeaponConfigurationId = id;
  state.combinedWeaponWorkspaceView = "edit";
  state.combinedWeaponCreationDialog = null;
  markCombinedWeaponLibraryDirty();
  normalizeWeaponConfiguration4(state.package);
  refreshDirtyFromCleanSnapshot();
  render();
}

function stripFrozenAbilityDataFromCombinedCopy(configuration) {
  configuration.abilityConfigurationId = "";
  const action = configuration.actionConfiguration ||= {
    actions: [], commandSequences: [], abilityActionBindings: [], reactionActionBindings: []
  };
  const abilityRoles = new Set((action.abilityActionBindings || []).map(binding => binding?.actionRole).filter(Boolean));
  action.abilityActionBindings = [];
  action.actions = (action.actions || []).filter(entry => {
    const role = String(entry?.actionRole || "");
    return !abilityRoles.has(role) && !role.startsWith("ability.");
  });
}

async function deleteCombinedWeaponConfiguration(id) {
  const item = getCombinedWeaponItems().find(candidate => candidate?.combinedWeaponConfigurationId === id);
  if (!item) return;
  if (isCombinedWeaponBound(id)) {
    state.message = "不能删除公共组合：当前角色仍在引用它。请先解除当前角色绑定。";
    render();
    return;
  }
  const references = await readJson(`/api/weapon-library/references?configurationType=combinedWeapon&configurationId=${encodeURIComponent(id)}`, null);
  if ((references?.referenceCount || 0) > 0) {
    state.message = `不能删除公共组合：仍有 ${references.referenceCount} 个角色引用。请先从这些角色移除引用。`;
    render();
    return;
  }
  if (!window.confirm(`确认删除公共组合武器配置 ${item.displayName || id}（${id}）？`)) return;
  state.weaponLibrary.combinedWeaponConfigurations = getCombinedWeaponItems().filter(candidate => candidate !== item);
  markCombinedWeaponLibraryDirty();
  state.activeCombinedWeaponConfigurationId = state.package?.applicationConfig?.defaultCombinedWeaponConfigurationId
    || state.weaponLibrary.combinedWeaponConfigurations[0]?.combinedWeaponConfigurationId || "";
  if (state.combinedWeaponExpandedOverviewId === id) state.combinedWeaponExpandedOverviewId = "";
  render();
}

function setCombinedWeaponAsDefault(id) {
  if (!getCombinedWeaponItems().some(item => item?.combinedWeaponConfigurationId === id)) return;
  getCombinedWeaponBinding(id, true);
  state.package.applicationConfig.defaultCombinedWeaponConfigurationId = id;
  refreshDirtyFromCleanSnapshot();
  render();
}

function setCombinedWeaponBindingEnabled(id, enabled) {
  const app = state.package?.applicationConfig;
  if (!app || !getCombinedWeaponItems().some(item => item?.combinedWeaponConfigurationId === id)) return;
  if (enabled) {
    getCombinedWeaponBinding(id, true);
    if (!app.defaultCombinedWeaponConfigurationId) app.defaultCombinedWeaponConfigurationId = id;
  } else {
    app.combinedWeaponConfigurationBindings = getCombinedWeaponBindings().filter(item =>
      item?.combinedWeaponConfigurationId !== id);
    if (app.defaultCombinedWeaponConfigurationId === id)
      app.defaultCombinedWeaponConfigurationId = app.combinedWeaponConfigurationBindings[0]?.combinedWeaponConfigurationId || "";
  }
  normalizeWeaponConfiguration4(state.package);
  refreshDirtyFromCleanSnapshot();
  render();
}

function setCombinedWeaponSlot(slotId, singleWeaponConfigurationId) {
  const configuration = getActiveCombinedWeaponBaseConfiguration();
  if (!configuration) return;
  const geometry = state.package.geometry ||= {};
  geometry.weaponSlots = Array.isArray(geometry.weaponSlots) ? geometry.weaponSlots : [];
  configuration.slots = (configuration.slots || []).filter(item => item?.slotId !== slotId);
  const selectedId = singleWeaponConfigurationId || "weapon.unarmed";
  ensureCharacterWeaponSlot(slotId);
  configuration.slots.push({ slotId, singleWeaponConfigurationId: selectedId });
  configuration.slots.sort((left, right) => String(left?.slotId || "").localeCompare(String(right?.slotId || "")));
  markCombinedWeaponLibraryDirty();
  normalizeWeaponConfiguration4(state.package);
  refreshDirtyFromCleanSnapshot();
  render();
}

function updateCombinedWeaponField(field, value) {
  const item = getActiveCombinedWeaponBaseConfiguration();
  if (!item) return;
  if (field === "displayName") item.displayName = value;
  else if (["animationConfigurationId", "abilityConfigurationId", "reactionConfigurationId"].includes(field)) item[field] = value;
  else return;
  markCombinedWeaponLibraryDirty();
  render();
}

function updateCombinedPrivateActionConfiguration(value) {
  const item = getActiveCombinedWeaponBaseConfiguration();
  if (!item) return;
  try {
    const parsed = JSON.parse(value || "{}");
    item.actionConfiguration = {
      actions: Array.isArray(parsed.actions) ? parsed.actions : [],
      commandSequences: Array.isArray(parsed.commandSequences) ? parsed.commandSequences : [],
      abilityActionBindings: Array.isArray(parsed.abilityActionBindings) ? parsed.abilityActionBindings : [],
      reactionActionBindings: Array.isArray(parsed.reactionActionBindings) ? parsed.reactionActionBindings : []
    };
    if (typeof globalThis.normalizeActionConfiguration === "function") {
      globalThis.normalizeActionConfiguration(item.actionConfiguration);
    }
    markCombinedWeaponLibraryDirty();
    render();
  } catch (error) {
    state.message = `公共默认动作 JSON 无效：${error.message}`;
    renderShellStatus();
  }
}

function updateCombinedWeaponOverrideField(field, value) {
  const item = getActiveCombinedWeaponBaseConfiguration();
  if (!item || !["animationConfigurationId", "abilityConfigurationId", "reactionConfigurationId"].includes(field)) return;
  const binding = getCombinedWeaponBinding(item.combinedWeaponConfigurationId, true);
  if (value) binding.overrides[field] = value;
  else delete binding.overrides[field];
  refreshDirtyFromCleanSnapshot();
  render();
}

function setCombinedWeaponActionOverrideEnabled(enabled) {
  const item = getActiveCombinedWeaponBaseConfiguration();
  if (!item) return;
  const binding = getCombinedWeaponBinding(item.combinedWeaponConfigurationId, true);
  if (enabled) binding.overrides.actionConfiguration = cloneCombinedWeaponValue(item.actionConfiguration || {
    actions: [], commandSequences: [], abilityActionBindings: [], reactionActionBindings: []
  });
  else delete binding.overrides.actionConfiguration;
  refreshDirtyFromCleanSnapshot();
  render();
}

const CORE_CHARACTER_ACTION_ROLES = [
  ["primary", "普通攻击 1"],
  ["primary.combo2", "连段 2"],
  ["primary.combo3", "连段 3"],
  ["heavy", "重击"],
  ["evade", "闪避"]
];

function getCharacterActionCatalogActions() {
  return Array.isArray(state.characterActionCatalog?.actions)
    ? state.characterActionCatalog.actions
    : [];
}

function getCharacterActionSetId(configurationId) {
  return (state.characterActionCatalog?.actionSets || []).find(item =>
    item?.combinedWeaponConfigurationId === configurationId)?.actionSetId || "待编译生成";
}

function getCharacterActionRoleRows(actionConfiguration, reactionConfiguration) {
  const labels = new Map(CORE_CHARACTER_ACTION_ROLES);
  for (const action of actionConfiguration?.actions || []) {
    if (action?.actionRole && !labels.has(action.actionRole)) labels.set(action.actionRole, action.actionRole);
  }
  for (const binding of actionConfiguration?.abilityActionBindings || []) {
    if (binding?.actionRole && !labels.has(binding.actionRole)) labels.set(binding.actionRole, binding.actionRole);
  }
  for (const binding of actionConfiguration?.reactionActionBindings || []) {
    if (binding?.actionRole && !labels.has(binding.actionRole)) labels.set(binding.actionRole, binding.actionRole);
  }
  for (const slot of reactionConfiguration?.slots || []) {
    const role = slot?.actionRole || "";
    if (role && !labels.has(role)) labels.set(role, `受击 · ${slot.displayName || slot.slotId || role}`);
  }
  return [...labels].map(([role, label]) => ({ role, label }));
}

function getCharacterActionBinding(configuration, role) {
  return (configuration?.actions || []).find(item => item?.actionRole === role) || null;
}

function setCharacterActionRole(scope, role, actionId) {
  const publicItem = getActiveCombinedWeaponBaseConfiguration();
  if (!publicItem || !role) return;
  let configuration;
  if (scope === "character") {
    const binding = getCombinedWeaponBinding(publicItem.combinedWeaponConfigurationId, true);
    if (!binding.overrides.actionConfiguration) {
      // A character override is a complete replacement. The first edited row copies the full
      // public mapping, then only changes the selected role.
      binding.overrides.actionConfiguration = cloneCombinedWeaponValue(publicItem.actionConfiguration || {
        actions: [], commandSequences: [], abilityActionBindings: [], reactionActionBindings: []
      });
    }
    configuration = binding.overrides.actionConfiguration;
  } else {
    configuration = publicItem.actionConfiguration ||= {
      actions: [], commandSequences: [], abilityActionBindings: [], reactionActionBindings: []
    };
  }
  configuration.actions = Array.isArray(configuration.actions) ? configuration.actions : [];
  const existingIndex = configuration.actions.findIndex(item => item?.actionRole === role);
  if (!actionId || actionId === "0") {
    if (existingIndex >= 0) {
      configuration.actions.splice(existingIndex, 1);
    }
    for (const binding of configuration.abilityActionBindings || []) {
      if (binding?.actionRole === role) binding.actionId = 0;
    }
    for (const binding of configuration.reactionActionBindings || []) {
      if (binding?.actionRole === role) binding.actionId = 0;
    }
    for (const binding of configuration.commandBindings || []) {
      if (binding?.actionRole === role) binding.actionId = 0;
    }
  } else {
    const numericId = Number(actionId);
    const normalizedActionId = Number.isInteger(numericId) && numericId > 0 ? numericId : actionId;
    if (existingIndex >= 0) {
      configuration.actions[existingIndex].actionId = normalizedActionId;
    } else {
      configuration.actions.push({ actionRole: role, actionId: normalizedActionId });
    }
    for (const binding of configuration.abilityActionBindings || []) {
      if (binding?.actionRole === role) binding.actionId = normalizedActionId;
    }
    for (const binding of configuration.reactionActionBindings || []) {
      if (binding?.actionRole === role) binding.actionId = normalizedActionId;
    }
    for (const binding of configuration.commandBindings || []) {
      if (binding?.actionRole === role) binding.actionId = normalizedActionId;
    }
  }
  if (scope === "public") markCombinedWeaponLibraryDirty();
  else refreshDirtyFromCleanSnapshot();
  render();
}

function renderCharacterActionOption(action, selectedActionId) {
  const animation = action.animationResourceKey || action.animationActionKey || "无动画";
  const hit = action.hasHitTrack ? `命中轨 ${action.hitTrackCount}` : "无命中轨";
  const animationOnly = action.animationOnly ? " · 仅动画" : "";
  const isSelected = selectedActionId !== "" && selectedActionId != null && selectedActionId !== 0 && String(action.actionId) === String(selectedActionId);
  return `<option value="${escapeHtml(action.actionId)}"${isSelected ? " selected" : ""}${action.compatibilityKind === "Incompatible" ? " disabled" : ""}>${escapeHtml(action.displayName || action.actionId)} · ${escapeHtml(animation)} · ${action.durationFrames || 0}f · ${hit} · ${escapeHtml(action.compatibilityLabel || "未知")}${animationOnly}</option>`;
}

function renderCharacterActionRoleTable(scope, actionConfiguration, reactionConfiguration) {
  const actions = getCharacterActionCatalogActions();
  const common = actions.filter(action => action?.sourceKind === "common");
  const character = actions.filter(action => action?.sourceKind === "character");
  const rows = getCharacterActionRoleRows(actionConfiguration, reactionConfiguration);
  return `<div class="character-action-slot-table"><table><thead><tr><th>动作槽</th><th>动作库选择</th><th>当前动作信息</th><th>操作</th></tr></thead><tbody>${rows.map(row => {
    const rawSelectedId = getCharacterActionBinding(actionConfiguration, row.role)?.actionId;
    const selectedId = rawSelectedId != null && rawSelectedId !== "" && rawSelectedId !== 0 ? String(rawSelectedId) : "";
    const selected = selectedId ? (actions.find(action => String(action?.actionId) === selectedId) || null) : null;
    const requestKey = `${getActiveCombinedWeaponBaseConfiguration()?.combinedWeaponConfigurationId || ""}:${selected?.actionId ?? selectedId}`;
    const openStatus = state.characterActionOpenRequests?.[requestKey] || null;
    const source = selected?.sourceKind === "character" ? "角色专属" : selected ? "公共动作" : "未选择";
    const metadata = selected
      ? `${selected.animationResourceKey || selected.animationActionKey || "无动画"} · ${selected.durationFrames || 0} 帧 · ${selected.hasHitTrack ? `命中轨 ${selected.hitTrackCount}` : "无命中轨"} · ${selected.compatibilityLabel || "未知"}`
      : "请选择动作";
    const statusTone = ["failed", "timedOut", "saveFailed", "conflict"].includes(openStatus?.status)
      ? "error" : ["opened", "saved"].includes(openStatus?.status) ? "ok" : "warn";
    const statusLabel = openStatus?.status === "saved" ? "已保存"
      : openStatus?.status === "opened" ? "已打开"
      : openStatus?.status === "conflict" ? "版本冲突"
      : openStatus?.status === "saveFailed" ? "保存失败"
      : openStatus?.status === "timedOut" ? "超时"
      : openStatus?.status === "failed" ? "打开失败"
      : openStatus ? "打开中" : "";
    return `<tr><th><strong>${escapeHtml(row.label)}</strong><code>${escapeHtml(row.role)}</code></th><td><select data-character-action-role="${escapeHtml(row.role)}" data-character-action-scope="${scope}"><option value="">未选择</option>${scope === "character" ? `<optgroup label="角色专属">${character.map(action => renderCharacterActionOption(action, selectedId)).join("") || `<option disabled>暂无角色专属动作</option>`}</optgroup>` : ""}<optgroup label="公共动作">${common.map(action => renderCharacterActionOption(action, selectedId)).join("")}</optgroup></select></td><td><span class="sync-badge ${selected?.compatibilityKind === "Incompatible" ? "error" : selected ? "ok" : "warn"}">${escapeHtml(source)}</span><span>${escapeHtml(metadata)}</span>${selected?.animationOnly ? `<span class="sync-badge warn">仅动画，不造成伤害</span>` : ""}${openStatus ? `<span class="sync-badge ${statusTone}" title="${escapeHtml(openStatus.message || "")}">${escapeHtml(statusLabel)}</span>` : ""}</td><td>${selected ? `<button type="button" data-combined-action="openCharacterAction" data-action-id="${escapeHtml(selected.actionId)}">在 Unity 查看${selected.readOnly ? "（只读）" : ""}</button>${openStatus?.requestId ? `<button type="button" data-combined-action="refreshCharacterActionStatus" data-request-key="${escapeHtml(requestKey)}" data-request-id="${escapeHtml(openStatus.requestId)}">查看状态</button>` : ""}${scope === "character" && selected.sourceKind === "common" ? `<button type="button" data-combined-action="copyCharacterAction" data-action-role="${escapeHtml(row.role)}" data-action-id="${escapeHtml(selected.actionId)}">复制为角色专属并编辑</button>` : ""}` : ""}</td></tr>`;
  }).join("")}</tbody></table></div>`;
}

async function openCharacterActionInUnity(actionId) {
  const action = getCharacterActionCatalogActions().find(item => String(item?.actionId) === String(actionId));
  const item = getActiveCombinedWeaponBaseConfiguration();
  if (!action || !item) return;
  const library = (state.characterActionCatalog?.libraries || []).find(entry => entry?.libraryId === action.libraryId);
  const normalizedActionId = Number(action.actionId) || action.actionId;
  const key = `${item.combinedWeaponConfigurationId}:${normalizedActionId}`;
  state.characterActionOpenRequests = { ...(state.characterActionOpenRequests || {}), [key]: { status: "pending", message: "等待 Unity...", requestId: "" } };
  state.message = `正在请求 Unity 打开动作：${normalizedActionId}`;
  renderShellStatus();
  try {
    const response = await fetch("/api/character-action/open", {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({
        package: state.packageRelative,
        abilityId: 0,
        profileId: state.characterActionCatalog.profileId,
        profileSourceDirectory: state.characterActionCatalog.profileSourceDirectory,
        actionSetSourceDirectory: state.characterActionCatalog.actionSetSourceDirectory,
        actionLibrarySourceDirectory: library?.sourceDirectory || action.sourceDirectory,
        actionSetStableId: getCharacterActionSetId(item.combinedWeaponConfigurationId),
        actionLibraryId: action.libraryId,
        actionId: normalizedActionId,
        sourceHash: action.sourceHash,
        mode: action.readOnly ? "readOnly" : "readWrite"
      })
    });
    const result = await readJsonResponseOrText(response);
    if (!response.ok) throw new Error(result?.message || `HTTP ${response.status}`);
    state.characterActionOpenRequests[key] = {
      status: result.status || "pending",
      message: result.message || "等待 Unity...",
      requestId: result.requestId || ""
    };
    render();
    pollCharacterActionOpenStatus(key, result.requestId, 0);
  } catch (error) {
    const message = `Unity 打开动作失败：${error?.message || error}`;
    state.characterActionOpenRequests[key] = { status: "failed", message };
    state.message = message;
    render();
  }
}

function pollCharacterActionOpenStatus(key, requestId, attempt) {
  window.setTimeout(async () => {
    const status = await readJson(`/api/character-action/open-status?id=${encodeURIComponent(requestId)}`, null);
    if (["opened", "failed", "timedOut"].includes(status?.status)) {
      state.characterActionOpenRequests[key] = { ...status, requestId };
      state.message = status.message || (status.status === "opened" ? "Unity 已打开动作。" : "Unity 打开动作失败。");
      render();
      return;
    }
    if (attempt >= 16) {
      state.characterActionOpenRequests[key] = { status: "timedOut", message: "Unity 打开动作超时。", requestId };
      state.message = "Unity 打开动作超时；请确认 Editor 已打开且编译完成。";
      render();
      return;
    }
    pollCharacterActionOpenStatus(key, requestId, attempt + 1);
  }, attempt ? 1000 : 500);
}

async function refreshCharacterActionOpenStatus(key, requestId) {
  if (!key || !requestId) return;
  const status = await readJson(`/api/character-action/open-status?id=${encodeURIComponent(requestId)}`, null);
  if (!status) {
    state.message = "未读取到 Unity 动作状态。";
    renderShellStatus();
    return;
  }
  state.characterActionOpenRequests[key] = { ...status, requestId };
  state.message = status.message || `Unity 动作状态：${status.status || "未知"}`;
  render();
}

async function copyCharacterActionToCharacter(role, sourceActionId) {
  const item = getActiveCombinedWeaponBaseConfiguration();
  const source = getCharacterActionCatalogActions().find(action => String(action?.actionId) === String(sourceActionId));
  if (!item || !source) return;
  const normalizedSourceActionId = Number(source.actionId) || source.actionId;
  state.message = `正在复制公共动作 ${normalizedSourceActionId}...`;
  renderShellStatus();
  const result = await postJsonDetailed("/api/character-actions/copy-to-character", {
    package: state.packageRelative,
    combinedWeaponConfigurationId: item.combinedWeaponConfigurationId,
    actionRole: role,
    sourceActionId: normalizedSourceActionId,
    sourceHash: source.sourceHash,
    expectedRevision: state.packageRevision || ""
  });
  if (!result?.success) {
    state.message = result?.code === "CHARACTER_ACTION_SOURCE_CONFLICT"
      ? "公共动作源已经变化；请刷新状态后重试，未覆盖任何文件。"
      : (result?.message || "复制公共动作失败；事务已回滚。");
    renderShellStatus();
    return;
  }
  state.packageRevision = result.revision || state.packageRevision;
  await loadPackageState();
  state.activeCombinedWeaponConfigurationId = item.combinedWeaponConfigurationId;
  state.message = `已复制为角色专属动作 ${result.actionId} 并原子更新 ${role} 槽位。`;
  render();
  await openCharacterActionInUnity(result.actionId);
}

function updateCombinedOverrideActionConfiguration(value) {
  const item = getActiveCombinedWeaponBaseConfiguration();
  if (!item) return;
  try {
    const parsed = JSON.parse(value || "{}");
    const binding = getCombinedWeaponBinding(item.combinedWeaponConfigurationId, true);
    binding.overrides.actionConfiguration = {
      actions: Array.isArray(parsed.actions) ? parsed.actions : [],
      commandSequences: Array.isArray(parsed.commandSequences) ? parsed.commandSequences : [],
      abilityActionBindings: Array.isArray(parsed.abilityActionBindings) ? parsed.abilityActionBindings : [],
      reactionActionBindings: Array.isArray(parsed.reactionActionBindings) ? parsed.reactionActionBindings : []
    };
    if (typeof globalThis.normalizeActionConfiguration === "function") {
      globalThis.normalizeActionConfiguration(binding.overrides.actionConfiguration);
    }
    refreshDirtyFromCleanSnapshot();
    render();
  } catch (error) {
    state.message = `角色动作覆盖 JSON 无效：${error.message}`;
    renderShellStatus();
  }
}

function renderCombinedWeaponWorkbench() {
  if (!el.combinedWeaponConfigurationPanel) return;
  const items = getCombinedWeaponItems();
  if (!items.some(item => item?.combinedWeaponConfigurationId === state.activeCombinedWeaponConfigurationId))
    state.activeCombinedWeaponConfigurationId = state.package?.applicationConfig?.defaultCombinedWeaponConfigurationId
      || items[0]?.combinedWeaponConfigurationId
      || "";
  renderCombinedWeaponTopSelector(items);
  if (state.combinedWeaponSubWorkspace === "reaction" && typeof globalThis.renderReactionConfigurationWorkbench === "function") {
    el.combinedWeaponConfigurationPanel.innerHTML = globalThis.renderReactionConfigurationWorkbench();
    return;
  }
  const baseline = getCombinedWeaponBaselineItems();
  const summary = getCombinedWeaponDraftSummary(items, baseline);
  const content = state.combinedWeaponWorkspaceView === "overview"
    ? renderCombinedWeaponOverview(items, baseline)
    : renderCombinedWeaponEditor(items, baseline);
  el.combinedWeaponConfigurationPanel.innerHTML = `<div class="single-weapon-workbench combined-weapon-workbench">
    ${renderCombinedWeaponWorkbenchHeader(summary)}
    <div class="single-weapon-subtabs" role="tablist" aria-label="组合武器配置视图">
      <button type="button" data-combined-action="setView" data-combined-view="edit" class="${state.combinedWeaponWorkspaceView === "edit" ? "active" : ""}">配置编辑</button>
      <button type="button" data-combined-action="setView" data-combined-view="overview" class="${state.combinedWeaponWorkspaceView === "overview" ? "active" : ""}">配置总览</button>
    </div>
    ${content}
    ${renderCombinedWeaponCreationDialog()}
  </div>`;
}

function renderCombinedWeaponTopSelector(items) {
  if (!el.activeCombinedWeaponConfigurationSelect) return;
  const defaultId = state.package?.applicationConfig?.defaultCombinedWeaponConfigurationId || "";
  el.activeCombinedWeaponConfigurationSelect.innerHTML = items.map(item =>
    `<option value="${escapeHtml(item?.combinedWeaponConfigurationId || "")}"${item?.combinedWeaponConfigurationId === state.activeCombinedWeaponConfigurationId ? " selected" : ""}>${escapeHtml(item?.displayName || item?.combinedWeaponConfigurationId || "未命名")}${item?.combinedWeaponConfigurationId === defaultId ? "（角色默认）" : isCombinedWeaponBound(item?.combinedWeaponConfigurationId) ? "（角色已引用）" : "（公共未引用）"}</option>`).join("");
  el.activeCombinedWeaponConfigurationSelect.disabled = items.length === 0;
}

function renderCombinedWeaponWorkbenchHeader(summary) {
  const hasDraft = Boolean(state.dirty || state.weaponLibraryDirty);
  const dirtyLabel = !hasDraft ? "公共库与角色引用均已保存"
    : state.weaponLibraryDirty && state.dirty ? "公共默认与角色覆盖均有草稿"
      : state.weaponLibraryDirty ? `公共库未保存 · ${summary.changed || 1} 项组合变更`
        : "角色引用或覆盖尚未保存";
  return `<div class="single-weapon-workbench-head">
    <div><strong>公共组合武器配置</strong><span>${escapeHtml(state.packageRelative || "角色包")} · 当前角色只保存引用与覆盖</span></div>
    <div class="single-weapon-head-status"><span class="sync-badge ${hasDraft ? "warn" : "ok"}">${dirtyLabel}</span><button type="button" data-combined-action="new">新建公共配置</button><button type="button" data-combined-action="save"${hasDraft ? "" : " disabled"}>保存公共库与角色</button></div>
  </div>`;
}

function renderCombinedWeaponFilters() {
  return `<div class="single-weapon-filters combined-weapon-filters">
    <label class="single-weapon-search"><span>搜索</span><input data-combined-search value="${escapeHtml(state.combinedWeaponSearchQuery || "")}" placeholder="ID / 名称 / 武器 / 动画 / 能力 / 受击"></label>
    <label><span>生成状态</span><select data-combined-filter="readiness"><option value="all">全部</option>${[["ready", "可生成"], ["draft", "草稿"], ["blocked", "阻断"]].map(([value, label]) => `<option value="${value}"${state.combinedWeaponReadinessFilter === value ? " selected" : ""}>${label}</option>`).join("")}</select></label>
    <label><span>默认状态</span><select data-combined-filter="defaultState"><option value="all">全部</option><option value="default"${state.combinedWeaponDefaultFilter === "default" ? " selected" : ""}>仅默认</option><option value="nonDefault"${state.combinedWeaponDefaultFilter === "nonDefault" ? " selected" : ""}>非默认</option></select></label>
    <label><span>排序</span><select data-combined-filter="sort"><option value="id"${state.combinedWeaponSort === "id" ? " selected" : ""}>稳定 ID</option><option value="name"${state.combinedWeaponSort === "name" ? " selected" : ""}>名称</option><option value="default"${state.combinedWeaponSort === "default" ? " selected" : ""}>默认优先</option><option value="readiness"${state.combinedWeaponSort === "readiness" ? " selected" : ""}>生成状态</option><option value="status"${state.combinedWeaponSort === "status" ? " selected" : ""}>修改状态</option></select></label>
  </div>`;
}

function getFilteredCombinedWeaponItems(items, baseline) {
  return filterAndSortCombinedWeaponConfigurations(items, {
    query: state.combinedWeaponSearchQuery,
    readiness: state.combinedWeaponReadinessFilter,
    defaultState: state.combinedWeaponDefaultFilter,
    sort: state.combinedWeaponSort,
    isBound: item => isCombinedWeaponBound(item?.combinedWeaponConfigurationId),
    resolveItem: resolveCombinedWeaponConfiguration
  }, getCombinedWeaponSnapshots(), baseline, state.package?.applicationConfig?.defaultCombinedWeaponConfigurationId || "");
}

function renderCombinedWeaponEditor(items, baseline) {
  const visible = getFilteredCombinedWeaponItems(items, baseline);
  const active = getActiveCombinedWeaponBaseConfiguration();
  const activeVisible = visible.some(item => item === active);
  return `<div class="single-weapon-editor-layout">
    <aside class="single-weapon-master-list">${renderCombinedWeaponFilters()}${!activeVisible && active ? `<div class="single-weapon-lock-note"><span>当前配置被筛选条件隐藏，右侧草稿仍保留。</span><button type="button" data-combined-action="clearFilters">清除筛选</button></div>` : ""}<div class="single-weapon-list">${visible.map(item => renderCombinedWeaponListItem(item, baseline)).join("") || `<div class="empty compact">没有匹配配置。</div>`}</div></aside>
    <section class="single-weapon-detail">${active ? renderCombinedWeaponDetail(active, baseline) : `<div class="empty">公共库还没有组合武器配置。新建后可让当前角色引用。</div>`}</section>
  </div>`;
}

function renderCombinedWeaponListItem(item, baseline) {
  const id = item?.combinedWeaponConfigurationId || "";
  const snapshot = getCombinedWeaponSnapshot(id);
  const bound = isCombinedWeaponBound(id);
  const effective = resolveCombinedWeaponConfiguration(item);
  const readiness = bound
    ? getCombinedWeaponReadiness(effective, snapshot, baseline, item)
    : { state: "draft", label: "公共配置 · 当前角色未引用", missing: [] };
  const main = getCombinedWeaponSlot(item, "mainHand")?.singleWeaponConfigurationId || "未配置";
  const off = getCombinedWeaponSlot(item, "offHand")?.singleWeaponConfigurationId || "未配置";
  const isDefault = id === state.package?.applicationConfig?.defaultCombinedWeaponConfigurationId;
  return `<button type="button" class="single-weapon-list-item${id === state.activeCombinedWeaponConfigurationId ? " active" : ""}" data-combined-action="select" data-combined-id="${escapeHtml(id)}"><span class="single-weapon-list-title"><strong>${escapeHtml(item?.displayName || id)}</strong>${isDefault ? `<span class="mini-chip">角色默认</span>` : bound ? `<span class="mini-chip">角色已引用</span>` : renderCombinedWeaponDraftBadge(getCombinedWeaponRecordStatus(item, baseline))}</span><code>${escapeHtml(id)}</code><span class="single-weapon-list-meta"><span>主 ${escapeHtml(main)}</span><span>副 ${escapeHtml(off)}</span></span><span class="single-weapon-list-summary"><span>${escapeHtml(readiness.label)}</span><span>${(effective?.actionConfiguration?.actions || []).length} Action</span></span></button>`;
}

function renderCombinedWeaponDetail(item, baseline) {
  const id = item?.combinedWeaponConfigurationId || "";
  const snapshot = getCombinedWeaponSnapshot(id);
  const effective = resolveCombinedWeaponConfiguration(item);
  const bound = isCombinedWeaponBound(id);
  const readiness = bound
    ? getCombinedWeaponReadiness(effective, snapshot, baseline, item)
    : { state: "draft", label: "当前角色未引用", missing: [] };
  const status = getCombinedWeaponRecordStatus(item, baseline);
  const isDefault = id === state.package?.applicationConfig?.defaultCombinedWeaponConfigurationId;
  return `${renderCombinedDetailHero(item, readiness, status, isDefault, bound)}
    ${renderCombinedRecipeNavigator(effective, readiness)}
    <div class="single-weapon-form-sections combined-form-sections">
      <div id="combined-recipe-basics" class="combined-workflow-section">${renderCombinedBasicsStage(item, isDefault)}</div>
      <div id="combined-recipe-behaviors" class="combined-workflow-section">${renderCombinedBehaviorSectionHumanized(item)}${renderCombinedCharacterOverrideSection(item)}${renderCombinedQuickEditorsHumanized(item, effective, snapshot)}</div>
      <div id="combined-recipe-runtime" class="combined-workflow-section">${renderCombinedGenerationSection(snapshot, readiness)}</div>
    </div>`;
}

function renderCombinedDetailHero(item, readiness, status, isDefault, bound) {
  const id = item?.combinedWeaponConfigurationId || "";
  return `<header class="combined-detail-hero"><div class="combined-detail-title"><span class="combined-eyebrow">公共组合配置</span><div><strong>${escapeHtml(item?.displayName || id)}</strong>${isDefault ? `<span class="mini-chip accent">角色默认</span>` : bound ? `<span class="mini-chip">角色已引用</span>` : ""}</div><code>${escapeHtml(id)}</code></div><div class="combined-detail-actions"><span class="sync-badge ${readiness.state === "ready" ? "ok" : readiness.state === "blocked" ? "error" : "warn"}">${escapeHtml(readiness.label)}</span>${renderCombinedWeaponDraftBadge(status)}<button type="button" data-combined-action="copy" data-combined-id="${escapeHtml(id)}">复制公共配置</button><button type="button" data-combined-action="toggleBinding" data-combined-id="${escapeHtml(id)}">${bound ? "从当前角色移除" : "用于当前角色"}</button><button type="button" data-combined-action="default" data-combined-id="${escapeHtml(id)}"${isDefault ? " disabled" : ""}>设为角色默认</button><button type="button" class="danger subtle" data-combined-action="delete" data-combined-id="${escapeHtml(id)}">删除公共配置</button></div></header>`;
}

function renderCombinedRecipeNavigator(item, readiness) {
  const slotsReady = ["mainHand", "offHand"].every(slotId => Boolean(getCombinedWeaponSlot(item, slotId)?.singleWeaponConfigurationId));
  const behaviorsReady = Boolean(item?.animationConfigurationId && item?.reactionConfigurationId);
  const actionsReady = Boolean((item?.actionConfiguration?.actions || []).some(action => action?.actionRole && action?.actionId));
  const steps = [
    ["combined-recipe-basics", "1", "组合内容", slotsReady, "主手、副手与名称"],
    ["combined-recipe-behaviors", "2", "共享行为", behaviorsReady, "动画与受击"],
    ["combined-private-actions", "3", "动作默认与覆盖", actionsReady, "动作槽与角色映射"],
    ["combined-recipe-runtime", "4", "Runtime", readiness.state === "ready", "生成状态与诊断"]
  ];
  return `<nav class="combined-recipe-navigator" aria-label="组合配置编辑步骤">${steps.map(([target, number, label, ready, help]) => `<button type="button" data-combined-action="jump" data-combined-target="${target}" class="${ready ? "complete" : "pending"}"><span>${number}</span><strong>${label}</strong><small>${help}</small></button>`).join("")}</nav>`;
}

function renderCombinedIdentityCompact(item, isDefault) {
  return `<article class="combined-identity-card"><label><span>公共组合名称</span><input data-combined-field="displayName" value="${escapeHtml(item?.displayName || "")}" placeholder="例如：剑 / 盾"></label><div><span>全局稳定 ID</span><code>${escapeHtml(item?.combinedWeaponConfigurationId || "")}</code><small>${isDefault ? "当前角色默认引用此公共组合" : "创建后锁定；所有角色引用同一 ID"}</small></div></article>`;
}

function renderCombinedBasicsStage(item, isDefault) {
  return `<section class="combined-stage-card basics-stage"><header><div><span class="combined-stage-number">1</span><div><h4>定义公共组合内容</h4><p>主手和副手属于公共配置；角色不能覆盖槽位，避免同一 ID 表示不同武器。</p></div></div><span class="combined-stage-hint">角色运行时只选择整个组合</span></header>${renderCombinedIdentityCompact(item, isDefault)}${renderCombinedSlotSection(item)}</section>`;
}

function renderCombinedSlotSection(item) {
  return `<article class="single-weapon-form-section"><h4>公共主手 / 副手配置</h4><div class="weapon-configuration-slots">${renderCombinedSlotSelector(item, "mainHand", "主手")}${renderCombinedSlotSelector(item, "offHand", "副手")}</div><p class="field-help">槽位是公共组合的身份，不允许角色覆盖；空手必须显式选择 weapon.unarmed。</p></article>`;
}

function renderCombinedSlotSelector(item, slotId, label) {
  const slot = getCombinedWeaponSlot(item, slotId);
  const definitions = (state.weaponLibrary?.singleWeaponConfigurations || []).filter(definition =>
    (definition?.allowedSlots || []).includes(slotId));
  const options = definitions.map(definition => `<option value="${escapeHtml(definition?.singleWeaponConfigurationId || "")}"${definition?.singleWeaponConfigurationId === slot?.singleWeaponConfigurationId ? " selected" : ""}>${escapeHtml(definition?.displayName || definition?.singleWeaponConfigurationId || "未命名")}</option>`).join("");
  const characterSlot = (state.package?.geometry?.weaponSlots || []).find(item => item?.slotId === slotId);
  const detail = slot?.singleWeaponConfigurationId === "weapon.unarmed" ? "显式空手" : characterSlot ? `角色槽位：${characterSlot.attachSocketId || "未绑定 Socket"}` : "缺少角色武器槽位";
  return `<article class="weapon-configuration-slot"><label><span>${label}单一武器</span><select data-combined-slot="${slotId}">${options}</select></label><small>${escapeHtml(detail)}</small></article>`;
}

function renderCombinedBehaviorSectionHumanized(item) {
  return `<section class="combined-stage-card"><header><div><span class="combined-stage-number">2</span><div><h4>设置公共行为默认值</h4><p>所有引用角色默认继承这里的动画、受击与动作配置。</p></div></div><span class="combined-stage-hint">修改后影响未覆盖的角色</span></header><div class="combined-behavior-picker-grid">${renderCombinedBehaviorPicker("animation", "动画", "公共默认姿态、移动与动作片段", item?.animationConfigurationId)}${renderCombinedBehaviorPicker("reaction", "受击", "公共默认受击语义与 ActionRole", item?.reactionConfigurationId)}</div></section>`;
}

function renderCombinedCharacterOverrideSection(item) {
  const id = item?.combinedWeaponConfigurationId || "";
  const binding = getCombinedWeaponBinding(id);
  const overrides = binding?.overrides || {};
  if (!binding) {
    return `<section class="combined-stage-card"><header><div><span class="combined-stage-number">3</span><div><h4>当前角色覆盖</h4><p>此公共组合尚未用于当前角色；启用后默认完整继承公共行为。</p></div></div><button type="button" data-combined-action="toggleBinding" data-combined-id="${escapeHtml(id)}">用于当前角色</button></header></section>`;
  }
  return `<section class="combined-stage-card"><header><div><span class="combined-stage-number">3</span><div><h4>当前角色覆盖</h4><p>留空即继承公共默认；只保存当前角色确实不同的字段。能力配置仍按当前只读兼容边界处理。</p></div></div><span class="combined-stage-hint">槽位始终继承，不能覆盖</span></header><div class="combined-behavior-picker-grid">${renderCombinedOverridePicker("animation", "动画", item?.animationConfigurationId, overrides.animationConfigurationId)}${renderCombinedOverridePicker("reaction", "受击", item?.reactionConfigurationId, overrides.reactionConfigurationId)}</div></section>`;
}

function getCharacterAnimationTargetRig() {
  const animation = state.package?.animationAuthoring || {};
  const bodyKind = String(state.package?.geometry?.bodyProfile?.bodyKind || "");
  const model = (state.package?.resourceCatalog?.entries || []).find(entry =>
    String(entry?.usage || "") === "characterModel");
  const tags = (model?.tags || []).map(value => String(value || "").toLowerCase());
  const declaredRigType = String(animation.rigType || "");
  const explicitRigType = ["Humanoid", "Generic"].find(value => value.toLowerCase() === declaredRigType.toLowerCase()) || "";
  const legacyHumanoid = !declaredRigType && bodyKind !== "Primitive" && (tags.includes("humanoid") ||
    /humanoid/i.test(String(animation.skeletonProfileId || "")) ||
    /humanoid/i.test(String(animation.avatarProfileId || "")) ||
    [...(animation.sets || []), ...(animation.profiles || [])].some(value =>
      /^humanoid$/i.test(String(value?.compatibility?.metadata?.rigType || ""))));
  return {
    rigType: explicitRigType || (legacyHumanoid ? "Humanoid" : ""),
    skeletonProfileId: String(animation.skeletonProfileId || ""),
    avatarProfileId: String(animation.avatarProfileId || ""),
    humanoid: (explicitRigType || (legacyHumanoid ? "Humanoid" : "")) === "Humanoid"
  };
}

function getAnimationConfigurationCompatibility(configuration) {
  if (!configuration) return { kind: "missing", label: "未选择", compatible: false, description: "尚未选择动画配置" };
  const target = getCharacterAnimationTargetRig();
  const sourceSkeleton = String(configuration.skeletonProfileId || "");
  const sourceAvatar = String(configuration.avatarProfileId || "");
  const contracts = [
    ...(configuration.sets || []).map(value => value?.compatibility),
    ...(configuration.profiles || []).map(value => value?.compatibility)
  ];
  const declaredSourceRigType = String(configuration.rigType || "");
  const explicitSourceRigType = ["Humanoid", "Generic"].find(value => value.toLowerCase() === declaredSourceRigType.toLowerCase()) || "";
  const legacySourceHumanoid = !declaredSourceRigType && (
    /humanoid/i.test(sourceSkeleton) || /humanoid/i.test(sourceAvatar) || contracts.some(value =>
      /^humanoid$/i.test(String(value?.metadata?.rigType || ""))));
  const sourceRigType = explicitSourceRigType || (legacySourceHumanoid ? "Humanoid" : "");
  if (!target.rigType) {
    return { kind: "incompatible", label: "未声明 Rig", compatible: false,
      description: "Skeletal 角色必须明确声明 rigType；不会猜测为 Generic" };
  }
  if (!sourceRigType) {
    return { kind: "incompatible", label: "源 Rig 未声明", compatible: false,
      description: "动画源必须声明 rigType；仅旧 Humanoid 身份可兼容推断" };
  }
  if (target.rigType !== sourceRigType) {
    return { kind: "incompatible", label: "Rig 不匹配", compatible: false,
      description: `当前角色为 ${target.rigType}，动画源为 ${sourceRigType}` };
  }
  const native = Boolean(sourceSkeleton) && sourceSkeleton === target.skeletonProfileId &&
    (!sourceAvatar || sourceAvatar === target.avatarProfileId);
  if (native) {
    return { kind: "native", label: "原生匹配", compatible: true,
      description: "动画配置与当前角色使用相同的 Skeleton / Avatar 身份" };
  }

  const allowsRetargeting = contracts.length > 0 && contracts.every(value => value?.allowRetargeting === true);
  if (target.humanoid && sourceRigType === "Humanoid" && allowsRetargeting) {
    return { kind: "retarget", label: "Humanoid 重定向", compatible: true,
      description: "跨角色复用：Unity 将通过当前角色自己的 Humanoid Avatar 重定向动画" };
  }

  const reason = target.rigType === "Generic" ? "Generic 只允许相同 Skeleton / Avatar 原生复用" :
    !target.humanoid ? "当前角色不是 Humanoid" : "动画配置未完整允许 Retargeting";
  return { kind: "incompatible", label: "不兼容", compatible: false, description: reason };
}

function renderAnimationConfigurationOption(configuration, selectedId, prefix = "") {
  const id = configuration?.animationConfigurationId || "";
  const compatibility = getAnimationConfigurationCompatibility(configuration);
  const label = configuration?.displayName || id || "未命名";
  return `<option value="${escapeHtml(id)}"${id === selectedId ? " selected" : ""}${compatibility.compatible ? "" : " disabled"}>${escapeHtml(prefix + label)}（${escapeHtml(compatibility.label)}）</option>`;
}

function renderCombinedOverridePicker(type, label, publicId, overrideId) {
  const definitions = {
    animation: ["animationConfigurations", "animationConfigurationId"],
    ability: ["abilityConfigurations", "abilityConfigurationId"],
    reaction: ["reactionConfigurations", "reactionConfigurationId"]
  };
  const [collection, idField] = definitions[type];
  const items = state.weaponLibrary?.[collection] || [];
  const inheritedLabel = publicId ? `继承公共默认（${publicId}）` : "继承公共默认（当前未配置）";
  const options = type === "animation"
    ? items.map(value => renderAnimationConfigurationOption(value, overrideId, "覆盖为：")).join("")
    : items.map(value => `<option value="${escapeHtml(value?.[idField] || "")}"${value?.[idField] === overrideId ? " selected" : ""}>覆盖为：${escapeHtml(value?.displayName || value?.[idField] || "未命名")}</option>`).join("");
  const selected = type === "animation" && overrideId
    ? items.find(value => value?.[idField] === overrideId) : null;
  const compatibility = selected ? getAnimationConfigurationCompatibility(selected) : null;
  return `<article class="combined-behavior-picker ${overrideId ? "configured" : ""}"><div class="combined-behavior-picker-head"><div><strong>${label}覆盖</strong><small>${overrideId ? compatibility?.description || "当前角色使用覆盖值" : "当前角色继承公共默认"}</small></div><span class="sync-badge ${overrideId ? compatibility?.compatible === false ? "error" : "warn" : "ok"}">${overrideId ? compatibility?.label || "已覆盖" : "继承"}</span></div><label><span>当前角色</span><select data-combined-override-field="${idField}"><option value="">${escapeHtml(inheritedLabel)}</option>${options}</select></label></article>`;
}

function renderCombinedBehaviorPicker(type, label, description, selectedId) {
  const definitions = {
    animation: ["animationConfigurations", "animationConfigurationId"],
    ability: ["abilityConfigurations", "abilityConfigurationId"],
    reaction: ["reactionConfigurations", "reactionConfigurationId"]
  };
  const [collection, idField] = definitions[type];
  const items = state.weaponLibrary?.[collection] || [];
  const selected = items.find(item => item?.[idField] === selectedId) || null;
  if (selectedId) globalThis.requestGlobalConfigurationReferences?.(type, selectedId);
  const referenceState = state.globalConfigurationReferences?.[`${type}:${selectedId}`];
  const referenceCount = referenceState?.references?.length;
  const optional = type === "ability";
  const compatibility = type === "animation" && selected ? getAnimationConfigurationCompatibility(selected) : null;
  const options = type === "animation"
    ? items.map(value => renderAnimationConfigurationOption(value, selectedId)).join("")
    : items.map(value => `<option value="${escapeHtml(value?.[idField] || "")}"${value?.[idField] === selectedId ? " selected" : ""}>${escapeHtml(value?.displayName || value?.[idField] || "未命名")}</option>`).join("");
  const badgeLabel = compatibility?.label || (selected ? "已设置" : optional ? "可不配置" : "待补齐");
  const badgeClass = compatibility?.compatible === false ? "error" : selected || optional ? "ok" : "warn";
  const pickerDescription = compatibility?.description || description;
  return `<article class="combined-behavior-picker ${selected || optional ? "configured" : "missing"}"><div class="combined-behavior-picker-head"><span class="behavior-kind ${type}">${label.slice(0, 1)}</span><div><strong>${label}公共默认</strong><small>${escapeHtml(pickerDescription)}</small></div><span class="sync-badge ${badgeClass}">${escapeHtml(badgeLabel)}</span></div><label><span>公共默认</span><select data-combined-field="${idField}"><option value="">${optional ? "未配置（无能力）" : "未选择（保存为草稿）"}</option>${options}</select></label><div class="combined-behavior-picker-meta"><code>${escapeHtml(selectedId || (optional ? "无能力" : "无引用"))}</code><span>${selected ? referenceCount == null ? "正在读取引用范围" : `影响 ${referenceCount} 个角色 / 组合` : optional ? "无能力也是合法完整配置" : "选择后才能编译与导入"}</span></div><div class="combined-behavior-picker-actions"><button type="button" data-global-config-type="${type}" data-global-quick-action="newRebind">新建并设为公共默认</button>${selected ? `<button type="button" data-global-config-type="${type}" data-global-quick-action="copyRebind">复制并设为公共默认</button>` : ""}${type === "animation" && selected ? `<button type="button" class="primary" data-combined-action="openAnimationEditor">打开编辑器</button>` : type === "reaction" && selected ? `<button type="button" class="primary" data-combined-action="editReaction">编辑详情</button>` : `<button type="button" data-combined-action="jump" data-combined-target="combined-${type}-summary"${selected ? "" : " disabled"}>查看详情</button>`}</div></article>`;
}

function getGlobalBehaviorItem(type, id) {
  const definitions = {
    animation: ["animationConfigurations", "animationConfigurationId"],
    reaction: ["reactionConfigurations", "reactionConfigurationId"]
  };
  const definition = definitions[type];
  if (!definition) return null;
  const [collection, idField] = definition;
  return (state.weaponLibrary?.[collection] || []).find(item => item?.[idField] === id) || null;
}

function countAnimationConfiguration(configuration) {
  const result = { sets: 0, profiles: 0, groups: 0, clips: 0, blends: 0, timelines: 0, locomotion: false };
  if (!configuration) return result;
  result.sets = (configuration.sets || []).length;
  result.profiles = (configuration.profiles || []).length;
  for (const set of configuration.sets || []) {
    for (const group of set.groups || []) {
      result.groups++;
      result.clips += (group.clips || []).length;
      result.blends += (group.blend1D || []).length + (group.blend2D || []).length;
      result.timelines += (group.timelines || []).length;
      if (String(group.usage || "").toLowerCase() === "locomotion") result.locomotion = true;
    }
  }
  for (const profile of configuration.profiles || []) {
    if ((profile.slots || []).some(slot => String(slot.slotId || "").toLowerCase() === "locomotion")) result.locomotion = true;
  }
  return result;
}

function renderCombinedAnimationSummary(item, snapshot) {
  const configuration = getGlobalBehaviorItem("animation", item?.animationConfigurationId);
  if (!configuration) return `<article class="behavior-summary-card blocked"><strong>动画配置未选择或引用缺失</strong><span>可保存草稿，但 Compile / Unity Import 会被阻止。</span><button type="button" data-global-config-type="animation" data-global-quick-action="newRebind">新建并绑定</button></article>`;
  const count = countAnimationConfiguration(configuration);
  const refs = snapshot?.animationReferences || [];
  const diagnostics = [];
  if (!configuration.skeletonProfileId) diagnostics.push("缺少 Skeleton Profile");
  if (!configuration.avatarProfileId) diagnostics.push("缺少 Avatar Profile");
  if (!count.locomotion) diagnostics.push("locomotion 不完整");
  const compatibility = getAnimationConfigurationCompatibility(configuration);
  if (!compatibility.compatible) diagnostics.push(compatibility.description);
  return `<article class="behavior-summary-card"><header><div><strong>动画配置：${escapeHtml(configuration.displayName || configuration.animationConfigurationId)}</strong><code>${escapeHtml(configuration.animationConfigurationId)}</code></div><span class="sync-badge ${compatibility.compatible ? "ok" : "error"}">${escapeHtml(compatibility.label)}</span></header><p class="field-help">${escapeHtml(compatibility.description)}</p><div class="behavior-summary-grid"><span>Rig Type<strong>${escapeHtml(configuration.rigType || "未声明")}</strong></span><span>Skeleton<strong>${escapeHtml(configuration.skeletonProfileId || "未设置")}</strong></span><span>Avatar<strong>${escapeHtml(configuration.avatarProfileId || "未设置")}</strong></span><span>Set / Profile<strong>${count.sets} / ${count.profiles}</strong></span><span>Group / Clip<strong>${count.groups} / ${count.clips}</strong></span><span>Blend / Timeline<strong>${count.blends} / ${count.timelines}</strong></span><span>引用范围<strong>${refs.length} 角色 / 组合</strong></span></div>${diagnostics.length ? `<div class="combined-weapon-diagnostics">${diagnostics.map(value => `<span>${escapeHtml(value)}</span>`).join("")}</div>` : ""}<div class="weapon-configuration-actions"><button type="button" data-combined-action="openAnimationEditor">打开 Animation Editor</button><button type="button" data-global-config-type="animation" data-global-quick-action="newRebind">新建并绑定</button><button type="button" data-global-config-type="animation" data-global-quick-action="copyRebind">复制并重绑</button></div><details class="behavior-readonly-json"><summary>只读 JSON 审计</summary><pre>${escapeHtml(JSON.stringify(configuration, null, 2))}</pre></details></article>`;
}

function renderCombinedReactionSummary(item, snapshot) {
  const configuration = getGlobalBehaviorItem("reaction", item?.reactionConfigurationId);
  if (!configuration) return `<article class="behavior-summary-card blocked"><strong>受击配置未选择或引用缺失</strong><span>可保存草稿，但 Compile / Unity Import 会被阻止。</span><button type="button" data-global-config-type="reaction" data-global-quick-action="newRebind">新建并绑定</button></article>`;
  const slots = configuration.slots || [];
  const action = item?.actionConfiguration || {};
  const coverage = getReactionBindingCoverage(configuration, action);
  const resources = slots.filter(slot => slot?.resourceKey || slot?.resourceSelection?.runtimeResourceKey).length;
  const refs = snapshot?.reactionReferences || [];
  return `<article class="behavior-summary-card"><header><div><strong>受击配置：${escapeHtml(configuration.displayName || configuration.reactionConfigurationId)}</strong><code>${escapeHtml(configuration.reactionConfigurationId)}</code></div><span class="sync-badge ${coverage.minimumReady ? "ok" : "warn"}">${coverage.minimumReady ? "最小运行已就绪" : `必需 ${coverage.requiredResolved}/${coverage.requiredCount}`}</span></header><p class="field-help">必需槽位决定是否可运行；未绑定的可选受击槽位会使用默认 Slot <code>${escapeHtml(configuration.defaultSlotId || "未设置")}</code>，生命周期也会走各自的安全降级。</p><div class="behavior-summary-grid"><span>默认 Slot<strong>${escapeHtml(configuration.defaultSlotId || "未设置")}</strong></span><span>必需槽位<strong>${coverage.requiredResolved}/${coverage.requiredCount}</strong></span><span>可选增强<strong>${coverage.optionalResolved}/${coverage.optionalCount}</strong></span><span>动画资源覆盖<strong>${resources}/${slots.length}</strong></span><span>引用范围<strong>${refs.length} 角色 / 组合</strong></span></div><div class="weapon-configuration-actions"><button type="button" data-combined-action="editReaction">编辑受击配置</button><button type="button" data-global-config-type="reaction" data-global-quick-action="newRebind">新建并绑定</button><button type="button" data-global-config-type="reaction" data-global-quick-action="copyRebind">复制并重绑</button></div><details class="behavior-readonly-json"><summary>只读 JSON 审计</summary><pre>${escapeHtml(JSON.stringify(configuration, null, 2))}</pre></details></article>`;
}

function getReactionBindingCoverage(configuration, actionConfiguration) {
  const slots = configuration?.slots || [];
  const required = slots.filter(slot => slot?.required || slot?.slotId === configuration?.defaultSlotId);
  const optional = slots.filter(slot => !required.includes(slot));
  const isResolved = slot => globalThis.resolveReactionActionRole?.(
    actionConfiguration || {},
    slot?.actionRole || "")?.state === "resolved";
  const requiredResolved = required.filter(isResolved).length;
  const optionalResolved = optional.filter(isResolved).length;
  return {
    requiredCount: required.length,
    requiredResolved,
    optionalCount: optional.length,
    optionalResolved,
    minimumReady: required.length > 0 && requiredResolved === required.length
  };
}

function renderCombinedQuickEditorsHumanized(publicItem, effectiveItem, snapshot) {
  const publicAction = publicItem?.actionConfiguration || { actions: [], commandSequences: [], abilityActionBindings: [], reactionActionBindings: [] };
  const binding = getCombinedWeaponBinding(publicItem?.combinedWeaponConfigurationId || "");
  const overrideAction = binding?.overrides?.actionConfiguration;
  const effectiveAction = effectiveItem?.actionConfiguration || publicAction;
  const reactionConfiguration = getGlobalBehaviorItem("reaction", effectiveItem?.reactionConfigurationId);
  const mappedRoles = new Set((effectiveAction.reactionActionBindings || []).map(entry => entry?.actionRole).filter(Boolean));
  const profileId = state.characterActionCatalog?.profileId || "待编译生成";
  const actionSetId = getCharacterActionSetId(publicItem?.combinedWeaponConfigurationId || "");
  return `<section class="combined-stage-card shared-detail-stage"><header><div><span class="combined-stage-number">4</span><div><h4>选择角色实际使用的动作</h4><p>CharacterStudio 只选择动作槽；时间轴、命中、位移和表现请在 Unity Character Action Authoring 中编辑。</p></div></div></header><div class="weapon-configuration-quick-edit"><div id="combined-animation-summary" class="combined-summary-anchor">${renderCombinedAnimationSummary(effectiveItem, snapshot)}</div><div id="combined-reaction-summary" class="combined-summary-anchor">${renderCombinedReactionSummary(effectiveItem, snapshot)}</div><article id="combined-private-actions" class="combined-private-action-card"><header><div><strong>公共默认 CharacterAction / ActionSet</strong><span>公共组合只能引用 Common 动作库</span></div><div class="combined-private-action-stats"><span><strong>${(publicAction.actions || []).length}</strong> Actions</span><span><strong>${mappedRoles.size}</strong> 最终受击 Role 映射</span></div></header><div class="combined-private-action-identity"><dl><dt>Profile</dt><dd><code>${escapeHtml(profileId)}</code></dd><dt>ActionSet</dt><dd><code>${escapeHtml(actionSetId)}</code></dd><dt>动作库</dt><dd><code>Common</code></dd></dl><p>这些 ID 由编译器确定性生成，不能手填。</p></div>${renderCharacterActionRoleTable("public", publicAction, reactionConfiguration)}<details class="behavior-readonly-json"><summary>只读 JSON 审计</summary><pre>${escapeHtml(JSON.stringify(publicAction, null, 2))}</pre></details></article>${binding ? `<article class="combined-private-action-card"><header><div><strong>当前角色动作覆盖</strong><span>${overrideAction ? "完整替换公共映射" : "正在继承公共默认"}</span></div><button type="button" data-combined-action="toggleActionOverride">${overrideAction ? "恢复继承" : "创建角色覆盖"}</button></header>${overrideAction ? `${renderCharacterActionRoleTable("character", overrideAction, reactionConfiguration)}<p class="field-help">角色覆盖可引用 Common 或当前角色动作库；第一次修改会复制完整公共映射。</p><details class="behavior-readonly-json"><summary>只读 JSON 审计</summary><pre>${escapeHtml(JSON.stringify(overrideAction, null, 2))}</pre></details>` : `<p class="field-help">当前角色没有保存动作副本；首次修改任一动作槽时会复制完整公共映射。</p>`}</article>` : ""}</div></section>`;
}

function renderCombinedGenerationSection(snapshot, readiness) {
  const diagnostics = snapshot?.diagnostics || [];
  return `<article class="single-weapon-form-section"><h4>Compiler / Runtime 投影</h4><div class="single-weapon-readonly-summary">${renderCombinedReadOnlyGroup("生成标识", [["Loadout", snapshot?.loadoutStableId || "未生成"], ["EquipmentState", snapshot?.equipmentStateStableId || "未生成"], ["Animation Profile", snapshot?.generatedAnimationProfileId || "未生成"], ["ActionSet", snapshot?.generatedActionSetId || "未生成"]])}</div>${diagnostics.length ? `<div class="combined-weapon-diagnostics">${diagnostics.map(issue => `<span><code>${escapeHtml(issue?.code || "DIAGNOSTIC")}</code>${escapeHtml(issue?.message || "")}</span>`).join("")}</div>` : `<p class="field-help">${escapeHtml(readiness.state === "ready" ? "当前保存版本已生成唯一 EquipmentState。" : "保存并重新编译后刷新生成快照。")}</p>`}</article>`;
}

function renderCombinedWeaponOverview(items, baseline) {
  const visible = getFilteredCombinedWeaponItems(items, baseline);
  const snapshots = new Map(getCombinedWeaponSnapshots().map(item => [item?.combinedWeaponConfigurationId || "", item]));
  const summary = getCombinedWeaponDraftSummary(items, baseline);
  const boundItems = items.filter(item => isCombinedWeaponBound(item?.combinedWeaponConfigurationId));
  const readyCount = boundItems.filter(item => getCombinedWeaponReadiness(resolveCombinedWeaponConfiguration(item), snapshots.get(item?.combinedWeaponConfigurationId), baseline, item).state === "ready").length;
  const blockedCount = boundItems.filter(item => getCombinedWeaponReadiness(resolveCombinedWeaponConfiguration(item), snapshots.get(item?.combinedWeaponConfigurationId), baseline, item).state === "blocked").length;
  return `<div class="single-weapon-overview combined-weapon-overview"><div class="single-weapon-overview-stats">${renderCombinedWeaponStat("公共配置", items.length)}${renderCombinedWeaponStat("当前角色引用", boundItems.length, "ok")}${renderCombinedWeaponStat("可生成", readyCount, readyCount === boundItems.length ? "ok" : "")}${renderCombinedWeaponStat("编译阻断", blockedCount, blockedCount ? "warn" : "ok")}${renderCombinedWeaponStat("公共草稿变更", summary.changed, summary.changed ? "warn" : "ok")}</div>${renderCombinedWeaponFilters()}<div class="single-weapon-overview-table-wrap"><table class="single-weapon-overview-table combined-weapon-overview-table"><thead><tr><th>公共组合</th><th>主手 / 副手</th><th>角色最终动画 / 受击</th><th>最终动作</th><th>角色状态</th><th>公共草稿</th><th>操作</th></tr></thead><tbody>${visible.map(item => renderCombinedWeaponOverviewRow(item, baseline)).join("") || `<tr><td colspan="7"><div class="empty">没有匹配配置。</div></td></tr>`}</tbody></table></div></div>`;
}

function renderCombinedWeaponOverviewRow(item, baseline) {
  const id = item?.combinedWeaponConfigurationId || "";
  const snapshot = getCombinedWeaponSnapshot(id);
  const bound = isCombinedWeaponBound(id);
  const effective = resolveCombinedWeaponConfiguration(item);
  const readiness = bound
    ? getCombinedWeaponReadiness(effective, snapshot, baseline, item)
    : { state: "draft", label: "当前角色未引用" };
  const status = getCombinedWeaponRecordStatus(item, baseline);
  const expanded = state.combinedWeaponExpandedOverviewId === id;
  const main = getCombinedWeaponSlot(item, "mainHand");
  const off = getCombinedWeaponSlot(item, "offHand");
  const isDefault = id === state.package?.applicationConfig?.defaultCombinedWeaponConfigurationId;
  return `<tr><td><strong>${escapeHtml(item?.displayName || id)}${isDefault ? " · 角色默认" : bound ? " · 已引用" : ""}</strong><code>${escapeHtml(id)}</code></td><td><span>主：${escapeHtml(main?.singleWeaponConfigurationId || "未配置")}</span><span>副：${escapeHtml(off?.singleWeaponConfigurationId || "未配置")}</span></td><td><span>动画：${escapeHtml(effective?.animationConfigurationId || "未选择")}</span><span>受击：${escapeHtml(effective?.reactionConfigurationId || "未选择")}</span></td><td><span>${(effective?.actionConfiguration?.actions || []).length} Action</span><span>${escapeHtml(getCharacterActionSetId(id))}</span></td><td><span class="sync-badge ${readiness.state === "ready" ? "ok" : readiness.state === "blocked" ? "error" : "warn"}">${escapeHtml(readiness.label)}</span></td><td>${renderCombinedWeaponDraftBadge(status)}</td><td><div class="single-weapon-row-actions"><button type="button" data-combined-action="editOverview" data-combined-id="${escapeHtml(id)}">编辑</button><button type="button" data-combined-action="copy" data-combined-id="${escapeHtml(id)}">复制</button><button type="button" data-combined-action="toggleOverview" data-combined-id="${escapeHtml(id)}">${expanded ? "收起" : "展开"}</button><button type="button" class="danger" data-combined-action="delete" data-combined-id="${escapeHtml(id)}">删除公共配置</button></div></td></tr>${expanded ? `<tr class="single-weapon-overview-expanded"><td colspan="7">${renderCombinedWeaponReadOnlySummary(effective, snapshot)}</td></tr>` : ""}`;
}

function renderCombinedWeaponReadOnlySummary(item, snapshot) {
  const main = getCombinedWeaponSlot(item, "mainHand");
  const off = getCombinedWeaponSlot(item, "offHand");
  return `<div class="single-weapon-readonly-summary">${renderCombinedReadOnlyGroup("身份", [["ID", item?.combinedWeaponConfigurationId], ["名称", item?.displayName]])}${renderCombinedReadOnlyGroup("主副手", [["主手", main?.singleWeaponConfigurationId || "未配置"], ["副手", off?.singleWeaponConfigurationId || "未配置"]])}${renderCombinedReadOnlyGroup("共享行为配置", [["动画", item?.animationConfigurationId || "未选择"], ["受击", item?.reactionConfigurationId || "未选择"]])}${renderCombinedReadOnlyGroup("最终动作", [["Profile", state.characterActionCatalog?.profileId || "未生成"], ["ActionSet", getCharacterActionSetId(item?.combinedWeaponConfigurationId || "")], ["Actions", (item?.actionConfiguration?.actions || []).length], ["受击绑定", (item?.actionConfiguration?.reactionActionBindings || []).length]])}${renderCombinedReadOnlyGroup("Runtime", [["EquipmentState", snapshot?.equipmentStateStableId || "未生成"], ["Generation", snapshot?.generationStatus || "Unknown"], ["Diagnostics", (snapshot?.diagnostics || []).length]])}</div>`;
}

function renderCombinedReadOnlyGroup(title, rows) {
  return `<section><h5>${escapeHtml(title)}</h5><dl>${rows.map(([label, value]) => `<dt>${escapeHtml(label)}</dt><dd>${escapeHtml(value ?? "")}</dd>`).join("")}</dl></section>`;
}

function renderCombinedWeaponStat(label, value, tone = "") {
  return `<article class="single-weapon-stat ${tone}"><strong>${escapeHtml(value)}</strong><span>${escapeHtml(label)}</span></article>`;
}

function renderCombinedWeaponDraftBadge(status) {
  if (status === "new") return `<span class="sync-badge pending">新增</span>`;
  if (status === "modified") return `<span class="sync-badge warn">已修改</span>`;
  return `<span class="sync-badge ok">已保存</span>`;
}

function renderCombinedWeaponCreationDialog() {
  const dialog = state.combinedWeaponCreationDialog;
  if (!dialog) return "";
  const copy = dialog.mode === "copy";
  return `<div class="single-weapon-dialog-backdrop"><form class="single-weapon-dialog" data-combined-create-form><div><strong>${copy ? "复制公共组合武器配置" : "新建公共组合武器配置"}</strong><span>${copy ? "复制槽位、共享行为引用和公共动作默认；角色覆盖不会随之复制。" : "新配置从双空手和未选择行为默认的草稿开始。"}</span></div><label><span>稳定 ID</span><input required autofocus data-combined-create-field="id" value="${escapeHtml(dialog.id || "")}"></label><label><span>显示名称</span><input required data-combined-create-field="displayName" value="${escapeHtml(dialog.displayName || "")}"></label>${dialog.error ? `<p class="single-weapon-dialog-error">${escapeHtml(dialog.error)}</p>` : ""}<div class="single-weapon-dialog-actions"><button type="button" data-combined-action="closeCreate">取消</button><button type="submit">${copy ? "创建副本" : "创建配置"}</button></div></form></div>`;
}

function clearCombinedWeaponFilters() {
  state.combinedWeaponSearchQuery = "";
  state.combinedWeaponReadinessFilter = "all";
  state.combinedWeaponDefaultFilter = "all";
  state.combinedWeaponSort = "id";
  render();
}

async function handleCombinedWeaponWorkbenchClick(event) {
  if (state.combinedWeaponSubWorkspace === "reaction" && event.target.closest("[data-reaction-action]")) {
    await globalThis.handleReactionWorkbenchClick?.(event);
    return;
  }
  const button = event.target.closest("button[data-combined-action]");
  if (!button || button.disabled) return;
  const action = button.dataset.combinedAction;
  const id = button.dataset.combinedId || state.activeCombinedWeaponConfigurationId;
  if (action === "setView") {
    state.combinedWeaponWorkspaceView = button.dataset.combinedView === "overview" ? "overview" : "edit";
    render();
  } else if (action === "select") {
    state.activeCombinedWeaponConfigurationId = id;
    render();
  } else if (action === "editOverview") {
    state.activeCombinedWeaponConfigurationId = id;
    state.combinedWeaponWorkspaceView = "edit";
    render();
  } else if (action === "new") openCombinedWeaponCreationDialog("new");
  else if (action === "copy") openCombinedWeaponCreationDialog("copy", id);
  else if (action === "delete") await deleteCombinedWeaponConfiguration(id);
  else if (action === "default") setCombinedWeaponAsDefault(id);
  else if (action === "toggleBinding") setCombinedWeaponBindingEnabled(id, !isCombinedWeaponBound(id));
  else if (action === "toggleActionOverride") {
    const binding = getCombinedWeaponBinding(id);
    setCombinedWeaponActionOverrideEnabled(!binding?.overrides?.actionConfiguration);
  }
  else if (action === "openCharacterAction") {
    await openCharacterActionInUnity(button.dataset.actionId || "");
  }
  else if (action === "copyCharacterAction") {
    await copyCharacterActionToCharacter(
      button.dataset.actionRole || "",
      button.dataset.actionId || "");
  }
  else if (action === "refreshCharacterActionStatus") {
    await refreshCharacterActionOpenStatus(
      button.dataset.requestKey || "",
      button.dataset.requestId || "");
  }
  else if (action === "save") {
    button.classList.add("is-busy");
    button.disabled = true;
    try {
      await savePackage();
    } finally {
      button.classList.remove("is-busy");
      button.disabled = false;
    }
  }
  else if (action === "toggleOverview") {
    state.combinedWeaponExpandedOverviewId = state.combinedWeaponExpandedOverviewId === id ? "" : id;
    render();
  } else if (action === "clearFilters") clearCombinedWeaponFilters();
  else if (action === "jump") {
    const target = document.getElementById(button.dataset.combinedTarget || "");
    target?.scrollIntoView({ behavior: "smooth", block: "start" });
    target?.classList.add("attention-pulse");
    window.setTimeout(() => target?.classList.remove("attention-pulse"), 900);
  }
  else if (action === "editReaction") {
    state.activeReactionConfigurationWorkbenchId = getActiveCombinedWorkbenchConfiguration()?.reactionConfigurationId || "";
    state.activeReactionSlotId = "";
    state.combinedWeaponSubWorkspace = "reaction";
    render();
  } else if (action === "openAnimationEditor") {
    if (state.weaponLibraryDirty) {
      state.message = "请先显式保存 CharacterStudio 的全局库草稿，再打开 Animation Editor，避免两个窗口基于旧 revision 并发写入。";
      renderShellStatus();
      return;
    }
    const configurationId = getActiveCombinedWorkbenchConfiguration()?.animationConfigurationId || "";
    const url = `/Tools/MxFramework.AnimationEditor/web/?source=globalWeaponLibrary&configurationId=${encodeURIComponent(configurationId)}&package=${encodeURIComponent(state.packageRelative || "")}`;
    window.open(url, "MxFrameworkAnimationEditor");
  }
  else if (action === "closeCreate") {
    state.combinedWeaponCreationDialog = null;
    render();
  }
}

function handleCombinedWeaponWorkbenchChange(event) {
  if (state.combinedWeaponSubWorkspace === "reaction" && event.target.closest("[data-reaction-config-field], [data-reaction-slot-field]")) {
    globalThis.handleReactionWorkbenchChange?.(event);
    return;
  }
  const filter = event.target.closest("[data-combined-filter]");
  if (filter) {
    const key = filter.dataset.combinedFilter;
    if (key === "readiness") state.combinedWeaponReadinessFilter = filter.value || "all";
    else if (key === "defaultState") state.combinedWeaponDefaultFilter = filter.value || "all";
    else if (key === "sort") state.combinedWeaponSort = filter.value || "id";
    render();
    return;
  }
  const slot = event.target.closest("[data-combined-slot]");
  if (slot) {
    setCombinedWeaponSlot(slot.dataset.combinedSlot, slot.value || "weapon.unarmed");
    return;
  }
  const actionRole = event.target.closest("[data-character-action-role]");
  if (actionRole) {
    setCharacterActionRole(
      actionRole.dataset.characterActionScope || "public",
      actionRole.dataset.characterActionRole || "",
      actionRole.value || "");
    return;
  }
  const field = event.target.closest("[data-combined-field]");
  if (field) {
    updateCombinedWeaponField(field.dataset.combinedField, field.value || "");
    return;
  }
  const overrideField = event.target.closest("[data-combined-override-field]");
  if (overrideField) {
    updateCombinedWeaponOverrideField(overrideField.dataset.combinedOverrideField, overrideField.value || "");
    return;
  }
  const privateActions = event.target.closest("[data-combined-private-json]");
  if (privateActions) {
    updateCombinedPrivateActionConfiguration(privateActions.value || "");
    return;
  }
  const overrideActions = event.target.closest("[data-combined-override-json]");
  if (overrideActions) updateCombinedOverrideActionConfiguration(overrideActions.value || "");
}

function handleCombinedWeaponWorkbenchInput(event) {
  if (state.combinedWeaponSubWorkspace === "reaction" && event.target.closest("[data-reaction-search]")) {
    globalThis.handleReactionWorkbenchInput?.(event);
    return;
  }
  const creationField = event.target.closest("[data-combined-create-field]");
  if (creationField && state.combinedWeaponCreationDialog) {
    state.combinedWeaponCreationDialog[creationField.dataset.combinedCreateField] = creationField.value;
    state.combinedWeaponCreationDialog.error = "";
    return;
  }
  const search = event.target.closest("[data-combined-search]");
  if (!search) return;
  const start = search.selectionStart;
  state.combinedWeaponSearchQuery = search.value || "";
  renderCombinedWeaponWorkbench();
  const next = el.combinedWeaponConfigurationPanel?.querySelector("[data-combined-search]");
  next?.focus();
  if (next && Number.isInteger(start)) next.setSelectionRange(start, start);
}

function handleCombinedWeaponWorkbenchSubmit(event) {
  if (!event.target.closest("[data-combined-create-form]")) return;
  event.preventDefault();
  submitCombinedWeaponCreationDialog();
}

function wireCombinedWeaponWorkbench() {
  const panel = el.combinedWeaponConfigurationPanel;
  if (!panel || panel.dataset.combinedWeaponWorkbenchWired === "1") return;
  panel.dataset.combinedWeaponWorkbenchWired = "1";
  panel.addEventListener("click", handleCombinedWeaponWorkbenchClick);
  panel.addEventListener("change", handleCombinedWeaponWorkbenchChange);
  panel.addEventListener("input", handleCombinedWeaponWorkbenchInput);
  panel.addEventListener("submit", handleCombinedWeaponWorkbenchSubmit);
}

function applyCombinedWeaponWorkbenchAction(action) {
  if (action === "new") openCombinedWeaponCreationDialog("new");
  else if (action === "copy") openCombinedWeaponCreationDialog("copy");
  else if (action === "delete") void deleteCombinedWeaponConfiguration(state.activeCombinedWeaponConfigurationId);
  else if (action === "default") setCombinedWeaponAsDefault(state.activeCombinedWeaponConfigurationId);
  else return false;
  return true;
}

Object.assign(globalThis, {
  renderCombinedWeaponWorkbench,
  wireCombinedWeaponWorkbench,
  openCombinedWeaponCreationDialog,
  applyCombinedWeaponWorkbenchAction,
  getActiveCombinedWorkbenchConfiguration,
  getActiveCombinedWeaponBaseConfiguration,
  getAnimationConfigurationCompatibility,
  renderAnimationConfigurationOption,
  updateCombinedWeaponField,
  setCombinedWeaponSlot,
  updateCombinedPrivateActionConfiguration
});

globalThis.CombinedWeaponWorkbenchTest = {
  canonicalizeCombinedWeaponConfiguration,
  getCombinedWeaponRecordStatus,
  getCombinedWeaponDraftSummary,
  getCombinedWeaponCompleteness,
  getCombinedWeaponReadiness,
  filterAndSortCombinedWeaponConfigurations,
  countAnimationConfiguration,
  getAnimationConfigurationCompatibility,
  getReactionBindingCoverage,
  stripFrozenAbilityDataFromCombinedCopy
};

export {
  canonicalizeCombinedWeaponConfiguration,
  getCombinedWeaponRecordStatus,
  getCombinedWeaponDraftSummary,
  getCombinedWeaponCompleteness,
  getCombinedWeaponReadiness,
  filterAndSortCombinedWeaponConfigurations,
  countAnimationConfiguration,
  getAnimationConfigurationCompatibility,
  getReactionBindingCoverage,
  stripFrozenAbilityDataFromCombinedCopy
};
