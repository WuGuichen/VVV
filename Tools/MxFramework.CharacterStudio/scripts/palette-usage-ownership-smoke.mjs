// character_palette_usage.json 归属校验 smoke。
//
// 覆盖两条历史缺陷：
//   1) 全局单槽 state.surfacePalettePreviewUsage 被武器 / 挂点预览覆盖，把武器模型哈希写进角色 usage；
//   2) usage 的来源路径回退到预览路径（可能是 previewMesh 的 .glb），哈希与本体模型不是一回事。
//
// 断言的是 viewport3d.js 暴露的归属契约：来源必须取自资源目录声明、发布必须带代次、
// 保存前复核必须同时校验「包 / 本体资源 / 本体路径 / 模型哈希 / 代次」。

import path from "node:path";
import { fileURLToPath, pathToFileURL } from "node:url";

function require(condition, message) {
  if (!condition) throw new Error(message);
}

globalThis.document = {
  createElementNS() {
    return {
      style: {},
      addEventListener() {},
      removeEventListener() {},
      setAttribute() {},
      set src(value) { this._src = value; },
      get src() { return this._src || ""; }
    };
  },
  createElement() { return this.createElementNS(); }
};

globalThis.state = {
  packageRelative: "",
  package: null,
  surfacePalettePreviewUsage: null,
  message: ""
};

const repoRoot = path.resolve(path.dirname(fileURLToPath(import.meta.url)), "../../..");
const viewport = await import(pathToFileURL(path.join(
  repoRoot,
  "Tools/MxFramework.CharacterStudio/web/src/viewport3d.js")).href);

const BODY_RESOURCE_KEY = "char.000009.model.body";
const BODY_RELATIVE_PATH = "resources/models/000009_lod0.fbx";
const BODY_HASH = "sha256:5848a76befb1badd71a74878ffbe120f9bfea939773f57560ab9859366609648";
const WEAPON_HASH = "sha256:5e6a0068b0a1b0e7b1b0e4b0a1b0e7b1b0e4b0a1b0e7b1b0e4b0a1b0e7b1b0e4";

for (const name of [
  "invalidateSurfacePalettePreviewUsage",
  "getSurfacePaletteUsageGeneration",
  "validateSurfacePaletteUsageOwner",
  "buildSurfacePaletteUsageSource",
  "publishSurfacePalettePreviewUsage"
]) {
  require(typeof viewport[name] === "function", `viewport3d must export ${name}.`);
}

// ---------------------------------------------------------------------------
// 1. usage 来源必须取自资源目录声明，禁止回退到预览路径
// ---------------------------------------------------------------------------

const urlOf = relative => `/pkg/${relative}`;
const bodyResource = { resourceKey: BODY_RESOURCE_KEY, relativePath: BODY_RELATIVE_PATH };

const source = viewport.buildSurfacePaletteUsageSource(bodyResource, urlOf);
require(source, "A declared body model must produce a usage source.");
require(source.resourceKey === BODY_RESOURCE_KEY, "usage source must carry the body resource key.");
require(source.relativePath === BODY_RELATIVE_PATH, "usage source must use the declared relativePath.");
require(source.modelUrl === `/pkg/${BODY_RELATIVE_PATH}`,
  "usage source modelUrl must be built from the declared relativePath, not from a preview fallback.");

require(viewport.buildSurfacePaletteUsageSource({ resourceKey: BODY_RESOURCE_KEY, relativePath: "" }, urlOf) === null,
  "A body model without relativePath must not produce a usage source (preview-path fallback is forbidden).");
require(viewport.buildSurfacePaletteUsageSource({ resourceKey: "", relativePath: BODY_RELATIVE_PATH }, urlOf) === null,
  "A body model without resourceKey must not produce a usage source.");
require(viewport.buildSurfacePaletteUsageSource(null, urlOf) === null,
  "A missing body resource must not produce a usage source.");

// ---------------------------------------------------------------------------
// 2. 切包 / 重载必须作废上一次扫描结果
// ---------------------------------------------------------------------------

const generationBefore = viewport.getSurfacePaletteUsageGeneration();
require(Number.isInteger(generationBefore), "The usage generation must be an integer.");
viewport.invalidateSurfacePalettePreviewUsage();
const generationAfter = viewport.getSurfacePaletteUsageGeneration();
require(generationAfter === generationBefore + 1, "Invalidation must advance the usage generation.");
require(globalThis.state.surfacePalettePreviewUsage === null, "Invalidation must clear the pending usage slot.");

// ---------------------------------------------------------------------------
// 3. 发布必须校验代次：过期代次的扫描结果不得落位
// ---------------------------------------------------------------------------

function makeUsage(modelHash) {
  return {
    format: "mx.characterPaletteUsage.v2",
    schemaVersion: "2.0",
    packageId: "000009",
    catalogHash: "sha256:catalog",
    modelHash,
    tokenUsages: [{ logicalToken: 0, colorIndex: 0, surfaceProfileId: "skin_soft", faceCount: 12 }]
  };
}

function makeOwner(generation, modelHash) {
  return {
    generation,
    packageRelative: "Authoring/CharacterPackages/character-000009",
    packageId: "000009",
    bodyResourceKey: BODY_RESOURCE_KEY,
    bodyRelativePath: BODY_RELATIVE_PATH,
    modelHash
  };
}

require(viewport.publishSurfacePalettePreviewUsage({ usage: makeUsage(BODY_HASH), owner: makeOwner(generationBefore, BODY_HASH) }) === false,
  "A stale generation must not be published.");
require(globalThis.state.surfacePalettePreviewUsage === null, "A rejected publish must leave the slot untouched.");

require(viewport.publishSurfacePalettePreviewUsage(null) === false, "An empty pending usage must not be published.");

// ---------------------------------------------------------------------------
// 4. 保存前复核
// ---------------------------------------------------------------------------

globalThis.state.packageRelative = "Authoring/CharacterPackages/character-000009";
globalThis.state.package = {
  manifest: { packageId: "000009" },
  resourceCatalog: { entries: [{ resourceKey: BODY_RESOURCE_KEY, relativePath: BODY_RELATIVE_PATH }] }
};

const currentGeneration = viewport.getSurfacePaletteUsageGeneration();
const currentUsage = makeUsage(BODY_HASH);
require(viewport.publishSurfacePalettePreviewUsage({
  usage: currentUsage,
  owner: makeOwner(currentGeneration, BODY_HASH)
}) === true, "A current generation must be published.");
require(globalThis.state.surfacePalettePreviewUsage === currentUsage, "The published usage must land in the shared slot.");

require(viewport.validateSurfacePaletteUsageOwner(currentUsage).ok === true,
  "A usage that matches the current package, body resource, path, hash and generation must validate.");

// 4a. 外来模型哈希（武器哈希被写进角色 usage 的回归场景）
const foreign = makeUsage(WEAPON_HASH);
require(viewport.validateSurfacePaletteUsageOwner(foreign).ok === false,
  "A usage whose modelHash differs from the scanned model must be rejected.");

// 4b. 切包后旧扫描结果作废
viewport.invalidateSurfacePalettePreviewUsage();
require(viewport.validateSurfacePaletteUsageOwner(currentUsage).ok === false,
  "A usage invalidated by a reload must be rejected.");

// 重新发布，继续验证其余条件
const gen2 = viewport.getSurfacePaletteUsageGeneration();
require(viewport.publishSurfacePalettePreviewUsage({ usage: currentUsage, owner: makeOwner(gen2, BODY_HASH) }) === true,
  "Re-publishing after a reload must succeed.");
require(viewport.validateSurfacePaletteUsageOwner(currentUsage).ok === true, "Re-published usage must validate again.");

// 4c. 本体资源被移除
const savedEntries = globalThis.state.package.resourceCatalog.entries;
globalThis.state.package.resourceCatalog.entries = [];
require(viewport.validateSurfacePaletteUsageOwner(currentUsage).ok === false,
  "A usage whose body resource is no longer declared must be rejected.");

// 4d. 本体路径被替换
globalThis.state.package.resourceCatalog.entries = [{ resourceKey: BODY_RESOURCE_KEY, relativePath: "resources/models/other.fbx" }];
require(viewport.validateSurfacePaletteUsageOwner(currentUsage).ok === false,
  "A usage whose body model path changed must be rejected.");
globalThis.state.package.resourceCatalog.entries = savedEntries;

// 4e. 包切换
globalThis.state.packageRelative = "Authoring/CharacterPackages/character-000007";
require(viewport.validateSurfacePaletteUsageOwner(currentUsage).ok === false,
  "A usage produced for another package must be rejected.");
globalThis.state.packageRelative = "Authoring/CharacterPackages/character-000009";

require(viewport.validateSurfacePaletteUsageOwner(currentUsage).ok === true, "The restored context must validate again.");

// 4f. 未扫描时（无归属信息）
viewport.invalidateSurfacePalettePreviewUsage();
require(viewport.validateSurfacePaletteUsageOwner(currentUsage).ok === false,
  "A usage without ownership information must be rejected.");
const noOwner = viewport.validateSurfacePaletteUsageOwner(null);
require(noOwner.ok === false && typeof noOwner.reason === "string" && noOwner.reason.length > 0,
  "A rejection must carry a human-readable reason.");

console.log("CharacterStudio palette usage ownership smoke passed.");
