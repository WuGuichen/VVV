using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using MxFramework.CharacterAction;
using UnityEngine;
using UnityEngine.UIElements;

namespace MxFramework.Editor.CharacterAction
{
    internal enum CharacterActionBindingTableSort
    {
        SourceOrder = 0,
        TargetAction = 1,
        Status = 2,
        ProblemCount = 3,
    }

    internal sealed class CharacterActionBindingTableRow
    {
        internal int OriginalIndex;
        internal string Source = string.Empty;
        internal string TargetAction = string.Empty;
        internal string Condition = string.Empty;
        internal string Policy = string.Empty;
        internal string Status = string.Empty;
        internal int ProblemCount;
    }

    internal static class CharacterActionBindingTableProjection
    {
        internal static List<CharacterActionBindingTableRow> BuildCommandRows(
            CharacterActionBindingJsonDto[] bindings,
            string source,
            ISet<int> knownActionIds,
            string query,
            CharacterActionBindingTableSort sort,
            Func<int, int> getProblemCount = null)
        {
            bindings = bindings ?? Array.Empty<CharacterActionBindingJsonDto>();
            var rows = new List<CharacterActionBindingTableRow>(bindings.Length);
            for (int i = 0; i < bindings.Length; i++)
            {
                CharacterActionBindingJsonDto binding = bindings[i] ?? new CharacterActionBindingJsonDto();
                bool unknownAction = !Contains(knownActionIds, binding.ActionId);
                int problemCount = Math.Max(0, getProblemCount?.Invoke(i) ?? 0);
                if (unknownAction)
                    problemCount = Math.Max(1, problemCount);
                var row = new CharacterActionBindingTableRow
                {
                    OriginalIndex = i,
                    Source = source ?? string.Empty,
                    TargetAction = binding.ActionId > 0 ? "#" + binding.ActionId : "-",
                    Condition = "Intent " + CharacterActionAuthoringIds.Label(binding.IntentId, 404),
                    Policy = "P" + binding.Priority.ToString(CultureInfo.InvariantCulture)
                        + (binding.AllowQueue
                            ? " · 可排队 " + binding.QueueWindowFrames.ToString(CultureInfo.InvariantCulture) + " 帧"
                            : " · 不排队"),
                    Status = unknownAction ? "未知动作引用" : problemCount > 0 ? "有问题" : "有效",
                    ProblemCount = problemCount,
                };
                if (MatchesQuery(row, query))
                    rows.Add(row);
            }

            SortRows(rows, sort);
            return rows;
        }

        internal static List<CharacterActionBindingTableRow> BuildAbilityRows(
            CharacterAbilityActionBindingJsonDto[] bindings,
            string source,
            ISet<int> knownActionIds,
            ISet<int> knownAbilityIds,
            string query,
            CharacterActionBindingTableSort sort,
            Func<int, int> getProblemCount = null)
        {
            bindings = bindings ?? Array.Empty<CharacterAbilityActionBindingJsonDto>();
            var rows = new List<CharacterActionBindingTableRow>(bindings.Length);
            for (int i = 0; i < bindings.Length; i++)
            {
                CharacterAbilityActionBindingJsonDto binding =
                    bindings[i] ?? new CharacterAbilityActionBindingJsonDto();
                bool unknownAction = !Contains(knownActionIds, binding.ActionId);
                bool unknownAbility = knownAbilityIds != null
                    && knownAbilityIds.Count > 0
                    && !knownAbilityIds.Contains(binding.AbilityId);
                int problemCount = Math.Max(0, getProblemCount?.Invoke(i) ?? 0);
                if (unknownAction || unknownAbility)
                    problemCount = Math.Max(1, problemCount);
                string status = unknownAction
                    ? "未知动作引用"
                    : unknownAbility
                        ? "未知能力"
                        : problemCount > 0 ? "有问题" : "有效";
                var row = new CharacterActionBindingTableRow
                {
                    OriginalIndex = i,
                    Source = source ?? string.Empty,
                    TargetAction = binding.ActionId > 0 ? "#" + binding.ActionId : "-",
                    Condition = BuildAbilityCondition(binding),
                    Policy = "直接映射",
                    Status = status,
                    ProblemCount = problemCount,
                };
                if (MatchesQuery(row, query))
                    rows.Add(row);
            }

            SortRows(rows, sort);
            return rows;
        }

        internal static bool CanReorder(string query, CharacterActionBindingTableSort sort)
        {
            return string.IsNullOrWhiteSpace(query)
                && sort == CharacterActionBindingTableSort.SourceOrder;
        }

        internal static bool ContainsOriginalIndex(
            IReadOnlyList<CharacterActionBindingTableRow> rows,
            int originalIndex)
        {
            for (int i = 0; i < (rows?.Count ?? 0); i++)
            {
                if (rows[i]?.OriginalIndex == originalIndex)
                    return true;
            }

            return false;
        }

        internal static bool TryMove<T>(IReadOnlyList<T> source, int fromIndex, int delta, out T[] result)
        {
            int count = source?.Count ?? 0;
            int toIndex = fromIndex + delta;
            if (fromIndex < 0 || fromIndex >= count || toIndex < 0 || toIndex >= count)
            {
                result = null;
                return false;
            }

            result = new T[count];
            for (int i = 0; i < count; i++)
                result[i] = source[i];
            T value = result[fromIndex];
            result[fromIndex] = result[toIndex];
            result[toIndex] = value;
            return true;
        }

        private static string BuildAbilityCondition(CharacterAbilityActionBindingJsonDto binding)
        {
            string required = string.Join(", ", binding.RequiredTags ?? Array.Empty<string>());
            string forbidden = string.Join(", ", binding.ForbiddenTags ?? Array.Empty<string>());
            string result = "Ability " + binding.AbilityId.ToString(CultureInfo.InvariantCulture);
            if (!string.IsNullOrWhiteSpace(required))
                result += " · 需要 " + required;
            if (!string.IsNullOrWhiteSpace(forbidden))
                result += " · 禁止 " + forbidden;
            return result;
        }

        private static bool Contains(ISet<int> values, int value)
        {
            return values != null
                && value > 0
                && values.Contains(value);
        }

        private static bool MatchesQuery(CharacterActionBindingTableRow row, string query)
        {
            if (string.IsNullOrWhiteSpace(query))
                return true;
            query = query.Trim();
            return ContainsIgnoreCase(row.Source, query)
                || ContainsIgnoreCase(row.TargetAction, query)
                || ContainsIgnoreCase(row.Condition, query)
                || ContainsIgnoreCase(row.Policy, query)
                || ContainsIgnoreCase(row.Status, query)
                || row.OriginalIndex.ToString(CultureInfo.InvariantCulture).IndexOf(
                    query,
                    StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static bool ContainsIgnoreCase(string value, string query)
        {
            return (value ?? string.Empty).IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static void SortRows(
            List<CharacterActionBindingTableRow> rows,
            CharacterActionBindingTableSort sort)
        {
            if (sort == CharacterActionBindingTableSort.SourceOrder)
                return;
            rows.Sort((left, right) =>
            {
                int compare;
                switch (sort)
                {
                    case CharacterActionBindingTableSort.TargetAction:
                        compare = string.Compare(left.TargetAction, right.TargetAction, StringComparison.Ordinal);
                        break;
                    case CharacterActionBindingTableSort.Status:
                        compare = string.Compare(left.Status, right.Status, StringComparison.Ordinal);
                        break;
                    case CharacterActionBindingTableSort.ProblemCount:
                        compare = right.ProblemCount.CompareTo(left.ProblemCount);
                        break;
                    default:
                        compare = 0;
                        break;
                }
                return compare != 0 ? compare : left.OriginalIndex.CompareTo(right.OriginalIndex);
            });
        }
    }

    internal readonly struct CharacterActionBindingProblemNavigationPlan
    {
        internal CharacterActionBindingProblemNavigationPlan(
            string actionSetStableId,
            CharacterActionAuthoringProblemTargetKind targetKind,
            int originalIndex,
            string inspectorCardElementName,
            string fieldElementName)
        {
            ActionSetStableId = actionSetStableId ?? string.Empty;
            TargetKind = targetKind;
            OriginalIndex = originalIndex;
            InspectorCardElementName = inspectorCardElementName ?? string.Empty;
            FieldElementName = fieldElementName ?? string.Empty;
        }

        internal string ActionSetStableId { get; }
        internal CharacterActionAuthoringProblemTargetKind TargetKind { get; }
        internal int OriginalIndex { get; }
        internal string InspectorCardElementName { get; }
        internal string FieldElementName { get; }
    }

    internal static class CharacterActionBindingProblemNavigation
    {
        private const string ElementPrefix = "character-action";

        internal static bool TryCreate(
            CharacterActionAuthoringProblemLocation location,
            CharacterActionAuthoringDocumentSession session,
            CharacterActionSetJsonDto fallbackActionSet,
            out CharacterActionBindingProblemNavigationPlan plan)
        {
            plan = default;
            if (location.TargetKind != CharacterActionAuthoringProblemTargetKind.CommandBinding
                && location.TargetKind != CharacterActionAuthoringProblemTargetKind.AbilityBinding)
            {
                return false;
            }

            if (!TryResolveActionSetStableId(
                    location.ObjectKey,
                    location.SourcePath,
                    session,
                    fallbackActionSet,
                    out string actionSetStableId,
                    out CharacterActionSetJsonDto actionSet))
            {
                return false;
            }

            int count = location.TargetKind == CharacterActionAuthoringProblemTargetKind.CommandBinding
                ? actionSet.CommandBindings?.Length ?? 0
                : actionSet.AbilityBindings?.Length ?? 0;
            if (location.ItemIndex < 0 || location.ItemIndex >= count)
                return false;

            string cardName = InspectorCardElementName(location.TargetKind, location.ItemIndex);
            plan = new CharacterActionBindingProblemNavigationPlan(
                actionSetStableId,
                location.TargetKind,
                location.ItemIndex,
                cardName,
                FieldElementName(location.TargetKind, location.ItemIndex, location.FieldPath));
            return true;
        }

        internal static bool TryResolveActionSetStableId(
            string objectKey,
            string sourcePath,
            CharacterActionAuthoringDocumentSession session,
            CharacterActionSetJsonDto fallbackActionSet,
            out string actionSetStableId,
            out CharacterActionSetJsonDto actionSet)
        {
            actionSetStableId = string.Empty;
            actionSet = null;
            if (string.IsNullOrWhiteSpace(objectKey))
                return false;

            if (int.TryParse(
                    objectKey,
                    NumberStyles.None,
                    CultureInfo.InvariantCulture,
                    out int documentIndex))
            {
                if (session == null || documentIndex < 0)
                    return false;

                CharacterActionAuthoringActionSetBundleDocument bundle =
                    FindBundleBySourcePath(session.ActionSetBundles, sourcePath);
                CharacterActionSetJsonDto[] actionSets =
                    bundle?.ActionSetsDocument.ActionSets ?? Array.Empty<CharacterActionSetJsonDto>();
                if (documentIndex >= actionSets.Length || actionSets[documentIndex] == null)
                    return false;

                actionSet = actionSets[documentIndex];
                actionSetStableId = actionSet.StableId ?? string.Empty;
                return !string.IsNullOrWhiteSpace(actionSetStableId);
            }

            if (session != null)
            {
                CharacterActionAuthoringActionSetBundleDocument bundle = session.FindActionSetBundle(objectKey);
                CharacterActionSetJsonDto[] actionSets =
                    bundle?.ActionSetsDocument.ActionSets ?? Array.Empty<CharacterActionSetJsonDto>();
                for (int i = 0; i < actionSets.Length; i++)
                {
                    if (!string.Equals(actionSets[i]?.StableId, objectKey, StringComparison.Ordinal))
                        continue;
                    actionSet = actionSets[i];
                    actionSetStableId = objectKey;
                    return true;
                }
                return false;
            }

            if (!string.Equals(fallbackActionSet?.StableId, objectKey, StringComparison.Ordinal))
                return false;
            actionSet = fallbackActionSet;
            actionSetStableId = objectKey;
            return true;
        }

        internal static string InspectorCardElementName(
            CharacterActionAuthoringProblemTargetKind targetKind,
            int originalIndex)
        {
            return ElementPrefix
                + "-"
                + TargetSegment(targetKind)
                + "-binding-"
                + originalIndex.ToString(CultureInfo.InvariantCulture);
        }

        internal static string FieldElementName(
            CharacterActionAuthoringProblemTargetKind targetKind,
            int originalIndex,
            string fieldPath)
        {
            string cardName = InspectorCardElementName(targetKind, originalIndex);
            string field = TopLevelField(fieldPath);
            return string.IsNullOrWhiteSpace(field) ? cardName : cardName + "-" + field;
        }

        private static CharacterActionAuthoringActionSetBundleDocument FindBundleBySourcePath(
            IReadOnlyList<CharacterActionAuthoringActionSetBundleDocument> bundles,
            string sourcePath)
        {
            if (string.IsNullOrWhiteSpace(sourcePath))
                return null;
            CharacterActionAuthoringActionSetBundleDocument match = null;
            for (int i = 0; i < (bundles?.Count ?? 0); i++)
            {
                CharacterActionAuthoringActionSetBundleDocument candidate = bundles[i];
                if (!PathsEqual(candidate?.Source.ActionSetsPath, sourcePath))
                    continue;
                if (match != null)
                    return null;
                match = candidate;
            }
            return match;
        }

        private static bool PathsEqual(string left, string right)
        {
            string normalizedLeft = NormalizePath(left);
            string normalizedRight = NormalizePath(right);
            if (string.Equals(normalizedLeft, normalizedRight, StringComparison.OrdinalIgnoreCase))
                return true;
            if (string.IsNullOrWhiteSpace(normalizedLeft) || string.IsNullOrWhiteSpace(normalizedRight))
                return false;
            try
            {
                return string.Equals(
                    NormalizePath(Path.GetFullPath(normalizedLeft)),
                    NormalizePath(Path.GetFullPath(normalizedRight)),
                    StringComparison.OrdinalIgnoreCase);
            }
            catch (Exception)
            {
                return false;
            }
        }

        private static string NormalizePath(string path)
        {
            return (path ?? string.Empty).Trim().Replace('\\', '/').TrimEnd('/');
        }

        private static string TopLevelField(string fieldPath)
        {
            if (string.IsNullOrWhiteSpace(fieldPath))
                return string.Empty;
            string field = fieldPath.Trim();
            int separator = field.IndexOfAny(new[] { '.', '[' });
            if (separator >= 0)
                field = field.Substring(0, separator);
            if (field.Length == 0)
                return string.Empty;

            var result = new char[field.Length];
            int count = 0;
            for (int i = 0; i < field.Length; i++)
            {
                char character = field[i];
                if (char.IsLetterOrDigit(character) || character == '-' || character == '_')
                    result[count++] = character;
            }
            return count == 0 ? string.Empty : new string(result, 0, count);
        }

        private static string TargetSegment(CharacterActionAuthoringProblemTargetKind targetKind)
        {
            return targetKind == CharacterActionAuthoringProblemTargetKind.AbilityBinding
                ? "ability"
                : "command";
        }
    }

    public sealed partial class CharacterActionAuthoringWindow
    {
        private static readonly string[] BindingSortLabels =
        {
            "原始顺序",
            "目标动作",
            "状态",
            "问题数",
        };

        private string _commandBindingTableQuery = string.Empty;
        private string _abilityBindingTableQuery = string.Empty;
        private CharacterActionBindingTableSort _commandBindingTableSort =
            CharacterActionBindingTableSort.SourceOrder;
        private CharacterActionBindingTableSort _abilityBindingTableSort =
            CharacterActionBindingTableSort.SourceOrder;
        private int _selectedCommandBindingOriginalIndex = -1;
        private int _selectedAbilityBindingOriginalIndex = -1;

        private void BuildCommandBindingTableWorkbench(VisualElement main, VisualElement inspector)
        {
            BuildActionSetSummary(main, includeBindings: true);
            CharacterActionSetJsonDto set = _model?.ActionSet;
            if (set == null)
            {
                inspector.Add(new HelpBox("未选择 ActionSet。", HelpBoxMessageType.Info));
                return;
            }

            HashSet<int> knownActions = BuildBindingKnownActionIds();
            List<CharacterActionBindingTableRow> rows =
                CharacterActionBindingTableProjection.BuildCommandRows(
                    set.CommandBindings,
                    set.StableId,
                    knownActions,
                    _commandBindingTableQuery,
                    _commandBindingTableSort,
                    index => CountBindingProblems(
                        CharacterActionAuthoringProblemTargetKind.CommandBinding,
                        set.StableId,
                        index));
            VisualElement card = AddCard(main, "指令绑定");
            AddBindingTableToolbar(
                card,
                rows.Count,
                set.CommandBindings?.Length ?? 0,
                _commandBindingTableQuery,
                value =>
                {
                    _commandBindingTableQuery = value;
                    SaveSessionState();
                    RebuildMappingWorkspaceContent();
                },
                _commandBindingTableSort,
                value =>
                {
                    _commandBindingTableSort = value;
                    SaveSessionState();
                    RebuildMappingWorkspaceContent();
                });

            MultiColumnListView table = BuildBindingTable(rows, row =>
            {
                _selectedCommandBindingOriginalIndex = row.OriginalIndex;
                BuildCommandBindingTableInspector(inspector, row.OriginalIndex);
            });
            card.Add(table);
            AddCommandBindingTableActions(card, rows, table);
            SelectBindingTableRow(
                table,
                rows,
                ref _selectedCommandBindingOriginalIndex,
                inspector,
                BuildCommandBindingTableInspector);
        }

        private void BuildAbilityBindingTableWorkbench(VisualElement main, VisualElement inspector)
        {
            BuildActionSetSummary(main, includeBindings: true);
            CharacterActionSetJsonDto set = _model?.ActionSet;
            if (set == null)
            {
                inspector.Add(new HelpBox("未选择 ActionSet。", HelpBoxMessageType.Info));
                return;
            }

            int focusedIndex = -1;
            if (_focusedAbilityId > 0)
            {
                focusedIndex = FindAbilityBindingIndex(
                    set.AbilityBindings ?? Array.Empty<CharacterAbilityActionBindingJsonDto>(),
                    _focusedAbilityId);
                if (focusedIndex >= 0)
                    _selectedAbilityBindingOriginalIndex = focusedIndex;
            }

            HashSet<int> knownActions = BuildBindingKnownActionIds();
            int[] knownAbilityArray = CharacterActionPreviewBindingDiscovery.DiscoverKnownAbilityIds(
                set.CharacterStableId,
                set.EquipmentStateStableId);
            var knownAbilities = new HashSet<int>(knownAbilityArray);
            List<CharacterActionBindingTableRow> rows =
                CharacterActionBindingTableProjection.BuildAbilityRows(
                    set.AbilityBindings,
                    set.StableId,
                    knownActions,
                    knownAbilities,
                    _abilityBindingTableQuery,
                    _abilityBindingTableSort,
                    index => CountBindingProblems(
                        CharacterActionAuthoringProblemTargetKind.AbilityBinding,
                        set.StableId,
                        index));
            VisualElement card = AddCard(main, "能力绑定");
            if (_focusedAbilityId > 0)
            {
                AddFocusedAbilityBindingBanner(card, focusedIndex);
                if (focusedIndex >= 0)
                {
                    card.Add(new Button(() =>
                    {
                        _selectedAbilityBindingOriginalIndex = focusedIndex;
                        RebuildMappingWorkspaceContent();
                    })
                    {
                        text = "定位 abilityId "
                            + _focusedAbilityId.ToString(CultureInfo.InvariantCulture),
                    });
                }
            }
            AddBindingTableToolbar(
                card,
                rows.Count,
                set.AbilityBindings?.Length ?? 0,
                _abilityBindingTableQuery,
                value =>
                {
                    _abilityBindingTableQuery = value;
                    SaveSessionState();
                    RebuildMappingWorkspaceContent();
                },
                _abilityBindingTableSort,
                value =>
                {
                    _abilityBindingTableSort = value;
                    SaveSessionState();
                    RebuildMappingWorkspaceContent();
                });

            MultiColumnListView table = BuildBindingTable(rows, row =>
            {
                _selectedAbilityBindingOriginalIndex = row.OriginalIndex;
                BuildAbilityBindingTableInspector(inspector, row.OriginalIndex);
            });
            card.Add(table);
            AddAbilityBindingTableActions(card, rows, table);
            SelectBindingTableRow(
                table,
                rows,
                ref _selectedAbilityBindingOriginalIndex,
                inspector,
                BuildAbilityBindingTableInspector);
            _focusedAbilityBindingElement = table;
        }

        private static MultiColumnListView BuildBindingTable(
            List<CharacterActionBindingTableRow> rows,
            Action<CharacterActionBindingTableRow> selectionChanged)
        {
            var table = new MultiColumnListView
            {
                itemsSource = rows,
                selectionType = SelectionType.Single,
                virtualizationMethod = CollectionVirtualizationMethod.FixedHeight,
                fixedItemHeight = 25f,
                showAlternatingRowBackgrounds = AlternatingRowBackground.ContentOnly,
                reorderable = false,
            };
            table.style.height = Mathf.Clamp(56f + (rows.Count * 25f), 220f, 500f);
            table.style.flexGrow = 1f;
            AddBindingColumn(table, rows, "order", "顺序", 52f,
                row => (row.OriginalIndex + 1).ToString(CultureInfo.InvariantCulture));
            AddBindingColumn(table, rows, "source", "来源", 145f, row => row.Source);
            AddBindingColumn(table, rows, "target", "目标动作", 180f, row => row.TargetAction);
            AddBindingColumn(table, rows, "condition", "条件", 220f, row => row.Condition);
            AddBindingColumn(table, rows, "policy", "策略", 130f, row => row.Policy);
            AddBindingColumn(table, rows, "status", "状态", 100f, row => row.Status);
            AddBindingColumn(table, rows, "problems", "问题", 58f,
                row => row.ProblemCount.ToString(CultureInfo.InvariantCulture));
            table.selectionChanged += selected =>
            {
                foreach (object item in selected)
                {
                    if (item is CharacterActionBindingTableRow row)
                        selectionChanged?.Invoke(row);
                    break;
                }
            };
            return table;
        }

        private static void AddBindingColumn(
            MultiColumnListView table,
            IList<CharacterActionBindingTableRow> rows,
            string name,
            string title,
            float width,
            Func<CharacterActionBindingTableRow, string> getText)
        {
            table.columns.Add(new Column
            {
                name = name,
                title = title,
                width = width,
                minWidth = Math.Min(width, 52f),
                stretchable = name == "target" || name == "condition",
                sortable = false,
                makeCell = () => new Label(),
                bindCell = (element, index) =>
                {
                    var label = (Label)element;
                    label.text = index >= 0 && index < rows.Count
                        ? getText(rows[index]) ?? string.Empty
                        : string.Empty;
                    label.tooltip = label.text;
                },
            });
        }

        private static void AddBindingTableToolbar(
            VisualElement parent,
            int visibleCount,
            int totalCount,
            string query,
            Action<string> queryChanged,
            CharacterActionBindingTableSort sort,
            Action<CharacterActionBindingTableSort> sortChanged)
        {
            var toolbar = new VisualElement();
            toolbar.style.flexDirection = FlexDirection.Row;
            toolbar.style.alignItems = Align.Center;
            toolbar.style.marginBottom = 5f;
            var search = new TextField("搜索")
            {
                value = query ?? string.Empty,
                isDelayed = true,
            };
            search.style.flexGrow = 1f;
            search.RegisterValueChangedCallback(evt => queryChanged?.Invoke(evt.newValue ?? string.Empty));
            toolbar.Add(search);
            var sortField = new PopupField<string>(
                "排序",
                new List<string>(BindingSortLabels),
                (int)sort);
            sortField.style.minWidth = 165f;
            sortField.RegisterValueChangedCallback(evt =>
            {
                int index = Array.IndexOf(BindingSortLabels, evt.newValue);
                if (index >= 0)
                    sortChanged?.Invoke((CharacterActionBindingTableSort)index);
            });
            toolbar.Add(sortField);
            toolbar.Add(new Label(visibleCount.ToString(CultureInfo.InvariantCulture)
                + " / " + totalCount.ToString(CultureInfo.InvariantCulture)));
            parent.Add(toolbar);
        }

        private void AddCommandBindingTableActions(
            VisualElement parent,
            IReadOnlyList<CharacterActionBindingTableRow> rows,
            MultiColumnListView table)
        {
            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.flexWrap = Wrap.Wrap;
            Button add = new Button(AddCommandBindingTableRow) { text = "新增" };
            Button duplicate = new Button(DuplicateSelectedCommandBinding) { text = "复制" };
            Button remove = new Button(RemoveSelectedCommandBinding) { text = "删除" };
            Button moveUp = new Button(() => MoveSelectedCommandBinding(-1)) { text = "上移" };
            Button moveDown = new Button(() => MoveSelectedCommandBinding(1)) { text = "下移" };
            Button locate = new Button(() => LocateCommandBindingAction(_selectedCommandBindingOriginalIndex))
            {
                text = "定位目标动作"
            };
            row.Add(add);
            row.Add(duplicate);
            row.Add(remove);
            row.Add(moveUp);
            row.Add(moveDown);
            row.Add(locate);
            parent.Add(row);
            table.selectionChanged += _ => UpdateBindingActionButtons(
                duplicate,
                remove,
                moveUp,
                moveDown,
                locate,
                _selectedCommandBindingOriginalIndex,
                _model?.ActionSet?.CommandBindings?.Length ?? 0,
                _commandBindingTableQuery,
                _commandBindingTableSort);
            UpdateBindingActionButtons(
                duplicate,
                remove,
                moveUp,
                moveDown,
                locate,
                _selectedCommandBindingOriginalIndex,
                _model?.ActionSet?.CommandBindings?.Length ?? 0,
                _commandBindingTableQuery,
                _commandBindingTableSort);
            if (!CharacterActionBindingTableProjection.CanReorder(
                _commandBindingTableQuery,
                _commandBindingTableSort))
            {
                row.Add(new Label("筛选或非原始排序时不能重排。"));
            }
        }

        private void AddAbilityBindingTableActions(
            VisualElement parent,
            IReadOnlyList<CharacterActionBindingTableRow> rows,
            MultiColumnListView table)
        {
            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.flexWrap = Wrap.Wrap;
            Button add = new Button(AddAbilityBindingTableRow) { text = "新增" };
            Button duplicate = new Button(DuplicateSelectedAbilityBinding) { text = "复制" };
            Button remove = new Button(RemoveSelectedAbilityBinding) { text = "删除" };
            Button moveUp = new Button(() => MoveSelectedAbilityBinding(-1)) { text = "上移" };
            Button moveDown = new Button(() => MoveSelectedAbilityBinding(1)) { text = "下移" };
            Button locate = new Button(() => LocateAbilityBindingAction(_selectedAbilityBindingOriginalIndex))
            {
                text = "定位目标动作"
            };
            row.Add(add);
            row.Add(duplicate);
            row.Add(remove);
            row.Add(moveUp);
            row.Add(moveDown);
            row.Add(locate);
            parent.Add(row);
            table.selectionChanged += _ => UpdateBindingActionButtons(
                duplicate,
                remove,
                moveUp,
                moveDown,
                locate,
                _selectedAbilityBindingOriginalIndex,
                _model?.ActionSet?.AbilityBindings?.Length ?? 0,
                _abilityBindingTableQuery,
                _abilityBindingTableSort);
            UpdateBindingActionButtons(
                duplicate,
                remove,
                moveUp,
                moveDown,
                locate,
                _selectedAbilityBindingOriginalIndex,
                _model?.ActionSet?.AbilityBindings?.Length ?? 0,
                _abilityBindingTableQuery,
                _abilityBindingTableSort);
            if (!CharacterActionBindingTableProjection.CanReorder(
                _abilityBindingTableQuery,
                _abilityBindingTableSort))
            {
                row.Add(new Label("筛选或非原始排序时不能重排。"));
            }
        }

        private static void UpdateBindingActionButtons(
            Button duplicate,
            Button remove,
            Button moveUp,
            Button moveDown,
            Button locate,
            int selectedIndex,
            int count,
            string query,
            CharacterActionBindingTableSort sort)
        {
            bool hasSelection = selectedIndex >= 0 && selectedIndex < count;
            bool canReorder = hasSelection
                && CharacterActionBindingTableProjection.CanReorder(query, sort);
            duplicate.SetEnabled(hasSelection);
            remove.SetEnabled(hasSelection);
            locate.SetEnabled(hasSelection);
            moveUp.SetEnabled(canReorder && selectedIndex > 0);
            moveDown.SetEnabled(canReorder && selectedIndex < count - 1);
        }

        private static void SelectBindingTableRow(
            MultiColumnListView table,
            IReadOnlyList<CharacterActionBindingTableRow> rows,
            ref int selectedOriginalIndex,
            VisualElement inspector,
            Action<VisualElement, int> buildInspector)
        {
            int displayIndex = -1;
            for (int i = 0; i < rows.Count; i++)
            {
                if (rows[i].OriginalIndex == selectedOriginalIndex)
                {
                    displayIndex = i;
                    break;
                }
            }
            if (displayIndex < 0 && rows.Count > 0)
            {
                displayIndex = 0;
                selectedOriginalIndex = rows[0].OriginalIndex;
            }
            if (displayIndex >= 0)
            {
                table.selectedIndex = displayIndex;
                buildInspector(inspector, selectedOriginalIndex);
            }
            else
            {
                selectedOriginalIndex = -1;
                inspector.Clear();
                inspector.Add(new HelpBox("没有匹配的绑定。", HelpBoxMessageType.Info));
            }
        }

        private void PrepareBindingProblemNavigation(
            CharacterActionBindingProblemNavigationPlan plan)
        {
            if (plan.TargetKind == CharacterActionAuthoringProblemTargetKind.CommandBinding)
            {
                if (!IsCommandBindingVisible(plan.OriginalIndex))
                    _commandBindingTableQuery = string.Empty;
                _selectedCommandBindingOriginalIndex = plan.OriginalIndex;
                return;
            }

            if (!IsAbilityBindingVisible(plan.OriginalIndex))
                _abilityBindingTableQuery = string.Empty;
            _selectedAbilityBindingOriginalIndex = plan.OriginalIndex;
        }

        private bool IsCommandBindingVisible(int originalIndex)
        {
            CharacterActionSetJsonDto set = _model?.ActionSet;
            if (set == null)
                return false;
            List<CharacterActionBindingTableRow> rows =
                CharacterActionBindingTableProjection.BuildCommandRows(
                    set.CommandBindings,
                    set.StableId,
                    BuildBindingKnownActionIds(),
                    _commandBindingTableQuery,
                    _commandBindingTableSort,
                    index => CountBindingProblems(
                        CharacterActionAuthoringProblemTargetKind.CommandBinding,
                        set.StableId,
                        index));
            return CharacterActionBindingTableProjection.ContainsOriginalIndex(rows, originalIndex);
        }

        private bool IsAbilityBindingVisible(int originalIndex)
        {
            CharacterActionSetJsonDto set = _model?.ActionSet;
            if (set == null)
                return false;
            int[] knownAbilityArray = CharacterActionPreviewBindingDiscovery.DiscoverKnownAbilityIds(
                set.CharacterStableId,
                set.EquipmentStateStableId);
            List<CharacterActionBindingTableRow> rows =
                CharacterActionBindingTableProjection.BuildAbilityRows(
                    set.AbilityBindings,
                    set.StableId,
                    BuildBindingKnownActionIds(),
                    new HashSet<int>(knownAbilityArray),
                    _abilityBindingTableQuery,
                    _abilityBindingTableSort,
                    index => CountBindingProblems(
                        CharacterActionAuthoringProblemTargetKind.AbilityBinding,
                        set.StableId,
                        index));
            return CharacterActionBindingTableProjection.ContainsOriginalIndex(rows, originalIndex);
        }

        private void ScheduleFocusBindingProblemInspector(
            CharacterActionBindingProblemNavigationPlan plan)
        {
            rootVisualElement.schedule.Execute(() =>
            {
                VisualElement target = _mappingInspectorHost?.Q<VisualElement>(plan.FieldElementName)
                    ?? _mappingInspectorHost?.Q<VisualElement>(plan.InspectorCardElementName);
                if (target == null)
                    return;

                if (_mappingInspectorHost is ScrollView inspectorScroll)
                    inspectorScroll.ScrollTo(target);
                else
                    target.GetFirstAncestorOfType<ScrollView>()?.ScrollTo(target);
                FocusBindingProblemElement(target);
            }).ExecuteLater(1);
        }

        private static void FocusBindingProblemElement(VisualElement target)
        {
            Button button = target.Q<Button>();
            if (button != null)
            {
                button.Focus();
                return;
            }

            IntegerField integerField = target.Q<IntegerField>();
            if (integerField != null)
            {
                integerField.Focus();
                return;
            }

            Toggle toggle = target.Q<Toggle>();
            if (toggle != null)
            {
                toggle.Focus();
                return;
            }

            TextField textField = target.Q<TextField>();
            if (textField != null)
                textField.Focus();
            else if (target.focusable)
                target.Focus();
        }

        private void BuildCommandBindingTableInspector(VisualElement inspector, int index)
        {
            inspector.Clear();
            CharacterActionBindingJsonDto[] bindings =
                _model?.ActionSet?.CommandBindings ?? Array.Empty<CharacterActionBindingJsonDto>();
            if (index < 0 || index >= bindings.Length)
                return;
            CharacterActionBindingJsonDto binding = bindings[index] ?? new CharacterActionBindingJsonDto();
            bindings[index] = binding;
            VisualElement card = AddCard(inspector, "指令绑定 #" + (index + 1));
            card.name = CharacterActionBindingProblemNavigation.InspectorCardElementName(
                CharacterActionAuthoringProblemTargetKind.CommandBinding,
                index);
            VisualElement intentField = AddBindingProblemFieldHost(
                card,
                CharacterActionAuthoringProblemTargetKind.CommandBinding,
                index,
                "intentId");
            AddActionSetSearchPickerField(
                intentField,
                "Intent Id",
                CharacterActionAuthoringIds.Label(binding.IntentId, 404),
                CharacterActionAuthoringSearchCatalogBuilder.BuildIntents(_documentSession, _model),
                CharacterActionAuthoringSearchCategory.Intent,
                value => binding.IntentId = CharacterActionAuthoringIds.Resolve(value, 404),
                refreshScope: UiRefreshScope.Bindings);
            VisualElement actionField = AddBindingProblemFieldHost(
                card,
                CharacterActionAuthoringProblemTargetKind.CommandBinding,
                index,
                "actionId");
            AddActionSetSearchPickerField(
                actionField,
                "目标动作",
                binding.ActionId.ToString(CultureInfo.InvariantCulture),
                CharacterActionAuthoringSearchCatalogBuilder.BuildActions(_documentSession, _model),
                CharacterActionAuthoringSearchCategory.Action,
                value =>
                {
                    if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsed))
                        binding.ActionId = parsed;
                },
                refreshScope: UiRefreshScope.Bindings);
            AddBindingIntegerField(AddBindingProblemFieldHost(
                    card,
                    CharacterActionAuthoringProblemTargetKind.CommandBinding,
                    index,
                    "priority"), "Priority", binding.Priority, value =>
                ApplyCommandBindingEdit(index, item => item.Priority = value));
            AddBindingToggle(AddBindingProblemFieldHost(
                    card,
                    CharacterActionAuthoringProblemTargetKind.CommandBinding,
                    index,
                    "allowQueue"), "Allow Queue", binding.AllowQueue, value =>
                ApplyCommandBindingEdit(index, item => item.AllowQueue = value));
            AddBindingIntegerField(AddBindingProblemFieldHost(
                    card,
                    CharacterActionAuthoringProblemTargetKind.CommandBinding,
                    index,
                    "queueWindowFrames"), "Queue Window Frames", binding.QueueWindowFrames, value =>
                ApplyCommandBindingEdit(index, item => item.QueueWindowFrames = Math.Max(0, value)));
            AddUnknownActionReferenceHint(card, binding.ActionId);
        }

        private void BuildAbilityBindingTableInspector(VisualElement inspector, int index)
        {
            inspector.Clear();
            CharacterAbilityActionBindingJsonDto[] bindings =
                _model?.ActionSet?.AbilityBindings ?? Array.Empty<CharacterAbilityActionBindingJsonDto>();
            if (index < 0 || index >= bindings.Length)
                return;
            CharacterAbilityActionBindingJsonDto binding =
                bindings[index] ?? new CharacterAbilityActionBindingJsonDto();
            bindings[index] = binding;
            VisualElement card = AddCard(inspector, "能力绑定 #" + (index + 1));
            card.name = CharacterActionBindingProblemNavigation.InspectorCardElementName(
                CharacterActionAuthoringProblemTargetKind.AbilityBinding,
                index);
            VisualElement abilityField = AddBindingProblemFieldHost(
                card,
                CharacterActionAuthoringProblemTargetKind.AbilityBinding,
                index,
                "abilityId");
            AddActionSetSearchPickerField(
                abilityField,
                "Ability Id",
                binding.AbilityId.ToString(CultureInfo.InvariantCulture),
                CharacterActionAuthoringSearchCatalogBuilder.BuildAbilities(
                    _documentSession,
                    _model,
                    CharacterActionPreviewBindingDiscovery.DiscoverKnownAbilityIds(
                        _model?.ActionSet?.CharacterStableId,
                        _model?.ActionSet?.EquipmentStateStableId)),
                CharacterActionAuthoringSearchCategory.Ability,
                value =>
                {
                    if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsed))
                        binding.AbilityId = parsed;
                },
                refreshScope: UiRefreshScope.Bindings);
            VisualElement actionField = AddBindingProblemFieldHost(
                card,
                CharacterActionAuthoringProblemTargetKind.AbilityBinding,
                index,
                "actionId");
            AddActionSetSearchPickerField(
                actionField,
                "目标动作",
                binding.ActionId.ToString(CultureInfo.InvariantCulture),
                CharacterActionAuthoringSearchCatalogBuilder.BuildActions(_documentSession, _model),
                CharacterActionAuthoringSearchCategory.Action,
                value =>
                {
                    if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsed))
                        binding.ActionId = parsed;
                },
                refreshScope: UiRefreshScope.Bindings);
            VisualElement requiredTagsField = AddBindingProblemFieldHost(
                card,
                CharacterActionAuthoringProblemTargetKind.AbilityBinding,
                index,
                "requiredTags");
            AddActionSetTagPickerField(
                requiredTagsField,
                "Required Tags",
                binding.RequiredTags,
                value => binding.RequiredTags = value);
            VisualElement forbiddenTagsField = AddBindingProblemFieldHost(
                card,
                CharacterActionAuthoringProblemTargetKind.AbilityBinding,
                index,
                "forbiddenTags");
            AddActionSetTagPickerField(
                forbiddenTagsField,
                "Forbidden Tags",
                binding.ForbiddenTags,
                value => binding.ForbiddenTags = value);
            AddUnknownActionReferenceHint(card, binding.ActionId);
            card.Add(new Label(FormatAbilityBindingStatus(binding))
            {
                style = { whiteSpace = WhiteSpace.Normal },
            });
        }

        private static VisualElement AddBindingProblemFieldHost(
            VisualElement parent,
            CharacterActionAuthoringProblemTargetKind targetKind,
            int originalIndex,
            string fieldPath)
        {
            var host = new VisualElement
            {
                name = CharacterActionBindingProblemNavigation.FieldElementName(
                    targetKind,
                    originalIndex,
                    fieldPath),
            };
            parent.Add(host);
            return host;
        }

        private void AddUnknownActionReferenceHint(VisualElement parent, int actionId)
        {
            if (BuildBindingKnownActionIds().Contains(actionId))
                return;
            parent.Add(new HelpBox(
                "未知动作引用：" + (actionId <= 0 ? "（空）" : "#" + actionId)
                + "。该旧值会保留，除非你显式修改。",
                HelpBoxMessageType.Warning));
        }

        private void AddBindingTextField(
            VisualElement parent,
            string label,
            string value,
            Action<string> commit)
        {
            var field = new TextField(label)
            {
                value = value ?? string.Empty,
                isDelayed = true,
            };
            field.RegisterValueChangedCallback(evt => commit?.Invoke(evt.newValue ?? string.Empty));
            parent.Add(field);
        }

        private void AddBindingReferenceField(
            VisualElement parent,
            string label,
            string value,
            List<string> choices,
            Action<string> commit)
        {
            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.flexWrap = Wrap.Wrap;
            var field = new TextField(label)
            {
                value = value ?? string.Empty,
                isDelayed = true,
            };
            field.style.flexGrow = 1f;
            field.RegisterValueChangedCallback(evt => commit?.Invoke(evt.newValue ?? string.Empty));
            row.Add(field);
            List<string> popupChoices = BuildReferenceChoices(value, choices);
            if (popupChoices.Count > 0)
            {
                var menuLabels = new List<string>(popupChoices.Count);
                for (int i = 0; i < popupChoices.Count; i++)
                    menuLabels.Add(CharacterActionAuthoringUiLabels.ToMenuSafeLabel(popupChoices[i]));
                var popup = new PopupField<string>(string.Empty, menuLabels, 0);
                popup.style.minWidth = 145f;
                popup.RegisterValueChangedCallback(evt =>
                {
                    int index = menuLabels.IndexOf(evt.newValue);
                    if (index >= 0 && index < popupChoices.Count)
                        commit?.Invoke(popupChoices[index] ?? string.Empty);
                });
                row.Add(popup);
            }
            parent.Add(row);
        }

        private void AddBindingIntegerField(
            VisualElement parent,
            string label,
            int value,
            Action<int> commit)
        {
            var field = new IntegerField(label) { value = value, isDelayed = true };
            field.RegisterValueChangedCallback(evt => commit?.Invoke(evt.newValue));
            parent.Add(field);
        }

        private void AddBindingToggle(
            VisualElement parent,
            string label,
            bool value,
            Action<bool> commit)
        {
            var field = new Toggle(label) { value = value };
            field.RegisterValueChangedCallback(evt => commit?.Invoke(evt.newValue));
            parent.Add(field);
        }

        private void ApplyCommandBindingEdit(int index, Action<CharacterActionBindingJsonDto> edit)
        {
            ApplyActionSetDocumentChange(() =>
            {
                CharacterActionBindingJsonDto[] bindings =
                    _model.ActionSet.CommandBindings ?? Array.Empty<CharacterActionBindingJsonDto>();
                if (index < 0 || index >= bindings.Length)
                    return;
                CharacterActionBindingJsonDto binding = bindings[index] ?? new CharacterActionBindingJsonDto();
                edit?.Invoke(binding);
                bindings[index] = binding;
                _model.ActionSet.CommandBindings = bindings;
            }, UiRefreshScope.Bindings);
        }

        private void ApplyAbilityBindingEdit(int index, Action<CharacterAbilityActionBindingJsonDto> edit)
        {
            ApplyActionSetDocumentChange(() =>
            {
                CharacterAbilityActionBindingJsonDto[] bindings =
                    _model.ActionSet.AbilityBindings ?? Array.Empty<CharacterAbilityActionBindingJsonDto>();
                if (index < 0 || index >= bindings.Length)
                    return;
                CharacterAbilityActionBindingJsonDto binding =
                    bindings[index] ?? new CharacterAbilityActionBindingJsonDto();
                edit?.Invoke(binding);
                bindings[index] = binding;
                _model.ActionSet.AbilityBindings = bindings;
            }, UiRefreshScope.Bindings);
        }

        private void AddCommandBindingTableRow()
        {
            CharacterActionBindingJsonDto[] existing =
                _model.ActionSet.CommandBindings ?? Array.Empty<CharacterActionBindingJsonDto>();
            _selectedCommandBindingOriginalIndex = existing.Length;
            ApplyActionSetDocumentChange(() =>
            {
                var result = new CharacterActionBindingJsonDto[existing.Length + 1];
                Array.Copy(existing, result, existing.Length);
                result[existing.Length] = new CharacterActionBindingJsonDto
                {
                    IntentId = CharacterActionAuthoringIds.Resolve("Intent", 404),
                    ActionId = FirstBindingActionId(),
                };
                _model.ActionSet.CommandBindings = result;
            }, UiRefreshScope.Bindings);
        }

        private void AddAbilityBindingTableRow()
        {
            CharacterAbilityActionBindingJsonDto[] existing =
                _model.ActionSet.AbilityBindings ?? Array.Empty<CharacterAbilityActionBindingJsonDto>();
            _selectedAbilityBindingOriginalIndex = existing.Length;
            List<string> known = GetKnownAbilityIdChoices();
            int abilityId = 0;
            if (known.Count > 0)
                int.TryParse(known[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out abilityId);
            ApplyActionSetDocumentChange(() =>
            {
                var result = new CharacterAbilityActionBindingJsonDto[existing.Length + 1];
                Array.Copy(existing, result, existing.Length);
                result[existing.Length] = new CharacterAbilityActionBindingJsonDto
                {
                    AbilityId = abilityId,
                    ActionId = FirstBindingActionId(),
                    RequiredTags = Array.Empty<string>(),
                    ForbiddenTags = Array.Empty<string>(),
                };
                _model.ActionSet.AbilityBindings = result;
            }, UiRefreshScope.Bindings);
        }

        private void DuplicateSelectedCommandBinding()
        {
            int index = _selectedCommandBindingOriginalIndex;
            CharacterActionBindingJsonDto[] existing =
                _model.ActionSet.CommandBindings ?? Array.Empty<CharacterActionBindingJsonDto>();
            if (index < 0 || index >= existing.Length)
                return;
            CharacterActionBindingJsonDto source = existing[index] ?? new CharacterActionBindingJsonDto();
            _selectedCommandBindingOriginalIndex = index + 1;
            ApplyActionSetDocumentChange(() =>
            {
                var result = new CharacterActionBindingJsonDto[existing.Length + 1];
                Array.Copy(existing, 0, result, 0, index + 1);
                result[index + 1] = new CharacterActionBindingJsonDto
                {
                    IntentId = source.IntentId,
                    ActionId = source.ActionId,
                    Priority = source.Priority,
                    AllowQueue = source.AllowQueue,
                    QueueWindowFrames = source.QueueWindowFrames,
                };
                Array.Copy(existing, index + 1, result, index + 2, existing.Length - index - 1);
                _model.ActionSet.CommandBindings = result;
            }, UiRefreshScope.Bindings);
        }

        private void DuplicateSelectedAbilityBinding()
        {
            int index = _selectedAbilityBindingOriginalIndex;
            CharacterAbilityActionBindingJsonDto[] existing =
                _model.ActionSet.AbilityBindings ?? Array.Empty<CharacterAbilityActionBindingJsonDto>();
            if (index < 0 || index >= existing.Length)
                return;
            CharacterAbilityActionBindingJsonDto source =
                existing[index] ?? new CharacterAbilityActionBindingJsonDto();
            _selectedAbilityBindingOriginalIndex = index + 1;
            ApplyActionSetDocumentChange(() =>
            {
                var result = new CharacterAbilityActionBindingJsonDto[existing.Length + 1];
                Array.Copy(existing, 0, result, 0, index + 1);
                result[index + 1] = new CharacterAbilityActionBindingJsonDto
                {
                    AbilityId = source.AbilityId,
                    ActionId = source.ActionId,
                    RequiredTags = (string[])(source.RequiredTags ?? Array.Empty<string>()).Clone(),
                    ForbiddenTags = (string[])(source.ForbiddenTags ?? Array.Empty<string>()).Clone(),
                };
                Array.Copy(existing, index + 1, result, index + 2, existing.Length - index - 1);
                _model.ActionSet.AbilityBindings = result;
            }, UiRefreshScope.Bindings);
        }

        private void RemoveSelectedCommandBinding()
        {
            int index = _selectedCommandBindingOriginalIndex;
            CharacterActionBindingJsonDto[] existing =
                _model.ActionSet.CommandBindings ?? Array.Empty<CharacterActionBindingJsonDto>();
            if (index < 0 || index >= existing.Length)
                return;
            _selectedCommandBindingOriginalIndex = Math.Min(index, existing.Length - 2);
            ApplyActionSetDocumentChange(() =>
            {
                var result = new CharacterActionBindingJsonDto[existing.Length - 1];
                Array.Copy(existing, 0, result, 0, index);
                Array.Copy(existing, index + 1, result, index, existing.Length - index - 1);
                _model.ActionSet.CommandBindings = result;
            }, UiRefreshScope.Bindings);
        }

        private void RemoveSelectedAbilityBinding()
        {
            int index = _selectedAbilityBindingOriginalIndex;
            CharacterAbilityActionBindingJsonDto[] existing =
                _model.ActionSet.AbilityBindings ?? Array.Empty<CharacterAbilityActionBindingJsonDto>();
            if (index < 0 || index >= existing.Length)
                return;
            _selectedAbilityBindingOriginalIndex = Math.Min(index, existing.Length - 2);
            ApplyActionSetDocumentChange(() =>
            {
                var result = new CharacterAbilityActionBindingJsonDto[existing.Length - 1];
                Array.Copy(existing, 0, result, 0, index);
                Array.Copy(existing, index + 1, result, index, existing.Length - index - 1);
                _model.ActionSet.AbilityBindings = result;
            }, UiRefreshScope.Bindings);
        }

        private void MoveSelectedCommandBinding(int delta)
        {
            if (!CharacterActionBindingTableProjection.CanReorder(
                _commandBindingTableQuery,
                _commandBindingTableSort))
                return;
            CharacterActionBindingJsonDto[] existing =
                _model.ActionSet.CommandBindings ?? Array.Empty<CharacterActionBindingJsonDto>();
            if (!CharacterActionBindingTableProjection.TryMove(
                existing,
                _selectedCommandBindingOriginalIndex,
                delta,
                out CharacterActionBindingJsonDto[] moved))
                return;
            _selectedCommandBindingOriginalIndex += delta;
            ApplyActionSetDocumentChange(
                () => _model.ActionSet.CommandBindings = moved,
                UiRefreshScope.Bindings);
        }

        private void MoveSelectedAbilityBinding(int delta)
        {
            if (!CharacterActionBindingTableProjection.CanReorder(
                _abilityBindingTableQuery,
                _abilityBindingTableSort))
                return;
            CharacterAbilityActionBindingJsonDto[] existing =
                _model.ActionSet.AbilityBindings ?? Array.Empty<CharacterAbilityActionBindingJsonDto>();
            if (!CharacterActionBindingTableProjection.TryMove(
                existing,
                _selectedAbilityBindingOriginalIndex,
                delta,
                out CharacterAbilityActionBindingJsonDto[] moved))
                return;
            _selectedAbilityBindingOriginalIndex += delta;
            ApplyActionSetDocumentChange(
                () => _model.ActionSet.AbilityBindings = moved,
                UiRefreshScope.Bindings);
        }

        private void LocateCommandBindingAction(int index)
        {
            CharacterActionBindingJsonDto[] bindings =
                _model.ActionSet.CommandBindings ?? Array.Empty<CharacterActionBindingJsonDto>();
            if (index >= 0 && index < bindings.Length)
                LocateBindingAction(bindings[index]?.ActionId ?? 0);
        }

        private void LocateAbilityBindingAction(int index)
        {
            CharacterAbilityActionBindingJsonDto[] bindings =
                _model.ActionSet.AbilityBindings ?? Array.Empty<CharacterAbilityActionBindingJsonDto>();
            if (index >= 0 && index < bindings.Length)
                LocateBindingAction(bindings[index]?.ActionId ?? 0);
        }

        private void LocateBindingAction(int actionId)
        {
            if (SelectWorkbenchAction(actionId))
            {
                SetWorkspace(AuthoringWorkspace.ActionComposition);
                return;
            }
            ShowNotification(new GUIContent("未找到目标动作：#" + actionId));
        }

        private HashSet<int> BuildBindingKnownActionIds()
        {
            var result = new HashSet<int>();
            if (_documentSession != null)
            {
                for (int i = 0; i < _documentSession.ProfileActions.Count; i++)
                {
                    int actionId = _documentSession.ProfileActions[i]?.Action?.ActionId ?? 0;
                    if (actionId > 0)
                        result.Add(actionId);
                }
            }
            else
            {
                for (int i = 0; i < (_model?.Actions.Count ?? 0); i++)
                {
                    int actionId = _model.Actions[i]?.ActionId ?? 0;
                    if (actionId > 0)
                        result.Add(actionId);
                }
            }
            return result;
        }

        private List<int> BuildBindingActionChoices()
        {
            HashSet<int> known = BuildBindingKnownActionIds();
            var result = new List<int>(known);
            result.Sort();
            return result;
        }

        private int FirstBindingActionId()
        {
            List<int> actions = BuildBindingActionChoices();
            return actions.Count > 0 ? actions[0] : 0;
        }

        private int CountBindingProblems(
            CharacterActionAuthoringProblemTargetKind targetKind,
            string actionSetStableId,
            int originalIndex)
        {
            if (_problemsModel == null && _validation != null)
                _problemsModel = CharacterActionAuthoringProblemsModel.Create(_validation, _referenceDiscovery);
            if (_problemsModel == null)
                return 0;
            int count = 0;
            IReadOnlyList<CharacterActionAuthoringProblem> problems = _problemsModel.Items;
            for (int i = 0; i < problems.Count; i++)
            {
                CharacterActionAuthoringProblemLocation location = problems[i].Location;
                if (location.TargetKind != targetKind || location.ItemIndex != originalIndex)
                    continue;
                if (!string.IsNullOrWhiteSpace(location.ObjectKey)
                    && !string.Equals(location.ObjectKey, actionSetStableId, StringComparison.Ordinal)
                    && !int.TryParse(location.ObjectKey, NumberStyles.None, CultureInfo.InvariantCulture, out _))
                    continue;
                count++;
            }
            return count;
        }
    }
}
