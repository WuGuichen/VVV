using MxFramework.Game.CharacterTest.TacticalCard;

namespace MxFramework.Game.CharacterTest
{
    // Read-only projection. Unknown future kinds retain their exact ID and configured fee; no execution admission.
    internal readonly struct CharacterTestTacticalAccessoryDisplay
    {
        private CharacterTestTacticalAccessoryDisplay(TacticalAccessoryCombatKind kind, string id, int? cost, int? momentum, string rule = null)
        {
            Kind = kind; Name = DisplayName(kind, id);
            ConfirmationCost = cost >= 0 ? cost : null;
            Description = kind == TacticalAccessoryCombatKind.BorrowMomentum && momentum.HasValue ? "首次合格条件触发时回能 " + momentum + "。"
                : kind == TacticalAccessoryCombatKind.BreakMomentum && momentum.HasValue ? "首次合格条件触发时扣除对方最多 " + momentum + " 可用动势。"
                : kind == TacticalAccessoryCombatKind.PushAway ? "首次合格条件触发时施加推离。"
                : kind == TacticalAccessoryCombatKind.DuelInvitation ? "与指定敌人的直接伤害提高。"
                : kind == TacticalAccessoryCombatKind.ShatteredFantasy ? "本次主卡直接伤害提高；主卡实例耗尽。"
                : kind == TacticalAccessoryCombatKind.BlackHolePocket ? "主卡自然完成后生成牵引场。"
                : kind == TacticalAccessoryCombatKind.NoTurningBack ? "背后攻防增幅；不可主动转身。"
                : kind == TacticalAccessoryCombatKind.NonLethalOath ? "绑定本次出牌来源，伤害不致命。"
                : kind == TacticalAccessoryCombatKind.OnePunch ? "首次合格接触增伤；主卡实例耗尽。"
                : kind == TacticalAccessoryCombatKind.GlassCannon ? "首次输出增伤；兑现前有一次风险。"
                : kind == TacticalAccessoryCombatKind.LuckyCoin ? "确认后定面；增幅绑定实际受益动作。"
                : kind == TacticalAccessoryCombatKind.IronThrone ? "绑定实际执行；锁定主动移动与朝向。"
                : kind == 0 ? "未装配配件。" : "配件随主卡提交。";
            RuleText = rule ?? Description;
        }
        internal TacticalAccessoryCombatKind Kind { get; }
        internal string Name { get; }
        internal int? ConfirmationCost { get; }
        internal string Description { get; }
        internal string RuleText { get; }
        internal string FeeText => ConfirmationCost.HasValue ? "费用 +" + ConfirmationCost.Value : "费用未配置";
        internal string SlotText => Kind == 0 ? "空槽" : Name.Length <= 4 ? Name : "配件";
        internal static CharacterTestTacticalAccessoryDisplay Unresolved(TacticalAccessoryCombatKind kind)
            => new CharacterTestTacticalAccessoryDisplay(kind, null, kind == 0 ? (int?)0 : null, null);
        internal static CharacterTestTacticalAccessoryDisplay From(CharacterTacticalAccessoryDefinition definition)
            => definition == null ? Unresolved(0) : new CharacterTestTacticalAccessoryDisplay(definition.Execution?.Kind ?? 0,
                definition.Reference?.AccessoryId, definition.Execution?.ConfirmationMomentumCost, definition.Execution?.MomentumAmount,
                Rule(definition.Execution?.Kind ?? 0, definition.Execution?.SecondBatch?.RearScaleMilli,
                    definition.Execution?.SecondBatch?.OnePunchScaleMilli, definition.Execution?.ThirdBatch?.DamageScaleMilli));
        internal static CharacterTestTacticalAccessoryDisplay From(TacticalFixedAccessoryManifest part)
            => part == null ? Unresolved(0) : new CharacterTestTacticalAccessoryDisplay(part.Kind, part.AccessoryId, part.ConfirmationCost, part.MomentumAmount,
                Rule(part.Kind, part.IsSecondBatch ? part.SecondBatch.RearScaleMilli : (int?)null,
                    part.IsSecondBatch ? part.SecondBatch.OnePunchScaleMilli : (int?)null,
                    part.IsThirdBatch ? TacticalAccessoryThirdBatchRules.TrialScaleMilli : (int?)null));
        private static string Scale(int? milli) => milli.HasValue && milli.Value > 0
            ? "×" + (milli.Value / 1000m).ToString("0.###", System.Globalization.CultureInfo.InvariantCulture) : "（倍率未配置）";
        private static string Rule(TacticalAccessoryCombatKind kind, int? rear, int? punch, int? third)
        {
            switch (kind) {
                case TacticalAccessoryCombatKind.NoTurningBack: return "提交回合内不能主动转身；背后合格直接伤害的攻防倍率" + Scale(rear) + "。";
                case TacticalAccessoryCombatKind.NonLethalOath: return "绑定本次出牌的实际来源，直接与延迟伤害不致命；不改变其他出牌。";
                case TacticalAccessoryCombatKind.OnePunch: return "本次主卡首次合格接触伤害" + Scale(punch) + "；付款后该战斗实例耗尽，空挥或取消不退款。";
                case TacticalAccessoryCombatKind.GlassCannon: return "首次合格输出伤害" + Scale(third) + "；兑现前先承受的首次合格伤害同倍率，风险只付一次。至下一完整回合结算。";
                case TacticalAccessoryCombatKind.LuckyCoin: return "确认成功后才读取实际硬币回执；伤害" + Scale(third) + "，受益者为本次执行或指定敌人的下一合格攻击。至下一完整回合结算。";
                case TacticalAccessoryCombatKind.IronThrone: return "仅绑定本次实际ACK执行；执行中限制主动移动、主动转身及普通击退，不把其他执行或特殊外力视为同一绑定。";
                default: return null;
            }
        }
        private static string DisplayName(TacticalAccessoryCombatKind kind, string id)
        {
            switch (kind) {
                case TacticalAccessoryCombatKind.BorrowMomentum: return "借力";
                case TacticalAccessoryCombatKind.PushAway: return "推离";
                case TacticalAccessoryCombatKind.BreakMomentum: return "破势";
                case TacticalAccessoryCombatKind.DuelInvitation: return "决斗邀请";
                case TacticalAccessoryCombatKind.ShatteredFantasy: return "破碎幻想";
                case TacticalAccessoryCombatKind.BlackHolePocket: return "黑洞口袋";
                case TacticalAccessoryCombatKind.NoTurningBack: return "不许回头";
                case TacticalAccessoryCombatKind.NonLethalOath: return "不杀之誓";
                case TacticalAccessoryCombatKind.OnePunch: return "一拳之约";
                case TacticalAccessoryCombatKind.GlassCannon: return "玻璃大炮";
                case TacticalAccessoryCombatKind.LuckyCoin: return "幸运硬币";
                case TacticalAccessoryCombatKind.IronThrone: return "铁王座";
                default: return kind == 0 && string.IsNullOrEmpty(id) ? "空槽"
                    : string.IsNullOrEmpty(id) ? "配件类型 " + (int)kind : id;
            }
        }
        internal static int TotalCost(int mainCost, int accessoryCost)
            => mainCost < 0 || accessoryCost < 0 || mainCost > int.MaxValue - accessoryCost ? -1 : mainCost + accessoryCost;
        internal static string CostBreakdown(int totalCost, int accessoryCost)
            => totalCost < 0 || accessoryCost < 0 || totalCost < accessoryCost ? "费用未配置"
                : $"主卡 {totalCost - accessoryCost} + 配件 {accessoryCost}                  总费用 {totalCost}";
    }
}
