(() => {
  'use strict';
  const key = 'mx.combat-design.levels.pending.v1';
  const open = document.createElement('button'); open.textContent = '关卡与角色行为';
  open.style.cssText = 'position:fixed;right:24px;bottom:24px;z-index:1100;padding:12px 18px'; document.body.append(open);
  const dialog = document.createElement('dialog');
  dialog.style.cssText = 'width:min(900px,90vw);max-height:90vh;border-radius:12px;padding:24px;color:inherit;background:var(--surface,#171d28)';
  dialog.innerHTML = `<h2>关卡与角色行为</h2><p>选择关卡实例的正式行为组版本，并为动作槽配置动作。预检会锁定实际依赖，发布后才能在关卡窗口装载。</p>
    <label>关卡 <select data-level></select></label> <button data-refresh>刷新</button> <button data-resume>恢复待完成操作</button>
    <p data-scene></p><label>角色实例 <select data-actor></select></label><div data-edit></div>
    <p><button data-save>保存</button> <button data-preflight>预检</button> <button data-publish disabled>发布此关</button> <button data-close>关闭</button></p>
    <p role="status" data-status></p>`;
  document.body.append(dialog);
  const $ = s => dialog.querySelector(s); let library, draft, sourceHash, previewToken, dirty = false, busy = false;
  const status = message => { $('[data-status]').textContent = message; };
  const changed = () => { dirty = true; previewToken = null; $('[data-publish]').disabled = true; status('未保存；需要重新预检。'); };
  const option = (label, value) => { const item = document.createElement('option'); item.textContent = label; item.value = value; return item; };
  const field = (label, input) => { const row = document.createElement('p'); const title = document.createElement('label'); title.textContent = label + ' '; title.append(input); row.append(title); return row; };
  async function api(path, payload) {
    const response = await fetch('/api/combat-design/authoring' + path, payload ? { method:'POST', headers:{'Content-Type':'application/json'}, body:JSON.stringify(payload) } : {});
    const value = await response.json(); if (!response.ok) throw new Error(value.message || JSON.stringify(value)); return value;
  }
  async function command(operation, request, resume = false) {
    if (busy) throw new Error('当前操作尚未结束。'); busy = true;
    try {
      const pending = localStorage.getItem(key);
      if (pending && !resume) throw new Error('已有待完成操作，请先恢复它。');
      const payload = resume ? JSON.parse(pending) : { operation, request, idempotencyKey:crypto.randomUUID() };
      if (!payload) throw new Error('没有待完成操作。');
      localStorage.setItem(key, JSON.stringify(payload));
      let result = await api('/submit', payload);
      for (let i=0; i<90 && (result.status === 'PendingEditor' || !result.status); i++) {
        status('等待 Unity 完成 ' + payload.operation + '；可以关闭窗口后恢复。');
        await new Promise(resolve => setTimeout(resolve, 1000)); result = await api('/status?operationId=' + encodeURIComponent(result.operationId));
      }
      if (result.status === 'PendingEditor' || !result.status) throw new Error('操作仍在等待；稍后恢复同一操作。');
      localStorage.removeItem(key);
      if (result.status !== 'Succeeded') throw new Error(result.message || result.status);
      return result;
    } finally { busy = false; }
  }
  async function reload() {
    library = await command('levels.refresh', {}); const list = $('[data-level]'); list.replaceChildren();
    library.levels.forEach(l => list.append(option(l.DisplayName + ' / ' + l.LevelId, l.LevelId)));
    list.value = library.defaultLevelId; await load();
  }
  function apply(result) {
    draft = result.document; sourceHash = result.sourceHash; previewToken = result.previewToken || null;
    dirty = false; $('[data-publish]').disabled = !previewToken;
    $('[data-scene]').textContent = '目标 Scene: ' + draft.scenePath + '。预检和发布前，请在 Unity 打开此目标 Scene 并保持编辑就绪；保存不需要切换 Scene。预检使用该 Scene 的 GameManager 配置，未配置 profile 时使用 Runtime 默认值。发布后可在关卡窗口装载。';
    const actors = draft.modules.find(m => m.moduleId === 'characterTest.characters').payload.characters;
    const list = $('[data-actor]'); const previous = list.value; list.replaceChildren();
    actors.forEach(a => list.append(option(a.displayName + ' / ' + a.stableId, a.stableId))); if (actors.some(a => a.stableId === previous)) list.value = previous;
    draw();
  }
  async function load() { apply(await command('levels.open', {levelId:$('[data-level]').value})); status('已读取关卡权威文档。'); }
  function draw() {
    const editor = $('[data-edit]'); editor.replaceChildren();
    const actor = draft.modules.find(m => m.moduleId === 'characterTest.characters').payload.characters.find(a => a.stableId === $('[data-actor]').value);
    if (!actor) return;
    const identity = document.createElement('p'); identity.textContent = '实例 ' + actor.stableId + ' · 模板 ' + actor.characterId; editor.append(identity);
    const groups = document.createElement('select'); groups.append(option('无正式行为组',''));
    library.groups.forEach((g,i) => groups.append(option(g.displayName + ' / ' + g.groupId + ' @' + g.revision, String(i))));
    const binding = actor.behaviorBinding;
    groups.value = binding ? String(library.groups.findIndex(g => g.groupId === binding.groupId && g.revision === binding.groupRevision)) : '';
    groups.onchange = () => {
      if (groups.value === '') delete actor.behaviorBinding;
      else { const group = library.groups[Number(groups.value)]; actor.runtimeAiPlannerProfilePath = '';
        actor.behaviorBinding = {groupId:group.groupId,groupRevision:group.revision,groupContentHash:group.contentHash,
          cards:group.cards.map(c => ({entryId:c.entryId,cardId:c.cardId,revision:c.revision,semanticHash:c.semanticHash,contentHash:c.contentHash,inputFingerprint:''})),actions:[]}; }
      changed(); draw();
    }; editor.append(field('正式组版本', groups));
    if (!binding) return;
    const group = library.groups.find(g => g.groupId === binding.groupId && g.revision === binding.groupRevision);
    if (!group) { status('引用组版本缺失；请选择明确版本。'); return; }
    group.cards.filter(card => card.enabled).forEach(card => card.slots.forEach(path => {
      let value = binding.actions.find(a => a.entryId === card.entryId && a.slotPath === path);
      const actions = document.createElement('select'); actions.append(option('请选择动作','0'));
      library.actions.forEach(a => actions.append(option(a.Name + ' / ' + a.ActionId, String(a.ActionId)))); actions.value = String(value?.actionId || 0);
      actions.onchange = () => { if (!value) { value = {entryId:card.entryId,slotPath:path}; binding.actions.push(value); }
        value.actionId = Number(actions.value); value.dependencyFingerprint = ''; changed(); };
      editor.append(field(card.cardId + ' @' + card.revision + ' / ' + path, actions));
    }));
  }
  const request = () => ({ levelId:draft.levelId, expectedSourceHash:sourceHash, document:draft, previewToken });
  const run = fn => async () => { try { await fn(); } catch (error) { status(error.message); } };
  open.onclick = run(async () => { dialog.showModal(); if (!library && !localStorage.getItem(key)) await reload(); else if (localStorage.getItem(key)) status('有待完成操作，请点击恢复。'); });
  $('[data-close]').onclick = () => dialog.close();
  $('[data-refresh]').onclick = run(async () => { if (dirty && !confirm('放弃当前未保存修改并刷新？')) return; await reload(); });
  $('[data-resume]').onclick = run(async () => { const result = await command(null,null,true); if (result.document) apply(result); else { library = null; await reload(); } status('已恢复原操作。'); });
  $('[data-level]').onchange = run(async () => { if (dirty && !confirm('放弃未保存修改并切换关卡？')) { $('[data-level]').value = draft.levelId; return; } await load(); });
  $('[data-actor]').onchange = draw;
  $('[data-save]').onclick = run(async () => { apply(await command('levels.save',request())); status('已保存，尚未发布。'); });
  $('[data-preflight]').onclick = run(async () => { const result = await command('levels.preflight',request()); apply(result); status(result.message); });
  $('[data-publish]').onclick = run(async () => { apply(await command('levels.publish',request())); status('正式关卡已发布。'); });
})();
