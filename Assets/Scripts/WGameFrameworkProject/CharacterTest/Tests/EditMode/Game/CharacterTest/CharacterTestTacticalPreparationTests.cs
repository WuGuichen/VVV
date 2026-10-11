using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using MxFramework.CharacterAction;
using MxFramework.Game.CharacterTest;
using NUnit.Framework;
using UnityEngine;

namespace MxFramework.Tests.Game.CharacterTest
{
    public sealed class CharacterTestTacticalPreparationTests
    {
        private GameObject _host;
        private CharacterTestRuntimeAiPlannerDevSceneController _controller;
        [SetUp] public void Setup()
        {
            _host = new GameObject("Tactical preparation test"); _host.SetActive(false);
            _controller = _host.AddComponent<CharacterTestRuntimeAiPlannerDevSceneController>();
            _controller.PrepareInitialSetupState(restorePersistedDraft: false);
        }
        [TearDown] public void Cleanup() { UnityEngine.Object.DestroyImmediate(_host); }
        private CharacterTestRuntimeAiLabDraftWorkspace Workspace => _controller.DraftWorkspace;
        private static CharacterTestTacticalDslSource Movement(string key = "one") => new CharacterTestTacticalDslSource {
            CardId = key, Name = key, Root = new CharacterTestTacticalDslNode { NodeId = "root", Kind = "Sequence", Children = new List<CharacterTestTacticalDslNode> {
                new CharacterTestTacticalDslNode { NodeId = "move", Kind = "Reference", DefinitionId = key } } },
            Definitions = new List<CharacterTestTacticalSharedDefinition> { new CharacterTestTacticalSharedDefinition {
                DefinitionId = key, Source = CharacterTestTacticalAuthoringTemplates.CreateMovement(CharacterTestRuntimeAiLabMovementStrategy.OrbitTarget) } } };
        private CharacterTestRuntimeAiLabBehaviorCardDraft Import(string key = "one", bool removeUnusedSlots = false)
        {
            var source = Movement(key);
            if (removeUnusedSlots) source.Definitions[0].Source.Slots.Clear();
            Assert.IsTrue(Workspace.TryImportComposition(1, 0, Workspace.CurrentRevision, source, out var id, out string issue), issue);
            return Workspace.Cards[id];
        }
        private object[] Prepare(CharacterTestTacticalInputRequirements requirements, bool fresh = false)
        {
            var args = new object[] { 1, 0, CharacterTestCharacterDefinitionRegistry.DefaultPlayableCharacterId, requirements, fresh, null, null };
            var method = _controller.GetType().GetMethod("TryPrepareTacticalSlotInputsCore", BindingFlags.Instance | BindingFlags.NonPublic);
            method.Invoke(_controller, args); return args;
        }

        [Test]
        public void MissingCharacterDefinitionReportsAnIssueInsteadOfThrowing()
        {
            var card = Import();
            var args = new object[] { card, int.MaxValue, null, null };
            var method = _controller.GetType().GetMethod("TryPrepareCardSlotBindings", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsFalse((bool)method.Invoke(_controller, args));
            Assert.IsNull(args[2]);
            StringAssert.Contains(int.MaxValue.ToString(), (string)args[3]);
        }

        [Test]
        public void LightweightRoleUsesTheSameEquipmentRigAndContentHashAsReferenceRuntime()
        {
            int character = CharacterTestCharacterDefinitionRegistry.DefaultPlayableCharacterId;
            var measurement = CharacterTestTacticalSummaryPreparation.StandardMeasurement();
            using var probe = new CharacterActionReferenceProbeSession(new CharacterActionId(301), measurement.ReferenceDummy, character);
            var role = CharacterTestTacticalPreparationCatalog.Role(character);
            var input = role.Input(301);
            Assert.AreEqual(probe.Source.Equipment, role.Source.Equipment);
            Assert.IsTrue(probe.TryComputeActionHash(out var expected, out string issue), issue);
            Assert.IsTrue(CharacterActionContentHashUtility.TryCompute(input.Action, input.Source, input.Physics, input.Bakes, input.Bindings, out var actual, out issue), issue);
            Assert.AreEqual(expected, actual, "Authoring snapshots must match the authoritative measurement inputs.");
        }

        [Test]
        public void RepeatedPreparationReusesSuccessOrFailureAndFreshBoundaryRevalidates()
        {
            var first = Prepare(CharacterTestTacticalInputRequirements.All);
            long builds = _controller.TacticalInputBuildCount, roles = CharacterTestTacticalPreparationCatalog.RoleBuildCount;
            for (int i = 0; i < 8; i++)
            {
                var again = Prepare(CharacterTestTacticalInputRequirements.All);
                Assert.AreSame(first[5], again[5]); Assert.AreEqual(first[6], again[6]);
            }
            Assert.AreEqual(builds, _controller.TacticalInputBuildCount); Assert.AreEqual(roles, CharacterTestTacticalPreparationCatalog.RoleBuildCount);
            Prepare(CharacterTestTacticalInputRequirements.All, true);
            Assert.AreEqual(builds + 1, _controller.TacticalInputBuildCount);
        }

        [Test]
        public void PureMovementDoesNotPrepareActionSummaryOrBuildARole()
        {
            long roles = CharacterTestTacticalPreparationCatalog.RoleBuildCount;
            var card = Import(); var preview = _controller.BuildTacticalPreview(card);
            Assert.IsTrue(preview.Ready, preview.Diagnostic);
            Assert.AreEqual(0, _controller.TacticalInputBuildCount); Assert.AreEqual(roles, CharacterTestTacticalPreparationCatalog.RoleBuildCount);
            Assert.IsNull(preview.SummaryRequest);
            Assert.AreEqual(CharacterTestTacticalInputRequirements.None, preview.WholeCard.Program.TacticalSource.Requirements);
            Assert.AreEqual(0, preview.WholeCard.Program.TacticalSource.ActionId);
        }

        [Test]
        public void FeedbackSelectionAndConditionNavigationNeverReprepareOrRecompile()
        {
            var card = Import(removeUnusedSlots: true); var first = _controller.BuildTacticalPreview(card);
            long builds = _controller.TacticalCompileBuildCount;
            for (int i = 0; i < 10; i++)
            {
                _controller.CompositionEditingSession.Feedback("feedback " + i);
                _controller.CompositionEditingSession.Select("root"); _controller.BuildTacticalPreview(card);
                _controller.CompositionEditingSession.Select("move", CharacterTestTacticalEditTarget.SharedDefinition); _controller.BuildTacticalPreview(card);
                _controller.CompositionEditingSession.ConfirmShared(_controller.CompositionEditingSession.Token); _controller.BuildTacticalPreview(card);
            }
            Assert.AreEqual(0, _controller.TacticalInputBuildCount); Assert.AreEqual(builds, _controller.TacticalCompileBuildCount);
            _controller.CompositionEditingSession.FinishSharedEditing();
            Assert.AreSame(first.WholeCard.Program.Execution, _controller.BuildTacticalPreview(card).WholeCard.Program.Execution);
        }

        [Test]
        public void SharedSourceEditingUpdatesCompiledFramesAndUndoRestoresWholeCard()
        {
            var card = Import(removeUnusedSlots: true);
            var original = _controller.BuildTacticalPreview(card);
            var session = _controller.CompositionEditingSession;
            Assert.IsTrue(session.ConfirmShared(session.Token));
            var sourceView = _controller.BuildTacticalPreview(card);
            Assert.IsNull(sourceView.WholeCard);
            Assert.IsTrue(sourceView.Ready, sourceView.Diagnostic);
            Assert.IsTrue(sourceView.Editor.CanEdit);
            string timelineId = sourceView.Source.EntryTimelineId;
            var staleToken = sourceView.Editor.Token;
            Assert.IsTrue(sourceView.Editor.Commit(staleToken, "调整源事件时间", source => {
                var ev = CharacterTestTacticalAuthoringEdits.Event(source, timelineId, "move");
                ev.Start = CharacterTestTacticalAuthoringTemplates.Absolute(8);
                ev.End = CharacterTestTacticalAuthoringTemplates.Anchor("move", "Start", 24);
            }));
            var changed = _controller.BuildTacticalPreview(card);
            Assert.IsTrue(changed.TryGetFrames("move", out int start, out int end));
            Assert.AreEqual(8, start); Assert.AreEqual(32, end);
            var compiled = _controller.CompileBehaviorCardLogic(card).Program;
            int index = Enumerable.Range(0, compiled.Execution.Events.Length)
                .Single(i => compiled.ExecutionDebugMap.Event(i).EndsWith(":event:move", StringComparison.Ordinal));
            Assert.AreEqual(start, compiled.Execution.Events[index].StartFrame);
            Assert.AreEqual(24, compiled.Execution.Events[index].DurationFrames);
            session.SetNativeExecutionView(true);
            Assert.IsNotNull(_controller.BuildTacticalPreview(card).WholeCard);
            session.SetNativeExecutionView(false);
            Assert.IsNull(_controller.BuildTacticalPreview(card).WholeCard);
            session.Undo(false);
            card = Workspace.Cards[card.CardDraftId];
            var restored = _controller.BuildTacticalPreview(card);
            Assert.IsTrue(restored.TryGetFrames("move", out start, out end));
            Assert.AreEqual(0, start); Assert.AreEqual(120, end);
            session.FinishSharedEditing();
            var whole = _controller.BuildTacticalPreview(card);
            Assert.IsFalse(whole.Editor.CanEdit);
            Assert.AreEqual(original.WholeCard.Program.SemanticHash, whole.WholeCard.Program.SemanticHash);
            Assert.IsFalse(whole.Editor.Commit(staleToken, "过期编辑", source => source.Name = "stale"));
        }

        [Test]
        public void UnrelatedCardAndRenameKeepExecutionWhileSharedSemanticEditRecompiles()
        {
            var card = Import(); var first = _controller.CompileBehaviorCardLogic(card); long builds = _controller.TacticalCompileBuildCount;
            Import("unrelated"); _controller.CompileBehaviorCardLogic(card);
            Assert.AreEqual(builds, _controller.TacticalCompileBuildCount);
            Assert.IsTrue(Workspace.TryRenameCompositionCard(new CharacterTestTacticalEditToken(Workspace, card), "renamed", out var issue), issue);
            Assert.AreSame(first.Program.Execution, _controller.CompileBehaviorCardLogic(card).Program.Execution);
            Assert.AreEqual(builds, _controller.TacticalCompileBuildCount);
            var definition = Workspace.CloneTacticalDefinitions().Single(d => d.DefinitionId == "one");
            definition.Source.Timelines[0].Events[0].Operation.Speed = 0.5f;
            Assert.IsTrue(Workspace.TryReplaceTacticalDefinition(Workspace.CaptureTacticalDefinitionEdit("one"), definition.Source, true, out issue), issue);
            Assert.AreNotEqual(first.Program.SemanticHash, _controller.CompileBehaviorCardLogic(card).Program.SemanticHash);
            Assert.AreEqual(builds + 1, _controller.TacticalCompileBuildCount); Assert.AreEqual(0, _controller.TacticalInputBuildCount);
        }

        [Test]
        public void DependencyClosureExcludesUnreferencedDefinitionsAndActionSlots()
        {
            var card = Import(); Import("unrelated");
            Assert.AreEqual(2, Workspace.ResolveComposition(card).Definitions.Count);
            var compileSource = Workspace.ResolveComposition(card, referencedOnly: true);
            Assert.AreEqual(1, compileSource.Definitions.Count);
            Assert.AreEqual(CharacterTestTacticalInputRequirements.None, CharacterTestTacticalDependencies.Analyze(compileSource));
            var movement = compileSource.Definitions[0].Source.Timelines[0].Events[0].Operation.Movement;
            movement.DistanceSource = "ActionBand"; movement.ActionSlotId = "primary";
            Assert.AreEqual(CharacterTestTacticalInputRequirements.PrimaryAction | CharacterTestTacticalInputRequirements.SpatialSummary,
                CharacterTestTacticalDependencies.Analyze(compileSource));
        }

        [Test]
        public void SetupPresentationNeverExecutesPreflight()
        {
            Import(); long count = _controller.TacticalCompileBuildCount;
            for (int i = 0; i < 10; i++)
            { Assert.IsTrue(_controller.TryGetSessionStartStatusForDisplay(out _, out bool pending)); Assert.IsTrue(pending); }
            Assert.AreEqual(count, _controller.TacticalCompileBuildCount); Assert.AreEqual(0, _controller.TacticalInputBuildCount);
        }

        [Test]
        public void InspectionCatalogIsReusedButExecutableRuntimeLoadsRemainIndependent()
        {
            var inspection = CharacterTestCharacterActionConfigSource.LoadInspectionCatalog();
            Assert.AreSame(inspection, CharacterTestCharacterActionConfigSource.LoadInspectionCatalog());
            var runtime = CharacterTestCharacterActionConfigSource.Load();
            Assert.AreNotSame(inspection.Registry, runtime.Registry);
            long generation = CharacterTestTacticalPreparationCatalog.Revision;
            CharacterTestDefaultBakedWeaponTraceSource.InvalidateCachedInstanceForMeasurement();
            Assert.Greater(CharacterTestTacticalPreparationCatalog.Revision, generation);
            Assert.AreNotSame(inspection, CharacterTestCharacterActionConfigSource.LoadInspectionCatalog());
        }

        [Test]
        public void PureMovementFrozenBindingDoesNotRequireAnAttackOpener()
        {
            var card = Import(); var compiled = _controller.CompileBehaviorCardLogic(card);
            var dummy = CharacterTestTacticalSummaryPreparation.StandardMeasurement().ReferenceDummy;
            using var probe = new CharacterActionReferenceProbeSession(new CharacterActionId(301), dummy);
            Assert.IsTrue(compiled.Program.TacticalSource.ValidateBinding(probe.Runtime, probe.Source, out string issue), issue);
            Assert.IsTrue(_controller.SelectTacticalTestAction(301, _controller.SelectedTacticalTestActionKey));
            long count = _controller.TacticalCompileBuildCount;
            Assert.IsTrue(_controller.CompileBehaviorCardLogic(card).Success); Assert.AreEqual(count, _controller.TacticalCompileBuildCount);
        }

        [Test]
        public void SummaryChangeInvalidatesNegativePreparation()
        {
            Prepare(CharacterTestTacticalInputRequirements.All); long before = _controller.TacticalInputBuildCount;
            CharacterTestTacticalPreparationCatalog.SummaryChanged(); Prepare(CharacterTestTacticalInputRequirements.All);
            Assert.AreEqual(before + 1, _controller.TacticalInputBuildCount);
        }
    }
}
