using System;
using System.Collections.Generic;
using MxFramework.Game.CharacterTest;
using MxFramework.Runtime;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;

namespace MxFramework.Tests.Game.CharacterTest
{
    public sealed class TacticalEnemyCardPresentationTests
    {
        private static CharacterTestRuntimeAiLabCompiledLogicProgram WaitProgram(out CharacterTestTacticalDescriptionSegment segment)
        {
            var source = CharacterTestTacticalAuthoringTemplates.CreateWaitTwoSeconds();
            var published = CharacterTestTacticalPublishedSource.Read(new JObject {
                ["schema"] = CharacterTestTacticalPublishedSource.Schema,
                ["legacy"] = JObject.Parse(CharacterTestTacticalAuthoringJson.Write(source)) });
            Assert.IsTrue(published.TryCompile("wait-test", "真实等待", 1, CharacterTestTacticalCardInputs.Unbound(1), out var program, out var issue), issue);
            segment = new CharacterTestTacticalDescriptionSegment(source.Timelines[0].EventBlocks[0].BlockId, "preview-legacy", "等待两秒");
            return program;
        }

        private static long Hash(CharacterTestRuntimeAiLabBehaviorCardLogicEvaluator evaluator)
        {
            var hash = new RuntimeHashAccumulator(1001, 1);
            evaluator.CreateSnapshot().ExecutionState.AddHash(hash);
            return hash.ToHash();
        }

        [Test]
        public void PendingReadDoesNotStartEvaluateOrPayForCard()
        {
            var program = WaitProgram(out var segment);
            var runtime = new CharacterTestBehaviorExecutionEvaluatorTests.RuntimeStub();
            var evaluator = new CharacterTestRuntimeAiLabBehaviorCardLogicEvaluator(program, runtime);
            var before = Hash(evaluator); var view = new CharacterTestEnemyCardPresentation();
            int evaluations = runtime.EvaluateCount;
            view.Read("wait-test#1", program, evaluator, true, false); view.BindSegments(new[] { segment });
            Assert.IsTrue(view.Pending);
            Assert.AreEqual(CharacterTestEnemyIntentState.NotStarted, view.Intent(segment));
            Assert.AreEqual(-1, view.RemainingDelayFrames(segment));
            Assert.AreEqual(before, Hash(evaluator)); Assert.AreEqual(evaluations, runtime.EvaluateCount);
            Assert.AreEqual(0, runtime.StartRequests);
        }

        [Test]
        public void RepeatedReadsFreezeWaitAndNeverAdvanceOrChangeTarget()
        {
            var program = WaitProgram(out var segment);
            var runtime = new CharacterTestBehaviorExecutionEvaluatorTests.RuntimeStub();
            var evaluator = new CharacterTestRuntimeAiLabBehaviorCardLogicEvaluator(program, runtime);
            var events = new CharacterTestRuntimeAiLabLogicEventBuffer();
            Assert.IsTrue(evaluator.Start(0, events)); evaluator.Tick(1, events); evaluator.Tick(10, events);
            var view = new CharacterTestEnemyCardPresentation();
            view.Read("wait-test#1", program, evaluator, false, false); view.BindSegments(new[] { segment });
            Assert.AreEqual(CharacterTestEnemyIntentState.Running, view.Intent(segment));
            int remaining = view.RemainingDelayFrames(segment); Assert.Greater(remaining, 0);
            var before = Hash(evaluator); int evaluations = runtime.EvaluateCount;
            for (int i = 0; i < 100; i++)
            {
                view.Read("wait-test#1", program, evaluator, false, false);
                Assert.AreEqual(remaining, view.RemainingDelayFrames(segment));
            }
            Assert.AreEqual(before, Hash(evaluator)); Assert.AreEqual(evaluations, runtime.EvaluateCount);
            evaluator.Tick(11, events); view.Read("wait-test#1", program, evaluator, false, false);
            Assert.AreEqual(remaining - 1, view.RemainingDelayFrames(segment));
            evaluator.Tick(300, events); view.Read("wait-test#1", program, evaluator, false, false);
            Assert.AreEqual(CharacterTestEnemyIntentState.Completed, view.Intent(segment));
        }

        [Test]
        public void UnmappedOrAutomaticProgressIsUnavailableAndCannotReusePreviousExecution()
        {
            var program = WaitProgram(out var segment);
            var evaluator = new CharacterTestRuntimeAiLabBehaviorCardLogicEvaluator(program, new CharacterTestBehaviorExecutionEvaluatorTests.RuntimeStub());
            var events = new CharacterTestRuntimeAiLabLogicEventBuffer(); evaluator.Start(0, events); evaluator.Tick(1, events);
            var view = new CharacterTestEnemyCardPresentation(); view.Read("first", program, evaluator, false, false);
            view.BindSegments(new[] { segment }); Assert.AreEqual(CharacterTestEnemyIntentState.Running, view.Intent(segment));
            view.Read("automatic", program, evaluator, false, true);
            Assert.AreEqual(CharacterTestEnemyIntentState.Unavailable, view.Intent(segment));
            Assert.AreEqual(-1, view.RemainingDelayFrames(segment));
            var unknown = new CharacterTestTacticalDescriptionSegment("absent", "preview-legacy", "不能猜测");
            view.BindSegments(new[] { unknown }); Assert.AreEqual(CharacterTestEnemyIntentState.Unavailable, view.Intent(unknown));
        }

        [Test]
        public void SharedDefinitionInstancesMapByAuthoredNodeInsteadOfDescriptionOrder()
        {
            var source = CharacterTestTacticalAuthoringTemplates.CreateWaitTwoSeconds();
            var definition = new CharacterTestTacticalSharedDefinition { DefinitionId = "shared-wait", Revision = 1, Source = source };
            var composition = new CharacterTestTacticalCompositionSource {
                Root = new CharacterTestTacticalDslNode { NodeId = "root", Kind = "Sequence", Children = new List<CharacterTestTacticalDslNode> {
                    CharacterTestTacticalDslTrialTemplates.Reference("first", "shared-wait"),
                    CharacterTestTacticalDslTrialTemplates.Reference("second", "shared-wait") } } };
            var published = CharacterTestTacticalPublishedSource.Read(new JObject {
                ["schema"] = CharacterTestTacticalPublishedSource.Schema,
                ["composition"] = JObject.Parse(CharacterTestTacticalCompositionJson.Write(composition)),
                ["definitions"] = new JArray(JObject.Parse(CharacterTestTacticalCompositionJson.WriteShared(definition))) });
            Assert.IsTrue(published.TryCompile("shared-test", "两次等待", 1, CharacterTestTacticalCardInputs.Unbound(1), out var program, out var issue), issue);
            var first = new CharacterTestTacticalDescriptionSegment(source.Timelines[0].EventBlocks[0].BlockId, "first", "等待");
            var second = new CharacterTestTacticalDescriptionSegment(source.Timelines[0].EventBlocks[0].BlockId, "second", "等待");
            var runtime = new CharacterTestBehaviorExecutionEvaluatorTests.RuntimeStub();
            var evaluator = new CharacterTestRuntimeAiLabBehaviorCardLogicEvaluator(program, runtime);
            var events = new CharacterTestRuntimeAiLabLogicEventBuffer(); evaluator.Start(0, events); evaluator.Tick(1, events);
            var view = new CharacterTestEnemyCardPresentation(); view.Read("shared-test#1", program, evaluator, false, false);
            view.BindSegments(new[] { second, first });
            Assert.AreEqual(CharacterTestEnemyIntentState.Running, view.Intent(first));
            Assert.AreEqual(CharacterTestEnemyIntentState.NotStarted, view.Intent(second));
        }

        [Test]
        public void CompletedAndFailedFactsDoNotMarkFutureIntentCompleted()
        {
            Assert.AreEqual(CharacterTestEnemyIntentState.NotStarted, CharacterTestEnemyCardPresentation.ResolveIntent(0, CharacterTestRuntimeAiLabLogicRuntimeState.Failed));
            Assert.AreEqual(CharacterTestEnemyIntentState.Completed, CharacterTestEnemyCardPresentation.ResolveIntent(2, CharacterTestRuntimeAiLabLogicRuntimeState.Running));
            Assert.AreEqual(CharacterTestEnemyIntentState.Failed, CharacterTestEnemyCardPresentation.ResolveIntent(1, CharacterTestRuntimeAiLabLogicRuntimeState.Failed));
            Assert.AreEqual(CharacterTestEnemyIntentState.Unavailable, CharacterTestEnemyCardPresentation.ResolveIntent(1, CharacterTestRuntimeAiLabLogicRuntimeState.Completed));
        }

        [Test]
        public void OverlappingMarkersStayDistinctAndEdgePopupStaysBesideActor()
        {
            var occupied = new List<Rect>(); var body = new Rect(900, 350, 90, 210);
            for (int i = 0; i < 6; i++)
            {
                var marker = CharacterTestFairyGuiTacticalBattleView.PlaceEnemyMarker(body, occupied);
                Assert.Greater(marker.width, 0);
                foreach (var previous in occupied) Assert.IsFalse(marker.Overlaps(previous));
                occupied.Add(marker);
            }
            var edge = new Rect(1820, 350, 60, 200);
            var popup = CharacterTestFairyGuiTacticalBattleView.PlaceEnemyPopup(edge, 244, 377, Array.Empty<Rect>());
            Assert.Less(popup.xMax, edge.xMin); Assert.GreaterOrEqual(popup.xMin, 0);
            Assert.LessOrEqual(popup.yMax, 730);
        }
    }
}
