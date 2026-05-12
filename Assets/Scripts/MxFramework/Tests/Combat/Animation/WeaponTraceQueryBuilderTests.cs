using System;
using System.Collections.Generic;
using MxFramework.Combat.Animation;
using MxFramework.Combat.Core;
using MxFramework.Combat.Physics;
using MxFramework.Core.Math;
using NUnit.Framework;

namespace MxFramework.Tests.Combat.Animation
{
    public class WeaponTraceQueryBuilderTests
    {
        [Test]
        public void BuildCurrentBladeCapsule_UsesCurrentRootAndTip()
        {
            WeaponTraceFrame frame = CreateFrame(tipNowX: 4);

            CombatCapsuleQuery query = WeaponTraceQueryBuilder.BuildCurrentBladeCapsule(
                frame,
                new CombatEntityId(11),
                actionId: 1001,
                queryId: 8,
                sourceOrder: 2);

            Assert.AreEqual(frame.RootNow, query.PointA);
            Assert.AreEqual(frame.TipNow, query.PointB);
            Assert.AreEqual(frame.Radius, query.Radius);
            Assert.AreEqual(8, query.Header.QueryId);
            Assert.AreEqual(11, query.Header.SourceEntityId.Value);
            Assert.AreEqual(1001, query.Header.ActionId);
            Assert.AreEqual(2, query.Header.SourceOrder);
        }

        [Test]
        public void GetTipSweepSubstepCount_UsesDeterministicDistanceThreshold()
        {
            WeaponTraceFrame frame = CreateFrame(tipNowX: 10);

            int count = WeaponTraceQueryBuilder.GetTipSweepSubstepCount(
                frame,
                Fix64.FromInt(3),
                maxSubsteps: 8);

            Assert.AreEqual(4, count);
        }

        [Test]
        public void GetTipSweepSubstepCount_ClampsToMaxSubsteps()
        {
            WeaponTraceFrame frame = CreateFrame(tipNowX: 10);

            int count = WeaponTraceQueryBuilder.GetTipSweepSubstepCount(
                frame,
                Fix64.One,
                maxSubsteps: 3);

            Assert.AreEqual(3, count);
        }

        [Test]
        public void BuildTipSweepSegments_InterpolatesTipPathInStableOrder()
        {
            WeaponTraceFrame frame = CreateFrame(tipNowX: 10);
            var segments = new List<WeaponTraceSegment>();

            int count = WeaponTraceQueryBuilder.BuildTipSweepSegments(
                frame,
                Fix64.FromInt(3),
                maxSubsteps: 8,
                segments);

            Assert.AreEqual(4, count);
            Assert.AreEqual(0, segments[0].SegmentIndex);
            Assert.AreEqual(0, segments[0].PointA.X.RawValue);
            Assert.AreEqual(2500000, segments[0].PointB.X.RawValue);
            Assert.AreEqual(7500000, segments[3].PointA.X.RawValue);
            Assert.AreEqual(10000000, segments[3].PointB.X.RawValue);
            Assert.AreEqual(frame.Radius, segments[3].Radius);
            Assert.AreEqual(frame.TargetMask, segments[3].TargetMask);
        }

        [Test]
        public void BuildTipSweepCapsules_OutputsCapsuleQueriesForPhysics()
        {
            WeaponTraceFrame frame = CreateFrame(tipNowX: 10);
            var queries = new List<CombatCapsuleQuery>();

            int count = WeaponTraceQueryBuilder.BuildTipSweepCapsules(
                frame,
                new CombatEntityId(11),
                actionId: 1001,
                queryIdStart: 20,
                Fix64.FromInt(3),
                maxSubsteps: 8,
                queries);

            Assert.AreEqual(4, count);
            Assert.AreEqual(20, queries[0].Header.QueryId);
            Assert.AreEqual(23, queries[3].Header.QueryId);
            Assert.AreEqual(0, queries[0].Header.SourceOrder);
            Assert.AreEqual(3, queries[3].Header.SourceOrder);
            Assert.AreEqual(frame.TraceId, queries[2].Header.TraceId);
            Assert.AreEqual(CombatQueryKind.Capsule, queries[2].Header.Kind);
            Assert.AreEqual(5000000, queries[1].PointB.X.RawValue);
        }

        [Test]
        public void InvalidTraceInput_Throws()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new WeaponTraceFrame(
                -1,
                FixVector3.Zero,
                FixVector3.Zero,
                FixVector3.Zero,
                FixVector3.Zero,
                Fix64.One,
                CombatPhysicsLayerMask.All));

            Assert.Throws<ArgumentOutOfRangeException>(() => new WeaponTraceFrame(
                1,
                FixVector3.Zero,
                FixVector3.Zero,
                FixVector3.Zero,
                FixVector3.Zero,
                -Fix64.One,
                CombatPhysicsLayerMask.All));

            Assert.Throws<ArgumentOutOfRangeException>(() => WeaponTraceQueryBuilder.GetTipSweepSubstepCount(
                CreateFrame(tipNowX: 1),
                Fix64.Zero,
                maxSubsteps: 8));
        }

        [Test]
        public void HitOnceKey_DeduplicatesPerActionInstanceTraceAndTarget()
        {
            var first = new WeaponHitOnceKey(100, 7, new CombatEntityId(3));
            var duplicate = new WeaponHitOnceKey(100, 7, new CombatEntityId(3));
            var otherTrace = new WeaponHitOnceKey(100, 8, new CombatEntityId(3));
            var set = new HashSet<WeaponHitOnceKey>();

            Assert.IsTrue(set.Add(first));
            Assert.IsFalse(set.Add(duplicate));
            Assert.IsTrue(set.Add(otherTrace));
            Assert.AreEqual(2, set.Count);
        }

        private static WeaponTraceFrame CreateFrame(int tipNowX)
        {
            return new WeaponTraceFrame(
                traceId: 7,
                rootPrev: FixVector3.Zero,
                tipPrev: FixVector3.Zero,
                rootNow: new FixVector3(Fix64.Zero, Fix64.FromInt(1), Fix64.Zero),
                tipNow: new FixVector3(Fix64.FromInt(tipNowX), Fix64.Zero, Fix64.Zero),
                radius: Fix64.Half,
                targetMask: CombatPhysicsLayerMask.FromLayer(2));
        }
    }
}
