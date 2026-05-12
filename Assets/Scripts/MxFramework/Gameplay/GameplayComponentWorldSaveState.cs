using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using MxFramework.Runtime;

namespace MxFramework.Gameplay
{
    public sealed class GameplayComponentWorldSaveState
    {
        public const int CurrentSchemaVersion = 1;

        public GameplayComponentWorldSaveState(
            int schemaVersion,
            IReadOnlyList<GameplayComponentEntitySaveState> entities,
            IReadOnlyList<GameplayComponentStoreSaveState> componentStores)
        {
            SchemaVersion = schemaVersion;
            Entities = CopyList(entities);
            ComponentStores = CopyList(componentStores);
        }

        public int SchemaVersion { get; }
        public IReadOnlyList<GameplayComponentEntitySaveState> Entities { get; }
        public IReadOnlyList<GameplayComponentStoreSaveState> ComponentStores { get; }

        internal static ReadOnlyCollection<T> CopyList<T>(IReadOnlyList<T> source)
        {
            if (source == null || source.Count == 0)
                return new ReadOnlyCollection<T>(new List<T>());

            var copy = new List<T>(source.Count);
            for (int i = 0; i < source.Count; i++)
                copy.Add(source[i]);

            return new ReadOnlyCollection<T>(copy);
        }
    }

    public sealed class GameplayComponentEntitySaveState
    {
        public GameplayComponentEntitySaveState(int index, int generation)
        {
            Index = index;
            Generation = generation;
        }

        public int Index { get; }
        public int Generation { get; }
    }

    public sealed class GameplayComponentStoreSaveState
    {
        public GameplayComponentStoreSaveState(
            string schemaId,
            int schemaVersion,
            IReadOnlyList<GameplayComponentEntrySaveState> entries)
        {
            SchemaId = schemaId ?? string.Empty;
            SchemaVersion = schemaVersion;
            Entries = GameplayComponentWorldSaveState.CopyList(entries);
        }

        public string SchemaId { get; }
        public int SchemaVersion { get; }
        public IReadOnlyList<GameplayComponentEntrySaveState> Entries { get; }
    }

    public sealed class GameplayComponentEntrySaveState
    {
        public GameplayComponentEntrySaveState(
            int entityIndex,
            int entityGeneration,
            RuntimeCustomState payload)
        {
            EntityIndex = entityIndex;
            EntityGeneration = entityGeneration;
            Payload = payload;
        }

        public int EntityIndex { get; }
        public int EntityGeneration { get; }
        public RuntimeCustomState Payload { get; }
    }
}
