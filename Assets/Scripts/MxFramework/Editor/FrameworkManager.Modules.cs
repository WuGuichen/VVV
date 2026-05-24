using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace MxFramework.Editor
{
    public sealed partial class FrameworkManager
    {
        private VisualElement _validationBannerContainer;

        private VisualElement CreateModulePanel()
        {
            var panel = CreatePanel("模块列表");
            panel.style.flexGrow = 1;
            panel.style.marginRight = 8;
            panel.style.minHeight = 300;

            VisualElement header = CreateModuleRow("模块", "程序集", "依赖", "状态", isHeader: true);
            panel.Add(header);

            var scroll = new ScrollView(ScrollViewMode.Vertical);
            scroll.style.flexGrow = 1;

            IReadOnlyList<FrameworkModuleInfo> modules = MxEditorUtils.GetModules();
            for (int i = 0; i < modules.Count; i++)
            {
                FrameworkModuleInfo module = modules[i];
                VisualElement row = CreateModuleRow(module.Name, module.AssemblyName, module.Dependencies, module.Status, isHeader: false);
                _moduleRows.Add(row);

                // 奇偶斑马底色
                Color rowBg = (i % 2 == 1) ? new Color(0.16f, 0.16f, 0.16f) : Color.clear;
                row.style.backgroundColor = rowBg;

                // 悬停高亮
                row.RegisterCallback<MouseEnterEvent>(evt =>
                {
                    row.style.backgroundColor = new Color(0.2f, 0.24f, 0.32f);
                });
                row.RegisterCallback<MouseLeaveEvent>(evt =>
                {
                    row.style.backgroundColor = rowBg;
                });

                scroll.Add(row);
            }
            panel.Add(scroll);

            return panel;
        }

        private VisualElement CreateValidationPanel()
        {
            var panel = CreatePanel("验证结果");
            panel.style.width = 300;
            panel.style.flexShrink = 0;

            var scroll = new ScrollView(ScrollViewMode.Vertical);
            scroll.style.flexGrow = 1;

            _validationBannerContainer = new VisualElement();
            scroll.Add(_validationBannerContainer);

            // 保留原有 _validationLabel 做兼容，但其设为不可见或放在备用位置，这里选择设为隐藏或不塞入
            _validationLabel = new Label();
            _validationLabel.style.display = DisplayStyle.None;
            scroll.Add(_validationLabel);

            var note = new Label("Phase 8.0 只验证模块 asmdef 是否存在。依赖图、GitNexus 和沙盒检查会在后续阶段接入。");
            note.style.whiteSpace = WhiteSpace.Normal;
            note.style.marginTop = 12;
            note.style.fontSize = 11;
            note.style.color = new Color(0.55f, 0.55f, 0.55f);
            scroll.Add(note);

            panel.Add(scroll);
            return panel;
        }

        private void RefreshValidation()
        {
            if (_displayMode == EditorDisplayMode.Runtime)
            {
                RefreshRuntimeView();
                return;
            }

            IReadOnlyList<FrameworkModuleInfo> modules = MxEditorUtils.GetModules();
            bool valid = MxEditorUtils.ValidateModuleAssets(modules, out string report);
            _lastReport = MxEditorUtils.CreateFrameworkManagerReport(EditorDisplayMode.Authoring.ToString(), report);

            if (_validationBannerContainer != null)
            {
                _validationBannerContainer.Clear();
                string titleText = valid ? "模块资源：正常" : "模块资源：发现问题";
                VisualElement banner = CreateAlertBanner(titleText, report, !valid);
                _validationBannerContainer.Add(banner);
            }

            if (_validationLabel != null)
            {
                _validationLabel.text = (valid ? "模块资源：正常\n\n" : "模块资源：发现问题\n\n") + report;
                _validationLabel.style.color = valid ? new Color(0.45f, 0.78f, 0.45f) : new Color(1f, 0.55f, 0.35f);
            }
        }
    }
}
