const DEFAULT_PACKAGE = "Tools/MxFramework.Authoring/samples/character-iron-vanguard";
const LAYERS = { colliders: true, sockets: true, traces: true, weapons: true };
const LOADOUTS = [
  { id: "unarmed", label: "Unarmed", slots: [] },
  { id: "single_sword", label: "Single Sword", slots: ["mainHand"] },
  { id: "sword_shield", label: "Sword Shield", slots: ["mainHand", "offHand"] }
];

const state = {
  packages: [],
  packageRelative: DEFAULT_PACKAGE,
  package: null,
  validation: null,
  compileResult: null,
  importResult: null,
  selectedPath: "manifest",
  activeLoadout: "sword_shield",
  layers: { ...LAYERS },
  dirty: false,
  canWrite: false,
  apiAvailable: false,
  message: ""
};

const el = {};

document.addEventListener("DOMContentLoaded", () => {
  for (const id of [
    "packageSelect", "reloadButton", "saveButton", "compileButton", "importButton",
    "packageSummary", "packageTree", "dirtyBadge", "loadoutTabs", "viewport",
    "inspector", "diagnostics", "importStatus", "selectionBadge", "copyReportButton",
    "subtitle"
  ]) el[id] = document.getElementById(id);

  el.reloadButton.addEventListener("click", () => loadAll());
  el.saveButton.addEventListener("click", () => savePackage());
  el.compileButton.addEventListener("click", () => compilePackage());
  el.importButton.addEventListener("click", () => importUnity());
  el.copyReportButton.addEventListener("click", () => copyReport());
  el.packageSelect.addEventListener("change", event => {
    state.packageRelative = event.target.value;
    state.selectedPath = "manifest";
    loadAll();
  });
  document.getElementById("layerToggles").addEventListener("click", event => {
    const button = event.target.closest("button[data-layer]");
    if (!button) return;
    const layer = button.dataset.layer;
    state.layers[layer] = !state.layers[layer];
    button.classList.toggle("active", state.layers[layer]);
    renderViewport();
  });

  loadAll();
});

async function loadAll() {
  state.message = "Loading package...";
  renderShellStatus();
  await loadPackages();
  await loadPackageState();
  render();
}

async function loadPackages() {
  const apiPackages = await readJson("/api/character/packages", null);
  if (Array.isArray(apiPackages) && apiPackages.length > 0) {
    state.packages = apiPackages;
    state.apiAvailable = true;
  } else {
    state.packages = [{ relative: DEFAULT_PACKAGE, packageId: "iron_vanguard", kind: "character" }];
  }
  if (!state.packages.some(pkg => pkg.relative === state.packageRelative)) {
    state.packageRelative = state.packages[0].relative;
  }
  el.packageSelect.innerHTML = state.packages.map(pkg => {
    const label = `${pkg.packageId || pkg.relative} (${pkg.version || pkg.kind || "character"})`;
    return `<option value="${escapeHtml(pkg.relative)}"${pkg.relative === state.packageRelative ? " selected" : ""}>${escapeHtml(label)}</option>`;
  }).join("");
}

async function loadPackageState() {
  const apiState = await readJson(`/api/character/state?package=${encodeURIComponent(state.packageRelative)}`, null);
  if (apiState && apiState.package) {
    state.package = clone(apiState.package);
    state.validation = apiState.validation || apiState.package.validationReport || { issues: [] };
    state.importResult = apiState.importReport || null;
    state.canWrite = Boolean(apiState.canWrite);
    state.apiAvailable = true;
    state.dirty = false;
    state.message = "Authoring server connected.";
    return;
  }

  state.package = await readStaticPackage(state.packageRelative);
  state.validation = state.package.validationReport || { issues: [] };
  state.importResult = null;
  state.canWrite = false;
  state.apiAvailable = false;
  state.dirty = false;
  state.message = "Static preview: start the Authoring server to save, compile, or import.";
}

async function readStaticPackage(root) {
  const [manifest, resourceCatalog, bodyProfile, bodyParts, colliders, sockets, attachments, traces, appConfig, validation] = await Promise.all([
    readJson(`/${root}/manifest.json`, {}),
    readJson(`/${root}/resource_catalog.json`, { entries: [] }),
    readJson(`/${root}/geometry/body_geometry.json`, {}),
    readJson(`/${root}/geometry/body_parts.json`, { bodyParts: [] }),
    readJson(`/${root}/geometry/body_colliders.json`, { colliders: [] }),
    readJson(`/${root}/geometry/sockets.json`, { sockets: [] }),
    readJson(`/${root}/geometry/weapon_attachments.json`, { attachments: [] }),
    readJson(`/${root}/geometry/traces.json`, { traces: [] }),
    readJson(`/${root}/config/character_application.json`, {}),
    readJson(`/${root}/validation/last_report.json`, { issues: [] })
  ]);
  return {
    manifest,
    resourceCatalog,
    geometry: {
      schemaVersion: bodyParts.schemaVersion || colliders.schemaVersion || "1.0",
      bodyProfile,
      bodyParts: bodyParts.bodyParts || [],
      colliders: colliders.colliders || [],
      sockets: sockets.sockets || [],
      weaponAttachments: attachments.attachments || [],
      traces: traces.traces || []
    },
    applicationConfig: appConfig,
    validationReport: validation
  };
}

function render() {
  renderShellStatus();
  renderSummary();
  renderTree();
  renderLoadouts();
  renderViewport();
  renderInspector();
  renderDiagnostics();
  renderImportStatus();
}

function renderShellStatus() {
  el.subtitle.textContent = state.message || "Character Resource Package external workstation";
  el.dirtyBadge.textContent = state.dirty ? "dirty" : "clean";
  el.dirtyBadge.className = `badge ${state.dirty ? "warn" : "ok"}`;
  el.saveButton.disabled = !state.canWrite || !state.package;
  el.compileButton.disabled = !state.apiAvailable || !state.package;
  el.importButton.disabled = !state.apiAvailable || !state.package || state.dirty || isImportBlocked();
}

function renderSummary() {
  const pkg = state.package;
  if (!pkg) {
    el.packageSummary.innerHTML = `<div class="empty">No package loaded.</div>`;
    return;
  }
  const geometry = pkg.geometry || {};
  el.packageSummary.innerHTML = [
    summaryCell("Package", pkg.manifest?.packageId || "-"),
    summaryCell("Version", pkg.manifest?.version || "-"),
    summaryCell("Resources", (pkg.resourceCatalog?.entries || []).length),
    summaryCell("Colliders", (geometry.colliders || []).length),
    summaryCell("Sockets", (geometry.sockets || []).length),
    summaryCell("Traces", (geometry.traces || []).length)
  ].join("");
}

function summaryCell(label, value) {
  return `<div><strong>${escapeHtml(String(value))}</strong>${escapeHtml(label)}</div>`;
}

function renderTree() {
  const nodes = buildTree(state.package);
  el.packageTree.innerHTML = nodes.map(node => {
    const active = node.path === state.selectedPath ? " active" : "";
    return `<button class="${active}" type="button" data-path="${escapeHtml(node.path)}" style="padding-left:${8 + node.depth * 14}px"><span class="kind">${escapeHtml(node.kind)}</span><span class="label">${escapeHtml(node.label)}</span></button>`;
  }).join("");
  el.packageTree.querySelectorAll("button[data-path]").forEach(button => {
    button.addEventListener("click", () => selectPath(button.dataset.path));
  });
}

function buildTree(pkg) {
  if (!pkg) return [];
  const g = pkg.geometry || {};
  const nodes = [
    node("manifest", "manifest", "manifest", 0),
    node("resources", "resources", "resource catalog", 0),
    ...grouped((pkg.resourceCatalog?.entries || []), "resource", entry => `resources/${entry.resourceKey || entry.localId}`, entry => entry.resourceKey || entry.relativePath || "resource", 1),
    node("config", "config", "character application", 0),
    node("geometry/body", "body", g.bodyProfile?.profileId || "body geometry", 0),
    ...grouped((g.bodyParts || []), "part", part => `geometry/body_parts/${part.partId}`, part => part.partId || "part", 1),
    ...grouped((g.colliders || []), "collider", collider => `geometry/colliders/${collider.colliderId}`, collider => collider.colliderId || "collider", 1),
    ...grouped((g.sockets || []), "socket", socket => `geometry/sockets/${socket.socketId}`, socket => socket.socketId || "socket", 1),
    ...grouped((g.weaponAttachments || []), "weapon", attachment => `geometry/weapon_attachments/${attachment.weaponId}`, attachment => `${attachment.equipSlot || "slot"}:${attachment.weaponId || "weapon"}`, 1),
    ...grouped((g.traces || []), "trace", trace => `geometry/traces/${trace.traceId}`, trace => trace.traceId || "trace", 1),
    node("validation", "validation", "validation/gates", 0),
    ...grouped((state.validation?.issues || []), "issue", (_, index) => `validation/issues/${index}`, issue => issue.code || issue.message || "issue", 1)
  ];
  return nodes;
}

function node(path, kind, label, depth) {
  return { path, kind, label, depth };
}

function grouped(items, kind, pathFn, labelFn, depth) {
  return items.map((item, index) => node(pathFn(item, index), kind, labelFn(item, index), depth));
}

function renderLoadouts() {
  el.loadoutTabs.innerHTML = LOADOUTS.map(loadout => `<button type="button" data-loadout="${loadout.id}" class="${loadout.id === state.activeLoadout ? "active" : ""}">${loadout.label}</button>`).join("");
  el.loadoutTabs.querySelectorAll("button").forEach(button => {
    button.addEventListener("click", () => {
      state.activeLoadout = button.dataset.loadout;
      renderLoadouts();
      renderViewport();
      renderDiagnostics();
    });
  });
}

function renderViewport() {
  if (!state.package) {
    el.viewport.innerHTML = `<div class="empty">No package loaded.</div>`;
    return;
  }
  const g = state.package.geometry || {};
  const selected = state.selectedPath;
  const activeSlots = new Set((LOADOUTS.find(l => l.id === state.activeLoadout) || LOADOUTS[0]).slots);
  const attachments = (g.weaponAttachments || []).filter(a => activeSlots.has(a.equipSlot));
  const traces = (g.traces || []).filter(t => activeSlots.has(t.equipSlot));
  const socketsById = Object.fromEntries((g.sockets || []).map(s => [s.socketId, s]));
  const body = g.bodyProfile || {};
  const height = Number(body.heightMeters || body.defaultCapsule?.height || 1.8);
  const radius = Number(body.radiusMeters || body.defaultCapsule?.radius || 0.35);

  const shapes = [];
  shapes.push(`<rect x="46" y="${scaleY(height)}" width="8" height="${Math.max(8, height * 38)}" rx="4" fill="#dce8ea" stroke="#91a4ad" />`);
  shapes.push(`<ellipse cx="50" cy="${scaleY(height)}" rx="${Math.max(3, radius * 12)}" ry="4" fill="#e8f0f2" stroke="#91a4ad" />`);

  if (state.layers.colliders) {
    for (const c of g.colliders || []) {
      const p = c.localPose?.position || {};
      const x = scaleX(p.x);
      const y = scaleY(p.y);
      const path = `geometry/colliders/${c.colliderId}`;
      const cls = selected === path ? "selected" : "";
      if (c.shape === "Box") {
        shapes.push(`<rect class="${cls}" data-object-path="${escapeHtml(path)}" x="${x - 4}" y="${y - 5}" width="8" height="10" fill="rgba(31,122,122,0.18)" stroke="#1f7a7a" />`);
      } else if (c.shape === "Capsule") {
        shapes.push(`<rect class="${cls}" data-object-path="${escapeHtml(path)}" x="${x - 5}" y="${y - 14}" width="10" height="28" rx="5" fill="rgba(31,122,122,0.16)" stroke="#1f7a7a" />`);
      } else {
        shapes.push(`<circle class="${cls}" data-object-path="${escapeHtml(path)}" cx="${x}" cy="${y}" r="${Math.max(3, Number(c.radius || 0.12) * 18)}" fill="rgba(31,122,122,0.16)" stroke="#1f7a7a" />`);
      }
    }
  }

  if (state.layers.sockets) {
    for (const s of g.sockets || []) {
      const p = s.localPose?.position || {};
      const x = scaleX(p.x);
      const y = scaleY(p.y);
      const path = `geometry/sockets/${s.socketId}`;
      const cls = selected === path ? "selected" : "";
      shapes.push(`<g class="${cls}" data-object-path="${escapeHtml(path)}"><line x1="${x - 4}" y1="${y}" x2="${x + 4}" y2="${y}" stroke="#b46a1f"/><line x1="${x}" y1="${y - 4}" x2="${x}" y2="${y + 4}" stroke="#b46a1f"/><circle cx="${x}" cy="${y}" r="2.4" fill="#fff" stroke="#b46a1f"/></g>`);
    }
  }

  if (state.layers.weapons) {
    for (const a of attachments) {
      const socket = socketsById[a.attachSocketId];
      const p = socket?.localPose?.position || {};
      const x = scaleX(p.x) + (a.equipSlot === "offHand" ? -8 : 8);
      const y = scaleY(p.y) - 4;
      const path = `geometry/weapon_attachments/${a.weaponId}`;
      const cls = selected === path ? "selected" : "";
      shapes.push(`<g class="${cls}" data-object-path="${escapeHtml(path)}"><rect x="${x - 3}" y="${y - 13}" width="6" height="22" rx="2" fill="rgba(180,106,31,0.18)" stroke="#b46a1f"/><text x="${x + 5}" y="${y - 9}" font-size="3" fill="#734515">${escapeHtml(a.equipSlot || "")}</text></g>`);
    }
  }

  if (state.layers.traces) {
    for (const t of traces) {
      const start = t.startPose?.position || {};
      const end = t.endPose?.position || {};
      const sx = scaleX(start.x) + 8;
      const sy = scaleY(start.y);
      const ex = scaleX(end.x) + 8;
      const ey = scaleY(end.y);
      const path = `geometry/traces/${t.traceId}`;
      const cls = selected === path ? "selected" : "";
      shapes.push(`<g class="${cls}" data-object-path="${escapeHtml(path)}"><line x1="${sx}" y1="${sy}" x2="${ex}" y2="${ey}" stroke="#c24141" stroke-width="${Math.max(0.8, Number(t.radius || 0.04) * 18)}" opacity="0.45"/><circle cx="${sx}" cy="${sy}" r="2" fill="#c24141"/><circle cx="${ex}" cy="${ey}" r="2" fill="#c24141"/></g>`);
    }
  }

  el.viewport.innerHTML = `<svg viewBox="0 0 100 100" role="img" aria-label="Character package viewport"><defs><pattern id="grid" width="10" height="10" patternUnits="userSpaceOnUse"><path d="M 10 0 L 0 0 0 10" fill="none" stroke="#edf1f3" stroke-width="0.6"/></pattern></defs><rect width="100" height="100" fill="url(#grid)"/><text x="4" y="7" font-size="3.5" fill="#61717f">${escapeHtml(state.package.manifest?.packageId || "character")}</text>${shapes.join("")}</svg>`;
  el.viewport.querySelectorAll("[data-object-path]").forEach(item => {
    item.addEventListener("click", event => {
      event.stopPropagation();
      selectPath(item.getAttribute("data-object-path"));
    });
  });
}

function renderInspector() {
  const target = findTarget(state.selectedPath);
  el.selectionBadge.textContent = target.kind || "none";
  if (!target.value) {
    el.inspector.innerHTML = `<div class="empty">Select a package object.</div>`;
    return;
  }
  const fields = editableFields(target.kind);
  if (fields.length === 0) {
    el.inspector.innerHTML = `<div class="object-title"><strong>${escapeHtml(target.label)}</strong><span>${escapeHtml(state.selectedPath)}</span></div><pre>${escapeHtml(JSON.stringify(target.value, null, 2))}</pre>`;
    return;
  }
  el.inspector.innerHTML = `<div class="object-title"><strong>${escapeHtml(target.label)}</strong><span>${escapeHtml(state.selectedPath)}</span></div><div class="field-grid">${fields.map(field => renderField(target, field)).join("")}</div>`;
  el.inspector.querySelectorAll("[data-field]").forEach(input => {
    input.addEventListener("input", () => {
      setNested(target.value, input.dataset.field, coerceValue(input.value, input.dataset.type));
      state.dirty = true;
      renderShellStatus();
      renderViewport();
    });
  });
}

function editableFields(kind) {
  if (kind === "collider") return [
    ["shape", "select", ["Capsule", "Box", "Sphere"]],
    ["partId"], ["hitZoneId"], ["localPose.position.x", "number"], ["localPose.position.y", "number"],
    ["localPose.position.z", "number"], ["size.x", "number"], ["size.y", "number"], ["size.z", "number"],
    ["radius", "number"], ["height", "number"], ["priority", "number"], ["isWeakPoint", "select", ["false", "true"]],
    ["damageMultiplierOverride", "number"]
  ];
  if (kind === "socket") return [
    ["socketId"], ["parentPartId"], ["bonePath"], ["locatorPath"], ["localPose.position.x", "number"],
    ["localPose.position.y", "number"], ["localPose.position.z", "number"], ["usage", "select", ["Weapon", "Vfx", "Camera", "Ui", "Gameplay"]],
    ["handedness", "select", ["None", "Left", "Right", "Both"]]
  ];
  if (kind === "weapon") return [
    ["weaponId"], ["equipSlot"], ["attachSocketId"], ["localGripPose.position.x", "number"],
    ["localGripPose.position.y", "number"], ["localGripPose.position.z", "number"], ["previewResourceKey"],
    ["traceId"], ["traceRadius", "number"]
  ];
  if (kind === "trace") return [
    ["traceId"], ["weaponId"], ["equipSlot"], ["startLocatorPath"], ["endLocatorPath"],
    ["startPose.position.x", "number"], ["startPose.position.y", "number"], ["startPose.position.z", "number"],
    ["endPose.position.x", "number"], ["endPose.position.y", "number"], ["endPose.position.z", "number"],
    ["radius", "number"], ["sampleRule", "select", ["LineSegment", "CapsuleSweep", "FixedSamples"]]
  ];
  return [];
}

function renderField(target, fieldSpec) {
  const [path, type = "text", options = null] = fieldSpec;
  const value = getNested(target.value, path);
  if (type === "select") {
    const normalized = typeof value === "boolean" ? String(value) : (value || "");
    return `<div class="field"><label>${escapeHtml(path)}</label><select data-field="${escapeHtml(path)}" data-type="${escapeHtml(path === "isWeakPoint" ? "boolean" : "text")}">${options.map(option => `<option value="${escapeHtml(option)}"${String(option) === String(normalized) ? " selected" : ""}>${escapeHtml(option)}</option>`).join("")}</select></div>`;
  }
  return `<div class="field"><label>${escapeHtml(path)}</label><input data-field="${escapeHtml(path)}" data-type="${escapeHtml(type)}" value="${escapeHtml(value == null ? "" : String(value))}"></div>`;
}

function renderDiagnostics() {
  const validationIssues = state.validation?.issues || [];
  const gate = state.compileResult?.gateReport;
  const gateIssues = gate?.issues || [];
  const status = state.compileResult?.status || (validationIssues.length ? "Validation" : "Ready");
  const rows = [`<div class="status-line"><strong>Status</strong><span class="badge ${isImportBlocked() ? "error" : "ok"}">${escapeHtml(status)}</span></div>`];
  if (gate) {
    rows.push(`<div class="meta">source=${escapeHtml(state.compileResult.hashes?.sourcePackageHash || "-")} mapping=${escapeHtml(state.compileResult.hashes?.resourceMappingHash || "-")}</div>`);
  }
  const issues = [...validationIssues, ...gateIssues];
  if (issues.length === 0) {
    rows.push(`<div class="empty">No diagnostics.</div>`);
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
  const report = state.importResult?.report || state.importResult || null;
  if (!report) {
    el.importStatus.innerHTML = `<div class="empty">No import report loaded.</div>`;
    return;
  }
  const operations = report.operations || [];
  el.importStatus.innerHTML = [
    `<div class="status-line"><strong>${escapeHtml(report.status || (state.importResult.success ? "Ready" : "Unknown"))}</strong><span class="badge">${escapeHtml(report.targetRootPath || state.importResult.reportOut || "-")}</span></div>`,
    `<div class="meta">added=${report.addedCount ?? "-"} updated=${report.updatedCount ?? "-"} skipped=${report.skippedCount ?? "-"} conflicts=${report.conflictCount ?? "-"}</div>`,
    ...operations.slice(0, 8).map(op => `<div class="operation-item"><strong>${escapeHtml(op.action || op.kind || "operation")}</strong><div class="meta">${escapeHtml(op.targetPath || op.sourcePath || "")}</div></div>`)
  ].join("");
}

async function savePackage() {
  if (!state.canWrite) {
    state.message = "Static preview cannot save. Start the Authoring server.";
    renderShellStatus();
    return;
  }
  const response = await fetch(`/api/character/save?package=${encodeURIComponent(state.packageRelative)}`, {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify({ package: state.package })
  });
  if (!response.ok) {
    state.message = await response.text();
    renderShellStatus();
    return;
  }
  const data = await response.json();
  state.package = data.package;
  state.validation = data.validation;
  state.dirty = false;
  state.message = "Package saved and validated.";
  render();
}

async function compilePackage() {
  const data = await readJson(`/api/character/compile?package=${encodeURIComponent(state.packageRelative)}&checkHashes=false`, null);
  if (!data) {
    state.message = "Compile failed or server unavailable.";
    renderShellStatus();
    return;
  }
  state.compileResult = data;
  state.message = `Compile status: ${data.status || "Unknown"}`;
  renderDiagnostics();
}

async function importUnity() {
  if (isImportBlocked()) return;
  try {
    const response = await fetch(`/api/character/import-unity?package=${encodeURIComponent(state.packageRelative)}`, {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({ package: state.packageRelative, unityRoot: "Assets/MxFrameworkGenerated/CharacterPackages", checkHashes: false, dryRun: false })
    });
    state.importResult = await response.json();
    state.message = response.ok && state.importResult.success ? "Import completed." : "Import failed.";
    renderShellStatus();
    renderImportStatus();
  } catch (error) {
    state.message = `Import request failed: ${error instanceof Error ? error.message : String(error)}`;
    renderShellStatus();
  }
}

async function copyReport() {
  const text = JSON.stringify({
    validation: state.validation,
    compile: state.compileResult,
    import: state.importResult
  }, null, 2);
  await navigator.clipboard?.writeText(text);
  state.message = "Report copied.";
  renderShellStatus();
}

function selectPath(path) {
  if (!path) return;
  state.selectedPath = path;
  renderTree();
  renderViewport();
  renderInspector();
}

function findTarget(path) {
  const pkg = state.package || {};
  const g = pkg.geometry || {};
  if (path === "manifest") return target("manifest", "Manifest", pkg.manifest);
  if (path === "resources") return target("resources", "Resource Catalog", pkg.resourceCatalog);
  if (path.startsWith("resources/")) return target("resource", path.slice(10), (pkg.resourceCatalog?.entries || []).find(x => x.resourceKey === path.slice(10)));
  if (path === "config") return target("config", "Character Application", pkg.applicationConfig);
  if (path === "geometry/body") return target("body", "Body Geometry", g.bodyProfile);
  if (path.startsWith("geometry/body_parts/")) return target("part", path.split("/").pop(), (g.bodyParts || []).find(x => x.partId === path.split("/").pop()));
  if (path.startsWith("geometry/colliders/")) return target("collider", path.split("/").pop(), (g.colliders || []).find(x => x.colliderId === path.split("/").pop()));
  if (path.startsWith("geometry/sockets/")) return target("socket", path.split("/").pop(), (g.sockets || []).find(x => x.socketId === path.split("/").pop()));
  if (path.startsWith("geometry/weapon_attachments/")) return target("weapon", path.split("/").pop(), (g.weaponAttachments || []).find(x => x.weaponId === path.split("/").pop()));
  if (path.startsWith("geometry/traces/")) return target("trace", path.split("/").pop(), (g.traces || []).find(x => x.traceId === path.split("/").pop()));
  if (path.startsWith("validation/issues/")) return target("issue", path.split("/").pop(), (state.validation?.issues || [])[Number(path.split("/").pop())]);
  return target("", path, null);
}

function target(kind, label, value) {
  return { kind, label, value };
}

function normalizeIssuePath(sourceObjectPath) {
  if (!sourceObjectPath) return "validation";
  const path = String(sourceObjectPath);
  if (path === "characterStableId" || path === "manifest" || path.endsWith("manifest.json")) return "manifest";
  if (path.startsWith("geometry/bodyColliders/")) return path.replace("geometry/bodyColliders/", "geometry/colliders/");
  if (path.startsWith("geometry/body_colliders/")) return path.replace("geometry/body_colliders/", "geometry/colliders/");
  if (path.startsWith("geometry/weaponAttachments/")) return path.replace("geometry/weaponAttachments/", "geometry/weapon_attachments/");
  if (path.startsWith("geometry/weapon_attachments/")) return path;
  if (path.startsWith("geometry/sockets/") || path.startsWith("geometry/traces/") || path.startsWith("geometry/colliders/")) return path;
  if ((state.package?.resourceCatalog?.entries || []).some(entry => entry.resourceKey === path)) return `resources/${path}`;
  return path.startsWith("validation/") ? path : "validation";
}

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

function coerceValue(value, type) {
  if (type === "number") return Number(value || 0);
  if (type === "boolean") return value === "true";
  return value;
}

function isImportBlocked() {
  const gate = state.compileResult?.gateReport;
  if (gate?.importBlocked || gate?.exportBlocked) return true;
  const issues = [
    ...(state.validation?.issues || []),
    ...(gate?.issues || [])
  ];
  return issues.some(issue => issue.gate === "ImportBlocked" || issue.gate === "ExportBlocked");
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

window.CharacterStudioTest = { buildTree, normalizeIssuePath, editableFields };
