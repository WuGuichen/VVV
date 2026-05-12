using System;
using System.Collections.Generic;
using MxFramework.Core.Collections;
using MxFramework.Runtime;

namespace MxFramework.Gameplay
{
    public sealed class GameplayRuntimeModule : RuntimeModule
    {
        public const string DefaultModuleId = "mxframework.gameplay.runtime";
        public const int DefaultAbilityResultCapacity = 64;

        private readonly List<RuntimeCommand> _drainedCommands = new List<RuntimeCommand>();
        private readonly GameplayCommandExecutionState _commandState = new GameplayCommandExecutionState();
        private readonly RingBuffer<GameplayAbilityRuntimeResult> _abilityResults;
        private readonly List<GameplayAbilityRuntimeResult> _abilityResultsView = new List<GameplayAbilityRuntimeResult>();
        private readonly RuntimeEventQueue<GameplayRuntimeEvent> _events;

        public GameplayRuntimeModule(
            GameplayWorld world,
            GameplayAbilityRegistry abilityRegistry,
            RuntimeCommandBuffer commandBuffer,
            bool tickWorldAutomatically = true,
            string moduleId = DefaultModuleId,
            RuntimeTickStage tickStage = RuntimeTickStage.Simulation,
            int priority = 100,
            int abilityResultCapacity = DefaultAbilityResultCapacity,
            GameplaySystemPipeline systemPipeline = null,
            Action<GameplaySystemPipeline> configureDefaultPipeline = null,
            GameplayComponentWorld componentWorld = null)
            : base(moduleId, tickStage, priority)
        {
            if (systemPipeline != null && configureDefaultPipeline != null)
            {
                throw new ArgumentException(
                    "Configure default pipeline cannot be used when an explicit gameplay system pipeline is provided.",
                    nameof(configureDefaultPipeline));
            }

            World = world ?? throw new ArgumentNullException(nameof(world));
            AbilityRegistry = abilityRegistry ?? throw new ArgumentNullException(nameof(abilityRegistry));
            CommandBuffer = commandBuffer ?? throw new ArgumentNullException(nameof(commandBuffer));
            ComponentWorld = componentWorld ?? new GameplayComponentWorld();
            _events = ComponentWorld.Events;
            TickWorldAutomatically = tickWorldAutomatically;
            _abilityResults = new RingBuffer<GameplayAbilityRuntimeResult>(abilityResultCapacity);
            SystemPipeline = systemPipeline ?? CreateConfiguredDefaultPipeline(configureDefaultPipeline);
        }

        public GameplayWorld World { get; }
        public GameplayAbilityRegistry AbilityRegistry { get; }
        public RuntimeCommandBuffer CommandBuffer { get; }
        public GameplayComponentWorld ComponentWorld { get; }
        public GameplaySystemPipeline SystemPipeline { get; }
        public bool TickWorldAutomatically { get; }
        public int AbilityResultCapacity => _abilityResults.Capacity;
        public RuntimeEventQueue<GameplayRuntimeEvent> Events => _events;
        public IReadOnlyList<GameplayAbilityRuntimeResult> AbilityResults => _abilityResultsView;

        public override void Tick(RuntimeTickContext context)
        {
            RuntimeFrame frame = new RuntimeFrame(context.FrameIndex);
            DrainCommands(frame, context);

            if (TickWorldAutomatically)
            {
                World.Tick(context.DeltaTime);
                EnqueueEvent(new GameplayRuntimeEvent(
                    frame,
                    GameplayRuntimeEventType.WorldTicked,
                    commandId: 0,
                    casterEntityId: 0,
                    abilityId: 0,
                    targetEntityId: 0,
                    failureCode: GameplayAbilityRuntimeFailureCode.None,
                    reason: string.Empty,
                    traceId: string.Empty));
            }
        }

        public int DrainEvents(RuntimeFrame frame, List<GameplayRuntimeEvent> output)
        {
            return _events.Drain(frame, output);
        }

        public int CopyAbilityResults(List<GameplayAbilityRuntimeResult> output)
        {
            if (output == null)
            {
                throw new ArgumentNullException(nameof(output));
            }

            int countBefore = output.Count;
            _abilityResults.CopyTo(output);
            return output.Count - countBefore;
        }

        public void ClearAbilityResults()
        {
            _abilityResults.Clear();
            _abilityResultsView.Clear();
        }

        private void DrainCommands(RuntimeFrame frame, RuntimeTickContext tickContext)
        {
            _drainedCommands.Clear();
            _commandState.Clear();
            IReadOnlyList<RuntimeCommand> commands = CommandBuffer.DrainForFrame(frame);
            for (int i = 0; i < commands.Count; i++)
            {
                _drainedCommands.Add(commands[i]);
            }

            RunSystemPipeline(frame, tickContext);
            _drainedCommands.Clear();
            _commandState.Clear();
        }

        private void RunSystemPipeline(RuntimeFrame frame, RuntimeTickContext tickContext)
        {
            if (SystemPipeline == null)
                return;

            var context = new GameplaySystemContext(
                frame,
                tickContext.DeltaTime,
                tickContext.ElapsedTime,
                World,
                _drainedCommands,
                _events,
                _commandState,
                ComponentWorld);
            SystemPipeline.Tick(context);
        }

        private void RecordAbilityResult(GameplayAbilityRuntimeResult result)
        {
            _abilityResults.Add(result);
            RefreshAbilityResultsView();
        }

        private void EnqueueEvent(in GameplayRuntimeEvent evt)
        {
            _events.Enqueue(evt.Frame, evt);
        }

        private void RefreshAbilityResultsView()
        {
            _abilityResultsView.Clear();
            _abilityResults.CopyTo(_abilityResultsView);
        }

        public static GameplaySystemPipeline CreateDefaultSystemPipeline(
            GameplayAbilityRegistry abilityRegistry,
            Action<GameplayAbilityRuntimeResult> resultSink = null)
        {
            if (abilityRegistry == null)
                throw new ArgumentNullException(nameof(abilityRegistry));

            var pipeline = new GameplaySystemPipeline();
            pipeline.Add(new GameplayAbilityCommandSystem(abilityRegistry, resultSink));
            pipeline.Add(new GameplayEntityLifecycleCommandSystem());
            pipeline.Add(new GameplayComponentEntityCommandSystem());
            pipeline.Add(new GameplayUnsupportedCommandSystem());
            return pipeline;
        }

        private GameplaySystemPipeline CreateConfiguredDefaultPipeline(Action<GameplaySystemPipeline> configureDefaultPipeline)
        {
            GameplaySystemPipeline pipeline = CreateDefaultSystemPipeline(AbilityRegistry, RecordAbilityResult);
            configureDefaultPipeline?.Invoke(pipeline);
            return pipeline;
        }
    }
}
