using System;
using System.Collections.Generic;
using MxFramework.Config.Levels;
using MxFramework.Editor.Levels;
using MxFramework.Gameplay.Triggers;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace MxFramework.Game.CharacterTest.Editor
{
    public sealed partial class CharacterTestWorldObjectLevelEditorModule
    {
        private string _triggerWorkspaceSearchText = string.Empty;
        private string _triggerWorkspaceCategory =
            TriggerWorkspaceAllCategory;
        private string _triggerWorkspaceRegionFilter = string.Empty;
        private string _selectedTriggerWorkspaceEntryId = string.Empty;
        private bool _triggerWorkspaceRequiresExplicitSelection;
        private float _triggerWorkspaceListScrollY;
        private float _triggerWorkspaceEditorScrollY;
        private bool _showTriggerWorkspaceRuntimeInspector;
        private MxLevelEditorWindow _triggerWorkspaceWindow;
        private VisualElement _triggerWorkspaceRoot;
        private VisualElement _triggerWorkspaceListHost;
        private VisualElement _triggerWorkspaceEditorHost;
        private TwoPaneSplitView _triggerWorkspaceSplit;
        private Button _triggerWorkspaceNarrowToggle;
        private bool _triggerWorkspaceNarrow;
        private bool _triggerWorkspaceNavigationOpen;

        internal VisualElement CreateGameplayTriggerWorkspace(
            MxLevelEditorWindow window)
        {
            EnsureLoaded();
            ForceValidate();
            _triggerWorkspaceWindow = window;
            _activeContext = new MxLevelEditorContext(
                window,
                this,
                window == null
                    ? RefreshTriggerWorkspaceAfterMutation
                    : window.RepaintRequested);

            var root = new VisualElement
            {
                name = "character-test-trigger-workspace"
            };
            root.AddToClassList(
                "mx-level-editor__trigger-workspace");
            root.style.flexGrow = 1;
            root.style.minWidth = 0;
            root.style.minHeight = 0;
            _triggerWorkspaceRoot = root;

            _triggerWorkspaceNarrowToggle = new Button(
                ToggleTriggerWorkspaceNavigation)
            {
                name = "character-test-trigger-workspace-navigation-toggle",
                text = "显示触发器列表",
                tooltip = "在窄窗口中显示或收起触发器列表。"
            };
            _triggerWorkspaceNarrowToggle.AddToClassList(
                "mx-level-editor__trigger-workspace-navigation-toggle");
            _triggerWorkspaceNarrowToggle.style.display =
                DisplayStyle.None;
            root.Add(_triggerWorkspaceNarrowToggle);

            var split = new TwoPaneSplitView(
                0,
                280f,
                TwoPaneSplitViewOrientation.Horizontal)
            {
                name = "character-test-trigger-workspace-split"
            };
            split.AddToClassList(
                "mx-level-editor__trigger-workspace-split");
            split.style.flexGrow = 1;
            split.style.minWidth = 0;
            split.style.minHeight = 0;
            root.Add(split);
            _triggerWorkspaceSplit = split;

            split.Add(CreateTriggerWorkspaceNavigation());
            _triggerWorkspaceEditorHost = new VisualElement
            {
                name = "character-test-trigger-workspace-editor-host"
            };
            _triggerWorkspaceEditorHost.AddToClassList(
                "mx-level-editor__trigger-workspace-editor-host");
            _triggerWorkspaceEditorHost.style.flexGrow = 1;
            _triggerWorkspaceEditorHost.style.minWidth = 0;
            _triggerWorkspaceEditorHost.style.minHeight = 0;
            split.Add(_triggerWorkspaceEditorHost);

            EnsureTriggerWorkspaceSelection(
                BuildTriggerWorkspaceItems());
            RebuildTriggerWorkspaceList();
            RebuildTriggerWorkspaceEditor();
            root.RegisterCallback<GeometryChangedEvent>(
                evt => ApplyTriggerWorkspaceWidth(
                    evt.newRect.width));
            return root;
        }

        private void ApplyTriggerWorkspaceWidth(float width)
        {
            if (_triggerWorkspaceSplit == null
                || _triggerWorkspaceNarrowToggle == null
                || width <= 0f)
            {
                return;
            }

            bool narrow = width < 920f;
            if (narrow == _triggerWorkspaceNarrow)
                return;

            _triggerWorkspaceNarrow = narrow;
            _triggerWorkspaceRoot?.EnableInClassList(
                "mx-level-editor__trigger-workspace--narrow",
                narrow);
            _triggerWorkspaceNarrowToggle.style.display =
                narrow
                    ? DisplayStyle.Flex
                    : DisplayStyle.None;
            if (narrow)
            {
                _triggerWorkspaceNavigationOpen = false;
                _triggerWorkspaceSplit.CollapseChild(0);
            }
            else
            {
                _triggerWorkspaceNavigationOpen = true;
                _triggerWorkspaceSplit.UnCollapse();
            }
            UpdateTriggerWorkspaceNavigationToggle();
        }

        private void ToggleTriggerWorkspaceNavigation()
        {
            if (!_triggerWorkspaceNarrow
                || _triggerWorkspaceSplit == null)
            {
                return;
            }

            _triggerWorkspaceNavigationOpen =
                !_triggerWorkspaceNavigationOpen;
            if (_triggerWorkspaceNavigationOpen)
                _triggerWorkspaceSplit.UnCollapse();
            else
                _triggerWorkspaceSplit.CollapseChild(0);
            UpdateTriggerWorkspaceNavigationToggle();
        }

        private void UpdateTriggerWorkspaceNavigationToggle()
        {
            if (_triggerWorkspaceNarrowToggle == null)
                return;
            _triggerWorkspaceNarrowToggle.text =
                _triggerWorkspaceNavigationOpen
                    ? "收起触发器列表"
                    : "显示触发器列表";
        }

        private VisualElement CreateTriggerWorkspaceNavigation()
        {
            var navigation = new VisualElement
            {
                name = "character-test-trigger-workspace-navigation"
            };
            navigation.AddToClassList(
                "mx-level-editor__trigger-workspace-navigation");

            var actions = new VisualElement();
            actions.AddToClassList(
                "mx-level-editor__trigger-workspace-nav-actions");
            var search = new ToolbarSearchField
            {
                name = "character-test-trigger-workspace-search",
                value = _triggerWorkspaceSearchText,
                tooltip = "搜索名称、稳定 ID、区域、动作或已登记引用。"
            };
            search.AddToClassList(
                "mx-level-editor__trigger-workspace-search");
            search.RegisterValueChangedCallback(
                change =>
                {
                    _triggerWorkspaceSearchText =
                        change.newValue ?? string.Empty;
                    RebuildTriggerWorkspaceList();
                });
            actions.Add(search);
            actions.Add(CreateTriggerWorkspaceAddMenu());
            navigation.Add(actions);

            _triggerWorkspaceListHost = new VisualElement
            {
                name = "character-test-trigger-workspace-list-host"
            };
            _triggerWorkspaceListHost.style.flexGrow = 1;
            _triggerWorkspaceListHost.style.minHeight = 0;
            navigation.Add(_triggerWorkspaceListHost);
            return navigation;
        }

        private ToolbarMenu CreateTriggerWorkspaceAddMenu(
            string preferredRegionStableId = null)
        {
            var add = new ToolbarMenu
            {
                name = "character-test-trigger-workspace-add",
                text = "新建触发器"
            };
            add.AddToClassList(
                "mx-level-editor__trigger-workspace-add");
            add.menu.AppendAction(
                "战斗状态/当前玩家生命值低于阈值时触发 Story",
                _ => AddPlayerHpThresholdFromWorkspace());

            List<LevelWorldObjectRegionDefinition> regions =
                BuildEditableTriggerRegions(
                    string.IsNullOrWhiteSpace(preferredRegionStableId)
                        ? _triggerWorkspaceRegionFilter
                        : preferredRegionStableId);
            if (regions.Count == 0)
            {
                add.menu.AppendAction(
                    "区域/没有可用的 Trigger 区域",
                    _ => { },
                    _ => DropdownMenuAction.Status.Disabled);
                return add;
            }

            for (int i = 0; i < regions.Count; i++)
            {
                LevelWorldObjectRegionDefinition region = regions[i];
                string regionName = ResolveTriggerWorkspaceRegionName(
                    region,
                    region.StableId)
                    .Replace("/", "／");
                string prefix = regions.Count > 1
                    ? regionName + "/"
                    : string.Empty;
                AppendTriggerTemplateAction(
                    add,
                    prefix + "进入时施加 Buff",
                    region.StableId,
                    CharacterTestLevelGameplayTriggerAuthoringDocument
                        .GameplayApplyBuffEnterOnceTemplateId);
                AppendTriggerTemplateAction(
                    add,
                    prefix + "区域内维持 Buff",
                    region.StableId,
                    CharacterTestLevelGameplayTriggerAuthoringDocument
                        .GameplayApplyRemoveBuffPresenceTemplateId);
                AppendTriggerTemplateAction(
                    add,
                    prefix + "进入时触发 Story",
                    region.StableId,
                    CharacterTestLevelGameplayTriggerAuthoringDocument
                        .StoryRaiseTriggerEnterOnceTemplateId);
            }

            return add;
        }

        private void AppendTriggerTemplateAction(
            ToolbarMenu menu,
            string label,
            string regionStableId,
            string templateId)
        {
            menu.menu.AppendAction(
                label,
                _ => AddTriggerFromWorkspace(
                    regionStableId,
                    templateId));
        }

        private List<LevelWorldObjectRegionDefinition>
            BuildEditableTriggerRegions(string requiredRegionStableId)
        {
            var regions =
                new List<LevelWorldObjectRegionDefinition>();
            if (_document?.Regions == null)
                return regions;

            for (int i = 0; i < _document.Regions.Count; i++)
            {
                LevelWorldObjectRegionDefinition region =
                    _document.Regions[i];
                if (region == null
                    || !region.Enabled
                    || region.RegionKind
                        != LevelWorldObjectRegionKind.TriggerRegion
                    || (!string.IsNullOrWhiteSpace(
                            requiredRegionStableId)
                        && !string.Equals(
                            region.StableId,
                            requiredRegionStableId,
                            StringComparison.Ordinal)))
                {
                    continue;
                }

                regions.Add(region);
            }
            return regions;
        }

        private void RebuildTriggerWorkspaceList()
        {
            if (_triggerWorkspaceListHost == null)
                return;

            _triggerWorkspaceListHost.Clear();
            List<TriggerWorkspaceItem> items =
                BuildTriggerWorkspaceItems();
            EnsureTriggerWorkspaceSelection(items);
            _triggerWorkspaceListHost.Add(
                CreateTriggerWorkspaceCategories(items));

            if (!string.IsNullOrWhiteSpace(
                    _triggerWorkspaceRegionFilter))
            {
                LevelWorldObjectRegionDefinition filteredRegion =
                    FindTriggerWorkspaceRegion(
                        _triggerWorkspaceRegionFilter);
                var filter = new VisualElement();
                filter.AddToClassList(
                    "mx-level-editor__trigger-workspace-filter");
                filter.Add(new Label(
                    "区域："
                    + ResolveTriggerWorkspaceRegionName(
                        filteredRegion,
                        _triggerWorkspaceRegionFilter)));
                var clear = new Button(
                    () =>
                    {
                        _triggerWorkspaceRegionFilter =
                            string.Empty;
                        _selectedTriggerWorkspaceEntryId =
                            string.Empty;
                        _triggerWorkspaceRequiresExplicitSelection =
                            false;
                        RebuildTriggerWorkspaceList();
                        RebuildTriggerWorkspaceEditor();
                    })
                {
                    text = "×",
                    tooltip = "清除区域筛选"
                };
                filter.Add(clear);
                _triggerWorkspaceListHost.Add(filter);
            }

            var scroll = new ScrollView(ScrollViewMode.Vertical)
            {
                name = "character-test-trigger-workspace-list"
            };
            scroll.AddToClassList(
                "mx-level-editor__trigger-workspace-list");
            scroll.verticalScroller.valueChanged +=
                value => _triggerWorkspaceListScrollY = value;
            _triggerWorkspaceListHost.Add(scroll);

            int visibleCount = 0;
            for (int i = 0; i < items.Count; i++)
            {
                TriggerWorkspaceItem item = items[i];
                if (!IsTriggerWorkspaceItemVisible(item))
                    continue;
                scroll.Add(CreateTriggerWorkspaceListItem(item));
                visibleCount++;
            }

            if (visibleCount == 0)
            {
                var empty = new Label(
                    string.IsNullOrWhiteSpace(
                        _triggerWorkspaceSearchText)
                        ? "当前分类没有触发器。"
                        : "没有匹配的触发器。");
                empty.AddToClassList(
                    "mx-level-editor__trigger-workspace-empty");
                scroll.Add(empty);
            }

            scroll.schedule.Execute(
                () =>
                {
                    if (scroll.panel != null)
                    {
                        scroll.scrollOffset = new Vector2(
                            0f,
                            _triggerWorkspaceListScrollY);
                    }
                });
        }

        private VisualElement CreateTriggerWorkspaceCategories(
            IReadOnlyList<TriggerWorkspaceItem> items)
        {
            var row = new VisualElement();
            row.AddToClassList(
                "mx-level-editor__trigger-workspace-categories");
            AddTriggerWorkspaceCategory(
                row,
                TriggerWorkspaceAllCategory,
                items?.Count ?? 0);

            int regionCount = CountTriggerWorkspaceCategory(
                items,
                TriggerWorkspaceRegionCategory);
            if (regionCount > 0)
            {
                AddTriggerWorkspaceCategory(
                    row,
                    TriggerWorkspaceRegionCategory,
                    regionCount);
            }

            int flowCount = CountTriggerWorkspaceCategory(
                items,
                TriggerWorkspaceFlowCategory);
            if (flowCount > 0)
            {
                AddTriggerWorkspaceCategory(
                    row,
                    TriggerWorkspaceFlowCategory,
                    flowCount);
            }
            return row;
        }

        private void AddTriggerWorkspaceCategory(
            VisualElement row,
            string category,
            int count)
        {
            var button = new Button(
                () =>
                {
                    _triggerWorkspaceCategory = category;
                    RebuildTriggerWorkspaceList();
                })
            {
                text = category + " " + count
            };
            button.AddToClassList(
                "mx-level-editor__trigger-workspace-category");
            button.EnableInClassList(
                "mx-level-editor__trigger-workspace-category--selected",
                string.Equals(
                    _triggerWorkspaceCategory,
                    category,
                    StringComparison.Ordinal));
            row.Add(button);
        }

        private static int CountTriggerWorkspaceCategory(
            IReadOnlyList<TriggerWorkspaceItem> items,
            string category)
        {
            int count = 0;
            if (items == null)
                return count;
            for (int i = 0; i < items.Count; i++)
            {
                if (string.Equals(
                        items[i].Category,
                        category,
                        StringComparison.Ordinal))
                {
                    count++;
                }
            }
            return count;
        }

        private bool IsTriggerWorkspaceItemVisible(
            TriggerWorkspaceItem item)
        {
            if (item == null)
                return false;
            if (!string.Equals(
                    _triggerWorkspaceCategory,
                    TriggerWorkspaceAllCategory,
                    StringComparison.Ordinal)
                && !string.Equals(
                    item.Category,
                    _triggerWorkspaceCategory,
                    StringComparison.Ordinal))
            {
                return false;
            }
            if (!string.IsNullOrWhiteSpace(
                    _triggerWorkspaceRegionFilter)
                && !string.Equals(
                    item.Region?.StableId,
                    _triggerWorkspaceRegionFilter,
                    StringComparison.Ordinal))
            {
                return false;
            }
            return string.IsNullOrWhiteSpace(
                    _triggerWorkspaceSearchText)
                || item.SearchText.IndexOf(
                    _triggerWorkspaceSearchText.Trim(),
                    StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private VisualElement CreateTriggerWorkspaceListItem(
            TriggerWorkspaceItem item)
        {
            var row = new VisualElement
            {
                name = "character-test-trigger-workspace-item-"
                    + item.Entry.StableId,
                focusable = true,
                tooltip = item.EffectSentence
            };
            row.AddToClassList(
                "mx-level-editor__trigger-workspace-item");
            row.EnableInClassList(
                "mx-level-editor__trigger-workspace-item--selected",
                string.Equals(
                    _selectedTriggerWorkspaceEntryId,
                    item.Entry.StableId,
                    StringComparison.Ordinal));
            row.RegisterCallback<ClickEvent>(
                _ => SelectTriggerWorkspaceItem(item));
            row.RegisterCallback<KeyDownEvent>(
                evt =>
                {
                    if (evt.keyCode == KeyCode.Return
                        || evt.keyCode == KeyCode.Space)
                    {
                        SelectTriggerWorkspaceItem(item);
                        evt.StopPropagation();
                    }
                });

            var firstLine = new VisualElement();
            firstLine.AddToClassList(
                "mx-level-editor__trigger-workspace-item-first-line");
            var status = new Label(
                item.IsValid
                    ? item.IsEnabled ? "●" : "○"
                    : "!");
            status.AddToClassList(
                item.IsValid
                    ? item.IsEnabled
                        ? "mx-level-editor__trigger-workspace-item-status--valid"
                        : "mx-level-editor__trigger-workspace-item-status--disabled"
                    : "mx-level-editor__trigger-workspace-item-status--error");
            firstLine.Add(status);
            var title = new Label(item.DisplayName);
            title.AddToClassList(
                "mx-level-editor__trigger-workspace-item-title");
            firstLine.Add(title);
            row.Add(firstLine);

            var detail = new Label(
                item.Category + " · " + item.RegionName);
            detail.AddToClassList(
                "mx-level-editor__trigger-workspace-item-detail");
            row.Add(detail);
            return row;
        }

        private void SelectTriggerWorkspaceItem(
            TriggerWorkspaceItem item)
        {
            if (item == null)
                return;
            _selectedTriggerWorkspaceEntryId =
                item.Entry.StableId;
            _triggerWorkspaceRequiresExplicitSelection = false;
            RebuildTriggerWorkspaceList();
            RebuildTriggerWorkspaceEditor();
            SelectTriggerWorkspaceSceneContext(item);
        }

        private void EnsureTriggerWorkspaceSelection(
            IReadOnlyList<TriggerWorkspaceItem> items)
        {
            if (items == null || items.Count == 0)
            {
                _selectedTriggerWorkspaceEntryId =
                    string.Empty;
                return;
            }
            if (_triggerWorkspaceRequiresExplicitSelection
                && string.IsNullOrWhiteSpace(
                    _selectedTriggerWorkspaceEntryId))
            {
                return;
            }

            for (int i = 0; i < items.Count; i++)
            {
                if (string.Equals(
                        items[i].Entry.StableId,
                        _selectedTriggerWorkspaceEntryId,
                        StringComparison.Ordinal))
                {
                    return;
                }
            }

            _selectedTriggerWorkspaceEntryId =
                items[0].Entry.StableId;
        }

        private void RebuildTriggerWorkspaceEditor(
            bool preserveViewState = false)
        {
            if (_triggerWorkspaceEditorHost == null)
                return;

            VisualElement editorHost =
                _triggerWorkspaceEditorHost;
            ScrollView previousScroll =
                preserveViewState
                    ? editorHost.Q<ScrollView>(
                        "character-test-trigger-workspace-editor")
                    : null;
            Vector2 scrollOffset =
                previousScroll?.scrollOffset
                ?? new Vector2(
                    0f,
                    _triggerWorkspaceEditorScrollY);
            InspectorFocusLocator? focusLocator =
                preserveViewState
                    ? CaptureInspectorFocusLocator(editorHost)
                    : null;
            int refreshRevision =
                ++_inspectorRefreshRevision;
            _triggerWorkspaceEditorHost.Clear();
            List<TriggerWorkspaceItem> items =
                BuildTriggerWorkspaceItems();
            TriggerWorkspaceItem selected =
                FindSelectedTriggerWorkspaceItem(items);
            if (selected == null)
            {
                var empty = new Label(
                    items.Count == 0
                        ? "点击“新建触发器”创建一条玩法规则。"
                        : "从左侧选择一个触发器。");
                empty.AddToClassList(
                    "mx-level-editor__trigger-workspace-editor-empty");
                _triggerWorkspaceEditorHost.Add(empty);
                return;
            }

            var scroll = new ScrollView(ScrollViewMode.Vertical)
            {
                name = "character-test-trigger-workspace-editor"
            };
            scroll.AddToClassList(
                "mx-level-editor__trigger-workspace-editor");
            scroll.contentContainer.style.width =
                Length.Percent(100f);
            scroll.contentContainer.style.maxWidth = 1180f;
            scroll.contentContainer.style.alignSelf = Align.Center;
            scroll.verticalScroller.valueChanged +=
                value => _triggerWorkspaceEditorScrollY = value;
            _triggerWorkspaceEditorHost.Add(scroll);

            scroll.Add(CreateTriggerWorkspaceHeader(selected));
            if (!_triggerDocument.TryGetTemplateSettings(
                    selected.Entry,
                    out _,
                    out GameplayTriggerStateScope stateScope,
                    out GameplayTriggerPresenceLimit presenceLimit,
                    out long cooldownFrames,
                    out int maxActivations)
                || !_triggerDocument.TryGetTemplatePayloads(
                    selected.Entry,
                    out long payload0,
                    out long payload1,
                    out long payload2))
            {
                scroll.Add(new HelpBox(
                    "该触发器的数据不完整，请先根据诊断修复。",
                    HelpBoxMessageType.Error));
                scroll.Add(
                    CreateGameplayTriggerTechnicalFoldout(
                        selected.Entry));
            }
            else
            {
                scroll.Add(CreateTriggerWorkspaceEventSection(
                    selected));
                scroll.Add(CreateTriggerWorkspaceConditionSection(
                    selected));
                scroll.Add(CreateTriggerActionGroup(
                    selected.Entry,
                    payload0,
                    payload1,
                    payload2));
                scroll.Add(CreateTriggerWorkspaceEffectSummary(
                    selected.EffectSentence));
                scroll.Add(CreateTriggerWorkspaceAdvancedOptions(
                    selected.Entry,
                    stateScope,
                    presenceLimit,
                    cooldownFrames,
                    maxActivations,
                    payload0,
                    payload1,
                    payload2));
                scroll.Add(
                    CreateGameplayTriggerTechnicalFoldout(
                        selected.Entry));
            }

            AddGameplayTriggerDiagnostics(scroll);
            if (_showTriggerWorkspaceRuntimeInspector)
            {
                VisualElement runtime = selected.Region != null
                    ? CreateGameplayTriggerRuntimeInspector(
                        selected.Region)
                    : CreateGameplayTriggerRuntimeInspector(
                        selected.Entry);
                runtime.AddToClassList(
                    "mx-level-editor__trigger-workspace-runtime");
                scroll.Add(runtime);
            }

            scroll.schedule.Execute(
                () =>
                {
                    if (scroll.panel != null)
                    {
                        scroll.scrollOffset = new Vector2(
                            0f,
                            preserveViewState
                                ? scrollOffset.y
                                : _triggerWorkspaceEditorScrollY);
                    }
                });
            if (preserveViewState)
            {
                scroll.scrollOffset = scrollOffset;
                editorHost.schedule.Execute(
                    () =>
                    {
                        if (refreshRevision
                                != _inspectorRefreshRevision
                            || !ReferenceEquals(
                                _triggerWorkspaceEditorHost,
                                editorHost))
                        {
                            return;
                        }

                        scroll.scrollOffset = scrollOffset;
                        _triggerWorkspaceEditorScrollY =
                            scrollOffset.y;
                        ResolveInspectorFocusTarget(
                                editorHost,
                                focusLocator)
                            ?.Focus();
                    });
            }
            SelectTriggerWorkspaceSceneContext(selected);
        }

        private TriggerWorkspaceItem FindSelectedTriggerWorkspaceItem(
            IReadOnlyList<TriggerWorkspaceItem> items)
        {
            if (items == null)
                return null;
            for (int i = 0; i < items.Count; i++)
            {
                if (string.Equals(
                        items[i].Entry.StableId,
                        _selectedTriggerWorkspaceEntryId,
                        StringComparison.Ordinal))
                {
                    return items[i];
                }
            }
            return null;
        }

        private VisualElement CreateTriggerWorkspaceHeader(
            TriggerWorkspaceItem item)
        {
            var root = new VisualElement();
            root.AddToClassList(
                "mx-level-editor__trigger-workspace-header");

            var firstLine = new VisualElement();
            firstLine.AddToClassList(
                "mx-level-editor__trigger-workspace-header-line");
            if (item.Region != null)
            {
                var locate = new Button(
                    () => LocateTriggerWorkspaceRegion(item))
                {
                    text = "◎",
                    tooltip = "在 Scene 中选择并聚焦来源区域。"
                };
                locate.name =
                    "character-test-trigger-workspace-locate";
                locate.AddToClassList(
                    "mx-level-editor__trigger-workspace-locate");
                firstLine.Add(locate);
            }

            var name = new TextField
            {
                name = "character-test-trigger-workspace-name",
                value = item.DisplayName,
                isDelayed = true,
                tooltip = "Enter 或失焦后提交名称，Esc 放弃本次输入。"
            };
            name.AddToClassList(
                "mx-level-editor__trigger-workspace-name");
            RegisterTriggerWorkspaceNameEditing(
                name,
                item.Entry.StableId,
                item.DisplayName);
            firstLine.Add(name);

            var enabled = new Toggle("启用")
            {
                name = "character-test-trigger-enabled",
                value = item.IsEnabled
            };
            enabled.AddToClassList(
                "mx-level-editor__trigger-workspace-enabled");
            enabled.RegisterValueChangedCallback(
                change =>
                {
                    if (SetTriggerEnabled(
                            item.Entry.StableId,
                            change.newValue))
                    {
                        RefreshTriggerInspectorAfterMutation();
                    }
                });
            firstLine.Add(enabled);

            var duplicate = new Button(
                () => DuplicateTriggerFromWorkspace(
                    item.Entry.StableId))
            {
                text = "复制"
            };
            duplicate.AddToClassList(
                "mx-level-editor__trigger-workspace-secondary");
            firstLine.Add(duplicate);

            var more = new ToolbarMenu
            {
                text = "更多"
            };
            more.AddToClassList(
                "mx-level-editor__trigger-workspace-more");
            more.menu.AppendAction(
                _showTriggerWorkspaceRuntimeInspector
                    ? "关闭运行时检查"
                    : "运行时检查",
                _ => SetTriggerWorkspaceRuntimeInspectorVisible(
                    !_showTriggerWorkspaceRuntimeInspector));
            more.menu.AppendSeparator();
            more.menu.AppendAction(
                "删除触发器...",
                _ => DeleteTriggerFromWorkspace(
                    item.Entry.StableId));
            firstLine.Add(more);
            root.Add(firstLine);

            var metadata = new Label(
                item.Category
                + " · "
                + item.Entry.StableId
                + " · "
                + item.RegionName);
            metadata.AddToClassList(
                "mx-level-editor__trigger-workspace-metadata");
            root.Add(metadata);
            return root;
        }

        private VisualElement CreateTriggerWorkspaceEventSection(
            TriggerWorkspaceItem item)
        {
            VisualElement group =
                CreateTriggerEditorGroup("事件");
            group.name = "character-test-trigger-event-section";
            if (CharacterTestLevelGameplayTriggerAuthoringDocument
                    .IsPlayerHpThresholdTemplate(item.Entry.TemplateId))
            {
                group.Add(CreateTriggerClauseRow(
                    CreateTriggerClauseWord("当"),
                    CreateTriggerClauseValue(
                        "当前玩家",
                        "由 CharacterTest Gameplay Attribute World 提供。"),
                    CreateTriggerClauseWord("的"),
                    CreateTriggerClauseValue("生命值"),
                    CreateTriggerClauseWord("进入低生命状态时")));
                return group;
            }

            bool presenceTemplate = string.Equals(
                    item.Entry.TemplateId,
                    CharacterTestLevelGameplayTriggerAuthoringDocument
                        .GameplayApplyRemoveBuffPresenceTemplateId,
                    StringComparison.Ordinal);
            group.Add(CreateTriggerClauseRow(
                CreateTriggerClauseWord("当"),
                CreateTriggerClauseValue("触发角色"),
                CreateTriggerClauseValue(
                    presenceTemplate ? "进入或停留" : "进入",
                    "事件范围由当前正式模板决定。"),
                CreateTriggerClauseValue(
                    item.RegionName,
                    "由 Combat occupancy 区域来源提供。")));
            if (presenceTemplate)
            {
                group.Add(CreateTriggerClauseRow(
                    CreateTriggerClauseWord("当"),
                    CreateTriggerClauseValue("该角色"),
                    CreateTriggerClauseValue(
                        "离开",
                        "事件范围由当前正式模板决定。"),
                    CreateTriggerClauseValue(
                        item.RegionName,
                        "由 Combat occupancy 区域来源提供。")));
            }
            return group;
        }

        private VisualElement CreateTriggerWorkspaceConditionSection(
            TriggerWorkspaceItem item)
        {
            VisualElement group =
                CreateTriggerEditorGroup("条件");
            group.name = "character-test-trigger-condition-section";
            if (CharacterTestLevelGameplayTriggerAuthoringDocument
                    .IsPlayerHpThresholdTemplate(item.Entry.TemplateId))
            {
                if (_triggerDocument.TryGetPlayerHpThresholds(
                        item.Entry,
                        out int enterThreshold,
                        out int exitThreshold))
                {
                    group.Add(CreatePlayerHpThresholdEditor(
                        item.Entry,
                        enterThreshold,
                        exitThreshold));
                }
                else
                {
                    group.Add(new HelpBox(
                        "当前玩家生命阈值无法解析，原值会保持不变。",
                        HelpBoxMessageType.Warning));
                }
                group.Add(CreateTriggerBooleanConditionEditor(
                    item.Entry));
                return group;
            }
            group.Add(CreateTriggerSubjectFilterEditor(
                item.Entry,
                sourceResolved: true,
                GameplayTriggerTargetSelector.Subject,
                string.Empty));
            group.Add(CreateTriggerBooleanConditionEditor(
                item.Entry));
            return group;
        }

        private VisualElement CreateTriggerWorkspaceEffectSummary(
            string sentence)
        {
            var root = new VisualElement();
            root.AddToClassList(
                "mx-level-editor__trigger-workspace-effect");
            var label = new Label(sentence)
            {
                name = "character-test-trigger-summary"
            };
            label.AddToClassList(
                "mx-level-editor__trigger-workspace-effect-text");
            root.Add(label);
            var copy = new Button(
                () => EditorGUIUtility.systemCopyBuffer =
                    sentence ?? string.Empty)
            {
                text = "复制",
                tooltip = "复制效果说明"
            };
            copy.AddToClassList(
                "mx-level-editor__trigger-workspace-effect-copy");
            root.Add(copy);
            return root;
        }

        private Foldout CreateTriggerWorkspaceAdvancedOptions(
            CharacterTestLevelGameplayTriggerEntry entry,
            GameplayTriggerStateScope stateScope,
            GameplayTriggerPresenceLimit presenceLimit,
            long cooldownFrames,
            int maxActivations,
            long payload0,
            long payload1,
            long payload2)
        {
            Foldout foldout =
                CreateGameplayTriggerAdvancedOptions(
                    entry,
                    cooldownFrames,
                    maxActivations,
                    payload0,
                    payload1,
                    payload2);
            if (!CharacterTestLevelGameplayTriggerAuthoringDocument
                    .IsPlayerHpThresholdTemplate(entry?.TemplateId))
            {
                foldout.Insert(
                    0,
                    CreateTriggerStrategyEditor(
                        entry,
                        stateScope,
                        presenceLimit,
                        cooldownFrames,
                        maxActivations));
            }
            return foldout;
        }

        private void AddTriggerFromWorkspace(
            string regionStableId,
            string templateId)
        {
            CharacterTestLevelGameplayTriggerEntry entry =
                AddTriggerTemplate(
                    regionStableId,
                    templateId);
            if (entry == null)
                return;
            _triggerWorkspaceRegionFilter =
                regionStableId;
            _selectedTriggerWorkspaceEntryId =
                entry.StableId;
            _triggerWorkspaceRequiresExplicitSelection = false;
            RefreshTriggerInspectorAfterMutation();
        }

        private void AddPlayerHpThresholdFromWorkspace()
        {
            CharacterTestLevelGameplayTriggerEntry entry =
                AddPlayerHpLowStoryTrigger();
            if (entry == null)
                return;
            _triggerWorkspaceRegionFilter = string.Empty;
            _selectedTriggerWorkspaceEntryId = entry.StableId;
            _triggerWorkspaceRequiresExplicitSelection = false;
            RefreshTriggerInspectorAfterMutation();
        }

        private void DuplicateTriggerFromWorkspace(
            string stableId)
        {
            if (!DuplicateTriggerEntry(
                    stableId,
                    out CharacterTestLevelGameplayTriggerEntry duplicate)
                || duplicate == null)
            {
                return;
            }

            _selectedTriggerWorkspaceEntryId =
                duplicate.StableId;
            _triggerWorkspaceRequiresExplicitSelection = false;
            RefreshTriggerInspectorAfterMutation();
        }

        private void DeleteTriggerFromWorkspace(
            string stableId)
        {
            if (!EditorUtility.DisplayDialog(
                    "删除玩法触发器",
                    "删除该触发器配置？场景区域会保留。",
                    "删除",
                    "取消"))
            {
                return;
            }
            if (!RemoveTriggerEntry(stableId))
                return;

            _selectedTriggerWorkspaceEntryId =
                string.Empty;
            _triggerWorkspaceRequiresExplicitSelection = false;
            RefreshTriggerInspectorAfterMutation();
        }

        private bool UpdateTriggerDisplayName(
            string stableId,
            string displayName)
        {
            if (_triggerDocument == null
                || string.IsNullOrWhiteSpace(displayName)
                || !_triggerDocument.TryFindEntry(
                    stableId,
                    out CharacterTestLevelGameplayTriggerEntry entry))
            {
                return false;
            }

            string normalized = displayName.Trim();
            if (string.Equals(
                    entry.DisplayName,
                    normalized,
                    StringComparison.Ordinal))
            {
                return true;
            }

            EnsureUndoSession();
            using (CharacterTestLevelEditorDocumentUndoSession.Transaction
                   transaction =
                   _undoSession.BeginTransaction(
                       "重命名 CharacterTest 关卡触发器"))
            {
                entry.DisplayName = normalized;
                MarkTriggerDirty();
                transaction.Commit();
            }
            return true;
        }

        internal bool UpdateTriggerDisplayNameForTests(
            string stableId,
            string displayName)
        {
            return UpdateTriggerDisplayName(
                stableId,
                displayName);
        }

        internal string GetTriggerDisplayNameForTests(
            string stableId)
        {
            return _triggerDocument != null
                && _triggerDocument.TryFindEntry(
                    stableId,
                    out CharacterTestLevelGameplayTriggerEntry entry)
                    ? entry.DisplayName
                    : string.Empty;
        }

        internal void SetTriggerWorkspaceWidthForTests(float width)
        {
            ApplyTriggerWorkspaceWidth(width);
        }

        internal bool TriggerWorkspaceNavigationCollapsedForTests =>
            _triggerWorkspaceNarrow
            && !_triggerWorkspaceNavigationOpen;

        internal string TriggerWorkspaceSelectedEntryIdForTests =>
            _selectedTriggerWorkspaceEntryId;

        internal float TriggerWorkspaceListScrollYForTests =>
            _triggerWorkspaceListScrollY;

        internal string TriggerWorkspaceRegionFilterForTests =>
            _triggerWorkspaceRegionFilter;

        internal bool TriggerWorkspaceRequiresExplicitSelectionForTests =>
            _triggerWorkspaceRequiresExplicitSelection;

        internal void SetTriggerWorkspaceListScrollYForTests(float value)
        {
            _triggerWorkspaceListScrollY = Mathf.Max(0f, value);
        }

        private void LocateTriggerWorkspaceRegion(
            TriggerWorkspaceItem item)
        {
            if (item?.Region == null)
                return;
            SelectTransform(item.Region.TransformRef);
            PingTransform(item.Region.TransformRef);
            FrameTransform(item.Region.TransformRef);
            SelectTriggerWorkspaceSceneContext(item);
        }

        private void SelectTriggerWorkspaceSceneContext(
            TriggerWorkspaceItem item)
        {
            if (item?.Region == null
                || _triggerWorkspaceWindow == null)
            {
                return;
            }

            _triggerWorkspaceWindow.SetWorkspaceSceneContext(
                "regions",
                CreateRegionRowKey(item.Region));
        }

        private void RefreshTriggerWorkspaceAfterMutation()
        {
            RebuildTriggerWorkspaceList();
            RebuildTriggerWorkspaceEditor(
                preserveViewState: true);
            _triggerWorkspaceWindow?.RefreshWorkspaceState();
            SceneView.RepaintAll();
        }

        private bool IsTriggerWorkspaceAttached()
        {
            return _triggerWorkspaceRoot != null
                && (_triggerWorkspaceRoot.panel != null
                    || _triggerWorkspaceWindow == null);
        }

        private VisualElement CreateGameplayTriggerLinkCard(
            LevelWorldObjectRegionDefinition region)
        {
            IReadOnlyList<CharacterTestLevelGameplayTriggerEntry> entries =
                _triggerDocument?.FindEntriesForRegion(region.StableId)
                ?? Array.Empty<CharacterTestLevelGameplayTriggerEntry>();
            var root = new VisualElement
            {
                name = "character-test-trigger-card"
            };
            root.AddToClassList(
                "mx-level-editor__trigger-link-card");

            var summary = new Label("关联触发器")
            {
                name = "character-test-trigger-link-title"
            };
            summary.AddToClassList(
                "mx-level-editor__trigger-link-summary");
            root.Add(summary);

            var count = new Label(entries.Count + " 个")
            {
                name = "character-test-trigger-count"
            };
            count.AddToClassList(
                "mx-level-editor__trigger-count-badge");
            root.Add(count);

            var open = new Button(
                () => OpenGameplayTriggerWorkspace(
                    region.StableId))
            {
                name = "character-test-trigger-open-workspace",
                text = entries.Count == 0
                    ? "打开玩法触发器"
                    : "编辑关联触发器"
            };
            open.AddToClassList(
                "mx-level-editor__trigger-link-open");
            root.Add(open);

            ToolbarMenu add =
                CreateTriggerWorkspaceAddMenu(region.StableId);
            add.name = "character-test-trigger-add";
            add.text = "新建关联触发器";
            add.AddToClassList(
                "mx-level-editor__trigger-link-add");
            root.Add(add);
            return root;
        }

        private void OpenGameplayTriggerWorkspace(
            string regionStableId)
        {
            PrepareGameplayTriggerWorkspaceRegion(regionStableId);

            MxLevelEditorWindow window =
                _activeContext?.Window
                ?? MxLevelEditorWindow.OpenWindow();
            window.TrySelectWorkspace(
                CharacterTestGameplayTriggerWorkspace
                    .WorkspaceIdValue);
        }

        private void PrepareGameplayTriggerWorkspaceRegion(
            string regionStableId)
        {
            _triggerWorkspaceRegionFilter =
                regionStableId ?? string.Empty;
            IReadOnlyList<CharacterTestLevelGameplayTriggerEntry> entries =
                _triggerDocument?.FindEntriesForRegion(
                    _triggerWorkspaceRegionFilter)
                ?? Array.Empty<CharacterTestLevelGameplayTriggerEntry>();
            if (entries.Count == 1)
            {
                _selectedTriggerWorkspaceEntryId =
                    entries[0].StableId;
                _triggerWorkspaceRequiresExplicitSelection = false;
            }
            else
            {
                _selectedTriggerWorkspaceEntryId =
                    string.Empty;
                _triggerWorkspaceRequiresExplicitSelection =
                    entries.Count > 1;
            }
        }

        internal void PrepareGameplayTriggerWorkspaceRegionForTests(
            string regionStableId)
        {
            PrepareGameplayTriggerWorkspaceRegion(regionStableId);
        }

        private void AddAndOpenGameplayTriggerWorkspace(
            string regionStableId,
            string templateId)
        {
            CharacterTestLevelGameplayTriggerEntry entry =
                AddTriggerTemplate(regionStableId, templateId);
            if (entry == null)
                return;
            _selectedTriggerWorkspaceEntryId =
                entry.StableId;
            _triggerWorkspaceRegionFilter =
                regionStableId;
            _triggerWorkspaceRequiresExplicitSelection = false;
            MxLevelEditorWindow window =
                _activeContext?.Window
                ?? MxLevelEditorWindow.OpenWindow();
            window.TrySelectWorkspace(
                CharacterTestGameplayTriggerWorkspace
                    .WorkspaceIdValue);
        }

        private bool BuildGameplayTriggerSceneContextMenu(
            Row row,
            GenericMenu menu)
        {
            if (row == null
                || menu == null
                || row.Kind != RowKind.Region
                || _document?.Regions == null
                || row.Index < 0
                || row.Index >= _document.Regions.Count)
            {
                return false;
            }

            LevelWorldObjectRegionDefinition region =
                _document.Regions[row.Index];
            if (region == null
                || !region.Enabled
                || region.RegionKind
                    != LevelWorldObjectRegionKind.TriggerRegion)
            {
                return false;
            }

            IReadOnlyList<CharacterTestLevelGameplayTriggerEntry> entries =
                _triggerDocument?.FindEntriesForRegion(region.StableId)
                ?? Array.Empty<CharacterTestLevelGameplayTriggerEntry>();
            menu.AddItem(
                new GUIContent(
                    entries.Count == 0
                        ? "打开玩法触发器"
                        : "编辑关联触发器 (" + entries.Count + ")"),
                false,
                () => OpenGameplayTriggerWorkspace(
                    region.StableId));
            menu.AddItem(
                new GUIContent(
                    "新建关联触发器/进入时施加 Buff"),
                false,
                () => AddAndOpenGameplayTriggerWorkspace(
                    region.StableId,
                    CharacterTestLevelGameplayTriggerAuthoringDocument
                        .GameplayApplyBuffEnterOnceTemplateId));
            menu.AddItem(
                new GUIContent(
                    "新建关联触发器/区域内维持 Buff"),
                false,
                () => AddAndOpenGameplayTriggerWorkspace(
                    region.StableId,
                    CharacterTestLevelGameplayTriggerAuthoringDocument
                        .GameplayApplyRemoveBuffPresenceTemplateId));
            menu.AddItem(
                new GUIContent(
                    "新建关联触发器/进入时触发 Story"),
                false,
                () => AddAndOpenGameplayTriggerWorkspace(
                    region.StableId,
                    CharacterTestLevelGameplayTriggerAuthoringDocument
                        .StoryRaiseTriggerEnterOnceTemplateId));
            return true;
        }

        internal int TriggerWorkspaceVisibleEntryCountForTests(
            string searchText,
            string category)
        {
            string previousSearch = _triggerWorkspaceSearchText;
            string previousCategory = _triggerWorkspaceCategory;
            _triggerWorkspaceSearchText =
                searchText ?? string.Empty;
            _triggerWorkspaceCategory =
                string.IsNullOrWhiteSpace(category)
                    ? TriggerWorkspaceAllCategory
                    : category;
            List<TriggerWorkspaceItem> items =
                BuildTriggerWorkspaceItems();
            int count = 0;
            for (int i = 0; i < items.Count; i++)
            {
                if (IsTriggerWorkspaceItemVisible(items[i]))
                    count++;
            }
            _triggerWorkspaceSearchText = previousSearch;
            _triggerWorkspaceCategory = previousCategory;
            return count;
        }
    }
}
