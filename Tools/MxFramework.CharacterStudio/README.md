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
- Three.js viewport for package GLB resources, with selectable colliders, sockets, weapon attachments, and traces;
- SVG viewport fallback when `node_modules/three` has not been installed;
- inspector draft edits for colliders, sockets, attachments, and traces;
- save through `/api/character/save`, which validates with the Authoring Core gate before writing package JSON;
- compile diagnostics through `/api/character/compile`;
- Unity import bridge through `/api/character/import-unity`.

Static file preview still opens the sample package, but save, compile, and import require `editor serve`.

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
