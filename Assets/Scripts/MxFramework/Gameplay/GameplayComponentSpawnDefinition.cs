using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace MxFramework.Gameplay
{
    public sealed class GameplayComponentSpawnDefinition
    {
        private readonly IGameplayComponentSpawnInitializer[] _initializers;
        private readonly ReadOnlyCollection<IGameplayComponentSpawnInitializer> _initializersView;

        public GameplayComponentSpawnDefinition(
            int definitionId,
            string stableId,
            int schemaVersion,
            IReadOnlyList<IGameplayComponentSpawnInitializer> initializers)
        {
            if (definitionId <= 0)
                throw new ArgumentOutOfRangeException(nameof(definitionId), "Gameplay component spawn definition id must be greater than zero.");
            if (!GameplayComponentSpawnRegistry.IsValidStableId(stableId))
                throw new ArgumentException("Gameplay component spawn stable id must be a lowercase dotted id.", nameof(stableId));
            if (schemaVersion <= 0)
                throw new ArgumentOutOfRangeException(nameof(schemaVersion), "Gameplay component spawn schema version must be greater than zero.");

            DefinitionId = definitionId;
            StableId = stableId;
            SchemaVersion = schemaVersion;
            _initializers = CopyInitializers(initializers);
            _initializersView = Array.AsReadOnly(_initializers);
        }

        public int DefinitionId { get; }
        public string StableId { get; }
        public int SchemaVersion { get; }
        public IReadOnlyList<IGameplayComponentSpawnInitializer> Initializers => _initializersView;

        private static IGameplayComponentSpawnInitializer[] CopyInitializers(
            IReadOnlyList<IGameplayComponentSpawnInitializer> initializers)
        {
            if (initializers == null || initializers.Count == 0)
                return Array.Empty<IGameplayComponentSpawnInitializer>();

            var copy = new IGameplayComponentSpawnInitializer[initializers.Count];
            var seenSchemaIds = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < initializers.Count; i++)
            {
                IGameplayComponentSpawnInitializer initializer = initializers[i];
                if (initializer == null)
                    throw new ArgumentException("Gameplay component spawn initializer cannot be null.", nameof(initializers));
                if (string.IsNullOrWhiteSpace(initializer.SchemaId))
                    throw new ArgumentException("Gameplay component spawn initializer schema id cannot be empty.", nameof(initializers));
                if (!seenSchemaIds.Add(initializer.SchemaId))
                    throw new ArgumentException("Gameplay component spawn initializer schema id is duplicated: " + initializer.SchemaId, nameof(initializers));

                copy[i] = initializer;
            }

            return copy;
        }
    }
}
