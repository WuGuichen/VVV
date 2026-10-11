// CharacterStudio refresh/session preservation.
// Draft data stays in sessionStorage only; UI state may survive a later browser session.

const CHARACTER_STUDIO_SESSION_VERSION = 1;
const CHARACTER_STUDIO_DRAFT_STORAGE_PREFIX = "mx.characterStudio.draft.v1:";
const CHARACTER_STUDIO_UI_STORAGE_PREFIX = "mx.characterStudio.ui.v1:";
const CHARACTER_STUDIO_PREFERENCES_STORAGE_KEY = "mx.characterStudio.preferences.v1";
const CHARACTER_STUDIO_UI_FIELDS = [
  "selectedPath",
  "activeCombinedWeaponConfigurationId",
  "combinedWeaponWorkspaceView",
  "combinedWeaponSearchQuery",
  "combinedWeaponReadinessFilter",
  "combinedWeaponDefaultFilter",
  "combinedWeaponSort",
  "combinedWeaponExpandedOverviewId",
  "combinedWeaponSubWorkspace",
  "activeReactionConfigurationWorkbenchId",
  "activeReactionSlotId",
  "reactionConfigurationSearchQuery",
  "activeSingleWeaponConfigurationId",
  "singleWeaponPoseGuideVisible",
  "singleWeaponPoseReferenceId",
  "singleWeaponWorkspaceView",
  "singleWeaponSearchQuery",
  "singleWeaponCategoryFilter",
  "singleWeaponResourceFilter",
  "singleWeaponReferenceFilter",
  "singleWeaponSort",
  "singleWeaponReferenceDetailId",
  "singleWeaponExpandedOverviewId",
  "activeAnimationConfigurationId",
  "activeAbilityConfigurationId",
  "activeReactionConfigurationId",
  "previewPose",
  "previewMotion",
  "hurtboxDisplayMode",
  "hurtboxDisplayPartId",
  "hurtboxBindingReferenceVisible",
  "bonePickerSearchQuery",
  "bonePickerZoom",
  "bonePickerYaw",
  "bonePickerPitch",
  "viewportCameraState",
  "viewportCameraStates",
  "viewportMode",
  "bodyMeasurementsVisible",
  "bodyAdvancedBindingsOpen",
  "bodyPartsPreviewScope",
  "bodyPartAdvancedOpen",
  "movementSourceCapsuleVisible",
  "movementRegisteredBoundsVisible",
  "movementSourceDetailsOpen",
  "focusedWeaponAttachmentId",
  "previousCharacterSelection",
  "weaponTransformMode",
  "weaponTransformKey",
  "symmetryEditEnabled",
  "contextDockCollapsed",
  "activeContextTab",
  "activeRuntimeValidationTab",
  "activeInspectorTab",
  "selectedReactionSlotId",
  "treeCollapsed",
  "resourcePickerSearchQuery",
  "resourcePickerUsageFilter",
  "resourcePickerProviderFilter",
  "layers"
];

let characterStudioPersistenceTimer = 0;
let characterStudioPersistenceSuspendCount = 0;

function hasUnsavedCharacterStudioChanges() {
  return Boolean(state.dirty || state.bodyPartDamageDirty || state.weaponLibraryDirty || state.importMaterialsDirty
    || globalThis.hasUnsavedImportMaterialsChanges?.());
}

function getCharacterStudioStorageKey(prefix, packageRelative = state.packageRelative) {
  return `${prefix}${String(packageRelative || "default")}`;
}

function readCharacterStudioStorage(storage, key) {
  try {
    const json = storage?.getItem?.(key);
    return json ? JSON.parse(json) : null;
  } catch {
    return null;
  }
}

function writeCharacterStudioStorage(storage, key, value) {
  try {
    storage?.setItem?.(key, JSON.stringify(value));
    return true;
  } catch {
    return false;
  }
}

function removeCharacterStudioStorage(storage, key) {
  try {
    storage?.removeItem?.(key);
  } catch {
    // Storage is best-effort and must never block editing.
  }
}

function getCharacterStudioScrollKey(element, index) {
  if (element.id) return `id:${element.id}`;
  if (element.dataset?.contextPanel) return `context:${element.dataset.contextPanel}`;
  if (element.dataset?.validationPanel) return `validation:${element.dataset.validationPanel}`;
  return `class:${element.className || element.tagName}:${index}`;
}

function getCharacterStudioScrollableElements() {
  if (typeof document === "undefined") return [];
  return Array.from(document.querySelectorAll([
    "#packageTree",
    "#inspector",
    "#diagnostics",
    "#importStatus",
    "#resourcePickerList",
    "[data-context-panel]",
    "[data-validation-panel]",
    ".weapon-configuration-panel",
    ".single-weapon-workbench",
    ".combined-weapon-workbench",
    ".reaction-configuration-workbench"
  ].join(",")));
}

function getCharacterStudioDetailsState() {
  if (typeof document === "undefined") return [];
  return Array.from(document.querySelectorAll("details")).map((element, index) => ({
    index,
    open: Boolean(element.open)
  }));
}

function captureCharacterStudioScrollState() {
  const scroll = {};
  getCharacterStudioScrollableElements().forEach((element, index) => {
    const top = Number(element.scrollTop || 0);
    const left = Number(element.scrollLeft || 0);
    if (top || left) scroll[getCharacterStudioScrollKey(element, index)] = { top, left };
  });
  if (typeof window !== "undefined" && (window.scrollX || window.scrollY)) {
    scroll.window = { top: window.scrollY, left: window.scrollX };
  }
  return scroll;
}

function captureCharacterStudioUiSnapshot() {
  const fields = {};
  for (const field of CHARACTER_STUDIO_UI_FIELDS) fields[field] = clone(state[field]);
  return {
    version: CHARACTER_STUDIO_SESSION_VERSION,
    packageRelative: state.packageRelative,
    fields,
    resourceBindingRole: el.resourceBindingRole?.value || "preview",
    topPanelCollapsed: Boolean(el.topPanelContainer?.classList?.contains("collapsed")
      || (typeof document !== "undefined" && document.getElementById("topPanelContainer")?.classList.contains("collapsed"))),
    details: getCharacterStudioDetailsState(),
    scroll: captureCharacterStudioScrollState()
  };
}

function applyCharacterStudioUiSnapshot(snapshot) {
  if (!snapshot || snapshot.version !== CHARACTER_STUDIO_SESSION_VERSION) return false;
  for (const field of CHARACTER_STUDIO_UI_FIELDS) {
    if (Object.prototype.hasOwnProperty.call(snapshot.fields || {}, field)) {
      state[field] = clone(snapshot.fields[field]);
    }
  }
  state.resourcePickerOpen = false;
  state.resourcePickerField = null;
  state.resourcePickerQuery = null;
  state.characterManagerMode = "";
  if (el.resourceBindingRole && snapshot.resourceBindingRole) {
    el.resourceBindingRole.value = snapshot.resourceBindingRole;
  }
  return true;
}

function restoreCharacterStudioDomState(snapshot) {
  if (!snapshot || typeof document === "undefined") return;
  const restore = () => {
    const topPanel = el.topPanelContainer || document.getElementById("topPanelContainer");
    const topToggle = document.getElementById("topToggleBtn");
    topPanel?.classList.toggle("collapsed", Boolean(snapshot.topPanelCollapsed));
    topToggle?.classList.toggle("floating", Boolean(snapshot.topPanelCollapsed));
    const topToggleIcon = topToggle?.querySelector("svg");
    if (topToggleIcon) topToggleIcon.style.transform = snapshot.topPanelCollapsed ? "rotate(180deg)" : "rotate(0deg)";
    const details = Array.from(document.querySelectorAll("details"));
    for (const item of snapshot.details || []) {
      if (details[item.index]) details[item.index].open = Boolean(item.open);
    }
    getCharacterStudioScrollableElements().forEach((element, index) => {
      const position = snapshot.scroll?.[getCharacterStudioScrollKey(element, index)];
      if (!position) return;
      element.scrollTop = Number(position.top || 0);
      element.scrollLeft = Number(position.left || 0);
    });
    if (snapshot.scroll?.window) {
      window.scrollTo(Number(snapshot.scroll.window.left || 0), Number(snapshot.scroll.window.top || 0));
    }
  };
  if (typeof requestAnimationFrame === "function") requestAnimationFrame(() => requestAnimationFrame(restore));
  else setTimeout(restore, 0);
}

function createCharacterStudioDraftSnapshot() {
  if (!hasUnsavedCharacterStudioChanges()) return null;
  const documentHistoryDirty = Boolean(state.dirty || state.bodyPartDamageDirty);
  return {
    version: CHARACTER_STUDIO_SESSION_VERSION,
    packageRelative: state.packageRelative,
    savedAt: new Date().toISOString(),
    packageRevision: state.packageRevision || "",
    weaponLibraryRevision: state.weaponLibraryRevision || "",
    package: state.dirty ? clone(state.package) : null,
    cleanPackageJson: state.cleanPackageJson || "",
    packageDirty: Boolean(state.dirty),
    bodyPartDamagePackageRevision: state.bodyPartDamage?.packageRevision || "",
    bodyPartDamage: state.bodyPartDamageDirty ? clone(state.bodyPartDamage) : null,
    cleanBodyPartDamageJson: state.cleanBodyPartDamageJson || "",
    bodyPartDamageDirty: Boolean(state.bodyPartDamageDirty),
    weaponLibrary: state.weaponLibraryDirty ? clone(state.weaponLibrary) : null,
    weaponLibraryDirty: Boolean(state.weaponLibraryDirty),
    singleWeaponLibraryBaseline: state.weaponLibraryDirty ? clone(state.singleWeaponLibraryBaseline || []) : [],
    combinedWeaponConfigurationLibraryBaseline: state.weaponLibraryDirty ? clone(state.combinedWeaponConfigurationLibraryBaseline || []) : [],
    animationConfigurationLibraryBaseline: state.weaponLibraryDirty ? clone(state.animationConfigurationLibraryBaseline || []) : [],
    reactionConfigurationLibraryBaseline: state.weaponLibraryDirty ? clone(state.reactionConfigurationLibraryBaseline || []) : [],
    importMaterials: state.importMaterialsDirty ? clone(state.importMaterials) : null,
    importMaterialsDirty: Boolean(state.importMaterialsDirty),
    importMaterialsSelectedIds: Array.from(state.importMaterialsSelectedIds || []),
    undoStack: documentHistoryDirty ? clone(state.undoStack || []) : [],
    redoStack: documentHistoryDirty ? clone(state.redoStack || []) : [],
    ui: captureCharacterStudioUiSnapshot()
  };
}

function restoreCharacterStudioDraftSnapshot(snapshot, loadedRevisions = {}) {
  if (!snapshot || snapshot.version !== CHARACTER_STUDIO_SESSION_VERSION
    || snapshot.packageRelative !== state.packageRelative) return { restored: false, conflicts: [] };
  const conflicts = [];
  if (snapshot.packageDirty && snapshot.package) {
    if (loadedRevisions.packageRevision && snapshot.packageRevision
      && loadedRevisions.packageRevision !== snapshot.packageRevision) conflicts.push("角色包");
    state.package = clone(snapshot.package);
    state.packageRevision = snapshot.packageRevision || state.packageRevision;
    state.cleanPackageJson = snapshot.cleanPackageJson || state.cleanPackageJson;
    state.dirty = true;
    state.undoStack = clone(snapshot.undoStack || []);
    state.redoStack = clone(snapshot.redoStack || []);
  }
  if (snapshot.bodyPartDamageDirty && snapshot.bodyPartDamage) {
    if (loadedRevisions.packageRevision && snapshot.bodyPartDamagePackageRevision
      && loadedRevisions.packageRevision !== snapshot.bodyPartDamagePackageRevision) conflicts.push("部位伤害文档");
    state.bodyPartDamage = clone(snapshot.bodyPartDamage);
    state.cleanBodyPartDamageJson = snapshot.cleanBodyPartDamageJson || "";
    state.bodyPartDamageDirty = true;
    state.undoStack = clone(snapshot.undoStack || []);
    state.redoStack = clone(snapshot.redoStack || []);
  }
  if (snapshot.weaponLibraryDirty && snapshot.weaponLibrary) {
    if (loadedRevisions.weaponLibraryRevision && snapshot.weaponLibraryRevision
      && loadedRevisions.weaponLibraryRevision !== snapshot.weaponLibraryRevision) conflicts.push("全局武器库");
    state.weaponLibrary = clone(snapshot.weaponLibrary);
    state.weaponLibraryRevision = snapshot.weaponLibraryRevision || state.weaponLibraryRevision;
    state.weaponLibraryDirty = true;
    state.singleWeaponLibraryBaseline = clone(snapshot.singleWeaponLibraryBaseline || []);
    state.combinedWeaponConfigurationLibraryBaseline = clone(snapshot.combinedWeaponConfigurationLibraryBaseline || []);
    state.animationConfigurationLibraryBaseline = clone(snapshot.animationConfigurationLibraryBaseline || []);
    state.reactionConfigurationLibraryBaseline = clone(snapshot.reactionConfigurationLibraryBaseline || []);
  }
  if (snapshot.importMaterialsDirty && snapshot.importMaterials) {
    const loadedImportMaterials = clone(state.importMaterials || {});
    const draftImportMaterials = clone(snapshot.importMaterials);
    const draftDocument = clone(draftImportMaterials.document || draftImportMaterials);
    const loadedStatusesById = new Map((loadedImportMaterials.items || []).map(item => [String(item?.id || item?.item?.id || ""), item]));
    const draftItems = Array.isArray(draftDocument.items) ? draftDocument.items : [];
    const mergedStatuses = draftItems.map(item => ({
      ...(loadedStatusesById.get(String(item?.id || "")) || {}),
      id: item?.id || "",
      item: clone(item)
    }));
    const mergedImportMaterials = {
      ...loadedImportMaterials,
      document: draftDocument,
      items: mergedStatuses,
      summary: null
    };
    state.importMaterials = globalThis.normalizeImportMaterialsPayload
      ? globalThis.normalizeImportMaterialsPayload(mergedImportMaterials)
      : mergedImportMaterials;
    state.importMaterialsDirty = true;
    state.importMaterialsSelectedIds = new Set(snapshot.importMaterialsSelectedIds || []);
  }
  if (snapshot.weaponLibraryDirty && typeof globalThis.normalizeWeaponConfiguration4 === "function" && state.package) {
    globalThis.normalizeWeaponConfiguration4(state.package);
  }
  applyCharacterStudioUiSnapshot(snapshot.ui);
  return {
    restored: Boolean(snapshot.packageDirty || snapshot.bodyPartDamageDirty
      || snapshot.weaponLibraryDirty || snapshot.importMaterialsDirty),
    conflicts
  };
}

function persistCharacterStudioSessionState() {
  if (characterStudioPersistenceSuspendCount || !state.packageRelative || typeof window === "undefined") return;
  persistCharacterStudioPreferences();
  const uiSnapshot = captureCharacterStudioUiSnapshot();
  writeCharacterStudioStorage(window.localStorage,
    getCharacterStudioStorageKey(CHARACTER_STUDIO_UI_STORAGE_PREFIX), uiSnapshot);
  const draftKey = getCharacterStudioStorageKey(CHARACTER_STUDIO_DRAFT_STORAGE_PREFIX);
  const draftSnapshot = createCharacterStudioDraftSnapshot();
  if (draftSnapshot) writeCharacterStudioStorage(window.sessionStorage, draftKey, draftSnapshot);
  else removeCharacterStudioStorage(window.sessionStorage, draftKey);
}

function readCharacterStudioPreferences() {
  if (typeof window === "undefined") return null;
  const value = readCharacterStudioStorage(window.localStorage, CHARACTER_STUDIO_PREFERENCES_STORAGE_KEY);
  return value?.version === CHARACTER_STUDIO_SESSION_VERSION ? value : null;
}

function persistCharacterStudioPreferences() {
  if (typeof window === "undefined") return;
  writeCharacterStudioStorage(window.localStorage, CHARACTER_STUDIO_PREFERENCES_STORAGE_KEY, {
    version: CHARACTER_STUDIO_SESSION_VERSION,
    packageRelative: String(state.packageRelative || ""),
    unityImportBuildTarget: String(state.unityImportBuildTarget || "")
  });
  syncCharacterStudioPreferenceQuery();
}

function syncCharacterStudioPreferenceQuery() {
  if (typeof window === "undefined" || !window.history?.replaceState) return;
  const url = new URL(window.location.href);
  if (state.packageRelative) url.searchParams.set("package", String(state.packageRelative));
  else url.searchParams.delete("package");
  if (state.unityImportBuildTarget) url.searchParams.set("buildTarget", String(state.unityImportBuildTarget));
  else url.searchParams.delete("buildTarget");
  window.history.replaceState(window.history.state, "", url);
}

function restoreCharacterStudioPreferences() {
  if (typeof window === "undefined") return false;
  const preferences = readCharacterStudioPreferences();
  if (!preferences) return false;
  const query = new URLSearchParams(window.location.search);
  if (!query.get("package") && preferences.packageRelative) {
    state.packageRelative = String(preferences.packageRelative);
    state.userSelectedPackage = true;
  }
  if (!query.get("buildTarget") && preferences.unityImportBuildTarget) {
    state.unityImportBuildTarget = String(preferences.unityImportBuildTarget);
  }
  return true;
}

function scheduleCharacterStudioSessionPersistence() {
  if (characterStudioPersistenceSuspendCount || typeof window === "undefined") return;
  clearTimeout(characterStudioPersistenceTimer);
  characterStudioPersistenceTimer = window.setTimeout(persistCharacterStudioSessionState, 120);
}

function suspendCharacterStudioSessionPersistence() {
  characterStudioPersistenceSuspendCount++;
  clearTimeout(characterStudioPersistenceTimer);
}

function resumeCharacterStudioSessionPersistence() {
  characterStudioPersistenceSuspendCount = Math.max(0, characterStudioPersistenceSuspendCount - 1);
}

function readSavedCharacterStudioUiSnapshot(packageRelative = state.packageRelative) {
  if (typeof window === "undefined") return null;
  return readCharacterStudioStorage(window.localStorage,
    getCharacterStudioStorageKey(CHARACTER_STUDIO_UI_STORAGE_PREFIX, packageRelative));
}

function readSavedCharacterStudioDraftSnapshot(packageRelative = state.packageRelative) {
  if (typeof window === "undefined") return null;
  return readCharacterStudioStorage(window.sessionStorage,
    getCharacterStudioStorageKey(CHARACTER_STUDIO_DRAFT_STORAGE_PREFIX, packageRelative));
}

function clearSavedCharacterStudioDraft(packageRelative = state.packageRelative) {
  if (typeof window === "undefined") return;
  removeCharacterStudioStorage(window.sessionStorage,
    getCharacterStudioStorageKey(CHARACTER_STUDIO_DRAFT_STORAGE_PREFIX, packageRelative));
}

function registerCharacterStudioSessionProtection() {
  if (typeof window === "undefined" || window.__mxCharacterStudioSessionProtection) return;
  window.__mxCharacterStudioSessionProtection = true;
  window.addEventListener("pagehide", persistCharacterStudioSessionState);
  for (const eventName of ["input", "change", "click", "pointerup", "keyup"]) {
    window.addEventListener(eventName, scheduleCharacterStudioSessionPersistence, true);
  }
  window.addEventListener("beforeunload", event => {
    persistCharacterStudioSessionState();
    if (!hasUnsavedCharacterStudioChanges()) return;
    event.preventDefault();
    event.returnValue = "";
  });
}

Object.assign(globalThis, {
  hasUnsavedCharacterStudioChanges,
  captureCharacterStudioUiSnapshot,
  applyCharacterStudioUiSnapshot,
  restoreCharacterStudioDomState,
  createCharacterStudioDraftSnapshot,
  restoreCharacterStudioDraftSnapshot,
  persistCharacterStudioSessionState,
  persistCharacterStudioPreferences,
  restoreCharacterStudioPreferences,
  scheduleCharacterStudioSessionPersistence,
  suspendCharacterStudioSessionPersistence,
  resumeCharacterStudioSessionPersistence,
  readSavedCharacterStudioUiSnapshot,
  readSavedCharacterStudioDraftSnapshot,
  clearSavedCharacterStudioDraft,
  registerCharacterStudioSessionProtection
});

export {
  hasUnsavedCharacterStudioChanges,
  captureCharacterStudioUiSnapshot,
  applyCharacterStudioUiSnapshot,
  createCharacterStudioDraftSnapshot,
  restoreCharacterStudioDraftSnapshot
};
