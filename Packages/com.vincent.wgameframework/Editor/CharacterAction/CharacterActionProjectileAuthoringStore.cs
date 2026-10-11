using System;
using System.Collections.Generic;
using System.IO;
using MxFramework.CharacterAction;

namespace MxFramework.Editor.CharacterAction
{
    internal sealed partial class CharacterActionProjectileAuthoringStore
    {
        public static string DefaultAbilityAuthoringPath => ResolveProjectPath(
            MxFrameworkEditorProjectPathKind.AbilityAuthoringJson);
        public static string DefaultProjectileSourceDirectoryPath => ResolveProjectPath(
            MxFrameworkEditorProjectPathKind.ProjectileSourceDirectory);
        public const string DefaultGeneratedProjectileDefinitionsPath =
            "Assets/MxFrameworkGenerated/Combat/Projectiles/projectile_definitions.json";
        public const string DefaultGeneratedProjectileSpawnProfilesPath =
            "Assets/MxFrameworkGenerated/Combat/Projectiles/projectile_spawn_profiles.json";
        public const string DefaultGeneratedProjectilePresentationBindingsPath =
            "Assets/MxFrameworkGenerated/Combat/Projectiles/projectile_presentation_bindings.json";
        private const int CharacterTestDynamicCharacterLayerMask = 1 << 8;
        private const int CharacterTestStaticEnvironmentLayerMask = 1 << 1;

        private readonly CharacterActionProjectileAuthoringStoreOptions _options;
        private readonly Dictionary<string, CharacterActionProjectileAuthoringRecord> _byStableId;
        private readonly CharacterActionProjectileAuthoringRecord[] _records;

        private CharacterActionProjectileAuthoringStore(
            CharacterActionProjectileAuthoringStoreOptions options,
            CharacterActionProjectileAuthoringRecord[] records,
            string[] diagnostics)
        {
            _options = options ?? CharacterActionProjectileAuthoringStoreOptions.Default;
            _records = records ?? Array.Empty<CharacterActionProjectileAuthoringRecord>();
            Diagnostics = diagnostics ?? Array.Empty<string>();
            _byStableId = new Dictionary<string, CharacterActionProjectileAuthoringRecord>(StringComparer.Ordinal);
            for (int i = 0; i < _records.Length; i++)
            {
                CharacterActionProjectileAuthoringRecord record = _records[i];
                if (record != null && !string.IsNullOrWhiteSpace(record.StableId))
                    _byStableId[record.StableId] = record;
            }
        }

        public IReadOnlyList<CharacterActionProjectileAuthoringRecord> Records => _records;
        public IReadOnlyList<string> Diagnostics { get; }

        public static CharacterActionProjectileAuthoringStore LoadDefault(string characterStableId = "")
        {
            return Load(CharacterActionProjectileAuthoringStoreOptions.Default, characterStableId);
        }

        private static string ResolveProjectPath(MxFrameworkEditorProjectPathKind kind)
        {
            return MxFrameworkEditorProjectPaths.TryResolve(kind, out string path)
                ? path
                : string.Empty;
        }

        private string[] CopyDiagnostics()
        {
            var diagnostics = new string[Diagnostics.Count];
            for (int i = 0; i < Diagnostics.Count; i++)
                diagnostics[i] = Diagnostics[i];
            return diagnostics;
        }

        internal static string ResolveDefaultProjectileSourceDirectory(string abilityAuthoringPath)
        {
            string directory = Path.GetDirectoryName(abilityAuthoringPath ?? string.Empty);
            return string.IsNullOrWhiteSpace(directory)
                ? "projectiles"
                : Path.Combine(directory, "projectiles");
        }

        public bool TryGetByStableId(
            string stableId,
            out CharacterActionProjectileAuthoringRecord record)
        {
            if (string.IsNullOrWhiteSpace(stableId))
            {
                record = null;
                return false;
            }

            return _byStableId.TryGetValue(stableId, out record);
        }

    }
}
