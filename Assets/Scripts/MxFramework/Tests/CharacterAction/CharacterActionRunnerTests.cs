using MxFramework.CharacterAction;
using MxFramework.Combat.Animation;
using NUnit.Framework;

namespace MxFramework.Tests.CharacterAction
{
    public sealed class CharacterActionRunnerTests
    {
        [Test]
        public void Runner_StartsPlanTicksFramesAndFinishes()
        {
            CharacterActionConfig action = CreateAction(
                id: 100,
                stableId: "light_attack",
                durationFrames: 4);
            CharacterActionRunner runner = StartRunner(action, out CharacterActionRunnerOperationResult start);

            Assert.IsTrue(start.Accepted);
            Assert.AreEqual(1, runner.ActiveInstance.InstanceId);
            Assert.AreEqual("light_attack", runner.ActiveInstance.Plan.ActionId);
            Assert.AreEqual(CharacterActionInstanceState.Running, runner.ActiveInstance.State);
            Assert.AreEqual(0, runner.ActiveInstance.LocalFrame);
            AssertHasEvent(start.Events, CharacterActionRunnerEventKind.ActionStarted);
            AssertHasEvent(start.Events, CharacterActionRunnerEventKind.PhaseChanged);

            runner.Tick();
            runner.Tick();
            CharacterActionRunnerOperationResult lastFrame = runner.Tick();

            Assert.AreEqual(3, runner.ActiveInstance.LocalFrame);
            Assert.AreEqual(CharacterActionInstanceState.Running, runner.ActiveInstance.State);
            AssertNoEvent(lastFrame.Events, CharacterActionRunnerEventKind.ActionFinished);

            CharacterActionRunnerOperationResult finish = runner.Tick();

            Assert.AreEqual(4, runner.ActiveInstance.LocalFrame);
            Assert.AreEqual(CharacterActionInstanceState.Finished, runner.ActiveInstance.State);
            Assert.AreEqual("finished", runner.ActiveInstance.FinishReason);
            AssertHasEvent(finish.Events, CharacterActionRunnerEventKind.ActionFinished);
        }

        [Test]
        public void Runner_StartResolveResultWithoutExecutionDefinitionIsRejected()
        {
            CharacterActionConfig action = CreateAction(
                id: 100,
                stableId: "phase_only_action",
                durationFrames: 3);
            var runner = new CharacterActionRunner();

            CharacterActionRunnerOperationResult start = runner.Start(CharacterActionResolveResult.Success(CreatePlan(action)));

            Assert.IsFalse(start.Accepted);
            Assert.AreEqual(CharacterActionDiagnosticCodes.MissingActionConfig, start.Diagnostics[0].Code);
            Assert.IsFalse(runner.HasActiveInstance);
        }

        [Test]
        public void RunnerActionDefinition_FromPlanIsDisabled()
        {
            CharacterActionConfig action = CreateAction(
                id: 100,
                stableId: "phase_only_action",
                durationFrames: 3);
            CharacterActionPlan plan = CreatePlan(action);

            Assert.Throws<System.NotSupportedException>(() => CharacterActionRunnerActionDefinition.FromPlan(plan));
        }

        [Test]
        public void Runner_StartWithMissingTrackDispatchDefinitionIsRejected()
        {
            CharacterActionConfig action = CreateAction(
                id: 100,
                stableId: "track_action",
                durationFrames: 3,
                debugTrack: new DebugTrackConfig(new[]
                {
                    new DebugTrackEvent(1, CharacterActionTrackEventKind.EmitDebugMarker, "marker.track", "debug.track"),
                }));
            CharacterActionPlan plan = CreatePlan(action);
            var runner = new CharacterActionRunner();
            var definition = new CharacterActionRunnerActionDefinition(
                action.Id,
                action.StableId,
                action.TimelineAuthority,
                action.CancelRules,
                action.InterruptRules,
                combatTimeline: null,
                trackEvents: null);

            CharacterActionRunnerOperationResult start = runner.Start(
                CharacterActionResolveResult.Success(plan),
                definition);

            Assert.IsFalse(start.Accepted);
            Assert.AreEqual(CharacterActionDiagnosticCodes.MissingActionConfig, start.Diagnostics[0].Code);
            Assert.IsFalse(runner.HasActiveInstance);
        }

        [Test]
        public void Runner_FromConfigExecutionStillStartsPlan()
        {
            CharacterActionConfig action = CreateAction(
                id: 100,
                stableId: "phase_only_action",
                durationFrames: 3);

            CharacterActionRunner runner = StartRunner(action, out CharacterActionRunnerOperationResult start);

            Assert.IsTrue(start.Accepted);
            Assert.AreEqual("phase_only_action", runner.ActiveInstance.Plan.ActionId);
            Assert.AreEqual(0, runner.ActiveInstance.LocalFrame);
            AssertHasEvent(start.Events, CharacterActionRunnerEventKind.ActionStarted);
            AssertHasEvent(start.Events, CharacterActionRunnerEventKind.PhaseChanged);
        }

        [Test]
        public void Runner_DurationOneFinishesAfterLastLegalFrame()
        {
            CharacterActionConfig action = CreateAction(
                id: 100,
                stableId: "one_frame_action",
                durationFrames: 1);
            CharacterActionRunner runner = StartRunner(action, out CharacterActionRunnerOperationResult start);

            Assert.IsTrue(start.Accepted);
            Assert.AreEqual(0, runner.ActiveInstance.LocalFrame);
            Assert.AreEqual(CharacterActionInstanceState.Running, runner.ActiveInstance.State);
            AssertNoEvent(start.Events, CharacterActionRunnerEventKind.ActionFinished);

            CharacterActionRunnerOperationResult finish = runner.Tick();

            Assert.AreEqual(1, runner.ActiveInstance.LocalFrame);
            Assert.AreEqual(CharacterActionInstanceState.Finished, runner.ActiveInstance.State);
            AssertHasEvent(finish.Events, CharacterActionRunnerEventKind.ActionFinished);
        }

        [Test]
        public void Runner_DurationTwoKeepsLastLegalFrameRunningAndFinishesOnNextTick()
        {
            CharacterActionConfig action = CreateAction(
                id: 100,
                stableId: "two_frame_action",
                durationFrames: 2);
            CharacterActionRunner runner = StartRunner(action, out _);

            CharacterActionRunnerOperationResult lastFrame = runner.Tick();

            Assert.AreEqual(1, runner.ActiveInstance.LocalFrame);
            Assert.AreEqual(CharacterActionInstanceState.Running, runner.ActiveInstance.State);
            AssertNoEvent(lastFrame.Events, CharacterActionRunnerEventKind.ActionFinished);

            CharacterActionRunnerOperationResult finish = runner.Tick();

            Assert.AreEqual(2, runner.ActiveInstance.LocalFrame);
            Assert.AreEqual(CharacterActionInstanceState.Finished, runner.ActiveInstance.State);
            AssertHasEvent(finish.Events, CharacterActionRunnerEventKind.ActionFinished);
        }

        [Test]
        public void Runner_DirectStartWhileRunningIsRejectedAndKeepsCurrentInstance()
        {
            CharacterActionConfig light = CreateAction(
                id: 100,
                stableId: "light_attack",
                durationFrames: 5);
            CharacterActionConfig dodge = CreateAction(
                id: 200,
                stableId: "dodge",
                category: CharacterActionCategory.Dodge,
                durationFrames: 3);
            CharacterActionRunner runner = StartRunner(light, out _);

            CharacterActionRunnerOperationResult result = runner.Start(
                CharacterActionResolveResult.Success(CreatePlan(dodge)),
                CharacterActionRunnerActionDefinition.FromConfig(dodge));

            Assert.IsFalse(result.Accepted);
            Assert.AreEqual(CharacterActionDiagnosticCodes.CharacterCancelRejected, result.Diagnostics[0].Code);
            AssertHasEvent(result.Events, CharacterActionRunnerEventKind.CancelRejected);
            Assert.AreEqual("light_attack", runner.ActiveInstance.Plan.ActionId);
            Assert.AreEqual(1, runner.ActiveInstance.InstanceId);
            Assert.AreEqual(CharacterActionInstanceState.Running, runner.ActiveInstance.State);
        }

        [Test]
        public void Runner_EmitsPhaseChangedWhenLocalFrameEntersNewPhase()
        {
            CharacterActionConfig action = CreateAction(
                id: 100,
                stableId: "light_attack",
                durationFrames: 5,
                phases: new[]
                {
                    new CharacterActionPhase(CharacterActionPhaseKind.Startup, 0, 0),
                    new CharacterActionPhase(CharacterActionPhaseKind.Active, 1, 2),
                    new CharacterActionPhase(CharacterActionPhaseKind.Recovery, 3, 4),
                });
            CharacterActionRunner runner = StartRunner(action, out CharacterActionRunnerOperationResult start);

            CharacterActionRunnerOperationResult active = runner.Tick();
            CharacterActionRunnerOperationResult recovery = runner.Tick();
            recovery = runner.Tick();

            Assert.AreEqual(CharacterActionPhaseKind.Startup, start.Events[1].CurrentPhase);
            CharacterActionRunnerEvent activeChange = FindEvent(active.Events, CharacterActionRunnerEventKind.PhaseChanged);
            Assert.AreEqual(CharacterActionPhaseKind.Startup, activeChange.PreviousPhase);
            Assert.AreEqual(CharacterActionPhaseKind.Active, activeChange.CurrentPhase);
            CharacterActionRunnerEvent recoveryChange = FindEvent(recovery.Events, CharacterActionRunnerEventKind.PhaseChanged);
            Assert.AreEqual(CharacterActionPhaseKind.Active, recoveryChange.PreviousPhase);
            Assert.AreEqual(CharacterActionPhaseKind.Recovery, recoveryChange.CurrentPhase);
        }

        [Test]
        public void Runner_DispatchesAllTrackKindsAsNoEngineEvents()
        {
            CharacterActionConfig action = CreateAction(
                id: 100,
                stableId: "multi_track_action",
                durationFrames: 4,
                motionTrack: new MotionTrackConfig(false, new[]
                {
                    new MotionTrackEvent(1, CharacterActionTrackEventKind.SetMovementMode, CharacterMovementMode.Run, 1f, 0.5f, -1f, "motion.run"),
                }),
                combatTrack: new CombatTrackConfig("combat.light", new[]
                {
                    new CombatTrackEvent(1, CharacterActionTrackEventKind.StartCombatAction, "combat.light", "trace.light", "combat.start"),
                }),
                gameplayTrack: new GameplayTrackConfig(new[]
                {
                    new GameplayTrackEvent(1, CharacterActionTrackEventKind.SendGameplayRequest, "gameplay.hit", "ability.light", "gameplay.hit"),
                }),
                animationTrack: new AnimationTrackConfig(new[]
                {
                    new AnimationTrackEvent(1, CharacterActionTrackEventKind.CrossFadeAnimation, "anim.light", 0.08f, "anim.start"),
                }),
                presentationTrack: new PresentationTrackConfig(new[]
                {
                    new PresentationTrackEvent(1, CharacterActionTrackEventKind.SpawnVisualCue, "vfx.light", "resource.light", "vfx.light"),
                }),
                debugTrack: new DebugTrackConfig(new[]
                {
                    new DebugTrackEvent(1, CharacterActionTrackEventKind.EmitDebugMarker, "marker.light", "debug.marker"),
                }));
            CharacterActionRunner runner = StartRunner(action, out _);

            CharacterActionRunnerOperationResult result = runner.Tick();

            AssertTrack(result.Events, CharacterActionTrackKind.Motion, CharacterActionTrackEventKind.SetMovementMode, "motion.run");
            AssertTrack(result.Events, CharacterActionTrackKind.Combat, CharacterActionTrackEventKind.StartCombatAction, "combat.start");
            AssertTrack(result.Events, CharacterActionTrackKind.Gameplay, CharacterActionTrackEventKind.SendGameplayRequest, "gameplay.hit");
            AssertTrack(result.Events, CharacterActionTrackKind.Animation, CharacterActionTrackEventKind.CrossFadeAnimation, "anim.start");
            AssertTrack(result.Events, CharacterActionTrackKind.Presentation, CharacterActionTrackEventKind.SpawnVisualCue, "vfx.light");
            AssertTrack(result.Events, CharacterActionTrackKind.Debug, CharacterActionTrackEventKind.EmitDebugMarker, "debug.marker");
            CharacterActionRunnerEvent motion = FindTrack(result.Events, CharacterActionTrackKind.Motion);
            CharacterActionRunnerEvent combat = FindTrack(result.Events, CharacterActionTrackKind.Combat);
            CharacterActionRunnerEvent gameplay = FindTrack(result.Events, CharacterActionTrackKind.Gameplay);
            CharacterActionRunnerEvent animation = FindTrack(result.Events, CharacterActionTrackKind.Animation);
            CharacterActionRunnerEvent presentation = FindTrack(result.Events, CharacterActionTrackKind.Presentation);
            CharacterActionRunnerEvent debug = FindTrack(result.Events, CharacterActionTrackKind.Debug);
            Assert.AreEqual("combat.light", combat.TrackDispatch.CombatActionId);
            Assert.AreEqual("anim.light", animation.TrackDispatch.AnimationActionKey);
            StringAssert.Contains("motionMode=Run", motion.ToReplayLine());
            StringAssert.Contains("motionVector=1,0.5,-1", motion.ToReplayLine());
            StringAssert.Contains("combatActionId=combat.light", combat.ToReplayLine());
            StringAssert.Contains("traceProfileId=trace.light", combat.ToReplayLine());
            StringAssert.Contains("gameplayRequestId=gameplay.hit", gameplay.ToReplayLine());
            StringAssert.Contains("abilityStableId=ability.light", gameplay.ToReplayLine());
            StringAssert.Contains("animationActionKey=anim.light", animation.ToReplayLine());
            StringAssert.Contains("presentationCueId=vfx.light", presentation.ToReplayLine());
            StringAssert.Contains("resourceKey=resource.light", presentation.ToReplayLine());
            StringAssert.Contains("debugMarkerId=marker.light", debug.ToReplayLine());
        }

        [Test]
        public void Runner_CancelRequestAcceptedCancelsCurrentAndStartsResolvedTargetPlan()
        {
            CharacterActionConfig light = CreateAction(
                id: 100,
                stableId: "light_attack",
                durationFrames: 6,
                cancelRules: new[]
                {
                    new CharacterCancelRule(2, 4, targetActionId: 200, sourceKind: CharacterActionSourceKind.Command),
                });
            CharacterActionConfig dodge = CreateAction(
                id: 200,
                stableId: "dodge",
                category: CharacterActionCategory.Dodge,
                durationFrames: 3);
            CharacterActionRunner runner = StartRunner(light, out _);
            runner.Tick();
            runner.Tick();

            CharacterActionRunnerOperationResult result = runner.RequestCancel(CreateTransition(dodge, CharacterActionSourceKind.Command));

            Assert.IsTrue(result.Accepted);
            AssertHasEvent(result.Events, CharacterActionRunnerEventKind.ActionCancelled);
            AssertHasEvent(result.Events, CharacterActionRunnerEventKind.ActionStarted);
            Assert.AreEqual("dodge", runner.ActiveInstance.Plan.ActionId);
            Assert.AreEqual(2, runner.ActiveInstance.InstanceId);
            Assert.AreEqual(CharacterActionInstanceState.Running, runner.ActiveInstance.State);
        }

        [Test]
        public void Runner_DispatchesTrackEventOnLastLegalFrameBeforeFinish()
        {
            CharacterActionConfig action = CreateAction(
                id: 100,
                stableId: "last_frame_track",
                durationFrames: 2,
                debugTrack: new DebugTrackConfig(new[]
                {
                    new DebugTrackEvent(1, CharacterActionTrackEventKind.EmitDebugMarker, "marker.last", "debug.last"),
                }));
            CharacterActionRunner runner = StartRunner(action, out _);

            CharacterActionRunnerOperationResult lastFrame = runner.Tick();

            Assert.AreEqual(1, runner.ActiveInstance.LocalFrame);
            Assert.AreEqual(CharacterActionInstanceState.Running, runner.ActiveInstance.State);
            AssertTrack(lastFrame.Events, CharacterActionTrackKind.Debug, CharacterActionTrackEventKind.EmitDebugMarker, "debug.last");
            AssertNoEvent(lastFrame.Events, CharacterActionRunnerEventKind.ActionFinished);
        }

        [Test]
        public void Runner_LastFrameCancelAcceptedBeforeFinish()
        {
            CharacterActionConfig light = CreateAction(
                id: 100,
                stableId: "light_attack",
                durationFrames: 2,
                cancelRules: new[]
                {
                    new CharacterCancelRule(1, 1, targetActionId: 200, sourceKind: CharacterActionSourceKind.Command),
                });
            CharacterActionConfig dodge = CreateAction(
                id: 200,
                stableId: "dodge",
                category: CharacterActionCategory.Dodge,
                durationFrames: 3);
            CharacterActionRunner runner = StartRunner(light, out _);
            CharacterActionRunnerOperationResult lastFrame = runner.Tick();

            Assert.AreEqual(1, runner.ActiveInstance.LocalFrame);
            Assert.AreEqual(CharacterActionInstanceState.Running, runner.ActiveInstance.State);
            AssertNoEvent(lastFrame.Events, CharacterActionRunnerEventKind.ActionFinished);

            CharacterActionRunnerOperationResult result = runner.RequestCancel(CreateTransition(dodge, CharacterActionSourceKind.Command));

            Assert.IsTrue(result.Accepted);
            AssertHasEvent(result.Events, CharacterActionRunnerEventKind.ActionCancelled);
            AssertHasEvent(result.Events, CharacterActionRunnerEventKind.ActionStarted);
            Assert.AreEqual("dodge", runner.ActiveInstance.Plan.ActionId);
            Assert.AreEqual(CharacterActionInstanceState.Running, runner.ActiveInstance.State);
        }

        [Test]
        public void Runner_CombatAnchoredCancelUsesCombatTimelineWindow()
        {
            CharacterActionConfig light = CreateAction(
                id: 100,
                stableId: "combat_light_attack",
                durationFrames: 3,
                timelineAuthority: CharacterActionTimelineAuthority.CombatAnchored,
                cancelRules: new[]
                {
                    new CharacterCancelRule(1, 1, targetActionId: 200, sourceKind: CharacterActionSourceKind.Command),
                });
            CharacterActionConfig dodge = CreateAction(
                id: 200,
                stableId: "dodge",
                category: CharacterActionCategory.Dodge,
                durationFrames: 3);
            CombatActionTimeline timeline = CreateCombatTimeline(cancelTargetActionId: 200);
            var runner = new CharacterActionRunner();
            CharacterActionRunnerOperationResult start = runner.Start(
                CharacterActionResolveResult.Success(CreatePlan(light)),
                CharacterActionRunnerActionDefinition.FromConfig(light, timeline));
            Assert.IsTrue(start.Accepted);
            runner.Tick();

            CharacterActionRunnerOperationResult result = runner.RequestCancel(CreateTransition(dodge, CharacterActionSourceKind.Command));

            Assert.IsTrue(result.Accepted);
            AssertHasEvent(result.Events, CharacterActionRunnerEventKind.ActionCancelled);
            AssertHasEvent(result.Events, CharacterActionRunnerEventKind.ActionStarted);
            Assert.AreEqual("dodge", runner.ActiveInstance.Plan.ActionId);
        }

        [Test]
        public void Runner_CancelRequestRejectedKeepsCurrentInstanceRunning()
        {
            CharacterActionConfig light = CreateAction(
                id: 100,
                stableId: "light_attack",
                durationFrames: 6,
                cancelRules: new[]
                {
                    new CharacterCancelRule(3, 4, targetActionId: 200, sourceKind: CharacterActionSourceKind.Command),
                });
            CharacterActionConfig dodge = CreateAction(
                id: 200,
                stableId: "dodge",
                category: CharacterActionCategory.Dodge,
                durationFrames: 3);
            CharacterActionRunner runner = StartRunner(light, out _);
            runner.Tick();

            CharacterActionRunnerOperationResult result = runner.RequestCancel(CreateTransition(dodge, CharacterActionSourceKind.Command));

            Assert.IsFalse(result.Accepted);
            Assert.AreEqual(CharacterActionDiagnosticCodes.CharacterCancelRejected, result.Diagnostics[0].Code);
            AssertHasEvent(result.Events, CharacterActionRunnerEventKind.CancelRejected);
            Assert.AreEqual("light_attack", runner.ActiveInstance.Plan.ActionId);
            Assert.AreEqual(1, runner.ActiveInstance.InstanceId);
            Assert.AreEqual(CharacterActionInstanceState.Running, runner.ActiveInstance.State);
        }

        [Test]
        public void Runner_CancelRequestRejectedWithoutActiveInstanceEmitsReplayEvent()
        {
            CharacterActionConfig dodge = CreateAction(
                id: 200,
                stableId: "dodge",
                category: CharacterActionCategory.Dodge,
                durationFrames: 3);
            var runner = new CharacterActionRunner();

            CharacterActionRunnerOperationResult result = runner.RequestCancel(CreateTransition(dodge, CharacterActionSourceKind.Command));

            Assert.IsFalse(result.Accepted);
            Assert.AreEqual(CharacterActionDiagnosticCodes.CharacterCancelRejected, result.Diagnostics[0].Code);
            CharacterActionRunnerEvent rejected = FindEvent(result.Events, CharacterActionRunnerEventKind.CancelRejected);
            Assert.AreEqual(0, rejected.InstanceId);
            Assert.AreEqual(CharacterActionInstanceState.None, rejected.State);
            Assert.IsFalse(runner.HasActiveInstance);
        }

        [Test]
        public void Runner_InterruptRequestAcceptedInterruptsCurrentAndStartsResolvedReactionPlan()
        {
            CharacterActionConfig light = CreateAction(
                id: 100,
                stableId: "light_attack",
                durationFrames: 6,
                interruptRules: new[]
                {
                    new CharacterInterruptRule(CharacterActionSourceKind.PostureBreak, minimumPriority: 50, targetActionId: 400),
                });
            CharacterActionConfig reaction = CreateAction(
                id: 400,
                stableId: "posture_break_react",
                category: CharacterActionCategory.Reaction,
                durationFrames: 4);
            CharacterActionRunner runner = StartRunner(light, out _);
            runner.Tick();

            CharacterActionRunnerOperationResult result = runner.RequestInterrupt(
                CreateTransition(reaction, CharacterActionSourceKind.PostureBreak, priority: 100));

            Assert.IsTrue(result.Accepted);
            AssertHasEvent(result.Events, CharacterActionRunnerEventKind.ActionInterrupted);
            AssertHasEvent(result.Events, CharacterActionRunnerEventKind.ActionStarted);
            Assert.AreEqual("posture_break_react", runner.ActiveInstance.Plan.ActionId);
            Assert.AreEqual(CharacterActionCategory.Reaction, runner.ActiveInstance.Plan.Category);
            Assert.AreEqual(2, runner.ActiveInstance.InstanceId);
        }

        [Test]
        public void Runner_InterruptRequestRejectedKeepsCurrentInstanceRunning()
        {
            CharacterActionConfig light = CreateAction(
                id: 100,
                stableId: "light_attack",
                durationFrames: 6,
                interruptRules: new[]
                {
                    new CharacterInterruptRule(CharacterActionSourceKind.PostureBreak, minimumPriority: 50, targetActionId: 400),
                });
            CharacterActionConfig reaction = CreateAction(
                id: 400,
                stableId: "posture_break_react",
                category: CharacterActionCategory.Reaction,
                durationFrames: 4);
            CharacterActionRunner runner = StartRunner(light, out _);

            CharacterActionRunnerOperationResult result = runner.RequestInterrupt(
                CreateTransition(reaction, CharacterActionSourceKind.PostureBreak, priority: 10));

            Assert.IsFalse(result.Accepted);
            Assert.AreEqual(CharacterActionDiagnosticCodes.CharacterCancelRejected, result.Diagnostics[0].Code);
            AssertHasEvent(result.Events, CharacterActionRunnerEventKind.InterruptRejected);
            Assert.AreEqual("light_attack", runner.ActiveInstance.Plan.ActionId);
            Assert.AreEqual(1, runner.ActiveInstance.InstanceId);
            Assert.AreEqual(CharacterActionInstanceState.Running, runner.ActiveInstance.State);
        }

        [Test]
        public void Runner_DebugSnapshotReportsCurrentStateLastEventsAndRejectReason()
        {
            CharacterActionConfig light = CreateAction(
                id: 100,
                stableId: "light_attack",
                durationFrames: 5,
                debugTrack: new DebugTrackConfig(new[]
                {
                    new DebugTrackEvent(0, CharacterActionTrackEventKind.EmitDebugMarker, "light.start", "debug.start"),
                }));
            CharacterActionConfig dodge = CreateAction(
                id: 200,
                stableId: "dodge",
                category: CharacterActionCategory.Dodge,
                durationFrames: 3);
            CharacterActionRunner runner = StartRunner(light, out _);

            CharacterActionDebugSnapshot startSnapshot = runner.CreateDebugSnapshot();

            Assert.AreEqual(1, startSnapshot.ActiveActionInstanceId);
            Assert.AreEqual("light_attack", startSnapshot.ActionId);
            Assert.AreEqual(CharacterActionInstanceState.Running, startSnapshot.State);
            Assert.AreEqual(0, startSnapshot.LocalFrame);
            Assert.AreEqual(CharacterActionPhaseKind.Startup, startSnapshot.CurrentPhase);
            AssertContains(startSnapshot.FiredEventsThisFrame, "kind=ActionStarted");
            AssertContains(startSnapshot.FiredEventsThisFrame, "trackEvent=EmitDebugMarker");

            CharacterActionRunnerOperationResult reject = runner.RequestCancel(CreateTransition(dodge, CharacterActionSourceKind.Command));
            CharacterActionDebugSnapshot rejectedSnapshot = runner.CreateDebugSnapshot();

            Assert.IsFalse(reject.Accepted);
            Assert.AreEqual(CharacterActionDiagnosticCodes.CharacterCancelRejected, rejectedSnapshot.LastRejectReason);
            AssertContains(rejectedSnapshot.FiredEventsThisFrame, "kind=CancelRejected");
        }

        private static CharacterActionRunner StartRunner(
            CharacterActionConfig action,
            out CharacterActionRunnerOperationResult start)
        {
            var runner = new CharacterActionRunner();
            start = runner.Start(
                CharacterActionResolveResult.Success(CreatePlan(action)),
                CharacterActionRunnerActionDefinition.FromConfig(action));
            return runner;
        }

        private static CharacterActionTransitionRequest CreateTransition(
            CharacterActionConfig action,
            CharacterActionSourceKind sourceKind,
            int priority = 0)
        {
            return new CharacterActionTransitionRequest(
                CharacterActionResolveResult.Success(CreatePlan(action)),
                CharacterActionRunnerActionDefinition.FromConfig(action),
                sourceKind,
                priority,
                traceId: "trace." + action.StableId);
        }

        private static CharacterActionPlan CreatePlan(CharacterActionConfig action)
        {
            int durationFrames = action.DurationFrames.HasValue
                ? action.DurationFrames.Value
                : 0;
            return new CharacterActionPlan(
                planId: action.Id,
                actionId: action.StableId,
                category: action.Category,
                priority: action.Priority,
                durationFrames: durationFrames,
                phases: action.Phases,
                tracks: CharacterActionTrackPlan.FromConfig(action),
                traceId: "trace." + action.StableId);
        }

        private static CharacterActionConfig CreateAction(
            int id,
            string stableId,
            CharacterActionCategory category = CharacterActionCategory.BasicAttack,
            int durationFrames = 4,
            CharacterActionTimelineAuthority timelineAuthority = CharacterActionTimelineAuthority.CharacterAuthored,
            CharacterActionPhase[] phases = null,
            CharacterCancelRule[] cancelRules = null,
            CharacterInterruptRule[] interruptRules = null,
            MotionTrackConfig motionTrack = null,
            CombatTrackConfig combatTrack = null,
            GameplayTrackConfig gameplayTrack = null,
            AnimationTrackConfig animationTrack = null,
            PresentationTrackConfig presentationTrack = null,
            DebugTrackConfig debugTrack = null)
        {
            return new CharacterActionConfig(
                id,
                stableId,
                stableId,
                category,
                timelineAuthority,
                tags: null,
                priority: category == CharacterActionCategory.Reaction ? 100 : 10,
                durationFrames: durationFrames,
                requirements: null,
                phases: phases ?? CreateDefaultPhases(durationFrames),
                cancelRules: cancelRules,
                interruptRules: interruptRules,
                motionTrack: motionTrack,
                combatTrack: combatTrack,
                gameplayTrack: gameplayTrack,
                animationTrack: animationTrack,
                presentationTrack: presentationTrack,
                debugTrack: debugTrack);
        }

        private static CharacterActionPhase[] CreateDefaultPhases(int durationFrames)
        {
            if (durationFrames <= 0)
                return new CharacterActionPhase[0];
            if (durationFrames == 1)
                return new[] { new CharacterActionPhase(CharacterActionPhaseKind.Startup, 0, 0) };
            if (durationFrames == 2)
            {
                return new[]
                {
                    new CharacterActionPhase(CharacterActionPhaseKind.Startup, 0, 0),
                    new CharacterActionPhase(CharacterActionPhaseKind.Recovery, 1, 1),
                };
            }

            return new[]
            {
                new CharacterActionPhase(CharacterActionPhaseKind.Startup, 0, 0),
                new CharacterActionPhase(CharacterActionPhaseKind.Active, 1, durationFrames - 2),
                new CharacterActionPhase(CharacterActionPhaseKind.Recovery, durationFrames - 1, durationFrames - 1),
            };
        }

        private static CombatActionTimeline CreateCombatTimeline(int cancelTargetActionId)
        {
            return new CombatActionTimeline(
                actionId: 100,
                totalFrames: 3,
                startup: new CombatFrameRange(0, 0),
                active: new CombatFrameRange(1, 1),
                recovery: new CombatFrameRange(2, 2),
                windows: new[]
                {
                    new CombatActionWindow(
                        CombatActionWindowKind.Cancel,
                        new CombatFrameRange(1, 1),
                        cancelTargetActionId),
                },
                events: null);
        }

        private static void AssertHasEvent(CharacterActionRunnerEvent[] events, CharacterActionRunnerEventKind kind)
        {
            for (int i = 0; i < events.Length; i++)
            {
                if (events[i].Kind == kind)
                    return;
            }

            Assert.Fail("Expected runner event " + kind + ".");
        }

        private static void AssertNoEvent(CharacterActionRunnerEvent[] events, CharacterActionRunnerEventKind kind)
        {
            for (int i = 0; i < events.Length; i++)
            {
                if (events[i].Kind == kind)
                    Assert.Fail("Did not expect runner event " + kind + ".");
            }
        }

        private static CharacterActionRunnerEvent FindEvent(CharacterActionRunnerEvent[] events, CharacterActionRunnerEventKind kind)
        {
            for (int i = 0; i < events.Length; i++)
            {
                if (events[i].Kind == kind)
                    return events[i];
            }

            Assert.Fail("Expected runner event " + kind + ".");
            return default;
        }

        private static void AssertTrack(
            CharacterActionRunnerEvent[] events,
            CharacterActionTrackKind trackKind,
            CharacterActionTrackEventKind eventKind,
            string stableEventId)
        {
            CharacterActionRunnerEvent runnerEvent = FindTrack(events, trackKind);
            Assert.AreEqual(CharacterActionRunnerEventKind.TrackEventFired, runnerEvent.Kind);
            Assert.AreEqual(eventKind, runnerEvent.TrackDispatch.EventKind);
            Assert.AreEqual(stableEventId, runnerEvent.TrackDispatch.StableEventId);
        }

        private static CharacterActionRunnerEvent FindTrack(CharacterActionRunnerEvent[] events, CharacterActionTrackKind trackKind)
        {
            for (int i = 0; i < events.Length; i++)
            {
                if (events[i].Kind == CharacterActionRunnerEventKind.TrackEventFired
                    && events[i].TrackDispatch.TrackKind == trackKind)
                {
                    return events[i];
                }
            }

            Assert.Fail("Expected track dispatch " + trackKind + ".");
            return default;
        }

        private static void AssertContains(string[] values, string expectedPart)
        {
            for (int i = 0; i < values.Length; i++)
            {
                if (values[i].Contains(expectedPart))
                    return;
            }

            Assert.Fail("Expected value containing '" + expectedPart + "'.");
        }
    }
}
