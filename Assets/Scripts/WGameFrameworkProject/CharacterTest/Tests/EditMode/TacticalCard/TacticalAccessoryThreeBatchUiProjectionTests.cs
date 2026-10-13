using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using MxFramework.Game.CharacterTest;
using MxFramework.Game.CharacterTest.TacticalCard;
using MxFramework.Gameplay;
using MxFramework.Runtime;
using NUnit.Framework;

namespace MxFramework.Tests.Game.CharacterTest
{
    internal sealed class TacticalAccessoryThreeBatchUiProjectionTests
    {
        [TestCase(TacticalAccessoryCombatKind.NoTurningBack, "不许回头")]
        [TestCase(TacticalAccessoryCombatKind.NonLethalOath, "不杀之誓")]
        [TestCase(TacticalAccessoryCombatKind.OnePunch, "一拳之约")]
        [TestCase(TacticalAccessoryCombatKind.GlassCannon, "玻璃大炮")]
        [TestCase(TacticalAccessoryCombatKind.LuckyCoin, "幸运硬币")]
        [TestCase(TacticalAccessoryCombatKind.IronThrone, "铁王座")]
        public void LaterOfferUsesExactFrozenDefinitionAndExistingAttach(TacticalAccessoryCombatKind kind, string name)
        {
            var actual = CharacterTestRuntimeAiLabFixedAssembly.Definition(kind);
            var frozen = TacticalAccessoryThirdBatchPublication.CreateWithPreviousBatches(null).Configuration.Accessories.Single(d => d.Execution.Kind == kind);
            Assert.AreEqual(frozen.Reference.ContentHash, actual.Reference.ContentHash);
            var display = CharacterTestTacticalAccessoryDisplay.From(actual);
            Assert.AreEqual(name, display.Name); Assert.AreEqual(frozen.Execution.ConfirmationMomentumCost, display.ConfirmationCost);
            Assert.IsNotEmpty(display.Description); Assert.IsNotEmpty(display.RuleText);
            var selection = new CharacterTestRuntimeAiLabCardTrialSelection("fixture.main", "action:17") { FixedQuality = CharacterTacticalCardQuality.Silver };
            Assert.IsTrue(CharacterTestRuntimeAiLabFixedAssembly.TryEquip(selection, kind, -1, out _));
            var main = TacticalFixedAssemblyManifestTests.Main();
            CharacterTestRuntimeAiLabFixedAssembly.Attach(selection, main, 11, new Dictionary<string,int> { { "main.action", 17 } });
            Assert.AreEqual(kind, main.FixedAssembly.Accessories[0].Kind);
            Assert.AreEqual(frozen.Execution.ConfirmationMomentumCost, main.FixedAssembly.AccessoryConfirmationCost);
            Assert.AreEqual(display.Name, CharacterTestTacticalAccessoryDisplay.From(main.FixedAssembly.Accessories[0]).Name);
            var copy = selection.Clone(); Assert.IsTrue(CharacterTestRuntimeAiLabFixedAssembly.TryEquip(selection, TacticalAccessoryCombatKind.BorrowMomentum, 0, out _));
            Assert.AreEqual(kind, copy.AccessorySlots[0]); Assert.AreEqual(TacticalAccessoryCombatKind.BorrowMomentum, selection.AccessorySlots[0]);
        }
        [Test]
        public void ProjectionDoesNotGuessFeeOrUseAnOldFeeTable()
        {
            var definition = CharacterTestRuntimeAiLabFixedAssembly.Definition(TacticalAccessoryCombatKind.LuckyCoin);
            definition.Execution.ConfirmationMomentumCost = 8;
            Assert.AreEqual("费用 +8", CharacterTestTacticalAccessoryDisplay.From(definition).FeeText);
            definition.Execution.ConfirmationMomentumCost = null;
            Assert.AreEqual("费用未配置", CharacterTestTacticalAccessoryDisplay.From(definition).FeeText);
            Assert.AreEqual(1, CharacterTestRuntimeAiLabFixedAssembly.Definition(TacticalAccessoryCombatKind.LuckyCoin).Execution.ConfirmationMomentumCost);
        }
        [Test]
        public void LabNineDefinitionsMatchFrozenAuthoringJsonWithoutPublishingIt()
        {
            string path = Environment.GetEnvironmentVariable("MX_ACCESSORY_UI_FROZEN_CONFIG")
                ?? "Authoring/TacticalAccessories/tactical_card_assembly.double-edge-nine.v3.json";
            var config = Newtonsoft.Json.JsonConvert.DeserializeObject<CharacterTacticalAssemblyPublication>(File.ReadAllText(path));
            Assert.AreEqual(9, config.Configuration.Accessories.Length);
            foreach (var expected in config.Configuration.Accessories) {
                var actual = CharacterTestRuntimeAiLabFixedAssembly.Definition(expected.Execution.Kind);
                Assert.IsTrue(Newtonsoft.Json.Linq.JToken.DeepEquals(Newtonsoft.Json.Linq.JToken.FromObject(expected), Newtonsoft.Json.Linq.JToken.FromObject(actual)), expected.Reference.AccessoryId);
            }
        }
        private static readonly GameplayEntityId Self = new GameplayEntityId(1,1), Enemy = new GameplayEntityId(2,1);
        private static TacticalThirdBatchCommit Commit() => new TacticalThirdBatchCommit(7,1,101,Self,1001,1,new TacticalThirdBatchOrder(10,0,1,1,Self,1));
        private static TacticalAccessoryThirdBatchReadout Read(TacticalAccessoryThirdBatchState state, bool throne = false)
            => new TacticalAccessoryThirdBatchReadout(new TacticalDoubleEdgeCommitKey(7,1,101,Self,1001,10),state,throne);
        [TestCase(1u, TacticalThirdBatchCoinFace.SelfExecution)]
        [TestCase(8192u, TacticalThirdBatchCoinFace.EnemyNextAttack)]
        public void CoinReadoutKeepsPublishedFaceBeneficiaryPendingBindingAndExpiryWithoutDrawing(uint seed, TacticalThirdBatchCoinFace face)
        {
            var ledger = new TacticalThirdBatchCoinLedger(7,seed);
            Assert.IsTrue(ledger.TryPublish(true,Commit(),Enemy,out var receipt)); Assert.AreEqual(face,receipt.Face);
            var state = new TacticalAccessoryThirdBatchState(Commit(),false,false,receipt); var random = ledger.RandomState;
            var output = CharacterTestTacticalAccessoryStateDisplay.Describe(Read(state),new TacticalThirdBatchParameters(false,true,false),out string compact);
            StringAssert.Contains(face == TacticalThirdBatchCoinFace.SelfExecution ? "自身面" : "敌方面",output);
            StringAssert.Contains(face == TacticalThirdBatchCoinFace.SelfExecution ? "持有者 1:1" : "指定敌人 2:1",output);
            StringAssert.Contains("完整回合 2",output); StringAssert.Contains("至回合 2",compact);
            if (face == TacticalThirdBatchCoinFace.EnemyNextAttack) {
                StringAssert.Contains("pending",output);
                var attack = new TacticalThirdBatchAttack(7,201,Enemy,9,3,new TacticalThirdBatchOrder(10,0,2,11,Enemy,1));
                var item = TacticalThirdBatchEvent.Start(attack.AcceptedOrder,attack,true,true,true);
                Assert.IsTrue(state.TryOpenRound(10,0,new[]{item.Order},1)); Assert.IsTrue(state.TryApplyNext(item,out _));
                output = CharacterTestTacticalAccessoryStateDisplay.Describe(Read(state),new TacticalThirdBatchParameters(false,true,false),out _);
                StringAssert.Contains("bound",output); StringAssert.Contains("执行 201／攻击 9／revision 3",output);
            }
            for (int i=0;i<10;i++) CharacterTestTacticalAccessoryStateDisplay.Describe(Read(state),new TacticalThirdBatchParameters(false,true,false),out _);
            Assert.AreEqual(random.State,ledger.RandomState.State); Assert.AreEqual(random.DrawCount,ledger.DrawCount); Assert.AreEqual(1,ledger.ReceiptCount);
        }
        [Test]
        public void GlassAndThroneReadActualPhaseAndExactExecution()
        {
            var state = new TacticalAccessoryThirdBatchState(Commit(),true,true);
            var output = CharacterTestTacticalAccessoryStateDisplay.Describe(Read(state,true),new TacticalThirdBatchParameters(true,false,true),out _);
            StringAssert.Contains("输出待兑现 · 风险待付",output); StringAssert.Contains("铁王座 · 绑定执行 101 · 当前生效",output);
            output = CharacterTestTacticalAccessoryStateDisplay.Describe(Read(state,false),new TacticalThirdBatchParameters(true,false,true),out _);
            StringAssert.Contains("当前未生效",output); StringAssert.DoesNotContain("幸运硬币",output);
        }
        [Test]
        public void GlassRiskPaidAndSettledAppearFromActualStateTransitions()
        {
            var state = new TacticalAccessoryThirdBatchState(Commit(),true,false);
            var attack = new TacticalThirdBatchAttack(7,201,Enemy,1,1,new TacticalThirdBatchOrder(0,0,1,10,Enemy,1));
            var source = new TacticalThirdBatchSource(attack,12,TacticalThirdBatchOrigin.DirectProjectile,new TacticalThirdBatchOrder(0,0,2,12,Enemy,1));
            var damage = TacticalThirdBatchEvent.Hit(new TacticalThirdBatchOrder(10,0,2,12,Self,1),source,MxFramework.Combat.Hit.HitResolveKind.Damage,true,true,false);
            Assert.IsTrue(state.TryOpenRound(10,0,new[]{damage.Order},1)); Assert.IsTrue(state.TryApplyNext(damage,out _));
            StringAssert.Contains("输出待兑现 · 风险已付",CharacterTestTacticalAccessoryStateDisplay.Describe(Read(state),new TacticalThirdBatchParameters(true,false,false),out _));
            var settle = TacticalThirdBatchEvent.SettleFullTurn(new TacticalThirdBatchOrder(11,0,1,90,Self,1),2);
            Assert.IsTrue(state.TryOpenRound(11,0,new[]{settle.Order},1)); Assert.IsTrue(state.TryApplyNext(settle,out _));
            StringAssert.Contains("已结算 · 风险关闭",CharacterTestTacticalAccessoryStateDisplay.Describe(Read(state),new TacticalThirdBatchParameters(true,false,false),out _));
        }
        [Test]
        public void NewSessionAndMissingCommittedJournalClearTheProjection()
        {
            Assert.IsFalse(CharacterTestTacticalAccessoryStateDisplay.TryDescribe(new TacticalDoubleEdgeBattleState(),null,Self,out string compact,out string detail));
            Assert.AreEqual("",compact); Assert.AreEqual("",detail);
        }
    }
}
