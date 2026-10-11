using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using MxFramework.CharacterAction;
using MxFramework.Network;
using MxFramework.Runtime;
using MxFramework.Vfx;
using UnityEngine;

namespace MxFramework.Game.CharacterTest
{
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(93)]
    [AddComponentMenu("MxFramework/Game/Character Test/Runtime AI Lab Scene Controller")]
    public sealed partial class CharacterTestRuntimeAiPlannerDevSceneController : MonoBehaviour
    {
        internal string SimulationPacingStatus => _context?.FixedTickDriver.LastBudgetExhausted == true
            ? "性能保护：最近推进 " + _context.FixedTickDriver.LastExecutedSteps + " 步，播放可能慢于实时。"
            : string.Empty;

        private const int DefaultSelectedAgentIndex = 0;
        private const int MaxSessionInitializationFrames = 8;
        private static readonly Vector2[] DefaultTeam1SetupSpawns =
        {
            new Vector2(-27f, 14f),
            new Vector2(-27f, 10.5f),
            new Vector2(-27f, 17.5f)
        };

        private static readonly Vector2[] DefaultTeam2SetupSpawns =
        {
            new Vector2(27f, 14f),
            new Vector2(27f, 10.5f),
            new Vector2(27f, 17.5f)
        };

        private CharacterTestRuntimeAiPlannerDevFrameBuffer _frameBuffer =
            new CharacterTestRuntimeAiPlannerDevFrameBuffer();
        private CharacterTestRuntimeAiPlannerDevTimeline _timeline =
            new CharacterTestRuntimeAiPlannerDevTimeline();
        private CharacterTestArenaMatchConfig _matchConfig = CreateRuntimeAiLabMatchConfig();
        private readonly RuntimeAiLabTestStateCaptureStore _testStateCaptures =
            new RuntimeAiLabTestStateCaptureStore();
        private CharacterTestRuntimeAiLabDraftPersistence _draftPersistence =
            new CharacterTestRuntimeAiLabDraftPersistence();
        private bool _usesTransientDraftPersistence;
        private readonly CharacterTestRuntimeAiLabBehaviorCardCompileCache _behaviorCardCompileCache =
            new CharacterTestRuntimeAiLabBehaviorCardCompileCache();
        private readonly CharacterTestRuntimeAiLabFrameSelectionCoordinator _frameSelection =
            new CharacterTestRuntimeAiLabFrameSelectionCoordinator();
        private CharacterTestRuntimeAiLabDraftWorkspace _draftWorkspace;
        private CharacterTestRuntimeAiLabBehaviorCardReferenceCatalog _behaviorCardReferences;
        private CharacterActionConfigRegistry _authoringActionRegistry;
        private ICharacterTestRuntimeAiLabPlayerLegalitySource _playerLegalitySource;
        private ICharacterTestRuntimeAiLabPublishSource _publishSource;
        private CharacterTestRuntimeAiPlannerProjectCatalogAuthoringSession _projectCatalogAuthoring;
        private readonly Dictionary<string, CharacterTestRuntimeAiLabBehaviorCardDraft>
            _standaloneProjectCardDrafts = new Dictionary<string, CharacterTestRuntimeAiLabBehaviorCardDraft>(
                StringComparer.Ordinal);
        private int _standaloneProjectCardDraftRevision;
        private string _lastPublishStatus = "尚未发布当前草稿。";
        private string _lastPublishStableCardId = string.Empty;
        private bool _hasLastPublishAttempt;
        private bool _lastPublishSucceeded;
        private bool _hasPlayerLegalityCache;
        private CharacterTestRuntimeAiLabBehaviorGroupDraft _cachedLegalityGroup;
        private int _cachedLegalityWorkspaceRevision;
        private int _cachedLegalityGroupRevision;
        private int _cachedLegalityCharacterId;
        private long _cachedLegalitySourceIdentity;
        private CharacterTestRuntimeAiLabPlayerLegalityState _cachedPlayerLegality;
        private bool _hasPublishPreflightCache;
        private CharacterTestRuntimeAiLabBehaviorGroupDraft _cachedPublishGroup;
        private CharacterTestRuntimeAiLabBehaviorCardDraft _cachedPublishCard;
        private ICharacterTestRuntimeAiLabPublishSource _cachedPublishSource;
        private int _cachedPublishWorkspaceRevision;
        private int _cachedPublishGroupRevision;
        private int _cachedPublishCardRevision;
        private int _cachedPublishEntryIndex;
        private int _cachedPublishPhase;
        private long _cachedPublishReferenceHash;
        private long _cachedPublishLegalityIdentity;
        private bool _cachedCanPublish;
        private string _cachedPublishReason = string.Empty;

        private GameManager _gameManager;
        private CharacterTestRuntimeContext _context;
        private CharacterTestRuntimeSessionBuilder _sessionBuilder;
        private CharacterTestRuntimeAiPlannerDevScenePhase _phase =
            CharacterTestRuntimeAiPlannerDevScenePhase.Setup;
        private string _statusMessage = "请完成配置并启动运行时智能实验室测试。";
        private int _lastCapturedRuntimeFrame = int.MinValue;
        private int _selectedAgentIndex;
        private bool _debugEditGateEnabled;
        private bool _previewOverlayEnabled = true;
        private bool _detailsExpanded;
        private bool _seekInProgress;
        private int _selectedSetupTeamId = CharacterTestArenaMatchConfig.Team1Id;
        private int _selectedSetupSlotIndex;
        private bool _setupSnapEnabled = true;
        private CharacterTestNetworkSnapshotParticipant _snapshotParticipant;
        private CharacterTestArenaMatchConfig _replayBootstrapConfig;
        private int _lastTestStateCapturedFrame = int.MinValue;
        private RuntimeAiLabDeterministicReplayResult _lastReplayResult;
        private int _replayBootstrapEndFrame;
        private RuntimeAiLabInitialReplayBaseline _initialReplayBaseline;
        private int _replayConfigurationRevision;
        private CharacterTestRuntimeAiLabShellCoordinator _shellCoordinator;
        private long _shellGeneration;
        private CharacterTestRuntimeAiPlannerBehaviorCoreSlotKind _coreSlotReplacementKind =
            CharacterTestRuntimeAiPlannerBehaviorCoreSlotKind.Flexible;

        public CharacterTestRuntimeAiPlannerDevScenePhase Phase => CardTrialOwnsSession ? CharacterTestRuntimeAiPlannerDevScenePhase.Running : _phase;
        internal CharacterTestRuntimeAiPlannerDevFrameBuffer FrameBuffer => _frameBuffer;
        internal CharacterTestRuntimeAiPlannerDevTimeline Timeline => _timeline;
        public int SelectedAgentIndex => _selectedAgentIndex;
        public bool DebugEditGateEnabled => _debugEditGateEnabled;
        public bool PreviewOverlayEnabled => _previewOverlayEnabled;
        public bool DetailsExpanded => _detailsExpanded;
        public bool LoopPlaybackActive => _loopPlaybackActive;
        public int SelectedSetupTeamId => _selectedSetupTeamId;
        public int SelectedSetupSlotIndex => _selectedSetupSlotIndex;
        public bool SetupSnapEnabled => _setupSnapEnabled;
        public string StatusMessage => _statusMessage;
        public bool StateCaptureEnabled => _testStateCaptures.Enabled;
        public bool HasSelectedTestStateCapture => _timeline.HasSelection
            && _testStateCaptures.TryGet(_timeline.SelectedFrame, out _);
        public string FrameSyncStatusText => _lastReplayResult.Authority == null
            ? "帧同步：尚未校验"
            : _lastReplayResult.Authority.Passed ? "帧同步：通过" : "帧同步：失败";
        public string PresentationStatusText => _lastReplayResult.Presentation == null
            ? "表现状态：尚未采集"
            : _lastReplayResult.Presentation.Passed ? "表现状态：匹配" : "表现状态：不匹配";
        public bool ShouldAutoOpenPanel => true;
        internal CharacterTestArenaMatchConfig MatchConfig => _matchConfig;
        internal CharacterTestRuntimeAiPlannerProjectCatalogState ProjectCatalogState =>
            _projectCatalogAuthoring?.State ?? _matchConfig?.ProjectCatalogState;
        internal int StandaloneProjectCardDraftRevision => _standaloneProjectCardDraftRevision;
        /// <remarks>LowFreqAlloc on the first authoring-only lookup; NoAlloc after initialization.</remarks>
        internal bool TryResolveCharacterActionPresentation(
            CharacterActionId actionId,
            out string displayName,
            out int durationFrames)
        {
            CharacterActionConfigRegistry registry = ResolveCharacterActionRegistry();
            if (registry != null && registry.TryGetAction(actionId, out CharacterActionConfig config))
            {
                displayName = config.DisplayName ?? string.Empty;
                durationFrames = config.DurationFrames.HasValue
                    ? Math.Max(0, config.DurationFrames.Value)
                    : 0;
                return true;
            }

            displayName = string.Empty;
            durationFrames = 0;
            return false;
        }

        /// <remarks>LowFreqAlloc only when the immutable reference catalog changes.</remarks>
        internal bool TryGetBehaviorRecipeCharacterActionCatalog(
            out IReadOnlyList<CharacterActionConfig> actions,
            out long stableHash)
        {
            EnsureBehaviorCardReferences();
            if (_behaviorCardReferences == null)
            {
                actions = Array.Empty<CharacterActionConfig>();
                stableHash = 0L;
                return false;
            }

            actions = _behaviorCardReferences.CharacterActions;
            stableHash = _behaviorCardReferences.StableHash;
            return true;
        }

        /// <remarks>NoAlloc. Latest-frame presentation projection only.</remarks>
        internal bool TryCaptureActiveActionProgress(
            int entityId,
            out CharacterTestRuntimeAiLabActiveActionProgress progress)
        {
            IReadOnlyList<CharacterTestCharacterEntity> entities = _context?.Slice?.Services.CharacterEntities;
            for (int i = 0; entities != null && i < entities.Count; i++)
            {
                CharacterTestCharacterEntity entity = entities[i];
                if (entity == null || entity.Id.Value != entityId)
                    continue;

                CharacterTestCharacterActionRuntime actionRuntime =
                    _context?.Slice?.Services.ActionRuntime;
                if (actionRuntime == null
                    || !actionRuntime.TryGetActiveActionPresentationState(
                        entity.Id,
                        out long instanceId,
                        out CharacterActionId actionId,
                        out int localFrame,
                        out int durationFrames))
                    break;

                progress = new CharacterTestRuntimeAiLabActiveActionProgress(
                    entityId,
                    instanceId,
                    actionId,
                    localFrame,
                    durationFrames);
                return true;
            }

            progress = default;
            return false;
        }

        /// <remarks>NoAlloc. Latest-frame presentation query over the retained authority snapshot.</remarks>
        internal bool TryResolveLatestCommittedBehaviorEntryIndex(out int entryIndex)
        {
            entryIndex = -1;
            if (IsHistoricalFrameSelected
                || !_frameBuffer.TryGetSelected(out CharacterTestRuntimeAiPlannerDevFrameSnapshot frame)
                || frame == null
                || frame.Frame != _timeline.LatestFrame
                || frame.Agents == null
                || _selectedAgentIndex < 0
                || _selectedAgentIndex >= frame.Agents.Count)
            {
                return false;
            }

            CharacterTestRuntimeAiPlannerAgentSnapshot selectedAgent =
                frame.Agents[_selectedAgentIndex];
            CharacterTestRuntimeAiPlannerStateSnapshot state =
                frame.RuntimeAiPlannerStateSnapshot;
            CharacterTestRuntimeAiLabLogicSlotStateSnapshot[] slots =
                state?.BehaviorLogicSlots;
            CharacterTestRuntimeAiLabLogicAgentStateSnapshot logicAgent = null;
            for (int slotIndex = 0; slots != null && slotIndex < slots.Length; slotIndex++)
            {
                CharacterTestRuntimeAiLabLogicSlotStateSnapshot slot = slots[slotIndex];
                if (slot == null || slot.Slot != selectedAgent.RuntimeAiPlannerProfileSlot)
                    continue;
                CharacterTestRuntimeAiLabLogicAgentStateSnapshot[] agents = slot.Agents;
                for (int agentIndex = 0; agents != null && agentIndex < agents.Length; agentIndex++)
                {
                    CharacterTestRuntimeAiLabLogicAgentStateSnapshot candidate = agents[agentIndex];
                    if (candidate != null && candidate.EntityId == selectedAgent.EntityId)
                    {
                        logicAgent = candidate;
                        break;
                    }
                }
                break;
            }

            if (logicAgent == null || logicAgent.ActiveBindingIndex < 0)
                return false;
            if (!DraftWorkspace.TryGetGroupForSlot(
                    SelectedSetupTeamId,
                    SelectedSetupSlotIndex,
                    out CharacterTestRuntimeAiLabBehaviorGroupDraft group)
                || group == null
                || logicAgent.ActiveBindingIndex >= group.Entries.Count)
            {
                return false;
            }

            entryIndex = logicAgent.ActiveBindingIndex;
            return true;
        }

        internal bool TryResolveSetupRuntimeAiPlannerProfileSlot(
            int teamId,
            int slotIndex,
            out int profileSlot)
        {
            if (_sessionBuilder != null)
            {
                return _sessionBuilder.TryResolveArenaRuntimeAiPlannerProfileSlot(
                    teamId,
                    slotIndex,
                    out profileSlot);
            }

            profileSlot = 0;
            return false;
        }
        internal CharacterTestRuntimeAiLabDraftWorkspace DraftWorkspace
        {
            get
            {
                if (_draftWorkspace != null)
                    return _draftWorkspace;
#if UNITY_EDITOR
                if (HasAuthoringWorkspaceRoleOpenFailure)
                    return null;
#endif
                return _draftWorkspace = CharacterTestRuntimeAiLabDraftWorkspace.Import(_matchConfig);
            }
        }
        internal string DraftPersistenceStatus => _draftPersistence.LastStatus;
        internal bool DraftLastSaveWasAutomatic => _draftPersistence?.LastSaveWasAutomatic == true;
        internal string DraftPersistenceRoot => _draftPersistence.DraftsRoot;
        internal bool UsesTransientDraftPersistence => _usesTransientDraftPersistence;

        internal bool TryGetAvailableDraftWorkspace(
            out CharacterTestRuntimeAiLabDraftWorkspace workspace,
            out string diagnostic)
        {
            workspace = _draftWorkspace;
#if UNITY_EDITOR
            if (!IsConfiguredTestCardEditing && _authoringWorkspaceRoles != null
                && _authoringWorkspaceRoles.Enabled
                && (!_authoringWorkspaceRoles.HasActiveRole
                    || workspace == null
                    || _authoringWorkspaceRoles.ActiveWorkspace == null
                    || !string.Equals(
                        workspace.WorkspaceId,
                        _authoringWorkspaceRoles.ActiveWorkspace.WorkspaceId,
                        StringComparison.Ordinal)))
            {
                workspace = null;
                diagnostic = !_initialOpenResult.Success
                    && _initialOpenResult.Status
                        != CharacterTestRuntimeAiLabAuthoringWorkspaceRoleStatus.NotStarted
                    ? _initialOpenResult.Message
                    : !_lastOperationResult.Success
                        && _lastOperationResult.Status
                            != CharacterTestRuntimeAiLabAuthoringWorkspaceRoleStatus.NotStarted
                        ? _lastOperationResult.Message
                        : "Runtime AI Lab authoring workspace is unavailable.";
                return false;
            }
#endif
            if (workspace == null)
            {
                diagnostic = "Runtime AI Lab draft workspace is unavailable.";
                return false;
            }
            diagnostic = string.Empty;
            return true;
        }

        internal bool FlushDraftWorkspace(out string message)
        {
            using var startupProfile = CharacterTestStartupProfiler.SaveDraft.Auto();
            bool saved = _draftPersistence.Flush();
            message = _draftPersistence.LastStatus;
            return saved;
        }
        internal string LastPublishStatus => _lastPublishStatus;
        internal string LastPublishStableCardId => _lastPublishStableCardId;
        internal bool HasLastPublishAttempt => _hasLastPublishAttempt;
        internal bool LastPublishSucceeded => _lastPublishSucceeded;
        internal int PublishPreflightEvaluationCount { get; private set; }
        internal CharacterTestRuntimeAiLabShellCoordinator ShellCoordinator => _shellCoordinator;
        internal CharacterTestRuntimeAiPlannerBehaviorCoreSlotKind CoreSlotReplacementKind =>
            _coreSlotReplacementKind;
        internal bool IsCoreSlotReplacementActive =>
            CharacterTestRuntimeAiPlannerBehaviorCoreSlotPolicy.IsCore(_coreSlotReplacementKind);
        internal void ConfigurePlayerLegalitySource(ICharacterTestRuntimeAiLabPlayerLegalitySource source)
        {
            _playerLegalitySource = source;
            InvalidateStageEditAuthority();
            _hasPlayerLegalityCache = false;
            _hasPublishPreflightCache = false;
        }

        internal void ConfigurePublishSource(ICharacterTestRuntimeAiLabPublishSource source)
        {
            _publishSource = source;
            InvalidateStageEditAuthority();
            _hasPublishPreflightCache = false;
        }

        internal CharacterTestRuntimeAiLabPlayerLegalityState EvaluateSelectedPlayerLegality(
            CharacterTestRuntimeAiLabBehaviorGroupDraft group)
        {
            CharacterTestArenaSlotConfig slot = _matchConfig?.TryGetSlot(
                _selectedSetupTeamId,
                _selectedSetupSlotIndex);
            if (_playerLegalitySource == null || slot == null)
                return CharacterTestRuntimeAiLabPlayerLegalityState.Unavailable(
                    "玩家卡组上下文尚未接入 Runtime AI Lab。");
            CharacterTestRuntimeAiLabDraftWorkspace workspace = DraftWorkspace;
            long sourceIdentity = _playerLegalitySource.ProjectionIdentity;
            if (_hasPlayerLegalityCache
                && ReferenceEquals(_cachedLegalityGroup, group)
                && _cachedLegalityWorkspaceRevision == workspace.CurrentRevision
                && _cachedLegalityGroupRevision == (group != null ? group.CurrentRevision : -1)
                && _cachedLegalityCharacterId == slot.CharacterId
                && _cachedLegalitySourceIdentity == sourceIdentity)
            {
                return _cachedPlayerLegality;
            }

            _cachedPlayerLegality = _playerLegalitySource.Evaluate(
                slot.CharacterId,
                group,
                workspace.Cards);
            _cachedLegalityGroup = group;
            _cachedLegalityWorkspaceRevision = workspace.CurrentRevision;
            _cachedLegalityGroupRevision = group != null ? group.CurrentRevision : -1;
            _cachedLegalityCharacterId = slot.CharacterId;
            _cachedLegalitySourceIdentity = sourceIdentity;
            _hasPlayerLegalityCache = true;
            return _cachedPlayerLegality;
        }
        internal void ReportPlayerDeckLayerUnavailable(string reason)
        {
            _statusMessage = string.IsNullOrWhiteSpace(reason)
                ? "玩家行为卡组不可用，已保留 Runtime AI Lab 配置页。"
                : "玩家行为卡组不可用：" + reason;
        }
        internal CharacterTestRuntimeAiPlannerSnapshot CaptureRuntimeAiPlannerSnapshotForLab()
        {
            return _context?.Slice?.RuntimeAiPlannerCommands?.CreateSnapshot();
        }

        internal CharacterTestRuntimeAiPlannerStateSnapshot CaptureRuntimeAiPlannerStateForLab()
        {
            return _context?.Slice?.Services.Modules.RuntimeAiPlannerCommands?.CreateStateSnapshot();
        }

        internal CharacterTestRuntimeAiLabLogicEventBuffer LatestBehaviorLogicEventsForLab =>
            CollectLatestBehaviorLogicEvents(_context?.Slice?.Services.Modules.RuntimeAiPlannerCommands);

        private static CharacterTestArenaMatchConfig CreateRuntimeAiLabMatchConfig()
        {
            CharacterTestArenaMatchConfig config = CharacterTestArenaMatchConfig.CreateProjectBacked();
            config.SetSpawnPositions(
                DefaultTeam1SetupSpawns,
                DefaultTeam2SetupSpawns);
            return config;
        }

        internal void Initialize(
            GameManager gameManager,
            CharacterTestRuntimeContext context,
            CharacterTestRuntimeSessionBuilder sessionBuilder)
        {
            _gameManager = gameManager;
            _context = context;
            _sessionBuilder = sessionBuilder;
            _selectedAgentIndex = DefaultSelectedAgentIndex;
            _behaviorCardReferences = null;
            _projectCatalogAuthoring = new CharacterTestRuntimeAiPlannerProjectCatalogAuthoringSession(
                _matchConfig?.ProjectCatalogState);
            _behaviorCardCompileCache.Clear();
            InvalidateStageEditAuthority();
            _shellGeneration = DateTime.UtcNow.Ticks;
            _shellCoordinator = null;
        }

        internal CharacterTestRuntimeAiLabShellCoordinator EnsureShellCoordinator(
            bool playerDeckAvailable,
            bool labAvailable = true,
            CharacterTestRuntimeAiLabWorkspace initialWorkspace =
                CharacterTestRuntimeAiLabWorkspace.PlayerDeck)
        {
            if (_shellCoordinator == null)
            {
                if (_shellGeneration <= 0)
                    _shellGeneration = DateTime.UtcNow.Ticks;
                _shellCoordinator = new CharacterTestRuntimeAiLabShellCoordinator(
                    _shellGeneration,
                    playerDeckAvailable,
                    labAvailable,
                    initialWorkspace);
            }
            return _shellCoordinator;
        }

        /// <summary>
        /// Prepares the Lab setup. Direct callers default to isolated persistence so tests and
        /// previews cannot replace the user's recovery pointer. The scene composition root is the
        /// only production caller and explicitly opts into persisted-draft recovery.
        /// </summary>
        internal void PrepareInitialSetupState(bool restorePersistedDraft = false)
        {
            ConfigureDraftPersistence(restorePersistedDraft);
            if (!restorePersistedDraft)
                _matchConfig = CreateRuntimeAiLabMatchConfig();
            string setupFailure = string.Empty;
#if UNITY_EDITOR
            if (Application.isPlaying && restorePersistedDraft)
            {
                CharacterTestRuntimeAiLabAuthoringWorkspaceRoleResult roleResult =
                    OpenInitialAuthoringWorkspaceRole();
                if (roleResult.Success)
                    _draftWorkspace = roleResult.Workspace;
                else
                    setupFailure = roleResult.Message;
            }
            else
#endif
            {
                _draftWorkspace = Application.isPlaying
                    ? restorePersistedDraft
                        ? _draftPersistence.OpenOrCreate(_matchConfig)
                        : _draftPersistence.CreateFromCurrent(_matchConfig)
                    : CharacterTestRuntimeAiLabDraftWorkspace.Import(_matchConfig);
            }
            if (_draftWorkspace != null)
            {
                _frameLimit = _draftWorkspace.FrameLimit;
                if (_draftWorkspace.EnsurePublishedProjectCardDrafts(
                        _matchConfig?.ProjectCatalogState?.CardCatalog) && Application.isPlaying)
                {
                    _draftPersistence.MarkDirty();
                }
                ResetBehaviorRecipeSaveCheckpoint(_draftWorkspace);
                _draftWorkspace.RestoreSetupToArena(_matchConfig);
            }
            InvalidateStageEditAuthority();
            _behaviorCardCompileCache.Clear();
            _phase = CharacterTestRuntimeAiPlannerDevScenePhase.Setup;
            _statusMessage = setupFailure.Length > 0
                ? setupFailure
                : "准备就绪。启动测试后将开始捕获运行时智能规划帧。";
            _selectedAgentIndex = DefaultSelectedAgentIndex;
            _debugEditGateEnabled = false;
            _previewOverlayEnabled = true;
            _detailsExpanded = false;
            _selectedSetupTeamId = CharacterTestArenaMatchConfig.Team1Id;
            _selectedSetupSlotIndex = 0;
            _setupSnapEnabled = true;
            _frameBuffer.Clear();
            _timeline.Clear();
            ResetTimelineSessionState();
            ResetLoopPlaybackState();
            ResetHistoryPlaybackState();
            CancelBranchRecompute();
            _lastCapturedRuntimeFrame = int.MinValue;
            _snapshotParticipant = null;
            _replayBootstrapConfig = null;
            _testStateCaptures.Clear();
            _lastTestStateCapturedFrame = int.MinValue;
            _lastReplayResult = default;
            _replayBootstrapEndFrame = 0;
            _initialReplayBaseline = null;
            _replayConfigurationRevision++;
            ResetAuthoringRuntimeState();
            RestoreUnityTimeScale();
            SetPaused(true);
        }

        private void ConfigureDraftPersistence(bool restorePersistedDraft)
        {
            if (restorePersistedDraft)
            {
                if (_usesTransientDraftPersistence)
                {
                    _draftPersistence = new CharacterTestRuntimeAiLabDraftPersistence();
#if UNITY_EDITOR
                    ResetAuthoringWorkspaceRoleSession();
#endif
                }
                _usesTransientDraftPersistence = false;
                return;
            }

            string isolatedRoot = Path.Combine(
                Path.GetTempPath(),
                "MxFramework",
                "RuntimeAiLab",
                "TransientDrafts",
                Guid.NewGuid().ToString("N"));
            _draftPersistence = new CharacterTestRuntimeAiLabDraftPersistence(isolatedRoot);
            _usesTransientDraftPersistence = true;
#if UNITY_EDITOR
            ResetAuthoringWorkspaceRoleSession();
#endif
        }

        internal CharacterTestRuntimeAiLabBehaviorCardCompileResult CompileBehaviorCard(
            CharacterTestRuntimeAiLabBehaviorCardDraft card)
        {
            EnsureBehaviorCardReferences();

            return _behaviorCardCompileCache.GetOrCompile(
                DraftWorkspace,
                card,
                _behaviorCardReferences);
        }

        internal CharacterTestRuntimeAiLabBehaviorCardLogicCompileResult CompileBehaviorCardLogic(
            CharacterTestRuntimeAiLabBehaviorCardDraft card)
        {
            EnsureBehaviorCardReferences();
            if (card?.HasAuthoredSource == true) return CompileAuthoredCard(card, false);
            return _behaviorCardCompileCache.GetOrCompileLogic(
                DraftWorkspace,
                card,
                _behaviorCardReferences);
        }

        /// <summary>
        /// Validates every low-frequency setup dependency that can block a session before the
        /// runtime transition begins.
        /// </summary>
        /// <remarks>LowFreqAlloc: setup UI and explicit session-start preflight only.</remarks>
        internal bool TryValidateSessionStartConfiguration(out string diagnostic)
        {
            using var startupProfile = CharacterTestStartupProfiler.ValidateSession.Auto();
            bool valid = ValidateSessionStartConfigurationCore(out diagnostic);
            _lastStartCheckKey = StartCheckKey(); _lastStartCheckIssue = diagnostic; _lastStartCheckValid = valid;
            return valid;
        }

        private bool ValidateSessionStartConfigurationCore(out string diagnostic)
        {
            if (!TryGetAvailableDraftWorkspace(
                    out CharacterTestRuntimeAiLabDraftWorkspace workspace,
                    out diagnostic))
            {
                return false;
            }

            CharacterTestRuntimeAiLabSpawnValidationResult spawnValidation = ValidateSetupSpawns();
            if (spawnValidation == null || !spawnValidation.CanStart)
            {
                diagnostic = spawnValidation?.Summary ?? "测试出生点校验不可用。";
                return false;
            }

            if (ProjectCatalogState?.Success != true)
            {
                diagnostic = "项目行为目录加载失败："
                    + (string.IsNullOrWhiteSpace(ProjectCatalogState?.Diagnostic)
                        ? "没有可用的行为卡或行为组目录。"
                        : ProjectCatalogState.Diagnostic);
                return false;
            }

            if (!_matchConfig.BehaviorGroups.TryCompileAll(out diagnostic))
                return false;

            if (!TryResolveCurrentBehaviorLoadoutsForSessionStart(
                    out CharacterTestRuntimeAiPlannerBehaviorLoadoutRuntime loadouts,
                    out diagnostic))
            {
                return false;
            }
            if (_context?.Slice != null
                && !_matchConfig.TryBuildSpawnSpecs(loadouts, out _, out diagnostic))
            {
                if (IsBehaviorLoadoutFailure(diagnostic))
                    diagnostic = FormatBehaviorLoadoutSessionDiagnostic(diagnostic, loadouts);
                return false;
            }

            if (!TryGetSelectedAuthoringGroup(out CharacterTestRuntimeAiLabBehaviorGroupDraft group))
            {
                diagnostic = "当前没有可用于测试的行为卡或行为组。";
                return false;
            }

            if (_singleCardTestRequest.HasValue)
            {
                var request = _singleCardTestRequest.Value;
                if (request.SetupTeamId != _selectedSetupTeamId || request.SetupSlotIndex != _selectedSetupSlotIndex
                    || request.GroupDraftId != group.GroupDraftId
                    || !workspace.Cards.TryGetValue(request.CardDraftId, out var selectedTestCard)
                    || !selectedTestCard.HasAuthoredSource)
                { diagnostic = "单卡测试的角色或卡上下文已变化。"; return false; }
                var selectedCompile = CompileBehaviorCardForStart(selectedTestCard);
                if (selectedCompile?.Success != true || selectedCompile.Program == null)
                { diagnostic = FormatBehaviorCardCompileFailure(selectedTestCard, selectedCompile); return false; }
                diagnostic = string.Empty;
                return true;
            }

            bool responsibilitiesValid = workspace.TryValidateResponsibilityAssignments(
                group,
                out string responsibilityMessage);
            var coreSlotReport = new CharacterTestRuntimeAiPlannerBehaviorCoreSlotReport(
                responsibilitiesValid,
                CharacterTestRuntimeAiPlannerBehaviorCoreSlotKind.Flexible,
                responsibilityMessage);
            if (!coreSlotReport.Success)
            {
                diagnostic = "行为组基础契约不完整：" + coreSlotReport.Message;
                return false;
            }

            CharacterTestRuntimeAiPlannerBehaviorCompileResult compositionCompile =
                CharacterTestRuntimeAiPlannerBehaviorCompositionCompiler.Compile(
                    group.Profile,
                    _matchConfig.BehaviorGroups.DefinitionCatalog);
            if (compositionCompile == null || !compositionCompile.Success)
            {
                diagnostic = "当前行为组的基础组合无法编译。";
                return false;
            }

            // A newly created behavior group is allowed to enter the session before its first
            // card is authored. The compiled planner then produces the normal no-action result.
            if (group.Entries.Count == 0)
            {
                diagnostic = string.Empty;
                return true;
            }

            if (!TryValidateTacticalGroupTargets(group, out diagnostic)) return false;

            int skippedInvalidCardCount = 0;
            for (int i = 0; i < group.Entries.Count; i++)
            {
                CharacterTestRuntimeAiLabBehaviorEntryDraft entry = group.Entries[i];
                if (entry?.RuntimeEntry == null || string.IsNullOrWhiteSpace(entry.CardDraftId))
                {
                    diagnostic = "当前行为组包含损坏的行为卡条目，无法开始测试。";
                    return false;
                }
                if (!workspace.Cards.TryGetValue(
                        entry.CardDraftId,
                        out CharacterTestRuntimeAiLabBehaviorCardDraft card))
                {
                    diagnostic = "当前行为组缺少行为卡：" + entry.BehaviorId;
                    return false;
                }

                CharacterTestRuntimeAiLabBehaviorCardLogicCompileResult cardCompile =
                    card.HasAuthoredSource
                        ? CompileBehaviorCardForStart(card)
                        : _context?.Slice != null ? CompileBehaviorCardLogic(card)
                        : CharacterTestRuntimeAiLabBehaviorCardLogicCompiler.Compile(card.LogicPayload);
                if (cardCompile == null || !cardCompile.Success || cardCompile.Program == null)
                {
                    if (!IsRecoverableLegacyBehaviorCardCompileFailure(cardCompile))
                    {
                        diagnostic = FormatBehaviorCardCompileFailure(card, cardCompile);
                        return false;
                    }
                    skippedInvalidCardCount++;
                    continue;
                }
            }

            diagnostic = skippedInvalidCardCount > 0
                ? "将跳过 "
                    + skippedInvalidCardCount.ToString(CultureInfo.InvariantCulture)
                    + " 张配置错误的行为卡；其余行为可以测试。"
                : string.Empty;
            return true;
        }

        /// <summary>
        /// Locates the first behavior card that prevents the selected setup slot from running so
        /// the setup Start command can route directly into repair authoring.
        /// </summary>
        /// <remarks>LowFreqAlloc: explicit failed session-start recovery only.</remarks>
        internal bool TryFindFirstInvalidBehaviorCardEntry(
            out int entryIndex,
            out string diagnostic)
        {
            entryIndex = -1;
            diagnostic = string.Empty;
            if (!TryGetAvailableDraftWorkspace(
                    out CharacterTestRuntimeAiLabDraftWorkspace workspace,
                    out diagnostic))
            {
                return false;
            }
            if (!workspace.TryGetGroupForSlot(
                    _selectedSetupTeamId,
                    _selectedSetupSlotIndex,
                    out CharacterTestRuntimeAiLabBehaviorGroupDraft group)
                || group == null)
            {
                diagnostic = "当前槽位没有可编辑的行为组。";
                return false;
            }

            for (int i = 0; i < group.Entries.Count; i++)
            {
                CharacterTestRuntimeAiLabBehaviorEntryDraft entry = group.Entries[i];
                if (!workspace.Cards.TryGetValue(
                        entry.CardDraftId,
                        out CharacterTestRuntimeAiLabBehaviorCardDraft card))
                {
                    entryIndex = i;
                    diagnostic = "当前行为组缺少行为卡：" + entry.BehaviorId;
                    return true;
                }

                CharacterTestRuntimeAiLabBehaviorCardLogicCompileResult cardCompile =
                    _context?.Slice != null
                        ? CompileBehaviorCardLogic(card)
                        : CharacterTestRuntimeAiLabBehaviorCardLogicCompiler.Compile(card.LogicPayload);
                if (cardCompile == null || !cardCompile.Success || cardCompile.Program == null)
                {
                    if (IsRecoverableLegacyBehaviorCardCompileFailure(cardCompile))
                        continue;

                    entryIndex = i;
                    diagnostic = FormatBehaviorCardCompileFailure(card, cardCompile);
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Resolves a loadout projection that matches the latest setup behavior-group store.
        /// The setup slice is intentionally not rebuilt here: session start immediately builds
        /// the battle slice, and destroying the FairyGUI tree inside its click callback would
        /// make the visible editor disappear. The fresh projection is the authoritative
        /// preflight input; the battle rebuild creates the same revision for actual execution.
        /// </summary>
        /// <remarks>LowFreqAlloc: explicit setup validation and session start only.</remarks>
        private bool TryResolveCurrentBehaviorLoadoutsForSessionStart(
            out CharacterTestRuntimeAiPlannerBehaviorLoadoutRuntime loadouts,
            out string diagnostic)
        {
            loadouts = _context?.Slice?.Services.Modules.RuntimeAiBehaviorLoadouts;
            CharacterTestArenaBehaviorGroupDraftStore groups = _matchConfig?.BehaviorGroups;
            if (_context?.Slice == null || groups == null)
            {
                diagnostic = string.Empty;
                return true;
            }

            int configuredRevision = groups.Revision;
            int configuredCount = groups.Drafts.Count;
            int runtimeRevision = loadouts?.Catalog?.Revision ?? 0;
            int runtimeCount = loadouts?.Entries?.Count ?? 0;
            if (loadouts != null
                && runtimeRevision == configuredRevision
                && runtimeCount == configuredCount)
            {
                diagnostic = string.Empty;
                return true;
            }

            try
            {
                // This projection only resolves setup loadout identities. Profile slots are
                // rebuilt from the same group store by RebuildRuntimeAiLabBattleSession.
                loadouts = groups.CreateRuntime(
                    runtimeProfile: null,
                    existingProfileBindings: Array.Empty<CharacterTestRuntimeAiPlannerProfileBinding>(),
                    logger: _context.Logger);
            }
            catch (Exception exception)
            {
                diagnostic = FormatBehaviorLoadoutSessionDiagnostic(
                    "按最新行为组配置重建测试装载失败：" + exception.Message,
                    loadouts);
                return false;
            }

            if (loadouts?.Catalog == null
                || loadouts.Catalog.Revision != configuredRevision
                || loadouts.Entries.Count != configuredCount)
            {
                diagnostic = FormatBehaviorLoadoutSessionDiagnostic(
                    "按最新行为组配置重建后，装载身份仍不一致。",
                    loadouts);
                return false;
            }

            diagnostic = string.Empty;
            return true;
        }

        /// <remarks>LowFreqAlloc: setup failure diagnostics only.</remarks>
        private static bool IsBehaviorLoadoutFailure(string diagnostic)
        {
            return !string.IsNullOrWhiteSpace(diagnostic)
                && diagnostic.IndexOf("loadout", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        /// <remarks>LowFreqAlloc: setup failure diagnostics only.</remarks>
        private string FormatBehaviorLoadoutSessionDiagnostic(
            string cause,
            CharacterTestRuntimeAiPlannerBehaviorLoadoutRuntime loadouts)
        {
            CharacterTestArenaSlotConfig slot = _matchConfig?.TryGetSlot(
                _selectedSetupTeamId,
                _selectedSetupSlotIndex);
            int draftId = slot?.BehaviorGroupDraftId ?? 0;
            int expectedLoadoutId = draftId > 0
                ? CharacterTestArenaBehaviorGroupDraftStore.ToSessionLoadoutId(draftId)
                : 0;
            string groupName = "未选择";
            if (_matchConfig?.BehaviorGroups != null
                && _matchConfig.BehaviorGroups.TryGetDraft(
                    draftId,
                    out CharacterTestArenaBehaviorGroupDraft draft)
                && !string.IsNullOrWhiteSpace(draft.DisplayName))
            {
                groupName = draft.DisplayName;
            }

            int configuredRevision = _matchConfig?.BehaviorGroups?.Revision ?? 0;
            int runtimeRevision = loadouts?.Catalog?.Revision ?? 0;
            string detail = string.IsNullOrWhiteSpace(cause)
                ? "测试装载不可用。"
                : cause.Trim();
            return "当前角色的行为组“"
                + groupName
                + "”未能装载到测试会话。需要 Loadout "
                + expectedLoadoutId.ToString(CultureInfo.InvariantCulture)
                + "；配置版本 "
                + configuredRevision.ToString(CultureInfo.InvariantCulture)
                + "，运行装载版本 "
                + runtimeRevision.ToString(CultureInfo.InvariantCulture)
                + "。"
                + detail;
        }

        /// <remarks>LowFreqAlloc: formats authoring diagnostics for setup and explicit compile actions.</remarks>
        private static string FormatBehaviorCardCompileFailure(
            CharacterTestRuntimeAiLabBehaviorCardDraft card,
            CharacterTestRuntimeAiLabBehaviorCardLogicCompileResult compileResult)
        {
            string cardName = string.IsNullOrWhiteSpace(card?.DisplayName)
                ? "未命名行为卡"
                : card.DisplayName;
            CharacterTestRuntimeAiLabCardDiagnostic? firstError = null;
            IReadOnlyList<CharacterTestRuntimeAiLabCardDiagnostic> diagnostics = compileResult?.Diagnostics;
            for (int i = 0; diagnostics != null && i < diagnostics.Count; i++)
            {
                if (diagnostics[i].Severity == CharacterTestRuntimeAiLabCardDiagnosticSeverity.Error)
                {
                    firstError = diagnostics[i];
                    break;
                }
            }

            if (!firstError.HasValue)
                return "行为卡“" + cardName + "”无法用于测试。";

            CharacterTestRuntimeAiLabCardDiagnostic error = firstError.Value;
            if (string.Equals(
                    error.Code,
                    "CARD_V7_EVENT_DURATION_REVIEW_REQUIRED",
                    StringComparison.Ordinal))
            {
                return "行为卡“" + cardName
                    + "”不能测试：请在“编辑逻辑”中确认红色事件的固定时长。";
            }
            if (string.Equals(
                    error.Code,
                    "CARD_V6_EVENT_OPERATION_MISSING",
                    StringComparison.Ordinal))
            {
                return "行为卡“" + cardName
                    + "”不能测试：存在尚未配置操作的事件。请进入“编辑逻辑”配置或删除该事件。";
            }
            string detail = string.Equals(
                    error.Code,
                    "CARD_V2_DETECTOR_CONDITIONS",
                    StringComparison.Ordinal)
                ? "条件触发检测器缺少触发条件"
                : error.Message;
            string path = string.IsNullOrWhiteSpace(error.Path)
                ? string.Empty
                : "（" + error.Path + "）";
            return "行为卡“" + cardName + "”无法用于测试：" + detail + path;
        }

        /// <summary>
        /// Keeps a legacy conditional CharacterAction event editable without allowing an
        /// otherwise-invalid behavior recipe to bypass session-start validation.
        /// </summary>
        private static bool IsRecoverableLegacyBehaviorCardCompileFailure(
            CharacterTestRuntimeAiLabBehaviorCardLogicCompileResult compileResult)
        {
            IReadOnlyList<CharacterTestRuntimeAiLabCardDiagnostic> diagnostics =
                compileResult?.Diagnostics;
            bool foundRecoverableError = false;
            for (int i = 0; diagnostics != null && i < diagnostics.Count; i++)
            {
                CharacterTestRuntimeAiLabCardDiagnostic diagnostic = diagnostics[i];
                if (diagnostic.Severity != CharacterTestRuntimeAiLabCardDiagnosticSeverity.Error)
                    continue;
                if (!string.Equals(
                        diagnostic.Code,
                        "CARD_V8_ACTION_EVENT_CONDITION_UNSUPPORTED",
                        StringComparison.Ordinal))
                {
                    return false;
                }
                foundRecoverableError = true;
            }
            return foundRecoverableError;
        }

        private void EnsureBehaviorCardReferences()
        {
            var abilities = _context?.Slice?.Services.AbilityRuntime?.Definitions;
            CharacterActionConfigRegistry actions = ResolveCharacterActionRegistry();
            if (_behaviorCardReferences == null || !_behaviorCardReferences.Matches(abilities, actions))
            {
                _behaviorCardReferences = new CharacterTestRuntimeAiLabBehaviorCardReferenceCatalog(
                    abilities,
                    actions);
                _behaviorCardCompileCache.Clear();
                _hasPublishPreflightCache = false;
            }
        }

        private CharacterActionConfigRegistry ResolveCharacterActionRegistry()
        {
            CharacterActionConfigRegistry runtimeRegistry =
                _context?.Slice?.Services.ActionRuntime?.Registry;
            if (runtimeRegistry != null)
                return runtimeRegistry;
            // One loaded authoring catalog serves menus, references and tactical data preparation.
            _authoringActionRegistry = CharacterTestTacticalPreparationCatalog.Catalog.Registry;
            return _authoringActionRegistry;
        }

        internal CharacterTestRuntimeAiPlannerDevSceneSnapshot CaptureSnapshot()
        {
            _frameBuffer.TryGetSelected(out CharacterTestRuntimeAiPlannerDevFrameSnapshot selectedFrame);
            return new CharacterTestRuntimeAiPlannerDevSceneSnapshot(
                _phase,
                _statusMessage,
                _context != null && _context.Paused,
                _debugEditGateEnabled,
                _detailsExpanded,
                _loopPlaybackActive,
                _historyPlaybackActive,
                ReplayInProgress,
                _branchRecomputeArmed,
                _branchBaseFrame,
                _branchOriginalLatestFrame,
                _branchConfirmationPending,
                _branchConfirmationGeneration,
                _selectedAgentIndex,
                _frameSelection.Phase,
                _frameSelection.TransactionId,
                _frameSelection.PendingTarget,
                _frameSelection.QueuedTarget,
                _frameSelection.CommittedReceipt,
                _frameBuffer,
                _timeline,
                selectedFrame);
        }

        public bool StartSession()
        {
            return StartSession(CharacterTestRuntimeAiLabSessionStartPresentation.InspectFirstFrame);
        }

        internal bool StartArenaSession()
        {
            return StartSession(CharacterTestRuntimeAiLabSessionStartPresentation.ArenaPaused);
        }

        private bool StartSession(CharacterTestRuntimeAiLabSessionStartPresentation presentation)
        {
            using var startupProfile = CharacterTestStartupProfiler.StartSession.Auto();
            if (!EnsureRuntimeReady())
                return false;

            if (_phase != CharacterTestRuntimeAiPlannerDevScenePhase.Setup)
            {
                _statusMessage = "开始新测试前请先重置。";
                return false;
            }

            if (!TryValidateSessionStartConfiguration(out string validationMessage))
            {
                _statusMessage = validationMessage;
                return false;
            }
            if (!_matchConfig.TryFreezeProjectBehaviorCatalogs(out validationMessage))
            {
                _statusMessage = validationMessage;
                return false;
            }

            BuildCandidateRevision();
            if (_candidateRevision == null)
            {
                _statusMessage = _candidateDiagnostic;
                return false;
            }

            _phase = CharacterTestRuntimeAiPlannerDevScenePhase.Running;
            _selectedAgentIndex = DefaultSelectedAgentIndex;
            PrepareTimelineForSessionStart();
            _testStateCaptures.Clear();
            _lastTestStateCapturedFrame = int.MinValue;
            _lastReplayResult = default;
            _replayBootstrapEndFrame = 0;
            _initialReplayBaseline = null;
            _replayConfigurationRevision++;
            _replayBootstrapConfig = _matchConfig.Clone();
            ResetLoopPlaybackState();
            _lastCapturedRuntimeFrame = int.MinValue;
            _debugEditGateEnabled = false;
            _previewOverlayEnabled = true;
            _detailsExpanded = false;
            _context.MainMenuVisible = false;
            _context.ManualPaused = false;
            _context.MainMenuCoordinator?.RefreshEffectivePauseState();
            _sessionBuilder.RebuildRuntimeAiLabBattleSession(
                _replayBootstrapConfig,
                "Runtime AI Lab running.");
            _context.Slice?.Services.RuntimeCommands?.SetRuntimeAiBehaviorSelectionUnlocked(false);
            _snapshotParticipant = _context.Slice?.Services.Network.CreateSnapshotParticipant();
            bool needsTacticalBootstrap = false;
            foreach (var program in _candidateRevision.LogicPrograms) needsTacticalBootstrap |= program?.TacticalSource != null;
            var bootstrapModule = _context.Slice?.RuntimeAiPlannerCommands;
            bool baselineInstalled;
            bootstrapModule?.SetBootstrapDecisionsSuppressed(needsTacticalBootstrap);
            try
            {
                baselineInstalled = (!needsTacticalBootstrap || BootstrapTacticalActors(out validationMessage))
                    && TryApplyCandidateAsSessionBaseline(out validationMessage);
            }
            finally { bootstrapModule?.SetBootstrapDecisionsSuppressed(false); }
            if (!baselineInstalled)
            {
                _phase = CharacterTestRuntimeAiPlannerDevScenePhase.Setup;
                _context.MainMenuVisible = true;
                SetPaused(true);
                _statusMessage = validationMessage;
                return false;
            }
            int expectedAgentCount = CountEnabledSetupSlots();
            bool initialized = false;
            for (int i = 0; i < MaxSessionInitializationFrames; i++)
            {
                if (_gameManager == null || !_gameManager.StepSimulationOnceForRuntimeAiPlannerDev())
                {
                _statusMessage = "运行时智能实验室已启动，但初始化 Tick 失败。";
                    return false;
                }

                // Keep bootstrap commands/checkpoints for exact seek replay, while
                // leaving the final selection on the first frame that exposes all agents.
                CharacterTestRuntimeAiPlannerSnapshot initialSnapshot =
                    _context.Slice?.RuntimeAiPlannerCommands?.CreateSnapshot();
                int agentCount = initialSnapshot?.Agents?.Count ?? 0;
                if (agentCount >= expectedAgentCount)
                {
                    initialized = true;
                    break;
                }
            }

            SetPaused(true);
            if (!initialized)
            {
                _statusMessage = "运行时智能实验室初始化时未生成预期的智能体快照。";
                return false;
            }
            if (!TrySelectBaselineRuntimeAgent(out validationMessage))
            {
                _statusMessage = validationMessage;
                return false;
            }

            if (_candidateRevision != null
                && _candidateState == CharacterTestRuntimeAiLabCandidateState.PreviewComplete)
            {
                int applicationFrame = (int)Math.Min(
                    int.MaxValue,
                    _context.Slice.CurrentFrame.Value);
                if (!TryQueueCandidateApply(
                        _candidateRevision,
                        applicationFrame,
                        out validationMessage))
                {
                    _statusMessage = validationMessage;
                    return false;
                }
                ProcessPendingCandidateApply();
                if (_candidateState != CharacterTestRuntimeAiLabCandidateState.Applied)
                {
                    _statusMessage = "实验已创建，但当前行为候选版本未能在首个可见帧前应用。";
                    return false;
                }
            }

            _replayBootstrapEndFrame = (int)_context.Slice.CurrentFrame.Value;

            if (!CaptureInitialReplayBaseline())
                return false;

            // Hide internal bootstrap frames. The first valid Agent frame is the
            // only legal replay floor exposed by the Lab timeline.
            BeginVisibleTimelineSession((int)_context.Slice.CurrentFrame.Value);
            CaptureLatestFrameIfNeeded(force: true);
            _context.NetworkCoordinator?.BeginNetworkReplayRecording();
            ResetReplayValidationWorkspace(clearCapture: true);

            if (presentation == CharacterTestRuntimeAiLabSessionStartPresentation.ArenaPaused)
            {
                _statusMessage = "运行时智能实验室已启动，战场已暂停在首个捕获帧。";
                _timeline.SetMode(CharacterTestRuntimeAiPlannerDevTimelineMode.Paused);
            }
            else
            {
                _statusMessage = "运行时智能实验室已启动，并暂停在首个捕获帧。";
                _timeline.SetMode(CharacterTestRuntimeAiPlannerDevTimelineMode.Paused);
            }
            return true;
        }

        private bool TrySelectBaselineRuntimeAgent(out string diagnostic)
        {
            diagnostic = string.Empty;
            if (_baselineRevision == null)
            {
                diagnostic = "本次测试缺少行为卡基线。";
                return false;
            }

            CharacterTestRuntimeAiPlannerSnapshot snapshot =
                _context?.Slice?.RuntimeAiPlannerCommands?.CreateSnapshot();
            IReadOnlyList<CharacterTestRuntimeAiPlannerAgentSnapshot> agents =
                snapshot?.Agents;
            CharacterTestArenaSlotConfig selectedSlot = _matchConfig?.TryGetSlot(
                _selectedSetupTeamId,
                _selectedSetupSlotIndex);
            int expectedLoadoutId = selectedSlot != null
                ? CharacterTestArenaBehaviorGroupDraftStore.ToSessionLoadoutId(
                    selectedSlot.BehaviorGroupDraftId)
                : 0;
            for (int agentIndex = 0; agents != null && agentIndex < agents.Count; agentIndex++)
            {
                CharacterTestRuntimeAiPlannerAgentSnapshot agent = agents[agentIndex];
                int runtimeLoadoutId = agent.RuntimeAiBehaviorLoadoutId > 0
                    ? agent.RuntimeAiBehaviorLoadoutId
                    : expectedLoadoutId;
                if (agent.RuntimeAiPlannerProfileSlot != _baselineRevision.ProfileSlot
                    || runtimeLoadoutId != expectedLoadoutId)
                {
                    continue;
                }

                _selectedAgentIndex = agentIndex;
                return true;
            }

            diagnostic = "行为卡基线未匹配到所选角色的 Runtime AI Planner Agent。";
            return false;
        }

        internal bool ToggleSelectedSetupSlotEnabled()
        {
            if (!TryGetEditableSetupSlot(out CharacterTestArenaSlotConfig slot))
                return false;

            slot.Enabled = !slot.Enabled;
            SyncDraftSetupSlot(slot.TeamId, slot.SlotIndex);
            _statusMessage = FormatSetupSlotName(slot.TeamId, slot.SlotIndex)
                + (slot.Enabled ? " 已启用。" : " 已禁用。");
            return true;
        }

        internal bool SelectPreviousSetupCharacter()
        {
            return SelectSetupCharacter(-1);
        }

        internal bool SelectNextSetupCharacter()
        {
            return SelectSetupCharacter(1);
        }

        internal bool SelectSetupCharacterId(int characterId)
        {
            if (!TryGetEditableSetupSlot(out CharacterTestArenaSlotConfig slot))
                return false;

            var registry = new CharacterTestCharacterDefinitionRegistry();
            if (!registry.TryGet(characterId, out _))
            {
                _statusMessage = "所选角色当前不可用。";
                return false;
            }

            slot.CharacterId = characterId;
            SyncDraftSetupSlot(slot.TeamId, slot.SlotIndex);
            _statusMessage = "已为 "
                + FormatSetupSlotName(slot.TeamId, slot.SlotIndex)
                + " 选择角色。";
            return true;
        }

        internal bool SelectPreviousSetupBehaviorGroup()
        {
            return SelectSetupBehaviorGroup(-1);
        }

        internal bool SelectNextSetupBehaviorGroup()
        {
            return SelectSetupBehaviorGroup(1);
        }

        internal bool SelectSetupBehaviorGroupDraft(int draftId)
        {
            if (!TryGetEditableSetupSlot(out CharacterTestArenaSlotConfig slot))
                return false;

            CharacterTestArenaBehaviorGroupDraftStore drafts = _matchConfig.BehaviorGroups;
            if (drafts == null || !drafts.TryGetDraft(draftId, out _))
            {
                _statusMessage = "所选行为组已不存在。";
                return false;
            }
            if (!drafts.TryGetDraftDisplay(
                    draftId,
                    out _,
                    out string statusText,
                    out bool compileSuccess)
                || !compileSuccess)
            {
                _statusMessage = "该行为组当前不可用于测试。"
                    + (string.IsNullOrWhiteSpace(statusText) ? string.Empty : " " + statusText);
                return false;
            }

            slot.BehaviorGroupDraftId = draftId;
            slot.LoadoutId = CharacterTestArenaBehaviorGroupDraftStore.ToSessionLoadoutId(draftId);
            slot.VariantId = CharacterTestArenaBehaviorGroupDraftStore.SessionVariantId;
            SyncDraftSetupSlot(
                slot.TeamId,
                slot.SlotIndex,
                selectExactProjectSource: true);
            _statusMessage = "已为 "
                + FormatSetupSlotName(slot.TeamId, slot.SlotIndex)
                + " 选择行为组。";
            return true;
        }

        internal bool SelectSetupBehaviorGroupWorkspaceDraft(
            string groupDraftId,
            int arenaBehaviorGroupDraftId)
        {
            if (!TryGetEditableSetupSlot(out CharacterTestArenaSlotConfig slot)
                || DraftWorkspace == null)
                return false;
            bool selected = DraftWorkspace.TrySelectGroupForSlot(
                slot.TeamId,
                slot.SlotIndex,
                groupDraftId,
                arenaBehaviorGroupDraftId,
                out string message);
            if (selected)
            {
                _draftPersistence.MarkDirty();
                _draftPersistence.Flush();
            }
            _statusMessage = message;
            return selected;
        }

        public bool SelectSetupSpawnSlot(int teamId, int slotIndex)
        {
            if (_phase != CharacterTestRuntimeAiPlannerDevScenePhase.Setup)
            {
                _statusMessage = "选择出生槽位前请重置回配置阶段。";
                return false;
            }

            if (_matchConfig.TryGetSlot(teamId, slotIndex) == null)
            {
                _statusMessage = "所选出生槽位不可用。";
                return false;
            }

            if (IsConfiguredTestCardEditing && _matchConfig.TryGetSlot(teamId, slotIndex).CharacterId
                != _matchConfig.TryGetSlot(_selectedSetupTeamId, _selectedSetupSlotIndex)?.CharacterId && !LeaveConfiguredCard()) return false;
            _selectedSetupTeamId = teamId;
            _selectedSetupSlotIndex = slotIndex;
            SyncDraftSetupSlot(teamId, slotIndex);
            _statusMessage = FormatSetupSelectionStatus(teamId, slotIndex);
            return true;
        }

        public bool ClearSetupSpawnSelection()
        {
            if (_phase != CharacterTestRuntimeAiPlannerDevScenePhase.Setup)
                return false;

            _selectedSetupTeamId = 0;
            _selectedSetupSlotIndex = -1;
            _statusMessage = "已清除出生槽位选择。";
            return true;
        }

        public bool SetSelectedSetupSpawnPosition(Vector2 position)
        {
            return SetSetupSpawnPosition(_selectedSetupTeamId, _selectedSetupSlotIndex, position);
        }

        public bool SetSetupSpawnPosition(int teamId, int slotIndex, Vector2 position)
        {
            if (_phase != CharacterTestRuntimeAiPlannerDevScenePhase.Setup)
            {
                _statusMessage = "移动出生槽位前请重置回配置阶段。";
                return false;
            }

            if (_matchConfig.TryGetSlot(teamId, slotIndex) == null)
            {
                _statusMessage = "所选出生槽位不可用。";
                return false;
            }

            Vector2 next = _setupSnapEnabled
                ? CharacterTestRuntimeAiLabSpawnValidator.Snap(
                    position,
                    CharacterTestRuntimeAiLabSpawnValidator.DefaultSnapSize)
                : CharacterTestRuntimeAiLabSpawnValidator.ClampToArena(position);
            if (!_matchConfig.SetSpawnPosition(teamId, slotIndex, next))
            {
                _statusMessage = "更新出生位置失败。";
                return false;
            }

            _selectedSetupTeamId = teamId;
            _selectedSetupSlotIndex = slotIndex;
            SyncDraftSetupSlot(teamId, slotIndex);
            CharacterTestRuntimeAiLabSpawnValidationResult result = ValidateSetupSpawns();
            result.TryGetSlot(teamId, slotIndex, out CharacterTestRuntimeAiLabSpawnSlotValidation slot);
            _statusMessage = "已移动 "
                + FormatSetupSlotName(teamId, slotIndex)
                + " 到 x "
                + next.x.ToString("0.##", CultureInfo.InvariantCulture)
                + " z "
                + next.y.ToString("0.##", CultureInfo.InvariantCulture)
                + " | "
                + (string.IsNullOrEmpty(slot.Message) ? result.Summary : slot.Message);
            return true;
        }

        public bool ToggleSetupSnap()
        {
            _setupSnapEnabled = !_setupSnapEnabled;
            _statusMessage = _setupSnapEnabled
                ? "出生位置吸附已开启：1 米网格。"
                : "出生位置吸附已关闭。";
            return true;
        }

        public bool MirrorSetupSpawns()
        {
            if (_phase != CharacterTestRuntimeAiPlannerDevScenePhase.Setup)
            {
                _statusMessage = "镜像出生位置前请重置回配置阶段。";
                return false;
            }

            for (int i = 0; i < CharacterTestArenaMatchConfig.SlotsPerTeam; i++)
            {
                if (_matchConfig.TryGetSpawnPosition(CharacterTestArenaMatchConfig.Team1Id, i, out Vector2 team1))
                    _matchConfig.SetSpawnPosition(CharacterTestArenaMatchConfig.Team2Id, i, new Vector2(-team1.x, team1.y));
                SyncDraftSetupSlot(CharacterTestArenaMatchConfig.Team2Id, i);
            }

            _statusMessage = "已将队伍 1 的出生位置镜像到队伍 2。";
            return true;
        }

        public bool ResetSetupSpawnDefaults()
        {
            if (_phase != CharacterTestRuntimeAiPlannerDevScenePhase.Setup)
            {
                _statusMessage = "恢复默认出生位置前请重置回配置阶段。";
                return false;
            }

            _matchConfig.SetSpawnPositions(DefaultTeam1SetupSpawns, DefaultTeam2SetupSpawns);
            for (int team = CharacterTestArenaMatchConfig.Team1Id; team <= CharacterTestArenaMatchConfig.Team2Id; team++)
            {
                for (int slot = 0; slot < CharacterTestArenaMatchConfig.SlotsPerTeam; slot++)
                    SyncDraftSetupSlot(team, slot);
            }
            _selectedSetupTeamId = CharacterTestArenaMatchConfig.Team1Id;
            _selectedSetupSlotIndex = 0;
            _statusMessage = "已恢复运行时智能实验室的默认出生位置。";
            return true;
        }

        public bool FitSetupView()
        {
            if (_phase != CharacterTestRuntimeAiPlannerDevScenePhase.Setup)
            {
                _statusMessage = "适配配置视野前请重置回配置阶段。";
                return false;
            }

            CharacterTestRuntimeAiLabSetupCameraController setupCamera =
                GetComponent<CharacterTestRuntimeAiLabSetupCameraController>();
            if (setupCamera == null)
            {
                _statusMessage = "运行时智能实验室配置相机不可用。";
                return false;
            }

            setupCamera.FitView();
            _statusMessage = "配置相机已适配整个测试场地。";
            return true;
        }

        internal IDisposable PushSetupCursorReleased()
        {
            return _context?.ThirdPersonCameraBridge?.PushCursorReleasedByUi();
        }

        internal CharacterTestRuntimeAiLabSpawnValidationResult ValidateSetupSpawns()
        {
            return CharacterTestRuntimeAiLabSpawnValidator.Validate(_matchConfig);
        }

        internal bool TryGetSelectedSetupSpawn(out CharacterTestRuntimeAiLabSpawnSlotValidation slot)
        {
            CharacterTestRuntimeAiLabSpawnValidationResult result = ValidateSetupSpawns();
            return result.TryGetSlot(_selectedSetupTeamId, _selectedSetupSlotIndex, out slot);
        }

        internal void SetSetupInteractionStatus(string message)
        {
            if (_phase != CharacterTestRuntimeAiPlannerDevScenePhase.Setup
                || string.IsNullOrEmpty(message))
            {
                return;
            }

            _statusMessage = message;
        }

        public bool ResetSession()
        {
            if (!EnsureRuntimeReady())
                return false;

            CancelDeterministicReplay("Deterministic replay cancelled by reset.");
            ResetReplayValidationWorkspace(clearCapture: true);
            ClearHistoricalBehaviorPresentation();

            _phase = CharacterTestRuntimeAiPlannerDevScenePhase.Setup;
            _debugEditGateEnabled = false;
            _previewOverlayEnabled = true;
            _detailsExpanded = false;
            // A restart rebuilds the configured arena baseline. Reset both sides of the
            // selection identity before rebuilding: the setup slot is authoritative, while the
            // runtime Agent index is only a projection whose ordering may change between sessions.
            _selectedSetupTeamId = CharacterTestArenaMatchConfig.Team1Id;
            _selectedSetupSlotIndex = 0;
            _selectedAgentIndex = DefaultSelectedAgentIndex;
            _frameBuffer.Clear();
            _timeline.Clear();
            ResetTimelineSessionState();
            ResetLoopPlaybackState();
            ResetHistoryPlaybackState();
            CancelBranchRecompute();
            _lastCapturedRuntimeFrame = int.MinValue;
            _snapshotParticipant = null;
            _initialReplayBaseline = null;
            _replayConfigurationRevision++;
            _context.Slice?.Services.RuntimeCommands?.SetRuntimeAiBehaviorSelectionUnlocked(false);
            _sessionBuilder.RebuildArenaSetupSession(_matchConfig, "Runtime AI Lab setup.");
            ResetTacticalCardTestPlayback();
            RestoreUnityTimeScale();
            SetPaused(true);
            _statusMessage = "运行时智能实验室测试已重置。";
            return true;
        }

        /// <summary>
        /// Freezes the current behavior-card draft and local test context into a fresh run that
        /// starts at RuntimeFrame 0. Existing history remains untouched if validation fails.
        /// </summary>
        /// <remarks>LowFreqAlloc: explicit Runtime AI Lab authoring command.</remarks>
        internal bool ApplyBehaviorTestAndRerun(out string message)
        {
            CharacterTestRuntimeAiLabTestCurrentChangesApplyResult result =
                ApplyBehaviorTestAndRerunCore(
                    CreateCompatibilityBehaviorTestRequest(),
                    markRestarting: null,
                    preserveShellContext: false,
                    verifySelectedCardIdentity: false);
            message = result.Success
                ? "已从第 0 帧重建行为测试预览。"
                : result.Message;
            _statusMessage = message;
            return result.Success;
        }

        public bool TogglePreviewOverlay()
        {
            _previewOverlayEnabled = !_previewOverlayEnabled;
            _statusMessage = _previewOverlayEnabled
                ? "Runtime AI Lab scene overlay enabled."
                : "Runtime AI Lab scene overlay disabled.";
            return true;
        }

        public bool ToggleDetails()
        {
            _detailsExpanded = !_detailsExpanded;
            _statusMessage = _detailsExpanded
                ? "Runtime AI Lab details drawer expanded."
                : "Runtime AI Lab details drawer collapsed.";
            return true;
        }

        internal bool TryBuildSelectedPreview(out CharacterTestRuntimeAiPlannerDevPreviewItem preview)
        {
            if (!_frameBuffer.TryGetSelected(out CharacterTestRuntimeAiPlannerDevFrameSnapshot frame))
            {
                preview = CharacterTestRuntimeAiPlannerDevPreviewItem.Unavailable("No captured frame.");
                return false;
            }

            preview = CharacterTestRuntimeAiPlannerDevPreviewBuilder.Build(
                frame,
                _selectedAgentIndex,
                _previewOverlayEnabled);
            return preview.Available;
        }

        public bool TogglePause()
        {
            if (TacticalCardTestMode && !TacticalCardTestInFlight && !IsHistoricalFrameSelected
                && !_historyPlaybackActive && !_loopPlaybackActive)
                return ToggleTacticalCardTestPlayback(out _);
            if (!EnsureRuntimeReady())
                return false;
            if (!EnsureTimelineSessionContinuable())
                return false;

            if (_seekInProgress)
            {
                _statusMessage = "请等待确定性回放完成，或重置以取消。";
                return false;
            }

            if (_phase != CharacterTestRuntimeAiPlannerDevScenePhase.Running)
            {
                _statusMessage = "切换暂停状态前请先启动测试。";
                return false;
            }

            if (_context.Paused)
            {
                if (_historyPlaybackActive)
                {
                    PauseHistoryPlayback("历史顺序播放已暂停。" );
                    return true;
                }

                if (_loopPlaybackActive)
                {
                    PauseLoopReview("Loop review paused.");
                    return true;
                }

                if (_timeline.LoopEnabled)
                    return StartLoopReview();

                if (_timeline.HasSelection
                    && _timeline.SelectedFrame < _timeline.LatestFrame)
                {
                    if (_branchRecomputeArmed)
                    {
                        if (!AdvanceOneAuthorityFrame(CharacterTestRuntimeAiLabPresentationPolicy.Immediate))
                            return false;
                        SetPaused(false);
                        _timeline.SetMode(CharacterTestRuntimeAiPlannerDevTimelineMode.Live);
                        _statusMessage = "已从选中帧建立新未来，继续实时模拟。";
                        return true;
                    }

                    return StartHistoryPlayback();
                }

                SetPaused(false);
                DisableDebugEditGate();
                _timeline.SelectLatest();
                _timeline.SetMode(CharacterTestRuntimeAiPlannerDevTimelineMode.Live);
            _statusMessage = "运行中。";
            }
            else
            {
                SetPaused(true);
                _timeline.SetMode(CharacterTestRuntimeAiPlannerDevTimelineMode.Paused);
            _statusMessage = "已暂停。";
            }

            if (TacticalCardTestMode) RefreshTacticalPreview();
            return true;
        }

        public bool StepFrame()
        {
            return StepFrames(1);
        }

        public bool StepBackFrame()
        {
            return StepFrames(-1);
        }

        public bool StepForward10()
        {
            return StepFrames(10);
        }

        public bool StepBack10()
        {
            return StepFrames(-10);
        }

        public bool StepFrames(int frameDelta)
        {
            if (!EnsureRuntimeReady())
                return false;
            if (!EnsureTimelineSessionContinuable())
                return false;

            if (_seekInProgress)
            {
                _statusMessage = "确定性回放正在进行。可通过重置取消。";
                return false;
            }

            if (!_context.Paused)
            {
                _statusMessage = "推进帧前请先暂停。";
                return false;
            }

            if (_loopPlaybackActive)
            {
                _statusMessage = "推进帧前请先暂停循环回看。";
                return false;
            }

            if (frameDelta == 0)
                return true;

            if (frameDelta < 0)
                return SeekRelativeFrame(frameDelta);

            if (_phase != CharacterTestRuntimeAiPlannerDevScenePhase.Running)
            {
                _statusMessage = "推进新帧前请先启动测试。";
                return false;
            }

            int remaining = frameDelta;
            if (_timeline.HasSelection && _timeline.SelectedFrame < _timeline.LatestFrame)
            {
                if (_branchRecomputeArmed)
                {
                    if (!AdvanceOneAuthorityFrame(CharacterTestRuntimeAiLabPresentationPolicy.Immediate))
                        return false;
                    remaining--;
                }
                else
                {
                int startFrame = _timeline.SelectedFrame;
                int target = Math.Min(_timeline.SelectedFrame + remaining, _timeline.LatestFrame);
                if (!SeekToFrame(target))
                    return false;

                if (_seekInProgress)
                    return true;

                remaining -= target - startFrame;
                }
            }

            for (int i = 0; i < remaining; i++)
            {
                if (!AdvanceOneAuthorityFrame(CharacterTestRuntimeAiLabPresentationPolicy.Immediate))
                {
                _statusMessage = "运行时切片不可用，推进失败。";
                    return false;
                }

            }

            _timeline.SetMode(CharacterTestRuntimeAiPlannerDevTimelineMode.Paused);
            _statusMessage = remaining == 1
                ? "已推进一个运行时智能规划帧。"
                : "已推进多个运行时智能规划帧。";
            return true;
        }

        public bool JumpToFrame(int frame)
        {
            return JumpToFrame(frame, 0);
        }

        internal bool JumpToFrame(int frame, int preferredAgentEntityId)
        {
            if (!EnsureTimelineSessionContinuable())
                return false;
            if (_seekInProgress)
            {
                _statusMessage = "确定性回放正在进行。可通过重置取消。";
                return false;
            }

            if (_loopPlaybackActive)
            {
                _statusMessage = "跳转帧前请先暂停循环回看。";
                return false;
            }

            if (frame < 0)
            {
                _statusMessage = "请输入零或更大的运行时帧编号。";
                return false;
            }

            if (!_timeline.TryResolveNearestFrame(frame, out int resolvedFrame, out bool clamped))
            {
                _statusMessage = "尚未捕获帧。";
                return false;
            }
            if (clamped)
                _statusMessage = "目标帧不可用，已定位到最近保留帧 " + resolvedFrame + "。";
            return SeekToFrame(new CharacterTestRuntimeAiLabSeekRequest(
                resolvedFrame,
                CharacterTestRuntimeAiLabSeekOrigin.Jump,
                stayPaused: true,
                preferredAgentEntityId));
        }

        public bool SelectPreviousFrame()
        {
            return StepFrames(-1);
        }

        internal bool SelectFirstFrame()
        {
            if (!EnsureTimelineSessionContinuable() || _timeline == null || _timeline.Count <= 0)
                return false;
            if (_loopPlaybackActive)
                PauseLoopReview("循环回看已停止。");
            ResetHistoryPlaybackState();
            CancelBranchRecompute();
            return SeekToFrame(new CharacterTestRuntimeAiLabSeekRequest(
                _timeline.Records[0].Frame,
                CharacterTestRuntimeAiLabSeekOrigin.Jump,
                stayPaused: true));
        }

        public bool SelectNextFrame()
        {
            return StepFrames(1);
        }

        public bool SelectLatestFrame()
        {
            if (!EnsureTimelineSessionContinuable())
                return false;
            if (_loopPlaybackActive)
                PauseLoopReview("循环回看已停止在当前帧。" );
            ResetHistoryPlaybackState();
            CancelBranchRecompute();

            if (!_timeline.HasSelection || _timeline.SelectedFrame >= _timeline.LatestFrame)
                CaptureLatestFrameIfNeeded(force: true);
            if (!_timeline.TryGetLatest(out CharacterTestRuntimeAiPlannerDevTimelineRecord latest))
            {
                _statusMessage = "尚未捕获帧。";
                return false;
            }

            bool result = SeekToFrame(new CharacterTestRuntimeAiLabSeekRequest(
                latest.Frame,
                CharacterTestRuntimeAiLabSeekOrigin.Latest,
                stayPaused: _context == null || _context.Paused));
            if (result)
            {
                if (_seekInProgress)
                    return true;

                _timeline.SetMode(_context != null && _context.Paused
                    ? CharacterTestRuntimeAiPlannerDevTimelineMode.Paused
                    : CharacterTestRuntimeAiPlannerDevTimelineMode.Live);
            _statusMessage = "已选择最新捕获帧。";
            }

            ClampSelectedAgentIndex();
            return result;
        }

        public bool CyclePlaybackSpeed()
        {
            float speed = _timeline.CyclePlaybackSpeed();
            ApplyUnityTimeScaleForPlayback();
            _statusMessage = "运行时智能实验室播放速度已设为 " + speed.ToString("0.##") + "x。";
            return true;
        }

        public bool IncreasePlaybackSpeed()
        {
            return AdjustPlaybackSpeed(1);
        }

        public bool DecreasePlaybackSpeed()
        {
            return AdjustPlaybackSpeed(-1);
        }

        private bool AdjustPlaybackSpeed(int direction)
        {
            float speed = _timeline.AdjustPlaybackSpeed(direction);
            ApplyUnityTimeScaleForPlayback();
            _statusMessage = "运行时智能实验室播放速度已设为 " + speed.ToString("0.##") + "x。";
            return true;
        }

        public bool SetLoopStart()
        {
            if (!EnsureTimelineSessionContinuable())
                return false;
            CancelBranchRecompute();
            if (_loopPlaybackActive)
            {
                _statusMessage = "修改循环起点 A 前请先暂停循环回看。";
                return false;
            }

            bool result = _timeline.SetLoopStartToSelection();
            _statusMessage = result
                ? "循环起点已设为帧 " + _timeline.LoopStartFrame + "。"
                : "设置循环起点前请先选择已捕获帧。";
            return result;
        }

        public bool SetLoopEnd()
        {
            if (!EnsureTimelineSessionContinuable())
                return false;
            CancelBranchRecompute();
            if (_loopPlaybackActive)
            {
                _statusMessage = "修改循环终点 B 前请先暂停循环回看。";
                return false;
            }

            bool result = _timeline.SetLoopEndToSelection();
            _statusMessage = result
                ? "循环终点已设为帧 " + _timeline.LoopEndFrame + "。"
                : "循环终点必须晚于循环起点。";
            return result;
        }

        public bool ToggleLoop()
        {
            if (!EnsureTimelineSessionContinuable())
                return false;
            CancelBranchRecompute();
            if (_loopPlaybackActive)
            {
                PauseLoopReview("循环回看已停止。" );
                _timeline.ToggleLoop();
                _statusMessage = "A-B 循环已关闭。";
                return true;
            }

            if (!_timeline.LoopEnabled
                && !_timeline.TryValidateLoopRange(out string diagnostic))
            {
                _statusMessage = diagnostic;
                return false;
            }

            bool result = _timeline.ToggleLoop();
            _statusMessage = result
                ? (_timeline.LoopEnabled ? "A-B 循环已开启。" : "A-B 循环已关闭。")
                : "开启循环前请先设置 A 和 B。";
            return result;
        }

        public bool SelectPreviousAgent()
        {
            if (!CanChangeSelectedAgent())
                return false;
            int count = ResolveSelectedAgentCount();
            if (count <= 0)
            {
                _selectedAgentIndex = 0;
                _statusMessage = "已选帧中没有运行时智能规划智能体。";
                return false;
            }

            int nextIndex = _selectedAgentIndex <= 0 ? count - 1 : _selectedAgentIndex - 1;
            if (nextIndex != _selectedAgentIndex)
            {
                CancelQueuedApply(
                    CharacterTestRuntimeAiLabQueuedApplyCancelReason.AgentChanged,
                    "Apply F+1 已取消：选中的 Agent 已变化。");
            }
            _selectedAgentIndex = nextIndex;
            SyncSelectedSetupSlotToAgent(nextIndex);
            _statusMessage = "已选择上一个运行时智能规划智能体。";
            return true;
        }

        public bool SelectNextAgent()
        {
            if (!CanChangeSelectedAgent())
                return false;
            int count = ResolveSelectedAgentCount();
            if (count <= 0)
            {
                _selectedAgentIndex = 0;
                _statusMessage = "No Runtime AI Planner agent in the selected frame.";
                return false;
            }

            int nextIndex = (_selectedAgentIndex + 1) % count;
            if (nextIndex != _selectedAgentIndex)
            {
                CancelQueuedApply(
                    CharacterTestRuntimeAiLabQueuedApplyCancelReason.AgentChanged,
                    "Apply F+1 已取消：选中的 Agent 已变化。");
            }
            _selectedAgentIndex = nextIndex;
            SyncSelectedSetupSlotToAgent(nextIndex);
            _statusMessage = "已选择下一个运行时智能规划智能体。";
            return true;
        }

        internal bool SelectAgent(int agentIndex)
        {
            if (!CanChangeSelectedAgent())
                return false;
            int count = ResolveSelectedAgentCount();
            if (agentIndex < 0 || agentIndex >= count)
            {
                _statusMessage = "所选运行时智能规划智能体不可用。";
                return false;
            }

            if (agentIndex != _selectedAgentIndex)
            {
                CancelQueuedApply(
                    CharacterTestRuntimeAiLabQueuedApplyCancelReason.AgentChanged,
                    "Apply F+1 已取消：选中的 Agent 已变化。");
            }
            _selectedAgentIndex = agentIndex;
            SyncSelectedSetupSlotToAgent(agentIndex);
            _statusMessage = "已选择运行时智能规划智能体 "
                + (agentIndex + 1).ToString(CultureInfo.InvariantCulture)
                + ".";
            return true;
        }

        internal bool TrySelectAgentByScreenPosition(
            UnityEngine.Camera camera,
            Vector2 screenPosition,
            float pickRadiusPixels = 54f)
        {
            if (!CanChangeSelectedAgent()
                || _phase != CharacterTestRuntimeAiPlannerDevScenePhase.Running
                || camera == null
                || !_frameBuffer.TryGetSelected(out CharacterTestRuntimeAiPlannerDevFrameSnapshot frame))
            {
                return false;
            }

            IReadOnlyList<CharacterTestRuntimeAiPlannerAgentSnapshot> agents = frame.Agents;
            int bestIndex = -1;
            float bestDistanceSquared = pickRadiusPixels * pickRadiusPixels;
            for (int i = 0; i < agents.Count; i++)
            {
                CharacterTestRuntimeAiPlannerAgentSnapshot agent = agents[i];
                if (!agent.HasScenePreviewGeometry)
                    continue;

                Vector3 world = new Vector3(
                    agent.ScenePreviewSourceCenterX,
                    agent.ScenePreviewSourceCenterY,
                    agent.ScenePreviewSourceCenterZ);
                Vector3 projected = camera.WorldToScreenPoint(world);
                if (projected.z <= 0f)
                    continue;

                float dx = projected.x - screenPosition.x;
                float dy = projected.y - screenPosition.y;
                float distanceSquared = dx * dx + dy * dy;
                if (distanceSquared >= bestDistanceSquared)
                    continue;

                bestDistanceSquared = distanceSquared;
                bestIndex = i;
            }

            return bestIndex >= 0 && SelectAgent(bestIndex);
        }

        public bool ToggleDebugEditGate()
        {
            if (!EnsureRuntimeReady())
                return false;

            if (_phase != CharacterTestRuntimeAiPlannerDevScenePhase.Running)
            {
                _statusMessage = "解锁调试编辑前请先启动测试。";
                return false;
            }

            if (!_context.Paused)
            {
                _statusMessage = "解锁运行时智能规划调试编辑前请先暂停。";
                return false;
            }

            bool next = !_debugEditGateEnabled;
            CharacterTestRuntimeCommandModule runtimeCommands = _context.Slice?.Services.RuntimeCommands;
            if (runtimeCommands == null)
            {
                _statusMessage = "运行时命令模块不可用。";
                return false;
            }

            runtimeCommands.SetRuntimeAiBehaviorSelectionUnlocked(next);
            _debugEditGateEnabled = next;
            _timeline.ClearEditBranchStart();
            _statusMessage = next
                ? "暂停会话的编辑锁已解锁。"
                : "编辑锁已锁定。";
            return true;
        }

        private bool CanChangeSelectedAgent()
        {
            if (_frameSelection.Phase == CharacterTestRuntimeAiLabFrameSelectionPhase.FailedLocked)
            {
                _statusMessage = "SESSION_LOCKED: reset or exit before changing Agent.";
                return false;
            }
            if (_frameSelection.IsBusy)
            {
                _statusMessage = "SEEK_BUSY: wait for the committed RuntimeFrame before changing Agent.";
                return false;
            }
            return true;
        }

        private void Update()
        {
            if (CardTrialOpen)
            {
                _cardTrial.Tick();
                if (CharacterTestRuntimeAiLabInput.IsKeyDown(CharacterTestRuntimeAiLabKey.F9)) _cardTrial.ToggleVisibility();
                return;
            }
            TickRuntimeSummary();
            ProcessRuntimeAiLabVisibilityShortcut(
                CharacterTestRuntimeAiLabInput.IsKeyDown(CharacterTestRuntimeAiLabKey.F9));

            ProcessQueuedBehaviorRecipeSaveAndRun();

            SaveConfiguredRecovery(out _, force: false);
            _draftPersistence.TickAutosave();
            if (_seekInProgress)
            {
                ProcessDeterministicReplay(ignoreBudget: false);
                if (_seekInProgress)
                    return;
            }

            ProcessTimelineScrubWork();
            if (_seekInProgress)
                return;

            ProcessReplayValidationWork();

            if (_historyPlaybackActive)
            {
                UpdateHistoryPlayback(Time.unscaledDeltaTime);
                return;
            }

            if (_loopPlaybackActive)
            {
                UpdateLoopReviewPlayback();
                return;
            }

            if (_phase == CharacterTestRuntimeAiPlannerDevScenePhase.Running && !_seekInProgress)
            {
                ProcessPendingCandidateApply();
                StopFailedTacticalCardApply();
                ApplyUnityTimeScaleForPlayback();
            }
        }

        internal void ProcessRuntimeAiLabVisibilityShortcut(bool shortcutPressed)
        {
            if (!shortcutPressed)
                return;
            if (_shellCoordinator != null)
                _shellCoordinator.ToggleVisibility();
            else
                _context?.UiRuntime?.ToggleRuntimeAiPlannerDevPanel();
        }

        private void OnDisable()
        {
            if (CardTrialOpen) { _cardTrial.Dispose(); return; }
            CancelRuntimeSummary();
            SaveConfiguredRecovery(out _);
            _behaviorRecipeSaveQueued = false;
            _queuedBehaviorRecipeEntryIndex = -1;
            if (Application.isPlaying && _draftWorkspace != null && _draftWorkspace.Dirty)
                _draftPersistence.Flush();
            CancelDeterministicReplay("Deterministic replay cancelled because the Lab controller was disabled.");
            ResetReplayValidationWorkspace(clearCapture: true);
            ResetLoopPlaybackState();
            ResetHistoryPlaybackState();
            CancelBranchRecompute();
            RestoreUnityTimeScale();
        }

        private void OnApplicationFocus(bool hasFocus)
        {
            if (CardTrialOpen) return;
            if (!hasFocus && Application.isPlaying && _draftWorkspace != null && _draftWorkspace.Dirty)
                _draftPersistence.Flush();
        }

        private void OnApplicationQuit()
        {
            if (CardTrialOpen) { _cardTrial.Dispose(); return; }
            SaveConfiguredRecovery(out _);
            if (_draftWorkspace != null && _draftWorkspace.Dirty)
                _draftPersistence.Flush();
        }

        private bool EnsureRuntimeReady()
        {
            if (_context == null || _sessionBuilder == null)
            {
                _statusMessage = "运行时智能实验室控制器尚未初始化。";
                return false;
            }

            return true;
        }

        private bool TryGetEditableSetupSlot(out CharacterTestArenaSlotConfig slot)
        {
            slot = null;
            if (_phase != CharacterTestRuntimeAiPlannerDevScenePhase.Setup)
            {
                _statusMessage = "编辑队伍槽位前请重置回配置阶段。";
                return false;
            }

            slot = _matchConfig.TryGetSlot(_selectedSetupTeamId, _selectedSetupSlotIndex);
            if (slot != null)
                return true;

                _statusMessage = "编辑前请先选择测试槽位。";
            return false;
        }

        private bool SelectSetupCharacter(int direction)
        {
            if (!TryGetEditableSetupSlot(out CharacterTestArenaSlotConfig slot))
                return false;

            var registry = new CharacterTestCharacterDefinitionRegistry();
            if (!registry.TrySelectNext(slot.CharacterId, direction, out int characterId))
            {
                _statusMessage = "当前没有可用的测试角色。";
                return false;
            }

            slot.CharacterId = characterId;
            SyncDraftSetupSlot(slot.TeamId, slot.SlotIndex);
            _statusMessage = "已为 "
                + FormatSetupSlotName(slot.TeamId, slot.SlotIndex)
                + " 选择角色。";
            return true;
        }

        private bool SelectSetupBehaviorGroup(int direction)
        {
            if (!TryGetEditableSetupSlot(out CharacterTestArenaSlotConfig slot))
                return false;

            CharacterTestArenaBehaviorGroupDraftStore drafts = _matchConfig.BehaviorGroups;
            if (drafts == null || drafts.Drafts.Count == 0)
            {
                _statusMessage = "当前没有可用的运行时智能实验室行为组。";
                return false;
            }

            slot.BehaviorGroupDraftId = drafts.SelectNextDraft(slot.BehaviorGroupDraftId, direction);
            slot.LoadoutId = CharacterTestArenaBehaviorGroupDraftStore.ToSessionLoadoutId(
                slot.BehaviorGroupDraftId);
            slot.VariantId = CharacterTestArenaBehaviorGroupDraftStore.SessionVariantId;
            SyncDraftSetupSlot(slot.TeamId, slot.SlotIndex);
            _statusMessage = "已为 "
                + FormatSetupSlotName(slot.TeamId, slot.SlotIndex)
                + " 选择行为组。";
            return true;
        }

        internal bool TryDuplicateDraftEntry(int entryIndex, out string message)
        {
            bool changed = DraftWorkspace.TryDuplicateEntry(
                _selectedSetupTeamId,
                _selectedSetupSlotIndex,
                entryIndex,
                out message);
            if (changed)
                _draftPersistence.MarkDirty();
            return changed;
        }

        internal bool TryAddDraftEntry(
            string cardDraftId,
            out int entryIndex,
            out string message)
        {
            bool changed = IsCoreSlotReplacementActive
                ? DraftWorkspace.TryReplaceCoreSlot(
                    _selectedSetupTeamId,
                    _selectedSetupSlotIndex,
                    _coreSlotReplacementKind,
                    cardDraftId,
                    out entryIndex,
                    out message)
                : DraftWorkspace.TryAddExistingCardEntry(
                    _selectedSetupTeamId,
                    _selectedSetupSlotIndex,
                    cardDraftId,
                    out entryIndex,
                    out message);
            if (changed)
                _draftPersistence.MarkDirty();
            return changed;
        }

        internal bool TrySetDraftEntryResponsibility(
            int entryIndex,
            CharacterTestRuntimeAiPlannerBehaviorResponsibilityKind responsibilityKind,
            out string message)
        {
            bool changed = DraftWorkspace.TrySetEntryResponsibility(
                _selectedSetupTeamId,
                _selectedSetupSlotIndex,
                entryIndex,
                responsibilityKind,
                out message);
            if (changed)
            {
                _draftPersistence.MarkDirty();
                BuildCandidateRevision();
            }
            return changed;
        }

        internal bool TrySetDraftEntryResponsibilities(
            int entryIndex,
            IReadOnlyList<CharacterTestRuntimeAiPlannerBehaviorResponsibilityKind> responsibilities,
            out string message)
        {
            bool changed = DraftWorkspace.TrySetEntryResponsibilities(
                _selectedSetupTeamId,
                _selectedSetupSlotIndex,
                entryIndex,
                responsibilities,
                out message);
            if (changed)
            {
                _draftPersistence.MarkDirty();
                BuildCandidateRevision();
            }
            return changed;
        }

        internal bool ToggleCoreSlotReplacement(int entryIndex, out string message)
        {
            if (!DraftWorkspace.TryGetGroupForSlot(
                _selectedSetupTeamId,
                _selectedSetupSlotIndex,
                out CharacterTestRuntimeAiLabBehaviorGroupDraft group)
                || entryIndex < 0
                || entryIndex >= group.Entries.Count)
            {
                message = "请选择一个基础槽。";
                return false;
            }

            CharacterTestRuntimeAiPlannerBehaviorCoreSlotKind slotKind =
                group.Entries[entryIndex].CoreSlotKind;
            if (!CharacterTestRuntimeAiPlannerBehaviorCoreSlotPolicy.IsCore(slotKind))
            {
                message = "自由行为直接编辑或拖动，不使用基础槽替换。";
                return false;
            }
            if (slotKind == CharacterTestRuntimeAiPlannerBehaviorCoreSlotKind.Targeting
                || slotKind == CharacterTestRuntimeAiPlannerBehaviorCoreSlotKind.Fallback)
            {
                message = "寻敌与兜底是锁定的免费基础策略，不能替换。";
                return false;
            }

            if (_coreSlotReplacementKind == slotKind)
            {
                _coreSlotReplacementKind =
                    CharacterTestRuntimeAiPlannerBehaviorCoreSlotKind.Flexible;
                message = "已取消替换。";
                return true;
            }

            _coreSlotReplacementKind = slotKind;
            message = "请选择可用于“"
                + CharacterTestRuntimeAiPlannerBehaviorCoreSlotPolicy.GetDisplayName(slotKind)
                + "”的行为。";
            return true;
        }

        internal void CancelCoreSlotReplacement()
        {
            _coreSlotReplacementKind =
                CharacterTestRuntimeAiPlannerBehaviorCoreSlotKind.Flexible;
        }

        internal bool TryMoveDraftEntry(int entryIndex, int direction, out string message)
        {
            bool changed = DraftWorkspace.TryMoveEntry(
                _selectedSetupTeamId,
                _selectedSetupSlotIndex,
                entryIndex,
                direction,
                out message);
            if (changed)
                _draftPersistence.MarkDirty();
            return changed;
        }

        internal bool TryToggleDraftEntry(int entryIndex, out string message)
        {
            bool changed = DraftWorkspace.TryToggleEntry(
                _selectedSetupTeamId,
                _selectedSetupSlotIndex,
                entryIndex,
                out message);
            if (changed)
                _draftPersistence.MarkDirty();
            return changed;
        }

        internal bool TryRemoveDraftEntry(int entryIndex, out string message)
        {
            bool changed = DraftWorkspace.TryRemoveEntry(
                _selectedSetupTeamId,
                _selectedSetupSlotIndex,
                entryIndex,
                out message);
            if (changed)
                _draftPersistence.MarkDirty();
            return changed;
        }

        internal bool TryUndoDraft(out string message)
        {
            bool changed = DraftWorkspace.TryUndo(out message);
            if (changed)
                _draftPersistence.MarkDirty();
            return changed;
        }

        private void SyncDraftSetupSlot(
            int teamId,
            int slotIndex,
            bool selectExactProjectSource = false)
        {
            if (DraftWorkspace.SyncSlotFromArena(
                _matchConfig,
                teamId,
                slotIndex,
                selectExactProjectSource))
                _draftPersistence.MarkDirty();
        }

        private int CountEnabledSetupSlots()
        {
            int count = 0;
            CountEnabledSlots(_matchConfig.Team1Slots, ref count);
            CountEnabledSlots(_matchConfig.Team2Slots, ref count);
            return count;
        }

        private static void CountEnabledSlots(
            IReadOnlyList<CharacterTestArenaSlotConfig> slots,
            ref int count)
        {
            if (slots == null)
                return;

            for (int i = 0; i < slots.Count; i++)
            {
                if (slots[i] != null && slots[i].Enabled)
                    count++;
            }
        }

        private void SetPaused(bool paused)
        {
            if (_context == null)
                return;

            _context.MainMenuVisible = false;
            _context.ManualPaused = paused;
            if (_context.MainMenuCoordinator != null)
                _context.MainMenuCoordinator.RefreshEffectivePauseState();
            else
                _context.Paused = paused;
        }

        private bool SeekRelativeFrame(int frameDelta)
        {
            if (!_timeline.HasSelection && !_timeline.SelectLatest())
            {
                _statusMessage = "尚未捕获帧。";
                return false;
            }

            int targetIndex = _timeline.SelectedIndex + frameDelta;
            if (targetIndex < 0)
                targetIndex = 0;
            else if (targetIndex >= _timeline.Count)
                targetIndex = _timeline.Count - 1;

            CharacterTestRuntimeAiPlannerDevTimelineRecord record = _timeline.Records[targetIndex];
            return SeekToFrame(new CharacterTestRuntimeAiLabSeekRequest(
                record.Frame,
                CharacterTestRuntimeAiLabSeekOrigin.Jump,
                stayPaused: true));
        }

        private bool SeekToFrame(int targetFrame)
        {
            return SeekToFrame(new CharacterTestRuntimeAiLabSeekRequest(
                targetFrame,
                CharacterTestRuntimeAiLabSeekOrigin.Jump,
                stayPaused: true));
        }

        private bool SeekToFrame(CharacterTestRuntimeAiLabSeekRequest request)
        {
            if (_seekInProgress)
            {
                _statusMessage = "确定性回放已在进行中。";
                return false;
            }

            if (!EnsureRuntimeReady())
                return false;

            if (_branchRecomputeArmed && request.TargetFrame != _branchBaseFrame)
                CancelBranchRecompute();

            if (_phase != CharacterTestRuntimeAiPlannerDevScenePhase.Running
                && _phase != CharacterTestRuntimeAiPlannerDevScenePhase.Completed)
            {
                _statusMessage = "定位历史帧前请先启动测试。";
                return false;
            }

            if (!_timeline.TryGetFrame(request.TargetFrame, out CharacterTestRuntimeAiPlannerDevTimelineRecord target))
            {
                _statusMessage = "运行时帧 " + request.TargetFrame + " 超出当前保留的时间轴范围。";
                return false;
            }

            if (_replayBootstrapConfig == null)
            {
                _statusMessage = "运行时智能实验室的初始回放基线不可用。";
                return false;
            }

            var selectionTarget = new CharacterTestRuntimeAiLabFrameSelectionTarget(
                target.Frame,
                request.AgentEntityId,
                target.RecordGeneration,
                _timeline.SessionIdentity.Generation,
                request.Origin);
            if (request.Origin != CharacterTestRuntimeAiLabSeekOrigin.Scrub)
            {
                if (!_frameSelection.TryBeginExplicit(selectionTarget, out string failureCode))
                {
                    _statusMessage = failureCode == CharacterTestRuntimeAiLabFrameSelectionFailureCodes.SessionLocked
                        ? "当前会话恢复失败，只能重置或退出。"
                        : "确定性回放已在进行中。";
                    return false;
                }
            }
            if (!_frameSelection.TryTakeNextRestore(out CharacterTestRuntimeAiLabFrameSelectionTarget activeTarget)
                || activeTarget.Frame != target.Frame
                || activeTarget.RecordGeneration != target.RecordGeneration
                || activeTarget.SessionGeneration != _timeline.SessionIdentity.Generation)
            {
                _frameSelection.AbortRestoreBeforeMutation();
                _statusMessage = "选帧事务已过期，请重新定位。";
                return false;
            }

            bool wasPaused = request.StayPaused || _context.Paused;
            return BeginDeterministicReplay(target, wasPaused, request);
        }

        private void EnqueueReplayCommands(CharacterTestRuntimeAiPlannerDevTimelineRecord replayRecord)
        {
            GameSlice slice = _context?.Slice;
            CharacterTestRuntimeAiLabFrozenArray<RuntimeCommand> commands = replayRecord != null
                ? replayRecord.DrainedCommands
                : default;
            if (slice == null)
                return;

            for (int i = 0; i < commands.Count; i++)
            {
                if (!CharacterTestRuntimeAiPlannerDevCommandReplay.ShouldReplayCommand(
                        commands[i],
                        localInputSuppressed: true))
                    continue;

                slice.Services.Commands.EnqueueRuntimeCommand(commands[i]);
            }
        }

        private void DisableDebugEditGate()
        {
            if (!_debugEditGateEnabled)
                return;

            _context?.Slice?.Services.RuntimeCommands?.SetRuntimeAiBehaviorSelectionUnlocked(false);
            _debugEditGateEnabled = false;
        }

        private void CaptureLatestFrameIfNeeded(bool force)
        {
            if (_seekInProgress || _loopPlaybackActive)
                return;

            GameSlice slice = _context?.Slice;
            CharacterTestRuntimeAiPlannerCommandModule module = slice?.RuntimeAiPlannerCommands;
            if (slice == null || module == null)
                return;

            int frame = slice.CurrentFrame.Value > int.MaxValue
                ? int.MaxValue
                : (int)slice.CurrentFrame.Value;
            if (!force && frame == _lastCapturedRuntimeFrame)
                return;

            _activeFrameCapture = AcquireFrameCaptureStorage(module, slice.CharacterRuntime.Entities.Count);
            CharacterTestRuntimeAiPlannerSnapshot snapshot = _activeFrameCapture.Display;
            CharacterTestRuntimeAiPlannerStateSnapshot stateSnapshot = _activeFrameCapture.State.Snapshot;
            CharacterTestRuntimeAiLabFrameSyncRuntimeEventSample runtimeEvents =
                CharacterTestRuntimeAiLabFrameSyncRuntimeEventSample.Capture(
                    slice.RuntimeEventQueueSnapshot,
                    slice.RecentRuntimeEvents);
            _activeFrameCapture.Capture(module, slice.CharacterRuntime, frame, runtimeEvents);
            bool branchTruncate = _branchTruncatePending
                && _timeline.SelectedFrame == _branchBaseFrame;
            if (branchTruncate)
                _timeline.MarkEditBranchStart();
            bool truncateFuture = branchTruncate || (_timeline.EditBranchStartFrame >= 0
                && _timeline.SelectedFrame == _timeline.EditBranchStartFrame);
            if (truncateFuture)
            {
                _timelineEventIndex.TruncateAfter(_timeline.SelectedFrame);
                TruncateAppliedRevisionEventsAfter(_timeline.SelectedFrame);
                ClearTimelineItemSelection();
            }
            _frameBuffer.Capture(
                frame,
                _activeFrameCapture.Frame.RealtimeSeconds,
                snapshot,
                truncateFuture,
                runtimeEvents,
                stateSnapshot,
                _activeFrameCapture.Frame);
            CaptureTimelineRecord(slice, frame, snapshot, stateSnapshot, runtimeEvents);
            CommitLatestLiveFrameSelection();
            if (branchTruncate)
            {
                _timeline.ClearEditBranchStart();
                _branchTruncatePending = false;
                _branchRecomputeArmed = false;
                _branchBaseFrame = 0;
                _branchOriginalLatestFrame = 0;
            }
            _lastCapturedRuntimeFrame = frame;
            _timeline.SetMode(_context != null && _context.Paused
                ? CharacterTestRuntimeAiPlannerDevTimelineMode.Paused
                : CharacterTestRuntimeAiPlannerDevTimelineMode.Live);
            ClampSelectedAgentIndex();
        }

        private void CommitLatestLiveFrameSelection()
        {
            if (_frameSelection.Phase != CharacterTestRuntimeAiLabFrameSelectionPhase.Idle
                || !_timelineSessionStarted
                || _timeline == null
                || !_timeline.SessionIdentity.IsValid
                || !_timeline.TryGetFrame(_timeline.LatestFrame, out CharacterTestRuntimeAiPlannerDevTimelineRecord record))
            {
                return;
            }

            int preferredAgent = ResolveSelectedAgentEntityId();
            int resolvedAgent = ResolveAgentForRecord(record, preferredAgent);
            CharacterTestRuntimeAiLabCommittedFrameBundle bundle = CreateCommittedFrameBundle(record);
            _frameSelection.CommitLiveFrame(resolvedAgent, bundle, out _, preferredAgent);
        }

        private void CaptureTimelineRecord(
            GameSlice slice,
            int frame,
            CharacterTestRuntimeAiPlannerSnapshot snapshot,
            CharacterTestRuntimeAiPlannerStateSnapshot stateSnapshot,
            CharacterTestRuntimeAiLabFrameSyncRuntimeEventSample runtimeEvents)
        {
            if (slice == null)
                return;

            if (_snapshotParticipant == null)
                _snapshotParticipant = slice.Services.Network.CreateSnapshotParticipant();

            var runtimeFrame = new RuntimeFrame(frame);
            long stateHash = _snapshotParticipant.ComputeCapturedStateHash(runtimeFrame, slice.RuntimeAiPlannerCommands, stateSnapshot);
            IReadOnlyList<RuntimeCommand> drainedCommands =
                slice.Services.Modules.RuntimeCommands.LastDrainedCommands;
            int evaluatedFrame = ResolveDecisionTraceFrameForPostTickSnapshot(frame);
            CharacterTestRuntimeAiLabDecisionTraceWriter traceWriter =
                _timeline.BeginDecisionTraceCapture(
                    _timeline.SessionIdentity.Generation,
                    frame,
                    out CharacterTestRuntimeAiLabDecisionTraceHandle traceHandle,
                    evaluatedFrame);
            CharacterTestRuntimeAiPlannerCommandModule plannerCommands =
                slice.Services.Modules.RuntimeAiPlannerCommands;
            if (plannerCommands != null)
            {
                // GameSlice.CurrentFrame identifies the next frame to execute after Tick(), while
                // evaluator diagnostics describe the frame that just completed. Timeline records
                // retain the post-tick snapshot, so attach the immediately preceding decision.
                try
                {
                    plannerCommands.WriteLatestBehaviorDecisionTraces(traceWriter, evaluatedFrame);
                }
                catch (Exception exception)
                {
                    // Runtime AI Lab diagnostics must never interrupt the authority tick or leak
                    // a bounded trace slot. Keep the RuntimeFrame record and mark its optional
                    // decision trace unavailable for this frame.
                    traceWriter.Seal();
                    _timeline.ReleaseDecisionTrace(traceHandle);
                    traceHandle = CharacterTestRuntimeAiLabDecisionTraceHandle.Invalid;
                    Debug.LogException(exception);
                }
            }
            else
                traceWriter.Seal();

            CharacterTestRuntimeAiPlannerDevTimelineRecord record;
            bool hasReplayAuthorityEvidence = false;
            int replayAuthorityFrame = 0;
            long replayAuthorityHash = 0L;
            if (_context?.NetworkRunner is ICharacterTestNetworkReplayEvidenceProvider evidenceProvider
                && evidenceProvider.TryGetLatestReplayEvidence(
                    out RuntimeFrame evidenceFrame,
                    out replayAuthorityHash)
                && evidenceFrame.Value >= int.MinValue
                && evidenceFrame.Value <= int.MaxValue)
            {
                replayAuthorityFrame = (int)evidenceFrame.Value;
                hasReplayAuthorityEvidence = replayAuthorityFrame == ResolveDecisionTraceFrameForPostTickSnapshot(frame);
            }
            try
            {
                record = _timeline.AddRecord(
                    frame,
                    _activeFrameCapture.Frame.RealtimeSeconds,
                    slice.Services.ElapsedSeconds,
                    snapshot,
                    drainedCommands,
                    authorityCheckpoint: null,
                    stateHash,
                    runtimeEvents,
                    _activeFrameCapture.AnimationInputs,
                    stateSnapshot,
                    traceHandle,
                    hasReplayAuthorityEvidence,
                    replayAuthorityFrame,
                    replayAuthorityHash,
                    _activeFrameCapture);
            }
            catch
            {
                _timeline.ReleaseDecisionTrace(traceHandle);
                throw;
            }
            _timelineEventIndex.CaptureFrame(
                frame,
                record.DrainedCommands,
                slice.RecentRuntimeEvents,
                slice.TraceRecorder,
                slice.Services.ActionRuntime?.TimelineEvents,
                CollectLatestBehaviorLogicEvents(slice.Services.Modules.RuntimeAiPlannerCommands),
                evaluatedFrame);
            BindAppliedRevisionEventsToRecord(record);
            // Content revisions invalidate the cached projection lazily. Keeping the last
            // immutable projection allows the live UI to enforce its 10 Hz refresh budget.
        }

        private void BindAppliedRevisionEventsToRecord(CharacterTestRuntimeAiPlannerDevTimelineRecord record)
        {
            if (record == null || _timeline == null || !_timeline.SessionIdentity.IsValid)
                return;
            long appliedRevisionHash = ComputeAppliedBehaviorRevisionHash(record.Frame);
            for (int i = 0; i < _appliedRevisionEvents.Count; i++)
            {
                CharacterTestRuntimeAiLabAppliedRevision applied = _appliedRevisionEvents[i];
                if (applied == null || applied.ApplicationFrame != record.Frame)
                    continue;
                if (!applied.TryBindRuntimeFrame(_timeline.SessionIdentity, record, appliedRevisionHash))
                {
                    _statusMessage = "Revision Apply 与 RuntimeFrame capture boundary 不一致。";
                    InvalidateTimelineProjection();
                }
            }
        }

        internal static int ResolveDecisionTraceFrameForPostTickSnapshot(int snapshotFrame)
        {
            return snapshotFrame > 0 ? snapshotFrame - 1 : snapshotFrame;
        }

        private void ApplyUnityTimeScaleForPlayback()
        {
            if (_phase != CharacterTestRuntimeAiPlannerDevScenePhase.Running || _context == null || _context.Paused)
            {
                RestoreUnityTimeScale();
                return;
            }

            Time.timeScale = _timeline.PlaybackSpeed;
        }

        private static void RestoreUnityTimeScale()
        {
            if (Math.Abs(Time.timeScale - 1f) > 0.0001f)
                Time.timeScale = 1f;
        }

        private string FormatSetupSelectionStatus(int teamId, int slotIndex)
        {
            if (!_matchConfig.TryGetSpawnPosition(teamId, slotIndex, out Vector2 position))
                return "Selected spawn slot.";

            return "Selected "
                + FormatSetupSlotName(teamId, slotIndex)
                + " | x "
                + position.x.ToString("0.##", CultureInfo.InvariantCulture)
                + " z "
                + position.y.ToString("0.##", CultureInfo.InvariantCulture)
                + ".";
        }

        private static string FormatSetupSlotName(int teamId, int slotIndex)
        {
            string team = teamId == CharacterTestArenaMatchConfig.Team2Id ? "Team 2" : "Team 1";
            return team + " Slot " + (slotIndex + 1).ToString(CultureInfo.InvariantCulture);
        }

        private int ResolveSelectedAgentCount()
        {
            if (!_frameBuffer.TryGetSelected(out CharacterTestRuntimeAiPlannerDevFrameSnapshot frame))
                return 0;

            IReadOnlyList<CharacterTestRuntimeAiPlannerAgentSnapshot> agents = frame.Agents;
            return agents != null ? agents.Count : 0;
        }

        /// <summary>
        /// Keeps the authoring target aligned with the arena slot that produced the selected
        /// runtime agent. This is an explicit UI selection operation and never runs in Tick.
        /// </summary>
        /// <remarks>LowFreqAlloc: scans the fixed six setup slots without allocating.</remarks>
        private void SyncSelectedSetupSlotToAgent(int agentIndex)
        {
            if (!_frameBuffer.TryGetSelected(out CharacterTestRuntimeAiPlannerDevFrameSnapshot frame))
                return;

            IReadOnlyList<CharacterTestRuntimeAiPlannerAgentSnapshot> agents = frame.Agents;
            if (agents == null || agentIndex < 0 || agentIndex >= agents.Count)
                return;

            CharacterTestRuntimeAiPlannerAgentSnapshot agent = agents[agentIndex];
            for (int teamId = CharacterTestArenaMatchConfig.Team1Id;
                 teamId <= CharacterTestArenaMatchConfig.Team2Id;
                 teamId++)
            {
                for (int slotIndex = 0;
                     slotIndex < CharacterTestArenaMatchConfig.SlotsPerTeam;
                     slotIndex++)
                {
                    CharacterTestArenaSlotConfig slot = _matchConfig?.TryGetSlot(teamId, slotIndex);
                    if (slot == null || !slot.Enabled)
                        continue;

                    int expectedLoadoutId = CharacterTestArenaBehaviorGroupDraftStore.ToSessionLoadoutId(
                        slot.BehaviorGroupDraftId);
                    if (!TryResolveSetupRuntimeAiPlannerProfileSlot(teamId, slotIndex, out int profileSlot)
                        || agent.RuntimeAiPlannerProfileSlot != profileSlot
                        || (agent.RuntimeAiBehaviorLoadoutId > 0
                            && agent.RuntimeAiBehaviorLoadoutId != expectedLoadoutId))
                    {
                        continue;
                    }

                    _selectedSetupTeamId = teamId;
                    _selectedSetupSlotIndex = slotIndex;
                    return;
                }
            }
        }

        private void ClampSelectedAgentIndex()
        {
            int count = ResolveSelectedAgentCount();
            if (count <= 0)
            {
                _selectedAgentIndex = 0;
                return;
            }

            if (_selectedAgentIndex < 0)
                _selectedAgentIndex = 0;
            else if (_selectedAgentIndex >= count)
                _selectedAgentIndex = count - 1;
        }
    }

    public enum CharacterTestRuntimeAiPlannerDevScenePhase
    {
        Setup,
        Running,
        Completed
    }

    internal enum CharacterTestRuntimeAiLabSessionStartPresentation
    {
        ArenaPaused,
        InspectFirstFrame
    }

    internal readonly struct CharacterTestRuntimeAiPlannerDevSceneSnapshot
    {
        public CharacterTestRuntimeAiPlannerDevSceneSnapshot(
            CharacterTestRuntimeAiPlannerDevScenePhase phase,
            string statusMessage,
            bool paused,
            bool debugEditGateEnabled,
            bool detailsExpanded,
            bool loopPlaybackActive,
            bool historyPlaybackActive,
            bool replayInProgress,
            bool branchRecomputeArmed,
            int branchRecomputeBaseFrame,
            int branchRecomputeOriginalLatestFrame,
            int selectedAgentIndex,
            CharacterTestRuntimeAiPlannerDevFrameBuffer frameBuffer,
            CharacterTestRuntimeAiPlannerDevTimeline timeline,
            CharacterTestRuntimeAiPlannerDevFrameSnapshot selectedFrame)
            : this(
                phase,
                statusMessage,
                paused,
                debugEditGateEnabled,
                detailsExpanded,
                loopPlaybackActive,
                historyPlaybackActive,
                replayInProgress,
                branchRecomputeArmed,
                branchRecomputeBaseFrame,
                branchRecomputeOriginalLatestFrame,
                false,
                0L,
                selectedAgentIndex,
                CharacterTestRuntimeAiLabFrameSelectionPhase.Idle,
                0L,
                default,
                default,
                default,
                frameBuffer,
                timeline,
                selectedFrame)
        {
        }

        public CharacterTestRuntimeAiPlannerDevSceneSnapshot(
            CharacterTestRuntimeAiPlannerDevScenePhase phase,
            string statusMessage,
            bool paused,
            bool debugEditGateEnabled,
            bool detailsExpanded,
            bool loopPlaybackActive,
            bool historyPlaybackActive,
            bool replayInProgress,
            bool branchRecomputeArmed,
            int branchRecomputeBaseFrame,
            int branchRecomputeOriginalLatestFrame,
            bool branchConfirmationPending,
            long branchConfirmationGeneration,
            int selectedAgentIndex,
            CharacterTestRuntimeAiLabFrameSelectionPhase frameSelectionPhase,
            long frameSelectionTransactionId,
            CharacterTestRuntimeAiLabFrameSelectionTarget activeRestoreTarget,
            CharacterTestRuntimeAiLabFrameSelectionTarget queuedTarget,
            CharacterTestRuntimeAiLabCommittedFrameReceipt committedFrameReceipt,
            CharacterTestRuntimeAiPlannerDevFrameBuffer frameBuffer,
            CharacterTestRuntimeAiPlannerDevTimeline timeline,
            CharacterTestRuntimeAiPlannerDevFrameSnapshot selectedFrame)
        {
            Phase = phase;
            StatusMessage = statusMessage ?? string.Empty;
            Paused = paused;
            DebugEditGateEnabled = debugEditGateEnabled;
            DetailsExpanded = detailsExpanded;
            LoopPlaybackActive = loopPlaybackActive;
            HistoryPlaybackActive = historyPlaybackActive;
            ReplayInProgress = replayInProgress;
            BranchRecomputeArmed = branchRecomputeArmed;
            BranchRecomputeBaseFrame = branchRecomputeBaseFrame;
            BranchRecomputeOriginalLatestFrame = branchRecomputeOriginalLatestFrame;
            BranchConfirmationPending = branchConfirmationPending;
            BranchConfirmationGeneration = branchConfirmationGeneration;
            SelectedAgentIndex = selectedAgentIndex;
            FrameSelectionPhase = frameSelectionPhase;
            FrameSelectionTransactionId = frameSelectionTransactionId;
            ActiveRestoreTarget = activeRestoreTarget;
            QueuedTarget = queuedTarget;
            CommittedFrameReceipt = committedFrameReceipt;
            FrameBuffer = frameBuffer;
            Timeline = timeline;
            SelectedFrame = selectedFrame;
        }

        public CharacterTestRuntimeAiPlannerDevScenePhase Phase { get; }
        public string StatusMessage { get; }
        public bool Paused { get; }
        public bool DebugEditGateEnabled { get; }
        public bool DetailsExpanded { get; }
        public bool LoopPlaybackActive { get; }
        public bool HistoryPlaybackActive { get; }
        public bool ReplayInProgress { get; }
        public bool BranchRecomputeArmed { get; }
        public int BranchRecomputeBaseFrame { get; }
        public int BranchRecomputeOriginalLatestFrame { get; }
        public bool BranchConfirmationPending { get; }
        public long BranchConfirmationGeneration { get; }
        public int SelectedAgentIndex { get; }
        public CharacterTestRuntimeAiLabFrameSelectionPhase FrameSelectionPhase { get; }
        public long FrameSelectionTransactionId { get; }
        public CharacterTestRuntimeAiLabFrameSelectionTarget ActiveRestoreTarget { get; }
        public CharacterTestRuntimeAiLabFrameSelectionTarget QueuedTarget { get; }
        public CharacterTestRuntimeAiLabCommittedFrameReceipt CommittedFrameReceipt { get; }
        public CharacterTestRuntimeAiPlannerDevFrameBuffer FrameBuffer { get; }
        public CharacterTestRuntimeAiPlannerDevTimeline Timeline { get; }
        public CharacterTestRuntimeAiPlannerDevFrameSnapshot SelectedFrame { get; }
    }
}
