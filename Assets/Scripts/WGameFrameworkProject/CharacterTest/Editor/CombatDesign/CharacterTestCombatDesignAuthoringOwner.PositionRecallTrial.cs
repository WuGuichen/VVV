using System;
using System.Linq;
using MxFramework.Game.CharacterTest.TacticalCard;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;

namespace MxFramework.Game.CharacterTest.Editor
{
    internal static partial class CharacterTestCombatDesignAuthoringOwner
    {
        private static string PositionRecallTrialName(string operation) => operation == "Record" ? "记录回返点试用" : "返回记录点试用";

        [MenuItem("MxFramework/CharacterTest/Tactical/Publish Position Recall Trials")]
        private static void PublishPositionRecallTrialFromMenu()
        {
            try { Debug.Log(PublishPositionRecallTrials().ToString(Formatting.Indented)); }
            catch (Exception error) { Debug.LogError("[PositionRecallTrial] " + error.Message); }
        }

        // LowFreqAlloc: an explicit Editor trial entry through the same authoring owners as Web publication.
        // It imports two unreferenced cards, publishes exact revisions and never changes groups, decks or bindings.
        internal static JObject PreparePositionRecallTrials()
        {
            Need(Ready, "EDITOR_BUSY: 请在编辑模式就绪后准备位置回返试用卡。");
            using (var context = new AuthoringContext("main"))
            {
                var record = context.EnsurePositionRecallTrial("Record");
                var returning = context.EnsurePositionRecallTrial("Return");
                int recordId = CharacterTestRuntimeAiPlannerPublishedBehaviorCardNumericIdentity.AllocateForAuthoring(record.PublishedStableId);
                int returnId = CharacterTestRuntimeAiPlannerPublishedBehaviorCardNumericIdentity.AllocateForAuthoring(returning.PublishedStableId);
                return new JObject { ["recordId"] = recordId, ["returnId"] = returnId,
                    ["recordStableId"] = record.PublishedStableId, ["returnStableId"] = returning.PublishedStableId,
                    ["identitiesReady"] = CharacterTestAuthoredContentIds.IsActive(417, recordId) && CharacterTestAuthoredContentIds.IsActive(417, returnId) };
            }
        }

        internal static JObject PublishPositionRecallTrials()
        {
            var prepared = PreparePositionRecallTrials();
            Need((bool?)prepared["identitiesReady"] == true,
                "TRIAL_IDENTITY_RELOAD_REQUIRED: 两张草稿身份已分配；请先用既有 emit_character_ids.py 生成并完成脚本编译，再发布同一草稿。");
            return new JObject { ["record"] = PublishPositionRecallTrial("Record"), ["return"] = PublishPositionRecallTrial("Return") };
        }

        internal static JObject PublishPositionRecallTrial(string operation)
        {
            Need(operation == "Record" || operation == "Return", "TRIAL_OPERATION_INVALID");
            Need(Ready, "EDITOR_BUSY: 请在编辑模式就绪后发布位置回返试用卡。");
            using (var context = new AuthoringContext("main"))
            {
                var card = context.EnsurePositionRecallTrial(operation);
                context.ReconcilePositionRecallTrial(card, operation);
                int numericId = CharacterTestRuntimeAiPlannerPublishedBehaviorCardNumericIdentity.AllocateForAuthoring(card.PublishedStableId);
                Need(CharacterTestAuthoredContentIds.IsActive(417, numericId),
                    "TRIAL_IDENTITY_RELOAD_REQUIRED: 新卡身份已分配；请先用既有 emit_character_ids.py 生成并完成脚本编译，再发布同一草稿。");
                var configuration = new JObject { ["characterId"] = CharacterTestPlayableCharacterCatalog.Discover().First().CharacterId,
                    ["actions"] = new JObject() };
                var request = new JObject { ["scope"] = "main", ["cardId"] = card.CardDraftId,
                    ["snapshotToken"] = context.Library()["snapshotToken"], ["configuration"] = configuration };
                var plan = context.Preflight(request);
                if ((bool?)plan["hasChanges"] == false)
                    return new JObject { ["published"] = true, ["message"] = "试用卡已发布，无需创建重复版本。",
                        ["stableId"] = card.PublishedStableId, ["revision"] = card.SourcePublishedRevision,
                        ["contentHash"] = card.SourcePublishedContentHash.ToString(), ["detail"] = plan["detail"] };
                return context.Publish(new JObject { ["scope"] = "main", ["confirmed"] = true,
                    ["planToken"] = plan["planToken"] }, Hash(Guid.NewGuid().ToString("N")));
            }
        }

        private sealed partial class AuthoringContext
        {
            internal void ReconcilePositionRecallTrial(CharacterTestRuntimeAiLabBehaviorCardDraft card, string operation)
            {
                string path = JournalPath(card.CardDraftId);
                if (!System.IO.File.Exists(path)) return;
                var journal = Read(path);
                Need((string)journal["stableId"] == card.PublishedStableId && (string)journal["name"] == PositionRecallTrialName(operation),
                    "TRIAL_RECOVERY_IDENTITY_CONFLICT");
                Reconcile(new JObject { ["cardId"] = card.CardDraftId, ["recoveryOperationId"] = journal["operationId"] });
            }

            internal CharacterTestRuntimeAiLabBehaviorCardDraft EnsurePositionRecallTrial(string operation)
            {
                var existing = _workspace.Cards.Values.Where(c => c.DisplayName == PositionRecallTrialName(operation)).ToArray();
                Need(existing.Length <= 1, "TRIAL_IDENTITY_CONFLICT: 存在多个同名试用草稿，请在作者入口核对。");
                if (existing.Length == 1)
                {
                    Need(existing[0].HasTacticalSource && existing[0].CloneTacticalSource().PositionRecall?.Operation == operation,
                        "TRIAL_SOURCE_CONFLICT: 同名草稿并非位置回返，未覆盖。");
                    return existing[0];
                }
                string stableId = CharacterTestRuntimeAiPlannerProjectCatalogAuthoringSession.GenerateProjectCardStableId(
                    id => _workspace.Cards.Values.Any(c => c.PublishedStableId == id) || _catalog.CardCatalog.Cards.Any(c => c.StableCardId == id),
                    () => BitConverter.ToUInt64(Guid.NewGuid().ToByteArray(), 0));
                var draft = new CharacterTestRuntimeAiLabBehaviorCardDraft(Guid.NewGuid().ToString("D"), stableId,
                    PositionRecallTrialName(operation), "Tactical", null) { IconKey = "ui.behavior-card.custom", Cost = 3, CooldownSeconds = 0 };
                draft.SetTacticalSource(CharacterTestTacticalAuthoringTemplates.CreatePositionRecall(PositionRecallTrialName(operation),
                    new TacticalPositionRecallAuthoring { Operation = operation, LifetimeFrames = operation == "Record" ? 600 : 0 }));
                Need(_workspace.TryImportStandaloneTacticalDraft(draft, _workspace.CurrentRevision, out string issue), issue);
                Persist(); return _workspace.Cards[draft.CardDraftId];
            }
        }
    }
}
