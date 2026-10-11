using MxFramework.CharacterAction;
using MxFramework.Combat.Animation;
using MxFramework.Runtime;
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
            Assert.AreEqual(new CharacterActionId(100), runner.ActiveInstance.Plan.ActionId);
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
        public void Runner_RestoreContinuesFromNextFrameWithoutReplayingOldTrackEvents()
        {
            CharacterActionConfig action = CreateAction(
                id: 100,
                stableId: "rollback_action",
                durationFrames: 5,
                debugTrack: new DebugTrackConfig(new[]
                {
                    new DebugTrackEvent(
                        1,
                        CharacterActionTrackEventKind.EmitDebugMarker,
                        "marker.frame1",
                        CharacterTestBulkIds.Id790017),
                    new DebugTrackEvent(
                        3,
                        CharacterActionTrackEventKind.EmitDebugMarker,
                        "marker.frame3",
                        CharacterTestBulkIds.Id790018),
                }));
            CharacterActionRunner original = StartRunner(action, out _);
            CharacterActionRunnerOperationResult frame1 = original.Tick();
            CharacterActionRunnerOperationResult frame2 = original.Tick();

            AssertTrack(frame1.Events, CharacterActionTrackKind.Debug, CharacterActionTrackEventKind.EmitDebugMarker, CharacterTestBulkIds.Id790017);
            Assert.AreEqual(2, original.ActiveInstance.LocalFrame);
            AssertNoEvent(frame2.Events, CharacterActionRunnerEventKind.TrackEventFired);

            CharacterActionRunnerRestoreState restoreState = original.CreateRestoreState();
            var restored = new CharacterActionRunner();
            CharacterActionDiagnostic diagnostic;
            bool restoredOk = restored.TryRestore(
                restoreState,
                CreatePlan(action),
                CharacterActionRunnerActionDefinition.FromConfig(action),
                out diagnostic);

            Assert.IsTrue(restoredOk, diagnostic.Message);
            Assert.AreEqual(0, restored.DrainEvents().Length);
            Assert.AreEqual(2, restored.ActiveInstance.LocalFrame);

            CharacterActionRunnerOperationResult next = restored.Tick();

            Assert.AreEqual(3, restored.ActiveInstance.LocalFrame);
            AssertTrack(next.Events, CharacterActionTrackKind.Debug, CharacterActionTrackEventKind.EmitDebugMarker, CharacterTestBulkIds.Id790018);
            AssertNoTrackStableEvent(next.Events, CharacterTestBulkIds.Id790017);
        }

        [Test]
        public void Runner_StatefulRangeUsesInclusiveEnterActiveExitOrdering()
        {
            CharacterActionConfig action = CreateAction(
                id: 101,
                stableId: "range_action",
                durationFrames: 6,
                motionTrack: new MotionTrackConfig(false, new[]
                {
                    new MotionTrackEvent(
                        2,
                        CharacterActionTrackEventKind.LockMovement,
                        CharacterMovementMode.ControlLocked,
                        stableEventId: CharacterTestNumericIds.Field_range_lock,
                        endFrame: 4),
                }));
            CharacterActionRunner runner = StartRunner(action, out _);

            Assert.AreEqual(0, CountLifecycle(runner.Tick().Events, CharacterActionTrackLifecyclePhase.Active));
            CharacterActionRunnerOperationResult frame2 = runner.Tick();
            CharacterActionRunnerOperationResult frame3 = runner.Tick();
            CharacterActionRunnerOperationResult frame4 = runner.Tick();

            AssertLifecycleSequence(
                frame2.Events,
                CharacterActionTrackLifecyclePhase.Enter,
                CharacterActionTrackLifecyclePhase.Active);
            AssertLifecycleSequence(frame3.Events, CharacterActionTrackLifecyclePhase.Active);
            AssertLifecycleSequence(
                frame4.Events,
                CharacterActionTrackLifecyclePhase.Active,
                CharacterActionTrackLifecyclePhase.Exit);
            CharacterActionRunnerEvent naturalExit = FindLifecycle(frame4.Events, CharacterActionTrackLifecyclePhase.Exit);
            Assert.AreEqual(CharacterActionTrackLifecycleBoundary.AfterFrame, naturalExit.TrackLifecycle.Boundary);
            Assert.AreEqual(CharacterActionTrackExitReason.NaturalEnd, naturalExit.TrackLifecycle.ExitReason);
        }

        [Test]
        public void Runner_SingleFrameStatefulRangeEmitsEnterActiveExit()
        {
            CharacterActionConfig action = CreateAction(
                id: 102,
                stableId: "single_frame_range",
                durationFrames: 2,
                motionTrack: new MotionTrackConfig(false, new[]
                {
                    new MotionTrackEvent(
                        0,
                        CharacterActionTrackEventKind.SetControlScale,
                        stableEventId: CharacterTestNumericIds.Field_single_scale,
                        movementControlScale: 0f,
                        rotationControlScale: 1f,
                        endFrame: 0),
                }));

            StartRunner(action, out CharacterActionRunnerOperationResult start);

            AssertLifecycleSequence(
                start.Events,
                CharacterActionTrackLifecyclePhase.Enter,
                CharacterActionTrackLifecyclePhase.Active,
                CharacterActionTrackLifecyclePhase.Exit);
        }

        [Test]
        public void Runner_ForcedCancellationExitsEnteredRangeExactlyOnce()
        {
            CharacterActionConfig action = CreateAction(
                id: 103,
                stableId: "cancelled_range",
                durationFrames: 8,
                motionTrack: new MotionTrackConfig(false, new[]
                {
                    new MotionTrackEvent(
                        0,
                        CharacterActionTrackEventKind.LockMovement,
                        CharacterMovementMode.ControlLocked,
                        stableEventId: CharacterTestNumericIds.Field_cancel_lock,
                        endFrame: 6),
                }));
            CharacterActionRunner runner = StartRunner(action, out _);
            runner.Tick();

            CharacterActionRunnerOperationResult cancelled = runner.CancelActive();
            CharacterActionRunnerOperationResult afterCancel = runner.Tick();

            Assert.AreEqual(1, CountLifecycle(cancelled.Events, CharacterActionTrackLifecyclePhase.Exit));
            CharacterActionRunnerEvent exit = FindLifecycle(cancelled.Events, CharacterActionTrackLifecyclePhase.Exit);
            Assert.AreEqual(CharacterActionTrackLifecycleBoundary.BeforeFrame, exit.TrackLifecycle.Boundary);
            Assert.AreEqual(CharacterActionTrackExitReason.ActionCancelled, exit.TrackLifecycle.ExitReason);
            Assert.AreEqual(0, CountLifecycle(afterCancel.Events, CharacterActionTrackLifecyclePhase.Exit));
        }

        [Test]
        public void Runner_RestoreRebuildsActiveRangeForSubsequentForcedExit()
        {
            CharacterActionConfig action = CreateAction(
                id: 105,
                stableId: "restored_range",
                durationFrames: 8,
                motionTrack: new MotionTrackConfig(false, new[]
                {
                    new MotionTrackEvent(
                        1,
                        CharacterActionTrackEventKind.LockMovement,
                        CharacterMovementMode.ControlLocked,
                        stableEventId: CharacterTestNumericIds.Field_restore_lock,
                        endFrame: 6),
                }));
            CharacterActionRunner source = StartRunner(action, out _);
            source.Tick();
            source.Tick();
            CharacterActionRunnerRestoreState state = source.CreateRestoreState();
            var restored = new CharacterActionRunner();

            Assert.IsTrue(restored.TryRestore(
                state,
                CreatePlan(action),
                CharacterActionRunnerActionDefinition.FromConfig(action),
                out CharacterActionDiagnostic diagnostic), diagnostic.Message);

            CharacterActionRunnerOperationResult cancelled = restored.CancelActive();

            Assert.AreEqual(1, CountLifecycle(cancelled.Events, CharacterActionTrackLifecyclePhase.Exit));
            CharacterActionRunnerEvent exit =
                FindLifecycle(cancelled.Events, CharacterActionTrackLifecyclePhase.Exit);
            Assert.AreEqual(CharacterTestNumericIds.Field_restore_lock, exit.TrackLifecycle.StableEventId);
            Assert.AreEqual(CharacterActionTrackExitReason.ActionCancelled, exit.TrackLifecycle.ExitReason);
        }

        [Test]
        public void Runner_OverlappingStatefulActiveDispatchUsesStartFrameThenStableIdPriority()
        {
            CharacterActionConfig action = CreateAction(
                id: 106,
                stableId: "overlap_order",
                durationFrames: 6,
                motionTrack: new MotionTrackConfig(false, new[]
                {
                    new MotionTrackEvent(
                        2,
                        CharacterActionTrackEventKind.SetControlScale,
                        stableEventId: CharacterTestNumericIds.Field_z_upper,
                        movementControlScale: 0.25f,
                        endFrame: 4),
                    new MotionTrackEvent(
                        1,
                        CharacterActionTrackEventKind.SetControlScale,
                        stableEventId: CharacterTestNumericIds.Field_a_lower,
                        movementControlScale: 0.75f,
                        endFrame: 4),
                }));
            CharacterActionRunner runner = StartRunner(action, out _);
            runner.Tick();
            CharacterActionRunnerOperationResult frame2 = runner.Tick();

            AssertLifecycleActiveOrder(frame2.Events, CharacterTestNumericIds.Field_a_lower, CharacterTestNumericIds.Field_z_upper);
        }

        [Test]
        public void Runner_BufferedStatefulActiveDispatchDoesNotAllocateAcross4096Ticks()
        {
            CharacterActionConfig action = CreateAction(
                id: 104,
                stableId: "no_alloc_range",
                durationFrames: 5000,
                phases: new[] { new CharacterActionPhase(CharacterActionPhaseKind.Active, 0, 4999) },
                motionTrack: new MotionTrackConfig(false, new[]
                {
                    new MotionTrackEvent(
                        0,
                        CharacterActionTrackEventKind.LockMovement,
                        CharacterMovementMode.ControlLocked,
                        stableEventId: CharacterTestNumericIds.Field_no_alloc_lock,
                        endFrame: 4999),
                }));
            CharacterActionPlan plan = CreatePlan(action);
            var runner = new CharacterActionRunner();
            var buffer = new CharacterActionRunnerEventBuffer(8);
            buffer.EnsureCapacity(8);
            Assert.IsTrue(runner.Start(
                CharacterActionResolveResult.Success(plan),
                CharacterActionRunnerActionDefinition.FromConfig(action),
                buffer).Accepted);
            Assert.IsTrue(runner.Tick(buffer).Accepted);

            _ = System.GC.GetAllocatedBytesForCurrentThread();
            long before = System.GC.GetAllocatedBytesForCurrentThread();
            bool valid = true;
            for (int i = 0; i < 4096; i++)
            {
                CharacterActionRunnerBufferedOperationResult tick = runner.Tick(buffer);
                valid &= tick.Accepted
                    && buffer.Count == 1
                    && buffer[0].Kind == CharacterActionRunnerEventKind.TrackLifecycle
                    && buffer[0].TrackLifecycle.Phase == CharacterActionTrackLifecyclePhase.Active;
            }
            long allocatedBytes = System.GC.GetAllocatedBytesForCurrentThread() - before;

            Assert.IsTrue(valid);
            Assert.AreEqual(0L, allocatedBytes);
        }

        [Test]
        public void Runner_TickDoesNotAllocateForFramesWithoutEvents()
        {
            CharacterActionConfig action = CreateAction(
                id: 100,
                stableId: "steady_action",
                durationFrames: 32,
                phases: new[] { new CharacterActionPhase(CharacterActionPhaseKind.Active, 0, 31) });
            CharacterActionRunner runner = StartRunner(action, out _);
            CharacterActionRunnerOperationResult warmup = runner.Tick();
            Assert.AreEqual(0, warmup.Events.Length);

            _ = System.GC.GetAllocatedBytesForCurrentThread();
            long before = System.GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < 16; i++)
            {
                CharacterActionRunnerOperationResult tick = runner.Tick();
                Assert.AreEqual(0, tick.Events.Length);
            }

            long allocatedBytes = System.GC.GetAllocatedBytesForCurrentThread() - before;
            Assert.AreEqual(0L, allocatedBytes);
        }

        [Test]
        public void Runner_BufferedStartTickAndFinishDoNotAllocateAfterWarmup()
        {
            CharacterActionConfig action = CreateAction(
                id: 100,
                stableId: "buffered_action",
                durationFrames: 1,
                debugTrack: new DebugTrackConfig(new[]
                {
                    new DebugTrackEvent(
                        0,
                        CharacterActionTrackEventKind.EmitDebugMarker,
                        "buffered.start",
                        CharacterTestBulkIds.Id790015),
                }));
            CharacterActionPlan plan = CreatePlan(action);
            CharacterActionResolveResult resolve = CharacterActionResolveResult.Success(plan);
            CharacterActionRunnerActionDefinition definition =
                CharacterActionRunnerActionDefinition.FromConfig(action);
            var runner = new CharacterActionRunner();
            var buffer = new CharacterActionRunnerEventBuffer(8);
            buffer.EnsureCapacity(8);

            for (int i = 0; i < 8; i++)
                Assert.IsTrue(RunBufferedCycle(runner, resolve, definition, buffer));
            Assert.AreEqual(0, runner.DrainEvents().Length);

            _ = System.GC.GetAllocatedBytesForCurrentThread();
            long before = System.GC.GetAllocatedBytesForCurrentThread();
            bool accepted = true;
            for (int i = 0; i < 32; i++)
                accepted &= RunBufferedCycle(runner, resolve, definition, buffer);
            long allocatedBytes = System.GC.GetAllocatedBytesForCurrentThread() - before;

            Assert.IsTrue(accepted);
            Assert.AreEqual(0L, allocatedBytes);
            Assert.AreEqual(0, runner.DrainEvents().Length);
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
                    new DebugTrackEvent(1, CharacterActionTrackEventKind.EmitDebugMarker, "marker.track", CharacterTestNumericIds.Field_debug_track),
                }));
            CharacterActionPlan plan = CreatePlan(action);
            var runner = new CharacterActionRunner();
            var definition = new CharacterActionRunnerActionDefinition(
                action.ActionId,
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
            Assert.AreEqual(new CharacterActionId(100), runner.ActiveInstance.Plan.ActionId);
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
            Assert.AreEqual(new CharacterActionId(100), runner.ActiveInstance.Plan.ActionId);
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
                    new MotionTrackEvent(1, CharacterActionTrackEventKind.SetMovementMode, CharacterMovementMode.Run, 1f, 0.5f, -1f, CharacterTestNumericIds.Field_motion_run),
                }),
                combatTrack: new CombatTrackConfig(CharacterTestNumericIds.Field_combat_light_attack, new[]
                {
                    new CombatTrackEvent(1, CharacterActionTrackEventKind.StartCombatAction, CharacterTestNumericIds.Field_combat_light_attack, CharacterTestNumericIds.Field_trace_light, CharacterTestNumericIds.Field_combat_start),
                }),
                gameplayTrack: new GameplayTrackConfig(new[]
                {
                    new GameplayTrackEvent(1, CharacterActionTrackEventKind.SendGameplayRequest, CharacterTestNumericIds.Field_gameplay_hit, CharacterTestNumericIds.Field_ability_light, CharacterTestNumericIds.Field_gameplay_hit),
                }),
                animationTrack: new AnimationTrackConfig(new[]
                {
                    new AnimationTrackEvent(1, CharacterActionTrackEventKind.CrossFadeAnimation, "anim.light", 0.08f, CharacterTestNumericIds.Field_anim_start),
                }),
                presentationTrack: new PresentationTrackConfig(new[]
                {
                    new PresentationTrackEvent(1, CharacterActionTrackEventKind.SpawnVisualCue, CharacterTestNumericIds.Field_vfx_light, "resource.light", CharacterTestNumericIds.Field_vfx_light),
                }),
                debugTrack: new DebugTrackConfig(new[]
                {
                    new DebugTrackEvent(1, CharacterActionTrackEventKind.EmitDebugMarker, "marker.light", CharacterTestNumericIds.Field_debug_marker),
                }));
            CharacterActionRunner runner = StartRunner(action, out _);

            CharacterActionRunnerOperationResult result = runner.Tick();

            AssertTrack(result.Events, CharacterActionTrackKind.Motion, CharacterActionTrackEventKind.SetMovementMode, CharacterTestNumericIds.Field_motion_run);
            AssertTrack(result.Events, CharacterActionTrackKind.Combat, CharacterActionTrackEventKind.StartCombatAction, CharacterTestNumericIds.Field_combat_start);
            AssertTrack(result.Events, CharacterActionTrackKind.Gameplay, CharacterActionTrackEventKind.SendGameplayRequest, CharacterTestNumericIds.Field_gameplay_hit);
            AssertTrack(result.Events, CharacterActionTrackKind.Animation, CharacterActionTrackEventKind.CrossFadeAnimation, CharacterTestNumericIds.Field_anim_start);
            AssertTrack(result.Events, CharacterActionTrackKind.Presentation, CharacterActionTrackEventKind.SpawnVisualCue, CharacterTestNumericIds.Field_vfx_light);
            AssertTrack(result.Events, CharacterActionTrackKind.Debug, CharacterActionTrackEventKind.EmitDebugMarker, CharacterTestNumericIds.Field_debug_marker);
            CharacterActionRunnerEvent motion = FindTrack(result.Events, CharacterActionTrackKind.Motion);
            CharacterActionRunnerEvent combat = FindTrack(result.Events, CharacterActionTrackKind.Combat);
            CharacterActionRunnerEvent gameplay = FindTrack(result.Events, CharacterActionTrackKind.Gameplay);
            CharacterActionRunnerEvent animation = FindTrack(result.Events, CharacterActionTrackKind.Animation);
            CharacterActionRunnerEvent presentation = FindTrack(result.Events, CharacterActionTrackKind.Presentation);
            CharacterActionRunnerEvent debug = FindTrack(result.Events, CharacterActionTrackKind.Debug);
            Assert.AreEqual(CharacterTestNumericIds.Field_combat_light_attack, combat.TrackDispatch.CombatActionId);
            Assert.AreEqual("anim.light", animation.TrackDispatch.AnimationActionKey);
            StringAssert.Contains("motionMode=Run", motion.ToReplayLine());
            StringAssert.Contains("motionVector=1,0.5,-1", motion.ToReplayLine());
            StringAssert.Contains("combatActionId=" + CharacterTestNumericIds.Field_combat_light_attack, combat.ToReplayLine());
            StringAssert.Contains("traceProfileId=" + CharacterTestNumericIds.Field_trace_light, combat.ToReplayLine());
            StringAssert.Contains("gameplayRequestId=" + CharacterTestNumericIds.Field_gameplay_hit, gameplay.ToReplayLine());
            StringAssert.Contains("abilityStableId=" + CharacterTestNumericIds.Field_ability_light, gameplay.ToReplayLine());
            StringAssert.Contains("animationActionKey=anim.light", animation.ToReplayLine());
            StringAssert.Contains("transitionSeconds=0.08", animation.ToReplayLine());
            StringAssert.Contains("presentationCueId=" + CharacterTestNumericIds.Field_vfx_light, presentation.ToReplayLine());
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
                    new CharacterCancelRule(2, 4),
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
            Assert.AreEqual(new CharacterActionId(200), runner.ActiveInstance.Plan.ActionId);
            Assert.AreEqual(2, runner.ActiveInstance.InstanceId);
            Assert.AreEqual(CharacterActionInstanceState.Running, runner.ActiveInstance.State);
        }

        [Test]
        public void Runner_CancelActiveCancelsCurrentWithoutTargetPlan()
        {
            CharacterActionConfig action = CreateAction(
                id: 100,
                stableId: "channel",
                durationFrames: 8);
            CharacterActionRunner runner = StartRunner(action, out _);

            CharacterActionRunnerOperationResult result = runner.CancelActive("external_cancel", CharacterTestRuntimeTraceIds.GatewayCancelOnlyMovement);

            Assert.IsTrue(result.Accepted);
            Assert.AreEqual(CharacterActionInstanceState.Cancelled, runner.ActiveInstance.State);
            Assert.AreEqual("external_cancel", runner.ActiveInstance.FinishReason);
            AssertHasEvent(result.Events, CharacterActionRunnerEventKind.ActionCancelled);
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
                    new DebugTrackEvent(1, CharacterActionTrackEventKind.EmitDebugMarker, "marker.last", CharacterTestNumericIds.Field_debug_last),
                }));
            CharacterActionRunner runner = StartRunner(action, out _);

            CharacterActionRunnerOperationResult lastFrame = runner.Tick();

            Assert.AreEqual(1, runner.ActiveInstance.LocalFrame);
            Assert.AreEqual(CharacterActionInstanceState.Running, runner.ActiveInstance.State);
            AssertTrack(lastFrame.Events, CharacterActionTrackKind.Debug, CharacterActionTrackEventKind.EmitDebugMarker, CharacterTestNumericIds.Field_debug_last);
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
                    new CharacterCancelRule(1, 1),
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
            Assert.AreEqual(new CharacterActionId(200), runner.ActiveInstance.Plan.ActionId);
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
                    new CharacterCancelRule(1, 1),
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
            Assert.AreEqual(new CharacterActionId(200), runner.ActiveInstance.Plan.ActionId);
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
                    new CharacterCancelRule(3, 4),
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
            Assert.AreEqual(new CharacterActionId(100), runner.ActiveInstance.Plan.ActionId);
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
                    new CharacterInterruptRule(CharacterActionSourceKind.CohesionBreak, minimumPriority: 50, targetActionId: new CharacterActionId(400)),
                });
            CharacterActionConfig reaction = CreateAction(
                id: 400,
                stableId: "cohesion_break_react",
                category: CharacterActionCategory.Reaction,
                durationFrames: 4);
            CharacterActionRunner runner = StartRunner(light, out _);
            runner.Tick();

            CharacterActionRunnerOperationResult result = runner.RequestInterrupt(
                CreateTransition(reaction, CharacterActionSourceKind.CohesionBreak, priority: 100));

            Assert.IsTrue(result.Accepted);
            AssertHasEvent(result.Events, CharacterActionRunnerEventKind.ActionInterrupted);
            AssertHasEvent(result.Events, CharacterActionRunnerEventKind.ActionStarted);
            Assert.AreEqual(new CharacterActionId(400), runner.ActiveInstance.Plan.ActionId);
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
                    new CharacterInterruptRule(CharacterActionSourceKind.CohesionBreak, minimumPriority: 50, targetActionId: new CharacterActionId(400)),
                });
            CharacterActionConfig reaction = CreateAction(
                id: 400,
                stableId: "cohesion_break_react",
                category: CharacterActionCategory.Reaction,
                durationFrames: 4);
            CharacterActionRunner runner = StartRunner(light, out _);

            CharacterActionRunnerOperationResult result = runner.RequestInterrupt(
                CreateTransition(reaction, CharacterActionSourceKind.CohesionBreak, priority: 10));

            Assert.IsFalse(result.Accepted);
            Assert.AreEqual(CharacterActionDiagnosticCodes.CharacterCancelRejected, result.Diagnostics[0].Code);
            AssertHasEvent(result.Events, CharacterActionRunnerEventKind.InterruptRejected);
            Assert.AreEqual(new CharacterActionId(100), runner.ActiveInstance.Plan.ActionId);
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
                    new DebugTrackEvent(0, CharacterActionTrackEventKind.EmitDebugMarker, "light.start", CharacterTestNumericIds.Field_debug_start),
                }));
            CharacterActionConfig dodge = CreateAction(
                id: 200,
                stableId: "dodge",
                category: CharacterActionCategory.Dodge,
                durationFrames: 3);
            CharacterActionRunner runner = StartRunner(light, out _);

            CharacterActionDebugSnapshot startSnapshot = runner.CreateDebugSnapshot();

            Assert.AreEqual(1, startSnapshot.ActiveActionInstanceId);
            Assert.AreEqual(new CharacterActionId(100), startSnapshot.ActionId);
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

        [Test]
        public void ExecutionGateway_StartsFirstActionAndReturnsStarted()
        {
            CharacterActionConfig action = CreateAction(100, "light_attack");
            CharacterActionResolveResult resolve = CharacterActionResolveResult.Success(CreatePlan(action));
            var gateway = new CharacterActionExecutionGateway();
            var buffer = new CharacterActionRunnerEventBuffer(8);
            CharacterActionExecutionRequest request = CharacterActionExecutionRequest.StartSlot(
                resolve,
                CharacterActionRunnerActionDefinition.FromConfig(action),
                CharacterActionSourceKind.LocalInput,
                actionSlotId: 1,
                priority: action.Priority,
                traceId: resolve.TraceId);

            CharacterActionExecutionResult result = gateway.Execute(in request, buffer);

            Assert.IsTrue(result.Accepted);
            Assert.AreEqual(CharacterActionExecutionStatus.Started, result.Status);
            Assert.AreEqual(new CharacterActionId(100), result.ActiveInstance.Plan.ActionId);
            AssertHasEvent(result.Events, CharacterActionRunnerEventKind.ActionStarted);
        }

        [Test]
        public void ExecutionGateway_AllowedWindowTransitionsAtomically()
        {
            CharacterActionConfig light = CreateAction(
                100,
                "light_attack",
                durationFrames: 6,
                cancelRules: new[]
                {
                    new CharacterCancelRule(1, 3),
                });
            CharacterActionConfig dodge = CreateAction(
                200,
                "dodge",
                CharacterActionCategory.Dodge,
                durationFrames: 3);
            CharacterActionExecutionGateway gateway = StartGateway(light, out _);
            var buffer = new CharacterActionRunnerEventBuffer(8);
            gateway.Tick(buffer);
            CharacterActionExecutionRequest request = CharacterActionExecutionRequest.StartIntent(
                CharacterActionResolveResult.Success(CreatePlan(dodge)),
                CharacterActionRunnerActionDefinition.FromConfig(dodge),
                CharacterActionSourceKind.Command,
                dodge.Priority,
                CharacterTestRuntimeTraceIds.GatewayTransition);

            CharacterActionExecutionResult result = gateway.Execute(in request, buffer);

            Assert.IsTrue(result.Accepted);
            Assert.AreEqual(CharacterActionExecutionStatus.Transitioned, result.Status);
            Assert.AreEqual(new CharacterActionId(200), gateway.ActiveInstance.Plan.ActionId);
            AssertHasEvent(result.Events, CharacterActionRunnerEventKind.ActionCancelled);
            AssertHasEvent(result.Events, CharacterActionRunnerEventKind.ActionStarted);
        }

        [Test]
        public void ExecutionGateway_RejectedWindowPreservesActiveIdentityFrameAndState()
        {
            CharacterActionConfig light = CreateAction(
                100,
                "light_attack",
                durationFrames: 6,
                cancelRules: new[]
                {
                    new CharacterCancelRule(3, 4),
                });
            CharacterActionConfig dodge = CreateAction(
                200,
                "dodge",
                CharacterActionCategory.Dodge,
                durationFrames: 3);
            CharacterActionExecutionGateway gateway = StartGateway(light, out _);
            var buffer = new CharacterActionRunnerEventBuffer(8);
            gateway.Tick(buffer);
            long instanceId = gateway.ActiveInstance.InstanceId;
            int localFrame = gateway.ActiveInstance.LocalFrame;
            CharacterActionExecutionRequest request = CharacterActionExecutionRequest.StartIntent(
                CharacterActionResolveResult.Success(CreatePlan(dodge)),
                CharacterActionRunnerActionDefinition.FromConfig(dodge),
                CharacterActionSourceKind.Command,
                dodge.Priority,
                CharacterTestRuntimeTraceIds.GatewayReject);

            CharacterActionExecutionResult result = gateway.Execute(in request, buffer);

            Assert.IsFalse(result.Accepted);
            Assert.AreEqual(CharacterActionExecutionStatus.Rejected, result.Status);
            Assert.AreEqual(instanceId, gateway.ActiveInstance.InstanceId);
            Assert.AreEqual(new CharacterActionId(100), gateway.ActiveInstance.Plan.ActionId);
            Assert.AreEqual(localFrame, gateway.ActiveInstance.LocalFrame);
            Assert.AreEqual(CharacterActionInstanceState.Running, gateway.ActiveInstance.State);
            AssertHasEvent(result.Events, CharacterActionRunnerEventKind.CancelRejected);
        }

        [Test]
        public void ExecutionGateway_CancelOnlyMovement_EndsActionWithoutStartingReplacement()
        {
            CharacterActionConfig landing = CreateAction(
                398,
                "landing_light",
                CharacterActionCategory.Movement,
                durationFrames: 6,
                cancelRules: new[]
                {
                    new CharacterCancelRule(
                        1,
                        4,
                        CharacterActionRequestType.Movement,
                        CharacterCancelInputMask.Any),
                });
            CharacterActionExecutionGateway gateway = StartGateway(landing, out _);
            var buffer = new CharacterActionRunnerEventBuffer(8);
            gateway.Tick(buffer);
            CharacterActionExecutionRequest request = CharacterActionExecutionRequest.CancelOnly(
                CharacterActionRequestType.Movement,
                CharacterActionSourceKind.Command,
                traceId: CharacterTestRuntimeTraceIds.GatewayCancelOnlyMovement);

            CharacterActionExecutionResult result = gateway.Execute(in request, buffer);

            Assert.IsTrue(result.Accepted);
            Assert.AreEqual(CharacterActionExecutionStatus.Cancelled, result.Status);
            Assert.AreEqual(CharacterActionInstanceState.Cancelled, gateway.ActiveInstance.State);
            AssertHasEvent(result.Events, CharacterActionRunnerEventKind.ActionCancelled);
            Assert.AreEqual(0, CountEvents(result.Events, CharacterActionRunnerEventKind.ActionStarted));
        }

        [Test]
        public void ExecutionGateway_CancelOnlyMovement_RejectedWindowPreservesRunningAction()
        {
            CharacterActionConfig landing = CreateAction(
                398,
                "landing_light",
                CharacterActionCategory.Movement,
                durationFrames: 6,
                cancelRules: new[]
                {
                    new CharacterCancelRule(3, 4, CharacterActionRequestType.Movement),
                });
            CharacterActionExecutionGateway gateway = StartGateway(landing, out _);
            var buffer = new CharacterActionRunnerEventBuffer(8);
            gateway.Tick(buffer);
            CharacterActionExecutionRequest request = CharacterActionExecutionRequest.CancelOnly(
                CharacterActionRequestType.Movement,
                CharacterActionSourceKind.Command);

            CharacterActionExecutionResult result = gateway.Execute(in request, buffer);

            Assert.IsFalse(result.Accepted);
            Assert.AreEqual(CharacterActionExecutionStatus.Rejected, result.Status);
            Assert.AreEqual(CharacterActionInstanceState.Running, gateway.ActiveInstance.State);
            Assert.AreEqual(1, gateway.ActiveInstance.LocalFrame);
            AssertHasEvent(result.Events, CharacterActionRunnerEventKind.CancelRejected);
        }

        [Test]
        public void ExecutionGateway_ReactionUsesInterruptRuleAndTargetPriority()
        {
            CharacterActionConfig active = CreateAction(
                100,
                "light_attack",
                durationFrames: 6,
                interruptRules: new[]
                {
                    new CharacterInterruptRule(CharacterActionSourceKind.Hit, minimumPriority: 50, targetActionId: new CharacterActionId(400)),
                });
            CharacterActionConfig reaction = CreateAction(
                400,
                "hit_react",
                CharacterActionCategory.Reaction,
                durationFrames: 3);
            CharacterActionExecutionGateway gateway = StartGateway(active, out _);
            var buffer = new CharacterActionRunnerEventBuffer(8);
            CharacterActionExecutionRequest request = CharacterActionExecutionRequest.React(
                CharacterActionResolveResult.Success(CreatePlan(reaction)),
                CharacterActionRunnerActionDefinition.FromConfig(reaction),
                CharacterActionSourceKind.Hit,
                reaction.Priority,
                CharacterTestRuntimeTraceIds.GatewayHit);

            CharacterActionExecutionResult result = gateway.Execute(in request, buffer);

            Assert.IsTrue(result.Accepted);
            Assert.AreEqual(CharacterActionExecutionStatus.Interrupted, result.Status);
            Assert.AreEqual(new CharacterActionId(400), gateway.ActiveInstance.Plan.ActionId);
            AssertHasEvent(result.Events, CharacterActionRunnerEventKind.ActionInterrupted);
        }

        [Test]
        public void ExecutionGateway_ReactionWithoutRulesIsRejectedWithoutLosingAction()
        {
            CharacterActionConfig active = CreateAction(100, "light_attack", durationFrames: 6);
            CharacterActionConfig reaction = CreateAction(
                400,
                "hit_react",
                CharacterActionCategory.Reaction,
                durationFrames: 3);
            CharacterActionExecutionGateway gateway = StartGateway(active, out _);
            long instanceId = gateway.ActiveInstance.InstanceId;
            var buffer = new CharacterActionRunnerEventBuffer(8);
            CharacterActionExecutionRequest request = CharacterActionExecutionRequest.React(
                CharacterActionResolveResult.Success(CreatePlan(reaction)),
                CharacterActionRunnerActionDefinition.FromConfig(reaction),
                CharacterActionSourceKind.Hit,
                reaction.Priority,
                CharacterTestRuntimeTraceIds.GatewayHitReject);

            CharacterActionExecutionResult result = gateway.Execute(in request, buffer);

            Assert.IsFalse(result.Accepted);
            Assert.AreEqual(instanceId, gateway.ActiveInstance.InstanceId);
            Assert.AreEqual(new CharacterActionId(100), gateway.ActiveInstance.Plan.ActionId);
            Assert.AreEqual(CharacterActionInstanceState.Running, gateway.ActiveInstance.State);
            AssertHasEvent(result.Events, CharacterActionRunnerEventKind.InterruptRejected);
        }

        [Test]
        public void ExecutionGateway_LifecycleTerminationAndNoActiveNoOpAreStructured()
        {
            CharacterActionConfig active = CreateAction(100, "light_attack", durationFrames: 6);
            CharacterActionExecutionGateway gateway = StartGateway(active, out _);
            var buffer = new CharacterActionRunnerEventBuffer(8);
            CharacterActionExecutionRequest request =
                CharacterActionExecutionRequest.TerminateForLifecycle(
                    CharacterActionLifecycleReason.Death,
                    CharacterTestRuntimeTraceIds.GatewayDeath);

            CharacterActionExecutionResult terminated = gateway.Execute(in request, buffer);

            Assert.IsTrue(terminated.Accepted);
            Assert.AreEqual(CharacterActionExecutionStatus.Terminated, terminated.Status);
            Assert.AreEqual(CharacterActionInstanceState.Cancelled, terminated.ActiveInstance.State);
            AssertHasEvent(terminated.Events, CharacterActionRunnerEventKind.ActionCancelled);

            var emptyGateway = new CharacterActionExecutionGateway();
            CharacterActionExecutionResult noOp = emptyGateway.Execute(in request, buffer);
            Assert.IsTrue(noOp.Accepted);
            Assert.AreEqual(CharacterActionExecutionStatus.NoOp, noOp.Status);
            Assert.AreEqual(0, noOp.Events.Count);
        }

        [TestCase(CharacterActionLifecycleReason.Death)]
        [TestCase(CharacterActionLifecycleReason.ReviveReset)]
        [TestCase(CharacterActionLifecycleReason.EntityDespawn)]
        [TestCase(CharacterActionLifecycleReason.ConfigurationReload)]
        [TestCase(CharacterActionLifecycleReason.RuntimeShutdown)]
        [TestCase(CharacterActionLifecycleReason.PreviewSeek)]
        [TestCase(CharacterActionLifecycleReason.ReplayRebuild)]
        public void ExecutionGateway_EachLifecycleTerminationEmitsExactlyOneTerminalEvent(
            CharacterActionLifecycleReason reason)
        {
            CharacterActionConfig active = CreateAction(
                100,
                "light_attack",
                durationFrames: 6,
                motionTrack: new MotionTrackConfig(false, new[]
                {
                    new MotionTrackEvent(
                        0,
                        CharacterActionTrackEventKind.LockMovement,
                        CharacterMovementMode.ControlLocked,
                        stableEventId: CharacterTestNumericIds.Field_lifecycle_lock,
                        endFrame: 5),
                }));
            CharacterActionExecutionGateway gateway = StartGateway(active, out _);
            var buffer = new CharacterActionRunnerEventBuffer(8);
            CharacterActionExecutionRequest request =
                CharacterActionExecutionRequest.TerminateForLifecycle(reason, CharacterTestRuntimeTraceIds.GatewayLifecycleOnce);

            CharacterActionExecutionResult terminated = gateway.Execute(in request, buffer);

            Assert.IsTrue(terminated.Accepted);
            Assert.AreEqual(CharacterActionExecutionStatus.Terminated, terminated.Status);
            Assert.AreEqual(1, CountEvents(terminated.Events, CharacterActionRunnerEventKind.ActionCancelled));
            Assert.AreEqual(1, CountLifecycle(terminated.Events, CharacterActionTrackLifecyclePhase.Exit));
            Assert.AreEqual(
                ExpectedTrackExitReason(reason),
                FindLifecycle(terminated.Events, CharacterActionTrackLifecyclePhase.Exit)
                    .TrackLifecycle.ExitReason);

            CharacterActionExecutionResult repeated = gateway.Execute(in request, buffer);
            Assert.AreEqual(CharacterActionExecutionStatus.NoOp, repeated.Status);
            Assert.AreEqual(0, repeated.Events.Count);
        }

        [Test]
        public void ExecutionGateway_LifecycleReplacementValidatesTargetBeforeMutatingActiveAction()
        {
            CharacterActionConfig active = CreateAction(100, "light_attack", durationFrames: 6);
            CharacterActionConfig death = CreateAction(
                400,
                "death_react",
                CharacterActionCategory.Reaction,
                durationFrames: 3);
            CharacterActionExecutionGateway gateway = StartGateway(active, out _);
            var buffer = new CharacterActionRunnerEventBuffer(16);
            gateway.Tick(buffer);
            long instanceId = gateway.ActiveInstance.InstanceId;
            int localFrame = gateway.ActiveInstance.LocalFrame;
            CharacterActionExecutionRequest invalid = CharacterActionExecutionRequest.ReplaceForLifecycle(
                CharacterActionResolveResult.Rejected(
                    CharacterActionRejectReason.MissingActionConfig,
                    traceId: CharacterTestRuntimeTraceIds.GatewayDeathInvalid),
                CharacterActionRunnerActionDefinition.FromConfig(death),
                CharacterActionLifecycleReason.Death,
                CharacterTestRuntimeTraceIds.GatewayDeathInvalid);

            CharacterActionExecutionResult rejected = gateway.Execute(in invalid, buffer);

            Assert.IsFalse(rejected.Accepted);
            Assert.AreEqual(CharacterActionExecutionStatus.Rejected, rejected.Status);
            Assert.AreEqual(instanceId, gateway.ActiveInstance.InstanceId);
            Assert.AreEqual(new CharacterActionId(100), gateway.ActiveInstance.Plan.ActionId);
            Assert.AreEqual(localFrame, gateway.ActiveInstance.LocalFrame);
            Assert.AreEqual(CharacterActionInstanceState.Running, gateway.ActiveInstance.State);

            CharacterActionExecutionRequest valid = CharacterActionExecutionRequest.ReplaceForLifecycle(
                CharacterActionResolveResult.Success(CreatePlan(death)),
                CharacterActionRunnerActionDefinition.FromConfig(death),
                CharacterActionLifecycleReason.Death,
                CharacterTestRuntimeTraceIds.GatewayDeath);
            CharacterActionExecutionResult replaced = gateway.Execute(in valid, buffer);

            Assert.IsTrue(replaced.Accepted);
            Assert.AreEqual(CharacterActionExecutionStatus.LifecycleReplaced, replaced.Status);
            Assert.AreEqual(new CharacterActionId(400), gateway.ActiveInstance.Plan.ActionId);
            Assert.AreEqual(1, CountEvents(replaced.Events, CharacterActionRunnerEventKind.ActionCancelled));
            Assert.AreEqual(1, CountEvents(replaced.Events, CharacterActionRunnerEventKind.ActionStarted));
        }

        [Test]
        public void ExecutionGateway_OrchestrationReplacementBypassesOrdinaryCancelWindowAtomically()
        {
            CharacterActionConfig active = CreateAction(
                100,
                "light_attack",
                durationFrames: 12,
                cancelRules: new[]
                {
                    new CharacterCancelRule(8, 10),
                });
            CharacterActionConfig target = CreateAction(
                200,
                "timeline_followup",
                CharacterActionCategory.Skill,
                durationFrames: 4);
            CharacterActionExecutionGateway gateway = StartGateway(active, out _);
            var buffer = new CharacterActionRunnerEventBuffer(16);
            gateway.Tick(buffer);
            long activeInstanceId = gateway.ActiveInstance.InstanceId;
            CharacterActionExecutionRequest ordinary = CharacterActionExecutionRequest.StartIntent(
                CharacterActionResolveResult.Success(CreatePlan(target)),
                CharacterActionRunnerActionDefinition.FromConfig(target),
                CharacterActionSourceKind.RuntimeAiPlanner,
                target.Priority,
                CharacterTestRuntimeTraceIds.GatewayOrchestrationInvalid);

            CharacterActionExecutionResult ordinaryRejected = gateway.Execute(in ordinary, buffer);

            Assert.IsFalse(ordinaryRejected.Accepted);
            Assert.AreEqual(CharacterActionExecutionStatus.Rejected, ordinaryRejected.Status);
            Assert.AreEqual(activeInstanceId, gateway.ActiveInstance.InstanceId);
            AssertHasEvent(ordinaryRejected.Events, CharacterActionRunnerEventKind.CancelRejected);

            CharacterActionExecutionRequest orchestration = CharacterActionExecutionRequest.ReplaceForOrchestration(
                CharacterActionResolveResult.Success(CreatePlan(target)),
                CharacterActionRunnerActionDefinition.FromConfig(target),
                CharacterActionOrchestrationReason.BehaviorRecipeTimelineJump,
                CharacterTestRuntimeTraceIds.GatewayOrchestrationInvalid);
            CharacterActionExecutionResult replaced = gateway.Execute(in orchestration, buffer);

            Assert.IsTrue(replaced.Accepted);
            Assert.AreEqual(CharacterActionExecutionStatus.OrchestrationReplaced, replaced.Status);
            Assert.AreEqual(new CharacterActionId(200), gateway.ActiveInstance.Plan.ActionId);
            Assert.AreNotEqual(activeInstanceId, gateway.ActiveInstance.InstanceId);
            Assert.AreEqual(1, CountEvents(replaced.Events, CharacterActionRunnerEventKind.ActionCancelled));
            Assert.AreEqual(1, CountEvents(replaced.Events, CharacterActionRunnerEventKind.ActionStarted));
        }

        [Test]
        public void ExecutionGateway_InvalidOrchestrationTargetPreservesActiveAction()
        {
            CharacterActionConfig active = CreateAction(100, "light_attack", durationFrames: 12);
            CharacterActionConfig target = CreateAction(200, "timeline_followup", durationFrames: 4);
            CharacterActionExecutionGateway gateway = StartGateway(active, out _);
            var buffer = new CharacterActionRunnerEventBuffer(16);
            gateway.Tick(buffer);
            long activeInstanceId = gateway.ActiveInstance.InstanceId;
            int activeLocalFrame = gateway.ActiveInstance.LocalFrame;
            CharacterActionExecutionRequest invalid = CharacterActionExecutionRequest.ReplaceForOrchestration(
                CharacterActionResolveResult.Rejected(
                    CharacterActionRejectReason.MissingActionConfig,
                    traceId: CharacterTestRuntimeTraceIds.GatewayOrchestrationInvalid),
                CharacterActionRunnerActionDefinition.FromConfig(target),
                CharacterActionOrchestrationReason.BehaviorRecipeTimelineJump,
                CharacterTestRuntimeTraceIds.GatewayOrchestrationInvalid);

            CharacterActionExecutionResult rejected = gateway.Execute(in invalid, buffer);

            Assert.IsFalse(rejected.Accepted);
            Assert.AreEqual(CharacterActionExecutionStatus.Rejected, rejected.Status);
            Assert.AreEqual(activeInstanceId, gateway.ActiveInstance.InstanceId);
            Assert.AreEqual(activeLocalFrame, gateway.ActiveInstance.LocalFrame);
            Assert.AreEqual(new CharacterActionId(100), gateway.ActiveInstance.Plan.ActionId);
            Assert.AreEqual(CharacterActionInstanceState.Running, gateway.ActiveInstance.State);
            Assert.AreEqual(0, CountEvents(rejected.Events, CharacterActionRunnerEventKind.ActionCancelled));
            Assert.AreEqual(0, CountEvents(rejected.Events, CharacterActionRunnerEventKind.ActionStarted));
        }

        [Test]
        public void ExecutionGateway_OrchestrationTerminationReleasesActionExactlyOnce()
        {
            CharacterActionConfig active = CreateAction(100, "light_attack", durationFrames: 12);
            CharacterActionExecutionGateway gateway = StartGateway(active, out _);
            var buffer = new CharacterActionRunnerEventBuffer(8);
            CharacterActionExecutionRequest request = CharacterActionExecutionRequest.TerminateForOrchestration(
                CharacterActionOrchestrationReason.BehaviorRecipeReturnToPlanner,
                CharacterTestRuntimeTraceIds.GatewayOrchestrationInvalid);

            CharacterActionExecutionResult terminated = gateway.Execute(in request, buffer);

            Assert.IsTrue(terminated.Accepted);
            Assert.AreEqual(CharacterActionExecutionStatus.OrchestrationTerminated, terminated.Status);
            Assert.AreEqual(CharacterActionInstanceState.Cancelled, terminated.ActiveInstance.State);
            Assert.AreEqual(1, CountEvents(terminated.Events, CharacterActionRunnerEventKind.ActionCancelled));

            CharacterActionExecutionResult repeated = gateway.Execute(in request, buffer);
            Assert.IsTrue(repeated.Accepted);
            Assert.AreEqual(CharacterActionExecutionStatus.NoOp, repeated.Status);
            Assert.AreEqual(0, repeated.Events.Count);
        }

        [Test]
        public void ExecutionGateway_ExternalCompletionChecksExpectedActionAndEmitsOneFinish()
        {
            CharacterActionConfig jump = CreateAction(300, "jump", durationFrames: 20);
            CharacterActionExecutionGateway gateway = StartGateway(jump, out _);
            var buffer = new CharacterActionRunnerEventBuffer(16);
            long instanceId = gateway.ActiveInstance.InstanceId;
            CharacterActionExecutionRequest mismatch = CharacterActionExecutionRequest.CompleteExternal(
                CharacterTestActionIds.Action_other,
                CharacterActionExternalCompletionReason.Landed,
                CharacterTestRuntimeTraceIds.GatewayLandedMismatch);

            CharacterActionExecutionResult rejected = gateway.Execute(in mismatch, buffer);

            Assert.IsFalse(rejected.Accepted);
            Assert.AreEqual(instanceId, gateway.ActiveInstance.InstanceId);
            Assert.AreEqual(CharacterActionInstanceState.Running, gateway.ActiveInstance.State);

            CharacterActionExecutionRequest landed = CharacterActionExecutionRequest.CompleteExternal(
                new CharacterActionId(300),
                CharacterActionExternalCompletionReason.Landed,
                CharacterTestRuntimeTraceIds.GatewayLanded);
            CharacterActionExecutionResult completed = gateway.Execute(in landed, buffer);

            Assert.IsTrue(completed.Accepted);
            Assert.AreEqual(CharacterActionExecutionStatus.Completed, completed.Status);
            Assert.AreEqual(CharacterActionInstanceState.Finished, completed.ActiveInstance.State);
            Assert.AreEqual(1, CountEvents(completed.Events, CharacterActionRunnerEventKind.ActionFinished));

            CharacterActionExecutionResult repeated = gateway.Execute(in landed, buffer);
            Assert.AreEqual(CharacterActionExecutionStatus.NoOp, repeated.Status);
            Assert.AreEqual(0, repeated.Events.Count);
        }

        [Test]
        public void ExecutionGateway_CaptureAndRestorePreserveIdentityFrameAndNextInstance()
        {
            CharacterActionConfig action = CreateAction(100, "light_attack", durationFrames: 6);
            CharacterActionExecutionGateway source = StartGateway(action, out _);
            var buffer = new CharacterActionRunnerEventBuffer(16);
            source.Tick(buffer);
            source.Tick(buffer);
            CharacterActionExecutionState state = source.CaptureState();
            var restored = new CharacterActionExecutionGateway();

            bool accepted = restored.TryRestore(
                state,
                CreatePlan(action),
                CharacterActionRunnerActionDefinition.FromConfig(action),
                out CharacterActionDiagnostic diagnostic);

            Assert.IsTrue(accepted, diagnostic.Message);
            Assert.AreEqual(state.ActiveActionInstanceId, restored.ActiveInstance.InstanceId);
            Assert.AreEqual(state.ActionId, restored.ActiveInstance.Plan.ActionId);
            Assert.AreEqual(state.LocalFrame, restored.ActiveInstance.LocalFrame);
            Assert.AreEqual(state.State, restored.ActiveInstance.State);
            Assert.AreEqual(state.NextInstanceId, restored.CaptureState().NextInstanceId);
            var restoredBuffer = new CharacterActionRunnerEventBuffer(16);
            for (int frame = state.LocalFrame + 1; frame <= action.DurationFrames + 1; frame++)
            {
                source.Tick(buffer);
                restored.Tick(restoredBuffer);
                CharacterActionExecutionState expected = source.CaptureState();
                CharacterActionExecutionState actual = restored.CaptureState();
                string at = "first divergence frame=" + frame + " field=action.";
                Assert.AreEqual(expected.ActiveActionInstanceId, actual.ActiveActionInstanceId, at + "instanceId");
                Assert.AreEqual(expected.NextInstanceId, actual.NextInstanceId, at + "nextInstanceId");
                Assert.AreEqual(expected.LocalFrame, actual.LocalFrame, at + "localFrame");
                Assert.AreEqual(expected.State, actual.State, at + "state");
                Assert.AreEqual(expected.CurrentPhase, actual.CurrentPhase, at + "phase");
                Assert.AreEqual(expected.TraceId, actual.TraceId, at + "traceId");
                Assert.AreEqual(expected.FinishReason, actual.FinishReason, at + "finishReason");
                Assert.AreEqual(buffer.Count, restoredBuffer.Count, at + "eventCount");
                for (int i = 0; i < buffer.Count; i++)
                    Assert.AreEqual(buffer[i].ToReplayLine(), restoredBuffer[i].ToReplayLine(), at + "event[" + i + "]");
            }
        }

        [Test]
        public void ExecutionGateway_OrdinaryWindowRejectionDoesNotAllocateAfterWarmup()
        {
            CharacterActionConfig light = CreateAction(
                100,
                "light_attack",
                durationFrames: 64,
                cancelRules: new[]
                {
                    new CharacterCancelRule(60, 63),
                });
            CharacterActionConfig dodge = CreateAction(
                200,
                "dodge",
                CharacterActionCategory.Dodge,
                durationFrames: 3);
            CharacterActionExecutionGateway gateway = StartGateway(light, out _);
            var buffer = new CharacterActionRunnerEventBuffer(8);
            CharacterActionExecutionRequest request = CharacterActionExecutionRequest.StartIntent(
                CharacterActionResolveResult.Success(CreatePlan(dodge)),
                CharacterActionRunnerActionDefinition.FromConfig(dodge),
                CharacterActionSourceKind.Command,
                dodge.Priority,
                CharacterTestRuntimeTraceIds.GatewayNoAllocReject);
            for (int i = 0; i < 8; i++)
                Assert.IsFalse(gateway.Execute(in request, buffer).Accepted);

            _ = System.GC.GetAllocatedBytesForCurrentThread();
            long before = System.GC.GetAllocatedBytesForCurrentThread();
            bool allRejected = true;
            for (int i = 0; i < 32; i++)
                allRejected &= !gateway.Execute(in request, buffer).Accepted;
            long allocated = System.GC.GetAllocatedBytesForCurrentThread() - before;

            Assert.IsTrue(allRejected);
            Assert.AreEqual(0L, allocated);
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

        private static int CountEvents(
            CharacterActionRunnerEventBuffer events,
            CharacterActionRunnerEventKind kind)
        {
            int count = 0;
            for (int i = 0; events != null && i < events.Count; i++)
            {
                if (events[i].Kind == kind)
                    count++;
            }
            return count;
        }

        private static CharacterActionExecutionGateway StartGateway(
            CharacterActionConfig action,
            out CharacterActionExecutionResult start)
        {
            var gateway = new CharacterActionExecutionGateway();
            var buffer = new CharacterActionRunnerEventBuffer(16);
            CharacterActionResolveResult resolve = CharacterActionResolveResult.Success(CreatePlan(action));
            CharacterActionExecutionRequest request = CharacterActionExecutionRequest.StartIntent(
                resolve,
                CharacterActionRunnerActionDefinition.FromConfig(action),
                CharacterActionSourceKind.Command,
                action.Priority,
                resolve.TraceId);
            start = gateway.Execute(in request, buffer);
            return gateway;
        }

        private static bool RunBufferedCycle(
            CharacterActionRunner runner,
            CharacterActionResolveResult resolve,
            CharacterActionRunnerActionDefinition definition,
            CharacterActionRunnerEventBuffer buffer)
        {
            CharacterActionRunnerBufferedOperationResult start =
                runner.Start(resolve, definition, buffer);
            if (!start.Accepted
                || buffer.Count != 3
                || buffer[0].Kind != CharacterActionRunnerEventKind.ActionStarted
                || buffer[1].Kind != CharacterActionRunnerEventKind.PhaseChanged
                || buffer[2].Kind != CharacterActionRunnerEventKind.TrackEventFired)
            {
                return false;
            }

            CharacterActionRunnerBufferedOperationResult finish = runner.Tick(buffer);
            bool hasFinished = false;
            for (int i = 0; i < buffer.Count; i++)
                hasFinished |= buffer[i].Kind == CharacterActionRunnerEventKind.ActionFinished;
            if (!finish.Accepted || !hasFinished)
            {
                return false;
            }

            runner.ClearCompletedInstance();
            return true;
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
                traceId: new RuntimeTraceIdentity(620001, 0, action.ActionId.Value, 0));
        }

        private static CharacterActionPlan CreatePlan(CharacterActionConfig action)
        {
            int durationFrames = action.DurationFrames.HasValue
                ? action.DurationFrames.Value
                : 0;
            return new CharacterActionPlan(
                planId: action.ActionId.Value,
                actionId: action.ActionId,
                category: action.Category,
                priority: action.Priority,
                durationFrames: durationFrames,
                phases: action.Phases,
                tracks: CharacterActionTrackPlan.FromConfig(action),
                traceId: new RuntimeTraceIdentity(620001, 0, action.ActionId.Value, 0));
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
                new CharacterActionId(id),
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

        private static int CountLifecycle(
            CharacterActionRunnerEvent[] events,
            CharacterActionTrackLifecyclePhase phase)
        {
            int count = 0;
            for (int i = 0; events != null && i < events.Length; i++)
            {
                if (events[i].Kind == CharacterActionRunnerEventKind.TrackLifecycle
                    && events[i].TrackLifecycle.Phase == phase)
                {
                    count++;
                }
            }

            return count;
        }

        private static CharacterActionRunnerEvent FindLifecycle(
            CharacterActionRunnerEvent[] events,
            CharacterActionTrackLifecyclePhase phase)
        {
            for (int i = 0; events != null && i < events.Length; i++)
            {
                if (events[i].Kind == CharacterActionRunnerEventKind.TrackLifecycle
                    && events[i].TrackLifecycle.Phase == phase)
                {
                    return events[i];
                }
            }

            Assert.Fail("Expected lifecycle phase " + phase + ".");
            return default;
        }

        private static int CountLifecycle(
            CharacterActionRunnerEventBuffer events,
            CharacterActionTrackLifecyclePhase phase)
        {
            int count = 0;
            for (int i = 0; events != null && i < events.Count; i++)
            {
                if (events[i].Kind == CharacterActionRunnerEventKind.TrackLifecycle
                    && events[i].TrackLifecycle.Phase == phase)
                {
                    count++;
                }
            }

            return count;
        }

        private static CharacterActionRunnerEvent FindLifecycle(
            CharacterActionRunnerEventBuffer events,
            CharacterActionTrackLifecyclePhase phase)
        {
            for (int i = 0; events != null && i < events.Count; i++)
            {
                if (events[i].Kind == CharacterActionRunnerEventKind.TrackLifecycle
                    && events[i].TrackLifecycle.Phase == phase)
                {
                    return events[i];
                }
            }

            Assert.Fail("Expected buffered lifecycle phase " + phase + ".");
            return default;
        }

        private static void AssertLifecycleSequence(
            CharacterActionRunnerEvent[] events,
            params CharacterActionTrackLifecyclePhase[] expected)
        {
            int found = 0;
            for (int i = 0; events != null && i < events.Length; i++)
            {
                if (events[i].Kind != CharacterActionRunnerEventKind.TrackLifecycle)
                    continue;
                Assert.Less(found, expected.Length, "Unexpected extra lifecycle event.");
                Assert.AreEqual(expected[found], events[i].TrackLifecycle.Phase);
                found++;
            }

            Assert.AreEqual(expected.Length, found);
        }

        private static void AssertLifecycleActiveOrder(
            CharacterActionRunnerEvent[] events,
            params int[] expectedStableEventIds)
        {
            int found = 0;
            for (int i = 0; events != null && i < events.Length; i++)
            {
                if (events[i].Kind != CharacterActionRunnerEventKind.TrackLifecycle
                    || events[i].TrackLifecycle.Phase != CharacterActionTrackLifecyclePhase.Active)
                {
                    continue;
                }

                Assert.Less(found, expectedStableEventIds.Length, "Unexpected extra Active lifecycle event.");
                Assert.AreEqual(expectedStableEventIds[found], events[i].TrackLifecycle.StableEventId);
                found++;
            }

            Assert.AreEqual(expectedStableEventIds.Length, found);
        }

        private static CharacterActionTrackExitReason ExpectedTrackExitReason(
            CharacterActionLifecycleReason reason)
        {
            switch (reason)
            {
                case CharacterActionLifecycleReason.Death:
                    return CharacterActionTrackExitReason.Death;
                case CharacterActionLifecycleReason.ReviveReset:
                    return CharacterActionTrackExitReason.ReviveReset;
                case CharacterActionLifecycleReason.EntityDespawn:
                    return CharacterActionTrackExitReason.EntityDespawn;
                case CharacterActionLifecycleReason.ConfigurationReload:
                    return CharacterActionTrackExitReason.ConfigurationReload;
                case CharacterActionLifecycleReason.RuntimeShutdown:
                    return CharacterActionTrackExitReason.RuntimeShutdown;
                case CharacterActionLifecycleReason.PreviewSeek:
                    return CharacterActionTrackExitReason.PreviewSeek;
                case CharacterActionLifecycleReason.ReplayRebuild:
                    return CharacterActionTrackExitReason.ReplayRebuild;
                default:
                    Assert.Fail("Unsupported lifecycle reason " + reason + ".");
                    return CharacterActionTrackExitReason.None;
            }
        }

        private static void AssertHasEvent(CharacterActionRunnerEventBuffer events, CharacterActionRunnerEventKind kind)
        {
            for (int i = 0; events != null && i < events.Count; i++)
            {
                if (events[i].Kind == kind)
                    return;
            }

            Assert.Fail("Expected buffered runner event " + kind + ".");
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
            int stableEventId)
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

        private static void AssertNoTrackStableEvent(
            CharacterActionRunnerEvent[] events,
            int stableEventId)
        {
            for (int i = 0; i < events.Length; i++)
            {
                if (events[i].Kind == CharacterActionRunnerEventKind.TrackEventFired
                    && events[i].TrackDispatch.StableEventId == stableEventId)
                {
                    Assert.Fail("Did not expect track event " + stableEventId + ".");
                }
            }
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
