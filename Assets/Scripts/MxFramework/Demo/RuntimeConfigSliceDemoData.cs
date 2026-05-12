using MxFramework.Config;
using MxFramework.Config.Runtime;

namespace MxFramework.Demo
{
    /// <summary>
    /// Demo config data for the Runtime Config Slice.
    /// Creates ConfigTable-driven BasicBuffConfig and BasicModifierConfig
    /// that match the hardcoded data from RuntimeVerticalSliceRunner.
    ///
    /// This is demo-only code. Real projects provide their own IConfigProvider
    /// from Excel/CSV/JSON/Luban/etc.
    /// </summary>
    internal static class RuntimeConfigSliceDemoData
    {
        // Attribute IDs (same as Const)
        public const int AttrHp = 1;
        public const int AttrAttack = 2;
        public const int AttrDefense = 3;

        // Modifier: Attack +50
        private const int ModAttackBoost = 200001;

        // Buff: Burning (id matches Const.BuffBurning)
        private const int BuffBurning = 100001;

        public static IConfigProvider CreateRegistry()
        {
            var registry = new ConfigRegistry();

            // Modifier config: Attack +50 via ParamIndex=AttrAttack, Parameters[0]=50
            var modTable = new ConfigTable<BasicModifierConfig>(BasicModifierConfig.CreateSchema());
            modTable.Add(new BasicModifierConfig(
                id: ModAttackBoost,
                nameText: new LocalizedTextKey("modifier.attack_up.name"),
                descriptionText: new LocalizedTextKey("modifier.attack_up.desc"),
                paramIndex: AttrAttack,
                parameters: new int[] { 50 }));
            registry.RegisterProvider<BasicModifierConfig>(modTable);

            // Buff config: Burning buff, references modifier 200001
            var buffTable = new ConfigTable<BasicBuffConfig>(BasicBuffConfig.CreateSchema());
            buffTable.Add(new BasicBuffConfig(
                id: BuffBurning,
                nameText: new LocalizedTextKey("buff.burning.name"),
                descriptionText: new LocalizedTextKey("buff.burning.desc"),
                duration: 5f,
                maxLayers: 3,
                modifierId: ModAttackBoost));
            registry.RegisterProvider<BasicBuffConfig>(buffTable);

            return registry;
        }
    }
}
