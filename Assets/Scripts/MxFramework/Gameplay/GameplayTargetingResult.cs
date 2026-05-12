using System;
using System.Collections.Generic;

namespace MxFramework.Gameplay
{
    /// <summary>Rejected target row with stable reason token and readable diagnostic label.</summary>
    public readonly struct GameplayTargetRejectedTarget
    {
        public GameplayTargetRejectedTarget(
            int candidateIndex,
            int entityId,
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

        public int EntityId { get; }

        public GameplayTargetRejectReason Reason { get; }

        public int DetailId { get; }

        public string ReasonToken
        {
            get
            {
                switch (Reason)
                {
                    case GameplayTargetRejectReason.NullCandidate:
                        return "null-candidate";
                    case GameplayTargetRejectReason.Dead:
                        return "dead";
                    case GameplayTargetRejectReason.SameTeam:
                        return "same-team";
                    case GameplayTargetRejectReason.DifferentTeam:
                        return "different-team";
                    case GameplayTargetRejectReason.NotCaster:
                        return "not-caster";
                    case GameplayTargetRejectReason.MissingRequiredTag:
                        return "missing-tag";
                    case GameplayTargetRejectReason.BlockedStatus:
                        return "blocked-status";
                    case GameplayTargetRejectReason.MaxTargetsReached:
                        return "max-targets";
                    case GameplayTargetRejectReason.NeutralTeam:
                        return "neutral-team";
                    default:
                        return "unknown";
                }
            }
        }

        public string ReasonMessage
        {
            get
            {
                switch (Reason)
                {
                    case GameplayTargetRejectReason.NullCandidate:
                        return "candidate is null";
                    case GameplayTargetRejectReason.Dead:
                        return "candidate is dead";
                    case GameplayTargetRejectReason.SameTeam:
                        return "same team rejected by enemy relation";
                    case GameplayTargetRejectReason.DifferentTeam:
                        return "different team rejected by same-team relation";
                    case GameplayTargetRejectReason.NotCaster:
                        return "candidate is not the caster";
                    case GameplayTargetRejectReason.MissingRequiredTag:
                        return "candidate is missing required tag";
                    case GameplayTargetRejectReason.BlockedStatus:
                        return "candidate has blocked status";
                    case GameplayTargetRejectReason.MaxTargetsReached:
                        return "candidate exceeded max targets";
                    case GameplayTargetRejectReason.NeutralTeam:
                        return "neutral team rejected by relation filter";
                    default:
                        return "unknown target rejection";
                }
            }
        }

        public string Label
        {
            get
            {
                string label = "candidate[" + CandidateIndex + "] entity=" + EntityId + " reason=" + ReasonToken;
                if (Reason == GameplayTargetRejectReason.MissingRequiredTag)
                    return label + " tag=" + DetailId;

                if (Reason == GameplayTargetRejectReason.BlockedStatus)
                    return label + " status=" + DetailId;

                return label;
            }
        }
    }

    /// <summary>Immutable result snapshot for a target selection query.</summary>
    public sealed class GameplayTargetingResult
    {
        public GameplayTargetingResult(
            IReadOnlyList<GameplayTargetCandidate> selectedTargets,
            IReadOnlyList<GameplayTargetRejectedTarget> rejectedTargets)
        {
            SelectedTargets = Copy(selectedTargets);
            RejectedTargets = Copy(rejectedTargets);
        }

        public IReadOnlyList<GameplayTargetCandidate> SelectedTargets { get; }

        public IReadOnlyList<GameplayTargetRejectedTarget> RejectedTargets { get; }

        public int SelectedCount => SelectedTargets.Count;

        public int RejectedCount => RejectedTargets.Count;

        public bool HasTargets => SelectedTargets.Count > 0;

        private static GameplayTargetCandidate[] Copy(IReadOnlyList<GameplayTargetCandidate> targets)
        {
            if (targets == null || targets.Count == 0)
                return Array.Empty<GameplayTargetCandidate>();

            var copy = new GameplayTargetCandidate[targets.Count];
            for (int i = 0; i < targets.Count; i++)
                copy[i] = targets[i];

            return copy;
        }

        private static GameplayTargetRejectedTarget[] Copy(IReadOnlyList<GameplayTargetRejectedTarget> targets)
        {
            if (targets == null || targets.Count == 0)
                return Array.Empty<GameplayTargetRejectedTarget>();

            var copy = new GameplayTargetRejectedTarget[targets.Count];
            for (int i = 0; i < targets.Count; i++)
                copy[i] = targets[i];

            return copy;
        }
    }
}
