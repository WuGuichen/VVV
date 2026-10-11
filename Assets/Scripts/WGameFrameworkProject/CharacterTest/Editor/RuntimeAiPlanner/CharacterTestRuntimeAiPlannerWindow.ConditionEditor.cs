using System;
using System.Collections.Generic;
using System.IO;
using MxFramework.Diagnostics;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace MxFramework.Game.CharacterTest.Editor
{
    public sealed partial class CharacterTestRuntimeAiPlannerWindow
    {
        private VisualElement CreateConditionEditor(
            CharacterTestRuntimeAiPlannerEditorModel.ConditionDraft condition,
            Action onChanged)
        {
            var root = new VisualElement();
            EnumField fact = new EnumField("Fact", condition.Fact);
            fact.RegisterValueChangedCallback(evt =>
            {
                condition.Fact = (CharacterTestRuntimeAiPlannerFactKind)(object)evt.newValue;
                onChanged();
            });
            root.Add(fact);

            EnumField op = new EnumField("比较", condition.Operator);
            op.RegisterValueChangedCallback(evt =>
            {
                condition.Operator = (CharacterTestRuntimeAiPlannerConditionOperator)(object)evt.newValue;
                onChanged();
            });
            root.Add(op);

            if (UsesFloatValue(condition))
            {
                FloatField value = new FloatField("数值") { value = condition.FloatValue, isDelayed = true };
                value.RegisterValueChangedCallback(evt =>
                {
                    condition.FloatValue = evt.newValue;
                    onChanged();
                });
                root.Add(value);
            }
            else
            {
                Toggle value = new Toggle("布尔值") { value = condition.BoolValue };
                value.RegisterValueChangedCallback(evt =>
                {
                    condition.BoolValue = evt.newValue;
                    onChanged();
                });
                root.Add(value);
            }

            return root;
        }

        private VisualElement CreateSmartConditionEditor(
            CharacterTestRuntimeAiPlannerEditorModel.ConditionDraft condition,
            Action onChanged)
        {
            var root = new VisualElement();
            if (condition == null)
                return root;

            CharacterTestRuntimeAiPlannerEditorFactCatalog.NormalizeCondition(condition);
            CharacterTestRuntimeAiPlannerEditorFactMetadata metadata =
                CharacterTestRuntimeAiPlannerEditorFactCatalog.Get(condition.Fact);

            List<string> factChoices = CharacterTestRuntimeAiPlannerEditorFactCatalog.CreateFactChoices();
            string factChoice = CharacterTestRuntimeAiPlannerEditorFactCatalog.GetFactChoice(condition.Fact);
            int factIndex = factChoices.IndexOf(factChoice);
            if (factIndex < 0)
            {
                factChoices.Add(factChoice);
                factIndex = factChoices.Count - 1;
            }

            var fact = new PopupField<string>("事实", factChoices, factIndex);
            fact.RegisterValueChangedCallback(evt =>
            {
                if (CharacterTestRuntimeAiPlannerEditorFactCatalog.TryGetFactFromChoice(
                        evt.newValue,
                        out CharacterTestRuntimeAiPlannerFactKind selectedFact))
                {
                    condition.Fact = selectedFact;
                    CharacterTestRuntimeAiPlannerEditorFactCatalog.ApplyFactDefault(condition);
                }

                onChanged();
            });
            root.Add(fact);

            List<string> operatorChoices =
                CharacterTestRuntimeAiPlannerEditorFactCatalog.CreateOperatorChoices(metadata);
            string operatorChoice =
                CharacterTestRuntimeAiPlannerEditorFactCatalog.GetOperatorLabel(condition.Operator);
            int operatorIndex = operatorChoices.IndexOf(operatorChoice);
            if (operatorIndex < 0)
            {
                condition.Operator = metadata.Operators.Count > 0
                    ? metadata.Operators[0]
                    : CharacterTestRuntimeAiPlannerConditionOperator.Equals;
                operatorChoice =
                    CharacterTestRuntimeAiPlannerEditorFactCatalog.GetOperatorLabel(condition.Operator);
                operatorIndex = operatorChoices.IndexOf(operatorChoice);
            }

            var op = new PopupField<string>("比较", operatorChoices, Math.Max(0, operatorIndex));
            op.RegisterValueChangedCallback(evt =>
            {
                if (CharacterTestRuntimeAiPlannerEditorFactCatalog.TryGetOperatorFromLabel(
                        evt.newValue,
                        out CharacterTestRuntimeAiPlannerConditionOperator selectedOperator)
                    && CharacterTestRuntimeAiPlannerEditorFactCatalog.IsOperatorAllowed(
                        metadata,
                        selectedOperator))
                {
                    condition.Operator = selectedOperator;
                }

                onChanged();
            });
            root.Add(op);

            switch (metadata.ValueType)
            {
                case CharacterTestRuntimeAiPlannerEditorFactValueType.Bool:
                {
                    Toggle value = new Toggle("布尔值") { value = condition.BoolValue };
                    value.RegisterValueChangedCallback(evt =>
                    {
                        condition.BoolValue = evt.newValue;
                        onChanged();
                    });
                    root.Add(value);
                    break;
                }
                case CharacterTestRuntimeAiPlannerEditorFactValueType.Int:
                {
                    IntegerField value = new IntegerField("整数值")
                    {
                        value = (int)Math.Round(condition.FloatValue),
                        isDelayed = true
                    };
                    value.RegisterValueChangedCallback(evt =>
                    {
                        condition.FloatValue = evt.newValue;
                        onChanged();
                    });
                    root.Add(value);
                    break;
                }
                case CharacterTestRuntimeAiPlannerEditorFactValueType.SurfaceKind:
                {
                    List<string> choices = CharacterTestRuntimeAiPlannerEditorFactCatalog.CreateSurfaceChoices();
                    string current = CharacterTestRuntimeAiPlannerEditorFactCatalog.FormatSurfaceKind(
                        (int)Math.Round(condition.FloatValue));
                    int index = choices.IndexOf(current);
                    if (index < 0)
                    {
                        choices.Add(current);
                        index = choices.Count - 1;
                    }

                    var value = new PopupField<string>("地表", choices, index);
                    value.RegisterValueChangedCallback(evt =>
                    {
                        if (CharacterTestRuntimeAiPlannerEditorFactCatalog.TryGetSurfaceKindFromChoice(
                                evt.newValue,
                                out int surfaceKind))
                        {
                            condition.FloatValue = surfaceKind;
                        }

                        onChanged();
                    });
                    root.Add(value);
                    break;
                }
                default:
                {
                    FloatField value = new FloatField("数值") { value = condition.FloatValue, isDelayed = true };
                    value.RegisterValueChangedCallback(evt =>
                    {
                        condition.FloatValue = evt.newValue;
                        onChanged();
                    });
                    root.Add(value);
                    break;
                }
            }

            root.Add(CreateReadOnlyLine("说明", metadata.Description));
            root.Add(CreateReadOnlyLine("技术 key", metadata.Key));
            root.Add(CreateConditionTemplateRow(condition, onChanged));
            return root;
        }

        private VisualElement CreateConditionTemplateRow(
            CharacterTestRuntimeAiPlannerEditorModel.ConditionDraft condition,
            Action onChanged)
        {
            VisualElement root = CreateButtonRow();
            Label label = new Label("条件模板");
            label.style.marginRight = 6;
            root.Add(label);
            for (int i = 0; i < CommonConditionTemplates.Length; i++)
            {
                CharacterTestRuntimeAiPlannerConditionTemplate template = CommonConditionTemplates[i];
                root.Add(CreateMiniButton(
                    CharacterTestRuntimeAiPlannerEditorFactCatalog.GetTemplateLabel(template),
                    () =>
                    {
                        CharacterTestRuntimeAiPlannerEditorFactCatalog.ApplyTemplate(
                            condition,
                            template,
                            _model.TargetMemoryFrames);
                        onChanged();
                    }));
            }

            return root;
        }

        private VisualElement CreateStatePopup(
            string label,
            string currentStateId,
            bool includeEmpty,
            Action<string> onChanged)
        {
            List<string> choices = CreateStateChoices(includeEmpty);
            string current = string.IsNullOrWhiteSpace(currentStateId) ? EmptyStateChoice : currentStateId;
            int index = choices.IndexOf(current);
            if (index < 0)
            {
                choices.Add(current);
                index = choices.Count - 1;
            }

            var popup = new PopupField<string>(label, choices, index);
            popup.RegisterValueChangedCallback(evt =>
            {
                string value = string.Equals(evt.newValue, EmptyStateChoice, StringComparison.Ordinal)
                    ? string.Empty
                    : evt.newValue;
                onChanged(value);
            });
            return popup;
        }

        private List<string> CreateStateChoices(bool includeEmpty)
        {
            var choices = new List<string>();
            if (includeEmpty || _model.States.Count == 0)
                choices.Add(EmptyStateChoice);
            for (int i = 0; i < _model.States.Count; i++)
                choices.Add(_model.States[i].StateId);
            return choices;
        }

    }
}
