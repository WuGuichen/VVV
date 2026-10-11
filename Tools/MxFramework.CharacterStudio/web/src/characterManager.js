// CharacterStudio character package lifecycle UI: create, duplicate, and archive.

function getSelectedCharacterPackageItem() {
  return (state.packages || []).find(item => item.relative === state.packageRelative) || null;
}

function hasUnsavedCharacterManagerChanges() {
  return Boolean(globalThis.hasUnsavedCharacterStudioChanges?.());
}

async function loadCharacterIdCatalog() {
  const response = await fetch("/api/character/id-catalog");
  const result = await readJsonResponseOrText(response);
  if (!response.ok) throw new Error(result?.message || `读取角色编号池失败：HTTP ${response.status}`);
  state.characterIdCatalog = result;
  return result;
}

function setCharacterManagerError(message = "") {
  if (!el.characterManagerError) return;
  el.characterManagerError.textContent = message;
  el.characterManagerError.hidden = !message;
}

function renderCharacterManagerToolbar() {
  const selected = getSelectedCharacterPackageItem();
  const available = Boolean(state.apiAvailable);
  if (el.newCharacterButton) el.newCharacterButton.disabled = !available;
  if (el.deleteCharacterButton) el.deleteCharacterButton.disabled = !available;
  if (el.duplicateCharacterButton) el.duplicateCharacterButton.disabled = !available || !selected;
  if (el.migrateCharacterButton) {
    el.migrateCharacterButton.disabled = !available || !selected;
    el.migrateCharacterButton.textContent = state.packageWriteConflict?.operation === "migration" ? "强制迁移" : "迁移";
  }
  if (el.saveButton) {
    el.saveButton.textContent = state.packageWriteConflict?.operation === "save" ? "强制保存" : "保存";
    el.saveButton.title = state.packageWriteConflict?.operation === "save"
      ? "磁盘版本已变化；显式覆盖当前磁盘版本"
      : "校验并保存资源包草稿";
  }
  if (el.archiveCharacterButton) {
    el.archiveCharacterButton.disabled = !available || !selected || selected.canArchive === false || selected.isTemplate === true;
    el.archiveCharacterButton.title = selected?.canArchive === false || selected?.isTemplate === true
      ? "内置角色模板不可归档"
      : "归档当前角色，不删除 Unity Generated 产物";
  }
}

async function migrateCurrentCharacterPackage() {
  if (!state.apiAvailable || !state.packageRelative) return;
  if (hasUnsavedCharacterManagerChanges()) {
    state.message = "请先保存或撤销当前未保存改动，再执行版本迁移。";
    renderShellStatus();
    return;
  }

  const planResponse = await fetch(`/api/character/migration?package=${encodeURIComponent(state.packageRelative)}`);
  const planResult = await readJsonResponseOrText(planResponse);
  if (!planResponse.ok) {
    state.message = planResult?.message || `读取迁移计划失败：HTTP ${planResponse.status}`;
    renderShellStatus();
    return;
  }

  state.migrationPlan = planResult?.plan || null;
  if (!state.migrationPlan?.canMigrate) {
    state.message = `迁移被阻止：${(state.migrationPlan?.blockingReasons || []).join("；")}`;
    renderShellStatus();
    return;
  }
  if (!state.migrationPlan?.requiresMigration) {
    state.packageRevision = planResult?.revision || state.packageRevision;
    state.message = "当前角色包已经使用最新 schema。";
    renderShellStatus();
    return;
  }

  const stepCount = state.migrationPlan.steps?.length || 0;
  if (!globalThis.confirm?.(`将执行 ${stepCount} 个 schema 迁移步骤，并重新校验角色包。是否继续？`)) return;

  const force = state.packageWriteConflict?.operation === "migration";
  const response = await fetch(`/api/character/migration?package=${encodeURIComponent(state.packageRelative)}`, {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify({
      expectedRevision: state.packageRevision || planResult?.revision || "",
      expectedWeaponLibraryRevision: state.weaponLibraryRevision || planResult?.weaponLibraryRevision || "missing",
      force
    })
  });
  const result = await readJsonResponseOrText(response);
  if (!response.ok) {
    if (response.status === 409 && result?.code === "CHARACTER_PACKAGE_WRITE_CONFLICT") {
      state.packageWriteConflict = { ...result, operation: "migration" };
      state.message = "迁移前磁盘版本发生变化。点击“刷新状态”保留当前界面并查看新状态，或点击“放弃改动并重载”重新加载磁盘版本，也可以再次点击“强制迁移”。";
      renderCharacterManagerToolbar();
    } else {
      state.message = result?.message || `角色包迁移失败：HTTP ${response.status}`;
    }
    renderShellStatus();
    return;
  }

  state.packageWriteConflict = null;
  state.migrationPlan = null;
  await loadPackageState();
  state.message = `角色包迁移完成，共应用 ${result?.appliedSteps?.length || stepCount} 个步骤。`;
  render();
}

async function openCharacterManager(mode) {
  if (!state.apiAvailable) {
    state.message = "角色管理需要启动 Authoring 服务。";
    renderShellStatus();
    return;
  }
  if (hasUnsavedCharacterManagerChanges()) {
    state.message = "请先保存或撤销当前角色的未保存改动，再执行角色管理操作。";
    renderShellStatus();
    return;
  }

  const selected = getSelectedCharacterPackageItem();
  if ((mode === "duplicate" || mode === "archive") && !selected) {
    state.message = "请先选择要操作的角色包。";
    renderShellStatus();
    return;
  }
  if (mode === "archive" && (selected.canArchive === false || selected.isTemplate === true)) {
    state.message = "内置 Humanoid / Creature 模板不可归档。";
    renderShellStatus();
    return;
  }

  state.characterManagerMode = mode;
  state.characterManagerSubmitting = false;
  setCharacterManagerError("");
  el.characterManagerForm?.reset();
  el.characterManagerIdentityFields.hidden = mode === "archive";
  el.characterManagerArchiveFields.hidden = mode !== "archive";
  el.characterManagerTemplateField.hidden = mode !== "create";
  el.characterManagerSource.hidden = mode === "create";

  const currentPackageId = selected?.packageId || state.package?.manifest?.packageId || "character";
  const currentDisplayName = state.package?.manifest?.displayName || selected?.packageId || "Character";
  let catalog = state.characterIdCatalog || null;
  if (mode !== "archive") {
    try {
      catalog = await loadCharacterIdCatalog();
    } catch (error) {
      state.message = error instanceof Error ? error.message : String(error);
      renderShellStatus();
      return;
    }
  }
  if (mode === "create") {
    el.characterManagerTitle.textContent = "新建角色";
    el.characterManagerSummary.textContent = "从标准模板创建 Authoring package 和对应的 ArtSource 源目录。";
    el.characterManagerPackageId.value = catalog?.nextCandidate || "等待分配";
    el.characterManagerDisplayName.value = "";
    el.characterManagerTemplate.value = "humanoid";
    el.submitCharacterManagerButton.textContent = "创建角色";
  } else if (mode === "duplicate") {
    el.characterManagerTitle.textContent = "复制角色";
    el.characterManagerSummary.textContent = "复制当前角色包及其角色自有 ArtSource，并生成新的稳定身份。";
    el.characterManagerSource.textContent = `来源：${currentPackageId} · ${state.packageRelative}`;
    el.characterManagerPackageId.value = catalog?.nextCandidate || "等待分配";
    el.characterManagerDisplayName.value = `${currentDisplayName} Copy`;
    el.submitCharacterManagerButton.textContent = "复制角色";
  } else {
    el.characterManagerTitle.textContent = "归档角色";
    el.characterManagerSummary.textContent = "把角色从活动创作目录移入 Archive；不会删除 Unity Generated 产物。";
    el.characterManagerSource.textContent = `当前角色：${currentPackageId} · ${state.packageRelative}`;
    el.characterManagerArchiveWarning.textContent = `将归档 ${currentPackageId} 的 Authoring package 与 ArtSource。生成的 Prefab、ResourceCatalog 和 CharacterAction 文件会继续保留，后续可单独清理。`;
    el.characterManagerArchiveConfirm.checked = false;
    el.submitCharacterManagerButton.textContent = "确认归档";
  }

  el.submitCharacterManagerButton.disabled = false;
  el.characterManagerOverlay.hidden = false;
  if (mode === "archive") el.characterManagerArchiveConfirm.focus();
  else el.characterManagerDisplayName.focus();
}

function closeCharacterManager() {
  if (state.characterManagerSubmitting) return;
  state.characterManagerMode = "";
  setCharacterManagerError("");
  if (el.characterManagerOverlay) el.characterManagerOverlay.hidden = true;
}

async function submitCharacterManager(event) {
  event?.preventDefault();
  if (state.characterManagerSubmitting) return;
  const mode = state.characterManagerMode;
  const selected = getSelectedCharacterPackageItem();
  if (!mode) return;

  let endpoint = "";
  let payload = {};
  if (mode === "archive") {
    if (!el.characterManagerArchiveConfirm.checked) {
      setCharacterManagerError("请先确认归档范围和 Generated 产物保留规则。");
      return;
    }
    endpoint = "/api/character/archive";
    payload = {
      package: state.packageRelative,
      confirmPackageId: selected?.packageId || state.package?.manifest?.packageId || ""
    };
  } else {
    const displayName = String(el.characterManagerDisplayName.value || "").trim();
    if (!displayName) {
      setCharacterManagerError("显示名称不能为空。");
      return;
    }
    const idCatalogRevision = Number(state.characterIdCatalog?.revision || 0);
    if (mode === "create") {
      endpoint = "/api/character/create";
      payload = { displayName, template: el.characterManagerTemplate.value || "humanoid", idCatalogRevision };
    } else {
      endpoint = "/api/character/duplicate";
      payload = { sourcePackage: state.packageRelative, displayName, idCatalogRevision };
    }
  }

  state.characterManagerSubmitting = true;
  el.submitCharacterManagerButton.disabled = true;
  setCharacterManagerError("");
  try {
    const response = await fetch(endpoint, {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify(payload)
    });
    const result = await readJsonResponseOrText(response);
    if (!response.ok) {
      setCharacterManagerError(result?.message || result?.error || `角色管理请求失败：HTTP ${response.status}`);
      return;
    }

    state.characterManagerSubmitting = false;
    el.characterManagerOverlay.hidden = true;
    state.characterManagerMode = "";
    if (mode === "archive") {
      state.userSelectedPackage = false;
      state.packageRelative = "";
      await loadAll();
      const retained = Array.isArray(result?.generatedOutputsRetained) ? result.generatedOutputsRetained.length : 0;
      state.message = `角色已归档：${result?.packageId || payload.confirmPackageId}。${retained} 个 Generated 保留路径均未执行删除。`;
    } else {
      state.packageRelative = result?.packageRelative || state.packageRelative;
      state.userSelectedPackage = true;
      await loadAll();
      const allocatedId = result?.allocatedCharacterId || "未知编号";
      state.message = mode === "create"
        ? `角色已创建：${allocatedId} · ${payload.displayName}。Authoring package 与 ArtSource 已就绪。`
        : `角色已复制：${allocatedId} · ${payload.displayName}。新角色已切换为当前包。`;
    }
    renderShellStatus();
    renderCharacterManagerToolbar();
  } catch (error) {
    setCharacterManagerError(error instanceof Error ? error.message : String(error));
  } finally {
    state.characterManagerSubmitting = false;
    if (el.submitCharacterManagerButton) el.submitCharacterManagerButton.disabled = false;
  }
}

function registerCharacterManagerEvents() {
  registerCharacterDeletionEvents();
  el.newCharacterButton?.addEventListener("click", () => openCharacterManager("create"));
  el.duplicateCharacterButton?.addEventListener("click", () => openCharacterManager("duplicate"));
  el.archiveCharacterButton?.addEventListener("click", () => openCharacterManager("archive"));
  el.migrateCharacterButton?.addEventListener("click", migrateCurrentCharacterPackage);
  el.closeCharacterManagerButton?.addEventListener("click", closeCharacterManager);
  el.cancelCharacterManagerButton?.addEventListener("click", closeCharacterManager);
  el.characterManagerForm?.addEventListener("submit", submitCharacterManager);
  el.characterManagerOverlay?.addEventListener("click", event => {
    if (event.target === el.characterManagerOverlay) closeCharacterManager();
  });
  document.addEventListener("keydown", event => {
    if (event.key === "Escape" && !el.characterManagerOverlay?.hidden) closeCharacterManager();
  });
  renderCharacterManagerToolbar();
}

Object.assign(globalThis, {
  openCharacterManager,
  closeCharacterManager,
  submitCharacterManager,
  registerCharacterManagerEvents,
  renderCharacterManagerToolbar,
  migrateCurrentCharacterPackage
});

const characterDeletion = { plan: null, busy: false, applying: false, generation: 0 };
const deleteElement = suffix => document.getElementById(`characterDelete${suffix}`);

function updateCharacterDeletionControls() {
  const id = deleteElement('Id').value.trim();
  deleteElement('Inspect').disabled = characterDeletion.busy || !/^[0-9]{6}$/.test(id) || id === '000000';
  deleteElement('Id').disabled = characterDeletion.busy;
  deleteElement('Cancel').disabled = characterDeletion.applying;
  deleteElement('Apply').disabled = characterDeletion.busy || characterDeletion.plan?.status !== 'ready'
    || characterDeletion.plan?.characterIds?.[0] !== id || deleteElement('Confirm').value.trim() !== id
    || !deleteElement('EditorsClosed').checked;
}

function openCharacterDeletion() {
  if (!state.apiAvailable || hasUnsavedCharacterManagerChanges()) {
    state.message = '请先保存或撤销未保存改动，并连接 Authoring 服务。';
    renderShellStatus(); return;
  }
  characterDeletion.generation++;
  characterDeletion.plan = null; characterDeletion.busy = false; characterDeletion.applying = false;
  deleteElement('Id').value = getSelectedCharacterPackageItem()?.packageId || '';
  deleteElement('Confirm').value = ''; deleteElement('EditorsClosed').checked = false;
  deleteElement('Plan').textContent = '点击“检查删除范围”后可查看待删除目录及引用检查结果。';
  deleteElement('Error').textContent = '';
  updateCharacterDeletionControls(); deleteElement('Dialog').showModal(); deleteElement('Id').focus();
}

async function inspectCharacterDeletion() {
  if (characterDeletion.busy) return;
  const id = deleteElement('Id').value.trim(), generation = ++characterDeletion.generation;
  characterDeletion.busy = true; characterDeletion.plan = null;
  deleteElement('Confirm').value = ''; deleteElement('EditorsClosed').checked = false;
  deleteElement('Error').textContent = ''; deleteElement('Plan').textContent = '正在检查文件和引用…';
  const started = Date.now();
  const progressTimer = setInterval(() => {
    if (generation === characterDeletion.generation)
      deleteElement('Plan').textContent = `正在检查文件和引用，已等待 ${Math.floor((Date.now() - started) / 1000)} 秒…`;
  }, 1000);
  updateCharacterDeletionControls();
  try {
    const response = await fetch('/api/character/delete-plan', { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify({ characterId: id }) });
    const plan = await readJsonResponseOrText(response);
    if (generation !== characterDeletion.generation) return;
    if (!response.ok) throw new Error(plan?.error || plan?.message || '删除检查失败');
    characterDeletion.plan = plan;
    const blockers = plan.blockingReferences || [];
    const sharedEdits = plan.sharedEdits || [];
    const cleanup = sharedEdits.length ? `<strong>确认删除时一并清理</strong><ul>${sharedEdits.map(edit => `<li>${edit.kind === 'emptyTacticalAcquisition' ? '空战术获取记录' : edit.kind === 'characterRuntimeRegistration' ? '角色运行资源登记' : '角色资源登记'}：${Number(edit.recordCount)} 条<br><code>${escapeHtml(edit.path)}</code></li>`).join('')}</ul><p>保留这些文件及其他角色的记录。</p>` : '';
    const heading = plan.status === 'blocked' ? `仍有 ${blockers.length} 处引用，请先解除后重新检查。`
      : plan.status === 'already_deleted' ? '该角色已删除，没有剩余内容。'
      : `将删除 ${plan.fileCount} 个文件，合计 ${(Number(plan.bytes) / 1048576).toFixed(1)} MB。`;
    deleteElement('Plan').innerHTML = `<p>${escapeHtml(heading)}</p>${blockers.length ? `<strong>阻止删除的引用</strong><ul>${blockers.map(b => `<li><code>${escapeHtml(b.path)}</code></li>`).join('')}</ul>` : ''}${cleanup}<details${blockers.length || sharedEdits.length ? '' : ' open'}><summary>删除目录（${(plan.paths || []).length} 处）</summary><ul>${(plan.paths || []).map(p => `<li><code>${escapeHtml(p)}</code></li>`).join('')}</ul></details>`;
  } catch (error) {
    if (generation === characterDeletion.generation) { deleteElement('Error').textContent = error.message; deleteElement('Plan').textContent = '检查未完成，未删除任何文件。'; }
  } finally {
    clearInterval(progressTimer);
    if (generation === characterDeletion.generation) { characterDeletion.busy = false; updateCharacterDeletionControls(); }
  }
}

async function applyCharacterDeletion() {
  updateCharacterDeletionControls();
  if (deleteElement('Apply').disabled || hasUnsavedCharacterManagerChanges()) return;
  const plan = characterDeletion.plan, id = plan.characterIds[0];
  characterDeletion.busy = true; characterDeletion.applying = true;
  deleteElement('Error').textContent = ''; updateCharacterDeletionControls();
  try {
    const response = await fetch('/api/character/delete', { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify({ characterId: id, confirmCharacterId: deleteElement('Confirm').value.trim(), editorsClosed: deleteElement('EditorsClosed').checked, planHash: plan.planHash }) });
    const result = await readJsonResponseOrText(response);
    if (!response.ok || !['deleted', 'already_deleted'].includes(result?.status)) {
      const remaining = result?.remainingPaths?.join('\n');
      throw new Error(remaining ? `部分内容未能删除，请重新检查后重试：\n${remaining}` : result?.error || result?.message || '删除失败，请重新检查范围。');
    }
    deleteElement('Dialog').close();
    if (getSelectedCharacterPackageItem()?.packageId === id) { state.userSelectedPackage = false; state.packageRelative = ''; }
    await loadAll();
    state.message = `角色 ${id} 已彻底删除，编号永久停用。`; renderShellStatus(); renderCharacterManagerToolbar();
  } catch (error) { deleteElement('Error').textContent = error.message; }
  finally { characterDeletion.plan = null; characterDeletion.busy = false; characterDeletion.applying = false; updateCharacterDeletionControls(); }
}

function registerCharacterDeletionEvents() {
  el.deleteCharacterButton?.addEventListener('click', openCharacterDeletion);
  deleteElement('Inspect')?.addEventListener('click', inspectCharacterDeletion);
  deleteElement('Apply')?.addEventListener('click', applyCharacterDeletion);
  deleteElement('Cancel')?.addEventListener('click', () => { if (!characterDeletion.applying) deleteElement('Dialog').close(); });
  deleteElement('Dialog')?.addEventListener('cancel', event => { if (characterDeletion.applying) event.preventDefault(); });
  deleteElement('Dialog')?.addEventListener('close', () => { characterDeletion.generation++; characterDeletion.plan = null; });
  deleteElement('Id')?.addEventListener('input', () => {
    characterDeletion.generation++; characterDeletion.plan = null;
    deleteElement('Plan').textContent = '角色编号已变化，请重新检查删除范围。'; updateCharacterDeletionControls();
  });
  deleteElement('Confirm')?.addEventListener('input', updateCharacterDeletionControls);
  deleteElement('EditorsClosed')?.addEventListener('change', updateCharacterDeletionControls);
}
