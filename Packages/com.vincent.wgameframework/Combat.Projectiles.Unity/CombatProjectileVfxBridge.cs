using System;
using System.Collections.Generic;
using MxFramework.Combat.Hit;
using MxFramework.Combat.Projectiles;
using MxFramework.Core.Math;
using MxFramework.Runtime;
using MxFramework.Vfx;

namespace MxFramework.Combat.Projectiles.Unity
{
    /// <summary>Stable projectile presentation lifecycle event kind.</summary>
    public enum CombatProjectilePresentationEventKind : byte
    {
        Spawn = 1,
        DirectImpact = 2,
        AreaImpact = 3,
        BlockedImpact = 4,
        Expire = 5,
        ExternalRemoval = 6,
        RuntimeReset = 7,
        LateAttachRebuild = 8
    }

    /// <summary>Stable identity of one projectile presentation event.</summary>
    /// <remarks>NoAlloc value type.</remarks>
    public readonly struct CombatProjectilePresentationCorrelation : IEquatable<CombatProjectilePresentationCorrelation>
    {
        public CombatProjectilePresentationCorrelation(int projectileId, int combatFrame, CombatProjectilePresentationEventKind kind, int eventSequence)
        {
            ProjectileId = projectileId;
            CombatFrame = combatFrame;
            Kind = kind;
            EventSequence = eventSequence;
        }

        public int ProjectileId { get; }
        public int CombatFrame { get; }
        public CombatProjectilePresentationEventKind Kind { get; }
        public int EventSequence { get; }

        public bool Equals(CombatProjectilePresentationCorrelation other) =>
            ProjectileId == other.ProjectileId && CombatFrame == other.CombatFrame && Kind == other.Kind && EventSequence == other.EventSequence;
        public override bool Equals(object obj) => obj is CombatProjectilePresentationCorrelation other && Equals(other);
        public override int GetHashCode() => (((ProjectileId * 397) ^ CombatFrame) * 397 ^ (int)Kind) * 397 ^ EventSequence;
    }

    /// <summary>Immutable context offered to a custom projectile presenter extension.</summary>
    /// <remarks>NoAlloc value type; it exposes no mutable event-buffer ownership.</remarks>
    public readonly struct CombatProjectilePresentationEventContext
    {
        public CombatProjectilePresentationEventContext(
            RuntimeFrame frame,
            CombatProjectilePresentationEventKind kind,
            int sequence,
            CombatProjectileSnapshot projectile,
            FixVector3 position,
            FixVector3 normal,
            int targetId,
            bool consumedProjectile)
        {
            Frame = frame;
            Kind = kind;
            Sequence = sequence;
            Projectile = projectile;
            Position = position;
            Normal = normal;
            TargetId = targetId;
            ConsumedProjectile = consumedProjectile;
        }

        public RuntimeFrame Frame { get; }
        public CombatProjectilePresentationEventKind Kind { get; }
        public int Sequence { get; }
        public CombatProjectileSnapshot Projectile { get; }
        public FixVector3 Position { get; }
        public FixVector3 Normal { get; }
        public int TargetId { get; }
        public bool ConsumedProjectile { get; }
    }

    /// <summary>Optional immutable-registration custom presentation extension.</summary>
    public interface ICombatProjectilePresentationExtension
    {
        /// <summary>Reports whether this extension claims a definition/event pair without producing side effects.</summary>
        /// <remarks>NoAlloc after initialization. Implementations must not mutate combat state.</remarks>
        bool CanHandle(in CombatProjectilePresentationEventContext context);

        /// <summary>Handles a previously claimed event. Only the deterministic first claimant is invoked.</summary>
        /// <remarks>NoAlloc after initialization. Implementations must not mutate combat state.</remarks>
        void Handle(in CombatProjectilePresentationEventContext context);
    }

    /// <summary>Low-frequency diagnostic counters for projectile presentation routing.</summary>
    public readonly struct CombatProjectileVfxBridgeDiagnostics
    {
        public CombatProjectileVfxBridgeDiagnostics(int missingBindings, int missingSlots, int invalidIds, int backendUnavailable,
            int duplicateClaims, int trackingOverflows)
        {
            MissingBindings = missingBindings;
            MissingSlots = missingSlots;
            InvalidDefinitionIds = invalidIds;
            BackendUnavailable = backendUnavailable;
            DuplicateExtensionClaims = duplicateClaims;
            TrackingOverflows = trackingOverflows;
        }

        public int MissingBindings { get; }
        public int MissingSlots { get; }
        public int InvalidDefinitionIds { get; }
        public int BackendUnavailable { get; }
        public int DuplicateExtensionClaims { get; }
        public int TrackingOverflows { get; }
    }

    /// <summary>Routes authoritative projectile frame events to v2 VFX slots and persistent views.</summary>
    /// <remarks>ProcessFrame is NoAlloc after fixed buffers and downstream list capacities are warmed.</remarks>
    public sealed class CombatProjectileVfxBridge
    {
        private const int DefaultCapacity = 1024;
        private static readonly RuntimeTraceIdentity EmptyTrace = default;
        private readonly CombatProjectileUnityPresentationRegistry _registry;
        private readonly VfxRuntimeModule _vfx;
        private readonly CombatProjectileUnityPresenter _presenter;
        private readonly ICombatProjectilePresentationExtension[] _extensions;
        private readonly CombatProjectilePresentationCorrelation[] _dedupe;
        private readonly CombatProjectileActiveView[] _tracked;
        private readonly bool[] _trackedSeen;
        private int _dedupeCount;
        private int _dedupeWrite;
        private int _trackedCount;
        private int _missingBindings;
        private int _missingSlots;
        private int _invalidIds;
        private int _backendUnavailable;
        private int _duplicateClaims;
        private int _trackingOverflows;

        /// <summary>Creates a bridge with a stable custom-extension registration order.</summary>
        /// <remarks>LowFreqAlloc: copies the extension list once.</remarks>
        public CombatProjectileVfxBridge(
            CombatProjectileUnityPresentationRegistry registry,
            VfxRuntimeModule vfx,
            CombatProjectileUnityPresenter presenter = null,
            IReadOnlyList<ICombatProjectilePresentationExtension> extensions = null,
            int trackingCapacity = DefaultCapacity,
            int correlationCapacity = DefaultCapacity)
        {
            if (trackingCapacity <= 0) throw new ArgumentOutOfRangeException(nameof(trackingCapacity));
            if (correlationCapacity <= 0) throw new ArgumentOutOfRangeException(nameof(correlationCapacity));
            _registry = registry ?? throw new ArgumentNullException(nameof(registry));
            _vfx = vfx;
            _presenter = presenter;
            _tracked = new CombatProjectileActiveView[trackingCapacity];
            _trackedSeen = new bool[trackingCapacity];
            _dedupe = new CombatProjectilePresentationCorrelation[correlationCapacity];
            int count = extensions?.Count ?? 0;
            _extensions = count == 0 ? Array.Empty<ICombatProjectilePresentationExtension>() : new ICombatProjectilePresentationExtension[count];
            for (int i = 0; i < count; i++)
                _extensions[i] = extensions[i];
        }

        /// <summary>Routes one authoritative frame in fixed Spawn, Impact, Area, Expire order.</summary>
        /// <remarks>NoAlloc after initialization and stable downstream capacity.</remarks>
        public void ProcessFrame(RuntimeFrame frame, CombatProjectileFrameEventBuffer events, IReadOnlyList<CombatProjectileActiveView> activeProjectiles)
        {
            if (events == null)
                throw new ArgumentNullException(nameof(events));
            IReadOnlyList<CombatProjectileSpawnEvent> spawns = events.Spawns;
            for (int i = 0; i < spawns.Count; i++)
            {
                CombatProjectileSnapshot projectile = spawns[i].Projectile;
                bool tracked = Track(projectile);
                if (!tracked)
                    _trackingOverflows++;
                var context = CreateContext(frame, CombatProjectilePresentationEventKind.Spawn, i, projectile, projectile.Origin, FixVector3.Zero, 0, false);
                if (Reserve(in context))
                {
                    if (!TryClaim(in context))
                        EmitOneShot(in context, CombatProjectileUnityPresentationSlotKind.Spawn);
                    if (tracked)
                        _presenter?.BuildPersistent(projectile);
                }
            }

            IReadOnlyList<CombatProjectileImpactEvent> impacts = events.Impacts;
            for (int i = 0; i < impacts.Count; i++)
            {
                CombatProjectileImpactEvent impact = impacts[i];
                if (impact.Context.IsAreaPulse)
                    continue;
                CombatProjectilePresentationEventKind kind;
                CombatProjectileUnityPresentationSlotKind slot;
                if (impact.ResolveResult.Kind == HitResolveKind.Damage)
                {
                    kind = CombatProjectilePresentationEventKind.DirectImpact;
                    slot = CombatProjectileUnityPresentationSlotKind.DirectImpact;
                }
                else if (impact.ResolveResult.Kind == HitResolveKind.Blocked)
                {
                    kind = CombatProjectilePresentationEventKind.BlockedImpact;
                    slot = CombatProjectileUnityPresentationSlotKind.BlockedImpact;
                }
                else
                {
                    continue;
                }

                var context = CreateContext(frame, kind, i, impact.Projectile, impact.PhysicsHit.Point, impact.PhysicsHit.Normal,
                    impact.ResolveResult.TargetId.Value, impact.ConsumedProjectile);
                if (!Reserve(in context))
                    continue;
                bool claimed = TryClaim(in context);
                if (!claimed)
                    EmitOneShot(in context, slot);
                if (impact.ConsumedProjectile)
                {
                    _presenter?.ExitPersistent(impact.Projectile.ProjectileId, kind);
                    RemoveTracked(impact.Projectile.ProjectileId);
                }
            }

            IReadOnlyList<CombatProjectileAreaPulseTriggerEvent> areas = events.AreaPulseTriggers;
            for (int i = 0; i < areas.Count; i++)
            {
                CombatProjectileAreaPulseTriggerEvent area = areas[i];
                FixVector3 normal = area.HasDirectHit ? area.DirectPhysicsHit.Normal : FixVector3.Zero;
                int targetId = area.HasDirectHit ? area.DirectResolveResult.TargetId.Value : 0;
                var context = CreateContext(frame, CombatProjectilePresentationEventKind.AreaImpact, i, area.Projectile, area.Center, normal, targetId, area.ConsumedProjectile);
                if (!Reserve(in context))
                    continue;
                bool claimed = TryClaim(in context);
                if (!claimed)
                    EmitOneShot(in context, CombatProjectileUnityPresentationSlotKind.AreaImpact);
                if (area.ConsumedProjectile && area.TriggerKind != CombatProjectileAreaPulseTriggerKind.Expiration)
                {
                    _presenter?.ExitPersistent(area.Projectile.ProjectileId, CombatProjectilePresentationEventKind.DirectImpact);
                    RemoveTracked(area.Projectile.ProjectileId);
                }
            }

            IReadOnlyList<CombatProjectileExpiredEvent> expirations = events.Expirations;
            for (int i = 0; i < expirations.Count; i++)
            {
                CombatProjectileExpiredEvent expiration = expirations[i];
                var context = CreateContext(frame, CombatProjectilePresentationEventKind.Expire, i, expiration.Projectile,
                    expiration.Projectile.Position, FixVector3.Zero, 0, true);
                if (!Reserve(in context))
                    continue;
                bool claimed = TryClaim(in context);
                if (!claimed)
                    EmitOneShot(in context, CombatProjectileUnityPresentationSlotKind.Expire);
                _presenter?.ExitPersistent(expiration.Projectile.ProjectileId, CombatProjectilePresentationEventKind.Expire);
                RemoveTracked(expiration.Projectile.ProjectileId);
            }

            if (activeProjectiles != null)
            {
                ReconcileActive(frame, activeProjectiles);
                _presenter?.SyncTrackedFromBridge(_tracked, _trackedCount);
            }
        }

        /// <summary>Clears persistent views immediately without emitting one-shots.</summary>
        /// <remarks>NoAlloc after initialization.</remarks>
        public void Reset() => Reset(RuntimeFrame.Zero);

        /// <summary>Notifies custom paths of runtime reset, clears correlation state, and immediately clears standard persistent views.</summary>
        /// <remarks>NoAlloc after initialization.</remarks>
        public void Reset(RuntimeFrame frame)
        {
            for (int i = 0; i < _trackedCount; i++)
            {
                CombatProjectileSnapshot snapshot = ToSnapshot(_tracked[i]);
                var context = CreateContext(frame, CombatProjectilePresentationEventKind.RuntimeReset, i, snapshot,
                    snapshot.Position, FixVector3.Zero, 0, true);
                TryClaim(in context);
            }
            _trackedCount = 0;
            _dedupeCount = 0;
            _dedupeWrite = 0;
            _presenter?.ResetPersistent();
        }

        /// <summary>Returns a caller-owned immutable diagnostic snapshot.</summary>
        /// <remarks>NoAlloc value construction.</remarks>
        public CombatProjectileVfxBridgeDiagnostics GetDiagnostics() =>
            new CombatProjectileVfxBridgeDiagnostics(_missingBindings, _missingSlots, _invalidIds, _backendUnavailable,
                _duplicateClaims, _trackingOverflows);

        private static CombatProjectilePresentationEventContext CreateContext(RuntimeFrame frame, CombatProjectilePresentationEventKind kind,
            int sequence, CombatProjectileSnapshot projectile, FixVector3 position, FixVector3 normal, int targetId, bool consumed) =>
            new CombatProjectilePresentationEventContext(frame, kind, sequence, projectile, position, normal, targetId, consumed);

        private bool Reserve(in CombatProjectilePresentationEventContext context)
        {
            var correlation = new CombatProjectilePresentationCorrelation(context.Projectile.ProjectileId,
                context.Projectile.LastFrame.Value, context.Kind, context.Sequence);
            for (int i = 0; i < _dedupeCount; i++)
            {
                if (_dedupe[i].Equals(correlation))
                    return false;
            }
            _dedupe[_dedupeWrite] = correlation;
            _dedupeWrite = (_dedupeWrite + 1) % _dedupe.Length;
            if (_dedupeCount < _dedupe.Length)
                _dedupeCount++;
            return true;
        }

        private bool TryClaim(in CombatProjectilePresentationEventContext context)
        {
            int first = -1;
            for (int i = 0; i < _extensions.Length; i++)
            {
                ICombatProjectilePresentationExtension extension = _extensions[i];
                if (extension == null || !extension.CanHandle(in context))
                    continue;
                if (first < 0)
                    first = i;
                else
                    _duplicateClaims++;
            }
            if (first < 0)
                return false;
            _extensions[first].Handle(in context);
            return true;
        }

        private void ReconcileActive(RuntimeFrame frame, IReadOnlyList<CombatProjectileActiveView> activeProjectiles)
        {
            for (int i = 0; i < _trackedCount; i++)
                _trackedSeen[i] = false;

            int activeCount = activeProjectiles?.Count ?? 0;
            for (int i = 0; i < activeCount; i++)
            {
                CombatProjectileActiveView active = activeProjectiles[i];
                int trackedIndex = FindTracked(active.ProjectileId);
                if (trackedIndex >= 0)
                {
                    _tracked[trackedIndex] = active;
                    _trackedSeen[trackedIndex] = true;
                    continue;
                }

                CombatProjectileSnapshot snapshot = ToSnapshot(active);
                var context = CreateContext(frame, CombatProjectilePresentationEventKind.LateAttachRebuild, i, snapshot,
                    active.Position, FixVector3.Zero, 0, false);
                if (!Track(active))
                {
                    _trackingOverflows++;
                    _presenter?.ExitPersistent(active.ProjectileId, CombatProjectilePresentationEventKind.RuntimeReset);
                    continue;
                }
                if (Reserve(in context))
                    TryClaim(in context);
            }

            for (int i = _trackedCount - 1; i >= 0; i--)
            {
                if (_trackedSeen[i])
                    continue;
                CombatProjectileActiveView active = _tracked[i];
                CombatProjectileSnapshot snapshot = ToSnapshot(active);
                var context = CreateContext(frame, CombatProjectilePresentationEventKind.ExternalRemoval, i, snapshot,
                    active.Position, FixVector3.Zero, 0, true);
                if (Reserve(in context))
                    TryClaim(in context);
                _presenter?.ExitPersistent(active.ProjectileId, CombatProjectilePresentationEventKind.ExternalRemoval);
                RemoveTrackedAt(i);
            }
        }

        private int FindTracked(int projectileId)
        {
            for (int i = 0; i < _trackedCount; i++)
            {
                if (_tracked[i].ProjectileId == projectileId)
                    return i;
            }
            return -1;
        }

        private bool Track(CombatProjectileSnapshot projectile)
        {
            return Track(new CombatProjectileActiveView(projectile.ProjectileId, projectile.SpawnSequence, projectile.DefinitionId,
                projectile.DefinitionStableId, projectile.SourceEntityId, projectile.ActionId, projectile.ActionInstanceId,
                projectile.TraceId, projectile.LocalFrame, projectile.SpawnFrame, projectile.LastFrame, projectile.AgeFrames,
                projectile.Position, projectile.Direction, projectile.Origin, projectile.HasTargetPoint, projectile.TargetPoint,
                projectile.EffectiveLifetimeFrames, projectile.HasTrackingTarget, projectile.TrackingTargetEntityId,
                projectile.AcceptedImpacts.Count, projectile.LaunchSpeedPerFrame));
        }

        private bool Track(CombatProjectileActiveView projectile)
        {
            int index = FindTracked(projectile.ProjectileId);
            if (index >= 0)
            {
                _tracked[index] = projectile;
                _trackedSeen[index] = true;
                return true;
            }
            if (_trackedCount >= _tracked.Length)
                return false;
            _tracked[_trackedCount] = projectile;
            _trackedSeen[_trackedCount] = true;
            _trackedCount++;
            return true;
        }

        private void RemoveTracked(int projectileId)
        {
            int index = FindTracked(projectileId);
            if (index >= 0)
                RemoveTrackedAt(index);
        }

        private void RemoveTrackedAt(int index)
        {
            int last = --_trackedCount;
            if (index < last)
            {
                _tracked[index] = _tracked[last];
                _trackedSeen[index] = _trackedSeen[last];
            }
            _tracked[last] = default;
            _trackedSeen[last] = false;
        }

        private static CombatProjectileSnapshot ToSnapshot(CombatProjectileActiveView projectile) => new CombatProjectileSnapshot(
            projectile.ProjectileId, projectile.SpawnSequence, projectile.DefinitionId, projectile.DefinitionStableId,
            projectile.SourceEntityId, projectile.ActionId, projectile.ActionInstanceId, projectile.TraceId, projectile.LocalFrame,
            projectile.SpawnFrame, projectile.LastFrame, projectile.AgeFrames, projectile.Position, projectile.Direction,
            projectile.Origin, projectile.HasTargetPoint, projectile.TargetPoint, null, projectile.EffectiveLifetimeFrames,
            projectile.HasTrackingTarget, projectile.TrackingTargetEntityId, projectile.LaunchSpeedPerFrame);

        private void EmitOneShot(in CombatProjectilePresentationEventContext context, CombatProjectileUnityPresentationSlotKind slotKind)
        {
            if (_vfx == null)
            {
                _backendUnavailable++;
                return;
            }
            if (!_registry.TryGetV2(context.Projectile.DefinitionId, out CombatProjectileUnityPresentationBindingV2 binding))
            {
                _missingBindings++;
                return;
            }
            if (!binding.TryGetSlot(slotKind, out CombatProjectileUnityPresentationSlot slot))
            {
                _missingSlots++;
                return;
            }
            if (slot.VfxDefinitionId <= 0)
            {
                _invalidIds++;
                return;
            }

            CombatProjectileUnityPresentationPose pose = CombatProjectileUnityPresentationPoseResolver.Resolve(
                slot.Orientation, slot.LocalOffset, context.Position, context.Projectile.Direction, context.Normal);
            _vfx.EnqueueSpawnRequest(new VfxSpawnRequest(
                context.Frame,
                VfxSourceDomains.ProjectilePresentation,
                context.Projectile.LastFrame.Value,
                context.Projectile.LocalFrame,
                context.Sequence,
                (int)context.Kind,
                context.Projectile.SourceEntityId.Value,
                context.TargetId,
                new VfxDefinitionId(slot.VfxDefinitionId),
                VfxAnchor.World(new VfxPose(ToVfx(pose.Position), ToVfx(pose.Forward))),
                new VfxParameterBlock((int)context.Kind, context.Sequence),
                slot.LifetimeFramesOverride,
                EmptyTrace));
        }

        private static VfxVector3 ToVfx(FixVector3 value) => VfxVector3.FromRaw(value.X.RawValue, value.Y.RawValue, value.Z.RawValue);
    }
}
