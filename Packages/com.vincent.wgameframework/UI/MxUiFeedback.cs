using System;
using System.Collections.Generic;

namespace MxFramework.UI
{
    public enum MxUiFeedbackKind
    {
        None = 0,
        Click = 1,
        Confirm = 2,
        Back = 3,
        Error = 4
    }

    public enum MxUiFeedbackPhase
    {
        Activation = 0,
        Success = 1,
        Failure = 2
    }

    public readonly struct MxUiFeedbackPolicy : IEquatable<MxUiFeedbackPolicy>
    {
        public MxUiFeedbackPolicy(
            MxUiFeedbackKind activation,
            MxUiFeedbackKind success,
            MxUiFeedbackKind failure)
        {
            ValidateKind(activation, nameof(activation));
            ValidateKind(success, nameof(success));
            ValidateKind(failure, nameof(failure));

            Activation = activation;
            Success = success;
            Failure = failure;
        }

        public MxUiFeedbackKind Activation { get; }
        public MxUiFeedbackKind Success { get; }
        public MxUiFeedbackKind Failure { get; }
        public bool HasOutcomeFeedback =>
            Success != MxUiFeedbackKind.None || Failure != MxUiFeedbackKind.None;

        public bool Equals(MxUiFeedbackPolicy other)
        {
            return Activation == other.Activation &&
                   Success == other.Success &&
                   Failure == other.Failure;
        }

        public override bool Equals(object obj)
        {
            return obj is MxUiFeedbackPolicy other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = (int)Activation;
                hash = (hash * 397) ^ (int)Success;
                return (hash * 397) ^ (int)Failure;
            }
        }

        public static bool operator ==(MxUiFeedbackPolicy left, MxUiFeedbackPolicy right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(MxUiFeedbackPolicy left, MxUiFeedbackPolicy right)
        {
            return !left.Equals(right);
        }

        private static void ValidateKind(MxUiFeedbackKind kind, string parameterName)
        {
            if (kind < MxUiFeedbackKind.None || kind > MxUiFeedbackKind.Error)
                throw new ArgumentOutOfRangeException(parameterName);
        }
    }

    public readonly struct MxUiFeedbackContext
    {
        public MxUiFeedbackContext(
            MxUiViewId sourceViewId,
            string commandId,
            long invocationId,
            MxUiFeedbackPhase phase)
        {
            if (invocationId <= 0L)
                throw new ArgumentOutOfRangeException(nameof(invocationId));
            if (phase < MxUiFeedbackPhase.Activation || phase > MxUiFeedbackPhase.Failure)
                throw new ArgumentOutOfRangeException(nameof(phase));

            SourceViewId = sourceViewId;
            CommandId = commandId ?? string.Empty;
            InvocationId = invocationId;
            Phase = phase;
        }

        public MxUiViewId SourceViewId { get; }
        public string CommandId { get; }
        public long InvocationId { get; }
        public MxUiFeedbackPhase Phase { get; }
    }

    public interface IMxUiFeedbackPlayer
    {
        /// <summary>
        /// Attempts to play one feedback kind. NoAlloc after player initialization.
        /// </summary>
        bool TryPlay(MxUiFeedbackKind kind, in MxUiFeedbackContext context);
    }

    public readonly struct MxUiFeedbackPolicyBinding
    {
        public MxUiFeedbackPolicyBinding(
            MxUiViewId sourceViewId,
            string commandId,
            MxUiFeedbackPolicy policy)
        {
            SourceViewId = sourceViewId;
            CommandId = commandId ?? string.Empty;
            Policy = policy;
        }

        public MxUiViewId SourceViewId { get; }
        public string CommandId { get; }
        public MxUiFeedbackPolicy Policy { get; }
    }

    public interface IMxUiFeedbackPolicyProvider
    {
        /// <summary>
        /// Resolves an exact view-and-command policy. NoAlloc.
        /// </summary>
        bool TryGetPolicy(
            MxUiViewId sourceViewId,
            string commandId,
            out MxUiFeedbackPolicy policy);
    }

    /// <summary>
    /// Immutable runtime lookup built during composition. Constructor is LowFreqAlloc;
    /// policy lookup is NoAlloc.
    /// </summary>
    public sealed class MxUiFeedbackPolicyRegistry : IMxUiFeedbackPolicyProvider
    {
        private readonly Dictionary<PolicyKey, MxUiFeedbackPolicy> _policies;

        public MxUiFeedbackPolicyRegistry(IReadOnlyList<MxUiFeedbackPolicyBinding> bindings)
        {
            if (bindings == null)
                throw new ArgumentNullException(nameof(bindings));

            _policies = new Dictionary<PolicyKey, MxUiFeedbackPolicy>(bindings.Count);
            for (int i = 0; i < bindings.Count; i++)
            {
                MxUiFeedbackPolicyBinding binding = bindings[i];
                if (!binding.SourceViewId.IsValid)
                    throw new ArgumentException("Feedback policy view id must be valid.", nameof(bindings));
                if (string.IsNullOrWhiteSpace(binding.CommandId))
                    throw new ArgumentException("Feedback policy command id must be non-empty.", nameof(bindings));

                var key = new PolicyKey(binding.SourceViewId, binding.CommandId);
                if (!_policies.TryAdd(key, binding.Policy))
                    throw new ArgumentException(
                        "Duplicate UI feedback policy binding.",
                        nameof(bindings));
            }
        }

        public bool TryGetPolicy(
            MxUiViewId sourceViewId,
            string commandId,
            out MxUiFeedbackPolicy policy)
        {
            if (!sourceViewId.IsValid || string.IsNullOrEmpty(commandId))
            {
                policy = default;
                return false;
            }

            return _policies.TryGetValue(new PolicyKey(sourceViewId, commandId), out policy);
        }

        private readonly struct PolicyKey : IEquatable<PolicyKey>
        {
            public PolicyKey(MxUiViewId sourceViewId, string commandId)
            {
                SourceViewId = sourceViewId;
                CommandId = commandId;
            }

            private MxUiViewId SourceViewId { get; }
            private string CommandId { get; }

            public bool Equals(PolicyKey other)
            {
                return SourceViewId == other.SourceViewId &&
                       string.Equals(CommandId, other.CommandId, StringComparison.Ordinal);
            }

            public override bool Equals(object obj)
            {
                return obj is PolicyKey other && Equals(other);
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    return (SourceViewId.GetHashCode() * 397) ^
                           StringComparer.Ordinal.GetHashCode(CommandId);
                }
            }
        }
    }
}
