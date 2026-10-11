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

        internal Type GetStoreComponentType(int index)
        {
            return Registry.GetStoreComponentType(index);
        }

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

        /// <summary>Copies the canonical live-entity snapshot into caller-owned storage.</summary>
        /// <remarks>NoAlloc.</remarks>
        public bool TryCopyEntitySnapshot(Span<GameplayEntityId> destination, out int written)
        {
            return Registry.TryCopyEntitySnapshot(destination, out written);
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
