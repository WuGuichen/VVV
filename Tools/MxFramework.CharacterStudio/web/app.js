const DEFAULT_PACKAGE = "Tools/MxFramework.Authoring/samples/character-iron-vanguard";
const LAYERS = { colliders: true, sockets: true, traces: true, weapons: true };
const LOADOUTS = [
  { id: "unarmed", label: "徒手", slots: [] },
  { id: "single_sword", label: "单手剑", slots: ["mainHand"] },
  { id: "sword_shield", label: "剑盾", slots: ["mainHand", "offHand"] }
];

const MODEL_IMPORT_ROLES = {
  body: {
    label: "角色主体模型",
    title: "替换角色主体模型资源",
    pending: "正在导入角色主体模型",
    done: "角色主体模型已导入"
  },
  mainHand: {
    label: "主手槽武器模型",
    title: "替换当前 mainHand 槽引用的武器预览模型；不创建新的武器定义",
    pending: "正在导入主手槽武器模型",
    done: "主手槽武器模型已导入并绑定到 mainHand"
  },
  offHand: {
    label: "副手槽武器模型",
    title: "替换当前 offHand 槽引用的武器预览模型；不创建新的武器定义",
    pending: "正在导入副手槽武器模型",
    done: "副手槽武器模型已导入并绑定到 offHand"
  },
  preview: {
    label: "仅选中资源",
    title: "仅导入到资源目录，不自动挂到角色主体或武器槽",
    pending: "正在导入资源目录模型",
    done: "模型已导入资源目录"
  }
};

const KIND_LABELS = {
  manifest: "清单",
  resources: "资源",
  resource: "资源",
  config: "配置",
  body: "身体",
  part: "部位",
  collider: "碰撞体",
  socket: "挂点",
  weapon: "武器",
  trace: "轨迹",
  validation: "诊断",
  issue: "问题"
};

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
  message: "",
  userSelectedPackage: false
};

const el = {};
let threeRuntimePromise = null;
let viewportRenderId = 0;
let viewportCleanup = null;

document.addEventListener("DOMContentLoaded", () => {
  for (const id of [
    "packageSelect", "reloadButton", "saveButton", "compileButton", "importButton",
    "modelImportRole", "modelImportButton", "modelFileInput",
    "packageSummary", "packageTree", "dirtyBadge", "loadoutTabs", "viewport",
    "resourceLibraryTarget", "modelResourceList",
    "inspector", "diagnostics", "importStatus", "selectionBadge", "copyReportButton",
    "subtitle"
  ]) el[id] = document.getElementById(id);

  el.reloadButton.addEventListener("click", () => loadAll());
  el.saveButton.addEventListener("click", () => savePackage());
  el.compileButton.addEventListener("click", () => compilePackage());
  el.importButton.addEventListener("click", () => importUnity());
  el.modelImportButton.addEventListener("click", () => {
    if (!state.canWrite) {
      state.message = "静态预览不能导入模型。请通过 Authoring server 打开页面。";
      renderShellStatus();
      return;
    }
    el.modelFileInput.value = "";
    el.modelFileInput.click();
  });
  el.modelFileInput.addEventListener("change", () => {
    const file = el.modelFileInput.files?.[0];
    if (file) importModel(file);
  });
  el.modelImportRole.addEventListener("change", () => {
    updateModelImportTitle();
    renderResourceLibrary();
  });
  el.modelResourceList.addEventListener("click", event => {
    const button = event.target.closest("button[data-resource-key]");
    if (!button) return;
    bindModelResource(button.dataset.resourceKey);
  });
  el.copyReportButton.addEventListener("click", () => copyReport());
  el.packageSelect.addEventListener("change", event => {
    state.packageRelative = event.target.value;
    state.userSelectedPackage = true;
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
  if (!state.userSelectedPackage && state.packages.length > 0) {
    state.packageRelative = state.packages[0].relative;
  } else if (!state.packages.some(pkg => pkg.relative === state.packageRelative)) {
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
    state.message = "已连接 Authoring 服务。";
    return;
  }

  state.package = await readStaticPackage(state.packageRelative);
  state.validation = state.package.validationReport || { issues: [] };
  state.importResult = null;
  state.canWrite = false;
  state.apiAvailable = false;
  state.dirty = false;
  state.message = "静态预览：请启动 Authoring server 后再保存、预检、导入模型或导入 Unity。";
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
  renderResourceLibrary();
  renderViewport();
  renderInspector();
  renderDiagnostics();
  renderImportStatus();
}

function renderShellStatus() {
  el.subtitle.textContent = state.message || "角色资源包外部装配工作台";
  el.dirtyBadge.textContent = state.dirty ? "dirty" : "clean";
  el.dirtyBadge.className = `badge ${state.dirty ? "warn" : "ok"}`;
  el.saveButton.disabled = !state.canWrite || !state.package;
  el.modelImportButton.disabled = !state.canWrite || !state.package;
  el.modelImportRole.disabled = !state.canWrite || !state.package;
  el.compileButton.disabled = !state.apiAvailable || !state.package;
  el.importButton.disabled = !state.apiAvailable || !state.package || state.dirty || isImportBlocked();
  updateModelImportTitle();
}

function getModelImportRole() {
  return MODEL_IMPORT_ROLES[el.modelImportRole?.value] || MODEL_IMPORT_ROLES.preview;
}

function updateModelImportTitle() {
  if (!el.modelImportButton || !el.modelImportRole) return;
  el.modelImportButton.title = `${getModelImportRole().title}。支持 GLB/GLTF；FBX 会先转换为 GLB。`;
}

function renderResourceLibrary() {
  if (!el.modelResourceList || !el.resourceLibraryTarget) return;
  const targetRole = el.modelImportRole?.value || "preview";
  const roleInfo = getModelImportRole();
  el.resourceLibraryTarget.textContent = targetRole === "preview"
    ? "当前目标：仅选中资源"
    : `当前替换目标：${roleInfo.label}`;

  const resources = getModelResources(state.package);
  if (!resources.length) {
    el.modelResourceList.innerHTML = `<div class="empty">暂无模型资源。</div>`;
    return;
  }

  el.modelResourceList.innerHTML = resources.map(resource => {
    const path = `resources/${resource.resourceKey}`;
    const selected = path === state.selectedPath;
    const binding = describeResourceBinding(resource, state.package);
    const thumbnailUrl = getResourceThumbnailUrl(resource, state.package);
    const sourceName = resource.provenance?.sourceFile || resource.relativePath || resource.localId || resource.resourceKey;
    const imported = (resource.tags || []).some(tag => tag === "characterstudio-import" || tag === "converted-from-fbx");
    const thumb = thumbnailUrl
      ? `<img src="${escapeHtml(thumbnailUrl)}" alt="${escapeHtml(getResourceDisplayName(resource))}">`
      : `<span>${escapeHtml(getResourceInitial(resource))}</span>`;
    return `
      <button type="button" class="resource-card ${selected ? "active" : ""}" data-resource-key="${escapeHtml(resource.resourceKey || "")}" title="${escapeHtml(sourceName)}">
        <span class="resource-thumb">${thumb}</span>
        <span class="resource-info">
          <strong>${escapeHtml(getResourceDisplayName(resource))}</strong>
          <span>${escapeHtml(binding || "未绑定到角色或武器槽")}</span>
          <span>${escapeHtml(resource.usage || "usage?")} / ${escapeHtml(resource.sourceFormat || "format?")}${imported ? " / imported" : ""}</span>
        </span>
      </button>`;
  }).join("");
}

function getModelResources(pkg) {
  return (pkg?.resourceCatalog?.entries || [])
    .filter(resource => resource && resource.typeId === "model" && resource.resourceKey)
    .sort((a, b) => getResourceSortKey(a, pkg).localeCompare(getResourceSortKey(b, pkg)));
}

function getResourceSortKey(resource, pkg) {
  const binding = describeResourceBinding(resource, pkg);
  const rank = binding.includes("角色主体") ? "0" : binding.includes("mainHand") ? "1" : binding.includes("offHand") ? "2" : "3";
  return `${rank}:${getResourceDisplayName(resource)}`;
}

function getResourceDisplayName(resource) {
  const source = resource.provenance?.sourceFile || resource.localId || resource.resourceKey || resource.relativePath || "model";
  const name = String(source).split("/").pop().replace(/\.(glb|gltf|fbx)$/i, "");
  return name || "model";
}

function getResourceInitial(resource) {
  const name = getResourceDisplayName(resource);
  return name.slice(0, 2).toUpperCase();
}

function describeResourceBinding(resource, pkg) {
  const bindings = [];
  if (resource.usage === "characterModel") bindings.push("角色主体");
  for (const attachment of pkg?.geometry?.weaponAttachments || []) {
    if (attachment.previewResourceKey === resource.resourceKey) {
      bindings.push(`${attachment.equipSlot || "slot"}:${attachment.weaponId || "weapon"}`);
    }
  }
  return bindings.join(" / ");
}

function getResourceThumbnailUrl(resource, pkg) {
  const previewKey = resource.preview?.thumbnailResourceKey || resource.preview?.placeholderResourceKey || "";
  const preview = (pkg?.resourceCatalog?.entries || []).find(entry => entry.resourceKey === previewKey);
  if (!preview?.relativePath) return "";
  return encodeURI(`/${state.packageRelative}/${preview.relativePath}`);
}

function bindModelResource(resourceKey) {
  if (!resourceKey || !state.package) return;
  const resource = (state.package.resourceCatalog?.entries || []).find(entry => entry.resourceKey === resourceKey);
  if (!resource) return;

  const role = el.modelImportRole?.value || "preview";
  const path = `resources/${resource.resourceKey}`;
  if (!state.canWrite || role === "preview") {
    state.selectedPath = path;
    state.message = role === "preview"
      ? `已选中资源：${getResourceDisplayName(resource)}`
      : "静态预览只能选中资源，不能替换绑定。";
    renderTree();
    renderResourceLibrary();
    renderViewport();
    renderInspector();
    renderShellStatus();
    return;
  }

  if (role === "body") {
    bindBodyModelResource(resource);
  } else if (role === "mainHand" || role === "offHand") {
    if (!bindWeaponSlotResource(role, resource)) return;
  } else {
    state.selectedPath = path;
    render();
    return;
  }

  ensureApplicationResourceKey(resource.resourceKey);
  touchModelResource(resource, role);
  state.selectedPath = path;
  state.dirty = true;
  state.message = `${getResourceDisplayName(resource)} 已设为${getModelImportRole().label}。保存后写入资源包。`;
  render();
}

function bindBodyModelResource(resource) {
  for (const entry of getModelResources(state.package)) {
    if (entry.resourceKey !== resource.resourceKey && entry.usage === "characterModel") {
      entry.usage = "previewMesh";
      removeTag(entry.tags, "body");
    }
  }
  resource.usage = "characterModel";
  addTagValue(resource, "body");
}

function bindWeaponSlotResource(slot, resource) {
  const attachments = state.package?.geometry?.weaponAttachments || [];
  const attachment = attachments.find(item => item.equipSlot === slot);
  if (!attachment) {
    state.message = `没有找到 ${slot} 武器挂载项，无法替换。`;
    renderShellStatus();
    return false;
  }

  attachment.previewResourceKey = resource.resourceKey;
  if (resource.usage !== "characterModel") {
    resource.usage = "weaponModel";
  }
  addTagValue(resource, "weapon");
  addTagValue(resource, slot);
  return true;
}

function ensureApplicationResourceKey(resourceKey) {
  const appConfig = state.package.applicationConfig || {};
  state.package.applicationConfig = appConfig;
  appConfig.resourceKeys = Array.isArray(appConfig.resourceKeys) ? appConfig.resourceKeys : [];
  if (!appConfig.resourceKeys.includes(resourceKey)) appConfig.resourceKeys.push(resourceKey);
}

function touchModelResource(resource, role) {
  resource.provenance = resource.provenance || {};
  resource.provenance.modifiedUtc = new Date().toISOString();
  addTagValue(resource, "characterstudio-bind");
  addTagValue(resource, role);
}

function addTagValue(resource, tag) {
  if (!tag) return;
  resource.tags = Array.isArray(resource.tags) ? resource.tags : [];
  if (!resource.tags.some(existing => String(existing).toLowerCase() === String(tag).toLowerCase())) {
    resource.tags.push(tag);
  }
}

function removeTag(tags, tag) {
  if (!Array.isArray(tags)) return;
  const index = tags.findIndex(existing => String(existing).toLowerCase() === String(tag).toLowerCase());
  if (index >= 0) tags.splice(index, 1);
}

function renderSummary() {
  const pkg = state.package;
  if (!pkg) {
    el.packageSummary.innerHTML = `<div class="empty">No package loaded.</div>`;
    return;
  }
  const geometry = pkg.geometry || {};
  el.packageSummary.innerHTML = [
    summaryCell("资源包", pkg.manifest?.packageId || "-"),
    summaryCell("版本", pkg.manifest?.version || "-"),
    summaryCell("资源", (pkg.resourceCatalog?.entries || []).length),
    summaryCell("碰撞体", (geometry.colliders || []).length),
    summaryCell("挂点", (geometry.sockets || []).length),
    summaryCell("轨迹", (geometry.traces || []).length)
  ].join("");
}

function summaryCell(label, value) {
  return `<div><strong>${escapeHtml(String(value))}</strong>${escapeHtml(label)}</div>`;
}

function renderTree() {
  const nodes = buildTree(state.package);
  el.packageTree.innerHTML = nodes.map(node => {
    const active = node.path === state.selectedPath ? " active" : "";
    return `<button class="${active}" type="button" data-path="${escapeHtml(node.path)}" style="padding-left:${8 + node.depth * 14}px"><span class="kind">${escapeHtml(KIND_LABELS[node.kind] || node.kind)}</span><span class="label">${escapeHtml(node.label)}</span></button>`;
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
    node("resources", "resources", "资源目录", 0),
    ...grouped((pkg.resourceCatalog?.entries || []), "resource", entry => `resources/${entry.resourceKey || entry.localId}`, entry => entry.resourceKey || entry.relativePath || "resource", 1),
    node("config", "config", "角色配置", 0),
    node("geometry/body", "body", g.bodyProfile?.profileId || "body geometry", 0),
    ...grouped((g.bodyParts || []), "part", part => `geometry/body_parts/${part.partId}`, part => part.partId || "part", 1),
    ...grouped((g.colliders || []), "collider", collider => `geometry/colliders/${collider.colliderId}`, collider => collider.colliderId || "collider", 1),
    ...grouped((g.sockets || []), "socket", socket => `geometry/sockets/${socket.socketId}`, socket => socket.socketId || "socket", 1),
    ...grouped((g.weaponAttachments || []), "weapon", attachment => `geometry/weapon_attachments/${attachment.weaponId}`, attachment => `${attachment.equipSlot || "slot"}:${attachment.weaponId || "weapon"}`, 1),
    ...grouped((g.traces || []), "trace", trace => `geometry/traces/${trace.traceId}`, trace => trace.traceId || "trace", 1),
    node("validation", "validation", "诊断与门禁", 0),
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
    el.viewport.innerHTML = `<div class="empty">未加载资源包。</div>`;
    return;
  }

  if (viewportCleanup) {
    viewportCleanup();
    viewportCleanup = null;
  }

  const renderId = ++viewportRenderId;
  el.viewport.innerHTML = `<div class="viewport3d" aria-label="3D 角色预览"><div class="viewport-status">正在加载 3D 预览...</div></div>`;
  renderThreeViewport(renderId).catch(error => {
    if (renderId !== viewportRenderId) return;
    renderSvgViewport(`3D 预览不可用：${error instanceof Error ? error.message : String(error)}`);
  });
}

async function renderThreeViewport(renderId) {
  const runtime = await loadThreeRuntime();
  if (renderId !== viewportRenderId) return;

  const { THREE, GLTFLoader, OrbitControls } = runtime;
  const host = el.viewport.querySelector(".viewport3d");
  if (!host) return;

  host.innerHTML = "";

  const scene = new THREE.Scene();
  scene.background = new THREE.Color(0xf8fafb);

  const camera = new THREE.PerspectiveCamera(42, 1, 0.01, 100);
  camera.position.set(2.35, 1.55, 3.15);

  const renderer = new THREE.WebGLRenderer({ antialias: true, alpha: false, preserveDrawingBuffer: true });
  renderer.setPixelRatio(Math.min(window.devicePixelRatio || 1, 2));
  host.append(renderer.domElement);

  const controls = new OrbitControls(camera, renderer.domElement);
  controls.enableDamping = true;
  controls.target.set(0, 0.95, 0);
  controls.minDistance = 0.75;
  controls.maxDistance = 8;

  scene.add(new THREE.HemisphereLight(0xffffff, 0xb8c2c9, 1.8));
  const key = new THREE.DirectionalLight(0xffffff, 1.7);
  key.position.set(2.5, 3.5, 2.5);
  scene.add(key);
  scene.add(new THREE.GridHelper(3.2, 16, 0xd6dde2, 0xe6ecef));

  const content = new THREE.Group();
  const pickables = [];
  scene.add(content);

  const loader = new GLTFLoader();
  const packageUrl = relative => encodeURI(`/${state.packageRelative}/${relative}`);
  const resources = state.package.resourceCatalog?.entries || [];
  const geometry = state.package.geometry || {};
  const socketsById = Object.fromEntries((geometry.sockets || []).map(socket => [socket.socketId, socket]));
  const activeSlots = new Set((LOADOUTS.find(loadout => loadout.id === state.activeLoadout) || LOADOUTS[0]).slots);

  const bodyResource = resources.find(resource => resource.usage === "characterModel")
    || resources.find(resource => resource.resourceKey === geometry.bodyProfile?.modelRootStableId)
    || resources.find(resource => resource.typeId === "model");
  let loadedBody = false;
  if (bodyResource?.relativePath) {
    loadedBody = await addGltfResource({
      THREE,
      loader,
      content,
      pickables,
      url: packageUrl(bodyResource.relativePath),
      objectPath: `resources/${bodyResource.resourceKey}`,
      name: bodyResource.localId || bodyResource.resourceKey
    });
  }
  if (!loadedBody) addFallbackBody(THREE, content, pickables, geometry.bodyProfile);

  if (state.layers.colliders) addColliderMeshes(THREE, content, pickables, geometry.colliders || []);
  if (state.layers.sockets) addSocketMeshes(THREE, content, pickables, geometry.sockets || []);
  if (state.layers.traces) addTraceMeshes(THREE, content, pickables, (geometry.traces || []).filter(trace => activeSlots.has(trace.equipSlot)));
  if (state.layers.weapons) {
    await addWeaponMeshes({
      THREE,
      loader,
      content,
      pickables,
      resources,
      packageUrl,
      socketsById,
      attachments: (geometry.weaponAttachments || []).filter(attachment => activeSlots.has(attachment.equipSlot))
    });
  }

  frameContent(THREE, camera, controls, content, geometry.bodyProfile);

  const raycaster = new THREE.Raycaster();
  raycaster.params.Line = { threshold: 0.08 };
  const pointer = new THREE.Vector2();
  const onPointerDown = event => {
    const rect = renderer.domElement.getBoundingClientRect();
    pointer.x = ((event.clientX - rect.left) / rect.width) * 2 - 1;
    pointer.y = -(((event.clientY - rect.top) / rect.height) * 2 - 1);
    raycaster.setFromCamera(pointer, camera);
    const hit = raycaster.intersectObjects(pickables, true)
      .find(item => findObjectPath(item.object));
    const objectPath = hit ? findObjectPath(hit.object) : "";
    if (objectPath) selectPath(objectPath);
  };
  renderer.domElement.addEventListener("pointerdown", onPointerDown);

  const resize = () => {
    const width = Math.max(1, host.clientWidth);
    const height = Math.max(1, host.clientHeight);
    renderer.setSize(width, height, false);
    camera.aspect = width / height;
    camera.updateProjectionMatrix();
  };
  const resizeObserver = new ResizeObserver(resize);
  resizeObserver.observe(host);
  resize();

  let frame = 0;
  const animate = () => {
    controls.update();
    renderer.render(scene, camera);
    frame = requestAnimationFrame(animate);
  };
  animate();

  viewportCleanup = () => {
    cancelAnimationFrame(frame);
    resizeObserver.disconnect();
    renderer.domElement.removeEventListener("pointerdown", onPointerDown);
    renderer.dispose();
  };
}

function renderSvgViewport(message = "") {
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

  el.viewport.innerHTML = `<div class="viewport-fallback">${message ? `<div class="viewport-status">${escapeHtml(message)}</div>` : ""}<svg viewBox="0 0 100 100" role="img" aria-label="Character package viewport"><defs><pattern id="grid" width="10" height="10" patternUnits="userSpaceOnUse"><path d="M 10 0 L 0 0 0 10" fill="none" stroke="#edf1f3" stroke-width="0.6"/></pattern></defs><rect width="100" height="100" fill="url(#grid)"/><text x="4" y="7" font-size="3.5" fill="#61717f">${escapeHtml(state.package.manifest?.packageId || "character")}</text>${shapes.join("")}</svg></div>`;
  el.viewport.querySelectorAll("[data-object-path]").forEach(item => {
    item.addEventListener("click", event => {
      event.stopPropagation();
      selectPath(item.getAttribute("data-object-path"));
    });
  });
}

async function loadThreeRuntime() {
  if (!threeRuntimePromise) {
    threeRuntimePromise = Promise.all([
      import("../node_modules/three/build/three.module.js"),
      import("../node_modules/three/examples/jsm/loaders/GLTFLoader.js"),
      import("../node_modules/three/examples/jsm/controls/OrbitControls.js")
    ]).then(([THREE, loaderModule, controlsModule]) => ({
      THREE,
      GLTFLoader: loaderModule.GLTFLoader,
      OrbitControls: controlsModule.OrbitControls
    }));
  }
  return threeRuntimePromise;
}

async function addGltfResource({ THREE, loader, content, pickables, url, objectPath, name, position = null }) {
  try {
    const gltf = await new Promise((resolve, reject) => loader.load(url, resolve, undefined, reject));
    const root = gltf.scene;
    root.name = name || objectPath;
    if (position) root.position.copy(position);
    makeSelectable(root, objectPath, pickables);
    content.add(root);
    return true;
  } catch {
    return false;
  }
}

function addFallbackBody(THREE, content, pickables, body = {}) {
  const height = Number(body.heightMeters || body.defaultCapsule?.height || 1.8);
  const radius = Number(body.radiusMeters || body.defaultCapsule?.radius || 0.34);
  const material = new THREE.MeshStandardMaterial({ color: 0xcddde0, roughness: 0.72, metalness: 0.05 });
  const geometry = THREE.CapsuleGeometry
    ? new THREE.CapsuleGeometry(radius, Math.max(0.01, height - radius * 2), 8, 18)
    : new THREE.CylinderGeometry(radius, radius, height, 18);
  const mesh = new THREE.Mesh(geometry, material);
  mesh.position.y = height / 2;
  makeSelectable(mesh, "geometry/body", pickables);
  content.add(mesh);
}

function addColliderMeshes(THREE, content, pickables, colliders) {
  for (const collider of colliders) {
    const objectPath = `geometry/colliders/${collider.colliderId}`;
    const selected = state.selectedPath === objectPath;
    const material = new THREE.MeshBasicMaterial({
      color: selected ? 0xffa11f : 0x1f7a7a,
      transparent: true,
      opacity: selected ? 0.42 : 0.24,
      wireframe: !selected,
      depthWrite: false
    });
    let geometry;
    if (collider.shape === "Box") {
      const size = collider.size || {};
      geometry = new THREE.BoxGeometry(Number(size.x || 0.25), Number(size.y || 0.25), Number(size.z || 0.25));
    } else if (collider.shape === "Capsule" && THREE.CapsuleGeometry) {
      const radius = Number(collider.radius || 0.12);
      const height = Number(collider.height || 0.5);
      geometry = new THREE.CapsuleGeometry(radius, Math.max(0.01, height - radius * 2), 8, 16);
    } else {
      geometry = new THREE.SphereGeometry(Number(collider.radius || 0.12), 20, 12);
    }
    const mesh = new THREE.Mesh(geometry, material);
    mesh.position.copy(toVector3(THREE, collider.localPose?.position));
    makeSelectable(mesh, objectPath, pickables);
    content.add(mesh);
  }
}

function addSocketMeshes(THREE, content, pickables, sockets) {
  for (const socket of sockets) {
    const objectPath = `geometry/sockets/${socket.socketId}`;
    const selected = state.selectedPath === objectPath;
    const material = new THREE.MeshStandardMaterial({ color: selected ? 0xffa11f : 0xb46a1f, emissive: selected ? 0x442000 : 0x000000 });
    const mesh = new THREE.Mesh(new THREE.SphereGeometry(0.035, 16, 10), material);
    mesh.position.copy(toVector3(THREE, socket.localPose?.position));
    makeSelectable(mesh, objectPath, pickables);
    content.add(mesh);
  }
}

function addTraceMeshes(THREE, content, pickables, traces) {
  for (const trace of traces) {
    const objectPath = `geometry/traces/${trace.traceId}`;
    const selected = state.selectedPath === objectPath;
    const points = [
      toVector3(THREE, trace.startPose?.position),
      toVector3(THREE, trace.endPose?.position)
    ];
    const line = new THREE.Line(
      new THREE.BufferGeometry().setFromPoints(points),
      new THREE.LineBasicMaterial({ color: selected ? 0xffa11f : 0xc24141, linewidth: 2 })
    );
    makeSelectable(line, objectPath, pickables);
    content.add(line);
  }
}

async function addWeaponMeshes({ THREE, loader, content, pickables, resources, packageUrl, socketsById, attachments }) {
  for (const attachment of attachments) {
    const objectPath = `geometry/weapon_attachments/${attachment.weaponId}`;
    const socket = socketsById[attachment.attachSocketId];
    const position = toVector3(THREE, socket?.localPose?.position);
    const resource = resources.find(entry => entry.resourceKey === attachment.previewResourceKey);
    let loaded = false;
    if (resource?.relativePath) {
      loaded = await addGltfResource({
        THREE,
        loader,
        content,
        pickables,
        url: packageUrl(resource.relativePath),
        objectPath,
        name: attachment.weaponId,
        position
      });
    }
    if (!loaded) {
      const selected = state.selectedPath === objectPath;
      const material = new THREE.MeshStandardMaterial({ color: selected ? 0xffa11f : 0xb46a1f, transparent: true, opacity: 0.74 });
      const mesh = new THREE.Mesh(new THREE.BoxGeometry(0.08, 0.45, 0.08), material);
      mesh.position.copy(position);
      mesh.position.x += attachment.equipSlot === "offHand" ? -0.12 : 0.12;
      makeSelectable(mesh, objectPath, pickables);
      content.add(mesh);
    }
  }
}

function frameContent(THREE, camera, controls, content, body = {}) {
  const box = new THREE.Box3().setFromObject(content);
  if (!box.isEmpty()) {
    const center = box.getCenter(new THREE.Vector3());
    const size = box.getSize(new THREE.Vector3());
    const radius = Math.max(size.x, size.y, size.z, 1);
    controls.target.copy(center);
    controls.target.y = Math.max(0.8, center.y);
    camera.position.set(center.x + radius * 1.2, controls.target.y + radius * 0.55, center.z + radius * 1.7);
    camera.near = Math.max(0.01, radius / 100);
    camera.far = Math.max(100, radius * 20);
    camera.updateProjectionMatrix();
  } else {
    controls.target.set(0, Number(body.heightMeters || 1.8) / 2, 0);
  }
  controls.update();
}

function makeSelectable(object, objectPath, pickables) {
  object.userData.objectPath = objectPath;
  object.traverse?.(child => { child.userData.objectPath = objectPath; });
  pickables.push(object);
}

function findObjectPath(object) {
  let cursor = object;
  while (cursor) {
    if (cursor.userData?.objectPath) return cursor.userData.objectPath;
    cursor = cursor.parent;
  }
  return "";
}

function toVector3(THREE, value = {}) {
  return new THREE.Vector3(Number(value.x || 0), Number(value.y || 0), Number(value.z || 0));
}

function renderInspector() {
  const target = findTarget(state.selectedPath);
  el.selectionBadge.textContent = target.kind || "none";
  if (!target.value) {
    el.inspector.innerHTML = `<div class="empty">请选择一个资源包对象。</div>`;
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
    rows.push(`<div class="empty">暂无诊断。</div>`);
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
    el.importStatus.innerHTML = `<div class="empty">暂无 Unity 导入报告。</div>`;
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
    state.message = "静态预览不能保存。请启动 Authoring server。";
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
  state.message = "资源包已保存并完成校验。";
  render();
}

async function importModel(file) {
  if (!state.canWrite) {
    state.message = "静态预览不能导入模型。请启动 Authoring server。";
    renderShellStatus();
    return;
  }

  const extension = file.name.split(".").pop()?.toLowerCase() || "";
  if (!["glb", "gltf", "fbx"].includes(extension)) {
    state.message = "仅支持导入 .glb、.gltf 或 .fbx 模型。";
    renderShellStatus();
    return;
  }

  const role = el.modelImportRole.value;
  const roleInfo = getModelImportRole();
  state.message = extension === "fbx"
    ? `${roleInfo.pending}，并转换 FBX：${file.name}`
    : `${roleInfo.pending}：${file.name}`;
  renderShellStatus();
  try {
    const bytesBase64 = await readFileAsBase64(file);
    const response = await fetch(`/api/character/import-model?package=${encodeURIComponent(state.packageRelative)}`, {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({
        fileName: file.name,
        role,
        bytesBase64
      })
    });
    if (!response.ok) {
      state.message = await response.text();
      renderShellStatus();
      return;
    }
    const data = await response.json();
    state.package = data.package;
    state.validation = data.validation || data.package?.validationReport || { issues: [] };
    state.importResult = data.importReport || state.importResult;
    state.dirty = false;
    state.canWrite = Boolean(data.canWrite);
    state.apiAvailable = true;
    state.selectedPath = findImportedModelPath(role, data.package, file.name) || state.selectedPath;
    state.message = extension === "fbx"
      ? `${roleInfo.done}，FBX 已转换为 GLB：${file.name}`
      : `${roleInfo.done}：${file.name}`;
    render();
  } catch (error) {
    state.message = `模型导入失败：${error instanceof Error ? error.message : String(error)}`;
    renderShellStatus();
  }
}

function readFileAsBase64(file) {
  return new Promise((resolve, reject) => {
    const reader = new FileReader();
    reader.addEventListener("load", () => {
      const value = String(reader.result || "");
      resolve(value.includes(",") ? value.slice(value.indexOf(",") + 1) : value);
    });
    reader.addEventListener("error", () => reject(reader.error || new Error("File read failed.")));
    reader.readAsDataURL(file);
  });
}

function findImportedModelPath(role, pkg, sourceFileName) {
  const resources = pkg?.resourceCatalog?.entries || [];
  if (role === "body") {
    const entry = resources.find(resource => resource.usage === "characterModel" || resource.localId === "model.body");
    return entry?.resourceKey ? `resources/${entry.resourceKey}` : "geometry/body";
  }

  if (role === "mainHand" || role === "offHand") {
    const attachment = (pkg?.geometry?.weaponAttachments || []).find(item => item.equipSlot === role);
    if (attachment?.previewResourceKey && resources.some(resource => resource.resourceKey === attachment.previewResourceKey)) {
      return `resources/${attachment.previewResourceKey}`;
    }
    return attachment?.weaponId ? `geometry/weapon_attachments/${attachment.weaponId}` : "";
  }

  const bySource = resources.find(resource => resource.provenance?.sourceFile === sourceFileName);
  if (bySource?.resourceKey) return `resources/${bySource.resourceKey}`;
  return "";
}

async function compilePackage() {
  const data = await readJson(`/api/character/compile?package=${encodeURIComponent(state.packageRelative)}&checkHashes=false`, null);
  if (!data) {
    state.message = "预检失败，或 Authoring server 不可用。";
    renderShellStatus();
    return;
  }
  state.compileResult = data;
  state.message = `预检状态：${data.status || "Unknown"}`;
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
    state.message = response.ok && state.importResult.success ? "Unity 导入完成。" : "Unity 导入失败。";
    renderShellStatus();
    renderImportStatus();
  } catch (error) {
    state.message = `Unity 导入请求失败：${error instanceof Error ? error.message : String(error)}`;
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
  state.message = "报告已复制。";
  renderShellStatus();
}

function selectPath(path) {
  if (!path) return;
  state.selectedPath = path;
  renderTree();
  renderResourceLibrary();
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
