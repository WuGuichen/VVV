using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEditor;
using UnityEngine;

namespace MxFramework.Game.CharacterTest.Editor
{
    [InitializeOnLoad]
    internal static class CharacterTestRuntimeAiPlannerScenePreview
    {
        private const string EnabledKey =
            "MxFramework.CharacterTest.RuntimeAiPlanner.ScenePreview.Enabled";
        private const string ModeKey =
            "MxFramework.CharacterTest.RuntimeAiPlanner.ScenePreview.Mode";
        private const string LabelModeKey =
            "MxFramework.CharacterTest.RuntimeAiPlanner.ScenePreview.LabelMode";
        private const string ShowLegendKey =
            "MxFramework.CharacterTest.RuntimeAiPlanner.ScenePreview.ShowLegend";
        private const string ShowTargetLineKey =
            "MxFramework.CharacterTest.RuntimeAiPlanner.ScenePreview.ShowTargetLine";
        private const string ShowAttackRangeKey =
            "MxFramework.CharacterTest.RuntimeAiPlanner.ScenePreview.ShowAttackRange";
        private const string ShowDecisionMoveKey =
            "MxFramework.CharacterTest.RuntimeAiPlanner.ScenePreview.ShowDecisionMove";
        private const double RefreshIntervalSeconds = 0.2d;
        private const float SourceMarkerSize = 0.12f;
        private const float TargetMarkerSize = 0.1f;
        private const float DecisionArrowLength = 1.25f;
        private const float LegendWidth = 330f;
        private const float LegendHeight = 104f;

        private static readonly List<CharacterTestRuntimeAiPlannerScenePreviewItem> CachedItems =
            new List<CharacterTestRuntimeAiPlannerScenePreviewItem>(16);

        private static double _lastSnapshotTime;
        private static bool _cacheDirty = true;
        private static GUIStyle _labelStyle;
        private static GUIStyle _legendStyle;

        static CharacterTestRuntimeAiPlannerScenePreview()
        {
            SceneView.duringSceneGui -= OnSceneGui;
            SceneView.duringSceneGui += OnSceneGui;
            EditorApplication.update -= OnEditorUpdate;
            EditorApplication.update += OnEditorUpdate;
        }

        public static bool Enabled
        {
            get => EditorPrefs.GetBool(EnabledKey, false);
            set
            {
                if (Enabled == value)
                    return;

                EditorPrefs.SetBool(EnabledKey, value);
                _cacheDirty = true;
                RequestRepaintIfActive();
            }
        }

        public static CharacterTestRuntimeAiPlannerScenePreviewMode Mode
        {
            get
            {
                int raw = EditorPrefs.GetInt(
                    ModeKey,
                    (int)CharacterTestRuntimeAiPlannerScenePreviewMode.SelectedAgent);
                if (raw < (int)CharacterTestRuntimeAiPlannerScenePreviewMode.SelectedAgent
                    || raw > (int)CharacterTestRuntimeAiPlannerScenePreviewMode.AllAgents)
                {
                    return CharacterTestRuntimeAiPlannerScenePreviewMode.SelectedAgent;
                }

                return (CharacterTestRuntimeAiPlannerScenePreviewMode)raw;
            }

            set
            {
                if (Mode == value)
                    return;

                EditorPrefs.SetInt(ModeKey, (int)value);
                _cacheDirty = true;
                RequestRepaintIfActive();
            }
        }

        public static int SelectedAgentEntityId { get; set; }

        public static double RefreshRateHz => 1d / RefreshIntervalSeconds;

        public static int CachedItemCount => CachedItems.Count;

        public static CharacterTestRuntimeAiPlannerScenePreviewLabelMode LabelMode
        {
            get
            {
                int raw = EditorPrefs.GetInt(
                    LabelModeKey,
                    (int)CharacterTestRuntimeAiPlannerScenePreviewLabelMode.Selected);
                if (raw < (int)CharacterTestRuntimeAiPlannerScenePreviewLabelMode.Off
                    || raw > (int)CharacterTestRuntimeAiPlannerScenePreviewLabelMode.All)
                {
                    return CharacterTestRuntimeAiPlannerScenePreviewLabelMode.Selected;
                }

                return (CharacterTestRuntimeAiPlannerScenePreviewLabelMode)raw;
            }

            set
            {
                if (LabelMode == value)
                    return;

                EditorPrefs.SetInt(LabelModeKey, (int)value);
                RequestRepaintIfActive();
            }
        }

        public static bool ShowLegend
        {
            get => EditorPrefs.GetBool(ShowLegendKey, true);
            set => SetBoolPreference(ShowLegendKey, value);
        }

        public static bool ShowTargetLine
        {
            get => EditorPrefs.GetBool(ShowTargetLineKey, true);
            set => SetBoolPreference(ShowTargetLineKey, value);
        }

        public static bool ShowAttackRange
        {
            get => EditorPrefs.GetBool(ShowAttackRangeKey, true);
            set => SetBoolPreference(ShowAttackRangeKey, value);
        }

        public static bool ShowDecisionMove
        {
            get => EditorPrefs.GetBool(ShowDecisionMoveKey, true);
            set => SetBoolPreference(ShowDecisionMoveKey, value);
        }

        public static void ResetPreferencesForTests()
        {
            EditorPrefs.DeleteKey(EnabledKey);
            EditorPrefs.DeleteKey(ModeKey);
            EditorPrefs.DeleteKey(LabelModeKey);
            EditorPrefs.DeleteKey(ShowLegendKey);
            EditorPrefs.DeleteKey(ShowTargetLineKey);
            EditorPrefs.DeleteKey(ShowAttackRangeKey);
            EditorPrefs.DeleteKey(ShowDecisionMoveKey);
            SelectedAgentEntityId = 0;
            _cacheDirty = true;
            CachedItems.Clear();
            RequestRepaintIfActive();
        }

        public static Color ResolveColorForTests(
            CharacterTestRuntimeAiPlannerScenePreviewSeverity severity)
        {
            switch (severity)
            {
                case CharacterTestRuntimeAiPlannerScenePreviewSeverity.Error:
                    return new Color(0.9f, 0.2f, 0.16f, 1f);
                case CharacterTestRuntimeAiPlannerScenePreviewSeverity.Warning:
                    return new Color(0.95f, 0.72f, 0.18f, 1f);
                case CharacterTestRuntimeAiPlannerScenePreviewSeverity.Inactive:
                    return new Color(0.5f, 0.5f, 0.5f, 0.9f);
                default:
                    return new Color(0.18f, 0.85f, 0.36f, 1f);
            }
        }

        public static Color ResolveTargetColorForTests(
            CharacterTestRuntimeAiPlannerScenePreviewItem item)
        {
            if (!item.TargetExists || !item.HasGeometry)
                return ResolveColorForTests(CharacterTestRuntimeAiPlannerScenePreviewSeverity.Inactive);
            if (item.TargetBlocked)
                return ResolveColorForTests(CharacterTestRuntimeAiPlannerScenePreviewSeverity.Warning);
            return item.TargetVisible
                ? ResolveColorForTests(CharacterTestRuntimeAiPlannerScenePreviewSeverity.Normal)
                : ResolveColorForTests(CharacterTestRuntimeAiPlannerScenePreviewSeverity.Inactive);
        }

        public static Color ResolveLineColorForTests(
            CharacterTestRuntimeAiPlannerScenePreviewItem item)
        {
            if (item.Severity == CharacterTestRuntimeAiPlannerScenePreviewSeverity.Error)
                return ResolveColorForTests(CharacterTestRuntimeAiPlannerScenePreviewSeverity.Error);
            if (!item.TargetExists || !item.HasGeometry)
                return ResolveColorForTests(CharacterTestRuntimeAiPlannerScenePreviewSeverity.Inactive);
            if (item.TargetBlocked || !item.MoveDirectPathClear || item.MoveCandidateIndex >= 0)
                return ResolveColorForTests(CharacterTestRuntimeAiPlannerScenePreviewSeverity.Warning);
            return item.TargetVisible
                ? ResolveColorForTests(CharacterTestRuntimeAiPlannerScenePreviewSeverity.Normal)
                : ResolveColorForTests(CharacterTestRuntimeAiPlannerScenePreviewSeverity.Inactive);
        }

        public static Color ResolveAttackRangeColorForTests(
            CharacterTestRuntimeAiPlannerScenePreviewItem item)
        {
            if (!item.TargetExists || !item.HasGeometry || item.AttackRange <= 0f)
                return new Color(0.5f, 0.5f, 0.5f, 0.35f);
            return item.TargetInAttackRange
                ? new Color(0.18f, 0.85f, 0.36f, 0.85f)
                : new Color(0.5f, 0.5f, 0.5f, 0.45f);
        }

        public static Color ResolveMoveColorForTests(
            CharacterTestRuntimeAiPlannerScenePreviewItem item)
        {
            if (item.DecisionMove.sqrMagnitude <= 0.0001f)
                return ResolveColorForTests(CharacterTestRuntimeAiPlannerScenePreviewSeverity.Inactive);
            if (!item.MovePathAvailable)
                return ResolveColorForTests(CharacterTestRuntimeAiPlannerScenePreviewSeverity.Error);
            if (!item.MoveDirectPathClear || item.MoveCandidateIndex >= 0)
                return ResolveColorForTests(CharacterTestRuntimeAiPlannerScenePreviewSeverity.Warning);
            return ResolveColorForTests(CharacterTestRuntimeAiPlannerScenePreviewSeverity.Normal);
        }

        public static bool ShouldDrawLabelForTests(
            CharacterTestRuntimeAiPlannerScenePreviewItem item,
            CharacterTestRuntimeAiPlannerScenePreviewLabelMode labelMode,
            CharacterTestRuntimeAiPlannerScenePreviewMode mode,
            int selectedAgentEntityId)
        {
            switch (labelMode)
            {
                case CharacterTestRuntimeAiPlannerScenePreviewLabelMode.Off:
                    return false;
                case CharacterTestRuntimeAiPlannerScenePreviewLabelMode.All:
                    return true;
                case CharacterTestRuntimeAiPlannerScenePreviewLabelMode.Issues:
                    return item.Severity == CharacterTestRuntimeAiPlannerScenePreviewSeverity.Warning
                        || item.Severity == CharacterTestRuntimeAiPlannerScenePreviewSeverity.Error;
                case CharacterTestRuntimeAiPlannerScenePreviewLabelMode.Selected:
                    return mode == CharacterTestRuntimeAiPlannerScenePreviewMode.SelectedAgent
                        || (selectedAgentEntityId > 0 && item.AgentEntityId == selectedAgentEntityId);
                default:
                    return false;
            }
        }

        public static bool ShouldRequestRepaintForTests(
            bool enabled,
            bool isPlaying,
            int targetCount)
        {
            return enabled && isPlaying && targetCount > 0;
        }

        public static string FormatLegendForTests(
            int itemCount,
            CharacterTestRuntimeAiPlannerScenePreviewMode mode,
            CharacterTestRuntimeAiPlannerScenePreviewLabelMode labelMode)
        {
            return "Runtime AI Planner 场景预览"
                + "\nmode="
                + mode
                + " labels="
                + labelMode
                + " agents="
                + itemCount.ToString(CultureInfo.InvariantCulture)
                + " refresh="
                + RefreshRateHz.ToString("0.#", CultureInfo.InvariantCulture)
                + "Hz"
                + "\n绿色=可见/直通  黄色=遮挡/绕行  红色=拒绝/无路径/危险"
                + "\n灰色=无目标/几何不可用";
        }

        public static string FormatLabelForTests(
            CharacterTestRuntimeAiPlannerScenePreviewItem item)
        {
            string text = "Agent "
                + item.AgentEntityId.ToString(CultureInfo.InvariantCulture)
                + " | "
                + "state="
                + EmptyAsNone(item.StateText)
                + "\naction="
                + EmptyAsNone(item.ActionText)
                + " score/cost="
                + item.UtilityScore.ToString("0.###", CultureInfo.InvariantCulture)
                + "/"
                + item.ActionCost.ToString("0.###", CultureInfo.InvariantCulture)
                + "\ntarget="
                + (item.TargetExists ? item.TargetEntityId.ToString(CultureInfo.InvariantCulture) : "none")
                + " dist="
                + item.TargetDistance.ToString("0.##", CultureInfo.InvariantCulture)
                + " "
                + EmptyAsNone(item.VisibilityText)
                + " / "
                + EmptyAsNone(item.PathText)
                + " / "
                + EmptyAsNone(item.RangeText);
            if (!string.IsNullOrWhiteSpace(item.IssueText))
                text += "\nissue=" + item.IssueText;
            return text;
        }

        public static CharacterTestRuntimeAiPlannerScenePreviewSeverity ResolveSeverityForTests(
            CharacterTestRuntimeAiPlannerAgentSnapshot agent)
        {
            if (!string.IsNullOrWhiteSpace(agent.RejectReason))
                return CharacterTestRuntimeAiPlannerScenePreviewSeverity.Error;
            if (!agent.TargetExists || !agent.HasScenePreviewGeometry)
                return CharacterTestRuntimeAiPlannerScenePreviewSeverity.Inactive;
            if (!agent.MovePathAvailable || agent.EnvironmentNearHazard)
                return CharacterTestRuntimeAiPlannerScenePreviewSeverity.Error;
            if (agent.TargetLineOfSightBlocked
                || !agent.MoveDirectPathClear
                || agent.MoveCandidateIndex >= 0)
            {
                return CharacterTestRuntimeAiPlannerScenePreviewSeverity.Warning;
            }

            return CharacterTestRuntimeAiPlannerScenePreviewSeverity.Normal;
        }

        public static CharacterTestRuntimeAiPlannerScenePreviewItem CreateItemForTests(
            string sourceName,
            CharacterTestRuntimeAiPlannerAgentSnapshot agent)
        {
            CharacterTestRuntimeAiPlannerScenePreviewSeverity severity = ResolveSeverityForTests(agent);
            CharacterTestRuntimeAiPlannerPlayMonitorAgentViewModel viewModel =
                CharacterTestRuntimeAiPlannerPlayMonitorAgentViewModel.Create(sourceName, agent);
            return new CharacterTestRuntimeAiPlannerScenePreviewItem(
                viewModel.SourceName,
                agent.EntityId,
                agent.TargetEntityId,
                viewModel.StateText,
                viewModel.ActionText,
                agent.TargetExists,
                agent.TargetInAttackRange,
                agent.TargetVisible,
                agent.TargetLineOfSightBlocked,
                agent.MovePathAvailable,
                agent.MoveDirectPathClear,
                agent.MoveCandidateIndex,
                agent.EnvironmentNearHazard,
                agent.RejectReason,
                agent.TargetDistance,
                agent.SelectedUtilityScore,
                agent.SelectedActionCost,
                agent.HasScenePreviewGeometry,
                new Vector3(
                    agent.ScenePreviewSourceCenterX,
                    agent.ScenePreviewSourceCenterY,
                    agent.ScenePreviewSourceCenterZ),
                new Vector3(
                    agent.ScenePreviewTargetCenterX,
                    agent.ScenePreviewTargetCenterY,
                    agent.ScenePreviewTargetCenterZ),
                new Vector2(
                    agent.ScenePreviewTargetDirectionX,
                    agent.ScenePreviewTargetDirectionZ),
                new Vector2(
                    agent.ScenePreviewDecisionMoveX,
                    agent.ScenePreviewDecisionMoveZ),
                agent.ScenePreviewAttackRange,
                viewModel.DecisionHeadline,
                viewModel.ConditionSummary,
                viewModel.IssueText,
                viewModel.VisibilityText,
                viewModel.PathText,
                BuildRangeText(agent),
                BuildMoveText(agent),
                severity,
                agent.TargetDistanceAvailable,
                agent.HasScenePreviewDistanceWitness,
                new Vector3(
                    agent.ScenePreviewDistanceSourceX,
                    agent.ScenePreviewDistanceSourceY,
                    agent.ScenePreviewDistanceSourceZ),
                new Vector3(
                    agent.ScenePreviewDistanceTargetX,
                    agent.ScenePreviewDistanceTargetY,
                    agent.ScenePreviewDistanceTargetZ),
                agent.TargetRootDistance,
                agent.ScenePreviewDistanceGeometryFrame,
                agent.TargetLogicalHurtboxId.ToString(CultureInfo.InvariantCulture));
        }

        private static string BuildRangeText(CharacterTestRuntimeAiPlannerAgentSnapshot agent)
        {
            if (!agent.TargetExists)
                return "range=none";
            return agent.TargetInAttackRange ? "range=in" : "range=out";
        }

        private static string BuildMoveText(CharacterTestRuntimeAiPlannerAgentSnapshot agent)
        {
            float sqrMagnitude =
                (agent.ScenePreviewDecisionMoveX * agent.ScenePreviewDecisionMoveX)
                + (agent.ScenePreviewDecisionMoveZ * agent.ScenePreviewDecisionMoveZ);
            if (sqrMagnitude <= 0.0001f)
                return "move=idle";
            if (!agent.MovePathAvailable)
                return "move=blocked";
            if (!agent.MoveDirectPathClear || agent.MoveCandidateIndex >= 0)
                return "move=reroute";
            return "move=direct";
        }

        public static void BuildPreviewItemsForTests(
            IReadOnlyList<CharacterTestRuntimeAiPlannerEditorDebugTarget> targets,
            int selectedAgentEntityId,
            CharacterTestRuntimeAiPlannerScenePreviewMode mode,
            List<CharacterTestRuntimeAiPlannerScenePreviewItem> items)
        {
            items.Clear();
            if (targets == null)
                return;

            CharacterTestRuntimeAiPlannerScenePreviewItem first = default;
            bool hasFirst = false;
            bool selectedAdded = false;
            for (int i = 0; i < targets.Count; i++)
            {
                CharacterTestRuntimeAiPlannerEditorDebugTarget target = targets[i];
                CharacterTestRuntimeAiPlannerSnapshot snapshot =
                    target != null && target.SnapshotProvider != null ? target.SnapshotProvider() : null;
                IReadOnlyList<CharacterTestRuntimeAiPlannerAgentSnapshot> agents =
                    snapshot != null ? snapshot.Agents : null;
                int count = agents != null ? agents.Count : 0;
                for (int a = 0; a < count; a++)
                {
                    CharacterTestRuntimeAiPlannerScenePreviewItem item =
                        CreateItemForTests(target.Name, agents[a]);
                    if (mode == CharacterTestRuntimeAiPlannerScenePreviewMode.AllAgents)
                    {
                        items.Add(item);
                        continue;
                    }

                    if (!hasFirst)
                    {
                        first = item;
                        hasFirst = true;
                    }

                    if (selectedAgentEntityId > 0 && item.AgentEntityId == selectedAgentEntityId)
                    {
                        items.Add(item);
                        selectedAdded = true;
                        break;
                    }
                }

                if (selectedAdded)
                    break;
            }

            if (mode == CharacterTestRuntimeAiPlannerScenePreviewMode.SelectedAgent
                && !selectedAdded
                && hasFirst)
            {
                items.Add(first);
            }
        }

        private static void OnEditorUpdate()
        {
            if (!Enabled || !EditorApplication.isPlaying)
                return;
            if (CharacterTestRuntimeAiPlannerEditorDebugRegistry.Targets.Count == 0)
            {
                if (CachedItems.Count > 0)
                {
                    CachedItems.Clear();
                    SceneView.RepaintAll();
                }

                return;
            }

            double now = EditorApplication.timeSinceStartup;
            if (now - _lastSnapshotTime < RefreshIntervalSeconds)
                return;

            _cacheDirty = true;
            _lastSnapshotTime = now;
            SceneView.RepaintAll();
        }

        private static void OnSceneGui(SceneView sceneView)
        {
            if (!Enabled || !EditorApplication.isPlaying)
                return;

            RefreshCacheIfNeeded();
            if (Event.current == null || Event.current.type != EventType.Repaint)
                return;

            for (int i = 0; i < CachedItems.Count; i++)
                DrawItem(CachedItems[i]);
            if (ShowLegend)
                DrawLegend();
        }

        private static void RefreshCacheIfNeeded()
        {
            double now = EditorApplication.timeSinceStartup;
            if (!_cacheDirty && now - _lastSnapshotTime < RefreshIntervalSeconds)
                return;

            BuildPreviewItemsForTests(
                CharacterTestRuntimeAiPlannerEditorDebugRegistry.Targets,
                SelectedAgentEntityId,
                Mode,
                CachedItems);
            _cacheDirty = false;
            _lastSnapshotTime = now;
        }

        private static void DrawItem(CharacterTestRuntimeAiPlannerScenePreviewItem item)
        {
            if (!item.HasGeometry)
                return;

            using (new Handles.DrawingScope(ResolveColorForTests(item.Severity)))
            {
                float handleSize = HandleUtility.GetHandleSize(item.SourceCenter);
                Handles.SphereHandleCap(
                    0,
                    item.SourceCenter,
                    Quaternion.identity,
                    handleSize * SourceMarkerSize,
                    EventType.Repaint);
                if (item.TargetExists)
                {
                    using (new Handles.DrawingScope(ResolveTargetColorForTests(item)))
                    {
                        Handles.SphereHandleCap(
                            0,
                            item.TargetCenter,
                            Quaternion.identity,
                            handleSize * TargetMarkerSize,
                            EventType.Repaint);
                    }

                    if (ShowTargetLine)
                    {
                        using (new Handles.DrawingScope(ResolveLineColorForTests(item)))
                            Handles.DrawAAPolyLine(
                                4f,
                                item.DistanceSourcePoint,
                                item.DistanceTargetPoint);
                    }

                }

                if (ShowAttackRange && item.AttackRange > 0f)
                {
                    using (new Handles.DrawingScope(ResolveAttackRangeColorForTests(item)))
                        Handles.DrawWireDisc(item.SourceCenter, Vector3.up, item.AttackRange);
                }

                if (ShowDecisionMove)
                    DrawDecisionMove(item, handleSize);
                if (ShouldDrawLabelForTests(item, LabelMode, Mode, SelectedAgentEntityId))
                {
                    Handles.Label(
                        item.SourceCenter + Vector3.up * handleSize * 0.35f,
                        FormatLabelForTests(item),
                        LabelStyle);
                }
            }
        }

        private static void DrawDecisionMove(
            CharacterTestRuntimeAiPlannerScenePreviewItem item,
            float handleSize)
        {
            if (item.DecisionMove.sqrMagnitude <= 0.0001f)
                return;

            Vector3 direction = new Vector3(item.DecisionMove.x, 0f, item.DecisionMove.y).normalized;
            Vector3 end = item.SourceCenter + direction * DecisionArrowLength;
            using (new Handles.DrawingScope(ResolveMoveColorForTests(item)))
            {
                Handles.DrawAAPolyLine(5f, item.SourceCenter, end);
                Handles.ArrowHandleCap(
                    0,
                    end,
                    Quaternion.LookRotation(direction, Vector3.up),
                    handleSize * 0.35f,
                    EventType.Repaint);
            }
        }

        private static void DrawLegend()
        {
            Handles.BeginGUI();
            GUI.Label(
                new Rect(12f, 12f, LegendWidth, LegendHeight),
                FormatLegendForTests(CachedItems.Count, Mode, LabelMode),
                LegendStyle);
            Handles.EndGUI();
        }

        private static GUIStyle LabelStyle
        {
            get
            {
                if (_labelStyle != null)
                    return _labelStyle;

                _labelStyle = new GUIStyle(EditorStyles.helpBox)
                {
                    alignment = TextAnchor.UpperLeft,
                    fontSize = 11,
                    richText = false,
                    wordWrap = false
                };
                _labelStyle.normal.textColor = Color.white;
                return _labelStyle;
            }
        }

        private static GUIStyle LegendStyle
        {
            get
            {
                if (_legendStyle != null)
                    return _legendStyle;

                _legendStyle = new GUIStyle(EditorStyles.helpBox)
                {
                    alignment = TextAnchor.UpperLeft,
                    fontSize = 11,
                    richText = false,
                    wordWrap = true
                };
                _legendStyle.normal.textColor = Color.white;
                return _legendStyle;
            }
        }

        private static string EmptyAsNone(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? "none" : value;
        }

        private static void RequestRepaintIfActive()
        {
            if (!ShouldRequestRepaintForTests(
                    Enabled,
                    EditorApplication.isPlaying,
                    CharacterTestRuntimeAiPlannerEditorDebugRegistry.Targets.Count))
            {
                return;
            }

            SceneView.RepaintAll();
        }

        private static void SetBoolPreference(string key, bool value)
        {
            if (EditorPrefs.GetBool(key, true) == value)
                return;

            EditorPrefs.SetBool(key, value);
            RequestRepaintIfActive();
        }
    }
}
