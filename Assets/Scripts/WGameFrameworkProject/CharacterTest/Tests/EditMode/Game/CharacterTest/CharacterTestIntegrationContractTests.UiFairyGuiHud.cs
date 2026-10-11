using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using MxFramework.Audio;
using MxFramework.Camera;
using MxFramework.Camera.Unity;
using MxFramework.CharacterAction;
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
        public void FairyGuiCombatHud_RuntimeContractKeepsViewAliveUntilSceneRelease()
        {
            MxUiViewContract contract = CharacterTestFairyGuiCombatHudHost.CreateRuntimeViewContract();

            Assert.IsTrue(contract.Descriptor.KeepAlive);
            Assert.IsTrue(contract.Descriptor.CloseOnSceneChange);
            Assert.IsFalse(contract.Descriptor.Modal);
            Assert.AreEqual(MxUiLayer.Hud, contract.Descriptor.Layer);
        }

[Test]
        public void FairyGuiCombatHud_BuildsLocalPlayerStatusFromCharacterEntities()
        {
            using var slice = new GameSlice(
                NullRuntimeLogger.Instance,
                CharacterTestStoryFixture.CreateBootstrapContent(),
                resources: GlobalResourceRuntimeServices.Create(new GlobalResourceRuntimeOptions
                {
                    AllowMissingPreloadGroupInEditor = true
                }),
                baseResourcePreloadGroupId: string.Empty);
            CharacterTestCharacterEntity localPlayer = SpawnLocalPlayer(slice);

            Assert.IsTrue(CharacterTestFairyGuiCombatHudViewModelBuilder.TryBuild(
                slice,
                out CharacterTestFairyGuiCombatHudViewModel viewModel));
            Assert.IsTrue(viewModel.HasLocalPlayer);
            Assert.AreEqual(localPlayer.Id.Value, viewModel.PlayerEntityId);
            Assert.IsNotEmpty(viewModel.PlayerName);
            Assert.AreEqual(localPlayer.CombatState.Health, viewModel.PlayerHealth);
            Assert.AreEqual(localPlayer.CombatState.MaxHealth, viewModel.PlayerMaxHealth);
            Assert.AreEqual(1f, viewModel.PlayerHealthRatio, 0.0001f);
            Assert.AreEqual(localPlayer.CombatState.ScatterPressure, viewModel.PlayerScatterPressure);
            StringAssert.Contains("Action:", viewModel.PlayerActionText);
            Assert.IsFalse(viewModel.FeedbackVisible);
            Assert.AreEqual(string.Empty, viewModel.FeedbackText);
            Assert.AreEqual(CharacterTestAbilityHotbarRuntime.SlotCount, viewModel.AbilitySlots.Count);
            Assert.AreEqual("1", viewModel.AbilitySlots[0].KeyText);
            Assert.AreEqual("Fireball", viewModel.AbilitySlots[0].AbilityText);
            Assert.AreEqual("Flame Lance", viewModel.AbilitySlots[2].AbilityText);
            Assert.AreEqual("Tornado", viewModel.AbilitySlots[6].AbilityText);
            Assert.AreEqual("Guard Aura", viewModel.AbilitySlots[7].AbilityText);
            Assert.IsTrue(viewModel.AbilitySlots[0].IsBound);
            Assert.IsTrue(viewModel.AbilitySlots[6].IsBound);
            Assert.IsTrue(viewModel.AbilitySlots[7].IsBound);
        }

[Test]
        public void FairyGuiCombatHud_NoLocalPlayer_ReturnsEmptyState()
        {
            using var slice = new GameSlice(
                NullRuntimeLogger.Instance,
                CharacterTestStoryFixture.CreateBootstrapContent(),
                resources: GlobalResourceRuntimeServices.Create(new GlobalResourceRuntimeOptions
                {
                    AllowMissingPreloadGroupInEditor = true
                }),
                baseResourcePreloadGroupId: string.Empty);

            CharacterTestFairyGuiCombatHudViewModel viewModel =
                CharacterTestFairyGuiCombatHudViewModelBuilder.Build(slice);

            Assert.IsFalse(viewModel.HasLocalPlayer);
            Assert.IsFalse(viewModel.TargetVisible);
            Assert.IsTrue(viewModel.FeedbackVisible);
            StringAssert.Contains("Waiting for local player", viewModel.FeedbackText);
        }

[Test]
        public void FairyGuiCombatHud_TargetLockControlsTargetPlateVisibility()
        {
            using (GameSlice unlocked = new GameSlice(
                NullRuntimeLogger.Instance,
                CharacterTestStoryFixture.CreateBootstrapContent(),
                resources: GlobalResourceRuntimeServices.Create(new GlobalResourceRuntimeOptions
                {
                    AllowMissingPreloadGroupInEditor = true
                }),
                baseResourcePreloadGroupId: string.Empty))
            {
                SpawnLocalPlayer(unlocked);

                CharacterTestFairyGuiCombatHudViewModel unlockedViewModel =
                    CharacterTestFairyGuiCombatHudViewModelBuilder.Build(unlocked);

                Assert.IsTrue(unlockedViewModel.HasLocalPlayer);
                Assert.IsFalse(unlockedViewModel.TargetVisible);
                Assert.IsFalse(unlockedViewModel.TargetLocked);
                Assert.IsFalse(unlockedViewModel.FeedbackVisible);
            }

            using (GameSlice locked = CreateSliceWithLockedTarget(out CharacterTestCharacterEntity primaryTarget))
            {
                CharacterTestFairyGuiCombatHudViewModel lockedViewModel =
                    CharacterTestFairyGuiCombatHudViewModelBuilder.Build(locked);

                Assert.IsTrue(lockedViewModel.HasLocalPlayer);
                Assert.AreEqual(primaryTarget.Id.Value, lockedViewModel.PlayerEntityId);
                Assert.IsTrue(lockedViewModel.TargetVisible);
                Assert.IsTrue(lockedViewModel.TargetLocked);
                Assert.AreEqual(locked.LockedTargetEntityId, lockedViewModel.TargetEntityId);
                Assert.AreEqual("LOCK", lockedViewModel.TargetStatusText);
                Assert.IsFalse(
                    lockedViewModel.FeedbackVisible,
                    "Target-lock feedback is owned by the shared Guidance surface, not CombatHUD.");
                Assert.IsEmpty(lockedViewModel.FeedbackText);
            }
        }

        [Test]
        public void PlayerGuidanceCatalog_RegistersOnePackageAcrossHudAndToastLayers()
        {
            MxFairyGuiRuntimeCatalog catalog = MxFairyGuiGuidancePresenter.CreateCatalog();

            Assert.AreEqual(2, catalog.ViewCount);
            Assert.AreEqual(1, catalog.PackageCount);
            Assert.IsTrue(catalog.TryGet(
                MxPlayerGuidanceFairyGuiIds.CardViewId,
                out IMxFairyGuiRuntimeViewRegistration card));
            Assert.IsTrue(catalog.TryGet(
                MxPlayerGuidanceFairyGuiIds.NoticeViewId,
                out IMxFairyGuiRuntimeViewRegistration notices));
            Assert.AreEqual(MxUiLayer.Hud, card.Contract.Descriptor.Layer);
            Assert.AreEqual(MxUiLayer.Toast, notices.Contract.Descriptor.Layer);
            Assert.IsFalse(card.Contract.Descriptor.Modal);
            Assert.IsFalse(notices.Contract.Descriptor.Modal);
            Assert.AreEqual(card.Package.PackageId, notices.Package.PackageId);
        }

        [Test]
        public void GuidanceSequence_TargetLockObserverPrimesStaleHistoryAndMapsOnlyNewFacts()
        {
            var events = new List<CharacterTestRuntimeEvent>
            {
                new CharacterTestRuntimeEvent(
                    new RuntimeFrame(10),
                    CharacterTestRuntimeEventType.TargetLockChanged,
                    CharacterTestRuntimeCommandIds.SetTargetLock,
                    sourceId: 1,
                    targetId: 2,
                    entityId: 2)
            };
            CharacterTestGuidanceSequenceCoordinator coordinator =
                CreateGuidanceSequence(out CharacterTestGuidanceSnapshotMapper mapper);
            var prompts = new MxUiLocalizedTextRequest[6];
            prompts[3] = new MxUiLocalizedTextRequest(
                new MxUiTextKey("test.target-lock.binding"),
                "按 L 锁定一个可用目标");
            mapper.SetStepActionPrompts(prompts);
            CharacterTestGuidanceObservation observation = GuidanceObservation(grounded: true);
            CharacterTestGuidanceObservationContext context =
                GuidanceContext(events, Array.Empty<DamageTelemetryRow>(), in observation);

            coordinator.Prime(in context);
            Assert.IsTrue(coordinator.TrySelectStableId("target-lock", in context));
            MxUiGuidanceCardSnapshot initial = mapper.CreateCurrentCard(
                coordinator,
                in observation);
            Assert.AreEqual(CharacterTestGuidanceSnapshotMapper.ScopeId, initial.ScopeId);
            Assert.AreEqual("测试 4/6：锁定目标", initial.Title.FallbackText);
            Assert.AreEqual("按 L 锁定一个可用目标", initial.ActionLine.Body.FallbackText);
            Assert.AreEqual("等待锁定目标", initial.StatusLine.Body.FallbackText);
            Assert.AreEqual(MxUiGuidanceTone.Neutral, initial.StatusLine.Tone);
            Assert.IsFalse(
                coordinator.TryObserve(in context, out _),
                "Primed history must not replay on UI startup.");

            events.Add(new CharacterTestRuntimeEvent(
                new RuntimeFrame(11),
                CharacterTestRuntimeEventType.TargetLockRejected,
                CharacterTestRuntimeCommandIds.SetTargetLock,
                sourceId: 1,
                targetId: 3));
            context = GuidanceContext(events, Array.Empty<DamageTelemetryRow>(), in observation);
            Assert.IsTrue(coordinator.TryObserve(in context, out CharacterTestGuidanceCompletionOutcome rejectedOutcome));
            Assert.IsTrue(mapper.TryMapOutcome(
                coordinator,
                in observation,
                in rejectedOutcome,
                out MxUiGuidanceCardSnapshot rejectedCard,
                out MxUiGuidanceNotice rejected));
            Assert.AreEqual(CharacterTestGuidanceSnapshotMapper.ScopeId, rejected.ScopeId);
            Assert.AreEqual("未完成：当前目标不可锁定，请更换目标重试", rejectedCard.StatusLine.Body.FallbackText);
            Assert.AreEqual(MxUiGuidanceTone.Warning, rejectedCard.StatusLine.Tone);
            Assert.Greater(rejectedCard.Revision, initial.Revision);
            Assert.AreEqual(MxUiGuidanceTone.Warning, rejected.Tone);
            Assert.AreEqual(MxUiGuidanceGlyph.Warning, rejected.Glyph);
            Assert.AreEqual("当前无法锁定目标", rejected.Text.FallbackText);
            Assert.IsFalse(
                coordinator.TryObserve(in context, out _),
                "The same runtime event must be projected once.");

            events.Add(new CharacterTestRuntimeEvent(
                new RuntimeFrame(12),
                CharacterTestRuntimeEventType.TargetLockChanged,
                CharacterTestRuntimeCommandIds.SetTargetLock,
                sourceId: 1,
                targetId: 4,
                entityId: 4));
            observation = GuidanceObservation(grounded: true, targetLocked: true);
            context = GuidanceContext(events, Array.Empty<DamageTelemetryRow>(), in observation);
            Assert.IsTrue(coordinator.TryObserve(in context, out CharacterTestGuidanceCompletionOutcome completedOutcome));
            Assert.IsTrue(mapper.TryMapOutcome(
                coordinator,
                in observation,
                in completedOutcome,
                out MxUiGuidanceCardSnapshot completedCard,
                out MxUiGuidanceNotice locked));
            Assert.AreEqual("已完成：目标锁定已确认，即将继续", completedCard.StatusLine.Body.FallbackText);
            Assert.AreEqual(MxUiGuidanceTone.Success, completedCard.StatusLine.Tone);
            Assert.Greater(completedCard.Revision, rejectedCard.Revision);
            Assert.AreEqual(MxUiGuidanceTone.Success, locked.Tone);
            Assert.AreEqual(MxUiGuidanceGlyph.Success, locked.Glyph);
            Assert.AreEqual("目标锁定已确认", locked.Text.FallbackText);

            Assert.IsFalse(coordinator.TryAdvance(1.99f, in context));
            Assert.IsTrue(coordinator.TryAdvance(0.01f, in context));
            MxUiGuidanceCardSnapshot unlockCard = mapper.CreateCurrentCard(
                coordinator,
                in observation);
            Assert.AreEqual("测试 5/6：取消锁定", unlockCard.Title.FallbackText);
            Assert.AreEqual("等待取消目标锁定", unlockCard.StatusLine.Body.FallbackText);

            events.Add(new CharacterTestRuntimeEvent(
                new RuntimeFrame(13),
                CharacterTestRuntimeEventType.TargetLockChanged,
                CharacterTestRuntimeCommandIds.SetTargetLock,
                sourceId: 1,
                targetId: 0,
                entityId: 0));
            observation = GuidanceObservation(grounded: true, targetLocked: false);
            context = GuidanceContext(events, Array.Empty<DamageTelemetryRow>(), in observation);
            Assert.IsTrue(coordinator.TryObserve(in context, out CharacterTestGuidanceCompletionOutcome clearedOutcome));
            Assert.IsTrue(mapper.TryMapOutcome(
                coordinator,
                in observation,
                in clearedOutcome,
                out MxUiGuidanceCardSnapshot clearedCard,
                out MxUiGuidanceNotice cleared));
            Assert.AreEqual("已完成：目标锁定已取消，即将继续", clearedCard.StatusLine.Body.FallbackText);
            Assert.AreEqual(MxUiGuidanceTone.Success, clearedCard.StatusLine.Tone);
            Assert.Greater(clearedCard.Revision, unlockCard.Revision);
            Assert.AreEqual(MxUiGuidanceTone.Success, cleared.Tone);
            Assert.AreEqual("目标锁定取消已确认", cleared.Text.FallbackText);

            MxUiGuidanceCardSnapshot restored = mapper.CreateCurrentCard(
                coordinator,
                in observation);
            Assert.AreEqual("已完成：目标锁定已取消，即将继续", restored.StatusLine.Body.FallbackText);
            Assert.AreEqual(MxUiGuidanceTone.Success, restored.StatusLine.Tone);
            Assert.Greater(restored.Revision, clearedCard.Revision);
        }

        [Test]
        public void GuidanceSequence_ManualSelectionDoesNotReplayStaleCompletion()
        {
            var events = new List<CharacterTestRuntimeEvent>
            {
                new CharacterTestRuntimeEvent(
                    new RuntimeFrame(20),
                    CharacterTestRuntimeEventType.TargetLockChanged,
                    CharacterTestRuntimeCommandIds.SetTargetLock,
                    sourceId: 1,
                    targetId: 7,
                    entityId: 7)
            };
            CharacterTestGuidanceSequenceCoordinator coordinator =
                CreateGuidanceSequence(out CharacterTestGuidanceSnapshotMapper mapper);
            CharacterTestGuidanceObservation observation = GuidanceObservation(
                grounded: true,
                targetLocked: true);
            CharacterTestGuidanceObservationContext context =
                GuidanceContext(events, Array.Empty<DamageTelemetryRow>(), in observation);
            coordinator.Prime(in context);
            Assert.IsTrue(coordinator.TrySelectStableId("target-unlock", in context));

            MxUiGuidanceCardSnapshot selected = mapper.CreateCurrentCard(coordinator, in observation);
            Assert.AreEqual("测试 5/6：取消锁定", selected.Title.FallbackText);
            Assert.IsFalse(coordinator.TryObserve(in context, out _), "Manual selection must prime stale history.");

            events.Add(new CharacterTestRuntimeEvent(
                new RuntimeFrame(21),
                CharacterTestRuntimeEventType.TargetLockChanged,
                CharacterTestRuntimeCommandIds.SetTargetLock,
                sourceId: 1,
                targetId: 0,
                entityId: 0));
            observation = GuidanceObservation(grounded: true, targetLocked: false);
            context = GuidanceContext(events, Array.Empty<DamageTelemetryRow>(), in observation);
            Assert.IsTrue(coordinator.TryObserve(in context, out CharacterTestGuidanceCompletionOutcome outcome));
            Assert.IsTrue(mapper.TryMapOutcome(
                coordinator,
                in observation,
                in outcome,
                out MxUiGuidanceCardSnapshot completed,
                out _));
            Assert.AreEqual("已完成：目标锁定已取消，即将继续", completed.StatusLine.Body.FallbackText);
            Assert.IsTrue(coordinator.CreateSelectionSnapshot().Completed);
        }

        [Test]
        public void GuidanceSequence_ManualPreviousAndNextSelectionIsBounded()
        {
            var events = new List<CharacterTestRuntimeEvent>();
            CharacterTestGuidanceSequenceCoordinator coordinator =
                CreateGuidanceSequence(out CharacterTestGuidanceSnapshotMapper mapper);
            CharacterTestGuidanceObservation observation = GuidanceObservation(grounded: true);
            CharacterTestGuidanceObservationContext context =
                GuidanceContext(events, Array.Empty<DamageTelemetryRow>(), in observation);
            coordinator.Prime(in context);
            Assert.IsTrue(coordinator.TrySelectStableId("target-lock", in context));

            Assert.AreEqual("target-lock", coordinator.CreateSelectionSnapshot().SelectedStableId);
            Assert.IsTrue(coordinator.TrySelectPrevious(in context));
            Assert.AreEqual("camera-mode-switch", coordinator.CurrentStep.StableId);
            Assert.IsTrue(coordinator.TrySelectNext(in context));
            Assert.IsTrue(coordinator.TrySelectNext(in context));
            MxUiGuidanceCardSnapshot next = mapper.CreateCurrentCard(coordinator, in observation);
            Assert.AreEqual("测试 5/6：取消锁定", next.Title.FallbackText);
            Assert.IsTrue(coordinator.TrySelectPrevious(in context));
            MxUiGuidanceCardSnapshot previous = mapper.CreateCurrentCard(coordinator, in observation);
            Assert.AreEqual("测试 4/6：锁定目标", previous.Title.FallbackText);
        }

        [Test]
        public void GuidanceSequence_BasicMovementRequiresDistanceThenActualStop()
        {
            var events = new List<CharacterTestRuntimeEvent>();
            var damageRows = new List<DamageTelemetryRow>();
            var origin = GuidanceObservation(x: 0, z: 0, speed: 0, grounded: true);
            CharacterTestGuidanceSequenceCoordinator coordinator =
                CreateGuidanceSequence(out CharacterTestGuidanceSnapshotMapper mapper);
            CharacterTestGuidanceObservationContext context =
                GuidanceContext(events, damageRows, in origin);
            coordinator.Prime(in context);

            Assert.AreEqual(
                "测试 1/6：基础移动与停步",
                mapper.CreateCurrentCard(coordinator, in origin).Title.FallbackText);
            var moving = GuidanceObservation(x: 2100, z: 0, speed: 1000, grounded: true);
            context = GuidanceContext(events, damageRows, in moving);
            Assert.IsFalse(coordinator.TryObserve(in context, out _));

            var stopped = GuidanceObservation(x: 2100, z: 0, speed: 0, grounded: true);
            context = GuidanceContext(events, damageRows, in stopped);
            Assert.IsTrue(coordinator.TryObserve(in context, out CharacterTestGuidanceCompletionOutcome outcome));
            Assert.IsTrue(mapper.TryMapOutcome(
                coordinator,
                in stopped,
                in outcome,
                out MxUiGuidanceCardSnapshot completed,
                out _));
            Assert.AreEqual("已完成：移动与停步已确认，即将继续", completed.StatusLine.Body.FallbackText);
        }

        [Test]
        public void GuidanceSequence_JumpRequiresAirborneThenGrounded()
        {
            var events = new List<CharacterTestRuntimeEvent>();
            var damageRows = new List<DamageTelemetryRow>();
            var grounded = GuidanceObservation(grounded: true);
            CharacterTestGuidanceSequenceCoordinator coordinator =
                CreateGuidanceSequence(out _);
            CharacterTestGuidanceObservationContext context =
                GuidanceContext(events, damageRows, in grounded);
            coordinator.Prime(in context);
            Assert.IsTrue(coordinator.TrySelectStableId("jump-and-land", in context));

            Assert.IsFalse(coordinator.TryObserve(in context, out _));
            var airborne = GuidanceObservation(grounded: false);
            context = GuidanceContext(events, damageRows, in airborne);
            Assert.IsFalse(coordinator.TryObserve(in context, out _));
            context = GuidanceContext(events, damageRows, in grounded);
            Assert.IsTrue(coordinator.TryObserve(in context, out _));
        }

        [Test]
        public void GuidanceSequence_CameraModeIgnoresStaleEventAndAcceptsNewChange()
        {
            var events = new List<CharacterTestRuntimeEvent>
            {
                new CharacterTestRuntimeEvent(
                    new RuntimeFrame(30),
                    CharacterTestRuntimeEventType.CameraModeChanged,
                    CharacterTestRuntimeCommandIds.SetCameraMode,
                    sourceId: 1,
                    targetId: 0)
            };
            var damageRows = new List<DamageTelemetryRow>();
            var observation = GuidanceObservation(grounded: true);
            CharacterTestGuidanceSequenceCoordinator coordinator =
                CreateGuidanceSequence(out _);
            CharacterTestGuidanceObservationContext context =
                GuidanceContext(events, damageRows, in observation);
            coordinator.Prime(in context);
            Assert.IsTrue(coordinator.TrySelectStableId("camera-mode-switch", in context));

            Assert.IsFalse(coordinator.TryObserve(in context, out _));
            events.Add(new CharacterTestRuntimeEvent(
                new RuntimeFrame(30),
                CharacterTestRuntimeEventType.CameraModeChanged,
                CharacterTestRuntimeCommandIds.SetCameraMode,
                sourceId: 1,
                targetId: 0,
                payload0: 1));
            context = GuidanceContext(events, damageRows, in observation);
            Assert.IsTrue(coordinator.TryObserve(in context, out _));
        }

        [Test]
        public void GuidanceSequence_BasicAttackRequiresNewLocalPlayerDamage()
        {
            var events = new List<CharacterTestRuntimeEvent>();
            var damageRows = new List<DamageTelemetryRow> { GuidanceDamageRow(40, sourceEntityId: 7) };
            var observation = GuidanceObservation(localPlayerEntityId: 7, grounded: true);
            CharacterTestGuidanceSequenceCoordinator coordinator =
                CreateGuidanceSequence(out CharacterTestGuidanceSnapshotMapper mapper);
            CharacterTestGuidanceObservationContext context =
                GuidanceContext(events, damageRows, in observation);
            coordinator.Prime(in context);
            Assert.IsTrue(coordinator.TrySelectStableId("basic-attack-hit", in context));

            Assert.IsFalse(coordinator.TryObserve(in context, out _));
            damageRows.Add(GuidanceDamageRow(41, sourceEntityId: 8));
            context = GuidanceContext(events, damageRows, in observation);
            Assert.IsFalse(coordinator.TryObserve(in context, out _));
            damageRows.Add(GuidanceDamageRow(
                40,
                sourceEntityId: 7,
                localFrame: 5,
                hpBefore: 94,
                hpAfter: 88));
            context = GuidanceContext(events, damageRows, in observation);
            Assert.IsTrue(coordinator.TryObserve(in context, out CharacterTestGuidanceCompletionOutcome outcome));
            Assert.IsTrue(mapper.TryMapOutcome(
                coordinator,
                in observation,
                in outcome,
                out MxUiGuidanceCardSnapshot completed,
                out MxUiGuidanceNotice notice));
            Assert.AreEqual("已完成：普通攻击造成了真实伤害，本轮测试完成", completed.StatusLine.Body.FallbackText);
            Assert.That(notice.Text.FallbackText, Is.EqualTo("DMG 6 · HP 94 → 88"));
            Assert.That(
                notice.Text.Key.Value,
                Is.EqualTo("ui.character_test.guidance.basic_attack_hit.damage.resolved"));
        }

        [Test]
        public void GuidanceSequence_PlayerNavigationTraversesAuthoredStepsAndIsBounded()
        {
            var events = new List<CharacterTestRuntimeEvent>();
            var damageRows = new List<DamageTelemetryRow>();
            var observation = GuidanceObservation(grounded: true);
            CharacterTestGuidanceSequenceCoordinator coordinator =
                CreateGuidanceSequence(out _);
            CharacterTestGuidanceObservationContext context =
                GuidanceContext(events, damageRows, in observation);
            coordinator.Prime(in context);

            Assert.IsFalse(coordinator.TrySelectPrevious(in context));
            for (int i = 1; i < 6; i++)
                Assert.IsTrue(coordinator.TrySelectNext(in context));
            Assert.AreEqual("basic-attack-hit", coordinator.CreateSelectionSnapshot().SelectedStableId);
            Assert.IsFalse(coordinator.TrySelectNext(in context));
        }

        [Test]
        public void GuidanceSequence_ZeroDelayAdvancesOnNextTickAndFinalStepRemainsCompleted()
        {
            CharacterTestGuidanceAuthoringDocument document =
                CharacterTestGuidanceAuthoringAdapter.CreateSixStepReferenceDocument();
            document.Steps[0].AutoAdvanceSeconds = 0d;
            Assert.IsTrue(CharacterTestGuidanceCompiler.TryCompile(
                document,
                out CharacterTestGuidancePlan plan,
                out _));
            var coordinator = new CharacterTestGuidanceSequenceCoordinator(plan);
            CharacterTestGuidanceObservation origin = GuidanceObservation(grounded: true);
            var events = new List<CharacterTestRuntimeEvent>();
            var damageRows = new List<DamageTelemetryRow>();
            CharacterTestGuidanceObservationContext context =
                GuidanceContext(events, damageRows, in origin);
            coordinator.Prime(in context);

            CharacterTestGuidanceObservation stopped = GuidanceObservation(
                x: 2100,
                speed: 0,
                grounded: true);
            context = GuidanceContext(events, damageRows, in stopped);
            Assert.IsTrue(coordinator.TryObserve(in context, out _));
            Assert.AreEqual("basic-movement", coordinator.CurrentStep.StableId);
            Assert.IsTrue(coordinator.TryAdvance(0f, in context));
            Assert.AreEqual("jump-and-land", coordinator.CurrentStep.StableId);

            Assert.IsTrue(coordinator.TrySelectStableId("basic-attack-hit", in context));
            damageRows.Add(GuidanceDamageRow(100, sourceEntityId: 7));
            context = GuidanceContext(events, damageRows, in stopped);
            Assert.IsTrue(coordinator.TryObserve(in context, out _));
            Assert.IsTrue(coordinator.Completed);
            Assert.IsFalse(coordinator.TryAdvance(100f, in context));
            Assert.AreEqual("basic-attack-hit", coordinator.CurrentStep.StableId);
        }

        private static CharacterTestGuidanceSequenceCoordinator CreateGuidanceSequence(
            out CharacterTestGuidanceSnapshotMapper mapper)
        {
            Assert.IsTrue(CharacterTestGuidanceCompiler.TryCompile(
                CharacterTestGuidanceAuthoringAdapter.CreateSixStepReferenceDocument(),
                out CharacterTestGuidancePlan plan,
                out CharacterTestGuidanceAuthoringReport report),
                "Errors=" + report.ErrorCount);
            mapper = new CharacterTestGuidanceSnapshotMapper(plan);
            var titles = new MxUiLocalizedTextRequest[plan.StepCount];
            for (int i = 0; i < titles.Length; i++)
            {
                titles[i] = new MxUiLocalizedTextRequest(
                    new MxUiTextKey(plan.Steps[i].Content.Title.Key + ".test-resolved"),
                    "测试 " + (i + 1) + "/" + plan.StepCount + "："
                        + plan.Steps[i].Content.Title.Fallback);
            }
            mapper.SetStepTitlePrompts(titles);
            return new CharacterTestGuidanceSequenceCoordinator(plan);
        }

        private static CharacterTestGuidanceObservationContext GuidanceContext(
            IReadOnlyList<CharacterTestRuntimeEvent> events,
            IReadOnlyList<DamageTelemetryRow> damageRows,
            in CharacterTestGuidanceObservation observation)
        {
            return new CharacterTestGuidanceObservationContext(events, damageRows, in observation);
        }

        private static CharacterTestGuidanceObservation GuidanceObservation(
            int x = 0,
            int z = 0,
            int speed = 0,
            bool grounded = false,
            int localPlayerEntityId = 7,
            bool targetLocked = false)
        {
            return new CharacterTestGuidanceObservation(
                true,
                localPlayerEntityId,
                x,
                z,
                speed,
                grounded,
                CharacterTestCameraMode.ThirdPersonControlled,
                targetLocked);
        }

        private static DamageTelemetryRow GuidanceDamageRow(
            long frame,
            int sourceEntityId,
            int localFrame = 4,
            int hpBefore = 100,
            int hpAfter = 94)
        {
            return new DamageTelemetryRow(
                frame,
                DamageProfileSourceKind.WeaponTrace,
                new MxFramework.CharacterAction.CharacterActionId(301),
                actionInstanceId: (int)frame,
                stableEventId: global::MxFramework.Tests.Game.CharacterTest.MigrationFixtureIds.Id0033,
                damageProfileId: global::MxFramework.Tests.Game.CharacterTest.MigrationFixtureIds.Id0021,
                damageTypeId: CharacterTestAuthoredContentIds.D409_Id1,
                equipmentSlotId: CharacterTestAuthoredContentIds.D405_Id1,
                weaponId: global::MxFramework.Tests.Game.CharacterTest.MigrationFixtureIds.Id0088,
                sourceEntityId,
                targetEntityId: 9,
                hitZoneId: MxFramework.Game.CharacterTest.CharacterTestAuthoredContentIds.D400_Id55,
                bodyPartId: MxFramework.Game.CharacterTest.CharacterTestAuthoredContentIds.D400_Id10,
                localFrame,
                incomingDamage: 6,
                finalDamage: 6,
                resultKind: HitResolveKind.Damage,
                hpBefore,
                hpAfter);
        }

        [Test]
        public void FairyGuiCombatHud_ControlIntentAppliedDoesNotShowFeedback()
        {
            using var slice = new GameSlice(
                NullRuntimeLogger.Instance,
                CharacterTestStoryFixture.CreateBootstrapContent(),
                resources: GlobalResourceRuntimeServices.Create(new GlobalResourceRuntimeOptions
                {
                    AllowMissingPreloadGroupInEditor = true
                }),
                baseResourcePreloadGroupId: string.Empty);
            CharacterTestCharacterEntity localPlayer = SpawnLocalPlayer(slice);

            RuntimeCommandValidationResult commandResult = slice.EnqueueRuntimeCommand(
                CharacterTestRuntimeCommandFactory.SetControlIntent(
                    slice.CurrentFrame,
                    CharacterTestRuntimeCommandSources.PlayerInput,
                    localPlayer.Id.Value,
                    Vector2.zero,
                    Vector2.zero,
                    jumpPressed: false,
                    jumpHeld: false,
                    jumpReleased: false,
                    attackPrimaryPressed: false,
                    new MxFramework.Runtime.RuntimeTraceIdentity(global::MxFramework.Tests.Game.CharacterTest.MigrationFixtureIds.Trace0024)));
            Assert.IsTrue(commandResult.Success, commandResult.Error.Message);
            slice.Tick(0d);

            CharacterTestFairyGuiCombatHudViewModel viewModel =
                CharacterTestFairyGuiCombatHudViewModelBuilder.Build(slice);

            Assert.IsFalse(viewModel.FeedbackVisible);
            Assert.AreEqual(string.Empty, viewModel.FeedbackText);
        }

[Test]
        public void FairyGuiCombatHud_ActionRejectReasonShowsFeedback()
        {
            using var slice = new GameSlice(
                NullRuntimeLogger.Instance,
                CharacterTestStoryFixture.CreateBootstrapContent(),
                resources: GlobalResourceRuntimeServices.Create(new GlobalResourceRuntimeOptions
                {
                    AllowMissingPreloadGroupInEditor = true
                }),
                baseResourcePreloadGroupId: string.Empty);
            CharacterTestCharacterEntity localPlayer = SpawnLocalPlayer(slice);
            localPlayer.SetAction(localPlayer.Action.WithReject(CharacterActionDiagnosticCode.ActionSlotBlocked));

            CharacterTestFairyGuiCombatHudViewModel viewModel =
                CharacterTestFairyGuiCombatHudViewModelBuilder.Build(slice);

            Assert.IsTrue(viewModel.FeedbackVisible);
            Assert.AreEqual("Action rejected: " + CharacterActionDiagnosticCodes.ActionSlotBlocked, viewModel.FeedbackText);
        }

        [Test]
        public void FairyGuiCombatHud_BufferedActionAttemptDoesNotShowTemporaryReject()
        {
            using var slice = new GameSlice(
                NullRuntimeLogger.Instance,
                CharacterTestStoryFixture.CreateBootstrapContent(),
                resources: GlobalResourceRuntimeServices.Create(new GlobalResourceRuntimeOptions
                {
                    AllowMissingPreloadGroupInEditor = true
                }),
                baseResourcePreloadGroupId: string.Empty);
            CharacterTestCharacterEntity localPlayer = SpawnLocalPlayer(slice);
            QueueGameplayIntent(
                localPlayer,
                CharacterTestGameplayIntentKind.AttackPrimary,
                slice.CurrentFrame);
            localPlayer.SetAction(
                localPlayer.Action.WithReject(CharacterActionDiagnosticCode.ActionSlotBlocked));

            CharacterTestFairyGuiCombatHudViewModel bufferedViewModel =
                CharacterTestFairyGuiCombatHudViewModelBuilder.Build(slice);

            Assert.AreEqual(1, localPlayer.PendingGameplayIntentCount);
            Assert.IsFalse(bufferedViewModel.FeedbackVisible);
            Assert.AreEqual(string.Empty, bufferedViewModel.FeedbackText);

            localPlayer.GameplayIntentBuffer.Clear();
            CharacterTestFairyGuiCombatHudViewModel rejectedViewModel =
                CharacterTestFairyGuiCombatHudViewModelBuilder.Build(slice);

            Assert.IsTrue(rejectedViewModel.FeedbackVisible);
            Assert.AreEqual(
                "Action rejected: " + CharacterActionDiagnosticCodes.ActionSlotBlocked,
                rejectedViewModel.FeedbackText);
        }

[Test]
        public void FairyGuiCombatHud_NetworkLatencyBadgeFormatsStatus()
        {
            using var slice = new GameSlice(
                NullRuntimeLogger.Instance,
                CharacterTestStoryFixture.CreateBootstrapContent(),
                resources: GlobalResourceRuntimeServices.Create(new GlobalResourceRuntimeOptions
                {
                    AllowMissingPreloadGroupInEditor = true
                }),
                baseResourcePreloadGroupId: string.Empty);
            SpawnLocalPlayer(slice);

            CharacterTestFairyGuiCombatHudViewModel offline =
                CharacterTestFairyGuiCombatHudViewModelBuilder.Build(
                    slice,
                    networkStatus: CharacterTestNetworkSessionStatus.Offline());
            Assert.IsFalse(offline.NetworkLatencyVisible);

            CharacterTestFairyGuiCombatHudViewModel connected =
                CharacterTestFairyGuiCombatHudViewModelBuilder.Build(
                    slice,
                    networkStatus: CreateHudNetworkStatus(
                        NetworkSessionStatus.Connected,
                        rttFrames: 2,
                        tickRate: 60));
            Assert.IsTrue(connected.NetworkLatencyVisible);
            Assert.AreEqual("NET 2f / 33ms", connected.NetworkLatencyText);
            Assert.AreEqual("good", connected.NetworkLatencyTone);

            CharacterTestFairyGuiCombatHudViewModel connecting =
                CharacterTestFairyGuiCombatHudViewModelBuilder.Build(
                    slice,
                    networkStatus: CreateHudNetworkStatus(NetworkSessionStatus.Connecting));
            Assert.IsTrue(connecting.NetworkLatencyVisible);
            Assert.AreEqual("NET WAIT", connecting.NetworkLatencyText);
            Assert.AreEqual("warn", connecting.NetworkLatencyTone);

            CharacterTestFairyGuiCombatHudViewModel lost =
                CharacterTestFairyGuiCombatHudViewModelBuilder.Build(
                    slice,
                    networkStatus: CreateHudNetworkStatus(
                        NetworkSessionStatus.Connected,
                        disconnectReason: "timeout"));
            Assert.IsTrue(lost.NetworkLatencyVisible);
            Assert.AreEqual("NET LOST", lost.NetworkLatencyText);
            Assert.AreEqual("bad", lost.NetworkLatencyTone);
        }

[Test]
        public void FairyGuiCombatHud_SignatureIncludesNetworkLatency()
        {
            using var slice = new GameSlice(
                NullRuntimeLogger.Instance,
                CharacterTestStoryFixture.CreateBootstrapContent(),
                resources: GlobalResourceRuntimeServices.Create(new GlobalResourceRuntimeOptions
                {
                    AllowMissingPreloadGroupInEditor = true
                }),
                baseResourcePreloadGroupId: string.Empty);
            SpawnLocalPlayer(slice);

            CharacterTestFairyGuiCombatHudViewModel lowLatency =
                CharacterTestFairyGuiCombatHudViewModelBuilder.Build(
                    slice,
                    networkStatus: CreateHudNetworkStatus(
                        NetworkSessionStatus.Connected,
                        rttFrames: 2,
                        tickRate: 60));
            CharacterTestFairyGuiCombatHudViewModel highLatency =
                CharacterTestFairyGuiCombatHudViewModelBuilder.Build(
                    slice,
                    networkStatus: CreateHudNetworkStatus(
                        NetworkSessionStatus.Connected,
                        rttFrames: 7,
                        tickRate: 60));

            Assert.AreNotEqual(
                CharacterTestFairyGuiCombatHudViewModelSignature.Create(lowLatency),
                CharacterTestFairyGuiCombatHudViewModelSignature.Create(highLatency));
            Assert.AreEqual("bad", highLatency.NetworkLatencyTone);
        }

[Test]
        public void FairyGuiCombatHud_SignatureIncludesAbilitySlots()
        {
            using var slice = new GameSlice(
                NullRuntimeLogger.Instance,
                CharacterTestStoryFixture.CreateBootstrapContent(),
                resources: GlobalResourceRuntimeServices.Create(new GlobalResourceRuntimeOptions
                {
                    AllowMissingPreloadGroupInEditor = true
                }),
                baseResourcePreloadGroupId: string.Empty);
            SpawnLocalPlayer(slice);

            CharacterTestFairyGuiCombatHudViewModel defaultHotbar =
                CharacterTestFairyGuiCombatHudViewModelBuilder.Build(slice);
            Assert.IsTrue(slice.AbilityHotbar.ClearSlot(1, out string message), message);
            CharacterTestFairyGuiCombatHudViewModel changedHotbar =
                CharacterTestFairyGuiCombatHudViewModelBuilder.Build(slice);

            Assert.AreNotEqual(
                CharacterTestFairyGuiCombatHudViewModelSignature.Create(defaultHotbar),
                CharacterTestFairyGuiCombatHudViewModelSignature.Create(changedHotbar));
            Assert.AreEqual("Empty", changedHotbar.AbilitySlots[0].AbilityText);
            Assert.IsFalse(changedHotbar.AbilitySlots[0].IsBound);
        }

        [Test]
        public void FairyGuiCombatHud_SignatureIncludesBehaviorCardPresentation()
        {
            var hidden = new CharacterTestFairyGuiCombatHudViewModel();
            var visible = new CharacterTestFairyGuiCombatHudViewModel
            {
                BehaviorCardVisible = true,
                BehaviorCardIconText = "ATK",
                BehaviorCardIconKey = "ui.card.attack",
                BehaviorCardName = "基础攻击",
                BehaviorCardReasonText = "目标进入攻击范围"
            };

            Assert.AreNotEqual(
                CharacterTestFairyGuiCombatHudViewModelSignature.Create(hidden),
                CharacterTestFairyGuiCombatHudViewModelSignature.Create(visible));
        }

[Test]
        public void FairyGuiCombatHud_SignatureCreateWarmPathDoesNotAllocate()
        {
            var viewModel = new CharacterTestFairyGuiCombatHudViewModel
            {
                HasLocalPlayer = true,
                PlayerEntityId = 1,
                PlayerName = "Wanderer",
                PlayerHealth = 50,
                PlayerMaxHealth = 100,
                PlayerScatterPressure = 25,
                PlayerMaxScatterPressure = 100,
                PlayerScatterPressureBand = "stable",
                PlayerActionText = "Action: light_attack",
                TargetVisible = true,
                TargetLocked = true,
                TargetEntityId = 2,
                TargetName = "Sentinel",
                TargetHealth = 30,
                TargetMaxHealth = 60,
                TargetScatterPressure = 40,
                TargetMaxScatterPressure = 80,
                TargetScatterPressureBand = "pressured",
                TargetStatusText = "HITSTUN 4f",
                FeedbackVisible = true,
                FeedbackText = "Input rejected: blocked.",
                NetworkLatencyVisible = true,
                NetworkLatencyText = "NET 4f / 67ms",
                NetworkLatencyTone = "warn"
            };
            viewModel.SetAbilitySlots(CreateCombatHudSlotViewModels());
            viewModel.SetPlayerBuffs(new[]
            {
                new CharacterTestFairyGuiCombatHudBuffViewModel
                {
                    BuffId = CharacterTestGuardAuraIds.BuffId,
                    ShortName = "DEF+",
                    TimeText = "2f",
                    StackText = "2",
                    TooltipText = "Guard Aura Defense",
                    Remaining01 = 0.5f,
                    Polarity = GameplayBuffPresentationPolarity.Buff
                }
            });
            viewModel.SetTargetBuffs(new[]
            {
                new CharacterTestFairyGuiCombatHudBuffViewModel
                {
                    BuffId = CharacterTestGuardAuraIds.OnHitDefenseBreakBuffId,
                    ShortName = "DEF-",
                    TimeText = "300f",
                    TooltipText = "Defense Break",
                    Remaining01 = 0.25f,
                    Polarity = GameplayBuffPresentationPolarity.Debuff
                }
            });

            _ = CharacterTestFairyGuiCombatHudViewModelSignature.Create(viewModel);
            _ = GC.GetAllocatedBytesForCurrentThread();
            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < 64; i++)
                _ = CharacterTestFairyGuiCombatHudViewModelSignature.Create(viewModel);

            long allocatedBytes = GC.GetAllocatedBytesForCurrentThread() - before;
            Assert.AreEqual(0L, allocatedBytes);
        }

[Test]
        public void FairyGuiCombatHud_BuildIntoStableHotbarDoesNotAllocateAfterWarmup()
        {
            using var slice = new GameSlice(
                NullRuntimeLogger.Instance,
                CharacterTestStoryFixture.CreateBootstrapContent(),
                resources: GlobalResourceRuntimeServices.Create(new GlobalResourceRuntimeOptions
                {
                    AllowMissingPreloadGroupInEditor = true
                }),
                baseResourcePreloadGroupId: string.Empty);
            CharacterTestCharacterEntity localPlayer = SpawnLocalPlayer(slice);
            var viewModel = new CharacterTestFairyGuiCombatHudViewModel();
            var hotbarStates = new CharacterTestAbilityHotbarSlotState[
                CharacterTestAbilityHotbarRuntime.SlotCount];

            for (int i = 0; i < 8; i++)
            {
                CharacterTestFairyGuiCombatHudViewModelBuilder.BuildInto(
                    viewModel,
                    slice,
                    MxUiNullTextProvider.Instance,
                    networkStatus: null,
                    hotbarSlotStates: hotbarStates);
            }

            _ = GC.GetAllocatedBytesForCurrentThread();
            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < 64; i++)
            {
                CharacterTestFairyGuiCombatHudViewModelBuilder.BuildInto(
                    viewModel,
                    slice,
                    MxUiNullTextProvider.Instance,
                    networkStatus: null,
                    hotbarSlotStates: hotbarStates);
            }

            long allocatedBytes = GC.GetAllocatedBytesForCurrentThread() - before;
            Assert.AreEqual(0L, allocatedBytes);
            Assert.AreEqual(localPlayer.Id.Value, viewModel.PlayerEntityId);
            Assert.AreEqual(CharacterTestAbilityHotbarRuntime.SlotCount, viewModel.AbilitySlots.Count);
            Assert.AreEqual("1", viewModel.AbilitySlots[0].KeyText);
            Assert.AreEqual("Fireball", viewModel.AbilitySlots[0].AbilityText);
        }

[Test]
        public void FairyGuiCombatHud_BuildsBuffPresentationBarsFromActiveBuffs()
        {
            using var slice = CreateSliceWithLockedTarget(out CharacterTestCharacterEntity localPlayer);
            CharacterTestCharacterEntity target = slice.CharacterEntities.First(
                entity => entity.Id.Value == slice.LockedTargetEntityId);
            GameplayComponentStore<GameplayComponentBuffSetComponent> buffStore =
                slice.CharacterRuntime.World.GameplayComponentWorld
                    .GetOrCreateStore<GameplayComponentBuffSetComponent>();
            long frame = slice.CurrentFrame.Value;
            buffStore.Set(
                localPlayer.GameplayEntityId,
                new GameplayComponentBuffSetComponent(new GameplayComponentBuffEntry(
                    CharacterTestGuardAuraIds.BuffId,
                    stackCount: 1,
                    startFrame: frame,
                    endFrame: frame + CharacterTestGuardAuraIds.GuardAuraBuffDurationFrames)));
            buffStore.Set(
                target.GameplayEntityId,
                new GameplayComponentBuffSetComponent(
                    new GameplayComponentBuffEntry(
                        CharacterTestGuardAuraIds.OnHitDefenseBreakBuffId,
                        stackCount: 1,
                        startFrame: frame,
                        endFrame: frame + CharacterTestGuardAuraIds.OnHitDefenseBreakDurationFrames),
                    new GameplayComponentBuffEntry(
                        CharacterTestStasisIds.StasisBuffId,
                        stackCount: 1,
                        startFrame: frame,
                        endFrame: frame + CharacterTestStasisIds.StasisBuffDurationFrames)));

            MxConfigUiTextProvider textProvider = CreateCharacterTestTextProvider("zh-CN");
            CharacterTestFairyGuiCombatHudViewModel viewModel =
                CharacterTestFairyGuiCombatHudViewModelBuilder.Build(slice, textProvider);

            Assert.AreEqual(1, viewModel.PlayerBuffs.Count);
            Assert.AreEqual(CharacterTestGuardAuraIds.BuffId, viewModel.PlayerBuffs[0].BuffId);
            Assert.AreEqual("守护光环防御", viewModel.PlayerBuffs[0].DisplayName);
            Assert.AreEqual("DEF+", viewModel.PlayerBuffs[0].ShortName);
            Assert.AreEqual(GameplayBuffPresentationPolarity.Buff, viewModel.PlayerBuffs[0].Polarity);
            StringAssert.Contains("守护光环防御", viewModel.PlayerBuffs[0].TooltipText);
            StringAssert.Contains("防御+5，瞬时，不可叠加", viewModel.PlayerBuffs[0].TooltipText);
            Assert.AreEqual(2, viewModel.TargetBuffs.Count);
            Assert.AreEqual(CharacterTestStasisIds.StasisBuffId, viewModel.TargetBuffs[0].BuffId);
            Assert.AreEqual("凝滞", viewModel.TargetBuffs[0].DisplayName);
            Assert.AreEqual("STOP", viewModel.TargetBuffs[0].ShortName);
            Assert.AreEqual(GameplayBuffPresentationPolarity.Debuff, viewModel.TargetBuffs[0].Polarity);
            StringAssert.Contains("凝滞", viewModel.TargetBuffs[0].TooltipText);
            StringAssert.Contains(
                "stasis",
                viewModel.TargetBuffs[0].TooltipText);
            Assert.AreEqual(CharacterTestGuardAuraIds.OnHitDefenseBreakBuffId, viewModel.TargetBuffs[1].BuffId);
            Assert.AreEqual("防御破绽", viewModel.TargetBuffs[1].DisplayName);
            Assert.AreEqual("DEF-", viewModel.TargetBuffs[1].ShortName);
            Assert.AreEqual(GameplayBuffPresentationPolarity.Debuff, viewModel.TargetBuffs[1].Polarity);
            StringAssert.Contains("防御破绽", viewModel.TargetBuffs[1].TooltipText);
            StringAssert.Contains("防御-3，持�?秒，不可叠加", viewModel.TargetBuffs[1].TooltipText);

            textProvider.SetLocale(new MxUiLocaleId("en-US"));
            CharacterTestFairyGuiCombatHudViewModel enViewModel =
                CharacterTestFairyGuiCombatHudViewModelBuilder.Build(slice, textProvider);
            Assert.AreEqual("Guard Aura Defense", enViewModel.PlayerBuffs[0].DisplayName);
            StringAssert.Contains("Defense +5, instant, not stackable", enViewModel.PlayerBuffs[0].TooltipText);
            Assert.AreEqual("Stasis", enViewModel.TargetBuffs[0].DisplayName);
            StringAssert.Contains(
                "Self-driven action, movement, and facing pause; external motion remains active.",
                enViewModel.TargetBuffs[0].TooltipText);
            Assert.AreEqual("Defense Break", enViewModel.TargetBuffs[1].DisplayName);
            StringAssert.Contains("Defense -3, lasts 5s, not stackable", enViewModel.TargetBuffs[1].TooltipText);

            CharacterTestFairyGuiCombatHudViewModelSignature withBuffs =
                CharacterTestFairyGuiCombatHudViewModelSignature.Create(viewModel);
            buffStore.Remove(target.GameplayEntityId);
            CharacterTestFairyGuiCombatHudViewModel withoutTargetBuff =
                CharacterTestFairyGuiCombatHudViewModelBuilder.Build(slice, textProvider);
            Assert.AreNotEqual(
                withBuffs,
                CharacterTestFairyGuiCombatHudViewModelSignature.Create(withoutTargetBuff));
            Assert.AreEqual(0, withoutTargetBuff.TargetBuffs.Count);
        }

[Test]
        public void FairyGuiCombatHud_ShowsRecentOnHitTargetBuffWithoutTargetLock()
        {
            using var slice = new GameSlice(
                NullRuntimeLogger.Instance,
                CharacterTestStoryFixture.CreateBootstrapContent(),
                resources: GlobalResourceRuntimeServices.Create(new GlobalResourceRuntimeOptions
                {
                    AllowMissingPreloadGroupInEditor = true
                }),
                baseResourcePreloadGroupId: string.Empty);
            CharacterTestCharacterEntity localPlayer = SpawnLocalPlayer(slice);

            RuntimeCommandValidationResult spawnTarget = slice.EnqueueRuntimeCommand(
                CharacterTestRuntimeCommandFactory.SpawnCharacter(
                    slice.CurrentFrame,
                    CharacterTestRuntimeCommandSources.System,
                    CharacterTestCharacterSpawnSpec.TemporaryCompanion,
                    new MxFramework.Runtime.RuntimeTraceIdentity(global::MxFramework.Tests.Game.CharacterTest.MigrationFixtureIds.Trace0023)));
            Assert.IsTrue(spawnTarget.Success, spawnTarget.Error.Message);
            slice.Tick(0d);

            CharacterTestCharacterEntity target = slice.CharacterEntities.First(entity => entity != localPlayer);
            long frame = slice.CurrentFrame.Value;
            target.SetCombatState(target.CombatState.ApplyHit(
                damage: 1,
                staggerFrames: 0,
                frame,
                sourceEntityId: localPlayer.Id.Value,
                traceId: 42));
            GameplayComponentStore<GameplayComponentBuffSetComponent> buffStore =
                slice.CharacterRuntime.World.GameplayComponentWorld
                    .GetOrCreateStore<GameplayComponentBuffSetComponent>();
            buffStore.Set(
                target.GameplayEntityId,
                new GameplayComponentBuffSetComponent(new GameplayComponentBuffEntry(
                    CharacterTestGuardAuraIds.OnHitDefenseBreakBuffId,
                    stackCount: 1,
                    startFrame: frame,
                    endFrame: frame + CharacterTestGuardAuraIds.OnHitDefenseBreakDurationFrames)));

            CharacterTestFairyGuiCombatHudViewModel viewModel =
                CharacterTestFairyGuiCombatHudViewModelBuilder.Build(slice);

            Assert.IsTrue(viewModel.TargetVisible);
            Assert.IsFalse(viewModel.TargetLocked);
            Assert.AreEqual(target.Id.Value, viewModel.TargetEntityId);
            Assert.AreEqual("TARGET", viewModel.TargetStatusText);
            Assert.AreEqual(1, viewModel.TargetBuffs.Count);
            Assert.AreEqual(
                CharacterTestGuardAuraIds.OnHitDefenseBreakBuffId,
                viewModel.TargetBuffs[0].BuffId);
            Assert.AreEqual("DEF-", viewModel.TargetBuffs[0].ShortName);
        }

[Test]
        public void FairyGuiHeadPad_RendersBuffPresentationBadges()
        {
            using CharacterTestFairyGuiHeadPad pad =
                CharacterTestFairyGuiHeadPad.CreateDetachedForTests(out Fgui.GComponent root);
            Assert.NotNull(root);

            var items = new[]
            {
                new GameplayBuffPresentationItem(
                    CharacterTestGuardAuraIds.BuffId,
                    "Guard Aura Defense",
                    "DEF+",
                    stackCount: 1,
                    remainingFrames: 2L,
                    remaining01: 0.5f,
                    isPermanent: false,
                    GameplayBuffPresentationPolarity.Aura,
                    iconKey: string.Empty,
                    effectSummary: "Defense(3) +5",
                    priority: 100),
                new GameplayBuffPresentationItem(
                    CharacterTestGuardAuraIds.OnHitDefenseBreakBuffId,
                    "Defense Break",
                    "DEF-",
                    stackCount: 3,
                    remainingFrames: 15L,
                    remaining01: 0.25f,
                    isPermanent: false,
                    GameplayBuffPresentationPolarity.Debuff,
                    iconKey: string.Empty,
                    effectSummary: "Defense(3) -3",
                    priority: 90),
                new GameplayBuffPresentationItem(
                    601010,
                    "Guarded",
                    "BUF",
                    stackCount: 1,
                    remainingFrames: 0L,
                    remaining01: 1f,
                    isPermanent: true,
                    GameplayBuffPresentationPolarity.Buff,
                    iconKey: string.Empty,
                    effectSummary: "Attack(2) +1",
                    priority: 10),
                new GameplayBuffPresentationItem(
                    601011,
                    "Neutral State",
                    "NEU",
                    stackCount: 1,
                    remainingFrames: 9L,
                    remaining01: 0.75f,
                    isPermanent: false,
                    GameplayBuffPresentationPolarity.Neutral,
                    iconKey: string.Empty,
                    effectSummary: "Status(7)",
                    priority: 1)
            };

            CharacterTestCharacterCombatStateComponent combatState =
                CharacterTestCharacterCombatStateComponent.CreateDefault();
            pad.Sync(
                1,
                "Target",
                2,
                CharacterTestCharacterControlOwner.RemotePlayer,
                CharacterTestHeadPadAllegiance.Enemy,
                combatState,
                true,
                CharacterTestHeadPadDisplayMode.CombatReadout,
                items,
                Vector3.zero);

            Assert.IsTrue(HeadPadBadge(root, 1).visible);
            Assert.IsTrue(HeadPadBadge(root, 4).visible);
            Assert.IsNull(root.GetChild("buffBadge5"));
            Assert.AreEqual("DEF+", HeadPadBadgeText(root, 1, "Label"));
            Assert.AreEqual(string.Empty, HeadPadBadgeText(root, 1, "Stack"));
            Assert.IsTrue(HeadPadBadgeObject(root, 1, "Remaining").visible);
            Assert.AreEqual(15f, HeadPadBadgeObject(root, 1, "Remaining").width, 0.001f);
            Assert.AreEqual("DEF-", HeadPadBadgeText(root, 2, "Label"));
            Assert.AreEqual("3", HeadPadBadgeText(root, 2, "Stack"));
            Assert.IsTrue(HeadPadBadgeObject(root, 2, "Remaining").visible);
            Assert.AreEqual(8f, HeadPadBadgeObject(root, 2, "Remaining").width, 0.001f);
            Assert.AreEqual("BUF", HeadPadBadgeText(root, 3, "Label"));
            Assert.IsFalse(HeadPadBadgeObject(root, 3, "Remaining").visible);
            Assert.AreEqual("NEU", HeadPadBadgeText(root, 4, "Label"));
            Assert.AreNotEqual(
                CharacterTestFairyGuiHeadPad.ResolveBuffBadgeBorderColor(GameplayBuffPresentationPolarity.Aura),
                CharacterTestFairyGuiHeadPad.ResolveBuffBadgeBorderColor(GameplayBuffPresentationPolarity.Debuff));
            Assert.AreNotEqual(
                CharacterTestFairyGuiHeadPad.ResolveBuffBadgeBackgroundColor(GameplayBuffPresentationPolarity.Buff),
                CharacterTestFairyGuiHeadPad.ResolveBuffBadgeBackgroundColor(GameplayBuffPresentationPolarity.Neutral));

            int firstBadgeUpdates = pad.DebugGetBuffBadgeUpdateCount(1);
            int secondBadgeUpdates = pad.DebugGetBuffBadgeUpdateCount(2);
            pad.Sync(
                1,
                "Target",
                2,
                CharacterTestCharacterControlOwner.RemotePlayer,
                CharacterTestHeadPadAllegiance.Enemy,
                combatState,
                true,
                CharacterTestHeadPadDisplayMode.CombatReadout,
                items,
                Vector3.zero);

            Assert.AreEqual(firstBadgeUpdates, pad.DebugGetBuffBadgeUpdateCount(1));
            Assert.AreEqual(secondBadgeUpdates, pad.DebugGetBuffBadgeUpdateCount(2));

            GameplayBuffPresentationItem[] changedItems =
            {
                new GameplayBuffPresentationItem(
                    items[0].BuffId,
                    items[0].DisplayName,
                    items[0].ShortName,
                    items[0].StackCount,
                    items[0].RemainingFrames,
                    0.25f,
                    items[0].IsPermanent,
                    items[0].Polarity,
                    items[0].IconKey,
                    items[0].EffectSummary,
                    items[0].Priority),
                items[1],
                items[2],
                items[3]
            };
            pad.Sync(
                1,
                "Target",
                2,
                CharacterTestCharacterControlOwner.RemotePlayer,
                CharacterTestHeadPadAllegiance.Enemy,
                combatState,
                true,
                CharacterTestHeadPadDisplayMode.CombatReadout,
                changedItems,
                Vector3.zero);

            Assert.Greater(pad.DebugGetBuffBadgeUpdateCount(1), firstBadgeUpdates);
            Assert.AreEqual(secondBadgeUpdates, pad.DebugGetBuffBadgeUpdateCount(2));
            Assert.AreEqual(8f, HeadPadBadgeObject(root, 1, "Remaining").width, 0.001f);

            pad.Sync(
                1,
                "Target",
                2,
                CharacterTestCharacterControlOwner.RemotePlayer,
                CharacterTestHeadPadAllegiance.Enemy,
                combatState,
                true,
                CharacterTestHeadPadDisplayMode.HealthOnly,
                items,
                Vector3.zero);

            Assert.IsFalse(root.GetChild("scatterPressureBack").visible);
            Assert.IsTrue(HeadPadBadge(root, 1).visible);

            pad.Sync(
                1,
                "Target",
                2,
                CharacterTestCharacterControlOwner.RemotePlayer,
                CharacterTestHeadPadAllegiance.Enemy,
                combatState,
                true,
                CharacterTestHeadPadDisplayMode.Hidden,
                items,
                Vector3.zero);

            Assert.IsFalse(HeadPadBadge(root, 1).visible);
            Assert.IsFalse(HeadPadBadge(root, 4).visible);
        }

[Test]
        public void FairyGuiCombatHudSourcePackage_DeclaresNetworkLatencyBadgeControls()
        {
            string panelPath = Path.Combine(
                Directory.GetCurrentDirectory(),
                "FGUIProject/assets/MxCharacterTestCombatHud/CombatHudPanel.xml");

            string panelText = File.ReadAllText(panelPath);

            StringAssert.Contains("CombatHudPanel", File.ReadAllText(Path.Combine(
                Directory.GetCurrentDirectory(),
                "FGUIProject/assets/MxCharacterTestCombatHud/package.xml")));
            AssertCombatHudChildDeclared(
                panelText,
                CharacterTestFairyGuiCombatHudIds.NetworkLatencyBadgeBackground);
            AssertCombatHudChildDeclared(panelText, CharacterTestFairyGuiCombatHudIds.NetworkLatencyText);
            for (int slot = 1; slot <= CharacterTestAbilityHotbarRuntime.SlotCount; slot++)
            {
                AssertCombatHudChildDeclared(panelText, CharacterTestFairyGuiCombatHudIds.AbilitySlotBackground(slot));
                AssertCombatHudChildDeclared(panelText, CharacterTestFairyGuiCombatHudIds.AbilitySlotSelected(slot));
                AssertCombatHudChildDeclared(panelText, CharacterTestFairyGuiCombatHudIds.AbilitySlotKey(slot));
                AssertCombatHudChildDeclared(panelText, CharacterTestFairyGuiCombatHudIds.AbilitySlotName(slot));
                AssertCombatHudChildDeclared(panelText, CharacterTestFairyGuiCombatHudIds.AbilitySlotCooldownFill(slot));
                AssertCombatHudChildDeclared(panelText, CharacterTestFairyGuiCombatHudIds.AbilitySlotCooldownText(slot));
            }

            for (int slot = 1; slot <= CharacterTestFairyGuiCombatHudIds.BuffSlotCount; slot++)
            {
                AssertCombatHudChildDeclared(panelText, CharacterTestFairyGuiCombatHudIds.PlayerBuffSlotBackground(slot));
                AssertCombatHudChildDeclared(panelText, CharacterTestFairyGuiCombatHudIds.PlayerBuffSlotBorder(slot));
                AssertCombatHudChildDeclared(panelText, CharacterTestFairyGuiCombatHudIds.PlayerBuffSlotLabel(slot));
                AssertCombatHudChildDeclared(panelText, CharacterTestFairyGuiCombatHudIds.PlayerBuffSlotCooldownFill(slot));
                AssertCombatHudChildDeclared(panelText, CharacterTestFairyGuiCombatHudIds.PlayerBuffSlotStack(slot));
                AssertCombatHudChildDeclared(panelText, CharacterTestFairyGuiCombatHudIds.PlayerBuffSlotTime(slot));
                AssertCombatHudChildDeclared(panelText, CharacterTestFairyGuiCombatHudIds.TargetBuffSlotBackground(slot));
                AssertCombatHudChildDeclared(panelText, CharacterTestFairyGuiCombatHudIds.TargetBuffSlotBorder(slot));
                AssertCombatHudChildDeclared(panelText, CharacterTestFairyGuiCombatHudIds.TargetBuffSlotLabel(slot));
                AssertCombatHudChildDeclared(panelText, CharacterTestFairyGuiCombatHudIds.TargetBuffSlotCooldownFill(slot));
                AssertCombatHudChildDeclared(panelText, CharacterTestFairyGuiCombatHudIds.TargetBuffSlotStack(slot));
                AssertCombatHudChildDeclared(panelText, CharacterTestFairyGuiCombatHudIds.TargetBuffSlotTime(slot));
            }
        }

[Test]
        public void FairyGuiCombatHudBinder_BindsVisibilityTextAndBarWidths()
        {
            Fgui.GComponent root = CreateCombatHudRoot();
            using (var handle = new MxFairyGuiComponentHandle(
                CharacterTestFairyGuiCombatHudIds.PackageId,
                CharacterTestFairyGuiCombatHudIds.ComponentName,
                root))
            {
                var binder = new CharacterTestFairyGuiCombatHudBinder();
                var viewModel = new CharacterTestFairyGuiCombatHudViewModel
                {
                    HasLocalPlayer = true,
                    PlayerName = "Wanderer",
                    PlayerHealth = 50,
                    PlayerMaxHealth = 100,
                    PlayerHealthRatio = 0.5f,
                    PlayerScatterPressure = 25,
                    PlayerMaxScatterPressure = 100,
                    PlayerScatterPressureRatio = 0.25f,
                    PlayerActionText = "Action: light_attack",
                    TargetVisible = true,
                    TargetLocked = true,
                    TargetName = "Sentinel",
                    TargetHealth = 30,
                    TargetMaxHealth = 60,
                    TargetHealthRatio = 0.5f,
                    TargetScatterPressure = 40,
                    TargetMaxScatterPressure = 80,
                    TargetScatterPressureRatio = 0.5f,
                    TargetStatusText = "HITSTUN 4f",
                    FeedbackVisible = true,
                    FeedbackText = "Input rejected: blocked.",
                    NetworkLatencyVisible = true,
                    NetworkLatencyText = "NET 4f / 67ms",
                    NetworkLatencyTone = "warn"
                };
                viewModel.SetAbilitySlots(CreateCombatHudSlotViewModels());
                viewModel.SetPlayerBuffs(new[]
                {
                    new CharacterTestFairyGuiCombatHudBuffViewModel
                    {
                        BuffId = CharacterTestGuardAuraIds.BuffId,
                        ShortName = "DEF+",
                        TimeText = "2f",
                        Remaining01 = 0.5f,
                        Polarity = GameplayBuffPresentationPolarity.Aura,
                        TooltipText = "Guard Aura\nDefense(3) +5"
                    }
                });
                viewModel.SetTargetBuffs(new[]
                {
                    new CharacterTestFairyGuiCombatHudBuffViewModel
                    {
                        BuffId = CharacterTestGuardAuraIds.OnHitDefenseBreakBuffId,
                        ShortName = "DEF-",
                        TimeText = "300f",
                        Remaining01 = 0.25f,
                        Polarity = GameplayBuffPresentationPolarity.Debuff,
                        TooltipText = "Defense Break\nDefense(3) -3"
                    }
                });

                binder.Bind(handle, viewModel);

                Assert.AreEqual("Wanderer", CombatHudText(root, CharacterTestFairyGuiCombatHudIds.PlayerName));
                Assert.AreEqual("50/100", CombatHudText(root, CharacterTestFairyGuiCombatHudIds.PlayerHealthText));
                Assert.AreEqual("Action: light_attack", CombatHudText(root, CharacterTestFairyGuiCombatHudIds.PlayerActionText));
                Assert.AreEqual(178f, CombatHudObject(root, CharacterTestFairyGuiCombatHudIds.PlayerHealthFill).width, 0.001f);
                Assert.AreEqual(88f, CombatHudObject(root, CharacterTestFairyGuiCombatHudIds.PlayerScatterPressureFill).width, 0.001f);
                Assert.IsTrue(CombatHudObject(root, CharacterTestFairyGuiCombatHudIds.TargetPlate).visible);
                Assert.IsTrue(CombatHudObject(root, CharacterTestFairyGuiCombatHudIds.LockCrosshair).visible);
                Assert.AreEqual("Sentinel", CombatHudText(root, CharacterTestFairyGuiCombatHudIds.TargetName));
                Assert.AreEqual("HITSTUN 4f", CombatHudText(root, CharacterTestFairyGuiCombatHudIds.TargetStatusText));
                Assert.AreEqual(178f, CombatHudObject(root, CharacterTestFairyGuiCombatHudIds.TargetHealthFill).width, 0.001f);
                Assert.AreEqual(176f, CombatHudObject(root, CharacterTestFairyGuiCombatHudIds.TargetScatterPressureFill).width, 0.001f);
                Assert.IsTrue(CombatHudObject(root, CharacterTestFairyGuiCombatHudIds.FeedbackPanel).visible);
                Assert.IsTrue(CombatHudObject(root, CharacterTestFairyGuiCombatHudIds.FeedbackTrim).visible);
                Assert.IsTrue(CombatHudObject(root, CharacterTestFairyGuiCombatHudIds.FeedbackText).visible);
                Assert.AreEqual("Input rejected: blocked.", CombatHudText(root, CharacterTestFairyGuiCombatHudIds.FeedbackText));
                Assert.IsTrue(CombatHudObject(root, CharacterTestFairyGuiCombatHudIds.NetworkLatencyBadgeBackground).visible);
                Assert.IsTrue(CombatHudObject(root, CharacterTestFairyGuiCombatHudIds.NetworkLatencyText).visible);
                Assert.AreEqual("NET 4f / 67ms", CombatHudText(root, CharacterTestFairyGuiCombatHudIds.NetworkLatencyText));
                Assert.AreEqual("1", CombatHudText(root, CharacterTestFairyGuiCombatHudIds.AbilitySlotKey(1)));
                Assert.AreEqual("Flame Lance", CombatHudText(root, CharacterTestFairyGuiCombatHudIds.AbilitySlotName(1)));
                Assert.AreEqual("30f", CombatHudText(root, CharacterTestFairyGuiCombatHudIds.AbilitySlotCooldownText(1)));
                Assert.IsTrue(CombatHudObject(root, CharacterTestFairyGuiCombatHudIds.AbilitySlotSelected(1)).visible);
                Assert.IsTrue(CombatHudObject(root, CharacterTestFairyGuiCombatHudIds.AbilitySlotCooldownFill(1)).visible);
                Assert.AreEqual(36f, CombatHudObject(root, CharacterTestFairyGuiCombatHudIds.AbilitySlotCooldownFill(1)).width, 0.001f);
                Assert.AreEqual("Empty", CombatHudText(root, CharacterTestFairyGuiCombatHudIds.AbilitySlotName(8)));
                Assert.IsTrue(CombatHudObject(root, CharacterTestFairyGuiCombatHudIds.PlayerBuffSlotBackground(1)).visible);
                Assert.AreEqual("DEF+", CombatHudText(root, CharacterTestFairyGuiCombatHudIds.PlayerBuffSlotLabel(1)));
                Assert.AreEqual("2f", CombatHudText(root, CharacterTestFairyGuiCombatHudIds.PlayerBuffSlotTime(1)));
                Assert.AreEqual(18f, CombatHudObject(root, CharacterTestFairyGuiCombatHudIds.PlayerBuffSlotCooldownFill(1)).height, 0.001f);
                Assert.IsTrue(CombatHudObject(root, CharacterTestFairyGuiCombatHudIds.TargetBuffSlotBackground(1)).visible);
                Assert.AreEqual("DEF-", CombatHudText(root, CharacterTestFairyGuiCombatHudIds.TargetBuffSlotLabel(1)));
                Assert.AreEqual("300f", CombatHudText(root, CharacterTestFairyGuiCombatHudIds.TargetBuffSlotTime(1)));
                Assert.AreEqual(27f, CombatHudObject(root, CharacterTestFairyGuiCombatHudIds.TargetBuffSlotCooldownFill(1)).height, 0.001f);

                viewModel.TargetVisible = false;
                viewModel.TargetLocked = false;
                viewModel.FeedbackVisible = false;
                viewModel.NetworkLatencyVisible = false;
                binder.Bind(handle, viewModel);

                Assert.IsFalse(CombatHudObject(root, CharacterTestFairyGuiCombatHudIds.TargetPlate).visible);
                Assert.IsFalse(CombatHudObject(root, CharacterTestFairyGuiCombatHudIds.TargetBuffSlotBackground(1)).visible);
                Assert.IsFalse(CombatHudObject(root, CharacterTestFairyGuiCombatHudIds.LockCrosshair).visible);
                Assert.IsFalse(CombatHudObject(root, CharacterTestFairyGuiCombatHudIds.FeedbackPanel).visible);
                Assert.IsFalse(CombatHudObject(root, CharacterTestFairyGuiCombatHudIds.FeedbackTrim).visible);
                Assert.IsFalse(CombatHudObject(root, CharacterTestFairyGuiCombatHudIds.FeedbackText).visible);
                Assert.AreEqual(string.Empty, CombatHudText(root, CharacterTestFairyGuiCombatHudIds.FeedbackText));
                Assert.IsFalse(CombatHudObject(root, CharacterTestFairyGuiCombatHudIds.NetworkLatencyBadgeBackground).visible);
                Assert.IsFalse(CombatHudObject(root, CharacterTestFairyGuiCombatHudIds.NetworkLatencyText).visible);
                Assert.AreEqual(string.Empty, CombatHudText(root, CharacterTestFairyGuiCombatHudIds.NetworkLatencyText));
            }
        }

[Test]
        public void FairyGuiCombatHudBinder_SyncLockCrosshairCentersOnStagePosition()
        {
            Fgui.GComponent root = CreateCombatHudRoot();
            Fgui.GObject crosshair = CombatHudObject(root, CharacterTestFairyGuiCombatHudIds.LockCrosshair);
            var stagePosition = new Vector2(320f, 240f);

            CharacterTestFairyGuiCombatHudBinder.SyncLockCrosshair(root, true, stagePosition);

            Assert.IsTrue(crosshair.visible);
            Assert.AreEqual(Mathf.Round(stagePosition.x - crosshair.width * 0.5f), crosshair.x, 0.001f);
            Assert.AreEqual(Mathf.Round(stagePosition.y - crosshair.height * 0.5f), crosshair.y, 0.001f);

            CharacterTestFairyGuiCombatHudBinder.SyncLockCrosshair(root, false, new Vector2(16f, 16f));

            Assert.IsFalse(crosshair.visible);
        }

[Test]
        public void FairyGuiCombatHudBinder_WarmBindDoesNotAllocateStatusText()
        {
            Fgui.GComponent root = CreateCombatHudRoot();
            using (var handle = new MxFairyGuiComponentHandle(
                CharacterTestFairyGuiCombatHudIds.PackageId,
                CharacterTestFairyGuiCombatHudIds.ComponentName,
                root))
            {
                var binder = new CharacterTestFairyGuiCombatHudBinder();
                CharacterTestFairyGuiCombatHudViewModel viewModel =
                    CreateCombatHudBinderTestViewModel();

                binder.Bind(handle, viewModel);

                _ = GC.GetAllocatedBytesForCurrentThread();
                long before = GC.GetAllocatedBytesForCurrentThread();
                for (int i = 0; i < 32; i++)
                    binder.Bind(handle, viewModel);

                long allocatedBytes = GC.GetAllocatedBytesForCurrentThread() - before;
                Assert.AreEqual(0L, allocatedBytes);
            }
        }

[Test]
        public void NetworkSessionModeMapper_MapsPlayerLanModesToUnityTransportEndpoints()
        {
            Assert.AreEqual(
                CharacterTestNetworkMode.UnityTransportHost,
                CharacterTestNetworkSessionModeMapper.ToNetworkMode(CharacterTestNetworkSessionKind.Host));
            Assert.AreEqual(
                CharacterTestNetworkMode.UnityTransportHost,
                CharacterTestNetworkSessionModeMapper.ToNetworkMode(
                    CharacterTestNetworkSessionKind.Host,
                    CharacterTestNetworkLaunchMode.DirectLan));
            Assert.AreEqual(
                CharacterTestNetworkMode.UnityTransportRoomHost,
                CharacterTestNetworkSessionModeMapper.ToNetworkMode(
                    CharacterTestNetworkSessionKind.Host,
                    CharacterTestNetworkLaunchMode.Room));
            Assert.AreEqual(
                CharacterTestNetworkMode.UnityTransportClient,
                CharacterTestNetworkSessionModeMapper.ToNetworkMode(CharacterTestNetworkSessionKind.Join));
            Assert.AreEqual(
                CharacterTestNetworkMode.UnityTransportClient,
                CharacterTestNetworkSessionModeMapper.ToNetworkMode(
                    CharacterTestNetworkSessionKind.Join,
                    CharacterTestNetworkLaunchMode.DirectLan));
            Assert.AreEqual(
                CharacterTestNetworkMode.UnityTransportRoomClient,
                CharacterTestNetworkSessionModeMapper.ToNetworkMode(
                    CharacterTestNetworkSessionKind.Join,
                    CharacterTestNetworkLaunchMode.Room));
            Assert.AreEqual(
                CharacterTestNetworkMode.Disabled,
                CharacterTestNetworkSessionModeMapper.ToNetworkMode(CharacterTestNetworkSessionKind.Offline));
            Assert.AreEqual(
                CharacterTestNetworkSessionKind.Host,
                CharacterTestNetworkSessionModeMapper.ToSessionKind(CharacterTestNetworkMode.UnityTransportServer));
            Assert.AreEqual(
                CharacterTestNetworkSessionKind.Host,
                CharacterTestNetworkSessionModeMapper.ToSessionKind(CharacterTestNetworkMode.UnityTransportHost));
            Assert.AreEqual(
                CharacterTestNetworkSessionKind.Host,
                CharacterTestNetworkSessionModeMapper.ToSessionKind(CharacterTestNetworkMode.UnityTransportRoomServer));
            Assert.AreEqual(
                CharacterTestNetworkSessionKind.Host,
                CharacterTestNetworkSessionModeMapper.ToSessionKind(CharacterTestNetworkMode.UnityTransportRoomHost));
            Assert.AreEqual(
                CharacterTestNetworkSessionKind.Join,
                CharacterTestNetworkSessionModeMapper.ToSessionKind(CharacterTestNetworkMode.UnityTransportClient));
            Assert.AreEqual(
                CharacterTestNetworkSessionKind.Join,
                CharacterTestNetworkSessionModeMapper.ToSessionKind(CharacterTestNetworkMode.UnityTransportRoomClient));
            Assert.AreEqual(
                CharacterTestNetworkLaunchMode.DirectLan,
                CharacterTestNetworkSessionModeMapper.ToLaunchMode(CharacterTestNetworkMode.UnityTransportHost));
            Assert.AreEqual(
                CharacterTestNetworkLaunchMode.Room,
                CharacterTestNetworkSessionModeMapper.ToLaunchMode(CharacterTestNetworkMode.UnityTransportRoomHost));
            Assert.AreEqual(
                CharacterTestNetworkSessionKind.Offline,
                CharacterTestNetworkSessionModeMapper.ToSessionKind(CharacterTestNetworkMode.UnityTransportLoopback));
        }

[Test]
        public void FairyGuiMainMenuBuilder_UsesControllerStatusAndLabels()
        {
            var controller = new RecordingMainMenuController();
            controller.Status = new CharacterTestMainMenuStatus
            {
                IsPaused = false,
                IsMenuVisible = true,
                MenuPausesRuntime = false,
                HasEnteredGameplay = true,
                IsNetworkSessionActive = true,
                NetworkSessionKind = CharacterTestNetworkSessionKind.Host,
                RuntimeFrame = 42,
                Message = "Hosting LAN session."
            };

            MxConfigUiTextProvider textProvider = CreateCharacterTestTextProvider("en-US");
            CharacterTestFairyGuiMainMenuViewModel viewModel =
                CharacterTestFairyGuiMainMenuViewModelBuilder.Build(
                    controller,
                    canOpenSettings: true,
                    canOpenAbilities: true,
                    textProvider);

            Assert.AreEqual("Resume", viewModel.StartLabel);
            Assert.IsTrue(viewModel.CanStart);
            Assert.IsTrue(viewModel.CanNetwork);
            Assert.IsTrue(viewModel.CanAbilities);
            Assert.IsTrue(viewModel.CanSettings);
            StringAssert.Contains("Running", viewModel.StatusText);
            StringAssert.Contains("Live Menu", viewModel.StatusText);
            StringAssert.Contains("Host", viewModel.StatusText);
            StringAssert.Contains("frame 42", viewModel.StatusText);
            Assert.AreEqual("Hosting LAN session.", viewModel.Message);

            textProvider.SetLocale(new MxUiLocaleId("zh-CN"));
            CharacterTestFairyGuiMainMenuViewModel zhViewModel =
                CharacterTestFairyGuiMainMenuViewModelBuilder.Build(
                    controller,
                    canOpenSettings: true,
                    canOpenAbilities: true,
                    textProvider);
            Assert.AreEqual("暂停菜单", zhViewModel.Title);
            Assert.AreEqual("继续", zhViewModel.StartLabel);
            StringAssert.Contains("运行中", zhViewModel.StatusText);
        }

[Test]
        public void FairyGuiMainMenuCommandSink_CallsControllerAndSettingsToggleCallback()
        {
            var controller = new RecordingMainMenuController();
            int settingsToggleCount = 0;
            int abilitiesToggleCount = 0;
            int behaviorDeckToggleCount = 0;
            var sink = new CharacterTestFairyGuiMainMenuCommandSink(
                controller,
                () => settingsToggleCount++,
                () => abilitiesToggleCount++,
                () => behaviorDeckToggleCount++);

            sink.Enqueue(new MxUiCommand(
                CharacterTestFairyGuiMainMenuIds.ViewId,
                CharacterTestFairyGuiMainMenuCommandIds.Start,
                null));
            Assert.AreEqual(1, controller.StartCount);
            Assert.IsTrue(sink.LastResult.Success);

            sink.Enqueue(new MxUiCommand(
                CharacterTestFairyGuiMainMenuIds.ViewId,
                CharacterTestFairyGuiMainMenuCommandIds.Network,
                null));
            Assert.AreEqual(1, controller.NetworkCount);

            sink.Enqueue(new MxUiCommand(
                CharacterTestFairyGuiMainMenuIds.ViewId,
                CharacterTestFairyGuiMainMenuCommandIds.SettingsToggle,
                null));
            Assert.AreEqual(1, settingsToggleCount);
            Assert.AreEqual(1, sink.SettingsToggleCount);
            Assert.AreEqual(1, controller.NetworkCount);

            sink.Enqueue(new MxUiCommand(
                CharacterTestFairyGuiMainMenuIds.ViewId,
                CharacterTestFairyGuiMainMenuCommandIds.AbilitiesToggle,
                null));
            Assert.AreEqual(1, abilitiesToggleCount);
            Assert.AreEqual(1, sink.AbilitiesToggleCount);
            Assert.AreEqual(1, controller.NetworkCount);

            sink.Enqueue(new MxUiCommand(
                CharacterTestFairyGuiMainMenuIds.ViewId,
                CharacterTestFairyGuiMainMenuCommandIds.BehaviorDeckToggle,
                null));
            Assert.AreEqual(1, behaviorDeckToggleCount);
            Assert.AreEqual(1, sink.BehaviorDeckToggleCount);
            Assert.AreEqual(1, controller.NetworkCount);

            sink.Enqueue(new MxUiCommand(
                CharacterTestFairyGuiMainMenuIds.ViewId,
                CharacterTestFairyGuiMainMenuCommandIds.RestartOffline,
                null));
            Assert.AreEqual(1, controller.RestartCount);

            sink.Enqueue(new MxUiCommand(
                CharacterTestFairyGuiMainMenuIds.ViewId,
                CharacterTestFairyGuiMainMenuCommandIds.Close,
                null));
            Assert.AreEqual(1, controller.CloseCount);
        }

[Test]
        public void FairyGuiMainMenuSourcePackage_DeclaresMainMenuComponentAndBindableControls()
        {
            string packagePath = Path.Combine(
                Directory.GetCurrentDirectory(),
                "FGUIProject/assets/MxCharacterTestUi/package.xml");
            string panelPath = Path.Combine(
                Directory.GetCurrentDirectory(),
                "FGUIProject/assets/MxCharacterTestUi/MainMenuPanel.xml");
            string primaryButtonPath = Path.Combine(
                Directory.GetCurrentDirectory(),
                "FGUIProject/assets/MxCharacterTestUi/Components/PrimaryCommandButton.xml");

            string packageText = File.ReadAllText(packagePath);
            string panelText = File.ReadAllText(panelPath);
            string buttonText = File.ReadAllText(primaryButtonPath);

            StringAssert.Contains("MainMenuPanel.xml", packageText);
            StringAssert.Contains("PrimaryCommandButton.xml", packageText);
            StringAssert.Contains("NeutralCommandButton.xml", packageText);
            StringAssert.Contains("DangerCommandButton.xml", packageText);
            StringAssert.Contains("panel_modal_frame_512x512.png", packageText);
            StringAssert.Contains("scale9grid=\"48,48,416,416\"", packageText);
            StringAssert.Contains("button_command_base_320x72.png", packageText);
            StringAssert.Contains("scale9grid=\"36,18,248,36\"", packageText);
            StringAssert.Contains("extention=\"Button\"", buttonText);
            StringAssert.Contains("name=\"icon\"", buttonText);
            StringAssert.Contains("name=\"overOverlay\"", buttonText);
            StringAssert.Contains("name=\"downOverlay\"", buttonText);
            StringAssert.Contains("fileName=\"Images/MxUiFoundation/panels/panel_modal_frame_512x512.png\"", panelText);
            StringAssert.Contains("fileName=\"Images/MxUiFoundation/dividers/divider_section_horizontal_512x16.png\"", panelText);
            StringAssert.Contains("name=\"btnStart\"", panelText);
            StringAssert.Contains("name=\"btnBehaviorDeck\"", panelText);
            StringAssert.Contains("icon=\"ui://", panelText);
            AssertMainMenuRelationDeclared(panelText, CharacterTestFairyGuiMainMenuIds.Overlay, "width-width,height-height");
            AssertMainMenuRelationDeclared(panelText, CharacterTestFairyGuiMainMenuIds.PanelFrame, "center-center,middle-middle");
            AssertMainMenuChildDeclared(panelText, CharacterTestFairyGuiMainMenuIds.Title);
            AssertMainMenuChildDeclared(panelText, CharacterTestFairyGuiMainMenuIds.Status);
            AssertMainMenuChildDeclared(panelText, CharacterTestFairyGuiMainMenuIds.Message);
            AssertMainMenuChildDeclared(panelText, CharacterTestFairyGuiMainMenuIds.Start);
            AssertMainMenuChildDeclared(panelText, CharacterTestFairyGuiMainMenuIds.Network);
            AssertMainMenuChildDeclared(panelText, CharacterTestFairyGuiMainMenuIds.Abilities);
            AssertMainMenuChildDeclared(panelText, CharacterTestFairyGuiMainMenuIds.BehaviorDeck);
            AssertMainMenuChildDeclared(panelText, CharacterTestFairyGuiMainMenuIds.Settings);
            AssertMainMenuChildDeclared(panelText, CharacterTestFairyGuiMainMenuIds.Restart);
            AssertMainMenuChildDeclared(panelText, CharacterTestFairyGuiMainMenuIds.Close);
        }

[Test]
        public void FairyGuiMainMenuBinder_BindsLabelsAndCommands()
        {
            Fgui.GComponent root = CreateMainMenuRoot();
            var sink = new RecordingUiCommandSink();
            using (var handle = new MxFairyGuiComponentHandle(
                CharacterTestFairyGuiMainMenuIds.PackageId,
                CharacterTestFairyGuiMainMenuIds.ComponentName,
                root))
            {
                var binder = new CharacterTestFairyGuiMainMenuBinder(sink);
                var viewModel = new CharacterTestFairyGuiMainMenuViewModel
                {
                    StatusText = "Running  Live Menu  Offline  frame 5",
                    Message = "Ready.",
                    StartLabel = "Resume",
                    CanStart = true,
                    CanNetwork = true,
                    CanAbilities = true,
                    CanBehaviorDeck = true,
                    CanSettings = true,
                    CanRestart = true,
                    CanClose = true
                };

                binder.Bind(handle, viewModel);

                Assert.AreEqual("Pause Menu", MainMenuText(root, CharacterTestFairyGuiMainMenuIds.Title));
                Assert.AreEqual("Running  Live Menu  Offline  frame 5", MainMenuText(root, CharacterTestFairyGuiMainMenuIds.Status));
                Assert.AreEqual("Ready.", MainMenuText(root, CharacterTestFairyGuiMainMenuIds.Message));
                Assert.AreEqual("Resume", MainMenuButton(root, CharacterTestFairyGuiMainMenuIds.Start).text);

                MainMenuButton(root, CharacterTestFairyGuiMainMenuIds.Start).onClick.Call();
                Assert.AreEqual(1, sink.Count);
                Assert.AreEqual(CharacterTestFairyGuiMainMenuCommandIds.Start, sink.Last.CommandId);

                MainMenuButton(root, CharacterTestFairyGuiMainMenuIds.Network).onClick.Call();
                Assert.AreEqual(2, sink.Count);
                Assert.AreEqual(CharacterTestFairyGuiMainMenuCommandIds.Network, sink.Last.CommandId);

                MainMenuButton(root, CharacterTestFairyGuiMainMenuIds.Abilities).onClick.Call();
                Assert.AreEqual(3, sink.Count);
                Assert.AreEqual(CharacterTestFairyGuiMainMenuCommandIds.AbilitiesToggle, sink.Last.CommandId);

                MainMenuButton(root, CharacterTestFairyGuiMainMenuIds.BehaviorDeck).onClick.Call();
                Assert.AreEqual(4, sink.Count);
                Assert.AreEqual(CharacterTestFairyGuiMainMenuCommandIds.BehaviorDeckToggle, sink.Last.CommandId);

                MainMenuButton(root, CharacterTestFairyGuiMainMenuIds.Settings).onClick.Call();
                Assert.AreEqual(5, sink.Count);
                Assert.AreEqual(CharacterTestFairyGuiMainMenuCommandIds.SettingsToggle, sink.Last.CommandId);

                MainMenuButton(root, CharacterTestFairyGuiMainMenuIds.Restart).onClick.Call();
                Assert.AreEqual(6, sink.Count);
                Assert.AreEqual(CharacterTestFairyGuiMainMenuCommandIds.RestartOffline, sink.Last.CommandId);

                MainMenuButton(root, CharacterTestFairyGuiMainMenuIds.Close).onClick.Call();
                Assert.AreEqual(7, sink.Count);
                Assert.AreEqual(CharacterTestFairyGuiMainMenuCommandIds.Close, sink.Last.CommandId);
            }
        }

[Test]
        public void FairyGuiMainMenuBinder_UsesStageSizeWithoutPerChildScaling()
        {
            float originalScaleFactor = Fgui.UIContentScaler.scaleFactor;
            try
            {
                Fgui.UIContentScaler.scaleFactor = 0.5f;
                Fgui.GComponent lowScaleRoot = CreateMainMenuRoot();
                lowScaleRoot.SetSize(2560f, 1440f);
                BindMainMenu(lowScaleRoot);
                Fgui.GObject lowScaleFrame =
                    lowScaleRoot.GetChild(CharacterTestFairyGuiMainMenuIds.PanelFrame);
                Assert.AreEqual(2560f, lowScaleRoot.width, 0.0001f);
                Assert.AreEqual(1440f, lowScaleRoot.height, 0.0001f);
                Assert.AreEqual(1f, lowScaleFrame.scaleX, 0.0001f);
                Assert.AreEqual(1f, lowScaleFrame.scaleY, 0.0001f);

                Fgui.UIContentScaler.scaleFactor = 1f;
                Fgui.GComponent defaultScaleRoot = CreateMainMenuRoot();
                defaultScaleRoot.SetSize(1280f, 720f);
                BindMainMenu(defaultScaleRoot);
                Fgui.GObject defaultScaleFrame =
                    defaultScaleRoot.GetChild(CharacterTestFairyGuiMainMenuIds.PanelFrame);
                Assert.AreEqual(1280f, defaultScaleRoot.width, 0.0001f);
                Assert.AreEqual(720f, defaultScaleRoot.height, 0.0001f);
                Assert.AreEqual(1f, defaultScaleFrame.scaleX, 0.0001f);
                Assert.AreEqual(1f, defaultScaleFrame.scaleY, 0.0001f);
            }
            finally
            {
                Fgui.UIContentScaler.scaleFactor = originalScaleFactor;
            }
        }

        private static void BindMainMenu(Fgui.GComponent root)
        {
            var sink = new RecordingUiCommandSink();
            using var handle = new MxFairyGuiComponentHandle(
                CharacterTestFairyGuiMainMenuIds.PackageId,
                CharacterTestFairyGuiMainMenuIds.ComponentName,
                root);
            var binder = new CharacterTestFairyGuiMainMenuBinder(sink);
            binder.Bind(handle, new CharacterTestFairyGuiMainMenuViewModel());
        }

[Test]
        public void FairyGuiAbilityBindingPanelSourcePackage_DeclaresComponentAndBindableControls()
        {
            string packagePath = Path.Combine(
                Directory.GetCurrentDirectory(),
                "FGUIProject/assets/MxCharacterTestAbilityUi/package.xml");
            string panelPath = Path.Combine(
                Directory.GetCurrentDirectory(),
                "FGUIProject/assets/MxCharacterTestAbilityUi/AbilityBindingPanel.xml");

            string packageText = File.ReadAllText(packagePath);
            string panelText = File.ReadAllText(panelPath);

            StringAssert.Contains("AbilityBindingPanel.xml", packageText);
            StringAssert.Contains("CompactNeutralButton.xml", packageText);
            StringAssert.Contains("panel_modal_frame_512x512.png", packageText);
            StringAssert.Contains("<component size=\"1920,1080\"", panelText);
            StringAssert.Contains("name=\"panelFrame\" xy=\"0,0\" size=\"1920,1080\"", panelText);
            AssertAbilityBindingPanelRelationDeclared(panelText, "panelFrame", "width-width,height-height");
            AssertAbilityBindingPanelRelationDeclared(panelText, "abilityBand", "width-width,height-height");
            AssertAbilityBindingPanelRelationDeclared(panelText, CharacterTestFairyGuiAbilityBindingPanelIds.AbilityButton(0), "width-width");
            AssertAbilityBindingPanelRelationDeclared(panelText, CharacterTestFairyGuiAbilityBindingPanelIds.Save, "right-right,bottom-bottom");
            AssertAbilityBindingPanelChildDeclared(panelText, CharacterTestFairyGuiAbilityBindingPanelIds.Title);
            AssertAbilityBindingPanelChildDeclared(panelText, CharacterTestFairyGuiAbilityBindingPanelIds.Status);
            AssertAbilityBindingPanelChildDeclared(panelText, CharacterTestFairyGuiAbilityBindingPanelIds.Message);
            AssertAbilityBindingPanelChildDeclared(panelText, CharacterTestFairyGuiAbilityBindingPanelIds.SelectedSlotLabel);
            for (int slot = 1; slot <= CharacterTestAbilityHotbarRuntime.SlotCount; slot++)
                AssertAbilityBindingPanelChildDeclared(panelText, CharacterTestFairyGuiAbilityBindingPanelIds.SlotButton(slot));
            for (int i = 0; i <= 14; i++)
                AssertAbilityBindingPanelChildDeclared(panelText, CharacterTestFairyGuiAbilityBindingPanelIds.AbilityButton(i));
            StringAssert.Contains("btnAbility14", panelText);
            StringAssert.Contains("Area Confinement", panelText);
            StringAssert.Contains("Channel Beam", panelText);
            StringAssert.Contains("Chain Lightning", panelText);
            StringAssert.Contains("Poison Bottle", panelText);
            AssertAbilityBindingPanelChildDeclared(panelText, CharacterTestFairyGuiAbilityBindingPanelIds.Clear);
            AssertAbilityBindingPanelChildDeclared(panelText, CharacterTestFairyGuiAbilityBindingPanelIds.ResetDefaults);
            AssertAbilityBindingPanelChildDeclared(panelText, CharacterTestFairyGuiAbilityBindingPanelIds.Save);
            AssertAbilityBindingPanelChildDeclared(panelText, CharacterTestFairyGuiAbilityBindingPanelIds.Cancel);
        }

[Test]
        public void FairyGuiArenaSetupPanel_RuntimeContractUsesPlayerInteractiveModalInput()
        {
            MxUiViewContract contract = CharacterTestFairyGuiArenaSetupPanelHost.CreateRuntimeViewContract();

            MxUiContractValidationResult validation = contract.Validate();
            Assert.IsTrue(
                validation.Success,
                validation.Errors.Count > 0 ? validation.Errors[0] : string.Empty);
            Assert.IsTrue(contract.Descriptor.CloseOnSceneChange);
            Assert.IsFalse(contract.Descriptor.KeepAlive);
            Assert.IsTrue(contract.Descriptor.Modal);
            Assert.AreEqual(MxUiLayer.Modal, contract.Descriptor.Layer);
        }

[Test]
        public void FairyGuiArenaSetupPanelSourcePackage_DeclaresComponentAndStableControls()
        {
            string packagePath = Path.Combine(
                Directory.GetCurrentDirectory(),
                "FGUIProject/assets/MxCharacterTestHud/package.xml");
            string panelPath = Path.Combine(
                Directory.GetCurrentDirectory(),
                "FGUIProject/assets/MxCharacterTestHud/ArenaSetupPanel.xml");

            string packageText = File.ReadAllText(packagePath);
            string panelText = File.ReadAllText(panelPath);

            StringAssert.Contains("ArenaSetupPanel.xml", packageText);
            StringAssert.Contains("<component size=\"1920,1080\"", panelText);
            StringAssert.Contains("Components/SettingsPanelToggleButton.xml", panelText);
            StringAssert.Contains("Components/SettingsPanelMiniButton.xml", panelText);
            StringAssert.Contains("team1Slot0Frame", panelText);
            StringAssert.Contains("team2Slot0Frame", panelText);
            Assert.IsFalse(panelText.Contains("Character &lt;", StringComparison.Ordinal));
            Assert.IsFalse(panelText.Contains("Strategy &lt;", StringComparison.Ordinal));
            Assert.IsFalse(panelText.Contains("Variant &lt;", StringComparison.Ordinal));
            AssertFairyGuiArenaPanelChildDeclared(panelText, CharacterTestFairyGuiArenaSetupPanelIds.Title);
            AssertFairyGuiArenaPanelChildDeclared(panelText, CharacterTestFairyGuiArenaSetupPanelIds.Status);
            AssertFairyGuiArenaPanelChildDeclared(panelText, CharacterTestFairyGuiArenaSetupPanelIds.Message);
            AssertFairyGuiArenaPanelChildDeclared(panelText, CharacterTestFairyGuiArenaSetupPanelIds.Team1Header);
            AssertFairyGuiArenaPanelChildDeclared(panelText, CharacterTestFairyGuiArenaSetupPanelIds.Team2Header);
            AssertFairyGuiArenaPanelChildDeclared(panelText, CharacterTestFairyGuiArenaSetupPanelIds.SummaryText);
            AssertFairyGuiArenaPanelChildDeclared(panelText, CharacterTestFairyGuiArenaSetupPanelIds.BattleStatusText);
            AssertFairyGuiArenaPanelChildDeclared(panelText, CharacterTestFairyGuiArenaSetupPanelIds.CameraModeText);
            AssertFairyGuiArenaPanelChildDeclared(panelText, CharacterTestFairyGuiArenaSetupPanelIds.CameraModeHint);
            AssertFairyGuiArenaPanelChildDeclared(panelText, CharacterTestFairyGuiArenaSetupPanelIds.CameraThirdPerson);
            AssertFairyGuiArenaPanelChildDeclared(panelText, CharacterTestFairyGuiArenaSetupPanelIds.CameraAuto);
            AssertFairyGuiArenaPanelChildDeclared(panelText, CharacterTestFairyGuiArenaSetupPanelIds.Start);
            AssertFairyGuiArenaPanelChildDeclared(panelText, CharacterTestFairyGuiArenaSetupPanelIds.Replay);
            AssertFairyGuiArenaPanelChildDeclared(panelText, CharacterTestFairyGuiArenaSetupPanelIds.Reconfigure);
            for (int i = 0; i < CharacterTestArenaMatchConfig.SlotsPerTeam; i++)
            {
                AssertFairyGuiArenaPanelChildDeclared(
                    panelText,
                    CharacterTestFairyGuiArenaSetupPanelIds.SlotToggle(
                        CharacterTestArenaMatchConfig.Team1Id,
                        i));
                AssertFairyGuiArenaPanelChildDeclared(
                    panelText,
                    CharacterTestFairyGuiArenaSetupPanelIds.SlotSummary(
                        CharacterTestArenaMatchConfig.Team1Id,
                        i));
                AssertFairyGuiArenaPanelChildDeclared(
                    panelText,
                    CharacterTestFairyGuiArenaSetupPanelIds.CharacterLabel(
                        CharacterTestArenaMatchConfig.Team1Id,
                        i));
                AssertFairyGuiArenaPanelChildDeclared(
                    panelText,
                    CharacterTestFairyGuiArenaSetupPanelIds.StrategyNext(
                        CharacterTestArenaMatchConfig.Team2Id,
                        i));
                AssertFairyGuiArenaPanelChildDeclared(
                    panelText,
                    CharacterTestFairyGuiArenaSetupPanelIds.StrategyLabel(
                        CharacterTestArenaMatchConfig.Team2Id,
                        i));
                AssertFairyGuiArenaPanelChildDeclared(
                    panelText,
                    CharacterTestFairyGuiArenaSetupPanelIds.VariantPrev(
                        CharacterTestArenaMatchConfig.Team2Id,
                        i));
                AssertFairyGuiArenaPanelChildDeclared(
                    panelText,
                    CharacterTestFairyGuiArenaSetupPanelIds.VariantLabel(
                        CharacterTestArenaMatchConfig.Team2Id,
                        i));
                AssertFairyGuiArenaPanelChildDeclared(
                    panelText,
                    CharacterTestFairyGuiArenaSetupPanelIds.EditGroup(
                        CharacterTestArenaMatchConfig.Team2Id,
                        i));
            }
        }

[Test]
        public void FairyGuiArenaBehaviorGroupPanelSourcePackage_DeclaresComponentAndStableControls()
        {
            string packagePath = Path.Combine(
                Directory.GetCurrentDirectory(),
                "FGUIProject/assets/MxCharacterTestHud/package.xml");
            string panelPath = Path.Combine(
                Directory.GetCurrentDirectory(),
                "FGUIProject/assets/MxCharacterTestHud/ArenaBehaviorGroupPanel.xml");

            string packageText = File.ReadAllText(packagePath);
            string panelText = File.ReadAllText(panelPath);

            StringAssert.Contains("ArenaBehaviorGroupPanel.xml", packageText);
            StringAssert.Contains("<component size=\"1920,1080\"", panelText);
            StringAssert.Contains("Components/SettingsPanelActionButton.xml", panelText);
            StringAssert.Contains("Components/SettingsPanelMiniButton.xml", panelText);
            AssertFairyGuiArenaPanelChildDeclared(
                panelText,
                CharacterTestFairyGuiArenaBehaviorGroupPanelIds.Title);
            AssertFairyGuiArenaPanelChildDeclared(
                panelText,
                CharacterTestFairyGuiArenaBehaviorGroupPanelIds.Back);
            AssertFairyGuiArenaPanelChildDeclared(
                panelText,
                CharacterTestFairyGuiArenaBehaviorGroupPanelIds.DraftLabel);
            AssertFairyGuiArenaPanelChildDeclared(
                panelText,
                CharacterTestFairyGuiArenaBehaviorGroupPanelIds.CoverageHeadline);
            AssertFairyGuiArenaPanelChildDeclared(
                panelText,
                CharacterTestFairyGuiArenaBehaviorGroupPanelIds.CoverageSummary);
            AssertFairyGuiArenaPanelChildDeclared(
                panelText,
                CharacterTestFairyGuiArenaBehaviorGroupPanelIds.CoverageRequired);
            AssertFairyGuiArenaPanelChildDeclared(
                panelText,
                CharacterTestFairyGuiArenaBehaviorGroupPanelIds.CoverageRisk);
            AssertFairyGuiArenaPanelChildDeclared(
                panelText,
                CharacterTestFairyGuiArenaBehaviorGroupPanelIds.SearchInput);
            AssertFairyGuiArenaPanelChildDeclared(
                panelText,
                CharacterTestFairyGuiArenaBehaviorGroupPanelIds.SearchButton);
            AssertFairyGuiArenaPanelChildDeclared(
                panelText,
                CharacterTestFairyGuiArenaBehaviorGroupPanelIds.ClearSearch);
            AssertFairyGuiArenaPanelChildDeclared(
                panelText,
                CharacterTestFairyGuiArenaBehaviorGroupPanelIds.FillBasic);
            AssertFairyGuiArenaPanelChildDeclared(
                panelText,
                CharacterTestFairyGuiArenaBehaviorGroupPanelIds.LibrarySummary);
            AssertFairyGuiArenaPanelChildDeclared(
                panelText,
                CharacterTestFairyGuiArenaBehaviorGroupPanelIds.EntrySummary);
            AssertFairyGuiArenaPanelChildDeclared(
                panelText,
                CharacterTestFairyGuiArenaBehaviorGroupPanelIds.InspectorText);
            AssertFairyGuiArenaPanelChildDeclared(
                panelText,
                CharacterTestFairyGuiArenaBehaviorGroupPanelIds.SelectedCardDetail);
            AssertFairyGuiArenaPanelChildDeclared(
                panelText,
                CharacterTestFairyGuiArenaBehaviorGroupPanelIds.DecisionText);
            AssertFairyGuiArenaPanelChildDeclared(
                panelText,
                CharacterTestFairyGuiArenaBehaviorGroupPanelIds.DiagnosticsText);
            for (int i = 0; i < CharacterTestFairyGuiArenaBehaviorGroupPanelIds.CapabilitySlotCount; i++)
            {
                AssertFairyGuiArenaPanelChildDeclared(
                    panelText,
                    CharacterTestFairyGuiArenaBehaviorGroupPanelIds.CapabilitySlot(i));
                AssertFairyGuiArenaPanelChildDeclared(
                    panelText,
                    CharacterTestFairyGuiArenaBehaviorGroupPanelIds.CapabilitySlotName(i));
                AssertFairyGuiArenaPanelChildDeclared(
                    panelText,
                    CharacterTestFairyGuiArenaBehaviorGroupPanelIds.CapabilitySlotRole(i));
                AssertFairyGuiArenaPanelChildDeclared(
                    panelText,
                    CharacterTestFairyGuiArenaBehaviorGroupPanelIds.CapabilitySlotState(i));
                AssertFairyGuiArenaPanelChildDeclared(
                    panelText,
                    CharacterTestFairyGuiArenaBehaviorGroupPanelIds.CapabilitySlotHint(i));
            }

            for (int i = 0; i < CharacterTestFairyGuiArenaBehaviorGroupPanelIds.LibraryRowCount; i++)
            {
                AssertFairyGuiArenaPanelChildDeclared(
                    panelText,
                    CharacterTestFairyGuiArenaBehaviorGroupPanelIds.LibraryRow(i));
                AssertFairyGuiArenaPanelChildDeclared(
                    panelText,
                    CharacterTestFairyGuiArenaBehaviorGroupPanelIds.LibraryRowTitle(i));
                AssertFairyGuiArenaPanelChildDeclared(
                    panelText,
                    CharacterTestFairyGuiArenaBehaviorGroupPanelIds.LibraryRowIntent(i));
                AssertFairyGuiArenaPanelChildDeclared(
                    panelText,
                    CharacterTestFairyGuiArenaBehaviorGroupPanelIds.LibraryRowTrigger(i));
                AssertFairyGuiArenaPanelChildDeclared(
                    panelText,
                    CharacterTestFairyGuiArenaBehaviorGroupPanelIds.LibraryRowOutput(i));
                AssertFairyGuiArenaPanelChildDeclared(
                    panelText,
                    CharacterTestFairyGuiArenaBehaviorGroupPanelIds.LibraryRowBadges(i));
                AssertFairyGuiArenaPanelChildDeclared(
                    panelText,
                    CharacterTestFairyGuiArenaBehaviorGroupPanelIds.LibraryRowRisk(i));
            }

            for (int i = 0; i < CharacterTestFairyGuiArenaBehaviorGroupPanelIds.EntryRowCount; i++)
            {
                AssertFairyGuiArenaPanelChildDeclared(
                    panelText,
                    CharacterTestFairyGuiArenaBehaviorGroupPanelIds.EntryRow(i));
                AssertFairyGuiArenaPanelChildDeclared(
                    panelText,
                    CharacterTestFairyGuiArenaBehaviorGroupPanelIds.EntryRowOrder(i));
                AssertFairyGuiArenaPanelChildDeclared(
                    panelText,
                    CharacterTestFairyGuiArenaBehaviorGroupPanelIds.EntryRowTitle(i));
                AssertFairyGuiArenaPanelChildDeclared(
                    panelText,
                    CharacterTestFairyGuiArenaBehaviorGroupPanelIds.EntryRowOutput(i));
                AssertFairyGuiArenaPanelChildDeclared(
                    panelText,
                    CharacterTestFairyGuiArenaBehaviorGroupPanelIds.EntryRowForecast(i));
                AssertFairyGuiArenaPanelChildDeclared(
                    panelText,
                    CharacterTestFairyGuiArenaBehaviorGroupPanelIds.EntryRowState(i));
            }

            for (int i = 0; i < CharacterTestFairyGuiArenaBehaviorGroupPanelIds.ForecastRowCount; i++)
            {
                AssertFairyGuiArenaPanelChildDeclared(
                    panelText,
                    CharacterTestFairyGuiArenaBehaviorGroupPanelIds.ForecastRow(i));
                AssertFairyGuiArenaPanelChildDeclared(
                    panelText,
                    CharacterTestFairyGuiArenaBehaviorGroupPanelIds.ForecastScenario(i));
                AssertFairyGuiArenaPanelChildDeclared(
                    panelText,
                    CharacterTestFairyGuiArenaBehaviorGroupPanelIds.ForecastCard(i));
                AssertFairyGuiArenaPanelChildDeclared(
                    panelText,
                    CharacterTestFairyGuiArenaBehaviorGroupPanelIds.ForecastOutput(i));
                AssertFairyGuiArenaPanelChildDeclared(
                    panelText,
                    CharacterTestFairyGuiArenaBehaviorGroupPanelIds.ForecastRisk(i));
            }

            for (int i = 0; i < CharacterTestFairyGuiArenaBehaviorGroupPanelIds.ParameterRowCount; i++)
            {
                AssertFairyGuiArenaPanelChildDeclared(
                    panelText,
                    CharacterTestFairyGuiArenaBehaviorGroupPanelIds.ParameterLabel(i));
                AssertFairyGuiArenaPanelChildDeclared(
                    panelText,
                    CharacterTestFairyGuiArenaBehaviorGroupPanelIds.ParameterPrev(i));
                AssertFairyGuiArenaPanelChildDeclared(
                    panelText,
                    CharacterTestFairyGuiArenaBehaviorGroupPanelIds.ParameterNext(i));
            }
        }

[Test]
        public void FairyGuiArenaSetupPanelManifest_DeclaresUniqueCommands()
        {
            MxUiViewContract contract =
                CharacterTestFairyGuiArenaSetupPanelManifest.CreateViewContract();

            MxUiContractValidationResult validation = contract.Validate();
            Assert.IsTrue(
                validation.Success,
                validation.Errors.Count > 0 ? validation.Errors[0] : string.Empty);
            Assert.AreEqual(
                contract.Commands.Count,
                contract.Commands.Select(command => command.CommandId).Distinct().Count());
            Assert.IsTrue(contract.Commands.Any(
                command => command.CommandId.StartsWith(
                    CharacterTestFairyGuiArenaSetupPanelCommandIds.ToggleSlot + ".team",
                    StringComparison.Ordinal)));
            Assert.IsTrue(contract.Commands.Any(
                command => command.CommandId.StartsWith(
                    CharacterTestFairyGuiArenaSetupPanelCommandIds.EditGroup + ".team",
                    StringComparison.Ordinal)));
            Assert.IsTrue(contract.Commands.Any(
                command => command.CommandId == CharacterTestFairyGuiArenaSetupPanelCommandIds.CameraThirdPerson));
            Assert.IsTrue(contract.Commands.Any(
                command => command.CommandId == CharacterTestFairyGuiArenaSetupPanelCommandIds.CameraAuto));
        }

[Test]
        public void FairyGuiArenaBehaviorGroupPanelManifest_DeclaresUniqueCommands()
        {
            MxUiViewContract contract =
                CharacterTestFairyGuiArenaBehaviorGroupPanelManifest.CreateViewContract();

            MxUiContractValidationResult validation = contract.Validate();
            Assert.IsTrue(
                validation.Success,
                validation.Errors.Count > 0 ? validation.Errors[0] : string.Empty);
            Assert.AreEqual(
                contract.Commands.Count,
                contract.Commands.Select(command => command.CommandId).Distinct().Count());
            Assert.IsTrue(contract.Commands.Any(
                command => command.CommandId.StartsWith(
                    CharacterTestFairyGuiArenaBehaviorGroupPanelCommandIds.SelectLibraryRow + ".row",
                    StringComparison.Ordinal)));
            Assert.IsTrue(contract.Commands.Any(
                command => command.CommandId.StartsWith(
                    CharacterTestFairyGuiArenaBehaviorGroupPanelCommandIds.SelectCapabilitySlot + ".row",
                    StringComparison.Ordinal)));
            Assert.IsTrue(contract.Commands.Any(
                command => command.CommandId == CharacterTestFairyGuiArenaBehaviorGroupPanelCommandIds.ApplySearch));
            Assert.IsTrue(contract.Commands.Any(
                command => command.CommandId == CharacterTestFairyGuiArenaBehaviorGroupPanelCommandIds.ClearSearch));
            Assert.IsTrue(contract.Commands.Any(
                command => command.CommandId == CharacterTestFairyGuiArenaBehaviorGroupPanelCommandIds.FillBasic));
        }

[Test]
        public void FairyGuiAbilityBindingPanelBinder_BindsLabelsAndCommands()
        {
            Fgui.GComponent root = CreateAbilityBindingPanelRoot();
            Fgui.GComponent layerRoot = new Fgui.GComponent();
            layerRoot.SetSize(1600f, 900f);
            layerRoot.AddChild(root);
            var sink = new RecordingUiCommandSink();
            var hotbar = new CharacterTestAbilityHotbarRuntime(
                CharacterTestAbilityCatalogSource.CreateRegistry());
            CharacterTestFairyGuiAbilityBindingPanelViewModel viewModel =
                CharacterTestFairyGuiAbilityBindingPanelViewModelBuilder.Build(
                    hotbar,
                    hotbar.CreateBindingSnapshot(),
                    selectedSlot: 2,
                    dirty: true);
            using (var handle = new MxFairyGuiComponentHandle(
                CharacterTestFairyGuiAbilityBindingPanelIds.PackageId,
                CharacterTestFairyGuiAbilityBindingPanelIds.ComponentName,
                root))
            {
                var binder = new CharacterTestFairyGuiAbilityBindingPanelBinder(
                    sink,
                    CharacterTestFairyGuiAbilityBindingPanelIds.ViewId);

                binder.Bind(handle, viewModel);

                Assert.AreEqual(1600f, root.width);
                Assert.AreEqual(900f, root.height);
                Assert.AreEqual("Abilities", AbilityPanelText(root, CharacterTestFairyGuiAbilityBindingPanelIds.Title));
                Assert.AreEqual("Unsaved changes.", AbilityPanelText(root, CharacterTestFairyGuiAbilityBindingPanelIds.Status));
                Assert.AreEqual("Selected slot 2", AbilityPanelText(root, CharacterTestFairyGuiAbilityBindingPanelIds.SelectedSlotLabel));
                StringAssert.Contains("2", AbilityPanelButton(root, CharacterTestFairyGuiAbilityBindingPanelIds.SlotButton(2)).text);
                StringAssert.StartsWith("> ", AbilityPanelButton(root, CharacterTestFairyGuiAbilityBindingPanelIds.SlotButton(2)).text);
                StringAssert.Contains("Bound", AbilityPanelButton(root, CharacterTestFairyGuiAbilityBindingPanelIds.AbilityButton(0)).text);
                Assert.IsFalse(AbilityPanelButton(root, CharacterTestFairyGuiAbilityBindingPanelIds.AbilityButton(12)).visible);

                AbilityPanelButton(root, CharacterTestFairyGuiAbilityBindingPanelIds.SlotButton(3)).onClick.Call();
                Assert.AreEqual(1, sink.Count);
                Assert.AreEqual(CharacterTestFairyGuiAbilityBindingPanelCommandIds.SelectSlot, sink.Last.CommandId);
                var slotPayload = (CharacterTestFairyGuiAbilityBindingPanelPayload)sink.Last.Payload;
                Assert.AreEqual(3, slotPayload.SlotNumber);

                AbilityPanelButton(root, CharacterTestFairyGuiAbilityBindingPanelIds.AbilityButton(2)).onClick.Call();
                Assert.AreEqual(2, sink.Count);
                Assert.AreEqual(CharacterTestFairyGuiAbilityBindingPanelCommandIds.SelectAbility, sink.Last.CommandId);
                var abilityPayload = (CharacterTestFairyGuiAbilityBindingPanelPayload)sink.Last.Payload;
                Assert.AreEqual(CharacterTestAbilityIds.FlameLanceAbilityId, abilityPayload.AbilityId);

                AbilityPanelButton(root, CharacterTestFairyGuiAbilityBindingPanelIds.Save).onClick.Call();
                Assert.AreEqual(3, sink.Count);
                Assert.AreEqual(CharacterTestFairyGuiAbilityBindingPanelCommandIds.Save, sink.Last.CommandId);

                AbilityPanelButton(root, CharacterTestFairyGuiAbilityBindingPanelIds.Cancel).onClick.Call();
                Assert.AreEqual(4, sink.Count);
                Assert.AreEqual(CharacterTestFairyGuiAbilityBindingPanelCommandIds.Cancel, sink.Last.CommandId);
            }
        }

[Test]
        public void FairyGuiAbilityBindingPanelCommandSink_SavesWorkingBindingsAndCancelsThroughCloseCallback()
        {
            string directory = Path.Combine(
                Path.GetTempPath(),
                "MxFramework.CharacterTestAbilityPanelTests",
                Guid.NewGuid().ToString("N"));
            string path = Path.Combine(directory, "ability_hotbar.json");
            try
            {
                var hotbar = new CharacterTestAbilityHotbarRuntime(
                    CharacterTestAbilityCatalogSource.CreateRegistry(),
                    path);
                int closeCount = 0;
                var sink = new CharacterTestFairyGuiAbilityBindingPanelCommandSink(
                    hotbar,
                    () =>
                    {
                        closeCount++;
                        return true;
                    });

                sink.Enqueue(new MxUiCommand(
                    CharacterTestFairyGuiAbilityBindingPanelIds.ViewId,
                    CharacterTestFairyGuiAbilityBindingPanelCommandIds.SelectSlot,
                    new CharacterTestFairyGuiAbilityBindingPanelPayload { SlotNumber = 9 }));
                sink.Enqueue(new MxUiCommand(
                    CharacterTestFairyGuiAbilityBindingPanelIds.ViewId,
                    CharacterTestFairyGuiAbilityBindingPanelCommandIds.SelectAbility,
                    new CharacterTestFairyGuiAbilityBindingPanelPayload
                    {
                        AbilityId = CharacterTestAbilityIds.FireballAbilityId
                    }));

                Assert.IsTrue(sink.Dirty);
                Assert.IsTrue(hotbar.TryGetAbilityForSlot(9, out AbilityDefinitionConfig beforeSaveAbility));
                Assert.AreEqual(CharacterTestAbilityIds.FireballExplosionAbilityId, beforeSaveAbility.AbilityId);

                sink.Enqueue(new MxUiCommand(
                    CharacterTestFairyGuiAbilityBindingPanelIds.ViewId,
                    CharacterTestFairyGuiAbilityBindingPanelCommandIds.Save,
                    null));

                Assert.AreEqual(1, sink.SaveCount);
                Assert.IsFalse(sink.Dirty);
                Assert.IsTrue(File.Exists(path));
                Assert.IsTrue(hotbar.TryGetAbilityForSlot(9, out AbilityDefinitionConfig ability));
                Assert.AreEqual(CharacterTestAbilityIds.FireballAbilityId, ability.AbilityId);

                sink.Enqueue(new MxUiCommand(
                    CharacterTestFairyGuiAbilityBindingPanelIds.ViewId,
                    CharacterTestFairyGuiAbilityBindingPanelCommandIds.Cancel,
                    null));

                Assert.AreEqual(1, sink.CancelCount);
                Assert.AreEqual(1, closeCount);
            }
            finally
            {
                if (Directory.Exists(directory))
                    Directory.Delete(directory, recursive: true);
            }
        }

[Test]
        public void FairyGuiNetworkPanelBuilder_UsesControllerDefaultsAndOfflineStatus()
        {
            var controller = new RecordingNetworkSessionController();
            controller.Status = CharacterTestNetworkSessionStatus.Offline("Offline");

            CharacterTestFairyGuiNetworkPanelViewModel viewModel =
                CharacterTestFairyGuiNetworkPanelViewModelBuilder.Build(controller);

            Assert.AreEqual(CharacterTestNetworkSessionKind.Offline, viewModel.Status.Kind);
            Assert.IsTrue(viewModel.CanHost);
            Assert.IsTrue(viewModel.CanJoin);
            Assert.IsFalse(viewModel.CanDisconnect);
            Assert.IsTrue(viewModel.CanClose);
            Assert.AreEqual("Offline", viewModel.StatusText);
            Assert.AreEqual(CharacterTestNetworkLaunchMode.Room, viewModel.SelectedLaunchMode);
            Assert.IsTrue(viewModel.CanSelectLaunchMode);
            Assert.AreEqual("0.0.0.0", viewModel.HostOptions.BindAddress);
            Assert.AreEqual("10.1.2.3", viewModel.JoinOptions.RemoteAddress);
            Assert.AreEqual(9911, viewModel.JoinOptions.Port);
            Assert.AreEqual(CharacterTestNetworkLaunchMode.Room, viewModel.HostOptions.LaunchMode);
            Assert.AreEqual("CharacterTest LAN Room", viewModel.HostOptions.RoomName);
            Assert.AreEqual("Player", viewModel.JoinOptions.DisplayName);
            Assert.IsFalse(viewModel.HostOptions.RoomClientAutoReady);
            Assert.IsFalse(viewModel.HostOptions.RoomServerAutoStartWhenReady);
        }

[Test]
        public void FairyGuiNetworkPanelBuilder_UsesSavedPreferencesWithoutPersistingPeerIds()
        {
            CharacterTestNetworkPanelPreferences.Clear();
            try
            {
                CharacterTestNetworkPanelPreferences.Save(new CharacterTestNetworkSessionOptions
                {
                    Kind = CharacterTestNetworkSessionKind.Join,
                    LaunchMode = CharacterTestNetworkLaunchMode.Room,
                    RemoteAddress = "192.168.1.201",
                    Port = 7788,
                    ServerPeerId = "server",
                    ClientPeerId = "saved-client-should-not-load",
                    RoomId = "duel-room",
                    RoomName = "duel-room",
                    DisplayName = "Client Beta"
                });
                CharacterTestNetworkPanelPreferences.SaveAutoSyncProbeAfterEnterGame(true);

                var controller = new RecordingNetworkSessionController();
                controller.Status = CharacterTestNetworkSessionStatus.Offline("Offline");

                CharacterTestFairyGuiNetworkPanelViewModel viewModel =
                    CharacterTestFairyGuiNetworkPanelViewModelBuilder.Build(
                        controller,
                        selectedLaunchMode: CharacterTestNetworkLaunchMode.DirectLan,
                        useSavedPreferences: true);

                Assert.AreEqual(CharacterTestNetworkLaunchMode.Room, viewModel.SelectedLaunchMode);
                Assert.AreEqual("192.168.1.201", viewModel.JoinOptions.RemoteAddress);
                Assert.AreEqual(7788, viewModel.JoinOptions.Port);
                Assert.AreEqual("duel-room", viewModel.JoinOptions.RoomName);
                Assert.AreEqual("duel-room", viewModel.JoinOptions.RoomId);
                Assert.AreEqual("Client Beta", viewModel.JoinOptions.DisplayName);
                Assert.AreEqual("client-0", viewModel.JoinOptions.ClientPeerId);
                Assert.IsTrue(viewModel.AutoSyncProbeAfterEnterGame);
                Assert.AreEqual("Auto Probe On", viewModel.AutoSyncProbeButtonText);
            }
            finally
            {
                CharacterTestNetworkPanelPreferences.Clear();
            }
        }

[Test]
        public void FairyGuiNetworkPanelBuilder_MapsRoomStatusAndDiagnostics()
        {
            var controller = new RecordingNetworkSessionController();
            controller.Status = new CharacterTestNetworkSessionStatus
            {
                Kind = CharacterTestNetworkSessionKind.Host,
                LaunchMode = CharacterTestNetworkLaunchMode.Room,
                NetworkMode = CharacterTestNetworkMode.UnityTransportRoomServer,
                BindAddress = "0.0.0.0",
                Port = 9911,
                ServerPeerId = "server",
                ClientPeerId = "client-0",
                TransportMode = "utp",
                TransportId = "server:9911",
                ServerStatus = NetworkSessionStatus.Connected,
                RoomStatus = NetworkRoomStatus.Open,
                RoomId = "duel-room",
                RoomName = "Duel Room",
                RoomMembers = "0:server[ready][on],1:client[wait][on]",
                RoomReadyCount = 1,
                RoomConnectedCount = 2,
                RoomStateVersion = 7,
                RoomReason = "waiting",
                RttEstimateFrames = 3,
                AverageRttFrames = 4,
                FrameDriftFrames = 1,
                ResyncCount = 2,
                SnapshotMismatchCount = 1,
                SnapshotRequestCount = 3
            };

            CharacterTestFairyGuiNetworkPanelViewModel viewModel =
                CharacterTestFairyGuiNetworkPanelViewModelBuilder.Build(
                    controller,
                    selectedLaunchMode: CharacterTestNetworkLaunchMode.Room);

            Assert.AreEqual(CharacterTestNetworkLaunchMode.Room, viewModel.SelectedLaunchMode);
            Assert.IsFalse(viewModel.CanHost);
            Assert.IsFalse(viewModel.CanJoin);
            Assert.IsTrue(viewModel.CanDisconnect);
            Assert.IsFalse(viewModel.CanClose);
            Assert.AreEqual("Room Open", viewModel.StatusText);
            StringAssert.Contains("Duel Room", viewModel.RoomSummaryText);
            StringAssert.Contains("ready 1/2", viewModel.RoomSummaryText);
            StringAssert.Contains("0:server", viewModel.MemberSummaryText);
            StringAssert.Contains("Readiness", viewModel.ReadinessSummaryText);
            StringAssert.Contains("utp", viewModel.DetailTransportText);
            StringAssert.Contains("RTT: 3f avg 4f", viewModel.DetailRttText);
            StringAssert.Contains("resync 2", viewModel.DetailSnapshotText);
        }

[Test]
        public void FairyGuiNetworkPanelBuilder_OnlyRoomHostCanRequestEnterGame()
        {
            var controller = new RecordingNetworkSessionController();
            controller.Status = new CharacterTestNetworkSessionStatus
            {
                Kind = CharacterTestNetworkSessionKind.Host,
                LaunchMode = CharacterTestNetworkLaunchMode.Room,
                NetworkMode = CharacterTestNetworkMode.UnityTransportRoomHost,
                RoomStatus = NetworkRoomStatus.Open,
                RoomReadyCount = 2,
                RoomConnectedCount = 2,
                IsRoomHost = true,
                LocalRoomReady = true,
                CanStartRoomGame = true,
                CanRequestEnterGameplay = true
            };

            CharacterTestFairyGuiNetworkPanelViewModel hostViewModel =
                CharacterTestFairyGuiNetworkPanelViewModelBuilder.Build(controller);

            Assert.IsTrue(hostViewModel.CanEnterGame);
            Assert.AreEqual("Enter Game", hostViewModel.EnterGameButtonText);

            controller.Status = new CharacterTestNetworkSessionStatus
            {
                Kind = CharacterTestNetworkSessionKind.Join,
                LaunchMode = CharacterTestNetworkLaunchMode.Room,
                NetworkMode = CharacterTestNetworkMode.UnityTransportRoomClient,
                RoomStatus = NetworkRoomStatus.Open,
                RoomReadyCount = 2,
                RoomConnectedCount = 2,
                IsRoomHost = false,
                LocalRoomReady = true,
                CanRequestEnterGameplay = false
            };

            CharacterTestFairyGuiNetworkPanelViewModel clientViewModel =
                CharacterTestFairyGuiNetworkPanelViewModelBuilder.Build(controller);

            Assert.IsFalse(clientViewModel.CanEnterGame);
            Assert.AreEqual("Waiting Host", clientViewModel.EnterGameButtonText);

            controller.Status = new CharacterTestNetworkSessionStatus
            {
                Kind = CharacterTestNetworkSessionKind.Join,
                LaunchMode = CharacterTestNetworkLaunchMode.DirectLan,
                NetworkMode = CharacterTestNetworkMode.UnityTransportClient,
                ClientStatus = NetworkSessionStatus.Connected,
                OwnedEntityId = 10,
                FullSnapshotReceivedCount = 1,
                CanEnterGameplay = true,
                CanRequestEnterGameplay = true
            };

            CharacterTestFairyGuiNetworkPanelViewModel directLanViewModel =
                CharacterTestFairyGuiNetworkPanelViewModelBuilder.Build(controller);

            Assert.IsTrue(directLanViewModel.CanEnterGame);
            Assert.AreEqual("Enter Game", directLanViewModel.EnterGameButtonText);
        }

[Test]
        public void FairyGuiNetworkPanelBuilder_ExposesSyncProbeStatus()
        {
            var controller = new RecordingNetworkSessionController();
            controller.Status = new CharacterTestNetworkSessionStatus
            {
                Kind = CharacterTestNetworkSessionKind.Join,
                LaunchMode = CharacterTestNetworkLaunchMode.DirectLan,
                NetworkMode = CharacterTestNetworkMode.UnityTransportClient,
                ClientStatus = NetworkSessionStatus.Connected,
                OwnedEntityId = 42,
                FullSnapshotReceivedCount = 1,
                CanEnterGameplay = true,
                CanRequestEnterGameplay = true
            };
            controller.ProbeStatus = new CharacterTestNetworkSyncProbeStatus
            {
                Result = CharacterTestNetworkSyncProbeResult.Running,
                IsRunning = true,
                CanStart = true,
                AutoRunEnabled = true,
                AutoRunPending = true,
                ResultCode = "RUNNING",
                Summary = "Sync Probe RUNNING: 12/900 frames",
                Detail = "auth recv=4 echo=4"
            };

            CharacterTestFairyGuiNetworkPanelViewModel viewModel =
                CharacterTestFairyGuiNetworkPanelViewModelBuilder.Build(controller);

            Assert.IsTrue(viewModel.CanRunSyncProbe);
            Assert.IsTrue(viewModel.SyncProbeRunning);
            Assert.IsTrue(viewModel.AutoSyncProbeAfterEnterGame);
            Assert.IsTrue(viewModel.AutoSyncProbePending);
            Assert.AreEqual("Auto Probe Pending", viewModel.AutoSyncProbeButtonText);
            Assert.AreEqual("Stop Probe", viewModel.RunSyncProbeButtonText);
            StringAssert.Contains("Sync Probe RUNNING", viewModel.SyncProbeSummaryText);
            StringAssert.Contains("auth recv=4", viewModel.SyncProbeSummaryText);
        }

[Test]
        public void FairyGuiNetworkPanelBuilder_ExposesReplayStatus()
        {
            var controller = new RecordingNetworkSessionController();
            controller.Status = new CharacterTestNetworkSessionStatus
            {
                Kind = CharacterTestNetworkSessionKind.Host,
                LaunchMode = CharacterTestNetworkLaunchMode.Room,
                NetworkMode = CharacterTestNetworkMode.UnityTransportRoomHost,
                ServerStatus = NetworkSessionStatus.Connected,
                ClientStatus = NetworkSessionStatus.Connected,
                IsRoomHost = true
            };

            CharacterTestFairyGuiNetworkPanelViewModel idle =
                CharacterTestFairyGuiNetworkPanelViewModelBuilder.Build(controller);

            Assert.AreEqual("Replay Check: not run", idle.ReplaySummaryText);
            Assert.AreEqual("idle", idle.ReplayTone);

            controller.ReplayStatus = new CharacterTestReplayPlaybackStatus
            {
                Available = true,
                Recording = true,
                ResultCode = "RECORDING",
                Summary = "CharacterTest replay recording started.",
                FrameCount = 12,
                AuthoritativeCommandCount = 3
            };
            CharacterTestFairyGuiNetworkPanelViewModel recording =
                CharacterTestFairyGuiNetworkPanelViewModelBuilder.Build(controller);

            Assert.AreEqual("Replay Check: recording authoritative commands", recording.ReplaySummaryText);
            Assert.AreEqual("recording", recording.ReplayTone);

            controller.ReplayStatus = new CharacterTestReplayPlaybackStatus
            {
                Available = true,
                PlaybackRan = true,
                Success = true,
                ResultCode = "PASS",
                Summary = "CharacterTest replay PASS.",
                FrameCount = 300,
                AuthoritativeCommandCount = 46
            };
            CharacterTestFairyGuiNetworkPanelViewModel pass =
                CharacterTestFairyGuiNetworkPanelViewModelBuilder.Build(controller);

            Assert.AreEqual("Replay Check: PASS frames=300 auth=46", pass.ReplaySummaryText);
            Assert.AreEqual("pass", pass.ReplayTone);

            controller.ReplayStatus = new CharacterTestReplayPlaybackStatus
            {
                Available = true,
                PlaybackRan = true,
                Success = false,
                ResultCode = "HashMismatch",
                Summary = "CharacterTest replay FAIL.",
                FrameCount = 300,
                AuthoritativeCommandCount = 46,
                FailureFrame = new RuntimeFrame(128)
            };
            CharacterTestFairyGuiNetworkPanelViewModel fail =
                CharacterTestFairyGuiNetworkPanelViewModelBuilder.Build(controller);

            Assert.AreEqual("Replay Check: FAIL HashMismatch frame=128", fail.ReplaySummaryText);
            Assert.AreEqual("fail", fail.ReplayTone);

            controller.Status.Kind = CharacterTestNetworkSessionKind.Join;
            controller.Status.NetworkMode = CharacterTestNetworkMode.UnityTransportRoomClient;
            controller.Status.IsRoomHost = false;
            controller.ReplayStatus =
                CharacterTestReplayPlaybackStatus.Unavailable("Active CharacterTest network runner does not expose replay.");
            CharacterTestFairyGuiNetworkPanelViewModel client =
                CharacterTestFairyGuiNetworkPanelViewModelBuilder.Build(controller);

            Assert.AreEqual("Replay Check: host only", client.ReplaySummaryText);
            Assert.AreEqual("muted", client.ReplayTone);
        }

[Test]
        public void FairyGuiNetworkPanelCommandSink_TogglesAutoSyncProbeWithoutStartingProbe()
        {
            CharacterTestNetworkPanelPreferences.Clear();
            try
            {
                var controller = new RecordingNetworkSessionController();
                var sink = new CharacterTestFairyGuiNetworkPanelCommandSink(
                    controller,
                    controller,
                    null,
                    null,
                    persistPreferences: true);

                sink.Enqueue(new MxUiCommand(
                    CharacterTestFairyGuiNetworkPanelIds.ViewId,
                    CharacterTestFairyGuiNetworkPanelCommandIds.AutoSyncProbe,
                    true));

                Assert.AreEqual(1, controller.SetAutoProbeCount);
                Assert.IsTrue(controller.AutoProbeEnabled);
                Assert.AreEqual(0, controller.StartProbeCount);
                Assert.AreEqual(0, controller.StopProbeCount);
                Assert.IsTrue(CharacterTestNetworkPanelPreferences.LoadAutoSyncProbeAfterEnterGame());
                Assert.IsTrue(sink.LastResult.Success);

                sink.Enqueue(new MxUiCommand(
                    CharacterTestFairyGuiNetworkPanelIds.ViewId,
                    CharacterTestFairyGuiNetworkPanelCommandIds.AutoSyncProbe,
                    false));

                Assert.AreEqual(2, controller.SetAutoProbeCount);
                Assert.IsFalse(controller.AutoProbeEnabled);
                Assert.IsFalse(CharacterTestNetworkPanelPreferences.LoadAutoSyncProbeAfterEnterGame(true));
            }
            finally
            {
                CharacterTestNetworkPanelPreferences.Clear();
            }
        }

[Test]
        public void FairyGuiNetworkPanelCommandSink_TogglesSyncProbe()
        {
            var controller = new RecordingNetworkSessionController();
            controller.Status = new CharacterTestNetworkSessionStatus
            {
                Kind = CharacterTestNetworkSessionKind.Join,
                LaunchMode = CharacterTestNetworkLaunchMode.DirectLan,
                NetworkMode = CharacterTestNetworkMode.UnityTransportClient,
                ClientStatus = NetworkSessionStatus.Connected,
                OwnedEntityId = 42,
                FullSnapshotReceivedCount = 1,
                CanEnterGameplay = true
            };
            var sink = new CharacterTestFairyGuiNetworkPanelCommandSink(
                controller,
                controller,
                null,
                null);

            sink.Enqueue(new MxUiCommand(
                CharacterTestFairyGuiNetworkPanelIds.ViewId,
                CharacterTestFairyGuiNetworkPanelCommandIds.RunSyncProbe,
                null));

            Assert.AreEqual(1, controller.StartProbeCount);
            Assert.AreEqual(0, controller.StopProbeCount);
            Assert.IsTrue(sink.LastResult.Success);
            StringAssert.Contains("RUNNING", sink.LastResult.Message);

            sink.Enqueue(new MxUiCommand(
                CharacterTestFairyGuiNetworkPanelIds.ViewId,
                CharacterTestFairyGuiNetworkPanelCommandIds.RunSyncProbe,
                null));

            Assert.AreEqual(1, controller.StartProbeCount);
            Assert.AreEqual(1, controller.StopProbeCount);
            Assert.IsTrue(sink.LastResult.Success);
            StringAssert.Contains("CANCELLED", sink.LastResult.Message);
        }

[Test]
        public void FairyGuiNetworkPanelCommandSink_CallsControllerAndCloseCallback()
        {
            var controller = new RecordingNetworkSessionController();
            int closeCount = 0;
            int sessionStartedCount = 0;
            var sink = new CharacterTestFairyGuiNetworkPanelCommandSink(
                controller,
                () => closeCount++,
                () => sessionStartedCount++,
                null);
            CharacterTestNetworkSessionOptions hostOptions =
                CharacterTestNetworkSessionOptions.HostDefaults(8111, "0.0.0.0", "server-a");
            CharacterTestNetworkSessionOptions joinOptions =
                CharacterTestNetworkSessionOptions.JoinDefaults("10.0.0.9", 8112, "server-a", "client-a");

            sink.Enqueue(new MxUiCommand(
                CharacterTestFairyGuiNetworkPanelIds.ViewId,
                CharacterTestFairyGuiNetworkPanelCommandIds.Host,
                hostOptions));
            Assert.AreEqual(1, controller.StartCount);
            Assert.AreEqual(CharacterTestNetworkSessionKind.Host, controller.LastStartOptions.Kind);
            Assert.AreEqual(8111, controller.LastStartOptions.Port);
            Assert.AreEqual(0, controller.StopCount);
            Assert.AreEqual(0, closeCount);
            Assert.AreEqual(0, sessionStartedCount);

            sink.Enqueue(new MxUiCommand(
                CharacterTestFairyGuiNetworkPanelIds.ViewId,
                CharacterTestFairyGuiNetworkPanelCommandIds.Join,
                joinOptions));
            Assert.AreEqual(2, controller.StartCount);
            Assert.AreEqual(CharacterTestNetworkSessionKind.Join, controller.LastStartOptions.Kind);
            Assert.AreEqual("10.0.0.9", controller.LastStartOptions.RemoteAddress);
            Assert.AreEqual(8112, controller.LastStartOptions.Port);
            Assert.AreEqual(0, closeCount);
            Assert.AreEqual(0, sessionStartedCount);

            sink.Enqueue(new MxUiCommand(
                CharacterTestFairyGuiNetworkPanelIds.ViewId,
                CharacterTestFairyGuiNetworkPanelCommandIds.Ready,
                true));
            Assert.AreEqual(1, controller.ReadyCount);
            Assert.IsTrue(controller.LastReady);
            Assert.AreEqual(0, closeCount);

            sink.Enqueue(new MxUiCommand(
                CharacterTestFairyGuiNetworkPanelIds.ViewId,
                CharacterTestFairyGuiNetworkPanelCommandIds.EnterGame,
                null));
            Assert.AreEqual(1, controller.EnterGameCount);
            Assert.AreEqual(0, controller.StartRoomCount);
            Assert.AreEqual(0, closeCount);

            sink.Enqueue(new MxUiCommand(
                CharacterTestFairyGuiNetworkPanelIds.ViewId,
                CharacterTestFairyGuiNetworkPanelCommandIds.Disconnect,
                null));
            Assert.AreEqual(1, controller.StopCount);
            Assert.AreEqual(CharacterTestNetworkMode.Disabled, sink.LastResult.NetworkMode);
            Assert.AreEqual(0, closeCount);
            Assert.AreEqual(0, sessionStartedCount);

            sink.Enqueue(new MxUiCommand(
                CharacterTestFairyGuiNetworkPanelIds.ViewId,
                CharacterTestFairyGuiNetworkPanelCommandIds.Close,
                null));
            Assert.AreEqual(1, closeCount);
            Assert.AreEqual(1, controller.StopCount);
            Assert.IsTrue(sink.LastResult.Success);
            Assert.AreEqual(0, sessionStartedCount);

            sink.Enqueue(new MxUiCommand(
                CharacterTestFairyGuiNetworkPanelIds.ViewId,
                MxFairyGuiCommandIds.Cancel,
                null));
            Assert.AreEqual(2, closeCount);
            Assert.AreEqual(1, controller.StopCount);
            Assert.IsTrue(sink.LastResult.Success);
            Assert.AreEqual(0, sessionStartedCount);
        }

[Test]
        public void FairyGuiNetworkPanelCommandSink_PersistsOptionsWhenEnabled()
        {
            CharacterTestNetworkPanelPreferences.Clear();
            try
            {
                var controller = new RecordingNetworkSessionController();
                var sink = new CharacterTestFairyGuiNetworkPanelCommandSink(
                    controller,
                    controller,
                    null,
                    null,
                    persistPreferences: true);
                CharacterTestNetworkSessionOptions joinOptions =
                    CharacterTestNetworkSessionOptions.JoinDefaults(
                        "10.0.0.42",
                        8899,
                        "server-a",
                        "client-a");
                joinOptions.LaunchMode = CharacterTestNetworkLaunchMode.Room;
                joinOptions.RoomName = "saved-room";
                joinOptions.RoomId = "saved-room";
                joinOptions.DisplayName = "Client Alpha";

                sink.Enqueue(new MxUiCommand(
                    CharacterTestFairyGuiNetworkPanelIds.ViewId,
                    CharacterTestFairyGuiNetworkPanelCommandIds.Join,
                    joinOptions));

                Assert.AreEqual(1, controller.StartCount);
                CharacterTestNetworkSessionOptions applied =
                    CharacterTestNetworkPanelPreferences.Apply(
                        CharacterTestNetworkSessionOptions.JoinDefaults(
                            "127.0.0.1",
                            7777,
                            "server",
                            "fresh-runtime-client"));
                Assert.AreEqual(CharacterTestNetworkLaunchMode.Room, applied.LaunchMode);
                Assert.AreEqual("10.0.0.42", applied.RemoteAddress);
                Assert.AreEqual(8899, applied.Port);
                Assert.AreEqual("saved-room", applied.RoomName);
                Assert.AreEqual("Client Alpha", applied.DisplayName);
                Assert.AreEqual("fresh-runtime-client", applied.ClientPeerId);
            }
            finally
            {
                CharacterTestNetworkPanelPreferences.Clear();
            }
        }

[Test]
        public void CharacterTestNetworkEndpoint_SplitsHostPortEndpoint()
        {
            Assert.IsTrue(CharacterTestNetworkEndpoint.TrySplitHostPort(
                "192.168.1.201:7777",
                out string host,
                out ushort port));
            Assert.AreEqual("192.168.1.201", host);
            Assert.AreEqual(7777, port);

            Assert.IsTrue(CharacterTestNetworkEndpoint.TrySplitHostPort(
                "localhost:8888",
                out host,
                out port));
            Assert.AreEqual("localhost", host);
            Assert.AreEqual(8888, port);

            Assert.IsFalse(CharacterTestNetworkEndpoint.TrySplitHostPort("192.168.1.201", out _, out _));
            Assert.IsFalse(CharacterTestNetworkEndpoint.TrySplitHostPort("192.168.1.201:", out _, out _));
            Assert.IsFalse(CharacterTestNetworkEndpoint.TrySplitHostPort("::1:7777", out _, out _));
        }

[Test]
        public void FairyGuiNetworkPanelCommandSink_ChangesLaunchModeWithoutStartingSession()
        {
            var controller = new RecordingNetworkSessionController();
            CharacterTestNetworkLaunchMode lastMode = CharacterTestNetworkLaunchMode.DirectLan;
            var sink = new CharacterTestFairyGuiNetworkPanelCommandSink(
                controller,
                null,
                mode => lastMode = mode);

            sink.Enqueue(new MxUiCommand(
                CharacterTestFairyGuiNetworkPanelIds.ViewId,
                CharacterTestFairyGuiNetworkPanelCommandIds.RoomMode,
                null));

            Assert.AreEqual(1, sink.CommandCount);
            Assert.AreEqual(CharacterTestNetworkLaunchMode.Room, lastMode);
            Assert.AreEqual(0, controller.StartCount);
            Assert.AreEqual(0, controller.StopCount);
            Assert.IsTrue(sink.LastResult.Success);
        }

[Test]
        public void FairyGuiNetworkPanelSourcePackage_DeclaresNetworkPanelComponentAndBindableControls()
        {
            string packagePath = Path.Combine(
                Directory.GetCurrentDirectory(),
                "FGUIProject/assets/MxCharacterTestNetworkUi/package.xml");
            string panelPath = Path.Combine(
                Directory.GetCurrentDirectory(),
                "FGUIProject/assets/MxCharacterTestNetworkUi/NetworkPanel.xml");
            string buttonPath = Path.Combine(
                Directory.GetCurrentDirectory(),
                "FGUIProject/assets/MxCharacterTestNetworkUi/Components/CompactNeutralButton.xml");

            string packageText = File.ReadAllText(packagePath);
            string panelText = File.ReadAllText(panelPath);
            string buttonText = File.ReadAllText(buttonPath);

            StringAssert.Contains("NetworkPanel.xml", packageText);
            StringAssert.Contains("CompactNeutralButton.xml", packageText);
            StringAssert.Contains("menu_network_32x32.png", packageText);
            StringAssert.Contains("extention=\"Button\"", buttonText);
            AssertNetworkPanelChildDeclared(panelText, CharacterTestFairyGuiNetworkPanelIds.PanelFrame);
            AssertNetworkPanelChildDeclared(panelText, CharacterTestFairyGuiNetworkPanelIds.PanelTrim);
            AssertNetworkPanelChildDeclared(panelText, CharacterTestFairyGuiNetworkPanelIds.Title);
            AssertNetworkPanelChildDeclared(panelText, CharacterTestFairyGuiNetworkPanelIds.Status);
            AssertNetworkPanelChildDeclared(panelText, CharacterTestFairyGuiNetworkPanelIds.Endpoint);
            AssertNetworkPanelChildDeclared(panelText, CharacterTestFairyGuiNetworkPanelIds.DirectLanMode);
            AssertNetworkPanelChildDeclared(panelText, CharacterTestFairyGuiNetworkPanelIds.RoomMode);
            AssertNetworkPanelChildDeclared(panelText, CharacterTestFairyGuiNetworkPanelIds.ModeDescription);
            AssertNetworkPanelChildDeclared(panelText, CharacterTestFairyGuiNetworkPanelIds.ConnectionHeader);
            AssertNetworkPanelChildDeclared(panelText, CharacterTestFairyGuiNetworkPanelIds.RemoteAddressLabel);
            AssertNetworkPanelChildDeclared(panelText, CharacterTestFairyGuiNetworkPanelIds.RemoteAddressInput);
            AssertNetworkPanelChildDeclared(panelText, CharacterTestFairyGuiNetworkPanelIds.PortLabel);
            AssertNetworkPanelChildDeclared(panelText, CharacterTestFairyGuiNetworkPanelIds.PortInput);
            AssertNetworkPanelChildDeclared(panelText, CharacterTestFairyGuiNetworkPanelIds.DisplayNameLabel);
            AssertNetworkPanelChildDeclared(panelText, CharacterTestFairyGuiNetworkPanelIds.DisplayNameInput);
            AssertNetworkPanelChildDeclared(panelText, CharacterTestFairyGuiNetworkPanelIds.RoomNameLabel);
            AssertNetworkPanelChildDeclared(panelText, CharacterTestFairyGuiNetworkPanelIds.RoomNameInput);
            AssertNetworkPanelChildDeclared(panelText, CharacterTestFairyGuiNetworkPanelIds.ConnectionSummary);
            AssertNetworkPanelChildDeclared(panelText, CharacterTestFairyGuiNetworkPanelIds.RoomHeader);
            AssertNetworkPanelChildDeclared(panelText, CharacterTestFairyGuiNetworkPanelIds.RoomSummary);
            AssertNetworkPanelChildDeclared(panelText, CharacterTestFairyGuiNetworkPanelIds.MemberSummary);
            AssertNetworkPanelChildDeclared(panelText, CharacterTestFairyGuiNetworkPanelIds.ReadinessSummary);
            AssertNetworkPanelChildDeclared(panelText, CharacterTestFairyGuiNetworkPanelIds.SyncProbeSummary);
            AssertNetworkPanelChildDeclared(panelText, CharacterTestFairyGuiNetworkPanelIds.ReplayHeader);
            AssertNetworkPanelChildDeclared(panelText, CharacterTestFairyGuiNetworkPanelIds.ReplaySummary);
            AssertNetworkPanelChildDeclared(panelText, CharacterTestFairyGuiNetworkPanelIds.DetailsHeader);
            AssertNetworkPanelChildDeclared(panelText, CharacterTestFairyGuiNetworkPanelIds.DetailTransport);
            AssertNetworkPanelChildDeclared(panelText, CharacterTestFairyGuiNetworkPanelIds.DetailPeer);
            AssertNetworkPanelChildDeclared(panelText, CharacterTestFairyGuiNetworkPanelIds.DetailRtt);
            AssertNetworkPanelChildDeclared(panelText, CharacterTestFairyGuiNetworkPanelIds.DetailFrames);
            AssertNetworkPanelChildDeclared(panelText, CharacterTestFairyGuiNetworkPanelIds.DetailSnapshot);
            AssertNetworkPanelChildDeclared(panelText, CharacterTestFairyGuiNetworkPanelIds.DetailDisconnect);
            AssertNetworkPanelChildDeclared(panelText, CharacterTestFairyGuiNetworkPanelIds.Message);
            Assert.IsFalse(panelText.Contains("name=\"btnSettings\""));
            AssertNetworkPanelChildDeclared(panelText, CharacterTestFairyGuiNetworkPanelIds.Host);
            AssertNetworkPanelChildDeclared(panelText, CharacterTestFairyGuiNetworkPanelIds.Join);
            AssertNetworkPanelChildDeclared(panelText, CharacterTestFairyGuiNetworkPanelIds.Disconnect);
            AssertNetworkPanelChildDeclared(panelText, CharacterTestFairyGuiNetworkPanelIds.Ready);
            AssertNetworkPanelChildDeclared(panelText, CharacterTestFairyGuiNetworkPanelIds.EnterGame);
            AssertNetworkPanelChildDeclared(panelText, CharacterTestFairyGuiNetworkPanelIds.AutoSyncProbe);
            AssertNetworkPanelChildDeclared(panelText, CharacterTestFairyGuiNetworkPanelIds.RunSyncProbe);
            AssertNetworkPanelChildDeclared(panelText, CharacterTestFairyGuiNetworkPanelIds.Close);
            Assert.IsFalse(panelText.Contains("name=\"btnStartRoom\""));
            StringAssert.Contains("name=\"panelFrame\" xy=\"0,0\" size=\"1920,1080\"", panelText);
            StringAssert.Contains("input=\"true\"", panelText);
        }

[Test]
        public void FairyGuiNetworkPanelBinder_BindsStatusInputsAndCommands()
        {
            Fgui.GComponent root = CreateNetworkPanelRoot();
            root.SetSize(1600f, 900f);
            var sink = new RecordingUiCommandSink();
            using (var handle = new MxFairyGuiComponentHandle(
                CharacterTestFairyGuiNetworkPanelIds.PackageId,
                CharacterTestFairyGuiNetworkPanelIds.ComponentName,
                root))
            {
                var binder = new CharacterTestFairyGuiNetworkPanelBinder(sink);
                var viewModel = new CharacterTestFairyGuiNetworkPanelViewModel
                {
                    Status = CharacterTestNetworkSessionStatus.Offline("Offline"),
                    HostOptions = CharacterTestNetworkSessionOptions.HostDefaults(7777, "0.0.0.0", "server"),
                    JoinOptions = CharacterTestNetworkSessionOptions.JoinDefaults("192.168.1.20", 7777, "server", "client-0"),
                    SelectedLaunchMode = CharacterTestNetworkLaunchMode.Room,
                    CanSelectLaunchMode = true,
                    CanHost = true,
                    CanJoin = true,
                    CanDisconnect = true,
                    CanReady = true,
                    LocalReady = false,
                    CanEnterGame = true,
                    CanToggleAutoSyncProbe = true,
                    AutoSyncProbeAfterEnterGame = false,
                    CanRunSyncProbe = true,
                    CanClose = true,
                    StatusText = "Offline",
                    EndpointText = "192.168.1.20:7777",
                    ModeDescriptionText = "Room mode",
                    ConnectionHeaderText = "Connection",
                    ConnectionSummaryText = "LAN Room · Offline",
                    RoomHeaderText = "Room",
                    RoomSummaryText = "CharacterTest LAN Room · Closed",
                    MemberSummaryText = "Members: waiting",
                    ReadinessSummaryText = "Readiness: waiting",
                    SyncProbeSummaryText = "Sync Probe: not run.",
                    ReplaySummaryText = "Replay Check: PASS frames=300 auth=46",
                    ReplayTone = "pass",
                    ReadyButtonText = "Ready",
                    EnterGameButtonText = "Enter Game",
                    AutoSyncProbeButtonText = "Auto Probe Off",
                    RunSyncProbeButtonText = "Run Sync Probe",
                    DetailsHeaderText = "Details",
                    DetailTransportText = "Transport: none",
                    DetailPeerText = "Peer: server=server client=client-0 owned=0",
                    DetailRttText = "RTT: 0f avg 0f",
                    DetailFramesText = "Frames: server 0 client 0 drift 0f",
                    DetailSnapshotText = "Snapshots: resync 0 mismatch 0 req 0",
                    DetailDisconnectText = "Disconnect: none"
                };

                binder.Bind(handle, viewModel);

                Assert.AreEqual(1600f, root.width);
                Assert.AreEqual(900f, root.height);
                Assert.AreEqual(1600f, NetworkPanelObject(root, "modalShade").width);
                Assert.AreEqual(900f, NetworkPanelObject(root, "modalShade").height);
                Assert.AreEqual(1600f, NetworkPanelObject(root, CharacterTestFairyGuiNetworkPanelIds.PanelFrame).width);
                Assert.AreEqual(900f, NetworkPanelObject(root, CharacterTestFairyGuiNetworkPanelIds.PanelFrame).height);
                Assert.AreEqual("Offline", NetworkPanelText(root, CharacterTestFairyGuiNetworkPanelIds.Status));
                Assert.AreEqual("192.168.1.20", NetworkPanelInput(root, CharacterTestFairyGuiNetworkPanelIds.RemoteAddressInput).text);
                Assert.AreEqual("7777", NetworkPanelInput(root, CharacterTestFairyGuiNetworkPanelIds.PortInput).text);
                Assert.AreEqual("Player", NetworkPanelInput(root, CharacterTestFairyGuiNetworkPanelIds.DisplayNameInput).text);
                Assert.AreEqual("CharacterTest LAN Room", NetworkPanelInput(root, CharacterTestFairyGuiNetworkPanelIds.RoomNameInput).text);
                Assert.IsFalse(NetworkPanelButton(root, CharacterTestFairyGuiNetworkPanelIds.DirectLanMode).selected);
                Assert.IsTrue(NetworkPanelButton(root, CharacterTestFairyGuiNetworkPanelIds.RoomMode).selected);
                Assert.IsFalse(NetworkPanelButton(root, CharacterTestFairyGuiNetworkPanelIds.AutoSyncProbe).selected);
                Assert.AreEqual("LAN Room · Offline", NetworkPanelText(root, CharacterTestFairyGuiNetworkPanelIds.ConnectionSummary));
                Assert.AreEqual("Readiness: waiting", NetworkPanelText(root, CharacterTestFairyGuiNetworkPanelIds.ReadinessSummary));
                Assert.AreEqual("Sync Probe: not run.", NetworkPanelText(root, CharacterTestFairyGuiNetworkPanelIds.SyncProbeSummary));
                Assert.AreEqual("Replay Check: PASS frames=300 auth=46", NetworkPanelText(root, CharacterTestFairyGuiNetworkPanelIds.ReplaySummary));
                Assert.AreEqual("Transport: none", NetworkPanelText(root, CharacterTestFairyGuiNetworkPanelIds.DetailTransport));

                NetworkPanelInput(root, CharacterTestFairyGuiNetworkPanelIds.PortInput).text = "8888";
                NetworkPanelInput(root, CharacterTestFairyGuiNetworkPanelIds.RoomNameInput).text = "duel-room";
                NetworkPanelInput(root, CharacterTestFairyGuiNetworkPanelIds.DisplayNameInput).text = "Host Alpha";
                NetworkPanelButton(root, CharacterTestFairyGuiNetworkPanelIds.Host).onClick.Call();
                Assert.AreEqual(1, sink.Count);
                Assert.AreEqual(CharacterTestFairyGuiNetworkPanelCommandIds.Host, sink.Last.CommandId);
                var hostPayload = (CharacterTestNetworkSessionOptions)sink.Last.Payload;
                Assert.AreEqual(CharacterTestNetworkSessionKind.Host, hostPayload.Kind);
                Assert.AreEqual(CharacterTestNetworkLaunchMode.Room, hostPayload.LaunchMode);
                Assert.AreEqual(8888, hostPayload.Port);
                Assert.AreEqual("duel-room", hostPayload.RoomId);
                Assert.AreEqual("duel-room", hostPayload.RoomName);
                Assert.AreEqual("Host Alpha", hostPayload.DisplayName);

                NetworkPanelInput(root, CharacterTestFairyGuiNetworkPanelIds.RemoteAddressInput).text = "10.0.0.42";
                NetworkPanelInput(root, CharacterTestFairyGuiNetworkPanelIds.PortInput).text = "8899";
                NetworkPanelInput(root, CharacterTestFairyGuiNetworkPanelIds.DisplayNameInput).text = "Client Beta";
                NetworkPanelButton(root, CharacterTestFairyGuiNetworkPanelIds.Join).onClick.Call();
                Assert.AreEqual(2, sink.Count);
                Assert.AreEqual(CharacterTestFairyGuiNetworkPanelCommandIds.Join, sink.Last.CommandId);
                var joinPayload = (CharacterTestNetworkSessionOptions)sink.Last.Payload;
                Assert.AreEqual(CharacterTestNetworkSessionKind.Join, joinPayload.Kind);
                Assert.AreEqual(CharacterTestNetworkLaunchMode.Room, joinPayload.LaunchMode);
                Assert.AreEqual("10.0.0.42", joinPayload.RemoteAddress);
                Assert.AreEqual(8899, joinPayload.Port);
                Assert.AreEqual("duel-room", joinPayload.RoomId);
                Assert.AreEqual("Client Beta", joinPayload.DisplayName);

                NetworkPanelButton(root, CharacterTestFairyGuiNetworkPanelIds.Disconnect).onClick.Call();
                Assert.AreEqual(3, sink.Count);
                Assert.AreEqual(CharacterTestFairyGuiNetworkPanelCommandIds.Disconnect, sink.Last.CommandId);
                Assert.IsNull(sink.Last.Payload);

                NetworkPanelButton(root, CharacterTestFairyGuiNetworkPanelIds.Ready).onClick.Call();
                Assert.AreEqual(4, sink.Count);
                Assert.AreEqual(CharacterTestFairyGuiNetworkPanelCommandIds.Ready, sink.Last.CommandId);
                Assert.AreEqual(true, sink.Last.Payload);

                NetworkPanelButton(root, CharacterTestFairyGuiNetworkPanelIds.EnterGame).onClick.Call();
                Assert.AreEqual(5, sink.Count);
                Assert.AreEqual(CharacterTestFairyGuiNetworkPanelCommandIds.EnterGame, sink.Last.CommandId);
                Assert.IsNull(sink.Last.Payload);

                NetworkPanelButton(root, CharacterTestFairyGuiNetworkPanelIds.AutoSyncProbe).onClick.Call();
                Assert.AreEqual(6, sink.Count);
                Assert.AreEqual(CharacterTestFairyGuiNetworkPanelCommandIds.AutoSyncProbe, sink.Last.CommandId);
                Assert.AreEqual(true, sink.Last.Payload);

                NetworkPanelButton(root, CharacterTestFairyGuiNetworkPanelIds.RunSyncProbe).onClick.Call();
                Assert.AreEqual(7, sink.Count);
                Assert.AreEqual(CharacterTestFairyGuiNetworkPanelCommandIds.RunSyncProbe, sink.Last.CommandId);
                Assert.IsNull(sink.Last.Payload);

                NetworkPanelButton(root, CharacterTestFairyGuiNetworkPanelIds.Close).onClick.Call();
                Assert.AreEqual(8, sink.Count);
                Assert.AreEqual(CharacterTestFairyGuiNetworkPanelCommandIds.Close, sink.Last.CommandId);
                Assert.IsNull(sink.Last.Payload);
            }
        }

[Test]
        public void FairyGuiNetworkPanelBinder_JoinAddressAcceptsHostPortEndpoint()
        {
            Fgui.GComponent root = CreateNetworkPanelRoot();
            var sink = new RecordingUiCommandSink();
            using (var handle = new MxFairyGuiComponentHandle(
                CharacterTestFairyGuiNetworkPanelIds.PackageId,
                CharacterTestFairyGuiNetworkPanelIds.ComponentName,
                root))
            {
                var binder = new CharacterTestFairyGuiNetworkPanelBinder(sink);
                binder.Bind(
                    handle,
                    new CharacterTestFairyGuiNetworkPanelViewModel
                    {
                        Status = CharacterTestNetworkSessionStatus.Offline("Offline"),
                        HostOptions = CharacterTestNetworkSessionOptions.HostDefaults(),
                        JoinOptions = CharacterTestNetworkSessionOptions.JoinDefaults("127.0.0.1", 7777),
                        SelectedLaunchMode = CharacterTestNetworkLaunchMode.DirectLan,
                        CanJoin = true
                    });

                NetworkPanelInput(root, CharacterTestFairyGuiNetworkPanelIds.RemoteAddressInput).text =
                    "192.168.1.201:7788";
                NetworkPanelInput(root, CharacterTestFairyGuiNetworkPanelIds.PortInput).text = "7777";
                NetworkPanelButton(root, CharacterTestFairyGuiNetworkPanelIds.Join).onClick.Call();

                Assert.AreEqual(1, sink.Count);
                var joinPayload = (CharacterTestNetworkSessionOptions)sink.Last.Payload;
                Assert.AreEqual(CharacterTestNetworkSessionKind.Join, joinPayload.Kind);
                Assert.AreEqual("192.168.1.201", joinPayload.RemoteAddress);
                Assert.AreEqual(7788, joinPayload.Port);
            }
        }

[Test]
        public void FairyGuiSettingsPanelSourcePackage_DeclaresSettingsPanelComponentAndBindableControls()
        {
            string packagePath = Path.Combine(
                Directory.GetCurrentDirectory(),
                "FGUIProject/assets/MxCharacterTestSettingsUi/package.xml");
            string panelPath = Path.Combine(
                Directory.GetCurrentDirectory(),
                "FGUIProject/assets/MxCharacterTestSettingsUi/SettingsPanel.xml");
            string miniButtonPath = Path.Combine(
                Directory.GetCurrentDirectory(),
                "FGUIProject/assets/MxCharacterTestSettingsUi/Components/SettingsFrostedButton.xml");
            string actionButtonPath = Path.Combine(
                Directory.GetCurrentDirectory(),
                "FGUIProject/assets/MxCharacterTestSettingsUi/Components/SettingsFrostedButton.xml");
            string iconButtonPath = Path.Combine(
                Directory.GetCurrentDirectory(),
                "FGUIProject/assets/MxCharacterTestSettingsUi/Components/SettingsFrostedButton.xml");
            string modeButtonPath = Path.Combine(
                Directory.GetCurrentDirectory(),
                "FGUIProject/assets/MxCharacterTestSettingsUi/Components/SettingsFrostedNavigation.xml");
            string inputBindingRowPath = Path.Combine(
                Directory.GetCurrentDirectory(),
                "FGUIProject/assets/MxCharacterTestSettingsUi/Components/SettingsInputBindingRow.xml");

            string packageText = File.ReadAllText(packagePath);
            string panelText = File.ReadAllText(panelPath);
            string miniButtonText = File.ReadAllText(miniButtonPath);
            string actionButtonText = File.ReadAllText(actionButtonPath);
            string iconButtonText = File.ReadAllText(iconButtonPath);
            string modeButtonText = File.ReadAllText(modeButtonPath);
            string inputBindingRowText = File.ReadAllText(inputBindingRowPath);

            StringAssert.Contains("SettingsPanel.xml", packageText);
            StringAssert.Contains("SettingsFrostedButton.xml", packageText);
            StringAssert.Contains("SettingsFrostedButton.xml", packageText);
            StringAssert.Contains("SettingsFrostedButton.xml", packageText);
            StringAssert.Contains("SettingsFrostedNavigation.xml", packageText);
            StringAssert.Contains("SettingsInputBindingRow.xml", packageText);
            StringAssert.Contains("frosted_settings_800x480.png", packageText);
            StringAssert.Contains("frosted_status_label_sliced.png", packageText);
            StringAssert.Contains("name=\"scrim\" xy=\"0,0\" size=\"1280,800\"", panelText);
            StringAssert.Contains("name=\"panelFrame\" xy=\"48,40\" size=\"1184,720\"", panelText);
            StringAssert.Contains("name=\"btnNavGraphics\" xy=\"76,178\" size=\"188,64\"", panelText);
            StringAssert.Contains("name=\"btnNavAudio\" xy=\"76,266\" size=\"188,64\"", panelText);
            StringAssert.Contains("name=\"btnNavControls\" xy=\"76,354\" size=\"188,64\"", panelText);
            StringAssert.Contains("frosted_settings_800x480.png", panelText);
            StringAssert.Contains("sidePair=\"width-width,height-height\"", panelText);
            StringAssert.Contains("name=\"inputList\"", panelText);
            StringAssert.Contains("defaultItem=\"ui://", panelText);
            StringAssert.Contains("extention=\"Button\"", miniButtonText);
            StringAssert.Contains("extention=\"Button\"", actionButtonText);
            StringAssert.Contains("extention=\"Button\"", iconButtonText);
            StringAssert.Contains("extention=\"Button\"", modeButtonText);
            StringAssert.Contains("control_320x96.png", actionButtonText);
            StringAssert.Contains("name=\"actionLabel\"", inputBindingRowText);
            StringAssert.Contains("name=\"primaryButton\"", inputBindingRowText);
            StringAssert.Contains("name=\"secondaryButton\"", inputBindingRowText);
            StringAssert.Contains("name=\"cancelHint\"", inputBindingRowText);
            Assert.IsTrue(File.Exists(Path.Combine(
                Directory.GetCurrentDirectory(),
                "FGUIProject/assets/MxCharacterTestSettingsUi/Images/MxUiFoundation/panels/settings/frosted_settings_800x480.png")));
            Assert.IsTrue(File.Exists(Path.Combine(
                Directory.GetCurrentDirectory(),
                "FGUIProject/assets/MxCharacterTestSettingsUi/Images/MxUiFoundation/buttons/tactical_home/frosted_status_label_sliced.png")));
            Assert.IsTrue(File.Exists(Path.Combine(
                Directory.GetCurrentDirectory(),
                "FGUIProject/assets/MxCharacterTestSettingsUi/Images/MxUiFoundation/panels/settings/control_320x96.png")));
            AssertSettingsPanelChildDeclared(panelText, CharacterTestFairyGuiSettingsPanelIds.Title);
            AssertSettingsPanelChildDeclared(panelText, CharacterTestFairyGuiSettingsPanelIds.Subtitle);
            AssertSettingsPanelChildDeclared(panelText, CharacterTestFairyGuiSettingsPanelIds.PageTitle);
            AssertSettingsPanelChildDeclared(panelText, CharacterTestFairyGuiSettingsPanelIds.PageDescription);
            AssertSettingsPanelChildDeclared(panelText, CharacterTestFairyGuiSettingsPanelIds.Status);
            AssertSettingsPanelChildDeclared(panelText, CharacterTestFairyGuiSettingsPanelIds.DirtyMarker);
            AssertSettingsPanelChildDeclared(panelText, CharacterTestFairyGuiSettingsPanelIds.Close);
            AssertSettingsPanelChildDeclared(panelText, CharacterTestFairyGuiSettingsPanelIds.NavGraphics);
            AssertSettingsPanelChildDeclared(panelText, CharacterTestFairyGuiSettingsPanelIds.NavAudio);
            AssertSettingsPanelChildDeclared(panelText, CharacterTestFairyGuiSettingsPanelIds.NavControls);
            AssertSettingsPanelChildDeclared(panelText, CharacterTestFairyGuiSettingsPanelIds.KeyboardMouseTab);
            AssertSettingsPanelChildDeclared(panelText, CharacterTestFairyGuiSettingsPanelIds.GamepadTab);
            AssertSettingsPanelChildDeclared(panelText, CharacterTestFairyGuiSettingsPanelIds.InputInstruction);
            AssertSettingsPanelChildDeclared(panelText, CharacterTestFairyGuiSettingsPanelIds.ActionColumnTitle);
            AssertSettingsPanelChildDeclared(panelText, CharacterTestFairyGuiSettingsPanelIds.PrimaryColumnTitle);
            AssertSettingsPanelChildDeclared(panelText, CharacterTestFairyGuiSettingsPanelIds.SecondaryColumnTitle);
            AssertSettingsPanelChildDeclared(panelText, CharacterTestFairyGuiSettingsPanelIds.InputList);
            AssertSettingsPanelChildDeclared(panelText, CharacterTestFairyGuiSettingsPanelIds.InputRebindProxy);
            AssertSettingsPanelChildDeclared(panelText, CharacterTestFairyGuiSettingsPanelIds.AntiAliasingValue);
            AssertSettingsPanelChildDeclared(panelText, CharacterTestFairyGuiSettingsPanelIds.AntiAliasingPrev);
            AssertSettingsPanelChildDeclared(panelText, CharacterTestFairyGuiSettingsPanelIds.AntiAliasingNext);
            AssertSettingsPanelChildDeclared(panelText, CharacterTestFairyGuiSettingsPanelIds.ShadowDistanceInput);
            AssertSettingsPanelChildDeclared(panelText, CharacterTestFairyGuiSettingsPanelIds.DirectionalShadowQualityValue);
            AssertSettingsPanelChildDeclared(panelText, CharacterTestFairyGuiSettingsPanelIds.ShadowCascadeCountValue);
            AssertSettingsPanelChildDeclared(panelText, CharacterTestFairyGuiSettingsPanelIds.LocalShadowResolutionValue);
            AssertSettingsPanelChildDeclared(panelText, CharacterTestFairyGuiSettingsPanelIds.LanguageLabel);
            AssertSettingsPanelChildDeclared(panelText, CharacterTestFairyGuiSettingsPanelIds.LanguageValue);
            AssertSettingsPanelChildDeclared(panelText, CharacterTestFairyGuiSettingsPanelIds.LanguagePrev);
            AssertSettingsPanelChildDeclared(panelText, CharacterTestFairyGuiSettingsPanelIds.LanguageNext);
            AssertSettingsPanelChildDeclared(panelText, CharacterTestFairyGuiSettingsPanelIds.VSyncToggle);
            AssertSettingsPanelChildDeclared(panelText, CharacterTestFairyGuiSettingsPanelIds.RenderScaleInput);
            AssertSettingsPanelChildDeclared(panelText, CharacterTestFairyGuiSettingsPanelIds.ResolutionValue);
            AssertSettingsPanelChildDeclared(panelText, CharacterTestFairyGuiSettingsPanelIds.WindowModeValue);
            AssertSettingsPanelChildDeclared(panelText, CharacterTestFairyGuiSettingsPanelIds.AudioCategoryLabel);
            AssertSettingsPanelChildDeclared(panelText, CharacterTestFairyGuiSettingsPanelIds.AudioCategoryValue);
            AssertSettingsPanelChildDeclared(panelText, CharacterTestFairyGuiSettingsPanelIds.AudioCategoryPrevious);
            AssertSettingsPanelChildDeclared(panelText, CharacterTestFairyGuiSettingsPanelIds.AudioCategoryNext);
            AssertSettingsPanelChildDeclared(panelText, CharacterTestFairyGuiSettingsPanelIds.AudioVolumeInput);
            AssertSettingsPanelChildDeclared(panelText, CharacterTestFairyGuiSettingsPanelIds.AudioMuteToggle);
            AssertSettingsPanelChildDeclared(panelText, CharacterTestFairyGuiSettingsPanelIds.Apply);
            AssertSettingsPanelChildDeclared(panelText, CharacterTestFairyGuiSettingsPanelIds.CancelChanges);
            AssertSettingsPanelChildDeclared(panelText, CharacterTestFairyGuiSettingsPanelIds.ResetInput);
            StringAssert.Contains("input=\"true\"", panelText);
        }

[Test]
        public void FairyGuiSettingsPanelBinder_CentersModalWithinResponsiveScrim()
        {
            Fgui.GComponent root = CreateSettingsPanelRoot();
            Fgui.GComponent layerRoot = new Fgui.GComponent();
            layerRoot.SetSize(1600f, 900f);
            layerRoot.AddChild(root);
            var sink = new RecordingUiCommandSink();
            using (var handle = new MxFairyGuiComponentHandle(
                CharacterTestFairyGuiSettingsPanelIds.PackageId,
                CharacterTestFairyGuiSettingsPanelIds.ComponentName,
                root))
            {
                var binder = new CharacterTestFairyGuiSettingsPanelBinder(sink);

                binder.Bind(handle, new CharacterTestFairyGuiSettingsPanelViewModel());

                Assert.AreEqual(1600f, root.width);
                Assert.AreEqual(900f, root.height);
                Assert.AreEqual(1600f, SettingsPanelObject(root, "scrim").width);
                Assert.AreEqual(900f, SettingsPanelObject(root, "scrim").height);
                Assert.AreEqual(1184f, SettingsPanelObject(root, CharacterTestFairyGuiSettingsPanelIds.PanelFrame).width);
                Assert.AreEqual(720f, SettingsPanelObject(root, CharacterTestFairyGuiSettingsPanelIds.PanelFrame).height);
                Assert.AreEqual(208f, SettingsPanelObject(root, CharacterTestFairyGuiSettingsPanelIds.PanelFrame).x);
                Assert.AreEqual(90f, SettingsPanelObject(root, CharacterTestFairyGuiSettingsPanelIds.PanelFrame).y);
                Assert.AreEqual(308f, SettingsPanelObject(root, CharacterTestFairyGuiSettingsPanelIds.Title).x);
                Assert.AreEqual(126f, SettingsPanelObject(root, CharacterTestFairyGuiSettingsPanelIds.Title).y);
                Assert.AreEqual(480f, SettingsPanelObject(root, CharacterTestFairyGuiSettingsPanelIds.InputList).x);
                Assert.AreEqual(336f, SettingsPanelObject(root, CharacterTestFairyGuiSettingsPanelIds.InputList).y);
                // A second bind and a smaller viewport must not accumulate offsets or clip the modal.
                binder.Bind(handle, new CharacterTestFairyGuiSettingsPanelViewModel());
                Assert.AreEqual(208f, SettingsPanelObject(root, CharacterTestFairyGuiSettingsPanelIds.PanelFrame).x);
                layerRoot.SetSize(1280f, 720f);
                binder.Bind(handle, new CharacterTestFairyGuiSettingsPanelViewModel());
                Fgui.GObject frame = SettingsPanelObject(root, CharacterTestFairyGuiSettingsPanelIds.PanelFrame);
                Assert.AreEqual(0.8f, frame.scaleX, 0.001f);
                Assert.AreEqual(640f, frame.x + frame.width * frame.scaleX * 0.5f, 0.01f);
                Assert.AreEqual(360f, frame.y + frame.height * frame.scaleY * 0.5f, 0.01f);
            }
        }

[Test]
        public void FairyGuiSettingsPanelBinder_MapsToggleSelectedState()
        {
            Fgui.GComponent root = CreateSettingsPanelRoot();
            var sink = new RecordingUiCommandSink();
            using (var handle = new MxFairyGuiComponentHandle(
                CharacterTestFairyGuiSettingsPanelIds.PackageId,
                CharacterTestFairyGuiSettingsPanelIds.ComponentName,
                root))
            {
                var binder = new CharacterTestFairyGuiSettingsPanelBinder(sink);

                binder.Bind(handle, new CharacterTestFairyGuiSettingsPanelViewModel
                {
                    VSyncCount = 1,
                    AudioMuted = false
                });
                Assert.IsTrue(SettingsPanelButton(root, CharacterTestFairyGuiSettingsPanelIds.VSyncToggle).selected);
                Assert.IsFalse(SettingsPanelButton(root, CharacterTestFairyGuiSettingsPanelIds.AudioMuteToggle).selected);

                binder.Bind(handle, new CharacterTestFairyGuiSettingsPanelViewModel
                {
                    VSyncCount = 0,
                    AudioMuted = true
                });
                Assert.IsFalse(SettingsPanelButton(root, CharacterTestFairyGuiSettingsPanelIds.VSyncToggle).selected);
                Assert.IsTrue(SettingsPanelButton(root, CharacterTestFairyGuiSettingsPanelIds.AudioMuteToggle).selected);
            }
        }

[Test]
        public void FairyGuiSettingsPanelBinder_DisabledButtonsStayClippableAndReadable()
        {
            Fgui.GComponent root = CreateSettingsPanelRoot();
            using (var handle = new MxFairyGuiComponentHandle(
                CharacterTestFairyGuiSettingsPanelIds.PackageId,
                CharacterTestFairyGuiSettingsPanelIds.ComponentName,
                root))
            {
                var binder = new CharacterTestFairyGuiSettingsPanelBinder(
                    new RecordingUiCommandSink());

                binder.Bind(handle, new CharacterTestFairyGuiSettingsPanelViewModel
                {
                    CanEdit = false,
                    SelectedPage = CharacterTestSettingsPage.Controls
                });

                Fgui.GButton controls = SettingsPanelButton(
                    root,
                    CharacterTestFairyGuiSettingsPanelIds.NavControls);
                Assert.IsFalse(controls.enabled);
                Assert.IsFalse(controls.grayed);
                Assert.AreEqual(0.45f, controls.alpha);
                Assert.AreEqual("按键", controls.text);

                Fgui.GButton cancel = SettingsPanelButton(
                    root,
                    CharacterTestFairyGuiSettingsPanelIds.CancelChanges);
                Assert.IsTrue(cancel.enabled);
                Assert.IsFalse(cancel.grayed);
                Assert.AreEqual(1f, cancel.alpha);
            }
        }

[Test]
        public void FairyGuiSettingsPanelBinder_BindsPagesFieldsAndStaticCommands()
        {
            Fgui.GComponent root = CreateSettingsPanelRoot();
            var sink = new RecordingUiCommandSink();
            using (var handle = new MxFairyGuiComponentHandle(
                CharacterTestFairyGuiSettingsPanelIds.PackageId,
                CharacterTestFairyGuiSettingsPanelIds.ComponentName,
                root))
            {
                var binder = new CharacterTestFairyGuiSettingsPanelBinder(
                    sink,
                    CharacterTestFairyGuiSettingsPanelIds.ViewId,
                    CreateCharacterTestTextProvider("zh-CN"));
                var viewModel = new CharacterTestFairyGuiSettingsPanelViewModel
                {
                    Title = "设置",
                    Subtitle = "调整游戏体验与操作方式",
                    PageTitle = "按键设置",
                    PageDescription = "为键盘、鼠标或手柄重新分配操作",
                    InputInstruction = "点击按键框开始重新绑定；监听时按 Esc 取消。",
                    ActionColumnTitle = "操作",
                    PrimaryColumnTitle = "主要按键",
                    SecondaryColumnTitle = "备用按键",
                    StatusText = "Ready",
                    DirtyText = "Dirty",
                    Dirty = true,
                    SelectedPage = CharacterTestSettingsPage.Controls,
                    SelectedInputDevice = CharacterTestSettingsInputDevicePage.KeyboardMouse,
                    AntiAliasingText = "4",
                    ShadowDistanceText = "120",
                    DirectionalShadowQualityText = "中",
                    ShadowCascadeCountText = "3",
                    LocalShadowResolutionText = "2048",
                    VSyncCount = 1,
                    VSyncText = "1",
                    RenderScaleText = "1.25",
                    ResolutionText = "1920 x 1080",
                    WindowModeText = "FullscreenWindow",
                    LocaleText = "zh-CN",
                    AudioCategoryText = "Music",
                    AudioVolumeText = "0.8",
                    AudioMuted = false,
                    AudioMutedText = "On"
                };

                binder.Bind(handle, viewModel);

                Assert.AreEqual("设置", SettingsPanelText(root, CharacterTestFairyGuiSettingsPanelIds.Title));
                Assert.AreEqual("按键设置", SettingsPanelText(root, CharacterTestFairyGuiSettingsPanelIds.PageTitle));
                Assert.AreEqual("4", SettingsPanelText(root, CharacterTestFairyGuiSettingsPanelIds.AntiAliasingValue));
                Assert.AreEqual("中", SettingsPanelText(root, CharacterTestFairyGuiSettingsPanelIds.DirectionalShadowQualityValue));
                Assert.AreEqual("3", SettingsPanelText(root, CharacterTestFairyGuiSettingsPanelIds.ShadowCascadeCountValue));
                Assert.AreEqual("2048", SettingsPanelText(root, CharacterTestFairyGuiSettingsPanelIds.LocalShadowResolutionValue));
                Assert.AreEqual("画面与阴影", SettingsPanelText(root, CharacterTestFairyGuiSettingsPanelIds.GraphicsHeader));
                Assert.AreEqual("语言", SettingsPanelText(root, CharacterTestFairyGuiSettingsPanelIds.LanguageLabel));
                Assert.AreEqual("zh-CN", SettingsPanelText(root, CharacterTestFairyGuiSettingsPanelIds.LanguageValue));
                Assert.AreEqual("分类", SettingsPanelText(root, CharacterTestFairyGuiSettingsPanelIds.AudioCategoryLabel));
                Assert.AreEqual("Music", SettingsPanelText(root, CharacterTestFairyGuiSettingsPanelIds.AudioCategoryValue));
                Assert.AreEqual("1.25", SettingsPanelInput(root, CharacterTestFairyGuiSettingsPanelIds.RenderScaleInput).text);
                Assert.AreEqual("120", SettingsPanelInput(root, CharacterTestFairyGuiSettingsPanelIds.ShadowDistanceInput).text);
                Assert.AreEqual("0.8", SettingsPanelInput(root, CharacterTestFairyGuiSettingsPanelIds.AudioVolumeInput).text);
                Assert.IsTrue(SettingsPanelButton(root, CharacterTestFairyGuiSettingsPanelIds.VSyncToggle).selected);
                Assert.IsFalse(SettingsPanelButton(root, CharacterTestFairyGuiSettingsPanelIds.AudioMuteToggle).selected);
                Assert.IsTrue(SettingsPanelButton(root, CharacterTestFairyGuiSettingsPanelIds.NavControls).selected);
                Assert.IsTrue(SettingsPanelButton(root, CharacterTestFairyGuiSettingsPanelIds.KeyboardMouseTab).selected);
                Assert.AreEqual(0, root.GetChild(CharacterTestFairyGuiSettingsPanelIds.InputList).asList.numItems);

                SettingsPanelInput(root, CharacterTestFairyGuiSettingsPanelIds.RenderScaleInput).text = "1.5";
                SettingsPanelInput(root, CharacterTestFairyGuiSettingsPanelIds.ShadowDistanceInput).text = "80";
                SettingsPanelInput(root, CharacterTestFairyGuiSettingsPanelIds.AudioVolumeInput).text = "0.35";
                SettingsPanelButton(root, CharacterTestFairyGuiSettingsPanelIds.Apply).onClick.Call();

                Assert.AreEqual(1, sink.Count);
                Assert.AreEqual(CharacterTestFairyGuiSettingsPanelCommandIds.Apply, sink.Last.CommandId);
                var editPayload = (CharacterTestFairyGuiSettingsPanelEditPayload)sink.Last.Payload;
                Assert.AreEqual("1.5", editPayload.RenderScaleText);
                Assert.AreEqual("80", editPayload.ShadowDistanceText);
                Assert.AreEqual("0.35", editPayload.AudioVolumeText);

                SettingsPanelButton(root, CharacterTestFairyGuiSettingsPanelIds.AntiAliasingNext).onClick.Call();
                Assert.AreEqual(2, sink.Count);
                Assert.AreEqual(CharacterTestFairyGuiSettingsPanelCommandIds.AntiAliasingNext, sink.Last.CommandId);
                Assert.IsNull(sink.Last.Payload);

                SettingsPanelButton(root, CharacterTestFairyGuiSettingsPanelIds.LanguageNext).onClick.Call();
                Assert.AreEqual(3, sink.Count);
                Assert.AreEqual(CharacterTestFairyGuiSettingsPanelCommandIds.LanguageNext, sink.Last.CommandId);
                Assert.IsNull(sink.Last.Payload);

                SettingsPanelButton(root, CharacterTestFairyGuiSettingsPanelIds.AudioCategoryNext).onClick.Call();
                Assert.AreEqual(4, sink.Count);
                Assert.AreEqual(CharacterTestFairyGuiSettingsPanelCommandIds.AudioCategoryNext, sink.Last.CommandId);
                Assert.IsNull(sink.Last.Payload);

                SettingsPanelButton(root, CharacterTestFairyGuiSettingsPanelIds.GamepadTab).onClick.Call();
                Assert.AreEqual(5, sink.Count);
                Assert.AreEqual(CharacterTestFairyGuiSettingsPanelCommandIds.SelectGamepad, sink.Last.CommandId);
                Assert.IsNull(sink.Last.Payload);

                SettingsPanelButton(root, CharacterTestFairyGuiSettingsPanelIds.NavAudio).onClick.Call();
                Assert.AreEqual(6, sink.Count);
                Assert.AreEqual(CharacterTestFairyGuiSettingsPanelCommandIds.SelectAudio, sink.Last.CommandId);
                Assert.IsNull(sink.Last.Payload);

                SettingsPanelButton(root, CharacterTestFairyGuiSettingsPanelIds.CancelChanges).onClick.Call();
                Assert.AreEqual(7, sink.Count);
                Assert.AreEqual(CharacterTestFairyGuiSettingsPanelCommandIds.CancelChanges, sink.Last.CommandId);
                Assert.IsNull(sink.Last.Payload);

                SettingsPanelButton(root, CharacterTestFairyGuiSettingsPanelIds.Close).onClick.Call();
                Assert.AreEqual(8, sink.Count);
                Assert.AreEqual(CharacterTestFairyGuiSettingsPanelCommandIds.Close, sink.Last.CommandId);
                Assert.IsNull(sink.Last.Payload);
            }
        }

[Test]
        public void FairyGuiSettingsPanelCommandSink_UpdatesWorkingProfileAndCallsHostMethods()
        {
            var store = new RecordingSettingsStore();
            var applier = new RecordingSettingsApplier();
            var rebinding = new RecordingInputRebindingService();
            var runtime = new CharacterTestSettingsRuntime(
                "settings.json",
                new MxSettingsService(store),
                new MxInputRebindingSettingsController(rebinding, null),
                MxSettingsLoadResult.Succeeded(MxSettingsProfile.CreateDefault()),
                applier,
                inputBindingTargets: new[] { new MxInputBindingTarget("Gameplay/Jump", 0) });
            CharacterTestLocalizationRuntime localizationRuntime = CharacterTestLocalizationRuntime.Create(
                GetCharacterTestLocalizationPath(),
                NullRuntimeLogger.Instance);
            int closeCount = 0;
            var sink = new CharacterTestFairyGuiSettingsPanelCommandSink(runtime, () =>
            {
                closeCount++;
                return true;
            },
            localizationRuntime);

            sink.Enqueue(new MxUiCommand(
                CharacterTestFairyGuiSettingsPanelIds.ViewId,
                CharacterTestFairyGuiSettingsPanelCommandIds.AntiAliasingNext,
                null));
            sink.Enqueue(new MxUiCommand(
                CharacterTestFairyGuiSettingsPanelIds.ViewId,
                CharacterTestFairyGuiSettingsPanelCommandIds.DirectionalShadowQualityNext,
                null));
            sink.Enqueue(new MxUiCommand(
                CharacterTestFairyGuiSettingsPanelIds.ViewId,
                CharacterTestFairyGuiSettingsPanelCommandIds.ShadowCascadeCountNext,
                null));
            sink.Enqueue(new MxUiCommand(
                CharacterTestFairyGuiSettingsPanelIds.ViewId,
                CharacterTestFairyGuiSettingsPanelCommandIds.LocalShadowResolutionNext,
                null));
            sink.Enqueue(new MxUiCommand(
                CharacterTestFairyGuiSettingsPanelIds.ViewId,
                CharacterTestFairyGuiSettingsPanelCommandIds.ToggleVSync,
                null));
            sink.Enqueue(new MxUiCommand(
                CharacterTestFairyGuiSettingsPanelIds.ViewId,
                CharacterTestFairyGuiSettingsPanelCommandIds.ToggleMute,
                null));

            Assert.AreEqual(1, runtime.CurrentProfile.Graphics.AntiAliasingSamples);
            Assert.AreEqual(MxGraphicsFeatureQuality.Low, runtime.CurrentProfile.Graphics.DirectionalShadowQuality);
            Assert.AreEqual(0, runtime.CurrentProfile.Graphics.MainLightShadowmapResolution);
            Assert.AreEqual(1, runtime.CurrentProfile.Graphics.ShadowCascadeCount);
            Assert.AreEqual(256, runtime.CurrentProfile.Graphics.AdditionalLightsShadowmapResolution);
            Assert.AreEqual(1, runtime.CurrentProfile.Graphics.VSyncCount);
            Assert.IsTrue(runtime.CurrentProfile.Audio.BusSettings[0].Muted);
            Assert.IsTrue(runtime.SettingsUi.Dirty);

            sink.Enqueue(new MxUiCommand(
                CharacterTestFairyGuiSettingsPanelIds.ViewId,
                CharacterTestFairyGuiSettingsPanelCommandIds.LanguageNext,
                null));

            Assert.AreEqual("en-US", runtime.CurrentProfile.Localization.LocaleId);
            Assert.AreEqual(new MxUiLocaleId("en-US"), localizationRuntime.TextProvider.CurrentLocale);
            Assert.AreEqual("Language changed.", sink.LastStatusMessage);

            sink.Enqueue(new MxUiCommand(
                CharacterTestFairyGuiSettingsPanelIds.ViewId,
                CharacterTestFairyGuiSettingsPanelCommandIds.AudioCategoryNext,
                null));
            Assert.AreEqual(MxFramework.Audio.AudioMixBusIds.Music, runtime.SelectedAudioBusId);

            sink.Enqueue(new MxUiCommand(
                CharacterTestFairyGuiSettingsPanelIds.ViewId,
                CharacterTestFairyGuiSettingsPanelCommandIds.Apply,
                new CharacterTestFairyGuiSettingsPanelEditPayload
                {
                    RenderScaleText = "1.35",
                    ShadowDistanceText = "120",
                    AudioVolumeText = "0.42"
                }));

            Assert.AreEqual(1, sink.ApplyCount);
            Assert.AreSame(sink.LastApplyResult, runtime.LastApplyResult);
            Assert.AreEqual(1, applier.ApplyCount);
            Assert.AreEqual(1.35f, applier.LastProfile.Graphics.RenderScale);
            Assert.AreEqual(120f, applier.LastProfile.Graphics.ShadowDistance);
            Assert.AreEqual(0.42f, applier.LastProfile.Audio.BusSettings[1].Volume);
            Assert.AreEqual(1, sink.SaveCount);
            Assert.AreEqual(1, store.SaveCount);
            Assert.AreEqual(1, rebinding.SaveCount);
            Assert.IsFalse(runtime.SettingsUi.Dirty);

            sink.Enqueue(new MxUiCommand(
                CharacterTestFairyGuiSettingsPanelIds.ViewId,
                CharacterTestFairyGuiSettingsPanelCommandIds.Save,
                new CharacterTestFairyGuiSettingsPanelEditPayload
                {
                    RenderScaleText = "1.1",
                    AudioVolumeText = "0.5"
                }));

            Assert.AreEqual(2, sink.SaveCount);
            Assert.AreSame(sink.LastSaveResult, runtime.LastSaveResult);
            Assert.AreEqual(2, store.SaveCount);
            Assert.AreEqual(1.1f, store.SavedProfile.Graphics.RenderScale);
            Assert.AreEqual(0.5f, store.SavedProfile.Audio.BusSettings[1].Volume);
            Assert.IsFalse(runtime.SettingsUi.Dirty);

            sink.Enqueue(new MxUiCommand(
                CharacterTestFairyGuiSettingsPanelIds.ViewId,
                CharacterTestFairyGuiSettingsPanelCommandIds.StartInputRebind,
                new CharacterTestFairyGuiSettingsPanelInputPayload
                {
                    RowIndex = 0,
                    Target = new MxInputBindingTarget("Gameplay/Jump", 0)
                }));
            sink.Enqueue(new MxUiCommand(
                CharacterTestFairyGuiSettingsPanelIds.ViewId,
                CharacterTestFairyGuiSettingsPanelCommandIds.SaveInput,
                null));
            sink.Enqueue(new MxUiCommand(
                CharacterTestFairyGuiSettingsPanelIds.ViewId,
                CharacterTestFairyGuiSettingsPanelCommandIds.ResetInput,
                null));

            Assert.AreEqual(1, rebinding.StartCount);
            Assert.AreEqual(2, rebinding.SaveCount);
            Assert.AreEqual(1, rebinding.ResetCount);

            sink.Enqueue(new MxUiCommand(
                CharacterTestFairyGuiSettingsPanelIds.ViewId,
                CharacterTestFairyGuiSettingsPanelCommandIds.Reload,
                null));

            Assert.AreEqual(1, sink.ReloadCount);
            Assert.AreSame(sink.LastLoadResult, runtime.LastLoadResult);
            Assert.AreEqual(1, store.LoadCount);

            sink.Enqueue(new MxUiCommand(
                CharacterTestFairyGuiSettingsPanelIds.ViewId,
                CharacterTestFairyGuiSettingsPanelCommandIds.Close,
                null));
            sink.Enqueue(new MxUiCommand(
                CharacterTestFairyGuiSettingsPanelIds.ViewId,
                MxFairyGuiCommandIds.Cancel,
                null));
            Assert.AreEqual(2, closeCount);
            Assert.AreEqual(2, sink.CloseCount);
            Assert.AreEqual(1, sink.ApplyCount);
            Assert.AreEqual(2, sink.SaveCount);
            Assert.AreEqual(3, sink.ReloadCount);
            Assert.AreEqual(3, store.LoadCount);
            Assert.AreEqual(2, rebinding.LoadCount);
        }

        [Test]
        public void FairyGuiSettingsPanelFeedbackRoute_PlaysGeneratedActivationAndRealOutcomes()
        {
            var runtime = new CharacterTestSettingsRuntime(
                "settings.json",
                new MxSettingsService(new RecordingSettingsStore()),
                new MxInputRebindingSettingsController(
                    new RecordingInputRebindingService(),
                    null),
                MxSettingsLoadResult.Succeeded(MxSettingsProfile.CreateDefault()),
                new RecordingSettingsApplier(),
                inputBindingTargets: new[]
                {
                    new MxInputBindingTarget("Gameplay/Jump", 0)
                });
            int closeCount = 0;
            var businessSink =
                new CharacterTestFairyGuiSettingsPanelCommandSink(
                    runtime,
                    () =>
                    {
                        closeCount++;
                        return true;
                    });
            MxUiFeedbackPolicyRegistry policies =
                MxFairyGuiManifestProjection.CreateFeedbackPolicyRegistry(
                    CharacterTestFairyGuiSettingsPanelManifest.Create());
            var player = new RecordingUiFeedbackPlayer();
            using var route = new MxUiFeedbackCommandSink(
                businessSink,
                policies,
                player);

            route.Enqueue(new MxUiCommand(
                CharacterTestFairyGuiSettingsPanelIds.ViewId,
                CharacterTestFairyGuiSettingsPanelCommandIds.Apply,
                new CharacterTestFairyGuiSettingsPanelEditPayload()));
            route.Enqueue(new MxUiCommand(
                CharacterTestFairyGuiSettingsPanelIds.ViewId,
                CharacterTestFairyGuiSettingsPanelCommandIds.StartInputRebind,
                null));
            route.Enqueue(new MxUiCommand(
                CharacterTestFairyGuiSettingsPanelIds.ViewId,
                CharacterTestFairyGuiSettingsPanelCommandIds.AntiAliasingNext,
                null));
            route.Enqueue(new MxUiCommand(
                CharacterTestFairyGuiSettingsPanelIds.ViewId,
                CharacterTestFairyGuiSettingsPanelCommandIds.Close,
                null));

            CollectionAssert.AreEqual(
                new[]
                {
                    MxUiFeedbackKind.Confirm,
                    MxUiFeedbackKind.Error,
                    MxUiFeedbackKind.Click,
                    MxUiFeedbackKind.Back
                },
                player.Kinds);
            Assert.AreEqual(4, businessSink.CommandCount);
            Assert.AreEqual(1, closeCount);
            Assert.Greater(businessSink.LastCommand.InvocationId, 0L);
            Assert.AreEqual(0, route.CaptureDebugSnapshot().PendingCount);
        }

        [Test]
        public void FairyGuiNetworkPanelHost_WithPublishedPackage_LoadsBindsAndReleases()
        {
            RemoveCharacterTestLegacyPanelPackageIfLoaded();
            Texture2D combatHudAtlas = null;
            GlobalResourceRuntimeServices resources = null;
            GameSlice slice = null;
            var cameraObject = new GameObject("CharacterTest Network Panel Main Camera Test");
            var hostObject = new GameObject("CharacterTest Network Panel Host Test");
            try
            {
                resources = CreateNetworkPanelResourceServices(out combatHudAtlas);
                slice = new GameSlice(
                    NullRuntimeLogger.Instance,
                    CharacterTestStoryFixture.CreateBootstrapContent(),
                    resources,
                    baseResourcePreloadGroupId: string.Empty);
                var controller = new RecordingNetworkSessionController();
                controller.Status = CharacterTestNetworkSessionStatus.Offline("Offline");

                cameraObject.tag = "MainCamera";
                cameraObject.AddComponent<UnityEngine.Camera>();
                CharacterTestFairyGuiNetworkPanelHost host =
                    hostObject.AddComponent<CharacterTestFairyGuiNetworkPanelHost>();

                host.Configure(
                    slice,
                    controller,
                    logger: NullRuntimeLogger.Instance);

                Assert.IsTrue(host.IsConfigured, host.LastStatusMessage);
                Assert.IsTrue(host.IsOpen, host.LastStatusMessage);
                Assert.AreEqual(1, host.SuccessfulRefreshCount);
                Assert.NotNull(host.LastViewModel);
                Assert.AreEqual("Offline", host.LastViewModel.StatusText);
                Assert.AreEqual(CharacterTestFairyGuiNetworkPanelIds.ComponentName, "NetworkPanel");
                AssertPublishedNetworkPanelControlTypes();

                host.Refresh();

                Assert.AreEqual(1, host.SuccessfulRefreshCount);
                Assert.AreEqual(1, host.SkippedRefreshCount);

                Assert.IsTrue(host.Close());
                Assert.IsFalse(host.IsOpen);

                Assert.DoesNotThrow(() => host.Release());
                Assert.DoesNotThrow(() => host.Release());
            }
            finally
            {
                CharacterTestFairyGuiNetworkPanelHost host =
                    hostObject.GetComponent<CharacterTestFairyGuiNetworkPanelHost>();
                host?.Release();
                slice?.Dispose();
                resources?.Dispose();
                UnityEngine.Object.DestroyImmediate(hostObject);
                UnityEngine.Object.DestroyImmediate(cameraObject);
                DestroyObjectNamed("Stage Camera");
                if (combatHudAtlas != null)
                    UnityEngine.Object.DestroyImmediate(combatHudAtlas);
                RemoveCharacterTestLegacyPanelPackageIfLoaded();
            }
        }

[Test]
        public void FairyGuiSettingsPanelHost_WithPublishedPackage_LoadsBindsAndReleases()
        {
            RemoveCharacterTestLegacyPanelPackageIfLoaded();
            Texture2D combatHudAtlas = null;
            GlobalResourceRuntimeServices resources = null;
            GameSlice slice = null;
            var cameraObject = new GameObject("CharacterTest Settings Panel Main Camera Test");
            var hostObject = new GameObject("CharacterTest Settings Panel Host Test");
            try
            {
                resources = CreateSettingsPanelResourceServices(out combatHudAtlas);
                slice = new GameSlice(
                    NullRuntimeLogger.Instance,
                    CharacterTestStoryFixture.CreateBootstrapContent(),
                    resources,
                    baseResourcePreloadGroupId: string.Empty);
                CharacterTestLocalizationRuntime localizationRuntime = CharacterTestLocalizationRuntime.Create(
                    GetCharacterTestLocalizationPath(),
                    NullRuntimeLogger.Instance);
                var rebinding = new RecordingInputRebindingEditSessionService();
                var settingsRuntime = new CharacterTestSettingsRuntime(
                    "settings.json",
                    new MxSettingsService(null),
                    new MxInputRebindingSettingsController(rebinding, null),
                    MxSettingsLoadResult.Succeeded(MxSettingsProfile.CreateDefault()),
                    localizationOptions: localizationRuntime.LocaleOptions);

                cameraObject.tag = "MainCamera";
                cameraObject.AddComponent<UnityEngine.Camera>();
                CharacterTestFairyGuiSettingsPanelHost host =
                    hostObject.AddComponent<CharacterTestFairyGuiSettingsPanelHost>();

                host.Configure(
                    slice,
                    settingsRuntime,
                    localizationRuntime,
                    localizationRuntime.TextProvider,
                    NullRuntimeLogger.Instance);

                Assert.IsTrue(host.IsConfigured, host.LastStatusMessage);
                Assert.IsFalse(host.IsOpen, host.LastStatusMessage);
                Assert.AreEqual(0, host.SuccessfulRefreshCount);
                Assert.IsNull(host.LastViewModel);

                host.Refresh();
                Assert.IsFalse(host.IsOpen);
                Assert.AreEqual(0, host.SuccessfulRefreshCount);

                Assert.IsTrue(host.Open(), host.LastStatusMessage);
                Assert.IsTrue(host.IsOpen, host.LastStatusMessage);
                Assert.IsTrue(rebinding.IsEditSessionActive);
                Assert.AreEqual(1, rebinding.BeginEditSessionCount);
                Assert.AreEqual(1, host.SuccessfulRefreshCount);
                Assert.NotNull(host.LastViewModel);
                Assert.AreEqual("SettingsPanel", CharacterTestFairyGuiSettingsPanelIds.ComponentName);
                Assert.AreEqual("设置", host.LastViewModel.Title);
                Assert.AreEqual("简体中文", host.LastViewModel.LocaleText);
                Assert.AreEqual("Master", host.LastViewModel.AudioCategoryText);
                Assert.AreEqual("1", host.LastViewModel.AudioVolumeText);
                AssertPublishedSettingsPanelControlTypes();

                host.Refresh();

                Assert.AreEqual(1, host.SuccessfulRefreshCount);
                Assert.AreEqual(1, host.SkippedRefreshCount);

                Assert.IsTrue(host.Close());
                Assert.IsFalse(host.IsOpen);
                Assert.IsFalse(rebinding.IsEditSessionActive);
                Assert.AreEqual(1, rebinding.RollbackEditSessionCount);
                host.Refresh();
                Assert.IsFalse(host.IsOpen);
                Assert.AreEqual(1, host.SuccessfulRefreshCount);
                Assert.DoesNotThrow(() => host.Release());
                Assert.DoesNotThrow(() => host.Release());
            }
            finally
            {
                CharacterTestFairyGuiSettingsPanelHost host =
                    hostObject.GetComponent<CharacterTestFairyGuiSettingsPanelHost>();
                host?.Release();
                slice?.Dispose();
                resources?.Dispose();
                UnityEngine.Object.DestroyImmediate(hostObject);
                UnityEngine.Object.DestroyImmediate(cameraObject);
                DestroyObjectNamed("Stage Camera");
                if (combatHudAtlas != null)
                    UnityEngine.Object.DestroyImmediate(combatHudAtlas);
                RemoveCharacterTestLegacyPanelPackageIfLoaded();
            }
        }

[Test]
        public void FairyGuiRuntimeAiLabTimeline_PublishedPackageCreatesFormalComponents()
        {
            RemoveCharacterTestLegacyPanelPackageIfLoaded();
            Fgui.FontManager.Clear();
            Texture2D legacyPanelAtlas = null;
            GlobalResourceRuntimeServices resources = null;
            MxFairyGuiPackageLoadScope packageScope = null;
            GameObject gestureControllerHost = null;
            var host = new MxFairyGuiHost();
            bool packageEnsured = false;
            try
            {
                resources = CreatePlayerPanelResourceServices(
                    CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.PackageId,
                    CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.PackageBytesKey,
                    CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.Atlas0Key,
                    out legacyPanelAtlas);
                var bridge = new MxFairyGuiResourceBridge(resources.ResourceManager);
                var package = CharacterTestFairyGuiRuntimeAiPlannerDevPanelManifest.CreatePackageDescriptor();
                MxFairyGuiPackageLoadResult load = bridge.LoadPackage(package);
                Assert.IsTrue(load.Success, load.Failure.Message);
                packageScope = load.Scope;

                Assert.IsTrue(host.EnsurePackage(packageScope, out string failure), failure);
                packageEnsured = true;
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelViewModel viewModel = null;
                const int gestureEntryIndex = 2;
                gestureControllerHost = new GameObject("Runtime AI Lab Timeline Gesture Test");
                CharacterTestRuntimeAiPlannerDevSceneController gestureController =
                    gestureControllerHost.AddComponent<CharacterTestRuntimeAiPlannerDevSceneController>();
                gestureController.PrepareInitialSetupState();
                Assert.IsTrue(
                    gestureController.TryCreateReactiveChaseDodgeGroup(0, out string presetMessage),
                    presetMessage);
                if (gestureController.AuthoringMode != CharacterTestRuntimeAiLabAuthoringMode.Developer)
                    gestureController.ToggleAuthoringMode();
                Assert.IsTrue(gestureController.DraftWorkspace.TryGetGroupForSlot(
                    gestureController.SelectedSetupTeamId,
                    gestureController.SelectedSetupSlotIndex,
                    out CharacterTestRuntimeAiLabBehaviorGroupDraft gestureGroup));
                CharacterTestRuntimeAiLabBehaviorEntryDraft gestureEntry =
                    gestureGroup.Entries[gestureEntryIndex];
                Assert.IsTrue(gestureController.DraftWorkspace.Cards.TryGetValue(
                    gestureEntry.CardDraftId,
                    out CharacterTestRuntimeAiLabBehaviorCardDraft gestureCard));
                Assert.IsTrue(
                    CharacterTestRuntimeAiLabBehaviorCardGraphMutation.TryCanonicalizeDetectorWindows(
                        gestureCard.LogicPayload,
                        out _,
                        out string canonicalizationDiagnostic),
                    canonicalizationDiagnostic);
                var commandSink = new RecordingUiCommandSink();
                var gestureHandler = new CharacterTestRuntimeAiLabBehaviorCardWorkspaceCommandHandler();
                gestureHandler.Configure(
                    gestureController,
                    null,
                    null,
                    () => { },
                    () => gestureEntryIndex,
                    workspaceView: () => viewModel?.BehaviorCardWorkspace);
                var binder = new CharacterTestFairyGuiRuntimeAiPlannerDevPanelBinder(
                    commandSink,
                    CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.ViewId,
                    null,
                    null,
                    gestureHandler);
                Assert.IsTrue(host.TryCreateComponent(
                    CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.PackageId,
                    CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.ComponentName,
                    out IMxFairyGuiComponentHandle panelHandle,
                    out failure), failure);
                using (panelHandle)
                {
                    var concrete = panelHandle as MxFairyGuiComponentHandle;
                    Assert.NotNull(concrete);
                    Fgui.GComponent timeline = concrete.Component.GetChild(
                        CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.RuntimeFrameWorkspace)?.asCom;
                    Assert.NotNull(timeline);
                    Fgui.GComponent runtimeFrameViewport = timeline.GetChild(
                        "runtimeFrameFiveTrackViewport")?.asCom;
                    Assert.NotNull(
                        runtimeFrameViewport,
                        "RuntimeFrame shell children: "
                        + string.Join(", ", Enumerable.Range(0, timeline.numChildren)
                            .Select(index => timeline.GetChildAt(index)?.name ?? "<null>")));
                    Assert.NotNull(runtimeFrameViewport.GetChild("timelineViewport"));
                    Assert.NotNull(runtimeFrameViewport.GetChild("timelineScrubHitArea"));
                    Assert.IsNull(timeline.GetChild("timelineResizeHandle"));
                    Assert.NotNull(runtimeFrameViewport.GetChild("timelineFutureAdvanceFill"));
                    Fgui.GComponent timelineEventDetails =
                        timeline.GetChild("timelineEventDetails")?.asCom;
                    Assert.NotNull(timelineEventDetails);
                    Assert.AreEqual(
                        26,
                        timelineEventDetails.GetChild("detailTitle")?.asTextField?.textFormat.size);
                    Assert.AreEqual(
                        24,
                        timelineEventDetails.GetChild("detailFrame")?.asTextField?.textFormat.size);
                    Assert.AreEqual(52f, timelineEventDetails.GetChild("btnCloseDetail")?.width);
                    Assert.AreEqual(56f, timelineEventDetails.GetChild("btnLocateStart")?.height);
                    Assert.NotNull(runtimeFrameViewport.GetChild("trackRevision"));
                    Assert.NotNull(runtimeFrameViewport.GetChild("trackWarnings"));
                    Assert.IsNull(timeline.GetChild("timelineRevisionTrackText"));
                    Assert.IsNull(timeline.GetChild("timelineWarningTrackText"));
                    Assert.NotNull(timeline.GetChild("btnFirstFrame"));
                    Assert.NotNull(timeline.GetChild("btnStepBack10"));
                    Assert.NotNull(timeline.GetChild("btnStepBack"));
                    Assert.NotNull(timeline.GetChild("btnTimelinePlayPause"));
                    Assert.NotNull(timeline.GetChild("btnNextFrame"));
                    Assert.NotNull(timeline.GetChild("btnStepForward10"));
                    Assert.NotNull(timeline.GetChild("btnLatestFrame"));
                    Assert.IsNull(timeline.GetChild("btnStep"));
                    Assert.NotNull(timeline.GetChild("btnTimelineCollapse"));
                    Assert.NotNull(timeline.GetChild("btnTimelineMore"));
                    Assert.IsNull(timeline.GetChild("compactTrackContent"));
                    Assert.NotNull(timeline.GetChild("btnBranchRecompute"));
                    Assert.NotNull(timeline.GetChild("branchRecomputeRiskText"));
                    Assert.NotNull(
                        runtimeFrameViewport.GetChild("trackRevision")?.asCom?.GetChild("trackIcon"));
                    Assert.NotNull(
                        runtimeFrameViewport.GetChild("trackWarnings")?.asCom?.GetChild("trackIcon"));
                    Fgui.GComponent behaviorCardLibrary = concrete.Component.GetChild(
                        CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.BehaviorCardLibrary)?.asCom;
                    Fgui.GComponent behaviorCardTimeline = concrete.Component.GetChild(
                        CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.BehaviorCardTimeline)?.asCom;
                    Assert.NotNull(behaviorCardLibrary);
                    Assert.NotNull(behaviorCardTimeline);
                    Fgui.GComponent conditionPicker = Fgui.UIPackage.CreateObject(
                        CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.PackageId,
                        "RuntimeAiLabBehaviorConditionPicker")?.asCom;
                    Assert.NotNull(conditionPicker);
                    Assert.NotNull(conditionPicker.GetChild("btnTarget"));
                    Assert.NotNull(conditionPicker.GetChild("btnSelf"));
                    Assert.NotNull(conditionPicker.GetChild("btnEnvironment"));
                    Assert.NotNull(conditionPicker.GetChild("btnEventResult"));
                    Assert.NotNull(conditionPicker.GetChild("propertyList"));
                    conditionPicker.Dispose();
                    Fgui.GComponent recipeInspector = Fgui.UIPackage.CreateObject(
                        CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.PackageId,
                        "RuntimeAiLabBehaviorRecipeInspector")?.asCom;
                    Assert.NotNull(recipeInspector);
                    Assert.NotNull(recipeInspector.GetChild("btnAddConditionGroup"));
                    Assert.NotNull(recipeInspector.GetChild("conditionPickerPrototype"));
                    Assert.NotNull(recipeInspector.GetChild("targetStatePickerPrototype"));
                    recipeInspector.Dispose();
                    Fgui.GComponent conditionRow = Fgui.UIPackage.CreateObject(
                        CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.PackageId,
                        "RuntimeAiLabBehaviorTriggerConditionRow")?.asCom;
                    Assert.NotNull(conditionRow);
                    Assert.NotNull(conditionRow.GetChild("observation"));
                    Assert.NotNull(conditionRow.GetChild("subjectScopeTitle")?.asTextField);
                    conditionRow.Dispose();
                    Assert.NotNull(concrete.Component.GetChild(
                        CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.TimelineModeHost));
                    Assert.NotNull(concrete.Component.GetChild(
                        CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.TimelineModeRuntimeTrace));
                    Assert.NotNull(concrete.Component.GetChild(
                        CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.TimelineModeBehaviorPlan));
                    Assert.NotNull(concrete.Component.GetChild(
                        CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.TimelineModeBehaviorLogic));
                    Fgui.GComponent factsWorkspace = concrete.Component.GetChild(
                        CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.FactsWorkspace)?.asCom;
                    Assert.NotNull(factsWorkspace);
                    Assert.NotNull(factsWorkspace.GetChild("workspaceBackground"));
                    Assert.NotNull(factsWorkspace.GetChild("contextText"));
                    Assert.NotNull(factsWorkspace.GetChild("agentRow8"));
                    Assert.NotNull(factsWorkspace.GetChild("factRow12"));
                    Assert.NotNull(factsWorkspace.GetChild("candidateRow9"));
                    Assert.NotNull(factsWorkspace.GetChild("causalStep4"));
                    Assert.NotNull(factsWorkspace.GetChild("causalArrow1"));
                    Assert.NotNull(factsWorkspace.GetChild("causalArrow2"));
                    Assert.NotNull(factsWorkspace.GetChild("causalArrow3"));
                    Assert.NotNull(factsWorkspace.GetChild("commitPhase3"));
                    Assert.NotNull(factsWorkspace.GetChild("btnReturnSourceFrame"));
                    Assert.IsNull(concrete.Component.GetChild("liveWorkspace"));
                    Assert.NotNull(behaviorCardLibrary.GetChild("libraryQuery"));
                    Assert.NotNull(behaviorCardLibrary.GetChild("btnLibraryFilter"));
                    Assert.NotNull(behaviorCardLibrary.GetChild("assemblyResponsibilityAll")?.asButton);
                    Assert.NotNull(behaviorCardLibrary.GetChild("assemblyResponsibilityTargeting")?.asButton);
                    Assert.NotNull(behaviorCardLibrary.GetChild("assemblyResponsibilityEngage")?.asButton);
                    Assert.NotNull(behaviorCardLibrary.GetChild("assemblyResponsibilityDamage")?.asButton);
                    Assert.NotNull(behaviorCardLibrary.GetChild("assemblyResponsibilityFallback")?.asButton);
                    Assert.NotNull(behaviorCardLibrary.GetChild("assemblyResponsibilityOther")?.asButton);
                    Assert.NotNull(behaviorCardLibrary.GetChild("assemblyClose")?.asButton);
                    Assert.NotNull(behaviorCardTimeline.GetChild("timelineGroupHost")?.asCom);
                    Assert.NotNull(behaviorCardTimeline.GetChild("timelineGroupPrototype")?.asCom);
                    Assert.NotNull(behaviorCardTimeline.GetChild("trackPrototype")?.asCom);
                    Assert.NotNull(behaviorCardTimeline.GetChild("spanPrototype")?.asCom);
                    Assert.NotNull(behaviorCardTimeline.GetChild("markerPrototype")?.asCom);
                    Assert.NotNull(behaviorCardTimeline.GetChild("timelinePlayhead")?.asGraph);
                    Assert.NotNull(behaviorCardTimeline.GetChild("btnTimelineZoomOut"));
                    Assert.NotNull(behaviorCardTimeline.GetChild("btnTimelineZoomIn"));
                    Assert.NotNull(behaviorCardTimeline.GetChild("btnTimelineFit"));
                    Assert.NotNull(behaviorCardTimeline.GetChild("timelineHorizontalScrollTrack"));
                    Assert.NotNull(behaviorCardTimeline.GetChild("timelineHorizontalScrollThumb"));
                    Assert.NotNull(behaviorCardTimeline.GetChild("timelineVerticalScrollTrack"));
                    Assert.NotNull(behaviorCardTimeline.GetChild("timelineVerticalScrollThumb"));
                    Assert.NotNull(concrete.Component.GetChild(
                        CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.DraftHashText));
                    Fgui.GList workbenchList = concrete.Component.GetChild(
                        CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.WorkbenchEntryList)?.asList;
                    Assert.NotNull(workbenchList);
                    Assert.NotNull(workbenchList.scrollPane);
                    Assert.IsNotEmpty(workbenchList.defaultItem);
                    Assert.NotNull(concrete.Component.GetChild(
                        CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.ResponsibilityTabAll)?.asButton);
                    Assert.NotNull(concrete.Component.GetChild(
                        CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.ResponsibilityTabTargeting)?.asButton);
                    Assert.NotNull(concrete.Component.GetChild(
                        CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.ResponsibilityTabEngage)?.asButton);
                    Assert.NotNull(concrete.Component.GetChild(
                        CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.ResponsibilityTabDamage)?.asButton);
                    Assert.NotNull(concrete.Component.GetChild(
                        CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.ResponsibilityTabFallback)?.asButton);
                    Assert.NotNull(concrete.Component.GetChild(
                        CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.ResponsibilityTabOther)?.asButton);
                    workbenchList.numItems = 1;
                    Fgui.GComponent groupCard = workbenchList.GetChildAt(0)?.asCom;
                    Assert.NotNull(groupCard);
                    Assert.NotNull(groupCard.GetChild("triggerText"));
                    Assert.NotNull(groupCard.GetChild("actionText"));
                    Assert.NotNull(groupCard.GetChild("priorityText"));
                    Assert.NotNull(groupCard.GetChild("inlineToggleEntry"));
                    Assert.NotNull(groupCard.GetChild("inlineDeleteEntry"));
                    workbenchList.numItems = 0;

                    viewModel = new CharacterTestFairyGuiRuntimeAiPlannerDevPanelViewModel
                    {
                        WorkspaceMode = CharacterTestRuntimeAiLabWorkspaceMode.Diagnostics,
                        DiagnosticsTab = CharacterTestRuntimeAiLabDiagnosticsTab.RuntimeFrame,
                        TimelineMode = CharacterTestRuntimeAiLabTimelineMode.RuntimeTrace,
                        ShowTimelineModeHost = true,
                        ShowTimeline = true,
                        CanSwitchTimelineMode = true,
                        RuntimeTraceModeSelected = true,
                        RuntimeTraceExpanded = true,
                        BehaviorPlanExpanded = true,
                        TimelineExpanded = true,
                        CanJumpFrame = true,
                        CanToggleTimelineExpanded = true,
                        CanZoomTimeline = true,
                        CanPanTimeline = true,
                        CanPauseResume = true,
                        CanStep = true,
                        CanChangeSpeed = true,
                        CanReset = true,
                        PresentationContext =
                            CharacterTestRuntimeAiPlannerDevPresentationContext.PausedInspect,
                        ShowPausedToolbar = true,
                        TimelineProjection = new CharacterTestRuntimeAiLabTimelineProjection(
                            contentRevision: 1,
                            new CharacterTestRuntimeAiLabTimelineDomain(3, 3, 3, 3, 900, 1),
                            new CharacterTestRuntimeAiLabTimelineViewport(3, 902, -1, 1200f, true),
                            new[]
                            {
                                new CharacterTestRuntimeAiLabTimelineTrackProjection(CharacterTestRuntimeAiLabTimelineTrackKind.Revision, "版本", Array.Empty<CharacterTestRuntimeAiLabTimelineItem>()),
                                new CharacterTestRuntimeAiLabTimelineTrackProjection(CharacterTestRuntimeAiLabTimelineTrackKind.Decision, "决策", Array.Empty<CharacterTestRuntimeAiLabTimelineItem>()),
                                new CharacterTestRuntimeAiLabTimelineTrackProjection(CharacterTestRuntimeAiLabTimelineTrackKind.Action, "动作", Array.Empty<CharacterTestRuntimeAiLabTimelineItem>()),
                                new CharacterTestRuntimeAiLabTimelineTrackProjection(CharacterTestRuntimeAiLabTimelineTrackKind.Combat, "命中 / 伤害", Array.Empty<CharacterTestRuntimeAiLabTimelineItem>()),
                                new CharacterTestRuntimeAiLabTimelineTrackProjection(CharacterTestRuntimeAiLabTimelineTrackKind.Warnings, "警告", Array.Empty<CharacterTestRuntimeAiLabTimelineItem>())
                            })
                    };
                    viewModel.Workspace.ActiveWorkspace =
                        CharacterTestRuntimeAiLabWorkspaceMode.Diagnostics;
                    viewModel.Workspace.ActiveDiagnosticsTab =
                        CharacterTestRuntimeAiLabDiagnosticsTab.RuntimeFrame;
                    viewModel.Workspace.ActiveTimelineMode =
                        CharacterTestRuntimeAiLabTimelineMode.RuntimeTrace;
                    viewModel.Workspace.ShowDiagnostics = true;
                    viewModel.RuntimeFrame = new CharacterTestRuntimeAiLabRuntimeFrameWorkspaceViewModel
                    {
                        Available = true,
                        AgentEntityId = 1,
                        AgentSlotLabel = "T1-1",
                        AgentDisplayName = "Codex Flow Test",
                        SelectedFrame = 3,
                        LatestFrame = 3,
                        FrameLimit = 900,
                        CanPauseResume = true,
                        CanJumpFrame = true,
                        CanPan = true,
                        CanZoom = true,
                        Projection = viewModel.TimelineProjection,
                        AgentOptions = new[]
                        {
                            new CharacterTestRuntimeAiLabRuntimeAgentOptionViewModel
                            {
                                AgentIndex = 0,
                                EntityId = 1,
                                TeamId = 1,
                                SlotIndex = 0,
                                SlotLabel = "T1-1",
                                DisplayName = "Codex Flow Test",
                                Selected = true
                            }
                        }
                    };
                    binder.Bind(panelHandle, viewModel);

                    Assert.AreEqual(0f, timeline.x);
                    Assert.AreEqual(0f, timeline.y);
                    Assert.AreEqual(1920f, timeline.width);
                    Assert.AreEqual(1080f, timeline.height);
                    Assert.AreEqual(740f, runtimeFrameViewport.y);
                    Assert.AreEqual(340f, runtimeFrameViewport.height);
                    Assert.AreEqual(60f, runtimeFrameViewport.GetChild("trackRevision").height);
                    Assert.AreEqual(280f, runtimeFrameViewport.GetChild("trackWarnings").y);
                    Assert.IsTrue(
                        timelineEventDetails.visible,
                        "Runtime Trace must retain the permanent upper-right Current Decision surface.");
                    Assert.AreEqual(1400f, timelineEventDetails.x);
                    Assert.AreEqual(668f, timelineEventDetails.height);
                    Assert.AreEqual(
                        64f,
                        timelineEventDetails.GetChild("detailTitle").y,
                        "Decision content must begin below the right-side session header.");
                    Assert.AreEqual(116f, timelineEventDetails.GetChild("causalCard1").y);
                    Assert.Less(
                        timelineEventDetails.GetChild("causalCard1").sortingOrder,
                        timelineEventDetails.GetChild("causalIcon1").sortingOrder);
                    Assert.Less(
                        timelineEventDetails.GetChild("causalIcon1").sortingOrder,
                        timelineEventDetails.GetChild("memberRow1").sortingOrder);
                    Assert.AreEqual(
                        "已暂停 · 第 3 帧",
                        concrete.Component.GetChild(
                            CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.FrameSummary)
                            ?.asTextField?.text);
                    Assert.AreEqual(
                        "T1-1",
                        concrete.Component.GetChild(
                            CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.Phase)
                            ?.asTextField?.text);
                    Assert.AreEqual(
                        "Codex Flow Test",
                        concrete.Component.GetChild(
                            CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.Title)
                            ?.asTextField?.text);
                    Fgui.GObject runtimeTopBar = concrete.Component.GetChild(
                        CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.TopBar);
                    Assert.AreEqual(1400f, runtimeTopBar.x);
                    Assert.AreEqual(0f, runtimeTopBar.y);
                    Assert.AreEqual(520f, runtimeTopBar.width);
                    Assert.AreEqual(60f, runtimeTopBar.height);
                    Assert.GreaterOrEqual(
                        runtimeTopBar.x,
                        1400f,
                        "The session bar must not cover the 1400px-wide Game viewport.");
                    Fgui.GObject runtimeSlotBadge = concrete.Component.GetChild(
                        "runtimeFrameSlotBadge");
                    Assert.AreEqual(1416f, runtimeSlotBadge.x);
                    Assert.AreEqual(10f, runtimeSlotBadge.y);
                    Assert.AreEqual(72f, runtimeSlotBadge.width);
                    Assert.AreEqual(40f, runtimeSlotBadge.height);
                    Fgui.GTextField runtimeTitle = concrete.Component.GetChild(
                        CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.Title)?.asTextField;
                    Fgui.GTextField runtimeStatus = concrete.Component.GetChild(
                        CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.FrameSummary)?.asTextField;
                    Assert.AreEqual(1500f, runtimeTitle.x);
                    Assert.AreEqual(6f, runtimeTitle.y);
                    Assert.AreEqual(180f, runtimeTitle.width);
                    Assert.AreEqual(26f, runtimeTitle.height);
                    Assert.AreEqual(22, runtimeTitle.textFormat.size);
                    Assert.IsFalse(runtimeTitle.onRollOver.isEmpty);
                    Assert.AreEqual(1500f, runtimeStatus.x);
                    Assert.AreEqual(32f, runtimeStatus.y);
                    Assert.AreEqual(180f, runtimeStatus.width);
                    Assert.AreEqual(20f, runtimeStatus.height);
                    Assert.AreEqual(16, runtimeStatus.textFormat.size);
                    Fgui.GButton runtimePause = concrete.Component.GetChild(
                        CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.PauseResume)?.asButton;
                    Fgui.GButton runtimeStep = concrete.Component.GetChild(
                        CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.Step)?.asButton;
                    Fgui.GButton runtimeRestart = concrete.Component.GetChild(
                        CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.Reset)?.asButton;
                    Fgui.GButton runtimeBack = concrete.Component.GetChild(
                        CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.Close)?.asButton;
                    Assert.NotNull(runtimePause);
                    Assert.NotNull(runtimeStep);
                    Assert.NotNull(runtimeRestart);
                    Assert.NotNull(runtimeBack);
                    Assert.AreEqual(1688f, runtimePause.x);
                    Assert.AreEqual(1744f, runtimeStep.x);
                    Assert.AreEqual(1800f, runtimeRestart.x);
                    Assert.AreEqual(1856f, runtimeBack.x);
                    Assert.AreEqual(10f, runtimePause.y);
                    Assert.AreEqual(48f, runtimePause.width);
                    Assert.AreEqual(40f, runtimePause.height);
                    Assert.Less(runtimeTopBar.sortingOrder, runtimeSlotBadge.sortingOrder);
                    Assert.Less(
                        runtimeSlotBadge.sortingOrder,
                        concrete.Component.GetChild(
                            CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.Phase).sortingOrder);
                    Assert.IsFalse(runtimePause.onRollOver.isEmpty);
                    Assert.IsFalse(runtimeStep.onRollOver.isEmpty);
                    Assert.IsFalse(runtimeRestart.onRollOver.isEmpty);
                    Assert.IsFalse(runtimeBack.onRollOver.isEmpty);
                    runtimePause.onClick.Call();
                    Assert.AreEqual(
                        CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.PauseResume,
                        commandSink.Last.CommandId);
                    runtimeStep.onClick.Call();
                    Assert.AreEqual(
                        CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.Step,
                        commandSink.Last.CommandId);
                    runtimeRestart.onClick.Call();
                    Assert.AreEqual(
                        CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.Reset,
                        commandSink.Last.CommandId);
                    runtimeBack.onClick.Call();
                    Assert.AreEqual(
                        CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.Close,
                        commandSink.Last.CommandId);
                    Fgui.GButton duplicateTimelinePause = timeline.GetChild(
                        "btnTimelinePlayPause")?.asButton;
                    Assert.NotNull(duplicateTimelinePause);
                    Assert.IsFalse(duplicateTimelinePause.visible);
                    Assert.IsFalse(duplicateTimelinePause.touchable);
                    Assert.IsFalse(duplicateTimelinePause.enabled);
                    Assert.IsFalse(timeline.GetChild("btnBranchRecompute").visible);
                    Assert.AreEqual(904f, timeline.GetChild("btnNextFrame").x);
                    Assert.AreEqual(978f, timeline.GetChild("btnStepForward10").x);
                    Assert.AreEqual(1166f, timeline.GetChild("btnTimelineZoomOut").x);
                    Fgui.GButton playbackSpeed = timeline.GetChild(
                        CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.TimelinePlaybackSpeed)?.asButton;
                    Assert.NotNull(playbackSpeed);
                    Assert.IsTrue(playbackSpeed.visible);
                    Assert.AreEqual(1076f, playbackSpeed.x);
                    Assert.AreEqual(80f, playbackSpeed.width);
                    Assert.AreEqual(viewModel.SpeedLabel, playbackSpeed.text);
                    Assert.IsFalse(playbackSpeed.onRollOver.isEmpty);
                    playbackSpeed.onClick.Call();
                    Assert.AreEqual(
                        CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.PlaybackSpeed,
                        commandSink.Last.CommandId);
                    Assert.AreEqual(1492f, timeline.GetChild("btnTimelineMore").x);
                    viewModel.RuntimeFrame.SelectedFrame = 2;
                    binder.Bind(panelHandle, viewModel);
                    Assert.IsTrue(timeline.GetChild("btnBranchRecompute").visible);
                    Assert.IsTrue(runtimeFrameViewport.GetChild("timelineScrubHitArea").touchable);
                    Assert.IsFalse(runtimeFrameViewport.GetChild("timelineViewport").touchable);
                    Assert.IsFalse(runtimeFrameViewport.GetChild("timelineFutureMask").touchable);
                    Assert.IsFalse(runtimeFrameViewport.GetChild("timelinePlayhead").touchable);
                    Assert.AreEqual(227f, runtimeFrameViewport.GetChild("timelinePlayhead").x);
                    Assert.AreEqual(224f, runtimeFrameViewport.GetChild("timelineSelectedFrameLabel").x);

                    Fgui.GComponent rootBeforeModeSwitch = concrete.Component;
                    Fgui.GButton traceModeButton = concrete.Component.GetChild(
                        CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds
                            .TimelineModeRuntimeTrace)?.asButton;
                    Fgui.GButton planModeButton = concrete.Component.GetChild(
                        CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds
                            .TimelineModeBehaviorPlan)?.asButton;
                    Fgui.GButton logicModeButton = concrete.Component.GetChild(
                        CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds
                            .TimelineModeBehaviorLogic)?.asButton;
                    Assert.NotNull(traceModeButton);
                    Assert.NotNull(planModeButton);
                    Assert.NotNull(logicModeButton);
                    Assert.AreEqual(
                        CharacterTestRuntimeAiLabTimelineWorkbenchLayout.LogicModeButtonX,
                        logicModeButton.x,
                        "Behavior Logic must be the first timeline mode tab.");
                    Assert.AreEqual(
                        CharacterTestRuntimeAiLabTimelineWorkbenchLayout.PlanModeButtonX,
                        planModeButton.x,
                        "Behavior Plan must remain the middle timeline mode tab.");
                    Assert.AreEqual(
                        CharacterTestRuntimeAiLabTimelineWorkbenchLayout.RuntimeModeButtonX,
                        traceModeButton.x,
                        "Runtime Trace must be the third timeline mode tab.");
                    Assert.IsTrue(
                        traceModeButton.visible,
                        "The Runtime Trace timeline mode button must be visible.");
                    Assert.IsTrue(
                        traceModeButton.selected,
                        "The Runtime Trace timeline mode button must reflect the selected mode.");
                    planModeButton.onClick.Call();
                    Assert.AreEqual(
                        CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds
                            .TimelineModeBehaviorPlan,
                        commandSink.Last.CommandId);
                    logicModeButton.onClick.Call();
                    Assert.AreEqual(
                        CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds
                            .TimelineModeBehaviorLogic,
                        commandSink.Last.CommandId);

                    viewModel.TimelineMode =
                        CharacterTestRuntimeAiLabTimelineMode.BehaviorPlan;
                    viewModel.Workspace.ActiveTimelineMode =
                        CharacterTestRuntimeAiLabTimelineMode.BehaviorPlan;
                    viewModel.ShowTimeline = false;
                    viewModel.ShowBehaviorPlan = true;
                    viewModel.RuntimeTraceModeSelected = false;
                    viewModel.BehaviorPlanModeSelected = true;
                    CharacterTestRuntimeAiLabBehaviorCardLogicPayload planLogicPayload =
                        gestureCard.LogicPayload.Clone();
                    CharacterTestRuntimeAiLabBehaviorCardLogicCompileResult planLogicCompile =
                        CharacterTestRuntimeAiLabBehaviorCardLogicCompiler.Compile(planLogicPayload);
                    CharacterTestRuntimeAiLabBehaviorCardWorkspaceViewModelBuilder.Build(
                        viewModel,
                        "追击突进斩",
                        planLogicCompile,
                        planLogicPayload,
                        selectedStageIndex: 0,
                        behaviorEntryId: gestureEntry.EntryId);
                    CharacterTestRuntimeAiLabBehaviorCardTimelineViewModel planTimeline =
                        viewModel.BehaviorCardWorkspace.Timeline;
                    planTimeline.CharacterCommandText = "观察目标";
                    planTimeline.CurrentBehaviorAvailable = true;
                    planTimeline.CurrentBehaviorTitleText = "等待轻攻击条件";
                    planTimeline.CurrentBehaviorFactText =
                        "已存在目标 · 条件却要求“没有目标”";
                    planTimeline.CurrentBehaviorCommandText = "观察目标";
                    planTimeline.CurrentBehaviorWarningVisible = true;
                    planTimeline.CurrentBehaviorWarningText =
                        "轻攻击条件与当前事实矛盾";
                    planTimeline.CurrentBehaviorRepairDetectorStableId =
                        planTimeline.Detectors[0].DetectorId;
                    planTimeline.BehaviorCandidates = new[]
                    {
                        new CharacterTestRuntimeAiLabBehaviorCandidateViewModel
                        {
                            EntryIndex = 0,
                            StableId = "candidate.attack",
                            Title = "主要攻击",
                            ScoreText = "执行中",
                            Committed = true,
                            Evaluated = true,
                            ScoreAvailable = true,
                            Score = 10f,
                            ConditionCount = 1
                        },
                        new CharacterTestRuntimeAiLabBehaviorCandidateViewModel
                        {
                            EntryIndex = 1,
                            StableId = "candidate.approach",
                            Title = "靠近目标",
                            ScoreText = "得分 50",
                            Selected = true,
                            Eligible = true,
                            Evaluated = true,
                            ScoreAvailable = true,
                            Score = 50f,
                            ConditionCount = 1
                        }
                    };
                    planTimeline.BehaviorCandidates[0].ConditionTexts[0] = "目标不在攻击范围";
                    planTimeline.BehaviorCandidates[0].ConditionKnown[0] = true;
                    planTimeline.BehaviorCandidates[0].ConditionSatisfied[0] = false;
                    planTimeline.BehaviorCandidates[1].ConditionTexts[0] = "目标存在";
                    planTimeline.BehaviorCandidates[1].ConditionKnown[0] = true;
                    planTimeline.BehaviorCandidates[1].ConditionSatisfied[0] = true;
                    planTimeline.BehaviorCandidateCount = 2;
                    binder.Bind(panelHandle, viewModel);
                    Assert.AreSame(rootBeforeModeSwitch, concrete.Component);
                    Assert.IsTrue(
                        timeline.visible,
                        "Behavior Plan keeps the RuntimeFrame root alive for the permanent Current Decision surface.");
                    Assert.IsTrue(
                        behaviorCardTimeline.visible,
                        "The Behavior Plan timeline must replace Runtime Trace inside the persistent host.");
                    Assert.IsTrue(
                        planModeButton.selected,
                        "The Behavior Plan timeline mode button must reflect the selected mode.");
                    traceModeButton.onClick.Call();
                    Assert.AreEqual(
                        CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds
                            .TimelineModeRuntimeTrace,
                        commandSink.Last.CommandId,
                        "The persistent mode strip must remain bound after entering Behavior Plan.");
                    Assert.AreEqual(0f, behaviorCardTimeline.x);
                    Assert.AreEqual(668f, behaviorCardTimeline.y);
                    Assert.AreEqual(1920f, behaviorCardTimeline.width);
                    Assert.AreEqual(412f, behaviorCardTimeline.height);
                    Assert.AreEqual(0f, timeline.x);
                    Assert.AreEqual(0f, timeline.y);
                    Assert.AreEqual(1920f, timeline.width);
                    Assert.AreEqual(1080f, timeline.height);
                    Assert.IsFalse(runtimeFrameViewport.visible);
                    Assert.IsTrue(timelineEventDetails.visible);
                    Assert.AreEqual(668f, timelineEventDetails.height);
                    Assert.AreEqual(
                        "下一行为候选",
                        timelineEventDetails.GetChild("detailTitle")?.asTextField?.text);
                    Assert.IsFalse(timelineEventDetails.GetChild("planActionBackground").visible);
                    Fgui.GComponent firstCandidateRow = timelineEventDetails.GetChild(
                        "behaviorPlanCandidateRow0")?.asCom;
                    Assert.NotNull(firstCandidateRow);
                    Assert.IsTrue(firstCandidateRow.visible);
                    Assert.AreEqual("主要攻击", firstCandidateRow.GetChildAt(2)?.asTextField?.text);
                    Assert.AreEqual(
                        "目标不在攻击范围",
                        firstCandidateRow.GetChildAt(3)?.asTextField?.text);
                    Assert.IsFalse(timelineEventDetails.GetChild("causalCard1").visible);
                    Fgui.GButton planDecisionDetails = timelineEventDetails.GetChild(
                        "memberRow5")?.asButton;
                    Assert.IsFalse(planDecisionDetails.visible);
                    Assert.IsFalse(behaviorCardLibrary.visible);
                    Fgui.GComponent gameplayPlanLayer = behaviorCardTimeline.GetChild(
                        "runtimeGameplayPlanLayer")?.asCom;
                    Assert.NotNull(gameplayPlanLayer);
                    Assert.IsTrue(gameplayPlanLayer.visible);
                    Assert.AreEqual(72f, concrete.Component.GetChild(
                        CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.TimelineModeHost).height);
                    Assert.AreEqual(148f, planModeButton.width);
                    Assert.AreEqual(52f, planModeButton.height);
                    Assert.AreEqual(24, planModeButton.titleFontSize);
                    Assert.AreEqual(
                        28,
                        behaviorCardTimeline.GetChild("timelineTitle")?.asTextField?.textFormat.size);
                    Assert.IsFalse(
                        behaviorCardTimeline.GetChild("timelineTitle").visible,
                        "Behavior Plan replaces the card title with the compact current-action summary.");
                    Assert.AreEqual(
                        24,
                        behaviorCardTimeline.GetChild("timelineValidation")?.asTextField?.textFormat.size);
                    Assert.IsFalse(gameplayPlanLayer.GetChild("planTrackLabel0").visible);
                    Assert.AreEqual(
                        24,
                        gameplayPlanLayer.GetChild("planRulerLabel0")?.asTextField?.textFormat.size);
                    Assert.IsFalse(gameplayPlanLayer.GetChild("planRulerBackground").visible);
                    Assert.IsFalse(gameplayPlanLayer.GetChild("planRulerLabel0").visible);
                    Assert.IsFalse(gameplayPlanLayer.GetChild("planRulerTick0").visible);
                    Assert.AreEqual(88f, gameplayPlanLayer.GetChild("planTrackBackground0")?.y);
                    Assert.AreEqual(64f, gameplayPlanLayer.GetChild("planTrackBackground0")?.height);
                    Assert.AreEqual(160f, gameplayPlanLayer.GetChild("planTrackBackground1")?.y);
                    Assert.AreEqual(72f, gameplayPlanLayer.GetChild("planTrackBackground1")?.height);
                    Assert.IsFalse(gameplayPlanLayer.GetChild("planTrackLabel1").visible);
                    Assert.IsNull(gameplayPlanLayer.GetChild("planTrackLabel2"));
                    Assert.IsNull(gameplayPlanLayer.GetChild("planTrackLabel3"));
                    Assert.IsNull(gameplayPlanLayer.GetChild("planTrackBackground3"));
                    Assert.IsNull(
                        gameplayPlanLayer.GetChild("planStage0"),
                        "A single-stage plan must not repeat the stage as a separate header card.");
                    Assert.NotNull(
                        gameplayPlanLayer.GetChild("planAction0"),
                        "Gameplay Plan must render the first stage gameplay action.");
                    Fgui.GComponent firstPlanAction = gameplayPlanLayer.GetChild("planAction0")?.asCom;
                    Assert.NotNull(firstPlanAction?.GetChild("planProgressFill"));
                    Assert.NotNull(firstPlanAction?.GetChild("planIndeterminateSweep"));
                    Assert.NotNull(firstPlanAction?.GetChild("planStateBadge"));
                    Assert.NotNull(firstPlanAction?.GetChild("planMetricTargetBadge"));
                    Assert.NotNull(firstPlanAction?.GetChild("planActionRestartBadge"));
                    Assert.IsNull(gameplayPlanLayer.GetChild("planSnapshotProgress"));
                    Assert.IsNull(gameplayPlanLayer.GetChild("planSnapshotStartDot"));
                    Assert.IsNull(gameplayPlanLayer.GetChild("planSnapshotHeadDot"));
                    Assert.IsNull(gameplayPlanLayer.GetChild("planPlayhead"));
                    Assert.IsNull(gameplayPlanLayer.GetChild("planExecutionOverrun"));
                    Assert.NotNull(gameplayPlanLayer.GetChild("planBehaviorRelationshipLabel"));
                    Assert.NotNull(gameplayPlanLayer.GetChild("planBehaviorSuccessExit"));
                    Assert.NotNull(gameplayPlanLayer.GetChild("planBehaviorFailureExit"));
                    Assert.NotNull(gameplayPlanLayer.GetChild("planSummaryPrefix"));
                    Assert.NotNull(gameplayPlanLayer.GetChild("planSummaryCurrent"));
                    Assert.NotNull(gameplayPlanLayer.GetChild("planSummaryFramePrefix"));
                    Assert.NotNull(gameplayPlanLayer.GetChild("planSummaryFrame"));
                    Assert.NotNull(gameplayPlanLayer.GetChild("planSummaryNextPrefix"));
                    Assert.NotNull(gameplayPlanLayer.GetChild("planSummaryNext"));
                    Assert.NotNull(gameplayPlanLayer.GetChild("planBehaviorNodeBackground0"));
                    Assert.NotNull(gameplayPlanLayer.GetChild("planBehaviorNodeBackground1"));
                    Assert.NotNull(gameplayPlanLayer.GetChild("planBehaviorNodeBackground2"));
                    Fgui.GComponent compactExit = gameplayPlanLayer.GetChild("planExit0")?.asCom;
                    Assert.NotNull(
                        compactExit,
                        "The compact plan must keep the current behavior's real exit visible.");
                    StringAssert.Contains(
                        "重新决策",
                        compactExit.GetChildAt(3)?.asTextField?.text);
                    CharacterTestFairyGuiRuntimeAiLabBehaviorCardWorkspaceBinder
                        gameplayWorkspaceBinder =
                            GetPrivateField<CharacterTestFairyGuiRuntimeAiLabBehaviorCardWorkspaceBinder>(
                                binder,
                                "_behaviorCardWorkspaceBinder");
                    CharacterTestFairyGuiRuntimeAiLabBehaviorPlanTimelineBinder
                        gameplayTimelineBinder =
                            GetPrivateField<CharacterTestFairyGuiRuntimeAiLabBehaviorPlanTimelineBinder>(
                                gameplayWorkspaceBinder,
                                "_planTimeline");
                    int gameplayPlanChildHighWater = gameplayPlanLayer.numChildren;
                    Fgui.GObject firstPlanStage = gameplayPlanLayer.GetChild("planStage0");
                    viewModel.BehaviorCardWorkspace.Timeline.Stages[0].Current = true;
                    viewModel.BehaviorCardWorkspace.Timeline.Stages[0].ActionRunning = true;
                    viewModel.BehaviorCardWorkspace.Timeline.Stages[0].ActualDurationFrames = 3;
                    viewModel.BehaviorCardWorkspace.Timeline.Stages[0].ActionRuntimeState =
                        (int)CharacterTestRuntimeAiLabLogicActionState.Running;
                    viewModel.BehaviorCardWorkspace.Timeline.Stages[0].ActionDurationFrames = 20;
                    viewModel.BehaviorCardWorkspace.Timeline.Stages[0].ActionProgressFrames = 5;
                    viewModel.BehaviorCardWorkspace.Timeline.Stages[0].ActionProgressIndeterminate = false;
                    viewModel.BehaviorCardWorkspace.Timeline.Detectors[0].Active = true;
                    viewModel.BehaviorCardWorkspace.Timeline.Detectors[0].RuntimeElapsedFrames = 5;
                    viewModel.BehaviorCardWorkspace.Timeline.RuntimeFrame = 29;
                    viewModel.BehaviorCardWorkspace.Timeline.CharacterCommandText = "观察目标";
                    viewModel.BehaviorCardWorkspace.Timeline.BehaviorRelationshipAvailable = true;
                    viewModel.BehaviorCardWorkspace.Timeline.PreviousBehaviorText = "上一行为\n接近目标";
                    viewModel.BehaviorCardWorkspace.Timeline.PreviousBehaviorRelationText = "切换 →";
                    viewModel.BehaviorCardWorkspace.Timeline.CommittedBehaviorText = "当前行为\n轻攻击";
                    viewModel.BehaviorCardWorkspace.Timeline.NextBehaviorText = "下一 · 闪避";
                    viewModel.BehaviorCardWorkspace.Timeline.NextBehaviorRelationText = "→ 候选";
                    viewModel.BehaviorCardWorkspace.Timeline.NextBehaviorPending = true;
                    viewModel.BehaviorCardWorkspace.Timeline.BehaviorCandidates = new[]
                    {
                        new CharacterTestRuntimeAiLabBehaviorCandidateViewModel
                        {
                            EntryIndex = 0,
                            StableId = "candidate.approach",
                            Title = "靠近目标",
                            ScoreText = "得分 50",
                            Selected = true,
                            Pending = true,
                            Eligible = true,
                            Evaluated = true,
                            ConditionCount = 1
                        },
                        new CharacterTestRuntimeAiLabBehaviorCandidateViewModel
                        {
                            EntryIndex = 1,
                            StableId = "candidate.attack",
                            Title = "主要攻击",
                            ScoreText = "执行中",
                            Committed = true,
                            Evaluated = true,
                            ConditionCount = 1
                        }
                    };
                    viewModel.BehaviorCardWorkspace.Timeline.BehaviorCandidates[0].ConditionTexts[0] = "目标存在";
                    viewModel.BehaviorCardWorkspace.Timeline.BehaviorCandidates[0].ConditionKnown[0] = true;
                    viewModel.BehaviorCardWorkspace.Timeline.BehaviorCandidates[0].ConditionSatisfied[0] = true;
                    viewModel.BehaviorCardWorkspace.Timeline.BehaviorCandidates[1].ConditionTexts[0] = "在攻击范围";
                    viewModel.BehaviorCardWorkspace.Timeline.BehaviorCandidates[1].ConditionKnown[0] = true;
                    viewModel.BehaviorCardWorkspace.Timeline.BehaviorCandidates[1].ConditionSatisfied[0] = false;
                    viewModel.BehaviorCardWorkspace.Timeline.BehaviorCandidateCount = 2;
                    gameplayTimelineBinder.Bind(
                        behaviorCardTimeline,
                        viewModel.BehaviorCardWorkspace.Timeline,
                        visible: true);
                    Assert.AreEqual(
                        gameplayPlanChildHighWater,
                        gameplayPlanLayer.numChildren,
                        "Runtime refresh must reuse the compact plan pool and must not create lower candidate cards.");
                    gameplayTimelineBinder.Bind(
                        behaviorCardTimeline,
                        viewModel.BehaviorCardWorkspace.Timeline,
                        visible: true);
                    Assert.AreEqual(gameplayPlanChildHighWater, gameplayPlanLayer.numChildren);
                    Assert.AreSame(firstPlanStage, gameplayPlanLayer.GetChild("planStage0"));
                    Fgui.GObject progressFill = firstPlanAction.GetChild("planProgressFill");
                    Assert.IsTrue(progressFill.visible);
                    Assert.Greater(progressFill.width, 0f);
                    Assert.Less(progressFill.width, firstPlanAction.width);
                    Assert.AreEqual(
                        "5/20",
                        firstPlanAction.GetChild("planStateBadge")?.asTextField?.text);
                    Assert.IsFalse(gameplayPlanLayer.GetChild("planSummaryPrefix").visible);
                    Assert.IsFalse(gameplayPlanLayer.GetChild("planSummaryCurrent").visible);
                    Assert.IsFalse(gameplayPlanLayer.GetChild("planSummaryFrame").visible);
                    Assert.IsFalse(gameplayPlanLayer.GetChild("planSummaryNext").visible);
                    Fgui.GComponent pendingCandidate =
                        gameplayPlanLayer.GetChild("planBehaviorCandidate0")?.asCom;
                    Fgui.GComponent committedCandidate =
                        gameplayPlanLayer.GetChild("planBehaviorCandidate1")?.asCom;
                    Assert.IsNull(pendingCandidate);
                    Assert.IsNull(committedCandidate);
                    Fgui.GObject actionLabel = firstPlanAction.GetChildAt(3);
                    Fgui.GObject actionBadge = firstPlanAction.GetChild("planStateBadge");
                    Assert.LessOrEqual(
                        actionLabel.y + actionLabel.height,
                        actionBadge.y,
                        "Action title and local-frame badge must use separate rows.");
                    Assert.IsFalse(gameplayPlanLayer.GetChild("planExecutionStatus").visible);
                    Fgui.GObject restartBadge = firstPlanAction.GetChild("planActionRestartBadge");
                    viewModel.BehaviorCardWorkspace.Timeline.Stages[0].ActionInstanceId = 41L;
                    gameplayTimelineBinder.Bind(
                        behaviorCardTimeline,
                        viewModel.BehaviorCardWorkspace.Timeline,
                        visible: true);
                    Assert.IsFalse(restartBadge.visible);
                    viewModel.BehaviorCardWorkspace.Timeline.Stages[0].ActionInstanceId = 42L;
                    gameplayTimelineBinder.Bind(
                        behaviorCardTimeline,
                        viewModel.BehaviorCardWorkspace.Timeline,
                        visible: true);
                    Assert.IsTrue(restartBadge.visible);
                    Assert.AreEqual("↻", restartBadge.asTextField?.text);
                    viewModel.BehaviorCardWorkspace.Timeline.Stages[0].ActionProgressFrames = 999;
                    viewModel.BehaviorCardWorkspace.Timeline.Stages[0].ActionProgressOverrun = true;
                    gameplayTimelineBinder.Bind(
                        behaviorCardTimeline,
                        viewModel.BehaviorCardWorkspace.Timeline,
                        visible: true);
                    Assert.AreEqual(
                        "! 20+/20",
                        firstPlanAction.GetChild("planStateBadge")?.asTextField?.text);
                    Assert.IsTrue(firstPlanAction.GetChild("planStateBadge").visible);
                    viewModel.BehaviorCardWorkspace.Timeline.Stages[0].ActionDurationFrames = 0;
                    viewModel.BehaviorCardWorkspace.Timeline.Stages[0].ActionProgressFrames = 37;
                    viewModel.BehaviorCardWorkspace.Timeline.Stages[0].ActionProgressIndeterminate = true;
                    viewModel.BehaviorCardWorkspace.Timeline.Stages[0].ActionProgressOverrun = false;
                    viewModel.BehaviorCardWorkspace.Timeline.Stages[0].ActionRuntimeState =
                        (int)CharacterTestRuntimeAiLabLogicActionState.Running;
                    gameplayTimelineBinder.Bind(
                        behaviorCardTimeline,
                        viewModel.BehaviorCardWorkspace.Timeline,
                        visible: true);
                    Assert.AreEqual(
                        "37 帧 / ∞",
                        firstPlanAction.GetChild("planStateBadge")?.asTextField?.text);
                    Fgui.GObject actionSweep = firstPlanAction.GetChild("planIndeterminateSweep");
                    Assert.IsFalse(actionSweep.visible);
                    Assert.IsFalse(firstPlanAction.GetChild("planProgressFill").visible);
                    viewModel.BehaviorCardWorkspace.Timeline.Stages[0].ActionRuntimeState =
                        (int)CharacterTestRuntimeAiLabLogicActionState.Completed;
                    gameplayTimelineBinder.Bind(
                        behaviorCardTimeline,
                        viewModel.BehaviorCardWorkspace.Timeline,
                        visible: true);
                    Assert.AreEqual(
                        "✓ 37 帧 / ∞",
                        firstPlanAction.GetChild("planStateBadge")?.asTextField?.text);
                    viewModel.BehaviorCardWorkspace.Timeline.Stages[0].ActionRuntimeState =
                        (int)CharacterTestRuntimeAiLabLogicActionState.Failed;
                    gameplayTimelineBinder.Bind(
                        behaviorCardTimeline,
                        viewModel.BehaviorCardWorkspace.Timeline,
                        visible: true);
                    Assert.AreEqual(
                        "× 37 帧 / ∞",
                        firstPlanAction.GetChild("planStateBadge")?.asTextField?.text);
                    viewModel.BehaviorCardWorkspace.Timeline.Historical = true;
                    gameplayTimelineBinder.Bind(
                        behaviorCardTimeline,
                        viewModel.BehaviorCardWorkspace.Timeline,
                        visible: true);
                    Assert.IsFalse(firstPlanAction.GetChild("planProgressFill").visible);
                    Assert.IsFalse(firstPlanAction.GetChild("planIndeterminateSweep").visible);
                    Assert.AreEqual(
                        "历史帧",
                        gameplayPlanLayer.GetChild("planExecutionStatus")?.asTextField?.text);
                    Assert.IsFalse(gameplayPlanLayer.GetChild("planSummaryFrame").visible);
                    Assert.IsFalse(gameplayPlanLayer.GetChild("planSummaryPrefix").visible);
                    viewModel.BehaviorCardWorkspace.Timeline.Historical = false;
                    gameplayTimelineBinder.Bind(
                        behaviorCardTimeline,
                        viewModel.BehaviorCardWorkspace.Timeline,
                        visible: true);
                    Assert.AreEqual(gameplayPlanChildHighWater, gameplayPlanLayer.numChildren);
                    Assert.AreEqual(
                        36,
                        timelineEventDetails.GetChild("detailTitle")?.asTextField?.textFormat.size);
                    Assert.AreEqual(
                        "下一行为候选",
                        timelineEventDetails.GetChild("detailTitle")?.asTextField?.text);
                    Assert.AreEqual(
                        22,
                        timelineEventDetails.GetChild("causalTitle1")?.asTextField?.textFormat.size);
                    Assert.AreEqual(
                        20,
                        timelineEventDetails.GetChild("causalValue1")?.asTextField?.textFormat.size);
                    Assert.IsFalse(timelineEventDetails.GetChild("planActionBackground").visible);
                    Assert.IsFalse(timelineEventDetails.GetChild("detailEntities").visible);
                    Assert.IsFalse(timelineEventDetails.GetChild("causalCard1").visible);
                    Assert.IsTrue(
                        timelineEventDetails.GetChild("behaviorPlanCandidateRow0").visible);
                    firstPlanAction.onClick.Call();
                    Assert.AreEqual(668f, timelineEventDetails.height);
                    Assert.IsTrue(timelineEventDetails.GetChild("planActionBackground").visible);
                    Assert.IsTrue(timelineEventDetails.GetChild("detailEntities").visible);
                    Assert.AreEqual(116f, timelineEventDetails.GetChild("planActionBackground").y);
                    Assert.AreEqual(164f, timelineEventDetails.GetChild("detailEntities").y);
                    Assert.AreEqual(
                        "主动作详情",
                        timelineEventDetails.GetChild("planActionTitle")?.asTextField?.text);
                    firstPlanAction.onClick.Call();
                    Assert.AreEqual(668f, timelineEventDetails.height);
                    Assert.AreEqual(
                        "下一行为候选",
                        timelineEventDetails.GetChild("detailTitle")?.asTextField?.text);
                    Assert.IsFalse(timelineEventDetails.GetChild("memberRow1").visible);
                    Assert.IsFalse(behaviorCardTimeline.GetChild("timelineRuntimeStatus").visible);
                    Assert.IsFalse(behaviorCardTimeline.GetChild(
                        "timelineHorizontalScrollTrack").visible);
                    Assert.IsFalse(behaviorCardTimeline.GetChild(
                        "timelineVerticalScrollTrack").visible);

                    viewModel.TimelineMode =
                        CharacterTestRuntimeAiLabTimelineMode.BehaviorLogic;
                    viewModel.Workspace.ActiveTimelineMode =
                        CharacterTestRuntimeAiLabTimelineMode.BehaviorLogic;
                    viewModel.ShowBehaviorPlan = false;
                    viewModel.ShowBehaviorLogic = true;
                    viewModel.BehaviorPlanModeSelected = false;
                    viewModel.BehaviorLogicModeSelected = true;
                    viewModel.BehaviorCardWorkspace.Timeline.CanEdit = true;
                    viewModel.CanEditWorkbench = true;
                    viewModel.ToggleEntryLabel = "禁用";
                    viewModel.WorkbenchRows = new[]
                    {
                        new CharacterTestRuntimeAiLabWorkbenchRowViewModel
                        {
                            EntryIndex = 2,
                            Label = "行为卡",
                            TitleText = "基础攻击",
                            BadgeText = "就绪",
                            TriggerText = "持续行为",
                            PriorityText = "优先级 100",
                            Visible = true,
                            Selected = true
                        }
                    };
                    viewModel.WorkbenchRowCount = 1;
                    CharacterTestRuntimeAiLabBehaviorCardTimelineViewModel compactTimeline =
                        viewModel.BehaviorCardWorkspace.Timeline;
                    Assert.AreEqual(planLogicCompile.Program.Stages.Count, compactTimeline.StageCount);
                    Assert.AreEqual(planLogicCompile.Program.Detectors.Count, compactTimeline.DetectorCount);
                    Assert.AreEqual(planLogicCompile.Program.Transitions.Count, compactTimeline.TransitionCount);
                    CharacterTestRuntimeAiLabLogicDetectorViewModel[] compactOriginalDetectors =
                        compactTimeline.Detectors;
                    CharacterTestRuntimeAiLabLogicTransitionViewModel[] compactOriginalTransitions =
                        compactTimeline.Transitions;
                    int compactOriginalDetectorCount = compactTimeline.DetectorCount;
                    int compactOriginalTransitionCount = compactTimeline.TransitionCount;
                    compactTimeline.Detectors = compactOriginalDetectors.Take(2).ToArray();
                    compactTimeline.Transitions = compactOriginalTransitions.Take(2).ToArray();
                    compactTimeline.DetectorCount = compactTimeline.Detectors.Length;
                    compactTimeline.TransitionCount = compactTimeline.Transitions.Length;
                    CharacterTestRuntimeAiLabLogicStageViewModel compactStageViewModel =
                        compactTimeline.Stages[0];
                    CharacterTestRuntimeAiLabCardStepKind compactOriginalStageKind =
                        compactStageViewModel.Kind;
                    string compactOriginalStageTitle = compactStageViewModel.Title;
                    bool compactOriginalStageCurrent = compactStageViewModel.Current;
                    bool compactOriginalActionRunning = compactStageViewModel.ActionRunning;
                    int compactOriginalActualDuration = compactStageViewModel.ActualDurationFrames;
                    int compactOriginalActionProgress = compactStageViewModel.ActionProgressFrames;
                    string compactOriginalRuntimeStatus = compactStageViewModel.RuntimeStatusText;
                    CharacterTestRuntimeAiLabLogicDetectorTrigger compactOriginalFirstTrigger =
                        compactTimeline.Detectors[0].Trigger;
                    CharacterTestRuntimeAiLabLogicDetectorTrigger compactOriginalSecondTrigger =
                        compactTimeline.Detectors[1].Trigger;
                    string compactOriginalSecondDetectorTitle = compactTimeline.Detectors[1].Title;
                    string compactOriginalFirstTransitionLabel = compactTimeline.Transitions[0].Label;
                    string compactOriginalFirstTransitionEndReason =
                        compactTimeline.Transitions[0].EndReason;
                    string compactOriginalSecondTransitionLabel = compactTimeline.Transitions[1].Label;
                    string compactOriginalSecondTransitionEndReason =
                        compactTimeline.Transitions[1].EndReason;
                    compactStageViewModel.Kind = CharacterTestRuntimeAiLabCardStepKind.MoveToTarget;
                    compactStageViewModel.Title = "持续靠近";
                    compactStageViewModel.Current = false;
                    compactStageViewModel.ActionRunning = false;
                    compactStageViewModel.ActualDurationFrames = -1;
                    compactStageViewModel.ActionProgressFrames = -1;
                    compactStageViewModel.RuntimeStatusText =
                        "当前阶段 · 主动行为执行中 · 实际 1f";
                    compactTimeline.Detectors[0].Trigger =
                        CharacterTestRuntimeAiLabLogicDetectorTrigger.ActionCompleted;
                    compactTimeline.Detectors[1].Trigger =
                        CharacterTestRuntimeAiLabLogicDetectorTrigger.ActionFailed;
                    compactTimeline.Detectors[1].Title = "路径不可用时重新决策";
                    compactTimeline.Transitions[0].Label = "继续";
                    compactTimeline.Transitions[0].EndReason = "继续";
                    compactTimeline.Transitions[1].Label = "路径不可用时重新决策";
                    compactTimeline.Transitions[1].EndReason = "路径不可用时重新决策";
                    binder.Bind(panelHandle, viewModel);
                    Assert.AreSame(rootBeforeModeSwitch, concrete.Component);
                    Assert.IsFalse(concrete.Component.GetChild(
                        CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.DuplicateEntry).visible);
                    Assert.IsFalse(concrete.Component.GetChild(
                        CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.MoveEntryUp).visible);
                    Assert.IsFalse(concrete.Component.GetChild(
                        CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.MoveEntryDown).visible);
                    Assert.IsFalse(concrete.Component.GetChild(
                        CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.ToggleEntry).visible);
                    Assert.IsFalse(concrete.Component.GetChild(
                        CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.DeleteEntry).visible);
                    Assert.AreEqual(1, workbenchList.numItems);
                    Assert.NotNull(workbenchList.scrollPane);
                    Fgui.GComponent behaviorLogicCard = workbenchList.GetChildAt(0)?.asCom;
                    Assert.NotNull(behaviorLogicCard);
                    Assert.IsFalse(behaviorLogicCard.GetChild("orderText").visible);
                    Assert.IsFalse(behaviorLogicCard.GetChild("iconBackground").visible);
                    Assert.IsFalse(behaviorLogicCard.GetChild("behaviorIcon").visible);
                    Assert.IsFalse(behaviorLogicCard.GetChild("moreText").visible);
                    Assert.IsFalse(behaviorLogicCard.GetChild("priorityText").visible);
                    Assert.IsFalse(behaviorLogicCard.GetChild("triggerText").visible);
                    Assert.IsFalse(behaviorLogicCard.GetChild("badgeText").visible);
                    Assert.AreEqual(72f, behaviorLogicCard.GetChild("title").x);
                    Fgui.GButton inlineToggle = behaviorLogicCard.GetChild(
                        "inlineToggleEntry")?.asButton;
                    Fgui.GButton inlineDelete = behaviorLogicCard.GetChild(
                        "inlineDeleteEntry")?.asButton;
                    Assert.NotNull(inlineToggle);
                    Assert.NotNull(inlineDelete);
                    Assert.IsTrue(inlineToggle.visible);
                    Assert.IsTrue(inlineDelete.visible);
                    Assert.IsTrue(inlineToggle.touchable);
                    Assert.IsTrue(inlineDelete.touchable);
                    inlineToggle.onClick.Call();
                    Assert.AreEqual(
                        CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.ToggleEntry,
                        commandSink.Last.CommandId);
                    Assert.IsTrue(behaviorCardTimeline.visible);
                    Assert.AreEqual(500f, behaviorCardTimeline.x);
                    Assert.AreEqual(1420f, behaviorCardTimeline.width);
                    Assert.AreEqual(668f, behaviorCardTimeline.y);
                    Assert.AreEqual(412f, behaviorCardTimeline.height);
                    Assert.IsNull(behaviorCardTimeline.GetChild("btnTimelineAddStage"));
                    Assert.IsNull(behaviorCardTimeline.GetChild("btnTimelineAddCardDetector"));
                    Assert.IsTrue(logicModeButton.selected);
                    Assert.IsFalse(gameplayPlanLayer.visible);
                    Assert.IsFalse(behaviorCardLibrary.visible);
                    Fgui.GComponent recipeLayer = behaviorCardTimeline.GetChild(
                        "timelineGroupHost")?.asCom;
                    Assert.NotNull(recipeLayer);
                    Assert.IsTrue(recipeLayer.visible);
                    Assert.AreEqual(120f, recipeLayer.x);
                    Assert.AreEqual(0f, recipeLayer.y);
                    Assert.AreEqual(1300f, recipeLayer.width);
                    Assert.AreEqual(behaviorCardTimeline.height, recipeLayer.height);
                    Fgui.GButton undoButton = behaviorCardTimeline.GetChild(
                        "btnTimelineUndo")?.asButton;
                    Fgui.GButton redoButton = behaviorCardTimeline.GetChild(
                        "btnTimelineRedo")?.asButton;
                    Assert.NotNull(undoButton);
                    Assert.NotNull(redoButton);
                    Assert.IsTrue(undoButton.visible);
                    Assert.IsTrue(redoButton.visible);
                    Assert.Greater(undoButton.sortingOrder, recipeLayer.sortingOrder);
                    Assert.Greater(redoButton.sortingOrder, recipeLayer.sortingOrder);
                    Assert.AreEqual(10f, undoButton.x);
                    Assert.AreEqual(10f, redoButton.x);
                    Assert.AreEqual(88f, undoButton.y);
                    Assert.AreEqual(142f, redoButton.y);
                    Assert.AreEqual(100f, undoButton.width);
                    Assert.AreEqual(100f, redoButton.width);
                    Assert.AreEqual(44f, undoButton.height);
                    Assert.AreEqual(44f, redoButton.height);
                    Assert.NotNull(recipeLayer.GetChild("recipeBackground"));
                    Assert.IsNull(recipeLayer.GetChild("recipeToolbarBackground"));
                    Assert.IsNull(recipeLayer.GetChild("recipeTitle"));
                    Assert.AreEqual(
                        "行为配方",
                        behaviorCardTimeline.GetChild("timelineTitle")?.asTextField?.text);
                    Assert.AreEqual(0f, behaviorCardTimeline.GetChild("toolbar").x);
                    Assert.AreEqual(120f, behaviorCardTimeline.GetChild("toolbar").width);
                    Assert.AreEqual(412f, behaviorCardTimeline.GetChild("toolbar").height);
                    Assert.AreEqual(
                        "阶段帧",
                        recipeLayer.GetChild("recipeRulerTitle")?.asTextField?.text);
                    Assert.NotNull(recipeLayer.GetChild("recipeHorizontalScrollTrack"));
                    Assert.IsFalse(recipeLayer.GetChild("recipeRulerTitle").visible);
                    Assert.NotNull(recipeLayer.GetChild("recipeHorizontalScrollThumb"));
                    Assert.NotNull(recipeLayer.GetChild("recipeVerticalScrollTrack"));
                    Assert.NotNull(recipeLayer.GetChild("recipeVerticalScrollThumb"));
                    Fgui.GComponent recipeViewport = recipeLayer.GetChild(
                        "recipeViewport")?.asCom;
                    Assert.NotNull(recipeViewport);
                    Assert.AreEqual(1300f, recipeViewport.width);
                    Assert.AreEqual(
                        1290f,
                        recipeLayer.GetChild("recipeVerticalScrollTrack").x);
                    Fgui.GComponent recipeRows = recipeViewport.GetChild("recipeRows")?.asCom;
                    Assert.NotNull(recipeRows);
                    Assert.IsNull(recipeRows.GetChild("recipeTimelineHeader0"));
                    Assert.AreEqual(0, compactStageViewModel.EntryConditionCount);
                    Fgui.GComponent primaryTrack = recipeRows.GetChild("recipeTrack0")?.asCom;
                    Fgui.GComponent firstBranchTrack = recipeRows.GetChild("recipeTrack1")?.asCom;
                    Fgui.GComponent secondBranchTrack = recipeRows.GetChild("recipeTrack2")?.asCom;
                    Assert.NotNull(primaryTrack);
                    Assert.NotNull(firstBranchTrack);
                    Assert.NotNull(secondBranchTrack);
                    Assert.IsFalse(primaryTrack.GetChild("trackLabel").visible);
                    Assert.IsFalse(primaryTrack.GetChild("trackLabel").touchable);
                    Assert.IsFalse(firstBranchTrack.GetChild("trackLabel").visible);
                    Assert.IsFalse(secondBranchTrack.GetChild("trackLabel").visible);
                    Assert.AreEqual(12f, primaryTrack.GetChild("trackContent").x);
                    StringAssert.Contains(
                        compactStageViewModel.Title,
                        primaryTrack.GetChild("trackContent")?.asCom
                            ?.GetChild("eventSpan")?.asCom
                            ?.GetChild("spanLabel")?.asTextField?.text);
                    Assert.NotNull(primaryTrack.GetChild("trackContent")?.asCom?.GetChild("eventSpan"));
                    Assert.NotNull(firstBranchTrack.GetChild("trackContent")?.asCom?.GetChild("eventSpan"));
                    Assert.NotNull(secondBranchTrack.GetChild("trackContent")?.asCom?.GetChild("eventSpan"));
                    Assert.IsFalse(
                        primaryTrack.GetChild("trackContent")?.asCom
                            ?.GetChild("eventSpan")?.asCom
                            ?.GetChild("spanContinuation")?.visible ?? true);
                    Assert.IsFalse(
                        firstBranchTrack.GetChild("trackContent")?.asCom
                            ?.GetChild("eventSpan")?.asCom
                            ?.GetChild("spanContinuation")?.visible ?? true);
                    Assert.IsFalse(
                        secondBranchTrack.GetChild("trackContent")?.asCom
                            ?.GetChild("eventSpan")?.asCom
                            ?.GetChild("spanContinuation")?.visible ?? true);
                    Assert.AreEqual(
                        "显式退出行为卡",
                        secondBranchTrack.GetChild("trackContent")?.asCom
                            ?.GetChild("resultBadge")?.asTextField?.text);
                    Assert.AreEqual(
                        "动作实例失败（未配置原因）",
                        secondBranchTrack.GetChild("trackContent")?.asCom
                            ?.GetChild("eventSpan")?.asCom
                            ?.GetChild("spanLabel")?.asTextField?.text.TrimEnd(' ', '→'));
                    Assert.NotNull(
                        secondBranchTrack.GetChild("trackContent")?.asCom
                            ?.GetChild("resultBadgeBackground")?.asGraph);
                    compactStageViewModel.Kind = compactOriginalStageKind;
                    compactStageViewModel.Title = compactOriginalStageTitle;
                    compactStageViewModel.Current = compactOriginalStageCurrent;
                    compactStageViewModel.ActionRunning = compactOriginalActionRunning;
                    compactStageViewModel.ActualDurationFrames = compactOriginalActualDuration;
                    compactStageViewModel.ActionProgressFrames = compactOriginalActionProgress;
                    compactStageViewModel.RuntimeStatusText = compactOriginalRuntimeStatus;
                    compactTimeline.Detectors[0].Trigger = compactOriginalFirstTrigger;
                    compactTimeline.Detectors[1].Trigger = compactOriginalSecondTrigger;
                    compactTimeline.Detectors[1].Title = compactOriginalSecondDetectorTitle;
                    compactTimeline.Transitions[0].Label = compactOriginalFirstTransitionLabel;
                    compactTimeline.Transitions[0].EndReason =
                        compactOriginalFirstTransitionEndReason;
                    compactTimeline.Transitions[1].Label = compactOriginalSecondTransitionLabel;
                    compactTimeline.Transitions[1].EndReason =
                        compactOriginalSecondTransitionEndReason;
                    compactTimeline.Detectors = compactOriginalDetectors;
                    compactTimeline.Transitions = compactOriginalTransitions;
                    compactTimeline.DetectorCount = compactOriginalDetectorCount;
                    compactTimeline.TransitionCount = compactOriginalTransitionCount;
                    binder.Bind(panelHandle, viewModel);
                    StringAssert.Contains(
                        "正在执行",
                        timelineEventDetails.GetChild("detailTitle")?.asTextField?.text);
                    StringAssert.StartsWith(
                        "主行为事件 · ",
                        timelineEventDetails.GetChild("planActionTitle")?.asTextField?.text);
                    Assert.AreEqual(
                        "阶段行为",
                        timelineEventDetails.GetChild("planActionStartLabel")?.asTextField?.text);
                    Assert.AreEqual(
                        CharacterTestRuntimeAiLabTimelineWorkbenchLayout.DecisionPanelHeight,
                        timelineEventDetails.height,
                        "The right inspector must continue covering the full decision region.");
                    Assert.AreEqual(
                        CharacterTestRuntimeAiLabTimelineWorkbenchLayout.DecisionContentY + 8f,
                        timelineEventDetails.GetChild("detailTitle").y);
                    Assert.AreEqual(
                        26,
                        timelineEventDetails.GetChild("detailTitle")?.asTextField?.textFormat?.size,
                        "The compact runtime heading must remain readable after 1920-to-720 scaling.");
                    Assert.AreEqual(
                        24,
                        timelineEventDetails.GetChild("planActionTitle")?.asTextField?.textFormat?.size,
                        "The selected editor heading must be visually distinct from field labels.");
                    Assert.AreEqual(
                        CharacterTestRuntimeAiLabTimelineWorkbenchLayout.DecisionContentY + 580f,
                        timelineEventDetails.GetChild("planActionBackground").y
                            + timelineEventDetails.GetChild("planActionBackground").height);
                    Assert.IsFalse(timelineEventDetails.GetChild("btnLocateStart").visible);
                    Assert.AreEqual(
                        "删除阶段",
                        timelineEventDetails.GetChild("btnLocateEnd")?.asButton?.title);
                    Assert.IsFalse(timelineEventDetails.GetChild("planActionStartValue").visible);
                    Assert.IsFalse(timelineEventDetails.GetChild("planActionDurationValue").visible);
                    Fgui.GComponent typedControls = timelineEventDetails.GetChild(
                        "behaviorLogicTypedControls")?.asCom;
                    Assert.NotNull(typedControls);
                    Assert.NotNull(typedControls.GetChild("stageKind0"));
                    Assert.NotNull(typedControls.GetChild("stageKind4"));
                    Assert.NotNull(typedControls.GetChild("stageDuration0"));
                    Assert.NotNull(typedControls.GetChild("stageDuration2"));
                    Assert.AreEqual(
                        20,
                        typedControls.GetChild("stageKind0")?.asButton?.titleFontSize,
                        "Narrow typed choices must not fall back to unreadably small captions.");
                    Assert.NotNull(
                        typedControls.GetChild("contextSummaryBackground"),
                        "Recovered inspector height must contain contextual logic, not an empty region.");
                    Assert.AreEqual(
                        "关联逻辑",
                        typedControls.GetChild("contextSummaryTitle")?.asTextField?.text);
                    Assert.AreEqual(
                        22,
                        typedControls.GetChild("contextSummaryTitle")?.asTextField?.textFormat?.size);
                    Assert.AreEqual(
                        20,
                        typedControls.GetChild("contextSummaryFirst")?.asTextField?.textFormat?.size);
                    StringAssert.StartsWith(
                        "检测器 · ",
                        typedControls.GetChild("contextSummaryFirst")?.asTextField?.text);
                    StringAssert.StartsWith(
                        "出口 · ",
                        typedControls.GetChild("contextSummarySecond")?.asTextField?.text);
                    Assert.AreEqual(
                        CharacterTestRuntimeAiLabTimelineWorkbenchLayout.DecisionContentY + 510f,
                        timelineEventDetails.GetChild("btnLocateEnd").y,
                        "Structure actions must stay anchored beneath the contextual summary.");
                    CharacterTestRuntimeAiLabLogicStageViewModel selectedTypedStage =
                        viewModel.BehaviorCardWorkspace.Timeline.Stages[
                            viewModel.BehaviorCardWorkspace.Timeline.SelectedElementIndex];
                    Assert.AreEqual(
                        CharacterTestRuntimeAiLabCardStepKind.CharacterAction,
                        selectedTypedStage.Kind);
                    Assert.IsFalse(
                        typedControls.GetChild("stageKind1").touchable,
                        "The current action type is selected, not another generic edit button.");
                    Assert.IsTrue(
                        typedControls.GetChild("stageKind0").touchable,
                        "A different action type must be directly selectable.");
                    typedControls.GetChild("stageKind0")?.asButton?.onClick.Call();
                    Assert.IsTrue(gestureController.DraftWorkspace.TryGetGroupForSlot(
                        gestureController.SelectedSetupTeamId,
                        gestureController.SelectedSetupSlotIndex,
                        out gestureGroup));
                    gestureEntry = gestureGroup.Entries[gestureEntryIndex];
                    Assert.IsTrue(gestureController.DraftWorkspace.Cards.TryGetValue(
                        gestureEntry.CardDraftId,
                        out gestureCard));
                    CharacterTestRuntimeAiLabLogicStage selectedTypedPayloadStage =
                        gestureCard.LogicPayload.Stages.First(stage => string.Equals(
                            stage.StageId,
                            selectedTypedStage.StageId,
                            StringComparison.Ordinal));
                    Assert.AreEqual(
                        CharacterTestRuntimeAiLabCardStepKind.UseAbility,
                        selectedTypedPayloadStage.Kind,
                        "Clicking a typed choice must change the selected stage draft, not only its appearance. "
                        + gestureHandler.LastMessage);
                    Assert.IsFalse(
                        typedControls.GetChild("stageKind0").touchable,
                        "The newly selected choice must provide immediate feedback before the next full bind.");
                    Assert.IsTrue(
                        typedControls.GetChild("stageKind1").touchable,
                        "The previous choice must become selectable immediately.");
                    gestureHandler.UndoStageEdit();
                    Assert.NotNull(
                        timelineEventDetails.GetChild("btnLocateEnd")?.asCom?.GetChild(
                            "behaviorLogicActionSurface"),
                        "Behavior Logic actions must have an explicit action surface.");
                    Assert.IsFalse(
                        timelineEventDetails.GetChild("btnLocateEnd")?.asCom?.GetChild("icon").visible,
                        "Legacy timeline navigation icons must not leak into Behavior Logic actions.");
                    Assert.IsFalse(
                        timelineEventDetails.GetChild("detailEntities").visible,
                        "The selected-element subtitle repeats the compact editor heading.");
                    Assert.IsFalse(
                        timelineEventDetails.GetChild("detailMeta").visible,
                        "Frame context must not consume vertical space in Behavior Logic editing.");
                    Assert.IsFalse(timelineEventDetails.GetChild("memberRow1").visible);

                    CharacterTestRuntimeAiLabCardStepKind originalStageKind = selectedTypedStage.Kind;
                    string originalStageTitle = selectedTypedStage.Title;
                    CharacterTestRuntimeAiLabMovementStrategy originalMovementStrategy =
                        selectedTypedStage.MovementStrategy;
                    CharacterTestRuntimeAiLabMovementTargetKind originalMovementTargetKind =
                        selectedTypedStage.MovementTargetKind;
                    float originalMoveMinimum = selectedTypedStage.MoveMinDistance;
                    float originalMoveMaximum = selectedTypedStage.MoveMaxDistance;
                    int originalObservedTargetDistance =
                        viewModel.BehaviorCardWorkspace.Timeline.ObservedTargetDistanceDecimeters;
                    selectedTypedStage.Kind = CharacterTestRuntimeAiLabCardStepKind.MoveToTarget;
                    selectedTypedStage.Title = "持续靠近";
                    selectedTypedStage.MovementStrategy =
                        CharacterTestRuntimeAiLabMovementStrategy.ApproachTarget;
                    selectedTypedStage.MovementTargetKind =
                        CharacterTestRuntimeAiLabMovementTargetKind.CurrentCombatTarget;
                    selectedTypedStage.MoveMinDistance = 1.5f;
                    selectedTypedStage.MoveMaxDistance = 999f;
                    viewModel.BehaviorCardWorkspace.Timeline.ObservedTargetDistanceDecimeters = 63;
                    viewModel.BehaviorCardWorkspace.Timeline.SelectedElementKind =
                        CharacterTestRuntimeAiLabLogicElementKind.Stage;
                    viewModel.BehaviorCardWorkspace.Timeline.SelectedElementIndex =
                        selectedTypedStage.Index;
                    binder.Bind(panelHandle, viewModel);

                    typedControls = timelineEventDetails.GetChild(
                        "behaviorLogicTypedControls")?.asCom;
                    Assert.NotNull(typedControls);
                    Assert.AreEqual(
                        CharacterTestRuntimeAiLabTimelineWorkbenchLayout.DecisionPanelHeight,
                        timelineEventDetails.height,
                        "The right inspector must remain a complete column down to the workbench.");
                    Assert.AreEqual(
                        528f,
                        timelineEventDetails.GetChild("planActionBackground").height);
                    Assert.AreEqual(
                        128f,
                        typedControls.GetChild("movementEditorSurface").height,
                        "Only the actual parameter editor should stay compact inside the full column.");
                    Assert.AreEqual(
                        604f,
                        typedControls.GetChild("movementFeedbackSurface").y
                            + typedControls.GetChild("movementFeedbackSurface").height,
                        "Runtime feedback must occupy the lower inspector instead of leaving it blank.");
                    Assert.IsFalse(
                        timelineEventDetails.GetChild("planActionTitle").visible,
                        "The card heading already names the behavior; the movement stage title is redundant.");
                    Assert.AreEqual(
                        "停止距离",
                        typedControls.GetChild("movementDistanceLabel")?.asTextField?.text);
                    Assert.AreEqual(
                        "1.5",
                        typedControls.GetChild("movementDistance")?.asTextField?.text);
                    Assert.AreEqual(148f, typedControls.GetChild("movementDistance").width);
                    Assert.AreEqual(48f, typedControls.GetChild("movementDistance").height);
                    Assert.AreEqual(
                        viewModel.BehaviorCardWorkspace.Timeline.CanEdit,
                        typedControls.GetChild("movementDistance").touchable,
                        "The primary movement parameter must be directly clickable when the draft is editable.");
                    Fgui.GTextInput movementDistanceInput =
                        typedControls.GetChild("movementDistance")?.asTextInput;
                    Assert.NotNull(movementDistanceInput);
                    movementDistanceInput.onFocusIn.Call();
                    binder.Bind(panelHandle, viewModel);
                    typedControls = timelineEventDetails.GetChild(
                        "behaviorLogicTypedControls")?.asCom;
                    Assert.AreSame(
                        movementDistanceInput,
                        typedControls?.GetChild("movementDistance")?.asTextInput,
                        "A focused movement input must survive the live inspector refresh.");
                    movementDistanceInput.onFocusOut.Call();
                    Assert.AreEqual(
                        "6.3 米",
                        typedControls.GetChild("movementFeedbackValue")?.asTextField?.text);
                    Assert.AreEqual(
                        "正在接近",
                        typedControls.GetChild("movementFeedbackStatus")?.asTextField?.text);
                    Assert.NotNull(typedControls.GetChild("movementTargetMarker"));
                    Assert.IsNull(typedControls.GetChild("movementIntent"));
                    Assert.IsNull(typedControls.GetChild("movementOutcome"));
                    Assert.IsNull(typedControls.GetChild("stageKind0"));
                    Assert.IsNull(typedControls.GetChild("movementStrategy0"));
                    Assert.IsNull(typedControls.GetChild("movementTargetKind0"));
                    Assert.IsFalse(timelineEventDetails.GetChild("btnLocateStart").visible);
                    Assert.IsFalse(timelineEventDetails.GetChild("btnLocateEnd").visible);

                    selectedTypedStage.Kind = originalStageKind;
                    selectedTypedStage.Title = originalStageTitle;
                    selectedTypedStage.MovementStrategy = originalMovementStrategy;
                    selectedTypedStage.MovementTargetKind = originalMovementTargetKind;
                    selectedTypedStage.MoveMinDistance = originalMoveMinimum;
                    selectedTypedStage.MoveMaxDistance = originalMoveMaximum;
                    viewModel.BehaviorCardWorkspace.Timeline.ObservedTargetDistanceDecimeters =
                        originalObservedTargetDistance;
                    binder.Bind(panelHandle, viewModel);

                    Assert.Greater(viewModel.BehaviorCardWorkspace.Timeline.DetectorCount, 0);
                    CharacterTestRuntimeAiLabLogicDetectorViewModel repairDetector =
                        viewModel.BehaviorCardWorkspace.Timeline.Detectors[0];
                    repairDetector.Selected = true;
                    repairDetector.Trigger =
                        CharacterTestRuntimeAiLabLogicDetectorTrigger.ConditionEdge;
                    repairDetector.ConditionCount = 0;
                    repairDetector.Conditions =
                        Array.Empty<CharacterTestRuntimeAiLabLogicConditionViewModel>();
                    viewModel.BehaviorCardWorkspace.Timeline.SelectedElementKind =
                        CharacterTestRuntimeAiLabLogicElementKind.Detector;
                    viewModel.BehaviorCardWorkspace.Timeline.SelectedElementIndex = 0;
                    binder.Bind(panelHandle, viewModel);
                    Fgui.GButton logicRepairButton = timelineEventDetails.GetChild(
                        "btnLocateStart")?.asButton;
                    Assert.NotNull(logicRepairButton);
                    Assert.IsTrue(
                        logicRepairButton.visible,
                        "A missing ConditionEdge condition must expose the adjacent repair action.");
                    Assert.AreEqual("+ 添加条件", logicRepairButton.title);
                    Assert.AreEqual(
                        viewModel.BehaviorCardWorkspace.Timeline.CanEdit,
                        logicRepairButton.touchable,
                        "The adjacent repair action must follow the existing Draft edit gate.");
                    typedControls = timelineEventDetails.GetChild(
                        "behaviorLogicTypedControls")?.asCom;
                    Assert.NotNull(typedControls.GetChild("detectorTrigger0"));
                    Assert.AreEqual(
                        viewModel.BehaviorCardWorkspace.Timeline.CanEdit,
                        typedControls.GetChild("detectorTrigger1").touchable,
                        "Detector trigger choices must be directly selectable.");
                    Assert.NotNull(typedControls.GetChild("detectorScope0"));
                    Assert.NotNull(typedControls.GetChild("detectorScope1"));
                    Assert.IsFalse(
                        typedControls.GetChild("detectorWindow0").visible,
                        "The repair action replaces the detector window choice row.");
                    Assert.IsFalse(
                        timelineEventDetails.GetChild("planActionFailureLabel").visible,
                        "The repair action replaces the fourth summary row.");
                    Assert.IsFalse(
                        timelineEventDetails.GetChild("planActionFailureValue").visible,
                        "The repair action replaces the fourth summary value.");

                    viewModel.WorkspaceMode = CharacterTestRuntimeAiLabWorkspaceMode.Diagnostics;
                    viewModel.DiagnosticsTab = CharacterTestRuntimeAiLabDiagnosticsTab.Facts;
                    viewModel.ShowTimelineModeHost = false;
                    viewModel.ShowBehaviorPlan = false;
                    viewModel.ShowBehaviorLogic = false;
                    viewModel.Workspace.ActiveWorkspace = CharacterTestRuntimeAiLabWorkspaceMode.Diagnostics;
                    viewModel.Workspace.ShowBehaviorCard = false;
                    viewModel.Facts = new CharacterTestRuntimeAiLabFactsWorkspaceViewModel
                    {
                        Available = true,
                        ContextText = "Agent 2 · S10 / D9 / L10\nH 0123456789ABCDEF · C3 · Active r2 / Candidate r3",
                        HistoryStateText = "最新帧"
                    };
                    binder.Bind(panelHandle, viewModel);
                    Fgui.GGraph factsBackground = factsWorkspace.GetChild("workspaceBackground")?.asGraph;
                    Assert.IsTrue(factsWorkspace.visible);
                    Assert.NotNull(factsBackground);
                    Assert.IsTrue(factsBackground.visible);
                    Assert.GreaterOrEqual(factsBackground.width, factsWorkspace.width - 1f);
                    Assert.GreaterOrEqual(factsBackground.height, factsWorkspace.height - 1f);
                    Assert.AreEqual(1f, factsBackground.color.a);
                    Assert.IsFalse(behaviorCardLibrary.visible);
                    Assert.IsFalse(behaviorCardTimeline.visible);

                    CharacterTestRuntimeAiLabBehaviorCardLogicPayload logicPayload =
                        gestureCard.LogicPayload.Clone();
                    CharacterTestRuntimeAiLabBehaviorCardLogicCompileResult logicCompile =
                        CharacterTestRuntimeAiLabBehaviorCardLogicCompiler.Compile(logicPayload);
                    CharacterTestRuntimeAiLabBehaviorCardWorkspaceViewModelBuilder.Build(
                        viewModel,
                        "追击突进斩",
                        logicCompile,
                        logicPayload,
                        selectedStageIndex: 0,
                        behaviorEntryId: gestureEntry.EntryId);
                    viewModel.BehaviorCardWorkspace.Timeline.CanEdit = true;
                    viewModel.WorkspaceMode = CharacterTestRuntimeAiLabWorkspaceMode.BehaviorCard;
                    viewModel.Workspace.ActiveWorkspace = CharacterTestRuntimeAiLabWorkspaceMode.BehaviorCard;
                    viewModel.Workspace.ShowBehaviorCard = true;
                    binder.Bind(panelHandle, viewModel);
                    Assert.IsFalse(behaviorCardLibrary.visible);
                    Assert.IsTrue(behaviorCardTimeline.visible);
                    Assert.Greater(behaviorCardTimeline.numChildren, 10);
                    Fgui.GButton workbenchReplay = concrete.Component.GetChild(
                        CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.Reset)?.asButton;
                    Assert.NotNull(workbenchReplay);
                    Assert.IsTrue(workbenchReplay.visible);
                    Assert.AreEqual(1800f, workbenchReplay.x);
                    Assert.AreEqual(8f, workbenchReplay.y);
                    Assert.AreEqual(44f, workbenchReplay.width);
                    Assert.AreEqual(44f, workbenchReplay.height);
                    Assert.IsFalse(workbenchReplay.onRollOver.isEmpty);
                    workbenchReplay.onClick.Call();
                    Assert.AreEqual(
                        CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.Reset,
                        commandSink.Last.CommandId);

                    Fgui.GComponent warmRecipeLayer = behaviorCardTimeline.GetChild(
                        "timelineGroupHost")?.asCom;
                    Assert.NotNull(warmRecipeLayer);
                    Assert.IsTrue(warmRecipeLayer.visible);
                    Assert.IsNull(
                        behaviorCardTimeline.GetChild("runtimeTimelineContent"),
                        "The retired Stage/Detector/Transition graph must not be recreated.");
                    Fgui.GComponent warmRecipeViewport = warmRecipeLayer.GetChild(
                        "recipeViewport")?.asCom;
                    Fgui.GComponent warmRecipeRows = warmRecipeViewport?.GetChild(
                        "recipeRows")?.asCom;
                    Assert.NotNull(warmRecipeViewport);
                    Assert.NotNull(warmRecipeViewport.rootContainer.clipRect);
                    Assert.NotNull(warmRecipeRows);
                    Assert.IsNull(warmRecipeRows.GetChild("recipeTimelineHeader0"));
                    int warmPrimaryTrackIndex =
                        viewModel.BehaviorCardWorkspace.Timeline.Stages[0].EntryConditionCount > 0
                            ? 1
                            : 0;
                    Fgui.GComponent warmPrimaryTrack = warmRecipeRows.GetChild(
                        "recipeTrack" + warmPrimaryTrackIndex)?.asCom;
                    Assert.NotNull(warmPrimaryTrack);
                    Assert.AreEqual(50f, warmPrimaryTrack.height);
                    Assert.AreEqual(
                        38f,
                        warmPrimaryTrack.GetChild("trackContent")?.asCom
                            ?.GetChild("eventSpan")?.height);

                    CharacterTestFairyGuiRuntimeAiLabBehaviorCardWorkspaceBinder
                        visibilityWorkspaceBinder =
                            GetPrivateField<CharacterTestFairyGuiRuntimeAiLabBehaviorCardWorkspaceBinder>(
                                binder,
                                "_behaviorCardWorkspaceBinder");
                    CharacterTestFairyGuiRuntimeAiLabBehaviorCardTimelineBinder
                        visibilityTimelineBinder =
                            GetPrivateField<CharacterTestFairyGuiRuntimeAiLabBehaviorCardTimelineBinder>(
                                visibilityWorkspaceBinder,
                                "_timeline");
                    visibilityTimelineBinder.Bind(
                        behaviorCardTimeline,
                        viewModel.BehaviorCardWorkspace.Timeline,
                        visible: false,
                        behaviorLogic: true);
                    Assert.IsFalse(warmRecipeLayer.visible);
                    visibilityTimelineBinder.Bind(
                        behaviorCardTimeline,
                        viewModel.BehaviorCardWorkspace.Timeline,
                        visible: true,
                        behaviorLogic: true);
                    Assert.IsTrue(
                        warmRecipeLayer.visible,
                        "The recipe timeline must be restored after the shared root is hidden.");
                    Assert.IsTrue(warmPrimaryTrack.visible);
                    Assert.IsNull(warmRecipeRows.GetChild("runtimeStage0"));
                    Assert.IsNull(warmRecipeRows.GetChild("runtimeDetector0"));
                    Assert.IsNull(warmRecipeRows.GetChild("runtimeTransition0"));
                    Assert.IsNull(warmRecipeRows.GetChild("endPort"));

                    CharacterTestRuntimeAiLabBehaviorCardLogicPayload stressPayload =
                        MxFramework.Game.CharacterTest.Tests.CharacterTestRuntimeAiLabBehaviorCardWorkspaceTests
                            .CreateD1StressFixture();
                    CharacterTestRuntimeAiLabBehaviorCardLogicCompileResult stressCompile =
                        CharacterTestRuntimeAiLabBehaviorCardLogicCompiler.Compile(stressPayload);
                    Assert.IsTrue(stressCompile.Success);
                    CharacterTestRuntimeAiLabBehaviorCardWorkspaceViewModelBuilder.Build(
                        viewModel,
                        "D1 stress",
                        stressCompile,
                        stressPayload,
                        selectedStageIndex: 0,
                        selectedElementKind: CharacterTestRuntimeAiLabLogicElementKind.Transition);
                    viewModel.BehaviorCardWorkspace.Timeline.HorizontalStartFrame = 60;
                    viewModel.BehaviorCardWorkspace.Timeline.HorizontalEndFrame = 300;
                    viewModel.BehaviorCardWorkspace.Timeline.VerticalScrollOffset = 1200f;
                    visibilityTimelineBinder.Bind(
                        behaviorCardTimeline,
                        viewModel.BehaviorCardWorkspace.Timeline,
                        visible: true,
                        behaviorLogic: true);

                    Fgui.GComponent stressRecipeLayer = behaviorCardTimeline.GetChild(
                        "timelineGroupHost")?.asCom;
                    Fgui.GComponent stressViewport = stressRecipeLayer?.GetChild(
                        "recipeViewport")?.asCom;
                    Fgui.GComponent stressRows = stressViewport?.GetChild("recipeRows")?.asCom;
                    Assert.NotNull(stressRecipeLayer);
                    Assert.NotNull(stressViewport);
                    Assert.NotNull(stressViewport.rootContainer.clipRect);
                    Assert.NotNull(stressRows);
                    Assert.IsNull(stressRows.GetChild("recipeTimelineHeader0"));
                    Assert.NotNull(stressRows.GetChild("recipeTrack0"));
                    Assert.NotNull(stressRows.GetChild("recipeTrack1"));
                    Assert.NotNull(stressRecipeLayer.GetChild("recipeHorizontalScrollTrack"));
                    Assert.NotNull(stressRecipeLayer.GetChild("recipeVerticalScrollTrack"));
                    Assert.IsNull(stressRows.GetChild("runtimeTransition0"));

                    viewModel.ShowWorkbench = false;
                    viewModel.ShowTimeline = false;
                    viewModel.ShowSetupToolbar = true;
                    viewModel.WorkspaceMode = CharacterTestRuntimeAiLabWorkspaceMode.Setup;
                    viewModel.Workspace.ActiveWorkspace = CharacterTestRuntimeAiLabWorkspaceMode.Setup;
                    viewModel.Workspace.ShowBehaviorCard = false;
                    viewModel.Workspace.ShowSetup = true;
                    binder.Bind(panelHandle, viewModel);
                    Fgui.GObject setupTopBar = concrete.Component.GetChild(
                        CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.TopBar);
                    Assert.AreEqual(0f, setupTopBar.x);
                    Assert.AreEqual(0f, setupTopBar.y);
                    Assert.AreEqual(1920f, setupTopBar.width);
                    Assert.AreEqual(64f, setupTopBar.height);
                    Assert.IsTrue(
                        concrete.Component.GetChild(
                            CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.SetupWorkspaceBackground).visible,
                        "The dedicated Composer setup background must be visible before the test starts.");
                    Assert.IsFalse(
                        concrete.Component.GetChild(
                            CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.WorkspacePanel).visible,
                        "The shared runtime workspace background must not cover the dedicated setup workspace.");
                    Fgui.GComponent teamSummaryCard = concrete.Component.GetChild(
                        CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.SetupTeam1SummaryCard).asCom;
                    Assert.IsNotNull(teamSummaryCard.GetChild("validation"));
                    Assert.IsTrue(concrete.Component.GetChild(
                        CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.SetupReadyBackground).visible);
                    Assert.IsTrue(concrete.Component.GetChild(
                        CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.SetupSceneStatusBar).visible);
                }

                viewModel.WorkspaceMode = CharacterTestRuntimeAiLabWorkspaceMode.BehaviorCard;
                viewModel.Workspace.ActiveWorkspace = CharacterTestRuntimeAiLabWorkspaceMode.BehaviorCard;
                viewModel.Workspace.ShowSetup = false;
                viewModel.Workspace.ShowBehaviorCard = true;
                Assert.IsTrue(host.TryCreateComponent(
                    CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.PackageId,
                    CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.ComponentName,
                    out IMxFairyGuiComponentHandle remountedHandle,
                    out failure), failure);
                using (remountedHandle)
                {
                    Assert.DoesNotThrow(() => binder.Bind(remountedHandle, viewModel),
                        "A non-keep-alive workspace must rebuild root-owned timeline pools after remounting.");
                    var remountedConcrete = remountedHandle as MxFairyGuiComponentHandle;
                    Assert.NotNull(remountedConcrete);
                    Assert.IsTrue(remountedConcrete.Component.GetChild(
                        CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.BehaviorCardTimeline).visible);
                }

                string[] formalComponents =
                {
                    "RuntimeAiLabSetupTeamCard",
                    "RuntimeAiLabTimelineMarker",
                    "RuntimeAiLabTimelineSpan",
                    "RuntimeAiLabTimelineTick",
                    "RuntimeAiLabTimelineEventDetails",
                    "RuntimeAiLabTimelineTrack",
                    "RuntimeAiLabTimelineTrackContent"
                };
                for (int i = 0; i < formalComponents.Length; i++)
                {
                    Assert.IsTrue(host.TryCreateComponent(
                        CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.PackageId,
                        formalComponents[i],
                        out IMxFairyGuiComponentHandle handle,
                        out failure), formalComponents[i] + ": " + failure);
                    if (formalComponents[i] == "RuntimeAiLabTimelineEventDetails")
                    {
                        var eventDetailsHandle = handle as MxFairyGuiComponentHandle;
                        Assert.NotNull(eventDetailsHandle);
                        Assert.NotNull(eventDetailsHandle.Component.GetChild("detailAccent"));
                        Assert.NotNull(eventDetailsHandle.Component.GetChild("detailTrackIcon"));
                        Assert.NotNull(eventDetailsHandle.Component.GetChild("detailRangeBackground"));
                        Assert.NotNull(eventDetailsHandle.Component.GetChild("detailSummaryBackground"));
                        Assert.NotNull(eventDetailsHandle.Component.GetChild("detailEvidenceBackground"));
                        Assert.NotNull(eventDetailsHandle.Component.GetChild("detailEvidence"));
                    }
                    handle.Dispose();
                }
            }
            finally
            {
                if (packageEnsured)
                    host.ReleasePackage(CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.PackageId);
                else
                    packageScope?.Release();
                resources?.Dispose();
                if (legacyPanelAtlas != null)
                    UnityEngine.Object.DestroyImmediate(legacyPanelAtlas);
                if (gestureControllerHost != null)
                    UnityEngine.Object.DestroyImmediate(gestureControllerHost);
                RemoveCharacterTestLegacyPanelPackageIfLoaded();
                Fgui.FontManager.Clear();
            }
        }

        [Test]
        public void FairyGuiRuntimeAiLabConditionPicker_PublishedPackageExposesObjectFirstContract()
        {
            RemoveCharacterTestLegacyPanelPackageIfLoaded();
            Fgui.FontManager.Clear();
            Texture2D atlas = null;
            GlobalResourceRuntimeServices resources = null;
            MxFairyGuiPackageLoadScope packageScope = null;
            var host = new MxFairyGuiHost();
            bool packageEnsured = false;
            try
            {
                resources = CreatePlayerPanelResourceServices(
                    CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.PackageId,
                    CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.PackageBytesKey,
                    CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.Atlas0Key,
                    out atlas);
                var bridge = new MxFairyGuiResourceBridge(resources.ResourceManager);
                var package = CharacterTestFairyGuiRuntimeAiPlannerDevPanelManifest.CreatePackageDescriptor();
                MxFairyGuiPackageLoadResult load = bridge.LoadPackage(package);
                Assert.IsTrue(load.Success, load.Failure.Message);
                packageScope = load.Scope;
                Assert.IsTrue(host.EnsurePackage(packageScope, out string failure), failure);
                packageEnsured = true;

                Fgui.GComponent picker = Fgui.UIPackage.CreateObject(
                    CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.PackageId,
                    "RuntimeAiLabBehaviorConditionPicker")?.asCom;
                Assert.NotNull(picker);
                Assert.NotNull(picker.GetChild("btnTarget"));
                Assert.NotNull(picker.GetChild("btnSelf"));
                Assert.NotNull(picker.GetChild("btnEnvironment"));
                Assert.NotNull(picker.GetChild("btnEventResult"));
                Assert.NotNull(picker.GetChild("query")?.asTextInput);
                Assert.NotNull(picker.GetChild("propertyList")?.asList);
                picker.Dispose();

                Fgui.GComponent inspector = Fgui.UIPackage.CreateObject(
                    CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.PackageId,
                    "RuntimeAiLabBehaviorRecipeInspector")?.asCom;
                Assert.NotNull(inspector);
                Assert.NotNull(inspector.GetChild("btnAddConditionGroup"));
                Assert.NotNull(inspector.GetChild("conditionPickerPrototype"));
                Assert.NotNull(inspector.GetChild("targetStatePickerPrototype"));
                Assert.GreaterOrEqual(inspector.GetChild("conditionList").height, 334f);
                Assert.GreaterOrEqual(inspector.GetChild("bodyBackground").height, 520f);
                inspector.Dispose();

                Fgui.GComponent targetStatePicker = Fgui.UIPackage.CreateObject(
                    CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.PackageId,
                    "RuntimeAiLabTargetStatePicker")?.asCom;
                Assert.NotNull(targetStatePicker);
                Assert.NotNull(targetStatePicker.GetChild("phaseNoTarget")?.asButton);
                Assert.NotNull(targetStatePicker.GetChild("phaseAlive")?.asButton);
                Assert.NotNull(targetStatePicker.GetChild("flagperceivableRequired")?.asButton);
                Assert.NotNull(targetStatePicker.GetChild("flagrespondedForbidden")?.asButton);
                Assert.NotNull(targetStatePicker.GetChild("btnApply")?.asButton);
                targetStatePicker.Dispose();

                Fgui.GComponent row = Fgui.UIPackage.CreateObject(
                    CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.PackageId,
                    "RuntimeAiLabBehaviorTriggerConditionRow")?.asCom;
                Assert.NotNull(row);
                Assert.NotNull(row.GetChild("observation")?.asTextField);
                Assert.NotNull(row.GetChild("subjectScopeTitle")?.asTextField);
                Assert.GreaterOrEqual(
                    row.GetChild("observation").asTextField.textFormat.size,
                    13);
                Assert.GreaterOrEqual(row.GetChild("observation").height, 24f);
                row.Dispose();
            }
            finally
            {
                if (packageEnsured)
                    host.ReleasePackage(CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.PackageId);
                packageScope?.Dispose();
                resources?.Dispose();
                if (atlas != null)
                    UnityEngine.Object.DestroyImmediate(atlas);
                Fgui.FontManager.Clear();
            }
        }

        [Test]
        public void FairyGuiRuntimeAiLabWorkbenchList_PublishedPackageUsesNativeListAndSelectedCardActions()
        {
            RemoveCharacterTestLegacyPanelPackageIfLoaded();
            Fgui.FontManager.Clear();
            Texture2D legacyPanelAtlas = null;
            GlobalResourceRuntimeServices resources = null;
            MxFairyGuiPackageLoadScope packageScope = null;
            var host = new MxFairyGuiHost();
            bool packageEnsured = false;
            try
            {
                resources = CreatePlayerPanelResourceServices(
                    CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.PackageId,
                    CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.PackageBytesKey,
                    CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.Atlas0Key,
                    out legacyPanelAtlas);
                var bridge = new MxFairyGuiResourceBridge(resources.ResourceManager);
                var package = CharacterTestFairyGuiRuntimeAiPlannerDevPanelManifest.CreatePackageDescriptor();
                MxFairyGuiPackageLoadResult load = bridge.LoadPackage(package);
                Assert.IsTrue(load.Success, load.Failure.Message);
                packageScope = load.Scope;
                Assert.IsTrue(host.EnsurePackage(packageScope, out string failure), failure);
                packageEnsured = true;
                Assert.IsTrue(host.TryCreateComponent(
                    CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.PackageId,
                    CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.ComponentName,
                    out IMxFairyGuiComponentHandle panelHandle,
                    out failure), failure);

                using (panelHandle)
                {
                    var concrete = panelHandle as MxFairyGuiComponentHandle;
                    Assert.NotNull(concrete);
                    Fgui.GList workbenchList = concrete.Component.GetChild(
                        CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.WorkbenchEntryList)?.asList;
                    Assert.NotNull(workbenchList);
                    Assert.NotNull(workbenchList.scrollPane);
                    Assert.IsNotEmpty(workbenchList.defaultItem);

                    var commandSink = new RecordingUiCommandSink();
                    var rootSession = new CharacterTestFairyGuiRuntimeAiPlannerDevPanelRootSession(
                        CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.ViewId);
                    rootSession.ResetForRoot(concrete.Component);
                    var panelOverlayBinder = new CharacterTestFairyGuiRuntimeAiLabPanelOverlayBinder(
                        commandSink,
                        CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.ViewId,
                        popupService: null,
                        rootSession);
                    var contentBinder = new CharacterTestFairyGuiRuntimeAiLabPanelContentBinder(
                        commandSink,
                        CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.ViewId,
                        rootSession,
                        panelOverlayBinder,
                        behaviorCardPreviewSurface: null,
                        bindBehaviorCardRuntimeControls: null);
                    var viewModel = new CharacterTestFairyGuiRuntimeAiPlannerDevPanelViewModel
                    {
                        WorkspaceMode = CharacterTestRuntimeAiLabWorkspaceMode.BehaviorCard,
                        ShowBehaviorLogic = true,
                        ShowWorkbench = false,
                        CanEditWorkbench = true,
                        ToggleEntryLabel = "Disable",
                        WorkbenchResponsibilityCounts = new[] { 0, 0, 0, 2, 0 },
                        WorkbenchRowCount = 3,
                        WorkbenchRows = new[]
                        {
                            new CharacterTestRuntimeAiLabWorkbenchRowViewModel
                            {
                                RowKind = CharacterTestRuntimeAiLabWorkbenchRowKind.ResponsibilityHeader,
                                ResponsibilityKind = CharacterTestRuntimeAiPlannerBehaviorResponsibilityKind.Damage,
                                TitleText = "输出",
                                ResponsibilityEnabledCount = 2,
                                ResponsibilityTotalCount = 2,
                                ResponsibilitySummaryText = "Basic attack、Retreat",
                                ResponsibilityColor = "#E3634D",
                                Visible = false
                            },
                            new CharacterTestRuntimeAiLabWorkbenchRowViewModel
                            {
                                EntryIndex = 4,
                                ResponsibilityKinds = new[]
                                {
                                    CharacterTestRuntimeAiPlannerBehaviorResponsibilityKind.EngageOrPressure,
                                    CharacterTestRuntimeAiPlannerBehaviorResponsibilityKind.Damage
                                },
                                ResponsibilityKind = CharacterTestRuntimeAiPlannerBehaviorResponsibilityKind.Damage,
                                ResponsibilityDisplayText = "接战 +1",
                                TitleText = "Basic attack",
                                BadgeText = "Ready",
                                TriggerText = "Continuous",
                                PriorityText = "Priority 100",
                                ExecutionState = CharacterTestRuntimeAiLabExecutionDisplayState.RunningNow,
                                Visible = true,
                                Selected = true
                            },
                            new CharacterTestRuntimeAiLabWorkbenchRowViewModel
                            {
                                EntryIndex = 5,
                                ResponsibilityKinds = new[]
                                {
                                    CharacterTestRuntimeAiPlannerBehaviorResponsibilityKind.Damage
                                },
                                ResponsibilityKind = CharacterTestRuntimeAiPlannerBehaviorResponsibilityKind.Damage,
                                ResponsibilityDisplayText = "输出",
                                TitleText = "Retreat",
                                BadgeText = "Ready",
                                TriggerText = "Continuous",
                                PriorityText = "Priority 90",
                                ExecutionState = CharacterTestRuntimeAiLabExecutionDisplayState.ExecutedAtSelectedFrame,
                                Visible = true,
                                Selected = false
                            }
                        }
                    };

                    workbenchList.SetSize(460f, 240f);
                    contentBinder.BindWorkbenchRows(concrete.Component, viewModel);

                    Fgui.GButton allTab = concrete.Component.GetChild(
                        CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.ResponsibilityTabAll)?.asButton;
                    Fgui.GButton engageTab = concrete.Component.GetChild(
                        CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.ResponsibilityTabEngage)?.asButton;
                    Fgui.GButton damageTab = concrete.Component.GetChild(
                        CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.ResponsibilityTabDamage)?.asButton;
                    Fgui.GButton otherTab = concrete.Component.GetChild(
                        CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.ResponsibilityTabOther)?.asButton;
                    Assert.NotNull(allTab);
                    Assert.NotNull(engageTab);
                    Assert.NotNull(damageTab);
                    Assert.NotNull(otherTab);
                    Assert.IsTrue(allTab.visible,
                        "Behavior Logic must show responsibility tabs even when the legacy ShowWorkbench toggle is false.");
                    Assert.IsTrue(engageTab.visible);
                    Assert.IsTrue(damageTab.visible);
                    Assert.IsTrue(allTab.enabled);
                    Assert.IsTrue(engageTab.enabled);
                    Assert.IsTrue(damageTab.enabled);
                    Assert.IsTrue(allTab.touchable);
                    Assert.IsTrue(engageTab.touchable);
                    Assert.IsTrue(damageTab.touchable);
                    Assert.AreEqual("全部 3", allTab.title);
                    Assert.AreEqual("接战 0", engageTab.title);
                    Assert.AreEqual("输出 2", damageTab.title);
                    Assert.IsTrue(engageTab.GetChild("responsibilityMissingWarning").visible,
                        "An empty required responsibility must be visibly marked as a warning.");
                    Assert.IsFalse(damageTab.GetChild("responsibilityMissingWarning").visible);
                    Assert.IsFalse(otherTab.GetChild("responsibilityMissingWarning").visible,
                        "Other is an optional classification and must not be treated as missing.");

                    engageTab.onClick.BubbleCall();
                    Assert.AreEqual(1, workbenchList.numItems,
                        "Clicking a responsibility tab must immediately filter the shared list.");
                    allTab.onClick.BubbleCall();
                    Assert.AreEqual(2, workbenchList.numItems,
                        "Returning to All must immediately restore each unique entry once.");

                    Assert.AreEqual(Fgui.ListLayoutType.SingleColumn, workbenchList.layout);
                    Assert.AreEqual(Fgui.ListSelectionMode.None, workbenchList.selectionMode);
                    Assert.AreEqual(2, workbenchList.numItems);
                    Fgui.GComponent selectedCard = workbenchList.GetChildAt(0)?.asCom;
                    Fgui.GComponent unselectedCard = workbenchList.GetChildAt(1)?.asCom;
                    Assert.NotNull(selectedCard);
                    Assert.NotNull(unselectedCard);
                    Assert.AreEqual(460f, selectedCard.width,
                        "All responsibility tabs must share one full-width behavior list.");
                    Assert.AreEqual(0f, selectedCard.x);
                    Assert.AreEqual(68f, selectedCard.height);
                    Assert.AreEqual(18f, selectedCard.GetChild("title").x);
                    Assert.AreEqual(
                        (Color)CharacterTestFairyGuiRuntimeAiLabSemanticColors.RuntimeActiveText,
                        selectedCard.GetChild("title").asTextField.color,
                        "Only the behavior that is truly running now must use the active green title color.");
                    Assert.AreEqual(
                        (Color)CharacterTestFairyGuiRuntimeAiLabSemanticColors.PrimaryText,
                        unselectedCard.GetChild("title").asTextField.color,
                        "Historical execution and every non-running state must keep the primary white title color.");
                    viewModel.WorkbenchRows[1].ExecutionState =
                        CharacterTestRuntimeAiLabExecutionDisplayState.None;
                    viewModel.WorkbenchRows[2].ExecutionState =
                        CharacterTestRuntimeAiLabExecutionDisplayState.RunningNow;
                    contentBinder.BindWorkbenchRows(concrete.Component, viewModel);
                    Assert.AreEqual(
                        (Color)CharacterTestFairyGuiRuntimeAiLabSemanticColors.PrimaryText,
                        selectedCard.GetChild("title").asTextField.color,
                        "The previously running behavior must return to primary white after the runtime switches away.");
                    Assert.AreEqual(
                        (Color)CharacterTestFairyGuiRuntimeAiLabSemanticColors.RuntimeActiveText,
                        unselectedCard.GetChild("title").asTextField.color,
                        "The newly running behavior must turn green without changing the editing selection.");
                    Assert.IsFalse(selectedCard.GetChild("orderText").visible);
                    Assert.IsFalse(selectedCard.GetChild("triggerText").visible);
                    Assert.IsFalse(selectedCard.GetChild("priorityText").visible);
                    Assert.IsFalse(selectedCard.GetChild("badgeText").visible);

                    Fgui.GButton selectedToggle = selectedCard.GetChild("inlineToggleEntry")?.asButton;
                    Fgui.GButton selectedDelete = selectedCard.GetChild("inlineDeleteEntry")?.asButton;
                    Fgui.GButton unselectedToggle = unselectedCard.GetChild("inlineToggleEntry")?.asButton;
                    Fgui.GButton unselectedDelete = unselectedCard.GetChild("inlineDeleteEntry")?.asButton;
                    Fgui.GButton selectedResponsibilities =
                        selectedCard.GetChild("inlineResponsibilities")?.asButton;
                    Fgui.GButton unselectedResponsibilities =
                        unselectedCard.GetChild("inlineResponsibilities")?.asButton;
                    Assert.NotNull(selectedToggle);
                    Assert.NotNull(selectedDelete);
                    Assert.NotNull(unselectedToggle);
                    Assert.NotNull(unselectedDelete);
                    Assert.NotNull(selectedResponsibilities);
                    Assert.NotNull(unselectedResponsibilities);
                    Assert.IsEmpty(selectedResponsibilities.title,
                        "Behavior rows must not repeat their responsibility tab names.");
                    Assert.IsEmpty(unselectedResponsibilities.title);
                    Assert.IsNotEmpty(selectedResponsibilities.icon);
                    Assert.IsTrue(selectedResponsibilities.visible);
                    Assert.IsFalse(unselectedResponsibilities.visible);
                    Assert.IsTrue(selectedToggle.visible);
                    Assert.IsTrue(selectedDelete.visible);
                    Assert.IsTrue(selectedToggle.touchable);
                    Assert.IsTrue(selectedDelete.touchable);
                    Assert.IsFalse(unselectedToggle.visible);
                    Assert.IsFalse(unselectedDelete.visible);

                    selectedToggle.onClick.BubbleCall();
                    Assert.AreEqual(
                        CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.ToggleEntry,
                        commandSink.Last.CommandId,
                        "The inline toggle must stop bubbling before the list replaces its command with row selection.");
                    selectedCard.onClick.BubbleCall();
                    Assert.AreEqual(
                        CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.SelectWorkbenchEntry(4),
                        commandSink.Last.CommandId);
                    selectedDelete.onClick.BubbleCall();
                    Assert.AreEqual(
                        CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.DeleteEntry,
                        commandSink.Last.CommandId,
                        "The inline delete action must remain owned by the selected card.");
                    Assert.AreEqual(
                        4,
                        commandSink.Last.Payload,
                        "The inline delete action must carry the owning entry instead of relying on mutable selection state.");
                }
            }
            finally
            {
                if (packageEnsured)
                    host.ReleasePackage(CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.PackageId);
                else
                    packageScope?.Release();
                resources?.Dispose();
                if (legacyPanelAtlas != null)
                    UnityEngine.Object.DestroyImmediate(legacyPanelAtlas);
                RemoveCharacterTestLegacyPanelPackageIfLoaded();
                Fgui.FontManager.Clear();
            }
        }

        [Test]
        public void FairyGuiRuntimeAiLabFactsResponsiveLayout_UsesCompactModeAtFullHd()
        {
            Assert.IsTrue(
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelBinder.IsFactsCompactLayout(1920));
            Assert.IsFalse(
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelBinder.IsFactsCompactLayout(1921));
            CharacterTestFairyGuiRuntimeAiPlannerDevPanelBinder.ResolveFactsResponsiveTableLayout(
                1920,
                CharacterTestRuntimeAiLabFactsTableTab.Facts,
                out bool showFacts,
                out bool showCandidates,
                out float candidateX,
                out float candidateWidth);
            Assert.IsTrue(showFacts);
            Assert.IsFalse(showCandidates);
            Assert.AreEqual(244f, candidateX);
            Assert.AreEqual(1000f, candidateWidth);
            CharacterTestFairyGuiRuntimeAiPlannerDevPanelBinder.ResolveFactsResponsiveTableLayout(
                1920,
                CharacterTestRuntimeAiLabFactsTableTab.Candidates,
                out showFacts,
                out showCandidates,
                out candidateX,
                out candidateWidth);
            Assert.IsFalse(showFacts);
            Assert.IsTrue(showCandidates);
            Assert.AreEqual(244f, candidateX);
            Assert.AreEqual(1000f, candidateWidth);
            CharacterTestFairyGuiRuntimeAiPlannerDevPanelBinder.ResolveFactsResponsiveTableLayout(
                2560,
                CharacterTestRuntimeAiLabFactsTableTab.Facts,
                out showFacts,
                out showCandidates,
                out candidateX,
                out candidateWidth);
            Assert.IsTrue(showFacts);
            Assert.IsTrue(showCandidates);
            Assert.AreEqual(1260f, candidateX);
            Assert.AreEqual(612f, candidateWidth);
        }

[Test]
        public void FairyGuiBehaviorDeckPanelHost_WithPublishedPackage_LoadsBindsAndReleases()
        {
            RemoveCharacterTestLegacyPanelPackageIfLoaded();
            MxUiViewContract runtimeContract =
                CharacterTestFairyGuiBehaviorDeckPanelHost.CreateRuntimeViewContractForTests();
            Assert.IsTrue(runtimeContract.Commands.Any(command =>
                string.Equals(
                    command.CommandId,
                    CharacterTestFairyGuiBehaviorDeckPanelCommandIds.CardAction,
                    StringComparison.Ordinal)
                && string.Equals(
                    command.PayloadType,
                    typeof(CharacterTestFairyGuiBehaviorDeckCardActionPayload).FullName,
                    StringComparison.Ordinal)));
            Texture2D behaviorDeckAtlas = null;
            GlobalResourceRuntimeServices resources = null;
            GameSlice slice = null;
            var cameraObject = new GameObject("CharacterTest Behavior Deck Main Camera Test");
            var hostObject = new GameObject("CharacterTest Behavior Deck Host Test");
            try
            {
                CharacterTestCharacterActionRuntime actionRuntime = CharacterTestCharacterActionConfigSource.Load();
                var references = new CharacterTestRuntimeAiLabBehaviorCardReferenceCatalog(
                    CharacterTestAbilityCatalogSource.CreateRegistry(),
                    actionRuntime.Registry);
                string longDescription = "Description start. "
                    + new string('D', 620)
                    + " Description end.";
                string longUse = "Use start. "
                    + new string('U', 520)
                    + " Use end.";
                string longRisk = "Risk start. "
                    + new string('R', 520)
                    + " Risk end.";
                string expectedDetailsText = longDescription
                    + "\n\n用途：" + longUse
                    + "\n\n风险：" + longRisk;
                const int publishedCardCount = 10;
                var publishedCards = new CharacterTestRuntimeAiPlannerPublishedBehaviorCard[publishedCardCount];
                var unlockedCardIds = new string[publishedCardCount];
                for (int i = 0; i < publishedCardCount; i++)
                {
                    string stableCardId = i == 0
                        ? "card.ui-test"
                        : "card.ui-test-" + i.ToString("00", CultureInfo.InvariantCulture);
                    unlockedCardIds[i] = stableCardId;
                    CharacterTestRuntimeAiLabBehaviorCardPayload payload =
                        CharacterTestRuntimeAiLabBehaviorCardFactory.CreateActionChainFixture();
                    var draft = new CharacterTestRuntimeAiLabBehaviorCardDraft(
                        cardDraftId: null,
                        publishedStableId: stableCardId,
                        displayName: "UI Test Card " + i.ToString(CultureInfo.InvariantCulture),
                        category: "Test",
                        payload: payload);
                    draft.LogicPayload = CharacterTestRuntimeAiLabBehaviorCardV1Migration.Convert(payload);
                    var metadata = new CharacterTestRuntimeAiPlannerPublishedBehaviorCardMetadata(
                        stableCardId,
                        "ui." + stableCardId,
                        1,
                        i == 0
                            ? longDescription
                            : "UI test card " + i.ToString(CultureInfo.InvariantCulture),
                        i == 0
                            ? longUse
                            : "UI test use",
                        i == 0
                            ? longRisk
                            : "UI test risk");
                    CharacterTestRuntimeAiPlannerPublishedBehaviorCardPublishResult publishResult =
                        CharacterTestRuntimeAiPlannerPublishedBehaviorCardPublisher.ValidateAndCreate(
                            draft,
                            new CharacterTestRuntimeAiPlannerPublishedBehaviorCardPublishRequest(
                                stableCardId,
                                1,
                                1,
                                1,
                                metadata),
                            references);
                    Assert.IsTrue(publishResult.Success);
                    publishedCards[i] = publishResult.Card;
                }
                CharacterTestRuntimeAiPlannerPublishedBehaviorCardCatalogCreateResult catalogResult =
                    CharacterTestRuntimeAiPlannerPublishedBehaviorCardCatalog.ValidateAndCreate(
                        "character-test.behavior-deck.ui-test",
                        1,
                        publishedCards,
                        references);
                Assert.IsTrue(catalogResult.Success);
                CharacterTestRuntimeAiPlannerInstalledDeckBuildResult deckResult =
                    CharacterTestRuntimeAiPlannerInstalledDeckRules.ValidateAndCreate(
                        new CharacterTestRuntimeAiPlannerInstalledDeckCandidate(
                            "character.grunt",
                            Array.Empty<string>(),
                            1),
                        catalogResult.Catalog,
                        CharacterTestRuntimeAiPlannerPublishedBehaviorCardPublisher.CurrentRuntimeContractVersion);
                Assert.IsTrue(deckResult.Success);
                CharacterTestRuntimeAiPlannerUnlockedCardCollectionResult unlocked =
                    CharacterTestRuntimeAiPlannerUnlockedCardCollection.ValidateAndCreate(
                        unlockedCardIds);
                CharacterTestRuntimeAiPlannerPlayerDeckWorkbenchCreateResult workbenchResult =
                    CharacterTestRuntimeAiPlannerPlayerDeckWorkbench.ValidateAndCreate(
                        catalogResult.Catalog,
                        unlocked.Collection,
                        deckResult.Deck);
                Assert.IsTrue(workbenchResult.Success);
                Assert.IsTrue(
                    CharacterTestFairyGuiCombatHudViewModelBuilder.TryResolveBehaviorCardMetadata(
                        workbenchResult.Workbench,
                        "card.ui-test",
                        out string hudDisplayName,
                        out string hudIconKey));
                Assert.AreEqual("card.ui-test", hudDisplayName);
                Assert.AreEqual("ui.card.ui-test", hudIconKey);

                resources = CreatePlayerPanelResourceServices(
                    CharacterTestFairyGuiBehaviorDeckPanelIds.PackageId,
                    CharacterTestFairyGuiBehaviorDeckPanelIds.PackageBytesKey,
                    CharacterTestFairyGuiBehaviorDeckPanelIds.Atlas0Key,
                    out behaviorDeckAtlas);
                Assert.IsTrue(resources.ResourceManager.Contains(
                    CharacterTestFairyGuiBehaviorDeckPanelIds.PackageBytesKey));
                Assert.IsTrue(resources.ResourceManager.Contains(
                    CharacterTestFairyGuiBehaviorDeckPanelIds.Atlas0Key));
                slice = new GameSlice(
                    NullRuntimeLogger.Instance,
                    CharacterTestStoryFixture.CreateBootstrapContent(),
                    resources,
                    baseResourcePreloadGroupId: string.Empty);

                cameraObject.tag = "MainCamera";
                cameraObject.AddComponent<UnityEngine.Camera>();
                CharacterTestFairyGuiBehaviorDeckPanelHost host =
                    hostObject.AddComponent<CharacterTestFairyGuiBehaviorDeckPanelHost>();
                string labEditedCardId = string.Empty;
                double behaviorDeckUiClock = 0d;
                host.Configure(
                    slice,
                    workbenchResult.Workbench,
                    NullRuntimeLogger.Instance,
                    openCardInLab: stableCardId =>
                    {
                        labEditedCardId = stableCardId;
                        return true;
                    },
                    unscaledTimeProvider: () => behaviorDeckUiClock);

                Assert.IsTrue(host.IsConfigured, host.LastStatusMessage);
                Assert.IsTrue(host.Open(), host.LastStatusMessage);
                Assert.IsTrue(host.IsOpen, host.LastStatusMessage);
                Assert.NotNull(host.LastViewModel);
                Assert.AreEqual("行为卡组", host.LastViewModel.Title);
                Assert.AreEqual(6, host.LastViewModel.DeckCards.Count);
                StringAssert.Contains("警告", host.LastViewModel.DiagnosticsText);
                CharacterTestFairyGuiBehaviorDeckPanelViewModel initialViewModel = host.LastViewModel;
                host.Refresh();
                Assert.AreSame(initialViewModel, host.LastViewModel);
                Assert.AreEqual("BehaviorDeckPanel", CharacterTestFairyGuiBehaviorDeckPanelIds.ComponentName);
                Fgui.GComponent root = host.RootForTests;
                Assert.NotNull(root);
                Fgui.GButton libraryAction =
                    root.GetChild("libraryCard0").asCom.GetChild("btnAction").asButton;
                Assert.NotNull(libraryAction);
                Assert.IsTrue(libraryAction.focusable);
                AssertBehaviorDeckFocusPath(root, "libraryCard0/btnAction");
                Fgui.GComponent libraryCard = root.GetChild("libraryCard0").asCom;
                Assert.NotNull(libraryCard.GetChild("slot")?.asTextField);
                Assert.NotNull(libraryCard.GetChild("blockReason")?.asTextField);
                Assert.NotNull(libraryCard.GetChild("inlineFeedback")?.asTextField);
                Assert.NotNull(libraryCard.GetChild("feedbackBackground")?.asGraph);
                Assert.NotNull(libraryCard.GetChild("btnSecondaryAction")?.asButton);
                StringAssert.EndsWith("…", libraryCard.GetChild("description").asTextField.text);
                StringAssert.EndsWith("…", libraryCard.GetChild("use").asTextField.text);
                StringAssert.EndsWith("…", libraryCard.GetChild("risk").asTextField.text);
                StringAssert.Contains("Description start", libraryCard.tooltips);
                StringAssert.Contains("Risk start", libraryCard.GetChild("risk").tooltips);
                Fgui.GButton details = libraryCard.GetChild("btnDetails").asButton;
                Assert.NotNull(details);
                Assert.IsTrue(details.visible);
                details.onClick.Call();
                host.Refresh();
                Assert.IsTrue(host.LastViewModel.DetailsVisible);
                Assert.IsTrue(root.GetChild(CharacterTestFairyGuiBehaviorDeckPanelIds.DetailsBackdrop).visible);
                Assert.IsTrue(root.GetChild(CharacterTestFairyGuiBehaviorDeckPanelIds.DetailsTitle).visible);
                Assert.IsTrue(root.GetChild(CharacterTestFairyGuiBehaviorDeckPanelIds.DetailsBody).visible);
                Assert.IsFalse(libraryAction.enabled);
                Assert.IsFalse(root.GetChild(CharacterTestFairyGuiBehaviorDeckPanelIds.Close).asButton.enabled);
                Assert.Greater(host.LastViewModel.DetailsPageCount, 1);
                Assert.AreEqual(0, host.LastViewModel.DetailsPageIndex);
                Assert.IsTrue(root.GetChild(
                    CharacterTestFairyGuiBehaviorDeckPanelIds.DetailsPageStatus).visible);
                Fgui.GButton previousDetailsPage = root.GetChild(
                    CharacterTestFairyGuiBehaviorDeckPanelIds.PreviousDetailsPage).asButton;
                Fgui.GButton nextDetailsPage = root.GetChild(
                    CharacterTestFairyGuiBehaviorDeckPanelIds.NextDetailsPage).asButton;
                Assert.IsFalse(previousDetailsPage.enabled);
                Assert.IsTrue(nextDetailsPage.enabled);

                int initialDeckPageIndex = host.CommandSink.PageIndex;
                int initialSlotCount = workbenchResult.Workbench.View.SlotCount;
                host.CommandSink.Enqueue(new MxUiCommand(
                    CharacterTestFairyGuiBehaviorDeckPanelIds.ViewId,
                    CharacterTestFairyGuiBehaviorDeckPanelCommandIds.NextPage,
                    null));
                host.CommandSink.Enqueue(new MxUiCommand(
                    CharacterTestFairyGuiBehaviorDeckPanelIds.ViewId,
                    CharacterTestFairyGuiBehaviorDeckPanelCommandIds.CardAction,
                    new CharacterTestFairyGuiBehaviorDeckCardActionPayload
                    {
                        StableCardId = "card.ui-test",
                        Action = CharacterTestFairyGuiBehaviorDeckCardAction.Install,
                        Origin = CharacterTestFairyGuiBehaviorDeckCardOrigin.Library,
                        ActionRole = CharacterTestFairyGuiBehaviorDeckCardActionRole.Primary
                    }));
                host.CommandSink.Enqueue(new MxUiCommand(
                    CharacterTestFairyGuiBehaviorDeckPanelIds.ViewId,
                    CharacterTestFairyGuiBehaviorDeckPanelCommandIds.Close,
                    null));
                host.Refresh();
                Assert.IsTrue(host.IsOpen);
                Assert.IsTrue(host.LastViewModel.DetailsVisible);
                Assert.AreEqual(initialDeckPageIndex, host.CommandSink.PageIndex);
                Assert.AreEqual(initialSlotCount, workbenchResult.Workbench.View.SlotCount);

                nextDetailsPage.onClick.Call();
                host.Refresh();
                Assert.AreEqual(1, host.LastViewModel.DetailsPageIndex);
                Assert.IsTrue(previousDetailsPage.enabled);
                previousDetailsPage.onClick.Call();
                host.Refresh();
                Assert.AreEqual(0, host.LastViewModel.DetailsPageIndex);

                var detailsText = new StringBuilder(expectedDetailsText.Length);
                while (true)
                {
                    detailsText.Append(root.GetChild(
                        CharacterTestFairyGuiBehaviorDeckPanelIds.DetailsBody).asTextField.text);
                    if (!host.LastViewModel.CanNextDetailsPage)
                        break;
                    nextDetailsPage.onClick.Call();
                    host.Refresh();
                }
                Assert.AreEqual(expectedDetailsText, detailsText.ToString());
                Assert.AreEqual(
                    host.LastViewModel.DetailsPageCount - 1,
                    host.LastViewModel.DetailsPageIndex);
                Assert.IsFalse(nextDetailsPage.enabled);

                host.CommandSink.Enqueue(new MxUiCommand(
                    CharacterTestFairyGuiBehaviorDeckPanelIds.ViewId,
                    MxFramework.UI.FairyGui.MxFairyGuiCommandIds.Cancel,
                    null));
                host.Refresh();
                Assert.IsTrue(host.IsOpen);
                Assert.IsFalse(host.LastViewModel.DetailsVisible);
                Assert.IsFalse(root.GetChild(CharacterTestFairyGuiBehaviorDeckPanelIds.DetailsBackdrop).visible);
                Assert.AreEqual(initialDeckPageIndex, host.CommandSink.PageIndex);
                Assert.AreEqual(initialSlotCount, workbenchResult.Workbench.View.SlotCount);
                Assert.IsTrue(libraryAction.enabled);
                Assert.IsTrue(details.focused);

                details.onClick.Call();
                host.Refresh();
                Assert.IsTrue(host.LastViewModel.DetailsVisible);
                Fgui.GButton closeDetails =
                    root.GetChild(CharacterTestFairyGuiBehaviorDeckPanelIds.CloseDetails).asButton;
                Assert.IsTrue(closeDetails.visible);
                Assert.IsTrue(closeDetails.focused);
                closeDetails.onClick.Call();
                host.Refresh();
                Assert.IsFalse(host.LastViewModel.DetailsVisible);
                Assert.IsFalse(root.GetChild(CharacterTestFairyGuiBehaviorDeckPanelIds.DetailsBackdrop).visible);
                Assert.IsTrue(libraryAction.enabled);
                Assert.IsTrue(details.focused);
                Fgui.GButton labEdit =
                    root.GetChild("libraryCard0").asCom.GetChild("btnLabEdit").asButton;
                Assert.NotNull(labEdit);
                Assert.IsTrue(labEdit.visible);
                AssertBehaviorDeckFocusPath(root, "libraryCard0/btnLabEdit");
                labEdit.onClick.Call();
                Assert.AreEqual("card.ui-test", labEditedCardId);
                libraryAction.onClick.Call();
                Assert.AreEqual(1, workbenchResult.Workbench.View.SlotCount);

                host.Refresh();
                Assert.IsTrue(host.LastViewModel.FeedbackVisible);
                Assert.IsTrue(host.LastViewModel.BudgetDeltaVisible);
                Assert.IsTrue(libraryCard.GetChild("feedbackBackground").visible);
                Assert.IsNotEmpty(libraryCard.GetChild("inlineFeedback").asTextField.text);
                StringAssert.Contains("1", root.GetChild("deckCard0").asCom.GetChild("slot").asTextField.text);
                AssertBehaviorDeckFocusPath(root, "libraryCard0/btnAction");
                Assert.IsTrue(libraryAction.focused);

                Fgui.GButton secondLibraryAction =
                    root.GetChild("libraryCard1").asCom.GetChild("btnAction").asButton;
                secondLibraryAction.onClick.Call();
                host.Refresh();
                Assert.AreEqual(2, workbenchResult.Workbench.View.SlotCount);
                Assert.IsTrue(secondLibraryAction.focused);

                Fgui.GButton firstDeckRemove =
                    root.GetChild("deckCard0").asCom.GetChild("btnAction").asButton;
                firstDeckRemove.onClick.Call();
                host.Refresh();
                Assert.AreEqual(1, workbenchResult.Workbench.View.SlotCount);
                Assert.AreEqual("card.ui-test-01", host.LastViewModel.DeckCards[0].StableCardId);
                Assert.IsTrue(libraryAction.focused);

                libraryAction.onClick.Call();
                host.Refresh();
                Assert.AreEqual(2, workbenchResult.Workbench.View.SlotCount);
                Fgui.GButton staleDeckRemove =
                    root.GetChild("deckCard0").asCom.GetChild("btnAction").asButton;
                CharacterTestRuntimeAiPlannerPlayerDeckWorkbenchResult externalRemove =
                    workbenchResult.Workbench.TryRemove("card.ui-test");
                Assert.IsTrue(externalRemove.Success);
                staleDeckRemove.onClick.Call();
                host.Refresh();
                Assert.AreEqual(CharacterTestFairyGuiBehaviorDeckFeedbackTone.Error, host.LastViewModel.BudgetDeltaTone);
                Assert.IsTrue(libraryAction.focused);

                root.GetChild("btnNextPage").asButton.onClick.Call();
                host.Refresh();
                Assert.AreEqual(1, host.CommandSink.PageIndex);
                host.CommandSink.Enqueue(new MxUiCommand(
                    CharacterTestFairyGuiBehaviorDeckPanelIds.ViewId,
                    CharacterTestFairyGuiBehaviorDeckPanelCommandIds.CardAction,
                    new CharacterTestFairyGuiBehaviorDeckCardActionPayload
                    {
                        StableCardId = "card.ui-test",
                        Action = CharacterTestFairyGuiBehaviorDeckCardAction.Install,
                        Origin = CharacterTestFairyGuiBehaviorDeckCardOrigin.Library,
                        ActionRole = CharacterTestFairyGuiBehaviorDeckCardActionRole.Primary
                    }));
                host.Refresh();
                Assert.IsTrue(root.GetChild("deckHeader").focused);

                root.GetChild("btnPreviousPage").asButton.onClick.Call();
                host.Refresh();
                CharacterTestFairyGuiBehaviorDeckPanelViewModel visibleFeedbackViewModel =
                    host.LastViewModel;
                host.Refresh();
                Assert.AreSame(visibleFeedbackViewModel, host.LastViewModel);
                behaviorDeckUiClock = 2.4d;
                host.Refresh();
                Assert.IsFalse(host.LastViewModel.FeedbackVisible);
                Assert.IsFalse(libraryCard.GetChild("feedbackBackground").visible);
                Assert.IsEmpty(libraryCard.GetChild("inlineFeedback").asTextField.text);
                Assert.IsEmpty(host.CommandSink.LastStatusMessage);
                libraryAction.onClick.Call();
                Assert.AreEqual(1, workbenchResult.Workbench.View.SlotCount);

                host.Refresh();
                libraryAction.onClick.Call();
                Assert.AreEqual(2, workbenchResult.Workbench.View.SlotCount);
                host.Refresh();
                Assert.IsTrue(host.LastViewModel.FeedbackVisible);

                CharacterTestRuntimeAiPlannerPlayerDeckWorkbenchResult lockResult =
                    workbenchResult.Workbench.LockSession();
                Assert.IsTrue(lockResult.Success);
                host.Refresh();
                Assert.IsFalse(host.LastViewModel.FeedbackVisible);
                Assert.IsEmpty(host.CommandSink.LastStatusMessage);
                Assert.IsFalse(host.LastViewModel.LibraryCards[0].ActionEnabled);
                Assert.IsTrue(host.LastViewModel.LibraryCards[0].CanEditInLab);
                AssertBehaviorDeckFocusPath(root, "libraryCard0/btnLabEdit");

                Assert.IsTrue(host.Close());
                Assert.IsFalse(host.IsOpen);
                Assert.DoesNotThrow(() => host.Release());
                Assert.DoesNotThrow(() => host.Release());
            }
            finally
            {
                CharacterTestFairyGuiBehaviorDeckPanelHost host =
                    hostObject.GetComponent<CharacterTestFairyGuiBehaviorDeckPanelHost>();
                host?.Release();
                slice?.Dispose();
                resources?.Dispose();
                UnityEngine.Object.DestroyImmediate(hostObject);
                UnityEngine.Object.DestroyImmediate(cameraObject);
                DestroyObjectNamed("Stage Camera");
                if (behaviorDeckAtlas != null)
                    UnityEngine.Object.DestroyImmediate(behaviorDeckAtlas);
                RemoveCharacterTestLegacyPanelPackageIfLoaded();
            }
        }

        [Test]
        public void UiRuntime_BehaviorDeckExplicitOpen_UsesPlayerWorkbenchAndClosesCleanly()
        {
            RemoveCharacterTestLegacyPanelPackageIfLoaded();
            string directory = Path.Combine(
                Path.GetTempPath(),
                "mx-runtime-ai-lab-deck-ui-" + Guid.NewGuid().ToString("N"));
            Texture2D behaviorDeckAtlas = null;
            GlobalResourceRuntimeServices resources = null;
            GameSlice slice = null;
            var cameraObject = new GameObject("Runtime AI Lab Deck Main Camera Test");
            var hostObject = new GameObject("Runtime AI Lab Deck UI Runtime Test");
            CharacterTestUiRuntime runtime = null;
            try
            {
                Assert.IsTrue(CharacterTestRuntimeAiPlannerLocalPlayerDeckService.TryCreate(
                    Path.Combine(directory, "deck.json"),
                    1001,
                    out CharacterTestRuntimeAiPlannerLocalPlayerDeckService service,
                    out string error), error);
                resources = CreatePlayerPanelResourceServices(
                    CharacterTestFairyGuiBehaviorDeckPanelIds.PackageId,
                    CharacterTestFairyGuiBehaviorDeckPanelIds.PackageBytesKey,
                    CharacterTestFairyGuiBehaviorDeckPanelIds.Atlas0Key,
                    out behaviorDeckAtlas);
                slice = new GameSlice(
                    NullRuntimeLogger.Instance,
                    CharacterTestStoryFixture.CreateBootstrapContent(),
                    resources,
                    baseResourcePreloadGroupId: string.Empty);

                cameraObject.tag = "MainCamera";
                cameraObject.AddComponent<UnityEngine.Camera>();
                CharacterTestThirdPersonCameraRigBridge owner =
                    hostObject.AddComponent<CharacterTestThirdPersonCameraRigBridge>();
                runtime = new CharacterTestUiRuntime(owner, NullRuntimeLogger.Instance);
                runtime.Configure(new CharacterTestUiRuntimeOptions
                {
                    Slice = slice,
                    EnableDebugUi = false,
                    EnableFairyGuiCombatHud = false,
                    EnableFairyGuiFloatingCombatText = false,
                    EnableFairyGuiStoryUi = false,
                    EnableFairyGuiMainMenu = false,
                    EnableFairyGuiNetworkPanel = false,
                    EnableFairyGuiSettingsPanel = false,
                    EnableFairyGuiAbilityBindingPanel = false,
                    EnableFairyGuiRuntimeAiBehaviorLoadoutPanel = true,
                    PlayerBehaviorDeckService = service
                });

                Assert.IsNotNull(runtime.FairyGuiBehaviorDeckPanelHost);
                Assert.IsTrue(
                    runtime.FairyGuiBehaviorDeckPanelHost.IsConfigured,
                    runtime.LastFairyGuiRuntimeAiBehaviorLoadoutPanelStatus);
                Assert.IsTrue(runtime.OpenRuntimeAiBehaviorLoadoutPanel());
                Assert.IsTrue(
                    runtime.FairyGuiBehaviorDeckPanelHost.IsOpen,
                    runtime.LastFairyGuiRuntimeAiBehaviorLoadoutPanelStatus);
                Assert.AreSame(service.Workbench.View, runtime.FairyGuiBehaviorDeckPanelHost.CommandSink.View);
                Fgui.GComponent root = runtime.FairyGuiBehaviorDeckPanelHost.RootForTests;
                Assert.NotNull(root);
                Fgui.GButton preview = root.GetChild(
                    CharacterTestFairyGuiBehaviorDeckPanelIds.PreviewRecommendedPreset).asButton;
                Fgui.GButton confirm = root.GetChild(
                    CharacterTestFairyGuiBehaviorDeckPanelIds.ConfirmRecommendedPreset).asButton;
                Fgui.GButton cancel = root.GetChild(
                    CharacterTestFairyGuiBehaviorDeckPanelIds.CancelRecommendedPreset).asButton;
                Fgui.GButton undo = root.GetChild(
                    CharacterTestFairyGuiBehaviorDeckPanelIds.UndoRecommendedPreset).asButton;
                Assert.NotNull(root.GetChild(
                    CharacterTestFairyGuiBehaviorDeckPanelIds.PresetAddedRow(0))?.asCom);
                long sourceHash = service.Workbench.Deck.DeckHash;

                preview.onClick.Call();
                runtime.FairyGuiBehaviorDeckPanelHost.Refresh();
                Assert.IsTrue(runtime.FairyGuiBehaviorDeckPanelHost.LastViewModel.PresetPreviewVisible);
                Assert.IsTrue(root.GetChild(
                    CharacterTestFairyGuiBehaviorDeckPanelIds.PresetBackdrop).visible);
                Assert.IsTrue(root.GetChild(
                    CharacterTestFairyGuiBehaviorDeckPanelIds.PresetPanel).visible);
                Assert.IsTrue(confirm.enabled);
                Assert.IsFalse(root.GetChild(
                    CharacterTestFairyGuiBehaviorDeckPanelIds.Close).asButton.enabled);
                Assert.AreEqual(sourceHash, service.Workbench.Deck.DeckHash);

                cancel.onClick.Call();
                runtime.FairyGuiBehaviorDeckPanelHost.Refresh();
                Assert.IsFalse(runtime.FairyGuiBehaviorDeckPanelHost.LastViewModel.PresetPreviewVisible);
                Assert.AreEqual(sourceHash, service.Workbench.Deck.DeckHash);

                preview.onClick.Call();
                runtime.FairyGuiBehaviorDeckPanelHost.Refresh();
                confirm.onClick.Call();
                runtime.FairyGuiBehaviorDeckPanelHost.Refresh();
                Assert.AreEqual(3, service.Workbench.Deck.SlotCount);
                Assert.IsTrue(undo.visible);
                Assert.IsTrue(undo.enabled);

                undo.onClick.Call();
                runtime.FairyGuiBehaviorDeckPanelHost.Refresh();
                Assert.AreEqual(sourceHash, service.Workbench.Deck.DeckHash);
                Assert.IsFalse(undo.visible);

                preview.onClick.Call();
                runtime.FairyGuiBehaviorDeckPanelHost.Refresh();
                Assert.IsTrue(runtime.FairyGuiBehaviorDeckPanelHost.LastViewModel.PresetPreviewVisible);
                service.Workbench.LockSession();
                confirm.onClick.Call();
                runtime.FairyGuiBehaviorDeckPanelHost.Refresh();
                Assert.IsFalse(runtime.FairyGuiBehaviorDeckPanelHost.LastViewModel.PresetPreviewVisible);
                Assert.IsFalse(preview.enabled);
                Fgui.GButton close = root.GetChild(
                    CharacterTestFairyGuiBehaviorDeckPanelIds.Close).asButton;
                Assert.IsTrue(close.visible);
                Assert.IsTrue(close.enabled);
                Assert.IsTrue(close.focused);
                Assert.IsTrue(runtime.CloseRuntimeAiBehaviorLoadoutPanel());
                Assert.IsFalse(runtime.FairyGuiBehaviorDeckPanelHost.IsOpen);
            }
            finally
            {
                runtime?.Release();
                slice?.Dispose();
                resources?.Dispose();
                UnityEngine.Object.DestroyImmediate(hostObject);
                UnityEngine.Object.DestroyImmediate(cameraObject);
                DestroyObjectNamed("Stage Camera");
                if (behaviorDeckAtlas != null)
                    UnityEngine.Object.DestroyImmediate(behaviorDeckAtlas);
                RemoveCharacterTestLegacyPanelPackageIfLoaded();
                if (Directory.Exists(directory))
                    Directory.Delete(directory, true);
            }
        }

        [Test]
        public void UiRuntime_RuntimeAiLabPlayerLayer_IsExclusiveAndDoesNotReopenAfterReconfigure()
        {
            RemoveCharacterTestLegacyPanelPackageIfLoaded();
            string directory = Path.Combine(
                Path.GetTempPath(),
                "mx-runtime-ai-lab-player-layer-" + Guid.NewGuid().ToString("N"));
            GlobalResourceRuntimeServices resources = null;
            GameSlice slice = null;
            var cameraObject = new GameObject("Runtime AI Lab Player Layer Main Camera Test");
            var hostObject = new GameObject("Runtime AI Lab Player Layer UI Test");
            CharacterTestUiRuntime runtime = null;
            try
            {
                Assert.IsTrue(CharacterTestRuntimeAiPlannerLocalPlayerDeckService.TryCreate(
                    Path.Combine(directory, "deck.json"),
                    1001,
                    out CharacterTestRuntimeAiPlannerLocalPlayerDeckService service,
                    out string error), error);
                resources = CreateDefaultResourceServicesIfContains(
                    CharacterTestFairyGuiBehaviorDeckPanelIds.PackageBytesKey,
                    CharacterTestFairyGuiBehaviorDeckPanelIds.Atlas0Key,
                    CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.PackageBytesKey,
                    CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.Atlas0Key,
                    CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.RecoveryPackageBytesKey,
                    CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.RecoveryAtlas0Key);
                Assert.IsNotNull(resources, "Published Runtime AI Lab and behavior deck resources are required.");
                slice = new GameSlice(
                    NullRuntimeLogger.Instance,
                    CharacterTestStoryFixture.CreateBootstrapContent(),
                    resources,
                    baseResourcePreloadGroupId: string.Empty);

                cameraObject.tag = "MainCamera";
                cameraObject.AddComponent<UnityEngine.Camera>();
                CharacterTestThirdPersonCameraRigBridge owner =
                    hostObject.AddComponent<CharacterTestThirdPersonCameraRigBridge>();
                CharacterTestRuntimeAiPlannerDevSceneController controller =
                    hostObject.AddComponent<CharacterTestRuntimeAiPlannerDevSceneController>();
                controller.PrepareInitialSetupState();

                runtime = new CharacterTestUiRuntime(owner, NullRuntimeLogger.Instance);
                ConfigureRuntimeAiLabPlayerLayer(runtime, slice, service, controller);

                Assert.IsTrue(runtime.FairyGuiBehaviorDeckPanelHost.IsOpen);
                Assert.IsFalse(runtime.FairyGuiRuntimeAiPlannerDevPanelHost.IsOpen);
                Fgui.GComponent deckRoot = runtime.FairyGuiBehaviorDeckPanelHost.RootForTests;
                Assert.NotNull(deckRoot);
                Fgui.GButton labEdit =
                    deckRoot.GetChild("libraryCard0").asCom.GetChild("btnLabEdit").asButton;
                Assert.NotNull(labEdit);
                labEdit.onClick.Call();
                Assert.IsFalse(
                    runtime.FairyGuiBehaviorDeckPanelHost.IsOpen,
                    controller.ShellCoordinator?.LastNavigationResult.Code
                    + " | "
                    + controller.ShellCoordinator?.LastNavigationResult.Message
                    + " | "
                    + runtime.RuntimeAiLabAuthoringNavigationStatus);
                Assert.IsTrue(
                    runtime.FairyGuiRuntimeAiPlannerDevPanelHost.IsOpen,
                    runtime.FairyGuiRuntimeAiPlannerDevPanelHost.LastStatusMessage);
                Assert.AreEqual(
                    CharacterTestRuntimeAiLabWorkspaceMode.BehaviorCard,
                    runtime.FairyGuiRuntimeAiPlannerDevPanelHost.CommandSink.WorkspaceMode);

                runtime.FairyGuiRuntimeAiPlannerDevPanelHost.CommandSink.Enqueue(new MxUiCommand(
                    CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.ViewId,
                    CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.WorkspaceBack,
                    null));
                Assert.IsTrue(runtime.FairyGuiBehaviorDeckPanelHost.IsOpen);
                Assert.IsFalse(runtime.FairyGuiRuntimeAiPlannerDevPanelHost.IsOpen);

                Assert.IsTrue(runtime.ToggleRuntimeAiPlannerDevPanel());
                Assert.IsFalse(runtime.FairyGuiBehaviorDeckPanelHost.IsOpen);
                Assert.IsFalse(runtime.FairyGuiRuntimeAiPlannerDevPanelHost.IsOpen);

                runtime.Release();
                runtime = new CharacterTestUiRuntime(owner, NullRuntimeLogger.Instance);
                ConfigureRuntimeAiLabPlayerLayer(runtime, slice, service, controller);

                Assert.IsFalse(runtime.FairyGuiBehaviorDeckPanelHost.IsOpen);
                Assert.IsFalse(runtime.FairyGuiRuntimeAiPlannerDevPanelHost.IsOpen);

                Assert.IsTrue(runtime.ToggleRuntimeAiPlannerDevPanel());
                Assert.IsTrue(runtime.FairyGuiBehaviorDeckPanelHost.IsOpen);
                Assert.IsFalse(runtime.FairyGuiRuntimeAiPlannerDevPanelHost.IsOpen);
            }
            finally
            {
                runtime?.Release();
                slice?.Dispose();
                resources?.Dispose();
                UnityEngine.Object.DestroyImmediate(hostObject);
                UnityEngine.Object.DestroyImmediate(cameraObject);
                DestroyObjectNamed("Stage Camera");
                RemoveCharacterTestLegacyPanelPackageIfLoaded();
                if (Directory.Exists(directory))
                    Directory.Delete(directory, true);
            }
        }

        private static void ConfigureRuntimeAiLabPlayerLayer(
            CharacterTestUiRuntime runtime,
            GameSlice slice,
            CharacterTestRuntimeAiPlannerLocalPlayerDeckService service,
            CharacterTestRuntimeAiPlannerDevSceneController controller)
        {
            runtime.Configure(new CharacterTestUiRuntimeOptions
            {
                Slice = slice,
                EnableDebugUi = false,
                EnableFairyGuiCombatHud = false,
                EnableFairyGuiFloatingCombatText = false,
                EnableFairyGuiStoryUi = false,
                EnableFairyGuiMainMenu = false,
                EnableFairyGuiNetworkPanel = false,
                EnableFairyGuiSettingsPanel = false,
                EnableFairyGuiAbilityBindingPanel = false,
                EnableFairyGuiRuntimeAiBehaviorLoadoutPanel = true,
                PlayerBehaviorDeckService = service,
                EnableFairyGuiRuntimeAiPlannerDevPanel = true,
                AutoOpenFairyGuiRuntimeAiPlannerDevPanel = true,
                RuntimeAiPlannerDevSceneController = controller
            });
        }

        private static void AssertBehaviorDeckFocusPath(Fgui.GComponent root, string expectedPath)
        {
            FieldInfo metadataField = typeof(MxFairyGuiFocusNavigation).GetField(
                "MetadataByRoot",
                BindingFlags.NonPublic | BindingFlags.Static);
            Assert.NotNull(metadataField);
            var metadataByRoot = metadataField.GetValue(null) as System.Collections.IDictionary;
            Assert.NotNull(metadataByRoot);
            var metadata = metadataByRoot[root] as MxFairyGuiFocusNavigationMetadata;
            Assert.NotNull(metadata);
            CollectionAssert.Contains(metadata.OrderedChildNames, expectedPath);
        }

[Test]
        public void FairyGuiAbilityBindingPanelHost_WithPublishedPackage_LoadsBindsAndReleases()
        {
            RemoveCharacterTestLegacyPanelPackageIfLoaded();
            Texture2D combatHudAtlas = null;
            GlobalResourceRuntimeServices resources = null;
            GameSlice slice = null;
            var cameraObject = new GameObject("CharacterTest Ability Binding Panel Main Camera Test");
            var hostObject = new GameObject("CharacterTest Ability Binding Panel Host Test");
            try
            {
                resources = CreateAbilityBindingPanelResourceServices(out combatHudAtlas);
                slice = new GameSlice(
                    NullRuntimeLogger.Instance,
                    CharacterTestStoryFixture.CreateBootstrapContent(),
                    resources,
                    baseResourcePreloadGroupId: string.Empty);
                var hotbar = new CharacterTestAbilityHotbarRuntime(
                    CharacterTestAbilityCatalogSource.CreateRegistry());

                cameraObject.tag = "MainCamera";
                cameraObject.AddComponent<UnityEngine.Camera>();
                CharacterTestFairyGuiAbilityBindingPanelHost host =
                    hostObject.AddComponent<CharacterTestFairyGuiAbilityBindingPanelHost>();

                host.Configure(slice, hotbar, NullRuntimeLogger.Instance);

                Assert.IsTrue(host.IsConfigured, host.LastStatusMessage);
                Assert.IsFalse(host.IsOpen, host.LastStatusMessage);
                Assert.AreEqual(0, host.SuccessfulRefreshCount);

                Assert.IsTrue(host.Open(), host.LastStatusMessage);
                Assert.IsTrue(host.IsOpen, host.LastStatusMessage);
                Assert.AreEqual(1, host.SuccessfulRefreshCount);
                Assert.NotNull(host.LastViewModel);
                Assert.AreEqual("Abilities", host.LastViewModel.Title);
                AssertPublishedAbilityBindingPanelControlTypes();

                host.Refresh();

                Assert.AreEqual(1, host.SuccessfulRefreshCount);
                Assert.AreEqual(1, host.SkippedRefreshCount);

                Assert.IsTrue(host.Close());
                Assert.IsFalse(host.IsOpen);
                Assert.DoesNotThrow(() => host.Release());
                Assert.DoesNotThrow(() => host.Release());
            }
            finally
            {
                CharacterTestFairyGuiAbilityBindingPanelHost host =
                    hostObject.GetComponent<CharacterTestFairyGuiAbilityBindingPanelHost>();
                host?.Release();
                slice?.Dispose();
                resources?.Dispose();
                UnityEngine.Object.DestroyImmediate(hostObject);
                UnityEngine.Object.DestroyImmediate(cameraObject);
                DestroyObjectNamed("Stage Camera");
                if (combatHudAtlas != null)
                    UnityEngine.Object.DestroyImmediate(combatHudAtlas);
                RemoveCharacterTestLegacyPanelPackageIfLoaded();
            }
        }

[Test]
        public void FairyGuiAbilityBindingPanelBinder_BackfillsPublishedPackageOverflowAbilityChoices()
        {
            RemoveCharacterTestLegacyPanelPackageIfLoaded();
            Texture2D combatHudAtlas = null;
            GlobalResourceRuntimeServices resources = null;
            MxFairyGuiPackageLoadScope packageScope = null;
            IMxFairyGuiComponentHandle handle = null;
            var host = new MxFairyGuiHost();
            bool packageEnsured = false;
            try
            {
                resources = CreateAbilityBindingPanelResourceServices(out combatHudAtlas);
                var bridge = new MxFairyGuiResourceBridge(resources.ResourceManager);
                var package = new MxFairyGuiPackageDescriptor(
                    CharacterTestFairyGuiAbilityBindingPanelIds.PackageId,
                    CharacterTestFairyGuiAbilityBindingPanelIds.PackageBytesKey,
                    new[]
                    {
                        new MxFairyGuiPackageResourceDescriptor(
                            CharacterTestFairyGuiAbilityBindingPanelIds.Atlas0Key,
                            MxFairyGuiPackageResourceKind.Atlas)
                    });
                MxFairyGuiPackageLoadResult load = bridge.LoadPackage(package);
                Assert.IsTrue(load.Success, load.Failure.Message);
                packageScope = load.Scope;

                string failure;
                Assert.IsTrue(host.EnsurePackage(packageScope, out failure), failure);
                packageEnsured = true;
                Assert.IsTrue(host.TryCreateComponent(
                    CharacterTestFairyGuiAbilityBindingPanelIds.PackageId,
                    CharacterTestFairyGuiAbilityBindingPanelIds.ComponentName,
                    out handle,
                    out failure), failure);

                var concrete = handle as MxFairyGuiComponentHandle;
                Assert.NotNull(concrete);
                Fgui.GComponent root = concrete.Component;

                Fgui.GObject existingPoisonButton =
                    root.GetChild(CharacterTestFairyGuiAbilityBindingPanelIds.AbilityButton(14));
                if (existingPoisonButton != null)
                    root.RemoveChild(existingPoisonButton, true);

                Fgui.GButton template =
                    root.GetChild(CharacterTestFairyGuiAbilityBindingPanelIds.AbilityButton(13))?.asButton;
                Assert.NotNull(template);
                Assert.IsFalse(string.IsNullOrWhiteSpace(template.resourceURL));

                var sink = new RecordingUiCommandSink();
                var hotbar = new CharacterTestAbilityHotbarRuntime(
                    CharacterTestAbilityCatalogSource.CreateRegistry());
                CharacterTestFairyGuiAbilityBindingPanelViewModel viewModel =
                    CharacterTestFairyGuiAbilityBindingPanelViewModelBuilder.Build(
                        hotbar,
                        hotbar.CreateBindingSnapshot(),
                        selectedSlot: 1,
                        dirty: false);
                var binder = new CharacterTestFairyGuiAbilityBindingPanelBinder(
                    sink,
                    CharacterTestFairyGuiAbilityBindingPanelIds.ViewId);

                binder.Bind(handle, viewModel);

                Assert.IsFalse(AbilityPanelButton(
                    root,
                    CharacterTestFairyGuiAbilityBindingPanelIds.AbilityButton(12)).visible);
                Assert.IsTrue(binder.ScrollAbilityChoicesForTest(99));
                Assert.IsFalse(AbilityPanelButton(
                    root,
                    CharacterTestFairyGuiAbilityBindingPanelIds.AbilityButton(12)).visible);
                Assert.IsNotNull(root.GetChild(CharacterTestFairyGuiAbilityBindingPanelIds.AbilityButton(14)));
                Assert.IsNotNull(root.GetChild(CharacterTestFairyGuiAbilityBindingPanelIds.AbilityButton(17)));

                Fgui.GButton poisonButton = AbilityPanelButton(
                    root,
                    CharacterTestFairyGuiAbilityBindingPanelIds.AbilityButton(1));
                Assert.NotNull(poisonButton);
                Assert.IsTrue(poisonButton.visible);
                StringAssert.Contains("Poison Bottle", poisonButton.text);
                Fgui.GButton meteorButton = AbilityPanelButton(
                    root,
                    CharacterTestFairyGuiAbilityBindingPanelIds.AbilityButton(3));
                Assert.NotNull(meteorButton);
                Assert.IsTrue(meteorButton.visible);
                StringAssert.Contains("Meteor Barrage", meteorButton.text);
                Fgui.GButton runeTurretButton = AbilityPanelButton(
                    root,
                    CharacterTestFairyGuiAbilityBindingPanelIds.AbilityButton(8));
                Assert.NotNull(runeTurretButton);
                Assert.IsTrue(runeTurretButton.visible);
                StringAssert.Contains("Rune Turret", runeTurretButton.text);

                runeTurretButton.onClick.Call();

                Assert.AreEqual(1, sink.Count);
                Assert.AreEqual(CharacterTestFairyGuiAbilityBindingPanelCommandIds.SelectAbility, sink.Last.CommandId);
                var payload = (CharacterTestFairyGuiAbilityBindingPanelPayload)sink.Last.Payload;
                Assert.AreEqual(CharacterTestAbilityIds.RuneTurretAbilityId, payload.AbilityId);
            }
            finally
            {
                handle?.Dispose();
                if (packageEnsured)
                    host.ReleasePackage(CharacterTestFairyGuiAbilityBindingPanelIds.PackageId);
                else
                    packageScope?.Release();
                resources?.Dispose();
                if (combatHudAtlas != null)
                    UnityEngine.Object.DestroyImmediate(combatHudAtlas);
                RemoveCharacterTestLegacyPanelPackageIfLoaded();
            }
        }

[Test]
        public void FairyGuiNetworkPanelHost_CanBeConfiguredForOnDemandOpen()
        {
            RemoveCharacterTestLegacyPanelPackageIfLoaded();
            Texture2D combatHudAtlas = null;
            GlobalResourceRuntimeServices resources = null;
            GameSlice slice = null;
            var cameraObject = new GameObject("CharacterTest Network Panel On Demand Main Camera Test");
            var hostObject = new GameObject("CharacterTest Network Panel On Demand Host Test");
            try
            {
                resources = CreateNetworkPanelResourceServices(out combatHudAtlas);
                slice = new GameSlice(
                    NullRuntimeLogger.Instance,
                    CharacterTestStoryFixture.CreateBootstrapContent(),
                    resources,
                    baseResourcePreloadGroupId: string.Empty);
                var controller = new RecordingNetworkSessionController();

                cameraObject.tag = "MainCamera";
                cameraObject.AddComponent<UnityEngine.Camera>();
                CharacterTestFairyGuiNetworkPanelHost host =
                    hostObject.AddComponent<CharacterTestFairyGuiNetworkPanelHost>();

                host.Configure(
                    slice,
                    controller,
                    logger: NullRuntimeLogger.Instance,
                    layerHost: null,
                    autoOpen: false);

                Assert.IsTrue(host.IsConfigured, host.LastStatusMessage);
                Assert.IsFalse(host.IsOpen);
                host.Refresh();
                Assert.IsFalse(host.IsOpen);
                Assert.AreEqual(0, host.SuccessfulRefreshCount);

                Assert.IsTrue(host.Open(), host.LastStatusMessage);
                Assert.IsTrue(host.IsOpen);
                Assert.AreEqual(1, host.SuccessfulRefreshCount);
            }
            finally
            {
                CharacterTestFairyGuiNetworkPanelHost host =
                    hostObject.GetComponent<CharacterTestFairyGuiNetworkPanelHost>();
                host?.Release();
                slice?.Dispose();
                resources?.Dispose();
                UnityEngine.Object.DestroyImmediate(hostObject);
                UnityEngine.Object.DestroyImmediate(cameraObject);
                DestroyObjectNamed("Stage Camera");
                if (combatHudAtlas != null)
                    UnityEngine.Object.DestroyImmediate(combatHudAtlas);
                RemoveCharacterTestLegacyPanelPackageIfLoaded();
            }
        }

[Test]
        public void FairyGuiMainMenuHost_WithPublishedPackage_LoadsBindsAndReleases()
        {
            RemoveCharacterTestMainMenuPackageIfLoaded();
            GlobalResourceRuntimeServices resources = null;
            GameSlice slice = null;
            var cameraObject = new GameObject("CharacterTest Main Menu Main Camera Test");
            var hostObject = new GameObject("CharacterTest Main Menu Host Test");
            try
            {
                resources = GlobalResourceRuntimeServices.Create(new GlobalResourceRuntimeOptions
                {
                    AllowMissingPreloadGroupInEditor = true
                });
                Assert.IsFalse(resources.HasBootstrapError, resources.BootstrapErrorMessage);
                Assert.IsTrue(resources.ResourceManager.Contains(CharacterTestFairyGuiMainMenuIds.PackageBytesKey));
                Assert.IsTrue(resources.ResourceManager.Contains(CharacterTestFairyGuiMainMenuIds.Atlas0Key));
                slice = new GameSlice(
                    NullRuntimeLogger.Instance,
                    CharacterTestStoryFixture.CreateBootstrapContent(),
                    resources,
                    baseResourcePreloadGroupId: string.Empty);
                Assert.IsTrue(slice.Resources.ResourceManager.Contains(
                    CharacterTestFairyGuiMainMenuIds.PackageBytesKey),
                    DescribeResourceEntries(slice.Resources.ResourceManager, "charactertest.player_ui"));
                Assert.IsTrue(slice.Resources.ResourceManager.Contains(
                    CharacterTestFairyGuiMainMenuIds.Atlas0Key),
                    DescribeResourceEntries(slice.Resources.ResourceManager, "charactertest.player_ui"));
                var controller = new RecordingMainMenuController();
                controller.Status = new CharacterTestMainMenuStatus
                {
                    IsPaused = true,
                    NetworkSessionKind = CharacterTestNetworkSessionKind.Offline,
                    Message = "Offline"
                };

                cameraObject.tag = "MainCamera";
                cameraObject.AddComponent<UnityEngine.Camera>();
                CharacterTestFairyGuiMainMenuHost host =
                    hostObject.AddComponent<CharacterTestFairyGuiMainMenuHost>();

                host.Configure(slice, controller, NullRuntimeLogger.Instance, autoOpen: false);

                Assert.IsTrue(host.IsConfigured, host.LastStatusMessage);
                Assert.IsFalse(host.IsOpen, host.LastStatusMessage);
                Assert.AreEqual(CharacterTestFairyGuiMainMenuIds.ComponentName, "MainMenuPanel");
                AssertMainMenuPackageCanCreateImageBackedComponentAfterReload(slice.Resources.ResourceManager);

                Assert.DoesNotThrow(() => host.Release());
                Assert.DoesNotThrow(() => host.Release());
            }
            finally
            {
                CharacterTestFairyGuiMainMenuHost host =
                    hostObject.GetComponent<CharacterTestFairyGuiMainMenuHost>();
                host?.Release();
                slice?.Dispose();
                resources?.Dispose();
                UnityEngine.Object.DestroyImmediate(hostObject);
                UnityEngine.Object.DestroyImmediate(cameraObject);
                DestroyObjectNamed("Stage Camera");
                RemoveCharacterTestMainMenuPackageIfLoaded();
            }
        }

[Test]
        public void FairyGuiCombatHudHost_WithPublishedPackage_LoadsBindsAndReleases()
        {
            RemoveCharacterTestCombatHudPackageIfLoaded();
            Texture2D combatHudAtlas = null;
            GlobalResourceRuntimeServices resources = null;
            GameSlice slice = null;
            var cameraObject = new GameObject("CharacterTest Combat HUD Main Camera Test");
            var hostObject = new GameObject("CharacterTest Combat HUD Host Test");
            try
            {
                resources = CreateCombatHudResourceServices(out combatHudAtlas);
                slice = new GameSlice(
                    NullRuntimeLogger.Instance,
                    CharacterTestStoryFixture.CreateBootstrapContent(),
                    resources,
                    baseResourcePreloadGroupId: string.Empty);
                CharacterTestCharacterEntity localPlayer = SpawnLocalPlayer(slice);

                cameraObject.tag = "MainCamera";
                cameraObject.AddComponent<UnityEngine.Camera>();
                CharacterTestFairyGuiCombatHudHost host =
                    hostObject.AddComponent<CharacterTestFairyGuiCombatHudHost>();

                host.Configure(slice, NullRuntimeLogger.Instance);

                Assert.IsTrue(host.IsConfigured, host.LastStatusMessage);
                Assert.IsTrue(host.IsOpen, host.LastStatusMessage);
                Assert.AreEqual(1, host.SuccessfulRefreshCount);
                Assert.NotNull(host.LastViewModel);
                Assert.AreEqual(localPlayer.Id.Value, host.LastViewModel.PlayerEntityId);
                Assert.IsTrue(host.LastViewModel.HasLocalPlayer);

                host.Refresh();

                Assert.AreEqual(1, host.SuccessfulRefreshCount);
                Assert.AreEqual(1, host.SkippedRefreshCount);

                Assert.IsTrue(host.Close());
                Assert.IsFalse(host.IsOpen);

                Assert.DoesNotThrow(() => host.Release());
                Assert.DoesNotThrow(() => host.Release());
            }
            finally
            {
                CharacterTestFairyGuiCombatHudHost host =
                    hostObject.GetComponent<CharacterTestFairyGuiCombatHudHost>();
                host?.Release();
                slice?.Dispose();
                resources?.Dispose();
                UnityEngine.Object.DestroyImmediate(hostObject);
                UnityEngine.Object.DestroyImmediate(cameraObject);
                DestroyObjectNamed("Stage Camera");
                if (combatHudAtlas != null)
                    UnityEngine.Object.DestroyImmediate(combatHudAtlas);
                RemoveCharacterTestCombatHudPackageIfLoaded();
            }
        }

[Test]
        public void NetworkPanelHost_BackCallback_DefersCloseToRuntime()
        {
            var hostObject = new GameObject("CharacterTest Network Panel Back Callback Test");
            try
            {
                CharacterTestFairyGuiNetworkPanelHost host =
                    hostObject.AddComponent<CharacterTestFairyGuiNetworkPanelHost>();
                bool callbackCalled = false;
                SetPrivateField(host, "_openRequested", true);
                SetPrivateField(host, "_closeRequested", new Action(() => callbackCalled = true));

                InvokePrivate(host, "HandleCloseRequested");

                Assert.IsTrue(callbackCalled);
                Assert.IsTrue(
                    GetPrivateField<bool>(host, "_openRequested"),
                    "Back must let CharacterTestUiRuntime open MainMenu before NetworkPanel closes.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(hostObject);
            }
        }
}
}
