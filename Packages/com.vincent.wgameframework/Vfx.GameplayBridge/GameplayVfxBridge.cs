using System;
using System.Collections.Generic;
using MxFramework.Gameplay;
using MxFramework.Runtime;

namespace MxFramework.Vfx.GameplayBridge
{
    public sealed class GameplayVfxBridge : IGameplayRuntimeEventSink, IDisposable
    {
        public const int AttributeChangedTriggerBase = 1000000;
        public const int ComponentEntityCreatedTrigger = 2000001;
        public const int ComponentEntityDestroyedTrigger = 2000002;

        private readonly VfxRuntimeModule _vfx;
        private readonly IVfxTriggerResolver _resolver;
        private readonly List<VfxSpawnRequest> _resolved = new List<VfxSpawnRequest>();
        private bool _isInstalled;
        private bool _isDisposed;

        public GameplayVfxBridge(VfxRuntimeModule vfx, IVfxTriggerResolver resolver)
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

        public void OnGameplayRuntimeEvents(RuntimeFrame frame, IReadOnlyList<GameplayRuntimeEvent> events)
        {
            ThrowIfDisposed();
            if (!_isInstalled || events == null)
                return;

            for (int i = 0; i < events.Count; i++)
                Translate(frame, events[i], i);
        }

        private void Translate(RuntimeFrame fanoutFrame, in GameplayRuntimeEvent evt, int sourceOrder)
        {
            switch (evt.Type)
            {
                case GameplayRuntimeEventType.AbilityCastSucceeded:
                    Resolve(new VfxTriggerContext(
                        fanoutFrame,
                        VfxSourceDomains.Gameplay,
                        evt.AbilityId,
                        evt.CasterEntityId,
                        evt.TargetEntityId,
                        evt.Frame.Value,
                        localFrame: 0,
                        sourceOrder: sourceOrder,
                        anchor: VfxAnchor.Target(evt.CasterEntityId, evt.TargetEntityId, VfxPose.Identity),
                        parameters: VfxParameterBlock.Empty,
                        traceId: evt.TraceId));
                    break;
                case GameplayRuntimeEventType.ComponentAttributeChanged:
                    Resolve(new VfxTriggerContext(
                        fanoutFrame,
                        VfxSourceDomains.Gameplay,
                        AttributeChangedTriggerBase + evt.AttributeId,
                        evt.ComponentEntityIndex,
                        evt.TargetEntityId,
                        evt.Frame.Value,
                        localFrame: 0,
                        sourceOrder: sourceOrder,
                        anchor: VfxAnchor.Source(evt.ComponentEntityIndex, VfxPose.Identity),
                        parameters: new VfxParameterBlock(evt.AttributeId, evt.OldAttributeValue, evt.NewAttributeValue, evt.AttributeDelta),
                        traceId: evt.TraceId));
                    break;
                case GameplayRuntimeEventType.ComponentEntityCreated:
                    if (evt.TryGetComponentEntityId(out GameplayEntityId created))
                    {
                        Resolve(new VfxTriggerContext(
                            fanoutFrame,
                            VfxSourceDomains.Gameplay,
                            ComponentEntityCreatedTrigger,
                            created.Index,
                            0,
                            evt.Frame.Value,
                            localFrame: 0,
                            sourceOrder: sourceOrder,
                            anchor: VfxAnchor.Source(created.Index, VfxPose.Identity),
                            parameters: new VfxParameterBlock(created.Generation),
                            traceId: evt.TraceId));
                    }
                    break;
                case GameplayRuntimeEventType.ComponentEntityDestroyed:
                    if (evt.TryGetComponentEntityId(out GameplayEntityId destroyed))
                    {
                        Resolve(new VfxTriggerContext(
                            fanoutFrame,
                            VfxSourceDomains.Gameplay,
                            ComponentEntityDestroyedTrigger,
                            destroyed.Index,
                            0,
                            evt.Frame.Value,
                            localFrame: 0,
                            sourceOrder: sourceOrder,
                            anchor: VfxAnchor.Source(destroyed.Index, VfxPose.Identity),
                            parameters: new VfxParameterBlock(destroyed.Generation),
                            traceId: evt.TraceId));
                    }
                    break;
            }
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
                throw new ObjectDisposedException(nameof(GameplayVfxBridge));
        }
    }
}
