const SURFACE_PALETTE_FORMAT_V3 = "mx.wgameSurfacePaletteCatalog.v3";
const COLOR_COUNT = 256 * 256;
const PROFILE_COUNT = 16;
const CELL_HEIGHT = 96;
const GRID_GAP = 8;
const PALETTE_CULL_MODES = new Set(["Off", "Front", "Back"]);

// ---------------------------------------------------------------------------
// 两个 usage 槽位的职责边界（不要混用）
//
// - state.package.paletteUsage
//     磁盘上 config/character_palette_usage.json 的已提交内容，来自 /api/character/state。
//     只读参照物：用来展示「当前落盘的是什么」，不参与提交。
//
// - state.surfacePalettePreviewUsage
//     本次 3D 预览扫描出的待提交内容，由 viewport3d.js 在加载本体模型时写入，
//     并附带归属信息（包 / 本体资源 / 源模型哈希 / 加载代次，见 validateSurfacePaletteUsageOwner）。
//     切包或重载会整体作废；保存成功后才会同步回 state.package.paletteUsage。
//
// 只有「扫描结果 == 归属信息所指的模型版本」时才允许提交；服务端只能判定 modelHash，
// 无法重算 tokenUsages，所以错配必须在提交前拦下，而不是靠服务端改写。
// ---------------------------------------------------------------------------

const PROFILE_LABELS = {
  skin_soft: "柔和皮肤",
  skin_glossy: "光泽皮肤",
  hair_matte: "哑光头发",
  hair_sheen: "柔亮头发",
  fabric_woven: "织物",
  fabric_silk: "丝绸",
  leather_rough: "粗糙皮革",
  leather_polished: "抛光皮革",
  wood_raw: "原木",
  wood_varnished: "清漆木材",
  matte_solid_rough: "粗哑光实体",
  matte_solid_smooth: "细哑光实体",
  metal_brushed: "拉丝金属",
  metal_polished: "抛光金属",
  crystal_clear: "通透晶体",
  crystal_frosted: "磨砂晶体"
};

const workspace = {
  wired: false,
  loading: false,
  catalog: null,
  rgb8: null,
  filtered: [],
  selectedColorIndex: 0,
  selectedProfile: 0,
  references: null,
  query: "",
  showTechnical: false,
  view: "slice",
  red5: 0,
  green6: "",
  blue5: "",
  columns: 6,
  favorites: new Set(),
  recent: [],
  resizeObserver: null
};

function getHost() {
  return document.getElementById("surfacePaletteWorkbench");
}

function decodeBase64(value) {
  const binary = atob(String(value || ""));
  const bytes = new Uint8Array(binary.length);
  for (let index = 0; index < binary.length; index++) bytes[index] = binary.charCodeAt(index);
  return bytes;
}

function hydrateCatalog(catalog) {
  if (catalog?.format !== SURFACE_PALETTE_FORMAT_V3
    || Number(catalog.width) !== 256
    || Number(catalog.height) !== 256
    || Number(catalog.colorCount) !== COLOR_COUNT
    || Number(catalog.surfaceProfileCount) !== PROFILE_COUNT
    || !Array.isArray(catalog.surfaceProfiles)
    || catalog.surfaceProfiles.length !== PROFILE_COUNT) {
    throw new Error("全局画板不是 RGB565 Color Palette v3 / 16 Surface Profile。");
  }
  const rgb8 = decodeBase64(catalog.colorData?.rgb8Base64);
  if (rgb8.byteLength !== COLOR_COUNT * 3) throw new Error(`RGB8 表长度错误：${rgb8.byteLength}`);
  workspace.catalog = catalog;
  workspace.rgb8 = rgb8;
  if (Number(catalog.colorData?.uniqueRgb8Count) !== COLOR_COUNT || Number(catalog.colorData?.aliasCount) !== 0)
    throw new Error("RGB565 Catalog 必须包含 65,536 个唯一 RGB8，且 aliasCount=0。");
  state.surfacePaletteCatalog = catalog;
}

function loadLocalCollections() {
  try {
    workspace.favorites = new Set(JSON.parse(localStorage.getItem("mx.surfacePalette.v2.favorites") || "[]")
      .map(Number).filter(value => Number.isInteger(value) && value >= 0 && value < COLOR_COUNT));
    workspace.recent = JSON.parse(localStorage.getItem("mx.surfacePalette.v2.recent") || "[]")
      .map(Number).filter(value => Number.isInteger(value) && value >= 0 && value < COLOR_COUNT).slice(0, 48);
    workspace.selectedProfile = Math.max(0, Math.min(15, Number(localStorage.getItem("mx.surfacePalette.v2.profile")) || 0));
  } catch {
    workspace.favorites = new Set();
    workspace.recent = [];
    workspace.selectedProfile = 0;
  }
}

function saveLocalCollections() {
  localStorage.setItem("mx.surfacePalette.v2.favorites", JSON.stringify([...workspace.favorites]));
  localStorage.setItem("mx.surfacePalette.v2.recent", JSON.stringify(workspace.recent.slice(0, 48)));
  localStorage.setItem("mx.surfacePalette.v2.profile", String(workspace.selectedProfile));
}

async function loadSurfacePaletteCatalog({ force = false } = {}) {
  if (state.package?.bakedSurfaceBinding || (state.package?.bakedSurfaceIssues || []).length
      || !state.package?.paletteBinding?.enabled) {
    renderSurfacePaletteWorkbench();
    return null;
  }
  if (workspace.loading) return null;
  workspace.loading = true;
  try {
    if (!force && state.surfacePaletteCatalog?.format === SURFACE_PALETTE_FORMAT_V3) {
      hydrateCatalog(state.surfacePaletteCatalog);
    } else {
      const catalog = await readJson("/api/authoring/palette/catalog", null);
      hydrateCatalog(catalog);
    }
    applyFilters();
    renderSurfacePaletteWorkbench();
    if (!state.package?.bakedSurfaceBinding && !(state.package?.bakedSurfaceIssues || []).length
        && state.package?.paletteBinding?.enabled) publishSelection();
    return workspace.catalog;
  } catch (error) {
    workspace.catalog = null;
    workspace.rgb8 = null;
    renderSurfacePaletteWorkbench(error?.message || "无法读取全局 RGB565 Color Palette v3。", true);
    return null;
  } finally {
    workspace.loading = false;
  }
}

async function saveSurfacePalettePreviewUsage({ suppressMessage = false } = {}) {
  if (state.package?.bakedSurfaceBinding || (state.package?.bakedSurfaceIssues || []).length) return true;
  if (!state.package?.paletteBinding?.enabled) return true;
  const usage = state.surfacePalettePreviewUsage;
  if (usage?.format !== "mx.characterPaletteUsage.v2" || !usage.catalogHash || !usage.modelHash) {
    if (!suppressMessage) {
      state.message = "3D 预览尚未完成 Color + Surface Profile 扫描，暂不能生成 usage v2。";
      renderShellStatus?.();
    }
    return false;
  }
  // 归属复核：只有「当前包 + 当前本体资源 + 当前模型版本」的扫描结果才允许落盘。
  // 服务端只能判定 modelHash，无法重算 tokenUsages，所以错配必须在提交前就拦下来。
  const ownership = globalThis.validateSurfacePaletteUsageOwner?.(usage) || { ok: true };
  if (!ownership.ok) {
    if (!suppressMessage) {
      state.message = ownership.reason || "本次 3D 预览扫描结果已作废，请等待重新扫描后再保存。";
      renderShellStatus?.();
    }
    return false;
  }
  const response = await postJsonDetailed("/api/authoring/palette/usage", {
    package: state.packageRelative,
    expectedRevision: state.packageRevision || "",
    usage
  });
  if (!response?.success) {
    state.message = response?.message || "character_palette_usage.json 保存失败。";
    renderShellStatus?.();
    return false;
  }
  state.package.paletteUsage = clone(usage);
  state.packageRevision = response.revision || state.packageRevision;
  return true;
}

async function saveSurfacePaletteBinding({ suppressMessage = false } = {}) {
  if (state.package?.bakedSurfaceBinding || (state.package?.bakedSurfaceIssues || []).length) return true;
  const binding = state.package?.paletteBinding;
  if (!binding) return true;
  const response = await postJsonDetailed("/api/authoring/palette/binding", {
    package: state.packageRelative,
    expectedRevision: state.packageRevision || "",
    binding
  });
  if (!response?.success) {
    if (!suppressMessage) state.message = response?.message || "character_palette_binding.json 保存失败。";
    return false;
  }
  state.packageRevision = response.revision || state.packageRevision;
  return true;
}

function profileAt(index = workspace.selectedProfile) {
  return workspace.catalog?.surfaceProfiles?.[index] || null;
}

function colorCoordinates(colorIndex) {
  return { x: colorIndex & 255, y: colorIndex >> 8 };
}

function rgb565Components(colorIndex) {
  return { red5: (colorIndex >>> 11) & 31, green6: (colorIndex >>> 5) & 63, blue5: colorIndex & 31 };
}

function colorIndexFromRgb565(red5, green6, blue5) {
  return ((red5 & 31) << 11) | ((green6 & 63) << 5) | (blue5 & 31);
}

function colorIndexFromRgb8(red, green, blue) {
  const red5 = Math.floor((Math.max(0, Math.min(255, red)) * 31 + 127) / 255);
  const green6 = Math.floor((Math.max(0, Math.min(255, green)) * 63 + 127) / 255);
  const blue5 = Math.floor((Math.max(0, Math.min(255, blue)) * 31 + 127) / 255);
  return colorIndexFromRgb565(red5, green6, blue5);
}

function colorIndexFromHex(value) {
  const match = /^#?([0-9a-f]{6})$/i.exec(String(value || "").trim());
  if (!match) return null;
  const packed = Number.parseInt(match[1], 16);
  return colorIndexFromRgb8((packed >>> 16) & 255, (packed >>> 8) & 255, packed & 255);
}

function colorId(colorIndex) {
  const { red5, green6, blue5 } = rgb565Components(colorIndex);
  return `wgame.color.v3.r${String(red5).padStart(2, "0")}.g${String(green6).padStart(2, "0")}.b${String(blue5).padStart(2, "0")}`;
}

function tokenId(colorIndex, profileIndex = workspace.selectedProfile) {
  const profile = profileAt(profileIndex);
  return colorId(colorIndex).replace("wgame.color", "wgame.surface") + `.${profile?.profileId || "invalid"}`;
}

function rgbAt(colorIndex) {
  const offset = colorIndex * 3;
  return [workspace.rgb8[offset], workspace.rgb8[offset + 1], workspace.rgb8[offset + 2]];
}

function hexAt(colorIndex) {
  return `#${rgbAt(colorIndex).map(value => value.toString(16).padStart(2, "0")).join("").toUpperCase()}`;
}

function srgbToLinear(value) {
  const normalized = value / 255;
  return normalized <= 0.04045 ? normalized / 12.92 : Math.pow((normalized + 0.055) / 1.055, 2.4);
}

function actualOklch(colorIndex) {
  const [red, green, blue] = rgbAt(colorIndex);
  const r = srgbToLinear(red);
  const g = srgbToLinear(green);
  const b = srgbToLinear(blue);
  const lRoot = Math.cbrt(0.4122214708 * r + 0.5363325363 * g + 0.0514459929 * b);
  const mRoot = Math.cbrt(0.2119034982 * r + 0.6806995451 * g + 0.1073969566 * b);
  const sRoot = Math.cbrt(0.0883024619 * r + 0.2817188376 * g + 0.6299787005 * b);
  const l = 0.2104542553 * lRoot + 0.793617785 * mRoot - 0.0040720468 * sRoot;
  const a = 1.9779984951 * lRoot - 2.428592205 * mRoot + 0.4505937099 * sRoot;
  const labB = 0.0259040371 * lRoot + 0.7827717662 * mRoot - 0.808675766 * sRoot;
  const c = Math.hypot(a, labB);
  const h = c > 1e-9 ? (Math.atan2(labB, a) * 180 / Math.PI + 360) % 360 : 0;
  return { l, c, h };
}

function describeColor(colorIndex) {
  const { x, y } = colorCoordinates(colorIndex);
  const { red5, green6, blue5 } = rgb565Components(colorIndex);
  return {
    colorIndex,
    x,
    y,
    red5,
    green6,
    blue5,
    srgbHex: hexAt(colorIndex),
    canonicalColorIndex: colorIndex,
    isAlias: false,
    normalizedRgb565: { r: red5 / 31, g: green6 / 63, b: blue5 / 31 },
    oklch: actualOklch(colorIndex),
  };
}

function selectedToken() {
  if (!workspace.catalog || !workspace.rgb8) return null;
  const color = describeColor(workspace.selectedColorIndex);
  const profile = profileAt();
  return {
    ...color,
    colorId: colorId(color.colorIndex),
    tokenId: tokenId(color.colorIndex),
    logicalToken: color.colorIndex * PROFILE_COUNT + workspace.selectedProfile,
    surfaceProfile: workspace.selectedProfile,
    surfaceProfileId: profile?.profileId,
    profile
  };
}

function publishSelection() {
  const selection = selectedToken();
  state.surfacePaletteSelection = selection;
  globalThis.dispatchEvent?.(new CustomEvent("wgame-surface-palette-selection", { detail: selection }));
}

function getCurrentUsageColorIndices() {
  const usages = state.package?.paletteUsage?.tokenUsages || [];
  return new Set(usages.map(item => Number(item?.colorIndex)).filter(value => Number.isInteger(value) && value >= 0));
}

function parseDirectQuery(text) {
  if (!text) return null;
  const fromHex = colorIndexFromHex(text);
  if (fromHex !== null) return fromHex;
  const numeric = Number(text);
  if (Number.isInteger(numeric)) {
    if (numeric >= 0 && numeric < COLOR_COUNT) return numeric;
    if (numeric >= 0 && numeric < COLOR_COUNT * PROFILE_COUNT) return Math.floor(numeric / PROFILE_COUNT);
  }
  let match = /^\(?\s*(\d{1,3})\s*[,/]\s*(\d{1,3})\s*\)?$/.exec(text);
  if (match) {
    const x = Number(match[1]);
    const y = Number(match[2]);
    if (x < 256 && y < 256) return y * 256 + x;
  }
  match = /wgame\.(?:color|surface)\.v3\.r(\d{2})\.g(\d{2})\.b(\d{2})/i.exec(text);
  if (match) {
    const red5 = Number(match[1]);
    const green6 = Number(match[2]);
    const blue5 = Number(match[3]);
    if (red5 < 32 && green6 < 64 && blue5 < 32) return colorIndexFromRgb565(red5, green6, blue5);
  }
  return null;
}

function applyFilters() {
  if (!workspace.catalog || !workspace.rgb8) {
    workspace.filtered = [];
    return;
  }
  const text = workspace.query.trim().toLowerCase();
  const direct = parseDirectQuery(text);
  const used = getCurrentUsageColorIndices();
  const recent = new Set(workspace.recent);
  const result = [];
  for (let colorIndex = 0; colorIndex < COLOR_COUNT; colorIndex++) {
    const { x, y } = colorCoordinates(colorIndex);
    const { red5, green6, blue5 } = rgb565Components(colorIndex);
    if (workspace.view === "slice" && red5 !== Number(workspace.red5)) continue;
    if (workspace.green6 !== "" && green6 !== Number(workspace.green6)) continue;
    if (workspace.blue5 !== "" && blue5 !== Number(workspace.blue5)) continue;
    if (workspace.view === "used" && !used.has(colorIndex)) continue;
    if (workspace.view === "favorites" && !workspace.favorites.has(colorIndex)) continue;
    if (workspace.view === "recent" && !recent.has(colorIndex)) continue;
    if (direct !== null && colorIndex !== direct) continue;
    if (text && direct === null) {
      const searchable = `${colorId(colorIndex)} ${colorIndex} ${x},${y} r${red5} g${green6} b${blue5} ${hexAt(colorIndex)}`.toLowerCase();
      if (!searchable.includes(text)) continue;
    }
    result.push(colorIndex);
  }
  if (workspace.view === "recent") {
    const order = new Map(workspace.recent.map((value, position) => [value, position]));
    result.sort((left, right) => (order.get(left) ?? 9999) - (order.get(right) ?? 9999));
  }
  workspace.filtered = result;
}

function renderSurfacePaletteWorkbench(message = "", isError = false) {
  const host = getHost();
  if (!host) return;
  workspace.resizeObserver?.disconnect?.();
  workspace.resizeObserver = null;
  // Resolve the character's surface before loading the historical global atlas.
  if (state.package?.bakedSurfaceBinding || (state.package?.bakedSurfaceIssues || []).length) {
    host.innerHTML = `<div class="surface-palette-shell"><div class="surface-palette-summary"><strong>角色材质 · 烘焙贴图</strong></div>${renderSelectionEditor()}</div>`;
    return;
  }
  if (!state.package?.paletteBinding?.enabled) {
    host.innerHTML = `<div class="surface-palette-empty"><h3>角色材质 · 烘焙发布</h3><p>新角色默认使用烘焙底色和材质控制图。</p><p>请在 Blender 的 WGame Rig 中点击“发布到 Unity”。已有传统贴图材质继续由原材料导入管理。</p></div>`;
    return;
  }
  if (!workspace.catalog) {
    host.innerHTML = `<div class="surface-palette-empty${isError ? " is-error" : ""}">${escapeHtml(message || "正在读取全规律 Color Palette...")}</div>`;
    if (!message && !workspace.loading) void loadSurfacePaletteCatalog();
    return;
  }
  const usage = state.package?.paletteUsage || null;
  host.innerHTML = `
    <div class="surface-palette-shell">
      <div class="surface-palette-summary">
        <div><strong>角色材质 · 历史色板兼容</strong><span>此角色继续使用历史色板；在 Blender 中选择“转为烘焙发布”可转换。制作阶段仍可使用色板配色。</span></div>
        <div class="surface-palette-summary-meta">
          <code>${escapeHtml(workspace.catalog.catalogHash || "")}</code>
          <span>65,536 唯一 RGB · 0 Alias · 16 Profile · 1,048,576 逻辑组合</span>
          <span class="${usage?.paletteRepaintRequired ? "is-warning" : ""}">${usage ? (usage.paletteRepaintRequired ? `当前角色待重画 · ${usage.invalidFaceCount || 0} 面` : `当前角色使用 ${(usage.tokenUsages || []).length} 个组合`) : "当前角色无 usage v2"}</span>
          <button type="button" data-palette-action="technical">${workspace.showTechnical ? "隐藏技术地址" : "技术地址浏览"}</button>
          <button type="button" data-palette-action="refresh">刷新画板</button>
        </div>
      </div>
      ${workspace.showTechnical ? `<div class="surface-palette-toolbar">
        <label>搜索或近似颜色<input type="search" data-palette-filter="query" value="${escapeHtml(workspace.query)}" placeholder="任意 Hex / colorIndex / logicalToken / x,y / Token ID"></label>
        <label>视图<select data-palette-filter="view">
          <option value="slice"${workspace.view === "slice" ? " selected" : ""}>当前 R5 切片</option>
          <option value="all"${workspace.view === "all" ? " selected" : ""}>技术视图：全部地址</option>
          <option value="used"${workspace.view === "used" ? " selected" : ""}>当前模型已用</option>
          <option value="recent"${workspace.view === "recent" ? " selected" : ""}>最近使用</option>
          <option value="favorites"${workspace.view === "favorites" ? " selected" : ""}>收藏</option>
        </select></label>
        <label>R5 切片<input type="number" data-palette-filter="red5" value="${escapeHtml(workspace.red5)}" min="0" max="31"></label>
        <label>固定 G6<input type="number" data-palette-filter="green6" value="${escapeHtml(workspace.green6)}" min="0" max="63" placeholder="全部"></label>
        <label>固定 B5<input type="number" data-palette-filter="blue5" value="${escapeHtml(workspace.blue5)}" min="0" max="31" placeholder="全部"></label>
      </div>` : ""}
      <div class="surface-palette-main${workspace.showTechnical ? "" : " is-picker-only"}">
        ${workspace.showTechnical ? `<div class="surface-palette-browser">
          <div class="surface-palette-count">匹配 ${workspace.filtered.length} 个唯一颜色地址；原始 65K Atlas 仅在技术视图展开</div>
          <div class="surface-palette-grid-viewport" data-palette-grid tabindex="0"><div class="surface-palette-grid-spacer"></div><div class="surface-palette-grid-visible"></div></div>
        </div>` : ""}
        <aside class="surface-palette-editor">${renderSelectionEditor()}</aside>
      </div>
    </div>`;
  wireRenderedControls();
  renderVirtualGrid();
}

function renderSelectionEditor() {
  const baked = state.package?.bakedSurfaceBinding;
  if (baked || (state.package?.bakedSurfaceIssues || []).length) {
    const issues = state.package?.bakedSurfaceIssues || [];
    return `<section><h3>烘焙材质</h3><p>${issues.length ? "烘焙资源需要修复" : "运行时使用合成底色与材质控制图"}</p>
      <p>制作源：${escapeHtml(baked?.authoringSourcePath || "未记录")}</p>
      <p>底色：${escapeHtml(baked?.baseColorFile || "缺失")}</p>
      <p>材质控制：${escapeHtml(baked?.surfaceDataFile || "缺失")}</p>
      <p>尺寸：${Number(baked?.textureSize) || 0} · 头发方向来自导出网格</p>
      ${issues.map(issue => `<p>${escapeHtml(issue.message || issue.Message || "烘焙校验失败")}</p>`).join("")}
      <p>请在 Blender 修改配色、细节和头发，再重新烘焙导出。</p></section>`;
  }
  const token = selectedToken();
  if (!token) return "<section><h3>请选择颜色</h3></section>";
  const favorite = workspace.favorites.has(token.colorIndex);
  const references = workspace.references?.logicalToken === token.logicalToken ? workspace.references : null;
  const profiles = workspace.catalog.surfaceProfiles.map(profile => {
    const label = PROFILE_LABELS[profile.profileId] || profile.displayName || profile.profileId;
    return `<button type="button" class="surface-palette-profile${profile.index === workspace.selectedProfile ? " is-selected" : ""}" data-profile-index="${profile.index}" title="${escapeHtml(profile.profileId)} · Alpha ${profile.alphaByte}"><strong>${profile.index}. ${escapeHtml(label)}</strong><span>${escapeHtml(profile.profileId)}</span></button>`;
  }).join("");
  const binding = state.package?.paletteBinding || {};
  const faceResources = (state.package?.resourceCatalog?.entries || []).filter(item =>
    String(item?.typeId || "").toLowerCase() === "texture"
    && (item?.importHints?.metadata?.bindTarget === "faceDetail" || item?.resourceKey === binding.faceDetailTextureResourceKey));
  const faceOptions = faceResources.map(item => `<option value="${escapeHtml(item.resourceKey || "")}"${item.resourceKey === binding.faceDetailTextureResourceKey ? " selected" : ""}>${escapeHtml(item.localId || item.resourceKey || "FaceDetail")}</option>`).join("");
  const cullMode = String(binding.cullMode || "").trim();
  const invalidCullModeOption = PALETTE_CULL_MODES.has(cullMode)
    ? ""
    : `<option value="" selected disabled>配置无效：${escapeHtml(cullMode || "未设置")}</option>`;
  return `<section>
    <div class="surface-palette-selected-head"><span class="surface-palette-selected-swatch" style="--token-color:${token.srgbHex}"></span><div><h3>${token.srgbHex}</h3><code>${escapeHtml(token.colorId)}</code></div></div>
    <div class="surface-palette-color-picker">
      <label>颜色选择器<input type="color" data-palette-picker="color" value="${token.srgbHex}"></label>
      <label>输入任意 Hex<input type="text" data-palette-picker="hex" value="${token.srgbHex}" pattern="#?[0-9A-Fa-f]{6}"></label>
      <button type="button" data-palette-action="applyHex">映射到最近的唯一 RGB565</button>
    </div>
    <dl>
      <dt>Color index / 坐标</dt><dd>${token.colorIndex} · (${token.x}, ${token.y})</dd>
      <dt>RGB565 公式</dt><dd>R5 ${token.red5}/31 · G6 ${token.green6}/63 · B5 ${token.blue5}/31</dd>
      <dt>最终 RGB8</dt><dd>${token.srgbHex}</dd>
      <dt>最终 OKLCH</dt><dd>L ${formatNumber(token.oklch.l, 4)} · C ${formatNumber(token.oklch.c, 4)} · H ${formatNumber(token.oklch.h, 2)}°</dd>
      <dt>唯一性</dt><dd>此地址即 Canonical · 无 Alias</dd>
    </dl>
    <div class="surface-palette-editor-actions"><button type="button" data-palette-action="favorite">${favorite ? "取消收藏" : "收藏颜色"}</button><button type="button" data-palette-action="references">查看当前组合引用</button></div>
    <hr><h3>第二步：选择 Surface Profile</h3>
    <div class="surface-palette-profile-grid">${profiles}</div>
    <dl>
      <dt>Profile</dt><dd>${token.surfaceProfile}. ${escapeHtml(PROFILE_LABELS[token.surfaceProfileId] || token.surfaceProfileId)}</dd>
      <dt>顶点 Alpha</dt><dd>${token.profile.alphaByte} / 255</dd>
      <dt>Logical token</dt><dd>${token.logicalToken}</dd>
      <dt>Token ID</dt><dd><code>${escapeHtml(token.tokenId)}</code></dd>
      <dt>响应</dt><dd>Shadow ${formatNumber(token.profile.shadowFloor, 3)} · Wrap ${formatNumber(token.profile.wrap, 3)} · Spec ${formatNumber(token.profile.specular, 3)} · Power ${formatNumber(token.profile.specularPower, 0)} · Rim ${formatNumber(token.profile.rim, 3)}</dd>
      <dt>程序细节</dt><dd>${escapeHtml(token.profile.detailMode)} · Scale ${formatNumber(token.profile.detailScale, 0)}</dd>
    </dl>
    ${references ? `<div class="surface-palette-references"><strong>${references.referenceCount} 个版本化引用 · ${references.faceCount} 面</strong>${(references.references || []).map(item => `<div>${escapeHtml(item.packageId || item.package)} · ${item.faceCount} 面 · ${escapeHtml(item.source)}</div>`).join("") || "<div>暂无版本化 usage 引用</div>"}</div>` : ""}
    <hr><h3>角色面渲染</h3>
    <label>渲染面<select data-palette-binding-field="cullMode">
      ${invalidCullModeOption}
      <option value="Back"${cullMode === "Back" ? " selected" : ""}>仅正面（剔除背面）</option>
      <option value="Off"${cullMode === "Off" ? " selected" : ""}>双面（不剔除）</option>
      <option value="Front"${cullMode === "Front" ? " selected" : ""}>仅背面（剔除正面）</option>
    </select></label>
    <p>控制角色 Palette 材质的面剔除方式；保存后通过 Unity 导入写入生成材质。</p>
    <hr><h3>角色 FaceDetail</h3>
    <label><input type="checkbox" data-face-detail-field="enabled"${binding.faceDetailEnabled ? " checked" : ""}> 启用角色专属 512 Direct RGBA</label>
    <label>角色贴图资源<select data-face-detail-field="resource"><option value="">请选择角色包贴图</option>${faceOptions}</select></label>
    <label>细节强度<input type="range" min="0" max="1" step="0.01" value="${Number(binding.faceDetailIntensity ?? 1)}" data-face-detail-field="intensity"></label>
    <p>${binding.faceDetailEnabled ? (binding.faceDetailTextureResourceKey ? "启用时使用 FBX uv2 / FaceDetailUV 直接采样。" : "缺少角色专属 FaceDetail 资源，保存与导入将被阻止。") : "当前角色未启用 FaceDetail。"}</p>
    <p>任意输入颜色都会量化到最近的唯一 RGB565 地址；颜色公式和 16 套材质响应都是生成器权威数据。</p>
  </section>`;
}

function formatNumber(value, digits) {
  const number = Number(value);
  return Number.isFinite(number) ? number.toFixed(digits) : "-";
}

function wireRenderedControls() {
  const host = getHost();
  for (const input of host.querySelectorAll("[data-palette-filter]")) {
    const eventName = input.type === "search" ? "input" : "change";
    input.addEventListener(eventName, () => {
      workspace[input.dataset.paletteFilter] = input.value;
      applyFilters();
      const viewport = host.querySelector("[data-palette-grid]");
      if (viewport) viewport.scrollTop = 0;
      const refocus = input.type === "search";
      const selectionStart = input.selectionStart;
      renderSurfacePaletteWorkbench();
      if (refocus) {
        const replacement = getHost()?.querySelector('[data-palette-filter="query"]');
        replacement?.focus();
        replacement?.setSelectionRange?.(selectionStart, selectionStart);
      }
    });
  }
  for (const button of host.querySelectorAll("[data-palette-action]")) {
    button.addEventListener("click", () => handleAction(button.dataset.paletteAction));
  }
  const colorPicker = host.querySelector('[data-palette-picker="color"]');
  const hexPicker = host.querySelector('[data-palette-picker="hex"]');
  colorPicker?.addEventListener("input", () => { if (hexPicker) hexPicker.value = colorPicker.value.toUpperCase(); });
  hexPicker?.addEventListener("input", () => {
    if (/^#?[0-9a-f]{6}$/i.test(hexPicker.value) && colorPicker)
      colorPicker.value = (hexPicker.value.startsWith("#") ? hexPicker.value : `#${hexPicker.value}`).toLowerCase();
  });
  for (const button of host.querySelectorAll("[data-profile-index]")) {
    button.addEventListener("click", () => selectProfile(Number(button.dataset.profileIndex)));
  }
  for (const input of host.querySelectorAll("[data-face-detail-field]")) {
    input.addEventListener("change", () => {
      const binding = state.package?.paletteBinding;
      if (!binding || binding.format !== "mx.characterPaletteBinding.v5") return;
      const field = input.dataset.faceDetailField;
      globalThis.beginUndoTransaction?.("编辑角色 FaceDetail", `surfacePalette.faceDetail.${field}`);
      if (field === "enabled") binding.faceDetailEnabled = Boolean(input.checked);
      if (field === "resource") binding.faceDetailTextureResourceKey = input.value || "";
      if (field === "intensity") binding.faceDetailIntensity = Math.min(1, Math.max(0, Number(input.value) || 0));
      state.dirty = true;
      globalThis.commitUndoTransaction?.();
      renderSurfacePaletteWorkbench();
      renderShellStatus?.();
    });
  }
  for (const input of host.querySelectorAll("[data-palette-binding-field]")) {
    input.addEventListener("change", () => {
      const binding = state.package?.paletteBinding;
      if (!binding || binding.format !== "mx.characterPaletteBinding.v5") return;
      const field = input.dataset.paletteBindingField;
      if (field !== "cullMode" || !PALETTE_CULL_MODES.has(input.value)) return;
      globalThis.beginUndoTransaction?.("修改角色面渲染", `surfacePalette.${field}`);
      binding.cullMode = input.value;
      state.dirty = true;
      globalThis.commitUndoTransaction?.();
      renderSurfacePaletteWorkbench();
      renderShellStatus?.();
      globalThis.renderViewport?.();
    });
  }
}

function updateGridColumns(viewport) {
  const available = Math.max(320, viewport.clientWidth - 16);
  workspace.columns = Math.max(2, Math.floor((available + GRID_GAP) / (124 + GRID_GAP)));
}

function renderVirtualGrid() {
  const viewport = getHost()?.querySelector("[data-palette-grid]");
  const spacer = viewport?.querySelector(".surface-palette-grid-spacer");
  const visible = viewport?.querySelector(".surface-palette-grid-visible");
  if (!viewport || !spacer || !visible) return;
  updateGridColumns(viewport);
  const rowCount = Math.ceil(workspace.filtered.length / workspace.columns);
  spacer.style.height = `${Math.max(1, rowCount * (CELL_HEIGHT + GRID_GAP))}px`;
  const draw = () => {
    const startRow = Math.max(0, Math.floor(viewport.scrollTop / (CELL_HEIGHT + GRID_GAP)) - 2);
    const visibleRows = Math.ceil(viewport.clientHeight / (CELL_HEIGHT + GRID_GAP)) + 4;
    const endRow = Math.min(rowCount, startRow + visibleRows);
    visible.style.transform = `translateY(${startRow * (CELL_HEIGHT + GRID_GAP)}px)`;
    visible.style.gridTemplateColumns = `repeat(${workspace.columns}, minmax(0, 1fr))`;
    const start = startRow * workspace.columns;
    const end = Math.min(workspace.filtered.length, endRow * workspace.columns);
    visible.innerHTML = workspace.filtered.slice(start, end).map(colorIndex => {
      const { x, y } = colorCoordinates(colorIndex);
      const { red5, green6, blue5 } = rgb565Components(colorIndex);
      const selected = colorIndex === workspace.selectedColorIndex;
      const favorite = workspace.favorites.has(colorIndex);
      const hex = hexAt(colorIndex);
      return `<button type="button" class="surface-palette-token${selected ? " is-selected" : ""}" data-color-index="${colorIndex}" title="${escapeHtml(colorId(colorIndex))}">
        <span class="surface-palette-token-color" style="--token-color:${hex}"></span>
        <span class="surface-palette-token-id">${favorite ? "★ " : ""}#${colorIndex} · (${x},${y})</span>
        <span>${hex} · R${red5} G${green6} B${blue5}</span>
      </button>`;
    }).join("");
    for (const button of visible.querySelectorAll("[data-color-index]")) {
      button.addEventListener("click", () => selectColor(Number(button.dataset.colorIndex)));
    }
  };
  viewport.addEventListener("scroll", draw, { passive: true });
  workspace.resizeObserver = new ResizeObserver(() => { updateGridColumns(viewport); draw(); });
  workspace.resizeObserver.observe(viewport);
  draw();
}

function selectColor(colorIndex) {
  if (!Number.isInteger(colorIndex) || colorIndex < 0 || colorIndex >= COLOR_COUNT) return;
  workspace.selectedColorIndex = colorIndex;
  workspace.red5 = rgb565Components(colorIndex).red5;
  workspace.references = null;
  workspace.recent = [colorIndex, ...workspace.recent.filter(value => value !== colorIndex)].slice(0, 48);
  saveLocalCollections();
  applyFilters();
  publishSelection();
  renderSurfacePaletteWorkbench();
}

function selectProfile(profileIndex) {
  if (!Number.isInteger(profileIndex) || profileIndex < 0 || profileIndex >= PROFILE_COUNT) return;
  workspace.selectedProfile = profileIndex;
  workspace.references = null;
  saveLocalCollections();
  publishSelection();
  renderSurfacePaletteWorkbench();
}

async function handleAction(action) {
  if (action === "refresh") return loadSurfacePaletteCatalog({ force: true });
  if (action === "technical") {
    workspace.showTechnical = !workspace.showTechnical;
    renderSurfacePaletteWorkbench();
    return;
  }
  if (action === "applyHex") {
    const value = getHost()?.querySelector('[data-palette-picker="hex"]')?.value || "";
    const mapped = colorIndexFromHex(value);
    if (mapped === null) return;
    return selectColor(mapped);
  }
  const token = selectedToken();
  if (!token) return;
  if (action === "favorite") {
    if (workspace.favorites.has(token.colorIndex)) workspace.favorites.delete(token.colorIndex);
    else workspace.favorites.add(token.colorIndex);
    saveLocalCollections();
    applyFilters();
    renderSurfacePaletteWorkbench();
    return;
  }
  if (action === "references") {
    workspace.references = await readJson(`/api/authoring/palette/references?logicalToken=${token.logicalToken}`, null);
    renderSurfacePaletteWorkbench();
  }
}

function wireSurfacePaletteWorkbench() {
  if (workspace.wired) return;
  workspace.wired = true;
  loadLocalCollections();
  loadSurfacePaletteCatalog();
}

Object.assign(globalThis, {
  wireSurfacePaletteWorkbench,
  loadSurfacePaletteCatalog,
  renderSurfacePaletteWorkbench,
  saveSurfacePalettePreviewUsage,
  saveSurfacePaletteBinding,
  getSurfacePaletteSelection: () => selectedToken()
});
