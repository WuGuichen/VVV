// Browser material adapter for mx.characterBakedSurfaceBinding.v1.
// Three.js lighting is a preview approximation; texture/mesh channel ownership is exact.
export function normalizeBakedSurfacePreviewBinding(binding) {
  if (!binding || binding.format !== 'mx.characterBakedSurfaceBinding.v1'
      || binding.schemaVersion !== '1.0' || binding.enabled !== true
      || binding.uvLayout !== 'bakeUv0HairFlowUv1HairMetaUv2'
      || binding.surfaceDataLayout !== 'aoRimEmissionProfile16'
      || ![512, 1024, 2048, 4096].includes(binding.textureSize)
      || !['Off', 'Front', 'Back'].includes(binding.cullMode)) {
    throw new Error('烘焙材质描述无效，请在 Blender 修复并重新发布。');
  }
  for (const key of ['modelFile', 'baseColorFile', 'surfaceDataFile']) {
    const path = String(binding[key] || '').replaceAll('\\', '/');
    if (!path || /^(?:[a-z]+:|\/)/i.test(path) || path.split('/').some(part => part === '..' || part === '.'))
      throw new Error(`烘焙资源路径无效：${key}`);
  }
  if (!/\.fbx$/i.test(binding.modelFile)) throw new Error('烘焙角色预览需要描述中绑定的 FBX 模型。');
  for (const key of ['primaryWidth', 'primaryStrength', 'secondaryWidth', 'secondaryStrength',
    'primaryShift', 'secondaryShift', 'rootDarkening', 'rimStrength', 'aoStrength']) {
    if (!Number.isFinite(binding.hair?.[key])) throw new Error(`烘焙头发参数缺失：${key}`);
  }
  return binding;
}

export async function loadBakedSurfacePreview(THREE, binding, loadTexture, catalog) {
  normalizeBakedSurfacePreviewBinding(binding);
  if (!Array.isArray(catalog?.surfaceProfiles) || catalog.surfaceProfiles.length !== 16)
    throw new Error('烘焙材质预览缺少 16 类表面光照定义。');
  const textures = await Promise.all([
    loadTexture(binding.baseColorFile, binding.baseColorHash, true),
    loadTexture(binding.surfaceDataFile, binding.surfaceDataHash, false)
  ]);
  if (textures.some(texture => !texture)) {
    for (const texture of textures) texture?.dispose();
    throw new Error('烘焙底色或材质控制图加载失败；不会回退到色板着色。');
  }
  for (const texture of textures) {
    // The baked descriptor selects its authoritative FBX; its UV0 is unmodified BakeUV.
    texture.flipY = true;
    texture.channel = 0;
    texture.wrapS = texture.wrapT = THREE.ClampToEdgeWrapping;
    texture.magFilter = THREE.LinearFilter;
    texture.minFilter = THREE.LinearMipmapLinearFilter;
    texture.generateMipmaps = true;
    texture.needsUpdate = true;
  }
  const [baseColor, surfaceData] = textures;
  baseColor.colorSpace = THREE.SRGBColorSpace;
  surfaceData.colorSpace = THREE.NoColorSpace;
  // Classification must never interpolate into another profile. Continuous RGB stays filtered.
  const profileData = surfaceData.clone();
  profileData.magFilter = profileData.minFilter = THREE.NearestFilter;
  profileData.generateMipmaps = false;
  profileData.needsUpdate = true;
  return { binding, baseColor, surfaceData, profileData, catalog,
    dispose() { baseColor.dispose(); surfaceData.dispose(); profileData.dispose(); } };
}

function createBakedMaterial(THREE, source, surface, profileFunctions) {
  const { binding, baseColor, surfaceData, profileData } = surface;
  const material = new THREE.MeshPhysicalMaterial({
    map: baseColor, color: 0xffffff, roughness: 1, metalness: 1,
    anisotropy: Math.max(0.001, Math.min(1, binding.hair.primaryStrength)),
    side: binding.cullMode === 'Off' ? THREE.DoubleSide : binding.cullMode === 'Front' ? THREE.BackSide : THREE.FrontSide,
    transparent: false, opacity: 1, vertexColors: false
  });
  material.name = `${source?.name || 'Character'}_BakedSurface`;
  // Texture ownership is visible to the existing viewport disposal traversal.
  material.wgameSurfaceData = surfaceData;
  material.wgameProfileData = profileData;
  material.onBeforeCompile = shader => {
    Object.assign(shader.uniforms, {
      wgSurfaceData: { value: surfaceData }, wgProfileData: { value: profileData },
      wgHairRootDarkening: { value: binding.hair.rootDarkening }
    });
    shader.vertexShader = shader.vertexShader.replace('void main() {', `
attribute vec2 wgBakedHairFlow;
attribute vec2 wgBakedHairMeta;
varying vec4 vWgBakedHair;
void main() {
  vWgBakedHair = vec4(wgBakedHairFlow, wgBakedHairMeta);
`);
    shader.fragmentShader = shader.fragmentShader.replace('void main() {', `
uniform sampler2D wgSurfaceData;
uniform sampler2D wgProfileData;
uniform float wgHairRootDarkening;
varying vec4 vWgBakedHair;
${profileFunctions}
void main() {
  vec4 wgData = texture2D(wgSurfaceData, vMapUv);
  float wgProfile = min(15.0, floor(texture2D(wgProfileData, vMapUv).a * 16.0));
  float wgRoughness, wgMetallic, wgSpecular, wgRim;
  wgSurfaceProfileParams(wgProfile, wgRoughness, wgMetallic, wgSpecular, wgRim);
  float wgHair = (wgProfile >= 2.0 && wgProfile < 4.0 && vWgBakedHair.w > 0.5) ? 1.0 : 0.0;
`)
      .replace('#include <color_fragment>', `
  diffuseColor.rgb *= mix(0.72, 1.0, clamp(wgData.r, 0.0, 1.0));
  diffuseColor.rgb *= mix(1.0, 1.0 - wgHairRootDarkening * (1.0 - smoothstep(0.0, 0.3, vWgBakedHair.z)), wgHair);
`)
      .replace('#include <roughnessmap_fragment>', `
#include <roughnessmap_fragment>
  roughnessFactor = wgRoughness;
`)
      .replace('#include <metalnessmap_fragment>', `
#include <metalnessmap_fragment>
  metalnessFactor = wgMetallic;
`)
      .replace('#include <emissivemap_fragment>', `
#include <emissivemap_fragment>
  totalEmissiveRadiance += diffuseColor.rgb * clamp(wgData.b, 0.0, 1.0) * 0.65;
  float wgFresnel = pow(1.0 - clamp(dot(normalize(normal), normalize(-vViewPosition)), 0.0, 1.0), 2.5);
  totalEmissiveRadiance += diffuseColor.rgb * wgFresnel * wgRim * clamp(wgData.g, 0.0, 1.0) * 0.4;
`)
      .replace('#include <lights_physical_fragment>', `
#include <lights_physical_fragment>
#ifdef USE_ANISOTROPY
  vec2 wgFlow = dot(vWgBakedHair.xy, vWgBakedHair.xy) > 0.000001 ? normalize(vWgBakedHair.xy) : vec2(1.0, 0.0);
  material.anisotropy *= wgHair;
  material.alphaT = mix(pow2(material.roughness), 1.0, pow2(material.anisotropy));
  material.anisotropyT = tbn[0] * wgFlow.x + tbn[1] * wgFlow.y;
  material.anisotropyB = tbn[1] * wgFlow.x - tbn[0] * wgFlow.y;
#endif
`);
  };
  material.customProgramCacheKey = () => `WGameBakedSurfacePreview.v1.${surface.catalog.catalogHash}`;
  material.userData = { wgameBakedSurface: true, baseColorFile: binding.baseColorFile,
    surfaceDataFile: binding.surfaceDataFile, sourceMaterialName: source?.name || '' };
  return material;
}

export function applyBakedSurfacePreviewMaterials(THREE, root, surface, profileFunctions) {
  const meshes = [];
  root.traverse(object => { if (object.isMesh && object.geometry && object.material) meshes.push(object); });
  if (!meshes.length) throw new Error('烘焙角色模型没有可绘制网格。');
  for (const object of meshes) {
    const geometry = object.geometry;
    const count = geometry.getAttribute('position')?.count;
    for (const name of ['uv', 'uv1', 'uv2']) {
      const attribute = geometry.getAttribute(name);
      if (!count || attribute?.itemSize !== 2 || attribute.count !== count
          || !attribute.array.every(Number.isFinite)) throw new Error(`烘焙网格 ${object.name} 缺少有效的 ${name} 数据。`);
      if (name === 'uv' && !attribute.array.every(value => value >= -0.00001 && value <= 1.00001))
        throw new Error(`烘焙网格 ${object.name} 的 BakeUV 超出贴图范围。`);
    }
  }
  const replacedMaterials = new Set(), replacedGeometries = new Set();
  const audit = [];
  for (const object of meshes) {
    const geometry = object.geometry.clone();
    geometry.setAttribute('wgBakedHairFlow', geometry.getAttribute('uv1'));
    geometry.setAttribute('wgBakedHairMeta', geometry.getAttribute('uv2'));
    replacedGeometries.add(object.geometry);
    object.geometry = geometry;
    const sources = Array.isArray(object.material) ? object.material : [object.material];
    const materials = sources.map(source => {
      if (source) replacedMaterials.add(source);
      return createBakedMaterial(THREE, source, surface, profileFunctions);
    });
    object.material = Array.isArray(object.material) ? materials : materials[0];
    audit.push({ rendererNode: object.name, bakedSurface: true, uvAttribute: 'uv',
      baseColorFile: surface.binding.baseColorFile, surfaceDataFile: surface.binding.surfaceDataFile });
  }
  for (const material of replacedMaterials) material.dispose();
  for (const geometry of replacedGeometries) geometry.dispose();
  root.userData.packagePreviewMaterials = { bakedSurface: true, appliedCount: audit.length, fallbackCount: 0 };
  root.userData.packagePreviewMaterialAudit = audit;
  return root.userData.packagePreviewMaterials;
}
