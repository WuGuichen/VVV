using System;
using System.Collections.Generic;
using MxFramework.Combat.Animation;
using MxFramework.Combat.Core;
using MxFramework.Combat.Damage;
using MxFramework.Combat.Hit;
using MxFramework.Combat.Physics;
using MxFramework.Core.Math;
using MxFramework.Runtime;

namespace MxFramework.Vfx.CombatBridge
{
    public sealed class CombatVfxBridge : IDisposable
    {
        private readonly VfxRuntimeModule _vfx;
        private readonly IVfxTriggerResolver _resolver;
        private readonly List<VfxSpawnRequest> _resolved = new List<VfxSpawnRequest>();
        private bool _isInstalled;
        private bool _isDisposed;

        public CombatVfxBridge(VfxRuntimeModule vfx, IVfxTriggerResolver resolver)
        {
            _vfx = vfx ?? throw new ArgumentNullException(nameof(vfx));
            _resolver = resolver ?? throw new ArgumentNullException(nameof(resolver));
        }

        public bool IsInstalled => _isInstalled;
        public bool IsDisposed => _isDisposed;

        public void Install()
        {
            ThrowIfDisposed();
            _isInstalled = true;
        }

        public void Uninstall()
        {
            _isInstalled = false;
            _resolved.Clear();
        }

        public void Dispose()
        {
            if (_isDisposed)
                return;

            Uninstall();
            _isDisposed = true;
        }

        public void PublishActionFrameEvent(RuntimeFrame runtimeFrame, in ActionFrameEventRaisedEvent evt)
        {
            ThrowIfDisposed();
            if (!_isInstalled)
                return;

            Resolve(new VfxTriggerContext(
                runtimeFrame,
                VfxSourceDomains.CombatAction,
                evt.FrameEvent.EventId,
                evt.EntityId.Value,
                targetId: 0,
                sourceFrame: evt.WorldFrame.Value,
                localFrame: evt.LocalFrame,
                sourceOrder: evt.FrameEvent.SourceOrder,
                anchor: VfxAnchor.Source(evt.EntityId.Value, VfxPose.Identity),
                parameters: new VfxParameterBlock(evt.ActionId, evt.ActionInstanceId, evt.FrameEvent.IntPayload),
                traceId: new RuntimeTraceIdentity(3001, evt.EntityId.Value, evt.ActionInstanceId, evt.FrameEvent.EventId)));
        }

        public void PublishHitPresentationEvent(RuntimeFrame runtimeFrame, in HitPresentationEvent evt)
        {
            ThrowIfDisposed();
            if (!_isInstalled)
                return;

            DamageProfileUsage usage = evt.DamageEvent.Request.Usage;
            int triggerId = GetHitPresentationTriggerId(evt.PresentationProfileId);
            Resolve(new VfxTriggerContext(
                runtimeFrame,
                VfxSourceDomains.CombatHit,
                triggerId,
                usage.SourceEntityId,
                usage.TargetEntityId,
                usage.Frame,
                usage.LocalFrame,
                sourceOrder: 0,
                anchor: CreateHitAnchor(evt),
                parameters: new VfxParameterBlock(
                    evt.DamageEvent.FinalDamage,
                    evt.DamageEvent.ProfileResult.IncomingDamage,
                    (int)usage.SourceKind,
                    evt.DamageEvent.HitResult.Kind.GetHashCode()),
                traceId: new RuntimeTraceIdentity(3002, usage.SourceEntityId, usage.Frame, usage.LocalFrame)));
        }

        private static VfxAnchor CreateHitAnchor(in HitPresentationEvent evt)
        {
            DamageProfileUsage usage = evt.DamageEvent.Request.Usage;
            HitResolveResult hit = evt.DamageEvent.HitResult;
            VfxPose pose = HasHitPose(hit)
                ? new VfxPose(
                    CombatVfxPhysicsAdapter.ToVfxVector(hit.HitPointWorld),
                    CombatVfxPhysicsAdapter.ToVfxVector(hit.HitNormalWorld))
                : VfxPose.Identity;
            return VfxAnchor.Target(usage.SourceEntityId, usage.TargetEntityId, pose);
        }

        private static bool HasHitPose(in HitResolveResult hit)
        {
            return !hit.HitPointWorld.IsZero || !hit.HitNormalWorld.IsZero;
        }

        private void Resolve(in VfxTriggerContext context)
        {
            _resolved.Clear();
            _resolver.Resolve(context, _resolved);
            for (int i = 0; i < _resolved.Count; i++)
                _vfx.EnqueueSpawnRequest(_resolved[i]);
            _resolved.Clear();
        }

        private void ThrowIfDisposed()
        {
            if (_isDisposed)
                throw new ObjectDisposedException(nameof(CombatVfxBridge));
        }

        public static int GetHitPresentationTriggerId(int presentationProfileId)
        {
            if (presentationProfileId <= 0) throw new ArgumentOutOfRangeException(nameof(presentationProfileId));
            return presentationProfileId;
        }
    }

    public sealed class CombatVfxPhysicsAdapter
    {
        private readonly CombatPhysicsWorld _world;
        private readonly List<CombatQueryResult> _results = new List<CombatQueryResult>();

        public CombatVfxPhysicsAdapter(CombatPhysicsWorld world)
        {
            _world = world ?? throw new ArgumentNullException(nameof(world));
        }

        public bool TryProjectFirstHitAnchor(
            in CombatPhysicsQuery query,
            VfxAnchorKind anchorKind,
            int socketId,
            out VfxAnchor anchor,
            out CombatQueryResult hit)
        {
            _results.Clear();
            _world.Query(query, _results);
            if (_results.Count == 0)
            {
                anchor = default;
                hit = default;
                return false;
            }

            _results.Sort();
            hit = _results[0];
            var pose = new VfxPose(ToVfxVector(hit.Point), ToVfxVector(hit.Normal));
            anchor = new VfxAnchor(
                anchorKind,
                hit.Query.SourceEntityId.Value,
                hit.TargetEntityId.Value,
                socketId,
                pose);
            _results.Clear();
            return true;
        }

        public static VfxVector3 ToVfxVector(FixVector3 value)
        {
            return VfxVector3.FromRaw(value.X.RawValue, value.Y.RawValue, value.Z.RawValue);
        }
    }
}
