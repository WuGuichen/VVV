# Issue 68 FMOD Bank Resource Policy

> Status: Implemented documentation policy
> Scope: FMOD bank, StreamingAssets mirror, FMODStudioCache, AudioEventDefinition, and ordinary Resource Catalog boundary.
> Gitea: #68

## Decision Summary

- `FMOD/MxFrameworkAudioDemo/MxFrameworkAudioDemo/Build/Desktop/Master.bank` and `Master.strings.bank` are FMOD Studio demo build outputs. For the framework demo they are versioned authoring output / release input, not ordinary Resource Catalog resources.
- `Assets/StreamingAssets/Master.bank` and `Master.strings.bank` are the Unity runtime mirror loaded by FMOD Unity Integration and `MxFramework.Audio.FMOD`. They are versioned runtime assets for the current demo setup.
- Build/Desktop banks and StreamingAssets banks should be refreshed and reviewed together. A bank content change should check both locations, FMOD settings/cache, and `FmodAudioSetupValidator`.
- `Assets/Plugins/FMOD/Cache/Editor/FMODStudioCache.asset` remains tracked as versioned generated metadata. It records Editor-visible bank, event, guid, and path data after FMOD bank refresh. Do not hand-edit it as authoritative config.
- Current `.gitignore` policy is accepted: keep `FMOD/.../Build/**`, ignore FMOD Studio machine caches under `.cache/buildrecords/` and `.cache/fsbcache/`, and do not ignore `Assets/Plugins/FMOD/Cache/Editor/FMODStudioCache.asset`.
- FMOD bank `.bank` files are not `ResourceCatalogEntry` items in #63-#67. They must not use `ResourceTypeIds.AudioClip`.
- FMOD event paths and GUIDs are not generic `ResourceKey` values. Runtime/config references should use `AudioEventDefinition.Id`, `FmodEventGuid`, and `FmodEventPath`.
- `Assets/_TempImportedResources/Audio/MagicEffects/**` are Unity `AudioClip` resource samples. They may become ordinary `ResourceTypeIds.AudioClip` Catalog entries after #64/#65, but they are not FMOD Studio source audio in this issue.

## Current Repository State

Tracked FMOD bank and metadata files:

```text
FMOD/MxFrameworkAudioDemo/MxFrameworkAudioDemo/Build/Desktop/Master.bank
FMOD/MxFrameworkAudioDemo/MxFrameworkAudioDemo/Build/Desktop/Master.strings.bank
Assets/StreamingAssets/Master.bank
Assets/StreamingAssets/Master.strings.bank
Assets/Plugins/FMOD/Resources/FMODStudioSettings.asset
Assets/Plugins/FMOD/Cache/Editor/FMODStudioCache.asset
```

Observed settings/cache state:

- `FMODStudioSettings.asset` uses source project `FMOD/MxFrameworkAudioDemo/MxFrameworkAudioDemo/MxFrameworkAudioDemo.fspro`.
- Source bank path is `FMOD/MxFrameworkAudioDemo/MxFrameworkAudioDemo/Build`.
- Import type is StreamingAssets, with `TargetBankFolder` empty, so runtime banks resolve under `Assets/StreamingAssets`.
- `MasterBanks` contains `Master`.
- `FMODStudioCache.asset` records `bank:/Master`, `bank:/Master.strings`, `event:/MxFramework/Demo/OneShot`, and `event:/MxFramework/Demo/Loop`.
- The cache contains repeated generated bank/event objects, but the primary serialized lists point at current Master bank and strings bank metadata. This is accepted as FMOD-generated metadata unless it causes concrete editor failures.

## Validation Boundary

`FmodAudioSetupValidator` owns FMOD setup validation:

- FMOD Settings exists.
- Runtime bank root resolves.
- At least one `.bank` exists under the runtime root.
- `Master.bank` and `Master.strings.bank` exist.
- `BankLoadType.Specified` has explicit banks.
- `BankLoadType.All` warns when settings cache lists are empty.

Ordinary Resource Catalog validation owns Unity resources:

- Unity `AudioClip` samples such as MagicEffects.
- UI textures, VFX prefabs, art prefabs/models, AssetBundles, RemoteBundles, and their provider data.
- Catalog shape, provider address, labels, dependencies, and direct/dependency-only rules.

The two validators should not reinterpret each other's data. Catalog validation should not parse `.bank` files, and FMOD validation should not require a generic `ResourceCatalogEntry` for bank or event paths.

## Future Work

Create a separate S2 issue before adding any of these:

- `ResourceTypeIds.FmodBank`, `ResourceTypeIds.AudioEvent`, or `ResourceTypeIds.FmodEventManifest`.
- `FmodBankProvider` or Mod/DLC bank loading.
- Deterministic bank copy/build tooling between FMOD Build/Desktop and `Assets/StreamingAssets`.
- A policy change that ignores `FMODStudioCache.asset` or treats it as local-only cache.

Until that issue exists, #63-#67 ordinary Resource Catalog work must not add `.bank` files, FMOD event paths, or FMOD GUIDs as normal sample resource entries.
