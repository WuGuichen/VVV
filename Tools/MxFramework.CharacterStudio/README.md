# MxFramework CharacterStudio

CharacterStudio is the external browser workstation for authoring Character Resource Packages. The C1 MVP is wired to the existing Authoring CLI/Core pipeline instead of reimplementing resource resolution, compilation, diagnostics, or Unity import logic in the UI.

## Run

Install the browser 3D dependency once:

```bash
npm --prefix Tools/MxFramework.CharacterStudio install
```

From the repository root:

```bash
dotnet run --project Tools/MxFramework.Authoring/src/MxFramework.Authoring.Cli/MxFramework.Authoring.Cli.csproj -- \
  editor serve --root . --port 4873 --package Tools/MxFramework.Authoring/samples/character-iron-vanguard
```

Open:

```text
http://127.0.0.1:4873/Tools/MxFramework.CharacterStudio/web/
```

The root `index.html` redirects to `web/` for static repo servers.

## Scope

- package discovery and package tree for the Iron Vanguard Character Resource Package;
- read-only resource, geometry, validation, and import report browsing;
- Chinese-first workstation labels and actions;
- model import into the selected Character Resource Package plus a model resource library for selecting which imported resource replaces the body, main-hand slot, or off-hand slot;
- Three.js viewport for package GLB resources, with selectable colliders, sockets, weapon attachments, and traces;
- SVG viewport fallback when `node_modules/three` has not been installed;
- inspector draft edits for colliders, sockets, attachments, and traces;
- save through `/api/character/save`, which validates with the Authoring Core gate before writing package JSON;
- compile diagnostics through `/api/character/compile`;
- Unity import bridge through `/api/character/import-unity`.

Static file preview still opens the sample package, but save, compile, and import require `editor serve`.

`导入模型` writes the selected `.glb` / `.gltf` file into the current package under `resources/models/`, updates `resource_catalog.json`, and saves through the same validation gate as normal draft edits. `角色主体模型` replaces the body model resource, `主手槽武器模型` / `副手槽武器模型` replace the preview model referenced by the current weapon attachment slot, and `仅导入资源` only adds a catalog resource without binding it to the body or a weapon slot. The `导入资源` strip lists model resources with thumbnail, name, usage, format, and current body/weapon-slot binding; clicking a resource thumbnail binds it to the current target selected in the model-purpose dropdown and marks the package dirty. `.fbx` input is accepted as an import source only: the Authoring server converts it through the local `fbx2gltf` dependency, stores the generated `.glb`, and records `sourceFormat: "glb"` in the package catalog. `导入 Unity` is a separate step that writes the compiled package outputs into the Unity project.

FBX conversion requires the CharacterStudio npm dependencies:

```bash
npm --prefix Tools/MxFramework.CharacterStudio install
```

Set `MXFRAMEWORK_FBX2GLTF=/absolute/path/to/FBX2glTF` to use a custom converter binary.

## Smoke

```bash
npm --prefix Tools/MxFramework.CharacterStudio run smoke
```

The smoke script verifies the committed sample package shape and a small amount of CharacterStudio tree/path behavior.

Equivalent CLI commands behind the bridge:

```bash
dotnet run --project Tools/MxFramework.Authoring/src/MxFramework.Authoring.Cli/MxFramework.Authoring.Cli.csproj -- \
  character validate --package Tools/MxFramework.Authoring/samples/character-iron-vanguard --check-files

dotnet run --project Tools/MxFramework.Authoring/src/MxFramework.Authoring.Cli/MxFramework.Authoring.Cli.csproj -- \
  character compile --package Tools/MxFramework.Authoring/samples/character-iron-vanguard --out Temp/MxFrameworkAuthoring/CharacterStudio/compile --check-files

dotnet run --project Tools/MxFramework.Authoring/src/MxFramework.Authoring.Cli/MxFramework.Authoring.Cli.csproj -- \
  character import-unity --package Tools/MxFramework.Authoring/samples/character-iron-vanguard --project-root . --unity-root Assets/MxFrameworkGenerated/CharacterPackages --check-files
```
