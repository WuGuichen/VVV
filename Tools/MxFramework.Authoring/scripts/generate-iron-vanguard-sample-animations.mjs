import fs from "node:fs";
import path from "node:path";
import crypto from "node:crypto";

const repoRoot = path.resolve(new URL("../../../", import.meta.url).pathname);
const packageRoot = path.join(repoRoot, "Tools/MxFramework.Authoring/samples/character-iron-vanguard");
const animationDir = path.join(packageRoot, "resources/animations");
const catalogPath = path.join(packageRoot, "resource_catalog.json");

const TARGET_ARRAY_BUFFER = 34962;
const TARGET_ELEMENT_ARRAY_BUFFER = 34963;
const COMPONENT_FLOAT = 5126;
const COMPONENT_UNSIGNED_SHORT = 5123;

const cubePositions = [
  -0.5, -0.5, 0.5,
  0.5, -0.5, 0.5,
  0.5, 0.5, 0.5,
  -0.5, 0.5, 0.5,
  -0.5, -0.5, -0.5,
  0.5, -0.5, -0.5,
  0.5, 0.5, -0.5,
  -0.5, 0.5, -0.5
];

const cubeIndices = [
  0, 1, 2, 0, 2, 3,
  1, 5, 6, 1, 6, 2,
  5, 4, 7, 5, 7, 6,
  4, 0, 3, 4, 3, 7,
  3, 2, 6, 3, 6, 7,
  4, 5, 1, 4, 1, 0
];

const nodeMap = {
  root: 0,
  torso: 1,
  head: 2,
  leftArm: 3,
  rightArm: 4,
  leftLeg: 5,
  rightLeg: 6,
  sword: 7,
  shield: 8
};

main();

function main() {
  fs.mkdirSync(animationDir, { recursive: true });
  writeGlb("locomotion.glb", createDocument(createLocomotionAnimations()));
  writeGlb("combat.glb", createDocument(createCombatAnimations()));
  updateResourceCatalog();
}

function createDocument(animations) {
  const builder = createBufferBuilder();
  const positionAccessor = builder.addAccessor(cubePositions, COMPONENT_FLOAT, "VEC3", TARGET_ARRAY_BUFFER);
  const indexAccessor = builder.addAccessor(cubeIndices, COMPONENT_UNSIGNED_SHORT, "SCALAR", TARGET_ELEMENT_ARRAY_BUFFER);

  const document = {
    asset: { version: "2.0", generator: "MxFramework sample animation generator" },
    scene: 0,
    scenes: [{ nodes: [nodeMap.root] }],
    nodes: [
      { name: "Armature", children: [nodeMap.torso, nodeMap.head, nodeMap.leftArm, nodeMap.rightArm, nodeMap.leftLeg, nodeMap.rightLeg, nodeMap.sword, nodeMap.shield] },
      { name: "Torso", mesh: 0, translation: [0, 1.15, 0], scale: [0.45, 0.85, 0.25] },
      { name: "Head", mesh: 1, translation: [0, 2.05, 0], scale: [0.28, 0.28, 0.28] },
      { name: "LeftArm", mesh: 2, translation: [-0.52, 1.28, 0], scale: [0.13, 0.58, 0.13] },
      { name: "RightArm", mesh: 2, translation: [0.52, 1.28, 0], scale: [0.13, 0.58, 0.13] },
      { name: "LeftLeg", mesh: 2, translation: [-0.18, 0.35, 0], scale: [0.14, 0.68, 0.14] },
      { name: "RightLeg", mesh: 2, translation: [0.18, 0.35, 0], scale: [0.14, 0.68, 0.14] },
      { name: "Sword", mesh: 3, translation: [0.73, 1.18, 0.05], scale: [0.04, 0.78, 0.04] },
      { name: "Shield", mesh: 4, translation: [-0.72, 1.15, 0.02], scale: [0.34, 0.44, 0.08] }
    ],
    meshes: [
      mesh("torso", positionAccessor, indexAccessor, 0),
      mesh("head", positionAccessor, indexAccessor, 1),
      mesh("limb", positionAccessor, indexAccessor, 2),
      mesh("sword", positionAccessor, indexAccessor, 3),
      mesh("shield", positionAccessor, indexAccessor, 4)
    ],
    materials: [
      material("Iron teal", [0.12, 0.62, 0.62, 1]),
      material("Signal head", [0.95, 0.72, 0.22, 1]),
      material("Bone yellow", [0.82, 0.78, 0.14, 1]),
      material("Sword bronze", [0.78, 0.46, 0.18, 1]),
      material("Shield blue", [0.14, 0.28, 0.9, 1])
    ],
    animations: animations.map(animation => bakeAnimation(animation, builder))
  };

  document.buffers = [{ byteLength: builder.byteLength() }];
  document.bufferViews = builder.bufferViews;
  document.accessors = builder.accessors;
  return { document, binary: builder.binary() };
}

function createLocomotionAnimations() {
  return [
    {
      name: "idle",
      tracks: [
        translation(nodeMap.torso, [0, 0.5, 1.0, 1.2], [[0, 1.15, 0], [0, 1.19, 0], [0, 1.15, 0], [0, 1.15, 0]]),
        rotation(nodeMap.head, [0, 0.6, 1.2], [quatY(-0.08), quatY(0.08), quatY(-0.08)])
      ]
    },
    {
      name: "walk",
      tracks: [
        translation(nodeMap.root, [0, 0.45, 0.9], [[0, 0, 0], [0, 0.03, 0.06], [0, 0, 0.12]]),
        rotation(nodeMap.leftLeg, [0, 0.45, 0.9], [quatX(-0.42), quatX(0.42), quatX(-0.42)]),
        rotation(nodeMap.rightLeg, [0, 0.45, 0.9], [quatX(0.42), quatX(-0.42), quatX(0.42)]),
        rotation(nodeMap.leftArm, [0, 0.45, 0.9], [quatX(0.35), quatX(-0.35), quatX(0.35)]),
        rotation(nodeMap.rightArm, [0, 0.45, 0.9], [quatX(-0.35), quatX(0.35), quatX(-0.35)])
      ]
    },
    {
      name: "run",
      tracks: [
        translation(nodeMap.root, [0, 0.35, 0.7], [[0, 0, 0], [0, 0.06, 0.12], [0, 0, 0.24]]),
        rotation(nodeMap.leftLeg, [0, 0.35, 0.7], [quatX(-0.75), quatX(0.75), quatX(-0.75)]),
        rotation(nodeMap.rightLeg, [0, 0.35, 0.7], [quatX(0.75), quatX(-0.75), quatX(0.75)]),
        rotation(nodeMap.leftArm, [0, 0.35, 0.7], [quatX(0.62), quatX(-0.62), quatX(0.62)]),
        rotation(nodeMap.rightArm, [0, 0.35, 0.7], [quatX(-0.62), quatX(0.62), quatX(-0.62)])
      ]
    }
  ];
}

function createCombatAnimations() {
  return [
    {
      name: "sword_attack",
      tracks: [
        rotation(nodeMap.rightArm, [0, 0.25, 0.55, 0.8], [quatZ(-0.2), quatZ(-1.15), quatZ(0.85), quatZ(-0.2)]),
        rotation(nodeMap.sword, [0, 0.25, 0.55, 0.8], [quatZ(0.0), quatZ(-0.65), quatZ(1.05), quatZ(0.0)]),
        translation(nodeMap.torso, [0, 0.35, 0.8], [[0, 1.15, 0], [0.08, 1.14, 0.06], [0, 1.15, 0]])
      ]
    },
    {
      name: "shield_guard",
      tracks: [
        rotation(nodeMap.leftArm, [0, 0.35, 1.0], [quatZ(0.1), quatZ(0.72), quatZ(0.1)]),
        translation(nodeMap.shield, [0, 0.35, 1.0], [[-0.72, 1.15, 0.02], [-0.54, 1.38, 0.18], [-0.72, 1.15, 0.02]]),
        rotation(nodeMap.torso, [0, 0.35, 1.0], [quatY(0), quatY(-0.18), quatY(0)])
      ]
    },
    {
      name: "unarmed_attack",
      tracks: [
        rotation(nodeMap.rightArm, [0, 0.22, 0.52, 0.78], [quatX(-0.2), quatX(-1.25), quatX(0.55), quatX(-0.2)]),
        translation(nodeMap.rightArm, [0, 0.22, 0.52, 0.78], [[0.52, 1.28, 0], [0.62, 1.28, 0.16], [0.52, 1.28, 0.04], [0.52, 1.28, 0]])
      ]
    }
  ];
}

function bakeAnimation(animation, builder) {
  const samplers = [];
  const channels = [];
  for (const track of animation.tracks) {
    const input = builder.addAccessor(track.times, COMPONENT_FLOAT, "SCALAR");
    const output = builder.addAccessor(track.values.flat(), COMPONENT_FLOAT, track.path === "rotation" ? "VEC4" : "VEC3");
    samplers.push({ input, output, interpolation: "LINEAR" });
    channels.push({ sampler: samplers.length - 1, target: { node: track.node, path: track.path } });
  }
  return { name: animation.name, samplers, channels };
}

function mesh(name, positionAccessor, indexAccessor, materialIndex) {
  return {
    name,
    primitives: [{ attributes: { POSITION: positionAccessor }, indices: indexAccessor, material: materialIndex }]
  };
}

function material(name, baseColorFactor) {
  return {
    name,
    pbrMetallicRoughness: { baseColorFactor, metallicFactor: 0, roughnessFactor: 0.72 }
  };
}

function translation(node, times, values) {
  return { node, times, values, path: "translation" };
}

function rotation(node, times, values) {
  return { node, times, values, path: "rotation" };
}

function quatX(angle) {
  return quatAxis([1, 0, 0], angle);
}

function quatY(angle) {
  return quatAxis([0, 1, 0], angle);
}

function quatZ(angle) {
  return quatAxis([0, 0, 1], angle);
}

function quatAxis(axis, angle) {
  const half = angle / 2;
  const s = Math.sin(half);
  return [axis[0] * s, axis[1] * s, axis[2] * s, Math.cos(half)];
}

function createBufferBuilder() {
  const chunks = [];
  const accessors = [];
  const bufferViews = [];
  let byteOffset = 0;

  return {
    accessors,
    bufferViews,
    addAccessor(values, componentType, type, target) {
      const componentCount = getComponentCount(type);
      const typed = componentType === COMPONENT_UNSIGNED_SHORT
        ? new Uint16Array(values)
        : new Float32Array(values);
      const byteLength = typed.byteLength;
      const aligned = align4(byteOffset);
      if (aligned > byteOffset) {
        chunks.push(Buffer.alloc(aligned - byteOffset));
        byteOffset = aligned;
      }
      const bufferView = bufferViews.length;
      bufferViews.push({
        buffer: 0,
        byteOffset,
        byteLength,
        ...(target ? { target } : {})
      });
      chunks.push(Buffer.from(new Uint8Array(typed.buffer)));
      byteOffset += byteLength;

      const accessor = {
        bufferView,
        componentType,
        count: values.length / componentCount,
        type
      };
      const bounds = calculateBounds(values, componentCount);
      if (bounds) {
        accessor.min = bounds.min;
        accessor.max = bounds.max;
      }
      accessors.push(accessor);
      return accessors.length - 1;
    },
    byteLength() {
      return align4(byteOffset);
    },
    binary() {
      const aligned = align4(byteOffset);
      if (aligned > byteOffset) chunks.push(Buffer.alloc(aligned - byteOffset));
      return Buffer.concat(chunks);
    }
  };
}

function calculateBounds(values, componentCount) {
  if (values.length === 0) return null;
  const min = Array(componentCount).fill(Number.POSITIVE_INFINITY);
  const max = Array(componentCount).fill(Number.NEGATIVE_INFINITY);
  for (let i = 0; i < values.length; i += componentCount) {
    for (let j = 0; j < componentCount; j++) {
      const value = values[i + j];
      min[j] = Math.min(min[j], value);
      max[j] = Math.max(max[j], value);
    }
  }
  return { min, max };
}

function getComponentCount(type) {
  switch (type) {
    case "SCALAR": return 1;
    case "VEC2": return 2;
    case "VEC3": return 3;
    case "VEC4": return 4;
    default: throw new Error(`Unsupported accessor type: ${type}`);
  }
}

function writeGlb(fileName, { document, binary }) {
  const json = Buffer.from(JSON.stringify(document), "utf8");
  const jsonPadding = align4(json.length) - json.length;
  const binPadding = align4(binary.length) - binary.length;
  const jsonChunk = Buffer.concat([json, Buffer.alloc(jsonPadding, 0x20)]);
  const binChunk = Buffer.concat([binary, Buffer.alloc(binPadding)]);
  const totalLength = 12 + 8 + jsonChunk.length + 8 + binChunk.length;
  const header = Buffer.alloc(12);
  header.writeUInt32LE(0x46546c67, 0);
  header.writeUInt32LE(2, 4);
  header.writeUInt32LE(totalLength, 8);
  const jsonHeader = Buffer.alloc(8);
  jsonHeader.writeUInt32LE(jsonChunk.length, 0);
  jsonHeader.writeUInt32LE(0x4e4f534a, 4);
  const binHeader = Buffer.alloc(8);
  binHeader.writeUInt32LE(binChunk.length, 0);
  binHeader.writeUInt32LE(0x004e4942, 4);
  fs.writeFileSync(path.join(animationDir, fileName), Buffer.concat([header, jsonHeader, jsonChunk, binHeader, binChunk]));
}

function updateResourceCatalog() {
  const catalog = JSON.parse(fs.readFileSync(catalogPath, "utf8"));
  const modifiedUtc = "2026-05-21T00:00:00Z";
  for (const entry of catalog.entries || []) {
    if (entry.relativePath !== "resources/animations/locomotion.glb" && entry.relativePath !== "resources/animations/combat.glb") {
      continue;
    }
    const fullPath = path.join(packageRoot, entry.relativePath);
    const hash = `sha256:${crypto.createHash("sha256").update(fs.readFileSync(fullPath)).digest("hex")}`;
    entry.hash = hash;
    entry.hashes ||= {};
    entry.hashes.algorithm = "sha256";
    entry.hashes.contentHash = hash;
    entry.importHints ||= {};
    entry.importHints.metadata ||= {};
    entry.importHints.metadata.placeholderResource = "false";
    entry.preview ||= {};
    entry.preview.isPlaceholder = false;
    entry.provenance ||= {};
    entry.provenance.license = "sample-generated";
    entry.provenance.modifiedUtc = modifiedUtc;
  }
  fs.writeFileSync(catalogPath, `${JSON.stringify(catalog, null, 2)}\n`);
}

function align4(value) {
  return (value + 3) & ~3;
}
