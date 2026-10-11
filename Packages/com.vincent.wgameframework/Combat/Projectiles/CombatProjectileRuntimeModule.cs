using System;
using System.Collections.Generic;
using MxFramework.Combat.Core;
using MxFramework.Combat.Hit;
using MxFramework.Combat.Physics;
using MxFramework.Runtime;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

namespace MxFramework.Combat.Projectiles
{
    public sealed class CombatProjectileRuntimeModule : RuntimeModule
    {
        public const string DefaultModuleId = "combat.projectile";
        public const int DefaultPriority = 120;

        private readonly CombatProjectileDefinitionRegistry _definitions;
        private readonly ICombatProjectileSpawnSource _spawnSource;
        private readonly CombatPhysicsWorld _physicsWorld;
        private readonly ICombatProjectileImpactResolver _impactResolver;
        private readonly ITeamRelationProvider _teamRelationProvider;
        private readonly IHitTargetStateResolver _targetStateResolver;
        private readonly bool _allowFriendlyFire;
        private CombatFixedStepDriver _fixedStepDriver;
        private CombatProjectileRuntime _runtime;

        public CombatProjectileRuntimeModule(
            CombatProjectileDefinitionRegistry definitions = null,
            ICombatProjectileSpawnSource spawnSource = null,
            CombatPhysicsWorld physicsWorld = null,
            ICombatProjectileImpactResolver impactResolver = null,
            ITeamRelationProvider teamRelationProvider = null,
            IHitTargetStateResolver targetStateResolver = null,
            bool allowFriendlyFire = false,
            int priority = DefaultPriority)
            : base(DefaultModuleId, RuntimeTickStage.Simulation, priority)
        {
            _definitions = definitions;
            _spawnSource = spawnSource;
            _physicsWorld = physicsWorld;
            _impactResolver = impactResolver;
            _teamRelationProvider = teamRelationProvider;
            _targetStateResolver = targetStateResolver;
            _allowFriendlyFire = allowFriendlyFire;
        }

        public CombatProjectileRuntime Runtime => _runtime;

        public override void Initialize(RuntimeHostContext context)
        {
            if (context == null)
                throw new ArgumentNullException(nameof(context));

            CombatProjectileDefinitionRegistry definitions = _definitions ?? context.Services.Get<CombatProjectileDefinitionRegistry>();
            ICombatProjectileSpawnSource spawnSource = _spawnSource ?? context.Services.Get<ICombatProjectileSpawnSource>();
            CombatPhysicsWorld physicsWorld = _physicsWorld ?? context.Services.Get<CombatPhysicsWorld>();
            ICombatProjectileImpactResolver impactResolver = _impactResolver ?? context.Services.Get<ICombatProjectileImpactResolver>();
            ITeamRelationProvider teamRelationProvider = _teamRelationProvider;
            IHitTargetStateResolver targetStateResolver = _targetStateResolver;
            ICombatProjectileTrackingTargetProvider trackingTargetProvider = null;
            if (teamRelationProvider == null)
                context.Services.TryGet(out teamRelationProvider);
            if (targetStateResolver == null)
                context.Services.TryGet(out targetStateResolver);
            context.Services.TryGet(out trackingTargetProvider);

            _fixedStepDriver = GetOrCreateFixedStepDriver(context);
            _runtime = new CombatProjectileRuntime(
                definitions,
                spawnSource,
                physicsWorld,
                impactResolver,
                teamRelationProvider,
                targetStateResolver,
                _allowFriendlyFire,
                trackingTargetProvider);
        }

        /// <remarks>NoAlloc on the steady-state path after registration/initialization; invalid-call and exception paths may allocate diagnostics.</remarks>
        public override void Tick(RuntimeTickContext context)
        {
            if (context.Stage != TickStage || _runtime == null)
                return;

            CombatFixedStepBatch batch = _fixedStepDriver.Advance(context);
            for (int i = 0; i < batch.StepCount; i++)
                _runtime.Tick(batch.GetStepFrame(i));
        }

        public override void Stop(RuntimeHostContext context)
        {
        }

        public override void Dispose()
        {
            _runtime = null;
            _fixedStepDriver = null;
        }

        /// <summary>Borrowed synchronous state view; do not retain it for rollback or asynchronous consumers.</summary>
        public CombatProjectileRuntimeState ReadStateView()
        {
            if (_runtime == null) throw new InvalidOperationException("Combat projectile runtime module is not initialized.");
            return _runtime.ReadStateView(ModuleId);
        }

        public CombatProjectileRuntimeState CreateStateSnapshot()
        {
            if (_runtime == null)
                throw new InvalidOperationException("Combat projectile runtime module is not initialized.");

            return _runtime.CreateStateSnapshot(ModuleId);
        }

        public CombatProjectileDebugSnapshot CreateDebugSnapshot()
        {
            if (_runtime == null)
                throw new InvalidOperationException("Combat projectile runtime module is not initialized.");

            return _runtime.CreateDebugSnapshot(ModuleId);
        }

        internal CombatProjectileRuntimeState CaptureRuntimeState()
        {
            return CreateStateSnapshot();
        }

        internal void RestoreRuntimeState(CombatProjectileRuntimeState state)
        {
            if (_runtime == null)
                throw new InvalidOperationException("Combat projectile runtime module is not initialized.");

            _runtime.RestoreRuntimeState(state);
        }

        private static CombatFixedStepDriver GetOrCreateFixedStepDriver(RuntimeHostContext context)
        {
            if (context.Services.TryGet(out CombatFixedStepDriver driver))
                return driver;

            driver = new CombatFixedStepDriver();
            if (context.Services is RuntimeServiceRegistry registry)
                registry.Register(driver);

            return driver;
        }
    }

    internal static class CombatProjectileRuntimeHashFields
    {
        public const int DefinitionHash = 1;
        public const int NextProjectileId = 2;
        public const int ActiveCount = 3;
        public const int ActiveProjectiles = 4;
    }

    public sealed class CombatProjectileRuntimeHashContributor : IRuntimeHashContributor
    {
        private readonly CombatProjectileRuntime _runtime;
        private readonly string _moduleId;

        public CombatProjectileRuntimeHashContributor(
            CombatProjectileRuntime runtime,
            string moduleId = CombatProjectileRuntimeModule.DefaultModuleId)
        {
            _runtime = runtime ?? throw new ArgumentNullException(nameof(runtime));
            _moduleId = moduleId ?? string.Empty;
        }

        public CombatProjectileRuntimeHashContributor(CombatProjectileRuntimeModule module)
        {
            if (module == null)
                throw new ArgumentNullException(nameof(module));
            if (module.Runtime == null)
                throw new InvalidOperationException("Combat projectile runtime module is not initialized.");

            _runtime = module.Runtime;
            _moduleId = module.ModuleId;
        }

        public string DiagnosticContributorId => _moduleId;
        public string ContributorIdString => _moduleId;
        public int ContributorId => RuntimeHashContributorIds.CombatProjectile;
        public int HashSchemaId => RuntimeHashSchemaIds.CombatProjectile;
        public int HashSchemaVersion => RuntimeHashSchemaIds.Version1;

        public void Contribute(RuntimeHashContext context, RuntimeHashAccumulator accumulator)
        {
            if (accumulator == null)
                throw new ArgumentNullException(nameof(accumulator));

            CombatProjectileRuntimeState state = _runtime.CreateStateSnapshot(_moduleId);
            accumulator.AddLong(CombatProjectileRuntimeHashFields.DefinitionHash, state.DefinitionHash);
            accumulator.AddInt(CombatProjectileRuntimeHashFields.NextProjectileId, state.NextProjectileId);
            accumulator.AddInt(CombatProjectileRuntimeHashFields.ActiveCount, state.ActiveProjectiles.Count);
            accumulator.BeginArray(CombatProjectileRuntimeHashFields.ActiveProjectiles, state.ActiveProjectiles.Count);
            for (int i = 0; i < state.ActiveProjectiles.Count; i++)
            {
                accumulator.BeginElement(i);
                state.ActiveProjectiles[i].Contribute(accumulator);
                accumulator.EndElement();
            }
            accumulator.EndArray();
        }
    }

    public sealed class CombatProjectileRuntimeSaveStateProvider :
        IRuntimeSaveStateProvider,
        IRuntimeSaveStateRestorer
    {
        public const string CustomStateTypeId = "mxframework.combat.projectile.runtime";
        public const int CustomStateSchemaVersion = CombatProjectileRuntimeState.CurrentSchemaVersion;

        private static readonly JsonSerializerSettings JsonSettings = new JsonSerializerSettings
        {
            ContractResolver = new CamelCasePropertyNamesContractResolver(),
            Formatting = Formatting.None
        };

        private readonly CombatProjectileRuntime _runtime;
        private readonly string _moduleId;

        public CombatProjectileRuntimeSaveStateProvider(
            CombatProjectileRuntime runtime,
            string moduleId = CombatProjectileRuntimeModule.DefaultModuleId)
        {
            _runtime = runtime ?? throw new ArgumentNullException(nameof(runtime));
            _moduleId = moduleId ?? string.Empty;
        }

        public CombatProjectileRuntimeSaveStateProvider(CombatProjectileRuntimeModule module)
        {
            if (module == null)
                throw new ArgumentNullException(nameof(module));
            if (module.Runtime == null)
                throw new InvalidOperationException("Combat projectile runtime module is not initialized.");

            _runtime = module.Runtime;
            _moduleId = module.ModuleId;
        }

        public RuntimeSaveStateResult<RuntimeSaveState> CaptureSaveState()
        {
            CombatProjectileRuntimeState state = _runtime.CreateStateSnapshot(_moduleId);
            string payloadJson = JsonConvert.SerializeObject(CombatProjectileRuntimeStateDto.FromState(state), JsonSettings);
            var customState = new RuntimeCustomState(3312, CustomStateTypeId, CustomStateSchemaVersion, payloadJson);
            var moduleState = new RuntimeModuleSaveState(_moduleId, CustomStateSchemaVersion, customState);
            var saveState = new RuntimeSaveState(
                RuntimeSaveState.CurrentSchemaVersion,
                DateTime.UtcNow,
                frameworkVersion: string.Empty,
                configVersion: string.Empty,
                resourceCatalogVersion: string.Empty,
                frame: 0L,
                entities: null,
                globalCounters: null,
                moduleStates: new[] { moduleState });
            return RuntimeSaveStateResult<RuntimeSaveState>.Succeeded(saveState);
        }

        public RuntimeSaveStateResult<bool> RestoreSaveState(RuntimeSaveState saveState)
        {
            if (saveState == null)
            {
                return RuntimeSaveStateResult<bool>.Failed(new RuntimeSaveStateError(
                    RuntimeSaveStateErrorCode.InvalidDocument,
                    "$",
                    "Combat projectile restore state is missing."));
            }

            RuntimeModuleSaveState moduleState = null;
            for (int i = 0; i < saveState.ModuleStates.Count; i++)
            {
                RuntimeModuleSaveState candidate = saveState.ModuleStates[i];
                if (candidate != null && string.Equals(candidate.ModuleId, _moduleId, StringComparison.Ordinal))
                {
                    moduleState = candidate;
                    break;
                }
            }

            if (moduleState == null || moduleState.CustomState == null)
            {
                return RuntimeSaveStateResult<bool>.Failed(new RuntimeSaveStateError(
                    RuntimeSaveStateErrorCode.InvalidDocument,
                    "$.moduleStates",
                    "Combat projectile module state was not found."));
            }

            if (!string.Equals(moduleState.CustomState.TypeId, CustomStateTypeId, StringComparison.Ordinal))
            {
                return RuntimeSaveStateResult<bool>.Failed(new RuntimeSaveStateError(
                    RuntimeSaveStateErrorCode.CustomStateMismatch,
                    "$.moduleStates['" + _moduleId + "']",
                    "Combat projectile custom state type mismatch."));
            }

            if (moduleState.CustomState.SchemaVersion != CustomStateSchemaVersion)
            {
                return RuntimeSaveStateResult<bool>.Failed(new RuntimeSaveStateError(
                    RuntimeSaveStateErrorCode.UnsupportedVersion,
                    "$.moduleStates['" + _moduleId + "']",
                    "Unsupported Combat projectile custom state schema.",
                    moduleState.CustomState.SchemaVersion,
                    CustomStateSchemaVersion));
            }

            try
            {
                CombatProjectileRuntimeStateDto dto = JsonConvert.DeserializeObject<CombatProjectileRuntimeStateDto>(
                    moduleState.CustomState.PayloadJson,
                    JsonSettings);
                if (dto == null)
                    throw new JsonException("Combat projectile state payload was empty.");

                CombatProjectileRuntimeState state = dto.ToState(_moduleId);
                if (state.SchemaVersion != CombatProjectileRuntimeState.CurrentSchemaVersion)
                {
                    return RuntimeSaveStateResult<bool>.Failed(new RuntimeSaveStateError(
                        RuntimeSaveStateErrorCode.UnsupportedVersion,
                        "$.moduleStates['" + _moduleId + "'].customState.payloadJson.schemaVersion",
                        "Unsupported Combat projectile runtime state schema.",
                        state.SchemaVersion,
                        CombatProjectileRuntimeState.CurrentSchemaVersion));
                }

                long currentDefinitionHash = _runtime.DefinitionHash;
                if (state.DefinitionHash != currentDefinitionHash)
                {
                    return RuntimeSaveStateResult<bool>.Failed(new RuntimeSaveStateError(
                        RuntimeSaveStateErrorCode.CustomStateMismatch,
                        "$.moduleStates['" + _moduleId + "'].customState.definitionHash",
                        "Combat projectile definition hash mismatch."));
                }

                _runtime.RestoreRuntimeState(state);
                return RuntimeSaveStateResult<bool>.Succeeded(true);
            }
            catch (Exception ex)
            {
                return RuntimeSaveStateResult<bool>.Failed(new RuntimeSaveStateError(
                    RuntimeSaveStateErrorCode.InvalidDocument,
                    "$.moduleStates['" + _moduleId + "'].customState.payloadJson",
                    "Combat projectile custom state could not be parsed: " + ex.Message,
                    exception: ex));
            }
        }

        private sealed class CombatProjectileRuntimeStateDto
        {
            public int schemaVersion;
            public long definitionHash;
            public int nextProjectileId;
            public CombatProjectileSnapshotDto[] activeProjectiles;

            public static CombatProjectileRuntimeStateDto FromState(CombatProjectileRuntimeState state)
            {
                return new CombatProjectileRuntimeStateDto
                {
                    schemaVersion = state.SchemaVersion,
                    definitionHash = state.DefinitionHash,
                    nextProjectileId = state.NextProjectileId,
                    activeProjectiles = Convert(state.ActiveProjectiles)
                };
            }

            public CombatProjectileRuntimeState ToState(string moduleId)
            {
                return new CombatProjectileRuntimeState(
                    schemaVersion,
                    moduleId,
                    definitionHash,
                    nextProjectileId,
                    Convert(activeProjectiles));
            }

            private static CombatProjectileSnapshotDto[] Convert(IReadOnlyList<CombatProjectileSnapshot> values)
            {
                if (values == null || values.Count == 0)
                    return Array.Empty<CombatProjectileSnapshotDto>();

                var result = new CombatProjectileSnapshotDto[values.Count];
                for (int i = 0; i < values.Count; i++)
                    result[i] = CombatProjectileSnapshotDto.From(values[i]);

                return result;
            }

            private static List<CombatProjectileSnapshot> Convert(CombatProjectileSnapshotDto[] values)
            {
                var result = new List<CombatProjectileSnapshot>();
                if (values == null)
                    return result;

                for (int i = 0; i < values.Length; i++)
                    result.Add(values[i].ToSnapshot());

                return result;
            }
        }

        private sealed class CombatProjectileSnapshotDto
        {
            public int projectileId;
            public int spawnSequence;
            public int definitionId;
            public string definitionStableId;
            public RuntimeHashDigest definitionLabelDigest;
            public int sourceEntityId;
            public int actionId;
            public int actionInstanceId;
            public int traceId;
            public int localFrame;
            public int spawnFrame;
            public int lastFrame;
            public int ageFrames;
            public long positionX;
            public long positionY;
            public long positionZ;
            public long directionX;
            public long directionY;
            public long directionZ;
            public long originX;
            public long originY;
            public long originZ;
            public bool hasTargetPoint;
            public long targetPointX;
            public long targetPointY;
            public long targetPointZ;
            public int effectiveLifetimeFrames;
            public long launchSpeedPerFrame;
            public bool hasTrackingTarget;
            public int trackingTargetEntityId;
            public CombatProjectileHitRecordDto[] acceptedImpacts;

            public static CombatProjectileSnapshotDto From(CombatProjectileSnapshot snapshot)
            {
                return new CombatProjectileSnapshotDto
                {
                    projectileId = snapshot.ProjectileId,
                    spawnSequence = snapshot.SpawnSequence,
                    definitionId = snapshot.DefinitionId,
                    definitionStableId = snapshot.DefinitionStableId,
                    definitionLabelDigest = snapshot.DefinitionLabelDigest,
                    sourceEntityId = snapshot.SourceEntityId.Value,
                    actionId = snapshot.ActionId,
                    actionInstanceId = snapshot.ActionInstanceId,
                    traceId = snapshot.TraceId,
                    localFrame = snapshot.LocalFrame,
                    spawnFrame = snapshot.SpawnFrame.Value,
                    lastFrame = snapshot.LastFrame.Value,
                    ageFrames = snapshot.AgeFrames,
                    positionX = snapshot.Position.X.RawValue,
                    positionY = snapshot.Position.Y.RawValue,
                    positionZ = snapshot.Position.Z.RawValue,
                    directionX = snapshot.Direction.X.RawValue,
                    directionY = snapshot.Direction.Y.RawValue,
                    directionZ = snapshot.Direction.Z.RawValue,
                    originX = snapshot.Origin.X.RawValue,
                    originY = snapshot.Origin.Y.RawValue,
                    originZ = snapshot.Origin.Z.RawValue,
                    hasTargetPoint = snapshot.HasTargetPoint,
                    targetPointX = snapshot.TargetPoint.X.RawValue,
                    targetPointY = snapshot.TargetPoint.Y.RawValue,
                    targetPointZ = snapshot.TargetPoint.Z.RawValue,
                    effectiveLifetimeFrames = snapshot.EffectiveLifetimeFrames,
                    launchSpeedPerFrame = snapshot.LaunchSpeedPerFrame.RawValue,
                    hasTrackingTarget = snapshot.HasTrackingTarget,
                    trackingTargetEntityId = snapshot.TrackingTargetEntityId.Value,
                    acceptedImpacts = CombatProjectileHitRecordDto.FromRecords(snapshot.AcceptedImpacts)
                };
            }

            public CombatProjectileSnapshot ToSnapshot()
            {
                return new CombatProjectileSnapshot(
                    projectileId,
                    spawnSequence,
                    definitionId,
                    definitionStableId,
                    new CombatEntityId(sourceEntityId),
                    actionId,
                    actionInstanceId,
                    traceId,
                    localFrame,
                    new CombatFrame(spawnFrame),
                    new CombatFrame(lastFrame),
                    ageFrames,
                    new MxFramework.Core.Math.FixVector3(
                        MxFramework.Core.Math.Fix64.FromRaw(positionX),
                        MxFramework.Core.Math.Fix64.FromRaw(positionY),
                        MxFramework.Core.Math.Fix64.FromRaw(positionZ)),
                    new MxFramework.Core.Math.FixVector3(
                        MxFramework.Core.Math.Fix64.FromRaw(directionX),
                        MxFramework.Core.Math.Fix64.FromRaw(directionY),
                        MxFramework.Core.Math.Fix64.FromRaw(directionZ)),
                    new MxFramework.Core.Math.FixVector3(
                        MxFramework.Core.Math.Fix64.FromRaw(originX),
                        MxFramework.Core.Math.Fix64.FromRaw(originY),
                        MxFramework.Core.Math.Fix64.FromRaw(originZ)),
                    hasTargetPoint,
                    new MxFramework.Core.Math.FixVector3(
                        MxFramework.Core.Math.Fix64.FromRaw(targetPointX),
                        MxFramework.Core.Math.Fix64.FromRaw(targetPointY),
                        MxFramework.Core.Math.Fix64.FromRaw(targetPointZ)),
                    CombatProjectileHitRecordDto.ToRecords(acceptedImpacts),
                    effectiveLifetimeFrames,
                    hasTrackingTarget,
                    new CombatEntityId(trackingTargetEntityId),
                    MxFramework.Core.Math.Fix64.FromRaw(launchSpeedPerFrame),
                    definitionLabelDigest: definitionLabelDigest);
            }
        }

        private sealed class CombatProjectileHitRecordDto
        {
            public int targetEntityId;
            public int frame;

            public static CombatProjectileHitRecordDto[] FromRecords(
                IReadOnlyList<CombatProjectileHitRecord> records)
            {
                if (records == null || records.Count == 0)
                    return Array.Empty<CombatProjectileHitRecordDto>();

                var result = new CombatProjectileHitRecordDto[records.Count];
                for (int i = 0; i < records.Count; i++)
                {
                    result[i] = new CombatProjectileHitRecordDto
                    {
                        targetEntityId = records[i].TargetEntityId.Value,
                        frame = records[i].Frame.Value
                    };
                }

                return result;
            }

            public static List<CombatProjectileHitRecord> ToRecords(
                CombatProjectileHitRecordDto[] values)
            {
                var result = new List<CombatProjectileHitRecord>();
                if (values == null || values.Length == 0)
                    return result;

                for (int i = 0; i < values.Length; i++)
                {
                    CombatProjectileHitRecordDto value = values[i];
                    if (value == null || value.targetEntityId <= 0)
                        continue;

                    result.Add(new CombatProjectileHitRecord(
                        new CombatEntityId(value.targetEntityId),
                        new CombatFrame(value.frame)));
                }

                return result;
            }
        }
    }
}
