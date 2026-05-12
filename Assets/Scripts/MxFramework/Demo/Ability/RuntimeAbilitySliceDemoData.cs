using MxFramework.Buffs;
using MxFramework.Config;
using MxFramework.Config.Runtime;

namespace MxFramework.Demo
{
    /// <summary>Demo-only config data for the Gameplay ability runtime slice.</summary>
    internal static class RuntimeAbilitySliceDemoData
    {
        public static IConfigProvider CreateRegistry()
        {
            var table = new ConfigTable<BasicAbilityConfig>(BasicAbilityConfig.CreateSchema());
            table.Add(new BasicAbilityConfig(
                AbilityConst.AbilityStrike,
                new LocalizedTextKey("ability.strike.name"),
                new LocalizedTextKey("ability.strike.desc"),
                AbilityTargetSelectorKind.SingleEnemy,
                new[]
                {
                    AbilityEffectConfig.DamageByAttackDefense(
                        AbilityConst.AttrAttack,
                        AbilityConst.AttrDefense,
                        AbilityConst.AttrHp)
                }));
            table.Add(new BasicAbilityConfig(
                AbilityConst.AbilityIgnite,
                new LocalizedTextKey("ability.ignite.name"),
                new LocalizedTextKey("ability.ignite.desc"),
                AbilityTargetSelectorKind.SingleEnemy,
                new[]
                {
                    AbilityEffectConfig.ApplyBuff(AbilityConst.BuffBurning)
                }));

            return table;
        }
    }

    /// <summary>Demo-only BuffFactory that maps demo buff IDs to demo buff classes.</summary>
    internal sealed class RuntimeAbilitySliceBuffFactory : IBuffFactory
    {
        public bool TryCreate(int buffId, out IBuff buff)
        {
            if (buffId == AbilityConst.BuffBurning)
            {
                buff = new AbilityBurningBuff();
                return true;
            }

            buff = null;
            return false;
        }
    }
}
