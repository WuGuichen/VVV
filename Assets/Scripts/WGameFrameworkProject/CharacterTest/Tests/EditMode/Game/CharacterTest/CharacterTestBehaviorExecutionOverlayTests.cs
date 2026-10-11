using System;
using System.Collections.Generic;
using MxFramework.CharacterAction;
using MxFramework.Game.CharacterTest;
using MxFramework.Runtime;
using Newtonsoft.Json;
using NUnit.Framework;
using UnityEngine;

namespace MxFramework.Tests.Game.CharacterTest
{
    public sealed class CharacterTestBehaviorExecutionOverlayTests
    {
        [Test]
        public void DefaultCompilerInstallsNativeOverlayAndRealActionMovementRunTogether()
        {
            using var f = new Fixture();
            Assert.NotNull(f.Program.Execution); Assert.IsNull(f.Program.RecipeV6); Assert.IsEmpty(f.Program.Stages);
            bool together = false, actionAfterMovement = false, active = false, completed = false;
            for (int frame = 1; frame < 110; frame++)
            {
                f.Step(frame);
                bool running = f.Source.Action.State == MxFramework.Game.CharacterTest.CharacterTestActionStateId.Running;
                together |= running && f.Source.Input.HasActiveMove;
                actionAfterMovement |= running && !f.Source.Input.HasActiveMove && frame > 8;
                var snapshot = f.Overlay.CreateStateSnapshot(0);
                if (snapshot.Agents.Length > 0 && snapshot.Agents[0].ActiveBindingIndex >= 0)
                { active = true; Assert.NotNull(snapshot.Agents[0].ExecutionState); Assert.NotNull(snapshot.Agents[0].ExecutionRuntimeState); }
                else if (active) { completed = true; break; }
            }
            Assert.IsTrue(together, "The existing Overlay must submit action and movement in the same world frame.");
            Assert.IsTrue(actionAfterMovement, "Releasing movement must not terminate the action.");
            Assert.IsTrue(completed); Assert.AreNotEqual(MxFramework.Game.CharacterTest.CharacterTestActionStateId.Running, f.Source.Action.State); Assert.IsFalse(f.Source.Input.HasActiveMove);
            Assert.IsTrue(f.Overlay.HasTerminalCommit, "Manual card testing must observe completion after execution cleanup.");
            Assert.IsFalse(f.Overlay.HasActiveExecution, "The next card must be installable at the paused terminal boundary.");
        }

        [Test]
        public void OverlayJsonRestoreContinuesSameCommandsAndHashWithoutStageState()
        {
            using var f = new Fixture();
            for (int frame = 1; frame <= 5; frame++) f.Step(frame);
            var restored = f.CreateOverlay();
            var saved = JsonConvert.DeserializeObject<CharacterTestRuntimeAiLabLogicSlotStateSnapshot>(JsonConvert.SerializeObject(f.Overlay.CreateStateSnapshot(0)));
            Assert.IsTrue(restored.TryApplyStateSnapshot(saved, out string issue), issue);
            var canonical = new List<RuntimeCommand>(); var shadow = new List<RuntimeCommand>();
            for (int frame = 6; frame <= 15; frame++)
            {
                f.RefreshFacts(); canonical.Clear(); shadow.Clear();
                Submit(f.Overlay, f, frame, command => { canonical.Add(command); return f.Commands.Enqueue(command); });
                Submit(restored, f, frame, command => { shadow.Add(command); return RuntimeCommandValidationResult.Accepted(command); });
                CollectionAssert.AreEqual(canonical, shadow);
                f.Consume(frame);
                var first = new RuntimeHashAccumulator(RuntimeHashSchemaIds.RuntimeAiPlannerBehaviorExecution, 1); var second = new RuntimeHashAccumulator(RuntimeHashSchemaIds.RuntimeAiPlannerBehaviorExecution, 1);
                f.Overlay.CreateStateSnapshot(0).Contribute(first, 1); restored.CreateStateSnapshot(0).Contribute(second, 1);
                Assert.AreEqual(first.ToHash(), second.ToHash(), "The restored Overlay diverged at frame " + frame);
            }
        }

        [Test]
        public void InvalidRestorePreservesTheActiveOverlay()
        {
            using var f = new Fixture(); f.Step(1); f.Step(2); f.Step(3);
            var before = f.Overlay.CreateStateSnapshot(0);
            var invalid = CharacterTestRuntimeAiLabLogicSlotStateSnapshot.CloneArray(new[] { before })[0];
            invalid.Agents[0].ExecutionState.ProgramIdentity++;
            Assert.IsFalse(f.Overlay.TryApplyStateSnapshot(invalid, out _));
            var a = new RuntimeHashAccumulator(RuntimeHashSchemaIds.RuntimeAiPlannerBehaviorExecution, 1); var b = new RuntimeHashAccumulator(RuntimeHashSchemaIds.RuntimeAiPlannerBehaviorExecution, 1);
            before.Contribute(a, 1); f.Overlay.CreateStateSnapshot(0).Contribute(b, 1);
            Assert.AreEqual(a.ToHash(), b.ToHash());
        }

        [Test]
        public void NativeObservationProjectionUsesExecutionIndexesAndNeverCreatesStages()
        {
            using var f = new Fixture(); f.Step(1); f.Step(2); f.Step(3);
            var view = CharacterTestBehaviorExecutionProjection.Get(f.Program);
            Assert.AreSame(view, CharacterTestBehaviorExecutionProjection.Get(f.Program));
            Assert.AreEqual(f.Program.Execution.Events.Length, view.Events.Count);
            for (int i = 0; i < view.Events.Count; i++)
            { Assert.AreEqual(i, view.Events[i].Index); Assert.IsNull(view.Events[i].ActionStage); Assert.AreEqual(f.Program.ExecutionDebugMap.Event(i), view.Events[i].EventId); }
            for (int i = 0; i < view.Timelines.Count; i++) Assert.IsNull(view.Timelines[i].HeaderStage);
            var state = f.Overlay.CreateStateSnapshot(0).Agents[0];
            Assert.AreEqual(state.ExecutionState.Results.Length, view.Events.Count);
            CollectionAssert.AreEqual(state.ExecutionState.Results, state.EventResults);
        }

        [Test]
        public void StaleControlReleaseCannotOverwriteAnotherOwner()
        {
            using var f = new Fixture();
            var current = new CharacterTestBehaviorControlInput(2, 1000, 1000, ownerToken: 222);
            var stale = new CharacterTestBehaviorControlInput(1, 0, 0, ownerToken: 111, isRelease: true);
            Assert.IsTrue(f.Commands.Enqueue(CharacterTestRuntimeCommandFactory.SetBehaviorControlIntent(new RuntimeFrame(1),
                CharacterTestRuntimeCommandSources.RuntimeAiPlanner, f.Source.Id.Value, Vector2.right, 1f, current)).Success);
            Assert.IsTrue(f.Commands.Enqueue(CharacterTestRuntimeCommandFactory.SetBehaviorControlIntent(new RuntimeFrame(1),
                CharacterTestRuntimeCommandSources.RuntimeAiPlanner, f.Source.Id.Value, Vector2.zero, 1f, stale)).Success);
            f.Consume(1);
            Assert.AreEqual(222, f.Source.Input.BehaviorControl.OwnerToken); Assert.IsTrue(f.Source.Input.HasActiveMove);
        }

        [Test]
        public void StaleActionTerminationCannotStopAReplacementInstance()
        {
            using var f = new Fixture();
            var frame = new RuntimeFrame(1);
            Assert.IsTrue(f.Commands.Enqueue(CharacterTestRuntimeCommandFactory.BehaviorRecipeOrchestration(frame,
                CharacterTestRuntimeCommandSources.RuntimeAiPlanner, f.Source.Id.Value,
                CharacterTestBehaviorRecipeOrchestrationOperation.ReplaceCharacterAction, 301)).Success);
            f.Consume(1);
            Assert.IsTrue(f.Probe.Runtime.ActionRuntime.TryGetActiveActionPresentationState(f.Source.Id, out long old, out _, out _, out _));
            frame = new RuntimeFrame(2);
            Assert.IsTrue(f.Commands.Enqueue(CharacterTestRuntimeCommandFactory.BehaviorRecipeOrchestration(frame,
                CharacterTestRuntimeCommandSources.RuntimeAiPlanner, f.Source.Id.Value,
                CharacterTestBehaviorRecipeOrchestrationOperation.ReplaceCharacterAction, 301)).Success);
            var stale = new CharacterTestRuntimeAiLabRuntimeIntent(CharacterTestRuntimeAiLabRuntimeIntentKind.BehaviorRecipeOrchestration, Vector2.zero,
                orchestrationOperation: CharacterTestBehaviorRecipeOrchestrationOperation.TerminateForReturnToPlanner,
                orchestrationExpectedInstanceId: old);
            Assert.IsTrue(CharacterTestBehaviorExecutionCommandWriter.Submit(frame, f.Source.Id.Value, stale, f.Commands.Enqueue).Success);
            f.Consume(2);
            Assert.IsTrue(f.Probe.Runtime.ActionRuntime.TryGetActiveActionPresentationState(f.Source.Id, out long replacement, out _, out _, out _));
            Assert.AreNotEqual(old, replacement);
        }

        [Test]
        public void PassiveWaitRetainsCardOwnershipWithoutPublishingAnotherControlOnSameFrame()
        {
            using var f = new Fixture(conditionWait: true);
            // Activation owns the card; sampling its first block begins on the next world frame.
            Assert.IsTrue(f.Overlay.TryTick(f.Source, f.Facts, new RuntimeFrame(1), out var activation));
            Assert.AreEqual(CharacterTestRuntimeAiLabRuntimeIntentKind.None, activation.Kind);
            Assert.IsTrue(f.Overlay.TryTick(f.Source, f.Facts, new RuntimeFrame(2), out var entry));
            Assert.IsTrue(entry.ExecutionControl.IsOwnerOnlyRelease);
            Assert.IsTrue(CharacterTestBehaviorExecutionCommandWriter.Submit(new RuntimeFrame(2), f.Source.Id.Value, entry, f.Commands.Enqueue).Success);
            f.Consume(2);
            Assert.IsFalse(f.Source.Input.BehaviorControl.IsPresent);
            Assert.IsTrue(f.Overlay.TryTick(f.Source, f.Facts, new RuntimeFrame(2), out var repeated));
            Assert.AreEqual(CharacterTestRuntimeAiLabRuntimeIntentKind.None, repeated.Kind);
            Assert.IsTrue(f.Overlay.HasActiveExecution, "A silent wait must not fall through to ordinary Planner control.");
        }

        [Test]
        public void OwnerOnlyReleaseIsConsumedWithoutResettingPhysicsOrStoppingTheRealAction()
        {
            using var f = new Fixture();
            Assert.IsTrue(f.Commands.Enqueue(CharacterTestRuntimeCommandFactory.BehaviorRecipeOrchestration(new RuntimeFrame(1),
                CharacterTestRuntimeCommandSources.RuntimeAiPlanner, f.Source.Id.Value,
                CharacterTestBehaviorRecipeOrchestrationOperation.ReplaceCharacterAction, 301)).Success);
            f.Consume(1);
            var running = f.Probe.Runtime.ActionRuntime.GetActionInstanceForObservation(f.Source.Id);
            Assert.IsTrue(running.IsRunning); long instance = running.InstanceId; int local = running.LocalFrame;
            var claim = new CharacterTestBehaviorControlInput(2, 1000, 1000, ownerToken: 222);
            var release = new CharacterTestBehaviorControlInput(1, 0, 0, ownerToken: 222, isRelease: true, isOwnerOnlyRelease: true);
            Assert.IsTrue(f.Commands.Enqueue(CharacterTestRuntimeCommandFactory.SetBehaviorControlIntent(new RuntimeFrame(2),
                CharacterTestRuntimeCommandSources.RuntimeAiPlanner, f.Source.Id.Value, Vector2.right, 1f, claim)).Success);
            Assert.IsTrue(f.Commands.Enqueue(CharacterTestRuntimeCommandFactory.SetBehaviorControlIntent(new RuntimeFrame(2),
                CharacterTestRuntimeCommandSources.RuntimeAiPlanner, f.Source.Id.Value, Vector2.zero, 1f, release)).Success);
            var tick = new RuntimeTickContext(2, 1d / 60d, 2d / 60d, RuntimeTickStage.Simulation);
            f.Source.SetMotionState(5f, Vector2.zero, isGrounded: false, horizontalVelocity: Vector2.right * 3f);
            int vertical = f.Source.VerticalVelocityMillimetersPerSecond, horizontal = f.Source.HorizontalVelocityXMillimetersPerSecond;
            f.Commands.Tick(tick); Assert.IsEmpty(f.Commands.LastCommandErrors);
            Assert.IsFalse(f.Source.Input.HasActiveMove); Assert.IsFalse(f.Source.Input.BehaviorControl.IsPresent);
            Assert.AreEqual(vertical, f.Source.VerticalVelocityMillimetersPerSecond);
            Assert.AreEqual(horizontal, f.Source.HorizontalVelocityXMillimetersPerSecond);
            Assert.IsTrue(running.IsRunning); Assert.AreEqual(local, running.LocalFrame);
            f.Probe.Runtime.Tick(tick);
            Assert.IsTrue(running.IsRunning); Assert.AreEqual(instance, running.InstanceId); Assert.Greater(running.LocalFrame, local);
        }

        [Test]
        public void StaleOwnerOnlyReleaseCannotOverwriteReplacementMovementOrFacing()
        {
            using var f = new Fixture();
            var current = new CharacterTestBehaviorControlInput(2, 1000, 1000, ownerToken: 222);
            var stale = new CharacterTestBehaviorControlInput(1, 0, 0, ownerToken: 111, isRelease: true, isOwnerOnlyRelease: true);
            Assert.IsTrue(f.Commands.Enqueue(CharacterTestRuntimeCommandFactory.SetBehaviorControlIntent(new RuntimeFrame(1),
                CharacterTestRuntimeCommandSources.RuntimeAiPlanner, f.Source.Id.Value, Vector2.right, 1f, current)).Success);
            Assert.IsTrue(f.Commands.Enqueue(CharacterTestRuntimeCommandFactory.SetBehaviorControlIntent(new RuntimeFrame(1),
                CharacterTestRuntimeCommandSources.RuntimeAiPlanner, f.Source.Id.Value, Vector2.zero, 1f, stale)).Success);
            f.Consume(1);
            Assert.AreEqual(222, f.Source.Input.BehaviorControl.OwnerToken);
            Assert.AreEqual(2, f.Source.Input.BehaviorControl.FacingMode); Assert.IsTrue(f.Source.Input.HasActiveMove);
        }

        private static void Submit(CharacterTestRuntimeAiLabBehaviorCardRuntimeOverlay overlay, Fixture f, int frame,
            Func<RuntimeCommand, RuntimeCommandValidationResult> submit)
        {
            Assert.IsTrue(overlay.TryTick(f.Source, f.Facts, new RuntimeFrame(frame), out var intent));
            if (intent.Kind == CharacterTestRuntimeAiLabRuntimeIntentKind.None) return;
            var result = CharacterTestBehaviorExecutionCommandWriter.Submit(new RuntimeFrame(frame), f.Source.Id.Value, intent, submit);
            Assert.IsTrue(result.Success, result.Success ? string.Empty : result.Error.Message);
            overlay.RecordCommandResult(f.Source.Id.Value, result);
        }
        private sealed class Fixture : IDisposable
        {
            public readonly CharacterActionReferenceProbeSession Probe;
            public readonly CharacterTestRuntimeCommandModule Commands;
            public readonly CharacterTestRuntimeAiPlannerFactState Facts = new CharacterTestRuntimeAiPlannerFactState();
            public readonly CharacterTestRuntimeAiLabCompiledLogicProgram Program;
            public readonly CharacterTestRuntimeAiPlannerBehaviorCompositionProfile Profile;
            public readonly CharacterTestRuntimeAiLabBehaviorCardRuntimeOverlay Overlay;
            public CharacterTestCharacterEntity Source => Probe.Source;
            public Fixture(bool conditionWait = false)
            {
                Probe = new CharacterActionReferenceProbeSession(new CharacterActionId(301), new CharacterActionReferenceDummy(1, 1800, 300, 0, 900, 0));
                Assert.IsTrue(Probe.Runtime.TrySetControlOwner(Source.Id.Value, CharacterTestCharacterControlOwner.RemotePlayer));
                Probe.Target.SetTransform(CharacterTestCharacterTransformComponent.FromDeterministic(0, 0, 2000, 0));
                Probe.Runtime.Tick(new RuntimeTickContext(0, 1d / 60d, 0, RuntimeTickStage.Simulation));
                Commands = new CharacterTestRuntimeCommandModule(Probe.Runtime, NullRuntimeLogger.Instance);
                var source = new CharacterTestRuntimeAiLabBehaviorCardLogicPayload
                { BehaviorRecipeVersion = 11, EntryTimelineId = "main" };
                var timeline = new CharacterTestRuntimeAiLabBehaviorRecipeTimeline { TimelineId = "main" }; source.Timelines.Add(timeline);
                timeline.Events.Add(new CharacterTestRuntimeAiLabBehaviorRecipeEvent { EventId = "move", TimelineId = "main", DurationFrames = 4,
                    Operation = new CharacterTestRuntimeAiLabBehaviorRecipeEventOperation { Kind = CharacterTestRuntimeAiLabBehaviorRecipeEventOperationKind.MoveToTarget } });
                timeline.Events.Add(new CharacterTestRuntimeAiLabBehaviorRecipeEvent { EventId = "action", TimelineId = "main", StartFrame = 1, DurationFrames = 76,
                    Operation = new CharacterTestRuntimeAiLabBehaviorRecipeEventOperation { Kind = CharacterTestRuntimeAiLabBehaviorRecipeEventOperationKind.CharacterAction, CharacterActionId = new CharacterActionId(301) } });
                var compiled = CharacterTestRuntimeAiLabBehaviorCardLogicCompiler.Compile(source, new CharacterTestRuntimeAiLabBehaviorCardReferenceCatalog(null, Probe.Runtime.ActionRuntime.Registry));
                Assert.IsTrue(compiled.Success, JsonConvert.SerializeObject(compiled.Diagnostics)); Program = compiled.Program;
                if (conditionWait)
                {
                    var inputs = CharacterTestTacticalCardInputs.BindSlots(1, new Dictionary<string, CharacterTestTacticalCardInputs>());
                    Assert.IsTrue(CharacterTestTacticalAuthoringCompiler.TryCompileSharedTrial(
                        CharacterTestTacticalAuthoringTemplates.CreateWaitOpportunity(), inputs, out var wait, out var issues), string.Join(";", issues));
                    Program = wait.Program;
                }
                Profile = new CharacterTestRuntimeAiPlannerBehaviorCompositionProfile("execution-overlay-test");
                Profile.Entries.Add(new CharacterTestRuntimeAiPlannerBehaviorModuleEntry(CharacterTestRuntimeAiPlannerBehaviorCompositionCatalog.FoundationIdle, true, 0)
                { NumericId = 71001, DefinitionNumericId = 71002 });
                Overlay = CreateOverlay(); RefreshFacts();
            }
            public CharacterTestRuntimeAiLabBehaviorCardRuntimeOverlay CreateOverlay() => new CharacterTestRuntimeAiLabBehaviorCardRuntimeOverlay(Profile,
                new[] { Program }, Probe.Runtime.ActionRuntime, null, 71, entities: Probe.Runtime.Entities,
                navigationService: CharacterTestDirectAiNavigationService.Instance, characterRuntime: Probe.Runtime);
            public void RefreshFacts()
            {
                CharacterTestDirectAiNavigationService.Instance.TryResolveMove(Source, Probe.Target, 0f, out var navigation);
                Facts.SetTarget(Probe.Target.Id.Value, 0f, targetDistanceAvailable: false,
                    targetRootDistance: Vector2.Distance(new Vector2(Source.Transform.X, Source.Transform.Z), new Vector2(Probe.Target.Transform.X, Probe.Target.Transform.Z)),
                    navigation: navigation, selfActionRunning: Source.Action.State == MxFramework.Game.CharacterTest.CharacterTestActionStateId.Running, targetVisible: true,
                    targetLineOfSightBlocked: false, targetLastSeenAgeFrames: 0, targetThreatScore: 1f, targetDead: !Probe.Target.CombatState.IsAlive);
            }
            public void Step(int frame) { RefreshFacts(); Submit(Overlay, this, frame, Commands.Enqueue); Consume(frame); }
            public void Consume(int frame)
            {
                var tick = new RuntimeTickContext(frame, 1d / 60d, frame / 60d, RuntimeTickStage.Simulation);
                Commands.Tick(tick); Assert.AreEqual(0, Commands.LastCommandErrors.Count,
                    Commands.LastCommandErrors.Count == 0 ? string.Empty : Commands.LastCommandErrors[0].ToString()); Probe.Runtime.Tick(tick);
            }
            public void Dispose() { Commands.Dispose(); Probe.Dispose(); }
        }
    }
}
