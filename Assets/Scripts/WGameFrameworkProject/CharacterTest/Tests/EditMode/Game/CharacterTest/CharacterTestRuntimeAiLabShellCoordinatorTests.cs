using System;
using System.Collections.Generic;
using MxFramework.Game.CharacterTest;
using NUnit.Framework;

namespace MxFramework.Tests.Game.CharacterTest
{
    public sealed class CharacterTestRuntimeAiLabShellCoordinatorTests
    {
        [TestCase(false)]
        [TestCase(true)]
        public void PendingGroupMountUsesTargetNavigationBeforeCommitAndRestoresSourceOnFailure(bool failTarget)
        {
            var coordinator = CreateCoordinator(out FakeMountAdapter adapter);
            Navigate(coordinator, CharacterTestRuntimeAiLabWorkspace.Setup);
            var source = coordinator.CurrentState;
            var sink = new CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandSink(null);
            var projected = new List<CharacterTestRuntimeAiLabWorkspaceMode>();
            adapter.BeforeWorkspaceMount = state =>
            {
                Assert.That(coordinator.CurrentState, Is.SameAs(source), "A mount must not commit navigation early.");
                sink.SynchronizeShellNavigationState(state);
                projected.Add(sink.WorkspaceMode);
            };
            if (failTarget)
                adapter.FailWorkspace = CharacterTestRuntimeAiLabWorkspace.BehaviorGroup;

            var result = coordinator.Navigate(source.Generation, source.Revision,
                source.ActiveWorkspace, CharacterTestRuntimeAiLabWorkspace.BehaviorGroup);

            Assert.That(projected[0], Is.EqualTo(CharacterTestRuntimeAiLabWorkspaceMode.BehaviorGroup));
            Assert.That(result.Success, Is.EqualTo(!failTarget));
            if (failTarget)
            {
                Assert.That(projected, Is.EqualTo(new[] {
                    CharacterTestRuntimeAiLabWorkspaceMode.BehaviorGroup,
                    CharacterTestRuntimeAiLabWorkspaceMode.Setup }));
                Assert.That(coordinator.CurrentState, Is.SameAs(source));
                Assert.That(sink.WorkspaceMode, Is.EqualTo(CharacterTestRuntimeAiLabWorkspaceMode.Setup));
            }
            else
            {
                Assert.That(projected.Count, Is.EqualTo(1));
                Assert.That(coordinator.CurrentState.ActiveWorkspace,
                    Is.EqualTo(CharacterTestRuntimeAiLabWorkspace.BehaviorGroup));
            }
        }

        [Test]
        public void SetupCardEditingKeepsItsLogicModeWhenTestAliasesToRuntimeWorkbench()
        {
            var coordinator = CreateCoordinator(out FakeMountAdapter adapter); adapter.SetupPhase = true;
            Navigate(coordinator, CharacterTestRuntimeAiLabWorkspace.Setup);
            var state = coordinator.CurrentState;
            var opened = coordinator.NavigateToBehaviorCard(state.Generation, state.Revision, state.ActiveWorkspace,
                "group", "entry", "card", CharacterTestRuntimeAiLabSelectedElementKind.None, string.Empty, CharacterTestRuntimeAiLabBehaviorCardViewport.Default);
            Assert.IsTrue(opened.Success, opened.Message);
            var before = coordinator.CurrentState.PageContext;
            Assert.AreEqual(CharacterTestRuntimeAiLabTimelineMode.BehaviorLogic, before.TimelineMode);
            state = coordinator.CurrentState;
            var started = coordinator.BeginSessionStart(state.Generation, state.Revision, state.ActiveWorkspace,
                CharacterTestRuntimeAiLabWorkspace.BehaviorCard, new object(), () => { adapter.SetupPhase = false; return true; }, _ => true, before.TimelineMode);
            Assert.IsTrue(started.Success, started.Message);
            var after = coordinator.CurrentState.PageContext;
            Assert.AreEqual(before.TimelineMode, after.TimelineMode);
            Assert.AreEqual(before.CardDraftId, after.CardDraftId);
            Assert.AreEqual(before.EntryId, after.EntryId);
        }

        [Test]
        public void InitialState_PrefersPlayerDeck_AndMountsExactlyOneWorkspace()
        {
            var coordinator = CreateCoordinator(out FakeMountAdapter adapter);

            Assert.That(coordinator.CurrentState.ActiveWorkspace,
                Is.EqualTo(CharacterTestRuntimeAiLabWorkspace.PlayerDeck));
            Assert.That(coordinator.CurrentState.Visibility,
                Is.EqualTo(CharacterTestRuntimeAiLabShellVisibility.Visible));
            Assert.That(adapter.VisibleMountCount, Is.EqualTo(1));
            Assert.That(adapter.MountedWorkspace,
                Is.EqualTo(CharacterTestRuntimeAiLabWorkspace.PlayerDeck));
        }

        [Test]
        public void InitialAttach_PlayerDeckUnavailableFallsBackToSetup()
        {
            var coordinator = new CharacterTestRuntimeAiLabShellCoordinator(
                generation: 18,
                playerDeckAvailable: true,
                labAvailable: true);
            var adapter = new FakeMountAdapter();
            adapter.UnavailableWorkspaces.Add(CharacterTestRuntimeAiLabWorkspace.PlayerDeck);

            CharacterTestRuntimeAiLabNavigationResult result =
                coordinator.AttachMountAdapter(18, adapter);

            Assert.That(result.Success, Is.True, result.Message);
            Assert.That(result.Code, Is.EqualTo("PLAYER_DECK_WORKSPACE_UNAVAILABLE"));
            Assert.That(coordinator.CurrentState.ActiveWorkspace,
                Is.EqualTo(CharacterTestRuntimeAiLabWorkspace.Setup));
            Assert.That(adapter.MountedWorkspace,
                Is.EqualTo(CharacterTestRuntimeAiLabWorkspace.Setup));
        }

        [TestCase((int)CharacterTestRuntimeAiLabWorkspace.PlayerDeck)]
        [TestCase((int)CharacterTestRuntimeAiLabWorkspace.Setup)]
        [TestCase((int)CharacterTestRuntimeAiLabWorkspace.BehaviorGroup)]
        [TestCase((int)CharacterTestRuntimeAiLabWorkspace.BehaviorCard)]
        [TestCase((int)CharacterTestRuntimeAiLabWorkspace.Facts)]
        [TestCase((int)CharacterTestRuntimeAiLabWorkspace.RuntimeFrame)]
        [TestCase((int)CharacterTestRuntimeAiLabWorkspace.Replay)]
        public void ToggleVisibility_ReopensEveryWorkspaceThroughCoordinator(int workspaceValue)
        {
            var workspace = (CharacterTestRuntimeAiLabWorkspace)workspaceValue;
            var coordinator = CreateCoordinator(out FakeMountAdapter adapter);
            if (workspace != CharacterTestRuntimeAiLabWorkspace.PlayerDeck)
                Navigate(coordinator, workspace);
            long revisionBeforeHide = coordinator.CurrentState.Revision;

            CharacterTestRuntimeAiLabNavigationResult hidden = coordinator.ToggleVisibility();
            CharacterTestRuntimeAiLabNavigationResult visible = coordinator.ToggleVisibility();

            Assert.That(hidden.Success, Is.True, hidden.Message);
            Assert.That(visible.Success, Is.True, visible.Message);
            Assert.That(coordinator.CurrentState.Visibility,
                Is.EqualTo(CharacterTestRuntimeAiLabShellVisibility.Visible));
            Assert.That(coordinator.CurrentState.ActiveWorkspace, Is.EqualTo(workspace));
            Assert.That(coordinator.CurrentState.Revision, Is.EqualTo(revisionBeforeHide + 2));
            Assert.That(adapter.MountedWorkspace, Is.EqualTo(workspace));
            Assert.That(adapter.VisibleMountCount, Is.EqualTo(1));
        }

        [Test]
        public void CloseToPlayerDeck_HidesAndF9ReopensAtPlayerDeck()
        {
            var coordinator = CreateCoordinator(out FakeMountAdapter adapter);
            Navigate(coordinator, CharacterTestRuntimeAiLabWorkspace.Setup);
            Navigate(coordinator, CharacterTestRuntimeAiLabWorkspace.BehaviorGroup);

            CharacterTestRuntimeAiLabNavigationResult closed =
                coordinator.CloseToPlayerDeck();

            Assert.That(closed.Success, Is.True, closed.Message);
            Assert.That(closed.Code, Is.EqualTo("SHELL_CLOSED_TO_PLAYER_DECK"));
            Assert.That(coordinator.CurrentState.Visibility,
                Is.EqualTo(CharacterTestRuntimeAiLabShellVisibility.Hidden));
            Assert.That(coordinator.CurrentState.ActiveWorkspace,
                Is.EqualTo(CharacterTestRuntimeAiLabWorkspace.PlayerDeck));
            Assert.That(adapter.VisibleMountCount, Is.Zero);

            CharacterTestRuntimeAiLabNavigationResult reopened =
                coordinator.ToggleVisibility();

            Assert.That(reopened.Success, Is.True, reopened.Message);
            Assert.That(coordinator.CurrentState.Visibility,
                Is.EqualTo(CharacterTestRuntimeAiLabShellVisibility.Visible));
            Assert.That(coordinator.CurrentState.ActiveWorkspace,
                Is.EqualTo(CharacterTestRuntimeAiLabWorkspace.PlayerDeck));
            Assert.That(adapter.MountedWorkspace,
                Is.EqualTo(CharacterTestRuntimeAiLabWorkspace.PlayerDeck));
            Assert.That(adapter.VisibleMountCount, Is.EqualTo(1));
        }

        [Test]
        public void Navigate_RuntimeFrameBecomesReturnWorkspaceForEvidencePeers()
        {
            var coordinator = CreateCoordinator(out _);
            Navigate(coordinator, CharacterTestRuntimeAiLabWorkspace.Setup);
            Navigate(coordinator, CharacterTestRuntimeAiLabWorkspace.BehaviorGroup);
            Navigate(coordinator, CharacterTestRuntimeAiLabWorkspace.Facts);
            Navigate(coordinator, CharacterTestRuntimeAiLabWorkspace.RuntimeFrame);
            Navigate(coordinator, CharacterTestRuntimeAiLabWorkspace.Replay);

            CharacterTestRuntimeAiLabNavigationResult back = coordinator.Back(
                coordinator.CurrentState.Generation,
                coordinator.CurrentState.Revision,
                CharacterTestRuntimeAiLabWorkspace.Replay);

            Assert.That(back.Success, Is.True, back.Message);
            Assert.That(coordinator.CurrentState.ActiveWorkspace,
                Is.EqualTo(CharacterTestRuntimeAiLabWorkspace.RuntimeFrame));
        }

        [Test]
        public void ToggleVisibility_DismissesTopModalBeforeHidingShell()
        {
            var coordinator = CreateCoordinator(out FakeMountAdapter adapter);
            adapter.HasTopModal = true;
            long revision = coordinator.CurrentState.Revision;

            CharacterTestRuntimeAiLabNavigationResult dismissed = coordinator.ToggleVisibility();

            Assert.That(dismissed.Success, Is.True, dismissed.Message);
            Assert.That(dismissed.Code, Is.EqualTo("ACTIVE_MODAL_DISMISSED"));
            Assert.That(coordinator.CurrentState.Visibility,
                Is.EqualTo(CharacterTestRuntimeAiLabShellVisibility.Visible));
            Assert.That(coordinator.CurrentState.Revision, Is.EqualTo(revision));
            Assert.That(adapter.VisibleMountCount, Is.EqualTo(1));

            CharacterTestRuntimeAiLabNavigationResult hidden = coordinator.ToggleVisibility();
            Assert.That(hidden.Code, Is.EqualTo("SHELL_HIDDEN"));
            Assert.That(coordinator.CurrentState.Visibility,
                Is.EqualTo(CharacterTestRuntimeAiLabShellVisibility.Hidden));
        }

        [Test]
        public void ToggleVisibility_ModalDismissFailureKeepsShellVisible()
        {
            var coordinator = CreateCoordinator(out FakeMountAdapter adapter);
            adapter.FailModalDismiss = true;

            CharacterTestRuntimeAiLabNavigationResult result = coordinator.ToggleVisibility();

            Assert.That(result.Success, Is.False);
            Assert.That(result.Code, Is.EqualTo("ACTIVE_MODAL_DISMISS_FAILED"));
            Assert.That(coordinator.CurrentState.Visibility,
                Is.EqualTo(CharacterTestRuntimeAiLabShellVisibility.Visible));
            Assert.That(adapter.VisibleMountCount, Is.EqualTo(1));
        }

        [Test]
        public void ToggleVisibility_PreservesStablePageContextAndViewport()
        {
            var coordinator = CreateCoordinator(out FakeMountAdapter adapter);
            Navigate(coordinator, CharacterTestRuntimeAiLabWorkspace.Setup);
            Navigate(coordinator, CharacterTestRuntimeAiLabWorkspace.BehaviorGroup);
            Navigate(coordinator, CharacterTestRuntimeAiLabWorkspace.BehaviorCard);
            long revision = coordinator.CurrentState.Revision;
            CharacterTestRuntimeAiLabNavigationResult updated =
                coordinator.TryUpdateBehaviorCardContext(
                    coordinator.CurrentState.Generation,
                    revision,
                    " group-a ",
                    " entry-a ",
                    " card-b ",
                    CharacterTestRuntimeAiLabSelectedElementKind.Detector,
                    " detector-c ",
                    new CharacterTestRuntimeAiLabBehaviorCardViewport(30, 330, 1.5f, 84f, true));
            Assert.That(updated.Success, Is.True, updated.Message);

            coordinator.ToggleVisibility();
            Assert.That(adapter.VisibleMountCount, Is.Zero);
            coordinator.ToggleVisibility();

            CharacterTestRuntimeAiLabPageContext page = coordinator.CurrentState.PageContext;
            Assert.That(page.GroupDraftId, Is.EqualTo("group-a"));
            Assert.That(page.EntryId, Is.EqualTo("entry-a"));
            Assert.That(page.CardDraftId, Is.EqualTo("card-b"));
            Assert.That(page.SelectedElementStableId, Is.EqualTo("detector-c"));
            Assert.That(page.BehaviorCardViewport.HorizontalStartFrame, Is.EqualTo(30));
            Assert.That(page.BehaviorCardViewport.VerticalScrollOffset, Is.EqualTo(84f));
            Assert.That(adapter.VisibleMountCount, Is.EqualTo(1));
        }

        [Test]
        public void HiddenRecovery_FailedF9PreflightCommitsExactlyOneHiddenRevision()
        {
            var coordinator = CreateCoordinator(out FakeMountAdapter adapter);
            coordinator.EnterUnavailableRecovery(
                "recovery package missing",
                CharacterTestRuntimeAiLabWorkspace.PlayerDeck);
            long revision = coordinator.CurrentState.Revision;
            adapter.FailPrepare = true;

            CharacterTestRuntimeAiLabNavigationResult result = coordinator.ToggleVisibility();

            Assert.That(result.Success, Is.False);
            Assert.That(result.Code, Is.EqualTo("RECOVERY_SURFACE_UNAVAILABLE"));
            Assert.That(coordinator.CurrentState.Visibility,
                Is.EqualTo(CharacterTestRuntimeAiLabShellVisibility.Hidden));
            Assert.That(coordinator.CurrentState.PresentationMode,
                Is.EqualTo(CharacterTestRuntimeAiLabPresentationMode.Recovery));
            Assert.That(coordinator.CurrentState.Revision, Is.EqualTo(revision + 1));
            Assert.That(adapter.PrepareCount, Is.EqualTo(1));
            Assert.That(adapter.RecoveryMountCount, Is.Zero);
            Assert.That(adapter.VisibleMountCount, Is.Zero);
        }

        [Test]
        public void HiddenSessionStart_FailedF9MountCommitsExactlyOneHiddenRevision()
        {
            var coordinator = CreateCoordinator(out FakeMountAdapter adapter);
            Navigate(coordinator, CharacterTestRuntimeAiLabWorkspace.Setup);
            CharacterTestRuntimeAiLabNavigationResult reopenResult = default;
            long revisionBeforeReopen = 0;
            long revisionAfterReopen = 0;
            CharacterTestRuntimeAiLabShellVisibility visibilityAfterReopen = default;
            CharacterTestRuntimeAiLabPresentationMode modeAfterReopen = default;

            coordinator.BeginSessionStart(
                coordinator.CurrentState.Generation,
                coordinator.CurrentState.Revision,
                CharacterTestRuntimeAiLabWorkspace.Setup,
                CharacterTestRuntimeAiLabWorkspace.Facts,
                new object(),
                () =>
                {
                    CharacterTestRuntimeAiLabNavigationResult hidden =
                        coordinator.ToggleVisibility();
                    Assert.That(hidden.Success, Is.True, hidden.Message);
                    revisionBeforeReopen = coordinator.CurrentState.Revision;
                    adapter.FailTransition = true;
                    reopenResult = coordinator.ToggleVisibility();
                    revisionAfterReopen = coordinator.CurrentState.Revision;
                    visibilityAfterReopen = coordinator.CurrentState.Visibility;
                    modeAfterReopen = coordinator.CurrentState.PresentationMode;
                    return false;
                },
                _ => true);

            Assert.That(reopenResult.Success, Is.False);
            Assert.That(reopenResult.Code, Is.EqualTo("SESSION_START_TRANSITION_REOPEN_FAILED"));
            Assert.That(revisionAfterReopen, Is.EqualTo(revisionBeforeReopen + 1));
            Assert.That(visibilityAfterReopen,
                Is.EqualTo(CharacterTestRuntimeAiLabShellVisibility.Hidden));
            Assert.That(modeAfterReopen,
                Is.EqualTo(CharacterTestRuntimeAiLabPresentationMode.SessionStartInProgress));
        }

        [Test]
        public void StaleContextCommand_DoesNotMutateStateOrRevision()
        {
            var coordinator = CreateCoordinator(out _);
            long revision = coordinator.CurrentState.Revision;
            Navigate(coordinator, CharacterTestRuntimeAiLabWorkspace.Setup);

            CharacterTestRuntimeAiLabNavigationResult result =
                coordinator.TryUpdatePlayerDeckContext(
                    coordinator.CurrentState.Generation,
                    revision,
                    4,
                    "card.source");

            Assert.That(result.Success, Is.False);
            Assert.That(result.Code, Is.EqualTo("NAVIGATION_STALE_COMMAND"));
            Assert.That(coordinator.CurrentState.ActiveWorkspace,
                Is.EqualTo(CharacterTestRuntimeAiLabWorkspace.Setup));
            Assert.That(coordinator.CurrentState.Revision, Is.EqualTo(revision + 1));
        }

        [Test]
        public void TargetMountFailure_RestoresSourceWithoutPublishingFalseTarget()
        {
            var coordinator = CreateCoordinator(out FakeMountAdapter adapter);
            adapter.FailWorkspace = CharacterTestRuntimeAiLabWorkspace.Setup;
            long revision = coordinator.CurrentState.Revision;

            CharacterTestRuntimeAiLabNavigationResult result = coordinator.Navigate(
                coordinator.CurrentState.Generation,
                revision,
                CharacterTestRuntimeAiLabWorkspace.PlayerDeck,
                CharacterTestRuntimeAiLabWorkspace.Setup);

            Assert.That(result.Success, Is.False);
            Assert.That(result.Code, Is.EqualTo("NAVIGATION_TARGET_MOUNT_FAILED"));
            Assert.That(coordinator.CurrentState.ActiveWorkspace,
                Is.EqualTo(CharacterTestRuntimeAiLabWorkspace.PlayerDeck));
            Assert.That(adapter.MountedWorkspace,
                Is.EqualTo(CharacterTestRuntimeAiLabWorkspace.PlayerDeck));
            Assert.That(adapter.VisibleMountCount, Is.EqualTo(1));
        }

        [Test]
        public void TargetAndSourceMountFailure_HidesWithoutChangingPublishedContext()
        {
            var coordinator = CreateCoordinator(out FakeMountAdapter adapter);
            CharacterTestRuntimeAiLabPageContext original = coordinator.CurrentState.PageContext;
            long revision = coordinator.CurrentState.Revision;
            adapter.FailAllWorkspaces = true;

            CharacterTestRuntimeAiLabNavigationResult result = coordinator.Navigate(
                coordinator.CurrentState.Generation,
                revision,
                CharacterTestRuntimeAiLabWorkspace.PlayerDeck,
                CharacterTestRuntimeAiLabWorkspace.Setup);

            Assert.That(result.Success, Is.False);
            Assert.That(result.Code, Is.EqualTo("NAVIGATION_SOURCE_RESTORE_FAILED"));
            Assert.That(coordinator.CurrentState.Visibility,
                Is.EqualTo(CharacterTestRuntimeAiLabShellVisibility.Hidden));
            Assert.That(coordinator.CurrentState.PageContext, Is.SameAs(original));
            Assert.That(coordinator.CurrentState.Revision, Is.EqualTo(revision + 1));
        }

        [Test]
        public void SessionStart_TransitionMustMountBeforeDomainCall()
        {
            var coordinator = CreateCoordinator(out FakeMountAdapter adapter);
            Navigate(coordinator, CharacterTestRuntimeAiLabWorkspace.Setup);
            adapter.FailTransition = true;
            int domainCalls = 0;

            CharacterTestRuntimeAiLabNavigationResult result = coordinator.BeginSessionStart(
                coordinator.CurrentState.Generation,
                coordinator.CurrentState.Revision,
                CharacterTestRuntimeAiLabWorkspace.Setup,
                CharacterTestRuntimeAiLabWorkspace.Facts,
                new object(),
                () =>
                {
                    domainCalls++;
                    return true;
                },
                _ => true);

            Assert.That(result.Success, Is.False);
            Assert.That(result.Code, Is.EqualTo("SESSION_START_TRANSITION_MOUNT_FAILED"));
            Assert.That(domainCalls, Is.Zero);
            Assert.That(coordinator.CurrentState.ActiveWorkspace,
                Is.EqualTo(CharacterTestRuntimeAiLabWorkspace.Setup));
            Assert.That(adapter.VisibleMountCount, Is.EqualTo(1));
        }

        [Test]
        public void SessionStart_RebuildAdapterKeepsTransactionAndCompletesOnce()
        {
            var coordinator = CreateCoordinator(out FakeMountAdapter oldAdapter);
            Navigate(coordinator, CharacterTestRuntimeAiLabWorkspace.Setup);
            FakeMountAdapter rebuiltAdapter = null;
            long transactionId = 0;

            CharacterTestRuntimeAiLabNavigationResult result = coordinator.BeginSessionStart(
                coordinator.CurrentState.Generation,
                coordinator.CurrentState.Revision,
                CharacterTestRuntimeAiLabWorkspace.Setup,
                CharacterTestRuntimeAiLabWorkspace.Facts,
                new object(),
                () =>
                {
                    transactionId = coordinator.CurrentState.Transaction.TransactionId;
                    coordinator.DetachMountAdapter(oldAdapter);
                    rebuiltAdapter = new FakeMountAdapter();
                    CharacterTestRuntimeAiLabNavigationResult attached = coordinator.AttachMountAdapter(
                        coordinator.CurrentState.Generation,
                        rebuiltAdapter);
                    Assert.That(attached.Success, Is.True, attached.Message);
                    Assert.That(rebuiltAdapter.TransitionMountCount, Is.EqualTo(1));
                    return true;
                },
                _ => true);

            Assert.That(result.Success, Is.True, result.Message);
            Assert.That(coordinator.CurrentState.ActiveWorkspace,
                Is.EqualTo(CharacterTestRuntimeAiLabWorkspace.Facts));
            Assert.That(coordinator.CurrentState.LastCompletedTransactionId, Is.EqualTo(transactionId));
            Assert.That(rebuiltAdapter.VisibleMountCount, Is.EqualTo(1));
            CharacterTestRuntimeAiLabNavigationResult duplicate = coordinator.CompleteSessionStart(
                transactionId,
                true,
                _ => true);
            Assert.That(duplicate.Success, Is.False);
            Assert.That(duplicate.Code, Is.EqualTo("TRANSACTION_ALREADY_COMPLETED"));
        }

        [Test]
        public void SessionStart_TransitionReattachFailureStaysHiddenUntilExactCompletion()
        {
            var coordinator = CreateCoordinator(out FakeMountAdapter oldAdapter);
            Navigate(coordinator, CharacterTestRuntimeAiLabWorkspace.Setup);
            FakeMountAdapter rebuiltAdapter = null;
            CharacterTestRuntimeAiLabNavigationResult attachResult = default;

            CharacterTestRuntimeAiLabNavigationResult result = coordinator.BeginSessionStart(
                coordinator.CurrentState.Generation,
                coordinator.CurrentState.Revision,
                CharacterTestRuntimeAiLabWorkspace.Setup,
                CharacterTestRuntimeAiLabWorkspace.Facts,
                new object(),
                () =>
                {
                    coordinator.DetachMountAdapter(oldAdapter);
                    rebuiltAdapter = new FakeMountAdapter { FailTransition = true };
                    attachResult = coordinator.AttachMountAdapter(
                        coordinator.CurrentState.Generation,
                        rebuiltAdapter);
                    return true;
                },
                _ => true);

            Assert.That(attachResult.Success, Is.False);
            Assert.That(attachResult.Code, Is.EqualTo("SESSION_START_TRANSITION_REATTACH_FAILED"));
            Assert.That(result.Success, Is.True, result.Message);
            Assert.That(result.Code, Is.EqualTo("SESSION_START_COMPLETED_HIDDEN"));
            Assert.That(coordinator.CurrentState.Visibility,
                Is.EqualTo(CharacterTestRuntimeAiLabShellVisibility.Hidden));
            Assert.That(coordinator.CurrentState.PresentationMode,
                Is.EqualTo(CharacterTestRuntimeAiLabPresentationMode.Workspace));
            Assert.That(coordinator.CurrentState.ActiveWorkspace,
                Is.EqualTo(CharacterTestRuntimeAiLabWorkspace.Facts));

            rebuiltAdapter.FailTransition = false;
            CharacterTestRuntimeAiLabNavigationResult reopened = coordinator.ToggleVisibility();
            Assert.That(reopened.Success, Is.True, reopened.Message);
            Assert.That(rebuiltAdapter.MountedWorkspace,
                Is.EqualTo(CharacterTestRuntimeAiLabWorkspace.Facts));
        }

        [Test]
        public void Attach_RunningInvalidWorkspaceFallsBackToFactsWhenHistoryExists()
        {
            var coordinator = new CharacterTestRuntimeAiLabShellCoordinator(
                generation: 19,
                playerDeckAvailable: false,
                labAvailable: true);
            var adapter = new FakeMountAdapter
            {
                SetupPhase = false,
                RuntimeHistory = true
            };
            adapter.InvalidContextWorkspaces.Add(CharacterTestRuntimeAiLabWorkspace.Setup);

            CharacterTestRuntimeAiLabNavigationResult result =
                coordinator.AttachMountAdapter(19, adapter);

            Assert.That(result.Success, Is.True, result.Message);
            Assert.That(result.Code, Is.EqualTo("RUNNING_PHASE_FACTS_FALLBACK"));
            Assert.That(coordinator.CurrentState.ActiveWorkspace,
                Is.EqualTo(CharacterTestRuntimeAiLabWorkspace.Facts));
        }

        [Test]
        public void Attach_RunningInvalidWorkspaceWithoutHistoryStaysHidden()
        {
            var coordinator = new CharacterTestRuntimeAiLabShellCoordinator(
                generation: 20,
                playerDeckAvailable: false,
                labAvailable: true);
            var adapter = new FakeMountAdapter { SetupPhase = false };
            adapter.InvalidContextWorkspaces.Add(CharacterTestRuntimeAiLabWorkspace.Setup);

            CharacterTestRuntimeAiLabNavigationResult result =
                coordinator.AttachMountAdapter(20, adapter);

            Assert.That(result.Success, Is.False);
            Assert.That(result.Code, Is.EqualTo("SESSION_CONTEXT_UNAVAILABLE"));
            Assert.That(coordinator.CurrentState.Visibility,
                Is.EqualTo(CharacterTestRuntimeAiLabShellVisibility.Hidden));
            Assert.That(adapter.VisibleMountCount, Is.Zero);
        }

        [Test]
        public void Attach_NormalizesMissingBehaviorCardSelectionWithoutLeavingCard()
        {
            var coordinator = CreateCoordinator(out FakeMountAdapter oldAdapter);
            Navigate(coordinator, CharacterTestRuntimeAiLabWorkspace.BehaviorGroup);
            CharacterTestRuntimeAiLabNavigationResult opened = coordinator.NavigateToBehaviorCard(
                coordinator.CurrentState.Generation,
                coordinator.CurrentState.Revision,
                CharacterTestRuntimeAiLabWorkspace.BehaviorGroup,
                "group-a",
                "entry-a",
                "card-a",
                CharacterTestRuntimeAiLabSelectedElementKind.Detector,
                "detector-missing",
                CharacterTestRuntimeAiLabBehaviorCardViewport.Default);
            Assert.That(opened.Success, Is.True, opened.Message);
            coordinator.DetachMountAdapter(oldAdapter);
            var rebuiltAdapter = new FakeMountAdapter { ClearBehaviorCardSelection = true };

            CharacterTestRuntimeAiLabNavigationResult attached = coordinator.AttachMountAdapter(
                coordinator.CurrentState.Generation,
                rebuiltAdapter);

            Assert.That(attached.Success, Is.True, attached.Message);
            Assert.That(coordinator.CurrentState.ActiveWorkspace,
                Is.EqualTo(CharacterTestRuntimeAiLabWorkspace.BehaviorCard));
            Assert.That(coordinator.CurrentState.PageContext.SelectedElementKind,
                Is.EqualTo(CharacterTestRuntimeAiLabSelectedElementKind.None));
            Assert.That(coordinator.CurrentState.PageContext.SelectedElementStableId, Is.Empty);
        }

        [Test]
        public void Attach_CommitsCanonicalEntryWhenDuplicateSharesCardDraftId()
        {
            var coordinator = CreateCoordinator(out FakeMountAdapter oldAdapter);
            Navigate(coordinator, CharacterTestRuntimeAiLabWorkspace.BehaviorGroup);
            CharacterTestRuntimeAiLabNavigationResult opened = coordinator.NavigateToBehaviorCard(
                coordinator.CurrentState.Generation,
                coordinator.CurrentState.Revision,
                CharacterTestRuntimeAiLabWorkspace.BehaviorGroup,
                "group-a",
                "entry-removed",
                "card-shared",
                CharacterTestRuntimeAiLabSelectedElementKind.None,
                string.Empty,
                CharacterTestRuntimeAiLabBehaviorCardViewport.Default);
            Assert.That(opened.Success, Is.True, opened.Message);
            coordinator.DetachMountAdapter(oldAdapter);
            var rebuiltAdapter = new FakeMountAdapter
            {
                NormalizedBehaviorCardEntryId = "entry-canonical"
            };

            CharacterTestRuntimeAiLabNavigationResult attached = coordinator.AttachMountAdapter(
                coordinator.CurrentState.Generation,
                rebuiltAdapter);

            Assert.That(attached.Success, Is.True, attached.Message);
            Assert.That(coordinator.CurrentState.ActiveWorkspace,
                Is.EqualTo(CharacterTestRuntimeAiLabWorkspace.BehaviorCard));
            Assert.That(coordinator.CurrentState.PageContext.GroupDraftId, Is.EqualTo("group-a"));
            Assert.That(coordinator.CurrentState.PageContext.EntryId, Is.EqualTo("entry-canonical"));
            Assert.That(coordinator.CurrentState.PageContext.CardDraftId, Is.EqualTo("card-shared"));
            Assert.That(rebuiltAdapter.MountedEntryId, Is.EqualTo("entry-canonical"));
        }

        [Test]
        public void Attach_RecoverySurfaceFailureKeepsFaultAndHidesShell()
        {
            var coordinator = CreateCoordinator(out FakeMountAdapter oldAdapter);
            coordinator.EnterRecovery(
                "TEST_RECOVERY",
                "render failed",
                CharacterTestRuntimeAiLabWorkspace.BehaviorCard,
                canRetry: true,
                canResetSession: true);
            coordinator.DetachMountAdapter(oldAdapter);
            var rebuiltAdapter = new FakeMountAdapter { FailRecovery = true };

            CharacterTestRuntimeAiLabNavigationResult result = coordinator.AttachMountAdapter(
                coordinator.CurrentState.Generation,
                rebuiltAdapter);

            Assert.That(result.Success, Is.False);
            Assert.That(result.Code, Is.EqualTo("RECOVERY_SURFACE_UNAVAILABLE"));
            Assert.That(coordinator.CurrentState.Visibility,
                Is.EqualTo(CharacterTestRuntimeAiLabShellVisibility.Hidden));
            Assert.That(coordinator.CurrentState.PresentationMode,
                Is.EqualTo(CharacterTestRuntimeAiLabPresentationMode.Recovery));
            Assert.That(coordinator.CurrentState.Fault, Is.Not.Null);
            Assert.That(coordinator.CurrentState.Fault.FailedTarget,
                Is.EqualTo(CharacterTestRuntimeAiLabWorkspace.BehaviorCard));
        }

        [Test]
        public void SessionStart_FailureUsesExactRecoveryBeforeRestoringSource()
        {
            var coordinator = CreateCoordinator(out FakeMountAdapter adapter);
            Navigate(coordinator, CharacterTestRuntimeAiLabWorkspace.Setup);
            object snapshot = new object();
            int recoveryCalls = 0;

            CharacterTestRuntimeAiLabNavigationResult result = coordinator.BeginSessionStart(
                coordinator.CurrentState.Generation,
                coordinator.CurrentState.Revision,
                CharacterTestRuntimeAiLabWorkspace.Setup,
                CharacterTestRuntimeAiLabWorkspace.Facts,
                snapshot,
                () => false,
                candidate =>
                {
                    recoveryCalls++;
                    return ReferenceEquals(snapshot, candidate);
                });

            Assert.That(result.Success, Is.True, result.Message);
            Assert.That(recoveryCalls, Is.EqualTo(1));
            Assert.That(coordinator.CurrentState.ActiveWorkspace,
                Is.EqualTo(CharacterTestRuntimeAiLabWorkspace.Setup));
            Assert.That(coordinator.CurrentState.LastCompletedOutcome,
                Is.EqualTo(CharacterTestRuntimeAiLabSessionStartOutcome.Failed));
            Assert.That(adapter.VisibleMountCount, Is.EqualTo(1));
        }

        [Test]
        public void SessionStart_UnrecoverableFailureEntersHiddenRecoveryAndF9RetriesSurface()
        {
            var coordinator = CreateCoordinator(out FakeMountAdapter adapter);
            Navigate(coordinator, CharacterTestRuntimeAiLabWorkspace.Setup);

            CharacterTestRuntimeAiLabNavigationResult result = coordinator.BeginSessionStart(
                coordinator.CurrentState.Generation,
                coordinator.CurrentState.Revision,
                CharacterTestRuntimeAiLabWorkspace.Setup,
                CharacterTestRuntimeAiLabWorkspace.Facts,
                new object(),
                () => false,
                _ => false);

            Assert.That(result.Success, Is.False);
            Assert.That(result.Code, Is.EqualTo("SESSION_START_RECOVERY_REQUIRED"));
            Assert.That(coordinator.CurrentState.Visibility,
                Is.EqualTo(CharacterTestRuntimeAiLabShellVisibility.Hidden));
            Assert.That(coordinator.CurrentState.PresentationMode,
                Is.EqualTo(CharacterTestRuntimeAiLabPresentationMode.Recovery));

            coordinator.ToggleVisibility();
            Assert.That(coordinator.CurrentState.Visibility,
                Is.EqualTo(CharacterTestRuntimeAiLabShellVisibility.Visible));
            Assert.That(adapter.RecoveryMountCount, Is.EqualTo(1));
            Assert.That(adapter.VisibleMountCount, Is.EqualTo(1));
        }

        [Test]
        public void Recovery_CanRestoreLastKnownSafeWorkspace()
        {
            var coordinator = CreateCoordinator(out FakeMountAdapter adapter);
            Navigate(coordinator, CharacterTestRuntimeAiLabWorkspace.Setup);
            coordinator.EnterRecovery(
                "TEST_RECOVERY",
                "mount failed",
                CharacterTestRuntimeAiLabWorkspace.Facts,
                canRetry: true,
                canResetSession: false);

            CharacterTestRuntimeAiLabNavigationResult result = coordinator.RestoreLastWorkspace(
                coordinator.CurrentState.Generation,
                coordinator.CurrentState.Revision);

            Assert.That(result.Success, Is.True, result.Message);
            Assert.That(coordinator.CurrentState.PresentationMode,
                Is.EqualTo(CharacterTestRuntimeAiLabPresentationMode.Workspace));
            Assert.That(coordinator.CurrentState.ActiveWorkspace,
                Is.EqualTo(CharacterTestRuntimeAiLabWorkspace.Setup));
            Assert.That(adapter.MountedWorkspace,
                Is.EqualTo(CharacterTestRuntimeAiLabWorkspace.Setup));
        }

        [Test]
        public void Recovery_ResetMustSucceedBeforeSetupIsPublished()
        {
            var coordinator = CreateCoordinator(out _);
            coordinator.EnterRecovery(
                "TEST_RECOVERY",
                "domain failed",
                CharacterTestRuntimeAiLabWorkspace.Facts,
                canRetry: true,
                canResetSession: true);

            CharacterTestRuntimeAiLabNavigationResult rejected = coordinator.ResetRecoveryToSetup(
                coordinator.CurrentState.Generation,
                coordinator.CurrentState.Revision,
                () => false);
            Assert.That(rejected.Success, Is.False);
            Assert.That(coordinator.CurrentState.PresentationMode,
                Is.EqualTo(CharacterTestRuntimeAiLabPresentationMode.Recovery));

            CharacterTestRuntimeAiLabNavigationResult accepted = coordinator.ResetRecoveryToSetup(
                coordinator.CurrentState.Generation,
                coordinator.CurrentState.Revision,
                () => true);
            Assert.That(accepted.Success, Is.True, accepted.Message);
            Assert.That(coordinator.CurrentState.ActiveWorkspace,
                Is.EqualTo(CharacterTestRuntimeAiLabWorkspace.Setup));
        }

        [Test]
        public void PreviewContractFault_IsDeferredAndDrainedExactlyOnce()
        {
            CharacterTestRuntimeAiLabShellCoordinator coordinator =
                CreateCoordinator(out FakeMountAdapter adapter);
            Navigate(coordinator, CharacterTestRuntimeAiLabWorkspace.BehaviorCard);
            int recoveryMountsBeforeFault = adapter.RecoveryMountCount;
            var deferred = new CharacterTestRuntimeAiLabDeferredPreviewFault();

            deferred.Enqueue("PREVIEW_ROOT_UNAVAILABLE", "preview root was replaced");
            deferred.Enqueue("PREVIEW_ROOT_UNAVAILABLE", "duplicate callback");

            Assert.That(
                adapter.RecoveryMountCount,
                Is.EqualTo(recoveryMountsBeforeFault),
                "Enqueue must not synchronously unmount or mount from the Preview Host call stack.");
            Assert.That(deferred.TryTake(out string code, out string detail), Is.True);
            CharacterTestRuntimeAiLabNavigationResult recovery = coordinator.EnterRecovery(
                code,
                detail,
                CharacterTestRuntimeAiLabWorkspace.BehaviorCard,
                canRetry: true,
                canResetSession: false);

            Assert.That(recovery.Success, Is.True, recovery.Message);
            Assert.That(adapter.RecoveryMountCount, Is.EqualTo(recoveryMountsBeforeFault + 1));
            Assert.That(deferred.TryTake(out _, out _), Is.False);
            Assert.That(adapter.RecoveryMountCount, Is.EqualTo(recoveryMountsBeforeFault + 1));
        }

        [Test]
        public void UnifiedTimelineMode_PreservesIndependentContextWithoutRemount_AndF9RestoresMode()
        {
            CharacterTestRuntimeAiLabShellCoordinator coordinator =
                CreateCoordinator(out FakeMountAdapter adapter);
            Navigate(coordinator, CharacterTestRuntimeAiLabWorkspace.Setup);
            adapter.SetupPhase = false;
            adapter.RuntimeHistory = true;
            Navigate(coordinator, CharacterTestRuntimeAiLabWorkspace.RuntimeFrame);

            CharacterTestRuntimeAiLabRuntimeFramePageContext runtimeContext =
                new CharacterTestRuntimeAiLabRuntimeFramePageContext(
                    new CharacterTestRuntimeAiLabTimelineViewport(
                        30,
                        330,
                        2,
                        1732f,
                        followLatest: false),
                    default,
                    default,
                    detailsVisible: true);
            CharacterTestRuntimeAiLabNavigationResult runtimeUpdated =
                coordinator.TryUpdateRuntimeFrameContext(
                    coordinator.CurrentState.Generation,
                    coordinator.CurrentState.Revision,
                    runtimeContext);
            Assert.That(runtimeUpdated.Success, Is.True, runtimeUpdated.Message);
            CharacterTestRuntimeAiLabNavigationResult behaviorUpdated =
                coordinator.TryUpdateBehaviorCardContext(
                    coordinator.CurrentState.Generation,
                    coordinator.CurrentState.Revision,
                    "group-a",
                    "entry-a",
                    "card-a",
                    CharacterTestRuntimeAiLabSelectedElementKind.Stage,
                    "stage-a",
                    new CharacterTestRuntimeAiLabBehaviorCardViewport(
                        90,
                        690,
                        1.75f,
                        128f,
                        true));
            Assert.That(behaviorUpdated.Success, Is.True, behaviorUpdated.Message);

            int mountsBeforeModeSwitch = adapter.WorkspaceMountCount;
            CharacterTestRuntimeAiLabNavigationResult plan =
                coordinator.TrySetTimelineMode(
                    coordinator.CurrentState.Generation,
                    coordinator.CurrentState.Revision,
                    CharacterTestRuntimeAiLabWorkspace.RuntimeFrame,
                    CharacterTestRuntimeAiLabTimelineMode.BehaviorPlan,
                    outgoingModeExpanded: false);

            Assert.That(plan.Success, Is.True, plan.Message);
            Assert.That(adapter.WorkspaceMountCount, Is.EqualTo(mountsBeforeModeSwitch));
            Assert.That(coordinator.CurrentState.ActiveWorkspace,
                Is.EqualTo(CharacterTestRuntimeAiLabWorkspace.RuntimeFrame));
            Assert.That(coordinator.CurrentState.PageContext.TimelineMode,
                Is.EqualTo(CharacterTestRuntimeAiLabTimelineMode.BehaviorPlan));
            Assert.That(coordinator.CurrentState.PageContext.RuntimeTraceExpanded, Is.False);
            Assert.That(
                coordinator.CurrentState.PageContext.RuntimeFrameContext.Viewport.StartFrame,
                Is.EqualTo(30));
            Assert.That(
                coordinator.CurrentState.PageContext.BehaviorCardViewport.HorizontalStartFrame,
                Is.EqualTo(90));
            Assert.That(coordinator.CurrentState.PageContext.SelectedElementStableId,
                Is.EqualTo("stage-a"));

            coordinator.ToggleVisibility();
            coordinator.ToggleVisibility();
            Assert.That(coordinator.CurrentState.PageContext.TimelineMode,
                Is.EqualTo(CharacterTestRuntimeAiLabTimelineMode.BehaviorPlan));

            CharacterTestRuntimeAiLabNavigationResult trace =
                coordinator.TrySetTimelineMode(
                    coordinator.CurrentState.Generation,
                    coordinator.CurrentState.Revision,
                    CharacterTestRuntimeAiLabWorkspace.RuntimeFrame,
                    CharacterTestRuntimeAiLabTimelineMode.RuntimeTrace,
                    outgoingModeExpanded: false);
            Assert.That(trace.Success, Is.True, trace.Message);
            Assert.That(coordinator.CurrentState.PageContext.TimelineMode,
                Is.EqualTo(CharacterTestRuntimeAiLabTimelineMode.RuntimeTrace));
            Assert.That(coordinator.CurrentState.PageContext.BehaviorPlanExpanded, Is.False);
        }

        [Test]
        public void UnifiedTimelineMode_SetupPhaseFailsClosed()
        {
            CharacterTestRuntimeAiLabShellCoordinator coordinator =
                CreateCoordinator(out FakeMountAdapter _);
            Navigate(coordinator, CharacterTestRuntimeAiLabWorkspace.Setup);
            Navigate(coordinator, CharacterTestRuntimeAiLabWorkspace.RuntimeFrame);

            CharacterTestRuntimeAiLabNavigationResult result =
                coordinator.TrySetTimelineMode(
                    coordinator.CurrentState.Generation,
                    coordinator.CurrentState.Revision,
                    CharacterTestRuntimeAiLabWorkspace.RuntimeFrame,
                    CharacterTestRuntimeAiLabTimelineMode.BehaviorPlan,
                    outgoingModeExpanded: true);

            Assert.That(result.Success, Is.False);
            Assert.That(result.Code, Is.EqualTo("TIMELINE_MODE_SESSION_UNAVAILABLE"));
            Assert.That(coordinator.CurrentState.PageContext.TimelineMode,
                Is.EqualTo(CharacterTestRuntimeAiLabTimelineMode.RuntimeTrace));
        }

        [Test]
        public void UnifiedTimelineMode_BlankGroupCanOpenBehaviorLogicButNotBehaviorPlan()
        {
            CharacterTestRuntimeAiLabShellCoordinator coordinator =
                CreateCoordinator(out FakeMountAdapter adapter);
            Navigate(coordinator, CharacterTestRuntimeAiLabWorkspace.Setup);
            adapter.SetupPhase = false;
            adapter.RuntimeHistory = true;
            Navigate(coordinator, CharacterTestRuntimeAiLabWorkspace.RuntimeFrame);

            CharacterTestRuntimeAiLabNavigationResult logic =
                coordinator.TrySetTimelineMode(
                    coordinator.CurrentState.Generation,
                    coordinator.CurrentState.Revision,
                    CharacterTestRuntimeAiLabWorkspace.RuntimeFrame,
                    CharacterTestRuntimeAiLabTimelineMode.BehaviorLogic,
                    outgoingModeExpanded: true);

            Assert.That(logic.Success, Is.True, logic.Message);
            Assert.That(coordinator.CurrentState.PageContext.TimelineMode,
                Is.EqualTo(CharacterTestRuntimeAiLabTimelineMode.BehaviorLogic));

            CharacterTestRuntimeAiLabNavigationResult plan =
                coordinator.TrySetTimelineMode(
                    coordinator.CurrentState.Generation,
                    coordinator.CurrentState.Revision,
                    CharacterTestRuntimeAiLabWorkspace.RuntimeFrame,
                    CharacterTestRuntimeAiLabTimelineMode.BehaviorPlan,
                    outgoingModeExpanded: true);

            Assert.That(plan.Success, Is.False);
            Assert.That(plan.Code, Is.EqualTo("BEHAVIOR_CARD_CONTEXT_UNAVAILABLE"));
            Assert.That(coordinator.CurrentState.PageContext.TimelineMode,
                Is.EqualTo(CharacterTestRuntimeAiLabTimelineMode.BehaviorLogic));
        }

        [Test]
        public void UnifiedTimelineMode_OldRoutesAliasWithoutRemount()
        {
            CharacterTestRuntimeAiLabShellCoordinator coordinator =
                CreateCoordinator(out FakeMountAdapter adapter);
            Navigate(coordinator, CharacterTestRuntimeAiLabWorkspace.Setup);
            adapter.SetupPhase = false;
            adapter.RuntimeHistory = true;
            Navigate(coordinator, CharacterTestRuntimeAiLabWorkspace.RuntimeFrame);
            CharacterTestRuntimeAiLabNavigationResult context =
                coordinator.TryUpdateBehaviorCardContext(
                    coordinator.CurrentState.Generation,
                    coordinator.CurrentState.Revision,
                    "group-a",
                    "entry-a",
                    "card-a",
                    CharacterTestRuntimeAiLabSelectedElementKind.Detector,
                    "detector-a",
                    CharacterTestRuntimeAiLabBehaviorCardViewport.Default);
            Assert.That(context.Success, Is.True, context.Message);
            int mountCount = adapter.WorkspaceMountCount;

            CharacterTestRuntimeAiLabNavigationResult behaviorAlias =
                coordinator.Navigate(
                    coordinator.CurrentState.Generation,
                    coordinator.CurrentState.Revision,
                    CharacterTestRuntimeAiLabWorkspace.RuntimeFrame,
                    CharacterTestRuntimeAiLabWorkspace.BehaviorCard);
            Assert.That(behaviorAlias.Success, Is.True, behaviorAlias.Message);
            Assert.That(behaviorAlias.Code, Is.EqualTo("BEHAVIOR_CARD_ROUTE_ALIASED"));
            Assert.That(coordinator.CurrentState.PageContext.TimelineMode,
                Is.EqualTo(CharacterTestRuntimeAiLabTimelineMode.BehaviorLogic));

            CharacterTestRuntimeAiLabNavigationResult runtimeAlias =
                coordinator.Navigate(
                    coordinator.CurrentState.Generation,
                    coordinator.CurrentState.Revision,
                    CharacterTestRuntimeAiLabWorkspace.RuntimeFrame,
                    CharacterTestRuntimeAiLabWorkspace.RuntimeFrame);
            Assert.That(runtimeAlias.Success, Is.True, runtimeAlias.Message);
            Assert.That(runtimeAlias.Code, Is.EqualTo("RUNTIME_FRAME_ROUTE_ALIASED"));
            Assert.That(coordinator.CurrentState.PageContext.TimelineMode,
                Is.EqualTo(CharacterTestRuntimeAiLabTimelineMode.RuntimeTrace));
            Assert.That(adapter.WorkspaceMountCount, Is.EqualTo(mountCount));
        }

        private static CharacterTestRuntimeAiLabShellCoordinator CreateCoordinator(
            out FakeMountAdapter adapter)
        {
            var coordinator = new CharacterTestRuntimeAiLabShellCoordinator(
                generation: 17,
                playerDeckAvailable: true,
                labAvailable: true);
            adapter = new FakeMountAdapter();
            CharacterTestRuntimeAiLabNavigationResult result =
                coordinator.AttachMountAdapter(17, adapter);
            Assert.That(result.Success, Is.True, result.Message);
            return coordinator;
        }

        private static void Navigate(
            CharacterTestRuntimeAiLabShellCoordinator coordinator,
            CharacterTestRuntimeAiLabWorkspace target)
        {
            CharacterTestRuntimeAiLabNavigationResult result = coordinator.Navigate(
                coordinator.CurrentState.Generation,
                coordinator.CurrentState.Revision,
                coordinator.CurrentState.ActiveWorkspace,
                target);
            Assert.That(result.Success, Is.True, result.Message);
        }

        private sealed class FakeMountAdapter : ICharacterTestRuntimeAiLabShellMountAdapter
        {
            internal Action<CharacterTestRuntimeAiLabShellState> BeforeWorkspaceMount { get; set; }
            internal CharacterTestRuntimeAiLabWorkspace? FailWorkspace { get; set; }
            internal bool FailAllWorkspaces { get; set; }
            internal bool FailTransition { get; set; }
            internal bool FailRecovery { get; set; }
            internal bool FailPrepare { get; set; }
            internal bool FailModalDismiss { get; set; }
            internal bool HasTopModal { get; set; }
            internal bool SetupPhase { get; set; } = true;
            internal bool RuntimeHistory { get; set; }
            internal bool ClearBehaviorCardSelection { get; set; }
            internal string NormalizedBehaviorCardEntryId { get; set; } = string.Empty;
            internal HashSet<CharacterTestRuntimeAiLabWorkspace> UnavailableWorkspaces { get; } =
                new HashSet<CharacterTestRuntimeAiLabWorkspace>();
            internal HashSet<CharacterTestRuntimeAiLabWorkspace> InvalidContextWorkspaces { get; } =
                new HashSet<CharacterTestRuntimeAiLabWorkspace>();
            internal CharacterTestRuntimeAiLabWorkspace MountedWorkspace { get; private set; }
            internal string MountedEntryId { get; private set; } = string.Empty;
            internal int VisibleMountCount { get; private set; }
            internal int TransitionMountCount { get; private set; }
            internal int RecoveryMountCount { get; private set; }
            internal int PrepareCount { get; private set; }
            internal int WorkspaceMountCount { get; private set; }

            public bool IsSetupPhase => SetupPhase;

            public bool HasRuntimeHistory => RuntimeHistory;

            public bool TryPrepareForReopen(out string diagnostic)
            {
                PrepareCount++;
                if (FailPrepare)
                {
                    diagnostic = "preflight failed";
                    return false;
                }
                diagnostic = string.Empty;
                return true;
            }

            public bool IsWorkspaceAvailable(CharacterTestRuntimeAiLabWorkspace workspace)
            {
                return !UnavailableWorkspaces.Contains(workspace);
            }

            public bool TryNormalizeWorkspaceContext(
                CharacterTestRuntimeAiLabWorkspace workspace,
                CharacterTestRuntimeAiLabPageContext context,
                out CharacterTestRuntimeAiLabPageContext normalizedContext,
                out string diagnostic)
            {
                bool normalizeBehaviorCard = workspace == CharacterTestRuntimeAiLabWorkspace.BehaviorCard
                    && (ClearBehaviorCardSelection
                        || NormalizedBehaviorCardEntryId.Length > 0);
                normalizedContext = normalizeBehaviorCard
                    ? context.WithBehaviorCard(
                        context.GroupDraftId,
                        NormalizedBehaviorCardEntryId.Length > 0
                            ? NormalizedBehaviorCardEntryId
                            : context.EntryId,
                        context.CardDraftId,
                        ClearBehaviorCardSelection
                            ? CharacterTestRuntimeAiLabSelectedElementKind.None
                            : context.SelectedElementKind,
                        ClearBehaviorCardSelection
                            ? string.Empty
                            : context.SelectedElementStableId,
                        context.BehaviorCardViewport)
                    : context;
                diagnostic = string.Empty;
                if (!InvalidContextWorkspaces.Contains(workspace))
                    return true;
                diagnostic = "workspace context invalid";
                return false;
            }

            public bool TryMountWorkspace(
                CharacterTestRuntimeAiLabShellState state,
                out string diagnostic)
            {
                WorkspaceMountCount++;
                BeforeWorkspaceMount?.Invoke(state);
                if (FailAllWorkspaces
                    || (FailWorkspace.HasValue && FailWorkspace.Value == state.ActiveWorkspace))
                {
                    diagnostic = "workspace mount failed";
                    return false;
                }
                MountedWorkspace = state.ActiveWorkspace;
                MountedEntryId = state.PageContext.EntryId;
                VisibleMountCount = 1;
                diagnostic = string.Empty;
                return true;
            }

            public bool TryMountTransition(
                CharacterTestRuntimeAiLabShellState state,
                out string diagnostic)
            {
                if (FailTransition)
                {
                    diagnostic = "transition mount failed";
                    return false;
                }
                TransitionMountCount++;
                VisibleMountCount = 1;
                diagnostic = string.Empty;
                return true;
            }

            public bool TryMountRecovery(
                CharacterTestRuntimeAiLabShellState state,
                out string diagnostic)
            {
                if (FailRecovery)
                {
                    diagnostic = "recovery mount failed";
                    return false;
                }
                RecoveryMountCount++;
                VisibleMountCount = 1;
                diagnostic = string.Empty;
                return true;
            }

            public bool TryDismissTopModalForVisibilityToggle(
                out bool dismissed,
                out string diagnostic)
            {
                if (FailModalDismiss)
                {
                    dismissed = false;
                    diagnostic = "modal dismiss failed";
                    return false;
                }
                dismissed = HasTopModal;
                HasTopModal = false;
                diagnostic = string.Empty;
                return true;
            }

            public void UnmountAll()
            {
                VisibleMountCount = 0;
            }
        }
    }
}
