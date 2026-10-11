using System;
using System.Reflection;
using MxFramework.Game.CharacterTest;
using MxFramework.Gameplay.Triggers;
using MxFramework.UI;
using UnityEngine;
using Object = UnityEngine.Object;

namespace MxFramework.Tests.Gameplay.TriggerAutomation
{
    internal sealed class TriggerTestRuntimeAiLabPlayerPublishFlowArguments :
        ITriggerTestFunctionArguments
    {
        internal static readonly TriggerTestRuntimeAiLabPlayerPublishFlowArguments Instance =
            new TriggerTestRuntimeAiLabPlayerPublishFlowArguments();

        private TriggerTestRuntimeAiLabPlayerPublishFlowArguments()
        {
        }
    }

    /// <summary>
    /// Drives the complete local player authoring and publication flow through
    /// the live FairyGUI command boundaries.
    /// </summary>
    /// <remarks>
    /// Allocation classification: LowFreqAlloc. This test-only adapter runs
    /// only after an explicit manual Trigger request.
    /// </remarks>
    internal sealed class TriggerTestRuntimeAiLabPlayerPublishFlowFunction :
        TriggerTestFunction<TriggerTestRuntimeAiLabPlayerPublishFlowArguments>
    {
        internal static readonly TriggerTestFunctionId Id =
            new TriggerTestFunctionId(
                "mx.trigger.test.function.character-test.runtime-ai-lab-player-publish-flow");

        public override TriggerTestFunctionId FunctionId => Id;

        public override TriggerTestFunctionCapability Capability =>
            TriggerTestFunctionCapability.UnityAdapter;

        protected override TriggerTestOperationResult ExecuteTyped(
            in TriggerTestFunctionContext context,
            TriggerTestRuntimeAiLabPlayerPublishFlowArguments arguments)
        {
            if (!Application.isPlaying)
                return TriggerTestOperationResult.Failed("Unity is not in Play Mode.");
            if (!context.Fixture.Services.TryGet(
                    out TriggerTestRuntimeAiLabPlayerPublishFlowProbe probe))
            {
                return TriggerTestOperationResult.Failed(
                    "Runtime AI Lab player publish probe is unavailable.");
            }
            probe.InvocationCount++;

            if (!TriggerTestRuntimeAiLabCommandProbe.TryResolveController(
                    out CharacterTestRuntimeAiPlannerDevSceneController controller,
                    out string diagnostic))
                return Fail(probe, "resolve-controller", diagnostic);
            probe.Controller = controller;
            if (controller.CaptureSnapshot().Phase !=
                CharacterTestRuntimeAiPlannerDevScenePhase.Setup)
            {
                return Fail(
                    probe,
                    "require-setup",
                    "The player publish flow must start from Setup.");
            }
            if (controller.ShellCoordinator?.CurrentState?.ActiveWorkspace !=
                CharacterTestRuntimeAiLabWorkspace.PlayerDeck)
            {
                return Fail(
                    probe,
                    "require-player-deck",
                    "The live shell is not showing Player Deck.");
            }

            // Fork the Lab authoring state into a transient recovery root before this manual
            // acceptance flow edits anything. The production composition root is the only
            // caller allowed to opt into the user's latest-workspace pointer.
            controller.PrepareInitialSetupState(restorePersistedDraft: false);

            GameManager gameManager = Object.FindAnyObjectByType<GameManager>();
            CharacterTestRuntimeAiPlannerLocalPlayerDeckService deckService =
                gameManager?.RuntimeContext?.PlayerBehaviorDeckService;
            CharacterTestFairyGuiBehaviorDeckPanelHost deckHost =
                gameManager?.RuntimeContext?.UiRuntime?.FairyGuiBehaviorDeckPanelHost;
            if (deckService == null || deckHost == null || !deckHost.IsOpen)
            {
                return Fail(
                    probe,
                    "resolve-player-deck",
                    "The live Player Deck service or FairyGUI panel is unavailable.");
            }

            if (!TrySelectAuthorableLibraryCard(
                    deckService,
                    deckHost.CommandSink.View,
                    out CharacterTestRuntimeAiPlannerPublishedBehaviorCard source,
                    out bool sourceWasInstalled,
                    out diagnostic))
                return Fail(probe, "select-card", diagnostic);
            probe.StableCardId = source.StableCardId;
            probe.SourcePublishedRevision = source.Revision;

            EnqueueDeckCardAction(
                deckHost.CommandSink,
                source.StableCardId,
                CharacterTestFairyGuiBehaviorDeckCardAction.EditInLab,
                sourceWasInstalled
                    ? CharacterTestFairyGuiBehaviorDeckCardOrigin.InstalledSlot
                    : CharacterTestFairyGuiBehaviorDeckCardOrigin.Library);
            if (!TryResolveBehaviorCardPanel(
                    controller,
                    out CharacterTestFairyGuiRuntimeAiPlannerDevPanelHost panelHost,
                    out diagnostic))
                return Fail(probe, "open-cow-draft", diagnostic);

            CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandSink panelSink =
                panelHost.CommandSink;
            if (!TryReadFirstDetector(
                    panelHost,
                    out string detectorId,
                    out CharacterTestRuntimeAiLabLogicDetectorTrigger originalTrigger,
                    out diagnostic))
                return Fail(probe, "read-detector", diagnostic);
            probe.DetectorId = detectorId;
            if (!TryCanonicalizeDetectorWindows(
                    panelHost,
                    panelSink,
                    out diagnostic)
                || !TryReadDetector(
                    panelHost,
                    detectorId,
                    out originalTrigger,
                    out diagnostic))
            {
                return Fail(probe, "canonicalize-draft", diagnostic);
            }
            probe.OriginalTrigger = originalTrigger;

            EnqueueBehaviorCardCommand(
                panelSink,
                CharacterTestFairyGuiRuntimeAiLabBehaviorCardCommandIds.CycleDetectorTrigger,
                detectorId);
            if (!TryReadDetector(
                    panelHost,
                    detectorId,
                    out CharacterTestRuntimeAiLabLogicDetectorTrigger editedTrigger,
                    out diagnostic)
                || editedTrigger == originalTrigger)
            {
                return Fail(
                    probe,
                    "edit-draft",
                    string.IsNullOrEmpty(diagnostic)
                        ? "The detector trigger did not change."
                        : diagnostic);
            }
            probe.EditedTrigger = editedTrigger;

            EnqueuePanelCommand(
                panelSink,
                CharacterTestFairyGuiRuntimeAiLabBehaviorCardCommandIds.UndoStageEdit);
            if (!TryReadDetector(
                    panelHost,
                    detectorId,
                    out CharacterTestRuntimeAiLabLogicDetectorTrigger undoTrigger,
                    out diagnostic))
                return Fail(probe, "undo-draft", diagnostic);
            probe.UndoTrigger = undoTrigger;

            EnqueuePanelCommand(
                panelSink,
                CharacterTestFairyGuiRuntimeAiLabBehaviorCardCommandIds.RedoStageEdit);
            if (!TryReadDetector(
                    panelHost,
                    detectorId,
                    out CharacterTestRuntimeAiLabLogicDetectorTrigger redoTrigger,
                    out diagnostic))
                return Fail(probe, "redo-draft", diagnostic);
            probe.RedoTrigger = redoTrigger;

            EnqueuePanelCommand(
                panelSink,
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.PublishDraft);
            if (!deckService.TryResolveLatestPublishedCard(
                    source.StableCardId,
                    out CharacterTestRuntimeAiPlannerPublishedBehaviorCard published)
                || published.Revision != source.Revision + 1)
            {
                return Fail(
                    probe,
                    "publish-draft",
                    "Local Publish did not create the next immutable card revision. " +
                    panelSink.LastStatusMessage);
            }
            probe.PublishedRevision = published.Revision;

            EnqueuePanelCommand(
                panelSink,
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.Close);
            deckHost = gameManager.RuntimeContext.UiRuntime.FairyGuiBehaviorDeckPanelHost;
            if (deckHost == null || !deckHost.IsOpen)
            {
                return Fail(
                    probe,
                    "return-player-deck",
                    "Player Deck did not reopen after leaving the Lab.");
            }

            string displacedStableCardId = string.Empty;
            if (sourceWasInstalled)
            {
                EnqueueDeckCardAction(
                    deckHost.CommandSink,
                    source.StableCardId,
                    CharacterTestFairyGuiBehaviorDeckCardAction.Remove,
                    CharacterTestFairyGuiBehaviorDeckCardOrigin.InstalledSlot);
            }
            else if (TryFindDeckCard(
                    deckHost.CommandSink.View,
                    source.StableCardId,
                    out CharacterTestRuntimeAiPlannerPlayerDeckCardView pendingInstall)
                && !pendingInstall.CanInstall)
            {
                if (!TryFindHighestCostRemovableCard(
                        deckHost.CommandSink.View,
                        out CharacterTestRuntimeAiPlannerPlayerDeckCardView displaced))
                {
                    return Fail(
                        probe,
                        "prepare-install-capacity",
                        "No removable installed card can make room for the published card.");
                }
                displacedStableCardId = displaced.StableCardId;
                EnqueueDeckCardAction(
                    deckHost.CommandSink,
                    displacedStableCardId,
                    CharacterTestFairyGuiBehaviorDeckCardAction.Remove,
                    CharacterTestFairyGuiBehaviorDeckCardOrigin.InstalledSlot);
            }

            probe.RemovedFromDeck = TryFindDeckCard(
                deckHost.CommandSink.View,
                source.StableCardId,
                out CharacterTestRuntimeAiPlannerPlayerDeckCardView removedCard)
                && !removedCard.IsInstalled;
            if (!probe.RemovedFromDeck)
                return Fail(probe, "verify-library-card", deckHost.CommandSink.LastStatusMessage);

            EnqueueDeckCardAction(
                deckHost.CommandSink,
                source.StableCardId,
                CharacterTestFairyGuiBehaviorDeckCardAction.Install,
                CharacterTestFairyGuiBehaviorDeckCardOrigin.Library);
            probe.ReinstalledInDeck = TryFindDeckCard(
                deckHost.CommandSink.View,
                source.StableCardId,
                out CharacterTestRuntimeAiPlannerPlayerDeckCardView installedCard)
                && installedCard.IsInstalled
                && installedCard.Revision == published.Revision;
            if (!probe.ReinstalledInDeck)
                return Fail(probe, "reinstall-published-card", deckHost.CommandSink.LastStatusMessage);

            EnqueueDeckCardAction(
                deckHost.CommandSink,
                source.StableCardId,
                CharacterTestFairyGuiBehaviorDeckCardAction.EditInLab,
                CharacterTestFairyGuiBehaviorDeckCardOrigin.InstalledSlot);
            if (!TryResolveBehaviorCardPanel(controller, out panelHost, out diagnostic))
                return Fail(probe, "reopen-published-card", diagnostic);
            panelSink = panelHost.CommandSink;

            if (!TryReadFirstDetector(
                    panelHost,
                    out _,
                    out _,
                    out diagnostic))
                return Fail(probe, "verify-reopened-card", diagnostic);

            EnqueuePanelCommand(
                panelSink,
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.Close);
            deckHost = gameManager.RuntimeContext.UiRuntime.FairyGuiBehaviorDeckPanelHost;
            if (deckHost == null || !deckHost.IsOpen)
            {
                return Fail(
                    probe,
                    "restore-runnable-setup",
                    "Player Deck did not reopen before returning to Setup.");
            }
            EnqueuePanelCommand(
                deckHost.CommandSink,
                CharacterTestFairyGuiBehaviorDeckPanelCommandIds.ContinueToSetup,
                CharacterTestFairyGuiBehaviorDeckPanelIds.ViewId);
            panelHost = Object.FindAnyObjectByType<
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelHost>();
            panelSink = panelHost?.CommandSink;
            if (panelHost == null
                || panelSink == null
                || controller.ShellCoordinator?.CurrentState?.ActiveWorkspace !=
                    CharacterTestRuntimeAiLabWorkspace.Setup
                || !TryPrepareRunnableApplyGroup(
                    controller,
                    out int liveEntryIndex,
                    out string liveDetectorId,
                    out diagnostic))
            {
                return Fail(probe, "restore-runnable-setup", diagnostic);
            }

            EnqueuePanelCommand(
                panelSink,
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.Start);
            CharacterTestRuntimeAiPlannerDevSceneSnapshot started =
                controller.CaptureSnapshot();
            if (started.Phase != CharacterTestRuntimeAiPlannerDevScenePhase.Running
                || !started.Paused)
            {
                return Fail(
                    probe,
                    "start-session",
                    "The reopened published card did not start a paused session. " +
                    panelSink.LastStatusMessage);
            }

            panelHost = Object.FindAnyObjectByType<
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelHost>();
            panelSink = panelHost?.CommandSink;
            if (panelHost == null || panelSink == null)
                return Fail(probe, "resolve-live-editor", "The live Lab panel is unavailable.");
            EnqueuePanelCommand(
                panelSink,
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.Step);
            started = controller.CaptureSnapshot();
            if (started.Phase != CharacterTestRuntimeAiPlannerDevScenePhase.Running
                || !started.Paused
                || started.Timeline == null
                || started.Timeline.Count == 0)
            {
                return Fail(
                    probe,
                    "establish-live-authority",
                    panelSink.LastStatusMessage);
            }

            EnqueuePanelCommand(
                panelSink,
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.OpenBehaviorGroup);
            EnqueuePanelCommand(
                panelSink,
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.SelectWorkbenchEntry(
                    liveEntryIndex));
            EnqueuePanelCommand(
                panelSink,
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.BehaviorCardTab);
            if (!TryReadDetector(
                    panelHost,
                    liveDetectorId,
                    out CharacterTestRuntimeAiLabLogicDetectorTrigger liveSourceTrigger,
                    out diagnostic))
                return Fail(probe, "resolve-live-editor", diagnostic);
            if (!TryCanonicalizeDetectorWindows(
                    panelHost,
                    panelSink,
                    out diagnostic)
                || !TryReadDetector(
                    panelHost,
                    liveDetectorId,
                    out liveSourceTrigger,
                    out diagnostic))
            {
                return Fail(probe, "canonicalize-live-draft", diagnostic);
            }

            EnqueueBehaviorCardCommand(
                panelSink,
                CharacterTestFairyGuiRuntimeAiLabBehaviorCardCommandIds.CycleDetectorTrigger,
                liveDetectorId);
            if (!TryReadDetector(
                    panelHost,
                    liveDetectorId,
                    out CharacterTestRuntimeAiLabLogicDetectorTrigger liveEditedTrigger,
                    out diagnostic)
                || liveEditedTrigger == liveSourceTrigger
                || controller.CandidateRevision == null
                || controller.CandidateState !=
                    CharacterTestRuntimeAiLabCandidateState.PreviewComplete)
            {
                return Fail(
                    probe,
                    "edit-live-draft",
                    string.IsNullOrEmpty(diagnostic)
                        ? controller.CandidateDiagnostic
                        : diagnostic);
            }

            probe.LiveFrameBeforeApply = started.Timeline.LatestFrame;
            probe.AppliedEventCountBefore = controller.AppliedRevisionEventCount;
            probe.ActiveAuthorityHashBefore =
                controller.ActiveRevision?.AuthorityHash ?? 0L;
            probe.CandidateAuthorityHash =
                controller.CandidateRevision.AuthorityHash;

            CharacterTestRuntimeAiPlannerDevSceneSnapshot applySnapshot =
                controller.CaptureSnapshot();
            int agentCount = applySnapshot.SelectedFrame?.Agents?.Count ?? 0;
            for (int agentIndex = 0;
                 agentIndex < agentCount
                 && controller.CandidateState !=
                    CharacterTestRuntimeAiLabCandidateState.ApplyPending;
                 agentIndex++)
            {
                panelSink.Enqueue(new MxUiCommand(
                    CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.ViewId,
                    CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds
                        .RuntimeTestSelectAgent,
                    agentIndex));
                EnqueuePanelCommand(
                    panelSink,
                    CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.ApplyDraft);
            }
            if (controller.CandidateState !=
                    CharacterTestRuntimeAiLabCandidateState.ApplyPending
                || controller.QueuedCandidateRevision == null
                || controller.QueuedCandidateRevision.AuthorityHash !=
                    probe.CandidateAuthorityHash)
                return Fail(probe, "queue-apply", panelSink.LastStatusMessage);

            if (!TryProcessPendingCandidateApply(controller, out diagnostic))
                return Fail(probe, "process-apply-boundary", diagnostic);
            EnqueuePanelCommand(
                panelSink,
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.Step);
            CharacterTestRuntimeAiPlannerDevSceneSnapshot applied =
                controller.CaptureSnapshot();
            probe.LiveFrameAfterApply = applied.Timeline.LatestFrame;
            probe.AppliedEventCountAfter = controller.AppliedRevisionEventCount;
            probe.LastAppliedRevisionFrame = controller.LastAppliedRevisionFrame;
            probe.ActiveAuthorityHashAfter =
                controller.ActiveRevision?.AuthorityHash ?? 0L;

            if (!sourceWasInstalled || !string.IsNullOrEmpty(displacedStableCardId))
            {
                EnqueuePanelCommand(
                    panelSink,
                    CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds.Close);
                deckHost = gameManager.RuntimeContext.UiRuntime.FairyGuiBehaviorDeckPanelHost;
                if (deckHost == null || !deckHost.IsOpen)
                {
                    return Fail(
                        probe,
                        "restore-deck",
                        "Player Deck did not reopen for acceptance cleanup.");
                }
                if (!sourceWasInstalled)
                {
                    EnqueueDeckCardAction(
                        deckHost.CommandSink,
                        source.StableCardId,
                        CharacterTestFairyGuiBehaviorDeckCardAction.Remove,
                        CharacterTestFairyGuiBehaviorDeckCardOrigin.InstalledSlot);
                }
                EnqueueDeckCardAction(
                    deckHost.CommandSink,
                    displacedStableCardId,
                    CharacterTestFairyGuiBehaviorDeckCardAction.Install,
                    CharacterTestFairyGuiBehaviorDeckCardOrigin.Library);
                if (!TryFindDeckCard(
                        deckHost.CommandSink.View,
                        displacedStableCardId,
                        out CharacterTestRuntimeAiPlannerPlayerDeckCardView restored)
                    || !restored.IsInstalled)
                {
                    return Fail(
                        probe,
                        "restore-deck",
                        deckHost.CommandSink.LastStatusMessage);
                }
            }
            probe.FinalStage = "complete";

            Debug.Log(
                "[TriggerTest][RuntimeAiLabPlayerPublishFlow][PASS] " +
                source.StableCardId +
                " r" +
                source.Revision +
                " -> r" +
                published.Revision +
                ", Apply F+1 frame " +
                probe.LiveFrameAfterApply);
            return TriggerTestOperationResult.Succeeded(
                "Player Deck -> COW Draft -> Undo/Redo -> Publish -> " +
                "reinstall/reopen -> Apply F+1 completed.");
        }

        private static TriggerTestOperationResult Fail(
            TriggerTestRuntimeAiLabPlayerPublishFlowProbe probe,
            string stage,
            string diagnostic)
        {
            probe.FinalStage = stage ?? string.Empty;
            return TriggerTestOperationResult.Failed(
                probe.FinalStage +
                ": " +
                (string.IsNullOrWhiteSpace(diagnostic)
                    ? "No diagnostic was provided."
                    : diagnostic));
        }

        private static bool TrySelectAuthorableLibraryCard(
            CharacterTestRuntimeAiPlannerLocalPlayerDeckService service,
            CharacterTestRuntimeAiPlannerPlayerDeckWorkbenchView view,
            out CharacterTestRuntimeAiPlannerPublishedBehaviorCard source,
            out bool sourceWasInstalled,
            out string diagnostic)
        {
            source = null;
            sourceWasInstalled = false;
            int maximumRemovableCost = 0;
            if (view?.Cards != null)
            {
                for (int i = 0; i < view.Cards.Count; i++)
                {
                    CharacterTestRuntimeAiPlannerPlayerDeckCardView installed =
                        view.Cards[i];
                    if (installed != null
                        && installed.IsInstalled
                        && installed.CanRemove)
                    {
                        maximumRemovableCost = Math.Max(
                            maximumRemovableCost,
                            installed.Cost);
                    }
                }
            }
            if (TrySelectAuthorableCard(
                    service,
                    view,
                    requireInstalled: false,
                    maximumRemovableCost,
                    out source))
            {
                diagnostic = string.Empty;
                return true;
            }
            if (TrySelectAuthorableCard(
                    service,
                    view,
                    requireInstalled: true,
                    maximumRemovableCost,
                    out source))
            {
                sourceWasInstalled = true;
                diagnostic = string.Empty;
                return true;
            }

            diagnostic =
                "No installable Library card with an isolated COW path and editable detector was found.";
            return false;
        }

        private static bool TrySelectAuthorableCard(
            CharacterTestRuntimeAiPlannerLocalPlayerDeckService service,
            CharacterTestRuntimeAiPlannerPlayerDeckWorkbenchView view,
            bool requireInstalled,
            int maximumRemovableCost,
            out CharacterTestRuntimeAiPlannerPublishedBehaviorCard source)
        {
            source = null;
            if (view?.Cards == null)
                return false;
            for (int i = 0; i < view.Cards.Count; i++)
            {
                CharacterTestRuntimeAiPlannerPlayerDeckCardView card =
                    view.Cards[i];
                if (card == null
                    || card.IsInstalled != requireInstalled
                    || requireInstalled && !card.CanRemove
                    || !requireInstalled && card.Cost > maximumRemovableCost
                    || card.Availability !=
                        CharacterTestRuntimeAiPlannerPlayerDeckCardAvailability.Available
                    || !service.TryResolveLatestPublishedCard(
                        card.StableCardId,
                        out CharacterTestRuntimeAiPlannerPublishedBehaviorCard candidate))
                {
                    continue;
                }

                CharacterTestRuntimeAiLabBehaviorCardLogicPayload logic =
                    candidate.CloneLogic();
                if (logic?.Detectors == null || logic.Detectors.Count == 0)
                    continue;
                source = candidate;
                return true;
            }

            return false;
        }

        private static bool TryResolveBehaviorCardPanel(
            CharacterTestRuntimeAiPlannerDevSceneController controller,
            out CharacterTestFairyGuiRuntimeAiPlannerDevPanelHost panelHost,
            out string diagnostic)
        {
            panelHost = Object.FindAnyObjectByType<
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelHost>();
            if (panelHost != null
                && panelHost.IsOpen
                && panelHost.CommandSink != null
                && panelHost.LastViewModel != null
                && controller.ShellCoordinator?.CurrentState?.ActiveWorkspace ==
                    CharacterTestRuntimeAiLabWorkspace.BehaviorCard)
            {
                diagnostic = string.Empty;
                return true;
            }

            diagnostic = "The live FairyGUI Behavior Card workspace is not open.";
            return false;
        }

        private static bool TryReadFirstDetector(
            CharacterTestFairyGuiRuntimeAiPlannerDevPanelHost panelHost,
            out string detectorId,
            out CharacterTestRuntimeAiLabLogicDetectorTrigger trigger,
            out string diagnostic)
        {
            detectorId = string.Empty;
            trigger = default;
            CharacterTestRuntimeAiLabBehaviorCardTimelineViewModel timeline =
                panelHost?.LastViewModel?.BehaviorCardWorkspace?.Timeline;
            if (timeline?.Detectors == null || timeline.DetectorCount <= 0)
            {
                diagnostic = "The Behavior Card workspace has no editable detector.";
                return false;
            }

            CharacterTestRuntimeAiLabLogicDetectorViewModel detector =
                timeline.Detectors[0];
            if (detector == null || string.IsNullOrEmpty(detector.DetectorId))
            {
                diagnostic = "The first detector has no stable identity.";
                return false;
            }

            detectorId = detector.DetectorId;
            trigger = detector.Trigger;
            diagnostic = string.Empty;
            return true;
        }

        private static bool TryReadDetector(
            CharacterTestFairyGuiRuntimeAiPlannerDevPanelHost panelHost,
            string detectorId,
            out CharacterTestRuntimeAiLabLogicDetectorTrigger trigger,
            out string diagnostic)
        {
            trigger = default;
            CharacterTestRuntimeAiLabBehaviorCardTimelineViewModel timeline =
                panelHost?.LastViewModel?.BehaviorCardWorkspace?.Timeline;
            if (timeline?.Detectors != null)
            {
                int count = Math.Min(timeline.DetectorCount, timeline.Detectors.Length);
                for (int i = 0; i < count; i++)
                {
                    CharacterTestRuntimeAiLabLogicDetectorViewModel detector =
                        timeline.Detectors[i];
                    if (detector != null
                        && string.Equals(
                            detector.DetectorId,
                            detectorId,
                            StringComparison.Ordinal))
                    {
                        trigger = detector.Trigger;
                        diagnostic = string.Empty;
                        return true;
                    }
                }
            }

            diagnostic =
                "Detector " + detectorId + " is not present in the live workspace.";
            return false;
        }

        private static bool TryFindDeckCard(
            CharacterTestRuntimeAiPlannerPlayerDeckWorkbenchView view,
            string stableCardId,
            out CharacterTestRuntimeAiPlannerPlayerDeckCardView card)
        {
            if (view?.Cards != null)
            {
                for (int i = 0; i < view.Cards.Count; i++)
                {
                    CharacterTestRuntimeAiPlannerPlayerDeckCardView candidate =
                        view.Cards[i];
                    if (candidate != null
                        && string.Equals(
                            candidate.StableCardId,
                            stableCardId,
                            StringComparison.Ordinal))
                    {
                        card = candidate;
                        return true;
                    }
                }
            }

            card = null;
            return false;
        }

        private static bool TryProcessPendingCandidateApply(
            CharacterTestRuntimeAiPlannerDevSceneController controller,
            out string diagnostic)
        {
            MethodInfo process = typeof(CharacterTestRuntimeAiPlannerDevSceneController)
                .GetMethod(
                    "ProcessPendingCandidateApply",
                    BindingFlags.Instance | BindingFlags.NonPublic);
            if (process == null)
            {
                diagnostic =
                    "The Runtime AI Lab Apply F+1 update boundary cannot be resolved.";
                return false;
            }

            process.Invoke(controller, null);
            if (controller.CandidateState ==
                CharacterTestRuntimeAiLabCandidateState.Applied)
            {
                diagnostic = string.Empty;
                return true;
            }

            diagnostic = controller.CandidateDiagnostic;
            return false;
        }

        private static bool TryCanonicalizeDetectorWindows(
            CharacterTestFairyGuiRuntimeAiPlannerDevPanelHost panelHost,
            CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandSink panelSink,
            out string diagnostic)
        {
            CharacterTestRuntimeAiLabBehaviorCardTimelineViewModel timeline =
                panelHost?.LastViewModel?.BehaviorCardWorkspace?.Timeline;
            if (timeline == null)
            {
                diagnostic = "The Behavior Card timeline is unavailable.";
                return false;
            }
            if (!timeline.RequiresDetectorWindowCanonicalization)
            {
                diagnostic = string.Empty;
                return true;
            }

            EnqueuePanelCommand(
                panelSink,
                CharacterTestFairyGuiRuntimeAiLabBehaviorCardCommandIds
                    .CanonicalizeDetectorWindows);
            EnqueuePanelCommand(
                panelSink,
                CharacterTestFairyGuiRuntimeAiLabBehaviorCardCommandIds
                    .CanonicalizeDetectorWindows);
            timeline = panelHost.LastViewModel?.BehaviorCardWorkspace?.Timeline;
            if (timeline != null
                && !timeline.RequiresDetectorWindowCanonicalization)
            {
                diagnostic = string.Empty;
                return true;
            }

            diagnostic = panelSink.LastStatusMessage;
            return false;
        }

        private static bool TryFindHighestCostRemovableCard(
            CharacterTestRuntimeAiPlannerPlayerDeckWorkbenchView view,
            out CharacterTestRuntimeAiPlannerPlayerDeckCardView card)
        {
            card = null;
            if (view?.Cards == null)
                return false;
            for (int i = 0; i < view.Cards.Count; i++)
            {
                CharacterTestRuntimeAiPlannerPlayerDeckCardView candidate =
                    view.Cards[i];
                if (candidate == null
                    || !candidate.IsInstalled
                    || !candidate.CanRemove
                    || card != null && candidate.Cost <= card.Cost)
                {
                    continue;
                }
                card = candidate;
            }

            return card != null;
        }

        private static bool TryPrepareRunnableApplyGroup(
            CharacterTestRuntimeAiPlannerDevSceneController controller,
            out int entryIndex,
            out string detectorId,
            out string diagnostic)
        {
            entryIndex = -1;
            detectorId = string.Empty;
            diagnostic = string.Empty;
            // Use the user's current group as-is. This live Trigger must never rename or
            // repopulate a persisted Runtime AI Lab workspace with the legacy chase/dodge
            // fixture; isolated unit tests still exercise that fixture through transient drafts.
            if (!controller.DraftWorkspace.TryGetGroupForSlot(
                    controller.SelectedSetupTeamId,
                    controller.SelectedSetupSlotIndex,
                    out CharacterTestRuntimeAiLabBehaviorGroupDraft group)
                || !CharacterTestRuntimeAiPlannerBehaviorCoreSlotPolicy.Validate(
                    group.Profile.Entries,
                    null).Success
                || !controller.TryValidateSessionStartConfiguration(
                    out diagnostic))
            {
                return false;
            }

            for (int i = 0; i < group.Entries.Count; i++)
            {
                CharacterTestRuntimeAiLabBehaviorEntryDraft entry =
                    group.Entries[i];
                if (entry == null
                    || !string.Equals(
                        entry.BehaviorId,
                        CharacterTestRuntimeAiPlannerBehaviorCompositionCatalog
                            .FoundationApproachAndAttack,
                        StringComparison.Ordinal)
                    || !controller.DraftWorkspace.Cards.TryGetValue(
                        entry.CardDraftId,
                        out CharacterTestRuntimeAiLabBehaviorCardDraft card)
                    || card.LogicPayload?.Detectors == null
                    || card.LogicPayload.Detectors.Count == 0)
                {
                    continue;
                }

                entryIndex = i;
                detectorId = card.LogicPayload.Detectors[0].DetectorId;
                if (!string.IsNullOrEmpty(detectorId))
                {
                    diagnostic = string.Empty;
                    return true;
                }
            }

            diagnostic =
                "The current behavior group has no editable compatibility detector for this legacy publish-flow check.";
            return false;
        }

        private static void EnqueueDeckCardAction(
            CharacterTestFairyGuiBehaviorDeckPanelCommandSink sink,
            string stableCardId,
            CharacterTestFairyGuiBehaviorDeckCardAction action,
            CharacterTestFairyGuiBehaviorDeckCardOrigin origin)
        {
            sink.Enqueue(new MxUiCommand(
                CharacterTestFairyGuiBehaviorDeckPanelIds.ViewId,
                CharacterTestFairyGuiBehaviorDeckPanelCommandIds.CardAction,
                new CharacterTestFairyGuiBehaviorDeckCardActionPayload
                {
                    StableCardId = stableCardId,
                    Action = action,
                    Origin = origin,
                    ActionRole =
                        CharacterTestFairyGuiBehaviorDeckCardActionRole.Primary
                }));
        }

        private static void EnqueueBehaviorCardCommand(
            CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandSink sink,
            string commandId,
            string detectorId)
        {
            sink.Enqueue(new MxUiCommand(
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.ViewId,
                commandId,
                new CharacterTestRuntimeAiLabBehaviorCardElementCommand(
                    detectorId,
                    kind: CharacterTestRuntimeAiLabLogicElementKind.Detector)));
        }

        private static void EnqueuePanelCommand(
            CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandSink sink,
            string commandId)
        {
            EnqueuePanelCommand(
                sink,
                commandId,
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.ViewId);
        }

        private static void EnqueuePanelCommand(
            IMxUiCommandSink sink,
            string commandId,
            MxUiViewId viewId)
        {
            sink.Enqueue(new MxUiCommand(
                viewId,
                commandId,
                null));
        }
    }

    internal sealed class TriggerTestRuntimeAiLabPlayerPublishFlowProbe
    {
        internal CharacterTestRuntimeAiPlannerDevSceneController Controller;
        internal int InvocationCount;
        internal string StableCardId = string.Empty;
        internal string DetectorId = string.Empty;
        internal int SourcePublishedRevision;
        internal int PublishedRevision;
        internal CharacterTestRuntimeAiLabLogicDetectorTrigger OriginalTrigger;
        internal CharacterTestRuntimeAiLabLogicDetectorTrigger EditedTrigger;
        internal CharacterTestRuntimeAiLabLogicDetectorTrigger UndoTrigger;
        internal CharacterTestRuntimeAiLabLogicDetectorTrigger RedoTrigger;
        internal bool RemovedFromDeck;
        internal bool ReinstalledInDeck;
        internal int LiveFrameBeforeApply;
        internal int LiveFrameAfterApply;
        internal int AppliedEventCountBefore;
        internal int AppliedEventCountAfter;
        internal int LastAppliedRevisionFrame;
        internal long ActiveAuthorityHashBefore;
        internal long CandidateAuthorityHash;
        internal long ActiveAuthorityHashAfter;
        internal string FinalStage = string.Empty;
    }

    internal sealed class TriggerTestRuntimeAiLabPlayerPublishFlowAssertion :
        ITriggerTestAssertion
    {
        public string AssertionId => "player-publish-flow-completed";

        public TriggerTestOperationResult Evaluate(
            in TriggerTestAssertionContext context)
        {
            if (!context.Fixture.Services.TryGet(
                    out TriggerTestRuntimeAiLabPlayerPublishFlowProbe probe))
            {
                return TriggerTestOperationResult.Failed(
                    "Runtime AI Lab player publish probe is unavailable.");
            }
            if (probe.InvocationCount != 1
                || probe.Controller == null
                || !string.Equals(probe.FinalStage, "complete", StringComparison.Ordinal))
            {
                return TriggerTestOperationResult.Failed(
                    "The player publish flow did not complete exactly once; last stage: " +
                    probe.FinalStage);
            }
            if (probe.PublishedRevision != probe.SourcePublishedRevision + 1)
            {
                return TriggerTestOperationResult.Failed(
                    "Local Publish did not advance the immutable card revision by one.");
            }
            if (probe.EditedTrigger == probe.OriginalTrigger
                || probe.UndoTrigger != probe.OriginalTrigger
                || probe.RedoTrigger != probe.EditedTrigger)
            {
                return TriggerTestOperationResult.Failed(
                    "Detector edit, Undo and Redo did not preserve the expected semantic states.");
            }
            if (!probe.RemovedFromDeck || !probe.ReinstalledInDeck)
            {
                return TriggerTestOperationResult.Failed(
                    "The published Library card was not installed through Player Deck.");
            }
            if (probe.LiveFrameAfterApply != probe.LiveFrameBeforeApply + 1
                || probe.AppliedEventCountAfter != probe.AppliedEventCountBefore + 1
                || probe.LastAppliedRevisionFrame != probe.LiveFrameAfterApply)
            {
                return TriggerTestOperationResult.Failed(
                    "Apply F+1 did not commit on the next live frame.");
            }
            if (probe.CandidateAuthorityHash == 0L
                || probe.ActiveAuthorityHashAfter != probe.CandidateAuthorityHash
                || probe.ActiveAuthorityHashAfter == probe.ActiveAuthorityHashBefore)
            {
                return TriggerTestOperationResult.Failed(
                    "The active Runtime AI Planner revision does not match the queued candidate.");
            }

            return TriggerTestOperationResult.Succeeded(
                "Verified Player Deck, COW Draft, Undo/Redo, local Publish, " +
                "remove/install/reopen and Apply F+1 for " +
                probe.StableCardId +
                " r" +
                probe.PublishedRevision +
                ".");
        }
    }
}
