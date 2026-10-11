using System;
using System.Collections.Generic;
using MxFramework.Rendering;
using MxFramework.Runtime;
using UnityEngine;

namespace MxFramework.Vfx.RenderingBridge
{
    public sealed class VfxRenderingBridge : IVfxRuntimeEventSink, IDisposable
    {
        private readonly VfxRuntimeModule _vfx;
        private readonly IRenderDataPublisher _publisher;
        private readonly IRenderSubjectMap<long> _instanceSubjects;
        private readonly List<VfxRuntimeEvent> _events = new List<VfxRuntimeEvent>();
        private readonly Dictionary<long, RuntimeFrame> _pendingRelease = new Dictionary<long, RuntimeFrame>();
        private readonly HashSet<long> _knownSubjects = new HashSet<long>();
        private bool _isInstalled;
        private bool _isDisposed;

        public VfxRenderingBridge(
            VfxRuntimeModule vfx,
            IRenderDataPublisher publisher,
            IRenderSubjectMap<long> instanceSubjects)
        {
            _vfx = vfx ?? throw new ArgumentNullException(nameof(vfx));
            _publisher = publisher ?? throw new ArgumentNullException(nameof(publisher));
            _instanceSubjects = instanceSubjects ?? throw new ArgumentNullException(nameof(instanceSubjects));
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
            ReleaseAllKnown();
            ReleaseAllPending();
            _events.Clear();
        }

        public void Dispose()
        {
            if (_isDisposed)
                return;

            Uninstall();
            _isDisposed = true;
        }

        public int DrainFrame(RuntimeFrame frame)
        {
            ThrowIfDisposed();
            if (!_isInstalled)
                return 0;

            ReleasePending(frame);
            _events.Clear();
            int drained = _vfx.DrainEvents(frame, _events);
            for (int i = 0; i < _events.Count; i++)
                Translate(frame, _events[i]);

            _events.Clear();
            return drained;
        }

        public void OnVfxRuntimeEvents(RuntimeFrame frame, IReadOnlyList<VfxRuntimeEvent> events)
        {
            ThrowIfDisposed();
            if (!_isInstalled || events == null)
                return;

            ReleasePending(frame);
            for (int i = 0; i < events.Count; i++)
                Translate(frame, events[i]);
        }

        private void Translate(RuntimeFrame frame, in VfxRuntimeEvent evt)
        {
            switch (evt.Type)
            {
                case VfxRuntimeEventType.Spawned:
                case VfxRuntimeEventType.Rebuilt:
                    PublishSpawned(evt);
                    break;
                case VfxRuntimeEventType.Stopped:
                case VfxRuntimeEventType.Expired:
                    PublishDespawned(frame, evt);
                    break;
            }
        }

        private void PublishSpawned(in VfxRuntimeEvent evt)
        {
            long key = evt.InstanceId.Value;
            ReleasePending(key);
            MxRenderSubjectId subject = _instanceSubjects.GetOrCreate(key, MxRenderSubjectRole.Tracked);
            _knownSubjects.Add(key);
            _publisher.PublishImpact(subject, new MxRenderImpactEvent(
                ToVector3(evt.Anchor.Pose.Position),
                Color.white,
                Intensity(evt.Parameters),
                DurationSeconds(evt.Parameters)));
        }

        private void PublishDespawned(RuntimeFrame frame, in VfxRuntimeEvent evt)
        {
            long key = evt.InstanceId.Value;
            if (!_instanceSubjects.TryResolve(key, out MxRenderSubjectId subject))
                return;

            _publisher.PublishSubjectLifecycle(subject, MxSubjectLifecycleKind.Despawned);
            _knownSubjects.Remove(key);
            if (!_pendingRelease.ContainsKey(key))
                _pendingRelease.Add(key, frame);
        }

        private void ReleasePending(RuntimeFrame frame)
        {
            if (_pendingRelease.Count == 0)
                return;

            var release = new List<long>();
            foreach (KeyValuePair<long, RuntimeFrame> pair in _pendingRelease)
            {
                if (pair.Value < frame)
                    release.Add(pair.Key);
            }

            for (int i = 0; i < release.Count; i++)
                ReleasePending(release[i]);
        }

        private void ReleasePending(long key)
        {
            if (!_pendingRelease.Remove(key))
                return;

            _instanceSubjects.Release(key);
        }

        private void ReleaseAllPending()
        {
            if (_pendingRelease.Count == 0)
                return;

            var release = new List<long>(_pendingRelease.Keys);
            _pendingRelease.Clear();
            for (int i = 0; i < release.Count; i++)
                _instanceSubjects.Release(release[i]);
        }

        private void ReleaseAllKnown()
        {
            if (_knownSubjects.Count == 0)
                return;

            var release = new List<long>(_knownSubjects);
            _knownSubjects.Clear();
            for (int i = 0; i < release.Count; i++)
            {
                _pendingRelease.Remove(release[i]);
                _instanceSubjects.Release(release[i]);
            }
        }

        private void ThrowIfDisposed()
        {
            if (_isDisposed)
                throw new ObjectDisposedException(nameof(VfxRenderingBridge));
        }

        private static Vector3 ToVector3(VfxVector3 value)
        {
            const float scale = 1f / VfxVector3.Scale;
            return new Vector3(value.RawX * scale, value.RawY * scale, value.RawZ * scale);
        }

        private static float Intensity(VfxParameterBlock parameters)
        {
            if (parameters.Value0 == 0)
                return 1f;

            float value = parameters.Value0 / 1000f;
            return value < 0f ? 0f : value;
        }

        private static float DurationSeconds(VfxParameterBlock parameters)
        {
            return parameters.Value1 <= 0 ? 0.1f : parameters.Value1 / 1000f;
        }
    }
}
