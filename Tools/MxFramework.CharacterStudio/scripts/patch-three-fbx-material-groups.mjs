import fs from "node:fs";
import { fileURLToPath } from "node:url";

const loaderPath = fileURLToPath(new URL("../node_modules/three/examples/jsm/loaders/FBXLoader.js", import.meta.url));
const marker = "WGame CharacterStudio: draw AllSame FBX material groups";
let source = fs.readFileSync(loaderPath, "utf8");
if (!source.includes(marker)) {
  const anchor = "\t\tthis.addMorphTargets( geo, geoNode, morphTargets, preTransform );";
  if (source.indexOf(anchor) < 0 || source.indexOf(anchor) !== source.lastIndexOf(anchor)) {
    throw new Error("Unexpected FBXLoader geometry builder; cannot patch AllSame material groups.");
  }
  source = source.replace(anchor, `\t\t// ${marker}
\t\t// Unused material slots still produce a material array. Without a group,
\t\t// WebGLRenderer submits no draw calls for that array.
\t\tif ( geoInfo.material && geoInfo.material.mappingType === 'AllSame' ) {
\t\t\tconst materialIndex = getData( 0, 0, 0, geoInfo.material )[ 0 ];
\t\t\tgeo.addGroup( 0, buffers.vertex.length / 3, Math.max( 0, materialIndex ) );
\t\t}

${anchor}`);
  fs.writeFileSync(loaderPath, source, "utf8");
}
process.stdout.write("Three.js FBX AllSame material groups ready.\n");
