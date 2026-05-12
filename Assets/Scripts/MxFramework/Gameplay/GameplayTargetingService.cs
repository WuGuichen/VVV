using System;
using System.Collections.Generic;

namespace MxFramework.Gameplay
{
    /// <summary>Selects valid targets from candidates in input order and records stable rejection reasons.</summary>
    public sealed class GameplayTargetingService
    {
        public GameplayTargetingResult Select(
            GameplayTargetQuery query,
            IReadOnlyList<GameplayTargetCandidate> candidates)
        {
            if (query == null)
                throw new ArgumentNullException(nameof(query));

            var selected = new List<GameplayTargetCandidate>();
            var rejected = new List<GameplayTargetRejectedTarget>();

            if (candidates == null)
                return new GameplayTargetingResult(selected, rejected);

            for (int i = 0; i < candidates.Count; i++)
                EvaluateCandidate(query, candidates[i], i, selected, rejected);

            return new GameplayTargetingResult(selected, rejected);
        }

        public GameplayTargetingResult Select(
            GameplayTargetQuery query,
            IReadOnlyList<IRuntimeEntity> candidates)
        {
            if (query == null)
                throw new ArgumentNullException(nameof(query));

            var selected = new List<GameplayTargetCandidate>();
            var rejected = new List<GameplayTargetRejectedTarget>();

            if (candidates == null)
                return new GameplayTargetingResult(selected, rejected);

            for (int i = 0; i < candidates.Count; i++)
            {
                IRuntimeEntity entity = candidates[i];
                if (entity == null)
                {
                    rejected.Add(new GameplayTargetRejectedTarget(
                        i,
                        0,
                        GameplayTargetRejectReason.NullCandidate));
                    continue;
                }

                EvaluateCandidate(query, new GameplayTargetCandidate(entity), i, selected, rejected);
            }

            return new GameplayTargetingResult(selected, rejected);
        }

        private static void EvaluateCandidate(
            GameplayTargetQuery query,
            GameplayTargetCandidate candidate,
            int candidateIndex,
            List<GameplayTargetCandidate> selected,
            List<GameplayTargetRejectedTarget> rejected)
        {
            if (TryCreateFilterRejection(query, candidate, candidateIndex, out GameplayTargetRejectedTarget rejection))
            {
                rejected.Add(rejection);
                return;
            }

            if (query.HasMaxTargets && selected.Count >= query.MaxTargets)
            {
                rejected.Add(new GameplayTargetRejectedTarget(
                    candidateIndex,
                    candidate.EntityId,
                    GameplayTargetRejectReason.MaxTargetsReached));
                return;
            }

            selected.Add(candidate);
        }

        private static bool TryCreateFilterRejection(
            GameplayTargetQuery query,
            GameplayTargetCandidate candidate,
            int candidateIndex,
            out GameplayTargetRejectedTarget rejection)
        {
            if (query.RequireAlive && !candidate.IsAlive)
            {
                rejection = new GameplayTargetRejectedTarget(
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
                        rejection = new GameplayTargetRejectedTarget(
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
                        rejection = new GameplayTargetRejectedTarget(
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
                        rejection = new GameplayTargetRejectedTarget(
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
                    rejection = new GameplayTargetRejectedTarget(
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
                    rejection = new GameplayTargetRejectedTarget(
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
