using System;
using MxFramework.Config.Levels;
using MxFramework.Gameplay.Triggers;
using UnityEditor;
using UnityEngine.UIElements;

namespace MxFramework.Game.CharacterTest.Editor
{
    public sealed partial class CharacterTestWorldObjectLevelEditorModule
    {
        private void RefreshTriggerInspectorAfterMutation()
        {
            ForceValidate();
            if (IsTriggerWorkspaceAttached())
            {
                RefreshTriggerWorkspaceAfterMutation();
                return;
            }
            RefreshInspector(preserveScrollPosition: true);
            SceneView.RepaintAll();
        }

        private void AddGameplayTriggerDiagnostics(VisualElement card)
        {
            if (_report?.Issues == null)
                return;

            int shown = 0;
            string triggerPath = "$.modules."
                + CharacterTestLevelGameplayTriggerAuthoringAdapter.ModuleId;
            for (int i = 0; i < _report.Issues.Count && shown < 4; i++)
            {
                LevelWorldObjectAuthoringIssue issue = _report.Issues[i];
                if (issue == null
                    || string.IsNullOrWhiteSpace(issue.Path)
                    || !issue.Path.StartsWith(
                        triggerPath,
                        StringComparison.Ordinal))
                {
                    continue;
                }

                HelpBoxMessageType type =
                    issue.Severity == LevelWorldObjectAuthoringSeverity.Error
                        ? HelpBoxMessageType.Error
                        : HelpBoxMessageType.Warning;
                var helpBox = new HelpBox(
                    CharacterTestLevelEditorChineseText.DiagnosticText(issue),
                    type)
                {
                    name = "character-test-trigger-diagnostic",
                    tooltip = CharacterTestLevelEditorChineseText
                        .DiagnosticTooltip(issue)
                };
                card.Add(helpBox);
                shown++;
            }
        }

        private CharacterTestLevelGameplayTriggerRegionBinding
            FindTriggerRegionBinding(int sourceBindingId)
        {
            if (_triggerDocument?.RegionBindings == null)
                return null;
            for (int i = 0;
                 i < _triggerDocument.RegionBindings.Count;
                 i++)
            {
                CharacterTestLevelGameplayTriggerRegionBinding binding =
                    _triggerDocument.RegionBindings[i];
                if (binding?.SourceBindingId == sourceBindingId)
                    return binding;
            }
            return null;
        }

        private static string ResolveTriggerTemplateName(string templateId)
        {
            if (IsStoryTriggerTemplate(templateId))
            {
                return "进入时触发剧情";
            }

            if (string.Equals(
                    templateId,
                    CharacterTestLevelGameplayTriggerAuthoringDocument
                        .GameplayApplyBuffEnterOnceTemplateId,
                    StringComparison.Ordinal))
            {
                return "进入时施加 Buff";
            }

            if (string.Equals(
                    templateId,
                    CharacterTestLevelGameplayTriggerAuthoringDocument
                        .GameplayApplyRemoveBuffPresenceTemplateId,
                    StringComparison.Ordinal))
            {
                return "区域内维持 Buff";
            }

            return "未知触发器";
        }

        private static string ResolveTriggerAction(string templateId)
        {
            if (string.Equals(
                    templateId,
                    CharacterTestLevelGameplayTriggerAuthoringDocument
                        .StoryRaiseTriggerEnterOnceTemplateId,
                    StringComparison.Ordinal))
            {
                return "触发剧情";
            }

            return string.Equals(
                templateId,
                CharacterTestLevelGameplayTriggerAuthoringDocument
                    .GameplayApplyRemoveBuffPresenceTemplateId,
                StringComparison.Ordinal)
                ? "进入施加 Buff，离开移除 Buff"
                : "施加 Buff";
        }

        private static GameplayTriggerDeliveryContract
            ResolveTriggerDeliveryContract(string templateId)
        {
            return IsStoryTriggerTemplate(templateId)
                ? GameplayTriggerDeliveryContract.ReliableDeferred
                : GameplayTriggerDeliveryContract.SameFrameReserved;
        }
    }
}
