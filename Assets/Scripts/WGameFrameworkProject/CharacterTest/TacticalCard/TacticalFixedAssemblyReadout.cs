using System;
using System.Globalization;
using System.Text;

namespace MxFramework.Game.CharacterTest.TacticalCard
{
    /// <summary>Cold player-facing projection of the same frozen contract. Does not compute admission or predict hits.</summary>
    internal static class TacticalFixedAssemblyReadout
    {
        /// <remarks>LowFreqAlloc. Missing action budget is displayed as unavailable, never guessed as zero.</remarks>
        internal static bool TryDescribe(TacticalCardDefinition definition, TacticalFixedCardFeePlan fees,
            out string summary, out string detail)
        {
            summary = detail = string.Empty;
            var assembly = definition?.FixedAssembly;
            if (assembly == null || fees != null && !ReferenceEquals(fees.Assembly, assembly)) return false;
            string quality = assembly.Quality == CharacterTacticalCardQuality.Silver ? "银"
                : assembly.Quality == CharacterTacticalCardQuality.Gold ? "金" : "彩";
            summary = quality + "卡 · 配件 " + Number(assembly.Accessories.Count) + "/" + Number(assembly.SlotCount);
            var text = new StringBuilder();
            text.Append(definition.Name).Append('\n').Append(summary)
                .Append("\n主卡费用 ").Append(Number(assembly.MainConfirmationCost))
                .Append(" · 配件费用 ").Append(Number(assembly.AccessoryConfirmationCost));
            if (fees == null) text.Append("\n行动费用尚未核验，不能确认出牌。");
            else {
                text.Append("\n整牌准入需动势 ").Append(Number(fees.MaximumAdmissionCost))
                    .Append(" · 确认支付 ").Append(Number(fees.ConfirmationCost));
                if (fees.ReservedAfterConfirmation > 0)
                    text.Append("\n行动预留 ").Append(Number(fees.ReservedAfterConfirmation)).Append("，按实际起手支付。");
            }
            foreach (var accessory in assembly.Accessories) {
                text.Append("\n\n槽 ").Append(Number(accessory.Slot + 1)).Append(" · ").Append(accessory.Kind >= TacticalAccessoryCombatKind.BorrowMomentum && accessory.Kind <= TacticalAccessoryCombatKind.BlackHolePocket ? Name(accessory.Kind) : accessory.AccessoryId)
                    .Append(" · 费用 ").Append(Number(accessory.ConfirmationCost)).Append('\n').Append(Trigger(accessory));
                if (accessory.Kind > TacticalAccessoryCombatKind.BreakMomentum) continue;
                text.Append("\n未触发时：").Append(accessory.Lifetime == TacticalAccessoryLifetimeKind.UntilBattleEnd
                    ? "保留至战斗结束或角色失效。" : "保留 " + Number(accessory.DurationTurns) + " 回合。");
                text.Append("触发后消耗；主行动结束不清除。");
            }
            detail = text.ToString(); return true;
        }
        /// <remarks>LowFreqAlloc view projection. Released budget is not reported as action spending.</remarks>
        internal static string DescribeCommitted(TacticalFixedCardCommitManifest commit, MomentumLedger ledger)
        {
            if (commit?.IsConfirmed != true) return string.Empty;
            var fees = commit.Fees;
            string budget = ledger == null ? "行动预留状态暂不可读。"
                : ledger.HasFixedReservation(commit.ReservationToken) ? "剩余行动预留 " + Number(ledger.Reserved)
                : "行动预留已释放。";
            return "最近出牌已支付 " + Number(fees.ConfirmationCost) + "（主卡 " + Number(fees.MainCost)
                + "＋配件 " + Number(fees.AccessoryCost) + "）\n" + budget
                + "\n实际行动费在起手时支付；成功确认的牌费不因未起手取消而退还。";
        }

        /// <remarks>LowFreqAlloc. One-line conditions projection of the same committed receipt;
        /// the full payment/lifetime explanation remains available in the observation tooltip.</remarks>
        internal static string DescribeCommittedCompact(TacticalFixedCardCommitManifest commit, MomentumLedger ledger)
        {
            if (commit?.IsConfirmed != true) return string.Empty;
            var fees = commit.Fees;
            string budget = ledger == null ? "预留待核验"
                : ledger.HasFixedReservation(commit.ReservationToken) ? "行动预留 " + Number(ledger.Reserved)
                : "预留已释放";
            return "牌费已付 " + Number(fees.ConfirmationCost) + "（主 " + Number(fees.MainCost)
                + "＋配件 " + Number(fees.AccessoryCost) + "） · " + budget;
        }

        /// <remarks>LowFreqAlloc, from the actual combat owner. Shows all armed effects and the latest
        /// commit's terminal effects; older terminal activations remain included in the summary counts.</remarks>
        internal static bool TryDescribeEffects(TacticalInteractionEffectAuthority effects, out string summary, out string detail)
        {
            summary = detail = string.Empty;
            if (effects == null || effects.AccessoryActivationCount == 0) return false;
            var journal = effects.ConfirmedFixedAssembly;
            ulong latestCommit = journal == null || journal.CommitCount == 0 ? 0 : journal.CommitAt(journal.CommitCount - 1).CommitId;
            int armed = 0, consumed = 0, expired = 0;
            var text = new StringBuilder();
            for (int i = 0; i < effects.AccessoryActivationCount; i++) {
                var state = effects.AccessoryAt(i);
                if (state.Status == TacticalAccessoryCombatStatus.Armed) armed++;
                else if (state.Status == TacticalAccessoryCombatStatus.Consumed) consumed++;
                else if (state.Status == TacticalAccessoryCombatStatus.Expired) expired++;
                if (state.Status != TacticalAccessoryCombatStatus.Armed && state.Identity.CommitId != latestCommit) continue;
                if (text.Length != 0) text.Append('\n');
                text.Append(Name(state.Kind)).Append(" · ").Append(state.Status == TacticalAccessoryCombatStatus.Armed
                    ? "待触发" : state.Status == TacticalAccessoryCombatStatus.Consumed ? "已消耗" : "已到期");
                if (state.Status == TacticalAccessoryCombatStatus.Armed)
                    text.Append(state.Identity.LastTurn == ulong.MaxValue ? " · 战斗结束或角色失效时结束"
                        : " · 有效至回合 " + state.Identity.LastTurn.ToString(CultureInfo.InvariantCulture));
                else if (state.Status == TacticalAccessoryCombatStatus.Consumed
                    && effects.TryReadAccessoryConsumption(state.Identity.ActivationId, out var result)) {
                    if (state.Kind == TacticalAccessoryCombatKind.BorrowMomentum) text.Append(" · 实际回复 ").Append(Number(result.MomentumGranted)).Append(" 动势");
                    else if (state.Kind == TacticalAccessoryCombatKind.BreakMomentum) text.Append(" · 实际削减 ").Append(Number(result.MomentumSpent)).Append(" 动势");
                    else text.Append(result.PushX == 0 && result.PushZ == 0 ? " · 本次未产生推离" : " · 已发出推离，移动受碰撞约束");
                }
            }
            summary = "配件 " + Number(armed) + " 待触发 · " + Number(consumed) + " 已消耗"
                + (expired == 0 ? "" : " · " + Number(expired) + " 已到期");
            detail = text.ToString(); return true;
        }

        internal static string Name(TacticalAccessoryCombatKind kind)
            => kind == TacticalAccessoryCombatKind.BorrowMomentum ? "借力"
                : kind == TacticalAccessoryCombatKind.PushAway ? "推离"
                : kind == TacticalAccessoryCombatKind.BreakMomentum ? "破势"
                : kind == TacticalAccessoryCombatKind.DuelInvitation ? "决斗邀请"
                : kind == TacticalAccessoryCombatKind.ShatteredFantasy ? "破碎幻想"
                : kind == TacticalAccessoryCombatKind.BlackHolePocket ? "黑洞口袋" : "配件类型 " + (int)kind;
        private static string Trigger(TacticalFixedAccessoryManifest accessory)
        {
            switch (accessory.Kind) {
                case TacticalAccessoryCombatKind.BorrowMomentum:
                    return "下一次成功格挡或招架直接攻击且自身仍存活后，恢复至多 " + Number(accessory.MomentumAmount) + " 动势。";
                case TacticalAccessoryCombatKind.BreakMomentum:
                    return "下一次直接攻击被格挡或招架且目标仍存活后，削减目标至多 " + Number(accessory.MomentumAmount) + " 可用动势。";
                case TacticalAccessoryCombatKind.PushAway:
                    return "下一次直接攻击判定为伤害命中后，按命中方向和目标质量推离；护甲全额吸收也算，目标死亡或不可推动时也消耗。";
                case TacticalAccessoryCombatKind.DuelInvitation:
                    return "提交回合内与指定敌人的合格直接伤害提高；双方死亡或离场时撤销。";
                case TacticalAccessoryCombatKind.ShatteredFantasy:
                    return "本次主卡的合格直接伤害提高；付款后该战斗实例必定耗尽，取消或空挥也不退还。";
                case TacticalAccessoryCombatKind.BlackHolePocket:
                    return "主卡自然完成后生成牵引场；下一实际模拟步生效，持续至下一完整回合结算。";
                default: return "配件随主卡提交；效果按其配置合同生效。";
            }
        }
        private static string Number(int value) => value.ToString(CultureInfo.InvariantCulture);
    }
}
