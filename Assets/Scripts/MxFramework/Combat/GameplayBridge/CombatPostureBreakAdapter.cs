using System;
using MxFramework.Combat.Animation;
using MxFramework.Combat.Core;
using MxFramework.Gameplay;

namespace MxFramework.Combat.GameplayBridge
{
    public sealed class CombatPostureBreakAdapter : IDisposable
    {
        private readonly GameplayPosturePressureSystem _postureSystem;
        private readonly CombatActionRunner _runner;
        private readonly CombatEntityGameplayMap _entityMap;
        private IDisposable _subscription;
        private bool _disposed;

        public CombatPostureBreakAdapter(
            GameplayPosturePressureSystem postureSystem,
            CombatActionRunner runner,
            CombatEntityGameplayMap entityMap)
        {
            _postureSystem = postureSystem ?? throw new ArgumentNullException(nameof(postureSystem));
            _runner = runner ?? throw new ArgumentNullException(nameof(runner));
            _entityMap = entityMap ?? throw new ArgumentNullException(nameof(entityMap));
        }

        public bool IsEnabled => _subscription != null;

        public void Enable()
        {
            ThrowIfDisposed();
            if (_subscription != null)
            {
                return;
            }

            _subscription = _postureSystem.PostureBreakEvents.Subscribe(OnPostureBreak);
        }

        public void Disable()
        {
            IDisposable subscription = _subscription;
            if (subscription == null)
            {
                return;
            }

            _subscription = null;
            subscription.Dispose();
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            Disable();
            _disposed = true;
        }

        private void OnPostureBreak(PostureBreakEvent breakEvent)
        {
            if (!_entityMap.TryGetCombatId(breakEvent.EntityId, out CombatEntityId combatId))
            {
                return;
            }

            _runner.ForceCancel(combatId);
        }

        private void ThrowIfDisposed()
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(nameof(CombatPostureBreakAdapter));
            }
        }
    }
}
