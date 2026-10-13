using System;
using System.Collections.Generic;
using MxFramework.CharacterAction;
using MxFramework.Combat.Damage;
using MxFramework.Combat.Hit;
using MxFramework.Core.Math;
using MxFramework.Gameplay;
using NUnit.Framework;

namespace MxFramework.Game.CharacterTest.TacticalCard
{
    public sealed class TacticalFixedReadoutTests
    {
        private sealed class Fixture
        {
            internal readonly GameplayEntityId Actor = new GameplayEntityId(1, 1);
            internal readonly TacticalCardDefinition Definition = TacticalFixedAssemblyManifestTests.Main();
            internal readonly MomentumLedger Ledger = new MomentumLedger(20);
            internal readonly TacticalInteractionEffectAuthority Effects = new TacticalInteractionEffectAuthority();
            internal TacticalFixedCardPaymentCommit Paid;
            internal Fixture() {
                Assert.IsTrue(TacticalFixedAssemblyManifest.TryCreate(TacticalFixedAssemblyManifestTests.Snapshot(), Definition,
                    11, 7, 3, new Dictionary<string, int> { { "main.action", 17 } }, out var assembly, out _));
                Definition.AttachFixedAssembly(assembly);
                var card = new TacticalCardInstance(501, Definition); card.MoveTo(CardZone.Hand); card.MoveTo(CardZone.PendingAction);
                Assert.IsTrue(Ledger.TryReserve(3, out var token));
                var program = TacticalFixedPaymentCoreTests.OneAction();
                Assert.IsTrue(CharacterTestFixedActionBudgetProof.TryCreate(program, _ => 7, out var proof, out _));
                Assert.IsTrue(TacticalFixedCardFeePlan.TryCreate(assembly, TacticalFixedCardPaymentTiming.CardAndAccessoriesThenActualActions, 7, out var fees));
                Effects.Bind(7, 2, Actor);
                Assert.IsTrue(TacticalFixedCardCommitManifest.TryPrepare(assembly, fees, card, 7, 2, Actor, 1, 1, 10, 0, 1, token, default, out var manifest));
                Assert.IsTrue(TacticalFixedCardCommitManifest.PreparedPaymentAndAccessories.TryPrepare(manifest, card, Ledger,
                    Effects, program, proof, out var ready, out _));
                Assert.IsTrue(ready.TryPublish(out Paid));
            }
            internal void Block() {
                var receipt = new TacticalAccessoryCombatReceipt(7, 2, 11, 1, new GameplayEntityId(2, 1), Actor,
                    new CharacterActionId(17), 12, 13, DamageProfileSourceKind.WeaponTrace, TacticalAccessoryDamageOrigin.DirectAttack,
                    HitResolveKind.Blocked, true, true, true, new FixVector3(Fix64.One, Fix64.Zero, Fix64.Zero), 80000, 1000);
                Assert.AreEqual(1, Effects.CommitAccessoryReceipt(receipt, Ledger, null, null));
            }
        }
        [Test] public void MissingBudgetRemainsUnknownAndDoesNotClaimZeroCost() {
            var f = new Fixture();
            Assert.IsTrue(TacticalFixedAssemblyReadout.TryDescribe(f.Definition, null, out var summary, out var detail));
            StringAssert.Contains("配件 2/2", summary); StringAssert.Contains("行动费用尚未核验", detail);
            StringAssert.DoesNotContain("整牌准入需动势 0", detail);
        }
        [Test] public void FrozenFeesSeparateAdmissionConfirmationAndActualActionReservation() {
            var f = new Fixture();
            Assert.IsTrue(TacticalFixedAssemblyReadout.TryDescribe(f.Definition, f.Paid.Record.Fees, out _, out var detail));
            StringAssert.Contains("整牌准入需动势 12", detail); StringAssert.Contains("确认支付 5", detail);
            StringAssert.Contains("行动预留 7", detail); StringAssert.Contains("主行动结束不清除", detail);
        }
        [Test] public void ForeignAssemblyFeePlanCannotBeDisplayedAsThisCard() {
            var a = new Fixture(); var b = new Fixture();
            Assert.IsFalse(TacticalFixedAssemblyReadout.TryDescribe(a.Definition, b.Paid.Record.Fees, out _, out _));
        }
        [Test] public void CommittedReadoutDoesNotInventSpendingFromReleasedBudget() {
            var f = new Fixture();
            StringAssert.Contains("最近出牌已支付 5", TacticalFixedAssemblyReadout.DescribeCommitted(f.Paid.Record, f.Ledger));
            StringAssert.Contains("剩余行动预留 7", TacticalFixedAssemblyReadout.DescribeCommitted(f.Paid.Record, f.Ledger));
            Assert.IsTrue(f.Paid.ReleaseUnexecutedBudget());
            string detail = TacticalFixedAssemblyReadout.DescribeCommitted(f.Paid.Record, f.Ledger);
            StringAssert.Contains("行动预留已释放", detail); StringAssert.DoesNotContain("实际行动已支付 7", detail);
            Assert.AreEqual(15, f.Ledger.Current);
        }
        [Test] public void EffectsShowActualCommittedGrantAndReadoutDoesNotMutateAuthority() {
            var f = new Fixture(); f.Block();
            int balance = f.Ledger.Current; ulong sequence = f.Effects.AccessoryReceiptSequence;
            var before = f.Effects.Capture();
            Assert.IsTrue(TacticalFixedAssemblyReadout.TryDescribeEffects(f.Effects, out var summary, out var detail));
            StringAssert.Contains("1 待触发", summary); StringAssert.Contains("1 已消耗", summary);
            StringAssert.Contains("借力 · 已消耗 · 实际回复 2", detail); StringAssert.Contains("推离 · 待触发", detail);
            Assert.AreEqual(balance, f.Ledger.Current); Assert.AreEqual(sequence, f.Effects.AccessoryReceiptSequence);
            Assert.AreEqual(Newtonsoft.Json.JsonConvert.SerializeObject(before), Newtonsoft.Json.JsonConvert.SerializeObject(f.Effects.Capture()));
        }
        [Test] public void SaturatedGrantDisplaysZeroAndClearLeavesNoStaleArmedEffect() {
            var f = new Fixture(); f.Ledger.Grant(100); f.Block();
            Assert.IsTrue(TacticalFixedAssemblyReadout.TryDescribeEffects(f.Effects, out _, out var detail));
            StringAssert.Contains("实际回复 0", detail);
            f.Effects.Clear(); Assert.IsFalse(TacticalFixedAssemblyReadout.TryDescribeEffects(f.Effects, out _, out _));
        }
    }
}
