import fs from "node:fs";
import path from "node:path";
import { fileURLToPath } from "node:url";
import "./patch-three-fbx-material-groups.mjs";

const scriptDirectory = path.dirname(fileURLToPath(import.meta.url));
const loaderPath = path.resolve(
  scriptDirectory,
  "../node_modules/three/examples/jsm/loaders/FBXLoader.js"
);
const marker = "WGame CharacterStudio: preserve FBX vertex color alpha";

if (!fs.existsSync(loaderPath)) {
  throw new Error(`Three.js FBXLoader not found: ${loaderPath}`);
}

let source = fs.readFileSync(loaderPath, "utf8");
if (source.includes(marker)) {
  process.stdout.write("Three.js FBXLoader already preserves PaletteData alpha.\n");
  process.exit(0);
}

function replaceOnce(before, after, label) {
  const first = source.indexOf(before);
  if (first < 0 || source.indexOf(before, first + before.length) >= 0) {
    throw new Error(`Unexpected Three.js FBXLoader source while patching ${label}.`);
  }
  source = source.replace(before, after);
}

replaceOnce(
  "geo.setAttribute( 'color', new Float32BufferAttribute( buffers.colors, 3 ) );",
  `// ${marker}\n\t\t\tgeo.setAttribute( 'color', new Float32BufferAttribute( buffers.colors, 4 ) );`,
  "BufferAttribute itemSize"
);

replaceOnce(
  "faceColors.push( data[ 0 ], data[ 1 ], data[ 2 ] );",
  "faceColors.push( data[ 0 ], data[ 1 ], data[ 2 ], data[ 3 ] );",
  "polygon color alpha"
);

replaceOnce(
  `buffers.colors.push( faceColors[ i0 * 3 ] );
\t\t\t\tbuffers.colors.push( faceColors[ i0 * 3 + 1 ] );
\t\t\t\tbuffers.colors.push( faceColors[ i0 * 3 + 2 ] );

\t\t\t\tbuffers.colors.push( faceColors[ i1 * 3 ] );
\t\t\t\tbuffers.colors.push( faceColors[ i1 * 3 + 1 ] );
\t\t\t\tbuffers.colors.push( faceColors[ i1 * 3 + 2 ] );

\t\t\t\tbuffers.colors.push( faceColors[ i2 * 3 ] );
\t\t\t\tbuffers.colors.push( faceColors[ i2 * 3 + 1 ] );
\t\t\t\tbuffers.colors.push( faceColors[ i2 * 3 + 2 ] );`,
  `buffers.colors.push( faceColors[ i0 * 4 ] );
\t\t\t\tbuffers.colors.push( faceColors[ i0 * 4 + 1 ] );
\t\t\t\tbuffers.colors.push( faceColors[ i0 * 4 + 2 ] );
\t\t\t\tbuffers.colors.push( faceColors[ i0 * 4 + 3 ] );

\t\t\t\tbuffers.colors.push( faceColors[ i1 * 4 ] );
\t\t\t\tbuffers.colors.push( faceColors[ i1 * 4 + 1 ] );
\t\t\t\tbuffers.colors.push( faceColors[ i1 * 4 + 2 ] );
\t\t\t\tbuffers.colors.push( faceColors[ i1 * 4 + 3 ] );

\t\t\t\tbuffers.colors.push( faceColors[ i2 * 4 ] );
\t\t\t\tbuffers.colors.push( faceColors[ i2 * 4 + 1 ] );
\t\t\t\tbuffers.colors.push( faceColors[ i2 * 4 + 2 ] );
\t\t\t\tbuffers.colors.push( faceColors[ i2 * 4 + 3 ] );`,
  "triangulated color alpha"
);

fs.writeFileSync(loaderPath, source, "utf8");
process.stdout.write("Patched Three.js FBXLoader to preserve PaletteData RGBA.\n");
