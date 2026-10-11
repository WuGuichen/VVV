using System.Collections.Generic;
using MxFramework.Game.CharacterTest;
using MxFramework.Game.CharacterTest.TacticalCard;
using NUnit.Framework;

namespace MxFramework.Tests.Game.CharacterTest
{
    public sealed class CharacterTestUtilityCardFaceTests
    {
        [TestCase("bc_46mffvm25x", 1)]
        [TestCase("bc_jx3ssp6k3v", 2)]
        [TestCase("bc_k4mqws81ka", 0)]
        [TestCase("bc_t1a9wyxth4", 1)]
        [TestCase("bc_ncshwbb85f", 0)]
        public void UtilityQualityUsesSharedPresentationConfiguration(string cardId, int expected)
        {
            using (var skin = new CharacterTestFairyGuiTacticalCardSkin())
                Assert.AreEqual(expected, skin.ResolveQuality(cardId));
        }

        [Test]
        public void UtilityCardTextIsSharedWithoutClassificationOrTurnFooter()
        {
            var display = CharacterTestTacticalUtilityFace.Create(new InteractionPreparationPayload(TacticalInteractionKind.PursuitMark));
            Assert.AreEqual("下一次有效命中标记目标\n追加 8 基础伤害", display.CardText);
            Assert.AreEqual(display.CardText, (string)Newtonsoft.Json.Linq.JObject.Parse(display.ToJson())["cardText"]);
            StringAssert.DoesNotContain("本回合", display.CardText);
            StringAssert.DoesNotContain("功能卡", display.CardText);
            Assert.NotNull(UnityEngine.Resources.Load<UnityEngine.Material>("MxFramework/UI/CardTheme/AuthoredSilver"));
        }

        [TestCase(1, "8", "UtilityPursuitMark")]
        [TestCase(2, "3", "UtilityMomentumRecovery")]
        [TestCase(3, "12", "UtilityDefensePreparation")]
        [TestCase(4, "", "UtilityReturnMark")]
        [TestCase(5, "3", "UtilityHurtCharge")]
        public void FrozenUtilityPayloadHasDisplayWithoutActionBindings(int kind, string value, string illustration)
        {
            var payload = new InteractionPreparationPayload((TacticalInteractionKind)kind);
            var definition = new TacticalUtilityCardDefinition("test", "Misleading action name", "stale text", 7, payload, CardDestination.Exhaust);
            var data = CharacterTestTacticalCardPresentationProjection.FromDeployment(definition, null, null, null,
                MxFramework.UI.MxUiNullTextProvider.Instance, 1d / 60);
            Assert.NotNull(data.UtilityFace);
            Assert.AreEqual(value, data.UtilityFace.Value); Assert.AreEqual(illustration, data.UtilityFace.Illustration);
            Assert.AreEqual(7, data.Cost, "Cost remains the actual definition value, not the trial example.");
            Assert.AreEqual(TacticalInteractionTrialCards.Description(payload.Effect), data.Description);
            Assert.AreEqual("本回合 · 触发一次", data.UtilityFace.Lifetime);
            if (kind == 4) StringAssert.Contains("尝试", data.UtilityFace.Effect);
        }

        [Test]
        public void ReusingProjectionForActionClearsFormerUtilityDisplay()
        {
            var data = new CharacterTestCardUiPreviewData { UtilityFace = CharacterTestTacticalUtilityFace.Create(
                new InteractionPreparationPayload(TacticalInteractionKind.MomentumRecovery)) };
            var movement = CharacterTestTacticalAuthoringTemplates.CreateMovement(CharacterTestRuntimeAiLabMovementStrategy.MaintainDistance);
            movement.Schema = CharacterTestTacticalAuthoringSource.CurrentSchema; movement.Version = 2;
            var json = Newtonsoft.Json.Linq.JObject.Parse("{\"schema\":\"" + CharacterTestTacticalPublishedSource.Schema
                + "\",\"legacy\":" + CharacterTestTacticalAuthoringJson.Write(movement) + "}");
            var source = CharacterTestTacticalPublishedSource.Read(json);
            var document = CharacterTestTacticalCardPresentationProjection.Document("action", "Movement", source);
            CharacterTestTacticalCardPresentationProjection.Project(source, document, data,
                CharacterTestTacticalCardInputs.BindSlots(0, new Dictionary<string, CharacterTestTacticalCardInputs>()),
                null, null, MxFramework.UI.MxUiNullTextProvider.Instance, 1d / 60, new List<string>());
            Assert.IsNull(data.UtilityFace, "An action face must not retain an earlier utility trigger or value.");
        }
    }
}
