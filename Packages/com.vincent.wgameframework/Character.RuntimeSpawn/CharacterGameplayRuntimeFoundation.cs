using System;
using System.Collections.Generic;
using MxFramework.CharacterApplication;
using MxFramework.CharacterControl;
using MxFramework.Combat.Core;
using MxFramework.Gameplay;
using MxFramework.Runtime;

namespace MxFramework.CharacterRuntimeSpawn
{
    public static class CharacterGameplaySpawnDefinitionBuilder
    {
        public static CharacterGameplaySpawnDefinitionBuildResult Build(
            CharacterRuntimeBinding binding,
            CharacterImportedConfigSet configs)
        {
            if (binding == null)
                throw new ArgumentNullException(nameof(binding));
            if (configs == null)
                throw new ArgumentNullException(nameof(configs));

            var diagnostics = new List<string>();
            CharacterResolvedProfile profile = binding.ResolvedProfile;
            int definitionId = profile.CharacterId.Value;
            string stableId = CreateSpawnStableId(profile);
            int teamId = CharacterGameplayTeamIdResolver.Resolve(binding.SpawnPlan.TeamId);
            CharacterAttributeProfileConfig attributes = configs.FindAttributeProfile(profile.AttributeProfileId);
            GameplayAttributeValue[] attributeValues = BuildAttributeValues(attributes, diagnostics);

            var initializers = new List<IGameplayComponentSpawnInitializer>
            {
                new GameplayComponentSpawnInitializer<GameplayIdentityComponent>(
                    GameplayCoreComponentSchemaDescriptors.IdentityStableId,
                    new GameplayIdentityComponent(definitionId, variantId: profile.LoadoutId.Value)),
                new GameplayComponentSpawnInitializer<
                    GameplayTargetClassificationComponent>(
                    GameplayCoreComponentSchemaDescriptors
                        .TargetClassificationStableId,
                    new GameplayTargetClassificationComponent(
                        GameplayTargetIdentityTypes.Character,
                        GameplayTargetCapabilities.Damageable
                            | GameplayTargetCapabilities.Movable)),
                new GameplayComponentSpawnInitializer<GameplayTeamComponent>(
                    GameplayCoreComponentSchemaDescriptors.TeamStableId,
                    new GameplayTeamComponent(teamId)),
                new GameplayComponentSpawnInitializer<GameplayLifecycleComponent>(
                    GameplayCoreComponentSchemaDescriptors.LifecycleStableId,
                    GameplayLifecycleComponent.Alive)
            };

            if (attributeValues.Length > 0)
            {
                initializers.Add(new GameplayComponentSpawnInitializer<GameplayAttributeSetComponent>(
                    GameplayAttributeComponentSchemaDescriptors.AttributesStableId,
                    new GameplayAttributeSetComponent(attributeValues)));
            }
            else
            {
                diagnostics.Add("No character attributes were emitted for character " + definitionId + ".");
            }

            var definition = new GameplayComponentSpawnDefinition(
                definitionId,
                stableId,
                schemaVersion: 1,
                initializers);

            return new CharacterGameplaySpawnDefinitionBuildResult(definition, diagnostics.ToArray());
        }

        private static string CreateSpawnStableId(CharacterResolvedProfile profile)
        {
            string characterStableId = profile.DebugContext.CharacterStableId;
            if (!string.IsNullOrWhiteSpace(characterStableId))
            {
                string candidate = "mxframework.character." + SanitizeStableIdPart(characterStableId);
                if (IsValidSpawnStableId(candidate))
                    return candidate;
            }

            return "mxframework.character." + profile.CharacterId.Value.ToString(System.Globalization.CultureInfo.InvariantCulture);
        }

        private static GameplayAttributeValue[] BuildAttributeValues(
            CharacterAttributeProfileConfig attributes,
            List<string> diagnostics)
        {
            if (attributes == null)
            {
                diagnostics.Add("Character attribute profile is missing; attribute component initializer was not emitted.");
                return Array.Empty<GameplayAttributeValue>();
            }

            var values = new List<GameplayAttributeValue>();
            for (int i = 0; i < attributes.Attributes.Length; i++)
            {
                CharacterAttributeEntry attribute = attributes.Attributes[i];
                if (attribute.AttributeId.Value <= 0)
                {
                    diagnostics.Add("Skipped invalid character attribute id at index " + i + ".");
                    continue;
                }

                values.Add(new GameplayAttributeValue(
                    attribute.AttributeId.Value,
                    ToGameplayInt(attribute.BaseValue),
                    ToGameplayInt(attribute.InitialValue)));
            }

            return values.ToArray();
        }

        private static int ToGameplayInt(float value)
        {
            return checked((int)Math.Round(value, MidpointRounding.AwayFromZero));
        }

        private static string SanitizeStableIdPart(string value)
        {
            string trimmed = value.Trim().ToLowerInvariant();
            var chars = new char[trimmed.Length];
            for (int i = 0; i < trimmed.Length; i++)
            {
                char c = trimmed[i];
                bool valid = (c >= 'a' && c <= 'z')
                    || (c >= '0' && c <= '9')
                    || c == '.'
                    || c == '_'
                    || c == '-';
                chars[i] = valid ? c : '.';
            }

            return new string(chars).Trim('.');
        }

        private static bool IsValidSpawnStableId(string stableId)
        {
            if (string.IsNullOrWhiteSpace(stableId))
                return false;
            if (!string.Equals(stableId, stableId.Trim(), StringComparison.Ordinal))
                return false;
            if (stableId[0] == '.' || stableId[stableId.Length - 1] == '.')
                return false;

            bool hasDot = false;
            bool previousDot = false;
            for (int i = 0; i < stableId.Length; i++)
            {
                char c = stableId[i];
                if (c == '.')
                {
                    if (previousDot)
                        return false;

                    hasDot = true;
                    previousDot = true;
                    continue;
                }

                previousDot = false;
                bool valid = (c >= 'a' && c <= 'z')
                    || (c >= '0' && c <= '9')
                    || c == '_'
                    || c == '-';
                if (!valid)
                    return false;
            }

            return hasDot;
        }
    }

    public sealed class CharacterGameplaySpawnDefinitionBuildResult
    {
        public CharacterGameplaySpawnDefinitionBuildResult(
            GameplayComponentSpawnDefinition definition,
            string[] diagnostics)
        {
            Definition = definition ?? throw new ArgumentNullException(nameof(definition));
            Diagnostics = diagnostics ?? Array.Empty<string>();
        }

        public GameplayComponentSpawnDefinition Definition { get; }
        public string[] Diagnostics { get; }
    }

    public static class CharacterGameplayAbilityRegistryBuilder
    {
        public static CharacterGameplayAbilityRegistryBuildResult Build(
            CharacterRuntimeBinding binding,
            CharacterGameplayAbilityBindingCatalog bindingCatalog)
        {
            if (binding == null)
                throw new ArgumentNullException(nameof(binding));
            if (bindingCatalog == null)
                throw new ArgumentNullException(nameof(bindingCatalog));

            CharacterAbilityId[] abilityIds = binding.ResolvedProfile.EffectiveAbilityIds;
            var diagnostics = new List<string>();
            var registeredAbilityIds = new List<CharacterAbilityId>();
            var missingAbilityIds = new List<CharacterAbilityId>();
            var registry = new GameplayComponentAbilityRegistry();

            for (int i = 0; i < abilityIds.Length; i++)
            {
                CharacterAbilityId abilityId = abilityIds[i];
                if (!abilityId.IsValid)
                {
                    diagnostics.Add("Skipped invalid character ability id at index " + i + ".");
                    missingAbilityIds.Add(abilityId);
                    continue;
                }

                if (!bindingCatalog.TryGet(abilityId, out IGameplayComponentAbility ability))
                {
                    diagnostics.Add("No gameplay component ability binding registered for character ability id "
                        + abilityId.Value
                        + ".");
                    missingAbilityIds.Add(abilityId);
                    continue;
                }

                registry.Register(ability);
                registeredAbilityIds.Add(abilityId);
            }

            if (abilityIds.Length == 0)
            {
                diagnostics.Add("No effective character abilities were resolved for gameplay registry bootstrap.");
            }
            else if (missingAbilityIds.Count == 0)
            {
                diagnostics.Add("Registered "
                    + registeredAbilityIds.Count
                    + " character ability bindings into the gameplay component ability registry.");
            }

            return new CharacterGameplayAbilityRegistryBuildResult(
                registry,
                abilityIds,
                diagnostics.ToArray(),
                isDeferred: false,
                registeredAbilityIds: registeredAbilityIds.ToArray(),
                missingAbilityIds: missingAbilityIds.ToArray());
        }

        public static CharacterGameplayAbilityRegistryBuildResult BuildDeferred(CharacterRuntimeBinding binding)
        {
            if (binding == null)
                throw new ArgumentNullException(nameof(binding));

            CharacterAbilityId[] abilityIds = binding.ResolvedProfile.EffectiveAbilityIds;
            var diagnostics = new List<string>();
            if (abilityIds.Length == 0)
            {
                diagnostics.Add("No effective character abilities were resolved for gameplay registry bootstrap.");
            }
            else
            {
                diagnostics.Add("Character ability registry bootstrap is deferred for "
                    + abilityIds.Length
                    + " effective ability ids; #408 only creates spawn definitions and live entities.");
            }

            return new CharacterGameplayAbilityRegistryBuildResult(
                new GameplayComponentAbilityRegistry(),
                abilityIds,
                diagnostics.ToArray(),
                isDeferred: true,
                registeredAbilityIds: Array.Empty<CharacterAbilityId>(),
                missingAbilityIds: abilityIds);
        }
    }

    public sealed class CharacterGameplayAbilityBindingCatalog
    {
        private readonly Dictionary<int, IGameplayComponentAbility> _bindings =
            new Dictionary<int, IGameplayComponentAbility>();

        public int Count => _bindings.Count;

        public void Register(CharacterAbilityId abilityId, IGameplayComponentAbility ability)
        {
            if (!abilityId.IsValid)
                throw new ArgumentOutOfRangeException(nameof(abilityId), "Character ability id must be greater than zero.");
            if (ability == null)
                throw new ArgumentNullException(nameof(ability));
            if (ability.AbilityId != abilityId.Value)
            {
                throw new ArgumentException(
                    "Gameplay component ability id must match the character ability id.",
                    nameof(ability));
            }

            if (_bindings.ContainsKey(abilityId.Value))
                throw new InvalidOperationException("Character ability id is already registered: " + abilityId.Value);

            _bindings.Add(abilityId.Value, ability);
        }

        public bool TryGet(CharacterAbilityId abilityId, out IGameplayComponentAbility ability)
        {
            if (!abilityId.IsValid)
            {
                ability = null;
                return false;
            }

            return _bindings.TryGetValue(abilityId.Value, out ability);
        }

        public CharacterAbilityId[] CreateSnapshot()
        {
            if (_bindings.Count == 0)
                return Array.Empty<CharacterAbilityId>();

            var ids = new CharacterAbilityId[_bindings.Count];
            int index = 0;
            foreach (int abilityId in _bindings.Keys)
                ids[index++] = new CharacterAbilityId(abilityId);

            Array.Sort(ids, (left, right) => left.Value.CompareTo(right.Value));
            return ids;
        }

        public IGameplayComponentAbility[] CreateAbilitySnapshot()
        {
            if (_bindings.Count == 0)
                return Array.Empty<IGameplayComponentAbility>();

            var abilities = new List<IGameplayComponentAbility>(_bindings.Values);
            abilities.Sort((left, right) => left.AbilityId.CompareTo(right.AbilityId));
            return abilities.ToArray();
        }

        public void Clear()
        {
            _bindings.Clear();
        }
    }

    public sealed class CharacterGameplayAbilityRegistryBuildResult
    {
        public CharacterGameplayAbilityRegistryBuildResult(
            GameplayComponentAbilityRegistry registry,
            CharacterAbilityId[] effectiveAbilityIds,
            string[] diagnostics,
            bool isDeferred)
            : this(
                registry,
                effectiveAbilityIds,
                diagnostics,
                isDeferred,
                Array.Empty<CharacterAbilityId>(),
                Array.Empty<CharacterAbilityId>())
        {
        }

        public CharacterGameplayAbilityRegistryBuildResult(
            GameplayComponentAbilityRegistry registry,
            CharacterAbilityId[] effectiveAbilityIds,
            string[] diagnostics,
            bool isDeferred,
            CharacterAbilityId[] registeredAbilityIds,
            CharacterAbilityId[] missingAbilityIds)
        {
            Registry = registry ?? throw new ArgumentNullException(nameof(registry));
            EffectiveAbilityIds = effectiveAbilityIds ?? Array.Empty<CharacterAbilityId>();
            Diagnostics = diagnostics ?? Array.Empty<string>();
            IsDeferred = isDeferred;
            RegisteredAbilityIds = registeredAbilityIds ?? Array.Empty<CharacterAbilityId>();
            MissingAbilityIds = missingAbilityIds ?? Array.Empty<CharacterAbilityId>();
        }

        public GameplayComponentAbilityRegistry Registry { get; }
        public CharacterAbilityId[] EffectiveAbilityIds { get; }
        public CharacterAbilityId[] RegisteredAbilityIds { get; }
        public CharacterAbilityId[] MissingAbilityIds { get; }
        public string[] Diagnostics { get; }
        public bool IsDeferred { get; }
    }

    public sealed class CharacterRuntimeEntityRegistry
    {
        private readonly Dictionary<int, CharacterRuntimeEntityHandle> _byStableId =
            new Dictionary<int, CharacterRuntimeEntityHandle>();

        private readonly Dictionary<GameplayEntityId, CharacterRuntimeEntityHandle> _byGameplayEntityId =
            new Dictionary<GameplayEntityId, CharacterRuntimeEntityHandle>();

        public int Count => _byStableId.Count;

        public void Register(CharacterRuntimeEntityHandle handle)
        {
            if (!handle.IsValid)
                throw new ArgumentException("Character runtime entity handle must be valid.", nameof(handle));
            if (_byStableId.ContainsKey(handle.StableCharacterId))
                throw new InvalidOperationException("Character stable id is already registered: " + handle.StableCharacterId);
            if (_byGameplayEntityId.ContainsKey(handle.GameplayEntityId))
                throw new InvalidOperationException("Gameplay entity id is already registered: " + handle.GameplayEntityId);

            _byStableId.Add(handle.StableCharacterId, handle);
            _byGameplayEntityId.Add(handle.GameplayEntityId, handle);
        }

        public bool TryGetByStableCharacterId(int stableCharacterId, out CharacterRuntimeEntityHandle handle)
        {
            return _byStableId.TryGetValue(stableCharacterId, out handle);
        }

        public bool TryGetByGameplayEntityId(GameplayEntityId gameplayEntityId, out CharacterRuntimeEntityHandle handle)
        {
            return _byGameplayEntityId.TryGetValue(gameplayEntityId, out handle);
        }

        public bool RemoveByStableCharacterId(int stableCharacterId, out CharacterRuntimeEntityHandle handle)
        {
            if (!_byStableId.TryGetValue(stableCharacterId, out handle))
                return false;

            _byStableId.Remove(stableCharacterId);
            _byGameplayEntityId.Remove(handle.GameplayEntityId);
            return true;
        }

        public CharacterRuntimeEntityHandle[] CreateSnapshot()
        {
            if (_byStableId.Count == 0)
                return Array.Empty<CharacterRuntimeEntityHandle>();

            var snapshot = new CharacterRuntimeEntityHandle[_byStableId.Count];
            int index = 0;
            foreach (KeyValuePair<int, CharacterRuntimeEntityHandle> pair in _byStableId)
                snapshot[index++] = pair.Value;

            Array.Sort(snapshot, CompareHandles);
            return snapshot;
        }

        public void Clear()
        {
            _byStableId.Clear();
            _byGameplayEntityId.Clear();
        }

        private static int CompareHandles(CharacterRuntimeEntityHandle left, CharacterRuntimeEntityHandle right)
        {
            return left.StableCharacterId.CompareTo(right.StableCharacterId);
        }
    }

    public readonly struct CharacterRuntimeEntityHandle : IEquatable<CharacterRuntimeEntityHandle>
    {
        public CharacterRuntimeEntityHandle(
            int stableCharacterId,
            GameplayEntityId gameplayEntityId,
            CombatEntityId combatEntityId,
            CombatBodyId combatBodyId,
            CharacterControlEntityRef entityRef,
            int spawnDefinitionId)
        {
            if (stableCharacterId <= 0)
                throw new ArgumentOutOfRangeException(nameof(stableCharacterId), "Stable character id must be greater than zero.");
            if (!gameplayEntityId.IsValid)
                throw new ArgumentException("Gameplay entity id must be valid.", nameof(gameplayEntityId));
            if (spawnDefinitionId <= 0)
                throw new ArgumentOutOfRangeException(nameof(spawnDefinitionId), "Spawn definition id must be greater than zero.");

            StableCharacterId = stableCharacterId;
            GameplayEntityId = gameplayEntityId;
            CombatEntityId = combatEntityId;
            CombatBodyId = combatBodyId;
            EntityRef = entityRef;
            SpawnDefinitionId = spawnDefinitionId;
        }

        public int StableCharacterId { get; }
        public GameplayEntityId GameplayEntityId { get; }
        public CombatEntityId CombatEntityId { get; }
        public CombatBodyId CombatBodyId { get; }
        public CharacterControlEntityRef EntityRef { get; }
        public int SpawnDefinitionId { get; }
        public bool IsValid => StableCharacterId > 0 && GameplayEntityId.IsValid && SpawnDefinitionId > 0;

        public bool Equals(CharacterRuntimeEntityHandle other)
        {
            return StableCharacterId == other.StableCharacterId
                && GameplayEntityId.Equals(other.GameplayEntityId)
                && CombatEntityId.Equals(other.CombatEntityId)
                && CombatBodyId.Equals(other.CombatBodyId)
                && EntityRef.Equals(other.EntityRef)
                && SpawnDefinitionId == other.SpawnDefinitionId;
        }

        public override bool Equals(object obj)
        {
            return obj is CharacterRuntimeEntityHandle other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = StableCharacterId;
                hash = (hash * 397) ^ GameplayEntityId.GetHashCode();
                hash = (hash * 397) ^ CombatEntityId.GetHashCode();
                hash = (hash * 397) ^ CombatBodyId.GetHashCode();
                hash = (hash * 397) ^ EntityRef.GetHashCode();
                hash = (hash * 397) ^ SpawnDefinitionId;
                return hash;
            }
        }
    }

    public sealed class CharacterGameplayRuntimeBootstrap : IDisposable
    {
        private readonly List<GameplayRuntimeEvent> _events = new List<GameplayRuntimeEvent>();
        private long _nextFrame;
        private bool _disposed;

        public CharacterGameplayRuntimeBootstrap()
            : this(null)
        {
        }

        public CharacterGameplayRuntimeBootstrap(CharacterGameplayAbilityBindingCatalog abilityBindingCatalog)
        {
            World = new GameplayComponentWorld();
            RegisterDefaultSchemas(World);
            CommandBuffer = new RuntimeCommandBuffer();
            SpawnRegistry = new GameplayComponentSpawnRegistry();
            AbilityRegistry = new GameplayComponentAbilityRegistry();
            AbilityBindingCatalog = abilityBindingCatalog;
            RegisterCatalogAbilities(AbilityRegistry, AbilityBindingCatalog);
            EntityRegistry = new CharacterRuntimeEntityRegistry();
            GameplayModule = new GameplayRuntimeModule(
                new GameplayWorld(),
                new GameplayAbilityRegistry(),
                CommandBuffer,
                tickWorldAutomatically: false,
                configureDefaultPipeline: pipeline =>
                {
                    pipeline.Add(new GameplayComponentSpawnCommandSystem(SpawnRegistry));
                    pipeline.Add(new GameplayAttributeCommandSystem());
                    pipeline.Add(new GameplayComponentAbilityCommandSystem(
                        AbilityRegistry,
                        new GameplayComponentAbilityRequestStore(),
                        new GameplayComponentTargetingService()));
                    pipeline.Add(new GameplayLifecycleCleanupSystem());
                },
                componentWorld: World);
            Host = new RuntimeHost();
            Host.RegisterModule(GameplayModule);
            Host.Initialize();
            Host.Start();
        }

        public RuntimeHost Host { get; }
        public RuntimeCommandBuffer CommandBuffer { get; }
        public GameplayRuntimeModule GameplayModule { get; }
        public GameplayComponentWorld World { get; }
        public GameplayComponentSpawnRegistry SpawnRegistry { get; }
        public GameplayComponentAbilityRegistry AbilityRegistry { get; }
        public CharacterGameplayAbilityBindingCatalog AbilityBindingCatalog { get; }
        public CharacterRuntimeEntityRegistry EntityRegistry { get; }

        public CharacterGameplaySpawnResult Spawn(CharacterRuntimeBinding binding, CharacterImportedConfigSet configs)
        {
            EnsureNotDisposed();
            if (binding == null)
                throw new ArgumentNullException(nameof(binding));

            int stableCharacterId = binding.ResolvedProfile.CharacterId.Value;
            if (EntityRegistry.TryGetByStableCharacterId(stableCharacterId, out CharacterRuntimeEntityHandle existing))
            {
                return CharacterGameplaySpawnResult.Rejected(
                    existing,
                    "CharacterStableIdAlreadyRegistered",
                    new[] { "Character stable id is already live: " + stableCharacterId + "." });
            }

            CharacterGameplaySpawnDefinitionBuildResult definitionResult =
                CharacterGameplaySpawnDefinitionBuilder.Build(binding, configs);
            if (!SpawnRegistry.TryGet(definitionResult.Definition.DefinitionId, out _))
                SpawnRegistry.Register(definitionResult.Definition);

            CharacterGameplayAbilityRegistryBuildResult abilityResult =
                AbilityBindingCatalog == null
                    ? CharacterGameplayAbilityRegistryBuilder.BuildDeferred(binding)
                    : CharacterGameplayAbilityRegistryBuilder.Build(binding, AbilityBindingCatalog);
            RuntimeFrame frame = new RuntimeFrame(_nextFrame);
            RuntimeCommandValidationResult enqueue = CommandBuffer.Enqueue(
                GameplayRuntimeCommandFactory.SpawnComponentEntity(
                    frame,
                    definitionResult.Definition.DefinitionId,
                    variantId: binding.ResolvedProfile.LoadoutId.Value,
                    sourceId: stableCharacterId,
                    traceId: new RuntimeTraceIdentity(1002, stableCharacterId, 0, 0)));
            if (!enqueue.Success)
            {
                return CharacterGameplaySpawnResult.Rejected(
                    default,
                    "SpawnCommandRejectedByBuffer",
                    MergeDiagnostics(definitionResult.Diagnostics, abilityResult.Diagnostics, enqueue.Error.Message));
            }

            Host.Tick(_nextFrame, 0d);
            GameplayRuntimeEvent evt = FindSpawnEvent(frame, stableCharacterId);
            _nextFrame++;

            if (evt.Type != GameplayRuntimeEventType.ComponentEntityCreated ||
                !evt.TryGetComponentEntityId(out GameplayEntityId gameplayEntityId))
            {
                string reason = string.IsNullOrEmpty(evt.Reason) ? "MissingSpawnEvent" : evt.Reason;
                return CharacterGameplaySpawnResult.Rejected(
                    default,
                    reason,
                    MergeDiagnostics(definitionResult.Diagnostics, abilityResult.Diagnostics, "Character spawn did not create a live GameplayEntityId."));
            }

            var entityRef = CharacterControlEntityRef.FromGameplayAndCombat(
                gameplayEntityId,
                binding.CombatBodyBindingPlan == null ? CombatEntityId.None : binding.CombatBodyBindingPlan.EntityId,
                binding.CombatBodyBindingPlan == null ? CombatBodyId.None : binding.CombatBodyBindingPlan.BodyId,
                stableCharacterId);
            var handle = new CharacterRuntimeEntityHandle(
                stableCharacterId,
                gameplayEntityId,
                entityRef.CombatEntityId,
                entityRef.CombatBodyId,
                entityRef,
                definitionResult.Definition.DefinitionId);
            EntityRegistry.Register(handle);

            return CharacterGameplaySpawnResult.Spawned(
                handle,
                definitionResult.Definition,
                abilityResult,
                MergeDiagnostics(definitionResult.Diagnostics, abilityResult.Diagnostics));
        }

        public CharacterGameplayDestroyResult Destroy(int stableCharacterId)
        {
            EnsureNotDisposed();
            if (!EntityRegistry.TryGetByStableCharacterId(stableCharacterId, out CharacterRuntimeEntityHandle handle))
                return CharacterGameplayDestroyResult.NotFound(stableCharacterId);

            RuntimeFrame frame = new RuntimeFrame(_nextFrame);
            RuntimeCommandValidationResult enqueue = CommandBuffer.Enqueue(
                GameplayRuntimeCommandFactory.DestroyComponentEntity(
                    frame,
                    handle.GameplayEntityId,
                    sourceId: stableCharacterId,
                    traceId: new RuntimeTraceIdentity(1003, stableCharacterId, 0, 0)));
            if (!enqueue.Success)
                return CharacterGameplayDestroyResult.Rejected(handle, "DestroyCommandRejectedByBuffer");

            Host.Tick(_nextFrame, 0d);
            GameplayRuntimeEvent evt = FindDestroyEvent(frame, handle.GameplayEntityId);
            _nextFrame++;

            if (evt.Type != GameplayRuntimeEventType.ComponentEntityDestroyed)
            {
                string reason = string.IsNullOrEmpty(evt.Reason) ? "MissingDestroyEvent" : evt.Reason;
                return CharacterGameplayDestroyResult.Rejected(handle, reason);
            }

            EntityRegistry.RemoveByStableCharacterId(stableCharacterId, out _);
            return CharacterGameplayDestroyResult.Destroyed(handle);
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            Host.Dispose();
            EntityRegistry.Clear();
            _disposed = true;
        }

        private GameplayRuntimeEvent FindSpawnEvent(RuntimeFrame frame, int stableCharacterId)
        {
            _events.Clear();
            GameplayModule.DrainEvents(frame, _events);
            for (int i = 0; i < _events.Count; i++)
            {
                GameplayRuntimeEvent evt = _events[i];
                if (evt.CommandId == GameplayRuntimeCommandIds.SpawnComponentEntity &&
                    evt.TraceId.Equals(new RuntimeTraceIdentity(1002, stableCharacterId, 0, 0)))
                {
                    return evt;
                }
            }

            return default;
        }

        private GameplayRuntimeEvent FindDestroyEvent(RuntimeFrame frame, GameplayEntityId entityId)
        {
            _events.Clear();
            GameplayModule.DrainEvents(frame, _events);
            for (int i = 0; i < _events.Count; i++)
            {
                GameplayRuntimeEvent evt = _events[i];
                if (evt.CommandId == GameplayRuntimeCommandIds.DestroyComponentEntity &&
                    evt.ComponentEntityId.Equals(entityId))
                {
                    return evt;
                }
            }

            return default;
        }

        private void EnsureNotDisposed()
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(CharacterGameplayRuntimeBootstrap));
        }

        private static void RegisterDefaultSchemas(GameplayComponentWorld world)
        {
            GameplayCoreComponentSchemaDescriptors.RegisterDiagnostics(world.Schemas);
            GameplayCoreComponentSchemaDescriptors.RegisterRuntimeHash(world.Schemas);
            GameplayCoreComponentSchemaDescriptors.RegisterSaveState(world.Schemas);
            GameplayAttributeComponentSchemaDescriptors.RegisterDiagnostics(world.Schemas);
            GameplayAttributeComponentSchemaDescriptors.RegisterRuntimeHash(world.Schemas);
            GameplayAttributeComponentSchemaDescriptors.RegisterSaveState(world.Schemas);
            GameplayAbilityCooldownComponentSchemaDescriptors.RegisterDiagnostics(world.Schemas);
            GameplayAbilityCooldownComponentSchemaDescriptors.RegisterRuntimeHash(world.Schemas);
            GameplayAbilityCooldownComponentSchemaDescriptors.RegisterSaveState(world.Schemas);
        }

        private static void RegisterCatalogAbilities(
            GameplayComponentAbilityRegistry registry,
            CharacterGameplayAbilityBindingCatalog catalog)
        {
            if (registry == null || catalog == null)
                return;

            IGameplayComponentAbility[] abilities = catalog.CreateAbilitySnapshot();
            for (int i = 0; i < abilities.Length; i++)
            {
                IGameplayComponentAbility ability = abilities[i];
                if (ability != null && !registry.TryGet(ability.AbilityId, out _))
                    registry.Register(ability);
            }
        }

        private static string[] MergeDiagnostics(params string[][] groups)
        {
            var merged = new List<string>();
            for (int i = 0; i < groups.Length; i++)
            {
                string[] group = groups[i];
                if (group == null)
                    continue;
                for (int j = 0; j < group.Length; j++)
                    merged.Add(group[j] ?? string.Empty);
            }

            return merged.ToArray();
        }

        private static string[] MergeDiagnostics(string[] first, string[] second, string extra)
        {
            return MergeDiagnostics(first, second, new[] { extra });
        }
    }

    public sealed class CharacterGameplaySpawnResult
    {
        private CharacterGameplaySpawnResult(
            bool success,
            CharacterRuntimeEntityHandle handle,
            GameplayComponentSpawnDefinition spawnDefinition,
            CharacterGameplayAbilityRegistryBuildResult abilityRegistry,
            string reason,
            string[] diagnostics)
        {
            Success = success;
            Handle = handle;
            SpawnDefinition = spawnDefinition;
            AbilityRegistry = abilityRegistry;
            Reason = reason ?? string.Empty;
            Diagnostics = diagnostics ?? Array.Empty<string>();
        }

        public bool Success { get; }
        public CharacterRuntimeEntityHandle Handle { get; }
        public GameplayComponentSpawnDefinition SpawnDefinition { get; }
        public CharacterGameplayAbilityRegistryBuildResult AbilityRegistry { get; }
        public string Reason { get; }
        public string[] Diagnostics { get; }

        public static CharacterGameplaySpawnResult Spawned(
            CharacterRuntimeEntityHandle handle,
            GameplayComponentSpawnDefinition definition,
            CharacterGameplayAbilityRegistryBuildResult abilityRegistry,
            string[] diagnostics)
        {
            return new CharacterGameplaySpawnResult(true, handle, definition, abilityRegistry, string.Empty, diagnostics);
        }

        public static CharacterGameplaySpawnResult Rejected(
            CharacterRuntimeEntityHandle handle,
            string reason,
            string[] diagnostics)
        {
            return new CharacterGameplaySpawnResult(false, handle, null, null, reason, diagnostics);
        }
    }

    public sealed class CharacterGameplayDestroyResult
    {
        private CharacterGameplayDestroyResult(bool success, CharacterRuntimeEntityHandle handle, int stableCharacterId, string reason)
        {
            Success = success;
            Handle = handle;
            StableCharacterId = stableCharacterId;
            Reason = reason ?? string.Empty;
        }

        public bool Success { get; }
        public CharacterRuntimeEntityHandle Handle { get; }
        public int StableCharacterId { get; }
        public string Reason { get; }

        public static CharacterGameplayDestroyResult Destroyed(CharacterRuntimeEntityHandle handle)
        {
            return new CharacterGameplayDestroyResult(true, handle, handle.StableCharacterId, string.Empty);
        }

        public static CharacterGameplayDestroyResult NotFound(int stableCharacterId)
        {
            return new CharacterGameplayDestroyResult(false, default, stableCharacterId, "CharacterStableIdNotFound");
        }

        public static CharacterGameplayDestroyResult Rejected(CharacterRuntimeEntityHandle handle, string reason)
        {
            return new CharacterGameplayDestroyResult(false, handle, handle.StableCharacterId, reason);
        }
    }

    internal static class CharacterGameplayTeamIdResolver
    {
        public static int Resolve(string teamId)
        {
            if (string.IsNullOrWhiteSpace(teamId))
                return 0;
            if (int.TryParse(teamId, out int parsed))
                return parsed < 0 ? 0 : parsed;

            unchecked
            {
                const int fnvOffset = unchecked((int)2166136261);
                const int fnvPrime = 16777619;
                int hash = fnvOffset;
                string normalized = teamId.Trim().ToLowerInvariant();
                for (int i = 0; i < normalized.Length; i++)
                {
                    hash ^= normalized[i];
                    hash *= fnvPrime;
                }

                return (hash & 0x7fffffff) == 0 ? 1 : hash & 0x7fffffff;
            }
        }
    }
}
