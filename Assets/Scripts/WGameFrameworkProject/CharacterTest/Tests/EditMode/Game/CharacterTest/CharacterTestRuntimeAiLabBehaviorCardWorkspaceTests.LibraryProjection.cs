using System;
using System.Reflection;
using MxFramework.UI;
using NUnit.Framework;
using UnityEngine;

namespace MxFramework.Game.CharacterTest.Tests
{
    public sealed partial class CharacterTestRuntimeAiLabBehaviorCardWorkspaceTests
    {

        [Test]
        public void LibraryProjectionCombinesQueryWithAvailabilityFilter()
        {
            CharacterTestRuntimeAiPlannerPlayerDeckWorkbenchView source = CreateWorkbenchView();
            var viewModel = new CharacterTestFairyGuiRuntimeAiPlannerDevPanelViewModel();

            CharacterTestFairyGuiRuntimeAiPlannerDevPanelViewModelBuilder.FillBehaviorCardLibrary(
                viewModel,
                source,
                new CharacterTestRuntimeAiLabBehaviorCardLibraryState(
                    "alpha",
                    CharacterTestRuntimeAiLabBehaviorCardLibraryFilter.All,
                    string.Empty,
                    string.Empty,
                    0f));

            Assert.That(viewModel.BehaviorCardWorkspace.Library.ActiveCount, Is.EqualTo(1));
            Assert.That(
                viewModel.BehaviorCardWorkspace.Library.Cards[0].StableCardId,
                Is.EqualTo("card.alpha"));
            Assert.That(viewModel.BehaviorCardWorkspace.Library.Cards[0].Installed, Is.True);

            CharacterTestFairyGuiRuntimeAiPlannerDevPanelViewModelBuilder.FillBehaviorCardLibrary(
                viewModel,
                source,
                new CharacterTestRuntimeAiLabBehaviorCardLibraryState(
                    string.Empty,
                    CharacterTestRuntimeAiLabBehaviorCardLibraryFilter.UnavailableOrIncompatible,
                    string.Empty,
                    string.Empty,
                    0f));

            Assert.That(viewModel.BehaviorCardWorkspace.Library.ActiveCount, Is.EqualTo(1));
            Assert.That(
                viewModel.BehaviorCardWorkspace.Library.Cards[0].StableCardId,
                Is.EqualTo("card.beta"));
            Assert.That(viewModel.BehaviorCardWorkspace.Library.Cards[0].Available, Is.False);
            Assert.That(
                viewModel.BehaviorCardWorkspace.Library.Cards[0].BlockReasonText,
                Is.Not.Empty);
        }

        [Test]
        public void LibraryProjectionReportsEveryBehaviorGroupReferenceForSelectedCard()
        {
            CharacterTestRuntimeAiPlannerProjectCatalogSeedResult seed =
                CharacterTestRuntimeAiPlannerProjectCatalogSeed.Create();
            Assert.That(seed.Success, Is.True, seed.Diagnostic);
            var projectState = new CharacterTestRuntimeAiPlannerProjectCatalogState(
                seed.Cards,
                seed.Groups,
                "behavior_card_catalog.json",
                "behavior_group_catalog.json",
                "cards-hash",
                "groups-hash",
                string.Empty);
            CharacterTestRuntimeAiLabDraftWorkspace workspace =
                CharacterTestRuntimeAiLabDraftWorkspace.Import(
                    CharacterTestArenaMatchConfig.CreateProjectBacked(projectState));
            Assert.That(seed.Cards.Cards.Count, Is.GreaterThan(0));
            CharacterTestRuntimeAiPlannerPublishedBehaviorCard publishedCard = seed.Cards.Cards[0];
            Assert.That(workspace.TryOpenPublishedCardForSlot(
                CharacterTestArenaMatchConfig.Team1Id,
                0,
                publishedCard,
                out _,
                out CharacterTestRuntimeAiLabBehaviorCardDraft sourceCard,
                out _,
                out string openMessage), Is.True, openMessage);
            Assert.That(workspace.TryGetGroupForSlot(
                CharacterTestArenaMatchConfig.Team1Id,
                0,
                out CharacterTestRuntimeAiLabBehaviorGroupDraft sourceGroup), Is.True);
            Assert.That(sourceCard.PublishedStableId, Is.Not.Empty);
            var card = new CharacterTestRuntimeAiLabBehaviorAssetCardViewModel
            {
                StableCardId = sourceCard.PublishedStableId
            };
            CharacterTestFairyGuiRuntimeAiPlannerDevPanelViewModelBuilder
                .FillLibraryCardReferences(card, workspace);
            int originalGroupCount = card.ReferencingGroupCount;
            int originalReferenceCount = card.ReferenceCount;
            Assert.That(workspace.TryCreateLocalGroupCopyForSlot(
                CharacterTestArenaMatchConfig.Team1Id,
                0,
                out CharacterTestRuntimeAiLabBehaviorGroupDraft copy,
                out string copyMessage), Is.True, copyMessage);

            CharacterTestFairyGuiRuntimeAiPlannerDevPanelViewModelBuilder
                .FillLibraryCardReferences(card, workspace);

            Assert.That(card.ReferencingGroupCount, Is.EqualTo(originalGroupCount + 1));
            Assert.That(card.ReferenceCount, Is.GreaterThan(originalReferenceCount));
            StringAssert.Contains(sourceGroup.DisplayName, card.ReferenceSummaryText);
            StringAssert.Contains(copy.DisplayName, card.ReferenceSummaryText);
            StringAssert.Contains("草稿", card.ReferenceSummaryText);
        }

        [Test]
        public void LibraryProjectionRefreshesCurrentProjectCardNameEditabilityOnCachedPath()
        {
            var catalogHost = new GameObject("Library Permission Fixture");
            try
            {

            CharacterTestRuntimeAiPlannerPlayerDeckWorkbenchView source = CreateWorkbenchView();
            var viewModel = new CharacterTestFairyGuiRuntimeAiPlannerDevPanelViewModel();
            viewModel.WorkbenchRows =
                new[] { new CharacterTestRuntimeAiLabWorkbenchRowViewModel() };
            viewModel.WorkbenchRows[0].Visible = true;
            viewModel.WorkbenchRows[0].StableCardId = "card.alpha";
            viewModel.WorkbenchRows[0].EntryIndex = 3;
            viewModel.WorkbenchRows[0].ResponsibilityKinds = new[]
            {
                CharacterTestRuntimeAiPlannerBehaviorResponsibilityKind.Targeting,
                CharacterTestRuntimeAiPlannerBehaviorResponsibilityKind.Damage
            };
            viewModel.WorkbenchRows[0].ResponsibilityCompatibilityMask =
                (1 << (int)CharacterTestRuntimeAiPlannerBehaviorResponsibilityKind.Targeting)
                | (1 << (int)CharacterTestRuntimeAiPlannerBehaviorResponsibilityKind.Damage);
            viewModel.BehaviorCardIdentity.EditCard.HasPublishedSource = true;
            viewModel.BehaviorCardIdentity.EditCard.PublishedStableId = "card.alpha";
            viewModel.BehaviorCardIdentity.EditCard.DisplayName = "Alpha 草稿名";
            viewModel.BehaviorCardIdentity.EditCard.DraftRevision = 1;
            var state = new CharacterTestRuntimeAiLabBehaviorCardLibraryState(
                string.Empty,
                CharacterTestRuntimeAiLabBehaviorCardLibraryFilter.All,
                "card.alpha",
                string.Empty,
                0f);

            viewModel.CanEditProjectCardMetadata = false;
            viewModel.CanEditWorkbench = false;
            CharacterTestFairyGuiRuntimeAiPlannerDevPanelViewModelBuilder.FillBehaviorCardLibrary(
                viewModel,
                source,
                state);
            Assert.That(
                viewModel.BehaviorCardWorkspace.Library.Cards[0].DisplayNameEditable,
                Is.False);
            Assert.That(
                viewModel.BehaviorCardWorkspace.Library.Cards[0].ResponsibilityEditable,
                Is.False);

            var catalogController = catalogHost.AddComponent<CharacterTestRuntimeAiPlannerDevSceneController>();
            catalogController.PrepareInitialSetupState();
            Assert.IsTrue(catalogController.ProjectCatalogState.CanWriteProjectCatalogs);
            viewModel.CanEditProjectCardMetadata = true;
            viewModel.CanEditWorkbench = true;
            CharacterTestFairyGuiRuntimeAiPlannerDevPanelViewModelBuilder.FillBehaviorCardLibrary(
                viewModel,
                source,
                state, controller: catalogController);
            Assert.That(
                viewModel.BehaviorCardWorkspace.Library.Cards[0].DisplayNameEditable,
                Is.True,
                "The cached library row must use the current project-card metadata state.");
            Assert.That(
                viewModel.BehaviorCardWorkspace.Library.Cards[0].ResponsibilityEditable,
                Is.True);
            Assert.That(
                viewModel.BehaviorCardWorkspace.Library.Cards[0].Title,
                Is.EqualTo("Alpha 草稿名"));

            viewModel.BehaviorCardIdentity.EditCard.DisplayName = "Alpha 第二草稿名";
            viewModel.BehaviorCardIdentity.EditCard.DraftRevision = 2;
            CharacterTestFairyGuiRuntimeAiPlannerDevPanelViewModelBuilder.FillBehaviorCardLibrary(
                viewModel,
                source,
                state, controller: catalogController);
            Assert.That(
                viewModel.BehaviorCardWorkspace.Library.Cards[0].Title,
                Is.EqualTo("Alpha 第二草稿名"),
                "The card picker must project the current draft name instead of the published catalog name.");
            Assert.That(viewModel.BehaviorCardWorkspace.Library.Cards[0].EntryIndex, Is.EqualTo(3));
            Assert.That(
                viewModel.BehaviorCardWorkspace.Library.Cards[0].ResponsibilityKinds,
                Is.EqualTo(new[]
                {
                    CharacterTestRuntimeAiPlannerBehaviorResponsibilityKind.Targeting,
                    CharacterTestRuntimeAiPlannerBehaviorResponsibilityKind.Damage
                }));

            }
            finally { UnityEngine.Object.DestroyImmediate(catalogHost); }
        }

        [Test]
        public void LibraryProjectionSearchesCapabilityAndResponsibilityNamesFromMetadata()
        {
            var metadata = new CharacterTestRuntimeAiPlannerPublishedBehaviorCardMetadata(
                "压制打击",
                "icon.without.category.semantics",
                1,
                "同时控制并造成伤害",
                "用于近身压制",
                string.Empty,
                requiredCapabilities: new[]
                {
                    CharacterTestRuntimeAiPlannerBehaviorCapability.Control,
                    CharacterTestRuntimeAiPlannerBehaviorCapability.Damage
                });
            var published = new CharacterTestRuntimeAiPlannerPublishedBehaviorCard(
                "card.control-damage",
                1,
                0,
                int.MaxValue,
                metadata,
                null,
                1L,
                1L);
            var source = new CharacterTestRuntimeAiPlannerPlayerDeckWorkbenchView(
                "character.test",
                1,
                0,
                true,
                1L,
                true,
                new[]
                {
                    new CharacterTestRuntimeAiPlannerPlayerDeckCardView(
                        published.StableCardId,
                        published,
                        CharacterTestRuntimeAiPlannerPlayerDeckCardAvailability.Available,
                        true,
                        false,
                        true,
                        false,
                        false,
                        false,
                        CharacterTestRuntimeAiPlannerPlayerDeckCardBlockReason.None)
                },
                Array.Empty<CharacterTestRuntimeAiPlannerInstalledDeckDiagnostic>(),
                Array.Empty<CharacterTestRuntimeAiPlannerInstalledDeckDiagnostic>(),
                false);
            var viewModel = new CharacterTestFairyGuiRuntimeAiPlannerDevPanelViewModel();

            CharacterTestFairyGuiRuntimeAiPlannerDevPanelViewModelBuilder.FillBehaviorCardLibrary(
                viewModel,
                source,
                new CharacterTestRuntimeAiLabBehaviorCardLibraryState(
                    "接战",
                    CharacterTestRuntimeAiLabBehaviorCardLibraryFilter.All,
                    string.Empty,
                    string.Empty,
                    0f));

            CharacterTestRuntimeAiLabBehaviorAssetCardViewModel projected =
                viewModel.BehaviorCardWorkspace.Library.Cards[0];
            Assert.That(viewModel.BehaviorCardWorkspace.Library.ActiveCount, Is.EqualTo(1));
            Assert.That(
                projected.CompatibleResponsibilities,
                Is.EqualTo(new[]
                {
                    CharacterTestRuntimeAiPlannerBehaviorResponsibilityKind.EngageOrPressure,
                    CharacterTestRuntimeAiPlannerBehaviorResponsibilityKind.Damage
                }));
            Assert.That(projected.ResponsibilitySummaryText, Is.EqualTo("可承担：接战、输出"));

            CharacterTestFairyGuiRuntimeAiPlannerDevPanelViewModelBuilder.FillBehaviorCardLibrary(
                viewModel,
                source,
                new CharacterTestRuntimeAiLabBehaviorCardLibraryState(
                    "其他",
                    CharacterTestRuntimeAiLabBehaviorCardLibraryFilter.All,
                    string.Empty,
                    string.Empty,
                    0f));
            Assert.That(viewModel.BehaviorCardWorkspace.Library.ActiveCount, Is.EqualTo(0));
        }

        [Test]
        public void LibraryProjectionUsesDeterministicPrioritySortAndCombinedFlags()
        {
            CharacterTestRuntimeAiPlannerPlayerDeckWorkbenchView ordered = CreateWorkbenchView();
            CharacterTestRuntimeAiPlannerPlayerDeckCardView[] reversed =
            {
                ordered.Cards[1],
                ordered.Cards[0]
            };
            var source = new CharacterTestRuntimeAiPlannerPlayerDeckWorkbenchView(
                ordered.CharacterStableId,
                ordered.SlotCount,
                ordered.KnownCost,
                ordered.IsKnownCostComplete,
                ordered.DeckHash,
                ordered.IsResolved,
                reversed,
                Array.Empty<CharacterTestRuntimeAiPlannerInstalledDeckDiagnostic>(),
                Array.Empty<CharacterTestRuntimeAiPlannerInstalledDeckDiagnostic>(),
                ordered.IsSessionLocked);
            var viewModel = new CharacterTestFairyGuiRuntimeAiPlannerDevPanelViewModel();
            CharacterTestFairyGuiRuntimeAiPlannerDevPanelViewModelBuilder.FillBehaviorCardLibrary(
                viewModel,
                source,
                new CharacterTestRuntimeAiLabBehaviorCardLibraryState(
                    string.Empty,
                    CharacterTestRuntimeAiLabBehaviorCardLibraryFilter.Installed
                        | CharacterTestRuntimeAiLabBehaviorCardLibraryFilter.UnavailableOrIncompatible,
                    "card.alpha",
                    "card.beta",
                    0f));

            CharacterTestRuntimeAiLabBehaviorCardLibraryViewModel library =
                viewModel.BehaviorCardWorkspace.Library;
            Assert.That(library.ActiveCount, Is.EqualTo(2));
            Assert.That(library.Cards[0].StableCardId, Is.EqualTo("card.alpha"));
            Assert.That(library.Cards[1].StableCardId, Is.EqualTo("card.beta"));
            Assert.That(library.FirstVisibleIndex, Is.EqualTo(0));
            Assert.That(library.FocusedIndex, Is.EqualTo(1));
        }

        [Test]
        public void LibraryQueryCollapsesWhitespaceIntoCanonicalTokens()
        {
            var state = new CharacterTestRuntimeAiLabBehaviorCardLibraryState(
                "  alpha\t \r\n beta   gamma  ",
                CharacterTestRuntimeAiLabBehaviorCardLibraryFilter.All,
                string.Empty,
                string.Empty,
                0f);

            Assert.That(state.Query, Is.EqualTo("alpha beta gamma"));
            Assert.That(
                new CharacterTestRuntimeAiLabBehaviorCardLibraryState(
                    "alpha\tbeta",
                    CharacterTestRuntimeAiLabBehaviorCardLibraryFilter.All,
                    string.Empty,
                    string.Empty,
                    0f).Query,
                Is.EqualTo("alpha beta"));
            Assert.That(
                new CharacterTestRuntimeAiLabBehaviorCardLibraryState(
                    "alpha\nbeta",
                    CharacterTestRuntimeAiLabBehaviorCardLibraryFilter.All,
                    string.Empty,
                    string.Empty,
                    0f).Query,
                Is.EqualTo("alpha beta"));
        }

        [Test]
        public void LibraryPagingUsesProjectedCountInsteadOfUnfilteredWorkbenchCount()
        {
            var host = new GameObject("Behavior Card Library Paging Test");
            try
            {
                CharacterTestRuntimeAiPlannerDevSceneController controller =
                    host.AddComponent<CharacterTestRuntimeAiPlannerDevSceneController>();
                controller.PrepareInitialSetupState();
                EnterBehaviorCardWorkspace(controller);
                CharacterTestRuntimeAiLabBehaviorCardWorkspaceViewModel workspace =
                    CreateProjectedWorkspace(9);
                var handler = new CharacterTestRuntimeAiLabBehaviorCardWorkspaceCommandHandler();
                handler.Configure(
                    controller,
                    null,
                    null,
                    null,
                    workspaceView: () => workspace);

                Assert.That(handler.MoveLibraryPage(1, 4, 9), Is.True);
                Assert.That(handler.FirstVisibleLibraryIndex, Is.EqualTo(4));
                Assert.That(handler.MoveLibraryPage(1, 4, 9), Is.True);
                Assert.That(handler.FirstVisibleLibraryIndex, Is.EqualTo(5));
                Assert.That(handler.MoveLibraryPage(1, 4, 9), Is.False);
                Assert.That(handler.FocusLibraryCard(3, 9), Is.True);
                Assert.That(handler.FocusedLibraryIndex, Is.EqualTo(8));
                Assert.That(handler.FocusLibraryCard(4, 9), Is.False);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(host);
            }
        }

        [Test]
        public void LibraryStateUsesStableIdsAcrossProjectionReorderAndDiagnosticsRoundTrip()
        {
            var coordinator = new CharacterTestRuntimeAiLabShellCoordinator(
                generation: 23,
                playerDeckAvailable: true,
                labAvailable: true);
            var adapter = new BehaviorCardMountAdapter();
            Assert.That(coordinator.AttachMountAdapter(23, adapter).Success, Is.True);
            CharacterTestRuntimeAiLabShellState state = coordinator.CurrentState;
            CharacterTestRuntimeAiLabNavigationResult opened = coordinator.NavigateToBehaviorCard(
                state.Generation,
                state.Revision,
                state.ActiveWorkspace,
                "group.stable",
                "entry.stable",
                "card.stable",
                CharacterTestRuntimeAiLabSelectedElementKind.None,
                string.Empty,
                CharacterTestRuntimeAiLabBehaviorCardViewport.Default);
            Assert.That(opened.Success, Is.True, opened.Message);
            state = coordinator.CurrentState;
            var libraryState = new CharacterTestRuntimeAiLabBehaviorCardLibraryState(
                "beta",
                CharacterTestRuntimeAiLabBehaviorCardLibraryFilter.Installed
                    | CharacterTestRuntimeAiLabBehaviorCardLibraryFilter.RuntimeActive,
                "card.beta",
                "card.beta",
                72f);
            Assert.That(coordinator.TryUpdateBehaviorCardLibraryState(
                state.Generation,
                state.Revision,
                libraryState).Success, Is.True);

            Assert.That(coordinator.ToggleVisibility().Success, Is.True);
            Assert.That(coordinator.ToggleVisibility().Success, Is.True);

            state = coordinator.CurrentState;
            Assert.That(coordinator.Navigate(
                state.Generation,
                state.Revision,
                CharacterTestRuntimeAiLabWorkspace.BehaviorCard,
                CharacterTestRuntimeAiLabWorkspace.Facts).Success, Is.True);
            state = coordinator.CurrentState;
            Assert.That(coordinator.Back(
                state.Generation,
                state.Revision,
                CharacterTestRuntimeAiLabWorkspace.Facts).Success, Is.True);

            CharacterTestRuntimeAiLabBehaviorCardLibraryState restored =
                coordinator.CurrentState.PageContext.BehaviorCardLibraryState;
            Assert.That(restored.Query, Is.EqualTo("beta"));
            Assert.That(restored.Filter, Is.EqualTo(libraryState.Filter));
            Assert.That(restored.FirstVisibleStableCardId, Is.EqualTo("card.beta"));
            Assert.That(restored.FocusedStableCardId, Is.EqualTo("card.beta"));
            Assert.That(restored.ScrollOffsetY, Is.EqualTo(72f));
        }

        [Test]
        public void TimelineFitUsesProjectedDurationAndZoomPreservesViewportCenter()
        {
            var host = new GameObject("Behavior Card Timeline Viewport Test");
            try
            {
                CharacterTestRuntimeAiPlannerDevSceneController controller =
                    host.AddComponent<CharacterTestRuntimeAiPlannerDevSceneController>();
                controller.PrepareInitialSetupState();
                EnterBehaviorCardWorkspace(controller);
                CharacterTestRuntimeAiLabBehaviorCardWorkspaceViewModel workspace =
                    CreateProjectedWorkspace(1);
                workspace.Timeline.DurationFrames = 900;
                var handler = new CharacterTestRuntimeAiLabBehaviorCardWorkspaceCommandHandler();
                handler.Configure(
                    controller,
                    null,
                    null,
                    null,
                    workspaceView: () => workspace);

                Assert.That(handler.UpdateTimelineViewport(0, 0, 0, true), Is.True);
                CharacterTestRuntimeAiLabBehaviorCardViewport fit =
                    controller.ShellCoordinator.CurrentState.PageContext.BehaviorCardViewport;
                Assert.That(fit.HorizontalStartFrame, Is.EqualTo(0));
                Assert.That(fit.HorizontalEndFrame, Is.EqualTo(900));

                Assert.That(handler.UpdateTimelineViewport(1, 0, 0, false), Is.True);
                CharacterTestRuntimeAiLabBehaviorCardViewport panned =
                    controller.ShellCoordinator.CurrentState.PageContext.BehaviorCardViewport;
                int centerBeforeZoom = (panned.HorizontalStartFrame + panned.HorizontalEndFrame) / 2;
                Assert.That(handler.UpdateTimelineViewport(0, 1, 0, false), Is.True);
                CharacterTestRuntimeAiLabBehaviorCardViewport zoomed =
                    controller.ShellCoordinator.CurrentState.PageContext.BehaviorCardViewport;
                int centerAfterZoom = (zoomed.HorizontalStartFrame + zoomed.HorizontalEndFrame) / 2;
                Assert.That(centerAfterZoom, Is.EqualTo(centerBeforeZoom).Within(1));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(host);
            }
        }

        [Test]
        public void UnavailableLibraryCardCannotOpenThroughForgedPageCommand()
        {
            var workspace = CreateProjectedWorkspace(1);
            workspace.Library.Cards[0].StableCardId = "card.unavailable";
            workspace.Library.Cards[0].Available = false;
            workspace.Library.Cards[0].BlockReasonText = "缺少兼容 Published revision";
            int openCount = 0;
            var handler = new CharacterTestRuntimeAiLabBehaviorCardWorkspaceCommandHandler();
            handler.Configure(
                null,
                null,
                _ =>
                {
                    openCount++;
                    return true;
                },
                null,
                workspaceView: () => workspace);

            Assert.That(handler.TryOpenCard("card.unavailable"), Is.False);
            Assert.That(openCount, Is.EqualTo(0));
            StringAssert.Contains("兼容", handler.LastMessage);
        }

        [Test]
        public void UnknownOrProviderlessLibraryCardCannotOpenThroughForgedPageCommand()
        {
            CharacterTestRuntimeAiPlannerPlayerDeckWorkbenchView provider = CreateWorkbenchView();
            CharacterTestRuntimeAiLabBehaviorCardWorkspaceViewModel workspace = CreateProjectedWorkspace(1);
            workspace.Library.Cards[0].StableCardId = "card.alpha";
            workspace.Library.Cards[0].Available = true;
            int openCount = 0;
            var handler = new CharacterTestRuntimeAiLabBehaviorCardWorkspaceCommandHandler();
            handler.Configure(
                null,
                null,
                _ =>
                {
                    openCount++;
                    return true;
                },
                null,
                workspaceView: () => workspace,
                libraryView: () => provider);

            Assert.That(handler.TryOpenCard("card.unknown"), Is.False);
            Assert.That(openCount, Is.EqualTo(0));

            handler.Configure(
                null,
                null,
                _ =>
                {
                    openCount++;
                    return true;
                },
                null,
                workspaceView: () => workspace,
                libraryView: () => null);
            Assert.That(handler.TryOpenCard("card.alpha"), Is.False);
            Assert.That(openCount, Is.EqualTo(0));
        }

        [Test]
        public void StaleProjectedCardCannotOpenAfterLeavingWorkbenchUnion()
        {
            CharacterTestRuntimeAiPlannerPlayerDeckWorkbenchView provider = CreateWorkbenchView();
            CharacterTestRuntimeAiLabBehaviorCardWorkspaceViewModel workspace = CreateProjectedWorkspace(1);
            workspace.Library.Cards[0].StableCardId = "card.alpha";
            workspace.Library.Cards[0].Available = true;
            int openCount = 0;
            var handler = new CharacterTestRuntimeAiLabBehaviorCardWorkspaceCommandHandler();
            handler.Configure(
                null,
                null,
                _ =>
                {
                    openCount++;
                    return true;
                },
                null,
                workspaceView: () => workspace,
                libraryView: () => provider);

            Assert.That(handler.TryOpenCard("card.alpha"), Is.True);
            Assert.That(openCount, Is.EqualTo(1));

            var incompatibleAlpha = new CharacterTestRuntimeAiPlannerPlayerDeckCardView(
                "card.alpha",
                null,
                CharacterTestRuntimeAiPlannerPlayerDeckCardAvailability.Incompatible,
                true,
                false,
                false,
                false,
                false,
                false,
                CharacterTestRuntimeAiPlannerPlayerDeckCardBlockReason.Incompatible);
            provider = new CharacterTestRuntimeAiPlannerPlayerDeckWorkbenchView(
                provider.CharacterStableId,
                provider.SlotCount,
                provider.KnownCost,
                provider.IsKnownCostComplete,
                provider.DeckHash,
                false,
                new[] { incompatibleAlpha },
                Array.Empty<CharacterTestRuntimeAiPlannerInstalledDeckDiagnostic>(),
                Array.Empty<CharacterTestRuntimeAiPlannerInstalledDeckDiagnostic>(),
                provider.IsSessionLocked);
            Assert.That(handler.TryOpenCard("card.alpha"), Is.False);
            Assert.That(openCount, Is.EqualTo(1));
            StringAssert.Contains("不兼容", handler.LastMessage);

            provider = new CharacterTestRuntimeAiPlannerPlayerDeckWorkbenchView(
                provider.CharacterStableId,
                provider.SlotCount,
                provider.KnownCost,
                provider.IsKnownCostComplete,
                provider.DeckHash,
                provider.IsResolved,
                Array.Empty<CharacterTestRuntimeAiPlannerPlayerDeckCardView>(),
                Array.Empty<CharacterTestRuntimeAiPlannerInstalledDeckDiagnostic>(),
                Array.Empty<CharacterTestRuntimeAiPlannerInstalledDeckDiagnostic>(),
                provider.IsSessionLocked);

            Assert.That(handler.TryOpenCard("card.alpha"), Is.False);
            Assert.That(openCount, Is.EqualTo(1));
            StringAssert.Contains("不在", handler.LastMessage);
        }

        [Test]
        public void ReconcilePreservesFilteredFocusUntilCardLeavesWorkbenchUnion()
        {
            var host = new GameObject("Behavior Card Filtered Focus Test");
            try
            {
                CharacterTestRuntimeAiPlannerDevSceneController controller =
                    host.AddComponent<CharacterTestRuntimeAiPlannerDevSceneController>();
                controller.PrepareInitialSetupState();
                EnterBehaviorCardWorkspace(controller);
                CharacterTestRuntimeAiLabShellState shell = controller.ShellCoordinator.CurrentState;
                Assert.That(controller.ShellCoordinator.TryUpdateBehaviorCardLibraryState(
                    shell.Generation,
                    shell.Revision,
                    new CharacterTestRuntimeAiLabBehaviorCardLibraryState(
                        "alpha",
                        CharacterTestRuntimeAiLabBehaviorCardLibraryFilter.All,
                        "card.alpha",
                        "card.beta",
                        0f)).Success, Is.True);

                CharacterTestRuntimeAiPlannerPlayerDeckWorkbenchView provider = CreateWorkbenchView();
                CharacterTestRuntimeAiLabBehaviorCardWorkspaceViewModel workspace = CreateProjectedWorkspace(1);
                workspace.Library.Cards[0].StableCardId = "card.alpha";
                var handler = new CharacterTestRuntimeAiLabBehaviorCardWorkspaceCommandHandler();
                handler.Configure(
                    controller,
                    null,
                    null,
                    null,
                    workspaceView: () => workspace,
                    libraryView: () => provider);

                handler.ReconcileLibraryIndex();
                Assert.That(
                    controller.ShellCoordinator.CurrentState.PageContext.BehaviorCardLibraryState
                        .FocusedStableCardId,
                    Is.EqualTo("card.beta"));

                provider = new CharacterTestRuntimeAiPlannerPlayerDeckWorkbenchView(
                    provider.CharacterStableId,
                    provider.SlotCount,
                    provider.KnownCost,
                    provider.IsKnownCostComplete,
                    provider.DeckHash,
                    provider.IsResolved,
                    new[] { provider.Cards[0] },
                    Array.Empty<CharacterTestRuntimeAiPlannerInstalledDeckDiagnostic>(),
                    Array.Empty<CharacterTestRuntimeAiPlannerInstalledDeckDiagnostic>(),
                    provider.IsSessionLocked);
                handler.ReconcileLibraryIndex();
                Assert.That(
                    controller.ShellCoordinator.CurrentState.PageContext.BehaviorCardLibraryState
                        .FocusedStableCardId,
                    Is.Empty);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(host);
            }
        }

        [Test]
        public void WorkspaceRevisionIncludesLibraryAndViewportState()
        {
            var first = new CharacterTestFairyGuiRuntimeAiPlannerDevPanelViewModel();
            first.BehaviorCardWorkspace.Library.Query = "alpha";
            first.BehaviorCardWorkspace.Timeline.HorizontalStartFrame = 0;
            first.BehaviorCardWorkspace.Timeline.HorizontalEndFrame = 240;
            CharacterTestFairyGuiRuntimeAiPlannerDevPanelViewModelBuilder
                .FinalizeBehaviorCardWorkspaceRevision(first);

            var second = new CharacterTestFairyGuiRuntimeAiPlannerDevPanelViewModel();
            second.BehaviorCardWorkspace.Library.Query = "beta";
            second.BehaviorCardWorkspace.Timeline.HorizontalStartFrame = 30;
            second.BehaviorCardWorkspace.Timeline.HorizontalEndFrame = 270;
            CharacterTestFairyGuiRuntimeAiPlannerDevPanelViewModelBuilder
                .FinalizeBehaviorCardWorkspaceRevision(second);

            Assert.That(
                second.BehaviorCardWorkspace.WorkspaceRevision,
                Is.Not.EqualTo(first.BehaviorCardWorkspace.WorkspaceRevision));
        }
    }
}
