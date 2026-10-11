import assert from 'node:assert/strict';
import fs from 'node:fs';
import vm from 'node:vm';

const host = { innerHTML: '', querySelectorAll: () => [], querySelector: () => null };
let reads = 0, writes = 0;
const context = vm.createContext({
  console, Uint8Array, Set, Map, Number, String, Math,
  atob: value => Buffer.from(value, 'base64').toString('binary'),
  state: { package: {} },
  document: { getElementById: () => host },
  escapeHtml: value => String(value).replaceAll('<', '&lt;'),
  readJson: async () => { reads++; throw new Error('Atlas unavailable'); },
  postJsonDetailed: async () => { writes++; return { success: true }; },
  renderShellStatus: () => {},
});
vm.runInContext(fs.readFileSync(new URL('../web/src/surfacePaletteWorkbench.js', import.meta.url), 'utf8'), context);

context.state.package = { bakedSurfaceBinding: { baseColorFile: 'BaseColor.png', surfaceDataFile: 'SurfaceData.png', authoringSourcePath: 'source.blend', textureSize: 512 }, paletteBinding: { enabled: true } };
await context.loadSurfacePaletteCatalog();
assert.equal(reads, 0);
assert.match(host.innerHTML, /BaseColor.png/);
assert.match(host.innerHTML, /SurfaceData.png/);
assert.doesNotMatch(host.innerHTML, /历史色板兼容|无 usage/);
await context.saveSurfacePaletteBinding();
await context.saveSurfacePalettePreviewUsage();
assert.equal(writes, 0, 'Baked characters must not save stale palette bindings');

context.state.package = { bakedSurfaceIssues: [{ message: 'Broken baked descriptor' }], paletteBinding: { enabled: true } };
await context.loadSurfacePaletteCatalog();
assert.match(host.innerHTML, /Broken baked descriptor/);
assert.equal(reads, 0);

context.state.package = {};
await context.loadSurfacePaletteCatalog();
assert.match(host.innerHTML, /新角色默认/);
assert.doesNotMatch(host.innerHTML, /BaseColor.png/);
assert.equal(reads, 0);

// Return to an old character: a missing atlas affects only its legacy editor.
context.state.package = { paletteBinding: { enabled: true } };
await context.loadSurfacePaletteCatalog();
assert.equal(reads, 1);
assert.match(host.innerHTML, /Atlas unavailable/);
assert.doesNotMatch(host.innerHTML, /source.blend/);

// A cached catalog permits the legacy UI after switching back from baked/new.
vm.runInContext(`workspace.catalog = { catalogHash: 'test', surfaceProfiles: [{ index: 0, profileId: 'skin_soft', displayName: 'Skin', alphaByte: 8 }] }; workspace.rgb8 = new Uint8Array(65536 * 3);`, context);
context.renderSurfacePaletteWorkbench();
assert.match(host.innerHTML, /历史色板兼容/);
assert.doesNotMatch(host.innerHTML, /烘焙资源需要修复/);
console.log('Character surface workbench: baked, broken, new, legacy and switch/save isolation passed.');
