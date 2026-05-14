# Combat Gameplay Bridge Interface

> Status: Issue #25 adds the noEngine ID mapping layer used by later Combat -> Gameplay hit resolution bridge work.

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

## Existing Bridge Helper

`CombatGameplayEventBridge.TryCreateAbilityEvent` converts `HitResolveResult` values into Gameplay `AbilityEvent` values for callers that already resolved runtime caster and target entities.

## Validation

EditMode coverage lives in `Assets/Scripts/MxFramework/Tests/Combat/GameplayBridge/`:

- `CombatEntityGameplayMapTests.cs`
- `CombatGameplayEventBridgeTests.cs`
