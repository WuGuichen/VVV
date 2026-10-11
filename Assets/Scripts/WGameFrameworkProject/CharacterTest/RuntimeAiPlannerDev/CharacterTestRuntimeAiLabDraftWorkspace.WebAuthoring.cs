#if UNITY_EDITOR
using System;
using Newtonsoft.Json;

namespace MxFramework.Game.CharacterTest
{
    internal sealed partial class CharacterTestRuntimeAiLabDraftWorkspace
    {
        /// <summary>Imports an unreferenced author draft without replacing existing cards or changing groups.</summary>
        internal bool TryImportStandaloneTacticalDraft(CharacterTestRuntimeAiLabBehaviorCardDraft source,
            int expectedRevision, out string message)
        {
            message = string.Empty;
            if (IsReadOnly || expectedRevision != CurrentRevision || source?.HasAuthoredSource != true
                || !Guid.TryParse(source.CardDraftId, out _) || string.IsNullOrWhiteSpace(source.PublishedStableId)
                || source.SourcePublishedRevision != 0 || _cards.ContainsKey(source.CardDraftId)
                || System.Linq.Enumerable.Any(_cards.Values, card => card.PublishedStableId == source.PublishedStableId))
            { message = "AUTHOR_IMPORT_CONFLICT: 当前工作区已变化、只读或卡身份已存在。"; return false; }
            WorkspaceState before = null;
            try
            {
                var owned = source.Clone(newIdentity: false);
                CharacterTestTacticalPublishedSource.CaptureDraft(this, owned);
                before = CaptureState();
                _cards.Add(owned.CardDraftId, owned);
                TouchWorkspace(); PushUndo(before, "导入独立战术卡作者源");
                message = "作者源已导入；尚未发布。";
                return true;
            }
            catch (Exception error) when (error is ArgumentException || error is InvalidOperationException || error is JsonException)
            { if (before != null) RestoreState(before); message = error.Message; return false; }
        }

        /// <summary>One existing workspace transaction owns the Web form's source and envelope.</summary>
        /// <remarks>LowFreqAlloc: explicit Editor authoring only; the caller persists through DraftPersistence.</remarks>
        internal bool TryEditStandaloneTacticalSource(CharacterTestTacticalEditToken token, string json,
            string displayName, int cost, float cooldown, out string message)
        {
            message = string.Empty;
            if (IsReadOnly || token.WorkspaceId != WorkspaceId || token.WorkspaceRevision != CurrentRevision
                || !_cards.TryGetValue(token.CardId ?? string.Empty, out var card) || !card.HasAuthoredSource
                || token.CardRevision != card.CurrentRevision)
            { message = "AUTHOR_SOURCE_CONFLICT: 作者工作区版本已变化。"; return false; }
            if (string.IsNullOrWhiteSpace(displayName) || displayName.Length > 512 || cost < 0
                || cost > CharacterTestRuntimeAiPlannerPublishedBehaviorCardPublisher.MaximumPlayerCardCost
                || float.IsNaN(cooldown) || float.IsInfinity(cooldown) || cooldown < 0 || cooldown * 60d >= int.MaxValue)
            { message = "INVALID_CARD_METADATA: 请检查名称、费用和冷却时间。"; return false; }
            WorkspaceState before = null;
            try
            {
                var composition = card.HasCompositionSource ? CharacterTestTacticalCompositionJson.Read(json) : null;
                var tactical = composition == null ? CharacterTestTacticalAuthoringJson.Read(json) : null;
                string canonical = composition != null ? CharacterTestTacticalCompositionJson.Write(composition) : CharacterTestTacticalAuthoringJson.Write(tactical);
                if (canonical == (card.HasCompositionSource ? card.CompositionSourceJson : card.TacticalSourceJson)
                    && card.DisplayName == displayName.Trim() && card.Cost == cost && card.CooldownSeconds == cooldown) return true;
                before = CaptureState();
                if (composition != null) card.SetCompositionSource(composition); else card.SetTacticalSource(tactical);
                card.DisplayName = displayName.Trim(); card.Cost = cost; card.CooldownSeconds = cooldown;
                card.CurrentRevision++; card.LastCompileStatus = "WebAuthorSourceChanged"; TouchWorkspace();
                PushUndo(before, "编辑战术卡作者源");
                return true;
            }
            catch (Exception error) when (error is ArgumentException || error is InvalidOperationException || error is JsonException || error is OverflowException)
            { if (before != null) RestoreState(before); message = error.Message; return false; }
        }
    }
}
#endif
