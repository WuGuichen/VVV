using System;
using System.Collections.Generic;
using MxFramework.Combat.Core;
using MxFramework.Combat.Hit;
using MxFramework.Combat.Physics;
using MxFramework.Core.Math;
using MxFramework.Runtime;

namespace MxFramework.Combat.Projectiles
{
    public readonly struct CombatProjectileSpawnRequest : IEquatable<CombatProjectileSpawnRequest>
    {
        public CombatProjectileSpawnRequest(
            CombatFrame spawnFrame,
            int definitionId,
            CombatEntityId sourceEntityId,
            FixVector3 origin,
            FixVector3 direction,
            int actionId,
            int actionInstanceId,
            int traceId,
            int localFrame,
            int sourceOrder = 0)
            : this(
                spawnFrame,
                definitionId,
                sourceEntityId,
                origin,
                direction,
                false,
                FixVector3.Zero,
                actionId,
                actionInstanceId,
                traceId,
                localFrame,
                sourceOrder,
                effectiveLifetimeFrames: 0)
        {
        }

        public CombatProjectileSpawnRequest(
            CombatFrame spawnFrame,
            int definitionId,
            CombatEntityId sourceEntityId,
            FixVector3 origin,
            FixVector3 direction,
            bool hasTargetPoint,
            FixVector3 targetPoint,
            int actionId,
            int actionInstanceId,
            int traceId,
            int localFrame,
            int sourceOrder = 0,
            int effectiveLifetimeFrames = 0,
            bool hasTrackingTarget = false,
            CombatEntityId trackingTargetEntityId = default)
        {
            if (definitionId <= 0)
                throw new ArgumentOutOfRangeException(nameof(definitionId), "Projectile definition id must be positive.");
            if (sourceEntityId.IsNone)
                throw new ArgumentException("Projectile source entity id cannot be none.", nameof(sourceEntityId));
            if (!direction.TryNormalize(out FixVector3 normalizedDirection))
                throw new ArgumentException("Projectile direction cannot be zero.", nameof(direction));
            if (actionId < 0)
                throw new ArgumentOutOfRangeException(nameof(actionId), "Projectile action id cannot be negative.");
            if (actionInstanceId < 0)
                throw new ArgumentOutOfRangeException(nameof(actionInstanceId), "Projectile action instance id cannot be negative.");
            if (traceId < 0)
                throw new ArgumentOutOfRangeException(nameof(traceId), "Projectile trace id cannot be negative.");
            if (localFrame < 0)
                throw new ArgumentOutOfRangeException(nameof(localFrame), "Projectile local frame cannot be negative.");
            if (sourceOrder < 0)
                throw new ArgumentOutOfRangeException(nameof(sourceOrder), "Projectile source order cannot be negative.");
            if (effectiveLifetimeFrames < 0)
                throw new ArgumentOutOfRangeException(nameof(effectiveLifetimeFrames), "Projectile effective lifetime cannot be negative.");
            if (hasTrackingTarget && trackingTargetEntityId.IsNone)
                throw new ArgumentException("Projectile tracking target entity id cannot be none.", nameof(trackingTargetEntityId));

            SpawnFrame = spawnFrame;
            DefinitionId = definitionId;
            SourceEntityId = sourceEntityId;
            Origin = origin;
            Direction = normalizedDirection;
            HasTargetPoint = hasTargetPoint;
            TargetPoint = hasTargetPoint ? targetPoint : FixVector3.Zero;
            EffectiveLifetimeFrames = effectiveLifetimeFrames;
            HasTrackingTarget = hasTrackingTarget;
            TrackingTargetEntityId = hasTrackingTarget ? trackingTargetEntityId : default;
            ActionId = actionId;
            ActionInstanceId = actionInstanceId;
            TraceId = traceId;
            LocalFrame = localFrame;
            SourceOrder = sourceOrder;
        }

        public CombatFrame SpawnFrame { get; }
        public int DefinitionId { get; }
        public CombatEntityId SourceEntityId { get; }
        public FixVector3 Origin { get; }
        public FixVector3 Direction { get; }
        public bool HasTargetPoint { get; }
        public FixVector3 TargetPoint { get; }
        public int EffectiveLifetimeFrames { get; }
        public bool HasTrackingTarget { get; }
        public CombatEntityId TrackingTargetEntityId { get; }
        public int ActionId { get; }
        public int ActionInstanceId { get; }
        public int TraceId { get; }
        public int LocalFrame { get; }
        public int SourceOrder { get; }

        public bool Equals(CombatProjectileSpawnRequest other)
        {
            return SpawnFrame.Equals(other.SpawnFrame)
                && DefinitionId == other.DefinitionId
                && SourceEntityId.Equals(other.SourceEntityId)
                && Origin.Equals(other.Origin)
                && Direction.Equals(other.Direction)
                && HasTargetPoint == other.HasTargetPoint
                && TargetPoint.Equals(other.TargetPoint)
                && EffectiveLifetimeFrames == other.EffectiveLifetimeFrames
                && HasTrackingTarget == other.HasTrackingTarget
                && TrackingTargetEntityId.Equals(other.TrackingTargetEntityId)
                && ActionId == other.ActionId
                && ActionInstanceId == other.ActionInstanceId
                && TraceId == other.TraceId
                && LocalFrame == other.LocalFrame
                && SourceOrder == other.SourceOrder;
        }

        public override bool Equals(object obj)
        {
            return obj is CombatProjectileSpawnRequest other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = SpawnFrame.GetHashCode();
                hash = (hash * 397) ^ DefinitionId;
                hash = (hash * 397) ^ SourceEntityId.Value;
                hash = (hash * 397) ^ Origin.GetHashCode();
                hash = (hash * 397) ^ Direction.GetHashCode();
                hash = (hash * 397) ^ (HasTargetPoint ? 1 : 0);
                hash = (hash * 397) ^ TargetPoint.GetHashCode();
                hash = (hash * 397) ^ EffectiveLifetimeFrames;
                hash = (hash * 397) ^ (HasTrackingTarget ? 1 : 0);
                hash = (hash * 397) ^ TrackingTargetEntityId.Value;
                hash = (hash * 397) ^ ActionId;
                hash = (hash * 397) ^ ActionInstanceId;
                hash = (hash * 397) ^ TraceId;
                hash = (hash * 397) ^ LocalFrame;
                hash = (hash * 397) ^ SourceOrder;
                return hash;
            }
        }
    }

    public interface ICombatProjectileSpawnSource
    {
        int CollectSpawnRequests(CombatFrame frame, List<CombatProjectileSpawnRequest> output);
    }

    /// <summary>
    /// Supplies optional direct-hit sweep queries for projectile shapes that need more than the default center capsule.
    /// </summary>
    /// <remarks>NoAlloc after initialization; implementations append into caller-owned buffers.</remarks>
    public interface ICombatProjectileDirectSweepProvider
    {
        /// <summary>
        /// Appends direct-hit capsule sweeps for a projectile frame. Return zero to use the runtime default center capsule.
        /// </summary>
        /// <remarks>NoAlloc after initialization when <paramref name="output"/> capacity is warmed by the caller.</remarks>
        int CollectDirectSweepQueries(
            CombatFrame frame,
            CombatProjectileSnapshot projectile,
            CombatProjectileDefinition definition,
            CombatQueryHeader header,
            FixVector3 previousPosition,
            FixVector3 currentPosition,
            List<CombatCapsuleQuery> output);
    }

    public readonly struct CombatProjectileTrackingTargetQuery : IEquatable<CombatProjectileTrackingTargetQuery>
    {
        public CombatProjectileTrackingTargetQuery(
            CombatFrame frame,
            int projectileId,
            int definitionId,
            CombatEntityId sourceEntityId,
            CombatEntityId targetEntityId,
            FixVector3 position,
            FixVector3 direction)
        {
            if (projectileId <= 0)
                throw new ArgumentOutOfRangeException(nameof(projectileId), "Projectile id must be positive.");
            if (definitionId <= 0)
                throw new ArgumentOutOfRangeException(nameof(definitionId), "Projectile definition id must be positive.");
            if (sourceEntityId.IsNone)
                throw new ArgumentException("Projectile source entity id cannot be none.", nameof(sourceEntityId));
            if (targetEntityId.IsNone)
                throw new ArgumentException("Projectile tracking target entity id cannot be none.", nameof(targetEntityId));

            Frame = frame;
            ProjectileId = projectileId;
            DefinitionId = definitionId;
            SourceEntityId = sourceEntityId;
            TargetEntityId = targetEntityId;
            Position = position;
            Direction = direction;
        }

        public CombatFrame Frame { get; }
        public int ProjectileId { get; }
        public int DefinitionId { get; }
        public CombatEntityId SourceEntityId { get; }
        public CombatEntityId TargetEntityId { get; }
        public FixVector3 Position { get; }
        public FixVector3 Direction { get; }

        public bool Equals(CombatProjectileTrackingTargetQuery other)
        {
            return Frame.Equals(other.Frame)
                && ProjectileId == other.ProjectileId
                && DefinitionId == other.DefinitionId
                && SourceEntityId.Equals(other.SourceEntityId)
                && TargetEntityId.Equals(other.TargetEntityId)
                && Position.Equals(other.Position)
                && Direction.Equals(other.Direction);
        }

        public override bool Equals(object obj)
        {
            return obj is CombatProjectileTrackingTargetQuery other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = Frame.GetHashCode();
                hash = (hash * 397) ^ ProjectileId;
                hash = (hash * 397) ^ DefinitionId;
                hash = (hash * 397) ^ SourceEntityId.Value;
                hash = (hash * 397) ^ TargetEntityId.Value;
                hash = (hash * 397) ^ Position.GetHashCode();
                hash = (hash * 397) ^ Direction.GetHashCode();
                return hash;
            }
        }
    }

    public interface ICombatProjectileTrackingTargetProvider
    {
        bool TryResolveTrackingTarget(
            CombatProjectileTrackingTargetQuery query,
            out FixVector3 targetPoint);
    }

    public sealed class CombatProjectileSpawnRequestBuffer : ICombatProjectileSpawnSource
    {
        private readonly List<CombatProjectileSpawnRequest> _requests =
            new List<CombatProjectileSpawnRequest>();

        public int Count => _requests.Count;

        public void Enqueue(CombatProjectileSpawnRequest request)
        {
            _requests.Add(request);
        }

        public int CollectSpawnRequests(CombatFrame frame, List<CombatProjectileSpawnRequest> output)
        {
            if (output == null)
                throw new ArgumentNullException(nameof(output));

            int start = output.Count;
            for (int i = _requests.Count - 1; i >= 0; i--)
            {
                CombatProjectileSpawnRequest request = _requests[i];
                if (request.SpawnFrame > frame)
                    continue;

                output.Add(request);
                _requests.RemoveAt(i);
            }

            output.Sort(start, output.Count - start, CombatProjectileSpawnRequestComparer.Instance);
            return output.Count - start;
        }

        public void Clear()
        {
            _requests.Clear();
        }
    }

    public readonly struct CombatProjectileImpactPayload : IEquatable<CombatProjectileImpactPayload>
    {
        public CombatProjectileImpactPayload(
            int damage,
            int staggerFrames,
            FixVector3 knockback,
            HitTargetStateFlags targetState,
            int resolvePriority = 0,
            string presentationProfileId = "",
            string stableEventId = "")
        {
            if (damage < 0)
                throw new ArgumentOutOfRangeException(nameof(damage), "Projectile impact damage cannot be negative.");
            if (staggerFrames < 0)
                throw new ArgumentOutOfRangeException(nameof(staggerFrames), "Projectile impact stagger cannot be negative.");

            Damage = damage;
            StaggerFrames = staggerFrames;
            Knockback = knockback;
            TargetState = targetState;
            ResolvePriority = resolvePriority;
            PresentationProfileId = presentationProfileId ?? string.Empty;
            StableEventId = stableEventId ?? string.Empty;
        }

        public int Damage { get; }
        public int StaggerFrames { get; }
        public FixVector3 Knockback { get; }
        public HitTargetStateFlags TargetState { get; }
        public int ResolvePriority { get; }
        public string PresentationProfileId { get; }
        public string StableEventId { get; }

        public bool Equals(CombatProjectileImpactPayload other)
        {
            return Damage == other.Damage
                && StaggerFrames == other.StaggerFrames
                && Knockback.Equals(other.Knockback)
                && TargetState == other.TargetState
                && ResolvePriority == other.ResolvePriority
                && string.Equals(PresentationProfileId, other.PresentationProfileId, StringComparison.Ordinal)
                && string.Equals(StableEventId, other.StableEventId, StringComparison.Ordinal);
        }

        public override bool Equals(object obj)
        {
            return obj is CombatProjectileImpactPayload other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = Damage;
                hash = (hash * 397) ^ StaggerFrames;
                hash = (hash * 397) ^ Knockback.GetHashCode();
                hash = (hash * 397) ^ (int)TargetState;
                hash = (hash * 397) ^ ResolvePriority;
                hash = (hash * 397) ^ (PresentationProfileId == null ? 0 : StringComparer.Ordinal.GetHashCode(PresentationProfileId));
                hash = (hash * 397) ^ (StableEventId == null ? 0 : StringComparer.Ordinal.GetHashCode(StableEventId));
                return hash;
            }
        }
    }

    public enum CombatProjectileImpactSourceKind
    {
        DirectSweep = 0,
        AreaPulse = 1,
    }

    public enum CombatProjectileAreaPulseTriggerKind
    {
        DirectSweep = 0,
        Expiration = 1,
    }

    public readonly struct CombatProjectileImpactContext : IEquatable<CombatProjectileImpactContext>
    {
        public CombatProjectileImpactContext(CombatProjectileImpactSourceKind sourceKind)
        {
            if (!Enum.IsDefined(typeof(CombatProjectileImpactSourceKind), sourceKind))
                throw new ArgumentOutOfRangeException(nameof(sourceKind), "Projectile impact source kind is not supported.");

            SourceKind = sourceKind;
        }

        public CombatProjectileImpactSourceKind SourceKind { get; }
        public bool IsAreaPulse => SourceKind == CombatProjectileImpactSourceKind.AreaPulse;

        public bool Equals(CombatProjectileImpactContext other)
        {
            return SourceKind == other.SourceKind;
        }

        public override bool Equals(object obj)
        {
            return obj is CombatProjectileImpactContext other && Equals(other);
        }

        public override int GetHashCode()
        {
            return (int)SourceKind;
        }
    }

    public interface ICombatProjectileImpactResolver
    {
        bool TryResolveImpact(
            CombatProjectileSnapshot projectile,
            CombatQueryResult hit,
            CombatFrame frame,
            CombatProjectileImpactContext context,
            out CombatProjectileImpactPayload payload);
    }

    internal static class CombatProjectileHitRecordHashFields
    {
        public const int TargetEntityId = 1;
        public const int Frame = 2;
    }

    public readonly struct CombatProjectileHitRecord : IEquatable<CombatProjectileHitRecord>
    {
        public CombatProjectileHitRecord(CombatEntityId targetEntityId, CombatFrame frame)
        {
            if (targetEntityId.IsNone)
                throw new ArgumentException("Projectile hit record target entity id cannot be none.", nameof(targetEntityId));

            TargetEntityId = targetEntityId;
            Frame = frame;
        }

        public CombatEntityId TargetEntityId { get; }
        public CombatFrame Frame { get; }

        public bool Equals(CombatProjectileHitRecord other)
        {
            return TargetEntityId.Equals(other.TargetEntityId)
                && Frame.Equals(other.Frame);
        }

        public override bool Equals(object obj)
        {
            return obj is CombatProjectileHitRecord other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return (TargetEntityId.Value * 397) ^ Frame.Value;
            }
        }

        internal void Contribute(RuntimeHashAccumulator accumulator)
        {
            accumulator.AddInt(CombatProjectileHitRecordHashFields.TargetEntityId, TargetEntityId.Value);
            accumulator.AddInt(CombatProjectileHitRecordHashFields.Frame, Frame.Value);
        }
    }

    internal static class CombatProjectileSnapshotHashFields
    {
        public const int ProjectileId = 1;
        public const int SpawnSequence = 2;
        public const int DefinitionId = 3;
        public const int SourceEntityId = 4;
        public const int ActionId = 5;
        public const int ActionInstanceId = 6;
        public const int TraceId = 7;
        public const int LocalFrame = 8;
        public const int SpawnFrame = 9;
        public const int LastFrame = 10;
        public const int AgeFrames = 11;
        public const int PositionX = 12;
        public const int PositionY = 13;
        public const int PositionZ = 14;
        public const int DirectionX = 15;
        public const int DirectionY = 16;
        public const int DirectionZ = 17;
        public const int OriginX = 18;
        public const int OriginY = 19;
        public const int OriginZ = 20;
        public const int HasTargetPoint = 21;
        public const int TargetPointX = 22;
        public const int TargetPointY = 23;
        public const int TargetPointZ = 24;
        public const int EffectiveLifetimeFrames = 25;
        public const int LaunchSpeedPerFrame = 26;
        public const int HasTrackingTarget = 27;
        public const int TrackingTargetEntityId = 28;
        public const int AcceptedImpactCount = 29;
        public const int AcceptedImpacts = 30;
    }

    public readonly struct CombatProjectileSnapshot : IEquatable<CombatProjectileSnapshot>
    {
        public CombatProjectileSnapshot(
            int projectileId,
            int spawnSequence,
            int definitionId,
            string definitionStableId,
            CombatEntityId sourceEntityId,
            int actionId,
            int actionInstanceId,
            int traceId,
            int localFrame,
            CombatFrame spawnFrame,
            CombatFrame lastFrame,
            int ageFrames,
            FixVector3 position,
            FixVector3 direction,
            IReadOnlyList<CombatProjectileHitRecord> acceptedImpacts = null)
            : this(
                projectileId,
                spawnSequence,
                definitionId,
                    definitionStableId,
                sourceEntityId,
                actionId,
                actionInstanceId,
                traceId,
                localFrame,
                spawnFrame,
                lastFrame,
                ageFrames,
                position,
                direction,
                position,
                false,
                FixVector3.Zero,
                acceptedImpacts,
                effectiveLifetimeFrames: 0)
        {
        }

        public CombatProjectileSnapshot(
            int projectileId,
            int spawnSequence,
            int definitionId,
            string definitionStableId,
            CombatEntityId sourceEntityId,
            int actionId,
            int actionInstanceId,
            int traceId,
            int localFrame,
            CombatFrame spawnFrame,
            CombatFrame lastFrame,
            int ageFrames,
            FixVector3 position,
            FixVector3 direction,
            FixVector3 origin,
            bool hasTargetPoint,
            FixVector3 targetPoint,
            IReadOnlyList<CombatProjectileHitRecord> acceptedImpacts = null,
            int effectiveLifetimeFrames = 0,
            bool hasTrackingTarget = false,
            CombatEntityId trackingTargetEntityId = default,
            Fix64 launchSpeedPerFrame = default,
            RuntimeHashDigest definitionLabelDigest = default)
            : this(projectileId, spawnSequence, definitionId, definitionStableId, sourceEntityId, actionId, actionInstanceId, traceId, localFrame, spawnFrame, lastFrame, ageFrames, position, direction, origin, hasTargetPoint, targetPoint, acceptedImpacts, effectiveLifetimeFrames, hasTrackingTarget, trackingTargetEntityId, launchSpeedPerFrame, false, definitionLabelDigest)
        { }

        internal CombatProjectileSnapshot(
            int projectileId,
            int spawnSequence,
            int definitionId,
            string definitionStableId,
            CombatEntityId sourceEntityId,
            int actionId,
            int actionInstanceId,
            int traceId,
            int localFrame,
            CombatFrame spawnFrame,
            CombatFrame lastFrame,
            int ageFrames,
            FixVector3 position,
            FixVector3 direction,
            FixVector3 origin,
            bool hasTargetPoint,
            FixVector3 targetPoint,
            IReadOnlyList<CombatProjectileHitRecord> acceptedImpacts,
            int effectiveLifetimeFrames,
            bool hasTrackingTarget,
            CombatEntityId trackingTargetEntityId,
            Fix64 launchSpeedPerFrame,
            bool borrow,
            RuntimeHashDigest definitionLabelDigest)
{
            if (projectileId <= 0)
                throw new ArgumentOutOfRangeException(nameof(projectileId), "Projectile id must be positive.");
            if (spawnSequence < 0)
                throw new ArgumentOutOfRangeException(nameof(spawnSequence), "Projectile spawn sequence cannot be negative.");
            if (definitionId <= 0)
                throw new ArgumentOutOfRangeException(nameof(definitionId), "Projectile definition id must be positive.");
            if (sourceEntityId.IsNone)
                throw new ArgumentException("Projectile source entity id cannot be none.", nameof(sourceEntityId));
            if (!direction.TryNormalize(out _))
                throw new ArgumentException("Projectile direction cannot be zero.", nameof(direction));
            if (ageFrames < 0)
                throw new ArgumentOutOfRangeException(nameof(ageFrames), "Projectile age cannot be negative.");
            if (effectiveLifetimeFrames < 0)
                throw new ArgumentOutOfRangeException(nameof(effectiveLifetimeFrames), "Projectile effective lifetime cannot be negative.");
            if (launchSpeedPerFrame < Fix64.Zero)
                throw new ArgumentOutOfRangeException(nameof(launchSpeedPerFrame), "Projectile launch speed cannot be negative.");
            if (hasTrackingTarget && trackingTargetEntityId.IsNone)
                throw new ArgumentException("Projectile tracking target entity id cannot be none.", nameof(trackingTargetEntityId));

            ProjectileId = projectileId;
            SpawnSequence = spawnSequence;
            DefinitionId = definitionId;
            DefinitionStableId = definitionStableId ?? string.Empty;
            DefinitionLabelDigest = definitionLabelDigest;
            SourceEntityId = sourceEntityId;
            ActionId = actionId < 0 ? 0 : actionId;
            ActionInstanceId = actionInstanceId < 0 ? 0 : actionInstanceId;
            TraceId = traceId < 0 ? 0 : traceId;
            LocalFrame = localFrame < 0 ? 0 : localFrame;
            SpawnFrame = spawnFrame;
            LastFrame = lastFrame;
            AgeFrames = ageFrames;
            Position = position;
            Direction = direction;
            Origin = origin;
            HasTargetPoint = hasTargetPoint;
            TargetPoint = hasTargetPoint ? targetPoint : FixVector3.Zero;
            EffectiveLifetimeFrames = effectiveLifetimeFrames;
            LaunchSpeedPerFrame = launchSpeedPerFrame;
            HasTrackingTarget = hasTrackingTarget;
            TrackingTargetEntityId = hasTrackingTarget ? trackingTargetEntityId : default;
            AcceptedImpacts = borrow ? acceptedImpacts ?? Array.Empty<CombatProjectileHitRecord>() : CombatProjectileReadOnly.Copy(acceptedImpacts);
        }

        public int ProjectileId { get; }
        public int SpawnSequence { get; }
        public int DefinitionId { get; }
        public string DefinitionStableId { get; }
        public RuntimeHashDigest DefinitionLabelDigest { get; }
        public CombatEntityId SourceEntityId { get; }
        public int ActionId { get; }
        public int ActionInstanceId { get; }
        public int TraceId { get; }
        public int LocalFrame { get; }
        public CombatFrame SpawnFrame { get; }
        public CombatFrame LastFrame { get; }
        public int AgeFrames { get; }
        public FixVector3 Position { get; }
        public FixVector3 Direction { get; }
        public FixVector3 Origin { get; }
        public bool HasTargetPoint { get; }
        public FixVector3 TargetPoint { get; }
        public int EffectiveLifetimeFrames { get; }
        public Fix64 LaunchSpeedPerFrame { get; }
        public bool HasTrackingTarget { get; }
        public CombatEntityId TrackingTargetEntityId { get; }
        public IReadOnlyList<CombatProjectileHitRecord> AcceptedImpacts { get; }
        public int AcceptedImpactCount => AcceptedImpacts.Count;

        public bool Equals(CombatProjectileSnapshot other)
        {
            if (!(ProjectileId == other.ProjectileId
                && SpawnSequence == other.SpawnSequence
                && DefinitionId == other.DefinitionId
                && string.Equals(DefinitionStableId, other.DefinitionStableId, StringComparison.Ordinal)
                && SourceEntityId.Equals(other.SourceEntityId)
                && ActionId == other.ActionId
                && ActionInstanceId == other.ActionInstanceId
                && TraceId == other.TraceId
                && LocalFrame == other.LocalFrame
                && SpawnFrame.Equals(other.SpawnFrame)
                && LastFrame.Equals(other.LastFrame)
                && AgeFrames == other.AgeFrames
                && Position.Equals(other.Position)
                && Direction.Equals(other.Direction)
                && Origin.Equals(other.Origin)
                && HasTargetPoint == other.HasTargetPoint
                && TargetPoint.Equals(other.TargetPoint)
                && EffectiveLifetimeFrames == other.EffectiveLifetimeFrames
                && LaunchSpeedPerFrame.Equals(other.LaunchSpeedPerFrame)
                && HasTrackingTarget == other.HasTrackingTarget
                && TrackingTargetEntityId.Equals(other.TrackingTargetEntityId)))
            {
                return false;
            }

            if (AcceptedImpacts.Count != other.AcceptedImpacts.Count)
                return false;

            for (int i = 0; i < AcceptedImpacts.Count; i++)
            {
                if (!AcceptedImpacts[i].Equals(other.AcceptedImpacts[i]))
                    return false;
            }

            return true;
        }

        public override bool Equals(object obj)
        {
            return obj is CombatProjectileSnapshot other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = ProjectileId;
                hash = (hash * 397) ^ SpawnSequence;
                hash = (hash * 397) ^ DefinitionId;
                hash = (hash * 397) ^ (DefinitionStableId == null ? 0 : StringComparer.Ordinal.GetHashCode(DefinitionStableId));
                hash = (hash * 397) ^ SourceEntityId.Value;
                hash = (hash * 397) ^ ActionId;
                hash = (hash * 397) ^ ActionInstanceId;
                hash = (hash * 397) ^ TraceId;
                hash = (hash * 397) ^ LocalFrame;
                hash = (hash * 397) ^ SpawnFrame.Value;
                hash = (hash * 397) ^ LastFrame.Value;
                hash = (hash * 397) ^ AgeFrames;
                hash = (hash * 397) ^ Position.GetHashCode();
                hash = (hash * 397) ^ Direction.GetHashCode();
                hash = (hash * 397) ^ Origin.GetHashCode();
                hash = (hash * 397) ^ (HasTargetPoint ? 1 : 0);
                hash = (hash * 397) ^ TargetPoint.GetHashCode();
                hash = (hash * 397) ^ EffectiveLifetimeFrames;
                hash = (hash * 397) ^ LaunchSpeedPerFrame.GetHashCode();
                hash = (hash * 397) ^ (HasTrackingTarget ? 1 : 0);
                hash = (hash * 397) ^ TrackingTargetEntityId.Value;
                hash = (hash * 397) ^ AcceptedImpacts.Count;
                for (int i = 0; i < AcceptedImpacts.Count; i++)
                    hash = (hash * 397) ^ AcceptedImpacts[i].GetHashCode();
                return hash;
            }
        }

        internal void Contribute(RuntimeHashAccumulator accumulator)
        {
            accumulator.AddInt(CombatProjectileSnapshotHashFields.ProjectileId, ProjectileId);
            accumulator.AddInt(CombatProjectileSnapshotHashFields.SpawnSequence, SpawnSequence);
            accumulator.AddInt(CombatProjectileSnapshotHashFields.DefinitionId, DefinitionId);
            accumulator.AddInt(CombatProjectileSnapshotHashFields.SourceEntityId, SourceEntityId.Value);
            accumulator.AddInt(CombatProjectileSnapshotHashFields.ActionId, ActionId);
            accumulator.AddInt(CombatProjectileSnapshotHashFields.ActionInstanceId, ActionInstanceId);
            accumulator.AddInt(CombatProjectileSnapshotHashFields.TraceId, TraceId);
            accumulator.AddInt(CombatProjectileSnapshotHashFields.LocalFrame, LocalFrame);
            accumulator.AddInt(CombatProjectileSnapshotHashFields.SpawnFrame, SpawnFrame.Value);
            accumulator.AddInt(CombatProjectileSnapshotHashFields.LastFrame, LastFrame.Value);
            accumulator.AddInt(CombatProjectileSnapshotHashFields.AgeFrames, AgeFrames);
            accumulator.AddLong(CombatProjectileSnapshotHashFields.PositionX, Position.X.RawValue);
            accumulator.AddLong(CombatProjectileSnapshotHashFields.PositionY, Position.Y.RawValue);
            accumulator.AddLong(CombatProjectileSnapshotHashFields.PositionZ, Position.Z.RawValue);
            accumulator.AddLong(CombatProjectileSnapshotHashFields.DirectionX, Direction.X.RawValue);
            accumulator.AddLong(CombatProjectileSnapshotHashFields.DirectionY, Direction.Y.RawValue);
            accumulator.AddLong(CombatProjectileSnapshotHashFields.DirectionZ, Direction.Z.RawValue);
            accumulator.AddLong(CombatProjectileSnapshotHashFields.OriginX, Origin.X.RawValue);
            accumulator.AddLong(CombatProjectileSnapshotHashFields.OriginY, Origin.Y.RawValue);
            accumulator.AddLong(CombatProjectileSnapshotHashFields.OriginZ, Origin.Z.RawValue);
            accumulator.AddInt(CombatProjectileSnapshotHashFields.HasTargetPoint, HasTargetPoint ? 1 : 0);
            accumulator.AddLong(CombatProjectileSnapshotHashFields.TargetPointX, TargetPoint.X.RawValue);
            accumulator.AddLong(CombatProjectileSnapshotHashFields.TargetPointY, TargetPoint.Y.RawValue);
            accumulator.AddLong(CombatProjectileSnapshotHashFields.TargetPointZ, TargetPoint.Z.RawValue);
            accumulator.AddInt(CombatProjectileSnapshotHashFields.EffectiveLifetimeFrames, EffectiveLifetimeFrames);
            accumulator.AddLong(CombatProjectileSnapshotHashFields.LaunchSpeedPerFrame, LaunchSpeedPerFrame.RawValue);
            accumulator.AddInt(CombatProjectileSnapshotHashFields.HasTrackingTarget, HasTrackingTarget ? 1 : 0);
            accumulator.AddInt(CombatProjectileSnapshotHashFields.TrackingTargetEntityId, TrackingTargetEntityId.Value);
            accumulator.AddInt(CombatProjectileSnapshotHashFields.AcceptedImpactCount, AcceptedImpacts.Count);
            accumulator.BeginArray(CombatProjectileSnapshotHashFields.AcceptedImpacts, AcceptedImpacts.Count);
            for (int i = 0; i < AcceptedImpacts.Count; i++)
            {
                accumulator.BeginElement(i);
                AcceptedImpacts[i].Contribute(accumulator);
                accumulator.EndElement();
            }
            accumulator.EndArray();
        }
    }

    public sealed class CombatProjectileRuntimeState
    {
        public const int CurrentSchemaVersion = 1;

        public CombatProjectileRuntimeState(
            int schemaVersion,
            string moduleId,
            long definitionHash,
            int nextProjectileId,
            IReadOnlyList<CombatProjectileSnapshot> activeProjectiles)
            : this(schemaVersion, moduleId, definitionHash, nextProjectileId, activeProjectiles, false)
        { }

        internal CombatProjectileRuntimeState(
            int schemaVersion,
            string moduleId,
            long definitionHash,
            int nextProjectileId,
            IReadOnlyList<CombatProjectileSnapshot> activeProjectiles,
            bool borrow)
{
            if (nextProjectileId <= 0)
                throw new ArgumentOutOfRangeException(nameof(nextProjectileId), "Next projectile id must be positive.");

            SchemaVersion = schemaVersion;
            ModuleId = moduleId ?? string.Empty;
            DefinitionHash = definitionHash;
            NextProjectileId = nextProjectileId;
            ActiveProjectiles = borrow ? activeProjectiles : CombatProjectileReadOnly.Copy(activeProjectiles);
        }

        public int SchemaVersion { get; }
        public string ModuleId { get; internal set; }
        public long DefinitionHash { get; internal set; }
        public int NextProjectileId { get; internal set; }
        public IReadOnlyList<CombatProjectileSnapshot> ActiveProjectiles { get; }
        public int ActiveCount => ActiveProjectiles.Count;
    }

    /// <summary>
    /// NoAlloc projectile view for Tick/presentation/field logic. It intentionally excludes
    /// accepted-hit history and debug snapshot collections.
    /// </summary>
    public readonly struct CombatProjectileActiveView : IEquatable<CombatProjectileActiveView>
    {
        public CombatProjectileActiveView(
            int projectileId,
            int spawnSequence,
            int definitionId,
            string definitionStableId,
            CombatEntityId sourceEntityId,
            int actionId,
            int actionInstanceId,
            int traceId,
            int localFrame,
            CombatFrame spawnFrame,
            CombatFrame lastFrame,
            int ageFrames,
            FixVector3 position,
            FixVector3 direction,
            FixVector3 origin,
            bool hasTargetPoint,
            FixVector3 targetPoint,
            int effectiveLifetimeFrames,
            bool hasTrackingTarget,
            CombatEntityId trackingTargetEntityId,
            int acceptedImpactCount,
            Fix64 launchSpeedPerFrame = default)
        {
            if (projectileId <= 0)
                throw new ArgumentOutOfRangeException(nameof(projectileId), "Projectile id must be positive.");
            if (definitionId <= 0)
                throw new ArgumentOutOfRangeException(nameof(definitionId), "Projectile definition id must be positive.");
            if (sourceEntityId.IsNone)
                throw new ArgumentException("Projectile source entity id cannot be none.", nameof(sourceEntityId));
            if (!direction.TryNormalize(out FixVector3 normalizedDirection))
                throw new ArgumentException("Projectile direction cannot be zero.", nameof(direction));

            ProjectileId = projectileId;
            SpawnSequence = spawnSequence < 0 ? 0 : spawnSequence;
            DefinitionId = definitionId;
            DefinitionStableId = definitionStableId ?? string.Empty;
            SourceEntityId = sourceEntityId;
            ActionId = actionId < 0 ? 0 : actionId;
            ActionInstanceId = actionInstanceId < 0 ? 0 : actionInstanceId;
            TraceId = traceId < 0 ? 0 : traceId;
            LocalFrame = localFrame < 0 ? 0 : localFrame;
            SpawnFrame = spawnFrame;
            LastFrame = lastFrame;
            AgeFrames = ageFrames < 0 ? 0 : ageFrames;
            Position = position;
            Direction = normalizedDirection;
            Origin = origin;
            HasTargetPoint = hasTargetPoint;
            TargetPoint = hasTargetPoint ? targetPoint : FixVector3.Zero;
            EffectiveLifetimeFrames = effectiveLifetimeFrames < 0 ? 0 : effectiveLifetimeFrames;
            LaunchSpeedPerFrame = launchSpeedPerFrame < Fix64.Zero ? Fix64.Zero : launchSpeedPerFrame;
            HasTrackingTarget = hasTrackingTarget;
            TrackingTargetEntityId = hasTrackingTarget ? trackingTargetEntityId : default;
            AcceptedImpactCount = acceptedImpactCount < 0 ? 0 : acceptedImpactCount;
        }

        public int ProjectileId { get; }
        public int SpawnSequence { get; }
        public int DefinitionId { get; }
        public string DefinitionStableId { get; }
        public CombatEntityId SourceEntityId { get; }
        public int ActionId { get; }
        public int ActionInstanceId { get; }
        public int TraceId { get; }
        public int LocalFrame { get; }
        public CombatFrame SpawnFrame { get; }
        public CombatFrame LastFrame { get; }
        public int AgeFrames { get; }
        public FixVector3 Position { get; }
        public FixVector3 Direction { get; }
        public FixVector3 Origin { get; }
        public bool HasTargetPoint { get; }
        public FixVector3 TargetPoint { get; }
        public int EffectiveLifetimeFrames { get; }
        public Fix64 LaunchSpeedPerFrame { get; }
        public bool HasTrackingTarget { get; }
        public CombatEntityId TrackingTargetEntityId { get; }
        public int AcceptedImpactCount { get; }

        public bool Equals(CombatProjectileActiveView other)
        {
            return ProjectileId == other.ProjectileId
                && SpawnSequence == other.SpawnSequence
                && DefinitionId == other.DefinitionId
                && string.Equals(DefinitionStableId, other.DefinitionStableId, StringComparison.Ordinal)
                && SourceEntityId.Equals(other.SourceEntityId)
                && ActionId == other.ActionId
                && ActionInstanceId == other.ActionInstanceId
                && TraceId == other.TraceId
                && LocalFrame == other.LocalFrame
                && SpawnFrame.Equals(other.SpawnFrame)
                && LastFrame.Equals(other.LastFrame)
                && AgeFrames == other.AgeFrames
                && Position.Equals(other.Position)
                && Direction.Equals(other.Direction)
                && Origin.Equals(other.Origin)
                && HasTargetPoint == other.HasTargetPoint
                && TargetPoint.Equals(other.TargetPoint)
                && EffectiveLifetimeFrames == other.EffectiveLifetimeFrames
                && LaunchSpeedPerFrame.Equals(other.LaunchSpeedPerFrame)
                && HasTrackingTarget == other.HasTrackingTarget
                && TrackingTargetEntityId.Equals(other.TrackingTargetEntityId)
                && AcceptedImpactCount == other.AcceptedImpactCount;
        }

        public override bool Equals(object obj)
        {
            return obj is CombatProjectileActiveView other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = ProjectileId;
                hash = (hash * 397) ^ SpawnSequence;
                hash = (hash * 397) ^ DefinitionId;
                hash = (hash * 397) ^ (DefinitionStableId == null ? 0 : StringComparer.Ordinal.GetHashCode(DefinitionStableId));
                hash = (hash * 397) ^ SourceEntityId.Value;
                hash = (hash * 397) ^ ActionId;
                hash = (hash * 397) ^ ActionInstanceId;
                hash = (hash * 397) ^ TraceId;
                hash = (hash * 397) ^ LocalFrame;
                hash = (hash * 397) ^ SpawnFrame.Value;
                hash = (hash * 397) ^ LastFrame.Value;
                hash = (hash * 397) ^ AgeFrames;
                hash = (hash * 397) ^ Position.GetHashCode();
                hash = (hash * 397) ^ Direction.GetHashCode();
                hash = (hash * 397) ^ Origin.GetHashCode();
                hash = (hash * 397) ^ (HasTargetPoint ? 1 : 0);
                hash = (hash * 397) ^ TargetPoint.GetHashCode();
                hash = (hash * 397) ^ EffectiveLifetimeFrames;
                hash = (hash * 397) ^ LaunchSpeedPerFrame.GetHashCode();
                hash = (hash * 397) ^ (HasTrackingTarget ? 1 : 0);
                hash = (hash * 397) ^ TrackingTargetEntityId.Value;
                hash = (hash * 397) ^ AcceptedImpactCount;
                return hash;
            }
        }
    }

    /// <summary>
    /// Caller-owned projectile frame event buffer. Reusing one instance avoids
    /// CombatProjectileFrameEvents list/read-only wrapper copies on Tick.
    /// </summary>
    public sealed class CombatProjectileFrameEventBuffer
    {
        private readonly List<CombatProjectileSpawnEvent> _spawns;
        private readonly List<CombatProjectileAreaPulseTriggerEvent> _areaPulseTriggers;
        private readonly List<CombatProjectileImpactEvent> _impacts;
        private readonly List<CombatProjectileExpiredEvent> _expirations;
        private readonly List<CombatProjectileRejectedEvent> _rejections;
        private readonly List<CombatProjectilePhysicalEvent> _physicalEvents;

        public CombatProjectileFrameEventBuffer(
            int spawnCapacity = 4,
            int areaPulseTriggerCapacity = 4,
            int impactCapacity = 8,
            int expirationCapacity = 4,
            int rejectionCapacity = 2,
            int physicalEventCapacity = 8)
        {
            _spawns = new List<CombatProjectileSpawnEvent>(Math.Max(0, spawnCapacity));
            _areaPulseTriggers =
                new List<CombatProjectileAreaPulseTriggerEvent>(Math.Max(0, areaPulseTriggerCapacity));
            _impacts = new List<CombatProjectileImpactEvent>(Math.Max(0, impactCapacity));
            _expirations = new List<CombatProjectileExpiredEvent>(Math.Max(0, expirationCapacity));
            _rejections = new List<CombatProjectileRejectedEvent>(Math.Max(0, rejectionCapacity));
            _physicalEvents = new List<CombatProjectilePhysicalEvent>(Math.Max(0, physicalEventCapacity));
        }

        public IReadOnlyList<CombatProjectileSpawnEvent> Spawns => _spawns;
        public IReadOnlyList<CombatProjectileAreaPulseTriggerEvent> AreaPulseTriggers => _areaPulseTriggers;
        public IReadOnlyList<CombatProjectileImpactEvent> Impacts => _impacts;
        public IReadOnlyList<CombatProjectileExpiredEvent> Expirations => _expirations;
        public IReadOnlyList<CombatProjectileRejectedEvent> Rejections => _rejections;
        public IReadOnlyList<CombatProjectilePhysicalEvent> PhysicalEvents => _physicalEvents;
        public bool HasEvents =>
            _spawns.Count != 0
            || _areaPulseTriggers.Count != 0
            || _impacts.Count != 0
            || _expirations.Count != 0
            || _rejections.Count != 0
            || _physicalEvents.Count != 0;

        public void Clear()
        {
            _spawns.Clear();
            _areaPulseTriggers.Clear();
            _impacts.Clear();
            _expirations.Clear();
            _rejections.Clear();
            _physicalEvents.Clear();
        }

        internal void CopyFrom(
            IReadOnlyList<CombatProjectileSpawnEvent> spawns,
            IReadOnlyList<CombatProjectileAreaPulseTriggerEvent> areaPulseTriggers,
            IReadOnlyList<CombatProjectileImpactEvent> impacts,
            IReadOnlyList<CombatProjectileExpiredEvent> expirations,
            IReadOnlyList<CombatProjectileRejectedEvent> rejections,
            IReadOnlyList<CombatProjectilePhysicalEvent> physicalEvents)
        {
            Clear();
            Copy(spawns, _spawns);
            Copy(areaPulseTriggers, _areaPulseTriggers);
            Copy(impacts, _impacts);
            Copy(expirations, _expirations);
            Copy(rejections, _rejections);
            Copy(physicalEvents, _physicalEvents);
        }

        internal void CopyFrom(CombatProjectileFrameEventBuffer source)
        {
            if (source == null)
            {
                Clear();
                return;
            }

            CopyFrom(
                source.Spawns,
                source.AreaPulseTriggers,
                source.Impacts,
                source.Expirations,
                source.Rejections,
                source.PhysicalEvents);
        }

        private static void Copy<T>(IReadOnlyList<T> source, List<T> destination)
        {
            if (source == null || source.Count == 0)
                return;

            for (int i = 0; i < source.Count; i++)
                destination.Add(source[i]);
        }
    }

    public readonly struct CombatProjectileSpawnEvent : IEquatable<CombatProjectileSpawnEvent>
    {
        public CombatProjectileSpawnEvent(CombatProjectileSnapshot projectile)
        {
            Projectile = projectile;
        }

        public CombatProjectileSnapshot Projectile { get; }

        public bool Equals(CombatProjectileSpawnEvent other)
        {
            return Projectile.Equals(other.Projectile);
        }

        public override bool Equals(object obj)
        {
            return obj is CombatProjectileSpawnEvent other && Equals(other);
        }

        public override int GetHashCode()
        {
            return Projectile.GetHashCode();
        }
    }

    public enum CombatProjectilePhysicalEventKind
    {
        ActiveFrame = 0,
        Impact = 1,
        AreaPulse = 2,
        Expiration = 3,
    }

    /// <summary>
    /// Projectile-authored physical event fact. Consumers may interpret it, but it never queries or mutates world state.
    /// </summary>
    /// <remarks>NoAlloc value event; payload strings are definition-owned stable ids.</remarks>
    public readonly struct CombatProjectilePhysicalEvent : IEquatable<CombatProjectilePhysicalEvent>
    {
        public CombatProjectilePhysicalEvent(
            CombatProjectileSnapshot projectile,
            CombatProjectilePhysicalEventKind kind,
            FixVector3 position,
            FixVector3 direction,
            Fix64 radius,
            int impactPower,
            CombatProjectileAreaPulseTriggerKind areaPulseTriggerKind = CombatProjectileAreaPulseTriggerKind.DirectSweep)
        {
            if (!IsSupportedKind(kind))
                throw new ArgumentOutOfRangeException(nameof(kind), "Projectile physical event kind is not supported.");
            if (!IsSupportedTriggerKind(areaPulseTriggerKind))
                throw new ArgumentOutOfRangeException(nameof(areaPulseTriggerKind), "Projectile area pulse trigger kind is not supported.");

            Projectile = projectile;
            Kind = kind;
            Position = position;
            Direction = direction.IsZero ? projectile.Direction : direction;
            Radius = radius > Fix64.Zero ? radius : Fix64.Zero;
            ImpactPower = impactPower > 0 ? impactPower : 0;
            AreaPulseTriggerKind = areaPulseTriggerKind;
        }

        public CombatProjectileSnapshot Projectile { get; }
        public CombatProjectilePhysicalEventKind Kind { get; }
        public FixVector3 Position { get; }
        public FixVector3 Direction { get; }
        public Fix64 Radius { get; }
        public int ImpactPower { get; }
        public CombatProjectileAreaPulseTriggerKind AreaPulseTriggerKind { get; }

        public bool Equals(CombatProjectilePhysicalEvent other)
        {
            return Projectile.Equals(other.Projectile)
                && Kind == other.Kind
                && Position.Equals(other.Position)
                && Direction.Equals(other.Direction)
                && Radius.Equals(other.Radius)
                && ImpactPower == other.ImpactPower
                && AreaPulseTriggerKind == other.AreaPulseTriggerKind;
        }

        public override bool Equals(object obj)
        {
            return obj is CombatProjectilePhysicalEvent other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = Projectile.GetHashCode();
                hash = (hash * 397) ^ (int)Kind;
                hash = (hash * 397) ^ Position.GetHashCode();
                hash = (hash * 397) ^ Direction.GetHashCode();
                hash = (hash * 397) ^ Radius.GetHashCode();
                hash = (hash * 397) ^ ImpactPower;
                hash = (hash * 397) ^ (int)AreaPulseTriggerKind;
                return hash;
            }
        }

        private static bool IsSupportedKind(CombatProjectilePhysicalEventKind kind)
        {
            return kind == CombatProjectilePhysicalEventKind.ActiveFrame
                || kind == CombatProjectilePhysicalEventKind.Impact
                || kind == CombatProjectilePhysicalEventKind.AreaPulse
                || kind == CombatProjectilePhysicalEventKind.Expiration;
        }

        private static bool IsSupportedTriggerKind(CombatProjectileAreaPulseTriggerKind kind)
        {
            return kind == CombatProjectileAreaPulseTriggerKind.DirectSweep
                || kind == CombatProjectileAreaPulseTriggerKind.Expiration;
        }
    }

    /// <summary>
    /// Emits once when an <see cref="CombatProjectileImpactPolicyKind.AreaPulseOnImpact"/> projectile
    /// triggers its area pulse, even if the pulse query finds no accepted area targets.
    /// </summary>
    /// <remarks>NoAlloc value event; payload strings are definition-owned stable ids.</remarks>
    public readonly struct CombatProjectileAreaPulseTriggerEvent :
        IEquatable<CombatProjectileAreaPulseTriggerEvent>
    {
        public CombatProjectileAreaPulseTriggerEvent(
            CombatProjectileSnapshot projectile,
            CombatProjectileAreaPulseTriggerKind triggerKind,
            FixVector3 center,
            bool consumedProjectile,
            bool hasDirectHit,
            CombatQueryResult directPhysicsHit,
            HitResolveResult directResolveResult,
            CombatProjectileImpactPayload directPayload,
            int effectTimelineId = 0)
        {
            if (!Enum.IsDefined(typeof(CombatProjectileAreaPulseTriggerKind), triggerKind))
                throw new ArgumentOutOfRangeException(nameof(triggerKind), "Area pulse trigger kind is not supported.");

            Projectile = projectile;
            TriggerKind = triggerKind;
            Center = center;
            ConsumedProjectile = consumedProjectile;
            HasDirectHit = hasDirectHit;
            DirectPhysicsHit = directPhysicsHit;
            DirectResolveResult = directResolveResult;
            DirectPayload = directPayload;
            EffectTimelineId = effectTimelineId;
        }

        public CombatProjectileSnapshot Projectile { get; }
        public CombatProjectileAreaPulseTriggerKind TriggerKind { get; }
        public FixVector3 Center { get; }
        public bool ConsumedProjectile { get; }
        public bool HasDirectHit { get; }
        public CombatQueryResult DirectPhysicsHit { get; }
        public HitResolveResult DirectResolveResult { get; }
        public CombatProjectileImpactPayload DirectPayload { get; }
        public int EffectTimelineId { get; }

        public bool Equals(CombatProjectileAreaPulseTriggerEvent other)
        {
            return Projectile.Equals(other.Projectile)
                && TriggerKind == other.TriggerKind
                && Center.Equals(other.Center)
                && ConsumedProjectile == other.ConsumedProjectile
                && HasDirectHit == other.HasDirectHit
                && DirectPhysicsHit.Equals(other.DirectPhysicsHit)
                && DirectResolveResult.Equals(other.DirectResolveResult)
                && DirectPayload.Equals(other.DirectPayload)
                && EffectTimelineId == other.EffectTimelineId;
        }

        public override bool Equals(object obj)
        {
            return obj is CombatProjectileAreaPulseTriggerEvent other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = Projectile.GetHashCode();
                hash = (hash * 397) ^ (int)TriggerKind;
                hash = (hash * 397) ^ Center.GetHashCode();
                hash = (hash * 397) ^ (ConsumedProjectile ? 1 : 0);
                hash = (hash * 397) ^ (HasDirectHit ? 1 : 0);
                hash = (hash * 397) ^ DirectPhysicsHit.GetHashCode();
                hash = (hash * 397) ^ DirectResolveResult.GetHashCode();
                hash = (hash * 397) ^ DirectPayload.GetHashCode();
                hash = (hash * 397) ^ EffectTimelineId;
                return hash;
            }
        }
    }

    public readonly struct CombatProjectileImpactEvent : IEquatable<CombatProjectileImpactEvent>
    {
        public CombatProjectileImpactEvent(
            CombatProjectileSnapshot projectile,
            CombatQueryResult physicsHit,
            HitResolveResult resolveResult,
            CombatProjectileImpactPayload payload,
            CombatProjectileImpactContext context,
            bool consumedProjectile,
            int effectTimelineId = 0)
        {
            Projectile = projectile;
            PhysicsHit = physicsHit;
            ResolveResult = resolveResult;
            Payload = payload;
            Context = context;
            ConsumedProjectile = consumedProjectile;
            EffectTimelineId = effectTimelineId;
        }

        public CombatProjectileSnapshot Projectile { get; }
        public CombatQueryResult PhysicsHit { get; }
        public HitResolveResult ResolveResult { get; }
        public CombatProjectileImpactPayload Payload { get; }
        public CombatProjectileImpactContext Context { get; }
        public bool ConsumedProjectile { get; }
        public int EffectTimelineId { get; }

        public bool Equals(CombatProjectileImpactEvent other)
        {
            return Projectile.Equals(other.Projectile)
                && PhysicsHit.Equals(other.PhysicsHit)
                && ResolveResult.Equals(other.ResolveResult)
                && Payload.Equals(other.Payload)
                && Context.Equals(other.Context)
                && ConsumedProjectile == other.ConsumedProjectile
                && EffectTimelineId == other.EffectTimelineId;
        }

        public override bool Equals(object obj)
        {
            return obj is CombatProjectileImpactEvent other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = Projectile.GetHashCode();
                hash = (hash * 397) ^ PhysicsHit.GetHashCode();
                hash = (hash * 397) ^ ResolveResult.GetHashCode();
                hash = (hash * 397) ^ Payload.GetHashCode();
                hash = (hash * 397) ^ Context.GetHashCode();
                hash = (hash * 397) ^ (ConsumedProjectile ? 1 : 0);
                hash = (hash * 397) ^ EffectTimelineId;
                return hash;
            }
        }
    }

    public readonly struct CombatProjectileExpiredEvent : IEquatable<CombatProjectileExpiredEvent>
    {
        public CombatProjectileExpiredEvent(CombatProjectileSnapshot projectile, int effectTimelineId = 0)
        {
            Projectile = projectile;
            EffectTimelineId = effectTimelineId;
        }

        public CombatProjectileSnapshot Projectile { get; }
        public int EffectTimelineId { get; }

        public bool Equals(CombatProjectileExpiredEvent other)
        {
            return Projectile.Equals(other.Projectile)
                && EffectTimelineId == other.EffectTimelineId;
        }

        public override bool Equals(object obj)
        {
            return obj is CombatProjectileExpiredEvent other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return (Projectile.GetHashCode() * 397) ^ EffectTimelineId;
            }
        }
    }

    public readonly struct CombatProjectileRejectedEvent : IEquatable<CombatProjectileRejectedEvent>
    {
        public CombatProjectileRejectedEvent(CombatProjectileSpawnRequest request, string reason)
        {
            Request = request;
            Reason = reason ?? string.Empty;
        }

        public CombatProjectileSpawnRequest Request { get; }
        public string Reason { get; }

        public bool Equals(CombatProjectileRejectedEvent other)
        {
            return Request.Equals(other.Request)
                && string.Equals(Reason, other.Reason, StringComparison.Ordinal);
        }

        public override bool Equals(object obj)
        {
            return obj is CombatProjectileRejectedEvent other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return (Request.GetHashCode() * 397) ^ (Reason == null ? 0 : StringComparer.Ordinal.GetHashCode(Reason));
            }
        }
    }

    public sealed class CombatProjectileFrameEvents
    {
        public static readonly CombatProjectileFrameEvents Empty = new CombatProjectileFrameEvents(
            null,
            null,
            null,
            null,
            null,
            null);

        public CombatProjectileFrameEvents(
            IReadOnlyList<CombatProjectileSpawnEvent> spawns,
            IReadOnlyList<CombatProjectileAreaPulseTriggerEvent> areaPulseTriggers,
            IReadOnlyList<CombatProjectileImpactEvent> impacts,
            IReadOnlyList<CombatProjectileExpiredEvent> expirations,
            IReadOnlyList<CombatProjectileRejectedEvent> rejections,
            IReadOnlyList<CombatProjectilePhysicalEvent> physicalEvents)
        {
            Spawns = CombatProjectileReadOnly.Copy(spawns);
            AreaPulseTriggers = CombatProjectileReadOnly.Copy(areaPulseTriggers);
            Impacts = CombatProjectileReadOnly.Copy(impacts);
            Expirations = CombatProjectileReadOnly.Copy(expirations);
            Rejections = CombatProjectileReadOnly.Copy(rejections);
            PhysicalEvents = CombatProjectileReadOnly.Copy(physicalEvents);
        }

        public IReadOnlyList<CombatProjectileSpawnEvent> Spawns { get; }
        public IReadOnlyList<CombatProjectileAreaPulseTriggerEvent> AreaPulseTriggers { get; }
        public IReadOnlyList<CombatProjectileImpactEvent> Impacts { get; }
        public IReadOnlyList<CombatProjectileExpiredEvent> Expirations { get; }
        public IReadOnlyList<CombatProjectileRejectedEvent> Rejections { get; }
        public IReadOnlyList<CombatProjectilePhysicalEvent> PhysicalEvents { get; }
    }

    public sealed class CombatProjectileDebugSnapshot
    {
        public CombatProjectileDebugSnapshot(
            string moduleId,
            long definitionHash,
            IReadOnlyList<CombatProjectileSnapshot> activeProjectiles,
            CombatProjectileFrameEvents lastFrameEvents,
            IReadOnlyList<CombatProjectileDebugEntry> activeEntries = null)
        {
            ModuleId = moduleId ?? string.Empty;
            DefinitionHash = definitionHash;
            ActiveProjectiles = CombatProjectileReadOnly.Copy(activeProjectiles);
            LastFrameEvents = lastFrameEvents ?? CombatProjectileFrameEvents.Empty;
            ActiveEntries = CombatProjectileReadOnly.Copy(activeEntries);
        }

        public string ModuleId { get; }
        public long DefinitionHash { get; }
        public IReadOnlyList<CombatProjectileSnapshot> ActiveProjectiles { get; }
        public IReadOnlyList<CombatProjectileDebugEntry> ActiveEntries { get; }
        public int ActiveCount => ActiveProjectiles.Count;
        public CombatProjectileFrameEvents LastFrameEvents { get; }
    }

    public readonly struct CombatProjectileDebugEntry : IEquatable<CombatProjectileDebugEntry>
    {
        public CombatProjectileDebugEntry(
            CombatProjectileSnapshot projectile,
            int remainingFrames,
            int remainingAcceptedImpacts = 0)
        {
            Projectile = projectile;
            RemainingFrames = remainingFrames < 0 ? 0 : remainingFrames;
            RemainingAcceptedImpacts = remainingAcceptedImpacts < 0 ? 0 : remainingAcceptedImpacts;
        }

        public CombatProjectileSnapshot Projectile { get; }
        public int RemainingFrames { get; }
        public int AcceptedImpactCount => Projectile.AcceptedImpactCount;
        public int RemainingAcceptedImpacts { get; }

        public bool Equals(CombatProjectileDebugEntry other)
        {
            return Projectile.Equals(other.Projectile)
                && RemainingFrames == other.RemainingFrames
                && RemainingAcceptedImpacts == other.RemainingAcceptedImpacts;
        }

        public override bool Equals(object obj)
        {
            return obj is CombatProjectileDebugEntry other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = Projectile.GetHashCode();
                hash = (hash * 397) ^ RemainingFrames;
                hash = (hash * 397) ^ RemainingAcceptedImpacts;
                return hash;
            }
        }
    }

    internal sealed class CombatProjectileSpawnRequestComparer : IComparer<CombatProjectileSpawnRequest>
    {
        public static readonly CombatProjectileSpawnRequestComparer Instance =
            new CombatProjectileSpawnRequestComparer();

        public int Compare(CombatProjectileSpawnRequest left, CombatProjectileSpawnRequest right)
        {
            int compare = left.SpawnFrame.CompareTo(right.SpawnFrame);
            if (compare != 0)
                return compare;
            compare = left.SourceOrder.CompareTo(right.SourceOrder);
            if (compare != 0)
                return compare;
            compare = left.SourceEntityId.CompareTo(right.SourceEntityId);
            if (compare != 0)
                return compare;
            compare = left.DefinitionId.CompareTo(right.DefinitionId);
            if (compare != 0)
                return compare;
            return left.ActionInstanceId.CompareTo(right.ActionInstanceId);
        }
    }

    internal sealed class CombatProjectileSnapshotComparer : IComparer<CombatProjectileSnapshot>
    {
        public static readonly CombatProjectileSnapshotComparer Instance =
            new CombatProjectileSnapshotComparer();

        public int Compare(CombatProjectileSnapshot left, CombatProjectileSnapshot right)
        {
            return left.ProjectileId.CompareTo(right.ProjectileId);
        }
    }
}
