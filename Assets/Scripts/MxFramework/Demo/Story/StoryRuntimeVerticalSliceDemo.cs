using System;
using System.Collections.Generic;
using MxFramework.AI;
using MxFramework.Gameplay;
using MxFramework.Resources;
using MxFramework.Runtime;
using MxFramework.Story;
using MxFramework.Story.Config;
using MxFramework.Story.GameplayBridge;
using MxFramework.Story.ResourcesBridge;
using MxFramework.Story.Runtime;
using MxFramework.Story.RuntimeAiPlannerBridge;

namespace MxFramework.Demo.Story
{
    public sealed class StoryRuntimeVerticalSliceDemo : IDisposable
    {
        public const int GraphId = 441001;
        public const int TriggerId = 441101;
        public const int DialogueBeatId = 441201;
        public const int EndBeatId = 441202;
        public const int DialogueStepId = 441301;
        public const int SignalSeenFactStepId = 441302;
        public const int EndChoiceFactStepId = 441303;
        public const int EndSignalFactStepId = 441304;
        public const int ChoiceSetId = 441401;
        public const int StabilizeChoiceId = 441501;
        public const int StabilizeEffectId = 441601;
        public const int SignalAnchorDefinitionId = 441701;
        public const int SignalAttributeId = 441801;
        public const int SignalDelta = 5;
        public const int FactSignalSeenId = 441901;
        public const int FactChoiceSelectedId = 441902;
        public const int FactSignalLevelId = 441903;
        public const int DialogueTextKey = 442001;
        public const int ChoiceTextKey = 442002;
        public const int EndTextKey = 442003;

        private readonly List<GameplayRuntimeEvent> _gameplayEvents = new List<GameplayRuntimeEvent>();
        private readonly RingLog _eventLog = new RingLog(18);
        private RuntimeReplayRecorder _recorder;
        private RuntimeHost _host;
        private StoryDirector _storyDirector;
        private StoryRuntimeModule _storyModule;
        private RuntimeCommandBuffer _gameplayCommandBuffer;
        private GameplayComponentWorld _gameplayWorld;
        private GameplayRuntimeModule _gameplayModule;
        private StoryGameplayBridgeDiagnostics _gameplayBridgeDiagnostics;
        private StoryChoiceGameplayEffectModule _effectModule;
        private StoryRuntimeAiProjectionProfile _aiProjectionProfile;
        private StoryRuntimeAiProjectionDiagnostics _aiProjectionDiagnostics;
        private AiWorldState _aiWorldState;
        private ResourcePreloadPlan _resourcePreloadPlan;
        private GameplayEntityId _signalAnchorEntityId;
        private long _nextFrame;
        private string _savedStateJson = string.Empty;
        private string _saveStatus = "No save";
        private string _replayStatus = "Replay not run";

        public StoryRuntimeVerticalSliceDemo()
        {
            Reset();
        }

        public StoryRuntimeModule StoryModule => _storyModule;
        public RuntimeFrame CurrentCommandFrame => new RuntimeFrame(_nextFrame);
        public bool HasSavedState => !string.IsNullOrEmpty(_savedStateJson);

        public void Reset()
        {
            DisposeRuntime();
            _eventLog.Clear();
            _nextFrame = 0L;
            _savedStateJson = string.Empty;
            _saveStatus = "No save";
            _replayStatus = "Replay not run";
            _aiWorldState = new AiWorldState();
            _aiProjectionDiagnostics = new StoryRuntimeAiProjectionDiagnostics();
            _aiProjectionProfile = CreateAiProjectionProfile();
            _recorder = new RuntimeReplayRecorder(new RuntimeReplayHeader(
                1,
                "story-runtime-vertical-slice",
                "story-s5-demo-config",
                "story-s5-demo-resources",
                RuntimeFrame.Zero));

            CreateRuntime();
            ProjectRuntimeAiPlannerFacts();
            AddLog("Runtime reset");
        }

        public RuntimeCommandValidationResult RaiseTrigger()
        {
            RuntimeCommand command = StoryRuntimeCommandFactory.RaiseTrigger(
                CurrentCommandFrame,
                StoryRuntimeCommandSources.Input,
                TriggerId,
                traceId: "story.demo.trigger");
            RuntimeCommandValidationResult result = _storyModule.CommandBuffer.Enqueue(command);
            if (!result.Success)
                AddLog("Trigger enqueue rejected: " + result.Error.Message);
            return result;
        }

        public bool RaiseTriggerAndTick()
        {
            RuntimeCommandValidationResult result = RaiseTrigger();
            Tick();
            return result.Success && _storyModule.LastCommandErrors.Count == 0;
        }

        public bool CompletePresentationAndTick()
        {
            if (!TryGetWaitingPresentation(out StoryBeatInstanceSnapshot waiting))
            {
                AddLog("Continue skipped: Story is not waiting for presentation");
                return false;
            }

            RuntimeCommand command = StoryRuntimeCommandFactory.CompletePresentation(
                CurrentCommandFrame,
                StoryRuntimeCommandSources.PresentationAdapter,
                waiting.BeatInstanceId,
                waiting.PendingPresentationStepId,
                waiting.GraphId,
                "story.demo.presentation.complete");
            RuntimeCommandValidationResult result = _storyModule.CommandBuffer.Enqueue(command);
            if (!result.Success)
                AddLog("Presentation enqueue rejected: " + result.Error.Message);

            Tick();
            return result.Success && _storyModule.LastCommandErrors.Count == 0;
        }

        public bool SelectFirstChoiceAndTick()
        {
            if (!TryGetFirstEnabledChoice(out StoryBeatInstanceSnapshot beat, out StoryChoiceView choice))
            {
                AddLog("Choice skipped: no enabled Story choice");
                return false;
            }

            RuntimeCommand command = StoryRuntimeCommandFactory.SelectChoice(
                CurrentCommandFrame,
                StoryRuntimeCommandSources.UiAdapter,
                beat.BeatInstanceId,
                choice.ChoiceId,
                beat.GraphId,
                "story.demo.choice");
            RuntimeCommandValidationResult result = _storyModule.CommandBuffer.Enqueue(command);
            if (!result.Success)
                AddLog("Choice enqueue rejected: " + result.Error.Message);

            Tick();
            return result.Success && _storyModule.LastCommandErrors.Count == 0 && SignalValue == SignalDelta;
        }

        public void Tick()
        {
            RuntimeFrame frame = CurrentCommandFrame;
            _host.Tick(new RuntimeTickContext(frame.Value, 0d, 0d, RuntimeTickStage.Simulation));
            DrainGameplayEvents(frame);
            ProjectRuntimeAiPlannerFacts();
            RecordReplayFrame(frame);
            _nextFrame++;
        }

        public bool Save()
        {
            RuntimeSaveStateResult<RuntimeSaveState> storySave =
                new StoryRuntimeSaveStateProvider(_storyDirector, () => CurrentFrame.Value).CaptureSaveState();
            if (!storySave.Success)
            {
                _saveStatus = "Story save failed: " + storySave.Error;
                AddLog(_saveStatus);
                return false;
            }

            RuntimeSaveStateResult<RuntimeSaveState> gameplaySave =
                new GameplayComponentWorldSaveStateProvider(_gameplayWorld).CaptureSaveState();
            if (!gameplaySave.Success)
            {
                _saveStatus = "Gameplay save failed: " + gameplaySave.Error;
                AddLog(_saveStatus);
                return false;
            }

            RuntimeSaveState combined = CombineSaveStates(storySave.Value, gameplaySave.Value);
            _savedStateJson = RuntimeSaveStateJson.SaveToJson(combined);
            _saveStatus = "Saved frame " + combined.Frame + " hash " + ComputeHash();
            AddLog(_saveStatus);
            return true;
        }

        public bool Restore()
        {
            if (string.IsNullOrEmpty(_savedStateJson))
            {
                _saveStatus = "Restore skipped: no save";
                AddLog(_saveStatus);
                return false;
            }

            RuntimeSaveStateResult<RuntimeSaveState> loaded = RuntimeSaveStateJson.LoadFromJson(_savedStateJson);
            if (!loaded.Success)
            {
                _saveStatus = "Restore load failed: " + loaded.Error;
                AddLog(_saveStatus);
                return false;
            }

            RuntimeReplaySnapshot replayBeforeRestore = _recorder.CreateSnapshot();
            RuntimeSaveState saveState = loaded.Value;
            DisposeRuntime();
            _nextFrame = saveState.Frame + 1L;
            CreateRuntime(loadStoryGraph: false, createGameplayEntity: false);

            RuntimeSaveStateResult<bool> storyRestore =
                new StoryRuntimeSaveStateProvider(_storyDirector).RestoreSaveState(saveState);
            if (!storyRestore.Success)
            {
                _saveStatus = "Story restore failed: " + storyRestore.Error;
                AddLog(_saveStatus);
                return false;
            }

            RuntimeSaveStateResult<bool> gameplayRestore =
                new GameplayComponentWorldSaveStateProvider(_gameplayWorld).RestoreSaveState(saveState);
            if (!gameplayRestore.Success)
            {
                _saveStatus = "Gameplay restore failed: " + gameplayRestore.Error;
                AddLog(_saveStatus);
                return false;
            }

            RefreshSignalAnchorEntity();
            RebindEffectTarget();
            RestoreReplayRecords(replayBeforeRestore, saveState.Frame);
            ProjectRuntimeAiPlannerFacts();
            _saveStatus = "Restored frame " + saveState.Frame + " hash " + ComputeHash();
            AddLog(_saveStatus);
            return true;
        }

        public bool RunReplaySmoke()
        {
            RuntimeReplaySnapshot snapshot = _recorder.CreateSnapshot();
            var result = new RuntimeReplayPlaybackRunner().Play(snapshot, new ReplayDriver());
            _replayStatus = result.Success
                ? "Replay ok: " + result.FramesPlayed + " frames"
                : "Replay failed: " + result.FailureMessage;
            AddLog(_replayStatus);
            return result.Success;
        }

        public StoryRuntimeVerticalSliceSnapshot CreateSnapshot()
        {
            StoryDirectorSnapshot story = _storyDirector.CreateSnapshot();
            bool waiting = TryGetWaitingPresentation(story, out StoryBeatInstanceSnapshot waitingBeat);
            StoryRuntimeVerticalSliceChoiceSnapshot[] choices = CollectChoiceSnapshots(
                story,
                out StoryRuntimeVerticalSliceChoiceSnapshot firstEnabledChoice);
            StoryGraphRuntimeStatus graphStatus = GetGraphStatus(story);
            return new StoryRuntimeVerticalSliceSnapshot(
                CurrentFrame,
                _nextFrame,
                graphStatus,
                waiting ? waitingBeat.BeatInstanceId : 0,
                waiting ? waitingBeat.PendingPresentationStepId : 0,
                firstEnabledChoice.BeatInstanceId,
                firstEnabledChoice.ChoiceId,
                ResolveDialogueText(story),
                firstEnabledChoice.Text,
                SignalValue,
                ComputeHash(),
                _saveStatus,
                _replayStatus,
                FormatAiFacts(),
                _resourcePreloadPlan != null ? _resourcePreloadPlan.GroupId : string.Empty,
                _gameplayBridgeDiagnostics.CreateSnapshot().EnqueuedCommandCount,
                choices,
                _eventLog.ToArray());
        }

        public long ComputeHash()
        {
            return ComputeHash(CurrentFrame);
        }

        private long ComputeHash(RuntimeFrame frame)
        {
            return RuntimeHashCombiner.ComputeHash(
                frame,
                new IRuntimeHashContributor[]
                {
                    new StoryRuntimeHashContributor(_storyDirector),
                    new GameplayComponentWorldHashContributor(_gameplayWorld)
                });
        }

        public void Dispose()
        {
            DisposeRuntime();
        }

        private RuntimeFrame CurrentFrame => _nextFrame <= 0L ? RuntimeFrame.Zero : new RuntimeFrame(_nextFrame - 1L);

        private int SignalValue
        {
            get
            {
                if (!_signalAnchorEntityId.IsValid ||
                    !_gameplayWorld.TryGetStore(out GameplayComponentStore<GameplayAttributeSetComponent> store) ||
                    !store.TryGet(_signalAnchorEntityId, out GameplayAttributeSetComponent attributes))
                {
                    return 0;
                }

                return attributes.GetCurrentValueOrDefault(SignalAttributeId);
            }
        }

        private void CreateRuntime(bool loadStoryGraph = true, bool createGameplayEntity = true)
        {
            _storyDirector = new StoryDirector();
            if (loadStoryGraph)
            {
                StoryGraphDefinition graph = CreateStoryGraph();
                StoryLoadGraphResult load = _storyDirector.TryLoadGraph(graph);
                if (!load.Success)
                    throw new InvalidOperationException("Story graph failed to load: " + load.Message);
            }

            _storyModule = new StoryRuntimeModule(
                _storyDirector,
                new RuntimeCommandBuffer(null, CurrentCommandFrame));
            _gameplayCommandBuffer = new RuntimeCommandBuffer(null, CurrentCommandFrame);
            _gameplayWorld = CreateGameplayWorld();
            if (createGameplayEntity)
                CreateSignalAnchorEntity();

            _gameplayBridgeDiagnostics = new StoryGameplayBridgeDiagnostics();
            _effectModule = new StoryChoiceGameplayEffectModule(
                _storyModule,
                new StoryGameplayEffectBridge(
                    _gameplayCommandBuffer,
                    _gameplayWorld,
                    diagnostics: _gameplayBridgeDiagnostics),
                CreateEffectIntent);
            _gameplayModule = new GameplayRuntimeModule(
                new GameplayWorld(),
                new GameplayAbilityRegistry(),
                _gameplayCommandBuffer,
                tickWorldAutomatically: false,
                configureDefaultPipeline: pipeline => pipeline.Add(new GameplayAttributeCommandSystem()),
                componentWorld: _gameplayWorld);

            _host = new RuntimeHost();
            _host.RegisterModule(_storyModule);
            _host.RegisterModule(_effectModule);
            _host.RegisterModule(_gameplayModule);
            _host.Initialize();
            _host.Start();
            _resourcePreloadPlan = CreateResourcePreloadPlan();
        }

        private void DisposeRuntime()
        {
            if (_host == null)
                return;

            _host.Dispose();
            _host = null;
            _storyModule = null;
            _gameplayModule = null;
            _effectModule = null;
        }

        private StoryGameplayEffectIntent CreateEffectIntent(StoryRuntimeEvent evt)
        {
            if (evt.Kind != StoryEventKind.ChoiceResolved || evt.AuxId != StabilizeChoiceId)
                return default;

            return StoryGameplayEffectIntent.AddComponentAttribute(
                StoryGameplayEntityRef.ComponentEntity(_signalAnchorEntityId),
                StoryRuntimeCommandSources.GameplayBridge,
                SignalAttributeId,
                SignalDelta,
                traceId: "story.demo.effect.signal");
        }

        private static StoryGraphDefinition CreateStoryGraph()
        {
            StoryConfigSet config = new StoryConfigSet(
                new[] { new StoryGraphConfig(GraphId, DialogueBeatId, sourcePath: "story-s5-demo") },
                new[]
                {
                    new StoryBeatConfig(DialogueBeatId, GraphId, choiceSetId: ChoiceSetId, triggerIds: new[] { TriggerId }),
                    new StoryBeatConfig(EndBeatId, GraphId, sortOrder: 10)
                },
                new[]
                {
                    new StoryStepConfig(
                        DialogueStepId,
                        GraphId,
                        DialogueBeatId,
                        StoryStepKind.Line,
                        textKey: DialogueTextKey,
                        waitPolicy: StoryPresentationWaitPolicy.WaitForCommand),
                    new StoryStepConfig(
                        SignalSeenFactStepId,
                        GraphId,
                        DialogueBeatId,
                        StoryStepKind.SetFact,
                        sortOrder: 10,
                        factNamespace: GraphId,
                        factId: FactSignalSeenId,
                        factValueKind: StoryValueKind.Bool,
                        factValueRaw: 1L),
                    new StoryStepConfig(
                        EndChoiceFactStepId,
                        GraphId,
                        EndBeatId,
                        StoryStepKind.SetFact,
                        factNamespace: GraphId,
                        factId: FactChoiceSelectedId,
                        factValueKind: StoryValueKind.Bool,
                        factValueRaw: 1L),
                    new StoryStepConfig(
                        EndSignalFactStepId,
                        GraphId,
                        EndBeatId,
                        StoryStepKind.SetFact,
                        sortOrder: 10,
                        factNamespace: GraphId,
                        factId: FactSignalLevelId,
                        factValueKind: StoryValueKind.Int32,
                        factValueRaw: SignalDelta,
                        textKey: EndTextKey)
                },
                Array.Empty<StoryBranchConfig>(),
                new[]
                {
                    new StoryChoiceConfig(
                        StabilizeChoiceId,
                        GraphId,
                        DialogueBeatId,
                        ChoiceTextKey,
                        EndBeatId,
                        effectIds: new[] { StabilizeEffectId })
                },
                new[]
                {
                    new StoryFactConfig(FactSignalSeenId, GraphId, StoryValueKind.Bool),
                    new StoryFactConfig(FactChoiceSelectedId, GraphId, StoryValueKind.Bool),
                    new StoryFactConfig(FactSignalLevelId, GraphId, StoryValueKind.Int32)
                });

            var references = new StoryConfigReferenceIndex()
                .AddTextKey(DialogueTextKey)
                .AddTextKey(ChoiceTextKey)
                .AddTextKey(EndTextKey);
            StoryGraphConfigMappingResult result = StoryGraphConfigMapper.Map(config, GraphId, references);
            if (!result.IsValid)
                throw new InvalidOperationException("Story demo config is invalid: " + result.DiagnosticCount + " diagnostics.");

            return result.Definition;
        }

        private static GameplayComponentWorld CreateGameplayWorld()
        {
            var world = new GameplayComponentWorld();
            GameplayCoreComponentSchemaDescriptors.RegisterDiagnostics(world.Schemas);
            GameplayCoreComponentSchemaDescriptors.RegisterRuntimeHash(world.Schemas);
            GameplayCoreComponentSchemaDescriptors.RegisterSaveState(world.Schemas);
            GameplayAttributeComponentSchemaDescriptors.RegisterDiagnostics(world.Schemas);
            GameplayAttributeComponentSchemaDescriptors.RegisterRuntimeHash(world.Schemas);
            GameplayAttributeComponentSchemaDescriptors.RegisterSaveState(world.Schemas);
            return world;
        }

        private void CreateSignalAnchorEntity()
        {
            _signalAnchorEntityId = _gameplayWorld.CreateEntity();
            _gameplayWorld.GetOrCreateStore<GameplayIdentityComponent>().Set(
                _signalAnchorEntityId,
                new GameplayIdentityComponent(SignalAnchorDefinitionId));
            _gameplayWorld.GetOrCreateStore<GameplayLifecycleComponent>().Set(
                _signalAnchorEntityId,
                GameplayLifecycleComponent.Alive);
            _gameplayWorld.GetOrCreateStore<GameplayAttributeSetComponent>().Set(
                _signalAnchorEntityId,
                new GameplayAttributeSetComponent(new GameplayAttributeValue(SignalAttributeId, 0, 0)));
        }

        private void RefreshSignalAnchorEntity()
        {
            _signalAnchorEntityId = default;
            if (!_gameplayWorld.TryGetStore(out GameplayComponentStore<GameplayIdentityComponent> identities))
                return;

            GameplayEntityId[] entities = _gameplayWorld.CreateEntitySnapshot();
            for (int i = 0; i < entities.Length; i++)
            {
                if (identities.TryGet(entities[i], out GameplayIdentityComponent identity) &&
                    identity.DefinitionId == SignalAnchorDefinitionId)
                {
                    _signalAnchorEntityId = entities[i];
                    return;
                }
            }
        }

        private void RebindEffectTarget()
        {
            _effectModule.SetIntentFactory(CreateEffectIntent);
        }

        private static StoryRuntimeAiProjectionProfile CreateAiProjectionProfile()
        {
            return new StoryRuntimeAiProjectionProfile(new[]
            {
                new StoryRuntimeAiFactMapping(new StoryFactKey(GraphId, FactSignalSeenId), new AiFactKey("story.signal.seen")),
                new StoryRuntimeAiFactMapping(new StoryFactKey(GraphId, FactChoiceSelectedId), new AiFactKey("story.choice.selected")),
                new StoryRuntimeAiFactMapping(new StoryFactKey(GraphId, FactSignalLevelId), new AiFactKey("story.signal.level"))
            });
        }

        private void ProjectRuntimeAiPlannerFacts()
        {
            StoryRuntimeAiWorldStateProjector.Project(
                _storyDirector.CreateSnapshot(),
                _aiWorldState,
                _aiProjectionProfile,
                _aiProjectionDiagnostics);
        }

        private ResourcePreloadPlan CreateResourcePreloadPlan()
        {
            StoryResourcePreloadPlanResult result = StoryResourcePreloadPlanBuilder.Build(
                new StoryResourcePreloadMetadata(
                    "story.runtime.vertical_slice",
                    new[]
                    {
                        new StoryResourceKeyMetadata("story.runtime.vertical_slice.ui", ResourceTypeIds.VisualTreeAsset),
                        new StoryResourceKeyMetadata("story.runtime.vertical_slice.style", ResourceTypeIds.StyleSheet)
                    },
                    new[] { "story.runtime.vertical_slice" },
                    failFast: true,
                    maxConcurrentLoads: 2));

            if (!result.Success)
                throw new InvalidOperationException("Story demo resource preload plan failed: " + result.Diagnostics.Count);

            return result.Plan;
        }

        private bool TryGetWaitingPresentation(out StoryBeatInstanceSnapshot waiting)
        {
            return TryGetWaitingPresentation(_storyDirector.CreateSnapshot(), out waiting);
        }

        private static bool TryGetWaitingPresentation(
            StoryDirectorSnapshot snapshot,
            out StoryBeatInstanceSnapshot waiting)
        {
            for (int i = 0; i < snapshot.ActiveBeatInstances.Count; i++)
            {
                StoryBeatInstanceSnapshot beat = snapshot.ActiveBeatInstances[i];
                if (beat.IsWaitingForPresentation)
                {
                    waiting = beat;
                    return true;
                }
            }

            waiting = null;
            return false;
        }

        private bool TryGetFirstEnabledChoice(out StoryBeatInstanceSnapshot beat, out StoryChoiceView choice)
        {
            return TryGetFirstEnabledChoice(_storyDirector.CreateSnapshot(), out beat, out choice);
        }

        private bool TryGetFirstEnabledChoice(
            StoryDirectorSnapshot snapshot,
            out StoryBeatInstanceSnapshot beat,
            out StoryChoiceView choice)
        {
            for (int i = 0; i < snapshot.ActiveBeatInstances.Count; i++)
            {
                StoryBeatInstanceSnapshot candidate = snapshot.ActiveBeatInstances[i];
                if (!candidate.IsAwaitingChoice)
                    continue;

                StoryChoiceView[] choices = GetChoiceViews(candidate);
                for (int choiceIndex = 0; choiceIndex < choices.Length; choiceIndex++)
                {
                    if (!choices[choiceIndex].Enabled)
                        continue;

                    beat = candidate;
                    choice = choices[choiceIndex];
                    return true;
                }
            }

            beat = null;
            choice = default;
            return false;
        }

        private StoryRuntimeVerticalSliceChoiceSnapshot[] CollectChoiceSnapshots(
            StoryDirectorSnapshot snapshot,
            out StoryRuntimeVerticalSliceChoiceSnapshot firstEnabledChoice)
        {
            var results = new List<StoryRuntimeVerticalSliceChoiceSnapshot>();
            bool foundEnabled = false;
            firstEnabledChoice = default;

            for (int i = 0; i < snapshot.ActiveBeatInstances.Count; i++)
            {
                StoryBeatInstanceSnapshot beat = snapshot.ActiveBeatInstances[i];
                if (!beat.IsAwaitingChoice)
                    continue;

                StoryChoiceView[] choices = GetChoiceViews(beat);
                for (int choiceIndex = 0; choiceIndex < choices.Length; choiceIndex++)
                {
                    StoryChoiceView choice = choices[choiceIndex];
                    var choiceSnapshot = new StoryRuntimeVerticalSliceChoiceSnapshot(
                        beat.GraphId,
                        beat.BeatInstanceId,
                        choice.ChoiceId,
                        choice.LabelTextKey,
                        ResolveText(choice.LabelTextKey),
                        choice.Enabled);
                    results.Add(choiceSnapshot);

                    if (!foundEnabled && choice.Enabled)
                    {
                        firstEnabledChoice = choiceSnapshot;
                        foundEnabled = true;
                    }
                }
            }

            return results.ToArray();
        }

        private StoryChoiceView[] GetChoiceViews(StoryBeatInstanceSnapshot beat)
        {
            Span<StoryChoiceView> initial = stackalloc StoryChoiceView[8];
            int required = _storyDirector.GetChoices(beat.BeatInstanceId, beat.AwaitingChoiceSetId, initial);
            if (required <= 0)
                return Array.Empty<StoryChoiceView>();

            if (required <= initial.Length)
            {
                var choices = new StoryChoiceView[required];
                initial.Slice(0, required).CopyTo(choices);
                return choices;
            }

            var expanded = new StoryChoiceView[required];
            int expandedRequired = _storyDirector.GetChoices(beat.BeatInstanceId, beat.AwaitingChoiceSetId, expanded);
            int count = Math.Min(expandedRequired, expanded.Length);
            if (count == expanded.Length)
                return expanded;

            var trimmed = new StoryChoiceView[count];
            Array.Copy(expanded, trimmed, count);
            return trimmed;
        }

        private static StoryGraphRuntimeStatus GetGraphStatus(StoryDirectorSnapshot snapshot)
        {
            for (int i = 0; i < snapshot.Graphs.Count; i++)
            {
                if (snapshot.Graphs[i].GraphId == GraphId)
                    return snapshot.Graphs[i].Status;
            }

            return StoryGraphRuntimeStatus.Loaded;
        }

        private string ResolveDialogueText(StoryDirectorSnapshot snapshot)
        {
            if (TryGetWaitingPresentation(snapshot, out _))
                return ResolveText(DialogueTextKey);

            if (GetGraphStatus(snapshot) == StoryGraphRuntimeStatus.Completed)
                return ResolveText(EndTextKey);

            return "Press Trigger to raise the Story event.";
        }

        private static string ResolveText(int textKey)
        {
            switch (textKey)
            {
                case DialogueTextKey:
                    return "A runtime signal is waiting at the Story boundary.";
                case ChoiceTextKey:
                    return "Stabilize signal";
                case EndTextKey:
                    return "Signal stabilized through Gameplay command.";
                default:
                    return string.Empty;
            }
        }

        private string FormatAiFacts()
        {
            IReadOnlyDictionary<AiFactKey, object> snapshot = _aiWorldState.Snapshot();
            if (snapshot.Count == 0)
                return "No projected facts";

            var entries = new List<string>(snapshot.Count);
            foreach (KeyValuePair<AiFactKey, object> fact in snapshot)
                entries.Add(fact.Key.Value + "=" + fact.Value);

            entries.Sort(StringComparer.Ordinal);
            return string.Join(", ", entries.ToArray());
        }

        private void DrainGameplayEvents(RuntimeFrame frame)
        {
            _gameplayEvents.Clear();
            _gameplayModule.DrainEvents(frame, _gameplayEvents);
            for (int i = 0; i < _gameplayEvents.Count; i++)
                AddLog(FormatGameplayEvent(_gameplayEvents[i]));
        }

        private void RecordReplayFrame(RuntimeFrame frame)
        {
            _recorder.RecordFrame(
                frame,
                _storyModule.LastDrainedCommands,
                ComputeHash(frame),
                CreateDiagnosticsSummary());
        }

        private void RestoreReplayRecords(RuntimeReplaySnapshot snapshot, long maxFrame)
        {
            _recorder = new RuntimeReplayRecorder(snapshot.Header);
            for (int i = 0; i < snapshot.Records.Count; i++)
            {
                RuntimeReplayFrameRecord record = snapshot.Records[i];
                if (record.Frame.Value <= maxFrame)
                    _recorder.RecordFrame(record.Frame, record.Commands, record.ResultHash, record.DiagnosticsSummary);
            }
        }

        private string CreateDiagnosticsSummary()
        {
            StoryDirectorSnapshot story = _storyDirector.CreateSnapshot();
            return "storyBeats=" + story.ActiveBeatInstances.Count
                + ";signal=" + SignalValue
                + ";events=" + _gameplayBridgeDiagnostics.CreateSnapshot().EnqueuedCommandCount;
        }

        private static string FormatGameplayEvent(GameplayRuntimeEvent evt)
        {
            if (evt.Type == GameplayRuntimeEventType.ComponentAttributeChanged)
            {
                return "Gameplay " + evt.Reason
                    + " attr=" + evt.AttributeId
                    + " " + evt.OldAttributeValue
                    + "->" + evt.NewAttributeValue;
            }

            return "Gameplay " + evt.Type + " " + evt.Reason;
        }

        private void AddLog(string message)
        {
            _eventLog.Add(message ?? string.Empty);
        }

        private RuntimeSaveState CombineSaveStates(RuntimeSaveState story, RuntimeSaveState gameplay)
        {
            var modules = new List<RuntimeModuleSaveState>(story.ModuleStates.Count + gameplay.ModuleStates.Count);
            modules.AddRange(story.ModuleStates);
            modules.AddRange(gameplay.ModuleStates);
            return new RuntimeSaveState(
                RuntimeSaveState.CurrentSchemaVersion,
                DateTime.UtcNow,
                "story-s5-demo",
                "story-s5-demo-config",
                "story-s5-demo-resources",
                CurrentFrame.Value,
                null,
                null,
                modules,
                new Dictionary<string, string>
                {
                    { "scene", "Assets/Scenes/StoryRuntimeVerticalSlice.unity" },
                    { "issue", "441" }
                });
        }

        private sealed class ReplayDriver : IRuntimeReplayFrameDriver
        {
            private StoryRuntimeVerticalSliceDemo _demo;

            public void Reset(RuntimeReplayHeader header)
            {
                _demo?.Dispose();
                _demo = new StoryRuntimeVerticalSliceDemo();
            }

            public RuntimeReplayPlaybackFrameResult RunFrame(RuntimeReplayFrameRecord record)
            {
                for (int i = 0; i < record.Commands.Count; i++)
                    _demo.EnqueueReplayCommand(record.Commands[i]);

                _demo.Tick();
                return new RuntimeReplayPlaybackFrameResult(
                    record.Frame,
                    _demo.ComputeHash(),
                    _demo.CreateDiagnosticsSummary(),
                    _demo.StoryModule.LastCommandErrors);
            }
        }

        private void EnqueueReplayCommand(RuntimeCommand command)
        {
            if (StoryRuntimeCommandIds.IsStoryCommandId(command.CommandId))
            {
                _storyModule.CommandBuffer.Enqueue(command);
                return;
            }

            _gameplayCommandBuffer.Enqueue(command);
        }

        private sealed class StoryChoiceGameplayEffectModule : RuntimeModule
        {
            private readonly StoryRuntimeModule _storyModule;
            private readonly StoryGameplayEffectBridge _effectBridge;
            private readonly List<StoryRuntimeEvent> _events = new List<StoryRuntimeEvent>();
            private Func<StoryRuntimeEvent, StoryGameplayEffectIntent> _intentFactory;

            public StoryChoiceGameplayEffectModule(
                StoryRuntimeModule storyModule,
                StoryGameplayEffectBridge effectBridge,
                Func<StoryRuntimeEvent, StoryGameplayEffectIntent> intentFactory)
                : base("mxframework.demo.story.runtime_effect_bridge", RuntimeTickStage.Simulation, 0)
            {
                _storyModule = storyModule ?? throw new ArgumentNullException(nameof(storyModule));
                _effectBridge = effectBridge ?? throw new ArgumentNullException(nameof(effectBridge));
                _intentFactory = intentFactory ?? throw new ArgumentNullException(nameof(intentFactory));
            }

            public void SetIntentFactory(Func<StoryRuntimeEvent, StoryGameplayEffectIntent> intentFactory)
            {
                _intentFactory = intentFactory ?? throw new ArgumentNullException(nameof(intentFactory));
            }

            public override void Tick(RuntimeTickContext context)
            {
                _events.Clear();
                _storyModule.Events.Drain(new RuntimeFrame(context.FrameIndex), _events);
                for (int i = 0; i < _events.Count; i++)
                {
                    StoryRuntimeEvent evt = _events[i];
                    StoryGameplayEffectIntent intent = _intentFactory(evt);
                    if (intent.CommandId != 0 || intent.Kind != StoryGameplayEffectIntentKind.RuntimeCommand)
                        _effectBridge.EnqueueGameplayEffect(intent, new RuntimeFrame(context.FrameIndex));
                }
            }
        }

        private sealed class RingLog
        {
            private readonly string[] _entries;
            private int _next;
            private int _count;

            public RingLog(int capacity)
            {
                _entries = new string[Math.Max(1, capacity)];
            }

            public void Add(string message)
            {
                _entries[_next] = message ?? string.Empty;
                _next = (_next + 1) % _entries.Length;
                if (_count < _entries.Length)
                    _count++;
            }

            public void Clear()
            {
                Array.Clear(_entries, 0, _entries.Length);
                _next = 0;
                _count = 0;
            }

            public string[] ToArray()
            {
                var copy = new string[_count];
                for (int i = 0; i < _count; i++)
                {
                    int index = (_next - _count + i + _entries.Length) % _entries.Length;
                    copy[i] = _entries[index];
                }

                return copy;
            }
        }
    }

    public readonly struct StoryRuntimeVerticalSliceChoiceSnapshot
    {
        public StoryRuntimeVerticalSliceChoiceSnapshot(
            int graphId,
            int beatInstanceId,
            int choiceId,
            int labelTextKey,
            string text,
            bool enabled)
        {
            GraphId = graphId;
            BeatInstanceId = beatInstanceId;
            ChoiceId = choiceId;
            LabelTextKey = labelTextKey;
            Text = text ?? string.Empty;
            Enabled = enabled;
        }

        public int GraphId { get; }
        public int BeatInstanceId { get; }
        public int ChoiceId { get; }
        public int LabelTextKey { get; }
        public string Text { get; }
        public bool Enabled { get; }
    }

    public readonly struct StoryRuntimeVerticalSliceSnapshot
    {
        public StoryRuntimeVerticalSliceSnapshot(
            RuntimeFrame frame,
            long nextFrame,
            StoryGraphRuntimeStatus graphStatus,
            int waitingBeatInstanceId,
            int waitingStepId,
            int choiceBeatInstanceId,
            int choiceId,
            string dialogueText,
            string choiceText,
            int signalValue,
            long hash,
            string saveStatus,
            string replayStatus,
            string aiFacts,
            string preloadGroupId,
            int gameplayCommandCount,
            string[] eventLog)
            : this(
                frame,
                nextFrame,
                graphStatus,
                waitingBeatInstanceId,
                waitingStepId,
                choiceBeatInstanceId,
                choiceId,
                dialogueText,
                choiceText,
                signalValue,
                hash,
                saveStatus,
                replayStatus,
                aiFacts,
                preloadGroupId,
                gameplayCommandCount,
                CreateLegacyChoices(graphStatus, choiceBeatInstanceId, choiceId, choiceText),
                eventLog)
        {
        }

        public StoryRuntimeVerticalSliceSnapshot(
            RuntimeFrame frame,
            long nextFrame,
            StoryGraphRuntimeStatus graphStatus,
            int waitingBeatInstanceId,
            int waitingStepId,
            int choiceBeatInstanceId,
            int choiceId,
            string dialogueText,
            string choiceText,
            int signalValue,
            long hash,
            string saveStatus,
            string replayStatus,
            string aiFacts,
            string preloadGroupId,
            int gameplayCommandCount,
            IReadOnlyList<StoryRuntimeVerticalSliceChoiceSnapshot> choices,
            string[] eventLog)
        {
            Frame = frame;
            NextFrame = nextFrame;
            GraphStatus = graphStatus;
            WaitingBeatInstanceId = waitingBeatInstanceId;
            WaitingStepId = waitingStepId;
            ChoiceBeatInstanceId = choiceBeatInstanceId;
            ChoiceId = choiceId;
            DialogueText = dialogueText ?? string.Empty;
            ChoiceText = choiceText ?? string.Empty;
            SignalValue = signalValue;
            Hash = hash;
            SaveStatus = saveStatus ?? string.Empty;
            ReplayStatus = replayStatus ?? string.Empty;
            AiFacts = aiFacts ?? string.Empty;
            PreloadGroupId = preloadGroupId ?? string.Empty;
            GameplayCommandCount = gameplayCommandCount;
            Choices = choices ?? Array.Empty<StoryRuntimeVerticalSliceChoiceSnapshot>();
            EventLog = eventLog ?? Array.Empty<string>();
        }

        public RuntimeFrame Frame { get; }
        public long NextFrame { get; }
        public StoryGraphRuntimeStatus GraphStatus { get; }
        public int WaitingBeatInstanceId { get; }
        public int WaitingStepId { get; }
        public int ChoiceBeatInstanceId { get; }
        public int ChoiceId { get; }
        public string DialogueText { get; }
        public string ChoiceText { get; }
        public int SignalValue { get; }
        public long Hash { get; }
        public string SaveStatus { get; }
        public string ReplayStatus { get; }
        public string AiFacts { get; }
        public string PreloadGroupId { get; }
        public int GameplayCommandCount { get; }
        public IReadOnlyList<StoryRuntimeVerticalSliceChoiceSnapshot> Choices { get; }
        public IReadOnlyList<string> EventLog { get; }
        public bool IsWaitingForPresentation => WaitingBeatInstanceId > 0 && WaitingStepId > 0;
        public bool HasChoice => ChoiceBeatInstanceId > 0 && ChoiceId > 0;

        private static IReadOnlyList<StoryRuntimeVerticalSliceChoiceSnapshot> CreateLegacyChoices(
            StoryGraphRuntimeStatus graphStatus,
            int choiceBeatInstanceId,
            int choiceId,
            string choiceText)
        {
            if (choiceBeatInstanceId <= 0 || choiceId <= 0)
                return Array.Empty<StoryRuntimeVerticalSliceChoiceSnapshot>();

            return new[]
            {
                new StoryRuntimeVerticalSliceChoiceSnapshot(
                    StoryRuntimeVerticalSliceDemo.GraphId,
                    choiceBeatInstanceId,
                    choiceId,
                    0,
                    choiceText,
                    enabled: graphStatus != StoryGraphRuntimeStatus.Completed)
            };
        }
    }
}
