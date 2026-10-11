using System;
using System.Collections.Generic;
using System.Linq;
using MxFramework.UI;
using MxFramework.UI.FairyGui;
using NUnit.Framework;
using Fgui = global::FairyGUI;

namespace MxFramework.Game.CharacterTest.Tests
{
    public sealed partial class CharacterTestRuntimeAiLabPublishCurrentChangesTests
    {
        [Test]
        public void PublishCurrentChanges_CardAndGroupCommitOnceAndKeepOldRevisions()
        {
            Fixture fixture = CreateFixture();
            MarkCardMetadataChanged(fixture);

            CharacterTestRuntimeAiLabPublishCurrentChangesResult previewResult =
                fixture.Authoring.PreparePublishCurrentChanges(
                    fixture.Workspace.WorkspaceId,
                    fixture.Workspace.CurrentRevision,
                    fixture.Group,
                    fixture.Workspace.Cards,
                    fixture.References);

            Assert.That(previewResult.PreviewReady, Is.True, previewResult.Message);
            Assert.That(previewResult.Preview.ChangedCardCount, Is.EqualTo(1));
            Assert.That(previewResult.Preview.GroupDraftId, Is.EqualTo(fixture.Group.GroupDraftId));
            Assert.That(previewResult.Preview.BuildSummary(), Does.Contain("发布本次修改"));

            CharacterTestRuntimeAiLabPublishCurrentChangesResult committed =
                fixture.Authoring.CommitPublishCurrentChanges(
                    previewResult.Preview.Token,
                    fixture.Workspace.WorkspaceId,
                    fixture.Workspace.CurrentRevision,
                    fixture.Group,
                    fixture.Workspace.Cards,
                    fixture.References);

            Assert.That(committed.CatalogCommitSucceeded, Is.True, committed.Message);
            Assert.That(committed.PublishedCards.Count, Is.EqualTo(1));
            Assert.That(fixture.WriteCount, Is.EqualTo(1));
            CharacterTestRuntimeAiPlannerPublishedBehaviorCard published =
                committed.PublishedCards[0].Card;
            Assert.That(committed.State.CardCatalog.TryGetExact(
                published.StableCardId,
                fixture.Card.SourcePublishedRevision,
                out _), Is.True, "append-only Card source revision was removed");
            Assert.That(committed.State.GroupCatalog.TryGetExact(
                committed.Group.StableGroupId,
                fixture.Group.SourceProjectRevision,
                out _), Is.True, "append-only Group source revision was removed");
            Assert.That(FindPinnedCard(committed.Group, published.StableCardId).CardRevision,
                Is.EqualTo(published.Revision));

            CharacterTestRuntimeAiLabPublishCurrentChangesResult duplicate =
                fixture.Authoring.CommitPublishCurrentChanges(
                    previewResult.Preview.Token,
                    fixture.Workspace.WorkspaceId,
                    fixture.Workspace.CurrentRevision,
                    fixture.Group,
                    fixture.Workspace.Cards,
                    fixture.References);
            Assert.That(duplicate.CatalogCommitSucceeded, Is.True);
            Assert.That(duplicate.DuplicateConfirmation, Is.True);
            Assert.That(fixture.WriteCount, Is.EqualTo(1), "double confirmation wrote twice");
        }

        [Test]
        public void PublishCurrentChanges_NoChangesDoesNotWrite()
        {
            Fixture fixture = CreateFixture();

            CharacterTestRuntimeAiLabPublishCurrentChangesResult result =
                fixture.Authoring.PreparePublishCurrentChanges(
                    fixture.Workspace.WorkspaceId,
                    fixture.Workspace.CurrentRevision,
                    fixture.Group,
                    fixture.Workspace.Cards,
                    fixture.References);

            Assert.That(result.Status,
                Is.EqualTo(CharacterTestRuntimeAiLabPublishCurrentChangesStatus.NoChanges),
                result.Message);
            Assert.That(result.CatalogChanged, Is.False);
            Assert.That(fixture.WriteCount, Is.Zero);
        }

        [Test]
        public void PublishCurrentChanges_InvalidCardPreflightDoesNotWrite()
        {
            Fixture fixture = CreateFixture();
            fixture.Card.Cost = 99;
            fixture.Card.CurrentRevision++;
            fixture.Workspace.CurrentRevision++;

            CharacterTestRuntimeAiLabPublishCurrentChangesResult result =
                fixture.Authoring.PreparePublishCurrentChanges(
                    fixture.Workspace.WorkspaceId,
                    fixture.Workspace.CurrentRevision,
                    fixture.Group,
                    fixture.Workspace.Cards,
                    fixture.References);

            Assert.That(result.Status,
                Is.EqualTo(CharacterTestRuntimeAiLabPublishCurrentChangesStatus.CardPreflightFailed),
                result.Message);
            Assert.That(result.Stage,
                Is.EqualTo(CharacterTestRuntimeAiLabPublishCurrentChangesStage.CardPreflight));
            Assert.That(fixture.WriteCount, Is.Zero);
        }

        [Test]
        public void PublishCurrentChanges_StalePreviewDoesNotWrite()
        {
            Fixture fixture = CreateFixture();
            MarkCardMetadataChanged(fixture);
            CharacterTestRuntimeAiLabPublishCurrentChangesResult preview =
                Prepare(fixture);

            fixture.Card.CurrentRevision++;
            fixture.Workspace.CurrentRevision++;
            CharacterTestRuntimeAiLabPublishCurrentChangesResult result =
                fixture.Authoring.CommitPublishCurrentChanges(
                    preview.Preview.Token,
                    fixture.Workspace.WorkspaceId,
                    fixture.Workspace.CurrentRevision,
                    fixture.Group,
                    fixture.Workspace.Cards,
                    fixture.References);

            Assert.That(result.Status,
                Is.EqualTo(CharacterTestRuntimeAiLabPublishCurrentChangesStatus.PreviewExpired),
                result.Message);
            Assert.That(result.CatalogChanged, Is.False);
            Assert.That(fixture.WriteCount, Is.Zero);
        }

        [Test]
        public void PublishCurrentChanges_WriteConflictKeepsSourceState()
        {
            Fixture fixture = CreateFixture();
            MarkCardMetadataChanged(fixture);
            CharacterTestRuntimeAiLabPublishCurrentChangesResult preview = Prepare(fixture);
            fixture.FailWriteWithConflict = true;

            CharacterTestRuntimeAiLabPublishCurrentChangesResult result =
                fixture.Authoring.CommitPublishCurrentChanges(
                    preview.Preview.Token,
                    fixture.Workspace.WorkspaceId,
                    fixture.Workspace.CurrentRevision,
                    fixture.Group,
                    fixture.Workspace.Cards,
                    fixture.References);

            Assert.That(result.Status,
                Is.EqualTo(CharacterTestRuntimeAiLabPublishCurrentChangesStatus.SourceConflict),
                result.Message);
            Assert.That(result.Conflict, Is.True);
            Assert.That(result.CatalogChanged, Is.False);
            Assert.That(fixture.Authoring.State, Is.SameAs(fixture.SourceState));
            Assert.That(fixture.WriteCount, Is.EqualTo(1));
        }

        [Test]
        public void PublishCurrentChanges_CommitFailureConfirmsRollbackBeforeReportingZeroWrite()
        {
            Fixture fixture = CreateFixture();
            MarkCardMetadataChanged(fixture);
            CharacterTestRuntimeAiLabPublishCurrentChangesResult preview = Prepare(fixture);
            fixture.FailWrite = true;

            CharacterTestRuntimeAiLabPublishCurrentChangesResult result =
                fixture.Authoring.CommitPublishCurrentChanges(
                    preview.Preview.Token,
                    fixture.Workspace.WorkspaceId,
                    fixture.Workspace.CurrentRevision,
                    fixture.Group,
                    fixture.Workspace.Cards,
                    fixture.References);

            Assert.That(result.Status,
                Is.EqualTo(CharacterTestRuntimeAiLabPublishCurrentChangesStatus.CommitFailed),
                result.Message);
            Assert.That(result.CatalogChanged, Is.False);
            Assert.That(result.RequiresRecovery, Is.False);
            Assert.That(fixture.ReloadCount, Is.EqualTo(1),
                "failed commit did not verify the rollback identity");
            Assert.That(fixture.Authoring.State, Is.SameAs(fixture.SourceState));
        }

        [Test]
        public void PublishCurrentChanges_DraftCheckpointCommitsBothSources()
        {
            Fixture fixture = CreateFixture();
            MarkCardMetadataChanged(fixture);
            CharacterTestRuntimeAiLabPublishCurrentChangesResult committed = Commit(fixture);
            int previousWorkspaceRevision = fixture.Workspace.CurrentRevision;

            Assert.That(fixture.Workspace.TryStagePublishCurrentChangesCheckpoint(
                committed,
                committed.State.GroupFileHash,
                out CharacterTestRuntimeAiLabDraftWorkspace.PublishCurrentChangesCheckpoint checkpoint,
                out string message), Is.True, message);
            checkpoint.Commit();

            Assert.That(fixture.Card.SourcePublishedRevision,
                Is.EqualTo(committed.PublishedCards[0].Card.Revision));
            Assert.That(fixture.Card.SourcePublishedContentHash,
                Is.EqualTo(committed.PublishedCards[0].Card.ContentHash));
            Assert.That(fixture.Group.SourceProjectRevision, Is.EqualTo(committed.Group.Revision));
            Assert.That(fixture.Group.SourceProjectContentHash, Is.EqualTo(committed.Group.ContentHash));
            Assert.That(fixture.Workspace.CurrentRevision, Is.EqualTo(previousWorkspaceRevision + 1));
        }

        [Test]
        public void PublishCurrentChanges_DraftCheckpointRollbackRestoresSources()
        {
            Fixture fixture = CreateFixture();
            MarkCardMetadataChanged(fixture);
            int sourceCardRevision = fixture.Card.SourcePublishedRevision;
            long sourceCardHash = fixture.Card.SourcePublishedContentHash;
            int sourceGroupRevision = fixture.Group.SourceProjectRevision;
            long sourceGroupHash = fixture.Group.SourceProjectContentHash;
            int workspaceRevision = fixture.Workspace.CurrentRevision;
            CharacterTestRuntimeAiLabPublishCurrentChangesResult committed = Commit(fixture);

            Assert.That(fixture.Workspace.TryStagePublishCurrentChangesCheckpoint(
                committed,
                committed.State.GroupFileHash,
                out CharacterTestRuntimeAiLabDraftWorkspace.PublishCurrentChangesCheckpoint checkpoint,
                out string stageMessage), Is.True, stageMessage);
            Assert.That(checkpoint.TryRollback(out string rollbackMessage), Is.True, rollbackMessage);
            Assert.That(fixture.Workspace.Cards.TryGetValue(
                fixture.Card.CardDraftId,
                out CharacterTestRuntimeAiLabBehaviorCardDraft restoredCard), Is.True);
            Assert.That(fixture.Workspace.Groups.TryGetValue(
                fixture.Group.GroupDraftId,
                out CharacterTestRuntimeAiLabBehaviorGroupDraft restoredGroup), Is.True);

            Assert.That(restoredCard.SourcePublishedRevision, Is.EqualTo(sourceCardRevision));
            Assert.That(restoredCard.SourcePublishedContentHash, Is.EqualTo(sourceCardHash));
            Assert.That(restoredGroup.SourceProjectRevision, Is.EqualTo(sourceGroupRevision));
            Assert.That(restoredGroup.SourceProjectContentHash, Is.EqualTo(sourceGroupHash));
            Assert.That(fixture.Workspace.CurrentRevision, Is.EqualTo(workspaceRevision));
        }

        [Test]
        public void PublishPopupPreview_UsesBuildSummaryAndTypedTokenPayloads()
        {
            Fixture fixture = CreateFixture();
            MarkCardMetadataChanged(fixture);
            CharacterTestRuntimeAiLabPublishCurrentChangesResult preview = Prepare(fixture);

            CharacterTestRuntimeAiLabPublishCurrentChangesPopupViewModel viewModel =
                CharacterTestRuntimeAiLabPublishCurrentChangesPopupViewModel.FromResult(preview);

            Assert.That(viewModel.State,
                Is.EqualTo(CharacterTestRuntimeAiLabPublishCurrentChangesPopupState.Preview));
            Assert.That(viewModel.Summary, Is.EqualTo(preview.Preview.BuildSummary()));
            Assert.That(viewModel.Primary.Payload.Intent,
                Is.EqualTo(CharacterTestRuntimeAiLabPublishCurrentChangesCommandIntent.Confirm));
            Assert.That(viewModel.Secondary.Payload.Intent,
                Is.EqualTo(CharacterTestRuntimeAiLabPublishCurrentChangesCommandIntent.Cancel));
            Assert.That(viewModel.Primary.Payload.PreviewToken, Is.EqualTo(preview.Preview.Token));
            Assert.That(viewModel.Secondary.Payload.PreviewToken, Is.EqualTo(preview.Preview.Token));
        }

        [Test]
        public void PublishPopupFailure_KeepsTechnicalDiagnosticOutOfPrimaryCopy()
        {
            CharacterTestRuntimeAiLabPublishCurrentChangesResult failure =
                CharacterTestRuntimeAiLabPublishCurrentChangesResult.Fail(
                    CharacterTestRuntimeAiLabPublishCurrentChangesStatus.CardPreflightFailed,
                    CharacterTestRuntimeAiLabPublishCurrentChangesStage.CardPreflight,
                    "CARD_FIXTURE_INVALID",
                    "fixture/path/card.json hash=123 failed validation");

            CharacterTestRuntimeAiLabPublishCurrentChangesPopupViewModel viewModel =
                CharacterTestRuntimeAiLabPublishCurrentChangesPopupViewModel.FromResult(failure);

            Assert.That(viewModel.Message, Does.Contain("草稿仍然安全保留"));
            Assert.That(viewModel.Message, Does.Contain("游戏可用版本没有改变"));
            Assert.That(viewModel.Message, Does.Not.Contain("fixture/path"));
            Assert.That(viewModel.Details, Does.Contain("CardPreflight"));
            Assert.That(viewModel.Details, Does.Contain("CARD_FIXTURE_INVALID"));
            Assert.That(viewModel.Details, Does.Contain("fixture/path/card.json"));
            Assert.That(viewModel.Primary.Payload.Intent,
                Is.EqualTo(CharacterTestRuntimeAiLabPublishCurrentChangesCommandIntent.Retry));
        }

        [Test]
        public void PublishPopupSuccess_OffersTypedRestartAndContinueChoices()
        {
            Fixture fixture = CreateFixture();
            MarkCardMetadataChanged(fixture);
            CharacterTestRuntimeAiLabPublishCurrentChangesResult success =
                Commit(fixture).CompleteAfterDraftCheckpoint("发布完成。");

            CharacterTestRuntimeAiLabPublishCurrentChangesPopupViewModel viewModel =
                CharacterTestRuntimeAiLabPublishCurrentChangesPopupViewModel.FromResult(success);

            Assert.That(viewModel.State,
                Is.EqualTo(CharacterTestRuntimeAiLabPublishCurrentChangesPopupState.SuccessChoice));
            Assert.That(viewModel.Primary.Payload.Intent,
                Is.EqualTo(CharacterTestRuntimeAiLabPublishCurrentChangesCommandIntent
                    .RestartPublishedVersion));
            Assert.That(viewModel.Secondary.Payload.Intent,
                Is.EqualTo(CharacterTestRuntimeAiLabPublishCurrentChangesCommandIntent
                    .ContinueCurrentTest));
            Assert.That(viewModel.ClosePayload.Intent,
                Is.EqualTo(CharacterTestRuntimeAiLabPublishCurrentChangesCommandIntent
                    .ContinueCurrentTest));
            Assert.That(viewModel.Primary.Payload.PreviewToken, Is.EqualTo(success.Preview.Token));
        }

        [Test]
        public void PostPublishContinue_PreservesIdentityAndNeverInvokesRestart()
        {
            Fixture fixture = CreateFixture();
            MarkCardMetadataChanged(fixture);
            CharacterTestRuntimeAiLabPublishCurrentChangesResult success =
                Commit(fixture).CompleteAfterDraftCheckpoint("发布完成。");
            var coordinator =
                new CharacterTestRuntimeAiLabPublishCurrentChangesPostTestCoordinator();
            int restartCalls = 0;

            CharacterTestRuntimeAiLabPublishCurrentChangesPostTestResult result =
                coordinator.Choose(
                    success,
                    success.Preview.Token,
                    CharacterTestRuntimeAiLabPublishCurrentChangesPostTestChoice.ContinueCurrentTest,
                    () => "runtime-session-identity",
                    () =>
                    {
                        restartCalls++;
                        return CharacterTestRuntimeAiLabPublishCurrentChangesPostTestResult.Succeeded(
                            CharacterTestRuntimeAiLabPublishCurrentChangesPostTestChoice
                                .RestartPublishedVersion,
                            "unexpected",
                            runtimeIdentityPreserved: false);
                    });

            Assert.That(result.Success, Is.True, result.Message);
            Assert.That(result.RuntimeIdentityPreserved, Is.True);
            Assert.That(restartCalls, Is.Zero);
            CharacterTestRuntimeAiLabPublishCurrentChangesPostTestResult duplicate =
                coordinator.Choose(
                    success,
                    success.Preview.Token,
                    CharacterTestRuntimeAiLabPublishCurrentChangesPostTestChoice.ContinueCurrentTest,
                    () => "runtime-session-identity",
                    null);
            Assert.That(duplicate.Success, Is.True);
            Assert.That(duplicate.Duplicate, Is.True);
        }

        [Test]
        public void PostPublishRestart_DelegatesOnceAndRejectsASecondChoice()
        {
            Fixture fixture = CreateFixture();
            MarkCardMetadataChanged(fixture);
            CharacterTestRuntimeAiLabPublishCurrentChangesResult success =
                Commit(fixture).CompleteAfterDraftCheckpoint("发布完成。");
            var coordinator =
                new CharacterTestRuntimeAiLabPublishCurrentChangesPostTestCoordinator();
            int restartCalls = 0;
            CharacterTestRuntimeAiLabPublishCurrentChangesPostTestResult Restart()
            {
                restartCalls++;
                return CharacterTestRuntimeAiLabPublishCurrentChangesPostTestResult.Succeeded(
                    CharacterTestRuntimeAiLabPublishCurrentChangesPostTestChoice
                        .RestartPublishedVersion,
                    "queued at F0",
                    runtimeIdentityPreserved: false);
            }

            CharacterTestRuntimeAiLabPublishCurrentChangesPostTestResult first =
                coordinator.Choose(
                    success,
                    success.Preview.Token,
                    CharacterTestRuntimeAiLabPublishCurrentChangesPostTestChoice
                        .RestartPublishedVersion,
                    () => "runtime-session-identity",
                    Restart);
            CharacterTestRuntimeAiLabPublishCurrentChangesPostTestResult duplicate =
                coordinator.Choose(
                    success,
                    success.Preview.Token,
                    CharacterTestRuntimeAiLabPublishCurrentChangesPostTestChoice
                        .RestartPublishedVersion,
                    () => "runtime-session-identity",
                    Restart);
            CharacterTestRuntimeAiLabPublishCurrentChangesPostTestResult conflicting =
                coordinator.Choose(
                    success,
                    success.Preview.Token,
                    CharacterTestRuntimeAiLabPublishCurrentChangesPostTestChoice
                        .ContinueCurrentTest,
                    () => "runtime-session-identity",
                    Restart);

            Assert.That(first.Success, Is.True, first.Message);
            Assert.That(duplicate.Success, Is.True);
            Assert.That(duplicate.Duplicate, Is.True);
            Assert.That(conflicting.Success, Is.False);
            Assert.That(restartCalls, Is.EqualTo(1));
        }

        [Test]
        public void PublishPopupBinder_UsesPopupServiceAndEnqueuesTypedConfirm()
        {
            Fixture fixture = CreateFixture();
            MarkCardMetadataChanged(fixture);
            CharacterTestRuntimeAiLabPublishCurrentChangesResult preview = Prepare(fixture);
            var coordinator = new CharacterTestRuntimeAiLabPublishCurrentChangesPopupCoordinator();
            var commandSink = new RecordingCommandSink();
            var popupService = new RecordingPopupService();
            var popup = CreatePublishPopup();
            var anchor = new Fgui.GButton { name = "btnPublishCurrentChanges" };
            var binder =
                new CharacterTestFairyGuiRuntimeAiLabPublishCurrentChangesPopupBinder(
                    commandSink,
                    new MxUiViewId("ui.test.publish.popup"),
                    popupService,
                    coordinator);

            Assert.That(binder.TryAttach(popup, anchor, out string diagnostic),
                Is.True, diagnostic);
            coordinator.Present(preview);

            Assert.That(popupService.ShowCount, Is.EqualTo(1));
            Assert.That(popupService.LastPopup, Is.SameAs(popup));
            popup.GetChild(
                CharacterTestFairyGuiRuntimeAiLabPublishCurrentChangesPopupBinder
                    .PrimaryButtonControlName).asButton.onClick.Call();
            Assert.That(commandSink.Commands.Count, Is.EqualTo(1));
            Assert.That(commandSink.Commands[0].Payload,
                Is.TypeOf<CharacterTestRuntimeAiLabPublishCurrentChangesCommandPayload>());
            var payload = (CharacterTestRuntimeAiLabPublishCurrentChangesCommandPayload)
                commandSink.Commands[0].Payload;
            Assert.That(payload.Intent,
                Is.EqualTo(CharacterTestRuntimeAiLabPublishCurrentChangesCommandIntent.Confirm));
            Assert.That(payload.PreviewToken, Is.EqualTo(preview.Preview.Token));

            popupService.CloseOutside();
            Assert.That(commandSink.Commands.Count, Is.EqualTo(2));
            var outsidePayload = (CharacterTestRuntimeAiLabPublishCurrentChangesCommandPayload)
                commandSink.Commands[1].Payload;
            Assert.That(outsidePayload.Intent,
                Is.EqualTo(CharacterTestRuntimeAiLabPublishCurrentChangesCommandIntent.Cancel));
            binder.Dispose();
        }

        private static CharacterTestRuntimeAiLabPublishCurrentChangesResult Prepare(Fixture fixture)
        {
            CharacterTestRuntimeAiLabPublishCurrentChangesResult result =
                fixture.Authoring.PreparePublishCurrentChanges(
                    fixture.Workspace.WorkspaceId,
                    fixture.Workspace.CurrentRevision,
                    fixture.Group,
                    fixture.Workspace.Cards,
                    fixture.References);
            Assert.That(result.PreviewReady, Is.True, result.Message);
            return result;
        }

        private static CharacterTestRuntimeAiLabPublishCurrentChangesResult Commit(Fixture fixture)
        {
            CharacterTestRuntimeAiLabPublishCurrentChangesResult preview = Prepare(fixture);
            CharacterTestRuntimeAiLabPublishCurrentChangesResult result =
                fixture.Authoring.CommitPublishCurrentChanges(
                    preview.Preview.Token,
                    fixture.Workspace.WorkspaceId,
                    fixture.Workspace.CurrentRevision,
                    fixture.Group,
                    fixture.Workspace.Cards,
                    fixture.References);
            Assert.That(result.CatalogCommitSucceeded, Is.True, result.Message);
            return result;
        }

        private static void MarkCardMetadataChanged(Fixture fixture)
        {
            fixture.Card.DisplayName += " 修改";
            fixture.Card.CurrentRevision++;
            fixture.Workspace.CurrentRevision++;
        }

        private static CharacterTestRuntimeAiPlannerBehaviorGroupCardReference FindPinnedCard(
            CharacterTestRuntimeAiPlannerBehaviorGroupRevision group,
            string stableCardId)
        {
            for (int i = 0; i < group.Cards.Count; i++)
            {
                if (string.Equals(group.Cards[i].StableCardId, stableCardId, StringComparison.Ordinal))
                    return group.Cards[i];
            }
            Assert.Fail("Published Group did not pin the changed Card revision.");
            return null;
        }

        private static Fgui.GComponent CreatePublishPopup()
        {
            var popup = new Fgui.GComponent
            {
                name = CharacterTestFairyGuiRuntimeAiLabPublishCurrentChangesPopupBinder
                    .PopupComponentName
            };
            popup.AddChild(new Fgui.GTextField
            {
                name = CharacterTestFairyGuiRuntimeAiLabPublishCurrentChangesPopupBinder
                    .TitleControlName
            });
            popup.AddChild(new Fgui.GTextField
            {
                name = CharacterTestFairyGuiRuntimeAiLabPublishCurrentChangesPopupBinder
                    .MessageControlName
            });
            popup.AddChild(new Fgui.GTextField
            {
                name = CharacterTestFairyGuiRuntimeAiLabPublishCurrentChangesPopupBinder
                    .SummaryControlName
            });
            popup.AddChild(new Fgui.GGraph
            {
                name = CharacterTestFairyGuiRuntimeAiLabPublishCurrentChangesPopupBinder
                    .DetailsPanelControlName
            });
            popup.AddChild(new Fgui.GTextField
            {
                name = CharacterTestFairyGuiRuntimeAiLabPublishCurrentChangesPopupBinder
                    .DetailsTextControlName
            });
            popup.AddChild(new Fgui.GButton
            {
                name = CharacterTestFairyGuiRuntimeAiLabPublishCurrentChangesPopupBinder
                    .PrimaryButtonControlName
            });
            popup.AddChild(new Fgui.GButton
            {
                name = CharacterTestFairyGuiRuntimeAiLabPublishCurrentChangesPopupBinder
                    .SecondaryButtonControlName
            });
            popup.AddChild(new Fgui.GButton
            {
                name = CharacterTestFairyGuiRuntimeAiLabPublishCurrentChangesPopupBinder
                    .DetailsButtonControlName
            });
            popup.AddChild(new Fgui.GButton
            {
                name = CharacterTestFairyGuiRuntimeAiLabPublishCurrentChangesPopupBinder
                    .CloseButtonControlName
            });
            return popup;
        }

        private static Fixture CreateFixture()
        {
            CharacterTestRuntimeAiPlannerProjectCatalogSeedResult seed =
                CharacterTestRuntimeAiPlannerProjectCatalogSeed.Create();
            Assert.That(seed.Success, Is.True, seed.Diagnostic);
            var sourceState = new CharacterTestRuntimeAiPlannerProjectCatalogState(
                seed.Cards,
                seed.Groups,
                "fixture/behavior_card_catalog.json",
                "fixture/behavior_group_catalog.json",
                "cards-source-hash",
                "groups-source-hash",
                string.Empty);
            CharacterTestArenaMatchConfig config =
                CharacterTestArenaMatchConfig.CreateProjectBacked(sourceState);
            // Keep legacy transaction cases on an actual legacy revision even when the local
            // catalog also contains tactical cards created by the end-to-end authoring run.
            var defaultGroup = config.BehaviorGroups.GetDefaultDraft();
            var legacyGroup = seed.Groups.Groups.Where(g => g.StableGroupId == defaultGroup.StableId
                && g.Cards.All(p => seed.Cards.TryGetExact(p.StableCardId, p.CardRevision, out var published) && published.TacticalSource == null))
                .OrderByDescending(g => g.Revision).FirstOrDefault();
            Assert.IsNotNull(legacyGroup, "The versioned catalog must retain a legacy transaction fixture revision.");
            Assert.IsTrue(config.BehaviorGroups.TryAdoptProjectRevision(legacyGroup));
            CharacterTestRuntimeAiLabDraftWorkspace workspace =
                CharacterTestRuntimeAiLabDraftWorkspace.Import(config);
            Assert.That(workspace.TryGetGroupForSlot(
                CharacterTestArenaMatchConfig.Team1Id,
                0,
                out CharacterTestRuntimeAiLabBehaviorGroupDraft group), Is.True);
            CharacterTestRuntimeAiLabBehaviorCardDraft card = null;
            for (int i = 0; i < group.Entries.Count; i++)
            {
                if (workspace.Cards.TryGetValue(
                        group.Entries[i].CardDraftId,
                        out CharacterTestRuntimeAiLabBehaviorCardDraft candidate)
                    && candidate.HasPublishedSource)
                {
                    card = candidate;
                    break;
                }
            }
            Assert.That(card, Is.Not.Null);
            var references = new CharacterTestRuntimeAiLabBehaviorCardReferenceCatalog(
                CharacterTestAbilityCatalogSource.CreateEmbeddedFallback(),
                CharacterTestCharacterActionConfigSource.Load(
                    CharacterTestCharacterActionConfigSourceOptions.CreateLegacyFixtureFallback())
                    .Registry);
            return new Fixture(sourceState, workspace, group, card, references);
        }

        private sealed class Fixture
        {
            private CharacterTestRuntimeAiPlannerProjectCatalogState _nextState;

            internal Fixture(
                CharacterTestRuntimeAiPlannerProjectCatalogState sourceState,
                CharacterTestRuntimeAiLabDraftWorkspace workspace,
                CharacterTestRuntimeAiLabBehaviorGroupDraft group,
                CharacterTestRuntimeAiLabBehaviorCardDraft card,
                CharacterTestRuntimeAiLabBehaviorCardReferenceCatalog references)
            {
                SourceState = sourceState;
                Workspace = workspace;
                Group = group;
                Card = card;
                References = references;
                Authoring = new CharacterTestRuntimeAiPlannerProjectCatalogAuthoringSession(
                    sourceState,
                    Reload,
                    Write);
            }

            internal CharacterTestRuntimeAiPlannerProjectCatalogState SourceState { get; }
            internal CharacterTestRuntimeAiLabDraftWorkspace Workspace { get; }
            internal CharacterTestRuntimeAiLabBehaviorGroupDraft Group { get; }
            internal CharacterTestRuntimeAiLabBehaviorCardDraft Card { get; }
            internal CharacterTestRuntimeAiLabBehaviorCardReferenceCatalog References { get; }
            internal CharacterTestRuntimeAiPlannerProjectCatalogAuthoringSession Authoring { get; }
            internal int WriteCount { get; private set; }
            internal int ReloadCount { get; private set; }
            internal bool FailWriteWithConflict { get; set; }
            internal bool FailWrite { get; set; }
            internal bool FailReload { get; set; }
            internal bool ReturnUnconfirmedWriteFailure { get; set; }

            internal CharacterTestRuntimeAiPlannerProjectCatalogState Reload()
            {
                ReloadCount++;
                if (FailReload) return new CharacterTestRuntimeAiPlannerProjectCatalogState(null, null,
                    SourceState.CardCatalogPath, SourceState.GroupCatalogPath, string.Empty, string.Empty, "Injected catalog reread failure.");
                return _nextState ?? SourceState;
            }

            internal CharacterTestRuntimeAiPlannerProjectCatalogWriteResult Write(
                CharacterTestRuntimeAiPlannerProjectCatalogWriteRequest request)
            {
                WriteCount++;
                if (FailWriteWithConflict)
                {
                    return new CharacterTestRuntimeAiPlannerProjectCatalogWriteResult(
                        false,
                        true,
                        "Injected source hash conflict.");
                }
                if (FailWrite)
                {
                    return new CharacterTestRuntimeAiPlannerProjectCatalogWriteResult(
                        false,
                        false,
                        "Injected transaction failure with successful rollback.");
                }
                CharacterTestRuntimeAiPlannerBehaviorGroupCatalogLoadResult groups =
                    CharacterTestRuntimeAiPlannerBehaviorGroupCatalogJson.Load(
                        request.GroupCatalogJson,
                        request.CardCatalog,
                        CharacterTestRuntimeAiPlannerBehaviorDefinitionCatalog.CreateBuiltIn());
                if (!groups.Success)
                {
                    return new CharacterTestRuntimeAiPlannerProjectCatalogWriteResult(
                        false,
                        false,
                        groups.Diagnostic);
                }
                _nextState = new CharacterTestRuntimeAiPlannerProjectCatalogState(
                    request.CardCatalog,
                    groups.Catalog,
                    request.CardCatalogPath,
                    request.GroupCatalogPath,
                    "cards-committed-hash-" + WriteCount,
                    "groups-committed-hash-" + WriteCount,
                    string.Empty);
                return new CharacterTestRuntimeAiPlannerProjectCatalogWriteResult(
                    !ReturnUnconfirmedWriteFailure,
                    false,
                    string.Empty);
            }
        }

        private sealed class RecordingCommandSink : IMxUiCommandSink
        {
            internal List<MxUiCommand> Commands { get; } = new List<MxUiCommand>();

            public void Enqueue(MxUiCommand command)
            {
                Commands.Add(command);
            }
        }

        private sealed class RecordingPopupService : IMxFairyGuiPopupService
        {
            private Action<MxFairyGuiPopupCloseReason> _onClosed;

            public bool HasOpenPopup { get; private set; }
            public Fgui.GObject TopPopup => HasOpenPopup ? LastPopup : null;
            internal int ShowCount { get; private set; }
            internal Fgui.GObject LastPopup { get; private set; }

            public bool Show(
                MxUiViewId ownerViewId,
                Fgui.GObject popup,
                Fgui.GObject anchor,
                MxFairyGuiPopupOptions options = default,
                Action<MxFairyGuiPopupCloseReason> onClosed = null)
            {
                ShowCount++;
                LastPopup = popup;
                HasOpenPopup = true;
                _onClosed = onClosed;
                return true;
            }

            public bool Toggle(
                MxUiViewId ownerViewId,
                Fgui.GObject popup,
                Fgui.GObject anchor,
                MxFairyGuiPopupOptions options = default,
                Action<MxFairyGuiPopupCloseReason> onClosed = null)
            {
                return Show(ownerViewId, popup, anchor, options, onClosed);
            }

            public bool Hide(
                Fgui.GObject popup,
                MxFairyGuiPopupCloseReason reason = MxFairyGuiPopupCloseReason.OwnerClosed)
            {
                if (!HasOpenPopup || !ReferenceEquals(LastPopup, popup))
                    return false;
                Close(reason);
                return true;
            }

            public bool HideTop(
                MxFairyGuiPopupCloseReason reason = MxFairyGuiPopupCloseReason.Cancel)
            {
                if (!HasOpenPopup)
                    return false;
                Close(reason);
                return true;
            }

            public void HideOwner(
                MxUiViewId ownerViewId,
                MxFairyGuiPopupCloseReason reason = MxFairyGuiPopupCloseReason.OwnerClosed)
            {
                if (HasOpenPopup)
                    Close(reason);
            }

            public void HideAll(
                MxFairyGuiPopupCloseReason reason = MxFairyGuiPopupCloseReason.OwnerClosed)
            {
                if (HasOpenPopup)
                    Close(reason);
            }

            public bool IsOpen(Fgui.GObject popup)
            {
                return HasOpenPopup && ReferenceEquals(LastPopup, popup);
            }

            public void CloseOutside()
            {
                if (HasOpenPopup)
                    Close(MxFairyGuiPopupCloseReason.OutsidePointer);
            }

            public void Dispose()
            {
                HideAll(MxFairyGuiPopupCloseReason.ServiceDisposed);
            }

            private void Close(MxFairyGuiPopupCloseReason reason)
            {
                HasOpenPopup = false;
                Action<MxFairyGuiPopupCloseReason> callback = _onClosed;
                _onClosed = null;
                callback?.Invoke(reason);
            }
        }
    }
}
