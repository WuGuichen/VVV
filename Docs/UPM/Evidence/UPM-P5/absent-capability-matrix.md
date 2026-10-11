# UPM-P5 Absent Capability Matrix

> Status: Current
>
> Evidence level: Local E1, non-promotable

| Capability absent in base Consumer | Expected behavior | Evidence |
| --- | --- | --- |
| repository `Tools/` | Projectile window reports unavailable; no CLI launch | `ProjectileAuthoring_WithoutRepositoryTools_ReportsUnavailable` |
| `FGUIProject/` | project-only FairyGUI catalog window type is not installed | `FairyGuiCatalog_ProjectOnlyWindowIsNotInstalled` |
| `Assets/Config` / VFX authoring catalog | VFX window creates its core tree and reports unavailable without exception or `LogError` | `VfxAuthoring_CreateGuiWithoutCatalog_ReportsUnavailable` |
| repository Scene/VFX/Bundles roots | fixture asserts all forbidden roots are absent | `Fixture_ExcludesProjectOnlyEditorInputs` |
| FairyGUI, Unity Transport and VFX Graph optional packages | base lock and compiled DLL closure remain absent | runner package-lock and optional assembly checks |
| FMOD source/plugin | `MxFramework.Audio.FMOD` and `.Editor` DLLs remain absent in every Consumer profile | runner optional assembly checks |
| uGUI | portable hierarchy locator has no static source dependency | strict E0 optional-source boundary `0/0` |

Local results:

| Profile | Tests | `report.json` SHA-256 |
| --- | ---: | --- |
| base | 14/14 | `5711acc06f863e366c860cbcfd4be251b9a616bc87d08e8c8f69ab67467ef06c` |
| FairyGUI | 15/15 | `88845732910eae1c2ac71f72f4c1fddac05f4aea946d10b22b1ccb85cdd601ac` |
| Unity Transport | 15/15 | `59c1dee45092cc3618b923ee98efdd8d90c1ac65674c0c030014a85185c96ed8` |
| VFX Graph | 15/15 | `a7902a2408ec3142f221e5f9030d488eb38ec6b8f4c7bede02d2664e1ba8239a` |

All four rows share Local package tree `71bfe0a66e924ff24c35f44c58cfcb264d4a0b14d6ab991e2ede2257aa7bc7a4`. These reports do not replace the required remote full-SHA PinnedGit matrix.
