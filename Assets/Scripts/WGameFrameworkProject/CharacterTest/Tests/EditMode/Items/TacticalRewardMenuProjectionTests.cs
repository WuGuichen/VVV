using System;
using System.Threading.Tasks;
using MxFramework.Game.CharacterTest;
using MxFramework.Game.CharacterTest.TacticalCard;
using NUnit.Framework;

namespace MxFramework.Tests.Items
{
    public sealed class TacticalRewardMenuProjectionTests
    {
        private sealed class Prepared : IDisposable { public void Dispose() { } }
        private static CharacterTestFairyGuiMainMenuViewModel Menu(CharacterTestMainMenuStatus status)
            => CharacterTestFairyGuiMainMenuViewModelBuilder.BuildInto(new(), status, true, true, true, true);
        private static CharacterTestMainMenuStatus Outcome(int reward, bool saved = true) => new() {
            IsTacticalSlice = true, TacticalPhase = (int)TacticalPreparationState.Completed,
            TacticalSettlementSaved = saved, TacticalRewardState = reward, Message = "frozen reward and recipient" };

        [Test] public void PendingRewardShowsConfirmAndSuppressesRematch()
        {
            var model = Menu(Outcome(1)); Assert.AreEqual("确认领取奖励", model.StartLabel);
            Assert.IsTrue(model.CanStart); Assert.IsFalse(model.CanRestart); Assert.IsTrue(model.CanClose);
            Assert.AreEqual("奖励待领取", model.StatusText);
            Assert.AreEqual("frozen reward and recipient", model.Message); Assert.IsFalse(model.CanSettings);
        }
        [Test] public void ProcessingDisablesAllNavigationAndShowsDurableClaimWork()
        {
            var status = Outcome(1); status.TacticalBusy = status.TacticalRewardClaiming = true;
            var model = Menu(status); Assert.AreEqual("正在领取并保存…", model.StartLabel);
            Assert.AreEqual("正在领取并保存…", model.StatusText);
            Assert.IsFalse(model.CanStart); Assert.IsFalse(model.CanRestart); Assert.IsFalse(model.CanClose);
        }
        [Test] public void SaveFailureHasExplicitRetryWithoutSuccessfulClaimLabel()
        {
            var status = Outcome(1); status.TacticalRewardClaimFailed = true;
            var model = Menu(status); Assert.AreEqual("重试领取奖励", model.StartLabel); Assert.IsTrue(model.CanStart);
            Assert.AreEqual("奖励待领取", model.StatusText);
            Assert.IsFalse(model.CanRestart);
        }
        [TestCase(0, false)] [TestCase(0, true)]
        [TestCase(1, false)] [TestCase(1, true)]
        [TestCase(2, false)] [TestCase(2, true)]
        public void UnpersistedOutcomeSaveStatusTakesPriorityOverAnyMemoryRewardState(int reward, bool busy)
        {
            var status = Outcome(reward, false);
            status.TacticalBusy = busy;
            status.TacticalRewardClaimFailed = !busy; // A failure still cannot expose an unpersisted entitlement.
            status.TacticalRewardClaiming = busy; // Even a stale claim flag cannot bypass the settlement save gate.
            status.Message = busy ? "结算正在保存" : "进度尚未保存：injected failure";
            var model = Menu(status);
            Assert.AreEqual(busy ? "正在保存结算…" : "结算尚未保存", model.StatusText);
            Assert.AreEqual(busy ? "正在保存结算…" : "重试保存结算", model.StartLabel);
            Assert.AreEqual(!busy, model.CanStart);
            Assert.AreEqual(status.Message, model.Message);
            if (reward == 1) Assert.IsFalse(model.CanRestart);
        }
        [TestCase(0)] [TestCase(2)] public void NoRewardOrDurablyClaimedResultCanReturnToPreparation(int state)
        {
            var model = Menu(Outcome(state)); Assert.AreEqual("返回整备", model.StartLabel);
            Assert.AreEqual(state == 2 ? "奖励已领取" : "战斗已冻结", model.StatusText);
            Assert.IsTrue(model.CanStart); Assert.IsTrue(model.CanRestart);
        }
        [Test] public void ClaimLifecycleInvalidatesTheRealMainMenuSignature()
        {
            var pending = Menu(Outcome(1)); var claimed = Menu(Outcome(2));
            var a = CharacterTestFairyGuiMainMenuViewModelSignature.Create(pending, default);
            var b = CharacterTestFairyGuiMainMenuViewModelSignature.Create(claimed, default);
            Assert.AreNotEqual(a, b);
            pending.Status.TacticalRewardClaimFailed = true;
            Assert.AreNotEqual(a, CharacterTestFairyGuiMainMenuViewModelSignature.Create(pending, default));
        }
        [Test] public async Task ReturnPreparationBarrierDoesNotPublishWhenRewardCheckpointFails()
        {
            int publications = 0;
            var result = await TacticalFlowCheckpointBarrier.ReplaceAsync(() => new Prepared(),
                () => Task.FromResult(false), candidate => publications++, () => true);
            Assert.AreEqual(TacticalCheckpointTransition.SaveRejected, result); Assert.AreEqual(0, publications);
            result = await TacticalFlowCheckpointBarrier.ReplaceAsync(() => new Prepared(),
                () => Task.FromResult(true), candidate => publications++, () => true);
            Assert.AreEqual(TacticalCheckpointTransition.Published, result); Assert.AreEqual(1, publications);
        }
    }
}
