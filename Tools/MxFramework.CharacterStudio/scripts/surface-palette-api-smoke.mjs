const baseUrl = String(process.env.WGAME_SURFACE_PALETTE_API || "http://127.0.0.1:4875").replace(/\/$/, "");

const catalog = await get("/api/authoring/palette/catalog");
require(catalog.format === "mx.wgameSurfacePaletteCatalog.v3", "catalog format v3");
require(catalog.schemaVersion === "3.0", "catalog schema 3.0");
require(catalog.width === 256 && catalog.height === 256 && catalog.colorCount === 65536, "catalog color dimensions");
require(catalog.surfaceProfileCount === 16 && catalog.logicalTokenCount === 1048576, "catalog profile/token counts");
const rgb = Buffer.from(catalog.colorData.rgb8Base64, "base64");
require(rgb.length === 65536 * 3, "compact RGB8 coverage");
require(catalog.colorData.uniqueRgb8Count === 65536 && catalog.colorData.aliasCount === 0, "all RGB565 addresses unique");
require(!catalog.colorData.cMaxU16Base64 && !catalog.colorData.canonicalColorIndexU16Base64, "legacy alias tables removed");
const unique = new Set();
for (let index = 0; index < 65536; index++) {
  unique.add((rgb[index * 3] << 16) | (rgb[index * 3 + 1] << 8) | rgb[index * 3 + 2]);
}
require(unique.size === 65536, "RGB8 table contains no duplicates");

const formula = await get("/api/authoring/palette/query?red5=17&green6=42&blue5=9&surfaceProfile=crystal_frosted");
const expectedIndex = (17 << 11) | (42 << 5) | 9;
require(formula.matches?.[0]?.colorIndex === expectedIndex, "RGB565 component query");
require(formula.matches?.[0]?.tokenId === "wgame.surface.v3.r17.g42.b09.crystal_frosted", "v3 formula Token ID");
require(formula.matches?.[0]?.canonicalColorIndex === expectedIndex && formula.matches?.[0]?.isAlias === false, "every address is canonical");

const direct = await get(`/api/authoring/palette/query?colorIndex=${expectedIndex}&profile=metal_polished`);
require(direct.matches?.[0]?.red5 === 17 && direct.matches?.[0]?.green6 === 42 && direct.matches?.[0]?.blue5 === 9, "formula reverse decode");
require(direct.matches?.[0]?.surfaceProfileId === "metal_polished", "query selects surface profile");

const logicalToken = expectedIndex * 16 + 12;
const logical = await get(`/api/authoring/palette/query?logicalToken=${logicalToken}`);
require(logical.matches?.[0]?.logicalToken === logicalToken, "logical token round trip");

const exactHex = formula.matches[0].srgbHex;
const exact = await get(`/api/authoring/palette/query?hex=${encodeURIComponent(exactHex)}`);
require(exact.matches?.[0]?.colorIndex === expectedIndex, "exact RGB565 Hex query");

const nearest = await get("/api/authoring/palette/query?nearestHex=%23E8BFA8&profile=skin_soft");
require(nearest.queryMode === "nearestRgb565" && nearest.matches?.[0]?.surfaceProfileId === "skin_soft", "nearest RGB565 query");

const references = await get(`/api/authoring/palette/references?logicalToken=${logicalToken}`);
require(Number.isInteger(references.referenceCount) && Number.isInteger(references.faceCount), "versioned usage references");

for (const mutation of ["create", "duplicate", "update", "retire"]) {
  const result = await post(`/api/authoring/palette/${mutation}`, {}, 404);
  require(result.code === "SURFACE_PALETTE_READ_ONLY", `${mutation} must be removed from generator-owned Catalog v3`);
}

console.log("RGB565 Surface Palette v3 read-only formula/query API smoke passed.");

async function get(path) {
  const response = await fetch(baseUrl + path, { cache: "no-store" });
  const body = await response.json();
  if (!response.ok) throw new Error(`${path}: ${response.status} ${body.message || body.error || ""}`);
  return body;
}

async function post(path, payload, expectedStatus = 200) {
  const response = await fetch(baseUrl + path, {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(payload)
  });
  const body = await response.json();
  if (response.status !== expectedStatus) {
    throw new Error(`${path}: expected ${expectedStatus}, got ${response.status}: ${body.message || body.error || ""}`);
  }
  return body;
}

function require(condition, message) {
  if (!condition) throw new Error(message);
}
