// Source-only preparation. Import with the Authoring CLI / Unity bridge afterwards.
// Never overwrites an existing image or explicit presentation selection.
import fs from "node:fs";
import path from "node:path";
import crypto from "node:crypto";
import zlib from "node:zlib";

const root = path.resolve(new URL("../../..", import.meta.url).pathname.replace(/^\/(?=[A-Z]:)/i, ""));
function crc32(bytes) {
  let crc = -1;
  for (const byte of bytes) { crc ^= byte; for (let i = 0; i < 8; i++) crc = (crc >>> 1) ^ (0xedb88320 & -(crc & 1)); }
  return (crc ^ -1) >>> 0;
}
function chunk(type, data) {
  const value = Buffer.concat([Buffer.from(type), data]);
  const head = Buffer.alloc(4), tail = Buffer.alloc(4);
  head.writeUInt32BE(data.length); tail.writeUInt32BE(crc32(value));
  return Buffer.concat([head, value, tail]);
}
function placeholder(standing) {
  const w = 256, h = standing ? 384 : 256;
  const raw = Buffer.alloc((w * 4 + 1) * h);
  for (let y = 0; y < h; y++) for (let x = 0; x < w; x++) {
    const u = x / w, v = y / h;
    const head = ((u - .5) / .13) ** 2 + ((v - (standing ? .2 : .32)) / (standing ? .085 : .13)) ** 2 < 1;
    const torso = v > (standing ? .3 : .5) && v < (standing ? .62 : .91) && Math.abs(u - .5) < (standing ? .14 : .28) - v * .025;
    const legs = standing && v >= .6 && v < .9 && ((u > .36 && u < .47) || (u > .53 && u < .64));
    const stripe = y > h - 20 && x > 16 && x < w - 16;
    const color = head || torso || legs ? [104, 157, 171, 255] : stripe ? [197, 162, 102, 255] : [25, 42, 53, 255];
    const offset = y * (w * 4 + 1) + 1 + x * 4;
    color.forEach((value, i) => { raw[offset + i] = value; });
  }
  const header = Buffer.alloc(13); header.writeUInt32BE(w); header.writeUInt32BE(h, 4); header[8] = 8; header[9] = 6;
  return Buffer.concat([Buffer.from([137,80,78,71,13,10,26,10]), chunk("IHDR", header), chunk("IDAT", zlib.deflateSync(raw)), chunk("IEND", Buffer.alloc(0))]);
}
const packagesRoot = path.join(root, "Authoring/CharacterPackages");
for (const name of fs.readdirSync(packagesRoot).filter(name => /^character-\d{6}$/.test(name))) {
  const dir = path.join(packagesRoot, name), read = f => JSON.parse(fs.readFileSync(path.join(dir, f), "utf8").replace(/^\uFEFF/, ""));
  const manifest = read("manifest.json"), catalog = read("resource_catalog.json"), app = read("config/character_application.json");
  app.presentationImages ||= { schemaVersion: "1.0", portraitResourceKey: "", standingResourceKey: "", variants: [] };
  for (const standing of [false, true]) {
    const slot = standing ? "standing" : "portrait", field = `${slot}ResourceKey`;
    if (app.presentationImages[field]) continue;
    const key = `char.${manifest.packageId}.texture.ui_${slot}_placeholder`, relative = `resources/ui/${slot}_placeholder.png`;
    const filename = path.join(dir, relative);
    fs.mkdirSync(path.dirname(filename), { recursive: true });
    if (!fs.existsSync(filename)) fs.writeFileSync(filename, placeholder(standing), { flag: "wx" });
    const hash = "sha256:" + crypto.createHash("sha256").update(fs.readFileSync(filename)).digest("hex");
    if (!catalog.entries.some(entry => entry.resourceKey === key)) catalog.entries.push({
      resourceKey: key, localId: `ui_${slot}_placeholder`, stableId: `charpkg.${manifest.packageId}.resource.ui_${slot}_placeholder`,
      packageId: manifest.packageId, typeId: "texture", variant: "", usage: standing ? "characterStanding" : "characterPortrait", sourceFormat: "png", relativePath: relative, hash,
      hashes: { algorithm: "sha256", contentHash: hash },
      importHints: { targetPathPolicy: "generatedCharacterPackage", targetRelativePath: relative, providerId: "unityAsset", metadata: { placeholderResource: "true", textureSemantic: "Color" } },
      preview: { isPlaceholder: true }, provenance: { sourceTool: "presentation-placeholder-generator", origin: "game-display-placeholder" },
      conflictPolicy: { allowOverwrite: false }, tags: ["ui", "placeholder"], dependencies: []
    });
    app.presentationImages[field] = key;
  }
  for (const [file, value] of [["resource_catalog.json", catalog], ["config/character_application.json", app]]) fs.writeFileSync(path.join(dir, file), JSON.stringify(value, null, 2) + "\n");
  console.log(`${manifest.packageId}: independent portrait/standing source slots prepared`);
}
