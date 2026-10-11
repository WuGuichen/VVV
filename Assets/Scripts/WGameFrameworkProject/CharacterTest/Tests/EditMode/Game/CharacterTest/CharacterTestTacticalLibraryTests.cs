using System;
using System.IO;
using System.Linq;
using MxFramework.Game.CharacterTest;
using NUnit.Framework;

namespace MxFramework.Tests.Game.CharacterTest
{
    public sealed class CharacterTestTacticalLibraryTests
    {
        [Test]
        public void FormalCatalogAndDraftsMergeWithoutLosingIndependentCopiesOrCreatingRoleReferences()
        {
            using var fixture = new CharacterTestTacticalCardRuntimeFixture();
            var author = CharacterTestRuntimeAiLabDraftWorkspace.Import(CharacterTestArenaMatchConfig.CreateDefault());
            Assert.IsTrue(author.TryImportComposition(1, 0, author.CurrentRevision, CharacterTestTacticalDslTrialTemplates.Create(), out var id, out var issue), issue);
            var document = author.ResolveComposition(author.Cards[id]);
            Assert.IsTrue(CharacterTestTacticalDslCompiler.TryCompile(document, fixture.Inputs, out var compiled, out issue), issue);
            var frozen = CharacterTestTacticalFrozenSource.Capture(author, author.Cards[id], document, fixture.Inputs, compiled);
            var result = new CharacterTestRuntimeAiLabBehaviorCardLogicCompileResult(compiled.Program.WithTacticalSource(frozen), Array.Empty<CharacterTestRuntimeAiLabCardDiagnostic>());
            var references = new CharacterTestRuntimeAiLabBehaviorCardReferenceCatalog(null, fixture.Runtime.ActionRuntime.Registry);
            int version = CharacterTestRuntimeAiPlannerPublishedBehaviorCardPublisher.CurrentRuntimeContractVersion;
            CharacterTestRuntimeAiPlannerPublishedBehaviorCard Publish(string stableId, int revision, int minimum = 0)
            {
                var metadata = new CharacterTestRuntimeAiPlannerPublishedBehaviorCardMetadata(stableId, "test.icon", 0, "卡说明", "", "");
                int numericId = stableId == "bc_qkjc3ka7za"
                    ? CharacterTestAuthoredContentIds.D417_Id3
                    : CharacterTestAuthoredContentIds.D417_Id4;
                var published = CharacterTestRuntimeAiPlannerPublishedBehaviorCardPublisher.ValidateAndCreate(author.Cards[id],
                    new CharacterTestRuntimeAiPlannerPublishedBehaviorCardPublishRequest(stableId, revision,
                        minimum == 0 ? version : minimum, minimum == 0 ? version : minimum, metadata, numericId), references, result);
                Assert.IsTrue(published.Success, string.Join(";", published.Diagnostics.Select(d => d.Message)));
                return published.Card;
            }
            const string CardA = "bc_qkjc3ka7za";
            const string CardB = "tactical.bound-action";
            var r1 = Publish(CardA, 1); var r2 = Publish(CardA, 2); var future = Publish(CardA, 3, version + 1);
            var b = Publish(CardB, 1);
            var catalogResult = CharacterTestRuntimeAiPlannerPublishedBehaviorCardCatalog.ValidateAndCreate("library-test", 1, new[] { r1, r2, future, b }, references);
            Assert.IsTrue(catalogResult.Success, string.Join(";", catalogResult.Diagnostics.Select(d => d.Message)));
            string path = Path.GetFullPath(Path.Combine(UnityEngine.Application.dataPath, "../Library/TacticalLibraryTests", Guid.NewGuid().ToString("N")));
            var persistence = new CharacterTestRuntimeAiLabDraftPersistence(path);
            var workspace = persistence.CreateFromCurrent(CharacterTestArenaMatchConfig.CreateDefault());
            string slots = Newtonsoft.Json.JsonConvert.SerializeObject(workspace.Slots);
            int revisionBefore = workspace.CurrentRevision, groupCount = workspace.Groups.Count;
            var initial = CharacterTestRuntimeAiPlannerDevSceneController.BuildTacticalLibraryMetadata(workspace, catalogResult.Catalog);
            Assert.AreEqual(2, initial.Length); StringAssert.Contains("r2", initial.Single(x => x.Key == "published:" + CardA).Revision);
            Assert.AreEqual(revisionBefore, workspace.CurrentRevision);
            Assert.IsTrue(workspace.TryOpenStandaloneTacticalCard(r2, out var draft, out issue), issue);
            Assert.AreEqual(2, draft.SourcePublishedRevision);
            Assert.IsTrue(workspace.TryOpenStandaloneTacticalCard(r2, out var same, out issue), issue); Assert.AreSame(draft, same);
            var second = CharacterTestRuntimeAiLabBehaviorCardDraft.CreatePublishedCopy(r1); workspace.AddLoadedCard(second);
            var merged = CharacterTestRuntimeAiPlannerDevSceneController.BuildTacticalLibraryMetadata(workspace, catalogResult.Catalog);
            Assert.AreEqual(3, merged.Length); Assert.AreEqual(2, merged.Count(x => x.Draft));
            Assert.IsFalse(merged.Any(x => x.Key == "published:" + CardA));
            Assert.AreEqual(groupCount, workspace.Groups.Count);
            Assert.AreEqual(slots, Newtonsoft.Json.JsonConvert.SerializeObject(workspace.Slots));
            Assert.IsFalse(workspace.Groups.Values.Any(g => g.Entries.Any(e => e.CardDraftId == draft.CardDraftId)));
            persistence.MarkDirty(); Assert.IsTrue(persistence.Flush(), persistence.LastStatus);
            Assert.IsTrue(new CharacterTestRuntimeAiLabDraftPersistence(path).TryLoad(workspace.WorkspaceId, out var loaded, out issue), issue);
            Assert.AreEqual(draft.CompositionSourceJson, loaded.Cards[draft.CardDraftId].CompositionSourceJson);
            Assert.AreEqual(slots, Newtonsoft.Json.JsonConvert.SerializeObject(loaded.Slots));

            int beforeDelete = workspace.CurrentRevision;
            workspace.IsReadOnly = true;
            Assert.IsFalse(workspace.TryDeleteUnreferencedTacticalDraft(draft.CardDraftId, out issue));
            workspace.IsReadOnly = false;
            Assert.AreEqual(beforeDelete, workspace.CurrentRevision);
            var referencedWorkspace = CharacterTestRuntimeAiLabDraftWorkspace.Import(CharacterTestArenaMatchConfig.CreateDefault());
            referencedWorkspace.AddLoadedCard(draft.Clone(false));
            var referenceGroup = new CharacterTestRuntimeAiLabBehaviorGroupDraft(Guid.NewGuid().ToString(), "", "引用测试组", null);
            referenceGroup.Entries.Add(new CharacterTestRuntimeAiLabBehaviorEntryDraft(Guid.NewGuid().ToString(),
                draft.CardDraftId, "test", 0, false, null));
            referencedWorkspace.AddLoadedGroup(referenceGroup);
            Assert.IsFalse(referencedWorkspace.TryDeleteUnreferencedTacticalDraft(draft.CardDraftId, out issue));
            StringAssert.Contains("行为组", issue);
            Assert.AreEqual(beforeDelete, workspace.CurrentRevision);

            Assert.IsTrue(workspace.TryDeleteUnreferencedTacticalDraft(draft.CardDraftId, out issue), issue);
            Assert.IsFalse(workspace.Cards.ContainsKey(draft.CardDraftId));
            Assert.AreEqual(slots, Newtonsoft.Json.JsonConvert.SerializeObject(workspace.Slots));
            persistence.MarkDirty(); Assert.IsTrue(persistence.Flush(), persistence.LastStatus);
            Assert.IsTrue(new CharacterTestRuntimeAiLabDraftPersistence(path).TryLoad(workspace.WorkspaceId, out loaded, out issue), issue);
            Assert.IsFalse(loaded.Cards.ContainsKey(draft.CardDraftId));
            Assert.IsTrue(workspace.TryUndo(out issue), issue);
            Assert.IsTrue(workspace.Cards.ContainsKey(draft.CardDraftId));

            var assemblyGroup = new CharacterTestRuntimeAiLabBehaviorGroupDraft(Guid.NewGuid().ToString(), "", "装配验证组", null);
            workspace.AddLoadedGroup(assemblyGroup);
            Assert.IsTrue(workspace.TrySelectGroupForSlot(1, 0, assemblyGroup.GroupDraftId, 0, out issue), issue);
            Assert.IsTrue(workspace.TrySelectGroupForSlot(2, 0, assemblyGroup.GroupDraftId, 0, out issue), issue);
            int cardCountBeforeAssembly = workspace.Cards.Count;
            string sourceBeforeAssembly = workspace.Cards[draft.CardDraftId].CompositionSourceJson;
            Assert.IsTrue(workspace.TryAddExistingCardEntry(1, 0, draft.CardDraftId, out int addedIndex, out issue), issue);
            Assert.IsTrue(workspace.TryGetGroupForSlot(1, 0, out var assembled));
            Assert.IsTrue(workspace.TryGetGroupForSlot(2, 0, out var otherRoleGroup));
            Assert.AreNotEqual(assembled.GroupDraftId, otherRoleGroup.GroupDraftId);
            Assert.AreEqual(0, otherRoleGroup.Entries.Count);
            Assert.AreEqual(draft.CardDraftId, assembled.Entries[addedIndex].CardDraftId);
            Assert.AreEqual(cardCountBeforeAssembly, workspace.Cards.Count);
            Assert.AreEqual(sourceBeforeAssembly, workspace.Cards[draft.CardDraftId].CompositionSourceJson);
            persistence.MarkDirty(); Assert.IsTrue(persistence.Flush(), persistence.LastStatus);
            Assert.IsTrue(new CharacterTestRuntimeAiLabDraftPersistence(path).TryLoad(workspace.WorkspaceId, out loaded, out issue), issue);
            Assert.IsTrue(loaded.TryGetGroupForSlot(1, 0, out var reopenedGroup));
            Assert.AreEqual(draft.CardDraftId, reopenedGroup.Entries[addedIndex].CardDraftId);
            Assert.IsTrue(workspace.TryUndo(out issue), issue);
            Assert.IsTrue(workspace.TryAddExistingCardEntry(1, 0, draft.CardDraftId, out addedIndex, out issue), issue);
        }
    }
}
