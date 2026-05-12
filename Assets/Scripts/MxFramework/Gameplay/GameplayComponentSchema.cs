using System;

namespace MxFramework.Gameplay
{
    public readonly struct GameplayComponentSchema : IEquatable<GameplayComponentSchema>
    {
        public GameplayComponentSchema(
            string stableId,
            int version,
            Type componentType,
            string displayName = "",
            bool supportsDiagnostics = false,
            bool supportsHash = false,
            bool supportsSaveState = false)
        {
            ValidateStableId(stableId);
            if (version <= 0)
                throw new ArgumentOutOfRangeException(nameof(version), "Gameplay component schema version must be positive.");
            if (componentType == null)
                throw new ArgumentNullException(nameof(componentType));
            if (!typeof(IGameplayComponent).IsAssignableFrom(componentType))
                throw new ArgumentException("Gameplay component schema type must implement IGameplayComponent.", nameof(componentType));
            if (!componentType.IsValueType)
                throw new ArgumentException("Gameplay component schema type must be a value type component.", nameof(componentType));
            if (componentType.ContainsGenericParameters)
                throw new ArgumentException("Gameplay component schema type cannot contain open generic parameters.", nameof(componentType));

            StableId = stableId;
            Version = version;
            ComponentType = componentType;
            DisplayName = string.IsNullOrEmpty(displayName) ? stableId : displayName;
            SupportsDiagnostics = supportsDiagnostics;
            SupportsHash = supportsHash;
            SupportsSaveState = supportsSaveState;
        }

        public string StableId { get; }
        public int Version { get; }
        public Type ComponentType { get; }
        public string DisplayName { get; }
        public bool SupportsDiagnostics { get; }
        public bool SupportsHash { get; }
        public bool SupportsSaveState { get; }

        private static void ValidateStableId(string stableId)
        {
            if (string.IsNullOrWhiteSpace(stableId))
                throw new ArgumentException("Gameplay component schema stable id cannot be null or empty.", nameof(stableId));
            if (stableId.Trim() != stableId)
                throw new ArgumentException("Gameplay component schema stable id cannot contain leading or trailing whitespace.", nameof(stableId));

            bool previousWasDot = false;
            for (int i = 0; i < stableId.Length; i++)
            {
                char c = stableId[i];
                bool valid = c >= 'a' && c <= 'z'
                    || c >= '0' && c <= '9'
                    || c == '-'
                    || c == '_'
                    || c == '.';
                if (!valid)
                    throw new ArgumentException("Gameplay component schema stable id must use lowercase dotted id characters.", nameof(stableId));
                if (i == 0 && c == '.' || i == stableId.Length - 1 && c == '.')
                    throw new ArgumentException("Gameplay component schema stable id cannot start or end with '.'.", nameof(stableId));
                if (c == '.' && previousWasDot)
                    throw new ArgumentException("Gameplay component schema stable id cannot contain empty dotted segments.", nameof(stableId));

                previousWasDot = c == '.';
            }
        }

        public bool Equals(GameplayComponentSchema other)
        {
            return string.Equals(StableId, other.StableId, StringComparison.Ordinal)
                && Version == other.Version
                && ComponentType == other.ComponentType
                && string.Equals(DisplayName, other.DisplayName, StringComparison.Ordinal)
                && SupportsDiagnostics == other.SupportsDiagnostics
                && SupportsHash == other.SupportsHash
                && SupportsSaveState == other.SupportsSaveState;
        }

        public override bool Equals(object obj)
        {
            return obj is GameplayComponentSchema other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = StableId == null ? 0 : StableId.GetHashCode();
                hash = (hash * 397) ^ Version;
                hash = (hash * 397) ^ (ComponentType == null ? 0 : ComponentType.GetHashCode());
                hash = (hash * 397) ^ (DisplayName == null ? 0 : DisplayName.GetHashCode());
                hash = (hash * 397) ^ (SupportsDiagnostics ? 1 : 0);
                hash = (hash * 397) ^ (SupportsHash ? 1 : 0);
                hash = (hash * 397) ^ (SupportsSaveState ? 1 : 0);
                return hash;
            }
        }
    }
}
