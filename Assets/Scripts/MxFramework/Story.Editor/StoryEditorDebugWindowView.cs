using MxFramework.DebugUI;
using UnityEngine;
using UnityEngine.UIElements;

namespace MxFramework.Story.Editor
{
    public static class StoryEditorDebugWindowView
    {
        public const string RootName = "story-runtime-debug-root";
        public const string TitleName = "story-runtime-debug-title";
        public const string ContentName = "story-runtime-debug-content";
        public const string ReportName = "story-runtime-debug-report";

        public static void BuildReadonlyTree(VisualElement host, DebugUiDashboardViewModel dashboard, string reportText)
        {
            if (host == null)
            {
                return;
            }

            RemoveExistingRoot(host);

            var root = new VisualElement { name = RootName };
            root.style.flexDirection = FlexDirection.Column;
            root.style.flexGrow = 1;

            var title = new Label("Story 运行时调试") { name = TitleName };
            title.style.unityFontStyleAndWeight = FontStyle.Bold;
            title.style.fontSize = 18;
            title.style.marginBottom = 4;
            root.Add(title);

            var note = new Label("只读快照。触发、选择、表演完成等调试动作必须通过显式命令入口进入 Story RuntimeCommandBuffer。");
            note.style.whiteSpace = WhiteSpace.Normal;
            note.style.color = new Color(0.58f, 0.58f, 0.58f);
            note.style.marginBottom = 8;
            root.Add(note);

            var content = new VisualElement { name = ContentName };
            content.style.flexDirection = FlexDirection.Column;
            content.style.flexGrow = 1;
            root.Add(content);

            AddDashboard(content, dashboard ?? DebugUiDashboardViewModel.Empty);

            var report = new TextField("快照报告") { name = ReportName };
            report.multiline = true;
            report.isReadOnly = true;
            report.value = reportText ?? string.Empty;
            report.style.height = 180;
            report.style.marginTop = 10;
            root.Add(report);

            host.Add(root);
        }

        private static void AddDashboard(VisualElement root, DebugUiDashboardViewModel dashboard)
        {
            if (dashboard.SourceCount == 0)
            {
                root.Add(CreateSection("状态", "没有已注册的 Story Runtime 调试源。"));
                return;
            }

            for (int i = 0; i < dashboard.Sources.Count; i++)
            {
                DebugUiSourceViewModel source = dashboard.Sources[i];
                root.Add(CreateSourceHeader(source));
                for (int j = 0; j < source.Sections.Count; j++)
                {
                    DebugUiSectionViewModel section = source.Sections[j];
                    root.Add(CreateSection(section.Title, section.Body));
                }
            }
        }

        private static VisualElement CreateSourceHeader(DebugUiSourceViewModel source)
        {
            var label = new Label(source.SourceName + "  " + source.Status);
            label.style.unityFontStyleAndWeight = FontStyle.Bold;
            label.style.marginTop = 8;
            label.style.marginBottom = 4;
            return label;
        }

        private static VisualElement CreateSection(string titleText, string bodyText)
        {
            var section = new VisualElement();
            section.style.borderTopWidth = 1;
            section.style.borderRightWidth = 1;
            section.style.borderBottomWidth = 1;
            section.style.borderLeftWidth = 1;
            section.style.borderTopColor = new Color(0.24f, 0.24f, 0.24f);
            section.style.borderRightColor = new Color(0.24f, 0.24f, 0.24f);
            section.style.borderBottomColor = new Color(0.24f, 0.24f, 0.24f);
            section.style.borderLeftColor = new Color(0.24f, 0.24f, 0.24f);
            section.style.borderTopLeftRadius = 4;
            section.style.borderTopRightRadius = 4;
            section.style.borderBottomLeftRadius = 4;
            section.style.borderBottomRightRadius = 4;
            section.style.paddingLeft = 8;
            section.style.paddingRight = 8;
            section.style.paddingTop = 6;
            section.style.paddingBottom = 6;
            section.style.marginBottom = 6;

            var title = new Label(titleText ?? string.Empty);
            title.style.unityFontStyleAndWeight = FontStyle.Bold;
            section.Add(title);

            var body = new Label(bodyText ?? string.Empty);
            body.style.whiteSpace = WhiteSpace.Normal;
            body.style.marginTop = 4;
            section.Add(body);
            return section;
        }

        private static void RemoveExistingRoot(VisualElement host)
        {
            for (int i = host.childCount - 1; i >= 0; i--)
            {
                if (host[i].name == RootName)
                {
                    host.RemoveAt(i);
                }
            }
        }
    }
}
