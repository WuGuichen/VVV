// CharacterStudio split module. Registers functions on globalThis to preserve the original call graph.

function normalizeResourceLibraryPayload(payload) {
  if (!payload) return null;
  if (Array.isArray(payload)) return { items: payload };
  if (Array.isArray(payload.items)) return payload;
  if (Array.isArray(payload.entries)) return { items: payload.entries };
  return null;
}

function normalizeResourcePlanPayload(payload) {
  if (!payload) return null;
  if (payload.characterResourcePlan) return payload.characterResourcePlan;
  if (payload.CharacterResourcePlan) return payload.CharacterResourcePlan;
  if (payload.characterStableId || payload.spawnCritical || payload.groups) return payload;
  return null;
}

async function openResourcePicker() {
  if (!state.package) return;
  const role = el.resourceBindingRole?.value || "preview";
  if (role === "preview") {
    state.message = "请先选择 Body、主手或副手，再绑定已有资源。";
    renderShellStatus();
    return;
  }
  state.resourcePickerField = null;
  state.resourcePickerOpen = true;
  state.resourcePickerQuery = null;
  resetResourcePickerFilters();
  await loadResourcePickerQuery(getActiveResourceFieldSpec());
}

async function openResourcePickerForField(fieldPath) {
  if (!state.package || !fieldPath) return;
  const target = findTarget(state.selectedPath);
  if (!target?.value) return;
  const fieldSpec = getResourceFieldSpecForInspectorField(target, fieldPath);
  if (!fieldSpec) return;
  const slotDefinition = getAnimationSlotDefinition(target.value?.slotId || "");
  state.resourcePickerField = {
    kind: target.kind === "animationSlot" ? "animationSlot" : target.kind === "reactionSlot" ? "reactionSlot" : "",
    profileId: target.profileId || "",
    slotId: target.value?.slotId || "",
    targetPath: state.selectedPath,
    fieldPath,
    fieldSpec,
    title: `${target.label || target.kind}.${fieldPath}`,
    slotIntent: target.kind === "animationSlot" || target.kind === "reactionSlot" ? {
      slotId: target.value?.slotId || "",
      displayName: target.value?.displayName || slotDefinition?.displayName || target.value?.slotId || "slot",
      purpose: target.value?.purpose || target.value?.trigger || slotDefinition?.purpose || "",
      resourceHint: target.value?.resourceHint || slotDefinition?.resourceHint || "",
      required: target.value?.required ?? slotDefinition?.required ?? false
    } : null
  };
  state.resourcePickerOpen = true;
  state.resourcePickerQuery = null;
  resetResourcePickerFilters();
  await loadResourcePickerQuery(fieldSpec);
}

async function openSingleWeaponResourcePicker(singleWeaponConfigurationId) {
  const single = getSingleWeaponConfiguration(singleWeaponConfigurationId);
  if (!single || single.singleWeaponConfigurationId === "weapon.unarmed") return;
  const slot = (single.allowedSlots || []).includes("mainHand") ? "mainHand" : "offHand";
  const base = RESOURCE_FIELD_SPECS[slot] || RESOURCE_FIELD_SPECS.mainHand;
  const fieldSpec = {
    ...base,
    fieldKey: `SingleWeapon.${single.singleWeaponConfigurationId}.weaponRef`,
    displayName: `${single.displayName || single.singleWeaponConfigurationId} 武器模型`,
    compatibilityFilter: { ...(base.compatibilityFilter || {}), slotId: slot }
  };
  state.resourcePickerField = {
    kind: "singleWeapon",
    singleWeaponConfigurationId: single.singleWeaponConfigurationId,
    targetPath: "",
    fieldPath: "weaponRef",
    fieldSpec,
    title: `${single.displayName || single.singleWeaponConfigurationId}.weaponRef`
  };
  state.resourcePickerOpen = true;
  state.resourcePickerQuery = null;
  resetResourcePickerFilters();
  await loadResourcePickerQuery(fieldSpec);
}

async function openImportMaterialResourceRefPicker(itemId, slotKey) {
  if (!state.package) return;
  await loadCommonResources();
  const definition = getImportMaterialSlotDefinition(slotKey || "body");
  const existingItem = itemId ? findImportMaterialItem(itemId) : null;
  const fieldSpecBase = getImportMaterialResourceRefFieldSpec(definition, existingItem);
  const fieldSpec = {
    ...fieldSpecBase,
    fieldKey: getImportMaterialResourceRefFieldKey(definition, fieldSpecBase),
    displayName: definition?.key === "body"
      ? "公共 Body 模型"
      : definition?.key === "bodyTexture"
        ? "公共 Body 贴图"
        : fieldSpecBase.displayName || "公共源资源"
  };
  state.resourcePickerField = {
    kind: "importMaterialResourceRef",
    importMaterialItemId: itemId || "",
    slotKey: definition?.key || slotKey || "",
    targetPath: "",
    fieldPath: "source.selection",
    fieldSpec,
    commonOnly: true,
    title: `${definition?.label || "源"}公共资源`,
    slotIntent: {
      slotId: definition?.key || slotKey || "source",
      displayName: definition?.label || "源资源",
      purpose: "选择可复用公共资源。源清单只保存 resourceRef，不复制文件路径。",
      resourceHint: definition?.presetId || "resourceRef",
      required: definition?.required !== false
    }
  };
  state.resourcePickerOpen = true;
  state.resourcePickerQuery = null;
  resetResourcePickerFilters();
  state.resourcePickerLoading = false;
  state.message = `已从 ArtSource/Common 加载 ${state.commonResources?.summary?.total || 0} 个公共复用资源项。`;
  renderResourcePicker();
  renderShellStatus();
}

function getImportMaterialResourceRefFieldSpec(definition, item = null) {
  const presetId = item?.presetId || definition?.presetId || "";
  if (definition?.key === "body" || presetId === "characterModel") return RESOURCE_FIELD_SPECS.bodyPublicModel;
  if (definition?.key === "bodyTexture" || presetId === "texture") return RESOURCE_FIELD_SPECS.bodyPublicTexture;
  return RESOURCE_FIELD_SPECS.publicSourceResource;
}

function getImportMaterialResourceRefFieldKey(definition, fieldSpec) {
  if (definition?.key === "body") return "Character.Source.Body.resourceRef";
  if (definition?.key === "bodyTexture") return "Character.Source.BodyTexture.resourceRef";
  return fieldSpec?.fieldKey || "Character.Source.resourceRef";
}

async function openAnimationSlotPicker(profileId, slotId) {
  if (!state.package) return;
  const { profile, slot } = findAnimationSlot(profileId, slotId);
  if (!profile || !slot) return;
  const definition = ANIMATION_PROFILE_SLOTS.find(item => item.slotId === slot.slotId) || {};
  const fieldSpec = {
    ...RESOURCE_FIELD_SPECS.animationProfileSlot,
    fieldKey: `Animation.Profile.${slot.slotId}`,
    displayName: slot.displayName || definition.displayName || "Animation Slot",
    compatibilityFilter: { ...(RESOURCE_FIELD_SPECS.animationProfileSlot.compatibilityFilter || {}) }
  };
  state.resourcePickerField = {
    kind: "animationSlot",
    profileId: profile.profileId,
    slotId: slot.slotId,
    targetPath: getAnimationSlotPath(profile.profileId, slot.slotId),
    fieldPath: "resourceKey",
    fieldSpec,
    title: `${profile.displayName || profile.profileId}.${slot.displayName || slot.slotId}`,
    slotIntent: {
      slotId: slot.slotId,
      displayName: slot.displayName || definition.displayName || slot.slotId,
      purpose: slot.purpose || definition.purpose || "",
      resourceHint: slot.resourceHint || definition.resourceHint || "",
      required: slot.required ?? definition.required ?? false
    }
  };
  state.resourcePickerOpen = true;
  state.resourcePickerQuery = null;
  resetResourcePickerFilters();
  state.selectedPath = getAnimationSlotPath(profile.profileId, slot.slotId);
  renderTree();
  renderInspector();
  await loadResourcePickerQuery(fieldSpec);
}

async function openReactionSlotPicker(profileId, slotId) {
  if (!state.package) return;
  const { profile, slot } = findReactionSlot(profileId, slotId);
  if (!profile || !slot) return;
  const definition = REACTION_PROFILE_SLOTS.find(item => item.slotId === slot.slotId) || {};
  const fieldSpec = {
    ...RESOURCE_FIELD_SPECS.reactionAnimationSlot,
    fieldKey: `Reaction.Profile.${slot.slotId}`,
    displayName: slot.displayName || definition.displayName || "Reaction Slot",
    compatibilityFilter: { ...(RESOURCE_FIELD_SPECS.reactionAnimationSlot.compatibilityFilter || {}) }
  };
  state.resourcePickerField = {
    kind: "reactionSlot",
    profileId: profile.profileId,
    slotId: slot.slotId,
    targetPath: getReactionSlotPath(profile.profileId, slot.slotId),
    fieldPath: "resourceKey",
    fieldSpec,
    title: `${profile.displayName || profile.profileId}.${slot.displayName || slot.slotId}`,
    slotIntent: {
      slotId: slot.slotId,
      displayName: slot.displayName || definition.displayName || slot.slotId,
      purpose: `${slot.trigger || definition.trigger || "Reaction"}${slot.pressureBand ? ` / ${slot.pressureBand}` : ""}`,
      resourceHint: definition.resourceHint || slot.slotId || "",
      required: slot.required ?? definition.required ?? false
    }
  };
  state.resourcePickerOpen = true;
  state.resourcePickerQuery = null;
  resetResourcePickerFilters();
  state.selectedPath = getReactionSlotPath(profile.profileId, slot.slotId);
  renderTree();
  renderInspector();
  await loadResourcePickerQuery(fieldSpec);
}

async function openGlobalReactionSlotPicker(reactionConfigurationId, slotId) {
  const configuration = (state.weaponLibrary?.reactionConfigurations || []).find(item =>
    item?.reactionConfigurationId === reactionConfigurationId);
  const slot = (configuration?.slots || []).find(item => item?.slotId === slotId);
  if (!configuration || !slot) return;
  const fieldSpec = {
    ...RESOURCE_FIELD_SPECS.reactionAnimationSlot,
    fieldKey: `GlobalReaction.${slot.slotId}`,
    displayName: slot.displayName || slot.slotId || "Reaction Slot",
    compatibilityFilter: { ...(RESOURCE_FIELD_SPECS.reactionAnimationSlot.compatibilityFilter || {}) }
  };
  state.resourcePickerField = {
    kind: "globalReactionSlot",
    reactionConfigurationId,
    slotId,
    fieldPath: "resourceKey",
    fieldSpec,
    title: `${configuration.displayName || reactionConfigurationId}.${slot.displayName || slotId}`,
    slotIntent: {
      slotId,
      displayName: slot.displayName || slotId,
      purpose: `${slot.trigger || "Reaction"}${slot.pressureBand ? ` / ${slot.pressureBand}` : ""}`,
      required: Boolean(slot.required)
    }
  };
  state.resourcePickerOpen = true;
  state.resourcePickerQuery = null;
  resetResourcePickerFilters();
  await loadResourcePickerQuery(fieldSpec);
}

function clearAnimationSlotSelection(profileId, slotId) {
  const { slot } = findAnimationSlot(profileId, slotId);
  if (!slot) return;
  const previousResourceKey = firstNonEmpty(
    slot.resourceKey,
    slot.resourceSelection?.runtimeResourceKey,
    slot.resourceSelection?.packageResourceKey,
    slot.resourceSelection?.providerResourceKey,
    slot.resourceSelection?.resourceStableId);
  slot.resourceKey = "";
  slot.resourceSelection = {};
  if (previousResourceKey) {
    cleanupUnusedAnimationResourceKey(previousResourceKey);
  }
  state.selectedPath = getAnimationSlotPath(profileId, slotId);
  state.dirty = true;
  state.message = `${slot.displayName || slot.slotId} reference cleared. Resource assets were not deleted.`;
  render();
}

function clearReactionSlotSelection(profileId, slotId) {
  const { slot } = findReactionSlot(profileId, slotId);
  if (!slot) return;
  slot.resourceKey = "";
  slot.resourceSelection = {};
  slot.preloadPolicy = "AnimationWarmup";
  state.selectedPath = getReactionSlotPath(profileId, slotId);
  state.dirty = true;
  state.message = `${slot.displayName || slot.slotId} reaction animation cleared. Resource assets were not deleted.`;
  render();
}

function closeResourcePicker() {
  state.resourcePickerOpen = false;
  state.resourcePickerField = null;
  state.resourcePickerQuery = null;
  state.resourcePickerLoading = false;
  resetResourcePickerFilters();
  renderResourcePicker();
}

async function loadResourcePickerQuery(fieldSpec) {
  state.resourcePickerLoading = true;
  renderResourcePicker();
  const result = await postJson("/api/authoring/resources/pick", {
    package: state.packageRelative,
    fieldSpec: toAuthoringResourceFieldSpec(fieldSpec),
    context: buildResourceConsumerContext(fieldSpec),
    selection: {}
  }, null);
  state.resourcePickerQuery = result;
  state.resourcePickerLoading = false;
  renderResourcePicker();
}

function getActiveResourceFieldSpec() {
  const role = el.resourceBindingRole?.value || "preview";
  return RESOURCE_FIELD_SPECS[role] || RESOURCE_FIELD_SPECS.preview;
}

function getAnimationSlotDefinition(slotId) {
  return ANIMATION_PROFILE_SLOTS.find(item => item.slotId === slotId) || null;
}

function getActiveResourcePickerRequest() {
  if (state.resourcePickerField) {
    const fieldSpec = state.resourcePickerField.fieldSpec || getActiveResourceFieldSpec();
    return {
      ...state.resourcePickerField,
      fieldSpec,
      title: state.resourcePickerField.title || fieldSpec.displayName || "resource"
    };
  }
  const fieldSpec = getActiveResourceFieldSpec();
  const role = getResourceBindingRole();
  return {
    kind: "",
    targetPath: state.selectedPath || "",
    fieldPath: "",
    fieldSpec,
    title: fieldSpec.displayName || role.label || "resource",
    slotIntent: null
  };
}

function resetResourcePickerFilters() {
  state.resourcePickerSearchQuery = "";
  state.resourcePickerUsageFilter = "";
  state.resourcePickerProviderFilter = "";
}

function collectBodyBindingKeys(pkg) {
  const keys = [];
  const pushKey = value => {
    const key = String(value || "").trim();
    if (!key || keys.includes(key)) return;
    keys.push(key);
  };
  pushKey(pkg?.geometry?.bodyProfile?.modelRootStableId || "");
  for (const resource of getModelResources(pkg)) {
    if (!resource) continue;
    if (resource.usage === "characterModel" || hasTag(resource, "body")) {
      pushKey(resource.resourceKey);
      pushKey(resource.stableId);
    }
  }
  for (const key of pkg?.applicationConfig?.resourceKeys || []) {
    pushKey(key);
  }
  return keys;
}

function resolveBodyModelBindingStatus(pkg) {
  const packageValue = pkg || state.package;
  if (!packageValue) return null;
  const boundBody = getBoundBodyModelResource(packageValue);
  if (boundBody) {
    const key = boundBody.resourceKey || boundBody.stableId || "";
    return {
      key,
      summary: `${getResourceDisplayName(boundBody)} (${key || "unknown"})`
    };
  }
  const fallbackKeys = collectBodyBindingKeys(packageValue);
  for (const key of fallbackKeys) {
    const item = findResourceLibraryItemByKey(key);
    if (!item) continue;
    const resolvedKey = firstNonEmpty(item.resourceKey, item.runtimeResourceKey, item.packageResourceKey, item.stableId, key);
    return {
      key: resolvedKey,
      summary: `${item.displayName || resolvedKey} (${resolvedKey})`
    };
  }
  if (fallbackKeys.length > 0) {
    return {
      key: fallbackKeys[0],
      summary: `${fallbackKeys[0]} (binding key only)`
    };
  }
  return null;
}

function getResourceBindingView() {
  const targetRole = el.resourceBindingRole?.value || "preview";
  const roleInfo = getResourceBindingRole();
  const fieldSpec = getActiveResourceFieldSpec();
  const currentTarget = findTarget(state.selectedPath);
  const currentTargetText = currentTarget?.kind
    ? `${KIND_LABELS[currentTarget.kind] || currentTarget.kind} / ${currentTarget.label || state.selectedPath}`
    : (state.selectedPath || "未选择");
  const bodyBinding = resolveBodyModelBindingStatus(state.package);
  const boundBodyText = bodyBinding ? bodyBinding.summary : "未选择 Body 模型";
  const fieldText = targetRole === "preview"
    ? "当前仅预览选择。请先选中 Body、主手或副手再绑定。"
    : `${fieldSpec.fieldKey} / ${roleInfo.label}`;
  const flowText = "源文件写入 character_sources.json；已有公共资源在 Resource Manager 中管理。CharacterStudio 只保存字段绑定。";
  return {
    currentTargetText,
    boundBodyText,
    fieldText,
    flowText
  };
}

function renderResourceBindingBar() {
  if (!el.resourceBindingTarget) return;
  const binding = getResourceBindingView();

  el.resourceBindingTarget.innerHTML = `
    <span class="binding-compact-item"><span>当前目标</span><strong title="${escapeHtml(binding.currentTargetText)}">${escapeHtml(binding.currentTargetText)}</strong></span>
    <span class="binding-compact-item"><span>绑定字段</span><strong title="${escapeHtml(binding.fieldText)}">${escapeHtml(binding.fieldText)}</strong></span>
    <span class="binding-compact-item"><span>Body 资源</span><strong title="${escapeHtml(binding.boundBodyText)}">${escapeHtml(binding.boundBodyText)}</strong></span>`;

  if (el.resourceBindingDetails) {
    el.resourceBindingDetails.innerHTML = [
      renderBindingStatusLine("当前目标", binding.currentTargetText),
      renderBindingStatusLine("当前 Body 模型", binding.boundBodyText),
      renderBindingStatusLine("绑定字段", binding.fieldText),
      `<span class="binding-flow-note">${escapeHtml(binding.flowText)}</span>`,
      renderCharacterWeaponSlotBindings()
    ].join("");
    wireCharacterWeaponSlotBindings();
  }
}

function renderCharacterWeaponSlotBindings() {
  const sockets = state.package?.geometry?.sockets || [];
  const slots = state.package?.geometry?.weaponSlots || [];
  const renderSlot = (slotId, label) => {
    const slot = slots.find(item => item?.slotId === slotId) || {
      slotId,
      attachSocketId: "",
      slotPose: { parentKind: "Socket", parentPath: slotId, position: { x: 0, y: 0, z: 0 }, rotation: { x: 0, y: 0, z: 0, w: 1 }, scale: { x: 1, y: 1, z: 1 }, eulerHint: { x: 0, y: 0, z: 0 } }
    };
    const options = sockets.map(socket => `<option value="${escapeHtml(socket?.socketId || "")}"${socket?.socketId === slot.attachSocketId ? " selected" : ""}>${escapeHtml(socket?.socketId || "未命名")}</option>`).join("");
    const pose = slot.slotPose || { parentKind: "Socket", parentPath: slot.attachSocketId || slotId, position: { x: 0, y: 0, z: 0 }, rotation: { x: 0, y: 0, z: 0, w: 1 }, scale: { x: 1, y: 1, z: 1 }, eulerHint: { x: 0, y: 0, z: 0 } };
    return `<section class="character-weapon-slot-card"><div><strong>${label}</strong><code>${slotId}</code></div><label><span>角色 Socket</span><select data-character-weapon-slot="${slotId}" data-slot-field="attachSocketId">${options}</select></label><div class="single-weapon-field-grid">
      ${["x","y","z"].map(axis => `<label><span>Position ${axis.toUpperCase()}</span><input type="number" step="0.001" data-character-weapon-slot="${slotId}" data-slot-field="slotPose.position.${axis}" value="${escapeHtml(pose.position?.[axis] ?? 0)}"></label>`).join("")}
      ${["x","y","z"].map(axis => `<label><span>Rotation ${axis.toUpperCase()}</span><input type="number" step="0.1" data-character-weapon-slot="${slotId}" data-slot-field="slotPose.eulerHint.${axis}" value="${escapeHtml(pose.eulerHint?.[axis] ?? 0)}"></label>`).join("")}
    </div></section>`;
  };
  return `<div class="character-weapon-slot-workbench"><div><strong>角色武器槽位</strong><span>这里只编辑角色 Socket 与槽位位置/旋转；武器自身变换和 Trace 在单一武器配置中编辑。</span></div>${renderSlot("mainHand", "主手槽位")}${renderSlot("offHand", "副手槽位")}</div>`;
}

function wireCharacterWeaponSlotBindings() {
  if (!el.resourceBindingDetails || el.resourceBindingDetails.dataset.weaponSlotsWired === "1") return;
  el.resourceBindingDetails.dataset.weaponSlotsWired = "1";
  el.resourceBindingDetails.addEventListener("change", event => {
    const field = event.target.closest("[data-character-weapon-slot]");
    if (!field) return;
    const slot = ensureCharacterWeaponSlot(field.dataset.characterWeaponSlot);
    const parts = String(field.dataset.slotField || "").split(".");
    let cursor = slot;
    for (let i = 0; i < parts.length - 1; i++) cursor = cursor[parts[i]] ||= {};
    cursor[parts[parts.length - 1]] = field.type === "number" ? Number(field.value || 0) : field.value;
    if (field.dataset.slotField === "attachSocketId") {
      slot.slotPose.parentKind = "Socket";
      slot.slotPose.parentPath = field.value;
    }
    state.dirty = true;
    render();
  });
}

function getResourceManagerUrl(options = {}) {
  const params = new URLSearchParams();
  params.set("package", options.packageRelative || state.packageRelative || DEFAULT_PACKAGE);
  params.set("workspace", options.workspace || "browse");
  if (options.select) params.set("select", options.select);
  if (options.returnField) params.set("returnField", options.returnField);
  if (options.returnKind) params.set("returnKind", options.returnKind);
  if (options.usage) params.set("usage", options.usage);
  if (options.kind) params.set("kind", options.kind);
  return `/Tools/MxFramework.ResourceLibrary/web/?${params.toString()}`;
}

function openResourceManagerForResource(resourceOrKey, options = {}) {
  const resourceKey = typeof resourceOrKey === "string"
    ? resourceOrKey
    : getResourceSelectionKey(resourceOrKey)
      || resourceOrKey?.resourceKey
      || resourceOrKey?.runtimeResourceKey
      || resourceOrKey?.providerResourceKey
      || resourceOrKey?.packageResourceKey
      || "";
  const url = getResourceManagerUrl({
    ...options,
    select: options.select || resourceKey,
    usage: options.usage || resourceOrKey?.usage || "",
    kind: options.kind || resourceOrKey?.kind || resourceOrKey?.resourceKind || ""
  });
  if (options.returnField) {
    state.resourceManagerDelegation = {
      fieldKey: options.returnField,
      returnKind: options.returnKind || "",
      picker: state.resourcePickerField ? clone(state.resourcePickerField) : null,
      selectedPath: state.selectedPath || ""
    };
  }
  const opened = window.open(url, "MxFrameworkResourceManager");
  if (opened) {
    opened.focus?.();
    state.message = options.message || "已打开 Resource Manager。资源生命周期修改在那里处理。";
  } else {
    state.message = `请手动打开 Resource Manager：${url}`;
  }
  renderShellStatus();
  return opened;
}

function openResourceManagerForActivePicker() {
  const request = getActiveResourcePickerRequest();
  const spec = request?.fieldSpec || RESOURCE_FIELD_SPECS.preview;
  return openResourceManagerForResource("", {
    workspace: "browse",
    returnField: spec.fieldKey || "",
    returnKind: request?.kind || "resourceField",
    usage: (spec.acceptedUsages || [])[0] || "",
    kind: (spec.acceptedKinds || [])[0] || "",
    message: "已为当前 CharacterStudio 字段打开 Resource Manager。"
  });
}

function openResourceManagerForPublicResource(resourceOrKey, options = {}) {
  return openResourceManagerForResource(resourceOrKey, {
    workspace: options.workspace || "browse",
    message: options.message || "已为当前公共资源打开 Resource Manager。"
  });
}

function getResourceFieldSpecByFieldKey(fieldKey) {
  const key = String(fieldKey || "");
  return Object.values(RESOURCE_FIELD_SPECS).find(spec => spec.fieldKey === key) || null;
}

function normalizeResourceManagerReturnItem(payload, spec) {
  const resourceKey = firstNonEmpty(
    payload.runtimeResourceKey,
    payload.packageResourceKey,
    payload.providerResourceKey,
    payload.resourceKey,
    payload.resourceStableId);
  const providerId = firstNonEmpty(payload.providerId, payload.sourceProviderId);
  const kind = payload.kind || (spec?.acceptedKinds || [])[0] || "";
  const usage = payload.usage || (spec?.acceptedUsages || [])[0] || "";
  const bindingKind = payload.bindingKind || "ResourceManagerAsset";
  return {
    resourceId: payload.resourceId || payload.libraryItemId || resourceKey,
    libraryItemId: payload.libraryItemId || payload.resourceId || resourceKey,
    stableId: payload.resourceStableId || payload.stableId || resourceKey,
    displayName: payload.displayName || resourceKey,
    kind,
    usage,
    sourceProviderId: providerId,
    bindingKind,
    resourceKey,
    runtimeResourceKey: payload.runtimeResourceKey || "",
    providerResourceKey: payload.providerResourceKey || (!payload.runtimeResourceKey && !payload.packageResourceKey ? resourceKey : ""),
    packageResourceKey: payload.packageResourceKey || "",
    unityGuid: payload.unityGuid || "",
    unityAssetPath: payload.unityAssetPath || "",
    providerBindings: [{
      providerId,
      bindingKind,
      runtimeResourceKey: payload.runtimeResourceKey || "",
      providerResourceKey: payload.providerResourceKey || (!payload.runtimeResourceKey && !payload.packageResourceKey ? resourceKey : ""),
      packageResourceKey: payload.packageResourceKey || "",
      unityGuid: payload.unityGuid || "",
      unityAssetPath: payload.unityAssetPath || "",
      isPrimary: true
    }],
    raw: payload
  };
}

function handleResourceManagerReturnMessage(event) {
  if (event.origin !== window.location.origin) return;
  const payload = event.data || {};
  if (payload.type !== "mx-resource-manager-selection") return;
  if (payload.package && payload.package !== state.packageRelative) {
    state.message = `Resource Manager 返回的是 ${payload.package} 的资源，但当前 CharacterStudio 正在编辑 ${state.packageRelative}。`;
    renderShellStatus();
    return;
  }

  const delegation = state.resourceManagerDelegation || {};
  const fieldKey = payload.fieldKey || delegation.fieldKey || "";
  const spec = getResourceFieldSpecByFieldKey(fieldKey) || delegation.picker?.fieldSpec || RESOURCE_FIELD_SPECS.preview;
  const item = normalizeResourceManagerReturnItem(payload, spec);
  const selectionRef = createResourceSelectionRef(item, spec);
  if (delegation.picker) {
    state.resourcePickerField = delegation.picker;
  }
  if (delegation.selectedPath) {
    state.selectedPath = delegation.selectedPath;
  }
  if (state.resourcePickerField) {
    applyResourceSelectionToField(item, selectionRef);
  } else if (item.kind === "Model" && (selectionRef.runtimeResourceKey || selectionRef.packageResourceKey || selectionRef.providerResourceKey)) {
    bindModelResource(selectionRef.runtimeResourceKey || selectionRef.packageResourceKey || selectionRef.providerResourceKey);
  } else {
    state.message = `${item.displayName} 已从 Resource Manager 返回。请先打开 CharacterStudio 字段选择器再绑定。`;
    renderShellStatus();
  }
  state.resourceManagerDelegation = null;
}

function renderResourcePicker() {
  if (!el.resourcePickerOverlay || !el.resourcePickerList) return;
  el.resourcePickerOverlay.hidden = !state.resourcePickerOpen;
  if (!state.resourcePickerOpen) {
    el.resourcePickerList.innerHTML = "";
    if (el.resourcePickerContext) el.resourcePickerContext.innerHTML = "";
    renderResourcePickerDebug(null, [], []);
    return;
  }

  const request = getActiveResourcePickerRequest();
  const fieldSpec = request.fieldSpec;
  el.resourcePickerTitle.textContent = `选择 ${request.title}`;
  el.resourcePickerSummary.textContent = `${fieldSpec.fieldKey} | 输出 ${fieldSpec.outputKind} | 预加载 ${fieldSpec.preloadPolicy}。CharacterStudio 只绑定已有引用。`;
  renderResourcePickerContext(request, fieldSpec);

  if (state.resourcePickerLoading) {
    renderResourcePickerFilters([], request);
    renderResourcePickerDebug(request, [], []);
    el.resourcePickerList.innerHTML = `<div class="empty">正在加载资源候选...</div>`;
    return;
  }

  let rows = [];
  try {
    rows = getResourcePickerRows(fieldSpec);
  } catch (error) {
    renderResourcePickerFilters([], request);
    renderResourcePickerDebug(request, [], [], error);
    el.resourcePickerList.innerHTML = `<div class="empty">资源选择器加载失败：${escapeHtml(error?.message || error || "未知错误")}</div>`;
    return;
  }
  renderResourcePickerFilters(rows, request);
  const filteredRows = applyResourcePickerFilters(rows);
  renderResourcePickerDebug(request, rows, filteredRows);
  if (!rows.length) {
    el.resourcePickerList.innerHTML = `<div class="empty">${escapeHtml(getResourcePickerEmptyText(request, fieldSpec))}</div>`;
    return;
  }

  if (!filteredRows.length) {
    el.resourcePickerList.innerHTML = `<div class="empty">当前筛选没有匹配结果。请清空筛选或添加更多资源。</div>`;
    return;
  }

  const grouped = groupResourcePickerRows(filteredRows, request);
  el.resourcePickerList.innerHTML = grouped
    .filter(group => group.rows.length > 0)
    .map(group => {
      const cards = group.rows.map(row => renderResourcePickerCard(row, fieldSpec)).join("");
      return `<section class="resource-picker-group">
        <header>
          <strong>${escapeHtml(group.label)}</strong>
          <span>${escapeHtml(group.description)}</span>
          <span class="resource-group-count">${escapeHtml(String(group.rows.length))}</span>
        </header>
        <div class="resource-picker-group-grid">${cards}</div>
      </section>`;
    }).join("");
}

function renderResourcePickerDebug(request, rows = [], filteredRows = [], error = null) {
  if (!el.resourcePickerDebug) return;
  if (!request || request.kind !== "importMaterialResourceRef") {
    el.resourcePickerDebug.hidden = true;
    el.resourcePickerDebug.innerHTML = "";
    return;
  }
  const summary = state.commonResources?.summary || {};
  const commonItems = Array.isArray(state.commonResources?.items) ? state.commonResources.items : [];
  const publicRows = rows.filter(row => row.item?.sourceProviderId === "resourceMapping");
  const tone = error || state.commonResources?.unavailable ? "error" : publicRows.length ? "ok" : "warn";
  const message = error
    ? `选择器错误：${error.message || error}`
    : state.commonResources?.unavailable
      ? (state.commonResources.message || "公共复用资源 API 不可用。")
      : `公共 API：总数=${summary.total || 0}，就绪=${summary.ready || 0}，Unity 待处理=${summary.unityPending || 0}；清单候选=${commonItems.length}；行=${rows.length}；筛选后=${filteredRows.length}。`;
  el.resourcePickerDebug.hidden = false;
  el.resourcePickerDebug.className = `resource-picker-debug ${tone}`;
  el.resourcePickerDebug.innerHTML = `<strong>公共资源诊断</strong><span>${escapeHtml(message)}</span>`;
}

function renderResourcePickerContext(request, fieldSpec) {
  if (!el.resourcePickerContext) return;
  const chips = [];
  if (request.slotIntent) {
    chips.push(`<span class="picker-chip">${escapeHtml(request.slotIntent.slotId || "slot")}</span>`);
    if (request.slotIntent.resourceHint) chips.push(`<span class="picker-chip">${escapeHtml(request.slotIntent.resourceHint)}</span>`);
    chips.push(`<span class="picker-chip ${request.slotIntent.required ? "required" : ""}">${request.slotIntent.required ? "必填" : "可选"}</span>`);
  } else {
    chips.push(`<span class="picker-chip">字段绑定</span>`);
  }
  if (Array.isArray(fieldSpec.acceptedKinds) && fieldSpec.acceptedKinds.length) {
    chips.push(`<span class="picker-chip">类型：${escapeHtml(fieldSpec.acceptedKinds.join(", "))}</span>`);
  }
  if (Array.isArray(fieldSpec.acceptedUsages) && fieldSpec.acceptedUsages.length) {
    chips.push(`<span class="picker-chip">用途：${escapeHtml(fieldSpec.acceptedUsages.join(", "))}</span>`);
  }
  const purpose = request.slotIntent?.purpose
    ? `<span class="picker-help">${escapeHtml(request.slotIntent.purpose)}</span>`
    : "";
  el.resourcePickerContext.innerHTML = `
    <div class="resource-picker-chip-row">${chips.join("")}</div>
    ${purpose}
    ${renderResourcePickerContextHint(request)}
    <div class="resource-picker-manager-row">
      <button type="button" data-resource-manager-action="open-picker">打开 Resource Manager</button>
      <span class="picker-help">创建、同步、替换、Profile 和 import hint 修改都留在 Resource Manager。</span>
    </div>`;
}

function renderResourcePickerContextHint(request) {
  if (request?.kind === "importMaterialResourceRef")
    return `<span class="picker-help">推荐路径：从 ArtSource/Common 或已生成 Unity 登记中选择已同步公共资源。</span>`;
  if (request?.kind === "reactionSlot")
    return `<span class="picker-help">Reaction 槽位需要具体 AnimationClip。CharacterStudio 保存选择，Unity 导入时生成 Reaction Action。</span>`;
  return `<span class="picker-help">推荐路径：先在 AnimationEditor 绑定动画引用，再在这里选择。</span>`;
}

function renderResourcePickerFilters(rows, request) {
  if (!el.resourcePickerSearch || !el.resourcePickerUsageFilter || !el.resourcePickerProviderFilter || !el.resourcePickerClearFilters) return;
  const usageValues = Array.from(new Set(rows.map(row => row.item.usage || "").filter(Boolean))).sort((a, b) => a.localeCompare(b));
  const providerValues = Array.from(new Set(rows.map(row => row.item.sourceProviderId || "").filter(Boolean))).sort((a, b) => a.localeCompare(b));

  if (el.resourcePickerSearch.value !== state.resourcePickerSearchQuery) {
    el.resourcePickerSearch.value = state.resourcePickerSearchQuery;
  }
  el.resourcePickerSearch.placeholder = request.kind === "animationSlot"
    ? "按剪辑/分组名称、resource key、标签搜索"
    : request.kind === "reactionSlot"
      ? "按受击剪辑名称、resource key、标签搜索"
    : "按名称、resource key 或源路径搜索";

  populateResourcePickerSelect(el.resourcePickerUsageFilter, usageValues, state.resourcePickerUsageFilter, value => value);
  if (!usageValues.includes(state.resourcePickerUsageFilter)) {
    state.resourcePickerUsageFilter = "";
    el.resourcePickerUsageFilter.value = "";
  }

  populateResourcePickerSelect(el.resourcePickerProviderFilter, providerValues, state.resourcePickerProviderFilter, value => formatResourcePickerProviderLabel(value));
  if (!providerValues.includes(state.resourcePickerProviderFilter)) {
    state.resourcePickerProviderFilter = "";
    el.resourcePickerProviderFilter.value = "";
  }

  const hasActiveFilters = Boolean(state.resourcePickerSearchQuery || state.resourcePickerUsageFilter || state.resourcePickerProviderFilter);
  el.resourcePickerClearFilters.disabled = !hasActiveFilters;
}

function populateResourcePickerSelect(select, values, selectedValue, labelResolver) {
  const options = [`<option value="">全部</option>`];
  for (const value of values) {
    const selected = value === selectedValue ? " selected" : "";
    options.push(`<option value="${escapeHtml(value)}"${selected}>${escapeHtml(labelResolver(value))}</option>`);
  }
  select.innerHTML = options.join("");
}

function formatResourcePickerProviderLabel(providerId) {
  return PROVIDER_LABELS[providerId] || providerId;
}

function formatResourceAvailabilityLabel(status) {
  const value = String(status || "Unknown");
  const labels = {
    RuntimeReady: "Runtime 就绪",
    NotRuntimeLoadable: "Runtime 不可加载",
    RuntimeMissing: "Runtime 缺失",
    AudioCueOnly: "仅 AudioCue",
    PreviewOnly: "仅预览",
    AuthoringPreviewOnly: "制作预览，无需 Unity 导入",
    EditorOnly: "仅编辑器",
    Unknown: "未知"
  };
  return labels[value] || value;
}

/**
 * 专用预览 GLB 的三态明细行（纯函数，无跨模块依赖，便于回归）。
 * 对应"制作源可读取 / 不期望生成 Unity 副本 / 不具有运行加载资格"。
 */
function authoringOnlyPreviewDetailRows() {
  return [
    `<div><span>制作源</span><strong>可读取</strong></div>`,
    `<div><span>Unity 副本</span><strong>不生成</strong></div>`,
    `<div><span>运行加载</span><strong>不具备资格</strong></div>`
  ];
}

function applyResourcePickerFilters(rows) {
  const searchQuery = String(state.resourcePickerSearchQuery || "").trim().toLowerCase();
  const usageFilter = String(state.resourcePickerUsageFilter || "");
  const providerFilter = String(state.resourcePickerProviderFilter || "");
  return rows.filter(row => {
    const item = row.item;
    if (usageFilter && item.usage !== usageFilter) return false;
    if (providerFilter && item.sourceProviderId !== providerFilter) return false;
    if (searchQuery && !getResourcePickerSearchText(item).includes(searchQuery)) return false;
    return true;
  });
}

function getResourcePickerSearchText(item) {
  return [
    item.displayName,
    item.resourceKey,
    item.runtimeResourceKey,
    item.packageResourceKey,
    item.providerResourceKey,
    item.stableId,
    item.usage,
    item.kind,
    item.kindLabel,
    item.sourceProviderSummary,
    item.sourceProviderId,
    item.sourceName,
    item.unityAssetPath,
    ...(item.diagnostics || [])
  ].filter(Boolean).join(" ").toLowerCase();
}

function groupResourcePickerRows(rows, request) {
  const groups = {
    recommended: {
      key: "recommended",
      label: "推荐",
      description: "最匹配当前槽位和推荐绑定流程。",
      rows: []
    },
    matching: {
      key: "matching",
      label: "用途匹配",
      description: "符合当前字段契约。",
      rows: []
    },
    other: {
      key: "other",
      label: "其他可用",
      description: "兼容但置信度较低，或源元数据较弱。",
      rows: []
    }
  };
  for (const row of rows) {
    const key = getResourcePickerGroupKey(row, request);
    groups[key]?.rows.push(row);
  }
  return [groups.recommended, groups.matching, groups.other];
}

function getResourcePickerGroupKey(row, request) {
  const tone = row.selection?.tone || "warn";
  const item = row.item;
  const usageMatched = !request.fieldSpec?.acceptedUsages?.length || containsIgnoreCase(request.fieldSpec.acceptedUsages, item.usage);
  const runtimeReady = item.runtimeAvailability === "RuntimeReady";
  const preferredBinding = item.bindingKind === "ResourceManagerAsset" || Boolean(item.runtimeResourceKey);
  const intentMatched = matchResourcePickerIntent(item, request.slotIntent);
  if (tone === "match" && usageMatched && runtimeReady && preferredBinding && intentMatched) return "recommended";
  if (tone === "match" && usageMatched) return "matching";
  return "other";
}

function matchResourcePickerIntent(item, slotIntent) {
  if (!slotIntent) return true;
  const hint = String(slotIntent.resourceHint || "").toLowerCase();
  if (!hint) return true;
  const text = [
    item.usage,
    item.displayName,
    item.resourceKey,
    item.runtimeResourceKey,
    item.sourceName,
    item.unityAssetPath
  ].filter(Boolean).join(" ").toLowerCase();
  if (text.includes(hint)) return true;
  if (hint === "locomotion") return /(locomotion|move|idle|walk|run|strafe|turn)/i.test(text);
  if (hint === "combat") return /(combat|attack|hit|guard|slash|skill|reaction)/i.test(text);
  return false;
}

function renderResourcePickerCard(row, fieldSpec) {
  const item = row.item;
  const selection = row.selection;
  const selected = item.path === state.selectedPath || getResourceIdentityValues(item).includes(getCurrentResourceFieldValue());
  const thumb = item.thumbnailUrl
    ? `<img src="${escapeHtml(item.thumbnailUrl)}" alt="${escapeHtml(item.displayName)}">`
    : `<span>${escapeHtml(getResourceInitial(item))}</span>`;
  const providerText = formatResourcePickerProviderLabel(item.sourceProviderSummary || item.sourceProviderId || "");
  const advancedDetails = [
    `<div><span>绑定</span><strong>${escapeHtml(item.bindingKind)}</strong></div>`,
    `<div><span>Runtime</span><strong>${escapeHtml(item.runtimeAvailability)}</strong></div>`,
    `<div><span>Provider</span><strong>${escapeHtml(providerText || "-")}</strong></div>`,
    `<div><span>引用数</span><strong>${escapeHtml(String(item.referenceCount || 0))}</strong></div>`,
    `<div><span>输出</span><strong>${escapeHtml(fieldSpec.outputKind || "-")}</strong></div>`
  ];
  if (item.sourceName || item.unityAssetPath) {
    advancedDetails.push(`<div><span>来源</span><code title="${escapeHtml(item.unityAssetPath || item.sourceName)}">${escapeHtml(item.unityAssetPath || item.sourceName)}</code></div>`);
  }
  // 专用预览 GLB：明确区分"制作源可读 / 不生成 Unity 副本 / 无运行加载资格"三种状态，
  // 而不是只给一个可用性标签，避免被读成"缺资源"或"运行就绪"。
  if (item.runtimeAvailability === "AuthoringPreviewOnly") {
    advancedDetails.push(...authoringOnlyPreviewDetailRows());
  }
  const diagnosticsList = Array.isArray(item.diagnostics) && item.diagnostics.length
    ? `<ul>${item.diagnostics.slice(0, 6).map(diag => `<li>${escapeHtml(diag)}</li>`).join("")}</ul>`
    : `<span class="resource-empty-note">没有诊断信息。</span>`;
  return `
    <article class="resource-card ${selected ? "active" : ""} ${escapeHtml(selection.tone)}">
      <button type="button" class="resource-card-select" data-library-id="${escapeHtml(item.resourceId || item.libraryItemId || "")}" title="${escapeHtml(item.sourceName || item.stableId || item.displayName)}">
        <span class="resource-thumb">${thumb}</span>
        <span class="resource-info">
          <span class="resource-title"><strong>${escapeHtml(item.displayName)}</strong>${renderSyncBadge({ label: item.importStatusLabel, tone: item.importTone })}</span>
          <span>${escapeHtml(item.kindLabel)} / ${escapeHtml(item.usage || "未指定用途")} / ${escapeHtml(formatResourceAvailabilityLabel(item.runtimeAvailability))}</span>
          <span>${renderSelectionBadge(selection)} ${escapeHtml(selection.reason)}</span>
        </span>
      </button>
      <details class="resource-card-details">
        <summary>高级详情</summary>
        <div class="resource-card-meta">${advancedDetails.join("")}</div>
        ${diagnosticsList}
      </details>
    </article>`;
}

function getResourceFieldSpecForInspectorField(target, fieldPath) {
  if (target.kind === "animationSlot" && fieldPath === "resourceKey") {
    return {
      ...RESOURCE_FIELD_SPECS.animationProfileSlot,
      fieldKey: `Animation.Profile.${target.value?.slotId || "slot"}`,
      displayName: target.value?.displayName || "Animation group",
      compatibilityFilter: { ...(RESOURCE_FIELD_SPECS.animationProfileSlot.compatibilityFilter || {}) }
    };
  }
  if (target.kind === "reactionSlot" && fieldPath === "resourceKey") {
    return {
      ...RESOURCE_FIELD_SPECS.reactionAnimationSlot,
      fieldKey: `Reaction.Profile.${target.value?.slotId || "slot"}`,
      displayName: target.value?.displayName || "Reaction animation",
      compatibilityFilter: { ...(RESOURCE_FIELD_SPECS.reactionAnimationSlot.compatibilityFilter || {}) }
    };
  }
  if (target.kind === "weapon" && fieldPath === "weaponRef") {
    const slot = target.value?.equipSlot || "mainHand";
    const base = RESOURCE_FIELD_SPECS[slot] || RESOURCE_FIELD_SPECS.mainHand;
    return {
      ...base,
      fieldKey: `WeaponAttachment.${slot}.${fieldPath}`,
      displayName: "Default weapon",
      compatibilityFilter: { ...(base.compatibilityFilter || {}), slotId: slot }
    };
  }
  return null;
}

function getResourcePickerRows(fieldSpec) {
  if (state.resourcePickerField?.commonOnly) {
    return getCommonResourcePickerRows(fieldSpec)
      .sort((a, b) => getResourcePickerSortKey(a, fieldSpec).localeCompare(getResourcePickerSortKey(b, fieldSpec)));
  }
  const queryItems = Array.isArray(state.resourcePickerQuery?.items) ? state.resourcePickerQuery.items : [];
  let rows = [];
  if (queryItems.length) {
    rows = queryItems
      .map(row => ({ item: normalizeResourceLibraryItem(row.item), selection: evaluateServerPickerItem(row, fieldSpec) }))
      .filter(row => row.item);
  } else {
    rows = getResourceLibraryItems().map(item => ({ item, selection: evaluateResourceFieldSelection(item, fieldSpec) }));
  }
  rows = rows.concat(getCommonResourcePickerRows(fieldSpec));
  return mergeResourcePickerRows(rows, fieldSpec)
    .sort((a, b) => getResourcePickerSortKey(a, fieldSpec).localeCompare(getResourcePickerSortKey(b, fieldSpec)));
}

function getResourcePickerEmptyText(request, fieldSpec) {
  if (shouldIncludeCommonResourceRows(fieldSpec)) {
    if (state.commonResources?.unavailable) {
      return state.commonResources.message || "公共复用资源 API 不可用。请重启 Authoring 服务并刷新 CharacterStudio。";
    }
    const commonItems = getCommonResourceItemsForFieldSpec(fieldSpec);
    if (!commonItems.length) {
      const usage = fieldSpec.acceptedUsages?.length ? fieldSpec.acceptedUsages.join("/") : "resource";
      return `ArtSource/Common 下没有找到公共 ${usage} 源清单。请添加 *_sources.json 清单或同步公共资源。`;
    }
    return "已找到公共复用资源，但当前字段没有可选项。请检查缺失源文件或阻塞诊断。";
  }
  return "没有找到适配当前字段的资源。请先在 Resource Manager 中导入或同步资源。";
}

function shouldIncludeCommonResourceRows(fieldSpec) {
  if (!fieldSpec) return false;
  if (!fieldSpec.acceptedProviderIds?.includes("resourceMapping")) return false;
  return true;
}

function getCommonResourceItemsForFieldSpec(fieldSpec) {
  if (!shouldIncludeCommonResourceRows(fieldSpec)) return [];
  const items = Array.isArray(state.commonResources?.items) ? state.commonResources.items : [];
  return items.filter(item => {
    const usage = item?.usage || (String(item?.kind || "").toLowerCase() === "weapon" ? "weaponModel" : "characterModel");
    const kind = normalizeResourceKind(item?.resourceKind || inferCommonResourceKindFromUsage(usage));
    if (fieldSpec.acceptedKinds?.length && !containsIgnoreCase(fieldSpec.acceptedKinds, kind)) return false;
    if (fieldSpec.acceptedUsages?.length && !containsIgnoreCase(fieldSpec.acceptedUsages, usage)) return false;
    return true;
  });
}

function getCommonResourcePickerRows(fieldSpec) {
  return getCommonResourceItemsForFieldSpec(fieldSpec)
    .map(item => {
      const normalized = normalizeCommonResourcePickerItem(item);
      if (!normalized) return null;
      return {
        item: normalized,
        selection: evaluateCommonResourcePickerSelection(normalized, fieldSpec)
      };
    })
    .filter(Boolean);
}

function normalizeCommonResourcePickerItem(item) {
  if (!item) return null;
  const resourceKey = item.resourceKey || "";
  const usage = item.usage || (String(item.kind || "").toLowerCase() === "weapon" ? "weaponModel" : "characterModel");
  if (!resourceKey) return null;
  const kind = normalizeResourceKind(item.resourceKind || inferCommonResourceKindFromUsage(usage));
  const ready = item.status === "Ready" && item.profileRuntimeLoadable && item.unityGuid;
  const importTone = ready ? "ok" : (item.status === "Blocked" || item.status === "MissingSource" ? "error" : "warn");
  const runtimeAvailability = ready ? "RuntimeReady" : "NotRuntimeLoadable";
  const statusLabel = ready ? "就绪" : formatCommonResourceStatusLabel(item.status || "NeedsSync");
  const generatedAssetPath = item.generatedAssetPath || "";
  const unityRuntimeAssetPath = item.unityRuntimeAssetPath || generatedAssetPath;
  const rawGeneratedAssetPath = item.rawGeneratedAssetPath || generatedAssetPath;
  const resourceId = `common:${item.id || resourceKey}`;
  const publicId = item.resourceId || item.id || resourceKey;
  const itemLabel = item.displayName || "";
  const displayName = publicId && itemLabel && itemLabel !== publicId
    ? `${publicId} (${itemLabel})`
    : (publicId || itemLabel || resourceKey);
  return {
    raw: item,
    resource: item,
    resourceId,
    libraryItemId: resourceId,
    stableId: buildCommonResourceStableId(resourceKey, kind),
    sourceProviderId: "resourceMapping",
    sourceProviderSummary: "公共复用资源",
    providerBindings: [{
      bindingKind: "ResourceManagerAsset",
      runtimeResourceKey: resourceKey,
      providerResourceKey: resourceKey,
      packageResourceKey: "",
      unityGuid: item.unityGuid || "",
      unityAssetPath: unityRuntimeAssetPath,
      hash: item.sourceHash || "",
      isPrimary: true,
      providerData: {
        commonResourceId: item.resourceId || "",
        commonResourceKind: item.kind || "",
        sourceManifestPath: item.manifestPath || "",
        unityRuntimeAssetPath,
        rawGeneratedAssetPath,
        usesUnityWrapperPrefab: item.usesUnityWrapperPrefab ? "true" : "false",
        weaponMaterialBindingStatus: item.weaponMaterialBindingStatus || "",
        weaponMaterialTemplateId: item.weaponMaterialTemplateId || "",
        weaponMaterialPath: item.weaponMaterialPath || "",
        weaponMaterialTextureResourceKey: item.weaponMaterialTextureResourceKey || "",
        weaponMaterialSource: item.weaponMaterialSource || "",
        weaponMaterialRendererCount: String(item.weaponMaterialRendererCount || 0)
      }
    }],
    path: "",
    resourceKey,
    runtimeResourceKey: resourceKey,
    providerResourceKey: resourceKey,
    packageResourceKey: "",
    unityGuid: item.unityGuid || "",
    displayName,
    kind,
    kindLabel: RESOURCE_KIND_LABELS[kind] || kind,
    usage,
    sourceKind: "GeneratedAsset",
    bindingKind: "ResourceManagerAsset",
    runtimeBindingKind: "ResourceManagerAsset",
    runtimeAvailability,
    importStatus: statusLabel,
    importStatusLabel: statusLabel,
    importTone,
    referenceCount: 0,
    diagnostics: [
      item.message || "",
      item.manifestPath ? `manifest=${item.manifestPath}` : "",
      item.sourcePath ? `source=${item.sourcePath}` : "",
      rawGeneratedAssetPath ? `raw=${rawGeneratedAssetPath}` : "",
      unityRuntimeAssetPath ? `runtime=${unityRuntimeAssetPath}` : "",
      item.weaponMaterialBindingStatus ? `weaponMaterial=${item.weaponMaterialBindingStatus}` : "",
      item.weaponMaterialTemplateId ? `materialTemplate=${item.weaponMaterialTemplateId}` : "",
      item.weaponMaterialSource ? `materialSource=${item.weaponMaterialSource}` : "",
      item.weaponMaterialTextureResourceKey ? `materialTexture=${item.weaponMaterialTextureResourceKey}` : "",
      item.weaponMaterialRendererCount ? `materialRenderers=${item.weaponMaterialRendererCount}` : "",
      item.weaponMaterialPath ? `material=${item.weaponMaterialPath}` : "",
      item.usesUnityWrapperPrefab ? "Unity 使用 wrapper Prefab。" : ""
    ].filter(Boolean),
    thumbnailUrl: "",
    sourceName: resourceKey || item.sourcePath || item.manifestPath || "",
    unityAssetPath: unityRuntimeAssetPath
  };
}

function buildCommonResourceStableId(resourceKey, kind = "Generated") {
  const formatted = `common:${String(kind || "resource").toLowerCase()}:${resourceKey}:`;
  return `global.profile.${normalizeResourcePickerStableSegment(formatted)}`;
}

function inferCommonResourceKindFromUsage(usage) {
  const normalized = String(usage || "").toLowerCase();
  if (normalized === "charactermodel" || normalized === "weaponmodel" || normalized === "previewmesh") return "Model";
  if (normalized === "texture" || normalized === "previewthumbnail") return "Texture";
  if (normalized === "material") return "Material";
  if (normalized === "audiocue" || normalized === "audio") return "Audio";
  if (normalized === "vfxcue" || normalized === "vfx") return "Vfx";
  if (normalized === "config" || normalized === "characterconfig") return "Config";
  return "Generated";
}

function formatCommonResourceStatusLabel(status) {
  const value = String(status || "NeedsSync");
  const labels = {
    Ready: "就绪",
    Blocked: "阻塞",
    MissingSource: "源缺失",
    NeedsSync: "需同步",
    Changed: "有改动",
    UnityPending: "Unity 待处理"
  };
  return labels[value] || value;
}

function normalizeResourcePickerStableSegment(value) {
  const text = String(value || "resource").trim().toLowerCase();
  const normalized = text
    .replace(/[.:/\\\s]+/g, ".")
    .replace(/[^a-z0-9._-]+/g, "")
    .replace(/\.+/g, ".")
    .replace(/^\.+|\.+$/g, "");
  return normalized || "resource";
}

function evaluateCommonResourcePickerSelection(item, fieldSpec) {
  if (item.raw?.status === "Blocked" || item.raw?.status === "MissingSource") {
    return {
      tone: "blocked",
      label: "阻塞",
      reason: item.raw?.message || "公共源文件缺失或无效。"
    };
  }
  const relaxedSpec = {
    ...fieldSpec,
    allowIncompatibleWithWarning: true
  };
  const selection = evaluateResourceFieldSelection(item, relaxedSpec);
  if (selection.tone === "match") return selection;
  return {
    ...selection,
    tone: selection.tone === "blocked" ? "blocked" : "warn",
    label: selection.tone === "blocked" ? "阻塞" : "可选择",
    reason: selection.reason || "请先在 Resource Manager 中同步公共资源，再运行 Unity 导入供 Runtime 使用。"
  };
}

function mergeResourcePickerRows(rows, fieldSpec) {
  const selectableRows = rows.filter(row => row.selection?.tone !== "blocked");
  const groups = new Map();
  for (const row of selectableRows) {
    const key = getResourcePickerDedupeKey(row.item);
    const current = groups.get(key);
    if (!current) {
      groups.set(key, { ...row, item: { ...row.item } });
      continue;
    }
    groups.set(key, mergeResourcePickerRow(current, row, fieldSpec));
  }
  return Array.from(groups.values());
}

function mergeResourcePickerRow(left, right, fieldSpec) {
  const preferred = compareResourcePickerRowPriority(left, right, fieldSpec) >= 0 ? left : right;
  const secondary = preferred === left ? right : left;
  return {
    item: mergeResourcePickerItems(preferred.item, secondary.item),
    selection: preferred.selection?.tone === "match" || secondary.selection?.tone !== "match"
      ? preferred.selection
      : secondary.selection
  };
}

function mergeResourcePickerItems(primary, secondary) {
  const providerSummary = mergeStringValues([
    primary.sourceProviderSummary,
    primary.sourceProviderId,
    secondary.sourceProviderSummary,
    secondary.sourceProviderId
  ]);
  return {
    ...primary,
    providerBindings: mergeProviderBindings(primary.providerBindings, secondary.providerBindings),
    diagnostics: mergeStringValues([...(primary.diagnostics || []), ...(secondary.diagnostics || [])]),
    referenceCount: Math.max(primary.referenceCount || 0, secondary.referenceCount || 0),
    sourceProviderSummary: providerSummary.join(" + "),
    mergedProviderCount: providerSummary.length
  };
}

function mergeProviderBindings(left = [], right = []) {
  const merged = [];
  for (const binding of [...left, ...right]) {
    if (!binding) continue;
    const key = [
      binding.bindingKind,
      binding.runtimeResourceKey,
      binding.packageResourceKey,
      binding.providerResourceKey,
      binding.unityGuid,
      binding.unityAssetPath
    ].filter(Boolean).join("|");
    if (!merged.some(item => [
      item.bindingKind,
      item.runtimeResourceKey,
      item.packageResourceKey,
      item.providerResourceKey,
      item.unityGuid,
      item.unityAssetPath
    ].filter(Boolean).join("|") === key)) {
      merged.push(binding);
    }
  }
  return merged;
}

function mergeStringValues(values) {
  const result = [];
  for (const value of values) {
    const parts = String(value || "")
      .split("+")
      .map(part => part.trim())
      .filter(Boolean);
    for (const text of parts) {
      if (!result.includes(text)) result.push(text);
    }
  }
  return result;
}

function getResourcePickerDedupeKey(item) {
  return firstNonEmpty(
    item.packageResourceKey,
    item.resourceKey,
    item.stableId,
    item.runtimeResourceKey,
    item.providerResourceKey,
    `${item.kind}:${item.usage}:${item.displayName}`
  ).toLowerCase();
}

function compareResourcePickerRowPriority(left, right, fieldSpec) {
  return getResourcePickerPriority(left, fieldSpec) - getResourcePickerPriority(right, fieldSpec);
}

function getResourcePickerPriority(row, fieldSpec) {
  const item = row.item;
  const binding = getBindingForFieldSpec(item, fieldSpec) || {};
  let score = 0;
  if (row.selection?.tone === "match") score += 1000;
  if (row.selection?.tone === "warn") score += 500;
  if (binding.runtimeResourceKey || item.runtimeResourceKey) score += 240;
  if (item.runtimeAvailability === "RuntimeReady") score += 180;
  if (item.bindingKind === "ResourceManagerAsset") score += 140;
  if (binding.packageResourceKey || item.packageResourceKey) score += 100;
  if (item.sourceProviderId === "runtimeCatalog") score += 80;
  if (item.sourceProviderId === "characterPackage") score += 50;
  if (item.sourceProviderId === "unityAssetDatabase" || item.sourceProviderId === "unityProjectAssets") score += 20;
  if (item.importTone === "ok") score += 10;
  return score;
}

function getResourcePickerSortKey(row, fieldSpec) {
  const priority = String(9999 - getResourcePickerPriority(row, fieldSpec)).padStart(4, "0");
  return `${priority}:${getResourceLibrarySortKey(row.item)}`;
}

function evaluateServerPickerItem(row, spec) {
  const reasons = Array.isArray(row.reasons) ? row.reasons : [];
  const reason = reasons.map(item => item.code || item.message).filter(Boolean).join(" / ");
  if (row.selectable && row.hasWarnings) return { tone: "warn", label: "可选择", reason: reason || `${spec.outputKind} / ${spec.preloadPolicy}` };
  if (row.selectable) return { tone: "match", label: "可选择", reason: reason || `${spec.outputKind} / ${spec.preloadPolicy}` };
  return { tone: "blocked", label: "阻塞", reason: reason || "不符合当前字段。"};
}

function getCurrentResourceFieldValue() {
  if (!state.resourcePickerField) return "";
  if (state.resourcePickerField.kind === "importMaterialResourceRef") {
    const item = state.resourcePickerField.importMaterialItemId
      ? findImportMaterialItem(state.resourcePickerField.importMaterialItemId)
      : findImportMaterialSlotItem(getImportMaterialsDocument(), getImportMaterialSlotDefinition(state.resourcePickerField.slotKey));
    return getResourceSelectionKey(item?.source?.selection);
  }
  if (state.resourcePickerField.kind === "animationSlot") {
    const { slot } = findAnimationSlot(state.resourcePickerField.profileId, state.resourcePickerField.slotId);
    return firstNonEmpty(
      slot?.resourceKey,
      slot?.resourceSelection?.runtimeResourceKey,
      slot?.resourceSelection?.packageResourceKey,
      slot?.resourceSelection?.providerResourceKey,
      slot?.resourceSelection?.resourceStableId,
      slot?.resourceSelection?.unityAssetPath);
  }
  if (state.resourcePickerField.kind === "reactionSlot") {
    const { slot } = findReactionSlot(state.resourcePickerField.profileId, state.resourcePickerField.slotId);
    return firstNonEmpty(
      slot?.resourceKey,
      slot?.resourceSelection?.runtimeResourceKey,
      slot?.resourceSelection?.packageResourceKey,
      slot?.resourceSelection?.providerResourceKey,
      slot?.resourceSelection?.resourceStableId,
      slot?.resourceSelection?.unityAssetPath);
  }
  if (state.resourcePickerField.kind === "singleWeapon") {
    const single = getSingleWeaponConfiguration(state.resourcePickerField.singleWeaponConfigurationId);
    return getResourceSelectionKey(single?.weaponRef) || "";
  }
  const target = findTarget(state.resourcePickerField.targetPath);
  if (state.resourcePickerField.fieldPath === "weaponRef") {
    return getResourceSelectionKey(target.value?.weaponRef) || "";
  }
  return getNested(target.value, state.resourcePickerField.fieldPath) || "";
}

function getResourceLibraryItems() {
  const apiItems = state.resourceLibrary?.items;
  const rawItems = Array.isArray(apiItems) && apiItems.length > 0
    ? apiItems
    : (state.package?.resourceCatalog?.entries || []);
  return rawItems
    .map(item => normalizeResourceLibraryItem(item))
    .filter(Boolean)
    .sort((a, b) => getResourceLibrarySortKey(a).localeCompare(getResourceLibrarySortKey(b)));
}

function normalizeResourceLibraryItem(item) {
  if (!item) return null;
  const resource = item.resource || item;
  const providerBindings = Array.isArray(item.providerBindings) ? item.providerBindings : [];
  const primaryBinding = getPrimaryResourceBinding(providerBindings);
  const sync = getResourceUnitySync(resource);
  const kind = normalizeResourceKind(item.kind || resource.kind || mapResourceKind(resource.typeId));
  const diagnostics = [
    ...(Array.isArray(item.diagnostics) ? item.diagnostics.map(formatDiagnosticText) : []),
    ...sync.diagnostics
  ].filter(Boolean);
  const referenceCount = item.referenceCount ?? collectResourceReferences(resource).length;
  const bindingKind = normalizeResourceBindingKind(item.bindingKind || item.runtimeBindingKind || primaryBinding?.bindingKind || inferRuntimeBindingKind(resource, kind));
  const runtimeBindingKind = bindingKind;
  const runtimeAvailability = item.runtimeAvailability || inferRuntimeAvailability(resource, runtimeBindingKind, sync);
  const resourceId = item.resourceId || item.libraryItemId || resource.libraryItemId || resource.stableId || resource.resourceKey || resource.localId || resource.relativePath;
  const sourceProviderId = item.sourceProviderId || inferSourceProviderId(item, resource);
  const runtimeResourceKey = item.runtimeResourceKey || firstBindingValue(providerBindings, "runtimeResourceKey") || (runtimeBindingKind === "ResourceManagerAsset" ? resource.resourceKey || "" : "");
  const providerResourceKey = item.providerResourceKey || firstBindingValue(providerBindings, "providerResourceKey") || firstBindingValue(providerBindings, "packageResourceKey") || resource.resourceKey || "";
  const packageResourceKey = item.packageResourceKey || firstBindingValue(providerBindings, "packageResourceKey") || (sourceProviderId === "characterPackage" ? resource.resourceKey || "" : "");
  const unityGuid = item.unityGuid || firstBindingValue(providerBindings, "unityGuid") || sync.unityGuid || "";
  const unityAssetPath = item.unityAssetPath || firstBindingValue(providerBindings, "unityAssetPath") || sync.unityAssetPath || "";
  const importStatus = item.importStatus || sync.status;
  const importTone = getResourceImportTone(importStatus, sync.tone);
  return {
    raw: item,
    resource,
    resourceId,
    libraryItemId: item.libraryItemId || resourceId,
    stableId: item.stableId || resource.stableId || resourceId,
    sourceProviderId,
    providerBindings,
    path: resource.resourceKey ? `resources/${resource.resourceKey}` : "",
    resourceKey: resource.resourceKey || item.resourceKey || "",
    runtimeResourceKey,
    providerResourceKey,
    packageResourceKey,
    unityGuid,
    displayName: item.displayName || getResourceDisplayName(resource),
    kind,
    kindLabel: RESOURCE_KIND_LABELS[kind] || kind,
    usage: item.usage || resource.usage || "",
    sourceKind: item.sourceKind || inferSourceKind(resource),
    bindingKind,
    runtimeBindingKind,
    runtimeAvailability,
    importStatus,
    importStatusLabel: item.importStatusLabel || importStatus || sync.label,
    importTone,
    referenceCount,
    diagnostics,
    metadata: item.metadata || resource.metadata || {},
    thumbnailUrl: item.thumbnailUrl || getResourceThumbnailUrl(resource, state.package),
    sourceName: item.metadata?.sourcePath || resource.provenance?.sourceFile || resource.relativePath || resource.localId || resource.resourceKey || "",
    unityAssetPath
  };
}

function getPrimaryResourceBinding(bindings) {
  return bindings.find(binding => binding?.isPrimary) || bindings[0] || null;
}

function firstBindingValue(bindings, key) {
  const found = bindings.find(binding => binding && binding[key]);
  return found ? found[key] || "" : "";
}

function normalizeResourceKind(kind) {
  const normalized = String(kind || "").toLowerCase();
  if (normalized === "model") return "Model";
  if (normalized === "animation" || normalized === "animationclipgroup") return "Animation";
  if (normalized === "texture" || normalized === "preview") return "Texture";
  if (normalized === "material") return "Material";
  if (normalized === "avatarmask") return "AvatarMask";
  if (normalized === "vfx") return "Vfx";
  if (normalized === "audio") return "Audio";
  if (normalized === "config") return "Config";
  if (normalized === "generated") return "Generated";
  return kind || "Generated";
}

function normalizeResourceBindingKind(kind) {
  const value = String(kind || "");
  return value || "None";
}

function getResourceImportTone(status, fallbackTone = "") {
  const normalized = String(status || "").toLowerCase();
  if (["clean", "ready", "imported", "runtimeReady".toLowerCase()].includes(normalized)) return "ok";
  return fallbackTone || getUnityStatusTone(status);
}

function inferSourceProviderId(item, resource) {
  if (item?.sourceProviderId) return item.sourceProviderId;
  const sourceKind = item?.sourceKind || inferSourceKind(resource);
  if (sourceKind === "RuntimeCatalogAsset") return "runtimeCatalog";
  if (sourceKind === "UnityAsset") return "unityAssetDatabase";
  if (sourceKind === "FmodLibrary") return "fmod";
  if (sourceKind === "ExternalFile" || sourceKind === "PackageResource") return "characterPackage";
  return "generatedAssets";
}

function getResourceLibrarySortKey(item) {
  const resource = item.resource;
  const rank = isBodyModelBinding(resource, state.package)
    ? "0"
    : isWeaponResourceBinding(resource, state.package, "mainHand")
      ? "1"
      : isWeaponResourceBinding(resource, state.package, "offHand")
        ? "2"
        : item.kind === "Model" ? "3" : "4";
  return `${rank}:${item.kind}:${item.displayName}`;
}

function mapResourceKind(typeId) {
  const normalized = String(typeId || "").toLowerCase();
  if (normalized === "model" || normalized === "mesh" || normalized === "prefab") return "Model";
  if (normalized === "animation" || normalized === "anim" || normalized === "clip") return "Animation";
  if (normalized === "texture" || normalized === "preview" || normalized === "sprite" || normalized === "image") return "Texture";
  if (normalized === "material") return "Material";
  if (normalized === "vfx" || normalized === "effect") return "Vfx";
  if (normalized === "audio" || normalized === "audioclip" || normalized === "fmod") return "Audio";
  if (normalized === "config" || normalized.endsWith("config")) return "Config";
  return "Generated";
}

function inferSourceKind(resource) {
  const sourceTool = String(resource?.provenance?.sourceTool || "").toLowerCase();
  if (sourceTool.includes("fmod")) return "FmodLibrary";
  if (resource?.importHints?.providerId === "unityAsset") return "UnityAsset";
  if (resource?.relativePath) return "ExternalFile";
  return "GeneratedAsset";
}

function inferRuntimeBindingKind(resource, kind) {
  const metadata = resource?.importHints?.metadata || {};
  if (kind === "Audio" && (metadata.audioCueId || metadata.fmodEventPath)) return "AudioCue";
  if (kind === "Audio") return "AudioEventDefinition";
  if (kind === "Texture" && resource?.usage === "previewThumbnail") return "GeneratedPreviewOnly";
  // 专用预览 GLB 只服务制作侧：制作源可读取、不期望生成 Unity 副本、不具有运行加载资格。
  // 必须在 ResourceManagerAsset 兜底之前判定，否则会被当成运行资源并误报运行缺失。
  if (kind === "Model" && isAuthoringPreviewGlb(resource)) return "AuthoringPreviewOnly";
  if (resource?.resourceKey && resource?.importHints?.providerId !== "editorOnly") return "ResourceManagerAsset";
  if (resource?.relativePath) return "UnityEditorOnlyAsset";
  return "None";
}

/**
 * 与 Authoring 侧 CharacterPackageResourceExportPolicy 同一判定：usage=previewMesh 且有效源格式为 GLB。
 * 源格式缺失时按扩展名回退，比较大小写不敏感。
 */
function isAuthoringPreviewGlb(resource) {
  if (String(resource?.usage || "") !== "previewMesh") return false;
  const declared = String(resource?.sourceFormat || resource?.importHints?.metadata?.sourceFormat || "")
    .replace(/^\./, "")
    .toLowerCase();
  if (declared) return declared === "glb";
  return /\.glb$/i.test(String(resource?.relativePath || ""));
}

function inferRuntimeAvailability(resource, bindingKind, sync) {
  if (bindingKind === "AudioCue" || bindingKind === "AudioEventDefinition") return "AudioCueOnly";
  if (bindingKind === "AuthoringPreviewOnly") return "AuthoringPreviewOnly";
  if (bindingKind === "GeneratedPreviewOnly") return "PreviewOnly";
  if (bindingKind === "UnityEditorOnlyAsset") return "EditorOnly";
  if (bindingKind !== "ResourceManagerAsset") return "Unknown";
  if (!resource?.resourceKey && !resource?.runtimeResourceKey) return "NotRuntimeLoadable";
  const tone = sync?.tone || "";
  if (tone === "error") return "RuntimeMissing";
  if (tone === "warn") return "RuntimeMissing";
  return "RuntimeReady";
}

function evaluateResourceFieldSelection(item, spec) {
  const reasons = [];
  let selectable = true;
  let warn = false;
  if (spec.acceptedKinds?.length && !containsIgnoreCase(spec.acceptedKinds, item.kind)) {
    selectable = false;
    reasons.push(`kind=${item.kind || "unknown"}`);
  }
  if (spec.acceptedUsages?.length && !containsIgnoreCase(spec.acceptedUsages, item.usage)) {
    selectable = false;
    reasons.push(`usage=${item.usage || "unknown"}`);
  }
  if (spec.acceptedProviderIds?.length && !spec.acceptedProviderIds.includes(item.sourceProviderId)) {
    selectable = false;
    reasons.push(`provider=${item.sourceProviderId || "unknown"}`);
  }
  if (spec.acceptedBindingKinds?.length && !hasAcceptedBinding(item, spec.acceptedBindingKinds)) {
    selectable = false;
    reasons.push(`binding=${item.bindingKind || "unknown"}`);
  }
  if (spec.requireRuntimeLoadable && item.runtimeAvailability !== "RuntimeReady") {
    warn = spec.allowIncompatibleWithWarning;
    selectable = selectable && warn;
    reasons.push(`runtime=${item.runtimeAvailability}`);
  }
  if (spec.requireUnityImported && item.importTone !== "ok") {
    warn = spec.allowIncompatibleWithWarning;
    selectable = selectable && warn;
    reasons.push(`import=${item.importStatusLabel}`);
  }
  if (item.diagnostics.some(text => /failed|conflict|missing/i.test(text))) {
    warn = true;
  }
  if (selectable && !warn) return { tone: "match", label: "可选择", reason: `${spec.outputKind} / ${spec.preloadPolicy}` };
  if (selectable && warn) return { tone: "warn", label: "可选择", reason: reasons.join("; ") || "存在兼容性警告。" };
  return { tone: "blocked", label: "阻塞", reason: reasons.join("; ") || "不符合当前字段。" };
}

function containsIgnoreCase(values, value) {
  return values.some(item => String(item).toLowerCase() === String(value || "").toLowerCase());
}

function hasAcceptedBinding(item, acceptedKinds) {
  if (containsIgnoreCase(acceptedKinds, item.bindingKind)) return true;
  return (item.providerBindings || []).some(binding => containsIgnoreCase(acceptedKinds, binding?.bindingKind));
}

function renderSelectionBadge(selection) {
  const tone = selection.tone === "match" ? "ok" : selection.tone === "warn" ? "warn" : "error";
  return `<span class="selection-badge ${tone}">${escapeHtml(selection.label)}</span>`;
}

async function selectLibraryItem(libraryItemId) {
  const request = getActiveResourcePickerRequest();
  const spec = request.fieldSpec;
  const row = getResourcePickerRows(spec).find(candidate => candidate.item.resourceId === libraryItemId || candidate.item.libraryItemId === libraryItemId);
  const item = row?.item || getResourceLibraryItems().find(candidate => candidate.resourceId === libraryItemId || candidate.libraryItemId === libraryItemId);
  if (!item) return;
  const localSelection = row?.selection || evaluateResourceFieldSelection(item, spec);
  let selectionRef = createResourceSelectionRef(item, spec);
  const resolved = shouldSkipResourcePickerResolve(row, request)
    ? null
    : await resolveResourceSelection(selectionRef, spec);
  if (resolved?.selection) selectionRef = normalizeResolvedSelectionRef(resolved.selection, selectionRef);
  const blocked = resolved ? resolved.accepted === false : localSelection.tone === "blocked";
  const reason = resolved ? formatSelectionReasons(resolved.reasons) : localSelection.reason;
  if (blocked) {
    state.message = `${item.displayName} 不兼容 ${spec.fieldKey}：${reason || "没有返回原因。"}`;
    renderResourcePicker();
    renderShellStatus();
    return;
  }
  if (state.resourcePickerField) {
    applyResourceSelectionToField(item, selectionRef);
    return;
  }
  const resolvedModelKey = selectionRef.runtimeResourceKey || selectionRef.packageResourceKey || selectionRef.providerResourceKey || item.resourceKey || "";
  if (item.kind === "Model" && resolvedModelKey) {
    bindModelResource(resolvedModelKey);
    return;
  }
  state.selectedPath = item.path || state.selectedPath;
  state.message = `${spec.fieldKey} 已引用 ${item.displayName}；resourceSelectionRef=${selectionRef.resourceStableId || "待生成"}`;
  closeResourcePicker();
  renderTree();
  renderViewportModeBar();
  renderResourceBindingBar();
  renderInspector();
  renderShellStatus();
}

function shouldSkipResourcePickerResolve(row, request) {
  return row?.item?.sourceProviderId === "resourceMapping"
    && row?.item?.raw?.manifestPath
    && (request?.kind === "importMaterialResourceRef"
      || (request?.fieldSpec?.allowIncompatibleWithWarning && row?.selection?.tone !== "blocked"));
}

function applyResourceSelectionToField(item, selectionRef) {
  const picker = state.resourcePickerField;
  if (picker?.kind === "importMaterialResourceRef") {
    applyResourceSelectionToImportMaterial(item, selectionRef, picker);
    return;
  }
  if (picker?.kind === "animationSlot") {
    applyResourceSelectionToAnimationSlot(item, selectionRef, picker);
    return;
  }
  if (picker?.kind === "reactionSlot") {
    applyResourceSelectionToReactionSlot(item, selectionRef, picker);
    return;
  }
  if (picker?.kind === "globalReactionSlot") {
    const configuration = (state.weaponLibrary?.reactionConfigurations || []).find(value =>
      value?.reactionConfigurationId === picker.reactionConfigurationId);
    const slot = (configuration?.slots || []).find(value => value?.slotId === picker.slotId);
    if (!slot) return;
    slot.resourceKey = selectionRef.runtimeResourceKey || selectionRef.packageResourceKey || selectionRef.providerResourceKey || item.resourceKey || "";
    slot.resourceSelection = clone(selectionRef);
    state.weaponLibraryDirty = true;
    state.message = `${picker.title} 已引用 ${item.displayName}；资源生命周期仍由 Resource Manager 管理。`;
    closeResourcePicker();
    render();
    return;
  }
  if (picker?.kind === "singleWeapon") {
    const single = getSingleWeaponConfiguration(picker.singleWeaponConfigurationId);
    if (!single) return;
    single.weaponRef = clone(selectionRef);
    state.weaponLibraryDirty = true;
    state.message = `${picker.title} 已引用 ${item.displayName}；资产生命周期仍由 Resource Manager 管理。`;
    closeResourcePicker();
    render();
    return;
  }
  const target = picker ? findTarget(picker.targetPath) : null;
  if (!target?.value) return;
  const value = selectionRef.runtimeResourceKey || selectionRef.packageResourceKey || selectionRef.providerResourceKey || item.resourceKey || "";
  if (picker.fieldPath === "weaponRef") {
    applyWeaponAttachmentSelection(target.value, selectionRef, value);
  } else {
    setNested(target.value, picker.fieldPath, value);
  }
  state.selectedPath = picker.targetPath;
  state.dirty = true;
  state.message = `${picker.title} 已引用 ${item.displayName}；资源资产仍由 Resource Manager 管理。`;
  closeResourcePicker();
  render();
}

function applyResourceSelectionToImportMaterial(item, selectionRef, picker) {
  const definition = getImportMaterialSlotDefinition(picker?.slotKey || "body");
  const manifestItem = picker?.importMaterialItemId
    ? findImportMaterialItem(picker.importMaterialItemId)
    : ensureImportMaterialSlotItem(definition?.key || "body");
  if (!manifestItem || !definition) return;
  manifestItem.id = manifestItem.id || definition.id;
  manifestItem.label = definition.label;
  manifestItem.sourcePath = "";
  manifestItem.source = {
    kind: "resourceRef",
    scope: "public",
    path: "",
    selection: selectionRef
  };
  manifestItem.presetId = definition.presetId;
  manifestItem.slot = definition.slot || "";
  manifestItem.bindTarget = definition.bindTarget || "";
  manifestItem.target = {
    resourceKey: manifestItem.resourceKey || manifestItem.target?.resourceKey || "",
    bindTarget: definition.bindTarget || "",
    slot: definition.slot || ""
  };
  manifestItem.required = definition.required !== false;
  manifestItem.lastImportedHash = "";
  manifestItem.lastImportedUtc = "";
  markImportMaterialsDirty();
  state.message = `${definition.label} 已引用 ${item.displayName}；角色源清单不会复制源文件。`;
  closeResourcePicker();
  renderImportMaterialsPanel();
  renderShellStatus();
}

function applyResourceSelectionToAnimationSlot(item, selectionRef, picker) {
  const { profile, slot } = findAnimationSlot(picker.profileId, picker.slotId);
  if (!profile || !slot) return;
  const previousResourceKey = firstNonEmpty(
    slot.resourceKey,
    slot.resourceSelection?.runtimeResourceKey,
    slot.resourceSelection?.packageResourceKey,
    slot.resourceSelection?.providerResourceKey,
    slot.resourceSelection?.resourceStableId);
  const value = selectionRef.runtimeResourceKey || selectionRef.packageResourceKey || selectionRef.providerResourceKey || item.resourceKey || "";
  slot.resourceKey = value;
  slot.resourceSelection = selectionRef;
  const group = findAnimationGroupByResource(state.package, slot.resourceKey);
  if (group) slot.animationGroupId = group.groupId;
  slot.preloadPolicy = picker.fieldSpec?.preloadPolicy || "AnimationWarmup";
  if ((selectionRef.runtimeResourceKey || selectionRef.packageResourceKey) && slot.resourceKey) {
    ensureApplicationResourceKey(slot.resourceKey);
  }
  if (previousResourceKey && previousResourceKey !== slot.resourceKey) {
    cleanupUnusedAnimationResourceKey(previousResourceKey);
  }
  state.selectedPath = getAnimationSlotPath(profile.profileId, slot.slotId);
  state.dirty = true;
  state.message = `${slot.displayName || slot.slotId} 已引用 ${item.displayName}；${value ? "资源计划或编译器可以解析它。" : "当前只保存了创作选择。"}`;
  closeResourcePicker();
  render();
}

function applyResourceSelectionToReactionSlot(item, selectionRef, picker) {
  const { profile, slot } = findReactionSlot(picker.profileId, picker.slotId);
  if (!profile || !slot) return;
  const value = selectionRef.runtimeResourceKey || selectionRef.packageResourceKey || selectionRef.providerResourceKey || item.resourceKey || "";
  slot.resourceKey = value;
  slot.resourceSelection = selectionRef;
  slot.preloadPolicy = picker.fieldSpec?.preloadPolicy || "AnimationWarmup";
  state.selectedPath = getReactionSlotPath(profile.profileId, slot.slotId);
  state.dirty = true;
  state.message = `${slot.displayName || slot.slotId} 受击动画已引用 ${item.displayName}；Unity 导入会生成 reaction action。`;
  closeResourcePicker();
  render();
}

function createResourceSelectionRef(item, spec) {
  const binding = getBindingForFieldSpec(item, spec);
  return {
    resourceStableId: item.stableId || "",
    sourceProviderId: item.sourceProviderId || "",
    bindingKind: binding?.bindingKind || item.bindingKind || "None",
    expectedKind: item.kind || "",
    expectedUsage: item.usage || "",
    expectedHash: binding?.hash || item.raw?.hash || "",
    runtimeResourceKey: binding?.runtimeResourceKey || item.runtimeResourceKey || "",
    providerResourceKey: binding?.providerResourceKey || item.providerResourceKey || "",
    packageResourceKey: binding?.packageResourceKey || item.packageResourceKey || "",
    unityGuid: binding?.unityGuid || item.unityGuid || "",
    unityAssetPath: binding?.unityAssetPath || item.unityAssetPath || "",
    audioCueId: item.raw?.audioCueId || binding?.providerData?.audioCueId || "",
    audioEventDefinitionId: item.raw?.audioEventDefinitionId || binding?.providerData?.audioEventDefinitionId || ""
  };
}

function getBindingForFieldSpec(item, spec) {
  const bindings = item.providerBindings || [];
  const accepted = spec.acceptedBindingKinds || [];
  const outputKind = spec.outputKind || "ResourceSelectionRef";
  const candidates = bindings.filter(binding => !accepted.length || containsIgnoreCase(accepted, binding?.bindingKind));
  const outputMatch = candidates.find(binding => bindingMatchesOutputKind(binding, outputKind));
  return outputMatch || candidates.find(binding => binding?.isPrimary) || candidates[0] || getPrimaryResourceBinding(bindings);
}

function bindingMatchesOutputKind(binding, outputKind) {
  if (!binding) return false;
  if (outputKind === "RuntimeResourceKey") return Boolean(binding.runtimeResourceKey);
  if (outputKind === "ProviderResourceKey") return Boolean(binding.providerResourceKey);
  if (outputKind === "PackageResourceKey") return Boolean(binding.packageResourceKey);
  if (outputKind === "UnityGuid") return Boolean(binding.unityGuid);
  if (outputKind === "UnityAssetPath") return Boolean(binding.unityAssetPath);
  if (outputKind === "AudioCueId") return Boolean(binding.providerData?.audioCueId);
  if (outputKind === "AudioEventDefinitionId") return Boolean(binding.providerData?.audioEventDefinitionId);
  return true;
}

async function resolveResourceSelection(selection, spec) {
  return await postJson("/api/authoring/resources/resolve-selection", {
    package: state.packageRelative,
    fieldSpec: toAuthoringResourceFieldSpec(spec),
    context: buildResourceConsumerContext(spec),
    selection
  }, null);
}

function normalizeResolvedSelectionRef(selection, fallback) {
  return { ...fallback, ...selection };
}

function formatSelectionReasons(reasons) {
  return (reasons || []).map(item => item.code || item.message).filter(Boolean).join(" / ");
}

function toAuthoringResourceFieldSpec(spec) {
  return {
    fieldKey: spec.fieldKey || "",
    editorKind: spec.editorKind || "CharacterStudio",
    displayName: spec.displayName || "",
    acceptedKinds: spec.acceptedKinds || [],
    acceptedUsages: spec.acceptedUsages || [],
    acceptedProviderIds: spec.acceptedProviderIds || [],
    acceptedSourceKinds: spec.acceptedSourceKinds || [],
    acceptedBindingKinds: spec.acceptedBindingKinds || [],
    requireRuntimeLoadable: Boolean(spec.requireRuntimeLoadable),
    requireUnityImported: Boolean(spec.requireUnityImported),
    allowIncompatibleWithWarning: Boolean(spec.allowIncompatibleWithWarning),
    compatibilityFilter: spec.compatibilityFilter || {},
    preloadPolicy: spec.preloadPolicy || "None",
    outputKind: spec.outputKind || "ResourceSelectionRef"
  };
}

function buildResourceConsumerContext(spec) {
  const picker = state.resourcePickerField || {};
  return {
    consumerKind: "CharacterStudio",
    consumerStableId: state.package?.manifest?.stableId || state.package?.manifest?.packageId || "",
    scopeId: state.resourceLibrary?.scopeId || "",
    packageId: state.package?.manifest?.packageId || "",
    packagePath: state.packageRelative,
    slotId: spec.compatibilityFilter?.slotId || "",
    providerFilterIds: spec.acceptedProviderIds || [],
    metadata: {
      selectedPath: state.selectedPath || "",
      pickerKind: picker.kind || "",
      animationProfileId: picker.profileId || "",
      animationSlotId: picker.kind === "animationSlot" ? picker.slotId || "" : "",
      reactionProfileId: picker.kind === "reactionSlot" ? picker.profileId || "" : "",
      reactionSlotId: picker.kind === "reactionSlot" ? picker.slotId || "" : ""
    }
  };
}

function getResourceIdentityValues(item) {
  return [
    item.resourceId,
    item.libraryItemId,
    item.stableId,
    item.resourceKey,
    item.runtimeResourceKey,
    item.providerResourceKey,
    item.packageResourceKey,
    item.unityGuid,
    item.unityAssetPath
  ].filter(Boolean).map(String);
}

function findResourceLibraryItemByKey(key) {
  if (!key) return null;
  return getResourceLibraryItems().find(item => getResourceIdentityValues(item).includes(String(key))) || null;
}

Object.assign(globalThis, {
  normalizeResourceLibraryPayload,
  normalizeResourcePlanPayload,
  openResourcePicker,
  openResourcePickerForField,
  openSingleWeaponResourcePicker,
  openImportMaterialResourceRefPicker,
  openAnimationSlotPicker,
  openReactionSlotPicker,
  openGlobalReactionSlotPicker,
  clearAnimationSlotSelection,
  clearReactionSlotSelection,
  closeResourcePicker,
  loadResourcePickerQuery,
  getActiveResourceFieldSpec,
  getActiveResourcePickerRequest,
  getAnimationSlotDefinition,
  resetResourcePickerFilters,
  collectBodyBindingKeys,
  resolveBodyModelBindingStatus,
  getResourceBindingView,
  renderResourceBindingBar,
  getResourceManagerUrl,
  openResourceManagerForResource,
  openResourceManagerForActivePicker,
  openResourceManagerForPublicResource,
  getResourceFieldSpecByFieldKey,
  normalizeResourceManagerReturnItem,
  handleResourceManagerReturnMessage,
  renderResourcePicker,
  renderResourcePickerContext,
  renderResourcePickerFilters,
  populateResourcePickerSelect,
  formatResourcePickerProviderLabel,
  applyResourcePickerFilters,
  getResourcePickerSearchText,
  groupResourcePickerRows,
  getResourcePickerGroupKey,
  matchResourcePickerIntent,
  renderResourcePickerCard,
  getResourceFieldSpecForInspectorField,
  getResourcePickerRows,
  getResourcePickerEmptyText,
  shouldIncludeCommonResourceRows,
  getCommonResourceItemsForFieldSpec,
  getCommonResourcePickerRows,
  normalizeCommonResourcePickerItem,
  buildCommonResourceStableId,
  evaluateCommonResourcePickerSelection,
  mergeResourcePickerRows,
  mergeResourcePickerRow,
  mergeResourcePickerItems,
  mergeProviderBindings,
  mergeStringValues,
  getResourcePickerDedupeKey,
  compareResourcePickerRowPriority,
  getResourcePickerPriority,
  getResourcePickerSortKey,
  evaluateServerPickerItem,
  getCurrentResourceFieldValue,
  getResourceLibraryItems,
  normalizeResourceLibraryItem,
  getPrimaryResourceBinding,
  firstBindingValue,
  normalizeResourceKind,
  normalizeResourceBindingKind,
  getResourceImportTone,
  inferSourceProviderId,
  getResourceLibrarySortKey,
  mapResourceKind,
  inferSourceKind,
  inferRuntimeBindingKind,
  inferRuntimeAvailability,
  evaluateResourceFieldSelection,
  containsIgnoreCase,
  hasAcceptedBinding,
  renderSelectionBadge,
  selectLibraryItem,
  shouldSkipResourcePickerResolve,
  applyResourceSelectionToField,
  applyResourceSelectionToImportMaterial,
  applyResourceSelectionToAnimationSlot,
  applyResourceSelectionToReactionSlot,
  createResourceSelectionRef,
  getBindingForFieldSpec,
  bindingMatchesOutputKind,
  resolveResourceSelection,
  normalizeResolvedSelectionRef,
  formatSelectionReasons,
  toAuthoringResourceFieldSpec,
  buildResourceConsumerContext,
  getResourceIdentityValues,
  findResourceLibraryItemByKey
});
