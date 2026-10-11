// The browser supplies reference bone frames; the Authoring SDK owns template rules and dimensions.
let reference = null;
let mappings = {};
let lastResult = null;
let busy = false;
let requestGeneration = 0;
let referenceIdentity = "";

function setHumanoidHurtboxReference(value) {
  const changed = value && referenceIdentity !== value.key;
  reference = value;
  if (!value) requestGeneration++;
  if (changed) { referenceIdentity = value.key; mappings = {}; lastResult = null; requestGeneration++; }
  updateHumanoidHurtboxButton();
  if (value) globalThis.renderInspector?.();
}

function updateHumanoidHurtboxButton() {
  const button = document.getElementById("addHumanoidHurtboxesButton");
  const disabled = busy || !state.canWrite || !reference?.bones?.length
    || reference.packageRelative !== state.packageRelative || state.viewportMode === "weapon";
  const reason = busy ? "正在补齐人形受击体…" : !state.canWrite ? "当前模式不可编辑" : disabled
    ? "等待当前角色模型的参考骨骼加载完成" : "";
  for (const control of [button, ...document.querySelectorAll("[data-humanoid-add], [data-humanoid-retry]")].filter(Boolean)) {
    control.disabled = disabled;
    control.title = reason || "按参考姿态补齐 11 个人形模板部位；保留已有受击体";
  }
  document.querySelectorAll("[data-humanoid-status]").forEach(status => { status.textContent = reason; });
}

function renderHumanoidHurtboxTemplate() {
  if (!lastResult || !reference || reference.packageRelative !== state.packageRelative) return "";
  const options = reference.bones.map(bone => ({ value: bone.path, label: bone.path }));
  return `<section class="reference-card" id="humanoidHurtboxTemplatePanel">
    <div class="reference-card-head"><strong>人形 11 体模板</strong><span>骨骼比例生成初始尺寸；手脚并入前臂和小腿，保存前可在视口手调。</span></div>
    ${(lastResult.issues || []).map(issue => `<p class="field-help">${escapeHtml(issue)}</p>`).join("")}
    <details ${lastResult.success ? "" : "open"}><summary>骨骼映射（可修正后补齐）</summary>
    ${(lastResult.roles || []).map(role => `<label>${escapeHtml(role.label)}<select data-humanoid-role="${escapeHtml(role.role)}">
      <option value="">请选择骨骼</option>${options.map(option => `<option value="${escapeHtml(option.value)}" ${option.value === (mappings[role.role] ?? role.bonePath) ? "selected" : ""}>${escapeHtml(option.label)}</option>`).join("")}
    </select></label>`).join("")}</details>
    <button type="button" data-humanoid-retry ${busy ? "disabled" : ""}>补齐 11 体模板</button>
  </section>`;
}

function bindHumanoidHurtboxTemplate() {
  document.querySelectorAll("[data-humanoid-role]").forEach(select => select.addEventListener("change", () => {
    mappings[select.dataset.humanoidRole] = select.value;
  }));
  document.querySelector("[data-humanoid-retry]")?.addEventListener("click", addHumanoidHurtboxes);
}

async function addHumanoidHurtboxes() {
  if (busy || !state.canWrite || !reference?.bones?.length || reference.packageRelative !== state.packageRelative
    || state.viewportMode === "weapon") return;
  const currentReference = reference;
  const packageAtStart = state.package;
  const geometry = JSON.stringify(packageAtStart.geometry);
  const generation = ++requestGeneration;
  busy = true;
  updateHumanoidHurtboxButton();
  try {
    const response = await fetch("/api/character/humanoid-hurtboxes", {
      method: "POST", headers: { "Content-Type": "application/json" },
      body: JSON.stringify({ geometry: JSON.parse(geometry), bones: currentReference.bones, boneMappings: mappings })
    });
    if (response.status === 404) throw new Error("当前 Authoring 服务尚未加载 11 体模板接口，请重启 CharacterStudio 服务。");
    const result = await response.json();
    if (!state.canWrite || generation !== requestGeneration || reference?.key !== currentReference.key || state.package !== packageAtStart
      || JSON.stringify(state.package.geometry) !== geometry) {
      state.message = "生成期间角色或几何草稿已变化，请重新点击；已有编辑已保留。";
      return;
    }
    lastResult = result;
    if (!response.ok || !result.success) {
      state.message = (result.issues || [result.message || result.error || "模板生成失败。"]).join(" ");
      selectPath("geometry/body/parts", { preserveViewport: true });
      return;
    }
    for (const role of result.roles || []) mappings[role.role] = role.bonePath;
    const added = result.addedColliders || [];
    if (!added.length) {
      state.message = "11 个模板部位都已有受击体；保留原有尺寸和配置。";
      renderInspector();
      return;
    }
    beginUndoTransaction("添加人形 11 体模板", "humanoid11:add");
    (state.package.geometry.bodyParts ||= []).push(...result.addedParts);
    (state.package.geometry.colliders ||= []).push(...added);
    state.bodyPartsPreviewScope = "all";
    state.selectedPath = `geometry/colliders/${added[0].colliderId}`;
    state.activeInspectorTab = "properties";
    state.contextDockCollapsed = true;
    commitUndoTransaction();
    state.message = `已添加 ${added.length} 个受击体，保留 ${result.preservedColliderCount} 个已有匹配受击体。可撤销；手调后点击保存。`;
    render();
  } catch (error) {
    state.message = `人形模板生成失败：${error.message}`;
  } finally {
    busy = false;
    updateHumanoidHurtboxButton();
    renderShellStatus();
  }
}

Object.assign(globalThis, { setHumanoidHurtboxReference, updateHumanoidHurtboxButton,
  renderHumanoidHurtboxTemplate, bindHumanoidHurtboxTemplate, addHumanoidHurtboxes });
