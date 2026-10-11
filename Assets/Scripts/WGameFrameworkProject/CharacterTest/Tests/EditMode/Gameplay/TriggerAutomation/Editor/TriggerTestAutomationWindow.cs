using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using MxFramework.Diagnostics;
using MxFramework.Tests.Gameplay.TriggerAutomation.Scenarios.CharacterTest;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace MxFramework.Tests.Gameplay.TriggerAutomation
{
    /// <summary>
    /// Compact dockable runner for test-only Trigger scenarios.
    /// </summary>
    /// <remarks>
    /// Allocation classification: LowFreqAlloc. The window performs discovery,
    /// test execution, report IO and UI rebuilding only in explicit Editor
    /// interactions.
    /// </remarks>
    internal sealed class TriggerTestAutomationWindow : EditorWindow
    {
        private const string MenuPath =
            "MxFramework/Testing/Trigger Automation";
        private const string StylePath =
            "Assets/Scripts/WGameFrameworkProject/CharacterTest/Tests/EditMode/Gameplay/TriggerAutomation/" +
            "Editor/TriggerTestAutomationWindow.uss";
        private const string ManualScenarioTag = "manual";
        private static readonly Vector2 ExpandedMinimumSize =
            new Vector2(360f, 420f);
        private static readonly Vector2 CollapsedMinimumSize =
            new Vector2(220f, 56f);

        private TriggerTestManualExecutionService _executionService;
        private TriggerTestDiscoveryResult _discovery;
        private TriggerTestManualExecutionResult _lastExecution;
        private TriggerTestScenarioPicker _scenarioSelector;
        private IntegerField _historicalFrameField;
        private IntegerField _timelineEventField;
        private Button _runButton;
        private Button _rerunButton;
        private Button _collapseButton;
        private Label _connectionDot;
        private Label _profileHint;
        private Label _statusLabel;
        private Label _durationLabel;
        private VisualElement _expandedBody;
        private ScrollView _chainScroll;
        private VisualElement _chainHost;
        private Label _chainTitle;
        private Label _resultTitle;
        private Label _resultMessage;
        private Label _reportPaths;
        private Button _reportButton;
        private Button _copyButton;
        private bool _collapsed;
        private bool _isRunning;
        private bool _playModeStateSubscribed;

        [MenuItem(MenuPath, priority = 240)]
        private static void Open()
        {
            TriggerTestAutomationWindow window =
                GetWindow<TriggerTestAutomationWindow>();
            window.titleContent = new GUIContent("Trigger 测试");
            window.minSize = ExpandedMinimumSize;
            window.Show();
        }

        private void OnEnable()
        {
            if (_playModeStateSubscribed)
                return;

            EditorApplication.playModeStateChanged +=
                OnPlayModeStateChanged;
            TriggerTestRuntimeAiLabEnterPlayModeWorkflow.StateChanged +=
                OnEditorWorkflowStateChanged;
            _playModeStateSubscribed = true;
        }

        private void OnDisable()
        {
            if (!_playModeStateSubscribed)
                return;

            EditorApplication.playModeStateChanged -=
                OnPlayModeStateChanged;
            TriggerTestRuntimeAiLabEnterPlayModeWorkflow.StateChanged -=
                OnEditorWorkflowStateChanged;
            _playModeStateSubscribed = false;
        }

        public void CreateGUI()
        {
            _executionService = new TriggerTestManualExecutionService(
                typeof(TriggerTestAutomationWindow).Assembly);

            VisualElement root = rootVisualElement;
            root.Clear();
            root.name = "trigger-test-automation-root";
            root.AddToClassList("trigger-test-window");
            StyleSheet style =
                AssetDatabase.LoadAssetAtPath<StyleSheet>(StylePath);
            if (style != null)
                root.styleSheets.Add(style);

            root.Add(BuildHeader());
            _expandedBody = BuildExpandedBody();
            root.Add(_expandedBody);
            SetCollapsed(false);
            RefreshScenarios();
            RenderEditorWorkflowIfSelected();
        }

        internal void RefreshScenariosForTests() => RefreshScenarios();

        internal void SelectScenarioForTests(string scenarioId) =>
            SelectScenario(scenarioId);

        internal void RunSelectedForTests() => RunSelected();

        internal void OpenScenarioPickerForTests() =>
            _scenarioSelector?.Open();

        internal void SetScenarioSearchForTests(string search) =>
            _scenarioSelector?.SetSearchForTests(search);

        internal void SetScenarioFilterForTests(string filter) =>
            _scenarioSelector?.SetFilterForTests(filter);

        internal void SetHistoricalFrameForTests(int frame)
        {
            if (_historicalFrameField != null)
            {
                _historicalFrameField.SetValueWithoutNotify(frame);
                ApplyHistoricalFrame(frame);
            }
        }

        internal void SetTimelineEventForTests(int eventIndex)
        {
            if (_timelineEventField != null)
            {
                _timelineEventField.SetValueWithoutNotify(eventIndex);
                ApplyTimelineEvent(eventIndex);
            }
        }

        private VisualElement BuildHeader()
        {
            var header = new VisualElement
            {
                name = "trigger-test-header"
            };
            header.AddToClassList("trigger-test-header");

            var title = new Label("Trigger 测试")
            {
                name = "trigger-test-title"
            };
            title.AddToClassList("trigger-test-title");
            header.Add(title);

            _connectionDot = new Label("●")
            {
                name = "trigger-test-connection-dot",
                tooltip = "场景索引状态"
            };
            _connectionDot.AddToClassList("trigger-test-connection-dot");
            header.Add(_connectionDot);

            _collapseButton = new Button(ToggleCollapsed)
            {
                name = "trigger-test-collapse",
                text = "⌃",
                tooltip = "折叠面板"
            };
            _collapseButton.AddToClassList("trigger-test-icon-button");
            header.Add(_collapseButton);

            var more = new ToolbarMenu
            {
                name = "trigger-test-more",
                text = "⋮",
                tooltip = "更多操作"
            };
            more.AddToClassList("trigger-test-more");
            more.menu.AppendAction(
                "刷新场景",
                _ => RefreshScenarios(),
                DropdownMenuAction.AlwaysEnabled);
            more.menu.AppendSeparator();
            more.menu.AppendAction(
                "打开报告目录",
                _ => RevealReportRoot(),
                DropdownMenuAction.AlwaysEnabled);
            header.Add(more);
            return header;
        }

        private VisualElement BuildExpandedBody()
        {
            var body = new VisualElement
            {
                name = "trigger-test-expanded-body"
            };
            body.AddToClassList("trigger-test-expanded-body");

            var scenarioRow = new VisualElement
            {
                name = "trigger-test-scenario-row"
            };
            scenarioRow.AddToClassList("trigger-test-scenario-row");
            _scenarioSelector = new TriggerTestScenarioPicker();
            _scenarioSelector.SelectionChanged +=
                _ => OnScenarioSelectionChanged();
            scenarioRow.Add(_scenarioSelector);

            _runButton = new Button(RunSelected)
            {
                name = "trigger-test-run-selected",
                text = "▶",
                tooltip = "运行选中场景"
            };
            _runButton.AddToClassList("trigger-test-run-button");
            scenarioRow.Add(_runButton);
            body.Add(scenarioRow);

            _historicalFrameField = new IntegerField("目标帧")
            {
                name = "trigger-test-historical-frame",
                value = 0,
                tooltip = "输入当前时间轴保留窗口内的历史帧"
            };
            _historicalFrameField.AddToClassList(
                "trigger-test-historical-frame");
            _historicalFrameField.RegisterValueChangedCallback(
                evt => ApplyHistoricalFrame(evt.newValue));
            body.Add(_historicalFrameField);

            _timelineEventField = new IntegerField("事件序号")
            {
                name = "trigger-test-timeline-event",
                value = 0,
                tooltip = "输入当前时间轴可选择事件的序号"
            };
            _timelineEventField.AddToClassList(
                "trigger-test-timeline-event");
            _timelineEventField.RegisterValueChangedCallback(
                evt => ApplyTimelineEvent(evt.newValue));
            body.Add(_timelineEventField);

            _profileHint = new Label
            {
                name = "trigger-test-profile-hint"
            };
            _profileHint.AddToClassList("trigger-test-profile-hint");
            body.Add(_profileHint);

            var statusStrip = new VisualElement
            {
                name = "trigger-test-status-strip"
            };
            statusStrip.AddToClassList("trigger-test-status-strip");
            _statusLabel = new Label("尚未运行")
            {
                name = "trigger-test-status"
            };
            _statusLabel.AddToClassList("trigger-test-status");
            statusStrip.Add(_statusLabel);
            _durationLabel = new Label("—")
            {
                name = "trigger-test-duration"
            };
            _durationLabel.AddToClassList("trigger-test-duration");
            statusStrip.Add(_durationLabel);
            _rerunButton = new Button(RunSelected)
            {
                name = "trigger-test-rerun",
                text = "↻",
                tooltip = "重新运行选中场景"
            };
            _rerunButton.AddToClassList("trigger-test-icon-button");
            statusStrip.Add(_rerunButton);
            body.Add(statusStrip);

            _chainTitle = new Label("执行前说明")
            {
                name = "trigger-test-chain-title"
            };
            _chainTitle.AddToClassList("trigger-test-section-title");
            body.Add(_chainTitle);

            _chainScroll = new ScrollView(ScrollViewMode.Vertical)
            {
                name = "trigger-test-chain-scroll"
            };
            _chainScroll.AddToClassList("trigger-test-chain-scroll");
            _chainHost = new VisualElement
            {
                name = "trigger-test-chain"
            };
            _chainHost.AddToClassList("trigger-test-chain");
            _chainScroll.Add(_chainHost);
            body.Add(_chainScroll);

            var resultSection = new VisualElement
            {
                name = "trigger-test-result-section"
            };
            resultSection.AddToClassList("trigger-test-result-section");
            var resultHeading = new Label("结果");
            resultHeading.AddToClassList("trigger-test-section-title");
            resultSection.Add(resultHeading);
            _resultTitle = new Label("选择场景后运行")
            {
                name = "trigger-test-result-title"
            };
            _resultTitle.AddToClassList("trigger-test-result-title");
            resultSection.Add(_resultTitle);
            _resultMessage = new Label("逐操作日志和独立断言会显示在这里。")
            {
                name = "trigger-test-result-message"
            };
            _resultMessage.AddToClassList("trigger-test-result-message");
            resultSection.Add(_resultMessage);
            body.Add(resultSection);

            var footer = new VisualElement
            {
                name = "trigger-test-footer"
            };
            footer.AddToClassList("trigger-test-footer");
            _reportButton = new Button(RevealSelectedReport)
            {
                name = "trigger-test-open-report",
                text = "▤  报告",
                tooltip = "在文件管理器中定位最新报告"
            };
            _reportButton.AddToClassList("trigger-test-footer-button");
            footer.Add(_reportButton);
            _copyButton = new Button(CopySelectedSummary)
            {
                name = "trigger-test-copy-summary",
                text = "▣",
                tooltip = "复制运行摘要"
            };
            _copyButton.AddToClassList("trigger-test-icon-button");
            footer.Add(_copyButton);
            _reportPaths = new Label("latest.json · latest.md")
            {
                name = "trigger-test-report-paths"
            };
            _reportPaths.AddToClassList("trigger-test-report-paths");
            footer.Add(_reportPaths);
            body.Add(footer);
            return body;
        }

        private void RefreshScenarios()
        {
            if (_executionService == null)
            {
                _executionService = new TriggerTestManualExecutionService(
                    typeof(TriggerTestAutomationWindow).Assembly);
            }

            string selectedId = GetSelectedScenarioId();
            _discovery = FilterManualScenarios(
                _executionService.Discover());
            int selectedIndex = FindScenarioIndex(selectedId);
            if (selectedIndex < 0 && _discovery.Scenarios.Count > 0)
                selectedIndex = 0;
            selectedId = selectedIndex < 0
                ? string.Empty
                : _discovery.Scenarios[selectedIndex]
                    .Descriptor
                    .ScenarioId
                    .Value;
            RefreshScenarioPickerItems(selectedId);
            _connectionDot.EnableInClassList(
                "is-error",
                !_discovery.Succeeded);
            _connectionDot.tooltip = _discovery.Succeeded
                ? _discovery.Scenarios.Count + " 个场景已发现"
                : GetDiscoveryError();
            OnScenarioSelectionChanged();
        }

        private void RefreshScenarioPickerItems(string selectedId)
        {
            if (_scenarioSelector == null)
                return;

            int count = _discovery?.Scenarios.Count ?? 0;
            var items =
                new List<TriggerTestScenarioPickerItem>(count);
            for (int i = 0; i < count; i++)
            {
                TriggerTestDescriptor descriptor =
                    _discovery.Scenarios[i].Descriptor;
                TriggerTestManualScenarioDisplay display =
                    TriggerTestManualScenarioDisplayMap.Resolve(descriptor);
                bool canRun = TryGetScenarioRunState(
                    descriptor,
                    out string unavailableReason);
                items.Add(new TriggerTestScenarioPickerItem(
                    descriptor.ScenarioId.Value,
                    display.DisplayName,
                    display.GroupName,
                    descriptor.ExecutionProfile,
                    canRun,
                    unavailableReason));
            }

            _scenarioSelector.SetItems(items, selectedId);
        }

        private bool TryGetScenarioRunState(
            TriggerTestDescriptor descriptor,
            out string unavailableReason)
        {
            if (_discovery == null || !_discovery.Succeeded)
            {
                unavailableReason = GetDiscoveryError();
                return false;
            }
            if (_executionService == null)
            {
                unavailableReason = "Trigger 执行服务尚未初始化。";
                return false;
            }
            if (string.Equals(
                    descriptor.ScenarioId.Value,
                    RuntimeAiLabEnterPlayModeScenario.ScenarioId,
                    StringComparison.Ordinal) &&
                TriggerTestRuntimeAiLabEnterPlayModeWorkflow.IsRunning)
            {
                unavailableReason = "进入 Play Mode Trigger 正在执行。";
                return false;
            }

            return _executionService.CanRun(
                descriptor,
                out unavailableReason);
        }

        private static TriggerTestDiscoveryResult FilterManualScenarios(
            TriggerTestDiscoveryResult discovery)
        {
            if (discovery == null)
            {
                return new TriggerTestDiscoveryResult(
                    Array.Empty<ITriggerTestScenario>(),
                    Array.Empty<TriggerTestDiscoveryError>());
            }

            var scenarios = new List<ITriggerTestScenario>();
            for (int scenarioIndex = 0;
                 scenarioIndex < discovery.Scenarios.Count;
                 scenarioIndex++)
            {
                ITriggerTestScenario scenario =
                    discovery.Scenarios[scenarioIndex];
                IReadOnlyList<string> tags = scenario.Descriptor.Tags;
                for (int tagIndex = 0; tagIndex < tags.Count; tagIndex++)
                {
                    if (!string.Equals(
                            tags[tagIndex],
                            ManualScenarioTag,
                            StringComparison.Ordinal))
                    {
                        continue;
                    }

                    scenarios.Add(scenario);
                    break;
                }
            }

            var errors =
                new TriggerTestDiscoveryError[discovery.Errors.Count];
            for (int errorIndex = 0;
                 errorIndex < discovery.Errors.Count;
                 errorIndex++)
            {
                errors[errorIndex] = discovery.Errors[errorIndex];
            }

            return new TriggerTestDiscoveryResult(
                SortManualScenarios(scenarios),
                errors);
        }

        private static ITriggerTestScenario[] SortManualScenarios(
            List<ITriggerTestScenario> scenarios)
        {
            scenarios.Sort((left, right) =>
            {
                TriggerTestManualScenarioDisplay leftDisplay =
                    TriggerTestManualScenarioDisplayMap.Resolve(
                        left.Descriptor);
                TriggerTestManualScenarioDisplay rightDisplay =
                    TriggerTestManualScenarioDisplayMap.Resolve(
                        right.Descriptor);
                int order = leftDisplay.Order.CompareTo(rightDisplay.Order);
                return order != 0
                    ? order
                    : left.Descriptor.ScenarioId.CompareTo(
                        right.Descriptor.ScenarioId);
            });
            return scenarios.ToArray();
        }

        private void OnScenarioSelectionChanged()
        {
            RefreshSelectionState();
            if (RenderEditorWorkflowIfSelected())
                return;

            TriggerTestRunResult result = _lastExecution?.FindResult(
                GetSelectedScenarioId());
            if (result != null)
                RenderRunResult(result, _lastExecution);
            else
                RenderEmptyState();
        }

        private void RefreshSelectionState()
        {
            ITriggerTestScenario scenario = GetSelectedScenario();
            bool acceptsHistoricalFrame =
                scenario is ITriggerTestRuntimeAiLabHistoricalFrameScenario;
            if (_historicalFrameField != null)
            {
                _historicalFrameField.style.display =
                    acceptsHistoricalFrame
                        ? DisplayStyle.Flex
                        : DisplayStyle.None;
                if (scenario is
                    ITriggerTestRuntimeAiLabHistoricalFrameScenario target)
                {
                    _historicalFrameField.SetValueWithoutNotify(
                        target.TargetFrame);
                }
            }
            bool acceptsTimelineEvent =
                scenario is ITriggerTestRuntimeAiLabTimelineEventScenario;
            if (_timelineEventField != null)
            {
                _timelineEventField.style.display =
                    acceptsTimelineEvent
                        ? DisplayStyle.Flex
                        : DisplayStyle.None;
                if (scenario is
                    ITriggerTestRuntimeAiLabTimelineEventScenario target)
                {
                    _timelineEventField.SetValueWithoutNotify(
                        target.TargetEventIndex);
                }
            }

            bool canRun = CanRun();
            RefreshScenarioPickerItems(GetSelectedScenarioId());
            _runButton?.SetEnabled(canRun);
            _rerunButton?.SetEnabled(canRun);
            _reportButton?.SetEnabled(HasSelectedReport());
            _copyButton?.SetEnabled(
                _lastExecution?.FindResult(GetSelectedScenarioId()) != null ||
                IsEnterPlayModeSelected() &&
                TriggerTestRuntimeAiLabEnterPlayModeWorkflow
                    .CaptureSnapshot()
                    .HasState);

            TriggerTestDescriptor descriptor = GetSelectedDescriptor();
            if (descriptor == null)
            {
                _scenarioSelector.tooltip = GetDiscoveryError();
                _runButton.tooltip = "没有可运行的 Trigger Scenario";
            }
            else
            {
                string displayName =
                    TriggerTestManualScenarioDisplayMap
                        .Resolve(descriptor)
                        .DisplayName;
                _scenarioSelector.tooltip =
                    displayName +
                    "\n" +
                    descriptor.ScenarioId.Value;
                _runButton.tooltip = canRun
                    ? "运行：" + displayName
                    : GetUnavailableRunTooltip(descriptor);
            }
            _profileHint.text = GetProfileHint(descriptor);
        }

        private void RunSelected()
        {
            string scenarioId = GetSelectedScenarioId();
            if (_isRunning || string.IsNullOrEmpty(scenarioId))
                return;

            _scenarioSelector?.Close();
            SetRunning(true);
            try
            {
                ITriggerTestScenario scenario = GetSelectedScenario();
                _lastExecution = _executionService.Run(scenario);
                TriggerTestRunResult result =
                    _lastExecution.FindResult(scenarioId);
                if (result != null)
                    RenderRunResult(result, _lastExecution);
                else
                    RenderExecutionError(_lastExecution.ErrorMessage);
            }
            finally
            {
                SetRunning(false);
                RenderEditorWorkflowIfSelected();
            }
        }

        private void RenderRunResult(
            TriggerTestRunResult selected,
            TriggerTestManualExecutionResult execution)
        {
            TriggerTestRunStatus aggregateStatus =
                execution.FailedCount > 0
                    ? TriggerTestRunStatus.Failed
                    : execution.PendingCount > 0
                        ? TriggerTestRunStatus.PendingExternalEvidence
                        : TriggerTestRunStatus.Passed;
            SetStatusClass(aggregateStatus);
            _chainTitle.text = "执行链";
            int total = execution.Results.Count;
            int completed = execution.PassedCount + execution.PendingCount;
            _statusLabel.text =
                aggregateStatus == TriggerTestRunStatus.Failed
                ? "失败  " + execution.FailedCount + " / " + total
                : aggregateStatus ==
                  TriggerTestRunStatus.PendingExternalEvidence
                    ? "待外部验证  " + completed + " / " + total
                    : "通过  " + execution.PassedCount + " / " + total;
            _durationLabel.text =
                execution.Elapsed.TotalSeconds.ToString(
                    "0.00",
                    CultureInfo.InvariantCulture) +
                " s";

            _chainHost.Clear();
            IReadOnlyList<TriggerTestManualOperationViewModel> operations =
                TriggerTestManualOperationProjector.Project(
                    selected.SimulationResult);
            for (int i = 0; i < operations.Count; i++)
                _chainHost.Add(BuildOperationRow(operations[i], i));

            if (operations.Count == 0)
            {
                var empty = new Label("没有操作日志。");
                empty.AddToClassList("trigger-test-empty");
                _chainHost.Add(empty);
            }

            SetResultText(selected, operations);
            RefreshSelectionState();
        }

        private VisualElement BuildOperationRow(
            TriggerTestManualOperationViewModel operation,
            int index) =>
            BuildOperationRow(
                operation,
                index,
                operation.Succeeded ? "✓" : "×",
                !operation.Succeeded);

        private VisualElement BuildOperationRow(
            TriggerTestManualOperationViewModel operation,
            int index,
            string statusText,
            bool failed)
        {
            var row = new VisualElement
            {
                name = "trigger-test-operation-" + index,
                tooltip =
                    "帧 " +
                    operation.Frame +
                    " · 技术标识：" +
                    operation.TechnicalId +
                    (string.IsNullOrEmpty(operation.TechnicalDetails)
                        ? string.Empty
                        : "\n" + operation.TechnicalDetails)
            };
            row.AddToClassList("trigger-test-operation");
            row.AddToClassList(GetStageClass(operation.Stage));
            row.EnableInClassList("is-failed", failed);

            var connector = new Label(index == 0 ? "●" : "│\n●");
            connector.AddToClassList("trigger-test-operation-connector");
            row.Add(connector);

            var copy = new VisualElement();
            copy.AddToClassList("trigger-test-operation-copy");
            var heading = new Label(
                operation.Stage + " · " + operation.Operation);
            heading.AddToClassList("trigger-test-operation-heading");
            copy.Add(heading);
            if (!string.IsNullOrEmpty(operation.Result))
            {
                var result = new Label(operation.Result);
                result.AddToClassList("trigger-test-operation-result");
                copy.Add(result);
            }
            row.Add(copy);

            var status = new Label(statusText);
            status.AddToClassList("trigger-test-operation-status");
            row.Add(status);
            return row;
        }

        private bool RenderEditorWorkflowIfSelected()
        {
            if (!IsEnterPlayModeSelected() ||
                _chainHost == null)
            {
                return false;
            }

            TriggerTestRuntimeAiLabPlayModeSnapshot snapshot =
                TriggerTestRuntimeAiLabEnterPlayModeWorkflow.CaptureSnapshot();
            if (!snapshot.HasState)
                return false;

            TriggerTestRunStatus status =
                snapshot.Status == TriggerTestEditorWorkflowStatus.Passed
                    ? TriggerTestRunStatus.Passed
                    : snapshot.Status ==
                      TriggerTestEditorWorkflowStatus.Failed
                        ? TriggerTestRunStatus.Failed
                        : TriggerTestRunStatus.PendingExternalEvidence;
            SetStatusClass(status);
            _chainTitle.text = "执行链";
            _statusLabel.text =
                snapshot.Status == TriggerTestEditorWorkflowStatus.Passed
                    ? "通过  1 / 1"
                    : snapshot.Status ==
                      TriggerTestEditorWorkflowStatus.Failed
                        ? "失败  1 / 1"
                        : "运行中…";
            _durationLabel.text =
                snapshot.ElapsedSeconds.ToString(
                    "0.00",
                    CultureInfo.InvariantCulture) +
                " s";

            _chainHost.Clear();
            var operation =
                new TriggerTestManualOperationViewModel(
                    0L,
                    0,
                    "Editor",
                    "进入 Play Mode",
                    snapshot.Message,
                    RuntimeAiLabEnterPlayModeScenario.ScenarioId,
                    snapshot.Status !=
                    TriggerTestEditorWorkflowStatus.Failed);
            string glyph =
                snapshot.Status == TriggerTestEditorWorkflowStatus.Passed
                    ? "✓"
                    : snapshot.Status ==
                      TriggerTestEditorWorkflowStatus.Failed
                        ? "×"
                        : "…";
            _chainHost.Add(
                BuildOperationRow(
                    operation,
                    0,
                    glyph,
                    snapshot.Status ==
                    TriggerTestEditorWorkflowStatus.Failed));

            _resultTitle.text =
                snapshot.Status == TriggerTestEditorWorkflowStatus.Passed
                    ? "Play Mode 已就绪"
                    : snapshot.Status ==
                      TriggerTestEditorWorkflowStatus.Failed
                        ? "进入 Play Mode 失败"
                        : "等待 Unity 完成切换";
            _resultMessage.text = snapshot.Message;
            RefreshSelectionState();
            return true;
        }

        private void SetResultText(
            TriggerTestRunResult selected,
            IReadOnlyList<TriggerTestManualOperationViewModel> operations)
        {
            if (selected.SimulationResult.Failures.Count > 0)
            {
                FrameworkSimulationFailure failure =
                    selected.SimulationResult.Failures[0];
                _resultTitle.text = "执行失败";
                _resultMessage.text = failure.Message;
                return;
            }

            if (selected.Status ==
                TriggerTestRunStatus.PendingExternalEvidence)
            {
                _resultTitle.text = "内部校验通过";
                _resultMessage.text =
                    "仍有 " +
                    selected.ExternalChecks.Count +
                    " 项外部表现证据待完成。";
                return;
            }

            _resultTitle.text = "全部内部校验通过";
            _resultMessage.text = operations.Count == 0
                ? "场景未产生操作日志。"
                : operations[operations.Count - 1].Result;
        }

        private void RenderEmptyState()
        {
            SetStatusClass(null);
            _chainTitle.text = "Trigger 定义";
            _statusLabel.text = "尚未运行";
            _durationLabel.text = "—";
            _chainHost.Clear();
            ITriggerTestScenario scenario = GetSelectedScenario();
            if (scenario == null)
            {
                var empty = new Label(GetDiscoveryError());
                empty.AddToClassList("trigger-test-empty");
                _chainHost.Add(empty);
                _resultTitle.text = "没有可运行场景";
                _resultMessage.text = GetProfileHint(null);
                RefreshSelectionState();
                return;
            }

            try
            {
                SynchronizeScenarioParameters(scenario);
                IReadOnlyList<TriggerTestManualOperationViewModel> operations =
                    TriggerTestManualPlanProjector.Project(scenario);
                for (int i = 0; i < operations.Count; i++)
                {
                    _chainHost.Add(
                        BuildOperationRow(
                            operations[i],
                            i,
                            "○",
                            false));
                }

                if (operations.Count == 0)
                {
                    var empty = new Label("测试 Trigger 没有可展示的固定定义。");
                    empty.AddToClassList("trigger-test-empty");
                    _chainHost.Add(empty);
                }
            }
            catch (Exception exception)
            {
                var error = new Label(
                    "无法解析 Trigger Authoring：" +
                    exception.GetType().Name +
                    " · " +
                    exception.Message);
                error.AddToClassList("trigger-test-empty");
                error.AddToClassList("is-error");
                _chainHost.Add(error);
            }

            _resultTitle.text =
                TriggerTestManualScenarioDisplayMap
                    .Resolve(scenario.Descriptor)
                    .DisplayName;
            _resultMessage.text =
                GetProfileHint(scenario.Descriptor) +
                " · 内容来自 Trigger Authoring、Function 固定前置条件和验收声明，尚未执行。";
            RefreshSelectionState();
        }

        private void SynchronizeScenarioParameters(
            ITriggerTestScenario scenario)
        {
            if (scenario is ITriggerTestRuntimeAiLabHistoricalFrameScenario
                    historicalFrameScenario &&
                _historicalFrameField != null)
            {
                historicalFrameScenario.TargetFrame =
                    _historicalFrameField.value;
            }

            if (scenario is ITriggerTestRuntimeAiLabTimelineEventScenario
                    timelineEventScenario &&
                _timelineEventField != null)
            {
                timelineEventScenario.TargetEventIndex =
                    _timelineEventField.value;
            }
        }

        private void RenderExecutionError(string message)
        {
            SetStatusClass(TriggerTestRunStatus.Failed);
            _chainTitle.text = "执行链";
            _statusLabel.text = "无法运行";
            _durationLabel.text = "—";
            _chainHost.Clear();
            var error = new Label(
                string.IsNullOrWhiteSpace(message)
                    ? "Trigger 场景运行失败。"
                    : message);
            error.AddToClassList("trigger-test-empty");
            error.AddToClassList("is-error");
            _chainHost.Add(error);
            _resultTitle.text = "执行入口异常";
            _resultMessage.text = error.text;
            RefreshSelectionState();
        }

        private void SetRunning(bool running)
        {
            _isRunning = running;
            _runButton?.SetEnabled(!running && CanRun());
            _rerunButton?.SetEnabled(!running && CanRun());
            if (running)
            {
                SetStatusClass(null);
                _statusLabel.text = "运行中…";
                _durationLabel.text = string.Empty;
            }
        }

        private void ToggleCollapsed() => SetCollapsed(!_collapsed);

        private void SetCollapsed(bool collapsed)
        {
            _collapsed = collapsed;
            if (_expandedBody != null)
            {
                _expandedBody.style.display = collapsed
                    ? DisplayStyle.None
                    : DisplayStyle.Flex;
            }
            if (_collapseButton != null)
            {
                _collapseButton.text = collapsed ? "⌄" : "⌃";
                _collapseButton.tooltip = collapsed ? "展开面板" : "折叠面板";
            }
            minSize = collapsed
                ? CollapsedMinimumSize
                : ExpandedMinimumSize;
        }

        private void SelectScenario(string scenarioId)
        {
            int index = FindScenarioIndex(scenarioId);
            if (index < 0)
                return;

            _scenarioSelector.SetValueWithoutNotify(scenarioId);
            OnScenarioSelectionChanged();
        }

        private int FindScenarioIndex(string scenarioId)
        {
            if (string.IsNullOrEmpty(scenarioId) || _discovery == null)
                return -1;
            for (int i = 0; i < _discovery.Scenarios.Count; i++)
            {
                if (string.Equals(
                        _discovery.Scenarios[i].Descriptor.ScenarioId.Value,
                        scenarioId,
                        StringComparison.Ordinal))
                {
                    return i;
                }
            }

            return -1;
        }

        private bool CanRun() =>
            !_isRunning &&
            (!IsEnterPlayModeSelected() ||
             !TriggerTestRuntimeAiLabEnterPlayModeWorkflow.IsRunning) &&
            _discovery != null &&
            _discovery.Succeeded &&
            _scenarioSelector != null &&
            FindScenarioIndex(_scenarioSelector.Value) >= 0 &&
            _executionService != null &&
            _executionService.CanRun(
                GetSelectedDescriptor(),
                out _);

        private TriggerTestDescriptor GetSelectedDescriptor()
        {
            int index = _scenarioSelector == null
                ? -1
                : FindScenarioIndex(_scenarioSelector.Value);
            return _discovery != null &&
                   index >= 0 &&
                   index < _discovery.Scenarios.Count
                ? _discovery.Scenarios[index].Descriptor
                : null;
        }

        private ITriggerTestScenario GetSelectedScenario()
        {
            int index = _scenarioSelector == null
                ? -1
                : FindScenarioIndex(_scenarioSelector.Value);
            return _discovery != null &&
                   index >= 0 &&
                   index < _discovery.Scenarios.Count
                ? _discovery.Scenarios[index]
                : null;
        }

        private void ApplyHistoricalFrame(int frame)
        {
            if (GetSelectedScenario() is
                ITriggerTestRuntimeAiLabHistoricalFrameScenario scenario)
            {
                scenario.TargetFrame = frame;
                RenderEmptyState();
            }
        }

        private void ApplyTimelineEvent(int eventIndex)
        {
            if (GetSelectedScenario() is
                ITriggerTestRuntimeAiLabTimelineEventScenario scenario)
            {
                scenario.TargetEventIndex = eventIndex;
                RenderEmptyState();
            }
        }

        private string GetSelectedScenarioId() =>
            GetSelectedDescriptor()?.ScenarioId.Value ?? string.Empty;

        private bool IsEnterPlayModeSelected() =>
            string.Equals(
                GetSelectedScenarioId(),
                RuntimeAiLabEnterPlayModeScenario.ScenarioId,
                StringComparison.Ordinal);

        private bool HasSelectedReport()
        {
            TriggerTestDescriptor descriptor = GetSelectedDescriptor();
            return descriptor != null &&
                   File.Exists(
                       TriggerTestReportWriter.GetLatestJsonPath(
                           descriptor.ScenarioId));
        }

        private void RevealSelectedReport()
        {
            TriggerTestDescriptor descriptor = GetSelectedDescriptor();
            if (descriptor == null)
                return;
            string path =
                TriggerTestReportWriter.GetLatestJsonPath(
                    descriptor.ScenarioId);
            if (File.Exists(path))
                EditorUtility.RevealInFinder(path);
        }

        private static void RevealReportRoot()
        {
            Directory.CreateDirectory(
                TriggerTestReportWriter.RootDirectoryPath);
            EditorUtility.RevealInFinder(
                TriggerTestReportWriter.RootDirectoryPath);
        }

        private void CopySelectedSummary()
        {
            if (IsEnterPlayModeSelected())
            {
                TriggerTestRuntimeAiLabPlayModeSnapshot workflow =
                    TriggerTestRuntimeAiLabEnterPlayModeWorkflow
                        .CaptureSnapshot();
                if (workflow.HasState)
                {
                    EditorGUIUtility.systemCopyBuffer =
                        RuntimeAiLabEnterPlayModeScenario.ScenarioId +
                        Environment.NewLine +
                        "status: " +
                        workflow.Status +
                        Environment.NewLine +
                        "result: " +
                        workflow.Message;
                    return;
                }
            }

            TriggerTestRunResult result = _lastExecution?.FindResult(
                GetSelectedScenarioId());
            if (result == null)
                return;
            EditorGUIUtility.systemCopyBuffer =
                result.Descriptor.ScenarioId.Value +
                Environment.NewLine +
                "status: " +
                result.Status +
                Environment.NewLine +
                "operations: " +
                result.SimulationResult.Events.Count +
                Environment.NewLine +
                "failures: " +
                result.SimulationResult.Failures.Count;
        }

        private void SetStatusClass(TriggerTestRunStatus? status)
        {
            _statusLabel.EnableInClassList(
                "is-passed",
                status == TriggerTestRunStatus.Passed);
            _statusLabel.EnableInClassList(
                "is-pending",
                status == TriggerTestRunStatus.PendingExternalEvidence);
            _statusLabel.EnableInClassList(
                "is-failed",
                status == TriggerTestRunStatus.Failed);
        }

        private string GetDiscoveryError()
        {
            if (_discovery == null)
                return "尚未发现 Trigger 场景。";
            if (_discovery.Errors.Count == 0)
                return _discovery.Scenarios.Count == 0
                    ? "没有可运行的 Trigger 场景。"
                    : string.Empty;
            return _discovery.Errors[0].TypeName +
                   ": " +
                   _discovery.Errors[0].Message;
        }

        private string GetProfileHint(
            TriggerTestDescriptor descriptor)
        {
            if (descriptor == null)
                return "没有可运行的 Trigger 场景。";
            switch (descriptor.ExecutionProfile)
            {
                case TriggerTestExecutionProfile.PlayMode:
                    if (_executionService == null)
                        return "PlayMode · 执行服务尚未初始化。";
                    return _executionService.CanRun(
                        descriptor,
                        out string unavailableReason)
                            ? "PlayMode · 已连接真实场景；保持 Game View 可见"
                            : "PlayMode · " + unavailableReason;
                case TriggerTestExecutionProfile.EditMode:
                    return "EditMode · Editor 操作";
                default:
                    return "NoEngine · 逻辑测试，不会驱动 Game View";
            }
        }

        private string GetUnavailableRunTooltip(
            TriggerTestDescriptor descriptor)
        {
            if (_executionService == null ||
                _executionService.CanRun(
                    descriptor,
                    out string unavailableReason))
            {
                return "运行选中的 Trigger Scenario";
            }

            return "当前不可运行：" + unavailableReason;
        }

        private static string GetStageClass(string stage)
        {
            switch (stage)
            {
                case "输入":
                case "事件":
                    return "is-input";
                case "Trigger":
                case "条件":
                case "触发条件":
                case "触发过滤":
                case "策略":
                case "触发方式":
                case "行为条件":
                case "执行前提":
                    return "is-trigger";
                case "Function":
                case "行为":
                    return "is-function";
                default:
                    return "is-assertion";
            }
        }

        private void OnPlayModeStateChanged(
            PlayModeStateChange state)
        {
            if (_executionService == null ||
                _scenarioSelector == null)
            {
                return;
            }

            RefreshSelectionState();
            RenderEditorWorkflowIfSelected();
        }

        private void OnEditorWorkflowStateChanged()
        {
            if (_scenarioSelector == null)
                return;

            RenderEditorWorkflowIfSelected();
        }
    }
}
