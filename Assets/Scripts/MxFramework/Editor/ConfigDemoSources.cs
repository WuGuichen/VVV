using System.Collections.Generic;
using MxFramework.Config;
using MxFramework.Config.Runtime;

namespace MxFramework.Editor
{
    public static class ConfigDemoSources
    {
        private const string DemoActionCategoryEnumId = "demo.ActionCategory";
        private const string DemoActionTagsEnumId = "demo.ActionTags";

        public static IReadOnlyList<ConfigSchema> CreateSchemas()
        {
            return new ConfigSchema[]
            {
                BasicBuffConfig.CreateSchema(),
                BasicModifierConfig.CreateSchema(),
                CreateDemoActionCatalogSchema(),
                CreateDemoActionGraphSchema()
            };
        }

        public static IReadOnlyList<IConfigEditorSource> CreateSources()
        {
            ConfigTable<BasicBuffConfig> buffs = CreateSampleBuffTable();
            ConfigTable<BasicModifierConfig> modifiers = CreateSampleModifierTable();
            var registry = new ConfigRegistry();
            registry.RegisterProvider<BasicBuffConfig>(buffs);
            registry.RegisterProvider<BasicModifierConfig>(modifiers);

            MemoryLocalizationProvider localization = CreateSampleLocalization();
            return new IConfigEditorSource[]
            {
                new MemoryConfigEditorSource<BasicBuffConfig>(
                    "内置 Demo / BasicBuffConfig",
                    buffs,
                    registry,
                    localization,
                    sourceType: "Demo Memory"),
                new MemoryConfigEditorSource<BasicModifierConfig>(
                    "内置 Demo / BasicModifierConfig",
                    modifiers,
                    registry,
                    localization,
                    sourceType: "Demo Memory"),
                CreateDemoActionCatalogSource(),
                CreateDemoActionGraphSource()
            };
        }

        public static IConfigEditorSource CreateDemoActionCatalogSource()
        {
            return new ExternalConfigEditorSource(
                "内置 Demo / ActionCatalog",
                "Demo TSV",
                CreateDemoActionCatalogSchema(),
                rowCount: 2,
                keys: new object[] { 1001, 1002 },
                previewText:
                    "Id\tDisplayName\tCategory\tTags\tGraphId\tIconPath\tEnabled\n" +
                    "1001\tFire Burst\t1\t3\t9001\tAssets/Demo/Icons/fire_burst.png\ttrue\n" +
                    "1002\tGuard Break\t2\t12\t9002\tAssets/Demo/Icons/guard_break.png\tfalse\n",
                sourcePath: "Docs/Demo/Config/ActionCatalog.tsv",
                contentHash: "demo",
                enumRegistry: CreateDemoEnumRegistry());
        }

        public static IConfigEditorSource CreateDemoActionGraphSource()
        {
            return new ExternalConfigEditorSource(
                "内置 Demo / ActionGraph",
                "Demo JSON Index",
                CreateDemoActionGraphSchema(),
                rowCount: 2,
                keys: new object[] { 9001, 9002 },
                previewText:
                    "Id\tName\tEntryNode\tNodeCount\n" +
                    "9001\tFireBurstGraph\tCast\t4\n" +
                    "9002\tGuardBreakGraph\tApproach\t5\n",
                sourcePath: "Docs/Demo/Config/Graphs/",
                contentHash: "demo");
        }

        public static IConfigEditorSource CreateBrokenReferenceDemoSource()
        {
            return new ExternalConfigEditorSource(
                "可选 Demo / BrokenReference",
                "Demo TSV",
                CreateDemoActionCatalogSchema(),
                rowCount: 1,
                keys: new object[] { 1999 },
                previewText:
                    "Id\tDisplayName\tCategory\tTags\tGraphId\tIconPath\tEnabled\n" +
                    "1999\tMissing Graph\t1\t1\t9999\tAssets/Demo/Icons/missing.png\ttrue\n",
                sourcePath: "Docs/Demo/Config/BrokenReference.tsv",
                contentHash: "demo-broken",
                enumRegistry: CreateDemoEnumRegistry());
        }

        public static ConfigEnumRegistry CreateDemoEnumRegistry()
        {
            var registry = new ConfigEnumRegistry();
            registry.Register(new ConfigEnumDomain(DemoActionCategoryEnumId, displayName: "Demo Action Category")
                .AddValue(new ConfigEnumValue(1, "Attack", "攻击"))
                .AddValue(new ConfigEnumValue(2, "Defense", "防御"))
                .AddValue(new ConfigEnumValue(3, "Utility", "功能")));

            registry.Register(new ConfigEnumDomain(DemoActionTagsEnumId, isFlags: true, displayName: "Demo Action Tags")
                .AddValue(new ConfigEnumValue(1, "Area", "范围"))
                .AddValue(new ConfigEnumValue(2, "Projectile", "飞行物"))
                .AddValue(new ConfigEnumValue(4, "Interrupt", "打断"))
                .AddValue(new ConfigEnumValue(8, "Shield", "护盾")));
            return registry;
        }

        private static ConfigSchema CreateDemoActionCatalogSchema()
        {
            return new ConfigSchema(
                    "DemoActionCatalog",
                    null,
                    displayName: "Demo Action Catalog",
                    description: "Pure demo table for enum, flags, asset path and graph reference preview.",
                    structureKind: ConfigStructureKind.Table)
                .AddField(new ConfigField("Id", ConfigFieldType.Integer, displayName: "编号", required: true))
                .AddField(new ConfigField("DisplayName", ConfigFieldType.String, displayName: "显示名称", required: true))
                .AddField(new ConfigField("Category", ConfigFieldType.Enum, displayName: "动作分类", enumId: DemoActionCategoryEnumId))
                .AddField(new ConfigField("Tags", ConfigFieldType.Enum, displayName: "动作标签", enumId: DemoActionTagsEnumId))
                .AddField(new ConfigField(
                    "GraphId",
                    ConfigFieldType.ConfigReference,
                    displayName: "动作图",
                    referenceRule: new ConfigReferenceRule("GraphId", "DemoActionGraph", ConfigStructureKind.Graph)))
                .AddField(new ConfigField("IconPath", ConfigFieldType.AssetPath, displayName: "图标路径"))
                .AddField(new ConfigField("Enabled", ConfigFieldType.Boolean, displayName: "启用"));
        }

        private static ConfigSchema CreateDemoActionGraphSchema()
        {
            return new ConfigSchema(
                    "DemoActionGraph",
                    null,
                    displayName: "Demo Action Graph",
                    description: "Pure demo graph index for cross-source reference preview.",
                    structureKind: ConfigStructureKind.Graph)
                .AddField(new ConfigField("Id", ConfigFieldType.Integer, displayName: "编号", required: true))
                .AddField(new ConfigField("Name", ConfigFieldType.String, displayName: "名称", required: true))
                .AddField(new ConfigField("EntryNode", ConfigFieldType.String, displayName: "入口节点"))
                .AddField(new ConfigField("NodeCount", ConfigFieldType.Integer, displayName: "节点数量"));
        }

        private static ConfigTable<BasicBuffConfig> CreateSampleBuffTable()
        {
            var table = new ConfigTable<BasicBuffConfig>(BasicBuffConfig.CreateSchema());
            table.Add(new BasicBuffConfig(
                100001,
                new LocalizedTextKey("buff.burn.name"),
                new LocalizedTextKey("buff.burn.desc"),
                5f,
                3,
                modifierId: 200001));
            return table;
        }

        private static ConfigTable<BasicModifierConfig> CreateSampleModifierTable()
        {
            var table = new ConfigTable<BasicModifierConfig>(BasicModifierConfig.CreateSchema());
            table.Add(new BasicModifierConfig(
                200001,
                new LocalizedTextKey("mod.power.name"),
                new LocalizedTextKey("mod.power.desc"),
                paramIndex: 1,
                parameters: new[] { 10, 20 }));
            return table;
        }

        private static MemoryLocalizationProvider CreateSampleLocalization()
        {
            var localization = new MemoryLocalizationProvider();
            localization.Register(new LocalizedTextKey("buff.burn.name"), LocaleId.ZhCN, "燃烧");
            localization.Register(new LocalizedTextKey("buff.burn.name"), LocaleId.EnUS, "Burn");
            localization.Register(new LocalizedTextKey("buff.burn.desc"), LocaleId.ZhCN, "持续伤害");
            localization.Register(new LocalizedTextKey("buff.burn.desc"), LocaleId.EnUS, "Damage over time");
            localization.Register(new LocalizedTextKey("mod.power.name"), LocaleId.ZhCN, "强化");
            localization.Register(new LocalizedTextKey("mod.power.name"), LocaleId.EnUS, "Power");
            localization.Register(new LocalizedTextKey("mod.power.desc"), LocaleId.ZhCN, "提升效果");
            localization.Register(new LocalizedTextKey("mod.power.desc"), LocaleId.EnUS, "Improve effect");
            return localization;
        }
    }
}
