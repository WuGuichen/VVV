using System;
using System.Collections.Generic;

namespace MxFramework.Gameplay
{
    public readonly struct GameplayComponentTargetRejectedTarget
    {
        public GameplayComponentTargetRejectedTarget(
            int candidateIndex,
            GameplayEntityId entityId,
            GameplayTargetRejectReason reason,
            int detailId = 0)
        {
            if (candidateIndex < 0)
                throw new ArgumentOutOfRangeException(nameof(candidateIndex), "Candidate index cannot be negative.");

            CandidateIndex = candidateIndex;
            EntityId = entityId;
            Reason = reason;
            DetailId = detailId;
        }

        public int CandidateIndex { get; }
        public GameplayEntityId EntityId { get; }
        public GameplayTargetRejectReason Reason { get; }
        public int DetailId { get; }
    }

    public sealed class GameplayComponentTargetingResult
    {
        public GameplayComponentTargetingResult(
            IReadOnlyList<GameplayComponentTargetCandidate> selectedTargets,
            IReadOnlyList<GameplayComponentTargetRejectedTarget> rejectedTargets)
        {
            SelectedTargets = Copy(selectedTargets);
            RejectedTargets = Copy(rejectedTargets);
        }

        public IReadOnlyList<GameplayComponentTargetCandidate> SelectedTargets { get; }
        public IReadOnlyList<GameplayComponentTargetRejectedTarget> RejectedTargets { get; }
        public int SelectedCount => SelectedTargets.Count;
        public int RejectedCount => RejectedTargets.Count;
        public bool HasTargets => SelectedTargets.Count > 0;

        private static GameplayComponentTargetCandidate[] Copy(IReadOnlyList<GameplayComponentTargetCandidate> targets)
        {
            if (targets == null || targets.Count == 0)
                return Array.Empty<GameplayComponentTargetCandidate>();

            var copy = new GameplayComponentTargetCandidate[targets.Count];
            for (int i = 0; i < targets.Count; i++)
                copy[i] = targets[i];
            return copy;
        }

        private static GameplayComponentTargetRejectedTarget[] Copy(IReadOnlyList<GameplayComponentTargetRejectedTarget> targets)
        {
            if (targets == null || targets.Count == 0)
                return Array.Empty<GameplayComponentTargetRejectedTarget>();

            var copy = new GameplayComponentTargetRejectedTarget[targets.Count];
            for (int i = 0; i < targets.Count; i++)
                copy[i] = targets[i];
            return copy;
        }
    }
}
