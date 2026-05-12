using System;
using System.Collections.Generic;
using MxFramework.Core.Collections;
using MxFramework.Gameplay;
using MxFramework.Runtime;

namespace MxFramework.Demo.GameplayComponentRuntime
{
    public sealed class GameplayComponentRuntimeShowcase : IDisposable
    {
        public const int HeroDefinitionId = 41001;
        public const int EnemyDefinitionId = 41002;
        public const int StrikeAbilityId = 42001;
        public const int HpAttributeId = 1;
        public const int ManaAttributeId = 2;
        public const int AttackAttributeId = 3;

        private readonly RingBuffer<string> _eventLog = new RingBuffer<string>(16);
        private readonly List<GameplayRuntimeEvent> _drainedEvents = new List<GameplayRuntimeEvent>(8);
        private readonly List<string> _eventLogSnapshot = new List<string>(16);

        private GameplayComponentWorld _world;
        private GameplayComponentSpawnRegistry _spawnRegistry;
        private GameplayComponentAbilityRegistry _abilityRegistry;
        private GameplayComponentAbilityRequestStore _requestStore;
        private RuntimeCommandBuffer _commandBuffer;
        private GameplayRuntimeModule _module;
        private RuntimeHost _host;
        private GameplayEntityId _heroEntityId;
        private GameplayEntityId _enemyEntityId;
        private long _nextFrame;
        private string _savedStateJson = string.Empty;
        private string _saveStatus = "No save";

        public GameplayComponentRuntimeShowcase()
        {
            Reset();
        }

        public GameplayComponentWorld World => _world;
        public GameplayEntityId HeroEntityId => _heroEntityId;
        public GameplayEntityId EnemyEntityId => _enemyEntityId;
        public bool HasSavedState => !string.IsNullOrEmpty(_savedStateJson);

        public void Reset()
        {
            DisposeRuntime();
            _world = CreateWorld();
            _spawnRegistry = CreateSpawnRegistry();
            _abilityRegistry = CreateAbilityRegistry();
            _requestStore = new GameplayComponentAbilityRequestStore();
            _commandBuffer = new RuntimeCommandBuffer(null, RuntimeFrame.Zero);
            _nextFrame = 0L;
            _heroEntityId = default;
            _enemyEntityId = default;
            _savedStateJson = string.Empty;
            _saveStatus = "No save";
            _eventLog.Clear();
            CreateRuntime(_world, RuntimeFrame.Zero);
            AddLog("Runtime reset");
        }

        public bool SpawnActors()
        {
            if (_heroEntityId.IsValid && _world.IsAlive(_heroEntityId))
            {
                AddLog("Spawn skipped: actors already exist");
                return false;
            }

            RuntimeFrame frame = CurrentCommandFrame;
            EnqueueOrLog(GameplayRuntimeCommandFactory.SpawnComponentEntity(
                frame,
                HeroDefinitionId,
                traceId: "showcase.spawn.hero"));
            EnqueueOrLog(GameplayRuntimeCommandFactory.SpawnComponentEntity(
                frame,
                EnemyDefinitionId,
                traceId: "showcase.spawn.enemy"));
            TickQueuedFrame();
            RefreshActorIds();
            return _heroEntityId.IsValid && _enemyEntityId.IsValid;
        }

        public bool CastStrike()
        {
            if (!EnsureActors())
                return false;

            if (!_world.IsAlive(_enemyEntityId))
            {
                AddLog("Cast skipped: enemy is not alive");
                return false;
            }

            RuntimeFrame frame = CurrentCommandFrame;
            GameplayComponentAbilityRequestHandle handle = _requestStore.Add(CreateStrikeRequest());
            EnqueueOrLog(GameplayRuntimeCommandFactory.CastComponentAbilityRequest(
                frame,
                handle,
                StrikeAbilityId,
                traceId: "showcase.strike"));
            TickQueuedFrame();
            return true;
        }

        public bool MarkEnemyPendingDestroyAndTick()
        {
            if (!EnsureActors())
                return false;

            if (!_world.IsAlive(_enemyEntityId))
            {
                AddLog("Cleanup skipped: enemy is already removed");
                return false;
            }

            _world.GetOrCreateStore<GameplayLifecycleComponent>().Set(
                _enemyEntityId,
                GameplayLifecycleComponent.PendingDestroy);
            AddLog("Enemy marked PendingDestroy");
            TickQueuedFrame();
            return !_world.IsAlive(_enemyEntityId);
        }

        public bool Save()
        {
            RuntimeSaveStateResult<RuntimeSaveState> capture =
                new GameplayComponentWorldSaveStateProvider(_world).CaptureSaveState();
            if (!capture.Success)
            {
                _saveStatus = "Save failed: " + capture.Error;
                AddLog(_saveStatus);
                return false;
            }

            _savedStateJson = RuntimeSaveStateJson.SaveToJson(capture.Value);
            _saveStatus = "Saved frame " + CurrentFrame.Value + " hash " + ComputeHash();
            AddLog(_saveStatus);
            return true;
        }

        public bool Restore()
        {
            if (string.IsNullOrEmpty(_savedStateJson))
            {
                _saveStatus = "Restore skipped: no save";
                AddLog(_saveStatus);
                return false;
            }

            RuntimeSaveStateResult<RuntimeSaveState> load = RuntimeSaveStateJson.LoadFromJson(_savedStateJson);
            if (!load.Success)
            {
                _saveStatus = "Restore failed: " + load.Error;
                AddLog(_saveStatus);
                return false;
            }

            GameplayComponentWorld restored = CreateWorld();
            RuntimeSaveStateResult<bool> restore =
                new GameplayComponentWorldSaveStateProvider(restored).RestoreSaveState(load.Value);
            if (!restore.Success)
            {
                _saveStatus = "Restore failed: " + restore.Error;
                AddLog(_saveStatus);
                return false;
            }

            DisposeRuntime();
            _world = restored;
            _requestStore = new GameplayComponentAbilityRequestStore();
            _commandBuffer = new RuntimeCommandBuffer(null, CurrentCommandFrame);
            CreateRuntime(_world, CurrentCommandFrame);
            RefreshActorIds();
            _saveStatus = "Restored frame " + CurrentFrame.Value + " hash " + ComputeHash();
            AddLog(_saveStatus);
            return true;
        }

        public GameplayComponentRuntimeShowcaseSnapshot CreateSnapshot()
        {
            _eventLogSnapshot.Clear();
            _eventLog.CopyTo(_eventLogSnapshot);
            return new GameplayComponentRuntimeShowcaseSnapshot(
                CurrentFrame,
                _nextFrame,
                _heroEntityId,
                _enemyEntityId,
                _world != null && _heroEntityId.IsValid && _world.IsAlive(_heroEntityId),
                _world != null && _enemyEntityId.IsValid && _world.IsAlive(_enemyEntityId),
                GetLifecycle(_heroEntityId),
                GetLifecycle(_enemyEntityId),
                GetCurrent(_heroEntityId, HpAttributeId),
                GetCurrent(_heroEntityId, ManaAttributeId),
                GetCurrent(_enemyEntityId, HpAttributeId),
                GetCurrent(_enemyEntityId, ManaAttributeId),
                GetCooldownRemaining(_heroEntityId, StrikeAbilityId),
                _requestStore != null ? _requestStore.Count : 0,
                _commandBuffer != null ? _commandBuffer.PendingCount : 0,
                ComputeHash(),
                _saveStatus,
                _eventLogSnapshot.ToArray());
        }

        public long ComputeHash()
        {
            if (_world == null)
                return 0L;

            return RuntimeHashCombiner.ComputeHash(
                CurrentFrame,
                new IRuntimeHashContributor[] { new GameplayComponentWorldHashContributor(_world) });
        }

        public void Dispose()
        {
            DisposeRuntime();
        }

        private RuntimeFrame CurrentFrame => _nextFrame <= 0L ? RuntimeFrame.Zero : new RuntimeFrame(_nextFrame - 1L);
        private RuntimeFrame CurrentCommandFrame => new RuntimeFrame(_nextFrame);

        private void TickQueuedFrame()
        {
            RuntimeFrame frame = CurrentCommandFrame;
            _host.Tick(new RuntimeTickContext(frame.Value, 0d, 0d, RuntimeTickStage.Simulation));
            DrainEvents(frame);
            _nextFrame++;
        }

        private bool EnsureActors()
        {
            if (_heroEntityId.IsValid && _enemyEntityId.IsValid)
                return true;

            AddLog("Action skipped: spawn actors first");
            return false;
        }

        private GameplayComponentAbilityRequest CreateStrikeRequest()
        {
            var query = new GameplayComponentTargetQuery(
                _heroEntityId,
                casterTeamId: 1,
                relationFilter: GameplayTargetRelationFilter.Enemy,
                maxTargets: 1);
            return new GameplayComponentAbilityRequest(
                _heroEntityId,
                StrikeAbilityId,
                new[] { _enemyEntityId },
                query);
        }

        private void EnqueueOrLog(RuntimeCommand command)
        {
            RuntimeCommandValidationResult result = _commandBuffer.Enqueue(command);
            if (!result.Success)
                AddLog("Command rejected before runtime: " + result.Error);
        }

        private void DrainEvents(RuntimeFrame frame)
        {
            _drainedEvents.Clear();
            _module.DrainEvents(frame, _drainedEvents);
            for (int i = 0; i < _drainedEvents.Count; i++)
                AddLog(FormatEvent(_drainedEvents[i]));
        }

        private void RefreshActorIds()
        {
            GameplayEntityId[] entities = _world.CreateEntitySnapshot();
            _heroEntityId = FindEntityByDefinition(entities, HeroDefinitionId);
            _enemyEntityId = FindEntityByDefinition(entities, EnemyDefinitionId);
        }

        private GameplayEntityId FindEntityByDefinition(GameplayEntityId[] entities, int definitionId)
        {
            if (!_world.TryGetStore(out GameplayComponentStore<GameplayIdentityComponent> identities))
                return default;

            for (int i = 0; i < entities.Length; i++)
            {
                if (identities.TryGet(entities[i], out GameplayIdentityComponent identity) &&
                    identity.DefinitionId == definitionId)
                {
                    return entities[i];
                }
            }

            return default;
        }

        private int GetCurrent(GameplayEntityId entityId, int attributeId)
        {
            if (!entityId.IsValid ||
                !_world.TryGetStore(out GameplayComponentStore<GameplayAttributeSetComponent> store) ||
                !store.TryGet(entityId, out GameplayAttributeSetComponent attributes))
            {
                return 0;
            }

            return attributes.GetCurrentValueOrDefault(attributeId);
        }

        private long GetCooldownRemaining(GameplayEntityId entityId, int abilityId)
        {
            if (!entityId.IsValid ||
                !_world.TryGetStore(out GameplayComponentStore<GameplayAbilityCooldownComponent> store) ||
                !store.TryGet(entityId, out GameplayAbilityCooldownComponent cooldown))
            {
                return 0L;
            }

            return cooldown.GetRemainingFrames(abilityId, CurrentFrame);
        }

        private GameplayLifecycleState GetLifecycle(GameplayEntityId entityId)
        {
            if (!entityId.IsValid ||
                !_world.TryGetStore(out GameplayComponentStore<GameplayLifecycleComponent> store) ||
                !store.TryGet(entityId, out GameplayLifecycleComponent lifecycle))
            {
                return GameplayLifecycleState.None;
            }

            return lifecycle.State;
        }

        private void CreateRuntime(GameplayComponentWorld world, RuntimeFrame startFrame)
        {
            _commandBuffer = _commandBuffer ?? new RuntimeCommandBuffer(null, startFrame);
            _module = new GameplayRuntimeModule(
                new GameplayWorld(),
                new GameplayAbilityRegistry(),
                _commandBuffer,
                tickWorldAutomatically: false,
                configureDefaultPipeline: pipeline =>
                {
                    pipeline.Add(new GameplayComponentSpawnCommandSystem(_spawnRegistry));
                    pipeline.Add(new GameplayAttributeCommandSystem());
                    pipeline.Add(new GameplayComponentAbilityCommandSystem(
                        _abilityRegistry,
                        _requestStore,
                        new GameplayComponentTargetingService()));
                    pipeline.Add(new GameplayLifecycleCleanupSystem());
                },
                componentWorld: world);
            _host = new RuntimeHost();
            _host.RegisterModule(_module);
            _host.Initialize();
            _host.Start();
        }

        private void DisposeRuntime()
        {
            if (_host == null)
                return;

            _host.Dispose();
            _host = null;
            _module = null;
        }

        private void AddLog(string message)
        {
            _eventLog.Add(message ?? string.Empty);
        }

        private static GameplayComponentWorld CreateWorld()
        {
            var world = new GameplayComponentWorld();
            GameplayCoreComponentSchemaDescriptors.RegisterDiagnostics(world.Schemas);
            GameplayCoreComponentSchemaDescriptors.RegisterRuntimeHash(world.Schemas);
            GameplayCoreComponentSchemaDescriptors.RegisterSaveState(world.Schemas);
            GameplayAttributeComponentSchemaDescriptors.RegisterDiagnostics(world.Schemas);
            GameplayAttributeComponentSchemaDescriptors.RegisterRuntimeHash(world.Schemas);
            GameplayAttributeComponentSchemaDescriptors.RegisterSaveState(world.Schemas);
            GameplayAbilityCooldownComponentSchemaDescriptors.RegisterDiagnostics(world.Schemas);
            GameplayAbilityCooldownComponentSchemaDescriptors.RegisterRuntimeHash(world.Schemas);
            GameplayAbilityCooldownComponentSchemaDescriptors.RegisterSaveState(world.Schemas);
            return world;
        }

        private static GameplayComponentSpawnRegistry CreateSpawnRegistry()
        {
            var registry = new GameplayComponentSpawnRegistry();
            registry.Register(CreateActorDefinition(
                HeroDefinitionId,
                "mxframework.demo.component_runtime.hero",
                teamId: 1,
                hp: 30,
                mana: 10,
                attack: 6));
            registry.Register(CreateActorDefinition(
                EnemyDefinitionId,
                "mxframework.demo.component_runtime.enemy",
                teamId: 2,
                hp: 12,
                mana: 0,
                attack: 0));
            return registry;
        }

        private static GameplayComponentSpawnDefinition CreateActorDefinition(
            int definitionId,
            string stableId,
            int teamId,
            int hp,
            int mana,
            int attack)
        {
            return new GameplayComponentSpawnDefinition(
                definitionId,
                stableId,
                1,
                new IGameplayComponentSpawnInitializer[]
                {
                    new GameplayComponentSpawnInitializer<GameplayIdentityComponent>(
                        GameplayCoreComponentSchemaDescriptors.IdentityStableId,
                        new GameplayIdentityComponent(definitionId)),
                    new GameplayComponentSpawnInitializer<GameplayTeamComponent>(
                        GameplayCoreComponentSchemaDescriptors.TeamStableId,
                        new GameplayTeamComponent(teamId)),
                    new GameplayComponentSpawnInitializer<GameplayLifecycleComponent>(
                        GameplayCoreComponentSchemaDescriptors.LifecycleStableId,
                        GameplayLifecycleComponent.Alive),
                    new GameplayComponentSpawnInitializer<GameplayAttributeSetComponent>(
                        GameplayAttributeComponentSchemaDescriptors.AttributesStableId,
                        new GameplayAttributeSetComponent(
                            new GameplayAttributeValue(HpAttributeId, hp, hp),
                            new GameplayAttributeValue(ManaAttributeId, mana, mana),
                            new GameplayAttributeValue(AttackAttributeId, attack, attack)))
                });
        }

        private static GameplayComponentAbilityRegistry CreateAbilityRegistry()
        {
            var registry = new GameplayComponentAbilityRegistry();
            registry.Register(new GameplayComponentAttributeDeltaAbility(
                StrikeAbilityId,
                HpAttributeId,
                -6,
                GameplayComponentTargetMode.ExplicitSingle,
                new GameplayComponentAbilityRuleSet(
                    cooldownFrames: 2,
                    costs: new[] { new GameplayAbilityCost(ManaAttributeId, 3) })));
            return registry;
        }

        private static string FormatEvent(GameplayRuntimeEvent evt)
        {
            string entity = evt.TryGetComponentEntityId(out GameplayEntityId entityId)
                ? " entity=" + entityId.Index + ":" + entityId.Generation
                : string.Empty;
            string attribute = evt.AttributeId > 0
                ? " attr=" + evt.AttributeId + " " + evt.OldAttributeValue + "->" + evt.NewAttributeValue
                : string.Empty;
            return "f" + evt.Frame.Value + " " + evt.Type + " " + evt.Reason + entity + attribute;
        }
    }

    public readonly struct GameplayComponentRuntimeShowcaseSnapshot
    {
        public GameplayComponentRuntimeShowcaseSnapshot(
            RuntimeFrame frame,
            long nextFrame,
            GameplayEntityId heroEntityId,
            GameplayEntityId enemyEntityId,
            bool heroAlive,
            bool enemyAlive,
            GameplayLifecycleState heroLifecycle,
            GameplayLifecycleState enemyLifecycle,
            int heroHp,
            int heroMana,
            int enemyHp,
            int enemyMana,
            long strikeCooldownRemainingFrames,
            int pendingRequests,
            int pendingCommands,
            long hash,
            string saveStatus,
            string[] eventLog)
        {
            Frame = frame;
            NextFrame = nextFrame;
            HeroEntityId = heroEntityId;
            EnemyEntityId = enemyEntityId;
            HeroAlive = heroAlive;
            EnemyAlive = enemyAlive;
            HeroLifecycle = heroLifecycle;
            EnemyLifecycle = enemyLifecycle;
            HeroHp = heroHp;
            HeroMana = heroMana;
            EnemyHp = enemyHp;
            EnemyMana = enemyMana;
            StrikeCooldownRemainingFrames = strikeCooldownRemainingFrames;
            PendingRequests = pendingRequests;
            PendingCommands = pendingCommands;
            Hash = hash;
            SaveStatus = saveStatus ?? string.Empty;
            EventLog = eventLog ?? Array.Empty<string>();
        }

        public RuntimeFrame Frame { get; }
        public long NextFrame { get; }
        public GameplayEntityId HeroEntityId { get; }
        public GameplayEntityId EnemyEntityId { get; }
        public bool HeroAlive { get; }
        public bool EnemyAlive { get; }
        public GameplayLifecycleState HeroLifecycle { get; }
        public GameplayLifecycleState EnemyLifecycle { get; }
        public int HeroHp { get; }
        public int HeroMana { get; }
        public int EnemyHp { get; }
        public int EnemyMana { get; }
        public long StrikeCooldownRemainingFrames { get; }
        public int PendingRequests { get; }
        public int PendingCommands { get; }
        public long Hash { get; }
        public string SaveStatus { get; }
        public IReadOnlyList<string> EventLog { get; }
    }
}
