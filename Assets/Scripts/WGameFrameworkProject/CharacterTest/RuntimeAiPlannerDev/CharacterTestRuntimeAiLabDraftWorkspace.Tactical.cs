using System;

namespace MxFramework.Game.CharacterTest
{
    internal sealed partial class CharacterTestRuntimeAiLabBehaviorCardDraft
    {
        internal string TacticalSourceJson { get; private set; }
        internal bool HasTacticalSource => TacticalSourceJson != null;

        internal CharacterTestTacticalAuthoringSource CloneTacticalSource()
            => HasTacticalSource ? CharacterTestTacticalAuthoringJson.Read(TacticalSourceJson) : null;

        /// <remarks>LowFreqAlloc: exclusive authoring branch; callers commit through the workspace.</remarks>
        internal void SetTacticalSource(CharacterTestTacticalAuthoringSource source)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            var owned = source.Clone();
            owned.Name = string.Empty; // The card envelope owns the display name.
            string json = CharacterTestTacticalAuthoringJson.Write(owned);
            TacticalSourceJson = json;
            EnsureReservedTacticalIdentity();
            CompositionSourceJson = null;
            _logicPayload = null;
            _payload = null;
            LogicPayloadMigratedFromV1 = false;
        }
    }

    internal sealed partial class CharacterTestRuntimeAiLabDraftWorkspace
    {
        /// <summary>Imports a distinct source card into the selected group without changing existing cards.</summary>
        /// <remarks>LowFreqAlloc: explicit import transaction. New identities come from the existing draft owner.</remarks>
        internal bool TryImportTacticalSource(int teamId, int slotIndex, int expectedRevision,
            CharacterTestTacticalAuthoringSource source, out string cardId, out string message)
        {
            cardId = string.Empty;
            if (IsReadOnly || expectedRevision != CurrentRevision)
            { message = "工作副本已变化或只读，请重新导入。"; return false; }
            if (source == null || !TryGetGroupForSlot(teamId, slotIndex, out var group)
                || group.Entries.Count >= MaximumBehaviorGroupEntries)
            { message = "当前行为组不可用或已满。"; return false; }
            var card = new CharacterTestRuntimeAiLabBehaviorCardDraft(null, string.Empty,
                string.IsNullOrWhiteSpace(source.Name) ? "战术行为卡" : source.Name, "Tactical", null);
            card.SetTacticalSource(source);
            card.LastCompileStatus = "TacticalSourceImported";
            var before = CaptureState();
            if (CountGroupReferences(group.GroupDraftId) > 1)
            {
                var slot = TryGetSlot(teamId, slotIndex);
                group = group.Clone(newIdentity: true);
                _groups.Add(group.GroupDraftId, group);
                slot.GroupDraftId = group.GroupDraftId;
            }
            var runtimeEntry = new CharacterTestRuntimeAiPlannerBehaviorModuleEntry(
                CharacterTestRuntimeAiPlannerBehaviorCompositionCatalog.FoundationIdle,
                enabled: true, order: (group.Entries.Count + 1) * 10)
            { EntryId = card.CardDraftId };
            _cards.Add(card.CardDraftId, card);
            group.Entries.Add(new CharacterTestRuntimeAiLabBehaviorEntryDraft(null, card.CardDraftId,
                runtimeEntry.BehaviorId, runtimeEntry.Order, false, runtimeEntry));
            group.NormalizeOrders();
            Touch(group);
            PushUndo(before, "导入战术作者源");
            cardId = card.CardDraftId;
            message = "战术源已加入当前组；请选择新卡查看源时间轴。";
            return true;
        }
    }
}
