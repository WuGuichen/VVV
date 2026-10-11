using System;
using System.Collections.Generic;
using MxFramework.Core.Math;
using MxFramework.Gameplay.Triggers;
using MxFramework.Gameplay.Triggers.Config;
using UnityEditor.UIElements;
using UnityEngine.UIElements;

namespace MxFramework.Game.CharacterTest.Editor
{
    public sealed partial class CharacterTestWorldObjectLevelEditorModule
    {
        private const string TriggerConditionAll =
            "全部满足（且）";
        private const string TriggerConditionAny =
            "任一满足（或）";
        private const string TriggerConditionHasSubject =
            "触发对象存在";
        private const string TriggerConditionPreviousHealth =
            "触发前生命值";
        private const string TriggerConditionCurrentHealth =
            "当前生命值";
        private const int TriggerConditionClauseLimit = 8;

        private VisualElement CreateTriggerBooleanConditionEditor(
            CharacterTestLevelGameplayTriggerEntry entry)
        {
            var root = new VisualElement
            {
                name = "character-test-trigger-boolean-conditions"
            };
            root.AddToClassList(
                "mx-level-editor__trigger-condition-builder");

            if (!_triggerDocument.TryGetConditionRoot(
                    entry,
                    out GameplayTriggerConditionNodeAuthoring
                        conditionRoot))
            {
                root.Add(new HelpBox(
                    "找不到该触发器引用的附加条件。",
                    HelpBoxMessageType.Error));
                return root;
            }

            bool thresholdSource =
                _triggerDocument.TryGetSourceKind(
                    entry,
                    out GameplayTriggerSourceKind sourceKind)
                && sourceKind == GameplayTriggerSourceKind.Threshold;
            var clauses = new List<TriggerConditionClauseDraft>();
            if (!TryFlattenTriggerCondition(
                    conditionRoot,
                    thresholdSource,
                    out GameplayTriggerConditionOperation groupOperation,
                    clauses))
            {
                var compatibility = new HelpBox(
                    "该规则使用混合嵌套或尚未开放的条件，"
                    + "当前版本会原样保留："
                    + ResolveTriggerWorkspaceConditionSummary(entry),
                    HelpBoxMessageType.Warning)
                {
                    name =
                        "character-test-trigger-condition-compatibility"
                };
                root.Add(compatibility);
                return root;
            }

            VisualElement heading = new VisualElement();
            heading.AddToClassList(
                "mx-level-editor__trigger-condition-heading");
            var title = new Label("附加条件");
            title.AddToClassList(
                "mx-level-editor__trigger-condition-title");
            heading.Add(title);
            if (clauses.Count >= 2)
            {
                var choices = new List<string>
                {
                    TriggerConditionAll,
                    TriggerConditionAny
                };
                var logic = new PopupField<string>(
                    choices,
                    groupOperation
                        == GameplayTriggerConditionOperation.Any
                            ? 1
                            : 0)
                {
                    name = "character-test-trigger-condition-logic",
                    tooltip =
                        "全部满足表示条件之间使用“且”；"
                        + "任一满足表示条件之间使用“或”。"
                };
                logic.AddToClassList(
                    "mx-level-editor__trigger-condition-logic");
                logic.RegisterValueChangedCallback(
                    change =>
                    {
                        GameplayTriggerConditionOperation operation =
                            string.Equals(
                                change.newValue,
                                TriggerConditionAny,
                                StringComparison.Ordinal)
                                ? GameplayTriggerConditionOperation.Any
                                : GameplayTriggerConditionOperation.All;
                        ApplyTriggerConditionDraft(
                            entry,
                            operation,
                            clauses);
                    });
                heading.Add(logic);
            }
            root.Add(heading);

            if (clauses.Count == 0)
            {
                var empty = new Label(
                    "无附加条件。事件与目标筛选仍然生效。");
                empty.AddToClassList(
                    "mx-level-editor__trigger-condition-empty");
                root.Add(empty);
            }
            else
            {
                for (int i = 0; i < clauses.Count; i++)
                {
                    root.Add(CreateTriggerConditionClauseRow(
                        entry,
                        groupOperation,
                        clauses,
                        i,
                        thresholdSource));
                }
            }

            root.Add(CreateTriggerConditionAddMenu(
                entry,
                groupOperation,
                clauses));
            return root;
        }

        private VisualElement CreateTriggerConditionClauseRow(
            CharacterTestLevelGameplayTriggerEntry entry,
            GameplayTriggerConditionOperation groupOperation,
            List<TriggerConditionClauseDraft> clauses,
            int index,
            bool thresholdSource)
        {
            TriggerConditionClauseDraft clause = clauses[index];
            var row = new VisualElement
            {
                name =
                    "character-test-trigger-condition-clause-"
                    + index
            };
            row.AddToClassList(
                "mx-level-editor__trigger-condition-clause");

            var negate = new ToolbarToggle
            {
                name =
                    "character-test-trigger-condition-negate-"
                    + index,
                text = "非",
                value = clause.Negated,
                tooltip = "取反这一条条件。"
            };
            negate.AddToClassList(
                "mx-level-editor__trigger-condition-negate");
            negate.RegisterValueChangedCallback(
                change =>
                {
                    clause.Negated = change.newValue;
                    ApplyTriggerConditionDraft(
                        entry,
                        groupOperation,
                        clauses);
                });
            row.Add(negate);

            var operandChoices = new List<string>
            {
                TriggerConditionHasSubject
            };
            if (thresholdSource)
            {
                operandChoices.Add(
                    TriggerConditionPreviousHealth);
                operandChoices.Add(
                    TriggerConditionCurrentHealth);
            }

            var operand = new PopupField<string>(
                operandChoices,
                ResolveTriggerConditionOperandIndex(
                    clause.Operation,
                    thresholdSource))
            {
                name =
                    "character-test-trigger-condition-operand-"
                    + index
            };
            operand.AddToClassList(
                "mx-level-editor__trigger-condition-operand");
            operand.RegisterValueChangedCallback(
                change =>
                {
                    GameplayTriggerConditionOperation operation =
                        ResolveTriggerConditionOperation(
                            change.newValue);
                    if (operation != clause.Operation)
                    {
                        bool wasScalar =
                            IsTriggerScalarCondition(
                                clause.Operation);
                        clause.Operation = operation;
                        clause.Comparison =
                            GameplayTriggerComparison.Equal;
                        clause.Operand = operation
                            == GameplayTriggerConditionOperation
                                .HasSubject
                                ? 1L
                                : wasScalar
                                    ? clause.Operand
                                    : 0L;
                    }
                    ApplyTriggerConditionDraft(
                        entry,
                        groupOperation,
                        clauses);
                });
            row.Add(operand);

            if (IsTriggerScalarCondition(clause.Operation))
            {
                var comparisonChoices = new List<string>
                {
                    "=",
                    "≠",
                    "<",
                    "≤",
                    ">",
                    "≥"
                };
                var comparison = new PopupField<string>(
                    comparisonChoices,
                    (int)clause.Comparison)
                {
                    name =
                        "character-test-trigger-condition-comparison-"
                        + index
                };
                comparison.AddToClassList(
                    "mx-level-editor__trigger-condition-comparison");
                comparison.RegisterValueChangedCallback(
                    change =>
                    {
                        clause.Comparison =
                            ResolveTriggerConditionComparison(
                                change.newValue);
                        ApplyTriggerConditionDraft(
                            entry,
                            groupOperation,
                            clauses);
                    });
                row.Add(comparison);

                double displayedValue =
                    clause.Operand / (double)Fix64.Scale;
                var value = new DoubleField
                {
                    name =
                        "character-test-trigger-condition-value-"
                        + index,
                    value = displayedValue,
                    isDelayed = true,
                    tooltip =
                        "使用生命值单位编辑；保存时写入现有确定性 Fix64。"
                };
                value.AddToClassList(
                    "mx-level-editor__trigger-condition-value");
                value.RegisterValueChangedCallback(
                    change =>
                    {
                        if (!TryConvertTriggerConditionValue(
                                change.newValue,
                                out long rawValue))
                        {
                            value.SetValueWithoutNotify(
                                displayedValue);
                            return;
                        }

                        clause.Operand = rawValue;
                        ApplyTriggerConditionDraft(
                            entry,
                            groupOperation,
                            clauses);
                    });
                row.Add(value);
            }

            var remove = new Button(
                () =>
                {
                    clauses.RemoveAt(index);
                    ApplyTriggerConditionDraft(
                        entry,
                        groupOperation,
                        clauses);
                })
            {
                name =
                    "character-test-trigger-condition-remove-"
                    + index,
                text = "×",
                tooltip = "删除这一条附加条件。"
            };
            remove.AddToClassList(
                "mx-level-editor__trigger-condition-remove");
            row.Add(remove);
            return row;
        }

        private ToolbarMenu CreateTriggerConditionAddMenu(
            CharacterTestLevelGameplayTriggerEntry entry,
            GameplayTriggerConditionOperation groupOperation,
            List<TriggerConditionClauseDraft> clauses)
        {
            var add = new ToolbarMenu
            {
                name = "character-test-trigger-condition-add",
                text = "＋ 添加条件"
            };
            add.AddToClassList(
                "mx-level-editor__trigger-condition-add");
            add.SetEnabled(
                clauses.Count < TriggerConditionClauseLimit);
            add.menu.AppendAction(
                TriggerConditionHasSubject,
                _ => AddTriggerConditionClause(
                    entry,
                    groupOperation,
                    clauses,
                    GameplayTriggerConditionOperation.HasSubject));

            if (_triggerDocument.TryGetSourceKind(
                    entry,
                    out GameplayTriggerSourceKind sourceKind)
                && sourceKind == GameplayTriggerSourceKind.Threshold)
            {
                add.menu.AppendAction(
                    TriggerConditionPreviousHealth,
                    _ => AddTriggerConditionClause(
                        entry,
                        groupOperation,
                        clauses,
                        GameplayTriggerConditionOperation
                            .PreviousScalar));
                add.menu.AppendAction(
                    TriggerConditionCurrentHealth,
                    _ => AddTriggerConditionClause(
                        entry,
                        groupOperation,
                        clauses,
                        GameplayTriggerConditionOperation
                            .CurrentScalar));
            }

            return add;
        }

        private void AddTriggerConditionClause(
            CharacterTestLevelGameplayTriggerEntry entry,
            GameplayTriggerConditionOperation groupOperation,
            List<TriggerConditionClauseDraft> clauses,
            GameplayTriggerConditionOperation operation)
        {
            if (clauses == null
                || clauses.Count >= TriggerConditionClauseLimit)
            {
                return;
            }

            clauses.Add(
                new TriggerConditionClauseDraft(
                    operation,
                    operation
                        == GameplayTriggerConditionOperation.HasSubject
                            ? 1L
                            : 0L,
                    GameplayTriggerComparison.Equal,
                    negated: false));
            ApplyTriggerConditionDraft(
                entry,
                groupOperation,
                clauses);
        }

        private void ApplyTriggerConditionDraft(
            CharacterTestLevelGameplayTriggerEntry entry,
            GameplayTriggerConditionOperation groupOperation,
            List<TriggerConditionClauseDraft> clauses)
        {
            GameplayTriggerConditionNodeAuthoring root =
                BuildTriggerConditionRoot(
                    groupOperation,
                    clauses);
            if (UpdateTriggerConditionRoot(
                    entry.StableId,
                    root))
            {
                RefreshTriggerInspectorAfterMutation();
            }
        }

        private static GameplayTriggerConditionNodeAuthoring
            BuildTriggerConditionRoot(
                GameplayTriggerConditionOperation groupOperation,
                IReadOnlyList<TriggerConditionClauseDraft> clauses)
        {
            if (clauses == null || clauses.Count == 0)
            {
                return new GameplayTriggerConditionNodeAuthoring(
                    GameplayTriggerConditionOperation.PushTrue);
            }

            GameplayTriggerConditionOperation normalizedGroup =
                groupOperation
                    == GameplayTriggerConditionOperation.Any
                    ? GameplayTriggerConditionOperation.Any
                    : GameplayTriggerConditionOperation.All;
            GameplayTriggerConditionNodeAuthoring root =
                BuildTriggerConditionLeaf(clauses[0]);
            for (int i = 1; i < clauses.Count; i++)
            {
                root = new GameplayTriggerConditionNodeAuthoring(
                    normalizedGroup,
                    children: new[]
                    {
                        root,
                        BuildTriggerConditionLeaf(clauses[i])
                    });
            }
            return root;
        }

        private static GameplayTriggerConditionNodeAuthoring
            BuildTriggerConditionLeaf(
                TriggerConditionClauseDraft clause)
        {
            var leaf = new GameplayTriggerConditionNodeAuthoring(
                clause.Operation,
                clause.Operand,
                clause.Comparison);
            if (!clause.Negated)
                return leaf;

            return new GameplayTriggerConditionNodeAuthoring(
                GameplayTriggerConditionOperation.Not,
                children: new[] { leaf });
        }

        private static bool TryFlattenTriggerCondition(
            GameplayTriggerConditionNodeAuthoring root,
            bool allowScalar,
            out GameplayTriggerConditionOperation groupOperation,
            List<TriggerConditionClauseDraft> clauses)
        {
            groupOperation = GameplayTriggerConditionOperation.All;
            if (root == null || clauses == null)
                return false;
            if (root.Operation
                == GameplayTriggerConditionOperation.PushTrue)
            {
                return root.Children == null
                    || root.Children.Length == 0;
            }

            if (root.Operation
                    == GameplayTriggerConditionOperation.All
                || root.Operation
                    == GameplayTriggerConditionOperation.Any)
            {
                groupOperation = root.Operation;
                return TryAppendTriggerConditionGroup(
                    root,
                    groupOperation,
                    allowScalar,
                    clauses);
            }

            return TryReadTriggerConditionClause(
                root,
                allowScalar,
                clauses);
        }

        private static bool TryAppendTriggerConditionGroup(
            GameplayTriggerConditionNodeAuthoring node,
            GameplayTriggerConditionOperation groupOperation,
            bool allowScalar,
            List<TriggerConditionClauseDraft> clauses)
        {
            if (node?.Operation == groupOperation)
            {
                if (node.Children == null
                    || node.Children.Length != 2)
                {
                    return false;
                }

                return TryAppendTriggerConditionGroup(
                        node.Children[0],
                        groupOperation,
                        allowScalar,
                        clauses)
                    && TryAppendTriggerConditionGroup(
                        node.Children[1],
                        groupOperation,
                        allowScalar,
                        clauses);
            }

            return TryReadTriggerConditionClause(
                node,
                allowScalar,
                clauses);
        }

        private static bool TryReadTriggerConditionClause(
            GameplayTriggerConditionNodeAuthoring node,
            bool allowScalar,
            List<TriggerConditionClauseDraft> clauses)
        {
            if (node == null)
                return false;

            bool negated = false;
            if (node.Operation == GameplayTriggerConditionOperation.Not)
            {
                if (node.Children == null
                    || node.Children.Length != 1)
                {
                    return false;
                }
                negated = true;
                node = node.Children[0];
            }

            if (node == null
                || (node.Children != null
                    && node.Children.Length != 0))
            {
                return false;
            }

            switch (node.Operation)
            {
                case GameplayTriggerConditionOperation.HasSubject:
                    if (node.Operand == 0L)
                        negated = !negated;
                    clauses.Add(
                        new TriggerConditionClauseDraft(
                            node.Operation,
                            1L,
                            GameplayTriggerComparison.Equal,
                            negated));
                    return true;
                case GameplayTriggerConditionOperation.PreviousScalar:
                case GameplayTriggerConditionOperation.CurrentScalar:
                    if (!allowScalar)
                        return false;
                    clauses.Add(
                        new TriggerConditionClauseDraft(
                            node.Operation,
                            node.Operand,
                            node.Comparison,
                            negated));
                    return true;
                default:
                    return false;
            }
        }

        private static int ResolveTriggerConditionOperandIndex(
            GameplayTriggerConditionOperation operation,
            bool thresholdSource)
        {
            if (!thresholdSource)
                return 0;
            switch (operation)
            {
                case GameplayTriggerConditionOperation.PreviousScalar:
                    return 1;
                case GameplayTriggerConditionOperation.CurrentScalar:
                    return 2;
                default:
                    return 0;
            }
        }

        private static GameplayTriggerConditionOperation
            ResolveTriggerConditionOperation(string value)
        {
            if (string.Equals(
                    value,
                    TriggerConditionPreviousHealth,
                    StringComparison.Ordinal))
            {
                return GameplayTriggerConditionOperation.PreviousScalar;
            }
            if (string.Equals(
                    value,
                    TriggerConditionCurrentHealth,
                    StringComparison.Ordinal))
            {
                return GameplayTriggerConditionOperation.CurrentScalar;
            }
            return GameplayTriggerConditionOperation.HasSubject;
        }

        private static GameplayTriggerComparison
            ResolveTriggerConditionComparison(string value)
        {
            switch (value)
            {
                case "≠":
                    return GameplayTriggerComparison.NotEqual;
                case "<":
                    return GameplayTriggerComparison.Less;
                case "≤":
                    return GameplayTriggerComparison.LessOrEqual;
                case ">":
                    return GameplayTriggerComparison.Greater;
                case "≥":
                    return GameplayTriggerComparison.GreaterOrEqual;
                default:
                    return GameplayTriggerComparison.Equal;
            }
        }

        private static bool IsTriggerScalarCondition(
            GameplayTriggerConditionOperation operation)
        {
            return operation
                    == GameplayTriggerConditionOperation.PreviousScalar
                || operation
                    == GameplayTriggerConditionOperation.CurrentScalar;
        }

        private static bool TryConvertTriggerConditionValue(
            double value,
            out long rawValue)
        {
            rawValue = 0L;
            if (double.IsNaN(value) || double.IsInfinity(value))
                return false;

            double scaled = value * Fix64.Scale;
            if (scaled < long.MinValue || scaled > long.MaxValue)
                return false;

            rawValue = checked((long)Math.Round(
                scaled,
                MidpointRounding.AwayFromZero));
            return true;
        }

        private sealed class TriggerConditionClauseDraft
        {
            internal TriggerConditionClauseDraft(
                GameplayTriggerConditionOperation operation,
                long operand,
                GameplayTriggerComparison comparison,
                bool negated)
            {
                Operation = operation;
                Operand = operand;
                Comparison = comparison;
                Negated = negated;
            }

            internal GameplayTriggerConditionOperation Operation
            {
                get;
                set;
            }

            internal long Operand { get; set; }

            internal GameplayTriggerComparison Comparison
            {
                get;
                set;
            }

            internal bool Negated { get; set; }
        }
    }
}
