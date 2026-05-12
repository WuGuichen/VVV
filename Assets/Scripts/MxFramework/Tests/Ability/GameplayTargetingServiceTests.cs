using MxFramework.Gameplay;
using NUnit.Framework;

namespace MxFramework.Tests.Ability
{
    public class GameplayTargetingServiceTests
    {
        private const int CasterId = 1;
        private const int CasterTeam = 10;
        private const int EnemyTeam = 20;
        private const int TagGrounded = 100;
        private const int TagFlying = 101;
        private const int StatusStealth = 200;
        private const int StatusInvulnerable = 201;

        [Test]
        public void Select_FiltersDeadCandidates()
        {
            var service = new GameplayTargetingService();
            var query = new GameplayTargetQuery(CasterId, CasterTeam, requireAlive: true);
            var candidates = new[]
            {
                Target(2, EnemyTeam, isAlive: false),
                Target(3, EnemyTeam),
            };

            GameplayTargetingResult result = service.Select(query, candidates);

            Assert.AreEqual(1, result.SelectedCount);
            Assert.AreEqual(3, result.SelectedTargets[0].EntityId);
            Assert.AreEqual(GameplayTargetRejectReason.Dead, result.RejectedTargets[0].Reason);
            Assert.AreEqual("dead", result.RejectedTargets[0].ReasonToken);
        }

        [Test]
        public void Select_EnemyRelationRejectsSameTeam()
        {
            var service = new GameplayTargetingService();
            var query = new GameplayTargetQuery(
                CasterId,
                CasterTeam,
                relationFilter: GameplayTargetRelationFilter.Enemy);
            var candidates = new[]
            {
                Target(2, CasterTeam),
                Target(3, EnemyTeam),
            };

            GameplayTargetingResult result = service.Select(query, candidates);

            Assert.AreEqual(1, result.SelectedCount);
            Assert.AreEqual(3, result.SelectedTargets[0].EntityId);
            Assert.AreEqual(GameplayTargetRejectReason.SameTeam, result.RejectedTargets[0].Reason);
            Assert.AreEqual("same-team", result.RejectedTargets[0].ReasonToken);
        }

        [Test]
        public void Select_SameTeamRelationRejectsEnemyTeam()
        {
            var service = new GameplayTargetingService();
            var query = new GameplayTargetQuery(
                CasterId,
                CasterTeam,
                relationFilter: GameplayTargetRelationFilter.SameTeam);
            var candidates = new[]
            {
                Target(2, EnemyTeam),
                Target(3, CasterTeam),
            };

            GameplayTargetingResult result = service.Select(query, candidates);

            Assert.AreEqual(1, result.SelectedCount);
            Assert.AreEqual(3, result.SelectedTargets[0].EntityId);
            Assert.AreEqual(GameplayTargetRejectReason.DifferentTeam, result.RejectedTargets[0].Reason);
            Assert.AreEqual("different-team", result.RejectedTargets[0].ReasonToken);
        }

        [Test]
        public void Select_EnemyRelationRejectsNeutralTeam()
        {
            var service = new GameplayTargetingService();
            var query = new GameplayTargetQuery(
                CasterId,
                CasterTeam,
                relationFilter: GameplayTargetRelationFilter.Enemy);
            var candidates = new[]
            {
                Target(2, 0),
                Target(3, EnemyTeam),
            };

            GameplayTargetingResult result = service.Select(query, candidates);

            Assert.AreEqual(1, result.SelectedCount);
            Assert.AreEqual(3, result.SelectedTargets[0].EntityId);
            Assert.AreEqual(GameplayTargetRejectReason.NeutralTeam, result.RejectedTargets[0].Reason);
            Assert.AreEqual("neutral-team", result.RejectedTargets[0].ReasonToken);
        }

        [Test]
        public void Select_RequiresAllTags()
        {
            var service = new GameplayTargetingService();
            var query = new GameplayTargetQuery(
                CasterId,
                CasterTeam,
                requiredTags: new[] { TagGrounded, TagFlying });
            var candidates = new[]
            {
                Target(2, EnemyTeam, tags: new[] { TagGrounded }),
                Target(3, EnemyTeam, tags: new[] { TagGrounded, TagFlying }),
            };

            GameplayTargetingResult result = service.Select(query, candidates);

            Assert.AreEqual(1, result.SelectedCount);
            Assert.AreEqual(3, result.SelectedTargets[0].EntityId);
            Assert.AreEqual(GameplayTargetRejectReason.MissingRequiredTag, result.RejectedTargets[0].Reason);
            Assert.AreEqual(TagFlying, result.RejectedTargets[0].DetailId);
            Assert.AreEqual("candidate[0] entity=2 reason=missing-tag tag=101", result.RejectedTargets[0].Label);
        }

        [Test]
        public void Select_BlocksStatuses()
        {
            var service = new GameplayTargetingService();
            var query = new GameplayTargetQuery(
                CasterId,
                CasterTeam,
                blockedStatuses: new[] { StatusInvulnerable, StatusStealth });
            var candidates = new[]
            {
                Target(2, EnemyTeam, statuses: new[] { StatusStealth }),
                Target(3, EnemyTeam),
            };

            GameplayTargetingResult result = service.Select(query, candidates);

            Assert.AreEqual(1, result.SelectedCount);
            Assert.AreEqual(3, result.SelectedTargets[0].EntityId);
            Assert.AreEqual(GameplayTargetRejectReason.BlockedStatus, result.RejectedTargets[0].Reason);
            Assert.AreEqual(StatusStealth, result.RejectedTargets[0].DetailId);
            Assert.AreEqual("candidate[0] entity=2 reason=blocked-status status=200", result.RejectedTargets[0].Label);
        }

        [Test]
        public void Select_ConsumesGameplayTagAndStatusSets()
        {
            var requiredTags = new GameplayTagSet();
            requiredTags.Add(new GameplayTagId(TagGrounded));

            var blockedStatuses = new GameplayStatusSet();
            blockedStatuses.Add(new GameplayStatusId(StatusInvulnerable));

            var acceptedTags = new GameplayTagSet();
            acceptedTags.Add(new GameplayTagId(TagGrounded));

            var blockedTargetTags = new GameplayTagSet();
            blockedTargetTags.Add(new GameplayTagId(TagGrounded));

            var blockedTargetStatuses = new GameplayStatusSet();
            blockedTargetStatuses.Add(new GameplayStatusId(StatusInvulnerable));

            var service = new GameplayTargetingService();
            var query = new GameplayTargetQuery(
                CasterId,
                CasterTeam,
                true,
                GameplayTargetRelationFilter.Enemy,
                requiredTags,
                blockedStatuses);
            var candidates = new[]
            {
                new GameplayTargetCandidate(2, EnemyTeam, true, blockedTargetTags, blockedTargetStatuses),
                new GameplayTargetCandidate(3, EnemyTeam, true, acceptedTags, null),
            };

            GameplayTargetingResult result = service.Select(query, candidates);

            Assert.AreEqual(1, result.SelectedCount);
            Assert.AreEqual(3, result.SelectedTargets[0].EntityId);
            Assert.AreEqual(GameplayTargetRejectReason.BlockedStatus, result.RejectedTargets[0].Reason);
            Assert.AreEqual(StatusInvulnerable, result.RejectedTargets[0].DetailId);
        }

        [Test]
        public void Select_MaxTargetsRejectsLaterValidCandidates()
        {
            var service = new GameplayTargetingService();
            var query = new GameplayTargetQuery(CasterId, CasterTeam, maxTargets: 2);
            var candidates = new[]
            {
                Target(2, EnemyTeam),
                Target(3, EnemyTeam),
                Target(4, EnemyTeam),
            };

            GameplayTargetingResult result = service.Select(query, candidates);

            Assert.AreEqual(2, result.SelectedCount);
            Assert.AreEqual(2, result.SelectedTargets[0].EntityId);
            Assert.AreEqual(3, result.SelectedTargets[1].EntityId);
            Assert.AreEqual(GameplayTargetRejectReason.MaxTargetsReached, result.RejectedTargets[0].Reason);
            Assert.AreEqual("max-targets", result.RejectedTargets[0].ReasonToken);
        }

        [Test]
        public void Select_RejectedReasonsKeepInputOrderAndReadableLabels()
        {
            var service = new GameplayTargetingService();
            var query = new GameplayTargetQuery(
                CasterId,
                CasterTeam,
                relationFilter: GameplayTargetRelationFilter.Enemy,
                requiredTags: new[] { TagGrounded },
                blockedStatuses: new[] { StatusInvulnerable });
            var candidates = new[]
            {
                Target(2, CasterTeam),
                Target(3, EnemyTeam, tags: new[] { TagFlying }),
                Target(4, EnemyTeam, tags: new[] { TagGrounded }, statuses: new[] { StatusInvulnerable }),
                Target(5, EnemyTeam, tags: new[] { TagGrounded }),
            };

            GameplayTargetingResult result = service.Select(query, candidates);

            Assert.AreEqual(1, result.SelectedCount);
            Assert.AreEqual(5, result.SelectedTargets[0].EntityId);
            Assert.AreEqual(3, result.RejectedCount);
            Assert.AreEqual("candidate[0] entity=2 reason=same-team", result.RejectedTargets[0].Label);
            Assert.AreEqual("candidate[1] entity=3 reason=missing-tag tag=100", result.RejectedTargets[1].Label);
            Assert.AreEqual("candidate[2] entity=4 reason=blocked-status status=201", result.RejectedTargets[2].Label);
        }

        private static GameplayTargetCandidate Target(
            int entityId,
            int teamId,
            bool isAlive = true,
            int[] tags = null,
            int[] statuses = null)
        {
            return new GameplayTargetCandidate(entityId, teamId, isAlive, tags, statuses);
        }
    }
}
