using System.Collections.Generic;
using MxFramework.Combat.Core;
using MxFramework.Combat.Diagnostics;
using MxFramework.Combat.Hit;
using MxFramework.Combat.Physics;
using MxFramework.Core.Math;
using NUnit.Framework;

namespace MxFramework.Tests.Combat.Diagnostics
{
    public class CombatDiagnosticsTests
    {
        [Test]
        public void ReplayRecorder_CollectsFrameInputsInStableOrder()
        {
            var recorder = new CombatReplayRecorder();
            recorder.Record(new CombatReplayInput(new CombatFrame(2), new CombatEntityId(2), commandId: 1, value: 10, sourceOrder: 2));
            recorder.Record(new CombatReplayInput(new CombatFrame(1), new CombatEntityId(1), commandId: 1, value: 10, sourceOrder: 1));
            recorder.Record(new CombatReplayInput(new CombatFrame(2), new CombatEntityId(1), commandId: 2, value: 20, sourceOrder: 1));
            var results = new List<CombatReplayInput>();

            int count = recorder.CollectFrameInputs(new CombatFrame(2), results);

            Assert.AreEqual(2, count);
            Assert.AreEqual(1, results[0].EntityId.Value);
            Assert.AreEqual(2, results[1].EntityId.Value);
        }

        [Test]
        public void ReplayRecorder_HashIgnoresRecordOrder()
        {
            var first = new CombatReplayRecorder();
            var second = new CombatReplayRecorder();
            CombatReplayInput a = new CombatReplayInput(new CombatFrame(1), new CombatEntityId(1), commandId: 1, value: 10);
            CombatReplayInput b = new CombatReplayInput(new CombatFrame(2), new CombatEntityId(2), commandId: 2, value: 20);

            first.Record(a);
            first.Record(b);
            second.Record(b);
            second.Record(a);

            Assert.AreEqual(first.ComputeInputHash(), second.ComputeInputHash());
        }

        [Test]
        public void DebugSnapshot_HashIgnoresTraceRecordOrder()
        {
            CombatDebugSnapshot first = BuildSnapshot(queryOrder: 0);
            CombatDebugSnapshot second = BuildSnapshot(queryOrder: 1);

            Assert.AreEqual(first.FrameHash, second.FrameHash);
            Assert.AreEqual("frame=10 hash=" + first.FrameHash + " inputs=1 queries=2 hits=1", first.Summary);
        }

        [Test]
        public void DebugSnapshot_HashChangesWhenHitResultChanges()
        {
            CombatDebugSnapshot damage = BuildSnapshot(HitResolveKind.Damage);
            CombatDebugSnapshot blocked = BuildSnapshot(HitResolveKind.Blocked);

            Assert.AreNotEqual(damage.FrameHash, blocked.FrameHash);
        }

        [Test]
        public void DesyncDump_ReportsMismatchedHashes()
        {
            CombatDebugSnapshot expected = BuildSnapshot(HitResolveKind.Damage);
            CombatDebugSnapshot actual = BuildSnapshot(HitResolveKind.Blocked);
            var dump = new CombatDesyncDump(new CombatFrame(10), expected, actual);

            Assert.IsTrue(dump.HasMismatch);
            StringAssert.Contains("frame=10", dump.Summary);
            StringAssert.Contains(expected.FrameHash.ToString(), dump.Summary);
            StringAssert.Contains(actual.FrameHash.ToString(), dump.Summary);
        }

        private static CombatDebugSnapshot BuildSnapshot(HitResolveKind kind = HitResolveKind.Damage, int queryOrder = 0)
        {
            var builder = new CombatDebugSnapshotBuilder();
            builder.AddInput(new CombatReplayInput(new CombatFrame(10), new CombatEntityId(1), commandId: 100, value: 1));

            CombatQueryTrace first = Query(queryId: 1, targetOrder: 1);
            CombatQueryTrace second = Query(queryId: 2, targetOrder: 2);
            if (queryOrder == 0)
            {
                builder.AddQuery(first);
                builder.AddQuery(second);
            }
            else
            {
                builder.AddQuery(second);
                builder.AddQuery(first);
            }

            builder.AddHit(new CombatHitExplain(Result(kind), kind.ToString()));
            return builder.Build(new CombatFrame(10));
        }

        private static CombatQueryTrace Query(int queryId, int targetOrder)
        {
            return new CombatQueryTrace(
                new CombatFrame(10),
                new CombatQueryHeader(
                    queryId,
                    CombatQueryKind.Capsule,
                    new CombatEntityId(1),
                    traceId: 7,
                    actionId: 1001,
                    sourceOrder: targetOrder,
                    CombatPhysicsLayerMask.All));
        }

        private static HitResolveResult Result(HitResolveKind kind)
        {
            return new HitResolveResult(
                new CombatEntityId(1),
                new CombatEntityId(2),
                actionId: 1001,
                actionInstanceId: 2001,
                traceId: 7,
                frame: new CombatFrame(10),
                kind,
                damage: kind == HitResolveKind.Damage ? 10 : 0,
                staggerFrames: 3,
                knockback: new FixVector3(Fix64.One, Fix64.Zero, Fix64.Zero));
        }
    }
}
