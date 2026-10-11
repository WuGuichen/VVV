// CharacterStudio split module. Registers functions on globalThis to preserve the original call graph.

function scaleX(x = 0) {
  return 50 + Number(x || 0) * 42;
}

function scaleY(y = 0) {
  return 90 - Number(y || 0) * 39;
}

function getNested(obj, path) {
  return path.split(".").reduce((acc, key) => acc == null ? undefined : acc[key], obj);
}

function setNested(obj, path, value) {
  const parts = path.split(".");
  let cursor = obj;
  for (let i = 0; i < parts.length - 1; i++) {
    if (cursor[parts[i]] == null) cursor[parts[i]] = {};
    cursor = cursor[parts[i]];
  }
  cursor[parts[parts.length - 1]] = value;
}

function isImportBlocked() {
  if (Number(state.importMaterials?.integrityReceipt?.blockingIssueCount || 0) > 0) return true;
  const gate = state.compileResult?.gateReport;
  if (gate?.importBlocked || gate?.exportBlocked) return true;
  const issues = [
    ...(state.validation?.issues || []),
    ...(gate?.issues || [])
  ];
  return issues.some(issue => issue.gate === "ImportBlocked" || issue.gate === "ExportBlocked");
}

function clone(value) {
  return JSON.parse(JSON.stringify(value));
}

function escapeHtml(value) {
  return String(value ?? "").replace(/[&<>"']/g, ch => ({
    "&": "&amp;",
    "<": "&lt;",
    ">": "&gt;",
    "\"": "&quot;",
    "'": "&#39;"
  }[ch]));
}

function firstNonEmpty(...values) {
  for (const value of values) {
    if (value == null) continue;
    const text = String(value).trim();
    if (text) return text;
  }
  return "";
}

function createEmptyResourceSelectionRef() {
  return {
    resourceStableId: "",
    sourceProviderId: "",
    bindingKind: "None",
    expectedKind: "",
    expectedUsage: "",
    expectedHash: "",
    unityGuid: "",
    unityAssetPath: "",
    runtimeResourceKey: "",
    providerResourceKey: "",
    packageResourceKey: "",
    audioCueId: "",
    audioEventDefinitionId: ""
  };
}

function getResourceSelectionKey(selection) {
  return firstNonEmpty(
    selection?.runtimeResourceKey,
    selection?.packageResourceKey,
    selection?.providerResourceKey,
    selection?.resourceStableId,
    selection?.unityAssetPath,
    selection?.unityGuid);
}

function getWeaponAttachmentResourceKey(attachment) {
  if (!attachment) return "";
  return getResourceSelectionKey(getSingleWeaponConfiguration(
    attachment.singleWeaponConfigurationId || attachment.weaponId)?.weaponRef);
}

function applyWeaponAttachmentSelection(attachment, selection) {
  if (!attachment) return;
  const nextSelection = selection ? clone(selection) : createEmptyResourceSelectionRef();
  const definition = getSingleWeaponConfiguration(
    attachment.singleWeaponConfigurationId || attachment.weaponId);
  if (definition) definition.weaponRef = clone(nextSelection);
  state.weaponLibraryDirty = Boolean(definition);
  delete attachment.previewResourceSelection;
  delete attachment.previewResourceKey;
}

function clearWeaponAttachmentSelection(attachment) {
  applyWeaponAttachmentSelection(attachment, createEmptyResourceSelectionRef(), "");
}

function createPackageResourceSelectionRef(resource, expectedUsage = "weaponModel") {
  const selection = createEmptyResourceSelectionRef();
  if (!resource) return selection;
  selection.resourceStableId = firstNonEmpty(resource.stableId, resource.localId, resource.resourceKey);
  selection.sourceProviderId = "characterPackage";
  selection.bindingKind = "PackageResource";
  selection.expectedKind = "Model";
  selection.expectedUsage = expectedUsage || resource.usage || "";
  selection.providerResourceKey = resource.resourceKey || "";
  selection.packageResourceKey = resource.resourceKey || "";
  return selection;
}

function findDefaultWeaponAttachment(slot) {
  const attachments = state.package?.geometry?.weaponAttachments || [];
  const configuration = getActiveWeaponConfiguration();
  const selected = configuration?.slots?.find(item => String(item?.slotId || "") === String(slot || ""));
  return selected
    ? attachments.find(attachment => String(attachment?.attachmentId || "") === String(selected.attachmentId || "")) || null
    : null;
}

function getSingleWeaponConfiguration(singleWeaponConfigurationId) {
  return (state.weaponLibrary?.singleWeaponConfigurations || []).find(item =>
    String(item?.singleWeaponConfigurationId || "") === String(singleWeaponConfigurationId || "")) || null;
}

function getActiveCombinedWeaponConfiguration() {
  const configurations = state.package?.applicationConfig?.combinedWeaponConfigurations || [];
  return configurations.find(item =>
    String(item?.combinedWeaponConfigurationId || "") === String(state.activeCombinedWeaponConfigurationId || ""))
    || configurations.find(item =>
      String(item?.combinedWeaponConfigurationId || "") === String(state.package?.applicationConfig?.defaultCombinedWeaponConfigurationId || ""))
    || configurations[0]
    || null;
}

function getActiveWeaponConfiguration() {
  return getActiveCombinedWeaponConfiguration();
}

function getWeaponDefinition(weaponId) {
  return getSingleWeaponConfiguration(weaponId);
}

function showToast(message, type = "info", options = {}) {
  if (!message) return null;
  const container = document.getElementById("toastContainer") || createToastContainer();
  const toast = document.createElement("div");
  const id = `toast-${Date.now()}-${Math.random().toString(36).slice(2, 6)}`;
  toast.id = id;
  toast.className = `toast toast-${type}`;
  toast.setAttribute("role", type === "error" ? "alert" : "status");

  const icons = {
    error: `<svg viewBox="0 0 24 24" width="16" height="16" fill="none" stroke="currentColor" stroke-width="2"><circle cx="12" cy="12" r="10"/><line x1="12" y1="8" x2="12" y2="12"/><line x1="12" y1="16" x2="12.01" y2="16"/></svg>`,
    warn: `<svg viewBox="0 0 24 24" width="16" height="16" fill="none" stroke="currentColor" stroke-width="2"><path d="M10.29 3.86L1.82 18a2 2 0 0 0 1.71 3h16.94a2 2 0 0 0 1.71-3L13.71 3.86a2 2 0 0 0-3.42 0z"/><line x1="12" y1="9" x2="12" y2="13"/><line x1="12" y1="17" x2="12.01" y2="17"/></svg>`,
    success: `<svg viewBox="0 0 24 24" width="16" height="16" fill="none" stroke="currentColor" stroke-width="2"><path d="M22 11.08V12a10 10 0 1 1-5.93-9.14"/><polyline points="22 4 12 14.01 9 11.01"/></svg>`,
    info: `<svg viewBox="0 0 24 24" width="16" height="16" fill="none" stroke="currentColor" stroke-width="2"><circle cx="12" cy="12" r="10"/><line x1="12" y1="8" x2="12" y2="12"/><line x1="12" y1="16" x2="12.01" y2="16"/></svg>`
  };

  const iconHtml = icons[type] || icons.info;
  const actionHtml = options.actionLabel && options.onAction
    ? `<button type="button" class="toast-action">${escapeHtml(options.actionLabel)}</button>`
    : "";

  toast.innerHTML = `
    <span class="toast-icon">${iconHtml}</span>
    <div class="toast-content">
      ${options.title ? `<div class="toast-title">${escapeHtml(options.title)}</div>` : ""}
      <div class="toast-message">${escapeHtml(message)}</div>
    </div>
    ${actionHtml}
    <button type="button" class="toast-close" aria-label="关闭">&times;</button>
  `;

  if (options.onAction && options.actionLabel) {
    toast.querySelector(".toast-action")?.addEventListener("click", (e) => {
      e.stopPropagation();
      options.onAction();
      dismissToast(toast);
    });
  }

  toast.querySelector(".toast-close")?.addEventListener("click", () => dismissToast(toast));

  container.appendChild(toast);

  requestAnimationFrame(() => toast.classList.add("toast-visible"));

  const duration = options.duration ?? (type === "error" ? 6000 : type === "warn" ? 5000 : 3500);
  if (duration > 0) {
    toast._timeoutId = setTimeout(() => dismissToast(toast), duration);
  }

  return toast;
}

function dismissToast(toast) {
  if (!toast || toast._dismissed) return;
  toast._dismissed = true;
  if (toast._timeoutId) clearTimeout(toast._timeoutId);
  toast.classList.remove("toast-visible");
  toast.classList.add("toast-hiding");
  setTimeout(() => toast.remove(), 250);
}

function createToastContainer() {
  let container = document.getElementById("toastContainer");
  if (!container) {
    container = document.createElement("div");
    container.id = "toastContainer";
    container.className = "toast-container";
    container.setAttribute("aria-live", "polite");
    document.body.appendChild(container);
  }
  return container;
}

function notify(message, type = "info", options = {}) {
  state.message = message;
  showToast(message, type, options);
  if (typeof renderShellStatus === "function") {
    renderShellStatus();
  }
}

Object.assign(globalThis, {
  scaleX,
  scaleY,
  getNested,
  setNested,
  isImportBlocked,
  clone,
  escapeHtml,
  firstNonEmpty,
  createEmptyResourceSelectionRef,
  getResourceSelectionKey,
  getWeaponAttachmentResourceKey,
  applyWeaponAttachmentSelection,
  clearWeaponAttachmentSelection,
  createPackageResourceSelectionRef,
  findDefaultWeaponAttachment,
  getWeaponDefinition,
  getSingleWeaponConfiguration,
  getActiveWeaponConfiguration,
  getActiveCombinedWeaponConfiguration,
  showToast,
  dismissToast,
  createToastContainer,
  notify
});
