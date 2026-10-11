using System;
using MxFramework.Gameplay;

namespace MxFramework.Gameplay.Triggers
{
    /// <summary>Unique destructive source drain and bounded observer fanout.</summary>
    public sealed class GameplayTriggerEventRouterSystem : IGameplaySystem
    {
        public const string DefaultSystemId = "mxframework.gameplay.triggers.events.route";
        public const int DefaultPriority = 1000;

        private readonly GameplayTriggerRuntimeEventSource _source;
        private readonly GameplayTriggerRuntimeEventObserverRegistration[] _observers;

        /// <remarks>AllocByDesign. Copies, validates, and ordinal-sorts registrations.</remarks>
        public GameplayTriggerEventRouterSystem(
            GameplayTriggerRuntimeEventSource source,
            GameplayTriggerRuntimeEventObserverRegistration[] observers = null,
            string systemId = DefaultSystemId,
            int priority = DefaultPriority)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (string.IsNullOrWhiteSpace(systemId))
                throw new ArgumentException("Gameplay Trigger event Router system id cannot be empty.", nameof(systemId));

            observers = observers ?? Array.Empty<GameplayTriggerRuntimeEventObserverRegistration>();
            var frozen = new GameplayTriggerRuntimeEventObserverRegistration[observers.Length];
            for (int i = 0; i < observers.Length; i++)
            {
                GameplayTriggerRuntimeEventObserverRegistration registration = observers[i];
                if (string.IsNullOrWhiteSpace(registration.ObserverId))
                    throw new ArgumentException("Gameplay Trigger observer id cannot be empty.", nameof(observers));
                if (registration.Buffer == null)
                    throw new ArgumentException("Gameplay Trigger observer buffer cannot be null.", nameof(observers));
                registration.Buffer.ValidateRouterUnbound();
                for (int prior = 0; prior < i; prior++)
                {
                    if (string.Equals(
                            observers[prior].ObserverId,
                            registration.ObserverId,
                            StringComparison.Ordinal))
                        throw new ArgumentException("Gameplay Trigger observer id is duplicated.", nameof(observers));
                    if (ReferenceEquals(observers[prior].Buffer, registration.Buffer))
                        throw new ArgumentException("Gameplay Trigger observer buffer is duplicated.", nameof(observers));
                }
                frozen[i] = registration;
            }
            source.ValidateProducerBound();
            source.ValidateDrainUnbound();

            for (int i = 1; i < frozen.Length; i++)
            {
                GameplayTriggerRuntimeEventObserverRegistration value = frozen[i];
                int insert = i;
                while (insert > 0 && string.CompareOrdinal(
                           value.ObserverId,
                           frozen[insert - 1].ObserverId) < 0)
                {
                    frozen[insert] = frozen[insert - 1];
                    insert--;
                }
                frozen[insert] = value;
            }

            _source = source;
            _observers = frozen;
            SystemId = systemId;
            Priority = priority;
            source.BindDrainOwner();
            for (int i = 0; i < _observers.Length; i++) _observers[i].Buffer.BindRouter();
        }

        public string SystemId { get; }
        public GameplaySystemPhase Phase => GameplaySystemPhase.Diagnostics;
        public int Priority { get; }
        public bool IsEnabled { get; private set; } = true;
        public int ObserverCount => _observers.Length;

        /// <remarks>NoAlloc. Internal exact-composition identity.</remarks>
        internal GameplayTriggerRuntimeEventSource EventSourceIdentity => _source;

        /// <remarks>NoAlloc.</remarks>
        public void SetEnabled(bool enabled) => IsEnabled = enabled;

        /// <remarks>NoAlloc after initialization.</remarks>
        public void Tick(GameplaySystemContext context)
        {
            if (!IsEnabled) return;
            int eligible = _source.GetEligibleCount(context.Frame);
            for (int eventIndex = 0; eventIndex < eligible; eventIndex++)
            {
                GameplayTriggerRuntimeEvent evt = _source.GetPending(eventIndex);
                for (int observerIndex = 0; observerIndex < _observers.Length; observerIndex++)
                    _observers[observerIndex].Buffer.EnqueueFromRouter(in evt);
            }
            _source.ConsumePrefix(eligible);
        }

        /// <summary>Clears presentation-only transport state at an exclusive bootstrap/restore boundary.</summary>
        /// <remarks>NoAlloc. Bindings and enabled configuration are preserved.</remarks>
        public void ResetPresentationState()
        {
            _source.ResetPresentationState();
            for (int i = 0; i < _observers.Length; i++)
                _observers[i].Buffer.ResetPresentationState();
        }
    }
}
