using System.Collections.Generic;

namespace MxFramework.Gameplay
{
    /// <summary>Result of an ability cast attempt.</summary>
    public readonly struct AbilityCastResult
    {
        public readonly bool Success;
        public readonly string FailureReason;
        public readonly IReadOnlyList<IRuntimeEntity> Targets;

        private AbilityCastResult(bool success, string failureReason, IReadOnlyList<IRuntimeEntity> targets)
        {
            Success = success;
            FailureReason = failureReason;
            Targets = targets;
        }

        public static AbilityCastResult Ok(IReadOnlyList<IRuntimeEntity> targets)
            => new AbilityCastResult(true, null, targets);

        public static AbilityCastResult Fail(string reason)
            => new AbilityCastResult(false, reason, null);
    }
}
