using System;
using MxFramework.CharacterAction;
using MxFramework.Combat.Core;
using MxFramework.Combat.Damage;
using MxFramework.Core;
using MxFramework.Editor.CharacterAction;
using MxFramework.Gameplay;
using MxFramework.Runtime;
using NUnit.Framework;

namespace MxFramework.Tests.CharacterAction
{
    public sealed class CharacterActionTrackAdapterTests
    {
        [Test]
        public void Adapter_SetHitTargetStatePreservesMaskAndRangeLifecycle()
        {
            CharacterActionConfig action = CreateAction(
                combatTrack: new CombatTrackConfig(0, new[]
                {
                    new CombatTrackEvent(
                        0,
                        CharacterActionTrackEventKind.SetHitTargetState,
                        stableEventId: CharacterTestNumericIds.Field_state_unhittable,
                        endFrame: 1,
                        targetStateFlags: MxFramework.Combat.Hit.HitTargetStateFlags.Unhittable),
                }));
            CharacterActionRunner runner = StartRunner(action);
            var collector = new CharacterActionAdapterRequestCollector();
            var adapter = new CharacterActionTrackAdapter(combatSink: collector);

            CharacterActionAdapterResult first = adapter.AdaptMany(
                runner.DrainEvents(),
                CreateContext(),
                new CharacterActionDispatchBuffer());
            CharacterActionAdapterResult second = adapter.AdaptMany(
                runner.Tick().Events,
                CreateContext(),
                new CharacterActionDispatchBuffer());

            Assert.IsTrue(first.Accepted && second.Accepted);
            Assert.IsTrue(collector.CombatRequests.Count >= 3);
            for (int i = 0; i < collector.CombatRequests.Count; i++)
            {
                Assert.AreEqual(CharacterActionTrackEventKind.SetHitTargetState, collector.CombatRequests[i].EventKind);
                Assert.AreEqual(MxFramework.Combat.Hit.HitTargetStateFlags.Unhittable, collector.CombatRequests[i].TargetStateFlags);
            }
        }

        [Test]
        public void Adapter_RoutesRunnerDispatchToMotionCombatAndGameplayRequestRecords()
        {
            CharacterActionConfig action = CreateAction(
                motionTrack: new MotionTrackConfig(false, new[]
                {
                    new MotionTrackEvent(
                        1,
                        CharacterActionTrackEventKind.SetMovementMode,
                        CharacterMovementMode.Run,
                        1f,
                        0f,
                        0f,
                        CharacterTestBulkIds.Id790021),
                }),
                combatTrack: new CombatTrackConfig(CharacterTestNumericIds.Field_combat_light, new[]
                {
                    new CombatTrackEvent(
                        1,
                        CharacterActionTrackEventKind.StartCombatAction,
                        CharacterTestBulkIds.Id790022,
                        stableEventId: CharacterTestNumericIds.Field_combat_start),
                    new CombatTrackEvent(
                        1,
                        CharacterActionTrackEventKind.HitTraceWindow,
                        equipmentSlotId: CharacterTestNumericIds.Field_mainHand,
                        stableEventId: CharacterTestNumericIds.Field_combat_trace_start,
                        damagePayload: CreateLightDamagePayload()),
                }),
                gameplayTrack: new GameplayTrackConfig(new[]
                {
                    new GameplayTrackEvent(
                        1,
                        CharacterActionTrackEventKind.ApplyGameplayEffect,
                        requestId: CharacterTestNumericIds.Field_pressure_scatter_light,
                        abilityStableId: CharacterTestNumericIds.Field_ability_light,
                        stableEventId: CharacterTestNumericIds.Field_gameplay_pressure,
                        targetPolicyId: CharacterTestNumericIds.Field_target_locked_area_2m,
                        damagePayload: CreateAreaDamagePayload()),
                }));
            CharacterActionRunner runner = StartRunner(action);
            CharacterActionRunnerOperationResult tick = runner.Tick();
            var collector = new CharacterActionAdapterRequestCollector();
            var adapter = new CharacterActionTrackAdapter(collector, collector, collector, collector);

            CharacterActionAdapterResult result = adapter.AdaptMany(tick.Events, CreateContext(), new CharacterActionDispatchBuffer());

            Assert.IsTrue(result.Accepted);
            Assert.AreEqual(1, result.MotionRequestCount);
            Assert.AreEqual(4, result.CombatRequestCount);
            Assert.AreEqual(1, result.GameplayRequestCount);
            Assert.AreEqual(1, collector.MotionRequests.Count);
            Assert.AreEqual(4, collector.CombatRequests.Count);
            Assert.AreEqual(1, collector.GameplayRequests.Count);
            Assert.AreEqual(CharacterMovementMode.Run, collector.MotionRequests[0].MovementMode);
            Assert.AreEqual(new CombatBodyId(13), collector.MotionRequests[0].CombatBodyId);
            Assert.AreEqual(CharacterActionTrackEventKind.StartCombatAction, collector.CombatRequests[0].EventKind);
            Assert.AreEqual(CharacterTestNumericIds.CombatLight, collector.CombatRequests[0].CombatActionId);
            Assert.AreEqual(CharacterActionTrackEventKind.HitTraceWindow, collector.CombatRequests[1].EventKind);
            Assert.AreEqual(CharacterActionTrackLifecyclePhase.Enter, collector.CombatRequests[1].Metadata.LifecyclePhase);
            Assert.AreEqual(CharacterActionTrackLifecyclePhase.Active, collector.CombatRequests[2].Metadata.LifecyclePhase);
            Assert.AreEqual(CharacterActionTrackLifecyclePhase.Exit, collector.CombatRequests[3].Metadata.LifecyclePhase);
            Assert.AreEqual(string.Empty, collector.CombatRequests[1].TraceProfileId);
            Assert.AreEqual("mainHand", collector.CombatRequests[1].EquipmentSlotId);
            Assert.AreEqual(CharacterTestNumericIds.DamageSlash, collector.CombatRequests[1].DamagePayload.DamageTypeId);
            Assert.AreEqual("ct.soft_defense_v1", collector.CombatRequests[1].DamagePayload.FormulaId);
            Assert.AreEqual(6, collector.CombatRequests[1].DamagePayload.BaseDamage);
            Assert.AreEqual(CharacterActionTrackEventKind.ApplyGameplayEffect, collector.GameplayRequests[0].EventKind);
            Assert.AreEqual(CharacterTestNumericIds.Field_pressure_scatter_light, collector.GameplayRequests[0].RequestId);
            Assert.AreEqual(CharacterTestNumericIds.DamageForce, collector.GameplayRequests[0].DamagePayload.DamageTypeId);
            Assert.AreEqual(12, collector.GameplayRequests[0].DamagePayload.ImpactPower);
            Assert.AreEqual("target.locked.area_2m", collector.GameplayRequests[0].TargetPolicyId);
            Assert.AreEqual(new GameplayEntityId(7, 1), collector.GameplayRequests[0].GameplayEntityId);
            Assert.AreEqual(new RuntimeFrame(42), collector.GameplayRequests[0].Metadata.Frame);
            Assert.AreEqual(CharacterTestRuntimeTraceIds.TraceLightAttack, collector.GameplayRequests[0].Metadata.TraceId);
        }

        [Test]
        public void Adapter_RoutesStartEffectTimelineGameplayRequest()
        {
            CharacterActionConfig action = CreateAction(
                gameplayTrack: new GameplayTrackConfig(new[]
                {
                    new GameplayTrackEvent(
                        0,
                        CharacterActionTrackEventKind.StartEffectTimeline,
                        stableEventId: CharacterTestNumericIds.Field_timeline_start,
                        effectTimelineId: CharacterTestNumericIds.Field_test_double_explosion),
                }));
            CharacterActionRunner runner = StartRunner(action);
            var collector = new CharacterActionAdapterRequestCollector();
            var adapter = new CharacterActionTrackAdapter(gameplaySink: collector);

            CharacterActionAdapterResult result = adapter.AdaptMany(runner.DrainEvents(), CreateContext(), new CharacterActionDispatchBuffer());

            Assert.IsTrue(result.Accepted);
            Assert.AreEqual(1, result.GameplayRequestCount);
            Assert.AreEqual(1, collector.GameplayRequests.Count);
            Assert.AreEqual(CharacterActionTrackEventKind.StartEffectTimeline, collector.GameplayRequests[0].EventKind);
            Assert.AreEqual("test.double_explosion", collector.GameplayRequests[0].EffectTimelineId);
            Assert.AreEqual(CharacterTestNumericIds.Field_timeline_start, collector.GameplayRequests[0].Metadata.StableEventId);
        }

        [Test]
        public void Adapter_MissingCombatPayloadEmitsDiagnosticWithoutStartingCombatAuthority()
        {
            CharacterActionConfig action = CreateAction(
                combatTrack: new CombatTrackConfig(0, new[]
                {
                    new CombatTrackEvent(0, CharacterActionTrackEventKind.StartCombatAction, stableEventId: CharacterTestNumericIds.Field_combat_missing),
                }));
            CharacterActionRunner runner = StartRunner(action);
            var collector = new CharacterActionAdapterRequestCollector();
            var adapter = new CharacterActionTrackAdapter(combatSink: collector);

            CharacterActionAdapterResult result = adapter.AdaptMany(runner.DrainEvents(), CreateContext(), new CharacterActionDispatchBuffer());

            Assert.IsFalse(result.Accepted);
            Assert.AreEqual(0, result.CombatRequestCount);
            Assert.AreEqual(0, collector.CombatRequests.Count);
            AssertHasDiagnostic(result.Diagnostics, CharacterActionDiagnosticCodes.AdapterPayloadMissing);
        }

        [Test]
        public void Adapter_MissingGameplayRequestPayloadEmitsDiagnosticWithoutMutatingGameplayAuthority()
        {
            CharacterActionConfig action = CreateAction(
                gameplayTrack: new GameplayTrackConfig(new[]
                {
                    new GameplayTrackEvent(0, CharacterActionTrackEventKind.SendGameplayRequest, stableEventId: CharacterTestNumericIds.Field_gameplay_missing),
                }));
            CharacterActionRunner runner = StartRunner(action);
            var collector = new CharacterActionAdapterRequestCollector();
            var adapter = new CharacterActionTrackAdapter(gameplaySink: collector);

            CharacterActionAdapterResult result = adapter.AdaptMany(runner.DrainEvents(), CreateContext(), new CharacterActionDispatchBuffer());

            Assert.IsFalse(result.Accepted);
            Assert.AreEqual(0, result.GameplayRequestCount);
            Assert.AreEqual(0, collector.GameplayRequests.Count);
            AssertHasDiagnostic(result.Diagnostics, CharacterActionDiagnosticCodes.AdapterPayloadMissing);
        }

        [Test]
        public void Adapter_MissingSinkEmitsDiagnosticWithoutRequestRecord()
        {
            CharacterActionConfig action = CreateAction(
                motionTrack: new MotionTrackConfig(false, new[]
                {
                    new MotionTrackEvent(
                        0,
                        CharacterActionTrackEventKind.LockMovement,
                        CharacterMovementMode.ControlLocked,
                        stableEventId: CharacterTestNumericIds.Field_motion_lock,
                        endFrame: 0),
                }));
            CharacterActionRunner runner = StartRunner(action);
            var adapter = new CharacterActionTrackAdapter();

            CharacterActionAdapterResult result = adapter.AdaptMany(runner.DrainEvents(), CreateContext(), new CharacterActionDispatchBuffer());

            Assert.IsFalse(result.Accepted);
            Assert.AreEqual(0, result.MotionRequestCount);
            AssertHasDiagnostic(result.Diagnostics, CharacterActionDiagnosticCodes.AdapterSinkMissing);
        }

        [Test]
        public void Adapter_RoutesControlScaleMotionRequestPayload()
        {
            CharacterActionConfig action = CreateAction(
                motionTrack: new MotionTrackConfig(false, new[]
                {
                    new MotionTrackEvent(
                        0,
                        CharacterActionTrackEventKind.SetControlScale,
                        CharacterMovementMode.Idle,
                        stableEventId: CharacterTestNumericIds.Field_motion_control,
                        movementControlScale: 0.25f,
                        rotationControlScale: 0.75f),
                }));
            CharacterActionRunner runner = StartRunner(action);
            var collector = new CharacterActionAdapterRequestCollector();
            var adapter = new CharacterActionTrackAdapter(motionSink: collector);

            CharacterActionAdapterResult result = adapter.AdaptMany(runner.DrainEvents(), CreateContext(), new CharacterActionDispatchBuffer());

            Assert.IsTrue(result.Accepted);
            Assert.AreEqual(3, result.MotionRequestCount);
            Assert.AreEqual(CharacterActionTrackLifecyclePhase.Enter, collector.MotionRequests[0].Metadata.LifecyclePhase);
            Assert.AreEqual(CharacterActionTrackLifecyclePhase.Active, collector.MotionRequests[1].Metadata.LifecyclePhase);
            Assert.AreEqual(CharacterActionTrackLifecyclePhase.Exit, collector.MotionRequests[2].Metadata.LifecyclePhase);
            Assert.AreEqual(CharacterActionTrackEventKind.SetControlScale, collector.MotionRequests[1].EventKind);
            Assert.AreEqual(0.25f, collector.MotionRequests[1].MovementControlScale, 0.0001f);
            Assert.AreEqual(0.75f, collector.MotionRequests[1].RotationControlScale, 0.0001f);
            Assert.AreEqual(CharacterTestNumericIds.Field_motion_control, collector.MotionRequests[1].Metadata.StableEventId);
        }

        [Test]
        public void Adapter_RoutesCollisionProfileMotionRequestPayload()
        {
            CharacterActionConfig action = CreateAction(
                motionTrack: new MotionTrackConfig(false, new[]
                {
                    new MotionTrackEvent(
                        0,
                        CharacterActionTrackEventKind.SetCollisionProfile,
                        CharacterMovementMode.Idle,
                        stableEventId: CharacterTestNumericIds.Field_death_collision_corpse,
                        collisionProfileId: CharacterTestBulkIds.Id790019,
                        collisionRestorePolicy: CharacterActionCollisionProfileRestorePolicy.None,
                        collisionTransitionFrames: 0),
                }));
            CharacterActionRunner runner = StartRunner(action);
            var collector = new CharacterActionAdapterRequestCollector();
            var adapter = new CharacterActionTrackAdapter(motionSink: collector);

            CharacterActionAdapterResult result = adapter.AdaptMany(runner.DrainEvents(), CreateContext(), new CharacterActionDispatchBuffer());

            Assert.IsTrue(result.Accepted);
            Assert.AreEqual(3, result.MotionRequestCount);
            Assert.AreEqual(CharacterActionTrackLifecyclePhase.Enter, collector.MotionRequests[0].Metadata.LifecyclePhase);
            Assert.AreEqual(CharacterActionTrackLifecyclePhase.Active, collector.MotionRequests[1].Metadata.LifecyclePhase);
            Assert.AreEqual(CharacterActionTrackLifecyclePhase.Exit, collector.MotionRequests[2].Metadata.LifecyclePhase);
            CharacterActionMotionRequest request = collector.MotionRequests[1];
            Assert.AreEqual(CharacterActionTrackEventKind.SetCollisionProfile, request.EventKind);
            Assert.AreEqual("corpse", request.CollisionProfileId);
            Assert.AreEqual(CharacterActionCollisionProfileRestorePolicy.None, request.CollisionRestorePolicy);
            Assert.AreEqual(0, request.CollisionTransitionFrames);
            Assert.AreEqual(CharacterTestNumericIds.Field_death_collision_corpse, request.Metadata.StableEventId);
        }

        [Test]
        public void Adapter_RoutesApplyImpulseMotionVectorSpace()
        {
            CharacterActionConfig action = CreateAction(
                motionTrack: new MotionTrackConfig(false, new[]
                {
                    new MotionTrackEvent(
                        0,
                        CharacterActionTrackEventKind.ApplyImpulse,
                        CharacterMovementMode.Airborne,
                        z: 3f,
                        stableEventId: CharacterTestNumericIds.Field_motion_forward,
                        space: CharacterActionMotionVectorSpace.Local),
                }));
            CharacterActionRunner runner = StartRunner(action);
            var collector = new CharacterActionAdapterRequestCollector();
            var adapter = new CharacterActionTrackAdapter(motionSink: collector);

            CharacterActionAdapterResult result = adapter.AdaptMany(runner.DrainEvents(), CreateContext(), new CharacterActionDispatchBuffer());

            Assert.IsTrue(result.Accepted);
            Assert.AreEqual(1, result.MotionRequestCount);
            Assert.AreEqual(CharacterActionTrackEventKind.ApplyImpulse, collector.MotionRequests[0].EventKind);
            Assert.AreEqual(CharacterActionMotionVectorSpace.Local, collector.MotionRequests[0].MotionVectorSpace);
            Assert.AreEqual(3f, collector.MotionRequests[0].Z, 0.0001f);
        }

        [Test]
        public void Adapter_RoutesDebugTrackDispatchToDebugRequestRecords()
        {
            CharacterActionConfig action = CreateAction(
                debugTrack: new DebugTrackConfig(new[]
                {
                    new DebugTrackEvent(
                        1,
                        CharacterActionTrackEventKind.EmitDebugMarker,
                        "debug.light.active",
                        CharacterTestBulkIds.Id790020),
                }));
            CharacterActionRunner runner = StartRunner(action);
            CharacterActionRunnerOperationResult tick = runner.Tick();
            var collector = new CharacterActionAdapterRequestCollector();
            var adapter = new CharacterActionTrackAdapter(debugSink: collector);

            CharacterActionAdapterResult result = adapter.AdaptMany(tick.Events, CreateContext(), new CharacterActionDispatchBuffer());

            Assert.IsTrue(result.Accepted);
            Assert.AreEqual(1, result.DebugRequestCount);
            Assert.AreEqual(1, collector.DebugRequests.Count);
            Assert.AreEqual(CharacterActionTrackEventKind.EmitDebugMarker, collector.DebugRequests[0].EventKind);
            Assert.AreEqual("debug.light.active", collector.DebugRequests[0].MarkerId);
            Assert.AreEqual(CharacterTestNumericIds.Assert_debug_active, collector.DebugRequests[0].Metadata.StableEventId);
            Assert.AreEqual(new RuntimeFrame(42), collector.DebugRequests[0].Metadata.Frame);
            Assert.AreEqual(1, collector.DebugRequests[0].Metadata.LocalFrame);
        }

        [Test]
        public void Adapter_SinkFailureEmitsDiagnosticWithoutCountingRequest()
        {
            CharacterActionConfig action = CreateAction(
                motionTrack: new MotionTrackConfig(false, new[]
                {
                    new MotionTrackEvent(
                        0,
                        CharacterActionTrackEventKind.LockMovement,
                        CharacterMovementMode.ControlLocked,
                        stableEventId: CharacterTestNumericIds.Field_motion_lock,
                        endFrame: 0),
                }));
            CharacterActionRunner runner = StartRunner(action);
            var adapter = new CharacterActionTrackAdapter(motionSink: new ThrowingMotionSink());

            CharacterActionAdapterResult result = adapter.AdaptMany(runner.DrainEvents(), CreateContext(), new CharacterActionDispatchBuffer());

            Assert.IsFalse(result.Accepted);
            Assert.AreEqual(0, result.MotionRequestCount);
            AssertHasDiagnostic(result.Diagnostics, CharacterActionDiagnosticCodes.AdapterSinkFailure);
        }

        [Test]
        public void Adapter_RecordsPressureOnlyReactionRequestSeam()
        {
            CharacterReactionContext context = CharacterReactionContextBuilder.FromCohesionBreak(
                new CohesionBreakEvent(
                    new RuntimeFrame(9),
                    new GameplayEntityId(7, 1),
                    PressureBand.Critical,
                    previousValue: 90,
                    currentScatterPressure: 100,
                    maxScatterPressure: 100,
                    delta: 10,
                    traceId: CharacterTestRuntimeTraceIds.TracePressure)).Context;
            var collector = new CharacterActionAdapterRequestCollector();
            var adapter = new CharacterActionTrackAdapter(pressureOnlyReactionSink: collector);

            CharacterActionAdapterResult result = adapter.SubmitPressureOnlyReaction(
                context,
                new CharacterActionDispatchBuffer(),
                CharacterTestActionIds.Action_cohesion_break_react);

            Assert.IsTrue(result.Accepted);
            Assert.AreEqual(1, result.PressureOnlyReactionRequestCount);
            Assert.AreEqual(1, collector.PressureOnlyReactionRequests.Count);
            Assert.AreEqual(CharacterReactionContextCompleteness.PressureOnly, collector.PressureOnlyReactionRequests[0].Context.Completeness);
            Assert.AreEqual(
                CharacterTestActionIds.Action_cohesion_break_react,
                collector.PressureOnlyReactionRequests[0].RequestedActionId);
            Assert.IsFalse(collector.PressureOnlyReactionRequests[0].Context.HasFullHitContext);
        }

        [Test]
        public void PresentationAdapter_RoutesAnimationTrackDispatchToAnimationRequestRecords()
        {
            CharacterActionConfig action = CreateAction(
                animationTrack: new AnimationTrackConfig(new[]
                {
                    new AnimationTrackEvent(
                        1,
                        CharacterActionTrackEventKind.PlayAnimation,
                        "anim.light.play",
                        stableEventId: CharacterTestNumericIds.Field_anim_play,
                        playbackSpeed: 1.25f,
                        animationClipResourceKey: "art.character.skeleton.animation.light",
                        animationStartOffsetFrames: 6,
                        animationEndOffsetFrames: 3),
                    new AnimationTrackEvent(
                        1,
                        CharacterActionTrackEventKind.CrossFadeAnimation,
                        "anim.light.crossfade",
                        transitionSeconds: 0.12f,
                        stableEventId: CharacterTestNumericIds.Field_anim_crossfade),
                    new AnimationTrackEvent(
                        1,
                        CharacterActionTrackEventKind.SetAnimationBlend,
                        "blend.locomotion",
                        transitionSeconds: 0.08f,
                        stableEventId: CharacterTestNumericIds.Field_anim_blend),
                }));
            CharacterActionRunner runner = StartRunner(action);
            CharacterActionRunnerOperationResult tick = runner.Tick();
            var collector = new CharacterActionPresentationRequestCollector();
            var adapter = new CharacterActionPresentationTrackAdapter(collector);

            CharacterActionPresentationAdapterResult result = adapter.AdaptMany(
                tick.Events,
                CreatePresentationContext(),
                new CharacterActionDispatchBuffer());

            Assert.IsTrue(result.Accepted);
            Assert.AreEqual(3, result.AnimationRequestCount);
            Assert.AreEqual(3, collector.AnimationRequests.Count);
            Assert.AreEqual(CharacterActionTrackEventKind.PlayAnimation, collector.AnimationRequests[0].EventKind);
            Assert.AreEqual("anim.light.play", collector.AnimationRequests[0].ActionKey);
            Assert.AreEqual("art.character.skeleton.animation.light", collector.AnimationRequests[0].ClipResourceKey);
            Assert.AreEqual("art.character.skeleton.animation.light", collector.AnimationRequests[0].EffectiveAnimationKey);
            Assert.AreEqual(1.25f, collector.AnimationRequests[0].PlaybackSpeed);
            Assert.AreEqual(6, collector.AnimationRequests[0].StartOffsetFrames);
            Assert.AreEqual(3, collector.AnimationRequests[0].EndOffsetFrames);
            Assert.AreEqual(0.1f, collector.AnimationRequests[0].StartOffsetSeconds, 0.0001f);
            Assert.AreEqual("actor.skeleton", collector.AnimationRequests[0].TargetActorId);
            Assert.AreEqual("upper_body", collector.AnimationRequests[0].LayerId);
            Assert.AreEqual(CharacterActionTrackEventKind.CrossFadeAnimation, collector.AnimationRequests[1].EventKind);
            Assert.AreEqual(0.12f, collector.AnimationRequests[1].TransitionSeconds);
            Assert.AreEqual(CharacterActionTrackEventKind.SetAnimationBlend, collector.AnimationRequests[2].EventKind);
            Assert.AreEqual("blend.locomotion", collector.AnimationRequests[2].ActionKey);
            Assert.AreEqual(new RuntimeFrame(42), collector.AnimationRequests[2].Metadata.Frame);
            Assert.AreEqual(CharacterTestRuntimeTraceIds.TraceLightAttack, collector.AnimationRequests[2].Metadata.TraceId);
        }

        [Test]
        public void PresentationAdapter_RoutesPresentationTrackDispatchToAudioVfxCameraAndUiRequestRecords()
        {
            CharacterActionConfig action = CreateAction(
                presentationTrack: new PresentationTrackConfig(new[]
                {
                    new PresentationTrackEvent(
                        1,
                        CharacterActionTrackEventKind.PlayAudioCue,
                        cueId: CharacterTestNumericIds.Field_sfx_light,
                        stableEventId: CharacterTestNumericIds.Field_audio_light),
                    new PresentationTrackEvent(
                        1,
                        CharacterActionTrackEventKind.SpawnVisualCue,
                        resourceKey: "vfx.slash:GameObject",
                        stableEventId: CharacterTestNumericIds.Field_vfx_slash,
                        endFrame: 4,
                        vfxConfig: new CharacterActionVfxEventConfig(
                            attachment: new MxAttachmentTransform(
                                MxAttachmentMode.FollowSocket,
                                "mainHand",
                                MxTransformSpace.Local,
                                new MxTransformOffset(
                                    positionX: 0.25f,
                                    positionY: 1.5f,
                                    positionZ: -0.5f,
                                    rotationEulerY: 90f,
                                    scaleX: 2f,
                                    scaleY: 2f,
                                    scaleZ: 2f)),
                            playbackMode: CharacterActionVfxPlaybackMode.StopAtEventEnd,
                            playbackRate: 0.5f,
                            scaleMultiplier: 1.25f,
                            startOffsetFrames: 2,
                            endOffsetFrames: 1,
                            stopPolicy: CharacterActionVfxStopPolicy.Dissipate,
                            parameter0: 7,
                            parameter1: 8,
                            parameter2: 9,
                            parameter3: 10)),
                    new PresentationTrackEvent(
                        1,
                        CharacterActionTrackEventKind.CameraImpulse,
                        cueId: CharacterTestNumericIds.Field_camera_hit,
                        resourceKey: "camera.hit_payload:TextAsset",
                        stableEventId: CharacterTestNumericIds.Field_camera_hit),
                    new PresentationTrackEvent(
                        1,
                        CharacterActionTrackEventKind.UiFeedback,
                        cueId: CharacterTestNumericIds.Field_ui_hit_confirm,
                        resourceKey: "ui.hit_payload:TextAsset",
                        stableEventId: CharacterTestNumericIds.Field_ui_hit),
                }));
            CharacterActionRunner runner = StartRunner(action);
            CharacterActionRunnerOperationResult tick = runner.Tick();
            var collector = new CharacterActionPresentationRequestCollector();
            var adapter = new CharacterActionPresentationTrackAdapter(
                audioCueSink: collector,
                vfxSink: collector,
                cameraSink: collector,
                uiFeedbackSink: collector);

            CharacterActionPresentationAdapterResult result = adapter.AdaptMany(
                tick.Events,
                CreatePresentationContext(),
                new CharacterActionDispatchBuffer());

            Assert.IsTrue(result.Accepted);
            Assert.AreEqual(1, result.AudioCueRequestCount);
            Assert.AreEqual(1, result.VfxRequestCount);
            Assert.AreEqual(1, result.CameraRequestCount);
            Assert.AreEqual(1, result.UiFeedbackRequestCount);
            Assert.AreEqual(CharacterTestNumericIds.Field_sfx_light, collector.AudioCueRequests[0].CueId);
            Assert.AreEqual("vfx.slash:GameObject", collector.VfxRequests[0].ResourceKey);
            Assert.AreEqual(4, collector.VfxRequests[0].EndFrame);
            Assert.AreEqual(4, collector.VfxRequests[0].DurationFrames);
            Assert.AreEqual(4, collector.VfxRequests[0].EffectiveLifetimeFrames);
            Assert.AreEqual(MxAttachmentMode.FollowSocket, collector.VfxRequests[0].VfxConfig.Attachment.Mode);
            Assert.AreEqual("mainHand", collector.VfxRequests[0].VfxConfig.Attachment.SocketId);
            Assert.AreEqual(0.25f, collector.VfxRequests[0].VfxConfig.Attachment.Transform.PositionX);
            Assert.AreEqual(90f, collector.VfxRequests[0].VfxConfig.Attachment.Transform.RotationEulerY);
            Assert.AreEqual(2f, collector.VfxRequests[0].VfxConfig.Attachment.Transform.ScaleX);
            Assert.AreEqual(MxTransformSpace.Local, collector.VfxRequests[0].VfxConfig.Attachment.OffsetSpace);
            Assert.AreEqual(CharacterActionVfxPlaybackMode.StopAtEventEnd, collector.VfxRequests[0].VfxConfig.PlaybackMode);
            Assert.AreEqual(0.5f, collector.VfxRequests[0].VfxConfig.PlaybackRate);
            Assert.AreEqual(1.25f, collector.VfxRequests[0].VfxConfig.ScaleMultiplier);
            Assert.AreEqual(2, collector.VfxRequests[0].VfxConfig.StartOffsetFrames);
            Assert.AreEqual(1, collector.VfxRequests[0].VfxConfig.EndOffsetFrames);
            Assert.AreEqual(CharacterActionVfxStopPolicy.Dissipate, collector.VfxRequests[0].VfxConfig.StopPolicy);
            Assert.AreEqual(7, collector.VfxRequests[0].VfxConfig.Parameter0);
            Assert.AreEqual(10, collector.VfxRequests[0].VfxConfig.Parameter3);
            Assert.AreEqual(CharacterTestNumericIds.Field_camera_hit, collector.CameraRequests[0].RequestId);
            Assert.AreEqual("camera.hit_payload:TextAsset", collector.CameraRequests[0].PayloadKey);
            Assert.AreEqual("ui.hit_confirm", collector.UiFeedbackRequests[0].FeedbackId);
            Assert.AreEqual("ui.hit_payload:TextAsset", collector.UiFeedbackRequests[0].PayloadKey);
            Assert.AreEqual("actor.skeleton", collector.CameraRequests[0].TargetActorId);
        }

        [Test]
        public void PresentationAdapter_MissingPayloadAndSinkFailureEmitDiagnosticsWithoutChangingActionAuthority()
        {
            CharacterActionConfig action = CreateAction(
                animationTrack: new AnimationTrackConfig(new[]
                {
                    new AnimationTrackEvent(0, CharacterActionTrackEventKind.PlayAnimation, stableEventId: CharacterTestNumericIds.Field_anim_missing),
                }),
                presentationTrack: new PresentationTrackConfig(new[]
                {
                    new PresentationTrackEvent(
                        0,
                        CharacterActionTrackEventKind.UiFeedback,
                        cueId: CharacterTestNumericIds.Field_ui_hit_confirm,
                        stableEventId: CharacterTestNumericIds.Field_ui_hit),
                }));
            CharacterActionRunner runner = StartRunner(action);
            CharacterActionDebugSnapshot before = runner.CreateDebugSnapshot();
            var collector = new CharacterActionPresentationRequestCollector();
            var adapter = new CharacterActionPresentationTrackAdapter(
                animationSink: collector,
                uiFeedbackSink: new ThrowingUiFeedbackSink());

            CharacterActionPresentationAdapterResult result = adapter.AdaptMany(
                runner.DrainEvents(),
                CreatePresentationContext(),
                new CharacterActionDispatchBuffer());
            CharacterActionDebugSnapshot after = runner.CreateDebugSnapshot();

            Assert.IsFalse(result.Accepted);
            Assert.AreEqual(0, result.AnimationRequestCount);
            Assert.AreEqual(0, result.UiFeedbackRequestCount);
            Assert.AreEqual(0, collector.AnimationRequests.Count);
            AssertHasDiagnostic(result.Diagnostics, CharacterActionDiagnosticCodes.AdapterPayloadMissing);
            AssertHasDiagnostic(result.Diagnostics, CharacterActionDiagnosticCodes.AdapterSinkFailure);
            Assert.AreEqual(before.ActiveActionId, after.ActiveActionId);
            Assert.AreEqual(before.State, after.State);
            Assert.AreEqual(before.LocalFrame, after.LocalFrame);
        }

        [Test]
        public void TrackAdapter_ReusedDispatchBufferDoesNotAllocateAfterWarmup()
        {
            CharacterActionConfig action = CreateAction(
                motionTrack: new MotionTrackConfig(
                    usesRootMotion: false,
                    events: new[]
                    {
                        new MotionTrackEvent(
                            0,
                            CharacterActionTrackEventKind.SetMovementMode,
                            CharacterMovementMode.Run,
                            stableEventId: CharacterTestNumericIds.Field_motion_noalloc)
                    }));
            CharacterActionRunnerEvent[] events = StartRunner(action).DrainEvents();
            var collector = new CharacterActionAdapterRequestCollector();
            var adapter = new CharacterActionTrackAdapter(motionSink: collector);
            var buffer = new CharacterActionDispatchBuffer(8);
            for (int i = 0; i < 8; i++)
            {
                Assert.IsTrue(adapter.AdaptMany(events, CreateContext(), buffer).Accepted);
                collector.Clear();
            }

            _ = GC.GetAllocatedBytesForCurrentThread();
            long before = GC.GetAllocatedBytesForCurrentThread();
            bool accepted = true;
            for (int i = 0; i < 32; i++)
            {
                accepted &= adapter.AdaptMany(events, CreateContext(), buffer).Accepted;
                collector.Clear();
            }
            long allocated = GC.GetAllocatedBytesForCurrentThread() - before;

            Assert.IsTrue(accepted);
            Assert.AreEqual(0L, allocated);
        }

        [Test]
        public void TrackAdapter_MissingSinkRejectionDoesNotAllocateAfterWarmup()
        {
            CharacterActionConfig action = CreateAction(
                motionTrack: new MotionTrackConfig(
                    usesRootMotion: false,
                    events: new[]
                    {
                        new MotionTrackEvent(
                            0,
                            CharacterActionTrackEventKind.SetMovementMode,
                            CharacterMovementMode.Run,
                            stableEventId: CharacterTestNumericIds.Field_motion_reject_noalloc)
                    }));
            CharacterActionRunnerEvent[] events = StartRunner(action).DrainEvents();
            var adapter = new CharacterActionTrackAdapter();
            var buffer = new CharacterActionDispatchBuffer(8);
            CharacterActionAdapterContext context = CreateContext();
            for (int i = 0; i < 8; i++)
                Assert.IsFalse(adapter.AdaptMany(events, context, buffer).Accepted);

            _ = GC.GetAllocatedBytesForCurrentThread();
            long before = GC.GetAllocatedBytesForCurrentThread();
            bool rejected = true;
            int diagnosticCount = 0;
            for (int i = 0; i < 32; i++)
            {
                CharacterActionAdapterResult result = adapter.AdaptMany(events, context, buffer);
                rejected &= !result.Accepted;
                diagnosticCount += result.DiagnosticCount;
            }
            long allocated = GC.GetAllocatedBytesForCurrentThread() - before;

            Assert.IsTrue(rejected);
            Assert.AreEqual(32, diagnosticCount);
            Assert.AreEqual(0L, allocated);
        }

        [Test]
        public void PresentationAdapter_ReusedDispatchBufferDoesNotAllocateAfterWarmup()
        {
            CharacterActionConfig action = CreateAction(
                animationTrack: new AnimationTrackConfig(new[]
                {
                    new AnimationTrackEvent(
                        0,
                        CharacterActionTrackEventKind.PlayAnimation,
                        "anim.noalloc",
                        stableEventId: CharacterTestNumericIds.Field_animation_noalloc)
                }));
            CharacterActionRunnerEvent[] events = StartRunner(action).DrainEvents();
            var collector = new CharacterActionPresentationRequestCollector();
            var adapter = new CharacterActionPresentationTrackAdapter(animationSink: collector);
            var buffer = new CharacterActionDispatchBuffer(8);
            for (int i = 0; i < 8; i++)
            {
                Assert.IsTrue(adapter.AdaptMany(events, CreatePresentationContext(), buffer).Accepted);
                collector.Clear();
            }

            _ = GC.GetAllocatedBytesForCurrentThread();
            long before = GC.GetAllocatedBytesForCurrentThread();
            bool accepted = true;
            for (int i = 0; i < 32; i++)
            {
                accepted &= adapter.AdaptMany(events, CreatePresentationContext(), buffer).Accepted;
                collector.Clear();
            }
            long allocated = GC.GetAllocatedBytesForCurrentThread() - before;

            Assert.IsTrue(accepted);
            Assert.AreEqual(0L, allocated);
        }

        [Test]
        public void PresentationAdapter_MissingSinkRejectionDoesNotAllocateAfterWarmup()
        {
            CharacterActionConfig action = CreateAction(
                animationTrack: new AnimationTrackConfig(new[]
                {
                    new AnimationTrackEvent(
                        0,
                        CharacterActionTrackEventKind.PlayAnimation,
                        "anim.reject.noalloc",
                        stableEventId: CharacterTestNumericIds.Field_animation_reject_noalloc)
                }));
            CharacterActionRunnerEvent[] events = StartRunner(action).DrainEvents();
            var adapter = new CharacterActionPresentationTrackAdapter();
            var buffer = new CharacterActionDispatchBuffer(8);
            CharacterActionPresentationAdapterContext context = CreatePresentationContext();
            for (int i = 0; i < 8; i++)
                Assert.IsFalse(adapter.AdaptMany(events, context, buffer).Accepted);

            _ = GC.GetAllocatedBytesForCurrentThread();
            long before = GC.GetAllocatedBytesForCurrentThread();
            bool rejected = true;
            int diagnosticCount = 0;
            for (int i = 0; i < 32; i++)
            {
                CharacterActionPresentationAdapterResult result = adapter.AdaptMany(events, context, buffer);
                rejected &= !result.Accepted;
                diagnosticCount += result.DiagnosticCount;
            }
            long allocated = GC.GetAllocatedBytesForCurrentThread() - before;

            Assert.IsTrue(rejected);
            Assert.AreEqual(32, diagnosticCount);
            Assert.AreEqual(0L, allocated);
        }

        private static CharacterActionRunner StartRunner(CharacterActionConfig action)
        {
            var runner = new CharacterActionRunner();
            CharacterActionRunnerOperationResult start = runner.Start(
                CharacterActionResolveResult.Success(CreatePlan(action)),
                CharacterActionRunnerActionDefinition.FromConfig(action));
            Assert.IsTrue(start.Accepted);
            return runner;
        }

        private static CharacterActionDamagePayload CreateLightDamagePayload()
        {
            return new CharacterActionDamagePayload(
                CharacterTestBulkIds.Id790011,
                CharacterTestBulkIds.Id790023,
                6,
                new[] { new DamageAttributeScale(2, 0.65f) },
                34,
                CharacterActionDamageIdentityAuthoring.ExistingTags(new[] { "melee", "weapon", "light", "scatter" }),
                CharacterTestBulkIds.Id790012);
        }

        private static CharacterActionDamagePayload CreateAreaDamagePayload()
        {
            return new CharacterActionDamagePayload(
                CharacterTestBulkIds.Id790013,
                CharacterTestBulkIds.Id790024,
                6,
                new DamageAttributeScale[0],
                12,
                CharacterActionDamageIdentityAuthoring.ExistingTags(new[] { "area", "shockwave", "scatter" }),
                CharacterTestBulkIds.Id790014);
        }

        private static CharacterActionPlan CreatePlan(CharacterActionConfig action)
        {
            return new CharacterActionPlan(
                planId: action.ActionId.Value,
                actionId: action.ActionId,
                category: action.Category,
                priority: action.Priority,
                durationFrames: action.DurationFrames.Value,
                phases: action.Phases,
                tracks: CharacterActionTrackPlan.FromConfig(action),
                traceId: new RuntimeTraceIdentity(620001, 0, action.ActionId.Value, 0));
        }

        private static CharacterActionConfig CreateAction(
            MotionTrackConfig motionTrack = null,
            CombatTrackConfig combatTrack = null,
            GameplayTrackConfig gameplayTrack = null,
            AnimationTrackConfig animationTrack = null,
            PresentationTrackConfig presentationTrack = null,
            DebugTrackConfig debugTrack = null)
        {
            return new CharacterActionConfig(
                actionId: new CharacterActionId(100),
                displayName: "Light Attack",
                category: CharacterActionCategory.BasicAttack,
                timelineAuthority: CharacterActionTimelineAuthority.CharacterAuthored,
                tags: null,
                priority: 10,
                durationFrames: 3,
                requirements: null,
                phases: new[]
                {
                    new CharacterActionPhase(CharacterActionPhaseKind.Startup, 0, 0),
                    new CharacterActionPhase(CharacterActionPhaseKind.Active, 1, 1),
                    new CharacterActionPhase(CharacterActionPhaseKind.Recovery, 2, 2),
                },
                cancelRules: null,
                interruptRules: null,
                motionTrack: motionTrack,
                combatTrack: combatTrack,
                gameplayTrack: gameplayTrack,
                animationTrack: animationTrack,
                presentationTrack: presentationTrack,
                debugTrack: debugTrack);
        }

        private static CharacterActionAdapterContext CreateContext()
        {
            return new CharacterActionAdapterContext(
                new RuntimeFrame(42),
                new GameplayEntityId(7, 1),
                new CombatEntityId(11),
                new CombatBodyId(13),
                sourceId: 5);
        }

        private static CharacterActionPresentationAdapterContext CreatePresentationContext()
        {
            return new CharacterActionPresentationAdapterContext(
                new RuntimeFrame(42),
                "actor.skeleton",
                sourceId: 5,
                animationLayerId: "upper_body");
        }

        private static void AssertHasDiagnostic(CharacterActionDiagnostic[] diagnostics, string code)
        {
            for (int i = 0; i < diagnostics.Length; i++)
            {
                if (diagnostics[i].Code == code)
                    return;
            }

            Assert.Fail("Expected diagnostic code " + code + ".");
        }

        private sealed class ThrowingMotionSink : ICharacterActionMotionRequestSink
        {
            public CharacterActionAdapterSinkResult SubmitMotionRequest(CharacterActionMotionRequest request)
            {
                throw new System.InvalidOperationException("motion backend unavailable");
            }
        }

        private sealed class ThrowingUiFeedbackSink : ICharacterActionUiFeedbackRequestSink
        {
            public CharacterActionAdapterSinkResult SubmitUiFeedbackRequest(CharacterActionUiFeedbackRequest request)
            {
                throw new System.InvalidOperationException("ui feedback backend unavailable");
            }
        }
    }
}
