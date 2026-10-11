using System;
using System.Collections.Generic;
using MxFramework.Combat.Core;
using MxFramework.Core.Math;

namespace MxFramework.Combat.Physics
{
    public readonly struct CombatPhysicalContactSensorSample
    {
        public CombatPhysicalContactSensorSample(
            int sensorId,
            CombatPhysicalContactKind kind,
            CombatEntityId sourceEntityId,
            CombatBodyId sourceBodyId,
            CombatColliderId sourceColliderId,
            int sourceSlotId,
            FixVector3 center,
            Fix64 radius,
            CombatPhysicsLayerMask layerMask,
            CombatSurfaceMaterial fallbackSurfaceMaterial,
            int relativeSpeedMillimetersPerSecond,
            int normalSpeedMillimetersPerSecond,
            int tangentialSpeedMillimetersPerSecond,
            int pressure,
            int intensity,
            int traceId,
            int sourceOrder,
            bool enabled = true,
            int contactPointId = 0,
            int bodyPartId = 0,
            CombatBodyPartContactActivationMode activationMode = CombatBodyPartContactActivationMode.Always)
        {
            if (sensorId < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(sensorId), "Contact sensor id cannot be negative.");
            }

            if (sourceSlotId < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(sourceSlotId), "Contact source slot id cannot be negative.");
            }

            if (radius < Fix64.Zero)
            {
                throw new ArgumentOutOfRangeException(nameof(radius), "Contact sensor radius cannot be negative.");
            }

            if (traceId < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(traceId), "Contact sensor trace id cannot be negative.");
            }

            if (sourceOrder < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(sourceOrder), "Contact sensor source order cannot be negative.");
            }

            if (contactPointId < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(contactPointId), "Contact point id cannot be negative.");
            }

            if (bodyPartId < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(bodyPartId), "Contact body part id cannot be negative.");
            }

            SensorId = sensorId;
            Kind = kind;
            SourceEntityId = sourceEntityId;
            SourceBodyId = sourceBodyId;
            SourceColliderId = sourceColliderId;
            SourceSlotId = sourceSlotId;
            Center = center;
            Radius = radius;
            LayerMask = layerMask;
            FallbackSurfaceMaterial = fallbackSurfaceMaterial;
            RelativeSpeedMillimetersPerSecond = ClampNonNegative(relativeSpeedMillimetersPerSecond);
            NormalSpeedMillimetersPerSecond = ClampNonNegative(normalSpeedMillimetersPerSecond);
            TangentialSpeedMillimetersPerSecond = ClampNonNegative(tangentialSpeedMillimetersPerSecond);
            Pressure = ClampNonNegative(pressure);
            Intensity = ClampNonNegative(intensity);
            TraceId = traceId;
            SourceOrder = sourceOrder;
            Enabled = enabled;
            ContactPointId = contactPointId;
            BodyPartId = bodyPartId;
            ActivationMode = activationMode;
        }

        public int SensorId { get; }

        public CombatPhysicalContactKind Kind { get; }

        public CombatEntityId SourceEntityId { get; }

        public CombatBodyId SourceBodyId { get; }

        public CombatColliderId SourceColliderId { get; }

        public int SourceSlotId { get; }

        public FixVector3 Center { get; }

        public Fix64 Radius { get; }

        public CombatPhysicsLayerMask LayerMask { get; }

        public CombatSurfaceMaterial FallbackSurfaceMaterial { get; }

        public int RelativeSpeedMillimetersPerSecond { get; }

        public int NormalSpeedMillimetersPerSecond { get; }

        public int TangentialSpeedMillimetersPerSecond { get; }

        public int Pressure { get; }

        public int Intensity { get; }

        public int TraceId { get; }

        public int SourceOrder { get; }

        public bool Enabled { get; }

        public int ContactPointId { get; }

        public int BodyPartId { get; }

        public CombatBodyPartContactActivationMode ActivationMode { get; }

        private static int ClampNonNegative(int value)
        {
            return value < 0 ? 0 : value;
        }
    }

    public readonly struct CombatBodyPartContactPointDefinition : IEquatable<CombatBodyPartContactPointDefinition>
    {
        public CombatBodyPartContactPointDefinition(
            int contactPointId,
            int bodyPartId,
            FixVector3 localOffset,
            Fix64 radius,
            CombatPhysicsLayerMask layerMask,
            CombatBodyPartContactActivationMode activationMode,
            int sourceOrder,
            int sourceSlotId = 0)
        {
            if (contactPointId <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(contactPointId), "Contact point id must be positive.");
            }

            if (bodyPartId < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(bodyPartId), "Body part id cannot be negative.");
            }

            if (radius < Fix64.Zero)
            {
                throw new ArgumentOutOfRangeException(nameof(radius), "Contact point radius cannot be negative.");
            }

            if (sourceOrder < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(sourceOrder), "Contact point source order cannot be negative.");
            }

            if (sourceSlotId < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(sourceSlotId), "Contact point source slot id cannot be negative.");
            }

            ContactPointId = contactPointId;
            BodyPartId = bodyPartId;
            LocalOffset = localOffset;
            Radius = radius;
            LayerMask = layerMask;
            ActivationMode = activationMode;
            SourceOrder = sourceOrder;
            SourceSlotId = sourceSlotId == 0 ? contactPointId : sourceSlotId;
        }

        public int ContactPointId { get; }

        public int BodyPartId { get; }

        public FixVector3 LocalOffset { get; }

        public Fix64 Radius { get; }

        public CombatPhysicsLayerMask LayerMask { get; }

        public CombatBodyPartContactActivationMode ActivationMode { get; }

        public int SourceOrder { get; }

        public int SourceSlotId { get; }

        public bool Equals(CombatBodyPartContactPointDefinition other)
        {
            return ContactPointId == other.ContactPointId
                && BodyPartId == other.BodyPartId
                && LocalOffset.Equals(other.LocalOffset)
                && Radius.Equals(other.Radius)
                && LayerMask.Equals(other.LayerMask)
                && ActivationMode == other.ActivationMode
                && SourceOrder == other.SourceOrder
                && SourceSlotId == other.SourceSlotId;
        }

        public override bool Equals(object obj)
        {
            return obj is CombatBodyPartContactPointDefinition other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = ContactPointId;
                hash = (hash * 397) ^ BodyPartId;
                hash = (hash * 397) ^ LocalOffset.GetHashCode();
                hash = (hash * 397) ^ Radius.GetHashCode();
                hash = (hash * 397) ^ LayerMask.GetHashCode();
                hash = (hash * 397) ^ (int)ActivationMode;
                hash = (hash * 397) ^ SourceOrder;
                hash = (hash * 397) ^ SourceSlotId;
                return hash;
            }
        }

        public CombatHash ContributeHash(CombatHash hash)
        {
            return hash
                .Add(ContactPointId)
                .Add(BodyPartId)
                .Add(unchecked((ulong)LocalOffset.X.RawValue))
                .Add(unchecked((ulong)LocalOffset.Y.RawValue))
                .Add(unchecked((ulong)LocalOffset.Z.RawValue))
                .Add(unchecked((ulong)Radius.RawValue))
                .Add(LayerMask.Value)
                .Add((int)ActivationMode)
                .Add(SourceOrder)
                .Add(SourceSlotId);
        }
    }

    public readonly struct CombatBodyPartContactPointSample
    {
        public CombatBodyPartContactPointSample(
            CombatBodyPartContactPointDefinition definition,
            CombatEntityId sourceEntityId,
            CombatBodyId sourceBodyId,
            CombatColliderId sourceColliderId,
            FixVector3 bodyPosition,
            FixVector3 forward,
            FixVector3 right,
            FixVector3 up,
            CombatSurfaceMaterial fallbackSurfaceMaterial,
            int relativeSpeedMillimetersPerSecond,
            int normalSpeedMillimetersPerSecond,
            int tangentialSpeedMillimetersPerSecond,
            int pressure,
            int intensity,
            int traceId,
            bool actionWindowActive = false,
            bool explicitActive = false,
            bool motionStateActive = false,
            int sensorId = 0)
        {
            if (traceId < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(traceId), "Contact point trace id cannot be negative.");
            }

            if (sensorId < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(sensorId), "Contact point sensor id cannot be negative.");
            }

            Definition = definition;
            SourceEntityId = sourceEntityId;
            SourceBodyId = sourceBodyId;
            SourceColliderId = sourceColliderId;
            BodyPosition = bodyPosition;
            Forward = forward;
            Right = right;
            Up = up;
            FallbackSurfaceMaterial = fallbackSurfaceMaterial;
            RelativeSpeedMillimetersPerSecond = ClampNonNegative(relativeSpeedMillimetersPerSecond);
            NormalSpeedMillimetersPerSecond = ClampNonNegative(normalSpeedMillimetersPerSecond);
            TangentialSpeedMillimetersPerSecond = ClampNonNegative(tangentialSpeedMillimetersPerSecond);
            Pressure = ClampNonNegative(pressure);
            Intensity = ClampNonNegative(intensity);
            TraceId = traceId;
            ActionWindowActive = actionWindowActive;
            ExplicitActive = explicitActive;
            MotionStateActive = motionStateActive;
            SensorId = sensorId;
        }

        public CombatBodyPartContactPointDefinition Definition { get; }

        public CombatEntityId SourceEntityId { get; }

        public CombatBodyId SourceBodyId { get; }

        public CombatColliderId SourceColliderId { get; }

        public FixVector3 BodyPosition { get; }

        public FixVector3 Forward { get; }

        public FixVector3 Right { get; }

        public FixVector3 Up { get; }

        public CombatSurfaceMaterial FallbackSurfaceMaterial { get; }

        public int RelativeSpeedMillimetersPerSecond { get; }

        public int NormalSpeedMillimetersPerSecond { get; }

        public int TangentialSpeedMillimetersPerSecond { get; }

        public int Pressure { get; }

        public int Intensity { get; }

        public int TraceId { get; }

        public bool ActionWindowActive { get; }

        public bool ExplicitActive { get; }

        public bool MotionStateActive { get; }

        public int SensorId { get; }

        private static int ClampNonNegative(int value)
        {
            return value < 0 ? 0 : value;
        }
    }

    /// <summary>
    /// Converts deterministic body-part contact point samples into Combat physical-contact events.
    /// </summary>
    /// <remarks>
    /// NoAlloc after initialization when sample capacity and contact sensor capacities are pre-sized.
    /// New sensor ids may grow the internal contact state dictionary.
    /// </remarks>
    public sealed class CombatBodyPartContactPointRuntime
    {
        private static readonly SampleComparer Comparer = new SampleComparer();

        private readonly CombatPhysicalContactSensorRunner _sensorRunner;
        private readonly List<CombatBodyPartContactPointSample> _orderedSamples;
        private readonly List<CombatPhysicalContactSensorSample> _sensorSamples;

        public CombatBodyPartContactPointRuntime(
            int sampleCapacity,
            int queryHitCapacity)
        {
            if (sampleCapacity < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(sampleCapacity), "Contact point sample capacity cannot be negative.");
            }

            if (queryHitCapacity < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(queryHitCapacity), "Contact point query hit capacity cannot be negative.");
            }

            _sensorRunner = new CombatPhysicalContactSensorRunner(sampleCapacity, queryHitCapacity);
            _orderedSamples = new List<CombatBodyPartContactPointSample>(sampleCapacity);
            _sensorSamples = new List<CombatPhysicalContactSensorSample>(sampleCapacity);
        }

        public void Reset()
        {
            _sensorRunner.Reset();
            _orderedSamples.Clear();
            _sensorSamples.Clear();
        }

        /// <summary>
        /// Emits body-part contact events for the supplied deterministic samples.
        /// </summary>
        /// <remarks>NoAlloc after initialization when capacities are large enough for the active sample set.</remarks>
        public void Tick(
            CombatPhysicsWorld world,
            IReadOnlyList<CombatBodyPartContactPointSample> samples,
            CombatFrame frame,
            CombatPhysicalContactEventBuffer eventBuffer)
        {
            if (eventBuffer == null)
            {
                throw new ArgumentNullException(nameof(eventBuffer));
            }

            if (samples == null || samples.Count == 0)
            {
                return;
            }

            _orderedSamples.Clear();
            _sensorSamples.Clear();
            for (int i = 0; i < samples.Count; i++)
            {
                _orderedSamples.Add(samples[i]);
            }

            SortSamples(_orderedSamples);
            for (int i = 0; i < _orderedSamples.Count; i++)
            {
                CombatBodyPartContactPointSample sample = _orderedSamples[i];
                CombatBodyPartContactPointDefinition definition = sample.Definition;
                bool active = IsActive(sample);
                FixVector3 center = TransformLocalOffset(
                    sample.BodyPosition,
                    sample.Forward,
                    sample.Right,
                    sample.Up,
                    definition.LocalOffset);
                int sensorId = sample.SensorId > 0
                    ? sample.SensorId
                    : CreateSensorId(sample.SourceEntityId, definition.ContactPointId, definition.BodyPartId);
                _sensorSamples.Add(new CombatPhysicalContactSensorSample(
                    sensorId,
                    CombatPhysicalContactKind.BodyPartContact,
                    sample.SourceEntityId,
                    sample.SourceBodyId,
                    sample.SourceColliderId,
                    definition.SourceSlotId,
                    center,
                    definition.Radius,
                    definition.LayerMask,
                    sample.FallbackSurfaceMaterial,
                    sample.RelativeSpeedMillimetersPerSecond,
                    sample.NormalSpeedMillimetersPerSecond,
                    sample.TangentialSpeedMillimetersPerSecond,
                    sample.Pressure,
                    sample.Intensity,
                    sample.TraceId,
                    definition.SourceOrder,
                    active,
                    definition.ContactPointId,
                    definition.BodyPartId,
                    definition.ActivationMode));
            }

            _sensorRunner.Tick(world, _sensorSamples, frame, eventBuffer);
            _sensorSamples.Clear();
        }

        private static void SortSamples(List<CombatBodyPartContactPointSample> samples)
        {
            for (int i = 1; i < samples.Count; i++)
            {
                CombatBodyPartContactPointSample value = samples[i];
                int insertIndex = i - 1;
                while (insertIndex >= 0 && Comparer.Compare(samples[insertIndex], value) > 0)
                {
                    samples[insertIndex + 1] = samples[insertIndex];
                    insertIndex--;
                }

                samples[insertIndex + 1] = value;
            }
        }

        private static bool IsActive(in CombatBodyPartContactPointSample sample)
        {
            switch (sample.Definition.ActivationMode)
            {
                case CombatBodyPartContactActivationMode.Always:
                    return true;
                case CombatBodyPartContactActivationMode.ActionWindow:
                    return sample.ActionWindowActive;
                case CombatBodyPartContactActivationMode.ExplicitState:
                    return sample.ExplicitActive;
                case CombatBodyPartContactActivationMode.MotionState:
                    return sample.MotionStateActive;
                default:
                    return false;
            }
        }

        private static FixVector3 TransformLocalOffset(
            FixVector3 bodyPosition,
            FixVector3 forward,
            FixVector3 right,
            FixVector3 up,
            FixVector3 localOffset)
        {
            FixVector3 safeForward = forward.IsZero
                ? new FixVector3(Fix64.Zero, Fix64.Zero, Fix64.One)
                : forward;
            FixVector3 safeRight = right.IsZero
                ? new FixVector3(Fix64.One, Fix64.Zero, Fix64.Zero)
                : right;
            FixVector3 safeUp = up.IsZero
                ? new FixVector3(Fix64.Zero, Fix64.One, Fix64.Zero)
                : up;
            return bodyPosition
                + (safeRight * localOffset.X)
                + (safeUp * localOffset.Y)
                + (safeForward * localOffset.Z);
        }

        private static int CreateSensorId(
            CombatEntityId sourceEntityId,
            int contactPointId,
            int bodyPartId)
        {
            unchecked
            {
                int hash = 29;
                hash = (hash * 31) + sourceEntityId.Value;
                hash = (hash * 31) + contactPointId;
                hash = (hash * 31) + bodyPartId;
                hash &= int.MaxValue;
                return hash == 0 ? 1 : hash;
            }
        }

        private sealed class SampleComparer : IComparer<CombatBodyPartContactPointSample>
        {
            public int Compare(
                CombatBodyPartContactPointSample left,
                CombatBodyPartContactPointSample right)
            {
                int compare = left.Definition.SourceOrder.CompareTo(right.Definition.SourceOrder);
                if (compare != 0)
                    return compare;

                compare = left.SourceEntityId.Value.CompareTo(right.SourceEntityId.Value);
                if (compare != 0)
                    return compare;

                compare = left.SourceBodyId.Value.CompareTo(right.SourceBodyId.Value);
                if (compare != 0)
                    return compare;

                compare = left.Definition.ContactPointId.CompareTo(right.Definition.ContactPointId);
                if (compare != 0)
                    return compare;

                return left.Definition.BodyPartId.CompareTo(right.Definition.BodyPartId);
            }
        }
    }

    /// <summary>
    /// Runs deterministic Combat physics sensor samples and emits contact phase events.
    /// </summary>
    /// <remarks>
    /// NoAlloc after initialization when max sensor ids and query-hit capacity are pre-sized for the slice.
    /// New sensor ids may grow the internal state dictionary.
    /// </remarks>
    public sealed class CombatPhysicalContactSensorRunner
    {
        private readonly Dictionary<int, SensorContactState> _states;
        private readonly List<CombatQueryResult> _queryHits;

        public CombatPhysicalContactSensorRunner(int sensorCapacity, int queryHitCapacity)
        {
            if (sensorCapacity < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(sensorCapacity), "Contact sensor capacity cannot be negative.");
            }

            if (queryHitCapacity < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(queryHitCapacity), "Contact query hit capacity cannot be negative.");
            }

            _states = new Dictionary<int, SensorContactState>(sensorCapacity);
            _queryHits = new List<CombatQueryResult>(queryHitCapacity);
        }

        public void Reset()
        {
            _states.Clear();
            _queryHits.Clear();
        }

        /// <summary>
        /// Queries all caller-owned sensor samples and appends deterministic contact phase events.
        /// </summary>
        /// <remarks>
        /// NoAlloc after initialization when sensor ids and query-hit capacity are pre-sized.
        /// New sensor ids may grow the internal state dictionary.
        /// </remarks>
        public void Tick(
            CombatPhysicsWorld world,
            IReadOnlyList<CombatPhysicalContactSensorSample> samples,
            CombatFrame frame,
            CombatPhysicalContactEventBuffer eventBuffer)
        {
            if (eventBuffer == null)
            {
                throw new ArgumentNullException(nameof(eventBuffer));
            }

            if (samples == null || samples.Count == 0)
            {
                return;
            }

            for (int i = 0; i < samples.Count; i++)
            {
                TickSample(world, samples[i], frame, eventBuffer);
            }

            _queryHits.Clear();
        }

        private void TickSample(
            CombatPhysicsWorld world,
            in CombatPhysicalContactSensorSample sample,
            CombatFrame frame,
            CombatPhysicalContactEventBuffer eventBuffer)
        {
            if (!_states.TryGetValue(sample.SensorId, out SensorContactState previous))
            {
                previous = SensorContactState.Empty;
            }

            if (!sample.Enabled || world == null || sample.LayerMask.Value == 0)
            {
                if (previous.IsTouching)
                {
                    AddEndedEvent(sample, previous, frame, eventBuffer);
                }

                _states[sample.SensorId] = previous.Lifted();
                return;
            }

            if (!TryQueryContact(world, sample, out CombatQueryResult hit, out CombatSurfaceMaterial material))
            {
                if (previous.IsTouching)
                {
                    AddEndedEvent(sample, previous, frame, eventBuffer);
                }

                _states[sample.SensorId] = previous.Lifted();
                return;
            }

            CombatPhysicalContactPhase phase = previous.IsTouching
                && previous.TargetBodyId.Equals(hit.TargetBodyId)
                && previous.TargetColliderId.Equals(hit.TargetColliderId)
                ? CombatPhysicalContactPhase.Stayed
                : CombatPhysicalContactPhase.Started;
            SensorContactState current = new SensorContactState(
                true,
                hit.TargetBodyId,
                hit.TargetColliderId,
                hit.TargetShapeKind,
                hit.Point,
                hit.Normal,
                material);
            AddEvent(sample, current, phase, frame, eventBuffer);
            _states[sample.SensorId] = current;
        }

        private bool TryQueryContact(
            CombatPhysicsWorld world,
            in CombatPhysicalContactSensorSample sample,
            out CombatQueryResult hit,
            out CombatSurfaceMaterial material)
        {
            _queryHits.Clear();
            var header = new CombatQueryHeader(
                sample.SensorId,
                CombatQueryKind.Sphere,
                sample.SourceEntityId,
                sample.TraceId,
                actionId: 0,
                sourceOrder: sample.SourceOrder,
                layerMask: sample.LayerMask);
            var query = new CombatSphereQuery(header, sample.Center, sample.Radius);
            world.QuerySphere(query, _queryHits);

            int selectedIndex = SelectHitIndex(_queryHits, sample.SourceBodyId);
            if (selectedIndex < 0)
            {
                hit = default;
                material = sample.FallbackSurfaceMaterial;
                _queryHits.Clear();
                return false;
            }

            hit = _queryHits[selectedIndex];
            material = ResolveSurfaceMaterial(world, hit, sample.FallbackSurfaceMaterial);
            _queryHits.Clear();
            return true;
        }

        private static int SelectHitIndex(List<CombatQueryResult> hits, CombatBodyId sourceBodyId)
        {
            if (hits == null || hits.Count == 0)
            {
                return -1;
            }

            int fallback = -1;
            for (int i = 0; i < hits.Count; i++)
            {
                CombatQueryResult result = hits[i];
                if (!sourceBodyId.IsNone && result.TargetBodyId.Equals(sourceBodyId))
                {
                    continue;
                }

                if (fallback < 0)
                {
                    fallback = i;
                }

                if (result.Normal.Y >= Fix64.Half)
                {
                    return i;
                }
            }

            return fallback;
        }

        private static CombatSurfaceMaterial ResolveSurfaceMaterial(
            CombatPhysicsWorld world,
            in CombatQueryResult hit,
            CombatSurfaceMaterial fallback)
        {
            if (world == null)
            {
                return fallback;
            }

            switch (hit.TargetShapeKind)
            {
                case CombatPhysicsShapeKind.Sphere:
                    if (world.TryGetSphereCollider(
                        hit.TargetBodyId,
                        hit.TargetColliderId,
                        out CombatPhysicsSphereCollider sphereCollider))
                    {
                        return sphereCollider.SurfaceMaterial;
                    }

                    break;
                case CombatPhysicsShapeKind.Capsule:
                    if (world.TryGetCapsuleCollider(
                        hit.TargetBodyId,
                        hit.TargetColliderId,
                        out CombatPhysicsCapsuleCollider capsuleCollider))
                    {
                        return capsuleCollider.SurfaceMaterial;
                    }

                    break;
                case CombatPhysicsShapeKind.Obb:
                    if (world.TryGetObbCollider(
                        hit.TargetBodyId,
                        hit.TargetColliderId,
                        out CombatPhysicsObbCollider obbCollider))
                    {
                        return obbCollider.SurfaceMaterial;
                    }

                    break;
                default:
                    if (world.TryGetAabbCollider(
                        hit.TargetBodyId,
                        hit.TargetColliderId,
                        out CombatPhysicsAabbCollider aabbCollider))
                    {
                        return aabbCollider.SurfaceMaterial;
                    }

                    break;
            }

            return fallback;
        }

        private static void AddEndedEvent(
            in CombatPhysicalContactSensorSample sample,
            in SensorContactState previous,
            CombatFrame frame,
            CombatPhysicalContactEventBuffer eventBuffer)
        {
            AddEvent(sample, previous, CombatPhysicalContactPhase.Ended, frame, eventBuffer);
        }

        private static void AddEvent(
            in CombatPhysicalContactSensorSample sample,
            in SensorContactState state,
            CombatPhysicalContactPhase phase,
            CombatFrame frame,
            CombatPhysicalContactEventBuffer eventBuffer)
        {
            int intensity = sample.Intensity > 0
                ? sample.Intensity
                : CalculateIntensity(sample);
            int pairKey = CombatPhysicalContactEvent.CreateContactPairKey(
                sample.Kind,
                sample.SourceEntityId,
                sample.SourceBodyId,
                sample.SourceColliderId,
                state.TargetBodyId,
                state.TargetColliderId,
                sample.SourceSlotId,
                sample.ContactPointId,
                sample.BodyPartId);
            var contactEvent = new CombatPhysicalContactEvent(
                frame,
                eventBuffer.Count,
                sample.Kind,
                phase,
                sample.SourceEntityId,
                sample.SourceBodyId,
                sample.SourceColliderId,
                state.TargetBodyId,
                state.TargetColliderId,
                state.TargetShapeKind,
                sample.SourceSlotId,
                pairKey,
                state.Point,
                state.Normal,
                state.SurfaceMaterial,
                sample.RelativeSpeedMillimetersPerSecond,
                sample.NormalSpeedMillimetersPerSecond,
                sample.TangentialSpeedMillimetersPerSecond,
                sample.Pressure,
                intensity,
                sample.ContactPointId,
                sample.BodyPartId,
                sample.ActivationMode);
            eventBuffer.Add(contactEvent);
        }

        private static int CalculateIntensity(in CombatPhysicalContactSensorSample sample)
        {
            int intensity = sample.RelativeSpeedMillimetersPerSecond;
            if (sample.NormalSpeedMillimetersPerSecond > intensity)
            {
                intensity = sample.NormalSpeedMillimetersPerSecond;
            }

            if (sample.TangentialSpeedMillimetersPerSecond > intensity)
            {
                intensity = sample.TangentialSpeedMillimetersPerSecond;
            }

            int pressureIntensity = sample.Pressure / 100;
            return pressureIntensity > intensity ? pressureIntensity : intensity;
        }

        private readonly struct SensorContactState
        {
            public static readonly SensorContactState Empty = new SensorContactState(
                false,
                CombatBodyId.None,
                CombatColliderId.None,
                CombatPhysicsShapeKind.Aabb,
                FixVector3.Zero,
                FixVector3.Zero,
                CombatSurfaceMaterial.Default);

            public SensorContactState(
                bool isTouching,
                CombatBodyId targetBodyId,
                CombatColliderId targetColliderId,
                CombatPhysicsShapeKind targetShapeKind,
                FixVector3 point,
                FixVector3 normal,
                CombatSurfaceMaterial surfaceMaterial)
            {
                IsTouching = isTouching;
                TargetBodyId = targetBodyId;
                TargetColliderId = targetColliderId;
                TargetShapeKind = targetShapeKind;
                Point = point;
                Normal = normal;
                SurfaceMaterial = surfaceMaterial;
            }

            public bool IsTouching { get; }

            public CombatBodyId TargetBodyId { get; }

            public CombatColliderId TargetColliderId { get; }

            public CombatPhysicsShapeKind TargetShapeKind { get; }

            public FixVector3 Point { get; }

            public FixVector3 Normal { get; }

            public CombatSurfaceMaterial SurfaceMaterial { get; }

            public SensorContactState Lifted()
            {
                return new SensorContactState(
                    false,
                    TargetBodyId,
                    TargetColliderId,
                    TargetShapeKind,
                    Point,
                    Normal,
                    SurfaceMaterial);
            }
        }
    }
}
