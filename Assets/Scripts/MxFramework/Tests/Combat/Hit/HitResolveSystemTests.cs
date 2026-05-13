using System;
using System.Collections.Generic;
using MxFramework.Combat.Animation;
using MxFramework.Combat.Core;
using MxFramework.Combat.Hit;
using MxFramework.Combat.Physics;
using MxFramework.Core.Math;
using NUnit.Framework;

namespace MxFramework.Tests.Combat.Hit
{
    public class HitResolveSystemTests
    {
        [Test]
        public void Resolve_SortsByPriorityThenPhysicsHit()
        {
            var candidates = new[]
            {
                Candidate(target: 2, distanceRaw: 1000000, priority: 0),
                Candidate(target: 3, distanceRaw: 500000, priority: 0),
                Candidate(target: 4, distanceRaw: 2000000, priority: 10),
            };
            var results = new List<HitResolveResult>();

            int count = new HitResolveSystem().Resolve(candidates, new HashSet<WeaponHitOnceKey>(), results);

            Assert.AreEqual(3, count);
            Assert.AreEqual(4, results[0].TargetId.Value);
            Assert.AreEqual(3, results[1].TargetId.Value);
            Assert.AreEqual(2, results[2].TargetId.Value);
        }

        [Test]
        public void Resolve_AppliesTargetStateFiltersInOrder()
        {
            var candidates = new[]
            {
                Candidate(target: 2, state: HitTargetStateFlags.None),
                Candidate(target: 3, state: HitTargetStateFlags.Alive | HitTargetStateFlags.Invincible),
                Candidate(target: 4, state: HitTargetStateFlags.Alive | HitTargetStateFlags.Parrying),
                Candidate(target: 5, state: HitTargetStateFlags.Alive | HitTargetStateFlags.Blocking),
            };
            var results = new List<HitResolveResult>();

            new HitResolveSystem().Resolve(candidates, new HashSet<WeaponHitOnceKey>(), results);

            Assert.AreEqual(HitResolveKind.TargetDead, results[0].Kind);
            Assert.AreEqual(HitResolveKind.Invincible, results[1].Kind);
            Assert.AreEqual(HitResolveKind.Parried, results[2].Kind);
            Assert.AreEqual(HitResolveKind.Blocked, results[3].Kind);
            Assert.AreEqual(0, results[3].Damage);
        }

        [Test]
        public void Resolve_DamageKeepsDamageAndKnockback()
        {
            HitCandidate candidate = Candidate(
                target: 2,
                damage: 30,
                stagger: 12,
                knockback: new FixVector3(Fix64.One, Fix64.Zero, Fix64.Zero));
            var results = new List<HitResolveResult>();

            new HitResolveSystem().Resolve(new[] { candidate }, new HashSet<WeaponHitOnceKey>(), results);

            Assert.AreEqual(HitResolveKind.Damage, results[0].Kind);
            Assert.AreEqual(30, results[0].Damage);
            Assert.AreEqual(12, results[0].StaggerFrames);
            Assert.AreEqual(Fix64.One, results[0].Knockback.X);
            Assert.IsTrue(results[0].IsAcceptedDamage);
        }

        [Test]
        public void Resolve_SuperArmorKeepsDamageButRemovesStagger()
        {
            HitCandidate candidate = Candidate(
                target: 2,
                damage: 30,
                stagger: 12,
                state: HitTargetStateFlags.Alive | HitTargetStateFlags.SuperArmor);
            var results = new List<HitResolveResult>();

            new HitResolveSystem().Resolve(new[] { candidate }, new HashSet<WeaponHitOnceKey>(), results);

            Assert.AreEqual(HitResolveKind.Damage, results[0].Kind);
            Assert.AreEqual(30, results[0].Damage);
            Assert.AreEqual(0, results[0].StaggerFrames);
        }

        [Test]
        public void Resolve_DeduplicatesSameActionInstanceTraceAndTarget()
        {
            var candidates = new[]
            {
                Candidate(target: 2, traceId: 7, distanceRaw: 1000000),
                Candidate(target: 2, traceId: 7, distanceRaw: 2000000),
                Candidate(target: 2, traceId: 8, distanceRaw: 3000000),
            };
            var results = new List<HitResolveResult>();

            new HitResolveSystem().Resolve(candidates, new HashSet<WeaponHitOnceKey>(), results);

            Assert.AreEqual(HitResolveKind.Damage, results[0].Kind);
            Assert.AreEqual(HitResolveKind.Duplicate, results[1].Kind);
            Assert.AreEqual(HitResolveKind.Damage, results[2].Kind);
        }

        [Test]
        public void Resolve_PreventsSelfDamage()
        {
            var results = new List<HitResolveResult>();

            new HitResolveSystem().Resolve(
                new[] { Candidate(attacker: 2, target: 2) },
                new HashSet<WeaponHitOnceKey>(),
                results);

            Assert.AreEqual(HitResolveKind.SelfDamage, results[0].Kind);
            Assert.AreEqual(0, results[0].Damage);
        }

        [Test]
        public void Resolve_FiltersFriendlyTargetWhenFriendlyFireDisabled()
        {
            var results = new List<HitResolveResult>();
            var teams = new TestTeamRelationProvider();
            teams.SetTeam(new CombatEntityId(1), 10);
            teams.SetTeam(new CombatEntityId(2), 10);

            new HitResolveSystem().Resolve(
                new[] { Candidate(target: 2) },
                new HashSet<WeaponHitOnceKey>(),
                results,
                teams);

            Assert.AreEqual(HitResolveKind.Friendly, results[0].Kind);
            Assert.AreEqual(0, results[0].Damage);
        }

        [Test]
        public void Resolve_AllowsFriendlyTargetWhenFriendlyFireEnabled()
        {
            var results = new List<HitResolveResult>();
            var teams = new TestTeamRelationProvider();
            teams.SetTeam(new CombatEntityId(1), 10);
            teams.SetTeam(new CombatEntityId(2), 10);

            new HitResolveSystem().Resolve(
                new[] { Candidate(target: 2) },
                new HashSet<WeaponHitOnceKey>(),
                results,
                teams,
                allowFriendlyFire: true);

            Assert.AreEqual(HitResolveKind.Damage, results[0].Kind);
        }

        [Test]
        public void Resolve_UsesDynamicTargetStateResolver()
        {
            var results = new List<HitResolveResult>();
            var resolver = new TestTargetStateResolver(HitTargetStateFlags.Alive | HitTargetStateFlags.Invincible);

            new HitResolveSystem().Resolve(
                new[] { Candidate(target: 2, state: HitTargetStateFlags.Alive) },
                new HashSet<WeaponHitOnceKey>(),
                results,
                resolver);

            Assert.AreEqual(HitResolveKind.Invincible, results[0].Kind);
            Assert.AreEqual(2, resolver.LastTargetId.Value);
        }

        [Test]
        public void Resolve_DispatchesResolvedAndBlockedEvents()
        {
            var dispatcher = new TestCombatEventDispatcher();
            var system = new HitResolveSystem();
            system.SetEventDispatcher(dispatcher);
            var results = new List<HitResolveResult>();

            system.Resolve(
                new[] { Candidate(target: 2, state: HitTargetStateFlags.Alive | HitTargetStateFlags.Blocking) },
                new HashSet<WeaponHitOnceKey>(),
                results);

            Assert.AreEqual(1, dispatcher.ResolvedCount);
            Assert.AreEqual(HitResolveKind.Blocked, dispatcher.LastResolved.Kind);
            Assert.AreEqual(1, dispatcher.BlockedCount);
            Assert.AreEqual(1, dispatcher.LastBlockedAttacker.Value);
            Assert.AreEqual(2, dispatcher.LastBlockedTarget.Value);
            Assert.AreEqual(1001, dispatcher.LastBlockedActionId);
            Assert.AreEqual(10, dispatcher.LastBlockedFrame.Value);
        }

        [Test]
        public void ActionStateAdapter_MapsActionWindowsToTargetState()
        {
            CombatEntityId target = new CombatEntityId(2);
            var registry = new CombatActionRegistry();
            registry.RegisterTimeline(1001, new CombatActionTimeline(
                1001,
                5,
                new CombatFrameRange(0, 0),
                new CombatFrameRange(1, 3),
                new CombatFrameRange(4, 4),
                new[]
                {
                    new CombatActionWindow(CombatActionWindowKind.Invincible, new CombatFrameRange(0, 0)),
                    new CombatActionWindow(CombatActionWindowKind.Parry, new CombatFrameRange(0, 0)),
                    new CombatActionWindow(CombatActionWindowKind.SuperArmor, new CombatFrameRange(0, 0)),
                },
                events: null));

            var runner = new CombatActionRunner(registry);
            runner.ForceStartAction(target, 1001, new CombatFrame(10));
            var adapter = new ActionStateToHitTargetAdapter(runner);

            HitTargetStateFlags state = adapter.ResolveTargetState(target);

            Assert.IsTrue((state & HitTargetStateFlags.Alive) != 0);
            Assert.IsTrue((state & HitTargetStateFlags.Invincible) != 0);
            Assert.IsTrue((state & HitTargetStateFlags.Parrying) != 0);
            Assert.IsTrue((state & HitTargetStateFlags.SuperArmor) != 0);
        }

        [Test]
        public void InvalidCandidate_Throws()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => Candidate(target: 2, damage: -1));
            Assert.Throws<ArgumentOutOfRangeException>(() => Candidate(target: 2, stagger: -1));
        }

        private static HitCandidate Candidate(
            int target,
            int attacker = 1,
            int traceId = 7,
            int distanceRaw = 1000000,
            int priority = 0,
            int damage = 10,
            int stagger = 5,
            HitTargetStateFlags state = HitTargetStateFlags.Alive,
            FixVector3 knockback = default)
        {
            if (knockback.Equals(default(FixVector3)))
            {
                knockback = FixVector3.Zero;
            }

            CombatQueryHeader query = new CombatQueryHeader(
                1,
                CombatQueryKind.Capsule,
                new CombatEntityId(attacker),
                traceId,
                1001,
                0,
                CombatPhysicsLayerMask.All);
            var hit = new CombatQueryResult(
                query,
                new CombatEntityId(target),
                new CombatBodyId(target),
                new CombatColliderId(target),
                Fix64.FromRaw(distanceRaw),
                FixVector3.Zero,
                FixVector3.Zero);

            return new HitCandidate(
                new CombatEntityId(attacker),
                new CombatEntityId(target),
                1001,
                2001,
                traceId,
                new CombatFrame(10),
                hit,
                damage,
                stagger,
                knockback,
                state,
                priority);
        }

        private sealed class TestTeamRelationProvider : ITeamRelationProvider
        {
            private readonly Dictionary<CombatEntityId, int> _teams = new Dictionary<CombatEntityId, int>();

            public void SetTeam(CombatEntityId entityId, int teamId)
            {
                _teams[entityId] = teamId;
            }

            public bool AreHostile(CombatEntityId a, CombatEntityId b)
            {
                return _teams.TryGetValue(a, out int teamA)
                    && _teams.TryGetValue(b, out int teamB)
                    && teamA != teamB;
            }

            public bool AreFriendly(CombatEntityId a, CombatEntityId b)
            {
                return IsSameTeam(a, b);
            }

            public bool IsSameTeam(CombatEntityId a, CombatEntityId b)
            {
                return _teams.TryGetValue(a, out int teamA)
                    && _teams.TryGetValue(b, out int teamB)
                    && teamA == teamB;
            }
        }

        private sealed class TestTargetStateResolver : IHitTargetStateResolver
        {
            private readonly HitTargetStateFlags _state;

            public TestTargetStateResolver(HitTargetStateFlags state)
            {
                _state = state;
            }

            public CombatEntityId LastTargetId { get; private set; }

            public HitTargetStateFlags ResolveTargetState(CombatEntityId targetId)
            {
                LastTargetId = targetId;
                return _state;
            }
        }

        private sealed class TestCombatEventDispatcher : ICombatEventDispatcher
        {
            public int ResolvedCount { get; private set; }

            public HitResolveResult LastResolved { get; private set; }

            public int BlockedCount { get; private set; }

            public CombatEntityId LastBlockedAttacker { get; private set; }

            public CombatEntityId LastBlockedTarget { get; private set; }

            public int LastBlockedActionId { get; private set; }

            public CombatFrame LastBlockedFrame { get; private set; }

            public void DispatchHitResolved(in HitResolveResult result)
            {
                ResolvedCount++;
                LastResolved = result;
            }

            public void DispatchHitBlocked(CombatEntityId attackerId, CombatEntityId targetId, int actionId, CombatFrame frame)
            {
                BlockedCount++;
                LastBlockedAttacker = attackerId;
                LastBlockedTarget = targetId;
                LastBlockedActionId = actionId;
                LastBlockedFrame = frame;
            }
        }
    }
}
