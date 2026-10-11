using MxFramework.Persistence.RuntimeBridge;
using MxFramework.Runtime;

namespace MxFramework.Tests.Persistence.RuntimeBridge
{
    /// <summary>Deliberately violates bool/out invariants so service tests can prove defensive fail-closed handling.</summary>
    internal sealed class RuntimeSaveGameContradictoryTargetFixture : IRuntimeSaveGameRestoreTarget
    {
        private readonly IRuntimeSaveGameCandidateRestoreLease _lease;

        internal RuntimeSaveGameContradictoryTargetFixture(RuntimeSaveStateRestoreProfile profile)
        {
            _lease = new ContradictoryLease(profile);
        }

        public bool TryAcquireRestoreLease(
            RuntimeSaveStateRestoreProfile expectedRestoreProfile,
            out IRuntimeSaveGameCandidateRestoreLease lease,
            out RuntimeSaveGameTargetValidationError validationError)
        {
            lease = _lease;
            validationError = RuntimeSaveGameTargetValidationError.None;
            return false;
        }

        private sealed class ContradictoryLease : IRuntimeSaveGameCandidateRestoreLease
        {
            internal ContradictoryLease(RuntimeSaveStateRestoreProfile profile)
            {
                RestoreProfile = profile;
            }

            public string TargetId => string.Empty;
            public RuntimeSaveStateCoordinator Coordinator => null;
            public RuntimeSaveStateRestoreProfile RestoreProfile { get; }

            public bool TryCompleteRestore(
                in RuntimeSaveGameRestoreCompletion completion,
                out RuntimeSaveGameTargetValidationError validationError)
            {
                validationError = new RuntimeSaveGameTargetValidationError(
                    RuntimeSaveGameTargetValidationErrorCode.CompletionRejected,
                    "completion");
                return true;
            }

            public void Dispose()
            {
            }
        }
    }
}
