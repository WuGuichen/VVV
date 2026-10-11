using System;
using System.Collections.Generic;
using MxFramework.Combat.Damage;
using MxFramework.Combat.Hit;
using MxFramework.Core.Math;
using MxFramework.Game.CharacterTest;
using MxFramework.Gameplay;
using MxFramework.Runtime;
using MxFramework.UI;
using NUnit.Framework;
using UnityEngine;
using Fgui = global::FairyGUI;

namespace MxFramework.Tests.Game.CharacterTest
{
    public sealed class CharacterTestFloatingCombatTextTests
    {
        [SetUp]
        public void SetUp()
        {
            CharacterTestFairyGuiTestStageReset.Reset();
        }

        [TearDown]
        public void TearDown()
        {
            CharacterTestFairyGuiLayerRoots.ReleaseSharedLayerRoots();
            CharacterTestFairyGuiTestStageReset.Reset();
            DestroyObjectNamed("CharacterTest.HeadPadLateUpdate");
        }

        [Test]
        public void Builder_DamageTelemetryCreatesDamageText()
        {
            DamageTelemetryRow row = CreateTelemetryRow(
                resultKind: HitResolveKind.Damage,
                finalDamage: 17,
                hitPointWorld: Point(1, 2, 3));

            Assert.IsTrue(CharacterTestFloatingCombatTextEventBuilder.TryCreateFromTelemetry(
                row,
                Array.Empty<CharacterTestCharacterEntity>(),
                out CharacterTestFloatingCombatTextEvent evt));

            Assert.AreEqual(CharacterTestFloatingCombatTextKind.Damage, evt.Kind);
            Assert.AreEqual("-17", evt.Text);
            Assert.AreEqual(1, evt.SourceEntityId);
            Assert.AreEqual(2, evt.TargetEntityId);
            Assert.AreEqual(80, evt.Priority);
            Assert.AreEqual(1f, evt.WorldPosition.x, 0.001f);
            Assert.AreEqual(2f, evt.WorldPosition.y, 0.001f);
            Assert.AreEqual(3f, evt.WorldPosition.z, 0.001f);
        }

        [Test]
        public void Builder_BlockedAndZeroDamageCreateNonDamageFeedback()
        {
            Assert.IsTrue(CharacterTestFloatingCombatTextEventBuilder.TryCreateFromTelemetry(
                CreateTelemetryRow(HitResolveKind.Blocked, finalDamage: 0, hitPointWorld: Point(0, 1, 3)),
                Array.Empty<CharacterTestCharacterEntity>(),
                out CharacterTestFloatingCombatTextEvent blocked));
            Assert.AreEqual(CharacterTestFloatingCombatTextKind.Blocked, blocked.Kind);
            Assert.AreEqual("BLOCK", blocked.Text);

            Assert.IsTrue(CharacterTestFloatingCombatTextEventBuilder.TryCreateFromTelemetry(
                CreateTelemetryRow(HitResolveKind.Damage, finalDamage: 0, hitPointWorld: Point(0, 1, 3)),
                Array.Empty<CharacterTestCharacterEntity>(),
                out CharacterTestFloatingCombatTextEvent zeroDamage));
            Assert.AreEqual(CharacterTestFloatingCombatTextKind.Invalid, zeroDamage.Kind);
            Assert.AreEqual("0", zeroDamage.Text);
        }

        [Test]
        public void Builder_ScatterImpactAndCohesionBreakUseTargetFallbackAnchor()
        {
            var gameplayId = new GameplayEntityId(7, 1);
            CharacterTestCharacterEntity target = CreateEntity(2, gameplayId, 3f, 0.25f, -4f);
            CharacterTestCharacterEntity[] entities = { target };

            var scatter = new ScatterImpactResolvedEvent(
                new RuntimeFrame(8),
                gameplayId,
                scatterImpact: 12,
                debtBefore: 3,
                debtAfter: 15,
                currentCohesionLimit: 30,
                PressureBand.Stable,
                PressureBand.Pressed,
                sourceId: 1,
                reason: new GameplayScatterPressureReason(1201303));
            Assert.IsTrue(CharacterTestFloatingCombatTextEventBuilder.TryCreateFromScatterImpact(
                scatter,
                entities,
                out CharacterTestFloatingCombatTextEvent scatterText));
            Assert.AreEqual(CharacterTestFloatingCombatTextKind.ScatterImpact, scatterText.Kind);
            Assert.AreEqual("SCATTER +12", scatterText.Text);
            Assert.AreEqual(3f, scatterText.WorldPosition.x, 0.001f);
            Assert.AreEqual(2.05f, scatterText.WorldPosition.y, 0.001f);
            Assert.AreEqual(-4f, scatterText.WorldPosition.z, 0.001f);

            var cohesionBreak = new CohesionBreakEvent(
                new RuntimeFrame(9),
                gameplayId,
                PressureBand.Critical,
                previousValue: 28,
                currentScatterPressure: 35,
                maxScatterPressure: 40,
                delta: 7,
                sourceId: 1,
                reason: new GameplayScatterPressureReason(1201303));
            Assert.IsTrue(CharacterTestFloatingCombatTextEventBuilder.TryCreateFromCohesionBreak(
                cohesionBreak,
                entities,
                out CharacterTestFloatingCombatTextEvent breakText));
            Assert.AreEqual(CharacterTestFloatingCombatTextKind.CohesionBreak, breakText.Kind);
            Assert.AreEqual("破聚!", breakText.Text);
            Assert.AreEqual(140, breakText.Priority);
            Assert.Greater(breakText.Priority, scatterText.Priority);
            Assert.AreEqual(30, breakText.Style.FontSize);
            Assert.AreEqual(1.25f, breakText.Style.LifetimeSeconds, 0.001f);
            Assert.AreEqual(72f, breakText.Style.RisePixels, 0.001f);
            Assert.AreEqual(1.35f, breakText.Style.StartScale, 0.001f);
            Assert.AreEqual(1.05f, breakText.Style.EndScale, 0.001f);
        }

        [Test]
        public void Pool_PrewarmsReusesObjectsAndRespectsActiveCap()
        {
            var root = new Fgui.GComponent();
            var pool = new CharacterTestFloatingCombatTextPool(root, prewarmCount: 2, maxActiveCount: 3);
            try
            {
                Assert.AreEqual(2, pool.CreatedCount);
                Assert.AreEqual(2, pool.InactiveCount);

                Assert.IsTrue(pool.Spawn(CreateEvent(priority: 10, text: "10")));
                Assert.IsTrue(pool.Spawn(CreateEvent(priority: 20, text: "20")));
                Assert.IsTrue(pool.Spawn(CreateEvent(priority: 30, text: "30")));
                Assert.AreEqual(3, pool.ActiveCount);
                Assert.AreEqual(3, pool.CreatedCount);

                Assert.IsFalse(pool.Spawn(CreateEvent(priority: 5, text: "5")));
                Assert.AreEqual(3, pool.ActiveCount);
                Assert.AreEqual(3, pool.CreatedCount);
                Assert.AreEqual(1, pool.DroppedSpawnCount);

                Assert.IsTrue(pool.Spawn(CreateEvent(priority: 100, text: "100")));
                Assert.AreEqual(3, pool.ActiveCount);
                Assert.AreEqual(3, pool.CreatedCount);
                Assert.AreEqual(1, pool.RecycledActiveCount);
            }
            finally
            {
                pool.Dispose();
                pool.Dispose();
                root.Dispose();
            }
        }

        [Test]
        public void Pool_ClearAndDisposeSkipDestroyedNativeDisplayObjects()
        {
            var root = new Fgui.GComponent();
            var pool = new CharacterTestFloatingCombatTextPool(root, prewarmCount: 0, maxActiveCount: 2);
            try
            {
                Assert.IsTrue(pool.Spawn(CreateEvent(priority: 10, text: "10")));
                DestroyNativeDisplayObjects(root);

                Assert.DoesNotThrow(() => pool.Clear());
                Assert.DoesNotThrow(() => pool.Dispose());
            }
            finally
            {
                pool.Dispose();
                root.Dispose();
            }
        }

        [Test]
        public void Projector_MissingCameraOrBehindCameraSkipsSafely()
        {
            Assert.IsFalse(CharacterTestFloatingCombatTextProjector.TryProject(
                null,
                new Vector3(0f, 1f, 4f),
                out _));

            var cameraObject = new GameObject("CharacterTest Floating Text Projector Camera");
            try
            {
                UnityEngine.Camera camera = cameraObject.AddComponent<UnityEngine.Camera>();
                Assert.IsFalse(CharacterTestFloatingCombatTextProjector.TryProject(
                    camera,
                    new Vector3(0f, 1f, -4f),
                    out _));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(cameraObject);
            }
        }

        [Test]
        public void MainCameraCache_ReusesValidCameraAndRefreshesWhenDisabled()
        {
            UnityEngine.Camera[] existingCameras =
                UnityEngine.Object.FindObjectsByType<UnityEngine.Camera>(
                    FindObjectsInactive.Include);
            var existingCameraEnabledStates = new bool[existingCameras.Length];
            var firstObject = new GameObject("CharacterTest Main Camera Cache First");
            var secondObject = new GameObject("CharacterTest Main Camera Cache Second");
            try
            {
                for (int i = 0; i < existingCameras.Length; i++)
                {
                    UnityEngine.Camera existingCamera = existingCameras[i];
                    existingCameraEnabledStates[i] = existingCamera != null && existingCamera.enabled;
                    if (existingCamera != null && existingCamera.CompareTag("MainCamera"))
                        existingCamera.enabled = false;
                }

                firstObject.tag = "MainCamera";
                UnityEngine.Camera first = firstObject.AddComponent<UnityEngine.Camera>();
                UnityEngine.Camera cached = first;

                Assert.AreSame(first, CharacterTestMainCameraCache.Resolve(ref cached));
                Assert.AreSame(first, cached);

                secondObject.tag = "MainCamera";
                UnityEngine.Camera second = secondObject.AddComponent<UnityEngine.Camera>();
                first.enabled = false;
                cached = first;

                Assert.AreSame(second, CharacterTestMainCameraCache.Resolve(ref cached));
                Assert.AreSame(second, cached);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(secondObject);
                UnityEngine.Object.DestroyImmediate(firstObject);
                for (int i = 0; i < existingCameras.Length; i++)
                {
                    if (existingCameras[i] != null)
                        existingCameras[i].enabled = existingCameraEnabledStates[i];
                }
            }
        }

        [Test]
        public void HeadPad_Create_ParentsRootUnderHudLayer()
        {
            CharacterTestFairyGuiHeadPad pad = null;
            try
            {
                pad = CharacterTestFairyGuiHeadPad.Create();

                Fgui.GComponent hudRoot = GetLayerRoot(MxUiLayer.Hud);
                Fgui.GObject headPadRoot = FindChildByName(hudRoot, "CharacterTest.HeadPad");

                Assert.IsNotNull(headPadRoot);
                Assert.AreSame(hudRoot, headPadRoot.parent);
                Assert.AreEqual(CharacterTestFairyGuiLayerRoots.HudRootName, headPadRoot.parent.name);
            }
            finally
            {
                pad?.Dispose();
            }
        }

        [Test]
        public void HeadPad_Create_ReusesDisposedPadFromPool()
        {
            CharacterTestFairyGuiHeadPad first = null;
            CharacterTestFairyGuiHeadPad second = null;
            try
            {
                first = CharacterTestFairyGuiHeadPad.Create();
                Fgui.GComponent hudRoot = GetLayerRoot(MxUiLayer.Hud);
                Fgui.GObject firstRoot = FindChildByName(hudRoot, "CharacterTest.HeadPad");
                Assert.IsNotNull(firstRoot);

                first.Dispose();
                first = null;
                Assert.IsNull(firstRoot.parent);

                second = CharacterTestFairyGuiHeadPad.Create();
                Fgui.GObject secondRoot = FindChildByName(hudRoot, "CharacterTest.HeadPad");
                Assert.AreSame(firstRoot, secondRoot);
                Assert.AreSame(hudRoot, secondRoot.parent);
            }
            finally
            {
                second?.Dispose();
                first?.Dispose();
            }
        }

        [Test]
        public void HeadPad_CohesionBreakShowsTemporaryFlashAndLabel()
        {
            using CharacterTestFairyGuiHeadPad pad =
                CharacterTestFairyGuiHeadPad.CreateDetachedForTests(out Fgui.GComponent root);

            SyncHeadPad(
                pad,
                CreateCohesionBreakCombatState(),
                CharacterTestHeadPadDisplayMode.CombatReadout);

            AssertBreakEffectVisible(root);

            pad.TickVisibilityPresentation(0.7f, null);

            AssertBreakEffectHidden(root);
            Assert.Greater(root.alpha, 0f);
        }

        [Test]
        public void HeadPad_DeathFeedbackExpiresWhileRuntimePresentationIsPaused()
        {
            CharacterTestCharacterCombatStateComponent alive =
                CharacterTestCharacterCombatStateComponent.CreateDefault();
            CharacterTestCharacterCombatStateComponent dead = alive.ApplyHit(
                CharacterTestCharacterCombatStateComponent.DefaultMaxHealth,
                staggerFrames: 0,
                frame: 10L,
                sourceEntityId: 2,
                traceId: 3);

            using CharacterTestFairyGuiHeadPad unselectedPad =
                CharacterTestFairyGuiHeadPad.CreateDetachedForTests(out _);
            SyncHeadPad(
                unselectedPad,
                alive,
                CharacterTestHeadPadDisplayMode.CombatReadout,
                lockedVisible: true);
            SyncHeadPad(
                unselectedPad,
                dead,
                CharacterTestHeadPadDisplayMode.CombatReadout,
                lockedVisible: false);

            Assert.AreEqual(
                CharacterTestFairyGuiHeadPad.DeathFeedbackDurationSeconds,
                unselectedPad.DeathFeedbackRemainingForTests,
                0.001f);
            Assert.AreEqual(1f, unselectedPad.TargetDisplayAlphaForTests, 0.001f);

            unselectedPad.TickVisibilityPresentation(
                CharacterTestFairyGuiHeadPad.DeathFeedbackDurationSeconds + 0.01f,
                null);

            Assert.IsTrue(unselectedPad.DeadObserverSimplifiedForTests);
            Assert.AreEqual(0f, unselectedPad.TargetDisplayAlphaForTests, 0.001f);
        }

        [Test]
        public void HeadPad_DamageFeedbackSettlesWithoutRuntimePresentationTick()
        {
            CharacterTestCharacterCombatStateComponent alive =
                CharacterTestCharacterCombatStateComponent.CreateDefault();
            CharacterTestCharacterCombatStateComponent damaged = alive.ApplyHit(
                damage: 53,
                staggerFrames: 0,
                frame: 10L,
                sourceEntityId: 2,
                traceId: 3);

            using CharacterTestFairyGuiHeadPad pad =
                CharacterTestFairyGuiHeadPad.CreateDetachedForTests(out _);
            SyncHeadPad(
                pad,
                alive,
                CharacterTestHeadPadDisplayMode.CombatReadout);
            SyncHeadPad(
                pad,
                damaged,
                CharacterTestHeadPadDisplayMode.CombatReadout);

            Assert.Greater(pad.HealthFlashRemainingForTests, 0f);
            Assert.Greater(pad.FramePulseRemainingForTests, 0f);
            Assert.Greater(
                pad.HealthTrailWidthForTests,
                pad.TargetHealthWidthForTests);

            pad.TickVisibilityPresentation(1f, null);

            Assert.AreEqual(0f, pad.HealthFlashRemainingForTests, 0.001f);
            Assert.AreEqual(0f, pad.FramePulseRemainingForTests, 0.001f);
            Assert.AreEqual(
                pad.TargetHealthWidthForTests,
                pad.HealthTrailWidthForTests,
                0.001f);
        }

        [Test]
        public void HeadPad_AllyHealthColorStaysFriendlyGreenAtEveryAliveHealthLevel()
        {
            CharacterTestCharacterCombatStateComponent fullHealth =
                CharacterTestCharacterCombatStateComponent.CreateDefault();
            CharacterTestCharacterCombatStateComponent health47 = fullHealth.ApplyHit(
                damage: 53,
                staggerFrames: 0,
                frame: 10L,
                sourceEntityId: 2,
                traceId: 3);
            CharacterTestCharacterCombatStateComponent health25 = fullHealth.ApplyHit(
                damage: 75,
                staggerFrames: 0,
                frame: 11L,
                sourceEntityId: 2,
                traceId: 4);

            using CharacterTestFairyGuiHeadPad pad =
                CharacterTestFairyGuiHeadPad.CreateDetachedForTests(out _);
            SyncHeadPad(
                pad,
                fullHealth,
                CharacterTestHeadPadDisplayMode.CombatReadout,
                allegiance: CharacterTestHeadPadAllegiance.Ally);
            Color fullHealthColor = pad.BaseHealthFillColorForTests;

            SyncHeadPad(
                pad,
                health47,
                CharacterTestHeadPadDisplayMode.CombatReadout,
                allegiance: CharacterTestHeadPadAllegiance.Ally);
            Assert.AreEqual(fullHealthColor, pad.BaseHealthFillColorForTests);
            Assert.AreEqual(new Color(0.38f, 0.68f, 0.47f, 1f), fullHealthColor);

            SyncHeadPad(
                pad,
                health25,
                CharacterTestHeadPadDisplayMode.CombatReadout,
                allegiance: CharacterTestHeadPadAllegiance.Ally);
            Assert.AreEqual(fullHealthColor, pad.BaseHealthFillColorForTests);
        }

        [Test]
        public void HeadPad_CohesionBreakFlashRespectsDisplayMode()
        {
            using CharacterTestFairyGuiHeadPad healthOnlyPad =
                CharacterTestFairyGuiHeadPad.CreateDetachedForTests(out Fgui.GComponent healthOnlyRoot);

            SyncHeadPad(
                healthOnlyPad,
                CreateCohesionBreakCombatState(),
                CharacterTestHeadPadDisplayMode.HealthOnly);

            AssertBreakEffectVisible(healthOnlyRoot);

            SyncHeadPad(
                healthOnlyPad,
                CreateCohesionBreakCombatState(),
                CharacterTestHeadPadDisplayMode.Hidden);

            AssertBreakEffectHidden(healthOnlyRoot);

            using CharacterTestFairyGuiHeadPad hiddenPad =
                CharacterTestFairyGuiHeadPad.CreateDetachedForTests(out Fgui.GComponent hiddenRoot);

            SyncHeadPad(
                hiddenPad,
                CreateCohesionBreakCombatState(),
                CharacterTestHeadPadDisplayMode.Hidden);

            AssertBreakEffectHidden(hiddenRoot);
        }

        [Test]
        public void HeadPad_BrokenRemainingCapacityDoesNotPreFillFromRecoveryTimer()
        {
            using CharacterTestFairyGuiHeadPad pad =
                CharacterTestFairyGuiHeadPad.CreateDetachedForTests(out Fgui.GComponent root);

            CharacterTestCharacterCombatStateComponent brokenState =
                CreateTimedCohesionBreakCombatState(breakFrame: 12L, recoveryDelayFrames: 270);

            SyncHeadPad(
                pad,
                brokenState,
                CharacterTestHeadPadDisplayMode.CombatReadout,
                currentFrame: 12L);
            AssertRecoveryHidden(root);
            Assert.IsFalse(root.GetChild("scatterPressureFill").visible);

            SyncHeadPad(
                pad,
                brokenState,
                CharacterTestHeadPadDisplayMode.CombatReadout,
                currentFrame: 147L);
            AssertRecoveryHidden(root);
            Assert.IsFalse(root.GetChild("scatterPressureFill").visible);

            SyncHeadPad(
                pad,
                brokenState,
                CharacterTestHeadPadDisplayMode.CombatReadout,
                currentFrame: 282L);
            AssertRecoveryHidden(root);
            Assert.IsFalse(root.GetChild("scatterPressureFill").visible);

            SyncHeadPad(
                pad,
                brokenState,
                CharacterTestHeadPadDisplayMode.HealthOnly,
                currentFrame: 147L);
            AssertRecoveryHidden(root);
            AssertBreakEffectVisible(root);

            SyncHeadPad(
                pad,
                brokenState,
                CharacterTestHeadPadDisplayMode.Hidden,
                currentFrame: 147L);
            AssertRecoveryHidden(root);
            AssertBreakEffectHidden(root);
        }

        [Test]
        public void CombatState_SyncScatterPressureMirrorsTimedBrokenRecovery()
        {
            CharacterTestCharacterEntity entity = CreateEntity(1, new GameplayEntityId(1, 1), 0f, 0f, 0f);
            var scatterPressure = new GameplayScatterPressureComponent(
                100,
                recoveryRate: 2,
                recoveryDelayFrames: 360,
                currentScatterPressure: 100,
                lastPressureFrame: 20L,
                currentCohesionLimit: 100,
                maxScatterDebt: 100,
                brokenRecoveryDelayFrames: 270,
                lastCohesionBreakFrame: 20L);

            entity.SyncCombatScatterPressure(scatterPressure);

            Assert.AreEqual(270, entity.CombatState.BrokenRecoveryDelayFrames);
            Assert.AreEqual(20L, entity.CombatState.LastCohesionBreakFrame);
            Assert.IsFalse(entity.CombatState.TryGetCohesionBreakRecoveryProgress(19L, out _));
            Assert.IsTrue(entity.CombatState.TryGetCohesionBreakRecoveryProgress(155L, out float progress));
            Assert.AreEqual(0.5f, progress, 0.001f);
            Assert.IsTrue(entity.CombatState.TryGetCohesionBreakRecoveryProgress(290L, out float clampedProgress));
            Assert.AreEqual(1f, clampedProgress, 0.001f);

            entity.SyncCombatScatterPressure(new GameplayScatterPressureComponent(
                100,
                recoveryRate: 2,
                recoveryDelayFrames: 360,
                currentScatterPressure: 0,
                lastPressureFrame: 20L,
                currentCohesionLimit: 100,
                maxScatterDebt: 100,
                brokenRecoveryDelayFrames: 270,
                lastCohesionBreakFrame: 20L));

            Assert.IsFalse(entity.CombatState.TryGetCohesionBreakRecoveryProgress(290L, out _));
        }

        [Test]
        public void HeadPad_CohesionBreakFlashResetsWhenReusedFromPool()
        {
            CharacterTestFairyGuiHeadPad first = null;
            CharacterTestFairyGuiHeadPad second = null;
            try
            {
                first = CharacterTestFairyGuiHeadPad.Create();
                Fgui.GComponent hudRoot = GetLayerRoot(MxUiLayer.Hud);
                Fgui.GComponent firstRoot = FindChildByName(hudRoot, "CharacterTest.HeadPad") as Fgui.GComponent;
                Assert.IsNotNull(firstRoot);

                SyncHeadPad(
                    first,
                    CreateTimedCohesionBreakCombatState(breakFrame: 12L, recoveryDelayFrames: 270),
                    CharacterTestHeadPadDisplayMode.CombatReadout,
                    currentFrame: 147L);
                AssertBreakEffectVisible(firstRoot);
                // Remaining cohesion is zero while broken; the confirmed HUD has no second recovery gauge.
                AssertRecoveryHidden(firstRoot);
                Assert.IsFalse(FindChildByName(firstRoot, "scatterPressureFill").visible);

                first.Dispose();
                first = null;

                second = CharacterTestFairyGuiHeadPad.Create();
                Fgui.GComponent secondRoot = FindChildByName(hudRoot, "CharacterTest.HeadPad") as Fgui.GComponent;
                Assert.AreSame(firstRoot, secondRoot);
                AssertBreakEffectHidden(secondRoot);
                AssertRecoveryHidden(secondRoot);
            }
            finally
            {
                second?.Dispose();
                first?.Dispose();
            }
        }

        [Test]
        public void HeadPad_PrewarmPoolCreatesDetachedReusablePads()
        {
            CharacterTestFairyGuiHeadPad pad = null;
            try
            {
                int created = CharacterTestFairyGuiHeadPad.PrewarmPool(2);

                Assert.AreEqual(2, created);
                Assert.AreEqual(2, CharacterTestFairyGuiHeadPad.PooledCountForTests);
                Assert.AreEqual(0, CharacterTestFairyGuiHeadPad.ActiveCountForTests);

                CharacterTestFairyGuiLayerRoots.EnsureLayerRoots();
                Fgui.GComponent hudRoot = GetLayerRoot(MxUiLayer.Hud);
                Assert.AreEqual(0, CountChildrenByName(hudRoot, "CharacterTest.HeadPad"));

                pad = CharacterTestFairyGuiHeadPad.Create();

                Assert.AreEqual(1, CharacterTestFairyGuiHeadPad.PooledCountForTests);
                Assert.AreEqual(1, CharacterTestFairyGuiHeadPad.ActiveCountForTests);
                Assert.AreEqual(1, CountChildrenByName(hudRoot, "CharacterTest.HeadPad"));
            }
            finally
            {
                pad?.Dispose();
            }
        }

        [Test]
        public void LayerRoots_ToastStaysAfterHudWhenHeadPadCreatesRootsFirst()
        {
            CharacterTestFairyGuiHeadPad pad = null;
            try
            {
                pad = CharacterTestFairyGuiHeadPad.Create();
                CharacterTestFairyGuiLayerRoots.EnsureLayerRoots();

                AssertToastAfterHud();
            }
            finally
            {
                pad?.Dispose();
            }
        }

        [Test]
        public void LayerRoots_ReleaseSharedLayerRootsDisposesAndRecreatesRoots()
        {
            CharacterTestFairyGuiLayerRoots.EnsureLayerRoots();
            Fgui.GComponent hudRoot = GetLayerRoot(MxUiLayer.Hud);
            Fgui.GComponent toastRoot = GetLayerRoot(MxUiLayer.Toast);
            Fgui.DisplayObject hudDisplayObject = hudRoot.displayObject;
            Fgui.DisplayObject toastDisplayObject = toastRoot.displayObject;

            if (Application.isPlaying)
            {
                Assert.AreSame(Fgui.GRoot.inst, hudRoot.parent);
                Assert.AreSame(Fgui.GRoot.inst, toastRoot.parent);
            }
            else
            {
                Assert.IsNull(hudRoot.parent);
                Assert.IsNull(toastRoot.parent);
            }

            CharacterTestFairyGuiLayerRoots.ReleaseSharedLayerRoots();

            Assert.IsNull(hudRoot.parent);
            Assert.IsNull(toastRoot.parent);
            Assert.IsTrue(hudDisplayObject.isDisposed);
            Assert.IsTrue(toastDisplayObject.isDisposed);

            CharacterTestFairyGuiLayerRoots.EnsureLayerRoots();
            Assert.AreNotSame(hudRoot, GetLayerRoot(MxUiLayer.Hud));
            Assert.AreNotSame(toastRoot, GetLayerRoot(MxUiLayer.Toast));
        }

        [Test]
        public void SliceDispose_DoesNotReleaseSharedLayerRoots_ForHeadlessOrViewfulSlice()
        {
            CharacterTestFairyGuiLayerRoots.EnsureLayerRoots();
            Fgui.GComponent hudRoot = GetLayerRoot(MxUiLayer.Hud);
            Fgui.GComponent toastRoot = GetLayerRoot(MxUiLayer.Toast);
            Fgui.DisplayObject hudDisplayObject = hudRoot.displayObject;
            Fgui.DisplayObject toastDisplayObject = toastRoot.displayObject;

            using (var slice = new GameSlice(new CharacterTestSliceOptions
            {
                Logger = NullRuntimeLogger.Instance,
                CreateCharacterViews = false,
                EnableLocalInputCommands = false
            }))
            {
            }

            using (var slice = new GameSlice(new CharacterTestSliceOptions
            {
                Logger = NullRuntimeLogger.Instance,
                CreateCharacterViews = true,
                EnableLocalInputCommands = false
            }))
            {
            }

            Assert.AreSame(hudRoot, GetLayerRoot(MxUiLayer.Hud));
            Assert.AreSame(toastRoot, GetLayerRoot(MxUiLayer.Toast));
            Assert.IsFalse(hudDisplayObject.isDisposed);
            Assert.IsFalse(toastDisplayObject.isDisposed);
        }

        [Test]
        public void HeadPad_MultiplePadsStayUnderHudAndBehindToast()
        {
            CharacterTestFairyGuiHeadPad first = null;
            CharacterTestFairyGuiHeadPad second = null;
            try
            {
                first = CharacterTestFairyGuiHeadPad.Create();
                second = CharacterTestFairyGuiHeadPad.Create();

                Fgui.GComponent hudRoot = GetLayerRoot(MxUiLayer.Hud);
                Assert.AreEqual(2, CountChildrenByName(hudRoot, "CharacterTest.HeadPad"));
                AssertToastAfterHud();
            }
            finally
            {
                second?.Dispose();
                first?.Dispose();
            }
        }

        [Test]
        public void FloatingCombatTextRelease_DoesNotDetachHeadPadHudLayer()
        {
            using var slice = new GameSlice(
                NullRuntimeLogger.Instance,
                createCharacterViews: false,
                enableCharacterSimulation: false);
            var cameraObject = new GameObject("CharacterTest Floating Text Layer Camera");
            var hostObject = new GameObject("CharacterTest Floating Text Layer Host");
            CharacterTestFairyGuiHeadPad pad = null;
            CharacterTestFairyGuiFloatingCombatTextHost host = null;
            try
            {
                cameraObject.tag = "MainCamera";
                cameraObject.AddComponent<UnityEngine.Camera>();
                pad = CharacterTestFairyGuiHeadPad.Create();
                Fgui.GComponent hudRoot = GetLayerRoot(MxUiLayer.Hud);
                Fgui.GObject headPadRoot = FindChildByName(hudRoot, "CharacterTest.HeadPad");

                host = hostObject.AddComponent<CharacterTestFairyGuiFloatingCombatTextHost>();
                host.Configure(slice, NullRuntimeLogger.Instance);
                Assert.IsTrue(host.IsConfigured, host.LastStatusMessage);

                host.Release();

                if (Application.isPlaying)
                    Assert.AreSame(Fgui.GRoot.inst, hudRoot.parent);
                else
                    Assert.IsNull(hudRoot.parent);
                Assert.AreSame(hudRoot, headPadRoot.parent);
                AssertToastAfterHud();
            }
            finally
            {
                host?.Release();
                pad?.Dispose();
                UnityEngine.Object.DestroyImmediate(hostObject);
                UnityEngine.Object.DestroyImmediate(cameraObject);
            }
        }

        [Test]
        public void UiRuntime_ConfigureFairyGuiPrewarmsHeadPadPool()
        {
            using var slice = new GameSlice(
                NullRuntimeLogger.Instance,
                createCharacterViews: false,
                enableCharacterSimulation: false);
            var hostObject = new GameObject("CharacterTest HeadPad Prewarm Runtime Host");
            CharacterTestUiRuntime runtime = null;
            try
            {
                CharacterTestThirdPersonCameraRigBridge owner =
                    hostObject.AddComponent<CharacterTestThirdPersonCameraRigBridge>();
                runtime = new CharacterTestUiRuntime(owner, NullRuntimeLogger.Instance);

                runtime.Configure(new CharacterTestUiRuntimeOptions
                {
                    Slice = slice,
                    IsPausedProvider = () => false,
                    EnableDebugUi = false,
                    EnableFairyGuiCombatHud = true,
                    EnableFairyGuiFloatingCombatText = false,
                    EnableFairyGuiStoryUi = false,
                    EnableFairyGuiMainMenu = false,
                    EnableFairyGuiNetworkPanel = false,
                    EnableFairyGuiSettingsPanel = false,
                    EnableFairyGuiAbilityBindingPanel = false
                });

                Assert.AreEqual(
                    CharacterTestFairyGuiHeadPad.DefaultPrewarmCount,
                    CharacterTestFairyGuiHeadPad.PooledCountForTests);
                Assert.AreEqual(0, CharacterTestFairyGuiHeadPad.ActiveCountForTests);
            }
            finally
            {
                runtime?.Release();
                UnityEngine.Object.DestroyImmediate(hostObject);
            }
        }

        [Test]
        public void UiRuntime_DisabledFloatingCombatTextDoesNotCreateHost()
        {
            using var slice = new GameSlice(
                NullRuntimeLogger.Instance,
                createCharacterViews: false,
                enableCharacterSimulation: false);
            var hostObject = new GameObject("CharacterTest Floating Text Disabled Host Test");
            CharacterTestUiRuntime runtime = null;
            try
            {
                CharacterTestThirdPersonCameraRigBridge owner =
                    hostObject.AddComponent<CharacterTestThirdPersonCameraRigBridge>();
                runtime = new CharacterTestUiRuntime(owner, NullRuntimeLogger.Instance);

                runtime.Configure(new CharacterTestUiRuntimeOptions
                {
                    Slice = slice,
                    IsPausedProvider = () => false,
                    EnableDebugUi = false,
                    EnableFairyGuiCombatHud = false,
                    EnableFairyGuiFloatingCombatText = false,
                    EnableFairyGuiStoryUi = false
                });

                Assert.IsNull(runtime.FairyGuiFloatingCombatTextHost);
                Assert.IsNull(hostObject.GetComponent<CharacterTestFairyGuiFloatingCombatTextHost>());
                Assert.IsFalse(runtime.IsFairyGuiFloatingCombatTextConfigured);
                Assert.AreEqual(string.Empty, runtime.LastFairyGuiFloatingCombatTextStatus);
                Assert.IsNull(runtime.FairyGuiCombatHudHost);
                Assert.IsNull(runtime.FairyGuiStoryDialogHost);
            }
            finally
            {
                runtime?.Release();
                UnityEngine.Object.DestroyImmediate(hostObject);
            }
        }

        [Test]
        public void UiRuntime_RefreshForwardsToFloatingCombatTextOncePerFrameAndReleaseIsIdempotent()
        {
            using var slice = new GameSlice(
                NullRuntimeLogger.Instance,
                createCharacterViews: false,
                enableCharacterSimulation: false);
            IList<DamageTelemetryRow> rows = GetMutableDamageRows(slice);
            rows.Add(CreateTelemetryRow(
                runtimeFrame: slice.CurrentFrame.Value,
                resultKind: HitResolveKind.Damage,
                finalDamage: 19,
                hitPointWorld: Point(0, 1, 5)));

            var cameraObject = new GameObject("CharacterTest Floating Text Runtime Camera");
            var hostObject = new GameObject("CharacterTest Floating Text Runtime Host");
            CharacterTestUiRuntime runtime = null;
            CharacterTestFairyGuiFloatingCombatTextHost floatingHost = null;
            try
            {
                cameraObject.tag = "MainCamera";
                cameraObject.AddComponent<UnityEngine.Camera>();
                CharacterTestThirdPersonCameraRigBridge owner =
                    hostObject.AddComponent<CharacterTestThirdPersonCameraRigBridge>();
                runtime = new CharacterTestUiRuntime(owner, NullRuntimeLogger.Instance);
                runtime.Configure(new CharacterTestUiRuntimeOptions
                {
                    Slice = slice,
                    IsPausedProvider = () => false,
                    EnableDebugUi = false,
                    EnableFairyGuiCombatHud = false,
                    EnableFairyGuiFloatingCombatText = true,
                    EnableFairyGuiStoryUi = false
                });

                floatingHost = runtime.FairyGuiFloatingCombatTextHost;
                Assert.IsNotNull(floatingHost);
                Assert.IsTrue(runtime.IsFairyGuiFloatingCombatTextConfigured, runtime.LastFairyGuiFloatingCombatTextStatus);
                Assert.AreEqual(CharacterTestFairyGuiFloatingCombatTextHost.PrewarmCount, floatingHost.CreatedTextObjectCount);
                Assert.AreEqual(1, floatingHost.SpawnedCount);
                Assert.AreEqual(1, floatingHost.ActiveCount);

                runtime.Refresh();

                Assert.AreEqual(1, floatingHost.SpawnedCount);
                Assert.AreEqual(1, floatingHost.ActiveCount);
                Assert.AreEqual(1, floatingHost.SkippedRefreshCount);
                Assert.GreaterOrEqual(floatingHost.RefreshAttemptCount, 2);

                runtime.TickGameplayPresentation(99f);

                Assert.AreEqual(0, floatingHost.ActiveCount);

                runtime.Release();

                Assert.IsNull(runtime.FairyGuiFloatingCombatTextHost);
                Assert.IsFalse(floatingHost.IsConfigured);
                Assert.DoesNotThrow(() => runtime.Refresh());
                Assert.DoesNotThrow(() => runtime.Release());
            }
            finally
            {
                runtime?.Release();
                UnityEngine.Object.DestroyImmediate(hostObject);
                UnityEngine.Object.DestroyImmediate(cameraObject);
            }
        }

        private static DamageTelemetryRow CreateTelemetryRow(
            HitResolveKind resultKind,
            int finalDamage,
            FixVector3 hitPointWorld,
            long runtimeFrame = 12)
        {
            return new DamageTelemetryRow(
                runtimeFrame,
                DamageProfileSourceKind.WeaponTrace,
                new MxFramework.CharacterAction.CharacterActionId(301),
                actionInstanceId: 1,
                stableEventId: global::MxFramework.Tests.Game.CharacterTest.MigrationFixtureIds.Id0033,
                damageProfileId: global::MxFramework.Tests.Game.CharacterTest.MigrationFixtureIds.Id0021,
                damageTypeId: CharacterTestAuthoredContentIds.D409_Id1,
                equipmentSlotId: CharacterTestAuthoredContentIds.D405_Id1,
                weaponId: global::MxFramework.Tests.Game.CharacterTest.MigrationFixtureIds.Id0088,
                sourceEntityId: 1,
                targetEntityId: 2,
                hitZoneId: MxFramework.Game.CharacterTest.CharacterTestAuthoredContentIds.D400_Id55,
                bodyPartId: MxFramework.Game.CharacterTest.CharacterTestAuthoredContentIds.D400_Id10,
                localFrame: 4,
                incomingDamage: Math.Max(0, finalDamage),
                finalDamage: finalDamage,
                resultKind: resultKind,
                hpBefore: 100,
                hpAfter: Math.Max(0, 100 - finalDamage),
                hitPointWorld: hitPointWorld,
                hitNormalWorld: Point(0, 1, 0));
        }

        private static CharacterTestFloatingCombatTextEvent CreateEvent(int priority, string text)
        {
            return new CharacterTestFloatingCombatTextEvent(
                new RuntimeFrame(priority),
                CharacterTestFloatingCombatTextKind.Damage,
                sourceEntityId: 1,
                targetEntityId: 2,
                text,
                new Vector3(0f, 1f, 5f),
                priority,
                CharacterTestFloatingCombatTextStyle.Damage);
        }

        private static CharacterTestCharacterEntity CreateEntity(
            int entityId,
            GameplayEntityId gameplayEntityId,
            float x,
            float y,
            float z)
        {
            return new CharacterTestCharacterEntity(
                new CharacterTestCharacterEntityId(entityId),
                new CharacterTestCharacterIdentityComponent(entityId, "Entity " + entityId, default, default),
                new CharacterTestCharacterTeamComponent(1, 1),
                new CharacterTestCharacterTransformComponent(x, y, z),
                new CharacterTestCharacterEquipmentComponent(useDefaultLoadout: false),
                new CharacterTestCharacterControlComponent(CharacterTestCharacterControlOwner.None),
                gameplayEntityId);
        }

        private static CharacterTestCharacterCombatStateComponent CreateCohesionBreakCombatState()
        {
            return CharacterTestCharacterCombatStateComponent
                .CreateDefault()
                .WithScatterPressure(100, 100, PressureBand.Broken)
                .ApplyCohesionBreak(reactionFrames: 18, frame: 12L);
        }

        private static CharacterTestCharacterCombatStateComponent CreateTimedCohesionBreakCombatState(
            long breakFrame,
            int recoveryDelayFrames)
        {
            return CharacterTestCharacterCombatStateComponent
                .CreateDefault()
                .WithScatterPressure(
                    100,
                    100,
                    PressureBand.Broken,
                    recoveryDelayFrames,
                    breakFrame)
                .ApplyCohesionBreak(reactionFrames: 18, frame: breakFrame);
        }

        private static void SyncHeadPad(
            CharacterTestFairyGuiHeadPad pad,
            CharacterTestCharacterCombatStateComponent combatState,
            CharacterTestHeadPadDisplayMode displayMode,
            long currentFrame = 12L,
            bool lockedVisible = true,
            CharacterTestHeadPadAllegiance allegiance = CharacterTestHeadPadAllegiance.Enemy)
        {
            pad.Sync(
                1,
                "Target",
                2,
                CharacterTestCharacterControlOwner.RemotePlayer,
                allegiance,
                combatState,
                lockedVisible,
                displayMode,
                null,
                Vector3.zero,
                currentFrame);
        }

        private static void AssertBreakEffectVisible(Fgui.GComponent root)
        {
            Fgui.GObject flash = FindChildByName(root, "cohesionBreakFlash");
            Fgui.GTextField label = FindChildByName(root, "cohesionBreakLabel") as Fgui.GTextField;

            Assert.IsNotNull(flash);
            Assert.IsNotNull(label);
            Assert.IsTrue(flash.visible);
            Assert.Greater(flash.alpha, 0f);
            Assert.IsTrue(label.visible);
            Assert.AreEqual("破聚", label.text);
            Assert.Greater(label.alpha, 0f);
            Assert.Greater(label.scaleX, 1f);
        }

        private static void AssertBreakEffectHidden(Fgui.GComponent root)
        {
            Fgui.GObject flash = FindChildByName(root, "cohesionBreakFlash");
            Fgui.GTextField label = FindChildByName(root, "cohesionBreakLabel") as Fgui.GTextField;

            Assert.IsNotNull(flash);
            Assert.IsNotNull(label);
            Assert.IsFalse(flash.visible);
            Assert.AreEqual(0f, flash.alpha, 0.001f);
            Assert.IsFalse(label.visible);
            Assert.AreEqual(0f, label.alpha, 0.001f);
            Assert.AreEqual(1f, label.scaleX, 0.001f);
            Assert.AreEqual(1f, label.scaleY, 0.001f);
        }

        private static void AssertRecoveryProgress(
            Fgui.GComponent root,
            float expectedWidth,
            float widthTolerance)
        {
            Fgui.GObject fill = FindChildByName(root, "cohesionRecoveryFill");
            Fgui.GObject spark = FindChildByName(root, "cohesionRecoverySpark");

            Assert.IsNotNull(fill);
            Assert.IsNotNull(spark);
            Assert.IsTrue(fill.visible);
            Assert.IsTrue(spark.visible);
            Assert.AreEqual(expectedWidth, fill.width, widthTolerance);
        }

        private static void AssertRecoveryHidden(Fgui.GComponent root)
        {
            Fgui.GObject fill = FindChildByName(root, "cohesionRecoveryFill");
            Fgui.GObject spark = FindChildByName(root, "cohesionRecoverySpark");

            Assert.IsNotNull(fill);
            Assert.IsNotNull(spark);
            Assert.IsFalse(fill.visible);
            Assert.IsFalse(spark.visible);
        }

        private static FixVector3 Point(int x, int y, int z)
        {
            return new FixVector3(Fix64.FromInt(x), Fix64.FromInt(y), Fix64.FromInt(z));
        }

        private static IList<DamageTelemetryRow> GetMutableDamageRows(GameSlice slice)
        {
            var rows = slice.CombatTraceSystem.DamageTelemetryRows as IList<DamageTelemetryRow>;
            Assert.IsNotNull(rows);
            return rows;
        }

        private static Fgui.GComponent GetLayerRoot(MxUiLayer layer)
        {
            Assert.IsTrue(CharacterTestFairyGuiLayerRoots.TryGetLayerRoot(layer, out Fgui.GComponent root));
            Assert.IsNotNull(root);
            return root;
        }

        private static void AssertToastAfterHud()
        {
            IReadOnlyList<MxUiLayer> layerOrder = CharacterTestFairyGuiLayerRoots.SharedLayerHost.LayerOrder;
            int hudOrder = IndexOfLayer(layerOrder, MxUiLayer.Hud);
            int toastOrder = IndexOfLayer(layerOrder, MxUiLayer.Toast);
            Assert.GreaterOrEqual(hudOrder, 0);
            Assert.Greater(toastOrder, hudOrder);

            int hudIndex = CharacterTestFairyGuiLayerRoots.GetLayerRootIndex(MxUiLayer.Hud);
            int toastIndex = CharacterTestFairyGuiLayerRoots.GetLayerRootIndex(MxUiLayer.Toast);
            if (hudIndex >= 0 || toastIndex >= 0)
                Assert.Greater(toastIndex, hudIndex);
        }

        private static int IndexOfLayer(IReadOnlyList<MxUiLayer> layers, MxUiLayer layer)
        {
            for (int i = 0; i < layers.Count; i++)
            {
                if (layers[i] == layer)
                    return i;
            }

            return -1;
        }

        private static Fgui.GObject FindChildByName(Fgui.GComponent root, string name)
        {
            for (int i = 0; i < root.numChildren; i++)
            {
                Fgui.GObject child = root.GetChildAt(i);
                if (child != null && string.Equals(child.name, name, StringComparison.Ordinal))
                    return child;
            }

            return null;
        }

        private static int CountChildrenByName(Fgui.GComponent root, string name)
        {
            int count = 0;
            for (int i = 0; i < root.numChildren; i++)
            {
                Fgui.GObject child = root.GetChildAt(i);
                if (child != null && string.Equals(child.name, name, StringComparison.Ordinal))
                    count++;
            }

            return count;
        }

        private static void DestroyNativeDisplayObjects(Fgui.GComponent root)
        {
            if (root == null)
                return;

            for (int i = 0; i < root.numChildren; i++)
            {
                Fgui.GObject child = root.GetChildAt(i);
                GameObject native = child?.displayObject?.gameObject;
                if (native != null)
                    UnityEngine.Object.DestroyImmediate(native);
            }
        }

        private static void DestroyObjectNamed(string objectName)
        {
            if (string.Equals(objectName, "Stage Camera", StringComparison.Ordinal)
                || string.Equals(objectName, "Stage", StringComparison.Ordinal)
                || string.Equals(objectName, "GRoot", StringComparison.Ordinal))
            {
                CharacterTestFairyGuiTestStageReset.Reset();
                return;
            }

            GameObject existing = GameObject.Find(objectName);
            if (existing != null)
                UnityEngine.Object.DestroyImmediate(existing);
        }
    }
}
