using MxFramework.Config;

namespace MxFramework.Config.Runtime
{
    public sealed class BasicModifierConfig : IModifierConfig
    {
        public BasicModifierConfig(
            int id,
            LocalizedTextKey nameText,
            LocalizedTextKey descriptionText,
            int paramIndex = 0,
            int[] parameters = null)
        {
            Id = id;
            NameText = nameText;
            DescriptionText = descriptionText;
            ParamIndex = paramIndex;
            Parameters = parameters ?? new int[0];
        }

        public int Id { get; }
        public LocalizedTextKey NameText { get; }
        public LocalizedTextKey DescriptionText { get; }
        public int ParamIndex { get; }
        public int[] Parameters { get; }

        public static ConfigSchema<BasicModifierConfig> CreateSchema()
        {
            var schema = new ConfigSchema<BasicModifierConfig>(
                "BasicModifierConfig",
                displayName: "Basic Modifier Config",
                description: "Config-backed generic modifier.",
                idRange: new ConfigIdRange(200000, 299999));

            schema
                .AddField(new ConfigField("Id", ConfigFieldType.Integer, displayName: "编号", required: true))
                .AddField(new ConfigField("NameText", ConfigFieldType.LocalizedText, displayName: "名称文本", required: true))
                .AddField(new ConfigField("DescriptionText", ConfigFieldType.LocalizedText, displayName: "描述文本"))
                .AddField(new ConfigField("ParamIndex", ConfigFieldType.Integer, displayName: "参数索引"))
                .AddField(new ConfigField("Parameters", ConfigFieldType.Custom, displayName: "参数列表"))
                .RequireLocale(LocaleId.ZhCN)
                .RequireLocale(LocaleId.EnUS);

            return schema;
        }
    }
}
