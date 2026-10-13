using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using MxFramework.Animation;
using MxFramework.Audio;
using MxFramework.Camera;
using MxFramework.Camera.Unity;
using MxFramework.CharacterAction;
using MxFramework.CharacterRuntimeSpawn;
using MxFramework.CharacterRuntimeSpawn.Unity;
using MxFramework.Combat.Core;
using MxFramework.Combat.Damage;
using MxFramework.Combat.Hit;
using MxFramework.Combat.Motion;
using MxFramework.Combat.Physics;
using MxFramework.Combat.Projectiles;
using MxFramework.Combat.Projectiles.Unity;
using MxFramework.Config;
using MxFramework.Core.Math;
using MxFramework.DebugUI;
using MxFramework.Diagnostics;
using MxFramework.Game.CharacterTest;
using MxFramework.Gameplay;
using MxFramework.Input;
using MxFramework.Network;
using MxFramework.Rendering;
using MxFramework.Resources;
using MxFramework.Resources.Unity;
using MxFramework.Runtime;
using MxFramework.Runtime.Unity;
using MxFramework.Settings;
using MxFramework.Settings.Input;
using MxFramework.Story;
using MxFramework.Story.Runtime;
using MxFramework.UI;
using MxFramework.UI.ConfigBridge;
using MxFramework.UI.FairyGui;
using MxFramework.Vfx;
using MxFramework.Vfx.Unity;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using Fgui = global::FairyGUI;

namespace MxFramework.Tests.Game.CharacterTest
{
    public sealed partial class CharacterTestIntegrationContractTests
    {
        [TestCase(0f, 0f)]
        [TestCase(0.5f, 0.5f)]
        [TestCase(1f, 1f)]
        [TestCase(2f, 3f)]
        [TestCase(10f, 10f)]
        public void CharacterMotion_ControlKeysScaleActiveSpeedAndYaw(float movementScale, float rotationScale)
        {
            var world = CreateRuntimeWorldWithLocalPlayer(out var entity);
            var motion = new CharacterTestCharacterMotionSystem();
            motion.SetMovementProfile(CreateMovementRuntime(runSpeed: 4f, acceleration: 10000f, deceleration: 10000f, turnSpeed: 360f));
            entity.SetAction(CharacterTestCharacterActionComponent.None);
            entity.SetInput(new CharacterTestCharacterInputComponent(Vector2.right, jumpPressed: false, jumpHeld: false));
            entity.EnqueueMotionRequest(CharacterTestCharacterMotionRequest.ControlScale(movementScale, rotationScale,
                CharacterMovementMode.Idle, CharacterTestCharacterActionConfigSource.LightAttackActionId, 9001));
            motion.Tick(world, CharacterTestDeterministicUnits.DefaultTickRate);
            Assert.AreEqual(4f * movementScale, entity.HorizontalVelocity.magnitude, 0.002f);
            Assert.AreEqual(6f * rotationScale, entity.Transform.YawDegrees, 0.002f);
        }

        [Test]
        public void CharacterActionRuntime_PublishedLightAttackCompletesWithAuthoredMotionEvents()
        {
            var world = CreateRuntimeWorldWithLocalPlayer(out var entity);
            var runtime = CharacterTestCharacterActionConfigSource.Load();
            runtime.BindRuntimeWorld(world);
            Assert.IsTrue(runtime.IsReady);
            Assert.IsTrue(runtime.TryStartIntent(entity,
                CharacterTestCharacterActionConfigSource.LightAttackIntentId, new RuntimeFrame(0)));
            Assert.DoesNotThrow(() =>
            {
                for (int i = 0; i < 512 && entity.Action.ActiveActionId.IsValid; i++)
                {
                    runtime.TickEntity(entity);
                }
            });
            Assert.AreEqual(CharacterActionId.None, entity.Action.ActiveActionId);
            Assert.AreEqual(1f, entity.Action.MovementControlScale);
            Assert.AreEqual(1f, entity.Action.RotationControlScale);
        }

        [TestCase(false, 0f)]
        [TestCase(false, 2f)]
        [TestCase(false, 10f)]
        [TestCase(true, 0f)]
        public void CharacterActionProjection_NewStateEventsUsePersistedOrderAndRestoreLowerContribution(bool collision, float upperScale)
        {
            var world = CreateRuntimeWorldWithLocalPlayer(out var entity);
            var writer = new CharacterTestCharacterActionProjectionWriter();
            writer.BindRuntimeWorld(world);
            var kind = collision
                ? CharacterActionTrackEventKind.SetCollisionProfile
                : CharacterActionTrackEventKind.SetControlScale;
            // New authored identities need not exist in the compiled legacy catalog.
            // Reverse numeric IDs and dispatch order so neither can substitute for persisted priority.
            var lower = new CharacterActionTrackDispatchEvent(
                CharacterActionTrackKind.Motion, kind, frame: 0, stableEventId: int.MaxValue,
                endFrame: 3, movementControlScale: 0.5f, rotationControlScale: 0.75f,
                collisionProfileId: CharacterTestColliderPostureComponent.LowProfileId, stableEventOrder: 1);
            var upper = new CharacterActionTrackDispatchEvent(
                CharacterActionTrackKind.Motion, kind, frame: 0, stableEventId: int.MaxValue - 1,
                endFrame: 2, movementControlScale: upperScale, rotationControlScale: upperScale,
                collisionProfileId: CharacterTestColliderPostureComponent.ReviveLowProfileId, stableEventOrder: 2);
            var buffer = new CharacterActionRunnerEventBuffer();

            void Apply(CharacterActionTrackDispatchEvent dispatch, CharacterActionTrackLifecyclePhase phase)
            {
                var actionId = CharacterTestCharacterActionConfigSource.LightAttackActionId;
                var lifecycle = new CharacterActionTrackLifecycleEvent(
                    1, actionId, 0, phase, CharacterActionTrackLifecycleBoundary.BeforeFrame,
                    phase == CharacterActionTrackLifecyclePhase.Exit
                        ? CharacterActionTrackExitReason.ActionFinished : CharacterActionTrackExitReason.None,
                    dispatch);
                buffer.Clear();
                buffer.MutableEvents.Add(new CharacterActionRunnerEvent(
                    CharacterActionRunnerEventKind.TrackLifecycle, 1, 1, actionId,
                    CharacterActionInstanceState.Running, 0,
                    CharacterActionPhaseKind.None, CharacterActionPhaseKind.None,
                    dispatch, trackLifecycle: lifecycle));
                writer.ApplyOperation(world.CharacterComponents, entity.Id,
                    new CharacterActionExecutionResult(CharacterActionExecutionStatus.NoOp, null, buffer, null));
            }

            Apply(upper, CharacterActionTrackLifecyclePhase.Active);
            Apply(lower, CharacterActionTrackLifecyclePhase.Active);
            if (collision)
                Assert.AreEqual(CharacterTestColliderPostureComponent.ReviveLowProfileId, entity.ColliderPosture.ActionProfileId);
            else
            {
                Assert.AreEqual(upperScale, entity.Action.MovementControlScale);
                Assert.AreEqual(upperScale, entity.Action.RotationControlScale);
            }

            Apply(upper, CharacterActionTrackLifecyclePhase.Exit);
            if (collision)
                Assert.AreEqual(CharacterTestColliderPostureComponent.LowProfileId, entity.ColliderPosture.ActionProfileId);
            else
            {
                Assert.AreEqual(0.5f, entity.Action.MovementControlScale);
                Assert.AreEqual(0.75f, entity.Action.RotationControlScale);
            }

            Apply(lower, CharacterActionTrackLifecyclePhase.Exit);
            if (collision)
                Assert.IsFalse(entity.ColliderPosture.HasActionOverride);
            else
            {
                Assert.AreEqual(1f, entity.Action.MovementControlScale);
                Assert.AreEqual(1f, entity.Action.RotationControlScale);
            }
        }

[Test]
        public void AnimationSystem_ConsumesBufferedActionAnimationRequest_WhenActionFrameRequestWasCleared()
        {
            CharacterTestCharacterRuntimeWorld world =
                CreateRuntimeWorldWithLocalPlayer(out CharacterTestCharacterEntity entity);
            CharacterTestCharacterActionRuntime runtime = CreateAnimationRequestActionRuntime();
            var frame = new RuntimeFrame(12);

            Assert.IsTrue(runtime.TryStartIntent(
                entity,
                CharacterTestCharacterActionConfigSource.LightAttackIntentId,
                frame));
            Assert.AreEqual("anim.test.light", entity.Action.AnimationActionKeyThisFrame);
            CharacterTestCharacterAnimationRequestComponent request =
                world.CharacterComponents.GetAnimationRequest(entity.Id);
            Assert.IsTrue(request.IsValid);
            Assert.AreEqual(CharacterTestCharacterAnimationRequestStatus.Pending, request.Status);
            Assert.AreEqual(frame.Value, request.Frame);
            Assert.AreEqual(CharacterTestCharacterActionConfigSource.LightAttackActionId, request.ActionId);
            Assert.AreEqual("anim.test.light", request.AnimationKey);

            entity.SetAction(entity.Action.ClearFrameRequests());
            world.CharacterComponents.ClearAnimationRequestsBefore(frame.Value + 1);
            new CharacterTestCharacterAnimationSystem().Tick(world, new RuntimeFrame(frame.Value + 1));

            Assert.AreEqual("anim.test.light", entity.Animation.ActionKey);
            Assert.AreEqual(request.TransitionSeconds, entity.Animation.ActionTransitionSeconds, 0.0001f);
            Assert.Greater(entity.Animation.ActionHoldSeconds, 0f);
            CharacterTestCharacterAnimationRequestComponent emitted =
                world.CharacterComponents.GetAnimationRequest(entity.Id);
            Assert.IsTrue(emitted.IsEmitted);
            Assert.AreEqual(frame.Value + 1, emitted.EmittedFrame);

            world.CharacterComponents.ClearAnimationRequestsAtOrBefore(frame.Value + 1);
            Assert.IsFalse(world.CharacterComponents.GetAnimationRequest(entity.Id).IsValid);

            new CharacterTestCharacterAnimationSystem().Tick(world, new RuntimeFrame(frame.Value + 2));

            Assert.AreEqual(string.Empty, entity.Animation.ActionKey);
        }

[Test]
        public void AnimationSystem_EmitsBufferedFollowUpAnimation_WhenTargetDiesDuringFirstAttack()
        {
            CharacterTestCharacterEntity attacker;
            CharacterTestCharacterEntity target;
            CharacterTestCharacterRuntimeWorld world = CreateRuntimeWorldWithAttackerAndTarget(
                targetTeamId: 2,
                out attacker,
                out target);
            var traceRecorder = CharacterTestTraceRecorder.CreateInMemory(capacity: 64);
            var system = new CharacterTestCharacterActionSystem(NullRuntimeLogger.Instance);
            system.SetTraceRecorder(traceRecorder);
            var animationSystem = new CharacterTestCharacterAnimationSystem();
            animationSystem.SetTraceRecorder(traceRecorder);
            long frame = 0L;

            QueueGameplayIntent(attacker, CharacterTestGameplayIntentKind.AttackPrimary, new RuntimeFrame(frame));
            system.Tick(world, new RuntimeFrame(frame));
            animationSystem.Tick(world, new RuntimeFrame(frame));
            world.CharacterComponents.ClearAnimationRequestsAtOrBefore(frame);

            Assert.AreEqual(
                CharacterTestCharacterActionConfigSource.LightAttackActionId,
                attacker.Action.ActiveActionId);
            Assert.AreEqual(
                CharacterTestCharacterActionConfigSource.LightAttackAnimationClipResourceKey,
                attacker.Animation.ActionKey);

            AdvanceActionToLocalFrame(system, world, attacker, ref frame, 31);
            target.BeginDeath(frame, attacker.Id.Value, traceId: 77);
            Assert.AreEqual(CharacterTestCharacterLifeState.Dying, target.CombatState.LifeState);

            frame++;
            QueueGameplayIntent(attacker, CharacterTestGameplayIntentKind.AttackPrimary, new RuntimeFrame(frame));
            system.Tick(world, new RuntimeFrame(frame));

            Assert.AreEqual(
                CharacterTestCharacterActionConfigSource.LightAttackActionId,
                attacker.Action.ActiveActionId,
                "reject=" + attacker.Action.LastRejectReason);
            Assert.AreEqual(CharacterActionDiagnosticCodes.ActionSlotBlocked, attacker.Action.LastRejectReason);
            Assert.AreEqual(1, attacker.PendingGameplayIntentCount);

            AdvanceUntilActiveAction(
                system,
                world,
                attacker,
                ref frame,
                LightAttackSecondActionId,
                maxFrames: 32);
            Assert.AreEqual(0, attacker.Action.LocalFrame);
            Assert.AreEqual(LightAttackSecondAnimationClipResourceKey, attacker.Action.AnimationActionKeyThisFrame);

            attacker.SetAction(attacker.Action.ClearFrameRequests());
            long animationFrame = frame + 1;
            world.CharacterComponents.ClearAnimationRequestsBefore(animationFrame);
            animationSystem.Tick(world, new RuntimeFrame(animationFrame));

            Assert.AreEqual(LightAttackSecondAnimationClipResourceKey, attacker.Animation.ActionKey);
            Assert.That(
                traceRecorder.RecentEvents,
                Has.Some.Matches<CharacterTestTraceEvent>(
                    evt => evt.Category == "Attack"
                        && evt.Stage == "animation"
                        && evt.EntityId == attacker.Id.Value
                        && evt.Message.Contains("action=light_attack_2")
                        && evt.Message.Contains(LightAttackSecondAnimationClipResourceKey)));

            frame = animationFrame;
            AdvanceActionToLocalFrame(system, world, attacker, ref frame, 18);
            Assert.AreEqual(1f, attacker.Action.MovementControlScale, 0.0001f);
            Assert.AreEqual(1f, attacker.Action.RotationControlScale, 0.0001f);
        }

[Test]
        public void CharacterActionConfigSource_LightAttackControlScaleReleasesInGeneratedAndEmbedded()
        {
            CharacterTestCharacterActionRuntime external = CharacterTestCharacterActionConfigSource.Load();
            CharacterTestCharacterActionRuntime embedded = CharacterTestCharacterActionConfigSource.Load(
                CharacterTestCharacterActionConfigSourceOptions.CreateLegacyFixtureFallback());

            AssertLightAttackControlRelease(external, CharacterTestCharacterActionConfigSourceKind.ExternalJson);
            AssertLightAttackControlRelease(embedded, CharacterTestCharacterActionConfigSourceKind.EmbeddedFallback);
            AssertJumpAnimationConfigConsistency(external, embedded);
        }

[Test]
        public void CharacterActionRuntime_DefaultLightAttackControlScaleRestoresAtReleaseFrame()
        {
            CharacterTestCharacterRuntimeWorld world =
                CreateRuntimeWorldWithLocalPlayer(out CharacterTestCharacterEntity entity);
            CharacterTestCharacterActionRuntime runtime = CharacterTestCharacterActionConfigSource.Load();
            var frame = new RuntimeFrame(0);

            Assert.IsTrue(runtime.TryStartIntent(
                entity,
                CharacterTestCharacterActionConfigSource.LightAttackIntentId,
                frame));
            Assert.AreEqual(
                CharacterTestCharacterActionConfigSource.LightAttackActionId,
                entity.Action.ActiveActionId,
                "reject=" + entity.Action.LastRejectReason);
            Assert.AreEqual(0f, entity.Action.MovementControlScale, 0.0001f);
            Assert.AreEqual(1f, entity.Action.RotationControlScale, 0.0001f);

            for (int i = 0; i < 17; i++)
                runtime.TickEntity(entity);

            Assert.AreEqual(17, entity.Action.LocalFrame);
            Assert.AreEqual(0f, entity.Action.MovementControlScale, 0.0001f);
            Assert.AreEqual(0f, entity.Action.RotationControlScale, 0.0001f);

            runtime.TickEntity(entity);

            Assert.AreEqual(18, entity.Action.LocalFrame);
            Assert.AreEqual(1f, entity.Action.MovementControlScale, 0.0001f);
            Assert.AreEqual(1f, entity.Action.RotationControlScale, 0.0001f);
            Assert.AreEqual(CharacterTestCharacterActionConfigSource.LightAttackActionId, entity.Action.ActiveActionId);

            entity.ClearCombatRequests();
            while (entity.Action.LocalFrame < LightAttackHitTraceStartFrame)
                runtime.TickEntity(entity);

            Assert.AreEqual(LightAttackHitTraceStartFrame, entity.Action.LocalFrame);
            Assert.AreEqual(CharacterTestCharacterActionConfigSource.LightAttackActionId, entity.Action.ActiveActionId);
            Assert.AreEqual(1, entity.PendingCombatRequestCount);
            CharacterActionCombatRequest request = entity.CombatRequests[0];
            Assert.AreEqual(CharacterActionTrackEventKind.HitTraceWindow, request.EventKind);
            Assert.AreEqual("light_attack.hit_window", request.Metadata.StableEventId);
        }

[Test]
        public void RuntimeWorld_Spawn_CreatesAuthoritativeCharacterDataBeforeView()
        {
            var world = new CharacterTestCharacterRuntimeWorld();
            var registry = new CharacterTestCharacterDefinitionRegistry();
            Assert.IsTrue(registry.TryGet(
                CharacterTestCharacterDefinitionRegistry.IronVanguardCharacterId,
                out CharacterTestCharacterDefinition definition));

            CharacterTestCharacterEntity localPlayer = world.Spawn(
                new CharacterTestCharacterSpawnRequest(
                    definition.CharacterId,
                    teamId: 3,
                    x: 1.25f,
                    y: 2f,
                    z: -4.5f,
                    CharacterTestCharacterControlOwner.LocalPlayer),
                definition);
            CharacterTestCharacterEntity companion = world.Spawn(
                new CharacterTestCharacterSpawnRequest(
                    definition.CharacterId,
                    teamId: 4,
                    x: 9f,
                    y: 0f,
                    z: 2f,
                    CharacterTestCharacterControlOwner.None),
                definition);

            Assert.AreEqual(2, world.Entities.Count);
            Assert.AreEqual(1, localPlayer.Id.Value);
            Assert.AreEqual(2, companion.Id.Value);
            Assert.AreEqual(definition.CharacterId, localPlayer.Identity.CharacterId);
            Assert.AreEqual(definition.DisplayName, localPlayer.Identity.DisplayName);
            Assert.AreEqual(definition.PrefabKey, localPlayer.Identity.PrefabKey);
            Assert.AreEqual(3, localPlayer.Team.TeamId);
            Assert.AreEqual(definition.DefaultTeamId, localPlayer.Team.DefaultTeamId);
            Assert.AreEqual(CharacterTestCharacterControlOwner.LocalPlayer, localPlayer.Control.Owner);
            Assert.AreEqual(CharacterTestCharacterControlOwner.None, companion.Control.Owner);
            Assert.IsTrue(localPlayer.Equipment.UseDefaultLoadout);
            Assert.AreEqual(1.25f, localPlayer.Transform.X);
            Assert.AreEqual(2f, localPlayer.Transform.Y);
            Assert.AreEqual(-4.5f, localPlayer.Transform.Z);
        }

        private static CharacterTestCharacterActionRuntime CreateAnimationRequestActionRuntime()
        {
            var actionSet = new CharacterActionSetConfig(
                1,
                CharacterTestCharacterActionConfigSource.ActionSetStableId,
                "Animation Request Test",
                string.Empty,
                string.Empty,
                new[]
                {
                    new CharacterActionBinding(
                        CharacterTestCharacterActionConfigSource.LightAttackIntentId,
                        CharacterTestCharacterActionConfigSource.LightAttackActionId)
                },
                Array.Empty<CharacterAbilityActionBinding>(),
                Array.Empty<CharacterReactionBinding>(),
                0,
                string.Empty,
                MxFramework.CharacterAction.CharacterActionId.None);
            var lightAttack = new CharacterActionConfig(
                CharacterTestCharacterActionConfigSource.LightAttackActionId,
                "Light Attack",
                CharacterActionCategory.BasicAttack,
                CharacterActionTimelineAuthority.CharacterAuthored,
                Array.Empty<string>(),
                priority: 10,
                durationFrames: 6,
                requirements: Array.Empty<CharacterActionRequirement>(),
                phases: new[]
                {
                    new CharacterActionPhase(CharacterActionPhaseKind.Active, 0, 5)
                },
                cancelRules: Array.Empty<CharacterCancelRule>(),
                interruptRules: Array.Empty<CharacterInterruptRule>(),
                animationTrack: new AnimationTrackConfig(new[]
                {
                    new AnimationTrackEvent(
                        0,
                        CharacterActionTrackEventKind.CrossFadeAnimation,
                        "anim.test.light",
                        0.12f,
                        MxFramework.Game.CharacterTest.CharacterTestAuthoredContentIds.D401_Id158,
                        endFrame: 5)
                }));
            var import = new CharacterActionJsonImportResult(
                new[] { actionSet },
                new[] { lightAttack },
                Array.Empty<CharacterActionJsonDiagnostic>());
            var registry = new CharacterActionConfigRegistry(
                import.ActionSets,
                import.Actions,
                "animation-request-action-sets",
                "animation-request-actions");
            CharacterActionValidationReport validation = CharacterActionValidationReport.Validate(
                registry,
                new CharacterActionValidationOptions(CharacterActionMissingAnimationActionKeyPolicy.Ignore));
            Assert.IsTrue(validation.Success);

            return new CharacterTestCharacterActionRuntime(
                import,
                registry,
                validation,
                actionSet,
                CharacterTestCharacterActionConfigSourceKind.EmbeddedFallback,
                "animation request test",
                "animation-request-action-sets",
                "animation-request-actions",
                Array.Empty<CharacterTestCharacterActionConfigSourceDiagnostic>());
        }

        private static void AssertLightAttackControlRelease(
            CharacterTestCharacterActionRuntime runtime,
            CharacterTestCharacterActionConfigSourceKind expectedSourceKind)
        {
            Assert.IsNotNull(runtime);
            Assert.AreEqual(expectedSourceKind, runtime.ConfigSourceKind);
            AssertLightAttackControlRelease(
                runtime,
                CharacterTestCharacterActionConfigSource.LightAttackActionId,
                CharacterTestAuthoredContentIds.D401_Id159,
                CharacterTestAuthoredContentIds.D401_Id160);
            AssertLightAttackControlRelease(
                runtime,
                LightAttackSecondActionId,
                CharacterTestAuthoredContentIds.D401_Id163,
                CharacterTestAuthoredContentIds.D401_Id164);
            AssertLightAttackControlRelease(
                runtime,
                LightAttackThirdActionId,
                CharacterTestAuthoredContentIds.D401_Id167,
                CharacterTestAuthoredContentIds.D401_Id168);
        }

        private static void AssertLightAttackControlRelease(
            CharacterTestCharacterActionRuntime runtime,
            CharacterActionId actionId,
            int controlScaleEventId,
              int controlRestoreEventId)
        {
            Assert.IsTrue(runtime.Registry.TryGetAction(actionId, out CharacterActionConfig action), actionId.ToString());
            Assert.IsNotNull(action.MotionTrack, actionId.ToString());
            bool foundControlScale = false;
            bool foundControlRelease = false;
            MotionTrackEvent[] events = action.MotionTrack.Events;
            for (int i = 0; i < events.Length; i++)
            {
                MotionTrackEvent evt = events[i];
                if (evt.Frame == 0
                    && evt.Kind == CharacterActionTrackEventKind.SetControlScale
                    && evt.StableEventId == controlScaleEventId)
                {
                    foundControlScale = true;
                    Assert.AreEqual(0f, evt.MovementControlScale, 0.0001f, actionId.ToString());
                    Assert.AreEqual(1f, evt.RotationControlScale, 0.0001f, actionId.ToString());
                }

                if (evt.Frame == 17
                    && evt.Kind == CharacterActionTrackEventKind.SetControlScale
                    && evt.StableEventId == controlRestoreEventId)
                {
                    foundControlRelease = true;
                }
            }

            Assert.IsTrue(foundControlScale, actionId + " missing frame 0 control scale.");
            Assert.IsTrue(foundControlRelease, actionId + " missing frame 17 control restore.");
        }

        private static void AssertJumpAnimationConfigConsistency(
            CharacterTestCharacterActionRuntime external,
            CharacterTestCharacterActionRuntime embedded)
        {
            AssertJumpAnimationConfig(external);
            AssertJumpAnimationConfig(embedded);
        }

        private static void AssertJumpAnimationConfig(CharacterTestCharacterActionRuntime runtime)
        {
            Assert.IsTrue(
                runtime.Registry.TryGetAction(new MxFramework.CharacterAction.CharacterActionId(300), out CharacterActionConfig action),
                runtime.ConfigSourceKind.ToString());
            Assert.AreEqual(108, action.DurationFrames, runtime.ConfigSourceKind.ToString());
            Assert.IsNotNull(action.AnimationTrack, runtime.ConfigSourceKind.ToString());
            Assert.AreEqual(1, action.AnimationTrack.Events.Length, runtime.ConfigSourceKind.ToString());

            AnimationTrackEvent evt = action.AnimationTrack.Events[0];
            Assert.AreEqual(0, evt.Frame, runtime.ConfigSourceKind.ToString());
            Assert.AreEqual(107, evt.EndFrame, runtime.ConfigSourceKind.ToString());
            Assert.AreEqual(
                CharacterTestCharacterActionConfigSource.JumpAnimationActionKey,
                evt.AnimationActionKey,
                runtime.ConfigSourceKind.ToString());
            Assert.AreEqual(
                CharacterTestCharacterActionConfigSource.JumpAnimationClipResourceKey,
                evt.AnimationClipResourceKey,
                runtime.ConfigSourceKind.ToString());
        }

        private static void AdvanceUntilActiveAction(
            CharacterTestCharacterActionSystem system,
            CharacterTestCharacterRuntimeWorld world,
            CharacterTestCharacterEntity entity,
            ref long frame,
            CharacterActionId expectedActionId,
            int maxFrames)
        {
            for (int i = 0; i < maxFrames; i++)
            {
                frame++;
                system.Tick(world, new RuntimeFrame(frame));
                if (expectedActionId == entity.Action.ActiveActionId)
                {
                    return;
                }
            }

            Assert.Fail(
                "Expected active action "
                + expectedActionId
                + " within "
                + maxFrames
                + " frames. active="
                + entity.Action.ActiveActionId
                + " localFrame="
                + entity.Action.LocalFrame
                + " reject="
                + entity.Action.LastRejectReason
                + " pending="
                + entity.PendingGameplayIntentCount);
        }

[Test]
        public void CharacterActionSystem_EmptySkillSlotRejectsWithoutAction()
        {
            CharacterTestCharacterEntity attacker;
            CharacterTestCharacterEntity target;
            CharacterTestCharacterRuntimeWorld world = CreateRuntimeWorldWithAttackerAndTarget(
                targetTeamId: 2,
                out attacker,
                out target);
            CharacterTestSkillSlotRuntime skillSlots = CharacterTestSkillSlotRuntime.CreateDefault();
            Assert.IsTrue(skillSlots.TryEquip(8, 0, out _));
            var system = new CharacterTestCharacterActionSystem(
                NullRuntimeLogger.Instance,
                abilityRuntime: CharacterTestAbilityCatalogSource.CreateRuntimeModule(),
                skillSlots: skillSlots);

            QueueGameplayIntent(
                attacker,
                CharacterTestGameplayIntentKind.SkillSlot,
                RuntimeFrame.Zero,
                skillSlotPressedMask: CharacterTestSkillSlotRuntime.ToMask(8));

            system.Tick(world, RuntimeFrame.Zero);

            Assert.AreEqual(CharacterActionId.None, attacker.Action.ActiveActionId);
            Assert.AreEqual(CharacterTestSkillSlotRuntime.EmptyRejectReason, attacker.Action.LastRejectReason);
            Assert.AreEqual(0, attacker.PendingGameplayIntentCount);
        }

[Test]
        public void CharacterActionRuntime_BlockedIntent_DoesNotFreezeActiveRunner()
        {
            CharacterTestCharacterEntity entity = CreateRuntimeCharacter();
            CharacterTestCharacterActionRuntime runtime = CharacterTestCharacterActionConfigSource.Load();

            Assert.IsTrue(runtime.TryStartIntent(
                entity,
                CharacterTestCharacterActionConfigSource.JumpIntentId,
                new RuntimeFrame(10)));

            int localFrameBeforeBlockedInput = entity.Action.LocalFrame;
            Assert.IsFalse(runtime.TryStartIntent(
                entity,
                CharacterTestCharacterActionConfigSource.LightAttackIntentId,
                new RuntimeFrame(11)));
            Assert.AreEqual(CharacterActionDiagnosticCodes.CharacterCancelRejected, entity.Action.LastRejectReason);

            runtime.TickEntity(entity, clearFrameRequests: false);

            Assert.Greater(entity.Action.LocalFrame, localFrameBeforeBlockedInput);
            Assert.AreEqual(CharacterActionDiagnosticCodes.CharacterCancelRejected, entity.Action.LastRejectReason);
        }

[Test]
        public void CharacterActionRuntime_NoActiveAction_StartsNormally()
        {
            CharacterTestCharacterEntity entity = CreateRuntimeCharacter();
            CharacterTestCharacterActionRuntime runtime = CreateTransitionActionRuntime();

            Assert.IsTrue(runtime.TryStartIntent(entity, global::MxFramework.Tests.Game.CharacterTest.MigrationFixtureIds.Id0004, RuntimeFrame.Zero));

            Assert.AreEqual(TransitionFollowUpActionId, entity.Action.ActiveActionId);
            Assert.AreEqual(CharacterTestActionStateId.Running, entity.Action.State);
            Assert.AreEqual(string.Empty, entity.Action.LastRejectReason);
        }

[Test]
        public void CharacterActionRuntime_AllowedActiveTransition_UsesRequestCancelAndStartsTarget()
        {
            CharacterTestCharacterEntity entity = CreateRuntimeCharacter();
            CharacterTestCharacterActionRuntime runtime = CreateTransitionActionRuntime(allowFollowUp: true);

            Assert.IsTrue(runtime.TryStartIntent(entity, MxFramework.Game.CharacterTest.CharacterTestAuthoredContentIds.D404_Id59, RuntimeFrame.Zero));
            Assert.AreEqual(CharacterTestCharacterActionConfigSource.LightAttackActionId, entity.Action.ActiveActionId);

            Assert.IsTrue(runtime.TryStartIntent(entity, global::MxFramework.Tests.Game.CharacterTest.MigrationFixtureIds.Id0004, new RuntimeFrame(1)));

            Assert.AreEqual(TransitionFollowUpActionId, entity.Action.ActiveActionId);
            Assert.AreEqual(CharacterTestActionStateId.Running, entity.Action.State);
            Assert.AreEqual(0, entity.Action.LocalFrame);
            Assert.AreNotEqual(
                CharacterActionDiagnosticCodes.ActionLowerPriorityRejected,
                entity.Action.LastRejectReason);
        }

[Test]
        public void CharacterActionRuntime_RejectedActiveTransition_KeepsActiveActionAndSurfacesCancelRejected()
        {
            CharacterTestCharacterEntity entity = CreateRuntimeCharacter();
            CharacterTestCharacterActionRuntime runtime = CreateTransitionActionRuntime(allowFollowUp: false);

            Assert.IsTrue(runtime.TryStartIntent(entity, MxFramework.Game.CharacterTest.CharacterTestAuthoredContentIds.D404_Id59, RuntimeFrame.Zero));

            Assert.IsFalse(runtime.TryStartIntent(entity, global::MxFramework.Tests.Game.CharacterTest.MigrationFixtureIds.Id0004, new RuntimeFrame(1)));

            Assert.AreEqual(CharacterTestCharacterActionConfigSource.LightAttackActionId, entity.Action.ActiveActionId);
            Assert.AreEqual(CharacterTestActionStateId.Running, entity.Action.State);
            Assert.AreEqual(CharacterActionDiagnosticCodes.CharacterCancelRejected, entity.Action.LastRejectReason);
            Assert.AreNotEqual(
                CharacterActionDiagnosticCodes.ActionLowerPriorityRejected,
                entity.Action.LastRejectReason);
        }

[Test]
        public void CharacterActionSystem_AttackPrimaryFollowUpsUseBufferedTransitionWindows()
        {
            CharacterTestCharacterRuntimeWorld world =
                CreateRuntimeWorldWithLocalPlayer(out CharacterTestCharacterEntity entity);
            SetActionContext(
                world,
                entity,
                "char.combat_girl_sword",
                "equip_state.combat_girl_sword.sword_empty");
            var system = new CharacterTestCharacterActionSystem(NullRuntimeLogger.Instance);
            long frame = 0L;

            QueueGameplayIntent(entity, CharacterTestGameplayIntentKind.AttackPrimary, new RuntimeFrame(frame));
            system.Tick(world, new RuntimeFrame(frame));

            Assert.AreEqual(
                CharacterTestCharacterActionConfigSource.LightAttackActionId,
                entity.Action.ActiveActionId,
                "reject=" + entity.Action.LastRejectReason);
            Assert.AreEqual(0, entity.PendingGameplayIntentCount);

            AdvanceActionToLocalFrame(system, world, entity, ref frame, 31);
            frame++;
            QueueGameplayIntent(entity, CharacterTestGameplayIntentKind.AttackPrimary, new RuntimeFrame(frame));
            system.Tick(world, new RuntimeFrame(frame));

            Assert.AreEqual(
                CharacterTestCharacterActionConfigSource.LightAttackActionId,
                entity.Action.ActiveActionId,
                "reject=" + entity.Action.LastRejectReason);
            Assert.AreEqual(CharacterActionDiagnosticCodes.ActionSlotBlocked, entity.Action.LastRejectReason);
            Assert.AreEqual(1, entity.PendingGameplayIntentCount);

            AdvanceUntilActiveAction(
                system,
                world,
                entity,
                ref frame,
                LightAttackSecondActionId,
                maxFrames: 32);

            Assert.AreEqual(LightAttackSecondActionId, entity.Action.ActiveActionId);
            Assert.AreEqual(0, entity.Action.LocalFrame);
            Assert.AreEqual(0, entity.PendingGameplayIntentCount);
            Assert.AreEqual(LightAttackSecondAnimationClipResourceKey, entity.Action.AnimationActionKeyThisFrame);

            AdvanceActionToLocalFrame(system, world, entity, ref frame, 47);
            frame++;
            QueueGameplayIntent(entity, CharacterTestGameplayIntentKind.AttackPrimary, new RuntimeFrame(frame));
            system.Tick(world, new RuntimeFrame(frame));

            Assert.AreEqual(
                LightAttackSecondActionId,
                entity.Action.ActiveActionId,
                "reject=" + entity.Action.LastRejectReason);
            Assert.AreEqual(CharacterActionDiagnosticCodes.ActionSlotBlocked, entity.Action.LastRejectReason);
            Assert.AreEqual(1, entity.PendingGameplayIntentCount);

            AdvanceUntilActiveAction(
                system,
                world,
                entity,
                ref frame,
                LightAttackThirdActionId,
                maxFrames: 32);

            Assert.AreEqual(LightAttackThirdActionId, entity.Action.ActiveActionId);
            Assert.AreEqual(0, entity.Action.LocalFrame);
            Assert.AreEqual(0, entity.PendingGameplayIntentCount);
            Assert.AreEqual(LightAttackThirdAnimationClipResourceKey, entity.Action.AnimationActionKeyThisFrame);

            AdvanceActionToLocalFrame(system, world, entity, ref frame, 17);
            frame++;
            QueueGameplayIntent(entity, CharacterTestGameplayIntentKind.AttackPrimary, new RuntimeFrame(frame));
            system.Tick(world, new RuntimeFrame(frame));

            Assert.AreEqual(LightAttackThirdActionId, entity.Action.ActiveActionId);
            Assert.AreEqual(CharacterActionDiagnosticCodes.ActionSlotBlocked, entity.Action.LastRejectReason);
            Assert.AreEqual(1, entity.PendingGameplayIntentCount);

            AdvanceActionUntilInactive(system, world, entity, ref frame, maxFrames: 100);
            Assert.AreEqual(CharacterActionId.None, entity.Action.ActiveActionId);
            Assert.AreEqual(0, entity.PendingGameplayIntentCount);

            frame++;
            QueueGameplayIntent(entity, CharacterTestGameplayIntentKind.AttackPrimary, new RuntimeFrame(frame));
            system.Tick(world, new RuntimeFrame(frame));

            Assert.AreEqual(CharacterTestCharacterActionConfigSource.LightAttackActionId, entity.Action.ActiveActionId);
        }

[Test]
        public void CharacterActionSystem_DodgeInputResolvesConfiguredActionAndKeepsBlockedCurrentAction()
        {
            CharacterTestCharacterRuntimeWorld world =
                CreateRuntimeWorldWithLocalPlayer(out CharacterTestCharacterEntity entity);
            var system = new CharacterTestCharacterActionSystem(NullRuntimeLogger.Instance);
            long frame = 0L;

            QueueGameplayIntent(entity, CharacterTestGameplayIntentKind.AttackPrimary, new RuntimeFrame(frame));
            system.Tick(world, new RuntimeFrame(frame));

            Assert.AreEqual(CharacterTestCharacterActionConfigSource.LightAttackActionId, entity.Action.ActiveActionId);

            AdvanceActionToLocalFrame(system, world, entity, ref frame, 10);
            frame++;
            QueueGameplayIntent(entity, CharacterTestGameplayIntentKind.Dodge, new RuntimeFrame(frame));
            system.Tick(world, new RuntimeFrame(frame));

            Assert.AreEqual(CharacterTestCharacterActionConfigSource.LightAttackActionId, entity.Action.ActiveActionId);
            Assert.AreEqual(CharacterTestActionStateId.Running, entity.Action.State);
            Assert.AreEqual(CharacterActionDiagnosticCodes.CharacterCancelRejected, entity.Action.LastRejectReason);
            Assert.AreEqual(1, entity.PendingGameplayIntentCount,
                "A temporarily blocked player dodge remains buffered instead of becoming an invalid empty action.");
        }

        [Test]
        public void CharacterActionSystem_DodgeInputStartsConfiguredEvadeWhenIdle()
        {
            CharacterTestCharacterRuntimeWorld world =
                CreateRuntimeWorldWithLocalPlayer(out CharacterTestCharacterEntity entity);
            var system = new CharacterTestCharacterActionSystem(NullRuntimeLogger.Instance);

            QueueGameplayIntent(entity, CharacterTestGameplayIntentKind.Dodge, RuntimeFrame.Zero);
            system.Tick(world, RuntimeFrame.Zero);

            Assert.AreEqual(
                CharacterTestCharacterActionConfigSource.DefaultEvadeActionId,
                entity.Action.ActiveActionId);
            Assert.AreEqual(CharacterTestActionStateId.Running, entity.Action.State);
            Assert.AreEqual(string.Empty, entity.Action.LastRejectReason);
        }

[Test]
        public void CharacterActionSystem_AbilityTransition_CommitsCooldownOnlyAfterRequestCancelAccepted()
        {
            CharacterTestCharacterRuntimeWorld rejectedWorld =
                CreateRuntimeWorldWithLocalPlayer(out CharacterTestCharacterEntity rejectedEntity);
            AbilityRuntimeModule rejectedAbilityRuntime = CreateTransitionAbilityRuntime(out AbilityDefinitionConfig rejectedAbility);
            var rejectedSystem = new CharacterTestCharacterActionSystem(
                NullRuntimeLogger.Instance,
                abilityRuntime: rejectedAbilityRuntime,
                runtime: CreateTransitionActionRuntime(allowAbilityFollowUp: false));

            Assert.IsTrue(rejectedSystem.Runtime.TryStartIntent(rejectedEntity, MxFramework.Game.CharacterTest.CharacterTestAuthoredContentIds.D404_Id59, RuntimeFrame.Zero));
            QueueGameplayIntent(
                rejectedEntity,
                CharacterTestGameplayIntentKind.AbilitySlotCast,
                new RuntimeFrame(1),
                abilitySlotPressed: 1,
                abilityIdPressed: rejectedAbility.AbilityId);

            rejectedSystem.Tick(rejectedWorld, new RuntimeFrame(1));

            Assert.AreEqual(CharacterTestCharacterActionConfigSource.LightAttackActionId, rejectedEntity.Action.ActiveActionId);
            Assert.AreEqual(CharacterActionDiagnosticCodes.CharacterCancelRejected, rejectedEntity.Action.LastRejectReason);
            Assert.AreEqual(
                0L,
                rejectedAbilityRuntime.GetRemainingCooldownFrames(
                    rejectedEntity.Id.Value,
                    rejectedAbility,
                    new RuntimeFrame(1)));

            CharacterTestCharacterRuntimeWorld acceptedWorld =
                CreateRuntimeWorldWithLocalPlayer(out CharacterTestCharacterEntity acceptedEntity);
            AbilityRuntimeModule acceptedAbilityRuntime = CreateTransitionAbilityRuntime(out AbilityDefinitionConfig acceptedAbility);
            var acceptedSystem = new CharacterTestCharacterActionSystem(
                NullRuntimeLogger.Instance,
                abilityRuntime: acceptedAbilityRuntime,
                runtime: CreateTransitionActionRuntime(allowAbilityFollowUp: true));

            Assert.IsTrue(acceptedSystem.Runtime.TryStartIntent(acceptedEntity, MxFramework.Game.CharacterTest.CharacterTestAuthoredContentIds.D404_Id59, RuntimeFrame.Zero));
            QueueGameplayIntent(
                acceptedEntity,
                CharacterTestGameplayIntentKind.AbilitySlotCast,
                new RuntimeFrame(1),
                abilitySlotPressed: 1,
                abilityIdPressed: acceptedAbility.AbilityId);

            acceptedSystem.Tick(acceptedWorld, new RuntimeFrame(1));

            Assert.AreEqual(TransitionAbilityFollowUpActionId, acceptedEntity.Action.ActiveActionId);
            Assert.AreEqual(
                30L,
                acceptedAbilityRuntime.GetRemainingCooldownFrames(
                    acceptedEntity.Id.Value,
                    acceptedAbility,
                    new RuntimeFrame(1)));
        }

[Test]
        public void CharacterActionSystem_StasisFreezesActionFrameAndDropsNewActionRequests()
        {
            CharacterTestCharacterRuntimeWorld world =
                CreateRuntimeWorldWithLocalPlayer(out CharacterTestCharacterEntity entity);
            var system = new CharacterTestCharacterActionSystem(NullRuntimeLogger.Instance);

            Assert.IsTrue(system.Runtime.TryStartIntent(
                entity,
                CharacterTestCharacterActionConfigSource.LightAttackIntentId,
                RuntimeFrame.Zero));
            system.Tick(world, new RuntimeFrame(1));
            int frozenFrame = entity.Action.LocalFrame;
            Assert.Greater(frozenFrame, 0);

            GameplayComponentStore<GameplayStatusComponent> statusStore =
                world.GameplayComponentWorld.GetOrCreateStore<GameplayStatusComponent>();
            statusStore.Set(
                entity.GameplayEntityId,
                new GameplayStatusComponent(new GameplayStatusId(CharacterTestStasisIds.StasisStatusId)));
            var statusControl = new CharacterTestStatusControlEffectBridge();
            statusControl.Tick(world);
            entity.SetMotionStateDeterministic(
                verticalVelocityMillimetersPerSecond: 0,
                lastMoveInputXMilli: 0,
                lastMoveInputYMilli: 0,
                isGrounded: true,
                jumpBufferFramesRemaining: 5);
            entity.SetInput(new CharacterTestCharacterInputComponent(
                Vector2.right,
                jumpPressed: true,
                attackPrimaryPressed: true,
                skillSlotPressedMask: CharacterTestSkillSlotRuntime.ToMask(1),
                abilitySlotPressed: 1,
                abilityIdPressed: CharacterTestAbilityIds.FireballAbilityId));

            system.Tick(world, new RuntimeFrame(2), statusControl);

            Assert.AreEqual(CharacterTestCharacterActionConfigSource.LightAttackActionId, entity.Action.ActiveActionId);
            Assert.AreEqual(frozenFrame, entity.Action.LocalFrame);
            Assert.AreEqual(CharacterTestStasisIds.ActionFrozenRejectReason, entity.Action.LastRejectReason);
            Assert.AreEqual(string.Empty, entity.Action.AnimationActionKeyThisFrame);
            Assert.IsFalse(entity.Input.AttackPrimaryPressed);
            Assert.AreEqual(0, entity.Input.SkillSlotPressedMask);
            Assert.IsFalse(entity.Input.HasAbilitySlotCast);
            Assert.AreEqual(0, entity.JumpBufferFramesRemaining);
            Assert.AreEqual(1f, entity.Input.Move.x, 0.0001f);

            system.Tick(world, new RuntimeFrame(3), statusControl);
            Assert.AreEqual(frozenFrame, entity.Action.LocalFrame);

            statusStore.Remove(entity.GameplayEntityId);
            statusControl.Tick(world);
            system.Tick(world, new RuntimeFrame(4), statusControl);

            Assert.AreEqual(CharacterTestCharacterActionConfigSource.LightAttackActionId, entity.Action.ActiveActionId);
            Assert.Greater(entity.Action.LocalFrame, frozenFrame);
        }

[Test]
        public void CharacterActionSystem_StunFreezesActionWithoutStasisAnimationPause()
        {
            CharacterTestCharacterRuntimeWorld world =
                CreateRuntimeWorldWithLocalPlayer(out CharacterTestCharacterEntity entity);
            var system = new CharacterTestCharacterActionSystem(NullRuntimeLogger.Instance);

            Assert.IsTrue(system.Runtime.TryStartIntent(
                entity,
                CharacterTestCharacterActionConfigSource.LightAttackIntentId,
                RuntimeFrame.Zero));
            system.Tick(world, new RuntimeFrame(1));
            int frozenFrame = entity.Action.LocalFrame;
            Assert.Greater(frozenFrame, 0);

            GameplayComponentStore<GameplayStatusComponent> statusStore =
                world.GameplayComponentWorld.GetOrCreateStore<GameplayStatusComponent>();
            statusStore.Set(
                entity.GameplayEntityId,
                new GameplayStatusComponent(new GameplayStatusId(CharacterTestStunIds.StunnedStatusId)));
            var statusControl = new CharacterTestStatusControlEffectBridge();
            statusControl.Tick(world);
            entity.SetInput(new CharacterTestCharacterInputComponent(
                Vector2.zero,
                jumpPressed: false,
                attackPrimaryPressed: true));

            system.Tick(world, new RuntimeFrame(2), statusControl);

            Assert.IsTrue(statusControl.IsActionFrozen(entity));
            Assert.IsFalse(statusControl.ShouldPauseAnimationPresentation(entity));
            Assert.AreEqual(
                CharacterControlPresentationKind.Stunned,
                statusControl.GetPrimaryControlPresentationKind(entity));
            Assert.AreEqual(CharacterTestStunIds.ActionFrozenRejectReason, entity.Action.LastRejectReason);
            Assert.AreEqual(frozenFrame, entity.Action.LocalFrame);
            Assert.IsFalse(entity.Input.AttackPrimaryPressed);
        }

[Test]
        public void CharacterStatusControlPresentation_ArbitratesPrimaryAndFallsBackSameFrame()
        {
            CharacterTestCharacterRuntimeWorld world =
                CreateRuntimeWorldWithLocalPlayer(out CharacterTestCharacterEntity entity);
            GameplayComponentStore<GameplayStatusComponent> statusStore =
                world.GameplayComponentWorld.GetOrCreateStore<GameplayStatusComponent>();
            var statusControl = new CharacterTestStatusControlEffectBridge();

            statusStore.Set(
                entity.GameplayEntityId,
                new GameplayStatusComponent(
                    new GameplayStatusId(CharacterTestStasisIds.StasisStatusId),
                    new GameplayStatusId(CharacterTestStunIds.StunnedStatusId)));
            statusControl.Tick(world);
            Assert.IsTrue(statusControl.IsActionFrozen(entity));
            Assert.IsFalse(statusControl.ShouldPauseAnimationPresentation(entity));
            Assert.AreEqual(
                CharacterControlPresentationKind.Stunned,
                statusControl.GetPrimaryControlPresentationKind(entity));
            Assert.AreEqual(
                CharacterControlPresentationOverlayFlags.Stasis,
                statusControl.GetOverlayPresentationFlags(entity));

            statusStore.Set(
                entity.GameplayEntityId,
                new GameplayStatusComponent(
                    new GameplayStatusId(CharacterTestStunIds.StunnedStatusId),
                    new GameplayStatusId(CharacterTestParalysisIds.ParalyzedStatusId)));
            statusControl.Tick(world);
            Assert.AreEqual(
                CharacterControlPresentationKind.Paralyzed,
                statusControl.GetPrimaryControlPresentationKind(entity));
            Assert.AreEqual(
                CharacterTestParalysisIds.ActionFrozenRejectReason,
                statusControl.GetActionFrozenRejectReason(entity));

            statusStore.Set(
                entity.GameplayEntityId,
                new GameplayStatusComponent(new GameplayStatusId(CharacterTestStunIds.StunnedStatusId)));
            statusControl.Tick(world);
            Assert.AreEqual(
                CharacterControlPresentationKind.Stunned,
                statusControl.GetPrimaryControlPresentationKind(entity));
            Assert.AreEqual(
                CharacterTestStunIds.ActionFrozenRejectReason,
                statusControl.GetActionFrozenRejectReason(entity));

            statusStore.Set(
                entity.GameplayEntityId,
                new GameplayStatusComponent(new GameplayStatusId(CharacterTestStasisIds.StasisStatusId)));
            statusControl.Tick(world);
            Assert.AreEqual(
                CharacterControlPresentationKind.Stasis,
                statusControl.GetPrimaryControlPresentationKind(entity));
            Assert.IsTrue(statusControl.ShouldPauseAnimationPresentation(entity));
        }

[Test]
        public void CharacterAnimationSync_StunWithoutConfiguredAnimationFallsBackToIdle()
        {
            CharacterTestCharacterRuntimeWorld world =
                CreateRuntimeWorldWithLocalPlayer(out CharacterTestCharacterEntity entity);
            entity.SetAnimation(new CharacterTestCharacterAnimationComponent(
                new Vector2(0.7f, 0.4f),
                0.9f,
                grounded: true,
                airborne: false,
                actionActive: true,
                actionKey: CharacterTestCharacterActionConfigSource.LightAttackAnimationActionKey,
                actionTransitionSeconds: 0.12f,
                actionHoldSeconds: 0.3f));
            GameplayComponentStore<GameplayStatusComponent> statusStore =
                world.GameplayComponentWorld.GetOrCreateStore<GameplayStatusComponent>();
            statusStore.Set(
                entity.GameplayEntityId,
                new GameplayStatusComponent(new GameplayStatusId(CharacterTestStunIds.StunnedStatusId)));
            var statusControl = new CharacterTestStatusControlEffectBridge();
            statusControl.Tick(world);
            var animationSync = new CharacterTestCharacterAnimationSyncSystem(null);

            CharacterTestCharacterAnimationComponent animation =
                animationSync.ResolvePresentationAnimationForValidation(
                    entity,
                    statusControl,
                    out bool presentationPaused);

            Assert.IsFalse(presentationPaused);
            Assert.AreEqual(Vector2.zero, animation.LocomotionBlend);
            Assert.AreEqual(0f, animation.LocomotionSpeed01, 0.0001f);
            Assert.IsTrue(animation.Grounded);
            Assert.IsFalse(animation.Airborne);
            Assert.IsFalse(animation.ActionActive);
            Assert.AreEqual(string.Empty, animation.ActionKey);
        }

[Test]
        public void CharacterAnimationSync_ConfiguredStunAnimationEmitsActionKeyOnlyOnEnter()
        {
            CharacterTestCharacterRuntimeWorld world =
                CreateRuntimeWorldWithLocalPlayer(out CharacterTestCharacterEntity entity);
            GameplayComponentStore<GameplayStatusComponent> statusStore =
                world.GameplayComponentWorld.GetOrCreateStore<GameplayStatusComponent>();
            statusStore.Set(
                entity.GameplayEntityId,
                new GameplayStatusComponent(new GameplayStatusId(CharacterTestStunIds.StunnedStatusId)));
            var statusControl = new CharacterTestStatusControlEffectBridge();
            statusControl.Tick(world);
            const string stunActionKey = "anim.status.stunned";
            var registry = new CharacterControlPresentationAnimationRegistry(
                new CharacterControlPresentationAnimationDefinition(
                    CharacterControlPresentationKind.Stunned,
                    stunActionKey,
                    transitionSeconds: 0.14f,
                    holdSeconds: 0.25f));
            var animationSync = new CharacterTestCharacterAnimationSyncSystem(null, registry);

            CharacterTestCharacterAnimationComponent entered =
                animationSync.ResolvePresentationAnimationForValidation(
                    entity,
                    statusControl,
                    out bool enteredPaused);
            CharacterTestCharacterAnimationComponent continued =
                animationSync.ResolvePresentationAnimationForValidation(
                    entity,
                    statusControl,
                    out bool continuedPaused);

            Assert.IsFalse(enteredPaused);
            Assert.IsTrue(entered.ActionActive);
            Assert.AreEqual(stunActionKey, entered.ActionKey);
            Assert.AreEqual(0.14f, entered.ActionTransitionSeconds, 0.0001f);
            Assert.AreEqual(0.25f, entered.ActionHoldSeconds, 0.0001f);
            Assert.IsFalse(continuedPaused);
            Assert.IsTrue(continued.ActionActive);
            Assert.AreEqual(string.Empty, continued.ActionKey);
        }

[Test]
        public void CharacterAnimationSync_StasisWithStunUsesStunAnimationInsteadOfPause()
        {
            CharacterTestCharacterRuntimeWorld world =
                CreateRuntimeWorldWithLocalPlayer(out CharacterTestCharacterEntity entity);
            GameplayComponentStore<GameplayStatusComponent> statusStore =
                world.GameplayComponentWorld.GetOrCreateStore<GameplayStatusComponent>();
            statusStore.Set(
                entity.GameplayEntityId,
                new GameplayStatusComponent(
                    new GameplayStatusId(CharacterTestStasisIds.StasisStatusId),
                    new GameplayStatusId(CharacterTestStunIds.StunnedStatusId)));
            var statusControl = new CharacterTestStatusControlEffectBridge();
            statusControl.Tick(world);
            const string stunActionKey = "anim.status.stunned";
            var registry = new CharacterControlPresentationAnimationRegistry(
                new CharacterControlPresentationAnimationDefinition(
                    CharacterControlPresentationKind.Stunned,
                    stunActionKey,
                    transitionSeconds: 0.08f,
                    holdSeconds: 0.1f));
            var animationSync = new CharacterTestCharacterAnimationSyncSystem(null, registry);

            CharacterTestCharacterAnimationComponent animation =
                animationSync.ResolvePresentationAnimationForValidation(
                    entity,
                    statusControl,
                    out bool presentationPaused);

            Assert.IsFalse(presentationPaused);
            Assert.IsTrue(animation.ActionActive);
            Assert.AreEqual(stunActionKey, animation.ActionKey);
        }

[Test]
        public void CharacterActionRuntime_TickClearsJumpImpulseFrameRequest()
        {
            CharacterTestCharacterEntity entity = CreateRuntimeCharacter();
            CharacterTestCharacterActionRuntime runtime = CharacterTestCharacterActionConfigSource.Load();

            Assert.IsTrue(runtime.TryStartIntent(
                entity,
                CharacterTestCharacterActionConfigSource.JumpIntentId,
                new RuntimeFrame(10)));
            Assert.Greater(entity.Action.JumpImpulseY, 0f);

            runtime.TickEntity(entity);

            Assert.AreEqual(0f, entity.Action.JumpImpulseY);
            Assert.AreEqual(string.Empty, entity.Action.AnimationActionKeyThisFrame);
        }

[Test]
        public void CharacterActionSystem_HeldJumpDoesNotRefreshBufferedIntent()
        {
            CharacterTestCharacterRuntimeWorld world =
                CreateRuntimeWorldWithLocalPlayer(out CharacterTestCharacterEntity entity);
            var system = new CharacterTestCharacterActionSystem(NullRuntimeLogger.Instance);
            entity.SetMotionState(
                verticalVelocity: -1f,
                lastMoveInput: Vector2.zero,
                isGrounded: false,
                coyoteTimeSecondsRemaining: 0f);
            QueueGameplayIntent(entity, CharacterTestGameplayIntentKind.Jump, new RuntimeFrame(1));

            system.Tick(world, new RuntimeFrame(1));

            Assert.AreEqual(CharacterActionId.None, entity.Action.ActiveActionId);
            Assert.AreEqual(1, entity.PendingGameplayIntentCount);
            Assert.AreEqual(
                CharacterTestGameplayIntentBuffer.DefaultBufferFrames,
                entity.GameplayIntentBuffer.GetRemainingFrames(
                    CharacterTestGameplayIntentKind.Jump,
                    1));

            entity.SetInput(new CharacterTestCharacterInputComponent(
                Vector2.zero,
                jumpPressed: false,
                jumpHeld: true));

            for (int frame = 2; frame <= 12; frame++)
            {
                entity.SetMotionStateDeterministic(
                    verticalVelocityMillimetersPerSecond: -1000,
                    lastMoveInputXMilli: 0,
                    lastMoveInputYMilli: 0,
                    isGrounded: false,
                    coyoteTimeFramesRemaining: 0);
                system.Tick(world, new RuntimeFrame(frame));
                Assert.AreEqual(CharacterActionId.None, entity.Action.ActiveActionId);
            }

            entity.SetMotionStateDeterministic(
                verticalVelocityMillimetersPerSecond: 0,
                lastMoveInputXMilli: 0,
                lastMoveInputYMilli: 0,
                isGrounded: true,
                coyoteTimeFramesRemaining: 0);

            system.Tick(world, new RuntimeFrame(13));

            Assert.AreEqual(CharacterActionId.None, entity.Action.ActiveActionId);
            Assert.AreEqual(0, entity.PendingGameplayIntentCount);
        }

[Test]
        public void CharacterRuntime_LandingClearsJumpActionAndAllowsNextJump()
        {
            string directory = CreateTempActionConfigDirectory();
            try
            {
                WriteActionJsonFiles(directory, durationFrames: 142);
                using (GameSlice slice = CreateSliceWithActionDirectory(directory))
                {
                    CharacterTestCharacterEntity entity = SpawnLocalPlayer(slice);
                    Assert.IsTrue(slice.ActionRuntime.TryStartIntent(
                        entity,
                        CharacterTestCharacterActionConfigSource.JumpIntentId,
                        slice.CurrentFrame));
                    Assert.AreEqual(CharacterTestCharacterActionConfigSource.JumpActionId, entity.Action.ActiveActionId);

                    int landedFrame = -1;
                    for (int i = 0; i < 90; i++)
                    {
                        slice.Tick(1d / 60d);
                        if (entity.IsGrounded && string.IsNullOrEmpty((entity.Action.ActiveActionId).ToString()))
                        {
                            landedFrame = i + 1;
                            break;
                        }
                    }

                    Assert.GreaterOrEqual(landedFrame, 0, "Jump action should end when motion lands.");
                    Assert.Less(landedFrame, 142, "Landing should end the jump action before animation duration expires.");
                    Assert.AreEqual(CharacterTestActionStateId.None, entity.Action.State);
                    Assert.IsFalse(entity.Animation.ActionActive);

                    Assert.IsTrue(slice.ActionRuntime.TryStartIntent(
                        entity,
                        CharacterTestCharacterActionConfigSource.JumpIntentId,
                        slice.CurrentFrame));
                    Assert.AreEqual(CharacterTestCharacterActionConfigSource.JumpActionId, entity.Action.ActiveActionId);
                }
            }
            finally
            {
                DeleteDirectory(directory);
            }
        }

[Test]
        public void CharacterMovementProfile_MissingExternalProfile_UsesFallback()
        {
            string directory = CreateTempActionConfigDirectory();
            try
            {
                WriteActionJsonFiles(directory, durationFrames: 12);
                using (GameSlice slice = CreateSliceWithActionDirectory(directory))
                {
                    Assert.AreEqual(CharacterTestCharacterMovementProfileSource.DefaultProfileId, slice.MovementProfile.StableId);
                    Assert.IsTrue(slice.MovementProfile.UsedFallback);
                    Assert.AreEqual(4f, slice.MovementProfile.Profile.RunSpeed, 0.0001f);
                    Assert.AreEqual(-30f, slice.MovementProfile.Profile.Gravity, 0.0001f);
                    Assert.AreEqual(-50f, slice.MovementProfile.MaxFallSpeed, 0.0001f);
                }
            }
            finally
            {
                DeleteDirectory(directory);
            }
        }

[Test]
        public void CharacterMovementProfile_ExternalProfile_DrivesRunSpeedAndGravity()
        {
            string directory = CreateTempActionConfigDirectory();
            try
            {
                WriteActionJsonFiles(directory, durationFrames: 12);
                WriteMovementProfileJson(directory, runSpeed: 8f, gravity: -12f, acceleration: 16f);
                using (GameSlice slice = CreateSliceWithActionDirectory(directory))
                {
                    Assert.IsFalse(slice.MovementProfile.UsedFallback);
                    Assert.AreEqual(8f, slice.MovementProfile.Profile.RunSpeed, 0.0001f);
                    Assert.AreEqual(-12f, slice.MovementProfile.Profile.Gravity, 0.0001f);
                    Assert.AreEqual(16f, slice.MovementProfile.Profile.Acceleration, 0.0001f);
                }
            }
            finally
            {
                DeleteDirectory(directory);
            }
        }

[Test]
        public void CharacterMotion_AcceleratesAndDeceleratesHorizontalVelocity()
        {
            CharacterTestCharacterRuntimeWorld world = CreateRuntimeWorldWithLocalPlayer(out CharacterTestCharacterEntity entity);
            var motion = new CharacterTestCharacterMotionSystem();
            motion.SetMovementProfile(CreateMovementRuntime(runSpeed: 4f, acceleration: 4f, deceleration: 4f, groundFriction: 0f));

            entity.SetInput(new CharacterTestCharacterInputComponent(Vector2.up, jumpPressed: false, jumpHeld: true));
            motion.Tick(world, CharacterTestDeterministicUnits.DefaultTickRate);
            Assert.AreEqual(67, entity.HorizontalVelocityYMillimetersPerSecond);
            Assert.AreEqual(1, entity.Transform.ZMillimeters);

            motion.Tick(world, CharacterTestDeterministicUnits.DefaultTickRate);
            Assert.AreEqual(134, entity.HorizontalVelocityYMillimetersPerSecond);

            entity.SetInput(CharacterTestCharacterInputComponent.None);
            motion.Tick(world, CharacterTestDeterministicUnits.DefaultTickRate);
            Assert.AreEqual(67, entity.HorizontalVelocityYMillimetersPerSecond);
        }

[Test]
        public void CharacterMotion_MovementSpeedScaleMultipliesFinalTargetSpeed()
        {
            CharacterTestCharacterRuntimeWorld fullWorld =
                CreateRuntimeWorldWithLocalPlayer(out CharacterTestCharacterEntity fullEntity);
            CharacterTestCharacterRuntimeWorld halfWorld =
                CreateRuntimeWorldWithLocalPlayer(out CharacterTestCharacterEntity halfEntity);
            var fullMotion = new CharacterTestCharacterMotionSystem();
            var halfMotion = new CharacterTestCharacterMotionSystem();
            CharacterTestCharacterMovementProfileRuntime profile = CreateMovementRuntime(
                runSpeed: 4f,
                acceleration: 1000f,
                deceleration: 1000f,
                groundFriction: 0f);
            fullMotion.SetMovementProfile(profile);
            halfMotion.SetMovementProfile(profile);

            fullEntity.SetInput(new CharacterTestCharacterInputComponent(
                Vector2.up,
                jumpPressed: false,
                movementSpeedScaleMilli: 1000));
            halfEntity.SetInput(new CharacterTestCharacterInputComponent(
                Vector2.up,
                jumpPressed: false,
                movementSpeedScaleMilli: 500));

            fullMotion.Tick(fullWorld, CharacterTestDeterministicUnits.DefaultTickRate);
            halfMotion.Tick(halfWorld, CharacterTestDeterministicUnits.DefaultTickRate);

            Assert.AreEqual(4000, fullEntity.DesiredVelocityYMillimetersPerSecond);
            Assert.AreEqual(2000, halfEntity.DesiredVelocityYMillimetersPerSecond);
            Assert.AreEqual(
                fullEntity.DesiredVelocityYMillimetersPerSecond / 2,
                halfEntity.DesiredVelocityYMillimetersPerSecond);
        }

[Test]
        public void CharacterMotion_CommandWorldMoveOverridesLocalMovementBasis()
        {
            CharacterTestCharacterRuntimeWorld world = CreateRuntimeWorldWithLocalPlayer(out CharacterTestCharacterEntity entity);
            var motion = new CharacterTestCharacterMotionSystem();
            motion.SetMovementProfile(CreateMovementRuntime(runSpeed: 4f, acceleration: 4f, deceleration: 4f, groundFriction: 0f));
            motion.SetMovementBasisProvider(new FixedMovementBasisProvider(CharacterTestCharacterMovementBasis.Identity));

            entity.SetInput(new CharacterTestCharacterInputComponent(
                Vector2.up,
                jumpPressed: false,
                jumpHeld: true,
                worldMove: Vector2.right,
                hasWorldMove: true));

            motion.Tick(world, CharacterTestDeterministicUnits.DefaultTickRate);

            Assert.AreEqual(1, entity.Transform.XMillimeters);
            Assert.AreEqual(0, entity.Transform.ZMillimeters);
            Assert.AreEqual(67, entity.HorizontalVelocityXMillimetersPerSecond);
            Assert.AreEqual(0, entity.HorizontalVelocityYMillimetersPerSecond);
        }

[Test]
        public void CharacterMotion_UsesDesiredVelocityForLocomotionBlend()
        {
            CharacterTestCharacterRuntimeWorld world = CreateRuntimeWorldWithLocalPlayer(out CharacterTestCharacterEntity entity);
            var motion = new CharacterTestCharacterMotionSystem();
            var animation = new CharacterTestCharacterAnimationSystem();
            CharacterTestCharacterMovementProfileRuntime profile =
                CreateMovementRuntime(runSpeed: 4f, acceleration: 4f, deceleration: 4f, groundFriction: 0f);
            motion.SetMovementProfile(profile);
            animation.SetMovementProfile(profile);

            entity.SetInput(new CharacterTestCharacterInputComponent(Vector2.up, jumpPressed: false, jumpHeld: true));
            motion.Tick(world, CharacterTestDeterministicUnits.DefaultTickRate);
            animation.Tick(world);

            Assert.AreEqual(67, entity.HorizontalVelocityYMillimetersPerSecond);
            Assert.AreEqual(4000, entity.DesiredVelocityYMillimetersPerSecond);
            Assert.AreEqual(1f, entity.Animation.LocomotionSpeed01, 0.0001f);
            Assert.AreEqual(1f, entity.Animation.LocomotionBlend.y, 0.0001f);
        }

[Test]
        public void CharacterMotion_ConsumesImpulseMotionRequestAndClearsQueue()
        {
            CharacterTestCharacterRuntimeWorld world = CreateRuntimeWorldWithLocalPlayer(out CharacterTestCharacterEntity entity);
            var motion = new CharacterTestCharacterMotionSystem();
            motion.SetMovementProfile(CreateMovementRuntime(gravity: -30f));
            entity.EnqueueMotionRequest(CharacterTestCharacterMotionRequest.Impulse(
                new Vector3(0f, 8f, 0f),
                CharacterMovementMode.Airborne,
                MxFramework.Tests.CharacterActionLegacyFixtureIds.From("test.jump"),
                global::MxFramework.Tests.Game.CharacterTest.MigrationFixtureIds.Id0073));

            motion.Tick(world, 1f / 60f);

            Assert.AreEqual(0, entity.PendingMotionRequestCount);
            Assert.Greater(entity.VerticalVelocity, 0f);
            Assert.IsFalse(entity.IsGrounded);
            Assert.AreEqual(CharacterMovementMode.Airborne, entity.MovementMode);
        }

[Test]
        public void CharacterMotion_MotionRequestSourcesDoNotAllocateAfterWarmup()
        {
            CharacterTestCharacterRuntimeWorld world = CreateRuntimeWorldWithLocalPlayer(out CharacterTestCharacterEntity entity);
            var motion = new CharacterTestCharacterMotionSystem();
            motion.SetMovementProfile(CreateMovementRuntime());

            for (int i = 0; i < 4; i++)
            {
                entity.EnqueueMotionRequest(CharacterTestCharacterMotionRequest.ExternalForceRaw(
                    1200,
                    0,
                    0,
                    CharacterTestCharacterMotionRequestSpace.World,
                    MxFramework.Tests.CharacterActionLegacyFixtureIds.From("test.motion.source"),
                    global::MxFramework.Tests.Game.CharacterTest.MigrationFixtureIds.Id0075));
                motion.Tick(world, CharacterTestDeterministicUnits.DefaultTickRate);
            }

            _ = GC.GetAllocatedBytesForCurrentThread();
            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < 64; i++)
            {
                entity.EnqueueMotionRequest(CharacterTestCharacterMotionRequest.ExternalForceRaw(
                    1200,
                    0,
                    0,
                    CharacterTestCharacterMotionRequestSpace.World,
                    MxFramework.Tests.CharacterActionLegacyFixtureIds.From("test.motion.source"),
                    global::MxFramework.Tests.Game.CharacterTest.MigrationFixtureIds.Id0075));
                motion.Tick(world, CharacterTestDeterministicUnits.DefaultTickRate);
            }

            long allocatedBytes = GC.GetAllocatedBytesForCurrentThread() - before;
            Assert.AreEqual(0L, allocatedBytes);
        }

[Test]
        public void CharacterMotion_RootMotionDeltaRequest_MovesThroughMotionSystem()
        {
            CharacterTestCharacterRuntimeWorld world = CreateRuntimeWorldWithLocalPlayer(out CharacterTestCharacterEntity entity);
            var motion = new CharacterTestCharacterMotionSystem();
            motion.SetMovementProfile(CreateMovementRuntime());
            entity.EnqueueMotionRequest(CharacterTestCharacterMotionRequest.RootMotionDelta(
                new Vector3(0f, 0f, 1.25f),
                CharacterTestCharacterMotionRequestSpace.Local,
                MxFramework.Tests.CharacterActionLegacyFixtureIds.From("test.attack"),
                global::MxFramework.Tests.Game.CharacterTest.MigrationFixtureIds.Id0065));

            motion.Tick(world, CharacterTestDeterministicUnits.DefaultTickRate);

            Assert.AreEqual(0, entity.PendingMotionRequestCount);
            Assert.AreEqual(1.25f, entity.Transform.Z, 0.0001f);
            Assert.AreEqual(0f, entity.HorizontalVelocity.magnitude, 0.0001f);
            Assert.AreEqual(CharacterMovementMode.RootMotionDriven, entity.MovementMode);
        }

[Test]
        public void CharacterRuntime_BufferedJump_StartsAfterLanding()
        {
            string directory = CreateTempActionConfigDirectory();
            try
            {
                WriteActionJsonFiles(directory, durationFrames: 24);
                using (GameSlice slice = CreateSliceWithActionDirectory(directory))
                {
                    CharacterTestCharacterEntity entity = SpawnLocalPlayer(slice);
                    entity.SetTransform(new CharacterTestCharacterTransformComponent(0f, 0.03f, 0f));
                    entity.SetMotionState(
                        -1f,
                        Vector2.zero,
                        isGrounded: false,
                        coyoteTimeSecondsRemaining: 0f);

                    SetSliceInputSnapshot(slice, CreateInputSnapshot(Vector2.zero, jumpPressed: true, jumpHeld: true));
                    slice.Tick(1d / 60d);
                    Assert.AreEqual(CharacterActionId.None, entity.Action.ActiveActionId);
                    Assert.Greater(entity.PendingGameplayIntentCount, 0);

                    SetSliceInputSnapshot(slice, CreateInputSnapshot(Vector2.zero));
                    for (int i = 0; i < 8 && string.IsNullOrEmpty((entity.Action.ActiveActionId).ToString()); i++)
                        slice.Tick(1d / 60d);

                    Assert.AreEqual(CharacterTestCharacterActionConfigSource.JumpActionId, entity.Action.ActiveActionId);
                }
            }
            finally
            {
                DeleteDirectory(directory);
            }
        }

[Test]
        public void CharacterRuntime_CoyoteJump_StartsWhileRecentlyAirborne()
        {
            string directory = CreateTempActionConfigDirectory();
            try
            {
                WriteActionJsonFiles(directory, durationFrames: 24);
                using (GameSlice slice = CreateSliceWithActionDirectory(directory))
                {
                    CharacterTestCharacterEntity entity = SpawnLocalPlayer(slice);
                    entity.SetTransform(new CharacterTestCharacterTransformComponent(0f, 0.1f, 0f));
                    entity.SetMotionState(
                        0f,
                        Vector2.zero,
                        isGrounded: false,
                        coyoteTimeSecondsRemaining: 0.08f);

                    SetSliceInputSnapshot(slice, CreateInputSnapshot(Vector2.zero, jumpPressed: true, jumpHeld: true));
                    slice.Tick(1d / 60d);

                    Assert.AreEqual(CharacterTestCharacterActionConfigSource.JumpActionId, entity.Action.ActiveActionId);
                    Assert.Greater(entity.VerticalVelocity, 0f);
                }
            }
            finally
            {
                DeleteDirectory(directory);
            }
        }

[Test]
        public void CharacterMotion_JumpReleaseCutsUpwardVelocity()
        {
            CharacterTestCharacterRuntimeWorld world = CreateRuntimeWorldWithLocalPlayer(out CharacterTestCharacterEntity entity);
            var motion = new CharacterTestCharacterMotionSystem();
            motion.SetMovementProfile(CreateMovementRuntime(jumpReleaseVelocityMultiplier: 0.5f));
            entity.SetTransform(new CharacterTestCharacterTransformComponent(0f, 0.5f, 0f));
            entity.SetMotionState(8f, Vector2.zero, isGrounded: false);
            entity.SetInput(new CharacterTestCharacterInputComponent(
                Vector2.zero,
                jumpPressed: false,
                jumpHeld: false,
                jumpReleased: true));

            motion.Tick(world, 0.01f);

            Assert.Less(entity.VerticalVelocity, 4f);
        }

[Test]
        public void CharacterMotion_SpawnGrounded_DoesNotEmitLandingEvent()
        {
            CharacterTestCharacterRuntimeWorld world =
                CreateRuntimeWorldWithLocalPlayer(out CharacterTestCharacterEntity entity);
            var motion = new CharacterTestCharacterMotionSystem();
            motion.SetMovementProfile(CreateMovementRuntime(gravity: 0f));
            entity.SetTransform(CharacterTestCharacterTransformComponent.FromDeterministic(0, 0, 0, 0));
            entity.SetMotionStateDeterministic(0, 0, 0, isGrounded: true);

            motion.Tick(world, CharacterTestDeterministicUnits.DefaultTickRate, new RuntimeFrame(1));

            Assert.AreEqual(0, motion.LandingEventCount);
        }

[Test]
        public void CharacterMotion_RestoreBaseline_SuppressesFirstLandingEdge()
        {
            CharacterTestCharacterRuntimeWorld world =
                CreateRuntimeWorldWithLocalPlayer(out CharacterTestCharacterEntity entity);
            var motion = new CharacterTestCharacterMotionSystem();
            motion.SetMovementProfile(CreateMovementRuntime(gravity: 0f));
            entity.SetTransform(CharacterTestCharacterTransformComponent.FromDeterministic(0, 10, 0, 0));
            entity.SetMotionStateDeterministic(-8000, 0, 0, isGrounded: false);
            motion.SuppressLandingEdgesForNextTick();

            motion.Tick(world, CharacterTestDeterministicUnits.DefaultTickRate, new RuntimeFrame(1));

            Assert.AreEqual(0, motion.LandingEventCount);
            Assert.IsTrue(entity.IsGrounded);
        }

 [TestCase(7999, 398, 1500)]
 [TestCase(8000, 399, 800)]
        public void CharacterMotion_LandingBoundary_StartsConfiguredActionOnce(
            int downwardSpeedMillimetersPerSecond,
            int expectedActionId,
            int expectedPlaybackSpeedMilli)
        {
            CharacterTestCharacterRuntimeWorld world =
                CreateRuntimeWorldWithLocalPlayer(out CharacterTestCharacterEntity entity);
            var motion = new CharacterTestCharacterMotionSystem();
            motion.SetMovementProfile(CreateMovementRuntime(gravity: 0f));
            var actions = new CharacterTestCharacterActionSystem(NullRuntimeLogger.Instance);
            actions.Runtime.BindRuntimeWorld(world);
            entity.SetTransform(CharacterTestCharacterTransformComponent.FromDeterministic(0, 100, 0, 0));
            entity.SetMotionStateDeterministic(
                -downwardSpeedMillimetersPerSecond,
                0,
                0,
                isGrounded: false);

            var landingFrame = new RuntimeFrame(12);
            motion.Tick(world, CharacterTestDeterministicUnits.DefaultTickRate, landingFrame);

            Assert.AreEqual(1, motion.LandingEventCount);
            Assert.AreEqual(
                downwardSpeedMillimetersPerSecond,
                motion.GetLandingEvent(0).DownwardSpeedMillimetersPerSecond);

            actions.ProcessLandingActions(motion, landingFrame);

            Assert.AreEqual(new CharacterActionId(expectedActionId), entity.Action.ActiveActionId);
            Assert.AreEqual(expectedPlaybackSpeedMilli, entity.Action.AnimationPlaybackSpeedMilli);
            Assert.AreEqual(
                expectedPlaybackSpeedMilli,
                world.CharacterComponents.GetAnimationRequest(entity.Id).PlaybackSpeedMilli);

            motion.Tick(
                world,
                CharacterTestDeterministicUnits.DefaultTickRate,
                new RuntimeFrame(landingFrame.Value + 1));

            Assert.AreEqual(0, motion.LandingEventCount);
        }

[Test]
        public void CharacterMotion_BelowLightLandingThreshold_DoesNotStartLandingAction()
        {
            CharacterTestCharacterRuntimeWorld world =
                CreateRuntimeWorldWithLocalPlayer(out CharacterTestCharacterEntity entity);
            var motion = new CharacterTestCharacterMotionSystem();
            motion.SetMovementProfile(CreateMovementRuntime(gravity: 0f));
            var actions = new CharacterTestCharacterActionSystem(NullRuntimeLogger.Instance);
            actions.Runtime.BindRuntimeWorld(world);
            entity.SetTransform(CharacterTestCharacterTransformComponent.FromDeterministic(0, 10, 0, 0));
            entity.SetMotionStateDeterministic(-999, 0, 0, isGrounded: false);

            var landingFrame = new RuntimeFrame(12);
            motion.Tick(world, CharacterTestDeterministicUnits.DefaultTickRate, landingFrame);
            Assert.AreEqual(1, motion.LandingEventCount);

            actions.ProcessLandingActions(motion, landingFrame);

            Assert.AreEqual(CharacterActionId.None, entity.Action.ActiveActionId);
            Assert.IsFalse(world.CharacterComponents.GetAnimationRequest(entity.Id).IsValid);
        }

[Test]
        public void CharacterAction_LightLandingMovementWindow_CancelsToLocomotion()
        {
            var world = new CharacterTestCharacterRuntimeWorld();
            var definition = new CharacterTestCharacterDefinition(
                characterId: 1001,
                displayName: "Movement Cancel Test",
                prefabKey: default,
                defaultTeamId: 1,
                animationBinding: default);
            CharacterTestCharacterEntity entity = world.Spawn(
                new CharacterTestCharacterSpawnRequest(
                    definition.CharacterId,
                    definition.DefaultTeamId,
                    0f,
                    0f,
                    0f,
                    CharacterTestCharacterControlOwner.LocalPlayer),
                definition);
            entity.SetMotionState(0f, Vector2.zero, isGrounded: true);
            const string landingProfileId = "landing.movement-cancel.test";
            var actionSet = new CharacterActionSetConfig(
                1,
                "movement-cancel.test",
                "Movement Cancel Test",
                string.Empty,
                string.Empty,
                Array.Empty<CharacterActionBinding>(),
                Array.Empty<CharacterAbilityActionBinding>(),
                Array.Empty<CharacterReactionBinding>(),
                0,
                string.Empty,
                MxFramework.CharacterAction.CharacterActionId.None,
                defaultActionSlotMask: (int)CharacterCancelInputMask.LightLanding,
                landingProfileId: landingProfileId);
            var landingProfile = new CharacterLandingProfile(
                landingProfileId,
                lightLandingActionId: CharacterTestCharacterActionConfigSource.LandingLightActionId,
                heavyLandingActionId: MxFramework.CharacterAction.CharacterActionId.None);
            var landingAction = new CharacterActionConfig(
                CharacterTestCharacterActionConfigSource.LandingLightActionId,
                "Light Landing",
                CharacterActionCategory.Movement,
                CharacterActionTimelineAuthority.CharacterAuthored,
                Array.Empty<string>(),
                priority: 30,
                durationFrames: 55,
                requirements: Array.Empty<CharacterActionRequirement>(),
                phases: new[]
                {
                    new CharacterActionPhase(CharacterActionPhaseKind.Landing, 0, 54),
                },
                cancelRules: new[]
                {
                    new CharacterCancelRule(9, 54, CharacterActionRequestType.Movement),
                },
                interruptRules: Array.Empty<CharacterInterruptRule>());
            var import = new CharacterActionJsonImportResult(
                new[] { actionSet },
                new[] { landingAction },
                Array.Empty<CharacterReactionProfile>(),
                new[] { landingProfile },
                Array.Empty<CharacterActionJsonDiagnostic>());
            var registry = new CharacterActionConfigRegistry(
                import.ActionSets,
                import.Actions,
                "movement-cancel-action-sets",
                "movement-cancel-actions",
                landingProfiles: import.LandingProfiles,
                landingProfilesSourcePath: "movement-cancel-landing-profiles");
            CharacterActionValidationReport validation = CharacterActionValidationReport.Validate(
                registry,
                new CharacterActionValidationOptions(CharacterActionMissingAnimationActionKeyPolicy.Ignore));
            Assert.IsTrue(validation.Success);
            var runtime = new CharacterTestCharacterActionRuntime(
                import,
                registry,
                validation,
                actionSet,
                CharacterTestCharacterActionConfigSourceKind.EmbeddedFallback,
                "movement cancel test",
                "movement-cancel-action-sets",
                "movement-cancel-actions",
                Array.Empty<CharacterTestCharacterActionConfigSourceDiagnostic>());
            var actions = new CharacterTestCharacterActionSystem(
                NullRuntimeLogger.Instance,
                runtime: runtime);
            actions.Runtime.BindRuntimeWorld(world);

            var landingFrame = new RuntimeFrame(12);
            Assert.IsTrue(runtime.TryStartActionSlot(
                entity,
                (int)CharacterCancelInputMask.LightLanding,
                CharacterTestCharacterActionConfigSource.LandingLightActionId,
                landingFrame,
                new MxFramework.Runtime.RuntimeTraceIdentity(global::MxFramework.Tests.Game.CharacterTest.MigrationFixtureIds.Trace0017),
                CharacterActionSourceKind.Landing));
            Assert.AreEqual(CharacterTestCharacterActionConfigSource.LandingLightActionId, entity.Action.ActiveActionId);

            entity.SetInput(new CharacterTestCharacterInputComponent(Vector2.up, jumpPressed: false));
            for (int frame = 13; frame <= 21; frame++)
                actions.Tick(world, new RuntimeFrame(frame));

            Assert.AreEqual(CharacterTestCharacterActionConfigSource.LandingLightActionId, entity.Action.ActiveActionId);
            Assert.AreEqual(9, entity.Action.LocalFrame);

            var cancelFrame = new RuntimeFrame(22);
            actions.Tick(world, cancelFrame);

            Assert.AreEqual(CharacterActionId.None, entity.Action.ActiveActionId);
            Assert.AreEqual(1f, entity.MovementControlScale, 0.0001f);
            Assert.IsTrue(entity.Input.HasActiveMove);
        }

[Test]
        public void CharacterInput_ActiveMoveThreshold_UsesDeterministicMilliInput()
        {
            var below = new CharacterTestCharacterInputComponent(new Vector2(0.03f, 0f), jumpPressed: false);
            var above = new CharacterTestCharacterInputComponent(new Vector2(0.04f, 0f), jumpPressed: false);

            Assert.IsFalse(below.HasActiveMove);
            Assert.IsTrue(above.HasActiveMove);
        }

[Test]
        public void CharacterMotion_AirControlLimitsAirborneAcceleration()
        {
            CharacterTestCharacterRuntimeWorld world = CreateRuntimeWorldWithLocalPlayer(out CharacterTestCharacterEntity entity);
            var motion = new CharacterTestCharacterMotionSystem();
            motion.SetMovementProfile(CreateMovementRuntime(runSpeed: 4f, acceleration: 100f, airControl: 0.25f));
            entity.SetTransform(new CharacterTestCharacterTransformComponent(0f, 1f, 0f));
            entity.SetMotionState(0f, Vector2.zero, isGrounded: false);
            entity.SetInput(new CharacterTestCharacterInputComponent(Vector2.up, jumpPressed: false, jumpHeld: true));

            motion.Tick(world, CharacterTestDeterministicUnits.DefaultTickRate);

            Assert.AreEqual(4000, CharacterTestDeterministicMath.Magnitude(
                entity.DesiredVelocityXMillimetersPerSecond,
                entity.DesiredVelocityYMillimetersPerSecond));
            Assert.AreEqual(417, CharacterTestDeterministicMath.Magnitude(
                entity.HorizontalVelocityXMillimetersPerSecond,
                entity.HorizontalVelocityYMillimetersPerSecond));
        }

[Test]
        public void CharacterMotion_AirborneWithoutInputPreservesHorizontalInertia()
        {
            CharacterTestCharacterRuntimeWorld world = CreateRuntimeWorldWithLocalPlayer(out CharacterTestCharacterEntity entity);
            var motion = new CharacterTestCharacterMotionSystem();
            motion.SetMovementProfile(CreateMovementRuntime(runSpeed: 4f, acceleration: 100f, deceleration: 100f, airControl: 0.25f));
            entity.SetTransform(new CharacterTestCharacterTransformComponent(0f, 1f, 0f));
            entity.SetMotionState(
                verticalVelocity: 0f,
                lastMoveInput: Vector2.zero,
                isGrounded: false,
                horizontalVelocity: new Vector2(0f, 4f));
            entity.SetInput(CharacterTestCharacterInputComponent.None);

            motion.Tick(world, CharacterTestDeterministicUnits.DefaultTickRate);

            Assert.AreEqual(4000, entity.HorizontalVelocityYMillimetersPerSecond);
            Assert.AreEqual(67, entity.Transform.ZMillimeters);
            Assert.AreEqual(4000, CharacterTestDeterministicMath.Magnitude(
                entity.DesiredVelocityXMillimetersPerSecond,
                entity.DesiredVelocityYMillimetersPerSecond));
        }

[Test]
        public void CharacterMotion_AirborneSameInputDoesNotClampInheritedGroundSpeed()
        {
            CharacterTestCharacterRuntimeWorld world = CreateRuntimeWorldWithLocalPlayer(out CharacterTestCharacterEntity entity);
            var motion = new CharacterTestCharacterMotionSystem();
            motion.SetMovementProfile(CreateMovementRuntime(runSpeed: 4f, acceleration: 100f, deceleration: 100f, airControl: 0.25f));
            entity.SetTransform(new CharacterTestCharacterTransformComponent(0f, 1f, 0f));
            entity.SetMotionState(
                verticalVelocity: 0f,
                lastMoveInput: Vector2.zero,
                isGrounded: false,
                horizontalVelocity: new Vector2(0f, 4f));
            entity.SetInput(new CharacterTestCharacterInputComponent(Vector2.up, jumpPressed: false, jumpHeld: true));

            motion.Tick(world, CharacterTestDeterministicUnits.DefaultTickRate);

            Assert.AreEqual(4000, entity.HorizontalVelocityYMillimetersPerSecond);
            Assert.AreEqual(67, entity.Transform.ZMillimeters);
            Assert.AreEqual(4000, CharacterTestDeterministicMath.Magnitude(
                entity.DesiredVelocityXMillimetersPerSecond,
                entity.DesiredVelocityYMillimetersPerSecond));
        }

[Test]
        public void CharacterMotion_TurnSpeedSmoothsYawTowardMoveDirection()
        {
            CharacterTestCharacterRuntimeWorld world = CreateRuntimeWorldWithLocalPlayer(out CharacterTestCharacterEntity entity);
            var motion = new CharacterTestCharacterMotionSystem();
            motion.SetMovementProfile(CreateMovementRuntime(runSpeed: 4f, acceleration: 100f, turnSpeed: 90f));
            entity.SetInput(new CharacterTestCharacterInputComponent(Vector2.right, jumpPressed: false, jumpHeld: true));

            motion.Tick(world, CharacterTestDeterministicUnits.DefaultTickRate);

            Assert.AreEqual(1500, entity.Transform.YawMilliDegrees);
        }

[Test]
        public void CharacterMotion_AiControlledTurnsYawTowardMoveDirection()
        {
            CharacterTestCharacterRuntimeWorld world = CreateRuntimeWorldWithLocalPlayer(out CharacterTestCharacterEntity entity);
            var motion = new CharacterTestCharacterMotionSystem();
            motion.SetMovementProfile(CreateMovementRuntime(runSpeed: 4f, acceleration: 100f, turnSpeed: 90f));
            entity.SetControl(new CharacterTestCharacterControlComponent(
                CharacterTestCharacterControlOwner.AiControlled));
            entity.SetInput(new CharacterTestCharacterInputComponent(Vector2.right, jumpPressed: false, jumpHeld: true));

            motion.Tick(world, CharacterTestDeterministicUnits.DefaultTickRate);

            Assert.AreEqual(1500, entity.Transform.YawMilliDegrees);
        }

        [Test]
        public void CharacterMotion_TurnInPlaceAppliesQuantizedYawDeltaWithoutTranslation()
        {
            CharacterTestCharacterRuntimeWorld world = CreateRuntimeWorldWithLocalPlayer(out CharacterTestCharacterEntity entity);
            var motion = new CharacterTestCharacterMotionSystem();
            motion.SetMovementProfile(CreateMovementRuntime(runSpeed: 4f, acceleration: 100f, turnSpeed: 90f));
            entity.SetTransform(CharacterTestCharacterTransformComponent.FromDeterministic(
                1250,
                0,
                -750,
                120000));
            entity.SetMotionState(
                verticalVelocity: 0f,
                lastMoveInput: Vector2.zero,
                isGrounded: true,
                horizontalVelocity: new Vector2(4f, 3f),
                desiredVelocity: new Vector2(4f, 3f));
            entity.SetInput(new CharacterTestCharacterInputComponent(
                Vector2.right,
                jumpPressed: false,
                worldMove: Vector2.up,
                hasWorldMove: true,
                attackPrimaryPressed: true,
                turnInPlace: true,
                turnInPlaceYawMilliDegrees: 90000));

            motion.Tick(world, CharacterTestDeterministicUnits.DefaultTickRate);

            Assert.AreEqual(1250, entity.Transform.XMillimeters);
            Assert.AreEqual(-750, entity.Transform.ZMillimeters);
            Assert.AreEqual(210000, entity.Transform.YawMilliDegrees);
            Assert.AreEqual(0, entity.HorizontalVelocityXMillimetersPerSecond);
            Assert.AreEqual(0, entity.HorizontalVelocityYMillimetersPerSecond);
            Assert.AreEqual(CharacterMovementMode.TurnInPlace, entity.MovementMode);
            Assert.IsFalse(entity.Input.HasActiveMove);
            Assert.IsFalse(entity.Input.AttackPrimaryPressed);
            Assert.IsFalse(entity.Input.TurnInPlace,
                "Motion consumes the one-tick turn command after applying it.");
            Assert.AreEqual(0, entity.Input.TurnInPlaceYawMilliDegrees);

            motion.Tick(world, CharacterTestDeterministicUnits.DefaultTickRate);
            Assert.AreEqual(210000, entity.Transform.YawMilliDegrees,
                "A retained input must not apply the same turn delta twice.");
        }

[TestCase(CharacterTestCharacterControlOwner.None)]
[TestCase(CharacterTestCharacterControlOwner.Scripted)]
        public void CharacterMotion_NonCommandDrivenOwnersPreserveYawWhileMoving(
            CharacterTestCharacterControlOwner owner)
        {
            CharacterTestCharacterRuntimeWorld world = CreateRuntimeWorldWithLocalPlayer(out CharacterTestCharacterEntity entity);
            var motion = new CharacterTestCharacterMotionSystem();
            motion.SetMovementProfile(CreateMovementRuntime(runSpeed: 4f, acceleration: 100f, turnSpeed: 90f));
            entity.SetControl(new CharacterTestCharacterControlComponent(owner));
            entity.SetInput(new CharacterTestCharacterInputComponent(Vector2.right, jumpPressed: false, jumpHeld: true));

            motion.Tick(world, CharacterTestDeterministicUnits.DefaultTickRate);

            Assert.Greater(entity.HorizontalVelocity.magnitude, 0f);
            Assert.AreEqual(0, entity.Transform.YawMilliDegrees);
        }

[Test]
        public void CharacterMotion_ControlScaleRequest_ScalesActiveMovementAndRotation()
        {
            CharacterTestCharacterRuntimeWorld world = CreateRuntimeWorldWithLocalPlayer(out CharacterTestCharacterEntity entity);
            var motion = new CharacterTestCharacterMotionSystem();
            motion.SetMovementProfile(CreateMovementRuntime(runSpeed: 4f, acceleration: 100f, turnSpeed: 90f));
            entity.SetInput(new CharacterTestCharacterInputComponent(Vector2.right, jumpPressed: false, jumpHeld: true));
            entity.EnqueueMotionRequest(CharacterTestCharacterMotionRequest.ControlScale(
                movementControlScale: 0.5f,
                rotationControlScale: 0.5f,
                movementMode: CharacterMovementMode.Idle,
                sourceActionId: MxFramework.Tests.CharacterActionLegacyFixtureIds.From("test.attack"),
                stableEventId: global::MxFramework.Tests.Game.CharacterTest.MigrationFixtureIds.Id0063));

            motion.Tick(world, CharacterTestDeterministicUnits.DefaultTickRate);

            Assert.AreEqual(0.5f, entity.MovementControlScale, 0.0001f);
            Assert.AreEqual(0.5f, entity.RotationControlScale, 0.0001f);
            Assert.AreEqual(1667, CharacterTestDeterministicMath.Magnitude(
                entity.HorizontalVelocityXMillimetersPerSecond,
                entity.HorizontalVelocityYMillimetersPerSecond));
            Assert.AreEqual(750, entity.Transform.YawMilliDegrees);
            Assert.IsFalse(entity.MovementLocked);
        }

[Test]
        public void CharacterMotion_ControlScaleZeroBlocksActiveMoveAndTargetLockRotation()
        {
            CharacterTestCharacterRuntimeWorld world = CreateRuntimeWorldWithLocalPlayer(out CharacterTestCharacterEntity entity);
            var motion = new CharacterTestCharacterMotionSystem();
            motion.SetMovementProfile(CreateMovementRuntime(runSpeed: 4f, acceleration: 100f, turnSpeed: 90f));
            motion.SetMovementBasisProvider(new FixedMovementBasisProvider(new CharacterTestCharacterMovementBasis(
                new Vector2(0f, 1f),
                new Vector2(1f, 0f),
                hasLockedTarget: true,
                lockedTargetPosition: new Vector2(10f, 0f))));
            entity.SetInput(new CharacterTestCharacterInputComponent(Vector2.right, jumpPressed: false, jumpHeld: true));
            entity.EnqueueMotionRequest(CharacterTestCharacterMotionRequest.ControlScale(
                movementControlScale: 0f,
                rotationControlScale: 0f,
                movementMode: CharacterMovementMode.Idle,
                sourceActionId: MxFramework.Tests.CharacterActionLegacyFixtureIds.From("test.attack"),
                stableEventId: global::MxFramework.Tests.Game.CharacterTest.MigrationFixtureIds.Id0063));

            motion.Tick(world, CharacterTestDeterministicUnits.DefaultTickRate);

            Assert.IsFalse(entity.MovementLocked);
            Assert.AreEqual(CharacterMovementMode.Run, entity.MovementMode);
            Assert.AreEqual(0f, entity.HorizontalVelocity.magnitude, 0.0001f);
            Assert.AreEqual(0f, entity.Transform.YawDegrees, 0.0001f);
            Assert.AreEqual(0f, entity.MovementControlScale, 0.0001f);
            Assert.AreEqual(0f, entity.RotationControlScale, 0.0001f);
        }

[Test]
        public void CharacterRuntime_ControlScaleWindowExitRestoresActiveMovement()
        {
            string directory = CreateTempActionConfigDirectory();
            try
            {
                string motionTrack = @"{
        ""usesRootMotion"": false,
        ""events"": [
          { ""frame"": 0, ""endFrame"": 1, ""kind"": ""SetControlScale"", ""movementMode"": ""Idle"", ""movementControlScale"": 0.0, ""rotationControlScale"": 0.0, ""stableEventId"": ""light_attack.control_window"" }
        ]
      }";
                WriteActionJsonFiles(directory, CreateActionSetsJson(), CreateActionsJson(12, lightAttackMotionTrackOverride: motionTrack));
                using (GameSlice slice = CreateSliceWithActionDirectory(directory))
                {
                    CharacterTestCharacterEntity entity = SpawnLocalPlayer(slice);
                    SetSliceInputSnapshot(slice, CreateInputSnapshot(Vector2.right, attackPrimaryPressed: true));
                    slice.Tick(1d / 60d);

                    Assert.AreEqual(CharacterTestCharacterActionConfigSource.LightAttackActionId, entity.Action.ActiveActionId);
                    Assert.IsFalse(entity.MovementLocked);
                    Assert.AreEqual(0f, entity.MovementControlScale, 0.0001f);
                    Assert.AreEqual(0f, entity.RotationControlScale, 0.0001f);
                    Assert.AreEqual(0f, entity.HorizontalVelocity.magnitude, 0.0001f);

                    SetSliceInputSnapshot(slice, CreateInputSnapshot(Vector2.right));
                    for (int i = 0; i < 4; i++)
                        slice.Tick(1d / 60d);

                    Assert.IsFalse(entity.MovementLocked);
                    Assert.AreEqual(1f, entity.MovementControlScale, 0.0001f);
                    Assert.AreEqual(1f, entity.RotationControlScale, 0.0001f);
                    Assert.Greater(entity.HorizontalVelocity.magnitude, 0f);
                }
            }
            finally
            {
                DeleteDirectory(directory);
            }
        }

[Test]
        public void CharacterRuntime_ControlScaleTrackAutoResetsWhenActionEnds()
        {
            string directory = CreateTempActionConfigDirectory();
            try
            {
                string motionTrack = @"{
        ""usesRootMotion"": false,
        ""events"": [
          { ""frame"": 0, ""endFrame"": 9, ""kind"": ""SetControlScale"", ""movementMode"": ""Idle"", ""movementControlScale"": 0.0, ""rotationControlScale"": 0.0, ""stableEventId"": ""light_attack.control_window"" }
        ]
      }";
                WriteActionJsonFiles(directory, CreateActionSetsJson(), CreateActionsJson(6, lightAttackMotionTrackOverride: motionTrack));
                using (GameSlice slice = CreateSliceWithActionDirectory(directory))
                {
                    CharacterTestCharacterEntity entity = SpawnLocalPlayer(slice);
                    SetSliceInputSnapshot(slice, CreateInputSnapshot(Vector2.right, attackPrimaryPressed: true));
                    slice.Tick(1d / 60d);

                    Assert.AreEqual(CharacterTestCharacterActionConfigSource.LightAttackActionId, entity.Action.ActiveActionId);
                    Assert.AreEqual(0f, entity.MovementControlScale, 0.0001f);
                    Assert.AreEqual(0f, entity.RotationControlScale, 0.0001f);

                    SetSliceInputSnapshot(slice, CreateInputSnapshot(Vector2.right));
                    for (int i = 0; i < 12; i++)
                        slice.Tick(1d / 60d);

                    Assert.AreEqual(CharacterActionId.None, entity.Action.ActiveActionId);
                    Assert.AreEqual(1f, entity.MovementControlScale, 0.0001f);
                    Assert.AreEqual(1f, entity.RotationControlScale, 0.0001f);
                    Assert.Greater(entity.HorizontalVelocity.magnitude, 0f);
                }
            }
            finally
            {
                DeleteDirectory(directory);
            }
        }

[Test]
        public void CharacterRuntime_OverlappingStateWindowsRestoreLowerContributionAfterInclusiveEnd()
        {
            string directory = CreateTempActionConfigDirectory();
            try
            {
                string motionTrack = @"{
        ""usesRootMotion"": false,
        ""events"": [
          { ""frame"": 0, ""endFrame"": 3, ""kind"": ""SetControlScale"", ""movementMode"": ""Idle"", ""movementControlScale"": 0.5, ""rotationControlScale"": 0.5, ""stableEventId"": ""control.lower"" },
          { ""frame"": 1, ""endFrame"": 2, ""kind"": ""SetControlScale"", ""movementMode"": ""Idle"", ""movementControlScale"": 0.0, ""rotationControlScale"": 0.0, ""stableEventId"": ""control.upper"" },
          { ""frame"": 0, ""endFrame"": 3, ""kind"": ""SetCollisionProfile"", ""collisionProfileId"": ""low"", ""collisionRestorePolicy"": ""OnActionEnd"", ""stableEventId"": ""collision.lower"" },
          { ""frame"": 1, ""endFrame"": 2, ""kind"": ""SetCollisionProfile"", ""collisionProfileId"": ""revive_low"", ""collisionRestorePolicy"": ""OnActionEnd"", ""stableEventId"": ""collision.upper"" }
        ]
      }";
                WriteActionJsonFiles(
                    directory,
                    CreateActionSetsJson(),
                    CreateActionsJson(8, lightAttackMotionTrackOverride: motionTrack));
                using (GameSlice slice = CreateSliceWithActionDirectory(directory))
                {
                    CharacterTestCharacterEntity entity = SpawnLocalPlayer(slice);
                    SetSliceInputSnapshot(slice, CreateInputSnapshot(Vector2.zero, attackPrimaryPressed: true));

                    slice.Tick(1d / 60d);
                    Assert.AreEqual(0.5f, entity.Action.MovementControlScale, 0.0001f);
                    Assert.AreEqual(CharacterTestColliderPostureComponent.LowProfileId, entity.ColliderPosture.ActionProfileId);

                    SetSliceInputSnapshot(slice, CreateInputSnapshot(Vector2.zero));
                    slice.Tick(1d / 60d);
                    Assert.AreEqual(0f, entity.Action.MovementControlScale, 0.0001f);
                    Assert.AreEqual(CharacterTestColliderPostureComponent.ReviveLowProfileId, entity.ColliderPosture.ActionProfileId);

                    slice.Tick(1d / 60d);
                    Assert.AreEqual(0f, entity.Action.MovementControlScale, 0.0001f, "Upper contribution remains active on its inclusive end frame.");
                    Assert.AreEqual(CharacterTestColliderPostureComponent.ReviveLowProfileId, entity.ColliderPosture.ActionProfileId);

                    slice.Tick(1d / 60d);
                    Assert.AreEqual(0.5f, entity.Action.MovementControlScale, 0.0001f, "Lower contribution is restored after the upper window exits.");
                    Assert.AreEqual(CharacterTestColliderPostureComponent.LowProfileId, entity.ColliderPosture.ActionProfileId);

                    slice.Tick(1d / 60d);
                    Assert.AreEqual(1f, entity.Action.MovementControlScale, 0.0001f);
                    Assert.IsFalse(entity.ColliderPosture.HasActionOverride);
                }
            }
            finally
            {
                DeleteDirectory(directory);
            }
        }

[Test]
        public void CharacterMotion_ControlScaleRequest_CanSetRotationWithoutChangingMovementScale()
        {
            CharacterTestCharacterRuntimeWorld world = CreateRuntimeWorldWithLocalPlayer(out CharacterTestCharacterEntity entity);
            var motion = new CharacterTestCharacterMotionSystem();
            motion.SetMovementProfile(CreateMovementRuntime(runSpeed: 4f, acceleration: 100f, deceleration: 100f));
            entity.SetAction(CharacterTestCharacterActionComponent.None);
            entity.SetInput(new CharacterTestCharacterInputComponent(Vector2.right, jumpPressed: false, jumpHeld: true));
            entity.EnqueueMotionRequest(CharacterTestCharacterMotionRequest.ControlScale(
                movementControlScale: 1f,
                rotationControlScale: 0f,
                movementMode: CharacterMovementMode.Idle,
                sourceActionId: MxFramework.Tests.CharacterActionLegacyFixtureIds.From("test.attack"),
                stableEventId: global::MxFramework.Tests.Game.CharacterTest.MigrationFixtureIds.Id0066,
                hasMovementControlScale: false,
                hasRotationControlScale: true));

            motion.Tick(world, CharacterTestDeterministicUnits.DefaultTickRate);

            Assert.IsFalse(entity.MovementLocked);
            Assert.AreEqual(1f, entity.MovementControlScale, 0.0001f);
            Assert.AreEqual(0f, entity.RotationControlScale, 0.0001f);
            Assert.Greater(entity.HorizontalVelocity.magnitude, 0f);
            Assert.AreEqual(0f, entity.Transform.YawDegrees, 0.0001f);
        }

[Test]
        public void CharacterMotion_ControlScaleRequest_DoesNotApplyMovementMode()
        {
            CharacterTestCharacterRuntimeWorld world = CreateRuntimeWorldWithLocalPlayer(out CharacterTestCharacterEntity entity);
            var motion = new CharacterTestCharacterMotionSystem();
            motion.SetMovementProfile(CreateMovementRuntime(runSpeed: 4f, acceleration: 100f, deceleration: 100f));
            entity.SetAction(CharacterTestCharacterActionComponent.None);
            entity.SetInput(new CharacterTestCharacterInputComponent(Vector2.right, jumpPressed: false, jumpHeld: true));
            entity.EnqueueMotionRequest(CharacterTestCharacterMotionRequest.ControlScale(
                movementControlScale: 1f,
                rotationControlScale: 0.5f,
                movementMode: CharacterMovementMode.Airborne,
                sourceActionId: MxFramework.Tests.CharacterActionLegacyFixtureIds.From("test.attack"),
                stableEventId: global::MxFramework.Tests.Game.CharacterTest.MigrationFixtureIds.Id0063));

            motion.Tick(world, CharacterTestDeterministicUnits.DefaultTickRate);

            Assert.AreEqual(CharacterMovementMode.Run, entity.MovementMode);
            Assert.AreEqual(1f, entity.MovementControlScale, 0.0001f);
            Assert.AreEqual(0.5f, entity.RotationControlScale, 0.0001f);
        }

[Test]
        public void CharacterMotion_ControlScaleZeroDoesNotBlockRootMotionOrImpulse()
        {
            CharacterTestCharacterRuntimeWorld world = CreateRuntimeWorldWithLocalPlayer(out CharacterTestCharacterEntity entity);
            var motion = new CharacterTestCharacterMotionSystem();
            motion.SetMovementProfile(CreateMovementRuntime(runSpeed: 4f, acceleration: 100f, deceleration: 100f));
            entity.SetInput(new CharacterTestCharacterInputComponent(Vector2.right, jumpPressed: false, jumpHeld: true));
            entity.EnqueueMotionRequest(CharacterTestCharacterMotionRequest.ControlScale(
                movementControlScale: 0f,
                rotationControlScale: 0f,
                movementMode: CharacterMovementMode.Idle,
                sourceActionId: MxFramework.Tests.CharacterActionLegacyFixtureIds.From("test.attack"),
                stableEventId: global::MxFramework.Tests.Game.CharacterTest.MigrationFixtureIds.Id0063));
            entity.EnqueueMotionRequest(CharacterTestCharacterMotionRequest.Impulse(
                new Vector3(2f, 0f, 0f),
                CharacterMovementMode.Idle,
                MxFramework.Tests.CharacterActionLegacyFixtureIds.From("test.attack"),
                global::MxFramework.Tests.Game.CharacterTest.MigrationFixtureIds.Id0064));
            entity.EnqueueMotionRequest(CharacterTestCharacterMotionRequest.RootMotionDelta(
                new Vector3(0f, 0f, 1f),
                CharacterTestCharacterMotionRequestSpace.Local,
                MxFramework.Tests.CharacterActionLegacyFixtureIds.From("test.attack"),
                global::MxFramework.Tests.Game.CharacterTest.MigrationFixtureIds.Id0065));

            motion.Tick(world, CharacterTestDeterministicUnits.DefaultTickRate);

            Assert.IsFalse(entity.MovementLocked);
            Assert.AreEqual(CharacterMovementMode.RootMotionDriven, entity.MovementMode);
            Assert.AreEqual(33, entity.Transform.XMillimeters);
            Assert.AreEqual(1000, entity.Transform.ZMillimeters);
            Assert.AreEqual(2000, entity.HorizontalVelocityXMillimetersPerSecond);
        }

[Test]
        public void CharacterRuntime_LockMovementTrackBlocksHorizontalMovementUntilActionEnds()
        {
            string directory = CreateTempActionConfigDirectory();
            try
            {
                WriteActionJsonFiles(directory, CreateActionSetsJson(), CreateActionsJson(12, lockLightAttackMovement: true));
                using (GameSlice slice = CreateSliceWithActionDirectory(directory))
                {
                    CharacterTestCharacterEntity entity = SpawnLocalPlayer(slice);
                    SetSliceInputSnapshot(slice, CreateInputSnapshot(Vector2.up, attackPrimaryPressed: true));
                    slice.Tick(1d / 60d);

                    Assert.AreEqual(CharacterTestCharacterActionConfigSource.LightAttackActionId, entity.Action.ActiveActionId);
                    Assert.IsTrue(entity.MovementLocked);
                    Assert.AreEqual(0f, entity.HorizontalVelocity.magnitude, 0.0001f);

                    SetSliceInputSnapshot(slice, CreateInputSnapshot(Vector2.up));
                    for (int i = 0; i < 16 && !string.IsNullOrEmpty((entity.Action.ActiveActionId).ToString()); i++)
                        slice.Tick(1d / 60d);
                    slice.Tick(1d / 60d);

                    Assert.IsFalse(entity.MovementLocked);
                    Assert.Greater(entity.HorizontalVelocity.magnitude, 0f);
                }
            }
            finally
            {
                DeleteDirectory(directory);
            }
        }

[Test]
        public void CharacterMotion_FrameworkPhysicsWorld_BlocksWallMovement()
        {
            CharacterTestCharacterRuntimeWorld world = CreateRuntimeWorldWithLocalPlayer(out CharacterTestCharacterEntity entity);
            var motion = new CharacterTestCharacterMotionSystem();
            motion.SetMovementProfile(CreateMovementRuntime(runSpeed: 4f, acceleration: 100f, deceleration: 100f));
            motion.SetPhysicsRuntime(CreateFrameworkPhysicsRuntime(includeWall: true));
            entity.SetInput(new CharacterTestCharacterInputComponent(Vector2.right, jumpPressed: false, jumpHeld: true));

            for (int i = 0; i < 80; i++)
                motion.Tick(world, 1f / 60f);

            Assert.IsTrue(entity.PhysicsEnabled);
            Assert.IsTrue((entity.CollisionFlags & CombatMotionCollisionFlags.Wall) == CombatMotionCollisionFlags.Wall);
            Assert.LessOrEqual(entity.Transform.XMillimeters, 1650);
            Assert.AreEqual(0f, entity.HorizontalVelocity.x, 0.0001f);
        }

[Test]
        public void CharacterMotion_FrameworkPhysicsWorld_BlocksDynamicCharacterMovement()
        {
            CharacterTestCharacterRuntimeWorld world = CreateRuntimeWorldWithLocalPlayer(out CharacterTestCharacterEntity player);
            CharacterTestCharacterDefinition definition = GetIronVanguardDefinition();
            CharacterTestCharacterEntity target = world.Spawn(
                new CharacterTestCharacterSpawnRequest(
                    definition.CharacterId,
                    definition.DefaultTeamId,
                    0.9f,
                    0f,
                    0f,
                    CharacterTestCharacterControlOwner.None),
                definition);
            target.SetMotionState(0f, Vector2.zero, isGrounded: true);

            var motion = new CharacterTestCharacterMotionSystem();
            CharacterTestCharacterPhysicsRuntime physicsRuntime = CreateFrameworkPhysicsRuntime(includeWall: false);
            motion.SetMovementProfile(CreateMovementRuntime(runSpeed: 4f, acceleration: 100f, deceleration: 100f));
            motion.SetPhysicsRuntime(physicsRuntime);
            player.SetInput(new CharacterTestCharacterInputComponent(Vector2.right, jumpPressed: false, jumpHeld: true));

            for (int i = 0; i < 60; i++)
                motion.Tick(world, 1f / 60f);

            float minDistance = player.Physics.EffectiveRadiusMeters + target.Physics.EffectiveRadiusMeters;
            Assert.IsTrue(physicsRuntime.MotionObstacleLayerMask.ContainsLayer(CharacterTestCharacterPhysicsRuntime.DynamicCharacterLayer));
            Assert.IsTrue(player.PhysicsEnabled);
            Assert.GreaterOrEqual(HorizontalDistance(player, target), minDistance - 0.02f);
            Assert.Greater(target.Transform.X, 0.9f);
        }

[Test]
        public void CharacterMotion_FrameworkPhysicsWorld_ContinuousInputPushesDynamicCharacter()
        {
            CharacterTestCharacterRuntimeWorld world = CreateRuntimeWorldWithLocalPlayer(out CharacterTestCharacterEntity player);
            CharacterTestCharacterDefinition definition = GetIronVanguardDefinition();
            CharacterTestCharacterEntity target = world.Spawn(
                new CharacterTestCharacterSpawnRequest(
                    definition.CharacterId,
                    definition.DefaultTeamId,
                    0.9f,
                    0f,
                    0f,
                    CharacterTestCharacterControlOwner.None),
                definition);
            target.SetMotionState(0f, Vector2.zero, isGrounded: true);

            var motion = new CharacterTestCharacterMotionSystem();
            motion.SetMovementProfile(CreateMovementRuntime(runSpeed: 4f, acceleration: 100f, deceleration: 30f));
            motion.SetPhysicsRuntime(CreateFrameworkPhysicsRuntime(includeWall: false));
            player.SetInput(new CharacterTestCharacterInputComponent(Vector2.right, jumpPressed: false, jumpHeld: true));

            float targetStartX = target.Transform.X;
            for (int i = 0; i < 120; i++)
                motion.Tick(world, 1f / 60f);

            float minDistance = player.Physics.EffectiveRadiusMeters + target.Physics.EffectiveRadiusMeters;
            Assert.Greater(target.Transform.X, targetStartX + 0.25f);
            Assert.Greater(target.Physics.ContactVelocity.x, 0.1f);
            Assert.Greater(target.Physics.ContactCount, 0);
            Assert.GreaterOrEqual(HorizontalDistance(player, target), minDistance - 0.02f);
        }

[Test]
        public void CharacterAnimation_ContinuousPushKeepsPassiveTargetIdle()
        {
            CharacterTestCharacterRuntimeWorld world = CreateRuntimeWorldWithLocalPlayer(out CharacterTestCharacterEntity player);
            CharacterTestCharacterDefinition definition = GetIronVanguardDefinition();
            CharacterTestCharacterEntity target = world.Spawn(
                new CharacterTestCharacterSpawnRequest(
                    definition.CharacterId,
                    definition.DefaultTeamId,
                    0f,
                    0f,
                    0.9f,
                    CharacterTestCharacterControlOwner.None),
                definition);
            target.SetMotionState(0f, Vector2.zero, isGrounded: true);

            var motion = new CharacterTestCharacterMotionSystem();
            var animation = new CharacterTestCharacterAnimationSystem();
            CharacterTestCharacterMovementProfileRuntime profile =
                CreateMovementRuntime(runSpeed: 4f, acceleration: 100f, deceleration: 30f);
            motion.SetMovementProfile(profile);
            animation.SetMovementProfile(profile);
            motion.SetPhysicsRuntime(CreateFrameworkPhysicsRuntime(includeWall: false));
            player.SetInput(new CharacterTestCharacterInputComponent(Vector2.up, jumpPressed: false, jumpHeld: true));

            float targetStartZ = target.Transform.Z;
            for (int i = 0; i < 120; i++)
                motion.Tick(world, 1f / 60f);
            animation.Tick(world);

            Assert.Greater(target.Transform.Z, targetStartZ + 0.25f);
            Assert.Greater(target.Physics.ContactVelocity.y, 0.1f);
            Assert.Greater(target.Physics.ContactCount, 0);
            Assert.AreEqual(0f, target.DesiredVelocity.sqrMagnitude, 0.0001f);
            Assert.AreEqual(Vector2.zero, target.Animation.LocomotionBlend);
            Assert.AreEqual(0f, target.Animation.LocomotionSpeed01, 0.0001f);
            Assert.Greater(player.Animation.LocomotionBlend.y, 0.9f);
            Assert.AreEqual(0f, player.Animation.LocomotionBlend.x, 0.0001f);
            Assert.Greater(player.Animation.LocomotionSpeed01, 0.9f);
        }

[Test]
        public void CharacterMotion_StasisZerosActiveControlButAllowsExternalImpulse()
        {
            CharacterTestCharacterRuntimeWorld world =
                CreateRuntimeWorldWithLocalPlayer(out CharacterTestCharacterEntity entity);
            GameplayComponentStore<GameplayStatusComponent> statusStore =
                world.GameplayComponentWorld.GetOrCreateStore<GameplayStatusComponent>();
            statusStore.Set(
                entity.GameplayEntityId,
                new GameplayStatusComponent(new GameplayStatusId(CharacterTestStasisIds.StasisStatusId)));
            var stasisMotion = new CharacterTestMechanicMotionEffectBridge();
            var motion = new CharacterTestCharacterMotionSystem();
            motion.SetMovementProfile(CreateMovementRuntime(runSpeed: 4f, acceleration: 100f, deceleration: 30f));
            motion.SetPhysicsRuntime(CreateFrameworkPhysicsRuntime(includeWall: false));
            entity.SetInput(new CharacterTestCharacterInputComponent(Vector2.right, jumpPressed: false));
            float startX = entity.Transform.X;
            float startRotation = entity.Transform.YawDegrees;

            stasisMotion.Tick(world);
            entity.EnqueueMotionRequest(CharacterTestCharacterMotionRequest.ImpulseRaw(
                60000,
                0,
                0,
                CharacterMovementMode.Idle,
                MxFramework.Tests.CharacterActionLegacyFixtureIds.From("test.external_impulse"),
                global::MxFramework.Tests.Game.CharacterTest.MigrationFixtureIds.Id0070,
                CharacterTestCharacterMotionRequestSpace.World));
            motion.Tick(world, 1f / 60f, RuntimeFrame.Zero);

            Assert.AreEqual(Vector2.zero, entity.DesiredVelocity);
            Assert.AreEqual(0f, entity.MovementControlScale, 0.0001f);
            Assert.AreEqual(0f, entity.RotationControlScale, 0.0001f);
            Assert.AreEqual(startRotation, entity.Transform.YawDegrees, 0.0001f);
            Assert.Greater(entity.Transform.X, startX + 0.5f);
        }

[Test]
        public void CharacterMotion_StunZerosActiveMovementAndRotationControl()
        {
            CharacterTestCharacterRuntimeWorld world =
                CreateRuntimeWorldWithLocalPlayer(out CharacterTestCharacterEntity entity);
            GameplayComponentStore<GameplayStatusComponent> statusStore =
                world.GameplayComponentWorld.GetOrCreateStore<GameplayStatusComponent>();
            statusStore.Set(
                entity.GameplayEntityId,
                new GameplayStatusComponent(new GameplayStatusId(CharacterTestStunIds.StunnedStatusId)));
            var statusMotion = new CharacterTestMechanicMotionEffectBridge();
            var motion = new CharacterTestCharacterMotionSystem();
            motion.SetMovementProfile(CreateMovementRuntime(runSpeed: 4f, acceleration: 100f, deceleration: 30f));
            motion.SetPhysicsRuntime(CreateFrameworkPhysicsRuntime(includeWall: false));
            entity.SetInput(new CharacterTestCharacterInputComponent(Vector2.right, jumpPressed: false));
            float startRotation = entity.Transform.YawDegrees;

            statusMotion.Tick(world);
            motion.Tick(world, 1f / 60f, RuntimeFrame.Zero);

            Assert.AreEqual(Vector2.zero, entity.DesiredVelocity);
            Assert.AreEqual(0f, entity.MovementControlScale, 0.0001f);
            Assert.AreEqual(0f, entity.RotationControlScale, 0.0001f);
            Assert.AreEqual(startRotation, entity.Transform.YawDegrees, 0.0001f);
        }

[Test]
        public void CharacterMotion_RootedWithStunKeepsStunRotationLock()
        {
            CharacterTestCharacterRuntimeWorld world =
                CreateRuntimeWorldWithLocalPlayer(out CharacterTestCharacterEntity entity);
            GameplayComponentStore<GameplayStatusComponent> statusStore =
                world.GameplayComponentWorld.GetOrCreateStore<GameplayStatusComponent>();
            statusStore.Set(
                entity.GameplayEntityId,
                new GameplayStatusComponent(
                    new GameplayStatusId(CharacterTestAreaConfinementIds.AreaConfinedStatusId),
                    new GameplayStatusId(CharacterTestStunIds.StunnedStatusId)));
            var statusMotion = new CharacterTestMechanicMotionEffectBridge();
            var motion = new CharacterTestCharacterMotionSystem();
            motion.SetMovementProfile(CreateMovementRuntime(runSpeed: 4f, acceleration: 100f, deceleration: 30f));
            motion.SetPhysicsRuntime(CreateFrameworkPhysicsRuntime(includeWall: false));
            entity.SetInput(new CharacterTestCharacterInputComponent(Vector2.right, jumpPressed: false));

            statusMotion.Tick(world);
            motion.Tick(world, 1f / 60f, RuntimeFrame.Zero);

            Assert.AreEqual(Vector2.zero, entity.DesiredVelocity);
            Assert.AreEqual(0f, entity.MovementControlScale, 0.0001f);
            Assert.AreEqual(0f, entity.RotationControlScale, 0.0001f);
        }

[Test]
        public void CharacterMotion_FrameworkPhysicsWorld_ContinuousPushUsesMassDifference()
        {
            float heavyPushesLight = RunContinuousPushDistance(
                sourceMassKg: 160f,
                targetMassKg: 40f);
            float lightPushesHeavy = RunContinuousPushDistance(
                sourceMassKg: 40f,
                targetMassKg: 160f);

            Assert.Greater(lightPushesHeavy, 0.05f);
            Assert.Greater(heavyPushesLight, lightPushesHeavy * 2f);
        }

[Test]
        public void CharacterMotion_FrameworkPhysicsWorld_ContinuousPushRespectsUnpushableTarget()
        {
            CharacterTestCharacterRuntimeWorld world = CreateRuntimeWorldWithLocalPlayer(out CharacterTestCharacterEntity player);
            CharacterTestCharacterDefinition definition = GetIronVanguardDefinition();
            CharacterTestCharacterEntity target = world.Spawn(
                new CharacterTestCharacterSpawnRequest(
                    definition.CharacterId,
                    definition.DefaultTeamId,
                    0.9f,
                    0f,
                    0f,
                    CharacterTestCharacterControlOwner.None),
                definition);
            target.SetPhysics(target.Physics.WithRuntimeModifiers(
                massScale: target.Physics.MassScale,
                radiusScale: target.Physics.RadiusScale,
                pushScale: target.Physics.PushScale,
                canBePushed: false));
            target.SetMotionState(0f, Vector2.zero, isGrounded: true);

            var motion = new CharacterTestCharacterMotionSystem();
            motion.SetMovementProfile(CreateMovementRuntime(runSpeed: 4f, acceleration: 100f, deceleration: 30f));
            motion.SetPhysicsRuntime(CreateFrameworkPhysicsRuntime(includeWall: false));
            player.SetInput(new CharacterTestCharacterInputComponent(Vector2.right, jumpPressed: false, jumpHeld: true));

            float targetStartX = target.Transform.X;
            for (int i = 0; i < 120; i++)
                motion.Tick(world, 1f / 60f);

            float minDistance = player.Physics.EffectiveRadiusMeters + target.Physics.EffectiveRadiusMeters;
            Assert.AreEqual(targetStartX, target.Transform.X, 0.02f);
            Assert.AreEqual(0f, target.Physics.ContactVelocity.x, 0.0001f);
            Assert.Greater(target.Physics.ContactCount, 0);
            Assert.GreaterOrEqual(HorizontalDistance(player, target), minDistance - 0.02f);
        }

[Test]
        public void CharacterMotion_FrameworkPhysicsWorld_RootMotionDoesNotPenetrateDynamicCharacter()
        {
            CharacterTestCharacterRuntimeWorld world = CreateRuntimeWorldWithLocalPlayer(out CharacterTestCharacterEntity player);
            CharacterTestCharacterDefinition definition = GetIronVanguardDefinition();
            CharacterTestCharacterEntity target = world.Spawn(
                new CharacterTestCharacterSpawnRequest(
                    definition.CharacterId,
                    definition.DefaultTeamId,
                    0.9f,
                    0f,
                    0f,
                    CharacterTestCharacterControlOwner.None),
                definition);
            target.SetMotionState(0f, Vector2.zero, isGrounded: true);

            var motion = new CharacterTestCharacterMotionSystem();
            motion.SetMovementProfile(CreateMovementRuntime(runSpeed: 4f, acceleration: 100f, deceleration: 100f));
            motion.SetPhysicsRuntime(CreateFrameworkPhysicsRuntime(includeWall: false));
            player.EnqueueMotionRequest(CharacterTestCharacterMotionRequest.RootMotionDelta(
                new Vector3(2f, 0f, 0f),
                CharacterTestCharacterMotionRequestSpace.World,
                MxFramework.Tests.CharacterActionLegacyFixtureIds.From("test.root"),
                global::MxFramework.Tests.Game.CharacterTest.MigrationFixtureIds.Id0077));

            motion.Tick(world, 0.5f);

            float minDistance = player.Physics.EffectiveRadiusMeters + target.Physics.EffectiveRadiusMeters;
            Assert.IsTrue(player.PhysicsEnabled);
            Assert.GreaterOrEqual(HorizontalDistance(player, target), minDistance - 0.02f);
            Assert.Less(player.Transform.X, target.Transform.X);
        }

[Test]
        public void CharacterMotion_FrameworkPhysicsWorld_SeparatesSpawnOverlapByInverseMass()
        {
            CharacterTestCharacterRuntimeWorld world = CreateRuntimeWorldWithLocalPlayer(out CharacterTestCharacterEntity heavy);
            CharacterTestCharacterDefinition definition = GetIronVanguardDefinition();
            CharacterTestCharacterEntity light = world.Spawn(
                new CharacterTestCharacterSpawnRequest(
                    definition.CharacterId,
                    definition.DefaultTeamId,
                    0.2f,
                    0f,
                    0f,
                    CharacterTestCharacterControlOwner.None),
                definition);
            heavy.SetPhysics(CreateTestPhysicsProfile(massKg: 160f));
            light.SetPhysics(CreateTestPhysicsProfile(massKg: 40f));
            heavy.SetMotionState(0f, Vector2.zero, isGrounded: true);
            light.SetMotionState(0f, Vector2.zero, isGrounded: true);

            var motion = new CharacterTestCharacterMotionSystem();
            motion.SetMovementProfile(CreateMovementRuntime(runSpeed: 4f, acceleration: 100f, deceleration: 100f));
            motion.SetPhysicsRuntime(CreateFrameworkPhysicsRuntime(includeWall: false));

            motion.Tick(world, 1f / 60f);

            float minDistance = heavy.Physics.EffectiveRadiusMeters + light.Physics.EffectiveRadiusMeters;
            float heavyMove = Mathf.Abs(heavy.Transform.X);
            float lightMove = Mathf.Abs(light.Transform.X - 0.2f);
            Assert.GreaterOrEqual(HorizontalDistance(heavy, light), minDistance - 0.02f);
            Assert.Greater(lightMove, heavyMove * 2f);
            Assert.Greater(heavy.Physics.ContactCount, 0);
            Assert.Greater(light.Physics.ContactCount, 0);
        }

[Test]
        public void CharacterMotion_FrameworkPhysicsWorld_RuntimePhysicsModifierUpdatesColliderAndSnapshot()
        {
            CharacterTestCharacterRuntimeWorld world = CreateRuntimeWorldWithLocalPlayer(out CharacterTestCharacterEntity entity);
            CharacterTestCharacterPhysicsRuntime physicsRuntime = CreateFrameworkPhysicsRuntime(includeWall: false);
            entity.SetPhysics(entity.Physics.WithRuntimeModifiers(
                massScale: 1.5f,
                radiusScale: 1.25f,
                pushScale: 0.5f));

            var motion = new CharacterTestCharacterMotionSystem();
            motion.SetMovementProfile(CreateMovementRuntime(runSpeed: 4f, acceleration: 100f, deceleration: 100f));
            motion.SetPhysicsRuntime(physicsRuntime);
            motion.Tick(world, 1f / 60f);

            Assert.IsTrue(physicsRuntime.World.TryGetAabbCollider(
                physicsRuntime.GetBodyId(entity),
                new CombatColliderId(CharacterTestCharacterPhysicsRuntime.DynamicCharacterColliderId),
                out CombatPhysicsAabbCollider collider));
            Assert.AreEqual(ToFix64(-entity.Physics.EffectiveRadiusMeters), collider.LocalMin.X);
            Assert.AreEqual(ToFix64(entity.Physics.EffectiveRadiusMeters), collider.LocalMax.X);
            Assert.AreEqual(ToFix64(-entity.Physics.EffectiveHalfHeightMeters), collider.LocalMin.Y);
            Assert.AreEqual(ToFix64(entity.Physics.EffectiveHalfHeightMeters), collider.LocalMax.Y);

            CharacterTestNetworkCharacterSnapshot snapshot =
                CharacterTestNetworkCharacterSnapshot.FromEntity(entity);
            Assert.AreEqual(1500, snapshot.PhysicsMassScaleMilli);
            Assert.AreEqual(1250, snapshot.PhysicsRadiusScaleMilli);
            Assert.AreEqual(500, snapshot.PhysicsPushScaleMilli);
            Assert.AreEqual(entity.Physics.CapsuleRadiusMillimeters, snapshot.PhysicsCapsuleRadiusMillimeters);
            Assert.AreEqual(entity.Physics.MassGrams, snapshot.PhysicsMassGrams);
        }

[Test]
        public void CharacterMotion_FrameworkPhysicsWorld_CombatColliderSyncSkipsUnchangedState()
        {
            CharacterTestCharacterRuntimeWorld world = CreateRuntimeWorldWithLocalPlayer(out CharacterTestCharacterEntity entity);
            CharacterTestCharacterPhysicsRuntime physicsRuntime = CreateFrameworkPhysicsRuntime(includeWall: false);
            var motion = new CharacterTestCharacterMotionSystem();
            motion.SetMovementProfile(CreateMovementRuntime(runSpeed: 4f, acceleration: 100f, deceleration: 100f));
            motion.SetPhysicsRuntime(physicsRuntime);
            ICharacterTestCharacterColliderSync sync = motion;
            CombatBodyId bodyId = physicsRuntime.GetBodyId(entity);
            var colliderId = new CombatColliderId(CharacterTestCharacterPhysicsRuntime.DynamicCharacterColliderId);

            sync.SyncAllCharacterCombatColliders(world.Entities);
            CombatPhysicsWorldStats firstStats = physicsRuntime.World.CreateStats();
            Assert.IsTrue(physicsRuntime.World.TryGetBody(bodyId, out CombatPhysicsBody firstBody));
            Assert.IsTrue(physicsRuntime.World.TryGetAabbCollider(bodyId, colliderId, out _));

            sync.SyncAllCharacterCombatColliders(world.Entities);
            Assert.AreEqual(firstStats, physicsRuntime.World.CreateStats());

            entity.SetTransform(CharacterTestCharacterTransformComponent.FromDeterministic(
                entity.Transform.XMillimeters + 100,
                entity.Transform.YMillimeters,
                entity.Transform.ZMillimeters,
                entity.Transform.YawMilliDegrees));
            sync.SyncAllCharacterCombatColliders(world.Entities);
            CombatPhysicsWorldStats movedStats = physicsRuntime.World.CreateStats();
            Assert.Greater(movedStats.Revision, firstStats.Revision);
            Assert.AreEqual(firstStats.BodyCount, movedStats.BodyCount);
            Assert.AreEqual(firstStats.ColliderCount, movedStats.ColliderCount);
            Assert.IsTrue(physicsRuntime.World.TryGetBody(bodyId, out CombatPhysicsBody movedBody));
            Assert.AreNotEqual(firstBody.Position, movedBody.Position);

            sync.SyncAllCharacterCombatColliders(world.Entities);
            Assert.AreEqual(movedStats, physicsRuntime.World.CreateStats());

            Assert.IsTrue(physicsRuntime.World.RemoveCollider(bodyId, colliderId));
            sync.SyncAllCharacterCombatColliders(world.Entities);
            Assert.IsTrue(physicsRuntime.World.TryGetAabbCollider(bodyId, colliderId, out _));
            CombatPhysicsWorldStats restoredStats = physicsRuntime.World.CreateStats();

            entity.SetPhysics(entity.Physics.WithRuntimeModifiers(
                massScale: entity.Physics.MassScale,
                radiusScale: entity.Physics.RadiusScale,
                pushScale: entity.Physics.PushScale,
                blocksCharacters: false));
            sync.SyncAllCharacterCombatColliders(world.Entities);
            CombatPhysicsWorldStats unblockedStats = physicsRuntime.World.CreateStats();
            Assert.Greater(unblockedStats.Revision, restoredStats.Revision);
            Assert.AreEqual(restoredStats.BodyCount, unblockedStats.BodyCount);
            Assert.AreEqual(restoredStats.ColliderCount - 1, unblockedStats.ColliderCount);
            Assert.IsFalse(physicsRuntime.World.TryGetAabbCollider(bodyId, colliderId, out _));

            sync.SyncAllCharacterCombatColliders(world.Entities);
            Assert.AreEqual(unblockedStats, physicsRuntime.World.CreateStats());
        }

[Test]
        public void CharacterMotion_ContactResolverPreparedLookupResolvesDynamicContactsWithoutSteadyStateAllocation()
        {
            CharacterTestCharacterRuntimeWorld world = CreateRuntimeWorldWithLocalPlayer(out CharacterTestCharacterEntity source);
            CharacterTestCharacterDefinition definition = GetIronVanguardDefinition();
            for (int i = 1; i < 96; i++)
            {
                world.Spawn(
                    new CharacterTestCharacterSpawnRequest(
                        definition.CharacterId,
                        definition.DefaultTeamId,
                        10f + i,
                        0f,
                        0f,
                        CharacterTestCharacterControlOwner.None),
                    definition);
            }

            CharacterTestCharacterEntity target = world.Entities[world.Entities.Count - 1];
            CharacterTestCharacterPhysicsRuntime physicsRuntime = CreateFrameworkPhysicsRuntime(includeWall: false);
            CombatMotionStepResult physicsStep = CreateDynamicCharacterCollisionStep(
                physicsRuntime.GetEntityId(target),
                physicsRuntime.GetBodyId(target));
            var resolver = new CharacterTestCharacterContactResolver(colliderSync: null);

            _ = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < 4; i++)
            {
                resolver.Clear();
                resolver.PrepareDynamicCharacterLookup(world.Entities);
                resolver.RecordDynamicContactHit(
                    source,
                    world,
                    physicsStep,
                    physicsRuntime,
                    attemptedVelocityX: 1000,
                    attemptedVelocityY: 0);
            }

            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < 64; i++)
            {
                resolver.Clear();
                resolver.PrepareDynamicCharacterLookup(world.Entities);
                resolver.RecordDynamicContactHit(
                    source,
                    world,
                    physicsStep,
                    physicsRuntime,
                    attemptedVelocityX: 1000,
                    attemptedVelocityY: 0);
            }

            long allocatedBytes = GC.GetAllocatedBytesForCurrentThread() - before;
            Assert.AreEqual(0L, allocatedBytes);

            resolver.Clear();
            resolver.PrepareDynamicCharacterLookup(world.Entities);
            resolver.RecordDynamicContactHit(
                source,
                world,
                physicsStep,
                physicsRuntime,
                attemptedVelocityX: 1000,
                attemptedVelocityY: 0);
            resolver.ApplyCharacterContactState(world.Entities);

            Assert.AreEqual(1, source.Physics.ContactCount);
            Assert.AreEqual(1, target.Physics.ContactCount);
        }

[Test]
        public void CharacterMotion_ContactResolverPressureUsesSpatialCandidatesWithoutSteadyStateAllocation()
        {
            CharacterTestCharacterRuntimeWorld world = CreateRuntimeWorldWithLocalPlayer(out CharacterTestCharacterEntity source);
            CharacterTestCharacterDefinition definition = GetIronVanguardDefinition();
            CharacterTestCharacterEntity target = world.Spawn(
                new CharacterTestCharacterSpawnRequest(
                    definition.CharacterId,
                    definition.DefaultTeamId,
                    0.65f,
                    0f,
                    0f,
                    CharacterTestCharacterControlOwner.None),
                definition);
            CharacterTestCharacterEntity largeDiagonalDecoy = world.Spawn(
                new CharacterTestCharacterSpawnRequest(
                    definition.CharacterId,
                    definition.DefaultTeamId,
                    5f,
                    0f,
                    5f,
                    CharacterTestCharacterControlOwner.None),
                definition);
            largeDiagonalDecoy.SetPhysics(CharacterTestCharacterPhysicsComponent.FromDeterministic(
                5000,
                largeDiagonalDecoy.Physics.CapsuleHeightMillimeters,
                largeDiagonalDecoy.Physics.CapsuleCenterYMillimeters,
                largeDiagonalDecoy.Physics.MassGrams,
                largeDiagonalDecoy.Physics.MassScaleMilli,
                largeDiagonalDecoy.Physics.RadiusScaleMilli,
                largeDiagonalDecoy.Physics.PushScaleMilli,
                blocksCharacters: true,
                canBePushed: largeDiagonalDecoy.Physics.CanBePushed));
            for (int i = 0; i < 4; i++)
            {
                world.Spawn(
                    new CharacterTestCharacterSpawnRequest(
                        definition.CharacterId,
                        definition.DefaultTeamId,
                        5.1f + i * 0.05f,
                        0f,
                        5.1f,
                        CharacterTestCharacterControlOwner.None),
                    definition);
            }

            for (int i = 2; i < 96; i++)
            {
                world.Spawn(
                    new CharacterTestCharacterSpawnRequest(
                        definition.CharacterId,
                        definition.DefaultTeamId,
                        10f + i,
                        0f,
                        0f,
                        CharacterTestCharacterControlOwner.None),
                    definition);
            }

            var resolver = new CharacterTestCharacterContactResolver(colliderSync: null);

            _ = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < 4; i++)
                RunPressureContactSample(resolver, world, source);

            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < 64; i++)
                RunPressureContactSample(resolver, world, source);

            long allocatedBytes = GC.GetAllocatedBytesForCurrentThread() - before;
            int fullPairCount = world.Entities.Count * (world.Entities.Count - 1) / 2;
            Assert.AreEqual(0L, allocatedBytes);
            Assert.Greater(target.Physics.ContactCount, 0);
            Assert.Greater(resolver.LastPressureCandidatePairCount, 0);
            Assert.Greater(
                resolver.LastPressureSkippedCellCount,
                0,
                "Pressure resolution should skip occupied grid cells whose cell-local max radius cannot reach the source.");
            Assert.Less(
                resolver.LastPressureCandidatePairCount,
                fullPairCount,
                "Pressure resolution should use the XZ grid candidate set instead of scanning all entity pairs.");
            Assert.Less(
                resolver.LastContactStateApplyEntityCount,
                world.Entities.Count,
                "Contact state application should touch only current/previous contact entities after warm-up.");

            resolver.Clear();
            resolver.ApplyCharacterContactState(world.Entities);

            Assert.AreEqual(0, source.Physics.ContactCount);
            Assert.AreEqual(0, target.Physics.ContactCount);
            Assert.Less(
                resolver.LastContactStateApplyEntityCount,
                world.Entities.Count,
                "Clearing a prior contact state should touch only the previous contact entities.");
        }

[Test]
        public void CharacterMotion_ContactResolverPressureSkipsCellAggregateBoundsWithoutSteadyStateAllocation()
        {
            CharacterTestCharacterRuntimeWorld world = CreateRuntimeWorldWithLocalPlayer(out CharacterTestCharacterEntity source);
            source.SetTransform(CharacterTestCharacterTransformComponent.FromDeterministic(
                xMillimeters: 999,
                yMillimeters: 0,
                zMillimeters: 0));
            CharacterTestCharacterDefinition definition = GetIronVanguardDefinition();
            CharacterTestCharacterEntity target = world.Spawn(
                new CharacterTestCharacterSpawnRequest(
                    definition.CharacterId,
                    definition.DefaultTeamId,
                    0.65f,
                    0f,
                    0f,
                    CharacterTestCharacterControlOwner.None),
                definition);
            var decoys = new CharacterTestCharacterEntity[8];
            for (int i = 0; i < decoys.Length; i++)
            {
                CharacterTestCharacterEntity decoy = world.Spawn(
                    new CharacterTestCharacterSpawnRequest(
                        definition.CharacterId,
                        definition.DefaultTeamId,
                        1.99f,
                        0f,
                        i * 0.04f,
                        CharacterTestCharacterControlOwner.None),
                    definition);
                decoy.SetPhysics(CharacterTestCharacterPhysicsComponent.FromDeterministic(
                    CharacterTestCharacterPhysicsComponent.MinimumRadiusMillimeters,
                    decoy.Physics.CapsuleHeightMillimeters,
                    decoy.Physics.CapsuleCenterYMillimeters,
                    decoy.Physics.MassGrams,
                    decoy.Physics.MassScaleMilli,
                    decoy.Physics.RadiusScaleMilli,
                    decoy.Physics.PushScaleMilli,
                    blocksCharacters: true,
                    canBePushed: decoy.Physics.CanBePushed));
                decoys[i] = decoy;
            }

            var resolver = new CharacterTestCharacterContactResolver(colliderSync: null);

            _ = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < 4; i++)
                RunPressureContactSample(resolver, world, source);

            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < 64; i++)
                RunPressureContactSample(resolver, world, source);

            long allocatedBytes = GC.GetAllocatedBytesForCurrentThread() - before;
            int fullPairCount = world.Entities.Count * (world.Entities.Count - 1) / 2;
            Assert.AreEqual(0L, allocatedBytes);
            Assert.Greater(source.Physics.ContactCount, 0);
            Assert.Greater(target.Physics.ContactCount, 0);
            Assert.Greater(
                resolver.LastPressureSkippedCellCount,
                0,
                "Pressure resolution should skip occupied cells whose aggregate character bounds cannot overlap the source query.");
            Assert.Less(
                resolver.LastPressureCandidatePairCount,
                fullPairCount,
                "Pressure resolution should not pair-solve characters from skipped aggregate-bound cells.");
            for (int i = 0; i < decoys.Length; i++)
                Assert.AreEqual(0, decoys[i].Physics.ContactCount);
        }

[Test]
        public void CharacterMotion_ContactResolverPressureSkipsCellVerticalBoundsWithoutSteadyStateAllocation()
        {
            CharacterTestCharacterRuntimeWorld world = CreateRuntimeWorldWithLocalPlayer(out CharacterTestCharacterEntity source);
            source.SetTransform(CharacterTestCharacterTransformComponent.FromDeterministic(
                xMillimeters: 999,
                yMillimeters: 0,
                zMillimeters: 0));
            CharacterTestCharacterDefinition definition = GetIronVanguardDefinition();
            CharacterTestCharacterEntity target = world.Spawn(
                new CharacterTestCharacterSpawnRequest(
                    definition.CharacterId,
                    definition.DefaultTeamId,
                    0.65f,
                    0f,
                    0f,
                    CharacterTestCharacterControlOwner.None),
                definition);
            var decoys = new CharacterTestCharacterEntity[8];
            for (int i = 0; i < decoys.Length; i++)
            {
                decoys[i] = world.Spawn(
                    new CharacterTestCharacterSpawnRequest(
                        definition.CharacterId,
                        definition.DefaultTeamId,
                        1.1f,
                        2.5f,
                        i * 0.04f,
                        CharacterTestCharacterControlOwner.None),
                    definition);
            }

            var resolver = new CharacterTestCharacterContactResolver(colliderSync: null);

            _ = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < 4; i++)
                RunPressureContactSample(resolver, world, source);

            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < 64; i++)
                RunPressureContactSample(resolver, world, source);

            long allocatedBytes = GC.GetAllocatedBytesForCurrentThread() - before;
            int fullPairCount = world.Entities.Count * (world.Entities.Count - 1) / 2;
            Assert.AreEqual(0L, allocatedBytes);
            Assert.Greater(source.Physics.ContactCount, 0);
            Assert.Greater(target.Physics.ContactCount, 0);
            Assert.Greater(
                resolver.LastPressureSkippedCellCount,
                0,
                "Pressure resolution should skip occupied cells whose aggregate vertical range cannot overlap the source.");
            Assert.Less(
                resolver.LastPressureCandidatePairCount,
                fullPairCount,
                "Pressure resolution should not pair-solve characters from skipped vertical-bound cells.");
            for (int i = 0; i < decoys.Length; i++)
                Assert.AreEqual(0, decoys[i].Physics.ContactCount);
        }

[Test]
        public void CharacterMotion_ContactResolverPressureScansActiveCellsForSparseWideQueriesWithoutSteadyStateAllocation()
        {
            CharacterTestCharacterRuntimeWorld world = CreateRuntimeWorldWithLocalPlayer(out CharacterTestCharacterEntity source);
            CharacterTestCharacterDefinition definition = GetIronVanguardDefinition();
            CharacterTestCharacterEntity target = world.Spawn(
                new CharacterTestCharacterSpawnRequest(
                    definition.CharacterId,
                    definition.DefaultTeamId,
                    0.65f,
                    0f,
                    0f,
                    CharacterTestCharacterControlOwner.None),
                definition);
            CharacterTestCharacterEntity largeFarDecoy = world.Spawn(
                new CharacterTestCharacterSpawnRequest(
                    definition.CharacterId,
                    definition.DefaultTeamId,
                    20f,
                    0f,
                    20f,
                    CharacterTestCharacterControlOwner.None),
                definition);
            largeFarDecoy.SetPhysics(CharacterTestCharacterPhysicsComponent.FromDeterministic(
                20000,
                largeFarDecoy.Physics.CapsuleHeightMillimeters,
                largeFarDecoy.Physics.CapsuleCenterYMillimeters,
                largeFarDecoy.Physics.MassGrams,
                largeFarDecoy.Physics.MassScaleMilli,
                largeFarDecoy.Physics.RadiusScaleMilli,
                largeFarDecoy.Physics.PushScaleMilli,
                blocksCharacters: true,
                canBePushed: largeFarDecoy.Physics.CanBePushed));

            var resolver = new CharacterTestCharacterContactResolver(colliderSync: null);

            _ = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < 4; i++)
                RunPressureContactSample(resolver, world, source);

            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < 64; i++)
                RunPressureContactSample(resolver, world, source);

            long allocatedBytes = GC.GetAllocatedBytesForCurrentThread() - before;
            Assert.AreEqual(0L, allocatedBytes);
            Assert.Greater(source.Physics.ContactCount, 0);
            Assert.Greater(target.Physics.ContactCount, 0);
            Assert.AreEqual(0, largeFarDecoy.Physics.ContactCount);
            Assert.Greater(resolver.LastPressureSkippedCellCount, 0);
            Assert.Less(
                resolver.LastPressureQueryCellCount,
                64,
                "Sparse wide pressure queries should scan active contact cells instead of every empty coordinate in the global-radius rectangle.");
        }

[Test]
        public void CharacterMotion_ContactResolverPressureSkipsZeroAttemptedVelocityWithoutSteadyStateAllocation()
        {
            CharacterTestCharacterRuntimeWorld world = CreateRuntimeWorldWithLocalPlayer(out CharacterTestCharacterEntity source);
            CharacterTestCharacterDefinition definition = GetIronVanguardDefinition();
            CharacterTestCharacterEntity target = world.Spawn(
                new CharacterTestCharacterSpawnRequest(
                    definition.CharacterId,
                    definition.DefaultTeamId,
                    0.65f,
                    0f,
                    0f,
                    CharacterTestCharacterControlOwner.None),
                definition);
            var resolver = new CharacterTestCharacterContactResolver(colliderSync: null);

            _ = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < 4; i++)
                RunZeroPressureContactSample(resolver, world, source);

            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < 64; i++)
                RunZeroPressureContactSample(resolver, world, source);

            long allocatedBytes = GC.GetAllocatedBytesForCurrentThread() - before;
            Assert.AreEqual(0L, allocatedBytes);
            Assert.AreEqual(0, resolver.LastPressureQueryCellCount);
            Assert.AreEqual(0, resolver.LastPressureCandidatePairCount);
            Assert.AreEqual(0, resolver.LastContactStateApplyEntityCount);
            Assert.AreEqual(0, source.Physics.ContactCount);
            Assert.AreEqual(0, target.Physics.ContactCount);
        }

[Test]
        public void CharacterMotion_ContactResolverOverlapFiltersNonBlockingCharactersWithoutSteadyStateAllocation()
        {
            CharacterTestCharacterRuntimeWorld world = CreateRuntimeWorldWithLocalPlayer(out CharacterTestCharacterEntity source);
            CharacterTestCharacterDefinition definition = GetIronVanguardDefinition();
            CharacterTestCharacterEntity target = world.Spawn(
                new CharacterTestCharacterSpawnRequest(
                    definition.CharacterId,
                    definition.DefaultTeamId,
                    0.25f,
                    0f,
                    0f,
                    CharacterTestCharacterControlOwner.None),
                definition);
            for (int i = 2; i < 96; i++)
            {
                CharacterTestCharacterEntity entity = world.Spawn(
                    new CharacterTestCharacterSpawnRequest(
                        definition.CharacterId,
                        definition.DefaultTeamId,
                        10f + i,
                        0f,
                        0f,
                        CharacterTestCharacterControlOwner.None),
                    definition);
                entity.SetPhysics(CharacterTestCharacterPhysicsComponent.FromDeterministic(
                    entity.Physics.CapsuleRadiusMillimeters,
                    entity.Physics.CapsuleHeightMillimeters,
                    entity.Physics.CapsuleCenterYMillimeters,
                    entity.Physics.MassGrams,
                    entity.Physics.MassScaleMilli,
                    entity.Physics.RadiusScaleMilli,
                    entity.Physics.PushScaleMilli,
                    blocksCharacters: false,
                    canBePushed: entity.Physics.CanBePushed));
            }

            var resolver = new CharacterTestCharacterContactResolver(colliderSync: null);

            _ = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < 4; i++)
                RunOverlapPressureContactSample(resolver, world, source, target);

            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < 64; i++)
                RunOverlapPressureContactSample(resolver, world, source, target);

            long allocatedBytes = GC.GetAllocatedBytesForCurrentThread() - before;
            int fullPairCount = world.Entities.Count * (world.Entities.Count - 1) / 2;
            Assert.AreEqual(0L, allocatedBytes);
            Assert.Greater(source.Physics.ContactCount, 0);
            Assert.Greater(target.Physics.ContactCount, 0);
            Assert.Greater(resolver.LastOverlapCandidatePairCount, 0);
            Assert.Greater(resolver.LastPressureCandidatePairCount, 0);
            Assert.Less(
                resolver.LastOverlapCandidatePairCount,
                fullPairCount,
                "Overlap resolution should prefilter non-blocking characters before scanning pairs.");
            Assert.Less(
                resolver.LastPressureCandidatePairCount,
                fullPairCount,
                "Pressure resolution should reuse the blocking contact info list instead of scanning all world pairs.");
            Assert.Less(
                resolver.LastContactStateApplyEntityCount,
                world.Entities.Count,
                "Overlap contact state application should touch only current/previous contact entities after warm-up.");
        }

[Test]
        public void CharacterMotion_ContactResolverOverlapUsesSpatialCandidatesWithoutSteadyStateAllocation()
        {
            CharacterTestCharacterRuntimeWorld world = CreateRuntimeWorldWithLocalPlayer(out CharacterTestCharacterEntity source);
            CharacterTestCharacterDefinition definition = GetIronVanguardDefinition();
            CharacterTestCharacterEntity target = world.Spawn(
                new CharacterTestCharacterSpawnRequest(
                    definition.CharacterId,
                    definition.DefaultTeamId,
                    0.25f,
                    0f,
                    0f,
                    CharacterTestCharacterControlOwner.None),
                definition);
            float[,] sameCellDecoyPositions =
            {
                { 0.95f, 0f },
                { 0f, 0.95f },
                { 0.95f, 0.95f },
                { -0.95f, 0f },
                { 0f, -0.95f },
                { -0.95f, -0.95f },
                { 0.95f, -0.95f },
                { -0.95f, 0.95f },
            };
            int sameCellDecoyCount = sameCellDecoyPositions.GetLength(0);
            for (int i = 0; i < sameCellDecoyCount; i++)
            {
                CharacterTestCharacterEntity decoy = world.Spawn(
                    new CharacterTestCharacterSpawnRequest(
                        definition.CharacterId,
                        definition.DefaultTeamId,
                        sameCellDecoyPositions[i, 0],
                        0f,
                        sameCellDecoyPositions[i, 1],
                        CharacterTestCharacterControlOwner.None),
                    definition);
                decoy.SetPhysics(CharacterTestCharacterPhysicsComponent.FromDeterministic(
                    CharacterTestCharacterPhysicsComponent.MinimumRadiusMillimeters,
                    decoy.Physics.CapsuleHeightMillimeters,
                    decoy.Physics.CapsuleCenterYMillimeters,
                    decoy.Physics.MassGrams,
                    decoy.Physics.MassScaleMilli,
                    decoy.Physics.RadiusScaleMilli,
                    decoy.Physics.PushScaleMilli,
                    blocksCharacters: true,
                    canBePushed: decoy.Physics.CanBePushed));
            }

            for (int i = 2; i < 96; i++)
            {
                world.Spawn(
                    new CharacterTestCharacterSpawnRequest(
                        definition.CharacterId,
                        definition.DefaultTeamId,
                        8f + i * 2f,
                        0f,
                        0f,
                        CharacterTestCharacterControlOwner.None),
                    definition);
            }

            var resolver = new CharacterTestCharacterContactResolver(colliderSync: null);

            _ = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < 4; i++)
                RunOverlapContactSample(resolver, world, source, target);

            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < 64; i++)
                RunOverlapContactSample(resolver, world, source, target);

            long allocatedBytes = GC.GetAllocatedBytesForCurrentThread() - before;
            int fullPairCount = world.Entities.Count * (world.Entities.Count - 1) / 2;
            Assert.AreEqual(0L, allocatedBytes);
            Assert.Greater(source.Physics.ContactCount, 0);
            Assert.Greater(target.Physics.ContactCount, 0);
            Assert.Greater(resolver.LastOverlapCandidatePairCount, 0);
            Assert.Less(
                resolver.LastOverlapCandidatePairCount,
                fullPairCount,
                "Overlap resolution should use the XZ grid candidate set instead of scanning every blocking pair.");
            Assert.Less(
                resolver.LastOverlapCandidatePairCount,
                sameCellDecoyCount,
                "Overlap resolution should reject same-cell blockers outside the capsule radius before pair solving.");
        }

[Test]
        public void CharacterMotion_FrameworkPhysicsWorld_LandsOnSceneGround()
        {
            CharacterTestCharacterRuntimeWorld world = CreateRuntimeWorldWithLocalPlayer(out CharacterTestCharacterEntity entity);
            var motion = new CharacterTestCharacterMotionSystem();
            motion.SetMovementProfile(CreateMovementRuntime(gravity: -30f, acceleration: 100f));
            motion.SetPhysicsRuntime(CreateFrameworkPhysicsRuntime(includeWall: false));
            entity.SetTransform(new CharacterTestCharacterTransformComponent(0f, 3f, 0f, 0f));
            entity.SetMotionState(
                verticalVelocity: 0f,
                lastMoveInput: Vector2.zero,
                isGrounded: false);

            for (int i = 0; i < 80 && !entity.IsGrounded; i++)
                motion.Tick(world, 1f / 60f);

            Assert.IsTrue(entity.PhysicsEnabled);
            Assert.IsTrue(entity.IsGrounded);
            Assert.IsTrue((entity.CollisionFlags & CombatMotionCollisionFlags.Grounded) == CombatMotionCollisionFlags.Grounded);
            Assert.AreEqual(0f, entity.Transform.Y, 0.02f);
            Assert.AreEqual(0f, entity.VerticalVelocity, 0.0001f);
        }

[Test]
        public void CharacterMotion_ExternalForceBypassGroundDampingKeepsContinuousPullOnRoughGround()
        {
            CharacterTestCharacterRuntimeWorld regularWorld =
                CreateRuntimeWorldWithLocalPlayer(out CharacterTestCharacterEntity regularEntity);
            CharacterTestCharacterRuntimeWorld bypassWorld =
                CreateRuntimeWorldWithLocalPlayer(out CharacterTestCharacterEntity bypassEntity);
            var regularMotion = new CharacterTestCharacterMotionSystem();
            var bypassMotion = new CharacterTestCharacterMotionSystem();
            CharacterTestCharacterMovementProfileRuntime profile = CreateMovementRuntime(
                runSpeed: 4f,
                acceleration: 100f,
                deceleration: 100f,
                groundFriction: 60f);
            regularMotion.SetMovementProfile(profile);
            bypassMotion.SetMovementProfile(profile);
            regularMotion.SetPhysicsRuntime(CreateFrameworkPhysicsRuntime(includeWall: false, groundSurfaceFriction: Fix64.FromInt(60)));
            bypassMotion.SetPhysicsRuntime(CreateFrameworkPhysicsRuntime(includeWall: false, groundSurfaceFriction: Fix64.FromInt(60)));
            regularEntity.SetInput(CharacterTestCharacterInputComponent.None);
            bypassEntity.SetInput(CharacterTestCharacterInputComponent.None);
            regularEntity.SetMotionState(
                verticalVelocity: 0f,
                lastMoveInput: Vector2.zero,
                isGrounded: true,
                horizontalVelocity: Vector2.zero);
            bypassEntity.SetMotionState(
                verticalVelocity: 0f,
                lastMoveInput: Vector2.zero,
                isGrounded: true,
                horizontalVelocity: Vector2.zero);

            for (int i = 0; i < 20; i++)
            {
                regularEntity.EnqueueMotionRequest(CharacterTestCharacterMotionRequest.ExternalForceRaw(
                    6000,
                    0,
                    0,
                    CharacterTestCharacterMotionRequestSpace.World,
                    MxFramework.Tests.CharacterActionLegacyFixtureIds.From("test.wind"),
                    global::MxFramework.Tests.Game.CharacterTest.MigrationFixtureIds.Id0078));
                bypassEntity.EnqueueMotionRequest(CharacterTestCharacterMotionRequest.ExternalForceRaw(
                    6000,
                    0,
                    0,
                    CharacterTestCharacterMotionRequestSpace.World,
                    MxFramework.Tests.CharacterActionLegacyFixtureIds.From("test.wind"),
                    global::MxFramework.Tests.Game.CharacterTest.MigrationFixtureIds.Id0078,
                    bypassGroundDamping: true));
                regularMotion.Tick(regularWorld, 1f / 60f);
                bypassMotion.Tick(bypassWorld, 1f / 60f);
            }

            Assert.Greater(
                bypassEntity.HorizontalVelocityXMillimetersPerSecond,
                regularEntity.HorizontalVelocityXMillimetersPerSecond + 1000);
            Assert.Greater(
                bypassEntity.Transform.X,
                regularEntity.Transform.X + 0.15f);
            Assert.AreEqual(Vector2.zero, bypassEntity.DesiredVelocity);

            var animation = new CharacterTestCharacterAnimationSystem();
            animation.SetMovementProfile(profile);
            animation.Tick(bypassWorld);

            Assert.AreEqual(Vector2.zero, bypassEntity.Animation.LocomotionBlend);
            Assert.AreEqual(0f, bypassEntity.Animation.LocomotionSpeed01, 0.0001f);
        }

[Test]
        public void CharacterMotion_FrameworkPhysicsWorld_KeepsGroundedWhileWalkingOnSceneGround()
        {
            CharacterTestCharacterRuntimeWorld world = CreateRuntimeWorldWithLocalPlayer(out CharacterTestCharacterEntity entity);
            var motion = new CharacterTestCharacterMotionSystem();
            motion.SetMovementProfile(CreateMovementRuntime(runSpeed: 4f, acceleration: 100f, deceleration: 100f));
            motion.SetPhysicsRuntime(CreateFrameworkPhysicsRuntime(includeWall: false));
            entity.SetInput(new CharacterTestCharacterInputComponent(Vector2.up, jumpPressed: false, jumpHeld: true));

            for (int i = 0; i < 30; i++)
            {
                motion.Tick(world, 1f / 60f);
                Assert.IsTrue(entity.IsGrounded, "Grounded state should not drop while the framework motor is using a contact probe. frame=" + i);
                Assert.IsTrue((entity.CollisionFlags & CombatMotionCollisionFlags.Grounded) == CombatMotionCollisionFlags.Grounded);
            }

            Assert.Greater(entity.Transform.Z, 0f);
        }

[Test]
        public void CharacterMotion_FrameworkPhysicsWorld_SurfaceFrictionAddsGroundDamping()
        {
            CharacterTestCharacterRuntimeWorld lowWorld = CreateRuntimeWorldWithLocalPlayer(out CharacterTestCharacterEntity lowEntity);
            CharacterTestCharacterRuntimeWorld highWorld = CreateRuntimeWorldWithLocalPlayer(out CharacterTestCharacterEntity highEntity);
            var lowMotion = new CharacterTestCharacterMotionSystem();
            var highMotion = new CharacterTestCharacterMotionSystem();
            CharacterTestCharacterMovementProfileRuntime profile = CreateMovementRuntime(
                runSpeed: 4f,
                acceleration: 100f,
                deceleration: 0.1f,
                groundFriction: 0f);
            lowMotion.SetMovementProfile(profile);
            highMotion.SetMovementProfile(profile);
            lowMotion.SetPhysicsRuntime(CreateFrameworkPhysicsRuntime(includeWall: false, groundSurfaceFriction: Fix64.Zero));
            highMotion.SetPhysicsRuntime(CreateFrameworkPhysicsRuntime(includeWall: false, groundSurfaceFriction: Fix64.FromInt(60)));
            lowEntity.SetMotionState(
                verticalVelocity: 0f,
                lastMoveInput: Vector2.up,
                isGrounded: true,
                horizontalVelocity: Vector2.up * 4f);
            highEntity.SetMotionState(
                verticalVelocity: 0f,
                lastMoveInput: Vector2.up,
                isGrounded: true,
                horizontalVelocity: Vector2.up * 4f);
            lowEntity.SetInput(CharacterTestCharacterInputComponent.None);
            highEntity.SetInput(CharacterTestCharacterInputComponent.None);

            for (int i = 0; i < 30; i++)
            {
                lowMotion.Tick(lowWorld, 1f / 60f);
                highMotion.Tick(highWorld, 1f / 60f);
            }

            Assert.AreEqual(0f, lowEntity.SurfaceFriction, 0.0001f);
            Assert.AreEqual(60f, highEntity.SurfaceFriction, 0.0001f);
            Assert.Less(highEntity.HorizontalVelocity.magnitude, lowEntity.HorizontalVelocity.magnitude - 1f);
            Assert.IsTrue(highEntity.IsGrounded);
        }

[Test]
        public void CharacterPhysicsSceneBridge_PropagatesMarkerSurfaceMaterial()
        {
            var root = new GameObject("CharacterTest Physics Bridge Surface Material Test");
            var aabbObject = new GameObject("AABB Surface Material Marker");
            try
            {
                aabbObject.transform.SetParent(root.transform, worldPositionStays: false);
                CharacterTestCombatPhysicsSceneBridge bridge =
                    root.AddComponent<CharacterTestCombatPhysicsSceneBridge>();
                CharacterTestCombatPhysicsAabbMarker aabb =
                    aabbObject.AddComponent<CharacterTestCombatPhysicsAabbMarker>();
                SetPrivateField(aabb, "_bodyId", 321);
                SetPrivateField(aabb, "_colliderId", 3);
                SetPrivateField(aabb, "_surfaceMaterialKind", CombatSurfaceMaterialKind.BouncePad);
                SetPrivateField(aabb, "_surfaceFriction", 17f);
                SetPrivateField(aabb, "_surfaceElasticity", 0f);
                SetPrivateField(aabb, "_surfaceFixedBounceHeight", 4f);

                CharacterTestCharacterPhysicsRuntime runtime = bridge.BuildRuntime();

                Assert.NotNull(runtime);
                StringAssert.Contains("level=", runtime.SourceDescription);
                StringAssert.Contains("hash=", runtime.SourceDescription);
                Assert.IsTrue(runtime.World.TryGetAabbCollider(
                    new CombatBodyId(321),
                    new CombatColliderId(3),
                    out CombatPhysicsAabbCollider aabbCollider));
                Assert.AreEqual(CombatSurfaceMaterialKind.BouncePad, aabbCollider.SurfaceMaterialKind);
                Assert.AreEqual("bounce_pad", aabbCollider.SurfaceMaterialStableId);
                Assert.AreEqual(Fix64.FromInt(17), aabbCollider.SurfaceFriction);
                Assert.AreEqual(Fix64.Zero, aabbCollider.SurfaceElasticity);
                Assert.AreEqual(Fix64.FromInt(4), aabbCollider.SurfaceFixedBounceHeight);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

[Test]
        public void CharacterMotion_FrameworkPhysicsWorld_StepsUpLowAabbStair()
        {
            CharacterTestCharacterRuntimeWorld world = CreateRuntimeWorldWithLocalPlayer(out CharacterTestCharacterEntity entity);
            var motion = new CharacterTestCharacterMotionSystem();
            motion.SetMovementProfile(CreateMovementRuntime(runSpeed: 4f, acceleration: 100f, deceleration: 100f));
            motion.SetPhysicsRuntime(CreateFrameworkPhysicsRuntime(includeWall: false, includeLowStep: true, maxStepHeight: Fix64.FromRatio(35, 100)));
            entity.SetInput(new CharacterTestCharacterInputComponent(Vector2.right, jumpPressed: false, jumpHeld: true));
            bool steppedUp = false;
            var stepHistory = new StringBuilder();

            for (int i = 0; i < 40; i++)
            {
                motion.Tick(world, 1f / 60f);
                steppedUp |= (entity.CollisionFlags & CombatMotionCollisionFlags.StepUp) == CombatMotionCollisionFlags.StepUp;
                if (i % 5 == 0 || entity.Transform.XMillimeters >= 600)
                {
                    stepHistory.Append(" f")
                        .Append(i)
                        .Append(" x=")
                        .Append(entity.Transform.XMillimeters)
                        .Append(" y=")
                        .Append(entity.Transform.YMillimeters)
                        .Append(" vx=")
                        .Append(entity.HorizontalVelocityXMillimetersPerSecond)
                        .Append(" flags=")
                        .Append(entity.CollisionFlags);
                }
            }

            Assert.IsTrue(entity.PhysicsEnabled);
            Assert.IsTrue(
                steppedUp,
                "Expected StepUp flag. x="
                + entity.Transform.XMillimeters
                + " y="
                + entity.Transform.YMillimeters
                + " flags="
                + entity.CollisionFlags
                + stepHistory);
            Assert.Greater(entity.Transform.X, 1f);
            Assert.Greater(entity.Transform.Y, 0.05f);
            Assert.IsTrue(entity.IsGrounded);

            entity.SetInput(CharacterTestCharacterInputComponent.None);
            for (int i = 0; i < 12; i++)
            {
                motion.Tick(world, 1f / 60f);
                Assert.IsTrue(entity.IsGrounded, "Grounded state should stay stable after stepping onto an elevated AABB. frame=" + i);
                Assert.IsTrue((entity.CollisionFlags & CombatMotionCollisionFlags.Grounded) == CombatMotionCollisionFlags.Grounded);
            }
        }

[Test]
        public void CharacterRuntimeView_BodyPartContactPointUsesImportedMarkerWhenPresent()
        {
            var root = new GameObject("character-test-contact-root");
            var socket = new GameObject("Socket_foot.L").transform;
            var marker = new GameObject("ContactPoint_contact.foot_l.sole").transform;
            try
            {
                socket.SetParent(root.transform, false);
                socket.SetPositionAndRotation(
                    new Vector3(1f, 2f, 3f),
                    Quaternion.Euler(0f, 90f, 0f));
                marker.SetParent(socket, false);
                marker.localPosition = new Vector3(0.11f, 0.22f, 0.33f);

                CharacterBoneRuntimeSync boneSync = root.AddComponent<CharacterBoneRuntimeSync>();
                ConfigureBoneRuntimeSyncBinding(boneSync, "socket:foot.L", socket);
                var view = new CharacterTestRuntimeCharacterView(
                    new CharacterTestCharacterEntityId(1),
                    root,
                    null,
                    null,
                    null,
                    boneSync);
                var config = new MxFramework.CharacterApplication.CharacterBodyPartContactPointConfig(
                    new MxFramework.CharacterApplication.CharacterBodyPartContactPointConfigId(830001),
                    "contact.foot_l.sole",
                    "test_parts",
                    MxFramework.Game.CharacterTest.CharacterTestAuthoredContentIds.D400_Id51,
                    1,
                    701,
                    900,
                    800,
                    700,
                    90,
                    "ground",
                    MxFramework.CharacterApplication.CharacterBodyPartContactActivationMode.Always,
                    MxFramework.CharacterApplication.CharacterBodyPartContactMotionMode.BakedSocket,
                    MxFramework.Game.CharacterTest.CharacterTestAuthoredContentIds.D400_Id50,
                    "PresentationAudio",
                    "PresentationOnly",
                    1);

                Assert.IsTrue(view.TryResolveBodyPartContactPointWorldPosition(config, out Vector3 worldPosition));
                Assert.AreEqual(marker.position.x, worldPosition.x, 0.0001f);
                Assert.AreEqual(marker.position.y, worldPosition.y, 0.0001f);
                Assert.AreEqual(marker.position.z, worldPosition.z, 0.0001f);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

[Test]
        public void CharacterRuntimeView_TargetLockUiAnchorKeepsRootHorizontalPositionWhenRendererBoundsShift()
        {
            var root = new GameObject("character-test-target-lock-anchor-root");
            try
            {
                root.transform.position = new Vector3(10f, 0f, 20f);
                GameObject rendererHost = GameObject.CreatePrimitive(PrimitiveType.Cube);
                rendererHost.name = "character-test-target-lock-anchor-renderer";
                rendererHost.transform.SetParent(root.transform, false);
                rendererHost.transform.localPosition = new Vector3(4f, 1f, -3f);
                rendererHost.transform.localScale = new Vector3(2f, 2f, 2f);

                var view = new CharacterTestRuntimeCharacterView(
                    new CharacterTestCharacterEntityId(1),
                    root,
                    null,
                    null,
                    null);

                Assert.IsTrue(view.TryResolveTargetLockUiAnchor(out Vector3 position));
                Assert.AreEqual(root.transform.position.x, position.x, 0.0001f);
                Assert.AreEqual(root.transform.position.z, position.z, 0.0001f);
                Assert.Greater(position.y, root.transform.position.y);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

[Test]
        public void CharacterRuntimeView_ClearedActionResumesLocomotion()
        {
            var go = new GameObject("character-test-view-animation");
            try
            {
                CharacterRuntimeLocomotionBlendController locomotion =
                    go.AddComponent<CharacterRuntimeLocomotionBlendController>();
                var view = new CharacterTestRuntimeCharacterView(
                    new CharacterTestCharacterEntityId(1),
                    go,
                    null,
                    null,
                    locomotion);

                SetPrivateField(
                    locomotion,
                    "_lastActionKey",
                    CharacterTestCharacterActionConfigSource.LightAttackAnimationClipResourceKey);
                locomotion.SuppressLocomotionBlendForSeconds(10f);

                view.SyncAnimation(new CharacterTestCharacterAnimationComponent(
                    Vector2.zero,
                    0f,
                    grounded: true,
                    airborne: false,
                    actionActive: false));

                Assert.AreEqual(string.Empty, locomotion.LastActionKey);
                Assert.IsFalse(locomotion.IsLocomotionBlendSuppressed);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(go);
            }
        }

[Test]
        public void CharacterRuntimeView_NetworkPresentationInterpolatesSnapshotSamples()
        {
            var go = new GameObject("character-test-view-network-presentation");
            try
            {
                var view = new CharacterTestRuntimeCharacterView(
                    new CharacterTestCharacterEntityId(1),
                    go,
                    null,
                    null,
                    null);

                view.SyncTransform(
                    Vector3.zero,
                    Quaternion.identity,
                    smoothNetworkSnapshot: true,
                    frame: new RuntimeFrame(10));
                view.SyncTransform(
                    new Vector3(2f, 0f, 0f),
                    Quaternion.identity,
                    smoothNetworkSnapshot: true,
                    frame: new RuntimeFrame(12));

                view.TickNetworkPresentation(1d / 60d, tickRate: 60, interpolationDelayFrames: 1);

                Assert.AreEqual(1f, go.transform.position.x, 0.001f);

                view.SyncTransform(
                    new Vector3(4f, 0f, 0f),
                    Quaternion.identity,
                    smoothNetworkSnapshot: false);

                Assert.AreEqual(4f, go.transform.position.x, 0.001f);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(go);
            }
        }

[Test]
        public void CharacterRuntimeView_SyncTransformSkipsUnchangedPose()
        {
            var go = new GameObject("character-test-view-transform-dirty");
            try
            {
                var view = new CharacterTestRuntimeCharacterView(
                    new CharacterTestCharacterEntityId(1),
                    go,
                    null,
                    null,
                    null);
                var position = new Vector3(3f, 0f, 2f);
                Quaternion rotation = Quaternion.Euler(0f, 45f, 0f);

                view.SyncTransform(position, rotation);
                view.SyncTransform(position, rotation);

                Assert.AreEqual(1, view.TransformApplyCount);
                Assert.AreEqual(position, go.transform.position);
                Assert.That(
                    Mathf.Abs(Quaternion.Dot(rotation, go.transform.rotation)),
                    Is.EqualTo(1f).Within(0.0001f));

                Quaternion nextRotation = Quaternion.Euler(0f, 90f, 0f);
                view.SyncTransform(position, nextRotation);

                Assert.AreEqual(2, view.TransformApplyCount);
                Assert.That(
                    Mathf.Abs(Quaternion.Dot(nextRotation, go.transform.rotation)),
                    Is.EqualTo(1f).Within(0.0001f));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(go);
            }
        }

[Test]
        public void CharacterRuntimeView_HeadPadAnchorResolutionIsCached()
        {
            var go = new GameObject("character-test-view-headpad-anchor-cache");
            var head = new GameObject("Head");
            CharacterTestRuntimeCharacterView view = null;
            try
            {
                head.transform.SetParent(go.transform, worldPositionStays: false);
                head.transform.localPosition = new Vector3(0f, 1.8f, 0f);
                CharacterAbstractBoneRuntimeMap map = go.AddComponent<CharacterAbstractBoneRuntimeMap>();
                map.Configure(new[]
                {
                    new CharacterAbstractBoneRuntimeBinding(CharacterAbstractBoneIds.Head, MxFramework.Game.CharacterTest.CharacterTestAuthoredContentIds.D400_Id54, head.transform)
                });
                view = new CharacterTestRuntimeCharacterView(
                    new CharacterTestCharacterEntityId(1),
                    go,
                    null,
                    null,
                    null);
                CharacterTestCharacterCombatStateComponent combatState =
                    CharacterTestCharacterCombatStateComponent.CreateDefault();

                view.SyncCombatState(
                    combatState,
                    "Anchor Cache",
                    teamId: 1,
                    controlOwner: CharacterTestCharacterControlOwner.LocalPlayer,
                    hasLocalPlayerTeam: true,
                    localPlayerTeamId: 1);
                view.SyncCombatState(
                    combatState,
                    "Anchor Cache",
                    teamId: 1,
                    controlOwner: CharacterTestCharacterControlOwner.LocalPlayer,
                    hasLocalPlayerTeam: true,
                    localPlayerTeamId: 1);

                Assert.AreEqual(1, view.HeadPadAnchorResolveCount);
                Assert.AreEqual(0, view.RendererBoundsResolveCount);
            }
            finally
            {
                view?.Release();
                if (go != null)
                    UnityEngine.Object.DestroyImmediate(go);
            }
        }

[Test]
        public void CharacterRuntimeView_MissingHeadSemanticDoesNotUseRendererBoundsFallback()
        {
            var go = new GameObject("character-test-view-renderer-anchor-cache");
            CharacterTestRuntimeCharacterView view = null;
            try
            {
                go.AddComponent<MeshRenderer>();
                view = new CharacterTestRuntimeCharacterView(
                    new CharacterTestCharacterEntityId(1),
                    go,
                    null,
                    null,
                    null);
                CharacterTestCharacterCombatStateComponent combatState =
                    CharacterTestCharacterCombatStateComponent.CreateDefault();

                view.SyncCombatState(
                    combatState,
                    "Renderer Cache",
                    teamId: 1,
                    controlOwner: CharacterTestCharacterControlOwner.LocalPlayer,
                    hasLocalPlayerTeam: true,
                    localPlayerTeamId: 1);
                view.SyncCombatState(
                    combatState,
                    "Renderer Cache",
                    teamId: 1,
                    controlOwner: CharacterTestCharacterControlOwner.LocalPlayer,
                    hasLocalPlayerTeam: true,
                    localPlayerTeamId: 1);

                Assert.AreEqual(1, view.HeadPadAnchorResolveCount);
                Assert.AreEqual(0, view.RendererBoundsResolveCount);
            }
            finally
            {
                view?.Release();
                if (go != null)
                    UnityEngine.Object.DestroyImmediate(go);
            }
        }

[Test]
        public void CharacterRuntimeView_NetworkPresentationCatchesUpWhenBacklogged()
        {
            var go = new GameObject("character-test-view-network-presentation-catch-up");
            try
            {
                var view = new CharacterTestRuntimeCharacterView(
                    new CharacterTestCharacterEntityId(1),
                    go,
                    null,
                    null,
                    null);

                view.SyncTransform(
                    Vector3.zero,
                    Quaternion.identity,
                    smoothNetworkSnapshot: true,
                    frame: new RuntimeFrame(10));
                view.SyncTransform(
                    new Vector3(2f, 0f, 0f),
                    Quaternion.identity,
                    smoothNetworkSnapshot: true,
                    frame: new RuntimeFrame(40));

                view.TickNetworkPresentation(1d / 60d, tickRate: 60, interpolationDelayFrames: 2);

                Assert.Greater(go.transform.position.x, 1.5f);
                Assert.Less(go.transform.position.x, 2f);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(go);
            }
        }

[Test]
        public void CharacterRuntimeView_RunningActionWithoutNewEventKeepsCurrentActionAnimation()
        {
            var go = new GameObject("character-test-view-running-action");
            try
            {
                CharacterRuntimeLocomotionBlendController locomotion =
                    go.AddComponent<CharacterRuntimeLocomotionBlendController>();
                var view = new CharacterTestRuntimeCharacterView(
                    new CharacterTestCharacterEntityId(1),
                    go,
                    null,
                    null,
                    locomotion);

                SetPrivateField(
                    locomotion,
                    "_lastActionKey",
                    CharacterTestCharacterActionConfigSource.LightAttackAnimationClipResourceKey);
                locomotion.SuppressLocomotionBlendForSeconds(10f);

                view.SyncAnimation(new CharacterTestCharacterAnimationComponent(
                    Vector2.zero,
                    0f,
                    grounded: false,
                    airborne: true,
                    actionActive: true));

                Assert.AreEqual(
                    CharacterTestCharacterActionConfigSource.LightAttackAnimationClipResourceKey,
                    locomotion.LastActionKey);
                Assert.IsTrue(locomotion.IsLocomotionBlendSuppressed);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(go);
            }
        }

[Test]
        public void CharacterRuntimeView_RunningActionRefreshesSuppressionAfterOneFrameAnimationEvent()
        {
            var go = new GameObject("character-test-view-running-action-refresh");
            try
            {
                CharacterRuntimeLocomotionBlendController locomotion =
                    go.AddComponent<CharacterRuntimeLocomotionBlendController>();
                var view = new CharacterTestRuntimeCharacterView(
                    new CharacterTestCharacterEntityId(1),
                    go,
                    null,
                    null,
                    locomotion);

                SetPrivateField(
                    locomotion,
                    "_lastActionKey",
                    CharacterTestCharacterActionConfigSource.LightAttackAnimationClipResourceKey);
                locomotion.SuppressLocomotionBlendForSeconds(0.01f);
                locomotion.TickRuntimeAnimation(0.02f, 1);
                Assert.IsFalse(locomotion.IsLocomotionBlendSuppressed);

                view.SyncAnimation(new CharacterTestCharacterAnimationComponent(
                    Vector2.zero,
                    0f,
                    grounded: true,
                    airborne: false,
                    actionActive: true));

                Assert.AreEqual(
                    CharacterTestCharacterActionConfigSource.LightAttackAnimationClipResourceKey,
                    locomotion.LastActionKey);
                Assert.IsTrue(locomotion.IsLocomotionBlendSuppressed);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(go);
            }
        }

[Test]
        public void CharacterRuntimeView_JumpUsesTakeoffSuppressionAndDoesNotRefreshWithoutNewEvent()
        {
            var go = new GameObject("character-test-view-jump-suppression");
            try
            {
                CharacterRuntimeLocomotionBlendController locomotion =
                    go.AddComponent<CharacterRuntimeLocomotionBlendController>();
                var view = new CharacterTestRuntimeCharacterView(
                    new CharacterTestCharacterEntityId(1),
                    go,
                    null,
                    null,
                    locomotion);
                var jump = new CharacterTestCharacterAnimationComponent(
                    Vector2.zero,
                    0f,
                    grounded: false,
                    airborne: true,
                    actionActive: true,
                    actionKey: CharacterTestCharacterActionConfigSource.JumpAnimationClipResourceKey,
                    actionTransitionSeconds: 0.08f,
                    actionHoldSeconds: 1.8f);

                Assert.AreEqual(
                    0.25f,
                    CharacterTestRuntimeCharacterView.ResolveLocomotionSuppressionSeconds(jump),
                    0.0001f);

                SetPrivateField(
                    locomotion,
                    "_lastActionKey",
                    CharacterTestCharacterActionConfigSource.JumpAnimationClipResourceKey);
                locomotion.SuppressLocomotionBlendForSeconds(0.01f);
                locomotion.TickRuntimeAnimation(0.02f, 1);
                Assert.IsFalse(locomotion.IsLocomotionBlendSuppressed);

                view.SyncAnimation(new CharacterTestCharacterAnimationComponent(
                    Vector2.zero,
                    0f,
                    grounded: false,
                    airborne: true,
                    actionActive: true));

                Assert.AreEqual(
                    CharacterTestCharacterActionConfigSource.JumpAnimationClipResourceKey,
                    locomotion.LastActionKey);
                Assert.IsFalse(locomotion.IsLocomotionBlendSuppressed);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(go);
            }
        }

[Test]
        public void CharacterRuntimeView_LightAttackSuppressionUsesFullHoldAndRefreshesWithoutNewEvent()
        {
            var go = new GameObject("character-test-view-light-attack-suppression");
            try
            {
                CharacterRuntimeLocomotionBlendController locomotion =
                    go.AddComponent<CharacterRuntimeLocomotionBlendController>();
                var view = new CharacterTestRuntimeCharacterView(
                    new CharacterTestCharacterEntityId(1),
                    go,
                    null,
                    null,
                    locomotion);
                var lightAttack = new CharacterTestCharacterAnimationComponent(
                    Vector2.zero,
                    0f,
                    grounded: true,
                    airborne: false,
                    actionActive: true,
                    actionKey: CharacterTestCharacterActionConfigSource.LightAttackAnimationClipResourceKey,
                    actionTransitionSeconds: 0.12f,
                    actionHoldSeconds: 1.25f);
                var death = new CharacterTestCharacterAnimationComponent(
                    Vector2.zero,
                    0f,
                    grounded: true,
                    airborne: false,
                    actionActive: true,
                    actionKey: CharacterTestCharacterActionConfigSource.DeathReactionAnimationClipResourceKey,
                    actionTransitionSeconds: 0.12f,
                    actionHoldSeconds: 1.25f);

                Assert.AreEqual(
                    1.25f,
                    CharacterTestRuntimeCharacterView.ResolveLocomotionSuppressionSeconds(lightAttack),
                    0.0001f);
                Assert.AreEqual(
                    1.25f,
                    CharacterTestRuntimeCharacterView.ResolveLocomotionSuppressionSeconds(death),
                    0.0001f);

                SetPrivateField(
                    locomotion,
                    "_lastActionKey",
                    CharacterTestCharacterActionConfigSource.LightAttackAnimationClipResourceKey);
                locomotion.SuppressLocomotionBlendForSeconds(0.01f);
                locomotion.TickRuntimeAnimation(0.02f, 1);
                Assert.IsFalse(locomotion.IsLocomotionBlendSuppressed);

                view.SyncAnimation(new CharacterTestCharacterAnimationComponent(
                    Vector2.zero,
                    0f,
                    grounded: true,
                    airborne: false,
                    actionActive: true));

                Assert.AreEqual(
                    CharacterTestCharacterActionConfigSource.LightAttackAnimationClipResourceKey,
                    locomotion.LastActionKey);
                Assert.IsTrue(locomotion.IsLocomotionBlendSuppressed);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(go);
            }
        }

[Test]
        public void CharacterRuntimeView_RepeatedAnimationSyncDoesNotAllocateAfterWarmup()
        {
            var go = new GameObject("character-test-view-animation-perf");
            try
            {
                CharacterRuntimeLocomotionBlendController locomotion =
                    go.AddComponent<CharacterRuntimeLocomotionBlendController>();
                var view = new CharacterTestRuntimeCharacterView(
                    new CharacterTestCharacterEntityId(1),
                    go,
                    null,
                    null,
                    locomotion);
                CharacterTestTraceRecorder traceRecorder =
                    CharacterTestTraceRecorder.CreateInMemory(capacity: 16);
                var animation = new CharacterTestCharacterAnimationComponent(
                    new Vector2(0.25f, 0.75f),
                    0.8f,
                    grounded: true,
                    airborne: false,
                    actionActive: false);

                for (int i = 0; i < 8; i++)
                {
                    view.SyncAnimation(animation, new RuntimeFrame(i), traceRecorder);
                }

                Assert.AreEqual(1, traceRecorder.RecentEvents.Count);
                _ = GC.GetAllocatedBytesForCurrentThread();
                long before = GC.GetAllocatedBytesForCurrentThread();
                for (int i = 0; i < 64; i++)
                {
                    view.SyncAnimation(animation, new RuntimeFrame(16 + i), traceRecorder);
                }

                long allocatedBytes = GC.GetAllocatedBytesForCurrentThread() - before;
                Assert.AreEqual(0L, allocatedBytes);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(go);
            }
        }

[Test]
        public void CharacterRuntimeLocomotionBlendController_MissingInputControllerDoesNotAllocateAfterWarmup()
        {
            var go = new GameObject("character-test-locomotion-missing-input-perf");
            try
            {
                CharacterRuntimeLocomotionBlendController locomotion =
                    go.AddComponent<CharacterRuntimeLocomotionBlendController>();
                locomotion.UseExternalRuntimeTick = true;
                locomotion.SetExternalMotion(new Vector2(0.25f, 0.75f), 0.8f);

                for (int i = 0; i < 8; i++)
                {
                    locomotion.TickRuntimeAnimation(1f / 60f, i);
                }

                Assert.Greater(locomotion.Blend.sqrMagnitude, 0f);
                Assert.Greater(locomotion.Speed01, 0f);

                _ = GC.GetAllocatedBytesForCurrentThread();
                long before = GC.GetAllocatedBytesForCurrentThread();
                for (int i = 0; i < 64; i++)
                {
                    locomotion.TickRuntimeAnimation(1f / 60f, 16 + i);
                }

                long allocatedBytes = GC.GetAllocatedBytesForCurrentThread() - before;
                Assert.AreEqual(0L, allocatedBytes);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(go);
            }
        }

[Test]
        public void CharacterRuntimeLocomotionBlendController_PlaybackCompensationDoesNotAllocateAfterWarmup()
        {
            var go = new GameObject("character-test-locomotion-playback-compensation-perf");
            try
            {
                CharacterRuntimeLocomotionBlendController locomotion =
                    go.AddComponent<CharacterRuntimeLocomotionBlendController>();
                var idle = new ResourceKey("demo.animation.idle", ResourceTypeIds.AnimationClip);
                var right = new ResourceKey("demo.animation.right", ResourceTypeIds.AnimationClip);
                var forward = new ResourceKey("demo.animation.forward", ResourceTypeIds.AnimationClip);
                var diagonal = new ResourceKey("demo.animation.diagonal", ResourceTypeIds.AnimationClip);
                var blend = new MxAnimationBlend2DDefinition(
                    "locomotion2d",
                    "move.x",
                    "move.y",
                    MxAnimationLayerId.Base,
                    new[]
                    {
                        new MxAnimationBlend2DPoint(0, 0, idle),
                        new MxAnimationBlend2DPoint(1000, 0, right),
                        new MxAnimationBlend2DPoint(0, 1000, forward),
                        new MxAnimationBlend2DPoint(1000, 1000, diagonal)
                    });
                SetPrivateField(locomotion, "_blend2DDefinition", blend);
                SetPrivateField(locomotion, "_blendXParameter", "move.x");
                SetPrivateField(locomotion, "_blendYParameter", "move.y");
                SetPrivateField(locomotion, "_blendParameterScale", 1000);

                MethodInfo method = typeof(CharacterRuntimeLocomotionBlendController).GetMethod(
                    "CalculatePlaybackSpeedCompensation",
                    BindingFlags.Instance | BindingFlags.NonPublic);
                Assert.IsNotNull(method);
                var calculate =
                    (Func<int, int, int, int, int, float>)Delegate.CreateDelegate(
                        typeof(Func<int, int, int, int, int, float>),
                        locomotion,
                        method);

                for (int i = 0; i < 8; i++)
                    CalculatePlaybackCompensationSample(calculate, i);

                _ = GC.GetAllocatedBytesForCurrentThread();
                long before = GC.GetAllocatedBytesForCurrentThread();
                for (int i = 0; i < 128; i++)
                    CalculatePlaybackCompensationSample(calculate, i);
                long allocatedBytes = GC.GetAllocatedBytesForCurrentThread() - before;

                Assert.AreEqual(0L, allocatedBytes);
                float result = calculate(0, 1200, 0, 1000, 1000);
                Assert.AreEqual(1.2f, result, 0.0001f);
                Assert.AreEqual(1f, locomotion.LastBlendNodeSpeed, 0.0001f);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(go);
            }
        }

        private static float CalculatePlaybackCompensationSample(
            Func<int, int, int, int, int, float> calculate,
            int index)
        {
            switch (index & 3)
            {
                case 0:
                    return calculate(0, 1200, 0, 1000, 1000);
                case 1:
                    return calculate(1200, 0, 1000, 0, 1000);
                case 2:
                    return calculate(850, 850, 850, 850, 1000);
                default:
                    return calculate(-900, 300, 0, 300, 1000);
            }
        }

[Test]
        public void CharacterActionReload_Success_ReplacesRuntimeFromExternalJson()
        {
            string directory = CreateTempActionConfigDirectory();
            try
            {
                WriteActionJsonFiles(directory, durationFrames: 12);
                using (GameSlice slice = CreateSliceWithActionDirectory(directory))
                {
                    AssertJumpDuration(slice.ActionRuntime, 12);

                    WriteActionJsonFiles(directory, durationFrames: 18);
                    CharacterTestCharacterActionReloadResult result =
                        slice.ReloadCharacterActionConfig();

                    Assert.IsTrue(result.Success, result.Message);
                    Assert.AreEqual(slice.CurrentFrame, result.Frame);
                    Assert.IsTrue(slice.LastActionReloadResult.HasValue);
                    Assert.IsTrue(slice.LastActionReloadResult.Value.Success);
                    AssertJumpDuration(slice.ActionRuntime, 18);
                }
            }
            finally
            {
                DeleteDirectory(directory);
            }
        }

[Test]
        public void CharacterActionReload_Success_ClearsActiveActionAndAllowsRestart()
        {
            string directory = CreateTempActionConfigDirectory();
            try
            {
                WriteActionJsonFiles(directory, durationFrames: 12);
                using (GameSlice slice = CreateSliceWithActionDirectory(directory))
                {
                    CharacterTestCharacterEntity entity = SpawnLocalPlayer(slice);
                    Assert.IsTrue(slice.ActionRuntime.TryStartIntent(
                        entity,
                        CharacterTestCharacterActionConfigSource.JumpIntentId,
                        slice.CurrentFrame), "Initial jump rejected: " + entity.Action.LastRejectReason);
                    Assert.AreEqual(CharacterTestCharacterActionConfigSource.JumpActionId, entity.Action.ActiveActionId);
                    Assert.AreEqual(MxFramework.Game.CharacterTest.CharacterTestActionStateId.Running, entity.Action.State);
                    CharacterTestCharacterActionRuntime previousRuntime = slice.ActionRuntime;

                    WriteActionJsonFiles(directory, durationFrames: 18);
                    CharacterTestCharacterActionReloadResult result =
                        slice.ReloadCharacterActionConfig();

                    Assert.IsTrue(result.Success, result.Message);
                    Assert.AreEqual(CharacterActionId.None, entity.Action.ActiveActionId);
                    Assert.AreEqual(CharacterTestActionStateId.None, entity.Action.State);
                    AssertJumpDuration(slice.ActionRuntime, 18);
                    int reloadTerminationCount = 0;
                    for (int eventIndex = 0; eventIndex < previousRuntime.TimelineEvents.Count; eventIndex++)
                    {
                        CharacterTestCharacterActionTimelineEvent evt = previousRuntime.TimelineEvents[eventIndex];
                        if (evt.Kind == CharacterActionRunnerEventKind.ActionCancelled
                            && evt.TraceId.KindId == 90810)
                        {
                            reloadTerminationCount++;
                        }
                    }
                    Assert.AreEqual(1, reloadTerminationCount);

                    Assert.IsTrue(slice.ActionRuntime.TryStartIntent(
                        entity,
                        CharacterTestCharacterActionConfigSource.JumpIntentId,
                        slice.CurrentFrame), "Restarted jump rejected: " + entity.Action.LastRejectReason);
                    Assert.AreEqual(CharacterTestCharacterActionConfigSource.JumpActionId, entity.Action.ActiveActionId);
                    Assert.AreEqual(MxFramework.Game.CharacterTest.CharacterTestActionStateId.Running, entity.Action.State);
                }
            }
            finally
            {
                DeleteDirectory(directory);
            }
        }

[Test]
        public void CharacterActionReload_Failure_KeepsPreviousRuntime()
        {
            string directory = CreateTempActionConfigDirectory();
            try
            {
                WriteActionJsonFiles(directory, durationFrames: 12);
                using (GameSlice slice = CreateSliceWithActionDirectory(directory))
                {
                    CharacterTestCharacterActionRuntime original = slice.ActionRuntime;
                    AssertJumpDuration(original, 12);

                    WriteActionJsonFiles(
                        directory,
                        CreateActionSetsJson(),
                        CreateActionsJsonWithLightAttackCancelRule(
                            jumpDurationFrames: 18,
                            startFrame: 1,
                            endFrame: 3));

                    CharacterTestCharacterActionReloadResult result =
                        slice.ReloadCharacterActionConfig();

                    Assert.IsFalse(result.Success);
                    StringAssert.Contains(CharacterActionDiagnosticCodes.InvalidCancelWindow, result.Message);
                    StringAssert.Contains("actions[light_attack].cancelRules[0]", result.Message);
                    StringAssert.Contains("1-3", result.Message);
                    Assert.AreSame(original, slice.ActionRuntime);
                    Assert.IsTrue(slice.LastActionReloadResult.HasValue);
                    Assert.IsFalse(slice.LastActionReloadResult.Value.Success);
                    AssertJumpDuration(slice.ActionRuntime, 12);
                }
            }
            finally
            {
                DeleteDirectory(directory);
            }
        }
}
}
