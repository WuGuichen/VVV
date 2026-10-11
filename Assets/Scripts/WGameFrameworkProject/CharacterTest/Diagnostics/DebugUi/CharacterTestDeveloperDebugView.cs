using System;
using System.Collections.Generic;
using MxFramework.DebugUI;
using UnityEngine;
using UnityEngine.UIElements;

namespace MxFramework.Game.CharacterTest
{
    public sealed class CharacterTestDeveloperDebugView
    {
        internal const string TargetLockGuidanceStableId = "target-lock";
        internal const string TargetUnlockGuidanceStableId = "target-unlock";
        public const string RootName = "character-test-developer-debug-panel";
        public const string CollapsedSummaryName = "character-test-developer-debug-collapsed-summary";
        public const string RefreshPauseButtonName = "character-test-developer-debug-refresh-pause";
        public const string AnchorButtonName = "character-test-developer-debug-anchor";
        public const string CursorButtonName = "character-test-developer-debug-cursor";
        public const string GuidanceLockButtonName = "character-test-developer-debug-guidance-lock";
        public const string GuidanceUnlockButtonName = "character-test-developer-debug-guidance-unlock";
        public const string MovablePotencyTargetFieldName =
            "character-test-developer-debug-movable-potency-target";
        public const string GizmoControlsName = "character-test-developer-debug-gizmo-controls";
        public const string GizmoRuntimeWorldToggleName = "character-test-gizmo-runtime-world";
        public const string GizmoHardRootToggleName = "character-test-gizmo-hard-root";
        public const string GizmoBodyPresenceToggleName = "character-test-gizmo-body-presence";
        public const string GizmoAnimatedHurtboxToggleName = "character-test-gizmo-animated-hurtbox";
        public const string GizmoActiveTraceToggleName = "character-test-gizmo-active-trace";
        public const string GizmoBodyPartContactsToggleName = "character-test-gizmo-body-part-contacts";

        private VisualElement _host;
        private VisualElement _root;
        private VisualElement _collapsed;
        private VisualElement _expanded;
        private VisualElement _tabs;
        private ScrollView _content;
        private Label _collapsedSummary;
        private VisualElement _collapsedStatusDot;
        private Label _title;
        private Label _subtitle;
        private Label _statusBadge;
        private Label _errorBadge;
        private Label _mouseBadge;
        private Button _refreshPauseButton;
        private Button _anchorButton;
        private Button _cursorButton;
        private Button _guidanceLockButton;
        private Button _guidanceUnlockButton;
        private IntegerField _movablePotencyTargetField;
        private int _movablePotencyTargetId;
        private Button[] _tabButtons = Array.Empty<Button>();
        private readonly Dictionary<CharacterTestGizmoDisplayOption, Toggle> _gizmoToggles =
            new Dictionary<CharacterTestGizmoDisplayOption, Toggle>();
        private CharacterTestDeveloperDebugPanelAnchor _anchor =
            CharacterTestDeveloperDebugPanelAnchor.TopRight;

        public event Action<int> TabSelected;
        public event Action RefreshRequested;
        public event Action ActionReloadRequested;
        public event Action<CharacterTestSurfaceProbeOperation> SurfaceProbeRequested;
        public event Action MovableNaturalObjectResetRequested;
        public event Action<int, CharacterTestGenesisPotencyInfluenceKind> MovableNaturalObjectPotencyInfluenceRequested;
        public event Action RefreshPauseToggled;
        public event Action AnchorCycleRequested;
        public event Action CursorToggleRequested;
        internal event Action<CharacterTestGizmoDisplayOption, bool> GizmoOptionChanged;
        internal event Action GizmoDefaultsRequested;
        internal event Action<string> GuidanceTestSelected;
        public event Action CollapseRequested;
        public event Action HideRequested;
        public event Action ExpandRequested;

        public bool IsBuiltFor(VisualElement host)
        {
            return _host == host && _root != null && _root.parent == host;
        }

        public void Build(VisualElement host)
        {
            if (host == null)
                throw new ArgumentNullException(nameof(host));

            _host = host;
            RemoveExistingRoot(host);

            _root = new VisualElement { name = RootName };
            CharacterTestDeveloperDebugViewStyles.StyleRoot(_root, _anchor);
            host.Add(_root);

            BuildCollapsed();
            BuildExpanded();
        }

        public void SetAnchor(CharacterTestDeveloperDebugPanelAnchor anchor)
        {
            _anchor = anchor;
            if (_root != null)
                CharacterTestDeveloperDebugViewStyles.ApplyAnchor(_root, _anchor);

            if (_anchorButton != null)
                _anchorButton.text = "Move: " + FormatAnchor(anchor);
        }

        public void SetVisibility(DebugUiVisibility visibility)
        {
            if (_root == null)
                return;

            _root.style.display = visibility == DebugUiVisibility.Hidden ? DisplayStyle.None : DisplayStyle.Flex;
            _collapsed.style.display = visibility == DebugUiVisibility.Collapsed ? DisplayStyle.Flex : DisplayStyle.None;
            _expanded.style.display = visibility == DebugUiVisibility.Expanded ? DisplayStyle.Flex : DisplayStyle.None;
        }

        public void Bind(CharacterTestDeveloperDebugSnapshot snapshot, int activeTab, DebugUiVisibility visibility)
        {
            if (_root == null)
                return;

            snapshot = snapshot ?? CharacterTestDeveloperDebugSnapshot.Unavailable(false);
            activeTab = CharacterTestDeveloperDebugSnapshot.ClampTabIndex(activeTab);
            SetVisibility(visibility);
            _collapsedSummary.text = snapshot.FormatCollapsedSummary(activeTab);
            UpdateCollapsedStatusDot(snapshot);
            _title.text = "CharacterTest Developer";
            _subtitle.text = snapshot.IsAvailable
                ? "Frame " + snapshot.Frame + "  ·  " + CharacterTestDeveloperDebugSnapshot.TabNames[activeTab]
                : "Runtime unavailable";
            SetBadge(
                _statusBadge,
                snapshot.RefreshPaused ? "PAUSED" : snapshot.IsAvailable ? "LIVE" : "OFFLINE",
                snapshot.RefreshPaused ? ToneWarning() : snapshot.IsAvailable ? TonePositive() : ToneWarning());
            SetBadge(
                _errorBadge,
                snapshot.IssueCount == 0 ? "OK" : snapshot.IssueCount + " ISSUES",
                snapshot.IssueCount == 0 ? TonePositive() : ToneDanger());
            SetBadge(
                _mouseBadge,
                snapshot.HasCameraBridge
                    ? snapshot.CursorCaptured ? "MOUSE CAPTURED" : "MOUSE FREE"
                    : "MOUSE N/A",
                snapshot.HasCameraBridge && snapshot.CursorCaptured ? ToneWarning() : TonePositive());
            if (_refreshPauseButton != null)
                _refreshPauseButton.text = snapshot.RefreshPaused ? "Resume Refresh" : "Pause Refresh";
            if (_anchorButton != null)
                _anchorButton.text = "Move: " + FormatAnchor(_anchor);
            if (_cursorButton != null)
                _cursorButton.text = snapshot.HasCameraBridge && snapshot.CursorCaptured ? "Free Mouse" : "Capture Mouse";
            StyleTabs(activeTab);
            RenderActiveTab(snapshot, activeTab);
        }

        internal void BindGuidanceSelection(
            bool available,
            CharacterTestGuidanceSelectionSnapshot snapshot)
        {
            if (_guidanceLockButton == null || _guidanceUnlockButton == null)
                return;

            _guidanceLockButton.SetEnabled(available);
            _guidanceUnlockButton.SetEnabled(available);
            bool lockSelected = string.Equals(
                snapshot.SelectedStableId,
                TargetLockGuidanceStableId,
                StringComparison.Ordinal);
            bool unlockSelected = string.Equals(
                snapshot.SelectedStableId,
                TargetUnlockGuidanceStableId,
                StringComparison.Ordinal);
            _guidanceLockButton.text = lockSelected
                ? "▶ 测试 1：锁定目标"
                : "测试 1：锁定目标";
            _guidanceUnlockButton.text = unlockSelected
                ? "▶ 测试 2：取消锁定"
                : "测试 2：取消锁定";
            CharacterTestDeveloperDebugViewStyles.StyleButton(
                _guidanceLockButton,
                lockSelected);
            CharacterTestDeveloperDebugViewStyles.StyleButton(
                _guidanceUnlockButton,
                unlockSelected);
        }

        internal void BindGizmoSettings(CharacterTestGizmoDisplaySettings settings)
        {
            foreach (KeyValuePair<CharacterTestGizmoDisplayOption, Toggle> pair in _gizmoToggles)
                pair.Value.SetValueWithoutNotify(settings.Get(pair.Key));
        }

        private void BuildCollapsed()
        {
            _collapsed = new VisualElement();
            CharacterTestDeveloperDebugViewStyles.StyleCollapsed(_collapsed);

            _collapsedStatusDot = new VisualElement();
            CharacterTestDeveloperDebugViewStyles.StyleStatusDot(_collapsedStatusDot, TonePositive());
            _collapsed.Add(_collapsedStatusDot);

            _collapsedSummary = CreateLabel("-");
            _collapsedSummary.name = CollapsedSummaryName;
            CharacterTestDeveloperDebugViewStyles.StyleCollapsedSummary(_collapsedSummary);
            _collapsed.Add(_collapsedSummary);

            _collapsed.Add(CreateButton("Expand", () => ExpandRequested?.Invoke(), true));
            _root.Add(_collapsed);
        }

        private void BuildExpanded()
        {
            _expanded = new VisualElement();
            CharacterTestDeveloperDebugViewStyles.StyleExpanded(_expanded);
            _root.Add(_expanded);

            VisualElement header = new VisualElement();
            CharacterTestDeveloperDebugViewStyles.StyleHeader(header);

            VisualElement titleBlock = new VisualElement();
            CharacterTestDeveloperDebugViewStyles.StyleTitleBlock(titleBlock);
            _title = CreateLabel("CharacterTest Developer");
            CharacterTestDeveloperDebugViewStyles.StyleTitle(_title);
            titleBlock.Add(_title);
            _subtitle = CreateLabel("Frame 0");
            CharacterTestDeveloperDebugViewStyles.StyleSubtitle(_subtitle);
            titleBlock.Add(_subtitle);
            header.Add(titleBlock);

            VisualElement badgeGroup = new VisualElement();
            CharacterTestDeveloperDebugViewStyles.StyleBadgeGroup(badgeGroup);
            _statusBadge = CreateBadge("LIVE", TonePositive());
            _errorBadge = CreateBadge("OK", TonePositive());
            _mouseBadge = CreateBadge("MOUSE N/A", ToneWarning());
            badgeGroup.Add(_statusBadge);
            badgeGroup.Add(_errorBadge);
            badgeGroup.Add(_mouseBadge);
            header.Add(badgeGroup);
            _expanded.Add(header);

            VisualElement toolbar = new VisualElement();
            CharacterTestDeveloperDebugViewStyles.StyleToolbar(toolbar);
            toolbar.Add(CreateButton("Refresh", () => RefreshRequested?.Invoke()));
            toolbar.Add(CreateButton("Reload Actions", () => ActionReloadRequested?.Invoke(), true));
            toolbar.Add(CreateButton(
                "Impulse",
                () => SurfaceProbeRequested?.Invoke(CharacterTestSurfaceProbeOperation.ApplyImpulse)));
            toolbar.Add(CreateButton(
                "Hit",
                () => SurfaceProbeRequested?.Invoke(CharacterTestSurfaceProbeOperation.ApplyHit)));
            toolbar.Add(CreateButton(
                "Push",
                () => SurfaceProbeRequested?.Invoke(CharacterTestSurfaceProbeOperation.ApplyPush)));
            toolbar.Add(CreateButton(
                "Reset",
                () => SurfaceProbeRequested?.Invoke(CharacterTestSurfaceProbeOperation.Reset)));
            toolbar.Add(CreateButton("Reset Objects", () => MovableNaturalObjectResetRequested?.Invoke(), true));
            _guidanceLockButton = CreateButton(
                "测试 1：锁定目标",
                () => GuidanceTestSelected?.Invoke(TargetLockGuidanceStableId));
            _guidanceLockButton.name = GuidanceLockButtonName;
            toolbar.Add(_guidanceLockButton);
            _guidanceUnlockButton = CreateButton(
                "测试 2：取消锁定",
                () => GuidanceTestSelected?.Invoke(TargetUnlockGuidanceStableId));
            _guidanceUnlockButton.name = GuidanceUnlockButtonName;
            toolbar.Add(_guidanceUnlockButton);
            _movablePotencyTargetField = CreateMovablePotencyTargetField();
            toolbar.Add(_movablePotencyTargetField);
            toolbar.Add(CreateButton(
                "Apply Fire",
                () => MovableNaturalObjectPotencyInfluenceRequested?.Invoke(
                    _movablePotencyTargetId,
                    CharacterTestGenesisPotencyInfluenceKind.Fire)));
            toolbar.Add(CreateButton(
                "Apply Hammer",
                () => MovableNaturalObjectPotencyInfluenceRequested?.Invoke(
                    _movablePotencyTargetId,
                    CharacterTestGenesisPotencyInfluenceKind.Hammer)));
            toolbar.Add(CreateButton(
                "Apply Condense",
                () => MovableNaturalObjectPotencyInfluenceRequested?.Invoke(
                    _movablePotencyTargetId,
                    CharacterTestGenesisPotencyInfluenceKind.Condense)));
            _refreshPauseButton = CreateButton("Pause Refresh", () => RefreshPauseToggled?.Invoke());
            _refreshPauseButton.name = RefreshPauseButtonName;
            toolbar.Add(_refreshPauseButton);
            _anchorButton = CreateButton("Move: " + FormatAnchor(_anchor), () => AnchorCycleRequested?.Invoke());
            _anchorButton.name = AnchorButtonName;
            toolbar.Add(_anchorButton);
            _cursorButton = CreateButton("Capture Mouse", () => CursorToggleRequested?.Invoke());
            _cursorButton.name = CursorButtonName;
            toolbar.Add(_cursorButton);
            toolbar.Add(CreateButton("Minimize", () => CollapseRequested?.Invoke()));
            toolbar.Add(CreateButton("Close", () => HideRequested?.Invoke()));
            _expanded.Add(toolbar);

            BuildGizmoControls();

            _tabs = new VisualElement();
            CharacterTestDeveloperDebugViewStyles.StyleTabsContainer(_tabs);
            _tabButtons = new Button[CharacterTestDeveloperDebugSnapshot.TabNames.Length];
            for (int i = 0; i < CharacterTestDeveloperDebugSnapshot.TabNames.Length; i++)
            {
                int tab = i;
                Button button = CreateButton(
                    CharacterTestDeveloperDebugSnapshot.TabNames[i],
                    () => TabSelected?.Invoke(tab));
                _tabButtons[i] = button;
                _tabs.Add(button);
            }

            _expanded.Add(_tabs);

            _content = new ScrollView(ScrollViewMode.Vertical);
            CharacterTestDeveloperDebugViewStyles.StyleContent(_content);
            _expanded.Add(_content);
        }

        private void BuildGizmoControls()
        {
            VisualElement controls = new VisualElement { name = GizmoControlsName };
            CharacterTestDeveloperDebugViewStyles.StyleGizmoControls(controls);

            Label title = CreateLabel("Gizmo 显示 · 自动保存");
            CharacterTestDeveloperDebugViewStyles.StyleGizmoTitle(title);
            controls.Add(title);

            VisualElement toggles = new VisualElement();
            CharacterTestDeveloperDebugViewStyles.StyleGizmoToggleRow(toggles);
            toggles.Add(CreateGizmoToggle("物理世界", GizmoRuntimeWorldToggleName,
                CharacterTestGizmoDisplayOption.RuntimePhysicsWorld));
            toggles.Add(CreateGizmoToggle("硬根", GizmoHardRootToggleName,
                CharacterTestGizmoDisplayOption.HardRoot));
            toggles.Add(CreateGizmoToggle("身体占位", GizmoBodyPresenceToggleName,
                CharacterTestGizmoDisplayOption.BodyPresence));
            toggles.Add(CreateGizmoToggle("动画受击体", GizmoAnimatedHurtboxToggleName,
                CharacterTestGizmoDisplayOption.AnimatedHurtbox));
            toggles.Add(CreateGizmoToggle("攻击轨迹", GizmoActiveTraceToggleName,
                CharacterTestGizmoDisplayOption.ActiveTrace));
            toggles.Add(CreateGizmoToggle("部位接触点", GizmoBodyPartContactsToggleName,
                CharacterTestGizmoDisplayOption.BodyPartContacts));
            toggles.Add(CreateButton("恢复默认", () => GizmoDefaultsRequested?.Invoke()));
            controls.Add(toggles);
            _expanded.Add(controls);
        }

        private Toggle CreateGizmoToggle(
            string label,
            string name,
            CharacterTestGizmoDisplayOption option)
        {
            var toggle = new Toggle(label) { name = name };
            CharacterTestDeveloperDebugViewStyles.StyleGizmoToggle(toggle);
            toggle.RegisterValueChangedCallback(evt => GizmoOptionChanged?.Invoke(option, evt.newValue));
            _gizmoToggles[option] = toggle;
            return toggle;
        }

        private void RenderActiveTab(CharacterTestDeveloperDebugSnapshot snapshot, int activeTab)
        {
            _content.Clear();
            CharacterTestDeveloperDebugTabSnapshot tab = ResolveTab(snapshot, activeTab);
            if (tab == null || tab.Cards.Count == 0)
            {
                VisualElement empty = new VisualElement();
                CharacterTestDeveloperDebugViewStyles.StyleEmptyState(empty);
                Label label = CreateLabel("No data for this tab.");
                CharacterTestDeveloperDebugViewStyles.StyleEmptyLabel(label);
                empty.Add(label);
                _content.Add(empty);
                return;
            }

            for (int i = 0; i < tab.Cards.Count; i++)
                _content.Add(CreateCard(tab.Cards[i]));
        }

        private static CharacterTestDeveloperDebugTabSnapshot ResolveTab(
            CharacterTestDeveloperDebugSnapshot snapshot,
            int activeTab)
        {
            if (snapshot.Tabs == null || snapshot.Tabs.Count == 0)
                return null;

            if (activeTab >= 0 && activeTab < snapshot.Tabs.Count)
                return snapshot.Tabs[activeTab];

            return snapshot.Tabs[0];
        }

        private VisualElement CreateCard(CharacterTestDeveloperDebugCard card)
        {
            VisualElement root = new VisualElement();
            CharacterTestDeveloperDebugViewStyles.StyleCard(root);

            VisualElement header = new VisualElement();
            CharacterTestDeveloperDebugViewStyles.StyleCardHeader(header);
            VisualElement accent = new VisualElement();
            CharacterTestDeveloperDebugViewStyles.StyleCardHeaderAccent(accent);
            header.Add(accent);
            Label title = CreateLabel(card.Title);
            CharacterTestDeveloperDebugViewStyles.StyleCardHeaderTitle(title);
            header.Add(title);
            root.Add(header);

            VisualElement body = new VisualElement();
            CharacterTestDeveloperDebugViewStyles.StyleCardBody(body);
            RenderCardBody(body, card);
            root.Add(body);
            return root;
        }

        private static void RenderCardBody(VisualElement body, CharacterTestDeveloperDebugCard card)
        {
            switch (card.Layout)
            {
                case CharacterTestDeveloperDebugCardLayout.MetricGrid:
                    RenderMetricGrid(body, card.Lines);
                    break;
                case CharacterTestDeveloperDebugCardLayout.StatusGrid:
                    RenderStatusGrid(body, card.Lines);
                    break;
                case CharacterTestDeveloperDebugCardLayout.FlowPipeline:
                    RenderFlowPipeline(body, card.Lines);
                    break;
                case CharacterTestDeveloperDebugCardLayout.Log:
                    RenderLogList(body, card.Lines);
                    break;
                case CharacterTestDeveloperDebugCardLayout.Entity:
                    RenderEntityCard(body, card.Lines);
                    break;
                case CharacterTestDeveloperDebugCardLayout.InputPanel:
                    RenderInputPanel(body, card.Lines);
                    break;
                default:
                    RenderList(body, card.Lines);
                    break;
            }
        }

        private static void RenderList(VisualElement body, IReadOnlyList<CharacterTestDeveloperDebugLine> lines)
        {
            for (int i = 0; i < lines.Count; i++)
                RenderLine(body, lines[i], i % 2 == 1);
        }

        private static void RenderLine(VisualElement body, CharacterTestDeveloperDebugLine line, bool alternateRow)
        {
            switch (line.Kind)
            {
                case CharacterTestDeveloperDebugLineKind.Metric:
                    AddMetricTile(body, line.Name, line.Value, line.Warning);
                    break;
                case CharacterTestDeveloperDebugLineKind.StatusChip:
                    AddStatusChip(body, line.Name, line.Value, line.Warning);
                    break;
                case CharacterTestDeveloperDebugLineKind.Bool:
                    AddBoolPill(body, line.Name, line.Flag);
                    break;
                case CharacterTestDeveloperDebugLineKind.Vector2:
                    AddVectorStick(body, line.Name, line.Number0, line.Number1, line.Value);
                    break;
                case CharacterTestDeveloperDebugLineKind.Progress:
                    AddProgressBar(body, line.Name, line.Number0, line.Number1, line.Warning);
                    break;
                case CharacterTestDeveloperDebugLineKind.Log:
                    AddLogEntry(body, line.Value, line.Warning);
                    break;
                case CharacterTestDeveloperDebugLineKind.FlowStep:
                    AddFlowStep(body, line.Name, line.Value, line.Flag);
                    break;
                case CharacterTestDeveloperDebugLineKind.Row:
                    AddRow(body, line.Name, line.Value, line.Warning, alternateRow);
                    break;
                default:
                    AddText(body, line.Value, line.Warning);
                    break;
            }
        }

        private static void RenderMetricGrid(VisualElement body, IReadOnlyList<CharacterTestDeveloperDebugLine> lines)
        {
            VisualElement grid = new VisualElement();
            CharacterTestDeveloperDebugViewStyles.StyleMetricGrid(grid);
            for (int i = 0; i < lines.Count; i++)
            {
                CharacterTestDeveloperDebugLine line = lines[i];
                if (line.Kind == CharacterTestDeveloperDebugLineKind.Text)
                    AddText(body, line.Value, line.Warning);
                else if (line.Kind == CharacterTestDeveloperDebugLineKind.StatusChip)
                    AddStatusChip(grid, line.Name, line.Value, line.Warning);
                else if (line.Kind == CharacterTestDeveloperDebugLineKind.Progress)
                    AddProgressBar(body, line.Name, line.Number0, line.Number1, line.Warning);
                else
                    AddMetricTile(grid, line.Name, line.Value, line.Warning);
            }

            if (grid.childCount > 0)
                body.Add(grid);
        }

        private static void RenderStatusGrid(VisualElement body, IReadOnlyList<CharacterTestDeveloperDebugLine> lines)
        {
            VisualElement grid = new VisualElement();
            CharacterTestDeveloperDebugViewStyles.StyleStatusGrid(grid);
            for (int i = 0; i < lines.Count; i++)
            {
                CharacterTestDeveloperDebugLine line = lines[i];
                if (line.Kind == CharacterTestDeveloperDebugLineKind.Row)
                    AddStatusChip(grid, line.Name, line.Value, line.Warning);
                else
                    RenderLine(grid, line, false);
            }

            body.Add(grid);
        }

        private static void RenderFlowPipeline(VisualElement body, IReadOnlyList<CharacterTestDeveloperDebugLine> lines)
        {
            VisualElement pipeline = new VisualElement();
            CharacterTestDeveloperDebugViewStyles.StyleFlowPipeline(pipeline);
            for (int i = 0; i < lines.Count; i++)
            {
                CharacterTestDeveloperDebugLine line = lines[i];
                if (i > 0)
                {
                    Label arrow = CreateLabel("→");
                    CharacterTestDeveloperDebugViewStyles.StyleFlowArrow(arrow);
                    pipeline.Add(arrow);
                }

                if (line.Kind == CharacterTestDeveloperDebugLineKind.FlowStep)
                    AddFlowStep(pipeline, line.Name, line.Value, line.Flag);
                else
                    RenderLine(pipeline, line, false);
            }

            body.Add(pipeline);
        }

        private static void RenderLogList(VisualElement body, IReadOnlyList<CharacterTestDeveloperDebugLine> lines)
        {
            VisualElement list = new VisualElement();
            CharacterTestDeveloperDebugViewStyles.StyleLogList(list);
            bool hasEntry = false;
            for (int i = 0; i < lines.Count; i++)
            {
                CharacterTestDeveloperDebugLine line = lines[i];
                if (line.Kind == CharacterTestDeveloperDebugLineKind.Text
                    && string.Equals(line.Value, "none", StringComparison.OrdinalIgnoreCase))
                {
                    AddText(list, "暂无记录", line.Warning);
                    hasEntry = true;
                    continue;
                }

                if (line.Kind == CharacterTestDeveloperDebugLineKind.Log
                    || line.Kind == CharacterTestDeveloperDebugLineKind.Text)
                {
                    AddLogEntry(list, line.Value, line.Warning);
                    hasEntry = true;
                }
                else
                {
                    RenderLine(list, line, false);
                    hasEntry = true;
                }
            }

            if (!hasEntry)
                AddText(list, "暂无记录", false);

            body.Add(list);
        }

        private static void RenderEntityCard(VisualElement body, IReadOnlyList<CharacterTestDeveloperDebugLine> lines)
        {
            VisualElement chipGrid = new VisualElement();
            CharacterTestDeveloperDebugViewStyles.StyleEntityGrid(chipGrid);
            VisualElement metricGrid = new VisualElement();
            CharacterTestDeveloperDebugViewStyles.StyleMetricGrid(metricGrid);
            VisualElement vectorGrid = new VisualElement();
            CharacterTestDeveloperDebugViewStyles.StyleVectorGrid(vectorGrid);

            for (int i = 0; i < lines.Count; i++)
            {
                CharacterTestDeveloperDebugLine line = lines[i];
                switch (line.Kind)
                {
                    case CharacterTestDeveloperDebugLineKind.StatusChip:
                        AddStatusChip(chipGrid, line.Name, line.Value, line.Warning);
                        break;
                    case CharacterTestDeveloperDebugLineKind.Metric:
                        AddMetricTile(metricGrid, line.Name, line.Value, line.Warning);
                        break;
                    case CharacterTestDeveloperDebugLineKind.Vector2:
                        AddVectorStick(vectorGrid, line.Name, line.Number0, line.Number1, line.Value);
                        break;
                    default:
                        AddText(body, line.Value, line.Warning);
                        break;
                }
            }

            if (chipGrid.childCount > 0)
                body.Add(chipGrid);
            if (metricGrid.childCount > 0)
                body.Add(metricGrid);
            if (vectorGrid.childCount > 0)
                body.Add(vectorGrid);
        }

        private static void RenderInputPanel(VisualElement body, IReadOnlyList<CharacterTestDeveloperDebugLine> lines)
        {
            VisualElement chipRow = new VisualElement();
            CharacterTestDeveloperDebugViewStyles.StyleStatusGrid(chipRow);
            VisualElement boolRow = new VisualElement();
            CharacterTestDeveloperDebugViewStyles.StyleBoolGrid(boolRow);
            VisualElement vectorRow = new VisualElement();
            CharacterTestDeveloperDebugViewStyles.StyleVectorGrid(vectorRow);

            for (int i = 0; i < lines.Count; i++)
            {
                CharacterTestDeveloperDebugLine line = lines[i];
                switch (line.Kind)
                {
                    case CharacterTestDeveloperDebugLineKind.StatusChip:
                        AddStatusChip(chipRow, line.Name, line.Value, line.Warning);
                        break;
                    case CharacterTestDeveloperDebugLineKind.Bool:
                        AddBoolPill(boolRow, line.Name, line.Flag);
                        break;
                    case CharacterTestDeveloperDebugLineKind.Vector2:
                        AddVectorStick(vectorRow, line.Name, line.Number0, line.Number1, line.Value);
                        break;
                    default:
                        RenderLine(body, line, false);
                        break;
                }
            }

            if (chipRow.childCount > 0)
                body.Add(chipRow);
            if (boolRow.childCount > 0)
                body.Add(boolRow);
            if (vectorRow.childCount > 0)
                body.Add(vectorRow);
        }

        private static void AddMetricTile(VisualElement parent, string label, string value, bool warning)
        {
            VisualElement tile = new VisualElement();
            CharacterTestDeveloperDebugViewStyles.StyleMetricTile(tile);
            Label name = CreateLabel(label);
            CharacterTestDeveloperDebugViewStyles.StyleMetricLabel(name);
            tile.Add(name);
            Label body = CreateLabel(string.IsNullOrEmpty(value) ? "—" : value);
            CharacterTestDeveloperDebugViewStyles.StyleMetricValue(body, warning);
            tile.Add(body);
            parent.Add(tile);
        }

        private static void AddStatusChip(VisualElement parent, string label, string value, bool warning)
        {
            VisualElement chip = new VisualElement();
            CharacterTestDeveloperDebugViewStyles.StyleStatusChip(chip, warning);
            VisualElement dot = new VisualElement();
            CharacterTestDeveloperDebugViewStyles.StyleChipDot(dot, warning);
            chip.Add(dot);
            Label name = CreateLabel(label + ":");
            CharacterTestDeveloperDebugViewStyles.StyleChipLabel(name, warning);
            chip.Add(name);
            Label body = CreateLabel(string.IsNullOrEmpty(value) ? "—" : value);
            CharacterTestDeveloperDebugViewStyles.StyleChipValue(body);
            chip.Add(body);
            parent.Add(chip);
        }

        private static void AddBoolPill(VisualElement parent, string label, bool on)
        {
            VisualElement pill = new VisualElement();
            CharacterTestDeveloperDebugViewStyles.StyleBoolPill(pill, on);
            Label name = CreateLabel(label);
            CharacterTestDeveloperDebugViewStyles.StyleBoolLabel(name);
            pill.Add(name);
            Label value = CreateLabel(on ? "ON" : "OFF");
            CharacterTestDeveloperDebugViewStyles.StyleBoolValue(value, on);
            pill.Add(value);
            parent.Add(pill);
        }

        private static void AddVectorStick(VisualElement parent, string label, float x, float y, string display)
        {
            VisualElement block = new VisualElement();
            CharacterTestDeveloperDebugViewStyles.StyleVectorBlock(block);
            Label name = CreateLabel(label);
            CharacterTestDeveloperDebugViewStyles.StyleVectorLabel(name);
            block.Add(name);

            VisualElement canvas = new VisualElement();
            CharacterTestDeveloperDebugViewStyles.StyleVectorCanvas(canvas);
            VisualElement h = new VisualElement();
            CharacterTestDeveloperDebugViewStyles.StyleVectorCrosshairH(h);
            canvas.Add(h);
            VisualElement v = new VisualElement();
            CharacterTestDeveloperDebugViewStyles.StyleVectorCrosshairV(v);
            canvas.Add(v);

            float clampedX = Mathf.Clamp(x, -1f, 1f);
            float clampedY = Mathf.Clamp(y, -1f, 1f);
            VisualElement dot = new VisualElement();
            CharacterTestDeveloperDebugViewStyles.StyleVectorDot(dot);
            dot.style.left = Length.Percent(50f + clampedX * 42f);
            dot.style.top = Length.Percent(50f - clampedY * 42f);
            canvas.Add(dot);
            block.Add(canvas);

            Label value = CreateLabel(display);
            CharacterTestDeveloperDebugViewStyles.StyleVectorValue(value);
            block.Add(value);
            parent.Add(block);
        }

        private static void AddProgressBar(VisualElement parent, string label, float current, float max, bool warning)
        {
            VisualElement block = new VisualElement();
            CharacterTestDeveloperDebugViewStyles.StyleProgressBlock(block);
            VisualElement header = new VisualElement();
            CharacterTestDeveloperDebugViewStyles.StyleProgressHeader(header);
            Label name = CreateLabel(label);
            name.style.color = ColorFromRgb(0x6E, 0x84, 0x9C);
            name.style.fontSize = 11f;
            header.Add(name);
            float safeMax = Mathf.Max(1f, max);
            float ratio = Mathf.Clamp01(current / safeMax);
            Label value = CreateLabel(current.ToString("F0") + " / " + safeMax.ToString("F0"));
            value.style.color = warning ? ToneWarning() : ColorFromRgb(0xD7, 0xE7, 0xF5);
            value.style.fontSize = 11f;
            header.Add(value);
            block.Add(header);

            VisualElement track = new VisualElement();
            CharacterTestDeveloperDebugViewStyles.StyleProgressTrack(track);
            VisualElement fill = new VisualElement();
            CharacterTestDeveloperDebugViewStyles.StyleProgressFill(fill, warning);
            fill.style.width = Length.Percent(ratio * 100f);
            track.Add(fill);
            block.Add(track);
            parent.Add(block);
        }

        private static void AddFlowStep(VisualElement parent, string step, string detail, bool ok)
        {
            VisualElement node = new VisualElement();
            CharacterTestDeveloperDebugViewStyles.StyleFlowStep(node, ok);
            Label title = CreateLabel(step);
            CharacterTestDeveloperDebugViewStyles.StyleFlowStepTitle(title, ok);
            node.Add(title);
            Label body = CreateLabel(string.IsNullOrEmpty(detail) ? "—" : detail);
            CharacterTestDeveloperDebugViewStyles.StyleFlowStepDetail(body);
            node.Add(body);
            parent.Add(node);
        }

        private static void AddLogEntry(VisualElement parent, string message, bool warning)
        {
            VisualElement entry = new VisualElement();
            CharacterTestDeveloperDebugViewStyles.StyleLogEntry(entry, warning);
            VisualElement bar = new VisualElement();
            CharacterTestDeveloperDebugViewStyles.StyleLogBar(bar, warning);
            entry.Add(bar);
            Label text = CreateLabel(string.IsNullOrEmpty(message) ? "—" : message);
            CharacterTestDeveloperDebugViewStyles.StyleLogText(text, warning);
            entry.Add(text);
            parent.Add(entry);
        }

        private static void AddRow(VisualElement card, string name, string value, bool warning, bool alternate)
        {
            VisualElement row = new VisualElement();
            CharacterTestDeveloperDebugViewStyles.StyleRow(row, alternate);

            Label key = CreateLabel(name);
            CharacterTestDeveloperDebugViewStyles.StyleRowKey(key);
            row.Add(key);

            Label body = CreateLabel(string.IsNullOrEmpty(value) ? "—" : value);
            CharacterTestDeveloperDebugViewStyles.StyleRowValue(body, warning);
            row.Add(body);
            card.Add(row);
        }

        private static void AddText(VisualElement card, string value, bool warning)
        {
            Label label = CreateLabel(string.IsNullOrEmpty(value) ? "—" : value);
            CharacterTestDeveloperDebugViewStyles.StyleTextLine(label, warning);
            card.Add(label);
        }

        private void StyleTabs(int activeTab)
        {
            for (int i = 0; i < _tabButtons.Length; i++)
                CharacterTestDeveloperDebugViewStyles.StyleTabButton(_tabButtons[i], i == activeTab);
        }

        private void UpdateCollapsedStatusDot(CharacterTestDeveloperDebugSnapshot snapshot)
        {
            if (_collapsedStatusDot == null)
                return;

            Color color = !snapshot.IsAvailable
                ? ToneWarning()
                : snapshot.IssueCount > 0
                    ? ToneDanger()
                    : snapshot.RefreshPaused
                        ? ToneWarning()
                        : TonePositive();
            _collapsedStatusDot.style.backgroundColor = color;
        }

        private static Label CreateLabel(string value)
        {
            var label = new Label(value);
            label.style.fontSize = 12f;
            label.style.whiteSpace = WhiteSpace.Normal;
            label.style.unityTextAlign = TextAnchor.UpperLeft;
            return label;
        }

        private static Button CreateButton(string text, Action clicked, bool hot = false)
        {
            var button = new Button(clicked) { text = text };
            CharacterTestDeveloperDebugViewStyles.StyleButton(button, hot);
            return button;
        }

        private IntegerField CreateMovablePotencyTargetField()
        {
            var field = new IntegerField("Object");
            field.name = MovablePotencyTargetFieldName;
            field.value = _movablePotencyTargetId;
            field.style.width = 104f;
            field.style.minWidth = 104f;
            field.style.marginLeft = 4f;
            field.style.marginRight = 4f;
            field.style.color = ColorFromRgb(0xD7, 0xE7, 0xF5);
            field.labelElement.style.color = ColorFromRgb(0xA9, 0xBC, 0xD2);
            field.RegisterValueChangedCallback(evt =>
            {
                int clamped = Math.Max(0, evt.newValue);
                _movablePotencyTargetId = clamped;
                if (evt.newValue != clamped)
                    field.SetValueWithoutNotify(clamped);
            });
            return field;
        }

        private static Label CreateBadge(string value, Color color)
        {
            Label badge = CreateLabel(value);
            CharacterTestDeveloperDebugViewStyles.StyleBadge(badge);
            SetBadge(badge, value, color);
            return badge;
        }

        private static void SetBadge(Label badge, string value, Color color)
        {
            if (badge == null)
                return;

            badge.text = value;
            badge.style.color = color;
            badge.style.backgroundColor = new Color(color.r * 0.2f, color.g * 0.2f, color.b * 0.2f, 0.95f);
        }

        private static string FormatAnchor(CharacterTestDeveloperDebugPanelAnchor anchor)
        {
            switch (anchor)
            {
                case CharacterTestDeveloperDebugPanelAnchor.TopLeft:
                    return "TL";
                case CharacterTestDeveloperDebugPanelAnchor.BottomRight:
                    return "BR";
                default:
                    return "TR";
            }
        }

        private static void RemoveExistingRoot(VisualElement host)
        {
            for (int i = host.childCount - 1; i >= 0; i--)
            {
                VisualElement child = host[i];
                if (string.Equals(child.name, RootName, StringComparison.Ordinal))
                    child.RemoveFromHierarchy();
            }
        }

        private static Color TonePositive()
        {
            return ColorFromRgb(0x99, 0xF6, 0xBF);
        }

        private static Color ToneWarning()
        {
            return ColorFromRgb(0xFE, 0xD7, 0x8B);
        }

        private static Color ToneDanger()
        {
            return ColorFromRgb(0xFF, 0xB4, 0xB4);
        }

        private static Color ColorFromRgb(byte r, byte g, byte b)
        {
            return new Color(r / 255f, g / 255f, b / 255f, 1f);
        }
    }
}
