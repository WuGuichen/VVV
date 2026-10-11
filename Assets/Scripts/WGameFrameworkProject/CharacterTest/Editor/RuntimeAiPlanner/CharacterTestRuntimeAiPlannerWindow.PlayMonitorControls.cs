using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine.UIElements;

namespace MxFramework.Game.CharacterTest.Editor
{
    public sealed partial class CharacterTestRuntimeAiPlannerWindow
    {
        private static VisualElement CreatePlayMonitorScenePreviewControls()
        {
            var root = new VisualElement();
            root.style.flexDirection = FlexDirection.Column;
            root.style.minWidth = 0;
            root.Add(CreateSectionTitle("场景预览"));

            var enabled = new Toggle("场景预览")
            {
                name = "runtime-ai-planner-scene-preview-toggle",
                value = CharacterTestRuntimeAiPlannerScenePreview.Enabled
            };
            enabled.RegisterValueChangedCallback(evt =>
            {
                CharacterTestRuntimeAiPlannerScenePreview.Enabled = evt.newValue;
            });
            root.Add(enabled);

            var mode = new EnumField(
                "模式",
                CharacterTestRuntimeAiPlannerScenePreview.Mode)
            {
                name = "runtime-ai-planner-scene-preview-mode"
            };
            mode.RegisterValueChangedCallback(evt =>
            {
                if (evt.newValue is CharacterTestRuntimeAiPlannerScenePreviewMode previewMode)
                    CharacterTestRuntimeAiPlannerScenePreview.Mode = previewMode;
            });
            root.Add(mode);

            var labelMode = new EnumField(
                "标签",
                CharacterTestRuntimeAiPlannerScenePreview.LabelMode)
            {
                name = "runtime-ai-planner-scene-preview-label-mode"
            };
            labelMode.RegisterValueChangedCallback(evt =>
            {
                if (evt.newValue is CharacterTestRuntimeAiPlannerScenePreviewLabelMode previewLabelMode)
                    CharacterTestRuntimeAiPlannerScenePreview.LabelMode = previewLabelMode;
            });
            root.Add(labelMode);

            root.Add(CreateScenePreviewToggle(
                "显示图例",
                CharacterTestRuntimeAiPlannerScenePreview.ShowLegend,
                value => CharacterTestRuntimeAiPlannerScenePreview.ShowLegend = value));
            root.Add(CreateScenePreviewToggle(
                "显示目标线",
                CharacterTestRuntimeAiPlannerScenePreview.ShowTargetLine,
                value => CharacterTestRuntimeAiPlannerScenePreview.ShowTargetLine = value));
            root.Add(CreateScenePreviewToggle(
                "显示攻击范围",
                CharacterTestRuntimeAiPlannerScenePreview.ShowAttackRange,
                value => CharacterTestRuntimeAiPlannerScenePreview.ShowAttackRange = value));
            root.Add(CreateScenePreviewToggle(
                "显示移动箭头",
                CharacterTestRuntimeAiPlannerScenePreview.ShowDecisionMove,
                value => CharacterTestRuntimeAiPlannerScenePreview.ShowDecisionMove = value));

            root.Add(CreateWrappedLabel(
                "Play Mode 只读，刷新 "
                + CharacterTestRuntimeAiPlannerScenePreview.RefreshRateHz.ToString(
                    "0.#",
                    CultureInfo.InvariantCulture)
                + " Hz，当前绘制 agents="
                + CharacterTestRuntimeAiPlannerScenePreview.CachedItemCount.ToString(
                    CultureInfo.InvariantCulture)));
            return root;
        }

        private static Toggle CreateScenePreviewToggle(
            string label,
            bool value,
            Action<bool> setter)
        {
            var toggle = new Toggle(label)
            {
                value = value
            };
            toggle.RegisterValueChangedCallback(evt => setter(evt.newValue));
            return toggle;
        }

        private VisualElement CreatePlayMonitorRuntimeAiBehaviorLoadoutControls()
        {
            var root = new VisualElement();
            root.style.flexDirection = FlexDirection.Column;
            root.style.minWidth = 0;
            root.Add(CreateSectionTitle("Behavior Loadout"));

            if (!EditorApplication.isPlaying
                || !TryGetRuntimeAiBehaviorSelectionTarget(
                    out CharacterTestRuntimeAiPlannerEditorDebugTarget target))
            {
                root.Add(CreateWrappedLabel("No runtime behavior loadout controls are registered."));
                return root;
            }

            root.Add(CreateReadOnlyLine(
                "Phase",
                target.RuntimeAiBehaviorSelectionUnlocked ? "unlocked" : "locked"));
            root.Add(CreateReadOnlyLine(
                "Panel",
                target.RuntimeAiBehaviorLoadoutPanelOpen ? "open" : "closed"));
            string panelStatus = target.RuntimeAiBehaviorLoadoutPanelStatus;
            if (!string.IsNullOrWhiteSpace(panelStatus))
                root.Add(CreateWrappedLabel(panelStatus));

            Button phaseButton = new Button(() =>
            {
                target.SetRuntimeAiBehaviorSelectionUnlocked(!target.RuntimeAiBehaviorSelectionUnlocked);
                RefreshView();
            })
            {
                name = RuntimeAiBehaviorSelectionPhaseButtonName,
                text = target.RuntimeAiBehaviorSelectionUnlocked
                    ? "Lock selection phase"
                    : "Unlock selection phase"
            };
            root.Add(phaseButton);

            Button panelButton = new Button(() =>
            {
                if (target.RuntimeAiBehaviorLoadoutPanelOpen)
                    target.CloseRuntimeAiBehaviorLoadoutPanel();
                else
                    target.OpenRuntimeAiBehaviorLoadoutPanel();
                RefreshView();
            })
            {
                name = RuntimeAiBehaviorLoadoutPanelButtonName,
                text = target.RuntimeAiBehaviorLoadoutPanelOpen
                    ? "Close full-screen panel"
                    : "Open full-screen panel"
            };
            panelButton.SetEnabled(target.CanOpenRuntimeAiBehaviorLoadoutPanel
                || target.CanCloseRuntimeAiBehaviorLoadoutPanel);
            root.Add(panelButton);

            root.Add(CreateWrappedLabel(
                "The full-screen panel only submits SetRuntimeAiBehaviorSelection; behavior group editing and hot update stay separate."));
            return root;
        }

        private static bool TryGetRuntimeAiBehaviorSelectionTarget(
            out CharacterTestRuntimeAiPlannerEditorDebugTarget target)
        {
            IReadOnlyList<CharacterTestRuntimeAiPlannerEditorDebugTarget> targets =
                CharacterTestRuntimeAiPlannerEditorDebugRegistry.Targets;
            for (int i = 0; i < targets.Count; i++)
            {
                CharacterTestRuntimeAiPlannerEditorDebugTarget candidate = targets[i];
                if (candidate != null && candidate.HasRuntimeAiBehaviorSelectionControls)
                {
                    target = candidate;
                    return true;
                }
            }

            target = null;
            return false;
        }
    }
}
