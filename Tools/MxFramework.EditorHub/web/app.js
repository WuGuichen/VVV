const TOOLS = [
  { id: "combat-design", name: "战斗设计", description: "机制、依赖、卡面说明与版本证据", kind: "project", icon: '<path d="m4 4 16 16M20 4 4 20M3 8l5-5M16 21l5-5"/>' },
  { id: "character", name: "角色编辑器", description: "角色装配与配置", kind: "character", icon: '<circle cx="12" cy="7" r="3"/><path d="M5 21v-3a7 7 0 0 1 14 0v3"/>' },
  { id: "animation", name: "动画编辑器", description: "动画组与片段映射", kind: "character", icon: '<rect x="3" y="3" width="18" height="18" rx="3"/><path d="m10 8 6 4-6 4z"/>' },
  { id: "resources", name: "资源管理器", description: "全局资源浏览与引用", kind: "character", icon: '<path d="m12 3 9 5-9 5-9-5zM3 12l9 5 9-5M3 16l9 5 9-5"/>' },
  { id: "audio", name: "音频工作台", description: "音效配置与试听", kind: "character", icon: '<path d="M9 18V5l12-2v13M9 5l12-2"/><ellipse cx="6" cy="18" rx="3" ry="3"/><ellipse cx="18" cy="16" rx="3" ry="3"/>' },
  { id: "composer", name: "UI Composer", description: "界面布局与发布", kind: "screen", icon: '<rect x="3" y="3" width="18" height="18" rx="3"/><path d="M3 9h18M9 9v12"/>' },
  { id: "art", name: "UI 素材库", description: "素材浏览与使用建议", kind: "library", icon: '<rect x="3" y="3" width="18" height="18" rx="3"/><circle cx="8" cy="8" r="1"/><path d="m3 17 6-6 4 4 3-3 5 5"/>' },
  { id: "buff", name: "Buff 工作台", description: "效果配置与校验", kind: "buff", icon: '<path d="m12 2 8 4v6c0 5-8 10-8 10S4 17 4 12V6zM8 12h8M12 8v8"/>' }
];
const state = { packageRelative: "", targetTool: null, targetItems: [], targetRequest: 0, errors: new Map() };
const el = {};
document.addEventListener("DOMContentLoaded", () => {
  for (const id of ["serverStatus", "refreshButton", "restartServiceButton", "createPackageButton", "moreMenu", "diagnosticsButton",
    "toolGrid", "recentSection", "recentList", "clearRecentButton", "targetDialog", "targetForm", "targetTitle", "targetLabel",
    "targetSelect", "targetError", "targetSubmitButton", "targetCancelButton", "targetRefreshButton",
    "createDialog", "createForm", "createCancelButton", "createCancelFooterButton", "createTemplate", "createPackageId",
    "createDisplayName", "createDirectoryName", "createError", "createSubmitButton"]) el[id] = document.getElementById(id);
  const queryPackage = new URLSearchParams(window.location.search).get("package");
  if (queryPackage) Hub.rememberTarget("character", queryPackage);
  el.refreshButton.addEventListener("click", refresh);
  el.restartServiceButton.addEventListener("click", restartEditorService);
  el.createPackageButton.addEventListener("click", openCreateDialog);
  el.createCancelButton.addEventListener("click", closeCreateDialog);
  el.createCancelFooterButton.addEventListener("click", closeCreateDialog);
  el.createForm.addEventListener("submit", submitCreatePackage);
  el.createPackageId.addEventListener("input", syncCreateDefaults);
  el.toolGrid.addEventListener("click", event => {
    const button = event.target.closest("[data-tool]");
    if (button) {
      const tool = TOOLS.find(tool => tool.id === button.dataset.tool);
      if (tool.kind === "project") window.location.href = "./combat-design.html";
      else openTargetPicker(tool);
    }
  });
  el.targetCancelButton.addEventListener("click", () => Hub.closeDialog(el.targetDialog));
  el.targetDialog.addEventListener("close", () => { state.targetRequest++; });
  el.targetRefreshButton.addEventListener("click", loadTargetOptions);
  el.targetForm.addEventListener("submit", event => { event.preventDefault(); submitTarget(); });
  el.diagnosticsButton.addEventListener("click", () => {
    el.moreMenu.open = false;
    openTargetPicker({ id: "diagnostics", name: "诊断", kind: "character" });
  });
  el.recentList.addEventListener("click", event => {
    const button = event.target.closest("[data-recent]");
    if (button) openRecent(Number(button.dataset.recent));
  });
  el.clearRecentButton.addEventListener("click", () => {
    const preferences = Hub.readPreferences();
    preferences.recent = [];
    Hub.writePreferences(preferences);
    renderRecent();
  });
  document.addEventListener("click", event => {
    if (!el.moreMenu.contains(event.target)) el.moreMenu.open = false;
  });
  renderTools();
  renderRecent();
  refresh();
});
async function refresh() {
  el.refreshButton.disabled = true;
  el.serverStatus.textContent = "正在连接服务…";
  el.serverStatus.className = "service-status";
  state.errors.clear();
  renderTools();
  try {
    const packages = await Hub.readJson("/api/character/packages");
    if (!Array.isArray(packages)) throw new Error("服务返回了无法识别的角色列表。");
    el.serverStatus.textContent = "服务已连接";
    el.serverStatus.classList.add("connected");
  } catch (error) {
    el.serverStatus.textContent = window.location.protocol === "file:"
      ? "请通过 Editor Hub 启动脚本打开。"
      : `服务未连接：${error.message}。请刷新或在「更多」中重启。`;
    el.serverStatus.classList.add("failed");
  } finally { el.refreshButton.disabled = false; }
}
function renderTools() {
  el.toolGrid.innerHTML = TOOLS.map(tool => `
    <article class="tool-card">
      <button class="tool-launch" type="button" data-tool="${tool.id}" aria-label="打开${tool.name}">
        <span class="tool-icon" aria-hidden="true"><svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.6" stroke-linecap="round" stroke-linejoin="round">${tool.icon}</svg></span>
        <span class="tool-arrow" aria-hidden="true">↗</span><span class="tool-name">${tool.name}</span><span class="tool-description">${tool.description}</span>
      </button>
      ${state.errors.has(tool.id) ? `<p class="tool-error" role="alert">${Hub.escapeHtml(state.errors.get(tool.id))}</p>` : ""}
    </article>`).join("");
}
function recentEntries() {
  const recent = Hub.readPreferences().recent;
  return Array.isArray(recent) ? recent.filter(item => item && TOOLS.some(tool => tool.id === item.tool)
    && Hub.toolUrl(item.tool, item.targetId)).slice(0, 6) : [];
}
function renderRecent() {
  const recent = recentEntries();
  el.recentSection.hidden = recent.length === 0;
  el.recentList.innerHTML = recent.map((item, index) => `
    <button class="recent-item" data-recent="${index}" type="button">
      <span><strong>${Hub.escapeHtml(item.targetLabel || item.targetId)}</strong><small>${Hub.escapeHtml(TOOLS.find(tool => tool.id === item.tool).name)}</small></span>
      <span>继续编辑 ↗</span>
    </button>`).join("");
}
async function getTargetOptions(tool) {
  let items, options;
  if (tool.kind === "screen") {
    items = await Hub.readJson("/api/authoring/ui-composer/screens");
    options = Array.isArray(items) ? items.map(item => ({ id: item.screenId, label: item.displayName || item.screenId })) : [];
  } else if (tool.kind === "library") {
    items = await Hub.readJson("/api/authoring/ui-art/resources/libraries");
    options = Array.isArray(items) ? items.map(item => ({ id: item.libraryId || item.resourceRoot, label: item.displayName || item.libraryId || item.resourceRoot })) : [];
  } else if (tool.kind === "buff") {
    items = await Hub.readJson("/api/authoring/buffs/packages");
    options = Array.isArray(items) ? items.map(item => ({ id: item.relative, label: item.displayName || item.packageId || item.relative })) : [];
  } else {
    items = await Hub.readJson("/api/character/packages");
    options = Array.isArray(items) ? items.filter(item => !item.isTemplate).map(item => ({ id: item.relative, label: Hub.packageLabel(item) })) : [];
  }
  return options.filter(item => typeof item.id === "string" && item.id);
}
function openTargetPicker(tool) {
  if (!tool) return;
  state.targetTool = tool;
  el.targetTitle.textContent = tool.id === "diagnostics" ? "选择要检查的角色" : `打开${tool.name}`;
  el.targetLabel.textContent = ({ character: "选择角色", screen: "选择界面", library: "选择素材库", buff: "选择 Buff 包" })[tool.kind];
  el.targetSubmitButton.textContent = tool.id === "diagnostics" ? "查看诊断" : "打开编辑器";
  Hub.showDialog(el.targetDialog);
  loadTargetOptions();
}
async function loadTargetOptions() {
  const request = ++state.targetRequest;
  const tool = state.targetTool;
  state.targetItems = [];
  el.targetError.hidden = true;
  el.targetSubmitButton.disabled = true;
  el.targetRefreshButton.disabled = true;
  el.targetSelect.disabled = true;
  el.targetSelect.innerHTML = '<option value="">正在加载…</option>';
  try {
    const options = await getTargetOptions(tool);
    if (request !== state.targetRequest) return;
    if (!options.length) throw new Error(tool.kind === "character" ? "没有可用角色，请先新建角色。" : "没有可用内容，请先在对应工作台创建内容。");
    state.targetItems = options;
    el.targetSelect.innerHTML = options.map(item => `<option value="${Hub.escapeHtml(item.id)}">${Hub.escapeHtml(item.label)}</option>`).join("");
    const saved = Hub.readPreferences().targets?.[tool.kind];
    if (options.some(item => item.id === saved)) el.targetSelect.value = saved;
    el.targetSelect.disabled = false;
    el.targetSubmitButton.disabled = false;
    state.errors.delete(tool.id);
    renderTools();
    el.targetSelect.focus();
  } catch (error) {
    if (request !== state.targetRequest) return;
    const message = `无法打开${tool.name}：${error.message}`;
    el.targetSelect.innerHTML = '<option value="">暂无可用选项</option>';
    el.targetError.textContent = message;
    el.targetError.hidden = false;
    state.errors.set(tool.id, message);
    renderTools();
  } finally {
    if (request === state.targetRequest) el.targetRefreshButton.disabled = false;
  }
}
function submitTarget() {
  const target = state.targetItems.find(item => item.id === el.targetSelect.value);
  if (!target || el.targetSubmitButton.disabled) return;
  navigateTool(state.targetTool, target);
}
function navigateTool(tool, target) {
  const url = Hub.toolUrl(tool.id, target.id);
  if (!url) return;
  Hub.rememberTarget(tool.kind, target.id);
  Hub.rememberVisit(tool.id, target.id, target.label);
  window.location.assign(url);
}
async function openRecent(index) {
  const item = recentEntries()[index];
  if (!item) return;
  const tool = TOOLS.find(candidate => candidate.id === item.tool);
  const button = el.recentList.querySelector(`[data-recent="${index}"]`);
  if (button) button.disabled = true;
  try {
    const options = await getTargetOptions(tool);
    const target = options.find(candidate => candidate.id === item.targetId);
    if (!target) {
      state.errors.set(tool.id, "最近使用的内容已不存在，请重新选择。");
      renderTools();
      openTargetPicker(tool);
      return;
    }
    navigateTool(tool, target);
  } catch (error) {
    state.errors.set(tool.id, `无法打开：${error.message}`);
    renderTools();
  } finally { if (button) button.disabled = false; }
}

function openCreateDialog() {
  clearCreateError();
  el.createTemplate.value = "humanoid";
  el.createPackageId.value = "";
  el.createDisplayName.value = "";
  el.createDirectoryName.value = "";
  if (typeof el.createDialog.showModal === "function") {
    el.createDialog.showModal();
  } else {
    el.createDialog.setAttribute("open", "open");
  }
  el.createPackageId.focus();
}

function closeCreateDialog() {
  clearCreateError();
  if (typeof el.createDialog.close === "function") {
    el.createDialog.close();
  } else {
    el.createDialog.removeAttribute("open");
  }
}

function clearCreateError() {
  el.createError.hidden = true;
  el.createError.textContent = "";
}

function showCreateError(message) {
  el.createError.hidden = false;
  el.createError.textContent = message;
}

function syncCreateDefaults() {
  const packageId = normalizeCreatePackageId(el.createPackageId.value);
  if (!el.createDisplayName.value.trim()) {
    el.createDisplayName.value = buildDisplayName(packageId);
  }
  if (!el.createDirectoryName.value.trim()) {
    el.createDirectoryName.value = buildDirectoryName(packageId);
  }
}

async function submitCreatePackage(event) {
  event.preventDefault();
  clearCreateError();

  const payload = {
    packageId: el.createPackageId.value.trim(),
    displayName: el.createDisplayName.value.trim(),
    directoryName: el.createDirectoryName.value.trim(),
    template: el.createTemplate.value
  };

  if (!payload.packageId) {
    showCreateError("请填写角色包 ID。");
    el.createPackageId.focus();
    return;
  }

  setCreatePending(true);
  try {
    const response = await fetch("/api/character/create", {
      method: "POST",
      headers: {
        "Content-Type": "application/json",
        Accept: "application/json"
      },
      body: JSON.stringify(payload)
    });

    if (!response.ok) {
      let message = `${response.status} ${response.statusText}`;
      const errorText = await response.text();
      if (errorText) {
        try {
          const errorBody = JSON.parse(errorText);
          message = errorBody?.error || errorBody?.message || JSON.stringify(errorBody);
        } catch {
          message = errorText;
        }
      }
      throw new Error(message);
    }

    const result = await response.json();
    const packageRelative = result?.packageRelative;
    if (!packageRelative) throw new Error("创建服务未返回角色包位置。");

    state.packageRelative = packageRelative;
    Hub.rememberTarget("character", packageRelative);
    Hub.rememberVisit("character", packageRelative, payload.displayName || payload.packageId);
    closeCreateDialog();
    window.location.assign(`/Tools/MxFramework.CharacterStudio/web/?package=${encodeURIComponent(packageRelative)}`);
  } catch (error) {
    showCreateError(error.message || "创建角色失败。");
  } finally {
    setCreatePending(false);
  }
}

function setCreatePending(isPending) {
  el.createSubmitButton.disabled = isPending;
  el.createCancelButton.disabled = isPending;
  el.createCancelFooterButton.disabled = isPending;
  el.createPackageButton.disabled = isPending;
  el.createSubmitButton.textContent = isPending ? "正在创建…" : "创建并打开";
}

async function restartEditorService() {
  el.restartServiceButton.disabled = true;
  el.refreshButton.disabled = true;
  el.createPackageButton.disabled = true;
  el.serverStatus.textContent = "正在重启本机服务…";

  try {
    const response = await fetch("/api/editor/restart", {
      method: "POST",
      headers: { Accept: "application/json" }
    });

    if (!response.ok) {
      const text = await response.text();
      throw new Error(text || `${response.status} ${response.statusText}`);
    }

    await waitForServiceRecovery();
    window.location.reload();
  } catch (error) {
    el.serverStatus.textContent = `重启失败：${error.message || error}`;
    el.restartServiceButton.disabled = false;
    el.refreshButton.disabled = false;
    el.createPackageButton.disabled = false;
  }
}

async function waitForServiceRecovery(timeoutMs = 20000) {
  const deadline = Date.now() + timeoutMs;
  let sawDisconnect = false;

  while (Date.now() < deadline) {
    try {
      const response = await fetch("/api/character/packages", {
        headers: { Accept: "application/json" },
        cache: "no-store"
      });

      if (response.ok) {
        if (!sawDisconnect) {
          sawDisconnect = true;
        } else {
          return;
        }
      }
    } catch {
      sawDisconnect = true;
    }

    await delay(750);
  }

  throw new Error("等待服务恢复超时，请重新运行启动脚本。");
}

function delay(ms) {
  return new Promise(resolve => window.setTimeout(resolve, ms));
}

function normalizeCreatePackageId(value) {
  return String(value || "")
    .trim()
    .toLowerCase()
    .replace(/[^a-z0-9._-]+/g, "_")
    .replace(/[.-]+/g, "_")
    .replace(/_+/g, "_")
    .replace(/^_+|_+$/g, "");
}

function buildDisplayName(packageId) {
  if (!packageId) return "";
  return packageId
    .split(/[_\-.]+/)
    .filter(Boolean)
    .map(part => part.charAt(0).toUpperCase() + part.slice(1))
    .join(" ");
}

function buildDirectoryName(packageId) {
  const normalized = normalizeCreatePackageId(packageId).replace(/_/g, "-");
  if (!normalized) return "";
  return normalized.startsWith("character-") ? normalized : `character-${normalized}`;
}
