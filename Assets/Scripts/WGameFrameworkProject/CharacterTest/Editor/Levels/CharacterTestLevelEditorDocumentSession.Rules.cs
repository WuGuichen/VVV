using System;
using System.Collections.Generic;
using MxFramework.Game.CharacterTest.TacticalCard;
using UnityEngine.UIElements;
namespace MxFramework.Game.CharacterTest.Editor
{
    internal sealed partial class CharacterTestLevelEditorDocumentSession
    {
        private void BuildExistingStageSettings(MxFramework.Editor.Levels.MxLevelEditorWindow window, VisualElement panel)
        {
            _draft.TryGetModule(CharacterTestTacticalStageAuthoringDocument.ModuleId, out var stage);
            var defaults = new CharacterTestTacticalStageAuthoringDocument();
            var integers = new[] {
                ("initialMomentum", "初始动能", defaults.InitialMomentum), ("handCapacity", "手牌容量", defaults.HandCapacity),
                ("requiredStableTicks", "稳定判定 tick", defaults.RequiredStableTicks),
                ("preActionBudgetTicks", "前置阶段预算 tick", defaults.PreActionBudgetTicks),
                ("executionBudgetTicks", "执行预算 tick", defaults.ExecutionBudgetTicks),
                ("ingressBudgetTicks", "入场预算 tick", defaults.IngressBudgetTicks),
                ("storyTransferBudgetTicks", "故事转移预算 tick", defaults.StoryTransferBudgetTicks) };
            foreach (var setting in integers)
            {
                var field = new IntegerField(setting.Item2) { name = "mx-level-rule-" + setting.Item1,
                    value = (int?)stage.Payload[setting.Item1] ?? setting.Item3 };
                field.RegisterValueChangedCallback(evt => {
                    using var change = _undo.BeginTransaction("编辑关卡规则：" + setting.Item2);
                    _draft.TryGetModule(CharacterTestTacticalStageAuthoringDocument.ModuleId, out var current);
                    current.Payload[setting.Item1] = evt.newValue; _metadataDirty = true; _preview = null;
                    change.Commit(); window.RepaintRequested();
                }); panel.Add(field);
            }
            foreach (var setting in new[] { ("normalScale", "常态时间倍率", defaults.NormalScale), ("dilatedScale", "缓时倍率", defaults.DilatedScale) })
            {
                var field = new FloatField(setting.Item2) { name = "mx-level-rule-" + setting.Item1,
                    value = (float?)stage.Payload[setting.Item1] ?? setting.Item3 };
                field.RegisterValueChangedCallback(evt => {
                    using var change = _undo.BeginTransaction("编辑关卡规则：" + setting.Item2);
                    _draft.TryGetModule(CharacterTestTacticalStageAuthoringDocument.ModuleId, out var current);
                    current.Payload[setting.Item1] = evt.newValue; _metadataDirty = true; _preview = null;
                    change.Commit(); window.RepaintRequested();
                }); panel.Add(field);
            }
            panel.Add(new Label("同帧结果规则：" + ((string)stage.Payload["simultaneousOutcomePolicy"] ?? defaults.SimultaneousOutcomePolicy)));
            panel.Add(new Label("重置规则：" + ((string)stage.Payload["stageResetPolicy"] ?? defaults.StageResetPolicy)));
            panel.Add(new Label("以上使用现有战术规则；保存、检查失败会保留输入。"));
        }
    }
}
