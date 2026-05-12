using MxFramework.Config;

namespace MxFramework.Config.Runtime
{
    public sealed class BasicBuffConfig : IBuffConfig
    {
        public BasicBuffConfig(
            int id,
            LocalizedTextKey nameText,
            LocalizedTextKey descriptionText,
            float duration,
            int maxLayers,
            bool isPermanent = false,
            int modifierId = 0)
        {
            Id = id;
            NameText = nameText;
            DescriptionText = descriptionText;
            Duration = duration;
            MaxLayers = maxLayers;
            IsPermanent = isPermanent;
            ModifierId = modifierId;
        }

        public int Id { get; }
        public LocalizedTextKey NameText { get; }
        public LocalizedTextKey DescriptionText { get; }
        public float Duration { get; }
        public int MaxLayers { get; }
        public bool IsPermanent { get; }
        public int ModifierId { get; }

        public static ConfigSchema<BasicBuffConfig> CreateSchema()
        {
            var schema = new ConfigSchema<BasicBuffConfig>(
                "BasicBuffConfig",
                displayName: "Basic Buff Config",
                description: "Config-backed generic buff.",
                idRange: new ConfigIdRange(100000, 199999));

            schema
                .AddField(new ConfigField("Id", ConfigFieldType.Integer, displayName: "编号", required: true))
                .AddField(new ConfigField("NameText", ConfigFieldType.LocalizedText, displayName: "名称文本", required: true))
                .AddField(new ConfigField("DescriptionText", ConfigFieldType.LocalizedText, displayName: "描述文本"))
                .AddField(new ConfigField("Duration", ConfigFieldType.Float, displayName: "持续时间", required: true))
                .AddField(new ConfigField("MaxLayers", ConfigFieldType.Integer, displayName: "最大层数", required: true))
                .AddField(new ConfigField("IsPermanent", ConfigFieldType.Boolean, displayName: "永久生效"))
                .AddField(new ConfigField(
                    "ModifierId",
                    ConfigFieldType.ConfigReference,
                    displayName: "效果修改器",
                    referenceRule: new ConfigReferenceRule("ModifierId", typeof(BasicModifierConfig), required: false)))
                .RequireLocale(LocaleId.ZhCN)
                .RequireLocale(LocaleId.EnUS);

            return schema;
        }
    }
}
