using MxFramework.Diagnostics;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace MxFramework.Editor
{
    public sealed partial class FrameworkManager
    {
        private VisualElement _runtimeBannerContainer;

        private VisualElement CreateRuntimePanel()
        {
            var panel = CreatePanel("运行模式");
            panel.style.flexGrow = 1;
            panel.style.minHeight = 300;

            var scroll = new ScrollView(ScrollViewMode.Vertical);
            scroll.style.flexGrow = 1;

            _runtimeBannerContainer = new VisualElement();
            scroll.Add(_runtimeBannerContainer);

            _runtimeLabel = new Label();
            _runtimeLabel.style.display = DisplayStyle.None;
            scroll.Add(_runtimeLabel);

            var note = new Label("运行模式默认只读。后续游戏层通过 IFrameworkDebugSource 提供 Attribute、Buff、Modifier、AI 等快照。");
            note.style.whiteSpace = WhiteSpace.Normal;
            note.style.marginTop = 12;
            note.style.fontSize = 11;
            note.style.color = new Color(0.55f, 0.55f, 0.55f);
            scroll.Add(note);

            panel.Add(scroll);
            return panel;
        }

        private void RefreshRuntimeView()
        {
            string body;
            bool isError = false;
            if (!EditorApplication.isPlaying)
            {
                body = "当前不在 Play Mode。\n\n运行模式需要进入 Play Mode，或由游戏层注册 Runtime Debug Source 后才能显示真实快照。";
                isError = true;
            }
            else
            {
                body = "当前处于 Play Mode，但还没有连接 IFrameworkDebugSource。\n\n后续阶段会提供 Debug Source 注册入口，用于显示运行时 Attribute、Buff、Modifier、Counter 和 AI 快照。";
            }

            var snapshot = new FrameworkDebugSnapshot(
                "FrameworkManager",
                FrameworkDebugMode.Runtime,
                new[] { new FrameworkDebugSection("运行模式", body) });
            _lastReport = FrameworkDebugReportExporter.ExportText(snapshot);

            if (_runtimeBannerContainer != null)
            {
                _runtimeBannerContainer.Clear();
                string titleText = !EditorApplication.isPlaying ? "未进入播放模式" : "尚未注册 Debug Source";
                VisualElement banner = CreateAlertBanner(titleText, body, isError);
                _runtimeBannerContainer.Add(banner);
            }

            if (_runtimeLabel != null)
                _runtimeLabel.text = body;
        }
    }
}
