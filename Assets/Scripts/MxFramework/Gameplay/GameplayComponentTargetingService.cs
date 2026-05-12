using System;
using System.Collections.Generic;

namespace MxFramework.Gameplay
{
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
            if (query.RequireAlive && !candidate.IsAlive)
            {
                rejection = new GameplayComponentTargetRejectedTarget(
                    candidateIndex,
                    candidate.EntityId,
                    GameplayTargetRejectReason.Dead);
                return true;
            }

            switch (query.RelationFilter)
            {
                case GameplayTargetRelationFilter.Self:
                    if (candidate.EntityId != query.CasterEntityId)
                    {
                        rejection = new GameplayComponentTargetRejectedTarget(
                            candidateIndex,
                            candidate.EntityId,
                            GameplayTargetRejectReason.NotCaster);
                        return true;
                    }

                    break;

                case GameplayTargetRelationFilter.SameTeam:
                    GameplayTeamRelation sameTeamRelation = GameplayTeamRelations.Resolve(query.CasterTeamId, candidate.TeamId);
                    if (sameTeamRelation != GameplayTeamRelation.SameTeam)
                    {
                        rejection = new GameplayComponentTargetRejectedTarget(
                            candidateIndex,
                            candidate.EntityId,
                            sameTeamRelation == GameplayTeamRelation.Neutral
                                ? GameplayTargetRejectReason.NeutralTeam
                                : GameplayTargetRejectReason.DifferentTeam);
                        return true;
                    }

                    break;

                case GameplayTargetRelationFilter.Enemy:
                    GameplayTeamRelation enemyRelation = GameplayTeamRelations.Resolve(query.CasterTeamId, candidate.TeamId);
                    if (enemyRelation != GameplayTeamRelation.Enemy)
                    {
                        rejection = new GameplayComponentTargetRejectedTarget(
                            candidateIndex,
                            candidate.EntityId,
                            enemyRelation == GameplayTeamRelation.Neutral
                                ? GameplayTargetRejectReason.NeutralTeam
                                : GameplayTargetRejectReason.SameTeam);
                        return true;
                    }

                    break;
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
