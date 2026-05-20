# Camera UI 3D Validation 05

> Issue: #262 `[Camera/UI] 05：3D UI Demo Validation`
> Delivery level: Runtime Slice
> Milestone: `Phase 14: Camera Management`

## Goal

Validate one concrete UI 3D camera path in a framework runtime showcase entry. This slice uses the URP Overlay Camera path:

```text
Main Camera (URP Base)
  -> UI 3D Overlay Camera (URP Overlay)
  -> UI-layer 3D object
  -> UI Toolkit HUD remains visible above the rendered scene
```

## API Reuse Plan

| Requirement | Framework API / module | Used in this slice | Reason when not used |
| --- | --- | --- | --- |
| UI 3D overlay camera stack | `MxFramework.Camera.URP` / `MxCameraUrpOverlayStackBinder` | Used by `UiCamera3DValidationDemo` to bind and validate Base + Overlay camera stack | Not applicable |
| Unity camera presentation | `MxFramework.Camera.Unity` boundary | Demo stays in Unity-facing presentation layer and does not change camera core | No new noEngine camera authority is needed |
| UI Toolkit HUD | `UIDocument`, `PanelSettings`, UXML, USS | Used for status labels and manual validation controls | OnGUI is not used |
| RuntimeHost / command / replay / SaveState | `MxFramework.Runtime` | Not used | This is a visual camera validation slice, not a gameplay loop |
| Resources / Config | Composition root scene references | Not used | The sample objects and assets are owned by the validation scene, not a reusable runtime resource catalog |
| Diagnostics | HUD summary + Unity Console | Used as manual validation output | Debug UI source integration is covered by #263 |

## Entry

Generate or rebuild the scene through Unity:

```text
MxFramework/Camera UI/Create 3D Validation Scene
```

Open:

```text
Assets/Scenes/UiCamera3DValidation.unity
```

## Manual Validation

1. Open `Assets/Scenes/UiCamera3DValidation.unity`.
2. Enter Play Mode.
3. Confirm the world reference cube, the UI-layer 3D object, and the UI Toolkit HUD are visible.
4. Confirm the HUD reports:
   - `URP Overlay Stack Bound`
   - `Base excludes UI layer: yes`
   - `Overlay only UI layer: yes`
   - `3D UI object on UI layer: yes`
5. Click `Rebind` and confirm the stack remains valid.
6. Click `Pause Spin` / `Resume Spin` and confirm the UI 3D object responds.
7. Confirm Console has no new errors.

## Boundaries

- No WGame-specific character, level, or business data.
- No handwritten `.unity`, material, PanelSettings, prefab, or ScriptableObject YAML.
- Uses the existing project `UI` layer as the minimal validation layer; future production separation can introduce `MxUi3D` / `MxUiPreview3D` layers in a dedicated layer policy slice.
- Does not validate RenderTexture preview; #261 provides the preview slot foundation for a later demo if needed.
