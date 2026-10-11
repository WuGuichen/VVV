using System.Linq;
using MxFramework.Game.CharacterTest;
using NUnit.Framework;
using UnityEngine;

namespace MxFramework.Tests.Game.CharacterTest
{
    public sealed class CharacterTestNativeExecutionViewTests
    {
        [Test]
        public void NativeViewPreservesExecutionFieldsAndReusesTheSameImmutableProgram()
        {
            using var fixture = new CharacterTestTacticalCardRuntimeFixture();
            Assert.IsTrue(CharacterTestTacticalDslCompiler.TryCompile(CharacterTestTacticalDslTrialTemplates.Create(), fixture.Inputs, out var compiled, out string issue), issue);
            var view = CharacterTestNativeExecutionView.Get(compiled.Program);
            Assert.AreSame(view, CharacterTestNativeExecutionView.Get(compiled.Program));
            Assert.AreSame(compiled.Program, view.Program);
            var entries = view.Payload.Timelines.SelectMany(t => t.Events).ToDictionary(e => e.EventId);
            Assert.AreEqual(compiled.Program.Execution.Events.Length, entries.Count);
            for (int i = 0; i < compiled.Program.Execution.Events.Length; i++)
            {
                var ev = compiled.Program.Execution.Events[i]; var shown = entries[compiled.Program.ExecutionDebugMap.Event(i)];
                Assert.AreEqual(ev.StartFrame, shown.StartFrame); Assert.AreEqual(ev.DurationFrames, shown.DurationFrames);
                Assert.AreEqual(ev.Timing, shown.TimingKind); Assert.AreEqual(ev.Clock, shown.ClockOrigin);
                Assert.AreEqual(ev.Priority, shown.Priority); Assert.AreEqual(ev.MaxExecutionsPerPass, shown.MaxExecutionsPerPass);
                Assert.AreEqual(ev.CooldownFrames, shown.CooldownFrames);
                if (ev.Kind == CharacterTestRuntimeAiLabBehaviorRecipeEventOperationKind.CharacterAction)
                    Assert.AreEqual(compiled.Program.Execution.Actions[ev.Parameter].ActionId, shown.Operation.CharacterActionId.Value);
                if (ev.Kind == CharacterTestRuntimeAiLabBehaviorRecipeEventOperationKind.EnterTimeline)
                    Assert.AreEqual(compiled.Program.ExecutionDebugMap.Timeline(compiled.Program.Execution.Flows[ev.Parameter].TargetTimeline), shown.Operation.TargetTimelineId);
                if (ev.Kind == CharacterTestRuntimeAiLabBehaviorRecipeEventOperationKind.MoveToTarget)
                {
                    var move = compiled.Program.Execution.Movements[ev.Parameter];
                    Assert.AreEqual(move.Minimum, shown.Operation.MoveMinDistance); Assert.AreEqual(move.Maximum, shown.Operation.MoveMaxDistance);
                    Assert.AreEqual(move.Speed, shown.Operation.MoveSpeedMultiplier); Assert.AreEqual(move.Target, shown.Operation.MovementTargetKind);
                }
                Assert.AreEqual(ev.Condition >= 0, shown.ConditionRoot != null);
            }
        }

        [Test]
        public void OriginalInspectorReadsNativeConditionsWithoutEnablingAuthorWrites()
        {
            using var fixture = new CharacterTestTacticalCardRuntimeFixture();
            Assert.IsTrue(CharacterTestTacticalDslCompiler.TryCompile(CharacterTestTacticalDslTrialTemplates.Create(), fixture.Inputs, out var compiled, out string issue), issue);
            var go = new GameObject("Native inspector test"); go.SetActive(false);
            try
            {
                var controller = go.AddComponent<CharacterTestRuntimeAiPlannerDevSceneController>();
                var session = new CharacterTestTacticalCompositionEditingSession(controller); session.SetNativeExecutionView(true);
                var view = CharacterTestNativeExecutionView.Get(compiled.Program);
                var model = new CharacterTestRuntimeAiLabBehaviorCardTimelineViewModel { Available = true, CanEdit = true,
                    CompositionEditor = session, NativeExecution = view, RecipePayload = view.Payload,
                    CompiledRecipeV6 = CharacterTestBehaviorExecutionProjection.Get(compiled.Program) };
                foreach (var timeline in view.Payload.Timelines)
                    foreach (var evt in timeline.Events)
                    {
                        model.Presentation.SelectEvent(timeline.TimelineId, evt.EventId);
                        BehaviorRecipeTriggerEventInspectorProjection.Rebuild(model, view.Payload, null);
                        Assert.IsTrue(model.RecipeInspector.UnifiedV6); Assert.IsFalse(model.RecipeInspector.CanEdit);
                        Assert.IsNull(model.RecipeInspector.TacticalEditor); Assert.AreEqual(evt.EventId, model.RecipeInspector.EventId);
                        if (evt.ConditionRoot != null) Assert.IsNotEmpty(model.RecipeInspector.Conditions);
                    }
                var recreated = new CharacterTestTacticalCompositionEditingSession(controller);
                Assert.IsTrue(recreated.NativeExecutionView, "A rebuilt editing session must retain the viewer choice.");
                session.SetNativeExecutionView(false); Assert.IsFalse(model.InspectCompiledExecution);
            }
            finally { Object.DestroyImmediate(go); }
        }
    }
}
