"use strict";
(() => {
  const esc = value => Hub.escapeHtml(String(value ?? "")), pretty = value => esc(JSON.stringify(value, null, 2));
  const content = document.getElementById("intent-content"), status = document.getElementById("intent-status"), error = document.getElementById("intent-error");
  const views = { cards: "cards-view", mechanisms: "mechanisms-view", intent: "intent-view", templates: "templates-view", sources: "sources-view", authoring: "authoring-view", snapshots: "snapshot-view" }, names = Object.keys(views);
  const catalogViews = new Set(["intent", "templates", "sources"]), host = document.getElementById("catalog-host");
  let activeView = "intent";
  const labels = { intent: "已登记意图", combination: "已登记可选组合", pending: "待分类共享源", "unparsed-card": "未解析卡草稿", "template-expression": "模板制作表达" };
  const implementationLabels = { "main-source": "主树已实现", "isolated-source": "仅隔离实现", "needs-review": "候选 / 待复核" };
  const isComponent = entry => entry?.kind === "intent-component";
  const sourceLink = (path, line = 1, label = path) => `<a class="intent-link source-link" target="_blank" rel="noopener" href="./combat-design-source.html?path=${encodeURIComponent(path)}&line=${Number(line) || 1}#L${Number(line) || 1}">${esc(label)}</a>`;
  let projection, loading = false, changeNotice = ""; const open = new Map();
  function selectTab(view) {
    if (!Object.hasOwn(views, view)) view = "cards";
    for (const name of names) { const selected = name === view, tab = document.getElementById(`${name}-tab`); tab.setAttribute("aria-selected", String(selected)); tab.tabIndex = selected ? 0 : -1; document.getElementById(views[name]).hidden = !selected; }
  }
  function route(changes = {}, push = true) {
    const url = new URL(location.href);
    for (const [key, value] of Object.entries(changes)) { if (key === "view") url.hash = `#${value}`; else if (value) url.searchParams.set(key, value); else url.searchParams.delete(key); }
    if (url.href !== location.href) history[push ? "pushState" : "replaceState"](null, "", url); readRoute();
  }
  function entryView(entry) { return isComponent(entry) ? "intent" : entry.kind === "template-expression" ? "templates" : "sources"; }
  function selectEntry(key) {
    const entry = projection?.entries.find(e => e.entryKey === key);
    route({ view: entry ? entryView(entry) : "sources", entry: key || "", q: "", role: "", state: "", category: "" });
  }
  function readRoute() {
    const url = new URL(location.href), query = url.searchParams;
    let view = Object.hasOwn(views, url.hash.slice(1)) ? url.hash.slice(1) : "cards";
    let selected = projection?.entries.find(e => e.entryKey === query.get("entry"));
    // Old single-intent bookmarks resolve to their recognized component, never to a mixed template list.
    if (view === "intent" && selected?.componentUses?.length === 1) {
      selected = projection.entries.find(e => e.entryKey === selected.componentUses[0].entryKey);
      if (selected) { url.searchParams.set("entry", selected.entryKey); history.replaceState(null, "", url); }
    }
    // Redirect legacy mixed-directory links; type-specific directories never show another type's detail.
    if (view === "intent" && query.get("role") && query.get("role") !== "intent") view = query.get("role") === "template-expression" ? "templates" : "sources";
    if (selected && ((view === "intent" && entryView(selected) !== "intent") || (view === "templates" && entryView(selected) !== "templates"))) view = entryView(selected);
    if (url.hash !== `#${view}`) { url.hash = `#${view}`; history.replaceState(null, "", url); }
    selectTab(view);
    if (catalogViews.has(view)) { activeView = view; document.getElementById(`${view}-mount`).appendChild(host); }
    if (projection) render();
    window.dispatchEvent(new Event("combat-design-route"));
  }
  window.CombatDesignViews = { navigate: route, selectEntry, selectIntent: selectEntry, refreshCatalog: load };
  names.forEach((name, index) => {
    const tab = document.getElementById(`${name}-tab`); tab.addEventListener("click", () => route({ view: name, entry: "", q: "", role: "", state: "", category: "" }));
    tab.addEventListener("keydown", event => { const next = event.key === "ArrowRight" ? (index + 1) % names.length : event.key === "ArrowLeft" ? (index + names.length - 1) % names.length : event.key === "Home" ? 0 : event.key === "End" ? names.length - 1 : -1; if (next < 0) return; event.preventDefault(); route({ view: names[next], entry: "", q: "", role: "" }); document.getElementById(`${names[next]}-tab`).focus(); });
  });
  window.addEventListener("popstate", readRoute); window.addEventListener("hashchange", readRoute);
  content.addEventListener("click", event => { const button = event.target.closest("button[data-entry]"); if (button) { remember(); if (button.closest(".catalog-list")) route({ entry: button.dataset.entry, view: activeView }); else selectEntry(button.dataset.entry); } if (event.target.closest("#catalog-refresh")) { load(); window.dispatchEvent(new Event("combat-design-refresh-mechanisms")); } });
  content.addEventListener("input", event => { if (event.target.id === "catalog-search") route({ q: event.target.value, entry: "" }, false); });
  content.addEventListener("change", event => { const fields = { "catalog-role": "role", "catalog-state": "state", "catalog-category": "category" }; if (fields[event.target.id]) route({ [fields[event.target.id]]: event.target.value, entry: "" }); });
  function remember() { const key = document.getElementById("catalog-detail")?.dataset.entry; content.querySelectorAll("#catalog-detail details[data-proof]").forEach(d => open.set(`${key}:${d.dataset.proof}`, d.open)); }
  const button = (entry, selected = false) => !entry ? "" : `<button class="quiet-button catalog-entry" data-entry="${esc(entry.entryKey)}" aria-pressed="${selected}"><strong>${esc(entry.name || entry.id)}</strong><span>${isComponent(entry) ? `${esc(entry.category)} · ${esc(implementationLabels[entry.implementation])}` : `${esc(labels[entry.role.value] || "待分类")} · r${esc(entry.revision)}`}</span><small>${isComponent(entry) ? `已索引 ${entry.instances.length} 个事件块实例` : esc(entry.id)}</small></button>`;
  function render() {
    remember(); const query = new URL(location.href).searchParams, search = (query.get("q") || "").slice(0, 512), selectedKey = query.get("entry"), selected = selectedKey ? projection.entries.find(e => e.entryKey === selectedKey) : null;
    const auditRoles = new Set(["pending", "shared", "unparsed-card", "combination", "all"]);
    const requestedRole = query.get("role"), inferredAuditRole = selected?.kind === "card-draft" ? "unparsed-card" : selected?.role?.value === "combination" ? "combination" : selected?.kind === "shared-source" ? "shared" : "pending";
    const role = activeView === "intent" ? "intent" : activeView === "templates" ? "template-expression" : auditRoles.has(requestedRole) ? requestedRole : inferredAuditRole;
    const state = query.get("state") || "", category = query.get("category") || "";
    const entries = projection.entries.filter(e => activeView === "intent" ? isComponent(e) && (!state || e.implementation === state) && (!category || e.category === category)
      : !isComponent(e) && (role === "all" || (role === "shared" ? e.kind === "shared-source" : e.role.value === role)))
      .filter(e => `${e.name} ${e.id || ""} ${e.key || ""} ${e.summary || ""} ${e.workspaceId || ""} ${(e.events || []).map(v => v.description).join(" ")}`.toLocaleLowerCase().includes(search.toLocaleLowerCase()));
    const detail = selected && entries.some(e => e.entryKey === selected.entryKey) ? selected : entries[0], focused = document.activeElement?.id === "catalog-search", caret = document.getElementById("catalog-search")?.selectionStart;
    const counts = projection.entries.filter(e => !isComponent(e)).reduce((m, e) => { m[e.role.value] = (m[e.role.value] || 0) + 1; return m; }, {});
    const components = projection.entries.filter(isComponent), stateCount = value => components.filter(e => e.implementation === value).length;
    const scopeName = activeView === "intent" ? "意图组件" : activeView === "templates" ? "模板制作表达" : "原始源审查";
    if (!loading && error.hidden) status.textContent = `${scopeName}：当前筛选 ${entries.length} 项。${activeView === "intent" ? "目录从当前组件源码生成，草稿使用情况另行索引。" : `来自 ${projection.workspaces.length} 个工作区。`}重新读取不需要 Editor。`;
    const auditFilter = activeView === "sources" ? `<label>审查范围<select id="catalog-role">${[["pending", "待分类共享源"], ["shared", "全部共享源（审查）"], ["unparsed-card", "未解析旧卡草稿"], ["combination", "已登记可选组合"], ["all", "全部对象原始源（审查）"]].map(([v, label]) => `<option value="${v}" ${role === v ? "selected" : ""}>${label}</option>`).join("")}</select></label>` : "";
    const componentFilters = activeView === "intent" ? `<label>实现状态<select id="catalog-state"><option value="">全部状态</option>${Object.entries(implementationLabels).map(([value, label]) => `<option value="${value}" ${state === value ? "selected" : ""}>${label} (${stateCount(value)})</option>`).join("")}</select></label><label>组件类别<select id="catalog-category"><option value="">全部类别</option>${[...new Set(components.map(e => e.category))].map(value => `<option value="${esc(value)}" ${category === value ? "selected" : ""}>${esc(value)}</option>`).join("")}</select></label>` : "";
    const scopeText = activeView === "intent" ? `组件 ${components.length} 项 · 主树已实现 ${stateCount("main-source")} · 仅隔离实现 ${stateCount("isolated-source")} · 候选 / 待复核 ${stateCount("needs-review")}。实现状态只据本次来源；正式卡发布与玩家装配另行验证。` : activeView === "templates" ? `已识别模板制作表达 ${counts["template-expression"] || 0} 份。完整表达及其共享定义不作为基础意图组件。` : `原始源审查：共享源 ${projection.entries.filter(e => e.kind === "shared-source").length} · 待分类 ${counts.pending || 0} · 未解析旧卡草稿 ${counts["unparsed-card"] || 0}。历史分类记录不决定源码组件是否进入目录。`;
    content.innerHTML = `<div class="catalog-controls"><label>搜索${esc(scopeName)}名称 / ID / 操作<input id="catalog-search" type="search" value="${esc(search)}" placeholder="例如：面向、等待"></label>${componentFilters}${auditFilter}<button class="quiet-button" id="catalog-refresh" ${loading ? "disabled" : ""}>重新读取权威源</button></div><p class="intent-proof" id="catalog-scope">${esc(scopeText)}</p>${changeNotice ? `<p class="intent-warning" role="status">${esc(changeNotice)}</p>` : ""}<div class="catalog-layout"><nav class="catalog-list" aria-label="${esc(scopeName)}目录"><p>本次筛选 ${entries.length} 项</p>${entries.map(e => button(e, e.entryKey === detail?.entryKey)).join("") || "<p>本目录没有匹配项。</p>"}</nav><div id="catalog-detail" data-entry="${esc(detail?.entryKey || "")}">${selectedKey && !selected ? '<p class="intent-warning">URL 指向的源已不在本次索引内；引用或版本可能变化。</p>' : ""}${detail ? details(detail) : '<section class="intent-panel"><p>本目录没有匹配对象。可调整筛选或搜索。</p></section>'}</div></div>`;
    content.querySelectorAll("#catalog-detail details[data-proof]").forEach(d => d.open = open.get(`${detail?.entryKey}:${d.dataset.proof}`) || false);
    if (focused) { const input = document.getElementById("catalog-search"); input.focus(); if (caret != null) input.setSelectionRange(caret, caret); }
  }
  function details(entry) {
    if (isComponent(entry)) return componentDetails(entry);
    const find = key => projection.entries.find(e => e.entryKey === key);
    const refs = entry.resolvedReferences.map(ref => `<li>${ref.resolved ? button(find(ref.entryKey)) : `<strong class="intent-warning">缺失引用 ${esc(ref.id)}</strong>`}<p class="intent-proof">${esc(ref.pointer)}</p></li>`).join("");
    const used = entry.usedBy.map(ref => button(find(ref.entryKey))).join("");
    const events = entry.events.map(event => `<li><h3>${esc(event.title || event.eventId)}</h3><p>${esc(event.description)}</p><p>${esc(event.condition.text)}</p><p class="intent-proof">${esc(event.timing.description)} · ${esc(event.eventId)}<br>每轮上限 ${esc(event.execution.maxExecutionsPerPass ?? "未配置")} · 冷却帧 ${esc(event.execution.cooldownFrames ?? "未配置")}</p><details><summary>真实事件参数、条件树与时间锚点</summary><pre>${pretty(event.raw)}</pre><p class="intent-proof">${esc(event.pointer)}</p></details></li>`).join("");
    return `<section class="intent-panel"><p class="eyebrow">${esc(labels[entry.role.value] || "待分类共享源")}</p><h2 id="catalog-selected-title">${esc(entry.name || entry.id)}</h2><p class="intent-proof">${esc(entry.id)} · r${esc(entry.revision)}<br>${esc(entry.workspaceId)}</p><p>${entry.role.value === "pending" ? "设计角色尚未登记或分类来源陈旧；对象和事实仍可查看。不会按名称、文件夹或 Sequence 推断它是意图或独立组合。" : entry.kind === "template-expression" ? "既有卡草稿的组合表达，可用于制作模板；不证明独立模板身份、配置后可用卡、持有实例或卡组。" : entry.kind === "card-draft" ? "旧卡草稿 payload 未解析，只保留原文供审查；不归为意图或模板表达。" : "角色来自有来源的设计分类记录；不是从代码自动推断设计动机。"}</p><p class="${projection.sourceCurrent ? "fresh" : "intent-warning"}">${projection.sourceCurrent ? "本次读取一致；源码事实，不是运行验收。" : "读取期间源有变化，事实待重新读取。"} 解析状态：${esc(entry.factsStatus)}</p><p>${sourceLink(entry.source, 1, "打开真实源文件")}</p><div class="lifecycle"><span class="facet">设计：${esc(entry.role.current ? "分类来源相符" : "待分类/复核")}</span><span class="facet">代码：固定解析</span><span class="facet">配置：源已索引</span><span class="facet">启用：未推断</span><span class="facet">运行证据：未记录</span></div>${(entry.componentUses || []).length ? `<h3>本源包含的意图组件</h3>${entry.componentUses.map(use => button(find(use.entryKey))).join("")}` : ""}</section>
      <section class="intent-panel"><h2>事件、条件与组织</h2>${events ? `<ol class="intent-events">${events}</ol>` : "<p>此 schema 没有本解析器可读取的事件；查看原始源或引用。</p>"}${entry.blocks.map(block => `<article><h3>事件块 ${esc(block.blockId)}</h3><p>${esc(block.description)}</p><details><summary>真实块参数</summary><pre>${pretty(block.facts)}</pre></details></article>`).join("")}</section>
      <section class="intent-panel"><h2>动作槽与当前数据</h2>${entry.slots.map(slot => `<article><h3>${esc(slot.slotId)} → ${esc(slot.resolver)}</h3><p>${esc(slot.state)}。角色实际装配未验证。</p>${slot.action ? `<p>${esc(slot.action.name)} · ActionId ${esc(slot.action.actionId)} · 目录时长 ${esc(slot.action.raw.durationFrames)} 帧</p><details><summary>动作目录完整数据</summary><pre>${pretty(slot.action)}</pre></details>` : "<p>只有声明；绑定和当前角色输入未解析。</p>"}<details><summary>源槽声明</summary><pre>${pretty(slot.declaration)}</pre></details></article>`).join("") || "<p>源未声明动作槽。</p>"}<p class="intent-proof">值来自原有 source/action catalog；没有第二套数值台账。模板与玩家不能在这里改设计者硬规则。</p></section>
      <section class="intent-panel"><h2>${entry.role.value === "intent" ? "使用此意图的模板表达与引用反查" : "真实引用与反查"}</h2><h3>本对象引用</h3>${refs ? `<ul>${refs}</ul>` : "<p>当前已解析范围内无 Reference 引用。</p>"}<h3>${entry.role.value === "intent" ? "引用本意图的对象（不是意图列表项）" : "何处使用本对象"}</h3>${used || "<p>本次工作区索引内未发现使用者。</p>"}<p class="intent-proof">已识别表达节点的多对多直接引用；未知 schema / 节点保留原文。可用卡、实例、卡组、场景与跨工作区依赖尚未接入。</p></section>
      <section class="intent-panel"><h2>解析缺口与版本证据</h2>${entry.unparsed.length ? `<pre class="intent-warning">${pretty(entry.unparsed)}</pre>` : "<p>该源在当前固定解析覆盖内，没有已发现的未解析项。</p>"}<details data-proof="classification"><summary>分类依据</summary><pre>${pretty(entry.role)}</pre></details><details data-proof="source"><summary>源版本与完整原文</summary><p class="intent-proof">${esc(entry.source)}<br>源 SHA256 ${esc(entry.sourceHash)}</p><pre>${pretty(entry.raw)}</pre></details><details data-proof="projection"><summary>投影范围、读取集与诊断</summary><p>${esc(projection.coverage)}</p><p class="intent-proof">生成时间 ${esc(projection.generatedUtc)}<br>确定性 payload SHA256 ${esc(projection.projectionHash)}（时间单独排除）</p><pre>${pretty(entry.compileMetadata)}</pre><pre>${pretty(projection.diagnostics)}</pre><pre>${pretty(projection.readSet)}</pre></details></section>`;
  }
  function componentDetails(entry) {
    const find = key => projection.entries.find(e => e.entryKey === key);
    const used = [...new Map(entry.instances.flatMap(instance => instance.usedBy || []).map(ref => [ref.entryKey, ref])).values()];
    return `<section class="intent-panel"><p class="eyebrow">意图 · 模板组件</p><h2 id="catalog-selected-title">${esc(entry.name)}</h2><p class="intent-summary">${esc(entry.summary)}</p><div class="lifecycle"><span class="facet">${esc(implementationLabels[entry.implementation])}</span><span class="facet">${esc(entry.category)}</span><span class="facet">已索引实例 ${entry.instances.length}</span></div><p>${esc(entry.availability)}</p>${!projection.sourceCurrent ? '<p class="intent-warning">读取期间源发生变化，请重新读取。</p>' : ""}${entry.factoryMode ? `<p>工厂模式：<code>${esc(entry.factoryMode)}</code></p>` : ""}${entry.issues.length ? `<pre class="intent-warning">${pretty(entry.issues)}</pre>` : ""}</section>
      <section class="intent-panel"><h2>组件实现与制作参数</h2><p>以下入口和默认参数直接读取当前源码；每份已保存配置见下方实例。</p>${entry.sources.map(source => `<article><h3>${esc(source.member)}</h3><p>${sourceLink(source.path, source.line, `${source.path.split("/").pop()}:${source.line}`)}</p><details data-proof="member-${esc(source.member)}"><summary>查看源码与参数</summary><pre>${esc(source.excerpt)}</pre><p class="intent-proof">源码 SHA256 ${esc(source.hash)}<br>成员 SHA256 ${esc(source.memberHash)}</p></details></article>`).join("")}${entry.action ? `<h3>功能槽当前动作</h3><p>${esc(entry.action.data?.displayName)} · ActionId ${esc(entry.action.data?.actionId)} · ${esc(entry.action.data?.durationFrames)} 帧</p><p>${sourceLink(entry.action.source, 1, "打开动作目录来源")}</p>` : ""}</section>
      <section class="intent-panel"><h2>已保存的组件实例</h2>${entry.instances.length ? "<p>按事件块结构识别；同一组件的多份配置保留各自的值。包含它的完整战术不增加组件数量。</p>" : "<p>本次已索引草稿中未发现匹配实例。实现入口已列出；不据此生成或发布正式卡。</p>"}${entry.instances.map(instance => `<article class="component-instance"><h3>${esc(instance.name)} · ${esc(instance.block.title || instance.block.blockId)}</h3><p class="intent-proof">${esc(instance.definitionId)} · r${esc(instance.revision)} · ${esc(instance.timelineId)} / ${esc(instance.block.blockId)}</p><p>${sourceLink(instance.source, 1, "打开实例来源")}</p><details data-proof="instance-${esc(instance.entryKey)}-${esc(instance.block.blockId)}"><summary>事件、时序、条件与槽参数</summary><pre>${pretty({ block: instance.block, events: instance.events, slots: instance.slots })}</pre></details>${button(find(instance.entryKey))}</article>`).join("")}</section>
      <section class="intent-panel"><h2>使用此组件的模板表达</h2>${used.map(ref => button(find(ref.entryKey))).join("") || "<p>本次已解析引用范围内未发现模板表达使用者。</p>"}<p class="intent-proof">关联来自工作区索引和真实 Reference；不代表发布、装配或运行验收。</p></section>
      <section class="intent-panel"><details data-proof="component-projection"><summary>生成范围与诊断</summary><p>${esc(projection.coverage)}</p><p class="intent-proof">生成时间 ${esc(projection.generatedUtc)}<br>确定性投影 SHA256 ${esc(projection.projectionHash)}</p><pre>${pretty(projection.diagnostics)}</pre></details></section>`;
  }
  async function load() {
    if (loading) return; loading = true; error.hidden = true; status.textContent = "正在重新读取工作区索引与真实源…";
    try {
      const next = await Hub.readJson("/api/combat-design/catalog"); if (next.schema !== "mx.combat-design.catalog-projection.v1" || !Array.isArray(next.components)) throw new Error("组件目录协议不匹配，请检查工具服务版本。");
      if (projection && projection.projectionHash !== next.projectionHash) changeNotice = "已检测到源或分类记录变化，事实与引用已重建。先前投影不能作为当前验证证据；陈旧分类/规则会单独标记。";
      projection = { ...next, entries: [...next.components, ...next.entries] }; loading = false; readRoute();
    } catch (failure) { loading = false; error.hidden = false; error.textContent = failure.message; status.textContent = "读取失败；可重试，旧结果只作缓存参考。"; if (projection) { projection.sourceCurrent = false; render(); } else content.innerHTML = '<button class="quiet-button" id="catalog-refresh">重试读取</button>'; }
  }
  readRoute(); load();
})();
