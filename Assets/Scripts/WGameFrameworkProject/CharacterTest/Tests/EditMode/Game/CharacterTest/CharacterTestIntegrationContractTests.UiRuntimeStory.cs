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
        public void FairyGuiStoryDialog_BuildsPresentationModelAndContinueCommandFromPendingStory()
        {
            CharacterTestStoryContent content = CharacterTestStoryFixture.CreateBootstrapContent();
            using var slice = new GameSlice(
                NullRuntimeLogger.Instance,
                content,
                resources: GlobalResourceRuntimeServices.Create(new GlobalResourceRuntimeOptions
                {
                    AllowMissingPreloadGroupInEditor = true
                }),
                baseResourcePreloadGroupId: string.Empty);

            Assert.IsTrue(CharacterTestFairyGuiStoryDialogViewModelBuilder.TryBuild(
                slice,
                content,
                out CharacterTestFairyGuiStoryDialogViewModel viewModel));
            Assert.AreEqual("Presentation waiting", viewModel.Phase);
            StringAssert.Contains(CharacterTestStoryIds.Steps.LoadBaseResources.ToString(), viewModel.DialogueText);

            CharacterTestFairyGuiStoryCommandDescriptor command = FindStoryDialogCommand(
                viewModel,
                CharacterTestFairyGuiStoryCommandIds.CompletePresentation);
            Assert.IsTrue(command.Enabled);

            var payload = (CharacterTestFairyGuiStoryCompletePresentationPayload)command.Payload;
            Assert.AreEqual(CharacterTestStoryIds.BootstrapGraph, payload.GraphId);
            Assert.Greater(payload.BeatInstanceId, 0);
            Assert.AreEqual(CharacterTestStoryIds.Steps.LoadBaseResources, payload.StepId);

            var sink = new CharacterTestFairyGuiStoryCommandSink(slice);
            sink.Enqueue(new MxUiCommand(
                CharacterTestFairyGuiStoryDialogIds.ViewId,
                command.CommandId,
                command.Payload));

            Assert.IsTrue(sink.LastResult.Success, sink.LastResult.Error.Message);
            Assert.AreEqual(StoryRuntimeCommandIds.CompletePresentation, sink.LastRuntimeCommand.CommandId);
            Assert.AreEqual(StoryRuntimeCommandSources.PresentationAdapter, sink.LastRuntimeCommand.SourceId);
            Assert.AreEqual(CharacterTestStoryIds.BootstrapGraph, sink.LastRuntimeCommand.TargetId);
            Assert.AreEqual(1, sink.AcceptedCount);
        }

[Test]
        public void FairyGuiStoryDialog_BuildsChoiceModelFromStoryChoiceReader()
        {
            var snapshot = new StoryDirectorSnapshot(
                schemaVersion: 1,
                nextBeatInstanceId: 8,
                graphs: Array.Empty<StoryGraphRuntimeSnapshot>(),
                activeBeatInstances: new[]
                {
                    new StoryBeatInstanceSnapshot(
                        CharacterTestStoryIds.BootstrapGraph,
                        CharacterTestStoryIds.Beats.Entry,
                        beatInstanceId: 7,
                        currentStepIndex: 3,
                        pendingPresentationStepId: 0,
                        pendingPresentationPolicy: StoryPresentationWaitPolicy.NoWait,
                        awaitingChoiceSetId: 99)
                },
                facts: Array.Empty<StoryFactEntry>());
            var choices = new FakeStoryChoiceReader(new[]
            {
                new StoryChoiceView(501, CharacterTestStoryIds.Text.WelcomeLine, true),
                new StoryChoiceView(502, 0, false)
            });

            Assert.IsTrue(CharacterTestFairyGuiStoryDialogViewModelBuilder.TryBuild(
                snapshot,
                choices,
                CharacterTestStoryFixture.CreateBootstrapContent(),
                out CharacterTestFairyGuiStoryDialogViewModel viewModel));

            Assert.AreEqual("Choice available", viewModel.Phase);
            Assert.AreEqual(2, viewModel.Choices.Count);
            Assert.AreEqual(CharacterTestStoryText.WelcomeLine, viewModel.Choices[0].Text);
            Assert.IsTrue(viewModel.Choices[0].Enabled);
            Assert.AreEqual("Choice 502", viewModel.Choices[1].Text);
            Assert.IsFalse(viewModel.Choices[1].Enabled);

            var payload = (CharacterTestFairyGuiStorySelectChoicePayload)viewModel.Choices[0].Command.Payload;
            Assert.AreEqual(CharacterTestStoryIds.BootstrapGraph, payload.GraphId);
            Assert.AreEqual(7, payload.BeatInstanceId);
            Assert.AreEqual(501, payload.ChoiceId);
        }

[Test]
        public void FairyGuiStoryDialog_ReturnsFalseWhenStoryNoLongerWaitsForPresentationOrChoice()
        {
            CharacterTestStoryContent content = CharacterTestStoryFixture.CreateBootstrapContent();
            using var slice = new GameSlice(
                NullRuntimeLogger.Instance,
                content,
                resources: GlobalResourceRuntimeServices.Create(new GlobalResourceRuntimeOptions
                {
                    AllowMissingPreloadGroupInEditor = true
                }),
                baseResourcePreloadGroupId: string.Empty);
            Assert.IsTrue(CharacterTestFairyGuiStoryDialogViewModelBuilder.TryBuild(
                slice,
                content,
                out CharacterTestFairyGuiStoryDialogViewModel pending));
            CharacterTestFairyGuiStoryCommandDescriptor command = FindStoryDialogCommand(
                pending,
                CharacterTestFairyGuiStoryCommandIds.CompletePresentation);
            var sink = new CharacterTestFairyGuiStoryCommandSink(slice);
            sink.Enqueue(new MxUiCommand(
                CharacterTestFairyGuiStoryDialogIds.ViewId,
                command.CommandId,
                command.Payload));
            Assert.IsTrue(sink.LastResult.Success, sink.LastResult.Error.Message);

            slice.Tick(0d);

            Assert.IsFalse(CharacterTestFairyGuiStoryDialogViewModelBuilder.TryBuild(
                slice,
                content,
                out CharacterTestFairyGuiStoryDialogViewModel idle));
            Assert.IsNull(idle);
        }

[Test]
        public void FairyGuiStoryDialogCommandSink_DuplicateContinueClickDoesNotQueueTwice()
        {
            CharacterTestStoryContent content = CharacterTestStoryFixture.CreateBootstrapContent();
            using var slice = new GameSlice(
                NullRuntimeLogger.Instance,
                content,
                resources: GlobalResourceRuntimeServices.Create(new GlobalResourceRuntimeOptions
                {
                    AllowMissingPreloadGroupInEditor = true
                }),
                baseResourcePreloadGroupId: string.Empty);
            Assert.IsTrue(CharacterTestFairyGuiStoryDialogViewModelBuilder.TryBuild(
                slice,
                content,
                out CharacterTestFairyGuiStoryDialogViewModel viewModel));
            CharacterTestFairyGuiStoryCommandDescriptor command = FindStoryDialogCommand(
                viewModel,
                CharacterTestFairyGuiStoryCommandIds.CompletePresentation);
            var sink = new CharacterTestFairyGuiStoryCommandSink(slice);
            int pendingBeforeClick = slice.StoryModule.CommandBuffer.PendingCount;

            sink.Enqueue(new MxUiCommand(
                CharacterTestFairyGuiStoryDialogIds.ViewId,
                command.CommandId,
                command.Payload));
            sink.Enqueue(new MxUiCommand(
                CharacterTestFairyGuiStoryDialogIds.ViewId,
                command.CommandId,
                command.Payload));

            Assert.IsTrue(sink.LastResult.Success, sink.LastResult.Error.Message);
            Assert.AreEqual(1, sink.AcceptedCount, "AcceptedCount");
            Assert.AreEqual(0, sink.RejectedCount, "RejectedCount");
            Assert.AreEqual(1, sink.DuplicateIgnoredCount, "DuplicateIgnoredCount");
            Assert.AreEqual(
                pendingBeforeClick + 1,
                slice.StoryModule.CommandBuffer.PendingCount,
                "PendingCount");
        }

[Test]
        public void FairyGuiStoryDialogHost_MissingPackageResourceDisablesUiWithoutBreakingSlice()
        {
            CharacterTestStoryContent content = CharacterTestStoryFixture.CreateBootstrapContent();
            var resourceManager = new ResourceManager();
            using var resources = new GlobalResourceRuntimeServices(
                GlobalResourceRuntimeMode.Editor,
                resourceManager,
                new ResourcePreloadService(resourceManager));
            using var slice = new GameSlice(
                NullRuntimeLogger.Instance,
                content,
                resources: resources,
                baseResourcePreloadGroupId: string.Empty);
            var hostObject = new GameObject("CharacterTest FairyGUI Missing Resource Host Test");
            try
            {
                CharacterTestFairyGuiStoryDialogHost host =
                    hostObject.AddComponent<CharacterTestFairyGuiStoryDialogHost>();

                host.Configure(slice, content, NullRuntimeLogger.Instance);

                Assert.IsFalse(host.IsConfigured);
                Assert.IsFalse(host.IsOpen);
                Assert.IsNotEmpty(host.LastStatusMessage);
                Assert.DoesNotThrow(() => slice.Tick(0d));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(hostObject);
            }
        }

[Test]
        public void FairyGuiStoryDialogHost_RefreshSkipsSameViewModelAndClosesWhenStoryIsIdle()
        {
            RemoveCharacterTestStoryDialogPackageIfLoaded();
            CharacterTestStoryContent content = CharacterTestStoryFixture.CreateBootstrapContent();
            using GlobalResourceRuntimeServices resources = CreateStoryDialogResourceServices();
            using var slice = new GameSlice(
                NullRuntimeLogger.Instance,
                content,
                resources,
                baseResourcePreloadGroupId: string.Empty);
            var cameraObject = new GameObject("CharacterTest FairyGUI Main Camera Test");
            var hostObject = new GameObject("CharacterTest FairyGUI Host Test");
            try
            {
                cameraObject.tag = "MainCamera";
                cameraObject.AddComponent<UnityEngine.Camera>();
                CharacterTestFairyGuiStoryDialogHost host =
                    hostObject.AddComponent<CharacterTestFairyGuiStoryDialogHost>();

                host.Configure(slice, content, NullRuntimeLogger.Instance);

                Assert.IsTrue(host.IsConfigured, host.LastStatusMessage);
                Assert.IsTrue(host.IsOpen, host.LastStatusMessage);
                Assert.AreEqual(1, host.SuccessfulRefreshCount);

                host.Refresh();

                Assert.IsTrue(host.IsOpen, host.LastStatusMessage);
                Assert.AreEqual(1, host.SuccessfulRefreshCount);
                Assert.AreEqual(1, host.SkippedRefreshCount);

                Assert.IsTrue(CharacterTestFairyGuiStoryDialogViewModelBuilder.TryBuild(
                    slice,
                    content,
                    out CharacterTestFairyGuiStoryDialogViewModel viewModel));
                CharacterTestFairyGuiStoryCommandDescriptor command = FindStoryDialogCommand(
                    viewModel,
                    CharacterTestFairyGuiStoryCommandIds.CompletePresentation);
                var sink = new CharacterTestFairyGuiStoryCommandSink(slice);
                sink.Enqueue(new MxUiCommand(
                    CharacterTestFairyGuiStoryDialogIds.ViewId,
                    command.CommandId,
                    command.Payload));
                Assert.IsTrue(sink.LastResult.Success, sink.LastResult.Error.Message);

                slice.Tick(0d);
                host.Refresh();

                Assert.IsFalse(host.IsOpen);

                host.Refresh();

                Assert.IsFalse(host.IsOpen);
                Assert.AreEqual(1, host.SuccessfulRefreshCount);
                Assert.AreEqual(2, host.SkippedRefreshCount);
            }
            finally
            {
                CharacterTestFairyGuiStoryDialogHost host =
                    hostObject.GetComponent<CharacterTestFairyGuiStoryDialogHost>();
                host?.Release();
                UnityEngine.Object.DestroyImmediate(hostObject);
                UnityEngine.Object.DestroyImmediate(cameraObject);
                DestroyObjectNamed("Stage Camera");
                RemoveCharacterTestStoryDialogPackageIfLoaded();
            }
        }

[Test]
        public void UiRuntime_Configure_EnablesDebugUiAndFairyGuiStoryDialog()
        {
            RemoveCharacterTestStoryDialogPackageIfLoaded();
            CharacterTestStoryContent content = CharacterTestStoryFixture.CreateBootstrapContent();
            using GlobalResourceRuntimeServices resources = CreateStoryDialogResourceServices();
            using var slice = new GameSlice(
                NullRuntimeLogger.Instance,
                content,
                resources,
                baseResourcePreloadGroupId: string.Empty);
            var cameraObject = new GameObject("CharacterTest UI Runtime Main Camera Test");
            var hostObject = new GameObject("CharacterTest UI Runtime Host Test");
            CharacterTestUiRuntime runtime = null;
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
                    StoryContent = content,
                    IsPausedProvider = () => false,
                    EnableDebugUi = true,
                    DebugInitialVisibility = DebugUiVisibility.Hidden,
                    DebugShowOnStart = false,
                    EnableFairyGuiCombatHud = false,
                    EnableFairyGuiStoryUi = true
                });

                Assert.IsNotNull(runtime.DebugBootstrap);
                Assert.IsNotNull(hostObject.GetComponent<CharacterTestDebugUiHost>());
                Assert.IsNotNull(runtime.FairyGuiStoryDialogHost);
                Assert.AreSame(
                    hostObject.GetComponent<CharacterTestFairyGuiStoryDialogHost>(),
                    runtime.FairyGuiStoryDialogHost);
                Assert.IsTrue(runtime.IsFairyGuiStoryUiConfigured, runtime.LastFairyGuiStoryUiStatus);
            }
            finally
            {
                runtime?.Release();
                UnityEngine.Object.DestroyImmediate(hostObject);
                UnityEngine.Object.DestroyImmediate(cameraObject);
                DestroyObjectNamed("Stage Camera");
                RemoveCharacterTestStoryDialogPackageIfLoaded();
            }
        }

[Test]
        public void CharacterTestUiRuntime_ModalPanelRefreshScheduler_SkipsClosedAndThrottlesOpenPanels()
        {
            int lastRefreshFrame = int.MinValue;

            Assert.IsTrue(CharacterTestUiRuntime.ShouldRefreshModalPanel(
                100,
                isOpen: false,
                refreshAttemptCount: 0,
                ref lastRefreshFrame,
                refreshIntervalFrames: 6));
            Assert.AreEqual(100, lastRefreshFrame);

            Assert.IsFalse(CharacterTestUiRuntime.ShouldRefreshModalPanel(
                101,
                isOpen: false,
                refreshAttemptCount: 1,
                ref lastRefreshFrame,
                refreshIntervalFrames: 6));
            Assert.AreEqual(101, lastRefreshFrame);

            Assert.IsFalse(CharacterTestUiRuntime.ShouldRefreshModalPanel(
                102,
                isOpen: true,
                refreshAttemptCount: 2,
                ref lastRefreshFrame,
                refreshIntervalFrames: 6));
            Assert.AreEqual(101, lastRefreshFrame);

            Assert.IsTrue(CharacterTestUiRuntime.ShouldRefreshModalPanel(
                107,
                isOpen: true,
                refreshAttemptCount: 2,
                ref lastRefreshFrame,
                refreshIntervalFrames: 6));
            Assert.AreEqual(107, lastRefreshFrame);

            Assert.IsFalse(CharacterTestUiRuntime.ShouldRefreshModalPanel(
                108,
                isOpen: true,
                refreshAttemptCount: 3,
                ref lastRefreshFrame,
                refreshIntervalFrames: 6));
            Assert.AreEqual(107, lastRefreshFrame);
        }

[Test]
        public void CharacterTestUiRuntime_PlayerModalSurfaceClearGate_OnlyRunsOnModalEntry()
        {
            bool lastPlayerModalOpen = false;

            Assert.IsTrue(CharacterTestUiRuntime.ShouldClearPlayerSurfacesForModal(
                isPlayerModalOpen: true,
                ref lastPlayerModalOpen));
            Assert.IsTrue(lastPlayerModalOpen);

            Assert.IsFalse(CharacterTestUiRuntime.ShouldClearPlayerSurfacesForModal(
                isPlayerModalOpen: true,
                ref lastPlayerModalOpen));
            Assert.IsTrue(lastPlayerModalOpen);

            Assert.IsFalse(CharacterTestUiRuntime.ShouldClearPlayerSurfacesForModal(
                isPlayerModalOpen: false,
                ref lastPlayerModalOpen));
            Assert.IsFalse(lastPlayerModalOpen);

            Assert.IsTrue(CharacterTestUiRuntime.ShouldClearPlayerSurfacesForModal(
                isPlayerModalOpen: true,
                ref lastPlayerModalOpen));
            Assert.IsTrue(lastPlayerModalOpen);
        }

        [Test]
        public void CharacterTestUiSurfacePolicy_ClosesOtherLargePlayerSurfacesAndKeepsStoryDialog()
        {
            CharacterTestUiSurfaceMask state = CharacterTestUiSurfaceMask.None;
            state = CharacterTestUiSurfacePolicy.Add(state, CharacterTestUiSurface.MainMenu);
            state = CharacterTestUiSurfacePolicy.Add(state, CharacterTestUiSurface.NetworkPanel);
            state = CharacterTestUiSurfacePolicy.Add(state, CharacterTestUiSurface.SettingsPanel);
            state = CharacterTestUiSurfacePolicy.Add(state, CharacterTestUiSurface.AbilityBindingPanel);
            state = CharacterTestUiSurfacePolicy.Add(
                state,
                CharacterTestUiSurface.RuntimeAiBehaviorLoadoutPanel);
            state = CharacterTestUiSurfacePolicy.Add(state, CharacterTestUiSurface.ArenaSetupPanel);
            state = CharacterTestUiSurfacePolicy.Add(state, CharacterTestUiSurface.ArenaBehaviorGroupPanel);
            state = CharacterTestUiSurfacePolicy.Add(state, CharacterTestUiSurface.StoryDialog);

            CharacterTestUiSurfaceMask closeForSettings =
                CharacterTestUiSurfacePolicy.GetConflictingSurfacesToClose(
                    CharacterTestUiSurface.SettingsPanel,
                    state);

            Assert.IsTrue((closeForSettings & CharacterTestUiSurfaceMask.MainMenu) != 0);
            Assert.IsTrue((closeForSettings & CharacterTestUiSurfaceMask.NetworkPanel) != 0);
            Assert.IsFalse((closeForSettings & CharacterTestUiSurfaceMask.SettingsPanel) != 0);
            Assert.IsTrue((closeForSettings & CharacterTestUiSurfaceMask.AbilityBindingPanel) != 0);
            Assert.IsTrue((closeForSettings & CharacterTestUiSurfaceMask.RuntimeAiBehaviorLoadoutPanel) != 0);
            Assert.IsTrue((closeForSettings & CharacterTestUiSurfaceMask.ArenaSetupPanel) != 0);
            Assert.IsTrue((closeForSettings & CharacterTestUiSurfaceMask.ArenaBehaviorGroupPanel) != 0);
            Assert.IsFalse((closeForSettings & CharacterTestUiSurfaceMask.StoryDialog) != 0);

            Assert.AreEqual(
                CharacterTestUiSurfaceMask.None,
                CharacterTestUiSurfacePolicy.GetConflictingSurfacesToClose(
                    CharacterTestUiSurface.StoryDialog,
                    state));
        }

        [Test]
        public void CharacterTestUiSurfacePolicy_PlayerModalAndStorySuppressionMatchLegacySemantics()
        {
            CharacterTestUiSurfaceMask storyOnly =
                CharacterTestUiSurfacePolicy.Add(
                    CharacterTestUiSurfaceMask.None,
                    CharacterTestUiSurface.StoryDialog);
            CharacterTestUiSurfaceMask mainOnly =
                CharacterTestUiSurfacePolicy.Add(
                    CharacterTestUiSurfaceMask.None,
                    CharacterTestUiSurface.MainMenu);
            CharacterTestUiSurfaceMask networkOnly =
                CharacterTestUiSurfacePolicy.Add(
                    CharacterTestUiSurfaceMask.None,
                    CharacterTestUiSurface.NetworkPanel);

            Assert.IsFalse(CharacterTestUiSurfacePolicy.HasPlayerModalOpen(CharacterTestUiSurfaceMask.None));
            Assert.IsTrue(CharacterTestUiSurfacePolicy.HasPlayerModalOpen(storyOnly));
            Assert.IsTrue(CharacterTestUiSurfacePolicy.HasNonMainMenuPlayerModalOpen(storyOnly));
            Assert.IsFalse(CharacterTestUiSurfacePolicy.IsStoryDialogSuppressed(storyOnly));

            Assert.IsTrue(CharacterTestUiSurfacePolicy.HasPlayerModalOpen(mainOnly));
            Assert.IsFalse(CharacterTestUiSurfacePolicy.HasNonMainMenuPlayerModalOpen(mainOnly));
            Assert.IsTrue(CharacterTestUiSurfacePolicy.IsStoryDialogSuppressed(mainOnly));

            Assert.IsTrue(CharacterTestUiSurfacePolicy.HasPlayerModalOpen(networkOnly));
            Assert.IsTrue(CharacterTestUiSurfacePolicy.HasNonMainMenuPlayerModalOpen(networkOnly));
            Assert.IsTrue(CharacterTestUiSurfacePolicy.IsStoryDialogSuppressed(networkOnly));
        }

        [Test]
        public void CharacterTestUiSurfacePolicy_TacticalPreparationSuppressesNarrativeAndGameplayHud()
        {
            CharacterTestUiSurfaceMask tactical = CharacterTestUiSurfacePolicy.Add(
                CharacterTestUiSurfaceMask.None,
                CharacterTestUiSurface.TacticalPreparationPanel);

            Assert.IsTrue(CharacterTestUiSurfacePolicy.HasPlayerModalOpen(tactical));
            Assert.IsTrue(CharacterTestUiSurfacePolicy.HasNonMainMenuPlayerModalOpen(tactical));
            Assert.IsTrue(CharacterTestUiSurfacePolicy.IsStoryDialogSuppressed(tactical));
            Assert.IsTrue(CharacterTestUiSurfacePolicy.ShouldClearGameplayPresentation(tactical));
            Assert.AreEqual(
                CharacterTestUiSurfaceKind.PrimaryPanel,
                CharacterTestUiSurfacePolicy.GetDescriptor(CharacterTestUiSurface.TacticalPreparationPanel).Kind);
        }

        [Test]
        public void CharacterTestUiSurfacePolicy_LightOverlayDoesNotBecomePlayerModal()
        {
            CharacterTestUiSurfaceMask state = CharacterTestUiSurfaceMask.None;
            state = CharacterTestUiSurfacePolicy.Add(state, CharacterTestUiSurface.CombatHud);
            state = CharacterTestUiSurfacePolicy.Add(state, CharacterTestUiSurface.FloatingCombatText);
            state = CharacterTestUiSurfacePolicy.Add(state, CharacterTestUiSurface.Guidance);

            CharacterTestUiSurfaceDescriptor hud =
                CharacterTestUiSurfacePolicy.GetDescriptor(CharacterTestUiSurface.CombatHud);
            CharacterTestUiSurfaceDescriptor floatingText =
                CharacterTestUiSurfacePolicy.GetDescriptor(CharacterTestUiSurface.FloatingCombatText);
            CharacterTestUiSurfaceDescriptor guidance =
                CharacterTestUiSurfacePolicy.GetDescriptor(CharacterTestUiSurface.Guidance);

            Assert.AreEqual(CharacterTestUiSurfaceKind.Hud, hud.Kind);
            Assert.AreEqual(CharacterTestUiSurfaceKind.LightOverlay, floatingText.Kind);
            Assert.AreEqual(CharacterTestUiSurfaceKind.LightOverlay, guidance.Kind);
            Assert.AreEqual(CharacterTestUiSurfaceTraits.None, guidance.Traits);
            Assert.IsFalse(CharacterTestUiSurfacePolicy.HasPlayerModalOpen(state));
            Assert.IsFalse(CharacterTestUiSurfacePolicy.ShouldClearGameplayPresentation(state));
            Assert.AreEqual(
                CharacterTestUiSurfaceMask.None,
                CharacterTestUiSurfacePolicy.GetConflictingSurfacesToClose(
                    CharacterTestUiSurface.Guidance,
                    state));
        }

        [Test]
        public void CharacterTestUiSurfacePolicy_RunningArenaStatusDoesNotHideGameplayPresentation()
        {
            CharacterTestUiSurfaceMask arenaOnly = CharacterTestUiSurfacePolicy.Add(
                CharacterTestUiSurfaceMask.None,
                CharacterTestUiSurface.ArenaSetupPanel);

            Assert.IsTrue(CharacterTestUiSurfacePolicy.ShouldClearGameplayPresentation(
                arenaOnly,
                arenaSetupPanelBlocksGameplayPresentation: true));
            Assert.IsFalse(CharacterTestUiSurfacePolicy.ShouldClearGameplayPresentation(
                arenaOnly,
                arenaSetupPanelBlocksGameplayPresentation: false));

            CharacterTestUiSurfaceMask arenaAndSettings = CharacterTestUiSurfacePolicy.Add(
                arenaOnly,
                CharacterTestUiSurface.SettingsPanel);
            Assert.IsTrue(CharacterTestUiSurfacePolicy.ShouldClearGameplayPresentation(
                arenaAndSettings,
                arenaSetupPanelBlocksGameplayPresentation: false));
        }

        [Test]
        public void CharacterTestUiSurfacePolicy_CancelTargetPrefersTopOpenSurface()
        {
            CharacterTestUiSurfaceMask state = CharacterTestUiSurfaceMask.None;
            state = CharacterTestUiSurfacePolicy.Add(state, CharacterTestUiSurface.MainMenu);
            state = CharacterTestUiSurfacePolicy.Add(state, CharacterTestUiSurface.SettingsPanel);
            state = CharacterTestUiSurfacePolicy.Add(state, CharacterTestUiSurface.FloatingCombatText);

            Assert.IsTrue(CharacterTestUiSurfacePolicy.TryGetCancelTarget(
                state,
                CharacterTestUiSurface.SettingsPanel,
                out CharacterTestUiSurface target));
            Assert.AreEqual(CharacterTestUiSurface.SettingsPanel, target);

            Assert.IsTrue(CharacterTestUiSurfacePolicy.TryGetCancelTarget(
                state,
                CharacterTestUiSurface.FloatingCombatText,
                out target));
            Assert.AreEqual(CharacterTestUiSurface.SettingsPanel, target);
        }

        [Test]
        public void CharacterTestUiSurfacePolicy_StoryDialogRequiresAnExplicitChoiceOrContinue()
        {
            CharacterTestUiSurfaceMask state = CharacterTestUiSurfacePolicy.Add(
                CharacterTestUiSurfaceMask.None,
                CharacterTestUiSurface.StoryDialog);

            Assert.IsFalse(CharacterTestUiSurfacePolicy.TryGetCancelTarget(
                state,
                CharacterTestUiSurface.StoryDialog,
                out _));
            Assert.AreEqual(
                CharacterTestUiSurfaceCancelBehavior.None,
                CharacterTestUiSurfacePolicy.GetDescriptor(CharacterTestUiSurface.StoryDialog).CancelBehavior);
        }

        [Test]
        public void CharacterTestUiRuntime_Configure_EnablesSettingsPanelWhenSettingsRuntimeProvided()
        {
            ResetFairyGuiTestState();
            CharacterTestStoryContent content = CharacterTestStoryFixture.CreateBootstrapContent();
            Texture2D combatHudAtlas = null;
            GlobalResourceRuntimeServices resources = null;
            GameSlice slice = null;
            var cameraObject = new GameObject("CharacterTest UI Runtime Settings Camera Test");
            var hostObject = new GameObject("CharacterTest UI Runtime Settings Host Test");
            CharacterTestUiRuntime runtime = null;
            var input = new TrackingInputProvider(InputSnapshot.Empty);
            var cursorDriver = new RecordingCursorDriver();
            var cursorController = new MxCursorController(cursorDriver);
            IDisposable cameraCursorScope = null;
            try
            {
                resources = CreateSettingsPanelResourceServices(out combatHudAtlas);
                slice = new GameSlice(
                    NullRuntimeLogger.Instance,
                    content,
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
                CharacterTestThirdPersonCameraRigBridge owner =
                    hostObject.AddComponent<CharacterTestThirdPersonCameraRigBridge>();
                runtime = new CharacterTestUiRuntime(owner, NullRuntimeLogger.Instance);
                var mainMenuController = new RecordingMainMenuController();
                var networkController = new RecordingNetworkSessionController();
                cameraCursorScope = cursorController.Push(MxCursorRequest.Captured("test.camera"));

                runtime.Configure(new CharacterTestUiRuntimeOptions
                {
                    Slice = slice,
                    StoryContent = content,
                    IsPausedProvider = () => false,
                    EnableDebugUi = false,
                    EnableFairyGuiCombatHud = false,
                    EnableFairyGuiFloatingCombatText = false,
                    EnableFairyGuiStoryUi = false,
                    EnableFairyGuiMainMenu = true,
                    AutoOpenMainMenu = false,
                    EnableFairyGuiNetworkPanel = true,
                    AutoOpenNetworkPanel = false,
                    EnableFairyGuiSettingsPanel = true,
                    EnableFairyGuiAbilityBindingPanel = false,
                    EnableFairyGuiRuntimeAiBehaviorLoadoutPanel = false,
                    MainMenuController = mainMenuController,
                    NetworkSessionController = networkController,
                    SettingsRuntime = settingsRuntime,
                    LocalizationRuntime = localizationRuntime,
                    InputProvider = input,
                    CursorController = cursorController
                });

                Assert.IsNotNull(runtime.FairyGuiSettingsPanelHost);
                Assert.IsNotNull(runtime.FairyGuiMainMenuHost);
                Assert.IsNotNull(runtime.FairyGuiNetworkPanelHost);
                var runtimeShell = (MxFairyGuiRuntimeShell)typeof(CharacterTestUiRuntime)
                    .GetField("_fairyGuiRuntimeShell", BindingFlags.Instance | BindingFlags.NonPublic)
                    .GetValue(runtime);
                Assert.IsNotNull(runtimeShell);
                MxUiViewId[] requiredViews =
                {
                    CharacterTestFairyGuiMainMenuIds.ViewId,
                    CharacterTestFairyGuiNetworkPanelIds.ViewId,
                    CharacterTestFairyGuiSettingsPanelIds.ViewId
                };
                foreach (MxUiViewId requiredView in requiredViews)
                {
                    Assert.AreEqual(1, runtimeShell.Catalog.Registrations.Count(
                        registration => registration.ViewId.Equals(requiredView)),
                        "The enabled player panel must be registered exactly once: " + requiredView);
                }
                Assert.AreSame(
                    hostObject.GetComponent<CharacterTestFairyGuiSettingsPanelHost>(),
                    runtime.FairyGuiSettingsPanelHost);
                Assert.IsTrue(runtime.IsFairyGuiSettingsPanelConfigured, runtime.LastFairyGuiSettingsPanelStatus);
                Assert.IsFalse(runtime.FairyGuiSettingsPanelHost.IsOpen);
                Assert.IsTrue(runtime.FairyGuiMainMenuHost.IsConfigured, runtime.LastFairyGuiMainMenuStatus);
                Assert.NotNull(runtime.FairyGuiMainMenuHost.CommandSink);
                Assert.AreEqual(0, input.ActiveScopeCount);
                Assert.AreEqual(MxCursorMode.Captured, cursorController.Current.Mode);
                Assert.AreEqual("test.camera", cursorController.Current.Owner);

                runtime.Refresh();
                Assert.AreEqual(1, runtime.FairyGuiMainMenuHost.RefreshAttemptCount);
                Assert.AreEqual(1, runtime.FairyGuiNetworkPanelHost.RefreshAttemptCount);
                Assert.AreEqual(1, runtime.FairyGuiSettingsPanelHost.RefreshAttemptCount);

                runtime.Refresh();
                Assert.AreEqual(1, runtime.FairyGuiMainMenuHost.RefreshAttemptCount);
                Assert.AreEqual(1, runtime.FairyGuiNetworkPanelHost.RefreshAttemptCount);
                Assert.AreEqual(1, runtime.FairyGuiSettingsPanelHost.RefreshAttemptCount);

                Assert.IsTrue(
                    runtime.OpenMainMenu(),
                    runtime.FairyGuiMainMenuHost.LastStatusMessage);
                runtime.FairyGuiMainMenuHost.CommandSink.Enqueue(new MxUiCommand(
                    CharacterTestFairyGuiMainMenuIds.ViewId,
                    CharacterTestFairyGuiMainMenuCommandIds.SettingsToggle,
                    null));
                Assert.IsTrue(runtime.FairyGuiSettingsPanelHost.IsOpen, runtime.LastFairyGuiSettingsPanelStatus);
                Assert.IsTrue(rebinding.IsEditSessionActive);
                Assert.AreEqual(1, rebinding.BeginEditSessionCount);
                Assert.IsTrue(runtime.BlocksTacticalBattleInput);
                Assert.AreEqual(1, runtime.FairyGuiMainMenuHost.CommandSink.SettingsToggleCount);
                Assert.AreEqual(InputContext.UI, input.CurrentContext);
                Assert.AreEqual(1, input.ActiveScopeCount);
                Assert.AreEqual(MxCursorMode.Released, cursorController.Current.Mode);
                Assert.AreEqual("CharacterTest.UI.Modal", cursorController.Current.Owner);
                Assert.IsTrue(cursorDriver.Visible);
                Assert.AreEqual(CursorLockMode.None, cursorDriver.LockState);

                input.Commands.Enqueue(new InputCommand(
                    0L,
                    1,
                    InputIntent.Cancel,
                    InputCommandPhase.Pressed));
                runtime.ProcessInput();
                Assert.IsFalse(runtime.FairyGuiSettingsPanelHost.IsOpen);
                Assert.IsFalse(rebinding.IsEditSessionActive);
                Assert.AreEqual(1, rebinding.RollbackEditSessionCount);
                Assert.IsTrue(
                    runtime.FairyGuiMainMenuHost.IsOpen,
                    runtime.FairyGuiMainMenuHost.LastStatusMessage);

                runtime.ProcessInput();
                Assert.IsTrue(
                    runtime.FairyGuiMainMenuHost.IsOpen,
                    "A non-destructive peek must not process the same Cancel sequence twice.");

                Assert.IsTrue(runtime.FairyGuiMainMenuHost.Close());
                Assert.IsTrue(runtime.BlocksTacticalBattleInput,
                    "Closing the last modal must retain its input ownership for this frame.");
                Assert.IsTrue(runtime.OwnsTacticalCancelInput);
                Assert.AreEqual(0, input.ActiveScopeCount);
                Assert.AreEqual(MxCursorMode.Captured, cursorController.Current.Mode);
                Assert.AreEqual("test.camera", cursorController.Current.Owner);
            }
            finally
            {
                runtime?.Release();
                cameraCursorScope?.Dispose();
                cursorController.Dispose();
                slice?.Dispose();
                resources?.Dispose();
                UnityEngine.Object.DestroyImmediate(hostObject);
                UnityEngine.Object.DestroyImmediate(cameraObject);
                DestroyObjectNamed("Stage Camera");
                if (combatHudAtlas != null)
                    UnityEngine.Object.DestroyImmediate(combatHudAtlas);
                ResetFairyGuiTestState();
            }
        }

[Test]
        public void CharacterTestUiRuntime_Configure_MissingSettingsRuntimeDisablesPanelWithDiagnostic()
        {
            CharacterTestStoryContent content = CharacterTestStoryFixture.CreateBootstrapContent();
            using var slice = new GameSlice(
                NullRuntimeLogger.Instance,
                content,
                resources: GlobalResourceRuntimeServices.Create(new GlobalResourceRuntimeOptions
                {
                    AllowMissingPreloadGroupInEditor = true
                }),
                baseResourcePreloadGroupId: string.Empty);
            var hostObject = new GameObject("CharacterTest UI Runtime Missing Settings Host Test");
            CharacterTestUiRuntime runtime = null;
            try
            {
                CharacterTestThirdPersonCameraRigBridge owner =
                    hostObject.AddComponent<CharacterTestThirdPersonCameraRigBridge>();
                runtime = new CharacterTestUiRuntime(owner, NullRuntimeLogger.Instance);

                runtime.Configure(new CharacterTestUiRuntimeOptions
                {
                    Slice = slice,
                    StoryContent = content,
                    IsPausedProvider = () => false,
                    EnableDebugUi = false,
                    EnableFairyGuiCombatHud = false,
                    EnableFairyGuiFloatingCombatText = false,
                    EnableFairyGuiStoryUi = false,
                    EnableFairyGuiNetworkPanel = false,
                    EnableFairyGuiSettingsPanel = true,
                    SettingsRuntime = null
                });

                Assert.IsNull(runtime.FairyGuiSettingsPanelHost);
                Assert.IsNull(hostObject.GetComponent<CharacterTestFairyGuiSettingsPanelHost>());
                Assert.IsFalse(runtime.IsFairyGuiSettingsPanelConfigured);
                StringAssert.Contains("settings runtime is not available", runtime.LastFairyGuiSettingsPanelStatus);
            }
            finally
            {
                runtime?.Release();
                UnityEngine.Object.DestroyImmediate(hostObject);
            }
        }

[Test]
        public void UiRuntime_Configure_WhenDebugUiDisabled_DoesNotCreateDebugHostButKeepsStoryDialog()
        {
            RemoveCharacterTestStoryDialogPackageIfLoaded();
            CharacterTestStoryContent content = CharacterTestStoryFixture.CreateBootstrapContent();
            using GlobalResourceRuntimeServices resources = CreateStoryDialogResourceServices();
            using var slice = new GameSlice(
                NullRuntimeLogger.Instance,
                content,
                resources,
                baseResourcePreloadGroupId: string.Empty);
            var cameraObject = new GameObject("CharacterTest UI Runtime Debug Disabled Camera Test");
            var hostObject = new GameObject("CharacterTest UI Runtime Debug Disabled Host Test");
            CharacterTestUiRuntime runtime = null;
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
                    StoryContent = content,
                    IsPausedProvider = () => false,
                    EnableDebugUi = false,
                    EnableFairyGuiCombatHud = false,
                    EnableFairyGuiStoryUi = true
                });

                Assert.IsNotNull(runtime.DebugBootstrap);
                Assert.IsNull(hostObject.GetComponent<CharacterTestDebugUiHost>());
                Assert.IsNotNull(runtime.FairyGuiStoryDialogHost);
                Assert.IsTrue(runtime.IsFairyGuiStoryUiConfigured, runtime.LastFairyGuiStoryUiStatus);
            }
            finally
            {
                runtime?.Release();
                UnityEngine.Object.DestroyImmediate(hostObject);
                UnityEngine.Object.DestroyImmediate(cameraObject);
                DestroyObjectNamed("Stage Camera");
                RemoveCharacterTestStoryDialogPackageIfLoaded();
            }
        }

[Test]
        public void UiRuntime_Configure_WhenFairyGuiCombatHudDisabled_DoesNotCreateHudButKeepsDebugAndStoryDialog()
        {
            RemoveCharacterTestStoryDialogPackageIfLoaded();
            CharacterTestStoryContent content = CharacterTestStoryFixture.CreateBootstrapContent();
            using GlobalResourceRuntimeServices resources = CreateStoryDialogResourceServices();
            using var slice = new GameSlice(
                NullRuntimeLogger.Instance,
                content,
                resources,
                baseResourcePreloadGroupId: string.Empty);
            var cameraObject = new GameObject("CharacterTest UI Runtime Combat HUD Disabled Camera Test");
            var hostObject = new GameObject("CharacterTest UI Runtime Combat HUD Disabled Host Test");
            CharacterTestUiRuntime runtime = null;
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
                    StoryContent = content,
                    IsPausedProvider = () => false,
                    EnableDebugUi = true,
                    DebugInitialVisibility = DebugUiVisibility.Hidden,
                    DebugShowOnStart = false,
                    EnableFairyGuiCombatHud = false,
                    EnableFairyGuiStoryUi = true
                });

                Assert.IsNotNull(runtime.DebugBootstrap);
                Assert.IsNotNull(hostObject.GetComponent<CharacterTestDebugUiHost>());
                Assert.IsNull(runtime.FairyGuiCombatHudHost);
                Assert.IsNull(hostObject.GetComponent<CharacterTestFairyGuiCombatHudHost>());
                Assert.IsFalse(runtime.IsFairyGuiCombatHudConfigured);
                Assert.AreEqual(string.Empty, runtime.LastFairyGuiCombatHudStatus);
                Assert.IsNotNull(runtime.FairyGuiStoryDialogHost);
                Assert.IsTrue(runtime.IsFairyGuiStoryUiConfigured, runtime.LastFairyGuiStoryUiStatus);
            }
            finally
            {
                runtime?.Release();
                UnityEngine.Object.DestroyImmediate(hostObject);
                UnityEngine.Object.DestroyImmediate(cameraObject);
                DestroyObjectNamed("Stage Camera");
                RemoveCharacterTestStoryDialogPackageIfLoaded();
            }
        }

[Test]
        public void UiRuntime_Configure_WhenFairyGuiStoryUiDisabled_DoesNotCreateStoryHostButKeepsDebugUi()
        {
            CharacterTestStoryContent content = CharacterTestStoryFixture.CreateBootstrapContent();
            using var slice = new GameSlice(
                NullRuntimeLogger.Instance,
                content,
                resources: GlobalResourceRuntimeServices.Create(new GlobalResourceRuntimeOptions
                {
                    AllowMissingPreloadGroupInEditor = true
                }),
                baseResourcePreloadGroupId: string.Empty);
            var hostObject = new GameObject("CharacterTest UI Runtime Story Disabled Host Test");
            CharacterTestUiRuntime runtime = null;
            try
            {
                CharacterTestThirdPersonCameraRigBridge owner =
                    hostObject.AddComponent<CharacterTestThirdPersonCameraRigBridge>();
                runtime = new CharacterTestUiRuntime(owner, NullRuntimeLogger.Instance);

                runtime.Configure(new CharacterTestUiRuntimeOptions
                {
                    Slice = slice,
                    StoryContent = content,
                    IsPausedProvider = () => false,
                    EnableDebugUi = true,
                    DebugInitialVisibility = DebugUiVisibility.Hidden,
                    DebugShowOnStart = false,
                    EnableFairyGuiCombatHud = false,
                    EnableFairyGuiStoryUi = false
                });

                Assert.IsNotNull(runtime.DebugBootstrap);
                Assert.IsNotNull(hostObject.GetComponent<CharacterTestDebugUiHost>());
                Assert.IsNull(runtime.FairyGuiStoryDialogHost);
                Assert.IsNull(hostObject.GetComponent<CharacterTestFairyGuiStoryDialogHost>());
                Assert.IsFalse(runtime.IsFairyGuiStoryUiConfigured);
                Assert.AreEqual(string.Empty, runtime.LastFairyGuiStoryUiStatus);
            }
            finally
            {
                runtime?.Release();
                UnityEngine.Object.DestroyImmediate(hostObject);
            }
        }

[Test]
        public void CharacterTestUiRuntime_Configure_PreloadsEnabledFairyGuiPackagesOnce()
        {
            ResetFairyGuiTestState();
            CharacterTestStoryContent content = CharacterTestStoryFixture.CreateBootstrapContent();
            Texture2D combatHudAtlas = null;
            Texture2D mainMenuAtlas = null;
            GlobalResourceRuntimeServices resources = null;
            GameSlice slice = null;
            var cameraObject = new GameObject("CharacterTest UI Runtime Preload Camera Test");
            var hostObject = new GameObject("CharacterTest UI Runtime Preload Host Test");
            CharacterTestUiRuntime runtime = null;
            try
            {
                resources = CreateCharacterTestUiResourceServices(out combatHudAtlas, out mainMenuAtlas);
                slice = new GameSlice(
                    NullRuntimeLogger.Instance,
                    content,
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
                    StoryContent = content,
                    IsPausedProvider = () => false,
                    EnableDebugUi = false,
                    EnableFairyGuiCombatHud = true,
                    EnableFairyGuiFloatingCombatText = false,
                    EnableFairyGuiStoryUi = true,
                    EnableFairyGuiMainMenu = true,
                    AutoOpenMainMenu = false,
                    EnableFairyGuiNetworkPanel = false,
                    EnableFairyGuiSettingsPanel = false,
                    EnableFairyGuiAbilityBindingPanel = false,
                    MainMenuController = new RecordingMainMenuController()
                });

                Assert.AreEqual(4, runtime.FairyGuiPackagePreloadAttemptCountForTests);
                Assert.AreEqual(4, runtime.FairyGuiPackagePreloadSuccessCountForTests);
                Assert.AreEqual(4, runtime.FairyGuiPreloadedPackageCountForTests);
                StringAssert.Contains(
                    "Preloaded FairyGUI packages=4/4",
                    runtime.LastFairyGuiPackagePreloadStatusForTests);

                runtime.Release();

                Assert.AreEqual(0, runtime.FairyGuiPreloadedPackageCountForTests);
            }
            finally
            {
                runtime?.Release();
                slice?.Dispose();
                resources?.Dispose();
                UnityEngine.Object.DestroyImmediate(hostObject);
                UnityEngine.Object.DestroyImmediate(cameraObject);
                DestroyObjectNamed("Stage Camera");
                if (combatHudAtlas != null)
                    UnityEngine.Object.DestroyImmediate(combatHudAtlas);
                if (mainMenuAtlas != null)
                    UnityEngine.Object.DestroyImmediate(mainMenuAtlas);
                RemoveCharacterTestStoryDialogPackageIfLoaded();
                RemoveCharacterTestCombatHudPackageIfLoaded();
                RemoveCharacterTestMainMenuPackageIfLoaded();
            }
        }

[Test]
        public void UiRuntime_RefreshForwardsToCombatHudAndStoryDialogAndReleaseIsIdempotent()
        {
            RemoveCharacterTestStoryDialogPackageIfLoaded();
            RemoveCharacterTestCombatHudPackageIfLoaded();
            RemoveCharacterTestMainMenuPackageIfLoaded();
            CharacterTestStoryContent content = CharacterTestStoryFixture.CreateBootstrapContent();
            Texture2D combatHudAtlas = null;
            Texture2D mainMenuAtlas = null;
            GlobalResourceRuntimeServices resources = null;
            GameSlice slice = null;
            var cameraObject = new GameObject("CharacterTest UI Runtime Refresh Camera Test");
            var hostObject = new GameObject("CharacterTest UI Runtime Refresh Host Test");
            CharacterTestUiRuntime runtime = null;
            try
            {
                resources = CreateCharacterTestUiResourceServices(out combatHudAtlas, out mainMenuAtlas);
                slice = new GameSlice(
                    NullRuntimeLogger.Instance,
                    content,
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
                    StoryContent = content,
                    IsPausedProvider = () => false,
                    EnableDebugUi = false,
                    EnableFairyGuiCombatHud = true,
                    EnableFairyGuiStoryUi = true,
                    EnableFairyGuiNetworkPanel = false,
                    EnableFairyGuiSettingsPanel = false,
                    EnableFairyGuiAbilityBindingPanel = false
                });
                CharacterTestFairyGuiCombatHudHost combatHudHost = runtime.FairyGuiCombatHudHost;
                Assert.IsNotNull(combatHudHost);
                Assert.IsTrue(runtime.IsFairyGuiCombatHudConfigured, runtime.LastFairyGuiCombatHudStatus);
                Assert.IsTrue(combatHudHost.IsOpen, combatHudHost.LastStatusMessage);
                Assert.AreEqual(1, combatHudHost.SuccessfulRefreshCount);

                CharacterTestFairyGuiStoryDialogHost storyHost = runtime.FairyGuiStoryDialogHost;
                Assert.IsNotNull(storyHost);
                Assert.IsTrue(storyHost.IsOpen, storyHost.LastStatusMessage);

                runtime.Refresh();

                Assert.AreEqual(0, combatHudHost.SkippedRefreshCount);
                Assert.IsFalse(combatHudHost.IsOpen);
                Assert.AreEqual(1, storyHost.SkippedRefreshCount);

                runtime.Release();

                Assert.IsNull(runtime.FairyGuiCombatHudHost);
                Assert.IsNull(runtime.FairyGuiStoryDialogHost);
                Assert.IsFalse(storyHost.IsOpen);
                Assert.DoesNotThrow(() => runtime.Refresh());
                Assert.DoesNotThrow(() => runtime.Release());
            }
            finally
            {
                runtime?.Release();
                slice?.Dispose();
                resources?.Dispose();
                UnityEngine.Object.DestroyImmediate(hostObject);
                UnityEngine.Object.DestroyImmediate(cameraObject);
                DestroyObjectNamed("Stage Camera");
                if (combatHudAtlas != null)
                    UnityEngine.Object.DestroyImmediate(combatHudAtlas);
                if (mainMenuAtlas != null)
                    UnityEngine.Object.DestroyImmediate(mainMenuAtlas);
                RemoveCharacterTestStoryDialogPackageIfLoaded();
                RemoveCharacterTestCombatHudPackageIfLoaded();
                RemoveCharacterTestMainMenuPackageIfLoaded();
            }
        }

[Test]
        public void UiRuntime_Configure_MainMenuSuppressesStoryDialogUntilMenuCloses()
        {
            RemoveCharacterTestStoryDialogPackageIfLoaded();
            RemoveCharacterTestCombatHudPackageIfLoaded();
            RemoveCharacterTestMainMenuPackageIfLoaded();
            CharacterTestStoryContent content = CharacterTestStoryFixture.CreateBootstrapContent();
            Texture2D combatHudAtlas = null;
            Texture2D mainMenuAtlas = null;
            GlobalResourceRuntimeServices resources = null;
            GameSlice slice = null;
            var cameraObject = new GameObject("CharacterTest UI Runtime Menu Suppress Camera Test");
            var hostObject = new GameObject("CharacterTest UI Runtime Menu Suppress Host Test");
            CharacterTestUiRuntime runtime = null;
            try
            {
                resources = CreateCharacterTestUiResourceServices(out combatHudAtlas, out mainMenuAtlas);
                slice = new GameSlice(
                    NullRuntimeLogger.Instance,
                    content,
                    resources,
                    baseResourcePreloadGroupId: string.Empty);

                cameraObject.tag = "MainCamera";
                cameraObject.AddComponent<UnityEngine.Camera>();
                CharacterTestThirdPersonCameraRigBridge cameraBridge =
                    hostObject.AddComponent<CharacterTestThirdPersonCameraRigBridge>();
                runtime = new CharacterTestUiRuntime(cameraBridge, NullRuntimeLogger.Instance);
                runtime.Configure(new CharacterTestUiRuntimeOptions
                {
                    Slice = slice,
                    StoryContent = content,
                    IsPausedProvider = () => true,
                    EnableDebugUi = false,
                    EnableFairyGuiCombatHud = true,
                    EnableFairyGuiFloatingCombatText = false,
                    EnableFairyGuiStoryUi = true,
                    EnableFairyGuiMainMenu = true,
                    AutoOpenMainMenu = true,
                    EnableFairyGuiNetworkPanel = false,
                    MainMenuController = new RecordingMainMenuController(),
                    InputProvider = new TrackingInputProvider(InputSnapshot.Empty)
                });

                Assert.IsNotNull(runtime.FairyGuiMainMenuHost);
                Assert.IsTrue(runtime.FairyGuiMainMenuHost.IsOpen, runtime.LastFairyGuiMainMenuStatus);
                Assert.IsNotNull(runtime.FairyGuiCombatHudHost);
                runtime.Refresh();
                Assert.IsFalse(runtime.FairyGuiCombatHudHost.IsOpen);
                Assert.IsNotNull(runtime.FairyGuiStoryDialogHost);
                Assert.IsTrue(runtime.IsFairyGuiStoryUiConfigured, runtime.LastFairyGuiStoryUiStatus);
                Assert.IsFalse(runtime.FairyGuiStoryDialogHost.IsOpen);

                Assert.IsTrue(runtime.CloseMainMenu());
                runtime.Refresh();

                Assert.IsFalse(runtime.FairyGuiMainMenuHost.IsOpen);
                Assert.IsTrue(runtime.FairyGuiStoryDialogHost.IsOpen, runtime.LastFairyGuiStoryUiStatus);
            }
            finally
            {
                runtime?.Release();
                slice?.Dispose();
                resources?.Dispose();
                UnityEngine.Object.DestroyImmediate(hostObject);
                UnityEngine.Object.DestroyImmediate(cameraObject);
                DestroyObjectNamed("Stage Camera");
                if (combatHudAtlas != null)
                    UnityEngine.Object.DestroyImmediate(combatHudAtlas);
                if (mainMenuAtlas != null)
                    UnityEngine.Object.DestroyImmediate(mainMenuAtlas);
                RemoveCharacterTestStoryDialogPackageIfLoaded();
                RemoveCharacterTestCombatHudPackageIfLoaded();
                RemoveCharacterTestMainMenuPackageIfLoaded();
            }
        }

[Test]
        public void UiRuntime_OpenMainMenuWhileCombatHudOpen_KeepsSharedHudPackageResources()
        {
            ResetFairyGuiTestState();
            CharacterTestStoryContent content = CharacterTestStoryFixture.CreateBootstrapContent();
            GlobalResourceRuntimeServices resources = null;
            GameSlice slice = null;
            var cameraObject = new GameObject("CharacterTest UI Runtime Menu Package Camera Test");
            var hostObject = new GameObject("CharacterTest UI Runtime Menu Package Host Test");
            CharacterTestUiRuntime runtime = null;
            try
            {
                resources = GlobalResourceRuntimeServices.Create(new GlobalResourceRuntimeOptions
                {
                    AllowMissingPreloadGroupInEditor = true
                });
                Assert.IsFalse(resources.HasBootstrapError, resources.BootstrapErrorMessage);
                Assert.IsTrue(resources.ResourceManager.Contains(CharacterTestFairyGuiCombatHudIds.PackageBytesKey));
                Assert.IsTrue(resources.ResourceManager.Contains(CharacterTestFairyGuiCombatHudIds.Atlas0Key));
                slice = new GameSlice(
                    NullRuntimeLogger.Instance,
                    content,
                    resources,
                    baseResourcePreloadGroupId: string.Empty);

                cameraObject.tag = "MainCamera";
                cameraObject.AddComponent<UnityEngine.Camera>();
                CharacterTestThirdPersonCameraRigBridge cameraBridge =
                    hostObject.AddComponent<CharacterTestThirdPersonCameraRigBridge>();
                runtime = new CharacterTestUiRuntime(cameraBridge, NullRuntimeLogger.Instance);
                runtime.Configure(new CharacterTestUiRuntimeOptions
                {
                    Slice = slice,
                    StoryContent = content,
                    IsPausedProvider = () => true,
                    EnableDebugUi = false,
                    EnableFairyGuiCombatHud = true,
                    EnableFairyGuiFloatingCombatText = false,
                    EnableFairyGuiStoryUi = false,
                    EnableFairyGuiMainMenu = true,
                    AutoOpenMainMenu = false,
                    EnableFairyGuiNetworkPanel = false,
                    EnableFairyGuiSettingsPanel = false,
                    MainMenuController = new RecordingMainMenuController()
                });

                Assert.IsNotNull(runtime.FairyGuiCombatHudHost);
                Assert.IsTrue(runtime.FairyGuiCombatHudHost.IsOpen, runtime.LastFairyGuiCombatHudStatus);
                Assert.IsNotNull(Fgui.UIPackage.GetByName(CharacterTestFairyGuiCombatHudIds.PackageId));
                Assert.IsNotNull(runtime.FairyGuiMainMenuHost);
                Assert.IsFalse(runtime.FairyGuiMainMenuHost.IsOpen);

                Assert.IsTrue(runtime.OpenMainMenu(), runtime.LastFairyGuiMainMenuStatus);
                Assert.IsTrue(runtime.FairyGuiMainMenuHost.IsOpen, runtime.LastFairyGuiMainMenuStatus);
                AssertImageTextureAvailable(
                    GetOpenMainMenuComponent(runtime.FairyGuiMainMenuHost),
                    CharacterTestFairyGuiMainMenuIds.PanelFrame);

                runtime.Refresh();

                Assert.IsFalse(runtime.FairyGuiCombatHudHost.IsOpen);
                Assert.IsTrue(runtime.FairyGuiMainMenuHost.IsOpen, runtime.LastFairyGuiMainMenuStatus);
                Assert.IsNotNull(Fgui.UIPackage.GetByName(CharacterTestFairyGuiCombatHudIds.PackageId));
                AssertImageTextureAvailable(
                    GetOpenMainMenuComponent(runtime.FairyGuiMainMenuHost),
                    CharacterTestFairyGuiMainMenuIds.PanelFrame);
            }
            finally
            {
                runtime?.Release();
                slice?.Dispose();
                resources?.Dispose();
                UnityEngine.Object.DestroyImmediate(hostObject);
                UnityEngine.Object.DestroyImmediate(cameraObject);
                ResetFairyGuiTestState();
            }
        }

[Test]
        public void UiRuntime_PlayerInteractiveModalViews_PushUiInputContextAndReleaseCursorScope()
        {
            ResetFairyGuiTestState();
            RemoveCharacterTestLegacyPanelPackageIfLoaded();
            CharacterTestStoryContent content = CharacterTestStoryFixture.CreateBootstrapContent();
            Texture2D combatHudAtlas = null;
            GlobalResourceRuntimeServices resources = null;
            GameSlice slice = null;
            var cameraObject = new GameObject("CharacterTest UI Runtime Input Bridge Camera Test");
            var hostObject = new GameObject("CharacterTest UI Runtime Input Bridge Host Test");
            CharacterTestUiRuntime runtime = null;
            var cursorDriver = new RecordingCursorDriver();
            var cursorController = new MxCursorController(cursorDriver);
            IDisposable debugInputScope = null;
            try
            {
                resources = CreateNetworkPanelResourceServices(out combatHudAtlas);
                slice = new GameSlice(
                    NullRuntimeLogger.Instance,
                    content,
                    resources,
                    baseResourcePreloadGroupId: string.Empty);
                var input = new TrackingInputProvider(InputSnapshot.Empty);
                debugInputScope = input.PushContext(InputContext.Debug, InputContextPolicy.Overlay);

                cameraObject.tag = "MainCamera";
                cameraObject.AddComponent<UnityEngine.Camera>();
                CharacterTestThirdPersonCameraRigBridge cameraBridge =
                    hostObject.AddComponent<CharacterTestThirdPersonCameraRigBridge>();
                cameraBridge.Bind(slice, NullRuntimeLogger.Instance, cursorController: cursorController);

                runtime = new CharacterTestUiRuntime(cameraBridge, NullRuntimeLogger.Instance);
                runtime.Configure(new CharacterTestUiRuntimeOptions
                {
                    Slice = slice,
                    StoryContent = content,
                    IsPausedProvider = () => true,
                    EnableDebugUi = false,
                    EnableFairyGuiCombatHud = false,
                    EnableFairyGuiFloatingCombatText = false,
                    EnableFairyGuiStoryUi = false,
                    EnableFairyGuiMainMenu = true,
                    AutoOpenMainMenu = false,
                    EnableFairyGuiNetworkPanel = true,
                    AutoOpenNetworkPanel = false,
                    MainMenuController = new RecordingMainMenuController(),
                    NetworkSessionController = new RecordingNetworkSessionController(),
                    InputProvider = input,
                    CursorController = cursorController,
                    CameraRigBridge = cameraBridge
                });

                Assert.IsTrue(runtime.OpenMainMenu(), runtime.LastFairyGuiMainMenuStatus);
                Assert.IsTrue(runtime.FairyGuiMainMenuHost.IsOpen, runtime.LastFairyGuiMainMenuStatus);
                Assert.AreEqual(InputContext.UI, input.CurrentContext);
                Assert.IsTrue(input.IsContextEnabled(InputContext.UI));
                Assert.IsFalse(input.IsContextEnabled(InputContext.Gameplay));
                Assert.IsFalse(input.IsContextEnabled(InputContext.Debug));
                Assert.AreEqual(2, input.ActiveScopeCount);
                Assert.IsFalse(cameraBridge.CursorCaptured);
                Assert.AreEqual(MxCursorMode.Released, cursorController.Current.Mode);
                Assert.AreEqual("CharacterTest.UI.Modal", cursorController.Current.Owner);
                Assert.AreEqual(CursorLockMode.None, cursorDriver.LockState);
                Assert.IsTrue(cursorDriver.Visible);

                int transitionStateStart = cursorDriver.States.Count;
                Assert.IsTrue(runtime.OpenNetworkPanelFromMenu(), runtime.LastFairyGuiNetworkPanelStatus);
                Assert.IsFalse(runtime.FairyGuiMainMenuHost.IsOpen);
                Assert.IsTrue(runtime.FairyGuiNetworkPanelHost.IsOpen);
                Assert.IsFalse(input.IsContextEnabled(InputContext.Gameplay));
                Assert.IsFalse(input.IsContextEnabled(InputContext.Debug));
                Assert.AreEqual(2, input.ActiveScopeCount);
                Assert.AreEqual(3, input.PushCount);
                Assert.AreEqual(1, input.DisposeCount);
                Assert.IsFalse(cameraBridge.CursorCaptured);
                Assert.AreEqual(MxCursorMode.Released, cursorController.Current.Mode);
                for (int i = transitionStateStart; i < cursorDriver.States.Count; i++)
                    Assert.AreEqual(MxCursorMode.Released, cursorDriver.States[i].Mode);

                int returnStateStart = cursorDriver.States.Count;
                runtime.FairyGuiNetworkPanelHost.CommandSink.Enqueue(new MxUiCommand(
                    CharacterTestFairyGuiNetworkPanelIds.ViewId,
                    CharacterTestFairyGuiNetworkPanelCommandIds.Close,
                    null));

                Assert.IsTrue(runtime.FairyGuiMainMenuHost.IsOpen, runtime.LastFairyGuiMainMenuStatus);
                Assert.IsFalse(runtime.FairyGuiNetworkPanelHost.IsOpen);
                Assert.AreEqual(2, input.ActiveScopeCount);
                Assert.AreEqual(2, input.DisposeCount);
                Assert.IsTrue(input.IsContextEnabled(InputContext.UI));
                Assert.IsFalse(input.IsContextEnabled(InputContext.Gameplay));
                Assert.IsFalse(input.IsContextEnabled(InputContext.Debug));
                Assert.IsFalse(cameraBridge.CursorCaptured);
                Assert.AreEqual(MxCursorMode.Released, cursorController.Current.Mode);
                Assert.AreEqual("CharacterTest.UI.Modal", cursorController.Current.Owner);
                for (int i = returnStateStart; i < cursorDriver.States.Count; i++)
                    Assert.AreEqual(MxCursorMode.Released, cursorDriver.States[i].Mode);

                Assert.IsTrue(runtime.FairyGuiMainMenuHost.Close());

                Assert.AreEqual(1, input.ActiveScopeCount);
                Assert.AreEqual(3, input.DisposeCount);
                Assert.IsTrue(input.IsContextEnabled(InputContext.Gameplay));
                Assert.IsTrue(input.IsContextEnabled(InputContext.Debug));
                Assert.IsTrue(cameraBridge.CursorCaptured);
                Assert.AreEqual(MxCursorMode.Captured, cursorController.Current.Mode);
                Assert.AreEqual("CharacterTest.Camera", cursorController.Current.Owner);
                Assert.AreEqual(CursorLockMode.Locked, cursorDriver.LockState);
                Assert.IsFalse(cursorDriver.Visible);

                runtime.Release();

                Assert.AreEqual(1, input.ActiveScopeCount);
                Assert.AreEqual(3, input.DisposeCount);
            }
            finally
            {
                runtime?.Release();
                debugInputScope?.Dispose();
                slice?.Dispose();
                resources?.Dispose();
                UnityEngine.Object.DestroyImmediate(hostObject);
                UnityEngine.Object.DestroyImmediate(cameraObject);
                DestroyObjectNamed("Stage Camera");
                cursorController.Dispose();
                if (combatHudAtlas != null)
                    UnityEngine.Object.DestroyImmediate(combatHudAtlas);
                RemoveCharacterTestLegacyPanelPackageIfLoaded();
            }
        }

        [Test]
        public void UiRuntime_LargePlayerSurfaces_KeepOnlyOneExclusiveSessionOpen()
        {
            ResetFairyGuiTestState();
            RemoveCharacterTestStoryDialogPackageIfLoaded();
            RemoveCharacterTestCombatHudPackageIfLoaded();
            CharacterTestStoryContent content = CharacterTestStoryFixture.CreateBootstrapContent();
            Texture2D combatHudAtlas = null;
            Texture2D mainMenuAtlas = null;
            GlobalResourceRuntimeServices resources = null;
            GameSlice slice = null;
            var cameraObject = new GameObject("CharacterTest UI Runtime Surface Router Camera Test");
            var hostObject = new GameObject("CharacterTest UI Runtime Surface Router Host Test");
            CharacterTestUiRuntime runtime = null;
            var input = new TrackingInputProvider(InputSnapshot.Empty);
            var cursorDriver = new RecordingCursorDriver();
            var cursorController = new MxCursorController(cursorDriver);
            try
            {
                resources = CreateCharacterTestUiResourceServices(out combatHudAtlas, out mainMenuAtlas);
                slice = new GameSlice(
                    NullRuntimeLogger.Instance,
                    content,
                    resources,
                    baseResourcePreloadGroupId: string.Empty);
                CharacterTestArenaSessionController arenaController =
                    hostObject.AddComponent<CharacterTestArenaSessionController>();
                arenaController.PrepareInitialSetupState();

                cameraObject.tag = "MainCamera";
                cameraObject.AddComponent<UnityEngine.Camera>();
                CharacterTestThirdPersonCameraRigBridge cameraBridge =
                    hostObject.AddComponent<CharacterTestThirdPersonCameraRigBridge>();
                cameraBridge.Bind(slice, NullRuntimeLogger.Instance, cursorController: cursorController);

                runtime = new CharacterTestUiRuntime(cameraBridge, NullRuntimeLogger.Instance);
                runtime.Configure(new CharacterTestUiRuntimeOptions
                {
                    Slice = slice,
                    StoryContent = content,
                    IsPausedProvider = () => true,
                    EnableDebugUi = false,
                    EnableFairyGuiCombatHud = false,
                    EnableFairyGuiFloatingCombatText = false,
                    EnableFairyGuiStoryUi = true,
                    EnableFairyGuiMainMenu = true,
                    AutoOpenMainMenu = true,
                    EnableFairyGuiNetworkPanel = true,
                    AutoOpenNetworkPanel = false,
                    EnableFairyGuiSettingsPanel = false,
                    EnableFairyGuiAbilityBindingPanel = false,
                    EnableFairyGuiRuntimeAiBehaviorLoadoutPanel = true,
                    EnableFairyGuiArenaSetupPanel = true,
                    AutoOpenFairyGuiArenaSetupPanel = false,
                    MainMenuController = new RecordingMainMenuController(),
                    NetworkSessionController = new RecordingNetworkSessionController(),
                    InputProvider = input,
                    CursorController = cursorController,
                    CameraRigBridge = cameraBridge,
                    ArenaSessionController = arenaController
                });

                Assert.IsTrue(runtime.FairyGuiMainMenuHost.IsOpen, runtime.LastFairyGuiMainMenuStatus);
                Assert.AreEqual(1, CountOpenLargePlayerSurfaces(runtime));
                Assert.AreEqual(MxCursorMode.Released, cursorController.Current.Mode);

                Assert.IsTrue(runtime.OpenNetworkPanelFromMenu(), runtime.LastFairyGuiNetworkPanelStatus);
                Assert.IsFalse(runtime.FairyGuiMainMenuHost.IsOpen);
                Assert.IsTrue(runtime.FairyGuiNetworkPanelHost.IsOpen);
                Assert.AreEqual(1, CountOpenLargePlayerSurfaces(runtime));
                Assert.AreEqual(MxCursorMode.Released, cursorController.Current.Mode);

                Assert.IsTrue(runtime.OpenArenaSetupPanel(), runtime.LastFairyGuiArenaSetupPanelStatus);
                Assert.IsFalse(runtime.FairyGuiNetworkPanelHost.IsOpen);
                Assert.IsTrue(runtime.FairyGuiArenaSetupPanelHost.IsOpen);
                Assert.AreEqual(1, CountOpenLargePlayerSurfaces(runtime));

                Assert.IsTrue(
                    runtime.OpenArenaBehaviorGroupPanelForSlot(1, 0),
                    runtime.LastFairyGuiArenaBehaviorGroupPanelStatus);
                Assert.IsFalse(runtime.FairyGuiArenaSetupPanelHost.IsOpen);
                Assert.IsTrue(runtime.FairyGuiArenaBehaviorGroupPanelHost.IsOpen);
                Assert.AreEqual(1, CountOpenLargePlayerSurfaces(runtime));

                Assert.IsTrue(
                    runtime.OpenRuntimeAiBehaviorLoadoutPanel(),
                    runtime.LastFairyGuiRuntimeAiBehaviorLoadoutPanelStatus);
                Assert.IsFalse(runtime.FairyGuiArenaBehaviorGroupPanelHost.IsOpen);
                Assert.IsTrue(runtime.FairyGuiRuntimeAiBehaviorLoadoutPanelHost.IsOpen);
                Assert.AreEqual(1, CountOpenLargePlayerSurfaces(runtime));

                Assert.IsTrue(runtime.OpenMainMenu(), runtime.LastFairyGuiMainMenuStatus);
                Assert.IsFalse(runtime.FairyGuiRuntimeAiBehaviorLoadoutPanelHost.IsOpen);
                Assert.IsTrue(runtime.FairyGuiMainMenuHost.IsOpen);
                Assert.AreEqual(1, CountOpenLargePlayerSurfaces(runtime));

                Assert.IsTrue(runtime.CloseMainMenu());
                Assert.AreEqual(0, CountOpenLargePlayerSurfaces(runtime));
                Assert.AreEqual(MxCursorMode.Captured, cursorController.Current.Mode);
                Assert.AreEqual("CharacterTest.Camera", cursorController.Current.Owner);
            }
            finally
            {
                runtime?.Release();
                slice?.Dispose();
                resources?.Dispose();
                UnityEngine.Object.DestroyImmediate(hostObject);
                UnityEngine.Object.DestroyImmediate(cameraObject);
                DestroyObjectNamed("Stage Camera");
                cursorController.Dispose();
                if (combatHudAtlas != null)
                    UnityEngine.Object.DestroyImmediate(combatHudAtlas);
                if (mainMenuAtlas != null)
                    UnityEngine.Object.DestroyImmediate(mainMenuAtlas);
                RemoveCharacterTestStoryDialogPackageIfLoaded();
                RemoveCharacterTestCombatHudPackageIfLoaded();
                RemoveCharacterTestMainMenuPackageIfLoaded();
                ResetFairyGuiTestState();
            }
        }

        private static int CountOpenLargePlayerSurfaces(CharacterTestUiRuntime runtime)
        {
            int count = 0;
            if (runtime.FairyGuiMainMenuHost != null && runtime.FairyGuiMainMenuHost.IsOpen)
                count++;
            if (runtime.FairyGuiNetworkPanelHost != null && runtime.FairyGuiNetworkPanelHost.IsOpen)
                count++;
            if (runtime.FairyGuiSettingsPanelHost != null && runtime.FairyGuiSettingsPanelHost.IsOpen)
                count++;
            if (runtime.FairyGuiAbilityBindingPanelHost != null
                && runtime.FairyGuiAbilityBindingPanelHost.IsOpen)
            {
                count++;
            }
            if (runtime.FairyGuiRuntimeAiBehaviorLoadoutPanelHost != null
                && runtime.FairyGuiRuntimeAiBehaviorLoadoutPanelHost.IsOpen)
            {
                count++;
            }
            if (runtime.FairyGuiBehaviorDeckPanelHost != null
                && runtime.FairyGuiBehaviorDeckPanelHost.IsOpen)
            {
                count++;
            }
            if (runtime.FairyGuiArenaSetupPanelHost != null && runtime.FairyGuiArenaSetupPanelHost.IsOpen)
                count++;
            if (runtime.FairyGuiArenaBehaviorGroupPanelHost != null
                && runtime.FairyGuiArenaBehaviorGroupPanelHost.IsOpen)
            {
                count++;
            }

            return count;
        }

        [Test]
        public void UiRuntime_MainMenuSubPanels_AreExclusiveAndKeepCursorReleasedUntilMainMenuCloses()
        {
            ResetFairyGuiTestState();
            RemoveCharacterTestLegacyPanelPackageIfLoaded();
            CharacterTestStoryContent content = CharacterTestStoryFixture.CreateBootstrapContent();
            Texture2D combatHudAtlas = null;
            GlobalResourceRuntimeServices resources = null;
            GameSlice slice = null;
            var cameraObject = new GameObject("CharacterTest UI Runtime SubPanel Camera Test");
            var hostObject = new GameObject("CharacterTest UI Runtime SubPanel Host Test");
            CharacterTestUiRuntime runtime = null;
            var input = new TrackingInputProvider(InputSnapshot.Empty);
            var cursorDriver = new RecordingCursorDriver();
            var cursorController = new MxCursorController(cursorDriver);
            try
            {
                resources = CreateSettingsPanelResourceServices(out combatHudAtlas);
                slice = new GameSlice(
                    NullRuntimeLogger.Instance,
                    content,
                    resources,
                    baseResourcePreloadGroupId: string.Empty);
                CharacterTestLocalizationRuntime localizationRuntime = CharacterTestLocalizationRuntime.Create(
                    GetCharacterTestLocalizationPath(),
                    NullRuntimeLogger.Instance);
                var settingsRuntime = new CharacterTestSettingsRuntime(
                    "settings.json",
                    new MxSettingsService(null),
                    new MxInputRebindingSettingsController(null, null),
                    MxSettingsLoadResult.Succeeded(MxSettingsProfile.CreateDefault()),
                    localizationOptions: localizationRuntime.LocaleOptions);

                cameraObject.tag = "MainCamera";
                cameraObject.AddComponent<UnityEngine.Camera>();
                CharacterTestThirdPersonCameraRigBridge cameraBridge =
                    hostObject.AddComponent<CharacterTestThirdPersonCameraRigBridge>();
                cameraBridge.Bind(slice, NullRuntimeLogger.Instance, cursorController: cursorController);

                runtime = new CharacterTestUiRuntime(cameraBridge, NullRuntimeLogger.Instance);
                runtime.Configure(new CharacterTestUiRuntimeOptions
                {
                    Slice = slice,
                    StoryContent = content,
                    IsPausedProvider = () => true,
                    EnableDebugUi = false,
                    EnableFairyGuiCombatHud = false,
                    EnableFairyGuiFloatingCombatText = false,
                    EnableFairyGuiStoryUi = false,
                    EnableFairyGuiMainMenu = true,
                    AutoOpenMainMenu = true,
                    EnableFairyGuiNetworkPanel = false,
                    AutoOpenNetworkPanel = false,
                    EnableFairyGuiSettingsPanel = true,
                    EnableFairyGuiAbilityBindingPanel = true,
                    EnableFairyGuiRuntimeAiBehaviorLoadoutPanel = false,
                    MainMenuController = new RecordingMainMenuController(),
                    SettingsRuntime = settingsRuntime,
                    LocalizationRuntime = localizationRuntime,
                    InputProvider = input,
                    CursorController = cursorController,
                    CameraRigBridge = cameraBridge
                });

                Assert.IsTrue(runtime.FairyGuiMainMenuHost.IsOpen, runtime.LastFairyGuiMainMenuStatus);
                Assert.IsTrue(runtime.FairyGuiSettingsPanelHost.IsConfigured, runtime.LastFairyGuiSettingsPanelStatus);
                Assert.IsTrue(runtime.FairyGuiAbilityBindingPanelHost.IsConfigured, runtime.LastFairyGuiAbilityBindingPanelStatus);
                Assert.IsFalse(runtime.FairyGuiSettingsPanelHost.IsOpen);
                Assert.IsFalse(runtime.FairyGuiAbilityBindingPanelHost.IsOpen);
                Assert.AreEqual(1, input.ActiveScopeCount);
                Assert.AreEqual(MxCursorMode.Released, cursorController.Current.Mode);
                Assert.AreEqual("CharacterTest.UI.Modal", cursorController.Current.Owner);

                runtime.FairyGuiMainMenuHost.CommandSink.Enqueue(new MxUiCommand(
                    CharacterTestFairyGuiMainMenuIds.ViewId,
                    CharacterTestFairyGuiMainMenuCommandIds.SettingsToggle,
                    null));

                Assert.IsFalse(
                    runtime.FairyGuiMainMenuHost.IsOpen,
                    runtime.FairyGuiMainMenuHost.CommandSink.LastResult.Message
                    + " | "
                    + runtime.LastFairyGuiSettingsPanelStatus);
                Assert.IsTrue(runtime.FairyGuiSettingsPanelHost.IsOpen, runtime.LastFairyGuiSettingsPanelStatus);
                Assert.IsFalse(runtime.FairyGuiAbilityBindingPanelHost.IsOpen);
                Assert.AreEqual(1, input.ActiveScopeCount);
                Assert.AreEqual(MxCursorMode.Released, cursorController.Current.Mode);

                int settingsReturnStateStart = cursorDriver.States.Count;
                runtime.FairyGuiSettingsPanelHost.CommandSink.Enqueue(new MxUiCommand(
                    CharacterTestFairyGuiSettingsPanelIds.ViewId,
                    CharacterTestFairyGuiSettingsPanelCommandIds.Close,
                    null));

                Assert.IsTrue(runtime.FairyGuiMainMenuHost.IsOpen, runtime.LastFairyGuiMainMenuStatus);
                Assert.IsFalse(runtime.FairyGuiSettingsPanelHost.IsOpen);
                Assert.AreEqual(1, input.ActiveScopeCount);
                Assert.AreEqual(MxCursorMode.Released, cursorController.Current.Mode);
                for (int i = settingsReturnStateStart; i < cursorDriver.States.Count; i++)
                    Assert.AreEqual(MxCursorMode.Released, cursorDriver.States[i].Mode);

                runtime.FairyGuiMainMenuHost.CommandSink.Enqueue(new MxUiCommand(
                    CharacterTestFairyGuiMainMenuIds.ViewId,
                    CharacterTestFairyGuiMainMenuCommandIds.AbilitiesToggle,
                    null));

                Assert.IsFalse(runtime.FairyGuiMainMenuHost.IsOpen);
                Assert.IsFalse(runtime.FairyGuiSettingsPanelHost.IsOpen);
                Assert.IsTrue(runtime.FairyGuiAbilityBindingPanelHost.IsOpen, runtime.LastFairyGuiAbilityBindingPanelStatus);
                Assert.AreEqual(1, input.ActiveScopeCount);
                Assert.AreEqual(MxCursorMode.Released, cursorController.Current.Mode);

                int abilityReturnStateStart = cursorDriver.States.Count;
                runtime.FairyGuiAbilityBindingPanelHost.CommandSink.Enqueue(new MxUiCommand(
                    CharacterTestFairyGuiAbilityBindingPanelIds.ViewId,
                    CharacterTestFairyGuiAbilityBindingPanelCommandIds.Cancel,
                    null));

                Assert.IsTrue(runtime.FairyGuiMainMenuHost.IsOpen, runtime.LastFairyGuiMainMenuStatus);
                Assert.IsFalse(runtime.FairyGuiAbilityBindingPanelHost.IsOpen);
                Assert.AreEqual(1, input.ActiveScopeCount);
                Assert.AreEqual(MxCursorMode.Released, cursorController.Current.Mode);
                for (int i = abilityReturnStateStart; i < cursorDriver.States.Count; i++)
                    Assert.AreEqual(MxCursorMode.Released, cursorDriver.States[i].Mode);

                Assert.IsTrue(runtime.FairyGuiMainMenuHost.Close());

                Assert.AreEqual(0, input.ActiveScopeCount);
                Assert.IsTrue(cameraBridge.CursorCaptured);
                Assert.AreEqual(MxCursorMode.Captured, cursorController.Current.Mode);
                Assert.AreEqual("CharacterTest.Camera", cursorController.Current.Owner);
            }
            finally
            {
                runtime?.Release();
                slice?.Dispose();
                resources?.Dispose();
                UnityEngine.Object.DestroyImmediate(hostObject);
                UnityEngine.Object.DestroyImmediate(cameraObject);
                DestroyObjectNamed("Stage Camera");
                cursorController.Dispose();
                if (combatHudAtlas != null)
                    UnityEngine.Object.DestroyImmediate(combatHudAtlas);
                RemoveCharacterTestLegacyPanelPackageIfLoaded();
                ResetFairyGuiTestState();
            }
        }

[Test]
        public void UiRuntime_Configure_MainMenuAutoOpensAndNetworkPanelIsOnDemand()
        {
            RemoveCharacterTestLegacyPanelPackageIfLoaded();
            CharacterTestStoryContent content = CharacterTestStoryFixture.CreateBootstrapContent();
            Texture2D combatHudAtlas = null;
            GlobalResourceRuntimeServices resources = null;
            GameSlice slice = null;
            var cameraObject = new GameObject("CharacterTest UI Runtime Main Menu Camera Test");
            var hostObject = new GameObject("CharacterTest UI Runtime Main Menu Host Test");
            CharacterTestUiRuntime runtime = null;
            try
            {
                resources = CreateNetworkPanelResourceServices(out combatHudAtlas);
                slice = new GameSlice(
                    NullRuntimeLogger.Instance,
                    content,
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
                    StoryContent = content,
                    IsPausedProvider = () => true,
                    EnableDebugUi = false,
                    EnableFairyGuiCombatHud = false,
                    EnableFairyGuiFloatingCombatText = false,
                    EnableFairyGuiStoryUi = false,
                    EnableFairyGuiMainMenu = true,
                    AutoOpenMainMenu = true,
                    EnableFairyGuiNetworkPanel = true,
                    AutoOpenNetworkPanel = false,
                    MainMenuController = new RecordingMainMenuController(),
                    NetworkSessionController = new RecordingNetworkSessionController()
                });

                Assert.IsNotNull(runtime.FairyGuiMainMenuHost);
                Assert.IsTrue(runtime.IsFairyGuiMainMenuConfigured, runtime.LastFairyGuiMainMenuStatus);
                Assert.IsTrue(runtime.FairyGuiMainMenuHost.IsOpen, runtime.FairyGuiMainMenuHost.LastStatusMessage);
                Assert.IsNotNull(runtime.FairyGuiNetworkPanelHost);
                Assert.IsTrue(runtime.IsFairyGuiNetworkPanelConfigured, runtime.LastFairyGuiNetworkPanelStatus);
                Assert.IsFalse(runtime.FairyGuiNetworkPanelHost.IsOpen);

                Assert.IsTrue(runtime.OpenNetworkPanelFromMenu(), runtime.LastFairyGuiNetworkPanelStatus);
                Assert.IsFalse(runtime.FairyGuiMainMenuHost.IsOpen);
                Assert.IsTrue(runtime.FairyGuiNetworkPanelHost.IsOpen);

                runtime.Release();

                Assert.IsNull(runtime.FairyGuiMainMenuHost);
                Assert.IsNull(runtime.FairyGuiNetworkPanelHost);
                Assert.DoesNotThrow(() => runtime.Refresh());
            }
            finally
            {
                runtime?.Release();
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
        public void UiRuntime_NetworkPanelCloseSuppressesAutoOpenRefresh()
        {
            RemoveCharacterTestLegacyPanelPackageIfLoaded();
            CharacterTestStoryContent content = CharacterTestStoryFixture.CreateBootstrapContent();
            Texture2D combatHudAtlas = null;
            GlobalResourceRuntimeServices resources = null;
            GameSlice slice = null;
            var cameraObject = new GameObject("CharacterTest UI Runtime Network AutoOpen Camera Test");
            var hostObject = new GameObject("CharacterTest UI Runtime Network AutoOpen Host Test");
            CharacterTestUiRuntime runtime = null;
            try
            {
                resources = CreateNetworkPanelResourceServices(out combatHudAtlas);
                slice = new GameSlice(
                    NullRuntimeLogger.Instance,
                    content,
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
                    StoryContent = content,
                    IsPausedProvider = () => true,
                    EnableDebugUi = false,
                    EnableFairyGuiCombatHud = false,
                    EnableFairyGuiFloatingCombatText = false,
                    EnableFairyGuiStoryUi = false,
                    EnableFairyGuiMainMenu = false,
                    AutoOpenMainMenu = false,
                    EnableFairyGuiNetworkPanel = true,
                    AutoOpenNetworkPanel = true,
                    NetworkSessionController = new RecordingNetworkSessionController()
                });

                Assert.IsNotNull(runtime.FairyGuiNetworkPanelHost);
                Assert.IsTrue(runtime.IsFairyGuiNetworkPanelConfigured, runtime.LastFairyGuiNetworkPanelStatus);
                Assert.IsTrue(runtime.FairyGuiNetworkPanelHost.IsOpen, runtime.LastFairyGuiNetworkPanelStatus);

                Assert.IsTrue(runtime.FairyGuiNetworkPanelHost.Close());
                Assert.IsFalse(runtime.FairyGuiNetworkPanelHost.IsOpen);

                runtime.Refresh();

                Assert.IsFalse(runtime.FairyGuiNetworkPanelHost.IsOpen);
            }
            finally
            {
                runtime?.Release();
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
        public void UiRuntime_DetachAndRebindSession_PreservesHostsAndUpdatesSlice()
        {
            RemoveCharacterTestLegacyPanelPackageIfLoaded();
            CharacterTestStoryContent content = CharacterTestStoryFixture.CreateBootstrapContent();
            Texture2D combatHudAtlas = null;
            GlobalResourceRuntimeServices resources1 = null;
            GlobalResourceRuntimeServices resources2 = null;
            GameSlice slice1 = null;
            GameSlice slice2 = null;
            var cameraObject = new GameObject("CharacterTest UI Runtime Rebind Camera Test");
            var hostObject = new GameObject("CharacterTest UI Runtime Rebind Host Test");
            CharacterTestUiRuntime runtime = null;
            try
            {
                resources1 = CreateNetworkPanelResourceServices(out combatHudAtlas);
                slice1 = new GameSlice(
                    NullRuntimeLogger.Instance,
                    content,
                    resources1,
                    baseResourcePreloadGroupId: string.Empty);

                cameraObject.tag = "MainCamera";
                cameraObject.AddComponent<UnityEngine.Camera>();
                CharacterTestThirdPersonCameraRigBridge owner =
                    hostObject.AddComponent<CharacterTestThirdPersonCameraRigBridge>();
                runtime = new CharacterTestUiRuntime(owner, NullRuntimeLogger.Instance);

                var options1 = new CharacterTestUiRuntimeOptions
                {
                    Slice = slice1,
                    StoryContent = content,
                    IsPausedProvider = () => false,
                    EnableDebugUi = false,
                    EnableFairyGuiCombatHud = false,
                    EnableFairyGuiFloatingCombatText = true,
                    EnableFairyGuiStoryUi = false,
                    EnableFairyGuiMainMenu = false,
                    EnableFairyGuiNetworkPanel = false,
                    EnableFairyGuiSettingsPanel = false,
                    EnableFairyGuiAbilityBindingPanel = false,
                    EnableFairyGuiRuntimeAiBehaviorLoadoutPanel = false,
                    EnableFairyGuiArenaSetupPanel = false,
                    EnableFairyGuiRuntimeAiPlannerDevPanel = false
                };
                runtime.Configure(options1);

                Assert.IsTrue(runtime.IsConfigured);
                Assert.IsTrue(runtime.IsFairyGuiFloatingCombatTextConfigured);
                CharacterTestFairyGuiFloatingCombatTextHost floatingTextHost = runtime.FairyGuiFloatingCombatTextHost;
                Assert.IsNotNull(floatingTextHost);

                // Detach slice
                runtime.DetachSlice();

                // Create second slice
                resources2 = CreateNetworkPanelResourceServices(out _);
                slice2 = new GameSlice(
                    NullRuntimeLogger.Instance,
                    content,
                    resources2,
                    baseResourcePreloadGroupId: string.Empty);

                var options2 = new CharacterTestUiRuntimeOptions
                {
                    Slice = slice2,
                    StoryContent = content,
                    IsPausedProvider = () => false,
                    EnableDebugUi = false,
                    EnableFairyGuiCombatHud = false,
                    EnableFairyGuiFloatingCombatText = true,
                    EnableFairyGuiStoryUi = false,
                    EnableFairyGuiMainMenu = false,
                    EnableFairyGuiNetworkPanel = false,
                    EnableFairyGuiSettingsPanel = false,
                    EnableFairyGuiAbilityBindingPanel = false,
                    EnableFairyGuiRuntimeAiBehaviorLoadoutPanel = false,
                    EnableFairyGuiArenaSetupPanel = false,
                    EnableFairyGuiRuntimeAiPlannerDevPanel = false
                };

                Assert.IsTrue(runtime.CanRebind(options2));
                bool rebindResult = runtime.TryRebind(options2);
                Assert.IsTrue(rebindResult);

                // Verify host instance was retained
                Assert.AreSame(floatingTextHost, runtime.FairyGuiFloatingCombatTextHost);
                Assert.IsTrue(runtime.IsFairyGuiFloatingCombatTextConfigured);

                // Verify topology mismatch returns false
                var mismatchOptions = new CharacterTestUiRuntimeOptions
                {
                    Slice = slice2,
                    StoryContent = content,
                    EnableFairyGuiMainMenu = true // was false originally
                };
                Assert.IsFalse(runtime.CanRebind(mismatchOptions));
                Assert.IsFalse(runtime.TryRebind(mismatchOptions));
            }
            finally
            {
                runtime?.Release();
                slice1?.Dispose();
                slice2?.Dispose();
                resources1?.Dispose();
                resources2?.Dispose();
                UnityEngine.Object.DestroyImmediate(hostObject);
                UnityEngine.Object.DestroyImmediate(cameraObject);
                DestroyObjectNamed("Stage Camera");
                if (combatHudAtlas != null)
                    UnityEngine.Object.DestroyImmediate(combatHudAtlas);
                RemoveCharacterTestLegacyPanelPackageIfLoaded();
            }
        }
    }
}
