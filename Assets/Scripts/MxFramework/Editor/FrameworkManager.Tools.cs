using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace MxFramework.Editor
{
    public sealed partial class FrameworkManager
    {
        private VisualElement CreateToolsPanel()
        {
            var panel = CreatePanel("工具入口");
            panel.style.flexGrow = 1;
            panel.style.minHeight = 300;

            var scroll = new ScrollView(ScrollViewMode.Vertical);
            scroll.style.flexGrow = 1;

            IReadOnlyList<FrameworkManagerToolInfo> tools = FrameworkManagerToolRegistry.GetTools();
            if (tools.Count == 0)
            {
                var empty = new Label("还没有注册工具入口。其他 Editor 程序集可通过 FrameworkManagerToolRegistry.Register(...) 接入。");
                empty.style.whiteSpace = WhiteSpace.Normal;
                scroll.Add(empty);
            }
            else
            {
                string currentGroup = string.Empty;
                for (int i = 0; i < tools.Count; i++)
                {
                    FrameworkManagerToolInfo tool = tools[i];
                    if (tool.Group != currentGroup)
                    {
                        currentGroup = tool.Group;
                        var groupLabel = new Label(currentGroup);
                        groupLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
                        groupLabel.style.marginTop = i == 0 ? 0 : 10;
                        groupLabel.style.marginBottom = 4;
                        scroll.Add(groupLabel);
                    }

                    scroll.Add(CreateToolRow(tool));
                }
            }

            panel.Add(scroll);
            return panel;
        }

        private VisualElement CreateToolRow(FrameworkManagerToolInfo tool)
        {
            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.alignItems = Align.Center;
            row.style.borderBottomWidth = 1;
            row.style.borderBottomColor = new Color(0.18f, 0.18f, 0.18f);
            row.style.paddingTop = 4;
            row.style.paddingBottom = 4;

            var text = new Label(CreateToolSummary(tool));
            text.style.whiteSpace = WhiteSpace.Normal;
            text.style.flexGrow = 1;
            row.Add(text);

            row.Add(CreateButton("打开", () => OpenTool(tool)));
            return row;
        }

        private static string CreateToolSummary(FrameworkManagerToolInfo tool)
        {
            string summary = tool.DisplayName + "    " + tool.Status;
            if (!string.IsNullOrEmpty(tool.MenuPath))
                summary += "\n" + tool.MenuPath;
            if (!string.IsNullOrEmpty(tool.Description))
                summary += "\n" + tool.Description;
            return summary;
        }

        private void OpenTool(FrameworkManagerToolInfo tool)
        {
            if (!tool.HasOpenAction)
            {
                ShowNotification(new GUIContent("工具没有注册打开动作"));
                return;
            }

            tool.Open();
            _lastReport = "Framework Manager Tool\n"
                + "id: " + tool.Id + "\n"
                + "name: " + tool.DisplayName + "\n"
                + "menu: " + tool.MenuPath;
        }
    }
}
