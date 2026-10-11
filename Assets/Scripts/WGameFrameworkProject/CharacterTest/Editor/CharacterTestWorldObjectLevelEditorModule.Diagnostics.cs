using System;
using System.Collections.Generic;
using System.IO;
using MxFramework.Config.Levels;
using MxFramework.Editor.Levels;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace MxFramework.Game.CharacterTest.Editor
{
    public sealed partial class CharacterTestWorldObjectLevelEditorModule
    {
        public void BuildDiagnostics(MxLevelEditorContext context, VisualElement diagnostics)
        {
            _activeContext = context;
            _diagnostics = diagnostics;
            RefreshDiagnostics();
        }

        private void RefreshDiagnostics()
        {
            if (_diagnostics == null)
                return;

            _diagnostics.Clear();
            _diagnostics.Add(new Label(
                "摘要：可移动物体="
                + Count(_document.MovableInstances)
                + "，单个静态碰撞体="
                + Count(_document.SingleStaticColliders)
                + "，静态批次="
                + Count(_document.StaticColliderBatches)
                + "，关卡点="
                + Count(_document.Points)
                + "，区域="
                + Count(_document.Regions)
                + "，错误="
                + (_report?.ErrorCount ?? 0)
                + "，警告="
                + (_report?.WarningCount ?? 0)));
            if (_unregisteredSceneObjects.Count > 0)
            {
                _diagnostics.Add(new HelpBox(
                    "发现 " + _unregisteredSceneObjects.Count
                    + " 个未登记场景对象。它们不会进入运行时缓存；请使用顶部“未登记”按钮逐个处理。",
                    HelpBoxMessageType.Warning));
            }
            if (_report == null || _report.Issues.Count == 0)
                return;

            int max = Math.Min(12, _report.Issues.Count);
            for (int i = 0; i < max; i++)
            {
                LevelWorldObjectAuthoringIssue issue = _report.Issues[i];
                var label = new Label(CharacterTestLevelEditorChineseText.DiagnosticText(issue))
                {
                    tooltip = CharacterTestLevelEditorChineseText.DiagnosticTooltip(issue)
                };
                _diagnostics.Add(label);
                string location = issue.Path;
                _diagnostics.Add(new Button(() => RevealLevelIssue(_activeContext.Window, location))
                    { text = "定位区域／对象", name = "mx-level-world-locate-" + i });
            }
        }

    }
}
