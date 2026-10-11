import assert from "node:assert/strict";
import fs from "node:fs";
import path from "node:path";
import { createRequire } from "node:module";

const require = createRequire(import.meta.url);
const { chromium } = require(process.env.PLAYWRIGHT_MODULE || "playwright");
const output = path.resolve(process.env.COMBAT_DESIGN_EVIDENCE_DIR || "combat-design-evidence");
fs.mkdirSync(output, { recursive: true });
const base = process.env.EDITOR_HUB_URL || "http://127.0.0.1:4873";
const browser = await chromium.launch({ headless: true, ...(process.env.CHROMIUM_EXECUTABLE_PATH ? { executablePath: process.env.CHROMIUM_EXECUTABLE_PATH } : {}) });
const page = await browser.newPage({ viewport: { width: 1500, height: 1000 } });
const errors = [], writes = [], checks = [];
page.on("pageerror", error => errors.push(error.message));
page.on("request", request => { if (!["GET", "HEAD"].includes(request.method())) writes.push(`${request.method()} ${request.url()}`); });
const check = (condition, name) => { assert.ok(condition, name); checks.push(name); };
const title = () => page.locator("#catalog-selected-title").textContent();
const selectedTab = name => page.getByRole("tab", { name, exact: true }).getAttribute("aria-selected");
try {
  await page.goto(`${base}/Tools/MxFramework.EditorHub/web/combat-design.html#intent`);
  await page.locator('.catalog-list [data-entry="component/approach"]').waitFor();
  const response = await page.request.get(`${base}/api/combat-design/catalog`); assert.equal(response.status(), 200);
  const catalog = await response.json();
  check(catalog.components.length === 16 && await page.locator(".catalog-list .catalog-entry").count() === 16, "all 16 source components visible");
  check(await selectedTab("意图目录") === "true", "intent tab selected");
  check(catalog.components.every(c => c.implementation === "main-source"), "newly merged components use current main-tree status");
  const before = await page.locator(".catalog-list .catalog-entry strong").allTextContents();
  for (const component of catalog.components) {
    await page.locator(`.catalog-list [data-entry="${component.entryKey}"]`).click();
    assert.equal(await title(), component.name);
    assert.ok(await page.locator("#catalog-detail .source-link").count());
  }
  check(true, "every component opens its own detail and source links");
  await page.locator("#catalog-search").fill("等待");
  check((await page.locator(".catalog-list .catalog-entry strong").allTextContents()).includes("等待条件"), "search finds wait component by name");
  await page.locator("#catalog-category").selectOption("等待");
  check(await page.locator(".catalog-list .catalog-entry").count() === 3, "search and category identify three distinct waits");
  await page.locator('.catalog-list [data-entry="component/wait-condition"]').click();
  await page.reload(); await page.locator('.catalog-list [data-entry="component/wait-condition"]').waitFor();
  check(await title() === "等待条件" && await page.locator("#catalog-search").inputValue() === "等待" && await selectedTab("意图目录") === "true", "selection search and tab survive reload");
  const sourceHref = await page.locator("#catalog-detail .source-link").first().getAttribute("href");
  const popupPromise = page.waitForEvent("popup"); await page.locator("#catalog-detail .source-link").first().click();
  const sourcePage = await popupPromise; await sourcePage.locator("#source-code .source-selected").waitFor();
  check((await sourcePage.locator("#source-code .source-selected").textContent()).includes("CreateWaitOpportunity"), "source link opens actual member at the recorded line");
  await sourcePage.screenshot({ path: path.join(output, "source.png"), fullPage: false }); await sourcePage.close();
  const uniqueSources = [...new Set(catalog.components.flatMap(c => c.sources.map(s => s.path)))];
  for (const file of uniqueSources) {
    const result = await page.request.get(`${base}/api/combat-design/source?path=${encodeURIComponent(file)}`);
    assert.equal(result.status(), 200); const data = await result.json(); assert.equal(data.path, file); assert.ok(data.text.length);
  }
  check(true, `all ${uniqueSources.length} unique component source targets readable`);
  await page.locator("#catalog-search").fill("");
  await page.locator("#catalog-category").selectOption("移动");
  check(await page.locator(".catalog-list .catalog-entry").count() === 8, "movement category contains eight components");
  await page.locator("#catalog-category").selectOption("");
  for (const state of ["isolated-source", "needs-review"]) {
    await page.locator("#catalog-state").selectOption(state);
    assert.equal(await page.locator(".catalog-list .catalog-entry").count(), 0);
    assert.ok(await page.getByText("本目录没有匹配对象。可调整筛选或搜索。").isVisible());
  }
  check(true, "isolated and candidate filters do not promote absent entries");
  await page.locator("#catalog-state").selectOption("main-source");
  await page.locator('.catalog-list [data-entry="component/facing-action"]').click();
  const users = page.locator("#catalog-detail section").filter({ has: page.getByRole("heading", { name: "使用此组件的模板表达", exact: true }) });
  await users.locator("button[data-entry]").first().click();
  check(await selectedTab("模板表达") === "true" && (await page.locator(".catalog-list .catalog-entry").count()) === catalog.entries.filter(e => e.kind === "template-expression").length, "reference navigation selects separate template tab");
  await page.locator("#catalog-detail button[data-entry]").first().click();
  check(await selectedTab("源审查") === "true", "shared-source reference selects audit tab");
  await page.locator('#catalog-detail button[data-entry^="component/"]').first().click();
  check(await selectedTab("意图目录") === "true", "component backlink restores intent tab");
  const oldFacing = catalog.entries.find(e => e.id === "tactical.shared.facing-target");
  await page.goto(`${base}/Tools/MxFramework.EditorHub/web/combat-design.html?entry=${encodeURIComponent(oldFacing.entryKey)}#intent`);
  await page.locator('.catalog-list [data-entry="component/facing-target"][aria-pressed="true"]').waitFor();
  check(new URL(page.url()).searchParams.get("entry") === "component/facing-target", "old single-intent bookmark maps to the full component directory");
  const legacy = await (await page.request.get(`${base}/api/combat-design/intent`)).json();
  check(legacy.components.length === 16, "legacy single-example API resolves to complete catalog");
  await page.getByRole("tab", { name: "意图目录", exact: true }).click();
  const refresh = page.waitForResponse(r => r.url() === `${base}/api/combat-design/catalog` && r.status() === 200);
  await page.getByRole("button", { name: "重新读取权威源", exact: true }).click(); await refresh;
  await page.locator('.catalog-list [data-entry="component/approach"]').waitFor();
  check(JSON.stringify(await page.locator(".catalog-list .catalog-entry strong").allTextContents()) === JSON.stringify(before), "refresh keeps complete deterministic directory");
  await page.screenshot({ path: path.join(output, "catalog-desktop.png"), fullPage: false });
  await page.setViewportSize({ width: 390, height: 844 });
  check(await page.evaluate(() => document.documentElement.scrollWidth <= window.innerWidth), "narrow viewport has no horizontal overflow");
  await page.screenshot({ path: path.join(output, "catalog-mobile.png"), fullPage: false });
  check(errors.length === 0 && writes.length === 0, "no page errors or mutating requests");
  const evidence = { base, checkedUtc: new Date().toISOString(), componentCount: catalog.components.length, checks, errors, writes, sourceHref, projectionHash: catalog.projectionHash };
  fs.writeFileSync(path.join(output, "browser-verification.json"), JSON.stringify(evidence, null, 2));
  console.log(JSON.stringify(evidence, null, 2));
} catch (failure) {
  await page.screenshot({ path: path.join(output, "failure.png"), fullPage: false });
  fs.writeFileSync(path.join(output, "failure.json"), JSON.stringify({ message: failure.message, checks, errors, writes, url: page.url(), visibleText: await page.locator("body").innerText() }, null, 2));
  throw failure;
} finally { await browser.close(); }
