import fs from "node:fs";
import path from "node:path";
import { fileURLToPath, pathToFileURL } from "node:url";

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
globalThis.state = { package: {} };

const repoRoot = path.resolve(path.dirname(fileURLToPath(import.meta.url)), "../../..");
const THREE = await import(pathToFileURL(path.join(
  repoRoot,
  "Tools/MxFramework.CharacterStudio/node_modules/three/build/three.module.js")).href);
const loaderPath = path.join(
  repoRoot,
  "Tools/MxFramework.CharacterStudio/node_modules/three/examples/jsm/loaders/FBXLoader.js");
const loaderSource = fs.readFileSync(loaderPath, "utf8");
require(loaderSource.includes("WGame CharacterStudio: preserve FBX vertex color alpha"),
  "CharacterStudio postinstall patch must preserve FBX PaletteData alpha.");
const { FBXLoader } = await import(pathToFileURL(loaderPath).href);
const viewport = await import(pathToFileURL(path.join(
  repoRoot,
  "Tools/MxFramework.CharacterStudio/web/src/viewport3d.js")).href);
const catalog = JSON.parse(fs.readFileSync(
  path.join(repoRoot, "ArtSource/Common/Palettes/WGameSurfacePalette/catalog.json"), "utf8"));

require(catalog.format === "mx.wgameSurfacePaletteCatalog.v3", "RGB565 Catalog v3 is required.");
require(Buffer.from(catalog.colorData.rgb8Base64, "base64").length === 65536 * 3,
  "Compact RGB8 table should cover the entire Color Atlas.");
require(catalog.colorData.uniqueRgb8Count === 65536 && catalog.colorData.aliasCount === 0,
  "RGB565 Catalog should expose 65,536 unique RGB8 values and zero aliases.");

const cases = [
  {
    packageRoot: "Authoring/CharacterPackages/character-000001",
    model: "resources/models/000001_lod0.fbx"
  },
  {
    packageRoot: "Authoring/CharacterPackages/character-wgame-palette-mage-v1",
    model: "resources/models/wgamepalettemagev1.fbx"
  }
];

for (const item of cases) {
  const packageRoot = path.join(repoRoot, item.packageRoot);
  const binding = JSON.parse(fs.readFileSync(
    path.join(packageRoot, "config/character_palette_binding.json"),
    "utf8"));
  const surfacePalette = viewport.normalizeSurfacePaletteBinding(binding);
  require(surfacePalette, `${item.packageRoot} Color Palette binding v5 should normalize.`);
  require(surfacePalette.atlasWidth === 256 && surfacePalette.atlasHeight === 256,
    `${item.packageRoot} should use the complete 256x256 color address space.`);
  require(surfacePalette.surfaceProfileSource === "paletteDataAlpha16",
    `${item.packageRoot} should use PaletteData.A as 16-profile authority.`);

  const modelPath = path.join(packageRoot, item.model);
  const bytes = fs.readFileSync(modelPath);
  const buffer = bytes.buffer.slice(bytes.byteOffset, bytes.byteOffset + bytes.byteLength);
  const root = new FBXLoader().parse(buffer, path.dirname(modelPath) + path.sep);
  const texture = new THREE.Texture();
  const originalPaletteUvs = new Map();
  root.traverse(object => {
    if (!object.isMesh) return;
    originalPaletteUvs.set(object, object.geometry.getAttribute("uv1"));
    const data = object.geometry.getAttribute("color");
    require(data?.itemSize === 4, `${item.packageRoot} FBXLoader must expose PaletteData RGBA.`);
  });
  const previewPalette = {
    ...surfacePalette,
    catalogHash: catalog.catalogHash,
    catalog,
    texture,
    faceDetailTexture: binding.faceDetailEnabled ? new THREE.Texture() : null
  };
  const result = viewport.applySurfacePalettePreviewMaterials(THREE, root, previewPalette);
  require(result.appliedCount > 0, `${item.packageRoot} should receive a Color/Profile material.`);
  require(result.missingPaletteUvCount === 0, `${item.packageRoot} should expose FBX uv1 / PaletteUV.`);
  require(result.missingPaletteDataCount === 0, `${item.packageRoot} should expose PaletteData RGBA.`);
  require(result.missingFaceDetailUvCount === 0, `${item.packageRoot} should expose FBX uv2 / FaceDetailUV when enabled.`);
  const usage = viewport.buildSurfacePaletteUsageFromPreview(root, previewPalette);
  require(usage.format === "mx.characterPaletteUsage.v2" && usage.schemaVersion === "2.0",
    `${item.packageRoot} should produce usage v2.`);
  require(usage.invalidFaceCount > 0 && usage.paletteRepaintRequired,
    `${item.packageRoot} migrated -1 faces should remain explicitly repaint-required.`);

  let meshCount = 0;
  root.traverse(object => {
    if (!object.isMesh) return;
    meshCount++;
    const uv = object.geometry.getAttribute("uv");
    const paletteData = object.geometry.getAttribute("wgPaletteData");
    const faceDetailUv = object.geometry.getAttribute("wgFaceDetailUv");
    const sourceUv = originalPaletteUvs.get(object);
    require(uv && sourceUv && uv.count === sourceUv.count, `${item.packageRoot} should receive full PaletteUV.xy.`);
    require(paletteData?.itemSize === 4, `${item.packageRoot} preview shader should receive AO/Rim/Emission/Profile.`);
    require(faceDetailUv?.itemSize === 2, `${item.packageRoot} preview shader should receive FaceDetailUV.`);
    for (let index = 0; index < uv.count; index++) {
      require(Math.abs(uv.getX(index) - sourceUv.getX(index)) < 0.000001,
        `${item.packageRoot} Palette U was rewritten.`);
      require(Math.abs(uv.getY(index) - sourceUv.getY(index)) < 0.000001,
        `${item.packageRoot} Palette V was rewritten.`);
    }

    const materials = Array.isArray(object.material) ? object.material : [object.material];
    for (const material of materials) {
      require(material?.isMeshStandardMaterial, `${item.packageRoot} should use MeshStandardMaterial.`);
      require(material.map === texture, `${item.packageRoot} should use the shared RGB Color Palette texture.`);
      require(!material.vertexColors, `${item.packageRoot} data vertex colors must not multiply albedo directly.`);
      const shader = {
        vertexShader: "void main() {\n}",
        fragmentShader: "void main() {\n#include <color_fragment>\n#include <map_fragment>\n#include <roughnessmap_fragment>\n#include <metalnessmap_fragment>\n#include <emissivemap_fragment>\n}"
      };
      material.onBeforeCompile(shader);
      require(shader.vertexShader.includes("attribute vec4 wgPaletteData"),
        `${item.packageRoot} should pass PaletteData RGBA to the fragment stage.`);
      require(shader.fragmentShader.includes("wgSurfaceProfile") && shader.fragmentShader.includes("* 16.0"),
        `${item.packageRoot} should decode 16 Surface Profiles from vertex alpha.`);
      require(shader.fragmentShader.includes("wgInvalidPaletteUv") && shader.fragmentShader.includes("vec3( 1.0, 0.0, 1.0 )"),
        `${item.packageRoot} should show out-of-range PaletteUV as diagnostic magenta.`);
      require(shader.fragmentShader.includes("roughnessFactor = wgProfileRoughness"),
        `${item.packageRoot} should apply generated profile roughness response.`);
      require(shader.fragmentShader.includes("metalnessFactor = wgProfileMetallic"),
        `${item.packageRoot} should apply generated profile metal response.`);
      require(shader.fragmentShader.includes("totalEmissiveRadiance += diffuseColor.rgb"),
        `${item.packageRoot} should preview emission and profile rim.`);
      if (binding.faceDetailEnabled) {
        require(shader.vertexShader.includes("attribute vec2 wgFaceDetailUv"), `${item.packageRoot} should pass FaceDetailUV.`);
        require(shader.fragmentShader.includes("texture2D( wgFaceDetailTex, vWgFaceDetailUv )"), `${item.packageRoot} should directly sample its character FaceDetail texture.`);
      }
    }
  });
  require(meshCount > 0, `${item.packageRoot} should contain a preview mesh.`);
}

const viewportSource = fs.readFileSync(
  path.join(repoRoot, "Tools/MxFramework.CharacterStudio/web/src/viewport3d.js"),
  "utf8");
const workbenchSource = fs.readFileSync(
  path.join(repoRoot, "Tools/MxFramework.CharacterStudio/web/src/surfacePaletteWorkbench.js"),
  "utf8");
require(!viewportSource.includes("profileRow"), "CharacterStudio preview must not contain Profile row remapping.");
require(!viewportSource.includes("materialClassSource"), "CharacterStudio preview must not decode material from Palette texture alpha.");
require(viewportSource.includes("new THREE.DataTexture(pixels, 256, 256"),
  "CharacterStudio preview should build its RGB Atlas directly from compact Catalog data.");
require(workbenchSource.includes("renderVirtualGrid"), "Formula Color grid must be virtualized.");
require(workbenchSource.includes("surfaceProfiles") && workbenchSource.includes("selectedProfile"),
  "Workbench should implement the two-step Color/Profile selector.");
require(workbenchSource.includes('SURFACE_PALETTE_FORMAT_V3 = "mx.wgameSurfacePaletteCatalog.v3"'),
  "Workbench should require RGB565 Catalog v3.");
require(workbenchSource.includes("colorIndexFromRgb8") && workbenchSource.includes('data-palette-picker="color"'),
  "Workbench should map the primary color picker to a unique RGB565 address.");
require(workbenchSource.includes("showTechnical: false") && workbenchSource.includes("技术地址浏览"),
  "Raw 65K address browsing should be a hidden technical view by default.");
require(workbenchSource.includes('data-palette-binding-field="cullMode"')
  && workbenchSource.includes("双面（不剔除）")
  && workbenchSource.includes("PALETTE_CULL_MODES.has(input.value)")
  && workbenchSource.includes("binding.cullMode = input.value")
  && workbenchSource.includes("globalThis.renderViewport?.()"),
"Workbench should expose the SDK-owned Palette cull mode and refresh the 3D preview after edits.");
require(!workbenchSource.includes("cMaxU16Base64") && !workbenchSource.includes("canonicalColorIndexU16Base64"),
  "Workbench must not retain v2 Cmax or alias tables.");
for (const route of ["/create", "/duplicate", "/update", "/retire"]) {
  require(!workbenchSource.includes(`/api/authoring/palette${route}`), `Read-only workbench must remove ${route} API.`);
}
for (const route of ["/references", "/usage"]) {
  require(workbenchSource.includes(`/api/authoring/palette${route}`), `Workbench missing ${route} API.`);
}
require(viewportSource.includes("buildSurfacePaletteUsageFromPreview"),
  "CharacterStudio preview should generate usage v2 from uv1.xy and PaletteData.A.");

console.log("CharacterStudio RGB565 Color Palette v3 / Surface Profile preview smoke passed.");

function require(condition, message) {
  if (!condition) throw new Error(message);
}
