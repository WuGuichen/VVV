using System;
using System.Collections.Generic;
using MxFramework.CharacterApplication;
using MxFramework.CharacterRuntimeSpawn;
using MxFramework.Combat.Core;
using MxFramework.Combat.GameplayBridge;
using MxFramework.Gameplay;

namespace MxFramework.Game.CharacterTest
{
    public sealed partial class CharacterTestCharacterRuntimeWorld
    {
        private readonly List<CharacterTestCharacterEntity> _entities =
            new List<CharacterTestCharacterEntity>();
        private readonly List<CharacterTestMovableNaturalObjectState> _movableNaturalObjects =
            new List<CharacterTestMovableNaturalObjectState>();
        private readonly List<CharacterTestMovableNaturalObjectSpawnSpec> _movableNaturalObjectSpawnSpecs =
            new List<CharacterTestMovableNaturalObjectSpawnSpec>();
        private readonly CharacterTestComponentWorld _characterComponentWorld =
            new CharacterTestComponentWorld();
        private readonly GameplayComponentWorld _gameplayComponentWorld =
            new GameplayComponentWorld();
        private readonly CombatEntityGameplayMap _combatGameplayMap =
            new CombatEntityGameplayMap();
        private int _nextEntityId = 1;

        /// <summary>
        /// Low-frequency composition seam for fixed-capacity runtime registration owned by outer systems.
        /// </summary>
        /// <remarks>
        /// LowFreqAlloc observers may allocate their per-entity state here. They must not defer that work to Tick.
        /// </remarks>
        internal event Action<CharacterTestCharacterEntity> EntitySpawned;

        /// <summary>
        /// Low-frequency composition seam for releasing fixed-capacity runtime registration owned by outer systems.
        /// </summary>
        /// <remarks>
        /// NoAlloc. The entity has already left the world's active entity list when observers are invoked.
        /// </remarks>
        internal event Action<CharacterTestCharacterEntityId> EntityRemoved;

        /// <summary>
        /// Low-frequency composition seam for clearing fixed-capacity runtime registration owned by outer systems.
        /// </summary>
        /// <remarks>NoAlloc. Observers remain subscribed so the world can be populated again after Clear.</remarks>
        internal event Action EntitiesCleared;

        public IReadOnlyList<CharacterTestCharacterEntity> Entities => _entities;
        private readonly Dictionary<int, TacticalCard.IMomentumLedger> _tacticalMotion = new();
        internal void BindTacticalMotion(int entityId, TacticalCard.IMomentumLedger ledger)
        {
            if (entityId <= 0 || ledger == null) throw new ArgumentException("A live actor and motion ledger are required.");
            _tacticalMotion[entityId] = ledger;
        }
        internal bool TryGetTacticalMotion(int entityId, out TacticalCard.IMomentumLedger ledger) => _tacticalMotion.TryGetValue(entityId, out ledger);
        public IReadOnlyList<CharacterTestCharacterEntityId> EntityIds => _characterComponentWorld.EntityIds;
        public IReadOnlyList<CharacterTestMovableNaturalObjectState> MovableNaturalObjects =>
            _movableNaturalObjects;
        public GameplayComponentWorld GameplayComponentWorld => _gameplayComponentWorld;
        public CombatEntityGameplayMap CombatGameplayMap => _combatGameplayMap;
        internal CharacterTestComponentWorld CharacterComponents => _characterComponentWorld;

        public CharacterTestCharacterEntity Spawn(
            CharacterTestCharacterSpawnRequest request,
            CharacterTestCharacterDefinition definition)
        {
            return Spawn(request, definition, CharacterTestGeneratedPackageSource.LoadPackage(definition.GeneratedPackageRoot));
        }

        internal CharacterTestCharacterEntity Spawn(
            CharacterTestCharacterSpawnRequest request,
            CharacterTestCharacterDefinition definition,
            CharacterImportedPackage package)
        {
            GameplayEntityId gameplayEntityId = _gameplayComponentWorld.CreateEntity();
            var entity = new CharacterTestCharacterEntity(
                _characterComponentWorld,
                new CharacterTestCharacterEntityId(_nextEntityId++),
                new CharacterTestCharacterIdentityComponent(
                    definition.CharacterId,
                    definition.DisplayName,
                    definition.PrefabKey,
                    definition.AnimationBinding,
                    definition.GeneratedPackageRoot,
                    ResolveCharacterStableId(definition)),
                new CharacterTestCharacterTeamComponent(
                    request.TeamId,
                    definition.DefaultTeamId),
                CharacterTestCharacterTransformComponent.FromDeterministic(
                    request.XMillimeters,
                    request.YMillimeters,
                    request.ZMillimeters),
                new CharacterTestCharacterEquipmentComponent(
                    useDefaultLoadout: true,
                    ResolveDefaultEquipmentStateStableId(package)),
                new CharacterTestCharacterControlComponent(
                    request.ControlOwner,
                    request.RuntimeAiPlannerProfileSlot,
                    request.RuntimeAiBehaviorSelection),
                gameplayEntityId,
                definition.PhysicsProfile);

            entity.ReinitializeBodyPartState(package?.Geometry);
            _entities.Add(entity);
            RegisterGameplayState(entity, package);
            SyncCombatStateHealth(entity);
            EntitySpawned?.Invoke(entity);
            return entity;
        }

        internal static string ResolveCharacterStableId(CharacterTestCharacterDefinition definition)
        {
            string packageId = definition.PrefabKey.PackageId;
            if (definition.CharacterId == CharacterTestCharacterDefinitionRegistry.DefaultPlayableCharacterId)
                packageId = CharacterTestCharacterSpawnResources.DefaultPlayablePackageId;
            return string.IsNullOrWhiteSpace(packageId)
                ? string.Empty
                : "char." + packageId.Trim();
        }

        internal static string ResolveDefaultEquipmentStateStableId(
            CharacterImportedPackage package)
        {
            CharacterImportedConfigSet configs = package?.Configs;
            CharacterConfig character = configs?.Characters != null && configs.Characters.Length > 0
                ? configs.Characters[0]
                : null;
            EquipmentLoadoutConfig loadout = character == null
                ? null
                : configs.FindLoadout(character.DefaultLoadoutId);
            EquipmentStateConfig[] states = configs?.EquipmentStates;
            if (states == null || states.Length == 0)
                return string.Empty;

            if (loadout != null && loadout.EquipmentStateId.IsValid)
            {
                for (int i = 0; i < states.Length; i++)
                {
                    if (states[i] != null && states[i].StateId.Equals(loadout.EquipmentStateId))
                        return states[i].StableId ?? string.Empty;
                }
            }

            EquipmentStateConfig selected = states[0];
            for (int i = 1; i < states.Length; i++)
            {
                if (states[i] != null && (selected == null || states[i].Priority > selected.Priority))
                    selected = states[i];
            }

            return selected?.StableId ?? string.Empty;
        }

        public bool Remove(CharacterTestCharacterEntityId id)
        {
            for (int i = 0; i < _entities.Count; i++)
            {
                if (_entities[i].Id.Value != id.Value)
                    continue;

                CharacterTestCharacterEntity entity = _entities[i];
                GameplayEntityId gameplayEntityId = entity.GameplayEntityId;
                _entities.RemoveAt(i);
                _combatGameplayMap.RemoveGameplay(gameplayEntityId);
                _gameplayComponentWorld.DestroyEntity(gameplayEntityId);
                _characterComponentWorld.Remove(id);
                EntityRemoved?.Invoke(id);
                return true;
            }

            return false;
        }

        internal CharacterTestCharacterEntity FindEntity(int entityId)
        {
            if (entityId <= 0)
                return null;

            for (int i = 0; i < _entities.Count; i++)
            {
                CharacterTestCharacterEntity entity = _entities[i];
                if (entity.Id.Value == entityId)
                    return entity;
            }

            return null;
        }

        public void Clear()
        {
            _tacticalMotion.Clear();
            _entities.Clear();
            _characterComponentWorld.Clear();
            _movableNaturalObjects.Clear();
            _movableNaturalObjectSpawnSpecs.Clear();
            _nextEntityId = 1;
            _combatGameplayMap.Clear();
            _gameplayComponentWorld.Clear();
            EntitiesCleared?.Invoke();
        }

        internal bool TryGetComponent<T>(
            CharacterTestCharacterEntityId entityId,
            out T component)
        {
            return _characterComponentWorld.TryGetComponent(entityId, out component);
        }

        internal bool SetComponent<T>(
            CharacterTestCharacterEntityId entityId,
            T component)
        {
            return _characterComponentWorld.SetComponent(entityId, component);
        }

        internal void SetBattleState(
            CharacterTestCharacterEntityId entityId,
            CharacterTestBattleStateComponent battleState)
        {
            _characterComponentWorld.SetBattleState(entityId, battleState);
        }

        internal void SetPresence(
            CharacterTestCharacterEntityId entityId,
            CharacterTestEntityPresenceComponent presence)
        {
            _characterComponentWorld.SetPresence(entityId, presence);
        }

        internal CharacterTestCharacterRequestBufferComponent GetMutableRequestBuffer(
            CharacterTestCharacterEntityId entityId)
        {
            return _characterComponentWorld.GetMutableRequestBuffer(entityId);
        }

        public void SetMovableNaturalObjectSpawnSpecs(
            IReadOnlyList<CharacterTestMovableNaturalObjectSpawnSpec> specs)
        {
            _movableNaturalObjects.Clear();
            _movableNaturalObjectSpawnSpecs.Clear();
            if (specs == null || specs.Count == 0)
                return;

            foreach (CharacterTestMovableNaturalObjectSpawnSpec spec in NormalizeMovableNaturalObjectSpecs(specs))
            {
                _movableNaturalObjectSpawnSpecs.Add(spec);
                _movableNaturalObjects.Add(new CharacterTestMovableNaturalObjectState(spec));
            }
        }

        public int ResetMovableNaturalObjects(int numericId = 0)
        {
            if (numericId <= 0)
            {
                for (int i = 0; i < _movableNaturalObjects.Count; i++)
                    _movableNaturalObjects[i].Reset();
                return _movableNaturalObjects.Count;
            }

            for (int i = 0; i < _movableNaturalObjects.Count; i++)
            {
                if (_movableNaturalObjects[i].NumericId != numericId)
                    continue;

                _movableNaturalObjects[i].Reset();
                return 1;
            }

            return 0;
        }

        public bool ApplyMovableNaturalObjectPotencyInfluence(
            int numericId,
            CharacterTestGenesisPotencyInfluenceKind influenceKind,
            MxFramework.Runtime.RuntimeFrame frame,
            out int affectedCount,
            out string message)
        {
            affectedCount = 0;
            CharacterTestGenesisPotencyInfluenceKind normalizedInfluence =
                CharacterTestGenesisPotencyRuntime.NormalizeInfluenceKind(influenceKind);
            if (!CharacterTestGenesisPotencyRuntime.IsCommandInfluenceKind(normalizedInfluence))
            {
                message = "movable natural object potency influence is invalid";
                return false;
            }

            if (numericId <= 0)
            {
                for (int i = 0; i < _movableNaturalObjects.Count; i++)
                {
                    _movableNaturalObjects[i].ApplyPotencyInfluence(normalizedInfluence, frame);
                    affectedCount++;
                }

                message = affectedCount > 0
                    ? "movable natural object potency influence applied. count=" + affectedCount
                    : "no movable natural objects are registered";
                return affectedCount > 0;
            }

            for (int i = 0; i < _movableNaturalObjects.Count; i++)
            {
                if (_movableNaturalObjects[i].NumericId != numericId)
                    continue;

                _movableNaturalObjects[i].ApplyPotencyInfluence(normalizedInfluence, frame);
                affectedCount = 1;
                message = "movable natural object potency influence applied. target=" + numericId;
                return true;
            }

            message = "movable natural object target was not found: " + numericId;
            return false;
        }

        public bool TryFindMovableNaturalObject(
            int numericId,
            out CharacterTestMovableNaturalObjectState state)
        {
            for (int i = 0; i < _movableNaturalObjects.Count; i++)
            {
                CharacterTestMovableNaturalObjectState candidate = _movableNaturalObjects[i];
                if (candidate.NumericId != numericId)
                    continue;

                state = candidate;
                return true;
            }

            state = null;
            return false;
        }

        public CharacterTestMovableNaturalObjectSnapshot[] CaptureMovableNaturalObjectSnapshots()
        {
            if (_movableNaturalObjects.Count == 0)
                return new CharacterTestMovableNaturalObjectSnapshot[0];

            var snapshots = new CharacterTestMovableNaturalObjectSnapshot[_movableNaturalObjects.Count];
            for (int i = 0; i < _movableNaturalObjects.Count; i++)
                snapshots[i] = _movableNaturalObjects[i].CaptureSnapshot();
            return snapshots;
        }

        public bool ApplyMovableNaturalObjectSnapshots(
            IReadOnlyList<CharacterTestMovableNaturalObjectSnapshot> snapshots,
            out string message)
        {
            message = string.Empty;
            _movableNaturalObjects.Clear();
            _movableNaturalObjectSpawnSpecs.Clear();
            if (snapshots == null || snapshots.Count == 0)
                return true;

            var seenNumericIds = new HashSet<int>();
            var seenStableIds = new HashSet<string>();
            CharacterTestMovableNaturalObjectSnapshot[] ordered =
                CreateOrderedMovableNaturalObjectSnapshots(snapshots);
            for (int i = 0; i < ordered.Length; i++)
            {
                CharacterTestMovableNaturalObjectSnapshot snapshot = ordered[i];
                if (snapshot.NumericId <= 0)
                {
                    message = "Movable natural object snapshot numeric id is invalid.";
                    return false;
                }

                string stableId = snapshot.StableId ?? string.Empty;
                if (!seenNumericIds.Add(snapshot.NumericId) || !seenStableIds.Add(stableId))
                {
                    message = "Movable natural object snapshot ids must be unique.";
                    return false;
                }

                var spec = new CharacterTestMovableNaturalObjectSpawnSpec(
                    stableId,
                    snapshot.NumericId,
                    snapshot.Category,
                    snapshot.MassGrams,
                    snapshot.CreateSurfaceMaterial(),
                    snapshot.HalfExtentXMillimeters,
                    snapshot.HalfExtentYMillimeters,
                    snapshot.HalfExtentZMillimeters,
                    snapshot.XMillimeters,
                    snapshot.YMillimeters,
                    snapshot.ZMillimeters,
                    CharacterTestMovableNaturalObjectSpawnSpec.NormalizeShapeKind(
                        (CharacterTestMovableNaturalObjectShapeKind)snapshot.ShapeKind),
                    snapshot.CreateBasePotency(),
                    snapshot.CreatePotencyProfile());
                var state = new CharacterTestMovableNaturalObjectState(spec);
                state.ApplySnapshot(snapshot);
                _movableNaturalObjectSpawnSpecs.Add(spec);
                _movableNaturalObjects.Add(state);
            }

            return true;
        }

        public int GetCurrentAttribute(
            CharacterTestCharacterEntity entity,
            int attributeId,
            bool includeModifiers = false)
        {
            if (entity == null || attributeId <= 0)
                return 0;

            return GetCurrentAttribute(entity.GameplayEntityId, attributeId, includeModifiers);
        }

        public int GetCurrentAttribute(
            GameplayEntityId entityId,
            int attributeId,
            bool includeModifiers = false)
        {
            return GameplayComponentAttributeQuery.GetCurrentValueOrDefault(
                _gameplayComponentWorld,
                entityId,
                attributeId,
                includeModifiers);
        }

        public bool TryGetScatterPressure(
            CharacterTestCharacterEntity entity,
            out GameplayScatterPressureComponent scatterPressure)
        {
            if (entity == null)
            {
                scatterPressure = default;
                return false;
            }

            return TryGetScatterPressure(entity.GameplayEntityId, out scatterPressure);
        }

        public bool TryGetScatterPressure(
            GameplayEntityId entityId,
            out GameplayScatterPressureComponent scatterPressure)
        {
            if (!_gameplayComponentWorld.TryGetStore(out GameplayComponentStore<GameplayScatterPressureComponent> store)
                || !store.TryGet(entityId, out scatterPressure))
            {
                scatterPressure = default;
                return false;
            }

            return true;
        }

        public void SetCurrentAttribute(GameplayEntityId entityId, int attributeId, int currentValue)
        {
            if (!entityId.IsValid || attributeId <= 0)
                return;

            GameplayComponentStore<GameplayAttributeSetComponent> store =
                _gameplayComponentWorld.GetOrCreateStore<GameplayAttributeSetComponent>();
            store.TryGet(entityId, out GameplayAttributeSetComponent attributes);
            store.Set(entityId, attributes.SetCurrentValue(attributeId, currentValue));
        }

        public void ClearScatterPressure(CharacterTestCharacterEntity entity, long frame)
        {
            if (entity == null
                || !_gameplayComponentWorld.TryGetStore(out GameplayComponentStore<GameplayScatterPressureComponent> store)
                || !store.TryGet(entity.GameplayEntityId, out GameplayScatterPressureComponent scatterPressure))
            {
                return;
            }

            var cleared = new GameplayScatterPressureComponent(
                scatterPressure.MaxScatterPressure,
                scatterPressure.RecoveryRate,
                scatterPressure.RecoveryDelayFrames,
                currentScatterPressure: 0,
                lastPressureFrame: frame,
                currentCohesionLimit: scatterPressure.MaxScatterPressure,
                maxScatterDebt: scatterPressure.MaxScatterDebt,
                brokenRecoveryDelayFrames: scatterPressure.BrokenRecoveryDelayFrames,
                lastCohesionBreakFrame: -1L);
            store.Set(entity.GameplayEntityId, cleared);
            entity.SyncCombatScatterPressure(cleared);
        }

        public void HoldBrokenScatterRecovery(CharacterTestCharacterEntity entity, long frame)
        {
            if (entity == null
                || frame < 0L
                || !_gameplayComponentWorld.TryGetStore(out GameplayComponentStore<GameplayScatterPressureComponent> store)
                || !store.TryGet(entity.GameplayEntityId, out GameplayScatterPressureComponent scatterPressure)
                || !scatterPressure.IsBroken
                || scatterPressure.CurrentBand != PressureBand.Broken
                || scatterPressure.BrokenRecoveryDelayFrames <= 0)
            {
                return;
            }

            scatterPressure.LastCohesionBreakFrame = frame;
            store.Set(entity.GameplayEntityId, scatterPressure);
            entity.SyncCombatScatterPressure(scatterPressure);
        }

        public bool TryGetGameplayLifecycle(
            CharacterTestCharacterEntity entity,
            out GameplayLifecycleComponent lifecycle)
        {
            if (entity == null || !entity.GameplayEntityId.IsValid)
            {
                lifecycle = default;
                return false;
            }

            if (!_gameplayComponentWorld.TryGetStore(
                    out GameplayComponentStore<GameplayLifecycleComponent> store))
            {
                lifecycle = default;
                return false;
            }

            return store.TryGet(entity.GameplayEntityId, out lifecycle);
        }

        public void SetGameplayLifecycle(
            CharacterTestCharacterEntity entity,
            GameplayLifecycleState state)
        {
            if (entity == null || !entity.GameplayEntityId.IsValid)
                return;

            _gameplayComponentWorld.GetOrCreateStore<GameplayLifecycleComponent>().Set(
                entity.GameplayEntityId,
                new GameplayLifecycleComponent(state));
        }

        public void SyncCombatStateHealth(CharacterTestCharacterEntity entity)
        {
            if (entity == null)
                return;

            int hp = GetCurrentAttribute(entity, CharacterTestCharacterAttributeIds.Hp);
            int maxHp = CharacterTestCharacterAttributeIds.DefaultMaxHp;
            if (_gameplayComponentWorld.TryGetStore(out GameplayComponentStore<GameplayAttributeSetComponent> attributes)
                && attributes.TryGet(entity.GameplayEntityId, out GameplayAttributeSetComponent attributeSet)
                && attributeSet.TryGet(CharacterTestCharacterAttributeIds.Hp, out GameplayAttributeValue hpValue))
                maxHp = Math.Max(1, hpValue.BaseValue);
            entity.SyncCombatHealth(maxHp, hp);
            if (TryGetScatterPressure(entity, out GameplayScatterPressureComponent scatterPressure))
                entity.SyncCombatScatterPressure(scatterPressure);
        }

        private void RegisterGameplayState(
            CharacterTestCharacterEntity entity,
            CharacterImportedPackage package)
        {
            _combatGameplayMap.Register(ToCombatEntityId(entity), entity.GameplayEntityId);
            SetGameplayLifecycle(entity, GameplayLifecycleState.Alive);
            _gameplayComponentWorld
                .GetOrCreateStore<GameplayTargetClassificationComponent>()
                .Set(
                    entity.GameplayEntityId,
                    new GameplayTargetClassificationComponent(
                        GameplayTargetIdentityTypes.Character,
                        GameplayTargetCapabilities.Damageable
                            | GameplayTargetCapabilities.Movable));
            _gameplayComponentWorld.GetOrCreateStore<GameplayTeamComponent>().Set(
                entity.GameplayEntityId,
                new GameplayTeamComponent(entity.Team.TeamId));
            _gameplayComponentWorld.GetOrCreateStore<GameplayAttributeSetComponent>().Set(
                entity.GameplayEntityId,
                CreateInitialGameplayAttributes(package));
            CharacterConfig characterConfig = package?.Configs?.Characters != null && package.Configs.Characters.Length > 0
                ? package.Configs.Characters[0] : null;
            CharacterAttributeProfileConfig attributeProfile = characterConfig == null ? null
                : package.Configs.FindAttributeProfile(characterConfig.AttributeProfileId);
            _gameplayComponentWorld.GetOrCreateStore<GameplayScatterPressureComponent>().Set(
                entity.GameplayEntityId,
                new GameplayScatterPressureComponent(
                    attributeProfile?.BaseCohesion ?? CharacterTestCharacterAttributeIds.DefaultScatterPressureMax,
                    attributeProfile?.ScatterRecoveryRate ?? CharacterTestCharacterAttributeIds.DefaultScatterPressureRecoveryRate,
                    attributeProfile?.ScatterRecoveryDelayFrames ?? CharacterTestCharacterAttributeIds.DefaultScatterPressureRecoveryDelayFrames,
                    brokenRecoveryDelayFrames: attributeProfile?.CohesionBreakRecoveryDelayFrames ?? CharacterTestCharacterAttributeIds.DefaultCohesionBreakRecoveryDelayFrames));
        }

        private static GameplayAttributeSetComponent CreateInitialGameplayAttributes(
            CharacterImportedPackage package)
        {
            var values = new Dictionary<int, GameplayAttributeValue>
            {
                [CharacterTestCharacterAttributeIds.Hp] = new GameplayAttributeValue(
                    CharacterTestCharacterAttributeIds.Hp,
                    CharacterTestCharacterAttributeIds.DefaultMaxHp,
                    CharacterTestCharacterAttributeIds.DefaultMaxHp),
                [CharacterTestCharacterAttributeIds.Attack] = new GameplayAttributeValue(
                    CharacterTestCharacterAttributeIds.Attack,
                    CharacterTestCharacterAttributeIds.DefaultAttack,
                    CharacterTestCharacterAttributeIds.DefaultAttack),
                [CharacterTestCharacterAttributeIds.Defense] = new GameplayAttributeValue(
                    CharacterTestCharacterAttributeIds.Defense,
                    CharacterTestCharacterAttributeIds.DefaultDefense,
                    CharacterTestCharacterAttributeIds.DefaultDefense),
                [CharacterTestCharacterAttributeIds.ScatterPressure] = new GameplayAttributeValue(
                    CharacterTestCharacterAttributeIds.ScatterPressure,
                    CharacterTestCharacterAttributeIds.DefaultScatterPressure,
                    CharacterTestCharacterAttributeIds.DefaultScatterPressure)
            };

            CharacterImportedConfigSet configs = package?.Configs;
            CharacterConfig character = configs?.Characters != null && configs.Characters.Length > 0
                ? configs.Characters[0]
                : null;
            CharacterAttributeProfileConfig profile = character == null
                ? null
                : configs.FindAttributeProfile(character.AttributeProfileId);
            CharacterAttributeEntry[] packageAttributes = profile?.Attributes;
            bool hasCanonicalHp = false;
            for (int i = 0; packageAttributes != null && i < packageAttributes.Length; i++)
                hasCanonicalHp |= packageAttributes[i].AttributeId.Value == CharacterTestCharacterAttributeIds.Hp;
            for (int i = 0; packageAttributes != null && i < packageAttributes.Length; i++)
            {
                CharacterAttributeEntry attribute = packageAttributes[i];
                int attributeId = attribute.AttributeId.Value;
                // Older imported packages used a different HP identity. Normalize once at spawn.
                if (attributeId == 920001)
                {
                    if (hasCanonicalHp) continue;
                    attributeId = CharacterTestCharacterAttributeIds.Hp;
                }
                if (attributeId <= 0)
                    continue;

                values[attributeId] = new GameplayAttributeValue(
                    attributeId,
                    ToGameplayAttributeInt(attribute.BaseValue),
                    ToGameplayAttributeInt(attribute.InitialValue));
            }

            var result = new GameplayAttributeValue[values.Count];
            int index = 0;
            foreach (GameplayAttributeValue value in values.Values)
                result[index++] = value;
            return new GameplayAttributeSetComponent(result);
        }

        private static int ToGameplayAttributeInt(float value)
        {
            return checked((int)Math.Round(value, MidpointRounding.AwayFromZero));
        }

        private static CombatEntityId ToCombatEntityId(CharacterTestCharacterEntity entity)
        {
            return new CombatEntityId(CharacterTestCharacterPhysicsRuntime.DynamicBodyIdStart + entity.Id.Value);
        }

        private static List<CharacterTestMovableNaturalObjectSpawnSpec> NormalizeMovableNaturalObjectSpecs(
            IReadOnlyList<CharacterTestMovableNaturalObjectSpawnSpec> specs)
        {
            var candidates = new List<CharacterTestMovableNaturalObjectSpawnSpec>(specs.Count);
            for (int i = 0; i < specs.Count; i++)
            {
                CharacterTestMovableNaturalObjectSpawnSpec spec = specs[i];
                if (spec.NumericId > 0 && !string.IsNullOrWhiteSpace(spec.StableId))
                    candidates.Add(spec);
            }

            candidates.Sort(CompareMovableNaturalObjectSpawnSpecs);
            var seenNumericIds = new HashSet<int>();
            var seenStableIds = new HashSet<string>();
            var normalized = new List<CharacterTestMovableNaturalObjectSpawnSpec>(candidates.Count);
            for (int i = 0; i < candidates.Count; i++)
            {
                CharacterTestMovableNaturalObjectSpawnSpec spec = candidates[i];
                if (seenNumericIds.Add(spec.NumericId) && seenStableIds.Add(spec.StableId))
                    normalized.Add(spec);
            }

            return normalized;
        }

        private static CharacterTestMovableNaturalObjectSnapshot[] CreateOrderedMovableNaturalObjectSnapshots(
            IReadOnlyList<CharacterTestMovableNaturalObjectSnapshot> snapshots)
        {
            int count = 0;
            for (int i = 0; i < snapshots.Count; i++)
            {
                if (snapshots[i] != null)
                    count++;
            }

            if (count == 0)
                return Array.Empty<CharacterTestMovableNaturalObjectSnapshot>();

            var ordered = new CharacterTestMovableNaturalObjectSnapshot[count];
            int write = 0;
            for (int i = 0; i < snapshots.Count; i++)
            {
                CharacterTestMovableNaturalObjectSnapshot snapshot = snapshots[i];
                if (snapshot != null)
                    ordered[write++] = snapshot;
            }

            Array.Sort(ordered, CompareMovableNaturalObjectSnapshots);
            return ordered;
        }

        private static int CompareMovableNaturalObjectSnapshots(
            CharacterTestMovableNaturalObjectSnapshot left,
            CharacterTestMovableNaturalObjectSnapshot right)
        {
            int numeric = left.NumericId.CompareTo(right.NumericId);
            return numeric != 0
                ? numeric
                : string.Compare(left.StableId ?? string.Empty, right.StableId ?? string.Empty, StringComparison.Ordinal);
        }

        private static int CompareMovableNaturalObjectSpawnSpecs(
            CharacterTestMovableNaturalObjectSpawnSpec left,
            CharacterTestMovableNaturalObjectSpawnSpec right)
        {
            int numeric = left.NumericId.CompareTo(right.NumericId);
            return numeric != 0
                ? numeric
                : string.Compare(left.StableId, right.StableId, StringComparison.Ordinal);
        }
    }
}
