using System;
using System.Collections.Generic;
using MxFramework.Resources;
using MxFramework.Resources.Unity;
using MxFramework.Runtime;
using MxFramework.Story;
using MxFramework.Story.Runtime;

namespace MxFramework.Game.CharacterTest
{
    public enum CharacterTestBattleOutcome { None = 0, Victory = 1, Defeat = 2 }

    public sealed class CharacterTestStoryFlowBridge : IDisposable
    {
        private readonly StoryRuntimeModule _storyModule;
        private readonly GlobalResourceRuntimeServices _resources;
        private readonly IRuntimeLogger _logger;
        private readonly Func<RuntimeFrame> _currentFrameProvider;
        private readonly string _baseResourcePreloadGroupId;
        private readonly IDisposable _subscription;
        private readonly RuntimeLogBuffer _logBuffer = new RuntimeLogBuffer(128);
        private readonly ICharacterTestCharacterSpawnFlow _characterSpawnFlow;
        private readonly CharacterTestCharacterSpawnSpec[] _startupSpawnSpecs;
        private readonly HashSet<(int Graph, int Beat, int Step)> _requestedStartupSteps =
            new HashSet<(int Graph, int Beat, int Step)>();
        private bool _disposed;
        private ulong _battleStageRunId;
        private int _battleGraphId = CharacterTestStoryIds.BootstrapGraph;
        private Func<ulong, long, bool> _requestBattleRetry, _requestBattleNextWave;
        private Func<bool> _canLeaveBattle;
        private Action<long> _beginBattleTransfer;
        private bool _battleTransitionRequested;
        private bool _directStartupRequested;

        public CharacterTestBattleOutcome LastBattleOutcome { get; private set; }
        public bool? LastBattleVictory => LastBattleOutcome == CharacterTestBattleOutcome.None
            ? (bool?)null : LastBattleOutcome == CharacterTestBattleOutcome.Victory;
        public StoryEvent LastBattleOutcomeStep { get; private set; }
        /// <summary>Presentation notification only; callbacks must not reenter StoryDirector or advance the world.</summary>
        public event Action<StoryEvent> BattleOutcomeStarted;

        /// <summary>Cold per-session binding to original Story command requests and tactical time ownership.</summary>
        internal void BindTacticalBattle(ulong stageRunId, int graphId, Func<bool> canLeaveBattle,
            Func<ulong, long, bool> requestRetry, Func<ulong, long, bool> requestNextWave, Action<long> beginTransfer)
        {
            if (stageRunId == 0 || graphId <= 0) throw new ArgumentOutOfRangeException(nameof(stageRunId));
            if (_battleStageRunId != 0) throw new InvalidOperationException("Use a fresh Story bridge for a new StageRun.");
            _battleStageRunId = stageRunId; _battleGraphId = graphId;
            _canLeaveBattle = canLeaveBattle ?? throw new ArgumentNullException(nameof(canLeaveBattle));
            _beginBattleTransfer = beginTransfer ?? throw new ArgumentNullException(nameof(beginTransfer));
            _requestBattleRetry = requestRetry; _requestBattleNextWave = requestNextWave;
        }

        /// <summary>LowFreqAlloc. Enqueue through the bound Story owner; acceptance releases only this session's pause.</summary>
        public bool TryRequestRetry() => TryRequestBattleTransition(_requestBattleRetry);

        /// <summary>LowFreqAlloc. Next wave requires an accepted victory Step and a drained old reliable transaction.</summary>
        public bool TryRequestNextWave() => LastBattleOutcome == CharacterTestBattleOutcome.Victory &&
            TryRequestBattleTransition(_requestBattleNextWave);

        private bool TryRequestBattleTransition(Func<ulong, long, bool> request)
        {
            if (_disposed || _battleStageRunId == 0 || _battleTransitionRequested || request == null || !_canLeaveBattle()) return false;
            long frame = _currentFrameProvider().Value;
            if (frame == long.MaxValue || !request(_battleStageRunId, frame + 1)) return false;
            _battleTransitionRequested = true;
            _beginBattleTransfer(frame);
            return true;
        }

        internal bool SuppressMissingStartupSpawnWarning { get; set; }

        internal bool TryBootstrapStartupWithoutStory()
        {
            if (_disposed || _directStartupRequested || _characterSpawnFlow == null)
                return false;
            if (!_resources.TryPreloadGroup(_baseResourcePreloadGroupId, out _, out string errorMessage))
            {
                _logBuffer.Clear()
                    .Append("Preparation resource preload failed. groupId=")
                    .Append(string.IsNullOrWhiteSpace(_baseResourcePreloadGroupId) ? "<none>" : _baseResourcePreloadGroupId)
                    .Append(", error=")
                    .Append(errorMessage);
                _logger.Warning("StoryFlow", _logBuffer);
                return false;
            }
            CharacterTestCharacterSpawnSpec[] specs = _startupSpawnSpecs ?? CreateLegacyStartupSpawnSpecs();
            if (specs.Length == 0)
                return SuppressMissingStartupSpawnWarning;
            _directStartupRequested = true;
            bool accepted = true;
            for (int i = 0; i < specs.Length; i++)
                accepted &= _characterSpawnFlow.RequestSpawn(specs[i]);
            if (!accepted)
                _logger.Warning("StoryFlow", "Preparation startup character Spawn was rejected.");
            return accepted;
        }

        public CharacterTestStoryFlowBridge(
            StoryRuntimeModule storyModule,
            GlobalResourceRuntimeServices resources,
            IRuntimeLogger logger,
            Func<RuntimeFrame> currentFrameProvider,
            string baseResourcePreloadGroupId,
            ICharacterTestCharacterSpawnFlow characterSpawnFlow,
            IReadOnlyList<CharacterTestCharacterSpawnSpec> startupSpawnSpecs = null)
        {
            _storyModule = storyModule ?? throw new ArgumentNullException(nameof(storyModule));
            _resources = resources ?? throw new ArgumentNullException(nameof(resources));
            _logger = logger ?? NullRuntimeLogger.Instance;
            _currentFrameProvider = currentFrameProvider ?? (() => RuntimeFrame.Zero);
            _baseResourcePreloadGroupId = baseResourcePreloadGroupId ?? string.Empty;
            _subscription = _storyModule.Director.SubscribeEvents(OnStoryEvent);
            _characterSpawnFlow = characterSpawnFlow;
            _startupSpawnSpecs = CopyStartupSpawnSpecs(startupSpawnSpecs);
            _logger.Info("StoryFlow", "Bridge attached");
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            _subscription.Dispose();
            BattleOutcomeStarted = null;
            _requestBattleRetry = null; _requestBattleNextWave = null;
            _canLeaveBattle = null; _beginBattleTransfer = null;
            _disposed = true;
        }

        private void OnStoryEvent(StoryEvent evt)
        {
            if (_disposed || evt.Kind != StoryEventKind.StepStarted)
                return;

            switch (evt.AuxId)
            {
                case CharacterTestStoryIds.Actions.OpenLoadingUi:
                    OpenLoadingUi(evt);
                    break;
                case CharacterTestStoryIds.Actions.LoadBaseResources:
                    LoadBaseResources(evt);
                    break;
                case CharacterTestStoryIds.Actions.OpenStartupUi:
                    OpenStartupUi(evt);
                    break;
                case CharacterTestStoryIds.Actions.TacticalBattleVictory:
                case CharacterTestStoryIds.Actions.TacticalBattleDefeat:
                    RecordBattleOutcome(evt);
                    break;
            }
        }

        private void RecordBattleOutcome(StoryEvent evt)
        {
            bool victory = evt.AuxId == CharacterTestStoryIds.Actions.TacticalBattleVictory;
            if (evt.GraphId != _battleGraphId || evt.BeatInstanceId <= 0 ||
                evt.StepId != (victory ? CharacterTestStoryIds.Steps.TacticalBattleVictory : CharacterTestStoryIds.Steps.TacticalBattleDefeat) ||
                LastBattleOutcome != CharacterTestBattleOutcome.None) return;
            LastBattleOutcome = victory ? CharacterTestBattleOutcome.Victory : CharacterTestBattleOutcome.Defeat;
            LastBattleOutcomeStep = evt;
            BattleOutcomeStarted?.Invoke(evt);
        }

        private void OpenLoadingUi(StoryEvent evt)
        {
            _logBuffer.Clear()
                .Append("OpenLoadingUi requested. stepId=")
                .Append(evt.StepId);
            _logger.Info("StoryFlow", _logBuffer);
        }

        private void LoadBaseResources(StoryEvent evt)
        {
            _logBuffer.Clear()
                .Append("LoadBaseResources requested. stepId=")
                .Append(evt.StepId)
                .Append(", beatInstanceId=")
                .Append(evt.BeatInstanceId);
            _logger.Info("StoryFlow", _logBuffer);

            if (!_resources.TryPreloadGroup(_baseResourcePreloadGroupId, out ResourcePreloadResult result, out string errorMessage))
            {
                _logBuffer.Clear()
                    .Append("LoadBaseResources failed. mode=")
                    .Append(_resources.Mode.ToString())
                    .Append(", groupId=")
                    .Append(string.IsNullOrWhiteSpace(_baseResourcePreloadGroupId) ? "<none>" : _baseResourcePreloadGroupId)
                    .Append(", error=")
                    .Append(errorMessage);
                _logger.Warning("StoryFlow", _logBuffer);
                return;
            }

            _logBuffer.Clear()
                .Append("LoadBaseResources completed. mode=")
                .Append(_resources.Mode.ToString())
                .Append(", groupId=")
                .Append(string.IsNullOrWhiteSpace(_baseResourcePreloadGroupId) ? "<none>" : _baseResourcePreloadGroupId)
                .Append(", loaded=")
                .Append(result != null ? result.LoadedCount : 0);
            _logger.Info("StoryFlow", _logBuffer);
            CompletePresentationNextFrame(evt, new RuntimeTraceIdentity(90877));
        }

        private void OpenStartupUi(StoryEvent evt)
        {
            _logBuffer.Clear()
                .Append("OpenStartupUi requested. stepId=")
                .Append(evt.StepId);
            _logger.Info("StoryFlow", _logBuffer);
            if (_characterSpawnFlow == null)
            {
                _logger.Warning("StoryFlow", "Character spawn flow is not bound.");
                return;
            }

            CharacterTestCharacterSpawnSpec[] specs = _startupSpawnSpecs ?? CreateLegacyStartupSpawnSpecs();
            if (specs.Length == 0)
            {
                if (!SuppressMissingStartupSpawnWarning)
                    _logger.Warning("StoryFlow", "No CharacterTest startup spawn points are configured.");
                return;
            }

            // A repeated presentation notification is not another Spawn authorization.
            // Record before issuing requests: partial failure must not duplicate the accepted slots.
            if (!_requestedStartupSteps.Add((evt.GraphId, evt.BeatInstanceId, evt.StepId)))
                return;

            for (int i = 0; i < specs.Length; i++)
                if (!_characterSpawnFlow.RequestSpawn(specs[i]))
                    _logger.Warning("StoryFlow", "Startup character Spawn was rejected; a new Story recovery step is required.");
        }

        private void CompletePresentationNextFrame(StoryEvent evt, RuntimeTraceIdentity traceId)
        {
            RuntimeFrame current = _currentFrameProvider();
            var nextFrame = new RuntimeFrame(current.Value + 1);
            RuntimeCommandValidationResult result = _storyModule.CommandBuffer.Enqueue(
                StoryRuntimeCommandFactory.CompletePresentation(
                    nextFrame,
                    StoryRuntimeCommandSources.System,
                    evt.BeatInstanceId,
                    evt.StepId,
                    evt.GraphId,
                    traceId));

            if (result.Success)
            {
                _logBuffer.Clear()
                    .Append("CompletePresentation enqueued. stepId=")
                    .Append(evt.StepId)
                    .Append(", frame=")
                    .Append(nextFrame.Value);
                _logger.Info("StoryFlow", _logBuffer);
                return;
            }

            _logBuffer.Clear()
                .Append("CompletePresentation rejected. stepId=")
                .Append(evt.StepId)
                .Append(", error=")
                .Append(result.Error.Message);
            _logger.Warning("StoryFlow", _logBuffer);
        }

        private static CharacterTestCharacterSpawnSpec[] CopyStartupSpawnSpecs(
            IReadOnlyList<CharacterTestCharacterSpawnSpec> specs)
        {
            if (specs == null)
                return null;
            if (specs.Count == 0)
                return Array.Empty<CharacterTestCharacterSpawnSpec>();

            var copy = new CharacterTestCharacterSpawnSpec[specs.Count];
            for (int i = 0; i < specs.Count; i++)
                copy[i] = specs[i];
            return copy;
        }

        private static CharacterTestCharacterSpawnSpec[] CreateLegacyStartupSpawnSpecs()
        {
            return new[]
            {
                CharacterTestCharacterSpawnSpec.TemporaryPlayer,
                CharacterTestCharacterSpawnSpec.TemporaryAiOpponent,
                CharacterTestCharacterSpawnSpec.TemporaryAiOpponentNearby
            };
        }
    }
}
