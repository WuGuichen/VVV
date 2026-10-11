using MxFramework.Game.CharacterTest;
using MxFramework.Runtime;
using NUnit.Framework;

namespace MxFramework.Tests.Game.CharacterTest
{
    public sealed class CharacterTestTacticalFacingBridgeTests
    {
        [TestCase(1000, 0f, 30f, true)]
        [TestCase(-1000, 0f, 30f, true)]
        [TestCase(359000, 0f, 30f, true)]
        [TestCase(30000, 0f, 30f, true)]
        [TestCase(-30000, 0f, 30f, true)]
        [TestCase(31000, 0f, 30f, false)]
        [TestCase(-31000, 0f, 30f, false)]
        [TestCase(340000, 20f, 0f, true)]
        [TestCase(350000, 20f, 10f, true)]
        [TestCase(351000, 20f, 10f, false)]
        public void ActualBridgeUsesShortestSignedDifferenceWithSectorCenter(int yaw, float center, float tolerance, bool expected)
        {
            using (var fixture = new CharacterTestTacticalCardRuntimeFixture())
            {
                fixture.PlaceTarget(1000);
                var pose = fixture.Source.Transform;
                fixture.Source.SetTransform(CharacterTestCharacterTransformComponent.FromDeterministic(pose.XMillimeters, pose.YMillimeters, pose.ZMillimeters, yaw));
                fixture.RefreshFacts();
                var builder = new CharacterTestBehaviorExecutionBuilder(CharacterTestBehaviorExecutionEvaluatorTests.References());
                builder.AddTimeline("main", 1); builder.AddReturn("done", "main", 0, 1);
                var bridge = new CharacterTestRuntimeAiLabBehaviorCardRuntimeOverlay.RuntimeBridge(fixture.Source.Id.Value, "facing-test", builder.Build(),
                    fixture.Runtime.Entities, CharacterTestDirectAiNavigationService.Instance, fixture.Runtime.PerceptionFrameCache, fixture.Runtime, null);
                bridge.SetFrame(fixture.Source, fixture.Facts, new RuntimeFrame(fixture.NextFrame), fixture.Runtime.ActionRuntime, null);
                Assert.AreEqual(expected, bridge.Evaluate(new CharacterTestBehaviorExecutionPredicate(
                    CharacterTestBehaviorExecutionPredicateKind.TargetFacing, 0, center, tolerance)));
            }
        }
    }
}
