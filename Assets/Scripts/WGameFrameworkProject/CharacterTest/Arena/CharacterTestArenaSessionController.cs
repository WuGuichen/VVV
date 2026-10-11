using System;
using System.Collections.Generic;
using System.Globalization;
using MxFramework.Input;
using MxFramework.Runtime;
using UnityEngine;

namespace MxFramework.Game.CharacterTest
{
    /// <summary>
    /// Scene-owned CharacterTest arena harness for choosing Runtime AI Planner strategies and running offline matches.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(94)]
    [AddComponentMenu("MxFramework/Game/Character Test/Arena Session Controller")]
    public sealed class CharacterTestArenaSessionController : MonoBehaviour
    {
        private const int VictoryPollIntervalFrames = 6;
        private const string CameraModeTraceId = "character-test.arena.camera-mode";
        private CharacterTestArenaMatchConfig _config = CharacterTestArenaMatchConfig.CreateProjectBacked();
        private GameManager _gameManager;
        private CharacterTestRuntimeContext _context;
        private CharacterTestRuntimeSessionBuilder _sessionBuilder;
        private CharacterTestArenaMatchPhase _phase = CharacterTestArenaMatchPhase.Setup;
        private string _statusMessage = "Configure both teams, then start the arena match.";
        private float _matchStartTime;
        private float _elapsedSeconds;
        private int _winnerTeamId;
        private int _team1Alive;
        private int _team2Alive;
        private int _team1Total;
        private int _team2Total;
        private int _nextVictoryPollFrame;
        private CharacterTestCharacterDefinitionRegistry _characterDefinitions =
            new CharacterTestCharacterDefinitionRegistry();

        public CharacterTestArenaMatchPhase Phase => _phase;
        internal CharacterTestArenaMatchConfig Config => _config;
        public string StatusMessage => _statusMessage;
        public float ElapsedSeconds => _phase == CharacterTestArenaMatchPhase.Running
            ? Mathf.Max(0f, Time.realtimeSinceStartup - _matchStartTime)
            : _elapsedSeconds;
        public int WinnerTeamId => _winnerTeamId;
        public int Team1Alive => _team1Alive;
        public int Team2Alive => _team2Alive;
        public int Team1Total => _team1Total;
        public int Team2Total => _team2Total;
        public bool ShouldAutoOpenSetupPanel => true;

        internal void Initialize(
            GameManager gameManager,
            CharacterTestRuntimeContext context,
            CharacterTestRuntimeSessionBuilder sessionBuilder)
        {
            _gameManager = gameManager;
            _context = context;
            _sessionBuilder = sessionBuilder;
            if (_config == null)
                _config = CharacterTestArenaMatchConfig.CreateProjectBacked();
        }

        internal void PrepareInitialSetupState()
        {
            _phase = CharacterTestArenaMatchPhase.Setup;
            _winnerTeamId = 0;
            _elapsedSeconds = 0f;
            _statusMessage = "Configure both teams, then start the arena match.";
            SetRuntimePausedForSetup();
        }

        internal CharacterTestArenaSnapshot CaptureSnapshot()
        {
            CharacterTestRuntimeAiPlannerBehaviorLoadoutRuntime loadouts = ResolveLoadoutRuntime();
            return new CharacterTestArenaSnapshot(
                _phase,
                _statusMessage,
                ElapsedSeconds,
                _winnerTeamId,
                _team1Alive,
                _team2Alive,
                _team1Total,
                _team2Total,
                _config.Clone(),
                loadouts,
                ResolveCurrentCameraMode());
        }

        public bool SetSlotEnabled(int teamId, int slotIndex, bool enabled)
        {
            CharacterTestArenaSlotConfig slot = _config.TryGetSlot(teamId, slotIndex);
            if (slot == null)
            {
                _statusMessage = "Selected arena slot is not available.";
                return false;
            }

            slot.Enabled = enabled;
            _statusMessage = "Arena slot updated.";
            return true;
        }

        public bool SelectNextCharacter(int teamId, int slotIndex, int direction)
        {
            CharacterTestArenaSlotConfig slot = _config.TryGetSlot(teamId, slotIndex);
            if (slot == null)
            {
                _statusMessage = "Selected arena slot is not available.";
                return false;
            }

            if (!_characterDefinitions.TrySelectNext(
                    slot.CharacterId,
                    direction,
                    out int selectedCharacterId))
            {
                _statusMessage = "No playable character package is available.";
                return false;
            }

            slot.CharacterId = selectedCharacterId;
            _statusMessage = "Character selected.";
            return true;
        }

        internal void SetCharacterDefinitionsForTests(
            CharacterTestCharacterDefinitionRegistry definitions)
        {
            _characterDefinitions = definitions ?? new CharacterTestCharacterDefinitionRegistry();
        }

        public bool SelectNextLoadout(int teamId, int slotIndex, int direction)
        {
            CharacterTestArenaSlotConfig slot = _config.TryGetSlot(teamId, slotIndex);
            if (slot == null)
            {
                _statusMessage = "Selected arena slot is not available.";
                return false;
            }

            CharacterTestArenaBehaviorGroupDraftStore drafts = _config.BehaviorGroups;
            if (drafts == null || drafts.Drafts.Count == 0)
            {
                _statusMessage = "No Arena behavior group draft is available.";
                return false;
            }

            slot.BehaviorGroupDraftId = drafts.SelectNextDraft(slot.BehaviorGroupDraftId, direction);
            slot.LoadoutId = CharacterTestArenaBehaviorGroupDraftStore.ToSessionLoadoutId(
                slot.BehaviorGroupDraftId);
            slot.VariantId = CharacterTestArenaBehaviorGroupDraftStore.SessionVariantId;
            _statusMessage = "Behavior group selected.";
            return true;
        }

        public bool SelectNextVariant(int teamId, int slotIndex, int direction)
        {
            return SelectNextLoadout(teamId, slotIndex, direction);
        }

        public bool StartBattle()
        {
            if (_config?.ProjectCatalogState != null && !_config.ProjectCatalogState.Success)
            {
                _statusMessage = _config.ProjectCatalogDiagnostic;
                return false;
            }
            if (_sessionBuilder == null)
            {
                _statusMessage = "Arena runtime is not initialized.";
                return false;
            }

            if (!_config.BehaviorGroups.TryCompileAll(out string validationMessage))
            {
                _statusMessage = validationMessage;
                return false;
            }
            if (!_config.TryFreezeProjectBehaviorCatalogs(out validationMessage))
            {
                _statusMessage = validationMessage;
                return false;
            }

            CharacterTestRuntimeAiPlannerBehaviorLoadoutRuntime loadouts = ResolveLoadoutRuntime();
            if (!_config.TryBuildSpawnSpecs(loadouts, out _, out validationMessage))
            {
                _statusMessage = validationMessage;
                return false;
            }

            _phase = CharacterTestArenaMatchPhase.Running;
            _winnerTeamId = 0;
            _elapsedSeconds = 0f;
            _matchStartTime = Time.realtimeSinceStartup;
            _nextVictoryPollFrame = Time.frameCount;
            _context.MainMenuVisible = false;
            _context.ManualPaused = false;
            _context.MainMenuCoordinator?.RefreshEffectivePauseState();
            _sessionBuilder.RebuildArenaBattleSession(_config, "Arena battle running.");
            if (_context.PlayerBehaviorDeckService != null
                && !_context.PlayerBehaviorDeckService.TryLockAndInstallForSession(
                    _context.Slice?.Services.Modules.RuntimeAiPlannerCommands,
                    _context.Slice?.Services.CharacterEntities,
                    out string deckLockError))
            {
                _phase = CharacterTestArenaMatchPhase.Setup;
                _winnerTeamId = 0;
                _elapsedSeconds = 0f;
                SetRuntimePausedForSetup();
                _sessionBuilder.RebuildArenaSetupSession(
                    _config,
                    "Behavior deck must be repaired before battle: " + deckLockError);
                _statusMessage = "Behavior deck must be repaired before battle: " + deckLockError;
                return false;
            }
            _context.Slice?.Services.RuntimeCommands?.SetRuntimeAiBehaviorSelectionUnlocked(false);
            _statusMessage = "Arena battle running.";
            RefreshAliveCounts();
            return true;
        }

        public bool Reconfigure()
        {
            if (_sessionBuilder == null)
            {
                _statusMessage = "Arena runtime is not initialized.";
                return false;
            }

            _phase = CharacterTestArenaMatchPhase.Setup;
            _winnerTeamId = 0;
            _elapsedSeconds = 0f;
            SetRuntimePausedForSetup();
            _sessionBuilder.RebuildArenaSetupSession(_config, "Arena setup.");
            _statusMessage = "Configure both teams, then start the arena match.";
            return true;
        }

        public bool Replay()
        {
            return StartBattle();
        }

        public bool SelectNextCameraMode()
        {
            return SelectCameraMode(GetNextCameraMode(ResolveCurrentCameraMode()));
        }

        public bool SelectCameraMode(CharacterTestCameraMode mode)
        {
            if (!IsArenaCameraMode(mode))
            {
                _statusMessage = "Arena camera mode is not supported.";
                return false;
            }

            if (_config != null)
                _config.PreferredCameraMode = mode;

            if (_phase != CharacterTestArenaMatchPhase.Running)
            {
                _statusMessage = "Arena default camera mode selected: " + FormatCameraMode(mode) + ".";
                return true;
            }

            CharacterTestSliceServices services = _context?.Slice?.Services;
            if (services == null)
            {
                _statusMessage = "Arena camera mode selected: " + FormatCameraMode(mode) + ".";
                return true;
            }

            RuntimeCommandValidationResult result = services.EnqueueSetCameraMode(
                mode,
                CharacterTestRuntimeCommandSources.CameraInput);
            if (!result.Success)
            {
                _statusMessage = "Arena camera command rejected: " + result.Error.Message;
                return false;
            }

            _statusMessage = "Arena camera mode selected: " + FormatCameraMode(mode) + ".";
            return true;
        }

        private void Update()
        {
            if (_phase != CharacterTestArenaMatchPhase.Running)
                return;

            _elapsedSeconds = Mathf.Max(0f, Time.realtimeSinceStartup - _matchStartTime);
            if (Time.frameCount < _nextVictoryPollFrame)
                return;

            _nextVictoryPollFrame = Time.frameCount + VictoryPollIntervalFrames;
            RefreshAliveCounts();
            TryResolveVictory();
        }

        private void RefreshAliveCounts()
        {
            _team1Alive = 0;
            _team2Alive = 0;
            _team1Total = 0;
            _team2Total = 0;

            IReadOnlyList<CharacterTestCharacterEntity> entities =
                _context?.Slice?.Services.CharacterEntities;
            for (int i = 0; entities != null && i < entities.Count; i++)
            {
                CharacterTestCharacterEntity entity = entities[i];
                if (entity == null)
                    continue;

                if (entity.Team.TeamId == CharacterTestArenaMatchConfig.Team1Id)
                {
                    _team1Total++;
                    if (entity.CombatState.IsAlive)
                        _team1Alive++;
                }
                else if (entity.Team.TeamId == CharacterTestArenaMatchConfig.Team2Id)
                {
                    _team2Total++;
                    if (entity.CombatState.IsAlive)
                        _team2Alive++;
                }
            }
        }

        private void TryResolveVictory()
        {
            if (_team1Total <= 0 || _team2Total <= 0)
                return;

            if (_team1Alive > 0 && _team2Alive > 0)
                return;

            if (_team1Alive <= 0 && _team2Alive <= 0)
                _winnerTeamId = 0;
            else
                _winnerTeamId = _team1Alive > 0 ? CharacterTestArenaMatchConfig.Team1Id : CharacterTestArenaMatchConfig.Team2Id;

            _phase = CharacterTestArenaMatchPhase.Finished;
            _elapsedSeconds = Mathf.Max(0f, Time.realtimeSinceStartup - _matchStartTime);
            _statusMessage = _winnerTeamId > 0
                ? FormatWinnerStatus(_winnerTeamId)
                : "Arena battle ended in a draw.";
            SetRuntimePausedForSetup();
        }

        private void SetRuntimePausedForSetup()
        {
            if (_context == null)
                return;

            _context.MainMenuVisible = false;
            _context.ManualPaused = true;
            _context.MainMenuCoordinator?.RefreshEffectivePauseState();
        }

        private CharacterTestRuntimeAiPlannerBehaviorLoadoutRuntime ResolveLoadoutRuntime()
        {
            return _context?.Slice?.Services.Modules.RuntimeAiBehaviorLoadouts;
        }

        private CharacterTestCameraMode ResolveCurrentCameraMode()
        {
            CharacterTestSliceServices services = _context?.Slice?.Services;
            if (services != null && IsArenaCameraMode(services.CameraMode))
                return services.CameraMode;
            return _config != null ? _config.PreferredCameraMode : CharacterTestCameraMode.MultiTargetAuto;
        }

        internal static CharacterTestCameraMode GetNextCameraMode(CharacterTestCameraMode mode)
        {
            switch (mode)
            {
                case CharacterTestCameraMode.ThirdPersonControlled:
                    return CharacterTestCameraMode.MultiTargetAuto;
                default:
                    return CharacterTestCameraMode.ThirdPersonControlled;
            }
        }

        internal static bool IsArenaCameraMode(CharacterTestCameraMode mode)
        {
            return mode == CharacterTestCameraMode.ThirdPersonControlled
                || mode == CharacterTestCameraMode.MultiTargetAuto;
        }

        internal static string FormatCameraMode(CharacterTestCameraMode mode)
        {
            switch (mode)
            {
                case CharacterTestCameraMode.ThirdPersonControlled:
                    return "Third Person";
                default:
                    return "Auto Watch";
            }
        }

        private static string FormatWinnerStatus(int winnerTeamId)
        {
            if (winnerTeamId == CharacterTestArenaMatchConfig.Team1Id)
                return "我方胜利.";
            if (winnerTeamId == CharacterTestArenaMatchConfig.Team2Id)
                return "敌方胜利.";

            return "Team " + winnerTeamId.ToString(CultureInfo.InvariantCulture) + " wins.";
        }

    }

    public enum CharacterTestArenaMatchPhase
    {
        Setup,
        Running,
        Finished
    }

    internal sealed class CharacterTestArenaMatchConfig
    {
        public const int Team1Id = 1;
        public const int Team2Id = 2;
        public const int SlotsPerTeam = 3;
        private static readonly float[] SlotOffsets = { -2f, 0f, 2f };
        private static readonly Vector2[] DefaultTeam1SpawnPositions =
        {
            new Vector2(SlotOffsets[0], -3f),
            new Vector2(SlotOffsets[1], -3f),
            new Vector2(SlotOffsets[2], -3f)
        };
        private static readonly Vector2[] DefaultTeam2SpawnPositions =
        {
            new Vector2(SlotOffsets[0], 3f),
            new Vector2(SlotOffsets[1], 3f),
            new Vector2(SlotOffsets[2], 3f)
        };
        private readonly CharacterTestArenaSlotConfig[] _team1Slots;
        private readonly CharacterTestArenaSlotConfig[] _team2Slots;
        private Vector2[] _team1SpawnPositions;
        private Vector2[] _team2SpawnPositions;

        public CharacterTestArenaMatchConfig()
        {
            _team1Slots = CreateSlots(Team1Id);
            _team2Slots = CreateSlots(Team2Id);
            _team1SpawnPositions = CopySpawnPositions(DefaultTeam1SpawnPositions);
            _team2SpawnPositions = CopySpawnPositions(DefaultTeam2SpawnPositions);
            BehaviorGroups = CharacterTestArenaBehaviorGroupDraftStore.CreateEmpty();
            PreferredCameraMode = CharacterTestCameraMode.MultiTargetAuto;
            PresentationTeamId = Team1Id;
        }

        private CharacterTestArenaMatchConfig(
            CharacterTestArenaSlotConfig[] team1Slots,
            CharacterTestArenaSlotConfig[] team2Slots,
            CharacterTestArenaBehaviorGroupDraftStore behaviorGroups,
            CharacterTestCameraMode preferredCameraMode,
            int presentationTeamId,
            Vector2[] team1SpawnPositions,
            Vector2[] team2SpawnPositions,
            CharacterTestRuntimeAiPlannerProjectCatalogState projectCatalogState = null,
            CharacterTestRuntimeAiPlannerExperimentCatalogSnapshot frozenProjectCatalogSnapshot = null)
        {
            _team1Slots = CopySlots(team1Slots, Team1Id);
            _team2Slots = CopySlots(team2Slots, Team2Id);
            _team1SpawnPositions = CopySpawnPositions(team1SpawnPositions, DefaultTeam1SpawnPositions);
            _team2SpawnPositions = CopySpawnPositions(team2SpawnPositions, DefaultTeam2SpawnPositions);
            BehaviorGroups = behaviorGroups != null
                ? behaviorGroups.Clone()
                : CharacterTestArenaBehaviorGroupDraftStore.CreateEmpty();
            PreferredCameraMode = CharacterTestArenaSessionController.IsArenaCameraMode(preferredCameraMode)
                ? preferredCameraMode
                : CharacterTestCameraMode.MultiTargetAuto;
            PresentationTeamId = presentationTeamId == Team2Id ? Team2Id : Team1Id;
            ProjectCatalogState = projectCatalogState;
            FrozenProjectCatalogSnapshot = frozenProjectCatalogSnapshot;
        }

        public IReadOnlyList<CharacterTestArenaSlotConfig> Team1Slots => _team1Slots;
        public IReadOnlyList<CharacterTestArenaSlotConfig> Team2Slots => _team2Slots;
        public CharacterTestArenaBehaviorGroupDraftStore BehaviorGroups { get; private set; }
        public CharacterTestRuntimeAiPlannerProjectCatalogState ProjectCatalogState { get; private set; }
        public string ProjectCatalogDiagnostic => ProjectCatalogState?.Diagnostic ?? string.Empty;
        public CharacterTestRuntimeAiPlannerExperimentCatalogSnapshot FrozenProjectCatalogSnapshot { get; private set; }
        public CharacterTestCameraMode PreferredCameraMode { get; set; }
        public int PresentationTeamId { get; set; }

        public static CharacterTestArenaMatchConfig CreateDefault()
        {
            var config = new CharacterTestArenaMatchConfig();
            config.BehaviorGroups = CharacterTestArenaBehaviorGroupDraftStore.CreateDefault();
            config._team1Slots[0].Enabled = true;
            config._team2Slots[0].Enabled = true;
            return config;
        }

        public static CharacterTestArenaMatchConfig CreateProjectBacked()
        {
            CharacterTestRuntimeAiPlannerProjectCatalogState state =
                CharacterTestRuntimeAiPlannerProjectCatalogLoader.Load();
            return CreateProjectBacked(state);
        }

        internal static CharacterTestArenaMatchConfig CreateProjectBacked(
            CharacterTestRuntimeAiPlannerProjectCatalogState state)
        {
            state = state ?? new CharacterTestRuntimeAiPlannerProjectCatalogState(
                null,
                null,
                string.Empty,
                string.Empty,
                string.Empty,
                string.Empty,
                "项目行为目录不可用。");
            var config = new CharacterTestArenaMatchConfig
            {
                BehaviorGroups = state.CreateArenaStore(),
                ProjectCatalogState = state
            };
            config._team1Slots[0].Enabled = true;
            config._team2Slots[0].Enabled = true;
            CharacterTestArenaBehaviorGroupDraft first = config.BehaviorGroups.GetDefaultDraft();
            if (first != null)
            {
                config._team1Slots[0].BehaviorGroupDraftId = first.DraftId;
                config._team2Slots[0].BehaviorGroupDraftId = first.DraftId;
                config._team1Slots[0].LoadoutId = CharacterTestArenaBehaviorGroupDraftStore.ToSessionLoadoutId(first.DraftId);
                config._team2Slots[0].LoadoutId = CharacterTestArenaBehaviorGroupDraftStore.ToSessionLoadoutId(first.DraftId);
            }
            return config;
        }

        public CharacterTestArenaMatchConfig Clone()
        {
            return new CharacterTestArenaMatchConfig(
                _team1Slots,
                _team2Slots,
                BehaviorGroups,
                PreferredCameraMode,
                PresentationTeamId,
                _team1SpawnPositions,
                _team2SpawnPositions,
                ProjectCatalogState,
                FrozenProjectCatalogSnapshot);
        }

        internal bool TryFreezeProjectBehaviorCatalogs(out string diagnostic)
        {
            diagnostic = string.Empty;
            if (ProjectCatalogState == null)
                return true;
            if (!ProjectCatalogState.Success)
            {
                diagnostic = ProjectCatalogDiagnostic;
                return false;
            }
            var groups = new List<CharacterTestRuntimeAiPlannerBehaviorGroupRevision>();
            if (!CollectSelectedGroups(_team1Slots, groups, out diagnostic)
                || !CollectSelectedGroups(_team2Slots, groups, out diagnostic))
            {
                return false;
            }
            var snapshot = new CharacterTestRuntimeAiPlannerExperimentCatalogSnapshot(
                ProjectCatalogState.CardCatalog.CatalogHash,
                ProjectCatalogState.GroupCatalog.CatalogHash,
                groups.ToArray());
            if (!snapshot.Success)
            {
                diagnostic = snapshot.Diagnostic;
                return false;
            }
            FrozenProjectCatalogSnapshot = snapshot;
            return true;
        }

        internal void UpdateProjectCatalogState(CharacterTestRuntimeAiPlannerProjectCatalogState state)
        {
            ProjectCatalogState = state;
            FrozenProjectCatalogSnapshot = null;
        }

        internal bool AdoptPublishedProjectGroup(
            CharacterTestRuntimeAiPlannerBehaviorGroupRevision group,
            CharacterTestRuntimeAiPlannerProjectCatalogState state)
        {
            if (group == null || state == null || !state.Success)
                return false;
            if (!BehaviorGroups.TryAdoptProjectRevision(group))
                return false;
            UpdateProjectCatalogState(state);
            return true;
        }

        private bool CollectSelectedGroups(
            IReadOnlyList<CharacterTestArenaSlotConfig> slots,
            List<CharacterTestRuntimeAiPlannerBehaviorGroupRevision> destination,
            out string diagnostic)
        {
            for (int i = 0; i < slots.Count; i++)
            {
                CharacterTestArenaSlotConfig slot = slots[i];
                if (!slot.Enabled)
                    continue;
                if (!BehaviorGroups.TryGetDraft(slot.BehaviorGroupDraftId, out CharacterTestArenaBehaviorGroupDraft draft)
                    || !ProjectCatalogState.GroupCatalog.TryGetExact(
                        draft.StableId,
                        draft.SourceProjectRevision,
                        out CharacterTestRuntimeAiPlannerBehaviorGroupRevision group)
                    || group.ContentHash != draft.SourceProjectContentHash)
                {
                    diagnostic = "无法冻结所选项目行为组的具体 revision/hash。";
                    return false;
                }
                bool exists = false;
                for (int j = 0; j < destination.Count; j++)
                {
                    if (string.Equals(destination[j].StableGroupId, group.StableGroupId, StringComparison.Ordinal)
                        && destination[j].Revision == group.Revision)
                    {
                        exists = true;
                        break;
                    }
                }
                if (!exists)
                    destination.Add(group);
            }
            diagnostic = string.Empty;
            return true;
        }

        internal void SetSpawnPositions(
            IReadOnlyList<Vector2> team1SpawnPositions,
            IReadOnlyList<Vector2> team2SpawnPositions)
        {
            _team1SpawnPositions = CopySpawnPositions(team1SpawnPositions, DefaultTeam1SpawnPositions);
            _team2SpawnPositions = CopySpawnPositions(team2SpawnPositions, DefaultTeam2SpawnPositions);
        }

        internal bool TryGetSpawnPosition(int teamId, int slotIndex, out Vector2 position)
        {
            IReadOnlyList<Vector2> positions = ResolveSpawnPositions(teamId);
            if (positions == null || slotIndex < 0 || slotIndex >= SlotsPerTeam)
            {
                position = Vector2.zero;
                return false;
            }

            position = positions[slotIndex];
            return true;
        }

        internal bool SetSpawnPosition(int teamId, int slotIndex, Vector2 position)
        {
            Vector2[] positions = ResolveMutableSpawnPositions(teamId);
            if (positions == null || slotIndex < 0 || slotIndex >= SlotsPerTeam)
                return false;

            positions[slotIndex] = position;
            return true;
        }

        public CharacterTestArenaSlotConfig TryGetSlot(int teamId, int slotIndex)
        {
            if (slotIndex < 0 || slotIndex >= SlotsPerTeam)
                return null;

            if (teamId == Team1Id)
                return _team1Slots[slotIndex];
            if (teamId == Team2Id)
                return _team2Slots[slotIndex];

            return null;
        }

        public int CountEnabledSlots(int teamId)
        {
            IReadOnlyList<CharacterTestArenaSlotConfig> slots = teamId == Team1Id ? _team1Slots : _team2Slots;
            int count = 0;
            for (int i = 0; i < slots.Count; i++)
            {
                if (slots[i].Enabled)
                    count++;
            }

            return count;
        }

        public bool TryBuildSpawnSpecs(
            CharacterTestRuntimeAiPlannerBehaviorLoadoutRuntime loadouts,
            out CharacterTestCharacterSpawnSpec[] specs,
            out string message)
        {
            specs = Array.Empty<CharacterTestCharacterSpawnSpec>();
            if (loadouts == null)
            {
                message = "Runtime AI Planner behavior loadouts are unavailable.";
                return false;
            }

            int team1Count = CountEnabledSlots(Team1Id);
            int team2Count = CountEnabledSlots(Team2Id);
            if (team1Count <= 0 || team2Count <= 0)
            {
                message = "Both arena teams must have at least one enabled slot.";
                return false;
            }

            var registry = new CharacterTestCharacterDefinitionRegistry();
            specs = new CharacterTestCharacterSpawnSpec[team1Count + team2Count];
            int index = 0;
            if (!AppendTeamSpecs(
                    _team1Slots,
                    Team1Id,
                    _team1SpawnPositions,
                    loadouts,
                    registry,
                    specs,
                    ref index,
                    out message))
            {
                specs = Array.Empty<CharacterTestCharacterSpawnSpec>();
                return false;
            }

            if (!AppendTeamSpecs(
                    _team2Slots,
                    Team2Id,
                    _team2SpawnPositions,
                    loadouts,
                    registry,
                    specs,
                    ref index,
                    out message))
            {
                specs = Array.Empty<CharacterTestCharacterSpawnSpec>();
                return false;
            }

            message = string.Empty;
            return true;
        }

        private static bool AppendTeamSpecs(
            IReadOnlyList<CharacterTestArenaSlotConfig> slots,
            int teamId,
            IReadOnlyList<Vector2> spawnPositions,
            CharacterTestRuntimeAiPlannerBehaviorLoadoutRuntime loadouts,
            CharacterTestCharacterDefinitionRegistry registry,
            CharacterTestCharacterSpawnSpec[] specs,
            ref int index,
            out string message)
        {
            for (int i = 0; i < slots.Count; i++)
            {
                CharacterTestArenaSlotConfig slot = slots[i];
                if (!slot.Enabled)
                    continue;

                if (!registry.TryGet(slot.CharacterId, out _))
                {
                    message = "Arena character is not available. characterId="
                        + slot.CharacterId.ToString(CultureInfo.InvariantCulture);
                    return false;
                }

                if (!loadouts.TryResolveSelection(
                        CharacterTestArenaBehaviorGroupDraftStore.ToSessionLoadoutId(
                            slot.BehaviorGroupDraftId),
                        CharacterTestArenaBehaviorGroupDraftStore.SessionVariantId,
                        out CharacterTestRuntimeAiBehaviorSelection selection,
                        out message))
                {
                    return false;
                }

                Vector2 spawn = ResolveSpawnPosition(spawnPositions, teamId, i);
                specs[index++] = new CharacterTestCharacterSpawnSpec(
                    slot.CharacterId,
                    teamId,
                    spawn.x,
                    1f,
                    spawn.y,
                    CharacterTestCharacterControlOwner.AiControlled,
                    runtimeAiBehaviorSelection: selection);
            }

            message = string.Empty;
            return true;
        }

        private static CharacterTestArenaSlotConfig[] CreateSlots(int teamId)
        {
            var slots = new CharacterTestArenaSlotConfig[SlotsPerTeam];
            for (int i = 0; i < slots.Length; i++)
                slots[i] = CharacterTestArenaSlotConfig.CreateDefault(teamId, i);
            return slots;
        }

        private static Vector2 ResolveSpawnPosition(
            IReadOnlyList<Vector2> positions,
            int teamId,
            int slotIndex)
        {
            if (positions != null && slotIndex >= 0 && slotIndex < positions.Count)
                return positions[slotIndex];

            IReadOnlyList<Vector2> defaults = teamId == Team2Id
                ? DefaultTeam2SpawnPositions
                : DefaultTeam1SpawnPositions;
            return defaults != null && slotIndex >= 0 && slotIndex < defaults.Count
                ? defaults[slotIndex]
                : Vector2.zero;
        }

        private IReadOnlyList<Vector2> ResolveSpawnPositions(int teamId)
        {
            if (teamId == Team1Id)
                return _team1SpawnPositions;
            if (teamId == Team2Id)
                return _team2SpawnPositions;
            return null;
        }

        private Vector2[] ResolveMutableSpawnPositions(int teamId)
        {
            if (teamId == Team1Id)
                return _team1SpawnPositions;
            if (teamId == Team2Id)
                return _team2SpawnPositions;
            return null;
        }

        private static Vector2[] CopySpawnPositions(
            IReadOnlyList<Vector2> positions,
            IReadOnlyList<Vector2> fallback = null)
        {
            IReadOnlyList<Vector2> source = positions != null && positions.Count >= SlotsPerTeam
                ? positions
                : fallback;
            var copy = new Vector2[SlotsPerTeam];
            for (int i = 0; i < copy.Length; i++)
                copy[i] = source != null && i < source.Count ? source[i] : Vector2.zero;
            return copy;
        }

        private static CharacterTestArenaSlotConfig[] CopySlots(
            IReadOnlyList<CharacterTestArenaSlotConfig> source,
            int teamId)
        {
            var copy = new CharacterTestArenaSlotConfig[SlotsPerTeam];
            for (int i = 0; i < copy.Length; i++)
            {
                copy[i] = source != null && i < source.Count && source[i] != null
                    ? source[i].Clone()
                    : CharacterTestArenaSlotConfig.CreateDefault(teamId, i);
            }

            return copy;
        }
    }

    internal sealed class CharacterTestArenaSlotConfig
    {
        private CharacterTestArenaSlotConfig(
            int teamId,
            int slotIndex,
            bool enabled,
            int characterId,
            int loadoutId,
            int variantId,
            int behaviorGroupDraftId)
        {
            TeamId = teamId;
            SlotIndex = slotIndex;
            Enabled = enabled;
            CharacterId = characterId;
            LoadoutId = loadoutId;
            VariantId = variantId;
            BehaviorGroupDraftId = behaviorGroupDraftId > 0
                ? behaviorGroupDraftId
                : CharacterTestArenaBehaviorGroupDraftStore.DefaultDraftId;
        }

        public int TeamId { get; }
        public int SlotIndex { get; }
        public bool Enabled { get; set; }
        public int CharacterId { get; set; }
        public int LoadoutId { get; set; }
        public int VariantId { get; set; }
        public int BehaviorGroupDraftId { get; set; }

        public static CharacterTestArenaSlotConfig CreateDefault(int teamId, int slotIndex)
        {
            return new CharacterTestArenaSlotConfig(
                teamId,
                slotIndex,
                enabled: false,
                characterId: CharacterTestCharacterDefinitionRegistry.DefaultPlayableCharacterId,
                loadoutId: CharacterTestArenaBehaviorGroupDraftStore.ToSessionLoadoutId(
                    CharacterTestArenaBehaviorGroupDraftStore.DefaultDraftId),
                variantId: CharacterTestArenaBehaviorGroupDraftStore.SessionVariantId,
                behaviorGroupDraftId: CharacterTestArenaBehaviorGroupDraftStore.DefaultDraftId);
        }

        public CharacterTestArenaSlotConfig Clone()
        {
            return new CharacterTestArenaSlotConfig(
                TeamId,
                SlotIndex,
                Enabled,
                CharacterId,
                LoadoutId,
                VariantId,
                BehaviorGroupDraftId);
        }
    }

    internal readonly struct CharacterTestArenaSnapshot
    {
        public CharacterTestArenaSnapshot(
            CharacterTestArenaMatchPhase phase,
            string statusMessage,
            float elapsedSeconds,
            int winnerTeamId,
            int team1Alive,
            int team2Alive,
            int team1Total,
            int team2Total,
            CharacterTestArenaMatchConfig config,
            CharacterTestRuntimeAiPlannerBehaviorLoadoutRuntime loadoutRuntime,
            CharacterTestCameraMode cameraMode)
        {
            Phase = phase;
            StatusMessage = statusMessage ?? string.Empty;
            ElapsedSeconds = elapsedSeconds;
            WinnerTeamId = winnerTeamId;
            Team1Alive = team1Alive;
            Team2Alive = team2Alive;
            Team1Total = team1Total;
            Team2Total = team2Total;
            Config = config;
            LoadoutRuntime = loadoutRuntime;
            CameraMode = cameraMode;
        }

        public CharacterTestArenaMatchPhase Phase { get; }
        public string StatusMessage { get; }
        public float ElapsedSeconds { get; }
        public int WinnerTeamId { get; }
        public int Team1Alive { get; }
        public int Team2Alive { get; }
        public int Team1Total { get; }
        public int Team2Total { get; }
        public CharacterTestArenaMatchConfig Config { get; }
        public CharacterTestRuntimeAiPlannerBehaviorLoadoutRuntime LoadoutRuntime { get; }
        public CharacterTestCameraMode CameraMode { get; }
    }
}
