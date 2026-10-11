"use strict";
(async () => {
  const query = new URL(location.href).searchParams, path = query.get("path") || "";
  const title = document.getElementById("source-title"), status = document.getElementById("source-status"), code = document.getElementById("source-code"), expand = document.getElementById("source-expand");
  try {
    const source = await Hub.readJson(query.has("cardKey") ? `/api/tactical-card/source?key=${encodeURIComponent(query.get("cardKey"))}`
      : `/api/combat-design/source?path=${encodeURIComponent(path)}`);
    const lines = source.text.split(/\r?\n/), requested = Math.max(1, Math.min(lines.length, Number.parseInt(query.get("line"), 10) || 1));
    title.textContent = source.path; document.title = `${source.path.split("/").pop()} · 组件来源`;
    document.getElementById("source-hash").textContent = `SHA256 ${source.hash}`;
    const render = full => {
      const first = full ? 0 : Math.max(0, requested - 20), last = full ? lines.length : Math.min(lines.length, requested + 100);
      const fragment = document.createDocumentFragment();
      for (let i = first; i < last; i++) {
        const row = document.createElement("span"), number = document.createElement("span"); row.id = `L${i + 1}`;
        if (i + 1 === requested) row.className = "source-selected";
        number.className = "source-line-number"; number.textContent = `${String(i + 1).padStart(5)}  `;
        row.append(number, document.createTextNode(lines[i] || " ")); fragment.append(row);
      }
      code.replaceChildren(fragment); status.textContent = `当前来源 · 显示 ${first + 1}–${last} / ${lines.length} 行；定位第 ${requested} 行。`;
      expand.hidden = full || (first === 0 && last === lines.length);
      document.getElementById(`L${requested}`)?.scrollIntoView({ block: "center" });
    };
    expand.addEventListener("click", () => render(true)); render(false);
  } catch (failure) {
    title.textContent = "来源不可用"; const error = document.getElementById("source-error"); error.hidden = false; error.textContent = failure.message;
  }
})();
