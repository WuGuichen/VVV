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

        [UnityTest]
        public IEnumerator RuntimeAiLabScene_FinalCrossWorkspaceFlowPreservesIdentityAndReopensPlayerDeck()
        {
            EditorSceneManager.OpenScene(RuntimeAiLabScenePath, OpenSceneMode.Single);

            yield return new EnterPlayMode();
            yield return null;
            yield return null;

            CharacterTestRuntimeAiPlannerDevSceneController controller =
                Object.FindAnyObjectByType<CharacterTestRuntimeAiPlannerDevSceneController>();
            Assert.IsNotNull(controller);
            CharacterTestRuntimeAiLabShellCoordinator coordinator = controller.ShellCoordinator;
            Assert.IsNotNull(coordinator, "The live FairyGUI shell must own the Play entry flow.");
            Assert.AreEqual(
                CharacterTestRuntimeAiLabWorkspace.PlayerDeck,
                coordinator.CurrentState.ActiveWorkspace);

            GameManager gameManager = Object.FindAnyObjectByType<GameManager>();
            Assert.IsNotNull(gameManager);
            CharacterTestRuntimeAiPlannerLocalPlayerDeckService initialDeckService =
                gameManager.RuntimeContext.PlayerBehaviorDeckService;
            Assert.IsNotNull(initialDeckService);
            CharacterTestRuntimeAiPlannerInstalledDeck initialDeck = initialDeckService.Workbench.Deck;
            Assert.IsNotNull(initialDeck);
            CharacterTestFairyGuiBehaviorDeckPanelHost initialDeckHost =
                gameManager.RuntimeContext.UiRuntime.FairyGuiBehaviorDeckPanelHost;
            Assert.IsNotNull(initialDeckHost);
            Assert.IsTrue(initialDeckHost.IsOpen);
            initialDeckHost.CommandSink.Enqueue(new MxUiCommand(
                CharacterTestFairyGuiBehaviorDeckPanelIds.ViewId,
                CharacterTestFairyGuiBehaviorDeckPanelCommandIds.ContinueToSetup,
                null));
            Assert.AreEqual(
                CharacterTestRuntimeAiLabWorkspace.Setup,
                coordinator.CurrentState.ActiveWorkspace,
                initialDeckHost.CommandSink.LastStatusMessage);

            CharacterTestFairyGuiRuntimeAiPlannerDevPanelHost panelHost =
                Object.FindAnyObjectByType<CharacterTestFairyGuiRuntimeAiPlannerDevPanelHost>();
            Assert.IsNotNull(panelHost);
            Assert.IsTrue(panelHost.IsOpen);
            CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandSink sink = panelHost.CommandSink;
            Assert.IsNotNull(sink);
            sink.RestoreNavigationState(
                CharacterTestRuntimeAiLabWorkspaceMode.Setup,
                CharacterTestRuntimeAiLabDiagnosticsTab.Facts,
                CharacterTestRuntimeAiLabTimelineMode.RuntimeTrace,
                CharacterTestRuntimeAiLabWorkspaceMode.Setup,
                0,
                0,
                CharacterTestRuntimeAiLabLogicElementKind.Stage,
                0);
            EnqueueRuntimeAiLabCommand(
                sink,
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.OpenBehaviorGroup);
            EnqueueRuntimeAiLabCommand(
                sink,
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.SelectWorkbenchEntry(0));
            EnqueueRuntimeAiLabCommand(
                sink,
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.BehaviorCardTab);
            Assert.AreEqual(
                CharacterTestRuntimeAiLabWorkspace.BehaviorCard,
                coordinator.CurrentState.ActiveWorkspace,
                sink.LastStatusMessage);

            CharacterTestRuntimeAiLabPageContext authoringContext =
                coordinator.CurrentState.PageContext;
            string draftWorkspaceId = controller.DraftWorkspace.WorkspaceId;
            int draftRevision = controller.DraftWorkspace.CurrentRevision;
            Assert.IsFalse(string.IsNullOrEmpty(authoringContext.GroupDraftId));
            Assert.IsFalse(string.IsNullOrEmpty(authoringContext.EntryId));
            Assert.IsFalse(string.IsNullOrEmpty(authoringContext.CardDraftId));

            EnqueueRuntimeAiLabCommand(
                sink,
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.Start);
            for (int frame = 0;
                 frame < 16
                 && coordinator.CurrentState.LastCompletedOutcome
                 != CharacterTestRuntimeAiLabSessionStartOutcome.Succeeded;
                 frame++)
            {
                yield return null;
            }
            panelHost = Object.FindAnyObjectByType<CharacterTestFairyGuiRuntimeAiPlannerDevPanelHost>();
            Assert.IsNotNull(panelHost);
            sink = panelHost.CommandSink;
            Assert.IsNotNull(sink);
            Assert.AreEqual(
                CharacterTestRuntimeAiLabWorkspace.RuntimeFrame,
                coordinator.CurrentState.ActiveWorkspace,
                "Starting from Behavior Card must open the unified runtime workbench after the session start completes.");
            Assert.AreEqual(
                CharacterTestRuntimeAiLabTimelineMode.BehaviorLogic,
                coordinator.CurrentState.PageContext.TimelineMode,
                "Starting from Behavior Card must keep its behavior-plan tab selected inside the unified runtime workbench.");
            Assert.IsTrue(controller.StepForward10(), controller.StatusMessage);
            Assert.IsTrue(controller.StepBackFrame(), controller.StatusMessage);
            Assert.Less(controller.Timeline.SelectedFrame, controller.Timeline.LatestFrame);
            Assert.IsTrue(controller.SelectLatestFrame(), controller.StatusMessage);

            CharacterTestRuntimeAiLabCommittedFrameReceipt liveBaseline =
                controller.CaptureSnapshot().CommittedFrameReceipt;
            Assert.IsTrue(liveBaseline.IsValid);
            int liveFrame = liveBaseline.Frame;
            long liveAuthorityHash = liveBaseline.AuthorityHash;

            EnqueueRuntimeAiLabCommand(
                sink,
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.DiagnosticsFactsTab);
            Assert.AreEqual(CharacterTestRuntimeAiLabWorkspace.Facts,
                coordinator.CurrentState.ActiveWorkspace);
            EnqueueRuntimeAiLabCommand(
                sink,
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.DiagnosticsRuntimeFrameTab);
            Assert.AreEqual(CharacterTestRuntimeAiLabWorkspace.RuntimeFrame,
                coordinator.CurrentState.ActiveWorkspace);
            EnqueueRuntimeAiLabCommand(
                sink,
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.DiagnosticsReplayTab);
            Assert.AreEqual(CharacterTestRuntimeAiLabWorkspace.Replay,
                coordinator.CurrentState.ActiveWorkspace);

            EnqueueRuntimeAiLabCommand(
                sink,
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.ReplayFreeze);
            CharacterTestRuntimeAiLabReplayValidationWorkspaceSnapshot ready =
                controller.CaptureReplayValidationWorkspaceSnapshot();
            Assert.AreEqual(
                CharacterTestReplayValidationState.Ready,
                ready.State,
                sink.LastStatusMessage + "\n" + ready.Summary + "\n" + ready.ResultEvidence);
            Assert.IsTrue(controller.SelectReplayFrame(ready.StartFrame));
            Assert.AreEqual(
                liveFrame,
                controller.CaptureSnapshot().CommittedFrameReceipt.Frame,
                "Replay-local selection must not mutate the live committed frame.");

            EnqueueRuntimeAiLabCommand(
                sink,
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.ReplayValidate);
            int timeout = 120;
            CharacterTestRuntimeAiLabReplayValidationWorkspaceSnapshot replay =
                controller.CaptureReplayValidationWorkspaceSnapshot();
            while (replay.IsBusy && timeout-- > 0)
            {
                yield return null;
                replay = controller.CaptureReplayValidationWorkspaceSnapshot();
            }
            Assert.AreEqual(CharacterTestReplayValidationState.Passed, replay.State,
                replay.Summary + "\n" + replay.ResultEvidence);
            Assert.AreEqual(liveFrame, controller.CaptureSnapshot().CommittedFrameReceipt.Frame);

            bool returnedRuntimeWorkbenchFromStaleTokenTest = false;
            if (replay.CanOpenFacts)
            {
                EnqueueRuntimeAiLabCommand(
                    sink,
                    CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.ReplayOpenFacts);
                Assert.AreEqual(CharacterTestRuntimeAiLabWorkspace.Facts,
                    coordinator.CurrentState.ActiveWorkspace);
                EnqueueRuntimeAiLabCommand(
                    sink,
                    CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.WorkspaceBack);
                Assert.AreEqual(CharacterTestRuntimeAiLabWorkspace.Replay,
                    coordinator.CurrentState.ActiveWorkspace,
                    sink.LastStatusMessage);
                StringAssert.Contains("frame", sink.LastStatusMessage);

                EnqueueRuntimeAiLabCommand(
                    sink,
                    CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.ReplayOpenFacts);
                Assert.AreEqual(CharacterTestRuntimeAiLabWorkspace.Facts,
                    coordinator.CurrentState.ActiveWorkspace);
                Assert.IsTrue(sink.HasReplayReturnToken);
                controller.ResetTransientUiContextForClose();
                EnqueueRuntimeAiLabCommand(
                    sink,
                    CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.WorkspaceBack);
                Assert.AreEqual(CharacterTestRuntimeAiLabWorkspace.Facts,
                    coordinator.CurrentState.ActiveWorkspace,
                    "A stale Replay return token must fail closed in the evidence workspace.");
                Assert.AreEqual("REPLAY_RETURN_TOKEN_STALE", sink.LastStatusMessage);
                Assert.IsFalse(sink.HasReplayReturnToken);
                EnqueueRuntimeAiLabCommand(
                    sink,
                    CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.WorkspaceBack);
                Assert.AreEqual(CharacterTestRuntimeAiLabWorkspace.RuntimeFrame,
                    coordinator.CurrentState.ActiveWorkspace);
                Assert.AreEqual(
                    CharacterTestRuntimeAiLabTimelineMode.RuntimeTrace,
                    coordinator.CurrentState.PageContext.TimelineMode);
                returnedRuntimeWorkbenchFromStaleTokenTest = true;
            }

            if (!returnedRuntimeWorkbenchFromStaleTokenTest)
            {
                EnqueueRuntimeAiLabCommand(
                    sink,
                    CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.WorkspaceBack);
            }
            Assert.AreEqual(CharacterTestRuntimeAiLabWorkspace.RuntimeFrame,
                coordinator.CurrentState.ActiveWorkspace);
            Assert.AreEqual(
                CharacterTestRuntimeAiLabTimelineMode.RuntimeTrace,
                coordinator.CurrentState.PageContext.TimelineMode);
            Assert.AreEqual(authoringContext.GroupDraftId,
                coordinator.CurrentState.PageContext.GroupDraftId);
            Assert.AreEqual(authoringContext.EntryId,
                coordinator.CurrentState.PageContext.EntryId);
            Assert.AreEqual(authoringContext.CardDraftId,
                coordinator.CurrentState.PageContext.CardDraftId);

            Assert.IsTrue(controller.FreezeReplayValidationCapture(out string pressureFreezeDiagnostic),
                pressureFreezeDiagnostic);
            replay = controller.CaptureReplayValidationWorkspaceSnapshot();
            Assert.IsTrue(replay.CanOpenFacts);

            yield return null;
            global::FairyGUI.GComponent pressureRoot = panelHost.RootForTests;
            int pressurePreviewGeneration = panelHost.PreviewResourceGeneration;
            Assert.IsNotNull(pressureRoot);
            Assert.AreEqual(
                0,
                pressurePreviewGeneration,
                "The unified Runtime workbench must not allocate the removed standalone Behavior Card preview surface.");
            Assert.IsFalse(panelHost.HasBoundPreviewTexture);

            for (int cycle = 0; cycle < 2; cycle++)
            {
                EnqueueRuntimeAiLabCommand(
                    sink,
                    CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.DiagnosticsFactsTab);
                EnqueueRuntimeAiLabCommand(
                    sink,
                    CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.DiagnosticsRuntimeFrameTab);
                EnqueueRuntimeAiLabCommand(
                    sink,
                    CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.DiagnosticsReplayTab);
                if (replay.CanOpenFacts)
                {
                    EnqueueRuntimeAiLabCommand(
                        sink,
                        CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.ReplayOpenFacts);
                    Assert.IsTrue(sink.HasReplayReturnToken);
                    EnqueueRuntimeAiLabCommand(
                        sink,
                        CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.WorkspaceBack);
                    Assert.AreEqual(CharacterTestRuntimeAiLabWorkspace.Replay,
                        coordinator.CurrentState.ActiveWorkspace);
                    Assert.IsFalse(sink.HasReplayReturnToken);
                }
                EnqueueRuntimeAiLabCommand(
                    sink,
                    CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.WorkspaceBack);
                Assert.AreEqual(CharacterTestRuntimeAiLabWorkspace.RuntimeFrame,
                    coordinator.CurrentState.ActiveWorkspace);
                Assert.AreEqual(
                    CharacterTestRuntimeAiLabTimelineMode.RuntimeTrace,
                    coordinator.CurrentState.PageContext.TimelineMode);
                yield return null;
                Assert.IsTrue(panelHost.IsOpen);
                global::FairyGUI.GComponent nextPressureRoot = panelHost.RootForTests;
                Assert.IsNotNull(nextPressureRoot);
                Assert.AreNotSame(pressureRoot, nextPressureRoot);
                Assert.IsTrue(pressureRoot.displayObject == null
                    || pressureRoot.displayObject.isDisposed,
                    "A replaced workspace root must be disposed before the next root is mounted.");
                pressureRoot = nextPressureRoot;
                Assert.AreEqual(
                    pressurePreviewGeneration,
                    panelHost.PreviewResourceGeneration,
                    "Runtime workbench remounts must not recreate the removed standalone Behavior Card preview surface.");
                Assert.IsFalse(panelHost.HasBoundPreviewTexture);
            }

            Assert.AreEqual(CharacterTestRuntimeAiLabWorkspace.RuntimeFrame,
                coordinator.CurrentState.ActiveWorkspace);

            controller.SetFactsSearchText("stale-before-close");
            controller.SelectFactsStableId("fact.stale");
            controller.SetFactsScrollOffsets(12f, 24f, 36f);
            controller.CaptureFactsNavigationReturnToken();

            EnqueueRuntimeAiLabCommand(
                sink,
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.Close);
            Assert.AreEqual(CharacterTestRuntimeAiLabShellVisibility.Visible,
                coordinator.CurrentState.Visibility);
            Assert.AreEqual(CharacterTestRuntimeAiLabWorkspace.PlayerDeck,
                coordinator.CurrentState.ActiveWorkspace);
            Assert.IsFalse(panelHost.IsOpen);
            Assert.IsFalse(panelHost.HasBoundPreviewTexture);
            CharacterTestFairyGuiBehaviorDeckPanelHost deckPanelHost =
                Object.FindAnyObjectByType<CharacterTestFairyGuiBehaviorDeckPanelHost>();
            Assert.IsNotNull(deckPanelHost);
            Assert.IsTrue(deckPanelHost.IsOpen);
            Assert.IsFalse(controller.HasFactsNavigationReturnToken);
            Assert.AreEqual(string.Empty, controller.FactsSearchText);
            Assert.AreEqual(string.Empty, controller.FactsSelectedStableId);
            Assert.AreEqual(0f, controller.FactsFactScrollOffset);
            Assert.AreEqual(CharacterTestReplayValidationState.Empty,
                controller.CaptureReplayValidationWorkspaceSnapshot().State);
            Assert.IsFalse(sink.HasReplayReturnToken);
            Assert.AreEqual(CharacterTestRuntimeAiLabLogicElementKind.None,
                sink.SelectedLogicElementKind);
            Assert.AreEqual(-1, sink.SelectedLogicElementIndex);
            Assert.AreEqual(string.Empty, coordinator.CurrentState.PageContext.GroupDraftId);
            Assert.AreEqual(string.Empty, coordinator.CurrentState.PageContext.EntryId);
            Assert.AreEqual(string.Empty, coordinator.CurrentState.PageContext.CardDraftId);
            controller.ProcessRuntimeAiLabVisibilityShortcut(shortcutPressed: true);
            Assert.AreEqual(CharacterTestRuntimeAiLabShellVisibility.Hidden,
                coordinator.CurrentState.Visibility);
            Assert.IsFalse(deckPanelHost.IsOpen);
            controller.ProcessRuntimeAiLabVisibilityShortcut(shortcutPressed: true);
            Assert.AreEqual(CharacterTestRuntimeAiLabShellVisibility.Visible,
                coordinator.CurrentState.Visibility);
            Assert.AreEqual(CharacterTestRuntimeAiLabWorkspace.PlayerDeck,
                coordinator.CurrentState.ActiveWorkspace);
            Assert.IsTrue(deckPanelHost.IsOpen);
            Assert.IsFalse(panelHost.HasBoundPreviewTexture);
            CharacterTestRuntimeAiPlannerLocalPlayerDeckService rebuiltDeckService =
                gameManager.RuntimeContext.PlayerBehaviorDeckService;
            Assert.IsNotNull(rebuiltDeckService);
            Assert.AreNotSame(initialDeckService, rebuiltDeckService,
                "A rebuilt Slice must own a fresh presentation service.");
            Assert.IsTrue(rebuiltDeckService.Workbench.View.IsSessionLocked);
            AssertInstalledDeckIdentityEqual(initialDeck, rebuiltDeckService.Workbench.Deck);

            Assert.AreEqual(draftWorkspaceId, controller.DraftWorkspace.WorkspaceId);
            Assert.AreEqual(draftRevision, controller.DraftWorkspace.CurrentRevision);
            CharacterTestRuntimeAiLabCommittedFrameReceipt liveAfterReopen =
                controller.CaptureSnapshot().CommittedFrameReceipt;
            Assert.AreEqual(liveFrame, liveAfterReopen.Frame);
            Assert.AreEqual(liveAuthorityHash, liveAfterReopen.AuthorityHash);

            yield return new ExitPlayMode();
        }

        [Test]
        public void RuntimeSessionBuilder_PlayerDeckServiceBoundaryIsOfflineOrExplicitLabLoopbackOnly()
        {
            Assert.IsTrue(CharacterTestRuntimeSessionBuilder.ShouldCreatePlayerBehaviorDeckService(
                hasNetworkRunner: false,
                allowLockedLabPresentation: false));
            Assert.IsTrue(CharacterTestRuntimeSessionBuilder.ShouldCreatePlayerBehaviorDeckService(
                hasNetworkRunner: false,
                allowLockedLabPresentation: true));
            Assert.IsFalse(CharacterTestRuntimeSessionBuilder.ShouldCreatePlayerBehaviorDeckService(
                hasNetworkRunner: true,
                allowLockedLabPresentation: false),
                "Non-Lab network sessions must not create a local authority-shaped deck service.");
            Assert.IsTrue(CharacterTestRuntimeSessionBuilder.ShouldCreatePlayerBehaviorDeckService(
                hasNetworkRunner: true,
                allowLockedLabPresentation: true),
                "Only the explicit Lab Loopback presentation exception may expose a locked local deck.");
        }

        private static void AssertInstalledDeckIdentityEqual(
            CharacterTestRuntimeAiPlannerInstalledDeck expected,
            CharacterTestRuntimeAiPlannerInstalledDeck actual)
        {
            Assert.IsNotNull(expected);
            Assert.IsNotNull(actual);
            Assert.AreEqual(expected.CharacterStableId, actual.CharacterStableId);
            Assert.AreEqual(expected.CatalogId, actual.CatalogId);
            Assert.AreEqual(expected.CatalogRevision, actual.CatalogRevision);
            Assert.AreEqual(expected.CatalogHash, actual.CatalogHash);
            Assert.AreEqual(expected.RuntimeContractVersion, actual.RuntimeContractVersion);
            Assert.AreEqual(expected.SlotCount, actual.SlotCount);
            Assert.AreEqual(expected.TotalCost, actual.TotalCost);
            Assert.AreEqual(expected.DeckHash, actual.DeckHash);
            Assert.AreEqual(expected.Cards.Count, actual.Cards.Count);
            for (int i = 0; i < expected.Cards.Count; i++)
            {
                Assert.AreEqual(expected.Cards[i].StableCardId, actual.Cards[i].StableCardId);
                Assert.AreEqual(expected.Cards[i].Revision, actual.Cards[i].Revision);
                Assert.AreEqual(expected.Cards[i].Cost, actual.Cards[i].Cost);
                Assert.AreEqual(expected.Cards[i].ContentHash, actual.Cards[i].ContentHash);
            }
            Assert.AreEqual(expected.Fallbacks.Count, actual.Fallbacks.Count);
            for (int i = 0; i < expected.Fallbacks.Count; i++)
            {
                Assert.AreEqual(expected.Fallbacks[i].Kind, actual.Fallbacks[i].Kind);
                Assert.AreEqual(expected.Fallbacks[i].StableId, actual.Fallbacks[i].StableId);
                Assert.AreEqual(expected.Fallbacks[i].Cost, actual.Fallbacks[i].Cost);
                Assert.AreEqual(expected.Fallbacks[i].ConsumesSlot, actual.Fallbacks[i].ConsumesSlot);
            }
        }

        private static void EnqueueRuntimeAiLabCommand(
            CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandSink sink,
            string commandId)
        {
            sink.Enqueue(new MxUiCommand(
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.ViewId,
                commandId,
                null));
        }

        private static void AssertReplayEvidenceAvailability(
            CharacterTestRuntimeAiPlannerDevSceneController controller,
            CharacterTestRuntimeAiLabReplayEvidenceTargetKind targetKind,
            bool expectedAvailable)
        {
            bool resolved = controller.TryResolveReplayEvidenceLocator(
                targetKind,
                out CharacterTestRuntimeAiLabReplayEvidenceLocator locator,
                out string absenceCode);
            Assert.AreEqual(expectedAvailable, resolved, absenceCode);
            if (!resolved)
            {
                Assert.IsFalse(string.IsNullOrEmpty(absenceCode));
                return;
            }
            Assert.IsTrue(locator.IsValid);
            Assert.AreEqual(locator.ReplayFrame + 1, locator.LiveFrame);
            Assert.IsTrue(controller.MatchesReplayLocator(locator));
        }

        private static bool TryFindReplayNavigableItem(
            CharacterTestRuntimeAiLabTimelineProjection projection,
            CharacterTestRuntimeAiPlannerDevSceneController controller,
            out CharacterTestRuntimeAiLabTimelineItem item)
        {
            if (projection != null)
            {
                for (int trackIndex = 0; trackIndex < projection.Tracks.Count; trackIndex++)
                {
                    IReadOnlyList<CharacterTestRuntimeAiLabTimelineItem> items =
                        projection.Tracks[trackIndex].Items;
                    for (int itemIndex = 0; itemIndex < items.Count; itemIndex++)
                    {
                        CharacterTestRuntimeAiLabTimelineItem candidate = items[itemIndex];
                        if (controller.TryResolveReplayTimelineLocator(
                            candidate.CreateLocator(),
                            useEndFrame: false,
                            out _))
                        {
                            item = candidate;
                            return true;
                        }
                    }
                }
            }
            item = null;
            return false;
        }
    }
}
