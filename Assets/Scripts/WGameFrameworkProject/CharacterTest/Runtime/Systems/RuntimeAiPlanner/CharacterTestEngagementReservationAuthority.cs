using System;
using MxFramework.CharacterRuntimeSpawn;
using MxFramework.Combat.Core;
using MxFramework.Runtime;

namespace MxFramework.Game.CharacterTest
{
    internal enum CharacterTestEngagementReservationReleaseReason
    {
        None = 0,
        OwnerInvalid = 1,
        TargetInvalid = 2,
        ActionChanged = 3,
        ZoneInvalid = 4,
        AnchorInvalid = 5,
        PhaseChanged = 6,
        CapacityChanged = 7,
        PhysicsChanged = 8,
        GraphChanged = 9,
        NotRenewed = 10,
        StableConflictLost = 11,
        RuntimeReset = 12
    }

    internal readonly struct CharacterTestEngagementReservation
    {
        public CharacterTestEngagementReservation(
            int sourceEntityId,
            int targetEntityId,
            CombatEntityId targetCombatEntityId,
            int resolvedActionRevision,
            int zoneId,
            int anchorId,
            int capacity,
            int capacitySlot,
            RuntimeFrame acquiredFrame,
            RuntimeFrame updatedFrame,
            int zoneBindingRevision,
            int zoneRevision,
            int anchorRevision,
            RuntimePreparedDigest anchorContentHash,
            int phaseId,
            int phaseRevision,
            int combatPhysicsRevision,
            int graphRevision,
            long graphContentHash,
            CharacterTestEngagementReservationReleaseReason releaseReason =
                CharacterTestEngagementReservationReleaseReason.None)
        {
            SourceEntityId = sourceEntityId;
            TargetEntityId = targetEntityId;
            TargetCombatEntityId = targetCombatEntityId;
            ResolvedActionRevision = resolvedActionRevision;
            ZoneId = zoneId;
            AnchorId = anchorId;
            Capacity = capacity;
            CapacitySlot = capacitySlot;
            AcquiredFrame = acquiredFrame;
            UpdatedFrame = updatedFrame;
            ZoneBindingRevision = zoneBindingRevision;
            ZoneRevision = zoneRevision;
            AnchorRevision = anchorRevision;
            AnchorContentHash = anchorContentHash;
            PhaseId = phaseId;
            PhaseRevision = phaseRevision;
            CombatPhysicsRevision = combatPhysicsRevision;
            GraphRevision = graphRevision;
            GraphContentHash = graphContentHash;
            ReleaseReason = releaseReason;
        }

        public int SourceEntityId { get; }
        public int TargetEntityId { get; }
        public CombatEntityId TargetCombatEntityId { get; }
        public int ResolvedActionRevision { get; }
        public int ZoneId { get; }
        public int AnchorId { get; }
        public int Capacity { get; }
        public int CapacitySlot { get; }
        public RuntimeFrame AcquiredFrame { get; }
        public RuntimeFrame UpdatedFrame { get; }
        public int ZoneBindingRevision { get; }
        public int ZoneRevision { get; }
        public int AnchorRevision { get; }
        public RuntimePreparedDigest AnchorContentHash { get; }
        public int PhaseId { get; }
        public int PhaseRevision { get; }
        public int CombatPhysicsRevision { get; }
        public int GraphRevision { get; }
        public long GraphContentHash { get; }
        public CharacterTestEngagementReservationReleaseReason ReleaseReason { get; }
        public bool IsActive => SourceEntityId > 0
            && TargetEntityId > 0
            && TargetCombatEntityId.Value > 0
            && ResolvedActionRevision > 0
            && !(ZoneId <= 0)
            && !(AnchorId <= 0)
            && Capacity > 0
            && CapacitySlot >= 0
            && ReleaseReason == CharacterTestEngagementReservationReleaseReason.None;

        public CharacterTestEngagementReservation WithUpdate(
            RuntimeFrame frame,
            in CharacterTestEngagementCandidateResult selection)
        {
            return new CharacterTestEngagementReservation(
                SourceEntityId,
                TargetEntityId,
                TargetCombatEntityId,
                ResolvedActionRevision,
                ZoneId,
                AnchorId,
                selection.Capacity,
                CapacitySlot,
                AcquiredFrame,
                frame,
                selection.Pose.ZoneBindingRevision,
                selection.Pose.ZoneRevision,
                selection.Pose.AnchorRevision,
                selection.Pose.AnchorContentHash,
                selection.Pose.PhaseId,
                selection.Pose.PhaseRevision,
                selection.Pose.CombatPhysicsRevision,
                selection.Pose.GraphRevision,
                selection.Pose.GraphContentHash);
        }

        public CharacterTestEngagementReservation WithCapacitySlot(int slot)
        {
            return new CharacterTestEngagementReservation(
                SourceEntityId,
                TargetEntityId,
                TargetCombatEntityId,
                ResolvedActionRevision,
                ZoneId,
                AnchorId,
                Capacity,
                slot,
                AcquiredFrame,
                UpdatedFrame,
                ZoneBindingRevision,
                ZoneRevision,
                AnchorRevision,
                AnchorContentHash,
                PhaseId,
                PhaseRevision,
                CombatPhysicsRevision,
                GraphRevision,
                GraphContentHash);
        }
    }

    /// <summary>
    /// Fixed-capacity deterministic reservation authority. Per-frame lookup/update is allocation-free;
    /// capture/apply is an explicit LowFreqAlloc snapshot boundary.
    /// </summary>
    internal sealed class CharacterTestEngagementReservationAuthority
    {
        internal const int MaximumReservationCount = 128;
        private readonly CharacterTestEngagementReservation[] _entries =
            new CharacterTestEngagementReservation[MaximumReservationCount];
        private int _count;

        public int Count => _count;
        internal int Revision { get; private set; } = 1;

        public void Clear()
        {
            bool changed = _count > 0;
            Array.Clear(_entries, 0, _count);
            _count = 0;
            if (changed)
                IncrementRevision();
        }

        /// <remarks>NoAlloc.</remarks>
        public bool TryGetForSource(
            int sourceEntityId,
            out CharacterTestEngagementReservation reservation)
        {
            int index = FindSource(sourceEntityId);
            if (index >= 0)
            {
                reservation = _entries[index];
                return reservation.IsActive;
            }
            reservation = default;
            return false;
        }

        /// <remarks>NoAlloc. Excludes the renewing owner from the capacity count.</remarks>
        public int GetReservationCount(
            CombatEntityId targetEntityId,
            int zoneId,
            int anchorId,
            int excludingSourceEntityId = 0)
        {
            int count = 0;
            for (int i = 0; i < _count; i++)
            {
                CharacterTestEngagementReservation entry = _entries[i];
                if (entry.SourceEntityId != excludingSourceEntityId
                    && SameKey(entry, targetEntityId, zoneId, anchorId))
                {
                    count++;
                }
            }
            return count;
        }

        /// <summary>
        /// Read-only equivalent of the capacity/conflict decision made by
        /// <see cref="TryAcquire"/>. It does not renew, displace, reorder or
        /// otherwise mutate authority state.
        /// </summary>
        /// <remarks>NoAlloc.</remarks>
        public bool CanAcquire(
            int sourceEntityId,
            int targetEntityId,
            int resolvedActionRevision,
            RuntimeFrame frame,
            in CharacterTestEngagementCandidateResult selection)
        {
            if (sourceEntityId <= 0
                || targetEntityId <= 0
                || resolvedActionRevision <= 0
                || !selection.IsSelectable)
            {
                return false;
            }

            int existingIndex = FindSource(sourceEntityId);
            if (existingIndex >= 0)
            {
                CharacterTestEngagementReservation existing = _entries[existingIndex];
                if (SameKey(
                        existing,
                        selection.Pose.TargetEntityId,
                        selection.Pose.ZoneId,
                        selection.Pose.AnchorId)
                    && existing.ResolvedActionRevision == resolvedActionRevision)
                {
                    return true;
                }
            }

            int occupied = 0;
            int sameFrameWorstSource = int.MinValue;
            for (int i = 0; i < _count; i++)
            {
                CharacterTestEngagementReservation entry = _entries[i];
                if (entry.SourceEntityId == sourceEntityId
                    || !SameKey(
                        entry,
                        selection.Pose.TargetEntityId,
                        selection.Pose.ZoneId,
                        selection.Pose.AnchorId))
                {
                    continue;
                }
                occupied++;
                if (entry.AcquiredFrame == frame
                    && entry.SourceEntityId > sameFrameWorstSource)
                {
                    sameFrameWorstSource = entry.SourceEntityId;
                }
            }

            if (occupied >= selection.Capacity)
                return sameFrameWorstSource > sourceEntityId;
            return existingIndex >= 0 || _count < _entries.Length;
        }

        /// <summary>
        /// Acquires or renews one slot. New same-frame conflicts displace only the numerically
        /// greatest owner, making the winner independent of evaluation order.
        /// </summary>
        /// <remarks>NoAlloc.</remarks>
        public bool TryAcquire(
            int sourceEntityId,
            int targetEntityId,
            int resolvedActionRevision,
            RuntimeFrame frame,
            in CharacterTestEngagementCandidateResult selection,
            out CharacterTestEngagementReservation reservation)
        {
            reservation = default;
            if (sourceEntityId <= 0
                || targetEntityId <= 0
                || resolvedActionRevision <= 0
                || !selection.IsSelectable)
            {
                return false;
            }

            int existingIndex = FindSource(sourceEntityId);
            if (existingIndex >= 0)
            {
                CharacterTestEngagementReservation existing = _entries[existingIndex];
                if (SameKey(
                        existing,
                        selection.Pose.TargetEntityId,
                        selection.Pose.ZoneId,
                        selection.Pose.AnchorId)
                    && existing.ResolvedActionRevision == resolvedActionRevision)
                {
                    _entries[existingIndex] = existing.WithUpdate(frame, selection);
                    RecomputeSlots(
                        selection.Pose.TargetEntityId,
                        selection.Pose.ZoneId,
                        selection.Pose.AnchorId);
                    reservation = _entries[FindSource(sourceEntityId)];
                    IncrementRevision();
                    return true;
                }
                RemoveAt(existingIndex);
            }

            int occupied = 0;
            int sameFrameWorstIndex = -1;
            int sameFrameWorstSource = int.MinValue;
            for (int i = 0; i < _count; i++)
            {
                CharacterTestEngagementReservation entry = _entries[i];
                if (!SameKey(
                        entry,
                        selection.Pose.TargetEntityId,
                        selection.Pose.ZoneId,
                        selection.Pose.AnchorId))
                {
                    continue;
                }
                occupied++;
                if (entry.AcquiredFrame == frame
                    && entry.SourceEntityId > sameFrameWorstSource)
                {
                    sameFrameWorstSource = entry.SourceEntityId;
                    sameFrameWorstIndex = i;
                }
            }

            if (occupied >= selection.Capacity)
            {
                if (sameFrameWorstIndex < 0 || sourceEntityId >= sameFrameWorstSource)
                    return false;
                RemoveAt(sameFrameWorstIndex);
            }
            if (_count >= _entries.Length)
                return false;

            _entries[_count++] = new CharacterTestEngagementReservation(
                sourceEntityId,
                targetEntityId,
                selection.Pose.TargetEntityId,
                resolvedActionRevision,
                selection.Pose.ZoneId,
                selection.Pose.AnchorId,
                selection.Capacity,
                capacitySlot: 0,
                frame,
                frame,
                selection.Pose.ZoneBindingRevision,
                selection.Pose.ZoneRevision,
                selection.Pose.AnchorRevision,
                selection.Pose.AnchorContentHash,
                selection.Pose.PhaseId,
                selection.Pose.PhaseRevision,
                selection.Pose.CombatPhysicsRevision,
                selection.Pose.GraphRevision,
                selection.Pose.GraphContentHash);
            RecomputeSlots(
                selection.Pose.TargetEntityId,
                selection.Pose.ZoneId,
                selection.Pose.AnchorId);
            reservation = _entries[FindSource(sourceEntityId)];
            IncrementRevision();
            return true;
        }

        /// <remarks>NoAlloc. Explicit release is idempotent.</remarks>
        public bool Release(
            int sourceEntityId,
            CharacterTestEngagementReservationReleaseReason reason)
        {
            int index = FindSource(sourceEntityId);
            if (index < 0)
                return false;
            CharacterTestEngagementReservation removed = _entries[index];
            RemoveAt(index);
            RecomputeSlots(removed.TargetCombatEntityId, removed.ZoneId, removed.AnchorId);
            IncrementRevision();
            return true;
        }

        /// <summary>Releases dead/missing/stale authority before the frame evaluates candidates.</summary>
        /// <remarks>NoAlloc.</remarks>
        public void SweepInvalid(
            CharacterTestCharacterRuntimeModule runtime,
            CharacterTestNavigationSession navigation,
            RuntimeFrame frame)
        {
            bool changed = false;
            for (int i = _count - 1; i >= 0; i--)
            {
                CharacterTestEngagementReservation entry = _entries[i];
                if (IsCurrent(runtime, navigation, frame, entry))
                    continue;
                RemoveAt(i);
                changed = true;
            }
            RecomputeAllSlots();
            if (changed)
                IncrementRevision();
        }

        /// <remarks>LowFreqAlloc.</remarks>
        private static readonly Comparison<CharacterTestEngagementReservationStateSnapshot> CaptureComparison = CharacterTestEngagementReservationStateSnapshotComparer.Instance.Compare;

        internal void CaptureInto(CharacterTestEngagementReservationStateSnapshot[] result)
        {
            for (int i = 0; i < _count; i++)
                CharacterTestEngagementReservationStateSnapshot.CopyFrom(_entries[i], result[i]);
            Array.Sort(result, CaptureComparison);
        }

        public CharacterTestEngagementReservationStateSnapshot[] Capture()
        {
            if (_count == 0)
                return Array.Empty<CharacterTestEngagementReservationStateSnapshot>();
            var result = new CharacterTestEngagementReservationStateSnapshot[_count];
            for (int i = 0; i < _count; i++)
                result[i] = CharacterTestEngagementReservationStateSnapshot.From(_entries[i]);
            Array.Sort(result, CaptureComparison);
            return result;
        }

        /// <remarks>LowFreqAlloc.</remarks>
        public bool TryRestore(
            CharacterTestEngagementReservationStateSnapshot[] snapshots,
            out string failure)
        {
            Clear();
            failure = string.Empty;
            snapshots ??= Array.Empty<CharacterTestEngagementReservationStateSnapshot>();
            if (snapshots.Length > _entries.Length)
            {
                failure = "Runtime AI Planner engagement reservation capacity exceeded.";
                return false;
            }
            for (int i = 0; i < snapshots.Length; i++)
            {
                CharacterTestEngagementReservationStateSnapshot value = snapshots[i];
                if (value == null
                    || !value.TryCreate(out CharacterTestEngagementReservation entry)
                    || FindSource(entry.SourceEntityId) >= 0)
                {
                    Clear();
                    failure = "Runtime AI Planner engagement reservation snapshot is invalid.";
                    return false;
                }
                _entries[_count++] = entry;
            }
            RecomputeAllSlots();
            IncrementRevision();
            return true;
        }

        private void IncrementRevision()
        {
            Revision = Revision == int.MaxValue ? 1 : Revision + 1;
        }

        private static bool IsCurrent(
            CharacterTestCharacterRuntimeModule runtime,
            CharacterTestNavigationSession navigation,
            RuntimeFrame frame,
            in CharacterTestEngagementReservation entry)
        {
            if (runtime == null
                || navigation == null
                || !navigation.IsAvailable
                || frame.Value - entry.UpdatedFrame.Value > 1
                || navigation.Graph.Revision != entry.GraphRevision
                || navigation.Graph.ContentHash != entry.GraphContentHash
                || runtime.PhysicsRuntime?.World == null
                || runtime.PhysicsRuntime.World.Revision < entry.CombatPhysicsRevision)
            {
                return false;
            }
            CharacterTestCharacterEntity source = runtime.FindEntityById(entry.SourceEntityId);
            CharacterTestCharacterEntity target = runtime.FindEntityById(entry.TargetEntityId);
            if (source == null
                || target == null
                || !source.Presence.IsActive
                || source.Control.Owner != CharacterTestCharacterControlOwner.AiControlled
                || !target.Presence.IsActive
                || !target.BattleState.CountsAsAlive)
            {
                return false;
            }
            if (!runtime.TryGetDamageZoneSnapshot(
                    target.Id.Value,
                    out CharacterTestDamageZoneSnapshot zones)
                || zones.BindingRevision != entry.ZoneBindingRevision
                || !zones.TryGetAvailableZone(entry.ZoneId, out CharacterTestDamageZoneAggregate zone)
                || !zone.Approachable)
            {
                return false;
            }
            if (!runtime.TryGetRuntimeGeometryBinding(
                    target.Id.Value,
                    out CharacterImportedGeometryBinding geometry)
                || geometry.EngagementAnchorRevision != entry.AnchorRevision
                || !geometry.EngagementAnchorContentDigest.Equals(entry.AnchorContentHash))
            {
                return false;
            }
            CharacterEngagementAnchorRuntimeBinding anchor = FindAnchor(
                geometry.EngagementAnchors,
                entry.AnchorId);
            return anchor != null
                && anchor.Enabled
                && anchor.Capacity == entry.Capacity
                && (anchor.ZoneId == entry.ZoneId)
                && ((anchor.PhaseId <= 0)
                    || (anchor.PhaseId == entry.PhaseId));
        }

        private static CharacterEngagementAnchorRuntimeBinding FindAnchor(
            CharacterEngagementAnchorRuntimeBinding[] anchors,
            int anchorId)
        {
            for (int i = 0; anchors != null && i < anchors.Length; i++)
            {
                if ((anchors[i].AnchorId == anchorId))
                    return anchors[i];
            }
            return null;
        }

        private int FindSource(int sourceEntityId)
        {
            for (int i = 0; i < _count; i++)
            {
                if (_entries[i].SourceEntityId == sourceEntityId)
                    return i;
            }
            return -1;
        }

        private void RemoveAt(int index)
        {
            int last = _count - 1;
            if (index != last)
                _entries[index] = _entries[last];
            _entries[last] = default;
            _count = last;
        }

        private void RecomputeAllSlots()
        {
            for (int i = 0; i < _count; i++)
            {
                CharacterTestEngagementReservation entry = _entries[i];
                RecomputeSlots(entry.TargetCombatEntityId, entry.ZoneId, entry.AnchorId);
            }
        }

        private void RecomputeSlots(CombatEntityId targetEntityId, int zoneId, int anchorId)
        {
            for (int i = 0; i < _count; i++)
            {
                CharacterTestEngagementReservation current = _entries[i];
                if (!SameKey(current, targetEntityId, zoneId, anchorId))
                    continue;
                int slot = 0;
                for (int j = 0; j < _count; j++)
                {
                    CharacterTestEngagementReservation other = _entries[j];
                    if (SameKey(other, targetEntityId, zoneId, anchorId)
                        && other.SourceEntityId < current.SourceEntityId)
                    {
                        slot++;
                    }
                }
                _entries[i] = current.WithCapacitySlot(slot);
            }
        }

        private static bool SameKey(
            in CharacterTestEngagementReservation entry,
            CombatEntityId targetEntityId,
            int zoneId,
            int anchorId)
        {
            return entry.TargetCombatEntityId.Equals(targetEntityId)
                && (entry.ZoneId == zoneId)
                && (entry.AnchorId == anchorId);
        }
    }

    internal sealed class CharacterTestEngagementReservationStateSnapshotComparer :
        System.Collections.Generic.IComparer<CharacterTestEngagementReservationStateSnapshot>
    {
        public static readonly CharacterTestEngagementReservationStateSnapshotComparer Instance =
            new CharacterTestEngagementReservationStateSnapshotComparer();

        public int Compare(
            CharacterTestEngagementReservationStateSnapshot left,
            CharacterTestEngagementReservationStateSnapshot right)
        {
            if (ReferenceEquals(left, right)) return 0;
            if (left == null) return -1;
            if (right == null) return 1;
            int value = left.TargetCombatEntityId.CompareTo(right.TargetCombatEntityId);
            if (value != 0) return value;
            value = left.ZoneId.CompareTo(right.ZoneId);
            if (value != 0) return value;
            value = left.AnchorId.CompareTo(right.AnchorId);
            if (value != 0) return value;
            return left.SourceEntityId.CompareTo(right.SourceEntityId);
        }
    }
}
