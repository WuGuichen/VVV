using System;
using System.IO;
using System.Linq;
using MxFramework.Tests.Game.CharacterTest;
using Newtonsoft.Json;
using NUnit.Framework;
using UnityEngine;

namespace MxFramework.Game.CharacterTest.Tests
{
    public sealed partial class CharacterTestRuntimeAiLabPublishCurrentChangesTests
    {
        [Test]
        public void TacticalLibrarySingleCardPublishCreatesFirstRevisionWithoutChangingBehaviorGroups()
        {
            var fixture = CreateFixture();
            using var runtime = new CharacterTestTacticalCardRuntimeFixture();
            var workspace = fixture.Workspace;
            Assert.IsTrue(workspace.TryImportComposition(
                1,
                0,
                workspace.CurrentRevision,
                CharacterTestTacticalDslTrialTemplates.Create(),
                out string cardId,
                out string issue), issue);
            CharacterTestRuntimeAiLabBehaviorCardDraft card = workspace.Cards[cardId];
            card.PublishedStableId = "bc_qkjc3ka7za";
            string stableCardId = card.PublishedStableId;
            string groupCatalogBefore = CharacterTestRuntimeAiPlannerBehaviorGroupCatalogJson.ToJson(
                fixture.SourceState.GroupCatalog);
            fixture.Authoring.CompileTacticalForPublish = draft =>
            {
                CharacterTestTacticalDslSource document = workspace.ResolveComposition(draft);
                Assert.IsTrue(CharacterTestTacticalDslCompiler.TryCompile(
                    document, runtime.Inputs, out var compiled, out string error), error);
                CharacterTestTacticalFrozenSource frozen = CharacterTestTacticalFrozenSource.Capture(
                    workspace, draft, document, runtime.Inputs, compiled);
                return new CharacterTestRuntimeAiLabBehaviorCardLogicCompileResult(
                    compiled.Program.WithTacticalSource(frozen),
                    Array.Empty<CharacterTestRuntimeAiLabCardDiagnostic>());
            };

            CharacterTestRuntimeAiLabPublishResult result =
                fixture.Authoring.TryPublishCard(card, fixture.References);

            Assert.IsTrue(result.Success, string.Join(";", result.Diagnostics.Select(d => d.Message)));
            Assert.AreEqual(stableCardId, result.PublishedCard.StableCardId);
            Assert.AreEqual(1, result.PublishedCard.Revision);
            Assert.IsNotNull(result.PublishedCard.TacticalSource);
            Assert.AreEqual(1, fixture.WriteCount);
            Assert.AreEqual(groupCatalogBefore,
                CharacterTestRuntimeAiPlannerBehaviorGroupCatalogJson.ToJson(fixture.Authoring.State.GroupCatalog),
                "Single-card publish must preserve the behavior-group catalog byte-for-byte.");
            Assert.IsTrue(workspace.TryAdoptStandaloneTacticalPublishedRevision(
                cardId, result.PublishedCard, out issue), issue);
            Assert.IsTrue(workspace.Cards[cardId].HasPublishedSource);
            Assert.AreEqual(1, workspace.Cards[cardId].SourcePublishedRevision);
        }

        [Test]
        public void TacticalFirstPublishPinsSharedSourceReloadsRecompilesAndReplaysOnce()
        {
            var fixture = CreateFixture();
            using (var runtime = new CharacterTestTacticalCardRuntimeFixture())
            {
                var workspace = fixture.Workspace;
                var authored = CharacterTestTacticalDslTrialTemplates.Create();
                authored.Root.Children[0].Children[0].EndCondition = new CharacterTestRuntimeAiLabBehaviorRecipeConditionNode {
                    NodeId = "published-commit-failure", Kind = CharacterTestRuntimeAiLabBehaviorRecipeConditionNodeKind.EventSignal,
                    SourceEventId = "attack", Signal = CharacterTestRuntimeAiLabBehaviorRecipeEventSignal.CommitFailed };
                Assert.IsTrue(workspace.TryImportComposition(1, 0, workspace.CurrentRevision,
                    authored, out string id, out string issue), issue);
                Assert.IsTrue(workspace.TryDuplicateCompositionCard(new CharacterTestTacticalEditToken(workspace, workspace.Cards[id]),
                    1, 0, out string copyId, out issue), issue);
                Assert.IsTrue(workspace.TryGetGroupForSlot(1, 0, out var group));
                var card = workspace.Cards[id]; string reserved = card.PublishedStableId;
                fixture.Authoring.CompileTacticalForPublish = draft => {
                    var document = workspace.ResolveComposition(draft);
                    Assert.IsTrue(CharacterTestTacticalDslCompiler.TryCompile(document, runtime.Inputs, out var compiled, out var error), error);
                    var frozen = CharacterTestTacticalFrozenSource.Capture(workspace, draft, document, runtime.Inputs, compiled);
                    return new CharacterTestRuntimeAiLabBehaviorCardLogicCompileResult(compiled.Program.WithTacticalSource(frozen),
                        Array.Empty<CharacterTestRuntimeAiLabCardDiagnostic>());
                };
                var preview = fixture.Authoring.PreparePublishCurrentChanges(workspace.WorkspaceId, workspace.CurrentRevision, group, workspace.Cards, fixture.References);
                Assert.IsTrue(preview.PreviewReady, preview.Message);
                var committed = fixture.Authoring.CommitPublishCurrentChanges(preview.Preview.Token, workspace.WorkspaceId,
                    workspace.CurrentRevision, group, workspace.Cards, fixture.References);
                Assert.IsTrue(committed.CatalogCommitSucceeded, committed.Message);
                var published = committed.PublishedCards.Single(p => p.CardDraftId == id).Card;
                Assert.AreEqual(reserved, published.StableCardId); Assert.AreEqual(1, published.Revision);
                Assert.IsNull(published.CloneLogic()); Assert.IsNotNull(published.TacticalSource);
                Assert.Greater(published.TacticalSource.CloneDefinitions().Length, 0);
                string pinnedSource = published.TacticalSource.ToJson().ToString(Formatting.None);
                string json = CharacterTestRuntimeAiPlannerPublishedBehaviorCardCatalogJson.ToJson(committed.State.CardCatalog);
                var loaded = CharacterTestRuntimeAiPlannerPublishedBehaviorCardCatalogJson.Load(json, fixture.References);
                Assert.IsTrue(loaded.Success, string.Join(";", loaded.Diagnostics.Select(d => d.Message)));
                Assert.IsTrue(loaded.Catalog.TryGetExact(reserved, 1, out var reloaded));
                Assert.AreEqual(published.SemanticHash, reloaded.SemanticHash);
                Assert.Greater(reloaded.NumericId, 0, "Tactical source restore must preserve the authored integer identity.");
                Assert.AreEqual(published.NumericId, reloaded.NumericId);
                Assert.AreEqual(pinnedSource, reloaded.TacticalSource.ToJson().ToString(Formatting.None));
                Assert.AreEqual(CharacterTestRuntimeAiLabBehaviorRecipeEventSignal.CommitFailed,
                    reloaded.TacticalSource.CloneComposition().Root.Children[0].Children[0].EndCondition.Signal);
                Assert.IsTrue(reloaded.TacticalSource.TryCompile(reserved, reloaded.Metadata.DisplayName, 1, runtime.Inputs,
                    out var bound, out issue), issue);
                Assert.IsNotNull(bound.TacticalSource);
                Assert.IsFalse(reloaded.TacticalSource.TryCompile(reserved, reloaded.Metadata.DisplayName, 1, null, out _, out issue));
                Assert.IsTrue(workspace.TryStagePublishCurrentChangesCheckpoint(committed, committed.State.GroupFileHash, out var checkpoint, out issue), issue);
                checkpoint.Commit();
                Assert.IsTrue(workspace.Cards[id].HasPublishedSource);
                Assert.IsTrue(workspace.Cards[id].HasCompositionSource);
                Assert.AreEqual(reserved, workspace.Cards[id].PublishedStableId);
                var compare = new CharacterTestRuntimeAiPlannerProjectCatalogAuthoringSession(committed.State);
                compare.ResolveTacticalSource = draft => CharacterTestTacticalPublishedSource.CaptureDraft(workspace, draft);
                Assert.IsTrue(compare.TryHasUnpublishedCardChanges(workspace.Cards[id], fixture.References, out bool changed, out issue), issue);
                Assert.IsFalse(changed, "Reopened source comparison must not depend on current metrics or an optional compiler callback.");
                var reopened = CharacterTestRuntimeAiLabDraftWorkspace.Import(CharacterTestArenaMatchConfig.CreateProjectBacked(committed.State));
                Assert.IsTrue(reopened.TryGetProjectCardDraft(reserved, out var reopenedCard));
                Assert.IsTrue(CharacterTestTacticalDslCompiler.TryCompile(reopened.ResolveComposition(reopenedCard), runtime.Inputs, out _, out issue), issue);
                string persistenceRoot = Path.GetFullPath(Path.Combine(Application.dataPath, "../Library/Task04ExecutionEvidence/M3/reopen-" + Guid.NewGuid().ToString("N")));
                var persistence = new CharacterTestRuntimeAiLabDraftPersistence(persistenceRoot);
                persistence.AdoptWorkspace(reopened); persistence.MarkDirty(); Assert.IsTrue(persistence.Flush(), persistence.LastStatus);
                var reader = new CharacterTestRuntimeAiLabDraftPersistence(persistenceRoot);
                Assert.IsTrue(reader.TryLoad(reopened.WorkspaceId, out var diskWorkspace, out issue), issue);
                Assert.IsTrue(diskWorkspace.TryGetProjectCardDraft(reserved, out var diskCard));
                Assert.IsTrue(CharacterTestTacticalDslCompiler.TryCompile(diskWorkspace.ResolveComposition(diskCard), runtime.Inputs, out _, out issue), issue);
                string copied = workspace.Cards[copyId].PublishedStableId;
                foreach (var ids in new[] { new[] { reserved, copied }, new[] { copied, reserved } })
                {
                    var build = CharacterTestRuntimeAiPlannerInstalledDeckRules.ValidateAndCreate(
                        new CharacterTestRuntimeAiPlannerInstalledDeckCandidate("task04.actor", ids, 1), loaded.Catalog,
                        CharacterTestRuntimeAiPlannerPublishedBehaviorCardPublisher.CurrentRuntimeContractVersion);
                    Assert.IsTrue(build.Success, string.Join(";", build.Diagnostics.Select(d => d.Message)));
                    var removed = CharacterTestRuntimeAiPlannerInstalledDeckRules.TryRemove(build.Deck, ids[0], loaded.Catalog);
                    Assert.IsTrue(removed.Success);
                    var installed = CharacterTestRuntimeAiPlannerInstalledDeckRules.TryInstall(removed.Deck, ids[0], loaded.Catalog);
                    Assert.IsTrue(installed.Success);
                    foreach (var deck in new[] { build.Deck, installed.Deck })
                    {
                        Assert.IsTrue(CharacterTestRuntimeAiPlannerPlayerDeckRuntimeCompiler.TryCompile(
                            CharacterTestRuntimeAiPlannerSessionDeckLock.FromInstalledDeck(deck), loaded.Catalog, out var profile,
                            out var programs, out issue, runtime.Inputs), issue);
                        var expectedOrder = ids.OrderBy(s => s, StringComparer.Ordinal).ToArray();
                        CollectionAssert.AreEqual(expectedOrder, programs.Select(p => p.SelectionDefinition.DefinitionId).ToArray());
                        CollectionAssert.AreEqual(expectedOrder, profile.Entries.Select(e => e.EntryId).ToArray());
                    }
                }
                var definition = workspace.CloneTacticalDefinitions().First(d => d.DefinitionId == "shared.attack");
                definition.Source.Timelines[0].Events.First(e => e.Operation.Kind == "ApproachOptimalRange").End.OffsetFrames += 4;
                Assert.IsTrue(workspace.TryReplaceTacticalDefinition(workspace.CaptureTacticalDefinitionEdit(definition.DefinitionId), definition.Source, true, out issue), issue);
                Assert.AreEqual(pinnedSource, reloaded.TacticalSource.ToJson().ToString(Formatting.None), "Draft edits must not float a published dependency.");
                var replay = fixture.Authoring.CommitPublishCurrentChanges(preview.Preview.Token, workspace.WorkspaceId,
                    workspace.CurrentRevision, group, workspace.Cards, fixture.References);
                Assert.IsTrue(replay.DuplicateConfirmation); Assert.AreEqual(1, fixture.WriteCount);
                string folder = Path.GetFullPath(Path.Combine(Application.dataPath, "../Library/Task04ExecutionEvidence/M3"));
                Directory.CreateDirectory(folder);
                File.WriteAllText(Path.Combine(folder, "published-source-roundtrip.json"), JsonConvert.SerializeObject(new {
                    reserved, revision = published.Revision, sourceHash = published.SemanticHash.ToString("X16"),
                    contentHash = published.ContentHash.ToString("X16"), boundHash = bound.SemanticHash.ToString("X16"),
                    sharedDefinitions = reloaded.TacticalSource.CloneDefinitions().Select(d => new { d.DefinitionId, d.Revision }),
                    fixture.WriteCount, frozenDependencyPreserved = true, absentInputsBlocked = true }, Formatting.Indented));
            }
        }
    }
}
