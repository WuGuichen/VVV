using System;

namespace MxFramework.Gameplay
{
    /// <summary>
    /// Immutable component-target filter shared by low-frequency queries and
    /// high-frequency single-candidate runtime evaluation.
    /// </summary>
    /// <remarks>NoAlloc. Contains only value fields and performs no collection work.</remarks>
    public readonly struct GameplayComponentTargetFilter
        : IEquatable<GameplayComponentTargetFilter>
    {
        public GameplayComponentTargetFilter(
            GameplayTargetIdentityTypes acceptedIdentityTypes,
            GameplayTargetCapabilities requiredCapabilities,
            bool requireAlive,
            GameplayTargetRelationFilter relationFilter,
            GameplayEntityId referenceEntityId = default,
            int referenceTeamId = 0)
        {
            GameplayTargetClassificationValidation.ValidateIdentityTypes(
                acceptedIdentityTypes,
                nameof(acceptedIdentityTypes));
            GameplayTargetClassificationValidation.ValidateCapabilities(
                requiredCapabilities,
                nameof(requiredCapabilities));
            GameplayTargetRelationFilterValidation.Validate(
                relationFilter,
                nameof(relationFilter));

            switch (relationFilter)
            {
                case GameplayTargetRelationFilter.Any:
                    if (referenceTeamId != 0)
                    {
                        throw new ArgumentOutOfRangeException(
                            nameof(referenceTeamId),
                            referenceTeamId,
                            "An unrestricted target filter cannot carry a reference team.");
                    }

                    referenceEntityId = default;
                    break;

                case GameplayTargetRelationFilter.Self:
                    if (!referenceEntityId.IsValid)
                    {
                        throw new ArgumentException(
                            "A self target filter requires a valid generation-safe reference entity.",
                            nameof(referenceEntityId));
                    }

                    referenceTeamId = 0;
                    break;

                case GameplayTargetRelationFilter.SameTeam:
                case GameplayTargetRelationFilter.Enemy:
                case GameplayTargetRelationFilter.Neutral:
                    if (referenceTeamId <= 0)
                    {
                        throw new ArgumentOutOfRangeException(
                            nameof(referenceTeamId),
                            referenceTeamId,
                            "A team-relation target filter requires a positive reference team id.");
                    }

                    referenceEntityId = default;
                    break;
            }

            AcceptedIdentityTypes = acceptedIdentityTypes;
            RequiredCapabilities = requiredCapabilities;
            RequireAlive = requireAlive;
            RelationFilter = relationFilter;
            ReferenceEntityId = referenceEntityId;
            ReferenceTeamId = referenceTeamId;
        }

        public GameplayTargetIdentityTypes AcceptedIdentityTypes { get; }

        public GameplayTargetCapabilities RequiredCapabilities { get; }

        public bool RequireAlive { get; }

        public GameplayTargetRelationFilter RelationFilter { get; }

        public GameplayEntityId ReferenceEntityId { get; }

        public int ReferenceTeamId { get; }

        public bool Equals(GameplayComponentTargetFilter other)
        {
            return AcceptedIdentityTypes == other.AcceptedIdentityTypes
                && RequiredCapabilities == other.RequiredCapabilities
                && RequireAlive == other.RequireAlive
                && RelationFilter == other.RelationFilter
                && ReferenceEntityId == other.ReferenceEntityId
                && ReferenceTeamId == other.ReferenceTeamId;
        }

        public override bool Equals(object obj)
        {
            return obj is GameplayComponentTargetFilter other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = (int)AcceptedIdentityTypes;
                hash = (hash * 397) ^ (int)RequiredCapabilities;
                hash = (hash * 397) ^ (RequireAlive ? 1 : 0);
                hash = (hash * 397) ^ (int)RelationFilter;
                hash = (hash * 397) ^ ReferenceEntityId.GetHashCode();
                hash = (hash * 397) ^ ReferenceTeamId;
                return hash;
            }
        }

        public static bool operator ==(
            GameplayComponentTargetFilter left,
            GameplayComponentTargetFilter right) =>
            left.Equals(right);

        public static bool operator !=(
            GameplayComponentTargetFilter left,
            GameplayComponentTargetFilter right) =>
            !left.Equals(right);

        public static GameplayComponentTargetFilter Unrestricted =>
            new GameplayComponentTargetFilter(
                GameplayTargetIdentityTypes.None,
                GameplayTargetCapabilities.None,
                requireAlive: false,
                GameplayTargetRelationFilter.Any);
    }

    /// <summary>
    /// Stateless single-candidate evaluator shared by targeting services and
    /// runtime sensor hot paths.
    /// </summary>
    public static class GameplayComponentTargetFilterEvaluator
    {
        /// <remarks>NoAlloc.</remarks>
        public static bool TryAccept(
            in GameplayComponentTargetFilter filter,
            GameplayEntityId candidateEntityId,
            int candidateTeamId,
            GameplayLifecycleState candidateLifecycleState,
            in GameplayTargetClassificationComponent candidateClassification,
            out GameplayTargetRejectReason rejectionReason,
            out int rejectionDetailId)
        {
            if (!candidateClassification.MatchesAnyIdentity(
                    filter.AcceptedIdentityTypes))
            {
                rejectionReason = GameplayTargetRejectReason.IdentityTypeMismatch;
                rejectionDetailId = (int)candidateClassification.IdentityType;
                return false;
            }

            if (!candidateClassification.HasAllCapabilities(
                    filter.RequiredCapabilities))
            {
                rejectionReason =
                    GameplayTargetRejectReason.MissingRequiredCapability;
                rejectionDetailId = (int)(
                    filter.RequiredCapabilities
                    & ~candidateClassification.Capabilities);
                return false;
            }

            if (filter.RequireAlive
                && candidateLifecycleState != GameplayLifecycleState.Alive)
            {
                rejectionReason = GameplayTargetRejectReason.Dead;
                rejectionDetailId = (int)candidateLifecycleState;
                return false;
            }

            switch (filter.RelationFilter)
            {
                case GameplayTargetRelationFilter.Self:
                    if (candidateEntityId != filter.ReferenceEntityId)
                    {
                        rejectionReason = GameplayTargetRejectReason.NotCaster;
                        rejectionDetailId = 0;
                        return false;
                    }

                    break;

                case GameplayTargetRelationFilter.SameTeam:
                    GameplayTeamRelation sameTeamRelation =
                        GameplayTeamRelations.Resolve(
                            filter.ReferenceTeamId,
                            candidateTeamId);
                    if (sameTeamRelation != GameplayTeamRelation.SameTeam)
                    {
                        rejectionReason =
                            sameTeamRelation == GameplayTeamRelation.Neutral
                                ? GameplayTargetRejectReason.NeutralTeam
                                : GameplayTargetRejectReason.DifferentTeam;
                        rejectionDetailId = candidateTeamId;
                        return false;
                    }

                    break;

                case GameplayTargetRelationFilter.Enemy:
                    GameplayTeamRelation enemyRelation =
                        GameplayTeamRelations.Resolve(
                            filter.ReferenceTeamId,
                            candidateTeamId);
                    if (enemyRelation != GameplayTeamRelation.Enemy)
                    {
                        rejectionReason =
                            enemyRelation == GameplayTeamRelation.Neutral
                                ? GameplayTargetRejectReason.NeutralTeam
                                : GameplayTargetRejectReason.SameTeam;
                        rejectionDetailId = candidateTeamId;
                        return false;
                    }

                    break;

                case GameplayTargetRelationFilter.Neutral:
                    GameplayTeamRelation neutralRelation =
                        GameplayTeamRelations.Resolve(
                            filter.ReferenceTeamId,
                            candidateTeamId);
                    if (neutralRelation != GameplayTeamRelation.Neutral)
                    {
                        rejectionReason =
                            neutralRelation == GameplayTeamRelation.SameTeam
                                ? GameplayTargetRejectReason.SameTeam
                                : GameplayTargetRejectReason.DifferentTeam;
                        rejectionDetailId = candidateTeamId;
                        return false;
                    }

                    break;
            }

            rejectionReason = GameplayTargetRejectReason.None;
            rejectionDetailId = 0;
            return true;
        }
    }
}
