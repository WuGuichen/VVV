# Item Lifecycle Showcase

> Status: Guide
>
> Version: 0.1.0 | 2026-07-08

Item Lifecycle Showcase 是物品系统专用可玩验证入口。它验证生命周期状态流转，而不是完整 RPG 背包 UI：

```text
ItemDefinition -> LootResult -> WorldItem -> InventoryStack -> EquippedItem
```

## Entry

- Scene: `Assets/Scenes/ItemLifecycleShowcase.unity`
- Runtime rules: `Assets/Scripts/WGameFrameworkProject/Demos/Items/ItemLifecycleShowcase.cs`
- Unity runner: `Assets/Scripts/WGameFrameworkProject/Demos/Items/ItemLifecycleShowcaseRunner.cs`
- UI: `Assets/UI/MxFramework/ItemLifecycle/ItemLifecycleShowcase.uxml`
- Verified icon asset: `Assets/UI/MxFramework/ItemLifecycle/Icons/iron_sword_icon.png`
- Scene generator: `MxFramework/Items/Create Item Lifecycle Showcase Scene`

## What It Uses

- `MxFramework.Items` for item definitions and stable definition hash.
- `MxFramework.Items.Loot` for deterministic grouped loot resolution.
- `MxFramework.Items.World` for active world item state, pickup, expiration, hash, and SaveState.
- `MxFramework.Items.Runtime` for slot inventory, atomic insertion, move-to-equipment, unequip back to bag, hash, and SaveState.
- `MxFramework.Items.CharacterApplicationBridge` for projecting equipment container state to legacy-compatible `WeaponConfig[]` / `EquipmentLoadoutConfig`.
- `MxFramework.Runtime` for `RuntimeHost`, command execution, deterministic frame stepping, hash, and SaveState JSON.
- `MxFramework.Resources` for resolving the Iron Sword icon from `ItemDefinition.ResourceRefs` through a demo `ResourceCatalog` / `MemoryResourceProvider`.

The showcase does not use Unity trigger/collider/physics as pickup authority. Pickup range is a pure logical grid distance check in the rules layer.

## Controls

Use the UI Toolkit buttons as the primary control path:

- `Open Loot`: resolve the demo loot table and spawn world items near the current logical position.
- `-X` / `+X` / `-Z` / `+Z`: move the logical player position by one grid cell through the same command path used by keyboard input.
- `Pickup`: pick one world item within distance 1 into the bag container.
- `Equip`: move the first equipment item from bag to the selected equipment slot, then project to Character.Application equipment.
- `Unequip`: move the selected equipment slot back to the first empty bag slot.
- Equipment slot cards: select the target equipment slot for `Equip` / `Unequip`.
- `Save` / `Load`: capture and restore inventory + world item SaveState.
- `Hash`: compare current inventory + world item hash against the last saved hash.
- `Reset`: rebuild the deterministic showcase state and seed.

Keyboard shortcuts (`O`, `Space`, `E`, `R`, `WASD` / arrow keys) are compiled only when the legacy Unity input manager is enabled. The project currently uses the Input System package, so buttons are the supported default.

## Expected Flow

1. Open the scene and enter Play.
2. Click `Open Loot`.
3. Observe `World Items` and `Loot Log`.
4. Click `Pickup` to pick the first in-range item.
5. If `Iron Sword` remains out of range, click `+X`, then click `Pickup` again.
6. Select `Main Hand` or `Off Hand` in the equipment container, then click `Equip` after the weapon is in the bag.
7. Use `Save`, then mutate state, then `Load` and `Hash` to verify restoration.

The UI should show:

- Loot result lines in `Loot Log`.
- Active world items with id, item name, count, logical position, grid distance, and in-range marker.
- Bag slots and Main Hand / Off Hand equipment container slots.
- A visible Iron Sword icon while the sword moves from world item to inventory stack to equipment container.
- Target slot state, equipment summary text after equip, and selected slot highlight.
- Stable state hash and save/compare status.
- Loot log lines that name the pickup target or explain the nearest out-of-range item.

The icon validation is intentionally narrow. Iron Sword uses the resource key
`showcase.item.iron_sword.icon` with `ResourceTypeIds.Sprite`; material items may
show a fallback icon until they receive their own sample art. Icon Sprites are
view resources only and are not written into inventory/world SaveState, runtime
hash, or network payloads.

## Boundaries

This showcase is a framework validation slice. It intentionally does not implement:

- full inventory UI drag/drop;
- Unity 3D pickup presentation;
- trading, crafting, binding, durability, random affixes;
- real consumable use effects;
- server/client gameplay flow.

Future 3D pickup validation should route authority through framework runtime or Combat/physics query services, not through Unity trigger callbacks as gameplay truth.

## Validation

Recommended focused validation after touching this showcase or the Items lifecycle code:

```bash
git diff --check
Tools/Harness/run_lightweight_checks.sh
```

Unity focused validation:

- Refresh scripts and confirm Console has no errors.
- Run EditMode tests under `MxFramework.Tests.Items`.
- Open `Assets/Scenes/ItemLifecycleShowcase.unity`, enter Play, and confirm no Console errors.
