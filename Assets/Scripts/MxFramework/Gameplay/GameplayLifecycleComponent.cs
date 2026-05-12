using System;

namespace MxFramework.Gameplay
{
    public readonly struct GameplayLifecycleComponent : IGameplayComponent, IEquatable<GameplayLifecycleComponent>
    {
        public GameplayLifecycleComponent(GameplayLifecycleState state)
        {
            State = state;
        }

        public GameplayLifecycleState State { get; }
        public bool IsAlive => State == GameplayLifecycleState.Alive;
        public bool IsTerminal => State == GameplayLifecycleState.Destroyed;

        public static GameplayLifecycleComponent Alive => new GameplayLifecycleComponent(GameplayLifecycleState.Alive);
        public static GameplayLifecycleComponent PendingDestroy => new GameplayLifecycleComponent(GameplayLifecycleState.PendingDestroy);
        public static GameplayLifecycleComponent Destroyed => new GameplayLifecycleComponent(GameplayLifecycleState.Destroyed);

        public bool Equals(GameplayLifecycleComponent other)
        {
            return State == other.State;
        }

        public override bool Equals(object obj)
        {
            return obj is GameplayLifecycleComponent other && Equals(other);
        }

        public override int GetHashCode()
        {
            return (int)State;
        }
    }
}
