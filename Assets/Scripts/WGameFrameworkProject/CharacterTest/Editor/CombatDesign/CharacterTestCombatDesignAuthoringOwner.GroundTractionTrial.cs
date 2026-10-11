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
        private const string GroundTractionTrialName = "定点牵引试用";

        [MenuItem("MxFramework/CharacterTest/Tactical/Publish Ground Traction Trial")]
        private static void PublishGroundTractionTrialFromMenu()
        {
            try { Debug.Log(PublishGroundTractionTrial().ToString(Formatting.Indented)); }
            catch (Exception error) { Debug.LogError("[GroundTractionTrial] " + error.Message); }
        }

        // LowFreqAlloc: an explicit Editor trial entry through the same authoring owners as Web publication.
        // It imports one unreferenced card, publishes an exact revision and never changes groups, decks or bindings.
        internal static JObject PublishGroundTractionTrial()
        {
            Need(Ready, "EDITOR_BUSY: 请在编辑模式就绪后发布定点牵引试用卡。");
            using (var context = new AuthoringContext("main"))
            {
                var card = context.EnsureGroundTractionTrial();
                context.ReconcileGroundTractionTrial(card);
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
            internal void ReconcileGroundTractionTrial(CharacterTestRuntimeAiLabBehaviorCardDraft card)
            {
                string path = JournalPath(card.CardDraftId);
                if (!System.IO.File.Exists(path)) return;
                var journal = Read(path);
                Need((string)journal["stableId"] == card.PublishedStableId && (string)journal["name"] == GroundTractionTrialName,
                    "TRIAL_RECOVERY_IDENTITY_CONFLICT");
                Reconcile(new JObject { ["cardId"] = card.CardDraftId, ["recoveryOperationId"] = journal["operationId"] });
            }

            internal CharacterTestRuntimeAiLabBehaviorCardDraft EnsureGroundTractionTrial()
            {
                var existing = _workspace.Cards.Values.Where(c => c.DisplayName == GroundTractionTrialName).ToArray();
                Need(existing.Length <= 1, "TRIAL_IDENTITY_CONFLICT: 存在多个同名试用草稿，请在作者入口核对。");
                if (existing.Length == 1)
                {
                    Need(existing[0].HasTacticalSource && existing[0].CloneTacticalSource().ExecutionInput?.Freeze().IsGroundTraction == true,
                        "TRIAL_SOURCE_CONFLICT: 同名草稿并非定点牵引，未覆盖。");
                    return existing[0];
                }
                string stableId = CharacterTestRuntimeAiPlannerProjectCatalogAuthoringSession.GenerateProjectCardStableId(
                    id => _workspace.Cards.Values.Any(c => c.PublishedStableId == id) || _catalog.CardCatalog.Cards.Any(c => c.StableCardId == id),
                    () => BitConverter.ToUInt64(Guid.NewGuid().ToByteArray(), 0));
                var draft = new CharacterTestRuntimeAiLabBehaviorCardDraft(Guid.NewGuid().ToString("D"), stableId,
                    GroundTractionTrialName, "Tactical", null) { IconKey = "ui.behavior-card.custom", Cost = 3, CooldownSeconds = 0 };
                draft.SetTacticalSource(CharacterTestTacticalAuthoringTemplates.CreatePointMove(GroundTractionTrialName,
                    new TacticalPointMoveAuthoring { Mechanism = "GroundTraction", StartPolicy = "OwnedTractionAccepted",
                        RangeMillimeters = 6000, ArrivalMillimeters = 250, MaximumFrames = 90, RequireReachable = true,
                        TractionSpeedMillimetersPerSecond = 9000, TractionAccelerationMillimetersPerSecondSquared = 54000 }));
                Need(_workspace.TryImportStandaloneTacticalDraft(draft, _workspace.CurrentRevision, out string issue), issue);
                Persist(); return _workspace.Cards[draft.CardDraftId];
            }
        }
    }
}