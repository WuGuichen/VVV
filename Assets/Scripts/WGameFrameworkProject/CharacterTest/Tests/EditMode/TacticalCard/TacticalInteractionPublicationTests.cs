using System;
using System.Linq;
using MxFramework.CharacterApplication;
using MxFramework.Game.CharacterTest;
using MxFramework.Game.CharacterTest.TacticalCard;
using MxFramework.Gameplay;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace MxFramework.Tests.Game.CharacterTest
{
    public sealed class TacticalInteractionPublicationTests
    {
        internal static CharacterTestRuntimeAiPlannerPublishedBehaviorCard Publish(TacticalInteractionKind kind, string id, int numeric)
        {
            var source = CharacterTestTacticalAuthoringTemplates.CreateInteractionPreparation(kind);
            var draft = new CharacterTestRuntimeAiLabBehaviorCardDraft("fixture-" + id, id, TacticalInteractionTrialCards.Name(kind) + "（试用）", "Tactical", null)
            { Cost = 5, CooldownSeconds = 0 };
            draft.SetTacticalSource(source);
            var inputs = CharacterTestTacticalCardInputs.Unbound(101);
            Assert.IsTrue(CharacterTestTacticalAuthoringCompiler.TryCompile(source, inputs, out var compiled, out var errors), string.Join(";", errors));
            var frozen = CharacterTestTacticalFrozenSource.CaptureLegacy(null, draft, inputs, compiled);
            var program = compiled.Program.WithTacticalSource(frozen);
            var result = CharacterTestRuntimeAiPlannerPublishedBehaviorCardPublisher.ValidateAndCreate(draft,
                new CharacterTestRuntimeAiPlannerPublishedBehaviorCardPublishRequest(id, 1, 8, 8,
                    new CharacterTestRuntimeAiPlannerPublishedBehaviorCardMetadata(draft.DisplayName, "ui.behavior-card.custom", 5, "", "", ""), numeric),
                new CharacterTestRuntimeAiLabBehaviorCardReferenceCatalog(null, null),
                new CharacterTestRuntimeAiLabBehaviorCardLogicCompileResult(program, Array.Empty<CharacterTestRuntimeAiLabCardDiagnostic>()));
            Assert.IsTrue(result.Success, string.Join(";", result.Diagnostics.Select(d => d.Code)));
            return result.Card;
        }

        [Test] public void AllFiveUseRealCompilerPublisherExactRestoreAndFixedDescription()
        {
            foreach (TacticalInteractionKind kind in Enum.GetValues(typeof(TacticalInteractionKind)))
            {
                var card = Publish(kind, "fixture.prep" + (int)kind, 1);
                Assert.AreEqual(kind, card.TacticalSource.InteractionPreparation.Effect);
                Assert.AreEqual(5, card.Metadata.Cost); Assert.AreEqual(0, card.Metadata.CooldownSeconds);
                StringAssert.Contains("600", card.Metadata.UseSummary); StringAssert.Contains("不启动行动", card.Metadata.UseSummary);
                var restored = CharacterTestRuntimeAiPlannerPublishedBehaviorCardPublisher.Restore(card.StableCardId, 1, 8, 8,
                    card.Metadata, null, card.SemanticHash, card.ContentHash, null, card.TacticalSource, 1);
                Assert.IsTrue(restored.Success); Assert.AreEqual(card.ContentHash, restored.Card.ContentHash);
                Assert.IsFalse(card.TacticalSource.TryCompile(card.StableCardId, card.Metadata.DisplayName, 1, null, out _, out var issue));
                Assert.AreEqual("execution:InteractionPreparationAuthorityRequired", issue);
            }
        }

        [Test] public void PreparationSchemaRejectsRuleMutationAndMixedAuthorities()
        {
            var source = CharacterTestTacticalAuthoringTemplates.CreateInteractionPreparation(TacticalInteractionKind.PursuitMark);
            string json = CharacterTestTacticalAuthoringJson.Write(source);
            var token = JObject.Parse(json); token["interactionPreparation"]["ruleset"] = "custom";
            Assert.Throws<ArgumentException>(() => CharacterTestTacticalAuthoringJson.Read(token.ToString()));
            token = JObject.Parse(json); token["interactionPreparation"]["damage"] = 200;
            Assert.Throws<JsonSerializationException>(() => CharacterTestTacticalAuthoringJson.Read(token.ToString()));
            token = JObject.Parse(json); token["version"] = 4; token["schema"] = CharacterTestTacticalAuthoringSource.TimingSchema;
            Assert.Throws<JsonSerializationException>(() => CharacterTestTacticalAuthoringJson.Read(token.ToString()));
            source.PositionRecall = new TacticalPositionRecallAuthoring { Operation = "Record", LifetimeFrames = 600 };
            Assert.Throws<JsonSerializationException>(() => CharacterTestTacticalAuthoringJson.Read(CharacterTestTacticalAuthoringJson.Write(source)));
        }

        [Test] public void FormalUtilityIdentityProjectionAndDefaultDeckPayWithoutLabOptIn()
        {
            var published = Publish(TacticalInteractionKind.MomentumRecovery, "fixture.formal.recovery", 1);
            var catalog = new CharacterTacticalContentCatalog(); catalog.SetPublishedCards(new[] { published });
            var metadata = catalog.Cards.Single(); var reference = new CharacterTacticalCardReference {
                CardId = metadata.CardId, Revision = metadata.Revision, ContentHash = metadata.ContentHash, InstanceId = 19 };
            var definition = TacticalDeployment.CreateDefinition(reference, metadata);
            Assert.IsInstanceOf<TacticalUtilityCardDefinition>(definition); Assert.AreEqual(TacticalCardKind.Utility, definition.Kind);
            Assert.AreEqual(reference.CardId, definition.PublishedCardId); Assert.AreEqual(reference.ContentHash, definition.PublishedContentHash);
            Assert.AreEqual(19, definition.DeckEntryInstanceId); Assert.AreEqual(0, metadata.Slots.Length);
            var display = TacticalCardDisplayData.FromDefinition(definition);
            Assert.AreEqual(reference.ContentHash, display.PublishedContentHash);
            var deck = new TacticalDeckManager(1); deck.InitializeDeck(new[] { definition }, 73);
            var ledger = new MomentumLedger(50); var card = deck.Hand.Single();
            Assert.AreEqual(CardPlayFailure.None, deck.SetPendingAction(card, ledger)); Assert.AreEqual(5, ledger.Reserved);
            Assert.AreEqual(CardPlayFailure.None, deck.CommitPendingTestPreparation(ledger));
            Assert.AreEqual(45, ledger.Current); Assert.AreEqual(5, deck.PaidCardMomentum);
            Assert.AreEqual(CardZone.ExhaustPile, card.Zone); Assert.IsNull(deck.ResolvingAction); Assert.IsNull(deck.PendingAction);
            var authority = new TacticalInteractionEffectAuthority(); var actor = new GameplayEntityId(1,1);
            authority.AdvanceTurn(10,1,actor); authority.ActivateValidated(TacticalInteractionKind.MomentumRecovery,100,default,0,0,0,card.InstanceId,card.Version-1);
            Assert.IsTrue(TacticalInteractionEffectAuthority.ValidateSessionJoin(authority.Capture(), deck,10,1,100,actor));
            var stale = authority.Capture(); stale.Slots[1].SourceVersion++;
            Assert.IsFalse(TacticalInteractionEffectAuthority.ValidateSessionJoin(stale,deck,10,1,100,actor));
            reference.ContentHash = "stale";
            Assert.Throws<ArgumentException>(() => TacticalDeployment.CreateDefinition(reference,metadata));
        }

        [Test] public void FormalRestoreRejectsAlteredCostAndCooldownEvenWithNewMatchingHash()
        {
            var card = Publish(TacticalInteractionKind.HurtCharge,"fixture.formal.hurt",1);
            foreach (var metadata in new[] {
                new CharacterTestRuntimeAiPlannerPublishedBehaviorCardMetadata(card.Metadata.DisplayName,"",4,"","",""),
                new CharacterTestRuntimeAiPlannerPublishedBehaviorCardMetadata(card.Metadata.DisplayName,"",5,"","","",cooldownSeconds:1) })
            {
                long changed = CharacterTestRuntimeAiPlannerPublishedBehaviorCardPublisher.ComputeContentHash(card.StableCardId,1,8,8,metadata,card.SemanticHash);
                var result = CharacterTestRuntimeAiPlannerPublishedBehaviorCardPublisher.Restore(card.StableCardId,1,8,8,metadata,null,card.SemanticHash,changed,null,card.TacticalSource,1);
                Assert.IsFalse(result.Success);
            }
        }

        [Test] public void AllFiveSurviveActualWorkspaceFlushAndReload()
        {
            string root = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "mx-fixed-interaction-workspace-" + Guid.NewGuid().ToString("N"));
            try
            {
                var persistence = new CharacterTestRuntimeAiLabDraftPersistence(root);
                var workspace = persistence.OpenOrCreate(CharacterTestArenaMatchConfig.CreateDefault());
                foreach (TacticalInteractionKind kind in Enum.GetValues(typeof(TacticalInteractionKind)))
                {
                    var draft = new CharacterTestRuntimeAiLabBehaviorCardDraft(Guid.NewGuid().ToString("D"), "fixture.persist" + (int)kind, kind.ToString(), "Tactical", null)
                        { Cost = 5, CooldownSeconds = 0 };
                    draft.SetTacticalSource(CharacterTestTacticalAuthoringTemplates.CreateInteractionPreparation(kind));
                    Assert.IsTrue(workspace.TryImportStandaloneTacticalDraft(draft, workspace.CurrentRevision, out var issue), issue);
                }
                persistence.MarkDirty();
                Assert.IsTrue(persistence.Flush(), persistence.LastStatus);
                Assert.IsTrue(persistence.TryLoad(workspace.WorkspaceId, out var loaded, out var message), message);
                foreach (TacticalInteractionKind kind in Enum.GetValues(typeof(TacticalInteractionKind)))
                {
                    var draft = loaded.Cards.Values.Single(c => c.PublishedStableId == "fixture.persist" + (int)kind);
                    Assert.AreEqual(5, draft.Cost); Assert.AreEqual(0f, draft.CooldownSeconds);
                    var source = draft.CloneTacticalSource();
                    Assert.IsTrue(source.HasPreparationVersion);
                    Assert.AreEqual(kind, source.InteractionPreparation.Freeze().Effect);
                    Assert.AreEqual(CharacterTestTacticalAuthoringJson.Write(workspace.Cards.Values.Single(c => c.PublishedStableId == "fixture.persist" + (int)kind).CloneTacticalSource()), CharacterTestTacticalAuthoringJson.Write(source));
                }
            }
            finally { if (System.IO.Directory.Exists(root)) System.IO.Directory.Delete(root, true); }
        }

        [Test] public void PausedConfigurationCapturesCurrentDefinitionIdentityWithoutNewEnvelopeFields()
        {
            var participants = new[] {
                new TacticalDeploymentParticipant("player",1,"char.000001","Player",1,true,default,0,
                    Array.Empty<TacticalCardDefinition>(),null),
                new TacticalDeploymentParticipant("enemy",2,"char.000002","Enemy",2,false,default,0,
                    Array.Empty<TacticalCardDefinition>(),null) };
            var deployment = TacticalDeployment.CreateLabTrial(participants,50,new TacticalCardTrialOptions(1,5,1,
                new CharacterTestRuntimeAiPlannerProjectCatalogState(null,null,"","","","","")));
            string config = CharacterTestPausedSessionCodec.CaptureConfiguration(null,deployment);
            var root = JObject.Parse(config);
            Assert.AreEqual(CharacterTestAuthoredContentIds.Revision,(int)root["DefinitionIdentityCatalog"]["Revision"]);
            Assert.AreEqual(CharacterTestAuthoredContentIds.CatalogHash,(string)root["DefinitionIdentityCatalog"]["Hash"]);
            CharacterTestPausedSessionCodec.ValidateConfigurationIdentity(config);
            root["DefinitionIdentityCatalog"]["Hash"] = "sha256:old-trial-registry";
            Assert.Throws<InvalidOperationException>(() => CharacterTestPausedSessionCodec.ValidateConfigurationIdentity(root.ToString()));
        }

        [TestCase("missing")][TestCase("old-hash")][TestCase("old-revision")]
        [TestCase("numeric-only")][TestCase("duplicate")][TestCase("malformed")]
        public void OldPausedIdentityRejectsBeforeContextFactoriesOrWorldAllocation(string mode)
        {
            var root = new JObject { ["DefinitionIdentityCatalog"] = new JObject {
                ["Revision"] = CharacterTestAuthoredContentIds.Revision,
                ["Hash"] = CharacterTestAuthoredContentIds.CatalogHash } };
            if (mode=="missing") root.Remove("DefinitionIdentityCatalog");
            if (mode=="old-hash") root["DefinitionIdentityCatalog"]["Hash"] = "sha256:a77b86910d9dcd7c8123f0a88656d29a2f76ea07f11916251a2f97e69bc72d7f";
            if (mode=="old-revision") root["DefinitionIdentityCatalog"]["Revision"] = 1208;
            if (mode=="numeric-only") { root.Remove("DefinitionIdentityCatalog"); root["CardNumericIds"] = new JArray(45,46,47,48,49); }
            string config = root.ToString();
            if (mode=="duplicate") config = "{\"DefinitionIdentityCatalog\":{},\"DefinitionIdentityCatalog\":" + root["DefinitionIdentityCatalog"] + "}";
            if (mode=="malformed") config = "{";
            // Null context and previous world prove that rejection precedes all context/factory use.
            var error = Assert.Throws<InvalidOperationException>(() => CharacterTestPausedSessionCodec.Prepare(
                new CharacterTestPausedSessionDocument {Config=config},null,null));
            StringAssert.Contains("actual-definition-identity-mismatch",error.Message);
        }

        [Test] public void LegacyV4RecallSourceRetainsExactCanonicalSemanticHash()
        {
            var source = CharacterTestTacticalAuthoringTemplates.CreatePositionRecall("", new TacticalPositionRecallAuthoring { Operation="Record",LifetimeFrames=600 });
            var draft = new CharacterTestRuntimeAiLabBehaviorCardDraft("recall","bc_3gdyznnddp","", "Tactical",null);
            draft.SetTacticalSource(source);
            var published = CharacterTestTacticalPublishedSource.CaptureDraft(null,draft);
            Assert.AreEqual(6132646884257649900L,published.SemanticHash);
        }
    }
}
