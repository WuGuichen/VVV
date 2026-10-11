using System;
using System.Collections.Generic;
using MxFramework.Combat.Core;
using MxFramework.Combat.Physics;
using MxFramework.Runtime;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

namespace MxFramework.Combat.Shields
{
    public sealed class CombatShieldRuntimeModule : RuntimeModule
    {
        public const string DefaultModuleId = "combat.shield";
        public const int DefaultModuleIdentity = 3202;
        public const int DefaultPriority = 115;

        public int ModuleIdentity => DefaultModuleIdentity;

        private readonly CombatPhysicsWorld _physicsWorld;
        private readonly CombatShieldRuntime _providedRuntime;
        private CombatFixedStepDriver _fixedStepDriver;
        private CombatShieldRuntime _runtime;

        public CombatShieldRuntimeModule(
            CombatPhysicsWorld physicsWorld = null,
            CombatShieldRuntime runtime = null,
            int priority = DefaultPriority)
            : base(DefaultModuleId, RuntimeTickStage.Simulation, priority)
        {
            _physicsWorld = physicsWorld;
            _providedRuntime = runtime;
        }

        public CombatShieldRuntime Runtime => _runtime;

        public override void Initialize(RuntimeHostContext context)
        {
            if (context == null)
                throw new ArgumentNullException(nameof(context));

            CombatPhysicsWorld physicsWorld = _physicsWorld;
            if (physicsWorld == null)
                context.Services.TryGet(out physicsWorld);

            _fixedStepDriver = GetOrCreateFixedStepDriver(context);
            _runtime = _providedRuntime ?? new CombatShieldRuntime(physicsWorld);
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

        public override void Dispose()
        {
            _runtime = null;
            _fixedStepDriver = null;
        }

        public CombatShieldRuntimeState CreateStateSnapshot()
        {
            if (_runtime == null)
                throw new InvalidOperationException("Combat shield runtime module is not initialized.");

            return _runtime.CreateStateSnapshot(ModuleId);
        }

        public CombatShieldDebugSnapshot CreateDebugSnapshot()
        {
            if (_runtime == null)
                throw new InvalidOperationException("Combat shield runtime module is not initialized.");

            return _runtime.CreateDebugSnapshot(ModuleId);
        }

        internal CombatShieldRuntimeState CaptureRuntimeState()
        {
            return CreateStateSnapshot();
        }

        internal void RestoreRuntimeState(CombatShieldRuntimeState state)
        {
            if (_runtime == null)
                throw new InvalidOperationException("Combat shield runtime module is not initialized.");

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

    internal static class CombatShieldRuntimeHashFields
    {
        public const int ShieldCount = 1;
        public const int Shields = 2;
    }

    internal static class CombatShieldHashFields
    {
        public const int ShieldEntityId = 1;
        public const int OwnerEntityId = 2;
        public const int BodyId = 3;
        public const int ColliderId = 4;
        public const int BaseMotion = 5;
        public const int BaseCohesion = 6;
        public const int BaseScatter = 7;
        public const int CurrentMotion = 8;
        public const int CurrentCohesion = 9;
        public const int CurrentScatter = 10;
        public const int BaseCohesionLimit = 11;
        public const int CurrentCohesionLimit = 12;
        public const int RecoveryRate = 13;
        public const int RecoveryDelayFrames = 14;
        public const int LastImpactFrame = 15;
        public const int CurrentBand = 16;
        public const int Broken = 17;
    }

    public sealed class CombatShieldRuntimeHashContributor : IRuntimeHashContributor
    {
        private readonly CombatShieldRuntime _runtime;
        private readonly string _moduleId;

        public CombatShieldRuntimeHashContributor(
            CombatShieldRuntime runtime,
            string moduleId = CombatShieldRuntimeModule.DefaultModuleId)
        {
            _runtime = runtime ?? throw new ArgumentNullException(nameof(runtime));
            _moduleId = moduleId ?? string.Empty;
        }

        public CombatShieldRuntimeHashContributor(CombatShieldRuntimeModule module)
        {
            if (module == null)
                throw new ArgumentNullException(nameof(module));
            if (module.Runtime == null)
                throw new InvalidOperationException("Combat shield runtime module is not initialized.");

            _runtime = module.Runtime;
            _moduleId = module.ModuleId;
        }

        public string DiagnosticContributorId => _moduleId;
        public string ContributorIdString => _moduleId;
        public int ContributorId => RuntimeHashContributorIds.CombatShield;
        public int HashSchemaId => RuntimeHashSchemaIds.CombatShield;
        public int HashSchemaVersion => RuntimeHashSchemaIds.Version1;

        public void Contribute(RuntimeHashContext context, RuntimeHashAccumulator accumulator)
        {
            if (accumulator == null)
                throw new ArgumentNullException(nameof(accumulator));

            CombatShieldRuntimeState state = _runtime.CreateStateSnapshot(_moduleId);
            accumulator.AddInt(CombatShieldRuntimeHashFields.ShieldCount, state.Shields.Count);
            accumulator.BeginArray(CombatShieldRuntimeHashFields.Shields, state.Shields.Count);
            for (int i = 0; i < state.Shields.Count; i++)
            {
                accumulator.BeginElement(i);
                ContributeShield(accumulator, state.Shields[i]);
                accumulator.EndElement();
            }
            accumulator.EndArray();
        }

        private static void ContributeShield(
            RuntimeHashAccumulator accumulator,
            CombatShieldPotencyComponent shield)
        {
            accumulator.AddInt(CombatShieldHashFields.ShieldEntityId, shield.ShieldEntityId.Value);
            accumulator.AddInt(CombatShieldHashFields.OwnerEntityId, shield.OwnerEntityId.Value);
            accumulator.AddInt(CombatShieldHashFields.BodyId, shield.BodyId.Value);
            accumulator.AddInt(CombatShieldHashFields.ColliderId, shield.ColliderId.Value);
            accumulator.AddInt(CombatShieldHashFields.BaseMotion, shield.BasePotency.Motion);
            accumulator.AddInt(CombatShieldHashFields.BaseCohesion, shield.BasePotency.Cohesion);
            accumulator.AddInt(CombatShieldHashFields.BaseScatter, shield.BasePotency.Scatter);
            accumulator.AddInt(CombatShieldHashFields.CurrentMotion, shield.CurrentPotency.Motion);
            accumulator.AddInt(CombatShieldHashFields.CurrentCohesion, shield.CurrentPotency.Cohesion);
            accumulator.AddInt(CombatShieldHashFields.CurrentScatter, shield.CurrentPotency.Scatter);
            accumulator.AddInt(CombatShieldHashFields.BaseCohesionLimit, shield.BaseCohesionLimit);
            accumulator.AddInt(CombatShieldHashFields.CurrentCohesionLimit, shield.CurrentCohesionLimit);
            accumulator.AddInt(CombatShieldHashFields.RecoveryRate, shield.RecoveryRate);
            accumulator.AddInt(CombatShieldHashFields.RecoveryDelayFrames, shield.RecoveryDelayFrames);
            accumulator.AddLong(CombatShieldHashFields.LastImpactFrame, shield.LastImpactFrame);
            accumulator.AddInt(CombatShieldHashFields.CurrentBand, (int)shield.CurrentBand);
            accumulator.AddInt(CombatShieldHashFields.Broken, shield.IsBroken ? 1 : 0);
        }
    }

    public sealed class CombatShieldRuntimeSaveStateProvider :
        IRuntimeSaveStateProvider,
        IRuntimeSaveStateRestorer
    {
        public const string CustomStateTypeId = "mxframework.combat.shield.runtime";
        public const int CustomStateSchemaVersion = CombatShieldRuntimeState.CurrentSchemaVersion;

        private static readonly JsonSerializerSettings JsonSettings = new JsonSerializerSettings
        {
            ContractResolver = new CamelCasePropertyNamesContractResolver(),
            Formatting = Formatting.None
        };

        private readonly CombatShieldRuntime _runtime;
        private readonly string _moduleId;

        public CombatShieldRuntimeSaveStateProvider(
            CombatShieldRuntime runtime,
            string moduleId = CombatShieldRuntimeModule.DefaultModuleId)
        {
            _runtime = runtime ?? throw new ArgumentNullException(nameof(runtime));
            _moduleId = moduleId ?? string.Empty;
        }

        public CombatShieldRuntimeSaveStateProvider(CombatShieldRuntimeModule module)
        {
            if (module == null)
                throw new ArgumentNullException(nameof(module));
            if (module.Runtime == null)
                throw new InvalidOperationException("Combat shield runtime module is not initialized.");

            _runtime = module.Runtime;
            _moduleId = module.ModuleId;
        }

        public RuntimeSaveStateResult<RuntimeSaveState> CaptureSaveState()
        {
            CombatShieldRuntimeState state = _runtime.CreateStateSnapshot(_moduleId);
            string payloadJson = JsonConvert.SerializeObject(CombatShieldRuntimeStateDto.FromState(state), JsonSettings);
            var customState = new RuntimeCustomState(3313, CustomStateTypeId, CustomStateSchemaVersion, payloadJson);
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
                    "Combat shield restore state is missing."));
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
                    "Combat shield module state was not found."));
            }

            if (!string.Equals(moduleState.CustomState.TypeId, CustomStateTypeId, StringComparison.Ordinal))
            {
                return RuntimeSaveStateResult<bool>.Failed(new RuntimeSaveStateError(
                    RuntimeSaveStateErrorCode.CustomStateMismatch,
                    "$.moduleStates['" + _moduleId + "']",
                    "Combat shield custom state type mismatch."));
            }

            if (moduleState.CustomState.SchemaVersion != CustomStateSchemaVersion)
            {
                return RuntimeSaveStateResult<bool>.Failed(new RuntimeSaveStateError(
                    RuntimeSaveStateErrorCode.UnsupportedVersion,
                    "$.moduleStates['" + _moduleId + "']",
                    "Unsupported Combat shield custom state schema.",
                    moduleState.CustomState.SchemaVersion,
                    CustomStateSchemaVersion));
            }

            try
            {
                CombatShieldRuntimeStateDto dto = JsonConvert.DeserializeObject<CombatShieldRuntimeStateDto>(
                    moduleState.CustomState.PayloadJson,
                    JsonSettings);
                if (dto == null)
                    throw new JsonException("Combat shield state payload was empty.");

                CombatShieldRuntimeState state = dto.ToState(_moduleId);
                if (state.SchemaVersion != CombatShieldRuntimeState.CurrentSchemaVersion)
                {
                    return RuntimeSaveStateResult<bool>.Failed(new RuntimeSaveStateError(
                        RuntimeSaveStateErrorCode.UnsupportedVersion,
                        "$.moduleStates['" + _moduleId + "'].customState.payloadJson.schemaVersion",
                        "Unsupported Combat shield runtime state schema.",
                        state.SchemaVersion,
                        CombatShieldRuntimeState.CurrentSchemaVersion));
                }

                _runtime.RestoreRuntimeState(state);
                return RuntimeSaveStateResult<bool>.Succeeded(true);
            }
            catch (Exception ex)
            {
                return RuntimeSaveStateResult<bool>.Failed(new RuntimeSaveStateError(
                    RuntimeSaveStateErrorCode.InvalidDocument,
                    "$.moduleStates['" + _moduleId + "'].customState.payloadJson",
                    "Combat shield custom state could not be parsed: " + ex.Message,
                    exception: ex));
            }
        }

        private sealed class CombatShieldRuntimeStateDto
        {
            public int schemaVersion;
            public CombatShieldSnapshotDto[] shields;

            public static CombatShieldRuntimeStateDto FromState(CombatShieldRuntimeState state)
            {
                return new CombatShieldRuntimeStateDto
                {
                    schemaVersion = state.SchemaVersion,
                    shields = Convert(state.Shields)
                };
            }

            public CombatShieldRuntimeState ToState(string moduleId)
            {
                return new CombatShieldRuntimeState(
                    schemaVersion,
                    moduleId,
                    Convert(shields));
            }

            private static CombatShieldSnapshotDto[] Convert(IReadOnlyList<CombatShieldPotencyComponent> values)
            {
                if (values == null || values.Count == 0)
                    return Array.Empty<CombatShieldSnapshotDto>();

                var result = new CombatShieldSnapshotDto[values.Count];
                for (int i = 0; i < values.Count; i++)
                    result[i] = CombatShieldSnapshotDto.From(values[i]);

                return result;
            }

            private static List<CombatShieldPotencyComponent> Convert(CombatShieldSnapshotDto[] values)
            {
                var result = new List<CombatShieldPotencyComponent>();
                if (values == null)
                    return result;

                for (int i = 0; i < values.Length; i++)
                {
                    CombatShieldSnapshotDto value = values[i];
                    if (value == null || value.shieldEntityId <= 0 || value.ownerEntityId <= 0)
                        continue;

                    result.Add(value.ToComponent());
                }

                return result;
            }
        }

        private sealed class CombatShieldSnapshotDto
        {
            public int shieldEntityId;
            public int ownerEntityId;
            public int bodyId;
            public int colliderId;
            public int baseMotion;
            public int baseCohesion;
            public int baseScatter;
            public int currentMotion;
            public int currentCohesion;
            public int currentScatter;
            public int baseCohesionLimit;
            public int currentCohesionLimit;
            public int recoveryRate;
            public int recoveryDelayFrames;
            public long lastImpactFrame;
            public int currentBand;
            public bool isBroken;

            public static CombatShieldSnapshotDto From(CombatShieldPotencyComponent value)
            {
                return new CombatShieldSnapshotDto
                {
                    shieldEntityId = value.ShieldEntityId.Value,
                    ownerEntityId = value.OwnerEntityId.Value,
                    bodyId = value.BodyId.Value,
                    colliderId = value.ColliderId.Value,
                    baseMotion = value.BasePotency.Motion,
                    baseCohesion = value.BasePotency.Cohesion,
                    baseScatter = value.BasePotency.Scatter,
                    currentMotion = value.CurrentPotency.Motion,
                    currentCohesion = value.CurrentPotency.Cohesion,
                    currentScatter = value.CurrentPotency.Scatter,
                    baseCohesionLimit = value.BaseCohesionLimit,
                    currentCohesionLimit = value.CurrentCohesionLimit,
                    recoveryRate = value.RecoveryRate,
                    recoveryDelayFrames = value.RecoveryDelayFrames,
                    lastImpactFrame = value.LastImpactFrame,
                    currentBand = (int)value.CurrentBand,
                    isBroken = value.IsBroken
                };
            }

            public CombatShieldPotencyComponent ToComponent()
            {
                return new CombatShieldPotencyComponent(
                    new CombatEntityId(shieldEntityId),
                    new CombatEntityId(ownerEntityId),
                    new CombatBodyId(bodyId),
                    new CombatColliderId(colliderId),
                    new CombatShieldPotencyTriplet(baseMotion, baseCohesion, baseScatter),
                    new CombatShieldPotencyTriplet(currentMotion, currentCohesion, currentScatter),
                    baseCohesionLimit,
                    currentCohesionLimit,
                    recoveryRate,
                    recoveryDelayFrames,
                    lastImpactFrame,
                    (CombatShieldPressureBand)currentBand,
                    isBroken);
            }
        }
    }
}
