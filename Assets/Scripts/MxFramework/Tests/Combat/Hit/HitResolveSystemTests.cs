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
        public void InvalidCandidate_Throws()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => Candidate(target: 2, damage: -1));
            Assert.Throws<ArgumentOutOfRangeException>(() => Candidate(target: 2, stagger: -1));
        }

        private static HitCandidate Candidate(
            int target,
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
                new CombatEntityId(1),
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
                new CombatEntityId(1),
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
    }
}
