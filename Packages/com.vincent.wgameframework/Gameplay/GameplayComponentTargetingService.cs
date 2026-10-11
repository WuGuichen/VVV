using System;
using System.Collections.Generic;

namespace MxFramework.Gameplay
{
    /// <summary>
    /// Selects component target snapshots in stable input order.
    /// </summary>
    /// <remarks>
    /// AllocByDesign for the result lists. Per-candidate filtering performs only
    /// value, flag, and canonical-array checks.
    /// </remarks>
    public sealed class GameplayComponentTargetingService
    {
        public GameplayComponentTargetingResult Select(
            GameplayComponentTargetQuery query,
            IReadOnlyList<GameplayComponentTargetCandidate> candidates)
        {
            if (query == null)
                throw new ArgumentNullException(nameof(query));

            var selected = new List<GameplayComponentTargetCandidate>();
            var rejected = new List<GameplayComponentTargetRejectedTarget>();

            if (candidates == null)
                return new GameplayComponentTargetingResult(selected, rejected);

            for (int i = 0; i < candidates.Count; i++)
                EvaluateCandidate(query, candidates[i], i, selected, rejected);

            return new GameplayComponentTargetingResult(selected, rejected);
        }

        private static void EvaluateCandidate(
            GameplayComponentTargetQuery query,
            GameplayComponentTargetCandidate candidate,
            int candidateIndex,
            List<GameplayComponentTargetCandidate> selected,
            List<GameplayComponentTargetRejectedTarget> rejected)
        {
            if (TryCreateFilterRejection(query, candidate, candidateIndex, out GameplayComponentTargetRejectedTarget rejection))
            {
                rejected.Add(rejection);
                return;
            }

            if (query.HasMaxTargets && selected.Count >= query.MaxTargets)
            {
                rejected.Add(new GameplayComponentTargetRejectedTarget(
                    candidateIndex,
                    candidate.EntityId,
                    GameplayTargetRejectReason.MaxTargetsReached));
                return;
            }

            selected.Add(candidate);
        }

        private static bool TryCreateFilterRejection(
            GameplayComponentTargetQuery query,
            GameplayComponentTargetCandidate candidate,
            int candidateIndex,
            out GameplayComponentTargetRejectedTarget rejection)
        {
            GameplayComponentTargetFilter filter = query.Filter;
            GameplayTargetClassificationComponent classification =
                candidate.Classification;
            if (!GameplayComponentTargetFilterEvaluator.TryAccept(
                    in filter,
                    candidate.EntityId,
                    candidate.TeamId,
                    candidate.LifecycleState,
                    in classification,
                    out GameplayTargetRejectReason rejectionReason,
                    out int rejectionDetailId))
            {
                rejection = new GameplayComponentTargetRejectedTarget(
                    candidateIndex,
                    candidate.EntityId,
                    rejectionReason,
                    rejectionDetailId);
                return true;
            }

            IReadOnlyList<int> requiredTags = query.RequiredTags;
            for (int i = 0; i < requiredTags.Count; i++)
            {
                int tagId = requiredTags[i];
                if (!candidate.HasTag(tagId))
                {
                    rejection = new GameplayComponentTargetRejectedTarget(
                        candidateIndex,
                        candidate.EntityId,
                        GameplayTargetRejectReason.MissingRequiredTag,
                        tagId);
                    return true;
                }
            }

            IReadOnlyList<int> blockedStatuses = query.BlockedStatuses;
            for (int i = 0; i < blockedStatuses.Count; i++)
            {
                int statusId = blockedStatuses[i];
                if (candidate.HasStatus(statusId))
                {
                    rejection = new GameplayComponentTargetRejectedTarget(
                        candidateIndex,
                        candidate.EntityId,
                        GameplayTargetRejectReason.BlockedStatus,
                        statusId);
                    return true;
                }
            }

            rejection = default;
            return false;
        }
    }
}
