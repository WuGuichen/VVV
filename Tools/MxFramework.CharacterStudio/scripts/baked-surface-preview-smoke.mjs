import assert from 'node:assert/strict';
import fs from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import * as THREE from '../node_modules/three/build/three.module.js';
import { FBXLoader } from '../node_modules/three/examples/jsm/loaders/FBXLoader.js';
import { normalizeBakedSurfacePreviewBinding, loadBakedSurfacePreview, applyBakedSurfacePreviewMaterials } from '../web/src/bakedSurfacePreview.js';

globalThis.document = {
  createElementNS() { return { style: {}, addEventListener() {}, removeEventListener() {}, setAttribute() {}, set src(value) { this._src = value; } }; },
  createElement() { return this.createElementNS(); }
};
const repo = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '../../..');
const packageRoot = path.join(repo, 'Authoring/CharacterPackages/character-000009');
const binding = JSON.parse(fs.readFileSync(path.join(packageRoot, 'config/character_baked_surface_binding.json'), 'utf8'));
const catalog = JSON.parse(fs.readFileSync(path.join(repo, 'ArtSource/Common/Palettes/WGameSurfacePalette/catalog.json'), 'utf8'));
assert.equal(normalizeBakedSurfacePreviewBinding(binding), binding);
assert.throws(() => normalizeBakedSurfacePreviewBinding({ ...binding, uvLayout: 'palette' }), /描述无效/);
assert.throws(() => normalizeBakedSurfacePreviewBinding({ ...binding, baseColorFile: '../outside.png' }), /路径无效/);
const surface = await loadBakedSurfacePreview(THREE, binding, async () => new THREE.Texture(), catalog);
assert.equal(surface.baseColor.colorSpace, THREE.SRGBColorSpace);
assert.equal(surface.surfaceData.colorSpace, THREE.NoColorSpace);
assert.equal(surface.baseColor.flipY, true, 'FBX BakeUV orientation');
assert.equal(surface.profileData.minFilter, THREE.NearestFilter);
assert.equal(surface.profileData.generateMipmaps, false, 'Profile ids must not blend between classes');
const bytes = fs.readFileSync(path.join(packageRoot, binding.modelFile));
const root = new FBXLoader().parse(bytes.buffer.slice(bytes.byteOffset, bytes.byteOffset + bytes.byteLength), '');
const original = [];
root.traverse(object => {
  if (object.isMesh) original.push({ object, uv: [...object.geometry.getAttribute('uv').array],
    flow: [...object.geometry.getAttribute('uv1').array], meta: [...object.geometry.getAttribute('uv2').array],
    skin: [...object.geometry.getAttribute('skinWeight').array] });
});
assert(original.length > 0);
assert(original.some(item => item.flow.some(value => value < 0)), 'Fixture contains signed flow that is invalid as palette coordinates');
applyBakedSurfacePreviewMaterials(THREE, root, surface, 'void wgSurfaceProfileParams() {}');
for (const { object, uv, flow, meta, skin } of original) {
  assert.deepEqual([...object.geometry.getAttribute('uv').array], uv, 'Baked UV0 must not be replaced by hair flow');
  assert.deepEqual([...object.geometry.getAttribute('wgBakedHairFlow').array], flow);
  assert.deepEqual([...object.geometry.getAttribute('wgBakedHairMeta').array], meta);
  assert.deepEqual([...object.geometry.getAttribute('skinWeight').array], skin);
  for (const material of Array.isArray(object.material) ? object.material : [object.material]) {
    assert.equal(material.map, surface.baseColor);
    assert.equal(material.vertexColors, false);
    assert.equal(material.userData.wgameBakedSurface, true);
    assert.equal(material.userData.wgameSurfacePalette, undefined);
    const shader = { uniforms: {}, vertexShader: THREE.ShaderLib.physical.vertexShader, fragmentShader: THREE.ShaderLib.physical.fragmentShader };
    material.onBeforeCompile(shader);
    assert.equal(shader.uniforms.wgSurfaceData.value, surface.surfaceData);
    assert.equal(shader.uniforms.wgProfileData.value, surface.profileData);
    assert(shader.fragmentShader.includes('material.anisotropy *= wgHair'));
  }
}
const invalid = new THREE.Mesh(new THREE.BufferGeometry(), new THREE.MeshStandardMaterial());
invalid.geometry.setAttribute('position', new THREE.Float32BufferAttribute([0, 0, 0], 3));
assert.throws(() => applyBakedSurfacePreviewMaterials(THREE, invalid, surface, ''), /缺少有效/);
assert.equal(invalid.material.userData.wgameBakedSurface, undefined);
let disposed = false;
const goodTexture = new THREE.Texture();goodTexture.addEventListener('dispose', () => disposed = true);
await assert.rejects(() => loadBakedSurfacePreview(THREE, binding,
  async name => name === binding.baseColorFile ? goodTexture : null, catalog), /不会回退到色板/);
assert.equal(disposed, true);
surface.dispose();
console.log('Baked preview passed: real 000009 FBX, UV0/flow/skinning preservation, material routing, profile sampling and failure isolation.');
