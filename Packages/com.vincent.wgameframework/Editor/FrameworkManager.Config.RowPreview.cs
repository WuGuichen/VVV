using System.Collections.Generic;
using MxFramework.Config;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace MxFramework.Editor
{
    public sealed partial class FrameworkManager
    {
        private static VisualElement CreateRowPreview(IConfigEditorSource source, int maxRows)
        {
            var container = new VisualElement();
            if (!(source is IConfigEditorTablePreviewProvider provider))
            {
                var missing = new Label("当前配置源未提供行视图预览。");
                missing.style.whiteSpace = WhiteSpace.Normal;
                container.Add(missing);
                return container;
            }

            IReadOnlyList<ConfigEditorRowPreview> rows = provider.CreateRowPreview(maxRows);
            if (rows.Count == 0)
            {
                var empty = new Label("当前配置源没有可预览行。");
                empty.style.whiteSpace = WhiteSpace.Normal;
                container.Add(empty);
                return container;
            }

            for (int i = 0; i < rows.Count; i++)
            {
                ConfigEditorRowPreview row = rows[i];
                var rowTitle = new Label("Row " + (row.RowId > 0 ? row.RowId.ToString() : (i + 1).ToString()));
                rowTitle.style.unityFontStyleAndWeight = FontStyle.Bold;
                rowTitle.style.marginTop = i == 0 ? 0 : 6;
                container.Add(rowTitle);

                for (int j = 0; j < row.Cells.Count; j++)
                {
                    ConfigEditorCellPreview cell = row.Cells[j];
                    container.Add(CreateReadonlyCellControl(cell));
                }
            }

            var note = new Label("当前为只读控件映射预览，保存功能将在 Table Editor v1 后续阶段开启。");
            note.style.whiteSpace = WhiteSpace.Normal;
            note.style.marginTop = 8;
            note.style.color = new Color(0.55f, 0.55f, 0.55f);
            container.Add(note);
            return container;
        }

        private static VisualElement CreateReadonlyCellControl(ConfigEditorCellPreview cell)
        {
            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.alignItems = Align.Center;
            row.style.marginBottom = 3;

            var label = new Label(CreateCellFieldLabel(cell));
            label.style.width = 120;
            label.style.whiteSpace = WhiteSpace.Normal;
            row.Add(label);

            VisualElement control = CreateReadonlyControl(cell);
            control.SetEnabled(false);
            control.style.flexGrow = 1;
            row.Add(control);

            var hint = new Label(cell.ControlHint);
            hint.style.width = 92;
            hint.style.marginLeft = 6;
            hint.style.color = new Color(0.55f, 0.55f, 0.55f);
            row.Add(hint);

            return row;
        }

        private static string CreateFieldLabel(ConfigField field)
        {
            if (string.IsNullOrEmpty(field.DisplayName))
                return field.Name;

            return field.DisplayName + "\n" + field.Name;
        }

        private static string CreateReferenceFieldLabel(ConfigSchema schema, string fieldName)
        {
            if (schema == null)
                return fieldName;

            for (int i = 0; i < schema.Fields.Count; i++)
            {
                ConfigField field = schema.Fields[i];
                if (field.Name == fieldName && !string.IsNullOrEmpty(field.DisplayName))
                    return field.DisplayName + " " + field.Name;
            }

            return fieldName;
        }

        private static string CreateCellFieldLabel(ConfigEditorCellPreview cell)
        {
            if (string.IsNullOrEmpty(cell.FieldDisplayName))
                return cell.FieldName;

            return cell.FieldDisplayName + "\n" + cell.FieldName;
        }

        private static VisualElement CreateReadonlyControl(ConfigEditorCellPreview cell)
        {
            string value = cell.Value ?? string.Empty;
            switch (cell.ControlHint)
            {
                case "开关":
                    bool enabled = value == "True" || value == "true" || value == "1";
                    return new Toggle { value = enabled };
                case "枚举/Flags":
                case "枚举下拉":
                    return CreateReadonlyOptionsControl(cell);
                case "引用选择器":
                    return new Button { text = string.IsNullOrEmpty(value) ? "选择引用" : value };
                case "资源路径":
                    return new Button { text = string.IsNullOrEmpty(value) ? "选择资源" : value };
                default:
                    return new TextField { value = string.IsNullOrEmpty(cell.DisplayValue) ? value : cell.DisplayValue };
            }
        }

        private static VisualElement CreateReadonlyOptionsControl(ConfigEditorCellPreview cell)
        {
            var options = new List<string>();
            string selected = string.IsNullOrEmpty(cell.DisplayValue) ? cell.Value : cell.DisplayValue;
            if (!string.IsNullOrEmpty(selected))
                options.Add(selected);

            for (int i = 0; i < cell.Options.Count; i++)
            {
                if (!options.Contains(cell.Options[i]))
                    options.Add(cell.Options[i]);
            }

            if (options.Count == 0)
                options.Add("-");

            return new PopupField<string>(options, 0);
        }

        private static string CreateConfigChangeSummaryText(ConfigChangeReport report)
        {
            if (report == null || !report.HasBaseline)
                return "配置变动：已建立初始基线";

            if (!report.HasChanges)
                return "配置变动：无变化";

            return "配置变动：发现变化" +
                "\n新增源：" + report.AddedSourceCount +
                "\n移除源：" + report.RemovedSourceCount +
                "\n变化源：" + report.ChangedSourceCount +
                "\n错误类型变化：" + report.IssueTypeChangeCount;
        }
    }
}
