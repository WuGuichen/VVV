// Source operations retain editor drafts. Only server package changes are merged after synchronization.
let importMaterialsOperationId = 0;

function sourceValueEqual(left, right) {
  if (left === right) return true;
  if (!left || !right || typeof left !== "object" || typeof right !== "object" || Array.isArray(left) !== Array.isArray(right)) return false;
  const a = Object.keys(left), b = Object.keys(right);
  return a.length === b.length && a.every(key => Object.prototype.hasOwnProperty.call(right, key) && sourceValueEqual(left[key], right[key]));
}

function sourceCopy(value) {
  return value === undefined ? undefined : JSON.parse(JSON.stringify(value));
}

function sourceArrayIdentity(arrays, path) {
  const items = arrays.flat();
  if (!items.length) return null;
  if (["resourceKeys", "configFiles", "tags"].includes(path.split(".").pop()) && items.every(item => typeof item === "string")) return item => item;
  for (const key of ["resourceKey", "colliderId", "socketId", "partId", "traceId", "attachmentId", "profileId", "slotId", "stableId", "id"]) {
    if (items.every(item => item && typeof item === "object" && !Array.isArray(item) && typeof item[key] === "string" && item[key])
      && arrays.every(array => new Set(array.map(item => item[key])).size === array.length)) return item => item[key];
  }
  return null;
}

function mergeSourceSyncDelta(base, local, incoming, conflicts = [], path = "package", incomingWins = false) {
  if (sourceValueEqual(base, incoming) || sourceValueEqual(local, incoming)) return local;
  if (Array.isArray(base) && Array.isArray(local) && Array.isArray(incoming)) {
    const identity = sourceArrayIdentity([base, local, incoming], path);
    if (identity) {
      const b = new Map(base.map(item => [identity(item), item])), l = new Map(local.map(item => [identity(item), item])), r = new Map(incoming.map(item => [identity(item), item]));
      const localOrderChanged = !sourceValueEqual(local.map(identity).filter(key => b.has(key)), base.map(identity).filter(key => l.has(key)));
      const order = [...new Set((localOrderChanged ? [...local, ...incoming] : [...incoming, ...local]).map(identity))];
      const merged = order.map(key => mergeSourceSyncDelta(b.get(key), l.get(key), r.get(key), conflicts, `${path}[${key}]`, incomingWins)).filter(item => item !== undefined);
      local.splice(0, local.length, ...merged);
      return local;
    }
  }
  const object = value => value && typeof value === "object" && !Array.isArray(value);
  if (object(local) && object(incoming) && (object(base) || base === undefined)) {
    for (const key of new Set([...Object.keys(base || {}), ...Object.keys(incoming)])) {
      if (["__proto__", "constructor", "prototype"].includes(key)) continue;
      const value = mergeSourceSyncDelta(base?.[key], local[key], incoming[key], conflicts, `${path}.${key}`, incomingWins);
      if (value === undefined) delete local[key];
      else local[key] = value;
    }
    return local;
  }
  if (incomingWins || sourceValueEqual(local, base)) return sourceCopy(incoming);
  conflicts.push(path);
  return local;
}

function importMaterialSourceIdentity(item, root = "") {
  const source = item?.source || {};
  const target = item?.target || {};
  return JSON.stringify([normalizeImportMaterialPath(root), source.kind || "file", source.scope || "package",
    normalizeImportMaterialPath(item?.sourcePath || source.path || ""), getResourceSelectionKey(source.selection) || "",
    item?.presetId || "", item?.resourceKey || target.resourceKey || "", item?.bindTarget || target.bindTarget || "",
    item?.slot || target.slot || "", item?.textureSemantic || target.textureSemantic || "",
    item?.materialSurfaceMode || target.materialSurfaceMode || "", item?.alphaCutoff ?? target.alphaCutoff ?? 0.5]);
}

function getCheckedImportMaterialStatus(status, item, root, checkedRoot) {
  const identity = importMaterialSourceIdentity(item, root);
  const checkedIdentity = status?.checkedSourceIdentity || (status?.item ? importMaterialSourceIdentity(status.item, checkedRoot) : "");
  if (!checkedIdentity || checkedIdentity !== identity) return { id: item.id, item: sourceCopy(item), status: "NeedsCheck", checkedSourceIdentity: checkedIdentity,
    checkOutdated: true, message: "当前来源选择尚未检查；保存来源配置后可取得新的文件检查结果。" };
  return { ...status, item: sourceCopy(item), checkOutdated: false, checkedSourceIdentity: identity };
}

function applyCheckedImportMaterials(payload, preserveDocument = false) {
  const current = sourceCopy(getImportMaterialsDocument());
  const dirty = Boolean(state.importMaterialsDirty);
  const fresh = normalizeImportMaterialsPayload(payload);
  if (preserveDocument) {
    const statuses = new Map(fresh.items.map(item => [item.id, item]));
    fresh.items = current.items.map(item => getCheckedImportMaterialStatus(statuses.get(item.id), item, current.sourceRoot, fresh.checkedSourceRoot));
    fresh.document = current;
    fresh.integrityDraftPending = fresh.items.some(item => item.checkOutdated) || current.items.length !== (payload.document?.items || []).length;
    fresh.summary = summarizeImportMaterialStatuses(fresh.items, fresh.integrityReceipt);
  }
  state.importMaterials = fresh;
  state.importMaterialsDirty = preserveDocument && dirty;
  if (!state.importMaterialsDirty) state.importMaterialsTemplateStaged = false;
  reconcileImportMaterialSelection();
}

function isImportMaterialsBusy() {
  return Boolean(state.importMaterialsOperation?.packageRelative === state.packageRelative);
}

async function runImportMaterialsOperation(kind, action) {
  if (isImportMaterialsBusy()) return false;
  const operation = { id: ++importMaterialsOperationId, kind, packageRelative: state.packageRelative, package: state.package };
  state.importMaterialsOperation = operation;
  const current = () => state.importMaterialsOperation === operation && state.packageRelative === operation.packageRelative && state.package === operation.package;
  renderImportMaterialsPanel();
  try { return await action(operation, current); }
  catch (error) {
    if (current()) state.message = `来源操作未完成，当前草稿已保留：${error.message || error}`;
    return false;
  } finally {
    if (state.importMaterialsOperation === operation) {
      state.importMaterialsOperation = null;
      renderImportMaterialsPanel();
      renderShellStatus();
      globalThis.persistCharacterStudioSessionState?.();
    }
  }
}

async function requestImportMaterialsJson(path, body) {
  const response = await fetch(path, body === undefined ? undefined : {
    method: "POST", headers: { "Content-Type": "application/json" }, body: JSON.stringify(body)
  });
  const payload = await response.json();
  return { ok: response.ok, status: response.status, payload };
}

async function checkCharacterSourceSelection() {
  return runImportMaterialsOperation("check", async (operation, current) => {
    const result = await requestImportMaterialsJson(`/api/character/import-materials?package=${encodeURIComponent(operation.packageRelative)}`);
    if (!current()) return false;
    if (!result.ok || !result.payload?.document) throw new Error(result.payload?.message || "无法读取来源检查结果");
    applyCheckedImportMaterials(result.payload, Boolean(state.importMaterialsDirty));
    state.message = state.importMaterialsDirty ? "已更新已保存来源的检查结果；未保存的来源选择保持不变。" : "来源检查已更新。";
    renderContextDockState();
    return true;
  });
}

async function saveCharacterSourceSelection(operation, current, options = {}) {
  const submitted = prepareImportMaterialsDocumentForSave(getImportMaterialsDocument());
  const result = await requestImportMaterialsJson("/api/character/import-materials/save", { package: operation.packageRelative, document: submitted });
  if (!current()) return false;
  if (!result.ok || !result.payload?.document) throw new Error(result.payload?.message || "来源配置保存失败");
  const editedDuringSave = !sourceValueEqual(submitted, prepareImportMaterialsDocumentForSave(getImportMaterialsDocument()));
  applyCheckedImportMaterials(result.payload, editedDuringSave);
  if (!options.suppressMessage) state.message = editedDuringSave
    ? "本次提交的来源配置已保存；保存期间的新编辑仍保留为草稿。" : "来源配置已保存，尚未执行资源同步或 Unity 导入。";
  return true;
}

function applySourceSyncPackageChanges(before, after, initialRevision, documentWitnesses = {}) {
  const conflicts = [];
  mergeSourceSyncDelta(before.package, sourceCopy(state.package), after.package, conflicts);
  const sameRevision = !initialRevision || !before.revision || initialRevision === before.revision;
  const advancePackage = sameRevision && conflicts.length === 0;
  const advanceDocuments = [];
  const documentConflicts = [];
  if (advancePackage) for (const field of ["bodyPartDamage", "bodyPresence"]) {
    const document = state[field];
    if (!document || document.packageRevision === after.revision) continue;
    const witness = documentWitnesses[field];
    const provenUnchanged = Boolean(before.revision && after.revision) && witness?.before && witness?.after
      && Object.prototype.hasOwnProperty.call(witness.before, "document") && Object.prototype.hasOwnProperty.call(witness.after, "document")
      && witness.before.packageRevision === before.revision && witness.after.packageRevision === after.revision
      && witness.before.state === witness.after.state && sourceValueEqual(witness.before.document, witness.after.document);
    if (provenUnchanged && (!document.packageRevision || document.packageRevision === before.revision)) advanceDocuments.push(field);
    else if (document.document) documentConflicts.push(`${field === "bodyPartDamage" ? "部位伤害" : "Body Presence"}的版本需要核对，草稿已保留`);
  }
  const rebase = (json, incomingWins = false) => {
    if (!json) return json;
    const value = JSON.parse(json);
    return JSON.stringify(mergeSourceSyncDelta(before.package, value, after.package, [], "package", incomingWins));
  };
  // Build every transformed snapshot before mutating the live draft, so invalid history cannot cause a partial merge.
  const cleanPackageJson = rebase(state.cleanPackageJson, true);
  const rebasePartWitness = json => {
    if (!json || !advanceDocuments.includes("bodyPartDamage")) return json;
    const document = JSON.parse(json);
    if (document) document.packageRevision = after.revision;
    return JSON.stringify(document);
  };
  const rebaseEntry = entry => ({ ...entry, beforePackage: rebase(entry.beforePackage), afterPackage: rebase(entry.afterPackage),
    beforeBodyPartDamage: rebasePartWitness(entry.beforeBodyPartDamage), afterBodyPartDamage: rebasePartWitness(entry.afterBodyPartDamage) });
  const undo = (state.undoStack || []).map(rebaseEntry), redo = (state.redoStack || []).map(rebaseEntry);
  const active = state.activeUndoTransaction ? rebaseEntry(state.activeUndoTransaction) : null;
  mergeSourceSyncDelta(before.package, state.package, after.package);
  state.cleanPackageJson = cleanPackageJson;
  state.undoStack = undo; state.redoStack = redo; state.activeUndoTransaction = active;
  if (advancePackage) {
    state.packageRevision = after.revision || state.packageRevision;
    for (const field of advanceDocuments) state[field].packageRevision = state.packageRevision;
  }
  state.sourceSyncConflictPaths = [...new Set([...conflicts, ...documentConflicts])];
  if (!sameRevision) state.sourceSyncConflictPaths.unshift("同步前角色包已有外部版本变化");
  if (state.sourceSyncConflictPaths.length && !state.packageRevision && before.revision) state.packageRevision = before.revision;
  if (state.sourceSyncConflictPaths.length) state.externalRefreshConflict = { parts: ["角色包"], detectedAt: new Date().toISOString() };
  refreshDirtyFromCleanSnapshot();
  state.validation = after.validation || state.validation;
  state.authoringSources = after.authoringSources || state.authoringSources;
  state.importResult = after.importReport || state.importResult;
  if (after.unityResourceCatalog) state.unityResourceCatalog = after.unityResourceCatalog;
  return state.sourceSyncConflictPaths;
}

async function readSourceSyncDocumentWitnesses(packageRelative, fields) {
  const endpoints = { bodyPartDamage: "body-part-damage", bodyPresence: "body-presence" };
  const results = await Promise.allSettled(fields.map(field => requestImportMaterialsJson(`/api/character/${endpoints[field]}?package=${encodeURIComponent(packageRelative)}`)));
  return Object.fromEntries(fields.map((field, index) => [field, results[index].status === "fulfilled" && results[index].value.ok
    ? results[index].value.payload : null]));
}

async function syncCharacterSourceSelection(options = {}) {
  return runImportMaterialsOperation("sync", async (operation, current) => {
    const initialRevision = state.packageRevision || "";
    const documentFields = ["bodyPartDamage", "bodyPresence"].filter(field => state[field]);
    const [before, beforeDocuments] = await Promise.all([
      requestImportMaterialsJson(`/api/character/state?package=${encodeURIComponent(operation.packageRelative)}`),
      readSourceSyncDocumentWitnesses(operation.packageRelative, documentFields)
    ]);
    if (!current()) return false;
    if (!before.ok || !before.payload?.package) throw new Error("无法取得同步前角色状态，未执行同步");
    if (state.importMaterialsDirty || !state.importMaterials?.sourceManifestExists) {
      state.message = "正在保存来源选择，然后同步到角色资源包…";
      renderShellStatus();
      if (!await saveCharacterSourceSelection(operation, current, { suppressMessage: true })) return false;
      if (state.importMaterialsDirty) { state.message = "保存期间又有新的来源编辑；草稿已保留，请再次同步。"; return false; }
    }
    const result = await requestImportMaterialsJson("/api/character/import-materials/sync", {
      package: operation.packageRelative, allChanged: Boolean(options.allChanged), itemIds: options.itemIds || []
    });
    if (!current()) return false;
    // A 409 may contain successful partial writes plus blocking diagnostics. Keep and display its evidence.
    if (!result.payload?.state) throw new Error(result.payload?.message || "来源同步失败");
    const after = await requestImportMaterialsJson(`/api/character/state?package=${encodeURIComponent(operation.packageRelative)}`);
    if (!current()) return false;
    if (!after.ok || !after.payload?.package) throw new Error("同步已返回，但新角色状态读取失败；当前草稿未替换，请刷新状态后核对");
    const [metadata, afterDocuments] = await Promise.all([Promise.allSettled([
      requestImportMaterialsJson(`/api/character/readiness?package=${encodeURIComponent(operation.packageRelative)}`),
      requestImportMaterialsJson(`/api/authoring/resources?package=${encodeURIComponent(operation.packageRelative)}`),
      requestImportMaterialsJson(`/api/authoring/resources/resource-plan?package=${encodeURIComponent(operation.packageRelative)}`)
    ]), readSourceSyncDocumentWitnesses(operation.packageRelative, documentFields)]);
    if (!current()) return false;
    const ui = globalThis.captureCharacterStudioUiSnapshot?.();
    const witnesses = Object.fromEntries(documentFields.map(field => [field, { before: beforeDocuments[field], after: afterDocuments[field] }]));
    const conflicts = applySourceSyncPackageChanges(before.payload, after.payload, initialRevision, witnesses);
    applyCheckedImportMaterials(result.payload.state, Boolean(state.importMaterialsDirty));
    const values = metadata.map(item => item.status === "fulfilled" && item.value.ok ? item.value.payload : null);
    state.readiness = values[0];
    if (typeof normalizeResourceLibraryPayload === "function") state.resourceLibrary = normalizeResourceLibraryPayload(values[1]);
    if (typeof normalizeResourcePlanPayload === "function") state.resourcePlan = normalizeResourcePlanPayload(values[2]);
    const blocker = result.payload.state.integrityReceipt?.diagnostics?.find(item => item.severity === "Error");
    state.message = conflicts.length ? "来源已同步；部分草稿改动或文档版本需要核对，已保留编辑和撤销记录，请查看来源页的详情。"
      : result.ok && result.payload.success ? "已同步到角色资源包，未保存的角色编辑与撤销记录已保留。Unity 导入是后续独立步骤。"
        : `来源同步返回阻塞，草稿已保留${blocker?.message ? `：${blocker.message}` : "。请查看检查详情。"}`;
    render();
    globalThis.restoreCharacterStudioDomState?.(ui);
    return result.ok && result.payload.success && conflicts.length === 0;
  });
}

Object.assign(globalThis, { sourceValueEqual, sourceCopy, sourceArrayIdentity, mergeSourceSyncDelta,
  importMaterialSourceIdentity, getCheckedImportMaterialStatus, applyCheckedImportMaterials,
  isImportMaterialsBusy, runImportMaterialsOperation, requestImportMaterialsJson, checkCharacterSourceSelection,
  saveCharacterSourceSelection, applySourceSyncPackageChanges, readSourceSyncDocumentWitnesses, syncCharacterSourceSelection });
