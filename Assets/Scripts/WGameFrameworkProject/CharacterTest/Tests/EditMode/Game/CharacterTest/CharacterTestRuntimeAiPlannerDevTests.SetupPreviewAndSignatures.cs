using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using MxFramework.Animation;
using MxFramework.Audio;
using MxFramework.CharacterAction;
using MxFramework.CharacterRuntimeSpawn.Unity;
using MxFramework.Combat.Core;
using MxFramework.Combat.GameplayBridge;
using MxFramework.Combat.Physics;
using MxFramework.Combat.Projectiles;
using MxFramework.Combat.Shields;
using MxFramework.Core.Math;
using MxFramework.Game.CharacterTest;
using MxFramework.Gameplay;
using MxFramework.Network;
using MxFramework.Resources;
using MxFramework.Runtime;
using MxFramework.UI;
using MxFramework.UI.FairyGui;
using MxFramework.Vfx;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace MxFramework.Tests.Game.CharacterTest
{
    public sealed partial class CharacterTestRuntimeAiPlannerDevTests
    {

        [Test]
        public void RuntimeAiLabViewModel_PresentationContextDistinguishesLivePausedAndReview()
        {
            var timeline = new CharacterTestRuntimeAiPlannerDevTimeline();
            timeline.AddRecord(10, 0f, 0d, null, null, null, 10L);
            timeline.AddRecord(20, 0f, 0d, null, null, null, 20L);
            timeline.SelectLatest();
            var buffer = new CharacterTestRuntimeAiPlannerDevFrameBuffer();

            CharacterTestRuntimeAiPlannerDevSceneSnapshot live = CreateDevSceneSnapshot(
                paused: false,
                timeline: timeline,
                buffer: buffer);
            CharacterTestRuntimeAiPlannerDevSceneSnapshot paused = CreateDevSceneSnapshot(
                paused: true,
                timeline: timeline,
                buffer: buffer);

            Assert.AreEqual(
                CharacterTestRuntimeAiPlannerDevPresentationContext.Live,
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelViewModelBuilder.ResolvePresentationContext(live));
            Assert.AreEqual(
                CharacterTestRuntimeAiPlannerDevPresentationContext.PausedInspect,
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelViewModelBuilder.ResolvePresentationContext(paused));

            timeline.SelectFrame(10);
            CharacterTestRuntimeAiPlannerDevSceneSnapshot review = CreateDevSceneSnapshot(
                paused: true,
                timeline: timeline,
                buffer: buffer);
            Assert.AreEqual(
                CharacterTestRuntimeAiPlannerDevPresentationContext.Review,
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelViewModelBuilder.ResolvePresentationContext(review));

            CharacterTestRuntimeAiPlannerDevSceneSnapshot loopReview = CreateDevSceneSnapshot(
                paused: true,
                timeline: timeline,
                buffer: buffer,
                loopPlaybackActive: true);
            Assert.AreEqual(
                CharacterTestRuntimeAiPlannerDevPresentationContext.LoopReview,
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelViewModelBuilder.ResolvePresentationContext(loopReview));

            var frameZeroTimeline = new CharacterTestRuntimeAiPlannerDevTimeline();
            frameZeroTimeline.AddRecord(0, 0f, 0d, null, null, null, 1L);
            frameZeroTimeline.AddRecord(1, 0f, 0d, null, null, null, 2L);
            Assert.IsTrue(frameZeroTimeline.SelectFrame(0));
            CharacterTestRuntimeAiPlannerDevSceneSnapshot frameZeroReview = CreateDevSceneSnapshot(
                paused: true,
                timeline: frameZeroTimeline,
                buffer: buffer);
            Assert.AreEqual(
                CharacterTestRuntimeAiPlannerDevPresentationContext.Review,
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelViewModelBuilder.ResolvePresentationContext(
                    frameZeroReview));
            Assert.AreEqual(
                "回看 | 帧 0",
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelViewModelBuilder.BuildPhaseText(
                    CharacterTestRuntimeAiPlannerDevPresentationContext.Review,
                    frameZeroTimeline));
        }

        [UnityTest]
        public IEnumerator RuntimeAiLabScene_JumpToFrameZeroResolvesFirstRetainedFrame()
        {
            EditorSceneManager.OpenScene(RuntimeAiLabScenePath, OpenSceneMode.Single);

            yield return new EnterPlayMode();
            yield return null;
            yield return null;

            CharacterTestRuntimeAiPlannerDevSceneController controller =
                Object.FindAnyObjectByType<CharacterTestRuntimeAiPlannerDevSceneController>();
            Assert.IsNotNull(controller);
            Assert.IsTrue(controller.StartSession(), controller.StatusMessage);

            int firstRetainedFrame = controller.Timeline.RetainedStartFrame;
            Assert.IsTrue(controller.JumpToFrame(0), controller.StatusMessage);
            int updateBudget = 120;
            while (controller.ReplayInProgress && updateBudget-- > 0)
                yield return null;

            Assert.Greater(updateBudget, 0, controller.StatusMessage);
            Assert.AreEqual(firstRetainedFrame, controller.Timeline.SelectedFrame);
            StringAssert.DoesNotContain("大于零", controller.StatusMessage);

            yield return new ExitPlayMode();
        }

        [Test]
        public void RuntimeAiLabController_SetupSlotCommandsUpdateSharedMatchConfig()
        {
            var host = new GameObject("Runtime AI Lab Setup Slot Command Test");
            try
            {
                CharacterTestRuntimeAiPlannerDevSceneController controller =
                    host.AddComponent<CharacterTestRuntimeAiPlannerDevSceneController>();
                controller.PrepareInitialSetupState();
                Assert.IsTrue(controller.SelectSetupSpawnSlot(CharacterTestArenaMatchConfig.Team1Id, 1));

                Assert.IsTrue(controller.ToggleSelectedSetupSlotEnabled());
                Assert.IsTrue(controller.MatchConfig.Team1Slots[1].Enabled);
                int originalDraft = controller.MatchConfig.Team1Slots[1].BehaviorGroupDraftId;
                Assert.IsTrue(controller.SelectNextSetupBehaviorGroup());
                int selectedDraft = controller.MatchConfig.Team1Slots[1].BehaviorGroupDraftId;
                Assert.AreNotEqual(
                    originalDraft,
                    selectedDraft);
                CharacterTestRuntimeAiLabDraftSlot workspaceSlot =
                    controller.DraftWorkspace.TryGetSlot(
                        CharacterTestArenaMatchConfig.Team1Id,
                        1);
                Assert.IsNotNull(workspaceSlot);
                Assert.AreEqual(selectedDraft, workspaceSlot.ArenaBehaviorGroupDraftId);
                Assert.IsTrue(controller.DraftWorkspace.Dirty);
                Assert.IsTrue(controller.SelectSetupBehaviorGroupDraft(originalDraft));
                Assert.AreEqual(
                    originalDraft,
                    controller.MatchConfig.Team1Slots[1].BehaviorGroupDraftId);
                Assert.AreEqual(originalDraft, workspaceSlot.ArenaBehaviorGroupDraftId);
                Assert.IsTrue(controller.SelectNextSetupCharacter());
                Assert.AreEqual(
                    CharacterTestCharacterDefinitionRegistry.DefaultPlayableCharacterId,
                    controller.MatchConfig.Team1Slots[1].CharacterId);
                Assert.IsTrue(controller.SelectSetupCharacterId(
                    CharacterTestCharacterDefinitionRegistry.DefaultPlayableCharacterId));
                Assert.IsFalse(controller.SelectSetupCharacterId(int.MaxValue));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(host);
            }
        }

        [Test]
        public void RuntimeAiLabController_TrainingDummyCandidateUsesOnlyAuthoredBehaviors()
        {
            var host = new GameObject("Runtime AI Lab Training Dummy Candidate Test");
            try
            {
                CharacterTestRuntimeAiPlannerDevSceneController controller =
                    host.AddComponent<CharacterTestRuntimeAiPlannerDevSceneController>();
                controller.PrepareInitialSetupState(restorePersistedDraft: false);
                Assert.IsTrue(controller.SelectSetupSpawnSlot(CharacterTestArenaMatchConfig.Team2Id, 0));
                Assert.IsTrue(controller.SelectSetupBehaviorGroupDraft(4));

                MethodInfo buildCandidate = typeof(CharacterTestRuntimeAiPlannerDevSceneController)
                    .GetMethod("BuildCandidateRevision", BindingFlags.Instance | BindingFlags.NonPublic);
                Assert.IsNotNull(buildCandidate);
                buildCandidate.Invoke(controller, null);

                Assert.IsNotNull(controller.CandidateRevision, controller.CandidateDiagnostic);
                Assert.AreEqual(2, controller.CandidateRevision.Profile.Entries.Count);
                CollectionAssert.AreEqual(
                    new[]
                    {
                        CharacterTestRuntimeAiPlannerBehaviorCompositionCatalog.FoundationApproachAndAttack,
                        CharacterTestRuntimeAiPlannerBehaviorCompositionCatalog.FoundationIdle
                    },
                    controller.CandidateRevision.Profile.Entries
                        .Select(entry => entry.BehaviorId)
                        .ToArray());
                Assert.AreEqual(2, controller.CandidateRevision.LogicPrograms.Count);
                Assert.IsTrue(controller.CandidateRevision.LogicPrograms.All(program =>
                    program.Stages.All(stage =>
                        stage.Kind == CharacterTestRuntimeAiLabCardStepKind.Logic
                        || stage.Kind == CharacterTestRuntimeAiLabCardStepKind.Wait)));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(host);
            }
        }

        private static CharacterTestRuntimeAiPlannerDevSceneSnapshot CreateDevSceneSnapshot(
            bool paused,
            CharacterTestRuntimeAiPlannerDevTimeline timeline,
            CharacterTestRuntimeAiPlannerDevFrameBuffer buffer,
            bool loopPlaybackActive = false,
            bool replayInProgress = false)
        {
            return new CharacterTestRuntimeAiPlannerDevSceneSnapshot(
                CharacterTestRuntimeAiPlannerDevScenePhase.Running,
                string.Empty,
                paused,
                debugEditGateEnabled: false,
                detailsExpanded: false,
                loopPlaybackActive,
                historyPlaybackActive: false,
                replayInProgress,
                branchRecomputeArmed: false,
                branchRecomputeBaseFrame: 0,
                branchRecomputeOriginalLatestFrame: 0,
                selectedAgentIndex: 0,
                frameBuffer: buffer,
                timeline: timeline,
                selectedFrame: null);
        }

        [Test]
        public void RuntimeAiLabCommandSink_FitSetupViewUsesSetupCamera()
        {
            var host = new GameObject("Runtime AI Lab Fit View Test");
            try
            {
                CharacterTestRuntimeAiPlannerDevSceneController controller =
                    host.AddComponent<CharacterTestRuntimeAiPlannerDevSceneController>();
                host.AddComponent<CharacterTestRuntimeAiLabSetupCameraController>();
                controller.PrepareInitialSetupState();

                var sink = new CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandSink(controller);
                sink.Enqueue(new MxUiCommand(
                    CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.ViewId,
                    CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.FitSetupView,
                    null));

                Assert.AreEqual(
                    CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.FitSetupView,
                    sink.LastCommand.CommandId);
                StringAssert.Contains("已适配", sink.LastStatusMessage);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(host);
            }
        }

        [Test]
        public void RuntimeAiLabSetupCamera_FitViewUsesPerspectiveRtsDefaults()
        {
            var cameraHost = new GameObject("Runtime AI Lab Main Camera Test");
            var controllerHost = new GameObject("Runtime AI Lab Setup Camera Test");
            try
            {
                cameraHost.tag = "MainCamera";
                UnityEngine.Camera camera = cameraHost.AddComponent<UnityEngine.Camera>();
                CharacterTestRuntimeAiPlannerDevSceneController controller =
                    controllerHost.AddComponent<CharacterTestRuntimeAiPlannerDevSceneController>();
                CharacterTestRuntimeAiLabSetupCameraController setupCamera =
                    controllerHost.AddComponent<CharacterTestRuntimeAiLabSetupCameraController>();
                controller.PrepareInitialSetupState();
                setupCamera.FitView();

                MethodInfo lateUpdate = typeof(CharacterTestRuntimeAiLabSetupCameraController)
                    .GetMethod("LateUpdate", BindingFlags.Instance | BindingFlags.NonPublic);
                Assert.IsNotNull(lateUpdate);
                lateUpdate.Invoke(setupCamera, null);

                Assert.IsFalse(camera.orthographic);
                Assert.AreEqual(45f, setupCamera.Yaw, 0.01f);
                Assert.GreaterOrEqual(setupCamera.Pitch, 45f);
                Assert.LessOrEqual(setupCamera.Pitch, 65f);
                Assert.Greater(setupCamera.Distance, 40f);
                Assert.Less(camera.transform.eulerAngles.x, 70f);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(controllerHost);
                UnityEngine.Object.DestroyImmediate(cameraHost);
            }
        }

        [Test]
        public void RuntimeAiLabCameraResolver_SkipsFairyGuiUiCamera()
        {
            var uiHost = new GameObject("FairyGUI Stage Camera Test");
            var sceneHost = new GameObject("Runtime AI Lab Scene Camera Test");
            try
            {
                typeof(CharacterTestRuntimeAiLabCameraResolver)
                    .GetField("_cachedCamera", BindingFlags.Static | BindingFlags.NonPublic)
                    ?.SetValue(null, null);
                CharacterTestRuntimeAiLabCameraResolver.RequestSceneCameraRefresh();

                uiHost.tag = "MainCamera";
                UnityEngine.Camera uiCamera = uiHost.AddComponent<UnityEngine.Camera>();
                int uiLayer = LayerMask.NameToLayer("UI");
                uiCamera.cullingMask = uiLayer >= 0 ? 1 << uiLayer : 0;

                UnityEngine.Camera sceneCamera = sceneHost.AddComponent<UnityEngine.Camera>();
                sceneCamera.cullingMask = 1 << LayerMask.NameToLayer("Default");

                Assert.IsTrue(CharacterTestRuntimeAiLabCameraResolver.TryGetSceneCamera(
                    out UnityEngine.Camera resolved));
                Assert.AreNotSame(uiCamera, resolved);
                Assert.IsTrue((resolved.cullingMask & ~(1 << uiLayer)) != 0);
                StringAssert.DoesNotContain("FairyGUI", resolved.gameObject.name);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(sceneHost);
                UnityEngine.Object.DestroyImmediate(uiHost);
            }
        }

        [Test]
        public void RuntimeAiLabCameraResolver_NotificationReplacesLiveSourceAndRejectsPreviewCamera()
        {
            var firstHost = new GameObject("Runtime AI Lab Source Camera A");
            var secondHost = new GameObject("Runtime AI Lab Source Camera B");
            var previewHost = new GameObject("Runtime AI Lab Preview Camera Candidate");
            try
            {
                UnityEngine.Camera first = firstHost.AddComponent<UnityEngine.Camera>();
                UnityEngine.Camera second = secondHost.AddComponent<UnityEngine.Camera>();
                UnityEngine.Camera preview = previewHost.AddComponent<UnityEngine.Camera>();
                previewHost.AddComponent<CharacterTestRuntimeAiLabPreviewCameraMarker>();

                CharacterTestRuntimeAiLabCameraResolver.NotifySceneCameraChanged(first);
                Assert.IsTrue(CharacterTestRuntimeAiLabCameraResolver.TryGetSceneCamera(
                    out UnityEngine.Camera resolvedFirst,
                    out long firstGeneration));
                Assert.AreSame(first, resolvedFirst);

                CharacterTestRuntimeAiLabCameraResolver.NotifySceneCameraChanged(second);
                Assert.IsTrue(first.isActiveAndEnabled, "The former source stays alive for this regression.");
                Assert.IsTrue(CharacterTestRuntimeAiLabCameraResolver.TryGetSceneCamera(
                    out UnityEngine.Camera resolvedSecond,
                    out long secondGeneration));
                Assert.AreSame(second, resolvedSecond);
                Assert.Greater(secondGeneration, firstGeneration);

                CharacterTestRuntimeAiLabCameraResolver.NotifySceneCameraChanged(preview);
                Assert.IsTrue(CharacterTestRuntimeAiLabCameraResolver.TryGetSceneCamera(
                    out UnityEngine.Camera resolvedAfterPreview));
                Assert.AreNotSame(preview, resolvedAfterPreview);
            }
            finally
            {
                CharacterTestRuntimeAiLabCameraResolver.NotifySceneCameraChanged(null);
                UnityEngine.Object.DestroyImmediate(previewHost);
                UnityEngine.Object.DestroyImmediate(secondHost);
                UnityEngine.Object.DestroyImmediate(firstHost);
            }
        }

        [UnityTest]
        public IEnumerator RuntimeAiLabBehaviorCardPreviewHost_RebindsNewRootAndRebuildsInvalidTexture()
        {
            yield return new EnterPlayMode();

            var sourceAHost = new GameObject("Runtime AI Lab Preview Test Source A");
            var sourceBHost = new GameObject("Runtime AI Lab Preview Test Source B");
            global::FairyGUI.GComponent firstHeader = null;
            global::FairyGUI.GComponent secondHeader = null;
            global::FairyGUI.GComponent localFaultHeader = null;
            global::FairyGUI.GComponent contractFaultHeader = null;
            CharacterTestRuntimeAiLabBehaviorCardPreviewHost previewHost = null;
            try
            {
                UnityEngine.Camera sourceA = sourceAHost.AddComponent<UnityEngine.Camera>();
                sourceA.cullingMask = 1 << LayerMask.NameToLayer("Default");
                sourceA.fieldOfView = 48f;
                sourceA.transform.SetPositionAndRotation(
                    new Vector3(3f, 7f, -5f),
                    Quaternion.Euler(35f, 12f, 0f));
                CharacterTestRuntimeAiLabCameraResolver.NotifySceneCameraChanged(sourceA);

                firstHeader = CreateRuntimeAiLabPreviewTestHeader(868f, 184f);
                previewHost = new CharacterTestRuntimeAiLabBehaviorCardPreviewHost(null);
                previewHost.Bind(
                    firstHeader,
                    new CharacterTestFairyGuiRuntimeAiPlannerDevPanelViewModel(),
                    true);

                Assert.IsTrue(previewHost.Result.Ready, previewHost.Result.Message);
                RenderTexture firstTexture = previewHost.BoundRenderTexture;
                Assert.IsNotNull(firstTexture);
                Assert.IsTrue(firstTexture.IsCreated());
                Assert.AreSame(firstTexture, previewHost.BoundLoader.texture.nativeTexture);
                Assert.AreEqual(sourceA.transform.position, previewHost.PreviewCamera.transform.position);
                Assert.AreEqual(sourceA.transform.rotation, previewHost.PreviewCamera.transform.rotation);
                Assert.Greater(
                    CharacterTestRuntimeAiLabBehaviorCardPreviewHost
                        .ResolveFramingDirection(sourceA).y,
                    0f,
                    "Preview framing must remain above the subject instead of inheriting an origin-below-target direction.");
                int uiLayer = LayerMask.NameToLayer("UI");
                if (uiLayer >= 0)
                {
                    Assert.AreEqual(
                        0,
                        previewHost.PreviewCamera.cullingMask & (1 << uiLayer),
                        "Preview Camera must never render the FairyGUI UI layer copied from the source Camera.");
                }
                sourceAHost.AddComponent<CharacterTestRuntimeAiLabPreviewCameraMarker>();
                previewHost.PumpActiveSurface();
                Assert.IsTrue(
                    previewHost.Result.Ready,
                    "A Ready pump must trust the resolver's accepted source/generation and skip marker lookup.");

                firstHeader.Dispose();
                firstHeader = null;
                secondHeader = CreateRuntimeAiLabPreviewTestHeader(868f, 184f);
                previewHost.Bind(
                    secondHeader,
                    new CharacterTestFairyGuiRuntimeAiPlannerDevPanelViewModel(),
                    true);

                Assert.IsTrue(previewHost.Result.Ready, previewHost.Result.Message);
                Assert.AreSame(firstTexture, previewHost.BoundRenderTexture);
                Assert.AreSame(firstTexture, previewHost.BoundLoader.texture.nativeTexture);

                global::FairyGUI.GObject guide = secondHeader.GetChild("scenePreviewGuide");
                guide.width = 640f;
                previewHost.PumpActiveSurface();
                RenderTexture resizedTexture = previewHost.BoundRenderTexture;
                Assert.IsTrue(previewHost.Result.Ready, previewHost.Result.Message);
                Assert.AreNotSame(firstTexture, resizedTexture);
                Assert.AreSame(resizedTexture, previewHost.PreviewCamera.targetTexture);

                resizedTexture.Release();
                Assert.IsFalse(resizedTexture.IsCreated());
                previewHost.PumpActiveSurface();
                RenderTexture recoveredTexture = previewHost.BoundRenderTexture;
                Assert.IsTrue(previewHost.Result.Ready, previewHost.Result.Message);
                Assert.AreNotSame(resizedTexture, recoveredTexture);
                Assert.IsTrue(recoveredTexture.IsCreated());
                Assert.AreSame(recoveredTexture, previewHost.BoundLoader.texture.nativeTexture);

                UnityEngine.Camera sourceB = sourceBHost.AddComponent<UnityEngine.Camera>();
                sourceB.cullingMask = 1 << LayerMask.NameToLayer("Default");
                sourceB.fieldOfView = 31f;
                CharacterTestRuntimeAiLabCameraResolver.NotifySceneCameraChanged(sourceB);
                previewHost.PumpActiveSurface();
                Assert.AreEqual(31f, previewHost.PreviewCamera.fieldOfView, 0.01f);

                previewHost.SetActive(false);
                Assert.AreEqual(
                    CharacterTestRuntimeAiLabPreviewState.Suspended,
                    previewHost.Result.State);
                Assert.IsFalse(previewHost.PreviewCamera.enabled);

                localFaultHeader = CreateRuntimeAiLabPreviewTestHeader(868f, 184f);
                localFaultHeader.RemoveChild(
                    localFaultHeader.GetChild("scenePreviewGuide"),
                    true);
                previewHost.Bind(
                    localFaultHeader,
                    new CharacterTestFairyGuiRuntimeAiPlannerDevPanelViewModel(),
                    true);
                Assert.AreEqual(
                    CharacterTestRuntimeAiLabPreviewState.Faulted,
                    previewHost.Result.State);
                Assert.AreEqual(
                    CharacterTestRuntimeAiLabBehaviorCardPreviewHost.SurfaceInvalidCode,
                    previewHost.Result.Code);
                Assert.IsTrue(localFaultHeader.GetChild("previewStateOverlay").visible);
                Assert.IsTrue(localFaultHeader.GetChild("btnPreviewRetry").visible);

                contractFaultHeader = CreateRuntimeAiLabPreviewTestHeader(868f, 184f);
                contractFaultHeader.RemoveChild(
                    contractFaultHeader.GetChild("previewStateTitle"),
                    true);
                previewHost.Bind(
                    contractFaultHeader,
                    new CharacterTestFairyGuiRuntimeAiPlannerDevPanelViewModel(),
                    true);
                Assert.IsTrue(previewHost.TryConsumeContractFault(
                    out string faultCode,
                    out string faultMessage));
                Assert.AreEqual(
                    CharacterTestRuntimeAiLabBehaviorCardPreviewHost.RootUnavailableCode,
                    faultCode);
                Assert.IsNotEmpty(faultMessage);
            }
            finally
            {
                previewHost?.Dispose();
                contractFaultHeader?.Dispose();
                localFaultHeader?.Dispose();
                secondHeader?.Dispose();
                firstHeader?.Dispose();
                CharacterTestRuntimeAiLabCameraResolver.NotifySceneCameraChanged(null);
                UnityEngine.Object.Destroy(sourceBHost);
                UnityEngine.Object.Destroy(sourceAHost);
            }

            yield return null;
            yield return new ExitPlayMode();
        }

        private static global::FairyGUI.GComponent CreateRuntimeAiLabPreviewTestHeader(
            float guideWidth,
            float guideHeight)
        {
            var header = new global::FairyGUI.GComponent
            {
                name = "RuntimeAiLabPreviewTestHeader"
            };
            header.SetSize(1888f, 250f);

            var guide = new global::FairyGUI.GGraph { name = "scenePreviewGuide" };
            guide.SetSize(guideWidth, guideHeight);
            header.AddChild(guide);

            var hint = new global::FairyGUI.GTextField { name = "scenePreviewHint" };
            header.AddChild(hint);
            var overlay = new global::FairyGUI.GGraph { name = "previewStateOverlay" };
            overlay.SetSize(guideWidth - 16f, guideHeight - 16f);
            header.AddChild(overlay);
            header.AddChild(new global::FairyGUI.GTextField { name = "previewStateTitle" });
            header.AddChild(new global::FairyGUI.GTextField { name = "previewStateDetail" });
            header.AddChild(new global::FairyGUI.GButton { name = "btnPreviewRetry" });
            global::FairyGUI.GRoot.inst.AddChild(header);
            return header;
        }

        [Test]
        public void RuntimeAiLabSessionBuilder_SetupModeUsesNonCapturingCamera()
        {
            var host = new GameObject("Runtime AI Lab Camera Mode Test");
            try
            {
                var context = new CharacterTestRuntimeContext(host.AddComponent<GameManager>(), null);
                var builder = new CharacterTestRuntimeSessionBuilder(
                    host.GetComponent<GameManager>(),
                    context,
                    CharacterTestSceneProfile.LoadDefault());
                MethodInfo resolve = typeof(CharacterTestRuntimeSessionBuilder).GetMethod(
                    "ResolveInitialCameraMode",
                    BindingFlags.Instance | BindingFlags.NonPublic);

                Assert.IsNotNull(resolve);
                var mode = (CharacterTestCameraMode)resolve.Invoke(
                    builder,
                    new object[] { CharacterTestArenaMatchConfig.CreateDefault(), true });

                Assert.AreEqual(CharacterTestCameraMode.MultiTargetAuto, mode);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(host);
            }
        }

        [Test]
        public void RuntimeAiLabViewModelSignature_IgnoresInactivePageData()
        {
            var viewModel = new CharacterTestFairyGuiRuntimeAiPlannerDevPanelViewModel
            {
                WorkspaceMode = CharacterTestRuntimeAiLabWorkspaceMode.Setup,
                Workspace = new CharacterTestRuntimeAiLabWorkspaceViewModel
                {
                    ActiveWorkspace = CharacterTestRuntimeAiLabWorkspaceMode.Setup,
                    ActiveDiagnosticsTab = CharacterTestRuntimeAiLabDiagnosticsTab.Facts,
                    ShowSetup = true
                },
                SetupValidationText = "setup-valid"
            };
            string before = CharacterTestFairyGuiRuntimeAiPlannerDevPanelViewModelSignature.Create(viewModel);

            viewModel.WorkbenchDetailText = "inactive-workbench-change";
            viewModel.BehaviorCardWorkspace.Timeline.RuntimeStatusText = "inactive-card-change";
            viewModel.TimelineStatusText = "inactive-timeline-change";
            viewModel.Facts.HeaderText = "inactive-facts-change";
            string afterInactiveChanges =
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelViewModelSignature.Create(viewModel);
            Assert.AreEqual(before, afterInactiveChanges);

            viewModel.SetupValidationText = "setup-invalid";
            string afterActiveChange =
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelViewModelSignature.Create(viewModel);
            Assert.AreNotEqual(before, afterActiveChange);
        }

        [Test]
        public void RuntimeAiLabViewModelSignature_TracksDraftSaveAvailability()
        {
            var viewModel = new CharacterTestFairyGuiRuntimeAiPlannerDevPanelViewModel
            {
                WorkspaceMode = CharacterTestRuntimeAiLabWorkspaceMode.BehaviorGroup,
                Workspace = new CharacterTestRuntimeAiLabWorkspaceViewModel
                {
                    ActiveWorkspace = CharacterTestRuntimeAiLabWorkspaceMode.BehaviorGroup
                },
                ShowWorkbench = true,
                CanSaveDraft = false
            };
            string saved = CharacterTestFairyGuiRuntimeAiPlannerDevPanelViewModelSignature.Create(
                viewModel);

            viewModel.CanSaveDraft = true;

            Assert.AreNotEqual(
                saved,
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelViewModelSignature.Create(viewModel));
        }

        [Test]
        public void RuntimeAiLabViewModelSignature_TracksOnlyActiveDiagnosticsProjection()
        {
            var viewModel = new CharacterTestFairyGuiRuntimeAiPlannerDevPanelViewModel
            {
                WorkspaceMode = CharacterTestRuntimeAiLabWorkspaceMode.Diagnostics,
                DiagnosticsTab = CharacterTestRuntimeAiLabDiagnosticsTab.Facts,
                Workspace = new CharacterTestRuntimeAiLabWorkspaceViewModel
                {
                    ActiveWorkspace = CharacterTestRuntimeAiLabWorkspaceMode.Diagnostics,
                    ActiveDiagnosticsTab = CharacterTestRuntimeAiLabDiagnosticsTab.Facts,
                    ShowDiagnostics = true
                }
            };
            string before = CharacterTestFairyGuiRuntimeAiPlannerDevPanelViewModelSignature.Create(viewModel);

            viewModel.TimelineStatusText = "inactive-runtime-frame-change";
            Assert.AreEqual(
                before,
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelViewModelSignature.Create(viewModel));

            viewModel.Facts.HeaderText = "active-facts-change";
            Assert.AreNotEqual(
                before,
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelViewModelSignature.Create(viewModel));
        }

        [Test]
        public void RuntimeAiLabViewModelSignature_TracksRuntimeFrameBranchConfirmation()
        {
            var viewModel = new CharacterTestFairyGuiRuntimeAiPlannerDevPanelViewModel
            {
                WorkspaceMode = CharacterTestRuntimeAiLabWorkspaceMode.Diagnostics,
                DiagnosticsTab = CharacterTestRuntimeAiLabDiagnosticsTab.RuntimeFrame,
                Workspace = new CharacterTestRuntimeAiLabWorkspaceViewModel
                {
                    ActiveWorkspace = CharacterTestRuntimeAiLabWorkspaceMode.Diagnostics,
                    ActiveDiagnosticsTab = CharacterTestRuntimeAiLabDiagnosticsTab.RuntimeFrame,
                    ShowDiagnostics = true
                },
                BranchRecomputeLabel = "从此重算",
                BranchRecomputeRiskText = string.Empty
            };
            string before = CharacterTestFairyGuiRuntimeAiPlannerDevPanelViewModelSignature.Create(viewModel);

            viewModel.BranchRecomputeSelected = true;
            viewModel.BranchRecomputeLabel = "确认重算";
            viewModel.BranchRecomputeRiskText = "基于帧 13，首次生成将删除 14…23。";

            Assert.AreNotEqual(
                before,
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelViewModelSignature.Create(viewModel));
        }

        [Test]
        public void RuntimeAiLabViewModelSignature_TracksActiveReplayValidationState()
        {
            var viewModel = new CharacterTestFairyGuiRuntimeAiPlannerDevPanelViewModel
            {
                WorkspaceMode = CharacterTestRuntimeAiLabWorkspaceMode.Diagnostics,
                DiagnosticsTab = CharacterTestRuntimeAiLabDiagnosticsTab.Replay,
                Workspace = new CharacterTestRuntimeAiLabWorkspaceViewModel
                {
                    ActiveWorkspace = CharacterTestRuntimeAiLabWorkspaceMode.Diagnostics,
                    ActiveDiagnosticsTab = CharacterTestRuntimeAiLabDiagnosticsTab.Replay,
                    ShowDiagnostics = true
                }
            };
            viewModel.Replay.State = CharacterTestReplayValidationState.Ready;
            viewModel.Replay.StateLabel = "Ready";
            viewModel.Replay.CanValidate = true;
            string ready = CharacterTestFairyGuiRuntimeAiPlannerDevPanelViewModelSignature.Create(
                viewModel);

            viewModel.Replay.State = CharacterTestReplayValidationState.Passed;
            viewModel.Replay.StateLabel = "Deterministic Passed";
            viewModel.Replay.CanValidate = false;
            viewModel.Replay.CanRetry = true;
            viewModel.Replay.Progress01 = 1f;
            viewModel.Replay.ResultEvidenceText = "20/20 authority frames matched";

            Assert.AreNotEqual(
                ready,
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelViewModelSignature.Create(viewModel));
        }
    }
}
