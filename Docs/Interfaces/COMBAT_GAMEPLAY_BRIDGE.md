# Combat Gameplay Bridge Interface

> Status: Current
>
> Scope: noEngine Combat -> Gameplay entity mapping, transient action-state synchronization and event translation.
>
> Code-Doc: `Packages/com.vincent.wgameframework/Combat/GameplayBridge/CombatActionStateSyncSystem.cs`

## Scope

`MxFramework.Combat.GameplayBridge` is the runtime bridge assembly between `MxFramework.Combat` and `MxFramework.Gameplay`. It does not own Combat simulation state or Gameplay component state; it provides small translation helpers at the module boundary.

## CombatEntityGameplayMap

`CombatEntityGameplayMap` is a bidirectional registry between `CombatEntityId` and `GameplayEntityId`.

Public API:

```csharp
public sealed class CombatEntityGameplayMap
{
    public void Register(CombatEntityId combatId, GameplayEntityId gameplayId);
    public bool TryGetCombatId(GameplayEntityId gameplayId, out CombatEntityId combatId);
    public bool TryGetGameplayId(CombatEntityId combatId, out GameplayEntityId gameplayId);
    public bool RemoveCombat(CombatEntityId combatId);
    public bool RemoveGameplay(GameplayEntityId gameplayId);
    public void Clear();
    public int Count { get; }
    public void CopyCombatIds(List<CombatEntityId> output);
    public CombatEntityId[] CreateCombatIdSnapshot();
    public void CopyGameplayIds(List<GameplayEntityId> output);
    public GameplayEntityId[] CreateGameplayIdSnapshot();
}
```

Behavior:

- `Register` rejects `CombatEntityId.None` using `combatId.IsNone`.
- `Register` rejects `default(GameplayEntityId)` and any non-valid gameplay id.
- Registration is one-to-one. Re-registering an existing Combat id removes the stale Gameplay reverse mapping; re-registering an existing Gameplay id removes the stale Combat forward mapping.
- `RemoveCombat`, `RemoveGameplay`, and `Clear` keep both directions synchronized.
- `CopyCombatIds`, `CreateCombatIdSnapshot`, `CopyGameplayIds`, and `CreateGameplayIdSnapshot` return active mappings in first-registration order. Overwriting an existing Combat id does not move it in the order.
- The map is not serialized and is not thread-safe.

## CombatActionStateSyncSystem

`CombatActionStateSyncSystem` synchronizes queried `CombatActionState?` values into the mapped Gameplay entities' transient `CombatActionStateComponent` store. An active value preserves the action id, phase, local frame and finished flag; `null` writes the inactive component. `SetEnabled(false)` prevents queries and writes, and an empty map does not create a component store. Query exceptions propagate to the caller (the existing `GameplaySystemPipeline` wraps system failures with its metadata).

Each enabled Tick clears and refills a system-owned ID scratch list through `CopyCombatIds` before querying. It preserves the Tick-start ID snapshot in first-registration order. A mapping added during a query is first queried on the next Tick; a removed ID is still queried if present in the Tick-start snapshot, but is skipped when its Gameplay mapping no longer exists. The Gameplay mapping is resolved after the query, so an overwritten mapping writes to the current Gameplay entity. Mapping changes between Ticks are reflected by the next copy, without reusing stale IDs.

Allocation contract: direct and `GameplaySystemPipeline` Tick paths are `NoAlloc after initialization` once the ID-list and component-store high-water capacities are warm and the caller's query delegate does not allocate. Growth to a new high-water mark can allocate and is outside that steady-state contract. Clearing the scratch list retains its capacity; buffers belong to each system instance and follow the existing serial-use model. The public `CreateCombatIdSnapshot` / `CreateGameplayIdSnapshot` APIs retain their independent-array, `AllocByDesign` semantics.

## Existing Bridge Helper

`CombatGameplayEventBridge.TryCreateAbilityEvent` converts `HitResolveResult` values into Gameplay `AbilityEvent` values for callers that already resolved runtime caster and target entities.

## Validation

EditMode coverage lives in `Assets/Scripts/WGameFrameworkProject/CharacterTest/Tests/EditMode/Combat/GameplayBridge/`:

- `CombatEntityGameplayMapTests.cs`
- `CombatActionStateSyncSystemTests.cs`: active/inactive fields, metadata, disabled/empty paths, exception propagation, registration order, mapping changes before/during Tick, and warmed 2/32-entity direct/pipeline allocation loops. High-water growth is warmed separately from measurement.
- `CombatGameplayEventBridgeTests.cs`
