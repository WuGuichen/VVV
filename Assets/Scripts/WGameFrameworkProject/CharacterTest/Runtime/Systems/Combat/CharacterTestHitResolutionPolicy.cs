using System;
using System.Collections.Generic;
using MxFramework.CharacterAction;
using MxFramework.Combat.Animation;
using MxFramework.Combat.Core;
using MxFramework.Combat.Damage;
using MxFramework.Combat.Hit;
using MxFramework.Combat.Physics;
using MxFramework.Runtime;

namespace MxFramework.Game.CharacterTest
{
    /// <summary>
    /// Stable identity of one authored hit-event policy in a direct-hit window.
    /// Physical TraceId deliberately does not participate in this identity.
    /// </summary>
    internal readonly struct CharacterTestHitPolicyEventKey : IEquatable<CharacterTestHitPolicyEventKey>
    {
        internal CharacterTestHitPolicyEventKey(
            CombatEntityId attackerId,
            int actionInstanceId,
            int policyEventId)
        {
            AttackerId = attackerId;
            ActionInstanceId = actionInstanceId < 0 ? 0 : actionInstanceId;
            PolicyEventId = policyEventId <= 0 ? 1 : policyEventId;
        }

        internal CombatEntityId AttackerId { get; }
        internal int ActionInstanceId { get; }
        internal int PolicyEventId { get; }

        public bool Equals(CharacterTestHitPolicyEventKey other)
        {
            return AttackerId.Equals(other.AttackerId)
                && ActionInstanceId == other.ActionInstanceId
                && PolicyEventId == other.PolicyEventId;
        }

        public override bool Equals(object obj)
        {
            return obj is CharacterTestHitPolicyEventKey other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = AttackerId.Value;
                hash = (hash * 397) ^ ActionInstanceId;
                return (hash * 397) ^ PolicyEventId;
            }
        }
    }

    internal readonly struct CharacterTestHitPolicyEventTargetKey :
        IEquatable<CharacterTestHitPolicyEventTargetKey>
    {
        internal CharacterTestHitPolicyEventTargetKey(
            CharacterTestHitPolicyEventKey eventKey,
            CombatEntityId targetId)
        {
            EventKey = eventKey;
            TargetId = targetId;
        }

        internal CharacterTestHitPolicyEventKey EventKey { get; }
        internal CombatEntityId TargetId { get; }

        public bool Equals(CharacterTestHitPolicyEventTargetKey other)
        {
            return EventKey.Equals(other.EventKey) && TargetId.Equals(other.TargetId);
        }

        public override bool Equals(object obj)
        {
            return obj is CharacterTestHitPolicyEventTargetKey other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return (EventKey.GetHashCode() * 397) ^ TargetId.Value;
            }
        }
    }

    internal readonly struct CharacterTestHitPolicyAcceptedEvent
    {
        internal CharacterTestHitPolicyAcceptedEvent(
            CharacterTestHitPolicyEventKey key,
            int acceptedCount)
        {
            Key = key;
            AcceptedCount = acceptedCount;
        }

        internal CharacterTestHitPolicyEventKey Key { get; }
        internal int AcceptedCount { get; }
    }

    internal readonly struct CharacterTestHitPolicyAcceptedTarget
    {
        internal CharacterTestHitPolicyAcceptedTarget(
            CharacterTestHitPolicyEventTargetKey key)
        {
            Key = key;
        }

        internal CharacterTestHitPolicyEventTargetKey Key { get; }
    }

    /// <summary>
    /// CharacterTest-owned whole direct-hit window network snapshot. The legacy
    /// type name is kept for schema-24 JSON compatibility; the policy ledger,
    /// active traces, action instance mapping, id generators, and hit-once keys
    /// are one atomic state. All emitted arrays use stable key order and remain
    /// intentionally outside the framework-level Network assembly.
    /// </summary>
    public sealed class CharacterTestHitResolutionPolicyNetworkSnapshot
    {
        public CharacterTestHitPolicyEventNetworkSnapshot[] AcceptedEvents { get; set; } =
            Array.Empty<CharacterTestHitPolicyEventNetworkSnapshot>();
        public CharacterTestPolicyTargetNetworkSnapshot[] AcceptedTargets { get; set; } =
            Array.Empty<CharacterTestPolicyTargetNetworkSnapshot>();
        public CharacterTestHitTraceNetworkSnapshot[] ActiveTraces { get; set; } =
            Array.Empty<CharacterTestHitTraceNetworkSnapshot>();
        public CharacterTestHitTraceInstanceNetworkSnapshot[] InstanceIds { get; set; } =
            Array.Empty<CharacterTestHitTraceInstanceNetworkSnapshot>();
        public CharacterTestHitOnceKeyNetworkSnapshot[] HitOnceKeys { get; set; } =
            Array.Empty<CharacterTestHitOnceKeyNetworkSnapshot>();
        public CharacterTestBodyHitVolumePreviousCenterNetworkSnapshot[] BodyHitVolumePreviousCenters { get; set; } =
            Array.Empty<CharacterTestBodyHitVolumePreviousCenterNetworkSnapshot>();
        public int NextInstanceId { get; set; } = 1;
        public int NextHitWindowId { get; set; } = 1;

        public CharacterTestHitResolutionPolicyNetworkSnapshot CloneForRollback()
        {
            return new CharacterTestHitResolutionPolicyNetworkSnapshot
            {
                AcceptedEvents = CloneEvents(AcceptedEvents),
                AcceptedTargets = CloneTargets(AcceptedTargets),
                ActiveTraces = CharacterTestHitTraceNetworkSnapshot.CloneArray(ActiveTraces),
                InstanceIds = CharacterTestHitTraceInstanceNetworkSnapshot.CloneArray(InstanceIds),
                HitOnceKeys = CharacterTestHitOnceKeyNetworkSnapshot.CloneArray(HitOnceKeys),
                BodyHitVolumePreviousCenters = CharacterTestBodyHitVolumePreviousCenterNetworkSnapshot.CloneArray(
                    BodyHitVolumePreviousCenters),
                NextInstanceId = NextInstanceId,
                NextHitWindowId = NextHitWindowId
            };
        }

        internal bool IsEmpty =>
            (AcceptedEvents == null || AcceptedEvents.Length == 0)
            && (AcceptedTargets == null || AcceptedTargets.Length == 0)
            && (ActiveTraces == null || ActiveTraces.Length == 0)
            && (InstanceIds == null || InstanceIds.Length == 0)
            && (HitOnceKeys == null || HitOnceKeys.Length == 0)
            && (BodyHitVolumePreviousCenters == null || BodyHitVolumePreviousCenters.Length == 0)
            && NextInstanceId <= 1
            && NextHitWindowId <= 1;

        private static CharacterTestHitPolicyEventNetworkSnapshot[] CloneEvents(
            CharacterTestHitPolicyEventNetworkSnapshot[] source)
        {
            if (source == null || source.Length == 0)
                return Array.Empty<CharacterTestHitPolicyEventNetworkSnapshot>();

            var clone = new CharacterTestHitPolicyEventNetworkSnapshot[source.Length];
            for (int i = 0; i < source.Length; i++)
            {
                CharacterTestHitPolicyEventNetworkSnapshot entry = source[i];
                clone[i] = entry == null
                    ? null
                    : new CharacterTestHitPolicyEventNetworkSnapshot
                    {
                        AttackerEntityId = entry.AttackerEntityId,
                        ActionInstanceId = entry.ActionInstanceId,
                        PolicyEventId = entry.PolicyEventId,
                        AcceptedCount = entry.AcceptedCount
                    };
            }
            return clone;
        }

        private static CharacterTestPolicyTargetNetworkSnapshot[] CloneTargets(
            CharacterTestPolicyTargetNetworkSnapshot[] source)
        {
            if (source == null || source.Length == 0)
                return Array.Empty<CharacterTestPolicyTargetNetworkSnapshot>();

            var clone = new CharacterTestPolicyTargetNetworkSnapshot[source.Length];
            for (int i = 0; i < source.Length; i++)
            {
                CharacterTestPolicyTargetNetworkSnapshot entry = source[i];
                clone[i] = entry == null
                    ? null
                    : new CharacterTestPolicyTargetNetworkSnapshot
                    {
                        AttackerEntityId = entry.AttackerEntityId,
                        ActionInstanceId = entry.ActionInstanceId,
                        PolicyEventId = entry.PolicyEventId,
                        TargetEntityId = entry.TargetEntityId
                    };
            }
            return clone;
        }
    }

    public sealed class CharacterTestHitPolicyEventNetworkSnapshot
    {
        public int AttackerEntityId { get; set; }
        public int ActionInstanceId { get; set; }
        public int PolicyEventId { get; set; }
        public int AcceptedCount { get; set; }
    }

    public sealed class CharacterTestPolicyTargetNetworkSnapshot
    {
        public int AttackerEntityId { get; set; }
        public int ActionInstanceId { get; set; }
        public int PolicyEventId { get; set; }
        public int TargetEntityId { get; set; }
    }

    public sealed class CharacterTestHitTraceInstanceNetworkSnapshot
    {
        public long SourceInstanceId { get; set; }
        public int ActionInstanceId { get; set; }

        internal static CharacterTestHitTraceInstanceNetworkSnapshot[] CloneArray(
            CharacterTestHitTraceInstanceNetworkSnapshot[] source)
        {
            if (source == null || source.Length == 0)
                return Array.Empty<CharacterTestHitTraceInstanceNetworkSnapshot>();

            var clone = new CharacterTestHitTraceInstanceNetworkSnapshot[source.Length];
            for (int i = 0; i < source.Length; i++)
            {
                CharacterTestHitTraceInstanceNetworkSnapshot entry = source[i];
                clone[i] = entry == null
                    ? null
                    : new CharacterTestHitTraceInstanceNetworkSnapshot
                    {
                        SourceInstanceId = entry.SourceInstanceId,
                        ActionInstanceId = entry.ActionInstanceId
                    };
            }
            return clone;
        }
    }

    public sealed class CharacterTestHitOnceKeyNetworkSnapshot
    {
        public int ActionInstanceId { get; set; }
        public int TraceId { get; set; }
        public int TargetEntityId { get; set; }

        internal static CharacterTestHitOnceKeyNetworkSnapshot[] CloneArray(
            CharacterTestHitOnceKeyNetworkSnapshot[] source)
        {
            if (source == null || source.Length == 0)
                return Array.Empty<CharacterTestHitOnceKeyNetworkSnapshot>();

            var clone = new CharacterTestHitOnceKeyNetworkSnapshot[source.Length];
            for (int i = 0; i < source.Length; i++)
            {
                CharacterTestHitOnceKeyNetworkSnapshot entry = source[i];
                clone[i] = entry == null
                    ? null
                    : new CharacterTestHitOnceKeyNetworkSnapshot
                    {
                        ActionInstanceId = entry.ActionInstanceId,
                        TraceId = entry.TraceId,
                        TargetEntityId = entry.TargetEntityId
                    };
            }
            return clone;
        }
    }

    public sealed class CharacterTestBodyHitVolumePreviousCenterNetworkSnapshot
    {
        public int EntityId { get; set; }
        public int HitWindowId { get; set; }
        public long CenterXRaw { get; set; }
        public long CenterYRaw { get; set; }
        public long CenterZRaw { get; set; }

        internal static CharacterTestBodyHitVolumePreviousCenterNetworkSnapshot[] CloneArray(
            CharacterTestBodyHitVolumePreviousCenterNetworkSnapshot[] source)
        {
            if (source == null || source.Length == 0)
                return Array.Empty<CharacterTestBodyHitVolumePreviousCenterNetworkSnapshot>();

            var clone = new CharacterTestBodyHitVolumePreviousCenterNetworkSnapshot[source.Length];
            for (int i = 0; i < source.Length; i++)
            {
                CharacterTestBodyHitVolumePreviousCenterNetworkSnapshot entry = source[i];
                clone[i] = entry == null
                    ? null
                    : new CharacterTestBodyHitVolumePreviousCenterNetworkSnapshot
                    {
                        EntityId = entry.EntityId,
                        HitWindowId = entry.HitWindowId,
                        CenterXRaw = entry.CenterXRaw,
                        CenterYRaw = entry.CenterYRaw,
                        CenterZRaw = entry.CenterZRaw
                    };
            }
            return clone;
        }
    }

    public sealed class CharacterTestHitTraceNetworkSnapshot
    {
        public int EntityId { get; set; }
        public CharacterActionId ActionId { get; set; }
        public long SourceInstanceId { get; set; }
        public int SourceActionInstanceId { get; set; }
        public int HitWindowId { get; set; }
        public int EquipmentSlotId { get; set; }
        public int TraceProfileId { get; set; }
        public CharacterTestDamagePayloadNetworkSnapshot DamagePayload { get; set; }
        public CharacterTestHitEventNetworkSnapshot HitEvent { get; set; }
        public CharacterTestHitVolumeNetworkSnapshot HitVolume { get; set; }
        public int StableEventId { get; set; }
        public int StartFrame { get; set; }

        internal static CharacterTestHitTraceNetworkSnapshot[] CloneArray(
            CharacterTestHitTraceNetworkSnapshot[] source)
        {
            if (source == null || source.Length == 0)
                return Array.Empty<CharacterTestHitTraceNetworkSnapshot>();

            var clone = new CharacterTestHitTraceNetworkSnapshot[source.Length];
            for (int i = 0; i < source.Length; i++)
            {
                CharacterTestHitTraceNetworkSnapshot entry = source[i];
                clone[i] = entry == null
                    ? null
                    : new CharacterTestHitTraceNetworkSnapshot
                    {
                        EntityId = entry.EntityId,
                        ActionId = entry.ActionId,
                        SourceInstanceId = entry.SourceInstanceId,
                        SourceActionInstanceId = entry.SourceActionInstanceId,
                        HitWindowId = entry.HitWindowId,
                        EquipmentSlotId = entry.EquipmentSlotId,
                        TraceProfileId = entry.TraceProfileId,
                        DamagePayload = entry.DamagePayload?.CloneForRollback(),
                        HitEvent = entry.HitEvent?.CloneForRollback(),
                        HitVolume = entry.HitVolume?.CloneForRollback(),
                        StableEventId = entry.StableEventId,
                        StartFrame = entry.StartFrame
                    };
            }
            return clone;
        }
    }

    public sealed class CharacterTestDamagePayloadNetworkSnapshot
    {
        public int DamageTypeId { get; set; }
        public int FormulaId { get; set; }
        public int BaseDamage { get; set; }
        public DamageAttributeScaleNetworkSnapshot[] SourceAttributeScales { get; set; }
        public int ImpactPower { get; set; }
        public int LaunchPower { get; set; }
        public int[] Tags { get; set; }
        public int AnalysisCategory { get; set; }
        public int PresentationProfileId { get; set; }

        internal CharacterTestDamagePayloadNetworkSnapshot CloneForRollback()
        {
            var clone = new CharacterTestDamagePayloadNetworkSnapshot
            {
                DamageTypeId = DamageTypeId,
                FormulaId = FormulaId,
                BaseDamage = BaseDamage,
                ImpactPower = ImpactPower,
                LaunchPower = LaunchPower,
                AnalysisCategory = AnalysisCategory,
                PresentationProfileId = PresentationProfileId,
                Tags = Tags == null ? Array.Empty<int>() : (int[])Tags.Clone()
            };
            if (SourceAttributeScales == null || SourceAttributeScales.Length == 0)
            {
                clone.SourceAttributeScales = Array.Empty<DamageAttributeScaleNetworkSnapshot>();
            }
            else
            {
                clone.SourceAttributeScales = new DamageAttributeScaleNetworkSnapshot[SourceAttributeScales.Length];
                for (int i = 0; i < SourceAttributeScales.Length; i++)
                    clone.SourceAttributeScales[i] = SourceAttributeScales[i]?.CloneForRollback();
            }
            return clone;
        }
    }

    public sealed class DamageAttributeScaleNetworkSnapshot
    {
        public int AttributeId { get; set; }
        public float Scale { get; set; }

        internal DamageAttributeScaleNetworkSnapshot CloneForRollback()
        {
            return new DamageAttributeScaleNetworkSnapshot
            {
                AttributeId = AttributeId,
                Scale = Scale
            };
        }
    }

    public sealed class CharacterTestHitEventNetworkSnapshot
    {
        public int TargetTypes { get; set; }
        public int EnvironmentHitPolicy { get; set; }
        public int StableEventId { get; set; }
        public int MaxAcceptedHits { get; set; }
        public int HitSameTargetPolicy { get; set; }
        public CharacterTestHitDamageNetworkSnapshot Damage { get; set; }
        public int[] OnHitBuffIds { get; set; }

        internal CharacterTestHitEventNetworkSnapshot CloneForRollback()
        {
            return new CharacterTestHitEventNetworkSnapshot
            {
                TargetTypes = TargetTypes,
                EnvironmentHitPolicy = EnvironmentHitPolicy,
                StableEventId = StableEventId,
                MaxAcceptedHits = MaxAcceptedHits,
                HitSameTargetPolicy = HitSameTargetPolicy,
                Damage = Damage?.CloneForRollback(),
                OnHitBuffIds = OnHitBuffIds == null ? Array.Empty<int>() : (int[])OnHitBuffIds.Clone()
            };
        }
    }

    public sealed class CharacterTestHitDamageNetworkSnapshot
    {
        public int DamageTypeId { get; set; }
        public int BaseDamage { get; set; }
        public int ImpactPower { get; set; }
        public int LaunchPower { get; set; }
        public int[] Tags { get; set; }
        public int AnalysisCategory { get; set; }

        internal CharacterTestHitDamageNetworkSnapshot CloneForRollback()
        {
            return new CharacterTestHitDamageNetworkSnapshot
            {
                DamageTypeId = DamageTypeId,
                BaseDamage = BaseDamage,
                ImpactPower = ImpactPower,
                LaunchPower = LaunchPower,
                Tags = Tags == null ? Array.Empty<int>() : (int[])Tags.Clone(),
                AnalysisCategory = AnalysisCategory
            };
        }
    }

    public sealed class CharacterTestHitVolumeNetworkSnapshot
    {
        public int SourceKind { get; set; }
        public int ShapeKind { get; set; }
        public float LocalPointAX { get; set; }
        public float LocalPointAY { get; set; }
        public float LocalPointAZ { get; set; }
        public float LocalPointBX { get; set; }
        public float LocalPointBY { get; set; }
        public float LocalPointBZ { get; set; }
        public float Radius { get; set; }
        public float BodyOffsetX { get; set; }
        public float BodyOffsetY { get; set; }
        public float BodyOffsetZ { get; set; }
        public float BodyRadiusScale { get; set; }
        public float BodyRadiusAdd { get; set; }
        public float BodyHeightScale { get; set; }
        public float BodyHeightAdd { get; set; }
        public int StartSocketId { get; set; }
        public int EndSocketId { get; set; }

        internal CharacterTestHitVolumeNetworkSnapshot CloneForRollback()
        {
            return new CharacterTestHitVolumeNetworkSnapshot
            {
                SourceKind = SourceKind,
                ShapeKind = ShapeKind,
                LocalPointAX = LocalPointAX,
                LocalPointAY = LocalPointAY,
                LocalPointAZ = LocalPointAZ,
                LocalPointBX = LocalPointBX,
                LocalPointBY = LocalPointBY,
                LocalPointBZ = LocalPointBZ,
                Radius = Radius,
                BodyOffsetX = BodyOffsetX,
                BodyOffsetY = BodyOffsetY,
                BodyOffsetZ = BodyOffsetZ,
                BodyRadiusScale = BodyRadiusScale,
                BodyRadiusAdd = BodyRadiusAdd,
                BodyHeightScale = BodyHeightScale,
                BodyHeightAdd = BodyHeightAdd,
                StartSocketId = StartSocketId,
                EndSocketId = EndSocketId
            };
        }
    }

    internal sealed class CharacterTestHitResolutionPolicyStateSnapshot
    {
        internal CharacterTestHitResolutionPolicyStateSnapshot(
            CharacterTestHitPolicyAcceptedEvent[] acceptedEvents,
            CharacterTestHitPolicyAcceptedTarget[] acceptedTargets)
        {
            AcceptedEvents = acceptedEvents ?? Array.Empty<CharacterTestHitPolicyAcceptedEvent>();
            AcceptedTargets = acceptedTargets ?? Array.Empty<CharacterTestHitPolicyAcceptedTarget>();
        }

        internal CharacterTestHitPolicyAcceptedEvent[] AcceptedEvents { get; }
        internal CharacterTestHitPolicyAcceptedTarget[] AcceptedTargets { get; }
    }

    /// <summary>
    /// Runtime ledger shared by actual direct-hit resolution and PreviewTrace.
    /// The ledger is logically part of a hit window and therefore survives
    /// ticks, CharacterTest network snapshots/rollback, and the existing
    /// hit-window replay capture/restore boundary.
    /// </summary>
    internal sealed class CharacterTestHitResolutionPolicyState
    {
        private static readonly Comparison<CharacterTestHitPolicyAcceptedEvent> CompareAcceptedEventsCached = CompareAcceptedEvents;
        private static readonly Comparison<CharacterTestHitPolicyAcceptedTarget> CompareAcceptedTargetsCached = CompareAcceptedTargets;

        private readonly Dictionary<CharacterTestHitPolicyEventKey, int> _acceptedCounts =
            new Dictionary<CharacterTestHitPolicyEventKey, int>(32);
        private readonly HashSet<CharacterTestHitPolicyEventTargetKey> _acceptedTargets =
            new HashSet<CharacterTestHitPolicyEventTargetKey>();
        private readonly List<CharacterTestHitPolicyEventKey> _eventRemovalScratch =
            new List<CharacterTestHitPolicyEventKey>(16);
        private readonly List<CharacterTestHitPolicyEventTargetKey> _targetRemovalScratch =
            new List<CharacterTestHitPolicyEventTargetKey>(16);
        private readonly List<CharacterTestHitPolicyAcceptedEvent> _acceptedEventHashScratch =
            new List<CharacterTestHitPolicyAcceptedEvent>(32);
        private readonly List<CharacterTestHitPolicyAcceptedTarget> _acceptedTargetHashScratch =
            new List<CharacterTestHitPolicyAcceptedTarget>(32);

        internal bool TryAccept(
            HitCandidate candidate,
            int policyEventId,
            CombatHitEventConfig config)
        {
            CharacterTestHitPolicyEventKey eventKey = new CharacterTestHitPolicyEventKey(
                candidate.AttackerId,
                candidate.ActionInstanceId,
                policyEventId);
            int maxAcceptedHits = config.MaxAcceptedHits <= 0 ? 1 : config.MaxAcceptedHits;
            _acceptedCounts.TryGetValue(eventKey, out int acceptedCount);
            if (acceptedCount >= maxAcceptedHits)
                return false;

            CharacterTestHitPolicyEventTargetKey targetKey =
                new CharacterTestHitPolicyEventTargetKey(eventKey, candidate.TargetId);
            if (config.HitSameTargetPolicy == CombatHitSameTargetPolicy.OncePerEvent
                && _acceptedTargets.Contains(targetKey))
            {
                return false;
            }

            _acceptedCounts[eventKey] = acceptedCount + 1;
            if (config.HitSameTargetPolicy == CombatHitSameTargetPolicy.OncePerEvent)
                _acceptedTargets.Add(targetKey);
            return true;
        }

        internal void Clear()
        {
            _acceptedCounts.Clear();
            _acceptedTargets.Clear();
            _eventRemovalScratch.Clear();
            _targetRemovalScratch.Clear();
        }

        internal void ClearActionInstance(int actionInstanceId)
        {
            if (actionInstanceId <= 0)
                return;

            _eventRemovalScratch.Clear();
            foreach (KeyValuePair<CharacterTestHitPolicyEventKey, int> pair in _acceptedCounts)
            {
                if (pair.Key.ActionInstanceId == actionInstanceId)
                    _eventRemovalScratch.Add(pair.Key);
            }
            for (int i = 0; i < _eventRemovalScratch.Count; i++)
                _acceptedCounts.Remove(_eventRemovalScratch[i]);

            _targetRemovalScratch.Clear();
            foreach (CharacterTestHitPolicyEventTargetKey key in _acceptedTargets)
            {
                if (key.EventKey.ActionInstanceId == actionInstanceId)
                    _targetRemovalScratch.Add(key);
            }
            for (int i = 0; i < _targetRemovalScratch.Count; i++)
                _acceptedTargets.Remove(_targetRemovalScratch[i]);
        }

        internal CharacterTestHitResolutionPolicyStateSnapshot CaptureSnapshot()
        {
            var acceptedEvents = new CharacterTestHitPolicyAcceptedEvent[_acceptedCounts.Count];
            int eventIndex = 0;
            foreach (KeyValuePair<CharacterTestHitPolicyEventKey, int> pair in _acceptedCounts)
            {
                acceptedEvents[eventIndex++] = new CharacterTestHitPolicyAcceptedEvent(
                    pair.Key,
                    pair.Value);
            }

            var acceptedTargets = new CharacterTestHitPolicyAcceptedTarget[_acceptedTargets.Count];
            int targetIndex = 0;
            foreach (CharacterTestHitPolicyEventTargetKey key in _acceptedTargets)
                acceptedTargets[targetIndex++] = new CharacterTestHitPolicyAcceptedTarget(key);

            Array.Sort(acceptedEvents, CompareAcceptedEvents);
            Array.Sort(acceptedTargets, CompareAcceptedTargets);

            return new CharacterTestHitResolutionPolicyStateSnapshot(acceptedEvents, acceptedTargets);
        }

        internal CharacterTestHitResolutionPolicyNetworkSnapshot CaptureNetworkSnapshot()
        {
            CharacterTestHitResolutionPolicyStateSnapshot snapshot = CaptureSnapshot();
            var acceptedEvents = new CharacterTestHitPolicyEventNetworkSnapshot[snapshot.AcceptedEvents.Length];
            for (int i = 0; i < snapshot.AcceptedEvents.Length; i++)
            {
                CharacterTestHitPolicyEventKey key = snapshot.AcceptedEvents[i].Key;
                acceptedEvents[i] = new CharacterTestHitPolicyEventNetworkSnapshot
                {
                    AttackerEntityId = key.AttackerId.Value,
                    ActionInstanceId = key.ActionInstanceId,
                    PolicyEventId = key.PolicyEventId,
                    AcceptedCount = snapshot.AcceptedEvents[i].AcceptedCount
                };
            }

            var acceptedTargets = new CharacterTestPolicyTargetNetworkSnapshot[snapshot.AcceptedTargets.Length];
            for (int i = 0; i < snapshot.AcceptedTargets.Length; i++)
            {
                CharacterTestHitPolicyEventTargetKey key = snapshot.AcceptedTargets[i].Key;
                acceptedTargets[i] = new CharacterTestPolicyTargetNetworkSnapshot
                {
                    AttackerEntityId = key.EventKey.AttackerId.Value,
                    ActionInstanceId = key.EventKey.ActionInstanceId,
                    PolicyEventId = key.EventKey.PolicyEventId,
                    TargetEntityId = key.TargetId.Value
                };
            }

            return new CharacterTestHitResolutionPolicyNetworkSnapshot
            {
                AcceptedEvents = acceptedEvents,
                AcceptedTargets = acceptedTargets
            };
        }

        internal bool TryRestoreNetworkSnapshot(
            CharacterTestHitResolutionPolicyNetworkSnapshot snapshot,
            out string failure)
        {
            failure = string.Empty;
            if (!TryValidateNetworkSnapshot(snapshot, out failure))
                return false;
            Clear();
            if (snapshot == null)
                return true;

            CharacterTestHitPolicyEventNetworkSnapshot[] acceptedEvents =
                snapshot.AcceptedEvents ?? Array.Empty<CharacterTestHitPolicyEventNetworkSnapshot>();
            for (int i = 0; i < acceptedEvents.Length; i++)
            {
                CharacterTestHitPolicyEventNetworkSnapshot entry = acceptedEvents[i];
                if (entry == null
                    || entry.AttackerEntityId <= 0
                    || entry.ActionInstanceId <= 0
                    || entry.PolicyEventId <= 0
                    || entry.AcceptedCount <= 0)
                {
                    failure = "CharacterTest hit policy snapshot contains an invalid accepted-event entry.";
                    Clear();
                    return false;
                }

                var key = new CharacterTestHitPolicyEventKey(
                    new CombatEntityId(entry.AttackerEntityId),
                    entry.ActionInstanceId,
                    entry.PolicyEventId);
                if (_acceptedCounts.ContainsKey(key))
                {
                    failure = "CharacterTest hit policy snapshot contains duplicate accepted-event keys.";
                    Clear();
                    return false;
                }

                _acceptedCounts.Add(key, entry.AcceptedCount);
            }

            CharacterTestPolicyTargetNetworkSnapshot[] acceptedTargets =
                snapshot.AcceptedTargets ?? Array.Empty<CharacterTestPolicyTargetNetworkSnapshot>();
            for (int i = 0; i < acceptedTargets.Length; i++)
            {
                CharacterTestPolicyTargetNetworkSnapshot entry = acceptedTargets[i];
                if (entry == null
                    || entry.AttackerEntityId <= 0
                    || entry.ActionInstanceId <= 0
                    || entry.PolicyEventId <= 0
                    || entry.TargetEntityId <= 0)
                {
                    failure = "CharacterTest hit policy snapshot contains an invalid accepted-target entry.";
                    Clear();
                    return false;
                }

                var eventKey = new CharacterTestHitPolicyEventKey(
                    new CombatEntityId(entry.AttackerEntityId),
                    entry.ActionInstanceId,
                    entry.PolicyEventId);
                if (!_acceptedCounts.ContainsKey(eventKey))
                {
                    failure = "CharacterTest hit policy snapshot target references an unknown event key.";
                    Clear();
                    return false;
                }

                var targetKey = new CharacterTestHitPolicyEventTargetKey(
                    eventKey,
                    new CombatEntityId(entry.TargetEntityId));
                if (!_acceptedTargets.Add(targetKey))
                {
                    failure = "CharacterTest hit policy snapshot contains duplicate accepted-target keys.";
                    Clear();
                    return false;
                }
            }

            return true;
        }

        internal static bool TryValidateNetworkSnapshot(
            CharacterTestHitResolutionPolicyNetworkSnapshot snapshot,
            out string failure)
        {
            failure = string.Empty;
            if (snapshot == null)
                return true;

            CharacterTestHitPolicyEventNetworkSnapshot[] acceptedEvents =
                snapshot.AcceptedEvents ?? Array.Empty<CharacterTestHitPolicyEventNetworkSnapshot>();
            CharacterTestHitPolicyEventNetworkSnapshot previousEvent = null;
            for (int i = 0; i < acceptedEvents.Length; i++)
            {
                CharacterTestHitPolicyEventNetworkSnapshot entry = acceptedEvents[i];
                if (entry == null
                    || entry.AttackerEntityId <= 0
                    || entry.ActionInstanceId <= 0
                    || entry.PolicyEventId <= 0
                    || entry.AcceptedCount <= 0)
                {
                    failure = "CharacterTest hit policy snapshot contains an invalid accepted-event entry.";
                    return false;
                }
                if (previousEvent != null
                    && CompareNetworkEventEntries(previousEvent, entry) >= 0)
                {
                    failure = "CharacterTest hit policy accepted-events are not in stable order or contain duplicates.";
                    return false;
                }
                previousEvent = entry;
            }

            CharacterTestPolicyTargetNetworkSnapshot[] acceptedTargets =
                snapshot.AcceptedTargets ?? Array.Empty<CharacterTestPolicyTargetNetworkSnapshot>();
            CharacterTestPolicyTargetNetworkSnapshot previousTarget = null;
            for (int i = 0; i < acceptedTargets.Length; i++)
            {
                CharacterTestPolicyTargetNetworkSnapshot entry = acceptedTargets[i];
                if (entry == null
                    || entry.AttackerEntityId <= 0
                    || entry.ActionInstanceId <= 0
                    || entry.PolicyEventId <= 0
                    || entry.TargetEntityId <= 0)
                {
                    failure = "CharacterTest hit policy snapshot contains an invalid accepted-target entry.";
                    return false;
                }
                if (previousTarget != null
                    && CompareNetworkTargetEntries(previousTarget, entry) >= 0)
                {
                    failure = "CharacterTest hit policy accepted-targets are not in stable order or contain duplicates.";
                    return false;
                }
                previousTarget = entry;

                bool hasEvent = false;
                for (int eventIndex = 0; eventIndex < acceptedEvents.Length; eventIndex++)
                {
                    CharacterTestHitPolicyEventNetworkSnapshot acceptedEvent = acceptedEvents[eventIndex];
                    if (acceptedEvent.AttackerEntityId == entry.AttackerEntityId
                        && acceptedEvent.ActionInstanceId == entry.ActionInstanceId
                        && acceptedEvent.PolicyEventId == entry.PolicyEventId)
                    {
                        hasEvent = true;
                        break;
                    }
                }
                if (!hasEvent)
                {
                    failure = "CharacterTest hit policy snapshot target references an unknown event key.";
                    return false;
                }
            }
            return true;
        }

        private static int CompareNetworkEventEntries(
            CharacterTestHitPolicyEventNetworkSnapshot left,
            CharacterTestHitPolicyEventNetworkSnapshot right)
        {
            int compare = left.AttackerEntityId.CompareTo(right.AttackerEntityId);
            if (compare != 0)
                return compare;
            compare = left.ActionInstanceId.CompareTo(right.ActionInstanceId);
            return compare != 0 ? compare : left.PolicyEventId.CompareTo(right.PolicyEventId);
        }

        private static int CompareNetworkTargetEntries(
            CharacterTestPolicyTargetNetworkSnapshot left,
            CharacterTestPolicyTargetNetworkSnapshot right)
        {
            int compare = CompareNetworkEventEntries(
                new CharacterTestHitPolicyEventNetworkSnapshot
                {
                    AttackerEntityId = left.AttackerEntityId,
                    ActionInstanceId = left.ActionInstanceId,
                    PolicyEventId = left.PolicyEventId
                },
                new CharacterTestHitPolicyEventNetworkSnapshot
                {
                    AttackerEntityId = right.AttackerEntityId,
                    ActionInstanceId = right.ActionInstanceId,
                    PolicyEventId = right.PolicyEventId
                });
            return compare != 0 ? compare : left.TargetEntityId.CompareTo(right.TargetEntityId);
        }

        internal void AddStateHash(
            RuntimeHashAccumulator accumulator,
            int attackerEntityIdFilter = 0,
            bool excludeAttacker = false)
        {
            if (accumulator == null)
                throw new ArgumentNullException(nameof(accumulator));
            accumulator.BeginObject(CharacterTestHitResolutionPolicyHashFields.F11, CharacterTestHitResolutionPolicyHashFields.SchemaId, CharacterTestHitResolutionPolicyHashFields.SchemaVersion, true);

            _acceptedEventHashScratch.Clear();
            foreach (KeyValuePair<CharacterTestHitPolicyEventKey, int> pair in _acceptedCounts)
            {
                bool include = attackerEntityIdFilter <= 0
                    || (excludeAttacker
                        ? !MatchesAttacker(pair.Key.AttackerId, attackerEntityIdFilter)
                        : MatchesAttacker(pair.Key.AttackerId, attackerEntityIdFilter));
                if (include)
                {
                    _acceptedEventHashScratch.Add(new CharacterTestHitPolicyAcceptedEvent(
                        pair.Key,
                        pair.Value));
                }
            }
            _acceptedEventHashScratch.Sort(CompareAcceptedEventsCached);
            accumulator.BeginArray(CharacterTestHitResolutionPolicyHashFields.F1, _acceptedEventHashScratch.Count);
            for (int i = 0; i < _acceptedEventHashScratch.Count; i++)
            {
                CharacterTestHitPolicyAcceptedEvent entry = _acceptedEventHashScratch[i];
                accumulator.BeginElement(i);
                accumulator.AddInt(CharacterTestHitResolutionPolicyHashFields.F2, entry.Key.AttackerId.Value);
                accumulator.AddInt(CharacterTestHitResolutionPolicyHashFields.F3, entry.Key.ActionInstanceId);
                accumulator.AddInt(CharacterTestHitResolutionPolicyHashFields.F4, entry.Key.PolicyEventId);
                accumulator.AddInt(CharacterTestHitResolutionPolicyHashFields.F5, entry.AcceptedCount);
                accumulator.EndElement();
            }
            accumulator.EndArray();

            _acceptedTargetHashScratch.Clear();
            foreach (CharacterTestHitPolicyEventTargetKey key in _acceptedTargets)
            {
                bool include = attackerEntityIdFilter <= 0
                    || (excludeAttacker
                        ? !MatchesAttacker(key.EventKey.AttackerId, attackerEntityIdFilter)
                        : MatchesAttacker(key.EventKey.AttackerId, attackerEntityIdFilter));
                if (include)
                    _acceptedTargetHashScratch.Add(new CharacterTestHitPolicyAcceptedTarget(key));
            }
            _acceptedTargetHashScratch.Sort(CompareAcceptedTargetsCached);
            accumulator.BeginArray(CharacterTestHitResolutionPolicyHashFields.F6, _acceptedTargetHashScratch.Count);
            for (int i = 0; i < _acceptedTargetHashScratch.Count; i++)
            {
                CharacterTestHitPolicyEventTargetKey key = _acceptedTargetHashScratch[i].Key;
                accumulator.BeginElement(i);
                accumulator.AddInt(CharacterTestHitResolutionPolicyHashFields.F7, key.EventKey.AttackerId.Value);
                accumulator.AddInt(CharacterTestHitResolutionPolicyHashFields.F8, key.EventKey.ActionInstanceId);
                accumulator.AddInt(CharacterTestHitResolutionPolicyHashFields.F9, key.EventKey.PolicyEventId);
                accumulator.AddInt(CharacterTestHitResolutionPolicyHashFields.F10, key.TargetId.Value);
                accumulator.EndElement();
            }
            accumulator.EndArray();
            accumulator.EndObject();
        }

        private static bool MatchesAttacker(CombatEntityId attackerId, int logicalEntityId)
        {
            if (logicalEntityId <= 0)
                return true;

            // CharacterTest's combat queries use DynamicBodyIdStart + the
            // logical character id, while older policy-only callers supplied
            // the logical id directly. Accept both canonical forms so owned
            // and non-owned hashes partition the real direct-hit ledger.
            return attackerId.Value == logicalEntityId
                || CharacterTestCombatEntityLookup.ResolveCharacterEntityId(attackerId)
                    == logicalEntityId;
        }

        internal void RestoreSnapshot(CharacterTestHitResolutionPolicyStateSnapshot snapshot)
        {
            Clear();
            if (snapshot == null)
                return;

            CharacterTestHitPolicyAcceptedEvent[] acceptedEvents = snapshot.AcceptedEvents;
            for (int i = 0; i < acceptedEvents.Length; i++)
            {
                CharacterTestHitPolicyAcceptedEvent entry = acceptedEvents[i];
                _acceptedCounts[entry.Key] = entry.AcceptedCount;
            }

            CharacterTestHitPolicyAcceptedTarget[] acceptedTargets = snapshot.AcceptedTargets;
            for (int i = 0; i < acceptedTargets.Length; i++)
                _acceptedTargets.Add(acceptedTargets[i].Key);
        }

        private static int CompareAcceptedEvents(
            CharacterTestHitPolicyAcceptedEvent left,
            CharacterTestHitPolicyAcceptedEvent right)
        {
            return CompareEventKeys(left.Key, right.Key);
        }

        private static int CompareAcceptedTargets(
            CharacterTestHitPolicyAcceptedTarget left,
            CharacterTestHitPolicyAcceptedTarget right)
        {
            int compare = CompareEventKeys(left.Key.EventKey, right.Key.EventKey);
            return compare != 0 ? compare : left.Key.TargetId.Value.CompareTo(right.Key.TargetId.Value);
        }

        private static int CompareEventKeys(
            CharacterTestHitPolicyEventKey left,
            CharacterTestHitPolicyEventKey right)
        {
            int compare = left.AttackerId.Value.CompareTo(right.AttackerId.Value);
            if (compare != 0)
                return compare;
            compare = left.ActionInstanceId.CompareTo(right.ActionInstanceId);
            return compare != 0 ? compare : left.PolicyEventId.CompareTo(right.PolicyEventId);
        }
    }

    /// <summary>
    /// Shared one-candidate hit classification. A fresh generic hit-once set
    /// is used for each call; policy identity and bookkeeping are handled by
    /// <see cref="CharacterTestHitResolutionPolicyState"/> instead.
    /// </summary>
    internal sealed class CharacterTestHitResolutionEvaluator
    {
        private readonly HitResolveSystem _resolver = new HitResolveSystem();
        private readonly List<HitCandidate> _candidate = new List<HitCandidate>(1);
        private readonly List<HitResolveResult> _result = new List<HitResolveResult>(1);
        private readonly HashSet<WeaponHitOnceKey> _hitOnceKeys =
            new HashSet<WeaponHitOnceKey>(4);

        internal HitResolveResult Resolve(
            HitCandidate candidate,
            ITeamRelationProvider teamRelations,
            bool forceDuplicate = false,
            IHitTargetStateResolver targetStateResolver = null)
        {
            _candidate.Clear();
            _candidate.Add(candidate);
            _result.Clear();
            _hitOnceKeys.Clear();
            if (forceDuplicate)
                _hitOnceKeys.Add(candidate.HitOnceKey);

            _resolver.Resolve(_candidate, _hitOnceKeys, _result, teamRelations, targetStateResolver);
            return _result.Count == 0 ? default(HitResolveResult) : _result[0];
        }
    }

    /// <summary>
    /// Actual direct-hit resolver used by CharacterTest. It keeps the physical
    /// TraceId in HitCandidate/HitResolveResult while using PolicyEventId for
    /// authored multi-hit semantics.
    /// </summary>
    internal sealed class CharacterTestDirectHitResolutionService
    {
        private readonly List<HitCandidate> _sortedCandidates = new List<HitCandidate>(32);
        private readonly CharacterTestHitResolutionEvaluator _evaluator =
            new CharacterTestHitResolutionEvaluator();
        private readonly Action<HitCandidate> _missingPolicyReporter;

        internal CharacterTestDirectHitResolutionService(
            Action<HitCandidate> missingPolicyReporter = null)
        {
            _missingPolicyReporter = missingPolicyReporter;
        }

        internal int MissingPolicyCount { get; private set; }

        internal int Resolve(
            IReadOnlyList<HitCandidate> candidates,
            IReadOnlyDictionary<CharacterTestTracePayloadKey, CharacterTestTracePayload> payloadsByTrace,
            IReadOnlyDictionary<CombatQueryHeader, int> policyEventIdsByQuery,
            CharacterTestHitResolutionPolicyState policyState,
            List<HitResolveResult> results,
            ITeamRelationProvider teamRelations,
            IHitTargetStateResolver targetStateResolver = null)
        {
            if (candidates == null || policyState == null || results == null)
                return 0;

            _sortedCandidates.Clear();
            MissingPolicyCount = 0;
            for (int i = 0; i < candidates.Count; i++)
                _sortedCandidates.Add(candidates[i]);
            _sortedCandidates.Sort();

            int startCount = results.Count;
            for (int i = 0; i < _sortedCandidates.Count; i++)
            {
                HitCandidate candidate = _sortedCandidates[i];
                if (!TryResolvePolicy(
                    candidate,
                    payloadsByTrace,
                    policyEventIdsByQuery,
                    out CombatHitEventConfig config,
                    out int policyEventId))
                {
                    MissingPolicyCount++;
                    _missingPolicyReporter?.Invoke(candidate);
                    continue;
                }
                HitResolveResult resolved = _evaluator.Resolve(candidate, teamRelations, targetStateResolver: targetStateResolver);

                if (resolved.Kind == HitResolveKind.Damage
                    || resolved.Kind == HitResolveKind.Blocked)
                {
                    // Environment presentation is a blocker stop, not an
                    // authored target hit and must not consume MaxAcceptedHits.
                    bool isEnvironment = CharacterTestCombatHitEventResolver.IsStaticEnvironmentTarget(
                        candidate.TargetId);
                    if (!isEnvironment
                        && !policyState.TryAccept(candidate, policyEventId, config))
                    {
                        resolved = _evaluator.Resolve(candidate, teamRelations, forceDuplicate: true, targetStateResolver: targetStateResolver);
                    }
                }

                results.Add(resolved);
            }

            return results.Count - startCount;
        }

        private static bool TryResolvePolicy(
            HitCandidate candidate,
            IReadOnlyDictionary<CharacterTestTracePayloadKey, CharacterTestTracePayload> payloadsByTrace,
            IReadOnlyDictionary<CombatQueryHeader, int> policyEventIdsByQuery,
            out CombatHitEventConfig config,
            out int policyEventId)
        {
            config = default(CombatHitEventConfig);
            policyEventId = 0;
            if (payloadsByTrace != null
                && payloadsByTrace.TryGetValue(
                    new CharacterTestTracePayloadKey(candidate.ActionInstanceId, candidate.TraceId),
                    out CharacterTestTracePayload payload))
            {
                config = payload.Trace.HitEvent;
                if (policyEventIdsByQuery != null
                    && policyEventIdsByQuery.TryGetValue(
                        candidate.PhysicsHit.Query,
                        out int mappedPolicyEventId)
                    && mappedPolicyEventId > 0)
                {
                    policyEventId = mappedPolicyEventId;
                }
                else
                {
                    policyEventId = CharacterTestCharacterCombatTraceSystem.StablePolicyEventId(
                        payload.Trace.ActionId,
                        payload.Trace.StableEventId);
                }

                return policyEventId > 0;
            }

            return false;
        }
    }
}

namespace MxFramework.Game.CharacterTest
{
    // Frozen v3 protocol numbers. Retired entries remain reserved.
    internal static class CharacterTestHitResolutionPolicyHashFields
    {
        internal const int SchemaId = 10013;
        internal const int SchemaVersion = 1;
        internal const int F1 = 1; // characterTest.hitPolicy.acceptedEvents.count
        internal const int F2 = 2; // characterTest.hitPolicy.acceptedEvent.attackerEntityId
        internal const int F3 = 3; // characterTest.hitPolicy.acceptedEvent.actionInstanceId
        internal const int F4 = 4; // characterTest.hitPolicy.acceptedEvent.policyEventId
        internal const int F5 = 5; // characterTest.hitPolicy.acceptedEvent.acceptedCount
        internal const int F6 = 6; // characterTest.hitPolicy.acceptedTargets.count
        internal const int F7 = 7; // characterTest.hitPolicy.acceptedTarget.attackerEntityId
        internal const int F8 = 8; // characterTest.hitPolicy.acceptedTarget.actionInstanceId
        internal const int F9 = 9; // characterTest.hitPolicy.acceptedTarget.policyEventId
        internal const int F10 = 10; // characterTest.hitPolicy.acceptedTarget.targetEntityId
        internal const int F11 = 11; // $scope.AddStateHash
    }
}
