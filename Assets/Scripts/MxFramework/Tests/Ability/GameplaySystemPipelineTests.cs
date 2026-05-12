using System;
using System.Collections.Generic;
using MxFramework.Gameplay;
using MxFramework.Runtime;
using NUnit.Framework;

namespace MxFramework.Tests.Ability
{
    public sealed class GameplaySystemPipelineTests
    {
        [Test]
        public void Tick_RunsSystemsByPhasePriorityAndRegistrationOrder()
        {
            var order = new List<string>();
            var pipeline = new GameplaySystemPipeline();
            pipeline.Add(new RecordingSystem("simulation-late", GameplaySystemPhase.Simulation, 20, order));
            pipeline.Add(new RecordingSystem("command-a", GameplaySystemPhase.Command, 10, order));
            pipeline.Add(new RecordingSystem("pre", GameplaySystemPhase.PreCommand, 0, order));
            pipeline.Add(new RecordingSystem("command-b", GameplaySystemPhase.Command, 10, order));
            pipeline.Add(new RecordingSystem("command-early", GameplaySystemPhase.Command, -10, order));

            pipeline.Tick(CreateContext());

            CollectionAssert.AreEqual(
                new[] { "pre", "command-early", "command-a", "command-b", "simulation-late" },
                order);
        }

        [Test]
        public void Tick_SkipsDisabledSystemsAndSnapshotCountsEnabledSystems()
        {
            var order = new List<string>();
            var pipeline = new GameplaySystemPipeline();
            pipeline.Add(new RecordingSystem("enabled", GameplaySystemPhase.Command, 0, order));
            pipeline.Add(new RecordingSystem("disabled", GameplaySystemPhase.Command, 1, order, isEnabled: false));

            GameplaySystemPipelineSnapshot snapshot = pipeline.CreateSnapshot();
            pipeline.Tick(CreateContext());

            Assert.AreEqual(2, snapshot.SystemCount);
            Assert.AreEqual(1, snapshot.EnabledSystemCount);
            CollectionAssert.AreEqual(new[] { "enabled" }, order);
        }

        [Test]
        public void Add_RejectsNullEmptyAndDuplicateSystems()
        {
            var pipeline = new GameplaySystemPipeline();
            var order = new List<string>();

            Assert.Throws<ArgumentNullException>(() => pipeline.Add(null));
            Assert.Throws<ArgumentException>(() => pipeline.Add(new RecordingSystem(string.Empty, GameplaySystemPhase.Command, 0, order)));

            pipeline.Add(new RecordingSystem("system", GameplaySystemPhase.Command, 0, order));

            Assert.IsTrue(pipeline.Contains("system"));
            Assert.Throws<InvalidOperationException>(() => pipeline.Add(new RecordingSystem("system", GameplaySystemPhase.Command, 1, order)));
            Assert.IsTrue(pipeline.Remove("system"));
            Assert.IsFalse(pipeline.Contains("system"));
            Assert.IsFalse(pipeline.Remove("system"));
        }

        [Test]
        public void Tick_WrapsSystemExceptionWithSystemMetadata()
        {
            var pipeline = new GameplaySystemPipeline();
            pipeline.Add(new ThrowingSystem("throwing", GameplaySystemPhase.Resolution));

            GameplaySystemPipelineException ex = Assert.Throws<GameplaySystemPipelineException>(() => pipeline.Tick(CreateContext()));

            Assert.AreEqual("throwing", ex.SystemId);
            Assert.AreEqual(GameplaySystemPhase.Resolution, ex.Phase);
            Assert.IsInstanceOf<InvalidOperationException>(ex.InnerException);
        }

        [Test]
        public void Context_RejectsMismatchedEventsAndComponentWorldEvents()
        {
            var componentWorld = new GameplayComponentWorld();

            Assert.Throws<ArgumentException>(() => new GameplaySystemContext(
                RuntimeFrame.Zero,
                0d,
                0d,
                new GameplayWorld(),
                Array.Empty<RuntimeCommand>(),
                new RuntimeEventQueue<GameplayRuntimeEvent>(),
                componentWorld: componentWorld));
        }

        [Test]
        public void RuntimeModule_RunsPipelineWithDrainedCommandsWithoutExposingCommandBuffer()
        {
            var world = new GameplayWorld();
            var buffer = new RuntimeCommandBuffer();
            var observed = new List<RuntimeCommand>();
            var pipeline = new GameplaySystemPipeline();
            pipeline.Add(new CommandRecordingSystem("commands", observed));
            var module = new GameplayRuntimeModule(
                world,
                new GameplayAbilityRegistry(),
                buffer,
                tickWorldAutomatically: false,
                systemPipeline: pipeline);

            RuntimeCommand command = GameplayRuntimeCommandFactory.DespawnEntity(new RuntimeFrame(3), 42, traceId: "missing");
            buffer.Enqueue(command);

            module.Tick(new RuntimeTickContext(3, 0.25d, 1.5d, RuntimeTickStage.Simulation));

            Assert.AreEqual(1, observed.Count);
            Assert.AreEqual(command.CommandId, observed[0].CommandId);
            Assert.AreEqual(command.TargetId, observed[0].TargetId);
            Assert.AreEqual(new RuntimeFrame(4), buffer.CurrentFrame);
        }

        [Test]
        public void RuntimeModule_RunsPipelinePreCommandBeforePipelineCommandSystems()
        {
            var world = new GameplayWorld();
            var buffer = new RuntimeCommandBuffer();
            var order = new List<string>();
            var pipeline = new GameplaySystemPipeline();
            pipeline.Add(new RecordingSystem("command", GameplaySystemPhase.Command, 0, order));
            pipeline.Add(new RecordingSystem("pre", GameplaySystemPhase.PreCommand, 0, order));
            var module = new GameplayRuntimeModule(
                world,
                new GameplayAbilityRegistry(),
                buffer,
                tickWorldAutomatically: false,
                systemPipeline: pipeline);

            buffer.Enqueue(new RuntimeCommand(RuntimeFrame.Zero, sourceId: 0, commandId: 42, targetId: 0));

            module.Tick(new RuntimeTickContext(0, 0d, 0d, RuntimeTickStage.Simulation));

            CollectionAssert.AreEqual(new[] { "pre", "command" }, order);
        }

        private static GameplaySystemContext CreateContext()
        {
            return new GameplaySystemContext(
                RuntimeFrame.Zero,
                0d,
                0d,
                new GameplayWorld(),
                Array.Empty<RuntimeCommand>(),
                new RuntimeEventQueue<GameplayRuntimeEvent>());
        }

        private sealed class RecordingSystem : IGameplaySystem
        {
            private readonly List<string> _order;

            public RecordingSystem(
                string systemId,
                GameplaySystemPhase phase,
                int priority,
                List<string> order,
                bool isEnabled = true)
            {
                SystemId = systemId;
                Phase = phase;
                Priority = priority;
                _order = order;
                IsEnabled = isEnabled;
            }

            public string SystemId { get; }
            public GameplaySystemPhase Phase { get; }
            public int Priority { get; }
            public bool IsEnabled { get; }

            public void Tick(GameplaySystemContext context)
            {
                _order.Add(SystemId);
            }
        }

        private sealed class CommandRecordingSystem : IGameplaySystem
        {
            private readonly List<RuntimeCommand> _commands;

            public CommandRecordingSystem(string systemId, List<RuntimeCommand> commands)
            {
                SystemId = systemId;
                _commands = commands;
            }

            public string SystemId { get; }
            public GameplaySystemPhase Phase => GameplaySystemPhase.Command;
            public int Priority => 0;
            public bool IsEnabled => true;

            public void Tick(GameplaySystemContext context)
            {
                for (int i = 0; i < context.Commands.Count; i++)
                    _commands.Add(context.Commands[i]);
            }
        }

        private sealed class ThrowingSystem : IGameplaySystem
        {
            public ThrowingSystem(string systemId, GameplaySystemPhase phase)
            {
                SystemId = systemId;
                Phase = phase;
            }

            public string SystemId { get; }
            public GameplaySystemPhase Phase { get; }
            public int Priority => 0;
            public bool IsEnabled => true;

            public void Tick(GameplaySystemContext context)
            {
                throw new InvalidOperationException("boom");
            }
        }
    }
}
