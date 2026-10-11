using System;
using System.Collections.Generic;
using System.IO;
using MxFramework.CharacterApplication;
using MxFramework.CharacterRuntimeSpawn;
using MxFramework.CharacterRuntimeSpawn.Unity;
using MxFramework.Resources;
using UnityEngine;

namespace MxFramework.Game.CharacterTest
{
    internal static class CharacterTestPlayableCharacterCatalog
    {
        private const string GeneratedCharacterPackagesRoot =
            "Assets/MxFrameworkGenerated/CharacterPackages";
        private static IReadOnlyList<CharacterTestCharacterDefinition> s_cachedDefinitions;

        // Entering Play Mode must see newly imported content even when domain reload is disabled.
        // Ordinary registry queries share the snapshot for the remainder of the session.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetForPlaySession()
        {
            s_cachedDefinitions = null;
            CharacterTestGeneratedPackageSource.InvalidateCache();
        }

        public static IReadOnlyList<CharacterTestCharacterDefinition> Discover()
        {
            if (s_cachedDefinitions == null)
                s_cachedDefinitions = DiscoverUncached();
            return s_cachedDefinitions;
        }

        /// <summary>
        /// Rebuilds the low-frequency playable catalog after generated character packages change.
        /// AllocByDesign: intended for Editor reload and explicit content refresh boundaries.
        /// </summary>
        internal static IReadOnlyList<CharacterTestCharacterDefinition> Refresh()
        {
            CharacterTestGeneratedPackageSource.InvalidateCache();
            s_cachedDefinitions = DiscoverUncached();
            return s_cachedDefinitions;
        }

        private static IReadOnlyList<CharacterTestCharacterDefinition> DiscoverUncached()
        {
            var definitions = new List<CharacterTestCharacterDefinition>();
            var packageIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            string[] packageRoots = DiscoverPackageRoots();
            for (int i = 0; i < packageRoots.Length; i++)
            {
                if (!TryCreateDefinition(packageRoots[i], out CharacterTestCharacterDefinition definition))
                    continue;

                string packageId = ResolvePackageId(packageRoots[i]);
                if (!packageIds.Add(packageId) || ContainsCharacterId(definitions, definition.CharacterId))
                    continue;
                definitions.Add(definition);
            }

            definitions.Sort(CompareDefinitions);
            return definitions;
        }

        internal static int ComputeCharacterId(string packageId)
        {
            if (!string.IsNullOrWhiteSpace(packageId))
            {
                CharacterImportedPackage imported = CharacterTestGeneratedPackageSource.LoadPackage(
                    GeneratedCharacterPackagesRoot + "/" + packageId.Trim());
                if (imported?.Configs?.RuntimeCharacterId > 0)
                    return imported.Configs.RuntimeCharacterId;
            }

            return ComputeLegacyCharacterId(packageId);
        }

        internal static int ComputeLegacyCharacterId(string packageId)
        {
            if (string.Equals(
                    packageId,
                    CharacterTestCharacterSpawnResources.DefaultPlayablePackageId,
                    StringComparison.OrdinalIgnoreCase))
            {
                return CharacterTestCharacterDefinitionRegistry.DefaultPlayableCharacterId;
            }

            unchecked
            {
                uint hash = 2166136261u;
                string normalized = (packageId ?? string.Empty).Trim().ToLowerInvariant();
                for (int i = 0; i < normalized.Length; i++)
                {
                    hash ^= normalized[i];
                    hash *= 16777619u;
                }

                int id = (int)(hash & 0x3fffffff) + 10000;
                return id == CharacterTestCharacterDefinitionRegistry.DefaultPlayableCharacterId
                    ? id + 1
                    : id;
            }
        }

        private static string[] DiscoverPackageRoots()
        {
            var roots = new List<string>();
            AddPackageRoots(roots, GeneratedCharacterPackagesRoot);
            string streamingRoot = Path.Combine(
                Application.streamingAssetsPath,
                "MxFramework",
                "Generated",
                "CharacterPackages");
            if (Directory.Exists(streamingRoot))
            {
                string[] streamingDirectories = Directory.GetDirectories(streamingRoot);
                Array.Sort(streamingDirectories, StringComparer.OrdinalIgnoreCase);
                for (int i = 0; i < streamingDirectories.Length; i++)
                {
                    string packageId = Path.GetFileName(streamingDirectories[i]);
                    string packageRoot = GeneratedCharacterPackagesRoot + "/" + packageId;
                    if (!ContainsRoot(roots, packageRoot))
                        roots.Add(packageRoot);
                }
            }

            roots.Sort(StringComparer.OrdinalIgnoreCase);
            return roots.ToArray();
        }

        private static void AddPackageRoots(List<string> roots, string physicalRoot)
        {
            if (!Directory.Exists(physicalRoot))
                return;

            string[] directories = Directory.GetDirectories(physicalRoot);
            Array.Sort(directories, StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < directories.Length; i++)
            {
                string normalized = directories[i].Replace('\\', '/');
                if (!ContainsRoot(roots, normalized))
                    roots.Add(normalized);
            }
        }

        private static bool TryCreateDefinition(
            string packageRoot,
            out CharacterTestCharacterDefinition definition)
        {
            definition = default;
            CharacterImportedPackage package = CharacterTestGeneratedPackageSource.LoadPackage(packageRoot);
            if (package == null || string.IsNullOrWhiteSpace(package.PackageId))
                return false;
            // CharacterStudio may publish an import-ready presentation package before gameplay body
            // authoring. Humanoids normally declare BodyParts; creatures may instead author an explicit
            // core movement capsule and intentionally omit body-part hurtboxes.
            if (!HasPlayableBody(package))
                return false;
            if (!CharacterRuntimeAnimationResources.TryCreateBinding(
                    package,
                    out CharacterRuntimeAnimationBinding animationBinding,
                    out CharacterRuntimeSpawnResult spawnResult,
                    packageRoot))
            {
                Debug.LogError(
                    "MxFramework CharacterTest: generated character package was excluded because its canonical "
                    + "animation profile could not be resolved. package=" + package.PackageId
                    + " issues=" + FormatIssues(spawnResult));
                return false;
            }

            string displayName = ReadDisplayName(packageRoot, package.PackageId);
            bool isDefaultPlayable = string.Equals(
                package.PackageId,
                CharacterTestCharacterSpawnResources.DefaultPlayablePackageId,
                StringComparison.OrdinalIgnoreCase);
            string prefabId = CharacterRuntimePrefabResourceCatalog.GetCharacterPrefabResourceId(package.PackageId);
            ResourceKey prefabKey = isDefaultPlayable
                ? CharacterTestCharacterSpawnResources.DefaultPlayablePreviewPrefabKey
                : new ResourceKey(
                    prefabId,
                    ResourceTypeIds.GameObject,
                    variant: "default",
                    packageId: package.PackageId);
            definition = new CharacterTestCharacterDefinition(
                ComputeCharacterId(package.PackageId),
                displayName,
                prefabKey,
                defaultTeamId: 1,
                animationBinding,
                CharacterTestCharacterDefinitionRegistry.CreatePhysicsProfile(package),
                packageRoot,
                ResolveGameplayCameraProfile(package),
                ReadAttributes(package), package.TacticalDefaults,
                package.Configs.FindPresentationProfile(package.Configs.Characters[0].PresentationProfileId)?.PresentationImages);
            return true;
        }

        private const string HpAttributeStableId = "attr.hp";
        private const string AttackAttributeStableId = "attr.attack";
        private const string DefenseAttributeStableId = "attr.defense";

        /// <summary>
        /// Reads the attribute values the package actually authored. Missing entries stay zero so the
        /// caller can distinguish "not authored" from a real value instead of inventing a default.
        /// </summary>
        private static CharacterTestCharacterAttributes ReadAttributes(CharacterImportedPackage package)
        {
            CharacterConfig character = package?.Configs?.Characters != null && package.Configs.Characters.Length > 0
                ? package.Configs.Characters[0]
                : null;
            if (character == null)
                return default;
            CharacterAttributeProfileConfig profile = package.Configs.FindAttributeProfile(character.AttributeProfileId);
            if (profile?.Attributes == null)
                return default;

            int hp = 0, attack = 0, defense = 0;
            for (int i = 0; i < profile.Attributes.Length; i++)
            {
                CharacterAttributeEntry entry = profile.Attributes[i];
                if (string.Equals(entry.StableId, HpAttributeStableId, StringComparison.Ordinal))
                    hp = (int)entry.BaseValue;
                else if (string.Equals(entry.StableId, AttackAttributeStableId, StringComparison.Ordinal))
                    attack = (int)entry.BaseValue;
                else if (string.Equals(entry.StableId, DefenseAttributeStableId, StringComparison.Ordinal))
                    defense = (int)entry.BaseValue;
            }

            return new CharacterTestCharacterAttributes(hp, attack, defense);
        }

        private static CharacterGameplayCameraProfile ResolveGameplayCameraProfile(CharacterImportedPackage package)
        {
            CharacterConfig character = package?.Configs?.Characters != null && package.Configs.Characters.Length > 0
                ? package.Configs.Characters[0]
                : null;
            return character == null
                ? null
                : package.Configs.FindPresentationProfile(character.PresentationProfileId)?.GameplayCameraProfile;
        }

        private static bool HasPlayableBody(CharacterImportedPackage package)
        {
            if (package?.Configs?.BodyParts != null && package.Configs.BodyParts.Length > 0)
                return true;

            CharacterBodyGeometryRuntimeProfile body = package?.Geometry?.BodyProfile;
            return body != null
                && !string.IsNullOrWhiteSpace(body.ProfileId)
                && !string.IsNullOrWhiteSpace(body.BodyKind)
                && body.CapsuleHeight > 0f
                && body.CapsuleRadius > 0f;
        }

        private static string FormatIssues(CharacterRuntimeSpawnResult result)
        {
            if (result?.Issues == null || result.Issues.Length == 0)
                return "none";

            var text = new System.Text.StringBuilder();
            for (int i = 0; i < result.Issues.Length; i++)
            {
                CharacterRuntimeSpawnIssue issue = result.Issues[i];
                if (i > 0)
                    text.Append(" | ");
                text.Append(issue.Code).Append(':').Append(issue.Message);
            }
            return text.ToString();
        }

        private static string ReadDisplayName(string packageRoot, string fallback)
        {
            try
            {
                string physicalRoot = CharacterTestGeneratedPackageSource.ResolveGeneratedContentDirectory(packageRoot);
                string manifestPath = Path.Combine(physicalRoot, "package_cache", "manifest.json");
                if (!File.Exists(manifestPath))
                    return fallback ?? string.Empty;

                CharacterManifestMetadata manifest =
                    JsonUtility.FromJson<CharacterManifestMetadata>(File.ReadAllText(manifestPath));
                return !string.IsNullOrWhiteSpace(manifest?.displayName)
                    ? manifest.displayName
                    : fallback ?? string.Empty;
            }
            catch
            {
                return fallback ?? string.Empty;
            }
        }

        private static string ResolvePackageId(string packageRoot)
        {
            return Path.GetFileName((packageRoot ?? string.Empty).TrimEnd('/', '\\')) ?? string.Empty;
        }

        private static bool ContainsRoot(List<string> roots, string candidate)
        {
            for (int i = 0; i < roots.Count; i++)
            {
                if (string.Equals(roots[i], candidate, StringComparison.OrdinalIgnoreCase))
                    return true;
            }
            return false;
        }

        private static bool ContainsCharacterId(
            List<CharacterTestCharacterDefinition> definitions,
            int characterId)
        {
            for (int i = 0; i < definitions.Count; i++)
            {
                if (definitions[i].CharacterId == characterId)
                    return true;
            }
            return false;
        }

        private static int CompareDefinitions(
            CharacterTestCharacterDefinition left,
            CharacterTestCharacterDefinition right)
        {
            if (left.CharacterId == CharacterTestCharacterDefinitionRegistry.DefaultPlayableCharacterId)
                return right.CharacterId == left.CharacterId ? 0 : -1;
            if (right.CharacterId == CharacterTestCharacterDefinitionRegistry.DefaultPlayableCharacterId)
                return 1;
            return string.Compare(left.DisplayName, right.DisplayName, StringComparison.OrdinalIgnoreCase);
        }

        [Serializable]
        private sealed class CharacterManifestMetadata
        {
            public string displayName = string.Empty;
        }
    }
}
