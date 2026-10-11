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
        private static readonly string[] InteractionTrialStableIds = { "bc_46mffvm25x", "bc_k4mqws81ka", "bc_t1a9wyxth4", "bc_jx3ssp6k3v", "bc_ncshwbb85f" };

        [MenuItem("MxFramework/CharacterTest/Tactical/Publish Fixed Interaction Trials")]
        private static void PublishFixedInteractionTrialsFromMenu()
        {
            try { Debug.Log(PublishFixedInteractionTrials().ToString(Formatting.Indented)); }
            catch (Exception error) { Debug.LogError("[InteractionTrials] " + error.Message); }
        }

        // Explicit cold authoring entry through the existing draft, numeric identity and publication owners.
        // It creates no inventory instances, groups, equipped bindings, scenes or player rule editor.
        internal static JObject PrepareFixedInteractionTrials()
        {
            Need(Ready, "EDITOR_BUSY");
            var identities = new JArray();
            using (var context = new AuthoringContext("main"))
                foreach (TacticalInteractionKind kind in Enum.GetValues(typeof(TacticalInteractionKind)))
                {
                    var card = context.EnsureFixedInteractionTrial(kind);
                    int numeric = CharacterTestRuntimeAiPlannerPublishedBehaviorCardNumericIdentity.AllocateForAuthoring(card.PublishedStableId);
                    identities.Add(new JObject { ["effect"] = kind.ToString(), ["stableId"] = card.PublishedStableId,
                        ["numericId"] = numeric, ["active"] = CharacterTestAuthoredContentIds.IsActive(417,numeric) });
                }
            return new JObject { ["identities"] = identities, ["identitiesReady"] = identities.All(t => (bool)t["active"]) };
        }

        internal static JObject PublishFixedInteractionTrials()
        {
            var prepared = PrepareFixedInteractionTrials();
            Need((bool?)prepared["identitiesReady"] == true, "INTERACTION_IDENTITY_RELOAD_REQUIRED");
            var results = new JArray();
            foreach (TacticalInteractionKind kind in Enum.GetValues(typeof(TacticalInteractionKind)))
                using (var context = new AuthoringContext("main"))
                {
                    var card = context.EnsureFixedInteractionTrial(kind);
                    context.ReconcileFixedInteractionTrial(card);
                    var request = new JObject { ["scope"] = "main", ["cardId"] = card.CardDraftId,
                        ["snapshotToken"] = context.Library()["snapshotToken"],
                        ["configuration"] = new JObject { ["characterId"] = CharacterTestPlayableCharacterCatalog.Discover().First().CharacterId,
                            ["actions"] = new JObject() } };
                    var plan = context.Preflight(request);
                    results.Add((bool?)plan["hasChanges"] == false
                        ? new JObject { ["published"] = true, ["stableId"] = card.PublishedStableId,
                            ["revision"] = card.SourcePublishedRevision, ["contentHash"] = card.SourcePublishedContentHash.ToString() }
                        : context.Publish(new JObject { ["scope"] = "main", ["confirmed"] = true,
                            ["planToken"] = plan["planToken"] }, Hash(Guid.NewGuid().ToString("N"))));
                }
            return new JObject { ["cards"] = results };
        }

        private sealed partial class AuthoringContext
        {
            internal void ReconcileFixedInteractionTrial(CharacterTestRuntimeAiLabBehaviorCardDraft card)
            {
                string path = JournalPath(card.CardDraftId);
                if (!System.IO.File.Exists(path)) return;
                var journal = Read(path);
                Need((string)journal["stableId"] == card.PublishedStableId, "INTERACTION_RECOVERY_IDENTITY_CONFLICT");
                Reconcile(new JObject { ["cardId"] = card.CardDraftId, ["recoveryOperationId"] = journal["operationId"] });
            }
            internal CharacterTestRuntimeAiLabBehaviorCardDraft EnsureFixedInteractionTrial(TacticalInteractionKind kind)
            {
                var existing = _workspace.Cards.Values.Where(c => c.HasTacticalSource
                    && c.CloneTacticalSource().InteractionPreparation?.Freeze().Effect == kind).ToArray();
                Need(existing.Length <= 1, "INTERACTION_DUPLICATE_FORMAL_SOURCE");
                if (existing.Length == 1)
                {
                    Need(existing[0].Cost == 5 && existing[0].CooldownSeconds == 0f, "INTERACTION_FIXED_RULE_CONFLICT");
                    return existing[0];
                }
                var published = _catalog.CardCatalog.Cards.Where(c => c.TacticalSource?.InteractionPreparation?.Effect == kind).ToArray();
                Need(published.Length == 0, "INTERACTION_PUBLISHED_SOURCE_NOT_IMPORTED");
                string stable = InteractionTrialStableIds[(int)kind-1];
                Need(!_workspace.Cards.Values.Any(c => c.PublishedStableId == stable)
                    && !_catalog.CardCatalog.Cards.Any(c => c.StableCardId == stable), "INTERACTION_STABLE_ID_CONFLICT");
                var draft = new CharacterTestRuntimeAiLabBehaviorCardDraft(Guid.NewGuid().ToString("D"),stable,
                    TacticalInteractionTrialCards.Name(kind) + "（试用）", "Tactical",null)
                    { IconKey="ui.behavior-card.custom", Cost=5, CooldownSeconds=0 };
                draft.SetTacticalSource(CharacterTestTacticalAuthoringTemplates.CreateInteractionPreparation(kind));
                Need(_workspace.TryImportStandaloneTacticalDraft(draft,_workspace.CurrentRevision,out string issue),issue);
                Persist(); return _workspace.Cards[draft.CardDraftId];
            }
        }
    }
}
