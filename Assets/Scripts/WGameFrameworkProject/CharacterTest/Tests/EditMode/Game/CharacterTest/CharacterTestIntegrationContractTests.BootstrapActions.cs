using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using MxFramework.Audio;
using MxFramework.Camera;
using MxFramework.Camera.Unity;
using MxFramework.CharacterAction;
using MxFramework.CharacterControl;
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
        [Test]
        public void CharacterActionConfig_DefaultDirectoriesIncludePackageActionLibrary()
        {
            string[] directories = CharacterTestCharacterActionConfigSource.CreateDefaultCandidateDirectories();

            CollectionAssert.Contains(
                directories,
                "Assets/MxFrameworkGenerated/CharacterActions/Libraries/Common");
            string[] playerDirectories = CharacterTestCharacterActionConfigSource.CreateCandidateDirectories(
                "ignored-project-root", Application.streamingAssetsPath, false);
            Assert.IsFalse(Array.Exists(playerDirectories, path => path.Contains("ignored-project-root")));
        }

[Test]
        public void FixedTickDriver_AccumulatesSubFrameDelta()
        {
            var driver = new CharacterTestFixedTickDriver();
            int ticks = 0;
            double tickedSeconds = 0d;

            int firstStepCount = driver.Step(1d / 120d, 1d / 60d, deltaTime =>
            {
                ticks++;
                tickedSeconds += deltaTime;
            });
            int secondStepCount = driver.Step(1d / 120d, 1d / 60d, deltaTime =>
            {
                ticks++;
                tickedSeconds += deltaTime;
            });

            Assert.AreEqual(0, firstStepCount);
            Assert.AreEqual(1, secondStepCount);
            Assert.AreEqual(1, ticks);
            Assert.That(tickedSeconds, Is.EqualTo(1d / 60d).Within(0.0000001d));
            Assert.That(driver.AccumulatorSeconds, Is.EqualTo(0d).Within(0.0000001d));
        }

[Test]
        public void FixedTickDriver_CatchesUpMultipleFixedTicks()
        {
            var driver = new CharacterTestFixedTickDriver();
            int ticks = 0;

            int stepCount = driver.Step(3d / 60d, 1d / 60d, _ => ticks++);

            Assert.AreEqual(3, stepCount);
            Assert.AreEqual(3, ticks);
            Assert.That(driver.AccumulatorSeconds, Is.EqualTo(0d).Within(0.0000001d));
        }

[Test]
        public void FixedTickDriver_DropsBacklogAtMaxStepLimit()
        {
            var driver = new CharacterTestFixedTickDriver
            {
                MaxStepsPerUpdate = 2
            };
            int ticks = 0;

            int stepCount = driver.Step(10d / 60d, 1d / 60d, _ => ticks++);

            Assert.AreEqual(2, stepCount);
            Assert.AreEqual(2, ticks);
            Assert.That(driver.AccumulatorSeconds, Is.EqualTo(0d).Within(0.0000001d));
        }

[Test]
        public void FixedTickDriver_ResetClearsAccumulator()
        {
            var driver = new CharacterTestFixedTickDriver();
            driver.Step(1d / 120d, 1d / 60d, _ => { });

            driver.Reset();

            Assert.That(driver.AccumulatorSeconds, Is.EqualTo(0d).Within(0.0000001d));
        }

        [Test]
        public void TraceRecorder_DefaultFilePath_SeparatesMultiplayerPlayModeVirtualPlayers()
        {
            string mainPath = CharacterTestTraceRecorder.CreateDefaultFilePath(
                "C:/tmp/mx",
                new[] { "-name", "Player1" });
            string player2Path = CharacterTestTraceRecorder.CreateDefaultFilePath(
                "C:/tmp/mx",
                new[] { "-name", "Player2" });

            Assert.AreEqual("jump-trace.jsonl", Path.GetFileName(mainPath));
            Assert.AreEqual("jump-trace-Player2.jsonl", Path.GetFileName(player2Path));
        }

        [Test]
        public void TraceRecorder_FilePathFallback_UsesProcessUniqueFileWhenDefaultTraceIsLocked()
        {
            string directory = Path.Combine(
                Path.GetTempPath(),
                "MxFramework.CharacterTestTraceRecorderTests",
                Guid.NewGuid().ToString("N"));
            string lockedPath = Path.Combine(directory, "jump-trace.jsonl");
            Directory.CreateDirectory(directory);

            try
            {
                string recorderPath;
                using (new FileStream(lockedPath, FileMode.Create, FileAccess.ReadWrite, FileShare.None))
                {
                    using (var recorder = new CharacterTestTraceRecorder(
                        capacity: 16,
                        filePath: lockedPath,
                        fallbackToUniqueFilePath: true))
                    {
                        Assert.AreNotEqual(lockedPath, recorder.FilePath);
                        StringAssert.Contains("jump-trace-pid", Path.GetFileName(recorder.FilePath));
                        Assert.IsTrue(recorder.WritesFile);

                        recorder.JumpInput(RuntimeFrame.Zero, 1, "test");
                        recorderPath = recorder.FilePath;
                    }

                    Assert.IsTrue(File.Exists(recorderPath));
                    StringAssert.Contains("test", File.ReadAllText(recorderPath));
                }
            }
            finally
            {
                if (Directory.Exists(directory))
                    Directory.Delete(directory, recursive: true);
            }
        }

        [Test]
        public void TraceRecorder_InMemoryRecorderKeepsRecentEventsWithoutFileWrites()
        {
            CharacterTestTraceRecorder recorder = CharacterTestTraceRecorder.CreateInMemory(capacity: 16);

            recorder.Locomotion(RuntimeFrame.Zero, 1, "blend=1 speed=1");

            Assert.IsFalse(recorder.WritesFile);
            Assert.AreEqual(string.Empty, recorder.FilePath);
            Assert.AreEqual(1, recorder.RecentEvents.Count);
            Assert.AreEqual("Locomotion", recorder.RecentEvents[0].Category);
        }

        [Test]
        public void TraceRecorder_StructuredCombatTraceWritesCompatibleJsonMessage()
        {
            string directory = Path.Combine(
                Path.GetTempPath(),
                "MxFramework.CharacterTestTraceRecorderTests",
                Guid.NewGuid().ToString("N"));
            string tracePath = Path.Combine(directory, "combat-trace.jsonl");
            Directory.CreateDirectory(directory);

            try
            {
                string traceText;
                using (var recorder = new CharacterTestTraceRecorder(capacity: 16, filePath: tracePath))
                {
                    recorder.AttackCombatHitQuery(
                        new RuntimeFrame(7),
                        new CharacterTestCombatHitQueryTraceEvent(
                            activeTraceCount: 2,
                            candidateCount: 3,
                            resolvedCount: 1));
                    recorder.AttackCombatHit(
                        new RuntimeFrame(8),
                        11,
                        new CharacterTestCombatHitTraceEvent(
                            targetEntityId: 22,
                            kind: HitResolveKind.Damage,
                            damageSourceId: global::MxFramework.Tests.Game.CharacterTest.MigrationFixtureIds.Id0019,
                            damage: 12,
                            hasTarget: true,
                            health: 88,
                            maxHealth: 100,
                            scatterPressure: 12,
                            maxScatterPressure: 50,
                            pressureBand: PressureBand.Pressed,
                            staggerFrames: 3,
                            hitPointWorld: FixVector3.Zero,
                            hitNormalWorld: FixVector3.Zero,
                            traceId: 99));

                    Assert.AreEqual(2, recorder.RecentEvents.Count);
                    Assert.AreEqual("Attack", recorder.RecentEvents[0].Category);
                    Assert.AreEqual("combat", recorder.RecentEvents[0].Stage);
                    StringAssert.Contains("hitTrace query active=2", recorder.RecentEvents[0].Message);
                    StringAssert.Contains("hitTrace hit target=22 kind=Damage", recorder.RecentEvents[1].Message);
                }

                traceText = File.ReadAllText(tracePath);
                StringAssert.Contains(
                    "\"message\":\"hitTrace query active=2 candidates=3 resolved=1\"",
                    traceText);
                StringAssert.Contains("damageSource=damage\\\"id", traceText);
                StringAssert.Contains("hp=88/100", traceText);
                StringAssert.Contains("scatterPressure=12/50(Pressed)", traceText);
            }
            finally
            {
                if (Directory.Exists(directory))
                    Directory.Delete(directory, recursive: true);
            }
        }

        [Test]
        public void TraceRecorder_StructuredRuntimeTraceWritesCompatibleJsonMessage()
        {
            string directory = Path.Combine(
                Path.GetTempPath(),
                "MxFramework.CharacterTestTraceRecorderTests",
                Guid.NewGuid().ToString("N"));
            string tracePath = Path.Combine(directory, "runtime-trace.jsonl");
            Directory.CreateDirectory(directory);

            try
            {
                string traceText;
                using (var recorder = new CharacterTestTraceRecorder(capacity: 16, filePath: tracePath))
                {
                    recorder.LocomotionControlScale(
                        new RuntimeFrame(9),
                        3,
                        new CharacterTestMotionControlScaleTraceEvent(
                            500,
                            1000,
                            new CharacterMotionRequestSource(new MxFramework.CharacterAction.CharacterActionId(301), global::MxFramework.Tests.Game.CharacterTest.MigrationFixtureIds.Id0054)));
                    recorder.LocomotionRootMotionDelta(
                        new RuntimeFrame(10),
                        3,
                        new CharacterTestRootMotionDeltaTraceEvent(
                            12,
                            0,
                            -4,
                            new CharacterMotionRequestSource(new MxFramework.CharacterAction.CharacterActionId(301), global::MxFramework.Tests.Game.CharacterTest.MigrationFixtureIds.Id0008)));
                    recorder.LocomotionRootMotionActive(
                        new RuntimeFrame(11),
                        3,
                        new CharacterTestRootMotionActiveTraceEvent(new MxFramework.CharacterAction.CharacterActionId(301), "katana.slash", 2, 8));
                    recorder.LocomotionCollisionProfileBlocked(
                        new RuntimeFrame(12),
                        3,
                        new CharacterTestCollisionProfileBlockedTraceEvent(MxFramework.Game.CharacterTest.CharacterTestAuthoredContentIds.D402_Id2, MxFramework.Game.CharacterTest.CharacterTestAuthoredContentIds.D402_Id3, 2));
                    recorder.AttackInput(
                        new RuntimeFrame(13),
                        3,
                        new CharacterTestActionInputTraceEvent(
                            CharacterTestActionInputTraceKind.SkillSlot,
                            new MxFramework.CharacterAction.CharacterActionId(301),
                            CharacterTestActionStateId.Running,
                            slotIndex: 1));
                    recorder.AttackAction(
                        new RuntimeFrame(14),
                        3,
                        new CharacterTestActionOutcomeTraceEvent(
                            CharacterTestActionOutcomeTraceKind.Accepted,
                            new MxFramework.CharacterAction.CharacterActionId(301),
                            "katana.light"));

                    Assert.AreEqual(6, recorder.RecentEvents.Count);
                    StringAssert.Contains(
                        "controlScale move=0.5 rot=1 source=light_attack:motion.scale",
                        recorder.RecentEvents[0].Message);
                    StringAssert.Contains(
                        "rootMotionDeltaMm=(12,0,-4) source=light_attack:anim.root",
                        recorder.RecentEvents[1].Message);
                    StringAssert.Contains(
                        "rootMotion active action=light_attack clip=katana.slash frames=2-8",
                        recorder.RecentEvents[2].Message);
                    StringAssert.Contains(
                        "collisionProfile blocked profile=standing keep=crouch hits=2",
                        recorder.RecentEvents[3].Message);
                    StringAssert.Contains(
                        "skill_slot 2 pressed active=light_attack state=Running",
                        recorder.RecentEvents[4].Message);
                    StringAssert.Contains(
                        "accepted action=light_attack animKey=katana.light",
                        recorder.RecentEvents[5].Message);
                }

                traceText = File.ReadAllText(tracePath);
                StringAssert.Contains(
                    "\"message\":\"controlScale move=0.5 rot=1 source=light_attack:motion.scale\"",
                    traceText);
                StringAssert.Contains(
                    "\"message\":\"rootMotionDeltaMm=(12,0,-4) source=light_attack:anim.root\"",
                    traceText);
                StringAssert.Contains(
                    "\"message\":\"collisionProfile blocked profile=standing keep=crouch hits=2\"",
                    traceText);
                StringAssert.Contains(
                    "\"message\":\"accepted action=light_attack animKey=katana.light\"",
                    traceText);
            }
            finally
            {
                if (Directory.Exists(directory))
                    Directory.Delete(directory, recursive: true);
            }
        }

        [Test]
        public void HitTraceWindowState_StopFrame_RemainsActiveUntilFrameEnd()
        {
            var world = new CharacterTestCharacterRuntimeWorld();
            var registry = new CharacterTestCharacterDefinitionRegistry();
            Assert.IsTrue(registry.TryGet(
                CharacterTestCharacterDefinitionRegistry.IronVanguardCharacterId,
                out CharacterTestCharacterDefinition definition));

            CharacterTestCharacterEntity entity = world.Spawn(
                new CharacterTestCharacterSpawnRequest(
                    definition.CharacterId,
                    teamId: 1,
                    x: 0f,
                    y: 0f,
                    z: 0f,
                    CharacterTestCharacterControlOwner.None),
                definition);
            object state = CreateHitTraceWindowState();
            var frame = new RuntimeFrame(5);

            entity.EnqueueCombatRequest(CreateHitTraceWindowRequest(
                CharacterActionTrackEventKind.HitTraceWindow,
                frame,
                entity.Id.Value));
            entity.EnqueueCombatRequest(CreateHitTraceWindowRequest(
                CharacterActionTrackEventKind.HitTraceWindow,
                frame,
                entity.Id.Value));

            InvokeHitTraceWindowState(state, "ProcessStartCombatRequests", world, frame);
            Assert.AreEqual(1, GetHitTraceWindowStateActiveTraceCount(state));
            Assert.AreEqual(2, entity.PendingCombatRequestCount);

            InvokeHitTraceWindowState(state, "ProcessStopCombatRequestsAndClear", world, frame);
            Assert.AreEqual(0, GetHitTraceWindowStateActiveTraceCount(state));
            Assert.AreEqual(0, entity.PendingCombatRequestCount);
        }

[Test]
        public void CharacterActionConfigSource_LoadsDefaultLandingProfileAndActions()
        {
            CharacterTestCharacterActionRuntime runtime = CharacterTestCharacterActionConfigSource.Load();

            Assert.IsTrue(runtime.ImportResult.Success, FirstImportDiagnostic(runtime));
            Assert.IsTrue(runtime.ValidationReport.Success, FirstValidationDiagnostic(runtime));
            Assert.AreEqual(
                CharacterTestCharacterActionConfigSource.ActionSetStableId,
                runtime.ActionSet.StableId);
            Assert.AreEqual("landing.codex_flow_test.default", runtime.ActionSet.LandingProfileId);
            Assert.AreEqual(415, runtime.ActionSet.DefaultActionSlotMask);
            Assert.IsTrue(runtime.Registry.TryGetLandingProfile(
                runtime.ActionSet.LandingProfileId,
                out CharacterLandingProfile landingProfile));
            Assert.AreEqual(CharacterTestCharacterActionConfigSource.LandingLightActionId, landingProfile.LightLandingActionId);
            Assert.AreEqual(CharacterTestCharacterActionConfigSource.LandingHeavyActionId, landingProfile.HeavyLandingActionId);
            Assert.AreEqual(1000, landingProfile.LightLandingMinDownwardSpeedMillimetersPerSecond);
            Assert.AreEqual(8000, landingProfile.HeavyLandingMinDownwardSpeedMillimetersPerSecond);
            Assert.IsTrue(runtime.Registry.TryGetAction(
                CharacterTestCharacterActionConfigSource.LandingLightActionId,
                out CharacterActionConfig lightLanding));
            Assert.IsTrue(runtime.Registry.TryGetAction(
                CharacterTestCharacterActionConfigSource.LandingHeavyActionId,
                out CharacterActionConfig heavyLanding));
            Assert.AreEqual(55, lightLanding.DurationFrames.GetValueOrDefault());
            Assert.AreEqual(103, heavyLanding.DurationFrames.GetValueOrDefault());
            Assert.AreEqual(1.5f, lightLanding.AnimationTrack.Events[0].PlaybackSpeed);
            Assert.AreEqual(0.8f, heavyLanding.AnimationTrack.Events[0].PlaybackSpeed);
        }

[Test]
        public void CharacterActionConfigSource_LoadsJsonThroughImporterRegistryAndReport()
        {
            CharacterTestCharacterActionRuntime runtime = CharacterTestCharacterActionConfigSource.Load();

            Assert.IsTrue(runtime.ImportResult.Success, FirstImportDiagnostic(runtime));
            Assert.IsTrue(runtime.ValidationReport.Success, FirstValidationDiagnostic(runtime));
            Assert.AreEqual(CharacterTestCharacterActionConfigSourceKind.ExternalJson, runtime.ConfigSourceKind);
            AssertHasSourceDiagnostic(runtime, CharacterTestCharacterActionConfigSource.ExternalJsonLoadedCode);
            StringAssert.Contains("CharacterActions/ActionSets/codex_flow_test", runtime.ActionSetsSourcePath);
            StringAssert.Contains("CharacterActions/Libraries/Common", runtime.ActionsSourcePath);
            Assert.IsTrue(runtime.Registry.TryGetActionSet(
                CharacterTestCharacterActionConfigSource.ActionSetStableId,
                out CharacterActionSetConfig actionSet));
            Assert.AreSame(actionSet, runtime.ActionSet);
            Assert.AreEqual("landing.codex_flow_test.default", actionSet.LandingProfileId);
            Assert.AreEqual(415, actionSet.DefaultActionSlotMask);
            Assert.IsTrue(runtime.Registry.TryGetLandingProfile(
                actionSet.LandingProfileId,
                out CharacterLandingProfile landingProfile));
            Assert.AreEqual(CharacterTestCharacterActionConfigSource.LandingLightActionId, landingProfile.LightLandingActionId);
            Assert.AreEqual(CharacterTestCharacterActionConfigSource.LandingHeavyActionId, landingProfile.HeavyLandingActionId);
            Assert.AreEqual(1000, landingProfile.LightLandingMinDownwardSpeedMillimetersPerSecond);
            Assert.AreEqual(8000, landingProfile.HeavyLandingMinDownwardSpeedMillimetersPerSecond);
            Assert.IsTrue(runtime.Registry.TryGetAction(new MxFramework.CharacterAction.CharacterActionId(398), out CharacterActionConfig lightLanding));
            Assert.IsTrue(runtime.Registry.TryGetAction(new MxFramework.CharacterAction.CharacterActionId(399), out CharacterActionConfig heavyLanding));
            Assert.AreEqual(55, lightLanding.DurationFrames.GetValueOrDefault());
            Assert.AreEqual(103, heavyLanding.DurationFrames.GetValueOrDefault());
            Assert.AreEqual(1.5f, lightLanding.AnimationTrack.Events[0].PlaybackSpeed);
            Assert.AreEqual(0.8f, heavyLanding.AnimationTrack.Events[0].PlaybackSpeed);
            Assert.AreEqual(
                "reaction.codex_flow_test.sword_empty",
                actionSet.ReactionProfileId);
            Assert.IsTrue(runtime.Registry.TryGetReactionProfile(
                actionSet.ReactionProfileId,
                out CharacterReactionProfile reactionProfile));
            Assert.AreEqual(
                CharacterTestCharacterActionConfigSource.KatanaHitMidReactionActionId,
                reactionProfile.DefaultActionId);
            Assert.IsTrue(reactionProfile.Rules.Any(rule =>
                rule.ActionId == CharacterTestCharacterActionConfigSource.KatanaHitMidReactionActionId
                && rule.Trigger == CharacterReactionRuleTrigger.Hit
                && !rule.CurrentScatterPressureBand.HasValue
                && rule.ReactionGroupId == CharacterTestReactionGroupIds.HeightMid
                && rule.MaxImpactForce == 59));
            Assert.IsTrue(reactionProfile.Rules.Any(rule =>
                rule.ActionId == CharacterTestCharacterActionConfigSource.KatanaHitHighStrongReactionActionId
                && rule.Trigger == CharacterReactionRuleTrigger.Hit
                && !rule.CurrentScatterPressureBand.HasValue
                && rule.ReactionGroupId == CharacterTestReactionGroupIds.HeightHigh
                && rule.MinImpactForce == 60));
            Assert.IsTrue(runtime.Registry.TryGetAction(
                CharacterTestCharacterActionConfigSource.KatanaCohesionBreakReactionActionId,
                out CharacterActionConfig cohesionBreakReaction));
            Assert.AreEqual(CharacterActionCategory.Reaction, cohesionBreakReaction.Category);
            Assert.AreEqual(
                CharacterTestCharacterAttributeIds.DefaultCohesionBreakReactionFrames,
                cohesionBreakReaction.DurationFrames.GetValueOrDefault());
            Assert.AreEqual(1, cohesionBreakReaction.AnimationTrack.Events.Length);
            Assert.AreEqual(
                CharacterTestCharacterActionConfigSource.CohesionBreakReactionAnimationActionKey,
                cohesionBreakReaction.AnimationTrack.Events[0].AnimationActionKey);
            Assert.AreEqual(
                CharacterTestCharacterActionConfigSource.CohesionBreakReactionAnimationClipResourceKey,
                cohesionBreakReaction.AnimationTrack.Events[0].AnimationClipResourceKey);
            Assert.IsTrue(runtime.Registry.TryGetAction(
                CharacterTestCharacterActionConfigSource.KatanaHitMidReactionActionId,
                out CharacterActionConfig midHitReaction));
            Assert.AreEqual(CharacterActionCategory.Reaction, midHitReaction.Category);
            Assert.AreEqual(12, midHitReaction.DurationFrames.GetValueOrDefault());
            Assert.AreEqual(2, midHitReaction.MotionTrack.Events.Length);
            Assert.AreEqual(CharacterActionTrackEventKind.SetControlScale, midHitReaction.MotionTrack.Events[0].Kind);
            Assert.AreEqual(0, midHitReaction.MotionTrack.Events[0].Frame);
            Assert.AreEqual(0f, midHitReaction.MotionTrack.Events[0].MovementControlScale);
            Assert.AreEqual(0f, midHitReaction.MotionTrack.Events[0].RotationControlScale);
            Assert.AreEqual(CharacterActionTrackEventKind.SetControlScale, midHitReaction.MotionTrack.Events[1].Kind);
            Assert.AreEqual(11, midHitReaction.MotionTrack.Events[1].Frame);
            Assert.AreEqual(1f, midHitReaction.MotionTrack.Events[1].MovementControlScale);
            Assert.AreEqual(1f, midHitReaction.MotionTrack.Events[1].RotationControlScale);
            Assert.AreEqual(1, midHitReaction.AnimationTrack.Events.Length);
            Assert.AreEqual(
                CharacterTestCharacterActionConfigSource.HitMidReactionAnimationClipResourceKey,
                midHitReaction.AnimationTrack.Events[0].AnimationClipResourceKey);
            Assert.IsTrue(runtime.Registry.TryGetAction(
                CharacterTestCharacterActionConfigSource.JumpActionId,
                out CharacterActionConfig jump));
            Assert.AreEqual(CharacterTestCharacterActionConfigSource.JumpActionId, jump.ActionId);
            Assert.IsTrue(runtime.Registry.TryGetAction(
                CharacterTestCharacterActionConfigSource.LightAttackActionId,
                out CharacterActionConfig lightAttack));
            Assert.AreEqual(CharacterTestCharacterActionConfigSource.LightAttackActionId, lightAttack.ActionId);
            Assert.IsTrue(lightAttack.MotionTrack.UsesRootMotion);
            Assert.NotNull(lightAttack.MotionTrack.RootMotion);
            Assert.IsTrue(lightAttack.MotionTrack.RootMotion.HasFrames);
            Assert.AreEqual("light_attack.animation", lightAttack.MotionTrack.RootMotion.SourceAnimationEventId);
            Assert.AreEqual(
                CharacterTestCharacterActionConfigSource.LightAttackAnimationClipResourceKey,
                lightAttack.MotionTrack.RootMotion.SourceAnimationClipResourceKey);
            CharacterCancelRule generatedLightAttackCancelWindow = lightAttack.CancelRules.Single();
            Assert.AreEqual(35, generatedLightAttackCancelWindow.StartFrame);
            Assert.AreEqual(75, generatedLightAttackCancelWindow.EndFrame);
            Assert.IsTrue(runtime.Registry.TryGetAction(
                CharacterTestCharacterActionConfigSource.FireballActionId,
                out CharacterActionConfig fireball));
            CharacterActionBinding fireballBinding = actionSet.CommandBindings.FirstOrDefault(binding =>
                (binding.IntentId == CharacterTestCharacterActionConfigSource.FireballIntentId));
            Assert.NotNull(fireballBinding);
            Assert.AreEqual(CharacterTestCharacterActionConfigSource.FireballActionId, fireballBinding.ActionId);
            Assert.AreEqual(15, fireballBinding.Priority);
            Assert.AreEqual(CharacterActionCategory.Skill, fireball.Category);
            Assert.AreEqual(90, fireball.DurationFrames.GetValueOrDefault());
            GameplayTrackEvent fireballEvent = fireball.GameplayTrack.Events.Single(evt =>
                evt.Kind == CharacterActionTrackEventKind.SendGameplayRequest);
            Assert.AreEqual(6, fireballEvent.Frame);
            Assert.AreEqual(CharacterTestCharacterActionConfigSource.FireballProjectileRequestId, fireballEvent.RequestId);
            Assert.AreEqual(CharacterTestCharacterActionConfigSource.FireballProjectileImpactStableEventId, fireballEvent.StableEventId);
            Assert.IsTrue((fireballEvent.TargetPolicyId == 0));
            Assert.IsFalse(fireballEvent.DamagePayload.HasInlineData);
            PresentationTrackEvent fireballWindup = fireball.PresentationTrack.Events.Single(evt =>
                evt.Kind == CharacterActionTrackEventKind.SpawnVisualCue);
            Assert.AreEqual(0, fireballWindup.Frame);
            Assert.AreEqual(5, fireballWindup.EndFrame);
            Assert.AreEqual(CharacterTestVfxIds.FireballChargeResourceKey.ToString(), fireballWindup.ResourceKey);
            Assert.AreEqual(
                CharacterTestCharacterActionConfigSource.FireballWindupVfxStableEventId,
                fireballWindup.StableEventId);

            Assert.IsTrue(runtime.Registry.TryGetAction(
                CharacterTestCharacterActionConfigSource.FireballBurstActionId,
                out CharacterActionConfig fireballBurst));
            CharacterActionBinding fireballBurstBinding = actionSet.CommandBindings.FirstOrDefault(binding =>
                (binding.IntentId == CharacterTestCharacterActionConfigSource.FireballBurstIntentId));
            Assert.NotNull(fireballBurstBinding);
            Assert.AreEqual(CharacterTestCharacterActionConfigSource.FireballBurstActionId, fireballBurstBinding.ActionId);
            Assert.AreEqual(CharacterActionCategory.Skill, fireballBurst.Category);
            GameplayTrackEvent fireballBurstEvent = fireballBurst.GameplayTrack.Events.Single(evt =>
                evt.Kind == CharacterActionTrackEventKind.SendGameplayRequest);
            Assert.AreEqual(6, fireballBurstEvent.Frame);
            Assert.AreEqual(CharacterTestCharacterActionConfigSource.FireballBurstProjectileRequestId, fireballBurstEvent.RequestId);
            Assert.AreEqual(CharacterTestCharacterActionConfigSource.FireballBurstProjectileImpactStableEventId, fireballBurstEvent.StableEventId);
            PresentationTrackEvent fireballBurstWindup = fireballBurst.PresentationTrack.Events.Single(evt =>
                evt.Kind == CharacterActionTrackEventKind.SpawnVisualCue);
            Assert.AreEqual(0, fireballBurstWindup.Frame);
            Assert.AreEqual(5, fireballBurstWindup.EndFrame);
            Assert.AreEqual(
                CharacterTestVfxIds.FireballBurstChargeResourceKey.ToString(),
                fireballBurstWindup.ResourceKey);
            Assert.AreEqual(
                CharacterTestCharacterActionConfigSource.FireballBurstWindupVfxStableEventId,
                fireballBurstWindup.StableEventId);

            Assert.IsTrue(runtime.Registry.TryGetAction(
                CharacterTestCharacterActionConfigSource.FireballLobActionId,
                out CharacterActionConfig fireballLob));
            CharacterActionBinding fireballLobBinding = actionSet.CommandBindings.FirstOrDefault(binding =>
                (binding.IntentId == CharacterTestCharacterActionConfigSource.FireballLobIntentId));
            Assert.NotNull(fireballLobBinding);
            Assert.AreEqual(CharacterTestCharacterActionConfigSource.FireballLobActionId, fireballLobBinding.ActionId);
            Assert.AreEqual(CharacterActionCategory.Skill, fireballLob.Category);
            GameplayTrackEvent fireballLobEvent = fireballLob.GameplayTrack.Events.Single(evt =>
                evt.Kind == CharacterActionTrackEventKind.SendGameplayRequest);
            Assert.AreEqual(6, fireballLobEvent.Frame);
            Assert.AreEqual(CharacterTestCharacterActionConfigSource.FireballLobProjectileRequestId, fireballLobEvent.RequestId);
            Assert.AreEqual(CharacterTestCharacterActionConfigSource.FireballLobProjectileImpactStableEventId, fireballLobEvent.StableEventId);
            PresentationTrackEvent fireballLobWindup = fireballLob.PresentationTrack.Events.Single(evt =>
                evt.Kind == CharacterActionTrackEventKind.SpawnVisualCue);
            Assert.AreEqual(0, fireballLobWindup.Frame);
            Assert.AreEqual(5, fireballLobWindup.EndFrame);
            Assert.AreEqual(
                CharacterTestVfxIds.FireballBurstChargeResourceKey.ToString(),
                fireballLobWindup.ResourceKey);
        }

        [Test]
        public void CharacterTestReactionGroupIds_ResolvesHeightBandsFromHitZoneBodyPartAndSourceGroup()
        {
            Assert.AreEqual(
                CharacterTestReactionGroupIds.HeightHigh,
                CharacterTestReactionGroupIds.ResolveHeightGroup(CharacterTestAuthoredContentIds.D400_Id104, 0, 0));
            Assert.AreEqual(
                CharacterTestReactionGroupIds.HeightMid,
                CharacterTestReactionGroupIds.ResolveHeightGroup(MxFramework.Game.CharacterTest.CharacterTestAuthoredContentIds.D400_Id10, 0, 0));
            Assert.AreEqual(
                CharacterTestReactionGroupIds.HeightLow,
                CharacterTestReactionGroupIds.ResolveHeightGroup(0, global::MxFramework.Tests.Game.CharacterTest.MigrationFixtureIds.Id0032, 0));
            Assert.AreEqual(
                CharacterTestReactionGroupIds.HeightLow,
                CharacterTestReactionGroupIds.ResolveHeightGroup(CharacterTestAuthoredContentIds.D400_Id104, 0, CharacterTestReactionGroupIds.HeightLow));
            Assert.AreEqual(
                CharacterTestReactionGroupIds.HeightMid,
                CharacterTestReactionGroupIds.ResolveHeightGroup(global::MxFramework.Tests.Game.CharacterTest.MigrationFixtureIds.Id0083, 0, 0));
        }

        [Test]
        public void CharacterActionConfigSource_LightAttackUsesConfiguredSequenceAndTransitionWindows()
        {
            CharacterTestCharacterActionRuntime runtime = CharacterTestCharacterActionConfigSource.Load();

            Assert.IsTrue(runtime.ImportResult.Success, FirstImportDiagnostic(runtime));
            Assert.IsTrue(runtime.ValidationReport.Success, FirstValidationDiagnostic(runtime));
            Assert.IsTrue(runtime.Registry.TryGetActionSet(
                CharacterTestCharacterActionConfigSource.ActionSetStableId,
                out CharacterActionSetConfig actionSet));
            Assert.IsTrue(runtime.Registry.TryGetAction(
                CharacterTestCharacterActionConfigSource.LightAttackActionId,
                out CharacterActionConfig lightAttack));
            Assert.IsTrue(runtime.Registry.TryGetAction(
                LightAttackSecondActionId,
                out CharacterActionConfig lightAttackSecond));
            Assert.IsTrue(runtime.Registry.TryGetAction(
                LightAttackThirdActionId,
                out CharacterActionConfig lightAttackThird));

            Assert.IsFalse(actionSet.CommandBindings.Any(binding =>
                (binding.IntentId == CharacterTestCharacterActionConfigSource.LightAttackIntentId)));
            CharacterActionCommandSequence sequence = actionSet.CommandSequences.SingleOrDefault(candidate =>
                (candidate.IntentId == CharacterTestCharacterActionConfigSource.LightAttackIntentId));
            Assert.NotNull(sequence);
            CollectionAssert.AreEqual(
                new[]
                {
                    CharacterTestCharacterActionConfigSource.LightAttackActionId,
                    LightAttackSecondActionId,
                    LightAttackThirdActionId,
                },
                sequence.ActionIds);
            Assert.IsTrue(sequence.WrapAfterLast);
            Assert.AreEqual(10, sequence.Priority);
            Assert.IsFalse(sequence.AllowQueue);
            Assert.AreEqual(0, sequence.QueueWindowFrames);
            Assert.AreEqual(CharacterTestActionSlotIds.DefaultActionSlotMask, actionSet.DefaultActionSlotMask);

            Assert.AreEqual(
                LightAttackSecondAnimationClipResourceKey,
                lightAttackSecond.AnimationTrack.Events[0].AnimationClipResourceKey);
            Assert.AreEqual(
                LightAttackThirdAnimationClipResourceKey,
                lightAttackThird.AnimationTrack.Events[0].AnimationClipResourceKey);

            AssertSlotWindow(
                lightAttack,
                CharacterTestActionSlotIds.AttackPrimary,
                35,
                75);
            AssertSlotWindow(
                lightAttackSecond,
                CharacterTestActionSlotIds.AttackPrimary,
                48,
                75);
            Assert.AreEqual(1, lightAttackThird.CancelRules.Length);
            AssertSlotWindow(
                lightAttackThird,
                CharacterTestActionSlotIds.Dodge,
                67,
                75);
            Assert.IsFalse(lightAttackThird.CancelRules.Any(rule =>
                rule.CancelInputMask == (CharacterCancelInputMask)CharacterTestActionSlotIds.AttackPrimary));
        }

[Test]
        public void CharacterActionRuntime_LightAttackFollowUpClipsResolveBakedWeaponTraceFrames()
        {
            ICharacterTestBakedWeaponTraceSource source = CharacterTestDefaultBakedWeaponTraceSource.Instance;

            AssertLightAttackFollowUpTraceFramesResolve(source, LightAttackSecondAnimationClipResourceKey);
            AssertLightAttackFollowUpTraceFramesResolve(source, LightAttackThirdAnimationClipResourceKey);
        }

private static void AssertLightAttackFollowUpTraceFramesResolve(
            ICharacterTestBakedWeaponTraceSource source,
            string clipKey)
        {
            for (int frame = LightAttackHitTraceStartFrame; frame <= LightAttackHitTraceStopFrame; frame++)
            {
                Assert.IsTrue(
                    source.TryGetFrame(clipKey, IronSwordTraceProfileId, frame, out _),
                    clipKey + " missing baked trace frame " + frame);
            }
        }

[Test]
        public void CharacterActionConfigSource_LoadsEditorExportedJsonFromCandidateDirectory()
        {
            string directory = CreateTempActionConfigDirectory();
            try
            {
                WriteActionJsonFiles(directory, durationFrames: 12);

                CharacterTestCharacterActionRuntime runtime = CharacterTestCharacterActionConfigSource.Load(
                    new CharacterTestCharacterActionConfigSourceOptions(new[] { directory }));

                Assert.AreEqual(CharacterTestCharacterActionConfigSourceKind.ExternalJson, runtime.ConfigSourceKind);
                Assert.IsTrue(runtime.ImportResult.Success, FirstImportDiagnostic(runtime));
                Assert.IsTrue(runtime.ValidationReport.Success, FirstValidationDiagnostic(runtime));
                AssertHasSourceDiagnostic(runtime, CharacterTestCharacterActionConfigSource.ExternalJsonLoadedCode);
                StringAssert.Contains(directory.Replace('\\', '/'), runtime.ActionSetsSourcePath);
                Assert.IsTrue(runtime.Registry.TryGetAction(
                    CharacterTestCharacterActionConfigSource.JumpActionId,
                    out CharacterActionConfig jump));
                Assert.AreEqual(12, jump.DurationFrames);
            }
            finally
            {
                DeleteDirectory(directory);
            }
        }

[Test]
        public void CharacterActionConfigSource_MissingExternalFiles_ReportsMissingWithoutFallback()
        {
            string directory = CreateTempActionConfigDirectory();
            try
            {
                CharacterTestCharacterActionRuntime runtime = CharacterTestCharacterActionConfigSource.Load(
                    new CharacterTestCharacterActionConfigSourceOptions(new[] { directory }));

                Assert.AreEqual(CharacterTestCharacterActionConfigSourceKind.ExternalJson, runtime.ConfigSourceKind);
                Assert.IsTrue(runtime.ImportResult.Success, FirstImportDiagnostic(runtime));
                Assert.IsTrue(runtime.ValidationReport.Success, FirstValidationDiagnostic(runtime));
                Assert.IsFalse(runtime.IsReady);
                Assert.IsNull(runtime.ActionSet);
                AssertHasSourceDiagnostic(runtime, CharacterTestCharacterActionConfigSource.ExternalJsonMissingCode);
                Assert.IsFalse(runtime.Registry.TryGetAction(
                    CharacterTestCharacterActionConfigSource.LightAttackActionId,
                    out _));
            }
            finally
            {
                DeleteDirectory(directory);
            }
        }

[Test]
        public void CharacterActionConfigSource_LegacyFixturePolicy_UsesEmbeddedSample()
        {
            CharacterTestCharacterActionRuntime runtime = CharacterTestCharacterActionConfigSource.Load(
                CharacterTestCharacterActionConfigSourceOptions.CreateLegacyFixtureFallback());

            Assert.AreEqual(CharacterTestCharacterActionConfigSourceKind.EmbeddedFallback, runtime.ConfigSourceKind);
            Assert.IsTrue(runtime.IsReady, FirstImportDiagnostic(runtime));
            AssertHasSourceDiagnostic(runtime, CharacterTestCharacterActionConfigSource.EmbeddedFallbackCode);
            Assert.IsTrue(runtime.Registry.TryGetAction(
                CharacterTestCharacterActionConfigSource.LightAttackActionId,
                out CharacterActionConfig lightAttack));
            Assert.AreEqual(76, lightAttack.DurationFrames);
        }

[Test]
        public void CharacterActionRuntime_EntitiesResolveOnlyTheirExactCharacterAndEquipmentActionSet()
        {
            string directory = CreateTempActionConfigDirectory();
            try
            {
                WriteActionJsonFiles(
                    directory,
                    CreateContextIsolatedActionSetsJson(),
                    CreateActionsJson(12));
                CharacterTestCharacterActionRuntime runtime = CharacterTestCharacterActionConfigSource.Load(
                    new CharacterTestCharacterActionConfigSourceOptions(new[] { directory }));
                Assert.IsTrue(runtime.IsReady, FirstValidationDiagnostic(runtime));

                CharacterTestCharacterRuntimeWorld world =
                    CreateRuntimeWorldWithLocalPlayer(out CharacterTestCharacterEntity alpha);
                CharacterTestCharacterDefinition definition = GetIronVanguardDefinition();
                CharacterTestCharacterEntity beta = world.Spawn(
                    new CharacterTestCharacterSpawnRequest(
                        definition.CharacterId,
                        definition.DefaultTeamId,
                        1000f,
                        0f,
                        0f,
                        CharacterTestCharacterControlOwner.AiControlled),
                    definition);
                CharacterTestCharacterEntity missing = world.Spawn(
                    new CharacterTestCharacterSpawnRequest(
                        definition.CharacterId,
                        definition.DefaultTeamId,
                        2000f,
                        0f,
                        0f,
                        CharacterTestCharacterControlOwner.AiControlled),
                    definition);
                SetActionContext(world, alpha, "char.alpha", "equip.alpha.sword");
                SetActionContext(world, beta, "char.beta", "equip.beta.sword");
                SetActionContext(world, missing, "char.missing", "equip.missing.sword");
                runtime.BindRuntimeWorld(world);

                Assert.IsTrue(runtime.TryGetEntityActionSet(alpha.Id, out CharacterActionSetConfig alphaSet));
                Assert.AreEqual("alpha.sword", alphaSet.StableId);
                Assert.IsTrue(runtime.TryGetEntityActionSet(beta.Id, out CharacterActionSetConfig betaSet));
                Assert.AreEqual("beta.sword", betaSet.StableId);
                Assert.IsFalse(runtime.TryGetEntityActionSet(missing.Id, out _));

                CharacterActionResolveResult alphaResult = runtime.ResolveIntent(
                    alpha.Id,
                    CharacterTestCharacterActionConfigSource.LightAttackIntentId,
                    RuntimeFrame.Zero);
                CharacterActionResolveResult betaResult = runtime.ResolveIntent(
                    beta.Id,
                    CharacterTestCharacterActionConfigSource.LightAttackIntentId,
                    RuntimeFrame.Zero);
                CharacterActionResolveResult missingResult = runtime.ResolveIntent(
                    missing.Id,
                    CharacterTestCharacterActionConfigSource.LightAttackIntentId,
                    RuntimeFrame.Zero);

                Assert.AreEqual(CharacterTestCharacterActionConfigSource.JumpActionId, alphaResult.Plan.ActionId);
                Assert.AreEqual(CharacterTestCharacterActionConfigSource.LightAttackActionId, betaResult.Plan.ActionId);
                Assert.IsTrue(missingResult.IsRejected);
                Assert.AreEqual(CharacterActionRejectReason.MissingActionSet, missingResult.RejectReason);
                Assert.IsFalse(runtime.TryStartActionSlot(
                    missing.Id,
                    1,
                    new MxFramework.CharacterAction.CharacterActionId(301),
                    RuntimeFrame.Zero));
                Assert.AreEqual(
                    CharacterActionDiagnosticCodes.MissingActionSet,
                    missing.Action.LastRejectReason,
                    "A known action id must not bypass the missing exact character/equipment ActionSet.");
                Assert.IsFalse(runtime.TryStartIntent(
                    missing.Id,
                    CharacterTestCharacterActionConfigSource.LightAttackIntentId,
                    RuntimeFrame.Zero));
                Assert.AreEqual(
                    CharacterActionDiagnosticCodes.MissingActionSet,
                    missing.Action.LastRejectReason);
            }
            finally
            {
                DeleteDirectory(directory);
            }
        }

[Test]
        public void CharacterActionSystem_Dragon14SecondaryAndSkillPreferNaturalActionSetBindings()
        {
            CharacterTestCharacterRuntimeWorld world =
                CreateRuntimeWorldWithLocalPlayer(out CharacterTestCharacterEntity secondaryActor);
            CharacterTestCharacterDefinition definition = GetIronVanguardDefinition();
            CharacterTestCharacterEntity skillActor = world.Spawn(
                new CharacterTestCharacterSpawnRequest(
                    definition.CharacterId,
                    definition.DefaultTeamId,
                    1000f,
                    0f,
                    0f,
                    CharacterTestCharacterControlOwner.AiControlled),
                definition);
            SetActionContext(
                world,
                secondaryActor,
                "char.000003",
                "equip_state.000003.unarmed");
            SetActionContext(
                world,
                skillActor,
                "char.000003",
                "equip_state.000003.unarmed");

            AbilityRuntimeModule abilityRuntime = CharacterTestAbilityCatalogSource.CreateRuntimeModule();
            var system = new CharacterTestCharacterActionSystem(
                NullRuntimeLogger.Instance,
                abilityRuntime: abilityRuntime,
                skillSlots: CharacterTestSkillSlotRuntime.CreateDefault());
            Assert.IsTrue(system.Runtime.IsReady, FirstValidationDiagnostic(system.Runtime));
            Assert.IsTrue(abilityRuntime.Definitions.TryGet(
                CharacterTestAbilityIds.FireballAbilityId,
                out AbilityDefinitionConfig fireball));

            abilityRuntime.Tick(new RuntimeTickContext(0, 0d, 0d, RuntimeTickStage.PreSimulation));
            QueueGameplayIntent(
                secondaryActor,
                CharacterTestGameplayIntentKind.AttackSecondary,
                RuntimeFrame.Zero);
            QueueGameplayIntent(
                skillActor,
                CharacterTestGameplayIntentKind.SkillSlot,
                RuntimeFrame.Zero,
                skillSlotPressedMask: CharacterTestSkillSlotRuntime.ToMask(0));

            system.Tick(world, RuntimeFrame.Zero);

            Assert.AreEqual(new CharacterActionId(940003), secondaryActor.Action.ActiveActionId);
            Assert.AreEqual(new CharacterActionId(940004), skillActor.Action.ActiveActionId);
            Assert.AreEqual(
                0,
                abilityRuntime.GetRemainingCooldownFrames(
                    secondaryActor.Id.Value,
                    fireball,
                    RuntimeFrame.Zero));
            Assert.AreEqual(
                0,
                abilityRuntime.GetRemainingCooldownFrames(
                    skillActor.Id.Value,
                    fireball,
                    RuntimeFrame.Zero));
        }

[Test]
        public void CharacterActionSystem_NonDragonSecondaryRetainsFireballFallback()
        {
            CharacterTestCharacterRuntimeWorld world =
                CreateRuntimeWorldWithLocalPlayer(out CharacterTestCharacterEntity actor);
            SetActionContext(
                world,
                actor,
                "char.codex_flow_test",
                "equip_state.codex_flow_test.sword_empty");
            AbilityRuntimeModule abilityRuntime = CharacterTestAbilityCatalogSource.CreateRuntimeModule();
            var system = new CharacterTestCharacterActionSystem(
                NullRuntimeLogger.Instance,
                abilityRuntime: abilityRuntime);
            Assert.IsTrue(system.Runtime.IsReady, FirstValidationDiagnostic(system.Runtime));
            Assert.IsTrue(abilityRuntime.Definitions.TryGet(
                CharacterTestAbilityIds.FireballAbilityId,
                out AbilityDefinitionConfig fireball));

            abilityRuntime.Tick(new RuntimeTickContext(0, 0d, 0d, RuntimeTickStage.PreSimulation));
            QueueGameplayIntent(
                actor,
                CharacterTestGameplayIntentKind.AttackSecondary,
                RuntimeFrame.Zero);

            system.Tick(world, RuntimeFrame.Zero);

            Assert.AreEqual(
                CharacterTestCharacterActionConfigSource.FireballActionId,
                actor.Action.ActiveActionId);
            Assert.AreEqual(
                CharacterTestAbilityIds.FireballCooldownFrames,
                abilityRuntime.GetRemainingCooldownFrames(
                    actor.Id.Value,
                    fireball,
                    RuntimeFrame.Zero));
        }

[Test]
        public void CharacterActionConfigSource_InvalidExternalJson_ReportsStableDiagnosticsWithoutFallback()
        {
            string directory = CreateTempActionConfigDirectory();
            try
            {
                string actionSetsPath = Path.Combine(directory, CharacterTestCharacterActionConfigSource.ActionSetsFileName);
                File.WriteAllText(actionSetsPath, "{");
                File.WriteAllText(
                    Path.Combine(directory, CharacterTestCharacterActionConfigSource.ActionsFileName),
                    CreateActionsJson(8));

                CharacterTestCharacterActionRuntime runtime = CharacterTestCharacterActionConfigSource.Load(
                    new CharacterTestCharacterActionConfigSourceOptions(new[] { directory }));

                Assert.AreEqual(CharacterTestCharacterActionConfigSourceKind.ExternalJson, runtime.ConfigSourceKind);
                Assert.IsFalse(runtime.IsReady);
                Assert.IsFalse(runtime.ImportResult.Success);
                AssertHasSourceDiagnostic(runtime, CharacterTestCharacterActionConfigSource.ExternalJsonLoadedCode);
                AssertHasImportDiagnostic(
                    runtime,
                    CharacterActionDiagnosticCodes.JsonInvalidDocument,
                    actionSetsPath.Replace('\\', '/'));
                Assert.That(runtime.ActionSetsSourcePath, Does.Not.Contain("embedded"));
            }
            finally
            {
                DeleteDirectory(directory);
            }
        }

[Test]
        public void CharacterActionConfigSource_InvalidCancelWindow_ReportsStableValidationDiagnosticWithoutFallback()
        {
            string directory = CreateTempActionConfigDirectory();
            try
            {
                string actionsPath = Path.Combine(directory, CharacterTestCharacterActionConfigSource.ActionsFileName);
                WriteActionJsonFiles(
                    directory,
                    CreateActionSetsJson(),
                    CreateActionsJsonWithLightAttackCancelRule(
                        jumpDurationFrames: 8,
                        startFrame: 1,
                        endFrame: 3));

                CharacterTestCharacterActionRuntime runtime = CharacterTestCharacterActionConfigSource.Load(
                    new CharacterTestCharacterActionConfigSourceOptions(new[] { directory }));

                Assert.AreEqual(CharacterTestCharacterActionConfigSourceKind.ExternalJson, runtime.ConfigSourceKind);
                Assert.IsFalse(runtime.IsReady);
                Assert.IsTrue(runtime.ImportResult.Success, FirstImportDiagnostic(runtime));
                Assert.IsFalse(runtime.ValidationReport.Success);
                AssertHasSourceDiagnostic(runtime, CharacterTestCharacterActionConfigSource.ExternalJsonLoadedCode);
                AssertHasValidationDiagnostic(
                    runtime,
                    CharacterActionDiagnosticCodes.InvalidCancelWindow,
                    actionsPath.Replace('\\', '/'),
                    "actions[light_attack].cancelRules[0]");
                StringAssert.Contains("1-3", FirstValidationDiagnostic(runtime));

                CharacterTestCharacterEntity entity = CreateRuntimeCharacter();
                Assert.IsFalse(runtime.TryStartIntent(
                    entity,
                    CharacterTestCharacterActionConfigSource.LightAttackIntentId,
                    RuntimeFrame.Zero));
                Assert.AreEqual(CharacterActionDiagnosticCodes.ActionConfigNotReady, entity.Action.LastRejectReason);
                Assert.That(runtime.ActionSetsSourcePath, Does.Not.Contain("embedded"));
            }
            finally
            {
                DeleteDirectory(directory);
            }
        }

[Test]
        public void CharacterActionRuntime_JumpAndLightAttack_RouteThroughResolverRunner()
        {
            CharacterTestCharacterEntity entity = CreateRuntimeCharacter();
            CharacterTestCharacterActionRuntime runtime = CharacterTestCharacterActionConfigSource.Load();

            Assert.IsTrue(runtime.TryStartIntent(
                entity,
                CharacterTestCharacterActionConfigSource.JumpIntentId,
                new RuntimeFrame(10)));
            Assert.AreEqual(CharacterTestCharacterActionConfigSource.JumpActionId, entity.Action.ActiveActionId);
            Assert.AreEqual(CharacterTestCharacterActionConfigSource.JumpAnimationClipResourceKey, entity.Action.AnimationActionKeyThisFrame);
            Assert.AreEqual(CharacterTestCharacterActionConfigSource.JumpAnimationClipResourceKey, entity.Action.LastAnimationActionKey);
            Assert.Greater(entity.Action.JumpImpulseY, 0f);
            Assert.AreEqual(MxFramework.Game.CharacterTest.CharacterTestActionStateId.Running, entity.Action.State);
            Assert.That(entity.Action.Phase, Is.Not.Empty);

            TickUntilInactive(runtime, entity, maxTicks: 200);

            Assert.IsTrue(runtime.TryStartIntent(
                entity,
                CharacterTestCharacterActionConfigSource.LightAttackIntentId,
                new RuntimeFrame(30)),
                "source=" + runtime.ConfigSourceKind + ", reject=" + entity.Action.LastRejectReason);

            Assert.AreEqual(
                CharacterTestCharacterActionConfigSource.LightAttackActionId,
                entity.Action.ActiveActionId,
                "reject=" + entity.Action.LastRejectReason);
            Assert.AreEqual(CharacterTestCharacterActionConfigSource.LightAttackAnimationClipResourceKey, entity.Action.AnimationActionKeyThisFrame);
            Assert.AreEqual(CharacterTestCharacterActionConfigSource.LightAttackAnimationClipResourceKey, entity.Action.LastAnimationActionKey);
            Assert.AreEqual(0f, entity.Action.JumpImpulseY);
        }

[Test]
        public void CharacterActionRuntime_JumpMotionTrack_EnqueuesMotionRequest()
        {
            CharacterTestCharacterEntity entity = CreateRuntimeCharacter();
            CharacterTestCharacterActionRuntime runtime = CharacterTestCharacterActionConfigSource.Load();

            Assert.IsTrue(runtime.TryStartIntent(
                entity,
                CharacterTestCharacterActionConfigSource.JumpIntentId,
                new RuntimeFrame(10)));

            Assert.Greater(entity.PendingMotionRequestCount, 0);
            CharacterTestCharacterMotionRequest request = entity.MotionRequests[0];
            Assert.AreEqual(CharacterTestCharacterMotionRequestKind.Impulse, request.Kind);
            Assert.AreEqual(CharacterMovementMode.Airborne, request.MovementMode);
            Assert.AreEqual(CharacterTestCharacterActionConfigSource.JumpActionId, request.SourceActionId);
            Assert.AreEqual(CharacterTestAuthoredContentIds.D401_Id152, request.StableEventId);
            Assert.Greater(request.Value.y, 0f);
        }

[Test]
        public void CharacterActionRuntime_LightAttackPresentationTrack_EnqueuesAudioCueRequest()
        {
            CharacterTestCharacterEntity entity = CreateRuntimeCharacter();
            CharacterTestCharacterActionRuntime runtime = CharacterTestCharacterActionConfigSource.Load();

            Assert.IsTrue(runtime.TryStartIntent(
                entity,
                CharacterTestCharacterActionConfigSource.LightAttackIntentId,
                new RuntimeFrame(30)));

            int expectedPeakFrame = runtime.ConfigSourceKind == CharacterTestCharacterActionConfigSourceKind.ExternalJson
                ? 26
                : 25;
            for (int i = 0; i < expectedPeakFrame; i++)
                runtime.TickEntity(entity);

            Assert.AreEqual(1, entity.PendingAudioCueRequestCount);
            CharacterActionAudioCueRequest request = entity.AudioCueRequests[0];
            Assert.AreEqual(CharacterTestAudioCueCatalog.LightAttackCueId, request.CueId);
            Assert.AreEqual(CharacterTestAudioCueCatalog.LightAttackCueId, request.Metadata.StableEventId);
            Assert.AreEqual(CharacterTestCharacterActionConfigSource.LightAttackActionId, request.Metadata.ActionId);
            Assert.AreEqual(expectedPeakFrame, request.Metadata.LocalFrame);
            Assert.AreEqual(entity.Id.Value.ToString(), request.TargetActorId);
        }

[Test]
        public void CharacterTestAudioCueCatalog_Default_ResolvesCharacterTestCueBusAndEvents()
        {
            CharacterTestAudioCueCatalog catalog = CharacterTestAudioCueCatalog.CreateDefault();

            Assert.IsTrue(catalog.TryGetCue(
                CharacterTestAudioCueCatalog.LightAttackCueKey,
                out AudioCueDefinition cue));
            Assert.AreEqual(CharacterTestAudioCueCatalog.LightAttackCueId, cue.Id);
            Assert.AreEqual(CharacterTestAudioCueCatalog.LightAttackEventId, cue.EventId);
            Assert.AreEqual(AudioCuePlayMode.OneShot, cue.PlayMode);

            Assert.IsTrue(catalog.TryGetEvent(cue.EventId, out AudioEventDefinition audioEvent));
            Assert.AreEqual(CharacterTestAudioCueCatalog.LightAttackEventPath, audioEvent.FmodEventPath);
            Assert.AreEqual(CharacterTestAudioCueCatalog.LightAttackEventGuid, audioEvent.FmodEventGuid);
            Assert.IsTrue(audioEvent.Is3D);
            Assert.IsFalse(audioEvent.IsLoop);

            Assert.IsTrue(catalog.TryGetBus(audioEvent.BusId, out AudioBusDefinition bus));
            Assert.AreEqual(CharacterTestAudioCueCatalog.DefaultBusId, bus.Id);
            Assert.AreEqual("bus:/", bus.FmodBusPath);

            Assert.IsTrue(catalog.TryGetCue(
                CharacterTestAudioCueCatalog.FireballExplosionCueKey,
                out AudioCueDefinition explosionCue));
            Assert.AreEqual(CharacterTestAudioCueCatalog.FireballExplosionCueId, explosionCue.Id);
            Assert.AreEqual(CharacterTestAudioCueCatalog.FireballExplosionEventId, explosionCue.EventId);
            Assert.AreEqual(AudioCuePlayMode.OneShot, explosionCue.PlayMode);

            Assert.IsTrue(catalog.TryGetEvent(explosionCue.EventId, out AudioEventDefinition explosionEvent));
            Assert.AreEqual(CharacterTestAudioCueCatalog.FireballExplosionEventPath, explosionEvent.FmodEventPath);
            Assert.AreEqual(CharacterTestAudioCueCatalog.FireballExplosionEventGuid, explosionEvent.FmodEventGuid);
            Assert.IsTrue(explosionEvent.Is3D);
            Assert.IsFalse(explosionEvent.IsLoop);
        }

[Test]
        public void CharacterTestAudioCuePlaybackSystem_PlayOneShotAndClearsRequests()
        {
            CharacterTestCharacterRuntimeWorld world = CreateRuntimeWorldWithLocalPlayer(
                out CharacterTestCharacterEntity entity);
            entity.SetTransform(new CharacterTestCharacterTransformComponent(2.5f, 1.25f, -4f, 90f));
            entity.EnqueueAudioCueRequest(CreateAudioCueRequest(CharacterTestAudioCueCatalog.LightAttackCueId));
            var audioService = new RecordingAudioService();
            var system = new CharacterTestAudioCuePlaybackSystem(
                audioService,
                CharacterTestAudioCueCatalog.CreateDefault(),
                NullRuntimeLogger.Instance);

            system.Tick(world, new RuntimeFrame(42));

            Assert.AreEqual(1, system.LastFrameRequestCount);
            Assert.AreEqual(1, system.LastFramePlayedCount);
            Assert.AreEqual(0, system.LastFrameFailureCount);
            Assert.AreEqual(0, entity.PendingAudioCueRequestCount);
            Assert.AreEqual(1, audioService.PlayOneShotCount);
            Assert.AreEqual(CharacterTestAudioCueCatalog.LightAttackEventId, audioService.LastPlayRequest.EventId);
            Assert.AreEqual(42L, audioService.LastPlayRequest.Frame);
            Assert.AreEqual(new AudioTraceIdentity(927253), audioService.LastPlayRequest.TraceId);
            Assert.AreEqual(entity.Id.Value, audioService.LastPlayRequest.EmitterId);
            Assert.AreEqual(2.5f, audioService.LastPlayRequest.Transform.X, 0.0001f);
            Assert.AreEqual(1.25f, audioService.LastPlayRequest.Transform.Y, 0.0001f);
            Assert.AreEqual(-4f, audioService.LastPlayRequest.Transform.Z, 0.0001f);
            Assert.AreEqual(1f, audioService.LastPlayRequest.Transform.ForwardX, 0.0001f);
            Assert.AreEqual(0f, audioService.LastPlayRequest.Transform.ForwardY, 0.0001f);
            Assert.AreEqual(0f, audioService.LastPlayRequest.Transform.ForwardZ, 0.0001f);
        }

[Test]
        public void CharacterTestAudioCuePlaybackSystem_2DEvent_Preserves2DRequestPath()
        {
            const string cueKey = "sfx.two_d";
            const int eventId = 509902;
            CharacterTestCharacterRuntimeWorld world = CreateRuntimeWorldWithLocalPlayer(
                out CharacterTestCharacterEntity entity);
            entity.SetTransform(new CharacterTestCharacterTransformComponent(7f, 8f, 9f, 45f));
            entity.EnqueueAudioCueRequest(CreateAudioCueRequest(eventId));
            var catalog = new CharacterTestAudioCueCatalog();
            catalog.AddBus(new AudioBusDefinition(CharacterTestAudioCueCatalog.DefaultBusId, "test", "bus:/"));
            catalog.AddEvent(new AudioEventDefinition(
                eventId,
                "test.two_d",
                "event:/MxFramework/Demo/OneShot",
                "{532d60e2-a86b-468f-a2f8-5d69efcf7207}",
                AudioEventKind.Event,
                CharacterTestAudioCueCatalog.DefaultBusId,
                is3D: false,
                isLoop: false,
                maxDistance: 0f));
            catalog.AddCue(cueKey, new AudioCueDefinition(509902, cueKey, eventId, AudioCuePlayMode.OneShot));
            var audioService = new RecordingAudioService();
            var system = new CharacterTestAudioCuePlaybackSystem(
                audioService,
                catalog,
                NullRuntimeLogger.Instance);

            system.Tick(world, new RuntimeFrame(43));

            Assert.AreEqual(1, system.LastFrameRequestCount);
            Assert.AreEqual(1, system.LastFramePlayedCount);
            Assert.AreEqual(0, system.LastFrameFailureCount);
            Assert.AreEqual(1, audioService.PlayOneShotCount);
            Assert.AreEqual(eventId, audioService.LastPlayRequest.EventId);
            Assert.AreEqual(0, audioService.LastPlayRequest.EmitterId);
            Assert.AreEqual(0f, audioService.LastPlayRequest.Transform.X, 0.0001f);
            Assert.AreEqual(0f, audioService.LastPlayRequest.Transform.Y, 0.0001f);
            Assert.AreEqual(0f, audioService.LastPlayRequest.Transform.Z, 0.0001f);
        }

[Test]
        public void CharacterTestAudioCuePlaybackSystem_MissingEvent_ClearsRequestsWithoutPlayback()
        {
            const string cueKey = "sfx.missing_event";
            CharacterTestCharacterRuntimeWorld world = CreateRuntimeWorldWithLocalPlayer(
                out CharacterTestCharacterEntity entity);
            entity.EnqueueAudioCueRequest(CreateAudioCueRequest(509903));
            var catalog = new CharacterTestAudioCueCatalog();
            catalog.AddCue(cueKey, new AudioCueDefinition(509903, cueKey, 509903, AudioCuePlayMode.OneShot));
            var audioService = new RecordingAudioService();
            var system = new CharacterTestAudioCuePlaybackSystem(
                audioService,
                catalog,
                NullRuntimeLogger.Instance);

            system.Tick(world, new RuntimeFrame(44));

            Assert.AreEqual(1, system.LastFrameRequestCount);
            Assert.AreEqual(0, system.LastFramePlayedCount);
            Assert.AreEqual(1, system.LastFrameFailureCount);
            Assert.AreEqual(0, entity.PendingAudioCueRequestCount);
            Assert.AreEqual(0, audioService.PlayOneShotCount);
        }

[Test]
        public void CharacterTestAudioCuePlaybackSystem_MissingCue_ClearsRequestsWithoutPlayback()
        {
            CharacterTestCharacterRuntimeWorld world = CreateRuntimeWorldWithLocalPlayer(
                out CharacterTestCharacterEntity entity);
            entity.EnqueueAudioCueRequest(CreateAudioCueRequest(int.MaxValue));
            var audioService = new RecordingAudioService();
            var system = new CharacterTestAudioCuePlaybackSystem(
                audioService,
                CharacterTestAudioCueCatalog.CreateDefault(),
                NullRuntimeLogger.Instance);

            system.Tick(world, new RuntimeFrame(42));

            Assert.AreEqual(1, system.LastFrameRequestCount);
            Assert.AreEqual(0, system.LastFramePlayedCount);
            Assert.AreEqual(1, system.LastFrameFailureCount);
            Assert.AreEqual(0, entity.PendingAudioCueRequestCount);
            Assert.AreEqual(0, audioService.PlayOneShotCount);
        }

[Test]
        public void CharacterTestAudioCuePlaybackSystem_MissingAudioService_ClearsRequestsWithoutGameplayBlock()
        {
            CharacterTestCharacterRuntimeWorld world = CreateRuntimeWorldWithLocalPlayer(
                out CharacterTestCharacterEntity entity);
            entity.EnqueueAudioCueRequest(CreateAudioCueRequest(CharacterTestAudioCueCatalog.LightAttackCueId));
            var system = new CharacterTestAudioCuePlaybackSystem(
                null,
                CharacterTestAudioCueCatalog.CreateDefault(),
                NullRuntimeLogger.Instance);

            Assert.DoesNotThrow(() => system.Tick(world, new RuntimeFrame(42)));

            Assert.AreEqual(1, system.LastFrameRequestCount);
            Assert.AreEqual(0, system.LastFramePlayedCount);
            Assert.AreEqual(1, system.LastFrameFailureCount);
            Assert.AreEqual(0, entity.PendingAudioCueRequestCount);
        }

[Test]
        public void CharacterActionRuntime_ApplyImpulseSpace_EnqueuesLocalMotionRequest()
        {
            string directory = CreateTempActionConfigDirectory();
            try
            {
                string actionsJson = CreateActionsJson(8).Replace(
                    @"""movementMode"": ""Airborne"", ""x"": 0.0, ""y"": 8.0, ""z"": 0.0",
                    @"""movementMode"": ""Airborne"", ""space"": ""Local"", ""x"": 0.0, ""y"": 8.0, ""z"": 1.5");
                WriteActionJsonFiles(directory, CreateActionSetsJson(), actionsJson);
                CharacterTestCharacterEntity entity = CreateRuntimeCharacter();
                CharacterTestCharacterActionRuntime runtime = CharacterTestCharacterActionConfigSource.Load(
                    new CharacterTestCharacterActionConfigSourceOptions(new[] { directory }));

                Assert.IsTrue(runtime.TryStartIntent(
                    entity,
                    CharacterTestCharacterActionConfigSource.JumpIntentId,
                    new RuntimeFrame(10)));

                CharacterTestCharacterMotionRequest request = entity.MotionRequests[0];
                Assert.AreEqual(CharacterTestCharacterMotionRequestKind.Impulse, request.Kind);
                Assert.AreEqual(CharacterTestCharacterMotionRequestSpace.Local, request.Space);
                Assert.AreEqual(1.5f, request.Value.z, 0.0001f);
            }
            finally
            {
                DeleteDirectory(directory);
            }
        }

[Test]
        public void CharacterActionRuntime_LightAttackRootMotion_EnqueuesAndConsumesRootMotionDelta()
        {
            CharacterTestCharacterRuntimeWorld world = CreateRuntimeWorldWithLocalPlayer(
                out CharacterTestCharacterEntity entity);
            CharacterTestCharacterActionRuntime runtime = CharacterTestCharacterActionConfigSource.Load();
            var rootMotion = new CharacterTestCharacterRootMotionSystem(NullRuntimeLogger.Instance);
            var motion = new CharacterTestCharacterMotionSystem();
            motion.SetMovementProfile(CreateMovementRuntime());

            Assert.IsTrue(runtime.TryStartIntent(
                entity,
                CharacterTestCharacterActionConfigSource.LightAttackIntentId,
                new RuntimeFrame(30)));
            runtime.TickEntity(entity);

            rootMotion.Tick(world, runtime, new RuntimeFrame(31));

            Assert.Greater(entity.PendingMotionRequestCount, 0);
            CharacterTestCharacterMotionRequest request = default;
            for (int i = 0; i < entity.MotionRequests.Count; i++)
            {
                if (entity.MotionRequests[i].Kind == CharacterTestCharacterMotionRequestKind.RootMotionDelta)
                {
                    request = entity.MotionRequests[i];
                    break;
                }
            }

            Assert.AreEqual(CharacterTestCharacterMotionRequestKind.RootMotionDelta, request.Kind);
            Assert.AreEqual(CharacterTestCharacterMotionRequestSpace.Local, request.Space);
            Assert.AreEqual(CharacterMovementMode.RootMotionDriven, request.MovementMode);
            Assert.AreEqual(CharacterTestCharacterActionConfigSource.LightAttackActionId, request.SourceActionId);
            Assert.AreEqual(CharacterTestAuthoredContentIds.D401_Id158, request.StableEventId);
            Assert.AreNotEqual(Vector3.zero, request.Value);

            motion.Tick(world, 1f / 60f);

            Assert.AreEqual(0, entity.PendingMotionRequestCount);
            Assert.Greater(Mathf.Abs(entity.Transform.X), 0.0001f);
            Assert.Greater(Mathf.Abs(entity.Transform.Z), 0.0001f);
            Assert.AreEqual(0f, entity.HorizontalVelocity.magnitude, 0.0001f);
            Assert.AreEqual(CharacterMovementMode.RootMotionDriven, entity.MovementMode);
        }

[Test]
        public void CharacterActionRuntime_LightAttackHitTrace_DamagesEnemyDuringWindow()
        {
            CharacterTestCharacterEntity attacker;
            CharacterTestCharacterEntity target;
            CharacterTestCharacterRuntimeWorld world = CreateRuntimeWorldWithAttackerAndTarget(
                targetTeamId: 2,
                out attacker,
                out target);
            AlignTargetWithDefaultProjectileSocketLine(target);
            CharacterTestCharacterActionRuntime runtime = CharacterTestCharacterActionConfigSource.Load();
            CharacterTestCharacterPhysicsRuntime physicsRuntime = CreateFrameworkPhysicsRuntime(includeWall: false);
            var motion = new CharacterTestCharacterMotionSystem();
            motion.SetMovementProfile(CreateMovementRuntime());
            motion.SetPhysicsRuntime(physicsRuntime);
            var combat = new CharacterTestCharacterCombatTraceSystem();
            combat.SetPhysicsRuntime(physicsRuntime);

            HitResolveResult firstResult = RunLightAttackUntilCombatResult(
                world,
                runtime,
                motion,
                combat,
                attacker,
                maxFrames: 40);

            Assert.AreEqual(HitResolveKind.Damage, firstResult.Kind);
            Assert.Greater(firstResult.Damage, 1);
            Assert.AreEqual(physicsRuntime.GetEntityId(attacker), firstResult.AttackerId);
            Assert.AreEqual(physicsRuntime.GetEntityId(target), firstResult.TargetId);
            Assert.AreEqual(
                CharacterTestCharacterCombatStateComponent.DefaultMaxHealth - firstResult.Damage,
                target.CombatState.Health);
            Assert.AreEqual(6, target.CombatState.HitStunFramesRemaining);
            Assert.AreEqual(CharacterActionId.None, target.Action.ActiveActionId);
            Assert.AreEqual(firstResult.Damage, target.CombatState.LastDamage);
            Assert.AreEqual(attacker.Id.Value, target.CombatState.LastHitSourceEntityId);
            Assert.AreEqual(firstResult.TraceId, target.CombatState.LastHitTraceId);
            Assert.AreEqual(34, target.CombatState.ScatterPressure);
            Assert.AreEqual(
                CharacterTestCharacterAttributeIds.DefaultScatterPressureMax,
                target.CombatState.MaxScatterPressure);
            Assert.AreEqual(PressureBand.Stable, target.CombatState.ScatterPressureBand);
            Assert.IsTrue(world.TryGetScatterPressure(target, out GameplayScatterPressureComponent scatterPressure));
            Assert.AreEqual(target.CombatState.ScatterPressure, scatterPressure.CurrentScatterPressure);
            DamageTelemetryRow telemetry = combat.DamageTelemetryRows
                .First(row => row.ResultKind == HitResolveKind.Damage);
            Assert.AreEqual(DamageProfileSourceKind.WeaponTrace, telemetry.SourceKind);
            Assert.AreEqual(CharacterTestCharacterActionConfigSource.LightAttackActionId, telemetry.ActionId);
            Assert.AreEqual(CharacterTestAuthoredContentIds.D401_Id161, telemetry.StableEventId);
            Assert.AreEqual(CharacterTestAuthoredContentIds.D405_Id1, telemetry.EquipmentSlotId);
            Assert.AreEqual("weapon.main_hand_katana", telemetry.WeaponId);
            Assert.AreEqual(0, telemetry.DamageProfileId);
            Assert.AreEqual(CharacterTestAuthoredContentIds.D409_Id1, telemetry.DamageTypeId);
            Assert.AreEqual("hit.torso", telemetry.HitZoneId);
            Assert.AreEqual("torso", telemetry.BodyPartId);
            Assert.AreEqual(HitResolveKind.Damage, telemetry.ResultKind);
            Assert.AreEqual(firstResult.Damage, telemetry.IncomingDamage);
            Assert.AreEqual(firstResult.Damage, telemetry.FinalDamage);
            Assert.AreEqual(CharacterTestCharacterCombatStateComponent.DefaultMaxHealth, telemetry.HpBefore);
            Assert.AreEqual(target.CombatState.Health, telemetry.HpAfter);
            Assert.AreEqual(
                target.CombatState.Health,
                world.GetCurrentAttribute(target, CharacterTestCharacterAttributeIds.Hp));
            Assert.AreEqual(34, telemetry.ImpactPower);
            Assert.AreEqual(34, telemetry.ScatterImpact);
            Assert.AreEqual(76f, telemetry.SourceMassKg, 0.0001f);
            Assert.AreEqual(76f, telemetry.TargetMassKg, 0.0001f);
            AssertOnHitDefenseBreakBuff(world, target, firstResult.TraceId);
            Assert.AreEqual(1, combat.OnHitBuffLastSummary.RequestedBuffCount);
            Assert.Greater(telemetry.EffectiveImpact, 33.9f);
            Assert.Greater(telemetry.KnockbackSpeed, 0f);
            Assert.Greater(telemetry.PreviewKnockbackDistanceMeters, 0f);
            Assert.Less(telemetry.PreviewKnockbackDistanceMeters, 1.2f);
            Assert.Greater(telemetry.PreviewKnockbackDistanceMeters, 0.8f);
            Assert.AreEqual(firstResult.HitPointWorld, telemetry.HitPointWorld);
            Assert.AreEqual(firstResult.HitNormalWorld, telemetry.HitNormalWorld);
            CharacterTestHitPointDebugFrame hitPointFrame = combat.HitPointDebugFrames
                .First(frame => frame.Kind == HitResolveKind.Damage);
            Assert.AreEqual(attacker.Id.Value, hitPointFrame.SourceEntityId);
            Assert.AreEqual(target.Id.Value, hitPointFrame.TargetEntityId);
            Assert.AreEqual(firstResult.TraceId, hitPointFrame.TraceId);
            Assert.AreEqual(firstResult.HitPointWorld, hitPointFrame.HitPointWorld);
            Assert.AreEqual(firstResult.HitNormalWorld, hitPointFrame.HitNormalWorld);
            Assert.AreEqual("hit.torso", hitPointFrame.HitZoneId);
            Assert.AreEqual("torso", hitPointFrame.BodyPartId);
            CharacterTestCharacterMotionRequest knockback = target.MotionRequests
                .FirstOrDefault(request => request.Kind == CharacterTestCharacterMotionRequestKind.HorizontalVelocityOverride);
            Assert.AreEqual(CharacterTestCharacterMotionRequestKind.HorizontalVelocityOverride, knockback.Kind);
            Assert.Greater(new Vector2(knockback.Value.x, knockback.Value.z).magnitude, 0f);
        }

[Test]
        public void CharacterActionRuntime_LightAttackHitTrace_BreaksOnThirdConsecutiveLightHit()
        {
            CharacterTestCharacterEntity attacker;
            CharacterTestCharacterEntity target;
            CharacterTestCharacterRuntimeWorld world = CreateRuntimeWorldWithAttackerAndTarget(
                targetTeamId: 2,
                out attacker,
                out target);
            SetCurrentAttribute(world, target, CharacterTestCharacterAttributeIds.Hp, 1000);
            CharacterTestCharacterActionRuntime runtime = CharacterTestCharacterActionConfigSource.Load();
            CharacterTestCharacterPhysicsRuntime physicsRuntime = CreateFrameworkPhysicsRuntime(includeWall: false);
            var motion = new CharacterTestCharacterMotionSystem();
            motion.SetMovementProfile(CreateMovementRuntime());
            motion.SetPhysicsRuntime(physicsRuntime);
            var combat = new CharacterTestCharacterCombatTraceSystem();
            combat.SetPhysicsRuntime(physicsRuntime);

            long startFrame = 0L;
            HitResolveResult lastResult = default;
            for (int hitIndex = 1; hitIndex <= 3; hitIndex++)
            {
                attacker.SetTransform(new CharacterTestCharacterTransformComponent(0f, 0f, 0f, 0f));
                attacker.SetMotionState(0f, Vector2.zero, isGrounded: true);
                AlignTargetWithDefaultProjectileSocketLine(target);
                target.SetMotionState(0f, Vector2.zero, isGrounded: true);
                SyncCharacterCombatCollider(physicsRuntime, attacker);
                SyncCharacterCombatCollider(physicsRuntime, target);

                if (hitIndex == 3)
                {
                    Assert.IsTrue(runtime.TryStartIntent(
                        target,
                        CharacterTestCharacterActionConfigSource.LightAttackIntentId,
                        new RuntimeFrame(startFrame)));
                    Assert.AreEqual(
                        CharacterTestCharacterActionConfigSource.LightAttackActionId,
                        target.Action.ActiveActionId);
                }

                lastResult = RunLightAttackUntilCombatResult(
                    world,
                    runtime,
                    motion,
                    combat,
                    attacker,
                    maxFrames: 50,
                    startFrame: (int)startFrame,
                    failureDetails: () => "hitIndex=" + hitIndex);

                Assert.AreEqual(HitResolveKind.Damage, lastResult.Kind);
                Assert.IsTrue(world.TryGetScatterPressure(target, out GameplayScatterPressureComponent scatterPressure));
                Assert.AreEqual(Math.Min(100, hitIndex * 34), scatterPressure.ScatterDebt);

                if (hitIndex == 2)
                {
                    Assert.AreEqual(68, scatterPressure.ScatterDebt);
                    Assert.AreEqual(PressureBand.Cracked, scatterPressure.CurrentBand);
                    Assert.IsFalse(scatterPressure.IsBroken);
                    Assert.AreEqual(0, combat.CohesionBreakEvents.Count);
                    Assert.AreEqual(-1, target.CombatState.LastCohesionBreakFrame);
                }

                if (hitIndex < 3)
                {
                    Assert.AreEqual(0, combat.CohesionBreakEvents.Count);
                    TickCombatUntilActionInactive(
                        world,
                        runtime,
                        motion,
                        combat,
                        attacker,
                        startFrame: lastResult.Frame.Value + 1,
                        maxFrames: 120);
                    startFrame = lastResult.Frame.Value + 80L;
                }
            }

            Assert.AreEqual(1, combat.CohesionBreakEvents.Count);
            Assert.IsTrue(world.TryGetScatterPressure(target, out GameplayScatterPressureComponent brokenPressure));
            Assert.AreEqual(100, brokenPressure.ScatterDebt);
            Assert.AreEqual(PressureBand.Broken, brokenPressure.CurrentBand);
            Assert.IsTrue(brokenPressure.IsBroken);
            Assert.AreEqual(
                CharacterTestCharacterActionConfigSource.KatanaCohesionBreakReactionActionId,
                target.Action.ActiveActionId);
            Assert.AreEqual(lastResult.Frame.Value, target.CombatState.LastCohesionBreakFrame);
            Assert.AreEqual(
                CharacterTestCharacterAttributeIds.DefaultCohesionBreakReactionFrames,
                target.CombatState.HitStunFramesRemaining);
        }

        [Test]
        public void CharacterActionRuntime_BrokenWindowIgnoresScatterAndFullyRecoversOnTimer()
        {
            CharacterTestCharacterEntity attacker;
            CharacterTestCharacterEntity target;
            CharacterTestCharacterRuntimeWorld world = CreateRuntimeWorldWithAttackerAndTarget(
                targetTeamId: 2,
                out attacker,
                out target);
            SetCurrentAttribute(world, target, CharacterTestCharacterAttributeIds.Hp, 1000);
            CharacterTestCharacterActionRuntime runtime = CharacterTestCharacterActionConfigSource.Load();
            CharacterTestCharacterPhysicsRuntime physicsRuntime = CreateFrameworkPhysicsRuntime(includeWall: false);
            var motion = new CharacterTestCharacterMotionSystem();
            motion.SetMovementProfile(CreateMovementRuntime());
            motion.SetPhysicsRuntime(physicsRuntime);
            var combat = new CharacterTestCharacterCombatTraceSystem();
            combat.SetPhysicsRuntime(physicsRuntime);

            void ResetActorsForHit()
            {
                attacker.SetTransform(new CharacterTestCharacterTransformComponent(0f, 0f, 0f, 0f));
                attacker.SetMotionState(0f, Vector2.zero, isGrounded: true);
                AlignTargetWithDefaultProjectileSocketLine(target);
                target.SetMotionState(0f, Vector2.zero, isGrounded: true);
                SyncCharacterCombatCollider(physicsRuntime, attacker);
                SyncCharacterCombatCollider(physicsRuntime, target);
            }

            long startFrame = 0L;
            HitResolveResult breakHit = default;
            for (int hitIndex = 1; hitIndex <= 3; hitIndex++)
            {
                ResetActorsForHit();
                breakHit = RunLightAttackUntilCombatResult(
                    world,
                    runtime,
                    motion,
                    combat,
                    attacker,
                    maxFrames: 50,
                    startFrame: (int)startFrame,
                    failureDetails: () => "hitIndex=" + hitIndex);

                Assert.AreEqual(HitResolveKind.Damage, breakHit.Kind);
                if (hitIndex < 3)
                {
                    TickCombatUntilActionInactive(
                        world,
                        runtime,
                        motion,
                        combat,
                        attacker,
                        startFrame: breakHit.Frame.Value + 1,
                        maxFrames: 120);
                    startFrame = breakHit.Frame.Value + 80L;
                }
            }

            Assert.AreEqual(1, combat.CohesionBreakEvents.Count);
            Assert.IsTrue(world.TryGetScatterPressure(target, out GameplayScatterPressureComponent brokenPressure));
            Assert.AreEqual(100, brokenPressure.ScatterDebt);
            Assert.AreEqual(PressureBand.Broken, brokenPressure.CurrentBand);
            Assert.IsTrue(brokenPressure.IsBroken);
            Assert.AreEqual(breakHit.Frame.Value, brokenPressure.LastCohesionBreakFrame);
            Assert.AreEqual(
                CharacterTestCharacterAttributeIds.DefaultCohesionBreakRecoveryDelayFrames,
                brokenPressure.BrokenRecoveryDelayFrames);
            Assert.AreEqual(
                CharacterTestCharacterActionConfigSource.KatanaCohesionBreakReactionActionId,
                target.Action.ActiveActionId);

            for (int i = 0; i < CharacterTestCharacterAttributeIds.DefaultCohesionBreakReactionFrames; i++)
            {
                target.TickCombatState();
                runtime.TickEntity(target);
            }

            Assert.AreEqual(0, target.CombatState.HitStunFramesRemaining);
            Assert.AreEqual(CharacterActionId.None, target.Action.ActiveActionId);
            Assert.IsTrue(runtime.TryStartIntent(
                target,
                CharacterTestCharacterActionConfigSource.LightAttackIntentId,
                new RuntimeFrame(breakHit.Frame.Value + 30L)));
            Assert.AreEqual(
                CharacterTestCharacterActionConfigSource.LightAttackActionId,
                target.Action.ActiveActionId);

            TickCombatUntilActionInactive(
                world,
                runtime,
                motion,
                combat,
                attacker,
                startFrame: breakHit.Frame.Value + 1,
                maxFrames: 120);

            for (int i = 0; i < 6; i++)
                target.TickCombatState();
            Assert.Less(
                target.CombatState.HitStunFramesRemaining,
                CharacterTestCharacterAttributeIds.DefaultCohesionBreakReactionFrames);

            long brokenWindowHitStart = breakHit.Frame.Value + 80L;
            ResetActorsForHit();
            int hpBeforeBrokenWindowHit = world.GetCurrentAttribute(target, CharacterTestCharacterAttributeIds.Hp);
            HitResolveResult brokenWindowHit = RunLightAttackUntilCombatResult(
                world,
                runtime,
                motion,
                combat,
                attacker,
                maxFrames: 50,
                startFrame: (int)brokenWindowHitStart,
                failureDetails: () => "brokenWindowHit");

            Assert.AreEqual(HitResolveKind.Damage, brokenWindowHit.Kind);
            Assert.AreEqual(
                hpBeforeBrokenWindowHit - brokenWindowHit.Damage,
                world.GetCurrentAttribute(target, CharacterTestCharacterAttributeIds.Hp));
            Assert.AreEqual(
                CharacterTestCharacterAttributeIds.DefaultCohesionBreakReactionFrames,
                target.CombatState.HitStunFramesRemaining);
            Assert.IsTrue(world.TryGetScatterPressure(target, out GameplayScatterPressureComponent afterBrokenWindowHit));
            Assert.AreEqual(100, afterBrokenWindowHit.ScatterDebt);
            Assert.AreEqual(PressureBand.Broken, afterBrokenWindowHit.CurrentBand);
            Assert.AreEqual(breakHit.Frame.Value, afterBrokenWindowHit.LastCohesionBreakFrame);
            Assert.AreEqual(brokenPressure.LastPressureFrame, afterBrokenWindowHit.LastPressureFrame);
            Assert.AreEqual(0, combat.ScatterPressureChangedEvents.Count);
            Assert.AreEqual(0, combat.ScatterImpactResolvedEvents.Count);
            Assert.AreEqual(0, combat.CohesionBreakEvents.Count);
            Assert.AreEqual(
                CharacterTestCharacterActionConfigSource.KatanaHitMidReactionActionId,
                target.Action.ActiveActionId);
            Assert.AreEqual(0, target.Action.LocalFrame);

            TickCombatUntilActionInactive(
                world,
                runtime,
                motion,
                combat,
                attacker,
                startFrame: brokenWindowHit.Frame.Value + 1,
                maxFrames: 120);

            for (int i = 0; i < CharacterTestCharacterAttributeIds.DefaultCohesionBreakReactionFrames; i++)
            {
                target.TickCombatState();
                runtime.TickEntity(target);
            }

            Assert.AreEqual(CharacterActionId.None, target.Action.ActiveActionId);

            long recoveryFrame = breakHit.Frame.Value
                + CharacterTestCharacterAttributeIds.DefaultCohesionBreakRecoveryDelayFrames;
            combat.Tick(world, runtime, new RuntimeFrame(recoveryFrame - 1L));
            Assert.IsTrue(world.TryGetScatterPressure(target, out GameplayScatterPressureComponent beforeRecovery));
            Assert.AreEqual(100, beforeRecovery.ScatterDebt);
            Assert.AreEqual(PressureBand.Broken, beforeRecovery.CurrentBand);
            Assert.IsTrue(beforeRecovery.IsBroken);
            Assert.AreEqual(PressureBand.Broken, target.CombatState.ScatterPressureBand);

            combat.Tick(world, runtime, new RuntimeFrame(recoveryFrame));
            Assert.IsTrue(world.TryGetScatterPressure(target, out GameplayScatterPressureComponent recovered));
            Assert.AreEqual(0, recovered.ScatterDebt);
            Assert.AreEqual(PressureBand.Stable, recovered.CurrentBand);
            Assert.IsFalse(recovered.IsBroken);
            Assert.AreEqual(0, target.CombatState.ScatterPressure);
            Assert.AreEqual(PressureBand.Stable, target.CombatState.ScatterPressureBand);
            Assert.AreEqual(1, combat.ScatterPressureChangedEvents.Count);
            Assert.AreEqual(0, combat.ScatterImpactResolvedEvents.Count);
            Assert.AreEqual(0, combat.CohesionBreakEvents.Count);

            long afterRecoveryHitStart = recoveryFrame + 10L;
            ResetActorsForHit();
            HitResolveResult afterRecoveryHit = RunLightAttackUntilCombatResult(
                world,
                runtime,
                motion,
                combat,
                attacker,
                maxFrames: 50,
                startFrame: (int)afterRecoveryHitStart,
                failureDetails: () => "afterRecoveryHit");

            Assert.AreEqual(HitResolveKind.Damage, afterRecoveryHit.Kind);
            Assert.IsTrue(world.TryGetScatterPressure(target, out GameplayScatterPressureComponent restarted));
            Assert.AreEqual(34, restarted.ScatterDebt);
            Assert.AreEqual(PressureBand.Stable, restarted.CurrentBand);
            Assert.IsFalse(restarted.IsBroken);
            Assert.AreEqual(1, combat.ScatterImpactResolvedEvents.Count);
            Assert.AreEqual(0, combat.CohesionBreakEvents.Count);
            Assert.AreEqual(CharacterActionId.None, target.Action.ActiveActionId);
        }

        [Test]
        public void CharacterActionRuntime_LaunchPowerBeforeBrokenDoesNotStartLaunchedSpecialHit()
        {
            string directory = CreateTempActionConfigDirectory();
            try
            {
                string launchGameplayTrack = @"{
        ""events"": [
          { ""frame"": 0, ""kind"": ""ApplyGameplayEffect"", ""targetPolicyId"": ""target.area_2m"", ""stableEventId"": ""launch.input.damage"", ""hitEvent"": { ""targetTypes"": [""HostileCharacter""], ""damage"": { ""damageTypeId"": ""force"", ""formulaId"": ""Flat"", ""baseDamage"": 1, ""impactPower"": 1, ""launchPower"": 100, ""tags"": [""area"", ""special_hit""], ""analysisCategory"": ""LaunchTest"" } } }
        ]
      }";
                WriteActionJsonFiles(
                    directory,
                    CreateActionSetsJson(),
                    CreateActionsJson(12, lightAttackGameplayTrackOverride: launchGameplayTrack));

                CharacterTestCharacterEntity attacker;
                CharacterTestCharacterEntity target;
                CharacterTestCharacterRuntimeWorld world = CreateRuntimeWorldWithAttackerAndTarget(
                    targetTeamId: 2,
                    out attacker,
                    out target);
                SetCurrentAttribute(world, target, CharacterTestCharacterAttributeIds.Hp, 1000);
                CharacterTestCharacterActionRuntime launchRuntime = CharacterTestCharacterActionConfigSource.Load(
                    new CharacterTestCharacterActionConfigSourceOptions(new[] { directory }));
                CharacterTestCharacterPhysicsRuntime physicsRuntime = CreateFrameworkPhysicsRuntime(includeWall: false);
                var motion = new CharacterTestCharacterMotionSystem();
                motion.SetMovementProfile(CreateMovementRuntime());
                motion.SetPhysicsRuntime(physicsRuntime);
                var combat = new CharacterTestCharacterCombatTraceSystem();
                combat.SetPhysicsRuntime(physicsRuntime);

                attacker.SetTransform(new CharacterTestCharacterTransformComponent(0f, 0f, 0f, 0f));
                attacker.SetMotionState(0f, Vector2.zero, isGrounded: true);
                AlignTargetWithDefaultProjectileSocketLine(target);
                target.SetMotionState(0f, Vector2.zero, isGrounded: true);
                SyncCharacterCombatCollider(physicsRuntime, attacker);
                SyncCharacterCombatCollider(physicsRuntime, target);

                HitResolveResult hit = RunLightAttackUntilCombatResult(
                    world,
                    launchRuntime,
                    motion,
                    combat,
                    attacker,
                    maxFrames: 20,
                    startFrame: 0,
                    failureDetails: () => "preBrokenLaunchHit");

                Assert.AreEqual(HitResolveKind.Damage, hit.Kind);
                Assert.IsFalse(target.SpecialHitState.IsLaunched);
                Assert.AreEqual(CharacterActionId.None, target.Action.ActiveActionId);
                Assert.IsTrue(world.TryGetScatterPressure(target, out GameplayScatterPressureComponent pressure));
                Assert.AreEqual(PressureBand.Stable, pressure.CurrentBand);
            }
            finally
            {
                DeleteDirectory(directory);
            }
        }

        [Test]
        public void CharacterActionRuntime_BrokenWindowLaunchInputStartsLaunchedSpecialHit()
        {
            string directory = CreateTempActionConfigDirectory();
            try
            {
                string launchGameplayTrack = @"{
        ""events"": [
          { ""frame"": 0, ""kind"": ""ApplyGameplayEffect"", ""targetPolicyId"": ""target.area_2m"", ""stableEventId"": ""launch.input.damage"", ""hitEvent"": { ""targetTypes"": [""HostileCharacter""], ""damage"": { ""damageTypeId"": ""force"", ""formulaId"": ""Flat"", ""baseDamage"": 1, ""impactPower"": 1, ""launchPower"": 100, ""tags"": [""area"", ""special_hit""], ""analysisCategory"": ""LaunchTest"" } } }
        ]
      }";
                WriteActionJsonFiles(
                    directory,
                    CreateActionSetsJson(),
                    CreateActionsJson(12, lightAttackGameplayTrackOverride: launchGameplayTrack));

                CharacterTestCharacterEntity attacker;
                CharacterTestCharacterEntity target;
                CharacterTestCharacterRuntimeWorld world = CreateRuntimeWorldWithAttackerAndTarget(
                    targetTeamId: 2,
                    out attacker,
                    out target);
                SetCurrentAttribute(world, target, CharacterTestCharacterAttributeIds.Hp, 1000);
                CharacterTestCharacterActionRuntime breakRuntime = CharacterTestCharacterActionConfigSource.Load();
                CharacterTestCharacterActionRuntime launchRuntime = CharacterTestCharacterActionConfigSource.Load(
                    new CharacterTestCharacterActionConfigSourceOptions(new[] { directory }));
                CharacterTestCharacterPhysicsRuntime physicsRuntime = CreateFrameworkPhysicsRuntime(includeWall: false);
                var motion = new CharacterTestCharacterMotionSystem();
                motion.SetMovementProfile(CreateMovementRuntime());
                motion.SetPhysicsRuntime(physicsRuntime);
                var combat = new CharacterTestCharacterCombatTraceSystem();
                combat.SetPhysicsRuntime(physicsRuntime);

                void ResetActorsForHit()
                {
                    attacker.SetTransform(new CharacterTestCharacterTransformComponent(0f, 0f, 0f, 0f));
                    attacker.SetMotionState(0f, Vector2.zero, isGrounded: true);
                    AlignTargetWithDefaultProjectileSocketLine(target);
                    target.SetMotionState(0f, Vector2.zero, isGrounded: true);
                    SyncCharacterCombatCollider(physicsRuntime, attacker);
                    SyncCharacterCombatCollider(physicsRuntime, target);
                }

                long startFrame = 0L;
                HitResolveResult breakHit = default;
                for (int hitIndex = 1; hitIndex <= 3; hitIndex++)
                {
                    ResetActorsForHit();
                    breakHit = RunLightAttackUntilCombatResult(
                        world,
                        breakRuntime,
                        motion,
                        combat,
                        attacker,
                        maxFrames: 50,
                        startFrame: (int)startFrame,
                        failureDetails: () => "breakHitIndex=" + hitIndex);

                    Assert.AreEqual(HitResolveKind.Damage, breakHit.Kind);
                    if (hitIndex < 3)
                    {
                        TickCombatUntilActionInactive(
                            world,
                            breakRuntime,
                            motion,
                            combat,
                            attacker,
                            startFrame: breakHit.Frame.Value + 1,
                            maxFrames: 120);
                        startFrame = breakHit.Frame.Value + 80L;
                    }
                }

                Assert.IsTrue(world.TryGetScatterPressure(target, out GameplayScatterPressureComponent brokenPressure));
                Assert.AreEqual(PressureBand.Broken, brokenPressure.CurrentBand);
                Assert.IsFalse(target.SpecialHitState.IsLaunched);
                for (int i = 0; i < CharacterTestCharacterAttributeIds.DefaultCohesionBreakReactionFrames; i++)
                {
                    target.TickCombatState();
                    breakRuntime.TickEntity(target);
                }

                Assert.AreEqual(CharacterActionId.None, target.Action.ActiveActionId);

                target.SetSpecialHitSusceptibility(CharacterTestSpecialHitSusceptibilityComponent.Disabled);
                long rejectedLaunchFrame = breakHit.Frame.Value + 80L;
                ResetActorsForHit();
                HitResolveResult rejectedLaunchHit = RunLightAttackUntilCombatResult(
                    world,
                    launchRuntime,
                    motion,
                    combat,
                    attacker,
                    maxFrames: 20,
                    startFrame: (int)rejectedLaunchFrame,
                    failureDetails: () => "rejectedLaunchHit");

                Assert.AreEqual(HitResolveKind.Damage, rejectedLaunchHit.Kind);
                Assert.IsFalse(target.SpecialHitState.IsLaunched);
                Assert.AreEqual(
                    CharacterTestCharacterActionConfigSource.KatanaCohesionBreakReactionActionId,
                    target.Action.ActiveActionId);

                TickCombatUntilActionInactive(
                    world,
                    launchRuntime,
                    motion,
                    combat,
                    attacker,
                    startFrame: rejectedLaunchHit.Frame.Value + 1,
                    maxFrames: 120);
                for (int i = 0; i < CharacterTestCharacterAttributeIds.DefaultCohesionBreakReactionFrames; i++)
                {
                    target.TickCombatState();
                    launchRuntime.TickEntity(target);
                }

                target.SetSpecialHitSusceptibility(CharacterTestSpecialHitSusceptibilityComponent.HumanoidDefault);
                Assert.AreEqual(CharacterActionId.None, target.Action.ActiveActionId);

                long launchFrame = rejectedLaunchHit.Frame.Value + 80L;
                ResetActorsForHit();
                HitResolveResult launchHit = RunLightAttackUntilCombatResult(
                    world,
                    launchRuntime,
                    motion,
                    combat,
                    attacker,
                    maxFrames: 20,
                    startFrame: (int)launchFrame,
                    failureDetails: () => "launchHit");

                Assert.AreEqual(HitResolveKind.Damage, launchHit.Kind);
                Assert.IsTrue(target.SpecialHitState.IsLaunched);
                Assert.AreEqual(CharacterTestSpecialHitPhase.Enter, target.SpecialHitState.Phase);
                Assert.AreEqual(
                    CharacterTestBattleStateDominantReason.Launch,
                    target.BattleState.DominantReason);
                Assert.AreEqual(
                    CharacterTestCharacterActionConfigSource.KatanaLaunchReactionActionId,
                    target.Action.ActiveActionId);
                Assert.AreEqual(0, target.Action.LocalFrame);
                Assert.IsTrue(world.TryGetScatterPressure(target, out GameplayScatterPressureComponent afterLaunch));
                Assert.AreEqual(brokenPressure.ScatterDebt, afterLaunch.ScatterDebt);
                Assert.AreEqual(breakHit.Frame.Value, afterLaunch.LastCohesionBreakFrame);

                launchRuntime.TickEntity(target);
                motion.Tick(world, 1f / 60f, new RuntimeFrame(launchHit.Frame.Value + 1L));

                Assert.IsTrue(target.SpecialHitState.IsLaunched);
                Assert.AreEqual(CharacterTestSpecialHitPhase.Airborne, target.SpecialHitState.Phase);
                Assert.Greater(target.VerticalVelocity, 0f);
                Assert.IsFalse(target.IsGrounded);
            }
            finally
            {
                DeleteDirectory(directory);
            }
        }

[Test]
        public void CharacterActionRuntime_LightAttackScatterPressure_UsesDefaultRecoveryGraceWindow()
        {
            CharacterTestCharacterEntity attacker;
            CharacterTestCharacterEntity target;
            CharacterTestCharacterRuntimeWorld world = CreateRuntimeWorldWithAttackerAndTarget(
                targetTeamId: 2,
                out attacker,
                out target);
            AlignTargetWithDefaultProjectileSocketLine(target);
            CharacterTestCharacterActionRuntime runtime = CharacterTestCharacterActionConfigSource.Load();
            CharacterTestCharacterPhysicsRuntime physicsRuntime = CreateFrameworkPhysicsRuntime(includeWall: false);
            var motion = new CharacterTestCharacterMotionSystem();
            motion.SetMovementProfile(CreateMovementRuntime());
            motion.SetPhysicsRuntime(physicsRuntime);
            var combat = new CharacterTestCharacterCombatTraceSystem();
            combat.SetPhysicsRuntime(physicsRuntime);

            HitResolveResult result = RunLightAttackUntilCombatResult(
                world,
                runtime,
                motion,
                combat,
                attacker,
                maxFrames: 40);

            Assert.AreEqual(HitResolveKind.Damage, result.Kind);
            Assert.IsTrue(world.TryGetScatterPressure(target, out GameplayScatterPressureComponent afterHit));
            Assert.AreEqual(34, afterHit.ScatterDebt);
            Assert.AreEqual(
                CharacterTestCharacterAttributeIds.DefaultScatterPressureRecoveryDelayFrames,
                afterHit.RecoveryDelayFrames);

            TickCombatUntilActionInactive(
                world,
                runtime,
                motion,
                combat,
                attacker,
                startFrame: result.Frame.Value + 1,
                maxFrames: 120);

            combat.Tick(
                world,
                runtime,
                new RuntimeFrame(afterHit.LastPressureFrame + CharacterTestCharacterAttributeIds.DefaultScatterPressureRecoveryDelayFrames));

            Assert.IsTrue(world.TryGetScatterPressure(target, out GameplayScatterPressureComponent beforeRecovery));
            Assert.AreEqual(34, beforeRecovery.ScatterDebt);
            Assert.AreEqual(PressureBand.Stable, beforeRecovery.CurrentBand);

            combat.Tick(
                world,
                runtime,
                new RuntimeFrame(afterHit.LastPressureFrame + CharacterTestCharacterAttributeIds.DefaultScatterPressureRecoveryDelayFrames + 1));

            Assert.IsTrue(world.TryGetScatterPressure(target, out GameplayScatterPressureComponent recovered));
            Assert.AreEqual(
                34 - CharacterTestCharacterAttributeIds.DefaultScatterPressureRecoveryRate,
                recovered.ScatterDebt);
            Assert.AreEqual(PressureBand.Stable, recovered.CurrentBand);
            Assert.IsFalse(recovered.IsBroken);
        }

[Test]
        public void CharacterRuntimeWorld_SyncCombatStateHealthDoesNotAllocateAfterWarmup()
        {
            CharacterTestCharacterRuntimeWorld world =
                CreateRuntimeWorldWithLocalPlayer(out CharacterTestCharacterEntity entity);
            world.SyncCombatStateHealth(entity);
            Assert.AreEqual(
                CharacterTestCharacterAttributeIds.DefaultMaxHp,
                entity.CombatState.Health);
            Assert.AreEqual(
                CharacterTestCharacterAttributeIds.DefaultScatterPressureMax,
                entity.CombatState.MaxScatterPressure);
            Assert.AreEqual(PressureBand.Stable, entity.CombatState.ScatterPressureBand);

            for (int i = 0; i < 8; i++)
                world.SyncCombatStateHealth(entity);

            _ = GC.GetAllocatedBytesForCurrentThread();
            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < 64; i++)
                world.SyncCombatStateHealth(entity);

            long allocatedBytes = GC.GetAllocatedBytesForCurrentThread() - before;
            Assert.AreEqual(0L, allocatedBytes);
        }

[Test]
        public void CharacterActionRuntime_HitPointDebugFrames_PrunesWithAttackTraceHistory()
        {
            CharacterTestCharacterEntity attacker;
            CharacterTestCharacterEntity target;
            CharacterTestCharacterRuntimeWorld world = CreateRuntimeWorldWithAttackerAndTarget(
                targetTeamId: 2,
                out attacker,
                out target);
            AlignTargetWithDefaultProjectileSocketLine(target);
            CharacterTestCharacterActionRuntime runtime = CharacterTestCharacterActionConfigSource.Load();
            CharacterTestCharacterPhysicsRuntime physicsRuntime = CreateFrameworkPhysicsRuntime(includeWall: false);
            var motion = new CharacterTestCharacterMotionSystem();
            motion.SetMovementProfile(CreateMovementRuntime());
            motion.SetPhysicsRuntime(physicsRuntime);
            var combat = new CharacterTestCharacterCombatTraceSystem();
            combat.SetPhysicsRuntime(physicsRuntime);

            HitResolveResult result = RunLightAttackUntilCombatResult(
                world,
                runtime,
                motion,
                combat,
                attacker,
                maxFrames: 40);

            Assert.AreEqual(HitResolveKind.Damage, result.Kind);
            Assert.Greater(combat.HitPointDebugFrames.Count, 0);

            int startFrame = result.Frame.Value + 1;
            for (int i = 0; i < CharacterTestCharacterCombatTraceSystem.AttackTraceDebugHistoryFrames + 2; i++)
            {
                RuntimeFrame frame = new RuntimeFrame(startFrame + i);
                runtime.TickEntity(attacker);
                motion.Tick(world, 1f / 60f, frame);
                combat.Tick(world, runtime, frame);
            }

            Assert.AreEqual(0, combat.HitPointDebugFrames.Count);
        }

[Test]
        public void CharacterCombatTraceSystem_DebugFramePruneDoesNotAllocateAfterWarmup()
        {
            var attackFrames = new List<CharacterTestAttackTraceDebugFrame>(128);
            var hitPointFrames = new List<CharacterTestHitPointDebugFrame>(128);
            for (int i = 0; i < 128; i++)
            {
                attackFrames.Add(new CharacterTestAttackTraceDebugFrame(
                    i,
                    entityId: 1,
                    hitWindowId: 1,
                    localFrame: i,
                    equipmentSlotId: global::MxFramework.Tests.Game.CharacterTest.MigrationFixtureIds.Id0048,
                    traceProfileId: global::MxFramework.Tests.Game.CharacterTest.MigrationFixtureIds.Id0079,
                    traceId: i,
                    rootPrev: Vector3.zero,
                    tipPrev: Vector3.forward,
                    rootNow: Vector3.zero,
                    tipNow: Vector3.forward,
                    radius: 0.25f));
                hitPointFrames.Add(new CharacterTestHitPointDebugFrame(
                    i,
                    sourceEntityId: 1,
                    targetEntityId: 2,
                    kind: HitResolveKind.Damage,
                    traceId: i,
                    hitPointWorld: FixVector3.Zero,
                    hitNormalWorld: FixVector3.Zero,
                    hitZoneId: MxFramework.Game.CharacterTest.CharacterTestAuthoredContentIds.D400_Id55,
                    bodyPartId: MxFramework.Game.CharacterTest.CharacterTestAuthoredContentIds.D400_Id10));
            }

            CharacterTestCharacterCombatTraceSystem.PruneAttackTraceDebugFrames(attackFrames, 30);
            CharacterTestCharacterCombatTraceSystem.PruneHitPointDebugFrames(hitPointFrames, 30);

            _ = GC.GetAllocatedBytesForCurrentThread();
            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int currentFrame = 31; currentFrame < 95; currentFrame++)
            {
                CharacterTestCharacterCombatTraceSystem.PruneAttackTraceDebugFrames(
                    attackFrames,
                    currentFrame);
                CharacterTestCharacterCombatTraceSystem.PruneHitPointDebugFrames(
                    hitPointFrames,
                    currentFrame);
            }

            long allocatedBytes = GC.GetAllocatedBytesForCurrentThread() - before;
            Assert.AreEqual(0L, allocatedBytes);
            Assert.AreEqual(64, attackFrames.Count);
            Assert.AreEqual(64, hitPointFrames.Count);
            Assert.AreEqual(64, attackFrames[0].RuntimeFrame);
            Assert.AreEqual(64, hitPointFrames[0].RuntimeFrame);
        }

[Test]
        public void CharacterCombatTraceSystem_TimelineSuppressionPruneDoesNotAllocateAfterWarmup()
        {
            var world = new CharacterTestCharacterRuntimeWorld();
            CharacterTestCharacterPhysicsRuntime physicsRuntime = CreateFrameworkPhysicsRuntime(includeWall: false);
            var combat = new CharacterTestCharacterCombatTraceSystem();
            combat.SetPhysicsRuntime(physicsRuntime);
            combat.SetAbilityEffectTimelineRegistry(new AbilityEffectTimelineRegistry(new[]
            {
                new AbilityEffectTimelineDefinition(
                    global::MxFramework.Tests.Game.CharacterTest.MigrationFixtureIds.Id0074,
                    512,
                    null)
            }));

            AbilityEffectTimelineRuntimeModule timeline =
                GetPrivateField<AbilityEffectTimelineRuntimeModule>(combat, "_effectTimelineRuntime");
            AbilityEffectTimelineStartResult start = timeline.StartTimeline(
                new AbilityEffectTimelineContext(
                    0,
                    global::MxFramework.Tests.Game.CharacterTest.MigrationFixtureIds.Id0074,
                    ownerEntityId: 1,
                    sourceEntityId: 1,
                    triggerEntityId: 0,
                    AbilityEffectTimelineTriggerKind.ProjectileImpact,
                    FixVector3.Zero,
                    new FixVector3(Fix64.Zero, Fix64.Zero, Fix64.One)),
                RuntimeFrame.Zero);
            Assert.IsTrue(start.Success, start.FailureReason);

            HashSet<int> suppressions =
                GetPrivateField<HashSet<int>>(combat, "_suppressFrameZeroCombatTimelineInstances");
            Assert.IsTrue(suppressions.Add(start.InstanceId));

            for (int i = 0; i < 8; i++)
                combat.Tick(world, null, new RuntimeFrame(i));

            _ = GC.GetAllocatedBytesForCurrentThread();
            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 8; i < 72; i++)
                combat.Tick(world, null, new RuntimeFrame(i));

            long allocatedBytes = GC.GetAllocatedBytesForCurrentThread() - before;
            Assert.AreEqual(0L, allocatedBytes);
            Assert.AreEqual(1, combat.AbilityEffectTimelineActiveInstanceCount);
            Assert.IsTrue(suppressions.Contains(start.InstanceId));
        }

[Test]
        public void CharacterActionRuntime_HitPointDebugFrame_FilterMatchesAcceptedAndBlockedOnly()
        {
            FixVector3 point = new FixVector3(Fix64.One, Fix64.FromInt(2), Fix64.FromInt(3));
            FixVector3 normal = new FixVector3(Fix64.Zero, Fix64.One, Fix64.Zero);
            var damageFrame = new CharacterTestHitPointDebugFrame(
                12,
                1,
                2,
                HitResolveKind.Damage,
                3001,
                point,
                normal,
                MxFramework.Game.CharacterTest.CharacterTestAuthoredContentIds.D400_Id56,
                CharacterTestAuthoredContentIds.D400_Id3);

            Assert.IsTrue(CharacterTestHitPointDebugFrame.ShouldRecordKind(HitResolveKind.Damage));
            Assert.AreEqual(HitResolveKind.Damage, damageFrame.Kind);
            Assert.AreEqual(1, damageFrame.SourceEntityId);
            Assert.AreEqual(2, damageFrame.TargetEntityId);
            Assert.AreEqual(point, damageFrame.HitPointWorld);
            Assert.AreEqual(normal, damageFrame.HitNormalWorld);
            Assert.AreEqual("hit.head", damageFrame.HitZoneId);
            Assert.AreEqual("head", damageFrame.BodyPartId);

            Assert.IsTrue(CharacterTestHitPointDebugFrame.ShouldRecordKind(HitResolveKind.Blocked));
            Assert.IsFalse(CharacterTestHitPointDebugFrame.ShouldRecordKind(HitResolveKind.Duplicate));
            Assert.IsFalse(CharacterTestHitPointDebugFrame.ShouldRecordKind(HitResolveKind.Friendly));
            Assert.IsFalse(CharacterTestHitPointDebugFrame.ShouldRecordKind(HitResolveKind.SelfDamage));
            Assert.IsFalse(CharacterTestHitPointDebugFrame.ShouldRecordKind(HitResolveKind.TargetDead));
        }

[Test]
        public void CharacterImpactResponseResolver_UsesMassImpulseScaleAndAsymptoticDistance()
        {
            CharacterTestImpactResponse equalMass = CharacterTestImpactResponseResolver.Resolve(
                impactPower: 12,
                hitZoneImpulseScale: 1f,
                sourceMassKg: 86f,
                targetMassKg: 86f,
                knockbackDirection: Vector3.forward,
                effectiveDeceleration: 38f);
            Assert.AreEqual(12, equalMass.ScatterImpact);
            Assert.AreEqual(12f, equalMass.EffectiveImpact, 0.0001f);
            Assert.Greater(equalMass.KnockbackSpeed, 0f);
            Assert.Greater(equalMass.PreviewDistanceMeters, 0f);
            Assert.Less(equalMass.PreviewDistanceMeters, 10f);

            CharacterTestImpactResponse smallIntoLarge = CharacterTestImpactResponseResolver.Resolve(
                impactPower: 12,
                hitZoneImpulseScale: 1f,
                sourceMassKg: 40f,
                targetMassKg: 200f,
                knockbackDirection: Vector3.forward,
                effectiveDeceleration: 38f);
            Assert.AreEqual(4, smallIntoLarge.ScatterImpact);
            Assert.AreEqual(0f, smallIntoLarge.KnockbackSpeed, 0.0001f);
            Assert.AreEqual(0f, smallIntoLarge.PreviewDistanceMeters, 0.0001f);

            CharacterTestImpactResponse largeIntoSmall = CharacterTestImpactResponseResolver.Resolve(
                impactPower: 12,
                hitZoneImpulseScale: 1f,
                sourceMassKg: 200f,
                targetMassKg: 40f,
                knockbackDirection: Vector3.forward,
                effectiveDeceleration: 38f);
            Assert.Greater(largeIntoSmall.ScatterImpact, equalMass.ScatterImpact);
            Assert.Greater(largeIntoSmall.PreviewDistanceMeters, equalMass.PreviewDistanceMeters);
            Assert.Less(largeIntoSmall.PreviewDistanceMeters, 10f);

            CharacterTestImpactResponse scaled = CharacterTestImpactResponseResolver.Resolve(
                impactPower: 12,
                hitZoneImpulseScale: 2f,
                sourceMassKg: 86f,
                targetMassKg: 86f,
                knockbackDirection: Vector3.forward,
                effectiveDeceleration: 38f);
            Assert.AreEqual(24, scaled.ScatterImpact);
            Assert.Greater(scaled.KnockbackSpeed, equalMass.KnockbackSpeed);
        }

        [Test]
        public void CharacterCombatDamageApplier_ExplosionInteractionOwnsFireballAndMeteorKnockback()
        {
            Assert.IsTrue(CharacterTestCombatDamageApplier.IsInteractionOwnedKnockback(
                CharacterTestCharacterActionConfigSource.FireballExplosionProjectileImpactStableEventId));
            Assert.IsTrue(CharacterTestCombatDamageApplier.IsInteractionOwnedKnockback(
                CharacterTestCharacterActionConfigSource.MeteorBarrageProjectileImpactStableEventId));
            Assert.IsFalse(CharacterTestCombatDamageApplier.IsInteractionOwnedKnockback(
                MxFramework.Game.CharacterTest.CharacterTestAuthoredContentIds.D401_Id161));
        }

[Test]
        public void CharacterActionRuntime_LightAttackHitTrace_UsesGameplayAttackAndDefenseAttributes()
        {
            CharacterTestCharacterEntity attacker;
            CharacterTestCharacterEntity target;
            CharacterTestCharacterRuntimeWorld world = CreateRuntimeWorldWithAttackerAndTarget(
                targetTeamId: 2,
                out attacker,
                out target);
            SetCurrentAttribute(world, attacker, CharacterTestCharacterAttributeIds.Attack, 40);
            SetCurrentAttribute(world, target, CharacterTestCharacterAttributeIds.Defense, 4);
            CharacterTestCharacterActionRuntime runtime = CharacterTestCharacterActionConfigSource.Load();
            CharacterTestCharacterPhysicsRuntime physicsRuntime = CreateFrameworkPhysicsRuntime(includeWall: false);
            var motion = new CharacterTestCharacterMotionSystem();
            motion.SetMovementProfile(CreateMovementRuntime());
            motion.SetPhysicsRuntime(physicsRuntime);
            var combat = new CharacterTestCharacterCombatTraceSystem();
            combat.SetPhysicsRuntime(physicsRuntime);

            HitResolveResult result = RunLightAttackUntilCombatResult(
                world,
                runtime,
                motion,
                combat,
                attacker,
                maxFrames: 40);

            Assert.AreEqual(HitResolveKind.Damage, result.Kind);
            Assert.AreEqual(31, result.Damage);
            Assert.AreEqual(69, world.GetCurrentAttribute(target, CharacterTestCharacterAttributeIds.Hp));
            DamageTelemetryRow telemetry = combat.DamageTelemetryRows
                .First(row => row.ResultKind == HitResolveKind.Damage);
            Assert.AreEqual(DamageFormulaIds.CharacterTestSoftDefenseV1, telemetry.FormulaTrace.FormulaId);
            Assert.AreEqual(32f, telemetry.FormulaTrace.Offense);
            Assert.AreEqual(4f, telemetry.FormulaTrace.TargetDefense);
            Assert.AreEqual(50, telemetry.FormulaTrace.DefenseReference);
            Assert.AreEqual(0.6f, telemetry.FormulaTrace.DefenseWeight);
            Assert.AreEqual(31, telemetry.IncomingDamage);
            Assert.AreEqual(31, telemetry.FinalDamage);
            Assert.AreEqual(100, telemetry.HpBefore);
            Assert.AreEqual(69, telemetry.HpAfter);
        }

[Test]
        public void CharacterActionRuntime_LightAttackHitTrace_KillsTargetAndDeathOverridesCohesionBreak()
        {
            CharacterTestCharacterEntity attacker;
            CharacterTestCharacterEntity target;
            CharacterTestCharacterRuntimeWorld world = CreateRuntimeWorldWithAttackerAndTarget(
                targetTeamId: 2,
                out attacker,
                out target);
            SetCurrentAttribute(world, attacker, CharacterTestCharacterAttributeIds.Attack, 40);
            SetCurrentAttribute(world, target, CharacterTestCharacterAttributeIds.Defense, 4);
            SetCurrentAttribute(world, target, CharacterTestCharacterAttributeIds.Hp, 31);
            Assert.IsTrue(world.TryGetScatterPressure(target, out GameplayScatterPressureComponent scatterPressure));
            var primedScatter = new GameplayScatterPressureComponent(
                scatterPressure.MaxScatterPressure,
                scatterPressure.RecoveryRate,
                scatterPressure.RecoveryDelayFrames,
                currentScatterPressure: 90,
                lastPressureFrame: 0,
                currentCohesionLimit: scatterPressure.CurrentCohesionLimit,
                maxScatterDebt: scatterPressure.MaxScatterDebt,
                brokenRecoveryDelayFrames: scatterPressure.BrokenRecoveryDelayFrames,
                lastCohesionBreakFrame: scatterPressure.LastCohesionBreakFrame);
            world.GameplayComponentWorld
                .GetOrCreateStore<GameplayScatterPressureComponent>()
                .Set(target.GameplayEntityId, primedScatter);
            target.SyncCombatScatterPressure(primedScatter);
            CharacterTestCharacterActionRuntime runtime =
                CharacterTestCharacterActionConfigSource.Load(
                    CharacterTestCharacterActionConfigSourceOptions.CreateLegacyFixtureFallback());
            CharacterTestCharacterPhysicsRuntime physicsRuntime = CreateFrameworkPhysicsRuntime(includeWall: false);
            var motion = new CharacterTestCharacterMotionSystem();
            motion.SetMovementProfile(CreateMovementRuntime());
            motion.SetPhysicsRuntime(physicsRuntime);
            var combat = new CharacterTestCharacterCombatTraceSystem();
            combat.SetPhysicsRuntime(physicsRuntime);

            HitResolveResult result = RunLightAttackUntilCombatResult(
                world,
                runtime,
                motion,
                combat,
                attacker,
                maxFrames: 40);

            Assert.AreEqual(HitResolveKind.Damage, result.Kind);
            Assert.AreEqual(31, result.Damage);
            Assert.AreEqual(0, world.GetCurrentAttribute(target, CharacterTestCharacterAttributeIds.Hp));
            Assert.AreEqual(CharacterTestCharacterLifeState.Dying, target.CombatState.LifeState);
            Assert.IsFalse(target.CombatState.CanReceiveCombatHit);
            Assert.AreEqual(
                CharacterTestCharacterActionConfigSource.KatanaDeathReactionActionId,
                target.Action.ActiveActionId);
            Assert.AreEqual(1, combat.DeathEvents.Count);
            Assert.AreEqual(target.Id.Value, combat.DeathEvents[0].TargetEntityId);
            Assert.AreEqual(attacker.Id.Value, combat.DeathEvents[0].SourceEntityId);
            Assert.AreEqual(0, combat.CohesionBreakEvents.Count);
            Assert.AreEqual(0, target.CombatState.ScatterPressure);
            Assert.IsTrue(world.TryGetGameplayLifecycle(target, out GameplayLifecycleComponent lifecycle));
            Assert.AreEqual(GameplayLifecycleState.Destroyed, lifecycle.State);
            Assert.IsFalse(lifecycle.IsAlive);
        }

[Test]
        public void CharacterRuntimeModule_LightAttackTick_KillsTargetAndKeepsDeathAfterPublisher()
        {
            CharacterTestCharacterDefinition definition = GetIronVanguardDefinition();
            CharacterTestCharacterPhysicsRuntime physicsRuntime = CreateFrameworkPhysicsRuntime(includeWall: false);
            CharacterTestTraceRecorder traceRecorder = CharacterTestTraceRecorder.CreateInMemory(256);
            var module = new CharacterTestCharacterRuntimeModule(
                null,
                NullRuntimeLogger.Instance,
                actionConfigOptions: CharacterTestCharacterActionConfigSourceOptions.CreateLegacyFixtureFallback(),
                physicsRuntime: physicsRuntime,
                traceRecorder: traceRecorder,
                createViews: false);
            using (var host = new RuntimeHost())
            {
                host.RegisterModule(module);
                host.Initialize();
                host.Start();

                Assert.IsTrue(module.EnqueueSpawn(new CharacterTestCharacterSpawnRequest(
                    definition.CharacterId,
                    definition.DefaultTeamId,
                    0f,
                    0f,
                    0f,
                    CharacterTestCharacterControlOwner.LocalPlayer)));
                Assert.IsTrue(module.EnqueueSpawn(new CharacterTestCharacterSpawnRequest(
                    definition.CharacterId,
                    2,
                    0f,
                    0f,
                    0.95f,
                    CharacterTestCharacterControlOwner.None)));
                host.Tick(0, 1d / 60d, 0d);

                CharacterTestCharacterEntity attacker = module.Entities[0];
                CharacterTestCharacterEntity target = module.Entities[1];
                attacker.SetMotionState(0f, Vector2.zero, isGrounded: true);
                target.SetMotionState(0f, Vector2.zero, isGrounded: true);
                AlignTargetWithDefaultProjectileSocketLine(target);
                SyncCharacterCombatCollider(physicsRuntime, attacker);
                SyncCharacterCombatCollider(physicsRuntime, target);
                SetCurrentAttribute(module.World, attacker, CharacterTestCharacterAttributeIds.Attack, 40);
                SetCurrentAttribute(module.World, target, CharacterTestCharacterAttributeIds.Defense, 4);
                SetCurrentAttribute(module.World, target, CharacterTestCharacterAttributeIds.Hp, 31);

                QueueGameplayIntent(attacker, CharacterTestGameplayIntentKind.AttackPrimary, new RuntimeFrame(1));

                int deathFrame = -1;
                for (int frame = 1; frame < 80; frame++)
                {
                    host.Tick(frame, 1d / 60d, frame / 60d);
                    if (target.CombatState.LifeState == CharacterTestCharacterLifeState.Dying)
                    {
                        deathFrame = frame;
                        break;
                    }
                }

                Assert.Greater(
                    deathFrame,
                    0,
                    "Expected module tick path to enter Dying.\n" + FormatTraceTail(traceRecorder, 96));
                Assert.AreEqual(0, module.World.GetCurrentAttribute(target, CharacterTestCharacterAttributeIds.Hp));
                Assert.AreEqual(0, target.CombatState.Health);
                Assert.AreEqual(
                    CharacterTestCharacterActionConfigSource.KatanaDeathReactionActionId,
                    target.Action.ActiveActionId);
                Assert.AreEqual(1, module.CombatTraceSystem.DeathEvents.Count);
                Assert.IsTrue(module.World.TryGetGameplayLifecycle(
                    target,
                    out GameplayLifecycleComponent dyingLifecycle));
                Assert.AreEqual(GameplayLifecycleState.Destroyed, dyingLifecycle.State);
                Assert.That(
                    traceRecorder.RecentEvents,
                    Has.Some.Matches<CharacterTestTraceEvent>(
                        evt => evt.Message.Contains("death begin")));
                Assert.That(
                    traceRecorder.RecentEvents,
                    Has.Some.Matches<CharacterTestTraceEvent>(
                        evt => evt.Message.Contains("hit reaction skipped reason=TargetNotAliveAfterDamage")));

                int deadFrame = -1;
                for (int frame = deathFrame + 1; frame < deathFrame + 100; frame++)
                {
                    host.Tick(frame, 1d / 60d, frame / 60d);
                    if (target.CombatState.LifeState == CharacterTestCharacterLifeState.Dead)
                    {
                        deadFrame = frame;
                        break;
                    }
                }

                Assert.Greater(deadFrame, deathFrame, "Expected death reaction action to complete.");
                Assert.AreEqual(CharacterTestCharacterLifeState.Dead, target.CombatState.LifeState);
                Assert.AreEqual(0, module.World.GetCurrentAttribute(target, CharacterTestCharacterAttributeIds.Hp));
                Assert.IsTrue(module.World.TryGetGameplayLifecycle(
                    target,
                    out GameplayLifecycleComponent deadLifecycle));
                Assert.AreEqual(GameplayLifecycleState.Destroyed, deadLifecycle.State);
                Assert.That(
                    traceRecorder.RecentEvents,
                    Has.Some.Matches<CharacterTestTraceEvent>(
                        evt => evt.Message.Contains("death complete")));
            }
        }

[Test]
        public void CharacterRuntimeModule_ActionsStillDispatchTimelineEventsAfterTargetDeath()
        {
            CharacterTestCharacterDefinition definition = GetIronVanguardDefinition();
            CharacterTestCharacterPhysicsRuntime physicsRuntime = CreateFrameworkPhysicsRuntime(includeWall: false);
            CharacterTestTraceRecorder traceRecorder = CharacterTestTraceRecorder.CreateInMemory(512);
            var module = new CharacterTestCharacterRuntimeModule(
                null,
                NullRuntimeLogger.Instance,
                actionConfigOptions: CharacterTestCharacterActionConfigSourceOptions.CreateLegacyFixtureFallback(),
                physicsRuntime: physicsRuntime,
                traceRecorder: traceRecorder,
                createViews: false);
            using (var host = new RuntimeHost())
            {
                host.RegisterModule(module);
                host.Initialize();
                host.Start();

                Assert.IsTrue(module.EnqueueSpawn(new CharacterTestCharacterSpawnRequest(
                    definition.CharacterId,
                    definition.DefaultTeamId,
                    0f,
                    0f,
                    0f,
                    CharacterTestCharacterControlOwner.LocalPlayer)));
                Assert.IsTrue(module.EnqueueSpawn(new CharacterTestCharacterSpawnRequest(
                    definition.CharacterId,
                    2,
                    0f,
                    0f,
                    0.95f,
                    CharacterTestCharacterControlOwner.None)));
                host.Tick(0, 1d / 60d, 0d);

                CharacterTestCharacterEntity player = module.Entities[0];
                CharacterTestCharacterEntity target = module.Entities[1];
                player.SetMotionState(0f, Vector2.zero, isGrounded: true);
                target.SetMotionState(0f, Vector2.zero, isGrounded: true);
                AlignTargetWithDefaultProjectileSocketLine(target);
                SyncCharacterCombatCollider(physicsRuntime, player);
                SyncCharacterCombatCollider(physicsRuntime, target);
                SetCurrentAttribute(module.World, player, CharacterTestCharacterAttributeIds.Attack, 40);
                SetCurrentAttribute(module.World, target, CharacterTestCharacterAttributeIds.Defense, 4);
                SetCurrentAttribute(module.World, target, CharacterTestCharacterAttributeIds.Hp, 31);

                QueueGameplayIntent(player, CharacterTestGameplayIntentKind.AttackPrimary, new RuntimeFrame(1));
                int deathFrame = TickUntilLifeState(
                    host,
                    target,
                    CharacterTestCharacterLifeState.Dead,
                    startFrame: 1,
                    maxFrameExclusive: 140);

                Assert.Greater(
                    deathFrame,
                    0,
                    "Expected the first target to die before the post-kill input check.\n"
                    + FormatTraceTail(traceRecorder, 128));
                Assert.AreEqual(CharacterTestCharacterLifeState.Alive, player.CombatState.LifeState);
                Assert.IsTrue(player.BattleState.CanMoveByInput);
                Assert.IsTrue(player.BattleState.CanStartNormalAction);

                int attackFrame = deathFrame + 10;
                bool accepted = module.TryApplyControlIntent(
                    new RuntimeFrame(attackFrame),
                    new CharacterTestControlIntent(
                        player.Id.Value,
                        new Vector2(0f, 1f),
                        new Vector2(0f, 1f),
                        jumpPressed: false,
                        jumpHeld: false,
                        jumpReleased: false,
                        attackPrimaryPressed: true),
                    out string message);
                Assert.IsTrue(accepted, message);
                host.Tick(attackFrame, 1d / 60d, attackFrame / 60d);

                Assert.AreEqual(CharacterTestCharacterActionConfigSource.LightAttackActionId, player.Action.ActiveActionId);
                Assert.AreEqual(CharacterTestCharacterActionConfigSource.LightAttackAnimationClipResourceKey, player.Animation.ActionKey);
                Assert.That(
                    traceRecorder.RecentEvents,
                    Has.Some.Matches<CharacterTestTraceEvent>(
                        evt => evt.EntityId == player.Id.Value
                            && evt.Category == "Attack"
                            && evt.Stage == "animation"
                            && evt.Message.Contains("action=light_attack")));
                Assert.AreEqual(0f, player.Action.MovementControlScale, 0.0001f);

                for (int frame = attackFrame + 1; frame <= attackFrame + 20; frame++)
                    host.Tick(frame, 1d / 60d, frame / 60d);

                Assert.Greater(player.Action.LocalFrame, 18);
                Assert.AreEqual(1f, player.Action.MovementControlScale, 0.0001f);
                Assert.AreEqual(1f, player.Action.RotationControlScale, 0.0001f);
                Assert.Greater(
                    player.DesiredVelocity.sqrMagnitude,
                    0.001f,
                    "Post-kill movement input should still reach the motion system.");

                int jumpFrame = attackFrame + 90;
                AdvanceHostUntilNoActiveAction(host, player, attackFrame + 21, jumpFrame);
                player.SetMotionState(0f, Vector2.zero, isGrounded: true);
                accepted = module.TryApplyControlIntent(
                    new RuntimeFrame(jumpFrame),
                    new CharacterTestControlIntent(
                        player.Id.Value,
                        Vector2.zero,
                        Vector2.zero,
                        jumpPressed: true,
                        jumpHeld: true,
                        jumpReleased: false,
                        attackPrimaryPressed: false),
                    out message);
                Assert.IsTrue(accepted, message);
                host.Tick(jumpFrame, 1d / 60d, jumpFrame / 60d);

                Assert.AreEqual(CharacterTestCharacterActionConfigSource.JumpActionId, player.Action.ActiveActionId);
                Assert.AreEqual(CharacterTestCharacterActionConfigSource.JumpAnimationClipResourceKey, player.Animation.ActionKey);
                Assert.Greater(
                    player.VerticalVelocity,
                    0f,
                    "Post-kill jump action must dispatch its launch impulse.");
            }
        }

        private static int TickUntilLifeState(
            RuntimeHost host,
            CharacterTestCharacterEntity entity,
            CharacterTestCharacterLifeState expected,
            int startFrame,
            int maxFrameExclusive)
        {
            for (int frame = startFrame; frame < maxFrameExclusive; frame++)
            {
                host.Tick(frame, 1d / 60d, frame / 60d);
                if (entity.CombatState.LifeState == expected)
                    return frame;
            }

            return -1;
        }

        private static string FormatTraceTail(CharacterTestTraceRecorder traceRecorder, int maxEvents)
        {
            if (traceRecorder == null || traceRecorder.RecentEvents.Count == 0)
                return "trace: <empty>";

            int start = Math.Max(0, traceRecorder.RecentEvents.Count - Math.Max(1, maxEvents));
            var builder = new StringBuilder(4096);
            builder.AppendLine("trace tail:");
            for (int i = start; i < traceRecorder.RecentEvents.Count; i++)
                builder.AppendLine(traceRecorder.RecentEvents[i].ToDisplayString());
            return builder.ToString();
        }

        private static void AdvanceHostUntilNoActiveAction(
            RuntimeHost host,
            CharacterTestCharacterEntity entity,
            int startFrame,
            int maxFrameExclusive)
        {
            for (int frame = startFrame; frame < maxFrameExclusive; frame++)
            {
                if (string.IsNullOrEmpty((entity.Action.ActiveActionId).ToString()))
                    return;

                host.Tick(frame, 1d / 60d, frame / 60d);
            }

            Assert.AreEqual(
                CharacterActionId.None,
                entity.Action.ActiveActionId,
                "Expected action to finish before frame " + maxFrameExclusive
                + ". active=" + entity.Action.ActiveActionId
                + " localFrame=" + entity.Action.LocalFrame
                + " state=" + entity.Action.State);
        }

[Test]
        public void CharacterActionRuntime_LightAttackHitTrace_ExposesAttackTraceDebugFrames()
        {
            CharacterTestCharacterEntity attacker;
            CharacterTestCharacterEntity target;
            CharacterTestCharacterRuntimeWorld world = CreateRuntimeWorldWithAttackerAndTarget(
                targetTeamId: 2,
                out attacker,
                out target);
            CharacterTestCharacterActionRuntime runtime = CharacterTestCharacterActionConfigSource.Load();
            CharacterTestCharacterPhysicsRuntime physicsRuntime = CreateFrameworkPhysicsRuntime(includeWall: false);
            var motion = new CharacterTestCharacterMotionSystem();
            motion.SetMovementProfile(CreateMovementRuntime());
            motion.SetPhysicsRuntime(physicsRuntime);
            var combat = new CharacterTestCharacterCombatTraceSystem();
            combat.SetPhysicsRuntime(physicsRuntime);

            HitResolveResult result = RunLightAttackUntilCombatResult(
                world,
                runtime,
                motion,
                combat,
                attacker,
                maxFrames: 40);

            Assert.AreEqual(HitResolveKind.Damage, result.Kind);
            Assert.Greater(combat.AttackTraceDebugFrames.Count, 0);
            CharacterTestAttackTraceDebugFrame debugFrame =
                combat.AttackTraceDebugFrames[combat.AttackTraceDebugFrames.Count - 1];
            Assert.AreEqual(attacker.Id.Value, debugFrame.EntityId);
            Assert.AreEqual("trace.iron_sword.blade", debugFrame.TraceProfileId);
            Assert.AreEqual(result.TraceId, debugFrame.TraceId);
            Assert.Greater(debugFrame.HitWindowId, 0);
            Assert.Greater(debugFrame.Radius, 0f);
            Assert.Greater((debugFrame.TipNow - debugFrame.TipPrev).sqrMagnitude, 0.0001f);
        }

[Test]
        public void CharacterActionRuntime_LightAttackHitTrace_DebugFrameMatchesPreviewPoseAndStopIsExclusive()
        {
            CharacterTestCharacterEntity attacker;
            CharacterTestCharacterEntity target;
            CharacterTestCharacterRuntimeWorld world = CreateRuntimeWorldWithAttackerAndTarget(
                targetTeamId: 2,
                out attacker,
                out target);
            CharacterTestCharacterActionRuntime runtime = CharacterTestCharacterActionConfigSource.Load();
            CharacterTestCharacterPhysicsRuntime physicsRuntime = CreateFrameworkPhysicsRuntime(includeWall: false);
            var motion = new CharacterTestCharacterMotionSystem();
            motion.SetMovementProfile(CreateMovementRuntime());
            motion.SetPhysicsRuntime(physicsRuntime);
            var combat = new CharacterTestCharacterCombatTraceSystem();
            combat.SetPhysicsRuntime(physicsRuntime);

            Assert.IsTrue(runtime.TryStartIntent(
                attacker,
                CharacterTestCharacterActionConfigSource.LightAttackIntentId,
                new RuntimeFrame(0)));
            motion.Tick(world, 1f / 60f, new RuntimeFrame(0));
            for (int i = 0; i < 32; i++)
            {
                RuntimeFrame frame = new RuntimeFrame(i);
                runtime.TickEntity(attacker);
                motion.Tick(world, 1f / 60f, frame);
                combat.Tick(world, runtime, frame);
            }

            CharacterTestAttackTraceDebugFrame[] frames = combat.AttackTraceDebugFrames
                .Where(frame => frame.EntityId == attacker.Id.Value)
                .ToArray();
            CollectionAssert.Contains(frames.Select(frame => frame.LocalFrame).ToArray(), 23);
            CollectionAssert.Contains(frames.Select(frame => frame.LocalFrame).ToArray(), 28);
            CollectionAssert.DoesNotContain(frames.Select(frame => frame.LocalFrame).ToArray(), 29);

            CharacterTestAttackTraceDebugFrame frame23 = frames.First(frame => frame.LocalFrame == 23);
            Assert.AreEqual(-0.409871f, frame23.RootNow.x, 0.0005f);
            Assert.AreEqual(1.491468f, frame23.RootNow.y, 0.0005f);
            Assert.AreEqual(0.100785f, frame23.RootNow.z, 0.0005f);
            Assert.AreEqual(-0.378244f, frame23.TipNow.x, 0.0005f);
            Assert.AreEqual(1.908389f, frame23.TipNow.y, 0.0005f);
            Assert.AreEqual(-0.567563f, frame23.TipNow.z, 0.0005f);
        }

        [Test]
        public void CharacterActionRuntime_LightAttackWeaponTraceProximalRange_HitsCloseTargetWhenBladeMisses()
        {
            CharacterTestCharacterEntity attacker;
            CharacterTestCharacterEntity target;
            CharacterTestCharacterRuntimeWorld world = CreateRuntimeWorldWithAttackerAndTarget(
                targetTeamId: 2,
                out attacker,
                out target);
            target.SetTransform(new CharacterTestCharacterTransformComponent(-0.4f, 0f, 0.68f, 0f));
            CharacterTestCharacterActionRuntime runtime = CharacterTestCharacterActionConfigSource.Load();
            CharacterTestCharacterPhysicsRuntime physicsRuntime = CreateFrameworkPhysicsRuntime(includeWall: false);
            var motion = new CharacterTestCharacterMotionSystem();
            motion.SetMovementProfile(CreateMovementRuntime());
            motion.SetPhysicsRuntime(physicsRuntime);
            var combat = new CharacterTestCharacterCombatTraceSystem();
            combat.SetPhysicsRuntime(physicsRuntime);
            SyncCharacterCombatCollider(physicsRuntime, target);
            var telemetry = new List<DamageTelemetryRow>();

            List<HitResolveResult> results = RunLightAttackForFrames(
                world,
                runtime,
                motion,
                combat,
                attacker,
                maxFrames: 40,
                observedTelemetry: telemetry);

            HitResolveResult[] accepted = results
                .Where(result => result.IsAcceptedDamage)
                .ToArray();
            Assert.AreEqual(1, accepted.Length);
            Assert.AreEqual(physicsRuntime.GetEntityId(target), accepted[0].TargetId);
            DamageTelemetryRow row = telemetry.First(item => item.ResultKind == HitResolveKind.Damage);
            Assert.AreEqual(DamageProfileSourceKind.WeaponTrace, row.SourceKind);
            Assert.AreEqual(CharacterTestAuthoredContentIds.D401_Id161, row.StableEventId);
            Assert.IsTrue(combat.AttackTraceDebugFrames.Any(frame => frame.HasProximalRange));
        }

        [Test]
        public void CharacterActionRuntime_LightAttackWeaponTraceProximalRange_DoesNotDuplicatePrimaryTraceHit()
        {
            CharacterTestCharacterEntity attacker;
            CharacterTestCharacterEntity target;
            CharacterTestCharacterRuntimeWorld world = CreateRuntimeWorldWithAttackerAndTarget(
                targetTeamId: 2,
                out attacker,
                out target);
            AlignTargetWithDefaultProjectileSocketLine(target);
            CharacterTestCharacterActionRuntime runtime = CharacterTestCharacterActionConfigSource.Load();
            CharacterTestCharacterPhysicsRuntime physicsRuntime = CreateFrameworkPhysicsRuntime(includeWall: false);
            var motion = new CharacterTestCharacterMotionSystem();
            motion.SetMovementProfile(CreateMovementRuntime());
            motion.SetPhysicsRuntime(physicsRuntime);
            var combat = new CharacterTestCharacterCombatTraceSystem();
            combat.SetPhysicsRuntime(physicsRuntime);
            SyncCharacterCombatCollider(physicsRuntime, target);
            var telemetry = new List<DamageTelemetryRow>();

            List<HitResolveResult> results = RunLightAttackForFrames(
                world,
                runtime,
                motion,
                combat,
                attacker,
                maxFrames: 40,
                observedTelemetry: telemetry);

            HitResolveResult[] accepted = results
                .Where(result => result.IsAcceptedDamage)
                .ToArray();
            Assert.AreEqual(1, accepted.Length);
            DamageTelemetryRow row = telemetry.First(item => item.ResultKind == HitResolveKind.Damage);
            Assert.AreEqual(DamageProfileSourceKind.WeaponTrace, row.SourceKind);
        }

        [Test]
        public void CharacterActionRuntime_LightAttackWeaponTraceProximalRange_DoesNotHitSideTarget()
        {
            CharacterTestCharacterEntity attacker;
            CharacterTestCharacterEntity target;
            CharacterTestCharacterRuntimeWorld sideWorld = CreateRuntimeWorldWithAttackerAndTarget(
                targetTeamId: 2,
                out attacker,
                out target);
            target.SetTransform(new CharacterTestCharacterTransformComponent(2f, 0f, 0.68f, 0f));
            var telemetry = new List<DamageTelemetryRow>();
            int accepted = RunProximalWeaponTraceScenario(sideWorld, attacker, target, telemetry);

            Assert.AreEqual(0, accepted);
            Assert.IsFalse(telemetry.Any(item => item.ResultKind == HitResolveKind.Damage));
        }

        [Test]
        public void CharacterActionRuntime_LightAttackWeaponTraceProximalRange_DoesNotExtendFarRange()
        {
            CharacterTestCharacterEntity attacker;
            CharacterTestCharacterEntity target;
            CharacterTestCharacterRuntimeWorld farWorld = CreateRuntimeWorldWithAttackerAndTarget(
                targetTeamId: 2,
                out attacker,
                out target);
            target.SetTransform(new CharacterTestCharacterTransformComponent(0f, 0f, 3f, 0f));
            Assert.AreEqual(
                0,
                RunProximalWeaponTraceScenario(farWorld, attacker, target));
        }

        [Test]
        public void CharacterActionRuntime_LightAttackHitTrace_DamagesEachEnemyOnceInSameWindow()
        {
            CharacterTestCharacterEntity attacker;
            CharacterTestCharacterEntity firstTarget;
            CharacterTestCharacterEntity secondTarget;
            CharacterTestCharacterRuntimeWorld world = CreateRuntimeWorldWithAttackerAndTwoTargets(
                targetTeamId: 2,
                out attacker,
                out firstTarget,
                out secondTarget);
            CharacterTestCharacterActionRuntime runtime = CharacterTestCharacterActionConfigSource.Load();
            CharacterTestCharacterPhysicsRuntime physicsRuntime = CreateFrameworkPhysicsRuntime(includeWall: false);
            var motion = new CharacterTestCharacterMotionSystem();
            motion.SetMovementProfile(CreateMovementRuntime());
            motion.SetPhysicsRuntime(physicsRuntime);
            var combat = new CharacterTestCharacterCombatTraceSystem();
            combat.SetPhysicsRuntime(physicsRuntime);

            List<HitResolveResult> results = RunLightAttackUntilCombatResults(
                world,
                runtime,
                motion,
                combat,
                attacker,
                expectedAcceptedDamageCount: 2,
                maxFrames: 40);

            HitResolveResult[] accepted = results
                .Where(result => result.IsAcceptedDamage)
                .ToArray();
            Assert.AreEqual(2, accepted.Length);
            CollectionAssert.AreEquivalent(
                new[]
                {
                    physicsRuntime.GetEntityId(firstTarget),
                    physicsRuntime.GetEntityId(secondTarget)
                },
                accepted.Select(result => result.TargetId).ToArray());
            Assert.AreEqual(CharacterTestCharacterCombatStateComponent.DefaultMaxHealth - accepted[0].Damage, firstTarget.CombatState.Health);
            Assert.AreEqual(CharacterTestCharacterCombatStateComponent.DefaultMaxHealth - accepted[0].Damage, secondTarget.CombatState.Health);
            Assert.AreEqual(firstTarget.CombatState.Health, world.GetCurrentAttribute(firstTarget, CharacterTestCharacterAttributeIds.Hp));
            Assert.AreEqual(secondTarget.CombatState.Health, world.GetCurrentAttribute(secondTarget, CharacterTestCharacterAttributeIds.Hp));
        }

[Test]
        public void CharacterActionRuntime_LightAttackHitTrace_HitOnceRejectsRepeatedTarget()
        {
            CharacterTestCharacterEntity attacker;
            CharacterTestCharacterEntity target;
            CharacterTestCharacterRuntimeWorld world = CreateRuntimeWorldWithAttackerAndTarget(
                targetTeamId: 2,
                out attacker,
                out target);
            CharacterTestCharacterActionRuntime runtime = CharacterTestCharacterActionConfigSource.Load();
            CharacterTestCharacterPhysicsRuntime physicsRuntime = CreateFrameworkPhysicsRuntime(includeWall: false);
            var motion = new CharacterTestCharacterMotionSystem();
            motion.SetMovementProfile(CreateMovementRuntime());
            motion.SetPhysicsRuntime(physicsRuntime);
            var combat = new CharacterTestCharacterCombatTraceSystem();
            combat.SetPhysicsRuntime(physicsRuntime);

            HitResolveResult firstResult = RunLightAttackUntilCombatResult(
                world,
                runtime,
                motion,
                combat,
                attacker,
                maxFrames: 40);
            Assert.AreEqual(HitResolveKind.Damage, firstResult.Kind);

            HitResolveResult secondResult = RunUntilCombatResult(
                world,
                runtime,
                motion,
                combat,
                attacker,
                maxFrames: 5,
                startFrame: 40);

            Assert.AreEqual(HitResolveKind.Duplicate, secondResult.Kind);
            Assert.AreEqual(0, secondResult.Damage);
            Assert.AreEqual(physicsRuntime.GetEntityId(target), secondResult.TargetId);
            Assert.AreEqual(CharacterTestCharacterCombatStateComponent.DefaultMaxHealth - firstResult.Damage, target.CombatState.Health);
        }

[Test]
        public void CharacterActionRuntime_LightAttackHitTrace_DefenseBreakIncreasesDamageUntilExpired()
        {
            CharacterTestCharacterEntity attacker;
            CharacterTestCharacterEntity target;
            CharacterTestCharacterRuntimeWorld world = CreateRuntimeWorldWithAttackerAndTarget(
                targetTeamId: 2,
                out attacker,
                out target);
            AlignTargetWithDefaultProjectileSocketLine(target);
            CharacterTestCharacterActionRuntime runtime = CharacterTestCharacterActionConfigSource.Load();
            CharacterTestCharacterPhysicsRuntime physicsRuntime = CreateFrameworkPhysicsRuntime(includeWall: false);
            var motion = new CharacterTestCharacterMotionSystem();
            motion.SetMovementProfile(CreateMovementRuntime());
            motion.SetPhysicsRuntime(physicsRuntime);
            var combat = new CharacterTestCharacterCombatTraceSystem();
            combat.SetPhysicsRuntime(physicsRuntime);

            HitResolveResult firstResult = RunLightAttackUntilCombatResult(
                world,
                runtime,
                motion,
                combat,
                attacker,
                maxFrames: 40,
                startFrame: 0,
                failureDetails: () => "phase=first");
            Assert.AreEqual(HitResolveKind.Damage, firstResult.Kind);
            DamageTelemetryRow firstTelemetry = combat.DamageTelemetryRows
                .First(row => row.ResultKind == HitResolveKind.Damage);
            Assert.AreEqual(
                (float)CharacterTestCharacterAttributeIds.DefaultDefense,
                firstTelemetry.FormulaTrace.TargetDefense,
                0.0001f);
            AssertOnHitDefenseBreakBuff(
                world,
                target,
                firstResult.TraceId,
                firstResult.Frame.Value + CharacterTestGuardAuraIds.OnHitDefenseBreakDurationFrames);

            TickCombatUntilActionInactive(
                world,
                runtime,
                motion,
                combat,
                attacker,
                startFrame: 40,
                maxFrames: 120);

            HitResolveResult secondResult = RunLightAttackUntilCombatResult(
                world,
                runtime,
                motion,
                combat,
                attacker,
                maxFrames: 40,
                startFrame: 160,
                failureDetails: () => "phase=second");

            Assert.AreEqual(HitResolveKind.Damage, secondResult.Kind);
            DamageTelemetryRow secondTelemetry = combat.DamageTelemetryRows
                .First(row => row.ResultKind == HitResolveKind.Damage);
            Assert.AreEqual(
                (float)(CharacterTestCharacterAttributeIds.DefaultDefense
                + CharacterTestGuardAuraIds.OnHitDefensePenalty),
                secondTelemetry.FormulaTrace.TargetDefense,
                0.0001f);
            Assert.Greater(secondResult.Damage, firstResult.Damage);
            Assert.AreEqual(physicsRuntime.GetEntityId(target), secondResult.TargetId);
            AssertOnHitDefenseBreakBuff(
                world,
                target,
                secondResult.TraceId,
                secondResult.Frame.Value + CharacterTestGuardAuraIds.OnHitDefenseBreakDurationFrames);

            TickCombatUntilActionInactive(
                world,
                runtime,
                motion,
                combat,
                attacker,
                startFrame: secondResult.Frame.Value + 1,
                maxFrames: 120);

            AlignTargetWithDefaultProjectileSocketLine(target);
            target.SetMotionState(0f, Vector2.zero, isGrounded: true);
            HitResolveResult thirdResult = RunLightAttackUntilCombatResult(
                world,
                runtime,
                motion,
                combat,
                attacker,
                maxFrames: 40,
                startFrame: secondResult.Frame.Value
                    + (int)CharacterTestGuardAuraIds.OnHitDefenseBreakDurationFrames
                    + 10,
                failureDetails: () => "phase=third");

            Assert.AreEqual(HitResolveKind.Damage, thirdResult.Kind);
            DamageTelemetryRow thirdTelemetry = combat.DamageTelemetryRows
                .First(row => row.ResultKind == HitResolveKind.Damage);
            Assert.AreEqual(
                (float)CharacterTestCharacterAttributeIds.DefaultDefense,
                thirdTelemetry.FormulaTrace.TargetDefense,
                0.0001f);
            Assert.AreEqual(firstResult.Damage, thirdResult.Damage);
            Assert.AreEqual(
                CharacterTestCharacterCombatStateComponent.DefaultMaxHealth
                - firstResult.Damage
                - secondResult.Damage
                - thirdResult.Damage,
                target.CombatState.Health);
            Assert.AreEqual(target.CombatState.Health, world.GetCurrentAttribute(target, CharacterTestCharacterAttributeIds.Hp));
            AssertOnHitDefenseBreakBuff(
                world,
                target,
                thirdResult.TraceId,
                thirdResult.Frame.Value + CharacterTestGuardAuraIds.OnHitDefenseBreakDurationFrames);
        }

[Test]
        public void CharacterActionRuntime_LightAttackHitTrace_FriendlyTargetDoesNotApplyDamage()
        {
            CharacterTestCharacterEntity attacker;
            CharacterTestCharacterEntity target;
            CharacterTestCharacterRuntimeWorld world = CreateRuntimeWorldWithAttackerAndTarget(
                targetTeamId: GetIronVanguardDefinition().DefaultTeamId,
                out attacker,
                out target);
            CharacterTestCharacterActionRuntime runtime = CharacterTestCharacterActionConfigSource.Load();
            CharacterTestCharacterPhysicsRuntime physicsRuntime = CreateFrameworkPhysicsRuntime(includeWall: false);
            var motion = new CharacterTestCharacterMotionSystem();
            motion.SetMovementProfile(CreateMovementRuntime());
            motion.SetPhysicsRuntime(physicsRuntime);
            var combat = new CharacterTestCharacterCombatTraceSystem();
            combat.SetPhysicsRuntime(physicsRuntime);

            List<HitResolveResult> observedResults = RunLightAttackForFrames(
                world,
                runtime,
                motion,
                combat,
                attacker,
                maxFrames: 40);

            Assert.AreEqual(0, observedResults.Count);
            Assert.AreEqual(CharacterTestCharacterCombatStateComponent.DefaultMaxHealth, target.CombatState.Health);
            Assert.AreEqual(0, target.CombatState.LastDamage);
            Assert.AreEqual(0, target.CombatState.ScatterPressure);
        }

[Test]
        public void CharacterActionRuntime_LightAttackHitTrace_ThirdPartyTargetDoesNotMatchHostileTargetType()
        {
            CharacterTestCharacterEntity attacker;
            CharacterTestCharacterEntity target;
            CharacterTestCharacterRuntimeWorld world = CreateRuntimeWorldWithAttackerAndTarget(
                targetTeamId: 3,
                out attacker,
                out target);
            CharacterTestCharacterActionRuntime runtime = CharacterTestCharacterActionConfigSource.Load();
            CharacterTestCharacterPhysicsRuntime physicsRuntime = CreateFrameworkPhysicsRuntime(includeWall: false);
            var motion = new CharacterTestCharacterMotionSystem();
            motion.SetMovementProfile(CreateMovementRuntime());
            motion.SetPhysicsRuntime(physicsRuntime);
            var combat = new CharacterTestCharacterCombatTraceSystem();
            combat.SetPhysicsRuntime(physicsRuntime);

            List<HitResolveResult> observedResults = RunLightAttackForFrames(
                world,
                runtime,
                motion,
                combat,
                attacker,
                maxFrames: 40);

            Assert.AreEqual(0, observedResults.Count);
            Assert.AreEqual(CharacterTestCharacterCombatStateComponent.DefaultMaxHealth, target.CombatState.Health);
            Assert.AreEqual(0, target.CombatState.LastDamage);
            Assert.AreEqual(0, target.CombatState.ScatterPressure);
        }

[Test]
        public void CharacterTestHitEventResolver_DistinguishesHostileAndThirdPartyCharacters()
        {
            CharacterTestCharacterEntity attacker;
            CharacterTestCharacterEntity target;
            CharacterTestCharacterRuntimeWorld world = CreateRuntimeWorldWithAttackerAndTarget(
                targetTeamId: 3,
                out attacker,
                out target);
            var lookup = new CharacterTestCombatEntityLookup();
            lookup.Configure(world);
            var targetId = new CombatEntityId(CharacterTestCharacterPhysicsRuntime.DynamicBodyIdStart + target.Id.Value);

            bool hostileAllowed = CharacterTestCombatHitEventResolver.AllowsTarget(
                new CombatHitEventConfig(CombatHitTargetTypes.HostileCharacter),
                attacker,
                targetId,
                lookup,
                out bool hostileEnvironmentTarget);
            bool thirdPartyAllowed = CharacterTestCombatHitEventResolver.AllowsTarget(
                new CombatHitEventConfig(CombatHitTargetTypes.ThirdPartyCharacter),
                attacker,
                targetId,
                lookup,
                out bool thirdPartyEnvironmentTarget);

            Assert.IsFalse(hostileAllowed);
            Assert.IsFalse(hostileEnvironmentTarget);
            Assert.IsTrue(thirdPartyAllowed);
            Assert.IsFalse(thirdPartyEnvironmentTarget);
        }

[Test]
        public void CharacterActionRuntime_LightAttackHitTrace_UsesPublicImpactPowerInsteadOfLegacyProfileOverride()
        {
            CharacterTestCharacterEntity attacker;
            CharacterTestCharacterEntity target;
            CharacterTestCharacterRuntimeWorld world = CreateRuntimeWorldWithAttackerAndTarget(
                targetTeamId: 2,
                out attacker,
                out target);
            CharacterTestCharacterActionRuntime runtime = CharacterTestCharacterActionConfigSource.Load();
            CharacterTestCharacterPhysicsRuntime physicsRuntime = CreateFrameworkPhysicsRuntime(includeWall: false);
            var motion = new CharacterTestCharacterMotionSystem();
            motion.SetMovementProfile(CreateMovementRuntime());
            motion.SetPhysicsRuntime(physicsRuntime);
            CharacterTestCharacterCombatTraceSystem combat = CreateCombatTraceSystem();
            combat.SetPhysicsRuntime(physicsRuntime);

            Assert.IsTrue(runtime.TryStartIntent(
                target,
                CharacterTestCharacterActionConfigSource.LightAttackIntentId,
                new RuntimeFrame(0)));
            Assert.AreEqual(
                CharacterTestCharacterActionConfigSource.LightAttackActionId,
                target.Action.ActiveActionId);

            HitResolveResult result = RunLightAttackUntilCombatResult(
                world,
                runtime,
                motion,
                combat,
                attacker,
                maxFrames: 40);

            Assert.AreEqual(HitResolveKind.Damage, result.Kind);
            Assert.AreEqual(
                34,
                target.CombatState.ScatterPressure);
            Assert.AreEqual(PressureBand.Stable, target.CombatState.ScatterPressureBand);
            Assert.AreEqual(CharacterTestCharacterActionConfigSource.LightAttackActionId, target.Action.ActiveActionId);
            Assert.AreEqual(-1, target.CombatState.LastCohesionBreakFrame);
            Assert.AreEqual(0, combat.CohesionBreakEvents.Count);
        }

[Test]
        public void CharacterActionRuntime_CohesionSupportWithoutWindowDoesNotRaiseLightAttackLimit()
        {
            string directory = CreateTempActionConfigDirectory();
            try
            {
                const string supportWithoutWindow = @"{
        ""enabled"": true,
        ""supportRate"": 1.5,
        ""minSupportRatio"": 1.0,
        ""hasCohesionSupportWindow"": false,
        ""cohesionSupportWindowStartFrame"": 0,
        ""cohesionSupportWindowEndFrame"": 0
      }";
                WriteActionJsonFiles(
                    directory,
                    CreateActionSetsJson(),
                    CreateActionsJson(12, lightAttackCohesionSupportOverride: supportWithoutWindow));
                CharacterTestCharacterRuntimeWorld world =
                    CreateRuntimeWorldWithLocalPlayer(out CharacterTestCharacterEntity entity);
                CharacterTestCharacterActionRuntime runtime = CharacterTestCharacterActionConfigSource.Load(
                    new CharacterTestCharacterActionConfigSourceOptions(new[] { directory }));
                CharacterTestCharacterCombatTraceSystem combat = new CharacterTestCharacterCombatTraceSystem();
                combat.SetPhysicsRuntime(CreateFrameworkPhysicsRuntime(includeWall: false));

                Assert.IsTrue(runtime.TryStartIntent(
                    entity,
                    CharacterTestCharacterActionConfigSource.LightAttackIntentId,
                    RuntimeFrame.Zero));
                runtime.TickEntity(entity);
                combat.Tick(world, runtime, RuntimeFrame.Zero);

                Assert.IsTrue(world.TryGetScatterPressure(entity, out GameplayScatterPressureComponent pressure));
                Assert.AreEqual(
                    CharacterTestCharacterAttributeIds.DefaultScatterPressureMax,
                    pressure.CurrentCohesionLimit);
                Assert.AreEqual(PressureBand.Stable, pressure.CurrentBand);
                Assert.IsFalse(pressure.IsBroken);
            }
            finally
            {
                DeleteDirectory(directory);
            }
        }

[Test]
        public void CharacterActionRuntime_CohesionSupportWindowRaisesLimitOnlyInsideWindow()
        {
            string directory = CreateTempActionConfigDirectory();
            try
            {
                const string supportWindow = @"{
        ""enabled"": true,
        ""supportRate"": 1.5,
        ""minSupportRatio"": 1.0,
        ""hasCohesionSupportWindow"": true,
        ""cohesionSupportWindowStartFrame"": 2,
        ""cohesionSupportWindowEndFrame"": 4
      }";
                WriteActionJsonFiles(
                    directory,
                    CreateActionSetsJson(),
                    CreateActionsJson(12, lightAttackCohesionSupportOverride: supportWindow));
                CharacterTestCharacterRuntimeWorld world =
                    CreateRuntimeWorldWithLocalPlayer(out CharacterTestCharacterEntity entity);
                CharacterTestCharacterActionRuntime runtime = CharacterTestCharacterActionConfigSource.Load(
                    new CharacterTestCharacterActionConfigSourceOptions(new[] { directory }));
                CharacterTestCharacterCombatTraceSystem combat = new CharacterTestCharacterCombatTraceSystem();
                combat.SetPhysicsRuntime(CreateFrameworkPhysicsRuntime(includeWall: false));

                Assert.IsTrue(runtime.TryStartIntent(
                    entity,
                    CharacterTestCharacterActionConfigSource.LightAttackIntentId,
                    RuntimeFrame.Zero));

                bool sawOutsideBeforeWindow = false;
                bool sawInsideWindow = false;
                bool sawOutsideAfterWindow = false;
                for (int frame = 0; frame < 8; frame++)
                {
                    runtime.TickEntity(entity);
                    combat.Tick(world, runtime, new RuntimeFrame(frame));
                    Assert.IsTrue(world.TryGetScatterPressure(entity, out GameplayScatterPressureComponent pressure));

                    if (entity.Action.LocalFrame < 2)
                    {
                        Assert.AreEqual(
                            CharacterTestCharacterAttributeIds.DefaultScatterPressureMax,
                            pressure.CurrentCohesionLimit);
                        sawOutsideBeforeWindow = true;
                    }

                    if (entity.Action.LocalFrame >= 2 && entity.Action.LocalFrame <= 4)
                    {
                        Assert.AreEqual(150, pressure.CurrentCohesionLimit);
                        sawInsideWindow = true;
                    }

                    if (entity.Action.LocalFrame > 4 && entity.Action.LocalFrame < 9)
                    {
                        Assert.AreEqual(
                            CharacterTestCharacterAttributeIds.DefaultScatterPressureMax,
                            pressure.CurrentCohesionLimit);
                        sawOutsideAfterWindow = true;
                    }
                }

                Assert.IsTrue(sawOutsideBeforeWindow);
                Assert.IsTrue(sawInsideWindow);
                Assert.IsTrue(sawOutsideAfterWindow);
            }
            finally
            {
                DeleteDirectory(directory);
            }
        }

[Test]
        public void CharacterActionRuntime_ApplyGameplayEffectArea_DamagesEnemyThroughCommonDamagePayload()
        {
            string directory = CreateTempActionConfigDirectory();
            try
            {
                string gameplayTrack = @"{
        ""events"": [
          { ""frame"": 0, ""kind"": ""ApplyGameplayEffect"", ""targetPolicyId"": ""target.area_2m"", ""stableEventId"": ""shockwave.area.damage"", ""hitEvent"": { ""targetTypes"": [""HostileCharacter""], ""damage"": { ""damageTypeId"": ""force"", ""formulaId"": ""Flat"", ""baseDamage"": 12, ""impactPower"": 4, ""tags"": [""area"", ""shockwave"", ""scatter""], ""analysisCategory"": ""AreaPulse"" }, ""onHitBuffs"": [{ ""buffId"": 601009 }] } }
        ]
      }";
                WriteActionJsonFiles(
                    directory,
                    CreateActionSetsJson(),
                    CreateActionsJson(12, lightAttackGameplayTrackOverride: gameplayTrack));
                CharacterTestCharacterEntity attacker;
                CharacterTestCharacterEntity target;
                CharacterTestCharacterRuntimeWorld world = CreateRuntimeWorldWithAttackerAndTarget(
                    targetTeamId: 2,
                    out attacker,
                    out target);
                CharacterTestCharacterActionRuntime runtime = CharacterTestCharacterActionConfigSource.Load(
                    new CharacterTestCharacterActionConfigSourceOptions(new[] { directory }));
                CharacterTestCharacterPhysicsRuntime physicsRuntime = CreateFrameworkPhysicsRuntime(includeWall: false);
                var motion = new CharacterTestCharacterMotionSystem();
                motion.SetMovementProfile(CreateMovementRuntime());
                motion.SetPhysicsRuntime(physicsRuntime);
                var combat = new CharacterTestCharacterCombatTraceSystem();
                combat.SetPhysicsRuntime(physicsRuntime);

                HitResolveResult result = RunLightAttackUntilCombatResult(
                    world,
                    runtime,
                    motion,
                    combat,
                    attacker,
                    maxFrames: 10);

                Assert.AreEqual(HitResolveKind.Damage, result.Kind);
                Assert.AreEqual(12, result.Damage);
                Assert.AreEqual(88, world.GetCurrentAttribute(target, CharacterTestCharacterAttributeIds.Hp));
                DamageTelemetryRow telemetry = combat.DamageTelemetryRows
                    .First(row => row.ResultKind == HitResolveKind.Damage);
                Assert.AreEqual(DamageProfileSourceKind.AreaPulse, telemetry.SourceKind);
                Assert.AreEqual(0, telemetry.DamageProfileId);
                Assert.AreEqual("shockwave.area.damage", telemetry.StableEventId);
                Assert.AreEqual("force", telemetry.DamageTypeId);
                Assert.AreEqual(100, telemetry.HpBefore);
                Assert.AreEqual(88, telemetry.HpAfter);
                Assert.AreEqual(4, target.CombatState.ScatterPressure);
                Assert.AreEqual(PressureBand.Stable, target.CombatState.ScatterPressureBand);
                AssertOnHitDefenseBreakBuff(world, target, result.TraceId);
                Assert.AreEqual(1, combat.OnHitBuffLastSummary.RequestedBuffCount);
            }
            finally
            {
                DeleteDirectory(directory);
            }
        }

[Test]
        public void CharacterActionRuntime_ApplyGameplayEffectArea_FriendlyTargetDoesNotApplyDamage()
        {
            string directory = CreateTempActionConfigDirectory();
            try
            {
                string gameplayTrack = @"{
        ""events"": [
          { ""frame"": 0, ""kind"": ""ApplyGameplayEffect"", ""targetPolicyId"": ""target.area_2m"", ""stableEventId"": ""shockwave.area.damage"", ""hitEvent"": { ""damage"": { ""damageTypeId"": ""force"", ""formulaId"": ""Flat"", ""baseDamage"": 6, ""impactPower"": 12, ""tags"": [""area"", ""shockwave"", ""scatter""], ""analysisCategory"": ""AreaPulse"" } } }
        ]
      }";
                WriteActionJsonFiles(
                    directory,
                    CreateActionSetsJson(),
                    CreateActionsJson(12, lightAttackGameplayTrackOverride: gameplayTrack));
                CharacterTestCharacterEntity attacker;
                CharacterTestCharacterEntity target;
                CharacterTestCharacterRuntimeWorld world = CreateRuntimeWorldWithAttackerAndTarget(
                    targetTeamId: GetIronVanguardDefinition().DefaultTeamId,
                    out attacker,
                    out target);
                CharacterTestCharacterActionRuntime runtime = CharacterTestCharacterActionConfigSource.Load(
                    new CharacterTestCharacterActionConfigSourceOptions(new[] { directory }));
                CharacterTestCharacterPhysicsRuntime physicsRuntime = CreateFrameworkPhysicsRuntime(includeWall: false);
                var motion = new CharacterTestCharacterMotionSystem();
                motion.SetMovementProfile(CreateMovementRuntime());
                motion.SetPhysicsRuntime(physicsRuntime);
                var combat = new CharacterTestCharacterCombatTraceSystem();
                combat.SetPhysicsRuntime(physicsRuntime);

                HitResolveResult result = RunLightAttackUntilCombatResult(
                    world,
                    runtime,
                    motion,
                    combat,
                    attacker,
                    maxFrames: 10);

                Assert.AreEqual(HitResolveKind.Friendly, result.Kind);
                Assert.AreEqual(0, result.Damage);
                Assert.AreEqual(100, world.GetCurrentAttribute(target, CharacterTestCharacterAttributeIds.Hp));
                Assert.AreEqual(0, target.CombatState.ScatterPressure);
            }
            finally
            {
                DeleteDirectory(directory);
            }
        }

[Test]
        public void CharacterActionRuntime_LightAttackHitTrace_PublicImpactPowerDoesNotUseLegacyProfileImpactPower()
        {
            CharacterTestCharacterEntity attacker;
            CharacterTestCharacterEntity target;
            CharacterTestCharacterRuntimeWorld world = CreateRuntimeWorldWithAttackerAndTarget(
                targetTeamId: 2,
                out attacker,
                out target);
            CharacterTestCharacterActionRuntime runtime = CharacterTestCharacterActionConfigSource.Load();
            CharacterTestCharacterPhysicsRuntime physicsRuntime = CreateFrameworkPhysicsRuntime(includeWall: false);
            var motion = new CharacterTestCharacterMotionSystem();
            motion.SetMovementProfile(CreateMovementRuntime());
            motion.SetPhysicsRuntime(physicsRuntime);
            CharacterTestCharacterCombatTraceSystem combat = CreateCombatTraceSystem();
            combat.SetPhysicsRuntime(physicsRuntime);

            HitResolveResult result = RunLightAttackUntilCombatResult(
                world,
                runtime,
                motion,
                combat,
                attacker,
                maxFrames: 40);

            Assert.AreEqual(HitResolveKind.Damage, result.Kind);
            Assert.Less(target.CombatState.Health, CharacterTestCharacterCombatStateComponent.DefaultMaxHealth);
            Assert.AreEqual(34, target.CombatState.ScatterPressure);
            Assert.AreEqual(0, combat.CohesionBreakEvents.Count);
        }

        [Test]
        public void CharacterActionRuntime_OrdinaryHitStartsReactionAndLocksActiveControl()
        {
            CharacterTestCharacterEntity attacker;
            CharacterTestCharacterEntity target;
            CharacterTestCharacterRuntimeWorld world = CreateRuntimeWorldWithAttackerAndTarget(
                targetTeamId: 2,
                out attacker,
                out target);
            CharacterTestCharacterActionRuntime runtime = CharacterTestCharacterActionConfigSource.Load(
                CharacterTestCharacterActionConfigSourceOptions.CreateLegacyFixtureFallback());
            CharacterTestCharacterPhysicsRuntime physicsRuntime = CreateFrameworkPhysicsRuntime(includeWall: false);
            var motion = new CharacterTestCharacterMotionSystem();
            motion.SetMovementProfile(CreateMovementRuntime());
            motion.SetPhysicsRuntime(physicsRuntime);
            CharacterTestCharacterCombatTraceSystem combat = CreateCombatTraceSystem();
            combat.SetPhysicsRuntime(physicsRuntime);

            HitResolveResult result = RunLightAttackUntilCombatResult(
                world,
                runtime,
                motion,
                combat,
                attacker,
                maxFrames: 40);

            Assert.AreEqual(HitResolveKind.Damage, result.Kind);
            Assert.AreEqual(PressureBand.Stable, target.CombatState.ScatterPressureBand);
            Assert.AreEqual(
                CharacterTestCharacterActionConfigSource.KatanaHitMidReactionActionId,
                target.Action.ActiveActionId);
            Assert.AreEqual(0, target.Action.LocalFrame);
            Assert.AreEqual(0, target.Action.MovementControlScaleMilli);
            Assert.AreEqual(0, target.Action.RotationControlScaleMilli);

            int beforeKnockbackX = target.Transform.XMillimeters;
            int beforeKnockbackZ = target.Transform.ZMillimeters;
            target.SetInput(new CharacterTestCharacterInputComponent(
                Vector2.left,
                jumpPressed: false,
                jumpHeld: true));
            motion.Tick(world, 1f / 60f, new RuntimeFrame(100));

            Assert.AreEqual(0, target.MovementControlScaleMilli);
            Assert.AreEqual(0, target.RotationControlScaleMilli);
            Assert.AreEqual(0, target.DesiredVelocityXMillimetersPerSecond);
            Assert.AreEqual(0, target.DesiredVelocityYMillimetersPerSecond);
            Assert.IsTrue(
                target.Transform.XMillimeters != beforeKnockbackX
                || target.Transform.ZMillimeters != beforeKnockbackZ,
                "Combat impulse should still move the target while active control is scaled to zero.");

            int postHitX = target.Transform.XMillimeters;
            int postHitY = target.Transform.YMillimeters;
            int postHitZ = target.Transform.ZMillimeters;
            for (int i = 0; i < 12; i++)
                runtime.TickEntity(target);

            Assert.AreEqual(CharacterActionId.None, target.Action.ActiveActionId);
            Assert.AreEqual(CharacterTestDeterministicUnits.ScaleFactorScale, target.Action.MovementControlScaleMilli);
            Assert.AreEqual(CharacterTestDeterministicUnits.ScaleFactorScale, target.Action.RotationControlScaleMilli);
            Assert.AreEqual(postHitX, target.Transform.XMillimeters);
            Assert.AreEqual(postHitY, target.Transform.YMillimeters);
            Assert.AreEqual(postHitZ, target.Transform.ZMillimeters);
        }

[Test]
        public void CharacterActionRuntime_UnknownIntent_ReportsStableRejectReason()
        {
            CharacterTestCharacterEntity entity = CreateRuntimeCharacter();
            CharacterTestCharacterActionRuntime runtime = CharacterTestCharacterActionConfigSource.Load();

            CharacterActionResolveResult result = runtime.ResolveIntent(
                entity,
                global::MxFramework.Tests.Game.CharacterTest.MigrationFixtureIds.Id0005,
                new RuntimeFrame(4));

            Assert.IsTrue(result.IsRejected);
            Assert.AreEqual(CharacterActionRejectReason.MissingActionBinding, result.RejectReason);
            Assert.AreEqual(CharacterActionDiagnosticCodes.MissingActionBinding, result.Diagnostics[0].Code);
        }

[Test]
        public void CharacterTestScene_BindsSceneProfileForRuntimeDefaults()
        {
            string scenePath = Path.Combine(
                Directory.GetCurrentDirectory(),
                "Assets/Scenes/CharacterTestScene.unity");
            string sceneText = File.ReadAllText(scenePath);

            StringAssert.Contains("_profile:", sceneText);
            StringAssert.DoesNotContain("_fireballVfxPrefab:", sceneText);
            string[] removedNetworkFields =
            {
                "_networkMode:",
                "_enableNetworkLoopback:",
                "_unityTransportBindAddress:",
                "_unityTransportRemoteAddress:",
                "_unityTransportPort:",
                "_unityTransportServerPeerId:",
                "_unityTransportClientPeerId:",
                "_networkRoomId:",
                "_networkRoomName:",
                "_networkRoomMaxMembers:",
                "_networkRoomMinReadyMembers:",
                "_networkRoomClientDisplayName:",
                "_networkRoomClientAutoReady:",
                "_networkRoomServerAutoStartWhenReady:"
            };
            foreach (string field in removedNetworkFields)
                StringAssert.DoesNotContain(field, sceneText);

            CharacterTestSceneProfile profile =
                AssetDatabase.LoadAssetAtPath<CharacterTestSceneProfile>(
                    CharacterTestSceneProfile.DefaultAssetPath);
            Assert.IsNotNull(profile, "CharacterTestSceneProfile asset must exist.");
            Assert.AreEqual(
                "Assets/VFX/MxFramework/Samples/Fireball/Prefabs/Fireball.prefab",
                AssetDatabase.GetAssetPath(profile.FireballVfxPrefab));
            Assert.AreEqual(
                "Assets/VFX/MxFramework/Samples/BeamLance/Prefabs/BeamLance_Beam.prefab",
                AssetDatabase.GetAssetPath(profile.BeamLanceBeamVfxPrefab));
            Assert.AreEqual(
                "Assets/VFX/MxFramework/Samples/BeamLance/Prefabs/BeamLance_Impact.prefab",
                AssetDatabase.GetAssetPath(profile.BeamLanceImpactVfxPrefab));
            Assert.AreEqual(
                "Assets/VFX/MxFramework/Samples/BeamLance/Prefabs/BeamLance_Charge.prefab",
                AssetDatabase.GetAssetPath(profile.BeamLanceChargeVfxPrefab));
            Assert.AreEqual(
                "Assets/VFX/MxFramework/Samples/LaserLance/Prefabs/LaserLance_Laser.prefab",
                AssetDatabase.GetAssetPath(profile.LaserLancePathVfxPrefab));
            Assert.AreEqual(
                "Assets/VFX/MxFramework/Samples/LaserLance/Prefabs/LaserLance_Impact.prefab",
                AssetDatabase.GetAssetPath(profile.LaserLanceImpactVfxPrefab));
            Assert.AreEqual(
                "Assets/VFX/MxFramework/Samples/LaserLance/Prefabs/LaserLance_Charge.prefab",
                AssetDatabase.GetAssetPath(profile.LaserLanceChargeVfxPrefab));
            Assert.AreEqual(
                "Assets/VFX/MxFramework/Samples/Tornado/Prefabs/Tornado_Flight.prefab",
                AssetDatabase.GetAssetPath(profile.TornadoFlightVfxPrefab));
            Assert.AreEqual(
                "Assets/VFX/MxFramework/Samples/Tornado/Prefabs/Tornado_Charge.prefab",
                AssetDatabase.GetAssetPath(profile.TornadoChargeVfxPrefab));
            Assert.AreEqual(
                "Assets/VFX/MxFramework/Samples/Tornado/Prefabs/Tornado_Impact.prefab",
                AssetDatabase.GetAssetPath(profile.TornadoImpactVfxPrefab));
            Assert.AreEqual(
                "Assets/VFX/MxFramework/Samples/Tornado/Prefabs/Tornado_Tick.prefab",
                AssetDatabase.GetAssetPath(profile.TornadoTickVfxPrefab));

            string profilePath = Path.Combine(
                Directory.GetCurrentDirectory(),
                CharacterTestSceneProfile.DefaultAssetPath);
            string profileText = File.ReadAllText(profilePath);
            foreach (string field in removedNetworkFields)
                StringAssert.DoesNotContain(field, profileText);
            string[] removedUiFields =
            {
                "_enableDebugUi:",
                "_enableSkillPanel:",
                "_enableFairyGuiCombatHud:",
                "_enableFairyGuiFloatingCombatText:",
                "_enableFairyGuiStoryUi:",
                "_enableFairyGuiMainMenu:",
                "_showMainMenuOnStart:",
                "_pauseRuntimeWhenMainMenuOpen:",
                "_toggleMainMenuWithPauseInput:",
                "_enableFairyGuiNetworkPanel:",
                "_enableFairyGuiSettingsPanel:",
                "_autoOpenFairyGuiNetworkPanel:",
                "_debugPanelSettings:",
                "_skillPanelSettings:",
                "_debugUiInitialVisibility:",
                "_debugUiShowOnStart:",
                "_debugUiInitialTabIndex:"
            };
            foreach (string field in removedUiFields)
            {
                StringAssert.DoesNotContain(field, sceneText);
                StringAssert.DoesNotContain(field, profileText);
            }
            StringAssert.Contains("_networkLoopbackLatencyFrames:", profileText);
            StringAssert.Contains("_networkInputDelayFrames:", profileText);
            StringAssert.Contains("_networkSnapshotIntervalFrames:", profileText);
            StringAssert.Contains("_networkHeartbeatIntervalFrames:", profileText);
            StringAssert.Contains("_unityTransportMaxConnections:", profileText);
            StringAssert.Contains("_unityTransportReliableWindowSize:", profileText);
        }
}
}
