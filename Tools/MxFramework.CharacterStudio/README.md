# MxFramework CharacterStudio

CharacterStudio is the external browser workstation for authoring Character Resource Packages. The C1 MVP is wired to the existing Authoring CLI/Core pipeline instead of reimplementing resource resolution, compilation, diagnostics, or Unity import logic in the UI.

## Run

Authoring server is required for save, compile, source model import, and Unity import. The one-command launch scripts run the server, check the local environment, and open CharacterStudio in the browser.

Recommended shared entry for all external editors:

```bash
Tools/MxFramework.EditorHub/start-editor-hub.sh
```

Windows:

```bat
Tools\MxFramework.EditorHub\start-editor-hub.bat
```

Open the Hub and click `打开角色编辑器`.

macOS:

```bash
Tools/MxFramework.CharacterStudio/start-character-studio.sh
```

Windows:

```bat
Tools\MxFramework.CharacterStudio\start-character-studio.bat
```

macOS Finder can also double-click:

```text
Tools/MxFramework.CharacterStudio/start-character-studio.command
```

Defaults:

- port: `4873`
- package: `Tools/MxFramework.Authoring/samples/character-iron-vanguard`
- URL: `http://127.0.0.1:4873/Tools/MxFramework.CharacterStudio/web/`

Optional port and package override:

```bash
Tools/MxFramework.CharacterStudio/start-character-studio.sh 4874 Tools/MxFramework.Authoring/samples/character-slime
```

```bat
Tools\MxFramework.CharacterStudio\start-character-studio.bat 4874 Tools\MxFramework.Authoring\samples\character-slime
```

The launch scripts check:

- repository root and Authoring CLI project;
- .NET 9+ SDK in `PATH`;
- selected character package and `manifest.json`;
- port availability, with a friendly message if the server is already running;
- optional CharacterStudio npm dependencies for Three.js GLB preview and FBX conversion.

Install the browser 3D / FBX dependency once when model preview or FBX conversion is needed:

```bash
npm --prefix Tools/MxFramework.CharacterStudio install
```

Manual fallback from the repository root:

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
- model import into the selected Character Resource Package plus a field-driven resource picker for selecting or clearing which imported resource replaces the body, main-hand slot, or off-hand slot;
- Three.js viewport for package GLB resources, with selectable colliders, sockets, weapon attachments, and traces;
- SVG viewport fallback when `node_modules/three` has not been installed;
- inspector draft edits for colliders, sockets, attachments, traces, and per-model wrapper position / rotation / scale;
- save through `/api/character/save`, which validates with the Authoring Core gate before writing package JSON;
- compile diagnostics through `/api/character/compile`;
- Unity import bridge through `/api/character/import-unity`.
- C2-C minimum Unity sync visibility: the workstation reads the source package, the latest Unity import report, and `Assets/MxFrameworkGenerated/CharacterPackages/<packageId>/config/unity_resource_catalog.json` when present. Field resource pickers show Unity sync status, Unity asset path, GUID, importer kind, main object type, latest import operation, and diagnostics without mutating Unity assets.

Static file preview still opens the sample package, but save, compile, and import require `editor serve`.

`导入模型` writes the selected `.glb` / `.gltf` file into the current package under `resources/models/`, updates `resource_catalog.json`, and saves through the same validation gate as normal draft edits. `角色主体模型` replaces the body model resource reference, `主手槽武器模型` / `副手槽武器模型` replace the preview model reference on the current weapon attachment slot, and `仅导入资源` only adds a catalog resource without binding it to the body or a weapon slot. CharacterStudio does not keep a full resource library browser visible in the role-editing surface; click `选择已有资源` to open a `ResourceFieldSpec`-driven picker for the current field. `清空引用` clears the current body or weapon-slot reference without deleting the imported resource, so the same resource remains in `resource_catalog.json` and can be selected again later.

CharacterStudio treats catalog resources, character references, and weapon references as separate layers. `resource_catalog.json` is the asset library; `config/character_application.json.resourceKeys` is the character's direct resource reference list; weapon attachment / generated WeaponConfig entries keep their own model references. Changing the body binding updates the character's direct resource reference; changing a weapon slot updates that weapon reference only. Neither action deletes the resource, animation, or dependency graph. Full resource creation, replacement, deletion, tagging, and catalog cleanup belong to a separate Resource Library Editor surface; CharacterStudio only consumes its list through field pickers.

The `动画配置` panel is now a CharacterStudio handoff surface, not an animation source editor. It shows the character's current animation profile slots, lets authors choose animation group/resource references through the same `ResourceFieldSpec` picker used by model fields, and links to the standalone Animation Editor in `Tools/MxFramework.AnimationEditor/web/`. Profile metadata, `applicationConfig.animationGroups[]`, Group, Clip, Blend, and Timeline Event source content are read-only in CharacterStudio; those source edits belong to the Animation Editor and its `config/animation_authoring.json` package document. Package/runtime animation choices still remain as `ResourceSelectionRef` data so the compiler can place them into `AnimationWarmup`.

Model scale and art-offset correction live on `resource_catalog.json` as `importHints.modelWrapperPose`. CharacterStudio applies that pose as a wrapper GameObject around the imported model in the 3D preview and emits it in the compiled resource mapping so the Unity importer can preserve the same wrapper transform. Those resource-level edits are transitional in CharacterStudio and should move to the independent Resource Library Editor when that surface is implemented.

`.fbx` input is accepted as an import source only: the Authoring server converts it through the local `fbx2gltf` dependency, stores the generated `.glb`, and records `sourceFormat: "glb"` in the package catalog. `导入 Unity` is a separate step that writes the compiled package outputs into the Unity project.

The toolbar intentionally separates actions by layer:

- `导入源资源` writes imported model files into the Character source package resource catalog and can bind them to the current field.
- `选择已有资源` opens a field-scoped picker; it does not expose the full resource library as a permanent character-editing panel.
- `Prefab 重建预检` runs the Character compile gate and checks the generated inputs used by the Unity prefab path.
- `Unity 导入` writes the compiled package outputs and Unity resource catalog under `Assets/MxFrameworkGenerated/CharacterPackages/<packageId>/`.
- `查看/复制报告` copies validation, compile, import, and Unity sync JSON for review.

If the Unity catalog is missing, resource cards show `未读取Catalog` or `未入Catalog`; this means the source package can still be edited, but Unity import has not produced a usable sync ledger for that resource yet.

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
