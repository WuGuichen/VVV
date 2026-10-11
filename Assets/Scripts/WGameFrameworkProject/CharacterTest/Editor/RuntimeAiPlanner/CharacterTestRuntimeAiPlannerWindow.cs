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
    public sealed partial class CharacterTestRuntimeAiPlannerWindow : EditorWindow
    {
        public const string MenuPath = "MxFramework/Character/Runtime AI Planner";
        public const string TemplateMenuName = "runtime-ai-planner-template-menu";
        internal const string BehaviorGroupFlowExpandButtonName =
            "runtime-ai-planner-behavior-group-flow-expand";
        internal const string BehaviorGroupGuideFoldoutName =
            "runtime-ai-planner-behavior-group-guide-foldout";
        internal const string BehaviorGroupEditFoldoutName =
            "runtime-ai-planner-behavior-group-edit-foldout";
        internal const string BehaviorGroupGeneratedActionStripName =
            "runtime-ai-planner-behavior-group-generated-action-strip";
        internal const string BehaviorGroupGeneratedActionScrollerName =
            "runtime-ai-planner-behavior-group-generated-action-scroller";
        internal const string BehaviorGroupPreviewCandidateFieldName =
            "runtime-ai-planner-behavior-group-preview-candidate";
        internal const string BehaviorGroupPreviewSummaryName =
            "runtime-ai-planner-behavior-group-preview-summary";
        internal const string BehaviorGroupPreviewDiagnosticsFoldoutName =
            "runtime-ai-planner-behavior-group-preview-diagnostics";
        internal const string BehaviorGroupDecisionChainName =
            "runtime-ai-planner-behavior-group-decision-chain";
        internal const string BehaviorGroupRuntimeFlowFoldoutName =
            "runtime-ai-planner-behavior-group-runtime-flow-foldout";
        internal const string BehaviorGroupPreviewRawFoldoutName =
            "runtime-ai-planner-behavior-group-preview-raw";
        internal const string BehaviorCardLibraryName =
            "runtime-ai-planner-behavior-card-library";
        internal const string BehaviorCardEditorScrollName =
            "runtime-ai-planner-behavior-card-editor-scroll";
        internal const string BehaviorCardFlowChainName =
            "runtime-ai-planner-behavior-card-flow-chain";
        internal const string BehaviorCardStepCardName =
            "runtime-ai-planner-behavior-card-step-card";
        internal const string BehaviorCardStepTimelineTrackName =
            "runtime-ai-planner-behavior-card-step-timeline-track";
        internal const string BehaviorCardStepCompactTimelineName =
            "runtime-ai-planner-behavior-card-step-compact-timeline";
        internal const string BehaviorCardStepTimelineEventName =
            "runtime-ai-planner-behavior-card-step-timeline-event";
        internal const string BehaviorCardStepBranchChipName =
            "runtime-ai-planner-behavior-card-step-branch-chip";
        internal const string BehaviorCardStepBranchPanelName =
            "runtime-ai-planner-behavior-card-step-branch-panel";
        internal const string BehaviorCardBranchRuleRowName =
            "runtime-ai-planner-behavior-card-branch-rule-row";
        internal const string BehaviorCardStepWarningChipName =
            "runtime-ai-planner-behavior-card-step-warning-chip";
        internal const string BehaviorCardMainChainContainerName =
            "runtime-ai-planner-behavior-card-main-chain-container";
        internal const string BehaviorCardFlowBoardName =
            "runtime-ai-planner-behavior-card-flow-board";
        internal const string BehaviorCardFlowBoardLeftLaneName =
            "runtime-ai-planner-behavior-card-flow-board-left-lane";
        internal const string BehaviorCardFlowBoardMainLaneName =
            "runtime-ai-planner-behavior-card-flow-board-main-lane";
        internal const string BehaviorCardFlowBoardRightLaneName =
            "runtime-ai-planner-behavior-card-flow-board-right-lane";
        internal const string BehaviorCardFlowBoardBranchChipName =
            "runtime-ai-planner-behavior-card-flow-board-branch-chip";
        internal const string BehaviorCardFlowBoardConnectorName =
            "runtime-ai-planner-behavior-card-flow-board-connector";
        internal const string BehaviorCardFlowBoardEndName =
            "runtime-ai-planner-behavior-card-flow-board-end";
        internal const string BehaviorCardFlowchartCanvasName =
            "runtime-ai-planner-behavior-card-flowchart-canvas";
        internal const string BehaviorCardFlowchartNodeLayerName =
            "runtime-ai-planner-behavior-card-flowchart-node-layer";
        internal const string BehaviorCardFlowchartEdgeLayerName =
            "runtime-ai-planner-behavior-card-flowchart-edge-layer";
        internal const string BehaviorCardFlowchartActionNodeName =
            "runtime-ai-planner-behavior-card-flowchart-action-node";
        internal const string BehaviorCardFlowchartStartNodeName =
            "runtime-ai-planner-behavior-card-flowchart-start-node";
        internal const string BehaviorCardFlowchartDecisionNodeName =
            "runtime-ai-planner-behavior-card-flowchart-decision-node";
        internal const string BehaviorCardFlowchartEndNodeName =
            "runtime-ai-planner-behavior-card-flowchart-end-node";
        internal const string BehaviorCardFlowchartEdgeName =
            "runtime-ai-planner-behavior-card-flowchart-edge";
        internal const string BehaviorCardFlowchartEdgeLabelName =
            "runtime-ai-planner-behavior-card-flowchart-edge-label";
        internal const string BehaviorCardGraphCanvasName =
            "runtime-ai-planner-behavior-card-graph-canvas";
        internal const string BehaviorCardGraphNodeLayerName =
            "runtime-ai-planner-behavior-card-graph-node-layer";
        internal const string BehaviorCardGraphEdgeLayerName =
            "runtime-ai-planner-behavior-card-graph-edge-layer";
        internal const string BehaviorCardGraphEdgeName =
            "runtime-ai-planner-behavior-card-graph-edge";
        internal const string BehaviorCardGraphEdgeLabelName =
            "runtime-ai-planner-behavior-card-graph-edge-label";
        internal const string BehaviorCardChainConnectorName =
            "runtime-ai-planner-behavior-card-chain-connector";
        internal const string BehaviorCardPathForecastRowName =
            "runtime-ai-planner-behavior-card-path-forecast-row";
        internal const string BehaviorCardForecastTableName =
            "runtime-ai-planner-behavior-card-forecast-table";
        internal const string BehaviorCardFlowTimeRulerName =
            "runtime-ai-planner-behavior-card-flow-time-ruler";
        internal const string BehaviorCardBranchLegendName =
            "runtime-ai-planner-behavior-card-branch-legend";
        internal const string BehaviorCardTerminalPanelName =
            "runtime-ai-planner-behavior-card-terminal-panel";
        internal const string BehaviorCardConditionWindowFieldName =
            "runtime-ai-planner-behavior-card-condition-window";
        internal const string BehaviorCardStepInspectorName =
            "runtime-ai-planner-behavior-card-step-inspector";
        internal const string BehaviorCardTimelinePreviewName =
            "runtime-ai-planner-behavior-card-timeline-preview";
        internal const string BehaviorCardForecastName =
            "runtime-ai-planner-behavior-card-forecast";
        internal const string BehaviorCardSearchFieldName =
            "runtime-ai-planner-behavior-card-search";
        internal const string BehaviorCardCapabilityFilterName =
            "runtime-ai-planner-behavior-card-capability-filter";
        internal const string BehaviorCardAddStepButtonName =
            "runtime-ai-planner-behavior-card-add-step";
        internal const string BehaviorCardDeleteStepButtonName =
            "runtime-ai-planner-behavior-card-delete-step";
        internal const string BehaviorCardSaveDraftButtonName =
            "runtime-ai-planner-behavior-card-save-draft";
        internal const string RuntimeAiBehaviorSelectionPhaseButtonName =
            "runtime-ai-planner-behavior-selection-phase-button";
        internal const string RuntimeAiBehaviorLoadoutPanelButtonName =
            "runtime-ai-planner-behavior-loadout-panel-button";
        private const string EmptyStateChoice = "无";
        private const int StateTabIndex = 0;
        private const int ActionTabIndex = 1;
        private const int PerceptionTabIndex = 2;
        private const int BehaviorGroupPreviewTabIndex = 3;
        private const int BehaviorCompositionTabIndex = 4;
        private const int BehaviorCardEditorTabIndex = 5;
        private const int ValidationTabIndex = 6;
        private const int PlayMonitorTabIndex = 7;
        private const string BehaviorHotUpdatePrefKey =
            "MxFramework.CharacterTest.RuntimeAiPlanner.BehaviorCompositionHotUpdate";
        private const string BehaviorGroupGuideFoldoutPrefKey =
            "MxFramework.CharacterTest.RuntimeAiPlanner.BehaviorGroupGuideFoldout";
        private const string BehaviorGroupEditFoldoutPrefKey =
            "MxFramework.CharacterTest.RuntimeAiPlanner.BehaviorGroupEditFoldout";

        private static readonly CharacterTestRuntimeAiPlannerConditionTemplate[] CommonConditionTemplates =
        {
            CharacterTestRuntimeAiPlannerConditionTemplate.TargetVisible,
            CharacterTestRuntimeAiPlannerConditionTemplate.TargetBlocked,
            CharacterTestRuntimeAiPlannerConditionTemplate.TargetRemembered,
            CharacterTestRuntimeAiPlannerConditionTemplate.InAttackRange,
            CharacterTestRuntimeAiPlannerConditionTemplate.ActionNotRunning,
            CharacterTestRuntimeAiPlannerConditionTemplate.NearHazard,
            CharacterTestRuntimeAiPlannerConditionTemplate.NearUsefulObject
        };

        private readonly string[] _tabNames =
        {
            "状态", "动作", "感知", "行为组预览", "行为组编辑", "行为卡", "校验", "Play Monitor"
        };
        private readonly CharacterTestRuntimeAiPlannerEditorModel _model =
            new CharacterTestRuntimeAiPlannerEditorModel();
        private CharacterTestRuntimeAiPlannerBehaviorCompositionProfile _behaviorCompositionProfile;

        private string _profileProjectPath = CharacterTestRuntimeAiPlannerProfileJson.DefaultProjectAssetPath;
        private string _profileLoadFailure = string.Empty;
        private TextField _profilePathField;
        private CharacterTestRuntimeAiPlannerProfilePicker _profilePicker;
        private ToolbarToggle[] _tabToggles;
        private VisualElement _leftPanel;
        private VisualElement _centerPanel;
        private VisualElement _rightPanel;
        private Label _statusLabel;
        private int _activeTab;
        private string _selectedStateId = CharacterTestRuntimeAiPlannerProfileSource.ChaseStateId.ToString();
        private int _selectedTransitionIndex = -1;
        private int _selectedActionIndex = -1;
        private int _selectedAgentIndex = -1;
        private string _actionFilter = string.Empty;
        private string _lastReport = string.Empty;
        private string _lastBehaviorCompositionReport = string.Empty;
        private List<CharacterTestRuntimeAiPlannerBehaviorCardDraft> _behaviorCardDrafts;
        private int _selectedBehaviorCardIndex;
        private int _selectedBehaviorCardStepIndex;
        private string _selectedBehaviorCardBranchId = string.Empty;
        private string _behaviorCardSearchText = string.Empty;
        private string _behaviorCardCapabilityFilter = "All";
        private string _behaviorCardStatusText = "Editor-only 草稿，未发布";
        private Vector2 _behaviorCardEditorScrollOffset;
        private string _statusText = "未加载 Profile JSON";
        private bool _profileLoaded;
        private bool _dirty;
        private float _flowPreviewZoom = CharacterTestRuntimeAiPlannerFlowPreview.AutoZoom;
        private float _behaviorGroupFlowPreviewZoom = CharacterTestRuntimeAiPlannerFlowPreview.AutoZoom;
        private string _selectedBehaviorGroupPreviewCandidateKey = BehaviorGroupPreviewCandidateCurrentDraftKey;
        private readonly List<CharacterTestRuntimeAiPlannerPlayMonitorSnapshotEntry> _playMonitorSnapshotEntries =
            new List<CharacterTestRuntimeAiPlannerPlayMonitorSnapshotEntry>();
        private readonly List<CharacterTestRuntimeAiPlannerPlayMonitorAgentEntry> _playMonitorAgentEntries =
            new List<CharacterTestRuntimeAiPlannerPlayMonitorAgentEntry>();

        [MenuItem(MenuPath, priority = 140)]
        public static void Open()
        {
            CharacterTestRuntimeAiPlannerWindow window =
                GetWindow<CharacterTestRuntimeAiPlannerWindow>();
            window.titleContent = new GUIContent("Runtime AI Planner");
            window.minSize = new Vector2(1060f, 620f);
            window.Show();
        }

        public static void OpenProfile(string projectAssetPath)
        {
            CharacterTestRuntimeAiPlannerWindow window =
                GetWindow<CharacterTestRuntimeAiPlannerWindow>();
            window.titleContent = new GUIContent("Runtime AI Planner");
            window.minSize = new Vector2(1060f, 620f);
            window.Show();

            if (string.IsNullOrWhiteSpace(projectAssetPath))
                return;

            string normalized = NormalizeProjectAssetPath(projectAssetPath);
            window.SetProfilePath(normalized);
            window.LoadProfileFromProjectPath(normalized);
        }

        public void CreateGUI()
        {
            InitializeProfilePathFromSceneDefault();
            rootVisualElement.Clear();
            rootVisualElement.style.paddingLeft = 10;
            rootVisualElement.style.paddingRight = 10;
            rootVisualElement.style.paddingTop = 8;
            rootVisualElement.style.paddingBottom = 8;

            rootVisualElement.Add(CreateToolbar());
            rootVisualElement.Add(CreateTabBar());
            rootVisualElement.Add(CreateMainArea());

            _statusLabel = new Label();
            _statusLabel.style.marginTop = 6;
            _statusLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            rootVisualElement.Add(_statusLabel);

            TryAutoLoadSelectedProfile();
            RefreshView();
        }

    }
}
