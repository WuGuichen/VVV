let tacticalCatalog = null;
let tacticalCatalogRequest = null;
let tacticalCatalogIssue = "";

async function loadTacticalCatalog() {
  if (tacticalCatalog) return tacticalCatalog;
  if (!tacticalCatalogRequest) tacticalCatalogRequest = fetch("/api/character/tactical-content")
    .then(async response => {
      const value = await response.json();
      if (!response.ok) throw new Error(value.message || "战术目录读取失败");
      tacticalCatalog = value;
      return value;
    }).catch(error => { tacticalCatalogIssue = error.message; tacticalCatalogRequest = null; return null; });
  return tacticalCatalogRequest;
}

function editTacticalDefaults(label, edit) {
  beginUndoTransaction(label);
  edit();
  updateUndoTransactionAfter();
  commitUndoTransaction();
  refreshDirtyFromCleanSnapshot();
  renderShellStatus();
  renderTacticalDefaultsWorkbench();
}

async function renderTacticalDefaultsWorkbench() {
  const pkg = state.package;
  if (!pkg || state.selectedPath !== "config/tactical") return;
  el.inspector.innerHTML = "<p>正在读取战术内容…</p>";
  const catalog = await loadTacticalCatalog();
  if (state.package !== pkg || state.selectedPath !== "config/tactical") return;
  const escape = value => escapeHtml(String(value ?? ""));
  if (!catalog) {
    el.inspector.innerHTML = `<p>${escape(tacticalCatalogIssue)}</p><button id="retryTacticalCatalog">重试</button>`;
    el.inspector.querySelector("button").onclick = () => renderTacticalDefaultsWorkbench();
    return;
  }
  const character = pkg.applicationConfig?.characterStableId;
  const defaults = pkg.tacticalDefaults;
  if (!defaults) {
    el.inspector.innerHTML = "<h3>默认战术内容</h3><p>此角色尚未配置默认战术内容。</p><button id=enableTacticalDefaults>创建默认清单</button>";
    el.inspector.querySelector("button").onclick = () => editTacticalDefaults("创建默认战术清单", () => {
      pkg.tacticalDefaults = { schema: "mx.character.tactical-defaults.v1", characterStableId: character, actionIds: [], cards: [], bindings: [], diagnostics: [] };
    });
    return;
  }
  if (defaults.schema !== "mx.character.tactical-defaults.v1") {
    el.inspector.innerHTML = "<p>此默认配置版本不受支持，已保留原数据。请升级工具后编辑。</p>";
    return;
  }
  const actions = (catalog.actions || []).filter(action => action.characterStableIds?.includes(character));
  const actionName = id => { const value = actions.find(action => action.id === id); return value?.displayName || `${id}（名称待完善）`; };
  const cards = catalog.cards || [];
  el.inspector.innerHTML = `<section class="tactical-defaults-workbench"><header><h3>默认战术内容</h3><p>新游戏使用此初始清单。已有存档中的获取、移除和换装不会改写这里。</p><button id="reloadTacticalCatalog" type="button">刷新内容目录</button></header><div class="tactical-defaults-grid">
    <section><h4>初始战术动作</h4>${actions.map(action => `<label class="field"><input type="checkbox" data-tactical-action="${escape(action.id)}" ${defaults.actionIds.includes(action.id) ? "checked" : ""}><span>${escape(actionName(action.id))}</span><small title="${escape(action.source)}">${escape(action.actionLibraryId)} · 动作 ${action.actionId}</small></label>`).join("")}
    ${defaults.actionIds.filter(id => !actions.some(a => a.id === id)).map(id => `<p>失效动作：${escape(id)} <button data-remove-missing-action="${escape(id)}">移除</button></p>`).join("")}
    </section><section><h4>初始行为卡</h4>${cards.map(card => `<label class="field"><input type="checkbox" data-tactical-card="${escape(card.cardId)}" data-revision="${card.revision}" ${defaults.cards.some(c => c.cardId === card.cardId && c.revision === card.revision) ? "checked" : ""}><span>${escape(card.displayName || card.cardId)} · 版本 ${card.revision}</span></label>`).join("")}
    </section><section><h4>卡槽装配</h4>${defaults.cards.map(reference => {
      const card = cards.find(c => c.cardId === reference.cardId && c.revision === reference.revision && c.contentHash === reference.contentHash);
      if (!card) return `<p>行为卡版本失效：${escape(reference.cardId)} <button data-remove-missing-card="${escape(reference.cardId)}">移除</button></p>`;
      return `<fieldset><legend>${escape(card.displayName || card.cardId)}</legend>${card.slots.map(slot => {
        const binding = defaults.bindings.find(b => b.cardId === card.cardId && b.slotPath === slot);
        return `<label class="field">${escape(slot)}<select data-tactical-slot="${escape(slot)}" data-card="${escape(card.cardId)}"><option value="">未装配（此卡不可用）</option>${defaults.actionIds.map(id => `<option value="${escape(id)}" ${binding?.tacticalActionId === id ? "selected" : ""}>${escape(actionName(id))}</option>`).join("")}</select></label>`;
      }).join("")}</fieldset>`;
    }).join("")}</section></div>${(defaults.diagnostics || []).map(text => `<p>${escape(text)}</p>`).join("")}</section>`;
  el.inspector.querySelector("#reloadTacticalCatalog").onclick = () => { tacticalCatalog = null; tacticalCatalogRequest = null; renderTacticalDefaultsWorkbench(); };

  function removeAction(id) {
    const references = defaults.bindings.filter(b => b.tacticalActionId === id);
    if (references.length && !window.confirm(`此动作被以下卡槽引用：\n${references.map(b => `${b.cardId} / ${b.slotPath}`).join("\n")}\n是否解除引用并移除？相关卡将需要重新装配。`)) return false;
    editTacticalDefaults("移除默认战术动作", () => {
      defaults.actionIds = defaults.actionIds.filter(value => value !== id);
      defaults.bindings = defaults.bindings.filter(b => b.tacticalActionId !== id);
    });
    return true;
  }
  el.inspector.querySelectorAll("[data-tactical-action]").forEach(input => input.onchange = () => {
    const id = input.dataset.tacticalAction;
    if (input.checked) editTacticalDefaults("增加默认战术动作", () => defaults.actionIds.push(id));
    else if (!removeAction(id)) input.checked = true;
  });
  el.inspector.querySelectorAll("[data-remove-missing-action]").forEach(button => button.onclick = () => removeAction(button.dataset.removeMissingAction));
  function removeCard(id) {
    editTacticalDefaults("移除默认行为卡", () => {
      defaults.cards = defaults.cards.filter(c => c.cardId !== id);
      defaults.bindings = defaults.bindings.filter(b => b.cardId !== id);
    });
  }
  el.inspector.querySelectorAll("[data-remove-missing-card]").forEach(button => button.onclick = () => removeCard(button.dataset.removeMissingCard));
  el.inspector.querySelectorAll("[data-tactical-card]").forEach(input => input.onchange = () => {
    const card = cards.find(c => c.cardId === input.dataset.tacticalCard && c.revision === Number(input.dataset.revision));
    if (!input.checked) return removeCard(card.cardId);
    if (defaults.cards.some(c => c.cardId === card.cardId)) { input.checked = false; window.alert("先移除已配置的另一版本，再选择新版本。"); return; }
    editTacticalDefaults("增加默认行为卡", () => defaults.cards.push({ cardId: card.cardId, revision: card.revision, contentHash: card.contentHash }));
  });
  el.inspector.querySelectorAll("[data-tactical-slot]").forEach(select => select.onchange = () => editTacticalDefaults("修改默认卡槽装配", () => {
    defaults.bindings = defaults.bindings.filter(b => b.cardId !== select.dataset.card || b.slotPath !== select.dataset.tacticalSlot);
    if (select.value) defaults.bindings.push({ cardId: select.dataset.card, slotPath: select.dataset.tacticalSlot, tacticalActionId: select.value });
  }));
}

Object.assign(globalThis, { renderTacticalDefaultsWorkbench });
