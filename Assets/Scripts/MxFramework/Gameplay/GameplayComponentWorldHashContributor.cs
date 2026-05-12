using System;
using MxFramework.Runtime;

namespace MxFramework.Gameplay
{
    /// <summary>Contributes schema-backed GameplayComponentWorld state to the runtime result hash.</summary>
    public sealed class GameplayComponentWorldHashContributor : IRuntimeHashContributor
    {
        public const string StableContributorId = "mxframework.gameplay.component-world";

        private readonly GameplayComponentWorld _world;

        public GameplayComponentWorldHashContributor(GameplayComponentWorld world)
            : this(StableContributorId, world)
        {
        }

        public GameplayComponentWorldHashContributor(string contributorId, GameplayComponentWorld world)
        {
            if (string.IsNullOrEmpty(contributorId))
                throw new ArgumentException("Gameplay component world hash contributor id cannot be null or empty.", nameof(contributorId));

            ContributorId = contributorId;
            _world = world ?? throw new ArgumentNullException(nameof(world));
        }

        public string ContributorId { get; }

        public void Contribute(RuntimeHashContext context, RuntimeHashAccumulator accumulator)
        {
            if (accumulator == null)
                throw new ArgumentNullException(nameof(accumulator));

            GameplayEntityId[] entities = _world.CreateEntitySnapshot();
            GameplayComponentHashAdapter[] adapters = _world.Schemas.CreateHashAdapters();

            accumulator.AddInt("gameplay.componentWorld.entity.count", entities.Length);

            for (int entityIndex = 0; entityIndex < entities.Length; entityIndex++)
            {
                GameplayEntityId entityId = entities[entityIndex];
                accumulator.AddInt("gameplay.componentWorld.entity.index", entityId.Index);
                accumulator.AddInt("gameplay.componentWorld.entity.generation", entityId.Generation);

                for (int schemaIndex = 0; schemaIndex < adapters.Length; schemaIndex++)
                    AddComponentHash(accumulator, adapters[schemaIndex], entityId);
            }
        }

        private void AddComponentHash(
            RuntimeHashAccumulator accumulator,
            GameplayComponentHashAdapter adapter,
            GameplayEntityId entityId)
        {
            if (!adapter.Contains(_world.Registry, entityId))
                return;

            GameplayComponentSchema schema = adapter.Schema;
            accumulator.AddStringStable("gameplay.componentWorld.component.schemaId", schema.StableId);
            accumulator.AddInt("gameplay.componentWorld.component.schemaVersion", schema.Version);
            accumulator.AddInt("gameplay.componentWorld.component.entity.index", entityId.Index);
            accumulator.AddInt("gameplay.componentWorld.component.entity.generation", entityId.Generation);

            if (!adapter.TryWriteHash(_world.Registry, entityId, accumulator))
            {
                throw new InvalidOperationException(
                    "Gameplay component hash writer failed after component presence check. Schema="
                    + schema.StableId
                    + ", Entity="
                    + entityId);
            }
        }
    }
}
