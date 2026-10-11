using System;
using System.Globalization;
using MxFramework.Game.CharacterTest.TacticalCard;
using Newtonsoft.Json.Linq;

namespace MxFramework.Game.CharacterTest
{
    /// <summary>Presentation-only fields for a validated fixed interaction effect; never persisted as card logic.</summary>
    internal sealed class CharacterTestTacticalUtilityFace
    {
        internal string TypeText { get; private set; }
        internal string Trigger { get; private set; }
        internal string Prefix { get; private set; }
        internal string Value { get; private set; }
        internal string Suffix { get; private set; }
        internal string Effect { get; private set; }
        internal string Details { get; private set; }
        internal string Lifetime { get; private set; }
        internal string Illustration { get; private set; }
        internal string CardText => Trigger + "\n" + (Value.Length > 0 ? Prefix + " " + Value + " " + Suffix : Effect);

        /// <remarks>LowFreqAlloc: called alongside the existing fixed description projection.</remarks>
        internal static CharacterTestTacticalUtilityFace Create(InteractionPreparationPayload payload)
        {
            if (payload == null) throw new ArgumentNullException(nameof(payload));
            var face = new CharacterTestTacticalUtilityFace { TypeText = "功能卡 · 交互准备",
                Lifetime = "本回合 · 触发一次", Prefix = "", Value = "", Suffix = "", Effect = "" };
            switch (payload.Effect)
            {
                case TacticalInteractionKind.PursuitMark:
                    face.Trigger = "下一次有效命中标记目标";
                    face.Prefix = "追加"; face.Value = TacticalInteractionTrialCards.FixedDamage.ToString(CultureInfo.InvariantCulture);
                    face.Suffix = "基础伤害"; face.Details = "仍经过护甲结算"; face.Illustration = "UtilityPursuitMark"; break;
                case TacticalInteractionKind.MomentumRecovery:
                    face.Trigger = "下一次有效命中";
                    face.Prefix = "获得"; face.Value = TacticalInteractionTrialCards.MomentumGain.ToString(CultureInfo.InvariantCulture);
                    face.Suffix = "动能"; face.Details = "护甲吸收仍算命中"; face.Illustration = "UtilityMomentumRecovery"; break;
                case TacticalInteractionKind.DefensePreparation:
                    face.Trigger = "下一次主动攻击被普通防御挡住";
                    face.Prefix = "追加"; face.Value = TacticalInteractionTrialCards.ScatterPressure.ToString(CultureInfo.InvariantCulture);
                    face.Suffix = "散势压力"; face.Details = "不触发命中类效果"; face.Illustration = "UtilityDefensePreparation"; break;
                case TacticalInteractionKind.ReturnMark:
                    face.Trigger = "下一次有效命中后"; face.Effect = "安全时尝试回返";
                    face.Details = "先记录当前落地点\n等待动作释放、落地与外力稳定"; face.Illustration = "UtilityReturnMark"; break;
                case TacticalInteractionKind.HurtCharge:
                    face.Trigger = "首次受到外来实际HP伤害";
                    face.Prefix = "获得"; face.Value = TacticalInteractionTrialCards.MomentumGain.ToString(CultureInfo.InvariantCulture);
                    face.Suffix = "动能"; face.Details = "仅实际HP损失触发"; face.Illustration = "UtilityHurtCharge"; break;
                default: throw new ArgumentOutOfRangeException(nameof(payload), "Unsupported interaction presentation.");
            }
            return face;
        }

        /// <remarks>LowFreqAlloc: shared display transport consumed by Web, without interpreting gameplay source.</remarks>
        internal string ToJson() => new JObject { ["schema"] = "mx.tactical-card.utility-face.v1",
            ["typeText"] = TypeText, ["trigger"] = Trigger, ["prefix"] = Prefix, ["value"] = Value,
            ["suffix"] = Suffix, ["effect"] = Effect, ["details"] = Details, ["lifetime"] = Lifetime,
            ["illustration"] = Illustration, ["cardText"] = CardText }.ToString(Newtonsoft.Json.Formatting.None);
    }
}
