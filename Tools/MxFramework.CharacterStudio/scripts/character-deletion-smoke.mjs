import assert from 'node:assert/strict';
import fs from 'node:fs';
import vm from 'node:vm';

const nodes = new Map();
const element = id => {
  if (!nodes.has(id)) nodes.set(id, { value: '', checked: false, disabled: false, textContent: '', innerHTML: '',
    addEventListener() {}, focus() {}, showModal() { this.open = true; }, close() { this.open = false; } });
  return nodes.get(id);
};
let requests = [], result = { status: 'ready', characterIds: ['000002'], paths: ['ArtSource/Characters/000002'],
  fileCount: 2, bytes: 100, blockingReferences: [], planHash: 'a'.repeat(64),
  sharedEdits: [{kind:'characterResourceRegistration',path:'Assets/mapping.json',recordCount:4},
    {kind:'emptyTacticalAcquisition',path:'Assets/acquisition.json',recordCount:1},
    {kind:'characterRuntimeRegistration',path:'Assets/runtime.json',recordCount:2}] };
const state = { apiAvailable: true, packages: [{ packageId: '000002', relative: 'test' }], packageRelative: 'test' };
const context = vm.createContext({ state, el: {}, console, setInterval, clearInterval,
  document: { getElementById: element }, escapeHtml: s => String(s).replaceAll('<', '&lt;'),
  readJsonResponseOrText: async r => r.result, loadAll: async () => {}, renderShellStatus() {},
  fetch: async (url, options) => { requests.push([url, JSON.parse(options.body)]); return { ok: true, result }; }
});
vm.runInContext(fs.readFileSync(new URL('../web/src/characterManager.js', import.meta.url), 'utf8'), context);
context.openCharacterDeletion();
assert(element('characterDeleteApply').disabled);
await context.inspectCharacterDeletion();
assert.match(element('characterDeletePlan').innerHTML, /ArtSource\/Characters\/000002/);
assert.match(element('characterDeletePlan').innerHTML, /角色资源登记：4 条/);
assert.match(element('characterDeletePlan').innerHTML, /空战术获取记录：1 条/);
assert.match(element('characterDeletePlan').innerHTML, /角色运行资源登记：2 条/);
element('characterDeleteConfirm').value = '000001'; element('characterDeleteEditorsClosed').checked = true;
await context.applyCharacterDeletion(); assert.equal(requests.length, 1, 'Wrong confirmation must not send deletion');
element('characterDeleteConfirm').value = '000002'; element('characterDeleteEditorsClosed').checked = false;
await context.applyCharacterDeletion(); assert.equal(requests.length, 1, 'Editing acknowledgement required');
element('characterDeleteEditorsClosed').checked = true; result = { status: 'deleted' };
await context.applyCharacterDeletion(); assert.equal(requests[1][0], '/api/character/delete');
assert.equal(requests[1][1].planHash, 'a'.repeat(64)); assert.match(state.message, /已彻底删除/);
state.packageRelative = 'test'; context.openCharacterDeletion();
result = { status: 'blocked', characterIds: ['000002'], paths: [], blockingReferences: [{ path: 'Assets/consumer.prefab' }] };
await context.inspectCharacterDeletion();
element('characterDeleteConfirm').value = '000002'; element('characterDeleteEditorsClosed').checked = true;
await context.applyCharacterDeletion(); assert.equal(requests.length, 3);
assert.match(element('characterDeletePlan').innerHTML, /Assets\/consumer.prefab/);
// A response arriving after dialog dismissal must not revive its plan.
context.fetch = () => new Promise(resolve => { context.resolveFetch = resolve; });
const pending = context.inspectCharacterDeletion();
context.openCharacterDeletion();
context.resolveFetch({ ok: true, result: { status: 'ready', characterIds: ['000002'] } }); await pending;
assert(element('characterDeleteApply').disabled);
console.log('Character deletion UI confirmation, blocked references, exact plan and stale response checks passed.');
