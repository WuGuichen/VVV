// CharacterStudio split module. Registers functions on globalThis to preserve the original call graph.

async function loadAll(options = {}) {
  suspendCharacterStudioSessionPersistence?.();
  state.message = "正在加载资源包...";
  renderShellStatus();
  let uiSnapshot = options.uiSnapshot || null;
  let draftSnapshot = null;
  let restoreResult = { restored: false, conflicts: [] };
  try {
    await loadPackages();
    uiSnapshot ||= readSavedCharacterStudioUiSnapshot?.(state.packageRelative);
    draftSnapshot = options.ignoreSessionDraft
      ? null
      : readSavedCharacterStudioDraftSnapshot?.(state.packageRelative);
    await loadPackageState();
    const loadedRevisions = {
      packageRevision: state.packageRevision || "",
      weaponLibraryRevision: state.weaponLibraryRevision || ""
    };
    if (!draftSnapshot && uiSnapshot) applyCharacterStudioUiSnapshot?.(uiSnapshot);
    restoreResult = draftSnapshot
      ? restoreCharacterStudioDraftSnapshot(draftSnapshot, loadedRevisions)
      : restoreResult;
    if (!restoreResult.restored && uiSnapshot) applyCharacterStudioUiSnapshot?.(uiSnapshot);
    state.externalRefreshConflict = restoreResult.conflicts.length
      ? { parts: restoreResult.conflicts, detectedAt: new Date().toISOString() }
      : null;
    if (restoreResult.restored) {
      state.message = restoreResult.conflicts.length
        ? `已恢复临时草稿；磁盘上的${restoreResult.conflicts.join("、")}已经变化，保存时将要求处理版本冲突。`
        : "已恢复本标签页的临时草稿和编辑界面。";
    }
  } finally {
    resumeCharacterStudioSessionPersistence?.();
  }
  render();
  restoreCharacterStudioDomState?.(draftSnapshot?.ui || uiSnapshot);
  scheduleCharacterStudioSessionPersistence?.();
}

async function refreshExternalState() {
  const uiSnapshot = captureCharacterStudioUiSnapshot?.();
  const draftSnapshot = createCharacterStudioDraftSnapshot?.();
  state.message = "正在刷新 CharacterStudio 状态；当前草稿和界面会保留...";
  renderShellStatus();
  suspendCharacterStudioSessionPersistence?.();
  let restoreResult = { restored: false, conflicts: [] };
  try {
    await loadPackages();
    await loadPackageState();
    const loadedRevisions = {
      packageRevision: state.packageRevision || "",
      weaponLibraryRevision: state.weaponLibraryRevision || ""
    };
    applyCharacterStudioUiSnapshot?.(uiSnapshot);
    restoreResult = draftSnapshot
      ? restoreCharacterStudioDraftSnapshot(draftSnapshot, loadedRevisions)
      : restoreResult;
  } finally {
    resumeCharacterStudioSessionPersistence?.();
  }
  if (!restoreResult.restored) applyCharacterStudioUiSnapshot?.(uiSnapshot);
  state.externalRefreshConflict = restoreResult.conflicts.length
    ? { parts: restoreResult.conflicts, detectedAt: new Date().toISOString() }
    : null;
  if (restoreResult.conflicts.length) {
    state.message = `状态已刷新，草稿和界面已保留；磁盘上的${restoreResult.conflicts.join("、")}已经变化，未覆盖本地草稿。`;
    showToast(state.message, "warn", { title: "检测到磁盘变更", duration: 6000 });
  } else if (restoreResult.restored) {
    state.message = "状态已刷新；未保存草稿、Undo 和当前界面均已保留。";
    showToast(state.message, "info");
  } else {
    state.message = "状态已刷新；当前界面已保留。";
    showToast(state.message, "info");
  }
  render();
  restoreCharacterStudioDomState?.(draftSnapshot?.ui || uiSnapshot);
  persistCharacterStudioSessionState?.();
}

async function discardDraftAndReload() {
  const hasUnsavedChanges = hasUnsavedCharacterStudioChanges?.();
  if (hasUnsavedChanges && !globalThis.confirm?.("确定放弃当前角色草稿、部位伤害草稿、全局武器库草稿和源清单改动，并从磁盘重新加载吗？此操作不能撤销。")) {
    return;
  }
  const uiSnapshot = captureCharacterStudioUiSnapshot?.();
  const packageRelative = state.packageRelative;
  suspendCharacterStudioSessionPersistence?.();
  try {
    // The click that starts this action also schedules draft persistence. Keep
    // persistence suspended for the entire reload so that callback cannot
    // recreate the draft after it has been removed.
    clearSavedCharacterStudioDraft?.(packageRelative);
    state.externalRefreshConflict = null;
    await loadAll({ ignoreSessionDraft: true, uiSnapshot });
    clearSavedCharacterStudioDraft?.(packageRelative);
    state.message = hasUnsavedChanges ? "已放弃未保存改动并从磁盘重新加载；当前界面位置已保留。" : "已从磁盘重新加载；当前界面位置已保留。";
    showToast(state.message, "info");
    renderShellStatus();
    restoreCharacterStudioDomState?.(uiSnapshot);
  } finally {
    resumeCharacterStudioSessionPersistence?.();
  }
  persistCharacterStudioSessionState?.();
  clearSavedCharacterStudioDraft?.(packageRelative);
}

async function loadPackages() {
  const apiPackages = await readJson("/api/character/packages", null);
  if (Array.isArray(apiPackages) && apiPackages.length > 0) {
    state.packages = apiPackages.filter(pkg => pkg.isTemplate !== true);
    state.apiAvailable = true;
  } else {
    state.packages = [{ relative: DEFAULT_PACKAGE, packageId: "000001", kind: "character" }];
  }
  if (!state.userSelectedPackage && state.packages.length > 0) {
    state.packageRelative = state.packages[0].relative;
  } else if (!state.packages.some(pkg => pkg.relative === state.packageRelative)) {
    state.packageRelative = state.packages[0].relative;
  }
  persistCharacterStudioPreferences?.();
  el.packageSelect.innerHTML = state.packages.map(pkg => {
    const identity = pkg.displayName ? `${pkg.packageId || pkg.relative} · ${pkg.displayName}` : (pkg.packageId || pkg.relative);
    const label = `${identity} (${pkg.version || pkg.kind || "character"})`;
    return `<option value="${escapeHtml(pkg.relative)}"${pkg.relative === state.packageRelative ? " selected" : ""}>${escapeHtml(label)}</option>`;
  }).join("");
  globalThis.renderCharacterManagerToolbar?.();
}

async function loadPackageState() {
  state.sourceSyncConflictPaths = [];
  // 切包 / 重载必须让上一次 3D 预览扫描出的 palette usage 整体作废（代次 +1），
  // 否则保存时可能把上一份模型的面数分布写进当前包。
  if (typeof globalThis.invalidateSurfacePalettePreviewUsage === "function") {
    globalThis.invalidateSurfacePalettePreviewUsage();
  } else {
    state.surfacePalettePreviewUsage = null;
  }
  state.previewBones = [];
  state.previewBoneKey = "";
  state.activeBoneFieldPath = "";
  state.highlightedBoneValue = "";
  state.bonePickerOpen = false;
  state.viewportCameraState = null;
  state.viewportCameraStates = { character: null, weapons: {} };
  state.viewportMode = "character";
  state.focusedWeaponAttachmentId = "";
  state.previousCharacterSelection = "";
  state.weaponTransformMode = "translate";
  state.weaponTransformKey = "";
  state.pendingWeaponAutoFitKey = "";
  state.symmetryEditEnabled = false;
  state.skipNextCameraRemember = true;
  const apiState = await readJson(`/api/character/state?package=${encodeURIComponent(state.packageRelative)}`, null);
  if (apiState && apiState.package) {
    state.package = clone(apiState.package);
    state.package.paletteBinding = clone(apiState.paletteBinding || null);
    state.package.bakedSurfaceBinding = clone(apiState.bakedSurfaceBinding || null);
    state.package.bakedSurfaceIssues = clone(apiState.bakedSurfaceIssues || []);
    state.package.paletteUsage = clone(apiState.paletteUsage || null);
    await loadBodyPresenceState(state.package);
    await loadBodyPartDamageState(state.package);
    state.package.hurtboxFitDraft = await readJson(
      `/${state.packageRelative}/package_cache/hurtbox_fit_draft.json`,
      null);
    await loadPackageAnimationInventory();
    state.packageRevision = apiState.revision || "";
    state.weaponLibraryRevision = apiState.weaponLibraryRevision || "";
    state.packageWriteConflict = null;
    await loadGlobalWeaponLibrary();
    normalizeWeaponConfiguration3(state.package);
    ensureAnimationAuthoringConfig(state.package);
    ensureReactionAuthoringConfig(state.package);
    ensureGeometryCollections(state.package);
    state.validation = apiState.validation || apiState.package.validationReport || { issues: [] };
    state.authoringSources = apiState.authoringSources || null;
    state.clothMagica = apiState.clothMagica || null;
    state.importResult = apiState.importReport || null;
    state.unityResourceCatalog = apiState.unityResourceCatalog || null;
    state.unityResourceCatalogPath = apiState.unityResourceCatalogPath || "";
    if (!state.unityResourceCatalog) {
      await loadUnityResourceCatalog();
    }
    await loadWeaponTraceBakeCatalog();
    await loadCommonResources();
    await loadResourceLibraryAndPlan();
    await loadImportMaterials();
    await loadCharacterActionSets();
    await loadCharacterActionCatalog();
    await loadWeaponConfigurations();
    state.abilityChain = null;
    await loadReadiness();
    state.canWrite = Boolean(apiState.canWrite);
    state.apiAvailable = true;
    state.dirty = false;
    resetUndoHistory();
    state.message = "已连接 Authoring 服务。";
    globalThis.renderCharacterManagerToolbar?.();
    return;
  }

  state.package = await readStaticPackage(state.packageRelative);
  await loadBodyPresenceState(state.package);
  await loadBodyPartDamageState(state.package);
  await loadPackageAnimationInventory();
  state.packageRevision = "";
  state.packageWriteConflict = null;
  await loadGlobalWeaponLibrary();
  normalizeWeaponConfiguration3(state.package);
  ensureAnimationAuthoringConfig(state.package);
  ensureReactionAuthoringConfig(state.package);
  ensureGeometryCollections(state.package);
  state.validation = state.package.validationReport || { issues: [] };
  state.importResult = null;
  await loadUnityResourceCatalog();
  await loadWeaponTraceBakeCatalog();
  await loadCommonResources();
  await loadResourceLibraryAndPlan();
  await loadImportMaterials();
  await loadCharacterActionSets();
  await loadCharacterActionCatalog();
  await loadWeaponConfigurations();
  state.abilityChain = null;
  await loadReadiness();
  state.canWrite = false;
  state.apiAvailable = false;
  state.dirty = false;
  resetUndoHistory();
  state.message = "静态预览：保存、检查、导入资源或导入 Unity 前，请先启动 Authoring 服务。";
  globalThis.renderCharacterManagerToolbar?.();
}

async function loadBodyPresenceState(packageFallback = state.package) {
  const payload = await readJson(
    `/api/character/body-presence?package=${encodeURIComponent(state.packageRelative)}`,
    null);
  const normalized = normalizeBodyPresenceApiState(payload);
  if (normalized) {
    state.bodyPresence = normalized;
    return normalized;
  }

  const geometry = packageFallback?.geometry || {};
  const fallbackState = normalizeBodyPresenceDocumentStateValue(
    geometry.bodyPresenceDocumentState,
    "Unavailable");
  state.bodyPresence = {
    state: fallbackState,
    writeEligible: false,
    rawPayload: String(geometry.bodyPresenceRawPayload || ""),
    document: geometry.bodyPresence || null,
    canonicalContent: "",
    contentHash: "",
    packageRevision: state.packageRevision || "",
    source: "packageStateFallback"
  };
  return state.bodyPresence;
}

function normalizeBodyPresenceApiState(payload) {
  if (!payload || typeof payload !== "object") return null;
  const documentState = normalizeBodyPresenceDocumentStateValue(payload.state, "");
  if (!documentState) return null;
  return {
    state: documentState,
    writeEligible: Boolean(payload.writeEligible),
    rawPayload: String(payload.rawPayload || ""),
    document: payload.document || null,
    canonicalContent: String(payload.canonicalContent || ""),
    contentHash: String(payload.contentHash || ""),
    packageRevision: String(payload.packageRevision || ""),
    source: "dedicatedApi"
  };
}

function normalizeBodyPresenceDocumentStateValue(value, fallback = "Missing") {
  if (typeof value === "string") {
    return ["Missing", "Current", "UnsupportedSchema", "Invalid"].includes(value)
      ? value
      : fallback;
  }
  return ["Missing", "Current", "UnsupportedSchema", "Invalid"][Number(value)] || fallback;
}

async function saveBodyPresenceDocument() {
  const snapshot = state.bodyPresence;
  if (!state.canWrite || snapshot?.source !== "dedicatedApi"
    || snapshot.state !== "Current" || !snapshot.writeEligible || !snapshot.document) {
    state.message = "Body Presence 当前为只读状态，未发送保存请求。";
    renderShellStatus();
    return false;
  }

  const expectedRevision = snapshot.packageRevision || state.packageRevision || "";
  if (!expectedRevision) {
    state.message = "Body Presence 缺少 package revision，未发送保存请求。";
    renderShellStatus();
    return false;
  }

  state.bodyPresenceSaving = true;
  renderShellStatus();
  try {
    const response = await fetch(
      `/api/character/body-presence/save?package=${encodeURIComponent(state.packageRelative)}`,
      {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({
          expectedRevision,
          document: snapshot.document
        })
      });
    if (!response.ok) {
      const error = await readJsonResponseOrText(response);
      state.message = response.status === 409
        ? "Body Presence 保存发生 revision 冲突；请刷新状态后重试，原文件未被覆盖。"
        : summarizeApiFailure(error, `Body Presence 保存失败（HTTP ${response.status}），原文件未被覆盖。`);
      renderShellStatus();
      return false;
    }

    const saved = normalizeBodyPresenceApiState(await response.json());
    if (!saved) {
      state.message = "Body Presence 保存响应无效；请刷新状态确认磁盘内容。";
      renderShellStatus();
      return false;
    }
    state.bodyPresence = saved;
    if (state.packageRevision === expectedRevision) {
      state.packageRevision = saved.packageRevision || state.packageRevision;
    }
    state.message = `Body Presence 已通过独立 API 保存并重开：${getBodyPresenceVolumeCount(saved)} 个 volume。`;
    render();
    return true;
  } finally {
    state.bodyPresenceSaving = false;
    renderShellStatus();
  }
}

function getBodyPresenceVolumeCount(snapshot = state.bodyPresence) {
  return Array.isArray(snapshot?.document?.volumes) ? snapshot.document.volumes.length : 0;
}

async function loadBodyPartDamageState(packageFallback = state.package) {
  const payload = await readJson(
    `/api/character/body-part-damage?package=${encodeURIComponent(state.packageRelative)}`,
    null);
  const normalized = normalizeBodyPartDamageApiState(payload);
  if (normalized) {
    state.bodyPartDamage = normalized;
  } else {
    const geometry = packageFallback?.geometry || {};
    state.bodyPartDamage = {
      state: normalizeBodyPartDamageDocumentStateValue(
        geometry.bodyPartDamageDocumentState,
        "Unavailable"),
      writeEligible: false,
      rawPayload: String(geometry.bodyPartDamageRawPayload || ""),
      document: geometry.bodyPartDamage || null,
      contentHash: "",
      packageRevision: state.packageRevision || "",
      source: "packageStateFallback"
    };
  }
  state.bodyPartDamageDirty = false;
  state.bodyPartDamageValidation = null;
  state.cleanBodyPartDamageJson = serializeBodyPartDamageDocument(state.bodyPartDamage);
  return state.bodyPartDamage;
}

function normalizeBodyPartDamageApiState(payload) {
  if (!payload || typeof payload !== "object") return null;
  const documentState = normalizeBodyPartDamageDocumentStateValue(payload.state, "");
  if (!documentState) return null;
  return {
    state: documentState,
    writeEligible: Boolean(payload.writeEligible),
    rawPayload: String(payload.rawPayload || ""),
    document: payload.document || null,
    contentHash: String(payload.contentHash || ""),
    packageRevision: String(payload.packageRevision || ""),
    source: "dedicatedApi"
  };
}

function normalizeBodyPartDamageDocumentStateValue(value, fallback = "Missing") {
  if (typeof value === "string") {
    return ["Missing", "Current", "Future", "Invalid"].includes(value)
      ? value
      : fallback;
  }
  return ["Missing", "Current", "Future", "Invalid"][Number(value)] || fallback;
}

function serializeBodyPartDamageDocument(snapshot = state.bodyPartDamage) {
  return snapshot?.document ? JSON.stringify(snapshot.document) : "";
}

function refreshBodyPartDamageDirtyFromCleanSnapshot() {
  state.bodyPartDamageDirty = serializeBodyPartDamageDocument(state.bodyPartDamage)
    !== String(state.cleanBodyPartDamageJson || "");
  return state.bodyPartDamageDirty;
}

async function saveBodyPartDamageDocument(options = {}) {
  const snapshot = state.bodyPartDamage;
  if (!state.canWrite || snapshot?.source !== "dedicatedApi"
    || snapshot.state !== "Current" || !snapshot.document) {
    state.message = "部位伤害文档当前为只读状态，未发送保存请求。";
    renderShellStatus();
    return false;
  }

  const expectedRevision = snapshot.packageRevision || state.packageRevision || "";
  if (!expectedRevision) {
    state.message = "部位伤害文档缺少 package revision，未发送保存请求。";
    renderShellStatus();
    return false;
  }

  state.bodyPartDamageSaving = true;
  state.bodyPartDamageValidation = null;
  renderShellStatus();
  try {
    const response = await fetch(
      `/api/character/body-part-damage/save?package=${encodeURIComponent(state.packageRelative)}`,
      {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({
          expectedRevision,
          document: snapshot.document
        })
      });
    if (!response.ok) {
      const error = await readJsonResponseOrText(response);
      state.bodyPartDamageValidation = error?.validation || null;
      state.message = response.status === 409
        ? "部位伤害保存发生 revision 冲突；草稿和 Undo/Redo 均已保留，请刷新状态后处理。"
        : summarizeApiFailure(error, `部位伤害保存失败（HTTP ${response.status}）；草稿未丢失。`);
      render();
      return false;
    }

    const saved = normalizeBodyPartDamageApiState(await response.json());
    if (!saved) {
      state.message = "部位伤害保存响应无效；草稿未清空，请刷新状态确认磁盘内容。";
      renderShellStatus();
      return false;
    }
    state.bodyPartDamage = saved;
    state.bodyPartDamageDirty = false;
    state.bodyPartDamageValidation = null;
    state.cleanBodyPartDamageJson = serializeBodyPartDamageDocument(saved);
    state.packageRevision = saved.packageRevision || state.packageRevision;
    if (state.bodyPresence) state.bodyPresence.packageRevision = state.packageRevision;
    if (!options.suppressMessage) {
      const count = Array.isArray(saved.document?.partStates) ? saved.document.partStates.length : 0;
      state.message = `部位伤害已通过 T1 service 保存并重开：${count} 个 part state；顺序已按 partStateId 规范化。`;
    }
    if (!options.suppressRender) render();
    return true;
  } finally {
    state.bodyPartDamageSaving = false;
    renderShellStatus();
  }
}

async function loadPackageAnimationInventory() {
  if (!state.package || !state.packageRelative) return;
  state.package.animationInventory = await readJson(
    `/${state.packageRelative}/config/animation_inventory.json`,
    { entries: [] });
}

async function loadUnityResourceCatalog() {
  const packageId = state.package?.manifest?.packageId || "";
  state.unityResourceCatalog = null;
  state.unityResourceCatalogPath = "";
  if (!packageId) return;

  const catalogPath = `/Assets/MxFrameworkGenerated/CharacterPackages/${encodeURIComponent(packageId)}/config/unity_resource_catalog.json`;
  state.unityResourceCatalogPath = catalogPath.slice(1);
  state.unityResourceCatalog = await readJson(catalogPath, null);
}

async function loadWeaponTraceBakeCatalog() {
  const packageId = state.package?.manifest?.packageId || "";
  state.weaponTraceBakeCatalog = null;
  state.weaponTraceBakeCatalogPath = "";
  if (!packageId) return;

  const catalogPath = `/Assets/MxFrameworkGenerated/CharacterPackages/${encodeURIComponent(packageId)}/generated/weapon_trace_bake_catalog.json`;
  state.weaponTraceBakeCatalogPath = catalogPath.slice(1);
  state.weaponTraceBakeCatalog = await readJson(catalogPath, null);
}

async function loadCommonResources() {
  const payload = await readJson("/api/authoring/common-resources", null);
  state.commonResources = payload || {
    success: false,
    unavailable: true,
    sourceRoot: "ArtSource/Common",
    items: [],
    manifests: [],
    summary: { total: 0, ready: 0, changed: 0, blocked: 0, unityPending: 0 },
    message: "公共复用资源 API 不可用。请重启 Authoring 服务以启用 ArtSource/Common 扫描。"
  };
}

function findCommonResourceItemByKey(resourceKey) {
  const key = String(resourceKey || "");
  if (!key) return null;
  return (state.commonResources?.items || []).find(item => item?.resourceKey === key) || null;
}

function isCommonPreviewModelResource(resource) {
  return resource?.sourceProviderId === "resourceMapping";
}

async function loadResourceLibraryAndPlan() {
  const apiLibrary = await readJson(`/api/authoring/resources?package=${encodeURIComponent(state.packageRelative)}`, null);
  state.resourceLibrary = normalizeResourceLibraryPayload(apiLibrary);

  const apiPlan = await readJson(`/api/authoring/resources/resource-plan?package=${encodeURIComponent(state.packageRelative)}`, null);
  const packageId = state.package?.manifest?.packageId || "";
  const staticPlan = packageId
    ? await readJson(`/Assets/MxFrameworkGenerated/CharacterPackages/${encodeURIComponent(packageId)}/config/character_resource_plan.json`, null)
    : null;
  state.resourcePlan = normalizeResourcePlanPayload(apiPlan || staticPlan);
}

async function loadImportMaterials() {
  const apiMaterials = await readJson(`/api/character/import-materials?package=${encodeURIComponent(state.packageRelative)}`, null);
  if (apiMaterials?.document) {
    state.importMaterials = normalizeImportMaterialsPayload(apiMaterials);
    state.importMaterialsDirty = false;
    state.importMaterialsTemplateStaged = false;
    state.importMaterialSourceCandidates = null;
    reconcileImportMaterialSelection();
    return;
  }

  const packageId = state.package?.manifest?.packageId || "";
  const sourceManifestRelative = packageId ? `ArtSource/Characters/${packageId}/character_sources.json` : "";
  const sourceManifest = sourceManifestRelative ? await readJson(`/${sourceManifestRelative}`, null) : null;
  const legacyDocument = sourceManifest ? null : await readJson(`/${state.packageRelative}/config/import_materials.json`, null);
  const document = sourceManifest ? convertCharacterSourceManifestToImportMaterialsDocument(sourceManifest, sourceManifestRelative) : legacyDocument;
  state.importMaterials = normalizeImportMaterialsPayload(document
    ? {
      success: true,
      document,
      sourceManifestRelative,
      sourceManifestExists: Boolean(sourceManifest),
      legacyDocumentExists: Boolean(legacyDocument),
      documentKind: sourceManifest ? "sourceManifest" : "legacyPackageDocument",
      items: [],
      summary: { total: Array.isArray(document.items) ? document.items.length : 0 }
    }
    : {
      success: false,
      document: createEmptyImportMaterialsDocument(),
      sourceManifestRelative,
      sourceManifestExists: false,
      legacyDocumentExists: false,
      documentKind: "missing",
      items: [],
      summary: { total: 0 }
    });
  state.importMaterialsDirty = false;
  state.importMaterialsTemplateStaged = false;
  state.importMaterialSourceCandidates = null;
  reconcileImportMaterialSelection();
}

async function loadImportMaterialSourceCandidates(options = {}) {
  const document = getImportMaterialsDocument();
  const sourceRoot = options.sourceRoot ?? state.importMaterialSourcePickerRoot ?? document.sourceRoot ?? "";
  const query = options.query ?? state.importMaterialSourcePickerSearch ?? "";
  const url = `/api/character/import-materials/source-candidates?package=${encodeURIComponent(state.packageRelative)}&sourceRoot=${encodeURIComponent(sourceRoot)}&query=${encodeURIComponent(query)}`;
  const payload = await readJson(url, null);
  state.importMaterialSourceCandidates = normalizeImportMaterialSourceCandidatesPayload(payload, sourceRoot, query);
  return state.importMaterialSourceCandidates;
}

async function ensureImportMaterialSourceCandidates(options = {}) {
  const document = getImportMaterialsDocument();
  const sourceRoot = options.sourceRoot ?? state.importMaterialSourcePickerRoot ?? document.sourceRoot ?? "";
  const query = options.query ?? state.importMaterialSourcePickerSearch ?? "";
  const existing = state.importMaterialSourceCandidates;
  if (options.force || !existing || existing.sourceRoot !== sourceRoot || existing.query !== query) {
    return loadImportMaterialSourceCandidates({ query });
  }
  return existing;
}

async function loadReadiness() {
  state.readiness = await readJson(`/api/character/readiness?package=${encodeURIComponent(state.packageRelative)}`, null);
  const unityImportStage = (state.readiness?.stages || []).find(stage => stage?.id === "UnityImport");
  const editorRequestId = String(unityImportStage?.metrics?.editorRequestId || "").trim();
  if (editorRequestId) {
    const editorRebuildStatus = await readJson(
      `/api/character/import-unity/editor-status?id=${encodeURIComponent(editorRequestId)}`,
      null);
    if (editorRebuildStatus) {
      state.importResult = {
        ...(state.importResult || {}),
        editorRebuildStatus
      };
    }
  }
}

async function loadAbilityChain() {
  state.abilityChain = await readJson(`/api/character/ability-chain?package=${encodeURIComponent(state.packageRelative)}`, null);
  const scenarios = Array.isArray(state.abilityChain?.scenarios) ? state.abilityChain.scenarios : [];
  const selectedStillExists = scenarios.some(item =>
    String(item?.configurationId || item?.scenarioId || "") === String(state.activeCombinedWeaponConfigurationId || ""));
  if (!selectedStillExists) {
    state.activeCombinedWeaponConfigurationId = state.combinedWeaponConfigurationsSnapshot?.defaultCombinedWeaponConfigurationId
      || state.abilityChain?.defaultScenarioId
      || scenarios[0]?.scenarioId
      || "";
  }
}

async function loadWeaponConfigurations() {
  state.combinedWeaponConfigurationsSnapshot = await readJson(`/api/character/weapon-configurations?package=${encodeURIComponent(state.packageRelative)}`, null);
  const configurations = Array.isArray(state.combinedWeaponConfigurationsSnapshot?.combinedWeaponConfigurations)
    ? state.combinedWeaponConfigurationsSnapshot.combinedWeaponConfigurations
    : Array.isArray(state.package?.applicationConfig?.combinedWeaponConfigurations)
      ? state.package.applicationConfig.combinedWeaponConfigurations
    : [];
  const selectedStillExists = configurations.some(item =>
    String(item?.combinedWeaponConfigurationId || "") === String(state.activeCombinedWeaponConfigurationId || ""));
  if (!selectedStillExists) {
    state.activeCombinedWeaponConfigurationId = state.package?.applicationConfig?.defaultCombinedWeaponConfigurationId
      || configurations[0]?.combinedWeaponConfigurationId
      || "";
  }
}

async function loadCharacterActionCatalog() {
  state.characterActionCatalog = await readJson(
    `/api/character-actions/catalog?package=${encodeURIComponent(state.packageRelative)}`,
    null);
}

async function loadGlobalWeaponLibrary() {
  const apiState = await readJson("/api/weapon-library/state", null);
  if (apiState?.library) {
    state.weaponLibrary = clone(apiState.library);
    state.weaponLibraryRevision = apiState.revision || state.weaponLibraryRevision || "";
  } else {
    state.weaponLibrary = await readJson("/WeaponSource/global_weapon_library.json", {
      schemaVersion: "6.0",
      libraryId: "mx.global.single-weapons",
      singleWeaponConfigurations: [],
      combinedWeaponConfigurations: [],
      animationConfigurations: [],
      abilityConfigurations: [],
      reactionConfigurations: []
    });
    state.weaponLibraryRevision = "";
  }
  state.weaponLibraryDirty = false;
  state.weaponLibraryWriteConflict = null;
  state.weaponLibrary.combinedWeaponConfigurations = Array.isArray(state.weaponLibrary?.combinedWeaponConfigurations)
    ? state.weaponLibrary.combinedWeaponConfigurations : [];
  const singles = state.weaponLibrary?.singleWeaponConfigurations || [];
  state.singleWeaponLibraryBaseline = clone(singles);
  state.combinedWeaponConfigurationLibraryBaseline = clone(state.weaponLibrary.combinedWeaponConfigurations);
  state.animationConfigurationLibraryBaseline = clone(state.weaponLibrary?.animationConfigurations || []);
  state.reactionConfigurationLibraryBaseline = clone(state.weaponLibrary?.reactionConfigurations || []);
  if (!singles.some(item => item?.singleWeaponConfigurationId === state.activeSingleWeaponConfigurationId))
    state.activeSingleWeaponConfigurationId = singles[0]?.singleWeaponConfigurationId || "";
  for (const [collection, stateField, idField] of [
    ["animationConfigurations", "activeAnimationConfigurationId", "animationConfigurationId"],
    ["abilityConfigurations", "activeAbilityConfigurationId", "abilityConfigurationId"],
    ["reactionConfigurations", "activeReactionConfigurationId", "reactionConfigurationId"]
  ]) {
    state.weaponLibrary[collection] = Array.isArray(state.weaponLibrary[collection])
      ? state.weaponLibrary[collection] : [];
    if (!state.weaponLibrary[collection].some(item => item?.[idField] === state[stateField]))
      state[stateField] = state.weaponLibrary[collection][0]?.[idField] || "";
  }
  await globalThis.loadSingleWeaponReferenceIndex?.({ force: true, renderAfter: false });
}

function defineSchema3Alias(target, legacyName, currentName) {
  if (!target || Object.prototype.hasOwnProperty.call(target, legacyName)) return;
  Object.defineProperty(target, legacyName, {
    get: () => target[currentName],
    set: value => { target[currentName] = value; },
    enumerable: false,
    configurable: true
  });
}

function normalizeActionConfiguration(actionConfig) {
  if (!actionConfig || typeof actionConfig !== "object") return;
  actionConfig.actions = Array.isArray(actionConfig.actions) ? actionConfig.actions : [];
  actionConfig.commandSequences = Array.isArray(actionConfig.commandSequences) ? actionConfig.commandSequences : [];
  actionConfig.commandBindings = Array.isArray(actionConfig.commandBindings) ? actionConfig.commandBindings : [];
  actionConfig.abilityActionBindings = Array.isArray(actionConfig.abilityActionBindings) ? actionConfig.abilityActionBindings : [];
  actionConfig.reactionActionBindings = Array.isArray(actionConfig.reactionActionBindings) ? actionConfig.reactionActionBindings : [];

  // 1. Actions: ensure actionId is a valid positive integer, drop empty or non-numeric entries
  actionConfig.actions = actionConfig.actions
    .map(entry => {
      if (!entry || typeof entry !== "object") return null;
      const num = Number(entry.actionId);
      if (!Number.isInteger(num) || num <= 0) return null;
      return {
        actionRole: String(entry.actionRole || "").trim(),
        actionId: num
      };
    })
    .filter(entry => entry && entry.actionRole);

  // 2. AbilityActionBindings: ensure numeric abilityId and actionId
  for (const binding of actionConfig.abilityActionBindings) {
    if (!binding || typeof binding !== "object") continue;
    binding.abilityId = Math.round(Number(binding.abilityId)) || 0;
    binding.actionRole = String(binding.actionRole || "").trim();
    binding.actionId = Math.round(Number(binding.actionId)) || 0;
  }

  // 3. ReactionActionBindings: ensure numeric actionId
  for (const binding of actionConfig.reactionActionBindings) {
    if (!binding || typeof binding !== "object") continue;
    binding.reactionSlotId = String(binding.reactionSlotId || "").trim();
    binding.actionRole = String(binding.actionRole || "").trim();
    binding.actionId = Math.round(Number(binding.actionId)) || 0;
  }

  // 4. CommandBindings: ensure numeric actionId, priority, queueWindowFrames
  for (const binding of actionConfig.commandBindings) {
    if (!binding || typeof binding !== "object") continue;
    binding.intentId = String(binding.intentId || "").trim();
    binding.actionRole = String(binding.actionRole || "").trim();
    binding.actionId = Math.round(Number(binding.actionId)) || 0;
    binding.priority = Math.round(Number(binding.priority)) || 0;
    binding.allowQueue = Boolean(binding.allowQueue);
    binding.queueWindowFrames = Math.round(Number(binding.queueWindowFrames)) || 0;
  }

  // 5. CommandSequences: ensure queueWindowFrames and priority are integers
  for (const sequence of actionConfig.commandSequences) {
    if (!sequence || typeof sequence !== "object") continue;
    sequence.intentId = String(sequence.intentId || "").trim();
    sequence.actionRoles = Array.isArray(sequence.actionRoles)
      ? sequence.actionRoles.map(r => String(r || "").trim()).filter(Boolean)
      : [];
    sequence.wrapAfterLast = Boolean(sequence.wrapAfterLast);
    sequence.priority = Math.round(Number(sequence.priority)) || 0;
    sequence.allowQueue = Boolean(sequence.allowQueue);
    sequence.queueWindowFrames = Math.round(Number(sequence.queueWindowFrames)) || 0;
  }
}

function normalizeWeaponConfiguration4(pkg) {
  const app = pkg.applicationConfig ||= {};
  app.schemaVersion ||= "8.0";
  app.combinedWeaponConfigurationBindings = Array.isArray(app.combinedWeaponConfigurationBindings)
    ? app.combinedWeaponConfigurationBindings : [];
  for (const binding of app.combinedWeaponConfigurationBindings) {
    binding.overrides = binding?.overrides && typeof binding.overrides === "object"
      ? binding.overrides : {};
    binding.overrides.animationConfigurationId ||= "";
    binding.overrides.abilityConfigurationId ||= "";
    binding.overrides.reactionConfigurationId ||= "";
    if (binding.overrides.actionConfiguration) {
      normalizeActionConfiguration(binding.overrides.actionConfiguration);
    }
  }
  let legacyCombined = Array.isArray(app.combinedWeaponConfigurations)
    ? app.combinedWeaponConfigurations : [];
  delete app.combinedWeaponConfigurations;
  Object.defineProperty(app, "combinedWeaponConfigurations", {
    get: () => resolveCharacterCombinedWeaponConfigurations(app, legacyCombined),
    set: value => { legacyCombined = Array.isArray(value) ? value : []; },
    enumerable: false,
    configurable: true
  });
  defineSchema3Alias(app, "weaponConfigurations", "combinedWeaponConfigurations");
  defineSchema3Alias(app, "defaultWeaponConfigurationId", "defaultCombinedWeaponConfigurationId");
  Object.defineProperty(app, "weaponDefinitions", {
    get: () => state.weaponLibrary?.singleWeaponConfigurations || [],
    enumerable: false,
    configurable: true
  });
  for (const single of state.weaponLibrary?.singleWeaponConfigurations || [])
    defineSchema3Alias(single, "weaponId", "singleWeaponConfigurationId");
  state.weaponLibrary.combinedWeaponConfigurations = Array.isArray(state.weaponLibrary?.combinedWeaponConfigurations)
    ? state.weaponLibrary.combinedWeaponConfigurations : [];
  for (const configuration of state.weaponLibrary.combinedWeaponConfigurations) {
    defineSchema3Alias(configuration, "configurationId", "combinedWeaponConfigurationId");
    configuration.slots = Array.isArray(configuration.slots) ? configuration.slots : [];
    for (const slot of configuration.slots) {
      defineSchema3Alias(slot, "weaponId", "singleWeaponConfigurationId");
      delete slot.attachmentId;
    }
    configuration.animationConfigurationId ||= "";
    configuration.abilityConfigurationId ||= "";
    configuration.reactionConfigurationId ||= "";
    configuration.actionConfiguration ||= {
      actions: [],
      commandSequences: [],
      abilityActionBindings: [],
      reactionActionBindings: []
    };
    normalizeActionConfiguration(configuration.actionConfiguration);
  }
  const geometry = pkg.geometry ||= {};
  geometry.weaponSlots = Array.isArray(geometry.weaponSlots) ? geometry.weaponSlots : [];
  geometry.weaponAttachments = Array.isArray(geometry.weaponAttachments) ? geometry.weaponAttachments : [];
  for (const attachment of geometry.weaponAttachments) {
    defineSchema3Alias(attachment, "weaponId", "singleWeaponConfigurationId");
    defineSchema3Alias(attachment, "equipSlot", "slotId");
  }
}

function resolveCharacterCombinedWeaponConfigurations(app, legacyCombined = []) {
  const publicItems = state.weaponLibrary?.combinedWeaponConfigurations || [];
  const bindings = app?.combinedWeaponConfigurationBindings || [];
  if (!bindings.length && legacyCombined.length) return legacyCombined;
  return bindings.map(binding => {
    const defaults = publicItems.find(item =>
      String(item?.combinedWeaponConfigurationId || "") === String(binding?.combinedWeaponConfigurationId || ""));
    if (!defaults) return null;
    const overrides = binding?.overrides || {};
    const effective = clone(defaults);
    for (const field of ["animationConfigurationId", "abilityConfigurationId", "reactionConfigurationId"]) {
      const value = String(overrides?.[field] || "").trim();
      if (value) effective[field] = value;
    }
    if (overrides.actionConfiguration != null)
      effective.actionConfiguration = clone(overrides.actionConfiguration);
    return effective;
  }).filter(Boolean);
}

const normalizeWeaponConfiguration3 = normalizeWeaponConfiguration4;

async function loadCharacterActionSets() {
  const actionSetRoot = getCharacterActionPackageSourceFolder("actionSets");
  state.characterActionSets = await readJson(`/${actionSetRoot}/character_action_sets.json`, null);
  state.characterReactionProfiles = await readJson(`/${actionSetRoot}/character_reaction_profiles.json`, null);
}

async function saveReactionProfileConfig() {
  if (usesCompilerOwnedReactionProjection()) {
    state.message = "Schema 8 的受击配置由组合武器和编译器统一生成；运行结果页禁止直接改写。";
    return true;
  }
  if (String(state.package?.applicationConfig?.schemaVersion || "") === "4.0") return true;
  const profile = getDefaultReactionProfile();
  if (!profile) return true;
  const actionSetContext = getCurrentReactionActionSetContext();
  const actionSet = actionSetContext.actionSet || {};
  const reactionProfileId = getGeneratedReactionProfileStableId(state.package);
  const payload = {
    package: state.packageRelative,
    packageId: state.package?.manifest?.packageId || "",
    packageSegment: getCharacterActionPackageSegment(state.package),
    actionSetStableId: actionSet.stableId || `${getCharacterActionPackageSegment(state.package)}.default`,
    reactionProfileId,
    displayName: profile.displayName || `${reactionProfileId} Reaction`,
    slots: (profile.slots || []).map(slot => ({
      slotId: slot.slotId || "",
      displayName: slot.displayName || "",
      trigger: slot.trigger || "",
      pressureBand: slot.pressureBand || "",
      reactionGroupId: slot.reactionGroupId || "",
      minImpactForce: slot.minImpactForce ?? null,
      maxImpactForce: slot.maxImpactForce ?? null,
      actionId: slot.actionId || getGeneratedReactionActionId(state.package, slot),
      generatedActionId: getGeneratedReactionActionId(state.package, slot),
      durationFrames: Number(slot.durationFrames || 1),
      priority: Number(slot.priority || 0),
      resourceKey: firstNonEmpty(
        slot.resourceKey,
        slot.resourceSelection?.runtimeResourceKey,
        slot.resourceSelection?.packageResourceKey,
        slot.resourceSelection?.providerResourceKey),
      resourceSelection: slot.resourceSelection || {}
    }))
  };

  const result = await postJsonDetailed("/api/character-action/reaction-profile/save", payload);
  if (!result?.success) {
    state.message = firstNonEmpty(
      result?.message,
      result?.error,
      "保存 ActionSet 受击 Profile 失败。");
    showToast(state.message, "error", { title: "受击 Profile 保存失败" });
    renderShellStatus();
    return false;
  }

  await loadCharacterActionSets();
  await loadResourceLibraryAndPlan();
  return true;
}

async function regenerateReactionProfileConfig() {
  if (!state.canWrite) {
    state.message = "静态预览不能重新生成 CharacterAction 受击 Profile。请启动 Authoring 服务。";
    renderShellStatus();
    return false;
  }

  const saved = await saveReactionProfileConfig();
  if (!saved) return false;
  await loadReadiness();
  state.message = "已重新生成 CharacterAction 受击 Profile 交接 JSON。";
  render();
  return true;
}

async function readStaticPackage(root) {
  const [manifest, resourceCatalog, bodyProfile, bodyParts, colliders, sockets, weaponSlots, attachments, traces, contactPoints, appConfig, animationAuthoring, validation, hurtboxFitDraft, paletteBinding, paletteUsage, baseAttributes, bakedSurfaceBinding, tacticalDefaults] = await Promise.all([
    readJson(`/${root}/manifest.json`, {}),
    readJson(`/${root}/resource_catalog.json`, { entries: [] }),
    readJson(`/${root}/geometry/body_geometry.json`, {}),
    readJson(`/${root}/geometry/body_parts.json`, { bodyParts: [] }),
    readJson(`/${root}/geometry/body_colliders.json`, { colliders: [] }),
    readJson(`/${root}/geometry/sockets.json`, { sockets: [] }),
    readJson(`/${root}/geometry/weapon_slots.json`, { weaponSlots: [] }),
    readJson(`/${root}/geometry/weapon_attachments.json`, { attachments: [] }),
    readJson(`/${root}/geometry/traces.json`, { traces: [] }),
    readJson(`/${root}/geometry/body_part_contact_points.json`, { contactPoints: [] }),
    readJson(`/${root}/config/character_application.json`, {}),
    readJson(`/${root}/config/animation_authoring.json`, { profiles: [], sets: [] }),
    readJson(`/${root}/validation/last_report.json`, { issues: [] }),
    readJson(`/${root}/package_cache/hurtbox_fit_draft.json`, null),
    readJson(`/${root}/config/character_palette_binding.json`, null),
    readJson(`/${root}/config/character_palette_usage.json`, null),
    readJson(`/${root}/config/character_attributes.json`, null),
    readJson(`/${root}/config/character_baked_surface_binding.json`, null),
    readJson(`/${root}/config/character_tactical_defaults.json`, null)
  ]);
  const pkg = {
    manifest,
    resourceCatalog,
    geometry: {
      schemaVersion: bodyParts.schemaVersion || colliders.schemaVersion || contactPoints.schemaVersion || "1.0",
      bodyProfile,
      bodyParts: bodyParts.bodyParts || [],
      colliders: colliders.colliders || [],
      sockets: sockets.sockets || [],
      weaponSlots: weaponSlots.weaponSlots || [],
      weaponAttachments: attachments.attachments || [],
      traces: traces.traces || [],
      bodyPartContactPoints: contactPoints.contactPoints || []
    },
    applicationConfig: appConfig,
    tacticalDefaults,
    baseAttributes,
    animationAuthoring,
    validationReport: validation,
    hurtboxFitDraft,
    bakedSurfaceBinding,
    paletteBinding,
    paletteUsage
  };
  ensureAnimationAuthoringConfig(pkg);
  ensureReactionAuthoringConfig(pkg);
  return pkg;
}

async function savePackage(options = {}) {
  if (!state.canWrite) {
    state.message = "静态预览不能保存。请启动 Authoring 服务。";
    showToast(state.message, "warn");
    renderShellStatus();
    return;
  }
  const reactionProfileSaved = await saveReactionProfileConfig();
  if (!reactionProfileSaved) return;
  const packageWasDirty = Boolean(state.dirty);
  const bodyPartDamageWasDirty = Boolean(state.bodyPartDamageDirty);
  const sourceManifestWasDirty = Boolean(globalThis.hasUnsavedImportMaterialsChanges?.());
  const savedParts = [];
  const bakedSurfaceOwned = Boolean(state.package?.bakedSurfaceBinding)
    || (state.package?.bakedSurfaceIssues || []).length > 0;
  if (!bakedSurfaceOwned && state.package?.paletteBinding) {
    const previousMessage = state.message;
    const bindingSaved = await globalThis.saveSurfacePaletteBinding?.({ suppressMessage: false });
    if (!bindingSaved) {
      const reason = state.message && state.message !== previousMessage
        ? state.message
        : "角色画板 Binding 保存失败；Package 保存已停止。";
      state.message = reason;
      showToast(reason, "error", { title: "画板 Binding 保存失败" });
      renderShellStatus();
      return;
    }
    savedParts.push("character_palette_binding.json");
  }
  if (!bakedSurfaceOwned && state.package?.paletteBinding?.enabled) {
    const previousMessage = state.message;
    const usageSaved = await globalThis.saveSurfacePalettePreviewUsage?.({ suppressMessage: false });
    if (!usageSaved) {
      const reason = state.message && state.message !== previousMessage
        ? state.message
        : "全局画板 usage 未生成，Package 保存已停止；请等待 3D 预览完成后重试。";
      state.message = reason;
      showToast(reason, "warn", { title: "画板 Usage 未就绪" });
      renderShellStatus();
      return;
    }
    savedParts.push("character_palette_usage.json");
  }
  if (state.weaponLibraryDirty) {
    const librarySaved = await saveGlobalWeaponLibrary(options);
    if (!librarySaved) return;
    savedParts.push("全局单一武器库");
  }
  if (bodyPartDamageWasDirty) {
    const previousMessage = state.message;
    const bodyPartDamageSaved = await saveBodyPartDamageDocument({
      suppressMessage: false,
      suppressRender: true
    });
    if (!bodyPartDamageSaved) {
      const reason = state.message && state.message !== previousMessage
        ? state.message
        : "部位伤害草稿保存失败；Package 保存已停止。";
      state.message = reason;
      showToast(reason, "error", { title: "部位伤害保存失败" });
      renderShellStatus();
      return;
    }
    savedParts.push("geometry/body_part_damage.json");
  }
  if (packageWasDirty) {
    const response = await fetch(`/api/character/save?package=${encodeURIComponent(state.packageRelative)}`, {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({
        package: state.package,
        expectedRevision: state.packageRevision || "",
        force: Boolean(options.force || state.packageWriteConflict?.operation === "save")
      })
    });
    if (!response.ok) {
      const error = await readJsonResponseOrText(response);
      if (response.status === 409 && error?.code === "CHARACTER_PACKAGE_WRITE_CONFLICT") {
        state.packageWriteConflict = { ...error, operation: "save" };
        state.message = "角色包已被其他进程修改。可点击“刷新状态”保留本地草稿并查看新状态，或再次点击“强制保存”覆盖磁盘版本；只有“放弃改动并重载”会清空草稿。";
        showToast(state.message, "warn", {
          title: "角色包版本冲突 (409)",
          duration: 8000,
          actionLabel: "刷新状态",
          onAction: () => globalThis.refreshExternalState?.()
        });
        globalThis.renderCharacterManagerToolbar?.();
      } else if (error?.code === "CHARACTER_PACKAGE_VALIDATION_BLOCKED") {
        if (error.validation) state.validation = error.validation;
        const blockerCount = (error?.validation?.issues || []).filter(issue =>
          issue?.severity === "Error" || issue?.gate === "ExportBlocked").length;
        state.message = blockerCount > 0
          ? `Package 草稿未保存：有 ${blockerCount} 个结构错误必须先修复。详细内容已放到“诊断”。`
          : (error?.message || "Package 草稿未保存。详细内容已放到“诊断”。");
        showToast(state.message, "error", {
          title: "结构校验未通过",
          actionLabel: "查看诊断",
          onAction: () => globalThis.openWorkflowInspectorTab?.("diagnostics")
        });
        state.activeInspectorTab = "diagnostics";
        render();
        openWorkflowInspectorTab?.("diagnostics");
        return;
      } else {
        state.message = summarizeApiFailure(error, `Package 保存失败（HTTP ${response.status}）。`);
        showToast(state.message, "error", { title: "Package 保存失败" });
      }
      renderShellStatus();
      return;
    }
    const data = await response.json();
    const paletteBinding = state.package?.paletteBinding || null;
    const paletteUsage = state.package?.paletteUsage || null;
    const bakedSurfaceBinding = state.package?.bakedSurfaceBinding || null;
    const bakedSurfaceIssues = state.package?.bakedSurfaceIssues || [];
    state.package = data.package;
    state.package.bakedSurfaceBinding = bakedSurfaceBinding;
    state.package.bakedSurfaceIssues = bakedSurfaceIssues;
    state.package.paletteBinding = paletteBinding;
    state.package.paletteUsage = paletteUsage;
    normalizeWeaponConfiguration3(state.package);
    state.packageRevision = data.revision || "";
    if (state.bodyPresence) state.bodyPresence.packageRevision = state.packageRevision;
    if (state.bodyPartDamage) state.bodyPartDamage.packageRevision = state.packageRevision;
    state.packageWriteConflict = null;
    state.validation = data.validation;
    state.sourceSyncConflictPaths = [];
    state.dirty = false;
    resetUndoHistory();
    savedParts.push("package JSON");
  }
  if (sourceManifestWasDirty) {
    const manifestSaved = await globalThis.saveImportMaterialsIfDirty?.({ suppressMessage: true });
    if (!manifestSaved) {
      state.message = packageWasDirty
        ? "Package JSON 已保存，但 character_sources.json 保存失败。"
        : "character_sources.json 保存失败。";
      showToast(state.message, "error");
      renderShellStatus();
      return;
    }
    savedParts.push("character_sources.json");
  }
  if (savedParts.length === 0) {
    state.message = "没有需要保存的 Package JSON、body_part_damage.json 或 character_sources.json 改动。";
    showToast(state.message, "info");
    renderShellStatus();
    return;
  }
  if (bodyPartDamageWasDirty && !packageWasDirty) resetUndoHistory();
  await loadResourceLibraryAndPlan();
  await loadReadiness();
  if (!hasUnsavedCharacterStudioChanges?.()) clearSavedCharacterStudioDraft?.();
  else persistCharacterStudioSessionState?.();
  state.message = `已保存 ${savedParts.join(" 和 ")}。`;
  showToast(state.message, "success", { title: "保存成功" });
  render();
}

async function saveGlobalWeaponLibrary(options = {}) {
  try {
    normalizeWeaponConfiguration4(state.package);
    for (const configuration of state.weaponLibrary?.combinedWeaponConfigurations || []) {
      normalizeActionConfiguration(configuration?.actionConfiguration);
    }
    const response = await fetch("/api/weapon-library/save", {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({
        library: state.weaponLibrary,
        expectedRevision: state.weaponLibraryRevision || "",
        force: Boolean(options.force || state.weaponLibraryWriteConflict)
      })
    });
    if (!response.ok) {
      const error = await readJsonResponseOrText(response);
      if (response.status === 409 && error?.code === "GLOBAL_WEAPON_LIBRARY_WRITE_CONFLICT") {
        state.weaponLibraryWriteConflict = error;
        state.message = "全局武器库已被外部修改。可点击“刷新状态”同步最新数据，或再次点击“保存”以强制覆盖。";
        showToast(state.message, "warn", {
          title: "武器库版本冲突 (409)",
          duration: 8000,
          actionLabel: "刷新状态",
          onAction: () => globalThis.refreshExternalState?.()
        });
      } else {
        state.message = error?.message || error?.error || `全局武器库保存失败：HTTP ${response.status}`;
        showToast(state.message, "error", { title: "全局武器库保存失败" });
      }
      renderShellStatus();
      return false;
    }
    const data = await response.json();
    state.weaponLibrary = clone(data.library);
    state.weaponLibraryRevision = data.revision || "";
    state.weaponLibraryDirty = false;
    state.weaponLibraryWriteConflict = null;
    state.singleWeaponLibraryBaseline = clone(state.weaponLibrary?.singleWeaponConfigurations || []);
    state.combinedWeaponConfigurationLibraryBaseline = clone(state.weaponLibrary?.combinedWeaponConfigurations || []);
    state.animationConfigurationLibraryBaseline = clone(state.weaponLibrary?.animationConfigurations || []);
    state.reactionConfigurationLibraryBaseline = clone(state.weaponLibrary?.reactionConfigurations || []);
    normalizeWeaponConfiguration3(state.package);
    await globalThis.loadSingleWeaponReferenceIndex?.({ force: true, renderAfter: false });
    return true;
  } catch (err) {
    state.message = `全局武器库保存网络异常：${err?.message || err}`;
    showToast(state.message, "error");
    renderShellStatus();
    return false;
  }
}

async function compilePackage() {
  if (state.weaponLibraryDirty) {
    state.message = "全局单一武器库还有未保存草稿；保存后才能执行 Prefab 重建预检。";
    showToast(state.message, "warn");
    renderShellStatus();
    return;
  }
  if (!await globalThis.saveImportMaterialsIfDirty?.({ operationLabel: "compile" })) {
    state.message = "character_sources.json 保存失败，Prefab 预检已停止。";
    showToast(state.message, "error");
    renderShellStatus();
    return;
  }
  showToast("正在执行 Prefab 重建预检…", "info", { duration: 2500 });
  const data = await readJson(`/api/character/compile?package=${encodeURIComponent(state.packageRelative)}&checkHashes=false`, null);
  if (!data) {
    state.message = "Prefab 预检失败，或 Authoring 服务不可用。";
    showToast(state.message, "error");
    renderShellStatus();
    return;
  }
  state.compileResult = data;
  state.resourcePlan = normalizeResourcePlanPayload(data.resourcePlan || data.characterResourcePlan || data.plan) || state.resourcePlan;
  await loadReadiness();
  state.message = `Prefab 重建预检完成：${formatApiStatusLabel(data.status)}`;
  showToast(state.message, data.status === "Ready" ? "success" : "info");
  renderResourcePlanPreview();
  renderReadinessPanel();
  renderDiagnostics();
}

async function importUnity() {
  if (!state.unityImportBuildTarget) {
    state.message = "请选择本次 Unity 导入使用的目标平台。";
    showToast(state.message, "warn");
    renderShellStatus();
    return;
  }
  if (state.weaponLibraryDirty) {
    state.message = "全局单一武器库还有未保存草稿；保存后才能执行 Unity 导入。";
    showToast(state.message, "warn");
    renderShellStatus();
    return;
  }
  if (!await globalThis.saveImportMaterialsIfDirty?.({ operationLabel: "Unity import" })) {
    state.message = "character_sources.json 保存失败，Unity 导入已停止。";
    showToast(state.message, "error");
    renderShellStatus();
    renderImportStatus();
    return;
  }
  if (isImportBlocked()) {
    state.message = "Unity 导入被阻塞。请先修复阻塞项。";
    showToast(state.message, "error", {
      actionLabel: "查看诊断",
      onAction: () => globalThis.openWorkflowInspectorTab?.("diagnostics")
    });
    renderShellStatus();
    renderImportStatus();
    return;
  }
  startUnityImportTask();
  state.activeInspectorTab = "import";
  state.message = "Unity 导入进行中：正在写入创作资产、Unity 文件、重建 Prefab，并刷新 CharacterAction。";
  renderShellStatus();
  renderInspector();
  renderImportStatus();
  try {
    const response = await fetch(`/api/character/import-unity?package=${encodeURIComponent(state.packageRelative)}`, {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({
        package: state.packageRelative,
        unityRoot: "Assets/MxFrameworkGenerated/CharacterPackages",
        buildTarget: state.unityImportBuildTarget,
        checkHashes: false,
        dryRun: false
      })
    });
    state.importResult = await readJsonResponseOrText(response);
    if (response.ok && state.importResult?.success) {
      resetUndoHistory();
    }
    if (response.ok && state.importResult?.editorRebuild?.requestId) {
      state.importResult.editorRebuildStatus = await pollUnityImportEditorRebuild(state.importResult.editorRebuild.requestId);
    }
    await loadCommonResources();
    await loadUnityResourceCatalog();
    await loadResourceLibraryAndPlan();
    await loadReadiness();
    const importAcceptance = buildUnityImportAcceptance(state.importResult);
    const importSucceeded = response.ok && importAcceptance.status === "ready";
    finishUnityImportTask(importSucceeded, state.importResult, response.ok);
    state.message = importSucceeded
      ? "Unity 导入完成。Unity Editor bridge 已验收 Avatar、Prefab、默认武器和 CharacterAction 状态。"
      : "Unity 导入需要处理。请查看 Unity 导入验收详情。";
    renderShellStatus();
    renderResourceBindingBar();
    renderResourcePicker();
    renderResourcePlanPreview();
    renderReadinessPanel();
    renderInspector();
    renderImportStatus();
  } catch (error) {
    const message = error instanceof Error ? error.message : String(error);
    state.importResult = { success: false, stderr: message };
    finishUnityImportTask(false, state.importResult, false);
    state.message = `Unity 导入请求失败：${message}`;
    renderShellStatus();
    renderImportStatus();
  }
}

async function pollUnityImportEditorRebuild(requestId, options = {}) {
  const attempts = options.attempts || 90;
  const delayMs = options.delayMs || 1000;
  let latest = null;
  for (let attempt = 0; attempt < attempts; attempt++) {
    latest = await readJson(`/api/character/import-unity/editor-status?id=${encodeURIComponent(requestId)}`, null);
    if (latest) {
      state.importResult = {
        ...(state.importResult || {}),
        editorRebuildStatus: latest
      };
      if (state.unityImportTask) {
        state.unityImportTask = {
          ...state.unityImportTask,
          result: state.importResult,
          steps: buildUnityImportTaskSteps(state.importResult, { status: state.unityImportTask.status, success: state.unityImportTask.status === "success" })
        };
        renderInspector();
        renderImportStatus();
      }
      const status = String(latest.status || "").toLowerCase();
      if (status === "completed" || status === "failed" || status === "error")
        return latest;
    }
    await delay(delayMs);
  }
  if (latest) {
    const status = String(latest.status || "").toLowerCase();
    if (status === "pending" || status === "running") {
      return {
        ...latest,
        status: "timeout",
        timedOut: true,
        message: "CharacterStudio 等待结束前，Unity Editor Prefab 重建尚未完成。请检查 Unity Console 或刷新导入状态。"
      };
    }
  }
  const fallback = state.importResult?.editorRebuild || null;
  return fallback
    ? {
      ...fallback,
      status: "timeout",
      timedOut: true,
      message: "CharacterStudio 等待结束前，Unity Editor Prefab 重建状态不可用。"
    }
    : null;
}

function formatApiStatusLabel(status) {
  const value = String(status || "Unknown");
  const labels = {
    Ready: "就绪",
    Blocked: "阻塞",
    Warning: "需处理",
    Deferred: "延后",
    Pending: "等待",
    Running: "运行中",
    Success: "成功",
    Failed: "失败",
    Error: "错误",
    Unknown: "未知"
  };
  return labels[value] || value;
}

function delay(ms) {
  return new Promise(resolve => setTimeout(resolve, ms));
}

async function readJsonResponseOrText(response) {
  const text = await response.text();
  if (!text) return {};
  try {
    return JSON.parse(text);
  } catch {
    return {
      success: false,
      status: response.ok ? "Unknown" : "Failed",
      stderr: text
    };
  }
}

function summarizeApiFailure(error, fallback = "操作失败。") {
  const raw = firstNonEmpty(error?.message, error?.error, error?.stderr, fallback);
  const firstLine = String(raw || fallback).split(/\r?\n/, 1)[0].replace(/\s+/g, " ").trim();
  return firstLine.length > 220 ? `${firstLine.slice(0, 217)}...` : firstLine;
}

async function copyReport() {
  const text = JSON.stringify({
    validation: state.validation,
    readiness: state.readiness,
    abilityChain: getAbilityChainView(),
    compile: state.compileResult,
    import: state.importResult,
    importMaterials: state.importMaterials,
    resourceLibrary: state.resourceLibrary || { items: getResourceLibraryItems().map(item => ({ libraryItemId: item.libraryItemId, stableId: item.stableId, kind: item.kind, usage: item.usage, runtimeAvailability: item.runtimeAvailability, referenceCount: item.referenceCount })) },
    resourcePlan: getCharacterResourcePlan(),
    unityResourceCatalogPath: state.unityResourceCatalogPath,
    unityResourceCatalog: state.unityResourceCatalog
  }, null, 2);
  await navigator.clipboard?.writeText(text);
  state.message = "诊断、Unity 导入和同步报告已复制。";
  renderShellStatus();
}

async function readJson(path, fallback = null) {
  try {
    const response = await fetch(path, { cache: "no-store" });
    if (!response.ok) return fallback;
    return await response.json();
  } catch {
    return fallback;
  }
}

async function postJson(path, body, fallback = null) {
  try {
    const response = await fetch(path, {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify(body)
    });
    if (!response.ok) return fallback;
    return await response.json();
  } catch {
    return fallback;
  }
}

async function postJsonDetailed(path, body) {
  try {
    const response = await fetch(path, {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify(body)
    });
    const text = await response.text();
    let parsed = null;
    if (text) {
      try {
        parsed = JSON.parse(text);
      } catch {
        parsed = null;
      }
    }
    if (!response.ok) {
      return {
        ...(parsed || {}),
        success: false,
        statusCode: response.status,
        statusText: response.statusText,
        message: firstNonEmpty(parsed?.message, parsed?.error, text, response.statusText)
      };
    }
    return parsed || { success: true };
  } catch (error) {
    return {
      success: false,
      statusCode: 0,
      message: error?.message || "Network request failed."
    };
  }
}

Object.assign(globalThis, {
  loadAll,
  loadPackages,
  loadPackageState,
  loadBodyPresenceState,
  normalizeBodyPresenceApiState,
  normalizeBodyPresenceDocumentStateValue,
  saveBodyPresenceDocument,
  getBodyPresenceVolumeCount,
  loadBodyPartDamageState,
  normalizeBodyPartDamageApiState,
  normalizeBodyPartDamageDocumentStateValue,
  serializeBodyPartDamageDocument,
  refreshBodyPartDamageDirtyFromCleanSnapshot,
  saveBodyPartDamageDocument,
  loadPackageAnimationInventory,
  loadUnityResourceCatalog,
  loadCommonResources,
  findCommonResourceItemByKey,
  isCommonPreviewModelResource,
  saveReactionProfileConfig,
  regenerateReactionProfileConfig,
  loadResourceLibraryAndPlan,
  loadImportMaterials,
  loadImportMaterialSourceCandidates,
  ensureImportMaterialSourceCandidates,
  loadReadiness,
  loadAbilityChain,
  loadWeaponConfigurations,
  loadGlobalWeaponLibrary,
  normalizeWeaponConfiguration3,
  normalizeWeaponConfiguration4,
  normalizeActionConfiguration,
  loadCharacterActionSets,
  loadCharacterActionCatalog,
  readStaticPackage,
  savePackage,
  saveGlobalWeaponLibrary,
  compilePackage,
  importUnity,
  pollUnityImportEditorRebuild,
  delay,
  readJsonResponseOrText,
  refreshExternalState,
  discardDraftAndReload,
  copyReport,
  readJson,
  postJson,
  postJsonDetailed
});
