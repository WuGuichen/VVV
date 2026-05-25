using System;
using System.Collections.Generic;
using MxFramework.Gameplay;
using MxFramework.Runtime;

namespace MxFramework.Rendering.GameplayBridge
{
    public sealed class GameplayRenderingBridge : IDisposable
    {
        private readonly GameplayRuntimeModule _gameplay;
        private readonly IRenderDataPublisher _publisher;
        private readonly IRenderSubjectMap<GameplayEntityId> _componentSubjects;
        private readonly IRenderSubjectMap<int> _runtimeEntitySubjects;
        private readonly MxRenderSubjectRole _defaultRole;
        private readonly List<GameplayRuntimeEvent> _events = new List<GameplayRuntimeEvent>();
        private readonly HashSet<GameplayEntityId> _knownComponentSubjects = new HashSet<GameplayEntityId>();
        private readonly Dictionary<GameplayEntityId, RuntimeFrame> _pendingComponentSubjectRelease = new Dictionary<GameplayEntityId, RuntimeFrame>();
        private readonly Dictionary<int, RuntimeFrame> _pendingRuntimeEntitySubjectRelease = new Dictionary<int, RuntimeFrame>();
        private bool _isInstalled;
        private bool _isDisposed;

        public GameplayRenderingBridge(
            GameplayRuntimeModule gameplay,
            IRenderDataPublisher publisher,
            IRenderSubjectMap<GameplayEntityId> componentSubjects,
            MxRenderSubjectRole defaultRole = MxRenderSubjectRole.Tracked)
            : this(gameplay, publisher, componentSubjects, null, defaultRole)
        {
        }

        public GameplayRenderingBridge(
            GameplayRuntimeModule gameplay,
            IRenderDataPublisher publisher,
            IRenderSubjectMap<GameplayEntityId> componentSubjects,
            IRenderSubjectMap<int> runtimeEntitySubjects,
            MxRenderSubjectRole defaultRole = MxRenderSubjectRole.Tracked)
        {
            _gameplay = gameplay ?? throw new ArgumentNullException(nameof(gameplay));
            _publisher = publisher ?? throw new ArgumentNullException(nameof(publisher));
            _componentSubjects = componentSubjects ?? throw new ArgumentNullException(nameof(componentSubjects));
            _runtimeEntitySubjects = runtimeEntitySubjects;
            _defaultRole = defaultRole;
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
            if (!_isInstalled)
                return;

            _isInstalled = false;
            ReleaseKnownComponentSubjects();
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

            ReleasePendingSubjectMappings(frame);
            _events.Clear();
            int drained = _gameplay.DrainEvents(frame, _events);
            for (int i = 0; i < _events.Count; i++)
                Translate(_events[i]);

            _events.Clear();
            return drained;
        }

        private void Translate(in GameplayRuntimeEvent evt)
        {
            switch (evt.Type)
            {
                case GameplayRuntimeEventType.ComponentEntityCreated:
                    PublishComponentSpawned(evt);
                    break;
                case GameplayRuntimeEventType.ComponentEntityDestroyed:
                    PublishComponentDespawned(evt);
                    break;
                case GameplayRuntimeEventType.EntityDespawned:
                    PublishRuntimeEntityDespawned(evt);
                    break;
            }
        }

        private void PublishComponentSpawned(in GameplayRuntimeEvent evt)
        {
            if (!evt.TryGetComponentEntityId(out GameplayEntityId entityId))
                return;

            ReleasePendingComponentSubject(entityId);
            MxRenderSubjectId subject = _componentSubjects.GetOrCreate(entityId, _defaultRole);
            _knownComponentSubjects.Add(entityId);
            _publisher.PublishSubjectLifecycle(subject, MxSubjectLifecycleKind.Spawned);
        }

        private void PublishComponentDespawned(in GameplayRuntimeEvent evt)
        {
            if (!evt.TryGetComponentEntityId(out GameplayEntityId entityId))
                return;

            if (_pendingComponentSubjectRelease.ContainsKey(entityId))
                return;

            if (!_componentSubjects.TryResolve(entityId, out MxRenderSubjectId subject))
                return;

            _publisher.PublishSubjectLifecycle(subject, MxSubjectLifecycleKind.Despawned);
            _pendingComponentSubjectRelease[entityId] = evt.Frame;
            _knownComponentSubjects.Remove(entityId);
        }

        private void PublishRuntimeEntityDespawned(in GameplayRuntimeEvent evt)
        {
            if (_runtimeEntitySubjects == null || evt.TargetEntityId <= 0)
                return;

            if (_pendingRuntimeEntitySubjectRelease.ContainsKey(evt.TargetEntityId))
                return;

            if (!_runtimeEntitySubjects.TryResolve(evt.TargetEntityId, out MxRenderSubjectId subject))
                return;

            _publisher.PublishSubjectLifecycle(subject, MxSubjectLifecycleKind.Despawned);
            _pendingRuntimeEntitySubjectRelease[evt.TargetEntityId] = evt.Frame;
        }

        private void ReleaseKnownComponentSubjects()
        {
            ReleasePendingSubjectMappings();
            if (_knownComponentSubjects.Count == 0)
                return;

            var subjects = new List<GameplayEntityId>(_knownComponentSubjects);
            _knownComponentSubjects.Clear();
            for (int i = 0; i < subjects.Count; i++)
                _componentSubjects.Release(subjects[i]);
        }

        private void ReleasePendingSubjectMappings(RuntimeFrame frame)
        {
            ReleasePendingComponentSubjects(frame);
            ReleasePendingRuntimeEntitySubjects(frame);
        }

        private void ReleasePendingSubjectMappings()
        {
            ReleasePendingComponentSubjects();
            ReleasePendingRuntimeEntitySubjects();
        }

        private void ReleasePendingComponentSubjects(RuntimeFrame frame)
        {
            if (_pendingComponentSubjectRelease.Count == 0)
                return;

            var subjects = new List<GameplayEntityId>();
            foreach (var pair in _pendingComponentSubjectRelease)
            {
                if (pair.Value < frame)
                    subjects.Add(pair.Key);
            }

            for (int i = 0; i < subjects.Count; i++)
                ReleasePendingComponentSubject(subjects[i]);
        }

        private void ReleasePendingComponentSubjects()
        {
            if (_pendingComponentSubjectRelease.Count == 0)
                return;

            var subjects = new List<GameplayEntityId>(_pendingComponentSubjectRelease.Keys);
            for (int i = 0; i < subjects.Count; i++)
                ReleasePendingComponentSubject(subjects[i]);
        }

        private void ReleasePendingComponentSubject(GameplayEntityId entityId)
        {
            if (!_pendingComponentSubjectRelease.Remove(entityId))
                return;

            _componentSubjects.Release(entityId);
        }

        private void ReleasePendingRuntimeEntitySubjects(RuntimeFrame frame)
        {
            if (_runtimeEntitySubjects == null || _pendingRuntimeEntitySubjectRelease.Count == 0)
                return;

            var subjects = new List<int>();
            foreach (var pair in _pendingRuntimeEntitySubjectRelease)
            {
                if (pair.Value < frame)
                    subjects.Add(pair.Key);
            }

            for (int i = 0; i < subjects.Count; i++)
                ReleasePendingRuntimeEntitySubject(subjects[i]);
        }

        private void ReleasePendingRuntimeEntitySubjects()
        {
            if (_runtimeEntitySubjects == null || _pendingRuntimeEntitySubjectRelease.Count == 0)
                return;

            var subjects = new List<int>(_pendingRuntimeEntitySubjectRelease.Keys);
            for (int i = 0; i < subjects.Count; i++)
                ReleasePendingRuntimeEntitySubject(subjects[i]);
        }

        private void ReleasePendingRuntimeEntitySubject(int entityId)
        {
            if (!_pendingRuntimeEntitySubjectRelease.Remove(entityId))
                return;

            _runtimeEntitySubjects.Release(entityId);
        }

        private void ThrowIfDisposed()
        {
            if (_isDisposed)
                throw new ObjectDisposedException(nameof(GameplayRenderingBridge));
        }
    }
}
