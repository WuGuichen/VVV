using System;
using System.Collections.Generic;
using MxFramework.Runtime;

namespace MxFramework.Gameplay
{
    public readonly struct GameplaySystemContext
    {
        public GameplaySystemContext(
            RuntimeFrame frame,
            double deltaTime,
            double elapsedTime,
            GameplayWorld world,
            IReadOnlyList<RuntimeCommand> commands,
            RuntimeEventQueue<GameplayRuntimeEvent> events,
            GameplayCommandExecutionState commandState = null,
            GameplayComponentWorld componentWorld = null)
        {
            if (double.IsNaN(deltaTime) || double.IsInfinity(deltaTime) || deltaTime < 0d)
                throw new ArgumentOutOfRangeException(nameof(deltaTime), "Delta time must be finite and non-negative.");
            if (double.IsNaN(elapsedTime) || double.IsInfinity(elapsedTime) || elapsedTime < 0d)
                throw new ArgumentOutOfRangeException(nameof(elapsedTime), "Elapsed time must be finite and non-negative.");

            Frame = frame;
            DeltaTime = deltaTime;
            ElapsedTime = elapsedTime;
            World = world ?? throw new ArgumentNullException(nameof(world));
            Commands = commands ?? throw new ArgumentNullException(nameof(commands));
            Events = events ?? throw new ArgumentNullException(nameof(events));
            if (componentWorld != null && !ReferenceEquals(Events, componentWorld.Events))
            {
                throw new ArgumentException(
                    "Gameplay system context events must be the same queue as componentWorld.Events.",
                    nameof(componentWorld));
            }

            CommandState = commandState ?? new GameplayCommandExecutionState();
            ComponentWorld = componentWorld;
        }

        public RuntimeFrame Frame { get; }
        public double DeltaTime { get; }
        public double ElapsedTime { get; }
        public GameplayWorld World { get; }
        public IReadOnlyList<RuntimeCommand> Commands { get; }
        public RuntimeEventQueue<GameplayRuntimeEvent> Events { get; }
        public GameplayCommandExecutionState CommandState { get; }
        public GameplayComponentWorld ComponentWorld { get; }
    }
}
