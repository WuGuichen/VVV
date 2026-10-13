using System;
using System.Collections.Generic;
using MxFramework.CharacterAction;
using MxFramework.CharacterRuntimeSpawn;
using MxFramework.Combat.Motion;
using MxFramework.Combat.Physics;
using MxFramework.Core.Math;
using MxFramework.Gameplay;
using MxFramework.Network;
using MxFramework.Runtime;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

namespace MxFramework.Game.CharacterTest
{
    public sealed partial class CharacterTestNetworkSnapshotParticipant : INetworkPeerSnapshotParticipant
    {
        public const string ParticipantIdValue = "mxframework.character-test";
        public const int NumericParticipantId = NetworkSnapshotHashFields.NumericParticipantId;
        public const int LegacySchemaVersionValue = 0;
        public const int IntegerSchemaVersionValue = 1;
        public const int SurfaceMaterialSchemaVersionValue = 2;
        public const int MovableNaturalObjectSchemaVersionValue = 3;
        public const int MovableNaturalObjectShapeSchemaVersionValue = 4;
        public const int MovableNaturalObjectPotencySchemaVersionValue = 5;
        public const int LivingNaturalAttributesSchemaVersionValue = 6;
        public const int HookPullSchemaVersionValue = 7;
        public const int ArcMineSchemaVersionValue = 8;
        public const int RuneTurretSchemaVersionValue = 9;
        public const int RuntimeAiBehaviorSelectionSchemaVersionValue = 10;
        public const int ColliderPostureSchemaVersionValue = 11;
        public const int ActionRunnerSchemaVersionValue = 12;
        public const int AbilityRuntimeSchemaVersionValue = 13;
        public const int AreaConfinementSchemaVersionValue = 14;
        public const int PoisonBottlePoolSchemaVersionValue = 15;
        public const int PhysicalShieldSchemaVersionValue = 16;
        public const int RuntimeAiPlannerStateSchemaVersionValue = 17;
        public const int VfxRuntimeSchemaVersionValue = 18;
        public const int BattleStateSchemaVersionValue = 19;
        public const int AuthorityBoundarySchemaVersionValue = 20;
        public const int MovementSpeedScaleSchemaVersionValue = 21;
        public const int TurnInPlaceSchemaVersionValue = 22;
        public const int ControlIntentSourceSchemaVersionValue = 23;
        public const int HitResolutionPolicySchemaVersionValue = 24;
        public const int HitTraceWindowSchemaVersionValue = 25;
        public const int SourceRevisionSchemaVersionValue = 26;
        public const int CasterBodyPreviousCenterSchemaVersionValue = 27;
        public const int DamageZoneLockSchemaVersionValue = 28;
        public const int EngagementReservationSchemaVersionValue = 29;
        public const int NumericCharacterActionIdSchemaVersionValue = 30;
        public const int BodyPartStateSchemaVersionValue = 31;
        public const int SeveredBodyPartStateSchemaVersionValue = 32;
        public const int AuthorityPoseTerrainStateSchemaVersionValue = 33;
        public const int CombatTargetLockSchemaVersionValue = 34;
        public const int CombatEvadeStateSchemaVersionValue = 35;
        public const int BehaviorControlSchemaVersionValue = 36;
        public const int NumericHashIdentitySchemaVersionValue = 38;
        public const int GroundPointTractionSchemaVersionValue = 39;
        public const int PositionRecallSchemaVersionValue = 40;
        public const int FixedAssemblySchemaVersionValue = 41;
        public const int DoubleEdgeSchemaVersionValue = 42;
        public const int SecondBatchSchemaVersionValue = 43;
        public const int SchemaVersionValue = SecondBatchSchemaVersionValue;

        private static readonly JsonSerializerSettings JsonSettings = new JsonSerializerSettings
        {
            ContractResolver = new CamelCasePropertyNamesContractResolver(),
            Formatting = Formatting.None,
            NullValueHandling = NullValueHandling.Include
        };

        private readonly CharacterTestCharacterRuntimeModule _characterRuntimeModule;
        private readonly CharacterTestRuntimeCommandModule _commandModule;
        private readonly Func<string, int> _controlledEntityProvider;
        private readonly Func<CharacterTestNetworkOwnershipEntry[]> _ownershipProvider;
        private readonly RuntimeHashAccumulator _stateHashAccumulator =
            new RuntimeHashAccumulator(RuntimeHashSchemaIds.NetworkSnapshot, RuntimeHashSchemaIds.Version1);
        private readonly AbilityRuntimeModule _abilityRuntimeModule;
        private readonly RuntimeHashDigest _abilityRuntimeModuleIdDigest;
        private readonly CharacterTestRuntimeAiPlannerCommandModule _runtimeAiPlannerCommandModule;
        private readonly Action _authorityAvailabilityGuard;

        public CharacterTestNetworkSnapshotParticipant(
            CharacterTestCharacterRuntimeModule characterRuntimeModule,
            CharacterTestRuntimeCommandModule commandModule,
            Func<string, int> controlledEntityProvider = null,
            Func<CharacterTestNetworkOwnershipEntry[]> ownershipProvider = null,
            AbilityRuntimeModule abilityRuntimeModule = null,
            CharacterTestRuntimeAiPlannerCommandModule runtimeAiPlannerCommandModule = null)
            : this(characterRuntimeModule, commandModule, controlledEntityProvider, ownershipProvider,
                abilityRuntimeModule, runtimeAiPlannerCommandModule, null)
        {
        }

        internal CharacterTestNetworkSnapshotParticipant(
            CharacterTestCharacterRuntimeModule characterRuntimeModule,
            CharacterTestRuntimeCommandModule commandModule,
            Func<string, int> controlledEntityProvider,
            Func<CharacterTestNetworkOwnershipEntry[]> ownershipProvider,
            AbilityRuntimeModule abilityRuntimeModule,
            CharacterTestRuntimeAiPlannerCommandModule runtimeAiPlannerCommandModule,
            Action authorityAvailabilityGuard)
        {
            _characterRuntimeModule = characterRuntimeModule;
            _commandModule = commandModule;
            _controlledEntityProvider = controlledEntityProvider;
            _ownershipProvider = ownershipProvider;
            _abilityRuntimeModule = abilityRuntimeModule;
            _abilityRuntimeModuleIdDigest = abilityRuntimeModule != null
                ? abilityRuntimeModule.ModuleIdDigest
                : default;
            _runtimeAiPlannerCommandModule = runtimeAiPlannerCommandModule;
            _authorityAvailabilityGuard = authorityAvailabilityGuard;
        }

        public string ParticipantId => ParticipantIdValue;
        public string LastAppliedLocalPeerId { get; private set; } = string.Empty;
        public int LastAppliedLocalControlledEntityId { get; private set; }
        public CharacterTestNetworkOwnershipEntry[] LastAppliedOwnership { get; private set; } =
            new CharacterTestNetworkOwnershipEntry[0];

        public RuntimeCustomState CaptureSnapshot(RuntimeFrame frame)
        {
            CharacterTestNetworkSnapshotDocument document = CaptureDocument(frame, string.Empty);
            string payload = JsonConvert.SerializeObject(document, JsonSettings);
            return new RuntimeCustomState(NetworkSnapshotHashFields.NumericParticipantId, ParticipantId, SchemaVersionValue, payload);
        }

        public RuntimeCustomState CaptureSnapshot(RuntimeFrame frame, string receiverPeerId)
        {
            CharacterTestNetworkSnapshotDocument document = CaptureDocument(frame, receiverPeerId);
            string payload = JsonConvert.SerializeObject(document, JsonSettings);
            return new RuntimeCustomState(NetworkSnapshotHashFields.NumericParticipantId, ParticipantId, SchemaVersionValue, payload);
        }

        public NetworkSnapshotApplyResult ApplySnapshot(RuntimeFrame frame, RuntimeCustomState snapshot)
        {
            _authorityAvailabilityGuard?.Invoke();
            if (snapshot == null)
                return NetworkSnapshotApplyResult.Failed("CharacterTest snapshot is null.");
            if (snapshot.NumericTypeId != NetworkSnapshotHashFields.NumericParticipantId)
                return NetworkSnapshotApplyResult.Failed(
                    "CharacterTest snapshot participant mismatch: " + snapshot.NumericTypeId);
            if (!IsSupportedSchemaVersion(snapshot.SchemaVersion))
            {
                return NetworkSnapshotApplyResult.Failed(
                    "CharacterTest snapshot schema mismatch. expected="
                    + SchemaVersionValue
                    + " actual="
                    + snapshot.SchemaVersion);
            }

            CharacterTestNetworkSnapshotDocument document;
            try
            {
                document = JsonConvert.DeserializeObject<CharacterTestNetworkSnapshotDocument>(
                    snapshot.PayloadJson,
                    JsonSettings);
            }
            catch (Exception exception)
            {
                return NetworkSnapshotApplyResult.Failed(
                    "CharacterTest snapshot JSON parse failed: " + exception.Message);
            }

            if (document == null)
                return NetworkSnapshotApplyResult.Failed("CharacterTest snapshot payload is empty.");
            if (!IsSupportedSchemaVersion(document.SchemaVersion))
            {
                return NetworkSnapshotApplyResult.Failed(
                    "CharacterTest snapshot document schema mismatch. expected="
                    + SchemaVersionValue
                    + " actual="
                    + document.SchemaVersion);
            }
            if (snapshot.SchemaVersion != document.SchemaVersion)
            {
                return NetworkSnapshotApplyResult.Failed(
                    "CharacterTest snapshot envelope/document schema mismatch. envelope="
                    + snapshot.SchemaVersion
                    + " document="
                    + document.SchemaVersion);
            }

            document.NormalizeForSchema(document.SchemaVersion);
            return ApplyDocument(frame, document, preserveLocalPredictedEntity: true);
        }

        internal CharacterTestNetworkSnapshotDocument CaptureRollbackDocument(RuntimeFrame frame)
        {
            CharacterTestNetworkSnapshotDocument document = CaptureDocument(frame, string.Empty);
            return document.CloneForRollback();
        }

        internal NetworkSnapshotApplyResult ApplyRollbackDocument(
            RuntimeFrame frame,
            CharacterTestNetworkSnapshotDocument document,
            bool preserveLocalPredictedEntity,
            Func<CharacterTestNetworkFixedAssemblySnapshot[], long, bool> frozenPausedPreflight = null,
            Func<long[][], long, bool> frozenDoubleEdgePreflight = null)
        {
            _authorityAvailabilityGuard?.Invoke();
            if (document == null)
                return NetworkSnapshotApplyResult.Failed("CharacterTest rollback snapshot payload is empty.");
            if (!IsSupportedSchemaVersion(document.SchemaVersion))
            {
                return NetworkSnapshotApplyResult.Failed(
                    "CharacterTest rollback snapshot document schema mismatch. expected="
                    + SchemaVersionValue
                    + " actual="
                    + document.SchemaVersion);
            }

            document.NormalizeForSchema(document.SchemaVersion);
            return ApplyDocument(frame, document, preserveLocalPredictedEntity, frozenPausedPreflight, frozenDoubleEdgePreflight);
        }

        internal NetworkSnapshotApplyResult ApplyRollbackSnapshot(
            RuntimeFrame frame,
            RuntimeCustomState snapshot,
            bool preserveLocalPredictedEntity)
        {
            _authorityAvailabilityGuard?.Invoke();
            if (snapshot == null)
                return NetworkSnapshotApplyResult.Failed("CharacterTest rollback snapshot is null.");
            if (snapshot.NumericTypeId != NetworkSnapshotHashFields.NumericParticipantId)
                return NetworkSnapshotApplyResult.Failed(
                    "CharacterTest rollback snapshot participant mismatch: " + snapshot.NumericTypeId);
            if (!IsSupportedSchemaVersion(snapshot.SchemaVersion))
            {
                return NetworkSnapshotApplyResult.Failed(
                    "CharacterTest rollback snapshot schema mismatch. expected="
                    + SchemaVersionValue
                    + " actual="
                    + snapshot.SchemaVersion);
            }

            CharacterTestNetworkSnapshotDocument document;
            try
            {
                document = JsonConvert.DeserializeObject<CharacterTestNetworkSnapshotDocument>(
                    snapshot.PayloadJson,
                    JsonSettings);
            }
            catch (Exception exception)
            {
                return NetworkSnapshotApplyResult.Failed(
                    "CharacterTest rollback snapshot JSON parse failed: " + exception.Message);
            }

            if (document == null)
                return NetworkSnapshotApplyResult.Failed("CharacterTest rollback snapshot payload is empty.");
            if (snapshot.SchemaVersion != document.SchemaVersion)
            {
                return NetworkSnapshotApplyResult.Failed(
                    "CharacterTest rollback snapshot envelope/document schema mismatch. envelope="
                    + snapshot.SchemaVersion
                    + " document="
                    + document.SchemaVersion);
            }

            return ApplyRollbackDocument(frame, document, preserveLocalPredictedEntity);
        }

        private NetworkSnapshotApplyResult ApplyDocument(
            RuntimeFrame frame,
            CharacterTestNetworkSnapshotDocument document,
            bool preserveLocalPredictedEntity,
            Func<CharacterTestNetworkFixedAssemblySnapshot[], long, bool> frozenPausedPreflight = null,
            Func<long[][], long, bool> frozenDoubleEdgePreflight = null)
        {
            _authorityAvailabilityGuard?.Invoke();
            if (_characterRuntimeModule == null)
                return NetworkSnapshotApplyResult.Failed("CharacterTest runtime module is missing.");
            var fixedAssemblyPreflight = ValidateFixedAssemblyImport(document, frozenPausedPreflight, frozenDoubleEdgePreflight);
            if (!fixedAssemblyPreflight.Success) return fixedAssemblyPreflight;

            // Validate nested identity protocols before any entity, runtime or resource is changed.
            if (document.ArcMines != null
                && (document.ArcMines.SchemaVersion != CharacterTestArcMineRuntimeState.CurrentSchemaVersion
                    || document.ArcMines.ModuleId != CharacterTestArcMineRuntimeState.DefaultModuleId
                    || document.ArcMines.NextRuntimeId <= 0))
                return NetworkSnapshotApplyResult.Failed("Unsupported Arc Mine snapshot version or numeric module identity.");
            if (document.RuneTurrets != null
                && (document.RuneTurrets.SchemaVersion != CharacterTestRuneTurretRuntimeState.CurrentSchemaVersion
                    || document.RuneTurrets.ModuleId != CharacterTestRuneTurretRuntimeState.DefaultModuleId
                    || document.RuneTurrets.NextRuntimeId <= 0))
                return NetworkSnapshotApplyResult.Failed("Unsupported Rune Turret snapshot version or numeric module identity.");

            for (int i = 0; document.Characters != null && i < document.Characters.Length; i++)
            {
                CharacterTestNetworkCharacterSnapshot character = document.Characters[i];
                if (character == null
                    || !Enum.IsDefined(typeof(CharacterTestActionStateId), character.ActionState)
                    || !Enum.IsDefined(typeof(CharacterTestActionPhaseId), character.ActionPhase)
                    || !Enum.IsDefined(typeof(CharacterActionInstanceState), character.ActionRunnerState)
                    || !Enum.IsDefined(typeof(CharacterActionPhaseKind), character.ActionRunnerPhase)
                    || !CharacterActionDiagnosticCodeText.IsDefined(character.ActionRunnerLastRejectReason)
                    || !character.HasValidAnimationIdentityShape())
                    return NetworkSnapshotApplyResult.Failed("CharacterTest snapshot contains an undefined numeric action identity.");
                try { _ = character.LastRejection; }
                catch (ArgumentException) { return NetworkSnapshotApplyResult.Failed("CharacterTest snapshot contains an invalid numeric action rejection."); }
                bool hasAnimationIdentity = character.LastAnimationBindingId != 0
                    || character.AnimationBindingId != 0 || character.AnimationRequest.BindingId != 0;
                if (hasAnimationIdentity && (_characterRuntimeModule.ActionRuntime == null
                    || !_characterRuntimeModule.ActionRuntime.ValidateAnimationSnapshot(character)))
                    return NetworkSnapshotApplyResult.Failed("CharacterTest snapshot contains an unresolved numeric animation binding.");
            }

            if (document.SchemaVersion >= CombatTargetLockSchemaVersionValue)
            {
                bool hasValidCombatTarget = document.LockedCombatTarget != null
                    && document.LockedCombatTarget.TryToHandle(out _);
                if (document.TargetLockEnabled != hasValidCombatTarget
                    || (hasValidCombatTarget
                        && document.LockedCombatTarget.TargetEntityId
                            != document.LockedTargetEntityId))
                {
                    return NetworkSnapshotApplyResult.Failed(
                        "CharacterTest locked combat target document shape is invalid.");
                }
            }

            bool hasBodyPartState = document.SchemaVersion >= BodyPartStateSchemaVersionValue;
            bool hasAuthorityPoseTerrainState = document.SchemaVersion >=
                AuthorityPoseTerrainStateSchemaVersionValue;
            CharacterTestDamageZoneLockStateSnapshot[] damageZoneLocks =
                document.RuntimeAiPlannerState?.DamageZoneLocks
                ?? Array.Empty<CharacterTestDamageZoneLockStateSnapshot>();
            if (hasBodyPartState
                && !CharacterTestBodyPartStateNetworkSnapshotCodec.TryValidateDocumentShape(
                    document.SchemaVersion,
                    document.Frame,
                    document.Characters,
                    document.BodyPartStates,
                    out string bodyPartShapeFailure))
            {
                return NetworkSnapshotApplyResult.Failed(bodyPartShapeFailure);
            }
            if (hasAuthorityPoseTerrainState
                && !CharacterTestNetworkAuthorityPoseSnapshotCodec.TryValidateDocumentShape(
                    document.Frame,
                    document.Characters,
                    document.AuthorityPoseTerrainStates,
                    out string authorityPoseShapeFailure))
            {
                return NetworkSnapshotApplyResult.Failed(authorityPoseShapeFailure);
            }
            if (!CharacterTestBodyPartStateNetworkSnapshotCodec.TryValidateDamageZoneLocks(
                document.Frame,
                document.Characters,
                damageZoneLocks,
                requireLegacyZeroRevision: !hasBodyPartState,
                out string damageZoneLockFailure))
            {
                return NetworkSnapshotApplyResult.Failed(damageZoneLockFailure);
            }
            if (_runtimeAiPlannerCommandModule == null && damageZoneLocks.Length > 0)
            {
                return NetworkSnapshotApplyResult.Failed(
                    "CharacterTest DamageZone lock snapshot requires Runtime AI Planner authority.");
            }
            if (!CharacterTestBodyPartStateNetworkSnapshotCodec.TryPreflightDamageZoneLockRuntimeTargets(
                _characterRuntimeModule.Entities,
                document.Characters,
                damageZoneLocks,
                _characterRuntimeModule.World.GameplayComponentWorld.Registry.Lifecycle.CountAllocated,
                out damageZoneLockFailure))
            {
                return NetworkSnapshotApplyResult.Failed(damageZoneLockFailure);
            }
            if (hasBodyPartState)
            {
                NetworkSnapshotApplyResult bodyPartPreflight =
                    _characterRuntimeModule.PreflightNetworkBodyPartStateSnapshots(
                        document.Characters,
                        document.BodyPartStates);
                if (!bodyPartPreflight.Success)
                    return bodyPartPreflight;
            }

            NetworkSnapshotApplyResult result = _characterRuntimeModule.ApplyNetworkCharacters(
                document.Characters,
                frame,
                document.LocalControlledEntityId,
                preserveLocalPredictedEntity);
            if (!result.Success)
                return result;
            if (!CharacterTestBodyPartStateNetworkSnapshotCodec.TryPrepareCurrentAuthority(
                _characterRuntimeModule.Entities,
                document.BodyPartStates,
                legacyDefaults: !hasBodyPartState,
                allowSevered: document.SchemaVersion >= SeveredBodyPartStateSchemaVersionValue,
                out CharacterTestBodyPartStateNetworkRestorePlan[] bodyPartRestorePlans,
                out string bodyPartRestoreFailure))
            {
                return NetworkSnapshotApplyResult.Failed(bodyPartRestoreFailure);
            }
            if (!CharacterTestNetworkAuthorityPoseSnapshotCodec.TryPrepareCurrentAuthority(
                _characterRuntimeModule.Entities,
                document.AuthorityPoseTerrainStates,
                legacyDefaults: !hasAuthorityPoseTerrainState,
                out CharacterTestAuthorityPoseNetworkRestorePlan[] authorityPoseRestorePlans,
                out string authorityPoseRestoreFailure))
            {
                return NetworkSnapshotApplyResult.Failed(authorityPoseRestoreFailure);
            }
            PrunePendingCommandsForNonPlayerOwnedEntities();

            NetworkSnapshotApplyResult movableResult =
                _characterRuntimeModule.ApplyNetworkMovableNaturalObjects(
                    document.MovableNaturalObjects);
            if (!movableResult.Success)
                return movableResult;

            NetworkSnapshotApplyResult projectileResult = CharacterTestNetworkProjectileSnapshotCodec.Apply(
                _characterRuntimeModule?.CombatTraceSystem,
                document.Projectiles);
            if (!projectileResult.Success)
                return projectileResult;

            NetworkSnapshotApplyResult hookPullResult = CharacterTestNetworkHookPullSnapshotCodec.Apply(
                _characterRuntimeModule?.CombatTraceSystem,
                document.HookPulls);
            if (!hookPullResult.Success)
                return hookPullResult;

            NetworkSnapshotApplyResult arcMineResult = CharacterTestNetworkArcMineSnapshotCodec.Apply(
                _characterRuntimeModule?.CombatTraceSystem,
                document.ArcMines);
            if (!arcMineResult.Success)
                return arcMineResult;

            NetworkSnapshotApplyResult runeTurretResult = CharacterTestNetworkRuneTurretSnapshotCodec.Apply(
                _characterRuntimeModule?.CombatTraceSystem,
                document.RuneTurrets);
            if (!runeTurretResult.Success)
                return runeTurretResult;

            NetworkSnapshotApplyResult areaConfinementResult =
                CharacterTestNetworkAreaConfinementSnapshotCodec.Apply(
                    _characterRuntimeModule?.CombatTraceSystem,
                    document.AreaConfinement);
            if (!areaConfinementResult.Success)
                return areaConfinementResult;

            NetworkSnapshotApplyResult poisonBottlePoolResult =
                CharacterTestNetworkPoisonBottlePoolSnapshotCodec.Apply(
                    _characterRuntimeModule?.CombatTraceSystem,
                    document.PoisonBottlePools);
            if (!poisonBottlePoolResult.Success)
                return poisonBottlePoolResult;

            NetworkSnapshotApplyResult physicalShieldResult =
                CharacterTestNetworkPhysicalShieldSnapshotCodec.Apply(
                    _characterRuntimeModule?.CombatTraceSystem,
                    document.PhysicalShields);
            if (!physicalShieldResult.Success)
                return physicalShieldResult;

            NetworkSnapshotApplyResult abilityResult = CharacterTestNetworkAbilityRuntimeSnapshot.Apply(
                _abilityRuntimeModule,
                document.AbilityRuntime);
            if (!abilityResult.Success)
                return abilityResult;

            CharacterTestCharacterCombatTraceSystem combatTraceSystem =
                _characterRuntimeModule?.CombatTraceSystem;
            if (combatTraceSystem != null)
            {
                if (!combatTraceSystem.TryApplyNetworkHitResolutionPolicySnapshot(
                    document.HitResolutionPolicy,
                    out string hitResolutionPolicyFailure))
                {
                    return NetworkSnapshotApplyResult.Failed(hitResolutionPolicyFailure);
                }
            }
            else if (document.HitResolutionPolicy != null
                && !document.HitResolutionPolicy.IsEmpty)
            {
                return NetworkSnapshotApplyResult.Failed(
                    "CharacterTest hit policy snapshot requires the combat trace system.");
            }

            if (_runtimeAiPlannerCommandModule != null
                && !_runtimeAiPlannerCommandModule.TryApplyStateSnapshot(
                    document.RuntimeAiPlannerState,
                    out string runtimeAiPlannerFailure))
            {
                return NetworkSnapshotApplyResult.Failed(runtimeAiPlannerFailure);
            }

            CharacterTestBodyPartStateNetworkSnapshotCodec.Commit(bodyPartRestorePlans);
            CharacterTestNetworkAuthorityPoseSnapshotCodec.Commit(authorityPoseRestorePlans);
            RuntimeFrame restoredCompletedFrame =
                _characterRuntimeModule.TryGetLastCompletedRuntimeFrame(
                    out RuntimeFrame lastCompletedFrame)
                    ? lastCompletedFrame
                    : frame;
            _characterRuntimeModule.SyncBodyPartDerivedTopologyAfterAuthorityRestore(
                restoredCompletedFrame);
            combatTraceSystem?.ResealWorldTargetClassificationsAfterAuthorityRestore(
                restoredCompletedFrame);
            if (document.SchemaVersion >= SeveredBodyPartStateSchemaVersionValue)
            {
                _runtimeAiPlannerCommandModule
                    ?.SweepInvalidTopologyStateAfterAuthorityRestore(frame);
            }
            combatTraceSystem?.ResetBodyPartAuthorityForRestore();

            if (!TryResolveRestoredCombatTarget(
                    document,
                    restoredCompletedFrame,
                    out CharacterTestCombatTargetHandle restoredCombatTarget,
                    out string combatTargetFailure))
            {
                return NetworkSnapshotApplyResult.Failed(combatTargetFailure);
            }

            _commandModule?.ApplyNetworkSnapshotState(
                _commandModule.CameraMode,
                document.TargetLockEnabled,
                document.LockedTargetEntityId,
                document.RuntimeAiBehaviorSelectionUnlocked);
            _commandModule?.ApplyLockedCombatTargetSnapshot(restoredCombatTarget);
            LastAppliedLocalPeerId = document.LocalPeerId ?? string.Empty;
            LastAppliedLocalControlledEntityId = document.LocalControlledEntityId > 0
                ? document.LocalControlledEntityId
                : 0;
            LastAppliedOwnership = document.Ownerships ?? new CharacterTestNetworkOwnershipEntry[0];
            return NetworkSnapshotApplyResult.Succeeded();
        }

        private int PrunePendingCommandsForNonPlayerOwnedEntities()
        {
            if (_commandModule == null || _characterRuntimeModule == null)
                return 0;

            return _commandModule.CommandBuffer.RemoveWhere(command =>
            {
                if (!CharacterTestNetworkEntityControlGrantState.RequiresEntityControl(command))
                    return false;

                CharacterTestCharacterEntity entity =
                    _characterRuntimeModule.FindEntityById(command.TargetId);
                return entity == null || !CanApplyPlayerCommand(entity.Control.Owner);
            });
        }

        private readonly List<AbilityRuntimeCooldownEntry> _hashCooldowns = new List<AbilityRuntimeCooldownEntry>();
        private void AddLiveAbilityHash(RuntimeHashAccumulator accumulator, int caster = 0, bool exclude = false)
        {
            _hashCooldowns.Clear();
            _abilityRuntimeModule?.CopyCooldownEntries(_hashCooldowns);
            accumulator.AddInt(NetworkAbilityRuntimeSnapshotHashFields.SchemaVersion, AbilityRuntimeState.CurrentSchemaVersion);
            accumulator.AddDigest(
                NetworkAbilityRuntimeSnapshotHashFields.ModuleId,
                _abilityRuntimeModuleIdDigest);
            accumulator.AddLong(NetworkAbilityRuntimeSnapshotHashFields.DefinitionHash, _abilityRuntimeModule?.Definitions.ComputeStableHash() ?? 0L);
            int count = 0;
            foreach (var entry in _hashCooldowns)
            {
                if (caster <= 0 || (entry.CasterEntityId == caster) != exclude)
                    count++;
            }
            accumulator.BeginArray(NetworkAbilityRuntimeSnapshotHashFields.Cooldowns, count);
            int index = 0;
            foreach (var entry in _hashCooldowns)
            {
                if (caster > 0 && (entry.CasterEntityId == caster) == exclude)
                    continue;

                accumulator.BeginElement(index);
                accumulator.AddInt(NetworkAbilityCooldownEntryHashFields.CasterEntityId, entry.CasterEntityId);
                accumulator.AddInt(NetworkAbilityCooldownEntryHashFields.AbilityId, entry.AbilityId);
                accumulator.AddLong(NetworkAbilityCooldownEntryHashFields.EndFrame, entry.EndFrame);
                accumulator.EndElement();
                index++;
            }
            accumulator.EndArray();
        }

        public long ComputeStateHash(RuntimeFrame frame) => ComputeStateHashCore(frame, null);

        // Immediate Lab capture only: the caller has just captured this owner's state in the same synchronous post-tick operation.
        // This is not a frame-number cache; a subsequent capture/restore must acquire a new state snapshot.
        internal long ComputeCapturedStateHash(RuntimeFrame frame, CharacterTestRuntimeAiPlannerCommandModule owner,
            CharacterTestRuntimeAiPlannerStateSnapshot plannerState)
        {
            if (!ReferenceEquals(owner, _runtimeAiPlannerCommandModule) || plannerState == null)
                throw new ArgumentException("Captured planner state must belong to this snapshot participant.");
            return ComputeStateHashCore(frame, plannerState);
        }

        private long ComputeStateHashCore(RuntimeFrame frame, CharacterTestRuntimeAiPlannerStateSnapshot plannerState)
        {
            RuntimeHashAccumulator accumulator = CreateStateHashAccumulator(frame);
            var doubleEdge = _characterRuntimeModule?.World.DoubleEdgeState;
            if (doubleEdge != null && doubleEdge.HasState) {
                accumulator.BeginObject(NetworkRootStateFieldIds.DoubleEdgeStage, RuntimeHashSchemaIds.NetworkSnapshotDoubleEdgeStage, 2);
                doubleEdge.AddHash(ref accumulator); accumulator.EndObject();
            }
            AddCommandStateHash(accumulator);
            AddCharacterEntitiesHash(accumulator, _characterRuntimeModule != null
                ? _characterRuntimeModule.Entities
                : null,
                actionRuntime: _characterRuntimeModule?.ActionRuntime);
            CharacterTestBodyPartStateNetworkSnapshotCodec.AddStateHash(
                accumulator,
                _characterRuntimeModule?.Entities);
            CharacterTestNetworkAuthorityPoseSnapshotCodec.AddStateHash(
                accumulator,
                _characterRuntimeModule?.Entities);
            AddMovableNaturalObjectsHash(
                accumulator,
                _characterRuntimeModule?.MovableNaturalObjects);
            CharacterTestNetworkProjectileSnapshotCodec.AddStateHash(
                accumulator,
                _characterRuntimeModule?.CombatTraceSystem?.ReadProjectileStateView());
            CharacterTestNetworkHookPullSnapshotCodec.AddStateHash(
                accumulator,
                _characterRuntimeModule?.CombatTraceSystem?.ReadHookPullStateView());
            CharacterTestNetworkArcMineSnapshotCodec.AddStateHash(
                accumulator,
                _characterRuntimeModule?.CombatTraceSystem?.ReadArcMineStateView());
            CharacterTestNetworkRuneTurretSnapshotCodec.AddStateHash(
                accumulator,
                _characterRuntimeModule?.CombatTraceSystem?.ReadRuneTurretStateView());
            CharacterTestNetworkAreaConfinementSnapshotCodec.AddStateHash(
                accumulator,
                _characterRuntimeModule?.CombatTraceSystem?.ReadAreaConfinementStateView());
            CharacterTestNetworkPoisonBottlePoolSnapshotCodec.AddStateHash(
                accumulator,
                _characterRuntimeModule?.CombatTraceSystem?.ReadPoisonBottlePoolStateView());
            CharacterTestNetworkPhysicalShieldSnapshotCodec.AddStateHash(
                accumulator,
                _characterRuntimeModule?.CombatTraceSystem?.ReadPhysicalShieldStateView());
            AddLiveAbilityHash(accumulator);
            CharacterTestRuntimeAiPlannerStateSnapshot.AddStateHash(
                accumulator,
                plannerState ?? _runtimeAiPlannerCommandModule?.CaptureStateForHash());
            _characterRuntimeModule?.CombatTraceSystem?.AddNetworkHitResolutionPolicyHash(accumulator);
            accumulator.AddLong(NetworkRootStateFieldIds.ProjectileSpawnProfileHash,
                _characterRuntimeModule?.CombatTraceSystem?.ProjectileSpawnProfileHash ?? 0L);
            return accumulator.ToHash();
        }

        private static bool IsSupportedSchemaVersion(int schemaVersion)
        {
            return schemaVersion == SchemaVersionValue || schemaVersion == FixedAssemblySchemaVersionValue || schemaVersion == PositionRecallSchemaVersionValue;
        }

        private bool TryResolveRestoredCombatTarget(
            CharacterTestNetworkSnapshotDocument document,
            RuntimeFrame restoredCompletedFrame,
            out CharacterTestCombatTargetHandle handle,
            out string failure)
        {
            handle = default;
            failure = string.Empty;
            if (!document.TargetLockEnabled)
                return true;

            if (document.SchemaVersion >= CombatTargetLockSchemaVersionValue)
            {
                if (document.LockedCombatTarget == null
                    || !document.LockedCombatTarget.TryToHandle(out handle)
                    || handle.TargetEntityId != document.LockedTargetEntityId)
                {
                    failure = "CharacterTest locked combat target snapshot is invalid.";
                    return false;
                }
                return true;
            }

            CharacterTestCharacterEntity source = FindRestoreSourceEntity(document);
            CharacterTestCharacterEntity target = _characterRuntimeModule?.FindEntityById(
                document.LockedTargetEntityId);
            CharacterTestPerceptionFrameCache perception =
                _characterRuntimeModule?.PerceptionFrameCache;
            if (source == null || target == null || perception == null)
            {
                failure = "Legacy actor-only target lock cannot resolve source or target.";
                return false;
            }

            perception.BeginCommittedConsumerPhase(
                restoredCompletedFrame,
                _characterRuntimeModule,
                _characterRuntimeModule.PhysicsRuntime);
            if (!perception.TryAcquireNearestHurtbox(
                    restoredCompletedFrame,
                    source,
                    target,
                    out handle,
                    out _))
            {
                failure = "Legacy actor-only target lock has no available combat Hurtbox.";
                return false;
            }
            return true;
        }

        private CharacterTestCharacterEntity FindRestoreSourceEntity(
            CharacterTestNetworkSnapshotDocument document)
        {
            if (_characterRuntimeModule == null)
                return null;
            if (document.LocalControlledEntityId > 0)
            {
                CharacterTestCharacterEntity controlled =
                    _characterRuntimeModule.FindEntityById(document.LocalControlledEntityId);
                if (controlled != null)
                    return controlled;
            }

            IReadOnlyList<CharacterTestCharacterEntity> entities =
                _characterRuntimeModule.Entities;
            for (int i = 0; i < entities.Count; i++)
            {
                CharacterTestCharacterEntity entity = entities[i];
                if (entity != null
                    && entity.Control.Owner == CharacterTestCharacterControlOwner.LocalPlayer)
                {
                    return entity;
                }
            }
            return null;
        }

        public long ComputeOwnedStateHash(RuntimeFrame frame, int ownedEntityId)
        {
            RuntimeHashAccumulator accumulator = CreateStateHashAccumulator(frame);
            var doubleEdge = _characterRuntimeModule?.World.DoubleEdgeState;
            if (doubleEdge != null && doubleEdge.HasState) {
                accumulator.BeginObject(NetworkRootStateFieldIds.DoubleEdgeStage, RuntimeHashSchemaIds.NetworkSnapshotDoubleEdgeStage, 2);
                doubleEdge.AddHash(ref accumulator); accumulator.EndObject();
            }
            AddCommandStateHash(accumulator);
            AddCharacterEntitiesHash(
                accumulator,
                _characterRuntimeModule != null ? _characterRuntimeModule.Entities : null,
                ownedEntityId,
                _characterRuntimeModule?.ActionRuntime);
            CharacterTestBodyPartStateNetworkSnapshotCodec.AddStateHash(
                accumulator,
                _characterRuntimeModule?.Entities,
                ownedEntityId);
            CharacterTestNetworkAuthorityPoseSnapshotCodec.AddStateHash(
                accumulator,
                _characterRuntimeModule?.Entities,
                ownedEntityId);
            AddMovableNaturalObjectsHash(
                accumulator,
                _characterRuntimeModule?.MovableNaturalObjects);
            CharacterTestNetworkProjectileSnapshotCodec.AddStateHash(
                accumulator,
                _characterRuntimeModule?.CombatTraceSystem?.ReadProjectileStateView(),
                ownedEntityId);
            CharacterTestNetworkHookPullSnapshotCodec.AddStateHash(
                accumulator,
                _characterRuntimeModule?.CombatTraceSystem?.ReadHookPullStateView(),
                ownedEntityId);
            CharacterTestNetworkArcMineSnapshotCodec.AddStateHash(
                accumulator,
                _characterRuntimeModule?.CombatTraceSystem?.ReadArcMineStateView(),
                ownedEntityId);
            CharacterTestNetworkRuneTurretSnapshotCodec.AddStateHash(
                accumulator,
                _characterRuntimeModule?.CombatTraceSystem?.ReadRuneTurretStateView(),
                ownedEntityId);
            CharacterTestNetworkAreaConfinementSnapshotCodec.AddStateHash(
                accumulator,
                _characterRuntimeModule?.CombatTraceSystem?.ReadAreaConfinementStateView(),
                ownedEntityId);
            CharacterTestNetworkPoisonBottlePoolSnapshotCodec.AddStateHash(
                accumulator,
                _characterRuntimeModule?.CombatTraceSystem?.ReadPoisonBottlePoolStateView(),
                ownedEntityId);
            CharacterTestNetworkPhysicalShieldSnapshotCodec.AddStateHash(
                accumulator,
                _characterRuntimeModule?.CombatTraceSystem?.ReadPhysicalShieldStateView(),
                ownedEntityId);
            AddLiveAbilityHash(accumulator, ownedEntityId);
            CharacterTestRuntimeAiPlannerStateSnapshot.AddStateHashForOwnership(
                accumulator,
                _runtimeAiPlannerCommandModule?.CaptureStateForHash(),
                ownedEntityId,
                includeOwnedLocks: true);
            _characterRuntimeModule?.CombatTraceSystem?.AddNetworkHitResolutionPolicyHash(
                accumulator,
                ownedEntityId);
            accumulator.AddLong(NetworkRootStateFieldIds.ProjectileSpawnProfileHash,
                _characterRuntimeModule?.CombatTraceSystem?.ProjectileSpawnProfileHash ?? 0L);
            return accumulator.ToHash();
        }

        public long ComputeNonOwnedStateHash(RuntimeFrame frame, int ownedEntityId)
        {
            RuntimeHashAccumulator accumulator = CreateStateHashAccumulator(frame);
            var doubleEdge = _characterRuntimeModule?.World.DoubleEdgeState;
            if (doubleEdge != null && doubleEdge.HasState) {
                accumulator.BeginObject(NetworkRootStateFieldIds.DoubleEdgeStage, RuntimeHashSchemaIds.NetworkSnapshotDoubleEdgeStage, 2);
                doubleEdge.AddHash(ref accumulator); accumulator.EndObject();
            }
            AddCommandStateHash(accumulator);
            AddCharacterEntitiesHashExcluding(
                accumulator,
                _characterRuntimeModule != null ? _characterRuntimeModule.Entities : null,
                ownedEntityId,
                _characterRuntimeModule?.ActionRuntime);
            CharacterTestBodyPartStateNetworkSnapshotCodec.AddStateHash(
                accumulator,
                _characterRuntimeModule?.Entities,
                ownedEntityId,
                excludeOwnedEntity: true);
            CharacterTestNetworkAuthorityPoseSnapshotCodec.AddStateHash(
                accumulator,
                _characterRuntimeModule?.Entities,
                ownedEntityId,
                excludeOwnedEntity: true);
            AddMovableNaturalObjectsHash(
                accumulator,
                _characterRuntimeModule?.MovableNaturalObjects);
            CharacterTestNetworkProjectileSnapshotCodec.AddStateHashExcludingSource(
                accumulator,
                _characterRuntimeModule?.CombatTraceSystem?.ReadProjectileStateView(),
                ownedEntityId);
            CharacterTestNetworkHookPullSnapshotCodec.AddStateHashExcludingCaster(
                accumulator,
                _characterRuntimeModule?.CombatTraceSystem?.ReadHookPullStateView(),
                ownedEntityId);
            CharacterTestNetworkArcMineSnapshotCodec.AddStateHashExcludingCaster(
                accumulator,
                _characterRuntimeModule?.CombatTraceSystem?.ReadArcMineStateView(),
                ownedEntityId);
            CharacterTestNetworkRuneTurretSnapshotCodec.AddStateHashExcludingCaster(
                accumulator,
                _characterRuntimeModule?.CombatTraceSystem?.ReadRuneTurretStateView(),
                ownedEntityId);
            CharacterTestNetworkAreaConfinementSnapshotCodec.AddStateHashExcludingCaster(
                accumulator,
                _characterRuntimeModule?.CombatTraceSystem?.ReadAreaConfinementStateView(),
                ownedEntityId);
            CharacterTestNetworkPoisonBottlePoolSnapshotCodec.AddStateHashExcludingCaster(
                accumulator,
                _characterRuntimeModule?.CombatTraceSystem?.ReadPoisonBottlePoolStateView(),
                ownedEntityId);
            CharacterTestNetworkPhysicalShieldSnapshotCodec.AddStateHashExcludingCaster(
                accumulator,
                _characterRuntimeModule?.CombatTraceSystem?.ReadPhysicalShieldStateView(),
                ownedEntityId);
            AddLiveAbilityHash(accumulator, ownedEntityId, true);
            CharacterTestRuntimeAiPlannerStateSnapshot.AddStateHashForOwnership(
                accumulator,
                _runtimeAiPlannerCommandModule?.CaptureStateForHash(),
                ownedEntityId,
                includeOwnedLocks: false);
            _characterRuntimeModule?.CombatTraceSystem?.AddNetworkHitResolutionPolicyHash(
                accumulator,
                attackerEntityIdFilter: ownedEntityId,
                excludeAttacker: true);
            return accumulator.ToHash();
        }

        private RuntimeHashAccumulator CreateStateHashAccumulator(RuntimeFrame frame)
        {
            RuntimeHashAccumulator accumulator = _stateHashAccumulator;
            accumulator.Reset();
            accumulator.AddInt(NetworkSnapshotHashFields.ParticipantId, NetworkSnapshotHashFields.NumericParticipantId);
            accumulator.AddInt(NetworkSnapshotHashFields.SchemaVersion, SchemaVersionValue);
            accumulator.AddLong(NetworkSnapshotHashFields.Frame, frame.Value);
            return accumulator;
        }

        private void AddCommandStateHash(RuntimeHashAccumulator accumulator)
        {
            accumulator.AddInt(
                NetworkCommandStateHashFields.TargetLockEnabled,
                _commandModule != null && _commandModule.TargetLockEnabled ? 1 : 0);
            accumulator.AddInt(
                NetworkCommandStateHashFields.LockedTargetEntityId,
                _commandModule != null ? _commandModule.LockedTargetEntityId : 0);
            CharacterTestNetworkCombatTargetSnapshot lockedCombatTarget =
                _commandModule != null
                    ? CharacterTestNetworkCombatTargetSnapshot.FromHandle(
                        _commandModule.LockedCombatTarget)
                    : null;
            accumulator.AddInt(
                NetworkCommandStateHashFields.LockedCombatTargetPresent,
                lockedCombatTarget != null ? 1 : 0);
            if (lockedCombatTarget != null)
            {
                accumulator.BeginObject(
                    NetworkCommandStateHashFields.LockedCombatTarget,
                    RuntimeHashSchemaIds.NetworkSnapshotCombatTarget,
                    1);
                lockedCombatTarget.AddHash(accumulator);
                accumulator.EndObject();
            }
            accumulator.AddInt(
                NetworkCommandStateHashFields.RuntimeAiBehaviorSelectionUnlocked,
                _commandModule != null && _commandModule.RuntimeAiBehaviorSelectionUnlocked ? 1 : 0);
        }

        private void AddCharacterEntitiesHash(
            RuntimeHashAccumulator accumulator,
            IReadOnlyList<CharacterTestCharacterEntity> entities,
            int entityIdFilter = 0,
            CharacterTestCharacterActionRuntime actionRuntime = null)
        {
            int count = entities != null ? entities.Count : 0;
            if (entityIdFilter > 0)
            {
                for (int i = 0; i < count; i++)
                {
                    if (entities[i].Id.Value != entityIdFilter)
                        continue;

                    accumulator.AddInt(NetworkRootStateFieldIds.CharacterCount, 1);
                    AddCharacterEntityHash(
                        accumulator,
                        0,
                        entities[i],
                        CharacterTestCharacterControlOwner.LocalPlayer,
                        actionRuntime);
                    return;
                }

                accumulator.AddInt(NetworkRootStateFieldIds.CharacterCount, 0);
                return;
            }

            accumulator.AddInt(NetworkRootStateFieldIds.CharacterCount, count);
            for (int i = 0; i < count; i++)
                AddCharacterEntityHash(accumulator, i, entities[i], actionRuntime: actionRuntime);
        }

        private void AddCharacterEntitiesHashExcluding(
            RuntimeHashAccumulator accumulator,
            IReadOnlyList<CharacterTestCharacterEntity> entities,
            int excludedEntityId,
            CharacterTestCharacterActionRuntime actionRuntime = null)
        {
            int count = entities != null ? entities.Count : 0;
            if (excludedEntityId <= 0)
            {
                AddCharacterEntitiesHash(accumulator, entities, actionRuntime: actionRuntime);
                return;
            }

            int matched = 0;
            for (int i = 0; i < count; i++)
            {
                if (entities[i].Id.Value != excludedEntityId)
                    matched++;
            }

            accumulator.AddInt(NetworkRootStateFieldIds.CharacterCount, matched);
            int index = 0;
            for (int i = 0; i < count; i++)
            {
                if (entities[i].Id.Value == excludedEntityId)
                    continue;

                AddCharacterEntityHash(
                    accumulator,
                    index,
                    entities[i],
                    NormalizeNonOwnedControlOwner(entities[i].Control.Owner),
                    actionRuntime);
                index++;
            }
        }

        private static void AddMovableNaturalObjectsHash(
            RuntimeHashAccumulator accumulator,
            IReadOnlyList<CharacterTestMovableNaturalObjectState> objects)
        {
            int count = objects != null ? objects.Count : 0;
            accumulator.AddInt(NetworkRootStateFieldIds.MovableNaturalObjectCount, count);
            for (int i = 0; i < count; i++)
                AddMovableNaturalObjectHash(accumulator, i, objects[i]);
        }

        private static void AddMovableNaturalObjectHash(
            RuntimeHashAccumulator accumulator,
            int index,
            CharacterTestMovableNaturalObjectState state)
        {
            if (state == null)
                return;

            accumulator.AddInt(NetworkRootStateFieldIds.MovableNaturalObjectIndex, index);
            accumulator.AddDigest(NetworkRootStateFieldIds.MovableNaturalObjectStableIdDigest, state.StableLabelDigest);
            accumulator.AddInt(NetworkRootStateFieldIds.MovableNaturalObjectNumericId, state.NumericId);
            accumulator.AddDigest(NetworkRootStateFieldIds.MovableNaturalObjectCategoryDigest, state.CategoryDigest);
            accumulator.AddInt(NetworkRootStateFieldIds.MovableNaturalObjectMassGrams, state.MassGrams);
            accumulator.AddInt(NetworkRootStateFieldIds.MovableNaturalObjectMaterialKind, (int)state.Material.Kind);
            accumulator.AddInt(NetworkRootStateFieldIds.MovableNaturalObjectMaterialFrictionMilli,
                CharacterTestDeterministicUnits.ScaleToMilli(ToFloat(state.Material.Friction)));
            accumulator.AddInt(NetworkRootStateFieldIds.MovableNaturalObjectMaterialElasticityMilli,
                CharacterTestDeterministicUnits.ScaleToMilli(ToFloat(state.Material.Elasticity)));
            accumulator.AddInt(NetworkRootStateFieldIds.MovableNaturalObjectMaterialFixedBounceHeightMillimeters,
                CharacterTestDeterministicUnits.MetersToMillimeters(ToFloat(state.Material.FixedBounceHeight)));
            accumulator.AddInt(NetworkRootStateFieldIds.MovableNaturalObjectShapeKind, (int)state.ShapeKind);
            accumulator.AddInt(NetworkRootStateFieldIds.MovableNaturalObjectHalfExtentXMillimeters, state.HalfExtentXMillimeters);
            accumulator.AddInt(NetworkRootStateFieldIds.MovableNaturalObjectHalfExtentYMillimeters, state.HalfExtentYMillimeters);
            accumulator.AddInt(NetworkRootStateFieldIds.MovableNaturalObjectHalfExtentZMillimeters, state.HalfExtentZMillimeters);
            accumulator.AddInt(NetworkRootStateFieldIds.MovableNaturalObjectXMillimeters, state.XMillimeters);
            accumulator.AddInt(NetworkRootStateFieldIds.MovableNaturalObjectYMillimeters, state.YMillimeters);
            accumulator.AddInt(NetworkRootStateFieldIds.MovableNaturalObjectZMillimeters, state.ZMillimeters);
            accumulator.AddInt(NetworkRootStateFieldIds.MovableNaturalObjectVelocityXMillimetersPerSecond,
                state.VelocityXMillimetersPerSecond);
            accumulator.AddInt(NetworkRootStateFieldIds.MovableNaturalObjectVelocityYMillimetersPerSecond,
                state.VelocityYMillimetersPerSecond);
            accumulator.AddInt(NetworkRootStateFieldIds.MovableNaturalObjectVelocityZMillimetersPerSecond,
                state.VelocityZMillimetersPerSecond);
            accumulator.AddInt(NetworkRootStateFieldIds.MovableNaturalObjectContactVelocityXMillimetersPerSecond,
                state.ContactVelocityXMillimetersPerSecond);
            accumulator.AddInt(NetworkRootStateFieldIds.MovableNaturalObjectContactVelocityZMillimetersPerSecond,
                state.ContactVelocityZMillimetersPerSecond);
            accumulator.AddInt(NetworkRootStateFieldIds.MovableNaturalObjectContactDisplacementXMillimeters,
                state.ContactDisplacementXMillimeters);
            accumulator.AddInt(NetworkRootStateFieldIds.MovableNaturalObjectContactDisplacementZMillimeters,
                state.ContactDisplacementZMillimeters);
            accumulator.AddInt(NetworkRootStateFieldIds.MovableNaturalObjectContactPressure, state.ContactPressure);
            accumulator.AddInt(NetworkRootStateFieldIds.MovableNaturalObjectContactPairKind, (int)state.ContactKind);
            accumulator.AddInt(NetworkRootStateFieldIds.MovableNaturalObjectContactAId, state.ContactAId);
            accumulator.AddInt(NetworkRootStateFieldIds.MovableNaturalObjectContactBId, state.ContactBId);
            accumulator.AddInt(NetworkRootStateFieldIds.MovableNaturalObjectBehaviorState, (int)state.BehaviorStateCode);
            accumulator.AddInt(NetworkRootStateFieldIds.MovableNaturalObjectContactCount, state.ContactCount);
            AddPotencyTripletHash(accumulator, NetworkRootStateFieldIds.MovableNaturalObjectBasePotency, state.BasePotency);
            AddPotencyTripletHash(accumulator, NetworkRootStateFieldIds.MovableNaturalObjectCurrentPotency, state.CurrentPotency);
            AddPotencyTripletHash(accumulator, NetworkRootStateFieldIds.MovableNaturalObjectPendingPotency, state.PendingPotency);
            accumulator.AddInt(NetworkRootStateFieldIds.MovableNaturalObjectPendingPotencyInfluenceKind,
                (int)state.PendingPotencyInfluence);
            AddPotencyProfileHash(accumulator, NetworkRootStateFieldIds.MovableNaturalObjectPotencyProfile, state.PotencyProfile);
            AddPotencyApplicationHash(
                accumulator,
                NetworkRootStateFieldIds.MovableNaturalObjectLatestPotencyApplication,
                state.LatestPotencyApplication);
        }

        private static class PotencyTripletHashFields
        {
            internal const int Motion = 1;
            internal const int Cohesion = 2;
            internal const int Scatter = 3;
        }

        private static class PotencyProfileHashFields
        {
            internal const int ImmuneMotion = 1;
            internal const int ImmuneCohesion = 2;
            internal const int ImmuneScatter = 3;
            internal const int BurningEnabled = 4;
            internal const int CrushEnabled = 5;
            internal const int CrackEnabled = 6;
            internal const int CondensedLayerEnabled = 7;
            internal const int BurningMinMotion = 8;
            internal const int BurningMinScatter = 9;
            internal const int BurningMaxCohesion = 10;
            internal const int CondensedLayerMinCohesion = 11;
            internal const int CondensedLayerMaxMotion = 12;
        }

        private static class PotencyApplicationHashFields
        {
            internal const int Frame = 1;
            internal const int TargetNumericId = 2;
            internal const int InfluenceKind = 3;
            internal const int Before = 4;
            internal const int RawDelta = 5;
            internal const int AppliedDelta = 6;
            internal const int IgnoredDelta = 7;
            internal const int After = 8;
            internal const int ImmuneMotion = 9;
            internal const int ImmuneCohesion = 10;
            internal const int ImmuneScatter = 11;
            internal const int BurningThresholdMet = 12;
            internal const int CrushThresholdMet = 13;
            internal const int CrackThresholdMet = 14;
            internal const int CondensedLayerThresholdMet = 15;
            internal const int ResultKind = 16;
            internal const int ResultSource = 17;
        }

        private static void AddPotencyTripletHash(
            RuntimeHashAccumulator accumulator,
            int key,
            CharacterTestGenesisPotencyTriplet triplet)
        {
            accumulator.BeginObject(key, RuntimeHashSchemaIds.GenesisPotencyTriplet, 1);
            accumulator.AddInt(PotencyTripletHashFields.Motion, triplet.Motion);
            accumulator.AddInt(PotencyTripletHashFields.Cohesion, triplet.Cohesion);
            accumulator.AddInt(PotencyTripletHashFields.Scatter, triplet.Scatter);
            accumulator.EndObject();
        }

        private static void AddPotencyProfileHash(
            RuntimeHashAccumulator accumulator,
            int key,
            CharacterTestGenesisPotencyProfile profile)
        {
            accumulator.BeginObject(key, RuntimeHashSchemaIds.GenesisPotencyProfile, 1);
            accumulator.AddInt(PotencyProfileHashFields.ImmuneMotion, profile.ImmuneMotion ? 1 : 0);
            accumulator.AddInt(PotencyProfileHashFields.ImmuneCohesion, profile.ImmuneCohesion ? 1 : 0);
            accumulator.AddInt(PotencyProfileHashFields.ImmuneScatter, profile.ImmuneScatter ? 1 : 0);
            accumulator.AddInt(PotencyProfileHashFields.BurningEnabled, profile.BurningEnabled ? 1 : 0);
            accumulator.AddInt(PotencyProfileHashFields.CrushEnabled, profile.CrushEnabled ? 1 : 0);
            accumulator.AddInt(PotencyProfileHashFields.CrackEnabled, profile.CrackEnabled ? 1 : 0);
            accumulator.AddInt(PotencyProfileHashFields.CondensedLayerEnabled, profile.CondensedLayerEnabled ? 1 : 0);
            accumulator.AddInt(PotencyProfileHashFields.BurningMinMotion, profile.BurningMinMotion);
            accumulator.AddInt(PotencyProfileHashFields.BurningMinScatter, profile.BurningMinScatter);
            accumulator.AddInt(PotencyProfileHashFields.BurningMaxCohesion, profile.BurningMaxCohesion);
            accumulator.AddInt(PotencyProfileHashFields.CondensedLayerMinCohesion, profile.CondensedLayerMinCohesion);
            accumulator.AddInt(PotencyProfileHashFields.CondensedLayerMaxMotion, profile.CondensedLayerMaxMotion);
            accumulator.EndObject();
        }

        private static void AddPotencyApplicationHash(
            RuntimeHashAccumulator accumulator,
            int key,
            CharacterTestGenesisPotencyApplicationRecord record)
        {
            accumulator.BeginObject(key, RuntimeHashSchemaIds.GenesisPotencyApplication, 1);
            accumulator.AddLong(PotencyApplicationHashFields.Frame, record.Frame.Value);
            accumulator.AddInt(PotencyApplicationHashFields.TargetNumericId, record.TargetNumericId);
            accumulator.AddInt(PotencyApplicationHashFields.InfluenceKind, (int)record.InfluenceKind);
            AddPotencyTripletHash(accumulator, PotencyApplicationHashFields.Before, record.Before);
            AddPotencyTripletHash(accumulator, PotencyApplicationHashFields.RawDelta, record.RawDelta);
            AddPotencyTripletHash(accumulator, PotencyApplicationHashFields.AppliedDelta, record.AppliedDelta);
            AddPotencyTripletHash(accumulator, PotencyApplicationHashFields.IgnoredDelta, record.IgnoredDelta);
            AddPotencyTripletHash(accumulator, PotencyApplicationHashFields.After, record.After);
            accumulator.AddInt(PotencyApplicationHashFields.ImmuneMotion, record.ImmuneMotion ? 1 : 0);
            accumulator.AddInt(PotencyApplicationHashFields.ImmuneCohesion, record.ImmuneCohesion ? 1 : 0);
            accumulator.AddInt(PotencyApplicationHashFields.ImmuneScatter, record.ImmuneScatter ? 1 : 0);
            accumulator.AddInt(PotencyApplicationHashFields.BurningThresholdMet, record.BurningThresholdMet ? 1 : 0);
            accumulator.AddInt(PotencyApplicationHashFields.CrushThresholdMet, record.CrushThresholdMet ? 1 : 0);
            accumulator.AddInt(PotencyApplicationHashFields.CrackThresholdMet, record.CrackThresholdMet ? 1 : 0);
            accumulator.AddInt(
                PotencyApplicationHashFields.CondensedLayerThresholdMet,
                record.CondensedLayerThresholdMet ? 1 : 0);
            accumulator.AddInt(PotencyApplicationHashFields.ResultKind, (int)record.Result);
            accumulator.AddInt(PotencyApplicationHashFields.ResultSource, (int)record.ResultSourceCode);
            accumulator.EndObject();
        }

        private static CharacterTestCharacterControlOwner NormalizeNonOwnedControlOwner(
            CharacterTestCharacterControlOwner owner)
        {
            return CanApplyPlayerCommand(owner)
                ? CharacterTestCharacterControlOwner.RemotePlayer
                : owner;
        }

        private void AddCharacterEntityHash(
            RuntimeHashAccumulator accumulator,
            int index,
            CharacterTestCharacterEntity entity,
            CharacterTestCharacterControlOwner? controlOwnerOverride = null,
            CharacterTestCharacterActionRuntime actionRuntime = null)
        {
            accumulator.AddInt(NetworkRootStateFieldIds.CharacterIndex, index);
            accumulator.AddInt(NetworkRootStateFieldIds.CharacterEntityId, entity.Id.Value);
            accumulator.AddInt(NetworkRootStateFieldIds.CharacterCharacterId, entity.Identity.CharacterId);
            accumulator.AddInt(NetworkRootStateFieldIds.CharacterTeamId, entity.Team.TeamId);
            accumulator.AddInt(NetworkRootStateFieldIds.CharacterDefaultTeamId, entity.Team.DefaultTeamId);
            accumulator.AddInt(NetworkRootStateFieldIds.CharacterControlOwner,
                (int)(controlOwnerOverride ?? entity.Control.Owner));
            accumulator.AddInt(NetworkRootStateFieldIds.CharacterRuntimeAiPlannerProfileSlot,
                entity.Control.RuntimeAiPlannerProfileSlot);
            CharacterTestRuntimeAiBehaviorSelection selection =
                entity.Control.RuntimeAiBehaviorSelection;
            accumulator.AddInt(NetworkRootStateFieldIds.CharacterRuntimeAiBehaviorLoadoutId, selection.LoadoutId);
            accumulator.AddInt(NetworkRootStateFieldIds.CharacterRuntimeAiBehaviorVariantId, selection.VariantId);
            accumulator.AddLong(NetworkRootStateFieldIds.CharacterRuntimeAiBehaviorCatalogHash, selection.CatalogHash);
            accumulator.AddInt(NetworkRootStateFieldIds.CharacterRuntimeAiBehaviorProfileSlot, selection.ProfileSlot);
            accumulator.AddInt(NetworkRootStateFieldIds.CharacterXMillimeters, entity.Transform.XMillimeters);
            accumulator.AddInt(NetworkRootStateFieldIds.CharacterYMillimeters, entity.Transform.YMillimeters);
            accumulator.AddInt(NetworkRootStateFieldIds.CharacterZMillimeters, entity.Transform.ZMillimeters);
            accumulator.AddInt(NetworkRootStateFieldIds.CharacterYawMilliDegrees, entity.Transform.YawMilliDegrees);
            accumulator.AddInt(NetworkRootStateFieldIds.CharacterSourcePoseRevision, entity.SourcePoseRevision);
            accumulator.AddInt(NetworkRootStateFieldIds.CharacterSourceFacingRevision, entity.SourceFacingRevision);
            accumulator.AddInt(NetworkRootStateFieldIds.CharacterInputMoveXMilli, entity.Input.MoveXMilli);
            accumulator.AddInt(NetworkRootStateFieldIds.CharacterInputMoveYMilli, entity.Input.MoveYMilli);
            accumulator.AddInt(NetworkRootStateFieldIds.CharacterInputHasWorldMove, entity.Input.HasWorldMove ? 1 : 0);
            accumulator.AddInt(NetworkRootStateFieldIds.CharacterInputWorldMoveXMilli, entity.Input.WorldMoveXMilli);
            accumulator.AddInt(NetworkRootStateFieldIds.CharacterInputWorldMoveYMilli, entity.Input.WorldMoveYMilli);
            accumulator.AddInt(NetworkRootStateFieldIds.CharacterInputTurnInPlace, entity.Input.TurnInPlace ? 1 : 0);
            entity.Input.BehaviorControl.AddHash(ref accumulator, NetworkRootStateFieldIds.CharacterBehaviorControl);
            accumulator.AddInt(NetworkRootStateFieldIds.CharacterInputTurnInPlaceYawMilliDegrees,
                entity.Input.TurnInPlaceYawMilliDegrees);
            accumulator.AddInt(NetworkRootStateFieldIds.CharacterInputControlIntentSourceId,
                entity.Input.ControlIntentSourceId);
            accumulator.AddInt(NetworkRootStateFieldIds.CharacterInputMovementSpeedScaleMilli,
                entity.Input.MovementSpeedScaleMilli <= 0
                    ? CharacterTestControlIntent.DefaultMovementSpeedScaleMilli
                    : entity.Input.MovementSpeedScaleMilli);
            accumulator.AddInt(NetworkRootStateFieldIds.CharacterJumpPressed, entity.Input.JumpPressed ? 1 : 0);
            accumulator.AddInt(NetworkRootStateFieldIds.CharacterJumpHeld, entity.Input.JumpHeld ? 1 : 0);
            accumulator.AddInt(NetworkRootStateFieldIds.CharacterJumpReleased, entity.Input.JumpReleased ? 1 : 0);
            accumulator.AddInt(NetworkRootStateFieldIds.CharacterAttackPrimaryPressed, entity.Input.AttackPrimaryPressed ? 1 : 0);
            accumulator.AddInt(NetworkRootStateFieldIds.CharacterAttackSecondaryPressed, entity.Input.AttackSecondaryPressed ? 1 : 0);
            accumulator.AddInt(NetworkRootStateFieldIds.CharacterDodgePressed, entity.Input.DodgePressed ? 1 : 0);
            accumulator.AddInt(NetworkRootStateFieldIds.CharacterDebugPrimaryPressed, entity.Input.DebugPrimaryPressed ? 1 : 0);
            accumulator.AddInt(NetworkRootStateFieldIds.CharacterDebugSecondaryPressed, entity.Input.DebugSecondaryPressed ? 1 : 0);
            accumulator.AddInt(NetworkRootStateFieldIds.CharacterMaxHealth, entity.CombatState.MaxHealth);
            accumulator.AddInt(NetworkRootStateFieldIds.CharacterHealth, entity.CombatState.Health);
            accumulator.AddInt(NetworkRootStateFieldIds.CharacterHitStun, entity.CombatState.HitStunFramesRemaining);
            accumulator.AddInt(NetworkRootStateFieldIds.CharacterLastDamage, entity.CombatState.LastDamage);
            accumulator.AddLong(NetworkRootStateFieldIds.CharacterLastHitFrame, entity.CombatState.LastHitFrame);
            accumulator.AddInt(NetworkRootStateFieldIds.CharacterLastHitSource, entity.CombatState.LastHitSourceEntityId);
            accumulator.AddInt(NetworkRootStateFieldIds.CharacterLastHitTrace, entity.CombatState.LastHitTraceId);
            CharacterTestBattleStateComponent battleState = entity.BattleState;
            accumulator.AddInt(NetworkRootStateFieldIds.CharacterBattleLifeState, (int)battleState.LifeState);
            accumulator.AddInt(NetworkRootStateFieldIds.CharacterBattleDominantReason, (int)battleState.DominantReason);
            accumulator.AddInt(NetworkRootStateFieldIds.CharacterBattleGateFlags, (int)battleState.GateFlags);
            accumulator.AddLong(NetworkRootStateFieldIds.CharacterBattleSinceFrame, battleState.SinceFrame);
            accumulator.AddLong(NetworkRootStateFieldIds.CharacterBattleLastTransitionFrame,
                battleState.LastTransitionFrame);
            accumulator.AddInt(NetworkRootStateFieldIds.CharacterBattleSourceActionId,
                battleState.SourceActionId.Value);
            accumulator.AddInt(NetworkRootStateFieldIds.CharacterBattleSourceTraceId, battleState.SourceTraceId);
            CharacterTestSpecialHitStateComponent specialHitState = entity.SpecialHitState;
            accumulator.AddInt(NetworkRootStateFieldIds.CharacterSpecialHitState, (int)specialHitState.State);
            accumulator.AddInt(NetworkRootStateFieldIds.CharacterSpecialHitPhase, (int)specialHitState.Phase);
            accumulator.AddLong(NetworkRootStateFieldIds.CharacterSpecialHitSinceFrame, specialHitState.SinceFrame);
            accumulator.AddInt(NetworkRootStateFieldIds.CharacterSpecialHitSource, specialHitState.SourceEntityId);
            accumulator.AddInt(NetworkRootStateFieldIds.CharacterSpecialHitTrace, specialHitState.TraceId);
            accumulator.AddInt(NetworkRootStateFieldIds.CharacterLaunchVelocityYMillimetersPerSecond,
                specialHitState.LaunchVelocityYMillimetersPerSecond);
            accumulator.AddInt(NetworkRootStateFieldIds.CharacterLaunchHorizontalImpulseMillimetersPerSecond,
                specialHitState.HorizontalImpulseMillimetersPerSecond);
            accumulator.AddInt(NetworkRootStateFieldIds.CharacterActiveActionId, entity.Action.ActiveActionId.Value);
            accumulator.AddInt(NetworkRootStateFieldIds.CharacterActionState, (int)entity.Action.State);
            accumulator.AddInt(NetworkRootStateFieldIds.CharacterActionPhase, (int)entity.Action.Phase);
            accumulator.AddInt(NetworkRootStateFieldIds.CharacterActionLocalFrame, entity.Action.LocalFrame);
            CharacterActionExecutionState runnerState = default;
            bool hasRunnerState = actionRuntime != null
                && actionRuntime.TryCreateRunnerRestoreState(entity.Id, out runnerState);
            accumulator.AddLong(NetworkRootStateFieldIds.CharacterActionRunnerActiveInstanceId,
                hasRunnerState ? runnerState.ActiveActionInstanceId : 0L);
            accumulator.AddLong(NetworkRootStateFieldIds.CharacterActionRunnerNextInstanceId,
                hasRunnerState ? runnerState.NextInstanceId : 1L);
            accumulator.AddInt(NetworkRootStateFieldIds.CharacterActionRunnerActionId,
                hasRunnerState ? runnerState.ActionId.Value : 0);
            accumulator.AddInt(NetworkRootStateFieldIds.CharacterActionRunnerState,
                hasRunnerState ? (int)runnerState.State : (int)CharacterActionInstanceState.None);
            accumulator.AddInt(NetworkRootStateFieldIds.CharacterActionRunnerPhase,
                hasRunnerState ? (int)runnerState.CurrentPhase : (int)CharacterActionPhaseKind.None);
            accumulator.AddInt(NetworkRootStateFieldIds.CharacterActionRunnerLocalFrame,
                hasRunnerState ? runnerState.LocalFrame : 0);
            accumulator.AddInt(NetworkRootStateFieldIds.CharacterActionAnimationTransitionFrames, entity.Action.AnimationTransitionFrames);
            accumulator.AddInt(NetworkRootStateFieldIds.CharacterActionAnimationHoldFrames, entity.Action.AnimationHoldFrames);
            accumulator.AddInt(NetworkRootStateFieldIds.CharacterActionAnimationPlaybackSpeedMilli,
                entity.Action.AnimationPlaybackSpeedMilli);
            accumulator.AddInt(NetworkRootStateFieldIds.CharacterJumpImpulseYMillimetersPerSecond,
                entity.Action.JumpImpulseYMillimetersPerSecond);
            accumulator.AddInt(NetworkRootStateFieldIds.CharacterActionMovementMode, (int)entity.Action.MovementMode);
            accumulator.AddInt(NetworkRootStateFieldIds.CharacterActionMovementLocked, entity.Action.MovementLocked ? 1 : 0);
            accumulator.AddInt(NetworkRootStateFieldIds.CharacterActionMovementControlScaleMilli,
                entity.Action.MovementControlScaleMilli);
            accumulator.AddInt(NetworkRootStateFieldIds.CharacterActionRotationControlScaleMilli,
                entity.Action.RotationControlScaleMilli);
            entity.GroundPointTraction.AddHash(ref accumulator);
            entity.PositionRecall.AddHash(ref accumulator);
            AddFixedAssemblyHash(ref accumulator, entity);
            accumulator.AddInt(NetworkRootStateFieldIds.CharacterVerticalVelocityMillimetersPerSecond,
                entity.VerticalVelocityMillimetersPerSecond);
            accumulator.AddInt(NetworkRootStateFieldIds.CharacterHorizontalVelocityXMillimetersPerSecond,
                entity.HorizontalVelocityXMillimetersPerSecond);
            accumulator.AddInt(NetworkRootStateFieldIds.CharacterHorizontalVelocityYMillimetersPerSecond,
                entity.HorizontalVelocityYMillimetersPerSecond);
            accumulator.AddInt(NetworkRootStateFieldIds.CharacterDesiredVelocityXMillimetersPerSecond,
                entity.DesiredVelocityXMillimetersPerSecond);
            accumulator.AddInt(NetworkRootStateFieldIds.CharacterDesiredVelocityYMillimetersPerSecond,
                entity.DesiredVelocityYMillimetersPerSecond);
            accumulator.AddInt(NetworkRootStateFieldIds.CharacterLastMoveInputXMilli,
                entity.LastMoveInputXMilli);
            accumulator.AddInt(NetworkRootStateFieldIds.CharacterLastMoveInputYMilli,
                entity.LastMoveInputYMilli);
            accumulator.AddInt(NetworkRootStateFieldIds.CharacterIsGrounded, entity.IsGrounded ? 1 : 0);
            accumulator.AddInt(NetworkRootStateFieldIds.CharacterJumpBufferFramesRemaining,
                entity.JumpBufferFramesRemaining);
            accumulator.AddInt(NetworkRootStateFieldIds.CharacterCoyoteTimeFramesRemaining,
                entity.CoyoteTimeFramesRemaining);
            accumulator.AddInt(NetworkRootStateFieldIds.CharacterMovementMode, (int)entity.MovementMode);
            accumulator.AddInt(NetworkRootStateFieldIds.CharacterMovementLocked, entity.MovementLocked ? 1 : 0);
            accumulator.AddInt(NetworkRootStateFieldIds.CharacterMovementControlScaleMilli,
                entity.MovementControlScaleMilli);
            accumulator.AddInt(NetworkRootStateFieldIds.CharacterRotationControlScaleMilli,
                entity.RotationControlScaleMilli);
            accumulator.AddInt(
                NetworkRootStateFieldIds.CharacterMovementProfileId,
                entity.MovementProfileId);
            accumulator.AddInt(NetworkRootStateFieldIds.CharacterMovementProfileFallback, entity.MovementProfileFallback ? 1 : 0);
            accumulator.AddInt(NetworkRootStateFieldIds.CharacterPhysicsEnabled, entity.PhysicsEnabled ? 1 : 0);
            accumulator.AddInt(NetworkRootStateFieldIds.CharacterCollisionFlags, (int)entity.CollisionFlags);
            accumulator.AddInt(NetworkRootStateFieldIds.CharacterSurfaceMaterialKind, (int)entity.SurfaceMaterialKind);
            accumulator.AddInt(NetworkRootStateFieldIds.CharacterSurfaceFrictionMilli,
                entity.SurfaceFrictionMilli);
            accumulator.AddInt(NetworkRootStateFieldIds.CharacterSurfaceElasticityMilli,
                CharacterTestDeterministicUnits.ScaleToMilli(entity.SurfaceElasticity));
            accumulator.AddInt(NetworkRootStateFieldIds.CharacterSurfaceFixedBounceHeightMillimeters,
                CharacterTestDeterministicUnits.MetersToMillimeters(entity.SurfaceFixedBounceHeight));
            accumulator.AddInt(NetworkRootStateFieldIds.CharacterSurfaceBounceSpeedCapYMillimetersPerSecond,
                entity.SurfaceBounceSpeedCapYMillimetersPerSecond);
            accumulator.AddInt(NetworkRootStateFieldIds.CharacterPhysicsCapsuleRadiusMillimeters, entity.Physics.CapsuleRadiusMillimeters);
            accumulator.AddInt(NetworkRootStateFieldIds.CharacterPhysicsCapsuleHeightMillimeters, entity.Physics.CapsuleHeightMillimeters);
            accumulator.AddInt(NetworkRootStateFieldIds.CharacterPhysicsCapsuleCenterYMillimeters, entity.Physics.CapsuleCenterYMillimeters);
            accumulator.AddInt(NetworkRootStateFieldIds.CharacterPhysicsMassGrams, entity.Physics.MassGrams);
            accumulator.AddInt(NetworkRootStateFieldIds.CharacterPhysicsMassScaleMilli, entity.Physics.MassScaleMilli);
            accumulator.AddInt(NetworkRootStateFieldIds.CharacterPhysicsRadiusScaleMilli, entity.Physics.RadiusScaleMilli);
            accumulator.AddInt(NetworkRootStateFieldIds.CharacterPhysicsPushScaleMilli, entity.Physics.PushScaleMilli);
            accumulator.AddInt(NetworkRootStateFieldIds.CharacterPhysicsEffectiveRadiusMillimeters, entity.Physics.EffectiveRadiusMillimeters);
            accumulator.AddInt(NetworkRootStateFieldIds.CharacterPhysicsEffectiveHeightMillimeters, entity.Physics.EffectiveHeightMillimeters);
            accumulator.AddInt(NetworkRootStateFieldIds.CharacterPhysicsEffectiveMassGrams, entity.Physics.EffectiveMassGrams);
            accumulator.AddInt(NetworkRootStateFieldIds.CharacterPhysicsBlocksCharacters, entity.Physics.BlocksCharacters ? 1 : 0);
            accumulator.AddInt(NetworkRootStateFieldIds.CharacterPhysicsCanBePushed, entity.Physics.CanBePushed ? 1 : 0);
            CharacterTestColliderPostureComponent colliderPosture = entity.ColliderPosture;
            accumulator.AddInt(
                NetworkRootStateFieldIds.CharacterColliderProfileId,
                colliderPosture.ProfileId);
            accumulator.AddInt(NetworkRootStateFieldIds.CharacterColliderHeightMillimeters, colliderPosture.HeightMillimeters);
            accumulator.AddInt(NetworkRootStateFieldIds.CharacterColliderRadiusMillimeters, colliderPosture.RadiusMillimeters);
            accumulator.AddInt(NetworkRootStateFieldIds.CharacterColliderCenterYMillimeters, colliderPosture.CenterYMillimeters);
            accumulator.AddInt(NetworkRootStateFieldIds.CharacterColliderBlocksCharacters, colliderPosture.BlocksCharacters ? 1 : 0);
            accumulator.AddInt(NetworkRootStateFieldIds.CharacterColliderCanBePushed, colliderPosture.CanBePushed ? 1 : 0);
            accumulator.AddInt(NetworkRootStateFieldIds.CharacterColliderSource, (int)colliderPosture.Source);
            accumulator.AddInt(NetworkRootStateFieldIds.CharacterColliderPriority, colliderPosture.Priority);
            accumulator.AddLong(NetworkRootStateFieldIds.CharacterColliderSinceFrame, colliderPosture.SinceFrame);
            accumulator.AddInt(
                NetworkRootStateFieldIds.CharacterColliderActionProfileId,
                colliderPosture.ActionProfileId);
            accumulator.AddInt(NetworkRootStateFieldIds.CharacterColliderActionRestorePolicy, (int)colliderPosture.ActionRestorePolicy);
            accumulator.AddInt(NetworkRootStateFieldIds.CharacterColliderActionTransitionFrames, colliderPosture.ActionTransitionFrames);
            accumulator.AddInt(NetworkRootStateFieldIds.CharacterColliderActionSourceActionId, colliderPosture.ActionSourceActionId.Value);
            accumulator.AddInt(
                NetworkRootStateFieldIds.CharacterColliderActionStableEventId,
                colliderPosture.ActionStableEventId);
            accumulator.AddInt(NetworkRootStateFieldIds.CharacterPhysicsContactVelocityXMillimetersPerSecond,
                entity.Physics.ContactVelocityXMillimetersPerSecond);
            accumulator.AddInt(NetworkRootStateFieldIds.CharacterPhysicsContactVelocityYMillimetersPerSecond,
                entity.Physics.ContactVelocityYMillimetersPerSecond);
            // Per-frame contact displacement/count are derived observability values. They can
            // differ at a replay bootstrap boundary even when the resulting pose and every
            // forward-affecting authority field are identical. Contact velocity remains hashed
            // because the next motion step consumes it.
            AddLivingNaturalAttributesHash(accumulator, entity.LivingNaturalAttributes);
            if (entity.AuthorityPose != null) entity.AuthorityPose.AddStateHash(
                accumulator,
                NetworkRootStateFieldIds.CharacterAuthorityPose);
            else CharacterAuthorityPoseHashUtility.AddToHash(
                accumulator,
                NetworkRootStateFieldIds.CharacterAuthorityPose,
                null);
        }

        private static void AddLivingNaturalAttributesHash(
            RuntimeHashAccumulator accumulator,
            CharacterTestLivingNaturalAttributesRuntimeState state)
        {
            accumulator.AddInt(NetworkRootStateFieldIds.CharacterLivingNaturalAttributesLifecycle, (int)state.LifecycleState);
            accumulator.AddInt(NetworkRootStateFieldIds.CharacterLivingNaturalAttributesLastOperation, (int)state.LastOperation);
            accumulator.AddInt(NetworkRootStateFieldIds.CharacterLivingNaturalAttributesBodyBoundaryPresent,
                state.BodyBoundaryPresent ? 1 : 0);
            accumulator.AddInt(NetworkRootStateFieldIds.CharacterLivingNaturalAttributesBodyScaleMillimeters,
                state.BodyScaleMillimeters);
            accumulator.AddInt(NetworkRootStateFieldIds.CharacterLivingNaturalAttributesMassGrams, state.MassGrams);
            accumulator.AddInt(NetworkRootStateFieldIds.CharacterLivingNaturalAttributesContactSurfaceCount,
                state.ContactSurfaceCount);
            accumulator.AddInt(NetworkRootStateFieldIds.CharacterLivingNaturalAttributesNearFieldContactCount,
                state.NearFieldContactCount);
            accumulator.AddInt(NetworkRootStateFieldIds.CharacterLivingNaturalAttributesSelfCohesion, state.SelfCohesion);
            accumulator.AddInt(NetworkRootStateFieldIds.CharacterLivingNaturalAttributesCohesionLimit, state.CohesionLimit);
            accumulator.AddInt(NetworkRootStateFieldIds.CharacterLivingNaturalAttributesExternalPressure, state.ExternalPressure);
            accumulator.AddInt(NetworkRootStateFieldIds.CharacterLivingNaturalAttributesScatter, state.Scatter);
            accumulator.AddInt(NetworkRootStateFieldIds.CharacterLivingNaturalAttributesRecoveryTicks, state.RecoveryTicks);
            accumulator.AddInt(NetworkRootStateFieldIds.CharacterLivingNaturalAttributesRecoveryAmount, state.RecoveryAmount);
            accumulator.AddLong(NetworkRootStateFieldIds.CharacterLivingNaturalAttributesLastInputFrame, state.LastInputFrame);
            accumulator.AddLong(NetworkRootStateFieldIds.CharacterLivingNaturalAttributesLastTransitionFrame, state.LastTransitionFrame);
            accumulator.AddInt(NetworkRootStateFieldIds.CharacterLivingNaturalAttributesCanEmbody, state.CanEmbody ? 1 : 0);
            accumulator.AddInt(NetworkRootStateFieldIds.CharacterLivingNaturalAttributesCanSelfMaintain,
                state.CanSelfMaintain ? 1 : 0);
            accumulator.AddInt(NetworkRootStateFieldIds.CharacterLivingNaturalAttributesCanRecover, state.CanRecover ? 1 : 0);
            accumulator.AddInt(NetworkRootStateFieldIds.CharacterLivingNaturalAttributesCanAct, state.CanAct ? 1 : 0);
            accumulator.AddInt(NetworkRootStateFieldIds.CharacterLivingNaturalAttributesMaterialReading, (int)state.MaterialReading);
            accumulator.AddInt(NetworkRootStateFieldIds.CharacterLivingNaturalAttributesReturnedToMaterial,
                state.ReturnedToMaterial ? 1 : 0);
        }

        private CharacterTestNetworkSnapshotDocument CaptureDocument(RuntimeFrame frame, string receiverPeerId)
        {
            _authorityAvailabilityGuard?.Invoke();
            int controlledEntityId = ResolveControlledEntityId(receiverPeerId);
            CharacterTestNetworkCharacterSnapshot[] characters = _characterRuntimeModule != null
                ? _characterRuntimeModule.CaptureNetworkCharacters()
                : new CharacterTestNetworkCharacterSnapshot[0];
            PersonalizeCharacters(characters, receiverPeerId, controlledEntityId);
            return new CharacterTestNetworkSnapshotDocument
            {
                SchemaVersion = SchemaVersionValue,
                Frame = frame.Value,
                LocalPeerId = receiverPeerId ?? string.Empty,
                LocalControlledEntityId = controlledEntityId,
                Ownerships = _ownershipProvider != null
                    ? _ownershipProvider()
                    : new CharacterTestNetworkOwnershipEntry[0],
                TargetLockEnabled = _commandModule != null && _commandModule.TargetLockEnabled,
                LockedTargetEntityId = _commandModule != null ? _commandModule.LockedTargetEntityId : 0,
                LockedCombatTarget = _commandModule != null
                    ? CharacterTestNetworkCombatTargetSnapshot.FromHandle(
                        _commandModule.LockedCombatTarget)
                    : null,
                RuntimeAiBehaviorSelectionUnlocked =
                    _commandModule != null && _commandModule.RuntimeAiBehaviorSelectionUnlocked,
                Characters = characters,
                FixedAssemblyState = CaptureFixedAssemblyState(frame.Value),
                DoubleEdgeStage = _characterRuntimeModule?.World.DoubleEdgeState.CaptureProjection(),
                BodyPartStates = CharacterTestBodyPartStateNetworkSnapshotCodec.Capture(
                    _characterRuntimeModule?.Entities),
                AuthorityPoseTerrainStates =
                    CharacterTestNetworkAuthorityPoseSnapshotCodec.Capture(
                        _characterRuntimeModule?.Entities),
                MovableNaturalObjects = _characterRuntimeModule != null
                    ? _characterRuntimeModule.CaptureNetworkMovableNaturalObjects()
                    : new CharacterTestMovableNaturalObjectSnapshot[0],
                Projectiles = CharacterTestNetworkProjectileSnapshotCodec.Capture(
                    _characterRuntimeModule?.CombatTraceSystem),
                HookPulls = CharacterTestNetworkHookPullSnapshotCodec.Capture(
                    _characterRuntimeModule?.CombatTraceSystem),
                ArcMines = CharacterTestNetworkArcMineSnapshotCodec.Capture(
                    _characterRuntimeModule?.CombatTraceSystem),
                RuneTurrets = CharacterTestNetworkRuneTurretSnapshotCodec.Capture(
                    _characterRuntimeModule?.CombatTraceSystem),
                AreaConfinement = CharacterTestNetworkAreaConfinementSnapshotCodec.Capture(
                    _characterRuntimeModule?.CombatTraceSystem),
                PoisonBottlePools = CharacterTestNetworkPoisonBottlePoolSnapshotCodec.Capture(
                    _characterRuntimeModule?.CombatTraceSystem),
                PhysicalShields = CharacterTestNetworkPhysicalShieldSnapshotCodec.Capture(
                    _characterRuntimeModule?.CombatTraceSystem),
                AbilityRuntime = CharacterTestNetworkAbilityRuntimeSnapshot.Capture(_abilityRuntimeModule),
                RuntimeAiPlannerState = _runtimeAiPlannerCommandModule != null
                    ? _runtimeAiPlannerCommandModule.CreateStateSnapshot()
                    : CharacterTestRuntimeAiPlannerStateSnapshot.Empty(),
                HitResolutionPolicy = _characterRuntimeModule?.CombatTraceSystem != null
                    ? _characterRuntimeModule.CombatTraceSystem.CaptureNetworkHitResolutionPolicySnapshot()
                    : new CharacterTestHitResolutionPolicyNetworkSnapshot()
            };
        }

        private int ResolveControlledEntityId(string receiverPeerId)
        {
            if (_controlledEntityProvider == null || string.IsNullOrEmpty(receiverPeerId))
                return 0;

            int entityId = _controlledEntityProvider(receiverPeerId);
            return entityId > 0 ? entityId : 0;
        }

        private static void PersonalizeCharacters(
            CharacterTestNetworkCharacterSnapshot[] characters,
            string receiverPeerId,
            int controlledEntityId)
        {
            if (characters == null
                || characters.Length == 0
                || string.IsNullOrEmpty(receiverPeerId))
            {
                return;
            }

            for (int i = 0; i < characters.Length; i++)
            {
                CharacterTestNetworkCharacterSnapshot character = characters[i];
                if (character.EntityId == controlledEntityId)
                {
                    character.ControlOwner = CharacterTestCharacterControlOwner.LocalPlayer;
                    continue;
                }

                if (character.ControlOwner == CharacterTestCharacterControlOwner.LocalPlayer)
                    character.ControlOwner = CharacterTestCharacterControlOwner.RemotePlayer;
            }
        }

        private static bool CanApplyPlayerCommand(CharacterTestCharacterControlOwner owner)
        {
            return owner == CharacterTestCharacterControlOwner.LocalPlayer
                || owner == CharacterTestCharacterControlOwner.RemotePlayer;
        }

        private static float ToFloat(Fix64 value)
        {
            return (float)((double)value.RawValue / Fix64.Scale);
        }
    }

    public sealed class CharacterTestNetworkSnapshotDocument
    {
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        internal CharacterTestNetworkFixedAssemblySnapshot[] FixedAssemblyState { get; set; }
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        internal long[][] DoubleEdgeStage { get; set; }
        public int SchemaVersion { get; set; }
        public long Frame { get; set; }
        public string LocalPeerId { get; set; }
        public int LocalControlledEntityId { get; set; }
        public CharacterTestNetworkOwnershipEntry[] Ownerships { get; set; }
        public bool TargetLockEnabled { get; set; }
        public int LockedTargetEntityId { get; set; }
        public CharacterTestNetworkCombatTargetSnapshot LockedCombatTarget { get; set; }
        public bool RuntimeAiBehaviorSelectionUnlocked { get; set; }
        public CharacterTestNetworkCharacterSnapshot[] Characters { get; set; }
        public CharacterTestNetworkBodyPartStateSnapshot[] BodyPartStates { get; set; }
        public CharacterTestNetworkAuthorityPoseSnapshot[] AuthorityPoseTerrainStates { get; set; }
        public CharacterTestMovableNaturalObjectSnapshot[] MovableNaturalObjects { get; set; }
        public CharacterTestNetworkProjectileRuntimeSnapshot Projectiles { get; set; }
        public CharacterTestNetworkHookPullRuntimeSnapshot HookPulls { get; set; }
        public CharacterTestNetworkArcMineRuntimeSnapshot ArcMines { get; set; }
        public CharacterTestNetworkRuneTurretRuntimeSnapshot RuneTurrets { get; set; }
        public CharacterTestNetworkAreaConfinementRuntimeSnapshot AreaConfinement { get; set; }
        public CharacterTestNetworkPoisonBottlePoolRuntimeSnapshot PoisonBottlePools { get; set; }
        public CharacterTestNetworkPhysicalShieldRuntimeSnapshot PhysicalShields { get; set; }
        public CharacterTestNetworkAbilityRuntimeSnapshot AbilityRuntime { get; set; }
        public CharacterTestRuntimeAiPlannerStateSnapshot RuntimeAiPlannerState { get; set; }
        /// <summary>
        /// Whole direct-hit window state. The property name remains the
        /// schema-24 JSON name for RuntimeAiLab/network replay compatibility.
        /// </summary>
        public CharacterTestHitResolutionPolicyNetworkSnapshot HitResolutionPolicy { get; set; }

        public void NormalizeForSchema(int schemaVersion)
        {
            if (schemaVersion < CharacterTestNetworkSnapshotParticipant.CombatTargetLockSchemaVersionValue)
                LockedCombatTarget = null;
            if (schemaVersion < CharacterTestNetworkSnapshotParticipant.MovableNaturalObjectSchemaVersionValue
                || MovableNaturalObjects == null)
            {
                MovableNaturalObjects = Array.Empty<CharacterTestMovableNaturalObjectSnapshot>();
            }
            else if (schemaVersion < CharacterTestNetworkSnapshotParticipant.MovableNaturalObjectShapeSchemaVersionValue)
            {
                for (int i = 0; i < MovableNaturalObjects.Length; i++)
                {
                    if (MovableNaturalObjects[i] != null)
                        MovableNaturalObjects[i].ShapeKind = (int)CharacterTestMovableNaturalObjectShapeKind.Aabb;
                }
            }

            if (MovableNaturalObjects != null
                && schemaVersion < CharacterTestNetworkSnapshotParticipant.MovableNaturalObjectPotencySchemaVersionValue)
            {
                for (int i = 0; i < MovableNaturalObjects.Length; i++)
                    MovableNaturalObjects[i]?.ResetPotencyForLegacySchema();
            }

            if (Characters != null)
            {
                for (int i = 0; i < Characters.Length; i++)
                    {
                    Characters[i]?.NormalizeForSchema(schemaVersion);
                    if (Characters[i] != null && schemaVersion >= CharacterTestNetworkSnapshotParticipant.PositionRecallSchemaVersionValue
                        && !Characters[i].PositionRecall.IsValidSnapshot(Frame)) throw new JsonSerializationException("Invalid position recall snapshot frame.");
                    if (Characters[i] != null && !Characters[i].GroundPointTraction.IsValidSnapshot(Frame))
                        throw new JsonSerializationException("Ground traction receipt exceeds the snapshot frame.");
                }
            }

            BodyPartStates = schemaVersion <
                CharacterTestNetworkSnapshotParticipant.BodyPartStateSchemaVersionValue
                ? Array.Empty<CharacterTestNetworkBodyPartStateSnapshot>()
                : CharacterTestBodyPartStateNetworkSnapshotCodec.NormalizeCanonicalArray(
                    BodyPartStates,
                    schemaVersion);
            AuthorityPoseTerrainStates = schemaVersion <
                CharacterTestNetworkSnapshotParticipant.AuthorityPoseTerrainStateSchemaVersionValue
                ? Array.Empty<CharacterTestNetworkAuthorityPoseSnapshot>()
                : CharacterTestNetworkAuthorityPoseSnapshotCodec.NormalizeCanonicalArray(
                    AuthorityPoseTerrainStates);

            if (schemaVersion < CharacterTestNetworkSnapshotParticipant.RuntimeAiBehaviorSelectionSchemaVersionValue)
                RuntimeAiBehaviorSelectionUnlocked = false;

            if (schemaVersion < CharacterTestNetworkSnapshotParticipant.HookPullSchemaVersionValue)
                HookPulls = null;
            if (schemaVersion < CharacterTestNetworkSnapshotParticipant.ArcMineSchemaVersionValue)
                ArcMines = null;
            if (schemaVersion < CharacterTestNetworkSnapshotParticipant.RuneTurretSchemaVersionValue)
                RuneTurrets = null;
            if (schemaVersion < CharacterTestNetworkSnapshotParticipant.AreaConfinementSchemaVersionValue)
                AreaConfinement = null;
            if (schemaVersion < CharacterTestNetworkSnapshotParticipant.PoisonBottlePoolSchemaVersionValue)
                PoisonBottlePools = null;
            if (schemaVersion < CharacterTestNetworkSnapshotParticipant.PhysicalShieldSchemaVersionValue)
                PhysicalShields = null;
            if (schemaVersion < CharacterTestNetworkSnapshotParticipant.AbilityRuntimeSchemaVersionValue
                || AbilityRuntime == null)
                AbilityRuntime = CharacterTestNetworkAbilityRuntimeSnapshot.Empty();
            bool preserveActivePlannerVersion = RuntimeAiPlannerState?.HasActiveBehaviorBindings() == true;
            int originalPlannerVersion = RuntimeAiPlannerState?.SchemaVersion ?? 0;
            if (schemaVersion < CharacterTestNetworkSnapshotParticipant.RuntimeAiPlannerStateSchemaVersionValue
                || RuntimeAiPlannerState == null)
                RuntimeAiPlannerState = CharacterTestRuntimeAiPlannerStateSnapshot.Empty();
            else if (schemaVersion < CharacterTestNetworkSnapshotParticipant.DamageZoneLockSchemaVersionValue)
            {
                RuntimeAiPlannerState.SchemaVersion = CharacterTestRuntimeAiPlannerStateSnapshot.CurrentSchemaVersion;
                RuntimeAiPlannerState.DamageZoneLocks = Array.Empty<CharacterTestDamageZoneLockStateSnapshot>();
                RuntimeAiPlannerState.EngagementReservations =
                    Array.Empty<CharacterTestEngagementReservationStateSnapshot>();
            }
            else if (schemaVersion < CharacterTestNetworkSnapshotParticipant.EngagementReservationSchemaVersionValue)
            {
                RuntimeAiPlannerState.SchemaVersion = CharacterTestRuntimeAiPlannerStateSnapshot.CurrentSchemaVersion;
                RuntimeAiPlannerState.EngagementReservations =
                    Array.Empty<CharacterTestEngagementReservationStateSnapshot>();
            }
            if (RuntimeAiPlannerState.SchemaVersion
                == CharacterTestRuntimeAiPlannerStateSnapshot.LegacyActionBoundDamageZoneLockSchemaVersion)
            {
                RuntimeAiPlannerState.SchemaVersion =
                    CharacterTestRuntimeAiPlannerStateSnapshot.CurrentSchemaVersion;
            }
            RuntimeAiPlannerState.DamageZoneLocks =
                CharacterTestDamageZoneLockStateSnapshot.NormalizeCanonicalArray(
                    RuntimeAiPlannerState.DamageZoneLocks);
            if (preserveActivePlannerVersion && originalPlannerVersion != CharacterTestRuntimeAiPlannerStateSnapshot.CurrentSchemaVersion)
                RuntimeAiPlannerState.SchemaVersion = originalPlannerVersion;
            if (schemaVersion < CharacterTestNetworkSnapshotParticipant.BodyPartStateSchemaVersionValue)
            {
                for (int i = 0; i < RuntimeAiPlannerState.DamageZoneLocks.Length; i++)
                {
                    CharacterTestDamageZoneLockStateSnapshot value =
                        RuntimeAiPlannerState.DamageZoneLocks[i];
                    if (value != null)
                        value.BodyPartStateRevision = 0L;
                }
            }
            if (schemaVersion < CharacterTestNetworkSnapshotParticipant.HitResolutionPolicySchemaVersionValue
                || HitResolutionPolicy == null)
                HitResolutionPolicy = new CharacterTestHitResolutionPolicyNetworkSnapshot();
            else if (schemaVersion < CharacterTestNetworkSnapshotParticipant.HitTraceWindowSchemaVersionValue)
            {
                // Schema 24 carried only the accepted-policy ledger. It has
                // no trustworthy active-window/history payload, so restore a
                // canonical empty trace window while retaining that ledger.
                HitResolutionPolicy.ActiveTraces =
                    Array.Empty<CharacterTestHitTraceNetworkSnapshot>();
                HitResolutionPolicy.InstanceIds =
                    Array.Empty<CharacterTestHitTraceInstanceNetworkSnapshot>();
                HitResolutionPolicy.HitOnceKeys =
                    Array.Empty<CharacterTestHitOnceKeyNetworkSnapshot>();
                HitResolutionPolicy.NextInstanceId = 1;
                HitResolutionPolicy.NextHitWindowId = 1;
            }

            if (HitResolutionPolicy == null)
                HitResolutionPolicy = new CharacterTestHitResolutionPolicyNetworkSnapshot();
            if (schemaVersion < CharacterTestNetworkSnapshotParticipant.CasterBodyPreviousCenterSchemaVersionValue
                || HitResolutionPolicy.BodyHitVolumePreviousCenters == null)
            {
                HitResolutionPolicy.BodyHitVolumePreviousCenters =
                    Array.Empty<CharacterTestBodyHitVolumePreviousCenterNetworkSnapshot>();
            }
        }

        public CharacterTestNetworkSnapshotDocument CloneForRollback()
        {
            return new CharacterTestNetworkSnapshotDocument
            {
                SchemaVersion = SchemaVersion,
                Frame = Frame,
                LocalPeerId = LocalPeerId,
                LocalControlledEntityId = LocalControlledEntityId,
                Ownerships = CharacterTestNetworkOwnershipEntry.CloneArray(Ownerships),
                TargetLockEnabled = TargetLockEnabled,
                LockedTargetEntityId = LockedTargetEntityId,
                LockedCombatTarget = LockedCombatTarget?.Clone(),
                RuntimeAiBehaviorSelectionUnlocked = RuntimeAiBehaviorSelectionUnlocked,
                Characters = CharacterTestNetworkCharacterSnapshot.CloneArray(Characters),
                FixedAssemblyState = FixedAssemblyState == null ? null : Array.ConvertAll(FixedAssemblyState, row => row?.Clone()),
                DoubleEdgeStage = DoubleEdgeStage == null ? null : Array.ConvertAll(DoubleEdgeStage, row => row == null ? null : (long[])row.Clone()),
                BodyPartStates =
                    CharacterTestBodyPartStateNetworkSnapshotCodec.CloneCanonicalArray(BodyPartStates),
                AuthorityPoseTerrainStates =
                    CharacterTestNetworkAuthorityPoseSnapshotCodec.CloneCanonicalArray(
                        AuthorityPoseTerrainStates),
                MovableNaturalObjects =
                    CharacterTestMovableNaturalObjectSnapshot.CloneArray(MovableNaturalObjects),
                Projectiles = Projectiles?.CloneForRollback(),
                HookPulls = HookPulls?.CloneForRollback(),
                ArcMines = ArcMines?.CloneForRollback(),
                RuneTurrets = RuneTurrets?.CloneForRollback(),
                AreaConfinement = AreaConfinement?.CloneForRollback(),
                PoisonBottlePools = PoisonBottlePools?.CloneForRollback(),
                PhysicalShields = PhysicalShields?.CloneForRollback(),
                AbilityRuntime = AbilityRuntime?.CloneForRollback(),
                RuntimeAiPlannerState = RuntimeAiPlannerState?.CloneForRollback(),
                HitResolutionPolicy = HitResolutionPolicy?.CloneForRollback()
            };
        }
    }

    public sealed class CharacterTestNetworkAbilityRuntimeSnapshot
    {
        private static readonly JsonSerializerSettings AbilityJsonSettings = new JsonSerializerSettings
        {
            ContractResolver = new CamelCasePropertyNamesContractResolver(),
            Formatting = Formatting.None
        };

        public int SchemaVersion { get; set; }
        public string ModuleId { get; set; }
        public RuntimeHashDigest ModuleIdDigest { get; set; }
        public long DefinitionHash { get; set; }
        public CharacterTestNetworkAbilityCooldownSnapshot[] Cooldowns { get; set; }

        public static CharacterTestNetworkAbilityRuntimeSnapshot Empty()
        {
            return new CharacterTestNetworkAbilityRuntimeSnapshot
            {
                SchemaVersion = AbilityRuntimeState.CurrentSchemaVersion,
                ModuleId = string.Empty,
                ModuleIdDigest = default,
                DefinitionHash = 0L,
                Cooldowns = Array.Empty<CharacterTestNetworkAbilityCooldownSnapshot>()
            };
        }

        public static CharacterTestNetworkAbilityRuntimeSnapshot Capture(AbilityRuntimeModule module)
        {
            if (module == null)
                return Empty();

            AbilityRuntimeState state = module.CreateStateSnapshot();
            return new CharacterTestNetworkAbilityRuntimeSnapshot
            {
                SchemaVersion = state.SchemaVersion,
                ModuleId = state.ModuleId,
                ModuleIdDigest = module.ModuleIdDigest,
                DefinitionHash = state.DefinitionHash,
                Cooldowns = CharacterTestNetworkAbilityCooldownSnapshot.Capture(state.Cooldowns)
            };
        }

        public static NetworkSnapshotApplyResult Apply(
            AbilityRuntimeModule module,
            CharacterTestNetworkAbilityRuntimeSnapshot snapshot)
        {
            if (module == null)
                return NetworkSnapshotApplyResult.Succeeded();

            snapshot = snapshot ?? Empty();
            long definitionHash = snapshot.DefinitionHash != 0L
                ? snapshot.DefinitionHash
                : module.Definitions.ComputeStableHash();
            var payload = new CharacterTestNetworkAbilityRuntimeStatePayload
            {
                schemaVersion = snapshot.SchemaVersion,
                definitionHash = definitionHash,
                cooldowns = CharacterTestNetworkAbilityCooldownSnapshot.CloneArray(snapshot.Cooldowns)
            };
            string payloadJson = JsonConvert.SerializeObject(payload, AbilityJsonSettings);
            var customState = new RuntimeCustomState(
                3310,
                AbilityRuntimeSaveStateProvider.CustomStateTypeId,
                AbilityRuntimeSaveStateProvider.CustomStateSchemaVersion,
                payloadJson);
            var moduleState = new RuntimeModuleSaveState(
                module.ModuleId,
                AbilityRuntimeSaveStateProvider.CustomStateSchemaVersion,
                customState);
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
            RuntimeSaveStateResult<bool> result =
                new AbilityRuntimeSaveStateProvider(module).RestoreSaveState(saveState);
            return result.Success
                ? NetworkSnapshotApplyResult.Succeeded()
                : NetworkSnapshotApplyResult.Failed("Ability runtime restore failed: " + result.Error);
        }

        public static void AddStateHash(
            RuntimeHashAccumulator accumulator,
            CharacterTestNetworkAbilityRuntimeSnapshot snapshot,
            int casterEntityIdFilter = 0)
        {
            snapshot = snapshot ?? Empty();
            accumulator.AddInt(NetworkAbilityRuntimeSnapshotHashFields.SchemaVersion, snapshot.SchemaVersion);
            accumulator.AddDigest(
                NetworkAbilityRuntimeSnapshotHashFields.ModuleId,
                snapshot.ModuleIdDigest);
            accumulator.AddLong(NetworkAbilityRuntimeSnapshotHashFields.DefinitionHash, snapshot.DefinitionHash);
            CharacterTestNetworkAbilityCooldownSnapshot[] cooldowns =
                snapshot.Cooldowns ?? Array.Empty<CharacterTestNetworkAbilityCooldownSnapshot>();
            if (casterEntityIdFilter > 0)
            {
                int count = CountCooldownsForCaster(cooldowns, casterEntityIdFilter);
                accumulator.BeginArray(NetworkAbilityRuntimeSnapshotHashFields.Cooldowns, count);
                int index = 0;
                for (int i = 0; i < cooldowns.Length; i++)
                {
                    if (cooldowns[i] == null || cooldowns[i].CasterEntityId != casterEntityIdFilter)
                        continue;

                    accumulator.BeginElement(index);
                    cooldowns[i].Contribute(accumulator);
                    accumulator.EndElement();
                    index++;
                }
                accumulator.EndArray();
                return;
            }

            accumulator.BeginArray(NetworkAbilityRuntimeSnapshotHashFields.Cooldowns, cooldowns.Length);
            for (int i = 0; i < cooldowns.Length; i++)
            {
                accumulator.BeginElement(i);
                if (cooldowns[i] != null)
                    cooldowns[i].Contribute(accumulator);
                accumulator.EndElement();
            }
            accumulator.EndArray();
        }

        public static void AddStateHashExcludingCaster(
            RuntimeHashAccumulator accumulator,
            CharacterTestNetworkAbilityRuntimeSnapshot snapshot,
            int excludedCasterEntityId)
        {
            snapshot = snapshot ?? Empty();
            if (excludedCasterEntityId <= 0)
            {
                AddStateHash(accumulator, snapshot);
                return;
            }

            accumulator.AddInt(NetworkAbilityRuntimeSnapshotHashFields.SchemaVersion, snapshot.SchemaVersion);
            accumulator.AddDigest(
                NetworkAbilityRuntimeSnapshotHashFields.ModuleId,
                snapshot.ModuleIdDigest);
            accumulator.AddLong(NetworkAbilityRuntimeSnapshotHashFields.DefinitionHash, snapshot.DefinitionHash);
            CharacterTestNetworkAbilityCooldownSnapshot[] cooldowns =
                snapshot.Cooldowns ?? Array.Empty<CharacterTestNetworkAbilityCooldownSnapshot>();
            int count = 0;
            for (int i = 0; i < cooldowns.Length; i++)
            {
                if (cooldowns[i] != null && cooldowns[i].CasterEntityId != excludedCasterEntityId)
                    count++;
            }

            accumulator.BeginArray(NetworkAbilityRuntimeSnapshotHashFields.Cooldowns, count);
            int index = 0;
            for (int i = 0; i < cooldowns.Length; i++)
            {
                if (cooldowns[i] == null || cooldowns[i].CasterEntityId == excludedCasterEntityId)
                    continue;

                accumulator.BeginElement(index);
                cooldowns[i].Contribute(accumulator);
                accumulator.EndElement();
                index++;
            }
            accumulator.EndArray();
        }

        public CharacterTestNetworkAbilityRuntimeSnapshot CloneForRollback()
        {
            return new CharacterTestNetworkAbilityRuntimeSnapshot
            {
                SchemaVersion = SchemaVersion,
                ModuleId = ModuleId,
                DefinitionHash = DefinitionHash,
                Cooldowns = CharacterTestNetworkAbilityCooldownSnapshot.CloneArray(Cooldowns)
            };
        }

        private static int CountCooldownsForCaster(
            CharacterTestNetworkAbilityCooldownSnapshot[] cooldowns,
            int casterEntityId)
        {
            int count = 0;
            for (int i = 0; i < cooldowns.Length; i++)
            {
                if (cooldowns[i] != null && cooldowns[i].CasterEntityId == casterEntityId)
                    count++;
            }

            return count;
        }

        private sealed class CharacterTestNetworkAbilityRuntimeStatePayload
        {
            public int schemaVersion;
            public long definitionHash;
            public CharacterTestNetworkAbilityCooldownSnapshot[] cooldowns;
        }
    }

    public sealed class CharacterTestNetworkAbilityCooldownSnapshot
    {
        public int CasterEntityId { get; set; }
        public int AbilityId { get; set; }
        public long EndFrame { get; set; }

        public static CharacterTestNetworkAbilityCooldownSnapshot[] Capture(
            IReadOnlyList<AbilityRuntimeCooldownEntry> cooldowns)
        {
            if (cooldowns == null || cooldowns.Count == 0)
                return Array.Empty<CharacterTestNetworkAbilityCooldownSnapshot>();

            var result = new CharacterTestNetworkAbilityCooldownSnapshot[cooldowns.Count];
            for (int i = 0; i < cooldowns.Count; i++)
            {
                AbilityRuntimeCooldownEntry cooldown = cooldowns[i];
                result[i] = new CharacterTestNetworkAbilityCooldownSnapshot
                {
                    CasterEntityId = cooldown.CasterEntityId,
                    AbilityId = cooldown.AbilityId,
                    EndFrame = cooldown.EndFrame
                };
            }

            return result;
        }

        public static CharacterTestNetworkAbilityCooldownSnapshot[] CloneArray(
            CharacterTestNetworkAbilityCooldownSnapshot[] cooldowns)
        {
            if (cooldowns == null || cooldowns.Length == 0)
                return Array.Empty<CharacterTestNetworkAbilityCooldownSnapshot>();

            var result = new CharacterTestNetworkAbilityCooldownSnapshot[cooldowns.Length];
            for (int i = 0; i < cooldowns.Length; i++)
            {
                CharacterTestNetworkAbilityCooldownSnapshot cooldown = cooldowns[i];
                result[i] = cooldown == null
                    ? null
                    : new CharacterTestNetworkAbilityCooldownSnapshot
                    {
                        CasterEntityId = cooldown.CasterEntityId,
                        AbilityId = cooldown.AbilityId,
                        EndFrame = cooldown.EndFrame
                    };
            }

            return result;
        }

        public void Contribute(RuntimeHashAccumulator accumulator)
        {
            accumulator.AddInt(NetworkAbilityCooldownEntryHashFields.CasterEntityId, CasterEntityId);
            accumulator.AddInt(NetworkAbilityCooldownEntryHashFields.AbilityId, AbilityId);
            accumulator.AddLong(NetworkAbilityCooldownEntryHashFields.EndFrame, EndFrame);
        }

    }

    public sealed class CharacterTestNetworkOwnershipEntry
    {
        public string PeerId { get; set; }
        public int ControlledEntityId { get; set; }

        public static CharacterTestNetworkOwnershipEntry[] CloneArray(
            CharacterTestNetworkOwnershipEntry[] entries)
        {
            if (entries == null || entries.Length == 0)
                return Array.Empty<CharacterTestNetworkOwnershipEntry>();

            var result = new CharacterTestNetworkOwnershipEntry[entries.Length];
            for (int i = 0; i < entries.Length; i++)
            {
                CharacterTestNetworkOwnershipEntry entry = entries[i];
                result[i] = entry == null
                    ? null
                    : new CharacterTestNetworkOwnershipEntry
                    {
                        PeerId = entry.PeerId,
                        ControlledEntityId = entry.ControlledEntityId
                    };
            }

            return result;
        }
    }

    public sealed class CharacterTestNetworkCharacterSnapshot
    {
        public int EntityId { get; set; }
        public int CharacterId { get; set; }
        public int TeamId { get; set; }
        public int DefaultTeamId { get; set; }
        public CharacterTestCharacterControlOwner ControlOwner { get; set; }
        public int RuntimeAiPlannerProfileSlot { get; set; }
        public int RuntimeAiBehaviorLoadoutId { get; set; }
        public int RuntimeAiBehaviorVariantId { get; set; }
        public long RuntimeAiBehaviorCatalogHash { get; set; }
        public int RuntimeAiBehaviorProfileSlot { get; set; }
        public int XMillimeters { get; set; }
        public int YMillimeters { get; set; }
        public int ZMillimeters { get; set; }
        public int YawMilliDegrees { get; set; }
        public int SourcePoseRevision { get; set; } = CharacterTestCharacterSourceRevisionComponent.InitialRevision;
        public int SourceFacingRevision { get; set; } = CharacterTestCharacterSourceRevisionComponent.InitialRevision;
        public short InputMoveXMilli { get; set; }
        public short InputMoveYMilli { get; set; }
        public bool InputHasWorldMove { get; set; }
        public short InputWorldMoveXMilli { get; set; }
        public short InputWorldMoveYMilli { get; set; }
        public bool InputTurnInPlace { get; set; }
        public int InputTurnInPlaceYawMilliDegrees { get; set; }
        public CharacterTestBehaviorControlInput InputBehaviorControl { get; set; }
        public int InputControlIntentSourceId { get; set; }
        public int InputMovementSpeedScaleMilli { get; set; } =
            CharacterTestControlIntent.DefaultMovementSpeedScaleMilli;
        public bool JumpPressed { get; set; }
        public bool JumpHeld { get; set; }
        public bool JumpReleased { get; set; }
        public bool AttackPrimaryPressed { get; set; }
        public bool AttackSecondaryPressed { get; set; }
        public bool DodgePressed { get; set; }
        public CharacterTestCombatEvadeDirection CombatEvadeFirstDirection { get; set; } = CharacterTestCombatEvadeDirection.Back;
        public bool CombatEvadePendingRoll { get; set; }
        public CharacterTestCombatEvadeDirection CombatEvadePendingDirection { get; set; } = CharacterTestCombatEvadeDirection.None;
        public long CombatEvadeLastSpendFrame { get; set; } = long.MinValue;
        public int CombatEvadeRegenerationRemainder { get; set; }
        public int CombatEvadeStamina { get; set; } = 100;
        public bool DebugPrimaryPressed { get; set; }
        public bool DebugSecondaryPressed { get; set; }
        public int MaxHealth { get; set; }
        public int Health { get; set; }
        public int HitStunFramesRemaining { get; set; }
        public int LastDamage { get; set; }
        public long LastHitFrame { get; set; }
        public int LastHitSourceEntityId { get; set; }
        public int LastHitTraceId { get; set; }
        public int BattleLifeState { get; set; }
        public int BattleDominantReason { get; set; }
        public int BattleGateFlags { get; set; }
        public long BattleSinceFrame { get; set; }
        public long BattleLastTransitionFrame { get; set; }
        public CharacterActionId BattleSourceActionId { get; set; }
        public int BattleSourceTraceId { get; set; }
        [JsonIgnore]
        public bool HasExactBattleState { get; private set; }
        public CharacterActionId ActiveActionId { get; set; }
        [JsonConverter(typeof(RuntimeNumericEnumJsonConverter<CharacterTestActionStateId>))]
        public CharacterTestActionStateId ActionState { get; set; }
        [JsonConverter(typeof(RuntimeNumericEnumJsonConverter<CharacterTestActionPhaseId>))]
        public CharacterTestActionPhaseId ActionPhase { get; set; }
        public int ActionLocalFrame { get; set; }
        public long ActionRunnerActiveInstanceId { get; set; }
        public long ActionRunnerPlanId { get; set; }
        public long ActionRunnerNextInstanceId { get; set; }
        public CharacterActionId ActionRunnerActionId { get; set; }
        [JsonConverter(typeof(RuntimeNumericEnumJsonConverter<CharacterActionInstanceState>))]
        public CharacterActionInstanceState ActionRunnerState { get; set; }
        [JsonConverter(typeof(RuntimeNumericEnumJsonConverter<CharacterActionPhaseKind>))]
        public CharacterActionPhaseKind ActionRunnerPhase { get; set; }
        public int ActionRunnerLocalFrame { get; set; }
        public int ActionRunnerDurationFrames { get; set; }
        public RuntimeTraceIdentity ActionRunnerTraceId { get; set; }
        public string ActionRunnerFinishReason { get; set; }
        public CharacterActionDiagnosticCode ActionRunnerLastRejectReason { get; set; }
        [JsonIgnore] public string LastRejectReason => LastRejection.ToString();
        public int LastRejectReasonCode { get; set; }
        public int LastRejectDetailId { get; set; }
        [JsonIgnore] public CharacterTestActionRejection LastRejection => new CharacterTestActionRejection(LastRejectReasonCode, LastRejectDetailId);
        public int LastAnimationBindingId { get; set; }
        public int AnimationBindingId { get; set; }
        public int AnimationAuthorityBindingId { get; set; }
        public int AnimationRequestBindingId { get; set; }
        public CharacterTestAnimationRequestSnapshot AnimationRequest { get; set; } = new CharacterTestAnimationRequestSnapshot();

        internal bool HasValidAnimationIdentityShape()
        {
            return (LastAnimationBindingId == 0 || CharacterTestAuthoredContentIds.IsActive(400, LastAnimationBindingId))
                && (AnimationBindingId == 0 || CharacterTestAuthoredContentIds.IsActive(400, AnimationBindingId))
                && (AnimationAuthorityBindingId == 0 || AnimationAuthorityBindingId == AnimationBindingId)
                && (AnimationRequestBindingId == 0 || AnimationRequestBindingId == AnimationBindingId)
                && AnimationRequest != null && AnimationRequest.IsValid();
        }

        internal void RestoreAnimationState(CharacterTestCharacterEntity entity, CharacterTestCharacterActionRuntime runtime)
        {
            runtime.TryGetPreparedAnimation(CharacterId, AnimationBindingId, out var binding);
            CharacterTestCharacterAnimationComponent old = entity.Animation;
            string restoredActionKey = AnimationRequestBindingId > 0
                ? (AnimationAuthorityBindingId > 0 ? binding?.ActionKey : binding?.Clip.Id) ?? string.Empty : string.Empty;
            bool actionActive = entity.Action.ActiveActionId.IsValid && entity.Action.State == CharacterTestActionStateId.Running
                || CharacterTestCharacterActionSystem.IsDeathTerminalActionHold(entity);
            entity.SetAnimation(new CharacterTestCharacterAnimationComponent(old.LocomotionBlend, old.LocomotionSpeed01,
                entity.IsGrounded, !entity.IsGrounded, actionActive, restoredActionKey,
                AnimationRequestBindingId > 0 ? CharacterTestDeterministicUnits.FramesToSeconds(AnimationTransitionFrames) : 0f,
                AnimationRequestBindingId > 0 ? CharacterTestDeterministicUnits.FramesToSeconds(AnimationHoldFrames) : 0f,
                AnimationRequestBindingId > 0 ? CharacterTestDeterministicUnits.MilliToScale(AnimationPlaybackSpeedMilli) : 1f,
                AnimationBindingId, binding?.Clip.Id ?? string.Empty, AnimationAuthorityBindingId, AnimationRequestBindingId));
            runtime.TryGetPreparedAnimation(CharacterId, AnimationRequest.BindingId, out var requestBinding);
            entity.ComponentWorld.SetAnimationRequest(entity.Id,
                AnimationRequest.Restore(requestBinding?.ActionKey ?? string.Empty, requestBinding?.Clip.Id ?? string.Empty));
        }

        public string LastAnimationActionKey { get; set; }
        public string AnimationActionKeyThisFrame { get; set; }
        public int AnimationTransitionFrames { get; set; }
        public int AnimationHoldFrames { get; set; }
        public int AnimationPlaybackSpeedMilli { get; set; } = CharacterTestDeterministicUnits.ScaleFactorScale;
        public int AnimationTransitionMilliseconds { get; set; }
        public int AnimationHoldMilliseconds { get; set; }
        public int JumpImpulseYMillimetersPerSecond { get; set; }
        public CharacterMovementMode ActionMovementMode { get; set; }
        public bool ActionMovementLocked { get; set; }
        public int ActionMovementControlScaleMilli { get; set; }
        public int ActionRotationControlScaleMilli { get; set; }
        [JsonProperty] internal CharacterTestGroundPointTractionState GroundPointTraction { get; set; }
        [JsonProperty] internal CharacterTestPositionRecallState PositionRecall { get; set; }
        public int VerticalVelocityMillimetersPerSecond { get; set; }
        public int HorizontalVelocityXMillimetersPerSecond { get; set; }
        public int HorizontalVelocityYMillimetersPerSecond { get; set; }
        public int DesiredVelocityXMillimetersPerSecond { get; set; }
        public int DesiredVelocityYMillimetersPerSecond { get; set; }
        public short LastMoveInputXMilli { get; set; }
        public short LastMoveInputYMilli { get; set; }
        public bool IsGrounded { get; set; }
        public int JumpBufferFramesRemaining { get; set; }
        public int CoyoteTimeFramesRemaining { get; set; }
        public int JumpBufferMillisecondsRemaining { get; set; }
        public int CoyoteTimeMillisecondsRemaining { get; set; }
        public CharacterMovementMode MovementMode { get; set; }
        public bool MovementLocked { get; set; }
        public int MovementControlScaleMilli { get; set; }
        public int RotationControlScaleMilli { get; set; }
        public int MovementProfileId { get; set; }
        public bool MovementProfileFallback { get; set; }
        public bool PhysicsEnabled { get; set; }
        public CombatMotionCollisionFlags CollisionFlags { get; set; }
        public int SurfaceMaterialKind { get; set; }
        public string SurfaceMaterialStableId { get; set; }
        public int SurfaceFrictionMilli { get; set; }
        public int SurfaceElasticityMilli { get; set; }
        public int SurfaceFixedBounceHeightMillimeters { get; set; }
        public int SurfaceBounceSpeedCapYMillimetersPerSecond { get; set; }
        public int PhysicsCapsuleRadiusMillimeters { get; set; }
        public int PhysicsCapsuleHeightMillimeters { get; set; }
        public int PhysicsCapsuleCenterYMillimeters { get; set; }
        public int PhysicsMassGrams { get; set; }
        public int PhysicsMassScaleMilli { get; set; }
        public int PhysicsRadiusScaleMilli { get; set; }
        public int PhysicsPushScaleMilli { get; set; }
        public bool PhysicsBlocksCharacters { get; set; }
        public bool PhysicsCanBePushed { get; set; }
        public int ColliderProfileId { get; set; }
        public int ColliderHeightMillimeters { get; set; }
        public int ColliderRadiusMillimeters { get; set; }
        public int ColliderCenterYMillimeters { get; set; }
        public bool ColliderBlocksCharacters { get; set; }
        public bool ColliderCanBePushed { get; set; }
        public int ColliderSource { get; set; }
        public int ColliderPriority { get; set; }
        public long ColliderSinceFrame { get; set; }
        public int ColliderActionProfileId { get; set; }
        public string ColliderActionRestorePolicy { get; set; }
        public int ColliderActionTransitionFrames { get; set; }
        public CharacterActionId ColliderActionSourceActionId { get; set; }
        public int ColliderActionStableEventId { get; set; }
        public int PhysicsContactVelocityXMillimetersPerSecond { get; set; }
        public int PhysicsContactVelocityYMillimetersPerSecond { get; set; }
        public int PhysicsContactDisplacementXMillimeters { get; set; }
        public int PhysicsContactDisplacementYMillimeters { get; set; }
        public int PhysicsContactCount { get; set; }
        public int LivingNaturalAttributesLifecycleState { get; set; }
        public int LivingNaturalAttributesLastOperation { get; set; }
        public bool LivingNaturalAttributesBodyBoundaryPresent { get; set; }
        public int LivingNaturalAttributesBodyScaleMillimeters { get; set; }
        public int LivingNaturalAttributesMassGrams { get; set; }
        public int LivingNaturalAttributesContactSurfaceCount { get; set; }
        public int LivingNaturalAttributesNearFieldContactCount { get; set; }
        public int LivingNaturalAttributesSelfCohesion { get; set; }
        public int LivingNaturalAttributesCohesionLimit { get; set; }
        public int LivingNaturalAttributesExternalPressure { get; set; }
        public int LivingNaturalAttributesScatter { get; set; }
        public int LivingNaturalAttributesRecoveryTicks { get; set; }
        public int LivingNaturalAttributesRecoveryAmount { get; set; }
        public long LivingNaturalAttributesLastInputFrame { get; set; }
        public long LivingNaturalAttributesLastTransitionFrame { get; set; }
        public CharacterAuthorityPoseSnapshot AuthorityPose { get; set; }

        public static CharacterTestNetworkCharacterSnapshot[] CloneArray(
            CharacterTestNetworkCharacterSnapshot[] snapshots)
        {
            if (snapshots == null || snapshots.Length == 0)
                return Array.Empty<CharacterTestNetworkCharacterSnapshot>();

            var result = new CharacterTestNetworkCharacterSnapshot[snapshots.Length];
            for (int i = 0; i < snapshots.Length; i++)
                result[i] = snapshots[i]?.CloneForRollback();
            return result;
        }

        public CharacterTestNetworkCharacterSnapshot CloneForRollback()
        {
            var copy = (CharacterTestNetworkCharacterSnapshot)MemberwiseClone();
            copy.AnimationRequest = AnimationRequest?.Clone();
            return copy;
        }

        [JsonProperty("x", DefaultValueHandling = DefaultValueHandling.Ignore)]
        public float LegacyX { get; set; }
        [JsonProperty("y", DefaultValueHandling = DefaultValueHandling.Ignore)]
        public float LegacyY { get; set; }
        [JsonProperty("z", DefaultValueHandling = DefaultValueHandling.Ignore)]
        public float LegacyZ { get; set; }
        [JsonProperty("yawDegrees", DefaultValueHandling = DefaultValueHandling.Ignore)]
        public float LegacyYawDegrees { get; set; }
        [JsonProperty("inputMoveX", DefaultValueHandling = DefaultValueHandling.Ignore)]
        public float LegacyInputMoveX { get; set; }
        [JsonProperty("inputMoveY", DefaultValueHandling = DefaultValueHandling.Ignore)]
        public float LegacyInputMoveY { get; set; }
        [JsonProperty("inputWorldMoveX", DefaultValueHandling = DefaultValueHandling.Ignore)]
        public float LegacyInputWorldMoveX { get; set; }
        [JsonProperty("inputWorldMoveY", DefaultValueHandling = DefaultValueHandling.Ignore)]
        public float LegacyInputWorldMoveY { get; set; }
        [JsonProperty("animationTransitionSeconds", DefaultValueHandling = DefaultValueHandling.Ignore)]
        public float LegacyAnimationTransitionSeconds { get; set; }
        [JsonProperty("animationHoldSeconds", DefaultValueHandling = DefaultValueHandling.Ignore)]
        public float LegacyAnimationHoldSeconds { get; set; }
        [JsonProperty("jumpImpulseY", DefaultValueHandling = DefaultValueHandling.Ignore)]
        public float LegacyJumpImpulseY { get; set; }
        [JsonProperty("actionMovementControlScale", DefaultValueHandling = DefaultValueHandling.Ignore)]
        public float LegacyActionMovementControlScale { get; set; }
        [JsonProperty("actionRotationControlScale", DefaultValueHandling = DefaultValueHandling.Ignore)]
        public float LegacyActionRotationControlScale { get; set; }
        [JsonProperty("animationBlendX", DefaultValueHandling = DefaultValueHandling.Ignore)]
        public float LegacyAnimationBlendX { get; set; }
        [JsonProperty("animationBlendY", DefaultValueHandling = DefaultValueHandling.Ignore)]
        public float LegacyAnimationBlendY { get; set; }
        [JsonProperty("animationSpeed01", DefaultValueHandling = DefaultValueHandling.Ignore)]
        public float LegacyAnimationSpeed01 { get; set; }
        [JsonProperty("animationActionTransitionSeconds", DefaultValueHandling = DefaultValueHandling.Ignore)]
        public float LegacyAnimationActionTransitionSeconds { get; set; }
        [JsonProperty("animationActionHoldSeconds", DefaultValueHandling = DefaultValueHandling.Ignore)]
        public float LegacyAnimationActionHoldSeconds { get; set; }
        [JsonProperty("verticalVelocity", DefaultValueHandling = DefaultValueHandling.Ignore)]
        public float LegacyVerticalVelocity { get; set; }
        [JsonProperty("horizontalVelocityX", DefaultValueHandling = DefaultValueHandling.Ignore)]
        public float LegacyHorizontalVelocityX { get; set; }
        [JsonProperty("horizontalVelocityY", DefaultValueHandling = DefaultValueHandling.Ignore)]
        public float LegacyHorizontalVelocityY { get; set; }
        [JsonProperty("desiredVelocityX", DefaultValueHandling = DefaultValueHandling.Ignore)]
        public float LegacyDesiredVelocityX { get; set; }
        [JsonProperty("desiredVelocityY", DefaultValueHandling = DefaultValueHandling.Ignore)]
        public float LegacyDesiredVelocityY { get; set; }
        [JsonProperty("lastMoveInputX", DefaultValueHandling = DefaultValueHandling.Ignore)]
        public float LegacyLastMoveInputX { get; set; }
        [JsonProperty("lastMoveInputY", DefaultValueHandling = DefaultValueHandling.Ignore)]
        public float LegacyLastMoveInputY { get; set; }
        [JsonProperty("jumpBufferSecondsRemaining", DefaultValueHandling = DefaultValueHandling.Ignore)]
        public float LegacyJumpBufferSecondsRemaining { get; set; }
        [JsonProperty("coyoteTimeSecondsRemaining", DefaultValueHandling = DefaultValueHandling.Ignore)]
        public float LegacyCoyoteTimeSecondsRemaining { get; set; }
        [JsonProperty("movementControlScale", DefaultValueHandling = DefaultValueHandling.Ignore)]
        public float LegacyMovementControlScale { get; set; }
        [JsonProperty("rotationControlScale", DefaultValueHandling = DefaultValueHandling.Ignore)]
        public float LegacyRotationControlScale { get; set; }
        [JsonIgnore]
        public int LegacySurfaceMaterialKind { get; set; }
        [JsonIgnore]
        public string LegacySurfaceMaterialStableId { get; set; }
        [JsonProperty("surfaceFriction", DefaultValueHandling = DefaultValueHandling.Ignore)]
        public float LegacySurfaceFriction { get; set; }
        [JsonProperty("surfaceElasticity", DefaultValueHandling = DefaultValueHandling.Ignore)]
        public float LegacySurfaceElasticity { get; set; }
        [JsonProperty("surfaceFixedBounceHeight", DefaultValueHandling = DefaultValueHandling.Ignore)]
        public float LegacySurfaceFixedBounceHeight { get; set; }
        [JsonProperty("surfaceBounceSpeedCapY", DefaultValueHandling = DefaultValueHandling.Ignore)]
        public float LegacySurfaceBounceSpeedCapY { get; set; }
        [JsonProperty("physicsCapsuleRadiusMeters", DefaultValueHandling = DefaultValueHandling.Ignore)]
        public float LegacyPhysicsCapsuleRadiusMeters { get; set; }
        [JsonProperty("physicsCapsuleHeightMeters", DefaultValueHandling = DefaultValueHandling.Ignore)]
        public float LegacyPhysicsCapsuleHeightMeters { get; set; }
        [JsonProperty("physicsCapsuleCenterY", DefaultValueHandling = DefaultValueHandling.Ignore)]
        public float LegacyPhysicsCapsuleCenterY { get; set; }
        [JsonProperty("physicsMassKg", DefaultValueHandling = DefaultValueHandling.Ignore)]
        public float LegacyPhysicsMassKg { get; set; }
        [JsonProperty("physicsMassScale", DefaultValueHandling = DefaultValueHandling.Ignore)]
        public float LegacyPhysicsMassScale { get; set; }
        [JsonProperty("physicsRadiusScale", DefaultValueHandling = DefaultValueHandling.Ignore)]
        public float LegacyPhysicsRadiusScale { get; set; }
        [JsonProperty("physicsPushScale", DefaultValueHandling = DefaultValueHandling.Ignore)]
        public float LegacyPhysicsPushScale { get; set; }
        [JsonProperty("physicsContactVelocityX", DefaultValueHandling = DefaultValueHandling.Ignore)]
        public float LegacyPhysicsContactVelocityX { get; set; }
        [JsonProperty("physicsContactVelocityY", DefaultValueHandling = DefaultValueHandling.Ignore)]
        public float LegacyPhysicsContactVelocityY { get; set; }
        [JsonProperty("physicsContactDisplacementX", DefaultValueHandling = DefaultValueHandling.Ignore)]
        public float LegacyPhysicsContactDisplacementX { get; set; }
        [JsonProperty("physicsContactDisplacementY", DefaultValueHandling = DefaultValueHandling.Ignore)]
        public float LegacyPhysicsContactDisplacementY { get; set; }

        public void NormalizeForSchema(int schemaVersion)
        {
            if (schemaVersion < CharacterTestNetworkSnapshotParticipant.PositionRecallSchemaVersionValue) PositionRecall = default;
            else if (!PositionRecall.IsValidSnapshot()) throw new JsonSerializationException("Invalid position recall state.");
            if (schemaVersion < CharacterTestNetworkSnapshotParticipant.GroundPointTractionSchemaVersionValue)
                GroundPointTraction = default;
            else if (!GroundPointTraction.IsValidSnapshot())
                throw new JsonSerializationException("Invalid ground traction state.");
            if (schemaVersion < CharacterTestNetworkSnapshotParticipant.BehaviorControlSchemaVersionValue)
                InputBehaviorControl = default;
            HasExactBattleState = schemaVersion >=
                CharacterTestNetworkSnapshotParticipant.BattleStateSchemaVersionValue;
            if (schemaVersion < CharacterTestNetworkSnapshotParticipant.MovementSpeedScaleSchemaVersionValue
                || InputMovementSpeedScaleMilli <= 0)
            {
                InputMovementSpeedScaleMilli =
                    CharacterTestControlIntent.DefaultMovementSpeedScaleMilli;
            }
            if (schemaVersion < CharacterTestNetworkSnapshotParticipant.TurnInPlaceSchemaVersionValue)
            {
                InputTurnInPlace = false;
                InputTurnInPlaceYawMilliDegrees = 0;
            }
            if (schemaVersion < CharacterTestNetworkSnapshotParticipant.ControlIntentSourceSchemaVersionValue)
                InputControlIntentSourceId = 0;
            if (schemaVersion < CharacterTestNetworkSnapshotParticipant.SourceRevisionSchemaVersionValue)
            {
                SourcePoseRevision = CharacterTestCharacterSourceRevisionComponent.InitialRevision;
                SourceFacingRevision = CharacterTestCharacterSourceRevisionComponent.InitialRevision;
            }
            if (schemaVersion < CharacterTestNetworkSnapshotParticipant.CombatEvadeStateSchemaVersionValue)
            {
                CombatEvadeFirstDirection = CharacterTestCombatEvadeDirection.Back;
                CombatEvadePendingRoll = false;
                CombatEvadePendingDirection = CharacterTestCombatEvadeDirection.None;
                CombatEvadeLastSpendFrame = long.MinValue;
                CombatEvadeRegenerationRemainder = 0;
                CombatEvadeStamina = 100;
            }
            if (schemaVersion == CharacterTestNetworkSnapshotParticipant.SchemaVersionValue)
                return;

            if (schemaVersion == CharacterTestNetworkSnapshotParticipant.LegacySchemaVersionValue)
            {
                XMillimeters = CharacterTestDeterministicUnits.MetersToMillimeters(LegacyX);
                YMillimeters = CharacterTestDeterministicUnits.MetersToMillimeters(LegacyY);
                ZMillimeters = CharacterTestDeterministicUnits.MetersToMillimeters(LegacyZ);
                YawMilliDegrees = CharacterTestDeterministicUnits.DegreesToMilliDegrees(LegacyYawDegrees);
                InputMoveXMilli = CharacterTestDeterministicUnits.AxisToMilli(LegacyInputMoveX);
                InputMoveYMilli = CharacterTestDeterministicUnits.AxisToMilli(LegacyInputMoveY);
                InputWorldMoveXMilli = CharacterTestDeterministicUnits.AxisToMilli(LegacyInputWorldMoveX);
                InputWorldMoveYMilli = CharacterTestDeterministicUnits.AxisToMilli(LegacyInputWorldMoveY);
                AnimationTransitionFrames =
                    CharacterTestDeterministicUnits.SecondsToFrames(LegacyAnimationTransitionSeconds);
                AnimationHoldFrames =
                    CharacterTestDeterministicUnits.SecondsToFrames(LegacyAnimationHoldSeconds);
                JumpImpulseYMillimetersPerSecond =
                    CharacterTestDeterministicUnits.VelocityToMillimetersPerSecond(LegacyJumpImpulseY);
                ActionMovementControlScaleMilli =
                    CharacterTestDeterministicUnits.ScaleToMilli(LegacyActionMovementControlScale);
                ActionRotationControlScaleMilli =
                    CharacterTestDeterministicUnits.ScaleToMilli(LegacyActionRotationControlScale);
                VerticalVelocityMillimetersPerSecond =
                    CharacterTestDeterministicUnits.VelocityToMillimetersPerSecond(LegacyVerticalVelocity);
                HorizontalVelocityXMillimetersPerSecond =
                    CharacterTestDeterministicUnits.VelocityToMillimetersPerSecond(LegacyHorizontalVelocityX);
                HorizontalVelocityYMillimetersPerSecond =
                    CharacterTestDeterministicUnits.VelocityToMillimetersPerSecond(LegacyHorizontalVelocityY);
                DesiredVelocityXMillimetersPerSecond =
                    CharacterTestDeterministicUnits.VelocityToMillimetersPerSecond(LegacyDesiredVelocityX);
                DesiredVelocityYMillimetersPerSecond =
                    CharacterTestDeterministicUnits.VelocityToMillimetersPerSecond(LegacyDesiredVelocityY);
                LastMoveInputXMilli = CharacterTestDeterministicUnits.AxisToMilli(LegacyLastMoveInputX);
                LastMoveInputYMilli = CharacterTestDeterministicUnits.AxisToMilli(LegacyLastMoveInputY);
                JumpBufferFramesRemaining =
                    CharacterTestDeterministicUnits.SecondsToFrames(LegacyJumpBufferSecondsRemaining);
                CoyoteTimeFramesRemaining =
                    CharacterTestDeterministicUnits.SecondsToFrames(LegacyCoyoteTimeSecondsRemaining);
                MovementControlScaleMilli =
                    CharacterTestDeterministicUnits.ScaleToMilli(LegacyMovementControlScale);
                RotationControlScaleMilli =
                    CharacterTestDeterministicUnits.ScaleToMilli(LegacyRotationControlScale);
                if (LegacySurfaceMaterialKind != 0)
                    SurfaceMaterialKind = LegacySurfaceMaterialKind;
                if (!string.IsNullOrWhiteSpace(LegacySurfaceMaterialStableId))
                    SurfaceMaterialStableId = LegacySurfaceMaterialStableId;
                SurfaceFrictionMilli = CharacterTestDeterministicUnits.ScaleToMilli(LegacySurfaceFriction);
                SurfaceElasticityMilli = CharacterTestDeterministicUnits.ScaleToMilli(LegacySurfaceElasticity);
                SurfaceFixedBounceHeightMillimeters =
                    CharacterTestDeterministicUnits.MetersToMillimeters(LegacySurfaceFixedBounceHeight);
                SurfaceBounceSpeedCapYMillimetersPerSecond =
                    CharacterTestDeterministicUnits.VelocityToMillimetersPerSecond(LegacySurfaceBounceSpeedCapY);
                PhysicsCapsuleRadiusMillimeters =
                    CharacterTestDeterministicUnits.MetersToMillimeters(LegacyPhysicsCapsuleRadiusMeters);
                PhysicsCapsuleHeightMillimeters =
                    CharacterTestDeterministicUnits.MetersToMillimeters(LegacyPhysicsCapsuleHeightMeters);
                PhysicsCapsuleCenterYMillimeters =
                    CharacterTestDeterministicUnits.MetersToMillimeters(LegacyPhysicsCapsuleCenterY);
                PhysicsMassGrams =
                    CharacterTestDeterministicUnits.KilogramsToGrams(LegacyPhysicsMassKg);
                PhysicsMassScaleMilli =
                    CharacterTestDeterministicUnits.ScaleToMilli(LegacyPhysicsMassScale);
                PhysicsRadiusScaleMilli =
                    CharacterTestDeterministicUnits.ScaleToMilli(LegacyPhysicsRadiusScale);
                PhysicsPushScaleMilli =
                    CharacterTestDeterministicUnits.ScaleToMilli(LegacyPhysicsPushScale);
                PhysicsContactVelocityXMillimetersPerSecond =
                    CharacterTestDeterministicUnits.VelocityToMillimetersPerSecond(LegacyPhysicsContactVelocityX);
                PhysicsContactVelocityYMillimetersPerSecond =
                    CharacterTestDeterministicUnits.VelocityToMillimetersPerSecond(LegacyPhysicsContactVelocityY);
                PhysicsContactDisplacementXMillimeters =
                    CharacterTestDeterministicUnits.MetersToMillimeters(LegacyPhysicsContactDisplacementX);
                PhysicsContactDisplacementYMillimeters =
                    CharacterTestDeterministicUnits.MetersToMillimeters(LegacyPhysicsContactDisplacementY);
            }

            if (schemaVersion <= CharacterTestNetworkSnapshotParticipant.IntegerSchemaVersionValue)
            {
                if (AnimationTransitionFrames == 0 && AnimationTransitionMilliseconds > 0)
                    AnimationTransitionFrames = CharacterTestDeterministicUnits.SecondsToFrames(
                        CharacterTestDeterministicUnits.MillisecondsToSeconds(AnimationTransitionMilliseconds));
                if (AnimationHoldFrames == 0 && AnimationHoldMilliseconds > 0)
                    AnimationHoldFrames = CharacterTestDeterministicUnits.SecondsToFrames(
                        CharacterTestDeterministicUnits.MillisecondsToSeconds(AnimationHoldMilliseconds));
                if (JumpBufferFramesRemaining == 0 && JumpBufferMillisecondsRemaining > 0)
                    JumpBufferFramesRemaining = CharacterTestDeterministicUnits.SecondsToFrames(
                        CharacterTestDeterministicUnits.MillisecondsToSeconds(JumpBufferMillisecondsRemaining));
                if (CoyoteTimeFramesRemaining == 0 && CoyoteTimeMillisecondsRemaining > 0)
                    CoyoteTimeFramesRemaining = CharacterTestDeterministicUnits.SecondsToFrames(
                        CharacterTestDeterministicUnits.MillisecondsToSeconds(CoyoteTimeMillisecondsRemaining));
            }

            if (schemaVersion < CharacterTestNetworkSnapshotParticipant.LivingNaturalAttributesSchemaVersionValue)
                ResetLivingNaturalAttributesForLegacySchema();

            if (schemaVersion < CharacterTestNetworkSnapshotParticipant.RuntimeAiBehaviorSelectionSchemaVersionValue)
                ResetRuntimeAiBehaviorSelectionForLegacySchema();

            if (schemaVersion < CharacterTestNetworkSnapshotParticipant.ActionRunnerSchemaVersionValue)
                ResetActionRunnerForLegacySchema();

            if (schemaVersion < CharacterTestNetworkSnapshotParticipant.BattleStateSchemaVersionValue)
                ResetBattleStateForLegacySchema();
        }

        public CharacterTestCharacterTransformComponent CreateTransformComponent()
        {
            return CharacterTestCharacterTransformComponent.FromDeterministic(
                XMillimeters,
                YMillimeters,
                ZMillimeters,
                YawMilliDegrees);
        }

        public CharacterTestCharacterInputComponent CreateInputComponent()
        {
            return CharacterTestCharacterInputComponent.FromDeterministic(
                InputMoveXMilli,
                InputMoveYMilli,
                InputWorldMoveXMilli,
                InputWorldMoveYMilli,
                InputHasWorldMove,
                JumpPressed,
                JumpHeld,
                JumpReleased,
                AttackPrimaryPressed,
                AttackSecondaryPressed,
                DebugSecondaryPressed,
                DebugPrimaryPressed,
                DodgePressed,
                InputMovementSpeedScaleMilli,
                InputTurnInPlace,
                InputTurnInPlaceYawMilliDegrees,
                InputControlIntentSourceId, InputBehaviorControl);
        }

        internal CharacterTestCombatEvadeStateSnapshot CreateCombatEvadeStateSnapshot()
        {
            return new CharacterTestCombatEvadeStateSnapshot(
                CombatEvadeFirstDirection,
                CombatEvadePendingRoll,
                CombatEvadePendingDirection,
                CombatEvadeLastSpendFrame,
                CombatEvadeRegenerationRemainder);
        }

        public CharacterTestRuntimeAiBehaviorSelection CreateRuntimeAiBehaviorSelection()
        {
            return new CharacterTestRuntimeAiBehaviorSelection(
                RuntimeAiBehaviorLoadoutId,
                RuntimeAiBehaviorVariantId,
                RuntimeAiBehaviorCatalogHash,
                RuntimeAiBehaviorProfileSlot);
        }

        internal CharacterTestBattleStateComponent CreateBattleStateComponent()
        {
            return new CharacterTestBattleStateComponent(
                (CharacterTestCharacterLifeState)BattleLifeState,
                (CharacterTestBattleStateDominantReason)BattleDominantReason,
                (CharacterTestBattleGateFlags)BattleGateFlags,
                BattleSinceFrame,
                BattleLastTransitionFrame,
                BattleSourceActionId,
                BattleSourceTraceId);
        }

        public CharacterActionExecutionState CreateActionExecutionState()
        {
            bool hasRunnerPayload = ActionRunnerNextInstanceId > 0L
                || ActionRunnerActiveInstanceId > 0L
                || ActionRunnerActionId.IsValid
                || ActionRunnerState != CharacterActionInstanceState.None;
            CharacterActionId actionId = !ActionRunnerActionId.IsValid
                ? ActiveActionId
                : ActionRunnerActionId;
            CharacterActionInstanceState state = ActionRunnerState != CharacterActionInstanceState.None ? ActionRunnerState : ((int)ActionState > (int)CharacterActionInstanceState.Interrupted ? CharacterActionInstanceState.None : (CharacterActionInstanceState)ActionState);
            CharacterActionPhaseKind phase = ActionRunnerPhase != CharacterActionPhaseKind.None ? ActionRunnerPhase : ((int)ActionPhase > (int)CharacterActionPhaseKind.Custom ? CharacterActionPhaseKind.None : (CharacterActionPhaseKind)ActionPhase);
            int localFrame = hasRunnerPayload ? ActionRunnerLocalFrame : ActionLocalFrame;
            long activeInstanceId = ActionRunnerActiveInstanceId;
            if (activeInstanceId <= 0L
                && actionId.IsValid
                && state != CharacterActionInstanceState.None)
            {
                activeInstanceId = 1L;
            }

            long nextInstanceId = ActionRunnerNextInstanceId > 0L
                ? ActionRunnerNextInstanceId
                : activeInstanceId > 0L
                    ? Math.Max(2L, activeInstanceId + 1L)
                    : 1L;
            return new CharacterActionExecutionState(
                nextInstanceId,
                activeInstanceId,
                ActionRunnerPlanId,
                actionId,
                state,
                localFrame,
                phase,
                ActionRunnerDurationFrames,
                ActionRunnerTraceId,
                ActionRunnerFinishReason,
                ActionRunnerLastRejectReason);
        }

        public CharacterTestCharacterPhysicsComponent CreatePhysicsComponent(
            CharacterTestCharacterPhysicsComponent fallback)
        {
            if (PhysicsCapsuleRadiusMillimeters <= 0
                || PhysicsCapsuleHeightMillimeters <= 0
                || PhysicsMassGrams <= 0)
            {
                return fallback;
            }

            return CharacterTestCharacterPhysicsComponent.FromDeterministic(
                PhysicsCapsuleRadiusMillimeters,
                PhysicsCapsuleHeightMillimeters,
                PhysicsCapsuleCenterYMillimeters,
                PhysicsMassGrams,
                PhysicsMassScaleMilli,
                PhysicsRadiusScaleMilli,
                PhysicsPushScaleMilli,
                PhysicsBlocksCharacters,
                PhysicsCanBePushed,
                PhysicsContactVelocityXMillimetersPerSecond,
                PhysicsContactVelocityYMillimetersPerSecond,
                PhysicsContactDisplacementXMillimeters,
                PhysicsContactDisplacementYMillimeters,
                PhysicsContactCount);
        }

        public CharacterTestColliderPostureComponent CreateColliderPostureComponent(
            CharacterTestCharacterPhysicsComponent fallbackPhysics)
        {
            if (ColliderHeightMillimeters <= 0 || ColliderRadiusMillimeters <= 0)
                return CharacterTestColliderPostureComponent.FromPhysics(fallbackPhysics, frame: 0L);

            if (!Enum.IsDefined(typeof(CharacterTestColliderPostureSource), ColliderSource))
                ColliderSource = (int)CharacterTestColliderPostureSource.Standing;

            CharacterActionCollisionProfileRestorePolicy restorePolicy;
            if (string.Equals(ColliderActionRestorePolicy, nameof(CharacterActionCollisionProfileRestorePolicy.None), StringComparison.Ordinal))
            {
                restorePolicy = CharacterActionCollisionProfileRestorePolicy.None;
            }
            else
            {
                restorePolicy = CharacterActionCollisionProfileRestorePolicy.OnActionEnd;
            }

            return new CharacterTestColliderPostureComponent(
                ColliderProfileId,
                ColliderHeightMillimeters,
                ColliderRadiusMillimeters,
                ColliderCenterYMillimeters,
                ColliderBlocksCharacters,
                ColliderCanBePushed,
                (CharacterTestColliderPostureSource)ColliderSource,
                ColliderPriority,
                ColliderSinceFrame,
                ColliderActionProfileId,
                restorePolicy,
                ColliderActionTransitionFrames,
                ColliderActionSourceActionId,
                ColliderActionStableEventId);
        }

        public CombatSurfaceMaterial CreateSurfaceMaterial()
        {
            return new CombatSurfaceMaterial(
                ResolveSurfaceMaterialKind(SurfaceMaterialKind, SurfaceMaterialStableId),
                ToFix64(CharacterTestDeterministicUnits.MilliToScale(SurfaceFrictionMilli)),
                ToFix64(CharacterTestDeterministicUnits.MilliToScale(SurfaceElasticityMilli)),
                ToFix64(CharacterTestDeterministicUnits.MillimetersToMeters(
                    SurfaceFixedBounceHeightMillimeters)));
        }

        internal CharacterTestLivingNaturalAttributesRuntimeState CreateLivingNaturalAttributesRuntimeState()
        {
            return CharacterTestLivingNaturalAttributesRuntimeState.FromSnapshot(
                (CharacterTestLivingNaturalAttributesLifecycleState)LivingNaturalAttributesLifecycleState,
                (CharacterTestGenesisLivingNaturalAttributesRuntimeOperation)LivingNaturalAttributesLastOperation,
                LivingNaturalAttributesBodyBoundaryPresent,
                LivingNaturalAttributesBodyScaleMillimeters,
                LivingNaturalAttributesMassGrams,
                LivingNaturalAttributesContactSurfaceCount,
                LivingNaturalAttributesNearFieldContactCount,
                LivingNaturalAttributesSelfCohesion,
                LivingNaturalAttributesCohesionLimit,
                LivingNaturalAttributesExternalPressure,
                LivingNaturalAttributesScatter,
                LivingNaturalAttributesRecoveryTicks,
                LivingNaturalAttributesRecoveryAmount,
                LivingNaturalAttributesLastInputFrame,
                LivingNaturalAttributesLastTransitionFrame);
        }

        public void ApplyActionExecutionState(CharacterActionExecutionState state)
        {
            ActionRunnerActiveInstanceId = state.ActiveActionInstanceId;
            ActionRunnerPlanId = state.PlanId;
            ActionRunnerNextInstanceId = state.NextInstanceId;
            ActionRunnerActionId = state.ActionId;
            ActionRunnerState = state.State;
            ActionRunnerPhase = state.CurrentPhase;
            ActionRunnerLocalFrame = state.LocalFrame;
            ActionRunnerDurationFrames = state.DurationFrames;
            ActionRunnerTraceId = state.TraceId;
            ActionRunnerFinishReason = state.FinishReason;
            ActionRunnerLastRejectReason = state.LastRejectReasonCode;
        }

        public static CharacterTestNetworkCharacterSnapshot FromEntity(
            CharacterTestCharacterEntity entity,
            CharacterTestCharacterActionRuntime actionRuntime = null,
            CharacterTestCombatEvadeStateSnapshot combatEvadeState = default,
            int combatEvadeStamina = 100)
        {
            CharacterTestRuntimeAiBehaviorSelection runtimeAiBehaviorSelection =
                entity.Control.RuntimeAiBehaviorSelection;
            CharacterTestColliderPostureComponent colliderPosture = entity.ColliderPosture;
            CharacterTestBattleStateComponent battleState = entity.BattleState;
            var snapshot = new CharacterTestNetworkCharacterSnapshot
            {
                GroundPointTraction = entity.GroundPointTraction,
                PositionRecall = entity.PositionRecall,
                EntityId = entity.Id.Value,
                CharacterId = entity.Identity.CharacterId,
                TeamId = entity.Team.TeamId,
                DefaultTeamId = entity.Team.DefaultTeamId,
                ControlOwner = entity.Control.Owner,
                RuntimeAiPlannerProfileSlot = entity.Control.RuntimeAiPlannerProfileSlot,
                RuntimeAiBehaviorLoadoutId = runtimeAiBehaviorSelection.LoadoutId,
                RuntimeAiBehaviorVariantId = runtimeAiBehaviorSelection.VariantId,
                RuntimeAiBehaviorCatalogHash = runtimeAiBehaviorSelection.CatalogHash,
                RuntimeAiBehaviorProfileSlot = runtimeAiBehaviorSelection.ProfileSlot,
                XMillimeters = entity.Transform.XMillimeters,
                YMillimeters = entity.Transform.YMillimeters,
                ZMillimeters = entity.Transform.ZMillimeters,
                YawMilliDegrees = entity.Transform.YawMilliDegrees,
                SourcePoseRevision = entity.SourcePoseRevision,
                SourceFacingRevision = entity.SourceFacingRevision,
                InputMoveXMilli = entity.Input.MoveXMilli,
                InputMoveYMilli = entity.Input.MoveYMilli,
                InputHasWorldMove = entity.Input.HasWorldMove,
                InputWorldMoveXMilli = entity.Input.WorldMoveXMilli,
                InputWorldMoveYMilli = entity.Input.WorldMoveYMilli,
                InputTurnInPlace = entity.Input.TurnInPlace,
                InputTurnInPlaceYawMilliDegrees = entity.Input.TurnInPlaceYawMilliDegrees,
                InputBehaviorControl = entity.Input.BehaviorControl,
                InputControlIntentSourceId = entity.Input.ControlIntentSourceId,
                InputMovementSpeedScaleMilli = entity.Input.MovementSpeedScaleMilli <= 0
                    ? CharacterTestControlIntent.DefaultMovementSpeedScaleMilli
                    : entity.Input.MovementSpeedScaleMilli,
                JumpPressed = entity.Input.JumpPressed,
                JumpHeld = entity.Input.JumpHeld,
                JumpReleased = entity.Input.JumpReleased,
                AttackPrimaryPressed = entity.Input.AttackPrimaryPressed,
                AttackSecondaryPressed = entity.Input.AttackSecondaryPressed,
                DodgePressed = entity.Input.DodgePressed,
                CombatEvadeFirstDirection = combatEvadeState.FirstDirection == CharacterTestCombatEvadeDirection.None
                    ? CharacterTestCombatEvadeDirection.Back
                    : combatEvadeState.FirstDirection,
                CombatEvadePendingRoll = combatEvadeState.PendingRoll,
                CombatEvadePendingDirection = combatEvadeState.PendingDirection,
                CombatEvadeLastSpendFrame = combatEvadeState.LastSpendFrame,
                CombatEvadeRegenerationRemainder = combatEvadeState.RegenerationRemainder,
                CombatEvadeStamina = Math.Max(0, Math.Min(100, combatEvadeStamina)),
                DebugPrimaryPressed = entity.Input.DebugPrimaryPressed,
                DebugSecondaryPressed = entity.Input.DebugSecondaryPressed,
                MaxHealth = entity.CombatState.MaxHealth,
                Health = entity.CombatState.Health,
                HitStunFramesRemaining = entity.CombatState.HitStunFramesRemaining,
                LastDamage = entity.CombatState.LastDamage,
                LastHitFrame = entity.CombatState.LastHitFrame,
                LastHitSourceEntityId = entity.CombatState.LastHitSourceEntityId,
                LastHitTraceId = entity.CombatState.LastHitTraceId,
                BattleLifeState = (int)battleState.LifeState,
                BattleDominantReason = (int)battleState.DominantReason,
                BattleGateFlags = (int)battleState.GateFlags,
                BattleSinceFrame = battleState.SinceFrame,
                BattleLastTransitionFrame = battleState.LastTransitionFrame,
                BattleSourceActionId = battleState.SourceActionId,
                BattleSourceTraceId = battleState.SourceTraceId,
                HasExactBattleState = true,
                ActiveActionId = entity.Action.ActiveActionId,
                ActionState = entity.Action.State,
                ActionPhase = entity.Action.Phase,
                ActionLocalFrame = entity.Action.LocalFrame,
                LastRejectReasonCode = entity.Action.LastRejectReasonCode,
                LastRejectDetailId = entity.Action.LastRejectDetailId,
                LastAnimationBindingId = entity.Action.LastAnimationBindingId,
                AnimationBindingId = entity.Animation.BindingId,
                AnimationAuthorityBindingId = entity.Animation.AuthorityBindingId,
                AnimationRequestBindingId = entity.Animation.RequestBindingId,
                AnimationRequest = CharacterTestAnimationRequestSnapshot.Capture(entity.ComponentWorld.GetAnimationRequest(entity.Id)),
                LastAnimationActionKey = entity.Action.LastAnimationActionKey,
                AnimationActionKeyThisFrame = entity.Action.AnimationActionKeyThisFrame,
                AnimationTransitionFrames = entity.Action.AnimationTransitionFrames,
                AnimationHoldFrames = entity.Action.AnimationHoldFrames,
                AnimationPlaybackSpeedMilli = entity.Action.AnimationPlaybackSpeedMilli,
                JumpImpulseYMillimetersPerSecond = entity.Action.JumpImpulseYMillimetersPerSecond,
                ActionMovementMode = entity.Action.MovementMode,
                ActionMovementLocked = entity.Action.MovementLocked,
                ActionMovementControlScaleMilli = entity.Action.MovementControlScaleMilli,
                ActionRotationControlScaleMilli = entity.Action.RotationControlScaleMilli,
                VerticalVelocityMillimetersPerSecond = entity.VerticalVelocityMillimetersPerSecond,
                HorizontalVelocityXMillimetersPerSecond = entity.HorizontalVelocityXMillimetersPerSecond,
                HorizontalVelocityYMillimetersPerSecond = entity.HorizontalVelocityYMillimetersPerSecond,
                DesiredVelocityXMillimetersPerSecond = entity.DesiredVelocityXMillimetersPerSecond,
                DesiredVelocityYMillimetersPerSecond = entity.DesiredVelocityYMillimetersPerSecond,
                LastMoveInputXMilli = entity.LastMoveInputXMilli,
                LastMoveInputYMilli = entity.LastMoveInputYMilli,
                IsGrounded = entity.IsGrounded,
                JumpBufferFramesRemaining = entity.JumpBufferFramesRemaining,
                CoyoteTimeFramesRemaining = entity.CoyoteTimeFramesRemaining,
                MovementMode = entity.MovementMode,
                MovementLocked = entity.MovementLocked,
                MovementControlScaleMilli = entity.MovementControlScaleMilli,
                RotationControlScaleMilli = entity.RotationControlScaleMilli,
                MovementProfileId = entity.MovementProfileId,
                MovementProfileFallback = entity.MovementProfileFallback,
                PhysicsEnabled = entity.PhysicsEnabled,
                CollisionFlags = entity.CollisionFlags,
                SurfaceMaterialKind = (int)entity.SurfaceMaterialKind,
                SurfaceMaterialStableId = entity.SurfaceMaterialStableId,
                SurfaceFrictionMilli = entity.SurfaceFrictionMilli,
                SurfaceElasticityMilli =
                    CharacterTestDeterministicUnits.ScaleToMilli(entity.SurfaceElasticity),
                SurfaceFixedBounceHeightMillimeters =
                    CharacterTestDeterministicUnits.MetersToMillimeters(entity.SurfaceFixedBounceHeight),
                SurfaceBounceSpeedCapYMillimetersPerSecond =
                    entity.SurfaceBounceSpeedCapYMillimetersPerSecond,
                PhysicsCapsuleRadiusMillimeters = entity.Physics.CapsuleRadiusMillimeters,
                PhysicsCapsuleHeightMillimeters = entity.Physics.CapsuleHeightMillimeters,
                PhysicsCapsuleCenterYMillimeters = entity.Physics.CapsuleCenterYMillimeters,
                PhysicsMassGrams = entity.Physics.MassGrams,
                PhysicsMassScaleMilli = entity.Physics.MassScaleMilli,
                PhysicsRadiusScaleMilli = entity.Physics.RadiusScaleMilli,
                PhysicsPushScaleMilli = entity.Physics.PushScaleMilli,
                PhysicsBlocksCharacters = entity.Physics.BlocksCharacters,
                PhysicsCanBePushed = entity.Physics.CanBePushed,
                ColliderProfileId = colliderPosture.ProfileId,
                ColliderHeightMillimeters = colliderPosture.HeightMillimeters,
                ColliderRadiusMillimeters = colliderPosture.RadiusMillimeters,
                ColliderCenterYMillimeters = colliderPosture.CenterYMillimeters,
                ColliderBlocksCharacters = colliderPosture.BlocksCharacters,
                ColliderCanBePushed = colliderPosture.CanBePushed,
                ColliderSource = (int)colliderPosture.Source,
                ColliderPriority = colliderPosture.Priority,
                ColliderSinceFrame = colliderPosture.SinceFrame,
                ColliderActionProfileId = colliderPosture.ActionProfileId,
                ColliderActionRestorePolicy = colliderPosture.ActionRestorePolicy.ToString(),
                ColliderActionTransitionFrames = colliderPosture.ActionTransitionFrames,
                ColliderActionSourceActionId = colliderPosture.ActionSourceActionId,
                ColliderActionStableEventId = colliderPosture.ActionStableEventId,
                PhysicsContactVelocityXMillimetersPerSecond =
                    entity.Physics.ContactVelocityXMillimetersPerSecond,
                PhysicsContactVelocityYMillimetersPerSecond =
                    entity.Physics.ContactVelocityYMillimetersPerSecond,
                PhysicsContactDisplacementXMillimeters =
                    entity.Physics.ContactDisplacementXMillimeters,
                PhysicsContactDisplacementYMillimeters =
                    entity.Physics.ContactDisplacementYMillimeters,
                PhysicsContactCount = entity.Physics.ContactCount,
                LivingNaturalAttributesLifecycleState = (int)entity.LivingNaturalAttributes.LifecycleState,
                LivingNaturalAttributesLastOperation = (int)entity.LivingNaturalAttributes.LastOperation,
                LivingNaturalAttributesBodyBoundaryPresent =
                    entity.LivingNaturalAttributes.BodyBoundaryPresent,
                LivingNaturalAttributesBodyScaleMillimeters =
                    entity.LivingNaturalAttributes.BodyScaleMillimeters,
                LivingNaturalAttributesMassGrams = entity.LivingNaturalAttributes.MassGrams,
                LivingNaturalAttributesContactSurfaceCount =
                    entity.LivingNaturalAttributes.ContactSurfaceCount,
                LivingNaturalAttributesNearFieldContactCount =
                    entity.LivingNaturalAttributes.NearFieldContactCount,
                LivingNaturalAttributesSelfCohesion = entity.LivingNaturalAttributes.SelfCohesion,
                LivingNaturalAttributesCohesionLimit = entity.LivingNaturalAttributes.CohesionLimit,
                LivingNaturalAttributesExternalPressure = entity.LivingNaturalAttributes.ExternalPressure,
                LivingNaturalAttributesScatter = entity.LivingNaturalAttributes.Scatter,
                LivingNaturalAttributesRecoveryTicks = entity.LivingNaturalAttributes.RecoveryTicks,
                LivingNaturalAttributesRecoveryAmount = entity.LivingNaturalAttributes.RecoveryAmount,
                LivingNaturalAttributesLastInputFrame = entity.LivingNaturalAttributes.LastInputFrame,
                LivingNaturalAttributesLastTransitionFrame =
                    entity.LivingNaturalAttributes.LastTransitionFrame,
                AuthorityPose = entity.AuthorityPose != null
                    ? entity.AuthorityPose.CreateSnapshot()
                    : CharacterAuthorityPoseSnapshot.Empty()
            };

            if (actionRuntime != null
                && actionRuntime.TryCreateRunnerRestoreState(entity.Id, out CharacterActionExecutionState state))
            {
                snapshot.ApplyActionExecutionState(state);
            }
            else
            {
                snapshot.ResetActionRunnerForLegacySchema();
            }

            return snapshot;
        }

        private void ResetBattleStateForLegacySchema()
        {
            CharacterTestBattleStateComponent state = CharacterTestBattleStateComponent.Alive;
            BattleLifeState = (int)state.LifeState;
            BattleDominantReason = (int)state.DominantReason;
            BattleGateFlags = (int)state.GateFlags;
            BattleSinceFrame = state.SinceFrame;
            BattleLastTransitionFrame = state.LastTransitionFrame;
            BattleSourceActionId = state.SourceActionId;
            BattleSourceTraceId = state.SourceTraceId;
            HasExactBattleState = false;
        }

        private void ResetLivingNaturalAttributesForLegacySchema()
        {
            CharacterTestLivingNaturalAttributesRuntimeState state =
                CharacterTestLivingNaturalAttributesRuntimeState.CreateDefault();
            LivingNaturalAttributesLifecycleState = (int)state.LifecycleState;
            LivingNaturalAttributesLastOperation = (int)state.LastOperation;
            LivingNaturalAttributesBodyBoundaryPresent = state.BodyBoundaryPresent;
            LivingNaturalAttributesBodyScaleMillimeters = state.BodyScaleMillimeters;
            LivingNaturalAttributesMassGrams = state.MassGrams;
            LivingNaturalAttributesContactSurfaceCount = state.ContactSurfaceCount;
            LivingNaturalAttributesNearFieldContactCount = state.NearFieldContactCount;
            LivingNaturalAttributesSelfCohesion = state.SelfCohesion;
            LivingNaturalAttributesCohesionLimit = state.CohesionLimit;
            LivingNaturalAttributesExternalPressure = state.ExternalPressure;
            LivingNaturalAttributesScatter = state.Scatter;
            LivingNaturalAttributesRecoveryTicks = state.RecoveryTicks;
            LivingNaturalAttributesRecoveryAmount = state.RecoveryAmount;
            LivingNaturalAttributesLastInputFrame = state.LastInputFrame;
            LivingNaturalAttributesLastTransitionFrame = state.LastTransitionFrame;
        }

        private void ResetRuntimeAiBehaviorSelectionForLegacySchema()
        {
            RuntimeAiPlannerProfileSlot = 0;
            RuntimeAiBehaviorLoadoutId = 0;
            RuntimeAiBehaviorVariantId = 0;
            RuntimeAiBehaviorCatalogHash = 0L;
            RuntimeAiBehaviorProfileSlot = 0;
        }

        private void ResetActionRunnerForLegacySchema()
        {
            if (!ActiveActionId.IsValid
                || ActionState != CharacterTestActionStateId.Running)
            {
                ActionRunnerActiveInstanceId = 0L;
                ActionRunnerPlanId = 0L;
                ActionRunnerNextInstanceId = 1L;
                ActionRunnerActionId = CharacterActionId.None;
                ActionRunnerState = CharacterActionInstanceState.None;
                ActionRunnerPhase = CharacterActionPhaseKind.None;
                ActionRunnerLocalFrame = 0;
                ActionRunnerDurationFrames = 0;
                ActionRunnerTraceId = RuntimeTraceIdentity.None;
                ActionRunnerFinishReason = string.Empty;
                ActionRunnerLastRejectReason = CharacterActionDiagnosticCodeText.IsDefined((CharacterActionDiagnosticCode)LastRejectReasonCode)
                    ? (CharacterActionDiagnosticCode)LastRejectReasonCode : CharacterActionDiagnosticCode.None;
                return;
            }

            ActionRunnerActiveInstanceId = 1L;
            ActionRunnerPlanId = 0L;
            ActionRunnerNextInstanceId = 2L;
            ActionRunnerActionId = ActiveActionId;
            ActionRunnerState = (CharacterActionInstanceState)ActionState;
            ActionRunnerPhase = (CharacterActionPhaseKind)ActionPhase;
            ActionRunnerLocalFrame = ActionLocalFrame;
            ActionRunnerDurationFrames = 0;
            ActionRunnerTraceId = new RuntimeTraceIdentity(CharacterTestActionTraceKinds.Restore,
                EntityId, ActionRunnerActiveInstanceId, ActiveActionId.Value);
            ActionRunnerFinishReason = string.Empty;
            ActionRunnerLastRejectReason = CharacterActionDiagnosticCodeText.IsDefined((CharacterActionDiagnosticCode)LastRejectReasonCode)
                    ? (CharacterActionDiagnosticCode)LastRejectReasonCode : CharacterActionDiagnosticCode.None;
        }

        private static CombatSurfaceMaterialKind ResolveSurfaceMaterialKind(int kindValue, string stableId)
        {
            var kind = (CombatSurfaceMaterialKind)kindValue;
            switch (kind)
            {
                case CombatSurfaceMaterialKind.Default:
                case CombatSurfaceMaterialKind.HardSlick:
                case CombatSurfaceMaterialKind.Rough:
                case CombatSurfaceMaterialKind.Elastic:
                case CombatSurfaceMaterialKind.BouncePad:
                    return kind;
            }

            switch ((stableId ?? string.Empty).Trim().ToLowerInvariant())
            {
                case "hard_slick":
                    return CombatSurfaceMaterialKind.HardSlick;
                case "rough":
                    return CombatSurfaceMaterialKind.Rough;
                case "elastic":
                    return CombatSurfaceMaterialKind.Elastic;
                case "bounce_pad":
                    return CombatSurfaceMaterialKind.BouncePad;
                default:
                    return CombatSurfaceMaterialKind.Default;
            }
        }

        private static Fix64 ToFix64(float value)
        {
            return Fix64.FromRaw((long)Math.Round(value * Fix64.Scale));
        }
    }

    internal static class NetworkAbilityRuntimeSnapshotHashFields
    {
        public const int SchemaVersion = 1;
        public const int ModuleId = 2;
        public const int DefinitionHash = 3;
        public const int Cooldowns = 4;
    }

    internal static class NetworkSnapshotHashFields
    {
        public const int NumericParticipantId = 1003;
        public const int ParticipantId = 1;
        public const int SchemaVersion = 2;
        public const int Frame = 3;
    }

    internal static class NetworkAbilityCooldownEntryHashFields
    {
        public const int CasterEntityId = 1;
        public const int AbilityId = 2;
        public const int EndFrame = 3;
    }

    internal static class NetworkCommandStateHashFields
    {
        public const int TargetLockEnabled = 1;
        public const int LockedTargetEntityId = 2;
        public const int LockedCombatTargetPresent = 3;
        public const int LockedCombatTarget = 4;
        public const int RuntimeAiBehaviorSelectionUnlocked = 5;
    }
}
