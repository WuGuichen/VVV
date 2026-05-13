using System.Collections.Generic;
using MxFramework.Combat.Animation;
using MxFramework.Combat.Core;
using MxFramework.Combat.Hit;
using MxFramework.Combat.Physics;
using MxFramework.Core.Math;
using NUnit.Framework;

namespace MxFramework.Tests.Combat.Animation
{
    public class CombatWeaponTraceEvaluatorTests
    {
        [Test]
        public void TimelineTraceProvider_RegistersSequenceQueriesAndClearsAction()
        {
            var provider = new CombatActionTimelineTraceProvider();
            WeaponTraceFrame first = Trace(traceId: 7, radius: Fix64.Half);
            WeaponTraceFrame second = Trace(traceId: 8, radius: Fix64.One);
            var results = new List<WeaponTraceFrame>();

            provider.RegisterTrace(1001, localFrame: 2, first);
            provider.RegisterTraceSequence(1001, new[] { (Frame: 2, Trace: second) });
            provider.GetActiveTraces(new CombatEntityId(1), 1001, 1, localFrame: 2, results);

            Assert.AreEqual(2, results.Count);
            Assert.AreEqual(7, results[0].TraceId);
            Assert.AreEqual(8, results[1].TraceId);

            provider.ClearAction(1001);
            results.Clear();
            provider.GetActiveTraces(new CombatEntityId(1), 1001, 1, localFrame: 2, results);
            Assert.AreEqual(0, results.Count);
        }

        [Test]
        public void HitCollector_DeduplicatesSortsAndResets()
        {
            var collector = new CombatHitCollector();
            var raw = new List<HitCandidate>
            {
                Candidate(target: 3, traceId: 7, distanceRaw: 3000000),
                Candidate(target: 2, traceId: 7, distanceRaw: 1000000),
                Candidate(target: 2, traceId: 7, distanceRaw: 2000000),
                Candidate(target: 4, traceId: 8, distanceRaw: 4000000, priority: 10),
            };
            var deduplicated = new List<HitCandidate>();

            int firstCount = collector.Collect(raw, deduplicated);
            int secondCount = collector.Collect(raw, deduplicated);
            collector.Reset();
            int afterResetCount = collector.Collect(raw, deduplicated);

            Assert.AreEqual(3, firstCount);
            Assert.AreEqual(0, secondCount);
            Assert.AreEqual(3, afterResetCount);
            Assert.AreEqual(4, deduplicated[0].TargetId.Value);
            Assert.AreEqual(2, deduplicated[1].TargetId.Value);
            Assert.AreEqual(3, deduplicated[2].TargetId.Value);
            Assert.AreEqual(6, deduplicated.Count);
        }

        [Test]
        public void Evaluator_UsesActiveActionTracePhysicsAndAliveTargetState()
        {
            CombatWeaponTraceEvaluator evaluator = CreateEvaluator(out CombatActionRunner runner, out CombatActionTimelineTraceProvider provider, out _);
            var attacker = new CombatEntityId(1);
            var results = new List<HitCandidate>();

            runner.StartAction(attacker, 1001, CombatFrame.Zero);
            runner.TickActions(new CombatFrame(1));
            provider.RegisterTrace(1001, localFrame: 1, Trace(traceId: 7, radius: Fix64.Half));

            evaluator.EvaluateAll(new CombatFrame(1), results);

            Assert.AreEqual(1, results.Count);
            HitCandidate candidate = results[0];
            Assert.AreEqual(1, candidate.AttackerId.Value);
            Assert.AreEqual(2, candidate.TargetId.Value);
            Assert.AreEqual(1001, candidate.ActionId);
            Assert.AreEqual(1, candidate.ActionInstanceId);
            Assert.AreEqual(7, candidate.TraceId);
            Assert.AreEqual(HitTargetStateFlags.Alive, candidate.TargetState);
            Assert.AreEqual(new WeaponHitOnceKey(1, 7, new CombatEntityId(2)), candidate.HitOnceKey);
            Assert.AreEqual(CombatQueryKind.Capsule, candidate.PhysicsHit.Query.Kind);
        }

        [Test]
        public void Evaluator_UsesTargetStateResolverWhenProvided()
        {
            CombatWeaponTraceEvaluator evaluator = CreateEvaluator(
                out CombatActionRunner runner,
                out CombatActionTimelineTraceProvider provider,
                out _,
                _ => HitTargetStateFlags.Alive | HitTargetStateFlags.Invincible);
            var results = new List<HitCandidate>();

            runner.StartAction(new CombatEntityId(1), 1001, CombatFrame.Zero);
            runner.TickActions(new CombatFrame(1));
            provider.RegisterTrace(1001, localFrame: 1, Trace(traceId: 7, radius: Fix64.Half));

            evaluator.EvaluateAll(new CombatFrame(1), results);

            Assert.AreEqual(HitTargetStateFlags.Alive | HitTargetStateFlags.Invincible, results[0].TargetState);
        }

        [Test]
        public void Evaluator_IgnoresNonActiveActions()
        {
            CombatWeaponTraceEvaluator evaluator = CreateEvaluator(out CombatActionRunner runner, out CombatActionTimelineTraceProvider provider, out _);
            var results = new List<HitCandidate>();

            runner.StartAction(new CombatEntityId(1), 1001, CombatFrame.Zero);
            provider.RegisterTrace(1001, localFrame: 0, Trace(traceId: 7, radius: Fix64.Half));

            evaluator.EvaluateAll(CombatFrame.Zero, results);

            Assert.AreEqual(0, results.Count);
        }

        [Test]
        public void Evaluator_ReturnsNoCandidatesWhenPhysicsMisses()
        {
            CombatWeaponTraceEvaluator evaluator = CreateEvaluator(out CombatActionRunner runner, out CombatActionTimelineTraceProvider provider, out _);
            var results = new List<HitCandidate>();

            runner.StartAction(new CombatEntityId(1), 1001, CombatFrame.Zero);
            runner.TickActions(new CombatFrame(1));
            provider.RegisterTrace(1001, localFrame: 1, Trace(traceId: 7, radius: Fix64.Zero, tipNowX: 1));

            evaluator.EvaluateAll(new CombatFrame(1), results);

            Assert.AreEqual(0, results.Count);
        }

        [Test]
        public void Evaluator_ZeroRadiusTraceCanStillHitIntersectingAabb()
        {
            CombatWeaponTraceEvaluator evaluator = CreateEvaluator(out CombatActionRunner runner, out CombatActionTimelineTraceProvider provider, out _);
            var results = new List<HitCandidate>();

            runner.StartAction(new CombatEntityId(1), 1001, CombatFrame.Zero);
            runner.TickActions(new CombatFrame(1));
            provider.RegisterTrace(1001, localFrame: 1, Trace(traceId: 7, radius: Fix64.Zero));

            evaluator.EvaluateAll(new CombatFrame(1), results);

            Assert.AreEqual(1, results.Count);
            Assert.AreEqual(2, results[0].TargetId.Value);
        }

        private static CombatWeaponTraceEvaluator CreateEvaluator(
            out CombatActionRunner runner,
            out CombatActionTimelineTraceProvider provider,
            out CombatPhysicsWorld world,
            System.Func<CombatEntityId, HitTargetStateFlags> targetStateResolver = null)
        {
            var registry = new CombatActionRegistry();
            registry.RegisterTimeline(1001, new CombatActionTimeline(
                1001,
                4,
                new CombatFrameRange(0, 0),
                new CombatFrameRange(1, 2),
                new CombatFrameRange(3, 3),
                null,
                null));
            runner = new CombatActionRunner(registry);
            provider = new CombatActionTimelineTraceProvider();
            world = new CombatPhysicsWorld();
            RegisterBodyWithAabb(world, entity: 1, body: 1, collider: 1, layer: 1, x: 0);
            RegisterBodyWithAabb(world, entity: 2, body: 2, collider: 1, layer: 1, x: 3);
            return targetStateResolver == null
                ? new CombatWeaponTraceEvaluator(runner, provider, world)
                : new CombatWeaponTraceEvaluator(runner, provider, world, targetStateResolver);
        }

        private static WeaponTraceFrame Trace(int traceId, Fix64 radius, int tipNowX = 4)
        {
            return new WeaponTraceFrame(
                traceId,
                rootPrev: FixVector3.Zero,
                tipPrev: FixVector3.Zero,
                rootNow: FixVector3.Zero,
                tipNow: new FixVector3(Fix64.FromInt(tipNowX), Fix64.Zero, Fix64.Zero),
                radius,
                CombatPhysicsLayerMask.FromLayer(1));
        }

        private static HitCandidate Candidate(int target, int traceId, int distanceRaw, int priority = 0)
        {
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
                1,
                traceId,
                new CombatFrame(1),
                hit,
                damage: 0,
                staggerFrames: 0,
                knockback: FixVector3.Zero,
                targetState: HitTargetStateFlags.Alive,
                resolvePriority: priority);
        }

        private static void RegisterBodyWithAabb(
            CombatPhysicsWorld world,
            int entity,
            int body,
            int collider,
            int layer,
            int x)
        {
            world.UpsertBody(new CombatPhysicsBody(
                new CombatEntityId(entity),
                new CombatBodyId(body),
                new FixVector3(Fix64.FromInt(x), Fix64.Zero, Fix64.Zero)));
            world.UpsertAabbCollider(new CombatPhysicsAabbCollider(
                new CombatBodyId(body),
                new CombatColliderId(collider),
                layer,
                new FixVector3(-Fix64.Half, -Fix64.Half, -Fix64.Half),
                new FixVector3(Fix64.Half, Fix64.Half, Fix64.Half)));
        }
    }
}
