using System;
using System.Collections.Generic;
using System.Globalization;
using MxFramework.Core.Math;
using MxFramework.Runtime;

namespace MxFramework.Game.CharacterTest
{
    public enum CharacterTestInteractionKind
    {
        PhysicalField = 1,
    }

    public enum CharacterTestInteractionFieldShape
    {
        Sphere = 1,
        Capsule = 2,
        OrientedCapsule = 3,
    }

    public enum CharacterTestInteractionForceMode
    {
        RadialImpulse = 1,
        AttractionAcceleration = 2,
        DirectionalImpulse = 3,
    }

    [Flags]
    public enum CharacterTestInteractionTargetFilter
    {
        None = 0,
        DynamicCharacters = 1 << 0,
        DynamicMovableObjects = 1 << 1,
        RequireHostile = 1 << 2,
        ExcludeSource = 1 << 3,
    }

    public static class CharacterTestInteractionSourceDomains
    {
        public const int CombatProjectileAreaPulse = 1;
        public const int CombatProjectileExpiration = 2;
        public const int CombatProjectileActiveField = 3;
        public const int CombatProjectileDirectionalField = 4;
        public const int AbilityEffectTimelineAreaPulse = 5;
        public static string DisplayName(int id)
        {
            switch (id)
            {
                case CombatProjectileAreaPulse: return "CombatProjectile.AreaPulse";
                case CombatProjectileExpiration: return "CombatProjectile.Expiration";
                case CombatProjectileActiveField: return "CombatProjectile.ActiveField";
                case CombatProjectileDirectionalField: return "CombatProjectile.DirectionalField";
                case AbilityEffectTimelineAreaPulse: return "AbilityEffectTimeline.AreaPulse";
                case 0: return string.Empty;
                default: throw new ArgumentOutOfRangeException(nameof(id));
            }
        }

    }

    public readonly struct CharacterTestInteractionPhysicalField :
        IEquatable<CharacterTestInteractionPhysicalField>
    {
        public CharacterTestInteractionPhysicalField(
            CharacterTestInteractionFieldShape shape,
            CharacterTestInteractionForceMode forceMode,
            CharacterTestInteractionTargetFilter targetFilter,
            Fix64 capsuleBottomOffset,
            Fix64 capsuleTopOffset,
            int impulseSpeedMillimetersPerSecond,
            int accelerationMillimetersPerSecondSquared,
            int linearFalloffMinimumScaleMilli,
            int maxSpeedMillimetersPerSecond,
            int referenceMassGrams,
            int minimumMassScaleMilli,
            int maximumMassScaleMilli,
            bool useMassScale,
            bool usePushScale,
            bool useMaterialPushScale)
        {
            if (!IsSupportedFieldShape(shape))
                throw new ArgumentOutOfRangeException(nameof(shape), "CharacterTest interaction field shape is not supported.");
            if (!IsSupportedForceMode(forceMode))
                throw new ArgumentOutOfRangeException(nameof(forceMode), "CharacterTest interaction force mode is not supported.");

            int falloffMinimumScaleMilli = CharacterTestDeterministicMath.Clamp(
                linearFalloffMinimumScaleMilli,
                0,
                CharacterTestDeterministicUnits.ScaleFactorScale);
            int minimumScaleMilli = CharacterTestDeterministicMath.Clamp(
                minimumMassScaleMilli,
                0,
                CharacterTestDeterministicUnits.ScaleFactorScale * 10);
            int maximumScaleMilli = CharacterTestDeterministicMath.Clamp(
                Math.Max(minimumMassScaleMilli, maximumMassScaleMilli),
                minimumScaleMilli,
                CharacterTestDeterministicUnits.ScaleFactorScale * 10);

            Shape = shape;
            ForceMode = forceMode;
            TargetFilter = targetFilter;
            CapsuleBottomOffset = capsuleBottomOffset;
            CapsuleTopOffset = capsuleTopOffset;
            ImpulseSpeedMillimetersPerSecond = Math.Max(0, impulseSpeedMillimetersPerSecond);
            AccelerationMillimetersPerSecondSquared = Math.Max(0, accelerationMillimetersPerSecondSquared);
            LinearFalloffMinimumScaleMilli = falloffMinimumScaleMilli;
            MaxSpeedMillimetersPerSecond = Math.Max(0, maxSpeedMillimetersPerSecond);
            ReferenceMassGrams = Math.Max(0, referenceMassGrams);
            MinimumMassScaleMilli = minimumScaleMilli;
            MaximumMassScaleMilli = maximumScaleMilli;
            UseMassScale = useMassScale;
            UsePushScale = usePushScale;
            UseMaterialPushScale = useMaterialPushScale;
        }

        public CharacterTestInteractionFieldShape Shape { get; }
        public CharacterTestInteractionForceMode ForceMode { get; }
        public CharacterTestInteractionTargetFilter TargetFilter { get; }
        public Fix64 CapsuleBottomOffset { get; }
        public Fix64 CapsuleTopOffset { get; }
        public int ImpulseSpeedMillimetersPerSecond { get; }
        public int AccelerationMillimetersPerSecondSquared { get; }
        public int LinearFalloffMinimumScaleMilli { get; }
        public int MaxSpeedMillimetersPerSecond { get; }
        public int ReferenceMassGrams { get; }
        public int MinimumMassScaleMilli { get; }
        public int MaximumMassScaleMilli { get; }
        public bool UseMassScale { get; }
        public bool UsePushScale { get; }
        public bool UseMaterialPushScale { get; }

        public static CharacterTestInteractionPhysicalField RadialImpulseSphere(
            CharacterTestInteractionTargetFilter targetFilter,
            int impulseSpeedMillimetersPerSecond,
            int linearFalloffMinimumScaleMilli,
            int maxSpeedMillimetersPerSecond,
            int referenceMassGrams,
            int minimumMassScaleMilli,
            int maximumMassScaleMilli,
            bool useMassScale,
            bool usePushScale,
            bool useMaterialPushScale)
        {
            return new CharacterTestInteractionPhysicalField(
                CharacterTestInteractionFieldShape.Sphere,
                CharacterTestInteractionForceMode.RadialImpulse,
                targetFilter,
                Fix64.Zero,
                Fix64.Zero,
                impulseSpeedMillimetersPerSecond,
                accelerationMillimetersPerSecondSquared: 0,
                linearFalloffMinimumScaleMilli,
                maxSpeedMillimetersPerSecond,
                referenceMassGrams,
                minimumMassScaleMilli,
                maximumMassScaleMilli,
                useMassScale,
                usePushScale,
                useMaterialPushScale);
        }

        public static CharacterTestInteractionPhysicalField AttractionCapsule(
            CharacterTestInteractionTargetFilter targetFilter,
            Fix64 capsuleBottomOffset,
            Fix64 capsuleTopOffset,
            int accelerationMillimetersPerSecondSquared,
            int linearFalloffMinimumScaleMilli,
            int terminalSpeedMillimetersPerSecond,
            int referenceMassGrams,
            int minimumMassScaleMilli,
            int maximumMassScaleMilli,
            bool useMassScale)
        {
            return new CharacterTestInteractionPhysicalField(
                CharacterTestInteractionFieldShape.Capsule,
                CharacterTestInteractionForceMode.AttractionAcceleration,
                targetFilter,
                capsuleBottomOffset,
                capsuleTopOffset,
                impulseSpeedMillimetersPerSecond: 0,
                accelerationMillimetersPerSecondSquared,
                linearFalloffMinimumScaleMilli,
                terminalSpeedMillimetersPerSecond,
                referenceMassGrams,
                minimumMassScaleMilli,
                maximumMassScaleMilli,
                useMassScale,
                usePushScale: false,
                useMaterialPushScale: false);
        }

        /// <summary>Creates an oriented capsule field that applies a one-shot directional impulse.</summary>
        /// <remarks>NoAlloc.</remarks>
        public static CharacterTestInteractionPhysicalField DirectionalImpulseOrientedCapsule(
            CharacterTestInteractionTargetFilter targetFilter,
            Fix64 capsuleBottomOffset,
            Fix64 capsuleTopOffset,
            int impulseSpeedMillimetersPerSecond,
            int maxSpeedMillimetersPerSecond,
            int referenceMassGrams,
            int minimumMassScaleMilli,
            int maximumMassScaleMilli,
            bool useMassScale,
            bool useMaterialPushScale)
        {
            return new CharacterTestInteractionPhysicalField(
                CharacterTestInteractionFieldShape.OrientedCapsule,
                CharacterTestInteractionForceMode.DirectionalImpulse,
                targetFilter,
                capsuleBottomOffset,
                capsuleTopOffset,
                impulseSpeedMillimetersPerSecond,
                accelerationMillimetersPerSecondSquared: 0,
                linearFalloffMinimumScaleMilli: CharacterTestDeterministicUnits.ScaleFactorScale,
                maxSpeedMillimetersPerSecond,
                referenceMassGrams,
                minimumMassScaleMilli,
                maximumMassScaleMilli,
                useMassScale,
                usePushScale: false,
                useMaterialPushScale: useMaterialPushScale);
        }

        public bool Targets(CharacterTestInteractionTargetFilter filter)
        {
            return (TargetFilter & filter) == filter;
        }

        public bool Equals(CharacterTestInteractionPhysicalField other)
        {
            return Shape == other.Shape
                && ForceMode == other.ForceMode
                && TargetFilter == other.TargetFilter
                && CapsuleBottomOffset.Equals(other.CapsuleBottomOffset)
                && CapsuleTopOffset.Equals(other.CapsuleTopOffset)
                && ImpulseSpeedMillimetersPerSecond == other.ImpulseSpeedMillimetersPerSecond
                && AccelerationMillimetersPerSecondSquared == other.AccelerationMillimetersPerSecondSquared
                && LinearFalloffMinimumScaleMilli == other.LinearFalloffMinimumScaleMilli
                && MaxSpeedMillimetersPerSecond == other.MaxSpeedMillimetersPerSecond
                && ReferenceMassGrams == other.ReferenceMassGrams
                && MinimumMassScaleMilli == other.MinimumMassScaleMilli
                && MaximumMassScaleMilli == other.MaximumMassScaleMilli
                && UseMassScale == other.UseMassScale
                && UsePushScale == other.UsePushScale
                && UseMaterialPushScale == other.UseMaterialPushScale;
        }

        public override bool Equals(object obj)
        {
            return obj is CharacterTestInteractionPhysicalField other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = (int)Shape;
                hash = (hash * 397) ^ (int)ForceMode;
                hash = (hash * 397) ^ (int)TargetFilter;
                hash = (hash * 397) ^ CapsuleBottomOffset.GetHashCode();
                hash = (hash * 397) ^ CapsuleTopOffset.GetHashCode();
                hash = (hash * 397) ^ ImpulseSpeedMillimetersPerSecond;
                hash = (hash * 397) ^ AccelerationMillimetersPerSecondSquared;
                hash = (hash * 397) ^ LinearFalloffMinimumScaleMilli;
                hash = (hash * 397) ^ MaxSpeedMillimetersPerSecond;
                hash = (hash * 397) ^ ReferenceMassGrams;
                hash = (hash * 397) ^ MinimumMassScaleMilli;
                hash = (hash * 397) ^ MaximumMassScaleMilli;
                hash = (hash * 397) ^ (UseMassScale ? 1 : 0);
                hash = (hash * 397) ^ (UsePushScale ? 1 : 0);
                hash = (hash * 397) ^ (UseMaterialPushScale ? 1 : 0);
                return hash;
            }
        }

        public string ToSummaryLine()
        {
            return "shape=" + Shape
                + " mode=" + ForceMode
                + " targets=" + TargetFilter
                + " impulseMmS=" + ImpulseSpeedMillimetersPerSecond.ToString(CultureInfo.InvariantCulture)
                + " accelMmS2=" + AccelerationMillimetersPerSecondSquared.ToString(CultureInfo.InvariantCulture)
                + " falloffMin=" + LinearFalloffMinimumScaleMilli.ToString(CultureInfo.InvariantCulture)
                + " maxSpeed=" + MaxSpeedMillimetersPerSecond.ToString(CultureInfo.InvariantCulture)
                + " massScale=" + UseMassScale.ToString()
                + " pushScale=" + UsePushScale.ToString()
                + " materialScale=" + UseMaterialPushScale.ToString()
                + " capsuleOffsets=(" + CapsuleBottomOffset + "," + CapsuleTopOffset + ")";
        }

        internal void Contribute(RuntimeHashAccumulator accumulator, int key)
        {
            accumulator.BeginObject(key, CharacterTestInteractionEventHashFields.SchemaId, CharacterTestInteractionEventHashFields.SchemaVersion, true);
            accumulator.AddInt(CharacterTestInteractionEventHashFields.F1, (int)Shape);
            accumulator.AddInt(CharacterTestInteractionEventHashFields.F2, (int)ForceMode);
            accumulator.AddInt(CharacterTestInteractionEventHashFields.F3, (int)TargetFilter);
            accumulator.AddLong(CharacterTestInteractionEventHashFields.F4, CapsuleBottomOffset.RawValue);
            accumulator.AddLong(CharacterTestInteractionEventHashFields.F5, CapsuleTopOffset.RawValue);
            accumulator.AddInt(CharacterTestInteractionEventHashFields.F6, ImpulseSpeedMillimetersPerSecond);
            accumulator.AddInt(CharacterTestInteractionEventHashFields.F7, AccelerationMillimetersPerSecondSquared);
            accumulator.AddInt(CharacterTestInteractionEventHashFields.F8, LinearFalloffMinimumScaleMilli);
            accumulator.AddInt(CharacterTestInteractionEventHashFields.F9, MaxSpeedMillimetersPerSecond);
            accumulator.AddInt(CharacterTestInteractionEventHashFields.F10, ReferenceMassGrams);
            accumulator.AddInt(CharacterTestInteractionEventHashFields.F11, MinimumMassScaleMilli);
            accumulator.AddInt(CharacterTestInteractionEventHashFields.F12, MaximumMassScaleMilli);
            accumulator.AddInt(CharacterTestInteractionEventHashFields.F13, UseMassScale ? 1 : 0);
            accumulator.AddInt(CharacterTestInteractionEventHashFields.F14, UsePushScale ? 1 : 0);
            accumulator.AddInt(CharacterTestInteractionEventHashFields.F15, UseMaterialPushScale ? 1 : 0);
            accumulator.EndObject();
        }

        private static bool IsSupportedFieldShape(CharacterTestInteractionFieldShape shape)
        {
            return shape == CharacterTestInteractionFieldShape.Sphere
                || shape == CharacterTestInteractionFieldShape.Capsule
                || shape == CharacterTestInteractionFieldShape.OrientedCapsule;
        }

        private static bool IsSupportedForceMode(CharacterTestInteractionForceMode forceMode)
        {
            return forceMode == CharacterTestInteractionForceMode.RadialImpulse
                || forceMode == CharacterTestInteractionForceMode.AttractionAcceleration
                || forceMode == CharacterTestInteractionForceMode.DirectionalImpulse;
        }
    }

    public readonly struct CharacterTestInteractionEvent :
        IComparable<CharacterTestInteractionEvent>,
        IEquatable<CharacterTestInteractionEvent>
    {
        private static readonly FixVector3 DefaultDirection =
            new FixVector3(Fix64.Zero, Fix64.Zero, Fix64.One);

        public CharacterTestInteractionEvent(
            RuntimeFrame frame,
            CharacterTestInteractionKind kind,
            int stableId,
            int sourceOrder,
            int sourceEntityId,
            int targetEntityId,
            FixVector3 anchorPosition,
            FixVector3 direction,
            Fix64 radius,
            int intensity,
            int traceId,
            int sourceDomain,
            CharacterTestInteractionPhysicalField physicalField)
        {
            if (!IsSupportedKind(kind))
                throw new ArgumentOutOfRangeException(nameof(kind), "CharacterTest interaction kind is not supported.");
            if (radius < Fix64.Zero)
                throw new ArgumentOutOfRangeException(nameof(radius), "CharacterTest interaction radius cannot be negative.");

            Frame = frame;
            Kind = kind;
            if (stableId <= 0) throw new ArgumentOutOfRangeException(nameof(stableId));
            StableId = stableId;
            SourceOrder = sourceOrder < 0 ? 0 : sourceOrder;
            SourceEntityId = sourceEntityId < 0 ? 0 : sourceEntityId;
            TargetEntityId = targetEntityId < 0 ? 0 : targetEntityId;
            AnchorPosition = anchorPosition;
            Direction = direction.TryNormalize(out FixVector3 normalizedDirection)
                ? normalizedDirection
                : DefaultDirection;
            Radius = radius;
            Intensity = intensity < 0 ? 0 : intensity;
            TraceId = traceId < 0 ? 0 : traceId;
            if ((uint)sourceDomain > 5) throw new ArgumentOutOfRangeException(nameof(sourceDomain));
            SourceDomain = sourceDomain;
            PhysicalField = physicalField;
        }

        public RuntimeFrame Frame { get; }
        public CharacterTestInteractionKind Kind { get; }
        public int StableId { get; }
        public int SourceOrder { get; }
        public int SourceEntityId { get; }
        public int TargetEntityId { get; }
        public FixVector3 AnchorPosition { get; }
        public FixVector3 Direction { get; }
        public Fix64 Radius { get; }
        public int Intensity { get; }
        public int TraceId { get; }
        public int SourceDomain { get; }
        public CharacterTestInteractionPhysicalField PhysicalField { get; }

        public int CompareTo(CharacterTestInteractionEvent other)
        {
            int result = Frame.CompareTo(other.Frame);
            if (result != 0)
                return result;
            result = SourceOrder.CompareTo(other.SourceOrder);
            if (result != 0)
                return result;
            result = StableId.CompareTo(other.StableId);
            if (result != 0)
                return result;
            result = TraceId.CompareTo(other.TraceId);
            if (result != 0)
                return result;
            result = SourceEntityId.CompareTo(other.SourceEntityId);
            if (result != 0)
                return result;
            result = TargetEntityId.CompareTo(other.TargetEntityId);
            if (result != 0)
                return result;
            return SourceDomain.CompareTo(other.SourceDomain);
        }

        public bool HasSameIdentity(CharacterTestInteractionEvent other)
        {
            return Frame.Equals(other.Frame)
                && Kind == other.Kind
                && SourceOrder == other.SourceOrder
                && SourceEntityId == other.SourceEntityId
                && TargetEntityId == other.TargetEntityId
                && TraceId == other.TraceId
                && StableId == other.StableId
                && SourceDomain == other.SourceDomain;
        }

        public bool Equals(CharacterTestInteractionEvent other)
        {
            return HasSameIdentity(other)
                && AnchorPosition.Equals(other.AnchorPosition)
                && Direction.Equals(other.Direction)
                && Radius.Equals(other.Radius)
                && Intensity == other.Intensity
                && PhysicalField.Equals(other.PhysicalField);
        }

        public override bool Equals(object obj)
        {
            return obj is CharacterTestInteractionEvent other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = Frame.GetHashCode();
                hash = (hash * 397) ^ (int)Kind;
                hash = (hash * 397) ^ StableId;
                hash = (hash * 397) ^ SourceOrder;
                hash = (hash * 397) ^ SourceEntityId;
                hash = (hash * 397) ^ TargetEntityId;
                hash = (hash * 397) ^ AnchorPosition.GetHashCode();
                hash = (hash * 397) ^ Direction.GetHashCode();
                hash = (hash * 397) ^ Radius.GetHashCode();
                hash = (hash * 397) ^ Intensity;
                hash = (hash * 397) ^ TraceId;
                hash = (hash * 397) ^ SourceDomain;
                hash = (hash * 397) ^ PhysicalField.GetHashCode();
                return hash;
            }
        }

        public string ToSummaryLine()
        {
            return "kind=" + Kind
                + " frame=" + Frame.Value.ToString(CultureInfo.InvariantCulture)
                + " stable=" + StableId.ToString(CultureInfo.InvariantCulture)
                + " source=" + SourceEntityId.ToString(CultureInfo.InvariantCulture)
                + " target=" + TargetEntityId.ToString(CultureInfo.InvariantCulture)
                + " order=" + SourceOrder.ToString(CultureInfo.InvariantCulture)
                + " radius=" + Radius
                + " intensity=" + Intensity.ToString(CultureInfo.InvariantCulture)
                + " anchor=(" + AnchorPosition.X + "," + AnchorPosition.Y + "," + AnchorPosition.Z + ")"
                + " domain=" + CharacterTestInteractionSourceDomains.DisplayName(SourceDomain)
                + " field={" + PhysicalField.ToSummaryLine() + "}";
        }

        internal void Contribute(RuntimeHashAccumulator accumulator, int key)
        {
            accumulator.BeginObject(key, CharacterTestInteractionEventHashFields.SchemaId, CharacterTestInteractionEventHashFields.SchemaVersion, true);
            accumulator.AddLong(CharacterTestInteractionEventHashFields.F16, Frame.Value);
            accumulator.AddInt(CharacterTestInteractionEventHashFields.F17, (int)Kind);
            accumulator.AddInt(CharacterTestInteractionEventHashFields.F18, StableId);
            accumulator.AddInt(CharacterTestInteractionEventHashFields.F19, SourceOrder);
            accumulator.AddInt(CharacterTestInteractionEventHashFields.F20, SourceEntityId);
            accumulator.AddInt(CharacterTestInteractionEventHashFields.F21, TargetEntityId);
            accumulator.AddLong(CharacterTestInteractionEventHashFields.F22, AnchorPosition.X.RawValue);
            accumulator.AddLong(CharacterTestInteractionEventHashFields.F23, AnchorPosition.Y.RawValue);
            accumulator.AddLong(CharacterTestInteractionEventHashFields.F24, AnchorPosition.Z.RawValue);
            accumulator.AddLong(CharacterTestInteractionEventHashFields.F25, Direction.X.RawValue);
            accumulator.AddLong(CharacterTestInteractionEventHashFields.F26, Direction.Y.RawValue);
            accumulator.AddLong(CharacterTestInteractionEventHashFields.F27, Direction.Z.RawValue);
            accumulator.AddLong(CharacterTestInteractionEventHashFields.F28, Radius.RawValue);
            accumulator.AddInt(CharacterTestInteractionEventHashFields.F29, Intensity);
            accumulator.AddInt(CharacterTestInteractionEventHashFields.F30, TraceId);
            accumulator.AddInt(CharacterTestInteractionEventHashFields.F31, SourceDomain);
            PhysicalField.Contribute(accumulator, CharacterTestInteractionEventHashFields.F33);
            accumulator.EndObject();
        }

        private static string EmptyAsNone(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? "none" : value;
        }

        private static bool IsSupportedKind(CharacterTestInteractionKind kind)
        {
            return kind == CharacterTestInteractionKind.PhysicalField;
        }
    }

    public sealed class CharacterTestInteractionHistory
    {
        public const int DefaultMaxHistoryFrames = 30;

        private readonly RuntimeHashAccumulator _hash = new RuntimeHashAccumulator(CharacterTestInteractionEventHashFields.SchemaId, CharacterTestInteractionEventHashFields.SchemaVersion);
        private readonly List<CharacterTestInteractionEvent> _events;
        private readonly List<CharacterTestInteractionEvent> _lastFrameEvents;
        private readonly int _maxHistoryFrames;

        public CharacterTestInteractionHistory(int maxHistoryFrames = DefaultMaxHistoryFrames)
        {
            _maxHistoryFrames = maxHistoryFrames <= 0 ? DefaultMaxHistoryFrames : maxHistoryFrames;
            _events = new List<CharacterTestInteractionEvent>(Math.Max(8, _maxHistoryFrames * 2));
            _lastFrameEvents = new List<CharacterTestInteractionEvent>(8);
        }

        public IReadOnlyList<CharacterTestInteractionEvent> Events => _events;
        public IReadOnlyList<CharacterTestInteractionEvent> LastFrameEvents => _lastFrameEvents;
        public int EventCount => _events.Count;
        public int LastFrameEventCount => _lastFrameEvents.Count;
        public int Count => EventCount;
        public int LastFrameCount => LastFrameEventCount;

        public void BeginFrame(RuntimeFrame frame)
        {
            _lastFrameEvents.Clear();
            Prune(frame.Value);
        }

        public bool Record(CharacterTestInteractionEvent interaction)
        {
            if (ContainsIdentity(interaction))
                return false;

            InsertSorted(_events, interaction);
            InsertSorted(_lastFrameEvents, interaction);
            return true;
        }

        public void Clear()
        {
            _events.Clear();
            _lastFrameEvents.Clear();
        }

        public CharacterTestInteractionDebugSnapshot CreateDebugSnapshot()
        {
            return new CharacterTestInteractionDebugSnapshot(
                _events,
                _lastFrameEvents,
                ComputeStableHash());
        }

        public long ComputeStableHash()
        {
            RuntimeHashAccumulator accumulator = _hash;
            accumulator.Reset();
            accumulator.BeginArray(CharacterTestInteractionEventHashFields.F32, _events.Count);
            for (int i = 0; i < _events.Count; i++)
            {
                accumulator.BeginElement(i);
                _events[i].Contribute(accumulator, CharacterTestInteractionEventHashFields.F34);
                accumulator.EndElement();
            }
            accumulator.EndArray();
            return accumulator.ToHash();
        }

        private bool ContainsIdentity(CharacterTestInteractionEvent interaction)
        {
            for (int i = 0; i < _events.Count; i++)
            {
                if (_events[i].HasSameIdentity(interaction))
                    return true;
            }

            return false;
        }

        private static void InsertSorted(
            List<CharacterTestInteractionEvent> events,
            CharacterTestInteractionEvent interaction)
        {
            int index = events.Count;
            while (index > 0 && interaction.CompareTo(events[index - 1]) < 0)
                index--;

            if (index == events.Count)
                events.Add(interaction);
            else
                events.Insert(index, interaction);
        }

        private void Prune(long currentFrame)
        {
            if (_events.Count == 0)
                return;

            long oldestFrame = currentFrame - _maxHistoryFrames;
            int removeCount = 0;
            while (removeCount < _events.Count && _events[removeCount].Frame.Value < oldestFrame)
                removeCount++;
            if (removeCount > 0)
                _events.RemoveRange(0, removeCount);
        }
    }

    public sealed class CharacterTestInteractionDebugSnapshot
    {
        public static readonly CharacterTestInteractionDebugSnapshot Empty =
            new CharacterTestInteractionDebugSnapshot(null, null, 0L);

        public CharacterTestInteractionDebugSnapshot(
            IReadOnlyList<CharacterTestInteractionEvent> events,
            IReadOnlyList<CharacterTestInteractionEvent> lastFrameEvents,
            long stableHash)
        {
            Events = Copy(events);
            LastFrameEvents = Copy(lastFrameEvents);
            StableHash = stableHash;
        }

        public IReadOnlyList<CharacterTestInteractionEvent> Events { get; }
        public IReadOnlyList<CharacterTestInteractionEvent> LastFrameEvents { get; }
        public int Count => Events.Count;
        public int LastFrameCount => LastFrameEvents.Count;
        public long StableHash { get; }

        public bool TryGetLastEvent(out CharacterTestInteractionEvent interaction)
        {
            if (Events.Count == 0)
            {
                interaction = default;
                return false;
            }

            interaction = Events[Events.Count - 1];
            return true;
        }

        private static CharacterTestInteractionEvent[] Copy(IReadOnlyList<CharacterTestInteractionEvent> source)
        {
            if (source == null || source.Count == 0)
                return Array.Empty<CharacterTestInteractionEvent>();

            var result = new CharacterTestInteractionEvent[source.Count];
            for (int i = 0; i < source.Count; i++)
                result[i] = source[i];
            return result;
        }
    }
}

namespace MxFramework.Game.CharacterTest
{
    // Frozen v3 protocol numbers. Retired entries remain reserved.
    internal static class CharacterTestInteractionEventHashFields
    {
        internal const int SchemaId = 10011;
        internal const int SchemaVersion = 1;
        internal const int F1 = 1; // .shape
        internal const int F2 = 2; // .forceMode
        internal const int F3 = 3; // .targetFilter
        internal const int F4 = 4; // .capsuleBottomOffset.raw
        internal const int F5 = 5; // .capsuleTopOffset.raw
        internal const int F6 = 6; // .impulseSpeedMmS
        internal const int F7 = 7; // .accelerationMmS2
        internal const int F8 = 8; // .falloffMinMilli
        internal const int F9 = 9; // .maxSpeedMmS
        internal const int F10 = 10; // .referenceMassGrams
        internal const int F11 = 11; // .minMassScaleMilli
        internal const int F12 = 12; // .maxMassScaleMilli
        internal const int F13 = 13; // .useMassScale
        internal const int F14 = 14; // .usePushScale
        internal const int F15 = 15; // .useMaterialPushScale
        internal const int F16 = 16; // .frame
        internal const int F17 = 17; // .kind
        internal const int F18 = 18; // .stableId
        internal const int F19 = 19; // .sourceOrder
        internal const int F20 = 20; // .sourceEntityId
        internal const int F21 = 21; // .targetEntityId
        internal const int F22 = 22; // .anchor.x.raw
        internal const int F23 = 23; // .anchor.y.raw
        internal const int F24 = 24; // .anchor.z.raw
        internal const int F25 = 25; // .direction.x.raw
        internal const int F26 = 26; // .direction.y.raw
        internal const int F27 = 27; // .direction.z.raw
        internal const int F28 = 28; // .radius.raw
        internal const int F29 = 29; // .intensity
        internal const int F30 = 30; // .traceId
        internal const int F31 = 31; // .sourceDomain
        internal const int F32 = 32; // characterTest.interactions.count
        internal const int F33 = 33; // .physicalField
        internal const int F34 = 34; // characterTest.interactions.
    }
}


