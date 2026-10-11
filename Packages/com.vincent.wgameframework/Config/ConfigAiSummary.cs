using System.Text;

namespace MxFramework.Config
{
    public readonly struct ConfigAiSummary
    {
        public ConfigAiSummary(string tableName, string text)
        {
            TableName = tableName ?? string.Empty;
            Text = text ?? string.Empty;
        }

        public string TableName { get; }
        public string Text { get; }
    }

    public static class ConfigSchemaExporter
    {
        public static ConfigAiSummary ExportForAi(ConfigSchema schema)
        {
            if (schema == null)
                return new ConfigAiSummary(string.Empty, string.Empty);

            var builder = new StringBuilder();
            builder.Append("table: ").Append(schema.TableName).Append('\n');
            builder.Append("type: ").Append(schema.ConfigType != null ? schema.ConfigType.FullName : string.Empty).Append('\n');
            builder.Append("structure: ").Append(schema.StructureKind).Append('\n');
            if (schema.IdRange.IsValid)
                builder.Append("idRange: ").Append(schema.IdRange.MinInclusive).Append('-').Append(schema.IdRange.MaxInclusive).Append('\n');

            builder.Append("fields:\n");
            for (int i = 0; i < schema.Fields.Count; i++)
            {
                ConfigField field = schema.Fields[i];
                builder.Append("- ").Append(field.Name).Append(": ").Append(field.FieldType);
                if (!string.IsNullOrEmpty(field.EnumId))
                    builder.Append(" enum=").Append(field.EnumId);
                if (field.ReferenceRule.IsValid)
                    builder.Append(" -> ").Append(field.ReferenceRule.GetTargetDisplayName());
                if (!string.IsNullOrEmpty(field.Description))
                    builder.Append(" # ").Append(field.Description);
                builder.Append('\n');
            }

            if (schema.RequiredLocales.Count > 0)
            {
                builder.Append("requiredLocales:");
                for (int i = 0; i < schema.RequiredLocales.Count; i++)
                    builder.Append(' ').Append(schema.RequiredLocales[i]);
                builder.Append('\n');
            }

            return new ConfigAiSummary(schema.TableName, builder.ToString());
        }
    }
}
