using System;
using System.Collections.Generic;
using MxFramework.Runtime;

namespace MxFramework.Gameplay
{
    public sealed class GameplayComponentWorld
    {
        public GameplayComponentWorld()
            : this(null, null, null)
        {
        }

        public GameplayComponentWorld(
            GameplayComponentRegistry registry,
            RuntimeEventQueue<GameplayRuntimeEvent> events)
            : this(registry, events, null)
        {
        }

        public GameplayComponentWorld(
            GameplayComponentRegistry registry,
            RuntimeEventQueue<GameplayRuntimeEvent> events,
            GameplayComponentSchemaRegistry schemas)
        {
            Registry = registry ?? new GameplayComponentRegistry();
            Events = events ?? new RuntimeEventQueue<GameplayRuntimeEvent>();
            Schemas = schemas ?? new GameplayComponentSchemaRegistry();
        }

        public GameplayComponentRegistry Registry { get; }
        public RuntimeEventQueue<GameplayRuntimeEvent> Events { get; }
        public GameplayComponentSchemaRegistry Schemas { get; }
        public int CountAlive => Registry.CountAlive;
        public int StoreCount => Registry.StoreCount;
        public int PendingEventCount => Events.PendingCount;

        public GameplayEntityId CreateEntity()
        {
            return Registry.CreateEntity();
        }

        public bool DestroyEntity(GameplayEntityId entityId)
        {
            return Registry.DestroyEntity(entityId);
        }

        public bool IsAlive(GameplayEntityId entityId)
        {
            return Registry.IsAlive(entityId);
        }

        public GameplayEntityId[] CreateEntitySnapshot()
        {
            return Registry.CreateEntitySnapshot();
        }

        public GameplayComponentStore<T> CreateStore<T>() where T : struct, IGameplayComponent
        {
            return Registry.CreateStore<T>();
        }

        public GameplayComponentStore<T> GetOrCreateStore<T>() where T : struct, IGameplayComponent
        {
            return Registry.GetOrCreateStore<T>();
        }

        public bool TryGetStore<T>(out GameplayComponentStore<T> store) where T : struct, IGameplayComponent
        {
            return Registry.TryGetStore(out store);
        }

        public void EnqueueEvent(in GameplayRuntimeEvent evt)
        {
            Events.Enqueue(evt.Frame, evt);
        }

        public int DrainEvents(RuntimeFrame frame, List<GameplayRuntimeEvent> output)
        {
            return Events.Drain(frame, output);
        }

        public GameplayComponentWorldSnapshot CreateSnapshot()
        {
            return new GameplayComponentWorldSnapshot(
                Registry.CountAlive,
                Registry.StoreCount,
                Events.PendingCount);
        }

        public GameplayComponentWorldDiagnosticSnapshot CreateDiagnosticSnapshot()
        {
            return new GameplayComponentWorldDiagnostics().BuildSnapshot(this);
        }

        public void Clear()
        {
            Registry.Clear();
            Events.Clear();
        }
    }
}
