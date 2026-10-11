using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using MxFramework.Game.CharacterTest;
using NUnit.Framework;
using UnityEngine;

namespace MxFramework.Tests.Game.CharacterTest
{
    public sealed class CharacterTestTacticalCompositionEditingTests
    {
        [TestCase(false)]
        [TestCase(true)]
        public void StandaloneLibraryContextDoesNotChangeRoleGroupEvenForUnreferencedCards(bool unreferenced)
        {
            var workspace = CharacterTestRuntimeAiLabDraftWorkspace.Import(CharacterTestArenaMatchConfig.CreateDefault());
            Assert.IsTrue(workspace.TryCreateLocalGroupCopyForSlot(1, 0, out _, out string issue), issue);
            Assert.IsTrue(workspace.TryImportComposition(1, 0, workspace.CurrentRevision,
                CharacterTestTacticalDslTrialTemplates.Create(), out string id, out issue), issue);
            Assert.IsTrue(workspace.TryGetGroupForSlot(1, 0, out var owner));
            Assert.IsTrue(workspace.TryGetGroupForSlot(2, 0, out var original));
            Assert.AreNotEqual(owner.GroupDraftId, original.GroupDraftId);
            if (unreferenced)
            {
                string entryId = owner.Entries.First(e => e.CardDraftId == id).EntryId;
                owner.Entries.RemoveAll(e => e.CardDraftId == id);
                owner.Profile.Entries.RemoveAll(e => e.EntryId == entryId);
            }
            string slots = Newtonsoft.Json.JsonConvert.SerializeObject(workspace.Slots);
            int revision = workspace.CurrentRevision;
            string source = workspace.Cards[id].CompositionSourceJson;
            var go = new GameObject("Standalone tactical context"); go.SetActive(false);
            try
            {
                var controller = go.AddComponent<CharacterTestRuntimeAiPlannerDevSceneController>();
                var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
                typeof(CharacterTestRuntimeAiPlannerDevSceneController).GetField("_draftWorkspace", flags).SetValue(controller, workspace);
                typeof(CharacterTestRuntimeAiPlannerDevSceneController).GetField("_selectedSetupTeamId", flags).SetValue(controller, 2);
                Assert.IsTrue(controller.BeginStandaloneTacticalEditing("draft:" + id, out issue), issue);
                Assert.IsTrue(controller.TryGetSelectedAuthoringGroup(out var presentation));
                Assert.AreEqual(id, presentation.Entries.Single().CardDraftId);
                Assert.IsFalse(workspace.Groups.ContainsKey(presentation.GroupDraftId));
                Assert.IsTrue(workspace.TryGetGroupForSlot(2, 0, out var selected));
                Assert.AreEqual(original.GroupDraftId, selected.GroupDraftId);
                Assert.AreEqual(slots, Newtonsoft.Json.JsonConvert.SerializeObject(workspace.Slots));
                Assert.AreEqual(revision, workspace.CurrentRevision);
                Assert.IsFalse(controller.BeginStandaloneTacticalEditing("missing", out _));
                controller.EndStandaloneTacticalEditing();
            }
            finally { UnityEngine.Object.DestroyImmediate(go); }
            Assert.IsTrue(workspace.TryGetGroupForSlot(2, 0, out var restored));
            Assert.AreEqual(original.GroupDraftId, restored.GroupDraftId);
            Assert.AreEqual(source, workspace.Cards[id].CompositionSourceJson);
        }

        [Test]
        public void CardRenameIsOneUndoableCardOperationAndDoesNotRenameSharedContent()
        {
            var workspace = CharacterTestRuntimeAiLabDraftWorkspace.Import(CharacterTestArenaMatchConfig.CreateDefault());
            Assert.IsTrue(workspace.TryImportComposition(1, 0, workspace.CurrentRevision, CharacterTestTacticalDslTrialTemplates.Create(), out string id, out string issue), issue);
            var token = new CharacterTestTacticalEditToken(workspace, workspace.Cards[id]);
            string source = workspace.Cards[id].CompositionSourceJson;
            string name = workspace.Cards[id].DisplayName;
            string shared = CharacterTestTacticalCompositionJson.WriteShared(workspace.CloneTacticalDefinitions()[0]);
            Assert.IsTrue(workspace.TryRenameCompositionCard(token, "突进与撤退", out issue), issue);
            Assert.AreEqual(token.WorkspaceRevision + 1, workspace.CurrentRevision);
            Assert.AreEqual("突进与撤退", workspace.Cards[id].DisplayName);
            Assert.AreEqual(source, workspace.Cards[id].CompositionSourceJson);
            Assert.AreEqual(shared, CharacterTestTacticalCompositionJson.WriteShared(workspace.CloneTacticalDefinitions()[0]));
            Assert.IsFalse(workspace.TryRenameCompositionCard(token, "过期输入", out _));
            Assert.IsTrue(workspace.TryUndo(out issue), issue);
            Assert.AreEqual(name, workspace.Cards[id].DisplayName);
        }

        [Test]
        public void RuntimeSourceTargetsMapFrozenEventsAndLocalEndWithoutEnablingWrites()
        {
            var go = new GameObject("Frozen source navigation test"); go.SetActive(false);
            try
            {
                using (var fixture = new CharacterTestTacticalCardRuntimeFixture())
                {
                    var controller = go.AddComponent<CharacterTestRuntimeAiPlannerDevSceneController>();
                    var workspace = CharacterTestRuntimeAiLabDraftWorkspace.Import(CharacterTestArenaMatchConfig.CreateDefault());
                    Assert.IsTrue(workspace.TryImportComposition(1, 0, workspace.CurrentRevision, CharacterTestTacticalDslTrialTemplates.Create(), out string id, out string issue), issue);
                    var document = workspace.ResolveComposition(workspace.Cards[id]);
                    Assert.IsTrue(CharacterTestTacticalDslCompiler.TryCompile(document, fixture.Inputs, out var compiled, out issue), issue);
                    var frozen = CharacterTestTacticalFrozenSource.Capture(workspace, workspace.Cards[id], document, fixture.Inputs, compiled);
                    int EventIndex(Func<CharacterTestTacticalSourceLocation, bool> match)
                        => Enumerable.Range(0, compiled.Program.Execution.Events.Length).First(index =>
                            frozen.Locations.TryGetValue(compiled.Program.ExecutionDebugMap.Event(index), out var location) && match(location));
                    var state = new CharacterTestBehaviorExecutionState {
                        WaitEvent = EventIndex(l => l.NodeId == "attack" && l.EventId == "release"),
                        LastTriggeredEvent = EventIndex(l => l.NodeId == "assault" && l.Field == "endCondition") };
                    var runtime = new CharacterTestRuntimeAiLabBehaviorCardRuntimeIdentityResolution { Status = CharacterTestRuntimeAiLabIdentityResolutionStatus.Resolved,
                        Frame = 40, CommittedProgram = compiled.Program.WithTacticalSource(frozen),
                        LogicAgent = new CharacterTestRuntimeAiLabLogicAgentStateSnapshot { ExecutionState = state } };
                    var session = new CharacterTestTacticalCompositionEditingSession(controller); session.Bind(workspace, workspace.Cards[id], frozen);
                    session.BindRuntimeSources(runtime);
                    Assert.AreEqual(2, session.RuntimeTargets.Count);
                    Assert.AreEqual("attack", session.RuntimeTargets[0].Location.NodeId);
                    Assert.AreEqual("shared.attack", session.RuntimeTargets[0].Location.DefinitionId);
                    Assert.AreEqual(1, session.RuntimeTargets[0].Location.DefinitionRevision);
                    Assert.AreEqual("release", session.RuntimeTargets[0].Location.EventId);
                    Assert.AreEqual("assault", session.RuntimeTargets[1].Location.NodeId);
                    Assert.AreEqual("endCondition", session.RuntimeTargets[1].Location.Field);
                    var selectedTarget = session.RuntimeTargets[0];
                    session.BindRuntimeSources(runtime);
                    Assert.AreSame(selectedTarget, session.RuntimeTargets[0], "A presentation refresh of the same frame must not invalidate an open source menu.");
                    runtime.Frame++;
                    session.BindRuntimeSources(runtime);
                    Assert.AreNotSame(selectedTarget, session.RuntimeTargets[0], "A new runtime frame must invalidate the old source choice.");
                    Assert.AreEqual(40, selectedTarget.Frame, "An open read-only source menu retains its original observed frame.");
                    Assert.AreSame(frozen, selectedTarget.Source);
                    session.SelectLocation(session.RuntimeTargets[1].Location);
                    Assert.AreEqual(CharacterTestTacticalEditTarget.NodeEnd, session.Target);
                    Assert.IsTrue(session.AcceptsView(session.Token)); Assert.IsFalse(session.Accepts(session.Token));
                    runtime.Status = default; session.BindRuntimeSources(runtime);
                    Assert.IsEmpty(session.RuntimeTargets); Assert.IsNull(session.RuntimeSource);
                }
            }
            finally { UnityEngine.Object.DestroyImmediate(go); }
        }

        [Test]
        public void ReplayBaselineReuseRequiresTheSameImmutableProgramsAndExactProfile()
        {
            using (var fixture = new CharacterTestTacticalCardRuntimeFixture())
            {
                Assert.IsTrue(CharacterTestTacticalDslCompiler.TryCompile(CharacterTestTacticalDslTrialTemplates.Create(), fixture.Inputs,
                    out var compiled, out string issue), issue);
                var profile = CharacterTestRuntimeAiPlannerBehaviorCompositionCatalog.CreateDefaultProfile();
                var programs = Enumerable.Repeat(compiled.Program, profile.Entries.Count).ToArray();
                var expected = new CharacterTestRuntimeAiPlannerBehaviorOverlayInstallRequest(2, profile, programs, 123);
                Assert.IsTrue(CharacterTestRuntimeAiPlannerDevSceneController.IsSameReplayBinding(
                    new CharacterTestRuntimeAiPlannerBehaviorOverlayInstallRequest(2, profile.Clone(), programs.ToArray(), 123), expected));
                var changed = profile.Clone(); changed.DisplayName += " changed";
                Assert.IsFalse(CharacterTestRuntimeAiPlannerDevSceneController.IsSameReplayBinding(
                    new CharacterTestRuntimeAiPlannerBehaviorOverlayInstallRequest(2, changed, programs, 123), expected));
                changed = profile.Clone(); changed.Entries[0].EntryId += " changed";
                Assert.IsFalse(CharacterTestRuntimeAiPlannerDevSceneController.IsSameReplayBinding(
                    new CharacterTestRuntimeAiPlannerBehaviorOverlayInstallRequest(2, changed, programs, 123), expected));
                Assert.IsFalse(CharacterTestRuntimeAiPlannerDevSceneController.IsSameReplayBinding(
                    new CharacterTestRuntimeAiPlannerBehaviorOverlayInstallRequest(1, profile, programs, 123), expected));
                Assert.IsFalse(CharacterTestRuntimeAiPlannerDevSceneController.IsSameReplayBinding(
                    new CharacterTestRuntimeAiPlannerBehaviorOverlayInstallRequest(2, profile, programs, 124), expected));
                var sameHash = programs.ToArray(); sameHash[0] = compiled.Program.WithTacticalSource(null);
                Assert.AreEqual(programs[0].SemanticHash, sameHash[0].SemanticHash);
                Assert.IsFalse(CharacterTestRuntimeAiPlannerDevSceneController.IsSameReplayBinding(
                    new CharacterTestRuntimeAiPlannerBehaviorOverlayInstallRequest(2, profile, sameHash, 123), expected));
            }
        }

        [Test]
        public void BlankCardCanCreateSharedContentWithoutAHiddenTemplateAndUndoBothOwners()
        {
            using (var fixture = new CharacterTestTacticalCardRuntimeFixture())
            {
                var workspace = CharacterTestRuntimeAiLabDraftWorkspace.Import(CharacterTestArenaMatchConfig.CreateDefault());
                var document = new CharacterTestTacticalDslSource { CardId = "blank", Name = "空白", Root = CharacterTestTacticalCompositionEdits.NewNode("Sequence") };
                Assert.IsTrue(workspace.TryImportComposition(1, 0, workspace.CurrentRevision, document, out string id, out string issue), issue);
                string before = workspace.Cards[id].CompositionSourceJson;
                var token = new CharacterTestTacticalEditToken(workspace, workspace.Cards[id], CharacterTestTacticalEditTarget.Composition, document.Root.NodeId);
                Assert.IsTrue(workspace.TryCreateTacticalDefinitionReference(token, out issue), issue);
                var shared = workspace.CloneTacticalDefinitions().Single();
                Assert.AreEqual(shared.DefinitionId, workspace.Cards[id].CloneCompositionSource().Root.Children.Single().DefinitionId);
                CharacterTestTacticalAuthoringEdits.Add(shared.Source, shared.Source.EntryTimelineId, "RetreatFromTarget");
                Assert.IsTrue(workspace.TryReplaceTacticalDefinition(workspace.CaptureTacticalDefinitionEdit(shared.DefinitionId), shared.Source, true, out issue), issue);
                Assert.IsTrue(CharacterTestTacticalDslCompiler.TryCompile(workspace.ResolveComposition(workspace.Cards[id]), fixture.Inputs, out _, out issue), issue);
                Assert.IsTrue(workspace.TryUndo(out issue), issue);
                Assert.IsTrue(workspace.TryUndo(out issue), issue);
                Assert.AreEqual(0, workspace.CloneTacticalDefinitions().Length);
                Assert.AreEqual(before, workspace.Cards[id].CompositionSourceJson);
                Assert.IsTrue(workspace.TryRedo(out issue), issue);
                Assert.AreEqual(shared.DefinitionId, workspace.CloneTacticalDefinitions().Single().DefinitionId);
            }
        }

        [Test]
        public void NewWaitConditionCompilesAndGroupMovesPreserveUnselectedIdentity()
        {
            using (var fixture = new CharacterTestTacticalCardRuntimeFixture())
            {
                var document = CharacterTestTacticalDslTrialTemplates.Create();
                var source = new CharacterTestTacticalCompositionSource { Root = CharacterTestTacticalCompositionEdits.NewNode("Sequence") };
                var wait = CharacterTestTacticalCompositionEdits.NewNode("Wait");
                wait.Condition = document.Root.Children[0].EndCondition.Clone();
                CharacterTestTacticalCompositionEdits.Insert(source, source.Root.NodeId, 0, wait);
                document.Root = source.Root;
                Assert.IsTrue(CharacterTestTacticalDslCompiler.TryCompile(document, fixture.Inputs, out _, out string issue), issue);
                var a = CharacterTestTacticalCompositionEdits.NewNode("Reference", document.Definitions[0].DefinitionId);
                var b = CharacterTestTacticalCompositionEdits.NewNode("Reference", document.Definitions[0].DefinitionId);
                var c = CharacterTestTacticalCompositionEdits.NewNode("Exit");
                source.Root.Children.AddRange(new[] { a, b, c });
                Assert.Throws<ArgumentException>(() => CharacterTestTacticalCompositionEdits.Group(source, new[] { a.NodeId, c.NodeId }, "Sequence"));
                CharacterTestTacticalCompositionEdits.Group(source, new[] { a.NodeId, b.NodeId }, "Sequence");
                var group = source.Root.Children[1];
                CharacterTestTacticalCompositionEdits.Move(source, group.NodeId, 1);
                Assert.AreSame(c, source.Root.Children[1]);
                Assert.AreSame(a, group.Children[0]); Assert.AreSame(b, group.Children[1]);
                CharacterTestTacticalCompositionEdits.Ungroup(source, group.NodeId);
                Assert.AreEqual(new[] { wait.NodeId, c.NodeId, a.NodeId, b.NodeId }, source.Root.Children.Select(n => n.NodeId).ToArray());
            }
        }

        [Test]
        public void SameDefinitionDifferentReferenceRejectsPendingWriteAndSharedEditUsesOneOwner()
        {
            var go = new GameObject("Composition lease test"); go.SetActive(false);
            try
            {
                var controller = go.AddComponent<CharacterTestRuntimeAiPlannerDevSceneController>();
                var workspace = CharacterTestRuntimeAiLabDraftWorkspace.Import(CharacterTestArenaMatchConfig.CreateDefault());
                var document = CharacterTestTacticalDslTrialTemplates.Create();
                document.Root = CharacterTestTacticalCompositionEdits.NewNode("Sequence");
                document.Root.Children.Add(CharacterTestTacticalCompositionEdits.NewNode("Reference", "shared.attack"));
                document.Root.Children.Add(CharacterTestTacticalCompositionEdits.NewNode("Reference", "shared.attack"));
                Assert.IsTrue(workspace.TryImportComposition(1, 0, workspace.CurrentRevision, document, out string id, out string issue), issue);
                var session = new CharacterTestTacticalCompositionEditingSession(controller);
                session.Bind(workspace, workspace.Cards[id]);
                string a = document.Root.Children[0].NodeId, b = document.Root.Children[1].NodeId;
                session.Select(a, CharacterTestTacticalEditTarget.SharedDefinition);
                var pending = session.Token;
                Assert.IsTrue(session.ConfirmShared(pending));
                string before = workspace.Cards[id].CompositionSourceJson;
                session.Select(b, CharacterTestTacticalEditTarget.SharedDefinition);
                Assert.IsFalse(session.EditInternal(pending, s => s.Name = "stale", out _));
                Assert.IsFalse(session.EditInternal(session.Token, s => s.Name = "unconfirmed", out _));
                Assert.IsTrue(session.ConfirmShared(session.Token));
                Assert.IsTrue(session.EditInternal(session.Token, s => s.Name = "共同修改", out issue), issue);
                Assert.AreEqual(before, workspace.Cards[id].CompositionSourceJson);
                Assert.AreEqual("共同修改", workspace.CloneTacticalDefinitions().Single(d => d.DefinitionId == "shared.attack").Source.Name);
                Assert.AreEqual(2, workspace.CloneTacticalDefinitions().Single(d => d.DefinitionId == "shared.attack").Revision);
                Assert.IsFalse(session.Accepts(pending));
                Assert.IsTrue(workspace.TryUndo(out issue), issue);
                Assert.AreEqual(1, workspace.CloneTacticalDefinitions().Single(d => d.DefinitionId == "shared.attack").Revision);
                Assert.IsTrue(workspace.TryRedo(out issue), issue);
                Assert.AreEqual(2, workspace.CloneTacticalDefinitions().Single(d => d.DefinitionId == "shared.attack").Revision);
            }
            finally { UnityEngine.Object.DestroyImmediate(go); }
        }

        [Test]
        public void CardConditionProjectionCannotModifySharedContentOrAnotherField()
        {
            var go = new GameObject("Composition field test"); go.SetActive(false);
            try
            {
                var controller = go.AddComponent<CharacterTestRuntimeAiPlannerDevSceneController>();
                var workspace = CharacterTestRuntimeAiLabDraftWorkspace.Import(CharacterTestArenaMatchConfig.CreateDefault());
                var document = CharacterTestTacticalDslTrialTemplates.Create();
                Assert.IsTrue(workspace.TryImportComposition(1, 0, workspace.CurrentRevision, document, out string id, out string issue), issue);
                var session = new CharacterTestTacticalCompositionEditingSession(controller); session.Bind(workspace, workspace.Cards[id]);
                string shared = CharacterTestTacticalCompositionJson.WriteShared(workspace.CloneTacticalDefinitions()[0]);
                string node = document.Root.Children[0].NodeId;
                session.Select(node, CharacterTestTacticalEditTarget.NodeEnd); var pending = session.Token;
                session.Select(document.Root.NodeId, CharacterTestTacticalEditTarget.CardActivation);
                Assert.IsFalse(session.EditInternal(pending, s => s.ActivationConditionRoot = null, out _));
                var condition = document.Root.Children[0].EndCondition.Clone();
                Assert.IsTrue(session.EditInternal(session.Token, s => { s.ActivationConditionRoot = condition; s.Name = "投影不会覆盖卡名"; }, out issue), issue);
                Assert.IsNotNull(workspace.Cards[id].CloneCompositionSource().ActivationConditionRoot);
                Assert.IsNotNull(workspace.Cards[id].CloneCompositionSource().Root.Children[0].EndCondition);
                Assert.AreEqual(document.Name, workspace.Cards[id].DisplayName);
                Assert.AreEqual(shared, CharacterTestTacticalCompositionJson.WriteShared(workspace.CloneTacticalDefinitions()[0]));
            }
            finally { UnityEngine.Object.DestroyImmediate(go); }
        }

        [Test]
        public void FrozenPreviewUsesStoredInternalCompilationAndRejectsEdits()
        {
            using (var fixture = new CharacterTestTacticalCardRuntimeFixture())
            {
                var workspace = CharacterTestRuntimeAiLabDraftWorkspace.Import(CharacterTestArenaMatchConfig.CreateDefault());
                var document = CharacterTestTacticalDslTrialTemplates.Create();
                Assert.IsTrue(workspace.TryImportComposition(1, 0, workspace.CurrentRevision, document, out string id, out string issue), issue);
                Assert.IsTrue(CharacterTestTacticalDslCompiler.TryCompile(document, fixture.Inputs, out var compiled, out issue), issue);
                var frozen = CharacterTestTacticalFrozenSource.Capture(workspace, workspace.Cards[id], document, fixture.Inputs, compiled);
                var source = frozen.CloneDocument().Definitions.Single(d => d.DefinitionId == "shared.attack").Source;
                var preview = CharacterTestTacticalAuthoringPreview.FromFrozen(source, frozen.SharedCompilations["shared.attack"], frozen.CharacterId);
                Assert.IsTrue(preview.Ready); Assert.IsTrue(preview.IsFrozen);
                StringAssert.Contains("历史", preview.StatusText); StringAssert.DoesNotContain("尚未准备", preview.PreparationSummary);
                Assert.Throws<InvalidOperationException>(() => preview.WithEdits(s => s.Name = "不允许"));
                StringAssert.Contains("冻结编译结果", preview.DescribeEvent(source.EntryTimelineId, "attack"));
            }
        }

        [Test]
        public void TacticalCompositionBinder_SameDocumentRefreshReusesFlattenedRowsAndCurrentSelectionNodes()
        {
            var binder = new CharacterTestFairyGuiTacticalCompositionBinder();
            var document = CharacterTestTacticalDslTrialTemplates.Create();
            InvokeRefreshFlattenedItems(binder, document);
            var items = ReadFlattenedItems(binder);
            Assert.Greater(items.Count, 1);
            object itemsList = items;
            var firstNode = ReadFlattenedNode(items, 0);

            InvokeRefreshFlattenedItems(binder, document);

            Assert.AreSame(itemsList, ReadFlattenedItems(binder),
                "Repeated Bind of the same Document must retain the flattened row list.");
            Assert.AreSame(firstNode, ReadFlattenedNode(items, 0),
                "Repeated Bind of the same Document must retain the current row node identity.");
        }

        [Test]
        public void TacticalCompositionBinder_NewDocumentWithSameRowCountRefreshesCurrentNodeOrderWithoutExtraCloseRows()
        {
            var binder = new CharacterTestFairyGuiTacticalCompositionBinder();
            var original = CharacterTestTacticalDslTrialTemplates.Create();
            var reordered = CharacterTestTacticalDslTrialTemplates.Create();
            Assert.GreaterOrEqual(reordered.Root.Children.Count, 2);
            var firstChild = reordered.Root.Children[0];
            reordered.Root.Children.RemoveAt(0);
            reordered.Root.Children.Add(firstChild);

            InvokeRefreshFlattenedItems(binder, original);
            int originalCount = ReadFlattenedItems(binder).Count;
            InvokeRefreshFlattenedItems(binder, reordered);
            IList items = ReadFlattenedItems(binder);

            Assert.AreEqual(originalCount, items.Count,
                "Reordering a same-sized composition must preserve the row count.");
            Assert.AreSame(reordered.Root, ReadFlattenedNode(items, 0));
            Assert.AreSame(reordered.Root.Children[0], ReadFlattenedNode(items, 1),
                "A new Document must replace cached node references in the current order.");
            Assert.IsFalse(ReadFlattenedClose(items, 0), "The root opening row is not a close row.");
        }

        [Test]
        public void TacticalCompositionBinder_LeafNodeProducesOnlyOpeningRowWithoutCloseRow()
        {
            var binder = new CharacterTestFairyGuiTacticalCompositionBinder();
            var document = new CharacterTestTacticalDslSource
            {
                CardId = "leaf-card",
                Name = "叶节点",
                Root = CharacterTestTacticalCompositionEdits.NewNode("Exit")
            };

            InvokeRefreshFlattenedItems(binder, document);
            IList items = ReadFlattenedItems(binder);

            Assert.AreEqual(1, items.Count);
            Assert.AreSame(document.Root, ReadFlattenedNode(items, 0));
            Assert.IsFalse(ReadFlattenedClose(items, 0));
        }

        private static void InvokeRefreshFlattenedItems(
            CharacterTestFairyGuiTacticalCompositionBinder binder,
            CharacterTestTacticalDslSource document)
        {
            typeof(CharacterTestFairyGuiTacticalCompositionBinder)
                .GetMethod("RefreshFlattenedItems", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(binder, new object[] { document });
        }

        private static IList ReadFlattenedItems(CharacterTestFairyGuiTacticalCompositionBinder binder)
        {
            return (IList)typeof(CharacterTestFairyGuiTacticalCompositionBinder)
                .GetField("_items", BindingFlags.Instance | BindingFlags.NonPublic)
                .GetValue(binder);
        }

        private static CharacterTestTacticalDslNode ReadFlattenedNode(IList items, int index)
        {
            object tuple = items[index];
            FieldInfo field = tuple.GetType().GetField("node")
                ?? tuple.GetType().GetField("Item1");
            return (CharacterTestTacticalDslNode)field.GetValue(tuple);
        }

        private static bool ReadFlattenedClose(IList items, int index)
        {
            object tuple = items[index];
            FieldInfo field = tuple.GetType().GetField("close")
                ?? tuple.GetType().GetField("Item2");
            return (bool)field.GetValue(tuple);
        }
    }
}
