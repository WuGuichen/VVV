using System;
using MxFramework.Runtime;

namespace MxFramework.Gameplay
{
    /// <summary>Contributes schema-backed GameplayComponentWorld state to the numeric runtime result hash.</summary>
    /// <remarks>
    /// The combiner owns the root frame and the contributor envelope; this contributor writes only the
    /// world state body into the combiner's shared v2 workspace. It owns entity structure and presence
    /// detection only: each entity element carries its identity and a components array, and each present
    /// component element is delegated to its descriptor, which opens the schema-scoped component object
    /// (encoding the frozen sub-schema ids 2101-2107) and writes entity identity plus the payload. An
    /// absent component encodes an empty element, which the v2 accumulator distinguishes from a
    /// populated one. Every scope opened here is closed on every return path, so the workspace is always
    /// handed back at depth 0.
    /// </remarks>
    public sealed class GameplayComponentWorldHashContributor : IRuntimeHashContributor
    {
        /// <summary>Stable string identity retained for diagnostics and logging only; never hashed.</summary>
        public const string StableContributorId = "mxframework.gameplay.component-world";

        /// <summary>Hash schema version for the component world state body.</summary>
        public const int ComponentWorldHashSchemaVersion = 2;

        private readonly string _stableContributorId;
        private readonly GameplayComponentWorld _world;

        public GameplayComponentWorldHashContributor(GameplayComponentWorld world)
            : this(StableContributorId, world)
        {
        }

        public GameplayComponentWorldHashContributor(string stableContributorId, GameplayComponentWorld world)
        {
            if (string.IsNullOrEmpty(stableContributorId))
                throw new ArgumentException("Gameplay component world hash contributor id cannot be null or empty.", nameof(stableContributorId));

            _stableContributorId = stableContributorId;
            _world = world ?? throw new ArgumentNullException(nameof(world));
        }

        /// <summary>Stable string contributor identity for diagnostics and logging.</summary>
        public string DiagnosticContributorId => _stableContributorId ?? StableContributorId;

        /// <summary>Permanent integer contributor identity.</summary>
        public int ContributorId => RuntimeHashContributorIds.GameplayComponentWorld;

        /// <summary>Frozen hash schema id for the component world state body.</summary>
        public int HashSchemaId => RuntimeHashSchemaIds.GameplayComponentWorld;

        /// <summary>Frozen hash schema version.</summary>
        public int HashSchemaVersion => ComponentWorldHashSchemaVersion;

        /// <summary>Frozen field numbers for the component world state body.</summary>
        private static class Fields
        {
            public const int EntityCount = 1;
            public const int Entities = 2;
            public const int EntityIndex = 3;
            public const int EntityGeneration = 4;
            public const int Components = 5;
            public const int Allocation = 6;
            public const int SlotGeneration = 7;
            public const int SlotAlive = 8;
            public const int FreeIndices = 9;
            public const int FreeIndex = 10;
        }

        public void Contribute(RuntimeHashContext context, RuntimeHashAccumulator accumulator)
        {
            if (accumulator == null)
                throw new ArgumentNullException(nameof(accumulator));

            GameplayEntityId[] entities = _world.CreateEntitySnapshot();
            GameplayComponentHashAdapter[] adapters = _world.Schemas.CreateHashAdapters();

            // The combiner writes the frame before invoking contributors.
            accumulator.AddInt(Fields.EntityCount, entities.Length);
            accumulator.BeginArray(Fields.Entities, entities.Length);
            for (int entityIndex = 0; entityIndex < entities.Length; entityIndex++)
            {
                GameplayEntityId entityId = entities[entityIndex];
                accumulator.BeginElement(entityIndex);
                accumulator.AddInt(Fields.EntityIndex, entityId.Index);
                accumulator.AddInt(Fields.EntityGeneration, entityId.Generation);
                accumulator.BeginArray(Fields.Components, adapters.Length);
                for (int schemaIndex = 0; schemaIndex < adapters.Length; schemaIndex++)
                    AddComponentHash(accumulator, schemaIndex, adapters[schemaIndex], entityId);
                accumulator.EndArray();
                accumulator.EndElement();
            }
            accumulator.EndArray();
            GameplayEntityLifecycleSnapshot allocation = _world.Registry.Lifecycle.CaptureAllocationState();
            accumulator.BeginArray(Fields.Allocation, allocation.Generations.Count);
            for (int i = 0; i < allocation.Generations.Count; i++)
            {
                accumulator.BeginElement(i);
                accumulator.AddInt(Fields.SlotGeneration, allocation.Generations[i]);
                accumulator.AddInt(Fields.SlotAlive, allocation.Alive[i] ? 1 : 0);
                accumulator.EndElement();
            }
            accumulator.EndArray();
            accumulator.BeginArray(Fields.FreeIndices, allocation.FreeIndices.Count);
            for (int i = 0; i < allocation.FreeIndices.Count; i++)
            {
                accumulator.BeginElement(i);
                accumulator.AddInt(Fields.FreeIndex, allocation.FreeIndices[i]);
                accumulator.EndElement();
            }
            accumulator.EndArray();
        }

        private void AddComponentHash(
            RuntimeHashAccumulator accumulator,
            int schemaIndex,
            GameplayComponentHashAdapter adapter,
            GameplayEntityId entityId)
        {
            GameplayComponentSchema schema = adapter.Schema;

            accumulator.BeginElement(schemaIndex);

            if (!adapter.Contains(_world.Registry, entityId))
            {
                // Absent components encode an empty element; present descriptors open their own
                // schema-scoped component object and write entity identity plus the payload.
                accumulator.EndElement();
                return;
            }

            if (!adapter.TryWriteHash(_world.Registry, entityId, accumulator))
                throw new InvalidOperationException(
                    "Gameplay component hash writer failed after component presence check. Schema="
                    + schema.StableId
                    + ", Entity="
                    + entityId);

            accumulator.EndElement();
        }
    }
}
