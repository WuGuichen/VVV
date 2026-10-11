using System;
using System.Collections.Generic;

namespace MxFramework.Game.CharacterTest
{
    internal static partial class CharacterTestRuntimeAiPlannerPublishedBehaviorCardPublisher
    {
        private static CharacterTestRuntimeAiPlannerPublishedBehaviorCardPublishResult PublishTactical(
            CharacterTestRuntimeAiLabBehaviorCardDraft draft, CharacterTestRuntimeAiPlannerPublishedBehaviorCardPublishRequest request,
            CharacterTestRuntimeAiLabBehaviorCardLogicCompileResult compiled)
        {
            var diagnostics = new List<CharacterTestRuntimeAiLabCardDiagnostic>();
            var frozen = compiled?.Program?.TacticalSource;
            if (request == null || compiled?.Success != true || frozen == null
                || frozen.CardId != draft.CardDraftId || frozen.CardRevision != draft.CurrentRevision)
            {
                Error(diagnostics, "PUBLISHED_TACTICAL_COMPILE_REQUIRED", "$.source", "发布需要当前作者源、共享依赖和实际装配的成功编译。");
                return Failed(diagnostics);
            }
            var original = request.Metadata;
            if (original == null) { Error(diagnostics, "PUBLISHED_CARD_METADATA_MISSING", "$.metadata", "Metadata is required."); return Failed(diagnostics); }
            var source = CharacterTestTacticalPublishedSource.Capture(frozen);
            var capabilities = CharacterTestBehaviorExecutionUsage.Analyze(compiled.Program, true).Capabilities;
            if (source.PositionRecall != null || source.PointMove != null) capabilities = Array.AsReadOnly(new[] { CharacterTestRuntimeAiPlannerBehaviorCapability.Navigation });
            var description = source.InteractionPreparation != null ? CharacterTestTacticalDslDescription.GenerateInteractionPreparation(source.InteractionPreparation) : source.PositionRecall != null ? CharacterTestTacticalDslDescription.GeneratePositionRecall(source.PositionRecall) : source.PointMove?.IsGroundTraction == true
                ? CharacterTestTacticalDslDescription.GenerateGroundTraction(source.PointMove)
                : CharacterTestTacticalDslDescription.Generate(frozen.CloneDocument(), null);
            var metadata = new CharacterTestRuntimeAiPlannerPublishedBehaviorCardMetadata(original.DisplayName, original.IconKey,
                original.Cost, (source.InteractionPreparation != null || source.PositionRecall != null || source.PointMove?.IsGroundTraction == true) ? description.Text : source.PointMove != null ? "前往本次选定地点。" : description.Text,
                (source.InteractionPreparation != null || source.PositionRecall != null || source.PointMove?.IsGroundTraction == true) ? description.Details : source.PointMove != null ? "选择范围内可达的地点；开始移动时消耗动势。" : description.Details,
                source.InteractionPreparation != null || source.PositionRecall != null ? string.Join("; ", description.Warnings) : source.PointMove != null ? "开始前失败会释放预留；开始后受阻、超时或中断保留消耗。" : string.Join("; ", description.Warnings),
                original.Tags, capabilities, original.Responsibilities, source.InteractionPreparation != null || source.PositionRecall != null || source.PointMove != null ? 0f : original.CooldownSeconds);
            ValidateEnvelope(request.StableCardId, request.Revision, request.MinimumRuntimeContractVersion,
                request.MaximumRuntimeContractVersion, metadata, diagnostics);
            if (CharacterTestRuntimeAiLabBehaviorCardLogicValidator.HasErrors(diagnostics)) return Failed(diagnostics);
            bool pointAuthoring = source.InteractionPreparation != null || source.PositionRecall != null || source.PointMove != null || draft.HasTacticalSource && draft.CloneTacticalSource().ExecutionInput != null;
            if (pointAuthoring && (!draft.HasTacticalSource || draft.Cost != original.Cost || draft.CooldownSeconds != original.CooldownSeconds
                || CharacterTestTacticalPublishedSource.CaptureDraft(null, draft).SemanticHash != source.SemanticHash))
            {
                Error(diagnostics, "PUBLISHED_POINT_AUTHORING_STALE", "$.source", "Point source, cost and cooldown must match the current author draft.");
                return Failed(diagnostics);
            }
            if (source.InteractionPreparation != null && (original.Cost != TacticalCard.TacticalInteractionTrialCards.Cost || original.CooldownSeconds != 0f))
            { Error(diagnostics, "PUBLISHED_INTERACTION_COST_INVALID", "$.metadata.cost", "Fixed trial preparation cost must be 5."); return Failed(diagnostics); }
            long content = ComputeContentHash(request.StableCardId, request.Revision, request.MinimumRuntimeContractVersion,
                request.MaximumRuntimeContractVersion, metadata, source.SemanticHash);
            var card = new CharacterTestRuntimeAiPlannerPublishedBehaviorCard(request.StableCardId, request.Revision,
                request.MinimumRuntimeContractVersion, request.MaximumRuntimeContractVersion, metadata, null,
                source.SemanticHash, content, tacticalSource: source, numericId: request.NumericId);
            return new CharacterTestRuntimeAiPlannerPublishedBehaviorCardPublishResult(card, compiled.Program, diagnostics.ToArray());
        }

        private static CharacterTestRuntimeAiPlannerPublishedBehaviorCardPublishResult RestoreTactical(string id, int revision,
            int minimum, int maximum, CharacterTestRuntimeAiPlannerPublishedBehaviorCardMetadata metadata,
            CharacterTestTacticalPublishedSource source, long expectedSemantic, long expectedContent,
            int numericId = 0)
        {
            var diagnostics = new List<CharacterTestRuntimeAiLabCardDiagnostic>();
            ValidateEnvelope(id, revision, minimum, maximum, metadata, diagnostics);
            if (CharacterTestRuntimeAiLabBehaviorCardLogicValidator.HasErrors(diagnostics)) return Failed(diagnostics);
            source = CharacterTestTacticalPublishedSource.Read(source.ToJson());
            if (source.InteractionPreparation != null && (metadata.Cost != TacticalCard.TacticalInteractionTrialCards.Cost || metadata.CooldownSeconds != 0f))
            { Error(diagnostics, "PUBLISHED_INTERACTION_COST_INVALID", "$.metadata", "Fixed interaction cost/cooldown required."); return Failed(diagnostics); }
            long content = ComputeContentHash(id, revision, minimum, maximum, metadata, source.SemanticHash);
            if (source.SemanticHash != expectedSemantic || content != expectedContent)
            {
                Error(diagnostics, "PUBLISHED_TACTICAL_HASH_MISMATCH", "$.source", "固定作者源、共享依赖或发布内容 Hash 不一致。");
                return Failed(diagnostics);
            }
            return new CharacterTestRuntimeAiPlannerPublishedBehaviorCardPublishResult(new CharacterTestRuntimeAiPlannerPublishedBehaviorCard(
                id, revision, minimum, maximum, metadata, null, source.SemanticHash, content, tacticalSource: source,
                numericId: numericId), null, diagnostics.ToArray());
        }
    }
}
