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
                text.Append("\n\n槽 ").Append(Number(accessory.Slot + 1)).Append(" · ").Append(Name(accessory.Kind))
                    .Append(" · 费用 ").Append(Number(accessory.ConfirmationCost)).Append('\n').Append(Trigger(accessory));
                text.Append("\n未触发时：").Append(accessory.Lifetime == TacticalAccessoryLifetimeKind.UntilBattleEnd
                    ? "保留至战斗结束或角色失效。" : "保留 " + Number(accessory.DurationTurns) + " 回合。");
                text.Append("触发后消耗；主行动结束不清除。");
            }
            detail = text.ToString(); return true;
        }
        internal static string Name(TacticalAccessoryCombatKind kind)
            => kind == TacticalAccessoryCombatKind.BorrowMomentum ? "借力"
                : kind == TacticalAccessoryCombatKind.PushAway ? "推离"
                : kind == TacticalAccessoryCombatKind.BreakMomentum ? "破势" : string.Empty;
        private static string Trigger(TacticalFixedAccessoryManifest accessory)
        {
            switch (accessory.Kind) {
                case TacticalAccessoryCombatKind.BorrowMomentum:
                    return "下一次成功格挡或招架直接攻击且自身仍存活后，恢复至多 " + Number(accessory.MomentumAmount) + " 动势。";
                case TacticalAccessoryCombatKind.BreakMomentum:
                    return "下一次直接攻击被格挡或招架且目标仍存活后，削减目标至多 " + Number(accessory.MomentumAmount) + " 可用动势。";
                case TacticalAccessoryCombatKind.PushAway:
                    return "下一次直接攻击判定为伤害命中后，按命中方向和目标质量推离；护甲全额吸收也算，目标死亡或不可推动时也消耗。";
                default: throw new InvalidOperationException("unknown-frozen-accessory-kind");
            }
        }
        private static string Number(int value) => value.ToString(CultureInfo.InvariantCulture);
    }
}
