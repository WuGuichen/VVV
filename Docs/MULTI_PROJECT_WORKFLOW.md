# Multi-Project Workflow

> Status: Guide

`WGameFramework` is the reusable framework package for WGame-family Unity projects. Game-specific implementation projects should consume it as a Unity Package Manager dependency instead of copying framework code. Current package identity, evidence freshness and readiness gaps are maintained only in `UPM_CURRENT_STATUS.md`.

> Consumption rule: use a local `file:` reference for development or an explicitly reviewed immutable commit for shared baselines. Do not treat the moving `main` branch as a stable release channel. Read the exact current package version, reviewed baseline SHA, test matrix and missing gates from `UPM_CURRENT_STATUS.md`; use `UPM_OPTIMIZATION_ROADMAP.md` for the migration order.

## Local Consumer Layout

```text
E:/Unity/WxFramework/
  WGameFramework/
  WGame_B/
  WGame_B_Unity/
```

`WGame_B_Unity/Packages/manifest.json` should reference this repository from its `Packages` directory:

```json
"com.vincent.wgameframework": "file:../../WGameFramework/Packages/com.vincent.wgameframework"
```

This local reference remains the normal development path when the framework and game repositories are checked out side by side.

This is the transitional path. After the accepted Embedded Package cutover, local consumers must migrate to the new package root, for example:

```json
"com.vincent.wgameframework": "file:../../WGameFramework/Packages/com.vincent.wgameframework"
```

The cutover is a preview breaking change and must ship with a migration note; do not switch consumers before the package move is committed and its Local/PinnedGit matrix passes.

For a reviewed remote candidate that is already reachable from Gitea, use an immutable full commit SHA:

```json
"com.vincent.wgameframework": "git+<canonical-repository-url>?path=/Packages/com.vincent.wgameframework#<full-40-character-commit-sha>"
```

The repository authority is not yet frozen for release metadata. Use the reviewed baseline's approved source or a credential-free `-GitUrl` execution override; do not copy a machine-private address into release documentation. After Embedded cutover, the Git subpath becomes `/Packages/com.vincent.wgameframework`.

The `path` query must appear before the `#revision`. Validate the exact candidate from the framework repository before adopting it:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File Tools/Harness/run_upm_consumer_smoke.ps1 `
  -PackageSource PinnedGit `
  -Revision <full-40-character-commit-sha>
```

The command is a release-boundary check, not the daily inner loop: Git UPM must fetch the repository commit before extracting the package subdirectory, so a clean first import is materially slower than local `file:` consumption. A commit is eligible only when the clean report records `sourceMode=pinned-git`, `dependencyResolution.source=git`, identical requested/resolved revisions, the observed package version, zero compile/Editor log errors and a non-zero passing test count.

The runner creates every disposable test project outside the repository under `C:\WGameFrameworkConsumerSmokeRuns\<Local|PinnedGit>\<profile>\`. Each project owns its `Assets`, `Packages`, `ProjectSettings`, and clean `Library`; the tracked fixture is only copied source. `C:\WGameFrameworkConsumerSmoke` is the long-lived local-source consumer, while `C:\WGameFrameworkConsumerRemote` is the long-lived immutable-remote consumer; neither is gate output. `-ConsumerRoot` may override the generated-run location, but both long-lived projects, repository-contained paths, and overly broad roots are rejected before Unity starts.

For a package-boundary change that affects an optional adapter, rerun the matching positive profile locally and against the immutable candidate:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File Tools/Harness/run_upm_consumer_smoke.ps1 `
  -Profile with-fairygui

powershell.exe -NoProfile -ExecutionPolicy Bypass -File Tools/Harness/run_upm_consumer_smoke.ps1 `
  -PackageSource PinnedGit `
  -Revision <full-40-character-commit-sha> `
  -Profile with-fairygui
```

The other supported positive profiles are `with-unity-transport` and `with-vfx-graph`. The pinned FairyGUI profile resolves both repository package subpaths from the same full SHA. FMOD remains a separate project-owned gate. `Assets/Plugins/FMOD` has local package metadata, but it is not an approved optional source until its dependencies, license, LFS retrieval and clean Consumer profile are verified.

A tag-based Git reference becomes a supported release path only after the corresponding tag, package version and release report pass the same pinned Consumer Smoke. The consumer must not infer package stability from the current metadata version or from a moving branch.

## Repository Boundary

Keep reusable code here:

- `MxFramework` runtime modules
- generic Unity adapters
- portable Editor and approved optional adapters
- package-owned contract tests and self-contained `Samples~`
- framework documentation

Keep project validation and game-specific code outside the package root:

- WGameFramework repository-owned CharacterTest, integration tests and project-only authoring tools
- `Assets/Scripts/WGame`
- WGame_B rooms, creatures, UI, config, and scenes
- playtest fixtures and milestone-specific content

## Branch Routing

Use framework branches for reusable gaps:

- `feature/runtime-ai-decision-trace`
- `feature/combat-sensor-query`
- `fix/camera-follow-lag`

Use game repository branches for WGame_B milestones:

- `milestone/m0-sensory-sandbox`
- `milestone/m1-fog-vision`
- `milestone/m2-personality-hesitation`

Do not fork this repository for a game unless that game intentionally diverges from the shared framework roadmap.

## External Tools As Shared Editors

`Tools/` is shared framework-owned editor source. Game projects should not copy it.

Run the Authoring server with separate roots when editing another Unity project:

```bash
dotnet run --project WGameFramework/Tools/MxFramework.Authoring/src/MxFramework.Authoring.Cli/MxFramework.Authoring.Cli.csproj -- editor serve \
  --tools-root E:/Unity/WxFramework/WGameFramework \
  --project-root E:/Unity/WxFramework/WGame_B_Unity \
  --port 4973 \
  --package Authoring/CharacterPackages/wgame-b-prototype
```

Root meanings:

- `--tools-root`: framework repo that contains `Tools/`, web editors, samples, and shared schemas.
- `--project-root`: Unity project whose `Assets/`, `Temp/`, generated files, config, and runtime catalogs are edited.
- `--package`: project-owned authoring package when editing a game project. Framework sample packages are templates/reference content, not default game data.
- `--root`: compatibility shortcut that sets both roots to the same directory.

Recommended port blocks:

| Project | Ports |
| --- | --- |
| `WGameFramework` | `4873-4899` |
| `WGame_B_Unity` | `4973-4999` |

Each game project should own its FairyGUI project and publish path. For `WGame_B_Unity`, use:

```text
WGame_B_Unity/Authoring/CharacterPackages
WGame_B_Unity/FGUIProject
WGame_B_Unity/Assets/Bundles/FGUI/{publish_file_name}
```

## Unity MCP Shared Server Lifecycle

Multiple Unity Editors may register as separate plugin sessions on the same local MCP HTTP server. The server on `127.0.0.1:8080` is a shared hub; it is not owned by the lifetime of any single registered Unity project.

- Opening a project registers that Editor as a distinct MCP instance.
- Closing a project stops only that project's transports and unregisters its plugin session.
- Editor shutdown does not stop the shared HTTP server.
- Stop the server explicitly from `Window/MCP for Unity/Toggle MCP Window` when no project needs it.
- PID and launch-handshake tracking is scoped by normalized Unity project root so one project cannot claim or clear another project's server ownership state.
- Legacy unscoped ownership keys are discarded and are never accepted as shutdown authority.

Before routing an MCP command, list the active instances, select the exact `ProjectName@instance-id`, and verify its project path and editor readiness. A disconnected project must disappear from the instance list without affecting the remaining projects.
