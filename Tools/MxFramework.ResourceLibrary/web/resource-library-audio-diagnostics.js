// Audio package, diagnostics, metadata, and copy/export helpers.
function groupAudioRowsByBank(rows) {
  const groups = new Map();
  for (const row of rows) {
    for (const bank of row.banks) {
      if (!groups.has(bank)) groups.set(bank, []);
      groups.get(bank).push(row);
    }
  }
  return Array.from(groups.entries())
    .sort((left, right) => compareText(left[0], right[0]))
    .map(([bank, bankRows]) => ({ bank, rows: bankRows.sort(compareAudioRows) }));
}

function getAudioBankOptions(rows) {
  const banks = Array.from(new Set(rows.flatMap(row => row.banks))).sort(compareText);
  return [["all", "全部"], ...banks.map(bank => [bank, bank])];
}

function getPreferredAudioBrowseKind() {
  const item = getBrowserItems().find(isAudioResourceItem);
  return item?.kind || "audio";
}

function compareAudioRows(left, right) {
  return compareText(left.banks[0] || "", right.banks[0] || "")
    || compareText(left.fmodEventPath || left.displayName, right.fmodEventPath || right.displayName);
}

function getAudioPlanEntriesForRow(item, banks, audioCueId, audioEventDefinitionId, fmodEventPath) {
  const rows = [...getPlanMembership(item).filter(plan => stringValue(pick(plan, "group", "groupName")).toLowerCase() === "audio")];
  const audioGroup = getPlanGroups().find(group => group.label === "Audio");
  if (audioGroup) {
    for (const resource of audioGroup.resources) {
      if (audioPlanResourceMatches(resource, item, banks, audioCueId, audioEventDefinitionId, fmodEventPath)) {
        rows.push({ group: "Audio", ...resource });
      }
    }
  }
  return uniqueAudioObjects(rows);
}

function audioPlanResourceMatches(resource, item, banks, audioCueId, audioEventDefinitionId, fmodEventPath) {
  const resourceValue = stringValue(pick(resource, "resourceKey", "id", "cue", "event", "eventPath", "audioCueId", "audioEventDefinitionId"));
  const bankValue = stringValue(pick(resource, "bank", "bankName"));
  if (bankValue && banks.includes(bankValue)) return true;
  return [
    item.resourceKey,
    item.runtimeResourceKey,
    item.providerResourceKey,
    item.packageResourceKey,
    item.stableId,
    item.libraryItemId,
    audioCueId,
    audioEventDefinitionId,
    fmodEventPath
  ].filter(Boolean).includes(resourceValue);
}

function findAudioCueManifestEntry(item, audioCueId, audioEventDefinitionId, fmodEventPath, fmodEventGuid) {
  const manifest = pick(state.resourcePlanPayload, "audioCueManifest") || {};
  const cues = asArray(pick(manifest, "cues"));
  return cues.find(cue => {
    const providerData = pick(cue, "providerData") || {};
    return [
      stringValue(pick(cue, "cueId")),
      stringValue(pick(cue, "stableId")),
      stringValue(pick(cue, "resourceKey")),
      stringValue(pick(cue, "eventPath")),
      stringValue(pick(cue, "audioEventDefinitionId")),
      stringValue(pick(providerData, "audioEventDefinitionId")),
      stringValue(pick(providerData, "fmodEventGuid"))
    ].filter(Boolean).some(value => [
      item.resourceKey,
      item.runtimeResourceKey,
      item.providerResourceKey,
      item.packageResourceKey,
      item.stableId,
      item.libraryItemId,
      audioCueId,
      audioEventDefinitionId,
      fmodEventPath,
      fmodEventGuid
    ].filter(Boolean).includes(value));
  }) || null;
}

function getAudioDiagnosticsForRow(item, fmodEventPath, fmodEventGuid, audioCueId, audioEventDefinitionId) {
  const rowDiagnostics = getDiagnosticsForItem(item);
  const identities = [fmodEventPath, fmodEventGuid, audioCueId, audioEventDefinitionId].filter(Boolean);
  const globalAudioDiagnostics = getAllDiagnostics().filter(diagnostic => {
    const text = [
      pick(diagnostic, "code"),
      pick(diagnostic, "message"),
      pick(diagnostic, "sourceField"),
      pick(diagnostic, "eventPath"),
      pick(diagnostic, "eventGuid"),
      pick(diagnostic, "resourceKey"),
      pick(diagnostic, "runtimeResourceKey")
    ].join(" ");
    if (!/fmod|audio/i.test(text)) return false;
    return identities.some(identity => text.includes(identity));
  });
  return mergeAudioDiagnostics(rowDiagnostics, globalAudioDiagnostics);
}

function isAudioProviderDiagnostic(diagnostic) {
  const code = stringValue(pick(diagnostic, "code")).toLowerCase();
  const message = stringValue(pick(diagnostic, "message")).toLowerCase();
  const sourceField = stringValue(pick(diagnostic, "sourceField")).toLowerCase();
  if (code.includes("audio_semantic") || code.includes("audio_audition") || code.includes("orphan_candidate")) return false;
  return code.includes("fmod")
    || code.includes("provider_unavailable")
    || sourceField.includes("fmodaudiolibrarysnapshot")
    || message.includes("fmod audio library")
    || message.includes("fmod editor cache");
}

function isAudioInfrastructureDiagnostic(diagnostic) {
  return isAudioProviderDiagnostic(diagnostic) && isAudioActionableDiagnostic(diagnostic);
}

function isAudioActionableDiagnostic(diagnostic) {
  const severity = getAudioDiagnosticSeverity(diagnostic);
  const code = stringValue(pick(diagnostic, "code")).toLowerCase();
  if (isAudioIgnoredDiagnostic(diagnostic) || code.includes("audio_audition_required")) return false;
  return severity === "error" || severity === "warning";
}

function isAudioIgnoredDiagnostic(diagnostic) {
  const code = stringValue(pick(diagnostic, "code")).toLowerCase();
  return code.includes("orphan_candidate") || code.includes("not_runtime_loadable");
}

function getAudioDiagnosticSeverity(diagnostic) {
  const value = stringValue(pick(diagnostic, "severity")).toLowerCase();
  if (value === "error") return "error";
  if (value === "warning" || value === "warn") return "warning";
  return "info";
}

function mergeAudioDiagnostics(...groups) {
  const seen = new Set();
  const result = [];
  for (const diagnostic of groups.flatMap(asArray)) {
    if (!diagnostic) continue;
    const identity = firstString(
      pick(diagnostic, "runtimeResourceKey"),
      pick(diagnostic, "resourceStableId"),
      pick(diagnostic, "libraryItemStableId"),
      pick(diagnostic, "sourceStableId"),
      pick(diagnostic, "resourceKey"),
      pick(diagnostic, "eventPath"),
      pick(diagnostic, "eventGuid"),
      pick(diagnostic, "sourceField")
    );
    const key = [
      getAudioDiagnosticSeverity(diagnostic),
      stringValue(pick(diagnostic, "code")),
      identity,
      identity ? "" : stringValue(pick(diagnostic, "message"))
    ].join("|");
    if (seen.has(key)) continue;
    seen.add(key);
    result.push(diagnostic);
  }
  return result;
}

function summarizeAudioDiagnosticHealth(diagnostics) {
  const rows = mergeAudioDiagnostics(diagnostics).filter(row => !isAudioIgnoredDiagnostic(row));
  const errorCount = rows.filter(row => getAudioDiagnosticSeverity(row) === "error").length;
  const warningCount = rows.filter(row => getAudioDiagnosticSeverity(row) === "warning" && isAudioActionableDiagnostic(row)).length;
  const infoCount = rows.filter(row => !isAudioActionableDiagnostic(row)).length;
  return {
    errorCount,
    warningCount,
    infoCount,
    actionableCount: errorCount + warningCount
  };
}

function renderAudioGroupDiagnosticBadge(rows) {
  const summary = summarizeAudioDiagnosticHealth(asArray(rows).flatMap(row => row.diagnostics));
  if (summary.errorCount > 0) return smallBadge(`${summary.errorCount} 错误`, "error");
  if (summary.warningCount > 0) return smallBadge(`${summary.warningCount} 警告`, "warn");
  if (summary.infoCount > 0) return smallBadge(`${summary.infoCount} 条信息`, "info");
  return smallBadge("校验正常", "ok");
}

function isFmodProviderUnavailableDiagnostic(diagnostic) {
  const providerId = stringValue(pick(diagnostic, "providerId")).toLowerCase();
  const code = stringValue(pick(diagnostic, "code")).toLowerCase();
  const message = stringValue(pick(diagnostic, "message")).toLowerCase();
  return (code.includes("fmod") && code.includes("unavailable"))
    || ((providerId === "fmod" || providerId.includes("fmod")) && code.includes("provider_unavailable"))
    || (message.includes("fmod") && /unavailable|missing/.test(message));
}

function summarizeAudioDiagnostics(diagnostics) {
  return asArray(diagnostics)
    .map(diagnostic => stringValue(pick(diagnostic, "message")) || stringValue(pick(diagnostic, "code")))
    .filter(Boolean)
    .slice(0, 3)
    .join("；");
}

function getAudioGlobalMetadata() {
  return pick(state.resourcesPayload, "metadata") || {};
}

function getAudioMetadataBoolean(metadata, ...keys) {
  for (const key of keys) {
    const value = pick(metadata, key);
    const parsed = parseAudioBoolean(value);
    if (parsed !== null) return parsed;
  }
  return null;
}

function getAudioPeerItems(item) {
  const peers = asArray(item?.foldedItems);
  return peers.length > 0 ? peers : item ? [item] : [];
}

function getAudioDataObjects(item) {
  const objects = [];
  for (const peer of getAudioPeerItems(item)) {
    pushAudioObject(objects, peer);
    pushAudioObject(objects, peer.raw);
    pushAudioObject(objects, peer.metadata);
    pushAudioObject(objects, pick(peer.raw, "metadata"));
    for (const binding of [...asArray(peer.providerBindings), ...asArray(pick(peer.raw, "providerBindings"))]) {
      pushAudioObject(objects, binding);
      pushAudioObject(objects, pick(binding, "providerData"));
    }
  }
  return objects;
}

function pushAudioObject(objects, value) {
  if (value && typeof value === "object" && !objects.includes(value)) objects.push(value);
}

function getAudioFirstValue(item, ...keys) {
  for (const object of getAudioDataObjects(item)) {
    for (const key of keys) {
      const value = pick(object, key);
      if (value === undefined || value === null) continue;
      if (typeof value === "boolean" || typeof value === "number") return value;
      if (String(value).trim()) return value;
    }
  }
  return "";
}

function getAudioStringList(item, ...keys) {
  for (const object of getAudioDataObjects(item)) {
    for (const key of keys) {
      const list = parseAudioStringList(pick(object, key));
      if (list.length > 0) return list;
    }
  }
  return [];
}

function parseAudioStringList(value) {
  if (value === undefined || value === null) return [];
  if (Array.isArray(value)) return uniqueStrings(value.flatMap(parseAudioStringList));
  if (typeof value === "object") {
    const label = stringValue(pick(value, "name", "Name", "path", "Path", "id", "Id")).trim();
    return label ? [label] : [];
  }
  return uniqueStrings(String(value)
    .split(/[;,|]/)
    .map(part => part.trim())
    .filter(Boolean));
}

function getAudioBoolean(item, ...keys) {
  for (const key of keys) {
    const value = getAudioFirstValue(item, key);
    const parsed = parseAudioBoolean(value);
    if (parsed !== null) return parsed;
  }
  return null;
}

function parseAudioBoolean(value) {
  if (value === undefined || value === null || value === "") return null;
  if (typeof value === "boolean") return value;
  const text = String(value).trim().toLowerCase();
  if (["true", "1", "yes", "y"].includes(text)) return true;
  if (["false", "0", "no", "n"].includes(text)) return false;
  return null;
}

function getAudioNumber(item, ...keys) {
  for (const key of keys) {
    const value = getAudioFirstValue(item, key);
    if (value === "" || value === null || value === undefined) continue;
    const number = Number(value);
    if (Number.isFinite(number)) return number;
  }
  return NaN;
}

function uniqueAudioObjects(rows) {
  const seen = new Set();
  const result = [];
  for (const row of rows) {
    const key = stableJson(row);
    if (seen.has(key)) continue;
    seen.add(key);
    result.push(row);
  }
  return result;
}

function buildAudioSummaryJson(model) {
  return {
    package: state.packageRelative,
    provider: {
      state: model.provider.label,
      snapshotPath: model.provider.snapshotPath,
      generatedAtUtc: model.provider.generatedAtUtc,
      cacheTimeUtc: model.provider.cacheTimeUtc,
      cacheValid: model.provider.cacheValid,
      stale: model.provider.stale,
      diagnostics: model.provider.diagnostics
    },
    totals: {
      events: model.rows.length,
      filteredEvents: model.filteredRows.length,
      banks: model.bankOptions.length > 0 ? model.bankOptions.length - 1 : 0,
      cues: model.cueRows.length,
      diagnostics: model.diagnosticsCount
    },
    health: model.health,
    banks: model.bankGroups.map(group => ({
      bank: group.bank,
      events: group.rows.map(row => ({
        fmodEventPath: row.fmodEventPath,
        fmodEventGuid: row.fmodEventGuid,
        audioCueId: row.audioCueId,
        audioEventDefinitionId: row.audioEventDefinitionId,
        diagnostics: summarizeAudioDiagnosticHealth(row.diagnostics).actionableCount
      }))
    })),
    cues: model.cueRows.map(row => ({
      audioCueId: row.audioCueId,
      audioEventDefinitionId: row.audioEventDefinitionId,
      fmodEventPath: row.fmodEventPath,
      audioPlan: row.inAudioPlan,
      audioCueManifest: row.inCueManifest
    }))
  };
}

function buildSelectedAudioJson(row) {
  const detail = getCurrentDetail(row.item);
  const preview = getAudioPreviewInfo(row);
  return {
    package: state.packageRelative,
    resource: {
      displayName: row.displayName,
      resourceKey: row.resourceKey,
      stableId: row.item.stableId,
      runtimeAvailability: row.runtimeAvailability
    },
    event: {
      fmodEventPath: row.fmodEventPath,
      fmodEventGuid: row.fmodEventGuid,
      banks: row.banks,
      parameters: row.parameters,
      is3D: row.is3D,
      isLoop: row.isLoop,
      isStream: row.isStream,
      lengthMs: row.lengthMs,
      minDistance: row.minDistance,
      maxDistance: row.maxDistance
    },
    semantic: getAudioSemanticDraft(row),
    cue: {
      audioCueId: row.audioCueId,
      audioEventDefinitionId: row.audioEventDefinitionId,
      audioCueManifest: row.manifestEntry
    },
    build: {
      profileStatus: getBuildProfileStatus(row.item),
      audioPlanEntries: row.audioPlanEntries
    },
    preview: {
      sourcePath: preview.path,
      sourceUrl: preview.url,
      message: preview.message
    },
    references: asArray(pick(detail, "references")).length > 0 ? asArray(pick(detail, "references")) : row.references,
    diagnostics: mergeDiagnostics(row.diagnostics, asArray(pick(detail, "diagnostics")))
  };
}

function copyAudioSummary() {
  const model = createAudioWorkspaceModel();
  copyText(JSON.stringify(buildAudioSummaryJson(model), null, 2), "已复制 Audio 状态摘要");
}

function copySelectedAudioJson() {
  const model = createAudioWorkspaceModel();
  if (!model.selectedRow) {
    copyAudioSummary();
    return;
  }
  copyText(JSON.stringify(buildSelectedAudioJson(model.selectedRow), null, 2), "已复制当前 Audio JSON");
}
