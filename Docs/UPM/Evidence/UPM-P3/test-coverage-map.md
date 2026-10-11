# UPM-P3 Test Coverage Map

> Status: Current
>
> Phase state: inventory/classification freeze

## Classification decision

All ten existing package-root test assemblies move intact to `Assets/Scripts/WGameFrameworkProject/Tests/Repository/`. Their assembly names, asmdef GUIDs, source files, fixtures and assertions remain unchanged. No existing test is deleted, merged, ignored or replaced.

| Existing assembly | New owner | Preserved contract |
| --- | --- | --- |
| `MCPForUnityTests.EditMode` | repository-validation | MCP plugin PID and validate-scripts integration |
| `MxFramework.Tests.Application.Unity.Editor` | repository-validation | Application PlayMode fixture generation/validation support |
| `MxFramework.Tests.Application.SessionBridge.Unity.Editor` | repository-validation | Candidate-scene fixture generation/validation support |
| `MxFramework.Tests.Persistence.RuntimeBridge` | repository-validation | Full persistence/runtime bridge contract and service regression |
| `MxFramework.Tests.PlayMode.Application` | repository-validation | Application boot/candidate-scene PlayMode coverage |
| `MxFramework.Tests.PlayMode.Audio` | repository-validation | FMOD catalog/bank PlayMode coverage |
| `MxFramework.Tests.PlayMode.GameplayTriggers` | repository-validation | Demo scene, Unity physics and Input Test Framework coverage |
| `MxFramework.Tests.PlayMode.Resources` | repository-validation | CharacterTestScene resource-flow coverage |
| `MxFramework.Tests.PlayMode.Vfx` | repository-validation | Extended ParticleSystem lifecycle/baked-track coverage |
| `MxFramework.Tests.UI.FairyGUI` | repository-validation | FairyGUI package, Bundles, Demo and scene integration coverage |

The exact source list and GUID identity are frozen in `test-surface-manifest.json`.

## Minimal package contract successors

The package gains four new tests; these are not substitutes for the repository suite. They define the small contract that every `testables` Consumer must execute:

1. RuntimeHost start/tick/stop lifecycle ordering.
2. RuntimeCommand typed envelope roundtrip through the command buffer.
3. Representative Editor stylesheet resolution/load in both development and installed-package layouts.
4. Rejection of absolute and parent-traversal package asset paths.

Consumer-owned smoke tests continue to cover absent project data, Level Editor/Projectile windows, representative GUIDs and optional adapters. P3 must run both the four package-owned contracts and the existing Consumer-owned exact full names.

## Coverage conservation

- Existing versioned test sources before move: 31.
- Existing test attributes before move: 183.
- Approved deletions: 0.
- Existing assembly identity changes: 0 across 10 assemblies.
- New package contract tests: 4.

Completion requires the manifest to report every existing source at its target path, the repository assemblies to compile/run from the project-owned root, and all four new package tests to run in a real Consumer with explicit `testables` and Test Framework ownership.

## WGame palette follow-up ownership correction

`Packages/com.vincent.wgameframework/Tests/Editor/Rendering/WGamePaletteBindingTests.cs`
and its existing `.meta` move intact to
`Assets/Scripts/WGameFrameworkProject/CharacterTest/Tests/EditMode/Rendering/` under
the existing `MxFramework.Tests` repository-validation assembly. This fixture
requires WGame shader sources, project ShaderGUI, Art/ArtSource catalogs and
generated texture/importer contracts. Consumer does not import those project
resources. Every original test, assertion, namespace and source/meta byte is
preserved; no test is ignored or deleted. Both previously passing palette cases
move with the 14 failed cases.

Package tests additionally exercise palette automatic discovery with absent
source and the four output-presence combinations using temporary plain files;
they restore the Editor callback list and never execute generation. Explicit
generation validation remains unchanged. These contracts complement, rather
than replace, the repository palette integration fixture.

Candidate status: strict E0/offline evidence is recorded separately; repository
native palette fixture and Local Consumer validation remain required before
claiming this follow-up complete. Historical P3 execution records above remain
historical evidence and do not validate this candidate.
