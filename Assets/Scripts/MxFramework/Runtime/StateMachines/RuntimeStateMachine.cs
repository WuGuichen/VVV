using System.Collections.Generic;

namespace MxFramework.Runtime
{
    public delegate bool RuntimeStateTransitionPredicate<TState>(TState current, TState next, string reason);

    public sealed class RuntimeStateMachine<TState>
    {
        private readonly RuntimeStateTransitionPredicate<TState> _canTransition;

        public RuntimeStateMachine()
            : this(default(TState), null)
        {
        }

        public RuntimeStateMachine(TState initialState)
            : this(initialState, null)
        {
        }

        public RuntimeStateMachine(TState initialState, RuntimeStateTransitionPredicate<TState> canTransition)
        {
            Current = initialState;
            _canTransition = canTransition;
            LastTransitionReason = string.Empty;
        }

        public TState Current { get; private set; }

        public string LastTransitionReason { get; private set; }

        public int Version { get; private set; }

        public bool CanTransition(TState next)
        {
            return CanTransition(next, string.Empty);
        }

        public bool CanTransition(TState next, string reason)
        {
            if (IsCurrent(next))
            {
                return true;
            }

            return _canTransition == null || _canTransition(Current, next, NormalizeReason(reason));
        }

        public bool TryTransition(TState next, string reason)
        {
            if (IsCurrent(next))
            {
                return true;
            }

            string normalizedReason = NormalizeReason(reason);
            if (_canTransition != null && !_canTransition(Current, next, normalizedReason))
            {
                return false;
            }

            Apply(next, normalizedReason);
            return true;
        }

        public void Force(TState state, string reason)
        {
            if (IsCurrent(state))
            {
                return;
            }

            Apply(state, NormalizeReason(reason));
        }

        private bool IsCurrent(TState state)
        {
            return EqualityComparer<TState>.Default.Equals(Current, state);
        }

        private void Apply(TState state, string reason)
        {
            Current = state;
            LastTransitionReason = reason;
            Version++;
        }

        private static string NormalizeReason(string reason)
        {
            return reason ?? string.Empty;
        }
    }
}
