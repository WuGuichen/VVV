using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using MxFramework.Game.CharacterTest;
using NUnit.Framework;
using UnityEngine;

namespace MxFramework.Tests.Game.CharacterTest
{
    public sealed class CharacterTestRuntimeAiLabIdentityProjectionTests
    {
        private const int FirstSyntheticCardNumericId = 1001;
        private const int SecondSyntheticCardNumericId = 1002;

        private static readonly FieldInfo BaselineRevisionField = typeof(CharacterTestRuntimeAiPlannerDevSceneController)
            .GetField("_baselineRevision", BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly FieldInfo AppliedRevisionEventsField = typeof(CharacterTestRuntimeAiPlannerDevSceneController)
            .GetField("_appliedRevisionEvents", BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly FieldInfo ActiveRevisionField = typeof(CharacterTestRuntimeAiPlannerDevSceneController)
            .GetField("_activeRevision", BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly FieldInfo MatchConfigField = typeof(CharacterTestRuntimeAiPlannerDevSceneController)
            .GetField("_matchConfig", BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly FieldInfo DraftWorkspaceField = typeof(CharacterTestRuntimeAiPlannerDevSceneController)
            .GetField("_draftWorkspace", BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly FieldInfo ProjectCatalogAuthoringField = typeof(CharacterTestRuntimeAiPlannerDevSceneController)
            .GetField("_projectCatalogAuthoring", BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly FieldInfo BehaviorCardReferencesField = typeof(CharacterTestRuntimeAiPlannerDevSceneController)
            .GetField("_behaviorCardReferences", BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly FieldInfo DraftPersistenceField = typeof(CharacterTestRuntimeAiPlannerDevSceneController)
            .GetField("_draftPersistence", BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly FieldInfo DraftPersistenceLastStatusField =
            typeof(CharacterTestRuntimeAiLabDraftPersistence).GetField(
                "<LastStatus>k__BackingField",
                BindingFlags.Instance | BindingFlags.NonPublic);
#if UNITY_EDITOR
        private static readonly FieldInfo LastAuthoringWorkspaceRoleResultField =
            typeof(CharacterTestRuntimeAiPlannerDevSceneController).GetField(
                "_initialOpenResult",
                BindingFlags.Instance | BindingFlags.NonPublic);
#endif
        private static readonly FieldInfo RuntimeEntryBackingField =
            typeof(CharacterTestRuntimeAiLabBehaviorEntryDraft).GetField(
                "<RuntimeEntry>k__BackingField",
                BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly MethodInfo BuildCandidateRevisionMethod = typeof(CharacterTestRuntimeAiPlannerDevSceneController)
            .GetMethod("BuildCandidateRevision", BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly MethodInfo SetBehaviorRecipeSaveCheckpointMethod =
            typeof(CharacterTestRuntimeAiPlannerDevSceneController).GetMethod(
                "SetBehaviorRecipeSaveCheckpoint",
                BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly MethodInfo TruncateAppliedRevisionEventsMethod = typeof(CharacterTestRuntimeAiPlannerDevSceneController)
            .GetMethod("TruncateAppliedRevisionEventsAfter", BindingFlags.Instance | BindingFlags.NonPublic);

        [Test]
        public void CandidateRevision_LegacyCardsKeepPublishedIdentityEmpty()
        {
            GameObject host = CreateController(out CharacterTestRuntimeAiPlannerDevSceneController controller);
            try
            {
                CharacterTestRuntimeAiLabCandidateRevision revision = RequireCandidate(controller);
                Assert.That(revision.CardIdentities.Count, Is.EqualTo(revision.Profile.Entries.Count));
                Assert.That(revision.CardIdentities.Count, Is.EqualTo(revision.LogicPrograms.Count));
                for (int i = 0; i < revision.CardIdentities.Count; i++)
                {
                    CharacterTestRuntimeAiLabRevisionCardIdentity identity = revision.CardIdentities[i];
                    Assert.That(identity.BindingIndex, Is.EqualTo(i));
                    Assert.That(identity.HasPublishedSource, Is.False);
                    Assert.That(identity.PublishedStableId, Is.Empty);
                    Assert.That(identity.LegacySourceKey, Is.EqualTo(revision.Profile.Entries[i].BehaviorId));
                    Assert.That(identity.CandidateSemanticHash, Is.EqualTo(revision.LogicPrograms[i].SemanticHash));
                }
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(host);
            }
        }

        [Test]
        public void CandidateRevision_ClonesInputsAndRejectsMisalignedBindings()
        {
            GameObject host = CreateController(out CharacterTestRuntimeAiPlannerDevSceneController controller);
            try
            {
                CharacterTestRuntimeAiLabCandidateRevision source = RequireCandidate(controller);
                CharacterTestRuntimeAiPlannerBehaviorCompositionProfile profile = source.Profile;
                var programs = new CharacterTestRuntimeAiLabCompiledLogicProgram[source.LogicPrograms.Count];
                var identities = new CharacterTestRuntimeAiLabRevisionCardIdentity[source.CardIdentities.Count];
                for (int i = 0; i < programs.Length; i++)
                {
                    programs[i] = source.LogicPrograms[i];
                    identities[i] = source.CardIdentities[i];
                }
                string originalBehaviorId = profile.Entries[0].BehaviorId;
                CharacterTestRuntimeAiLabCompiledLogicProgram originalProgram = programs[0];
                CharacterTestRuntimeAiLabRevisionCardIdentity originalIdentity = identities[0];
                Assert.That(
                    CharacterTestRuntimeAiLabCandidateRevision.TryCreate(
                        1,
                        91,
                        profile,
                        source.Comparison,
                        programs,
                        identities,
                        string.Empty,
                        out CharacterTestRuntimeAiLabCandidateRevision frozen,
                        out string freezeDiagnostic),
                    Is.True,
                    freezeDiagnostic);

                profile.Entries[0].BehaviorId = "mutated-input";
                programs[0] = programs[1];
                identities[0] = identities[1];
                Assert.That(frozen.Profile.Entries[0].BehaviorId, Is.EqualTo(originalBehaviorId));
                Assert.That(frozen.LogicPrograms[0], Is.SameAs(originalProgram));
                Assert.That(frozen.CardIdentities[0], Is.SameAs(originalIdentity));

                CharacterTestRuntimeAiPlannerBehaviorCompositionProfile exposed = frozen.Profile;
                exposed.Entries[0].BehaviorId = "mutated-exposed-copy";
                Assert.That(frozen.Profile.Entries[0].BehaviorId, Is.EqualTo(originalBehaviorId));

                identities[0] = source.CardIdentities[1];
                Assert.That(
                    CharacterTestRuntimeAiLabCandidateRevision.TryCreate(
                        1,
                        92,
                        source.Profile,
                        source.Comparison,
                        CopyPrograms(source),
                        identities,
                        string.Empty,
                        out CharacterTestRuntimeAiLabCandidateRevision invalid,
                        out string invalidDiagnostic),
                    Is.False);
                Assert.That(invalid, Is.Null);
                StringAssert.Contains("inconsistent", invalidDiagnostic);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(host);
            }
        }

        [Test]
        public void BehaviorCardProjection_DoesNotRequireWrapperDefinitionOrTitle()
        {
            GameObject host = CreateController(out CharacterTestRuntimeAiPlannerDevSceneController controller);
            try
            {
                CharacterTestRuntimeAiLabBehaviorGroupDraft group = SelectedGroup(controller);
                CharacterTestRuntimeAiLabBehaviorEntryDraft entry = group.Entries[0];
                CharacterTestRuntimeAiLabBehaviorCardDraft card =
                    controller.DraftWorkspace.Cards[entry.CardDraftId];
                entry.RuntimeEntry.BehaviorId = "missing.wrapper.definition";
                card.DisplayName = string.Empty;

                CharacterTestFairyGuiRuntimeAiPlannerDevPanelViewModel view =
                    CharacterTestFairyGuiRuntimeAiPlannerDevPanelViewModelBuilder.Build(
                        controller,
                        commandStatus: null,
                        textProvider: null,
                        CharacterTestRuntimeAiLabWorkspaceMode.BehaviorCard,
                        selectedWorkbenchEntryIndex: 0);

                Assert.That(view.BehaviorCardIdentity.EditResolutionStatus,
                    Is.EqualTo(CharacterTestRuntimeAiLabIdentityResolutionStatus.Resolved));
                Assert.That(view.BehaviorCardIdentity.EditCard.DisplayName, Is.EqualTo("未命名行为卡"));
                Assert.That(
                    view.BehaviorCardWorkspace.Timeline.CardTitleText,
                    Is.EqualTo("未命名行为卡"));
                Assert.That(view.BehaviorCardWorkspace.Timeline.Available, Is.True);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(host);
            }
        }

        [Test]
        public void RuntimeResolution_JoinsSelectedEntityAndUsesFrozenPrimaryAction()
        {
            GameObject host = CreateController(out CharacterTestRuntimeAiPlannerDevSceneController controller);
            try
            {
                CharacterTestRuntimeAiLabCandidateRevision revision = RequireCandidate(controller);
                InstallBaseline(controller, revision);
                CharacterTestRuntimeAiLabLogicAgentStateSnapshot first = LogicAgent(revision, 10, 0);
                CharacterTestRuntimeAiLabLogicAgentStateSnapshot second = LogicAgent(revision, 20, 1);
                CharacterTestRuntimeAiPlannerStateSnapshot state = State(revision, 0, first, second);
                CharacterTestRuntimeAiPlannerAgentSnapshot selected = Agent(
                    20,
                    "本地化后的等待动作 [" + revision.CardIdentities[0].RuntimeEntryId + "]");

                CharacterTestRuntimeAiLabBehaviorCardRuntimeIdentityResolution result =
                    CharacterTestRuntimeAiLabBehaviorCardIdentityProjector.ResolveRuntime(
                        controller,
                        state,
                        4,
                        selected,
                        hasSelectedAgent: true);

                Assert.That(
                    result.Status,
                    Is.EqualTo(CharacterTestRuntimeAiLabIdentityResolutionStatus.Resolved),
                    FormatRuntimeResolution(result));
                Assert.That(result.CommittedCard.BindingIndex, Is.EqualTo(1));
                Assert.That(result.LogicAgent, Is.SameAs(second));
                Assert.That(result.PrimaryAction.Available, Is.True);
                Assert.That(
                    result.PrimaryAction.StageId,
                    Is.EqualTo(
                        revision.LogicPrograms[1].Stages[second.PrimaryActionStageIndex].StageId));
                Assert.That(result.PrimaryAction.LegacyActionKind,
                    Is.EqualTo(
                        revision.LogicPrograms[1].Stages[second.PrimaryActionStageIndex].LegacyActionKind));

                CharacterTestRuntimeAiLabBehaviorCardIdentityProjection projection =
                    CharacterTestRuntimeAiLabBehaviorCardIdentityProjector.BuildProjection(
                        controller,
                        editGroup: null,
                        selectedEntryIndex: -1,
                        currentEditSemanticHash: 0L,
                        runtime: result,
                        selectedAgent: selected,
                        hasSelectedAgent: true);
                Assert.That(projection.CompositionWrapperDiagnostic.Name, Is.EqualTo(selected.SelectedActionName));
                Assert.That(projection.CompositionWrapperDiagnostic.Name, Is.Not.EqualTo(result.PrimaryAction.DisplayName));

                CharacterTestRuntimeAiPlannerAgentSnapshot renamed = Agent(20, "renamed-without-binding-token");
                CharacterTestRuntimeAiLabBehaviorCardRuntimeIdentityResolution renamedResult =
                    CharacterTestRuntimeAiLabBehaviorCardIdentityProjector.ResolveRuntime(
                        controller,
                        state,
                        4,
                        renamed,
                        hasSelectedAgent: true);
                Assert.That(renamedResult.CommittedCard, Is.SameAs(result.CommittedCard));
                Assert.That(renamedResult.PrimaryAction.DisplayName, Is.EqualTo(result.PrimaryAction.DisplayName));

                state.BehaviorLogicSlots[0].Agents = new[] { second, LogicAgent(revision, 20, 0) };
                CharacterTestRuntimeAiLabBehaviorCardRuntimeIdentityResolution duplicate =
                    CharacterTestRuntimeAiLabBehaviorCardIdentityProjector.ResolveRuntime(
                        controller,
                        state,
                        4,
                        renamed,
                        hasSelectedAgent: true);
                Assert.That(duplicate.Status, Is.EqualTo(CharacterTestRuntimeAiLabIdentityResolutionStatus.Unresolved));
                Assert.That(duplicate.Reason, Is.EqualTo(CharacterTestRuntimeAiLabIdentityResolutionReason.AgentDuplicate));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(host);
            }
        }

        [Test]
        public void RuntimeResolution_ProjectsCommittedPendingAndTypedRelationships()
        {
            GameObject host = CreateController(out CharacterTestRuntimeAiPlannerDevSceneController controller);
            try
            {
                CharacterTestRuntimeAiLabCandidateRevision revision = RequireCandidate(controller);
                InstallBaseline(controller, revision);
                CharacterTestRuntimeAiLabLogicAgentStateSnapshot logic = LogicAgent(revision, 31, 1, pendingIndex: 0);
                CharacterTestRuntimeAiLabBehaviorCardRuntimeIdentityResolution runtime =
                    CharacterTestRuntimeAiLabBehaviorCardIdentityProjector.ResolveRuntime(
                        controller,
                        State(revision, 0, logic),
                        2,
                        Agent(31),
                        hasSelectedAgent: true);
                Assert.That(
                    runtime.Status,
                    Is.EqualTo(CharacterTestRuntimeAiLabIdentityResolutionStatus.Resolved),
                    FormatRuntimeResolution(runtime));
                Assert.That(runtime.CommittedCard.BindingIndex, Is.EqualTo(1));
                Assert.That(runtime.PendingCard.BindingIndex, Is.EqualTo(0));
                Assert.That(
                    runtime.PrimaryAction.StageId,
                    Is.EqualTo(
                        revision.LogicPrograms[1].Stages[logic.PrimaryActionStageIndex].StageId));

                var matchingEdit = new CharacterTestRuntimeAiLabEditCardIdentity
                {
                    DraftEntryId = runtime.CommittedCard.DraftEntryId,
                    CardDraftId = runtime.CommittedCard.CardDraftId,
                    CurrentSemanticHash = runtime.CommittedCard.CandidateSemanticHash
                };
                Assert.That(
                    CharacterTestRuntimeAiLabBehaviorCardIdentityProjector.ResolveRelationship(matchingEdit, runtime),
                    Is.EqualTo(CharacterTestRuntimeAiLabBehaviorCardIdentityRelationship.TransitioningToDifferentCard));

                runtime.PendingCard = null;
                Assert.That(
                    CharacterTestRuntimeAiLabBehaviorCardIdentityProjector.ResolveRelationship(matchingEdit, runtime),
                    Is.EqualTo(CharacterTestRuntimeAiLabBehaviorCardIdentityRelationship.SameCardSameSemantic));
                matchingEdit.CurrentSemanticHash++;
                Assert.That(
                    CharacterTestRuntimeAiLabBehaviorCardIdentityProjector.ResolveRelationship(matchingEdit, runtime),
                    Is.EqualTo(CharacterTestRuntimeAiLabBehaviorCardIdentityRelationship.SameCardDifferentSemantic));
                matchingEdit.CardDraftId = "different-card";
                matchingEdit.DraftEntryId = "different-entry";
                Assert.That(
                    CharacterTestRuntimeAiLabBehaviorCardIdentityProjector.ResolveRelationship(matchingEdit, runtime),
                    Is.EqualTo(CharacterTestRuntimeAiLabBehaviorCardIdentityRelationship.DifferentCard));

                CreateSamePublishedRevision(out CharacterTestRuntimeAiLabCandidateRevision samePublishedRevision);
                var samePublishedRuntime = new CharacterTestRuntimeAiLabBehaviorCardRuntimeIdentityResolution
                {
                    Status = CharacterTestRuntimeAiLabIdentityResolutionStatus.Resolved,
                    CommittedCard = samePublishedRevision.CardIdentities[0],
                    PendingCard = samePublishedRevision.CardIdentities[1]
                };
                var samePublishedEdit = new CharacterTestRuntimeAiLabEditCardIdentity
                {
                    HasPublishedSource = true,
                    PublishedStableId = samePublishedRevision.CardIdentities[0].PublishedStableId,
                    CardDraftId = samePublishedRevision.CardIdentities[0].CardDraftId,
                    DraftEntryId = samePublishedRevision.CardIdentities[0].DraftEntryId,
                    CurrentSemanticHash = samePublishedRevision.CardIdentities[0].CandidateSemanticHash
                };
                Assert.That(
                    CharacterTestRuntimeAiLabBehaviorCardIdentityProjector.ResolveRelationship(
                        samePublishedEdit,
                        samePublishedRuntime),
                    Is.EqualTo(CharacterTestRuntimeAiLabBehaviorCardIdentityRelationship
                        .TransitioningBetweenBindingsSamePublishedCard));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(host);
            }
        }

        [Test]
        public void RuntimeResolution_ReturnsTypedFailuresWithoutFallback()
        {
            GameObject host = CreateController(out CharacterTestRuntimeAiPlannerDevSceneController controller);
            try
            {
                CharacterTestRuntimeAiLabCandidateRevision revision = RequireCandidate(controller);
                InstallBaseline(controller, revision);
                CharacterTestRuntimeAiPlannerAgentSnapshot selected = Agent(41);
                CharacterTestRuntimeAiLabLogicAgentStateSnapshot logic = LogicAgent(revision, 41, 0);

                CharacterTestRuntimeAiLabBehaviorCardRuntimeIdentityResolution missingHistoryState =
                    CharacterTestRuntimeAiLabBehaviorCardIdentityProjector.ResolveRuntime(
                        controller,
                        state: null,
                        frame: 1,
                        selectedAgent: selected,
                        hasSelectedAgent: true,
                        requireCapturedState: true);
                Assert.That(missingHistoryState.Status,
                    Is.EqualTo(CharacterTestRuntimeAiLabIdentityResolutionStatus.Unresolved));
                Assert.That(missingHistoryState.Reason,
                    Is.EqualTo(CharacterTestRuntimeAiLabIdentityResolutionReason.NoRuntimeState));

                CharacterTestRuntimeAiLabBehaviorCardRuntimeIdentityResolution missingLiveState =
                    CharacterTestRuntimeAiLabBehaviorCardIdentityProjector.ResolveRuntime(
                        controller,
                        state: null,
                        frame: 1,
                        selectedAgent: selected,
                        hasSelectedAgent: true);
                Assert.That(missingLiveState.Status,
                    Is.EqualTo(CharacterTestRuntimeAiLabIdentityResolutionStatus.NotApplicable));

                var missingSlot = CharacterTestRuntimeAiPlannerStateSnapshot.Empty();
                AssertFailure(controller, missingSlot, selected,
                    CharacterTestRuntimeAiLabIdentityResolutionReason.LogicSlotMissing);

                CharacterTestRuntimeAiPlannerStateSnapshot state = State(revision, 0, logic);
                state.BehaviorLogicSlots[0].AuthorityHash++;
                AssertFailure(controller, state, selected,
                    CharacterTestRuntimeAiLabIdentityResolutionReason.AppliedRevisionMissing);

                state = State(revision, 0, logic);
                logic.ActiveBindingIndex = revision.CardIdentities.Count;
                AssertFailure(controller, state, selected,
                    CharacterTestRuntimeAiLabIdentityResolutionReason.BindingIndexOutOfRange);

                logic = LogicAgent(revision, 41, 0);
                state = State(revision, 0, logic);
                logic.ProgramSemanticHash++;
                AssertFailure(controller, state, selected,
                    CharacterTestRuntimeAiLabIdentityResolutionReason.ProgramSemanticMismatch);

                logic = LogicAgent(revision, 41, 0);
                state = State(revision, 0, logic);
                logic.CurrentStageIndex = revision.LogicPrograms[0].Stages.Count;
                AssertFailure(controller, state, selected,
                    CharacterTestRuntimeAiLabIdentityResolutionReason.StageIndexOutOfRange);

                logic = LogicAgent(revision, 41, 0);
                state = State(revision, 0, logic);
                logic.PrimaryActionStageIndex = revision.LogicPrograms[0].Stages.Count;
                AssertFailure(controller, state, selected,
                    CharacterTestRuntimeAiLabIdentityResolutionReason.PrimaryActionStageIndexOutOfRange);

                logic = LogicAgent(revision, 41, 0);
                state = State(revision, 0, logic);
                logic.RuntimeState = 999;
                AssertFailure(controller, state, selected,
                    CharacterTestRuntimeAiLabIdentityResolutionReason.RuntimeStateInvalid);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(host);
            }
        }

        [Test]
        public void AppliedRevisionHistory_ResolvesFrozenFrameAndTruncatesFuture()
        {
            GameObject host = CreateController(out CharacterTestRuntimeAiPlannerDevSceneController controller);
            try
            {
                CharacterTestRuntimeAiLabCandidateRevision firstRevision = RequireCandidate(controller);
                string firstName = firstRevision.CardIdentities[0].DisplayName;
                InstallBaseline(controller, firstRevision);

                CharacterTestRuntimeAiLabBehaviorGroupDraft group = SelectedGroup(controller);
                CharacterTestRuntimeAiLabBehaviorCardDraft card =
                    controller.DraftWorkspace.Cards[group.Entries[0].CardDraftId];
                card.DisplayName = "第二版冻结标题";
                card.LogicPayload = card.LogicPayload.Clone();
                Assert.That(card.LogicPayload.Timelines, Is.Not.Empty);
                Assert.That(card.LogicPayload.Timelines[0].Events, Is.Not.Empty);
                card.LogicPayload.Timelines[0].Events[0].StartFrame++;
                card.CurrentRevision++;
                RebuildCandidate(controller);
                CharacterTestRuntimeAiLabCandidateRevision secondRevision = RequireCandidate(controller);
                Assert.That(secondRevision, Is.Not.SameAs(firstRevision));
                Assert.That(secondRevision.AuthorityHash, Is.Not.EqualTo(firstRevision.AuthorityHash));
                Assert.That(secondRevision.CardIdentities[0].DisplayName, Is.EqualTo("第二版冻结标题"));
                AppliedEvents(controller).Add(new CharacterTestRuntimeAiLabAppliedRevision(10, 0, secondRevision));
                card.DisplayName = "当前未应用草稿标题";

                CharacterTestRuntimeAiLabBehaviorCardRuntimeIdentityResolution first =
                    CharacterTestRuntimeAiLabBehaviorCardIdentityProjector.ResolveRuntime(
                        controller,
                        State(firstRevision, 0, LogicAgent(firstRevision, 51, 0)),
                        5,
                        Agent(51),
                        hasSelectedAgent: true);
                CharacterTestRuntimeAiLabBehaviorCardRuntimeIdentityResolution second =
                    CharacterTestRuntimeAiLabBehaviorCardIdentityProjector.ResolveRuntime(
                        controller,
                        State(secondRevision, 0, LogicAgent(secondRevision, 51, 0)),
                        10,
                        Agent(51),
                        hasSelectedAgent: true);

                Assert.That(first.CommittedCard.DisplayName, Is.EqualTo(firstName));
                Assert.That(second.CommittedCard.DisplayName, Is.EqualTo("第二版冻结标题"));
                Assert.That(second.CommittedCard.DisplayName, Is.Not.EqualTo(card.DisplayName));

                TruncateAppliedRevisionEventsMethod.Invoke(controller, new object[] { 5 });
                CharacterTestRuntimeAiLabBehaviorCardRuntimeIdentityResolution removed =
                    CharacterTestRuntimeAiLabBehaviorCardIdentityProjector.ResolveRuntime(
                        controller,
                        State(secondRevision, 0, LogicAgent(secondRevision, 51, 0)),
                        10,
                        Agent(51),
                        hasSelectedAgent: true);
                Assert.That(removed.Status, Is.EqualTo(CharacterTestRuntimeAiLabIdentityResolutionStatus.Unresolved));
                Assert.That(removed.Reason,
                    Is.EqualTo(CharacterTestRuntimeAiLabIdentityResolutionReason.AppliedRevisionMissing));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(host);
            }
        }

        [Test]
        public void PublishedIdentity_NonCanonicalSourceFailsCandidateWithoutMutatingAppliedRevision()
        {
            GameObject host = CreateController(out CharacterTestRuntimeAiPlannerDevSceneController controller);
            try
            {
                CharacterTestRuntimeAiLabCandidateRevision accepted = RequireCandidate(controller);
                InstallBaseline(controller, accepted);
                ActiveRevisionField.SetValue(controller, accepted);
                CharacterTestRuntimeAiLabBehaviorGroupDraft group = SelectedGroup(controller);
                CharacterTestRuntimeAiLabBehaviorCardDraft card =
                    controller.DraftWorkspace.Cards[group.Entries[0].CardDraftId];
                card.PublishedStableId = " behavior-card.invalid ";
                card.SourcePublishedRevision = 1;
                card.SourcePublishedSemanticHash = accepted.LogicPrograms[0].SemanticHash;
                card.SourcePublishedContentHash = 7;

                CharacterTestRuntimeAiLabBehaviorCardIdentityProjection invalidEdit =
                    CharacterTestRuntimeAiLabBehaviorCardIdentityProjector.BuildProjection(
                        controller,
                        group,
                        selectedEntryIndex: 0,
                        currentEditSemanticHash: accepted.LogicPrograms[0].SemanticHash,
                        runtime: null,
                        selectedAgent: default,
                        hasSelectedAgent: false);
                Assert.That(invalidEdit.EditResolutionStatus,
                    Is.EqualTo(CharacterTestRuntimeAiLabIdentityResolutionStatus.Unresolved));
                Assert.That(invalidEdit.EditResolutionReason,
                    Is.EqualTo(CharacterTestRuntimeAiLabIdentityResolutionReason.PublishedStableIdNonCanonical));

                RebuildCandidate(controller);

                Assert.That(controller.CandidateRevision, Is.Null);
                Assert.That(controller.CandidateState, Is.EqualTo(CharacterTestRuntimeAiLabCandidateState.CompileFailed));
                StringAssert.Contains("not canonical", controller.CandidateDiagnostic);
                Assert.That(controller.ActiveRevision, Is.SameAs(accepted));
                Assert.That(((CharacterTestRuntimeAiLabAppliedRevision)BaselineRevisionField.GetValue(controller)).Revision,
                    Is.SameAs(accepted));
                Assert.That(AppliedEvents(controller), Is.Empty);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(host);
            }
        }

        [Test]
        public void CandidateRevision_BindingCountMismatchFailsWithoutMutatingAppliedRevision()
        {
            GameObject host = CreateController(out CharacterTestRuntimeAiPlannerDevSceneController controller);
            try
            {
                CharacterTestRuntimeAiLabCandidateRevision accepted = RequireCandidate(controller);
                InstallBaseline(controller, accepted);
                ActiveRevisionField.SetValue(controller, accepted);
                CharacterTestRuntimeAiLabBehaviorGroupDraft group = SelectedGroup(controller);
                Assert.That(group.Profile.Entries.Count, Is.GreaterThan(0));
                Assert.That(group.Entries.Count, Is.EqualTo(group.Profile.Entries.Count));
                group.Profile.Entries.RemoveAt(group.Profile.Entries.Count - 1);

                Assert.DoesNotThrow(() => RebuildCandidate(controller));

                Assert.That(controller.CandidateRevision, Is.Null);
                Assert.That(controller.CandidateState, Is.EqualTo(CharacterTestRuntimeAiLabCandidateState.CompileFailed));
                StringAssert.Contains("counts are inconsistent", controller.CandidateDiagnostic);
                Assert.That(controller.ActiveRevision, Is.SameAs(accepted));
                Assert.That(((CharacterTestRuntimeAiLabAppliedRevision)BaselineRevisionField.GetValue(controller)).Revision,
                    Is.SameAs(accepted));
                Assert.That(AppliedEvents(controller), Is.Empty);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(host);
            }
        }

        [Test]
        public void ViewModelSignature_TracksEveryIdentityLayer()
        {
            GameObject host = CreateController(out CharacterTestRuntimeAiPlannerDevSceneController controller);
            try
            {
                CharacterTestRuntimeAiLabCandidateRevision revision = RequireCandidate(controller);
                var view = new CharacterTestFairyGuiRuntimeAiPlannerDevPanelViewModel();
                var projection = new CharacterTestRuntimeAiLabBehaviorCardIdentityProjection
                {
                    EditResolutionStatus = CharacterTestRuntimeAiLabIdentityResolutionStatus.Resolved,
                    EditCard = new CharacterTestRuntimeAiLabEditCardIdentity { DisplayName = "Edit" },
                    CandidateCard = revision.CardIdentities[0],
                    Runtime = new CharacterTestRuntimeAiLabBehaviorCardRuntimeIdentityResolution
                    {
                        Status = CharacterTestRuntimeAiLabIdentityResolutionStatus.Resolved,
                        PrimaryAction = new CharacterTestRuntimeAiLabPrimaryActionIdentity()
                    },
                    CompositionWrapperDiagnostic = new CharacterTestRuntimeAiLabCompositionActionDiagnostic()
                };
                view.BehaviorCardIdentity = projection;
                view.Workspace.ActiveWorkspace = CharacterTestRuntimeAiLabWorkspaceMode.BehaviorCard;

                AssertSignatureChanged(view, () => projection.EditCard.DisplayName = "Edit changed");
                AssertSignatureChanged(view, () => projection.CandidateRevision = 2);
                AssertSignatureChanged(view, () => projection.CandidateCard = revision.CardIdentities[1]);
                AssertSignatureChanged(view, () => projection.Runtime.SelectedEntityId = 77);
                AssertSignatureChanged(view, () => projection.Runtime.RuntimeState =
                    CharacterTestRuntimeAiLabLogicRuntimeState.Transitioning);
                AssertSignatureChanged(view, () => projection.Runtime.BindingReleasePending = true);
                AssertSignatureChanged(view, () => projection.Runtime.CommittedCard = revision.CardIdentities[0]);
                AssertSignatureChanged(view, () => projection.Runtime.PendingCard = revision.CardIdentities[1]);
                AssertSignatureChanged(view, () => projection.Runtime.PrimaryAction.DisplayName = "Dash");
                AssertSignatureChanged(view, () => projection.CompositionWrapperDiagnostic.Name = "Wait wrapper");
                AssertSignatureChanged(view, () => projection.Relationship =
                    CharacterTestRuntimeAiLabBehaviorCardIdentityRelationship.TransitioningToDifferentCard);
                AssertSignatureChanged(view, () => projection.Runtime.Status =
                    CharacterTestRuntimeAiLabIdentityResolutionStatus.Unresolved);
                AssertSignatureChanged(view, () => projection.Runtime.Reason =
                    CharacterTestRuntimeAiLabIdentityResolutionReason.AppliedRevisionMissing);
                AssertSignatureChanged(view, () => projection.EditResolutionStatus =
                    CharacterTestRuntimeAiLabIdentityResolutionStatus.Unresolved);
                AssertSignatureChanged(view, () => projection.EditResolutionReason =
                    CharacterTestRuntimeAiLabIdentityResolutionReason.EditCardMissing);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(host);
            }
        }

        [Test]
        public void ViewModelSignature_TracksProjectCardMetadataInspectorFields()
        {
            var view = new CharacterTestFairyGuiRuntimeAiPlannerDevPanelViewModel();
            view.Workspace.ActiveWorkspace = CharacterTestRuntimeAiLabWorkspaceMode.BehaviorCard;
            AssertSignatureChanged(view, () => view.ProjectCardDisplayName = "修改后的名称");
            AssertSignatureChanged(view, () => view.ProjectCardDescription = "修改后的描述");
            AssertSignatureChanged(view, () => view.ProjectCardTags = "训练,木桩");
        }

        [TestCase(true, false, (int)CharacterTestRuntimeAiLabPublishUxState.GameAvailable, "已同步")]
        [TestCase(true, true, (int)CharacterTestRuntimeAiLabPublishUxState.HasUnpublishedChanges, "有未发布修改")]
        [TestCase(false, false, (int)CharacterTestRuntimeAiLabPublishUxState.WorkspaceOnly, "尚未发布")]
        [TestCase(false, true, (int)CharacterTestRuntimeAiLabPublishUxState.WorkspaceOnly, "尚未发布")]
        public void AuthoringUxPublishState_UsesPublishedSourceAndCanonicalDirty(
            bool hasPublishedSource,
            bool hasUnpublishedChanges,
            int expected,
            string expectedText)
        {
            CharacterTestRuntimeAiLabPublishUxState state =
                CharacterTestRuntimeAiLabAuthoringUxStatusFormatter.ResolvePublishState(
                    hasPublishedSource,
                    hasUnpublishedChanges);

            Assert.That((int)state, Is.EqualTo(expected));
            Assert.That(
                CharacterTestRuntimeAiLabAuthoringUxStatusFormatter.FormatPublishState(state),
                Is.EqualTo(expectedText));
        }

        [TestCase(false, false, false, "", (int)CharacterTestRuntimeAiLabDraftSaveUxState.Unavailable, "")]
        [TestCase(true, false, true, "", (int)CharacterTestRuntimeAiLabDraftSaveUxState.Saving, "草稿待自动保存")]
        [TestCase(true, false, false, "Runtime AI Lab draft saved.", (int)CharacterTestRuntimeAiLabDraftSaveUxState.Saved, "草稿已保存")]
        [TestCase(true, false, false, "Runtime AI Lab draft save failed: checkpoint", (int)CharacterTestRuntimeAiLabDraftSaveUxState.SaveFailed, "草稿保存失败")]
        [TestCase(true, false, true, "Runtime AI Lab draft save failed: disk", (int)CharacterTestRuntimeAiLabDraftSaveUxState.SaveFailed, "草稿保存失败")]
        [TestCase(true, true, true, "Runtime AI Lab workspace is read-only and was not saved.", (int)CharacterTestRuntimeAiLabDraftSaveUxState.ReadOnly, "只读")]
        public void AuthoringUxDraftSaveState_CoversAutosaveAndReadOnly(
            bool available,
            bool readOnly,
            bool dirty,
            string persistenceStatus,
            int expected,
            string expectedText)
        {
            CharacterTestRuntimeAiLabDraftSaveUxState state =
                CharacterTestRuntimeAiLabAuthoringUxStatusFormatter.ResolveDraftSaveState(
                    available,
                    readOnly,
                    dirty,
                    persistenceStatus);

            Assert.That((int)state, Is.EqualTo(expected));
            Assert.That(
                CharacterTestRuntimeAiLabAuthoringUxStatusFormatter.FormatDraftSaveState(state),
                Is.EqualTo(expectedText));
        }

        [TestCase(false, false, false, false, false, true, (int)CharacterTestRuntimeAiLabTestSourceUxState.NotStarted, "尚未开始测试")]
        [TestCase(true, false, false, false, false, true, (int)CharacterTestRuntimeAiLabTestSourceUxState.Unknown, "当前测试来源未知")]
        [TestCase(true, true, true, true, true, true, (int)CharacterTestRuntimeAiLabTestSourceUxState.PublishedVersion, "正式版本")]
        [TestCase(true, true, true, true, true, false, (int)CharacterTestRuntimeAiLabTestSourceUxState.UsingCurrentChanges, "正在使用本次修改")]
        [TestCase(true, true, false, true, true, false, (int)CharacterTestRuntimeAiLabTestSourceUxState.BehindCurrentChanges, "当前测试落后于修改")]
        [TestCase(true, true, true, false, true, false, (int)CharacterTestRuntimeAiLabTestSourceUxState.BehindCurrentChanges, "当前测试落后于修改")]
        [TestCase(true, true, true, true, false, false, (int)CharacterTestRuntimeAiLabTestSourceUxState.BehindCurrentChanges, "当前测试落后于修改")]
        public void AuthoringUxTestSourceState_CoversPublishedCandidateAndRuntime(
            bool sessionStarted,
            bool runtimeIdentityAvailable,
            bool candidateMatchesRuntime,
            bool currentMatchesCandidate,
            bool currentCardMatchesCandidate,
            bool publishedContentPristine,
            int expected,
            string expectedText)
        {
            CharacterTestRuntimeAiLabTestSourceUxState state =
                CharacterTestRuntimeAiLabAuthoringUxStatusFormatter.ResolveTestSourceState(
                    sessionStarted,
                    runtimeIdentityAvailable,
                    candidateMatchesRuntime,
                    currentMatchesCandidate,
                    currentCardMatchesCandidate,
                    publishedContentPristine);

            Assert.That((int)state, Is.EqualTo(expected));
            Assert.That(
                CharacterTestRuntimeAiLabAuthoringUxStatusFormatter.FormatTestSourceState(state),
                Is.EqualTo(expectedText));
        }

        [Test]
        public void AuthoringUxValidation_RemainsOrthogonalToPublishState()
        {
            CharacterTestRuntimeAiLabPublishUxState publishState =
                CharacterTestRuntimeAiLabAuthoringUxStatusFormatter.ResolvePublishState(
                    hasPublishedSource: true,
                    hasUnpublishedChanges: false);
            var status = new CharacterTestRuntimeAiLabAuthoringUxStatusViewModel
            {
                BehaviorCardPublishState = publishState,
                BehaviorCardHasValidationProblems = true,
                BehaviorCardPublishStatusText =
                    CharacterTestRuntimeAiLabAuthoringUxStatusFormatter.FormatPublishState(
                        publishState),
                BehaviorCardValidationStatusText =
                    CharacterTestRuntimeAiLabAuthoringUxStatusFormatter.FormatValidationState(true)
            };

            Assert.That(status.BehaviorCardPublishState,
                Is.EqualTo(CharacterTestRuntimeAiLabPublishUxState.GameAvailable));
            Assert.That(status.BehaviorCardPublishStatusText, Is.EqualTo("已同步"));
            Assert.That(status.BehaviorCardHasValidationProblems, Is.True);
            Assert.That(status.BehaviorCardValidationStatusText, Is.EqualTo("存在需要处理的问题"));
        }

        [Test]
        public void CanonicalCompare_CardRemainsDirtyAfterDraftSaveAndPersistenceReopen()
        {
            ProjectAuthoringFixture fixture = CreateProjectAuthoringFixture();
            Assert.That(fixture.Authoring.TryHasUnpublishedCardChanges(
                fixture.Card,
                fixture.References,
                out bool pristine,
                out string pristineDiagnostic), Is.True, pristineDiagnostic);
            Assert.That(pristine, Is.False);

            fixture.Card.DisplayName += " 修改";
            fixture.Card.CurrentRevision++;
            fixture.Workspace.CurrentRevision++;
            fixture.Workspace.LastSavedRevision = fixture.Workspace.CurrentRevision;
            Assert.That(fixture.Authoring.TryHasUnpublishedCardChanges(
                fixture.Card,
                fixture.References,
                out bool savedDirty,
                out string savedDiagnostic), Is.True, savedDiagnostic);
            Assert.That(savedDirty, Is.True,
                "Saving a draft checkpoint must not make unpublished content look Published.");

            string root = Path.Combine(
                Path.GetTempPath(),
                "mx-runtime-ai-lab-ux01-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            try
            {
                var persistence = new CharacterTestRuntimeAiLabDraftPersistence(root);
                persistence.AdoptWorkspace(fixture.Workspace);
                Assert.That(persistence.Flush(), Is.True, persistence.LastStatus);
                Assert.That(persistence.TryLoad(
                    fixture.Workspace.WorkspaceId,
                    out CharacterTestRuntimeAiLabDraftWorkspace reopened,
                    out string loadMessage), Is.True, loadMessage);
                Assert.That(reopened.Cards.TryGetValue(
                    fixture.Card.CardDraftId,
                    out CharacterTestRuntimeAiLabBehaviorCardDraft reopenedCard), Is.True);
                Assert.That(fixture.Authoring.TryHasUnpublishedCardChanges(
                    reopenedCard,
                    fixture.References,
                    out bool reopenedDirty,
                    out string reopenedDiagnostic), Is.True, reopenedDiagnostic);
                Assert.That(reopenedDirty, Is.True,
                    "A persisted unpublished draft must remain dirty after reopening.");
            }
            finally
            {
                if (Directory.Exists(root))
                    Directory.Delete(root, recursive: true);
            }
        }

        [Test]
        public void CanonicalCompare_GroupUsesFormalCandidateContentHash()
        {
            ProjectAuthoringFixture fixture = CreateProjectAuthoringFixture();
            Assert.That(fixture.Authoring.TryBuildGroupCandidateForComparison(
                fixture.Group,
                fixture.Workspace.Cards,
                out CharacterTestRuntimeAiPlannerBehaviorGroupRevision source,
                out CharacterTestRuntimeAiPlannerBehaviorGroupRevision candidate,
                out string candidateDiagnostic), Is.True, candidateDiagnostic);
            Assert.That(
                candidate.ContentHash,
                Is.EqualTo(source.ContentHash),
                FormatGroupCandidateDifference(source, candidate));
            Assert.That(fixture.Authoring.TryHasUnpublishedGroupChanges(
                fixture.Group,
                fixture.Workspace.Cards,
                out bool pristine,
                out string pristineDiagnostic), Is.True, pristineDiagnostic);
            Assert.That(pristine, Is.False);

            fixture.Group.DisplayName += " 修改";
            fixture.Group.CurrentRevision++;
            fixture.Workspace.CurrentRevision++;
            Assert.That(fixture.Authoring.TryHasUnpublishedGroupChanges(
                fixture.Group,
                fixture.Workspace.Cards,
                out bool modified,
                out string modifiedDiagnostic), Is.True, modifiedDiagnostic);
            Assert.That(modified, Is.True);
        }

        [Test]
        public void CanonicalCompare_NewDuplicateAuthoredEntryKeepsDraftIdentity()
        {
            ProjectAuthoringFixture fixture = CreateProjectAuthoringFixture();
            CharacterTestRuntimeAiLabBehaviorEntryDraft original =
                fixture.Group.Entries[fixture.EntryIndex];
            var duplicate = new CharacterTestRuntimeAiLabBehaviorEntryDraft(
                entryId: null,
                cardDraftId: original.CardDraftId,
                behaviorId: original.BehaviorId,
                order: (fixture.Group.Entries.Count + 1) * 10,
                disabled: original.Disabled,
                runtimeEntry: original.RuntimeEntry);
            fixture.Group.Entries.Add(duplicate);
            fixture.Group.NormalizeOrders();

            Assert.That(Guid.TryParse(duplicate.EntryId, out _), Is.True);
            Assert.That(fixture.Authoring.TryBuildGroupCandidateForComparison(
                fixture.Group,
                fixture.Workspace.Cards,
                out CharacterTestRuntimeAiPlannerBehaviorGroupRevision source,
                out CharacterTestRuntimeAiPlannerBehaviorGroupRevision candidate,
                out string diagnostic), Is.True, diagnostic);
            Assert.That(CountGroupCardReferences(source, duplicate.EntryId), Is.Zero);
            Assert.That(CountGroupCardReferences(candidate, duplicate.EntryId), Is.EqualTo(1),
                "A duplicate authored Entry must keep its Draft GUID after all matching source slots are consumed.");
            for (int i = 0; i < source.Cards.Count; i++)
            {
                Assert.That(
                    CountGroupCardReferences(candidate, source.Cards[i].EntryId),
                    Is.EqualTo(1),
                    "A duplicate must not steal or duplicate source EntryId "
                    + source.Cards[i].EntryId);
            }
        }

        private static string FormatGroupCandidateDifference(
            CharacterTestRuntimeAiPlannerBehaviorGroupRevision source,
            CharacterTestRuntimeAiPlannerBehaviorGroupRevision candidate)
        {
            const string CatalogId = "runtime-ai-lab.ux01.group-comparison";
            string sourceJson = CharacterTestRuntimeAiPlannerBehaviorGroupCatalogJson.ToJson(
                new CharacterTestRuntimeAiPlannerBehaviorGroupCatalog(
                    CatalogId,
                    1,
                    new[] { source }));
            string candidateJson = CharacterTestRuntimeAiPlannerBehaviorGroupCatalogJson.ToJson(
                new CharacterTestRuntimeAiPlannerBehaviorGroupCatalog(
                    CatalogId,
                    1,
                    new[] { candidate }));
            return "Canonical group candidate differs from its untouched Published source."
                + " sourceHash=" + (source?.ContentHash ?? 0)
                + ", candidateHash=" + (candidate?.ContentHash ?? 0)
                + "\nSOURCE:\n" + sourceJson
                + "\nCANDIDATE:\n" + candidateJson;
        }

        private static int CountGroupCardReferences(
            CharacterTestRuntimeAiPlannerBehaviorGroupRevision group,
            string entryId)
        {
            int count = 0;
            for (int i = 0; group != null && i < group.Cards.Count; i++)
            {
                if (string.Equals(group.Cards[i].EntryId, entryId, StringComparison.Ordinal))
                    count++;
            }
            return count;
        }

        [Test]
        public void CanonicalCompare_DoesNotMutateCandidateAppliedHistoryOrCatalogState()
        {
            GameObject host = CreateProjectBackedController(
                out CharacterTestRuntimeAiPlannerDevSceneController controller,
                out ProjectAuthoringFixture fixture);
            try
            {
                CharacterTestRuntimeAiLabCandidateRevision candidate = RequireCandidate(controller);
                InstallBaseline(controller, candidate);
                ActiveRevisionField.SetValue(controller, candidate);
                CharacterTestRuntimeAiPlannerProjectCatalogState catalogState = fixture.Authoring.State;

                BuildProjectAuthoringView(controller, fixture.EntryIndex);

                Assert.That(controller.CandidateRevision, Is.SameAs(candidate));
                Assert.That(controller.ActiveRevision, Is.SameAs(candidate));
                Assert.That(
                    ((CharacterTestRuntimeAiLabAppliedRevision)
                        BaselineRevisionField.GetValue(controller)).Revision,
                    Is.SameAs(candidate));
                Assert.That(AppliedEvents(controller), Is.Empty);
                Assert.That(fixture.Authoring.State, Is.SameAs(catalogState));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(host);
            }
        }

        [Test]
        public void AuthoringUxBuilder_ProjectsCanonicalPublishMatrixAndRetainedRefresh()
        {
            GameObject host = null;
            CharacterTestRuntimeAiPlannerDevSceneController controller = null;
            ProjectAuthoringFixture fixture = null;
            string stage = "controller-fixture";
            try
            {
                host = CreateProjectBackedController(out controller, out fixture);
                Assert.That(host, Is.Not.Null, "matrix stage=controller-fixture: host is null");
                Assert.That(controller, Is.Not.Null, "matrix stage=controller-fixture: controller is null");
                AssertProjectAuthoringFixture(fixture, "matrix stage=controller-fixture");
                stage = "view.pristine";
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelViewModel view =
                    BuildProjectAuthoringView(controller, fixture.EntryIndex);
                Assert.That(view, Is.Not.Null, "matrix stage=view.pristine: ViewModel is null");
                Assert.That(view.AuthoringStatus, Is.Not.Null,
                    "matrix stage=view.pristine: AuthoringStatus is null");
                Assert.That(view.AuthoringStatus.BehaviorGroupPublishState,
                    Is.EqualTo(CharacterTestRuntimeAiLabPublishUxState.GameAvailable));
                Assert.That(view.AuthoringStatus.BehaviorCardPublishState,
                    Is.EqualTo(CharacterTestRuntimeAiLabPublishUxState.GameAvailable));
                Assert.That(view.AuthoringAdvancedDiagnosticText, Is.Empty,
                    "Advanced diagnostics must stay cold while details are collapsed.");
                string pristineSignature =
                    CharacterTestFairyGuiRuntimeAiPlannerDevPanelViewModelSignature.Create(view);
                Assert.That(pristineSignature, Is.Not.Null,
                    "matrix stage=view.pristine: signature is null");

                stage = "view.group-modified";
                string originalGroupName = fixture.Group.DisplayName;
                fixture.Group.DisplayName += " 修改";
                fixture.Group.CurrentRevision++;
                fixture.Workspace.CurrentRevision++;
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelViewModel groupModified =
                    BuildProjectAuthoringView(controller, fixture.EntryIndex, view);
                Assert.That(groupModified, Is.Not.Null,
                    "matrix stage=view.group-modified: ViewModel is null");
                Assert.That(groupModified.AuthoringStatus.BehaviorGroupPublishState,
                    Is.EqualTo(CharacterTestRuntimeAiLabPublishUxState.HasUnpublishedChanges));
                stage = "view.group-restored";
                fixture.Group.DisplayName = originalGroupName;
                fixture.Group.CurrentRevision++;
                fixture.Workspace.CurrentRevision++;
                view = BuildProjectAuthoringView(controller, fixture.EntryIndex, groupModified);
                Assert.That(view, Is.Not.Null,
                    "matrix stage=view.group-restored: ViewModel is null");
                Assert.That(view.AuthoringStatus.BehaviorGroupPublishState,
                    Is.EqualTo(CharacterTestRuntimeAiLabPublishUxState.GameAvailable));

                stage = "view.card-modified";
                fixture.Card.DisplayName += " 修改";
                fixture.Card.CurrentRevision++;
                fixture.Workspace.CurrentRevision++;
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelViewModel modified =
                    BuildProjectAuthoringView(
                        controller,
                        fixture.EntryIndex,
                        view);
                Assert.That(modified, Is.Not.Null,
                    "matrix stage=view.card-modified: ViewModel is null");
                Assert.That(modified, Is.SameAs(view));
                Assert.That(modified.AuthoringStatus.BehaviorCardPublishState,
                    Is.EqualTo(CharacterTestRuntimeAiLabPublishUxState.HasUnpublishedChanges));
                Assert.That(modified.AuthoringStatus.BehaviorCardHasValidationProblems, Is.False);
                stage = "checkpoint.save";
                Assert.That(SetBehaviorRecipeSaveCheckpointMethod, Is.Not.Null,
                    "matrix stage=checkpoint.save: MethodInfo is null");
                SetBehaviorRecipeSaveCheckpointMethod.Invoke(
                    controller,
                    new object[] { fixture.Workspace, fixture.Card.CardDraftId });
                string modifiedSignature =
                    CharacterTestFairyGuiRuntimeAiPlannerDevPanelViewModelSignature.Create(modified);
                Assert.That(modifiedSignature, Is.Not.EqualTo(pristineSignature));

                stage = "persistence.resolve";
                Assert.That(DraftPersistenceField, Is.Not.Null,
                    "matrix stage=persistence.resolve: persistence FieldInfo is null");
                Assert.That(DraftPersistenceLastStatusField, Is.Not.Null,
                    "matrix stage=persistence.resolve: status FieldInfo is null");
                var persistence = (CharacterTestRuntimeAiLabDraftPersistence)
                    DraftPersistenceField.GetValue(controller);
                Assert.That(persistence, Is.Not.Null,
                    "matrix stage=persistence.resolve: persistence instance is null");
                stage = "view.save-failed";
                DraftPersistenceLastStatusField.SetValue(
                    persistence,
                    "Runtime AI Lab draft save failed: disk");
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelViewModel failedSave =
                    BuildProjectAuthoringView(
                        controller,
                        fixture.EntryIndex,
                        modified);
                Assert.That(failedSave, Is.Not.Null,
                    "matrix stage=view.save-failed: ViewModel is null");
                Assert.That(failedSave.AuthoringStatus.DraftSaveState,
                    Is.EqualTo(CharacterTestRuntimeAiLabDraftSaveUxState.SaveFailed));
                string failedSaveSignature =
                    CharacterTestFairyGuiRuntimeAiPlannerDevPanelViewModelSignature.Create(failedSave);
                Assert.That(failedSaveSignature, Is.Not.EqualTo(modifiedSignature));

                stage = "view.saved";
                fixture.Workspace.LastSavedRevision = fixture.Workspace.CurrentRevision;
                DraftPersistenceLastStatusField.SetValue(
                    persistence,
                    "Runtime AI Lab draft saved.");
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelViewModel saved =
                    BuildProjectAuthoringView(
                        controller,
                        fixture.EntryIndex,
                        modified);
                Assert.That(saved, Is.Not.Null, "matrix stage=view.saved: ViewModel is null");
                Assert.That(saved.AuthoringStatus.DraftSaveState,
                    Is.EqualTo(CharacterTestRuntimeAiLabDraftSaveUxState.Saved));
                Assert.That(saved.AuthoringStatus.BehaviorCardPublishState,
                    Is.EqualTo(CharacterTestRuntimeAiLabPublishUxState.HasUnpublishedChanges));
                Assert.That(
                    CharacterTestFairyGuiRuntimeAiPlannerDevPanelViewModelSignature.Create(saved),
                    Is.Not.EqualTo(failedSaveSignature),
                    "A retained Host ViewModel must rebind when autosave reaches its checkpoint.");
                string savedSignature =
                    CharacterTestFairyGuiRuntimeAiPlannerDevPanelViewModelSignature.Create(saved);
                stage = "view.read-only";
                fixture.Workspace.IsReadOnly = true;
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelViewModel readOnly =
                    BuildProjectAuthoringView(controller, fixture.EntryIndex, saved);
                Assert.That(readOnly, Is.Not.Null,
                    "matrix stage=view.read-only: ViewModel is null");
                Assert.That(readOnly.AuthoringStatus.DraftSaveState,
                    Is.EqualTo(CharacterTestRuntimeAiLabDraftSaveUxState.ReadOnly));
                Assert.That(
                    CharacterTestFairyGuiRuntimeAiPlannerDevPanelViewModelSignature.Create(readOnly),
                    Is.Not.EqualTo(savedSignature));
                fixture.Workspace.IsReadOnly = false;

                stage = "view.validation-error";
                Assert.That(fixture.Card.LogicPayload, Is.Not.Null,
                    "matrix stage=view.validation-error: source logic is null");
                CharacterTestRuntimeAiLabBehaviorCardLogicPayload validLogic =
                    fixture.Card.LogicPayload.Clone();
                Assert.That(validLogic, Is.Not.Null,
                    "matrix stage=view.validation-error: cloned logic is null");
                fixture.Card.LogicPayload.Timelines.Clear();
                fixture.Card.CurrentRevision++;
                fixture.Workspace.CurrentRevision++;
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelViewModel invalid =
                    BuildProjectAuthoringView(
                        controller,
                        fixture.EntryIndex,
                        readOnly);
                Assert.That(invalid, Is.Not.Null,
                    "matrix stage=view.validation-error: ViewModel is null");
                Assert.That(invalid.AuthoringStatus.BehaviorCardPublishState,
                    Is.EqualTo(CharacterTestRuntimeAiLabPublishUxState.HasUnpublishedChanges));
                Assert.That(invalid.AuthoringStatus.BehaviorCardHasValidationProblems, Is.True,
                    "Validation must remain orthogonal to canonical publish dirty.");

                stage = "publish.create-revision";
                fixture.Card.LogicPayload = validLogic;
                fixture.Card.CurrentRevision++;
                fixture.Workspace.CurrentRevision++;
                CharacterTestRuntimeAiPlannerPublishedBehaviorCard adopted =
                    CreateNextPublishedRevision(fixture);
                Assert.That(adopted, Is.Not.Null,
                    "matrix stage=publish.create-revision: Published card is null");
                stage = "publish.catalog";
                CharacterTestRuntimeAiPlannerProjectCatalogState nextState =
                    AddPublishedCardRevision(fixture, adopted);
                Assert.That(nextState, Is.Not.Null,
                    "matrix stage=publish.catalog: next project state is null");
                Assert.That(nextState.Success, Is.True,
                    "matrix stage=publish.catalog: " + nextState.Diagnostic);
                stage = "publish.adopt";
                Assert.That(fixture.Workspace.TryAdoptPublishedRevision(
                    CharacterTestArenaMatchConfig.Team1Id,
                    0,
                    fixture.EntryIndex,
                    adopted,
                    out string adoptionMessage), Is.True, adoptionMessage);
                fixture.Config.UpdateProjectCatalogState(nextState);
                var nextAuthoring =
                    new CharacterTestRuntimeAiPlannerProjectCatalogAuthoringSession(nextState);
                Assert.That(nextAuthoring, Is.Not.Null,
                    "matrix stage=publish.adopt: next authoring session is null");
                Assert.That(ProjectCatalogAuthoringField, Is.Not.Null,
                    "matrix stage=publish.adopt: authoring FieldInfo is null");
                ProjectCatalogAuthoringField.SetValue(
                    controller,
                    nextAuthoring);
                fixture.Workspace.LastSavedRevision = fixture.Workspace.CurrentRevision;
                stage = "view.adopted";
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelViewModel adoptedView =
                    BuildProjectAuthoringView(
                        controller,
                        fixture.EntryIndex,
                        invalid);
                Assert.That(adoptedView, Is.Not.Null,
                    "matrix stage=view.adopted: ViewModel is null");
                Assert.That(adoptedView.AuthoringStatus.BehaviorCardPublishState,
                    Is.EqualTo(CharacterTestRuntimeAiLabPublishUxState.GameAvailable));
            }
            catch (NullReferenceException exception)
            {
                throw new InvalidOperationException(
                    "Authoring UX matrix fixture threw NullReferenceException at stage=" + stage
                    + Environment.NewLine + exception,
                    exception);
            }
            finally
            {
                if (host != null)
                    UnityEngine.Object.DestroyImmediate(host);
            }
        }

        [Test]
        public void AuthoringUxBuilder_RealSameNameGroupsKeepDraftIdentityAndStateSeparate()
        {
            GameObject host = CreateProjectBackedController(
                out CharacterTestRuntimeAiPlannerDevSceneController controller,
                out ProjectAuthoringFixture fixture);
            try
            {
                string originalGroupDraftId = fixture.Group.GroupDraftId;
                string displayName = fixture.Group.DisplayName;
                Assert.That(fixture.Workspace.TryCreateLocalGroupCopyForSlot(
                    CharacterTestArenaMatchConfig.Team1Id,
                    0,
                    out CharacterTestRuntimeAiLabBehaviorGroupDraft copy,
                    out string copyMessage), Is.True, copyMessage);
                copy.DisplayName = displayName;
                RebuildCandidate(controller);
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelViewModel local =
                    BuildProjectAuthoringView(controller, fixture.EntryIndex);
                Assert.That(local.WorkbenchTitleText, Is.EqualTo(displayName));
                Assert.That(local.WorkbenchGroupIdentity, Is.EqualTo(copy.GroupDraftId));
                Assert.That(local.AuthoringStatus.BehaviorGroupPublishState,
                    Is.EqualTo(CharacterTestRuntimeAiLabPublishUxState.WorkspaceOnly));

                Assert.That(fixture.Workspace.TryRestoreGroupForSlot(
                    CharacterTestArenaMatchConfig.Team1Id,
                    0,
                    copy.GroupDraftId,
                    originalGroupDraftId), Is.True);
                RebuildCandidate(controller);
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelViewModel published =
                    BuildProjectAuthoringView(controller, fixture.EntryIndex, local);
                Assert.That(published.WorkbenchTitleText, Is.EqualTo(displayName));
                Assert.That(published.WorkbenchGroupIdentity, Is.EqualTo(originalGroupDraftId));
                Assert.That(published.AuthoringStatus.BehaviorGroupPublishState,
                    Is.EqualTo(CharacterTestRuntimeAiLabPublishUxState.GameAvailable));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(host);
            }
        }

        [Test]
        public void AuthoringUxBuildSourceRevision_TracksActiveCandidateIdentity()
        {
            GameObject host = CreateProjectBackedController(
                out CharacterTestRuntimeAiPlannerDevSceneController controller,
                out ProjectAuthoringFixture fixture);
            try
            {
                long before = CharacterTestFairyGuiRuntimeAiPlannerDevPanelViewModelBuilder
                    .ComputeBehaviorCardBuildSourceRevision(
                        controller,
                        commandStatus: null,
                        textProvider: null,
                        selectedWorkbenchEntryIndex: fixture.EntryIndex,
                        selectedCompiledStepIndex: 0,
                        selectedLogicElementKind: CharacterTestRuntimeAiLabLogicElementKind.Stage,
                        selectedLogicElementIndex: -1,
                        behaviorCardLibrary: null,
                        behaviorCardLibraryState: null);
                Assert.That(controller.CandidateRevision, Is.Not.Null, controller.CandidateDiagnostic);
                ActiveRevisionField.SetValue(controller, controller.CandidateRevision);
                long after = CharacterTestFairyGuiRuntimeAiPlannerDevPanelViewModelBuilder
                    .ComputeBehaviorCardBuildSourceRevision(
                        controller,
                        commandStatus: null,
                        textProvider: null,
                        selectedWorkbenchEntryIndex: fixture.EntryIndex,
                        selectedCompiledStepIndex: 0,
                        selectedLogicElementKind: CharacterTestRuntimeAiLabLogicElementKind.Stage,
                        selectedLogicElementIndex: -1,
                        behaviorCardLibrary: null,
                        behaviorCardLibraryState: null);
                Assert.That(after, Is.Not.EqualTo(before));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(host);
            }
        }

#if UNITY_EDITOR
        [Test]
        public void AuthoringUxBuilder_WorkspaceRoleFailureIsRetainedAndFailClosed()
        {
            GameObject host = CreateController(out CharacterTestRuntimeAiPlannerDevSceneController controller);
            try
            {
                Assert.That(LastAuthoringWorkspaceRoleResultField, Is.Not.Null);
                DraftWorkspaceField.SetValue(controller, null);
                LastAuthoringWorkspaceRoleResultField.SetValue(
                    controller,
                    new CharacterTestRuntimeAiLabAuthoringWorkspaceRoleResult(
                        CharacterTestRuntimeAiLabAuthoringWorkspaceRoleStatus.MainUnavailable,
                        CharacterTestRuntimeAiLabAuthoringWorkspaceRole.Main,
                        null,
                        "Main workspace missing"));
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelViewModel view = null;
                Assert.DoesNotThrow(() => view = BuildProjectAuthoringView(
                    controller,
                    0,
                    allowUnavailableWorkspace: true));
                Assert.That(view.CanEditWorkbench, Is.False);
                Assert.That(view.CanSaveDraft, Is.False);
                Assert.That(view.CanApplyDraft, Is.False);
                Assert.That(view.CanPublishDraft, Is.False);
                StringAssert.Contains("MainUnavailable", view.WorkbenchDetailText);
                string firstSignature =
                    CharacterTestFairyGuiRuntimeAiPlannerDevPanelViewModelSignature.Create(view);

                LastAuthoringWorkspaceRoleResultField.SetValue(
                    controller,
                    new CharacterTestRuntimeAiLabAuthoringWorkspaceRoleResult(
                        CharacterTestRuntimeAiLabAuthoringWorkspaceRoleStatus.RegistryInvalid,
                        CharacterTestRuntimeAiLabAuthoringWorkspaceRole.Main,
                        null,
                        "Registry invalid"));
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelViewModel refreshed =
                    BuildProjectAuthoringView(
                        controller,
                        0,
                        view,
                        allowUnavailableWorkspace: true);
                Assert.That(refreshed, Is.SameAs(view));
                StringAssert.Contains("RegistryInvalid", refreshed.WorkbenchDetailText);
                Assert.That(
                    CharacterTestFairyGuiRuntimeAiPlannerDevPanelViewModelSignature.Create(refreshed),
                    Is.Not.EqualTo(firstSignature));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(host);
            }
        }
#endif

        [Test]
        public void AuthoringUxBuilder_DuplicateCardNamesKeepStableIdentityScopedStates()
        {
            GameObject host = CreateController(out CharacterTestRuntimeAiPlannerDevSceneController controller);
            try
            {
                CharacterTestRuntimeAiLabBehaviorGroupDraft group = SelectedGroup(controller);
                Assert.That(group.Entries.Count, Is.GreaterThanOrEqualTo(2));
                CharacterTestRuntimeAiLabBehaviorCardDraft first =
                    controller.DraftWorkspace.Cards[group.Entries[0].CardDraftId];
                CharacterTestRuntimeAiLabBehaviorCardDraft second =
                    controller.DraftWorkspace.Cards[group.Entries[1].CardDraftId];
                first.DisplayName = "同名行为卡";
                second.DisplayName = "同名行为卡";
                first.PublishedStableId = string.Empty;
                first.SourcePublishedRevision = 0;
                second.PublishedStableId = string.Empty;
                second.SourcePublishedRevision = 0;

                CharacterTestFairyGuiRuntimeAiPlannerDevPanelViewModel view =
                    CharacterTestFairyGuiRuntimeAiPlannerDevPanelViewModelBuilder.Build(
                        controller,
                        commandStatus: null,
                        textProvider: null,
                        CharacterTestRuntimeAiLabWorkspaceMode.BehaviorGroup,
                        selectedWorkbenchEntryIndex: 0);
                CharacterTestRuntimeAiLabWorkbenchRowViewModel firstRow = null;
                CharacterTestRuntimeAiLabWorkbenchRowViewModel secondRow = null;
                for (int i = 0; i < view.WorkbenchRows.Length; i++)
                {
                    CharacterTestRuntimeAiLabWorkbenchRowViewModel row = view.WorkbenchRows[i];
                    if (row?.Visible != true)
                        continue;
                    if (row.EntryIndex == 0)
                        firstRow = row;
                    else if (row.EntryIndex == 1)
                        secondRow = row;
                }

                Assert.That(firstRow, Is.Not.Null);
                Assert.That(secondRow, Is.Not.Null);
                Assert.That(firstRow.TitleText, Is.EqualTo(secondRow.TitleText));
                Assert.That(firstRow.CardDraftId, Is.Not.Empty);
                Assert.That(secondRow.CardDraftId, Is.Not.Empty);
                Assert.That(firstRow.CardDraftId, Is.Not.EqualTo(secondRow.CardDraftId));
                Assert.That(firstRow.PublishedStableId, Is.Empty);
                Assert.That(secondRow.PublishedStableId, Is.Empty);
                Assert.That(firstRow.PublishState,
                    Is.EqualTo(CharacterTestRuntimeAiLabPublishUxState.WorkspaceOnly));
                Assert.That(secondRow.PublishState,
                    Is.EqualTo(CharacterTestRuntimeAiLabPublishUxState.WorkspaceOnly));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(host);
            }
        }

        [Test]
        public void ViewModelSignature_TracksEveryAuthoringUxStateAndStableIdentity()
        {
            var view = new CharacterTestFairyGuiRuntimeAiPlannerDevPanelViewModel();
            view.Workspace.ActiveWorkspace = CharacterTestRuntimeAiLabWorkspaceMode.BehaviorGroup;
            view.WorkbenchRows = new[]
            {
                new CharacterTestRuntimeAiLabWorkbenchRowViewModel
                {
                    EntryIndex = 0,
                    CardDraftId = "card-draft-1",
                    PublishedStableId = "bc_0000000001",
                    TitleText = "同名行为卡",
                    Visible = true
                },
                new CharacterTestRuntimeAiLabWorkbenchRowViewModel
                {
                    EntryIndex = 1,
                    CardDraftId = "card-draft-2",
                    PublishedStableId = "bc_0000000002",
                    TitleText = "同名行为卡",
                    Visible = true
                }
            };
            CharacterTestRuntimeAiLabAuthoringUxStatusViewModel status = view.AuthoringStatus;

            AssertSignatureChanged(view, () => status.BehaviorGroupPublishState =
                CharacterTestRuntimeAiLabPublishUxState.GameAvailable);
            AssertSignatureChanged(view, () => status.BehaviorCardPublishState =
                CharacterTestRuntimeAiLabPublishUxState.HasUnpublishedChanges);
            AssertSignatureChanged(view, () => status.DraftSaveState =
                CharacterTestRuntimeAiLabDraftSaveUxState.Saving);
            AssertSignatureChanged(view, () => status.TestSourceState =
                CharacterTestRuntimeAiLabTestSourceUxState.UsingCurrentChanges);
            AssertSignatureChanged(view, () => status.BehaviorGroupHasValidationProblems = true);
            AssertSignatureChanged(view, () => status.BehaviorCardHasValidationProblems = true);
            AssertSignatureChanged(view, () => view.WorkbenchRows[1].PublishState =
                CharacterTestRuntimeAiLabPublishUxState.WorkspaceOnly);
            AssertSignatureChanged(view, () => view.WorkbenchRows[1].HasValidationProblems = true);
            AssertSignatureChanged(view, () => view.WorkbenchRows[1].CardDraftId = "card-draft-3");
            AssertSignatureChanged(view, () =>
                view.WorkbenchRows[1].PublishedStableId = "bc_0000000003");
            view.Workspace.ActiveWorkspace = CharacterTestRuntimeAiLabWorkspaceMode.BehaviorCard;
            view.BehaviorCardWorkspace.Library.Cards = new[]
            {
                new CharacterTestRuntimeAiLabBehaviorAssetCardViewModel
                {
                    StableCardId = "bc_0000000001",
                    PublishedRevision = 1,
                    PublishState = CharacterTestRuntimeAiLabPublishUxState.GameAvailable
                }
            };
            AssertSignatureChanged(view, () =>
                view.BehaviorCardWorkspace.Library.Cards[0].PublishState =
                    CharacterTestRuntimeAiLabPublishUxState.HasUnpublishedChanges);
            AssertSignatureChanged(view, () =>
                view.BehaviorCardWorkspace.Library.Cards[0].PublishedRevision = 2);
        }

        [Test]
        public void AuthoringUxSignature_DuplicateGroupNamesRemainIdentityScoped()
        {
            var view = new CharacterTestFairyGuiRuntimeAiPlannerDevPanelViewModel
            {
                WorkbenchTitleText = "同名行为组",
                WorkbenchGroupIdentity = "group-draft-a"
            };
            view.Workspace.ActiveWorkspace = CharacterTestRuntimeAiLabWorkspaceMode.BehaviorGroup;
            view.AuthoringStatus.BehaviorGroupPublishState =
                CharacterTestRuntimeAiLabPublishUxState.GameAvailable;
            view.AuthoringStatus.BehaviorGroupPublishStatusText = "已同步";
            string first = CharacterTestFairyGuiRuntimeAiPlannerDevPanelViewModelSignature.Create(view);

            view.WorkbenchGroupIdentity = "group-draft-b";
            view.AuthoringStatus.BehaviorGroupPublishState =
                CharacterTestRuntimeAiLabPublishUxState.WorkspaceOnly;
            view.AuthoringStatus.BehaviorGroupPublishStatusText = "尚未发布";
            string second = CharacterTestFairyGuiRuntimeAiPlannerDevPanelViewModelSignature.Create(view);

            Assert.That(view.WorkbenchTitleText, Is.EqualTo("同名行为组"));
            Assert.That(second, Is.Not.EqualTo(first));
        }

        [TestCase(0)]
        [TestCase(1)]
        [TestCase(2)]
        [TestCase(3)]
        public void AuthoringUxBuilder_InvalidSelectedEntryFailsClosed(int invalidKind)
        {
            GameObject host = CreateController(out CharacterTestRuntimeAiPlannerDevSceneController controller);
            try
            {
                CharacterTestRuntimeAiLabBehaviorGroupDraft group = SelectedGroup(controller);
                Assert.That(group.Entries.Count, Is.GreaterThan(0));
                switch (invalidKind)
                {
                    case 0:
                        group.Entries[0] = null;
                        break;
                    case 1:
                        group.Entries[0].CardDraftId = string.Empty;
                        break;
                    case 2:
                        Assert.That(RuntimeEntryBackingField, Is.Not.Null);
                        RuntimeEntryBackingField.SetValue(group.Entries[0], null);
                        break;
                    default:
                        group.Entries[0].CardDraftId = Guid.NewGuid().ToString("D");
                        break;
                }

                CharacterTestFairyGuiRuntimeAiPlannerDevPanelViewModel view = null;
                Assert.DoesNotThrow(() =>
                {
                    view = CharacterTestFairyGuiRuntimeAiPlannerDevPanelViewModelBuilder.Build(
                        controller,
                        commandStatus: null,
                        textProvider: null,
                        CharacterTestRuntimeAiLabWorkspaceMode.BehaviorCard,
                        selectedWorkbenchEntryIndex: 0);
                });

                Assert.That(view, Is.Not.Null);
                StringAssert.Contains("条目已损坏", view.ApplyDraftDisabledReasonText);
                Assert.That(view.CanSaveDraft, Is.False);
                Assert.That(view.CanApplyDraft, Is.False);
                Assert.That(view.CanPublishDraft, Is.False);
                Assert.That(view.PlayerLegalityState.Status,
                    Is.EqualTo(CharacterTestRuntimeAiLabPlayerLegalityStatus.Unavailable));
                CharacterTestRuntimeAiLabWorkbenchRowViewModel invalidRow = null;
                for (int i = 0; i < view.WorkbenchRows.Length; i++)
                {
                    if (view.WorkbenchRows[i]?.EntryIndex == 0)
                    {
                        invalidRow = view.WorkbenchRows[i];
                        break;
                    }
                }
                Assert.That(invalidRow, Is.Not.Null);
                Assert.That(invalidRow.Invalid, Is.True);
                Assert.That(invalidRow.HasValidationProblems, Is.True);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(host);
            }
        }

        [Test]
        public void AuthoringUxBuilder_PooledRowClearsResponsibilitiesWhenEntryBecomesInvalid()
        {
            GameObject host = CreateController(out CharacterTestRuntimeAiPlannerDevSceneController controller);
            try
            {
                CharacterTestRuntimeAiLabBehaviorGroupDraft group = SelectedGroup(controller);
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelViewModel view =
                    CharacterTestFairyGuiRuntimeAiPlannerDevPanelViewModelBuilder.Build(
                        controller,
                        commandStatus: null,
                        textProvider: null,
                        CharacterTestRuntimeAiLabWorkspaceMode.BehaviorCard,
                        selectedWorkbenchEntryIndex: 0);
                CharacterTestRuntimeAiLabWorkbenchRowViewModel pooled = view.WorkbenchRows[0];
                pooled.ResponsibilityExpanded = true;
                pooled.ResponsibilityTotalCount = 4;
                pooled.ResponsibilityEnabledCount = 3;
                pooled.ResponsibilitySummaryText = "stale";
                pooled.ResponsibilityColor = "#ffffff";
                pooled.ResponsibilityWarning = true;

                group.Entries[0].CardDraftId = Guid.NewGuid().ToString("D");
                group.CurrentRevision++;
                controller.DraftWorkspace.CurrentRevision++;
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelViewModel refreshed =
                    CharacterTestFairyGuiRuntimeAiPlannerDevPanelViewModelBuilder.Build(
                        controller,
                        commandStatus: null,
                        textProvider: null,
                        CharacterTestRuntimeAiLabWorkspaceMode.BehaviorCard,
                        selectedWorkbenchEntryIndex: 0,
                        behaviorCardWorkspace: view.BehaviorCardWorkspace,
                        reusableViewModel: view);

                Assert.That(refreshed, Is.SameAs(view));
                CharacterTestRuntimeAiLabWorkbenchRowViewModel invalid = refreshed.WorkbenchRows[0];
                Assert.That(invalid.Invalid, Is.True);
                Assert.That(invalid.CardDraftId, Is.Empty);
                Assert.That(invalid.PublishedStableId, Is.Empty);
                Assert.That(invalid.ResponsibilityKinds,
                    Is.EqualTo(new[] { CharacterTestRuntimeAiPlannerBehaviorResponsibilityKind.Other }));
                Assert.That(invalid.ResponsibilityExpanded, Is.False);
                Assert.That(invalid.ResponsibilityTotalCount, Is.Zero);
                Assert.That(invalid.ResponsibilityEnabledCount, Is.Zero);
                Assert.That(invalid.ResponsibilitySummaryText, Is.Empty);
                Assert.That(invalid.ResponsibilityColor, Is.Empty);
                Assert.That(invalid.ResponsibilityWarning, Is.False);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(host);
            }
        }

        [Test]
        public void BehaviorCardLibraryCachedRefresh_ReprojectsCanonicalPublishState()
        {
            CharacterTestRuntimeAiPlannerPlayerDeckWorkbenchView source =
                CreateSinglePublishedCardWorkbenchView();
            var view = new CharacterTestFairyGuiRuntimeAiPlannerDevPanelViewModel();
            view.WorkbenchRows = new[]
            {
                new CharacterTestRuntimeAiLabWorkbenchRowViewModel
                {
                    EntryIndex = 0,
                    CardDraftId = "card-draft-1",
                    PublishedStableId = "bc_0000000001",
                    PublishState = CharacterTestRuntimeAiLabPublishUxState.GameAvailable,
                    PublishStatusText = "已同步",
                    Visible = true
                }
            };
            var state = new CharacterTestRuntimeAiLabBehaviorCardLibraryState(
                string.Empty,
                CharacterTestRuntimeAiLabBehaviorCardLibraryFilter.All,
                string.Empty,
                string.Empty,
                0f);

            CharacterTestFairyGuiRuntimeAiPlannerDevPanelViewModelBuilder.FillBehaviorCardLibrary(
                view,
                source,
                state);
            CharacterTestRuntimeAiLabBehaviorCardLibraryViewModel library =
                view.BehaviorCardWorkspace.Library;
            long structureKey = library.CachedStructureKey;
            Assert.That(library.Cards[0].StatusText, Is.EqualTo("已同步"));

            view.WorkbenchRows[0].PublishState =
                CharacterTestRuntimeAiLabPublishUxState.HasUnpublishedChanges;
            view.WorkbenchRows[0].PublishStatusText = "有未发布修改";
            CharacterTestFairyGuiRuntimeAiPlannerDevPanelViewModelBuilder.FillBehaviorCardLibrary(
                view,
                source,
                state);

            Assert.That(library.CachedStructureKey, Is.EqualTo(structureKey),
                "The canonical state change should exercise the cached refresh path.");
            Assert.That(library.Cards[0].PublishState,
                Is.EqualTo(CharacterTestRuntimeAiLabPublishUxState.HasUnpublishedChanges));
            Assert.That(library.Cards[0].StatusText, Is.EqualTo("有未发布修改"));
        }

        private static GameObject CreateController(out CharacterTestRuntimeAiPlannerDevSceneController controller)
        {
            var host = new GameObject("Runtime AI Lab A1 Identity Test");
            controller = host.AddComponent<CharacterTestRuntimeAiPlannerDevSceneController>();
            controller.PrepareInitialSetupState();
            ConvertSelectedGroupToLegacyCards(controller);
            RebuildCandidate(controller);
            AssertLegacyCandidateBindings(controller);
            return host;
        }

        private static void AssertLegacyCandidateBindings(
            CharacterTestRuntimeAiPlannerDevSceneController controller)
        {
            CharacterTestRuntimeAiLabBehaviorGroupDraft group = SelectedGroup(controller);
            CharacterTestRuntimeAiLabCandidateRevision revision = RequireCandidate(controller);
            Assert.That(revision.CardIdentities.Count, Is.EqualTo(group.Entries.Count));
            for (int i = 0; i < group.Entries.Count; i++)
            {
                CharacterTestRuntimeAiLabBehaviorEntryDraft entry = group.Entries[i];
                Assert.That(controller.DraftWorkspace.Cards.TryGetValue(
                    entry.CardDraftId,
                    out CharacterTestRuntimeAiLabBehaviorCardDraft card), Is.True);
                CharacterTestRuntimeAiLabRevisionCardIdentity identity = revision.CardIdentities[i];
                Assert.That(identity.RuntimeEntryId, Is.EqualTo(entry.RuntimeEntry.EntryId));
                Assert.That(identity.DraftEntryId, Is.EqualTo(entry.EntryId));
                Assert.That(identity.CardDraftId, Is.EqualTo(card.CardDraftId));
                Assert.That(identity.HasPublishedSource, Is.False);
                Assert.That(identity.PublishedStableId, Is.Empty);
                Assert.That(identity.LegacySourceKey, Is.EqualTo(entry.BehaviorId));
                Assert.That(
                    identity.CandidateSemanticHash,
                    Is.EqualTo(revision.LogicPrograms[i].SemanticHash));
            }
        }

        private static void ConvertSelectedGroupToLegacyCards(
            CharacterTestRuntimeAiPlannerDevSceneController controller)
        {
            CharacterTestRuntimeAiLabBehaviorGroupDraft group = SelectedGroup(controller);
            CharacterTestRuntimeAiLabDraftWorkspace workspace = controller.DraftWorkspace;
            CharacterTestRuntimeAiLabBehaviorCardReferenceCatalog references =
                CreateAuthoringReferences();
            for (int i = 0; i < group.Entries.Count; i++)
            {
                CharacterTestRuntimeAiLabBehaviorEntryDraft entry = group.Entries[i];
                workspace.Cards.TryGetValue(
                    entry.CardDraftId,
                    out CharacterTestRuntimeAiLabBehaviorCardDraft source);
                var legacy = new CharacterTestRuntimeAiLabBehaviorCardDraft(
                    null,
                    entry.BehaviorId,
                    source?.DisplayName ?? entry.BehaviorId,
                    source?.Category ?? "Legacy",
                    CharacterTestRuntimeAiLabBehaviorCardFactory.CreateLegacyAtomic(
                        entry.BehaviorId,
                        CharacterTestRuntimeAiPlannerActionKind.PrimaryAttack));
                CompileLegacyFixture(legacy, entry.BehaviorId, references);
                workspace.TrackStandaloneProjectCardDraft(legacy);
                entry.CardDraftId = legacy.CardDraftId;
            }
            workspace.LastSavedRevision = workspace.CurrentRevision;
        }

        private static CharacterTestRuntimeAiLabBehaviorCardLogicCompileResult
            CompileLegacyFixture(
                CharacterTestRuntimeAiLabBehaviorCardDraft card,
                string stableCardId,
                CharacterTestRuntimeAiLabBehaviorCardReferenceCatalog references)
        {
            Assert.That(card, Is.Not.Null);
            Assert.That(card.LogicPayload, Is.Not.Null);

            // Match the formal legacy-card import boundary before compiling. The v4 Stage
            // projection is not the current authority; fixed-duration confirmation must be
            // applied to every migrated Timeline/Event instead.
            CharacterTestRuntimeAiLabBehaviorRecipeV6Migration.SynchronizeForRead(card.LogicPayload);
            CharacterTestRuntimeAiLabBehaviorRecipeV5Templates.ConfigureGeneratedV7FixedIntervals(
                card.LogicPayload,
                stableCardId);
            CharacterTestRuntimeAiLabBehaviorRecipeV8Migration.SynchronizeForRead(card.LogicPayload);

            CharacterTestRuntimeAiLabBehaviorCardLogicCompileResult compile =
                CharacterTestRuntimeAiLabBehaviorCardLogicCompiler.Compile(
                    card.LogicPayload,
                    references);
            Assert.That(
                compile.Success,
                Is.True,
                "Legacy fixture must pass the formal BehaviorCardLogicCompiler: "
                + FormatLogicDiagnostics(compile.Diagnostics));
            AssertLegacyFixtureEventsAreCanonical(card.LogicPayload, stableCardId);
            return compile;
        }

        private static void AssertLegacyFixtureEventsAreCanonical(
            CharacterTestRuntimeAiLabBehaviorCardLogicPayload payload,
            string stableCardId)
        {
            Assert.That(payload.Timelines, Is.Not.Empty, stableCardId);
            for (int timelineIndex = 0; timelineIndex < payload.Timelines.Count; timelineIndex++)
            {
                CharacterTestRuntimeAiLabBehaviorRecipeTimeline timeline =
                    payload.Timelines[timelineIndex];
                Assert.That(timeline, Is.Not.Null, stableCardId + " timeline " + timelineIndex);
                for (int eventIndex = 0; eventIndex < timeline.Events.Count; eventIndex++)
                {
                    AssertLegacyFixtureEventIsCanonical(
                        timeline.Events[eventIndex],
                        stableCardId + " timeline " + timelineIndex + " event " + eventIndex);
                }
            }
            for (int eventIndex = 0; eventIndex < payload.LegacySharedEvents.Count; eventIndex++)
            {
                AssertLegacyFixtureEventIsCanonical(
                    payload.LegacySharedEvents[eventIndex],
                    stableCardId + " shared event " + eventIndex);
            }
        }

        private static void AssertLegacyFixtureEventIsCanonical(
            CharacterTestRuntimeAiLabBehaviorRecipeEvent recipeEvent,
            string context)
        {
            Assert.That(recipeEvent, Is.Not.Null, context);
            Assert.That(recipeEvent.Operation, Is.Not.Null, context);
            Assert.That(
                recipeEvent.Operation.Kind,
                Is.Not.EqualTo(CharacterTestRuntimeAiLabBehaviorRecipeEventOperationKind.Unconfigured),
                context);
            Assert.That(
                recipeEvent.TimingKind == CharacterTestRuntimeAiLabBehaviorRecipeEventTimingKind.Instant
                || recipeEvent.TimingKind == CharacterTestRuntimeAiLabBehaviorRecipeEventTimingKind.Duration,
                Is.True,
                context);
            Assert.That(recipeEvent.DurationFrames, Is.GreaterThanOrEqualTo(1), context);
            Assert.That(recipeEvent.DurationReviewRequired, Is.False, context);
            Assert.That(
                CharacterTestRuntimeAiLabBehaviorRecipeV8Migration.IsOperationCompatible(
                    recipeEvent.TimingKind,
                    recipeEvent.Operation.Kind),
                Is.True,
                context);
        }

        private static string FormatLogicDiagnostics(
            IReadOnlyList<CharacterTestRuntimeAiLabCardDiagnostic> diagnostics)
        {
            if (diagnostics == null || diagnostics.Count == 0)
                return "no diagnostics";
            var messages = new string[diagnostics.Count];
            for (int i = 0; i < diagnostics.Count; i++)
            {
                CharacterTestRuntimeAiLabCardDiagnostic diagnostic = diagnostics[i];
                messages[i] = diagnostic.Code + " @ " + diagnostic.Path + ": " + diagnostic.Message;
            }
            return string.Join(" | ", messages);
        }

        private static ProjectAuthoringFixture CreateProjectAuthoringFixture()
        {
            CharacterTestRuntimeAiPlannerProjectCatalogSeedResult seed =
                CharacterTestRuntimeAiPlannerProjectCatalogSeed.Create();
            Assert.That(seed, Is.Not.Null, "fixture stage=seed.create: result is null");
            Assert.That(seed.Success, Is.True, "fixture stage=seed.validate: " + seed.Diagnostic);
            Assert.That(seed.Cards, Is.Not.Null, "fixture stage=seed.cards: catalog is null");
            Assert.That(seed.Groups, Is.Not.Null, "fixture stage=seed.groups: catalog is null");
            var state = new CharacterTestRuntimeAiPlannerProjectCatalogState(
                seed.Cards,
                seed.Groups,
                "behavior_card_catalog.json",
                "behavior_group_catalog.json",
                "cards-hash",
                "groups-hash",
                string.Empty);
            Assert.That(state, Is.Not.Null, "fixture stage=state.create: state is null");
            Assert.That(state.Success, Is.True, "fixture stage=state.validate: " + state.Diagnostic);
            Assert.That(state.CardCatalog, Is.Not.Null, "fixture stage=state.cards: catalog is null");
            Assert.That(state.GroupCatalog, Is.Not.Null, "fixture stage=state.groups: catalog is null");
            CharacterTestArenaMatchConfig config =
                CharacterTestArenaMatchConfig.CreateProjectBacked(state);
            Assert.That(config, Is.Not.Null, "fixture stage=config.create: config is null");
            Assert.That(config.ProjectCatalogState, Is.SameAs(state),
                "fixture stage=config.project-state: project state was not retained");
            CharacterTestRuntimeAiLabDraftWorkspace workspace =
                CharacterTestRuntimeAiLabDraftWorkspace.Import(config);
            Assert.That(workspace, Is.Not.Null, "fixture stage=workspace.import: workspace is null");
            Assert.That(workspace.Cards, Is.Not.Null, "fixture stage=workspace.cards: map is null");
            Assert.That(workspace.TryGetGroupForSlot(
                CharacterTestArenaMatchConfig.Team1Id,
                0,
                out CharacterTestRuntimeAiLabBehaviorGroupDraft group), Is.True,
                "fixture stage=workspace.group: Team 1 slot 0 has no group");
            Assert.That(group, Is.Not.Null, "fixture stage=workspace.group: group is null");
            Assert.That(group.Profile, Is.Not.Null, "fixture stage=workspace.group-profile: profile is null");
            Assert.That(group.Entries, Is.Not.Null, "fixture stage=workspace.group-entries: list is null");
            Assert.That(group.Entries, Is.Not.Empty, "fixture stage=workspace.group-entries: list is empty");
            Assert.That(group.HasProjectSource, Is.True,
                "fixture stage=workspace.group-source: Published source is missing");
            int entryIndex = -1;
            CharacterTestRuntimeAiLabBehaviorCardDraft card = null;
            for (int i = 0; i < group.Entries.Count; i++)
            {
                CharacterTestRuntimeAiLabBehaviorEntryDraft entry = group.Entries[i];
                if (entry != null
                    && workspace.Cards.TryGetValue(
                        entry.CardDraftId,
                        out CharacterTestRuntimeAiLabBehaviorCardDraft candidate)
                    && candidate.HasPublishedSource)
                {
                    entryIndex = i;
                    card = candidate;
                    break;
                }
            }
            Assert.That(entryIndex, Is.GreaterThanOrEqualTo(0),
                "fixture stage=workspace.card-select: no Published card entry was found");
            Assert.That(card, Is.Not.Null, "fixture stage=workspace.card-select: card is null");
            Assert.That(card.LogicPayload, Is.Not.Null,
                "fixture stage=workspace.card-logic: logic payload is null");
            CharacterTestRuntimeAiLabBehaviorCardReferenceCatalog references =
                CreateAuthoringReferences();
            Assert.That(references, Is.Not.Null,
                "fixture stage=references.create: reference catalog is null");
            var authoring = new CharacterTestRuntimeAiPlannerProjectCatalogAuthoringSession(state);
            Assert.That(authoring, Is.Not.Null, "fixture stage=authoring.create: session is null");
            Assert.That(authoring.State, Is.SameAs(state),
                "fixture stage=authoring.state: project state was not retained");
            var fixture = new ProjectAuthoringFixture(
                state,
                config,
                workspace,
                group,
                card,
                entryIndex,
                references,
                authoring);
            AssertProjectAuthoringFixture(fixture, "fixture stage=fixture.final");
            return fixture;
        }

        private static GameObject CreateProjectBackedController(
            out CharacterTestRuntimeAiPlannerDevSceneController controller,
            out ProjectAuthoringFixture fixture)
        {
            controller = null;
            fixture = null;
            GameObject host = null;
            string stage = "fixture";
            try
            {
                fixture = CreateProjectAuthoringFixture();
                AssertProjectAuthoringFixture(fixture, "controller stage=fixture");
                stage = "host.create";
                host = new GameObject("Runtime AI Lab UX-01 Project Builder Test");
                Assert.That(host, Is.Not.Null, "controller stage=host.create: host is null");
                stage = "controller.add";
                controller = host.AddComponent<CharacterTestRuntimeAiPlannerDevSceneController>();
                Assert.That(controller, Is.Not.Null, "controller stage=controller.add: component is null");
                stage = "controller.prepare";
                controller.PrepareInitialSetupState();
                Assert.That(controller.DraftWorkspace, Is.Not.Null,
                    "controller stage=controller.prepare: initial workspace is null");
                Assert.That(DraftPersistenceField, Is.Not.Null,
                    "controller stage=controller.prepare: persistence FieldInfo is null");
                Assert.That(DraftPersistenceField.GetValue(controller), Is.Not.Null,
                    "controller stage=controller.prepare: persistence instance is null");
                stage = "controller.inject";
                Assert.That(MatchConfigField, Is.Not.Null,
                    "controller stage=controller.inject: match-config FieldInfo is null");
                Assert.That(DraftWorkspaceField, Is.Not.Null,
                    "controller stage=controller.inject: workspace FieldInfo is null");
                Assert.That(ProjectCatalogAuthoringField, Is.Not.Null,
                    "controller stage=controller.inject: authoring FieldInfo is null");
                Assert.That(BehaviorCardReferencesField, Is.Not.Null,
                    "controller stage=controller.inject: references FieldInfo is null");
                MatchConfigField.SetValue(controller, fixture.Config);
                DraftWorkspaceField.SetValue(controller, fixture.Workspace);
                ProjectCatalogAuthoringField.SetValue(controller, fixture.Authoring);
                BehaviorCardReferencesField.SetValue(controller, fixture.References);
                Assert.That(controller.DraftWorkspace, Is.SameAs(fixture.Workspace),
                    "controller stage=controller.inject: workspace was not retained");
                Assert.That(ProjectCatalogAuthoringField.GetValue(controller), Is.SameAs(fixture.Authoring),
                    "controller stage=controller.inject: authoring session was not retained");
                Assert.That(BehaviorCardReferencesField.GetValue(controller), Is.SameAs(fixture.References),
                    "controller stage=controller.inject: reference catalog was not retained");
                stage = "controller.restore-setup";
                fixture.Workspace.RestoreSetupToArena(fixture.Config);
                stage = "controller.candidate";
                RebuildCandidate(controller);
                Assert.That(controller.CandidateRevision, Is.Not.Null,
                    "controller stage=controller.candidate: " + controller.CandidateDiagnostic);
                return host;
            }
            catch (NullReferenceException exception)
            {
                if (host != null)
                    UnityEngine.Object.DestroyImmediate(host);
                throw new InvalidOperationException(
                    "Project-backed UX-01 controller fixture failed at stage=" + stage
                    + Environment.NewLine + exception,
                    exception);
            }
        }

        private static CharacterTestFairyGuiRuntimeAiPlannerDevPanelViewModel
            BuildProjectAuthoringView(
                CharacterTestRuntimeAiPlannerDevSceneController controller,
                int entryIndex,
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelViewModel reusable = null,
                bool allowUnavailableWorkspace = false)
        {
            Assert.That(controller, Is.Not.Null, "view stage=input.controller: controller is null");
            if (allowUnavailableWorkspace)
            {
                Assert.That(controller.DraftWorkspace, Is.Null,
                    "view stage=input.workspace: expected unavailable workspace");
            }
            else
            {
                Assert.That(controller.DraftWorkspace, Is.Not.Null,
                    "view stage=input.workspace: workspace is null");
                Assert.That(controller.DraftWorkspace.TryGetGroupForSlot(
                    controller.SelectedSetupTeamId,
                    controller.SelectedSetupSlotIndex,
                    out CharacterTestRuntimeAiLabBehaviorGroupDraft group), Is.True,
                    "view stage=input.group: selected group is missing");
                Assert.That(group, Is.Not.Null, "view stage=input.group: selected group is null");
                Assert.That(entryIndex, Is.GreaterThanOrEqualTo(0),
                    "view stage=input.entry: selected index is negative");
                Assert.That(entryIndex, Is.LessThan(group.Entries.Count),
                    "view stage=input.entry: selected index is out of range");
                Assert.That(BehaviorCardReferencesField, Is.Not.Null,
                    "view stage=input.references: FieldInfo is null");
                Assert.That(BehaviorCardReferencesField.GetValue(controller), Is.Not.Null,
                    "view stage=input.references: catalog is null");
            }
            CharacterTestFairyGuiRuntimeAiPlannerDevPanelViewModel result =
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelViewModelBuilder.Build(
                controller,
                commandStatus: null,
                textProvider: null,
                workspaceMode: CharacterTestRuntimeAiLabWorkspaceMode.BehaviorCard,
                selectedWorkbenchEntryIndex: entryIndex,
                behaviorCardWorkspace: reusable?.BehaviorCardWorkspace,
                reusableViewModel: reusable);
            Assert.That(result, Is.Not.Null, "view stage=build.result: ViewModel is null");
            Assert.That(result.AuthoringStatus, Is.Not.Null,
                "view stage=build.authoring-status: projection is null");
            Assert.That(result.BehaviorCardWorkspace, Is.Not.Null,
                "view stage=build.card-workspace: projection is null");
            return result;
        }

        private static CharacterTestRuntimeAiLabBehaviorCardReferenceCatalog
            CreateAuthoringReferences()
        {
            var abilities = CharacterTestAbilityCatalogSource.CreateEmbeddedFallback();
            Assert.That(abilities, Is.Not.Null,
                "references stage=abilities.embedded: registry is null");
            Assert.That(abilities.Count, Is.GreaterThan(0),
                "references stage=abilities.embedded: registry is empty");
            CharacterTestCharacterActionRuntime actionRuntime =
                CharacterTestCharacterActionConfigSource.Load(
                    CharacterTestCharacterActionConfigSourceOptions.CreateLegacyFixtureFallback());
            Assert.That(actionRuntime, Is.Not.Null,
                "references stage=actions.embedded: runtime is null");
            Assert.That(actionRuntime.Registry, Is.Not.Null,
                "references stage=actions.embedded: registry is null");
            Assert.That(actionRuntime.Registry.Actions, Is.Not.Empty,
                "references stage=actions.embedded: registry is empty");
            var references = new CharacterTestRuntimeAiLabBehaviorCardReferenceCatalog(
                abilities,
                actionRuntime.Registry);
            Assert.That(references, Is.Not.Null,
                "references stage=catalog.create: catalog is null");
            return references;
        }

        private static void AssertProjectAuthoringFixture(
            ProjectAuthoringFixture fixture,
            string stage)
        {
            Assert.That(fixture, Is.Not.Null, stage + ": fixture is null");
            Assert.That(fixture.State, Is.Not.Null, stage + ": state is null");
            Assert.That(fixture.Config, Is.Not.Null, stage + ": config is null");
            Assert.That(fixture.Workspace, Is.Not.Null, stage + ": workspace is null");
            Assert.That(fixture.Group, Is.Not.Null, stage + ": group is null");
            Assert.That(fixture.Card, Is.Not.Null, stage + ": card is null");
            Assert.That(fixture.References, Is.Not.Null, stage + ": references are null");
            Assert.That(fixture.Authoring, Is.Not.Null, stage + ": authoring session is null");
            Assert.That(fixture.EntryIndex, Is.GreaterThanOrEqualTo(0),
                stage + ": entry index is negative");
            Assert.That(fixture.EntryIndex, Is.LessThan(fixture.Group.Entries.Count),
                stage + ": entry index is out of range");
            Assert.That(fixture.Group.Entries[fixture.EntryIndex], Is.Not.Null,
                stage + ": selected entry is null");
            Assert.That(
                fixture.Group.Entries[fixture.EntryIndex].CardDraftId,
                Is.EqualTo(fixture.Card.CardDraftId),
                stage + ": selected entry/card identity mismatch");
        }

        private static CharacterTestRuntimeAiPlannerPublishedBehaviorCard
            CreateNextPublishedRevision(ProjectAuthoringFixture fixture)
        {
            AssertProjectAuthoringFixture(fixture, "publish-card stage=input");
            Assert.That(fixture.State.CardCatalog, Is.Not.Null,
                "publish-card stage=source-catalog: catalog is null");
            Assert.That(fixture.State.CardCatalog.TryGetExact(
                fixture.Card.PublishedStableId,
                fixture.Card.SourcePublishedRevision,
                out CharacterTestRuntimeAiPlannerPublishedBehaviorCard source), Is.True,
                "publish-card stage=source-card: exact Published source is missing");
            Assert.That(source, Is.Not.Null,
                "publish-card stage=source-card: exact Published source is null");
            var metadata = new CharacterTestRuntimeAiPlannerPublishedBehaviorCardMetadata(
                fixture.Card.DisplayName,
                fixture.Card.IconKey,
                fixture.Card.Cost,
                fixture.Card.ShortDescription,
                fixture.Card.UseSummary,
                fixture.Card.RiskSummary,
                SplitTags(fixture.Card.TagsText),
                fixture.Card.RequiredCapabilities,
                fixture.Card.ResponsibilityKinds);
            CharacterTestRuntimeAiPlannerPublishedBehaviorCardPublishResult publish =
                CharacterTestRuntimeAiPlannerPublishedBehaviorCardPublisher.ValidateAndCreate(
                    fixture.Card,
                    new CharacterTestRuntimeAiPlannerPublishedBehaviorCardPublishRequest(
                        fixture.Card.PublishedStableId,
                        fixture.Card.SourcePublishedRevision + 1,
                        source.MinimumRuntimeContractVersion,
                        source.MaximumRuntimeContractVersion,
                        metadata),
                    fixture.References);
            Assert.That(publish, Is.Not.Null,
                "publish-card stage=validate: publish result is null");
            Assert.That(publish.Diagnostics, Is.Not.Null,
                "publish-card stage=validate: diagnostics are null");
            Assert.That(publish.Success, Is.True,
                publish.Diagnostics.Count > 0 ? publish.Diagnostics[0].Message : string.Empty);
            Assert.That(publish.Card, Is.Not.Null,
                "publish-card stage=validate: Published card is null");
            return publish.Card;
        }

        private static CharacterTestRuntimeAiPlannerProjectCatalogState AddPublishedCardRevision(
            ProjectAuthoringFixture fixture,
            CharacterTestRuntimeAiPlannerPublishedBehaviorCard card)
        {
            AssertProjectAuthoringFixture(fixture, "publish-catalog stage=input");
            Assert.That(card, Is.Not.Null, "publish-catalog stage=input: card is null");
            Assert.That(fixture.State.CardCatalog, Is.Not.Null,
                "publish-catalog stage=source: CardCatalog is null");
            Assert.That(fixture.State.GroupCatalog, Is.Not.Null,
                "publish-catalog stage=source: GroupCatalog is null");
            var cards = new List<CharacterTestRuntimeAiPlannerPublishedBehaviorCard>(
                fixture.State.CardCatalog.Cards);
            cards.Add(card);
            CharacterTestRuntimeAiPlannerPublishedBehaviorCardCatalogCreateResult create =
                CharacterTestRuntimeAiPlannerPublishedBehaviorCardCatalog.ValidateAndCreate(
                    fixture.State.CardCatalog.CatalogId,
                    fixture.State.CardCatalog.Revision + 1,
                    cards,
                    fixture.References);
            Assert.That(create, Is.Not.Null,
                "publish-catalog stage=validate: result is null");
            Assert.That(create.Diagnostics, Is.Not.Null,
                "publish-catalog stage=validate: diagnostics are null");
            Assert.That(create.Success, Is.True,
                create.Diagnostics.Count > 0 ? create.Diagnostics[0].Message : string.Empty);
            Assert.That(create.Catalog, Is.Not.Null,
                "publish-catalog stage=validate: catalog is null");
            var state = new CharacterTestRuntimeAiPlannerProjectCatalogState(
                create.Catalog,
                fixture.State.GroupCatalog,
                fixture.State.CardCatalogPath,
                fixture.State.GroupCatalogPath,
                fixture.State.CardFileHash,
                fixture.State.GroupFileHash,
                string.Empty);
            Assert.That(state, Is.Not.Null, "publish-catalog stage=state: state is null");
            Assert.That(state.Success, Is.True,
                "publish-catalog stage=state: " + state.Diagnostic);
            return state;
        }

        private static string[] SplitTags(string value)
        {
            return string.IsNullOrWhiteSpace(value)
                ? Array.Empty<string>()
                : value.Split(
                    new[] { ',', '，' },
                    StringSplitOptions.RemoveEmptyEntries);
        }

        private static CharacterTestRuntimeAiPlannerPlayerDeckWorkbenchView
            CreateSinglePublishedCardWorkbenchView()
        {
            var metadata = new CharacterTestRuntimeAiPlannerPublishedBehaviorCardMetadata(
                "同名行为卡",
                string.Empty,
                1,
                string.Empty,
                string.Empty,
                string.Empty);
            var published = new CharacterTestRuntimeAiPlannerPublishedBehaviorCard(
                "bc_0000000001",
                1,
                CharacterTestRuntimeAiPlannerPublishedBehaviorCardPublisher.CurrentRuntimeContractVersion,
                CharacterTestRuntimeAiPlannerPublishedBehaviorCardPublisher.CurrentRuntimeContractVersion,
                metadata,
                logic: null,
                semanticHash: 11L,
                contentHash: 12L);
            var card = new CharacterTestRuntimeAiPlannerPlayerDeckCardView(
                published.StableCardId,
                published,
                CharacterTestRuntimeAiPlannerPlayerDeckCardAvailability.Available,
                isUnlocked: true,
                isInstalled: true,
                canInstall: false,
                canRemove: true,
                canReplace: false,
                canUseAsReplacement: false,
                blockReason: CharacterTestRuntimeAiPlannerPlayerDeckCardBlockReason.None,
                pinnedRevision: published.Revision);
            return new CharacterTestRuntimeAiPlannerPlayerDeckWorkbenchView(
                "character.test",
                slotCount: 1,
                knownCost: 1,
                isKnownCostComplete: true,
                deckHash: 1L,
                isResolved: true,
                cards: new[] { card },
                deckDiagnostics: Array.Empty<CharacterTestRuntimeAiPlannerInstalledDeckDiagnostic>(),
                lastCommandDiagnostics: Array.Empty<CharacterTestRuntimeAiPlannerInstalledDeckDiagnostic>(),
                isSessionLocked: false);
        }

        private sealed class ProjectAuthoringFixture
        {
            internal ProjectAuthoringFixture(
                CharacterTestRuntimeAiPlannerProjectCatalogState state,
                CharacterTestArenaMatchConfig config,
                CharacterTestRuntimeAiLabDraftWorkspace workspace,
                CharacterTestRuntimeAiLabBehaviorGroupDraft group,
                CharacterTestRuntimeAiLabBehaviorCardDraft card,
                int entryIndex,
                CharacterTestRuntimeAiLabBehaviorCardReferenceCatalog references,
                CharacterTestRuntimeAiPlannerProjectCatalogAuthoringSession authoring)
            {
                State = state;
                Config = config;
                Workspace = workspace;
                Group = group;
                Card = card;
                EntryIndex = entryIndex;
                References = references;
                Authoring = authoring;
            }

            internal CharacterTestRuntimeAiPlannerProjectCatalogState State { get; }
            internal CharacterTestArenaMatchConfig Config { get; }
            internal CharacterTestRuntimeAiLabDraftWorkspace Workspace { get; }
            internal CharacterTestRuntimeAiLabBehaviorGroupDraft Group { get; }
            internal CharacterTestRuntimeAiLabBehaviorCardDraft Card { get; }
            internal int EntryIndex { get; }
            internal CharacterTestRuntimeAiLabBehaviorCardReferenceCatalog References { get; }
            internal CharacterTestRuntimeAiPlannerProjectCatalogAuthoringSession Authoring { get; }
        }

        private static CharacterTestRuntimeAiLabCandidateRevision RequireCandidate(
            CharacterTestRuntimeAiPlannerDevSceneController controller)
        {
            Assert.That(controller.CandidateRevision, Is.Not.Null, controller.CandidateDiagnostic);
            return controller.CandidateRevision;
        }

        private static CharacterTestRuntimeAiLabBehaviorGroupDraft SelectedGroup(
            CharacterTestRuntimeAiPlannerDevSceneController controller)
        {
            Assert.That(
                controller.DraftWorkspace.TryGetGroupForSlot(
                    controller.SelectedSetupTeamId,
                    controller.SelectedSetupSlotIndex,
                    out CharacterTestRuntimeAiLabBehaviorGroupDraft group),
                Is.True);
            return group;
        }

        private static void InstallBaseline(
            CharacterTestRuntimeAiPlannerDevSceneController controller,
            CharacterTestRuntimeAiLabCandidateRevision revision)
        {
            Assert.That(BaselineRevisionField, Is.Not.Null);
            BaselineRevisionField.SetValue(controller, new CharacterTestRuntimeAiLabAppliedRevision(0, 0, revision));
        }

        private static List<CharacterTestRuntimeAiLabAppliedRevision> AppliedEvents(
            CharacterTestRuntimeAiPlannerDevSceneController controller)
        {
            Assert.That(AppliedRevisionEventsField, Is.Not.Null);
            return (List<CharacterTestRuntimeAiLabAppliedRevision>)AppliedRevisionEventsField.GetValue(controller);
        }

        private static void RebuildCandidate(CharacterTestRuntimeAiPlannerDevSceneController controller)
        {
            Assert.That(BuildCandidateRevisionMethod, Is.Not.Null);
            BuildCandidateRevisionMethod.Invoke(controller, null);
        }

        private static CharacterTestRuntimeAiPlannerAgentSnapshot Agent(int entityId, string selectedActionName = "")
        {
            return new CharacterTestRuntimeAiPlannerAgentSnapshot(
                entityId,
                targetEntityId: 0,
                activeStatePath: string.Empty,
                targetExists: false,
                targetInAttackRange: false,
                targetDistance: 0f,
                movePathAvailable: false,
                selectedUtilityScore: 0f,
                selectedActionCost: 0f,
                selectedActionId: 0,
                selectedActionName: selectedActionName,
                selectedActionKind: CharacterTestRuntimeAiPlannerActionKind.Wait,
                lastCommand: string.Empty,
                suppressedReason: string.Empty,
                rejectReason: string.Empty,
                transitionFromStateId: 0,
                transitionToStateId: 0,
                runtimeAiPlannerProfileSlot: 0);
        }

        private static CharacterTestRuntimeAiLabLogicAgentStateSnapshot LogicAgent(
            CharacterTestRuntimeAiLabCandidateRevision revision,
            int entityId,
            int activeIndex,
            int pendingIndex = -1)
        {
            CharacterTestRuntimeAiLabRevisionCardIdentity activeIdentity = activeIndex >= 0
                ? revision.CardIdentities[activeIndex]
                : null;
            CharacterTestRuntimeAiLabCompiledLogicProgram activeProgram = activeIndex >= 0
                ? revision.LogicPrograms[activeIndex]
                : null;
            int primaryActionStageIndex = FindPrimaryActionStageIndex(activeProgram);
            int currentStageIndex = activeProgram == null || activeProgram.Stages.Count == 0
                ? -1
                : primaryActionStageIndex >= 0 ? primaryActionStageIndex : 0;
            if (activeIdentity != null)
            {
                Assert.That(activeIdentity.BindingIndex, Is.EqualTo(activeIndex));
                Assert.That(activeIdentity.CardDraftId, Is.Not.Empty);
                Assert.That(activeIdentity.LegacySourceKey, Is.Not.Empty);
                Assert.That(activeIdentity.CandidateSemanticHash, Is.EqualTo(activeProgram.SemanticHash));
            }
            return new CharacterTestRuntimeAiLabLogicAgentStateSnapshot
            {
                EntityId = entityId,
                ActiveBindingIndex = activeIndex,
                PendingBindingIndex = pendingIndex,
                BehaviorCardStableId = activeIdentity == null
                    ? 0
                    : activeIndex == 0 ? FirstSyntheticCardNumericId : SecondSyntheticCardNumericId,
                ProgramSemanticHash = activeProgram?.SemanticHash ?? 0L,
                RuntimeState = (int)CharacterTestRuntimeAiLabLogicRuntimeState.Running,
                CurrentStageIndex = currentStageIndex,
                PrimaryActionStageIndex = primaryActionStageIndex,
                PrimaryActionState = primaryActionStageIndex >= 0
                    ? (int)CharacterTestRuntimeAiLabLogicActionState.Running
                    : (int)CharacterTestRuntimeAiLabLogicActionState.None
            };
        }

        private static int FindPrimaryActionStageIndex(
            CharacterTestRuntimeAiLabCompiledLogicProgram program)
        {
            for (int i = 0; program != null && i < program.Stages.Count; i++)
            {
                if (program.Stages[i]?.OwnsPrimaryAction == true)
                    return i;
            }
            return -1;
        }

        private static string FormatRuntimeResolution(
            CharacterTestRuntimeAiLabBehaviorCardRuntimeIdentityResolution resolution)
        {
            return resolution == null
                ? "Runtime resolution is null."
                : resolution.Reason + ": " + resolution.Diagnostic;
        }

        private static CharacterTestRuntimeAiPlannerStateSnapshot State(
            CharacterTestRuntimeAiLabCandidateRevision revision,
            int profileSlot,
            params CharacterTestRuntimeAiLabLogicAgentStateSnapshot[] agents)
        {
            return new CharacterTestRuntimeAiPlannerStateSnapshot
            {
                SchemaVersion = CharacterTestRuntimeAiPlannerStateSnapshot.CurrentSchemaVersion,
                Slots = Array.Empty<CharacterTestRuntimeAiPlannerSlotStateSnapshot>(),
                BehaviorLogicSlots = new[]
                {
                    new CharacterTestRuntimeAiLabLogicSlotStateSnapshot
                    {
                        Slot = profileSlot,
                        AuthorityHash = revision.AuthorityHash,
                        Agents = agents
                    }
                }
            };
        }

        private static void AssertFailure(
            CharacterTestRuntimeAiPlannerDevSceneController controller,
            CharacterTestRuntimeAiPlannerStateSnapshot state,
            CharacterTestRuntimeAiPlannerAgentSnapshot selected,
            CharacterTestRuntimeAiLabIdentityResolutionReason expected)
        {
            CharacterTestRuntimeAiLabBehaviorCardRuntimeIdentityResolution result =
                CharacterTestRuntimeAiLabBehaviorCardIdentityProjector.ResolveRuntime(
                    controller,
                    state,
                    1,
                    selected,
                    hasSelectedAgent: true);
            Assert.That(result.Status, Is.EqualTo(CharacterTestRuntimeAiLabIdentityResolutionStatus.Unresolved));
            Assert.That(result.Reason, Is.EqualTo(expected));
        }

        private static void AssertSignatureChanged(
            CharacterTestFairyGuiRuntimeAiPlannerDevPanelViewModel view,
            Action mutation)
        {
            string before = CharacterTestFairyGuiRuntimeAiPlannerDevPanelViewModelSignature.Create(view);
            mutation();
            string after = CharacterTestFairyGuiRuntimeAiPlannerDevPanelViewModelSignature.Create(view);
            Assert.That(after, Is.Not.EqualTo(before));
        }

        private static void CreateSamePublishedRevision(
            out CharacterTestRuntimeAiLabCandidateRevision revision)
        {
            const string StableId = "behavior-card.test.shared";
            var profile = new CharacterTestRuntimeAiPlannerBehaviorCompositionProfile("identity-test");
            var programs = new CharacterTestRuntimeAiLabCompiledLogicProgram[2];
            var identities = new CharacterTestRuntimeAiLabRevisionCardIdentity[2];
            CharacterTestRuntimeAiLabBehaviorCardReferenceCatalog references =
                CreateAuthoringReferences();
            for (int i = 0; i < 2; i++)
            {
                var card = new CharacterTestRuntimeAiLabBehaviorCardDraft(
                    null,
                    StableId,
                    "Shared card " + i,
                    "Test",
                    CharacterTestRuntimeAiLabBehaviorCardFactory.CreateLegacyAtomic(
                        CharacterTestRuntimeAiPlannerBehaviorCompositionCatalog.FoundationIdle));
                card.SourcePublishedRevision = 1;
                card.SourcePublishedSemanticHash = 11;
                card.SourcePublishedContentHash = 12;
                CharacterTestRuntimeAiLabBehaviorCardLogicCompileResult compile =
                    CompileLegacyFixture(card, StableId, references);
                programs[i] = compile.Program;
                var runtimeEntry = new CharacterTestRuntimeAiPlannerBehaviorModuleEntry(
                    CharacterTestRuntimeAiPlannerBehaviorCompositionCatalog.FoundationIdle,
                    enabled: true,
                    order: i);
                var entry = new CharacterTestRuntimeAiLabBehaviorEntryDraft(
                    null,
                    card.CardDraftId,
                    runtimeEntry.BehaviorId,
                    i,
                    disabled: false,
                    runtimeEntry);
                profile.Entries.Add(entry.RuntimeEntry);
                Assert.That(
                    CharacterTestRuntimeAiLabRevisionCardIdentity.TryCreate(
                        i,
                        entry,
                        card,
                        compile.Program,
                        out identities[i],
                        out string diagnostic),
                    Is.True,
                    diagnostic);
            }
            Assert.That(
                CharacterTestRuntimeAiLabCandidateRevision.TryCreate(
                    1,
                    101,
                    profile,
                    new CharacterTestRuntimeAiLabCandidateComparisonReport(2, 2, 0, string.Empty, 101, true),
                    programs,
                    identities,
                    string.Empty,
                    out revision,
                    out string candidateDiagnostic),
                Is.True,
                candidateDiagnostic);
        }

        private static CharacterTestRuntimeAiLabCompiledLogicProgram[] CopyPrograms(
            CharacterTestRuntimeAiLabCandidateRevision revision)
        {
            var result = new CharacterTestRuntimeAiLabCompiledLogicProgram[revision.LogicPrograms.Count];
            for (int i = 0; i < result.Length; i++)
                result[i] = revision.LogicPrograms[i];
            return result;
        }
    }
}
