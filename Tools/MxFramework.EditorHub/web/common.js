const Hub = (() => {
  const storageKey = "mx.editorHub.preferences.v1";
  const toolPaths = {
    character: "/Tools/MxFramework.CharacterStudio/web/",
    animation: "/Tools/MxFramework.AnimationEditor/web/",
    resources: "/Tools/MxFramework.ResourceLibrary/web/",
    audio: "/Tools/MxFramework.AudioWorkbench/web/",
    composer: "/Tools/MxFramework.FairyGuiUiComposer/web/",
    art: "/Tools/MxFramework.FairyGuiArtSlices/web/",
    buff: "/Tools/MxFramework.BuffWorkbench/web/",
    diagnostics: "/Tools/MxFramework.EditorHub/web/diagnostics.html"
  };
  function readPreferences() {
    try {
      const value = JSON.parse(localStorage.getItem(storageKey) || "{}");
      return value && typeof value === "object" && !Array.isArray(value) ? value : {};
    } catch { return {}; }
  }
  function writePreferences(value) {
    try { localStorage.setItem(storageKey, JSON.stringify(value)); } catch { /* The launcher also works without browser storage. */ }
  }
  function rememberTarget(kind, id) {
    const preferences = readPreferences();
    preferences.targets = { ...preferences.targets, [kind]: id };
    writePreferences(preferences);
  }
  function toolUrl(tool, targetId) {
    if (!Object.hasOwn(toolPaths, tool) || typeof targetId !== "string" || !targetId) return null;
    const key = tool === "composer" ? "screen" : tool === "art" ? "library" : "package";
    return `${toolPaths[tool]}?${key}=${encodeURIComponent(targetId)}`;
  }
  function rememberVisit(tool, targetId, targetLabel) {
    if (!toolUrl(tool, targetId) || tool === "diagnostics") return;
    const preferences = readPreferences();
    const previous = Array.isArray(preferences.recent) ? preferences.recent : [];
    preferences.recent = [{ tool, targetId, targetLabel, visitedAt: Date.now() }, ...previous
      .filter(item => item && toolUrl(item.tool, item.targetId) && (item.tool !== tool || item.targetId !== targetId))].slice(0, 6);
    writePreferences(preferences);
  }
  function escapeHtml(value) {
    return String(value ?? "").replace(/[&<>"']/g, ch => ({ "&": "&amp;", "<": "&lt;", ">": "&gt;", '"': "&quot;", "'": "&#39;" })[ch]);
  }
  function packageLabel(pkg) {
    return `${pkg.displayName || pkg.packageId || pkg.relative}${pkg.displayName && pkg.packageId && pkg.displayName !== pkg.packageId ? ` · ${pkg.packageId}` : ""}${pkg.isTemplate ? "（模板）" : ""}`;
  }
  async function readJson(url) {
    const response = await fetch(url, { headers: { Accept: "application/json" }, cache: "no-store", signal: AbortSignal.timeout(15000) });
    if (!response.ok) throw new Error(`${response.status} ${response.statusText}`);
    return response.json();
  }
  function showDialog(dialog) {
    if (typeof dialog.showModal === "function") dialog.showModal();
    else dialog.setAttribute("open", "open");
  }
  function closeDialog(dialog) {
    if (typeof dialog.close === "function") dialog.close();
    else dialog.removeAttribute("open");
  }
  return { readPreferences, writePreferences, rememberTarget, rememberVisit, toolUrl, escapeHtml, packageLabel, readJson, showDialog, closeDialog };
})();
