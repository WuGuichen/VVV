// Body navigation uses existing geometry objects; workspace paths do not create new authoring data.

function getBodyWorkspaceSection(path = state.selectedPath) {
  if (path === "geometry/body") return "overall";
  if (path === "geometry/body/capsule") return "capsule";
  if (path === "geometry/body/parts" || String(path || "").startsWith("geometry/body_parts/")
    || String(path || "").startsWith("geometry/colliders/")) return "parts";
  return "";
}

function isBodyWorkspacePath(path = state.selectedPath) {
  return Boolean(getBodyWorkspaceSection(path));
}

function getBodyWorkspaceSelection(path = state.selectedPath, geometry = state.package?.geometry || {}) {
  const section = getBodyWorkspaceSection(path);
  const collider = (geometry.colliders || []).find(item => path === `geometry/colliders/${item.colliderId}`);
  const part = (geometry.bodyParts || []).find(item => path === `geometry/body_parts/${item.partId}`
    || (collider && item.partId === collider.partId));
  const partId = part?.partId ?? collider?.partId;
  const partLabel = part?.displayName || part?.partId || (partId ? `未登记部位 · ${partId}` : "未指定部位");
  const label = section === "overall" ? "整体身体" : section === "capsule" ? "移动碰撞配置"
    : collider ? `${partLabel} → ${collider.colliderId}` : part ? partLabel : "部位与受击体";
  return { section, part, collider, partId, label };
}

function getBodyWorkspacePartColliders(colliders, geometry = state.package?.geometry || {}) {
  const selection = getBodyWorkspaceSelection(state.selectedPath, geometry);
  if (selection.section !== "parts") return [];
  if (!selection.part && !selection.collider) return colliders;
  return colliders.filter(item => (item.partId || "") === (selection.partId || ""));
}

function getBodyWorkspaceVisibleColliders(colliders, geometry = state.package?.geometry || {}) {
  const scope = getBodyPartsPreviewScope();
  if (getBodyWorkspaceSection() === "parts" && scope === "all") return colliders;
  if (scope === "selected") {
    const selected = getBodyWorkspaceSelection(state.selectedPath, geometry).collider;
    return selected ? colliders.filter(item => item.colliderId === selected.colliderId) : [];
  }
  return getBodyWorkspacePartColliders(colliders, geometry);
}

function getBodyPartsPreviewScope() {
  const selection = getBodyWorkspaceSelection();
  const scope = ["selected", "part", "all"].includes(state.bodyPartsPreviewScope) ? state.bodyPartsPreviewScope : "part";
  if (!selection.collider && !selection.part) return "all";
  return scope === "selected" && !selection.collider ? "part" : scope;
}

function setBodyPartsPreviewScope(scope) {
  state.bodyPartsPreviewScope = ["selected", "part", "all"].includes(scope) ? scope : "part";
  renderBodyWorkspace();
  if (state.hurtboxCoverageVisible) renderViewport();
  else if (typeof viewportSelectionSync === "function") viewportSelectionSync(state.selectedPath);
  else renderViewport();
  renderViewportModeBar();
  globalThis.scheduleCharacterStudioSessionPersistence?.();
}

function isBodyWorkspaceSelectionVisualSelected(objectPath, selectedPath = state.selectedPath) {
  if (objectPath === selectedPath) return true;
  if (!String(selectedPath || "").startsWith("geometry/body_parts/")) return false;
  const selection = getBodyWorkspaceSelection(selectedPath);
  return Boolean(selection.part && (state.package?.geometry?.colliders || [])
    .some(item => objectPath === `geometry/colliders/${item.colliderId}` && item.partId === selection.partId));
}

function getBodyWorkspacePickPath(path) {
  if (!isBodyWorkspacePath()) return path;
  return getBodyWorkspaceSection(path) === "parts" || path === "geometry/body/capsule" ? path : "";
}

function renderBodyWorkspace() {
  const host = document.getElementById("bodyWorkspaceNavigation");
  if (!host) return;
  const selection = getBodyWorkspaceSelection();
  host.hidden = !selection.section;
  if (!selection.section) return;
  const geometry = state.package?.geometry || {};
  const parts = geometry.bodyParts || [];
  const colliders = geometry.colliders || [];
  const visible = getBodyWorkspacePartColliders(colliders, geometry);
  const scope = getBodyPartsPreviewScope();
  const tabs = [
    ["overall", "geometry/body", "整体身体"],
    ["capsule", "geometry/body/capsule", "移动碰撞配置"],
    ["parts", "geometry/body/parts", "部位与受击体"]
  ];
  const oldScroll = Array.from(host.querySelectorAll("[data-body-list]"), item => [item.dataset.bodyList, item.scrollTop]);
  host.innerHTML = `<div class="body-workspace-heading"><strong>身体与受击</strong>
      <span aria-live="polite">${escapeHtml(selection.label)}</span>
      ${selection.section !== "overall" ? '<button type="button" data-body-path="geometry/body">返回整体身体</button>' : ""}</div>
    <nav class="body-workspace-tabs" aria-label="身体配置分类">${tabs.map(([key, path, label]) =>
      `<button type="button" data-body-path="${path}" class="${key === selection.section ? "active" : ""}" aria-current="${key === selection.section ? "page" : "false"}">${label}</button>`).join("")}
      ${selection.section === "parts" ? `<label class="body-preview-scope">预览范围<select data-body-preview-scope aria-label="受击体预览范围"><option value="selected"${scope === "selected" ? " selected" : ""}${!selection.collider ? " disabled" : ""}>仅选中</option><option value="part"${scope === "part" ? " selected" : ""}${!selection.part && !selection.collider ? " disabled" : ""}>当前部位</option><option value="all"${scope === "all" ? " selected" : ""}>全部</option></select></label>` : ""}</nav>
    ${selection.section === "parts" ? `<p class="body-workspace-description">这里配置伤害接收几何；移动代理与注册阻挡体在“移动碰撞配置”中查看。</p>
      <div class="body-parts-template-actions"><button type="button" data-humanoid-add disabled>补齐人形受击体</button><span data-humanoid-status>等待角色模型骨骼加载</span></div>
      <div class="body-workspace-lists">
      <section><div class="body-list-heading"><strong>身体部位 · ${parts.length}</strong>
        <button type="button" data-body-create="part"${!state.canWrite ? " disabled" : ""}>新增部位</button>
        <button type="button" data-body-path="geometry/body/parts" aria-pressed="${!selection.part && !selection.collider}">查看全部受击体</button></div>
        <div class="body-object-list" data-body-list="parts">${parts.map(part => renderBodyWorkspaceObjectButton(
          `geometry/body_parts/${part.partId}`, part.displayName || part.partId,
          `${colliders.filter(item => item.partId === part.partId).length} 个受击体`, part.partId === selection.partId)).join("")
          || '<p class="body-workspace-empty">尚未配置身体部位。</p>'}</div></section>
      <section><div class="body-list-heading"><strong>${selection.part || selection.collider ? "对应受击体" : "全部受击体"} · ${visible.length}</strong><button type="button" data-body-create="collider"${!state.canWrite || !selection.part ? " disabled" : ""} title="${selection.part ? "为当前所选部位添加受击体" : "请先选择一个身体部位"}">添加受击体</button></div>
        <div class="body-object-list" data-body-list="colliders">${visible.map(item => renderBodyWorkspaceObjectButton(
          `geometry/colliders/${item.colliderId}`, item.colliderId,
          ({ Capsule: "胶囊", Sphere: "球体", Box: "盒体" })[item.shape] || item.shape || "形状未设置",
          item === selection.collider)).join("") || '<p class="body-workspace-empty">当前范围没有受击体。</p>'}</div></section>
    </div>` : `<p class="body-workspace-description">${selection.section === "capsule"
      ? "青色为默认站立移动胶囊代理，紫色为注册到物理世界的阻挡盒体。检测方式随障碍类型变化。"
      : "这里编辑身体参数与尺寸参考。移动代理与注册阻挡体请在“移动碰撞配置”中查看。"}</p>`}
    ${selection.section === "overall" ? `<div class="body-measurement-controls"><button type="button" data-body-measurements-toggle aria-pressed="${state.bodyMeasurementsVisible !== false}">${state.bodyMeasurementsVisible !== false ? "隐藏" : "显示"}尺寸参考</button><span>青色：高度 · 黄色：半径 · 地面：Y = 0</span></div>` : ""}
    ${selection.section === "capsule" ? `<div class="body-measurement-controls"><button type="button" data-movement-bounds-toggle aria-pressed="${state.movementRegisteredBoundsVisible !== false}">${state.movementRegisteredBoundsVisible !== false ? "隐藏" : "显示"}注册盒体</button><button type="button" data-movement-source-toggle aria-pressed="${Boolean(state.movementSourceCapsuleVisible)}">${state.movementSourceCapsuleVisible ? "隐藏" : "显示"}源胶囊参考</button><span>青色：移动代理 · 紫色：注册盒体 · 橙色：源参考</span></div>` : ""}`;
  for (const [key, top] of oldScroll) {
    const list = host.querySelector(`[data-body-list="${key}"]`);
    if (list) list.scrollTop = top;
  }
  host.querySelectorAll("button[data-body-path]").forEach(button => button.addEventListener("click", () => {
    const path = button.dataset.bodyPath;
    selectPath(path, { preserveViewport: true });
    const selectedButton = Array.from(host.querySelectorAll("button[data-body-path]")).find(item => item.dataset.bodyPath === path);
    selectedButton?.focus?.({ preventScroll: true });
    selectedButton?.scrollIntoView?.({ block: "nearest", inline: "nearest" });
  }));
  host.querySelector("[data-body-preview-scope]")?.addEventListener("change", event => {
    setBodyPartsPreviewScope(event.target.value);
    host.querySelector("[data-body-preview-scope]")?.focus?.({ preventScroll: true });
  });
  host.querySelectorAll("[data-body-create]").forEach(button => button.addEventListener("click", () => {
    if (button.dataset.bodyCreate === "collider" && !getBodyWorkspaceSelection().part) return;
    createBodyWorkspaceItem(button.dataset.bodyCreate, { partId: getBodyWorkspaceSelection().part?.partId });
  }));
  host.querySelector("[data-humanoid-add]")?.addEventListener("click", () => globalThis.addHumanoidHurtboxes?.());
  globalThis.updateHumanoidHurtboxButton?.();
  host.querySelector("[data-body-measurements-toggle]")?.addEventListener("click", () => {
    state.bodyMeasurementsVisible = state.bodyMeasurementsVisible === false;
    renderBodyWorkspace();
    host.querySelector("[data-body-measurements-toggle]")?.focus?.({ preventScroll: true });
    if (!globalThis.refreshBodyMeasurementPreview?.()) renderViewport();
    globalThis.scheduleCharacterStudioSessionPersistence?.();
  });
  host.querySelector("[data-movement-source-toggle]")?.addEventListener("click", () => {
    state.movementSourceCapsuleVisible = !state.movementSourceCapsuleVisible;
    renderBodyWorkspace();
    host.querySelector("[data-movement-source-toggle]")?.focus?.({ preventScroll: true });
    if (!globalThis.refreshMovementCollisionPreview?.()) renderViewport();
    globalThis.scheduleCharacterStudioSessionPersistence?.();
  });
  host.querySelector("[data-movement-bounds-toggle]")?.addEventListener("click", () => {
    state.movementRegisteredBoundsVisible = state.movementRegisteredBoundsVisible === false;
    renderBodyWorkspace();
    host.querySelector("[data-movement-bounds-toggle]")?.focus?.({ preventScroll: true });
    if (!globalThis.refreshMovementCollisionPreview?.()) renderViewport();
    globalThis.scheduleCharacterStudioSessionPersistence?.();
  });
}

function bindBodyInspectorActions() {
  const details = el.inspector.querySelector("[data-body-advanced-bindings]");
  details?.addEventListener("toggle", () => {
    state.bodyAdvancedBindingsOpen = details.open;
    globalThis.scheduleCharacterStudioSessionPersistence?.();
  });
  const sourceDetails = el.inspector.querySelector("[data-movement-source-details]");
  sourceDetails?.addEventListener("toggle", () => {
    state.movementSourceDetailsOpen = sourceDetails.open;
    globalThis.scheduleCharacterStudioSessionPersistence?.();
  });
  el.inspector.querySelector("[data-movement-align-ground]")?.addEventListener("click", () => alignMovementCollisionToGround());
}

function renderBodyWorkspaceObjectButton(path, label, detail, active) {
  return `<button type="button" class="body-object-button${active ? " active" : ""}" data-body-path="${escapeHtml(path)}" aria-current="${Boolean(active)}">
    <span title="${escapeHtml(label)}">${escapeHtml(label)}</span><small>${escapeHtml(detail)}</small></button>`;
}

function renderBodyWorkspaceObjectTitle(target) {
  const selection = getBodyWorkspaceSelection();
  const character = [state.package?.manifest?.packageId, state.package?.manifest?.displayName].filter(Boolean).join(" · ");
  const kind = target.kind === "collider" ? "受击体" : target.kind === "part" ? "身体部位" : "";
  const usage = target.kind === "collider" ? '<p class="body-workspace-description">伤害接收几何 · 运行时使用角色受击层。所属部位与空间跟随分别配置。</p>' : "";
  return `<div class="object-title"><strong>${kind ? `${kind} · ` : ""}${escapeHtml(selection.label)}</strong><span>${escapeHtml(character)}</span></div>${usage}`;
}

function renderBodyPartsOverview() {
  const geometry = state.package?.geometry || {};
  return `${renderBodyWorkspaceObjectTitle({ kind: "bodyPartsOverview" })}<p class="body-workspace-description">从部位列表查看骨骼绑定，或从受击体列表查看形状、位置和尺寸。也可点击预览中的受击体。</p>
    <div class="reference-row"><span>身体部位</span><strong>${(geometry.bodyParts || []).length}</strong></div>
    <div class="reference-row"><span>受击体</span><strong>${(geometry.colliders || []).length}</strong></div>`;
}

function getBodyWorkspaceFields(target, fields) {
  if (["part", "collider"].includes(target.kind)) return globalThis.getBodyPartEditorFields?.(target, fields) || fields;
  if (target.kind !== "body") return fields;
  const capsule = state.selectedPath === "geometry/body/capsule";
  return fields.filter(spec => capsule ? spec.path.startsWith("defaultCapsule.") : !spec.path.startsWith("defaultCapsule."));
}

function leaveBodyWorkspace() {
  if (isBodyWorkspacePath()) selectPath("overview", { preserveViewport: true });
}

Object.assign(globalThis, {
  getBodyWorkspaceSection, isBodyWorkspacePath, getBodyWorkspaceSelection, getBodyWorkspaceVisibleColliders,
  getBodyWorkspacePartColliders,
  getBodyPartsPreviewScope, setBodyPartsPreviewScope,
  isBodyWorkspaceSelectionVisualSelected, getBodyWorkspacePickPath, renderBodyWorkspace,
  renderBodyWorkspaceObjectButton, renderBodyWorkspaceObjectTitle, renderBodyPartsOverview, getBodyWorkspaceFields,
  leaveBodyWorkspace, bindBodyInspectorActions
});
