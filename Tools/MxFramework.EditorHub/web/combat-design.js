"use strict";
const escape = text => Hub.escapeHtml(String(text ?? ""));
const status = document.getElementById("status"), error = document.getElementById("error");
const snapshots = document.getElementById("snapshots");
let selectedSnapshotHandle = "";
const labels = { design: "设计理由", code: "代码契约", configuration: "源登记范围", enabled: "启用范围", validated: "验证", play: "Play" };
const values = { unrecorded: "未记录", documented: "已记录", "registered-owner-contract": "已注册 owner 契约", unconnected: "未登记该共享源", connected: "仅该共享源已登记；不证明可执行", "fixture-only": "仅隔离样例", "not-inferred-from-draft": "草稿不足以证明启用", "not-recorded": "未记录", "owner-compile-text": "已编译并生成说明", stale: "已陈旧", "not-run": "未运行" };
document.getElementById("query").addEventListener("click", load);
document.getElementById("refresh-owner").addEventListener("click", () => command("refresh"));
document.getElementById("create-fixture").addEventListener("click", () => command("create-fixture"));
snapshots.addEventListener("change", event => {
  if (event.target.id !== "snapshot-choice") return;
  selectedSnapshotHandle = event.target.value;
  selectSnapshot();
});
load();

async function load() {
  error.hidden = true;
  try {
    const query = await Hub.readJson("/api/combat-design/query");
    const handles = query.snapshots.map(view => String(view.data.handle));
    if (!handles.includes(selectedSnapshotHandle)) selectedSnapshotHandle = "";
    const options = query.snapshots.map(view => `<option value="${escape(view.data.handle)}">${escape(view.data.name)} · ${escape(snapshotIdentity(view.data, handles))}</option>`).join("");
    snapshots.innerHTML = query.snapshots.length
      ? `<div class="snapshot-navigation"><label for="snapshot-choice">查阅快照（来源 ID 区分同名样例）</label><select id="snapshot-choice"><option value="">全部快照（${query.snapshots.length}）</option>${options}</select></div>${query.snapshots.map(view => render(view, handles)).join("")}` : "";
    const choice = document.getElementById("snapshot-choice");
    if (choice) choice.value = selectedSnapshotHandle;
    selectSnapshot();
    status.textContent = query.snapshots.length ? `已读取 ${query.snapshots.length} 份 owner 快照。` : "尚无快照。刷新会请求当前项目 Editor 自动发现草稿；生成样例会运行隔离 owner 链路。";
  } catch (e) { showError(e); }
}
async function command(operation) {
  error.hidden = true;
  const buttons = document.querySelectorAll("button"); buttons.forEach(b => b.disabled = true);
  try {
    let result = await fetchJson("/api/combat-design/submit", { operation, idempotencyKey: `${operation}/${crypto.randomUUID()}` });
    status.textContent = "请求已排队，等待项目 Editor owner。";
    for (let attempt = 0; attempt < 15 && result.status === "PendingEditor"; attempt++) {
      await new Promise(resolve => setTimeout(resolve, 1000));
      result = await Hub.readJson(`/api/combat-design/status?operationId=${encodeURIComponent(result.operationId)}`);
    }
    if (result.status === "Succeeded") { await load(); status.textContent = "owner 已完成，已重新读取快照。"; }
    else if (result.status === "PendingEditor") status.textContent = `等待 Editor；尚未确认写入或验证。操作 ${result.operationId} 可由 Agent 查询。`;
    else throw new Error(`${result.status}：${result.message}。${result.recovery || ""}`);
  } catch (e) { showError(e); } finally { buttons.forEach(b => b.disabled = false); }
}
async function fetchJson(url, body) {
  const response = await fetch(url, { method: "POST", headers: { "Content-Type": "application/json" }, body: JSON.stringify(body) });
  const value = await response.json(); if (!response.ok) throw new Error(value.message || value.code); return value;
}
function showError(e) { error.hidden = false; error.textContent = e.message; status.textContent = "请修正诊断后刷新；旧快照不作为成功证据。"; }

function shortHandle(handle, handles) {
  const value = String(handle);
  let length = 8;
  while (length < value.length && handles.some(other => other !== value && other.slice(0, length) === value.slice(0, length))) length += 4;
  return value.slice(0, length);
}
function snapshotIdentity(data, handles) {
  const date = new Date(data.generatedUtc);
  const generated = Number.isNaN(date.getTime()) ? String(data.generatedUtc || "未记录") : date.toISOString().replace("T", " ").replace(/\.\d{3}Z$/, " UTC");
  return `${data.writable ? "隔离样例" : "主项目草稿 · 只读"} · 来源 ${shortHandle(data.handle, handles)} · 生成 ${generated}`;
}
function selectSnapshot() {
  snapshots.querySelectorAll(".design-workspace").forEach(section => {
    section.hidden = selectedSnapshotHandle !== "" && section.dataset.snapshotHandle !== selectedSnapshotHandle;
  });
}
function render(view, handles) {
  const data = view.data, fresh = view.sourceCurrent && view.evidenceCurrent;
  const contract = data.contractFacts;
  const facts = contract?.contract === "recognized-facing-target/v1" && contract.comparison === "strict-less-than"
    ? `<p>转向当前目标；目标必须存活，方向必须有效。夹角 <strong>小于 ${escape(contract.thresholdDegrees)}°</strong> 时到位；独立上限 <strong>${escape(contract.maximumFrames)} 帧</strong>。该事实来自 owner 识别的 Facing 契约。</p>`
    : "<p>当前源结构没有可可靠概括的已注册面向契约，请展开权威源查看。</p>";
  const cards = data.cards.map(c => `<tr><td>${escape(c.name)}<br><small class="technical">草稿 r${escape(c.revision)} · 发布 r${escape(c.publishedRevision)}</small></td><td>${c.dependsOnFacing ? "引用共享面向机制" : "无直接引用"}</td><td><details><summary>编排/模板引用</summary><pre>${escape(JSON.stringify(c.composition, null, 2))}</pre></details></td></tr>`).join("");
  const previews = (data.evidence?.previews || []).map(p => { const card = data.cards.find(c => c.id === p.cardId); return `<article class="design-card"><h4>${escape(card?.name || "战术卡")}</h4><p>${escape(p.description)}</p><details open><summary>卡面文字预览</summary><pre>${escape(p.cardFace)}</pre></details><details><summary>细节与警告</summary><pre>${escape(p.details)}</pre><p>${escape((p.warnings || []).join("；") || "无说明警告")}</p></details></article>`; }).join("");
  return `<section class="design-workspace" data-snapshot-handle="${escape(data.handle)}"><h2>${escape(data.name)}</h2><p class="snapshot-identity">${escape(snapshotIdentity(data, handles))}</p><p>${data.writable ? "隔离验收样例 · 使用独立发布目录和目录内现有身份" : "主项目草稿 · 只读"}</p>
    <p class="${fresh ? "fresh" : "stale"}">${!view.sourceCurrent ? "权威源/代码已变化，请刷新；所有旧验证失效。" : !view.evidenceCurrent ? "当前源没有有效验证证据；旧预览仅供参考。" : "证据与当前源版本绑定；不含 Play 或引擎卡面截图验收。"}</p>
    <div class="lifecycle">${Object.entries(data.lifecycle || {}).map(([k,v]) => `<span class="facet">${escape(labels[k] || k)}：${escape(values[v] || v)}</span>`).join("")}</div>
    <h3>机制事实</h3>${facts}<details><summary>权威共享源</summary><pre>${escape(JSON.stringify(data.facts, null, 2))}</pre></details>
    <h3>设计理由</h3><p>${escape(data.rationale || "尚未由 Agent 撰写；系统不会从代码猜测设计理由。")}</p>
    <h3>受影响对象与编排引用</h3><table><thead><tr><th>战术卡</th><th>自动反查</th><th>引用来源</th></tr></thead><tbody>${cards}</tbody></table>
    <h3>真实编译与生成说明</h3>${previews || "<p>没有编译后的说明。</p>"}<p>${escape(data.evidence?.publication || "尚无隔离发布与重载证据。")}</p>
    <details><summary>Agent 句柄、版本与证据范围</summary><p>句柄 <code>${escape(data.handle)}</code><br>快照 SHA256 <code>${escape(view.snapshotHash)}</code></p><p>${escape(data.coverage)}</p><p class="technical">${escape(data.generatedUtc)}</p></details></section>`;
}
