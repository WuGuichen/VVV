using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using MxFramework.Diagnostics;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace MxFramework.Game.CharacterTest.Editor
{
    public sealed partial class CharacterTestRuntimeAiPlannerWindow
    {
        private void BuildPlayMonitor(VisualElement root)
        {
            root.Add(CreateSectionTitle("Play Monitor"));
            if (!EditorApplication.isPlaying || _playMonitorSnapshotEntries.Count == 0)
            {
                _lastReport = "Runtime AI Planner\nstatus: no CharacterTest runtime session\n";
                root.Add(CreateNotice("未发现 CharacterTest 运行会话。"));
                root.Add(CreateReadOnlyLine("模式", "Play Mode 只读监控"));
                root.Add(CreateReadOnlyLine("状态", "无已注册 CharacterTest session"));
                _statusText = "未发现 CharacterTest 运行会话";
                return;
            }

            int agentIndex = 0;
            bool selectedExists = false;
            var scroll = new ScrollView(ScrollViewMode.Vertical);
            scroll.style.flexGrow = 1;
            scroll.style.minHeight = 0;
            scroll.style.minWidth = 0;

            for (int i = 0; i < _playMonitorSnapshotEntries.Count; i++)
            {
                CharacterTestRuntimeAiPlannerPlayMonitorSnapshotEntry entry =
                    _playMonitorSnapshotEntries[i];
                CharacterTestRuntimeAiPlannerSnapshot snapshot = entry.Snapshot;
                int count = entry.AgentCount;

                scroll.Add(CreatePlayMonitorSourceHeader(entry.TargetName, snapshot, count));
                if (count == 0)
                {
                    scroll.Add(CreateNotice("当前 Runtime AI Planner session 没有注册 agent。"));
                    continue;
                }

                scroll.Add(CreateAgentSummaryHeader());
                for (int a = 0; a < count; a++)
                {
                    int localAgentIndex = agentIndex;
                    CharacterTestRuntimeAiPlannerPlayMonitorAgentEntry agentEntry =
                        _playMonitorAgentEntries[agentIndex];
                    CharacterTestRuntimeAiPlannerPlayMonitorAgentViewModel viewModel =
                        agentEntry.ViewModel;
                    Button row = CreateAgentSummaryRow(
                        viewModel,
                        () =>
                        {
                            _selectedAgentIndex = localAgentIndex;
                            CharacterTestRuntimeAiPlannerScenePreview.SelectedAgentEntityId =
                                agentEntry.Snapshot.EntityId;
                            RefreshView();
                        });
                    if (localAgentIndex == _selectedAgentIndex)
                    {
                        selectedExists = true;
                        row.style.backgroundColor = new Color(0.22f, 0.36f, 0.54f);
                    }

                    scroll.Add(row);
                    agentIndex++;
                }
            }

            if (agentIndex > 0 && (_selectedAgentIndex < 0 || !selectedExists))
                _selectedAgentIndex = 0;
            else if (agentIndex == 0)
                _selectedAgentIndex = -1;
            SyncScenePreviewSelectedAgent();
            root.Add(scroll);
            _lastReport = "Play Monitor report is generated when copied.";
            _statusText = "Play Monitor 只读，agents=" + agentIndex;
        }

        private void BuildPlayMonitorInspector(VisualElement root)
        {
            root.Add(CreateSectionTitle("Agent Snapshot"));
            if (!TryGetSelectedAgent(out CharacterTestRuntimeAiPlannerPlayMonitorAgentEntry agentEntry))
            {
                root.Add(CreateNotice("未发现 CharacterTest 运行会话。"));
                return;
            }

            CharacterTestRuntimeAiPlannerAgentSnapshot agent = agentEntry.Snapshot;
            CharacterTestRuntimeAiPlannerPlayMonitorAgentViewModel viewModel = agentEntry.ViewModel;
            var scroll = new ScrollView(ScrollViewMode.Vertical);
            scroll.style.flexGrow = 1;
            scroll.style.minHeight = 0;
            scroll.style.minWidth = 0;

            scroll.Add(CreatePlayMonitorInspectorHeader(viewModel));
            scroll.Add(CreatePlayMonitorSection(
                "Behavior Loadout",
                CreatePlayMonitorField("selection", viewModel.BehaviorLoadoutText),
                CreatePlayMonitorField("玩家短状态", viewModel.PlayerStatusText)));
            scroll.Add(CreatePlayMonitorSection(
                "决策摘要",
                CreatePlayMonitorField("为什么选择", viewModel.DecisionHeadline),
                CreatePlayMonitorField("条件摘要", viewModel.ConditionSummary),
                CreatePlayMonitorChipRow(
                    CreatePlayMonitorChip(viewModel.IssueText, GetSeverityColor(viewModel.Severity)),
                    CreatePlayMonitorChip(viewModel.VisibilityText, GetVisibilityColor(agent)),
                    CreatePlayMonitorChip(viewModel.PathText, GetPathColor(agent)))));
            scroll.Add(CreatePlayMonitorSection(
                "目标与感知",
                CreatePlayMonitorField("目标", viewModel.TargetText),
                CreatePlayMonitorField("距离", viewModel.DistanceText),
                CreatePlayMonitorField("感知事实", viewModel.TargetDetailText)));
            scroll.Add(CreatePlayMonitorSection(
                "移动与路径",
                CreatePlayMonitorField("路径", viewModel.PathText),
                CreatePlayMonitorField("移动诊断", viewModel.MovementDetailText)));
            scroll.Add(CreatePlayMonitorSection(
                "Utility",
                CreatePlayMonitorField("动作", viewModel.ActionText),
                CreatePlayMonitorField("score / cost", viewModel.ScoreCostText),
                CreatePlayMonitorField("Utility 明细", viewModel.UtilityDetailText)));
            scroll.Add(CreatePlayMonitorSection(
                "动作链",
                CreatePlayMonitorField("Ability / CharacterAction", viewModel.ActionChainText)));
            scroll.Add(CreatePlayMonitorSection(
                "命令输出",
                CreatePlayMonitorField("last command", viewModel.CommandText),
                CreatePlayMonitorField("recent transition", viewModel.TransitionText),
                CreatePlayMonitorField("命令明细", viewModel.CommandDetailText)));

            var raw = new Foldout { text = "Raw", value = false };
            raw.style.marginTop = 6;
            raw.Add(CreatePlayMonitorField("原始字段", viewModel.RawFactsText));
            scroll.Add(raw);
            root.Add(scroll);
        }

        private void BuildPlayMonitorLeftPanel(VisualElement root)
        {
            root.Add(CreateSectionTitle("Play Monitor"));
            root.Add(CreateWrappedLabel("只读运行监控"));
            root.Add(CreateWrappedLabel("保存 Profile 后需要重建 CharacterTest 会话才会影响 Play Mode runtime。"));
            root.Add(CreateSpacer(8));
            root.Add(CreateSectionTitle("Profile"));
            root.Add(CreateWrappedLabel(_profileLoaded ? EmptyAsNone(_model.ProfileId) : "未加载 Profile JSON"));
            root.Add(CreateWrappedLabel(_profileProjectPath));
            root.Add(CreateSpacer(8));
            root.Add(CreateSectionTitle("当前关注"));
            root.Add(CreateWrappedLabel(_selectedAgentIndex >= 0
                ? "Agent index " + _selectedAgentIndex
                : "未选择 agent"));
            root.Add(CreateWrappedLabel(
                "本次刷新 sources="
                + _playMonitorSnapshotEntries.Count.ToString(CultureInfo.InvariantCulture)
                + "，agents="
                + _playMonitorAgentEntries.Count.ToString(CultureInfo.InvariantCulture)));
            root.Add(CreateSpacer(8));
            root.Add(CreatePlayMonitorRuntimeAiBehaviorLoadoutControls());
            root.Add(CreateSpacer(8));
            root.Add(CreatePlayMonitorScenePreviewControls());
        }

        private bool TryGetSelectedAgent(out CharacterTestRuntimeAiPlannerPlayMonitorAgentEntry agentEntry)
        {
            agentEntry = default;
            if (!EditorApplication.isPlaying
                || _selectedAgentIndex < 0
                || _selectedAgentIndex >= _playMonitorAgentEntries.Count)
            {
                return false;
            }

            agentEntry = _playMonitorAgentEntries[_selectedAgentIndex];
            return true;
        }

        private void SyncScenePreviewSelectedAgent()
        {
            if (TryGetSelectedAgent(out CharacterTestRuntimeAiPlannerPlayMonitorAgentEntry entry))
                CharacterTestRuntimeAiPlannerScenePreview.SelectedAgentEntityId = entry.Snapshot.EntityId;
            else
                CharacterTestRuntimeAiPlannerScenePreview.SelectedAgentEntityId = 0;
        }


        private void CapturePlayMonitorSnapshots()
        {
            ClearPlayMonitorSnapshots();
            if (!EditorApplication.isPlaying)
                return;

            FillPlayMonitorSnapshotCache(
                CharacterTestRuntimeAiPlannerEditorDebugRegistry.Targets,
                _playMonitorSnapshotEntries,
                _playMonitorAgentEntries);
        }

        private void ClearPlayMonitorSnapshots()
        {
            _playMonitorSnapshotEntries.Clear();
            _playMonitorAgentEntries.Clear();
        }

        private string CreatePlayMonitorReportText()
        {
            IReadOnlyList<CharacterTestRuntimeAiPlannerEditorDebugTarget> targets =
                CharacterTestRuntimeAiPlannerEditorDebugRegistry.Targets;
            if (!EditorApplication.isPlaying || targets.Count == 0)
                return "Runtime AI Planner\nstatus: no CharacterTest runtime session\n";

            var builder = new StringBuilder();
            for (int i = 0; i < targets.Count; i++)
            {
                CharacterTestRuntimeAiPlannerEditorDebugTarget target = targets[i];
                CharacterTestRuntimeAiPlannerSnapshot snapshot =
                    target.SnapshotProvider != null ? target.SnapshotProvider() : null;
                var source = new CharacterTestRuntimeAiPlannerDebugSource(
                    () => snapshot,
                    target.Name);
                if (builder.Length > 0)
                {
                    builder.AppendLine();
                    builder.AppendLine();
                }

                builder.Append(FrameworkDebugReportExporter.ExportText(source.CreateSnapshot()));
            }

            return builder.ToString();
        }

        internal static void FillPlayMonitorSnapshotCache(
            IReadOnlyList<CharacterTestRuntimeAiPlannerEditorDebugTarget> targets,
            List<CharacterTestRuntimeAiPlannerPlayMonitorSnapshotEntry> snapshotEntries,
            List<CharacterTestRuntimeAiPlannerPlayMonitorAgentEntry> agentEntries)
        {
            snapshotEntries.Clear();
            agentEntries.Clear();
            if (targets == null)
                return;

            for (int i = 0; i < targets.Count; i++)
            {
                CharacterTestRuntimeAiPlannerEditorDebugTarget target = targets[i];
                if (target == null)
                    continue;

                CharacterTestRuntimeAiPlannerSnapshot snapshot =
                    target.SnapshotProvider != null ? target.SnapshotProvider() : null;
                snapshotEntries.Add(new CharacterTestRuntimeAiPlannerPlayMonitorSnapshotEntry(
                    target.Name,
                    snapshot));

                IReadOnlyList<CharacterTestRuntimeAiPlannerAgentSnapshot> agents =
                    snapshot != null ? snapshot.Agents : null;
                int count = agents != null ? agents.Count : 0;
                for (int a = 0; a < count; a++)
                {
                    CharacterTestRuntimeAiPlannerAgentSnapshot agent = agents[a];
                    agentEntries.Add(new CharacterTestRuntimeAiPlannerPlayMonitorAgentEntry(
                        target.Name,
                        agent,
                        CharacterTestRuntimeAiPlannerPlayMonitorAgentViewModel.Create(target.Name, agent)));
                }
            }
        }
    }
}
