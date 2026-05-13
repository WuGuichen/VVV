using System;
using System.Collections.Generic;
using MxFramework.Combat.Animation;
using MxFramework.Combat.Core;

namespace MxFramework.Runtime.Unity
{
    public sealed class CombatAnimationUnityModule
    {
        private readonly ICombatAnimationContext _animationContext;
        private readonly Dictionary<CombatEntityId, ICombatAnimatorDriver> _drivers =
            new Dictionary<CombatEntityId, ICombatAnimatorDriver>();

        private CombatActionRunner _subscribedRunner;

        public CombatAnimationUnityModule(ICombatAnimationContext animationContext)
        {
            _animationContext = animationContext ?? throw new ArgumentNullException(nameof(animationContext));
        }

        public int DriverCount => _drivers.Count;

        public void RegisterDriver(CombatEntityId entityId, ICombatAnimatorDriver driver)
        {
            if (entityId.IsNone)
            {
                throw new ArgumentException("Combat entity id cannot be None.", nameof(entityId));
            }

            _drivers[entityId] = driver ?? throw new ArgumentNullException(nameof(driver));
        }

        public bool UnregisterDriver(CombatEntityId entityId)
        {
            return _drivers.Remove(entityId);
        }

        public void Initialize()
        {
            CombatActionRunner runner = _animationContext.ActionRunner
                ?? throw new InvalidOperationException("Combat animation context has no ActionRunner.");

            if (ReferenceEquals(_subscribedRunner, runner))
            {
                return;
            }

            Shutdown();
            _subscribedRunner = runner;
            _subscribedRunner.ActionStarted += OnActionStarted;
            _subscribedRunner.ActionPhaseChanged += OnActionPhaseChanged;
            _subscribedRunner.ActionFinished += OnActionFinished;
            _subscribedRunner.ActionCanceled += OnActionCanceled;
            _subscribedRunner.ActionCancelRejected += OnActionCancelRejected;
        }

        public void Shutdown()
        {
            if (_subscribedRunner == null)
            {
                return;
            }

            _subscribedRunner.ActionStarted -= OnActionStarted;
            _subscribedRunner.ActionPhaseChanged -= OnActionPhaseChanged;
            _subscribedRunner.ActionFinished -= OnActionFinished;
            _subscribedRunner.ActionCanceled -= OnActionCanceled;
            _subscribedRunner.ActionCancelRejected -= OnActionCancelRejected;
            _subscribedRunner = null;
        }

        private void OnActionStarted(ActionStartedEvent evt)
        {
            if (_drivers.TryGetValue(evt.EntityId, out ICombatAnimatorDriver driver))
            {
                driver.OnActionStarted(evt);
            }
        }

        private void OnActionPhaseChanged(ActionPhaseChangedEvent evt)
        {
            if (_drivers.TryGetValue(evt.EntityId, out ICombatAnimatorDriver driver))
            {
                driver.OnActionPhaseChanged(evt);
            }
        }

        private void OnActionFinished(ActionFinishedEvent evt)
        {
            if (_drivers.TryGetValue(evt.EntityId, out ICombatAnimatorDriver driver))
            {
                driver.OnActionFinished(evt);
            }
        }

        private void OnActionCanceled(ActionCanceledEvent evt)
        {
            if (_drivers.TryGetValue(evt.EntityId, out ICombatAnimatorDriver driver))
            {
                driver.OnActionCanceled(evt);
            }
        }

        private void OnActionCancelRejected(ActionCancelRejectedEvent evt)
        {
            if (_drivers.TryGetValue(evt.EntityId, out ICombatAnimatorDriver driver))
            {
                driver.OnActionCancelRejected(evt);
            }
        }
    }
}
