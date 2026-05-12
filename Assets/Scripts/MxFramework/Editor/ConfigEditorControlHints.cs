using MxFramework.Config;

namespace MxFramework.Editor
{
    public static class ConfigEditorControlHints
    {
        public static ConfigEditorCellPreview CreateCellPreview(
            ConfigField field,
            string value,
            IConfigEditorEnumProvider enumProvider = null)
        {
            string displayValue = value ?? string.Empty;
            string enumId = field.EnumId;
            string[] options = new string[0];

            if (!string.IsNullOrEmpty(enumId) && enumProvider != null && enumProvider.TryGetEnumDomain(enumId, out ConfigEnumDomain domain))
            {
                displayValue = FormatEnumValue(domain, value);
                options = CreateEnumOptions(domain);
            }

            return new ConfigEditorCellPreview(
                field.Name,
                field.DisplayName,
                value,
                displayValue,
                GetControlHint(field),
                enumId,
                options);
        }

        public static string GetControlHint(ConfigField field)
        {
            if (field.ReferenceRule.IsValid)
                return "引用选择器";

            if (!string.IsNullOrEmpty(field.EnumId))
                return "枚举/Flags";

            switch (field.FieldType)
            {
                case ConfigFieldType.Integer:
                    return "整数输入";
                case ConfigFieldType.Float:
                    return "浮点输入";
                case ConfigFieldType.Boolean:
                    return "开关";
                case ConfigFieldType.Enum:
                    return "枚举下拉";
                case ConfigFieldType.ConfigReference:
                    return "引用选择器";
                case ConfigFieldType.LocalizedText:
                    return "多语言 Key";
                case ConfigFieldType.AssetPath:
                    return "资源路径";
                case ConfigFieldType.Custom:
                    return "自定义控件";
                default:
                    return "文本输入";
            }
        }

        private static string FormatEnumValue(ConfigEnumDomain domain, string value)
        {
            if (domain == null || string.IsNullOrEmpty(value))
                return value ?? string.Empty;

            return int.TryParse(value, out int intValue) ? domain.FormatValue(intValue) : value;
        }

        private static string[] CreateEnumOptions(ConfigEnumDomain domain)
        {
            if (domain == null)
                return new string[0];

            var options = new string[domain.Values.Count];
            for (int i = 0; i < domain.Values.Count; i++)
            {
                ConfigEnumValue value = domain.Values[i];
                options[i] = string.IsNullOrEmpty(value.DisplayName)
                    ? value.Name + " (" + value.Value + ")"
                    : value.DisplayName + " " + value.Name + " (" + value.Value + ")";
            }

            return options;
        }
    }
}
