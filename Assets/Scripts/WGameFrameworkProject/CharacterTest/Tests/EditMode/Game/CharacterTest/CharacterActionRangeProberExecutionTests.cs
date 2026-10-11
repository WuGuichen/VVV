using MxFramework.CharacterAction;
using MxFramework.Combat.Core;
using MxFramework.Combat.Hit;
using MxFramework.Game.CharacterTest;
using MxFramework.Runtime;
using NUnit.Framework;

namespace MxFramework.Tests.Game.CharacterTest
{
    public sealed class CharacterActionRangeProberExecutionTests
    {
        [TestCase(0)]
        [TestCase(700)]
        [TestCase(1350)]
        [TestCase(1400)]
        [TestCase(1450)]
        [TestCase(2000)]
        public void Real301GatewayExecutionMatchesNominalStaticTargetPrediction(int distanceMillimeters)
        {
            using (var session = new CharacterActionReferenceProbeSession(new CharacterActionId(301),
                new CharacterActionReferenceDummy(1, 1800, 300, 0, 900, 0)))
            {
                Assert.IsTrue(session.TryEvaluate(distanceMillimeters, out var prediction, out string issue), issue);
                Assert.IsTrue(session.Runtime.TryGetLastCompletedRuntimeFrame(out var completed));
                int startFrame = (int)completed.Value + 1;
                Assert.IsTrue(session.Runtime.ActionRuntime.TryStartActionSlot(session.Source,
                    (int)CharacterCancelInputMask.AttackPrimary, session.Action.ActionId, new RuntimeFrame(startFrame)),
                    "Gateway rejected action: " + session.Source.Action.LastRejectReason);
                bool actual = false;
                int firstHitLocalFrame = -1;
                var targetId = new CombatEntityId(CharacterTestCharacterPhysicsRuntime.DynamicBodyIdStart + session.Target.Id.Value);
                for (int frame = startFrame; frame <= startFrame + session.Action.DurationFrames.Value + 2; frame++)
                {
                    Assert.AreEqual(distanceMillimeters, session.Target.Transform.ZMillimeters, "Target moved before first hit.");
                    Assert.AreEqual(0, session.Target.Transform.XMillimeters);
                    Assert.AreEqual(0, session.Target.Transform.YMillimeters);
                    session.Runtime.Tick(new RuntimeTickContext(frame, 1d / 60d, frame / 60d, RuntimeTickStage.Simulation));
                    if (distanceMillimeters == 1400 && session.Source.Action.LocalFrame >= 23 && session.Source.Action.LocalFrame <= 29)
                        TestContext.WriteLine("local=" + session.Source.Action.LocalFrame + " rootMm=" + session.Source.Transform.XMillimeters
                            + "," + session.Source.Transform.YMillimeters + "," + session.Source.Transform.ZMillimeters
                            + " candidates=" + session.Runtime.CombatTraceSystem.LastCandidateCount
                            + " traceFrames=" + session.Runtime.CombatTraceSystem.AttackTraceDebugFrames.Count);
                    foreach (HitResolveResult hit in session.Source.LastCombatHitResults)
                    {
                        if (hit.TargetId.Equals(targetId) && hit.Kind == HitResolveKind.Damage)
                        {
                            actual = true;
                            firstHitLocalFrame = session.Source.Action.LocalFrame;
                            break;
                        }
                    }
                    if (actual) break;
                }
                Assert.AreEqual(prediction.CanHitFromCurrentPose, actual,
                    "distance=" + distanceMillimeters + " predicted first frame=" + prediction.EarliestCandidateHitTick + " actual first frame=" + firstHitLocalFrame);
                TestContext.WriteLine("distance=" + distanceMillimeters + " preview=" + prediction.CanHitFromCurrentPose
                    + " actual=" + actual + " firstLocalFrame=" + firstHitLocalFrame);
            }
        }
    }
}
