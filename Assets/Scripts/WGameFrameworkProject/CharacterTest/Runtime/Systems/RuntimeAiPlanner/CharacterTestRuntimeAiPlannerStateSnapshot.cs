using System;
using MxFramework.CharacterAction;
using MxFramework.Runtime;
using Newtonsoft.Json;

namespace MxFramework.Game.CharacterTest
{
    /// <summary>
    /// Captures Runtime AI Planner evaluator state for CharacterTest network snapshot, rollback, and
    /// Runtime AI Lab seek replay.
    /// </summary>
    /// <remarks>LowFreqAlloc: capture/apply occurs on snapshot and rollback paths, not during planner Tick.</remarks>
    public sealed class CharacterTestRuntimeAiPlannerStateSnapshot
    {
        public const int LegacyActionBoundDamageZoneLockSchemaVersion = 13;
        public const int CurrentSchemaVersion = 16;

        public int SchemaVersion { get; set; }
        public CharacterTestRuntimeAiPlannerSlotStateSnapshot[] Slots { get; set; }
        public CharacterTestRuntimeAiLabLogicSlotStateSnapshot[] BehaviorLogicSlots { get; set; }
        public CharacterTestDamageZoneLockStateSnapshot[] DamageZoneLocks { get; set; }
        public CharacterTestEngagementReservationStateSnapshot[] EngagementReservations { get; set; }
        internal bool HasActiveBehaviorBindings()
        {
            for (int i = 0; BehaviorLogicSlots != null && i < BehaviorLogicSlots.Length; i++)
            {
                var agents = BehaviorLogicSlots[i]?.Agents;
                for (int j = 0; agents != null && j < agents.Length; j++)
                    if (agents[j] != null && (agents[j].ActiveBindingIndex >= 0 || agents[j].PendingBindingIndex >= 0 || agents[j].BindingReleasePending)) return true;
            }
            return false;
        }

        /// <summary>
        /// Creates an empty planner state snapshot for legacy snapshots or scenes without planner evaluators.
        /// </summary>
        public static CharacterTestRuntimeAiPlannerStateSnapshot Empty()
        {
            return new CharacterTestRuntimeAiPlannerStateSnapshot
            {
                SchemaVersion = CurrentSchemaVersion,
                Slots = Array.Empty<CharacterTestRuntimeAiPlannerSlotStateSnapshot>(),
                BehaviorLogicSlots = Array.Empty<CharacterTestRuntimeAiLabLogicSlotStateSnapshot>(),
                DamageZoneLocks = Array.Empty<CharacterTestDamageZoneLockStateSnapshot>(),
                EngagementReservations = Array.Empty<CharacterTestEngagementReservationStateSnapshot>()
            };
        }

        /// <summary>
        /// Creates a deep copy suitable for rollback checkpoint storage.
        /// </summary>
        public CharacterTestRuntimeAiPlannerStateSnapshot CloneForRollback()
        {
            return new CharacterTestRuntimeAiPlannerStateSnapshot
            {
                SchemaVersion = SchemaVersion,
                Slots = CharacterTestRuntimeAiPlannerSlotStateSnapshot.CloneArray(Slots),
                BehaviorLogicSlots = CharacterTestRuntimeAiLabLogicSlotStateSnapshot.CloneArray(BehaviorLogicSlots),
                DamageZoneLocks = CharacterTestDamageZoneLockStateSnapshot.CloneArray(DamageZoneLocks),
                EngagementReservations =
                    CharacterTestEngagementReservationStateSnapshot.CloneArray(EngagementReservations)
            };
        }

        /// <summary>
        /// Adds planner continuous state to the CharacterTest deterministic state hash.
        /// </summary>
        public static void AddStateHash(
            RuntimeHashAccumulator accumulator,
            CharacterTestRuntimeAiPlannerStateSnapshot snapshot)
        {
            AddStateHashInternal(accumulator, snapshot, 1, 0, false, false);
        }

        public static void AddStateHashForOwnership(
            RuntimeHashAccumulator accumulator,
            CharacterTestRuntimeAiPlannerStateSnapshot snapshot,
            int ownedEntityId,
            bool includeOwnedLocks)
        {
            AddStateHashInternal(accumulator, snapshot, 1, ownedEntityId, true, includeOwnedLocks);
        }

        private static void AddStateHashInternal(
            RuntimeHashAccumulator accumulator,
            CharacterTestRuntimeAiPlannerStateSnapshot snapshot,
            int key,
            int ownedEntityId,
            bool filterLocks,
            bool includeOwnedLocks)
        {
            snapshot ??= Empty();
            accumulator.BeginObject(key, RuntimeAiPlannerStateHashFields.SchemaId,
                RuntimeAiPlannerStateHashFields.SchemaVersion, snapshot != null);
            accumulator.AddInt(RuntimeAiPlannerStateHashFields.SnapshotSchemaVersion, snapshot.SchemaVersion);

            CharacterTestRuntimeAiPlannerSlotStateSnapshot[] slots =
                snapshot.Slots ?? Array.Empty<CharacterTestRuntimeAiPlannerSlotStateSnapshot>();
            AddSlotArray(accumulator, RuntimeAiPlannerStateHashFields.Slots, slots);

            CharacterTestRuntimeAiLabLogicSlotStateSnapshot[] logicSlots =
                snapshot.BehaviorLogicSlots ?? Array.Empty<CharacterTestRuntimeAiLabLogicSlotStateSnapshot>();
            AddLogicSlotArray(accumulator, RuntimeAiPlannerStateHashFields.BehaviorLogicSlots, logicSlots);

            CharacterTestDamageZoneLockStateSnapshot[] locks =
                snapshot.DamageZoneLocks ?? Array.Empty<CharacterTestDamageZoneLockStateSnapshot>();
            int includedCount = 0;
            for (int i = 0; i < locks.Length; i++)
            {
                CharacterTestDamageZoneLockStateSnapshot value = locks[i];
                bool isOwned = value != null && value.SourceEntityId == ownedEntityId;
                if (!filterLocks || isOwned == includeOwnedLocks)
                    includedCount++;
            }
            accumulator.BeginArray(RuntimeAiPlannerStateHashFields.DamageZoneLocks, includedCount);
            int previousSourceEntityId = 0;
            for (int lockIndex = 0; lockIndex < includedCount; lockIndex++)
            {
                CharacterTestDamageZoneLockStateSnapshot value = null;
                int nextSourceEntityId = int.MaxValue;
                for (int i = 0; i < locks.Length; i++)
                {
                    CharacterTestDamageZoneLockStateSnapshot candidate = locks[i];
                    bool isOwned = candidate != null && candidate.SourceEntityId == ownedEntityId;
                    if (candidate == null
                        || (filterLocks && isOwned != includeOwnedLocks)
                        || candidate.SourceEntityId <= previousSourceEntityId
                        || candidate.SourceEntityId >= nextSourceEntityId)
                    {
                        continue;
                    }
                    value = candidate;
                    nextSourceEntityId = candidate.SourceEntityId;
                }
                accumulator.BeginElement(lockIndex);
                if (value != null)
                    value.Contribute(accumulator, RuntimeAiPlannerStateHashFields.Element);
                else
                {
                    accumulator.BeginObject(RuntimeAiPlannerStateHashFields.Element,
                        RuntimeAiPlannerStateHashFields.DamageZoneLockSchemaId,
                        RuntimeAiPlannerStateHashFields.ElementSchemaVersion, false);
                    accumulator.EndObject();
                }
                accumulator.EndElement();
                previousSourceEntityId = nextSourceEntityId;
            }
            accumulator.EndArray();

            CharacterTestEngagementReservationStateSnapshot[] reservations =
                snapshot.EngagementReservations
                ?? Array.Empty<CharacterTestEngagementReservationStateSnapshot>();
            int includedReservationCount = 0;
            for (int i = 0; i < reservations.Length; i++)
            {
                CharacterTestEngagementReservationStateSnapshot value = reservations[i];
                bool isOwned = value != null && value.SourceEntityId == ownedEntityId;
                if (!filterLocks || isOwned == includeOwnedLocks)
                    includedReservationCount++;
            }
            accumulator.BeginArray(RuntimeAiPlannerStateHashFields.EngagementReservations, includedReservationCount);
            int reservationIndex = 0;
            for (int i = 0; i < reservations.Length; i++)
            {
                CharacterTestEngagementReservationStateSnapshot value = reservations[i];
                bool isOwned = value != null && value.SourceEntityId == ownedEntityId;
                if (filterLocks && isOwned != includeOwnedLocks)
                    continue;
                accumulator.BeginElement(reservationIndex);
                if (value != null)
                    value.Contribute(accumulator, RuntimeAiPlannerStateHashFields.Element);
                else
                {
                    accumulator.BeginObject(RuntimeAiPlannerStateHashFields.Element,
                        RuntimeAiPlannerStateHashFields.EngagementReservationSchemaId,
                        RuntimeAiPlannerStateHashFields.ElementSchemaVersion, false);
                    accumulator.EndObject();
                }
                accumulator.EndElement();
                reservationIndex++;
            }
            accumulator.EndArray();
            accumulator.EndObject();
        }

        private static void AddSlotArray(
            RuntimeHashAccumulator accumulator,
            int key,
            CharacterTestRuntimeAiPlannerSlotStateSnapshot[] values)
        {
            accumulator.BeginArray(key, values.Length);
            for (int i = 0; i < values.Length; i++)
            {
                accumulator.BeginElement(i);
                if (values[i] != null)
                    values[i].Contribute(accumulator, RuntimeAiPlannerStateHashFields.Element);
                else
                {
                    accumulator.BeginObject(RuntimeAiPlannerStateHashFields.Element,
                        RuntimeAiPlannerStateHashFields.SlotSchemaId,
                        RuntimeAiPlannerStateHashFields.ElementSchemaVersion, false);
                    accumulator.EndObject();
                }
                accumulator.EndElement();
            }
            accumulator.EndArray();
        }

        private static void AddLogicSlotArray(
            RuntimeHashAccumulator accumulator,
            int key,
            CharacterTestRuntimeAiLabLogicSlotStateSnapshot[] values)
        {
            accumulator.BeginArray(key, values.Length);
            for (int i = 0; i < values.Length; i++)
            {
                accumulator.BeginElement(i);
                if (values[i] != null)
                    values[i].Contribute(accumulator, RuntimeAiPlannerStateHashFields.Element);
                else
                {
                    accumulator.BeginObject(RuntimeAiPlannerStateHashFields.Element,
                        RuntimeAiPlannerStateHashFields.LogicSlotSchemaId,
                        RuntimeAiPlannerStateHashFields.ElementSchemaVersion, false);
                    accumulator.EndObject();
                }
                accumulator.EndElement();
            }
            accumulator.EndArray();
        }
    }

    /// <summary>Authoritative engagement Anchor reservation affecting future planner commands.</summary>
    /// <remarks>LowFreqAlloc: snapshot, rollback and replay paths only.</remarks>
    public sealed class CharacterTestEngagementReservationStateSnapshot
    {
        public int SourceEntityId { get; set; }
        public int TargetEntityId { get; set; }
        public int TargetCombatEntityId { get; set; }
        public int ResolvedActionRevision { get; set; }
        public int ZoneId { get; set; }
        public int AnchorId { get; set; }
        public int Capacity { get; set; }
        public int CapacitySlot { get; set; }
        public long AcquiredFrame { get; set; }
        public long UpdatedFrame { get; set; }
        public int ZoneBindingRevision { get; set; }
        public int ZoneRevision { get; set; }
        public int AnchorRevision { get; set; }
        public RuntimePreparedDigest AnchorContentHash { get; set; }
        public int PhaseId { get; set; }
        public int PhaseRevision { get; set; }
        public int CombatPhysicsRevision { get; set; }
        public int GraphRevision { get; set; }
        public long GraphContentHash { get; set; }
        public int ReleaseReason { get; set; }

        internal static void CopyFrom(CharacterTestEngagementReservation value, CharacterTestEngagementReservationStateSnapshot target)
        {
            target.SourceEntityId = value.SourceEntityId;
            target.TargetEntityId = value.TargetEntityId;
            target.TargetCombatEntityId = value.TargetCombatEntityId.Value;
            target.ResolvedActionRevision = value.ResolvedActionRevision;
            target.ZoneId = value.ZoneId;
            target.AnchorId = value.AnchorId;
            target.Capacity = value.Capacity;
            target.CapacitySlot = value.CapacitySlot;
            target.AcquiredFrame = value.AcquiredFrame.Value;
            target.UpdatedFrame = value.UpdatedFrame.Value;
            target.ZoneBindingRevision = value.ZoneBindingRevision;
            target.ZoneRevision = value.ZoneRevision;
            target.AnchorRevision = value.AnchorRevision;
            target.AnchorContentHash = value.AnchorContentHash;
            target.PhaseId = value.PhaseId;
            target.PhaseRevision = value.PhaseRevision;
            target.CombatPhysicsRevision = value.CombatPhysicsRevision;
            target.GraphRevision = value.GraphRevision;
            target.GraphContentHash = value.GraphContentHash;
            target.ReleaseReason = (int)value.ReleaseReason;
        }

        internal static CharacterTestEngagementReservationStateSnapshot From(
            CharacterTestEngagementReservation value)
        {
            return new CharacterTestEngagementReservationStateSnapshot
            {
                SourceEntityId = value.SourceEntityId,
                TargetEntityId = value.TargetEntityId,
                TargetCombatEntityId = value.TargetCombatEntityId.Value,
                ResolvedActionRevision = value.ResolvedActionRevision,
                ZoneId = value.ZoneId,
                AnchorId = value.AnchorId,
                Capacity = value.Capacity,
                CapacitySlot = value.CapacitySlot,
                AcquiredFrame = value.AcquiredFrame.Value,
                UpdatedFrame = value.UpdatedFrame.Value,
                ZoneBindingRevision = value.ZoneBindingRevision,
                ZoneRevision = value.ZoneRevision,
                AnchorRevision = value.AnchorRevision,
                AnchorContentHash = value.AnchorContentHash,
                PhaseId = value.PhaseId,
                PhaseRevision = value.PhaseRevision,
                CombatPhysicsRevision = value.CombatPhysicsRevision,
                GraphRevision = value.GraphRevision,
                GraphContentHash = value.GraphContentHash,
                ReleaseReason = (int)value.ReleaseReason
            };
        }

        internal bool TryCreate(out CharacterTestEngagementReservation value)
        {
            value = default;
            if (SourceEntityId <= 0
                || TargetEntityId <= 0
                || TargetCombatEntityId <= 0
                || ResolvedActionRevision <= 0
                || ZoneId <= 0
                || AnchorId <= 0
                || Capacity <= 0
                || CapacitySlot < 0
                || CapacitySlot >= Capacity
                || AcquiredFrame < 0
                || UpdatedFrame < AcquiredFrame
                || ZoneBindingRevision == 0
                || ZoneRevision == 0
                || AnchorRevision <= 0
                || AnchorContentHash.Representation == RuntimeDigestRepresentation.Null
                || PhaseRevision < 0
                || CombatPhysicsRevision <= 0
                || GraphRevision <= 0
                || !Enum.IsDefined(
                    typeof(CharacterTestEngagementReservationReleaseReason),
                    ReleaseReason)
                || ReleaseReason != (int)CharacterTestEngagementReservationReleaseReason.None)
            {
                return false;
            }
            value = new CharacterTestEngagementReservation(
                SourceEntityId,
                TargetEntityId,
                new MxFramework.Combat.Core.CombatEntityId(TargetCombatEntityId),
                ResolvedActionRevision,
                ZoneId,
                AnchorId,
                Capacity,
                CapacitySlot,
                new RuntimeFrame(AcquiredFrame),
                new RuntimeFrame(UpdatedFrame),
                ZoneBindingRevision,
                ZoneRevision,
                AnchorRevision,
                AnchorContentHash,
                PhaseId,
                PhaseRevision,
                CombatPhysicsRevision,
                GraphRevision,
                GraphContentHash);
            return true;
        }

        public static CharacterTestEngagementReservationStateSnapshot[] CloneArray(
            CharacterTestEngagementReservationStateSnapshot[] source)
        {
            if (source == null || source.Length == 0)
                return Array.Empty<CharacterTestEngagementReservationStateSnapshot>();
            var result = new CharacterTestEngagementReservationStateSnapshot[source.Length];
            for (int i = 0; i < source.Length; i++)
            {
                CharacterTestEngagementReservationStateSnapshot value = source[i];
                if (value == null) continue;
                result[i] = FromSnapshot(value);
            }
            return result;
        }

        private static CharacterTestEngagementReservationStateSnapshot FromSnapshot(
            CharacterTestEngagementReservationStateSnapshot value)
        {
            return new CharacterTestEngagementReservationStateSnapshot
            {
                SourceEntityId = value.SourceEntityId,
                TargetEntityId = value.TargetEntityId,
                TargetCombatEntityId = value.TargetCombatEntityId,
                ResolvedActionRevision = value.ResolvedActionRevision,
                ZoneId = value.ZoneId,
                AnchorId = value.AnchorId,
                Capacity = value.Capacity,
                CapacitySlot = value.CapacitySlot,
                AcquiredFrame = value.AcquiredFrame,
                UpdatedFrame = value.UpdatedFrame,
                ZoneBindingRevision = value.ZoneBindingRevision,
                ZoneRevision = value.ZoneRevision,
                AnchorRevision = value.AnchorRevision,
                AnchorContentHash = value.AnchorContentHash,
                PhaseId = value.PhaseId,
                PhaseRevision = value.PhaseRevision,
                CombatPhysicsRevision = value.CombatPhysicsRevision,
                GraphRevision = value.GraphRevision,
                GraphContentHash = value.GraphContentHash,
                ReleaseReason = value.ReleaseReason
            };
        }

        public void Contribute(RuntimeHashAccumulator accumulator, int key)
        {
            accumulator.BeginObject(key, RuntimeAiPlannerStateHashFields.EngagementReservationSchemaId,
                RuntimeAiPlannerStateHashFields.ElementSchemaVersion);
            accumulator.AddInt(EngagementReservationHashFields.SourceEntityId, SourceEntityId);
            accumulator.AddInt(EngagementReservationHashFields.TargetEntityId, TargetEntityId);
            accumulator.AddInt(EngagementReservationHashFields.TargetCombatEntityId, TargetCombatEntityId);
            accumulator.AddInt(EngagementReservationHashFields.ResolvedActionRevision, ResolvedActionRevision);
            accumulator.AddInt(EngagementReservationHashFields.ZoneId, ZoneId);
            accumulator.AddInt(EngagementReservationHashFields.AnchorId, AnchorId);
            accumulator.AddInt(EngagementReservationHashFields.Capacity, Capacity);
            accumulator.AddInt(EngagementReservationHashFields.CapacitySlot, CapacitySlot);
            accumulator.AddLong(EngagementReservationHashFields.AcquiredFrame, AcquiredFrame);
            accumulator.AddLong(EngagementReservationHashFields.UpdatedFrame, UpdatedFrame);
            accumulator.AddInt(EngagementReservationHashFields.ZoneBindingRevision, ZoneBindingRevision);
            accumulator.AddInt(EngagementReservationHashFields.ZoneRevision, ZoneRevision);
            accumulator.AddInt(EngagementReservationHashFields.AnchorRevision, AnchorRevision);
            AnchorContentHash.Contribute(accumulator, EngagementReservationHashFields.AnchorContentHash);
            accumulator.AddInt(EngagementReservationHashFields.PhaseId, PhaseId);
            // CombatPhysicsRevision is a process-local freshness witness. Authority restore
            // may legitimately rebuild the same semantic world under a newer local revision.
            accumulator.AddInt(EngagementReservationHashFields.PhaseRevision, PhaseRevision);
            accumulator.AddInt(EngagementReservationHashFields.GraphRevision, GraphRevision);
            accumulator.AddLong(EngagementReservationHashFields.GraphContentHash, GraphContentHash);
            accumulator.AddInt(EngagementReservationHashFields.ReleaseReason, ReleaseReason);
            accumulator.EndObject();
        }
    }

    /// <summary>Authoritative DamageZone lock affecting the next planner attack decision.</summary>
    /// <remarks>LowFreqAlloc: snapshot, rollback, and replay paths only.</remarks>
    public sealed class CharacterTestDamageZoneLockStateSnapshot
    {
        public int SourceEntityId { get; set; }
        public int TargetGameplayEntityIndex { get; set; }
        public int TargetGameplayEntityGeneration { get; set; }
        public int TargetCombatEntityId { get; set; }
        public int ResolvedActionRevision { get; set; }
        public int ZoneId { get; set; }
        public int HitZoneId { get; set; }
        public int LogicalHurtboxId { get; set; }
        public int OpportunityLevel { get; set; }
        public int EarliestHitTick { get; set; }
        public int BindingRevision { get; set; }
        public int HurtboxRevision { get; set; }
        public int WorldRevision { get; set; }
        public int ZoneRevision { get; set; }
        public long BodyPartStateRevision { get; set; }
        public long SelectedFrame { get; set; }
        public int SwitchReason { get; set; }

        public static CharacterTestDamageZoneLockStateSnapshot[] CloneArray(
            CharacterTestDamageZoneLockStateSnapshot[] source)
        {
            if (source == null || source.Length == 0)
                return Array.Empty<CharacterTestDamageZoneLockStateSnapshot>();
            var result = new CharacterTestDamageZoneLockStateSnapshot[source.Length];
            for (int i = 0; i < source.Length; i++)
            {
                CharacterTestDamageZoneLockStateSnapshot value = source[i];
                if (value == null) continue;
                result[i] = new CharacterTestDamageZoneLockStateSnapshot
                {
                    SourceEntityId = value.SourceEntityId,
                    TargetGameplayEntityIndex = value.TargetGameplayEntityIndex,
                    TargetGameplayEntityGeneration = value.TargetGameplayEntityGeneration,
                    TargetCombatEntityId = value.TargetCombatEntityId,
                    ResolvedActionRevision = value.ResolvedActionRevision,
                    ZoneId = value.ZoneId,
                    HitZoneId = value.HitZoneId,
                    LogicalHurtboxId = value.LogicalHurtboxId,
                    OpportunityLevel = value.OpportunityLevel,
                    EarliestHitTick = value.EarliestHitTick,
                    BindingRevision = value.BindingRevision,
                    HurtboxRevision = value.HurtboxRevision,
                    WorldRevision = value.WorldRevision,
                    ZoneRevision = value.ZoneRevision,
                    BodyPartStateRevision = value.BodyPartStateRevision,
                    SelectedFrame = value.SelectedFrame,
                    SwitchReason = value.SwitchReason
                };
            }
            NormalizeCanonicalArray(result);
            return result;
        }

        internal static CharacterTestDamageZoneLockStateSnapshot[] NormalizeCanonicalArray(
            CharacterTestDamageZoneLockStateSnapshot[] source)
        {
            source ??= Array.Empty<CharacterTestDamageZoneLockStateSnapshot>();
            for (int i = 0; i < source.Length; i++)
            {
                CharacterTestDamageZoneLockStateSnapshot value = source[i];
                if (value == null) continue;
            }
            Array.Sort(source, CompareCanonical);
            return source;
        }

        public void Contribute(RuntimeHashAccumulator accumulator, int key)
        {
            accumulator.BeginObject(key, RuntimeAiPlannerStateHashFields.DamageZoneLockSchemaId,
                RuntimeAiPlannerStateHashFields.ElementSchemaVersion);
            accumulator.AddInt(DamageZoneLockHashFields.SourceEntityId, SourceEntityId);
            accumulator.AddInt(DamageZoneLockHashFields.TargetGameplayEntityIndex, TargetGameplayEntityIndex);
            accumulator.AddInt(DamageZoneLockHashFields.TargetGameplayEntityGeneration, TargetGameplayEntityGeneration);
            accumulator.AddInt(DamageZoneLockHashFields.TargetCombatEntityId, TargetCombatEntityId);
            accumulator.AddInt(DamageZoneLockHashFields.ZoneId, ZoneId);
            accumulator.AddInt(DamageZoneLockHashFields.HitZoneId, HitZoneId);
            accumulator.AddInt(DamageZoneLockHashFields.LogicalHurtboxId, LogicalHurtboxId);
            // Action, opportunity, frame and geometry revisions are derived
            // evaluation evidence. Only the stable actor/subtarget identity is
            // authoritative across rollback branches.
            accumulator.EndObject();
        }

        private static int CompareCanonical(
            CharacterTestDamageZoneLockStateSnapshot left,
            CharacterTestDamageZoneLockStateSnapshot right)
        {
            if (ReferenceEquals(left, right)) return 0;
            if (left == null) return 1;
            if (right == null) return -1;
            return left.SourceEntityId.CompareTo(right.SourceEntityId);
        }
    }

    /// <summary>Continuous Runtime AI Lab behavior-card state for one planner profile slot.</summary>
    /// <remarks>LowFreqAlloc: snapshot, rollback, and Warm Replay paths only.</remarks>
    public sealed class CharacterTestRuntimeAiLabLogicSlotStateSnapshot
    {
        public int Slot { get; set; }
        public long AuthorityHash { get; set; }
        public CharacterTestRuntimeAiLabLogicAgentStateSnapshot[] Agents { get; set; }

        public static CharacterTestRuntimeAiLabLogicSlotStateSnapshot[] CloneArray(
            CharacterTestRuntimeAiLabLogicSlotStateSnapshot[] slots)
        {
            if (slots == null || slots.Length == 0)
                return Array.Empty<CharacterTestRuntimeAiLabLogicSlotStateSnapshot>();
            var result = new CharacterTestRuntimeAiLabLogicSlotStateSnapshot[slots.Length];
            for (int i = 0; i < slots.Length; i++)
            {
                CharacterTestRuntimeAiLabLogicSlotStateSnapshot slot = slots[i];
                result[i] = slot == null
                    ? null
                    : new CharacterTestRuntimeAiLabLogicSlotStateSnapshot
                    {
                        Slot = slot.Slot,
                        AuthorityHash = slot.AuthorityHash,
                        Agents = CharacterTestRuntimeAiLabLogicAgentStateSnapshot.CloneArray(slot.Agents)
                    };
            }
            return result;
        }

        public void Contribute(RuntimeHashAccumulator accumulator, int key)
        {
            accumulator.BeginObject(key, RuntimeAiPlannerStateHashFields.LogicSlotSchemaId,
                RuntimeAiPlannerStateHashFields.ElementSchemaVersion);
            accumulator.AddInt(LogicSlotHashFields.Slot, Slot);
            accumulator.AddLong(LogicSlotHashFields.AuthorityHash, AuthorityHash);
            CharacterTestRuntimeAiLabLogicAgentStateSnapshot[] agents =
                Agents ?? Array.Empty<CharacterTestRuntimeAiLabLogicAgentStateSnapshot>();
            accumulator.BeginArray(LogicSlotHashFields.Agents, agents.Length);
            for (int i = 0; i < agents.Length; i++)
            {
                accumulator.BeginElement(i);
                if (agents[i] != null)
                    agents[i].Contribute(accumulator, RuntimeAiPlannerStateHashFields.Element);
                else
                {
                    accumulator.BeginObject(RuntimeAiPlannerStateHashFields.Element,
                        RuntimeAiPlannerStateHashFields.LogicAgentSchemaId,
                        RuntimeAiPlannerStateHashFields.ElementSchemaVersion, false);
                    accumulator.EndObject();
                }
                accumulator.EndElement();
            }
            accumulator.EndArray();
            accumulator.EndObject();
        }
    }

    /// <summary>Warm Replay state for the active behavior-card instance of one agent.</summary>
    /// <remarks>LowFreqAlloc: snapshot, rollback, and Warm Replay paths only.</remarks>
    public sealed class CharacterTestRuntimeAiLabLogicAgentStateSnapshot
    {
        public int EntityId { get; set; }
        public CharacterTestBehaviorExecutionState ExecutionState { get; set; }
        public CharacterTestBehaviorExecutionRuntimeState ExecutionRuntimeState { get; set; }
        public int ActiveBindingIndex { get; set; } = -1;
        public int PendingBindingIndex { get; set; } = -1;
        public bool BindingReleasePending { get; set; }
        public int BehaviorCardStableId { get; set; }
        // Compatibility aliases used by the Runtime AI Lab overlay. Both names carry the
        // authored numeric card identity; the stable text label never enters runtime state.
        public int BehaviorCardId
        {
            get => BehaviorCardStableId;
            set => BehaviorCardStableId = value;
        }
        public long ProgramSemanticHash { get; set; }
        public int RuntimeState { get; set; }
        public int CurrentStageIndex { get; set; } = -1;
        public int CurrentTimelineIndex { get; set; } = -1;
        public int CardStartedFrame { get; set; } = -1;
        public int StageEnteredFrame { get; set; } = -1;
        public int TimelineEnteredFrame { get; set; } = -1;
        public int LocalFrame { get; set; }
        public int PendingTransitionIndex { get; set; } = -1;
        public int PendingTimelineIndex { get; set; } = -1;
        public int PendingEventId { get; set; } = -1;
        public int PendingEventIndex
        {
            get => PendingEventId;
            set => PendingEventId = value;
        }
        public int LastTriggeredEventId { get; set; } = -1;
        public int LastTriggeredEventIndex
        {
            get => LastTriggeredEventId;
            set => LastTriggeredEventId = value;
        }
        public bool[] DetectorArmed { get; set; }
        public bool[] TriggerArmed { get; set; }
        public bool[] TriggerFired { get; set; }
        public int PrimaryActionState { get; set; }
        public int PrimaryActionStageIndex { get; set; } = -1;
        public int PrimaryActionTimelineIndex { get; set; } = -1;
        public long PrimaryActionInstanceId { get; set; }
        public int OrchestrationState { get; set; }
        public int OrchestrationOperation { get; set; }
        public int OrchestrationTargetTimelineIndex { get; set; } = -1;
        public int OrchestrationResourceId { get; set; }
        public bool OrchestrationCommandPending { get; set; }
        public bool OrchestrationCommandSubmitted { get; set; }
        public bool OrchestrationRejectAfterCleanup { get; set; }
        public long OrchestrationPreviousInstanceId { get; set; }
        public int OrchestrationFailureReasonCode { get; set; }
        public int OrchestrationFailureReasonId
        {
            get => OrchestrationFailureReasonCode;
            set => OrchestrationFailureReasonCode = value;
        }
        public int OrchestrationFailureDetailId { get; set; }
        public CharacterTestActionUseFailure OrchestrationFailureDetails { get; set; }
        public bool CommandPending { get; set; }
        public bool CommandRejected { get; set; }
        public bool ActionObserved { get; set; }
        public bool CancelRequested { get; set; }
        public bool ClearControlPending { get; set; }
        public int ActionEnteredFrame { get; set; }
        public int[] EventExecutionCounts { get; set; }
        public int[] EventLastExecutionFrames { get; set; }
        public int[] EventResults { get; set; }
        public int[] PreviousEventResults { get; set; }
        public int ActorOwnerEventIndex { get; set; } = -1;
        public int PendingV6Operation { get; set; }
        public int LastEvaluatedRuntimeFrame { get; set; } = -1;
        public int LocalPlayheadFrame { get; set; }
        public int TimelinePassGeneration { get; set; }
        public int[] LoopRepeatCounts { get; set; }
        public int ActiveWaitEventIndex { get; set; } = -1;
        public int PendingPlayheadTargetFrame { get; set; } = -1;
        public int PendingTimeControlEventIndex { get; set; } = -1;
        public bool WaitingForInstantOwnerAtTimelineEnd { get; set; }
        public int LastSampledPlayheadFrame { get; set; } = -1;

        public static CharacterTestRuntimeAiLabLogicAgentStateSnapshot[] CloneArray(
            CharacterTestRuntimeAiLabLogicAgentStateSnapshot[] agents)
        {
            if (agents == null || agents.Length == 0)
                return Array.Empty<CharacterTestRuntimeAiLabLogicAgentStateSnapshot>();
            var result = new CharacterTestRuntimeAiLabLogicAgentStateSnapshot[agents.Length];
            for (int i = 0; i < agents.Length; i++)
            {
                CharacterTestRuntimeAiLabLogicAgentStateSnapshot source = agents[i];
                if (source == null)
                    continue;
                result[i] = new CharacterTestRuntimeAiLabLogicAgentStateSnapshot
                {
                    EntityId = source.EntityId,
                    ExecutionState = source.ExecutionState?.Clone(), ExecutionRuntimeState = source.ExecutionRuntimeState?.Clone(),
                    ActiveBindingIndex = source.ActiveBindingIndex,
                    PendingBindingIndex = source.PendingBindingIndex,
                    BindingReleasePending = source.BindingReleasePending,
                    BehaviorCardStableId = source.BehaviorCardStableId,
                    ProgramSemanticHash = source.ProgramSemanticHash,
                    RuntimeState = source.RuntimeState,
                    CurrentStageIndex = source.CurrentStageIndex,
                    CurrentTimelineIndex = source.CurrentTimelineIndex,
                    CardStartedFrame = source.CardStartedFrame,
                    StageEnteredFrame = source.StageEnteredFrame,
                    TimelineEnteredFrame = source.TimelineEnteredFrame,
                    LocalFrame = source.LocalFrame,
                    PendingTransitionIndex = source.PendingTransitionIndex,
                    PendingTimelineIndex = source.PendingTimelineIndex,
                    PendingEventId = source.PendingEventId,
                    LastTriggeredEventId = source.LastTriggeredEventId,
                    DetectorArmed = source.DetectorArmed != null
                        ? (bool[])source.DetectorArmed.Clone()
                        : Array.Empty<bool>(),
                    TriggerArmed = source.TriggerArmed != null
                        ? (bool[])source.TriggerArmed.Clone()
                        : Array.Empty<bool>(),
                    TriggerFired = source.TriggerFired != null
                        ? (bool[])source.TriggerFired.Clone()
                        : Array.Empty<bool>(),
                    PrimaryActionState = source.PrimaryActionState,
                    PrimaryActionStageIndex = source.PrimaryActionStageIndex,
                    PrimaryActionTimelineIndex = source.PrimaryActionTimelineIndex,
                    PrimaryActionInstanceId = source.PrimaryActionInstanceId,
                    OrchestrationState = source.OrchestrationState,
                    OrchestrationOperation = source.OrchestrationOperation,
                    OrchestrationTargetTimelineIndex = source.OrchestrationTargetTimelineIndex,
                    OrchestrationResourceId = source.OrchestrationResourceId,
                    OrchestrationCommandPending = source.OrchestrationCommandPending,
                    OrchestrationCommandSubmitted = source.OrchestrationCommandSubmitted,
                    OrchestrationRejectAfterCleanup = source.OrchestrationRejectAfterCleanup,
                    OrchestrationPreviousInstanceId = source.OrchestrationPreviousInstanceId,
                    OrchestrationFailureReasonCode = source.OrchestrationFailureReasonCode,
                    OrchestrationFailureDetailId = source.OrchestrationFailureDetailId,
                    OrchestrationFailureDetails = source.OrchestrationFailureDetails,
                    CommandPending = source.CommandPending,
                    CommandRejected = source.CommandRejected,
                    ActionObserved = source.ActionObserved,
                    CancelRequested = source.CancelRequested,
                    ClearControlPending = source.ClearControlPending,
                    ActionEnteredFrame = source.ActionEnteredFrame,
                    EventExecutionCounts = source.EventExecutionCounts == null ? null : (int[])source.EventExecutionCounts.Clone(),
                    EventLastExecutionFrames = source.EventLastExecutionFrames == null ? null : (int[])source.EventLastExecutionFrames.Clone(),
                    EventResults = source.EventResults == null ? null : (int[])source.EventResults.Clone(),
                    PreviousEventResults = source.PreviousEventResults == null ? null : (int[])source.PreviousEventResults.Clone(),
                    ActorOwnerEventIndex = source.ActorOwnerEventIndex,
                    PendingV6Operation = source.PendingV6Operation,
                    LastEvaluatedRuntimeFrame = source.LastEvaluatedRuntimeFrame,
                    LocalPlayheadFrame = source.LocalPlayheadFrame,
                    TimelinePassGeneration = source.TimelinePassGeneration,
                    LoopRepeatCounts = source.LoopRepeatCounts == null ? null : (int[])source.LoopRepeatCounts.Clone(),
                    ActiveWaitEventIndex = source.ActiveWaitEventIndex,
                    PendingPlayheadTargetFrame = source.PendingPlayheadTargetFrame
                    ,PendingTimeControlEventIndex = source.PendingTimeControlEventIndex
                    ,WaitingForInstantOwnerAtTimelineEnd = source.WaitingForInstantOwnerAtTimelineEnd
                    ,LastSampledPlayheadFrame = source.LastSampledPlayheadFrame
                };
            }
            return result;
        }

        public void Contribute(RuntimeHashAccumulator accumulator, int key)
        {
            accumulator.BeginObject(key, RuntimeAiPlannerStateHashFields.LogicAgentSchemaId,
                RuntimeAiPlannerStateHashFields.ElementSchemaVersion);
            accumulator.AddInt(LogicAgentHashFields.EntityId, EntityId);
            accumulator.AddInt(LogicAgentHashFields.ActiveBindingIndex, ActiveBindingIndex);
            accumulator.AddInt(LogicAgentHashFields.PendingBindingIndex, PendingBindingIndex);
            accumulator.AddBool(LogicAgentHashFields.BindingReleasePending, BindingReleasePending);
            accumulator.AddInt(LogicAgentHashFields.BehaviorCardStableId, BehaviorCardStableId);
            accumulator.AddLong(LogicAgentHashFields.ProgramSemanticHash, ProgramSemanticHash);
            if (ExecutionState != null)
            {
                ExecutionState.AddHash(accumulator, LogicAgentHashFields.ExecutionState);
                if (ExecutionRuntimeState != null)
                    ExecutionRuntimeState.AddHash(accumulator, LogicAgentHashFields.ExecutionRuntimeState);
                else
                {
                    accumulator.BeginObject(LogicAgentHashFields.ExecutionRuntimeState,
                        RuntimeAiPlannerStateHashFields.ExecutionRuntimeSchemaId,
                        RuntimeAiPlannerStateHashFields.ElementSchemaVersion, false);
                    accumulator.EndObject();
                }
                accumulator.EndObject();
                return;
            }
            accumulator.AddInt(LogicAgentHashFields.RuntimeState, RuntimeState);
            accumulator.AddInt(LogicAgentHashFields.CurrentStageIndex, CurrentStageIndex);
            accumulator.AddInt(LogicAgentHashFields.CurrentTimelineIndex, CurrentTimelineIndex);
            accumulator.AddInt(LogicAgentHashFields.CardStartedFrame, CardStartedFrame);
            accumulator.AddInt(LogicAgentHashFields.StageEnteredFrame, StageEnteredFrame);
            accumulator.AddInt(LogicAgentHashFields.TimelineEnteredFrame, TimelineEnteredFrame);
            accumulator.AddInt(LogicAgentHashFields.LocalFrame, LocalFrame);
            accumulator.AddInt(LogicAgentHashFields.PendingTransitionIndex, PendingTransitionIndex);
            accumulator.AddInt(LogicAgentHashFields.PendingTimelineIndex, PendingTimelineIndex);
            accumulator.AddInt(LogicAgentHashFields.PendingEventId, PendingEventId);
            accumulator.AddInt(LogicAgentHashFields.LastTriggeredEventId, LastTriggeredEventId);
            RuntimeAiPlannerStateHashFields.AddBoolArray(accumulator, LogicAgentHashFields.DetectorArmed, DetectorArmed);
            RuntimeAiPlannerStateHashFields.AddBoolArray(accumulator, LogicAgentHashFields.TriggerArmed, TriggerArmed);
            RuntimeAiPlannerStateHashFields.AddBoolArray(accumulator, LogicAgentHashFields.TriggerFired, TriggerFired);
            accumulator.AddInt(LogicAgentHashFields.PrimaryActionState, PrimaryActionState);
            accumulator.AddInt(LogicAgentHashFields.PrimaryActionStageIndex, PrimaryActionStageIndex);
            accumulator.AddInt(LogicAgentHashFields.PrimaryActionTimelineIndex, PrimaryActionTimelineIndex);
            accumulator.AddLong(LogicAgentHashFields.PrimaryActionInstanceId, PrimaryActionInstanceId);
            accumulator.AddInt(LogicAgentHashFields.OrchestrationState, OrchestrationState);
            accumulator.AddInt(LogicAgentHashFields.OrchestrationOperation, OrchestrationOperation);
            accumulator.AddInt(LogicAgentHashFields.OrchestrationTargetTimelineIndex, OrchestrationTargetTimelineIndex);
            accumulator.AddInt(LogicAgentHashFields.OrchestrationResourceId, OrchestrationResourceId);
            accumulator.AddBool(LogicAgentHashFields.OrchestrationCommandPending, OrchestrationCommandPending);
            accumulator.AddBool(LogicAgentHashFields.OrchestrationCommandSubmitted, OrchestrationCommandSubmitted);
            accumulator.AddBool(LogicAgentHashFields.OrchestrationRejectAfterCleanup, OrchestrationRejectAfterCleanup);
            accumulator.AddLong(LogicAgentHashFields.OrchestrationPreviousInstanceId, OrchestrationPreviousInstanceId);
            accumulator.AddInt(LogicAgentHashFields.OrchestrationFailureReasonCode, OrchestrationFailureReasonCode);
            accumulator.AddInt(LogicAgentHashFields.OrchestrationFailureDetailId, OrchestrationFailureDetailId);
            OrchestrationFailureDetails.ContributeHash(accumulator, LogicAgentHashFields.OrchestrationFailureDetails);
            accumulator.AddBool(LogicAgentHashFields.CommandPending, CommandPending);
            accumulator.AddBool(LogicAgentHashFields.CommandRejected, CommandRejected);
            accumulator.AddBool(LogicAgentHashFields.ActionObserved, ActionObserved);
            accumulator.AddBool(LogicAgentHashFields.CancelRequested, CancelRequested);
            accumulator.AddBool(LogicAgentHashFields.ClearControlPending, ClearControlPending);
            accumulator.AddInt(LogicAgentHashFields.ActionEnteredFrame, ActionEnteredFrame);
            RuntimeAiPlannerStateHashFields.AddIntArray(accumulator, LogicAgentHashFields.EventExecutionCounts, EventExecutionCounts);
            RuntimeAiPlannerStateHashFields.AddIntArray(accumulator, LogicAgentHashFields.EventLastExecutionFrames, EventLastExecutionFrames);
            RuntimeAiPlannerStateHashFields.AddIntArray(accumulator, LogicAgentHashFields.EventResults, EventResults);
            RuntimeAiPlannerStateHashFields.AddIntArray(accumulator, LogicAgentHashFields.PreviousEventResults, PreviousEventResults);
            accumulator.AddInt(LogicAgentHashFields.ActorOwnerEventIndex, ActorOwnerEventIndex);
            accumulator.AddInt(LogicAgentHashFields.PendingV6Operation, PendingV6Operation);
            accumulator.AddInt(LogicAgentHashFields.LastEvaluatedRuntimeFrame, LastEvaluatedRuntimeFrame);
            accumulator.AddInt(LogicAgentHashFields.LocalPlayheadFrame, LocalPlayheadFrame);
            accumulator.AddInt(LogicAgentHashFields.LastSampledPlayheadFrame, LastSampledPlayheadFrame);
            accumulator.AddInt(LogicAgentHashFields.TimelinePassGeneration, TimelinePassGeneration);
            RuntimeAiPlannerStateHashFields.AddIntArray(accumulator, LogicAgentHashFields.LoopRepeatCounts, LoopRepeatCounts);
            accumulator.AddInt(LogicAgentHashFields.ActiveWaitEventIndex, ActiveWaitEventIndex);
            accumulator.AddInt(LogicAgentHashFields.PendingPlayheadTargetFrame, PendingPlayheadTargetFrame);
            accumulator.AddInt(LogicAgentHashFields.PendingTimeControlEventIndex, PendingTimeControlEventIndex);
            accumulator.AddBool(LogicAgentHashFields.WaitingForInstantOwnerAtTimelineEnd, WaitingForInstantOwnerAtTimelineEnd);
            accumulator.EndObject();
        }
    }

    /// <summary>
    /// Captures Runtime AI Planner evaluator state for one compiled profile slot.
    /// </summary>
    /// <remarks>LowFreqAlloc: used by snapshot and rollback capture/apply paths.</remarks>
    public sealed class CharacterTestRuntimeAiPlannerSlotStateSnapshot
    {
        public int Slot { get; set; }
        public CharacterTestRuntimeAiPlannerAgentStateSnapshot[] Agents { get; set; }

        /// <summary>
        /// Creates a deep copy of slot snapshots for rollback checkpoint storage.
        /// </summary>
        public static CharacterTestRuntimeAiPlannerSlotStateSnapshot[] CloneArray(
            CharacterTestRuntimeAiPlannerSlotStateSnapshot[] slots)
        {
            if (slots == null || slots.Length == 0)
                return Array.Empty<CharacterTestRuntimeAiPlannerSlotStateSnapshot>();

            var result = new CharacterTestRuntimeAiPlannerSlotStateSnapshot[slots.Length];
            for (int i = 0; i < slots.Length; i++)
            {
                CharacterTestRuntimeAiPlannerSlotStateSnapshot slot = slots[i];
                result[i] = slot == null
                    ? null
                    : new CharacterTestRuntimeAiPlannerSlotStateSnapshot
                    {
                        Slot = slot.Slot,
                        Agents = CharacterTestRuntimeAiPlannerAgentStateSnapshot.CloneArray(slot.Agents)
                    };
            }

            return result;
        }

        /// <summary>
        /// Adds this slot's planner continuous state to the CharacterTest deterministic state hash.
        /// </summary>
        public void Contribute(RuntimeHashAccumulator accumulator, int key)
        {
            accumulator.BeginObject(key, RuntimeAiPlannerStateHashFields.SlotSchemaId,
                RuntimeAiPlannerStateHashFields.ElementSchemaVersion);
            accumulator.AddInt(PlannerSlotHashFields.Slot, Slot);
            CharacterTestRuntimeAiPlannerAgentStateSnapshot[] agents =
                Agents ?? Array.Empty<CharacterTestRuntimeAiPlannerAgentStateSnapshot>();
            accumulator.BeginArray(PlannerSlotHashFields.Agents, agents.Length);
            for (int i = 0; i < agents.Length; i++)
            {
                accumulator.BeginElement(i);
                if (agents[i] != null)
                    agents[i].Contribute(accumulator, RuntimeAiPlannerStateHashFields.Element);
                else
                {
                    accumulator.BeginObject(RuntimeAiPlannerStateHashFields.Element,
                        RuntimeAiPlannerStateHashFields.PlannerAgentSchemaId,
                        RuntimeAiPlannerStateHashFields.ElementSchemaVersion, false);
                    accumulator.EndObject();
                }
                accumulator.EndElement();
            }
            accumulator.EndArray();
            accumulator.EndObject();
        }
    }

    /// <summary>
    /// Captures one agent's Runtime AI Planner continuous state that affects future decisions.
    /// </summary>
    /// <remarks>LowFreqAlloc: used by snapshot and rollback capture/apply paths.</remarks>
    public sealed class CharacterTestRuntimeAiPlannerAgentStateSnapshot
    {
        public int EntityId { get; set; }
        public int CurrentStateId { get; set; }
        public bool HadSubmittedControlIntent { get; set; }
        /// <summary>Legacy diagnostic field; schema 2 no longer serializes or hashes it.</summary>
        [JsonIgnore]
        public string LastCommand { get; set; }
        public int LastSeenTargetEntityId { get; set; }
        public int TargetLastSeenAgeFrames { get; set; }
        public int ObservedTargetAttackEntityId { get; set; }
        public CharacterActionId ObservedTargetAttackActionId { get; set; }
        public int ObservedTargetAttackLocalFrame { get; set; } = -1;
        public int TargetAttackSequence { get; set; }
        public int LastRespondedTargetAttackSequence { get; set; }
        public int NextDodgeAllowedFrame { get; set; }
        public int PendingDodgeTargetAttackSequence { get; set; }
        public int PendingDodgeCooldownFrames { get; set; }
        public int PendingDodgeRequestFrame { get; set; } = -1;
        public int LastMovementActionId { get; set; }
        public float LastDesiredMoveX { get; set; }
        public float LastDesiredMoveY { get; set; }
        public float LastOutputMoveX { get; set; }
        public float LastOutputMoveY { get; set; }
        public int StrafeSide { get; set; }
        public int NextStrafeSwitchFrame { get; set; }
        public int DirectionHoldFramesRemaining { get; set; }
        public int ReactionDelayFramesRemaining { get; set; }
        public float LastMovementSpeedScale { get; set; }

        /// <summary>
        /// Creates a deep copy of agent snapshots for rollback checkpoint storage.
        /// </summary>
        public static CharacterTestRuntimeAiPlannerAgentStateSnapshot[] CloneArray(
            CharacterTestRuntimeAiPlannerAgentStateSnapshot[] agents)
        {
            if (agents == null || agents.Length == 0)
                return Array.Empty<CharacterTestRuntimeAiPlannerAgentStateSnapshot>();

            var result = new CharacterTestRuntimeAiPlannerAgentStateSnapshot[agents.Length];
            for (int i = 0; i < agents.Length; i++)
            {
                CharacterTestRuntimeAiPlannerAgentStateSnapshot agent = agents[i];
                result[i] = agent == null
                    ? null
                    : new CharacterTestRuntimeAiPlannerAgentStateSnapshot
                    {
                        EntityId = agent.EntityId,
                        CurrentStateId = agent.CurrentStateId,
                        HadSubmittedControlIntent = agent.HadSubmittedControlIntent,
                        LastSeenTargetEntityId = agent.LastSeenTargetEntityId,
                        TargetLastSeenAgeFrames = agent.TargetLastSeenAgeFrames,
                        ObservedTargetAttackEntityId = agent.ObservedTargetAttackEntityId,
                        ObservedTargetAttackActionId = agent.ObservedTargetAttackActionId,
                        ObservedTargetAttackLocalFrame = agent.ObservedTargetAttackLocalFrame,
                        TargetAttackSequence = agent.TargetAttackSequence,
                        LastRespondedTargetAttackSequence = agent.LastRespondedTargetAttackSequence,
                        NextDodgeAllowedFrame = agent.NextDodgeAllowedFrame,
                        PendingDodgeTargetAttackSequence = agent.PendingDodgeTargetAttackSequence,
                        PendingDodgeCooldownFrames = agent.PendingDodgeCooldownFrames,
                        PendingDodgeRequestFrame = agent.PendingDodgeRequestFrame,
                        LastMovementActionId = agent.LastMovementActionId,
                        LastDesiredMoveX = agent.LastDesiredMoveX,
                        LastDesiredMoveY = agent.LastDesiredMoveY,
                        LastOutputMoveX = agent.LastOutputMoveX,
                        LastOutputMoveY = agent.LastOutputMoveY,
                        StrafeSide = agent.StrafeSide,
                        NextStrafeSwitchFrame = agent.NextStrafeSwitchFrame,
                        DirectionHoldFramesRemaining = agent.DirectionHoldFramesRemaining,
                        ReactionDelayFramesRemaining = agent.ReactionDelayFramesRemaining,
                        LastMovementSpeedScale = agent.LastMovementSpeedScale
                    };
            }

            return result;
        }

        /// <summary>
        /// Adds this agent's planner continuous state to the CharacterTest deterministic state hash.
        /// </summary>
        public void Contribute(RuntimeHashAccumulator accumulator, int key)
        {
            accumulator.BeginObject(key, RuntimeAiPlannerStateHashFields.PlannerAgentSchemaId,
                RuntimeAiPlannerStateHashFields.ElementSchemaVersion);
            accumulator.AddInt(PlannerAgentHashFields.EntityId, EntityId);
            accumulator.AddInt(PlannerAgentHashFields.CurrentStateId, CurrentStateId);
            accumulator.AddBool(PlannerAgentHashFields.HadSubmittedControlIntent, HadSubmittedControlIntent);
            accumulator.AddInt(PlannerAgentHashFields.LastSeenTargetEntityId, LastSeenTargetEntityId);
            accumulator.AddInt(PlannerAgentHashFields.TargetLastSeenAgeFrames, TargetLastSeenAgeFrames);
            accumulator.AddInt(PlannerAgentHashFields.ObservedTargetAttackEntityId, ObservedTargetAttackEntityId);
            accumulator.AddInt(PlannerAgentHashFields.ObservedTargetAttackActionId, ObservedTargetAttackActionId.Value);
            accumulator.AddInt(PlannerAgentHashFields.ObservedTargetAttackLocalFrame, ObservedTargetAttackLocalFrame);
            accumulator.AddInt(PlannerAgentHashFields.TargetAttackSequence, TargetAttackSequence);
            accumulator.AddInt(PlannerAgentHashFields.LastRespondedTargetAttackSequence, LastRespondedTargetAttackSequence);
            accumulator.AddInt(PlannerAgentHashFields.NextDodgeAllowedFrame, NextDodgeAllowedFrame);
            accumulator.AddInt(PlannerAgentHashFields.PendingDodgeTargetAttackSequence, PendingDodgeTargetAttackSequence);
            accumulator.AddInt(PlannerAgentHashFields.PendingDodgeCooldownFrames, PendingDodgeCooldownFrames);
            accumulator.AddInt(PlannerAgentHashFields.PendingDodgeRequestFrame, PendingDodgeRequestFrame);
            accumulator.AddInt(PlannerAgentHashFields.LastMovementActionId, LastMovementActionId);
            accumulator.AddDoubleQuantized(PlannerAgentHashFields.LastDesiredMoveX, LastDesiredMoveX, 1000000d);
            accumulator.AddDoubleQuantized(PlannerAgentHashFields.LastDesiredMoveY, LastDesiredMoveY, 1000000d);
            accumulator.AddDoubleQuantized(PlannerAgentHashFields.LastOutputMoveX, LastOutputMoveX, 1000000d);
            accumulator.AddDoubleQuantized(PlannerAgentHashFields.LastOutputMoveY, LastOutputMoveY, 1000000d);
            accumulator.AddInt(PlannerAgentHashFields.StrafeSide, StrafeSide);
            accumulator.AddInt(PlannerAgentHashFields.NextStrafeSwitchFrame, NextStrafeSwitchFrame);
            accumulator.AddInt(PlannerAgentHashFields.DirectionHoldFramesRemaining, DirectionHoldFramesRemaining);
            accumulator.AddInt(PlannerAgentHashFields.ReactionDelayFramesRemaining, ReactionDelayFramesRemaining);
            accumulator.AddDoubleQuantized(PlannerAgentHashFields.LastMovementSpeedScale, LastMovementSpeedScale, 1000000d);
            accumulator.EndObject();
        }
    }

    // Integer field identities are part of the CharacterTest state protocol. Keep these
    // values stable when fields are reordered in source or when the runtime hash backend
    // changes from FNV to XXH.
    internal static class RuntimeAiPlannerStateHashFields
    {
        internal const int SchemaId = RuntimeHashSchemaIds.RuntimeAiPlannerState;
        internal const int SchemaVersion = 2;
        internal const int ElementSchemaVersion = 1;
        internal const int SnapshotSchemaVersion = 1;
        internal const int Slots = 2;
        internal const int BehaviorLogicSlots = 3;
        internal const int DamageZoneLocks = 4;
        internal const int EngagementReservations = 5;
        internal const int Element = 1;
        internal const int SlotSchemaId = RuntimeHashSchemaIds.RuntimeAiPlannerSlotState;
        internal const int LogicSlotSchemaId = RuntimeHashSchemaIds.RuntimeAiPlannerBehaviorComposition;
        internal const int LogicAgentSchemaId = RuntimeHashSchemaIds.RuntimeAiPlannerBehaviorExecution;
        internal const int PlannerAgentSchemaId = RuntimeHashSchemaIds.RuntimeAiPlannerSlotState;
        internal const int EngagementReservationSchemaId = RuntimeHashSchemaIds.RuntimeAiPlannerSessionAuthority;
        internal const int DamageZoneLockSchemaId = RuntimeHashSchemaIds.RuntimeAiPlannerSessionParticipant;
        internal const int ExecutionRuntimeSchemaId = RuntimeHashSchemaIds.RuntimeAiPlannerBehaviorExecution;

        internal static void AddIntArray(RuntimeHashAccumulator accumulator, int key, int[] values)
        {
            values ??= Array.Empty<int>();
            accumulator.BeginArray(key, values.Length);
            for (int i = 0; i < values.Length; i++)
            {
                accumulator.BeginElement(i);
                accumulator.AddInt(Element, values[i]);
                accumulator.EndElement();
            }
            accumulator.EndArray();
        }

        internal static void AddBoolArray(RuntimeHashAccumulator accumulator, int key, bool[] values)
        {
            values ??= Array.Empty<bool>();
            accumulator.BeginArray(key, values.Length);
            for (int i = 0; i < values.Length; i++)
            {
                accumulator.BeginElement(i);
                accumulator.AddBool(Element, values[i]);
                accumulator.EndElement();
            }
            accumulator.EndArray();
        }
    }

    internal static class EngagementReservationHashFields
    {
        internal const int SourceEntityId = 1;
        internal const int TargetEntityId = 2;
        internal const int TargetCombatEntityId = 3;
        internal const int ResolvedActionRevision = 4;
        internal const int ZoneId = 5;
        internal const int AnchorId = 6;
        internal const int Capacity = 7;
        internal const int CapacitySlot = 8;
        internal const int AcquiredFrame = 9;
        internal const int UpdatedFrame = 10;
        internal const int ZoneBindingRevision = 11;
        internal const int ZoneRevision = 12;
        internal const int AnchorRevision = 13;
        internal const int AnchorContentHash = 14;
        internal const int PhaseId = 15;
        internal const int PhaseRevision = 16;
        internal const int GraphRevision = 17;
        internal const int GraphContentHash = 18;
        internal const int ReleaseReason = 19;
    }

    internal static class DamageZoneLockHashFields
    {
        internal const int SourceEntityId = 1;
        internal const int TargetGameplayEntityIndex = 2;
        internal const int TargetGameplayEntityGeneration = 3;
        internal const int TargetCombatEntityId = 4;
        internal const int ZoneId = 5;
        internal const int HitZoneId = 6;
        internal const int LogicalHurtboxId = 7;
    }

    internal static class LogicSlotHashFields
    {
        internal const int Slot = 1;
        internal const int AuthorityHash = 2;
        internal const int Agents = 3;
    }

    internal static class LogicAgentHashFields
    {
        internal const int EntityId = 1;
        internal const int ActiveBindingIndex = 2;
        internal const int PendingBindingIndex = 3;
        internal const int BindingReleasePending = 4;
        internal const int BehaviorCardStableId = 5;
        internal const int ProgramSemanticHash = 6;
        internal const int ExecutionState = 7;
        internal const int ExecutionRuntimeState = 8;
        internal const int RuntimeState = 9;
        internal const int CurrentStageIndex = 10;
        internal const int CurrentTimelineIndex = 11;
        internal const int CardStartedFrame = 12;
        internal const int StageEnteredFrame = 13;
        internal const int TimelineEnteredFrame = 14;
        internal const int LocalFrame = 15;
        internal const int PendingTransitionIndex = 16;
        internal const int PendingTimelineIndex = 17;
        internal const int PendingEventId = 18;
        internal const int LastTriggeredEventId = 19;
        internal const int DetectorArmed = 20;
        internal const int TriggerArmed = 21;
        internal const int TriggerFired = 22;
        internal const int PrimaryActionState = 23;
        internal const int PrimaryActionStageIndex = 24;
        internal const int PrimaryActionTimelineIndex = 25;
        internal const int PrimaryActionInstanceId = 26;
        internal const int OrchestrationState = 27;
        internal const int OrchestrationOperation = 28;
        internal const int OrchestrationTargetTimelineIndex = 29;
        internal const int OrchestrationResourceId = 30;
        internal const int OrchestrationCommandPending = 31;
        internal const int OrchestrationCommandSubmitted = 32;
        internal const int OrchestrationRejectAfterCleanup = 33;
        internal const int OrchestrationPreviousInstanceId = 34;
        internal const int OrchestrationFailureReasonCode = 35;
        internal const int CommandPending = 36;
        internal const int CommandRejected = 37;
        internal const int ActionObserved = 38;
        internal const int CancelRequested = 39;
        internal const int ClearControlPending = 40;
        internal const int ActionEnteredFrame = 41;
        internal const int EventExecutionCounts = 42;
        internal const int EventLastExecutionFrames = 43;
        internal const int EventResults = 44;
        internal const int PreviousEventResults = 45;
        internal const int ActorOwnerEventIndex = 46;
        internal const int PendingV6Operation = 47;
        internal const int LastEvaluatedRuntimeFrame = 48;
        internal const int LocalPlayheadFrame = 49;
        internal const int TimelinePassGeneration = 50;
        internal const int LoopRepeatCounts = 51;
        internal const int ActiveWaitEventIndex = 52;
        internal const int PendingPlayheadTargetFrame = 53;
        internal const int PendingTimeControlEventIndex = 54;
        internal const int WaitingForInstantOwnerAtTimelineEnd = 55;
        internal const int LastSampledPlayheadFrame = 56;
        internal const int OrchestrationFailureDetailId = 57;
        internal const int OrchestrationFailureDetails = 58;
    }

    internal static class PlannerSlotHashFields
    {
        internal const int Slot = 1;
        internal const int Agents = 2;
    }

    internal static class PlannerAgentHashFields
    {
        internal const int EntityId = 1;
        internal const int CurrentStateId = 2;
        internal const int HadSubmittedControlIntent = 3;
        internal const int LastSeenTargetEntityId = 4;
        internal const int TargetLastSeenAgeFrames = 5;
        internal const int ObservedTargetAttackEntityId = 6;
        internal const int ObservedTargetAttackActionId = 7;
        internal const int ObservedTargetAttackLocalFrame = 8;
        internal const int TargetAttackSequence = 9;
        internal const int LastRespondedTargetAttackSequence = 10;
        internal const int NextDodgeAllowedFrame = 11;
        internal const int PendingDodgeTargetAttackSequence = 12;
        internal const int PendingDodgeCooldownFrames = 13;
        internal const int PendingDodgeRequestFrame = 14;
        internal const int LastMovementActionId = 15;
        internal const int LastDesiredMoveX = 16;
        internal const int LastDesiredMoveY = 17;
        internal const int LastOutputMoveX = 18;
        internal const int LastOutputMoveY = 19;
        internal const int StrafeSide = 20;
        internal const int NextStrafeSwitchFrame = 21;
        internal const int DirectionHoldFramesRemaining = 22;
        internal const int ReactionDelayFramesRemaining = 23;
        internal const int LastMovementSpeedScale = 24;
    }
}
