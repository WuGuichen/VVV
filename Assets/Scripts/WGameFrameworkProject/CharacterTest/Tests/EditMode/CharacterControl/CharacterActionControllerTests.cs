using System;
using System.Collections.Generic;
using System.Globalization;
using MxFramework.CharacterAction;
using MxFramework.CharacterControl;
using MxFramework.Combat.Animation;
using MxFramework.Combat.Core;
using MxFramework.Combat.Damage;
using MxFramework.Combat.Hit;
using MxFramework.Core.Math;
using MxFramework.Gameplay;
using MxFramework.Runtime;
using NUnit.Framework;

namespace MxFramework.Tests.CharacterControl
{
    public sealed class CharacterActionControllerTests
    {
        private const int QuickAttackId = 1001;
        private const int HeavyAttackId = 2002;
        private const int AbilityStrike = 300001;

        [Test]
        public void CharacterActionLifecycle_SynchronizesControlStateAndEvents()
        {
            var machine = new CharacterControlStateMachine(CreateEntity());
            CharacterActionConfig action = CreateCharacterAction(
                id: 100,
                stableId: "character_control.quick_attack",
                intentId: CharacterTestNumericIds.IntentLightAttack,
                combatActionId: QuickAttackId);
            var controller = new CharacterActionController(
                machine,
                characterActionOptions: CreateCharacterActionOptions(action, CharacterTestNumericIds.IntentLightAttack));
            var events = new List<CharacterActionEvent>();
            var stateEvents = new List<CharacterStateChangedEvent>();
            controller.ActionEvent += events.Add;
            machine.StateChanged += stateEvents.Add;
            CharacterActionRequest request = CharacterActionRequest.CombatAction(
                RuntimeFrame.Zero,
                CreateEntity(),
                CharacterActionKind.Attack,
                QuickAttackId,
                sourceId: 4,
                traceId: CharacterTestRuntimeTraceIds.Attack,
                actionIntentId: CharacterTestNumericIds.IntentLightAttack);

            CharacterActionResult result = controller.Submit(request);

            Assert.IsTrue(result.Success);
            Assert.AreEqual(CharacterControlState.Action, machine.CurrentState);
            Assert.AreEqual(CharacterActionEventType.Accepted, events[0].Type);
            Assert.AreEqual(CharacterActionEventType.Started, events[1].Type);

            controller.TickCharacterAction(new RuntimeFrame(1));
            controller.TickCharacterAction(new RuntimeFrame(2));
            controller.TickCharacterAction(new RuntimeFrame(3));

            Assert.AreEqual(CharacterControlState.Locomotion, machine.CurrentState);
            Assert.AreEqual(CharacterActionEventType.Finished, events[events.Count - 1].Type);
            Assert.AreEqual(new RuntimeFrame(3), stateEvents[stateEvents.Count - 1].Frame);
        }

        [Test]
        public void SameFrameDuplicateAction_IsRejectedBeforeGateway()
        {
            var machine = new CharacterControlStateMachine(CreateEntity());
            CharacterActionConfig action = CreateCharacterAction(
                id: 100,
                stableId: "character_control.quick_attack",
                intentId: CharacterTestNumericIds.IntentLightAttack,
                combatActionId: QuickAttackId);
            var controller = new CharacterActionController(
                machine,
                characterActionOptions: CreateCharacterActionOptions(action, CharacterTestNumericIds.IntentLightAttack));
            CharacterActionRequest request = CharacterActionRequest.CombatAction(
                RuntimeFrame.Zero,
                CreateEntity(),
                CharacterActionKind.Attack,
                QuickAttackId,
                sourceId: 4,
                traceId: CharacterTestRuntimeTraceIds.Attack,
                actionIntentId: CharacterTestNumericIds.IntentLightAttack);

            Assert.IsTrue(controller.Submit(request).Success);
            CharacterActionResult duplicate = controller.Submit(request);

            Assert.IsFalse(duplicate.Success);
            Assert.AreEqual(CharacterActionRejectedReason.DuplicateSameFrame, duplicate.RejectedReason);
        }

        [Test]
        public void CharacterActionRuntime_StartsCombatActionFromConfiguredTrack()
        {
            using CharacterActionCombatTrackBridge combatBridge = CreateCombatBridge();
            var machine = new CharacterControlStateMachine(CreateEntity());
            CharacterActionConfig action = CreateCharacterAction(
                id: 100,
                stableId: "character_control.quick_attack",
                intentId: CharacterTestNumericIds.IntentLightAttack,
                combatActionId: QuickAttackId);
            var controller = new CharacterActionController(
                machine,
                characterActionOptions: CreateCharacterActionOptions(
                    action,
                    CharacterTestNumericIds.IntentLightAttack,
                    new CharacterActionTrackAdapter(combatSink: combatBridge),
                    combatBridge));
            var events = new List<CharacterActionEvent>();
            controller.ActionEvent += events.Add;
            CharacterActionRequest request = CharacterActionRequest.CombatAction(
                RuntimeFrame.Zero,
                CreateEntity(),
                CharacterActionKind.Attack,
                QuickAttackId,
                sourceId: 4,
                traceId: CharacterTestRuntimeTraceIds.Attack,
                actionIntentId: CharacterTestNumericIds.IntentLightAttack);

            CharacterActionResult result = controller.Submit(request);

            Assert.IsTrue(result.Success);
            Assert.IsTrue(controller.UsesCharacterActionRuntime);
            Assert.AreEqual(CharacterControlState.Action, machine.CurrentState);
            Assert.AreEqual(CombatActionPhase.Startup, combatBridge.GetCurrentPhase(CreateEntity().CombatEntityId));
            Assert.AreEqual(CharacterActionEventType.Accepted, events[0].Type);
            Assert.AreEqual(CharacterActionEventType.Started, events[1].Type);

            controller.TickCharacterAction(new RuntimeFrame(1));
            controller.TickCharacterAction(new RuntimeFrame(2));
            controller.TickCharacterAction(new RuntimeFrame(3));

            Assert.AreEqual(CharacterControlState.Locomotion, machine.CurrentState);
            Assert.AreEqual(CombatActionPhase.None, combatBridge.GetCurrentPhase(CreateEntity().CombatEntityId));
            Assert.IsTrue(events.Exists(evt => evt.Type == CharacterActionEventType.Finished));
        }

        [Test]
        public void CharacterActionRuntime_StartsCombatActionAtTrackDispatchFrame()
        {
            using CharacterActionCombatTrackBridge combatBridge = CreateCombatBridge();
            var machine = new CharacterControlStateMachine(CreateEntity());
            CharacterActionConfig action = CreateCharacterAction(
                id: 100,
                stableId: "character_control.delayed_attack",
                intentId: CharacterTestNumericIds.IntentDelayedAttack,
                combatActionId: QuickAttackId,
                combatTrackFrame: 1);
            var controller = new CharacterActionController(
                machine,
                characterActionOptions: CreateCharacterActionOptions(
                    action,
                    CharacterTestNumericIds.IntentDelayedAttack,
                    new CharacterActionTrackAdapter(combatSink: combatBridge),
                    combatBridge));
            CharacterActionRequest request = CharacterActionRequest.CombatAction(
                RuntimeFrame.Zero,
                CreateEntity(),
                CharacterActionKind.Attack,
                QuickAttackId,
                sourceId: 4,
                traceId: CharacterTestRuntimeTraceIds.Delayed,
                actionIntentId: CharacterTestNumericIds.IntentDelayedAttack);

            CharacterActionResult result = controller.Submit(request);

            Assert.IsTrue(result.Success, result.Message);
            Assert.AreEqual(CombatActionPhase.None, combatBridge.GetCurrentPhase(CreateEntity().CombatEntityId));

            controller.TickCharacterAction(new RuntimeFrame(1));

            Assert.AreEqual(CombatActionPhase.Startup, combatBridge.GetCurrentPhase(CreateEntity().CombatEntityId));
        }

        [Test]
        public void CharacterActionRuntime_ForwardsConfiguredTracksToAdapters()
        {
            var machine = new CharacterControlStateMachine(CreateEntity());
            var runtimeCollector = new CharacterActionAdapterRequestCollector();
            var presentationCollector = new CharacterActionPresentationRequestCollector();
            CharacterActionConfig action = CreateAdapterTrackAction();
            var controller = new CharacterActionController(
                machine,
                characterActionOptions: CreateCharacterActionOptions(
                    new[] { action },
                    new[] { CharacterTestNumericIds.IntentAdapterAttack },
                    new CharacterActionTrackAdapter(
                        motionSink: runtimeCollector,
                        combatSink: runtimeCollector,
                        gameplaySink: runtimeCollector),
                    new CharacterActionPresentationTrackAdapter(
                        animationSink: presentationCollector,
                        vfxSink: presentationCollector),
                    presentationTargetActorId: "actor.hero",
                    animationLayerId: "upper"));
            CharacterActionRequest request = CharacterActionRequest.CombatAction(
                RuntimeFrame.Zero,
                CreateEntity(),
                CharacterActionKind.Attack,
                QuickAttackId,
                sourceId: 4,
                traceId: CharacterTestRuntimeTraceIds.Adapter,
                actionIntentId: CharacterTestNumericIds.IntentAdapterAttack);

            CharacterActionResult result = controller.Submit(request);

            Assert.IsTrue(result.Success, result.Message);
            Assert.AreEqual(1, runtimeCollector.MotionRequests.Count);
            Assert.AreEqual(CharacterActionTrackEventKind.ApplyImpulse, runtimeCollector.MotionRequests[0].EventKind);
            Assert.AreEqual(new RuntimeFrame(0), runtimeCollector.MotionRequests[0].Metadata.Frame);
            Assert.AreEqual(1, runtimeCollector.CombatRequests.Count);
            Assert.AreEqual(CharacterActionTrackEventKind.HitTraceWindow, runtimeCollector.CombatRequests[0].EventKind);
            Assert.AreEqual("mainHand", runtimeCollector.CombatRequests[0].EquipmentSlotId);
            Assert.AreEqual(1, runtimeCollector.GameplayRequests.Count);
            Assert.AreEqual(CharacterTestNumericIds.Assert_gameplay_hit, runtimeCollector.GameplayRequests[0].RequestId);
            Assert.AreEqual(1, presentationCollector.AnimationRequests.Count);
            Assert.AreEqual("actor.hero", presentationCollector.AnimationRequests[0].TargetActorId);
            Assert.AreEqual("upper", presentationCollector.AnimationRequests[0].LayerId);
            Assert.AreEqual(1, presentationCollector.VfxRequests.Count);
            Assert.AreEqual("vfx.light", presentationCollector.VfxRequests[0].ResourceKey);
        }

        [Test]
        public void CharacterActionRuntime_ForwardsDebugTrackToAdapter()
        {
            var machine = new CharacterControlStateMachine(CreateEntity());
            var runtimeCollector = new CharacterActionAdapterRequestCollector();
            CharacterActionConfig action = CreateDebugTrackAction();
            var controller = new CharacterActionController(
                machine,
                characterActionOptions: CreateCharacterActionOptions(
                    new[] { action },
                    new[] { CharacterTestNumericIds.IntentDebugAttack },
                    new CharacterActionTrackAdapter(debugSink: runtimeCollector)));
            CharacterActionRequest request = CharacterActionRequest.CombatAction(
                RuntimeFrame.Zero,
                CreateEntity(),
                CharacterActionKind.Attack,
                QuickAttackId,
                sourceId: 4,
                traceId: CharacterTestRuntimeTraceIds.Debug,
                actionIntentId: CharacterTestNumericIds.IntentDebugAttack);

            CharacterActionResult result = controller.Submit(request);

            Assert.IsTrue(result.Success, result.Message);
            Assert.AreEqual(1, runtimeCollector.DebugRequests.Count);
            Assert.AreEqual("debug.light.start", runtimeCollector.DebugRequests[0].MarkerId);
            Assert.AreEqual(CharacterTestNumericIds.Assert_debug_start, runtimeCollector.DebugRequests[0].Metadata.StableEventId);
            Assert.AreEqual(new RuntimeFrame(0), runtimeCollector.DebugRequests[0].Metadata.Frame);
        }

        [Test]
        public void CharacterActionRuntime_SkipsDebugTrackWhenDebugSinkMissing()
        {
            var machine = new CharacterControlStateMachine(CreateEntity());
            var runtimeCollector = new CharacterActionAdapterRequestCollector();
            CharacterActionConfig action = CreateDebugTrackAction();
            var controller = new CharacterActionController(
                machine,
                characterActionOptions: CreateCharacterActionOptions(
                    new[] { action },
                    new[] { CharacterTestNumericIds.IntentDebugAttack },
                    new CharacterActionTrackAdapter(motionSink: runtimeCollector)));
            CharacterActionRequest request = CharacterActionRequest.CombatAction(
                RuntimeFrame.Zero,
                CreateEntity(),
                CharacterActionKind.Attack,
                QuickAttackId,
                sourceId: 4,
                traceId: CharacterTestRuntimeTraceIds.DebugNoSink,
                actionIntentId: CharacterTestNumericIds.IntentDebugAttack);

            CharacterActionResult result = controller.Submit(request);

            Assert.IsTrue(result.Success, result.Message);
            Assert.AreEqual(0, runtimeCollector.DebugRequests.Count);
        }

        [Test]
        public void CharacterActionRuntime_MotionTrackWritesCharacterControlRequestFrame()
        {
            var machine = new CharacterControlStateMachine(CreateEntity());
            var motionSink = new CharacterActionMotionRequestAccumulatorSink();
            CharacterActionConfig action = CreateMotionRequestFrameAction();
            var controller = new CharacterActionController(
                machine,
                characterActionOptions: CreateCharacterActionOptions(
                    new[] { action },
                    new[] { CharacterTestNumericIds.IntentMotionAttack },
                    new CharacterActionTrackAdapter(motionSink: motionSink)));
            CharacterActionRequest request = CharacterActionRequest.CombatAction(
                RuntimeFrame.Zero,
                CreateEntity(),
                CharacterActionKind.Attack,
                QuickAttackId,
                sourceId: 4,
                traceId: CharacterTestRuntimeTraceIds.Motion,
                actionIntentId: CharacterTestNumericIds.IntentMotionAttack);

            CharacterActionResult result = controller.Submit(request);

            CharacterMotionRequestFrame frame = motionSink.ToFrame();
            Assert.IsTrue(result.Success, result.Message);
            Assert.AreEqual((int)CharacterMovementMode.Run, frame.MovementModeId);
            Assert.IsTrue(frame.HasImpulse);
            Assert.AreEqual(0, frame.ImpulseVelocityX);
            Assert.AreEqual(0, frame.ImpulseVelocityY);
            Assert.AreEqual(2000, frame.ImpulseVelocityZ);
            Assert.IsTrue(frame.HasMovementControlScale);
            Assert.IsFalse(frame.HasRotationControlScale);
            Assert.AreEqual(250, frame.MovementControlScaleMilli);
            Assert.AreEqual(CharacterTestNumericIds.Assert_motion_scale, frame.ControlScaleSource.StableEventId);
        }

        [Test]
        public void CharacterActionRuntime_RootMotionSamplesWriteCharacterControlRequestFrame()
        {
            var machine = new CharacterControlStateMachine(CreateEntity());
            var motionSink = new CharacterActionMotionRequestAccumulatorSink();
            CharacterActionConfig action = CreateRootMotionFrameAction();
            var controller = new CharacterActionController(
                machine,
                characterActionOptions: CreateCharacterActionOptions(
                    new[] { action },
                    new[] { CharacterTestNumericIds.IntentRootMotionAttack },
                    new CharacterActionTrackAdapter(motionSink: motionSink)));
            CharacterActionRequest request = CharacterActionRequest.CombatAction(
                RuntimeFrame.Zero,
                CreateEntity(),
                CharacterActionKind.Attack,
                QuickAttackId,
                sourceId: 4,
                traceId: CharacterTestRuntimeTraceIds.RootMotion,
                actionIntentId: CharacterTestNumericIds.IntentRootMotionAttack);

            CharacterActionResult start = controller.Submit(request);
            CharacterMotionRequestFrame startFrame = motionSink.ToFrame();
            motionSink.Reset();
            CharacterActionResult tick = controller.TickCharacterAction(new RuntimeFrame(1));
            CharacterMotionRequestFrame tickFrame = motionSink.ToFrame();

            Assert.IsTrue(start.Success, start.Message);
            Assert.IsTrue(startFrame.HasRootMotion);
            Assert.AreEqual(0, startFrame.RootMotionDeltaX);
            Assert.AreEqual(0, startFrame.RootMotionDeltaY);
            Assert.AreEqual(500, startFrame.RootMotionDeltaZ);
            Assert.AreEqual((int)CharacterMovementMode.RootMotionDriven, startFrame.MovementModeId);
            Assert.AreEqual(CharacterTestNumericIds.Assert_anim_root, startFrame.RootMotionSource.StableEventId);
            Assert.AreEqual(CharacterMotionObstaclePolicy.StaticOnly, startFrame.RootMotionObstaclePolicy);
            Assert.IsTrue(tick.Success, tick.Message);
            Assert.IsTrue(tickFrame.HasRootMotion);
            Assert.AreEqual(250, tickFrame.RootMotionDeltaX);
            Assert.AreEqual(0, tickFrame.RootMotionDeltaY);
            Assert.AreEqual(0, tickFrame.RootMotionDeltaZ);
            Assert.AreEqual((int)CharacterMovementMode.RootMotionDriven, tickFrame.MovementModeId);
        }

        [Test]
        public void CharacterActionRuntime_UsesCancelWindowForImmediateTransition()
        {
            var machine = new CharacterControlStateMachine(CreateEntity());
            CharacterActionConfig quick = CreateCharacterAction(
                id: 100,
                stableId: "character_control.quick_attack",
                intentId: CharacterTestNumericIds.IntentLightAttack,
                combatActionId: QuickAttackId,
                cancelRules: new[]
                {
                    new CharacterCancelRule(1, 1)
                });
            CharacterActionConfig heavy = CreateCharacterAction(
                id: 200,
                stableId: "character_control.heavy_attack",
                intentId: CharacterTestNumericIds.IntentHeavyAttack,
                combatActionId: HeavyAttackId);
            var controller = new CharacterActionController(
                machine,
                characterActionOptions: CreateCharacterActionOptions(
                    new[] { quick, heavy },
                    new[] { CharacterTestNumericIds.IntentLightAttack, CharacterTestNumericIds.IntentHeavyAttack }));
            var events = new List<CharacterActionEvent>();
            controller.ActionEvent += events.Add;
            CharacterActionRequest first = CharacterActionRequest.CombatAction(
                RuntimeFrame.Zero,
                CreateEntity(),
                CharacterActionKind.Attack,
                QuickAttackId,
                sourceId: 4,
                traceId: CharacterTestRuntimeTraceIds.Attack1,
                actionIntentId: CharacterTestNumericIds.IntentLightAttack);
            CharacterActionRequest second = CharacterActionRequest.CombatAction(
                new RuntimeFrame(1),
                CreateEntity(),
                CharacterActionKind.Attack,
                HeavyAttackId,
                sourceId: 4,
                traceId: CharacterTestRuntimeTraceIds.Attack2,
                actionIntentId: CharacterTestNumericIds.IntentHeavyAttack);

            CharacterActionResult start = controller.Submit(first);
            Assert.IsTrue(start.Success, start.Message);
            controller.TickCharacterAction(new RuntimeFrame(1));
            CharacterActionResult transition = controller.Submit(second);

            Assert.IsTrue(transition.Success);
            Assert.IsFalse(transition.Queued);
            Assert.AreEqual(CharacterTestActionIds.Action_heavy_attack, controller.CreateDebugSnapshot().ActiveActionId);
            Assert.IsTrue(events.Exists(evt => evt.Type == CharacterActionEventType.Canceled && evt.Request.ActionIntentId == CharacterTestBulkIds36.Id790002));
            Assert.IsTrue(events.Exists(evt => evt.Type == CharacterActionEventType.Started && evt.Request.ActionIntentId == CharacterTestBulkIds36.Id790008));
        }

        [Test]
        public void SubmitReaction_StartsReactionStateWithDefaultLockMask()
        {
            var machine = new CharacterControlStateMachine(CreateEntity());
            CharacterActionConfig reaction = CreateReactionAction("cohesion_break_react");
            var controller = new CharacterActionController(
                machine,
                characterActionOptions: CreateCharacterActionOptions(
                    new[] { reaction },
                    new[] { CharacterTestNumericIds.IntentLightAttack },
                    reactionProfiles: new[] { CreateCohesionBreakProfile(CharacterTestActionIds.Action_cohesion_break_react) },
                    reactionProfileId: "pressure-only"));

            CharacterActionResult result = controller.SubmitReaction(CreateCohesionBreakContext());

            Assert.IsTrue(result.Success, result.Message);
            Assert.AreEqual(CharacterControlState.Reaction, machine.CurrentState);
            Assert.AreEqual(
                CharacterControlLockMask.Move | CharacterControlLockMask.Jump | CharacterControlLockMask.Action,
                machine.ControlLockMask);
        }

        [Test]
        public void SubmitReaction_FinishAndLifecycleTerminationReturnToLocomotion()
        {
            var finishMachine = new CharacterControlStateMachine(CreateEntity());
            CharacterActionConfig finishReaction = CreateReactionAction("cohesion_break_react", durationFrames: 2);
            var finishController = new CharacterActionController(
                finishMachine,
                characterActionOptions: CreateCharacterActionOptions(
                    new[] { finishReaction },
                    new[] { CharacterTestNumericIds.IntentLightAttack },
                    reactionProfiles: new[] { CreateCohesionBreakProfile(CharacterTestActionIds.Action_cohesion_break_react) },
                    reactionProfileId: "pressure-only"));
            Assert.IsTrue(finishController.SubmitReaction(CreateCohesionBreakContext()).Success);

            finishController.TickCharacterAction(new RuntimeFrame(1));
            finishController.TickCharacterAction(new RuntimeFrame(2));

            Assert.AreEqual(CharacterControlState.Locomotion, finishMachine.CurrentState);

            var terminateMachine = new CharacterControlStateMachine(CreateEntity());
            CharacterActionConfig terminateReaction = CreateReactionAction("cohesion_break_react", durationFrames: 8);
            var terminateController = new CharacterActionController(
                terminateMachine,
                characterActionOptions: CreateCharacterActionOptions(
                    new[] { terminateReaction },
                    new[] { CharacterTestNumericIds.IntentLightAttack },
                    reactionProfiles: new[] { CreateCohesionBreakProfile(CharacterTestActionIds.Action_cohesion_break_react) },
                    reactionProfileId: "pressure-only"));
            Assert.IsTrue(terminateController.SubmitReaction(CreateCohesionBreakContext()).Success);

            CharacterActionResult terminate = terminateController.TerminateForLifecycle(
                new RuntimeFrame(1),
                CharacterActionLifecycleReason.ReviveReset,
                CharacterTestBulkIds.Id795002);

            Assert.IsTrue(terminate.Success, terminate.Message);
            Assert.AreEqual(CharacterControlState.Locomotion, terminateMachine.CurrentState);
        }

        [Test]
        public void CharacterActionRuntime_CommandStillStartsActionStateWithReactionOptionsPresent()
        {
            var machine = new CharacterControlStateMachine(CreateEntity());
            CharacterActionConfig action = CreateCharacterAction(
                id: 100,
                stableId: "character_control.quick_attack",
                intentId: CharacterTestNumericIds.IntentLightAttack,
                combatActionId: QuickAttackId);
            CharacterActionConfig reaction = CreateReactionAction("cohesion_break_react");
            var controller = new CharacterActionController(
                machine,
                characterActionOptions: CreateCharacterActionOptions(
                    new[] { action, reaction },
                    new[] { CharacterTestNumericIds.IntentLightAttack, CharacterTestNumericIds.IntentUnusedReaction },
                    reactionProfiles: new[] { CreateCohesionBreakProfile(CharacterTestActionIds.Action_cohesion_break_react) },
                    reactionProfileId: "pressure-only"));
            CharacterActionRequest request = CharacterActionRequest.CombatAction(
                RuntimeFrame.Zero,
                CreateEntity(),
                CharacterActionKind.Attack,
                QuickAttackId,
                actionIntentId: CharacterTestNumericIds.IntentLightAttack);

            CharacterActionResult result = controller.Submit(request);

            Assert.IsTrue(result.Success, result.Message);
            Assert.AreEqual(CharacterControlState.Action, machine.CurrentState);
        }

        [Test]
        public void ReactionBridge_AppliesCohesionDeathAndExplicitHitContexts()
        {
            CharacterActionConfig cohesion = CreateReactionAction("cohesion_break_react");
            CharacterActionConfig death = CreateReactionAction("death_react");
            CharacterActionConfig hit = CreateReactionAction("torso_hit_react");
            CharacterReactionProfile profile = CreateMixedReactionProfile();

            var cohesionMachine = new CharacterControlStateMachine(CreateEntity());
            var cohesionController = new CharacterActionController(
                cohesionMachine,
                characterActionOptions: CreateCharacterActionOptions(
                    new[] { cohesion, death, hit },
                    new[] { CharacterTestNumericIds.IntentLightAttack, CharacterTestNumericIds.IntentDeath, CharacterTestNumericIds.IntentHit },
                    reactionProfiles: new[] { profile },
                    reactionProfileId: "pressure-only"));
            var cohesionBridge = new CharacterActionReactionBridge(cohesionController);

            CharacterActionResult cohesionResult = cohesionBridge.Apply(new CohesionBreakEvent(
                new RuntimeFrame(3),
                CreateEntity().GameplayEntityId,
                PressureBand.Critical,
                previousValue: 80,
                currentScatterPressure: 100,
                maxScatterPressure: 100,
                delta: 20));

            Assert.IsTrue(cohesionResult.Success, cohesionResult.Message);
            Assert.AreEqual(CharacterControlState.Reaction, cohesionMachine.CurrentState);

            var deathMachine = new CharacterControlStateMachine(CreateEntity());
            var deathController = new CharacterActionController(
                deathMachine,
                characterActionOptions: CreateCharacterActionOptions(
                    new[] { cohesion, death, hit },
                    new[] { CharacterTestNumericIds.IntentLightAttack, CharacterTestNumericIds.IntentDeath, CharacterTestNumericIds.IntentHit },
                    reactionProfiles: new[] { profile },
                    reactionProfileId: "pressure-only"));
            var deathBridge = new CharacterActionReactionBridge(deathController);

            CharacterActionResult deathResult = deathBridge.ApplyDeath(
                new RuntimeFrame(7),
                CreateEntity().GameplayEntityId,
                "death",
                CharacterTestBulkIds.Id795003);

            Assert.IsTrue(deathResult.Success, deathResult.Message);
            Assert.AreEqual(CharacterControlState.Reaction, deathMachine.CurrentState);

            var hitMachine = new CharacterControlStateMachine(CreateEntity());
            var hitController = new CharacterActionController(
                hitMachine,
                characterActionOptions: CreateCharacterActionOptions(
                    new[] { cohesion, death, hit },
                    new[] { CharacterTestNumericIds.IntentLightAttack, CharacterTestNumericIds.IntentDeath, CharacterTestNumericIds.IntentHit },
                    reactionProfiles: new[] { profile },
                    reactionProfileId: "pressure-only"));
            var hitBridge = new CharacterActionReactionBridge(hitController);
            DamageResolvedEvent damage = CreateDamageEvent();

            Assert.AreEqual(CharacterControlState.Locomotion, hitMachine.CurrentState);
            CharacterActionResult hitResult = hitBridge.ApplyHit(
                damage,
                new CharacterReactionHitMetadata(
                    CreateEntity().GameplayEntityId,
                    CharacterTestBulkIds.Id790033,
                    CharacterHitDirection.Back));

            Assert.IsTrue(hitResult.Success, hitResult.Message);
            Assert.AreEqual(CharacterControlState.Reaction, hitMachine.CurrentState);
        }

        [Test]
        public void GameplayTrackBridge_EnqueuesStableRuntimeCommand()
        {
            var buffer = new RuntimeCommandBuffer();
            var bridge = new CharacterActionGameplayTrackBridge(buffer);
            var metadata = new CharacterActionAdapterDispatchMetadata(
                new RuntimeFrame(7),
                instanceId: 1,
                planId: 100,
                actionId: CharacterTestActionIds.Action_character_control_ability,
                localFrame: 0,
                stableEventId: CharacterTestNumericIds.Field_ability_cast,
                sourceId: 9,
                traceId: CharacterTestRuntimeTraceIds.Skill);
            var request = new CharacterActionGameplayRequest(
                metadata,
                CreateEntity().GameplayEntityId,
                CharacterActionTrackEventKind.CastAbility,
                AbilityStrike,
                0);

            CharacterActionAdapterSinkResult result = bridge.SubmitGameplayRequest(request);

            Assert.IsTrue(result.Accepted);
            IReadOnlyList<RuntimeCommand> commands = buffer.DrainForFrame(new RuntimeFrame(7));
            Assert.AreEqual(1, commands.Count);
            Assert.AreEqual(GameplayRuntimeCommandIds.CastComponentAbility, commands[0].CommandId);
            Assert.AreEqual(9, commands[0].SourceId);
            Assert.AreEqual(CreateEntity().GameplayEntityId.Index, commands[0].TargetId);
            Assert.AreEqual(CreateEntity().GameplayEntityId.Generation, commands[0].Payload0);
            Assert.AreEqual(AbilityStrike, commands[0].Payload1);
            Assert.AreEqual(CharacterTestRuntimeTraceIds.Skill, commands[0].TraceId);
        }

        [Test]
        public void ConstraintCanRejectCooldownResourceOrStatusRules()
        {
            var machine = new CharacterControlStateMachine(CreateEntity());
            CharacterActionConfig action = CreateCharacterAction(
                QuickAttackId,
                "character_control.ability",
                CharacterActionRequest.GameplayAbilityIntentId,
                QuickAttackId);
            var controller = new CharacterActionController(
                machine,
                constraints: new ICharacterActionConstraint[]
                {
                    new RejectingConstraint()
                },
                characterActionOptions: CreateCharacterActionOptions(
                    action,
                    CharacterActionRequest.GameplayAbilityIntentId));
            CharacterActionRequest request = CharacterActionRequest.GameplayAbility(
                RuntimeFrame.Zero,
                CreateEntity(),
                AbilityStrike,
                sourceId: 9,
                traceId: CharacterTestRuntimeTraceIds.Cooldown);

            CharacterActionResult result = controller.Submit(request);

            Assert.IsFalse(result.Success);
            Assert.AreEqual(CharacterActionRejectedReason.ConstraintRejected, result.RejectedReason);
            Assert.AreEqual("cooldown", result.Message);
        }

        [Test]
        public void ActionLockRejectsActionRequest()
        {
            var machine = new CharacterControlStateMachine(CreateEntity());
            machine.SetControlLockMask(CharacterControlLockMask.Action, RuntimeFrame.Zero);
            CharacterActionConfig action = CreateCharacterAction(
                QuickAttackId,
                "character_control.ability",
                CharacterActionRequest.GameplayAbilityIntentId,
                QuickAttackId);
            var controller = new CharacterActionController(
                machine,
                characterActionOptions: CreateCharacterActionOptions(
                    action,
                    CharacterActionRequest.GameplayAbilityIntentId));
            CharacterActionRequest request = CharacterActionRequest.GameplayAbility(
                RuntimeFrame.Zero,
                CreateEntity(),
                AbilityStrike);

            CharacterActionResult result = controller.Submit(request);

            Assert.IsFalse(result.Success);
            Assert.AreEqual(CharacterActionRejectedReason.ActionLocked, result.RejectedReason);
        }

        private static CharacterControlEntityRef CreateEntity()
        {
            return CharacterControlEntityRef.FromGameplayAndCombat(
                new GameplayEntityId(1, 1),
                new CombatEntityId(10),
                new CombatBodyId(10),
                stableId: 1);
        }

        private static CharacterActionCombatTrackBridge CreateCombatBridge()
        {
            var registry = new CombatActionRegistry();
            registry.RegisterTimeline(QuickAttackId, new CombatActionTimeline(
                QuickAttackId,
                totalFrames: 3,
                startup: new CombatFrameRange(0, 0),
                active: new CombatFrameRange(1, 1),
                recovery: new CombatFrameRange(2, 2),
                windows: null,
                events: null));
            registry.RegisterTimeline(HeavyAttackId, new CombatActionTimeline(
                HeavyAttackId,
                totalFrames: 4,
                startup: new CombatFrameRange(0, 1),
                active: new CombatFrameRange(2, 2),
                recovery: new CombatFrameRange(3, 3),
                windows: null,
                events: null));
            return new CharacterActionCombatTrackBridge(registry);
        }

        private static CharacterActionControllerOptions CreateCharacterActionOptions(
            CharacterActionConfig action,
            int intentId,
            CharacterActionTrackAdapter trackAdapter = null,
            ICharacterActionDownstreamLifecycleSink downstreamLifecycleSink = null)
        {
            return CreateCharacterActionOptions(
                new[] { action },
                new[] { intentId },
                trackAdapter: trackAdapter,
                downstreamLifecycleSink: downstreamLifecycleSink);
        }

        private static CharacterActionControllerOptions CreateCharacterActionOptions(
            CharacterActionConfig[] actions,
            int[] intentIds,
            CharacterActionTrackAdapter trackAdapter = null,
            CharacterActionPresentationTrackAdapter presentationTrackAdapter = null,
            string presentationTargetActorId = "",
            string animationLayerId = "base",
            CharacterReactionProfile[] reactionProfiles = null,
            string reactionProfileId = "",
            ICharacterActionDownstreamLifecycleSink downstreamLifecycleSink = null)
        {
            var bindings = new CharacterActionBinding[actions.Length];
            for (int i = 0; i < actions.Length; i++)
            {
                bindings[i] = new CharacterActionBinding(
                    intentIds[i],
                    actions[i].ActionId,
                    priority: 0,
                    allowQueue: false,
                    queueWindowFrames: 0);
            }

            var actionSet = new CharacterActionSetConfig(
                1,
                "character_control.default",
                "Character Control Default",
                string.Empty,
                string.Empty,
                bindings,
                abilityBindings: null,
                reactionBindings: null,
                movementProfileId: 0,
                reactionProfileId: reactionProfileId,
                defaultActionId: MxFramework.CharacterAction.CharacterActionId.None);
            return new CharacterActionControllerOptions(
                actionSet,
                actions,
                reactionProfiles: reactionProfiles,
                trackAdapter: trackAdapter,
                presentationTrackAdapter: presentationTrackAdapter,
                downstreamLifecycleSink: downstreamLifecycleSink,
                presentationTargetActorId: presentationTargetActorId,
                animationLayerId: animationLayerId);
        }

        private static CharacterActionConfig CreateCharacterAction(
            int id,
            string stableId,
            int intentId,
            int combatActionId,
            CharacterCancelRule[] cancelRules = null,
            int combatTrackFrame = 0)
        {
            return new CharacterActionConfig(
                new CharacterActionId(id),
                stableId,
                CharacterActionCategory.BasicAttack,
                CharacterActionTimelineAuthority.CharacterAuthored,
                tags: null,
                priority: 0,
                durationFrames: 3,
                requirements: null,
                phases: new[]
                {
                    new CharacterActionPhase(CharacterActionPhaseKind.Startup, 0, 0),
                    new CharacterActionPhase(CharacterActionPhaseKind.Active, 1, 1),
                    new CharacterActionPhase(CharacterActionPhaseKind.Recovery, 2, 2)
                },
                cancelRules: cancelRules,
                interruptRules: null,
                combatTrack: new CombatTrackConfig(
                    combatActionId,
                    new[]
                    {
                        new CombatTrackEvent(
                            combatTrackFrame,
                            CharacterActionTrackEventKind.StartCombatAction,
                            combatActionId,
                            stableEventId: CharacterTestBulkIds.Id790034)
                    }));
        }

        private static CharacterActionConfig CreateReactionAction(string stableId, int durationFrames = 3)
        {
            return new CharacterActionConfig(
                new CharacterActionId(500 + stableId.Length),
                stableId,
                CharacterActionCategory.Reaction,
                CharacterActionTimelineAuthority.CharacterAuthored,
                tags: null,
                priority: 100,
                durationFrames: durationFrames,
                requirements: null,
                phases: CreateReactionPhases(durationFrames),
                cancelRules: null,
                interruptRules: null);
        }

        private static CharacterActionPhase[] CreateReactionPhases(int durationFrames)
        {
            if (durationFrames <= 1)
                return new[] { new CharacterActionPhase(CharacterActionPhaseKind.Active, 0, 0) };
            if (durationFrames == 2)
            {
                return new[]
                {
                    new CharacterActionPhase(CharacterActionPhaseKind.Startup, 0, 0),
                    new CharacterActionPhase(CharacterActionPhaseKind.Recovery, 1, 1)
                };
            }

            return new[]
            {
                new CharacterActionPhase(CharacterActionPhaseKind.Startup, 0, 0),
                new CharacterActionPhase(CharacterActionPhaseKind.Active, 1, durationFrames - 2),
                new CharacterActionPhase(CharacterActionPhaseKind.Recovery, durationFrames - 1, durationFrames - 1)
            };
        }

        private static CharacterReactionProfile CreateCohesionBreakProfile(CharacterActionId actionId)
        {
            return new CharacterReactionProfile(
                "pressure-only",
                new[]
                {
                    new CharacterReactionRule(
                        actionId,
                        CharacterReactionRuleTrigger.CohesionBreak,
                        currentScatterPressureBand: PressureBand.Broken),
                });
        }

        private static CharacterReactionProfile CreateMixedReactionProfile()
        {
            return new CharacterReactionProfile(
                "pressure-only",
                new[]
                {
                    new CharacterReactionRule(
                        CharacterTestActionIds.Action_cohesion_break_react,
                        CharacterReactionRuleTrigger.CohesionBreak,
                        currentScatterPressureBand: PressureBand.Broken),
                    new CharacterReactionRule(
                        CharacterTestActionIds.Action_death_react,
                        CharacterReactionRuleTrigger.Death,
                        isDeath: true),
                    new CharacterReactionRule(
                        CharacterTestActionIds.Action_torso_hit_react,
                        CharacterReactionRuleTrigger.Hit,
                        bodyPartId: CharacterTestNumericIds.BodyPartTorso,
                        hitZoneId: CharacterTestNumericIds.HitZoneRib,
                        damageTypeId: CharacterTestNumericIds.DamageSlash,
                        hitDirection: CharacterHitDirection.Back,
                        minImpactForce: 30,
                        reactionGroupId: CharacterTestNumericIds.ReactionCore),
                });
        }

        private static CharacterReactionContext CreateCohesionBreakContext()
        {
            return CharacterReactionContextBuilder.FromCohesionBreak(new CohesionBreakEvent(
                new RuntimeFrame(3),
                CreateEntity().GameplayEntityId,
                PressureBand.Critical,
                previousValue: 80,
                currentScatterPressure: 100,
                maxScatterPressure: 100,
                delta: 20)).Context;
        }

        private static DamageResolvedEvent CreateDamageEvent()
        {
            var usage = new DamageProfileUsage(
                DamageProfileSourceKind.WeaponTrace,
                actionId: CharacterTestActionIds.Action_attack_light,
                sourceEntityId: 2,
                targetEntityId: CreateEntity().GameplayEntityId.Index,
                hitZoneId: CharacterTestNumericIds.HitZoneRib,
                bodyPartId: CharacterTestNumericIds.BodyPartTorso,
                frame: 5);
            var profileResult = new DamageProfileResolveResult(
                success: true,
                reason: string.Empty,
                damageProfileId: CharacterTestBulkIds.Id790035,
                damageTypeId: CharacterTestNumericIds.DamageSlash,
                formulaId: DamageFormulaIds.Flat,
                incomingDamage: 20,
                staggerFrames: 1,
                impactForce: 0,
                knockback: FixVector3.Zero,
                impactPower: 48,
                guardPressure: 0,
                presentationProfileId: 0,
                usage: usage);
            var hitResult = new HitResolveResult(
                new CombatEntityId(2),
                CreateEntity().CombatEntityId,
                actionId: 100,
                actionInstanceId: 1,
                traceId: 7,
                frame: new CombatFrame(5),
                kind: HitResolveKind.Damage,
                damage: 20,
                staggerFrames: 1,
                knockback: FixVector3.Zero);
            return new DamageResolvedEvent(
                new DamageRequest(CharacterTestBulkIds.Id790035, usage),
                profileResult,
                hitResult,
                finalDamage: 20);
        }

        private static CharacterActionConfig CreateAdapterTrackAction()
        {
            return new CharacterActionConfig(
                new CharacterActionId(300),
                "Adapter Attack",
                CharacterActionCategory.BasicAttack,
                CharacterActionTimelineAuthority.CharacterAuthored,
                tags: null,
                priority: 0,
                durationFrames: 2,
                requirements: null,
                phases: new[]
                {
                    new CharacterActionPhase(CharacterActionPhaseKind.Active, 0, 0),
                    new CharacterActionPhase(CharacterActionPhaseKind.Recovery, 1, 1)
                },
                cancelRules: null,
                interruptRules: null,
                motionTrack: new MotionTrackConfig(
                    usesRootMotion: false,
                    events: new[]
                    {
                        new MotionTrackEvent(
                            0,
                            CharacterActionTrackEventKind.ApplyImpulse,
                            CharacterMovementMode.Airborne,
                            x: 1f,
                            stableEventId: CharacterTestNumericIds.Field_motion_impulse)
                    }),
                combatTrack: new CombatTrackConfig(
                    0,
                    new[]
                    {
                        new CombatTrackEvent(
                            0,
                            CharacterActionTrackEventKind.HitTraceWindow,
                             traceProfileId: 0,
                            stableEventId: CharacterTestNumericIds.Field_combat_trace,
                            equipmentSlotId: CharacterTestNumericIds.Field_mainHand)
                    }),
                gameplayTrack: new GameplayTrackConfig(new[]
                {
                    new GameplayTrackEvent(
                        0,
                        CharacterActionTrackEventKind.SendGameplayRequest,
                        requestId: CharacterTestNumericIds.Field_gameplay_hit,
                        stableEventId: CharacterTestNumericIds.Field_gameplay_hit)
                }),
                animationTrack: new AnimationTrackConfig(new[]
                {
                    new AnimationTrackEvent(
                        0,
                        CharacterActionTrackEventKind.CrossFadeAnimation,
                        "anim.light",
                        0.08f,
                        stableEventId: CharacterTestNumericIds.Field_anim_light)
                }),
                presentationTrack: new PresentationTrackConfig(new[]
                {
                    new PresentationTrackEvent(
                        0,
                        CharacterActionTrackEventKind.SpawnVisualCue,
                        resourceKey: "vfx.light",
                        stableEventId: CharacterTestNumericIds.Field_vfx_light,
                        endFrame: 1)
                }));
        }

        private static CharacterActionConfig CreateMotionRequestFrameAction()
        {
            return new CharacterActionConfig(
                new CharacterActionId(301),
                "Motion Request Frame",
                CharacterActionCategory.BasicAttack,
                CharacterActionTimelineAuthority.CharacterAuthored,
                tags: null,
                priority: 0,
                durationFrames: 1,
                requirements: null,
                phases: new[]
                {
                    new CharacterActionPhase(CharacterActionPhaseKind.Active, 0, 0)
                },
                cancelRules: null,
                interruptRules: null,
                motionTrack: new MotionTrackConfig(
                    usesRootMotion: false,
                    events: new[]
                    {
                        new MotionTrackEvent(
                            0,
                            CharacterActionTrackEventKind.SetMovementMode,
                            CharacterMovementMode.Run,
                            stableEventId: CharacterTestNumericIds.Field_motion_run),
                        new MotionTrackEvent(
                            0,
                            CharacterActionTrackEventKind.ApplyImpulse,
                            CharacterMovementMode.Idle,
                            z: 2f,
                            stableEventId: CharacterTestNumericIds.Field_motion_impulse),
                        new MotionTrackEvent(
                            0,
                            CharacterActionTrackEventKind.SetControlScale,
                            CharacterMovementMode.Airborne,
                            stableEventId: CharacterTestNumericIds.Field_motion_scale,
                            movementControlScale: 0.25f,
                            rotationControlScale: 1f,
                            hasMovementControlScale: true,
                            hasRotationControlScale: false)
                    }));
        }

        private static CharacterActionConfig CreateDebugTrackAction()
        {
            return new CharacterActionConfig(
                new CharacterActionId(303),
                "Debug Track",
                CharacterActionCategory.BasicAttack,
                CharacterActionTimelineAuthority.CharacterAuthored,
                tags: null,
                priority: 0,
                durationFrames: 1,
                requirements: null,
                phases: new[]
                {
                    new CharacterActionPhase(CharacterActionPhaseKind.Active, 0, 0)
                },
                cancelRules: null,
                interruptRules: null,
                debugTrack: new DebugTrackConfig(new[]
                {
                    new DebugTrackEvent(
                        0,
                        CharacterActionTrackEventKind.EmitDebugMarker,
                        "debug.light.start",
                        CharacterTestBulkIds.Id790025)
                }));
        }

        private static CharacterActionConfig CreateRootMotionFrameAction()
        {
            return new CharacterActionConfig(
                new CharacterActionId(302),
                "Root Motion Frame",
                CharacterActionCategory.BasicAttack,
                CharacterActionTimelineAuthority.CharacterAuthored,
                tags: null,
                priority: 0,
                durationFrames: 3,
                requirements: null,
                phases: new[]
                {
                    new CharacterActionPhase(CharacterActionPhaseKind.Active, 0, 1),
                    new CharacterActionPhase(CharacterActionPhaseKind.Recovery, 2, 2)
                },
                cancelRules: null,
                interruptRules: null,
                motionTrack: new MotionTrackConfig(
                    usesRootMotion: true,
                    events: null,
                    rootMotion: new CharacterActionRootMotionConfig(
                        sourceAnimationEventId: CharacterTestNumericIds.Field_anim_root,
                        sourceAnimationClipResourceKey: "anim.light",
                        space: CharacterActionRootMotionSpace.World,
                        sampleTickRate: CharacterActionAnimationKeyUtility.TimelineFramesPerSecond,
                        obstaclePolicy: CharacterActionRootMotionObstaclePolicy.StaticOnly,
                        frames: new[]
                        {
                            new CharacterActionRootMotionFrame(0, 0f, 0f, 0.5f),
                            new CharacterActionRootMotionFrame(1, 0.25f, 0f, 0f)
                        })));
        }

        private sealed class RejectingConstraint : ICharacterActionConstraint
        {
            public CharacterActionConstraintResult Evaluate(CharacterActionContext context)
            {
                return CharacterActionConstraintResult.Rejected(CharacterActionRejectedReason.ConstraintRejected, "cooldown");
            }
        }
    }
}
