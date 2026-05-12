using System;
using System.Collections.Generic;

namespace MxFramework.Gameplay
{
    public sealed class GameplayComponentSpawnRegistry
    {
        private readonly Dictionary<int, GameplayComponentSpawnDefinition> _definitionsById =
            new Dictionary<int, GameplayComponentSpawnDefinition>();

        private readonly Dictionary<string, GameplayComponentSpawnDefinition> _definitionsByStableId =
            new Dictionary<string, GameplayComponentSpawnDefinition>(StringComparer.Ordinal);

        public int Count => _definitionsById.Count;

        public void Register(GameplayComponentSpawnDefinition definition)
        {
            if (definition == null)
                throw new ArgumentNullException(nameof(definition));
            if (_definitionsById.ContainsKey(definition.DefinitionId))
                throw new InvalidOperationException("Gameplay component spawn definition id is already registered: " + definition.DefinitionId);
            if (_definitionsByStableId.ContainsKey(definition.StableId))
                throw new InvalidOperationException("Gameplay component spawn stable id is already registered: " + definition.StableId);

            _definitionsById.Add(definition.DefinitionId, definition);
            _definitionsByStableId.Add(definition.StableId, definition);
        }

        public bool TryGet(int definitionId, out GameplayComponentSpawnDefinition definition)
        {
            if (definitionId <= 0)
            {
                definition = null;
                return false;
            }

            return _definitionsById.TryGetValue(definitionId, out definition);
        }

        public GameplayComponentSpawnDefinition[] CreateSnapshot()
        {
            if (_definitionsById.Count == 0)
                return Array.Empty<GameplayComponentSpawnDefinition>();

            var definitions = new GameplayComponentSpawnDefinition[_definitionsById.Count];
            int index = 0;
            foreach (KeyValuePair<int, GameplayComponentSpawnDefinition> pair in _definitionsById)
                definitions[index++] = pair.Value;

            Array.Sort(definitions, CompareDefinitions);
            return definitions;
        }

        public void Clear()
        {
            _definitionsById.Clear();
            _definitionsByStableId.Clear();
        }

        internal static bool IsValidStableId(string stableId)
        {
            if (string.IsNullOrWhiteSpace(stableId))
                return false;
            if (!string.Equals(stableId, stableId.Trim(), StringComparison.Ordinal))
                return false;
            if (stableId[0] == '.' || stableId[stableId.Length - 1] == '.')
                return false;

            bool hasDot = false;
            bool previousDot = false;
            for (int i = 0; i < stableId.Length; i++)
            {
                char c = stableId[i];
                if (c == '.')
                {
                    if (previousDot)
                        return false;

                    hasDot = true;
                    previousDot = true;
                    continue;
                }

                previousDot = false;
                bool valid = (c >= 'a' && c <= 'z')
                             || (c >= '0' && c <= '9')
                             || c == '_'
                             || c == '-';
                if (!valid)
                    return false;
            }

            return hasDot;
        }

        private static int CompareDefinitions(
            GameplayComponentSpawnDefinition left,
            GameplayComponentSpawnDefinition right)
        {
            return left.DefinitionId.CompareTo(right.DefinitionId);
        }
    }
}
