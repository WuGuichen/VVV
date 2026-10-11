# UPM-P5 to P6 Locator Transition Inventory

> Status: Current

## P5 invariant

`MxFrameworkPackageAssetLocator` is the only implementation that knows both roots:

- canonical installed root: `Packages/com.vincent.wgameframework`;
- development fallback: `Assets/Scripts/MxFramework`.

Direct callers are:

- `Editor/Levels/MxLevelEditorWindow.cs`;
- `Editor/ProjectileAuthoring/ProjectileAuthoringWindow.cs`;
- `Editor/VfxAuthoringResourceLibraryWindow.cs`;
- `Editor/CharacterAction/CharacterActionAuthoringWindow.Chrome.cs`;
- `Editor/CharacterAction/Timeline/CharacterActionTimelineView.cs`;
- `Editor/MxAttachmentTransformField.cs`;
- `Editor/MxEditorUtils.cs`;
- `Editor/CharacterAction/CharacterActionReferenceDiscoveryService.cs`.

Package source search confirms there is no other `Assets/Scripts/MxFramework` literal under `Editor/`.

## P6 required updates

P6 must delete `DevelopmentAssetRoot` and the fallback branch, while preserving all callers as canonical-only users. It must also update or retire:

- Consumer runner Git `?path=/Assets/Scripts/MxFramework` and Local source root;
- Consumer fixture manifest/lock source;
- boundary checker and inventory generator default package roots;
- package README, multi-project workflow and Harness documentation;
- current P2-P5 generator defaults that still operate on the transition root.

Historical evidence retains old paths as history and is not rewritten. P6 must add a static assertion that no live code, runner, current doc or fixture contains the development fallback.
