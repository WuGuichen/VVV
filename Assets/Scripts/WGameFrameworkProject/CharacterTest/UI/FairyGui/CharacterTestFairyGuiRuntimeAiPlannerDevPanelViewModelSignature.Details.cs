using System;
using System.Globalization;
using System.Text;

namespace MxFramework.Game.CharacterTest
{
    internal static partial class CharacterTestFairyGuiRuntimeAiPlannerDevPanelViewModelSignature
    {
        private static void AppendToken(StringBuilder builder, string value)
        {
            value ??= string.Empty;
            builder.Append(value.Length).Append(':').Append(value).Append('|');
        }

        private static void AppendToken(
            StringBuilder builder,
            MxFramework.CharacterAction.CharacterActionId value)
        {
            builder.Append(value.Value).Append('|');
        }

        private static void AppendToken(
            StringBuilder builder,
            MxFramework.CharacterAction.CharacterActionId? value)
        {
            builder.Append(value?.Value ?? 0).Append('|');
        }

        private static void AppendIdentityProjection(
            StringBuilder builder,
            CharacterTestRuntimeAiLabBehaviorCardIdentityProjection projection)
        {
            CharacterTestRuntimeAiLabEditCardIdentity edit = projection?.EditCard;
            CharacterTestRuntimeAiLabRevisionCardIdentity candidate = projection?.CandidateCard;
            CharacterTestRuntimeAiLabBehaviorCardRuntimeIdentityResolution runtime = projection?.Runtime;
            CharacterTestRuntimeAiLabRevisionCardIdentity committed = runtime?.CommittedCard;
            CharacterTestRuntimeAiLabRevisionCardIdentity pending = runtime?.PendingCard;
            CharacterTestRuntimeAiLabPrimaryActionIdentity action = runtime?.PrimaryAction;
            CharacterTestRuntimeAiLabCompositionActionDiagnostic wrapper =
                projection?.CompositionWrapperDiagnostic;
            builder.Append(projection != null ? (int)projection.EditResolutionStatus : 0).Append('|')
                .Append(projection != null ? (int)projection.EditResolutionReason : 0).Append('|');
            AppendToken(builder, projection?.EditDiagnostic);
            builder.Append(projection != null ? (int)projection.Relationship : 0).Append('|')
                .Append(projection?.CandidateRevision ?? 0).Append('|')
                .Append((int)(projection?.CandidateState ?? CharacterTestRuntimeAiLabCandidateState.None)).Append('|')
                .Append(projection?.CandidateAuthorityHash ?? 0L).Append('|')
                .Append(edit != null && edit.HasPublishedSource ? '1' : '0').Append('|')
                .Append(edit?.DraftRevision ?? 0).Append('|')
                .Append(edit?.SourcePublishedRevision ?? 0).Append('|')
                .Append(edit?.SourcePublishedSemanticHash ?? 0L).Append('|')
                .Append(edit?.SourcePublishedContentHash ?? 0L).Append('|')
                .Append(edit?.CurrentSemanticHash ?? 0L).Append('|');
            AppendToken(builder, edit?.DraftEntryId);
            AppendToken(builder, edit?.CardDraftId);
            AppendToken(builder, edit?.PublishedStableId);
            AppendToken(builder, edit?.LegacySourceKey);
            AppendToken(builder, edit?.DisplayName);
            AppendRevisionIdentity(builder, candidate);
            builder.Append(runtime != null ? (int)runtime.Status : 0).Append('|')
                .Append(runtime != null ? (int)runtime.Reason : 0).Append('|')
                .Append(runtime?.SelectedEntityId ?? 0).Append('|')
                .Append(runtime?.Frame ?? 0).Append('|')
                .Append(runtime?.ProfileSlot ?? 0).Append('|')
                .Append(runtime?.AuthorityHash ?? 0L).Append('|')
                .Append(runtime != null ? (int)runtime.RuntimeState : 0).Append('|')
                .Append(runtime != null && runtime.BindingReleasePending ? '1' : '0').Append('|');
            AppendToken(builder, runtime?.Diagnostic);
            AppendToken(builder, runtime?.CurrentStageId);
            AppendToken(builder, runtime?.CurrentStageTitle);
            AppendRevisionIdentity(builder, committed);
            AppendRevisionIdentity(builder, pending);
            builder.Append(action != null && action.Available ? '1' : '0').Append('|')
                .Append(action?.StageIndex ?? -1).Append('|')
                .Append(action != null ? (int)action.StageKind : 0).Append('|')
                .Append(action?.AbilityId ?? 0).Append('|')
                .Append(action != null ? (int)action.LegacyActionKind : 0).Append('|')
                .Append(action != null ? (int)action.MovementStrategy : 0).Append('|')
                .Append(action != null ? (int)action.State : 0).Append('|')
                .Append(action != null && action.CommandPending ? '1' : '0')
                .Append(action != null && action.CommandRejected ? '1' : '0')
                .Append(action != null && action.ActionObserved ? '1' : '0')
                .Append(action != null && action.CancelRequested ? '1' : '0')
                .Append(action != null && action.ClearControlPending ? '1' : '0').Append('|');
            AppendToken(builder, action?.StageId);
            AppendToken(builder, action?.StageTitle);
            AppendToken(builder, action?.CharacterActionId);
            AppendToken(builder, action?.DisplayName);
            builder.Append(wrapper != null ? (int)wrapper.Kind : 0).Append('|');
            AppendToken(builder, wrapper?.Name);
        }

        private static void AppendRevisionIdentity(
            StringBuilder builder,
            CharacterTestRuntimeAiLabRevisionCardIdentity identity)
        {
            builder.Append(identity != null ? '1' : '0').Append('|')
                .Append(identity?.BindingIndex ?? -1).Append('|')
                .Append(identity != null && identity.HasPublishedSource ? '1' : '0').Append('|')
                .Append(identity?.SourcePublishedRevision ?? 0).Append('|')
                .Append(identity?.SourcePublishedSemanticHash ?? 0L).Append('|')
                .Append(identity?.SourcePublishedContentHash ?? 0L).Append('|')
                .Append(identity?.CandidateSemanticHash ?? 0L).Append('|');
            AppendToken(builder, identity?.RuntimeEntryId);
            AppendToken(builder, identity?.DraftEntryId);
            AppendToken(builder, identity?.CardDraftId);
            AppendToken(builder, identity?.PublishedStableId);
            AppendToken(builder, identity?.LegacySourceKey);
            AppendToken(builder, identity?.DisplayName);
        }

        private static void AppendFactsWorkspaceSignature(
            StringBuilder builder,
            CharacterTestRuntimeAiLabFactsWorkspaceViewModel facts)
        {
            builder.Append(facts != null && facts.Available ? '1' : '0').Append('|')
                .Append(facts?.Frame ?? 0).Append('|')
                .Append(facts?.DecisionFrame ?? 0).Append('|')
                .Append(facts?.LatestFrame ?? 0).Append('|')
                .Append(facts?.RetainedStartFrame ?? 0).Append('|')
                .Append(facts?.CommittedAgentEntityId ?? 0).Append('|')
                .Append(facts?.PreferredAgentEntityId ?? 0).Append('|')
                .Append(facts?.CommitGeneration ?? 0L).Append('|')
                .Append(facts?.AuthorityHash ?? 0L).Append('|')
                .Append(facts != null && facts.ChangedOnly ? '1' : '0')
                .Append(facts != null && facts.ParticipatedOnly ? '1' : '0')
                .Append(facts != null && facts.DeltaExpanded ? '1' : '0')
                .Append('|')
                .Append(facts != null ? (int)facts.ActiveTableTab : 0).Append('|');
            builder.Append(facts?.FactScrollOffset ?? 0).Append('|')
                .Append(facts?.CandidateScrollOffset ?? 0).Append('|')
                .Append(facts?.DeltaScrollOffset ?? 0).Append('|');
            AppendToken(builder, facts?.HeaderText);
            AppendToken(builder, facts?.ContextText);
            AppendToken(builder, facts?.HistoryStateText);
            AppendToken(builder, facts?.DiagnosticCode);
            AppendToken(builder, facts?.SearchText);
            AppendToken(builder, facts?.SelectedStableId);
            AppendFactsAgents(builder, facts?.Agents);
            AppendCausalSteps(builder, facts?.CausalFlow);
            AppendCommitPhases(builder, facts?.CommitPhases);
            AppendFactRows(builder, facts?.FactRows);
            AppendFactRows(builder, facts?.DeltaRows);
            AppendCandidateRows(builder, facts?.CandidateRows);
        }

        private static void AppendFactsAgents(
            StringBuilder builder,
            CharacterTestRuntimeAiLabFactsAgentViewModel[] rows)
        {
            int count = rows?.Length ?? 0;
            builder.Append(count).Append('|');
            for (int i = 0; i < count; i++)
            {
                CharacterTestRuntimeAiLabFactsAgentViewModel row = rows[i];
                builder.Append(row?.EntityId ?? 0).Append('|')
                    .Append(row != null && row.Selected ? '1' : '0').Append('|');
                AppendToken(builder, row?.DisplayName);
                AppendToken(builder, row?.StateText);
            }
        }

        private static void AppendCausalSteps(
            StringBuilder builder,
            CharacterTestRuntimeAiLabFactsCausalStepViewModel[] rows)
        {
            int count = rows?.Length ?? 0;
            builder.Append(count).Append('|');
            for (int i = 0; i < count; i++)
            {
                CharacterTestRuntimeAiLabFactsCausalStepViewModel row = rows[i];
                AppendToken(builder, row?.StableId);
                AppendToken(builder, row?.Title);
                AppendToken(builder, row?.StepNumber);
                AppendToken(builder, row?.PrimaryText);
                AppendToken(builder, row?.DetailText);
                builder.Append(row != null ? (int)row.Status : 0).Append('|')
                    .Append(row != null && row.Selected ? '1' : '0').Append('|');
            }
        }

        private static void AppendCommitPhases(
            StringBuilder builder,
            CharacterTestRuntimeAiLabFactsCommitPhaseViewModel[] rows)
        {
            int count = rows?.Length ?? 0;
            builder.Append(count).Append('|');
            for (int i = 0; i < count; i++)
            {
                CharacterTestRuntimeAiLabFactsCommitPhaseViewModel row = rows[i];
                AppendToken(builder, row?.StableId);
                AppendToken(builder, row?.Title);
                AppendToken(builder, row?.StateCode);
                AppendToken(builder, row?.ReasonCode);
                builder.Append(row?.EvidenceFrame ?? -1).Append('|')
                    .Append(row != null ? (int)row.Status : 0).Append('|')
                    .Append(row != null && row.Selected ? '1' : '0').Append('|');
            }
        }

        private static void AppendFactRows(
            StringBuilder builder,
            CharacterTestRuntimeAiLabFactsFactViewModel[] rows)
        {
            int count = rows?.Length ?? 0;
            builder.Append(count).Append('|');
            for (int i = 0; i < count; i++)
            {
                CharacterTestRuntimeAiLabFactsFactViewModel row = rows[i];
                AppendToken(builder, row?.StableId);
                AppendToken(builder, row?.DisplayName);
                AppendToken(builder, row?.ValueText);
                AppendToken(builder, row?.SourceText);
                AppendToken(builder, row?.ChangeText);
                AppendToken(builder, row?.ParticipationText);
                builder.Append(row?.LastChangedFrame ?? -1).Append('|')
                    .Append(row != null && row.Changed ? '1' : '0')
                    .Append(row != null && row.Pinned ? '1' : '0')
                    .Append(row != null && row.Selected ? '1' : '0')
                    .Append(row != null && row.Visible ? '1' : '0')
                    .Append('|');
            }
        }

        private static void AppendCandidateRows(
            StringBuilder builder,
            CharacterTestRuntimeAiLabFactsCandidateViewModel[] rows)
        {
            int count = rows?.Length ?? 0;
            builder.Append(count).Append('|');
            for (int i = 0; i < count; i++)
            {
                CharacterTestRuntimeAiLabFactsCandidateViewModel row = rows[i];
                AppendToken(builder, row?.StableId);
                AppendToken(builder, row?.LayerText);
                AppendToken(builder, row?.StateText);
                AppendToken(builder, row?.ScoreText);
                AppendToken(builder, row?.PriorityText);
                AppendToken(builder, row?.CostText);
                AppendToken(builder, row?.ReasonCode);
                AppendToken(builder, row?.RelatedFactsText);
                builder.Append(row?.StableRank ?? -1).Append('|')
                    .Append(row != null && row.Selected ? '1' : '0')
                    .Append(row != null && row.Visible ? '1' : '0')
                    .Append('|');
            }
        }
    }
}
