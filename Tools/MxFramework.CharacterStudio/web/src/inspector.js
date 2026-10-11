// CharacterStudio split module. Registers functions on globalThis to preserve the original call graph.

function renderSummary() {
  const pkg = state.package;
  if (!pkg) {
    el.packageSummary.innerHTML = `<div class="empty">没有加载资源包。</div>`;
    return;
  }
  const geometry = pkg.geometry || {};
  const unityEntries = getUnityCatalogEntries();
  el.packageSummary.innerHTML = [
    summaryCell("Package", pkg.manifest?.packageId || "-"),
    summaryCell("Version", pkg.manifest?.version || "-"),
    summaryCell("资源", (pkg.resourceCatalog?.entries || []).length),
    summaryCell("Unity 资源", unityEntries.length || "未加载"),
    summaryCell("受击体", (geometry.colliders || []).length),
    summaryCell("Body Presence", `${getBodyPresenceItems().length} · ${getBodyPresenceStateLabel(state.bodyPresence?.state)}`),
    summaryCell("部位伤害", `${getBodyPartDamageItems().length} · ${getBodyPartDamageStateLabel(state.bodyPartDamage?.state)}`),
    summaryCell("伤害区", getDamageZoneItems(geometry).length),
    summaryCell("接战锚点", getEngagementAnchorItems(geometry).length),
    summaryCell("接触点", (geometry.bodyPartContactPoints || []).length),
    summaryCell("Socket", (geometry.sockets || []).length),
    summaryCell("轨迹", (geometry.traces || []).length)
  ].join("");
}

function presentationImageResource(pkg, key, standing) {
  const usage = standing ? "characterStanding" : "characterPortrait";
  return (pkg?.resourceCatalog?.entries || []).find(entry => entry.resourceKey === key
    && entry.typeId === "texture" && !entry.variant && entry.usage === usage && /^(png|jpe?g)$/i.test(entry.sourceFormat || ""));
}

function presentationImageKey(images, standing, variantId = "") {
  const field = standing ? "standingResourceKey" : "portraitResourceKey";
  return images?.variants?.find(item => item.variantId === variantId)?.[field] || images?.[field] || "";
}

function renderPresentationImageInspector(target) {
  const images = target.value;
  if (images.schemaVersion !== "1.0") {
    el.inspector.innerHTML = `<h3>头像与立绘</h3><p>未知格式，保持只读。</p>`;
    return;
  }
  const renderSlot = (owner, path, standing, variantId = "") => {
    const field = standing ? "standingResourceKey" : "portraitResourceKey";
    const usage = standing ? "characterStanding" : "characterPortrait";
    const key = presentationImageKey(images, standing, variantId);
    const resource = presentationImageResource(state.package, key, standing);
    const url = resource?.relativePath ? encodeURI(`/${state.packageRelative}/${resource.relativePath}`) + `?v=${encodeURIComponent(resource.hash || "")}` : "";
    const label = standing ? "立绘" : "头像";
    const options = (state.package.resourceCatalog?.entries || []).filter(item => presentationImageResource(state.package, item.resourceKey, standing));
    const missing = owner[field] && !options.some(item => item.resourceKey === owner[field]);
    return `<section class="character-image-slot"><strong>${label}${variantId && !owner[field] ? " · 继承基础" : ""}</strong>
      <div class="character-image-frame ${standing ? "standing" : "portrait"}"><span>${label}占位</span>${url ? `<img src="${escapeHtml(url)}" alt="${label}" data-character-image>` : ""}</div>
      <select data-field="${path}${field}" data-type="string" ${state.canWrite ? "" : "disabled"}>
        <option value="">${variantId ? "继承基础图" : "未配置 · 显示占位"}</option>
        ${missing ? `<option value="${escapeHtml(owner[field])}" selected>无效引用：${escapeHtml(owner[field])}</option>` : ""}
        ${options.map(item => `<option value="${escapeHtml(item.resourceKey)}" ${item.resourceKey === owner[field] ? "selected" : ""}>${escapeHtml(item.resourceKey)}${item.preview?.isPlaceholder ? "（占位）" : ""}</option>`).join("")}</select>
      <small>${escapeHtml(resource ? (resource.preview?.isPlaceholder ? "正式占位资源" : "已登记的游戏展示资源") : key ? "资源未找到或用途错误，显示占位" : "未配置，显示占位")}</small></section>`;
  };
  el.inspector.innerHTML = `<h3>头像与立绘</h3><p>独立资源配置；头像按正方形展示，立绘按 2:3 框完整显示。模型缩略图和候选概念图不自动发布。</p>
    <div class="character-image-slots">${renderSlot(images, "", false)}${renderSlot(images, "", true)}</div>
    <h4>外观变体覆盖</h4><p>使用明确的外观 variantId，留空逐项继承基础；未知变体回退基础。此处不绑定战术配置。</p>
    ${(images.variants || []).map((variant, i) => `<article><label>稳定外观 ID <input data-field="variants.${i}.variantId" data-type="string" value="${escapeHtml(variant.variantId)}" ${state.canWrite ? "" : "disabled"}></label>
      <div class="character-image-slots">${renderSlot(variant, `variants.${i}.`, false, variant.variantId)}${renderSlot(variant, `variants.${i}.`, true, variant.variantId)}</div>
      <button data-remove-image-variant="${i}" ${state.canWrite ? "" : "disabled"}>移除覆盖</button></article>`).join("")}
    <button data-add-image-variant ${state.canWrite ? "" : "disabled"}>添加外观覆盖</button>`;
  el.inspector.querySelectorAll("[data-character-image]").forEach(img => img.addEventListener("error", () => { img.hidden = true; img.parentElement.querySelector("span").textContent = "图片加载失败 · 占位"; }));
  el.inspector.querySelectorAll("[data-field]").forEach(input => input.addEventListener("change", () => {
    commitInspectorField(target, input);
    renderInspector();
  }));
  const editVariants = edit => {
    beginInspectorUndoTransaction(target, "variants");
    state.package.applicationConfig.presentationImages = images;
    images.variants ||= [];
    edit(images.variants);
    updateUndoTransactionAfter();
    refreshDirtyFromCleanSnapshot();
    renderShellStatus();
    renderInspector();
  };
  el.inspector.querySelector("[data-add-image-variant]")?.addEventListener("click", () => editVariants(list => {
    let id = 1;
    while (list.some(item => item.variantId === `appearance.${id}`)) id++;
    list.push({ variantId: `appearance.${id}`, portraitResourceKey: "", standingResourceKey: "" });
  }));
  el.inspector.querySelectorAll("[data-remove-image-variant]").forEach(button => button.addEventListener("click", () => editVariants(list => list.splice(Number(button.dataset.removeImageVariant), 1))));
}

function summaryCell(label, value) {
  return `<div><strong>${escapeHtml(String(value))}</strong>${escapeHtml(label)}</div>`;
}

function renderTree() {
  const debugTreeOpen = el.packageTree.querySelector(".debug-tree")?.open || false;
  const scrollTop = el.packageTree.scrollTop;
  const nodes = buildTree(state.package);
  const technicalNodes = buildTechnicalTree(state.package);
  el.packageTree.innerHTML = `
    <div class="tree-section-title">创建流程</div>
    ${nodes.map(renderTreeButton).join("")}
    <details class="debug-tree"${debugTreeOpen ? " open" : ""}>
      <summary>高级 / 调试树</summary>
      ${technicalNodes.map(renderTreeButton).join("")}
    </details>`;
  el.packageTree.querySelectorAll("button[data-path], button[data-context-tab], button[data-inspector-tab]").forEach(button => {
    button.addEventListener("click", () => activateTreeNode(button));
  });
  el.packageTree.scrollTop = scrollTop;
}

function buildTree(pkg) {
  if (!pkg) return [];
  return [
    node("overview", "overview", "流程概览", 0),
    node("config/attributes", "baseAttributes", "基础属性", 0),
    node("config/images", "presentationImages", "头像与立绘", 0),
    node("config/tactical", "tacticalDefaults", "默认战术内容", 0),
    node("geometry/body", "body", "身体与受击", 0),
    node("workflow/materials", "workflow", "原材料与源文件", 0, { contextTab: "materials" }),
    node("workflow/binding", "workflow", "资源绑定", 0, { contextTab: "binding" }),
    node("workflow/animation", "workflow", "动画配置", 0, { contextTab: "animation" }),
    node("workflow/reactions", "workflow", "受击结果（只读）", 0, { contextTab: "reactions" }),
    node("workflow/import", "workflow", "Unity 导入", 0, { inspectorTab: "import" }),
    node("workflow/readiness", "workflow", "入游戏验证", 0, { contextTab: "readiness" })
  ];
}

function buildTechnicalTree(pkg) {
  if (!pkg) return [];
  const g = pkg.geometry || {};
  const animationProfile = getDefaultAnimationProfile();
  const reactionProfile = getDefaultReactionProfile();
  const nodes = [
    node("manifest", "manifest", "Manifest JSON", 0),
    node("config", "config", "角色引用（只读）", 0),
    node("config/gameplay-camera", "gameplayCamera", "游戏镜头", 0),
    node("config/animation", "animationConfig", "动画配置", 0),
    ...(animationProfile ? [
      node(`config/animation/${encodeURIComponent(animationProfile.profileId)}`, "animationProfile", animationProfile.profileId || "animation profile", 1),
      ...grouped((animationProfile.slots || []), "animationSlot", slot => getAnimationSlotPath(animationProfile.profileId, slot.slotId), slot => `${slot.slotId || "slot"}:${slot.resourceKey || "未选择"}`, 2)
    ] : []),
    node("config/reactions", "reactionConfig", "受击配置", 0),
    ...(reactionProfile ? [
      node(`config/reactions/${encodeURIComponent(reactionProfile.profileId)}`, "reactionProfile", reactionProfile.profileId || "reaction profile", 1),
      ...grouped((reactionProfile.slots || []), "reactionSlot", slot => getReactionSlotPath(reactionProfile.profileId, slot.slotId), slot => getReactionSlotTreeLabel(slot), 2)
    ] : []),
    node("geometry/body", "body", g.bodyProfile?.profileId || "身体几何", 0),
    ...grouped((g.bodyParts || []), "part", part => `geometry/body_parts/${part.partId}`, part => part.partId || "part", 1),
    ...grouped((g.colliders || []), "collider", collider => `geometry/colliders/${collider.colliderId}`, collider => collider.colliderId || "collider", 1),
    node("geometry/body_presence", "bodyPresence", `Body Presence · ${getBodyPresenceStateLabel(state.bodyPresence?.state)}`, 0),
    ...grouped(getBodyPresenceItems(), "bodyPresenceVolume", volume => `geometry/body_presence/${encodeURIComponent(volume.volumeId)}`, volume => volume.volumeId || "presence", 1),
    ...grouped(getBodyPresenceAnimatedSources(), "bodyPresenceAnimatedSource", source => `geometry/body_presence/animated/${encodeURIComponent(source.sourceId)}`, source => source.sourceId || "animated source", 1),
    node("geometry/body_part_damage", "bodyPartDamage", `部位伤害 · ${getBodyPartDamageStateLabel(state.bodyPartDamage?.state)}`, 0),
    ...grouped(getBodyPartDamageItems(), "bodyPartDamageDefinition", definition => `geometry/body_part_damage/${encodeURIComponent(definition.partStateId)}`, definition => definition.partStateId || "part-state", 1),
    node("geometry/damage_zones", "damageZones", "伤害区", 0),
    ...grouped(getDamageZoneItems(g), "damageZone", zone => `geometry/damage_zones/${encodeURIComponent(zone.zoneId)}`, zone => zone.zoneId || "zone", 1),
    node("geometry/engagement_anchors", "engagementAnchors", "接战锚点", 0),
    ...grouped(getEngagementAnchorItems(g), "engagementAnchor", anchor => `geometry/engagement_anchors/${encodeURIComponent(anchor.anchorId)}`, anchor => anchor.anchorId || "anchor", 1),
    node("geometry/body_part_contact_points", "contactPoint", "接触音频点", 0),
    ...grouped((g.bodyPartContactPoints || []), "contactPoint", (contactPoint, index) => getContactPointInspectorPath(contactPoint, index), contactPoint => contactPoint.stableId || `contact.${contactPoint.contactPointId || "new"}`, 1),
    ...grouped((g.sockets || []), "socket", socket => `geometry/sockets/${socket.socketId}`, socket => socket.socketId || "socket", 1),
    ...grouped((g.weaponAttachments || []), "weapon", attachment => getWeaponAttachmentPath(attachment), attachment => `${attachment.equipSlot || "slot"}:${attachment.weaponId || "weapon"}`, 1),
    ...grouped((g.traces || []), "trace", trace => `geometry/traces/${trace.traceId}`, trace => trace.traceId || "trace", 1),
    node("validation", "validation", "校验", 0),
    ...grouped((state.validation?.issues || []), "issue", (_, index) => `validation/issues/${index}`, issue => issue.code || issue.message || "issue", 1)
  ];
  return nodes;
}

function renderTreeButton(item) {
  const active = isTreeNodeActive(item) ? " active" : "";
  const attrs = [
    `class="${active}"`,
    `type="button"`,
    item.contextTab ? `data-context-tab="${escapeHtml(item.contextTab)}"` : `data-path="${escapeHtml(item.path)}"`,
    item.inspectorTab ? `data-inspector-tab="${escapeHtml(item.inspectorTab)}"` : "",
    `style="padding-left:${8 + item.depth * 14}px"`
  ].filter(Boolean).join(" ");
  return `<button ${attrs}><span class="kind">${escapeHtml(KIND_LABELS[item.kind] || item.kind)}</span><span class="label">${escapeHtml(item.label)}</span></button>`;
}

function isTreeNodeActive(item) {
  if (item.path === state.selectedPath) return true;
  if (item.path === "geometry/body" && globalThis.isBodyWorkspacePath?.()) return true;
  if (state.selectedPath === "config/attributes" || globalThis.isBodyWorkspacePath?.()) return false;
  if (item.contextTab && !state.contextDockCollapsed && state.activeContextTab === item.contextTab) return true;
  if (item.inspectorTab && state.activeInspectorTab === item.inspectorTab) return true;
  return false;
}

function activateTreeNode(button) {
  const contextTab = button.dataset.contextTab || "";
  const inspectorTab = button.dataset.inspectorTab || "";
  const path = button.dataset.path || "";
  if (contextTab) {
    leaveBaseAttributeWorkspace();
    globalThis.leaveBodyWorkspace?.();
    state.activeContextTab = normalizeContextTab(contextTab);
    state.contextDockCollapsed = false;
    renderContextDockState();
    renderWorkflowBar();
    renderTree();
    return;
  }
  if (inspectorTab) {
    leaveBaseAttributeWorkspace();
    globalThis.leaveBodyWorkspace?.();
    state.activeInspectorTab = ["properties", "diagnostics", "import"].includes(inspectorTab) ? inspectorTab : "properties";
    renderInspectorTabState();
    renderWorkflowBar();
    renderTree();
    return;
  }
  selectPath(path, { preserveViewport: true });
}

function node(path, kind, label, depth, extra = {}) {
  return { path, kind, label, depth, ...extra };
}

function grouped(items, kind, pathFn, labelFn, depth) {
  return items.map((item, index) => node(pathFn(item, index), kind, labelFn(item, index), depth));
}

function getContactPointInspectorPath(contactPoint, index = 0) {
  const key = contactPoint?.stableId || contactPoint?.contactPointId || index;
  return `geometry/body_part_contact_points/${key}`;
}

function renderInspector() {
  document.querySelectorAll("body > .bone-picker").forEach(picker => picker.remove());
  const target = findTarget(state.selectedPath);
  el.selectionBadge.textContent = KIND_LABELS[target.kind] || target.kind || "未选择";
  if (target.kind === "tacticalDefaults") {
    globalThis.renderTacticalDefaultsWorkbench?.();
    return;
  }
  if (target.kind === "gameplayCamera" && !target.value) {
    el.inspector.innerHTML = `${renderObjectTitle(target)}
      <div class="empty">该角色尚未声明 Gameplay Camera，将继续使用当前人形兼容默认。</div>
      <button type="button" data-gameplay-camera-action="enable">启用角色镜头配置</button>`;
    bindGameplayCameraActions();
    return;
  }
  if (!target.value) {
    el.inspector.innerHTML = `<div class="empty">请选择要查看的项目。</div>`;
    return;
  }
  normalizeInspectorTarget(target);
  if (target.kind === "overview") {
    el.inspector.innerHTML = `${globalThis.renderHumanoidHurtboxTemplate?.() || ""}${renderOverviewInspector()}`;
    bindHurtboxFitActions();
    globalThis.bindHumanoidHurtboxTemplate?.();
    return;
  }
  if (target.kind === "bodyPartsOverview") {
    el.inspector.innerHTML = `${renderBodyPartsOverview()}${globalThis.renderHumanoidHurtboxTemplate?.() || ""}`;
    globalThis.bindHumanoidHurtboxTemplate?.();
    return;
  }
  if (target.kind === "manifest") {
    el.inspector.innerHTML = renderManifestInspector(target.value);
    return;
  }
  if (target.kind === "presentationImages") {
    renderPresentationImageInspector(target);
    return;
  }
  if (target.kind === "bodyPresence") {
    el.inspector.innerHTML = renderBodyPresenceRoot(target.value);
    bindBodyPresenceActions();
    return;
  }
  if (target.kind === "config") {
    const source = target.value || {};
    const references = Object.fromEntries(["schemaVersion", "characterStableId", "bodyProfileStableId",
      "attributeProfileStableId", "equipmentSchemaStableId", "defaultCombinedWeaponConfigurationId",
      "combinedWeaponConfigurationBindings", "additionalActionLibraryIds", "resourceKeys"]
      .filter(key => source[key] !== undefined).map(key => [key, source[key]]));
    el.inspector.innerHTML = `${renderObjectTitle(target)}<p>本页只展示角色源中的引用、默认选择和明确覆盖。基础数值在“基础属性”编辑，武器与动作绑定在对应工作区编辑。</p><pre>${escapeHtml(JSON.stringify(references, null, 2))}</pre>`;
    return;
  }
  if (target.kind === "baseAttributes" && target.value?.schemaVersion !== "1.0") {
    el.inspector.innerHTML = `${renderObjectTitle(target)}<p>当前属性格式不受支持，保持只读。</p><pre>${escapeHtml(JSON.stringify(target.value, null, 2))}</pre>`;
    return;
  }
  if (target.kind === "bodyPresenceVolume") {
    el.inspector.innerHTML = renderBodyPresenceVolumeInspector(target.value);
    return;
  }
  if (target.kind === "bodyPresenceAnimatedSource"
    && (!state.canWrite || state.bodyPresence?.source !== "dedicatedApi"
      || state.bodyPresence?.document?.format !== "mx.characterBodyPresence.v2")) {
    el.inspector.innerHTML = `${renderObjectTitle(target)}<p>Animated Hurtbox source 仅在 Body Presence v2 dedicated API 中可编辑；旧版或未知 schema 保持只读。</p><pre>${escapeHtml(JSON.stringify(target.value, null, 2))}</pre>`;
    return;
  }
  if (target.kind === "bodyPartDamage") {
    el.inspector.innerHTML = renderBodyPartDamageRoot(target.value);
    bindBodyPartDamageActions(target);
    return;
  }
  if (target.kind === "bodyPartDamageDefinition"
    && (!state.canWrite
      || state.bodyPartDamage?.source !== "dedicatedApi"
      || normalizeBodyPartDamageDocumentStateValue(state.bodyPartDamage?.state, "") !== "Current")) {
    el.inspector.innerHTML = `${renderObjectTitle(target)}<p>当前部位伤害来源只读；请连接 T1 Authoring service 后编辑。</p><pre>${escapeHtml(JSON.stringify(target.value, null, 2))}</pre>`;
    return;
  }
  if (target.kind === "damageZones") {
    el.inspector.innerHTML = renderDamageZonesRoot(target.value);
    bindDamageZoneActions(target);
    return;
  }
  if (target.kind === "engagementAnchors") {
    el.inspector.innerHTML = renderEngagementAnchorsRoot(target.value);
    bindEngagementAnchorActions(target);
    return;
  }
  if (target.kind === "weapon") ensureWeaponModelWrapperPose(target.value);
  const availableFields = editableFields(target.kind, target.value);
  const fields = globalThis.getBodyWorkspaceFields?.(target, availableFields) || availableFields;
  if (fields.length === 0) {
    el.inspector.innerHTML = `${renderObjectTitle(target)}<pre>${escapeHtml(JSON.stringify(target.value, null, 2))}</pre>`;
    return;
  }
  const gameplayCameraToolbar = target.kind === "gameplayCamera" ? renderGameplayCameraToolbar() : "";
  const colliderManualToolbar = target.kind === "collider" ? renderColliderManualEditor(target.value) : "";
  const bodyPartDamageToolbar = target.kind === "bodyPartDamageDefinition" ? renderBodyPartDamageToolbar(target.value) : "";
  const damageZoneToolbar = target.kind === "damageZone" ? renderDamageZoneToolbar(target.value) : "";
  const engagementAnchorToolbar = target.kind === "engagementAnchor" ? renderEngagementAnchorToolbar(target.value) : "";
  const movementSummary = target.kind === "body" && state.selectedPath === "geometry/body/capsule"
    ? renderMovementCollisionInspectorSummary() : "";
  el.inspector.innerHTML = `${renderObjectTitle(target)}${movementSummary}${gameplayCameraToolbar}${colliderManualToolbar}${bodyPartDamageToolbar}${damageZoneToolbar}${engagementAnchorToolbar}${target.kind === "socket" ? renderAbstractBoneSocketDiagnostics(target.value) : ""}${renderFieldSections(target, fields)}`;
  if (target.kind === "body") globalThis.bindBodyInspectorActions?.();
  if (["part", "collider"].includes(target.kind)) globalThis.bindBodyPartInspectorActions?.();
  if (target.kind === "bodyPartDamageDefinition") bindBodyPartDamageActions(target);
  if (target.kind === "damageZone") bindDamageZoneActions(target);
  if (target.kind === "engagementAnchor") bindEngagementAnchorActions(target);
  if (target.kind === "gameplayCamera") bindGameplayCameraActions();
  if (target.kind === "collider") bindColliderManualEditingActions(target);
  if ((target.kind === "body" && state.selectedPath !== "geometry/body/capsule") || target.kind === "collider") {
    el.inspector.insertAdjacentHTML("beforeend", globalThis.renderHumanoidHurtboxTemplate?.() || "");
    globalThis.bindHumanoidHurtboxTemplate?.();
  }
  const boneInputs = [];
  el.inspector.querySelectorAll("[data-field]").forEach(input => {
    if (input.dataset.picker === "bone") {
      boneInputs.push(input);
      input.addEventListener("focus", () => activateBoneField(input.dataset.field));
      input.addEventListener("click", () => activateBoneField(input.dataset.field));
    }
    input.addEventListener("focus", () => {
      beginInspectorUndoTransaction(getInspectorInputTarget(target, input.dataset.field), input.dataset.field);
    });
    input.addEventListener("input", () => {
      commitInspectorField(getInspectorInputTarget(target, input.dataset.field), input);
    });
    input.addEventListener("change", async () => {
      const inputTarget = getInspectorInputTarget(target, input.dataset.field);
      const value = commitInspectorField(inputTarget, input);
      if (input.dataset.type === "number" && value !== undefined) {
        input.value = formatFieldValue(value, "number");
      }
      commitUndoTransaction();
      await saveCommonResourceTargetIfNeeded(inputTarget);
    });
    input.addEventListener("blur", () => commitUndoTransaction());
  });
  if (!boneInputs.some(input => input.dataset.field === state.activeBoneFieldPath)) {
    state.activeBoneFieldPath = boneInputs[0]?.dataset.field || "";
    state.bonePickerOpen = false;
  }
  el.inspector.querySelectorAll('[data-picker-action="openBonePicker"]').forEach(button => {
    button.addEventListener("click", event => {
      event.stopPropagation();
      activateBoneField(button.dataset.pickerField);
    });
  });
  el.inspector.querySelectorAll('[data-picker-action="openResourcePicker"]').forEach(button => {
    button.addEventListener("click", event => {
      event.stopPropagation();
      openResourcePickerForField(button.dataset.pickerField);
    });
  });
  el.inspector.querySelectorAll("[data-copy-path-field]").forEach(button => {
    button.addEventListener("click", async event => {
      event.stopPropagation();
      const fieldPath = button.dataset.copyPathField || "";
      if (!fieldPath) return;
      const sourceInput = Array.from(el.inspector.querySelectorAll("[data-field]"))
        .find(input => input.dataset.field === fieldPath);
      const value = sourceInput ? String(sourceInput.value || "") : "";
      if (!value) {
        state.message = `${fieldPath} 没有可复制的值。`;
        renderShellStatus();
        return;
      }
      try {
        await navigator.clipboard.writeText(value);
        state.message = `${fieldPath} 已复制。`;
      } catch {
        state.message = `${fieldPath} 复制失败。`;
      }
      renderShellStatus();
    });
  });  wireBonePicker(el.inspector);
  el.inspector.querySelectorAll("[data-inspector-action]").forEach(button => {
    button.addEventListener("click", async () => {
      const action = button.dataset.inspectorAction;
      if (action === "resetModelWrapperPose" && target.kind === "resource" && target.value?.typeId === "model") {
        if (isCommonPreviewModelResource(target.value)) {
          openResourceManagerForPublicResource(target.value, {
            message: "公共资源 wrapper pose 由 Resource Manager 管理。"
          });
          return;
        }
        resetModelWrapperPose(target.value);
        state.dirty = true;
        state.message = "模型 wrapper pose 已重置。";
        render();
      } else if (action === "chooseWeaponModel" && target.kind === "weapon") {
        openResourcePickerForField("weaponRef");
      } else if (action === "viewFocusedWeapon" && target.kind === "weapon") {
        enterWeaponViewport(getWeaponAttachmentIdentity(target.value));
      } else if (action === "applyRecommendedModelScale" && target.kind === "resource" && target.value?.typeId === "model") {
        await applyRecommendedModelScale(target.value);
      } else if (action === "applyModelScalePreset" && target.kind === "resource" && target.value?.typeId === "model") {
        await applyModelWrapperScalePreset(target.value, button.dataset.scalePreset);
      }
    });
  });
  el.inspector.querySelectorAll("button[data-jump]").forEach(button => {
    button.addEventListener("click", () => selectPath(button.dataset.jump));
  });
}

function renderBodyPresenceRoot(snapshot = state.bodyPresence || {}) {
  const documentState = normalizeBodyPresenceDocumentStateValue(snapshot.state, "Unavailable");
  const stateLabel = getBodyPresenceStateLabel(documentState);
  if (documentState === "Unavailable") {
    return `<section class="inspector-overview">
      <h3>Body Presence 状态不可用</h3>
      <p>请通过当前 Authoring 服务重新打开 CharacterStudio；静态或旧服务回退不会获得写权限。</p>
    </section>`;
  }
  if (documentState === "UnsupportedSchema" || documentState === "Invalid") {
    const reason = documentState === "UnsupportedSchema" ? "Future / UnsupportedSchema" : "Invalid";
    return `<section class="inspector-overview">
      <h3>Body Presence · ${escapeHtml(stateLabel)} · 只读</h3>
      <p>${reason} 文档由 Authoring Core 判定为不可写。CharacterStudio 不会创建默认值，也不会通过整包保存覆盖原文。</p>
      <pre>${escapeHtml(String(snapshot.rawPayload || ""))}</pre>
    </section>`;
  }
  if (documentState === "Missing") {
    return `<section class="inspector-overview">
      <h3>Body Presence · Missing</h3>
      <p>该可选文档不存在；CharacterStudio 不会隐式创建默认 Presence，旧 geometry binding 保持不变。</p>
    </section>`;
  }

  const volumes = getBodyPresenceItems(snapshot);
  const animatedSources = getBodyPresenceAnimatedSources(snapshot);
  const saveDisabled = !state.canWrite || snapshot.source !== "dedicatedApi"
    || !snapshot.writeEligible || state.bodyPresenceSaving;
  return `<section class="inspector-overview">
    <h3>Body Presence · Current</h3>
    <p>${volumes.length} 个 ModelRoot volume · ${animatedSources.length} 个 Animated Hurtbox source · document revision ${Number(snapshot.document?.revision || 0)}。v2 source 只保存稳定 Collider 引用与策略，不复制形状。</p>
    <div class="field-actions">
      <button type="button" data-body-presence-action="add-animated"${saveDisabled || snapshot.document?.format !== "mx.characterBodyPresence.v2" || animatedSources.length >= 4 ? " disabled" : ""}>添加 Animated Hurtbox source</button>
      <button type="button" data-body-presence-action="save"${saveDisabled ? " disabled" : ""}>${state.bodyPresenceSaving ? "保存中..." : "通过独立 API 保存并重开"}</button>
    </div>
    ${snapshot.contentHash ? `<p><code>${escapeHtml(snapshot.contentHash)}</code></p>` : ""}
    ${volumes.map(renderBodyPresenceVolumeCard).join("")}
    ${animatedSources.map(renderBodyPresenceAnimatedSourceCard).join("")}
  </section>`;
}

function renderBodyPresenceAnimatedSourceCard(source = {}) {
  return `<article class="behavior-summary-card">
    <header><div><strong>${escapeHtml(source.sourceId || "animated source")}</strong><code>${escapeHtml(source.colliderId || "missing collider")}</code></div></header>
    <p>skin ${Number(source.skinMillimeters || 0)}mm · pressure ${Number(source.pressureScaleMilli || 0)} · ${source.enabled ? "Enabled" : "Disabled"}</p>
    <div class="field-actions"><button type="button" data-body-presence-source="${escapeHtml(source.sourceId || "")}">编辑引用</button><button type="button" data-body-presence-remove-source="${escapeHtml(source.sourceId || "")}">删除</button></div>
  </article>`;
}

function renderBodyPresenceVolumeCard(volume = {}) {
  return `<article class="behavior-summary-card">
    <header><div><strong>${escapeHtml(volume.volumeId || "presence")}</strong><code>${escapeHtml(volume.shape || "Unknown")} · ${escapeHtml(volume.referenceFrame || "Unknown")}</code></div></header>
    <p>${escapeHtml(describeBodyPresenceDimensions(volume))} · center ${escapeHtml(formatBodyPresenceVector(volume.localPose?.position))} · skin ${Number(volume.skinMillimeters || 0)}mm · pressure ${Number(volume.pressureScaleMilli || 0)} · ${volume.enabled ? "Enabled" : "Disabled"}</p>
    <button type="button" data-body-presence-volume="${escapeHtml(volume.volumeId || "")}">查看 volume</button>
  </article>`;
}

function renderBodyPresenceVolumeInspector(volume = {}) {
  return `<section class="inspector-overview">
    <h3>${escapeHtml(volume.volumeId || "Presence Volume")}</h3>
    <p>只读 M0 展示；编辑器不会把 Body Presence 混入整包草稿。</p>
    <dl>
      <dt>Reference frame</dt><dd><code>${escapeHtml(volume.referenceFrame || "Unknown")}</code></dd>
      <dt>Shape</dt><dd><code>${escapeHtml(volume.shape || "Unknown")}</code></dd>
      <dt>Center</dt><dd><code>${escapeHtml(formatBodyPresenceVector(volume.localPose?.position))}</code></dd>
      <dt>Dimensions</dt><dd><code>${escapeHtml(describeBodyPresenceDimensions(volume))}</code></dd>
      <dt>Skin / pressure</dt><dd><code>${Number(volume.skinMillimeters || 0)}mm / ${Number(volume.pressureScaleMilli || 0)}</code></dd>
      <dt>Enabled</dt><dd><code>${volume.enabled ? "true" : "false"}</code></dd>
    </dl>
    <details><summary>只读 JSON 审计</summary><pre>${escapeHtml(JSON.stringify(volume, null, 2))}</pre></details>
  </section>`;
}

function bindBodyPresenceActions() {
  el.inspector.querySelector('[data-body-presence-action="save"]')
    ?.addEventListener("click", () => saveBodyPresenceDocument());
  el.inspector.querySelector('[data-body-presence-action="add-animated"]')
    ?.addEventListener("click", () => {
      const document = state.bodyPresence?.document;
      if (document?.format !== "mx.characterBodyPresence.v2") return;
      const sources = document.animatedHurtboxSources ||= [];
      if (sources.length >= 4) return;
      beginUndoTransaction("添加 Animated Hurtbox source", "body-presence:add-animated");
      let suffix = sources.length + 1;
      let sourceId = `presence.animated.source${suffix}`;
      while (sources.some(source => source?.sourceId === sourceId)) sourceId = `presence.animated.source${++suffix}`;
      sources.push({ sourceId, colliderId: firstValue(animatedHurtboxColliderOptions()) || "", skinMillimeters: 50, pressureScaleMilli: 1000, enabled: true });
      updateUndoTransactionAfter(); commitUndoTransaction();
      state.selectedPath = `geometry/body_presence/animated/${encodeURIComponent(sourceId)}`;
      renderTree(); renderInspector();
    });
  el.inspector.querySelectorAll("[data-body-presence-volume]").forEach(button => {
    button.addEventListener("click", () => {
      const volumeId = button.dataset.bodyPresenceVolume || "";
      if (volumeId) selectPath(`geometry/body_presence/${encodeURIComponent(volumeId)}`);
    });
  });
  el.inspector.querySelectorAll("[data-body-presence-source]").forEach(button => {
    button.addEventListener("click", () => selectPath(`geometry/body_presence/animated/${encodeURIComponent(button.dataset.bodyPresenceSource || "")}`));
  });
  el.inspector.querySelectorAll("[data-body-presence-remove-source]").forEach(button => {
    button.addEventListener("click", () => {
      const sources = getBodyPresenceAnimatedSources();
      const index = sources.findIndex(source => source?.sourceId === button.dataset.bodyPresenceRemoveSource);
      if (index < 0) return;
      beginUndoTransaction("删除 Animated Hurtbox source", "body-presence:remove-animated");
      sources.splice(index, 1); updateUndoTransactionAfter(); commitUndoTransaction();
      renderTree(); renderInspector();
    });
  });
}

function getBodyPresenceItems(snapshot = state.bodyPresence) {
  return Array.isArray(snapshot?.document?.volumes) ? snapshot.document.volumes : [];
}

function getBodyPresenceAnimatedSources(snapshot = state.bodyPresence) {
  return Array.isArray(snapshot?.document?.animatedHurtboxSources)
    ? snapshot.document.animatedHurtboxSources : [];
}

function animatedHurtboxColliderOptions() {
  return (state.package?.geometry?.colliders || [])
    .filter(collider => collider?.physicsLayer === "CharacterHurtbox"
      && ["Sphere", "Capsule", "Box"].includes(collider?.shape) && collider?.partId)
    .map(collider => ({ value: collider.colliderId, label: `${collider.colliderId} · ${collider.partId}` }));
}

function getBodyPresenceStateLabel(value) {
  const normalized = normalizeBodyPresenceDocumentStateValue(value, "Unavailable");
  if (normalized === "UnsupportedSchema") return "Future / UnsupportedSchema";
  return normalized;
}

function getBodyPartDamageItems(snapshot = state.bodyPartDamage) {
  return Array.isArray(snapshot?.document?.partStates) ? snapshot.document.partStates : [];
}

function getBodyPartDamageStateLabel(value) {
  const normalized = normalizeBodyPartDamageDocumentStateValue(value, "Unavailable");
  return normalized === "Future" ? "Future / 只读" : normalized;
}

function renderBodyPartDamageRoot(snapshot = state.bodyPartDamage || {}) {
  const documentState = normalizeBodyPartDamageDocumentStateValue(snapshot.state, "Unavailable");
  if (documentState === "Unavailable") {
    return `<section class="inspector-overview body-part-damage-editor">
      <h3>部位伤害状态不可用</h3>
      <p>请使用包含 T1 body-part-damage service 的 Authoring 服务重新打开。静态回退没有写权限。</p>
    </section>`;
  }
  if (documentState === "Future" || documentState === "Invalid") {
    const reason = documentState === "Future" ? "future format / schema" : "invalid payload";
    return `<section class="inspector-overview body-part-damage-editor is-readonly">
      <h3>部位伤害 · ${escapeHtml(getBodyPartDamageStateLabel(documentState))}</h3>
      <p>${escapeHtml(reason)} 由 T1 loader 判定为只读。这里保留原文，不提供创建、编辑或保存入口，也不会被顶层 Package 保存覆盖。</p>
      <pre>${escapeHtml(String(snapshot.rawPayload || ""))}</pre>
    </section>`;
  }
  if (documentState === "Missing") {
    const disabled = !state.canWrite || snapshot.source !== "dedicatedApi";
    return `<section class="inspector-overview body-part-damage-editor">
      <h3>部位伤害 · Missing</h3>
      <p>可选文件尚不存在。只有点击下方按钮才会创建内存草稿；浏览、刷新和顶层加载不会隐式创建文件。</p>
      <div class="field-actions"><button type="button" data-body-part-damage-action="create"${disabled ? " disabled" : ""}>创建部位伤害配置</button></div>
    </section>`;
  }

  const definitions = getBodyPartDamageItems(snapshot);
  const saveDisabled = !state.canWrite || snapshot.source !== "dedicatedApi"
    || state.bodyPartDamageSaving || !state.bodyPartDamageDirty;
  return `<section class="inspector-overview body-part-damage-editor">
    <h3>部位伤害 · Current${state.bodyPartDamageDirty ? " · 未保存" : ""}</h3>
    <p>${definitions.length}/8 个 part state · revision ${Number(snapshot.document?.revision || 0)}。上下移只调整当前草稿的阅读顺序；T1 service 保存后会按 <code>partStateId</code> 规范化排序。</p>
    <p class="field-help">Current 文档至少需要 1 个 part state；删除最后一组仍可保留草稿，但保存时会由 T1 typed validation 阻断并在此显示问题。</p>
    <div class="field-actions body-part-damage-actions">
      <button type="button" data-body-part-damage-action="add"${definitions.length >= 8 ? " disabled" : ""}>新增部位状态</button>
      <button type="button" data-body-part-damage-action="save"${saveDisabled ? " disabled" : ""}>${state.bodyPartDamageSaving ? "保存中..." : "通过 T1 service 保存并重开"}</button>
    </div>
    ${snapshot.contentHash ? `<p class="body-part-damage-hash"><code>${escapeHtml(snapshot.contentHash)}</code></p>` : ""}
    ${renderBodyPartDamageValidation()}
    <div class="body-part-damage-list">${definitions.map(renderBodyPartDamageCard).join("")}</div>
  </section>`;
}

function renderBodyPartDamageCard(definition = {}, index = 0) {
  const members = Array.isArray(definition.memberPartIds) ? definition.memberPartIds : [];
  return `<article class="behavior-summary-card body-part-damage-card">
    <header><div><strong>${escapeHtml(definition.partStateId || `part-state-${index + 1}`)}</strong><code>${escapeHtml(definition.damageZoneId || "无 DamageZone")}</code></div></header>
    <p>${members.length ? escapeHtml(members.join(" · ")) : "尚未映射 BodyPart"}</p>
    <p>Wound ${Number(definition.woundCapacity || 0)} / ×${(Number(definition.openDamageScaleMilli || 0) / 1000).toFixed(3)} · Integrity ${Number(definition.maxIntegrity || 0)} / ×${(Number(definition.integrityDamageScaleMilli || 0) / 1000).toFixed(3)} · ${definition.severable ? "Severable" : "Non-severable"}</p>
    <button type="button" data-body-part-damage-open="${escapeHtml(definition.partStateId || "")}">编辑</button>
  </article>`;
}

function renderBodyPartDamageValidation() {
  const issues = Array.isArray(state.bodyPartDamageValidation?.issues)
    ? state.bodyPartDamageValidation.issues
    : [];
  if (issues.length === 0) return "";
  return `<section class="body-part-damage-validation" aria-live="polite">
    <h4>T1 validation</h4>
    ${issues.map(issue => `<button type="button" class="diagnostic-item" data-body-part-damage-diagnostic="${escapeHtml(String(issue?.sourceObjectPath || issue?.sourcePath || ""))}"><strong>${escapeHtml(String(issue?.code || issue?.gate || "Validation"))}</strong><span>${escapeHtml(String(issue?.message || ""))}</span></button>`).join("")}
  </section>`;
}

function renderBodyPartDamageToolbar(definition = {}) {
  const readOnly = !state.canWrite || state.bodyPartDamage?.source !== "dedicatedApi";
  return `<div class="field-actions body-part-damage-actions">
    <button type="button" data-body-part-damage-action="add"${readOnly ? " disabled" : ""}>新增</button>
    <button type="button" data-body-part-damage-action="move-up" data-part-state-id="${escapeHtml(definition.partStateId || "")}"${readOnly ? " disabled" : ""}>上移草稿</button>
    <button type="button" data-body-part-damage-action="move-down" data-part-state-id="${escapeHtml(definition.partStateId || "")}"${readOnly ? " disabled" : ""}>下移草稿</button>
    <button type="button" data-body-part-damage-action="delete" data-part-state-id="${escapeHtml(definition.partStateId || "")}"${readOnly ? " disabled" : ""}>删除</button>
    <button type="button" data-body-part-damage-action="save"${readOnly || state.bodyPartDamageSaving || !state.bodyPartDamageDirty ? " disabled" : ""}>保存并规范化重开</button>
  </div>
  <p class="field-help body-part-damage-order-note">保存后的正式顺序由 T1 service 按 partStateId 决定；上下移不建立第二个 order 字段。</p>
  ${renderBodyPartDamageValidation()}`;
}

function bindBodyPartDamageActions(target) {
  el.inspector.querySelectorAll("[data-body-part-damage-open]").forEach(button => {
    button.addEventListener("click", () => {
      const partStateId = button.dataset.bodyPartDamageOpen || "";
      if (partStateId) selectPath(`geometry/body_part_damage/${encodeURIComponent(partStateId)}`);
    });
  });
  el.inspector.querySelectorAll("[data-body-part-damage-diagnostic]").forEach(button => {
    button.addEventListener("click", () => {
      const sourcePath = button.dataset.bodyPartDamageDiagnostic || "";
      const idMatch = sourcePath.match(/partStates\/(?:\d+\/)?([^/]+)/);
      if (idMatch?.[1]) selectPath(`geometry/body_part_damage/${encodeURIComponent(idMatch[1])}`);
    });
  });
  el.inspector.querySelectorAll("[data-body-part-damage-action]").forEach(button => {
    button.addEventListener("click", async () => {
      const snapshot = state.bodyPartDamage;
      if (!snapshot || snapshot.source !== "dedicatedApi") return;
      const action = button.dataset.bodyPartDamageAction || "";
      if (action === "save") {
        commitUndoTransaction();
        await saveBodyPartDamageDocument();
        return;
      }
      if (!["Missing", "Current"].includes(normalizeBodyPartDamageDocumentStateValue(snapshot.state, ""))) return;
      beginUndoTransaction(`部位伤害 ${action}`, `body-part-damage:${action}`);
      if (action === "create") {
        snapshot.state = "Current";
        snapshot.writeEligible = true;
        snapshot.rawPayload = "";
        snapshot.document = {
          format: "mx.characterBodyPartDamage.v1",
          schemaVersion: "1.0",
          revision: 1,
          partStates: []
        };
        state.selectedPath = "geometry/body_part_damage";
      } else if (action === "add") {
        const definitions = ensureBodyPartDamageItems(snapshot);
        if (definitions.length >= 8) {
          cancelUndoTransaction();
          state.message = "T1 合同每个角色最多 8 个部位状态。";
          renderShellStatus();
          return;
        }
        const existing = new Set(definitions.map(item => String(item?.partStateId || "")));
        let index = definitions.length + 1;
        let partStateId = `partstate.new_${index}`;
        while (existing.has(partStateId)) partStateId = `partstate.new_${++index}`;
        definitions.push({
          partStateId,
          memberPartIds: [],
          damageZoneId: "",
          woundCapacity: 0,
          openDamageScaleMilli: 1000,
          focusConsumeTag: "",
          maxIntegrity: 0,
          integrityDamageScaleMilli: 1000,
          breakReactionId: "",
          authoredPriorityDeltaOpen: 0,
          authoredPriorityDeltaBroken: 0,
          severable: false,
          severRequiredTag: "",
          presentationEventId: ""
        });
        state.selectedPath = `geometry/body_part_damage/${encodeURIComponent(partStateId)}`;
      } else {
        const partStateId = String(button.dataset.partStateId || target?.value?.partStateId || "");
        const definitions = ensureBodyPartDamageItems(snapshot);
        const currentIndex = definitions.findIndex(item => String(item?.partStateId || "") === partStateId);
        if (action === "delete" && currentIndex >= 0) {
          definitions.splice(currentIndex, 1);
          state.selectedPath = "geometry/body_part_damage";
        } else if (action === "move-up" && currentIndex > 0) {
          [definitions[currentIndex - 1], definitions[currentIndex]] = [definitions[currentIndex], definitions[currentIndex - 1]];
        } else if (action === "move-down" && currentIndex >= 0 && currentIndex < definitions.length - 1) {
          [definitions[currentIndex], definitions[currentIndex + 1]] = [definitions[currentIndex + 1], definitions[currentIndex]];
        }
      }
      state.bodyPartDamageValidation = null;
      updateUndoTransactionAfter();
      commitUndoTransaction();
      refreshBodyPartDamageDirtyFromCleanSnapshot();
      render();
    });
  });
}

function ensureBodyPartDamageItems(snapshot = state.bodyPartDamage) {
  snapshot.document ||= {
    format: "mx.characterBodyPartDamage.v1",
    schemaVersion: "1.0",
    revision: 1,
    partStates: []
  };
  snapshot.document.partStates = Array.isArray(snapshot.document.partStates)
    ? snapshot.document.partStates
    : [];
  return snapshot.document.partStates;
}

function formatBodyPresenceVector(value = {}) {
  const number = component => Number.isFinite(Number(value?.[component]))
    ? Number(value[component]).toFixed(3)
    : "0.000";
  return `(${number("x")}, ${number("y")}, ${number("z")})`;
}

function describeBodyPresenceDimensions(volume = {}) {
  if (volume.shape === "Capsule") return `radius ${Number(volume.radius || 0)}m · height ${Number(volume.height || 0)}m`;
  if (volume.shape === "Sphere") return `radius ${Number(volume.radius || 0)}m`;
  if (volume.shape === "Box") return `size ${formatBodyPresenceVector(volume.size)}`;
  return "dimensions unavailable";
}

function renderGameplayCameraToolbar() {
  const mode = state.gameplayCameraPreviewMode === "locked" ? "locked" : "normal";
  return `<div class="field-actions gameplay-camera-actions">
    <button type="button" data-gameplay-camera-action="preview-normal" class="${mode === "normal" ? "active" : ""}">普通跟随预览</button>
    <button type="button" data-gameplay-camera-action="preview-locked" class="${mode === "locked" ? "active" : ""}">目标锁定预览</button>
    <button type="button" data-gameplay-camera-action="measure">从当前预览测量</button>
    <button type="button" data-gameplay-camera-action="disable">停用显式配置</button>
  </div>`;
}

function renderDamageZonesRoot(geometry = {}) {
  const stateValue = normalizeDamageZonesDocumentState(geometry.damageZonesDocumentState);
  if (stateValue === "UnsupportedSchema" || stateValue === "Invalid") {
    const label = stateValue === "UnsupportedSchema" ? "不支持的 schema" : "无效 JSON";
    return `<section class="inspector-overview">
      <h3>DamageZone 文档只读</h3>
      <p>${label}。CharacterStudio 不会覆盖未知内容；请迁移或修正文档后再编辑。</p>
      <pre>${escapeHtml(String(geometry.damageZonesRawPayload || ""))}</pre>
    </section>`;
  }
  if (stateValue !== "Current") {
    return `<section class="inspector-overview">
      <h3>尚未启用 DamageZone</h3>
      <p>当前包继续使用 geometry binding v1。显式启用后才会创建 geometry/damage_zones.json 并切换到 v2。</p>
      <button type="button" data-damage-zone-action="enable">启用 DamageZone</button>
    </section>`;
  }
  return `<section class="inspector-overview">
    <h3>DamageZone</h3>
    <p>${getDamageZoneItems(geometry).length} 个区域。每个 Hit Zone 只能属于一个 DamageZone。</p>
    <button type="button" data-damage-zone-action="add">新增 DamageZone</button>
  </section>`;
}

function renderDamageZoneToolbar(zone = {}) {
  return `<div class="field-actions">
    <button type="button" data-damage-zone-action="add">新增区域</button>
    <button type="button" data-damage-zone-action="delete" data-zone-id="${escapeHtml(zone.zoneId || "")}">删除当前区域</button>
  </div>`;
}

function bindDamageZoneActions(target) {
  el.inspector.querySelectorAll("[data-damage-zone-action]").forEach(button => {
    button.addEventListener("click", () => {
      const geometry = state.package?.geometry;
      if (!geometry || !["Missing", "Current"].includes(normalizeDamageZonesDocumentState(geometry.damageZonesDocumentState))) return;
      const action = button.dataset.damageZoneAction;
      beginUndoTransaction(`DamageZone ${action}`, `damage-zone:${action}`);
      if (action === "enable") {
        geometry.damageZonesDocumentState = "Current";
        geometry.damageZonesRawPayload = null;
        geometry.damageZones = { format: "mx.characterDamageZones.v1", schemaVersion: "1.0", zones: [] };
        state.selectedPath = "geometry/damage_zones";
      } else if (action === "add") {
        const zones = ensureDamageZoneItems(geometry);
        const existing = new Set(zones.map(zone => String(zone?.zoneId || "")));
        let index = zones.length + 1;
        let zoneId = `zone.${index}`;
        while (existing.has(zoneId)) zoneId = `zone.${++index}`;
        zones.push({ zoneId, displayName: zoneId, hitZoneIds: [], hittable: true, aimable: true, approachable: true, authoredPriority: 0 });
        state.selectedPath = `geometry/damage_zones/${encodeURIComponent(zoneId)}`;
      } else if (action === "delete") {
        const zoneId = String(button.dataset.zoneId || target?.value?.zoneId || "");
        geometry.damageZones.zones = getDamageZoneItems(geometry).filter(zone => String(zone?.zoneId || "") !== zoneId);
        state.selectedPath = "geometry/damage_zones";
      }
      updateUndoTransactionAfter();
      commitUndoTransaction();
      refreshDirtyFromCleanSnapshot();
      render();
    });
  });
}

function normalizeDamageZonesDocumentState(value) {
  if (typeof value === "string") return value;
  return ["Missing", "Current", "UnsupportedSchema", "Invalid"][Number(value) || 0];
}

function getDamageZoneItems(geometry = {}) {
  if (Array.isArray(geometry.damageZones)) return geometry.damageZones;
  return Array.isArray(geometry.damageZones?.zones) ? geometry.damageZones.zones : [];
}

function ensureDamageZoneItems(geometry = {}) {
  if (!geometry.damageZones || Array.isArray(geometry.damageZones)) {
    geometry.damageZones = {
      format: "mx.characterDamageZones.v1",
      schemaVersion: "1.0",
      zones: Array.isArray(geometry.damageZones) ? geometry.damageZones : []
    };
  }
  geometry.damageZones.zones = Array.isArray(geometry.damageZones.zones) ? geometry.damageZones.zones : [];
  return geometry.damageZones.zones;
}

function renderEngagementAnchorsRoot(geometry = {}) {
  const stateValue = normalizeEngagementAnchorsDocumentState(geometry.engagementAnchorsDocumentState);
  if (stateValue === "UnsupportedSchema" || stateValue === "Invalid") {
    const label = stateValue === "UnsupportedSchema" ? "不支持的 schema" : "无效 JSON";
    return `<section class="inspector-overview">
      <h3>接战锚点文档只读</h3>
      <p>${label}。CharacterStudio 不会覆盖未知内容；请迁移或修正文档后再编辑。</p>
      <pre>${escapeHtml(String(geometry.engagementAnchorsRawPayload || ""))}</pre>
    </section>`;
  }
  if (stateValue !== "Current") {
    return `<section class="inspector-overview">
      <h3>尚未启用接战锚点</h3>
      <p>当前角色继续使用根节点兼容接近。显式启用后才会创建 geometry/engagement_anchors.json。</p>
      <button type="button" data-engagement-anchor-action="enable">启用接战锚点</button>
    </section>`;
  }
  const revision = Number(geometry.engagementAnchors?.revision || 0);
  return `<section class="inspector-overview">
    <h3>接战锚点</h3>
    <p>${getEngagementAnchorItems(geometry).length} 个锚点 · revision ${revision}。坐标和朝向均相对 ModelRoot；phase.default 是当前唯一运行时阶段。</p>
    <button type="button" data-engagement-anchor-action="add">新增接战锚点</button>
  </section>`;
}

function renderEngagementAnchorToolbar(anchor = {}) {
  return `<div class="field-actions">
    <button type="button" data-engagement-anchor-action="add">新增锚点</button>
    <button type="button" data-engagement-anchor-action="duplicate" data-anchor-id="${escapeHtml(anchor.anchorId || "")}">复制当前锚点</button>
    <button type="button" data-engagement-anchor-action="move-up" data-anchor-id="${escapeHtml(anchor.anchorId || "")}">上移</button>
    <button type="button" data-engagement-anchor-action="move-down" data-anchor-id="${escapeHtml(anchor.anchorId || "")}">下移</button>
    <button type="button" data-engagement-anchor-action="delete" data-anchor-id="${escapeHtml(anchor.anchorId || "")}">删除当前锚点</button>
  </div>`;
}

function bindEngagementAnchorActions(target) {
  el.inspector.querySelectorAll("[data-engagement-anchor-action]").forEach(button => {
    button.addEventListener("click", () => {
      const geometry = state.package?.geometry;
      if (!geometry || !["Missing", "Current"].includes(normalizeEngagementAnchorsDocumentState(geometry.engagementAnchorsDocumentState))) return;
      const action = button.dataset.engagementAnchorAction;
      beginUndoTransaction(`接战锚点 ${action}`, `engagement-anchor:${action}`);
      if (action === "enable") {
        geometry.engagementAnchorsDocumentState = "Current";
        geometry.engagementAnchorsRawPayload = null;
        geometry.engagementAnchors = { format: "mx.characterEngagementAnchors.v1", schemaVersion: "1.0", revision: 1, anchors: [] };
        state.selectedPath = "geometry/engagement_anchors";
      } else if (action === "add") {
        const anchors = ensureEngagementAnchorItems(geometry);
        const existing = new Set(anchors.map(anchor => String(anchor?.anchorId || "")));
        let index = anchors.length + 1;
        let anchorId = `anchor.${index}`;
        while (existing.has(anchorId)) anchorId = `anchor.${++index}`;
        const zoneId = getDamageZoneItems(geometry).find(zone => zone?.approachable)?.zoneId || "";
        anchors.push({
          anchorId,
          zoneId,
          referenceFrame: "ModelRoot",
          localPositionMillimeters: { x: 0, y: 0, z: 1000 },
          yawMilliDegrees: 180000,
          capacity: 1,
          authoredPriority: 0,
          enabled: true,
          phaseId: "phase.default"
        });
        state.selectedPath = `geometry/engagement_anchors/${encodeURIComponent(anchorId)}`;
      } else if (action === "duplicate") {
        const anchors = ensureEngagementAnchorItems(geometry);
        const anchorId = String(button.dataset.anchorId || target?.value?.anchorId || "");
        const sourceIndex = anchors.findIndex(anchor => String(anchor?.anchorId || "") === anchorId);
        if (sourceIndex < 0) {
          cancelUndoTransaction();
          return;
        }
        const copy = JSON.parse(JSON.stringify(anchors[sourceIndex]));
        const existing = new Set(anchors.map(anchor => String(anchor?.anchorId || "")));
        const baseId = `${anchorId || "anchor"}.copy`;
        let copyId = baseId;
        let suffix = 2;
        while (existing.has(copyId)) copyId = `${baseId}.${suffix++}`;
        copy.anchorId = copyId;
        anchors.splice(sourceIndex + 1, 0, copy);
        state.selectedPath = `geometry/engagement_anchors/${encodeURIComponent(copyId)}`;
      } else if (action === "move-up" || action === "move-down") {
        const anchors = ensureEngagementAnchorItems(geometry);
        const anchorId = String(button.dataset.anchorId || target?.value?.anchorId || "");
        const sourceIndex = anchors.findIndex(anchor => String(anchor?.anchorId || "") === anchorId);
        const destinationIndex = sourceIndex + (action === "move-up" ? -1 : 1);
        if (sourceIndex < 0 || destinationIndex < 0 || destinationIndex >= anchors.length) {
          cancelUndoTransaction();
          return;
        }
        [anchors[sourceIndex], anchors[destinationIndex]] = [anchors[destinationIndex], anchors[sourceIndex]];
        state.selectedPath = `geometry/engagement_anchors/${encodeURIComponent(anchorId)}`;
      } else if (action === "delete") {
        const anchorId = String(button.dataset.anchorId || target?.value?.anchorId || "");
        geometry.engagementAnchors.anchors = getEngagementAnchorItems(geometry)
          .filter(anchor => String(anchor?.anchorId || "") !== anchorId);
        state.selectedPath = "geometry/engagement_anchors";
      }
      updateUndoTransactionAfter();
      commitUndoTransaction();
      refreshDirtyFromCleanSnapshot();
      render();
    });
  });
}

function normalizeEngagementAnchorsDocumentState(value) {
  if (typeof value === "string") return value;
  return ["Missing", "Current", "UnsupportedSchema", "Invalid"][Number(value) || 0];
}

function getEngagementAnchorItems(geometry = {}) {
  if (Array.isArray(geometry.engagementAnchors)) return geometry.engagementAnchors;
  return Array.isArray(geometry.engagementAnchors?.anchors) ? geometry.engagementAnchors.anchors : [];
}

function ensureEngagementAnchorItems(geometry = {}) {
  if (!geometry.engagementAnchors || Array.isArray(geometry.engagementAnchors)) {
    geometry.engagementAnchors = {
      format: "mx.characterEngagementAnchors.v1",
      schemaVersion: "1.0",
      revision: 1,
      anchors: Array.isArray(geometry.engagementAnchors) ? geometry.engagementAnchors : []
    };
  }
  geometry.engagementAnchors.anchors = Array.isArray(geometry.engagementAnchors.anchors)
    ? geometry.engagementAnchors.anchors : [];
  return geometry.engagementAnchors.anchors;
}

function renderColliderManualEditor(collider = {}) {
  const disabled = state.canWrite ? "" : "disabled";
  return `<section class="collider-manual-editor" aria-label="手动编辑受击体">
    <div class="collider-manual-head">
      <div><strong>手动编辑受击体</strong><span data-collider-live-dimensions>${escapeHtml(describeColliderDimensions(collider))}</span></div>
      <span class="collider-manual-shortcuts"><kbd>W</kbd>移动 <kbd>E</kbd>旋转 <kbd>R</kbd>尺寸 <kbd>F</kbd>聚焦</span>
    </div>
    <div class="field-actions collider-manual-actions">
      <button type="button" data-collider-edit-action="focus">聚焦选中</button>
    </div>
    <div class="field-actions collider-manual-actions">
      <button type="button" data-collider-edit-action="expand" ${disabled}>每侧外扩 1cm</button>
      <button type="button" data-collider-edit-action="shrink" ${disabled}>每侧收缩 1cm</button>
      <button type="button" data-collider-edit-action="reset-rotation" ${disabled}>旋转归零</button>
      <button type="button" data-collider-edit-action="duplicate" ${disabled}>复制</button>
      <button type="button" class="danger subtle" data-collider-edit-action="delete" ${disabled}>删除</button>
    </div>
    <p class="collider-manual-hint">显示范围在中间预览工具栏选择。操作手柄使用局部坐标；按 R 调整尺寸，按住 Alt 可围绕中心对称调整高度。</p>
  </section>`;
}

function describeColliderDimensions(collider = {}) {
  const shape = String(collider.shape || "Capsule");
  if (shape === "Box") {
    const size = collider.size || {};
    return `盒体 · ${formatMeters(size.x)} × ${formatMeters(size.y)} × ${formatMeters(size.z)}`;
  }
  if (shape === "Sphere") return `球体 · 半径 ${formatMeters(collider.radius)}`;
  return `胶囊 · 半径 ${formatMeters(collider.radius)} / 总高 ${formatMeters(collider.height)}`;
}

function formatMeters(value) {
  const number = Number(value);
  return `${Number.isFinite(number) ? Number(number.toFixed(3)) : 0}m`;
}

function bindColliderManualEditingActions(target) {
  if (target?.kind !== "collider" || !target.value) return;
  el.inspector.querySelectorAll("[data-collider-edit-action]").forEach(button => {
    button.addEventListener("click", () => {
      const action = button.dataset.colliderEditAction;
      const collider = target.value;
      if (action === "focus") {
        if (!globalThis.focusSelectedViewportTransformTarget?.()) {
          state.message = "受击体尚未在 3D 视口中解析，无法聚焦。";
          renderShellStatus();
        }
        return;
      }
      if (action === "show-selected" || action === "show-part" || action === "show-all") {
        setBodyPartsPreviewScope(action === "show-selected" ? "selected" : action === "show-all" ? "all" : "part");
        return;
      }
      if (!state.canWrite) return;
      if (action === "duplicate") {
        duplicateColliderForManualEditing(target);
        return;
      }
      if (action === "delete") {
        deleteColliderFromManualEditing(target);
        return;
      }
      if (action === "expand" || action === "shrink") {
        const delta = action === "expand" ? 0.01 : -0.01;
        beginUndoTransaction(`${action === "expand" ? "外扩" : "收缩"}受击体 ${collider.colliderId || ""}`, `collider-margin:${collider.colliderId || ""}`);
        applyColliderMargin(collider, delta);
        applySymmetryEdit?.(target, collider.shape === "Box" ? "size" : "radius");
        updateUndoTransactionAfter();
        commitUndoTransaction();
        state.message = `${collider.colliderId || "受击体"} 已${action === "expand" ? "外扩" : "收缩"} 1cm；尚未保存。`;
        render();
        return;
      }
      if (action === "reset-rotation") {
        beginUndoTransaction(`受击体旋转归零 ${collider.colliderId || ""}`, `collider-rotation:${collider.colliderId || ""}`);
        const pose = collider.localPose || (collider.localPose = defaultLocalPose("BodyPart", collider.partId || ""));
        pose.rotation = { x: 0, y: 0, z: 0, w: 1 };
        pose.eulerHint = { x: 0, y: 0, z: 0 };
        applySymmetryEdit?.(target, "localPose");
        updateUndoTransactionAfter();
        commitUndoTransaction();
        state.message = `${collider.colliderId || "受击体"} 的局部旋转已归零；尚未保存。`;
        render();
      }
    });
  });
}

function applyColliderMargin(collider, deltaMeters) {
  if (!collider) return false;
  const delta = Number(deltaMeters);
  if (!Number.isFinite(delta) || delta === 0) return false;
  const shape = String(collider.shape || "Capsule");
  if (shape === "Box") {
    collider.size = collider.size || {};
    collider.size.x = roundColliderDimension(Math.max(0.001, Number(collider.size.x || 0.25) + delta * 2));
    collider.size.y = roundColliderDimension(Math.max(0.001, Number(collider.size.y || 0.25) + delta * 2));
    collider.size.z = roundColliderDimension(Math.max(0.001, Number(collider.size.z || 0.25) + delta * 2));
    return true;
  }
  collider.radius = roundColliderDimension(Math.max(0.001, Number(collider.radius || 0.12) + delta));
  if (shape === "Capsule") {
    collider.height = roundColliderDimension(Math.max(collider.radius * 2 + 0.001, Number(collider.height || 0.5) + delta * 2));
  }
  return true;
}

function roundColliderDimension(value) {
  return Number(Number(value).toFixed(6));
}

function duplicateColliderForManualEditing(target) {
  const colliders = state.package?.geometry?.colliders;
  if (!state.canWrite || !Array.isArray(colliders) || !target?.value) return false;
  beginUndoTransaction(`复制受击体 ${target.value.colliderId || ""}`, `collider-duplicate:${target.value.colliderId || ""}`);
  const sourceIndex = colliders.indexOf(target.value);
  const copy = JSON.parse(JSON.stringify(target.value));
  copy.colliderId = makeUniqueColliderId(`${target.value.colliderId || buildColliderAutoId(target.value.partId, target.value.shape)}_copy`, null);
  colliders.splice(sourceIndex >= 0 ? sourceIndex + 1 : colliders.length, 0, copy);
  state.selectedPath = `geometry/colliders/${copy.colliderId}`;
  state.bodyPartsPreviewScope = "selected";
  updateUndoTransactionAfter();
  commitUndoTransaction();
  state.message = `已复制为 ${copy.colliderId}；位置保持一致，请使用 W 移动。`;
  render();
  return true;
}

function deleteColliderFromManualEditing(target) {
  const colliders = state.package?.geometry?.colliders;
  if (!state.canWrite || !Array.isArray(colliders) || !target?.value) return false;
  const id = target.value.colliderId || "受击体";
  if (!window.confirm(`删除 ${id}？该操作只修改当前草稿，并可撤销。`)) return false;
  beginUndoTransaction(`删除受击体 ${id}`, `collider-delete:${id}`);
  const index = colliders.indexOf(target.value);
  if (index >= 0) colliders.splice(index, 1);
  const partPath = target.value.partId ? `geometry/body_parts/${target.value.partId}` : "geometry/body/parts";
  state.selectedPath = findTarget(partPath)?.value ? partPath : "geometry/body/parts";
  state.bodyPartsPreviewScope = "part";
  updateUndoTransactionAfter();
  commitUndoTransaction();
  state.message = `${id} 已从未保存草稿中删除。`;
  render();
  return true;
}

function createDefaultGameplayCameraProfile() {
  const abstractIds = abstractBoneOptions().map(option => option.value);
  const focusAbstractBoneId = abstractIds.includes("body_center")
    ? "body_center"
    : abstractIds.includes("head") ? "head" : (abstractIds[0] || "");
  return {
    profileId: `camera.${state.package?.manifest?.packageId || "character"}.gameplay`,
    focusAbstractBoneId,
    focusOffset: { x: 0, y: 0, z: 0 },
    framingBoundsCenter: { x: 0, y: 1, z: 0 },
    framingBoundsExtents: { x: 0.55, y: 1, z: 0.55 },
    normal: { distance: 2, minDistance: 1.5, maxDistance: 10, fieldOfView: 60, pitch: 28, yawOffset: 0, targetPadding: 0.35 },
    locked: { minDistance: 1.5, maxDistance: 10, targetScreenY: 0.66, targetPadding: 0.35 },
    obstruction: { radius: 0.22, padding: 0.12, minDistance: 0.75, focusClearance: 0.25 }
  };
}

function bindGameplayCameraActions() {
  el.inspector.querySelectorAll("[data-gameplay-camera-action]").forEach(button => {
    button.addEventListener("click", async () => {
      const action = button.dataset.gameplayCameraAction;
      const app = state.package.applicationConfig ||= {};
      if (action === "enable") {
        app.gameplayCameraProfile = createDefaultGameplayCameraProfile();
        state.gameplayCameraPreviewMode = "normal";
      } else if (action === "disable") {
        delete app.gameplayCameraProfile;
      } else if (action === "preview-normal" || action === "preview-locked") {
        state.gameplayCameraPreviewMode = action === "preview-locked" ? "locked" : "normal";
        renderViewport();
        renderInspector();
        return;
      } else if (action === "measure") {
        const measured = await globalThis.measureGameplayCameraFromViewport?.();
        if (!measured || !app.gameplayCameraProfile) {
          state.message = "当前 3D 预览没有可测量的角色模型。";
          renderShellStatus();
          return;
        }
        app.gameplayCameraProfile.framingBoundsCenter = measured.center;
        app.gameplayCameraProfile.framingBoundsExtents = measured.extents;
        state.message = `Gameplay Camera Bounds 已测量：${measured.sizeText}`;
      } else {
        return;
      }
      state.dirty = true;
      renderShellStatus();
      renderTree();
      renderViewport();
      renderInspector();
    });
  });
}

function renderObjectTitle(target) {
  if (globalThis.isBodyWorkspacePath?.()) return renderBodyWorkspaceObjectTitle(target);
  if (target.kind === "baseAttributes") {
    const manifest = state.package?.manifest || {};
    const character = [manifest.packageId, manifest.displayName].filter(Boolean).join(" · ");
    return `<div class="object-title"><strong>基础属性</strong><span>${escapeHtml(character)}</span></div><p class="attribute-page-description">角色初始数值。保存并导入 Unity 后，对新生成的角色生效。</p>`;
  }
  return `<div class="object-title"><strong>${escapeHtml(target.label)}</strong><span>${escapeHtml(state.selectedPath)}</span></div>${renderInspectorContext(target)}`;
}

function renderOverviewInspector() {
  const pkg = state.package || {};
  const view = typeof buildWorkflowView === "function" ? buildWorkflowView() : null;
  const issues = view?.issueCards || [];
  const sourceSummary = state.importMaterials?.summary || {};
  const commonSummary = state.commonResources?.summary || {};
  const resourcePlanGroups = Array.isArray(state.resourcePlan?.groups) ? state.resourcePlan.groups.length : 0;
  const unityEntries = getUnityCatalogEntries();
  const nextAction = view?.nextAction || { action: "reload", label: "刷新", detail: "读取角色状态" };
  return `<div class="inspector-overview">
    ${renderHurtboxFitReviewCard(pkg.hurtboxFitDraft)}
    <section class="reference-card overview-card">
      <div class="reference-card-head">
        <strong>当前流程</strong>
        <span>默认显示创作状态；Manifest JSON 已移到下方调试折叠区。</span>
      </div>
      <div class="reference-row"><span>角色包</span><strong>${escapeHtml(pkg.manifest?.packageId || "-")}</strong></div>
      <div class="reference-row"><span>版本</span><strong>${escapeHtml(pkg.manifest?.version || "-")}</strong></div>
      <div class="reference-row"><span>整体状态</span><strong>${escapeHtml(formatWorkflowStatusLabel(view?.overallStatus || "Unknown"))}</strong></div>
      <div class="reference-row"><span>下一步</span><strong>${escapeHtml(nextAction.detail || nextAction.label || "查看状态")}</strong>${renderWorkflowActionButton(nextAction, "workflow-primary-action")}</div>
    </section>
    <section class="reference-card overview-card">
      <div class="reference-card-head">
        <strong>需要处理的问题</strong>
        <span>先处理阻塞，再处理 warning；技术码保留在二级信息里。</span>
      </div>
      ${issues.length ? issues.map(card => renderWorkflowIssueCard(card)).join("") : `<div class="readiness-ok">当前没有阻塞或 warning。</div>`}
    </section>
    <section class="reference-card overview-card">
      <div class="reference-card-head">
        <strong>依赖与文件状态</strong>
        <span>用于判断下一步应该保存、同步角色源、还是进入 Unity 导入。</span>
      </div>
      <div class="reference-row"><span>角色本地源文件</span><strong>${escapeHtml(String(sourceSummary.total || 0))} 项</strong><span>${escapeHtml(String(sourceSummary.missing || 0))} 缺失 / ${escapeHtml(String(sourceSummary.dirty || 0))} 待同步</span></div>
      <div class="reference-row"><span>公共资源</span><strong>${escapeHtml(String(commonSummary.total || 0))} 项</strong><span>${escapeHtml(String(commonSummary.blocked || commonSummary.missing || 0))} 需 Resource Manager 处理</span></div>
      <div class="reference-row"><span>运行时资源计划</span><strong>${escapeHtml(String(resourcePlanGroups))} 组</strong><button type="button" data-workflow-action="open-context" data-context-tab="resourcePlan">查看资源计划</button></div>
      <div class="reference-row"><span>Unity Catalog</span><strong>${escapeHtml(String(unityEntries.length))} 项</strong><button type="button" data-workflow-action="open-inspector" data-inspector-tab="import">查看导入状态</button></div>
    </section>
    <details class="manifest-debug">
      <summary>Debug：Manifest JSON</summary>
      <pre>${escapeHtml(JSON.stringify(pkg.manifest || {}, null, 2))}</pre>
    </details>
  </div>`;
}

function renderHurtboxFitReviewCard(draft) {
  if (!draft) {
    return `<section class="reference-card overview-card">
      <div class="reference-card-head"><strong>Hurtbox 人工创作状态</strong><span>保存的 body_colliders.json 即为已批准权威；自动检测和额外确认都不是导入门禁。</span></div>
      <div class="reference-row"><span>状态</span><strong>已保存即生效</strong><button type="button" data-hurtbox-fit-action="confirm">记录可选人工确认</button></div>
      <div class="reference-row"><span>辅助建议</span><strong>尚未生成</strong><button type="button" data-hurtbox-fit-action="generate">生成可选拟合草稿</button></div>
    </section>`;
  }
  const issues = Array.isArray(draft.issues) ? draft.issues : [];
  const colliders = Array.isArray(draft.colliders) ? draft.colliders : [];
  const canApply = draft.status === "ReviewRequired" && colliders.length > 0;
  const confirmation = draft.authoritativeReviewStatus === "Confirmed" ? "已记录人工确认" : "已保存即生效";
  return `<section class="reference-card overview-card">
    <div class="reference-card-head"><strong>Hurtbox 人工创作状态</strong><span>body_colliders.json 是唯一权威；自动拟合只负责提供手动编辑起点，检测和额外确认不参与门禁。</span></div>
    <div class="reference-row"><span>生效状态</span><strong>${escapeHtml(confirmation)}</strong><span>保存后的当前文件直接进入 Unity 烘焙</span></div>
    <div class="reference-row"><span>预算</span><strong>${escapeHtml(String(draft.colliderCount || colliders.length))} / 32</strong><span>OBB ${escapeHtml(String(draft.obbCount || 0))} / 8</span></div>
    <div class="reference-row"><span>辅助拟合</span><strong>${escapeHtml(draft.status || "Unknown")}</strong><span>${escapeHtml(String(draft.sampledClipCount || 0))} clips / ${escapeHtml(String(draft.sampledFrameCount || 0))} reference frames；不参与门禁</span></div>
    ${issues.length ? `<div class="diagnostic-list">${issues.map(issue => `<div class="diagnostic error"><strong>${escapeHtml(issue.code || "HURTBOX_FIT")}</strong><span>${escapeHtml(issue.message || "")}</span></div>`).join("")}</div>` : ""}
    <div class="field-actions"><button type="button" data-hurtbox-fit-action="confirm">记录可选人工确认</button><button type="button" data-hurtbox-fit-action="generate">重新生成可选草稿</button><button type="button" data-hurtbox-fit-action="apply" ${canApply ? "" : "disabled"}>载入为手动编辑草稿</button><button type="button" data-hurtbox-fit-action="review">可选覆盖热图</button></div>
  </section>`;
}

function bindHurtboxFitActions() {
  el.inspector.querySelectorAll("[data-hurtbox-fit-action]").forEach(button => {
    button.addEventListener("click", async () => {
      const action = button.dataset.hurtboxFitAction;
      if (action === "confirm") {
        await globalThis.confirmHurtboxAuthoring?.();
        renderInspector();
        return;
      }
      if (action === "generate") {
        state.message = "正在通过 Unity Editor 采样正式 .anim 并生成 Hurtbox 拟合草稿……";
        renderShellStatus();
        await globalThis.importUnity?.();
        await globalThis.loadPackageState?.(state.packageRelative, { preserveDraft: true });
        return;
      }
      if (action === "review") {
        if (!state.hurtboxCoverageEnabled) document.getElementById("hurtboxCoverageButton")?.click();
        state.message = "已开启可选覆盖热图；结果仅供创作参考，不参与 Unity 导入门禁。";
        renderShellStatus();
        return;
      }
      if (action !== "apply") return;
      const draft = state.package?.hurtboxFitDraft;
      if (draft?.status !== "ReviewRequired" || !Array.isArray(draft.colliders) || !draft.colliders.length) return;
      beginUndoTransaction("载入 Hurtbox 初始草稿", "hurtbox-fit-apply");
      state.package.geometry.colliders = JSON.parse(JSON.stringify(draft.colliders));
      const firstCollider = state.package.geometry.colliders[0];
      if (firstCollider?.colliderId) {
        state.selectedPath = `geometry/colliders/${firstCollider.colliderId}`;
        state.hurtboxDisplayMode = "part";
        state.hurtboxDisplayPartId = firstCollider.partId || "";
      }
      updateUndoTransactionAfter();
      commitUndoTransaction();
      state.message = `已载入 ${draft.colliders.length} 个初始受击体；现在请按部位手动调整，确认后再保存。`;
      render();
    });
  });
}

function renderManifestInspector(manifest) {
  return `<div class="inspector-overview">
    <section class="reference-card overview-card">
      <div class="reference-card-head">
        <strong>Manifest 摘要</strong>
        <span>给调试和工程审查使用；创作流程优先看“流程概览”。</span>
      </div>
      <div class="reference-row"><span>Package ID</span><strong>${escapeHtml(manifest?.packageId || "-")}</strong></div>
      <div class="reference-row"><span>Stable ID</span><strong>${escapeHtml(manifest?.stableId || "-")}</strong></div>
      <div class="reference-row"><span>版本</span><strong>${escapeHtml(manifest?.version || "-")}</strong></div>
      <div class="reference-row"><span>类型</span><strong>${escapeHtml(manifest?.kind || "-")}</strong></div>
    </section>
    <details class="manifest-debug">
      <summary>Debug：原始 Manifest JSON</summary>
      <pre>${escapeHtml(JSON.stringify(manifest || {}, null, 2))}</pre>
    </details>
  </div>`;
}

function getInspectorInputTarget(target, fieldPath) {
  if (target?.kind !== "weapon" || !isWeaponModelWrapperField(fieldPath)) return target;
  const resource = ensureWeaponModelWrapperPose(target.value);
  return resource ? { kind: "resource", label: resource.resourceKey || "model", value: resource } : target;
}

async function saveCommonResourceTargetIfNeeded(target) {
  if (target?.kind !== "resource" || !isCommonPreviewModelResource(target.value)) return true;
  openResourceManagerForPublicResource(target.value, {
    message: "公共资源 import hints 由 Resource Manager 管理。"
  });
  return false;
}

function isWeaponModelWrapperField(fieldPath) {
  return String(fieldPath || "").startsWith("importHints.modelWrapperPose.");
}

function getWeaponModelResource(weapon) {
  const key = getWeaponAttachmentResourceKey(weapon);
  return key ? findResourceByKey(key) : null;
}

function ensureWeaponModelWrapperPose(weapon) {
  const resource = getWeaponModelResource(weapon);
  if (!resource || resource.typeId !== "model") return null;
  ensureModelWrapperPose(resource);
  return resource;
}

function renderInspectorContext(target) {
  if (target.kind === "animationProfile") return renderAnimationProfileReferenceContext(target.value);
  if (target.kind === "animationGroup") return renderAnimationGroupReferenceContext(target.value);
  if (target.kind === "animationClip") return renderAnimationClipReferenceContext(target);
  if (target.kind === "animationBlendSpace") return renderAnimationBlendSpaceContext(target);
  if (target.kind === "animationBlendPoint") return renderAnimationBlendPointContext(target);
  if (target.kind === "animationSlot") return renderAnimationSlotReferenceContext(target.value);
  if (target.kind === "reactionProfile") return renderReactionProfileReferenceContext(target.value);
  if (target.kind === "reactionSlot") return renderReactionSlotReferenceContext(target.value);
  if (target.kind === "weapon") return `${renderWeaponReferenceContext(target.value)}${renderTransformPathGuidanceContext(target.kind)}`;
  if (target.kind === "resource" && target.value) {
    const modelContext = target.value?.typeId === "model" ? renderModelResourceContext(target.value) : "";
    return `${modelContext}${renderResourceSyncContext(target.value)}`;
  }
  if (["part", "socket", "collider", "contactPoint", "trace"].includes(target.kind)) {
    return renderTransformPathGuidanceContext(target.kind);
  }
  return "";
}

function renderAnimationProfileReferenceContext(profile) {
  const slots = profile?.slots || [];
  const selectedGroups = collectOptions(slots.map(slot => slot?.animationGroupId)).join(" / ") || "未选择";
  return `<section class="reference-card">
    <div class="reference-card-head"><strong>Animation Profile 引用</strong><span>CharacterStudio 只保存 Profile 引用；分组和剪辑请在 AnimationEditor 中编辑。</span></div>
    <div class="reference-row"><span>Profile</span><strong>${escapeHtml(profile?.profileId || DEFAULT_ANIMATION_PROFILE_ID)}</strong></div>
    <div class="reference-row"><span>槽位</span><strong>${escapeHtml(String(slots.length))}</strong></div>
    <div class="reference-row"><span>已选分组</span><strong>${escapeHtml(selectedGroups)}</strong></div>
    ${renderAnimationEditorReferenceRow()}
  </section>`;
}

function renderAnimationGroupReferenceContext(group) {
  const resource = findPackageResourceByKey(state.package, group?.sourceResourceKey);
  return `<section class="reference-card">
    <div class="reference-card-head"><strong>Animation Group</strong><span>Group、Clip、Blend 和 Timeline 内容请在 AnimationEditor 中编辑。</span></div>
    <div class="reference-row"><span>源资源</span><strong>${escapeHtml(resource ? getResourceDisplayName(resource) : group?.sourceResourceKey || "未绑定")}</strong></div>
    <div class="reference-row"><span>Clips</span><strong>${escapeHtml(String(group?.clips?.length || 0))}</strong></div>
    <div class="reference-row"><span>BlendSpaces</span><strong>${escapeHtml(String(group?.blendSpaces?.length || 0))}</strong></div>
    ${renderAnimationEditorReferenceRow()}
  </section>`;
}

function renderAnimationClipReferenceContext(target) {
  return `<section class="reference-card">
    <div class="reference-card-head"><strong>Animation Clip</strong><span>剪辑源详情请在 AnimationEditor 中编辑。</span></div>
    <div class="reference-row"><span>Group</span><strong>${escapeHtml(target.groupId || "")}</strong></div>
    ${renderAnimationEditorReferenceRow()}
  </section>`;
}

function renderAnimationBlendSpaceContext(target) {
  const group = findAnimationGroupById(target.groupId);
  return `<section class="reference-card">
    <div class="reference-card-head"><strong>2D Blend</strong><span>BlendTree 详情请在 AnimationEditor 中编辑。</span></div>
    <div class="reference-row"><span>Group</span><strong>${escapeHtml(group?.groupId || target.groupId || "")}</strong></div>
    <div class="reference-row"><span>剪辑数</span><strong>${escapeHtml(String(group?.clips?.length || 0))}</strong></div>
    ${renderAnimationEditorReferenceRow()}
  </section>`;
}

function renderAnimationBlendPointContext(target) {
  return `<section class="reference-card">
    <div class="reference-card-head"><strong>Blend 点</strong><span>Blend 点数值请在 AnimationEditor 中编辑。</span></div>
    <div class="reference-row"><span>BlendSpace</span><strong>${escapeHtml(target.blendSpaceId || "")}</strong></div>
    ${renderAnimationEditorReferenceRow()}
  </section>`;
}

function renderAnimationEditorReferenceRow() {
  return `<div class="reference-row"><span>编辑器</span><strong>AnimationEditor</strong><a class="button mini-link" href="${escapeHtml(getAnimationEditorUrl())}">打开 AnimationEditor</a></div>`;
}

function renderAnimationSlotReferenceContext(slot) {
  const resource = findAnimationSlotResource(slot);
  const selection = slot?.resourceSelection || {};
  const selectedText = resource
    ? getResourceDisplayName(resource.resource || resource)
    : firstNonEmpty(slot?.resourceKey, selection.runtimeResourceKey, selection.packageResourceKey, selection.providerResourceKey, selection.unityAssetPath, "未选择");
  return `<section class="reference-card">
    <div class="reference-card-head"><strong>Animation Slot 引用</strong><span>保存此 Profile 槽位的 ResourceSelectionRef 数据。</span></div>
    <div class="reference-row"><span>已选资源</span><strong>${escapeHtml(selectedText)}</strong></div>
    <div class="reference-row"><span>绑定类型</span><strong>${escapeHtml(selection.bindingKind || resource?.bindingKind || "未知")}</strong></div>
    <div class="reference-row"><span>预加载策略</span><strong>${escapeHtml(slot?.preloadPolicy || "AnimationWarmup")}</strong></div>
    ${resource?.path ? renderReferenceRow("资源", resource.displayName, resource.path, "打开") : ""}
  </section>`;
}

function renderReactionProfileReferenceContext(profile) {
  const slots = profile?.slots || [];
  const selected = slots.filter(slot => firstNonEmpty(
    slot?.resourceKey,
    slot?.resourceSelection?.runtimeResourceKey,
    slot?.resourceSelection?.packageResourceKey,
    slot?.resourceSelection?.providerResourceKey,
    slot?.resourceSelection?.unityAssetPath)).length;
  return `<section class="reference-card">
    <div class="reference-card-head"><strong>受击动作组</strong><span>当前武器 ActionSet 绑定的受击动作规则；Unity 导入生成 CharacterAction 配置。</span></div>
    <div class="reference-row"><span>Authoring Profile</span><strong>${escapeHtml(profile?.profileId || DEFAULT_REACTION_PROFILE_ID)}</strong></div>
    <div class="reference-row"><span>生成 Profile</span><strong>${escapeHtml(getGeneratedReactionProfileStableId())}</strong></div>
    <div class="reference-row"><span>槽位</span><strong>${escapeHtml(String(slots.length))}</strong></div>
    <div class="reference-row"><span>已选动画</span><strong>${escapeHtml(String(selected))}</strong></div>
  </section>`;
}

function renderReactionSlotReferenceContext(slot) {
  const resource = findReactionSlotResource(slot);
  const selection = slot?.resourceSelection || {};
  const selectedText = resource
    ? getResourceDisplayName(resource.resource || resource)
    : firstNonEmpty(slot?.resourceKey, selection.runtimeResourceKey, selection.packageResourceKey, selection.providerResourceKey, selection.unityAssetPath, "未选择");
  return `<section class="reference-card">
    <div class="reference-card-head"><strong>${escapeHtml(getReactionSlotDisplayName(slot))}</strong><span>保存选中的受击动画；Unity 导入时写入对应动作规则。</span></div>
    <div class="reference-row"><span>生成 Action</span><strong>${escapeHtml(slot?.actionId || getGeneratedReactionActionId(state.package, slot || {}))}</strong></div>
    <div class="reference-row"><span>已选资源</span><strong>${escapeHtml(selectedText)}</strong></div>
    <div class="reference-row"><span>绑定类型</span><strong>${escapeHtml(selection.bindingKind || resource?.bindingKind || "未知")}</strong></div>
    <div class="reference-row"><span>预加载策略</span><strong>${escapeHtml(slot?.preloadPolicy || "AnimationWarmup")}</strong></div>
    ${resource?.path ? renderReferenceRow("资源", resource.displayName, resource.path, "打开") : ""}
  </section>`;
}

function renderWeaponReferenceContext(weapon) {
  const weaponItems = buildWeaponVisualizationItems(state.package, [weapon.equipSlot || "mainHand"], getResourceUnitySync);
  const item = weaponItems
    .find(entry => entry.attachment === weapon || entry.weaponId === weapon.weaponId)
    || weaponItems[0]
    || null;
  const weaponKey = getWeaponAttachmentResourceKey(weapon);
  const resource = item?.resource || findResourceByKey(weaponKey);
  const resourcePath = resource ? getPreviewModelResourceObjectPath(resource) : "";
  const socket = item?.socket || findSocketById(weapon.attachSocketId);
  const trace = item?.trace || findTraceById(weapon.traceId);
  const tone = item ? getWeaponVisualizationTone(item) : "warn";
  const issueRows = item?.issues?.length
    ? `<div class="weapon-data-issue-list">${item.issues.map(issue => `<div class="weapon-data-issue ${escapeHtml(issue.tone)}">${escapeHtml(issue.message)}</div>`).join("")}</div>`
    : `<div class="weapon-data-issue ok">武器模型、Socket 和轨迹引用可用。</div>`;
  return `<section class="reference-card">
    <div class="reference-card-head"><strong>武器数据</strong><span>默认武器挂接、Socket、模型、轨迹和 Unity 同步状态。</span></div>
    <div class="reference-row"><span>装备槽</span><strong>${escapeHtml(getEquipSlotLabel(weapon.equipSlot))}</strong><span class="weapon-data-state ${escapeHtml(tone)}">${escapeHtml(item ? getWeaponVisualizationStatusText(item) : "未知")}</span></div>
    <div class="reference-row"><span>武器</span><strong>${escapeHtml(weapon.weaponId || "未配置")}</strong><button type="button" data-inspector-action="viewFocusedWeapon">查看武器</button></div>
    ${renderReferenceRow("Socket", socket ? socket.socketId : weapon.attachSocketId || "未绑定", socket ? `geometry/sockets/${socket.socketId}` : "", "编辑 Socket")}
    ${renderReferenceRow("武器资源", resource ? getResourceDisplayName(resource) : weaponKey || "未绑定", resourcePath, "编辑资源")}
    <div class="reference-row"><span>选择武器</span><strong>${escapeHtml(resource ? "已绑定" : "需要选择")}</strong><button type="button" data-inspector-action="chooseWeaponModel">选择武器</button></div>
    ${renderReferenceRow("攻击轨迹", trace ? trace.traceId : weapon.traceId || "未绑定", trace ? `geometry/traces/${trace.traceId}` : "", "编辑轨迹")}
    <div class="reference-row"><span>Unity</span><strong>${item?.sync ? renderSyncBadge(item.sync) : "无数据"}</strong>${resourcePath ? `<button type="button" data-jump="${escapeHtml(resourcePath)}">查看资源</button>` : ""}</div>
    ${issueRows}
  </section>`;
}

function renderModelResourceContext(resource) {
  const owners = (state.package?.geometry?.weaponAttachments || [])
    .filter(weapon => getWeaponAttachmentResourceKey(weapon) === resource.resourceKey);
  const ownerText = owners.length
    ? owners.map(weapon => `${weapon.equipSlot || "slot"}:${weapon.weaponId}`).join(" / ")
    : (isBodyModelBinding(resource, state.package) ? "角色主体" : "没有角色持有者");
  const isCommon = isCommonPreviewModelResource(resource);
  const packagePath = isCommon
    ? (resource.generatedAssetPath || resource.relativePath || resource.provenance?.sourceFile || "")
    : `${state.packageRelative}/${resource.relativePath || ""}`;
  const scopeText = isCommon
    ? "公共资源变换。编辑会保存到 ArtSource/Common，并影响所有引用它的角色。"
    : "模型 wrapper pose 编辑会影响预览和导入位置。";
  const scaleDiagnostic = getModelScaleDiagnostic(resource);
  const scaleDiagnosticHtml = renderModelScaleDiagnosticContext(scaleDiagnostic);
  const scalePresetHtml = renderModelScalePresetContext();
  return `<section class="reference-card">
    <div class="reference-card-head"><strong>模型资源</strong><span>${escapeHtml(scopeText)}</span></div>
    <div class="reference-row"><span>持有者</span><strong>${escapeHtml(ownerText)}</strong></div>
    <div class="reference-row"><span>包路径</span><code>${escapeHtml(packagePath)}</code></div>
    ${scalePresetHtml}
    ${scaleDiagnosticHtml}
  </section>`;
}

function renderModelScalePresetContext() {
  const presets = globalThis.MODEL_WRAPPER_SCALE_PRESETS || [0.01, 0.1, 1, 10, 100];
  return `<div class="reference-row"><span>整体缩放</span><strong>预设</strong><span class="inline-actions">${presets.map(scale => `<button type="button" data-inspector-action="applyModelScalePreset" data-scale-preset="${escapeHtml(scale)}">${escapeHtml(scale)}x</button>`).join("")}</span></div>`;
}

function renderModelScaleDiagnosticContext(diagnostic) {
  if (!diagnostic?.status || ["NotModel", "NotFbx", "SourceMissing"].includes(diagnostic.status)) return "";
  const unit = diagnostic.unitScaleFactor == null ? "-" : String(diagnostic.unitScaleFactor);
  const recommended = diagnostic.recommendedScale == null ? "-" : String(diagnostic.recommendedScale);
  const preview = diagnostic.previewUnitScale == null ? "-" : String(diagnostic.previewUnitScale);
  const current = diagnostic.currentScale == null ? "-" : String(diagnostic.currentScale);
  if (diagnostic.status === "UnityConvertsUnits") {
    return `<div class="reference-row"><span>Unity 单位转换</span><strong>Convert Units</strong><span>${escapeHtml(`预览 ${preview}x；wrapper ${current}x`)}</span></div>
      <div class="meta" title="${escapeHtml(diagnostic.reason || "")}">Unity importer 处理 FBX 单位。模型 wrapper scale 是美术变换，不是单位转换。</div>`;
  }
  if (diagnostic.status === "WrapperScaleMismatch") {
    return `<div class="reference-row"><span>Unity 单位转换</span><strong>重复缩放</strong><button type="button" data-inspector-action="applyRecommendedModelScale">重置 wrapper 到 ${escapeHtml(recommended)}x</button></div>
      <div class="meta" title="${escapeHtml(diagnostic.reason || "")}">当前 wrapper 平均 scale：${escapeHtml(current)}；CharacterStudio 预览单位 scale：${escapeHtml(preview)}。</div>`;
  }
  if (diagnostic.status === "PreviewUnitScale") {
    return `<div class="reference-row"><span>预览单位缩放</span><strong>${escapeHtml(preview)}x</strong><span>不会保存到 wrapper</span></div>
      <div class="meta" title="${escapeHtml(diagnostic.reason || "")}">资产导入后，FBX 单位转换应由 Unity import 负责。</div>`;
  }
  if (diagnostic.status === "RecommendedScale") {
    return `<div class="reference-row"><span>FBX 缩放</span><strong>UnitScaleFactor ${escapeHtml(unit)} -> wrapper ${escapeHtml(recommended)}</strong><button type="button" data-inspector-action="applyRecommendedModelScale">应用推荐缩放</button></div>
      <div class="meta" title="${escapeHtml(diagnostic.reason || "")}">当前 wrapper 平均 scale：${escapeHtml(current)}。</div>`;
  }
  if (diagnostic.status === "ScaleApplied") {
    return `<div class="reference-row"><span>FBX 缩放</span><strong>已应用</strong><span>${escapeHtml(`UnitScaleFactor ${unit}; wrapper ${current}`)}</span></div>`;
  }
  if (diagnostic.status === "UnitOk") {
    return `<div class="reference-row"><span>FBX 缩放</span><strong>单位正常</strong><span>${escapeHtml(diagnostic.reason || "")}</span></div>`;
  }
  if (diagnostic.status === "Unknown") {
    return `<div class="reference-row"><span>FBX 缩放</span><strong>未知</strong><span>${escapeHtml(diagnostic.reason || "")}</span></div>`;
  }
  return "";
}

function renderResourceSyncContext(resource) {
  const sync = getResourceUnitySync(resource);
  const report = getUnityImportReport();
  const operation = sync.operation;
  const diagnostics = sync.diagnostics;
  const diagnosticsHtml = diagnostics.length
    ? `<div class="sync-diagnostics">${diagnostics.map(item => `<div>${escapeHtml(item)}</div>`).join("")}</div>`
    : `<div class="meta">没有 Unity 同步诊断。</div>`;
  return `<section class="reference-card sync-card">
    <div class="reference-card-head"><strong>Unity 同步</strong><span>此资源的导入报告和 Unity Resource Catalog 状态。</span></div>
    <div class="reference-row"><span>状态</span><strong>${renderSyncBadge(sync)}</strong></div>
    <div class="reference-row"><span>资产路径</span><code title="${escapeHtml(sync.unityAssetPath)}">${escapeHtml(sync.unityAssetPath || "-")}</code></div>
    <div class="reference-row"><span>GUID</span><code>${escapeHtml(sync.unityAssetGuid || "-")}</code></div>
    <div class="reference-row"><span>Importer</span><strong>${escapeHtml(sync.importerKind || sync.unityMainObjectType || "-")}</strong></div>
    ${operation ? `<div class="reference-row"><span>最近操作</span><strong>${escapeHtml(operation.action || operation.kind || "operation")}</strong></div>` : ""}
    ${report?.status ? `<div class="reference-row"><span>Report</span><strong>${escapeHtml(report.status)}</strong></div>` : ""}
    ${diagnosticsHtml}
  </section>`;
}

function renderReferenceRow(label, value, jumpPath, actionLabel) {
  return `<div class="reference-row"><span>${escapeHtml(label)}</span><strong>${escapeHtml(value)}</strong>${jumpPath ? `<button type="button" data-jump="${escapeHtml(jumpPath)}">${escapeHtml(actionLabel || "打开")}</button>` : ""}</div>`;
}

function renderTransformPathGuidanceContext(kind) {
  if (kind === "contactPoint") {
    return `<section class="reference-card">
      <div class="reference-card-head"><strong>Contact Audio Point</strong><span>PresentationOnly; not gameplay authority.</span></div>
      <div class="meta">Offset: local meters relative to Motion Socket when bound; otherwise character body local.</div>
      <div class="meta">Radius: sphere probe radius in meters; compiled to millimeters.</div>
      <div class="meta">Body Part: semantic owner for audio rule matching.</div>
      <div class="meta">Unity sockets and bones may drive presentation audio, but do not enter Combat hash, save, or network state.</div>
    </section>`;
  }
  const title = kind === "trace" ? "Trace Path Guidance" : "Path Guidance";
  return `<section class="reference-card">
    <div class="reference-card-head"><strong>${escapeHtml(title)}</strong><span>How parent part, bone path, locator path, and local parent path work together.</span></div>
    <div class="meta">Parent Part: hierarchy anchor for body/geometry relationships.</div>
    <div class="meta">Bone Path: bind to skeleton bones; preferred for stable rig following.</div>
    <div class="meta">Locator Path: bind to imported model locator markers when no bone path is suitable.</div>
    <div class="meta">Local Parent Path: interpreted by Parent Kind (Bone / Locator / BodyPart / Socket).</div>
    <div class="meta">Precedence: if both Bone Path and Locator Path exist, Bone Path is used first.</div>
  </section>`;
}

function findResourceByKey(resourceKey) {
  if (!resourceKey) return null;
  const packageResource = (state.package?.resourceCatalog?.entries || []).find(resource => resource?.resourceKey === resourceKey);
  if (packageResource) return packageResource;
  const commonResource = typeof findPreviewModelResourceByKey === "function"
    ? findPreviewModelResourceByKey(resourceKey, state.package?.resourceCatalog?.entries || [])
    : null;
  if (commonResource) return commonResource;
  const libraryItem = findResourceLibraryItemByKey(resourceKey);
  return libraryItem?.resource || null;
}

function collectResourceReferences(resource) {
  const references = [];
  const key = resource?.resourceKey || "";
  const stableId = resource?.stableId || "";
  if (!key && !stableId) return references;
  if (isApplicationResourceReference(key, state.package)) {
    references.push({ sourceConfigKind: "character", sourceField: "resourceKeys", preloadPolicy: "SpawnCritical" });
  }
  for (const attachment of state.package?.geometry?.weaponAttachments || []) {
    if (getWeaponAttachmentResourceKey(attachment) === key) {
      references.push({ sourceConfigKind: "weapon", sourceStableId: attachment.weaponId, sourceField: "weaponRef", preloadPolicy: "EquipmentInitial" });
    }
  }
  for (const group of state.package?.applicationConfig?.animationGroups || []) {
    if (group?.sourceResourceKey === key || group?.sourceResourceKey === stableId) {
      references.push({
        sourceConfigKind: "animationGroup",
        sourceStableId: group.groupId,
        sourceField: "sourceResourceKey",
        preloadPolicy: "AnimationWarmup"
      });
    }
  }
  for (const profile of state.package?.applicationConfig?.animationProfiles || []) {
    for (const slot of profile?.slots || []) {
      const selected = [
        slot?.resourceKey,
        slot?.resourceSelection?.runtimeResourceKey,
        slot?.resourceSelection?.packageResourceKey,
        slot?.resourceSelection?.providerResourceKey,
        slot?.resourceSelection?.resourceStableId
      ].filter(Boolean);
      if (selected.includes(key) || selected.includes(stableId)) {
        references.push({
          sourceConfigKind: "animation",
          sourceStableId: profile.profileId,
          sourceField: `slots/${slot.slotId}`,
          preloadPolicy: slot.preloadPolicy || "AnimationWarmup"
        });
      }
    }
  }
  for (const entry of state.package?.resourceCatalog?.entries || []) {
    if (entry === resource) continue;
    for (const dependency of entry.dependencies || []) {
      if (dependency?.resourceKey === key || dependency?.stableId === stableId) {
        references.push({ sourceConfigKind: "resource", sourceStableId: entry.stableId || entry.resourceKey, sourceField: "dependencies", preloadPolicy: "AnimationWarmup" });
      }
    }
    const preview = entry.preview || {};
    if ([preview.thumbnailResourceKey, preview.previewMeshResourceKey, preview.placeholderResourceKey].includes(key)) {
      references.push({ sourceConfigKind: "resource", sourceStableId: entry.stableId || entry.resourceKey, sourceField: "preview", preloadPolicy: "UiDeferred" });
    }
  }
  return references;
}

function formatDiagnosticText(issue) {
  if (!issue) return "";
  if (typeof issue === "string") return issue;
  return [issue.severity, issue.code || issue.gate, issue.message].filter(Boolean).join(": ");
}

function findSocketById(socketId) {
  if (!socketId) return null;
  return (state.package?.geometry?.sockets || []).find(socket => socket?.socketId === socketId) || null;
}

function findTraceById(traceId) {
  if (!traceId) return null;
  return (state.package?.geometry?.traces || []).find(trace => trace?.traceId === traceId) || null;
}

function normalizeInspectorTarget(target) {
  if (target.kind === "resource" && target.value?.typeId === "model") {
    ensureModelWrapperPose(target.value);
  } else if (target.kind === "collider") {
    ensureLocalPose(target.value, "localPose", { parentKind: "BodyPart", parentPath: target.value.partId || "" });
  } else if (target.kind === "bodyPartDamageDefinition") {
    target.value.partStateId = String(target.value.partStateId || "");
    target.value.memberPartIds = Array.isArray(target.value.memberPartIds) ? target.value.memberPartIds : [];
    target.value.damageZoneId = String(target.value.damageZoneId || "");
    target.value.woundCapacity = Number(target.value.woundCapacity || 0);
    target.value.openDamageScaleMilli = Number(target.value.openDamageScaleMilli || 0);
    target.value.focusConsumeTag = String(target.value.focusConsumeTag || "");
    target.value.maxIntegrity = Number(target.value.maxIntegrity || 0);
    target.value.integrityDamageScaleMilli = Number(target.value.integrityDamageScaleMilli || 0);
    target.value.breakReactionId = String(target.value.breakReactionId || "");
    target.value.authoredPriorityDeltaOpen = Number(target.value.authoredPriorityDeltaOpen || 0);
    target.value.authoredPriorityDeltaBroken = Number(target.value.authoredPriorityDeltaBroken || 0);
    target.value.severable = target.value.severable === true;
    target.value.severRequiredTag = String(target.value.severRequiredTag || "");
    target.value.presentationEventId = String(target.value.presentationEventId || "");
  } else if (target.kind === "bodyPresenceAnimatedSource") {
    target.value.sourceId = String(target.value.sourceId || "");
    target.value.colliderId = String(target.value.colliderId || "");
    target.value.skinMillimeters = Number(target.value.skinMillimeters || 0);
    target.value.pressureScaleMilli = Number(target.value.pressureScaleMilli || 0);
    target.value.enabled = target.value.enabled !== false;
  } else if (target.kind === "damageZone") {
    target.value.zoneId = String(target.value.zoneId || "");
    target.value.displayName = String(target.value.displayName || target.value.zoneId || "");
    target.value.hitZoneIds = Array.isArray(target.value.hitZoneIds) ? target.value.hitZoneIds : [];
    target.value.hittable = target.value.hittable !== false;
    target.value.aimable = target.value.aimable !== false;
    target.value.approachable = target.value.approachable !== false;
    target.value.authoredPriority = Number(target.value.authoredPriority || 0);
  } else if (target.kind === "socket") {
    ensureLocalPose(target.value, "localPose", defaultSocketPoseParent(target.value));
  } else if (target.kind === "weapon") {
    ensureLocalPose(target.value, "localGripPose", { parentKind: "Socket", parentPath: target.value.attachSocketId || "" });
  } else if (target.kind === "trace") {
    ensureLocalPose(target.value, "startPose", { parentKind: "Locator", parentPath: target.value.startLocatorPath || "" });
    ensureLocalPose(target.value, "endPose", { parentKind: "Locator", parentPath: target.value.endLocatorPath || "" });
  } else if (target.kind === "contactPoint") {
    normalizeContactPoint(target.value);
  }
}

function defaultSocketPoseParent(socket) {
  if (socket?.bonePath) return { parentKind: "Bone", parentPath: socket.bonePath };
  if (socket?.locatorPath) return { parentKind: "Locator", parentPath: socket.locatorPath };
  if (socket?.parentPartId) return { parentKind: "BodyPart", parentPath: socket.parentPartId };
  return { parentKind: "ModelRoot", parentPath: "" };
}

function normalizeContactPoint(contactPoint) {
  if (!contactPoint) return;
  contactPoint.stableId = String(contactPoint.stableId || "");
  contactPoint.contactPointId = Number(contactPoint.contactPointId || 0);
  contactPoint.bodyPartId = Number(contactPoint.bodyPartId || 0);
  contactPoint.partId = String(contactPoint.partId || firstValue(bodyPartOptions()) || "");
  contactPoint.localOffsetMeters = contactPoint.localOffsetMeters && typeof contactPoint.localOffsetMeters === "object"
    ? contactPoint.localOffsetMeters
    : { x: 0, y: 0, z: 0 };
  contactPoint.localOffsetMeters.x = Number(contactPoint.localOffsetMeters.x || 0);
  contactPoint.localOffsetMeters.y = Number(contactPoint.localOffsetMeters.y || 0);
  contactPoint.localOffsetMeters.z = Number(contactPoint.localOffsetMeters.z || 0);
  contactPoint.radiusMeters = Number(contactPoint.radiusMeters || 0.09);
  contactPoint.layerMaskProfileId = String(contactPoint.layerMaskProfileId || "ground");
  contactPoint.usage = String(contactPoint.usage || "PresentationAudio");
  contactPoint.authority = String(contactPoint.authority || "PresentationOnly");
  contactPoint.activationMode = String(contactPoint.activationMode || "Always");
  contactPoint.motionMode = String(contactPoint.motionMode || "Static");
  contactPoint.motionSocketId = String(contactPoint.motionSocketId || "");
  contactPoint.sourceOrder = Number(contactPoint.sourceOrder || 0);
}

function editableFields(kind, value = null) {
  if (kind === "baseAttributes") return [
    integerField("hp", "生命", { min: 1, max: 16777215, group: "基础属性", help: "生成角色时的生命值与基础生命上限。" }),
    integerField("attack", "攻击", { min: 0, max: 16777215, group: "基础属性", help: "参与动作伤害的攻击属性收益。" }),
    integerField("defense", "防御", { min: 0, max: 16777215, group: "基础属性", help: "参与承受伤害时的防御计算。" }),
    integerField("baseCohesion", "基础聚限", { min: 1, max: 2147483647, group: "聚限与恢复", help: "没有动作支撑时的基础破聚阈值。" }),
    integerField("scatterRecoveryRate", "散势恢复速度", { min: 0, max: 2147483647, unit: "点／模拟帧", group: "聚限与恢复", help: "开始恢复后，每个模拟帧回落的散势债务；0 表示不自动回落。" }),
    integerField("scatterRecoveryDelayFrames", "散势恢复延迟", { min: 0, max: 2147483647, unit: "帧", group: "聚限与恢复", help: "最后一次受压后，等待多久开始自然回落。" }),
    integerField("cohesionBreakRecoveryDelayFrames", "破聚恢复时间", { min: 0, max: 2147483647, unit: "帧", group: "聚限与恢复", help: "破聚后的定时恢复等待；0 表示不启用定时恢复。" })
  ];
  if (kind === "resource" && value?.typeId === "model") return [
    field("usage", { label: "Resource usage", type: "select", options: MODEL_USAGE_OPTIONS, group: "resource", help: "Declares whether this model is body, weapon, preview, or generic." }),
    modelPositionField("importHints.modelWrapperPose.position.x", "Pos X"),
    modelPositionField("importHints.modelWrapperPose.position.y", "Pos Y"),
    modelPositionField("importHints.modelWrapperPose.position.z", "Pos Z"),
    modelRotationField("importHints.modelWrapperPose.eulerHint.x", "Rot X"),
    modelRotationField("importHints.modelWrapperPose.eulerHint.y", "Rot Y"),
    modelRotationField("importHints.modelWrapperPose.eulerHint.z", "Rot Z"),
    modelScaleField("importHints.modelWrapperPose.scale.x", "Scale X"),
    modelScaleField("importHints.modelWrapperPose.scale.y", "Scale Y"),
    modelScaleField("importHints.modelWrapperPose.scale.z", "Scale Z")
  ];
  if (["animationProfile", "animationGroup", "animationClip", "animationBlendSpace", "animationBlendPoint"].includes(kind)) return [];
  if (kind === "gameplayCamera") return [
    identityField("profileId", "Camera Profile ID", ["camera.character.gameplay"], "Stable character gameplay camera profile id."),
    field("focusAbstractBoneId", { label: "Focus Abstract Bone", type: "select", options: abstractBoneOptions(), group: "focus", help: "Runtime only consumes generated abstract-bone bindings." }),
    positionField("focusOffset.x", "Focus Offset X", "focus"),
    positionField("focusOffset.y", "Focus Offset Y", "focus"),
    positionField("focusOffset.z", "Focus Offset Z", "focus"),
    positionField("framingBoundsCenter.x", "Bounds Center X", "framing"),
    positionField("framingBoundsCenter.y", "Bounds Center Y", "framing"),
    positionField("framingBoundsCenter.z", "Bounds Center Z", "framing"),
    positiveField("framingBoundsExtents.x", "Bounds Extent X", { min: 0.01, max: 100, step: 0.01, unit: "m", group: "framing" }),
    positiveField("framingBoundsExtents.y", "Bounds Extent Y", { min: 0.01, max: 100, step: 0.01, unit: "m", group: "framing" }),
    positiveField("framingBoundsExtents.z", "Bounds Extent Z", { min: 0.01, max: 100, step: 0.01, unit: "m", group: "framing" }),
    positiveField("normal.distance", "Normal Distance", { min: 0.01, max: 200, step: 0.1, unit: "m", group: "normal" }),
    positiveField("normal.minDistance", "Normal Min Distance", { min: 0.01, max: 200, step: 0.1, unit: "m", group: "normal" }),
    positiveField("normal.maxDistance", "Normal Max Distance", { min: 0.01, max: 200, step: 0.1, unit: "m", group: "normal" }),
    positiveField("normal.fieldOfView", "Field Of View", { min: 20, max: 80, step: 1, unit: "°", group: "normal" }),
    field("normal.pitch", { label: "Pitch", type: "number", min: -80, max: 80, step: 1, unit: "°", group: "normal" }),
    field("normal.yawOffset", { label: "Yaw Offset", type: "number", min: -180, max: 180, step: 1, unit: "°", group: "normal" }),
    positiveField("normal.targetPadding", "Normal Padding", { min: 0, max: 20, step: 0.05, unit: "m", group: "normal", fallback: 0 }),
    positiveField("locked.minDistance", "Locked Min Distance", { min: 0.01, max: 200, step: 0.1, unit: "m", group: "locked" }),
    positiveField("locked.maxDistance", "Locked Max Distance", { min: 0.01, max: 200, step: 0.1, unit: "m", group: "locked" }),
    field("locked.targetScreenY", { label: "Locked Screen Y", type: "number", min: 0.55, max: 0.85, step: 0.01, group: "locked" }),
    positiveField("locked.targetPadding", "Locked Padding", { min: 0, max: 20, step: 0.05, unit: "m", group: "locked", fallback: 0 }),
    positiveField("obstruction.radius", "Obstruction Radius", { min: 0, max: 10, step: 0.01, unit: "m", group: "obstruction", fallback: 0 }),
    positiveField("obstruction.padding", "Obstruction Padding", { min: 0, max: 10, step: 0.01, unit: "m", group: "obstruction", fallback: 0 }),
    positiveField("obstruction.minDistance", "Obstruction Min Distance", { min: 0, max: 100, step: 0.1, unit: "m", group: "obstruction", fallback: 0 }),
    positiveField("obstruction.focusClearance", "Focus Clearance", { min: 0, max: 10, step: 0.01, unit: "m", group: "obstruction", fallback: 0 })
  ];
  if (kind === "reactionProfile") return [];
  if (kind === "reactionSlot") return [
    field("trigger", { label: "Trigger", type: "select", options: ["CohesionBreak", "Hit", "Death"], group: "reaction" }),
    field("pressureBand", { label: "Pressure Band", type: "select", options: ["", "Stable", "Pressed", "Cracked", "Critical", "Broken"], group: "reaction" }),
    field("actionId", { label: "Generated Action ID", group: "reaction" }),
    integerField("durationFrames", "Duration", { min: 1, max: 240, group: "reaction" }),
    integerField("priority", "Priority", { min: 0, max: 1000, group: "reaction" }),
    field("resourceKey", { label: "Reaction animation", type: "select", options: animationResourceOptions("None"), group: "selection", picker: "resource" })
  ];
  if (kind === "body") return [
    positiveField("heightMeters", "参考身高", { min: 0.01, max: 5, step: 0.01, unit: "m", group: "bodyDimensions", fallback: 1.8,
      help: "标尺使用此高度。当前游戏优先使用默认胶囊高度；没有有效胶囊高度时使用此值。" }),
    positiveField("radiusMeters", "参考半径", { min: 0.01, max: 2, step: 0.01, unit: "m", group: "bodyDimensions", fallback: 0.35,
      help: "地面参考圆使用此半径。当前游戏优先使用默认胶囊半径；没有有效胶囊半径时使用此值。" }),
    positiveField("massKg", "质量", { min: 0, max: 500, step: 0.1, unit: "kg", group: "bodyDimensions", fallback: 75,
      help: "角色生成时使用的物理质量，不改变模型外观。0 表示使用游戏默认质量。" }),
    field("bodyKind", { label: "身体类型", type: "select", group: "bodyStructure", options: [
      { value: "Unknown", label: "未指定" }, { value: "Skeletal", label: "骨骼角色" },
      { value: "Primitive", label: "基础几何体" }, { value: "Compound", label: "组合身体" }
    ], help: "描述身体结构；切换类型不会自动替换模型或生成受击体。" }),
    field("bodyScale", { label: "身体倍率（未启用）", type: "number", unit: "×", group: "bodyStructure", readOnly: true,
      help: "保留已有配置值；当前游戏与预览均未使用此倍率缩放模型或碰撞体。" }),
    field("profileId", { label: "身体配置 ID", group: "bodyStructure", readOnly: true, help: "用于引用这份身体配置的固定标识。" }),
    positiveField("defaultCapsule.height", "配置高度", { min: 0.01, max: 5, step: 0.001, unit: "m", group: "movementDimensions", fallback: 1.8,
      help: "用于构造移动代理与注册体。游戏按毫米处理，解析后的尺寸见上方站立参数。" }),
    positiveField("defaultCapsule.radius", "配置水平半径", { min: 0.01, max: 2, step: 0.001, unit: "m", group: "movementDimensions", fallback: 0.35,
      help: "决定胶囊代理半径，也决定注册盒体的水平范围。默认站立半径不会超过高度的一半。" }),
    field("defaultCapsule.center.y", { label: "配置中心高度", type: "number", min: -10, max: 10, step: 0.001, unit: "m", group: "movementCenter", fallback: 0.9,
      help: "0 或负数先取高度的一半；游戏再把中心限制在有效范围。代理与注册体使用同一中心高度。" }),
    field("defaultCapsule.center.x", { label: "源中心 X（当前未使用）", type: "number", unit: "m", group: "movementSource", readOnly: true,
      help: "仅保留源配置。游戏移动边界的水平中心 X 为 0。" }),
    field("defaultCapsule.center.z", { label: "源中心 Z（当前未使用）", type: "number", unit: "m", group: "movementSource", readOnly: true,
      help: "仅保留源配置。游戏移动边界的水平中心 Z 为 0。" }),
    field("defaultPhysicsProfileId", { label: "物理配置引用", group: "bodyBindings" }),
    field("modelRootStableId", { label: "模型根引用", group: "bodyBindings" }),
    field("skeletonRootStableId", { label: "骨骼根引用", group: "bodyBindings" }),
    field("locatorRootStableId", { label: "定位点根引用", group: "bodyBindings" })
  ];
  if (kind === "animationSlot") return [
    field("animationGroupId", { label: "Animation Group", type: "select", options: animationGroupOptions("None"), group: "selection" }),
    field("resourceKey", { label: "Animation resource", type: "select", options: animationResourceOptions("None"), group: "selection", picker: "resource" })
  ];
  if (kind === "part") return [
    identityField("partId", "Body Part ID", bodyPartIdSuggestions(), "Stable body part id."),
    field("displayName", { label: "Display Name", group: "base" }),
    field("partKind", { label: "Part Kind", type: "select", options: BODY_PART_KIND_OPTIONS, group: "binding" }),
    field("parentPartId", { label: "Parent Part", type: "select", options: bodyPartOptions("None"), group: "binding" }),
    bonePathField("bonePath", "Bone Path", "Bound skeleton bone path."),
    locatorField("locatorId", "Locator", "Optional locator id."),
    hitZoneField("defaultHitZoneId", "Default Hit Zone", "Default hit zone for this part.", "combat", "None"),
    reactionGroupField("reactionGroupId", "Reaction Group", "Reaction group id.", "combat", "None"),
    tagsField("tags", "Tags", tagOptions(), "Optional tags.")
  ];
  if (kind === "collider") return [
    identityField("colliderId", "Collider ID", colliderIdSuggestions(value), "Stable collider id."),
    field("shape", { label: "Shape", type: "select", options: ["Capsule", "Box", "Sphere"], group: "base" }),
    field("partId", { label: "Body Part", type: "select", options: bodyPartOptions(), group: "binding" }),
    hitZoneField("hitZoneId", "Hit Zone", "Hit zone id.", "binding"),
    poseParentKindField("localPose.parentKind"),
    poseParentPathField("localPose.parentPath", "Parent Path", value?.localPose?.parentKind),
    positionField("localPose.position.x", "Position X", "localPose"),
    positionField("localPose.position.y", "Position Y", "localPose"),
    positionField("localPose.position.z", "Position Z", "localPose"),
    rotationField("localPose.eulerHint.x", "Rotation X"),
    rotationField("localPose.eulerHint.y", "Rotation Y"),
    rotationField("localPose.eulerHint.z", "Rotation Z"),
    ...colliderShapeFields(value),
    integerField("priority", "Priority", { min: 0, max: 1000, group: "base" }),
    field("isWeakPoint", { label: "Weak Point", type: "select", options: [{ value: "false", label: "No" }, { value: "true", label: "Yes" }], dataType: "boolean", group: "base" }),
    positiveField("damageMultiplierOverride", "Damage Multiplier", { max: 100, step: 0.01, group: "combat", fallback: 1 }),
    positiveField("impactPowerScaleOverride", "Impact Power Scale", { max: 100, step: 0.01, group: "combat", fallback: 1 }),
    field("materialStableId", { label: "Surface Material", group: "combat", placeholder: "material.flesh" }),
    field("physicsLayer", { label: "Physics Layer", type: "select", options: ["CharacterHurtbox"], group: "combat" })
  ];
  if (kind === "bodyPartDamageDefinition") return [
    identityField("partStateId", "Part State ID", ["partstate.head", "partstate.left_wing", "partstate.right_wing", "partstate.tail"], "稳定身份；正式保存按此字段规范化排序。"),
    tagsField("memberPartIds", "Member BodyParts", bodyPartDamageMemberOptions(value), "只映射现有 BodyPart；已被其它 part state 占用的成员不可重复选择。", "binding"),
    field("damageZoneId", { label: "Damage Zone", type: "select", options: bodyPartDamageZoneOptions(), group: "binding", help: "可为空；只引用现有 DamageZone，不会隐式创建。" }),
    field("woundCapacity", { label: "Wound Capacity", type: "number", step: 1, group: "wound", help: "T1 validation：0..1,000,000；0 关闭伤口轴。" }),
    field("openDamageScaleMilli", { label: "Open Damage Scale", type: "number", step: 1, unit: "milli", group: "wound", help: "T1 validation：0..4,000；启用伤口轴时必须大于 0，1000 表示不变。" }),
    field("focusConsumeTag", { label: "Focus Consume Tag", group: "wound", placeholder: "action.focus.consume.*", help: "可为空；只由正式 typed action/ability/interaction fact 消费，damage tag 不触发。" }),
    field("maxIntegrity", { label: "Max Integrity", type: "number", step: 1, group: "structure", help: "T1 validation：0..1,000,000；0 关闭结构轴。" }),
    field("integrityDamageScaleMilli", { label: "Integrity Damage Scale", type: "number", step: 1, unit: "milli", group: "structure", help: "T1 validation：0..4,000；1000 表示按 final HP damage 等量累计。" }),
    field("breakReactionId", { label: "Break Reaction ID", group: "structure", help: "可为空；Runtime 不从字符串猜测行为。" }),
    field("severable", { label: "Severable", type: "select", options: [{ value: "false", label: "No" }, { value: "true", label: "Yes" }], dataType: "boolean", group: "severance", help: "启用固定策略：只有帧开始已 Broken 且收到匹配 typed fact 才能断肢。" }),
    field("severRequiredTag", { label: "Sever Required Tag", group: "severance", placeholder: "action.* / ability.* / interaction.*", help: "显式 typed authority tag；不从 damage tag 猜测。" }),
    field("presentationEventId", { label: "Presentation Event ID", group: "severance", placeholder: "event.*", help: "一次性 Severed presentation 事件身份。" }),
    field("authoredPriorityDeltaOpen", { label: "Priority Delta · Open", type: "number", step: 1, group: "selection", help: "T1 validation：-1000..1000；只进入 selector priority。" }),
    field("authoredPriorityDeltaBroken", { label: "Priority Delta · Broken", type: "number", step: 1, group: "selection", help: "T1 validation：-1000..1000；不改变 Hittable/Aimable/Approachable。" })
  ];
  if (kind === "bodyPresenceAnimatedSource") return [
    identityField("sourceId", "Source ID", ["presence.animated.head", "presence.animated.tail_mid", "presence.animated.tail_tip"], "稳定 source identity；正式保存按 sourceId 排序。"),
    field("colliderId", { label: "CharacterHurtbox Collider", type: "select", options: animatedHurtboxColliderOptions(), group: "binding", help: "只引用现有 Hurtbox；shape/part/bone/track 仍由 collider 权威持有。" }),
    integerField("skinMillimeters", "Soft Skin", { min: 0, max: 10000, group: "policy", unit: "mm" }),
    integerField("pressureScaleMilli", "Pressure Scale", { min: 0, max: 100000, group: "policy", unit: "milli" }),
    field("enabled", { label: "Enabled", type: "select", options: [{ value: "false", label: "No" }, { value: "true", label: "Yes" }], dataType: "boolean", group: "policy" })
  ];
  if (kind === "damageZone") return [
    identityField("zoneId", "Zone ID", ["zone.head", "zone.torso", "zone.limbs"], "Stable DamageZone identity."),
    field("displayName", { label: "Display Name", group: "base" }),
    tagsField("hitZoneIds", "Hit Zones", hitZoneOptions(), "Select one or more existing Hit Zones. A Hit Zone may belong to only one DamageZone.", "binding"),
    field("hittable", { label: "Hittable", type: "select", options: [{ value: "false", label: "No" }, { value: "true", label: "Yes" }], dataType: "boolean", group: "capability" }),
    field("aimable", { label: "Aimable", type: "select", options: [{ value: "false", label: "No" }, { value: "true", label: "Yes" }], dataType: "boolean", group: "capability" }),
    field("approachable", { label: "Approachable", type: "select", options: [{ value: "false", label: "No" }, { value: "true", label: "Yes" }], dataType: "boolean", group: "capability" }),
    integerField("authoredPriority", "Authored Priority", { min: -100000, max: 100000, group: "selection" })
  ];
  if (kind === "engagementAnchor") return [
    identityField("anchorId", "Anchor ID", ["anchor.front", "anchor.rear", "anchor.left", "anchor.right"], "Stable package-local engagement anchor id."),
    field("zoneId", { label: "Damage Zone", type: "select", options: approachableDamageZoneOptions(), group: "binding", help: "Only DamageZones with approachable=true are valid." }),
    field("referenceFrame", { label: "Reference Frame", type: "select", options: ["ModelRoot"], group: "binding", help: "v1 accepts ModelRoot only." }),
    integerField("localPositionMillimeters.x", "Position X", { min: -10000000, max: 10000000, group: "pose", unit: "mm" }),
    integerField("localPositionMillimeters.y", "Position Y", { min: -10000000, max: 10000000, group: "pose", unit: "mm" }),
    integerField("localPositionMillimeters.z", "Position Z", { min: -10000000, max: 10000000, group: "pose", unit: "mm" }),
    integerField("yawMilliDegrees", "Yaw", { min: 0, max: 359999, group: "pose", unit: "mdeg" }),
    integerField("capacity", "Capacity", { min: 1, max: 64, group: "selection" }),
    integerField("authoredPriority", "Authored Priority", { min: -100000, max: 100000, group: "selection" }),
    field("enabled", { label: "Enabled", type: "select", options: [{ value: "false", label: "No" }, { value: "true", label: "Yes" }], dataType: "boolean", group: "selection" }),
    field("phaseId", { label: "Phase ID", suggestions: ["phase.default"], group: "selection", help: "phase.default is the current runtime phase; other values remain fail-closed until a phase authority is introduced." })
  ];
  if (kind === "contactPoint") return [
    identityField("stableId", "Stable ID", contactPointStableIdSuggestions(value), "Long-term stable authoring id."),
    integerField("contactPointId", "Contact Point ID", { min: 1, max: 1000000, group: "binding" }),
    integerField("bodyPartId", "Body Part Numeric ID", { min: 1, max: 1000000, group: "binding" }),
    field("partId", { label: "Body Part", type: "select", options: bodyPartOptions(), group: "binding" }),
    positionField("localOffsetMeters.x", "Offset X", "contact"),
    positionField("localOffsetMeters.y", "Offset Y", "contact"),
    positionField("localOffsetMeters.z", "Offset Z", "contact"),
    positiveField("radiusMeters", "Radius", { min: 0.001, max: 2, step: 0.01, unit: "m", group: "contact", fallback: 0.09 }),
    field("layerMaskProfileId", { label: "Layer Mask Profile", suggestions: contactLayerMaskOptions(), group: "contact", placeholder: "ground" }),
    field("usage", { label: "Usage", group: "contact", help: "PresentationAudio: this point drives view-only contact sound." }),
    field("authority", { label: "Authority", group: "contact", help: "PresentationOnly: not Combat authority, replay hash, save, or network state." }),
    field("activationMode", { label: "Activation Mode", type: "select", options: CONTACT_ACTIVATION_MODE_OPTIONS, group: "contact" }),
    field("motionMode", { label: "Motion Mode", type: "select", options: CONTACT_MOTION_MODE_OPTIONS, group: "contact" }),
    field("motionSocketId", { label: "Motion Socket", type: "select", options: socketOptions("Static offset"), group: "contact", help: "Bind to an animated socket/bone-follow target when the contact point should follow a body part." }),
    integerField("sourceOrder", "来源顺序", { min: 0, max: 1000000, group: "contact" })
  ];
  if (kind === "socket") return [
    identityField("socketId", "Socket ID", socketIdSuggestions(), "Stable socket id."),
    field("abstractBoneId", {
      label: "Abstract Bone",
      group: "identity",
      suggestions: ABSTRACT_BONE_ID_OPTIONS,
      help: "Assign one core semantic or ext.<packageId>.<name>. Each semantic must be unique; head is required for every spawnable character."
    }),
    field("parentPartId", { label: "Parent Part", type: "select", options: bodyPartOptions("None"), group: "binding" }),
    bonePathField("bonePath", "Bone Path", "Bone path used by this socket."),
    locatorField("locatorPath", "Locator Path", "Optional locator path."),
    poseParentKindField("localPose.parentKind"),
    poseParentPathField("localPose.parentPath", "Parent Path", value?.localPose?.parentKind),
    positionField("localPose.position.x", "Position X", "localPose"),
    positionField("localPose.position.y", "Position Y", "localPose"),
    positionField("localPose.position.z", "Position Z", "localPose"),
    rotationField("localPose.eulerHint.x", "Rotation X"),
    rotationField("localPose.eulerHint.y", "Rotation Y"),
    rotationField("localPose.eulerHint.z", "Rotation Z"),
    field("usage", { label: "Usage", type: "select", options: SOCKET_USAGE_OPTIONS, group: "usage" }),
    field("mirrorPairSocketId", { label: "Mirror Socket", type: "select", options: socketOptions("None"), group: "usage" }),
    field("handedness", { label: "Handedness", type: "select", options: SOCKET_HANDEDNESS_OPTIONS, group: "usage" }),
    field("sideTag", { label: "Side", type: "select", options: SOCKET_SIDE_OPTIONS, group: "usage" }),
    tagsField("tags", "Tags", tagOptions(), "Optional tags.")
  ];
  if (kind === "weapon") return withWeaponModelWrapperFields(value, [
    identityField("attachmentId", "Attachment ID", attachmentIdSuggestions(), "角色本地挂接的稳定 ID。"),
    field("singleWeaponConfigurationId", { label: "单一武器配置", type: "select", options: singleWeaponConfigurationOptions(), group: "base" }),
    field("slotId", { label: "挂接槽位", type: "select", options: EQUIP_SLOT_OPTIONS, group: "base" }),
    field("attachSocketId", { label: "Attach Socket", type: "select", options: socketOptions(), group: "binding" }),
    poseParentKindField("localGripPose.parentKind"),
    poseParentPathField("localGripPose.parentPath", "Parent Path", value?.localGripPose?.parentKind),
    positionField("localGripPose.position.x", "Grip Position X", "localPose"),
    positionField("localGripPose.position.y", "Grip Position Y", "localPose"),
    positionField("localGripPose.position.z", "Grip Position Z", "localPose"),
    rotationField("localGripPose.eulerHint.x", "Grip Rotation X"),
    rotationField("localGripPose.eulerHint.y", "Grip Rotation Y"),
    rotationField("localGripPose.eulerHint.z", "Grip Rotation Z"),
    field("traceId", { label: "Trace ID", type: "select", options: traceOptions("None"), group: "trace" }),
    field("traceStartSocketId", { label: "Trace Start Socket", type: "select", options: socketOptions("Use attach socket"), group: "trace" }),
    field("traceEndSocketId", { label: "Trace End Socket", type: "select", options: socketOptions("None"), group: "trace" }),
    positiveField("traceRadius", "Trace Radius", { max: 5, step: 0.01, unit: "m", group: "trace" }),
    field("traceSampleRule", { label: "Trace Sample Rule", type: "select", options: TRACE_SAMPLE_RULE_OPTIONS, group: "trace" })
  ]);
  if (kind === "trace") return [
    identityField("traceId", "Trace ID", traceIdSuggestions(), "Stable attack trace id."),
    field("weaponId", { label: "Weapon ID", type: "select", options: weaponOptions(), group: "base" }),
    field("equipSlot", { label: "Equip Slot", type: "select", options: EQUIP_SLOT_OPTIONS, group: "base" }),
    locatorField("startLocatorPath", "Start Locator", "Optional start locator."),
    locatorField("endLocatorPath", "End Locator", "Optional end locator."),
    poseParentKindField("startPose.parentKind", "Start Parent Kind"),
    poseParentPathField("startPose.parentPath", "Start Parent Path", value?.startPose?.parentKind),
    positionField("startPose.position.x", "Start X", "localPose"),
    positionField("startPose.position.y", "Start Y", "localPose"),
    positionField("startPose.position.z", "Start Z", "localPose"),
    rotationField("startPose.eulerHint.x", "Start Rotation X"),
    rotationField("startPose.eulerHint.y", "Start Rotation Y"),
    rotationField("startPose.eulerHint.z", "Start Rotation Z"),
    poseParentKindField("endPose.parentKind", "End Parent Kind"),
    poseParentPathField("endPose.parentPath", "End Parent Path", value?.endPose?.parentKind),
    positionField("endPose.position.x", "End X", "localPose"),
    positionField("endPose.position.y", "End Y", "localPose"),
    positionField("endPose.position.z", "End Z", "localPose"),
    rotationField("endPose.eulerHint.x", "End Rotation X"),
    rotationField("endPose.eulerHint.y", "End Rotation Y"),
    rotationField("endPose.eulerHint.z", "End Rotation Z"),
    positiveField("radius", "Trace Radius", { max: 5, step: 0.01, unit: "m", group: "trace" }),
    field("sampleRule", { label: "Sample Rule", type: "select", options: TRACE_SAMPLE_RULE_OPTIONS, group: "trace" }),
    integerField("fixedSampleCount", "Fixed Sample Count", { min: 1, max: 64, group: "trace" }),
    tagsField("actionKeys", "Action Keys", actionKeyOptions(), "Action keys that use this trace.")
  ];
  return [];
}

function withEmptyOption(options, label = "None") {
  return [{ value: "", label }, ...options];
}

function approachableDamageZoneOptions() {
  return getDamageZoneItems(state.package?.geometry || {})
    .filter(zone => zone?.zoneId && zone?.approachable)
    .map(zone => ({ value: zone.zoneId, label: zone.displayName ? `${zone.displayName} (${zone.zoneId})` : zone.zoneId }));
}

function bodyPartOptions(emptyLabel = "") {
  const options = (state.package?.geometry?.bodyParts || [])
    .filter(part => part?.partId)
    .map(part => ({ value: part.partId, label: part.displayName ? `${part.displayName} (${part.partId})` : part.partId }));
  return emptyLabel ? withEmptyOption(options, emptyLabel) : options;
}

function bodyPartDamageMemberOptions(currentDefinition = null) {
  const ownerByPartId = new Map();
  for (const definition of getBodyPartDamageItems()) {
    if (!definition || definition === currentDefinition) continue;
    for (const partId of definition.memberPartIds || []) {
      if (partId && !ownerByPartId.has(partId)) ownerByPartId.set(partId, definition.partStateId || "其它 part state");
    }
  }
  return bodyPartOptions().map(option => {
    const owner = ownerByPartId.get(option.value);
    return owner
      ? { ...option, label: `${option.label} · 已属于 ${owner}`, disabled: true }
      : option;
  });
}

function bodyPartDamageZoneOptions() {
  const zones = getDamageZoneItems(state.package?.geometry || {})
    .filter(zone => zone?.zoneId)
    .map(zone => ({
      value: zone.zoneId,
      label: zone.displayName ? `${zone.displayName} (${zone.zoneId})` : zone.zoneId
    }));
  return withEmptyOption(zones, "不投影 DamageZone");
}

function contactPointStableIdSuggestions(current = null) {
  const partId = current?.partId || firstValue(bodyPartOptions()) || "body";
  const base = `contact.${partId}.point`;
  const values = new Set([base, `contact.${partId}.sole`, `contact.${partId}.impact`]);
  for (const contactPoint of state.package?.geometry?.bodyPartContactPoints || []) {
    if (contactPoint?.stableId) values.add(contactPoint.stableId);
  }
  return Array.from(values).map(value => ({ value, label: value }));
}

function contactLayerMaskOptions() {
  return [
    { value: "ground", label: "ground" },
    { value: "world", label: "world" },
    { value: "environment", label: "environment" },
    { value: "character", label: "character" }
  ];
}

function socketOptions(emptyLabel = "") {
  const options = (state.package?.geometry?.sockets || [])
    .filter(socket => socket?.socketId)
    .map(socket => ({ value: socket.socketId, label: socket.socketId }));
  return emptyLabel ? withEmptyOption(options, emptyLabel) : options;
}

function abstractBoneOptions() {
  const values = new Set();
  for (const socket of state.package?.geometry?.sockets || []) {
    const id = String(socket?.abstractBoneId || "").trim();
    if (id) values.add(id);
  }
  return Array.from(values).sort().map(value => ({ value, label: value }));
}

function traceOptions(emptyLabel = "") {
  const options = (state.package?.geometry?.traces || [])
    .filter(trace => trace?.traceId)
    .map(trace => ({ value: trace.traceId, label: trace.traceId }));
  return emptyLabel ? withEmptyOption(options, emptyLabel) : options;
}

function modelResourceOptions(emptyLabel = "") {
  const options = getResourceLibraryItems()
    .filter(item => item.kind === "Model")
    .map(item => ({
      value: item.runtimeResourceKey || item.packageResourceKey || item.providerResourceKey || item.resourceKey,
      label: `${item.displayName} (${item.sourceProviderId})`
    }))
    .filter(option => option.value);
  return emptyLabel ? withEmptyOption(options, emptyLabel) : options;
}

function animationResourceOptions(emptyLabel = "") {
  const options = getResourceLibraryItems()
    .filter(item => item.kind === "Animation")
    .map(item => ({
      value: item.runtimeResourceKey || item.packageResourceKey || item.providerResourceKey || item.resourceKey || item.unityAssetPath,
      label: `${item.displayName} (${item.sourceProviderId})`
    }))
    .filter(option => option.value);
  return emptyLabel ? withEmptyOption(options, emptyLabel) : options;
}

function animationGroupOptions(emptyLabel = "") {
  const options = (state.package?.applicationConfig?.animationGroups || [])
    .filter(group => group?.groupId)
    .map(group => ({ value: group.groupId, label: group.displayName ? `${group.displayName} (${group.groupId})` : group.groupId }));
  return emptyLabel ? withEmptyOption(options, emptyLabel) : options;
}

function weaponOptions(emptyLabel = "") {
  const options = collectOptions(
    (state.weaponLibrary?.singleWeaponConfigurations || []).map(item => item?.singleWeaponConfigurationId),
    (state.package?.geometry?.traces || []).map(trace => trace?.weaponId)
  );
  return emptyLabel ? withEmptyOption(options, emptyLabel) : options;
}

function singleWeaponConfigurationOptions(emptyLabel = "") {
  const options = (state.weaponLibrary?.singleWeaponConfigurations || [])
    .filter(item => item?.singleWeaponConfigurationId && item.singleWeaponConfigurationId !== "weapon.unarmed")
    .map(item => ({
      value: item.singleWeaponConfigurationId,
      label: item.displayName
        ? `${item.displayName} (${item.singleWeaponConfigurationId})`
        : item.singleWeaponConfigurationId
    }));
  return emptyLabel ? withEmptyOption(options, emptyLabel) : options;
}

function attachmentIdSuggestions() {
  return collectOptions(
    (state.package?.geometry?.weaponAttachments || []).map(attachment => attachment?.attachmentId)
  );
}

function bodyPartIdSuggestions() {
  return collectOptions(COMMON_BODY_PART_IDS, (state.package?.geometry?.bodyParts || []).map(part => part?.partId));
}

function socketIdSuggestions() {
  return collectOptions(COMMON_SOCKET_IDS, (state.package?.geometry?.sockets || []).map(socket => socket?.socketId));
}

function weaponIdSuggestions() {
  return collectOptions(
    (state.package?.geometry?.weaponAttachments || []).map(attachment => attachment?.weaponId),
    (state.package?.geometry?.traces || []).map(trace => trace?.weaponId),
    getModelResources(state.package).filter(resource => resource.usage === "weaponModel").map(resource => resource.localId || resource.resourceKey)
  );
}

function traceIdSuggestions() {
  return collectOptions(
    (state.package?.geometry?.traces || []).map(trace => trace?.traceId),
    (state.package?.geometry?.weaponAttachments || []).map(attachment => attachment?.traceId)
  );
}

function colliderIdSuggestions(collider = {}) {
  const partIds = (state.package?.geometry?.bodyParts || []).map(part => part?.partId).filter(Boolean);
  const generated = partIds.flatMap(partId => ["Sphere", "Capsule", "Box"].map(shape => buildColliderAutoId(partId, shape)));
  return collectOptions(
    generated,
    (state.package?.geometry?.colliders || []).map(item => item?.colliderId),
    collider?.partId ? buildColliderAutoId(collider.partId, collider.shape || "Capsule") : ""
  );
}

function bonePathOptions() {
  return collectOptions(
    state.previewBones.flatMap(bone => [bone.path, bone.alias]),
    (state.package?.geometry?.bodyParts || []).map(part => part?.bonePath),
    (state.package?.geometry?.sockets || []).map(socket => socket?.bonePath),
    collectPoseParentPaths("Bone")
  );
}

function locatorPathOptions() {
  return collectOptions(
    (state.package?.geometry?.bodyParts || []).map(part => part?.locatorId),
    (state.package?.geometry?.sockets || []).flatMap(socket => [socket?.locatorPath]),
    (state.package?.geometry?.traces || []).flatMap(trace => [trace?.startLocatorPath, trace?.endLocatorPath]),
    collectPoseParentPaths("Locator")
  );
}

function hitZoneOptions() {
  return collectOptions(
    COMMON_HIT_ZONE_IDS,
    (state.package?.geometry?.bodyParts || []).map(part => part?.partId ? `hit.${part.partId}` : ""),
    (state.package?.geometry?.bodyParts || []).map(part => part?.defaultHitZoneId),
    (state.package?.geometry?.colliders || []).map(collider => collider?.hitZoneId)
  );
}

function reactionGroupOptions() {
  return collectOptions(
    COMMON_REACTION_GROUPS,
    (state.package?.geometry?.bodyParts || []).map(part => part?.reactionGroupId)
  );
}

function tagOptions() {
  return collectOptions(
    COMMON_TAGS,
    (state.package?.geometry?.bodyParts || []).flatMap(part => part?.tags || []),
    (state.package?.geometry?.sockets || []).flatMap(socket => socket?.tags || []),
    (state.package?.resourceCatalog?.entries || []).flatMap(resource => resource?.tags || [])
  );
}

function actionKeyOptions() {
  return collectOptions(COMMON_ACTION_KEYS, (state.package?.geometry?.traces || []).flatMap(trace => trace?.actionKeys || []));
}

function collectPoseParentPaths(parentKind) {
  const geometry = state.package?.geometry || {};
  const poses = [
    ...(geometry.colliders || []).map(item => item?.localPose),
    ...(geometry.sockets || []).map(item => item?.localPose),
    ...(geometry.weaponAttachments || []).map(item => item?.localGripPose),
    ...(geometry.traces || []).flatMap(item => [item?.startPose, item?.endPose])
  ];
  return poses
    .filter(pose => pose?.parentKind === parentKind)
    .map(pose => pose.parentPath);
}

function collectOptions(...sources) {
  const seen = new Set();
  const values = [];
  for (const source of sources) {
    const items = Array.isArray(source) ? source : [source];
    for (const item of items) {
      if (item == null) continue;
      const value = String(item).trim();
      if (!value || seen.has(value)) continue;
      seen.add(value);
      values.push({ value, label: value });
    }
  }
  values.sort((a, b) => a.label.localeCompare(b.label));
  return values;
}

function identityField(path, label, suggestions, help, group = "base") {
  return datalistField(path, label, suggestions, help, group);
}

function datalistField(path, label, suggestions, help, group = "base") {
  const first = normalizeOptions(suggestions)[0];
  return field(path, {
    label,
    group,
    suggestions,
    placeholder: first ? first.value : "",
    help
  });
}

function selectField(path, label, options, help, group = "base", emptyLabel = "") {
  const normalized = normalizeOptions(options);
  return field(path, {
    label,
    type: "select",
    options: emptyLabel ? withEmptyOption(normalized, emptyLabel) : normalized,
    group,
    help
  });
}

function hitZoneField(path, label, help, group = "combat", emptyLabel = "") {
  return selectField(path, label, hitZoneOptions(), help, group, emptyLabel);
}

function reactionGroupField(path, label, help, group = "combat", emptyLabel = "") {
  return selectField(path, label, reactionGroupOptions(), help, group, emptyLabel);
}

function bonePathField(path, label, help) {
  const spec = selectField(path, label, bonePathOptions(), help, "binding", "None");
  spec.picker = "bone";
  return spec;
}

function locatorField(path, label, help) {
  const spec = selectField(path, label, locatorPathOptions(), help, "binding", "None");
  return spec;
}

function positionField(path, label, group = "localPose") {
  return field(path, { label, type: "number", min: -10, max: 10, step: 0.01, unit: "m", group, fallback: 0 });
}

function modelPositionField(path, label) {
  return positionField(path, label, "modelTransform");
}

function modelRotationField(path, label) {
  return rotationField(path, label, "modelTransform");
}

function modelScaleField(path, label) {
  return field(path, { label, type: "number", min: 0.001, max: 100, step: 0.01, group: "modelTransform", fallback: 1 });
}

function withWeaponModelWrapperFields(weapon, fields) {
  const resource = ensureWeaponModelWrapperPose(weapon);
  if (!resource) return fields;
  return [
    ...fields,
    modelPositionField("importHints.modelWrapperPose.position.x", "Pos X"),
    modelPositionField("importHints.modelWrapperPose.position.y", "Pos Y"),
    modelPositionField("importHints.modelWrapperPose.position.z", "Pos Z"),
    modelRotationField("importHints.modelWrapperPose.eulerHint.x", "Rot X"),
    modelRotationField("importHints.modelWrapperPose.eulerHint.y", "Rot Y"),
    modelRotationField("importHints.modelWrapperPose.eulerHint.z", "Rot Z"),
    modelScaleField("importHints.modelWrapperPose.scale.x", "Scale X"),
    modelScaleField("importHints.modelWrapperPose.scale.y", "Scale Y"),
    modelScaleField("importHints.modelWrapperPose.scale.z", "Scale Z")
  ];
}

function rotationField(path, label, group = "localPose") {
  return field(path, { label, type: "number", min: -360, max: 360, step: 1, unit: "deg", group, fallback: 0 });
}

function localScaleField(path, label, group = "localPose") {
  return field(path, { label, type: "number", min: 0.001, max: 100, step: 0.01, group, fallback: 1 });
}

function poseParentKindField(path, label = "Parent Kind") {
  return field(path, {
    label,
    type: "select",
    options: POSE_PARENT_KIND_OPTIONS,
    group: "poseParent",
    help: "Parent coordinate space for this local pose."
  });
}

function poseParentPathField(path, label = "Parent Path", parentKind = "") {
  if (parentKind === "BodyPart") {
    return field(path, { label, type: "select", options: bodyPartOptions("None"), group: "poseParent", help: "Body part parent path." });
  }
  if (parentKind === "Socket") {
    return field(path, { label, type: "select", options: socketOptions("None"), group: "poseParent", help: "Socket parent path." });
  }
  if (parentKind === "Bone") {
    return field(path, { label, group: "poseParent", suggestions: bonePathOptions(), help: "Bone parent path.", picker: "bone" });
  }
  if (parentKind === "Locator") {
    return field(path, { label, group: "poseParent", suggestions: locatorPathOptions(), help: "Locator parent path." });
  }
  return field(path, { label, group: "poseParent", help: "ModelRoot / SkeletonRoot / WorldPreview 的父路径通常为空。" });
}

function mergeFieldHelp(primary, guidance) {
  const text = String(primary || "").trim();
  if (!text) return guidance;
  return `${text} ${guidance}`;
}

function field(path, options = {}) {
  return {
    path,
    type: options.type || "text",
    dataType: options.dataType || options.type || "text",
    resourceFieldRole: options.resourceFieldRole || "",
    label: options.label || path,
    options: options.options || null,
    suggestions: options.suggestions || null,
    group: options.group || "base",
    min: options.min,
    max: options.max,
    step: options.step,
    unit: options.unit || "",
    fallback: options.fallback,
    placeholder: options.placeholder || "",
    help: options.help || "",
    readOnly: Boolean(options.readOnly),
    picker: options.picker || "",
    pathFieldRole: options.pathFieldRole || ""
  };
}

function tagsField(path, label, options = tagOptions(), help = "", group = "base") {
  return field(path, { label, type: "multiSelect", dataType: "stringList", options, group, help });
}

function sizeField(path, label) {
  return positiveField(path, label, { max: 10, step: 0.01, unit: "m", group: "shape" });
}

function colliderShapeFields(collider = {}) {
  const shape = String(collider?.shape || "Capsule");
  if (shape === "Box") {
    return [
      sizeField("size.x", "Size X"),
      sizeField("size.y", "Size Y"),
      sizeField("size.z", "Size Z")
    ];
  }
  if (shape === "Sphere") {
    return [
      positiveField("radius", "Radius", { max: 10, step: 0.01, unit: "m", group: "shape" })
    ];
  }
  return [
    positiveField("radius", "Radius", { max: 10, step: 0.01, unit: "m", group: "shape" }),
    positiveField("height", "Height", { max: 10, step: 0.01, unit: "m", group: "shape" })
  ];
}

function positiveField(path, label, options = {}) {
  return field(path, {
    label,
    type: "number",
    min: options.min ?? 0,
    max: options.max ?? 100,
    step: options.step ?? 0.01,
    unit: options.unit || "",
    help: options.help || "",
    group: options.group || "base",
    fallback: options.fallback ?? 0
  });
}

function integerField(path, label, options = {}) {
  return field(path, {
    label,
    type: "number",
    min: options.min ?? 0,
    max: options.max ?? 1000,
    step: 1,
    group: options.group || "base",
    unit: options.unit || "",
    help: options.help || "",
    fallback: options.fallback ?? 0
  });
}

function renderFieldSections(target, fields) {
  const groups = [];
  for (const fieldSpec of fields) {
    const spec = normalizeFieldSpec(fieldSpec);
    let group = groups.find(item => item.key === spec.group);
    if (!group) {
      group = { key: spec.group, fields: [] };
      groups.push(group);
    }
    group.fields.push(spec);
  }

  return groups.map(group => {
    const heading = renderFieldSectionHeading(target, group.key);
    if (target.kind === "body" && group.key === "bodyBindings") {
      return `<details class="body-advanced-bindings" data-body-advanced-bindings${state.bodyAdvancedBindingsOpen ? " open" : ""}><summary>高级绑定</summary><p class="field-help">模型、骨骼与物理配置的现有引用。</p><div class="field-grid">${group.fields.map(fieldSpec => renderField(target, fieldSpec)).join("")}</div></details>`;
    }
    if (target.kind === "body" && group.key === "movementSource") {
      return `<details class="body-advanced-bindings" data-movement-source-details${state.movementSourceDetailsOpen ? " open" : ""}><summary>高级源信息</summary><p class="field-help">源偏移保留用于诊断，不改变当前游戏的移动中心。非零偏移的原有校验警告继续保留。</p><div class="field-grid">${group.fields.map(fieldSpec => renderField(target, fieldSpec)).join("")}</div></details>`;
    }
    if (["part", "collider"].includes(target.kind) && ["bodyPartAdvanced", "hurtboxAdvanced"].includes(group.key)) {
      return `<details class="body-advanced-bindings" data-body-part-advanced${state.bodyPartAdvancedOpen ? " open" : ""}><summary>高级信息</summary><div class="field-grid">${group.fields.map(fieldSpec => renderField(target, fieldSpec)).join("")}</div></details>`;
    }
    const description = target.kind === "body" && group.key === "bodyDimensions"
      ? '<p class="field-help">参考尺寸用于对照模型，不能作为模型的等比例缩放控制。移动代理与注册体在“移动碰撞配置”中查看。</p>' : "";
    return `<section class="field-section field-section-${escapeHtml(safeDomId(group.key))}">${heading}${description}<div class="field-grid">${group.fields.map(fieldSpec => renderField(target, fieldSpec)).join("")}</div></section>`;
  }).join("");
}

function renderFieldSectionHeading(target, groupKey) {
  const bodyLabels = { bodyDimensions: "尺寸与质量", bodyStructure: "身体结构", movementDimensions: "尺寸", movementCenter: "中心高度" };
  const partLabels = { bodyPartInfo: "部位信息", bodyPartBinding: "部位层级与骨骼绑定", bodyPartRules: "默认受击规则",
    hurtboxBinding: "所属部位与空间绑定", hurtboxShape: "形状尺寸", hurtboxTransform: "局部位置与旋转", hurtboxRules: "受击规则" };
  const label = (target.kind === "body" && bodyLabels[groupKey]) || (["part", "collider"].includes(target.kind) && partLabels[groupKey]) || FIELD_GROUP_LABELS[groupKey] || groupKey || "属性";
  const canResetModelPose = target.kind === "resource" && target.value?.typeId === "model" && groupKey === "modelTransform";
  const alignGround = target.kind === "body" && groupKey === "movementCenter";
  const aligned = alignGround && Math.abs(globalThis.getMovementCollisionPreview?.().effective.bottom || 0) < 0.0000001;
  const action = alignGround
    ? `<button type="button" data-movement-align-ground${!state.canWrite || aligned ? " disabled" : ""}>${aligned ? "底部已对齐地面" : "底部对齐地面"}</button>`
    : canResetModelPose
    ? `<button type="button" data-inspector-action="resetModelWrapperPose" title="Reset model wrapper transform">Reset</button>`
    : "";
  return `<div class="field-section-head"><h3>${escapeHtml(label)}</h3>${action}</div>`;
}

function renderFieldLegacy(target, fieldSpec) {
  const spec = normalizeFieldSpec(fieldSpec);
  const fieldTarget = getInspectorInputTarget(target, spec.path);
  const value = getNested(fieldTarget.value, spec.path);
  const label = spec.unit ? `${spec.label} (${spec.unit})` : spec.label;
  if (spec.type === "select") {
    const normalized = typeof value === "boolean" ? String(value) : (value || "");
    const options = spec.options || [];
    const select = `<select data-field="${escapeHtml(spec.path)}" data-type="${escapeHtml(spec.dataType)}"${renderPickerAttribute(spec)}${renderTitleAttribute(spec)}>${options.map(option => renderSelectOption(option, normalized)).join("")}</select>`;
    if (spec.picker === "resource") {
      const picker = `<button type="button" class="picker-button" data-picker-action="openResourcePicker" data-picker-field="${escapeHtml(spec.path)}">选择</button>`;
      return `<div class="field field-wide"><label>${escapeHtml(label)}</label><div class="field-control">${select}${picker}</div>${renderFieldHint(spec)}</div>`;
    }
    return `<div class="field"><label>${escapeHtml(label)}</label>${select}${renderFieldHint(spec)}</div>`;
  }
  if (spec.type === "multiSelect") {
    const selected = new Set(Array.isArray(value) ? value.map(item => String(item)) : String(value || "").split(",").map(item => item.trim()).filter(Boolean));
    const options = normalizeOptions(spec.options || []);
    return `<div class="field field-wide"><label>${escapeHtml(label)}</label><div class="choice-list">${options.map(option => renderCheckboxOption(spec, option, selected)).join("")}</div>${renderFieldHint(spec)}</div>`;
  }
  const inputType = spec.type === "number" ? "number" : "text";
  const datalistId = spec.suggestions ? `list-${safeDomId(spec.path)}` : "";
  const attrs = [
    `type="${escapeHtml(inputType)}"`,
    `data-field="${escapeHtml(spec.path)}"`,
    `data-type="${escapeHtml(spec.dataType)}"`,
    `data-fallback="${escapeHtml(String(spec.fallback ?? (spec.type === "number" ? 0 : "")))}"`
  ];
  const picker = renderPickerAttribute(spec);
  if (picker) attrs.push(picker);
  const title = renderTitleAttribute(spec);
  if (title) attrs.push(title);
  if (spec.placeholder) attrs.push(`placeholder="${escapeHtml(spec.placeholder)}"`);
  if (datalistId) attrs.push(`list="${escapeHtml(datalistId)}"`);
  if (spec.min !== undefined) attrs.push(`min="${escapeHtml(String(spec.min))}"`, `data-min="${escapeHtml(String(spec.min))}"`);
  if (spec.max !== undefined) attrs.push(`max="${escapeHtml(String(spec.max))}"`, `data-max="${escapeHtml(String(spec.max))}"`);
  if (spec.step !== undefined) attrs.push(`step="${escapeHtml(String(spec.step))}"`);
  if (spec.type === "number") attrs.push(`inputmode="decimal"`);
  const displayValue = formatFieldValue(value, spec.dataType);
  if (spec.picker === "bone") {
    const isOpen = state.bonePickerOpen && state.activeBoneFieldPath === spec.path;
    const className = isOpen ? "field field-wide" : "field";
    const picker = `<button type="button" class="picker-button" data-picker-action="openBonePicker" data-picker-field="${escapeHtml(spec.path)}">Bone</button>`;
    return `<div class="${className}"><label>${escapeHtml(label)}</label><div class="field-control"><input ${attrs.join(" ")} value="${escapeHtml(displayValue)}">${picker}</div>${renderDatalist(datalistId, spec.suggestions)}${renderFieldHint(spec)}${isOpen ? renderBonePicker() : ""}</div>`;
  }
  return `<div class="field"><label>${escapeHtml(label)}</label><input ${attrs.join(" ")} value="${escapeHtml(displayValue)}">${renderDatalist(datalistId, spec.suggestions)}${renderFieldHint(spec)}</div>`;
}

function normalizeFieldSpec(fieldSpec) {
  if (!Array.isArray(fieldSpec)) return fieldSpec;
  const [path, type = "text", options = null] = fieldSpec;
  return field(path, {
    type,
    dataType: path === "isWeakPoint" ? "boolean" : type,
    options,
    label: path
  });
}

function renderSelectOption(option, normalizedValue) {
  const normalized = typeof option === "object" && option !== null
    ? { value: option.value, label: option.label || option.value, disabled: Boolean(option.disabled) }
    : { value: option, label: option, disabled: false };
  return `<option value="${escapeHtml(normalized.value)}"${String(normalized.value) === String(normalizedValue) ? " selected" : ""}${normalized.disabled ? " disabled" : ""}>${escapeHtml(normalized.label)}</option>`;
}

function renderCheckboxOption(spec, option, selected) {
  const value = String(option.value ?? "");
  const id = `choice-${safeDomId(spec.path)}-${safeDomId(value)}`;
  return `<label class="choice${option.disabled ? " is-disabled" : ""}" for="${escapeHtml(id)}"><input id="${escapeHtml(id)}" type="checkbox" value="${escapeHtml(value)}" data-field="${escapeHtml(spec.path)}" data-type="${escapeHtml(spec.dataType)}"${selected.has(value) ? " checked" : ""}${option.disabled ? " disabled" : ""}><span>${escapeHtml(option.label || value)}</span></label>`;
}

function renderDatalist(id, suggestions) {
  if (!id || !suggestions) return "";
  const options = normalizeOptions(suggestions);
  if (options.length === 0) return "";
  return `<datalist id="${escapeHtml(id)}">${options.map(option => `<option value="${escapeHtml(option.value)}">${escapeHtml(option.label || option.value)}</option>`).join("")}</datalist>`;
}

function renderFieldHint(spec) {
  return spec.help ? `<span class="field-help">${escapeHtml(spec.help)}</span>` : "";
}

function renderTitleAttribute(spec) {
  return spec.help ? ` title="${escapeHtml(spec.help)}"` : "";
}

function renderPickerAttribute(spec) {
  return spec.picker ? ` data-picker="${escapeHtml(spec.picker)}"` : "";
}

function normalizeOptions(options) {
  return (options || []).map(option => typeof option === "object" && option !== null
    ? {
        value: String(option.value ?? ""),
        label: String(option.label || option.value || ""),
        disabled: Boolean(option.disabled)
      }
    : { value: String(option ?? ""), label: String(option ?? ""), disabled: false });
}

function safeDomId(value) {
  return String(value || "field").replace(/[^a-zA-Z0-9_-]+/g, "-");
}

function isPathFieldSpec(spec) {
  if (!spec) return false;
  if (spec.pathFieldRole) return true;
  return /(bonePath|locatorPath|startLocatorPath|endLocatorPath|\.parentPath)$/i.test(String(spec.path || ""));
}

function getPathFieldGuidance(spec) {
  const role = String(spec.pathFieldRole || "");
  if (globalThis.getBodyWorkspaceSection?.() === "parts") {
    if (role === "bone") return "选择实际跟随的骨骼路径。";
    if (role === "locator") return "选择模型定位点路径。";
    if (role === "poseParent") return "跟随目标按所选空间解释：骨骼路径、定位点、部位 ID 或挂点 ID。";
  }
  if (role === "bone") return "Bone Path: use skeleton bone paths for stable rig-follow behavior.";
  if (role === "locator") return "Locator Path: use model-imported locator markers when bone binding is unavailable.";
  if (role === "poseParent") return "Parent Path follows Parent Kind: Bone=bone path, Locator=locator path, BodyPart=part id, Socket=socket id.";
  return "Long reference path. Verify the full value before saving.";
}

function renderPathFieldViewer(spec, value) {
  if (!isPathFieldSpec(spec)) return "";
  const raw = String(value ?? "").trim();
  const display = raw || "(empty)";
  const guidance = getPathFieldGuidance(spec);
  return `<div class="path-value-view" data-path-view="${escapeHtml(spec.path)}">
    <code title="${escapeHtml(display)}">${escapeHtml(display)}</code>
    <button type="button" class="path-copy-button" data-copy-path-field="${escapeHtml(spec.path)}">复制</button>
  </div>
  <span class="path-field-guidance">${escapeHtml(guidance)}</span>`;
}

function renderField(target, fieldSpec) {
  const spec = normalizeFieldSpec(fieldSpec);
  const bonePickerLabel = ["part", "collider"].includes(target.kind) ? "选骨骼" : "Bone";
  const fieldTarget = getInspectorInputTarget(target, spec.path);
  const value = getNested(fieldTarget.value, spec.path);
  const inlineUnit = ["baseAttributes", "body", "part", "collider"].includes(target.kind) ? spec.unit : "";
  const label = spec.unit && !inlineUnit ? `${spec.label} (${spec.unit})` : spec.label;
  if (spec.readOnly) {
    return `<div class="field field-wide"><label>${escapeHtml(label)}</label><output class="body-readonly-value">${escapeHtml(formatFieldValue(value, spec.dataType) || spec.emptyText || "")}${inlineUnit ? `<span class="attribute-unit">${escapeHtml(inlineUnit)}</span>` : ""}</output>${renderFieldHint(spec)}</div>`;
  }
  if (spec.type === "select") {
    const normalized = typeof value === "boolean" ? String(value) : (value || "");
    const options = spec.options || [];
    const select = `<select data-field="${escapeHtml(spec.path)}" data-type="${escapeHtml(spec.dataType)}"${renderPickerAttribute(spec)}${renderTitleAttribute(spec)}>${options.map(option => renderSelectOption(option, normalized)).join("")}</select>`;
    const pathViewer = renderPathFieldViewer(spec, normalized);
    if (spec.picker === "resource") {
      const picker = `<button type="button" class="picker-button" data-picker-action="openResourcePicker" data-picker-field="${escapeHtml(spec.path)}">选择</button>`;
      return `<div class="field field-wide"><label>${escapeHtml(label)}</label><div class="field-control">${select}${picker}</div>${renderFieldHint(spec)}${pathViewer}</div>`;
    }
    if (spec.picker === "bone") {
      const isOpen = state.bonePickerOpen && state.activeBoneFieldPath === spec.path;
      const className = isOpen ? "field field-wide" : "field";
      const picker = `<button type="button" class="picker-button" data-picker-action="openBonePicker" data-picker-field="${escapeHtml(spec.path)}">${bonePickerLabel}</button>`;
      return `<div class="${className}"><label>${escapeHtml(label)}</label><div class="field-control">${select}${picker}</div>${renderFieldHint(spec)}${pathViewer}${isOpen ? renderBonePicker() : ""}</div>`;
    }
    return `<div class="field"><label>${escapeHtml(label)}</label>${select}${renderFieldHint(spec)}${pathViewer}</div>`;
  }
  if (spec.type === "multiSelect") {
    const selected = new Set(Array.isArray(value) ? value.map(item => String(item)) : String(value || "").split(",").map(item => item.trim()).filter(Boolean));
    const options = normalizeOptions(spec.options || []);
    return `<div class="field field-wide"><label>${escapeHtml(label)}</label><div class="choice-list">${options.map(option => renderCheckboxOption(spec, option, selected)).join("")}</div>${renderFieldHint(spec)}</div>`;
  }
  const inputType = spec.type === "number" ? "number" : "text";
  const datalistId = spec.suggestions ? `list-${safeDomId(spec.path)}` : "";
  const attrs = [
    `type="${escapeHtml(inputType)}"`,
    `data-field="${escapeHtml(spec.path)}"`,
    `data-type="${escapeHtml(spec.dataType)}"`,
    `data-fallback="${escapeHtml(String(spec.fallback ?? (spec.type === "number" ? 0 : "")))}"`
  ];
  const picker = renderPickerAttribute(spec);
  if (picker) attrs.push(picker);
  const title = renderTitleAttribute(spec);
  if (title) attrs.push(title);
  if (spec.placeholder) attrs.push(`placeholder="${escapeHtml(spec.placeholder)}"`);
  if (datalistId) attrs.push(`list="${escapeHtml(datalistId)}"`);
  if (spec.min !== undefined) attrs.push(`min="${escapeHtml(String(spec.min))}"`, `data-min="${escapeHtml(String(spec.min))}"`);
  if (spec.max !== undefined) attrs.push(`max="${escapeHtml(String(spec.max))}"`, `data-max="${escapeHtml(String(spec.max))}"`);
  if (spec.step !== undefined) attrs.push(`step="${escapeHtml(String(spec.step))}"`);
  if (spec.type === "number") attrs.push(`inputmode="decimal"`);
  const displayValue = formatFieldValue(value, spec.dataType);
  const pathViewer = renderPathFieldViewer(spec, displayValue);
  if (spec.picker === "bone") {
    const isOpen = state.bonePickerOpen && state.activeBoneFieldPath === spec.path;
    const className = isOpen ? "field field-wide" : "field";
    const pickerButton = `<button type="button" class="picker-button" data-picker-action="openBonePicker" data-picker-field="${escapeHtml(spec.path)}">${bonePickerLabel}</button>`;
    return `<div class="${className}"><label>${escapeHtml(label)}</label><div class="field-control"><input ${attrs.join(" ")} value="${escapeHtml(displayValue)}">${pickerButton}</div>${renderDatalist(datalistId, spec.suggestions)}${renderFieldHint(spec)}${pathViewer}${isOpen ? renderBonePicker() : ""}</div>`;
  }
  const attributeUnit = inlineUnit;
  const input = `<input ${attrs.join(" ")} value="${escapeHtml(displayValue)}">`;
  const control = attributeUnit ? `<div class="field-control attribute-value-control">${input}<span class="attribute-unit">${escapeHtml(attributeUnit)}</span></div>` : input;
  const timeHint = target.kind === "baseAttributes" && spec.path.endsWith("Frames")
    ? `<span class="attribute-time-hint" data-attribute-time-for="${escapeHtml(spec.path)}">${escapeHtml(formatAttributeTimeHint(spec.path, value))}</span>` : "";
  return `<div class="field${attributeUnit ? " field-wide" : ""}"><label>${escapeHtml(label)}</label>${control}${renderDatalist(datalistId, spec.suggestions)}${renderFieldHint(spec)}${timeHint}${pathViewer}</div>`;
}

function formatAttributeTimeHint(path, value) {
  const frames = Number(value);
  if (!Number.isFinite(frames) || frames < 0) return "";
  if (path === "cohesionBreakRecoveryDelayFrames" && frames === 0) return "不启用定时恢复";
  const seconds = Number((frames / 60).toFixed(2));
  return `${seconds} 秒模拟时间（按 60Hz）`;
}

function commitInspectorField(target, input) {
  const path = input.dataset.field;
  if ((target.kind === "part" && path === "partId") || (target.kind === "collider" && ["colliderId", "physicsLayer"].includes(path))) return undefined;
  if (target.kind === "body" && ["profileId", "bodyScale", "defaultCapsule.center.x", "defaultCapsule.center.z"].includes(path)) return undefined;
  beginInspectorUndoTransaction(target, path);
  const previousValue = getNested(target.value, path);
  const previousColliderId = target.kind === "collider" ? target.value?.colliderId || "" : "";
  const previousPartStateId = target.kind === "bodyPartDamageDefinition" ? target.value?.partStateId || "" : "";
  const previousAnimatedSourceId = target.kind === "bodyPresenceAnimatedSource" ? target.value?.sourceId || "" : "";
  const previousDamageZoneId = target.kind === "damageZone" ? target.value?.zoneId || "" : "";
  const previousEngagementAnchorId = target.kind === "engagementAnchor" ? target.value?.anchorId || "" : "";
  const previousContactPointStableId = target.kind === "contactPoint" ? target.value?.stableId || "" : "";
  const previousSlotResourceKey = (target.kind === "animationSlot" && (path === "resourceKey" || path === "animationGroupId"))
    ? firstNonEmpty(
      target.value?.resourceSelection?.runtimeResourceKey,
      target.value?.resourceSelection?.packageResourceKey,
      target.value?.resourceSelection?.providerResourceKey,
      target.value?.resourceSelection?.resourceStableId,
      previousValue,
      "")
    : "";
  const value = readInspectorInputValue(input);
  if (value === undefined) return undefined;
  const rawNumber = input.dataset.type === "number" ? Number(input.value) : NaN;
  if (Number.isFinite(rawNumber) && rawNumber !== value) {
    input.value = formatFieldValue(value, "number");
  }
  if (target.kind === "baseAttributes" && !state.package.baseAttributes)
    state.package.baseAttributes = target.value;
  if (target.kind === "presentationImages" && !state.package.applicationConfig.presentationImages)
    state.package.applicationConfig.presentationImages = target.value;
  setNested(target.value, input.dataset.field, value);
  if (target.kind === "baseAttributes" && path.endsWith("Frames")) {
    el.inspector?.querySelectorAll?.("[data-attribute-time-for]").forEach(hint => {
      hint.textContent = formatAttributeTimeHint(hint.dataset.attributeTimeFor, target.value[hint.dataset.attributeTimeFor]);
    });
  }
  afterInspectorFieldEdited(target, input.dataset.field, {
    previousSlotResourceKey,
    previousValue,
    previousColliderId,
    previousContactPointStableId
  });
  applySymmetryEdit(target, input.dataset.field);
  if (input.dataset.picker === "bone") {
    state.activeBoneFieldPath = input.dataset.field;
    state.highlightedBoneValue = value;
  }
  updateUndoTransactionAfter();
  refreshDirtyFromCleanSnapshot();
  renderShellStatus();
  requestViewportUpdate(target, input.dataset.field);
  if (target.kind === "body" && path.startsWith("defaultCapsule.")) globalThis.refreshMovementCollisionInspectorSummary?.();
  if (target.kind === "socket" && ["abstractBoneId", "bonePath", "locatorPath", "parentPartId"].includes(input.dataset.field)) {
    refreshAbstractBoneSocketDiagnostics(target.value);
    renderDiagnostics();
  }
  if (shouldRefreshInspectorForField(input.dataset.field)) renderInspector();
  if (input.dataset.field === "usage") {
    renderResourceBindingBar();
    renderResourcePicker();
  }
  if (target.kind === "body" && input.dataset.field === "modelRootStableId") {
    renderResourceBindingBar();
  }
  if (["animationSlot", "animationGroup", "animationClip", "animationBlendSpace", "animationBlendPoint"].includes(target.kind)) {
    renderAnimationConfigPanel();
    renderResourcePlanPreview();
    renderTree();
  }
  if (["reactionProfile", "reactionSlot"].includes(target.kind)) {
    renderReactionProfilePanel();
    renderResourcePlanPreview();
    renderTree();
  }
  if (target.kind === "collider" && target.value?.colliderId !== previousColliderId) {
    renderTree();
  }
  if (["part", "collider"].includes(target.kind) && ["displayName", "partId", "colliderId", "shape"].includes(path)) {
    globalThis.renderBodyWorkspace?.();
  }
  if (target.kind === "bodyPartDamageDefinition") {
    state.bodyPartDamageValidation = null;
    refreshBodyPartDamageDirtyFromCleanSnapshot();
    if (target.value?.partStateId !== previousPartStateId) {
      state.selectedPath = `geometry/body_part_damage/${encodeURIComponent(target.value.partStateId || "")}`;
      renderTree();
    }
  }
  if (target.kind === "bodyPresenceAnimatedSource" && target.value?.sourceId !== previousAnimatedSourceId) {
    state.selectedPath = `geometry/body_presence/animated/${encodeURIComponent(target.value.sourceId || "")}`;
    renderTree();
  }
  if (target.kind === "contactPoint" && target.value?.stableId !== previousContactPointStableId) {
    renderTree();
  }
  if (target.kind === "damageZone" && target.value?.zoneId !== previousDamageZoneId) {
    state.selectedPath = `geometry/damage_zones/${encodeURIComponent(target.value.zoneId || "")}`;
    renderTree();
  }
  if (target.kind === "engagementAnchor" && target.value?.anchorId !== previousEngagementAnchorId) {
    state.selectedPath = `geometry/engagement_anchors/${encodeURIComponent(target.value.anchorId || "")}`;
    renderTree();
  }
  return value;
}

function beginInspectorUndoTransaction(target, fieldPath) {
  if (!target?.value || !fieldPath) return null;
  return beginUndoTransaction(
    `Edit ${getInspectorUndoLabel(target, fieldPath)}`,
    `inspector:${state.selectedPath}:${fieldPath}`
  );
}

function getInspectorUndoLabel(target, fieldPath) {
  const kindLabel = KIND_LABELS[target.kind] || target.kind || "Unknown";
  return `${kindLabel} ${fieldPath}`;
}

function readInspectorInputValue(input) {
  const type = input.dataset.type || "text";
  if (type === "number") {
    const fallback = Number(input.dataset.fallback || 0);
    const raw = Number(input.value);
    const value = Number.isFinite(raw) ? raw : fallback;
    return clampFieldNumber(value, input);
  }
  if (type === "boolean") return input.value === "true";
  if (type === "stringList" && input.type === "checkbox") {
    return Array.from(input.closest(".field")?.querySelectorAll('input[type="checkbox"][data-field]') || [])
      .filter(item => item.checked)
      .map(item => item.value);
  }
  if (type === "stringList") {
    return input.value
      .split(",")
      .map(item => item.trim())
      .filter(Boolean);
  }
  return input.value;
}

function clampFieldNumber(value, input) {
  let number = value;
  const min = input.dataset.min === undefined ? NaN : Number(input.dataset.min);
  const max = input.dataset.max === undefined ? NaN : Number(input.dataset.max);
  if (Number.isFinite(min)) number = Math.max(min, number);
  if (Number.isFinite(max)) number = Math.min(max, number);
  return number;
}

function formatFieldValue(value, type) {
  if (value == null) return "";
  if (type === "number") {
    const number = Number(value);
    return Number.isFinite(number) ? String(Number(number.toFixed(6))) : "";
  }
  if (type === "stringList") {
    return Array.isArray(value) ? value.join(", ") : String(value || "");
  }
  return String(value);
}

function afterInspectorFieldEdited(target, path, context = {}) {
  const posePath = getPosePathFromEulerField(path);
  if (posePath) syncPoseRotationFromEuler(target.value, posePath);
  if (path.endsWith(".parentKind")) applyPoseParentDefault(target, path.slice(0, -".parentKind".length));
  if (target.kind === "collider" && path === "shape") normalizeColliderShapeDimensions(target.value);
  if (target.kind === "collider" && ["colliderId", "partId", "shape"].includes(path)) {
    normalizeColliderIdAfterEdit(target.value, path, context.previousValue, context.previousColliderId);
  }
  if (target.kind === "contactPoint") {
    normalizeContactPointAfterEdit(target.value, path, context.previousValue, context.previousContactPointStableId);
  }
  if (target.kind === "animationSlot" && path === "resourceKey") {
    const previousResourceKey = context.previousSlotResourceKey;
    const item = findResourceLibraryItemByKey(target.value.resourceKey);
    if (item) {
      target.value.resourceSelection = createResourceSelectionRef(item, RESOURCE_FIELD_SPECS.animationProfileSlot);
      const nextResourceKey = firstNonEmpty(
        target.value.resourceSelection.runtimeResourceKey,
        target.value.resourceSelection.packageResourceKey,
        target.value.resourceSelection.providerResourceKey,
        target.value.resourceKey);
      if (target.value.resourceSelection.runtimeResourceKey || target.value.resourceSelection.packageResourceKey) {
        ensureApplicationResourceKey(target.value.resourceKey);
      }
      if (previousResourceKey && previousResourceKey !== nextResourceKey) {
        cleanupUnusedAnimationResourceKey(previousResourceKey);
      }
    } else if (!target.value.resourceKey) {
      if (previousResourceKey && previousResourceKey !== target.value.resourceKey) {
        cleanupUnusedAnimationResourceKey(previousResourceKey);
      }
      target.value.resourceSelection = {};
    }
  }
  if (target.kind === "animationSlot" && path === "animationGroupId") {
    const previousResourceKey = context.previousSlotResourceKey;
    const group = findAnimationGroupById(target.value.animationGroupId);
    if (group?.sourceResourceKey) {
      target.value.resourceKey = group.sourceResourceKey;
      const item = findResourceLibraryItemByKey(group.sourceResourceKey);
      target.value.resourceSelection = item ? createResourceSelectionRef(item, RESOURCE_FIELD_SPECS.animationProfileSlot) : {};
      ensureApplicationResourceKey(group.sourceResourceKey);
      if (previousResourceKey && previousResourceKey !== target.value.resourceKey) {
        cleanupUnusedAnimationResourceKey(previousResourceKey);
      }
    }
  }
  if (target.kind === "reactionSlot" && path === "resourceKey") {
    const item = findResourceLibraryItemByKey(target.value.resourceKey);
    target.value.resourceSelection = item
      ? createResourceSelectionRef(item, RESOURCE_FIELD_SPECS.reactionAnimationSlot)
      : {};
  }
}

function shouldRefreshInspectorForField(path) {
  return path.endsWith(".parentKind")
    || path === "shape"
    || path === "colliderId"
    || path === "stableId"
    || path === "partId"
    || path === "usage"
    || path === "authority"
    || path === "activationMode"
    || path === "motionMode"
    || path === "motionSocketId"
    || path === "resourceKey"
    || path === "animationGroupId"
    || path === "attachSocketId"
    || path === "traceId"
    || path === "equipSlot";
}

function requestViewportUpdate(target, fieldPath) {
  if (target?.kind === "body" && String(fieldPath).startsWith("defaultCapsule.") && globalThis.refreshMovementCollisionPreview?.()) return;
  if (target?.kind === "body" && fieldPath === "massKg" && globalThis.refreshBodyMeasurementPreview?.()) return;
  if (shouldRebuildViewportForField(target, fieldPath)) {
    renderViewport();
    return;
  }
  refreshViewportLiveData();
  viewportSyncTransformControl?.(state.selectedPath || "");
}

function shouldRebuildViewportForField(target, fieldPath) {
  const path = String(fieldPath || "");
  if (!target?.kind || !path) return false;
  if (path.endsWith(".parentKind") || path.endsWith(".parentPath")) return true;
  if (["modelRootStableId", "equipSlot", "attachSocketId", "traceId", "abstractBoneId"].includes(path)) return true;
  if (target.kind === "collider" && /^(shape|size\.|radius|height)$/.test(path)) return true;
  if (target.kind === "contactPoint" && /^(stableId|usage|authority|activationMode|motionMode|motionSocketId|radiusMeters)$/.test(path)) return true;
  if (target.kind === "body" && /^(bodyKind|bodyScale|heightMeters|radiusMeters|defaultCapsule\.)/.test(path)) return true;
  return false;
}

function normalizeColliderShapeDimensions(collider) {
  if (!collider) return;
  const shape = String(collider.shape || "Capsule");
  if (shape === "Box") {
    collider.size = collider.size || {};
    if (!(Number(collider.size.x) > 0)) collider.size.x = 0.25;
    if (!(Number(collider.size.y) > 0)) collider.size.y = 0.25;
    if (!(Number(collider.size.z) > 0)) collider.size.z = 0.25;
    return;
  }

  if (!(Number(collider.radius) > 0)) collider.radius = 0.12;
  if (shape === "Capsule" && !(Number(collider.height) > 0)) {
    collider.height = Math.max(0.5, collider.radius * 2);
  }
}

function normalizeColliderIdAfterEdit(collider, path, previousValue = "", previousColliderId = "") {
  if (!collider) return;
  const beforeId = previousColliderId || collider.colliderId || "";
  if (path !== "colliderId" && beforeId) return;
  let nextId = sanitizeConfigId(collider.colliderId || "");

  if (path === "colliderId") {
    nextId = makeUniqueColliderId(nextId || buildColliderAutoId(collider.partId, collider.shape), collider);
  } else if (shouldAutoRenameColliderId(collider, beforeId, path, previousValue)) {
    nextId = makeUniqueColliderId(buildColliderAutoId(collider.partId, collider.shape), collider);
  }

  if (!nextId) return;
  if (nextId !== collider.colliderId) {
    collider.colliderId = nextId;
  }
  if (beforeId && beforeId !== nextId && state.selectedPath === `geometry/colliders/${beforeId}`) {
    state.selectedPath = `geometry/colliders/${nextId}`;
  }
}

function normalizeContactPointAfterEdit(contactPoint, path, previousValue = "", previousStableId = "") {
  if (!contactPoint) return;
  normalizeContactPoint(contactPoint);
  if (path === "stableId") {
    const previous = previousStableId || previousValue || "";
    contactPoint.stableId = String(contactPoint.stableId || "").trim().replace(/\s+/g, "_").replace(/[\\/]+/g, ".");
    if (previous && previous !== contactPoint.stableId && state.selectedPath === `geometry/body_part_contact_points/${previous}`) {
      state.selectedPath = `geometry/body_part_contact_points/${contactPoint.stableId}`;
    }
  }
  if (path === "partId" && contactPoint.bodyPartId <= 0 && typeof inferContactPointBodyPartId === "function") {
    contactPoint.bodyPartId = inferContactPointBodyPartId(contactPoint.partId, contactPoint.contactPointId);
  }
}

function shouldAutoRenameColliderId(collider, currentId, path, previousValue = "") {
  const id = String(currentId || collider?.colliderId || "");
  if (!id) return true;
  const previousShape = path === "shape" ? previousValue : collider?.shape;
  const previousPart = path === "partId" ? previousValue : collider?.partId;
  const previousAutoId = buildColliderAutoId(previousPart, previousShape);
  return id === previousAutoId || hasColliderShapeSuffix(id);
}

function buildColliderAutoId(partId, shape) {
  const base = sanitizeConfigId(partId || "body");
  const suffix = colliderShapeSuffix(shape);
  return `${base}_${suffix}`;
}

function colliderShapeSuffix(shape) {
  const normalized = String(shape || "Capsule").toLowerCase();
  if (normalized === "sphere") return "sphere";
  if (normalized === "box") return "box";
  return "capsule";
}

function hasColliderShapeSuffix(id) {
  return /_(sphere|capsule|box)(_\d+)?$/i.test(String(id || ""));
}

function makeUniqueColliderId(base, currentCollider) {
  const existing = (state.package?.geometry?.colliders || [])
    .filter(item => item !== currentCollider)
    .map(item => item?.colliderId);
  return uniqueConfigId(base, existing);
}

function sanitizeConfigId(value) {
  return String(value || "new")
    .trim()
    .replace(/\s+/g, "_")
    .replace(/[^A-Za-z0-9_.-]+/g, "_")
    .replace(/^_+|_+$/g, "") || "new";
}

function applyPoseParentDefault(target, posePath) {
  const pose = getNested(target.value, posePath);
  if (!pose) return;
  if (pose.parentKind === "BodyPart") {
    pose.parentPath = target.value.partId || target.value.parentPartId || firstValue(bodyPartOptions()) || "";
  } else if (pose.parentKind === "Socket") {
    pose.parentPath = target.value.attachSocketId || firstValue(socketOptions()) || "";
  } else if (pose.parentKind === "Bone") {
    pose.parentPath = target.value.bonePath || firstValue(bonePathOptions()) || "";
    state.activeBoneFieldPath = `${posePath}.parentPath`;
    state.bonePickerOpen = true;
    state.highlightedBoneValue = pose.parentPath;
  } else if (pose.parentKind === "Locator") {
    const traceLocator = posePath === "endPose" ? target.value.endLocatorPath : target.value.startLocatorPath;
    pose.parentPath = target.value.locatorPath || target.value.locatorId || traceLocator || firstValue(locatorPathOptions()) || "";
  } else {
    pose.parentPath = "";
  }
}

function firstValue(options) {
  const first = normalizeOptions(options)[0];
  return first ? first.value : "";
}

function resetModelWrapperPose(resource) {
  const pose = ensureModelWrapperPose(resource);
  pose.position = { x: 0, y: 0, z: 0 };
  pose.rotation = { x: 0, y: 0, z: 0, w: 1 };
  pose.scale = { x: 1, y: 1, z: 1 };
  pose.eulerHint = { x: 0, y: 0, z: 0 };
}

function syncModelWrapperRotationFromEuler(resource) {
  const pose = ensureModelWrapperPose(resource);
  syncPoseQuaternion(pose);
}

function getPosePathFromEulerField(path) {
  const marker = ".eulerHint.";
  const index = path.indexOf(marker);
  return index >= 0 ? path.slice(0, index) : "";
}

function syncPoseRotationFromEuler(owner, posePath) {
  const pose = ensureLocalPose(owner, posePath);
  syncPoseQuaternion(pose);
}

function syncPoseQuaternion(pose) {
  if (!pose) return;
  pose.rotation = quaternionFromEulerDegrees(
    pose.eulerHint?.x || 0,
    pose.eulerHint?.y || 0,
    pose.eulerHint?.z || 0
  );
}

function quaternionFromEulerDegrees(xDegrees, yDegrees, zDegrees) {
  const x = degreesToRadians(xDegrees) / 2;
  const y = degreesToRadians(yDegrees) / 2;
  const z = degreesToRadians(zDegrees) / 2;
  const c1 = Math.cos(x);
  const c2 = Math.cos(y);
  const c3 = Math.cos(z);
  const s1 = Math.sin(x);
  const s2 = Math.sin(y);
  const s3 = Math.sin(z);
  const quaternion = {
    x: s1 * c2 * c3 + c1 * s2 * s3,
    y: c1 * s2 * c3 - s1 * c2 * s3,
    z: c1 * c2 * s3 + s1 * s2 * c3,
    w: c1 * c2 * c3 - s1 * s2 * s3
  };
  const length = Math.hypot(quaternion.x, quaternion.y, quaternion.z, quaternion.w) || 1;
  return {
    x: Number((quaternion.x / length).toFixed(8)),
    y: Number((quaternion.y / length).toFixed(8)),
    z: Number((quaternion.z / length).toFixed(8)),
    w: Number((quaternion.w / length).toFixed(8))
  };
}

function formatInspectorStatusLabel(status) {
  const value = String(status || "Unknown");
  const labels = {
    Ready: "就绪",
    Validation: "校验",
    Blocked: "阻塞",
    Warning: "需处理",
    Pending: "等待",
    Unknown: "未知",
    Failed: "失败"
  };
  return labels[value] || value;
}

function getAbstractBoneLocalDiagnostics() {
  const sockets = state.package?.geometry?.sockets || [];
  const packageId = String(state.package?.manifest?.packageId || "");
  const counts = new Map();
  for (const socket of sockets) {
    const id = String(socket?.abstractBoneId || "").trim();
    if (id) counts.set(id, (counts.get(id) || 0) + 1);
  }

  const issues = [];
  if ((counts.get("head") || 0) === 0) {
    issues.push({
      severity: "Error",
      gate: "SpawnBlocked",
      code: "CHARPKG_REQUIRED_ABSTRACT_BONE_MISSING",
      message: "Every spawnable character requires one socket with abstractBoneId=head.",
      sourceObjectPath: "geometry/sockets"
    });
  }

  for (const socket of sockets) {
    const id = String(socket?.abstractBoneId || "").trim();
    if (!id) continue;
    const socketPath = `geometry/sockets/${socket?.socketId || "socket"}`;
    const validCore = ABSTRACT_BONE_ID_OPTIONS.includes(id);
    const extensionPrefix = `ext.${packageId}.`;
    const extensionName = id.startsWith(extensionPrefix) ? id.slice(extensionPrefix.length) : "";
    const validExtension = Boolean(packageId && extensionName && /^[a-z0-9_-]+$/.test(extensionName));
    if (!validCore && !validExtension) {
      issues.push({
        severity: "Error",
        gate: "SpawnBlocked",
        code: "CHARPKG_ABSTRACT_BONE_ID_INVALID",
        message: `${id} must be a core semantic or ext.${packageId || "<packageId>"}.<name>.`,
        sourceObjectPath: socketPath
      });
    }
    if ((counts.get(id) || 0) > 1) {
      issues.push({
        severity: "Error",
        gate: "SpawnBlocked",
        code: "CHARPKG_ABSTRACT_BONE_DUPLICATE",
        message: `${id} is assigned to more than one socket.`,
        sourceObjectPath: socketPath
      });
    }
    if (!isSocketBindingDeclared(socket)) {
      issues.push({
        severity: "Error",
        gate: "SpawnBlocked",
        code: "CHARPKG_ABSTRACT_BONE_BINDING_MISSING",
        message: `${id} binding does not match localPose.parentKind.`,
        sourceObjectPath: socketPath
      });
    }
  }
  return issues;
}

function isSocketBindingDeclared(socket) {
  const parentKind = String(socket?.localPose?.parentKind || "Unknown");
  if (parentKind === "ModelRoot") return true;
  if (parentKind === "Bone") return Boolean(String(socket?.bonePath || "").trim());
  if (parentKind === "Locator") return Boolean(String(socket?.locatorPath || "").trim());
  if (parentKind === "BodyPart") return Boolean(String(socket?.parentPartId || "").trim());
  return Boolean(String(socket?.bonePath || "").trim()
    || String(socket?.locatorPath || "").trim()
    || String(socket?.parentPartId || "").trim());
}

function renderAbstractBoneSocketDiagnostics(socket) {
  const socketPath = `geometry/sockets/${socket?.socketId || "socket"}`;
  const issues = getAbstractBoneLocalDiagnostics().filter(issue =>
    issue.sourceObjectPath === socketPath || issue.sourceObjectPath === "geometry/sockets");
  const stableRootBinding = String(socket?.localPose?.parentKind || "") === "ModelRoot";
  const rows = issues.length
    ? issues.map(issue => `<div class="diagnostic-item error"><strong>${escapeHtml(issue.code)}</strong><div class="meta">${escapeHtml(issue.message)}</div></div>`).join("")
    : stableRootBinding
      ? `<div class="readiness-ok">Stable ModelRoot binding: follows the character root and ignores bone animation.</div>`
      : `<div class="readiness-ok">Abstract bone semantic is locally valid.</div>`;
  return `<section class="reference-card" data-abstract-bone-diagnostics>${rows}</section>`;
}

function refreshAbstractBoneSocketDiagnostics(socket) {
  const host = el.inspector?.querySelector?.("[data-abstract-bone-diagnostics]");
  if (!host) return;
  const html = renderAbstractBoneSocketDiagnostics(socket);
  const wrapper = document.createElement("div");
  wrapper.innerHTML = html;
  host.replaceWith(wrapper.firstElementChild);
}

function getGameplayCameraLocalDiagnostics() {
  const profile = state.package?.applicationConfig?.gameplayCameraProfile;
  if (!profile) return [];
  const issues = [];
  const focusId = String(profile.focusAbstractBoneId || "").trim();
  const matches = (state.package?.geometry?.sockets || [])
    .filter(socket => String(socket?.abstractBoneId || "").trim() === focusId);
  if (!focusId || matches.length !== 1) {
    issues.push({ severity: "Error", gate: "SpawnBlocked", code: "CHARACTER_CAMERA_FOCUS_MISSING", message: `Gameplay Camera focus must resolve to exactly one socket. abstractBoneId=${focusId || "<empty>"} matches=${matches.length}`, sourceObjectPath: "gameplayCameraProfile" });
  }
  const extents = profile.framingBoundsExtents || {};
  if (![extents.x, extents.y, extents.z].every(value => Number.isFinite(Number(value)) && Number(value) > 0)) {
    issues.push({ severity: "Error", gate: "SpawnBlocked", code: "CHARACTER_CAMERA_BOUNDS_INVALID", message: "Gameplay Camera framing extents must be finite and greater than zero.", sourceObjectPath: "gameplayCameraProfile" });
  }
  const normal = profile.normal || {};
  const locked = profile.locked || {};
  const normalValid = Number(normal.minDistance) > 0
    && Number(normal.minDistance) <= Number(normal.distance)
    && Number(normal.distance) <= Number(normal.maxDistance)
    && Number(normal.fieldOfView) >= 20 && Number(normal.fieldOfView) <= 80
    && Number(normal.pitch) >= -80 && Number(normal.pitch) <= 80
    && Number(normal.targetPadding) >= 0;
  const lockedValid = Number(locked.minDistance) > 0
    && Number(locked.minDistance) <= Number(locked.maxDistance)
    && Number(locked.targetScreenY) >= 0.55 && Number(locked.targetScreenY) <= 0.85
    && Number(locked.targetPadding) >= 0;
  if (!profile.profileId || !normalValid || !lockedValid) {
    issues.push({ severity: "Error", gate: "SpawnBlocked", code: "CHARACTER_CAMERA_PROFILE_INVALID", message: "Gameplay Camera profile id, distance, FOV, pitch, screen placement, or padding is invalid.", sourceObjectPath: "gameplayCameraProfile" });
  }
  return issues;
}

function renderDiagnostics() {
  const validationIssues = state.validation?.issues || [];
  const gate = state.compileResult?.gateReport;
  const gateIssues = gate?.issues || [];
  const abstractBoneIssues = getAbstractBoneLocalDiagnostics();
  const gameplayCameraIssues = getGameplayCameraLocalDiagnostics();
  const status = state.compileResult?.status || (validationIssues.length ? "Validation" : "Ready");
  const rows = [`<div class="status-line"><strong>状态</strong><span class="badge ${isImportBlocked() ? "error" : "ok"}">${escapeHtml(formatInspectorStatusLabel(status))}</span></div>`];
  if (gate) {
    rows.push(`<div class="meta">source=${escapeHtml(state.compileResult.hashes?.sourcePackageHash || "-")} mapping=${escapeHtml(state.compileResult.hashes?.resourceMappingHash || "-")}</div>`);
  }
  const issues = [...abstractBoneIssues, ...gameplayCameraIssues, ...validationIssues, ...gateIssues];
  if (issues.length === 0) {
    rows.push(`<div class="empty">没有诊断信息。</div>`);
  } else {
    for (const issue of issues) {
      const cls = issue.severity === "Error" || issue.gate === "ImportBlocked" || issue.gate === "ExportBlocked" ? "error" : "warning";
      const issuePath = normalizeIssuePath(issue.sourceObjectPath || issue.sourcePath || "");
      rows.push(`<div class="diagnostic-item ${cls}"><strong>${escapeHtml(issue.code || issue.gate || "issue")}</strong><div class="meta">${escapeHtml(issue.message || "")}</div><div class="meta">${escapeHtml(issue.sourceObjectPath || issue.sourcePath || "")}</div>${issuePath ? `<button type="button" data-jump="${escapeHtml(issuePath)}">定位</button>` : ""}</div>`);
    }
  }
  el.diagnostics.innerHTML = rows.join("");
  el.diagnostics.querySelectorAll("button[data-jump]").forEach(button => {
    button.addEventListener("click", () => selectPath(button.dataset.jump));
  });
  renderShellStatus();
}

function renderImportStatus() {
  const report = getUnityImportReport();
  const readinessStage = getReadinessStage("UnityImport");
  const readinessIssues = [
    ...(readinessStage?.blockers || []),
    ...(readinessStage?.warnings || [])
  ];
  const readinessDiagnostics = readinessIssues.length
    ? `<section class="unity-import-readiness-issues"><strong>当前导入诊断</strong>${readinessIssues.map(issue => `<div class="diagnostic-item ${String(issue?.severity || "").toLowerCase() === "error" ? "error" : "warning"}"><strong>${escapeHtml(issue?.code || "UNITY_IMPORT_ISSUE")}</strong><div class="meta">${escapeHtml(issue?.message || "")}</div>${issue?.suggestedFix ? `<div class="diagnostic-fix"><strong>建议：</strong>${escapeHtml(issue.suggestedFix)}</div>` : ""}${issue?.sourceObjectPath || issue?.sourcePath ? `<div class="meta">${escapeHtml(issue.sourceObjectPath || issue.sourcePath)}</div>` : ""}</div>`).join("")}</section>`
    : "";
  const unityEntries = getUnityCatalogEntries();
  const importedCount = unityEntries.filter(entry => String(entry.importStatus || entry.providerData?.importStatus || "").toLowerCase() === "imported").length;
  const taskPanel = renderUnityImportTaskPanel();
  const catalogSummary = state.unityResourceCatalog
    ? `<div class="status-line"><strong>Unity Catalog</strong><span class="badge ok">${unityEntries.length} 项 / 已导入 ${importedCount}</span></div><div class="meta">${escapeHtml(state.unityResourceCatalogPath || "-")}</div>`
    : `<div class="status-line"><strong>Unity Catalog</strong><span class="badge warn">缺失</span></div><div class="meta">${escapeHtml(state.unityResourceCatalogPath || "Assets/MxFrameworkGenerated/CharacterPackages/<packageId>/config/unity_resource_catalog.json")}</div>`;
  if (!report && taskPanel) {
    el.importStatus.innerHTML = `${taskPanel}${readinessDiagnostics}${catalogSummary}`;
    return;
  }
  if (!report) {
    el.importStatus.innerHTML = `${readinessDiagnostics}${catalogSummary}<div class="empty">还没有 Unity 导入报告。运行 Unity 导入后会生成。</div>`;
    return;
  }
  const summary = getUnityImportReportSummary(report);
  const operations = summary.operations;
  const visibleOperations = operations.visibleOperations.slice(0, 8);
  const hiddenVisibleCount = Math.max(0, operations.visibleOperations.length - visibleOperations.length);
  el.importStatus.innerHTML = [
    taskPanel,
    readinessDiagnostics,
    catalogSummary,
    `<div class="status-line"><strong>${escapeHtml(summary.title)}</strong><span class="badge ${escapeHtml(summary.tone)}">${escapeHtml(summary.label)}</span></div>`,
    `<div class="meta" title="${escapeHtml(report.targetRootPath || state.importResult?.reportOut || "-")}">target=${escapeHtml(report.targetRootPath || state.importResult?.reportOut || "-")}</div>`,
    `<div class="meta">变更=${operations.changed} 已是最新=${operations.upToDate} 问题=${operations.problem} 总数=${operations.total}</div>`,
    `<div class="meta">report=${escapeHtml(report.reportPath || state.importResult?.reportOut || "-")}</div>`,
    visibleOperations.length
      ? visibleOperations.map(renderUnityImportOperation).join("")
      : `<div class="unity-import-operation-empty">没有变更或问题操作。已是最新的项目折叠在下方，并保留 hash 详情。</div>`,
    hiddenVisibleCount ? `<div class="meta">还有 ${hiddenVisibleCount} 个变更/问题操作被隐藏；请打开完整报告查看详情。</div>` : "",
    renderUnityImportUpToDateDetails(operations.upToDateOperations)
  ].join("");
}

function renderUnityImportOperation(operation) {
  const tone = getUnityImportOperationTone(operation);
  const category = classifyUnityImportOperation(operation);
  const path = operation.targetPath || operation.sourcePath || "";
  const hash = operation.contentHash || operation.existingHash || "";
  const artifactHash = operation.artifactHash && operation.artifactHash !== hash ? operation.artifactHash : "";
  return `<div class="operation-item ${escapeHtml(category)}">
    <strong>${escapeHtml(operation.action || operation.kind || "operation")}</strong>
    <span class="sync-badge ${escapeHtml(tone)}">${escapeHtml(getUnityImportOperationLabel(operation))}</span>
    <div class="meta" title="${escapeHtml(path)}">${escapeHtml(path)}</div>
    ${operation.message ? `<div class="meta">${escapeHtml(operation.message)}</div>` : ""}
    ${hash ? `<div class="meta" title="${escapeHtml(hash)}">hash=${escapeHtml(hash)}</div>` : ""}
    ${artifactHash ? `<div class="meta" title="${escapeHtml(artifactHash)}">artifact=${escapeHtml(artifactHash)}</div>` : ""}
  </div>`;
}

function renderUnityImportUpToDateDetails(operations) {
  if (!operations.length) return "";
  return `<details class="unity-import-up-to-date">
    <summary>已是最新项目 ${escapeHtml(String(operations.length))}</summary>
    <div class="unity-import-up-to-date-list">
      ${operations.map(renderUnityImportOperation).join("")}
    </div>
  </details>`;
}

function selectPath(path, options = {}) {
  if (!path) return;
  const { preserveViewport = false } = options || {};
  const attributeWorkspaceChanged = (state.selectedPath === "config/attributes") !== (path === "config/attributes");
  const bodyWorkspaceChanged = globalThis.getBodyWorkspaceSection?.(state.selectedPath) !== globalThis.getBodyWorkspaceSection?.(path);
  const requiresViewportRebuild = state.bonePickerOpen
    || attributeWorkspaceChanged
    || bodyWorkspaceChanged
    || (state.hurtboxCoverageVisible && globalThis.isBodyWorkspacePath?.(path) && path !== state.selectedPath)
    || ((state.selectedPath === "config/gameplay-camera") !== (path === "config/gameplay-camera"));
  state.selectedPath = path;
  if (path === "config/attributes") {
    if (requiresViewportRebuild) state.attributePreviewOverlays = false;
    state.viewportMode = "character";
    state.contextDockCollapsed = true;
    state.activeInspectorTab = "properties";
  }
  if (globalThis.isBodyWorkspacePath?.(path)) {
    if (bodyWorkspaceChanged) state.bodyPartsPreviewScope = "part";
    if (path.startsWith("geometry/body_parts/") && state.bodyPartsPreviewScope === "selected") state.bodyPartsPreviewScope = "part";
    state.viewportMode = "character";
    state.contextDockCollapsed = true;
    state.activeInspectorTab = "properties";
    state.weaponTransformKey = path.startsWith("geometry/colliders/") ? `${path}#localPose` : "";
  }
  if (typeof renderLayoutState === "function") renderLayoutState();
  if (typeof renderContextDockState === "function") renderContextDockState();
  if (typeof renderInspectorTabState === "function") renderInspectorTabState();
  if ((attributeWorkspaceChanged || bodyWorkspaceChanged) && typeof renderWorkflowBar === "function") renderWorkflowBar();
  const target = findTarget(path);
  syncHurtboxDisplayPartToSelection?.(target);
  state.activeBoneFieldPath = getDefaultBoneFieldPath(target);
  state.bonePickerOpen = false;
  state.resourcePickerField = null;
  state.resourcePickerQuery = null;
  state.highlightedBoneValue = state.activeBoneFieldPath ? (getNested(target.value, state.activeBoneFieldPath) || "") : "";
  el.packageTree.querySelectorAll("button[data-path], button[data-context-tab], button[data-inspector-tab]").forEach(button => {
    button.classList.toggle("active", isTreeNodeActive(button.dataset));
  });
  renderResourceBindingBar();
  renderResourcePicker();
  if (preserveViewport && !requiresViewportRebuild && typeof viewportSelectionSync === "function") {
    viewportSelectionSync(state.selectedPath);
  } else {
    renderViewport();
  }
  renderInspector();
  globalThis.scheduleCharacterStudioSessionPersistence?.();
}

function leaveBaseAttributeWorkspace() {
  if (state.selectedPath === "config/attributes") selectPath("overview", { preserveViewport: true });
}

function findTarget(path) {
  const pkg = state.package || {};
  const g = pkg.geometry || {};
  if (path === "overview") return target("overview", "流程概览", pkg);
  if (path === "manifest") return target("manifest", "Manifest", pkg.manifest);
  if (path === "resources") return target("resources", "资源 Catalog", pkg.resourceCatalog);
  if (path.startsWith("resources/")) return target("resource", path.slice(10), (pkg.resourceCatalog?.entries || []).find(x => x.resourceKey === path.slice(10)));
  if (path.startsWith("commonResources/")) {
    const resourceKey = decodeURIComponent(path.slice("commonResources/".length));
    return target("resource", resourceKey, findResourceByKey(resourceKey));
  }
  if (path === "config") return target("config", "角色源引用（只读）", pkg.applicationConfig);
  if (path === "config/images") return target("presentationImages", "头像与立绘", pkg.applicationConfig?.presentationImages || {
    schemaVersion: "1.0", portraitResourceKey: "", standingResourceKey: "", variants: []
  });
  if (path === "config/tactical") return target("tacticalDefaults", "默认战术内容", pkg.tacticalDefaults);
  if (path === "config/attributes") return target("baseAttributes", "基础属性", pkg.baseAttributes || {
    schemaVersion: "1.0", hp: 100, attack: 20, defense: 10, baseCohesion: 100,
    scatterRecoveryRate: 2, scatterRecoveryDelayFrames: 360, cohesionBreakRecoveryDelayFrames: 270
  });
  if (path === "config/gameplay-camera") return target("gameplayCamera", "Gameplay Camera", pkg.applicationConfig?.gameplayCameraProfile || null);
  if (path === "config/animation") return target("animationConfig", "Animation", pkg.applicationConfig);
  if (path === "config/animation/groups") return target("animationGroups", "动画分组", pkg.applicationConfig?.animationGroups || []);
  if (path.startsWith("config/animation/groups/")) {
    const parts = path.split("/");
    const groupId = decodeURIComponent(parts[3] || "");
    const group = (pkg.applicationConfig?.animationGroups || []).find(item => item?.groupId === groupId);
    if (parts.length <= 4) return target("animationGroup", group?.groupId || "Animation Group", group, { groupId });
    if (parts[4] === "clips") {
      const clipId = decodeURIComponent(parts[5] || "");
      const clip = (group?.clips || []).find(item => item?.clipId === clipId);
      return target("animationClip", clip?.clipId || clipId || "Animation Clip", clip, { groupId, clipId });
    }
    if (parts[4] === "blendSpaces") {
      const blendSpaceId = decodeURIComponent(parts[5] || "");
      const blendSpace = (group?.blendSpaces || []).find(item => item?.blendSpaceId === blendSpaceId);
      if (parts.length <= 6) return target("animationBlendSpace", blendSpace?.blendSpaceId || blendSpaceId || "Blend Space", blendSpace, { groupId, blendSpaceId });
      const index = Number(parts[7] || 0);
      return target("animationBlendPoint", `Blend Point ${index + 1}`, (blendSpace?.clips || [])[index], { groupId, blendSpaceId, pointIndex: index });
    }
  }
  if (path.startsWith("config/animation/")) {
    const parts = path.split("/");
    const profileId = decodeURIComponent(parts[2] || "");
    const profile = (pkg.applicationConfig?.animationProfiles || []).find(item => item?.profileId === profileId) || getDefaultAnimationProfile();
    if (parts.length <= 3) return target("animationProfile", profile?.profileId || "Animation Profile", profile, { profileId: profile?.profileId || "" });
    const slotId = decodeURIComponent(parts[4] || "");
    const slot = (profile?.slots || []).find(item => item?.slotId === slotId);
    return target("animationSlot", slot?.displayName || slotId || "Animation Slot", slot, { profileId: profile?.profileId || "" });
  }
  if (path === "config/reactions") return target("reactionConfig", "受击", pkg.applicationConfig);
  if (path.startsWith("config/reactions/")) {
    const parts = path.split("/");
    const profileId = decodeURIComponent(parts[2] || "");
    const profile = (pkg.applicationConfig?.reactionProfiles || []).find(item => item?.profileId === profileId) || getDefaultReactionProfile();
    if (parts.length <= 3) return target("reactionProfile", profile?.profileId || "Reaction Profile", profile, { profileId: profile?.profileId || "" });
    const slotId = decodeURIComponent(parts[4] || "");
    const slot = (profile?.slots || []).find(item => item?.slotId === slotId);
    return target("reactionSlot", getReactionSlotDisplayName(slot || slotId), slot, { profileId: profile?.profileId || "" });
  }
  if (path === "geometry/body") return target("body", "Body Geometry", g.bodyProfile);
  if (path === "geometry/body/capsule") return target("body", "移动碰撞配置", g.bodyProfile);
  if (path === "geometry/body/parts") return target("bodyPartsOverview", "部位与受击体", g);
  if (path === "geometry/body_presence") return target("bodyPresence", "Body Presence", state.bodyPresence || { state: "Unavailable" });
  if (path.startsWith("geometry/body_presence/animated/")) {
    const sourceId = decodeURIComponent(path.slice("geometry/body_presence/animated/".length));
    return target("bodyPresenceAnimatedSource", sourceId,
      getBodyPresenceAnimatedSources().find(source => source?.sourceId === sourceId));
  }
  if (path.startsWith("geometry/body_presence/")) {
    const volumeId = decodeURIComponent(path.slice("geometry/body_presence/".length));
    return target("bodyPresenceVolume", volumeId,
      getBodyPresenceItems().find(volume => volume?.volumeId === volumeId));
  }
  if (path === "geometry/body_part_damage") return target("bodyPartDamage", "部位伤害", state.bodyPartDamage || { state: "Unavailable" });
  if (path.startsWith("geometry/body_part_damage/")) {
    const partStateId = decodeURIComponent(path.slice("geometry/body_part_damage/".length));
    return target("bodyPartDamageDefinition", partStateId,
      getBodyPartDamageItems().find(definition => definition?.partStateId === partStateId));
  }
  if (path === "geometry/damage_zones") return target("damageZones", "DamageZone", g);
  if (path.startsWith("geometry/damage_zones/")) {
    const zoneId = decodeURIComponent(path.slice("geometry/damage_zones/".length));
    return target("damageZone", zoneId, getDamageZoneItems(g).find(zone => zone?.zoneId === zoneId));
  }
  if (path === "geometry/engagement_anchors") return target("engagementAnchors", "接战锚点", g);
  if (path.startsWith("geometry/engagement_anchors/")) {
    const anchorId = decodeURIComponent(path.slice("geometry/engagement_anchors/".length));
    return target("engagementAnchor", anchorId,
      getEngagementAnchorItems(g).find(anchor => anchor?.anchorId === anchorId));
  }
  if (path.startsWith("geometry/body_parts/")) return target("part", path.split("/").pop(), (g.bodyParts || []).find(x => x.partId === path.split("/").pop()));
  if (path.startsWith("geometry/colliders/")) return target("collider", path.split("/").pop(), (g.colliders || []).find(x => x.colliderId === path.split("/").pop()));
  if (path.startsWith("geometry/body_part_contact_points/")) {
    const key = path.split("/").pop();
    const contactPoint = (g.bodyPartContactPoints || []).find(x => String(x?.stableId || "") === key || String(x?.contactPointId || "") === key);
    return target("contactPoint", key, contactPoint);
  }
  if (path.startsWith("geometry/sockets/")) return target("socket", path.split("/").pop(), (g.sockets || []).find(x => x.socketId === path.split("/").pop()));
  if (path.startsWith("geometry/weapon_attachments/")) {
    const attachmentId = decodeURIComponent(path.split("/").pop());
    return target("weapon", attachmentId, findWeaponAttachmentById(attachmentId));
  }
  if (path.startsWith("geometry/traces/")) return target("trace", path.split("/").pop(), (g.traces || []).find(x => x.traceId === path.split("/").pop()));
  if (path.startsWith("validation/issues/")) return target("issue", path.split("/").pop(), (state.validation?.issues || [])[Number(path.split("/").pop())]);
  return target("", path, null);
}

function target(kind, label, value, extra = {}) {
  return { kind, label, value, ...extra };
}

function normalizeIssuePath(sourceObjectPath) {
  if (!sourceObjectPath) return "validation";
  const path = String(sourceObjectPath);
  if (path === "gameplayCameraProfile" || path.startsWith("gameplayCameraProfile/")) return "config/gameplay-camera";
  if (path === "characterStableId" || path === "manifest" || path.endsWith("manifest.json")) return "manifest";
  if (path.startsWith("geometry/bodyColliders/")) return path.replace("geometry/bodyColliders/", "geometry/colliders/");
  if (path.startsWith("geometry/body_colliders/")) return path.replace("geometry/body_colliders/", "geometry/colliders/");
  if (path.startsWith("geometry/bodyPartContactPoints/")) return path.replace("geometry/bodyPartContactPoints/", "geometry/body_part_contact_points/");
  if (path.startsWith("geometry/body_part_contact_points/")) return path;
  if (path.startsWith("geometry/weaponAttachments/")) return path.replace("geometry/weaponAttachments/", "geometry/weapon_attachments/");
  if (path.startsWith("geometry/weapon_attachments/")) return path;
  if (path.startsWith("engagementAnchors/anchors/")) return path.replace("engagementAnchors/anchors/", "geometry/engagement_anchors/");
  if (path === "geometry/body_presence.json" || path === "bodyPresence") return "geometry/body_presence";
  if (path.startsWith("bodyPresence/volumes/")) return `geometry/body_presence/${encodeURIComponent(path.slice("bodyPresence/volumes/".length))}`;
  if (path.startsWith("bodyPresence/animatedHurtboxSources/")) return `geometry/body_presence/animated/${encodeURIComponent(path.slice("bodyPresence/animatedHurtboxSources/".length).split("/")[0])}`;
  if (path === "geometry/body_part_damage.json" || path === "bodyPartDamage") return "geometry/body_part_damage";
  if (path.startsWith("bodyPartDamage/partStates/")) {
    const token = path.slice("bodyPartDamage/partStates/".length).split("/")[0];
    const index = Number(token);
    const definition = Number.isInteger(index) ? getBodyPartDamageItems()[index] : null;
    const partStateId = definition?.partStateId || token;
    return partStateId ? `geometry/body_part_damage/${encodeURIComponent(partStateId)}` : "geometry/body_part_damage";
  }
  if (path.startsWith("geometry/sockets/") || path.startsWith("geometry/traces/") || path.startsWith("geometry/colliders/") || path.startsWith("geometry/body_presence/") || path.startsWith("geometry/body_part_damage/") || path.startsWith("geometry/damage_zones/") || path.startsWith("geometry/engagement_anchors/")) return path;
  if ((state.package?.resourceCatalog?.entries || []).some(entry => entry.resourceKey === path)) return `resources/${path}`;
  return path.startsWith("validation/") ? path : "validation";
}

Object.assign(globalThis, {
  renderSummary,
  summaryCell,
  renderTree,
  buildTree,
  buildTechnicalTree,
  renderOverviewInspector,
  renderManifestInspector,
  node,
  grouped,
  getContactPointInspectorPath,
  getBodyPresenceItems,
  getBodyPresenceAnimatedSources,
  animatedHurtboxColliderOptions,
  getBodyPresenceStateLabel,
  renderBodyPresenceRoot,
  renderBodyPresenceVolumeInspector,
  getBodyPartDamageItems,
  getBodyPartDamageStateLabel,
  renderBodyPartDamageRoot,
  renderBodyPartDamageToolbar,
  bindBodyPartDamageActions,
  getEngagementAnchorItems,
  renderInspector,
  renderObjectTitle,
  getInspectorInputTarget,
  isWeaponModelWrapperField,
  getWeaponModelResource,
  ensureWeaponModelWrapperPose,
  renderInspectorContext,
  renderAnimationProfileReferenceContext,
  renderAnimationGroupReferenceContext,
  renderAnimationClipReferenceContext,
  renderAnimationBlendSpaceContext,
  renderAnimationBlendPointContext,
  renderAnimationEditorReferenceRow,
  renderAnimationSlotReferenceContext,
  renderReactionProfileReferenceContext,
  renderReactionSlotReferenceContext,
  renderWeaponReferenceContext,
  renderModelResourceContext,
  renderResourceSyncContext,
  renderReferenceRow,
  renderTransformPathGuidanceContext,
  findResourceByKey,
  collectResourceReferences,
  formatDiagnosticText,
  findSocketById,
  findTraceById,
  normalizeInspectorTarget,
  defaultSocketPoseParent,
  normalizeContactPoint,
  editableFields,
  withEmptyOption,
  bodyPartOptions,
  bodyPartDamageMemberOptions,
  bodyPartDamageZoneOptions,
  contactPointStableIdSuggestions,
  contactLayerMaskOptions,
  socketOptions,
  traceOptions,
  modelResourceOptions,
  animationResourceOptions,
  animationGroupOptions,
  weaponOptions,
  bodyPartIdSuggestions,
  socketIdSuggestions,
  weaponIdSuggestions,
  traceIdSuggestions,
  colliderIdSuggestions,
  bonePathOptions,
  locatorPathOptions,
  hitZoneOptions,
  reactionGroupOptions,
  tagOptions,
  actionKeyOptions,
  collectPoseParentPaths,
  collectOptions,
  identityField,
  datalistField,
  selectField,
  hitZoneField,
  reactionGroupField,
  bonePathField,
  locatorField,
  positionField,
  modelPositionField,
  modelRotationField,
  modelScaleField,
  withWeaponModelWrapperFields,
  rotationField,
  localScaleField,
  poseParentKindField,
  poseParentPathField,
  mergeFieldHelp,
  field,
  tagsField,
  sizeField,
  colliderShapeFields,
  positiveField,
  integerField,
  renderFieldSections,
  renderFieldSectionHeading,
  renderFieldLegacy,
  normalizeFieldSpec,
  renderSelectOption,
  renderCheckboxOption,
  renderDatalist,
  renderFieldHint,
  renderTitleAttribute,
  renderPickerAttribute,
  normalizeOptions,
  safeDomId,
  isPathFieldSpec,
  getPathFieldGuidance,
  renderPathFieldViewer,
  renderField,
  commitInspectorField,
  beginInspectorUndoTransaction,
  getInspectorUndoLabel,
  readInspectorInputValue,
  clampFieldNumber,
  formatFieldValue,
  afterInspectorFieldEdited,
  shouldRefreshInspectorForField,
  requestViewportUpdate,
  shouldRebuildViewportForField,
  normalizeColliderShapeDimensions,
  normalizeColliderIdAfterEdit,
  normalizeContactPointAfterEdit,
  shouldAutoRenameColliderId,
  buildColliderAutoId,
  colliderShapeSuffix,
  hasColliderShapeSuffix,
  makeUniqueColliderId,
  renderColliderManualEditor,
  bindColliderManualEditingActions,
  describeColliderDimensions,
  applyColliderMargin,
  duplicateColliderForManualEditing,
  deleteColliderFromManualEditing,
  sanitizeConfigId,
  applyPoseParentDefault,
  firstValue,
  resetModelWrapperPose,
  syncModelWrapperRotationFromEuler,
  getPosePathFromEulerField,
  syncPoseRotationFromEuler,
  syncPoseQuaternion,
  quaternionFromEulerDegrees,
  getAbstractBoneLocalDiagnostics,
  isSocketBindingDeclared,
  renderAbstractBoneSocketDiagnostics,
  refreshAbstractBoneSocketDiagnostics,
  renderDiagnostics,
  renderImportStatus,
  renderUnityImportOperation,
  renderUnityImportUpToDateDetails,
  selectPath,
  leaveBaseAttributeWorkspace,
  findTarget,
  target,
  normalizeIssuePath
});
