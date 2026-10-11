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
            table.Add(CreateStrikeConfig("ability.strike.name", "ability.strike.desc"));
            table.Add(CreateIgniteConfig());
            return table;
        }

        public static IConfigProvider CreatePatchRegistry()
        {
            var table = new ConfigTable<BasicAbilityConfig>(BasicAbilityConfig.CreateSchema());
            table.Add(CreateStrikeConfig("ability.strike.patch.name", "ability.strike.patch.desc"));
            table.Add(CreateIgniteConfig());
            return table;
        }

        public static IConfigProvider CreateModPackageRegistry()
        {
            var table = new ConfigTable<BasicAbilityConfig>(BasicAbilityConfig.CreateSchema());
            table.Add(CreateStrikeConfig("ability.strike.mod.name", "ability.strike.mod.desc"));
            table.Add(CreateIgniteConfig());
            return table;
        }

        public static ConfigChangeSet CreatePatchChangeSet()
        {
            var changes = new ConfigChangeSet();
            changes.Add(new ConfigRowChange(
                typeof(BasicAbilityConfig),
                AbilityConst.AbilityStrike,
                ConfigLayerKind.Patch,
                ConfigPatchOperation.Upsert,
                ConfigMergeChangeKind.Replaced,
                "Showcase Patch"));
            return changes;
        }

        public static ConfigChangeSet CreateModPackageChangeSet()
        {
            var changes = new ConfigChangeSet();
            changes.Add(new ConfigRowChange(
                typeof(BasicAbilityConfig),
                AbilityConst.AbilityStrike,
                ConfigLayerKind.Mod,
                ConfigPatchOperation.Upsert,
                ConfigMergeChangeKind.Replaced,
                "Showcase Mod Package"));
            return changes;
        }

        private static BasicAbilityConfig CreateStrikeConfig(string nameKey, string descriptionKey)
        {
            return new BasicAbilityConfig(
                AbilityConst.AbilityStrike,
                new LocalizedTextKey(nameKey),
                new LocalizedTextKey(descriptionKey),
                AbilityTargetSelectorKind.SingleEnemy,
                new[]
                {
                    AbilityEffectConfig.DamageByAttackDefense(
                        AbilityConst.AttrAttack,
                        AbilityConst.AttrDefense,
                        AbilityConst.AttrHp)
                });
        }

        private static BasicAbilityConfig CreateIgniteConfig()
        {
            return new BasicAbilityConfig(
                AbilityConst.AbilityIgnite,
                new LocalizedTextKey("ability.ignite.name"),
                new LocalizedTextKey("ability.ignite.desc"),
                AbilityTargetSelectorKind.SingleEnemy,
                new[]
                {
                    AbilityEffectConfig.DamageByAttackDefense(
                        AbilityConst.AttrAttack,
                        AbilityConst.AttrDefense,
                        AbilityConst.AttrHp)
                });
        }
    }
}
