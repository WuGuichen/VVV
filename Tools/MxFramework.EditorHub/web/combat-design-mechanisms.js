"use strict";
(() => {
  const esc = value => Hub.escapeHtml(String(value ?? "")), pretty = value => esc(JSON.stringify(value, null, 2));
  const content = document.getElementById("mechanisms-content"), status = document.getElementById("mechanisms-status"), error = document.getElementById("mechanisms-error");
  let projection, selected = "ready", loading = false; const open = new Map();
  function routeSelection() { if (!projection) return; const key = new URL(location.href).searchParams.get("mechanism"); selected = projection.mechanisms.some(m => m.navigationKey === key) ? key : "ready"; render(); }
  content.addEventListener("click", event => {
    const button = event.target.closest("button[data-mechanism]"); if (button) { remember(); window.CombatDesignViews.navigate({ view: "mechanisms", mechanism: button.dataset.mechanism }); }
    const drill = event.target.closest("button[data-intent-drill]"); if (drill) window.CombatDesignViews.selectIntent(drill.dataset.intentDrill);
    if (event.target.closest("#mechanism-refresh")) { load(); window.CombatDesignViews.refreshCatalog(); }
  });
  window.addEventListener("combat-design-route", routeSelection); window.addEventListener("combat-design-refresh-mechanisms", load);
  function remember() { content.querySelectorAll("#mechanism-detail details[data-proof]").forEach(d => open.set(`${selected}:${d.dataset.proof}`, d.open)); }
  function render() {
    const mechanism = projection.mechanisms.find(m => m.navigationKey === selected); const current = projection.sourceCurrent && mechanism.reviewedContractCurrent;
    const related = mechanism.relationships.map(entry => `<article><h3>${esc(entry.name)} · r${esc(entry.revision)}</h3><p class="intent-proof">${esc(entry.id)} · ${esc(entry.role.value === "intent" ? "已登记意图" : "待分类共享源")}<br>关系依据 ${esc(entry.basis)}</p><button class="quiet-button" data-intent-drill="${esc(entry.entryKey)}">查看此源的事件、条件与模板引用</button><p>实际引用草稿 ${entry.usedBy.length} 份：</p><ul>${entry.usedBy.map(ref => `<li><button class="quiet-button" data-intent-drill="${esc(ref.entryKey)}">${esc(ref.name)} · r${esc(ref.revision)}</button></li>`).join("")}</ul></article>`).join("");
    content.innerHTML = `<div class="toolbar"><button class="quiet-button" id="mechanism-refresh" ${loading ? "disabled" : ""}>重新读取权威源</button></div><nav class="mechanism-navigation" aria-label="玩法机制主目录">${projection.mechanisms.map(m => `<button class="quiet-button" data-mechanism="${esc(m.navigationKey)}" aria-pressed="${m.navigationKey === selected}">${esc(m.name)}</button>`).join("")}</nav>
      <section id="mechanism-detail" class="intent-panel"><p class="eyebrow">玩法机制</p><h2>${esc(mechanism.name)}</h2><p class="intent-summary">${esc(mechanism.purpose)}</p><p class="intent-proof">名称、设计目的和应对来自独立的 Agent 设计注释，绑定已有设计文档来源；不冒称由代码自动推断。</p><p class="${current ? "fresh" : "intent-warning"}">${current ? "登记源码指纹相符；已识别规则由固定提取和模板生成。" : "代码来源已变化或读取不一致，规则陈旧，需复核。"} 未提供当前运行验收。</p>
      <h3>源规则与提取事实</h3>${mechanism.rules.map(rule => `<article><h4>${esc(rule.title)}</h4><p class="${rule.parsed ? "" : "intent-warning"}">${esc(rule.text)}</p><details><summary>固定解析依据与真实代码</summary><p class="intent-proof">${esc(rule.source)}:${esc(rule.line)}<br>${esc(rule.origin)}<br>SHA256 ${esc(rule.observedHash)}</p><pre>${esc(rule.excerpt)}</pre></details></article>`).join("")}
      ${mechanism.configurationFacts.map(config => `<h3>当前生成配置</h3><p class="intent-proof">${esc(config.source)}<br>${esc(config.scope)}</p><pre>${pretty(config.facts)}</pre>`).join("")}<h3>利用与应对</h3><p>${esc(mechanism.response.text)}</p><p class="intent-proof">Agent 设计注释；来源${mechanism.annotation.current ? "相符" : "陈旧"}，与机器提取事实分开。</p><div class="lifecycle"><span class="facet">设计：${esc(mechanism.lifecycle.design)}</span><span class="facet">代码：${esc(mechanism.lifecycle.code)}</span><span class="facet">配置：源核查</span><span class="facet">启用：未推断</span><span class="facet">运行验证：未记录</span></div>
      <details data-proof="relationships"><summary>实际关联源与引用反查</summary>${related || "<p>此机制在当前固定提取覆盖内没有登记可证明的战术源关系；不会按名字猜关联。</p>"}<p class="intent-proof">关系来自功能槽、动作目录实际状态事件和草稿 Reference。组合、配置后可用卡、持有实例与卡组尚未接入。</p></details>
      <details data-proof="sources"><summary>设计注释来源与代码合同</summary><pre>${pretty(mechanism.annotation)}</pre>${mechanism.excerpts.map(e => `<p class="intent-proof">${esc(e.path)}:${esc(e.line)} · 文档核查快照</p><pre class="mechanism-excerpt">${esc(e.text)}</pre>`).join("")}<pre>${pretty(mechanism.sources)}</pre></details><details data-proof="evidence"><summary>版本绑定、诊断与范围</summary><p class="intent-proof">读取 ${esc(projection.generatedUtc)}<br>确定性机制 payload SHA256 ${esc(projection.projectionHash)}<br>目录 payload SHA256 ${esc(projection.catalogProjectionHash)}</p><p>${esc(projection.coverage)}</p><pre>${pretty(projection.diagnostics)}</pre></details></section>`;
    content.querySelectorAll("#mechanism-detail details[data-proof]").forEach(d => d.open = open.get(`${selected}:${d.dataset.proof}`) || false);
  }
  async function load() {
    if (loading) return; loading = true; error.hidden = true;
    try { const next = await Hub.readJson("/api/combat-design/mechanisms"); if (next.schema !== "mx.combat-design.mechanism-projection.v2") throw new Error("机制协议不匹配，请检查工具服务版本。"); projection = next; loading = false; routeSelection(); status.textContent = "已读取五个玩法机制及当前目录关系；源事实确定性生成，设计注释独立追踪。"; }
    catch (failure) { loading = false; error.hidden = false; error.textContent = failure.message; status.textContent = "机制读取失败，可重试。"; if (projection) { projection.sourceCurrent = false; render(); } else content.innerHTML = '<button id="mechanism-refresh" class="quiet-button">重试读取</button>'; }
  }
  load();
})();
